using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Fala escrita letra a letra numa caixa de diálogo, por cima de um TextMeshPro monoespaçado (cada letra numa célula
    // de mesma largura, para os hieróglifos caberem no lugar das letras). Sem o tradutor, cada letra aparece como o
    // hieróglifo dela (AlienGlyphs) e a letra fica invisível. Com o tradutor, cada letra nasce como hieróglifo,
    // embaralha por DecodeTime e vira a letra (efeito de decodificação). A letra some e aparece pela cor dos vértices do
    // TextMeshPro; os hieróglifos são Images filhas, postas no meio de cada célula. Tempo sem escala.
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DialogueText : MonoBehaviour
    {
        TextMeshProUGUI m_Text;
        readonly List<Image> m_Glyphs = new List<Image>();
        float[] m_RevealAt = new float[0];
        int m_Count;
        bool m_Translated;
        float m_Start;
        float m_EndAt;
        bool m_Dirty;
        static readonly StringBuilder s_Builder = new StringBuilder();

        static DialogueSettings Settings => DialogueSettings.Current;

        // Toda a fala já apareceu (e foi decodificada)
        public bool Done => Time.unscaledTime >= m_EndAt;

        // Letras que já apareceram (para o som do texto)
        public int Revealed { get; private set; }

        void Awake()
        {
            m_Text = GetComponent<TextMeshProUGUI>();
        }

        // Começa a escrever 'line'. 'instant' mostra tudo de uma vez (sem decodificar).
        public void Show(string line, bool translated, bool instant = false)
        {
            if (!m_Text)
                m_Text = GetComponent<TextMeshProUGUI>();
            line ??= "";
            m_Translated = translated;
            s_Builder.Clear();
            s_Builder.Append("<mspace=").Append(Settings.CharWidth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                .Append("em>").Append(Escape(line)).Append("</mspace>");
            m_Text.text = s_Builder.ToString();
            m_Text.ForceMeshUpdate();

            // Quando cada letra aparece: ritmo constante, com pausas depois da pontuação
            TMP_TextInfo info = m_Text.textInfo;
            m_Count = info.characterCount;
            if (m_RevealAt.Length < m_Count)
                m_RevealAt = new float[m_Count];
            m_Start = Time.unscaledTime;
            float t = 0f, step = 1f / Mathf.Max(1f, Settings.CharsPerSecond);
            for (int i = 0; i < m_Count; i++)
            {
                m_RevealAt[i] = instant ? m_Start - 10f : m_Start + t;
                char c = info.characterInfo[i].character;
                t += step;
                if (c == '.' || c == '!' || c == '?')
                    t += Settings.SentencePause;
                else if (c == ',' || c == ';' || c == ':')
                    t += Settings.CommaPause;
            }

            m_EndAt = instant ? m_Start : m_Start + t + (translated ? Settings.DecodeTime : 0f);
            m_Dirty = true;
            Apply();
        }

        // Pula a escrita: tudo aparece já decodificado
        public void Complete()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < m_Count; i++)
                m_RevealAt[i] = now - 10f;
            m_EndAt = now;
            m_Dirty = true;
        }

        public void Clear()
        {
            if (m_Text)
                m_Text.text = "";
            m_Count = 0;
            Revealed = 0;
            foreach (Image glyph in m_Glyphs)
                if (glyph.enabled)
                    glyph.enabled = false;
        }

        void LateUpdate()
        {
            if (m_Count > 0 && (m_Dirty || Time.unscaledTime <= m_EndAt + 0.05f || m_Text.havePropertiesChanged))
                Apply();
        }

        void Apply()
        {
            if (m_Text.havePropertiesChanged)
                m_Text.ForceMeshUpdate();
            m_Dirty = false;
            TMP_TextInfo info = m_Text.textInfo;
            float now = Time.unscaledTime;
            float decode = Settings.DecodeTime;
            Color32 color = m_Text.color;
            int glyphs = 0, revealed = 0;

            for (int i = 0; i < m_Count && i < info.characterCount; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                float age = now - m_RevealAt[i];
                if (age >= 0f)
                    revealed++;
                if (!ch.isVisible)
                    continue;

                bool shown = age >= 0f;
                bool letter = shown && m_Translated && age >= decode;
                SetAlpha(info, ch, letter ? color.a : (byte)0);
                if (!shown || letter)
                    continue;

                // Hieróglifo da letra; decodificando, embaralha entre glifos ao acaso
                int index = AlienGlyphs.IndexOf(ch.character);
                if (m_Translated && index != AlienGlyphs.Separator)
                {
                    int tick = Mathf.FloorToInt(age * Settings.ScrambleRate);
                    index = (int)((uint)(i * 7349 + tick * 2654435761u) % AlienGlyphs.Count);
                }

                Image glyph = Glyph(glyphs++);
                glyph.sprite = AlienGlyphs.Get(index);
                float height = Mathf.Max(4f, (ch.ascender - ch.baseLine) * 0.92f);
                float cell = ch.xAdvance - ch.origin;
                RectTransform rect = glyph.rectTransform;
                rect.sizeDelta = new Vector2(height * AlienGlyphs.Width / AlienGlyphs.Height, height);
                rect.localPosition = new Vector3(ch.origin + cell * 0.5f, ch.baseLine + height * 0.5f, 0f);
                Color glyphColor = m_Text.color;
                // Embaralhando, o glifo fica mais claro (piscando no tique)
                if (m_Translated)
                    glyphColor = Color.Lerp(glyphColor, Color.white, 0.5f + 0.5f * Mathf.Sin(age * 60f));
                glyph.color = glyphColor;
                if (!glyph.enabled)
                    glyph.enabled = true;
            }

            for (int i = glyphs; i < m_Glyphs.Count; i++)
                if (m_Glyphs[i].enabled)
                    m_Glyphs[i].enabled = false;

            Revealed = revealed;
            m_Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        static void SetAlpha(TMP_TextInfo info, TMP_CharacterInfo ch, byte alpha)
        {
            Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            int v = ch.vertexIndex;
            if (colors == null || v + 3 >= colors.Length)
                return;
            for (int k = 0; k < 4; k++)
                colors[v + k].a = alpha;
        }

        Image Glyph(int index)
        {
            while (m_Glyphs.Count <= index)
            {
                var go = new GameObject("Glifo", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.enabled = false;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                m_Glyphs.Add(image);
            }

            return m_Glyphs[index];
        }

        // A fala não pode abrir tags (o "<" vira um sinal comum)
        static string Escape(string line) => line.Replace("<", "<noparse><</noparse>");
    }
}
