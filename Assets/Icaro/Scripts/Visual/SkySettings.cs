using UnityEngine;

namespace Icaro
{
    // Ajustes das estrelas do céu noturno (shader Icaro/NightSky, movidas pelo NightSkyMotion) num asset em
    // Resources/SkySettings, lido a cada frame: mexer nele no Inspector durante o Play muda o céu na hora e o valor
    // continua depois de sair do Play. Densidade, brilho e espessura multiplicam os valores do céu de cada bioma; a
    // aurora (cortinas do sul ao norte) e o céu de cada bioma (planeta e brilho do horizonte, escritos nos materiais
    // do céu) também são ajustados aqui.
    // Menu "Ícaro > Ajustes do céu" seleciona o asset (cria se não existir).
    [CreateAssetMenu(menuName = "Ícaro/Ajustes do céu", fileName = "SkySettings")]
    public class SkySettings : ScriptableObject
    {
        public const string ResourcePath = "SkySettings";
        public const string AssetPath = "Assets/Icaro/Resources/SkySettings.asset";

        [Header("Aparência")]
        [Tooltip("Quantidade de estrelas (multiplica a densidade do céu de cada bioma)")]
        [Range(0f, 4f)] public float Density = 1f;

        [Tooltip("Brilho das estrelas e dos rastros (multiplica o brilho do céu de cada bioma)")]
        [Range(0f, 6f)] public float Brightness = 1f;

        [Tooltip("Espessura das estrelas e dos rastros (1 = cerca de 1 pixel)")]
        [Range(0.3f, 4f)] public float Thickness = 1f;

        [Header("Rotação")]
        [Tooltip("Graus por segundo a 1,0x (negativo inverte o sentido)")]
        [Range(-20f, 20f)] public float DegreesPerSecond = 2f;

        [Tooltip("1 = velocidade proporcional ao multiplicador de tempo; maior = acelera mais no fim")]
        [Range(0f, 3f)] public float SpeedExponent = 1f;

        [Header("Rastro")]
        [Tooltip("Segundos de caminho que o rastro mostra")]
        [Range(0f, 20f)] public float TrailSeconds = 3f;

        [Tooltip("Rastro máximo, em graus")]
        [Range(0f, 24f)] public float MaxTrailDegrees = 20f;

        [Header("Polo celeste")]
        [Tooltip("Direção do polo (graus: 0 = norte, 90 = leste, 180 = sul, 270 = oeste)")]
        [Range(0f, 360f)] public float PoleAzimuth = 90f;

        [Tooltip("Altura do polo acima do horizonte (graus). Os rastros cruzam o horizonte a 90° menos esse valor")]
        [Range(0f, 89f)] public float PoleElevation = 0f;

        [Tooltip("Raio (graus) em volta dos polos em que o rastro encurta até sumir (sem redemoinho)")]
        [Range(0f, 80f)] public float PoleFadeDegrees = 18f;

        [Header("Aurora: cortinas (só nos céus que têm aurora: Arquipélago)")]
        [Tooltip("Brilho da aurora (multiplica a força do céu do bioma)")]
        [Range(0f, 3f)] public float AuroraIntensity = 0.5f;

        [Tooltip("Quantas cortinas grandes cruzam o céu do sul ao norte (a primeira é a mais forte)")]
        [Range(1, 4)] public int AuroraBands = 3;

        [Tooltip("Distância entre as cortinas, de leste a oeste (em alturas da borda de baixo)")]
        [Range(0.2f, 5f)] public float AuroraSpacing = 1.3f;

        [Tooltip("Deslocamento de todas as cortinas para leste (positivo) ou oeste (negativo)")]
        [Range(-4f, 4f)] public float AuroraOffset = 0.35f;

        [Tooltip("Quanto o comprimento das cortinas ondula para os lados (como uma corda sacudida pela ponta do sul); " +
                 "os raios continuam retos")]
        [Range(0f, 3f)] public float AuroraWaviness = 0.6f;

        [Tooltip("Comprimento de cada onda ao longo da cortina (sul-norte); a velocidade das ondas segue AuroraSpeed")]
        [Range(0.5f, 12f)] public float AuroraWaveLength = 3f;

        [Tooltip("Altura das cortinas (em alturas da borda de baixo): maior = raios mais longos para cima")]
        [Range(0.2f, 5f)] public float AuroraHeight = 1.6f;

        [Tooltip("Velocidade do movimento (cortinas ondulando, raios cintilando)")]
        [Range(0f, 5f)] public float AuroraSpeed = 1f;

        [Tooltip("Quanto os raios verticais aparecem (0 = cortina lisa)")]
        [Range(0f, 1f)] public float AuroraRays = 0.7f;

        [Tooltip("Quantidade de raios ao longo da cortina (maior = mais finos e juntos)")]
        [Range(2f, 80f)] public float AuroraRayDensity = 25f;

        [Header("Aurora: pedaços ao lado das cortinas")]
        [Tooltip("Quantos pedaços menores (0 = nenhum)")]
        [Range(0, 6)] public int AuroraPieces = 3;

        [Tooltip("Comprimento de cada pedaço ao longo do céu (sul-norte)")]
        [Range(0.3f, 10f)] public float AuroraPieceLength = 2.5f;

