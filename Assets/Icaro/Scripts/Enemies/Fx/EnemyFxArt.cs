using System;

namespace Icaro
{
    // Estilo dos efeitos de um inimigo: as cores e o tamanho dos quadros
    public enum EnemyFxStyle
    {
        Moth = 0,   // Mariposa-lanterna (Zangão): gota âmbar com borda lilás
        Plasma = 1, // Explorador Tomado (Vagante): plasma ciano de miolo branco
        Spore = 2,  // morteiro de esporos do Vagante: bola âmbar com pintas de musgo
        Clod = 3,   // torrão do Pé-de-Musgo: terra escura com musgo em cima e pedrinhas
    }

    // Quadros dos efeitos dos inimigos em pixel art, desenhados em código (sem UnityEngine: o EnemyFxSprites converte
    // em Sprite e as prévias fora do Unity usam o mesmo desenho). Projéteis, ecos do rastro, clarão do disparo, carga
    // do aviso e impacto ficam a 24 px/m (a densidade dos inimigos); a marca de queda e a explosão do morteiro, a
    // 12 px/m. Projéteis apontam para a direita (+x). Sem brilho suave: o rastro e o desvanecer são pixels saltados.
    public static class EnemyFxArt
    {
        public const int PixelsPerMeter = 24;
        public const int GroundPixelsPerMeter = 12;

        static readonly Px[] k_Cyan = PixelRamps.Cyan;
        static readonly Px[] k_Amber = PixelRamps.Amber;
        static readonly Px[] k_Lilac = { Px.Hex(0x3a2a52), Px.Hex(0x6a5290), Px.Hex(0x9c86c4), Px.Hex(0xcdbfe6), Px.Hex(0xf2ecfb) };
        static readonly Px[] k_Moss = { Px.Hex(0x14281a), Px.Hex(0x22452a), Px.Hex(0x3a6a3a), Px.Hex(0x5f9a4c), Px.Hex(0x9cc870) };
        static readonly Px[] k_Red = PixelRamps.Red;
        static readonly Px[] k_Smoke = { Px.Hex(0x16141c), Px.Hex(0x26232e), Px.Hex(0x3a3644), Px.Hex(0x55505e), Px.Hex(0x77727e) };
        static readonly Px k_White = PixelRamps.White;
        static readonly Px[] k_Soil = { Px.Hex(0x140d09), Px.Hex(0x2a1b11), Px.Hex(0x45301d), Px.Hex(0x634528), Px.Hex(0x856036) };

        static Px[] Main(EnemyFxStyle style) =>
            style == EnemyFxStyle.Plasma ? k_Cyan : style == EnemyFxStyle.Clod ? k_Soil : k_Amber;
        static Px[] Rim(EnemyFxStyle style) => style == EnemyFxStyle.Plasma ? k_Cyan : style == EnemyFxStyle.Moth ? k_Lilac : k_Moss;

        // ---------- projétil ----------

        // 3 quadros do projétil em voo (tremendo um pouco), centro no meio da imagem
        public static PixelCanvas[] Bolt(EnemyFxStyle style)
        {
            var frames = new PixelCanvas[3];
            for (int f = 0; f < 3; f++)
                frames[f] = style == EnemyFxStyle.Plasma ? PlasmaBolt(f) : style == EnemyFxStyle.Moth ? MothBolt(f)
                    : style == EnemyFxStyle.Clod ? Clod(f) : Glob(f);
            return frames;
        }

        // Cápsula de plasma de 18 x 8: miolo branco na frente, corpo ciano, cauda que pisca
        static PixelCanvas PlasmaBolt(int frame)
        {
            var c = new PixelCanvas(18, 8);
            var random = new Random(11 + frame);
            c.FillFlat(PixelCanvas.Capsule(5f, 4f, 14f, 4f, 2.9f), k_Cyan[2]);
            c.FillFlat(PixelCanvas.Capsule(6.5f, 4f, 14.5f, 4f, 2.0f), k_Cyan[3]);
            c.FillFlat(PixelCanvas.Capsule(9f, 4f, 15f, 4f, 1.2f), k_Cyan[4]);
            c.Rect(11, 3, 4, 2, k_White);
            // cauda: pixels soltos que mudam a cada quadro
            for (int x = 0; x < 5; x++)
            {
                if (random.NextDouble() < 0.75 - x * 0.08)
                    c.Set(4 - x, 3 + random.Next(0, 2), x < 2 ? k_Cyan[3] : k_Cyan[2]);
                if (random.NextDouble() < 0.3)
                    c.Set(4 - x, random.Next(0, 2) == 0 ? 2 : 5, k_Cyan[1]);
            }

            // faísca em cima ou embaixo
            c.Set(8 + frame * 2, frame % 2 == 0 ? 1 : 6, k_Cyan[4]);
            c.Set(16, 3 + frame % 2, k_Cyan[4]);
            return c;
        }

