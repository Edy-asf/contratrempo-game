using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Pedras do kit gótico das vilas (pedido do usuário em 2026-11-08: casas, muros, postes e objetos tinham a mesma
    // pedra). Quase monocromático (cinza-azulado e violeta escuro, para o gótico), mas cada função com a própria
    // superfície, grão e valor: quatro paredes de casa (cantaria grande, cantaria miúda, pedra bruta argamassada e reboco
    // gasto com a pedra aparecendo), a moldura talhada, a coluna polida com veios, a pedra fina das esculturas, o soco
    // gasto, a pedra clara dos móveis e as escamas do telhado em três tons. Pixel art a 16 px/m; a linha 0 da imagem é a
    // de baixo (luz vinda de cima: a beira de cima de cada bloco clareia). Só usa a Paint e Color/Mathf, para a prévia
    // fora do Unity (icaro-tools/preview-picos) compilar o mesmo arquivo.
    public static partial class PicosTextureKit
    {
        // Base de cada pedra (o tom de cada peça muda um pouco por cima, no material)
        internal static readonly Color k_GothAshlar = new Color(0.4f, 0.41f, 0.47f);
        internal static readonly Color k_GothCoursed = new Color(0.33f, 0.33f, 0.39f);
        internal static readonly Color k_GothRubble = new Color(0.29f, 0.29f, 0.34f);
        internal static readonly Color k_GothRubbleMortar = new Color(0.47f, 0.48f, 0.53f);
        internal static readonly Color k_GothPlaster = new Color(0.47f, 0.46f, 0.52f);
        internal static readonly Color k_GothMolding = new Color(0.6f, 0.61f, 0.67f);
        internal static readonly Color k_GothColumn = new Color(0.21f, 0.21f, 0.26f);
        internal static readonly Color k_GothCarving = new Color(0.66f, 0.67f, 0.72f);
        internal static readonly Color k_GothPlinth = new Color(0.23f, 0.23f, 0.27f);
        internal static readonly Color k_GothFurniture = new Color(0.56f, 0.55f, 0.58f);
        internal static readonly Color[] k_GothScales =
        {
            new Color(0.34f, 0.35f, 0.4f),      // cinza (o de antes)
            new Color(0.24f, 0.27f, 0.34f),     // ardósia azulada
            new Color(0.3f, 0.26f, 0.33f),      // cinza-violeta
        };
        static readonly Color k_GothJoint = new Color(0.12f, 0.12f, 0.15f);
        // Tijolos de pedra (pedido do usuário em 2026-11-08: a família da parede das casas, com diferenças sutis)
        internal static readonly Color k_GothBrick = new Color(0.36f, 0.36f, 0.42f);
        internal static readonly Color k_GothBrickAlt = new Color(0.33f, 0.35f, 0.42f);
        internal static readonly Color k_GothBrickGrand = new Color(0.41f, 0.42f, 0.47f);
        internal static readonly Color k_GothBrickWorn = new Color(0.33f, 0.33f, 0.38f);

        // ---------- Ruído que repete na imagem ----------

        static float GHash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        // Ruído de valor com período inteiro (cells células cobrem a imagem toda: repete sem costura)
        static float GValue(float x, float y, int cells, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = ((x0 % cells) + cells) % cells, bx = (ax + 1) % cells;
            int ay = ((y0 % cells) + cells) % cells, by = (ay + 1) % cells;
            float a = Mathf.Lerp(GHash(ax, ay, seed), GHash(bx, ay, seed), fx);
            float b = Mathf.Lerp(GHash(ax, by, seed), GHash(bx, by, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        // Soma de oitavas, 0..1; 'cells' é o número de células da oitava mais grossa na imagem
        static float GFbm(int x, int y, int size, int cells, int seed, int octaves = 3)
        {
            float sum = 0f, weight = 0f, amp = 1f;
            for (int o = 0; o < octaves; o++)
            {
                float k = cells / (float)size;
                sum += GValue(x * k, y * k, cells, seed + o * 31) * amp;
                weight += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / weight;
        }

        // ---------- Blocos ----------

        // Bloco de cantaria de (x0, y0) a (x0 + w, y0 + h), repetindo pela imagem: face com manchas, beira de cima e da
        // esquerda claras, de baixo e da direita escuras, marcas de cinzel em diagonal (chisel > 0) e, às vezes, uma
        // quina lascada
        static void GBlock(Paint p, int x0, int y0, int w, int h, Color face, float chisel, int seed, float chip)
        {
            int size = p.W;
            bool chipped = GHash(x0, y0, seed + 5) < chip;
            int chipCorner = (int)(GHash(x0, y0, seed + 6) * 4f), chipSize = 2 + (int)(GHash(x0, y0, seed + 7) * 2f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int px = x0 + x, py = y0 + y;
                float mottle = 0.9f + 0.2f * GFbm(((px % size) + size) % size, ((py % p.H) + p.H) % p.H, size, 4, seed);
                float edge = 1f;
                if (y == h - 1)
                    edge = 1.16f;
                else if (x == 0)
                    edge = 1.07f;
                else if (y == 0)
                    edge = 0.8f;
                else if (x == w - 1)
                    edge = 0.9f;
                if (chisel > 0f && ((px + py) % 3 == 0) && y > 0 && y < h - 1)
                    edge *= 1f - chisel;
                Color c = face * mottle * edge;
                if (chipped)
                {
                    int cx = chipCorner % 2 == 0 ? x : w - 1 - x, cy = chipCorner < 2 ? y : h - 1 - y;
                    if (cx + cy < chipSize)
                        c = face * 0.55f;
                    else if (cx + cy == chipSize)
                        c = face * 1.12f;
                }
                SetWrapped(p, px, py, p.Vary(c, 0.02f));
            }
        }

        // Fiadas de blocos: alturas de fiada (somam a altura da imagem), comprimento de min a max px; a junta é a linha de
        // baixo e a coluna da esquerda de cada bloco (escura, às vezes com geada)
        static void GCourses(Paint p, int[] rows, int minLen, int maxLen, Color face, float toneRange, float chisel,
            float chip, int seed)
        {
            var layout = new System.Random(seed + 11);
            int y0 = 0;
            foreach (int h in rows)
            {
                int start = layout.Next(0, maxLen), x0 = start;
                while (x0 < start + p.W)
                {
                    int len = layout.Next(minLen, maxLen + 1);
                    if (x0 + len > start + p.W)
                        len = start + p.W - x0;
                    float tone = 1f - toneRange * 0.5f + (float)layout.NextDouble() * toneRange;
                    GBlock(p, x0, y0 + 1, len, h - 1, face * tone, chisel, seed, chip);
                    for (int y = 0; y < h; y++)
                        SetWrapped(p, x0, y0 + y, Joint(p, face));
                    x0 += len;
                }
                for (int x = 0; x < p.W; x++)
                    SetWrapped(p, x, y0, Joint(p, face));
                y0 += h;
            }
        }

        static Color Joint(Paint p, Color face) =>
            p.Next < 0.25f ? p.Vary(Color.Lerp(face, k_Frost, 0.55f), 0.03f) : p.Vary(Color.Lerp(k_GothJoint, face, 0.25f), 0.05f);

        // Pintas de geada e de pedra mais escura soltas
        static void GSpeckle(Paint p, int frost, int dark, Color face, int seed)
        {
            var r = new System.Random(seed + 23);
            for (int k = 0; k < frost; k++)
                SetWrapped(p, r.Next(0, p.W), r.Next(0, p.H), Color.Lerp(face, k_Frost, 0.6f));
            for (int k = 0; k < dark; k++)
                SetWrapped(p, r.Next(0, p.W), r.Next(0, p.H), face * 0.72f);
        }

        // ---------- Paredes das casas ----------

        // Cantaria grande (64 px = 4 m): fiadas de 0,6 a 0,75 m, blocos de 1,1 a 2,1 m, face lisa com marcas de cinzel
        // e uma quina lascada aqui e ali
        internal static Paint PaintGothAshlar(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothAshlar);
            GCourses(p, new[] { 12, 10, 11, 10, 11, 10 }, 18, 34, k_GothAshlar, 0.22f, 0.035f, 0.18f, seed);
            GSpeckle(p, 8, 10, k_GothAshlar, seed);
            return p;
        }

        // Cantaria miúda (64 px = 4 m): fiadas de 0,3 a 0,5 m, pedras de 0,4 a 0,9 m, tons mais variados
        internal static Paint PaintGothCoursed(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothCoursed);
            GCourses(p, new[] { 6, 8, 5, 7, 6, 8, 5, 7, 6, 6 }, 6, 14, k_GothCoursed, 0.34f, 0f, 0.1f, seed);
            GSpeckle(p, 10, 14, k_GothCoursed, seed);
            return p;
        }

        // ---------- Tijolos de pedra ----------

        // Tijolo comum (64 px = 4 m): fiadas iguais de 0,5 m, tijolos de 0,8 a 1,2 m em amarração corrida, tons próximos e
        // poucas lascas; a base das casas
        internal static Paint PaintGothBrick(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothBrick);
            GCourses(p, new[] { 8, 8, 8, 8, 8, 8, 8, 8 }, 13, 19, k_GothBrick, 0.16f, 0f, 0.08f, seed);
            GSpeckle(p, 8, 10, k_GothBrick, seed);
            return p;
        }

        // Tijolo alternado (64 px = 4 m): uma fiada alta de 0,6 m e uma baixa de 0,4 m, tijolos de 0,6 a 1,1 m, tom um pouco
        // mais frio; casas
        internal static Paint PaintGothBrickAlt(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothBrickAlt);
            GCourses(p, new[] { 10, 6, 10, 6, 10, 6, 10, 6 }, 10, 18, k_GothBrickAlt, 0.18f, 0f, 0.08f, seed);
            GSpeckle(p, 8, 10, k_GothBrickAlt, seed);
            return p;
        }

        // Tijolo monumental (64 px = 4 m): blocos grandes de 1,4 a 2,1 m em fiadas de 0,6 a 0,75 m, face lisa e mais clara,
        // tons quase iguais, juntas finas e claras (bem assentadas) e quase nenhuma lasca; igrejas, catedral, torres
        internal static Paint PaintGothBrickGrand(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothBrickGrand);
            GCourses(p, new[] { 12, 10, 11, 10, 11, 10 }, 22, 34, k_GothBrickGrand, 0.09f, 0.02f, 0.04f, seed);
            // Juntas mais claras e finas que as das casas
            for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                Color c = p.Pixels[y * p.W + x];
                if (c.r + c.g + c.b < (k_GothBrickGrand.r + k_GothBrickGrand.g + k_GothBrickGrand.b) * 0.62f)
                    p.Pixels[y * p.W + x] = Color.Lerp(c, k_GothBrickGrand * 0.7f, 0.6f);
            }
            GSpeckle(p, 5, 5, k_GothBrickGrand, seed);
            return p;
        }

        // Tijolo gasto (64 px = 4 m): o tijolo comum com mais lascas, tons mais soltos, alguns tijolos bem mais escuros,
        // manchas de umidade e geada nas juntas; ruínas e construções abandonadas
        internal static Paint PaintGothBrickWorn(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothBrickWorn);
            GCourses(p, new[] { 8, 8, 8, 8, 8, 8, 8, 8 }, 12, 19, k_GothBrickWorn, 0.32f, 0f, 0.4f, seed);
            for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                float stain = GFbm(x, y, 64, 3, seed + 41);
                if (stain < 0.38f)
                    p.Pixels[y * p.W + x] *= 0.84f;
            }
            GSpeckle(p, 26, 22, k_GothBrickWorn, seed);
            return p;
        }

        // Pedra bruta argamassada (64 px = 4 m): pedras irregulares (Voronoi que repete) com a argamassa clara entre elas,
        // cada pedra no próprio tom, clara em cima e escura embaixo, e geada na argamassa
        internal static Paint PaintGothRubble(int seed)
        {
            const int size = 64, grid = 6;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothRubbleMortar);
            float cell = size / (float)grid;
            var pts = new Vector2[grid * grid];
            var tones = new float[grid * grid];
            for (int j = 0; j < grid; j++)
            for (int i = 0; i < grid; i++)
            {
                int k = j * grid + i;
                pts[k] = new Vector2((i + 0.2f + GHash(i, j, seed) * 0.6f) * cell,
                    (j + 0.2f + GHash(i, j, seed + 1) * 0.6f) * cell);
                tones[k] = 0.8f + GHash(i, j, seed + 2) * 0.38f;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d1 = 1e9f, d2 = 1e9f;
                int best = 0;
                Vector2 off = Vector2.zero;
                for (int k = 0; k < pts.Length; k++)
                {
                    // Distância com repetição (a pedra do outro lado da imagem conta)
                    float dx = x - pts[k].x, dy = y - pts[k].y;
                    dx -= Mathf.Round(dx / size) * size;
                    dy -= Mathf.Round(dy / size) * size;
                    // Pedras um pouco achatadas, como assentadas em fiadas
                    float d = Mathf.Sqrt(dx * dx + dy * dy * 1.7f);
                    if (d < d1)
                    {
                        d2 = d1;
                        d1 = d;
                        best = k;
                        off = new Vector2(dx, dy);
                    }
                    else if (d < d2)
                        d2 = d;
                }
                float gap = d2 - d1;
                Color c;
                if (gap < 1.6f)
                    c = k_GothRubbleMortar * (0.9f + 0.15f * GFbm(x, y, size, 8, seed + 9));
                else
                {
                    float shade = 1f + Mathf.Clamp(off.y / (cell * 0.45f), -1f, 1f) * 0.12f;
                    if (gap < 2.6f)
                        shade *= off.y > 0f ? 1.15f : 0.72f;
                    c = k_GothRubble * tones[best] * shade * (0.9f + 0.2f * GFbm(x, y, size, 8, seed + best));
                }
                p.Set(x, y, p.Vary(c, 0.025f));
            }
            var r = new System.Random(seed + 4);
            for (int k = 0; k < 60; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size);
                if (p.Pixels[y * size + x].r > k_GothRubble.r * 1.25f)
                    p.Set(x, y, Color.Lerp(k_GothRubbleMortar, k_Frost, 0.7f));
            }
            return p;
        }

        // Reboco gasto (128 px = 8 m, para a parede inteira de uma casa não repetir os buracos): reboco cinza-lavanda liso com manchas grandes, escorridos de chuva e rachaduras
        // finas; onde ele caiu (três manchas de ruído) aparece a cantaria miúda por baixo, com a beira do reboco quebrado
        // clara em cima e com sombra embaixo
        internal static Paint PaintGothPlaster(int seed)
        {
            const int size = 128;
            Paint under = PaintGothCoursed(seed + 50);
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothPlaster);
            var r = new System.Random(seed + 3);
            var streak = new float[size];
            for (int k = 0; k < 14; k++)
            {
                int x0 = r.Next(0, size), w = r.Next(1, 3);
                float s = 0.03f + (float)r.NextDouble() * 0.07f;
                for (int dx = 0; dx < w; dx++)
                    streak[(x0 + dx) % size] += s;
            }
            var holes = new bool[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = GFbm(x, y, size, 6, seed + 17, 3);
                holes[y * size + x] = n > 0.64f;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (holes[i])
                {
                    p.Set(x, y, under.Pixels[(y % under.H) * under.W + x % under.W] * 0.92f);
                    continue;
                }
                float tone = 0.9f + 0.18f * GFbm(x, y, size, 3, seed + 3, 2) - streak[x] * (0.6f + 0.4f * GValue(x * 0.125f, y * 0.25f, 16, seed));
                // Beira do buraco: o reboco tem espessura (clara se o buraco está embaixo, sombra se está em cima)
                bool holeBelow = holes[((y - 1 + size) % size) * size + x], holeAbove = holes[((y + 1) % size) * size + x];
                bool holeSide = holes[y * size + (x + 1) % size] || holes[y * size + (x - 1 + size) % size];
                if (holeBelow)
                    tone *= 1.18f;
                else if (holeAbove || holeSide)
                    tone *= 0.78f;
                p.Set(x, y, p.Vary(k_GothPlaster * tone, 0.018f));
            }
            // Rachaduras finas que descem tortas
            for (int k = 0; k < 5; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size), len = r.Next(6, 14);
                for (int s = 0; s < len; s++)
                {
                    if (!holes[((y % size + size) % size) * size + ((x % size) + size) % size])
                        SetWrapped(p, x, y, k_GothPlaster * 0.62f);
                    y--;
                    if (r.NextDouble() < 0.4)
                        x += r.NextDouble() < 0.5 ? -1 : 1;
                }
            }
            GSpeckle(p, 24, 10, k_GothPlaster, seed);
            return p;
        }

        // ---------- Partes das peças ----------

        // Moldura talhada (64 px = 4 m): pedra clara e lisa em blocos compridos de 1,8 a 3,5 m e fiadas de 1 m, marcas de
        // cinzel bem leves e poucas lascas: as cornijas, frisos, arcos, pináculos e marcos de janela
        internal static Paint PaintGothMolding(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_GothMolding);
            GCourses(p, new[] { 16, 16, 16, 16 }, 28, 56, k_GothMolding, 0.1f, 0.02f, 0.12f, seed);
            GSpeckle(p, 6, 6, k_GothMolding, seed);
            return p;
        }

        // Coluna polida (64 px = 4 m): pedra escura quase lisa em tambores de 1 m, com veios claros finos em diagonal
        // (como ardósia ou mármore escuro), brilho largo e suave; as colunas, postes, colunas-lanterna e pilastras
        internal static Paint PaintGothColumn(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothColumn);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float m = 0.92f + 0.16f * GFbm(x, y, size, 2, seed, 2);
                // Veios: linhas onde um ruído deslocado na diagonal cruza um nível
                float v = GFbm(x, y, size, 2, seed + 40, 3) * 1.1f + (x + 2 * y) / (float)size;
                float frac = v * 2f - Mathf.Floor(v * 2f);
                Color c = k_GothColumn * m;
                if (frac < 0.025f)
                    c = Color.Lerp(c, new Color(0.62f, 0.62f, 0.7f), 0.5f);
                else if (frac < 0.06f)
                    c = Color.Lerp(c, new Color(0.45f, 0.45f, 0.53f), 0.22f);
                // Junta entre tambores, a cada metro
                if (y % 16 == 0)
                    c = p.Next < 0.3f ? Color.Lerp(c, k_Frost, 0.5f) : k_GothJoint;
                else if (y % 16 == 15)
                    c *= 1.1f;
                p.Set(x, y, p.Vary(c, 0.015f));
            }
            return p;
        }

        // Pedra fina das esculturas (64 px = 4 m): clara, quase sem juntas, manchas suaves, poros e marcas de cinzel muito
        // leves; estátuas, guardiões, monumentos e relevos
        internal static Paint PaintGothCarving(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothCarving);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float m = 0.88f + 0.2f * GFbm(x, y, size, 4, seed, 3);
                float pores = GHash(x, y, seed + 3) < 0.03f ? 0.8f : 1f;
                p.Set(x, y, p.Vary(k_GothCarving * m * pores, 0.012f));
            }
            // Duas emendas de bloco quase invisíveis
            for (int x = 0; x < size; x++)
            {
                SetWrapped(p, x, 21, p.Pixels[21 * size + x] * 0.86f);
                SetWrapped(p, x, 53, p.Pixels[53 * size + x] * 0.86f);
            }
            GSpeckle(p, 6, 0, k_GothCarving, seed);
            return p;
        }

        // Soco (64 px = 4 m): blocos grandes e escuros, ásperos e esburacados, mais gastos e com mais geada (o pé das
        // paredes, das muralhas e dos pedestais; a sujeira que sobe do chão fica no shader, pela altura)
        internal static Paint PaintGothPlinth(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothPlinth);
            GCourses(p, new[] { 16, 16, 16, 16 }, 22, 40, k_GothPlinth, 0.2f, 0f, 0.35f, seed);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (GHash(x, y, seed + 8) < 0.025f)
                {
                    // Pequenos buracos com a beira de baixo clara (sombra em cima)
                    SetWrapped(p, x, y, p.Pixels[y * size + x] * 0.6f);
                    SetWrapped(p, x, y - 1, p.Pixels[((y - 1 + size) % size) * size + x] * 1.08f);
                }
            }
            GSpeckle(p, 20, 6, k_GothPlinth, seed);
            return p;
        }

        // Pedra dos móveis (32 px = 2 m): clara e gasta pelo uso, grão miúdo de pintas, manchas mais lisas e claras
        // (polidas pelas mãos) e poucas juntas; bancos, mesas, bancadas, atris, balcões e lixeiras
        internal static Paint PaintGothFurniture(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothFurniture);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float worn = GFbm(x, y, size, 2, seed + 1, 2);
                float m = 0.9f + 0.12f * GFbm(x, y, size, 4, seed, 2) + (worn > 0.62f ? 0.08f : 0f);
                float grain = GHash(x, y, seed + 2);
                if (worn <= 0.62f)
                    m *= grain < 0.12f ? 0.84f : grain > 0.94f ? 1.1f : 1f;
                p.Set(x, y, p.Vary(k_GothFurniture * m, 0.01f));
            }
            for (int x = 0; x < size; x++)
                SetWrapped(p, x, 0, k_GothFurniture * 0.7f);
            GSpeckle(p, 8, 0, k_GothFurniture, seed);
            return p;
        }

        // Escamas do telhado (64 px = 4 m), num dos três tons: escamas de 8 px em fiadas desencontradas, cada uma no
        // próprio tom, com a ponta redonda clara, a sombra da fiada de cima, uma ou outra faltando e geada nas pontas
        internal static Paint PaintGothScales(int seed, Color stone)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, stone * 0.42f);
            var r = new System.Random(seed + 3);
            for (int row = 0; row < size / 8; row++)
            {
                int offset = row % 2 == 0 ? 0 : 4;
                for (int t = 0; t < size / 8; t++)
                {
                    float tone = 0.8f + (float)r.NextDouble() * 0.36f;
                    bool missing = r.NextDouble() < 0.008;
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        // y = 0 é a ponta de baixo, redonda; em cima a escama some sob a fiada de cima
                        float dx = (x - 3.5f) / 4f, dy = (7f - y) / 7f;
                        if (y < 4 && dx * dx + (1f - y / 4f) * (1f - y / 4f) > 1.05f)
                            continue;
                        Color c = missing ? stone * 0.55f
                            : stone * tone * (y > 5 ? 0.72f : y < 2 ? 1.18f : 1f) * (1.05f - Mathf.Abs(dx) * 0.22f);
                        if (!missing && y == 0 && r.NextDouble() < 0.3)
                            c = Color.Lerp(c, k_Frost, 0.6f);
                        SetWrapped(p, t * 8 + x + offset, row * 8 + y, p.Vary(c * (dy > 0.9f ? 0.85f : 1f), 0.025f));
                    }
                }
            }
            GSpeckle(p, 15, 0, stone, seed);
            return p;
        }

        // ---------- Ligas (as placas escuras de sempre ficam em PicosTechTextures.PaintAlloy) ----------

        internal static readonly Color k_GothSteel = new Color(0.5f, 0.52f, 0.58f);
        internal static readonly Color k_GothEngraved = new Color(0.19f, 0.2f, 0.25f);

        // Aço claro escovado (32 px = 2 m): riscos finos ao comprido (na horizontal da imagem), faixas mais claras e mais
        // escuras que repetem sem costura, poucos riscos de uso fundos e geada miúda; anéis, garras, cintas e detalhes
        internal static Paint PaintGothSteel(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_GothSteel);
            var rows = new float[size];
            for (int y = 0; y < size; y++)
                rows[y] = 0.94f + 0.12f * GHash(0, y, seed) + 0.06f * Mathf.Sin(y / (float)size * Mathf.PI * 4f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float streak = 0.97f + 0.06f * GValue(x * 0.125f, y, size, seed + 2);
                p.Set(x, y, p.Vary(k_GothSteel * rows[y] * streak, 0.012f));
            }
            var r = new System.Random(seed + 5);
            for (int k = 0; k < 3; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size), len = r.Next(4, 9);
                for (int j = 0; j < len; j++)
                    SetWrapped(p, x + j, y + (j > len / 2 && k % 2 == 0 ? 1 : 0), k_GothSteel * 0.72f);
            }
            GSpeckle(p, 6, 0, k_GothSteel, seed);
            return p;
        }

        // Dentro de um arco ogival de lados em x l..r, pé em 'bottom' e nascença em 'spring' (cada lado do alto é um arco de
        // círculo com o centro do lado oposto, raio 'rad')
        static bool InLancet(float px, float py, float l, float r, float bottom, float spring, float rad)
        {
            if (px < l || px > r || py < bottom)
                return false;
            if (py <= spring)
                return true;
            float dl = (px - (l + rad)) * (px - (l + rad)) + (py - spring) * (py - spring);
            float dr = (px - (r - rad)) * (px - (r - rad)) + (py - spring) * (py - spring);
            return dl <= rad * rad && dr <= rad * rad;
        }

        // Contorno de 1 px de um arco ogival (dentro dele e fora do arco 1 px menor)
        static bool LancetLine(float px, float py, float l, float r, float bottom, float spring, float rad) =>
            InLancet(px, py, l, r, bottom, spring, rad) && !InLancet(px, py, l + 1f, r - 1f, bottom + 1f, spring, rad - 1f);

        // Liga lavrada (64 px = 4 m): placas escuras de 2 x 4 m com a beira chanfrada e, gravado em cada uma (sulco escuro
        // com a beira de baixo pegando luz), o rendilhado de uma janela gótica: arco ogival grande, dois arcos menores
        // dentro e uma rosácea de quatro lóbulos entre eles; parafusos nos cantos. As faces grandes de metal (pedestais,
        // caixas, agulhas)
        internal static Paint PaintGothEngraved(int seed)
        {
            const int size = 64, pw = 32;
            Color b = k_GothEngraved;
            Paint p = GridTextureKit.Tile(size, size, seed, b);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = x % pw;
                float m = 0.94f + 0.1f * GFbm(x, y, size, 4, seed, 2);
                Color c = b * m;
                if (i == 0 || y == 0)
                    c = b * 0.55f;
                else if (y == size - 1)
                    c = Color.Lerp(b * 1.5f, k_Frost, 0.12f);
                else if (i == 1)
                    c = b * 1.25f;
                else if (i == pw - 1 || y == 1)
                    c = b * 0.78f;
                p.Set(x, y, p.Vary(c, 0.015f));
            }
            var groove = new bool[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x % pw) + 0.5f, py = y + 0.5f;
                bool line = LancetLine(px, py, 5f, 27f, 5f, 38f, 18.7f)
                            || LancetLine(px, py, 8f, 15.5f, 8f, 32f, 6.4f)
                            || LancetLine(px, py, 16.5f, 24f, 8f, 32f, 6.4f);
                // Rosácea: círculo e quatro lóbulos em volta de (16; 47)
                float d = Mathf.Sqrt((px - 16f) * (px - 16f) + (py - 47f) * (py - 47f));
                if (Mathf.Abs(d - 5.2f) < 0.5f)
                    line = true;
                foreach ((float ox, float oy) in new[] { (0f, 2.2f), (0f, -2.2f), (2.2f, 0f), (-2.2f, 0f) })
                {
                    float dd = Mathf.Sqrt((px - 16f - ox) * (px - 16f - ox) + (py - 47f - oy) * (py - 47f - oy));
                    if (Mathf.Abs(dd - 1.9f) < 0.45f && d > 1.2f)
                        line = true;
                }
                groove[y * size + x] = line;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!groove[y * size + x])
                    continue;
                p.Set(x, y, b * 0.45f);
                if (y > 0 && !groove[(y - 1) * size + x])
                    p.Set(x, y - 1, b * 1.35f);
            }
            foreach ((int bx, int by) in new[] { (3, 3), (28, 3), (3, 60), (28, 60), (35, 3), (60, 3), (35, 60), (60, 60) })
            {
                p.Set(bx, by, b * 1.6f);
                p.Set(bx + 1, by - 1, b * 0.6f);
            }
            GSpeckle(p, 8, 0, b, seed);
            return p;
        }

        // ---------- Estragos do ataque (kit AR_Ataque, pedido do usuário em 2026-11-08) ----------

        internal static readonly Color k_CharWood = new Color(0.09f, 0.075f, 0.07f);
        internal static readonly Color k_BurntStone = new Color(0.14f, 0.14f, 0.16f);
        internal static readonly Color k_Scorch = new Color(0.085f, 0.08f, 0.085f);
        internal static readonly Color k_FusedGlass = new Color(0.2f, 0.11f, 0.3f);
        internal static readonly Color k_DeadCrystal = new Color(0.25f, 0.22f, 0.31f);
        internal static readonly Color k_Carbon = new Color(0.055f, 0.05f, 0.05f);
        static readonly Color k_Ash = new Color(0.34f, 0.33f, 0.33f);

        // Madeira carbonizada (32 px = 2 m, a viga ao comprido em X): veios escuros ao longo e a crosta gretada em
        // blocos (greta quase preta, a beira de cima de cada bloco um pouco mais clara) e pintas de cinza
        internal static Paint PaintCharWood(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_CharWood);
            var r = new System.Random(seed + 1);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = GFbm(x / 4, y, size, 8, seed + 2, 2);
                p.Set(x, y, p.Vary(k_CharWood * (0.85f + 0.3f * grain), 0.04f));
            }
            for (int y = 0; y < size; y += 4)
            {
                int x = r.Next(0, 5);
                while (x < size + 5)
                {
                    int len = r.Next(3, 6);
                    for (int i = 0; i < len; i++)
                        SetWrapped(p, x + i, y + 3, k_CharWood * 1.4f);
                    for (int yy = 0; yy < 4; yy++)
                        SetWrapped(p, x, y + yy, k_CharWood * 0.35f);
                    x += len;
                }
                for (int x2 = 0; x2 < size; x2++)
                    if (GHash(x2, y, seed + 3) < 0.8f)
                        SetWrapped(p, x2, y, k_CharWood * 0.35f);
            }
            for (int k = 0; k < 14; k++)
                SetWrapped(p, r.Next(0, size), r.Next(0, size), k_Ash);
            return p;
        }

        // Pedra queimada (64 px = 4 m): os tijolos de pedra enegrecidos (as quebras e o interior queimado das casas),
        // com manchas de fuligem mais funda, a geada das juntas virando cinza e lascas
        internal static Paint PaintBurntStone(int seed)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_BurntStone);
            GCourses(p, new[] { 8, 8, 8, 8, 8, 8, 8, 8 }, 12, 19, k_BurntStone, 0.3f, 0f, 0.3f, seed);
            for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                Color c = p.Pixels[y * p.W + x];
                if (c.r + c.g + c.b > (k_BurntStone.r + k_BurntStone.g + k_BurntStone.b) * 1.6f)
                    c = k_Ash * (0.5f + 0.15f * GHash(x, y, seed + 8));
                float soot = GFbm(x, y, 64, 3, seed + 9);
                c *= soot < 0.42f ? 0.62f : soot < 0.5f ? 0.8f : 1f;
                p.Pixels[y * p.W + x] = new Color(c.r, c.g, c.b, 1f);
            }
            GSpeckle(p, 0, 30, k_BurntStone, seed);
            return p;
        }

        // Chão queimado (64 px = 4 m): cinza escura e fuligem em manchas de quatro tons, riscos de cinza mais clara e
        // pintas de carvão
        internal static Paint PaintScorch(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Scorch);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = GFbm(x, y, size, 4, seed);
                float m = n < 0.4f ? 0.7f : n < 0.56f ? 1f : n < 0.68f ? 1.4f : 1.9f;
                p.Set(x, y, p.Vary(k_Scorch * m, 0.06f));
            }
            var r = new System.Random(seed + 4);
            for (int k = 0; k < 10; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size), len = r.Next(3, 8), dx = r.Next(0, 2) == 0 ? 1 : -1;
                for (int i = 0; i < len; i++)
                    SetWrapped(p, x + i * dx, (y + i / 2) % size, k_Ash * 0.6f);
            }
            for (int k = 0; k < 40; k++)
                SetWrapped(p, r.Next(0, size), r.Next(0, size), k_Carbon * 0.6f);
            for (int k = 0; k < 18; k++)
                SetWrapped(p, r.Next(0, size), r.Next(0, size), k_Ash * 0.8f);
            return p;
        }

        // Vidro lilás fundido (32 px = 2 m): faixas escuras torcidas como vidro que escorreu, brilho claro nas cristas e
        // bolhas presas
        internal static Paint PaintFusedGlass(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_FusedGlass);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float swirl = GFbm(x, y, size, 3, seed, 2);
                float band = swirl * 5f + x / (float)size;
                band -= (int)band;
                Color c = band < 0.15f ? k_FusedGlass * 1.8f : band < 0.5f ? k_FusedGlass : k_FusedGlass * 0.7f;
                p.Set(x, y, p.Vary(c, 0.04f));
            }
            var r = new System.Random(seed + 5);
            for (int k = 0; k < 9; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size - 1);
                SetWrapped(p, x, y, k_FusedGlass * 0.45f);
                SetWrapped(p, x, y + 1, k_FusedGlass * 2.2f);
            }
            return p;
        }

        // Cristal apagado (32 px = 2 m): faces em faixas diagonais claras e escuras (as facetas), sem luz, com
        // rachaduras finas e claras
        internal static Paint PaintDeadCrystal(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_DeadCrystal);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int facet = ((x + y) / 8) % 4;
                float m = facet == 0 ? 1.25f : facet == 1 ? 1f : facet == 2 ? 0.8f : 0.92f;
                p.Set(x, y, p.Vary(k_DeadCrystal * m * (0.92f + 0.16f * GFbm(x, y, size, 4, seed, 2)), 0.03f));
            }
            var r = new System.Random(seed + 6);
            for (int k = 0; k < 4; k++)
            {
                int x = r.Next(0, size), y = r.Next(0, size), len = r.Next(4, 9);
                for (int i = 0; i < len; i++)
                {
                    SetWrapped(p, x, (y + i) % size, k_DeadCrystal * 1.7f);
                    if (r.Next(0, 3) == 0)
                        x += r.Next(0, 2) == 0 ? 1 : -1;
                }
            }
            return p;
        }

        // Carbonizado (32 px = 2 m): crosta quase preta gretada em placas, a greta um pouco mais clara, e cinza solta
        internal static Paint PaintCarbon(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Carbon);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float crack = Mathf.Abs(GFbm(x, y, size, 4, seed, 1) - 0.5f);
                Color c = crack < 0.03f ? k_Carbon * 2.4f : k_Carbon * (0.85f + 0.4f * GFbm(x, y, size, 8, seed + 1, 2));
                p.Set(x, y, p.Vary(c, 0.05f));
            }
            var r = new System.Random(seed + 7);
            for (int k = 0; k < 10; k++)
                SetWrapped(p, r.Next(0, size), r.Next(0, size), k_Ash * 0.7f);
            return p;
        }
    }
}
