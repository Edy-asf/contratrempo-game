using UnityEngine;

namespace Icaro
{
    // Onda de choque de chefe: um anel baixo que se expande pelo chão a partir de um ponto até cruzar a arena. Quem
    // está no chão quando o anel passa leva dano (uma vez por onda); para desviar, é preciso pular por cima. Visual:
    // faixa vertical em anel (malha gerada em código) com material aditivo tingido. Reaproveitada pelo PrefabPool.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Shockwave : MonoBehaviour
    {
        const int k_Segments = 72;
        const float k_HitWidth = 0.9f;

        static Mesh s_RingMesh;
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        BossKit m_Kit;
        GameObject m_Owner;
        float m_Radius;
        float m_Speed;
        float m_MaxRadius;
        float m_Height;
        float m_Damage;
        bool m_HitPlayer;
        Color m_Color;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;

        public static Shockwave Spawn(BossKit kit, Vector3 origin, float speed, float maxRadius, float damage,
            GameObject owner, Color color, float height = 0.8f)
        {
            if (!kit || !kit.ShockwavePrefab)
                return null;
            Shockwave wave = PrefabPool.Get(kit.ShockwavePrefab, origin, Quaternion.identity);
            wave.Begin(kit, speed, maxRadius, damage, owner, color, height);
            return wave;
        }

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = RingMesh();
            m_Renderer = GetComponent<MeshRenderer>();
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Block = new MaterialPropertyBlock();
        }

        void Begin(BossKit kit, float speed, float maxRadius, float damage, GameObject owner, Color color, float height)
        {
            m_Kit = kit;
            m_Speed = speed;
            m_MaxRadius = maxRadius;
            m_Damage = damage;
            m_Owner = owner;
            m_Color = color;
            m_Height = height;
            m_Radius = 0.6f;
            m_HitPlayer = false;
            Apply();
        }

        void Update()
        {
            m_Radius += m_Speed * m_Kit.SpeedMultiplier * Time.deltaTime;
            if (m_Radius >= m_MaxRadius)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            CheckPlayer();
            Apply();
        }

        // O anel acerta quem está na faixa dele e não está no ar acima da altura da onda
        void CheckPlayer()
        {
            if (m_HitPlayer || !BossTarget.Exists)
                return;

            float distance = BossTarget.FlatDistance(transform.position);
            if (Mathf.Abs(distance - m_Radius) > k_HitWidth)
                return;

            float aboveWave = BossTarget.Feet.y - transform.position.y;
            bool jumping = !BossTarget.IsGrounded && aboveWave > m_Height * 0.5f;
            if (jumping)
                return;

            m_HitPlayer = true;
            BossTarget.Damage(m_Damage, m_Owner, BossTarget.Feet + Vector3.up * 0.3f);
        }

        void Apply()
        {
            transform.localScale = new Vector3(m_Radius, m_Height, m_Radius);
            Color color = m_Color;
            // Some nos últimos 20% do caminho
            color.a *= Mathf.Clamp01((m_MaxRadius - m_Radius) / (m_MaxRadius * 0.2f));
            m_Block.SetColor(k_BaseColor, color);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        // Faixa vertical de raio 1 e altura 1 (a escala do objeto dá o raio e a altura)
        static Mesh RingMesh()
        {
            if (s_RingMesh)
                return s_RingMesh;

            var vertices = new Vector3[(k_Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[k_Segments * 6];
            for (int i = 0; i <= k_Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / k_Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction;
                vertices[i * 2 + 1] = direction + Vector3.up;
                uvs[i * 2] = new Vector2(i / (float)k_Segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)k_Segments, 1f);
            }

            for (int i = 0; i < k_Segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            s_RingMesh = new Mesh { name = "OndaDeChoque", vertices = vertices, uv = uvs, triangles = triangles };
            s_RingMesh.RecalculateBounds();
            return s_RingMesh;
        }
    }
}
