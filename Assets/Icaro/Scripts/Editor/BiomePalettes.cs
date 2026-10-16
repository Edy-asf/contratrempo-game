using UnityEngine;

namespace Icaro.EditorTools
{
    // Paleta e atmosfera de cada bioma (seção "Estilo visual" do CLAUDE.md). Os geradores leem daqui
    // as cores dos materiais, a névoa, o ambiente e a luz direcional. Só o Bosque está em uso por enquanto.
    public static class BiomePalettes
    {
        public class Palette
        {
            public string Name;

            // Névoa (ExponentialSquared)
            public Color FogColor;
            public float FogDensity;

            // Ambiente (trilight)
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;

            // Luz direcional
            public Color SunColor;
            public float SunIntensity;
            public Vector3 SunRotation;

            // Céu (HDRI)
            public Color SkyTint;
            public float SkyExposure;

            // Bioluminescência (HDR)
            public Color AccentCyan;
            public Color AccentAmber;

            // Superfícies
            public Color Floor;
            public Color Wall;
            public Color Cover;
            public Color Stone;
            public Color Wood;
            public Color Foliage;

            // Smoothness das superfícies: alto = aparência molhada
            public float Wetness;

            // Cor de chão da sala 'index': cada sala é um pouco mais escura que a anterior
            public Color FloorForRoom(int index) => Floor * (1f - 0.08f * index);
        }

        // Valores calibrados para ACES: superfícies escuras mas legíveis (0,25 a 0,45), ambiente médio e
        // névoa de cor média. O clima escuro vem da luz baixa e da névoa, não de superfícies pretas.

        // Bosque: verde-azulado com âmbar
        public static readonly Palette Bosque = new Palette
        {
            Name = "Bosque",
            FogColor = new Color(0.16f, 0.27f, 0.29f),
            FogDensity = 0.028f,
            AmbientSky = new Color(0.38f, 0.52f, 0.58f),
            AmbientEquator = new Color(0.20f, 0.28f, 0.30f),
            AmbientGround = new Color(0.08f, 0.10f, 0.10f),
            SunColor = new Color(0.62f, 0.74f, 0.92f),
            SunIntensity = 0.9f,
            SunRotation = new Vector3(35f, -35f, 0f),
            SkyTint = new Color(0.45f, 0.58f, 0.58f),
            SkyExposure = 0.6f,
            AccentCyan = new Color(0.1f, 1.6f, 1.9f),
            AccentAmber = new Color(2.2f, 1.1f, 0.25f),
            Floor = new Color(0.26f, 0.38f, 0.34f),
            Wall = new Color(0.28f, 0.33f, 0.36f),
            Cover = new Color(0.30f, 0.40f, 0.34f),
            Stone = new Color(0.38f, 0.42f, 0.45f),
            Wood = new Color(0.32f, 0.24f, 0.18f),
            Foliage = new Color(0.16f, 0.36f, 0.32f),
            Wetness = 0.5f,
        };

        // Charco: verde escuro e névoa baixa
        public static readonly Palette Charco = new Palette
        {
            Name = "Charco",
            FogColor = new Color(0.15f, 0.22f, 0.15f),
            FogDensity = 0.034f,
            AmbientSky = new Color(0.30f, 0.42f, 0.32f),
            AmbientEquator = new Color(0.16f, 0.24f, 0.16f),
            AmbientGround = new Color(0.07f, 0.09f, 0.07f),
            SunColor = new Color(0.58f, 0.72f, 0.66f),
            SunIntensity = 0.7f,
            SunRotation = new Vector3(30f, -60f, 0f),
            SkyTint = new Color(0.38f, 0.48f, 0.40f),
            SkyExposure = 0.5f,
            AccentCyan = new Color(0.1f, 1.4f, 1.5f),
            AccentAmber = new Color(1.6f, 1.4f, 0.2f),
            Floor = new Color(0.20f, 0.30f, 0.20f),
            Wall = new Color(0.24f, 0.30f, 0.24f),
            Cover = new Color(0.26f, 0.36f, 0.24f),
            Stone = new Color(0.32f, 0.37f, 0.32f),
            Wood = new Color(0.28f, 0.22f, 0.15f),
            Foliage = new Color(0.14f, 0.32f, 0.16f),
            Wetness = 0.7f,
        };

        // Ruínas: pedra cinza-azulada com runas ciano
        public static readonly Palette Ruinas = new Palette
        {
            Name = "Ruínas",
            FogColor = new Color(0.18f, 0.21f, 0.26f),
            FogDensity = 0.022f,
            AmbientSky = new Color(0.40f, 0.46f, 0.56f),
            AmbientEquator = new Color(0.22f, 0.25f, 0.30f),
            AmbientGround = new Color(0.09f, 0.09f, 0.11f),
            SunColor = new Color(0.66f, 0.76f, 0.95f),
            SunIntensity = 1f,
            SunRotation = new Vector3(38f, 20f, 0f),
            SkyTint = new Color(0.48f, 0.53f, 0.62f),
            SkyExposure = 0.6f,
            AccentCyan = new Color(0.1f, 1.8f, 2.2f),
            AccentAmber = new Color(1.8f, 0.9f, 0.2f),
            Floor = new Color(0.36f, 0.39f, 0.44f),
            Wall = new Color(0.42f, 0.46f, 0.52f),
            Cover = new Color(0.38f, 0.42f, 0.48f),
            Stone = new Color(0.45f, 0.49f, 0.55f),
            Wood = new Color(0.30f, 0.24f, 0.20f),
            Foliage = new Color(0.16f, 0.30f, 0.30f),
            Wetness = 0.35f,
        };

        // Cratera: tons quentes e rocha queimada
        public static readonly Palette Cratera = new Palette
        {
            Name = "Cratera",
            FogColor = new Color(0.30f, 0.16f, 0.10f),
            FogDensity = 0.022f,
            AmbientSky = new Color(0.55f, 0.34f, 0.24f),
            AmbientEquator = new Color(0.28f, 0.17f, 0.11f),
            AmbientGround = new Color(0.10f, 0.06f, 0.04f),
            SunColor = new Color(1f, 0.62f, 0.42f),
            SunIntensity = 1.1f,
            SunRotation = new Vector3(28f, 120f, 0f),
            SkyTint = new Color(0.62f, 0.45f, 0.38f),
            SkyExposure = 0.65f,
            AccentCyan = new Color(0.2f, 1.2f, 1.5f),
            AccentAmber = new Color(2.6f, 0.9f, 0.15f),
            Floor = new Color(0.26f, 0.18f, 0.14f),
            Wall = new Color(0.32f, 0.23f, 0.19f),
            Cover = new Color(0.34f, 0.24f, 0.18f),
            Stone = new Color(0.30f, 0.22f, 0.19f),
            Wood = new Color(0.22f, 0.14f, 0.10f),
            Foliage = new Color(0.26f, 0.18f, 0.11f),
            Wetness = 0.2f,
        };

        // Bioma em uso pelo gerador atual (os geradores trocam antes de montar)
        public static Palette Current = Bosque;
    }
}
