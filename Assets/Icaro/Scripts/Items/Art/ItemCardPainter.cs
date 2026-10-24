using System;
using static Icaro.PixelCanvas;
using static Icaro.PixelRamps;

namespace Icaro
{
    // Fundo das cartas de item em pixel art (110 x 154, mostrado em 3x), um por raridade, no estilo de carta
    // colecionável: moldura, fundo com trilhas de circuito, área da arte e a caixa do efeito. Comum: moldura de aço
    // escovado; Incomum: moldura verde chanfrada com luzes neon; Raro: moldura vermelha com ouro, janela da arte com
    // cantoneiras e gemas. Os textos e a arte do item ficam por cima (ItemCardView), nas regiões abaixo.
    public static class ItemCardPainter
    {
        public const int Width = 110;
        public const int Height = 154;

        // Regiões em pixels da carta (origem no alto à esquerda)
        public const int HeaderCenterY = 12;
        public const int ArtCenterY = 49;
        public const int NameCenterY = 87;
        public const int BoxX = 7;
        public const int BoxY = 95;
        public const int BoxWidth = 96;
        public const int BoxHeight = 52;

        // Canto inferior direito da fileira de selos (os selos vão para a esquerda)
        public const int BadgesRight = 101;
        public const int BadgesBottom = 79;

        static readonly Px[] k_GreenFrame = Ramp(0x0b2a16, 0x14502a, 0x1f7a3c, 0x36b058, 0x86eb92);
        static readonly Px[] k_RedFrame = Ramp(0x2a050c, 0x5a0c18, 0x8f1426, 0xc42a3c, 0xf06a78);

        public static PixelCanvas Paint(ItemRarity rarity)
        {
            var c = new PixelCanvas(Width, Height) { Dither = 0.25f };
            switch (rarity)
            {
                case ItemRarity.Incomum: Uncommon(c); break;
                case ItemRarity.Raro: Rare(c); break;
                default: Common(c); break;
            }

            return c;
        }

        static void Common(PixelCanvas c)
        {
            c.FillFlat(Plate(0, 0, Width, Height, 0, 6), Outline);
            c.Fill(Plate(1, 1, Width - 2, Height - 2, 4, 5), Steel, 0.05f);
            Shape inner = Plate(5, 5, 100, 144, 0, 2);
            Gradient(c, inner, Px.Hex(0x8e98a6), Px.Hex(0x5f6878));
            Circuit(c, new Random(11), Px.Hex(0xb4bcc8).WithAlpha(110), Px.Hex(0x4f5766).WithAlpha(120));
            Box(c, 5, 5, 100, 144, Steel[1]);
            for (int y = 28; y <= 70; y++)
            {
                int a = (int)(170 * Math.Sin(Math.PI * (y - 28) / 42.0));
                c.Rect(8, y, 2, 1, White.WithAlpha(a));
                c.Rect(100, y, 2, 1, White.WithAlpha(a));
            }

            c.FillFlat(Ring(55, ArtCenterY, 28, 27), White.WithAlpha(60));
            c.FillFlat(Ring(55, ArtCenterY, 31, 30.3f), White.WithAlpha(30));
            EffectBox(c, Px.Hex(0x434a57, 238), Px.Hex(0xc4ccd8), 2);
        }

