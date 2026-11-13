using UnityEngine;

namespace Icaro
{
    // Ajustes das conversas, do tradutor e da câmera presa no NPC. Asset em Resources/DialogueSettings (menu "Ícaro >
    // Ajustes dos diálogos"); mexer nele durante o Play muda na hora.
    [CreateAssetMenu(menuName = "Ícaro/Ajustes dos diálogos", fileName = "DialogueSettings")]
    public class DialogueSettings : ScriptableObject
    {
        public const string ResourcePath = "DialogueSettings";
        public const string AssetPath = "Assets/Icaro/Resources/DialogueSettings.asset";

        [Header("Texto")]
        [Tooltip("Letras por segundo ao escrever a fala")]
        public float CharsPerSecond = 42f;
        [Tooltip("Pausa extra depois de ponto, interrogação e exclamação (segundos)")]
        public float SentencePause = 0.22f;
        [Tooltip("Pausa extra depois de vírgula (segundos)")]
        public float CommaPause = 0.08f;
        [Tooltip("Com o tradutor: quanto tempo cada letra fica como hieróglifo embaralhando antes de virar letra")]
        public float DecodeTime = 0.22f;
        [Tooltip("Trocas de hieróglifo por segundo enquanto embaralha")]
        public float ScrambleRate = 22f;
        [Tooltip("Largura de cada letra (fração do tamanho da fonte); a fala é monoespaçada para os hieróglifos alinharem")]
        public float CharWidth = 0.62f;

        [Header("Câmera")]
        [Tooltip("Segundos para a câmera virar até o NPC")]
        public float TurnTime = 0.5f;
        [Tooltip("Distância da conversa: o NPC só conversa com o jogador até aqui (metros)")]
        public float InteractRadius = 3.2f;

        [Header("Guarda baixa")]
        [Tooltip("Perto de um NPC que já apareceu na vista, o ÍCARO-9 abaixa a arma e não atira (metros, no plano)")]
        public float GuardRadius = 9f;
        [Tooltip("Folga para levantar a arma de novo ao se afastar (metros além do raio)")]
        public float GuardRelease = 2f;

        [Header("HUD na conversa")]
        [Tooltip("Segundos para o resto do HUD sumir ao abrir a conversa e voltar ao fechar")]
        public float HudFadeTime = 0.35f;

        [Header("Tradutor")]
        public string TranslatorName = "Tradutor Aeônida";
        [TextArea(2, 4)]
        public string TranslatorDescription = "Decifra a escrita e a fala dos antigos habitantes de Kairós. Sem ele, as conversas são só hieróglifos.";
        public int TranslatorPrice = 90;

        static DialogueSettings s_Current;

        public static DialogueSettings Current
        {
            get
            {
                if (s_Current)
                    return s_Current;
                s_Current = Resources.Load<DialogueSettings>(ResourcePath);
#if UNITY_EDITOR
                if (!s_Current)
                    s_Current = CreateAsset();
#endif
                if (!s_Current)
                    s_Current = CreateInstance<DialogueSettings>();
                return s_Current;
            }
        }

#if UNITY_EDITOR
        public static DialogueSettings CreateAsset()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueSettings>(AssetPath);
            if (existing)
                return existing;
            var settings = CreateInstance<DialogueSettings>();
            UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return settings;
        }
#endif
    }
}
