using UnityEngine;
using UnityEditor;
using Paint = Icaro.EditorTools.GridTextureKit.Paint;

namespace Icaro.EditorTools
{
    // Tecnologia dos Picos (gótico por fora, máquina por dentro; pedido do usuário em 2026-11-07): a liga escura e lisa
    // das peças de tecnologia (anéis, garras, gaiolas, bobinas) e a luz lilás das janelas, bobinas e cristais. Pixel
    // art de 16 px/m como o resto
    public static partial class PicosTextureKit
    {
        static readonly Color k_Alloy = new Color(0.2f, 0.22f, 0.27f);
        static readonly Color k_Lilac = new Color(0.8f, 0.62f, 1f);

        static void BuildTech(Set set)
        {
            set.Alloy = GridTextureKit.Surface("Picos_Liga", PaintAlloy(1501), Color.black);
            // Metal de verdade: um pouco de reflexo e brilho especular (as outras superfícies ficam foscas)
            set.Alloy.SetFloat("_Metallic", 0.55f);
            set.Alloy.SetFloat("_Smoothness", 0.42f);
            set.Alloy.SetFloat("_SpecularHighlights", 1f);
            set.Alloy.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            set.Alloy.SetFloat("_EnvironmentReflections", 1f);
            set.Alloy.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(set.Alloy);
            set.Glow = GridTextureKit.Surface("Picos_Luz_Lilas", PaintLight(k_Lilac), k_Lilac * 2f);

            // Naves (pedido do usuário em 2026-11-08: nave não é de pedra nem gótica): casco claro de painéis lisos,
            // com um pouco de reflexo, e o vidro escuro e polido da cabine
            set.Hull = GridTextureKit.Surface("Picos_Casco", PaintHull(1521), Color.black);
            Shine(set.Hull, 0.3f, 0.5f);
            set.ShipGlass = GridTextureKit.Surface("Picos_Vidro_Nave", PaintShipGlass(1531), Color.black);
            Shine(set.ShipGlass, 0.2f, 0.88f);
        }

        // Kit gótico das Vilas do Alto: os pintores ficam em PicosGothicTextures (sem UnityEditor, para a prévia fora
        // do Unity compilar o mesmo arquivo)
        static void BuildGothic(Set set)
        {
            set.Steel = GridTextureKit.Surface("Picos_Liga_Clara", PaintGothSteel(2131), Color.black);
            Shine(set.Steel, 0.7f, 0.55f);
            set.Engraved = GridTextureKit.Surface("Picos_Liga_Lavrada", PaintGothEngraved(2132), Color.black);
            Shine(set.Engraved, 0.55f, 0.42f);
            set.StoneSocle = GridTextureKit.Surface("Picos_Pedra_Soco", PaintGothPlinth(2114), Color.black);
            set.StoneColumn = GridTextureKit.Surface("Picos_Pedra_Coluna", PaintGothColumn(2112), Color.black);
            Shine(set.StoneColumn, 0f, 0.35f);
            set.StoneCarving = GridTextureKit.Surface("Picos_Pedra_Escultura", PaintGothCarving(2113), Color.black);
            set.StoneFurniture = GridTextureKit.Surface("Picos_Pedra_Movel", PaintGothFurniture(2115), Color.black);
            set.StoneMolding = GridTextureKit.Surface("Picos_Pedra_Moldura", PaintGothMolding(2111), Color.black);
            // Paredes de tijolo de pedra, na ordem de PicosGothicKit.WallKind: comum, alternado, monumental, gasto
            set.GothWalls = new[]
            {
                GridTextureKit.Surface("Picos_Parede_Tijolo", PaintGothBrick(2141), Color.black),
                GridTextureKit.Surface("Picos_Parede_Tijolo_Alternado", PaintGothBrickAlt(2142), Color.black),
                GridTextureKit.Surface("Picos_Parede_Tijolo_Monumental", PaintGothBrickGrand(2143), Color.black),
                GridTextureKit.Surface("Picos_Parede_Tijolo_Gasto", PaintGothBrickWorn(2144), Color.black),
            };
            set.GothScales = new[]
            {
                GridTextureKit.Surface("Picos_Escamas_Cinza", PaintGothScales(2121, k_GothScales[0]), Color.black),
                GridTextureKit.Surface("Picos_Escamas_Ardosia", PaintGothScales(2122, k_GothScales[1]), Color.black),
                GridTextureKit.Surface("Picos_Escamas_Violeta", PaintGothScales(2123, k_GothScales[2]), Color.black),
            };

            // Desgaste e fuligem (Icaro/WeatheredStone): sujeira do chão, escorridos, pedra gasta e fuligem por material;
            // o Bairro Voador abandonado (tijolo gasto) fica bem mais sujo
            Weathered(set.GothWalls[0], 0.5f, 0.35f, 0.3f, 0.2f);
            Weathered(set.GothWalls[1], 0.5f, 0.35f, 0.3f, 0.2f);
            Weathered(set.GothWalls[2], 0.4f, 0.4f, 0.2f, 0.15f);
            Weathered(set.GothWalls[3], 0.65f, 0.5f, 0.5f, 0.35f);
            Weathered(set.StoneSocle, 0.6f, 0.2f, 0.3f, 0.15f);
            Weathered(set.StoneColumn, 0.35f, 0.15f, 0.15f, 0.1f);
            Weathered(set.StoneCarving, 0.3f, 0.4f, 0.3f, 0.12f);
            Weathered(set.StoneMolding, 0.3f, 0.3f, 0.2f, 0.15f);
            Weathered(set.StoneFurniture, 0.4f, 0f, 0.2f, 0.1f, 0.45f);

            // Estragos do ataque (kit AR_Ataque): a pedra queimada tem desgaste e fuligem forte; vidro fundido e cristal
            // apagado com algum reflexo
            set.CharWood = GridTextureKit.Surface("Picos_Madeira_Queimada", PaintCharWood(2151), Color.black);
            set.BurntStone = GridTextureKit.Surface("Picos_Pedra_Queimada", PaintBurntStone(2152), Color.black);
            Weathered(set.BurntStone, 0.3f, 0.3f, 0.4f, 0.55f);
            set.Scorch = GridTextureKit.Surface("Picos_Chao_Queimado", PaintScorch(2153), Color.black);
            set.FusedGlass = GridTextureKit.Surface("Picos_Vidro_Fundido", PaintFusedGlass(2154), Color.black);
            Shine(set.FusedGlass, 0.1f, 0.8f);
            set.DeadCrystal = GridTextureKit.Surface("Picos_Cristal_Apagado", PaintDeadCrystal(2155), Color.black);
            Shine(set.DeadCrystal, 0.05f, 0.7f);
            set.Carbon = GridTextureKit.Surface("Picos_Carbonizado", PaintCarbon(2156), Color.black);
        }

