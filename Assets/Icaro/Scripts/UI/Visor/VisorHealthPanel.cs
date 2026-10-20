using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Vida no canto inferior esquerdo do visor, no lugar do retângulo do Microgame: uma barra fina e translúcida com
    // marcas a cada SegmentHealth de vida, o valor em número e, quando houver, a barra do escudo (Escudo de Sincronia)
    // e a barreira do dash (Égide de Impulso). Ao levar dano a barra encolhe na hora e um rastro claro fica no valor
    // antigo e desce suavemente depois de TrailDelay (estilo Risk of Rain 2); ao curar, a barra sobe suave. Abaixo de
    // LowHealth a barra pulsa em vermelho. Criado pelo PlayerStats.
    public class VisorHealthPanel : MonoBehaviour
    {
        [Tooltip("Largura da barra (pixels de referência, tela de 1080 de altura)")]
        public float Width = 380f;

        [Tooltip("Vida entre uma marca e outra da barra")]
        public float SegmentHealth = 25f;

        [Tooltip("Segundos que o rastro espera antes de descer")]
        public float TrailDelay = 0.4f;

        [Tooltip("Velocidade com que o rastro alcança a barra (maior = mais rápido)")]
        public float TrailSpeed = 4f;

        [Tooltip("Fração da vida abaixo da qual a barra pulsa em vermelho")]
        [Range(0f, 1f)] public float LowHealth = 0.3f;

        static readonly Color k_Health = new Color(0.42f, 1f, 0.72f, 0.95f);
        static readonly Color k_Trail = new Color(1f, 0.93f, 0.62f, 0.9f);
        static readonly Color k_Back = new Color(0f, 0.02f, 0.04f, 0.45f);
        static readonly Color k_Shield = new Color(0.4f, 0.85f, 1f, 0.95f);
        static readonly Color k_Barrier = new Color(1f, 0.8f, 0.35f, 1f);
        static readonly Color k_Tick = new Color(0f, 0.03f, 0.05f, 0.75f);

        RectTransform m_Root;
        Image m_Glass;
        Image m_Fill;
        Image m_Trail;
        RectTransform m_BarRect;
        Image m_ShieldBack;
        Image m_ShieldFill;
        TextMeshProUGUI m_Value;
        TextMeshProUGUI m_Extra;
        readonly List<Image> m_Ticks = new List<Image>();
        float m_TickMax = -1f;

        Health m_Health;
        PlayerDefenses m_Defenses;
        float m_Shown = 1f;
        float m_TrailShown = 1f;
        float m_LastDamage = -10f;
        float m_Flash;
        int m_ShownHealth = -1, m_ShownMax = -1, m_ShownShield = -1, m_ShownBarrier = -1;
        bool m_ShownLow;

        void Update()
        {
            if (!m_Root && !Build())
                return;
            if (!m_Health && PlayerStats.Instance)
            {
                m_Health = PlayerStats.Instance.GetComponent<Health>();
                m_Defenses = PlayerStats.Instance.GetComponent<PlayerDefenses>();
                if (m_Health)
                {
                    m_Health.OnDamaged += OnDamaged;
                    m_Shown = m_TrailShown = Ratio();
                }
            }

            if (!m_Health)
                return;
            UpdateBar();
            UpdateShield();
        }

        void OnDestroy()
        {
            if (m_Health)
                m_Health.OnDamaged -= OnDamaged;
        }

        void OnDamaged(float damage, GameObject source)
        {
            m_LastDamage = Time.time;
            m_Flash = 1f;
        }

        float Ratio() => m_Health.MaxHealth > 0f ? Mathf.Clamp01(m_Health.CurrentHealth / m_Health.MaxHealth) : 0f;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.BottomLeft, out RectTransform group))
                return false;
            var anchor = Vector2.zero;
            m_Root = ItemUI.Rect("Vida", group, anchor, new Vector2(48f, 44f), new Vector2(Width + 24f, 74f));
            m_Glass = VisorUI.GlassPanel("Vidro", m_Root, anchor, Vector2.zero, m_Root.sizeDelta);

            VisorUI.Label("Titulo", m_Root, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(200f, 22f), 15f,
                TextAlignmentOptions.TopLeft, VisorUI.Dim).text = "INTEGRIDADE";
            m_Value = VisorUI.Label("Valor", m_Root, new Vector2(1f, 1f), new Vector2(-12f, -4f),
                new Vector2(220f, 30f), 24f, TextAlignmentOptions.TopRight, VisorUI.Text);
            m_Extra = VisorUI.Label("Extra", m_Root, new Vector2(0f, 1f), new Vector2(130f, -8f),
                new Vector2(160f, 22f), 15f, TextAlignmentOptions.TopLeft, k_Barrier);

            // Escudo: linha fina acima da vida
            m_ShieldFill = VisorUI.Bar("Escudo", m_Root, anchor, new Vector2(12f, 28f), new Vector2(Width, 4f), k_Back,
                k_Shield, out m_ShieldBack);

            // Vida: fundo, rastro e barra por cima
            Image back = ItemUI.Panel("Barra", m_Root, anchor, new Vector2(12f, 12f), new Vector2(Width, 12f), k_Back);
            m_BarRect = back.rectTransform;
            m_Trail = ItemUI.Fill("Rastro", m_BarRect, 0f, k_Trail);
            m_Fill = ItemUI.Fill("Vida", m_BarRect, 0f, k_Health);
            return true;
        }

        void UpdateBar()
        {
            float dt = Time.deltaTime;
            float ratio = Ratio();

            // Dano: a barra cai na hora; cura: sobe suave
            m_Shown = ratio < m_Shown ? ratio : Mathf.MoveTowards(m_Shown, ratio, dt * 1.5f);
            if (m_TrailShown < m_Shown)
                m_TrailShown = m_Shown;
            else if (Time.time - m_LastDamage > TrailDelay)
                m_TrailShown = Mathf.Lerp(m_TrailShown, m_Shown, 1f - Mathf.Exp(-dt * TrailSpeed));
            if (m_TrailShown - m_Shown < 0.002f)
                m_TrailShown = m_Shown;
            VisorUI.SetFill(m_Fill, m_Shown);
            VisorUI.SetFill(m_Trail, m_TrailShown);

            bool low = ratio <= LowHealth && ratio > 0f;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f) : 0f;
            m_Fill.color = low ? Color.Lerp(VisorUI.Danger, new Color(0.55f, 0.08f, 0.1f, 0.95f), pulse) : k_Health;
            // Texto só quando o número mostrado muda
            int health = Mathf.CeilToInt(m_Health.CurrentHealth), max = Mathf.CeilToInt(m_Health.MaxHealth);
            if (health != m_ShownHealth || max != m_ShownMax || low != m_ShownLow)
            {
                m_ShownHealth = health;
                m_ShownMax = max;
                m_ShownLow = low;
                m_Value.text = $"{health}<size=65%><color=#{ItemUI.Hex(VisorUI.Dim)}> / {max}</color></size>";
                m_Value.color = low ? VisorUI.Danger : VisorUI.Text;
            }

            // O vidro pisca ao levar dano
            m_Flash = Mathf.MoveTowards(m_Flash, 0f, dt * 4f);
            m_Glass.color = Color.Lerp(VisorUI.Glass, new Color(0.6f, 0.1f, 0.12f, 0.6f), m_Flash);

            UpdateTicks();
        }

        // Marcas finas a cada SegmentHealth de vida (refeitas quando a vida máxima muda)
        void UpdateTicks()
        {
            float max = m_Health.MaxHealth;
            if (Mathf.Approximately(max, m_TickMax))
                return;
            m_TickMax = max;
            foreach (Image tick in m_Ticks)
                Destroy(tick.gameObject);
            m_Ticks.Clear();
            if (SegmentHealth <= 0f || max <= SegmentHealth)
                return;
            for (float h = SegmentHealth; h < max - 0.01f; h += SegmentHealth)
            {
                Image tick = ItemUI.Panel("Marca", m_BarRect, new Vector2(h / max, 0.5f), Vector2.zero,
                    new Vector2(2f, 12f), k_Tick);
                m_Ticks.Add(tick);
            }
        }

        void UpdateShield()
        {
            bool shield = m_Defenses && m_Defenses.ShieldMax > 0f;
            if (m_ShieldBack.gameObject.activeSelf != shield)
                m_ShieldBack.gameObject.SetActive(shield);
            if (shield)
                VisorUI.SetFill(m_ShieldFill, m_Defenses.Shield / m_Defenses.ShieldMax);

            float barrier = m_Defenses ? m_Defenses.Barrier : 0f;
            int shieldShown = shield ? Mathf.CeilToInt(m_Defenses.Shield) : -1;
            int barrierShown = barrier > 0.5f ? Mathf.CeilToInt(barrier) : -1;
            if (shieldShown == m_ShownShield && barrierShown == m_ShownBarrier)
                return;
            m_ShownShield = shieldShown;
            m_ShownBarrier = barrierShown;
            string text = "";
            if (shieldShown >= 0)
                text += $"<color=#{ItemUI.Hex(k_Shield)}>ESCUDO {shieldShown}</color>  ";
            if (barrierShown >= 0)
                text += $"BARREIRA {barrierShown}";
            m_Extra.text = text;
        }
    }
}
