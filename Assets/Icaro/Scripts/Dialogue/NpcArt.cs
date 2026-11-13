using static Icaro.PixelCanvas;

namespace Icaro
{
    // Desenho dos NPCs em pixel art (16 px/m, a escala dos inimigos e das árvores), feito em código. Cada NPC tem a
    // folha de cor e a de brilho (as partes que brilham na cor cheia; o resto escuro), do mesmo tamanho. Pivô no chão,
    // no meio da largura. A chave vem do DialogueNpc.Art.
    public static class NpcArt
    {
        public const float PixelsPerMeter = 16f;

        public static (PixelCanvas Color, PixelCanvas Glow) Paint(string key)
        {
            switch (key)
            {
                case "Meton":
                    return Meton();
                default:
                    return Pilgrim();
            }
        }

        static readonly Px Outline = new Px(16, 14, 24);

        // Peregrino aeônida: manto encapuzado violeta-acinzentado, rosto na sombra com dois olhos ciano, cajado com um
        // cristal lilás e um pingente de cristal no peito. 24 x 32 px (1,5 x 2 m).
        static (PixelCanvas, PixelCanvas) Pilgrim()
        {
            var c = new PixelCanvas(24, 32);
            var g = new PixelCanvas(24, 32);
            Px[] robe = { new Px(38, 32, 52), new Px(56, 48, 74), new Px(76, 66, 98), new Px(98, 88, 122) };
            Px[] hood = { new Px(32, 27, 45), new Px(48, 41, 64), new Px(66, 57, 86), new Px(86, 76, 110) };
            Px[] staff = { new Px(54, 44, 40), new Px(80, 66, 56), new Px(104, 88, 72) };
            var lilac = new Px(206, 160, 255);
            var lilacLight = new Px(240, 220, 255);
            var cyan = new Px(130, 245, 255);

            // Cajado atrás do corpo, à esquerda
            c.Fill(Tube(4.5f, 5f, 4.5f, 31.5f, 0.9f), staff);
            // Manto: do ombro até a barra, alargando
            c.Fill(Poly(1.5f, 8f, 12f, 17f, 12f, 20f, 31.5f, 4.5f, 31.5f), robe, 0.1f);
            // Faixa na cintura
            c.Rect(7, 20, 11, 2, new Px(120, 86, 48));
            c.Rect(7, 20, 11, 1, new Px(160, 116, 60));
            // Braço segurando o cajado e a mão
            c.Fill(Capsule(10f, 14f, 6f, 18f, 1.7f), robe, 0.2f);
            c.Rect(4, 17, 2, 2, new Px(168, 168, 190));
            // Capuz e o rosto na sombra
            c.Fill(Ellipse(12.5f, 8.5f, 5.2f, 5.8f), hood, 0.15f);
            c.FillFlat(Ellipse(12.8f, 9.6f, 3.1f, 3.3f), new Px(12, 10, 18));
            // Barra rasgada
            for (int x = 5; x <= 19; x += 3)
                c.Erase(x, 31);

            Outlined(c);

            // Partes que brilham (desenhadas por cima do contorno)
            Glow(c, g, 11, 9, cyan);
            Glow(c, g, 14, 9, cyan);
            // Cristal do cajado
            Glow(c, g, 4, 1, lilacLight);
            Glow(c, g, 3, 2, lilac);
            Glow(c, g, 4, 2, lilacLight);
            Glow(c, g, 5, 2, lilac);
            Glow(c, g, 3, 3, lilac);
            Glow(c, g, 4, 3, lilac);
            Glow(c, g, 5, 3, lilac);
            Glow(c, g, 4, 4, lilac);
            // Pingente
            Glow(c, g, 12, 16, lilacLight);
            Glow(c, g, 12, 17, lilac);
            Glow(c, g, 13, 17, lilac);
            Glow(c, g, 12, 18, lilac);
            return (c, g);
        }

