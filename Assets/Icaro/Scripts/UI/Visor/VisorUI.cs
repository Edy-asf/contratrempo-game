using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Peças e cores comuns dos painéis do visor: vidro translúcido com cantos marcados, textos na fonte sci-fi fina
    // (Kenney Future Narrow, ItemFonts.Visor) e easing das animações
    public static class VisorUI
    {
        public static readonly Color Glass = new Color(0.04f, 0.09f, 0.13f, 0.5f);
        public static readonly Color Edge = new Color(0.55f, 0.9f, 1f, 0.55f);
        public static readonly Color Text = new Color(0.86f, 0.95f, 1f, 0.95f);
        public static readonly Color Dim = new Color(0.55f, 0.68f, 0.76f, 0.8f);
        public static readonly Color Accent = new Color(0.45f, 0.92f, 1f, 1f);
        public static readonly Color Warning = new Color(1f, 0.62f, 0.25f, 1f);
        public static readonly Color Danger = new Color(1f, 0.3f, 0.32f, 1f);

        // Painel de vidro (50% de transparência) com uma linha fina embaixo e marcas nos quatro cantos
        public static Image GlassPanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image back = ItemUI.Panel(name, parent, anchor, position, size, Glass);
            RectTransform rect = back.rectTransform;
            Line("Base", rect, new Vector2(0f, 0f), new Vector2(1f, 0f), 1.5f, Edge);
            const float mark = 10f;
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one })
            {
                // Um "L" em cada canto
                Image h = ItemUI.Panel("CantoH", rect, corner, Vector2.zero, new Vector2(mark, 2f), Edge);
                Image v = ItemUI.Panel("CantoV", rect, corner, Vector2.zero, new Vector2(2f, mark), Edge);
                h.rectTransform.pivot = v.rectTransform.pivot = corner;
            }

            return back;
        }

        // Linha esticada entre duas âncoras do pai (ex.: a base do painel)
        public static Image Line(string name, RectTransform parent, Vector2 from, Vector2 to, float thickness,
            Color color)
        {
            Image line = ItemUI.Panel(name, parent, from, Vector2.zero, Vector2.zero, color);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = from;
            rect.anchorMax = to;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = from.y == to.y ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
            rect.anchoredPosition = Vector2.zero;
            return line;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, bool wide = false)
        {
            TextMeshProUGUI text = ItemUI.Text(name, parent, anchor, position, size, fontSize, alignment, color);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            TMP_FontAsset font = wide ? ItemFonts.VisorWide : ItemFonts.Visor;
            if (font)
                text.font = font;
            return text;
        }

        // Barra horizontal: fundo e preenchimento que cresce da esquerda (anchorMax.x = fração)
        public static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            Color back, Color fill, out Image background)
        {
            background = ItemUI.Panel(name, parent, anchor, position, size, back);
            Image bar = ItemUI.Fill("Nivel", background.transform, 0f, fill);
            SetFill(bar, 1f);
            return bar;
        }

        // Só mexe na barra se o valor mudou (mexer reconstrói o canvas)
        public static void SetFill(Image bar, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            RectTransform rect = bar.rectTransform;
            if (Mathf.Abs(rect.anchorMax.x - fraction) > 0.0005f || rect.anchorMax.y != 1f)
                rect.anchorMax = new Vector2(fraction, 1f);
        }

        // Escala só quando muda
        public static void SetScale(Transform target, Vector3 scale)
        {
            if (target.localScale != scale)
                target.localScale = scale;
        }

        // Ease.OutBack: passa um pouco do fim e volta
        public static float EaseOutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }
    }
}
