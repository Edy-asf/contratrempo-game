using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Transforma um inimigo comum em elite (o RoomManager sorteia, com a chance da sala no CombatBalance):
    // - Blindado: vida x2,5 e 20% maior (dourado).
    // - Veloz: anda 50% mais rápido e tem um pouco mais de vida (verde).
    // - Explosivo: ao morrer, marca o chão e explode depois do aviso, acertando o jogador no raio (vermelho).
    // Visual: anel colorido girando aos pés, um losango flutuando acima e uma luz da cor do modificador. Todo elite
    // deixa cair um fragmento de metrônomo (RoomManager). Valores no CombatBalance.
    public class EliteAffix : MonoBehaviour
    {
        public EliteModifier Modifier;

        static readonly Color k_Armored = new Color(1f, 0.78f, 0.2f);
        static readonly Color k_Swift = new Color(0.35f, 1f, 0.35f);
        static readonly Color k_Explosive = new Color(1f, 0.3f, 0.15f);

        CombatBalance m_Balance;
        Transform m_Ring;
        Transform m_Marker;
        float m_Height = 2f;

        public static Color ColorOf(EliteModifier modifier) => modifier switch
        {
            EliteModifier.Blindado => k_Armored,
            EliteModifier.Veloz => k_Swift,
            _ => k_Explosive,
        };

        // Chamado logo depois do EnemyStats.Configure (antes do Start do inimigo)
        public static EliteAffix Apply(GameObject enemy, EliteModifier modifier, CombatBalance balance)
        {
            var stats = enemy.GetComponent<EnemyStats>();
            var health = enemy.GetComponent<Health>();
            if (!stats || !health || modifier == EliteModifier.None)
                return null;

            stats.Elite = modifier;
            float healthFactor = 1f;
            switch (modifier)
            {
                case EliteModifier.Blindado:
                    healthFactor = balance.EliteArmoredHealth;
                    enemy.transform.localScale *= balance.EliteArmoredScale;
                    break;
                case EliteModifier.Veloz:
                    healthFactor = balance.EliteSwiftHealth;
                    stats.MoveMultiplier = balance.EliteSwiftSpeed;
                    break;
                case EliteModifier.Explosivo:
                    healthFactor = balance.EliteBlastHealth;
                    break;
            }

            health.MaxHealth *= healthFactor;
            health.CurrentHealth = health.MaxHealth;

            var affix = enemy.AddComponent<EliteAffix>();
            affix.Modifier = modifier;
            affix.m_Balance = balance;
            return affix;
        }

        void Start()
        {
            var collider = GetComponentInChildren<Collider>();
            if (collider)
                m_Height = collider.bounds.max.y - transform.position.y;
            BuildVisual(ColorOf(Modifier));

            var health = GetComponent<Health>();
            if (health && Modifier == EliteModifier.Explosivo)
                health.OnDie += Explode;
        }

        void BuildVisual(Color color)
        {
            // Anel aos pés (linha) e losango acima da cabeça, com uma luz da cor do modificador
            var ring = new GameObject("AnelElite").AddComponent<LineRenderer>();
            m_Ring = ring.transform;
            m_Ring.SetParent(transform, false);
            m_Ring.localPosition = Vector3.up * 0.1f;
            const int segments = 32;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            float radius = Mathf.Max(0.8f, m_Height * 0.4f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            ring.widthMultiplier = 0.12f;
            ring.sharedMaterial = SharedMaterial();
            ring.startColor = ring.endColor = color * 2f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(marker.GetComponent<Collider>());
            m_Marker = marker.transform;
            m_Marker.SetParent(transform, false);
            m_Marker.localScale = Vector3.one * 0.3f;
            m_Marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedMaterial();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color * 2f);
            renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = new GameObject("LuzElite").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.up * m_Height * 0.6f;
            light.type = LightType.Point;
            light.color = color;
            light.range = 4f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }

        // Material aditivo do brilho dos elites (CombatBalance.EliteMaterial)
        Material SharedMaterial() => m_Balance && m_Balance.EliteMaterial
            ? m_Balance.EliteMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        void Update()
        {
            if (m_Ring)
                m_Ring.Rotate(Vector3.up, 90f * Time.deltaTime, Space.Self);
            if (m_Marker)
            {
                m_Marker.localPosition = Vector3.up * (m_Height + 0.6f + Mathf.Sin(Time.time * 3f) * 0.15f);
                m_Marker.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            }
        }

        void Explode()
        {
            if (!m_Balance)
                return;
            DelayedBlast.Create(transform.position, m_Balance.EliteBlastRadius, m_Balance.EliteBlastDamage,
                CombatTiming.Warning(m_Balance.EliteBlastWarning), ColorOf(EliteModifier.Explosivo), m_Balance.Kit);
        }
    }
}
