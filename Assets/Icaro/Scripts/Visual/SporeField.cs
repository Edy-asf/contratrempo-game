using UnityEngine;

namespace Icaro
{
    // Partículas de brilho que acompanham o jogador: o emissor (um ParticleSystem "Esporos_", em espaço de mundo) fica
    // centrado na câmera, numa área configurável, e a taxa de emissão mantém a quantidade pedida no ar. Assim o mapa
    // pode ser enorme sem as partículas ficarem diluídas. Ajustes no SkySettings (ArchipelagoSpores), reaplicados
    // quando mudam; o TimeAtmosphere continua acelerando e pulsando as partículas com o multiplicador. Com 'FromWater',
    // nascem na superfície do mar (a altura da raiz do bioma, onde o mar fica) e sobem; um plano de colisão na linha
    // d'água apaga as que descem abaixo dela, então nenhuma aparece dentro da água. Com a máscara do mar livre
    // (WaterSurfaceMask, gerada com o bioma), elas são soltas uma a uma só onde não há terra nem estrutura por cima
    // (nem ilhas, ruínas e destroços, nem embaixo das pontes). Com uma zona de início (SporeStartZone), só começam
    // quando o jogador entra nela (a primeira ilhota depois da praia) e dali em diante continuam.
    [RequireComponent(typeof(ParticleSystem))]
    public class SporeField : MonoBehaviour
    {
        ParticleSystem m_Particles;
        ParticleSystemRenderer m_Renderer;
        Material m_Material;
        Color m_BaseColor;
        int m_Version = -1;
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        Transform m_Camera;
        Transform m_WaterPlane;
        WaterSurfaceMask m_Mask;
        float m_Rate;
        float m_Pending;
        SporeStartZone m_Zone;
        bool m_Started;

        void Awake()
        {
            m_Particles = GetComponent<ParticleSystem>();
            m_Mask = GetComponentInParent<WaterSurfaceMask>();
            m_Zone = GetComponentInParent<SporeStartZone>();
            m_Started = !m_Zone;
            if (!m_Started)
            {
                ParticleSystem.EmissionModule emission = m_Particles.emission;
                emission.enabled = false;
                m_Particles.Clear();
            }
            // Material próprio (o brilho vai na cor dele, que passa de 1; o do Bosque continua o mesmo)
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            if (m_Renderer && m_Renderer.sharedMaterial)
            {
                m_Material = new Material(m_Renderer.sharedMaterial) { name = "Esporo_Arquipelago" };
                m_BaseColor = m_Material.HasProperty(k_BaseColor) ? m_Material.GetColor(k_BaseColor) : Color.white;
                m_Renderer.sharedMaterial = m_Material;
            }
        }

        void OnDestroy()
        {
            if (m_Material)
                Destroy(m_Material);
        }

        void LateUpdate()
        {
            SporeTuning tuning = SkySettings.Current.ArchipelagoSpores;
            if (tuning == null)
                return;
            if (m_Version != SkySettings.Version)
            {
                m_Version = SkySettings.Version;
                Apply(tuning);
            }

            if (!m_Camera)
            {
                Camera main = Camera.main;
                if (!main)
                    return;
                m_Camera = main.transform;
            }

            Vector3 eye = m_Camera.position;
            if (!m_Started && m_Zone.Contains(eye))
            {
                m_Started = true;
                Apply(tuning);
            }

            if (tuning.FromWater)
            {
                float water = WaterLevel(tuning);
                transform.position = new Vector3(eye.x, water + 0.05f, eye.z);
                if (m_WaterPlane)
                    m_WaterPlane.SetPositionAndRotation(new Vector3(eye.x, water, eye.z), Quaternion.identity);
                if (m_Mask && tuning.Enabled && m_Started)
                    EmitOverWater(tuning, eye, water);
            }
            else
                transform.position = new Vector3(eye.x, eye.y + (tuning.Height.x + tuning.Height.y) * 0.5f, eye.z);
        }

        // Solta as partículas uma a uma em pontos sorteados em volta do jogador, só onde a máscara diz que é mar livre
        void EmitOverWater(SporeTuning tuning, Vector3 eye, float water)
        {
            m_Pending = Mathf.Min(m_Pending + m_Rate * m_Particles.main.simulationSpeed * Time.deltaTime, 200f);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (m_Pending >= 1f)
            {
                m_Pending -= 1f;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Vector2 offset = Random.insideUnitCircle * tuning.Radius;
                    var point = new Vector3(eye.x + offset.x, water + 0.05f, eye.z + offset.y);
                    if (m_Mask.IsBlocked(point))
                        continue;
                    emit.position = point;
                    m_Particles.Emit(emit, 1);
                    break;
                }
            }
        }

