using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Mostra o multiplicador de tempo (ex.: "1,7x") com cor que esquenta conforme ele sobe.
    // Se nenhum texto for atribuído, cria sozinho um canvas com o texto no canto superior direito.
    public class TimeMultiplierHUD : MonoBehaviour
    {
        [Tooltip("Texto onde o multiplicador é escrito. Deixe vazio para criar um automaticamente.")]
        public TMP_Text Label;

        [Tooltip("Cor do texto de 1,0x (esquerda) até o teto do TimeManager (direita, 6,0x por padrão)")]
        public Gradient MultiplierColors = DefaultGradient();

        [Tooltip("Tamanho da fonte do texto criado automaticamente")]
        public float FontSize = 40f;

        float m_DisplayedMultiplier = -1f;

        void Start()
        {
            if (!Label)
                Label = CreateLabel();
        }

        void Update()
        {
            float multiplier = TimeManager.CurrentMultiplier;

            // Arredonda para a casa decimal mostrada, para só reescrever o texto quando ele muda
            float rounded = Mathf.Round(multiplier * 10f) / 10f;
            if (Mathf.Approximately(rounded, m_DisplayedMultiplier))
                return;

            m_DisplayedMultiplier = rounded;
            // Vírgula decimal fixa, sem depender dos dados de cultura da plataforma
            Label.text = rounded.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "x";

            float t = Mathf.InverseLerp(1f, TimeManager.CurrentMaxMultiplier, multiplier);
            Label.color = MultiplierColors.Evaluate(t);
        }

        TMP_Text CreateLabel()
        {
            var canvasObject = new GameObject("TimeMultiplierCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textObject = new GameObject("TimeMultiplierText", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-40f, -30f);
            rect.sizeDelta = new Vector2(300f, 60f);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopRight;
            text.raycastTarget = false;
            return text;
        }

        static Gradient DefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.2f),   // 2,0x
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.5f),  // 3,5x
                    new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0.8f),  // 5,0x
                    new GradientColorKey(new Color(0.85f, 0.1f, 0.55f), 1f), // 6,0x (teto)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
