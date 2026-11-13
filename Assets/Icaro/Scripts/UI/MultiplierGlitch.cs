using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Glitch do multiplicador com o tempo de Kairós parado (fora da luta, depois das cartas e no caminho entre as
    // salas): o tempo todo (Continuous; desligado, em surtos curtos a cada 1 a 3 s) faixas horizontais do texto saltam para
    // os lados (cópias do número recortadas por RectMask2D), o número pisca e blocos brancos aparecem em volta dele;
    // de vez em quando uma faixa sai em ciano, como aberração cromática. Sem som. Tempo sem escala; com o jogo pausado
    // (cartas, loja, menus) não acontece. Adicionado ao texto do multiplicador pelo TimeMultiplierHUD.
    [RequireComponent(typeof(TMP_Text))]
    public class MultiplierGlitch : MonoBehaviour
    {
        [Tooltip("Glitch o tempo todo enquanto o tempo está parado (desligado: em surtos)")]
        public bool Continuous = true;

        [Tooltip("Segundos entre um surto e outro (mínimo e máximo)")]
        public Vector2 Interval = new Vector2(1f, 3f);

        [Tooltip("Duração de um surto (mínimo e máximo)")]
        public Vector2 BurstLength = new Vector2(0.1f, 0.3f);

        [Tooltip("Segundos entre um quadro do glitch e outro durante o surto")]
        public float FrameTime = 0.045f;

        [Tooltip("Deslocamento máximo das faixas para os lados (pixels do visor)")]
        public float MaxShift = 22f;

        [Tooltip("Chance de uma faixa sair em ciano")]
        [Range(0f, 1f)] public float CyanChance = 0.15f;

        public Color Cyan = new Color(0.4f, 0.95f, 1f, 1f);

        [Tooltip("Palavra que aparece em pedaços no lugar do número")]
        public string ErrorText = "ERRO";

        [Tooltip("Chance de uma faixa mostrar a palavra em vez do número")]
        [Range(0f, 1f)] public float ErrorChance = 0.3f;

        [Tooltip("Com o tempo parado, a palavra fica no lugar do número o tempo todo (os pedaços do número continuam saltando por cima)")]
        public bool ErrorAlways = true;

        [Tooltip("Chance, a cada quadro, de o número sumir e a palavra aparecer inteira por um instante")]
        [Range(0f, 1f)] public float ErrorFlashChance = 0.08f;

        public Color ErrorColor = Color.white;

        const int k_Slices = 5;
        const int k_Blocks = 4;

        TMP_Text m_Label;
        RectTransform m_Rect;
        RectTransform[] m_SliceRoots;
        TextMeshProUGUI[] m_SliceTexts;
        Image[] m_Blocks;
        float m_NextBurst, m_BurstEnd, m_NextFrame;
        bool m_Bursting;

        void Awake()
        {
            m_Label = GetComponent<TMP_Text>();
            m_Rect = (RectTransform)transform;
            m_SliceRoots = new RectTransform[k_Slices];
            m_SliceTexts = new TextMeshProUGUI[k_Slices];
            for (int i = 0; i < k_Slices; i++)
            {
                var root = new GameObject("Faixa" + i, typeof(RectTransform));
                root.transform.SetParent(transform, false);
                root.AddComponent<RectMask2D>();
                m_SliceRoots[i] = (RectTransform)root.transform;

                var copy = new GameObject("Texto", typeof(RectTransform));
                copy.transform.SetParent(root.transform, false);
                TextMeshProUGUI text = copy.AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                m_SliceTexts[i] = text;
                root.SetActive(false);
            }

            m_Blocks = new Image[k_Blocks];
            for (int i = 0; i < k_Blocks; i++)
            {
                Image block = ItemUI.Panel("Bloco" + i, transform, new Vector2(1f, 0.5f), Vector2.zero, Vector2.one,
                    Color.white);
                block.raycastTarget = false;
                block.gameObject.SetActive(false);
                m_Blocks[i] = block;
            }

            m_NextBurst = Time.unscaledTime + Random.Range(Interval.x, Interval.y);
        }

        void OnDisable() => EndBurst();

        static bool Frozen => !TimeManager.CurrentlyRunning && KairosClock.Frozen && Time.timeScale > 0f;

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (!Frozen)
            {
                if (m_Bursting)
                    EndBurst();
                m_NextBurst = now + Random.Range(0.4f, 1.2f);
                return;
            }

            if (Continuous)
            {
                m_Bursting = true;
                m_BurstEnd = float.MaxValue;
            }
            else if (m_BurstEnd == float.MaxValue)
                m_BurstEnd = now;

            if (!m_Bursting)
            {
                if (now < m_NextBurst)
                    return;
                m_Bursting = true;
                m_BurstEnd = now + Random.Range(BurstLength.x, BurstLength.y);
                m_NextFrame = 0f;
            }

            if (now >= m_BurstEnd)
            {
                EndBurst();
                m_NextBurst = now + Random.Range(Interval.x, Interval.y);
                return;
            }

            if (now >= m_NextFrame)
            {
                m_NextFrame = now + FrameTime * Random.Range(0.6f, 1.4f);
                Scramble();
            }
        }

        // Um quadro do glitch: novas faixas, novos blocos e o número piscando
        void Scramble()
        {
            Vector2 size = m_Rect.rect.size;
            float textWidth = Mathf.Min(size.x, m_Label.preferredWidth);
            float blink = Random.value < 0.35f ? Random.Range(0.15f, 0.5f) : 1f;
            m_Label.alpha = blink;

            // A palavra no lugar do número (sempre, ou de vez em quando): o número some e ela aparece inteira na
            // primeira faixa, sem deslocar muito, piscando como o número piscaria
            bool flash = ErrorAlways || Random.value < ErrorFlashChance;
            if (flash)
                m_Label.alpha = 0f;

            int slices = Random.Range(2, k_Slices + 1);
            for (int i = 0; i < k_Slices; i++)
            {
                RectTransform root = m_SliceRoots[i];
                bool on = i < slices;
                root.gameObject.SetActive(on);
                if (!on)
                    continue;

                // Faixa de 6% a 30% da altura do texto, deslocada para um lado
                bool whole = flash && i == 0;
                float h = whole ? 1f : Random.Range(0.06f, 0.3f);
                float y0 = whole ? 0f : Random.Range(0.1f, 0.9f - h);
                root.anchorMin = new Vector2(0f, y0);
                root.anchorMax = new Vector2(1f, y0 + h);
                float shift = (whole ? Random.Range(0f, 4f) : Random.Range(3f, MaxShift)) * (Random.value < 0.5f ? -1f : 1f);
                root.offsetMin = new Vector2(shift, 0f);
                root.offsetMax = new Vector2(shift, 0f);

                // A cópia cobre o texto inteiro, para a faixa mostrar o mesmo pedaço do número
                TextMeshProUGUI text = m_SliceTexts[i];
                RectTransform t = text.rectTransform;
                t.anchorMin = new Vector2(0f, -y0 / h);
                t.anchorMax = new Vector2(1f, (1f - y0) / h);
                t.offsetMin = t.offsetMax = Vector2.zero;
                bool error = whole || Random.value < ErrorChance;
                CopyStyle(text, error ? ErrorText : m_Label.text);
                Color color = error ? ErrorColor : Random.value < CyanChance ? Cyan : Color.white;
                if (whole)
                    color.a *= blink;
                text.color = color;
            }

            // Blocos brancos sobre o número e um pouco em volta
            int blocks = Random.Range(0, k_Blocks + 1);
            for (int i = 0; i < k_Blocks; i++)
            {
                Image block = m_Blocks[i];
                bool on = i < blocks;
                block.gameObject.SetActive(on);
                if (!on)
                    continue;
                RectTransform r = block.rectTransform;
                r.sizeDelta = new Vector2(Random.Range(4f, 26f), Random.Range(2f, 7f));
                r.anchoredPosition = new Vector2(-Random.Range(-10f, textWidth + 10f), Random.Range(-0.45f, 0.45f) * size.y);
                Color color = Random.value < CyanChance ? Cyan : Color.white;
                color.a = Random.Range(0.5f, 1f);
                block.color = color;
            }
        }

        void CopyStyle(TextMeshProUGUI text, string content)
        {
            if (text.font != m_Label.font)
                text.font = m_Label.font;
            text.fontSize = m_Label.fontSize;
            text.alignment = m_Label.alignment;
            text.characterSpacing = m_Label.characterSpacing;
            text.fontStyle = m_Label.fontStyle;
            if (text.text != content)
                text.text = content;
        }

        void EndBurst()
        {
            m_Bursting = false;
            if (m_Label)
                m_Label.alpha = 1f;
            if (m_SliceRoots != null)
                foreach (RectTransform root in m_SliceRoots)
                    if (root)
                        root.gameObject.SetActive(false);
            if (m_Blocks != null)
                foreach (Image block in m_Blocks)
                    if (block)
                        block.gameObject.SetActive(false);
        }
    }
}
