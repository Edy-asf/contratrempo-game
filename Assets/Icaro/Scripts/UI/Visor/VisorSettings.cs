using UnityEngine;

namespace Icaro
{
    // Ajustes do visor do capacete (HelmetVisor e VisorInertia) num asset em Resources/VisorSettings, lido a cada
    // frame: mexer nele no Inspector durante o Play muda o visor na hora e o valor continua depois de sair do Play.
    // Menu "Ícaro > Ajustes do visor" seleciona o asset (cria se não existir).
    [CreateAssetMenu(menuName = "Ícaro/Ajustes do visor", fileName = "VisorSettings")]
    public class VisorSettings : ScriptableObject
    {
        public const string ResourcePath = "VisorSettings";
        public const string AssetPath = "Assets/Icaro/Resources/VisorSettings.asset";

        [Header("Visor")]
        [Tooltip("Distância do canvas à câmera (m); fica na frente do sprite da arma")]
        public float Distance = 0.25f;

        [Tooltip("Giro (graus) dos grupos laterais em volta da borda da tela, para o visor parecer curvo")]
        [Range(0f, 25f)] public float Curvature = 9f;

        [Header("Inércia ao virar a câmera")]
        [Tooltip("Intensidade geral da inércia (0 = preso à câmera)")]
        [Range(0f, 2f)] public float Strength = 1f;

        [Tooltip("Quanto do giro da câmera o visor deixa para trás a cada frame (0 a 1)")]
        [Range(0f, 1f)] public float RotationLag = 0.35f;

        [Tooltip("Tempo (s) para o visor alcançar a câmera depois de virar")]
        [Range(0.01f, 1f)] public float Smoothing = 0.12f;

        [Tooltip("Atraso máximo de giro, em graus")]
        [Range(0f, 10f)] public float MaxAngle = 2.5f;

        [Header("Inércia ao andar")]
        [Tooltip("Deslocamento por m/s de velocidade (m na distância do visor)")]
        [Range(0f, 0.01f)] public float MoveLag = 0.0012f;

        [Tooltip("Deslocamento máximo pelo movimento (m)")]
        [Range(0f, 0.05f)] public float MaxOffset = 0.012f;

        [Tooltip("Inclinação (graus) por m/s de passo lateral")]
        [Range(0f, 1f)] public float StrafeRoll = 0.12f;

        [Tooltip("Tempo (s) para o deslocamento pelo movimento assentar")]
        [Range(0.01f, 1f)] public float MoveSmoothing = 0.18f;

        static VisorSettings s_Current;

        // O asset de Resources; sem ele, uma cópia com os valores padrão
        public static VisorSettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<VisorSettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<VisorSettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static VisorSettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<VisorSettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<VisorSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
