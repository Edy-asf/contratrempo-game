using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Cor da faixa de aviso do visor
    public enum BannerTone
    {
        Normal,
        Warning,
        Success,
        Danger,
    }

    // Avisos do jogo numa faixa de vidro no meio-alto do visor, no lugar das mensagens do Microgame (o
    // DisplayMessageManager dele é removido): título grande em fonte larga e subtítulo na cor do aviso, entre duas
    // linhas que se abrem para os lados. A faixa abre esticando (Ease.OutBack) enquanto as letras do título se juntam,
    // fica HoldTime segundos e fecha achatando. Avisos seguidos fazem fila (o atual encurta). O código do Ícaro chama
    // VisorBanner.Show(título, subtítulo, tom); qualquer DisplayMessageEvent (inclusive do Microgame) também aparece,
    // com "Título: subtítulo" ou "Título\nsubtítulo" separados. Usa o tempo sem escala. Criado pelo PlayerStats.
    public class VisorBanner : MonoBehaviour
    {
        [Tooltip("Segundos da abertura")]
        public float OpenTime = 0.35f;

        [Tooltip("Segundos que o aviso fica aberto")]
        public float HoldTime = 1.8f;

        [Tooltip("Segundos do fechamento")]
        public float CloseTime = 0.3f;

        [Tooltip("Tempo mínimo aberto quando há outro aviso esperando")]
        public float MinHoldWhenQueued = 0.6f;

        [Tooltip("Altura da faixa acima do centro da tela (pixels de referência, tela de 1080)")]
        public float Height = 230f;

        static readonly Color k_Success = new Color(0.42f, 1f, 0.72f, 1f);
        const float k_StripHeight = 74f;
        const float k_SideLine = 150f;

        class Banner
        {
            public string Title;
            public string Subtitle;
            public BannerTone Tone;
            public float ShowAt;
        }

        static VisorBanner s_Instance;

        RectTransform m_Root;
        CanvasGroup m_Group;
        RectTransform m_Strip;
        Image m_TopLine;
        Image m_BottomLine;
        RectTransform m_Left;
        RectTransform m_Right;
        TextMeshProUGUI m_Title;
        TextMeshProUGUI m_Subtitle;

        readonly List<Banner> m_Queue = new List<Banner>();
        Banner m_Current;
        float m_Start;
        float m_CloseAt;
        float m_Width;
        float m_NextMicrogameCheck;
        bool m_MicrogameRemoved;

        // Mostra um aviso; sem a faixa (cena sem jogador), cai no aviso do Microgame
        public static void Show(string title, string subtitle = "", BannerTone tone = BannerTone.Normal)
        {
            if (s_Instance)
            {
                s_Instance.Enqueue(title, subtitle, tone, 0f);
                return;
            }

            DisplayMessageEvent evt = Events.DisplayMessageEvent;
            evt.Message = string.IsNullOrEmpty(subtitle) ? title : $"{title}: {subtitle}";
            evt.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(evt);
        }

        void Awake()
        {
            s_Instance = this;
            EventManager.AddListener<DisplayMessageEvent>(OnMessage);
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            EventManager.RemoveListener<DisplayMessageEvent>(OnMessage);
        }

        void OnMessage(DisplayMessageEvent evt)
        {
            string message = evt.Message ?? "";
            string title = message, subtitle = "";
            int split = message.IndexOf('\n');
            int length = 1;
            if (split < 0)
            {
                split = message.IndexOf(": ", System.StringComparison.Ordinal);
                length = 2;
            }

            if (split > 0)
            {
                title = message.Substring(0, split);
                subtitle = message.Substring(split + length);
            }

            Enqueue(title.Trim(), subtitle.Trim(), BannerTone.Normal, evt.DelayBeforeDisplay);
        }

        void Enqueue(string title, string subtitle, BannerTone tone, float delay)
        {
            if (string.IsNullOrEmpty(title))
                return;
            m_Queue.Add(new Banner
            {
                Title = title, Subtitle = subtitle ?? "", Tone = tone, ShowAt = Time.unscaledTime + delay,
            });
        }

        void Update()
        {
            float now = Time.unscaledTime;
            RemoveMicrogameMessages(now);
            if (!m_Root && !Build())
                return;

            // Próximo aviso da fila (o atual fecha mais cedo se houver outro esperando)
            int ready = -1;
            for (int i = 0; i < m_Queue.Count && ready < 0; i++)
            {
                if (now >= m_Queue[i].ShowAt)
                    ready = i;
            }
            if (m_Current != null && ready >= 0)
                m_CloseAt = Mathf.Min(m_CloseAt, Mathf.Max(now, m_Start + OpenTime + MinHoldWhenQueued));
            if (m_Current != null && now >= m_CloseAt + CloseTime)
                m_Current = null;
            if (m_Current == null && ready >= 0)
            {
                Begin(m_Queue[ready], now);
                m_Queue.RemoveAt(ready);
            }

            m_Root.gameObject.SetActive(m_Current != null);
            if (m_Current != null)
                Animate(now);
        }

        // O DisplayMessageManager do Microgame mostraria os mesmos avisos no estilo antigo
        void RemoveMicrogameMessages(float now)
        {
            if (m_MicrogameRemoved || now < m_NextMicrogameCheck)
                return;
            m_NextMicrogameCheck = now + 0.5f;
            var manager = FindAnyObjectByType<DisplayMessageManager>();
            if (!manager)
                return;
            if (manager.DisplayMessageRect)
                manager.DisplayMessageRect.gameObject.SetActive(false);
            Destroy(manager);
            m_MicrogameRemoved = true;
        }

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.Center, out RectTransform group))
                return false;
            var center = new Vector2(0.5f, 0.5f);
            m_Root = ItemUI.Rect("Aviso", group, center, new Vector2(0f, Height), new Vector2(1000f, 140f));
            m_Group = m_Root.gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;

            Image strip = ItemUI.Panel("Faixa", m_Root, center, Vector2.zero, new Vector2(600f, k_StripHeight),
                VisorUI.Glass);
            m_Strip = strip.rectTransform;
            m_TopLine = VisorUI.Line("Topo", m_Strip, Vector2.up, Vector2.one, 1.5f, VisorUI.Edge);
            m_TopLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            m_BottomLine = VisorUI.Line("Base", m_Strip, Vector2.zero, Vector2.right, 1.5f, VisorUI.Edge);

            m_Left = ItemUI.Panel("LinhaEsquerda", m_Root, center, Vector2.zero, new Vector2(0f, 2f), VisorUI.Edge)
                .rectTransform;
            m_Left.pivot = new Vector2(1f, 0.5f);
            m_Right = ItemUI.Panel("LinhaDireita", m_Root, center, Vector2.zero, new Vector2(0f, 2f), VisorUI.Edge)
                .rectTransform;
            m_Right.pivot = new Vector2(0f, 0.5f);

            m_Title = VisorUI.Label("Titulo", m_Root, center, new Vector2(0f, 10f), new Vector2(1000f, 50f), 40f,
                TextAlignmentOptions.Center, VisorUI.Text, true);
            m_Subtitle = VisorUI.Label("Subtitulo", m_Root, center, new Vector2(0f, -22f), new Vector2(1000f, 26f),
                18f, TextAlignmentOptions.Center, VisorUI.Accent);
            m_Root.gameObject.SetActive(false);
            return true;
        }

        void Begin(Banner banner, float now)
        {
            m_Current = banner;
            m_Start = now;
            m_CloseAt = now + OpenTime + HoldTime;

            Color tone = ToneColor(banner.Tone);
            m_Title.text = banner.Title.ToUpperInvariant();
            m_Subtitle.text = banner.Subtitle.ToUpperInvariant();
            m_Subtitle.color = tone;
            Color line = new Color(tone.r, tone.g, tone.b, 0.8f);
            m_TopLine.color = m_BottomLine.color = line;
            m_Left.GetComponent<Image>().color = m_Right.GetComponent<Image>().color = line;

            // Só o título: ele desce para o meio da faixa
            bool hasSubtitle = banner.Subtitle.Length > 0;
            m_Subtitle.gameObject.SetActive(hasSubtitle);
            m_Title.rectTransform.anchoredPosition = new Vector2(0f, hasSubtitle ? 10f : 0f);

            m_Title.characterSpacing = 8f;
            float titleWidth = m_Title.GetPreferredValues(m_Title.text).x;
            float subtitleWidth = hasSubtitle ? m_Subtitle.GetPreferredValues(m_Subtitle.text).x : 0f;
            m_Width = Mathf.Max(420f, Mathf.Max(titleWidth, subtitleWidth) + 120f);
        }

        void Animate(float now)
        {
            float t = now - m_Start;
            float open = Mathf.Clamp01(t / OpenTime);
            float close = Mathf.Clamp01((now - m_CloseAt) / CloseTime);
            float eased = VisorUI.EaseOutBack(open);

            // Faixa estica na horizontal ao abrir e achata ao fechar
            m_Strip.sizeDelta = new Vector2(Mathf.LerpUnclamped(80f, m_Width, eased),
                k_StripHeight * (1f - close * 0.7f));

            // Linhas laterais saem das pontas da faixa
            float half = m_Strip.sizeDelta.x * 0.5f;
            float side = k_SideLine * Mathf.Clamp01(eased) * (1f - close);
            m_Left.anchoredPosition = new Vector2(-half - 8f, 0f);
            m_Right.anchoredPosition = new Vector2(half + 8f, 0f);
            m_Left.sizeDelta = m_Right.sizeDelta = new Vector2(side, 2f);

            // Letras do título se juntam ao abrir e se espalham ao fechar
            m_Title.characterSpacing = Mathf.Lerp(40f, 8f, 1f - (1f - open) * (1f - open)) + close * 20f;
            m_Group.alpha = Mathf.Clamp01(open * 2.5f) * (1f - close);
        }

        static Color ToneColor(BannerTone tone) => tone switch
        {
            BannerTone.Warning => VisorUI.Warning,
            BannerTone.Success => k_Success,
            BannerTone.Danger => VisorUI.Danger,
            _ => VisorUI.Accent,
        };
    }
}
