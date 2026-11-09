using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Título das telas dos menus: texto na fonte dos títulos (FonteRE, em Resources) com contorno branco e sombra.
    // Essa fonte não tem acentos nem cedilha: as letras acentuadas são escritas sem o sinal e o sinal é desenhado por
    // cima em blocos de pixel (Accents), com o mesmo contorno e sombra, acompanhando a letra. Animações, em tempo sem
    // escala (os menus pausam o jogo): as letras caem uma a uma ao abrir (DropIn) e, depois, sobem um pouco em onda no
    // contratempo da música (128 BPM: o pulso de Kairós quando a música toca; senão, um relógio próprio).
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuTitle : MonoBehaviour
    {
        const string k_Font = "FonteRE";
        const float k_DropDelay = 0.045f;
        const float k_DropTime = 0.38f;
        const float k_DropHeight = 70f;
        const float k_PulseLift = 4f;

        enum Mark
        {
            Acute,
            Tilde,
            Circumflex,
            Cedilla,
        }

        struct Accent
        {
            public int Index;
            public Mark Mark;
        }

        static TMP_FontAsset s_Font;
        static bool s_FontLoaded;

        TextMeshProUGUI m_Text;
        MenuTitleAccents m_Accents;
        readonly List<Accent> m_List = new List<Accent>();
        float[] m_Offsets = new float[0];
        float[] m_Alphas = new float[0];
        float m_Age;
        bool m_Drop;
        bool m_Pulse = true;
        double m_LastPhase = -1;
        float m_OwnPhase;

        public TextMeshProUGUI Text => m_Text;

        public static TMP_FontAsset Font
        {
            get
            {
                if (!s_FontLoaded)
                {
                    s_FontLoaded = true;
                    s_Font = Resources.Load<TMP_FontAsset>(k_Font);
                }

                return s_Font;
            }
        }

        // Cria o título com o canto de cima à esquerda em 'position' (âncora no alto à esquerda do 'parent')
        public static MenuTitle Create(Transform parent, string text, Vector2 position, float size, Color color,
            bool dropIn, bool pulse = true)
        {
            TextMeshProUGUI head = ItemUI.Text("Titulo", parent, new Vector2(0f, 1f), position, new Vector2(1700f, size * 1.6f),
                size, TextAlignmentOptions.TopLeft, color);
            head.rectTransform.pivot = new Vector2(0f, 1f);
            head.fontStyle = FontStyles.Bold;
            head.textWrappingMode = TextWrappingModes.NoWrap;
            head.overflowMode = TextOverflowModes.Overflow;
            if (Font)
                head.font = Font;

            // Contorno branco e sombra deslocada (instância própria do material, destruída com o texto)
            Material material = head.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.05f, 0.05f, 0.05f, 0.9f));

            var title = head.gameObject.AddComponent<MenuTitle>();
            title.m_Drop = dropIn;
            title.m_Pulse = pulse;
            title.SetText(text);
            return title;
        }

        void Awake() => m_Text = GetComponent<TextMeshProUGUI>();

        public void SetText(string text)
        {
            if (!m_Text)
                m_Text = GetComponent<TextMeshProUGUI>();
            m_List.Clear();
            bool needsMarks = Font && m_Text.font == Font;
            var plain = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (needsMarks && Strip(c, out char baseChar, out Mark mark))
                {
                    m_List.Add(new Accent { Index = plain.Length, Mark = mark });
                    plain.Append(baseChar);
                }
                else
                {
                    plain.Append(c);
                }
            }

            m_Text.text = plain.ToString();
            m_Offsets = new float[plain.Length];
            m_Alphas = new float[plain.Length];
            for (int i = 0; i < m_Alphas.Length; i++)
                m_Alphas[i] = m_Drop ? 0f : 1f;
            m_Age = 0f;

            if (m_List.Count > 0 && !m_Accents)
            {
                var go = new GameObject("Acentos", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.pivot = m_Text.rectTransform.pivot;
                m_Accents = go.AddComponent<MenuTitleAccents>();
                m_Accents.raycastTarget = false;
                m_Accents.Owner = this;
            }

            if (m_Accents)
                m_Accents.enabled = m_List.Count > 0;
            Apply();
        }

        static bool Strip(char c, out char baseChar, out Mark mark)
        {
            mark = Mark.Acute;
            baseChar = c;
            switch (char.ToUpperInvariant(c))
            {
                case 'Á': baseChar = 'A'; return true;
                case 'É': baseChar = 'E'; return true;
                case 'Í': baseChar = 'I'; return true;
                case 'Ó': baseChar = 'O'; return true;
                case 'Ú': baseChar = 'U'; return true;
                case 'Ã': baseChar = 'A'; mark = Mark.Tilde; return true;
                case 'Õ': baseChar = 'O'; mark = Mark.Tilde; return true;
                case 'Â': baseChar = 'A'; mark = Mark.Circumflex; return true;
                case 'Ê': baseChar = 'E'; mark = Mark.Circumflex; return true;
                case 'Ô': baseChar = 'O'; mark = Mark.Circumflex; return true;
                case 'Ç': baseChar = 'C'; mark = Mark.Cedilla; return true;
                default: return false;
            }
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            m_Age += dt;

            // Contratempo: fase da música (pulso de Kairós) se ela anda; senão, 128 BPM próprios
            double phase = KairosBeat.Phase;
            float beat;
            if (m_LastPhase >= 0 && phase != m_LastPhase)
                beat = (float)(phase - System.Math.Floor(phase));
            else
            {
                m_OwnPhase = Mathf.Repeat(m_OwnPhase + dt * KairosBeat.BaseBpm / 60f, 1f);
                beat = m_OwnPhase;
            }
            m_LastPhase = phase;
            float offBeat = Mathf.Repeat(beat - 0.5f, 1f);

            int count = m_Offsets.Length;
            float dropEnd = (count - 1) * k_DropDelay + k_DropTime;
            for (int i = 0; i < count; i++)
            {
                float y = 0f;
                float alpha = 1f;
                if (m_Drop && m_Age < dropEnd + 0.05f)
                {
                    float u = Mathf.Clamp01((m_Age - i * k_DropDelay) / k_DropTime);
                    y = (1f - EaseOutBack(u)) * k_DropHeight;
                    alpha = Mathf.Ceil(Mathf.Clamp01(u * 2.5f) * 4f) / 4f;
                }
                else if (m_Pulse)
                {
                    // Onda curta da esquerda para a direita a cada contratempo, em degraus de 1 px
                    float t = offBeat - i * 0.012f;
                    float lift = t >= 0f ? Mathf.Exp(-t * 16f) : 0f;
                    y = Mathf.Round(lift * k_PulseLift);
                }

                m_Offsets[i] = y;
                m_Alphas[i] = alpha;
            }

            Apply();
        }

        static float EaseOutBack(float t)
        {
            t -= 1f;
            const float s = 1.5f;
            return 1f + t * t * ((s + 1f) * t + s);
        }

        // Desloca e esmaece cada letra na malha do texto e redesenha os acentos
        void Apply()
        {
            m_Text.ForceMeshUpdate();
            TMP_TextInfo info = m_Text.textInfo;
            for (int i = 0; i < info.characterCount && i < m_Offsets.Length; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible)
                    continue;
                TMP_MeshInfo mesh = info.meshInfo[c.materialReferenceIndex];
                var offset = new Vector3(0f, m_Offsets[i], 0f);
                byte alpha = (byte)(m_Alphas[i] * 255f);
                for (int v = 0; v < 4; v++)
                {
                    mesh.vertices[c.vertexIndex + v] += offset;
                    Color32 color = mesh.colors32[c.vertexIndex + v];
                    color.a = (byte)(color.a * alpha / 255);
                    mesh.colors32[c.vertexIndex + v] = color;
                }
            }

            m_Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            if (m_Accents)
                m_Accents.SetVerticesDirty();
        }

        // ---------- acentos ----------

        // Blocos (x0, y0, x1, y1) do sinal em unidades de 'u' (1/24 do tamanho), a partir do meio da letra: em cima do
        // topo dela (y para cima) ou, na cedilha, embaixo da linha de base
        static readonly Vector4[] k_Acute =
        {
            new Vector4(-1.6f, 1.2f, 0.6f, 2.5f), new Vector4(-0.2f, 2.3f, 2f, 3.6f), new Vector4(1.2f, 3.4f, 3.4f, 4.7f),
        };

        static readonly Vector4[] k_Circumflex =
        {
            new Vector4(-3.4f, 1.2f, -1.2f, 2.5f), new Vector4(-1.6f, 2.3f, 1.6f, 3.7f), new Vector4(1.2f, 1.2f, 3.4f, 2.5f),
        };

        static readonly Vector4[] k_Cedilla =
        {
            new Vector4(-0.7f, -1.8f, 0.9f, 0.2f), new Vector4(-0.3f, -3f, 2.3f, -1.6f), new Vector4(-2.2f, -4.2f, 1.6f, -2.8f),
        };

        // Tilde: quatro blocos em zigue-zague pela largura da letra (frações da largura, alturas em u)
        static readonly Vector4[] k_Tilde =
        {
            new Vector4(-0.46f, 1.3f, -0.2f, 2.6f), new Vector4(-0.24f, 2.4f, 0.02f, 3.7f),
            new Vector4(-0.02f, 1.3f, 0.24f, 2.6f), new Vector4(0.2f, 2.4f, 0.46f, 3.7f),
        };

        // Retângulos de cada sinal, no espaço do texto, com o deslocamento atual da letra
        internal void CollectAccents(List<Rect> rects)
        {
            rects.Clear();
            if (!m_Text)
                return;
            TMP_TextInfo info = m_Text.textInfo;
            float u = m_Text.fontSize / 24f;
            foreach (Accent accent in m_List)
            {
                if (accent.Index >= info.characterCount)
                    continue;
                TMP_CharacterInfo c = info.characterInfo[accent.Index];
                if (!c.isVisible || m_Alphas[accent.Index] <= 0f)
                    continue;
                float lift = m_Offsets[accent.Index];
                float left = c.bottomLeft.x;
                float right = c.topRight.x;
                float mid = (left + right) * 0.5f;
                float width = right - left;
                float top = c.topRight.y + lift;
                float baseLine = c.baseLine + lift;
                switch (accent.Mark)
                {
                    case Mark.Acute: AddBlocks(rects, k_Acute, mid, top, u, u); break;
                    case Mark.Circumflex: AddBlocks(rects, k_Circumflex, mid, top, u, u); break;
                    case Mark.Cedilla: AddBlocks(rects, k_Cedilla, mid, baseLine, u, u); break;
                    case Mark.Tilde: AddBlocks(rects, k_Tilde, mid, top, width, u); break;
                }
            }
        }

        static void AddBlocks(List<Rect> rects, Vector4[] blocks, float x, float y, float scaleX, float scaleY)
        {
            foreach (Vector4 b in blocks)
                rects.Add(Rect.MinMaxRect(x + b.x * scaleX, y + b.y * scaleY, x + b.z * scaleX, y + b.w * scaleY));
        }

        internal float Stroke => m_Text ? m_Text.fontSize * 0.045f : 4f;

        internal Color FillColor => m_Text ? m_Text.color : Color.white;
    }
}
