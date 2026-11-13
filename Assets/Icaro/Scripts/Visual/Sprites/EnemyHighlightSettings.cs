using UnityEngine;

namespace Icaro
{
    // Destaque dos inimigos em sprite (shader Icaro/EnemySprite): contorno colorido de largura fixa na tela, névoa mais
    // leve sobre eles e brilho mínimo maior, para não se misturarem às árvores e à névoa. A força vem das configurações
    // (GameSettings.EnemyHighlight, 0 desliga) e escala tudo entre o visual original e estes valores. Asset em
    // Resources/EnemyHighlightSettings (menu "Ícaro > Ajustes do destaque dos inimigos"); mexer nele durante o Play
    // muda na hora.
    [CreateAssetMenu(menuName = "Ícaro/Ajustes do destaque dos inimigos", fileName = "EnemyHighlightSettings")]
    public class EnemyHighlightSettings : ScriptableObject
    {
        public const string ResourcePath = "EnemyHighlightSettings";
        public const string AssetPath = "Assets/Icaro/Resources/EnemyHighlightSettings.asset";

        [Header("Contorno")]
        [Tooltip("Cor do contorno (uma que o cenário não usa)")]
        [ColorUsage(false, true)] public Color OutlineColor = new Color(1f, 0.13f, 0.1f);
        [Tooltip("Largura do contorno em pixels da tela a 1080p, com o destaque no máximo (acompanha a resolução)")]
        [Range(0f, 4f)] public float OutlinePixels = 2f;
        [Tooltip("Largura com o destaque no mínimo (acima de 0)")]
        [Range(0f, 4f)] public float MinOutlinePixels = 1f;

        [Header("Névoa e brilho")]
        [Tooltip("Quanto da névoa normal pesa sobre os inimigos com o destaque no máximo (1 = igual ao cenário)")]
        [Range(0f, 1f)] public float FogAmount = 0.4f;
        [Tooltip("Brilho mínimo do corpo no escuro com o destaque no máximo (sem destaque: o do EnemySpriteView, 0,25); " +
                 "as partes acesas sobem na mesma proporção")]
        [Range(0f, 1f)] public float MinBrightness = 0.4f;

        static readonly int k_Highlight = Shader.PropertyToID("_IcaroEnemyHighlight");
        static readonly int k_OutlineColor = Shader.PropertyToID("_IcaroEnemyOutlineColor");
        static int s_PushedFrame = -1;
        static EnemyHighlightSettings s_Current;

        public static EnemyHighlightSettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<EnemyHighlightSettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<EnemyHighlightSettings>();
                return s_Current;
            }
        }

        // Força do destaque escolhida nas configurações (0 a 1)
        public static float Strength => Mathf.Clamp01(GameSettings.EnemyHighlight);

        // Brilho mínimo do corpo de um sprite, entre o original e o do destaque
        public static float Brightness(float original) =>
            Mathf.Lerp(original, Mathf.Max(original, Current.MinBrightness), Strength);

        // Globais do shader, uma vez por quadro (chamado pelos sprites)
        public static void Push()
        {
            if (s_PushedFrame == Time.frameCount)
                return;
            s_PushedFrame = Time.frameCount;
            EnemyHighlightSettings s = Current;
            float k = Strength;
            float width = k > 0.001f ? Mathf.Lerp(s.MinOutlinePixels, s.OutlinePixels, k) : 0f;
            Shader.SetGlobalVector(k_Highlight, new Vector4(k, width, Mathf.Lerp(1f, s.FogAmount, k), 0f));
            Shader.SetGlobalColor(k_OutlineColor, s.OutlineColor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_PushedFrame = -1;

#if UNITY_EDITOR
        public static EnemyHighlightSettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyHighlightSettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<EnemyHighlightSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }

        [UnityEditor.MenuItem("Ícaro/Ajustes do destaque dos inimigos", priority = 62)]
        static void Select()
        {
            EnemyHighlightSettings settings = CreateAsset();
            UnityEditor.Selection.activeObject = settings;
            UnityEditor.EditorGUIUtility.PingObject(settings);
        }
#endif
    }
}
