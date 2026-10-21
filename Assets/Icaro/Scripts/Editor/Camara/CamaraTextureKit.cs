using UnityEditor;
using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas da Câmara de Cristal (pixel art desenhada em código): pedra monolítica preto-azulada lisa e polida
    // (blocos com juntas finas, piso de lajes e painéis com entalhes geométricos), rocha bruta da caverna, cristal
    // escuro e espelhado com as arestas e o miolo acesos, o núcleo branco-ciano e a superfície dos portais.
    public static class CamaraTextureKit
    {
        public class Set
        {
            public Material Stone, Floor, Engraved, Rock, Crystal, CrystalPale, Core, Portal;
            public Material Spores;
        }

        static readonly Color k_Stone = new Color(0.14f, 0.165f, 0.2f);
        static readonly Color k_StoneLight = new Color(0.24f, 0.28f, 0.33f);
        static readonly Color k_Joint = new Color(0.05f, 0.06f, 0.075f);
        static readonly Color k_Groove = new Color(0.07f, 0.085f, 0.1f);
        static readonly Color k_Rock = new Color(0.12f, 0.125f, 0.14f);
        static readonly Color k_RockLight = new Color(0.2f, 0.21f, 0.23f);
        static readonly Color k_RockDark = new Color(0.065f, 0.07f, 0.08f);
        static readonly Color k_CrystalDark = new Color(0.07f, 0.2f, 0.21f);
        static readonly Color k_CrystalMid = new Color(0.16f, 0.42f, 0.42f);
        static readonly Color k_CrystalLight = new Color(0.6f, 0.95f, 0.9f);
        static readonly Color k_Ice = new Color(0.85f, 1f, 1f);
        static readonly Color k_Cyan = new Color(0.3f, 1f, 0.8f);

        public static Set Build()
        {
            GridTextureKit.BeginSession();
            var set = new Set
            {
                Stone = Polish(GridTextureKit.Surface("Camara_Pedra", PaintStone(false), Color.black), 0.45f),
                Engraved = Polish(GridTextureKit.Surface("Camara_Pedra_Entalhe", PaintStone(true), Color.black), 0.45f),
                Floor = Polish(GridTextureKit.Surface("Camara_Piso", PaintFloor(), Color.black), 0.5f),
                Rock = GridTextureKit.Surface("Camara_Rocha", PaintRock(), Color.black),
                Crystal = Polish(GridTextureKit.Surface("Camara_Cristal", PaintCrystal(false), k_Cyan * 4f), 0.9f, true),
                CrystalPale = Polish(GridTextureKit.Surface("Camara_Cristal_Claro", PaintCrystal(true), k_Ice * 2.6f), 0.9f,
                    true),
                Core = GridTextureKit.Surface("Camara_Nucleo", PaintCore(), new Color(0.55f, 1f, 0.9f) * 3.5f),
                Portal = GridTextureKit.Surface("Camara_Portal", PaintPortal(), k_Cyan * 2.6f),
                Spores = GridTextureKit.SporeMaterial(),
            };
            return set;
        }

        // Pedra polida: um pouco de brilho especular (as luzes dos cristais correm pela superfície); o cristal também
        // reflete o ambiente
        static Material Polish(Material material, float smoothness, bool reflections = false)
        {
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SpecularHighlights", 1f);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", reflections ? 1f : 0f);
            if (reflections)
                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Blocos monolíticos (64 px = 4 m): fiadas de 2 m com juntas finas desencontradas, cada bloco num tom levemente
        // diferente, com um degradê sutil de cima para baixo e pintas claras de polimento. Com 'engraved', entalhes
        // geométricos: triângulos aninhados e linhas, em sulcos escuros com um fio claro embaixo.
        static Paint PaintStone(bool engraved)
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, engraved ? 811 : 801, k_Stone);
            var layout = new System.Random(802);
            for (int row = 0; row < 2; row++)
            {
                int y0 = row * 32, cut = row == 0 ? 0 : 24 + layout.Next(16);
                float shadeA = 0.92f + (float)layout.NextDouble() * 0.16f, shadeB = 0.92f + (float)layout.NextDouble() * 0.16f;
                for (int y = y0; y < y0 + 32; y++)
                for (int x = 0; x < size; x++)
                {
                    bool left = ((x - cut) % size + size) % size < 40;
                    float shade = (left ? shadeA : shadeB) * (1.04f - (y - y0) / 32f * 0.08f);
                    bool joint = y == y0 || x == cut % size || x == (cut + 40) % size;
                    Color c = joint ? k_Joint : k_Stone * shade;
                    if (!joint && p.Next < 0.025f)
                        c = Color.Lerp(c, k_StoneLight, 0.6f);
                    p.Set(x, y, p.Vary(c, 0.025f));
                }
            }

            if (engraved)
            {
                // Triângulos aninhados no meio e uma faixa de linhas em cima e embaixo
                for (int t = 0; t < 3; t++)
                {
                    int half = 22 - t * 7, height = 30 - t * 10, bx = 32, by = 18 + t * 2;
                    for (int s = 0; s <= height; s++)
                    {
                        int dx = half * (height - s) / Mathf.Max(1, height);
                        Groove(p, bx - dx, by + s);
                        Groove(p, bx + dx, by + s);
                    }

                    for (int x = bx - half; x <= bx + half; x++)
                        Groove(p, x, by);
                }

                for (int x = 0; x < size; x++)
                {
                    Groove(p, x, 6);
                    Groove(p, x, 58);
                    if (x % 8 == 0)
                        for (int y = 3; y < 10; y++)
                            Groove(p, x, y);
                }
            }

            return p;
        }

        static void Groove(Paint p, int x, int y)
        {
            p.Set(x, y, k_Groove);
            p.Set(x, y - 1, k_StoneLight * 0.9f);
        }

        // Piso (64 px = 4 m): lajes quadradas de 2 m bem polidas, juntas finas, um reflexo claro e difuso em cada laje
        static Paint PaintFloor()
        {
            const int size = 64;
            Paint p = GridTextureKit.Tile(size, size, 821, k_Stone);
            var layout = new System.Random(822);
            for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
            {
                float shade = 0.9f + (float)layout.NextDouble() * 0.2f;
                float hx = 6f + (float)layout.NextDouble() * 20f, hy = 6f + (float)layout.NextDouble() * 20f;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool joint = x == 0 || y == 0;
                    float glint = Mathf.Max(0f, 1f - Vector2.Distance(new Vector2(x, y), new Vector2(hx, hy)) / 14f);
                    Color c = joint ? k_Joint : Color.Lerp(k_Stone * shade, k_StoneLight, glint * 0.45f);
                    p.Set(sx * 32 + x, sy * 32 + y, p.Vary(c, 0.025f));
                }
            }

            return p;
        }

        // Rocha bruta da caverna (32 px = 4 m): camadas tortas, manchas e rachaduras
        static Paint PaintRock()
        {
            Paint p = GridTextureKit.Tile(32, 32, 831, k_Rock);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float band = Mathf.Sin((y + Mathf.Sin(x * 0.4f) * 2f) * 0.55f);
                Color c = band > 0.6f ? k_RockLight : band < -0.7f ? k_RockDark : k_Rock;
                p.Set(x, y, p.Vary(c, 0.08f));
            }

            for (int k = 0; k < 5; k++)
                p.Blob(p.Range(0, 32), p.Range(0, 32), p.Range(2, 5), k_Rock, k_RockLight, k_RockDark);
            for (int k = 0; k < 3; k++)
            {
                int x = p.Range(0, 32), y = p.Range(0, 32);
                for (int i = 0; i < 12; i++)
                {
                    p.Set(x, y, k_RockDark * 0.7f);
                    x += p.Range(-1, 2);
                    y += 1;
                }
            }

            return p;
        }

        // Face de cristal (32 x 64; u atravessa a face, v corre ao longo do cristal): escura e esverdeada, com veios
        // claros no comprimento; acesos: as duas arestas, um fio no miolo e algumas faíscas. 'pale' = cristal branco
        static Paint PaintCrystal(bool pale)
        {
            Paint p = GridTextureKit.Tile(32, 64, pale ? 843 : 841, k_CrystalDark);
            Color dark = pale ? new Color(0.25f, 0.4f, 0.42f) : k_CrystalDark, mid = pale ? k_CrystalLight * 0.7f : k_CrystalMid;
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                float vein = Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.15f) * 1.5f);
                Color c = Color.Lerp(dark, mid, Mathf.Clamp01(vein * 0.6f + (1f - Mathf.Abs(x - 15.5f) / 16f) * 0.35f));
                p.Set(x, y, p.Vary(c, 0.04f));
            }

            for (int y = 0; y < 64; y++)
            {
                foreach (int x in new[] { 0, 1, 30, 31 })
                    p.Set(x, y, pale ? k_Ice : k_CrystalLight, true);
                if (y % 3 != 0)
                    p.Set(15 + (int)(Mathf.Sin(y * 0.2f) * 2f), y, k_Ice, true);
            }

            for (int k = 0; k < 8; k++)
                p.Set(p.Range(2, 30), p.Range(0, 64), k_Ice, true);
            return p;
        }

        // Núcleo: branco no meio, ciano nas bordas, faixas que lembram luz em camadas
        static Paint PaintCore()
        {
            Paint p = GridTextureKit.Tile(32, 32, 851, k_Ice);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Abs(x - 15.5f) / 16f;
                Color c = Color.Lerp(k_Ice, k_Cyan, d * d + (y % 6 == 0 ? 0.25f : 0f));
                p.Set(x, y, p.Vary(c, 0.04f), true);
            }

            return p;
        }

        // Superfície do portal: anéis triangulares de luz ciano e branca sobre fundo verde-escuro aceso
        static Paint PaintPortal()
        {
            Paint p = GridTextureKit.Tile(32, 32, 861, k_CrystalMid);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float u = (x + 0.5f) / 32f, v = (y + 0.5f) / 32f;
                float tri = Mathf.Max(Mathf.Abs(u - 0.5f) * 2f + v, 1f - v) ;
                float band = Mathf.Repeat(tri * 5f, 1f);
                Color c = band < 0.18f ? k_Ice : band < 0.45f ? k_Cyan : k_CrystalMid * 1.6f;
                p.Set(x, y, p.Vary(c, 0.05f), true);
            }

            return p;
        }
    }
}