        // O mar fica na altura da raiz do bioma (na Via, o Arquipélago inteiro é deslocado)
        float WaterLevel(SporeTuning tuning) =>
            (transform.parent ? transform.parent.position.y : 0f) + tuning.WaterLevelOffset;

        void Apply(SporeTuning tuning)
        {
            ParticleSystem.EmissionModule emission = m_Particles.emission;
            if (!tuning.Enabled || tuning.Count <= 0)
            {
                emission.enabled = false;
                m_Particles.Clear();
                return;
            }

            float lifeMin = Mathf.Max(0.5f, Mathf.Min(tuning.Lifetime.x, tuning.Lifetime.y));
            float lifeMax = Mathf.Max(lifeMin, Mathf.Max(tuning.Lifetime.x, tuning.Lifetime.y));
            ParticleSystem.MainModule main = m_Particles.main;
            main.maxParticles = Mathf.CeilToInt(tuning.Count * 1.3f) + 10;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = tuning.FromWater ? new ParticleSystem.MinMaxCurve(0f)
                : new ParticleSystem.MinMaxCurve(tuning.Speed * 0.3f, tuning.Speed * 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(tuning.Size.x, tuning.Size.y),
                Mathf.Max(tuning.Size.x, tuning.Size.y));
            var colors = new GradientColorKey[]
            {
                new GradientColorKey(tuning.ColorA, 0f),
                new GradientColorKey(tuning.ColorB, 0.5f),
                new GradientColorKey(tuning.ColorC, 1f),
            };
            var gradient = new Gradient();
            gradient.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            if (m_Material && m_Material.HasProperty(k_BaseColor))
                m_Material.SetColor(k_BaseColor, m_BaseColor * tuning.Brightness);

            // Taxa que mantém a quantidade pedida no ar (cada uma vive, em média, a vida média)
            // Com a máscara, as partículas são soltas por código (EmitOverWater) e a emissão própria fica desligada
            m_Rate = tuning.Count / ((lifeMin + lifeMax) * 0.5f);
            bool manual = tuning.FromWater && m_Mask;
            emission.enabled = !manual && m_Started;
            emission.rateOverTime = manual ? 0f : m_Rate;

            ParticleSystem.ShapeModule shape = m_Particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            float band = tuning.FromWater ? 0.1f : Mathf.Max(0.1f, Mathf.Abs(tuning.Height.y - tuning.Height.x));
            shape.scale = new Vector3(tuning.Radius * 2f, band, tuning.Radius * 2f);

            // Da água: sobem com um pouco de deriva para os lados; a ondulação quase não puxa para baixo
            ParticleSystem.VelocityOverLifetimeModule velocity = m_Particles.velocityOverLifetime;
            velocity.enabled = tuning.FromWater;
            if (tuning.FromWater)
            {
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-tuning.Speed, tuning.Speed);
                velocity.y = new ParticleSystem.MinMaxCurve(tuning.RiseSpeed * 0.6f, tuning.RiseSpeed * 1.4f);
                velocity.z = new ParticleSystem.MinMaxCurve(-tuning.Speed, tuning.Speed);
            }

            ParticleSystem.NoiseModule noise = m_Particles.noise;
            noise.enabled = tuning.Wander > 0f;
            noise.separateAxes = true;
            noise.strengthX = tuning.Wander;
            noise.strengthY = tuning.FromWater ? tuning.Wander * 0.25f : tuning.Wander;
            noise.strengthZ = tuning.Wander;

            // Plano na linha d'água: a partícula que desce abaixo dela some
            ParticleSystem.CollisionModule collision = m_Particles.collision;
            collision.enabled = tuning.FromWater;
            if (tuning.FromWater)
            {
                if (!m_WaterPlane)
                {
                    m_WaterPlane = new GameObject("Linha_Agua").transform;
                    m_WaterPlane.SetParent(transform.parent, false);
                }

                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, m_WaterPlane);
                collision.lifetimeLoss = 1f;
                collision.bounce = 0f;
                collision.dampen = 1f;
                collision.radiusScale = 0.01f;
            }
            if (!m_Particles.isPlaying)
                m_Particles.Play();
        }
    }
}
