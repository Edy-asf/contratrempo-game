using UnityEngine;

namespace Icaro
{
    // Luz do ambiente na arma em sprite (WeaponSpriteHUD) num asset em Resources/WeaponLightSettings, lido a cada
    // frame: mexer nele no Inspector durante o Play muda a arma na hora e o valor continua depois de sair do Play.
    // Menu "Ícaro > Ajustes da luz da arma" seleciona o asset (cria se não existir).
    [CreateAssetMenu(menuName = "Ícaro/Ajustes da luz da arma", fileName = "WeaponLightSettings")]
    public class WeaponLightSettings : ScriptableObject
    {
        public const string ResourcePath = "WeaponLightSettings";
        public const string AssetPath = "Assets/Icaro/Resources/WeaponLightSettings.asset";

        [Header("Tom do bioma")]
        [Tooltip("Quanto a luz ambiente tinge a arma (0 = cores puras do desenho)")]
        [Range(0f, 1f)] public float AmbientTint = 0.4f;

        [Tooltip("Brilho mínimo da arma em lugares escuros (fração das cores do desenho)")]
        [Range(0f, 1f)] public float MinBrightness = 0.6f;

        [Header("Luzes por perto")]
        [Tooltip("Quanto as luzes pontuais por perto somam a cor delas à arma (0 = nada)")]
        [Range(0f, 2f)] public float NearbyLights = 0.6f;

        [Tooltip("Rapidez com que a cor das luzes por perto entra e sai (maior = mais rápido)")]
        [Range(0.5f, 20f)] public float NearbySmoothing = 6f;

        [Header("Clarão do disparo")]
        [Tooltip("Força do clarão de cada disparo (0 = sem clarão)")]
        [Range(0f, 1f)] public float ShotFlash = 0.35f;

        [Tooltip("Duração do clarão do disparo (s)")]
        [Range(0.01f, 0.5f)] public float ShotFlashTime = 0.08f;

        public Color ShotFlashColor = new Color(1f, 0.92f, 0.75f);

        static WeaponLightSettings s_Current;

        // O asset de Resources; sem ele, uma cópia com os valores padrão
        public static WeaponLightSettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<WeaponLightSettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<WeaponLightSettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static WeaponLightSettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponLightSettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<WeaponLightSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