        // Gota âmbar de 12 x 6 com a ponta branca e a borda lilás
        static PixelCanvas MothBolt(int frame)
        {
            var c = new PixelCanvas(12, 6);
            c.FillFlat(PixelCanvas.Ellipse(6.5f, 3f, 3.6f, 2.1f), k_Lilac[3]);
            c.FillFlat(PixelCanvas.Ellipse(7f, 3f, 3f, 1.5f), k_Amber[3]);
            c.FillFlat(PixelCanvas.Ellipse(8f, 3f, 1.8f, 0.9f), k_Amber[4]);
            c.Set(10, 2, k_White);
            c.Set(10, 3, k_White);
            c.Set(11, 2 + frame % 2, k_Amber[4]);
            c.Set(2, 2 + (frame + 1) % 2, k_Amber[2]);
            if (frame != 1)
                c.Set(1, 3 - frame / 2, k_Amber[1]);
            return c;
        }

        // Bola de esporos de 14 x 14 girando: âmbar com pintas escuras e musgo
        static PixelCanvas Glob(int frame)
        {
            var c = new PixelCanvas(14, 14) { Dither = 0.25f };
            c.Fill(PixelCanvas.Circle(7f, 7f, 5.4f), k_Amber);
            double a0 = frame * Math.PI / 6.0;
            for (int i = 0; i < 4; i++)
            {
                double a = a0 + i * Math.PI / 2.0;
                int x = (int)Math.Round(7 + Math.Cos(a) * 3.0 - 0.5);
                int y = (int)Math.Round(7 + Math.Sin(a) * 2.6 - 0.5);
                c.Set(x, y, k_Amber[1]);
                c.Set(x + 1, y, i % 2 == 0 ? k_Moss[3] : k_Amber[1]);
            }

            c.Set(5, 4, k_White);
            c.Set(4, 5, k_Amber[4]);
            c.Outline(PixelRamps.Outline);
            return c;
        }

        // Torrão de 22 x 22 girando: terra escura, musgo por cima e pedrinhas, com terra se soltando
        static PixelCanvas Clod(int frame)
        {
            var c = new PixelCanvas(22, 22) { Dither = 0.3f };
            var random = new Random(21);
            c.Fill(PixelCanvas.Ellipse(11f, 11.5f, 8.2f, 7.4f), k_Soil);
            double a0 = frame * Math.PI / 5.0;
            for (int i = 0; i < 7; i++)
            {
                double a = a0 + i * Math.PI * 2.0 / 7.0;
                int x = (int)Math.Round(11 + Math.Cos(a) * (3.0 + random.NextDouble() * 3.0) - 0.5);
                int y = (int)Math.Round(11.5 + Math.Sin(a) * (2.6 + random.NextDouble() * 2.6) - 0.5);
                if (Math.Sin(a) < -0.1)
                {
                    c.Set(x, y, k_Moss[3]);
                    c.Set(x + 1, y, k_Moss[2]);
                }
                else
                    c.Set(x, y, i % 2 == 0 ? k_Smoke[3] : k_Soil[1]);
            }

            for (int x = 5; x < 17; x++)
                if ((x + frame) % 3 != 0)
                    c.Set(x, 5 + (x + frame) % 2, k_Moss[x % 2 == 0 ? 3 : 4]);
            c.Set(3 + frame % 3, 18, k_Soil[2]);
            c.Set(18 - frame % 2, 17, k_Soil[1]);
            c.Outline(PixelRamps.Outline);
            return c;
        }

