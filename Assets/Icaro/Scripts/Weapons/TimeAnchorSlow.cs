using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Lentidão temporária num inimigo (item Munição Atordoante): enquanto dura, EnemyStats.SlowMultiplier deixa o
    // movimento mais lento (CombatTiming.Movement) e o EnemyTimeScaler aplica o mesmo ritmo a giro, animação e armas do Microgame. Um anel
    // aos pés mostra o efeito e pisca no fim. Cada acerto renova a duração (fica a lentidão mais forte). Adicionado
    // ao inimigo no primeiro acerto; com o PrefabPool, desligar a instância encerra o efeito.
    public class TimeAnchorSlow : MonoBehaviour
    {
        const int k_Segments = 40;
        const float k_BlinkTime = 0.6f;

        EnemyStats m_Stats;
        LineRenderer m_Ring;
        float m_Remaining;
        float m_Factor = 1f;
        Color m_Color;

        public bool IsSlowed => m_Remaining > 0f;

        // Deixa 'target' mais lento: 'factor' é o ritmo (0,5 = metade) por 'duration' segundos
        public static void Apply(Health target, float factor, float duration, Color color)
        {
            var stats = target ? target.GetComponent<EnemyStats>() : null;
            if (!stats || target.CurrentHealth <= 0f)
                return;

            var slow = stats.GetComponent<TimeAnchorSlow>();
            if (!slow)
                slow = stats.gameObject.AddComponent<TimeAnchorSlow>();
            slow.Begin(stats, factor, duration, color);
        }

        void Begin(EnemyStats stats, float factor, float duration, Color color)
        {
            m_Stats = stats;
            m_Factor = IsSlowed ? Mathf.Min(m_Factor, factor) : factor;
            m_Remaining = Mathf.Max(m_Remaining, duration);
            m_Color = color;
            m_Stats.SlowMultiplier = m_Factor;
            ShowRing();
        }

        void Update()
        {
            if (!IsSlowed)
                return;

            m_Remaining -= Time.deltaTime;
            if (m_Remaining <= 0f)
            {
                End();
                return;
            }

            if (m_Ring)
            {
                // Pulsa devagar (tempo ancorado) e pisca nos últimos instantes
                bool blinkOff = m_Remaining < k_BlinkTime && Mathf.Repeat(m_Remaining * 8f, 1f) < 0.5f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
                Color color = m_Color * (1.8f * pulse);
                color.a = blinkOff ? 0f : 0.9f;
                m_Ring.startColor = m_Ring.endColor = color;
                m_Ring.transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.Self);
            }
        }

        void OnDisable() => End();

        void End()
        {
            m_Remaining = 0f;
            m_Factor = 1f;
            if (m_Stats)
                m_Stats.SlowMultiplier = 1f;
            if (m_Ring)
                m_Ring.gameObject.SetActive(false);
        }

        // Anel no chão em volta do inimigo, do tamanho do colisor
        void ShowRing()
        {
            Material material = PlayerStats.Instance && PlayerStats.Instance.Catalog
                ? PlayerStats.Instance.Catalog.EffectMaterial
                : null;
            if (!material)
                return;

            if (!m_Ring)
            {
                var go = new GameObject("AnelAncora");
                go.transform.SetParent(transform, false);
                m_Ring = go.AddComponent<LineRenderer>();
                m_Ring.sharedMaterial = material;
                m_Ring.useWorldSpace = false;
                m_Ring.loop = true;
                m_Ring.positionCount = k_Segments;
                m_Ring.widthMultiplier = 0.08f;
                m_Ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Ring.receiveShadows = false;
            }

            var collider = GetComponentInChildren<Collider>();
            Bounds bounds = collider ? collider.bounds : new Bounds(transform.position, Vector3.one);
            float scale = Mathf.Max(0.01f, transform.lossyScale.x);
            float radius = (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.3f) / scale;
            m_Ring.transform.localPosition = Vector3.up * ((bounds.min.y + 0.1f - transform.position.y) / scale);
            for (int i = 0; i < k_Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / k_Segments;
                // Contorno "dentado", como um mostrador de relógio
                float r = radius * (i % 4 == 0 ? 1.12f : 1f);
                m_Ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }

            m_Ring.gameObject.SetActive(true);
        }
    }
}
