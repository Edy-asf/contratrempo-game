using UnityEngine;

namespace Icaro
{
    // Ajustes das habilidades ativas num asset em Resources/AbilitySettings, lido a cada uso: mexer nele no Inspector
    // durante o Play muda na hora e o valor fica. Menu "Ícaro > Ajustes das habilidades" seleciona o asset (cria se não
    // existir).
    [CreateAssetMenu(menuName = "Ícaro/Ajustes das habilidades", fileName = "AbilitySettings")]
    public class AbilitySettings : ScriptableObject
    {
        public const string ResourcePath = "AbilitySettings";
        public const string AssetPath = "Assets/Icaro/Resources/AbilitySettings.asset";

        [Header("Teste")]
        [Tooltip("O jogador já começa com as duas habilidades (para mostrar sem passar pela loja)")]
        public bool StartWithAll;

        [Header("Voltar no tempo")]
        public string RewindName = "Retorno de Kairós";
        [TextArea] public string RewindDescription =
            "Volta {0} segundos no tempo: sua vida, o multiplicador, os inimigos e os tiros voltam a como estavam. " +
            "Quem morreu nesse trecho volta à vida. Só durante a luta.";
        [Tooltip("Segundos que o tempo volta")] [Min(0.5f)] public float RewindSeconds = 4f;
        [Tooltip("Duração da volta na tela (segundos reais; tudo corre de trás para frente)")]
        [Min(0.2f)] public float RewindPlayback = 0.9f;
        [Min(1f)] public float RewindCooldown = 40f;
        [Tooltip("Intervalo da gravação (segundos)")] [Range(0.02f, 0.2f)] public float RecordInterval = 0.05f;
        public int RewindPrice = 140;

        [Header("Câmera lenta")]
        public string SlowName = "Fôlego de Kairós";
        [TextArea] public string SlowDescription =
            "Por {0} segundos o mundo fica {1}x mais lento e você não: inimigos, tiros e o multiplicador quase param. " +
            "Só durante a luta.";
        [Tooltip("Segundos com o mundo lento (sem contar as rampas)")] [Min(0.5f)] public float SlowDuration = 4f;
        [Tooltip("Velocidade do mundo durante a habilidade (1 = normal)")] [Range(0.05f, 0.9f)] public float SlowSpeed = 0.2f;
        [Min(0.01f)] public float SlowRampIn = 0.25f;
        [Min(0.01f)] public float SlowRampOut = 0.6f;
        [Min(1f)] public float SlowCooldown = 35f;
        public int SlowPrice = 120;

        public string NameOf(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => RewindName,
            AbilityType.TimeSlow => SlowName,
            _ => "",
        };

        public string DescriptionOf(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => string.Format(RewindDescription, RewindSeconds.ToString("0.#")),
            AbilityType.TimeSlow => string.Format(SlowDescription, SlowDuration.ToString("0.#"),
                (1f / Mathf.Max(0.01f, SlowSpeed)).ToString("0.#")),
            _ => "",
        };

        public float CooldownOf(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => RewindCooldown,
            AbilityType.TimeSlow => SlowCooldown,
            _ => 1f,
        };

        public int PriceOf(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => RewindPrice,
            AbilityType.TimeSlow => SlowPrice,
            _ => 0,
        };

        // Cor de cada habilidade no visor e na loja
        public static Color ColorOf(AbilityType type) => type switch
        {
            AbilityType.TimeRewind => new Color(0.35f, 0.95f, 1f),
            AbilityType.TimeSlow => new Color(0.72f, 0.55f, 1f),
            _ => Color.white,
        };

        static AbilitySettings s_Current;

        public static AbilitySettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<AbilitySettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<AbilitySettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static AbilitySettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilitySettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<AbilitySettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