        // Nuvem de esporos (24 px/m, 40 x 30), 3 quadros respirando: bolhas de névoa verde-musgo com pintas âmbar e
        // ciano acesas, borda em xadrez
        public static PixelCanvas[] SporePuff()
        {
            var frames = new PixelCanvas[3];
            for (int f = 0; f < 3; f++)
            {
                var c = new PixelCanvas(40, 30);
                var random = new Random(40 + f);
                float grow = f * 0.6f;
                for (int b = 0; b < 6; b++)
                {
                    float bx = 9f + b * 4.6f + (float)random.NextDouble() * 2f;
                    float by = 17f - (b % 2) * 4f - (float)random.NextDouble() * 2f;
                    float r = 6.5f + (float)random.NextDouble() * 2.5f + grow;
                    for (int y = 0; y < c.Height; y++)
                    for (int x = 0; x < c.Width; x++)
                    {
                        double dx = x + 0.5 - bx, dy = (y + 0.5 - by) * 1.25;
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > r)
                            continue;
                        if (d > r - 1.6 && (x + y + f) % 2 != 0)
                            continue;
                        int level = d < r * 0.45 ? 3 : d < r * 0.8 ? 2 : 1;
                        if (dy < -r * 0.35)
                            level = Math.Min(4, level + 1);
                        c.Set(x, y, k_Moss[level].WithAlpha(level >= 3 ? 210 : 150));
                    }
                }

                for (int i = 0; i < 9; i++)
                    c.Set(random.Next(4, 36), random.Next(5, 25), i % 3 == 0 ? k_Cyan[4] : k_Amber[3]);
                frames[f] = c;
            }

            return frames;
        }

        // 3 ecos do rastro, do mais perto ao mais longe: o mesmo formato, mais escuro e com pixels saltados
        public static PixelCanvas[] Ghost(EnemyFxStyle style)
        {
            var frames = new PixelCanvas[3];
            for (int level = 0; level < 3; level++)
            {
                PixelCanvas c;
                if (style == EnemyFxStyle.Spore || style == EnemyFxStyle.Clod)
                {
                    bool clod = style == EnemyFxStyle.Clod;
                    c = new PixelCanvas(clod ? 12 : 8, clod ? 12 : 8);
                    float r = (clod ? 4.4f : 2.6f) - level * (clod ? 1f : 0.6f);
                    float m = clod ? 6f : 4f;
                    c.FillFlat(PixelCanvas.Circle(m, m, r), (clod ? k_Soil : k_Amber)[2 - Math.Min(level, 1)]);
                }
                else
                {
                    bool plasma = style == EnemyFxStyle.Plasma;
                    c = new PixelCanvas(plasma ? 12 : 8, plasma ? 6 : 4);
                    float half = plasma ? 3.6f - level * 0.7f : 2.2f - level * 0.4f;
                    float cy = plasma ? 3f : 2f;
                    float cx = plasma ? 6f : 4f;
                    c.FillFlat(PixelCanvas.Capsule(cx - half, cy, cx + half, cy, plasma ? 1.9f - level * 0.35f : 1.2f - level * 0.2f),
                        Main(style)[3 - level]);
                }

                Thin(c, level);
                frames[level] = c;
            }

            return frames;
        }