        // Méton, o mercador de instantes: alto e magro, casaco comprido de remendos de várias épocas, cachecol vermelho,
        // cartola de aba larga, máscara de porcelana sorridente com olhos âmbar acesos atrás dela, e nas costas uma mochila
        // enorme de gavetas com relógios pendurados (cada um numa hora), um cobertor enrolado e uma lanterna no alto; na
        // mão, um relógio de bolso aceso na corrente. 32 x 44 px (2 x 2,75 m).
        static (PixelCanvas, PixelCanvas) Meton()
        {
            var c = new PixelCanvas(32, 44);
            var g = new PixelCanvas(32, 44);
            Px[] coat = { new Px(38, 28, 34), new Px(60, 44, 48), new Px(84, 62, 62), new Px(108, 84, 78) };
            Px[] wood = { new Px(42, 28, 22), new Px(66, 46, 30), new Px(94, 68, 42), new Px(122, 90, 56) };
            Px[] mask = { new Px(166, 160, 158), new Px(204, 198, 194), new Px(230, 226, 222), new Px(250, 248, 244) };
            Px[] hat = { new Px(22, 18, 28), new Px(36, 30, 44), new Px(54, 46, 62) };
            Px[] red = { new Px(90, 28, 34), new Px(130, 44, 48), new Px(164, 66, 62) };
            Px[] teal = { new Px(28, 66, 72), new Px(42, 96, 100), new Px(66, 128, 128) };
            Px[] violet = { new Px(60, 44, 84), new Px(84, 62, 112), new Px(108, 84, 140) };
            Px[] brassRamp = { new Px(120, 84, 36), new Px(180, 136, 62), new Px(226, 186, 100) };
            var brass = new Px(196, 150, 70);
            var dark = new Px(24, 18, 24);
            var face = new Px(226, 214, 186);
            var glove = new Px(58, 48, 60);
            var amber = new Px(255, 178, 70);
            var amberLight = new Px(255, 226, 150);

            // Mochila de gavetas atrás do corpo (aparece dos lados e acima dos ombros)
            c.Fill(Plate(6f, 4f, 20f, 27f, 2f, 1.5f), wood);
            for (int y = 10; y <= 28; y += 6)
            {
                c.Line(7, y, 24, y, wood[0]);
                c.Set(8, y - 3, brass);
                c.Set(23, y - 3, brass);
            }
            // Cobertor enrolado no alto e a lanterna
            c.Fill(Capsule(8f, 3.5f, 23f, 3.5f, 1.6f), teal);
            c.Rect(24, 0, 3, 4, dark);

            // Relógios pendurados dos lados (cada um numa hora)
            Clock(c, 4.5f, 15f, 2.6f, brassRamp, face, dark, 1, -1);
            Clock(c, 27.5f, 21f, 2.3f, brassRamp, face, dark, -1, 0);
            Clock(c, 4.5f, 26f, 2f, brassRamp, face, dark, 0, 1);
            c.Line(6, 11, 5, 12, dark);
            c.Line(26, 17, 27, 18, dark);

            // Pernas finas e botas
            c.Rect(13, 36, 2, 6, new Px(34, 30, 40));
            c.Rect(17, 36, 2, 6, new Px(34, 30, 40));
            c.Rect(12, 41, 3, 3, dark);
            c.Rect(17, 41, 4, 3, dark);

            // Casaco comprido, aberto embaixo, com remendos costurados
            c.Fill(Poly(0.1f, 11f, 15f, 21f, 15f, 23.5f, 38f, 8.5f, 38f), coat, 0.1f);
            c.Line(16, 27, 16, 37, coat[0]);
            Patch(c, 12, 22, 3, 3, red);
            Patch(c, 18, 29, 3, 4, teal);
            Patch(c, 11, 32, 2, 3, brassRamp);
            Patch(c, 19, 19, 2, 2, violet);
            // Cinto com fivela
            c.Rect(11, 26, 11, 1, new Px(46, 30, 24));
            c.Set(16, 26, brass);

            // Braços: o esquerdo solto, o direito segurando o relógio de bolso na corrente
            c.Fill(Capsule(11.5f, 16.5f, 9.5f, 28f, 1.4f), coat, 0.15f);
            c.Rect(9, 28, 2, 2, glove);
            c.Fill(Capsule(20.5f, 16.5f, 23f, 24.5f, 1.4f), coat, 0.15f);
            c.Rect(22, 25, 2, 2, glove);
            c.Line(19, 24, 23, 27, brass);
            c.Fill(Circle(24.5f, 28.5f, 1.7f), brassRamp);

            // Cachecol
            c.Fill(Plate(12.5f, 13.5f, 8f, 2.5f, 0.8f), red);
            c.Fill(Plate(17.5f, 15.5f, 2f, 5f, 0.6f), red);

            // Cabeça: máscara de porcelana sorridente
            c.Fill(Ellipse(16.5f, 10f, 3.8f, 3.9f), mask, 0.1f);
            c.Set(15, 12, dark);
            c.Set(16, 13, dark);
            c.Set(17, 13, dark);
            c.Set(18, 12, dark);
            c.Set(13, 11, new Px(222, 150, 150));
            c.Set(20, 11, new Px(222, 150, 150));

            // Cartola de aba larga com fita e um relógio miúdo
            c.Fill(Plate(12f, 0.5f, 9f, 5.5f, 1f), hat);
            c.Rect(12, 5, 9, 1, red[1]);
            c.Rect(9, 6, 15, 1, hat[2]);
            c.Rect(10, 5, 2, 1, hat[1]);
            c.Rect(21, 5, 2, 1, hat[1]);
            c.Set(19, 5, brass);

            Outlined(c);

            // Partes acesas
            Glow(c, g, 14, 9, amber);
            Glow(c, g, 19, 9, amber);
            Glow(c, g, 25, 1, amberLight);
            Glow(c, g, 25, 2, amber);
            Glow(c, g, 24, 28, amberLight);
            Glow(c, g, 25, 29, amber);
            Glow(c, g, 24, 29, amber);
            return (c, g);
        }

