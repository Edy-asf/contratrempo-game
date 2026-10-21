using UnityEditor;
using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas do Arquipélago (mesmas regras do GridTextureKit: pixel art gerada por código, 16 px/m, filtro Point;
    // brilho pelo mapa de emissão e cor no material). Noite verde-azulada:
    // - capim verde-água no topo das ilhas (com flores claras), penhasco de pedra em camadas com musgo;
    // - pedra de ruína em blocos (anéis, palácios, pontes, colunas) em três tons (cada bloco sorteia um), musgo
    //   espesso no topo das peças, pedra molhada com algas e cracas abaixo da linha d'água e o fundo escuro dos vãos;
    // - mar verde-azulado com ondinhas (quase opaco); portão de energia;
    // - tufos de capim, flores e trepadeiras (sprites).
    public static class ArquipelagoTextureKit
    {
        public class Set
        {
            public Material Grass, Cliff, Stone, StoneDark, Silhouette, Sea, Door;
            public Material[] StoneTones, StoneDarkTones;
            public Material Moss, Wet, Shadow, Sand;
            public Material[] Pavings;       // lajes grandes limpas (variações), para os pisos do Cemitério e da Ponte
            public Material[] PavingMoss;    // transições laje-musgo (o musgo cresce em manchas de um passo ao outro)
            public Material MossPatch;       // placa de musgo de borda irregular (carta deitada no chão)
            public Material Tuft, Flower, Vine;
            public Vector2 TuftSize, FlowerSize, VineSize;
            public Material Spores;
        }

        static readonly Color k_Grass = new Color(0.12f, 0.36f, 0.3f);
        static readonly Color k_GrassDark = new Color(0.07f, 0.24f, 0.21f);
        static readonly Color k_GrassLight = new Color(0.2f, 0.48f, 0.38f);
        static readonly Color k_Petal = new Color(0.8f, 0.92f, 0.85f);
        static readonly Color k_Rock = new Color(0.25f, 0.32f, 0.31f);
        static readonly Color k_RockDark = new Color(0.15f, 0.2f, 0.2f);
        static readonly Color k_RockLight = new Color(0.34f, 0.42f, 0.4f);
        static readonly Color k_Moss = new Color(0.13f, 0.34f, 0.26f);
        static readonly Color k_Ruin = new Color(0.3f, 0.36f, 0.36f);
        static readonly Color k_RuinDark = new Color(0.17f, 0.22f, 0.23f);
        static readonly Color k_Sea = new Color(0.07f, 0.3f, 0.28f);
        static readonly Color k_SeaLight = new Color(0.2f, 0.5f, 0.45f);
        static readonly Color k_MossDark = new Color(0.07f, 0.22f, 0.17f);
        static readonly Color k_MossLight = new Color(0.22f, 0.48f, 0.34f);
        static readonly Color k_Algae = new Color(0.08f, 0.2f, 0.14f);
        static readonly Color k_Barnacle = new Color(0.55f, 0.6f, 0.56f);
        static readonly Color k_Shadow = new Color(0.03f, 0.06f, 0.07f);

        // Tons de pedra (multiplicam a cor): neutro, esverdeado e mais claro azulado
        static readonly Color[] k_Tones =
        {
            new Color(1f, 1f, 1f), new Color(0.88f, 0.98f, 0.92f), new Color(1.07f, 1.06f, 1.12f),
        };

        public static Set Build()
        {
            GridTextureKit.BeginSession();
            Paint tuft = PaintTuft(), flower = PaintFlower(), vine = PaintVine();
            var set = new Set
            {
                Grass = GridTextureKit.Surface("Arquipelago_Capim", PaintGrass(), Color.black),
                Cliff = GridTextureKit.Surface("Arquipelago_Penhasco", PaintCliff(), Color.black),
                Stone = GridTextureKit.Surface("Arquipelago_Ruina", PaintRuin(false), Color.black),
                StoneDark = GridTextureKit.Surface("Arquipelago_Ruina_Escura", PaintRuin(true), Color.black),
                Sea = Sea(),
                Door = GridTextureKit.Surface("Arquipelago_Portao", GridTextureKit.PaintGate(), GridTextureKit.k_NeonCyan),
                Moss = GridTextureKit.Surface("Arquipelago_Ruina_Musgo", PaintMoss(), Color.black),
                Wet = GridTextureKit.Surface("Arquipelago_Ruina_Molhada", PaintWet(), Color.black),
                Shadow = GridTextureKit.Surface("Arquipelago_Ruina_Vao", PaintShadow(), Color.black),
                Sand = GridTextureKit.Surface("Arquipelago_Areia", PaintSand(), Color.black),
                Tuft = GridTextureKit.Sprite("Arquipelago_Tufo", tuft, Color.black),
                Vine = GridTextureKit.Sprite("Arquipelago_Trepadeira", vine, Color.black),
                VineSize = vine.Size / GridTextureKit.PixelsPerMeter,
                Flower = GridTextureKit.Sprite("Arquipelago_Flor", flower, Color.black),
                TuftSize = tuft.Size / GridTextureKit.PixelsPerMeter,
                FlowerSize = flower.Size / GridTextureKit.PixelsPerMeter,
                Spores = GridTextureKit.SporeMaterial(),
            };
            set.StoneTones = new Material[k_Tones.Length];
            set.StoneDarkTones = new Material[k_Tones.Length];
            for (int i = 0; i < k_Tones.Length; i++)
            {
                set.StoneTones[i] = GridTextureKit.Surface($"Arquipelago_Ruina_Tom{i + 1}", PaintRuin(false, i),
                    Color.black);
                set.StoneDarkTones[i] = GridTextureKit.Surface($"Arquipelago_Ruina_Escura_Tom{i + 1}", PaintRuin(true, i),
                    Color.black);
            }

            set.Pavings = new Material[3];
            for (int i = 0; i < set.Pavings.Length; i++)
                set.Pavings[i] = GridTextureKit.Surface($"Arquipelago_Laje_{i + 1}", PaintPaving(i), Color.black);
            set.PavingMoss = new Material[5];
            for (int k = 0; k < set.PavingMoss.Length; k++)
                set.PavingMoss[k] = GridTextureKit.Blend($"Arquipelago_Laje_Musgo_{k + 1}", set.Pavings[0], set.Moss,
                    (k + 1) / 6f, 2424);
            set.MossPatch = GridTextureKit.Sprite("Arquipelago_Placa_Musgo", PaintMossPatch(), Color.black);

            set.Silhouette = Silhouette(set.StoneDark.GetTexture("_BaseMap"));
            return set;
        }

        // Ruínas do fundo: vulto na névoa (shader Icaro/RuinSilhouette), com a mesma textura da pedra escura
        static Material Silhouette(Texture texture)
        {
            string path = GridTextureKit.MaterialsFolder + "/Arquipelago_Ruina_Silhueta.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Icaro/RuinSilhouette")) { name = "Arquipelago_Ruina_Silhueta" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(0.55f, 0.85f, 0.8f));
            material.SetFloat("_FogDensity", 0.0032f);
            material.SetFloat("_MaxFog", 0.8f);
            material.SetFloat("_Shade", 0.45f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Mar: quase opaco, com ondinhas (corre pelo WaterRipple)
        static Material Sea()
        {
            Material material = GridTextureKit.Surface("Arquipelago_Mar", PaintSea(), Color.black);
            // A versão com reflexo (Icaro/Sea) deixava o mar preto: volta ao URP/Lit transparente de antes
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit && material.shader != lit)
                material.shader = lit;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.88f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Paint PaintGrass()
        {
            Paint p = GridTextureKit.Tile(32, 32, 601, k_Grass);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float roll = p.Next;
                Color c = roll < 0.2f ? k_GrassDark : roll < 0.32f ? k_GrassLight : k_Grass;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            for (int i = 0; i < 3; i++)
                p.Set(p.Range(1, 31), p.Range(1, 31), k_Petal);
            return p;
        }

        // Penhasco: camadas horizontais de pedra (a textura repete a cada 2 m), musgo nas saliências
        static Paint PaintCliff()
        {
            Paint p = GridTextureKit.Tile(32, 32, 603, k_Rock);
            for (int y = 0; y < 32; y++)
            {
                int band = y / 6;
                for (int x = 0; x < 32; x++)
                {
                    Color c = y % 6 == 0 ? k_RockDark : y % 6 == 5 ? k_RockLight : band % 2 == 0 ? k_Rock : k_Rock * 0.92f;
                    if (y % 6 == 5 && p.Next < 0.35f)
                        c = k_Moss;
                    if ((x * 5 + band * 7) % 19 == 0 && y % 6 != 0)
                        c = k_RockDark; // rachadura
                    p.Set(x, y, p.Vary(c, 0.05f));
                }
            }

            return p;
        }

        // Pedra de ruína: blocos aparelhados com juntas e musgo, sem brilho
        static Paint PaintRuin(bool dark, int tone = 0)
        {
            Color stone = (dark ? k_RuinDark * 1.1f : k_Ruin) * k_Tones[tone];
            stone.a = 1f;
            Paint p = GridTextureKit.Tile(32, 32, (dark ? 607 : 605) + tone * 20, stone);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = y / 8;
                bool joint = y % 8 == 0 || (x + (row % 2) * 8) % 16 == 0;
                Color c = joint ? k_RuinDark * (dark ? 0.7f : 1f) : stone;
                if (!joint && y % 8 == 7 && p.Next < 0.3f)
                    c = k_Moss;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            return p;
        }

        // Lajes grandes do piso (8 m de textura, lajes de 2 m em fiadas desencontradas, algumas emendadas em lajes de
        // 4 m): juntas escuras, tom próprio em cada laje e bordas gastas; a variação 1 tem rachaduras e a 2, manchas de
        // sujeira e um pouco de musgo nas juntas. As três têm o mesmo desenho de lajes (emendam sem costura).
        static Paint PaintPaving(int variant)
        {
            const int size = 128, slab = 32;
            Paint p = GridTextureKit.Tile(size, size, 640 + variant * 7, k_Ruin);
            var layout = new System.Random(641);
            var tone = new float[size, size];
            var joint = new bool[size, size];
            for (int row = 0; row < size / slab; row++)
            {
                int offset = layout.Next(2) * slab / 2, y0 = row * slab;
                for (int x = -slab; x < size; )
                {
                    int width = layout.NextDouble() < 0.25 ? slab * 2 : slab;
                    float shade = 0.9f + (float)layout.NextDouble() * 0.2f;
                    for (int y = y0; y < y0 + slab; y++)
                    for (int dx = 0; dx < width; dx++)
                    {
                        int px = ((x + offset + dx) % size + size) % size;
                        tone[px, y] = shade;
                        joint[px, y] = y == y0 || dx == 0;
                    }

                    x += width;
                }
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = joint[x, y] ? k_RuinDark : k_Ruin * tone[x, y];
                // Borda gasta: um pouco mais clara logo depois da junta
                if (!joint[x, y] && (joint[(x + size - 1) % size, y] || joint[x, (y + size - 1) % size]) && p.Next < 0.5f)
                    c *= 1.12f;
                if (variant == 2 && joint[x, y] && p.Next < 0.35f)
                    c = k_Moss;
                c.a = 1f;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            if (variant == 1)
            {
                // Rachaduras: caminhos tortos escuros
                for (int k = 0; k < 4; k++)
                {
                    int x = p.Range(0, size), y = p.Range(0, size), length = p.Range(14, 34);
                    int dx = p.Range(-1, 2), dy = p.Range(0, 2) * 2 - 1;
                    for (int i = 0; i < length; i++)
                    {
                        p.Set(x, y, k_RuinDark * 0.8f);
                        if (p.Next < 0.6f)
                            y += dy;
                        else
                            x += dx == 0 ? p.Range(-1, 2) : dx;
                    }
                }
            }
            else if (variant == 2)
            {
                // Manchas de sujeira
                for (int k = 0; k < 6; k++)
                    p.Dot(p.Range(0, size), p.Range(0, size), k_RuinDark * 1.2f, p.Range(3, 7));
            }

            return p;
        }

        // Placa de musgo de borda irregular (com transparência), para deitar em cima das lajes
        static Paint PaintMossPatch()
        {
            const int size = 48;
            Paint p = GridTextureKit.Transparent(size, size, 645);
            float seedX = p.Next * 50f, seedY = p.Next * 50f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float edge = 1f - Mathf.Sqrt(u * u + v * v) + (Mathf.PerlinNoise(seedX + x * 0.18f, seedY + y * 0.18f) - 0.5f) * 0.7f;
                if (edge < 0.12f)
                    continue;
                float roll = p.Next;
                Color c = roll < 0.25f ? k_MossDark : roll < 0.4f ? k_MossLight : k_Moss;
                if (edge < 0.2f && p.Next < 0.5f)
                    continue;
                p.Set(x, y, p.Vary(c, 0.06f));
            }

            return p;
        }

        // Musgo espesso no topo das peças: tufos escuros e pontas claras
        static Paint PaintMoss()
        {
            Paint p = GridTextureKit.Tile(32, 32, 631, k_Moss);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float roll = p.Next;
                Color c = roll < 0.25f ? k_MossDark : roll < 0.4f ? k_MossLight : k_Moss;
                p.Set(x, y, p.Vary(c, 0.06f));
            }

            for (int i = 0; i < 10; i++)
                p.Dot(p.Range(0, 32), p.Range(0, 32), k_MossDark, p.Range(1, 3));
            for (int i = 0; i < 14; i++)
                p.Set(p.Range(0, 32), p.Range(0, 32), k_MossLight * 1.2f);
            return p;
        }

        // Pedra molhada da linha d'água: blocos escuros, fios de alga escorrendo e cachos de cracas
        static Paint PaintWet()
        {
            Color stone = k_RuinDark * 0.85f;
            stone.a = 1f;
            Paint p = GridTextureKit.Tile(32, 32, 633, stone);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = y / 8;
                bool joint = y % 8 == 0 || (x + (row % 2) * 8) % 16 == 0;
                p.Set(x, y, p.Vary(joint ? k_Shadow * 1.6f : stone, 0.05f));
            }

            for (int i = 0; i < 9; i++)
            {
                int x0 = p.Range(0, 32), length = p.Range(4, 14);
                for (int y = 0; y < length; y++)
                    p.Set(x0 + (y % 5 == 4 ? 1 : 0), y, k_Algae);
            }

            for (int i = 0; i < 6; i++)
            {
                int cx = p.Range(2, 30), cy = p.Range(2, 30);
                for (int k = 0; k < 4; k++)
                {
                    int bx = cx + p.Range(-2, 3), by = cy + p.Range(-2, 3);
                    p.Set(bx, by, k_Barnacle);
                    p.Set(bx + 1, by, k_Barnacle * 0.75f);
                    p.Set(bx, by + 1, k_Shadow);
                }
            }

            return p;
        }

        // Areia escura e úmida da costa, com conchinhas claras e marcas de onda
        static Paint PaintSand()
        {
            var sand = new Color(0.26f, 0.3f, 0.27f);
            Paint p = GridTextureKit.Tile(32, 32, 639, sand);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool ripple = (y + (x / 6) % 2) % 7 == 0;
                p.Set(x, y, p.Vary(ripple ? new Color(sand.r * 0.85f, sand.g * 0.85f, sand.b * 0.85f) : sand, 0.06f));
            }

            for (int i = 0; i < 6; i++)
                p.Set(p.Range(0, 32), p.Range(0, 32), k_Barnacle);
            return p;
        }

        // Fundo dos vãos (janelas): quase preto
        static Paint PaintShadow()
        {
            Paint p = GridTextureKit.Tile(32, 32, 635, k_Shadow);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, p.Vary(k_Shadow, 0.08f));
            return p;
        }

        // Trepadeira: fios que descem do topo, com folhinhas, mais finos e esparsos embaixo
        static Paint PaintVine()
        {
            Paint p = GridTextureKit.Transparent(16, 44, 637);
            for (int strand = 0; strand < 5; strand++)
            {
                int x = 1 + strand * 3 + p.Range(0, 2);
                int length = p.Range(18, 44);
                for (int y = 0; y < length; y++)
                {
                    int row = 43 - y;
                    if (y > 4 && p.Next < 0.18f)
                        x += p.Range(-1, 2);
                    x = Mathf.Clamp(x, 0, 15);
                    p.Set(x, row, y % 7 == 3 ? k_MossLight : k_MossDark);
                    if (p.Next < 0.35f)
                        p.Set(Mathf.Clamp(x + (p.Next < 0.5f ? -1 : 1), 0, 15), row, k_Moss);
                }
            }

            return p;
        }

        static Paint PaintSea()
        {
            Paint p = GridTextureKit.Tile(32, 32, 609, k_Sea);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, p.Vary(k_Sea, 0.05f));
            for (int i = 0; i < 7; i++)
            {
                int x0 = p.Range(0, 32), y0 = p.Range(0, 32), length = p.Range(3, 8);
                for (int s = 0; s < length; s++)
                    p.Set(x0 + s, y0, k_SeaLight);
            }

            return p;
        }

        // Tufo de capim alto verde-água
        static Paint PaintTuft()
        {
            Paint p = GridTextureKit.Transparent(16, 20, 611);
            for (int b = 0; b < 8; b++)
            {
                int x0 = 1 + b * 2, height = p.Range(8, 19);
                int lean = p.Range(-1, 2);
                for (int y = 0; y < height; y++)
                    p.Set(x0 + lean * y / 6, y, y > height - 4 ? k_GrassLight : k_Grass);
            }

            return p;
        }

        // Florzinhas claras em hastes finas
        static Paint PaintFlower()
        {
            Paint p = GridTextureKit.Transparent(16, 16, 613);
            for (int f = 0; f < 4; f++)
            {
                int x = 2 + f * 4, height = p.Range(6, 14);
                for (int y = 0; y < height; y++)
                    p.Set(x, y, k_GrassDark);
                p.Set(x, height, k_Petal);
                p.Set(x - 1, height, k_Petal);
                p.Set(x + 1, height, k_Petal);
                p.Set(x, height + 1, k_Petal);
            }

            return p;
        }
    }
}
