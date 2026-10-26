using UnityEditor;
using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas dos Picos acima das Nuvens (pixel art desenhada em código): rocha escura dos picos com fraturas
    // verticais, neve branco-azulada com ondulações do vento, cantaria clara dos Aeônidas em três tons (geada nas
    // juntas), piso de lajes com neve nas juntas, pedra com runas ciano acesas, gelo azul e a superfície do portal. A
    // neve é só visual (nada muda no movimento). As nuvens não são textura: o mar de nuvens é volume (Icaro/CloudVolume) e os
    // fiapos são tufos de fumaça (Icaro/CloudWisp), com materiais próprios.
    // As montanhas usam o Icaro/SnowRock (Mountain): rocha de colunas e neve em pixel art misturadas por pixel.
    public static class PicosTextureKit
    {
        public class Set
        {
            public Material Rock, Snow, Floor, Rune, Ice, Portal, Dark, CloudVolume, Wisp, Mountain, Iron;
            public Material[] Stones;
        }

        static readonly Color k_Rock = new Color(0.16f, 0.15f, 0.17f);
        static readonly Color k_RockLight = new Color(0.27f, 0.26f, 0.29f);
        static readonly Color k_RockDark = new Color(0.08f, 0.075f, 0.09f);
        static readonly Color k_Snow = new Color(0.86f, 0.89f, 0.97f);
        static readonly Color k_SnowShade = new Color(0.66f, 0.7f, 0.86f);
        static readonly Color k_SnowBright = new Color(0.97f, 0.98f, 1f);
        static readonly Color k_Stone = new Color(0.55f, 0.57f, 0.63f);
        static readonly Color k_StoneJoint = new Color(0.28f, 0.29f, 0.34f);
        static readonly Color k_Frost = new Color(0.85f, 0.9f, 0.98f);
        static readonly Color k_RuneStone = new Color(0.2f, 0.22f, 0.27f);
        static readonly Color k_Cyan = new Color(0.3f, 1f, 0.95f);
        static readonly Color k_Ice = new Color(0.55f, 0.8f, 0.95f);
        static readonly Color k_IceDeep = new Color(0.25f, 0.5f, 0.75f);

        public static Set Build()
        {
            GridTextureKit.BeginSession();
            return new Set
            {
                Rock = GridTextureKit.Surface("Picos_Rocha", PaintRock(), Color.black),
                Snow = GridTextureKit.Surface("Picos_Neve", PaintSnow(), Color.black),
                Floor = GridTextureKit.Surface("Picos_Piso", PaintFloor(), Color.black),
                Rune = GridTextureKit.Surface("Picos_Runas", PaintRunes(), k_Cyan * 3f),
                Ice = GridTextureKit.Surface("Picos_Gelo", PaintIce(), k_Ice * 0.6f),
                Portal = GridTextureKit.Surface("Picos_Portal", PaintPortal(), k_Cyan * 2.6f),
                Dark = GridTextureKit.Surface("Picos_Vao", GridTextureKit.Tile(4, 4, 992, new Color(0.03f, 0.035f, 0.05f)),
                    Color.black),
                CloudVolume = ShaderMaterial("Picos_Nuvens_Volume", "Icaro/CloudVolume"),
                Wisp = ShaderMaterial("Picos_Nuvens_Fiapos", "Icaro/CloudWisp"),
                Mountain = MountainMaterial(),
                Iron = GridTextureKit.Surface("Picos_Ferro", PaintIron(), Color.black),
                Stones = new[]
                {
                    GridTextureKit.Surface("Picos_Pedra_1", PaintStone(1101, 1f), Color.black),
                    GridTextureKit.Surface("Picos_Pedra_2", PaintStone(1102, 0.9f), Color.black),
                    GridTextureKit.Surface("Picos_Pedra_3", PaintStone(1103, 1.08f), Color.black),
                },
            };
        }

        // Material de um shader próprio (as nuvens), criado uma vez; os ajustes feitos no Inspector ficam
        static Material ShaderMaterial(string name, string shaderName)
        {
            string path = $"{GridTextureKit.MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // Montanhas: rocha e neve projetadas pelo mundo e misturadas no shader (Icaro/SnowRock)
        static Material MountainMaterial()
        {
            Material material = ShaderMaterial("Picos_Montanha", "Icaro/SnowRock");
            Paint rock = PaintMountainRock(), snow = PaintMountainSnow();
            material.SetTexture("_RockMap", GridTextureKit.Save("Picos_Montanha_Rocha", rock.W, rock.Pixels));
            material.SetTexture("_SnowMap", GridTextureKit.Save("Picos_Montanha_Neve", snow.W, snow.Pixels));
            // Neve nos topos e nas plataformas planas; nas paredes, só alguns riscos (o pedido foi pouca neve nas laterais)
            material.SetFloat("_SnowAmount", 0.42f);
            material.SetFloat("_SnowNoise", 0.3f);
            material.SetFloat("_Streaks", 0.08f);
            material.SetFloat("_HeightBonus", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Ferro das correntes e das ferragens (16 px): cinza-azulado escuro com geada nas bordas
        static Paint PaintIron()
        {
            Color iron = new Color(0.17f, 0.18f, 0.21f);
            Paint p = GridTextureKit.Tile(16, 16, 1231, iron);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, p.Vary(iron * (y > 12 ? 1.25f : 1f), 0.05f));
            for (int k = 0; k < 8; k++)
                p.Set(p.Range(0, 16), p.Range(10, 16), Color.Lerp(iron, k_Frost, 0.6f));
            return p;
        }

        static readonly Color k_Crag = new Color(0.29f, 0.26f, 0.27f);
        static readonly Color k_CragLight = new Color(0.43f, 0.39f, 0.39f);
        static readonly Color k_CragDark = new Color(0.12f, 0.1f, 0.12f);

        // Rocha das torres (64 px = 4 m, 16 px/m): colunas verticais de larguras diferentes separadas por fendas
        // escuras que serpenteiam, com a quina clara do lado da luz, camadas horizontais fracas e lascas
        static Paint PaintMountainRock()
        {
            Paint p = GridTextureKit.Tile(64, 64, 1201, k_Crag);
            var layout = new System.Random(1202);
            int[] cracks = { 0, 9, 17, 30, 38, 51 };
            var tones = new float[cracks.Length];
            for (int i = 0; i < tones.Length; i++)
                tones[i] = 0.86f + (float)layout.NextDouble() * 0.26f;

            for (int y = 0; y < 64; y++)
            {
                // A fenda serpenteia (o deslocamento volta ao começo no fim da imagem, para repetir sem costura)
                int wobble = Mathf.RoundToInt(Mathf.Sin(y / 64f * Mathf.PI * 2f) * 1.6f + Mathf.Sin(y / 64f * Mathf.PI * 6f) * 0.8f);
                for (int x = 0; x < 64; x++)
                {
                    // Coluna = a última fenda à esquerda (antes da primeira, a última da imagem, que dá a volta)
                    int column = cracks.Length - 1;
                    for (int i = 0; i < cracks.Length; i++)
                    {
                        if (x >= cracks[i] + wobble * (i % 2 == 0 ? 1 : -1))
                            column = i;
                    }

                    float stratum = Mathf.Sin(y / 64f * Mathf.PI * 8f + column * 1.3f) > 0.86f ? 0.9f : 1f;
                    p.Set(x, y, p.Vary(k_Crag * tones[column] * stratum, 0.05f));
                }

                for (int i = 0; i < cracks.Length; i++)
                {
                    int shift = wobble * (i % 2 == 0 ? 1 : -1);
                    int cx = (cracks[i] + shift + 64) % 64;
                    p.Set(cx, y, p.Vary(k_CragDark, 0.04f));
                    if (p.Next < 0.55f)
                        p.Set((cx + 1) % 64, y, p.Vary(k_CragDark * 1.4f, 0.04f));
                    p.Set((cx + 63) % 64, y, p.Vary(k_CragLight, 0.04f));
                }
            }

            // Lascas: pequenas faces claras com a sombra embaixo
            for (int k = 0; k < 16; k++)
            {
                int x = p.Range(0, 64), y = p.Range(0, 64), w = p.Range(2, 5);
                for (int i = 0; i < w; i++)
                {
                    p.Set(x + i, y, p.Vary(k_CragLight, 0.04f));
                    p.Set(x + i, y - 1, p.Vary(k_CragDark * 1.3f, 0.04f));
                }
            }

            for (int k = 0; k < 40; k++)
                p.Set(p.Range(0, 64), p.Range(0, 64), p.Vary(p.Next < 0.5f ? k_CragDark * 1.6f : k_CragLight * 0.95f, 0.05f));
            return p;
        }

        // Neve das montanhas (64 px = 4 m): branco-azulada com montes de sombra azul em blocos de pixel, linhas do vento
        // e brilhos
        static Paint PaintMountainSnow()
        {
            Paint p = GridTextureKit.Tile(64, 64, 1211, k_Snow);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = x / 64f * Mathf.PI * 2f, v = y / 64f * Mathf.PI * 2f;
                float drift = Mathf.Sin(u * 2f + Mathf.Sin(v) * 1.5f) * 0.5f + Mathf.Sin(v * 3f + Mathf.Cos(u * 2f)) * 0.5f;
                Color c = drift > 0.55f ? Color.Lerp(k_Snow, k_SnowShade, 0.55f) : drift < -0.7f ? k_SnowBright : k_Snow;
                p.Set(x, y, p.Vary(c, 0.02f));
            }

            for (int k = 0; k < 9; k++)
                p.Blob(p.Range(0, 64), p.Range(0, 64), p.Range(3, 7), k_Snow, k_SnowBright, Color.Lerp(k_Snow, k_SnowShade, 0.7f));
            for (int k = 0; k < 14; k++)
                p.Set(p.Range(0, 64), p.Range(0, 64), Color.white);
            return p;
        }

        // Rocha dos picos (32 px = 4 m): fraturas verticais, faixas tortas e pintas de geada
        static Paint PaintRock()
        {
            Paint p = GridTextureKit.Tile(32, 32, 1001, k_Rock);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float band = Mathf.Sin((x + Mathf.Sin(y * 0.3f) * 2.5f) * 0.7f);
                Color c = band > 0.65f ? k_RockLight : band < -0.75f ? k_RockDark : k_Rock;
                p.Set(x, y, p.Vary(c, 0.07f));
            }

            for (int k = 0; k < 5; k++)
            {
                int x = p.Range(0, 32), y = p.Range(0, 32);
                for (int i = 0; i < 16; i++)
                {
                    p.Set(x, y, k_RockDark * 0.7f);
                    x += p.Range(-1, 2) == 0 ? 0 : p.Range(-1, 2);
                    y += 1;
                }
            }

            for (int k = 0; k < 18; k++)
                p.Set(p.Range(0, 32), p.Range(0, 32), Color.Lerp(k_RockLight, k_Frost, 0.6f));
            return p;
        }

        // Neve (32 px = 4 m): branco-azulada, ondulações do vento e brilhos
        static Paint PaintSnow()
        {
            Paint p = GridTextureKit.Tile(32, 32, 1011, k_Snow);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float ripple = Mathf.Sin((x * 0.35f + y * 0.9f) + Mathf.Sin(x * 0.2f) * 2f);
                Color c = ripple > 0.85f ? k_SnowShade : ripple < -0.9f ? k_SnowBright : k_Snow;
                p.Set(x, y, p.Vary(c, 0.025f));
            }

            for (int k = 0; k < 10; k++)
                p.Set(p.Range(0, 32), p.Range(0, 32), Color.white);
            return p;
        }

        // Cantaria dos Aeônidas (64 px = 4 m): blocos claros em fiadas de 1 m desencontradas, juntas escuras com geada
        static Paint PaintStone(int seed, float shade)
        {
            Paint p = GridTextureKit.Tile(64, 64, seed, k_Stone * shade);
            var layout = new System.Random(seed + 7);
            for (int row = 0; row < 4; row++)
            {
                int y0 = row * 16, offset = layout.Next(0, 32);
                for (int y = y0; y < y0 + 16; y++)
                for (int x = 0; x < 64; x++)
                {
                    int bx = ((x + offset) % 64) / 32;
                    float tone = shade * (0.93f + (float)new System.Random(seed + row * 10 + bx).NextDouble() * 0.12f);
                    bool joint = y == y0 || (x + offset) % 32 == 0;
                    Color c = joint ? k_StoneJoint : k_Stone * tone * (1.03f - (y - y0) / 16f * 0.08f);
                    if (joint && p.Next < 0.45f)
                        c = k_Frost;
                    p.Set(x, y, p.Vary(c, 0.03f));
                }
            }

            for (int k = 0; k < 6; k++)
                p.Blob(p.Range(0, 64), p.Range(0, 64), p.Range(1, 3), k_Stone * shade * 0.9f, k_Stone * shade * 1.1f,
                    k_StoneJoint);
            return p;
        }

        // Piso de lajes de 2 m com a neve nas juntas e um pouco sobre as pedras (64 px = 4 m)
        static Paint PaintFloor()
        {
            Paint p = GridTextureKit.Tile(64, 64, 1021, k_Stone);
            var layout = new System.Random(1022);
            for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
            {
                float tone = 0.88f + (float)layout.NextDouble() * 0.16f;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool joint = x <= 1 || y <= 1;
                    Color c = joint ? k_Snow : k_Stone * tone;
                    p.Set(sx * 32 + x, sy * 32 + y, p.Vary(c, 0.03f));
                }
            }

            for (int k = 0; k < 7; k++)
                p.Blob(p.Range(0, 64), p.Range(0, 64), p.Range(3, 7), k_Snow, k_SnowBright, k_SnowShade);
            return p;
        }

        // Pedra escura com runas ciano acesas: anéis concêntricos e traços (64 px)
        static Paint PaintRunes()
        {
            Paint p = GridTextureKit.Tile(64, 64, 1031, k_RuneStone);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                p.Set(x, y, p.Vary(k_RuneStone, 0.05f));

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = 26f - ring * 8f;
                for (int a = 0; a < 220; a++)
                {
                    if ((a / 9 + ring) % 4 == 0)
                        continue;
                    float t = a / 220f * Mathf.PI * 2f;
                    p.Set(32 + Mathf.RoundToInt(Mathf.Cos(t) * radius), 32 + Mathf.RoundToInt(Mathf.Sin(t) * radius), k_Cyan,
                        true);
                }
            }

            for (int k = 0; k < 8; k++)
            {
                int x = p.Range(4, 60), y = p.Range(4, 60), length = p.Range(3, 7);
                for (int i = 0; i < length; i++)
                    p.Set(x + (k % 2 == 0 ? i : 0), y + (k % 2 == 0 ? 0 : i), k_Cyan, true);
            }

            return p;
        }

        // Gelo (32 x 64; v ao longo do pingente ou da cachoeira): azul com riscos claros no comprimento
        static Paint PaintIce()
        {
            Paint p = GridTextureKit.Tile(32, 64, 1041, k_Ice);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                float streak = Mathf.Sin(x * 1.3f + Mathf.Sin(y * 0.1f) * 2f);
                Color c = Color.Lerp(k_IceDeep, k_Ice, streak * 0.5f + 0.5f);
                bool shine = streak > 0.92f;
                p.Set(x, y, p.Vary(shine ? k_Frost : c, 0.04f), shine);
            }

            return p;
        }

        // Superfície do portal: anéis concêntricos de ciano e branco sobre azul-escuro aceso
        static Paint PaintPortal()
        {
            Paint p = GridTextureKit.Tile(32, 32, 1061, k_IceDeep);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                float band = Mathf.Repeat(d * 5f + Mathf.Atan2(y - 16f, x - 16f) * 0.3f, 1f);
                Color c = band < 0.15f ? Color.white : band < 0.45f ? k_Cyan : k_IceDeep * 1.4f;
                p.Set(x, y, p.Vary(c, 0.05f), true);
            }

            return p;
        }
    }
}
