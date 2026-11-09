using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Água congelada dos Picos (pedido do usuário em 2026-11-06: o gelo antigo, listras com pontos acesos, não parecia
    // água congelada; em 2026-11-07: o gelo novo, de 4 x 4 m, ficou repetitivo na cascata inteira): duas texturas em pixel
    // art a 16 px/m, sem brilho próprio, no shader Icaro/FrozenIce, que as projeta pelo mundo e quebra a repetição em
    // placas (cada uma desloca e gira a textura e muda o tom), com manchas grandes de tom, neve nas faces de cima e brilho.
    //   - Picos_Gelo_Cascata (8 x 16 m): cascata congelada vista de frente: colunas de gelo verticais de largura variada
    //     (beira clara de um lado, miolo azul, sulco fundo entre elas; algumas leitosas) que serpenteiam, lábios de
    //     escorrido em alturas soltas com pingos para baixo, linhas de escoamento, bolhas em fila e geada.
    //   - Picos_Gelo_Lago (8 x 8 m): rio congelado visto de cima: gelo transparente escuro e manchas leitosas por ruído
    //     sem padrão, rachaduras claras com sombra, bolhas presas em cachos e em fila, flores de geada.
    // As duas repetem sem costura nos dois eixos.
    public static partial class PicosTextureKit
    {
        static readonly Color k_IceGroove = new Color(0.09f, 0.2f, 0.36f);
        static readonly Color k_IcePale = new Color(0.74f, 0.9f, 0.98f);
        static readonly Color k_IceClear = new Color(0.17f, 0.36f, 0.55f);
        static readonly Color k_IceDark = new Color(0.1f, 0.22f, 0.37f);
        static readonly Color k_IceMilk = new Color(0.52f, 0.72f, 0.87f);

        static void BuildIce(Set set) => IceMaterials(out set.IceFall, out set.IceSheet);

        // Os dois materiais (também usado pela prévia do gelo, PicosIcePreview)
        internal static void IceMaterials(out Material fall, out Material sheet)
        {
            fall = IceMaterial("Picos_Gelo_Cascata", PaintIceFall(), true);
            sheet = IceMaterial("Picos_Gelo_Lago", PaintIceSheet(), false);
        }

        // Material do gelo no Icaro/FrozenIce; os ajustes são regravados a cada geração. Cascata: placas altas e estreitas
        // (sem girar, para as colunas continuarem em pé) com um sulco escuro na divisa e manchas compridas na vertical; rio:
        // placas largas giradas com rachadura clara na divisa e um pouco de neve soprada
        static Material IceMaterial(string name, Paint paint, bool fall)
        {
            Material material = ShaderMaterial(name, "Icaro/FrozenIce");
            Texture2D texture = GridTextureKit.Save(name, paint.W, paint.Pixels);
            material.SetTexture("_IceMap", texture);
            material.SetTexture("_BaseMap", texture);
            material.SetVector("_Tile", new Vector4(paint.W / 16f, paint.H / 16f, 0f, 0f));
            material.SetFloat("_TexelsPerMeter", 16f);
            material.SetFloat("_Smoothness", fall ? 0.5f : 0.62f);
            material.SetVector("_CellSize", fall ? new Vector4(4.5f, 19f, 0f, 0f) : new Vector4(10f, 10f, 0f, 0f));
            material.SetFloat("_CellRotate", fall ? 0f : 1f);
            material.SetFloat("_CellTint", fall ? 0.09f : 0.1f);
            material.SetFloat("_CellMilk", fall ? 0.3f : 0.35f);
            material.SetFloat("_CrackAmount", fall ? 1f : 0.75f);
            material.SetFloat("_CrackWobble", fall ? 0.35f : 0.9f);
            material.SetColor("_CrackColor", fall ? k_IceGroove * 0.85f : k_Frost);
            material.SetColor("_CrackSide", fall ? Color.Lerp(k_IceDeep, k_IcePale, 0.55f) : Color.Lerp(k_IceDark, k_IceGroove, 0.5f));
            material.SetVector("_MacroScale", fall ? new Vector4(7f, 46f, 0f, 0f) : new Vector4(30f, 30f, 0f, 0f));
            material.SetFloat("_MacroAmount", fall ? 0.6f : 0.55f);
            material.SetFloat("_SnowAmount", fall ? 0.4f : 0.26f);
            material.SetFloat("_SnowScale", fall ? 6f : 9f);
            material.SetFloat("_SnowSlope", fall ? 3f : 1.5f);
            material.SetFloat("_Rim", fall ? 0.35f : 0.3f);
            material.SetFloat("_SparkleAmount", fall ? 0.9f : 1.1f);
            UnityEditor.EditorUtility.SetDirty(material);
            return material;
        }

        // Pixel com o y dando a volta (o Paint só dá a volta no x)
        static int WrapIndex(Paint p, int x, int y) => (((y % p.H) + p.H) % p.H) * p.W + ((x % p.W) + p.W) % p.W;
        static void PutWrapped(Paint p, int x, int y, Color c) => p.Set(x, ((y % p.H) + p.H) % p.H, c);
        static void BlendWrapped(Paint p, int x, int y, Color c, float t)
        {
            int i = WrapIndex(p, x, y);
            p.Pixels[i] = Color.Lerp(p.Pixels[i], c, t);
        }

        // Ruído de valor que repete no ladrilho: grade de px x py células que dá a volta; Fbm em quatro oitavas
        static float TileNoise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float H(int i, int j)
            {
                i = ((i % px) + px) % px;
                j = ((j % py) + py) % py;
                uint h = (uint)(i * 73856093 ^ j * 19349663 ^ seed * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777215f;
            }

            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        static float TileFbm(int x, int y, int w, int h, int cellsX, int cellsY, int seed)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int o = 0; o < 4; o++)
            {
                int cx = cellsX << o, cy = cellsY << o;
                sum += TileNoise((x + 0.5f) * cx / w, (y + 0.5f) * cy / h, cx, cy, seed + o * 101) * weight;
                total += weight;
                weight *= 0.5f;
            }

            return sum / total;
        }

        static Paint PaintIceFall()
        {
            const int w = 128, h = 256;
            Paint p = GridTextureKit.Tile(w, h, 1061, k_Ice);
            var random = new System.Random(1061);
            float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);

            // Colunas: a maioria de 4 a 9 px e algumas largas (10 a 16 px, os "tubos"); somam 128
            var starts = new System.Collections.Generic.List<int>();
            for (int x = 0; x < w;)
            {
                starts.Add(x);
                int width = random.NextDouble() < 0.28 ? random.Next(10, 17) : random.Next(4, 10);
                if (w - x - width < 4)
                    width = w - x;
                x += width;
            }

            int count = starts.Count;
            int WidthOf(int i) => (i + 1 < count ? starts[i + 1] : w) - starts[i];

            // Cada divisa serpenteia com dois senos que fecham na altura (1 px; 2 px entre colunas largas)
            var waves = new (int K1, float P1, int K2, float P2, float Amp)[count];
            var tone = new (float Shade, float Milk)[count];
            for (int i = 0; i < count; i++)
            {
                int narrow = Mathf.Min(WidthOf(i), WidthOf((i - 1 + count) % count));
                waves[i] = (random.Next(1, 4), Rand(0f, 6.283f), random.Next(4, 7), Rand(0f, 6.283f), narrow >= 9 ? 2f : 1f);
                tone[i] = (Rand(0.88f, 1.1f), random.NextDouble() < 0.3 ? Rand(0.3f, 0.6f) : 0f);
            }

            int Border(int i, int y)
            {
                if (i >= count)
                    return w;
                (int k1, float p1, int k2, float p2, float amp) = waves[i];
                float s = Mathf.Sin(6.2832f * k1 * y / h + p1) * amp * 0.75f + Mathf.Sin(6.2832f * k2 * y / h + p2) * 0.45f;
                return starts[i] + Mathf.Clamp(Mathf.RoundToInt(s), -(int)amp, (int)amp);
            }

            // Lábios de escorrido em alturas soltas por coluna: a linha de geada, a luz em cima e a sombra embaixo (y = 0
            // é a linha de baixo da textura); cada lábio cobre só um pedaço da largura
            var lipKind = new sbyte[count, h];
            var lipFrom = new float[count, h];
            var lipTo = new float[count, h];
            var lips = new System.Collections.Generic.List<(int Column, int Y, float U0, float U1)>();
            for (int i = 0; i < count; i++)
            {
                int n = random.Next(3, 9);
                for (int k = 0; k < n; k++)
                {
                    int y0 = random.Next(0, h);
                    float u0 = Rand(0.05f, 0.3f), u1 = Rand(0.65f, 0.92f);
                    lips.Add((i, y0, u0, u1));
                    foreach ((int dy, sbyte kind) in new[] { (1, (sbyte)3), (0, (sbyte)1), (-1, (sbyte)2), (-2, (sbyte)4) })
                    {
                        int y = ((y0 + dy) % h + h) % h;
                        lipKind[i, y] = kind;
                        lipFrom[i, y] = u0;
                        lipTo[i, y] = u1;
                    }
                }
            }

            for (int y = 0; y < h; y++)
            for (int i = 0; i < count; i++)
            {
                int x0 = Border(i, y), x1 = Border(i + 1, y), width = x1 - x0;
                (float shade, float milk) = tone[i];
                for (int x = x0; x < x1; x++)
                {
                    float u = (x - x0 + 0.5f) / width;
                    Color c = u < 0.18f ? k_IcePale
                        : u < 0.52f ? k_Ice
                        : u < 0.8f ? Color.Lerp(k_Ice, k_IceDeep, 0.55f)
                        : x == x1 - 1 ? k_IceGroove : k_IceDeep;
                    c = Color.Lerp(c, k_IceMilk, milk) * shade;
                    sbyte kind = lipKind[i, y];
                    if (kind != 0 && u > lipFrom[i, y] && u < lipTo[i, y])
                        c = kind == 1 ? k_Frost
                            : kind == 3 ? Color.Lerp(c, k_IcePale, 0.4f)
                            : kind == 2 ? Color.Lerp(c, k_IceGroove, 0.5f)
                            : Color.Lerp(c, k_IceDeep, 0.25f);
                    p.Set(x, y, p.Vary(c, 0.03f));
                }
            }

            // Pingos que descem dos lábios
            foreach ((int column, int y0, float u0, float u1) in lips)
            {
                int drips = random.Next(0, 4);
                for (int d = 0; d < drips; d++)
                {
                    int x0 = Border(column, y0), width = Border(column + 1, y0) - x0;
                    int x = x0 + Mathf.RoundToInt(Rand(u0, u1) * width);
                    int length = random.Next(1, 7);
                    for (int k = 1; k <= length; k++)
                        PutWrapped(p, x, y0 - 2 - k, k == length ? k_IcePale : k_Frost);
                }
            }

            // Linhas de escoamento: riscos compridos, claros ou escuros, dentro das colunas largas
            for (int n = 0; n < 60; n++)
            {
                int i = random.Next(0, count);
                if (WidthOf(i) < 7)
                    continue;
                int y = random.Next(0, h), length = random.Next(12, 90);
                float u = Rand(0.25f, 0.75f);
                bool light = random.NextDouble() < 0.5;
                for (int k = 0; k < length; k++)
                {
                    int yy = y + k, x0 = Border(i, ((yy % h) + h) % h), x1 = Border(i + 1, ((yy % h) + h) % h);
                    BlendWrapped(p, x0 + Mathf.RoundToInt(u * (x1 - x0)), yy, light ? k_IcePale : k_IceGroove, light ? 0.35f : 0.3f);
                }
            }

            // Bolhas soltas e em fila na vertical
            for (int n = 0; n < 220; n++)
            {
                int x = random.Next(0, w), y = random.Next(0, h);
                PutWrapped(p, x, y, k_Frost);
                if (random.NextDouble() < 0.3)
                    PutWrapped(p, x, y + 1, k_IcePale);
            }

            for (int n = 0; n < 26; n++)
            {
                int x = random.Next(0, w), y = random.Next(0, h), bubbles = random.Next(3, 7);
                for (int b = 0; b < bubbles; b++)
                    PutWrapped(p, x + (random.NextDouble() < 0.2 ? 1 : 0), y + b * random.Next(2, 4), b % 2 == 0 ? k_IcePale : k_Frost);
            }

            // Geada em placas pequenas e riscos de geada
            for (int n = 0; n < 24; n++)
            {
                int x = random.Next(0, w), y = random.Next(0, h), pixels = random.Next(3, 8);
                for (int k = 0; k < pixels; k++)
                    PutWrapped(p, x + random.Next(-1, 2), y + random.Next(-1, 2), k % 3 == 0 ? k_Snow : k_Frost);
            }

            for (int n = 0; n < 30; n++)
            {
                int x = random.Next(0, w), y = random.Next(0, h), length = random.Next(4, 18);
                for (int k = 0; k < length; k++)
                    BlendWrapped(p, x, y + k, k_Frost, 0.5f);
            }

            return p;
        }

        static Paint PaintIceSheet()
        {
            const int size = 128;
            Paint p = GridTextureKit.Tile(size, size, 1071, k_IceClear);
            var random = new System.Random(1071);

            // Gelo escuro e transparente, claro, leitoso e com geada pelo ruído que repete sem padrão (o ruído de valor puro
            // deixava as manchas em grade: as coordenadas são entortadas por outro ruído, que também repete no ladrilho);
            // o gelo escuro e o claro dominam, o leitoso fica em veios. Xadrez na passagem
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float wx = (TileFbm(x, y, size, size, 2, 2, 1081) - 0.5f) * 70f;
                float wy = (TileFbm(x, y, size, size, 2, 2, 1083) - 0.5f) * 70f;
                int sx = Mathf.RoundToInt(x + wx), sy = Mathf.RoundToInt(y + wy);
                float n = TileFbm(((sx % size) + size) % size, ((sy % size) + size) % size, size, size, 2, 2, 1071)
                          + (((x + y) & 1) == 0 ? 0.012f : -0.012f);
                float streak = TileFbm(x, y, size, size, 1, 6, 1077);           // faixas compridas de bolhas finas
                Color c = n < 0.42f ? k_IceDark
                    : n < 0.56f ? k_IceClear
                    : n < 0.62f ? Color.Lerp(k_IceClear, k_IceMilk, 0.45f)
                    : n < 0.68f ? k_IceMilk
                    : n < 0.72f ? Color.Lerp(k_IceMilk, k_Frost, 0.6f) : k_Frost;
                if (streak > 0.66f && n < 0.58f && ((x * 7 + y * 3) % 5) == 0)
                    c = Color.Lerp(c, k_IcePale, 0.6f);
                p.Set(x, y, p.Vary(c, 0.025f));
            }

            // Rachaduras: caminhos tortos de 1 px, claros, com a sombra do lado de baixo; às vezes se dividem
            void Crack(float x, float y, float angle, int length, int depth)
            {
                for (int k = 0; k < length; k++)
                {
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    PutWrapped(p, ix, iy, k_Frost);
                    PutWrapped(p, ix, iy - 1, Color.Lerp(k_IceDark, k_IceGroove, 0.5f));
                    angle += ((float)random.NextDouble() - 0.5f) * 0.7f;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle);
                    if (depth < 2 && random.NextDouble() < 0.05)
                        Crack(x, y, angle + (random.NextDouble() < 0.5 ? 1f : -1f) * (0.6f + (float)random.NextDouble() * 0.6f),
                            length / 3, depth + 1);
                }
            }

            for (int i = 0; i < 12; i++)
                Crack(random.Next(0, size), random.Next(0, size), (float)random.NextDouble() * 6.283f, random.Next(14, 70), 0);

            // Bolhas presas: cachos de pontos claros (alguns de 2 px) e filas curtas, no gelo escuro
            for (int i = 0; i < 30; i++)
            {
                int cx = random.Next(0, size), cy = random.Next(0, size), bubbles = random.Next(3, 12), spread = random.Next(2, 6);
                for (int b = 0; b < bubbles; b++)
                {
                    int x = cx + random.Next(-spread, spread + 1), y = cy + random.Next(-spread, spread + 1);
                    PutWrapped(p, x, y, b % 3 == 0 ? k_IcePale : k_Frost);
                    if (random.NextDouble() < 0.3)
                        PutWrapped(p, x + 1, y, k_IcePale);
                }
            }

            for (int i = 0; i < 14; i++)
            {
                int x = random.Next(0, size), y = random.Next(0, size), dx = random.Next(-1, 2), dy = dx == 0 ? 1 : random.Next(-1, 2);
                int bubbles = random.Next(4, 9);
                for (int b = 0; b < bubbles; b++)
                    PutWrapped(p, x + dx * b * 2, y + dy * b * 2, b % 2 == 0 ? k_Frost : k_IcePale);
            }

            // Flores de geada: estrelinhas de 3 x 3 e algumas de 5 x 5 com as diagonais
            for (int i = 0; i < 18; i++)
            {
                int x = random.Next(0, size), y = random.Next(0, size);
                bool big = random.NextDouble() < 0.35;
                PutWrapped(p, x, y, k_SnowBright);
                for (int r = 1; r <= (big ? 2 : 1); r++)
                {
                    Color c = r == 1 ? k_Frost : Color.Lerp(k_Frost, k_IceMilk, 0.4f);
                    PutWrapped(p, x - r, y, c);
                    PutWrapped(p, x + r, y, c);
                    PutWrapped(p, x, y - r, c);
                    PutWrapped(p, x, y + r, c);
                }

                if (big)
                {
                    PutWrapped(p, x - 1, y - 1, k_Frost);
                    PutWrapped(p, x + 1, y + 1, k_Frost);
                    PutWrapped(p, x - 1, y + 1, k_Frost);
                    PutWrapped(p, x + 1, y - 1, k_Frost);
                }
            }

            return p;
        }
    }
}
