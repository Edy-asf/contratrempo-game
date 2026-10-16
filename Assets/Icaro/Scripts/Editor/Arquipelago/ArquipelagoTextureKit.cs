using UnityEditor;
using UnityEngine;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Texturas do Arquipélago (mesmas regras do GridTextureKit: pixel art gerada por código, 16 px/m, filtro Point;
    // brilho pelo mapa de emissão e cor no material). Noite verde-azulada:
    // - capim verde-água no topo das ilhas (com flores claras), penhasco de pedra em camadas com musgo;
    // - pedra de ruína em blocos (anéis, palácios, pontes, colunas) com runas ciano quase apagadas;
    // - mar verde-azulado com ondinhas (quase opaco); portão de energia;
    // - tufos de capim e flores (sprites).
    public static class ArquipelagoTextureKit
    {
        public class Set
        {
            public Material Grass, Cliff, Stone, StoneDark, Silhouette, Sea, Door;
            public Material Tuft, Flower;
            public Vector2 TuftSize, FlowerSize;
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
        static readonly Color k_Rune = new Color(0.55f, 1f, 0.95f);

        static readonly Color k_RuneGlow = new Color(0.2f, 1f, 0.9f) * 0.8f;

        public static Set Build()
        {
            GridTextureKit.BeginSession();
            Paint tuft = PaintTuft(), flower = PaintFlower();
            var set = new Set
            {
                Grass = GridTextureKit.Surface("Arquipelago_Capim", PaintGrass(), Color.black),
                Cliff = GridTextureKit.Surface("Arquipelago_Penhasco", PaintCliff(), Color.black),
                Stone = GridTextureKit.Surface("Arquipelago_Ruina", PaintRuin(false), k_RuneGlow),
                StoneDark = GridTextureKit.Surface("Arquipelago_Ruina_Escura", PaintRuin(true), k_RuneGlow * 0.5f),
                Sea = Sea(),
                Door = GridTextureKit.Surface("Arquipelago_Portao", GridTextureKit.PaintGate(), GridTextureKit.k_NeonCyan),
                Tuft = GridTextureKit.Sprite("Arquipelago_Tufo", tuft, Color.black),
                Flower = GridTextureKit.Sprite("Arquipelago_Flor", flower, Color.black),
                TuftSize = tuft.Size / GridTextureKit.PixelsPerMeter,
                FlowerSize = flower.Size / GridTextureKit.PixelsPerMeter,
                Spores = GridTextureKit.SporeMaterial(),
            };
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

        // Pedra de ruína: blocos aparelhados com juntas, musgo e, às vezes, uma runa que brilha fraco
        static Paint PaintRuin(bool dark)
        {
            Color stone = dark ? k_RuinDark * 1.1f : k_Ruin;
            Paint p = GridTextureKit.Tile(32, 32, dark ? 607 : 605, stone);
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

            // Runa simples em um dos blocos
            int rx = 4 + p.Range(0, 2) * 16, ry = 2 + p.Range(0, 3) * 8;
            int[,] glyph = { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 1, 0 }, { 1, 0, 1 } };
            for (int gy = 0; gy < 4; gy++)
            for (int gx = 0; gx < 3; gx++)
            {
                if (glyph[gy, gx] == 1)
                    p.Set(rx + gx * 2, ry + gy, k_Rune, true);
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
