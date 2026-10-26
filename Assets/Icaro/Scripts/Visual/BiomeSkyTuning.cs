using UnityEngine;

namespace Icaro
{
    // Ajustes do céu de um bioma (cores e claridade do céu, planeta e brilho do horizonte), escritos no material do
    // céu dele (shader Icaro/NightSky) pelo SkySettings: ficam no material para a transição de céu do desfiladeiro
    // (AtmosphereCorridor) continuar misturando um bioma no outro. Também a claridade do mapa no bioma (luz ambiente,
    // luar e névoa), que o BiomeZone e o AtmosphereCorridor multiplicam nos valores do bioma.
    [System.Serializable]
    public class BiomeSkyTuning
    {
        [Header("Cores e claridade do céu")]
        [Tooltip("Claridade do céu inteiro (estrelas, aurora e planeta incluídos)")]
        [Range(0f, 4f)] public float SkyBrightness = 1f;

        [Tooltip("Cor logo acima do horizonte")]
        public Color SkyHorizon = new Color(0.12f, 0.28f, 0.62f);

        [Tooltip("Cor do meio do céu")]
        public Color SkyMiddle = new Color(0.24f, 0.07f, 0.38f);

        [Tooltip("Cor do alto do céu (zênite)")]
        public Color SkyTop = new Color(0.01f, 0.01f, 0.035f);

        [Tooltip("Altura em que a cor do horizonte vira a do meio")]
        [Range(0.05f, 0.9f)] public float GradientLow = 0.22f;

        [Tooltip("Altura em que a cor do meio vira a do alto")]
        [Range(0.1f, 1.2f)] public float GradientHigh = 0.55f;

        [Tooltip("Altura da faixa de névoa no horizonte do céu")]
        [Range(0.01f, 0.6f)] public float HorizonHaze = 0.2f;

        [Header("Claridade do mapa")]
        [Tooltip("Luz ambiente do mapa (multiplica a do bioma)")]
        [Range(0f, 4f)] public float AmbientBrightness = 1f;

        [Tooltip("Luar (multiplica o do bioma)")]
        [Range(0f, 4f)] public float MoonBrightness = 1f;

        [Tooltip("Cor da névoa mais clara ou mais escura (multiplica a do bioma)")]
        [Range(0f, 4f)] public float FogBrightness = 1f;

        [Tooltip("Densidade da névoa (multiplica a do bioma; menor = enxerga mais longe)")]
        [Range(0f, 4f)] public float FogDensity = 1f;

        // As cores do céu começam com as do material (lidas na primeira vez)
        [SerializeField, HideInInspector] bool m_Captured;

        [Header("Planeta")]
        public bool Planet = true;

        [Tooltip("Tamanho do planeta no céu (graus de raio)")]
        [Range(1f, 75f)] public float PlanetSize = 10f;

        [Tooltip("Direção do planeta (graus: 0 = norte, 90 = leste, 180 = sul, 270 = oeste)")]
        [Range(0f, 360f)] public float PlanetAzimuth = 325f;

        [Tooltip("Altura do planeta acima do horizonte (graus; negativo = nascendo atrás do horizonte)")]
        [Range(-30f, 89f)] public float PlanetElevation = 17f;

        public Color PlanetColorA = new Color(0.55f, 0.32f, 0.72f);
        public Color PlanetColorB = new Color(0.2f, 0.55f, 0.62f);
        public Color RingColor = new Color(0.75f, 0.68f, 0.9f);

        [Header("Brilho do horizonte")]
        [Tooltip("Força do degradê de luz que sobe do horizonte (0 = sem)")]
        [Range(0f, 4f)] public float GlowStrength = 0.4f;

        [Tooltip("Até que altura o brilho sobe (0 = rente ao horizonte, 1 = até o alto)")]
        [Range(0.01f, 1f)] public float GlowHeight = 0.3f;

        [Tooltip("Suavidade: maior = o brilho cai mais rápido subindo")]
        [Range(0.3f, 8f)] public float GlowFalloff = 2f;

        [Tooltip("Usar as cores da aurora do bioma (senão, as duas cores abaixo)")]
        public bool GlowUseAuroraColors;

        [ColorUsage(false, true)] public Color GlowColorLow = new Color(0.45f, 0.25f, 0.75f);
        [ColorUsage(false, true)] public Color GlowColorHigh = new Color(0.15f, 0.35f, 0.55f);

        [Tooltip("Direção em que o brilho se concentra (graus: 0 = norte)")]
        [Range(0f, 360f)] public float GlowAzimuth = 0f;

        [Tooltip("0 = brilho em volta do horizonte inteiro; 1 = só em volta da direção")]
        [Range(0f, 1f)] public float GlowDirectional = 0f;

        [Tooltip("Largura do brilho em volta da direção (graus)")]
        [Range(5f, 180f)] public float GlowWidth = 70f;

        [Tooltip("Quanto o brilho aparece por cima da névoa do horizonte")]
        [Range(0f, 1f)] public float GlowOverFog = 0.7f;

        // Padrões de cada bioma (as cores do céu são as dos temas da geração)
        public static BiomeSkyTuning BosqueDefaults() => new BiomeSkyTuning { m_Captured = true };

