using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Carta de item no estilo de carta colecionável em pixel art: fundo da raridade (ItemCardPainter, em 3x), título
    // "CARTA COMUM/INCOMUM/RARA", arte do item (ItemArt), selos das categorias, nome e caixa "EFEITO:" com a frase
    // curta e a regra de acúmulo (nas cartas elementais, a frase de efeito no pé no lugar dela). Em volta, um brilho na cor da
    // raridade e, nas Incomuns e Raras, partículas subindo. Anima com o tempo real (a escolha pausa o jogo): o ícone
    // flutua em degraus de pixel e o brilho pulsa. Selected e Hovered vêm de quem usa a carta (ItemDraft).
    public class ItemCardView : MonoBehaviour
    {
        public const float Width = ItemCardPainter.Width * ItemArt.CardScale;
        public const float Height = ItemCardPainter.Height * ItemArt.CardScale;
        const float k_Px = ItemArt.CardScale;

        static readonly Color k_Label = new Color32(126, 224, 112, 255);
        static readonly Color k_Text = new Color32(236, 240, 245, 255);
        static readonly Color k_Shadow = new Color32(8, 10, 16, 230);
        static readonly Color k_Gold = new Color32(244, 220, 160, 255);

        public ItemDefinition Item { get; private set; }
        public RectTransform Root => (RectTransform)transform;
        public bool Selected { get; set; }
        public bool Hovered { get; set; }

        class Particle
        {
            public RectTransform Rect;
            public Image Image;
            public float Life;
            public float Age;
            public float Speed;
        }

        readonly List<Particle> m_Particles = new List<Particle>();
        Image m_Glow;
        RectTransform m_Icon;
        GameObject m_Selection;
        Image[] m_SelectionEdges;
        Color m_GlowColor;
        float m_GlowAlpha;
        Color m_ParticleColor;
        float m_Phase;
        float m_IconY;

        public static ItemCardView Create(Transform parent, ItemDefinition item, ItemCatalog catalog, int index,
            int owned, Vector2 position)
        {
            RectTransform root = ItemUI.Rect("Carta" + (index + 1), parent, new Vector2(0.5f, 0.5f), position,
                new Vector2(Width, Height));
            var view = root.gameObject.AddComponent<ItemCardView>();
            view.Build(item, catalog, index, owned);
            return view;
        }

        void Build(ItemDefinition item, ItemCatalog catalog, int index, int owned)
        {
            Item = item;
            m_Phase = index * 1.3f;
            ItemRarity rarity = item.Rarity;
            Vector2 center = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);

            // Brilho e partículas ficam atrás do corpo da carta
            m_GlowColor = rarity == ItemRarity.Raro ? new Color32(255, 50, 70, 255)
                : rarity == ItemRarity.Incomum ? new Color32(70, 240, 120, 255)
                : new Color32(210, 222, 238, 255);
            m_GlowAlpha = rarity == ItemRarity.Raro ? 0.7f : rarity == ItemRarity.Incomum ? 0.55f : 0.22f;
            m_Glow = ItemUI.Panel("Brilho", transform, center, Vector2.zero,
                new Vector2(Width + ItemArt.GlowMargin * 2f, Height + ItemArt.GlowMargin * 2f), m_GlowColor);
            m_Glow.sprite = ItemArt.Glow;

            m_ParticleColor = rarity == ItemRarity.Raro ? new Color32(255, 90, 100, 255) : new Color32(120, 255, 150, 255);
            int particles = rarity == ItemRarity.Raro ? 18 : rarity == ItemRarity.Incomum ? 10 : 0;
            RectTransform particleRoot = ItemUI.Rect("Particulas", transform, center, Vector2.zero, Vector2.zero);
            for (int i = 0; i < particles; i++)
                m_Particles.Add(NewParticle(particleRoot, true));

            Image body = ItemUI.Panel("Corpo", transform, center, Vector2.zero, new Vector2(Width, Height), Color.white);
            body.sprite = ItemArt.CardFor(rarity);

            // Arte do item (sem sprite gerado nem ícone na ficha: o provisório com a letra)
            Sprite icon = ItemArt.IconFor(item);
            m_IconY = -ItemCardPainter.ArtCenterY * k_Px;
            if (icon)
            {
                Image art = ItemUI.Panel("Arte", transform, top, new Vector2(0f, m_IconY),
                    new Vector2(icon.rect.width, icon.rect.height) * k_Px, Color.white);
                art.sprite = icon;
                art.preserveAspect = true;
                m_Icon = art.rectTransform;
            }
            else
            {
                m_Icon = ItemUI.Icon(transform, item, catalog, top, new Vector2(0f, m_IconY), 120f);
            }

            m_Icon.pivot = center;

            // Selos, da direita para a esquerda
            float badgeX = ItemCardPainter.BadgesRight * k_Px - Width * 0.5f;
            float badgeY = -ItemCardPainter.BadgesBottom * k_Px;
            const float badgeSize = ItemBadgePainter.Size * k_Px;
            foreach (ItemTag tag in System.Enum.GetValues(typeof(ItemTag)))
            {
                if (tag == ItemTag.None || (item.Tags & tag) == 0 || !ItemArt.BadgeFor(tag))
                    continue;
                Image badge = ItemUI.Panel("Selo_" + tag, transform, top,
                    new Vector2(badgeX - badgeSize * 0.5f, badgeY + badgeSize * 0.5f), Vector2.one * badgeSize,
                    Color.white);
                badge.rectTransform.pivot = center;
                badge.sprite = ItemArt.BadgeFor(tag);
                badgeX -= badgeSize + 2f * k_Px;
            }

            // Título e nome com sombra dura (um pixel da arte para baixo e para a direita)
            string header = rarity == ItemRarity.Raro ? "CARTA RARA"
                : rarity == ItemRarity.Incomum ? "CARTA INCOMUM" : "CARTA COMUM";
            ShadowText("Titulo", header, ItemFonts.Bold, top, new Vector2(0f, -ItemCardPainter.HeaderCenterY * k_Px),
                new Vector2(Width - 30f, 40f), 29f, 29f, rarity == ItemRarity.Raro ? (Color)new Color32(255, 236, 205, 255) : Color.white);
            ShadowText("Nome", item.DisplayName.ToUpperInvariant(), ItemFonts.Bold, top,
                new Vector2(0f, -ItemCardPainter.NameCenterY * k_Px), new Vector2(Width - 24f, 42f), 20f, 32f,
                rarity == ItemRarity.Raro ? k_Gold : Color.white);

            // Caixa do efeito
            float left = ItemCardPainter.BoxX * k_Px + 13f - Width * 0.5f;
            float textWidth = ItemCardPainter.BoxWidth * k_Px - 26f;
            float boxTop = ItemCardPainter.BoxY * k_Px + 7f;
            float boxBottom = (ItemCardPainter.BoxY + ItemCardPainter.BoxHeight) * k_Px - 8f;
            TextMeshProUGUI label = BoxText("Efeito", ItemFonts.Bold, left, boxTop, textWidth, 24f, 21f, k_Label);
            label.text = "EFEITO:";

            bool flavor = !string.IsNullOrEmpty(item.Flavor);
            float flavorHeight = flavor ? 46f : 0f;
            float descriptionTop = boxTop + 25f;
            TextMeshProUGUI description = BoxText("Descricao", ItemFonts.Regular, left, descriptionTop, textWidth,
                boxBottom - descriptionTop - flavorHeight, 21f, k_Text);
            description.enableAutoSizing = true;
            description.fontSizeMin = 13f;
            description.fontSizeMax = 21f;
            description.lineSpacing = -8f;
            // A regra de acúmulo ocupa o espaço da frase de efeito (nas elementais fica só no tooltip do HUD)
            description.text = item.Description + (flavor || string.IsNullOrEmpty(item.StackRule)
                ? ""
                : $"\n<size=82%><color=#{ItemUI.Hex(StackColor(rarity))}>Acúmulo: {item.StackRule}</color></size>");

            if (flavor)
            {
                TextMeshProUGUI quote = BoxText("Frase", ItemFonts.Regular, left, boxBottom - flavorHeight, textWidth,
                    flavorHeight, 17f, FlavorColor(rarity));
                quote.alignment = TextAlignmentOptions.BottomLeft;
                quote.fontStyle = FontStyles.Italic;
                quote.lineSpacing = -10f;
                quote.enableAutoSizing = true;
                quote.fontSizeMin = 12f;
                quote.fontSizeMax = 17f;
                quote.text = $"“{item.Flavor}”";
            }

            // Embaixo da carta: tecla e quantas o jogador já tem
            TextMeshProUGUI key = ItemUI.Text("Tecla", transform, new Vector2(0f, 0f), new Vector2(4f, -8f),
                new Vector2(80f, 30f), 22f, TextAlignmentOptions.TopLeft, new Color(0.62f, 0.68f, 0.75f));
            key.rectTransform.pivot = new Vector2(0f, 1f);
            key.font = ItemFonts.Bold;
            key.text = $"[{index + 1}]";
            TextMeshProUGUI count = ItemUI.Text("Quantidade", transform, new Vector2(1f, 0f), new Vector2(-4f, -8f),
                new Vector2(220f, 30f), 22f, TextAlignmentOptions.TopRight,
                owned > 0 ? new Color(0.8f, 0.9f, 1f) : new Color(0.55f, 0.95f, 0.65f));
            count.rectTransform.pivot = new Vector2(1f, 1f);
            count.font = ItemFonts.Bold;
            count.text = owned > 0 ? $"VOCÊ TEM: {owned}" : "NOVO";

            BuildSelection();
        }

        // Moldura branca e faixa "ESCOLHIDA" (aparecem com Selected)
        void BuildSelection()
        {
            m_Selection = ItemUI.Rect("Selecao", transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Width, Height)).gameObject;
            Transform s = m_Selection.transform;
            const float t = 6f, o = 9f;
            m_SelectionEdges = new[]
            {
                ItemUI.Panel("Cima", s, new Vector2(0.5f, 1f), new Vector2(0f, o - t * 0.5f), new Vector2(Width + o * 2f, t), Color.white),
                ItemUI.Panel("Baixo", s, new Vector2(0.5f, 0f), new Vector2(0f, -o + t * 0.5f), new Vector2(Width + o * 2f, t), Color.white),
                ItemUI.Panel("Esquerda", s, new Vector2(0f, 0.5f), new Vector2(-o + t * 0.5f, 0f), new Vector2(t, Height + o * 2f), Color.white),
                ItemUI.Panel("Direita", s, new Vector2(1f, 0.5f), new Vector2(o - t * 0.5f, 0f), new Vector2(t, Height + o * 2f), Color.white),
            };

            Image banner = ItemUI.Panel("Faixa", s, new Vector2(0.5f, 1f), new Vector2(0f, o + 22f), new Vector2(186f, 36f),
                Color.white);
            ItemUI.Fill("Borda", banner.transform, 3f, new Color32(18, 24, 34, 255));
            ItemUI.Fill("Fundo", banner.transform, 6f, Color.white);
            TextMeshProUGUI text = ItemUI.Text("Texto", banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                new Vector2(186f, 36f), 24f, TextAlignmentOptions.Center, new Color32(18, 24, 34, 255));
            text.font = ItemFonts.Bold;
            text.text = "ESCOLHIDA";
            m_Selection.SetActive(false);
        }

        void Update()
        {
            float time = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // Brilho: pulsa devagar; escolhida, fica branco
            float pulse = 0.85f + 0.15f * Mathf.Sin(time * 2.2f + m_Phase);
            Color glow = Selected ? Color.Lerp(m_GlowColor, Color.white, 0.6f) : m_GlowColor;
            glow.a = Mathf.Clamp01(m_GlowAlpha * pulse * (Selected ? 1.4f : Hovered ? 1.25f : 1f) + (Selected ? 0.15f : 0f));
            m_Glow.color = glow;

            // Ícone flutua em degraus de um pixel da arte
            if (m_Icon)
            {
                float bob = Mathf.Round(Mathf.Sin(time * 1.8f + m_Phase) * 1.5f) * k_Px;
                m_Icon.anchoredPosition = new Vector2(0f, m_IconY + bob);
            }

            m_Selection.SetActive(Selected);
            if (Selected)
            {
                float a = 0.75f + 0.25f * Mathf.Sin(time * 6f);
                foreach (Image edge in m_SelectionEdges)
                    edge.color = new Color(1f, 1f, 1f, a);
            }

            foreach (Particle p in m_Particles)
                UpdateParticle(p, dt);
        }

        Particle NewParticle(Transform parent, bool scatter)
        {
            Image image = ItemUI.Panel("Particula", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, m_ParticleColor);
            image.sprite = ItemArt.Pixel;
            var particle = new Particle { Rect = image.rectTransform, Image = image };
            Respawn(particle);
            if (scatter)
                particle.Age = Random.Range(0f, particle.Life);
            return particle;
        }

        // Nasce numa faixa em volta da carta e sobe devagar
        void Respawn(Particle p)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            bool vertical = Random.value < 0.7f;
            float x = vertical ? side * (Width * 0.5f + Random.Range(12f, 52f)) : Random.Range(-Width * 0.6f, Width * 0.6f);
            float y = vertical ? Random.Range(-Height * 0.5f, Height * 0.4f) : side * (Height * 0.5f + Random.Range(12f, 40f));
            p.Rect.anchoredPosition = new Vector2(Mathf.Round(x / k_Px) * k_Px, Mathf.Round(y / k_Px) * k_Px);
            p.Rect.sizeDelta = Vector2.one * (Random.value < 0.3f ? 3f : 2f) * k_Px;
            p.Life = Random.Range(1.6f, 3.2f);
            p.Speed = Random.Range(18f, 42f);
            p.Age = 0f;
        }

        void UpdateParticle(Particle p, float dt)
        {
            p.Age += dt;
            if (p.Age >= p.Life)
                Respawn(p);
            float t = p.Age / p.Life;
            Vector2 position = p.Rect.anchoredPosition;
            position.y += p.Speed * dt;
            p.Rect.anchoredPosition = position;
            Color color = Selected ? Color.Lerp(m_ParticleColor, Color.white, 0.5f) : m_ParticleColor;
            // Pisca em degraus no fim (estilo pixel)
            color.a = t < 0.15f ? t / 0.15f : t > 0.7f ? (Mathf.Repeat(p.Age * 10f, 1f) < 0.5f ? 0.9f : 0.2f) : 0.9f;
            p.Image.color = color;
        }

        // ---------- textos ----------

        void ShadowText(string name, string value, TMP_FontAsset font, Vector2 anchor, Vector2 position, Vector2 size,
            float minSize, float maxSize, Color color)
        {
            TextMeshProUGUI shadow = ItemUI.Text(name + "Sombra", transform, anchor, position + new Vector2(k_Px, -k_Px),
                size, maxSize, TextAlignmentOptions.Center, k_Shadow);
            TextMeshProUGUI text = ItemUI.Text(name, transform, anchor, position, size, maxSize,
                TextAlignmentOptions.Center, color);
            foreach (TextMeshProUGUI t in new[] { shadow, text })
            {
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (font)
                    t.font = font;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = minSize < maxSize;
                t.fontSizeMin = minSize;
                t.fontSizeMax = maxSize;
                t.characterSpacing = 2f;
                t.text = value;
            }
        }

        // Texto da caixa do efeito: x e y em pixels de tela a partir do canto superior esquerdo da carta
        TextMeshProUGUI BoxText(string name, TMP_FontAsset font, float left, float top, float width, float height,
            float fontSize, Color color)
        {
            TextMeshProUGUI text = ItemUI.Text(name, transform, new Vector2(0.5f, 1f), new Vector2(left, -top),
                new Vector2(width, height), fontSize, TextAlignmentOptions.TopLeft, color);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            if (font)
                text.font = font;
            return text;
        }

        static Color StackColor(ItemRarity rarity) => rarity == ItemRarity.Raro
            ? new Color32(190, 168, 176, 255)
            : rarity == ItemRarity.Incomum ? new Color32(150, 190, 165, 255) : new Color32(170, 180, 192, 255);

        static Color FlavorColor(ItemRarity rarity) => rarity == ItemRarity.Raro
            ? new Color32(196, 164, 176, 255)
            : rarity == ItemRarity.Incomum ? new Color32(140, 196, 170, 255) : new Color32(150, 170, 196, 255);
    }
}
