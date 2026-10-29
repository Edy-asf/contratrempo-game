using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas do bairro voador (sala 20), em pixel art de 16 px/m como as outras: reboco em quatro cores desbotadas
    // (manchas de chuva, rachaduras e pedaços onde o reboco caiu e a pedra aparece), tijolo, enxaimel, pedra escura
    // lavrada, telha em três tons, calçamento de pedra miúda das ruas (três variações e cinco passos de transição até a
    // neve) e vitral de chumbo com um brilho fraco
    public static partial class PicosTextureKit
    {
        static readonly Color[] k_PlasterColors =
        {
            new Color(0.62f, 0.52f, 0.36f),     // ocre
            new Color(0.7f, 0.68f, 0.62f),      // cal
            new Color(0.45f, 0.52f, 0.6f),      // azul desbotado
            new Color(0.6f, 0.44f, 0.4f),       // terracota rosada
        };
        static readonly Color[] k_RoofColors =
        {
            new Color(0.55f, 0.27f, 0.2f), new Color(0.47f, 0.31f, 0.24f), new Color(0.4f, 0.25f, 0.25f),
        };
        static readonly Color k_Brick = new Color(0.46f, 0.23f, 0.18f);
        static readonly Color k_Mortar = new Color(0.56f, 0.54f, 0.52f);
        static readonly Color k_Timber = new Color(0.22f, 0.16f, 0.12f);
        static readonly Color k_DarkStone = new Color(0.27f, 0.28f, 0.33f);
        static readonly Color k_Cobble = new Color(0.44f, 0.43f, 0.43f);

        static void BuildVillage(Set set)
        {
            set.HouseWalls = new Material[7];
            for (int i = 0; i < 4; i++)
                set.HouseWalls[i] = GridTextureKit.Surface($"Picos_Reboco_{i + 1}", PaintPlaster(1401 + i, k_PlasterColors[i]), Color.black);
            set.HouseWalls[4] = GridTextureKit.Surface("Picos_Tijolo", PaintBrick(1411), Color.black);
            set.HouseWalls[5] = GridTextureKit.Surface("Picos_Enxaimel", PaintTimber(1412), Color.black);
            set.HouseWalls[6] = GridTextureKit.Surface("Picos_Pedra_Escura", PaintDarkStone(1413), Color.black);
            set.Roofs = new Material[k_RoofColors.Length];
            for (int i = 0; i < k_RoofColors.Length; i++)
                set.Roofs[i] = GridTextureKit.Surface($"Picos_Telha_{i + 1}", PaintRoofTiles(1421 + i, k_RoofColors[i]), Color.black);
            set.Cobble = new Material[3];
            for (int i = 0; i < 3; i++)
                set.Cobble[i] = GridTextureKit.Surface($"Picos_Rua_{i + 1}", PaintCobble(1431 + i), Color.black);
            set.CobbleSnow = new Material[5];
            for (int k = 0; k < 5; k++)
                set.CobbleSnow[k] = GridTextureKit.Blend($"Picos_Rua_Neve_{k + 1}", set.Cobble[0], set.FloorSnow, (k + 1) / 6f, 1440);
            set.Glass = GridTextureKit.Surface("Picos_Vitral", PaintGlass(1450), new Color(0.42f, 0.36f, 0.95f) * 0.9f);
            set.Scales = GridTextureKit.Surface("Picos_Escamas", PaintScales(1460), Color.black);
            set.Cloth = GridTextureKit.Surface("Picos_Tecido", PaintCloth(1470, new Color(0.3f, 0.12f, 0.26f)), Color.black);
            set.ClothLight = GridTextureKit.Surface("Picos_Tecido_Claro", PaintCloth(1471, new Color(0.62f, 0.58f, 0.5f)), Color.black);
        }

        // Tecido velho e duro de gelo (64 px = 4 m): trama miúda, fios mais claros e mais escuros ao comprido, manchas
        // desbotadas e cristais de geada
        static Paint PaintCloth(int seed, Color cloth)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, cloth);
            var layout = new System.Random(seed + 7);
            float[] thread = new float[size];
            for (int x = 0; x < size; x++)
                thread[x] = 0.9f + (float)layout.NextDouble() * 0.18f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float weave = (x + y) % 2 == 0 ? 1.04f : 0.96f;
                float fade = 0.85f + 0.3f * Seamless(x, y, size, seed * 0.37f);
                p.Set(x, y, p.Vary(cloth * weave * thread[x] * fade, 0.02f));
            }
            for (int k = 0; k < 70; k++)
                SetWrapped(p, layout.Next(0, size), layout.Next(0, size), Color.Lerp(cloth, k_Frost, 0.55f));
            for (int k = 0; k < 6; k++)
            {
                int cx = layout.Next(0, size), cy = layout.Next(0, size);
                for (int i = -1; i <= 1; i++)
                {
                    SetWrapped(p, cx + i, cy, Color.Lerp(cloth, k_Frost, 0.8f));
                    SetWrapped(p, cx, cy + i, Color.Lerp(cloth, k_Frost, 0.8f));
                }
            }
            return p;
        }

        // Pixel com a imagem repetindo nos dois eixos
        static void SetWrapped(Paint p, int x, int y, Color c, bool glow = false) =>
            p.Set(((x % p.W) + p.W) % p.W, ((y % p.H) + p.H) % p.H, c, glow);

        // Ruído suave que repete na imagem (soma de senos com períodos inteiros)
        static float Seamless(int x, int y, int size, float seed)
        {
            float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
            return 0.5f + 0.22f * Mathf.Sin(u + v * 2f + seed) + 0.16f * Mathf.Sin(u * 3f - v + seed * 1.7f) + 0.12f * Mathf.Sin(v * 3f + seed * 2.3f);
        }

        // Reboco (64 px = 4 m): manchas grandes e suaves, escorridos verticais de chuva, rachaduras, geada e dois pedaços
        // onde o reboco caiu e aparece a pedra em fiadas, com a beira do reboco quebrado mais escura
        static Paint PaintPlaster(int seed, Color color)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, color);
            var layout = new System.Random(seed + 3);
            var streaks = new float[size];
            for (int k = 0; k < 7; k++)
            {
                int x0 = layout.Next(0, size), width = layout.Next(1, 4);
                float strength = 0.04f + (float)layout.NextDouble() * 0.08f;
                for (int dx = 0; dx < width; dx++)
                    streaks[(x0 + dx) % size] += strength;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float tone = 0.88f + Seamless(x, y, size, seed) * 0.22f - streaks[x] * (0.7f + 0.3f * Mathf.Sin(y / (float)size * Mathf.PI * 2f));
                p.Set(x, y, p.Vary(color * tone, 0.025f));
            }

            // Pedaços sem reboco: pedra em fiadas de 4 px, blocos de 6 a 9 px
            for (int patch = 0; patch < 2; patch++)
            {
                int cx = layout.Next(0, size), cy = layout.Next(0, size);
                float radius = 6f + (float)layout.NextDouble() * 6f;
                for (int dy = -14; dy <= 14; dy++)
                for (int dx = -16; dx <= 16; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx * 0.7f + dy * dy) + Mathf.Sin(dx * 1.3f + dy * 0.7f + patch) * 1.6f;
                    if (d > radius + 1f)
                        continue;
                    int x = cx + dx, y = cy + dy;
                    if (d > radius)
                    {
                        SetWrapped(p, x, y, color * 0.72f);
                        continue;
                    }
                    int row = ((y % size) + size) % size / 4, offset = row % 2 == 0 ? 0 : 4;
                    bool joint = ((y % 4) + 4) % 4 == 0 || ((x + offset) % 8 + 8) % 8 == 0;
                    Color stone = k_Stone * (0.78f + ((row * 7 + (x + offset) / 8) % 5) * 0.05f);
                    SetWrapped(p, x, y, p.Vary(joint ? k_StoneJoint : stone, 0.04f));
                }
            }

            // Rachaduras finas e geada
            for (int k = 0; k < 4; k++)
            {
                int x = layout.Next(0, size), y = layout.Next(0, size);
                for (int i = 0; i < layout.Next(6, 16); i++)
                {
                    SetWrapped(p, x, y, color * 0.55f);
                    x += layout.Next(-1, 2);
                    y += 1;
                }
            }
            for (int k = 0; k < 30; k++)
                SetWrapped(p, layout.Next(0, size), layout.Next(0, size), Color.Lerp(color, k_Frost, 0.6f));
            return p;
        }

        // Tijolo (64 px): tijolos de 8 x 4 px em fiadas desencontradas, uns mais escuros (queimados), argamassa clara
        // com geada
        static Paint PaintBrick(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Mortar);
            var layout = new System.Random(seed + 1);
            for (int row = 0; row < size / 4; row++)
            {
                int offset = row % 2 == 0 ? 0 : 4;
                for (int b = 0; b < size / 8; b++)
                {
                    float tone = 0.82f + (float)layout.NextDouble() * 0.3f;
                    if (layout.NextDouble() < 0.12)
                        tone *= 0.7f;
                    for (int y = 1; y < 4; y++)
                    for (int x = 1; x < 8; x++)
                    {
                        Color c = k_Brick * tone * (y == 1 ? 1.1f : y == 3 ? 0.9f : 1f);
                        SetWrapped(p, b * 8 + x + offset, row * 4 + y, p.Vary(c, 0.04f));
                    }
                }
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (y % 4 == 0 || (x + (y / 4 % 2 == 0 ? 0 : 4)) % 8 == 0)
                    p.Set(x, y, p.Next < 0.3f ? k_Frost * 0.9f : p.Vary(k_Mortar, 0.05f));
            return p;
        }

        // Enxaimel (64 px): reboco claro entre vigas escuras de madeira (postes a cada 2 m, travessas em cima e no meio,
        // escoras em diagonal em painéis alternados), com veios e manchas no reboco
        static Paint PaintTimber(int seed)
        {
            const int size = 64;
            Color plaster = new Color(0.72f, 0.68f, 0.58f);
            Paint p = GridTextureKit.Tile(size, size, seed, plaster);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                p.Set(x, y, p.Vary(plaster * (0.9f + Seamless(x, y, size, seed) * 0.15f), 0.03f));

            void Beam(int x, int y)
            {
                Color c = k_Timber * (0.85f + ((x * 3 + y) % 7) * 0.04f);
                SetWrapped(p, x, y, p.Vary(c, 0.05f));
            }
            for (int y = 0; y < size; y++)
            for (int k = 0; k < 2; k++)
            for (int dx = 0; dx < 3; dx++)
                Beam(k * 32 + dx, y);
            for (int x = 0; x < size; x++)
            for (int k = 0; k < 2; k++)
            for (int dy = 0; dy < 3; dy++)
                Beam(x, k * 32 + dy);
            for (int panel = 0; panel < 4; panel += 3)
            {
                int px = panel % 2 * 32, py = panel / 2 * 32;
                for (int t = 3; t < 30; t++)
                for (int w = 0; w < 3; w++)
                    Beam(px + t + w, py + (panel == 0 ? t : 32 - t));
            }
            return p;
        }

        // Pedra escura lavrada (64 px): blocos de 0,5 m de altura e comprimentos variados, beira de cima clara, juntas
        // escuras com geada
        static Paint PaintDarkStone(int seed)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, k_DarkStone);
            var layout = new System.Random(seed + 9);
            for (int row = 0; row < size / 8; row++)
            {
                int start = layout.Next(0, 12), x0 = start;
                while (x0 < start + size)
                {
                    int length = layout.Next(10, 22);
                    float tone = 0.85f + (float)layout.NextDouble() * 0.25f;
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < length; x++)
                    {
                        bool joint = y == 0 || x == 0;
                        Color c = joint ? (p.Next < 0.3f ? k_Frost * 0.8f : k_DarkStone * 0.45f) : k_DarkStone * tone * (y == 1 ? 1.15f : y == 7 ? 0.88f : 1f);
                        SetWrapped(p, x0 + x, row * 8 + y, p.Vary(c, 0.035f));
                    }
                    x0 += length;
                }
            }
            return p;
        }

        // Telha (64 px = 4 m): fiadas de 8 px, telhas de 8 px desencontradas, arredondadas (meio claro, beiras escuras),
        // a sombra da fiada de cima, uma ou outra quebrada e geada
        static Paint PaintRoofTiles(int seed, Color color)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, seed, color * 0.5f);
            var layout = new System.Random(seed + 5);
            for (int row = 0; row < size / 8; row++)
            {
                int offset = row % 2 == 0 ? 0 : 4;
                for (int t = 0; t < size / 8; t++)
                {
                    float tone = 0.86f + (float)layout.NextDouble() * 0.24f;
                    bool broken = layout.NextDouble() < 0.03;
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        float across = Mathf.Abs(x - 3.5f) / 3.5f;
                        Color c = broken ? color * 0.35f : color * tone * (1.12f - across * across * 0.4f) * (y < 2 ? 0.7f : 1f - (y - 2) * 0.02f);
                        if (x == 0 && !broken)
                            c = color * 0.5f;
                        SetWrapped(p, t * 8 + x + offset, row * 8 + y, p.Vary(c, 0.03f));
                    }
                }
            }
            for (int k = 0; k < 40; k++)
                SetWrapped(p, layout.Next(0, size), layout.Next(0, size), Color.Lerp(color, k_Frost, 0.7f));
            return p;
        }

        // Pedra miúda das ruas (128 px = 8 m, o tamanho da neve do chão para a transição): pedras arredondadas de 4 a 7
        // px numa grade torta, em tons cinza e castanho, com a argamassa escura e geada nas juntas
        static Paint PaintCobble(int seed)
        {
            const int size = 128, cell = 7;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Cobble * 0.45f);
            var layout = new System.Random(seed + 2);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (p.Next < 0.18f)
                    p.Set(x, y, Color.Lerp(k_Cobble * 0.45f, k_Frost, 0.5f));
            int cells = size / cell;
            for (int gy = 0; gy < cells + 1; gy++)
            for (int gx = 0; gx < cells + 1; gx++)
            {
                float cx = gx * (size / (float)cells) + (float)layout.NextDouble() * 2.4f - 1.2f;
                float cy = gy * (size / (float)cells) + (float)layout.NextDouble() * 2.4f - 1.2f;
                float radius = 2.6f + (float)layout.NextDouble() * 0.9f;
                float tone = 0.8f + (float)layout.NextDouble() * 0.35f;
                Color stone = Color.Lerp(k_Cobble, new Color(0.47f, 0.4f, 0.34f), (float)layout.NextDouble() * 0.6f) * tone;
                int r = Mathf.CeilToInt(radius);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + (float)layout.NextDouble() * 0.8f;
                    if (d > radius)
                        continue;
                    Color c = dx + dy < -radius * 0.6f ? stone * 1.15f : dx + dy > radius * 0.7f ? stone * 0.78f : stone;
                    SetWrapped(p, Mathf.RoundToInt(cx) + dx, Mathf.RoundToInt(cy) + dy, p.Vary(c, 0.04f));
                }
            }
            return p;
        }

        // Escamas de pedra (64 px = 4 m): fiadas de 6 px de placas arredondadas de 8 px, desencontradas, cinza-azuladas
        // com a beira de baixo clara e a sombra da fiada de cima; geada em algumas
        static Paint PaintScales(int seed)
        {
            const int size = 64;
            Color stone = new Color(0.34f, 0.35f, 0.4f);
            Paint p = GridTextureKit.Tile(size, size, seed, stone * 0.45f);
            var layout = new System.Random(seed + 3);
            for (int row = 0; row < size / 8; row++)
            {
                int offset = row % 2 == 0 ? 0 : 4;
                for (int t = 0; t < size / 8; t++)
                {
                    float tone = 0.85f + (float)layout.NextDouble() * 0.25f;
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        float dx = (x - 3.5f) / 4f, dy = (y - 1f) / 7f;
                        if (dx * dx + dy * dy * 0.6f > 1f && y > 4)
                            continue;
                        Color c = stone * tone * (y < 2 ? 0.75f : y > 5 ? 1.15f : 1f) * (1.05f - Mathf.Abs(dx) * 0.2f);
                        SetWrapped(p, t * 8 + x + offset, row * 8 + y, p.Vary(c, 0.03f));
                    }
                }
            }
            for (int k = 0; k < 50; k++)
                SetWrapped(p, layout.Next(0, size), layout.Next(0, size), Color.Lerp(stone, k_Frost, 0.7f));
            return p;
        }

        // Vitral (32 px = 2 m): losangos de vidro violeta-azulado entre fios de chumbo; o vidro brilha fraco
        static Paint PaintGlass(int seed)
        {
            const int size = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, new Color(0.04f, 0.03f, 0.06f));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool lead = (x + y) % 8 == 0 || (x - y + 64) % 8 == 0;
                if (lead)
                    continue;
                int pane = ((x + y) / 8 * 3 + (x - y + 64) / 8) % 4;
                Color glass = Color.Lerp(new Color(0.22f, 0.18f, 0.5f), new Color(0.2f, 0.45f, 0.6f), pane / 3f);
                p.Set(x, y, p.Vary(glass, 0.08f), true);
            }
            return p;
        }
    }
}
