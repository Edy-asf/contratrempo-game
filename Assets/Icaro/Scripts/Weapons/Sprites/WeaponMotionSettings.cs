using UnityEngine;

namespace Icaro
{
    // Movimento da arma em sprite (WeaponSpriteHUD) num asset em Resources/WeaponMotionSettings, lido a cada frame:
    // mexer nele no Inspector durante o Play muda a arma na hora e o valor continua depois de sair do Play (e ao
    // recomeçar a run). Menu "Ícaro > Ajustes do movimento da arma" seleciona o asset (cria se não existir); no Play,
    // o botão do Inspector abaixa e levanta a arma para conferir.
    [CreateAssetMenu(menuName = "Ícaro/Ajustes do movimento da arma", fileName = "WeaponMotionSettings")]
    public class WeaponMotionSettings : ScriptableObject
    {
        public const string ResourcePath = "WeaponMotionSettings";
        public const string AssetPath = "Assets/Icaro/Resources/WeaponMotionSettings.asset";

        [Header("Abaixar e levantar (abertura, escolha de cartas)")]
        [Tooltip("Segundos para abaixar a arma (tempo sem escala: funciona com o jogo pausado)")]
        [Range(0.05f, 3f)] public float LowerTime = 0.65f;

        [Tooltip("Segundos para levantar a arma de volta")]
        [Range(0.05f, 4f)] public float LiftTime = 1.3f;

        [Tooltip("Onde a arma fica abaixada, em fração da altura do sprite (x para a direita, y para cima)")]
        public Vector2 LoweredOffset = new Vector2(0.4f, -1.2f);

        [Tooltip("Giro da arma abaixada (graus, anti-horário: o cano desce), em volta da mão")]
        [Range(-90f, 90f)] public float LoweredAngle = 32f;

        [Tooltip("Quanto o cano passa do ponto ao terminar de levantar, antes de assentar (0 = nada)")]
        [Range(0f, 4f)] public float LiftSettle = 1.8f;

        [Header("Troca e balanço")]
        [Tooltip("Segundos para a arma subir da borda ao trocar de arma")]
        [Range(0.05f, 2f)] public float RaiseTime = 0.25f;

        [Tooltip("Balanço ao andar (pixels do desenho em código, 160 de altura)")]
        [Range(0f, 20f)] public float BobAmount = 4f;

        [Tooltip("Quanto o sprite atrasa ao virar a câmera (pixels do desenho em código por grau)")]
        [Range(0f, 3f)] public float SwayPerDegree = 0.6f;

        static WeaponMotionSettings s_Current;

        // O asset de Resources; sem ele, uma cópia com os valores padrão
        public static WeaponMotionSettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<WeaponMotionSettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<WeaponMotionSettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static WeaponMotionSettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponMotionSettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<WeaponMotionSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