        // Relógio pendurado: aro de latão, mostrador claro e os ponteiros (direção do ponteiro das horas em dx, dy)
        static void Clock(PixelCanvas c, float cx, float cy, float r, Px[] brass, Px face, Px hand, int dx, int dy)
        {
            c.Fill(Circle(cx, cy, r), brass);
            c.FillFlat(Circle(cx, cy, r - 0.9f), face);
            int x = (int)cx, y = (int)cy;
            c.Set(x, y, hand);
            c.Set(x, y - 1, hand);
            c.Set(x + dx, y + dy, hand);
        }

        // Remendo costurado: retângulo sombreado com pontos claros na beira de cima
        static void Patch(PixelCanvas c, int x, int y, int w, int h, Px[] ramp)
        {
            c.Fill(Plate(x, y, w, h, 0.6f), ramp);
            for (int i = x; i < x + w; i += 2)
                c.Set(i, y, ramp[ramp.Length - 1]);
        }

        static void Glow(PixelCanvas color, PixelCanvas glow, int x, int y, Px px)
        {
            color.Set(x, y, px);
            glow.Set(x, y, px);
        }

        // Contorno escuro de 1 pixel em volta de tudo (lê melhor no escuro e de longe)
        static void Outlined(PixelCanvas c)
        {
            var mark = new bool[c.Width * c.Height];
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    if (c.Get(x, y).A > 0)
                        continue;
                    if (c.Get(x - 1, y).A > 0 || c.Get(x + 1, y).A > 0 || c.Get(x, y - 1).A > 0 || c.Get(x, y + 1).A > 0)
                        mark[y * c.Width + x] = true;
                }
            }

            for (int i = 0; i < mark.Length; i++)
                if (mark[i])
                    c.Set(i % c.Width, i / c.Width, Outline);
        }
    }
}