        // Troca o Lit pelo shader com desgaste e fuligem (as propriedades da textura, da lisura e do metálico têm os mesmos
        // nomes) e regrava os ajustes a cada geração
        static void Weathered(Material m, float grime, float streaks, float wear, float soot, float grimeHeight = 2.2f)
        {
            Shader shader = Shader.Find("Icaro/WeatheredStone");
            if (!shader)
            {
                Debug.LogWarning("[Ícaro] Falta o shader Icaro/WeatheredStone: a pedra fica sem desgaste.");
                return;
            }
            if (m.shader != shader)
                m.shader = shader;
            m.SetFloat("_TexelsPerMeter", 16f);
            m.SetFloat("_GrimeAmount", grime);
            m.SetFloat("_GrimeHeight", grimeHeight);
            m.SetFloat("_StreakAmount", streaks);
            m.SetFloat("_WearAmount", wear);
            m.SetFloat("_SootAmount", soot);
            m.SetColor("_GrimeColor", new Color(0.36f, 0.33f, 0.31f));
            m.SetColor("_SootColor", new Color(0.16f, 0.14f, 0.14f));
            m.SetFloat("_SootStrength", 0.75f);
            m.SetFloat("_StreakDensity", 0.18f);
            EditorUtility.SetDirty(m);
        }

        static void Shine(Material m, float metallic, float smoothness)
        {
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_SpecularHighlights", 1f);
            m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 1f);
            m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
        }

        static readonly Color k_Hull = new Color(0.66f, 0.68f, 0.72f);
        static readonly Color k_ShipGlass = new Color(0.04f, 0.06f, 0.09f);

        // Casco (64 px = 4 m): placas grandes de 2 x 2 m num tom quase igual, com a junta fina e escura, o chanfro claro
        // em cima, uma linha de rebites rentes e, numa placa ou outra, uma faixa estreita mais escura (marca de serviço)
        static Paint PaintHull(int seed)
        {
            const int size = 64, pw = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Hull);
            var layout = new System.Random(seed + 5);
            for (int row = 0; row < size / pw; row++)
            for (int col = 0; col < size / pw; col++)
            {
                float tone = 0.96f + (float)layout.NextDouble() * 0.08f;
                bool mark = layout.NextDouble() < 0.3;
                int x0 = col * pw, y0 = row * pw;
                for (int y = 0; y < pw; y++)
                for (int i = 0; i < pw; i++)
                {
                    Color c = k_Hull * tone;
                    if (i == 0 || y == 0)
                        c = k_Hull * 0.5f;                  // junta
                    else if (y == pw - 1)
                        c = k_Hull * tone * 1.12f;          // chanfro de cima
                    else if (i == pw - 1 || y == 1)
                        c = k_Hull * tone * 0.86f;
                    else if (mark && y >= 6 && y <= 8)
                        c = k_Hull * tone * 0.72f;          // faixa de serviço
                    else if ((i == 3 || i == pw - 4) && y % 4 == 2)
                        c = k_Hull * tone * 0.8f;           // rebites rentes
                    p.Set(x0 + i, y0 + y, p.Vary(c, 0.01f));
                }
            }

