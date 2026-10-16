using UnityEngine;

namespace Icaro
{
    // Marca de aviso no chão: um círculo com borda que vai se enchendo até o ataque cair ali (chuva de esporos,
    // meteoros, poças). Só o visual: o dano é do chefe, no fim do aviso. Apoia no chão abaixo do ponto e some
    // sozinha. Reaproveitada pelo PrefabPool.
    public class GroundTelegraph : MonoBehaviour
    {
        [Tooltip("Disco que cresce do centro até a borda durante o aviso")]
        public Transform Fill;

        [Tooltip("Borda do círculo")]
        public Transform Ring;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        float m_Radius;
        float m_Duration;
        float m_Age;
        Color m_Color;
        Renderer m_FillRenderer;
        Renderer m_RingRenderer;
        MaterialPropertyBlock m_Block;

        // Mostra o aviso e devolve o ponto no chão onde ele ficou
        public static Vector3 Show(BossKit kit, Vector3 position, float radius, float duration, Color color)
        {
            Vector3 ground = GroundPoint(position);
            if (!kit || !kit.TelegraphPrefab)
                return ground;
            GroundTelegraph telegraph = PrefabPool.Get(kit.TelegraphPrefab, ground + Vector3.up * 0.05f,
                Quaternion.identity);
            telegraph.Begin(radius, duration, color);
            return ground;
        }

        void Awake()
        {
            m_FillRenderer = Fill ? Fill.GetComponent<Renderer>() : null;
            m_RingRenderer = Ring ? Ring.GetComponent<Renderer>() : null;
            m_Block = new MaterialPropertyBlock();
        }

        void Begin(float radius, float duration, Color color)
        {
            m_Radius = radius;
            m_Duration = Mathf.Max(0.05f, duration);
            m_Color = color;
            m_Age = 0f;
            Apply();
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= m_Duration + 0.15f)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            Apply();
        }

        void Apply()
        {
            float t = Mathf.Clamp01(m_Age / m_Duration);
            float diameter = m_Radius * 2f;
            if (Ring)
                Ring.localScale = new Vector3(diameter, 0.01f, diameter);
            if (Fill)
                Fill.localScale = new Vector3(diameter * t, 0.012f, diameter * t);

            float pulse = 0.75f + 0.25f * Mathf.Sin(m_Age * Mathf.Lerp(8f, 30f, t));
            Tint(m_RingRenderer, m_Color * pulse);
            Color fill = m_Color;
            fill.a *= 0.45f;
            Tint(m_FillRenderer, fill);
        }

        void Tint(Renderer renderer, Color color)
        {
            if (!renderer)
                return;
            m_Block.SetColor(k_BaseColor, color);
            renderer.SetPropertyBlock(m_Block);
        }

        // Primeiro chão abaixo do ponto (ignora inimigos e o jogador)
        public static Vector3 GroundPoint(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 20f, Vector3.down, s_Hits, 60f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = position;
            for (int i = 0; i < count; i++)
            {
                Collider collider = s_Hits[i].collider;
                if (s_Hits[i].distance >= best || collider.GetComponentInParent<EnemyStats>() ||
                    collider.GetComponentInParent<Unity.FPS.Gameplay.PlayerCharacterController>())
                    continue;
                best = s_Hits[i].distance;
                ground = s_Hits[i].point;
            }

            return ground;
        }
    }
}
