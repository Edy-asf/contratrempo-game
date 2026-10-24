using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Peças de interface comuns às cartas (ItemDraft) e ao inventário (ItemHUD): canvas, painéis, textos e o ícone
    // do item. Sem Icon na ficha, o ícone é um quadrado na cor da raridade com a letra do item.
    public static class ItemUI
    {
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            Color color)
        {
            var image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Painel que cobre o pai inteiro, com uma margem
        public static Image Fill(string name, Transform parent, float inset, Color color)
        {
            Image image = Panel(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var text = Rect(name, parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        // Ícone do item: o sprite da ficha, o gerado em pixel art (ItemArt) ou o provisório (cor da raridade e letra)
        public static RectTransform Icon(Transform parent, ItemDefinition item, ItemCatalog catalog, Vector2 anchor,
            Vector2 position, float size)
        {
            Color rarity = catalog ? catalog.ColorOf(item.Rarity) : Color.white;
            Image frame = Panel("Icone", parent, anchor, position, Vector2.one * size, rarity);
            Sprite icon = ItemArt.IconFor(item);
            if (icon)
            {
                Fill("Fundo", frame.transform, size * 0.05f, new Color(0.06f, 0.07f, 0.09f, 0.95f));
                Image sprite = Fill("Sprite", frame.transform, size * 0.05f, Color.white);
                sprite.sprite = icon;
                sprite.preserveAspect = true;
                return frame.rectTransform;
            }

            Fill("Fundo", frame.transform, size * 0.06f, Color.Lerp(rarity, Color.black, 0.65f));
            TextMeshProUGUI letter = Text("Letra", frame.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.one * size, size * 0.6f, TextAlignmentOptions.Center, rarity);
            letter.fontStyle = FontStyles.Bold;
            letter.text = item.IconLetter;
            return frame.rectTransform;
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        // Descrição com a regra de acúmulo (cinza) e, se houver, quantas pilhas o jogador já tem
        public static string Describe(ItemDefinition item, int owned)
        {
            string text = item.Description;
            if (!string.IsNullOrEmpty(item.StackRule))
                text += $"\n<color=#9AA4AE><size=85%>Acúmulo: {item.StackRule}</size></color>";
            if (owned > 0)
                text += $"\n<color=#C8D2DC><size=85%>Você tem: {owned}</size></color>";
            return text;
        }

        public static bool MouseOver(RectTransform rect)
        {
            if (Mouse.current == null || !rect)
                return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Mouse.current.position.ReadValue(), null);
        }
    }
}