        [Tooltip("Altura dos pedaços (fração da altura das cortinas)")]
        [Range(0.1f, 1.5f)] public float AuroraPieceHeight = 0.5f;

        [Tooltip("Brilho dos pedaços (fração do brilho das cortinas)")]
        [Range(0f, 1.5f)] public float AuroraPieceIntensity = 0.6f;

        [Tooltip("Distância de cada pedaço até a cortina dele, de leste a oeste")]
        [Range(0f, 3f)] public float AuroraPieceDistance = 0.5f;

        [Tooltip("Desloca todos os pedaços para leste (positivo) ou oeste (negativo)")]
        [Range(-4f, 4f)] public float AuroraPieceOffsetEastWest = 0f;

        [Tooltip("Desloca todos os pedaços ao longo do céu: para o norte (positivo) ou para o sul (negativo)")]
        [Range(-8f, 8f)] public float AuroraPieceOffsetSouthNorth = 0f;

        [Tooltip("Quanto os pedaços se espalham ao longo do céu, do sul ao norte (0 = todos no mesmo trecho)")]
        [Range(0f, 16f)] public float AuroraPieceSpread = 7f;

        [Tooltip("Quanto cada pedaço desliza para a frente e para trás com o tempo")]
        [Range(0f, 4f)] public float AuroraPieceDrift = 0.8f;

        [Header("Céu de cada bioma: planeta e brilho do horizonte")]
        public BiomeSkyTuning Bosque = new BiomeSkyTuning();

        public BiomeSkyTuning Arquipelago = BiomeSkyTuning.ArquipelagoDefaults();

        [Header("Partículas de brilho (Arquipélago)")]
        public SporeTuning ArchipelagoSpores = new SporeTuning();

        [Tooltip("Materiais do céu de cada bioma (preenchidos sozinhos no Editor)")]
        public Material BosqueSky;
        public Material ArquipelagoSky;

        const string k_BosqueSkyPath = "Assets/Icaro/Art/Grade/Materiais/Ceu_Bosque.mat";
        const string k_ArquipelagoSkyPath = "Assets/Icaro/Art/Grade/Materiais/Ceu_Arquipelago.mat";

        // Muda a cada alteração no Inspector: quem aplica a claridade do mapa reaplica na hora
        public static int Version { get; private set; }

        // Ajustes do bioma pelo nome do BiomeZone ou pelo material do céu (null = sem ajustes, multiplicadores 1)
        public BiomeSkyTuning TuningFor(string biome) =>
            biome == "Bosque" ? Bosque : biome == "Arquipelago" ? Arquipelago : null;

        public BiomeSkyTuning TuningFor(Material sky) =>
            !sky ? null : sky == BosqueSky ? Bosque : sky == ArquipelagoSky ? Arquipelago : null;

        // Escreve as cores, a claridade, o planeta e o brilho do horizonte de cada bioma no material do céu dele
        public void ApplyBiomes()
        {
            Bosque?.ApplyTo(BosqueSky);
            Arquipelago?.ApplyTo(ArquipelagoSky);
        }

#if UNITY_EDITOR
        static readonly string[] k_StarFields =
        {
            nameof(Density), nameof(Brightness), nameof(Thickness), nameof(DegreesPerSecond), nameof(SpeedExponent),
            nameof(TrailSeconds), nameof(MaxTrailDegrees), nameof(PoleAzimuth), nameof(PoleElevation),
            nameof(PoleFadeDegrees),
        };

        // Volta uma parte dos ajustes ao padrão, sem mexer nas outras
        public void ResetStars() => CopyDefaults(name => System.Array.IndexOf(k_StarFields, name) >= 0);

        public void ResetAurora() => CopyDefaults(name => name.StartsWith("Aurora"));

        public void ResetBosque()
        {
            Bosque = BiomeSkyTuning.BosqueDefaults();
            NotifyChanged();
        }

        public void ResetSpores()
        {
            ArchipelagoSpores = new SporeTuning();
            NotifyChanged();
        }

        public void ResetArquipelago()
        {
            Arquipelago = BiomeSkyTuning.ArquipelagoDefaults();
            NotifyChanged();
        }

        void CopyDefaults(System.Func<string, bool> which)
        {
            var defaults = CreateInstance<SkySettings>();
            foreach (System.Reflection.FieldInfo field in typeof(SkySettings).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (which(field.Name))
                    field.SetValue(this, field.GetValue(defaults));
            }

            DestroyImmediate(defaults);
            NotifyChanged();
        }

        // Depois de mudar os campos por código: grava nos materiais e avisa quem reaplica a claridade do mapa
        public void NotifyChanged()
        {
            FindSkies();
            ApplyBiomes();
            Version++;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        void OnValidate()
        {
            FindSkies();
            ApplyBiomes();
            Version++;
        }

        void FindSkies()
        {
            if (!BosqueSky)
                BosqueSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(k_BosqueSkyPath);
            if (!ArquipelagoSky)
                ArquipelagoSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(k_ArquipelagoSkyPath);
        }
#endif

        static SkySettings s_Current;

        // O asset de Resources; sem ele, uma cópia com os valores padrão
        public static SkySettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<SkySettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<SkySettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static SkySettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<SkySettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<SkySettings>();
            settings.FindSkies();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
