using UnityEngine;
using static Icaro.EditorTools.GridLobe;

namespace Icaro.EditorTools
{
    // Temas prontos dos biomas em grade (GridBiomeBuilder): Bosque (aprovado) e Charco.
    public static class GridThemes
    {
        // Bosque alienígena: 5 salas de formatos variados e a do chefe, noite roxa, floresta índigo e verde-azulada
        public static GridTheme Bosque() => new GridTheme
        {
            Biome = "Bosque",
            BiomeIndex = 0,
            SceneName = "Bosque_Grade",
            Materials = GridTextureKit.BuildBosque(),
            Arenas = new[]
            {
                new GridArena { Name = "Bosque 1", Number = 1, Lobes = new[] { L(0, 0, 15, 14) }, Noise = 0.08f,
                    Phase = 0.3f, Rocks = 9, Trees = 10, Perches = 3, OffsetX = 0, Corridor = 12 },
                new GridArena { Name = "Bosque 2", Number = 2, Lobes = new[] { L(0, 0, 22, 12) }, Noise = 0.07f,
                    Phase = 1.7f, Rocks = 12, Trees = 14, Perches = 4, OffsetX = 10, Corridor = 10 },
                new GridArena { Name = "Bosque 3", Number = 3, Lobes = new[] { L(-7, 0, 13, 15), L(7, 4, 12, 12) },
                    Noise = 0.06f, Phase = 2.9f, Rocks = 12, Trees = 16, Perches = 4, OffsetX = -8, Corridor = 14 },
                new GridArena { Name = "Bosque 4", Number = 4, Lobes = new[] { L(0, 0, 22, 20) }, Noise = 0.08f,
                    Phase = 4.1f, Rocks = 18, Trees = 18, Perches = 6, OffsetX = 6, Corridor = 10 },
                new GridArena { Name = "Bosque 5", Number = 5, Lobes = new[] { L(-9, -7, 13, 12), L(9, 7, 13, 12) },
                    Noise = 0.05f, Phase = 5.3f, Rocks = 12, Trees = 14, Perches = 5, OffsetX = -10, Corridor = 12 },
                new GridArena { Name = "Chefe do Bosque", Number = 6, Boss = true, Lobes = new[] { L(0, 0, 30, 28) },
                    Noise = 0.03f, Phase = 0.8f, Rocks = 0, Trees = 0, Perches = 0, OffsetX = 0, Corridor = 14 },
            },
        };

        // Charco alienígena (teste): uma sala com rio raso atravessando e duas lagoas, para aprovar o visual
        public static GridTheme CharcoTeste()
        {
            GridTheme theme = Charco();
            theme.SceneName = "Charco_Teste";
            theme.Arenas = new[]
            {
                new GridArena { Name = "Charco 1", Number = 1, Lobes = new[] { L(0, 0, 22, 18) }, Noise = 0.07f,
                    Phase = 1.1f, Rocks = 7, Trees = 9, Perches = 4, OffsetX = 0, Corridor = 12, River = true, Ponds = 2 },
            };
            return theme;
        }

        // Charco: pântano à noite. Lama verde-escura, passarelas de madeira, água rasa com algas que brilham, lagoas
        // fundas, mangues e ciprestes com barba-de-velho; névoa azul-esverdeada densa; céu com planeta azul.
        static GridTheme Charco() => new GridTheme
        {
            Biome = "Charco",
            BiomeIndex = 1,
            SceneName = "Charco_Grade",
            Materials = CharcoTextureKit.Build(),
            FogColor = new Color(0.06f, 0.15f, 0.17f),
            FogDensity = 0.018f,
            AmbientSky = new Color(0.78f, 0.95f, 1.15f),
            AmbientEquator = new Color(0.6f, 0.78f, 0.85f),
            AmbientGround = new Color(0.35f, 0.42f, 0.38f),
            MoonColor = new Color(0.65f, 0.85f, 1f),
            MoonIntensity = 1.3f,
            SkyBlue = new Color(0.06f, 0.3f, 0.42f),
            SkyPurple = new Color(0.06f, 0.1f, 0.3f),
            SkyBlueHeight = 0.2f,
            SkyTop = 0.5f,
            PlanetDirection = new Vector4(0.6f, 0.22f, 0.75f, 0f),
            PlanetSize = 0.12f,
            PlanetA = new Color(0.2f, 0.5f, 0.62f),
            PlanetB = new Color(0.1f, 0.25f, 0.45f),
            Ring = new Color(0.6f, 0.85f, 0.9f),
            SporeColors = new[]
            {
                new Color(0.55f, 1f, 0.35f), new Color(1f, 0.9f, 0.35f), new Color(0.25f, 0.9f, 1f),
            },
            ShallowDepth = 0.25f,
            DeepDepth = 1.2f,
            WaterSlow = 0.6f,
        };
    }
}
