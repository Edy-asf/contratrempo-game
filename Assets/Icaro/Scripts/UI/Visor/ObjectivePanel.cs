using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Aba de objetivo no canto superior direito do visor, abaixo do multiplicador e das Cronitas (estilo Risk of Rain
    // 2): cabeçalho "OBJETIVO" com o tempo da run e o objetivo atual com uma caixinha. O objetivo vem do estado do
    // jogo: fora de combate, avançar pelo bioma atual (BiomeZone); numa sala, eliminar os inimigos (com quantos
    // faltam); na sala do chefe, derrotar o chefe pelo nome e depois os reforços. Ao cumprir, a caixinha marca, o
    // texto é riscado e esmaece; ao trocar, o antigo desliza para a direita e some, o novo entra deslizando com um
    // brilho e a aba pisca. Texto longo quebra em até duas linhas e a aba cresce para baixo. Usa o tempo sem escala.
    // Criado pelo PlayerStats.
    public class ObjectivePanel : MonoBehaviour
    {
        [Tooltip("Segundos que o objetivo cumprido fica riscado antes de sair")]
        public float CompletedHold = 0.9f;

        [Tooltip("Segundos de cada deslize (saída e entrada)")]
        public float SlideTime = 0.35f;

        const float k_Width = 340f;
        const float k_HeaderHeight = 26f;
        const float k_LineHeight = 36f;
        const float k_TextTop = 7f;
        const float k_FontSize = 20f;
        const float k_TextLeft = 34f;
        const float k_Slide = 140f;

        class Line
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Check;
            public TextMeshProUGUI Text;
            public TextMeshProUGUI Count;
            public RectTransform[] Strikes;
            public RectTransform Shine;
            public int Kind;                 // 0 avançar, 1 chefe, 2 reforços, 3 sala
            public Object Context;           // bioma ou sala do objetivo
            public int ShownCount = int.MinValue;
            public bool Settled;             // parado: a animação não mexe mais em nada
            public bool Done;
            public float DoneAt;
            public float EnterAt;
            public float ExitAt = float.MaxValue;
            public float Height = k_LineHeight;
        }

        RectTransform m_Root;
        Image m_Glass;
        TextMeshProUGUI m_Time;
        RectTransform m_Lines;
        Line m_Current;
        Line m_Leaving;
        float m_Flash;
        float m_Height = k_LineHeight;
        int m_ShownSeconds = -1;

        // Sala do chefe: o chefe já apareceu (para saber quando ele morreu)
        RoomManager m_BossRoom;
        bool m_BossSeen;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.TopRight, out RectTransform group))
                return false;
            var anchor = new Vector2(1f, 1f);
            m_Root = ItemUI.Rect("Objetivo", group, anchor, new Vector2(-40f, -156f),
                new Vector2(k_Width, k_HeaderHeight + k_LineHeight + 6f));
            m_Glass = VisorUI.GlassPanel("Vidro", m_Root, anchor, Vector2.zero, m_Root.sizeDelta);

            VisorUI.Label("Titulo", m_Root, new Vector2(0f, 1f), new Vector2(12f, -6f), new Vector2(160f, 18f), 13f,
                TextAlignmentOptions.TopLeft, VisorUI.Dim, true).text = "OBJETIVO";
            m_Time = VisorUI.Label("Tempo", m_Root, anchor, new Vector2(-12f, -5f), new Vector2(120f, 20f), 16f,
                TextAlignmentOptions.TopRight, VisorUI.Text);
            Image divider = ItemUI.Panel("Divisao", m_Root, new Vector2(0.5f, 1f), new Vector2(0f, -k_HeaderHeight),
                new Vector2(k_Width - 20f, 1f), VisorUI.Edge);
            divider.color = new Color(VisorUI.Edge.r, VisorUI.Edge.g, VisorUI.Edge.b, 0.3f);

            // As linhas deslizam dentro desta área (cortadas nas bordas da aba), presa embaixo do cabeçalho
            m_Lines = ItemUI.Rect("Linhas", m_Root, new Vector2(0f, 1f), new Vector2(0f, -k_HeaderHeight - 2f),
                new Vector2(k_Width, k_LineHeight));
            m_Lines.gameObject.AddComponent<RectMask2D>();
            return true;
        }

        void Update()
        {
            if (!m_Root && !Build())
                return;
            float now = Time.unscaledTime;

            // Tempo da run: o texto só muda uma vez por segundo
            if (RunStats.Instance)
            {
                int seconds = Mathf.FloorToInt(RunStats.Instance.PlayTime);
                if (seconds != m_ShownSeconds)
                {
                    m_ShownSeconds = seconds;
                    m_Time.text = RunStats.FormatTime(seconds);
                }
            }

            Desired(out int kind, out Object context, out int count, out bool done);
            if (m_Current == null || m_Current.Kind != kind || m_Current.Context != context)
                Switch(kind, context, TextFor(kind, context), now);
            if (count != m_Current.ShownCount)
            {
                m_Current.ShownCount = count;
                m_Current.Count.text = count >= 0 ? count.ToString() : "";
                Measure(m_Current);
            }

            Resize();
            if (done && !m_Current.Done)
                Complete(m_Current, now);

            Animate(m_Current, now);
            if (m_Leaving != null)
            {
                Animate(m_Leaving, now);
                if (now >= m_Leaving.ExitAt + SlideTime)
                {
                    Destroy(m_Leaving.Rect.gameObject);
                    m_Leaving = null;
                }
            }

            m_Flash = Mathf.MoveTowards(m_Flash, 0f, Time.unscaledDeltaTime * 2.5f);
            m_Glass.color = Color.Lerp(VisorUI.Glass, new Color(VisorUI.Accent.r, VisorUI.Accent.g, VisorUI.Accent.b,
                0.4f), m_Flash * 0.6f);
        }

        // ---------- qual é o objetivo ----------

        // Qual objetivo vale agora (tipo e o bioma ou a sala dele); o texto só é montado na troca (TextFor)
        void Desired(out int kind, out Object context, out int count, out bool done)
        {
            RoomManager room = RoomManager.Active;
            count = -1;
            done = false;
            if (!room)
            {
                m_BossRoom = null;
                kind = 0;
                context = BiomeZone.Current;
                return;
            }

            int remaining = room.RemainingEnemies;
            if (room.IsBoss && !string.IsNullOrEmpty(room.BossName))
            {
                if (m_BossRoom != room)
                {
                    m_BossRoom = room;
                    m_BossSeen = false;
                }

                if (room.Boss)
                    m_BossSeen = true;
                // Chefe vivo (ou ainda surgindo); morto sem reforços, o objetivo dele fica cumprido
                if (!m_BossSeen || room.Boss || remaining <= 0)
                {
                    kind = 1;
                    context = room;
                    done = m_BossSeen && !room.Boss;
                    return;
                }

                kind = 2;
                context = room;
                count = remaining;
                done = remaining <= 0;
                return;
            }

            kind = 3;
            context = room;
            count = remaining;
            done = remaining <= 0;
        }

        static string TextFor(int kind, Object context)
        {
            switch (kind)
            {
                case 0:
                    var biome = context as BiomeZone;
                    return "Avance " + Through(biome ? biome.BiomeName : "") + " até a próxima arena";
                case 1:
                    var room = context as RoomManager;
                    return "Derrote " + (room ? room.BossName : "o chefe");
                case 2:
                    return "Elimine os reforços";
                default:
                    return "Elimine os inimigos";
            }
        }

        // "pelo Bosque", "pelas Ruínas"... (a Via quando não há bioma)
        static string Through(string biome) => biome switch
        {
            "" => "pela Via Aeônida",
            "Ruínas" => "pelas Ruínas",
            "Cratera" => "pela Cratera",
            _ => "pelo " + biome,
        };

        // ---------- troca e animação ----------

        void Switch(int kind, Object context, string text, float now)
        {
            float enter = now;
            if (m_Current != null)
            {
                // O objetivo anterior é dado como cumprido, fica riscado um instante e sai
                if (!m_Current.Done)
                    Complete(m_Current, now);
                if (m_Leaving != null)
                    Destroy(m_Leaving.Rect.gameObject);
                m_Leaving = m_Current;
                m_Leaving.ExitAt = Mathf.Max(now, m_Leaving.DoneAt + CompletedHold);
                enter = m_Leaving.ExitAt + SlideTime * 0.5f;
            }

            m_Current = CreateLine(kind, context, text);
            m_Current.EnterAt = enter;
            Animate(m_Current, now);
        }

        void Complete(Line line, float now)
        {
            line.Done = true;
            line.DoneAt = now;
        }

        void Animate(Line line, float now)
        {
            // Já parado (entrou, não está saindo e o risco, se houver, terminou): nada a mexer
            bool still = now >= line.EnterAt + SlideTime && now < line.ExitAt &&
                         (!line.Done || now >= line.DoneAt + 0.3f);
            if (still && line.Settled)
                return;
            line.Settled = still;

            // Entrada: desliza da direita com Ease.OutBack e acende
            float enter = Mathf.Clamp01((now - line.EnterAt) / SlideTime);
            float x = (1f - VisorUI.EaseOutBack(enter)) * k_Slide;
            float alpha = enter;
            if (enter > 0f && enter < 1f && line.Shine)
                line.Shine.anchoredPosition =
                    new Vector2(Mathf.Lerp(-40f, k_Width + 40f, enter), -line.Height * 0.5f);
            if (line.Shine)
                line.Shine.gameObject.SetActive(enter > 0f && enter < 1f);
            if (line == m_Current && enter > 0f && !line.Rect.gameObject.activeSelf)
                m_Flash = 1f;
            line.Rect.gameObject.SetActive(enter > 0f);

            // Saída: desliza para a direita e some
            float exit = Mathf.Clamp01((now - line.ExitAt) / SlideTime);
            x += exit * exit * k_Slide * 1.6f;
            alpha *= 1f - exit;

            line.Rect.anchoredPosition = new Vector2(x, 0f);
            line.Group.alpha = alpha;

            // Cumprido: a caixinha enche, o risco corre pelo texto e o texto esmaece
            float done = line.Done ? Mathf.Clamp01((now - line.DoneAt) / 0.3f) : 0f;
            line.Check.rectTransform.localScale = Vector3.one * (done > 0f ? 1f + 0.5f * Mathf.Sin(done * Mathf.PI) : 0f);
            Strike(line, done);
            line.Text.color = Color.Lerp(VisorUI.Text, VisorUI.Dim, done);
            line.Count.color = line.Text.color;
        }

        // Altura do texto (quebra em até duas linhas) e largura livre (sem contagem, o texto vai até a borda)
        void Measure(Line line)
        {
            bool counted = line.Count.text.Length > 0;
            float width = k_Width - k_TextLeft - (counted ? 62f : 14f);
            float textHeight = line.Text.GetPreferredValues(line.Text.text, width, 0f).y;
            line.Height = Mathf.Max(k_LineHeight, textHeight + k_TextTop * 2f);
            line.Text.rectTransform.sizeDelta = new Vector2(width, line.Height - k_TextTop);
            line.Rect.sizeDelta = new Vector2(k_Width, line.Height);
        }

        // A aba acompanha a altura do objetivo (suave)
        void Resize()
        {
            float target = Mathf.Max(m_Current.Height, m_Leaving != null ? m_Leaving.Height : 0f);
            if (Mathf.Approximately(m_Height, target) && Mathf.Approximately(m_Lines.sizeDelta.y, target))
                return;
            m_Height = Mathf.Lerp(m_Height, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            if (Mathf.Abs(m_Height - target) < 0.5f)
                m_Height = target;
            m_Lines.sizeDelta = new Vector2(k_Width, m_Height);
            m_Root.sizeDelta = new Vector2(k_Width, k_HeaderHeight + m_Height + 6f);
            m_Glass.rectTransform.sizeDelta = m_Root.sizeDelta;
        }

        // O risco corre linha por linha do texto, na ordem de leitura
        static void Strike(Line line, float progress)
        {
            TMP_TextInfo info = line.Text.textInfo;
            int lines = info != null ? Mathf.Min(info.lineCount, line.Strikes.Length) : 0;
            for (int i = 0; i < line.Strikes.Length; i++)
            {
                RectTransform strike = line.Strikes[i];
                bool show = i < lines && progress > 0f;
                if (strike.gameObject.activeSelf != show)
                    strike.gameObject.SetActive(show);
                if (!show)
                    continue;
                TMP_LineInfo lineInfo = info.lineInfo[i];
                float part = Mathf.Clamp01(progress * lines - i);
                float width = lineInfo.lineExtents.max.x - lineInfo.lineExtents.min.x;
                strike.anchoredPosition = new Vector2(lineInfo.lineExtents.min.x,
                    (lineInfo.ascender + lineInfo.descender) * 0.5f);
                strike.sizeDelta = new Vector2(width * part, 2f);
            }
        }

        Line CreateLine(int kind, Object context, string text)
        {
            var line = new Line { Kind = kind, Context = context };
            var top = new Vector2(0f, 1f);
            line.Rect = ItemUI.Rect("Linha", m_Lines, top, Vector2.zero, new Vector2(k_Width, k_LineHeight));
            line.Group = line.Rect.gameObject.AddComponent<CanvasGroup>();
            line.Group.alpha = 0f;

            // Caixinha (alinhada com a primeira linha do texto): moldura e o preenchimento que aparece ao cumprir
            Image box = ItemUI.Panel("Caixa", line.Rect, top, new Vector2(12f, -11f), Vector2.one * 14f, VisorUI.Edge);
            ItemUI.Fill("Fundo", box.transform, 1.5f, new Color(0.02f, 0.05f, 0.07f, 0.9f));
            line.Check = ItemUI.Fill("Marca", box.transform, 3f, VisorUI.Accent);
            line.Check.rectTransform.localScale = Vector3.zero;

            line.Text = VisorUI.Label("Texto", line.Rect, top, new Vector2(k_TextLeft, -k_TextTop),
                new Vector2(k_Width - k_TextLeft - 14f, k_LineHeight), k_FontSize, TextAlignmentOptions.TopLeft,
                VisorUI.Text);
            line.Text.textWrappingMode = TextWrappingModes.Normal;
            line.Text.overflowMode = TextOverflowModes.Overflow;
            line.Text.text = text;
            line.Count = VisorUI.Label("Contagem", line.Rect, new Vector2(1f, 1f), new Vector2(-14f, -k_TextTop),
                new Vector2(60f, 30f), k_FontSize, TextAlignmentOptions.TopRight, VisorUI.Text);
            line.Count.text = "";

            // Um risco por linha do texto (presos no canto do texto, no mesmo espaço do TextMeshPro)
            line.Strikes = new RectTransform[2];
            for (int i = 0; i < line.Strikes.Length; i++)
            {
                Image strike = ItemUI.Panel("Risco", line.Text.rectTransform, top, Vector2.zero, new Vector2(0f, 2f),
                    VisorUI.Text);
                line.Strikes[i] = strike.rectTransform;
                line.Strikes[i].pivot = new Vector2(0f, 0.5f);
                strike.gameObject.SetActive(false);
            }

            Image shine = ItemUI.Panel("Reflexo", line.Rect, top, Vector2.zero, new Vector2(30f, k_LineHeight * 2.4f),
                new Color(1f, 1f, 1f, 0.14f));
            shine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            line.Shine = shine.rectTransform;
            Measure(line);
            line.Rect.gameObject.SetActive(false);
            return line;
        }
    }
}
