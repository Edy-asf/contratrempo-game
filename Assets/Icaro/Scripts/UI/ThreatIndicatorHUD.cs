using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Indicadores de clareza no HUD:
    // - Dano direcional: ao levar dano (evento central), um arco vermelho em volta da mira aponta de onde veio o
    //   acerto (quem atirou ou golpeou) e some em DamageArcDuration segundos.
    // - Ameaças fora da tela: inimigos que estão avisando um ataque ou atirando (ThreatSignals, reportado pelas IAs)
    //   e que não aparecem na tela ganham uma seta âmbar na borda, apontando para eles; a seta pulsa mais rápido
    //   conforme o ataque se aproxima.
    // O jogador (PlayerStats) cria este HUD se a cena não tiver um. Tudo com instâncias fixas (sem alocar no combate).
    public class ThreatIndicatorHUD : MonoBehaviour
    {
        public Color DamageColor = new Color(1f, 0.15f, 0.1f, 0.9f);
        public Color ThreatColor = new Color(1f, 0.7f, 0.15f, 0.95f);

        public float DamageArcDuration = 1.2f;

        [Tooltip("Raio do anel dos arcos de dano em volta da mira (unidades do canvas, base 1080p)")]
        public float DamageRingRadius = 150f;

        [Tooltip("Margem da borda da tela para as setas")]
        public float EdgeMargin = 70f;

        const int k_DamageArcs = 8;
        const int k_ThreatArrows = 16;

        class Arc
        {
            public RectTransform Root;
            public Image Bar;
            public Vector3 SourcePosition;
            public float Age = float.MaxValue;
        }

        class Arrow
        {
            public RectTransform Root;
            public Image[] Parts;
        }

        Canvas m_Canvas;
        RectTransform m_Area;
        readonly Arc[] m_Arcs = new Arc[k_DamageArcs];
        readonly Arrow[] m_Arrows = new Arrow[k_ThreatArrows];
        int m_NextArc;

        void Start()
        {
            m_Canvas = ItemUI.CreateCanvas(transform, "Indicadores", 48);
            m_Area = (RectTransform)m_Canvas.transform;
            var center = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < k_DamageArcs; i++)
            {
                RectTransform root = ItemUI.Rect("Dano", m_Area, center, Vector2.zero, Vector2.one);
                Image bar = ItemUI.Panel("Arco", root, center, new Vector2(0f, DamageRingRadius), new Vector2(110f, 9f),
                    DamageColor);
                root.gameObject.SetActive(false);
                m_Arcs[i] = new Arc { Root = root, Bar = bar };
            }

            for (int i = 0; i < k_ThreatArrows; i++)
                m_Arrows[i] = CreateArrow("Ameaca", ThreatColor);
        }

        // Seta em "V": duas barras inclinadas dentro de um contêiner que gira para apontar
        Arrow CreateArrow(string name, Color color)
        {
            RectTransform root = ItemUI.Rect(name, m_Area, Vector2.zero, Vector2.zero, new Vector2(40f, 40f));
            root.pivot = new Vector2(0.5f, 0.5f);
            Image a = ItemUI.Panel("A", root, new Vector2(0.5f, 0.5f), new Vector2(-7f, 0f), new Vector2(5f, 26f), color);
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            Image b = ItemUI.Panel("B", root, new Vector2(0.5f, 0.5f), new Vector2(7f, 0f), new Vector2(5f, 26f), color);
            b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            root.gameObject.SetActive(false);
            return new Arrow { Root = root, Parts = new[] { a, b } };
        }

        void OnEnable() => CombatEvents.OnDamage += OnDamage;
        void OnDisable() => CombatEvents.OnDamage -= OnDamage;

        void OnDamage(DamageInfo info)
        {
            if (!info.VictimIsPlayer || !info.Attacker || info.Attacker == info.Victim || m_Arcs[0] == null)
                return;

            Arc arc = m_Arcs[m_NextArc];
            m_NextArc = (m_NextArc + 1) % m_Arcs.Length;
            arc.SourcePosition = info.Attacker.transform.position;
            arc.Age = 0f;
            arc.Root.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (!camera || !m_Area)
                return;

            // Configuração: setas e arcos desligados escondem o canvas inteiro
            if (m_Canvas.enabled != GameSettings.ThreatIndicators)
                m_Canvas.enabled = GameSettings.ThreatIndicators;
            if (!GameSettings.ThreatIndicators)
                return;

            UpdateDamageArcs(camera);
            UpdateThreats(camera);
        }

        // ---------- dano direcional ----------

        void UpdateDamageArcs(Camera camera)
        {
            Transform view = camera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            foreach (Arc arc in m_Arcs)
            {
                if (arc.Age >= DamageArcDuration)
                    continue;

                arc.Age += Time.deltaTime;
                if (arc.Age >= DamageArcDuration)
                {
                    arc.Root.gameObject.SetActive(false);
                    continue;
                }

                // Ângulo da fonte em volta do jogador, em relação para onde a câmera olha (atualiza ao girar)
                Vector3 toSource = Vector3.ProjectOnPlane(arc.SourcePosition - view.position, Vector3.up);
                float angle = Vector3.SignedAngle(forward, toSource, Vector3.up);
                arc.Root.localRotation = Quaternion.Euler(0f, 0f, -angle);

                Color color = DamageColor;
                color.a *= 1f - arc.Age / DamageArcDuration;
                arc.Bar.color = color;
            }
        }

        // ---------- ameaças fora da tela ----------

        void UpdateThreats(Camera camera)
        {
            ThreatSignals.Prune();
            IReadOnlyList<ThreatSignals.Threat> threats = ThreatSignals.Active;
            int used = 0;
            for (int i = 0; i < threats.Count && used < m_Arrows.Length; i++)
            {
                ThreatSignals.Threat threat = threats[i];
                Vector3 position = threat.Source.position + Vector3.up;
                if (!PlaceOffscreen(camera, position, m_Arrows[used]))
                    continue;

                // Pulsa mais rápido perto do fim do aviso
                float left = Mathf.Clamp01((threat.Until - Time.time) / threat.Duration);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(26f, 9f, left));
                Color color = ThreatColor;
                color.a *= pulse;
                foreach (Image part in m_Arrows[used].Parts)
                    part.color = color;
                m_Arrows[used].Root.localScale = Vector3.one * Mathf.Lerp(1.25f, 0.9f, left);
                used++;
            }

            for (int i = used; i < m_Arrows.Length; i++)
            {
                if (m_Arrows[i].Root.gameObject.activeSelf)
                    m_Arrows[i].Root.gameObject.SetActive(false);
            }
        }

        // Fora da tela (ou atrás): coloca a seta na borda, apontando para o ponto. Na tela: esconde e devolve false.
        bool PlaceOffscreen(Camera camera, Vector3 world, Arrow arrow)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            bool inside = !behind && screen.x > 0f && screen.x < Screen.width && screen.y > 0f &&
                          screen.y < Screen.height;
            if (inside)
            {
                arrow.Root.gameObject.SetActive(false);
                return false;
            }

            Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 offset = (Vector2)screen - half;
            if (behind)
                offset = -offset;
            if (offset.sqrMagnitude < 1f)
                offset = Vector2.down;

            float scale = Mathf.Max(0.01f, m_Canvas.scaleFactor);
            Vector2 limit = half - Vector2.one * (EdgeMargin * scale);
            float k = Mathf.Min(limit.x / Mathf.Abs(offset.x + 0.0001f), limit.y / Mathf.Abs(offset.y + 0.0001f));
            Vector2 edge = half + offset * k;

            arrow.Root.anchoredPosition = edge / scale;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            // O "V" desenhado aponta para baixo; gira para apontar na direção do alvo
            arrow.Root.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            if (!arrow.Root.gameObject.activeSelf)
                arrow.Root.gameObject.SetActive(true);
            return true;
        }
    }
}