        // Some com pixels saltados: nível 0 inteiro, 1 em xadrez, 2 um em quatro
        static void Thin(PixelCanvas c, int level)
        {
            if (level == 0)
                return;
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                bool keep = level == 1 ? (x + y) % 2 == 0 : x % 2 == 0 && y % 2 == 0;
                if (!keep)
                    c.Erase(x, y);
            }
        }

        // ---------- disparo ----------

        // Clarão de 2 quadros: cruz com miolo branco, depois um aro partido
        public static PixelCanvas[] Flash(EnemyFxStyle style)
        {
            Px[] main = Main(style);
            int size = style == EnemyFxStyle.Plasma ? 13 : style == EnemyFxStyle.Moth ? 9 : 13;
            int m = size / 2;
            int arm = style == EnemyFxStyle.Moth ? 3 : 5;

            var first = new PixelCanvas(size, size);
            for (int i = 1; i <= arm; i++)
            {
                Px col = i <= 1 ? main[4] : i <= arm - 2 ? main[3] : main[2];
                first.Set(m + i, m, col);
                first.Set(m - i, m, col);
                first.Set(m, m + i, col);
                first.Set(m, m - i, col);
                if (i <= arm / 2)
                {
                    first.Set(m + i, m + i, main[2]);
                    first.Set(m - i, m - i, main[2]);
                    first.Set(m + i, m - i, main[2]);
                    first.Set(m - i, m + i, main[2]);
                }
            }

            first.Rect(m - 1, m - 1, 3, 3, main[4]);
            first.Set(m, m, k_White);
            if (style != EnemyFxStyle.Moth)
            {
                first.Set(m - 1, m, k_White);
                first.Set(m + 1, m, k_White);
            }

            var second = new PixelCanvas(size, size);
            float ro = arm + 0.6f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - (m + 0.5), dy = y + 0.5 - (m + 0.5);
                double d = Math.Sqrt(dx * dx + dy * dy);
                double a = Math.Atan2(dy, dx);
                if (d > ro - 1.3 && d < ro && Math.Sin(a * 4) > -0.2)
                    second.Set(x, y, main[2]);
            }

            second.Set(m, m, main[3]);
            return new[] { first, second };
        }

        // Carga do aviso em 4 quadros (escolhidos pelo progresso): um núcleo que cresce e pixels sendo puxados para ele
        public static PixelCanvas[] Charge(EnemyFxStyle style)
        {
            Px[] main = Main(style);
            Px[] rim = Rim(style);
            bool small = style == EnemyFxStyle.Moth;
            int size = small ? 11 : 15;
            float m = size / 2f;
            var frames = new PixelCanvas[4];
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(size, size);
                float r = (small ? 1.2f : 1.6f) + f * (small ? 0.55f : 0.75f);
                c.FillFlat(PixelCanvas.Circle(m, m, r + 0.8f), main[1]);
                c.FillFlat(PixelCanvas.Circle(m, m, r), main[3]);
                c.FillFlat(PixelCanvas.Circle(m, m, r * 0.5f + 0.3f), f >= 2 ? k_White : main[4]);
                float pull = (small ? 5f : 7f) - f * (small ? 1f : 1.3f);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3.0 + f * 0.45;
                    int x = (int)Math.Floor(m + Math.Cos(a) * pull);
                    int y = (int)Math.Floor(m + Math.Sin(a) * pull);
                    c.Set(x, y, i % 2 == 0 ? main[4] : rim[3]);
                }

                frames[f] = c;
            }

            return frames;
        }

        // ---------- impacto ----------

        // 5 quadros: disco claro, anel com faíscas, anel partido, pontos se espalhando e caindo, restos
        public static PixelCanvas[] Impact(EnemyFxStyle style)
        {
            Px[] main = Main(style);
            Px[] rim = Rim(style);
            bool small = style == EnemyFxStyle.Moth;
            int size = small ? 13 : 19;
            float m = size / 2f;
            float k = small ? 0.65f : 1f;
            var frames = new PixelCanvas[5];
            var random = new Random(style == EnemyFxStyle.Plasma ? 5 : 9);

            var c0 = new PixelCanvas(size, size);
            c0.FillFlat(PixelCanvas.Circle(m, m, 3.4f * k), main[3]);
            c0.FillFlat(PixelCanvas.Circle(m, m, 2.2f * k), main[4]);
            c0.FillFlat(PixelCanvas.Circle(m, m, 1.1f * k + 0.2f), k_White);
            frames[0] = c0;

            var c1 = new PixelCanvas(size, size);
            Ring(c1, m, 4.8f * k, 1.4f, main[4], 1f);
            c1.FillFlat(PixelCanvas.Circle(m, m, 1.6f * k), main[2]);
            Sparks(c1, m, 6.5f * k, 2, main[4], rim[3], 0.0);
            frames[1] = c1;

            var c2 = new PixelCanvas(size, size);
            Ring(c2, m, 6.6f * k, 1.2f, main[3], 0.55f);
            Sparks(c2, m, 8.2f * k, 2, main[3], rim[2], 0.25);
            frames[2] = c2;

            var c3 = new PixelCanvas(size, size);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4.0 + 0.2;
                c3.Set((int)Math.Floor(m + Math.Cos(a) * 7.4 * k), (int)Math.Floor(m + Math.Sin(a) * 7.4 * k + 1), main[2]);
            }

            for (int i = 0; i < 4; i++)
                c3.Set(random.Next(2, size - 2), (int)(m + random.Next(1, (int)(5 * k) + 2)), rim[2]);
            frames[3] = c3;

            var c4 = new PixelCanvas(size, size);
            for (int i = 0; i < 3; i++)
                c4.Set(random.Next(3, size - 3), (int)(m + random.Next(2, (int)(6 * k) + 2)), main[1]);
            frames[4] = c4;
            return frames;
        }

        static void Ring(PixelCanvas c, float m, float radius, float width, Px color, float coverage)
        {
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                double dx = x + 0.5 - m, dy = y + 0.5 - m;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < radius - width || d > radius)
                    continue;
                double a = Math.Atan2(dy, dx);
                if (coverage < 1f && Math.Sin(a * 5 + 0.7) < 1 - coverage * 2)
                    continue;
                c.Set(x, y, color);
            }
        }

        // Faíscas: traços curtos saindo em 4 diagonais
        static void Sparks(PixelCanvas c, float m, float radius, int length, Px head, Px tail, double turn)
        {
            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 4.0 + i * Math.PI / 2.0 + turn;
                for (int j = 0; j < length; j++)
                {
                    double r = radius - j;
                    c.Set((int)Math.Floor(m + Math.Cos(a) * r), (int)Math.Floor(m + Math.Sin(a) * r), j == 0 ? head : tail);
                }
            }
        }

        // Queimado deixado na parede ou no chão (24 px/m, 10 x 10): mancha escura irregular com brasas da cor do tiro
        public static PixelCanvas Scorch(EnemyFxStyle style)
        {
            var c = new PixelCanvas(10, 10);
            var random = new Random(style == EnemyFxStyle.Plasma ? 3 : 8);
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                double dx = x + 0.5 - 5, dy = y + 0.5 - 5;
                double d = Math.Sqrt(dx * dx + dy * dy) + random.NextDouble() * 1.4;
                if (d < 2.6)
                    c.Set(x, y, k_Smoke[0].WithAlpha(230));
                else if (d < 4.4 && (x + y) % 2 == 0)
                    c.Set(x, y, k_Smoke[1].WithAlpha(170));
            }

            Px[] main = Main(style);
            c.Set(4, 4, main[2]);
            c.Set(6, 5, main[1]);
            c.Set(3, 6, main[1]);
            return c;
        }

        // ---------- morteiro ----------

        // Marca de queda no chão (12 px/m), 8 quadros pelo progresso: aro vermelho fixo e um aro âmbar fechando até o
        // centro; no último, o miolo se enche em xadrez
        public static PixelCanvas[] Marker(float radiusMeters)
        {
            int r = (int)Math.Ceiling(radiusMeters * GroundPixelsPerMeter);
            int size = r * 2 + 2;
            float m = size / 2f;
            var frames = new PixelCanvas[8];
            for (int f = 0; f < 8; f++)
            {
                var c = new PixelCanvas(size, size);
                // aro de fora tracejado em degraus de 2 px
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double dx = x + 0.5 - m, dy = y + 0.5 - m;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double a = Math.Atan2(dy, dx);
                    if (d <= r && d > r - 2)
                    {
                        int dash = (int)Math.Floor((a + Math.PI) / (2 * Math.PI) * 24);
                        c.Set(x, y, dash % 2 == 0 ? k_Red[3] : k_Red[2]);
                    }

                    float inner = r * (1f - (f + 1) / 8f);
                    if (f < 7 && d <= inner + 1 && d > inner - 0.5)
                        c.Set(x, y, k_Amber[3]);
                    else if (d < r - 2 && (x + y) % 2 == 0 && (x / 2 + y / 2) % 2 == 0)
                        c.Set(x, y, (f == 7 ? k_Red[3] : k_Red[1]).WithAlpha(f == 7 ? 230 : 120));
                }

                // cruz no centro
                int ci = (int)m;
                c.Set(ci, ci, k_Amber[4]);
                c.Set(ci - 1, ci, k_Amber[3]);
                c.Set(ci, ci - 1, k_Amber[3]);
                c.Set(ci - 1, ci - 1, k_Amber[3]);
                frames[f] = c;
            }

            return frames;
        }

        // Explosão de esporos (12 px/m) em 6 quadros: clarão, bola de fogo de borda irregular, esporos saindo, fumaça
        // com brasas, só a fumaça rala e as últimas brasas
        public static PixelCanvas[] Blast(float radiusMeters)
        {
            int r = (int)Math.Ceiling(radiusMeters * GroundPixelsPerMeter);
            int size = r * 2 + 4;
            float m = size / 2f;
            var frames = new PixelCanvas[6];
            var random = new Random(31);
            double[] wobble = new double[5];
            for (int i = 0; i < wobble.Length; i++)
                wobble[i] = random.NextDouble() * 6.28;

            for (int f = 0; f < 6; f++)
            {
                var c = new PixelCanvas(size, size);
                float grow = new[] { 0.35f, 0.62f, 0.85f, 1f, 1.05f, 1.08f }[f];
                float R = r * grow;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double dx = x + 0.5 - m, dy = (y + 0.5 - m) * 1.08;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double a = Math.Atan2(dy, dx);
                    double edge = R * (1 + 0.09 * Math.Sin(a * 5 + wobble[0]) + 0.06 * Math.Sin(a * 9 + wobble[1]));
                    double u = d / edge;
                    if (u > 1)
                        continue;
                    bool even = (x + y) % 2 == 0;
                    switch (f)
                    {
                        case 0:
                            c.Set(x, y, u < 0.6 ? k_White : k_Amber[4]);
                            break;
                        case 1:
                            c.Set(x, y, u < 0.35 ? k_White : u < 0.65 ? k_Amber[4] : u < 0.88 ? k_Amber[3] : k_Amber[2]);
                            break;
                        case 2:
                            if (u > 0.55)
                                c.Set(x, y, u < 0.72 ? k_Amber[3] : u < 0.9 ? k_Amber[2] : k_Smoke[2]);
                            else if (u > 0.3)
                                c.Set(x, y, even ? k_Amber[3] : k_Smoke[3]);
                            else if (even)
                                c.Set(x, y, k_Smoke[2]);
                            break;
                        case 3:
                            if (u > 0.6 && u < 0.95)
                                c.Set(x, y, even ? k_Smoke[3] : k_Smoke[2]);
                            else if (u >= 0.3 && u <= 0.6 && even)
                                c.Set(x, y, k_Smoke[2]);
                            break;
                        case 4:
                            if (u > 0.68 && (x % 2 == 0 && y % 2 == 0 || (x + y) % 4 == 0))
                                c.Set(x, y, k_Smoke[2]);
                            break;
                        case 5:
                            if (u > 0.82 && x % 2 == 0 && y % 2 == 0 && (x / 2 + y / 2) % 2 == 0)
                                c.Set(x, y, k_Smoke[1]);
                            break;
                    }
                }

                // esporos e brasas voando para fora
                if (f >= 1)
                {
                    var r2 = new Random(70);
                    int count = 18;
                    for (int i = 0; i < count; i++)
                    {
                        double a = r2.NextDouble() * Math.PI * 2;
                        double speed = 0.55 + r2.NextDouble() * 0.6;
                        double d = r * speed * (0.5 + f * 0.25);
                        int x = (int)Math.Floor(m + Math.Cos(a) * d);
                        int y = (int)Math.Floor(m + Math.Sin(a) * d * 0.9 + f * f * 0.4);
                        if (f >= 5 && i % 3 != 0)
                            continue;
                        Px col = i % 3 == 0 ? k_Moss[f >= 4 ? 2 : 4] : f >= 4 ? k_Amber[1] : k_Amber[f >= 3 ? 2 : 4];
                        c.Set(x, y, col);
                        if (f <= 2)
                            c.Set(x + (Math.Cos(a) > 0 ? -1 : 1), y, i % 3 == 0 ? k_Moss[2] : k_Amber[2]);
                    }
                }

                frames[f] = c;
            }

            return frames;
        }
    }
}
