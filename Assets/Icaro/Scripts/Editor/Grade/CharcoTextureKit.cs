using UnityEditor;
using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas e sprites do Charco alienígena (mesmas regras do GridTextureKit: pixel art gerada por código, 16 px/m,
    // filtro Point; brilho pelo mapa de emissão e cor no material).
    // - Lama verde-escura com poças pequenas (chão), passarela de tábuas (trilha), leito de lodo (fundo da água);
    // - água rasa com ondinhas e algas que brilham (transparente) e água funda mais escura;
    // - emaranhados de raízes com fungos azuis (cobertura); poleiros e portão iguais aos do Bosque (leitura comum);
    // - árvores da família do Charco (tronco cinza-oliva, copas verde-azuladas, barba-de-velho clara): Mangue e
    //   Mangue Alto (raízes aéreas, brilho azul nas raízes), Cipreste e Cipreste Ralo (base larga, sem brilho) e o
    //   Lírio-lanterna (destaque raro: haste com sino que brilha);
    // - plantas sem brilho: taboas, juncos, samambaias, tufos e cogumelos.
    public static class CharcoTextureKit
    {
        const float k_PixelsPerMeter = GridTextureKit.PixelsPerMeter;

        static readonly Color k_Mud = new Color(0.13f, 0.2f, 0.17f);
        static readonly Color k_MudDark = new Color(0.08f, 0.13f, 0.11f);
        static readonly Color k_MudMoss = new Color(0.2f, 0.32f, 0.22f);
        static readonly Color k_Puddle = new Color(0.25f, 0.4f, 0.42f);
        static readonly Color k_Wood = new Color(0.36f, 0.26f, 0.18f);
        static readonly Color k_WoodDark = new Color(0.15f, 0.1f, 0.07f);
        static readonly Color k_WoodLight = new Color(0.46f, 0.35f, 0.25f);
        static readonly Color k_Silt = new Color(0.08f, 0.13f, 0.14f);
        static readonly Color k_Pebble = new Color(0.16f, 0.22f, 0.22f);
        static readonly Color k_Water = new Color(0.2f, 0.55f, 0.62f);
        static readonly Color k_Ripple = new Color(0.5f, 0.85f, 0.9f);
        static readonly Color k_Root = new Color(0.3f, 0.26f, 0.2f);
        static readonly Color k_RootDark = new Color(0.18f, 0.15f, 0.11f);
        static readonly Color k_RootLight = new Color(0.42f, 0.36f, 0.28f);
        static readonly Color k_Trunk = new Color(0.24f, 0.24f, 0.2f);
        static readonly Color k_TrunkDark = new Color(0.13f, 0.13f, 0.1f);
        static readonly Color k_Leaf = new Color(0.08f, 0.3f, 0.28f);
        static readonly Color k_LeafLight = new Color(0.16f, 0.46f, 0.4f);
        static readonly Color k_LeafDark = new Color(0.04f, 0.17f, 0.17f);
        static readonly Color k_Moss = new Color(0.45f, 0.56f, 0.42f);
        static readonly Color k_GlowSpot = new Color(0.55f, 0.8f, 1f);

        static readonly Color k_NeonBlue = new Color(0.2f, 0.55f, 1f) * 2.2f;

        // Variação de uma árvore do Charco (os padrões dão a árvore original)
        class Style
        {
            public int Seed;
            public float Crown = 1f;   // largura da copa
            public float Roots = 1f;   // altura e abertura das raízes (Mangue)
            public float Leaf = 1f;    // tamanho dos tufos
            public float Moss = 1f;    // barba-de-velho
            public float Height = 1f;  // altura do tronco
        }

        static readonly Style k_Mangrove = new Style { Seed = 401 };
        static readonly Style k_TallMangrove = new Style { Seed = 409, Crown = 0.8f, Roots = 1.35f, Height = 1.25f };
        static readonly Style k_Cypress = new Style { Seed = 421 };
        static readonly Style k_SparseCypress = new Style { Seed = 433, Leaf = 0.75f, Moss = 1.7f, Height = 0.9f };

        public static GridTextureKit.Set Build()
        {
            GridTextureKit.BeginSession();
            var set = new GridTextureKit.Set
            {
                Rock = GridTextureKit.Surface("Charco_Raizes", PaintRoots(), k_NeonBlue),
                RockTop = GridTextureKit.Surface("Charco_Raizes_Topo", PaintRootsTop(), Color.black),
                Perch = GridTextureKit.Surface("Charco_Toco", GridTextureKit.PaintStump(), GridTextureKit.k_NeonAmber),
                PerchTop = GridTextureKit.Surface("Charco_Toco_Topo", GridTextureKit.PaintRings(), Color.black),
                Grass = GridTextureKit.Surface("Charco_Lama", PaintMud(), Color.black),
                Path = GridTextureKit.Surface("Charco_Passarela", PaintBoardwalk(), Color.black),
                Door = GridTextureKit.Surface("Charco_Portao", GridTextureKit.PaintGate(), GridTextureKit.k_NeonCyan),
                WaterBed = GridTextureKit.Surface("Charco_Leito", PaintBed(), Color.black),
            };

            Paint water = PaintWater();
            set.WaterSurface = Water("Charco_Agua", water, new Color(0.45f, 0.75f, 0.85f, 0.55f), k_NeonBlue * 0.6f);
            set.DeepSurface = Water("Charco_Agua_Funda", water, new Color(0.12f, 0.3f, 0.4f, 0.88f), k_NeonBlue * 0.35f);

            set.Trees = new[]
            {
                GridTextureKit.Species("Mangue", 1f, 0.55f, k => PaintMangrove(k, k_Mangrove), k_NeonBlue, false, "Charco_Arvore_"),
                GridTextureKit.Species("MangueAlto", 1f, 0.5f, k => PaintMangrove(k, k_TallMangrove), k_NeonBlue, false, "Charco_Arvore_"),
                GridTextureKit.Species("Cipreste", 1f, 0.7f, k => PaintCypress(k, k_Cypress), Color.black, false, "Charco_Arvore_"),
                GridTextureKit.Species("CipresteRalo", 1f, 0.6f, k => PaintCypress(k, k_SparseCypress), Color.black, false, "Charco_Arvore_"),
                GridTextureKit.Species("LirioLanterna", 1f, 0.35f, PaintLilyLantern, k_NeonBlue, true, "Charco_Arvore_"),
            };

            Paint cattail = PaintCattail(), reed = GridTextureKit.PaintGlowReed(), fern = GridTextureKit.PaintGlowFern(),
                tuft = GridTextureKit.PaintTuft(), mushroom = GridTextureKit.PaintMushroom();
            Material cattailMaterial = GridTextureKit.Sprite("Charco_Taboa", cattail, Color.black);
            Material reedMaterial = GridTextureKit.Sprite("Charco_Junco", reed, Color.black);
            Material fernMaterial = GridTextureKit.Sprite("Charco_Samambaia", fern, Color.black);
            Material tuftMaterial = GridTextureKit.Sprite("Charco_Tufo", tuft, Color.black);
            set.MushroomCyan = GridTextureKit.Sprite("Charco_Cogumelo", mushroom, Color.black);
            set.MushroomAmber = set.MushroomCyan;
            set.MushroomSize = mushroom.Size / k_PixelsPerMeter;
            set.Decor.Add(GridTextureKit.Decor(cattail, new[] { cattailMaterial }, 0.09f, 0.7f, 1.1f));
            set.Decor.Add(GridTextureKit.Decor(reed, new[] { reedMaterial }, 0.08f, 0.7f, 1f));
            set.Decor.Add(GridTextureKit.Decor(fern, new[] { fernMaterial }, 0.06f, 0.6f, 0.95f));
            set.Decor.Add(GridTextureKit.Decor(tuft, new[] { tuftMaterial }, 0.14f, 0.6f, 0.95f));
            set.Decor.Add(GridTextureKit.Decor(mushroom, new[] { set.MushroomCyan }, 0.03f, 0.6f, 1.1f));
            set.Spores = GridTextureKit.SporeMaterial();
            return set;
        }

        // Água: material transparente com ondinhas e algas que brilham (a superfície corre pelo WaterRipple)
        static Material Water(string name, Paint paint, Color color, Color glow)
        {
            Material material = GridTextureKit.Surface(name, paint, glow);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- superfícies ----------

        static Paint PaintMud()
        {
            Paint p = GridTextureKit.Tile(32, 32, 501, k_Mud);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float roll = p.Next;
                Color c = x == 0 || y == 0 ? k_MudDark : roll < 0.2f ? k_MudDark : roll < 0.3f ? k_MudMoss : k_Mud;
                p.Set(x, y, p.Vary(c, 0.05f));
            }

            // Poça pequena
            int px = p.Range(6, 24), py = p.Range(6, 24);
            for (int y = -2; y <= 2; y++)
            for (int x = -3; x <= 3; x++)
            {
                if (x * x / 9f + y * y / 4f <= 1f)
                    p.Set(px + x, py + y, x + y < -1 ? k_Ripple * 0.7f : k_Puddle);
            }

            return p;
        }

        // Passarela: 4 tábuas por célula, frestas escuras, pregos e um pouco de musgo
        static Paint PaintBoardwalk()
        {
            Paint p = GridTextureKit.Tile(32, 32, 503, k_Wood);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool gap = y % 8 == 0;
                Color c = gap ? k_WoodDark : y % 8 == 1 ? k_WoodLight : k_Wood;
                if (!gap && (x + (y / 8) * 11) % 32 == 0)
                    c = k_WoodDark; // emenda da tábua
                if (!gap && p.Next < 0.06f)
                    c = k_MudMoss;
                p.Set(x, y, p.Vary(c, 0.06f));
            }

            for (int plank = 0; plank < 4; plank++)
            {
                p.Set(3, plank * 8 + 4, k_WoodDark * 0.6f);
                p.Set(28, plank * 8 + 4, k_WoodDark * 0.6f);
            }

            return p;
        }

        static Paint PaintBed()
        {
            Paint p = GridTextureKit.Tile(32, 32, 507, k_Silt);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, p.Vary(p.Next < 0.12f ? k_Pebble : k_Silt, 0.08f));
            return p;
        }

        // Água: ondinhas claras em traços e algas que brilham
        static Paint PaintWater()
        {
            Paint p = GridTextureKit.Tile(32, 32, 509, k_Water);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, p.Vary(k_Water, 0.04f));
            for (int i = 0; i < 9; i++)
            {
                int x0 = p.Range(0, 32), y0 = p.Range(0, 32), length = p.Range(3, 7);
                for (int s = 0; s < length; s++)
                    p.Set(x0 + s, y0, k_Ripple);
            }

            for (int i = 0; i < 4; i++)
                p.Set(p.Range(0, 32), p.Range(0, 32), k_GlowSpot, true);
            return p;
        }

        // Emaranhado de raízes (bloco de 2 m x 6 m): raízes tortas sobre fundo escuro, musgo e fungos azuis que brilham
        static Paint PaintRoots()
        {
            Paint p = GridTextureKit.Tile(32, 96, 511, new Color(0.06f, 0.08f, 0.07f));
            for (int i = 0; i < 22; i++)
            {
                var start = new Vector2(p.Range(0, 32), p.Range(-10, 96));
                Vector2 previous = start;
                float angle = (60f + p.Range(0, 60)) * Mathf.Deg2Rad;
                for (int s = 0; s < 6; s++)
                {
                    angle += (p.Next - 0.5f) * 0.9f;
                    Vector2 next = previous + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 7f;
                    p.Stroke(previous, next, 3.5f, 2.5f, p.Next < 0.3f ? k_RootLight : k_Root, k_RootDark);
                    previous = next;
                }
            }

            for (int i = 0; i < 18; i++)
                p.Set(p.Range(0, 32), p.Range(0, 96), k_MudMoss);
            for (int i = 0; i < 5; i++)
                p.Dot(p.Range(2, 30), p.Range(4, 92), k_GlowSpot, 1);
            return p;
        }

        static Paint PaintRootsTop()
        {
            Paint p = GridTextureKit.Tile(32, 32, 513, k_MudMoss);
            for (int i = 0; i < 140; i++)
                p.Set(p.Range(0, 32), p.Range(0, 32), p.Next < 0.5f ? k_Root : k_MudDark);
            return p;
        }

        // ---------- árvores ----------

        // Mangue (8 x 11 m na escala 1): raízes aéreas em arco segurando o tronco, copa larga e chata; pontos azuis que
        // brilham nas raízes
        static Paint PaintMangrove(float k, Style style)
        {
            Paint p = GridTextureKit.Transparent(GridTextureKit.Px(128 * k), GridTextureKit.Px(176 * k), style.Seed);
            float mid = p.W * 0.5f;
            float rootTop = 30f * k * style.Roots;
            float trunkTop = 112f * k * style.Height;

            // Raízes em arco, do tronco até o chão
            int roots = 7;
            for (int i = 0; i < roots; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float reach = (10f + i * 5f + p.Range(0, 6)) * k * style.Roots;
                var start = new Vector2(mid + side * 2f * k, rootTop - i * 1.5f * k);
                var end = new Vector2(mid + side * reach, 0f);
                var control = new Vector2(mid + side * reach * 0.55f, rootTop + 8f * k);
                Vector2 previous = start;
                for (int s = 1; s <= 8; s++)
                {
                    float t = s / 8f;
                    Vector2 point = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
                    p.Stroke(previous, point, 3.2f * k, 2.4f * k, k_Trunk, k_TrunkDark);
                    previous = point;
                }

                p.Dot(Mathf.RoundToInt(control.x), Mathf.RoundToInt(control.y * 0.8f), k_GlowSpot,
                    GridTextureKit.Px(1.2f * k));
            }

            p.Stroke(new Vector2(mid, rootTop - 4f * k), new Vector2(mid + 3f * k, trunkTop), 9f * k, 6f * k, k_Trunk,
                k_TrunkDark);

            // Galhos e copa larga e chata
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(mid + 3f * k, trunkTop - (10f + i * 8f) * k);
                Vector2 end = start + new Vector2(side * (22f + p.Range(0, 10)) * style.Crown, 14f) * k;
                p.Stroke(start, end, 3.5f * k, 2f * k, k_Trunk, k_TrunkDark);
            }

            int blobs = Mathf.RoundToInt(16 * style.Crown);
            for (int i = 0; i < blobs; i++)
            {
                float x = mid + (p.Next * 2f - 1f) * 48f * k * style.Crown;
                float y = trunkTop + (p.Next * 2f - 1f) * 10f * k;
                p.Blob(x, y, (10f + p.Range(0, 5)) * k * style.Leaf, k_Leaf, k_LeafLight, k_LeafDark);
            }

            return p;
        }

        // Cipreste do brejo (6 x 13 m na escala 1): base larga em aletas, tronco alto e fino, tufos achatados de
        // folhas alternados e muita barba-de-velho pendurada; sem brilho
        static Paint PaintCypress(float k, Style style)
        {
            Paint p = GridTextureKit.Transparent(GridTextureKit.Px(96 * k), GridTextureKit.Px(208 * k), style.Seed);
            float mid = p.W * 0.5f;
            float top = 190f * k * style.Height;
            for (int y = 0; y < top; y++)
            {
                float t = y / top;
                float half = t < 0.15f ? Mathf.Lerp(13f, 4.5f, t / 0.15f) * k : Mathf.Lerp(4.5f, 2f, (t - 0.15f) / 0.85f) * k;
                for (int x = Mathf.FloorToInt(mid - half); x <= Mathf.CeilToInt(mid + half); x++)
                {
                    Color c = x > mid + half - 2f ? k_TrunkDark : (x + y / 6) % 5 == 0 ? k_TrunkDark : k_Trunk;
                    p.Set(x, y, p.Vary(c, 0.04f));
                }
            }

            int clumps = 8;
            for (int i = 0; i < clumps; i++)
            {
                float y = Mathf.Lerp(70f, 180f, i / (clumps - 1f)) * k * style.Height;
                float side = i % 2 == 0 ? -1f : 1f;
                float x = mid + side * (8f + p.Range(0, 14)) * k;
                p.Stroke(new Vector2(mid, y - 6f * k), new Vector2(x, y), 2.5f * k, 1.5f * k, k_Trunk, k_TrunkDark);
                for (int b = 0; b < 3; b++)
                    p.Blob(x + (b - 1) * 7f * k, y + p.Range(-2, 3) * k, (6f + p.Range(0, 3)) * k * style.Leaf, k_Leaf,
                        k_LeafLight, k_LeafDark);

                // Barba-de-velho pendurada do tufo
                int strands = Mathf.RoundToInt(4 * style.Moss);
                for (int s = 0; s < strands; s++)
                {
                    int sx = Mathf.RoundToInt(x + (p.Next * 2f - 1f) * 12f * k);
                    int length = GridTextureKit.Px((10 + p.Range(0, 16)) * k * Mathf.Sqrt(style.Moss));
                    int start = Mathf.RoundToInt(y - 4f * k);
                    for (int d = 0; d < length; d++)
                        p.Set(sx + Mathf.RoundToInt(Mathf.Sin(d * 0.3f + s) * 0.6f), start - d, k_Moss);
                }
            }

            return p;
        }

        // Lírio-lanterna (4 x 8 m na escala 1, destaque): haste curva com folhas largas na base e um sino caído que
        // brilha em azul
        static Paint PaintLilyLantern(float k)
        {
            Paint p = GridTextureKit.Transparent(GridTextureKit.Px(64 * k), GridTextureKit.Px(128 * k), 441);
            float mid = p.W * 0.5f;
            Vector2 previous = new Vector2(mid, 0f);
            for (int s = 1; s <= 12; s++)
            {
                float t = s / 12f;
                var point = new Vector2(mid + Mathf.Sin(t * 2.4f) * 10f * k, t * 100f * k);
                p.Stroke(previous, point, 5f * k, 3.5f * k, k_LeafLight * 0.8f, k_LeafDark);
                previous = point;
            }

            // Folhas largas na base
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(mid, 4f * k);
                var end = new Vector2(mid + side * (16f + i * 3f) * k, (14f + i * 6f) * k);
                p.Stroke(start, end, 6f * k, 2f * k, k_Leaf, k_LeafDark);
            }

            // Sino caído que brilha
            Vector2 bell = previous + new Vector2(6f * k, -8f * k);
            float rx = 12f * k, ry = 15f * k;
            for (int y = -Mathf.CeilToInt(ry); y <= Mathf.CeilToInt(ry); y++)
            for (int x = -Mathf.CeilToInt(rx); x <= Mathf.CeilToInt(rx); x++)
            {
                float d = x * x / (rx * rx) + y * y / (ry * ry);
                if (d > 1f || y > ry * 0.6f && Mathf.Abs(x) < rx * 0.3f)
                    continue;
                bool rim = d > 0.7f;
                Color c = rim ? new Color(0.35f, 0.6f, 0.95f) : new Color(0.75f, 0.9f, 1f);
                p.Set(Mathf.RoundToInt(bell.x) + x, Mathf.RoundToInt(bell.y) + y, c, !rim || p.Next < 0.4f);
            }

            return p;
        }

        // ---------- plantas ----------

        // Taboa (1,5 x 2,5 m): hastes finas com espigas marrons cilíndricas
        static Paint PaintCattail()
        {
            Paint p = GridTextureKit.Transparent(24, 40, 521);
            Color stalk = new Color(0.16f, 0.36f, 0.3f), head = new Color(0.36f, 0.22f, 0.14f),
                headDark = new Color(0.22f, 0.13f, 0.08f);
            for (int i = 0; i < 6; i++)
            {
                int x0 = 3 + i * 3 + p.Range(0, 2), height = p.Range(24, 39);
                float lean = (p.Next - 0.5f) * 0.2f;
                for (int y = 0; y < height; y++)
                {
                    int x = x0 + Mathf.RoundToInt(lean * y);
                    bool spike = y >= height - 9 && y < height - 2;
                    p.Set(x, y, spike ? head : stalk);
                    if (spike)
                        p.Set(x + 1, y, headDark);
                }
            }

            return p;
        }
    }
}
