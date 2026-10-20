using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Barra de vida do chefe no alto e no centro do visor: nome em fonte larga com a fase atual, barra longa com o
    // rastro amarelo-claro que desce depois do dano (como a vida do jogador), marcas nas trocas de fase
    // (BossDefinition.PhaseTwoAt e PhaseThreeAt) e a vida em número. Entra quando o chefe surge (RoomManager.Boss):
    // abre esticando e a barra enche da esquerda; na troca de fase o vidro pisca e o nome pulsa; ao morrer a barra
    // esvazia, segura um instante e some. Usa o tempo sem escala. Criado pelo PlayerStats.
    public class BossHealthBar : MonoBehaviour
    {
        [Tooltip("Largura da barra (pixels de referência, tela de 1080 de altura)")]
        public float Width = 760f;

        [Tooltip("Segundos da entrada (a barra enche da esquerda)")]
        public float IntroTime = 1f;

        [Tooltip("Segundos que o rastro espera antes de descer")]
        public float TrailDelay = 0.5f;

        [Tooltip("Velocidade com que o rastro alcança a barra (maior = mais rápido)")]
        public float TrailSpeed = 3f;

        static readonly Color k_Health = new Color(1f, 0.3f, 0.32f, 0.95f);
        static readonly Color k_Trail = new Color(1f, 0.93f, 0.62f, 0.9f);
        static readonly Color k_Back = new Color(0f, 0.02f, 0.04f, 0.55f);
        static readonly Color k_Tick = new Color(0.86f, 0.95f, 1f, 0.7f);
        const float k_BarHeight = 14f;
        const float k_DeathHold = 1.2f;
        const float k_FadeTime = 0.5f;

        RectTransform m_Root;
        CanvasGroup m_Group;
        Image m_Glass;
        TextMeshProUGUI m_Name;
        TextMeshProUGUI m_Value;
        RectTransform m_Bar;
        Image m_Fill;
        Image m_Trail;
        readonly List<Image> m_Ticks = new List<Image>();

        Health m_Health;
        BossAI m_Brain;
        string m_BossName;
        float m_Start;
        float m_DeadAt = -1f;
        float m_Shown;
        float m_TrailShown;
        float m_LastRatio;
        float m_LastDamage;
        float m_Flash;
        float m_NamePulse;
        int m_Phase;
        int m_ShownPhase = -1, m_ShownHealth = -1, m_ShownMax = -1;
        bool m_ShownDead;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.TopCenter, out RectTransform group))
                return false;
            var top = new Vector2(0.5f, 1f);
            m_Root = ItemUI.Rect("Chefe", group, top, new Vector2(0f, -30f), new Vector2(Width + 40f, 84f));
            m_Group = m_Root.gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;

            m_Name = VisorUI.Label("Nome", m_Root, top, new Vector2(0f, -4f), new Vector2(Width, 34f), 26f,
                TextAlignmentOptions.Top, VisorUI.Text, true);

            // Vidro em volta da barra, a barra com o rastro e a vida em número
            m_Glass = VisorUI.GlassPanel("Vidro", m_Root, top, new Vector2(0f, -38f),
                new Vector2(Width + 20f, k_BarHeight + 14f));
            Image back = ItemUI.Panel("Barra", m_Root, top, new Vector2(0f, -45f), new Vector2(Width, k_BarHeight),
                k_Back);
            m_Bar = back.rectTransform;
            m_Trail = ItemUI.Fill("Rastro", m_Bar, 0f, k_Trail);
            m_Fill = ItemUI.Fill("Vida", m_Bar, 0f, k_Health);
            m_Value = VisorUI.Label("Valor", m_Root, top, new Vector2(0f, -68f), new Vector2(Width, 20f), 15f,
                TextAlignmentOptions.Top, VisorUI.Dim);

            m_Root.gameObject.SetActive(false);
            return true;
        }

        void Update()
        {
            if (!m_Root && !Build())
                return;
            float now = Time.unscaledTime;

            // Chefe novo na sala em combate
            RoomManager room = RoomManager.Active;
            Health boss = room ? room.Boss : null;
            if (boss && boss != m_Health)
                Begin(boss, room.BossName, now);

            if (m_Health is null)
                return;

            bool dead = !m_Health || m_Health.CurrentHealth <= 0f;
            if (dead && m_DeadAt < 0f)
                m_DeadAt = now;
            if (m_DeadAt >= 0f && now - m_DeadAt > k_DeathHold + k_FadeTime)
            {
                m_Health = null;
                m_Root.gameObject.SetActive(false);
                return;
            }

            float ratio = dead ? 0f : Mathf.Clamp01(m_Health.CurrentHealth / Mathf.Max(1f, m_Health.MaxHealth));
            UpdateFill(ratio, now);
            UpdateLabels(dead);
            Animate(now);
        }

        void Begin(Health boss, string bossName, float now)
        {
            m_Health = boss;
            m_Brain = boss.GetComponent<BossAI>();
            m_BossName = string.IsNullOrEmpty(bossName) ? "Chefe" : bossName;
            m_Start = now;
            m_DeadAt = -1f;
            m_Shown = m_TrailShown = 0f;
            m_LastRatio = 1f;
            m_LastDamage = -10f;
            m_Phase = m_Brain ? m_Brain.Phase : 1;
            BuildTicks(boss.GetComponent<EnemyStats>());
            m_Root.gameObject.SetActive(true);
        }

        // Marcas nas vidas em que o chefe troca de fase
        void BuildTicks(EnemyStats stats)
        {
            foreach (Image tick in m_Ticks)
                Destroy(tick.gameObject);
            m_Ticks.Clear();
            var definition = stats ? stats.Definition as BossDefinition : null;
            if (!definition)
                return;
            foreach (float at in new[] { definition.PhaseTwoAt, definition.PhaseThreeAt })
            {
                if (at <= 0f || at >= 1f)
                    continue;
                Image tick = ItemUI.Panel("Fase", m_Bar, new Vector2(at, 0.5f), Vector2.zero,
                    new Vector2(2f, k_BarHeight + 8f), k_Tick);
                m_Ticks.Add(tick);
            }
        }

        void UpdateFill(float ratio, float now)
        {
            float dt = Time.unscaledDeltaTime;
            if (ratio < m_LastRatio - 0.0001f)
                m_LastDamage = now;
            m_LastRatio = ratio;

            // Entrada: a barra enche da esquerda até a vida atual; depois o dano cai na hora e o rastro desce
            float intro = Mathf.Clamp01((now - m_Start) / IntroTime);
            float target = Mathf.Min(ratio, 1f - (1f - intro) * (1f - intro));
            if (intro < 1f)
            {
                m_Shown = m_TrailShown = target;
            }
            else
            {
                m_Shown = ratio < m_Shown ? ratio : Mathf.MoveTowards(m_Shown, ratio, dt * 1.5f);
                if (m_TrailShown < m_Shown)
                    m_TrailShown = m_Shown;
                else if (now - m_LastDamage > TrailDelay)
                    m_TrailShown = Mathf.Lerp(m_TrailShown, m_Shown, 1f - Mathf.Exp(-dt * TrailSpeed));
                if (m_TrailShown - m_Shown < 0.002f)
                    m_TrailShown = m_Shown;
            }

            VisorUI.SetFill(m_Fill, m_Shown);
            VisorUI.SetFill(m_Trail, m_TrailShown);
        }

        void UpdateLabels(bool dead)
        {
            int phase = m_Brain ? m_Brain.Phase : 1;
            if (phase != m_Phase)
            {
                m_Phase = phase;
                m_Flash = 1f;
                m_NamePulse = 1f;
            }

            // Texto só quando a fase ou a vida mostrada mudam
            int health = dead || !m_Health ? 0 : Mathf.CeilToInt(m_Health.CurrentHealth);
            int max = m_Health ? Mathf.CeilToInt(m_Health.MaxHealth) : 0;
            if (phase == m_ShownPhase && dead == m_ShownDead && health == m_ShownHealth && max == m_ShownMax)
                return;
            m_ShownPhase = phase;
            m_ShownDead = dead;
            m_ShownHealth = health;
            m_ShownMax = max;
            string phaseColor = ItemUI.Hex(phase > 1 ? VisorUI.Danger : VisorUI.Dim);
            m_Name.text = $"{m_BossName.ToUpperInvariant()}<size=60%><color=#{phaseColor}>   |   " +
                          (dead ? "DERROTADO" : $"FASE {phase}") + "</color></size>";
            m_Value.text = dead || !m_Health ? "0" : $"{health} / {max}";
        }

        void Animate(float now)
        {
            float dt = Time.unscaledDeltaTime;

            // Abre esticando na horizontal (Ease.OutBack) e acende
            float open = Mathf.Clamp01((now - m_Start) / 0.4f);
            VisorUI.SetScale(m_Root, new Vector3(Mathf.LerpUnclamped(0.4f, 1f, VisorUI.EaseOutBack(open)), 1f, 1f));
            float alpha = Mathf.Clamp01(open * 2f);
            if (m_DeadAt >= 0f)
                alpha *= 1f - Mathf.Clamp01((now - m_DeadAt - k_DeathHold) / k_FadeTime);
            m_Group.alpha = alpha;

            // Troca de fase: o vidro pisca em vermelho e o nome pulsa
            m_Flash = Mathf.MoveTowards(m_Flash, 0f, dt * 1.5f);
            m_Glass.color = Color.Lerp(VisorUI.Glass, new Color(0.6f, 0.1f, 0.12f, 0.65f), m_Flash);
            m_NamePulse = Mathf.MoveTowards(m_NamePulse, 0f, dt * 2.5f);
            VisorUI.SetScale(m_Name.rectTransform, Vector3.one * (1f + 0.15f * Mathf.Sin(m_NamePulse * Mathf.PI)));
        }
    }
}
