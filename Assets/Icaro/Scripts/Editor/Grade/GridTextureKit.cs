using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Texturas e sprites em pixel art do mapa quadriculado, gerados por código (provisórios, até a arte final) e
    // salvos como PNG em Art/Grade/Texturas, com filtro Point e sem compressão. Escala: 16 px por metro (um bloco de
    // 2 m = 32 px; um bloco de 6 m de altura = 96 px). O mapa de emissão é branco onde brilha e a cor do brilho vem do
    // material, então o mesmo desenho serve para cores diferentes.
    // Bosque alienígena de Kairós (noite):
    // - chão de musgo azul-petróleo com esporos que brilham fraco, trilha de terra violeta;
    // - rochedos de ardósia violeta com cristais ciano (cobertura), tocos de casca roxa com fungos âmbar (poleiros);
    // - portão de energia (porta da arena);
    // - sprites (fundo transparente, pivô no centro de baixo): árvores da mesma família (Chorão e Lanterna de tronco
    //   índigo e copa verde-azulada, Fungo gigante como destaque; brilho ciano em todas), arbusto, cogumelo, tufo e
    //   plantas. Espiral e Coral ficam reservadas para outros biomas.
    public static class GridTextureKit
    {
        public const string Folder = IcaroAssetUtility.Root + "/Art/Grade/Texturas";
        public const string MaterialsFolder = IcaroAssetUtility.Root + "/Art/Grade/Materiais";
        public const float PixelsPerMeter = 16f;

        // Cores do Bosque alienígena
        static readonly Color k_Moss = new Color(0.12f, 0.3f, 0.33f);
        static readonly Color k_MossDark = new Color(0.08f, 0.2f, 0.25f);
        static readonly Color k_MossLight = new Color(0.2f, 0.42f, 0.42f);
        static readonly Color k_Violet = new Color(0.45f, 0.3f, 0.62f);
        static readonly Color k_Soil = new Color(0.3f, 0.23f, 0.34f);
        static readonly Color k_SoilDark = new Color(0.2f, 0.15f, 0.25f);
        static readonly Color k_Slate = new Color(0.34f, 0.32f, 0.46f);
        static readonly Color k_SlateDark = new Color(0.2f, 0.19f, 0.29f);
        internal static readonly Color k_Crystal = new Color(0.55f, 0.95f, 1f);
        static readonly Color k_Bark = new Color(0.3f, 0.19f, 0.3f);
        static readonly Color k_BarkDark = new Color(0.17f, 0.1f, 0.18f);
        static readonly Color k_BarkLight = new Color(0.44f, 0.3f, 0.42f);
        static readonly Color k_Gate = new Color(0.08f, 0.08f, 0.13f);
        static readonly Color k_White = new Color(0.92f, 0.95f, 1f);

        internal static readonly Color k_NeonCyan = new Color(0.1f, 1f, 0.9f) * 2.2f;
        internal static readonly Color k_NeonAmber = new Color(1f, 0.6f, 0.1f) * 2.2f;
        static readonly Color k_NeonMagenta = new Color(1f, 0.2f, 0.8f) * 2.2f;
        static readonly Color k_NeonViolet = new Color(0.6f, 0.4f, 1f) * 2.2f;

        // Árvores: cada espécie é desenhada em 3 tamanhos já no tamanho final (o pixel é o mesmo em todas, 16 px/m:
        // nada de esticar a textura) e cada tamanho tem 3 materiais por distância: perto (cor e brilho), meio (mais
        // escuro) e fundo (silhueta escura com poucos pontos de brilho, quase a cor da névoa)
        public static readonly float[] TreeScales = { 1f, 1.6f, 2.4f };

        public class TreeSize
        {
            public float Scale;
            public Vector2 Size;        // metros
            public float TrunkRadius;   // metros
            public Material Near, Mid, Far;

            // Variações de tom (normal, mais clara, mais escura e azulada): a floresta não parece carimbo repetido
            public Material[] NearTints, MidTints;
        }

        public class TreeSpecies
        {
            public string Name;
            public float Weight;        // proporção entre as espécies misturadas
            public bool Accent;         // destaque raro, que cresce em manchas próprias (o Fungo gigante)
            public TreeSize[] Sizes;
        }

        // Planta ou detalhe espalhado no chão: chance por célula, tamanho (metros) e faixa de escala; com mais de um
        // material, sorteia entre eles
        public class DecorItem
        {
            public Material[] Materials;
            public Vector2 Size;
            public float Chance;
            public Vector2 Scale = Vector2.one;
        }

        // Tudo o que um tema de bioma em grade usa
        public class Set
        {
            public Material Rock, RockTop;          // cobertura
            public Material Perch, PerchTop;        // poleiros do Espinhento
            public Material Grass, Path, Door;
            public TreeSpecies[] Trees;
            public List<DecorItem> Decor = new List<DecorItem>();
            public Material MushroomCyan, MushroomAmber;   // cogumelos em volta das árvores de destaque
            public Vector2 MushroomSize;
            public Material Spores;

            // Água (opcional): fundo, superfície da água rasa e da água funda
            public Material WaterBed, WaterSurface, DeepSurface;
        }

        static readonly HashSet<string> s_Saved = new HashSet<string>();

        // Começo de uma geração: pastas prontas e o registro de texturas compartilhadas zerado
        internal static void BeginSession()
        {
            IcaroAssetUtility.EnsureFolder(Folder);
            IcaroAssetUtility.EnsureFolder(MaterialsFolder);
            s_Saved.Clear();
        }

        public static Set BuildBosque()
        {
            BeginSession();

            var set = new Set
            {
                Rock = Surface("Bosque_Rocha", PaintRock(), Color.black),
                RockTop = Surface("Bosque_Rocha_Topo", PaintRockTop(), Color.black),
                Perch = Surface("Bosque_Toco", PaintStump(), Color.black),
                PerchTop = Surface("Bosque_Toco_Topo", PaintRings(), Color.black),
                Grass = Surface("Bosque_Grama", PaintGrass(), Color.black),
                Path = Surface("Bosque_Trilha", PaintPath(), Color.black),
                Door = Surface("Bosque_Portao", PaintGate(), k_NeonCyan),
            };

            // Bosque: espécies da mesma família (tronco índigo, copas verde-azuladas), misturadas na mesma proporção.
            // Chorão, Lanterna e as variantes delas (Chorão Largo, Lanterna Alta) brilham em ciano; Salgueiro, Galhada e
            // Galhada Rala não brilham. O Fungo
            // gigante é o destaque raro, em manchas próprias.
            // A Espiral e o Coral ficam para outros biomas.
            set.Trees = new[]
            {
                Species("Chorao", 1f, 0.6f, PaintWeepingTree, k_TreeGlow),
                Species("Lanterna", 1f, 0.4f, PaintLanternTree, k_TreeGlow),
                Species("LanternaAlta", 1f, 0.4f, k => PaintLanternTree(k, k_TallLanternStyle), k_TreeGlow),
                Species("ChoraoLargo", 1f, 0.6f, k => PaintWeepingTree(k, k_WideWeepingStyle), k_TreeGlow),
                Species("Salgueiro", 1f, 0.55f, PaintWillowTree, Color.black),
                Species("Galhada", 1f, 0.45f, PaintBranchyTree, Color.black),
                Species("GalhadaRala", 1f, 0.45f, k => PaintBranchyTree(k, k_SparseBranchyStyle), Color.black),
                Species("Fungo", 1f, 0.5f, PaintFungusTree, k_TreeGlow, true),
            };

            Paint bush = PaintBush(), mushroom = PaintMushroom(), tuft = PaintTuft();
            Material bushMaterial = Sprite("Bosque_Arbusto", bush, Color.black);
            set.MushroomCyan = Sprite("Bosque_Cogumelo_Ciano", mushroom, Color.black, "Bosque_Cogumelo");
            set.MushroomAmber = Sprite("Bosque_Cogumelo_Ambar", mushroom, Color.black, "Bosque_Cogumelo");
            set.MushroomSize = mushroom.Size / PixelsPerMeter;
            Material tuftMaterial = Sprite("Bosque_Tufo", tuft, Color.black);

            // Plantas do fundo (sem brilho: só as árvores brilham) e o material dos esporos que flutuam
            Paint flower = PaintGlowFlower(), fern = PaintGlowFern(), reed = PaintGlowReed();
            Material flowerMaterial = Sprite("Bosque_Flor_Neon", flower, Color.black);
            Material fernMaterial = Sprite("Bosque_Samambaia_Neon", fern, Color.black);
            Material reedMaterial = Sprite("Bosque_Junco_Neon", reed, Color.black);
            set.Decor.Add(Decor(bush, new[] { bushMaterial }, 0.06f, 0.8f, 1.3f));
            set.Decor.Add(Decor(mushroom, new[] { set.MushroomCyan, set.MushroomCyan, set.MushroomAmber }, 0.04f, 0.6f, 1.1f));
            set.Decor.Add(Decor(tuft, new[] { tuftMaterial }, 0.18f, 0.6f, 0.95f));
            set.Decor.Add(Decor(flower, new[] { flowerMaterial }, 0.04f, 0.6f, 0.95f));
            set.Decor.Add(Decor(fern, new[] { fernMaterial }, 0.06f, 0.6f, 0.95f));
            set.Decor.Add(Decor(reed, new[] { reedMaterial }, 0.04f, 0.6f, 0.95f));
            set.Spores = SporeMaterial();
            return set;
        }

        internal static DecorItem Decor(Paint paint, Material[] materials, float chance, float minScale, float maxScale) =>
            new DecorItem
            {
                Materials = materials,
                Size = paint.Size / PixelsPerMeter,
                Chance = chance,
                Scale = new Vector2(minScale, maxScale),
            };

        // Brilho comum das árvores do Bosque
        static readonly Color k_TreeGlow = k_NeonCyan;

        // Cores das distâncias: o meio escurece a árvore; o fundo vira silhueta (a névoa termina de apagar)
        static readonly Color k_MidTint = new Color(0.55f, 0.52f, 0.68f);
        static readonly Color k_FarTint = new Color(0.16f, 0.14f, 0.26f);
        static readonly Color k_LightTint = new Color(1.1f, 1.08f, 1.12f);
        static readonly Color k_DarkTint = new Color(0.82f, 0.86f, 1f);

        internal static TreeSpecies Species(string name, float weight, float trunk, System.Func<float, Paint> paint, Color glow,
            bool accent = false, string prefix = "Bosque_Arvore_")
        {
            var sizes = new TreeSize[TreeScales.Length];
            for (int i = 0; i < TreeScales.Length; i++)
            {
                float scale = TreeScales[i];
                Paint p = paint(scale);
                string id = $"{prefix}{name}_{i + 1}";
                Material near = Sprite(id, p, glow);
                sizes[i] = new TreeSize
                {
                    Scale = scale,
                    Size = p.Size / PixelsPerMeter,
                    TrunkRadius = trunk * scale,
                    Near = near,
                    Mid = Tint(near, id + "_Meio", k_MidTint, 0.6f),
                    Far = Tint(near, id + "_Fundo", k_FarTint, 0.3f),
                    NearTints = new[]
                    {
                        near, Tint(near, id + "_Clara", k_LightTint, 1f), Tint(near, id + "_Escura", k_DarkTint, 1f),
                    },
                    MidTints = new[]
                    {
                        Tint(near, id + "_Meio", k_MidTint, 0.6f), Tint(near, id + "_Meio_Clara", k_MidTint * k_LightTint, 0.6f),
                        Tint(near, id + "_Meio_Escura", k_MidTint * k_DarkTint, 0.6f),
                    },
                };
            }

            return new TreeSpecies { Name = name, Weight = weight, Accent = accent, Sizes = sizes };
        }

        // Cópia do material com a cor multiplicada e o brilho reduzido
        static Material Tint(Material source, string name, Color tint, float glow)
        {
            Material material = LoadOrCreate(name);
            material.CopyPropertiesFromMaterial(source);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_EmissionColor", source.GetColor("_EmissionColor") * glow);
            material.globalIlluminationFlags = source.globalIlluminationFlags;
            material.renderQueue = source.renderQueue;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- pintura ----------

        // Imagem e máscara de brilho do mesmo tamanho
        internal class Paint
        {
            public readonly int W, H;
            public readonly bool Wrap;
            public readonly Color[] Pixels;
            public readonly Color[] Glow;
            readonly System.Random m_Random;

            public Paint(int w, int h, int seed, Color background, bool wrap)
            {
                W = w;
                H = h;
                Wrap = wrap;
                Pixels = new Color[w * h];
                Glow = new Color[w * h];
                m_Random = new System.Random(seed);
                for (int i = 0; i < Pixels.Length; i++)
                {
                    Pixels[i] = background;
                    Glow[i] = Color.black;
                }
            }

            public Vector2 Size => new Vector2(W, H);
            public float Next => (float)m_Random.NextDouble();
            public int Range(int min, int max) => m_Random.Next(min, max);

            bool Index(int x, int y, out int index)
            {
                index = -1;
                if (y < 0 || y >= H)
                    return false;
                if (Wrap)
                    x = ((x % W) + W) % W;
                else if (x < 0 || x >= W)
                    return false;
                index = y * W + x;
                return true;
            }

            public void Set(int x, int y, Color c, bool glow = false)
            {
                if (!Index(x, y, out int i))
                    return;
                c.a = 1f;
                Pixels[i] = c;
                Glow[i] = glow ? Color.white : Color.black;
            }

            public bool Filled(int x, int y) => Index(x, y, out int i) && Pixels[i].a > 0.5f;

            public Color Vary(Color c, float amount)
            {
                float k = 1f + (Next * 2f - 1f) * amount;
                return new Color(c.r * k, c.g * k, c.b * k, 1f);
            }

            // Disco sombreado: luz no alto à esquerda, borda escura, contorno irregular
            public void Blob(float cx, float cy, float radius, Color mid, Color light, Color dark)
            {
                int r = Mathf.CeilToInt(radius);
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y) + Next * 1.4f;
                    if (d > radius)
                        continue;
                    Color c = x + y < -radius * 0.55f ? light : d > radius - 1.5f || x - y > radius * 0.75f ? dark : mid;
                    Set(Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) + y, Vary(c, 0.05f));
                }
            }

            // Traço grosso de a até b (galhos, caules), mais escuro do lado direito
            public void Stroke(Vector2 a, Vector2 b, float widthA, float widthB, Color color, Color shade)
            {
                float length = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2f));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 p = Vector2.Lerp(a, b, t);
                    float half = Mathf.Lerp(widthA, widthB, t) * 0.5f;
                    for (int x = Mathf.FloorToInt(-half); x <= Mathf.CeilToInt(half); x++)
                    {
                        if (Mathf.Abs(x) > half + 0.25f)
                            continue;
                        Color c = x > half - 1.2f ? shade : color;
                        Set(Mathf.RoundToInt(p.x) + x, Mathf.RoundToInt(p.y), c);
                    }
                }
            }

            // Ponto redondo que brilha
            public void Dot(int x, int y, Color c, int radius = 1)
            {
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius + 1)
                        Set(x + dx, y + dy, c, true);
                }
            }
        }

        internal static Paint Tile(int w, int h, int seed, Color background) => new Paint(w, h, seed, background, true);
        internal static Paint Transparent(int w, int h, int seed) => new Paint(w, h, seed, new Color(0f, 0f, 0f, 0f), false);

        // Musgo azul-petróleo com pintas violeta e alguns esporos que brilham; borda da célula mais escura
        static Paint PaintGrass()
        {
            Paint p = Tile(32, 32, 31, k_Moss);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float roll = p.Next;
                Color c = x == 0 || y == 0 ? k_MossDark : roll < 0.18f ? k_MossDark : roll < 0.26f ? k_MossLight : k_Moss;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            for (int i = 0; i < 5; i++)
                p.Set(p.Range(2, 30), p.Range(2, 30), k_Violet);
            for (int i = 0; i < 2; i++)
                p.Set(p.Range(2, 30), p.Range(2, 30), k_Crystal, true);
            return p;
        }

        static Paint PaintPath()
        {
            Paint p = Tile(32, 32, 37, k_Soil);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color c = x == 0 || y == 0 ? k_SoilDark : p.Next < 0.15f ? k_SoilDark : k_Soil;
                p.Set(x, y, p.Vary(c, 0.06f));
            }

            for (int i = 0; i < 6; i++)
            {
                int x = p.Range(2, 30), y = p.Range(2, 30);
                p.Set(x, y, k_Slate);
                p.Set(x + 1, y, k_SlateDark);
            }

            return p;
        }

        // Rochedos de ardósia empilhados (três pedras de 2 m) com musgo, sem brilho
        static Paint PaintRock()
        {
            Paint p = Tile(32, 96, 17, k_SlateDark);
            for (int block = 0; block < 3; block++)
            {
                int y0 = block * 32, shift = block % 2 == 0 ? 0 : 9;
                for (int y = 1; y < 31; y++)
                for (int x = 1; x < 31; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, 31 - x), Mathf.Min(y, 31 - y));
                    if (edge < 2 && p.Next < 0.5f)
                        continue;
                    Color c = edge < 3 ? k_SlateDark * 1.2f : y > 24 ? k_Slate * 1.12f : k_Slate;
                    if ((x * 7 + y * 3) % 23 == 0)
                        c = k_SlateDark;
                    if (y > 26 - (x * 5 % 4) && p.Next < 0.8f)
                        c = k_Moss;
                    p.Set(x + shift, y0 + y, p.Vary(c, 0.07f));
                }
            }

            return p;
        }

        static Paint PaintRockTop()
        {
            Paint p = Tile(32, 32, 19, k_Moss);
            for (int i = 0; i < 160; i++)
                p.Set(p.Range(0, 32), p.Range(0, 32), p.Next < 0.5f ? k_MossDark : k_Slate);
            return p;
        }

        // Toco gigante: casca roxa com sulcos, sem brilho
        internal static Paint PaintStump()
        {
            Paint p = Tile(32, 96, 23, k_Bark);
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 32; x++)
            {
                Color c = (x + (y / 9) % 3) % 6 == 0 ? k_BarkDark : (x + 2) % 6 == 0 ? k_BarkLight : k_Bark;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            for (int y = 92; y < 96; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, y == 95 ? k_BarkLight : k_BarkDark);
            return p;
        }

        internal static Paint PaintRings()
        {
            Paint p = Tile(32, 32, 29, k_BarkLight);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                Color c = d > 14 ? k_BarkDark : Mathf.FloorToInt(d) % 3 == 0 ? k_Bark : k_BarkLight;
                p.Set(x, y, p.Vary(c, 0.04f));
            }

            return p;
        }

        // Portão de energia: placa escura com linhas verticais ciano e moldura (brilham)
        internal static Paint PaintGate()
        {
            Paint p = Tile(32, 96, 41, k_Gate);
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 32; x++)
            {
                bool frame = y < 2 || y >= 94;
                bool line = x % 8 == 3 && (y / 6) % 2 == 0;
                if (frame || line)
                    p.Set(x, y, k_Crystal, true);
                else
                    p.Set(x, y, p.Vary(k_Gate, 0.1f));
            }

            return p;
        }

        // ---------- árvores (5 variações) ----------

        // Tronco índigo comum às árvores do Bosque (Lanterna e Chorão)
        static readonly Color k_TreeTrunk = new Color(0.2f, 0.16f, 0.32f);
        static readonly Color k_TreeTrunkDark = new Color(0.11f, 0.08f, 0.18f);

        internal static int Px(float value) => Mathf.Max(1, Mathf.RoundToInt(value));

        // Fungo gigante (6 x 10 m na escala 1): caule claro com anéis, chapéu magenta com pintas e lamelas que brilham.
        // 'k' é o tamanho: tudo cresce junto, o pixel continua o mesmo
        static Paint PaintFungusTree(float k)
        {
            Paint p = Transparent(Px(96 * k), Px(160 * k), 101);
            float mid = p.W * 0.5f;
            Color stem = new Color(0.72f, 0.66f, 0.82f), stemDark = new Color(0.48f, 0.42f, 0.6f);
            float stemTop = 112f * k;
            int ring = Px(14 * k);
            for (int y = 0; y < stemTop; y++)
            {
                float cx = mid + Mathf.Sin(y * 0.04f / k) * 4f * k;
                float half = Mathf.Lerp(7f, 4.5f, y / stemTop) * k + (y < 6f * k ? (6f * k - y) * 0.8f : 0f);
                for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                {
                    Color c = x > cx + half - 2f ? stemDark : y % ring == 0 ? stemDark : stem;
                    p.Set(x, y, p.Vary(c, 0.04f));
                }
            }

            Color cap = new Color(0.62f, 0.16f, 0.5f), capLight = new Color(0.85f, 0.35f, 0.7f),
                capDark = new Color(0.38f, 0.08f, 0.32f);
            float rx = 46f * k, ry = 30f * k, capX = mid + 4f * k;
            for (int y = 0; y <= ry; y++)
            {
                float half = rx * Mathf.Sqrt(1f - (y / ry) * (y / ry));
                for (int x = Mathf.FloorToInt(capX - half); x <= Mathf.CeilToInt(capX + half); x++)
                {
                    float shade = (x - capX) / rx - y / ry;
                    Color c = shade < -0.55f ? capLight : shade > 0.45f ? capDark : cap;
                    p.Set(x, Mathf.RoundToInt(stemTop + y), p.Vary(c, 0.04f));
                }
            }

            // Lamelas por baixo do chapéu
            float gillHeight = 8f * k;
            for (int y = Mathf.RoundToInt(stemTop - gillHeight); y < stemTop; y++)
            for (int x = 0; x < p.W; x++)
            {
                float t = (stemTop - y) / (gillHeight + 1f);
                float half = rx * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                if (Mathf.Abs(x - capX) > half)
                    continue;
                bool gill = x % 3 == 0;
                p.Set(x, y, gill ? k_Crystal : capDark, gill);
            }

            int spots = Mathf.RoundToInt(9 * k);
            for (int i = 0; i < spots; i++)
            {
                int x = Mathf.RoundToInt(capX + (p.Next * 2f - 1f) * rx * 0.78f);
                int y = Mathf.RoundToInt(stemTop + (0.1f + p.Next * 0.7f) * ry);
                if (p.Filled(x, y))
                    p.Dot(x, y, k_White, Px((1 + p.Range(0, 2)) * k));
            }

            return p;
        }

        // Variação de uma árvore da família: o mesmo desenho com galhos em outras posições, folhas de outro tamanho e
        // outras proporções. Os valores padrão dão a árvore original.
        class TreeStyle
        {
            public int Seed;
            public int Branches = -1;       // -1 = quantidade original
            public float BranchStart = 1f;  // altura do primeiro galho (ou da copa, no Chorão)
            public float BranchSpacing = 1f;
            public float Reach = 1f;        // comprimento dos galhos
            public float Leaf = 1f;         // tamanho dos tufos de folhas
            public float Crown = 1f;        // largura da copa (Chorão) ou do tufo do topo
            public float Length = 1f;       // comprimento dos tentáculos (Chorão)
            public float Moss = 1f;         // barba-de-velho (Galhada)
        }

        static readonly TreeStyle k_LanternStyle = new TreeStyle { Seed = 103 };
        static readonly TreeStyle k_WeepingStyle = new TreeStyle { Seed = 109 };
        static readonly TreeStyle k_BranchyStyle = new TreeStyle { Seed = 223 };

        // Lanterna Alta: galhos começando mais alto, menos galhos e tufos maiores
        static readonly TreeStyle k_TallLanternStyle = new TreeStyle
            { Seed = 131, Branches = 5, BranchStart = 1.45f, BranchSpacing = 0.9f, Reach = 0.85f, Leaf = 1.45f, Crown = 1.2f };

        // Chorão Largo: copa mais larga e baixa, tentáculos curtos
        static readonly TreeStyle k_WideWeepingStyle = new TreeStyle
            { Seed = 137, BranchStart = 0.8f, Crown = 1.3f, Length = 0.6f };

        // Galhada Rala: galhos em outras alturas e mais longos, tufos menores, mais barba-de-velho
        static readonly TreeStyle k_SparseBranchyStyle = new TreeStyle
            { Seed = 229, Branches = 6, BranchStart = 1.3f, BranchSpacing = 1.2f, Reach = 1.15f, Leaf = 0.7f, Moss = 1.8f };

        static Paint PaintLanternTree(float k) => PaintLanternTree(k, k_LanternStyle);
        static Paint PaintWeepingTree(float k) => PaintWeepingTree(k, k_WeepingStyle);
        static Paint PaintBranchyTree(float k) => PaintBranchyTree(k, k_BranchyStyle);

        // Lanterna (6 x 12 m na escala 1): tronco índigo torcido, galhos com tufos verde-azulados e orbes que brilham
        static Paint PaintLanternTree(float k, TreeStyle style)
        {
            Paint p = Transparent(Px(96 * k), Px(192 * k), style.Seed);
            float mid = p.W * 0.5f;
            Color leaf = new Color(0.12f, 0.38f, 0.4f), leafLight = new Color(0.22f, 0.55f, 0.52f),
                leafDark = new Color(0.06f, 0.22f, 0.26f);

            float top = 160f * k, step = 4f * k;
            var previous = new Vector2(mid, 0f);
            for (float y = step; y <= top; y += step)
            {
                var next = new Vector2(mid + Mathf.Sin(y * 0.05f / k) * 6f * k, y);
                p.Stroke(previous, next, Mathf.Lerp(10f, 4f, (y - step) / top) * k, Mathf.Lerp(10f, 4f, y / top) * k,
                    k_TreeTrunk, k_TreeTrunkDark);
                previous = next;
            }

            int branches = style.Branches > 0 ? style.Branches : 7;
            for (int i = 0; i < branches; i++)
            {
                float y = (70f * style.BranchStart + i * 14f * style.BranchSpacing) * k;
                float side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(mid + Mathf.Sin(y * 0.05f / k) * 6f * k, y);
                Vector2 end = start + new Vector2(side * (18f + p.Range(0, 14)) * style.Reach, 10f + p.Range(0, 10)) * k;
                p.Stroke(start, end, 3f * k, 1.5f * k, k_TreeTrunk, k_TreeTrunkDark);
                p.Blob(end.x, end.y + 3f * k, (6f + p.Range(0, 3)) * k * style.Leaf, leaf, leafLight, leafDark);

                // Orbe pendurado
                Vector2 hang = Vector2.Lerp(start, end, 0.7f);
                int ox = Mathf.RoundToInt(hang.x), oy = Mathf.RoundToInt(hang.y);
                int drop = Px((6 + p.Range(0, 10)) * k);
                for (int d = 0; d < drop; d++)
                    p.Set(ox, oy - d, k_TreeTrunkDark);
                p.Dot(ox, oy - drop - Px(2 * k), k_Crystal, Px(3 * k));
            }

            p.Blob(mid + Mathf.Sin(8f) * 6f * k, 168f * k, 14f * k * style.Crown, leaf, leafLight, leafDark);
            p.Dot(Mathf.RoundToInt(mid + 2f * k), Mathf.RoundToInt(180f * k), k_Crystal, Px(2 * k));
            return p;
        }

        // Reservada para outro bioma (não entra no Bosque). Espiral (4 x 14 m): tronco fino listrado em espiral,
        // folhas compridas violeta no topo com pontas magenta
        static Paint PaintSpiralTree()
        {
            Paint p = Transparent(64, 224, 107);
            Color teal = new Color(0.15f, 0.45f, 0.45f), tealDark = new Color(0.08f, 0.26f, 0.3f);
            for (int y = 0; y < 190; y++)
            {
                float half = Mathf.Lerp(6f, 1.5f, y / 190f);
                for (int x = Mathf.FloorToInt(32 - half); x <= Mathf.CeilToInt(32 + half); x++)
                {
                    bool band = ((y + (x - 32) * 3) % 16 + 16) % 16 < 5;
                    p.Set(x, y, p.Vary(band ? tealDark : teal, 0.04f));
                }
            }

            Color frond = new Color(0.5f, 0.25f, 0.7f), frondDark = new Color(0.3f, 0.12f, 0.45f);
            var top = new Vector2(32f, 192f);
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.Lerp(-150f, -30f, i / 8f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle));
                Vector2 mid = top + dir * 14f + Vector2.up * 6f;
                Vector2 tip = top + dir * 28f - Vector2.up * 4f;
                p.Stroke(top, mid, 4f, 3f, frond, frondDark);
                p.Stroke(mid, tip, 3f, 1f, frond, frondDark);
                p.Dot(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), new Color(1f, 0.45f, 0.85f), 1);
            }

            return p;
        }

        // Chorão (8 x 10 m na escala 1): tronco índigo curto e grosso, copa verde-azulada em domo e tentáculos com
        // pontas que brilham
        // Pixel mais baixo da copa na coluna x: o primeiro, de baixo para cima, que mudou desde 'before' (o desenho
        // antes da copa; o tronco não conta). -1 se a coluna não tem copa.
        static int CrownBottom(Paint p, Color[] before, int x)
        {
            if (x < 0 || x >= p.W)
                return -1;
            for (int y = 0; y < p.H; y++)
            {
                int i = y * p.W + x;
                if (p.Pixels[i] != before[i])
                    return y;
            }

            return -1;
        }

        static Paint PaintWeepingTree(float k, TreeStyle style)
        {
            Paint p = Transparent(Px(128 * k * Mathf.Max(1f, style.Crown)), Px(160 * k), style.Seed);
            float mid = p.W * 0.5f;
            float domeY = 108f * k * style.BranchStart;
            p.Stroke(new Vector2(mid, 0f), new Vector2(mid - 2f * k, domeY - 8f * k), 14f * k, 9f * k, k_TreeTrunk,
                k_TreeTrunkDark);

            Color dome = new Color(0.08f, 0.3f, 0.42f), domeLight = new Color(0.16f, 0.45f, 0.55f),
                domeDark = new Color(0.04f, 0.17f, 0.27f);
            var beforeCrown = (Color[])p.Pixels.Clone();
            int blobs = Mathf.RoundToInt(18 * style.Crown);
            for (int i = 0; i < blobs; i++)
            {
                float a = p.Next * Mathf.PI, r = p.Next * 40f * k;
                p.Blob(mid + Mathf.Cos(a) * r * 1.3f * style.Crown, domeY + Mathf.Sin(a) * r * 0.9f,
                    (12f + p.Range(0, 6)) * k, dome, domeLight, domeDark);
            }

            // Tentáculos a cada 4 px (o espaçamento é de pixel, não cresce com o tamanho), cada um preso no ponto mais
            // baixo da copa na coluna dele (sem copa na coluna, não há tentáculo: nada fica flutuando)
            int count = Mathf.RoundToInt(26 * k * style.Crown);
            float start0 = 12f * k, spread = (p.W - 24f * k) / count;
            for (int i = 0; i < count; i++)
            {
                int x = Mathf.RoundToInt(start0 + i * spread) + p.Range(-1, 2);
                int length = Px((30 + p.Range(0, 45)) * k * style.Length);
                int bottom = CrownBottom(p, beforeCrown, x);
                if (bottom < 0)
                    continue;
                int start = bottom + 1;
                for (int d = 0; d < length; d++)
                {
                    int sx = x + Mathf.RoundToInt((Mathf.Sin(d * 0.15f / k + i) - Mathf.Sin(i)) * 1.2f * k);
                    bool tip = d >= length - 4;
                    p.Set(sx, start - d, tip ? k_Crystal : domeLight, tip);
                }
            }

            return p;
        }

        // Salgueiro (7 x 11 m na escala 1, sem brilho): parente do Chorão, com tronco índigo mais alto, copa azul-
        // petróleo em dois andares e franjas caídas
        static Paint PaintWillowTree(float k)
        {
            Paint p = Transparent(Px(112 * k), Px(176 * k), 211);
            float mid = p.W * 0.5f;
            p.Stroke(new Vector2(mid, 0f), new Vector2(mid + 3f * k, 124f * k), 11f * k, 6f * k, k_TreeTrunk,
                k_TreeTrunkDark);

            Color crown = new Color(0.1f, 0.27f, 0.4f), crownLight = new Color(0.18f, 0.42f, 0.52f),
                crownDark = new Color(0.05f, 0.15f, 0.26f);
            var beforeCrown = (Color[])p.Pixels.Clone();
            for (int i = 0; i < 12; i++)
            {
                float a = p.Next * Mathf.PI * 2f;
                p.Blob(mid + Mathf.Cos(a) * p.Next * 40f * k, 98f * k + Mathf.Sin(a) * p.Next * 12f * k,
                    (10f + p.Range(0, 5)) * k, crown, crownLight, crownDark);
            }

            for (int i = 0; i < 9; i++)
            {
                float a = p.Next * Mathf.PI * 2f;
                p.Blob(mid + 3f * k + Mathf.Cos(a) * p.Next * 26f * k, 140f * k + Mathf.Sin(a) * p.Next * 14f * k,
                    (10f + p.Range(0, 5)) * k, crown, crownLight, crownDark);
            }

            // Franjas que caem da copa de baixo, presas no ponto mais baixo da copa na coluna de cada uma
            int count = Mathf.RoundToInt(22 * k);
            float left = 14f * k, spread = (p.W - 28f * k) / count;
            for (int i = 0; i < count; i++)
            {
                int x = Mathf.RoundToInt(left + i * spread) + p.Range(-1, 2);
                int length = Px((18 + p.Range(0, 28)) * k);
                int bottom = CrownBottom(p, beforeCrown, x);
                if (bottom < 0)
                    continue;
                int start = bottom + 1;
                for (int d = 0; d < length; d++)
                {
                    int sx = x + Mathf.RoundToInt((Mathf.Sin(d * 0.12f / k + i * 1.7f) - Mathf.Sin(i * 1.7f)) * k);
                    p.Set(sx, start - d, d > length - 3 ? crownDark : crown);
                }
            }

            return p;
        }

        // Galhada (6,5 x 11 m na escala 1, sem brilho): parente da Lanterna, tronco índigo torcido com muitos galhos,
        // tufos verde-azulados e barba-de-velho pendurada
        static Paint PaintBranchyTree(float k, TreeStyle style)
        {
            Paint p = Transparent(Px(104 * k), Px(176 * k), style.Seed);
            float mid = p.W * 0.5f;
            Color leaf = new Color(0.1f, 0.34f, 0.38f), leafLight = new Color(0.2f, 0.5f, 0.5f),
                leafDark = new Color(0.05f, 0.2f, 0.24f);
            Color moss = new Color(0.28f, 0.42f, 0.42f);

            float top = 150f * k, step = 4f * k;
            var previous = new Vector2(mid, 0f);
            for (float y = step; y <= top; y += step)
            {
                var next = new Vector2(mid + Mathf.Sin(y * 0.04f / k) * 8f * k, y);
                p.Stroke(previous, next, Mathf.Lerp(12f, 3f, (y - step) / top) * k, Mathf.Lerp(12f, 3f, y / top) * k,
                    k_TreeTrunk, k_TreeTrunkDark);
                previous = next;
            }

            int branches = style.Branches > 0 ? style.Branches : 9;
            int strands = Mathf.RoundToInt(3 * style.Moss);
            for (int i = 0; i < branches; i++)
            {
                float y = (50f * style.BranchStart + i * 11f * style.BranchSpacing) * k;
                float side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(mid + Mathf.Sin(y * 0.04f / k) * 8f * k, y);
                Vector2 end = start + new Vector2(side * (20f + p.Range(0, 16)) * style.Reach, 8f + p.Range(0, 14)) * k;
                p.Stroke(start, end, 3.5f * k, 1.5f * k, k_TreeTrunk, k_TreeTrunkDark);
                p.Blob(end.x, end.y + 2f * k, (7f + p.Range(0, 4)) * k * style.Leaf, leaf, leafLight, leafDark);
                Vector2 middle = Vector2.Lerp(start, end, 0.55f);
                p.Blob(middle.x, middle.y + 4f * k, (4f + p.Range(0, 3)) * k * style.Leaf, leaf, leafLight, leafDark);

                // Barba-de-velho pendurada do galho
                for (int s = 0; s < strands; s++)
                {
                    Vector2 hang = Vector2.Lerp(start, end, 0.3f + 0.75f * s / Mathf.Max(1, strands));
                    int length = Px((8 + p.Range(0, 12)) * k * Mathf.Sqrt(style.Moss));
                    for (int d = 1; d < length; d++)
                        p.Set(Mathf.RoundToInt(hang.x), Mathf.RoundToInt(hang.y) - d, moss);
                }
            }

            p.Blob(mid + Mathf.Sin(6f) * 8f * k, 158f * k, 16f * k, leaf, leafLight, leafDark);
            return p;
        }

        // Reservada para outro bioma (não entra no Bosque). Coral (6 x 9 m): galhos que se dividem, rosa-violeta,
        // com pontas que brilham
        static Paint PaintCoralTree()
        {
            Paint p = Transparent(96, 144, 113);
            Branch(p, new Vector2(48f, 0f), 90f, 42f, 9f, 5);
            return p;
        }

        static void Branch(Paint p, Vector2 start, float angle, float length, float width, int depth)
        {
            Color coral = new Color(0.72f, 0.32f, 0.6f), coralDark = new Color(0.45f, 0.16f, 0.4f);
            Vector2 end = start + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * length;
            p.Stroke(start, end, width, width * 0.7f, coral, coralDark);
            if (depth <= 1)
            {
                p.Dot(Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y), new Color(0.75f, 0.6f, 1f), 2);
                return;
            }

            float spread = 22f + p.Next * 18f;
            Branch(p, end, angle + spread, length * (0.68f + p.Next * 0.12f), width * 0.68f, depth - 1);
            Branch(p, end, angle - spread, length * (0.68f + p.Next * 0.12f), width * 0.68f, depth - 1);
        }

        // ---------- sprites pequenos ----------

        static Paint PaintBush()
        {
            Paint p = Transparent(32, 24, 47);
            Color bush = new Color(0.38f, 0.2f, 0.5f), light = new Color(0.55f, 0.32f, 0.68f),
                dark = new Color(0.22f, 0.1f, 0.32f);
            for (int i = 0; i < 7; i++)
                p.Blob(p.Range(9, 23), p.Range(7, 13), p.Range(5, 8), bush, light, dark);
            for (int i = 0; i < 4; i++)
            {
                int x = p.Range(6, 26), y = p.Range(5, 18);
                if (p.Filled(x, y))
                    p.Set(x, y, k_Crystal, true);
            }

            return p;
        }

        // Cogumelo de 1 m: o chapéu brilha (a cor vem do material: ciano ou âmbar)
        internal static Paint PaintMushroom()
        {
            Paint p = Transparent(16, 16, 53);
            for (int y = 0; y < 8; y++)
            for (int x = 6; x < 10; x++)
                p.Set(x, y, x == 6 ? new Color(0.6f, 0.58f, 0.7f) : new Color(0.82f, 0.8f, 0.9f));

            for (int y = 8; y < 14; y++)
            {
                int half = y < 12 ? 7 : 7 - (y - 11) * 2;
                for (int x = 8 - half; x < 8 + half; x++)
                    p.Set(x, y, y == 8 ? Color.gray : Color.white, y > 8);
            }

            return p;
        }

        // Tufo de folhas finas azul-petróleo com pontas magenta que brilham
        internal static Paint PaintTuft()
        {
            Paint p = Transparent(16, 20, 59);
            Color blade = new Color(0.16f, 0.45f, 0.5f), bladeLight = new Color(0.3f, 0.62f, 0.62f);
            for (int b = 0; b < 7; b++)
            {
                int x0 = 2 + b * 2, height = p.Range(8, 19);
                int lean = p.Range(-1, 2);
                for (int y = 0; y < height; y++)
                {
                    bool tip = y == height - 1;
                    p.Set(x0 + lean * y / 5, y, tip ? new Color(1f, 0.5f, 0.85f) : y > height - 4 ? bladeLight : blade,
                        tip);
                }
            }

            return p;
        }

        // ---------- plantas fluorescentes ----------

        // Flor de 1 x 2 m: caule fino, duas folhas e um bulbo que brilha (magenta)
        static Paint PaintGlowFlower()
        {
            Paint p = Transparent(16, 32, 61);
            Color stem = new Color(0.14f, 0.38f, 0.42f), stemDark = new Color(0.08f, 0.24f, 0.3f);
            for (int y = 0; y < 22; y++)
                p.Set(8 + Mathf.RoundToInt(Mathf.Sin(y * 0.25f)), y, y % 5 == 0 ? stemDark : stem);
            for (int i = 0; i < 5; i++)
            {
                p.Set(7 - i, 6 + i / 2, stem);
                p.Set(10 + i, 11 + i / 2, stem);
            }

            for (int y = -4; y <= 4; y++)
            for (int x = -4; x <= 4; x++)
            {
                if (x * x + y * y > 17)
                    continue;
                bool core = x * x + y * y < 5;
                p.Set(8 + x, 26 + y, core ? new Color(1f, 0.85f, 0.95f) : new Color(0.95f, 0.35f, 0.8f), true);
            }

            return p;
        }

        // Samambaia de 2 x 1,5 m: folhas em leque com a nervura do meio brilhando (verde-limão)
        internal static Paint PaintGlowFern()
        {
            Paint p = Transparent(32, 24, 67);
            Color leaf = new Color(0.12f, 0.3f, 0.38f), leafDark = new Color(0.07f, 0.18f, 0.26f);
            for (int f = 0; f < 6; f++)
            {
                float angle = Mathf.Lerp(20f, 160f, f / 5f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int s = 0; s < 18; s++)
                {
                    Vector2 point = new Vector2(16f, 1f) + dir * s + Vector2.down * (s * s * 0.025f);
                    int px = Mathf.RoundToInt(point.x), py = Mathf.RoundToInt(point.y);
                    int width = s < 12 ? 2 : 1;
                    for (int w = -width; w <= width; w++)
                        p.Set(px + Mathf.RoundToInt(-dir.y * w), py + Mathf.RoundToInt(dir.x * w), w < 0 ? leafDark : leaf);
                    p.Set(px, py, new Color(0.65f, 1f, 0.4f), s > 2);
                }
            }

            return p;
        }

        // Juncos de 1,5 x 2 m: hastes escuras com pontas que brilham (ciano)
        internal static Paint PaintGlowReed()
        {
            Paint p = Transparent(24, 32, 71);
            Color reed = new Color(0.12f, 0.16f, 0.36f), reedLight = new Color(0.2f, 0.26f, 0.5f);
            for (int r = 0; r < 8; r++)
            {
                int x0 = 3 + r * 2 + p.Range(0, 2), height = p.Range(16, 31);
                float lean = (p.Next - 0.5f) * 0.25f;
                for (int y = 0; y < height; y++)
                {
                    int x = x0 + Mathf.RoundToInt(lean * y);
                    bool tip = y >= height - 3;
                    p.Set(x, y, tip ? k_Crystal : y % 4 == 0 ? reedLight : reed, tip);
                }
            }

            return p;
        }

        // Esporos: partícula aditiva sem textura (quadradinho), bem acima de 1 para o Bloom
        internal static Material SporeMaterial()
        {
            string path = $"{MaterialsFolder}/Esporo_Neon.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Esporo_Neon" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", Color.white * 3f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- assets ----------

        internal static Texture2D Save(string name, int width, Color[] pixels, bool cutout = false)
        {
            int height = pixels.Length / width;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.npotScale = TextureImporterNPOTScale.None;
            // Sprites recortados: os mipmaps mantêm a cobertura do alfa (senão árvores distantes "afinam" e somem)
            importer.mipMapsPreserveCoverage = cutout;
            importer.alphaTestReferenceValue = 0.5f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool HasGlow(Paint p)
        {
            foreach (Color c in p.Glow)
            {
                if (c.r > 0f)
                    return true;
            }

            return false;
        }

        // Superfície do mapa (opaca)
        internal static Material Surface(string name, Paint paint, Color emission)
        {
            Texture2D baseMap = Save(name, paint.W, paint.Pixels);
            Texture2D glow = HasGlow(paint) && emission.maxColorComponent > 0f
                ? Save(name + "_Brilho", paint.W, paint.Glow)
                : null;
            Material material = LoadOrCreate(name);
            Setup(material, baseMap, glow, emission);
            return material;
        }

        // Sprite com recorte por alfa, dos dois lados, sem sombra. Dois materiais podem dividir a mesma textura
        // (textureName), que é salva uma vez por geração.
        internal static Material Sprite(string name, Paint paint, Color emission, string textureName = null)
        {
            string file = textureName ?? name;
            Texture2D baseMap, glow;
            if (s_Saved.Add(file))
            {
                baseMap = Save(file, paint.W, paint.Pixels, true);
                glow = HasGlow(paint) ? Save(file + "_Brilho", paint.W, paint.Glow) : null;
            }
            else
            {
                baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{file}.png");
                glow = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{file}_Brilho.png");
            }

            Material material = LoadOrCreate(name);
            Setup(material, baseMap, emission.maxColorComponent > 0f ? glow : null, emission);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            return material;
        }

        // Variante do material de sprite para o shader Icaro/BillboardSprite (giro na placa de vídeo), com a mesma
        // textura, cor, recorte e brilho
        internal static Material BillboardMaterial(Material lit)
        {
            string path = $"{MaterialsFolder}/{lit.name}_GPU.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Icaro/BillboardSprite");
            if (!material)
            {
                material = new Material(shader) { name = lit.name + "_GPU" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", lit.GetTexture("_BaseMap"));
            material.SetColor("_BaseColor", lit.GetColor("_BaseColor"));
            material.SetFloat("_Cutoff", lit.HasProperty("_Cutoff") ? lit.GetFloat("_Cutoff") : 0.5f);
            bool emission = lit.IsKeywordEnabled("_EMISSION");
            material.SetTexture("_EmissionMap", emission ? lit.GetTexture("_EmissionMap") : null);
            material.SetColor("_EmissionColor", emission ? lit.GetColor("_EmissionColor") : Color.black);
            if (emission)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Cópia de um material com a cor multiplicada por 'tint' (transição de cor entre os materiais de dois biomas)
        internal static Material Tinted(string name, Material source, Color tint)
        {
            Material material = LoadOrCreate(name);
            material.shader = source.shader;
            material.CopyPropertiesFromMaterial(source);
            material.shaderKeywords = source.shaderKeywords;
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Textura que mistura as de dois materiais (transição entre biomas): manchas de ruído repetível que crescem com
        // 'amount' (0 = só a primeira, 1 = só a segunda; a fração de pixels da segunda é 'amount'), com a borda das
        // manchas pontilhada. A mesma máscara serve para todos os passos de uma transição (as manchas só crescem de
        // um passo para o outro). A segunda textura é lida em ladrilho se o tamanho for outro.
        internal static Material Blend(string name, Material first, Material second, float amount, int seed)
        {
            Texture2D a = ReadBaseMap(first), b = ReadBaseMap(second);
            if (!a || !b)
            {
                if (a) Object.DestroyImmediate(a);
                if (b) Object.DestroyImmediate(b);
                return amount < 0.5f ? first : second;
            }

            int w = a.width, h = a.height;
            Color[] pa = a.GetPixels(), pb = b.GetPixels();
            float[,] coarse = Lattice(4, seed), fine = Lattice(8, seed + 1);
            var mask = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[y * w + x] = 0.65f * SampleLattice(coarse, (float)x / w, (float)y / h) +
                                  0.35f * SampleLattice(fine, (float)x / w, (float)y / h);

            // Posição de cada pixel na ordem da máscara (0 a 1): a fração trocada fica exatamente 'amount'
            var order = new int[mask.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            System.Array.Sort(order, (i, j) => mask[i].CompareTo(mask[j]));
            var rank = new float[mask.Length];
            for (int i = 0; i < order.Length; i++)
                rank[order[i]] = i / (order.Length - 1f);

            var random = new System.Random(seed + 2);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color ca = pa[i], cb = pb[(y % b.height) * b.width + x % b.width];
                // Os pixels de máscara mais baixa viram a segunda textura primeiro; perto do limite, pontilhado
                float edge = rank[i] - amount;
                pixels[i] = edge < -0.05f ? cb : edge > 0.05f ? ca : random.NextDouble() < 0.5 ? ca : cb;
            }

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Texture2D texture = Save(name, w, pixels);
            Material material = LoadOrCreate(name);
            Setup(material, texture, null, Color.black);
            return material;
        }

        static Texture2D ReadBaseMap(Material material)
        {
            string path = material ? AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        // Grade n x n de valores aleatórios (repetível nas bordas) e a leitura suave dela
        static float[,] Lattice(int n, int seed)
        {
            var random = new System.Random(seed);
            var values = new float[n, n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                values[x, y] = (float)random.NextDouble();
            return values;
        }

        static float SampleLattice(float[,] values, float u, float v)
        {
            int n = values.GetLength(0);
            float fx = u * n, fy = v * n;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            float V(int x, int y) => values[((x % n) + n) % n, ((y % n) + n) % n];
            return Mathf.Lerp(Mathf.Lerp(V(x0, y0), V(x0 + 1, y0), tx), Mathf.Lerp(V(x0, y0 + 1), V(x0 + 1, y0 + 1), tx),
                ty);
        }

        // Cor média da textura de um material (lida do PNG; pixels transparentes não contam)
        internal static Color AverageColor(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"));
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Color.gray;
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            Color sum = Color.clear;
            int count = 0;
            foreach (Color c in texture.GetPixels())
            {
                if (c.a < 0.5f)
                    continue;
                sum += c;
                count++;
            }

            Object.DestroyImmediate(texture);
            return count > 0 ? sum / count : Color.gray;
        }

        static Material LoadOrCreate(string name)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        static void Setup(Material material, Texture2D baseMap, Texture2D emissionMap, Color emission)
        {
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);

            // Sem brilho especular nem reflexo: sprites virados para a câmera refletiam a lua todos juntos (clarão)
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (emissionMap)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", emissionMap);
                material.SetColor("_EmissionColor", emission);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", null);
                material.SetColor("_EmissionColor", Color.black);
            }

            // O URP só mantém a palavra-chave _EMISSION se o material tiver uma flag de GI emissiva
            material.globalIlluminationFlags = emissionMap
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }
    }
}
