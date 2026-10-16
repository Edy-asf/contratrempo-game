using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Sprites das artes em pixel geradas em código (ItemIconPainter, ItemCardPainter, ItemBadgePainter), criados na
    // primeira vez que a interface pede e guardados para o resto do jogo. O ícone da ficha (ItemDefinition.Icon), se
    // houver, tem prioridade sobre o gerado. Textura com filtro Point: cada pixel da arte vira um bloco nítido.
    public static class ItemArt
    {
        // Pixels da arte por unidade do canvas (as cartas são mostradas em 3x)
        public const float CardScale = 3f;

        static readonly Dictionary<string, Sprite> s_Icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<ItemRarity, Sprite> s_Cards = new Dictionary<ItemRarity, Sprite>();
        static readonly Dictionary<ItemTag, Sprite> s_Badges = new Dictionary<ItemTag, Sprite>();
        static Sprite s_Glow;
        static Sprite s_Pixel;

        // Ícone do item: o da ficha ou o gerado pela chave (nome da ficha sem "Item_"); null se não houver nenhum
        public static Sprite IconFor(ItemDefinition item)
        {
            if (!item)
                return null;
            if (item.Icon)
                return item.Icon;

            string key = item.name.StartsWith("Item_") ? item.name.Substring(5) : item.name;
            if (!s_Icons.TryGetValue(key, out Sprite sprite))
            {
                PixelCanvas canvas = ItemIconPainter.Paint(key);
                sprite = canvas != null ? ToSprite(canvas, "Icone_" + key) : null;
                s_Icons[key] = sprite;
            }

            return sprite;
        }

        public static Sprite CardFor(ItemRarity rarity)
        {
            if (!s_Cards.TryGetValue(rarity, out Sprite sprite) || !sprite)
                s_Cards[rarity] = sprite = ToSprite(ItemCardPainter.Paint(rarity), "Carta_" + rarity);
            return sprite;
        }

        public static Sprite BadgeFor(ItemTag tag)
        {
            if (!s_Badges.TryGetValue(tag, out Sprite sprite))
            {
                PixelCanvas canvas = ItemBadgePainter.Paint(tag);
                s_Badges[tag] = sprite = canvas != null ? ToSprite(canvas, "Selo_" + tag) : null;
            }

            return sprite;
        }

        // Brilho suave em volta da carta (bilinear, branco: a cor vem da Image). Cobre a carta e uma margem de
        // GlowMargin em cada lado.
        public const float GlowMargin = 70f;

        public static Sprite Glow
        {
            get
            {
                if (s_Glow)
                    return s_Glow;
                const int w = 94, h = 122;
                float marginX = GlowMargin / (ItemCardPainter.Width * CardScale + GlowMargin * 2f) * w;
                float marginY = GlowMargin / (ItemCardPainter.Height * CardScale + GlowMargin * 2f) * h;
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "BrilhoCarta", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Max(marginX - x, x + 1 - (w - marginX)));
                        float dy = Mathf.Max(0f, Mathf.Max(marginY - y, y + 1 - (h - marginY)));
                        float d = Mathf.Sqrt(dx * dx / (marginX * marginX) + dy * dy / (marginY * marginY));
                        float a = Mathf.Clamp01(1f - d);
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                s_Glow = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return s_Glow;
            }
        }

        // Quadrado branco de 1 pixel (partículas e realces)
        public static Sprite Pixel
        {
            get
            {
                if (s_Pixel)
                    return s_Pixel;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);
                s_Pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                return s_Pixel;
            }
        }

        public static Color ToColor(Px px) => new Color32(px.R, px.G, px.B, px.A);

        // Textura nítida (filtro Point) de uma tela de pixels. A tela tem (0, 0) no alto; a textura do Unity, embaixo.
        public static Texture2D ToTexture(PixelCanvas canvas, string name)
        {
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    Px p = canvas.Pixels[y * canvas.Width + x];
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color32(p.R, p.G, p.B, p.A);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Sprite ToSprite(PixelCanvas canvas, string name)
        {
            Texture2D texture = ToTexture(canvas, name);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, canvas.Width, canvas.Height), new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = name;
            return sprite;
        }
    }
}