        public static BiomeSkyTuning ArquipelagoDefaults() => new BiomeSkyTuning
        {
            m_Captured = true,
            SkyHorizon = new Color(0.05f, 0.28f, 0.26f), SkyMiddle = new Color(0.03f, 0.12f, 0.16f),
            GradientLow = 0.25f, GradientHigh = 0.65f,
            PlanetSize = 22f, PlanetAzimuth = 0f, PlanetElevation = 14f,
            PlanetColorA = new Color(0.16f, 0.48f, 0.5f), PlanetColorB = new Color(0.08f, 0.22f, 0.36f),
            RingColor = new Color(0.55f, 0.82f, 0.85f),
            GlowStrength = 0.6f, GlowHeight = 0.35f, GlowFalloff = 2f, GlowUseAuroraColors = true,
            GlowAzimuth = 0f, GlowDirectional = 0.35f, GlowWidth = 90f, GlowOverFog = 0.7f,
        };

        static readonly int k_SkyBrightness = Shader.PropertyToID("_SkyBrightness");
        static readonly int k_Horizon = Shader.PropertyToID("_HorizonTint");
        static readonly int k_Middle = Shader.PropertyToID("_MidColor");
        static readonly int k_Top = Shader.PropertyToID("_TopColor");
        static readonly int k_GradientLow = Shader.PropertyToID("_GradientBlue");
        static readonly int k_GradientHigh = Shader.PropertyToID("_GradientTop");
        static readonly int k_HorizonHaze = Shader.PropertyToID("_HorizonFog");
        static readonly int k_PlanetSize = Shader.PropertyToID("_PlanetSize");
        static readonly int k_PlanetDir = Shader.PropertyToID("_PlanetDir");
        static readonly int k_PlanetA = Shader.PropertyToID("_PlanetColorA");
        static readonly int k_PlanetB = Shader.PropertyToID("_PlanetColorB");
        static readonly int k_Ring = Shader.PropertyToID("_RingColor");
        static readonly int k_AuroraA = Shader.PropertyToID("_AuroraColorA");
        static readonly int k_AuroraB = Shader.PropertyToID("_AuroraColorB");
        static readonly int k_GlowStrength = Shader.PropertyToID("_GlowStrength");
        static readonly int k_GlowHeight = Shader.PropertyToID("_GlowHeight");
        static readonly int k_GlowFalloff = Shader.PropertyToID("_GlowFalloff");
        static readonly int k_GlowLow = Shader.PropertyToID("_GlowColorLow");
        static readonly int k_GlowHigh = Shader.PropertyToID("_GlowColorHigh");
        static readonly int k_GlowAzimuth = Shader.PropertyToID("_GlowAzimuth");
        static readonly int k_GlowDirectional = Shader.PropertyToID("_GlowDirectional");
        static readonly int k_GlowWidth = Shader.PropertyToID("_GlowWidth");
        static readonly int k_GlowOverFog = Shader.PropertyToID("_GlowOverFog");

        public void ApplyTo(Material sky)
        {
            if (!sky || !sky.HasProperty(k_GlowStrength))
                return;
            if (!m_Captured)
            {
                m_Captured = true;
                SkyHorizon = sky.GetColor(k_Horizon);
                SkyMiddle = sky.GetColor(k_Middle);
                SkyTop = sky.GetColor(k_Top);
                GradientLow = sky.GetFloat(k_GradientLow);
                GradientHigh = sky.GetFloat(k_GradientHigh);
                HorizonHaze = sky.GetFloat(k_HorizonHaze);
            }

            sky.SetFloat(k_SkyBrightness, SkyBrightness);
            sky.SetColor(k_Horizon, SkyHorizon);
            sky.SetColor(k_Middle, SkyMiddle);
            sky.SetColor(k_Top, SkyTop);
            sky.SetFloat(k_GradientLow, GradientLow);
            sky.SetFloat(k_GradientHigh, GradientHigh);
            sky.SetFloat(k_HorizonHaze, HorizonHaze);
            sky.SetFloat(k_PlanetSize, Planet ? PlanetSize * Mathf.Deg2Rad : 0f);
            float azimuth = PlanetAzimuth * Mathf.Deg2Rad, elevation = PlanetElevation * Mathf.Deg2Rad;
            sky.SetVector(k_PlanetDir, new Vector4(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                Mathf.Cos(azimuth) * Mathf.Cos(elevation), 0f));
            sky.SetColor(k_PlanetA, PlanetColorA);
            sky.SetColor(k_PlanetB, PlanetColorB);
            sky.SetColor(k_Ring, RingColor);

            sky.SetFloat(k_GlowStrength, GlowStrength);
            sky.SetFloat(k_GlowHeight, GlowHeight);
            sky.SetFloat(k_GlowFalloff, GlowFalloff);
            sky.SetColor(k_GlowLow, GlowUseAuroraColors ? sky.GetColor(k_AuroraA) : GlowColorLow);
            sky.SetColor(k_GlowHigh, GlowUseAuroraColors ? sky.GetColor(k_AuroraB) : GlowColorHigh);
            sky.SetFloat(k_GlowAzimuth, GlowAzimuth);
            sky.SetFloat(k_GlowDirectional, GlowDirectional);
            sky.SetFloat(k_GlowWidth, GlowWidth);
            sky.SetFloat(k_GlowOverFog, GlowOverFog);
        }
    }
}
