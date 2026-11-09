using UnityEngine;

namespace Icaro
{
    // Desenhos em pixel dos menus, gerados em código na primeira vez que são pedidos (filtro Point, brancos: a cor vem
    // da Image): a ponta em degraus das faixas, a seta triangular, a caixa de contorno (fatiada em 9), o painel de
    // vidro com cantos chanfrados (preenchimento e contorno, fatiados em 9) e o degradê escuro da esquerda da tela.
    public static class MenuArt
    {
        // Ponta da faixa: StripSteps degraus de StripStep px (cada linha um degrau mais comprida, de cima para baixo)
        public const int StripSteps = 10;
        public const float StripStep = 6f;
        public const float StripCapWidth = StripSteps * StripStep;

        // Painel de vidro: canto chanfrado (em px) e borda da fatia
        public const int GlassCut = 18;
        const int k_GlassSize = 64;
        const int k_GlassBorder = 22;

        static Sprite s_StripCap, s_Arrow, s_Outline, s_GlassFill, s_GlassLine;
        static Texture2D s_Shade;

        public static Sprite StripCap => s_StripCap ? s_StripCap : s_StripCap = Make("Menu_PontaFaixa", StripSteps,
            StripSteps, (x, y) => x <= StripSteps - 1 - y, Vector4.zero);

        // Seta para a direita: 4 colunas de 3 px, cada uma mais baixa (8 px de altura em 2 px por linha)
        public static Sprite Arrow => s_Arrow ? s_Arrow : s_Arrow = Make("Menu_Seta", 4, 8,
            (x, y) => Mathf.Abs(y - 3.5f) < 4 - x, Vector4.zero);

        // Caixa de contorno de 2 px (fatiada: estica sem engrossar a borda)
        public static Sprite Outline => s_Outline ? s_Outline : s_Outline = Make("Menu_Contorno", 8, 8,
            (x, y) => x < 2 || y < 2 || x > 5 || y > 5, new Vector4(2, 2, 2, 2));

        public static Sprite GlassFill => s_GlassFill ? s_GlassFill : s_GlassFill = Make("Menu_VidroFundo", k_GlassSize,
            k_GlassSize, InsideGlass, Vector4.one * k_GlassBorder);

        public static Sprite GlassLine => s_GlassLine ? s_GlassLine : s_GlassLine = Make("Menu_VidroContorno",
            k_GlassSize, k_GlassSize, (x, y) => InsideGlass(x, y) && GlassEdge(x, y) < 3f, Vector4.one * k_GlassBorder);

        // Degradê horizontal: alfa cheio na esquerda caindo até 0 na direita (curva ^1,6), filtro bilinear
        public static Texture2D Shade
        {
            get
            {
                if (s_Shade)
                    return s_Shade;
                const int w = 256;
                s_Shade = new Texture2D(w, 1, TextureFormat.RGBA32, false)
                {
                    name = "Menu_Degrade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[w];
                for (int x = 0; x < w; x++)
                    pixels[x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(1f - x / (w - 1f), 1.6f) * 255f));
                s_Shade.SetPixels32(pixels);
                s_Shade.Apply(false, true);
                return s_Shade;
            }
        }

        // Canto de cima à esquerda e de baixo à direita cortados (y da textura cresce para cima)
        static bool InsideGlass(int x, int y)
        {
            int top = k_GlassSize - 1 - y;
            int right = k_GlassSize - 1 - x;
            return x + top >= GlassCut && right + y >= GlassCut;
        }

        // Distância até a borda mais perto (lados e chanfros)
        static float GlassEdge(int x, int y)
        {
            int top = k_GlassSize - 1 - y;
            int right = k_GlassSize - 1 - x;
            float side = Mathf.Min(Mathf.Min(x, y), Mathf.Min(top, right));
            float chamferA = (x + top - GlassCut) / 1.4142f;
            float chamferB = (right + y - GlassCut) / 1.4142f;
            return Mathf.Min(side, Mathf.Min(chamferA, chamferB));
        }

        // Textura branca com os pixels de 'inside' acesos ((0, 0) embaixo à esquerda) e sprite com a borda de fatia
        static Sprite Make(string name, int w, int h, System.Func<int, int, bool> inside, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }
    }
}
