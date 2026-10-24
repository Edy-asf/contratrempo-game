using UnityEngine;

namespace Icaro
{
    // Poça de ácido de chefe: um disco no chão que, enquanto o jogador pisa nele, causa dano em ticks (dano contínuo:
    // não liga a invulnerabilidade curta). Surge, dura Duration segundos e some. Reaproveitada pelo PrefabPool.
    public class AcidPuddle : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        GameObject m_Owner;
        float m_Radius;
        float m_Duration;
        float m_TickDamage;
        float m_TickInterval;
        float m_Age;
        float m_NextTick;
        Color m_Color;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        public static void Spawn(BossKit kit, Vector3 ground, float radius, float duration, float tickDamage,
            float tickInterval, GameObject owner, Color color)
        {
            if (!kit || !kit.PuddlePrefab)
                return;
            AcidPuddle puddle = PrefabPool.Get(kit.PuddlePrefab, ground + Vector3.up * 0.04f, Quaternion.identity);
            puddle.Begin(radius, duration, tickDamage, tickInterval, owner, color);
        }

        void Awake()
        {
            m_Renderer = GetComponentInChildren<Renderer>();
            m_Block = new MaterialPropertyBlock();
        }

        void Begin(float radius, float duration, float tickDamage, float tickInterval, GameObject owner, Color color)
        {
            m_Radius = radius;
            m_Duration = duration;
            m_TickDamage = tickDamage;
            m_TickInterval = Mathf.Max(0.1f, tickInterval);
            m_Owner = owner;
            m_Color = color;
            m_Age = 0f;
            m_NextTick = 0f;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= m_Duration)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            // Cresce rápido, borbulha e encolhe no fim
            float grow = Mathf.Clamp01(m_Age / 0.3f) * Mathf.Clamp01((m_Duration - m_Age) / 0.6f);
            float wobble = 1f + 0.04f * Mathf.Sin(m_Age * 9f);
            float diameter = m_Radius * 2f * grow * wobble;
            transform.localScale = new Vector3(diameter, 0.02f, diameter);
            m_Block.SetColor(k_BaseColor, m_Color * (0.8f + 0.2f * Mathf.Sin(m_Age * 5f)));
            if (m_Renderer)
                m_Renderer.SetPropertyBlock(m_Block);

            if (!BossTarget.Exists || m_Age < m_NextTick)
                return;
            bool inside = BossTarget.FlatDistance(transform.position) <= m_Radius * grow &&
                          BossTarget.Feet.y - transform.position.y < 1.2f;
            if (!inside)
                return;

            m_NextTick = m_Age + m_TickInterval;
            BossTarget.Damage(m_TickDamage, m_Owner, BossTarget.Feet, continuous: true);
        }
    }
}