            return p;
        }

        // Vidro da nave (16 px): escuro e quase liso, com um reflexo claro em diagonal
        static Paint PaintShipGlass(int seed)
        {
            Paint p = GridTextureKit.Tile(16, 16, seed, k_ShipGlass);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Color c = k_ShipGlass;
                int d = (x + y) % 16;
                if (d == 4 || d == 5)
                    c = Color.Lerp(k_ShipGlass, new Color(0.35f, 0.4f, 0.5f), 0.4f);
                p.Set(x, y, p.Vary(c, 0.01f));
            }

            return p;
        }

        // Liga (64 px = 4 m): placas de 1 x 2 m em fiadas desencontradas, cada uma num tom um pouco diferente, com a
        // junta escura de 1 px, a beira de cima e da esquerda clara (chanfro pegando luz), parafusos rentes nos cantos,
        // riscos finos de uso ao comprido e um pouco de geada nas juntas de cima
        static Paint PaintAlloy(int seed)
        {
            const int size = 64, pw = 16, ph = 32;
            Paint p = GridTextureKit.Tile(size, size, seed, k_Alloy);
            var layout = new System.Random(seed + 3);
            for (int row = 0; row < size / ph; row++)
            {
                int offset = row % 2 == 0 ? 0 : pw / 2;
                for (int col = 0; col < size / pw; col++)
                {
                    float tone = 0.9f + (float)layout.NextDouble() * 0.2f;
                    int x0 = col * pw + offset, y0 = row * ph;
                    for (int y = 0; y < ph; y++)
                    for (int i = 0; i < pw; i++)
                    {
                        int x = (x0 + i) % size, yy = y0 + y;
                        Color c = k_Alloy * tone;
                        if (i == 0 || y == 0)
                            c = k_Alloy * 0.55f;                                     // junta
                        else if (y == ph - 1)
                            c = Color.Lerp(k_Alloy * tone * 1.45f, k_Frost, 0.15f);  // chanfro de cima pegando luz
                        else if (i == 1)
                            c = k_Alloy * tone * 1.22f;                              // chanfro da esquerda
                        else if (i == pw - 1 || y == 1)
                            c = k_Alloy * tone * 0.8f;                               // sombra do chanfro
                        p.Set(x, yy, p.Vary(c, 0.02f));
                    }

                    // Parafusos rentes nos quatro cantos
                    foreach ((int bx, int by) in new[] { (3, 4), (pw - 3, 4), (3, ph - 4), (pw - 3, ph - 4) })
                    {
                        int x = (x0 + bx) % size, y = y0 + by;
                        p.Set(x, y, k_Alloy * 1.5f);
                        p.Set((x + 1) % size, y - 1, k_Alloy * 0.6f);
                    }

                    // Riscos de uso, finos e ao comprido
                    int scratches = layout.Next(0, 3);
                    for (int k = 0; k < scratches; k++)
                    {
                        int sx = x0 + 3 + layout.Next(0, pw - 6), sy = y0 + 4 + layout.Next(0, ph - 14), len = 4 + layout.Next(0, 8);
                        for (int j = 0; j < len; j++)
                            p.Set(sx % size, sy + j, k_Alloy * tone * 1.3f);
                    }

                    // Geada encostada na junta de cima de algumas placas
                    if (layout.NextDouble() < 0.4)
                        for (int i = 2; i < pw - 1; i++)
                            if (layout.NextDouble() < 0.45)
                                p.Set((x0 + i) % size, y0 + ph - 2, Color.Lerp(k_Alloy, k_Frost, 0.55f));
                }
            }

            return p;
        }

        // Luz (16 px): quase lisa, com o miolo mais claro e a beira um pouco mais escura; brilha inteira. Lilás nas
        // janelas, bobinas e cristais das peças de tecnologia (pedido do usuário em 2026-11-07)
        static Paint PaintLight(Color light)
        {
            Paint p = GridTextureKit.Tile(16, 16, 1511, light);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Mathf.Max(Mathf.Abs(x - 7.5f), Mathf.Abs(y - 7.5f)) / 8f;
                Color c = Color.Lerp(Color.Lerp(light, Color.white, 0.45f), light * 0.85f, d);
                p.Set(x, y, p.Vary(c, 0.015f), true);
            }

            return p;
        }
    }
}