        static void Uncommon(PixelCanvas c)
        {
            c.FillFlat(Poly(0f, Chamfer(0, 0, Width, Height, 9)), Outline);
            c.Fill(Poly(3.5f, Chamfer(1, 1, Width - 2, Height - 2, 8.5f)), k_GreenFrame, 0.05f);
            Shape inner = Poly(0f, Chamfer(5, 5, 100, 144, 5.5f));
            Gradient(c, inner, Px.Hex(0x1a4f36), Px.Hex(0x0a2418));
            Circuit(c, new Random(22), Bio[2].WithAlpha(80), Px.Hex(0x06180f).WithAlpha(140));
            Edges(c, Chamfer(5, 5, 99, 143, 5), Bio[3]);
            for (int y = 31; y <= 67; y++)
            {
                int a = (int)(255 * Math.Min(1.0, 1.6 * Math.Sin(Math.PI * (y - 31) / 36.0)));
                c.Rect(9, y, 2, 1, Bio[4].WithAlpha(a));
                c.Rect(99, y, 2, 1, Bio[4].WithAlpha(a));
                c.Rect(11, y, 1, 1, Bio[3].WithAlpha(a / 3));
                c.Rect(98, y, 1, 1, Bio[3].WithAlpha(a / 3));
            }

            c.FillFlat(Ring(55, ArtCenterY, 28, 27), Bio[3].WithAlpha(90));
            c.FillFlat(Ring(55, ArtCenterY, 24, 23.3f), Bio[3].WithAlpha(45));
            for (int i = 0; i < 4; i++)
            {
                double a = i * Math.PI / 2.0;
                int x = 55 + (int)Math.Round(Math.Cos(a) * 29.5), y = ArtCenterY + (int)Math.Round(Math.Sin(a) * 29.5);
                c.Rect(x - 1, y - 1, 2, 2, Bio[4].WithAlpha(200));
            }

            foreach (int x in new[] { 7, 102 })
            {
                foreach (int y in new[] { 7, 146 })
                    c.Rect(x - 1 + (x > 55 ? 0 : 1), y - 1 + (y > 77 ? 0 : 1), 1, 1, Bio[4]);
            }

            EffectBox(c, Px.Hex(0x0c2c1e, 242), Bio[3], 3);
        }

        static void Rare(PixelCanvas c)
        {
            c.FillFlat(Plate(0, 0, Width, Height, 0, 5), Outline);
            c.Fill(Plate(1, 1, Width - 2, Height - 2, 4, 4), k_RedFrame, 0.05f);
            Shape inner = Plate(5, 5, 100, 144, 0, 1);
            Gradient(c, inner, Px.Hex(0x4a1220), Px.Hex(0x240810));
            Circuit(c, new Random(33), Red[2].WithAlpha(70), Px.Hex(0x14030a).WithAlpha(140));
            Box(c, 5, 5, 100, 144, Gold[3]);
            Box(c, 6, 6, 98, 142, Gold[1].WithAlpha(180));

            // Janela da arte com cantoneiras douradas
            c.Rect(15, 20, 80, 58, Px.Hex(0x2a0812, 235));
            Box(c, 14, 19, 82, 60, Gold[2]);
            foreach (int x in new[] { 12, 91 })
            {
                foreach (int y in new[] { 17, 74 })
                {
                    bool right = x > 55, bottom = y > 49;
                    c.Rect(x, bottom ? y + 5 : y, 7, 2, Gold[4]);
                    c.Rect(right ? x + 5 : x, y, 2, 7, Gold[4]);
                    c.Set(right ? x + 5 : x + 1, bottom ? y + 5 : y + 1, Gold[2]);
                }
            }

            Gem(c, 55, 3, 4);
            Gem(c, 55, 150, 4);
            Gem(c, 3, 77, 3.5f);
            Gem(c, 106, 77, 3.5f);
            foreach (int x in new[] { 6, 103 })
            {
                foreach (int y in new[] { 6, 147 })
                    Gem(c, x, y, 2.5f);
            }

            EffectBox(c, Px.Hex(0x2c2731, 242), Px.Hex(0xa08a92), 2);
        }

        // ---------- peças ----------

        // Caixa do efeito: fundo escuro com borda de 1 pixel e cantos chanfrados
        static void EffectBox(PixelCanvas c, Px fill, Px border, int chamfer)
        {
            float[] box = Chamfer(BoxX, BoxY, BoxWidth, BoxHeight, chamfer);
            c.FillFlat(Poly(0f, box), fill);
            Edges(c, Chamfer(BoxX, BoxY, BoxWidth - 1, BoxHeight - 1, chamfer), border);
        }

        // Gema vermelha com aro dourado (losango)
        static void Gem(PixelCanvas c, float x, float y, float r)
        {
            x += 0.5f;
            y += 0.5f;
            c.FillFlat(Poly(0f, x, y - r - 1.2f, x + r + 1.2f, y, x, y + r + 1.2f, x - r - 1.2f, y), Outline);
            c.Fill(Poly(1f, x, y - r, x + r, y, x, y + r, x - r, y), Gold);
            float g = r * 0.55f;
            c.Fill(Poly(0.8f, x, y - g, x + g, y, x, y + g, x - g, y), Red, 0.15f);
            c.Set((int)(x - g * 0.4f), (int)(y - g * 0.4f), White);
        }

        // Trilhas de circuito: segmentos em ângulo reto com nós nas pontas, só sobre o fundo já pintado
        static void Circuit(PixelCanvas c, Random random, Px light, Px shadow)
        {
            for (int n = 0; n < 26; n++)
            {
                int x = 8 + random.Next(47) * 2, y = 8 + random.Next(69) * 2;
                int dir = random.Next(4);
                int segments = 1 + random.Next(3);
                Node(c, x, y, light, shadow);
                for (int s = 0; s < segments; s++)
                {
                    int length = 3 + random.Next(9);
                    int dx = dir == 0 ? 1 : dir == 2 ? -1 : 0, dy = dir == 1 ? 1 : dir == 3 ? -1 : 0;
                    for (int i = 0; i < length; i++)
                    {
                        x += dx;
                        y += dy;
                        Trace(c, x, y, light, shadow);
                    }

                    dir = (dir + (random.Next(2) == 0 ? 1 : 3)) % 4;
                }

                Node(c, x, y, light, shadow);
            }
        }

        static void Trace(PixelCanvas c, int x, int y, Px light, Px shadow)
        {
            if (x < 6 || y < 6 || x > 103 || y > 147 || c.Get(x, y).A < 255)
                return;
            c.Set(x, y, light);
            c.Set(x + 1, y + 1, shadow);
        }

        static void Node(PixelCanvas c, int x, int y, Px light, Px shadow)
        {
            Trace(c, x, y, light, shadow);
            Trace(c, x + 1, y, light, shadow);
            Trace(c, x, y + 1, light, shadow);
            Trace(c, x + 1, y + 1, light, shadow);
        }

        static void Gradient(PixelCanvas c, Shape shape, Px top, Px bottom)
        {
            for (int y = 0; y < c.Height; y++)
            {
                Px color = Px.Lerp(top, bottom, y / (float)(c.Height - 1));
                for (int x = 0; x < c.Width; x++)
                {
                    if (shape(x + 0.5f, y + 0.5f, out _, out _))
                        c.Set(x, y, color);
                }
            }
        }

        // Contorno de 1 pixel de um retângulo (x, y, largura, altura)
        static void Box(PixelCanvas c, int x, int y, int w, int h, Px color)
        {
            c.Rect(x, y, w, 1, color);
            c.Rect(x, y + h - 1, w, 1, color);
            c.Rect(x, y, 1, h, color);
            c.Rect(x + w - 1, y, 1, h, color);
        }

        // Pontos de um retângulo com os cantos cortados em 45 graus
        static float[] Chamfer(float x, float y, float w, float h, float cut) => new[]
        {
            x + cut, y, x + w - cut, y, x + w, y + cut, x + w, y + h - cut, x + w - cut, y + h, x + cut, y + h,
            x, y + h - cut, x, y + cut,
        };

        static void Edges(PixelCanvas c, float[] xy, Px color)
        {
            int n = xy.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                c.Line((int)xy[i * 2], (int)xy[i * 2 + 1], (int)xy[j * 2], (int)xy[j * 2 + 1], color);
            }
        }

        static Px[] Ramp(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
