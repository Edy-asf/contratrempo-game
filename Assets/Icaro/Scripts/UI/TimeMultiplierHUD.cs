using System.Globalization;
using TMPro;
using UnityEngine;

namespace Icaro
{
    // Mostra o multiplicador de tempo (ex.: "1,7x") com cor que esquenta conforme ele sobe.
    // Se nenhum texto for atribuído, cria sozinho o texto no canto superior direito do visor (HelmetVisor), discreto,
    // na fonte sci-fi fina, com a legenda "KAIRÓS".
    public class TimeMultiplierHUD : MonoBehaviour
    {
        [Tooltip("Texto onde o multiplicador é escrito. Deixe vazio para criar um automaticamente.")]
        public TMP_Text Label;

        [Tooltip("Cor do texto de 1,0x (esquerda) até o teto do TimeManager (direita, 6,0x por padrão)")]
        public Gradient MultiplierColors = DefaultGradient();

        [Tooltip("Tamanho da fonte do texto criado automaticamente")]
        public float FontSize = 46f;

        float m_DisplayedMultiplier = -1f;

        void Update()
        {
            if (!Label)
            {
                Label = CreateLabel();
                if (!Label)
                    return;
            }

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

        // No canto superior direito do visor; nulo enquanto o visor ainda não existe
        TMP_Text CreateLabel()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.TopRight, out RectTransform group))
            {
                // Cena sem jogador (testes do tempo): texto comum na tela depois de 1 s
                if (Time.timeSinceLevelLoad < 1f)
                    return null;
                group = (RectTransform)ItemUI.CreateCanvas(transform, "TimeMultiplierCanvas", 50).transform;
            }

            m_DisplayedMultiplier = -1f;
            TextMeshProUGUI caption = VisorUI.Label("Kairos", group, new Vector2(1f, 1f), new Vector2(-44f, -30f),
                new Vector2(200f, 18f), 14f, TextAlignmentOptions.TopRight, VisorUI.Dim, true);
            caption.text = "KAIRÓS";
            caption.characterSpacing = 8f;
            return VisorUI.Label("Multiplicador", group, new Vector2(1f, 1f), new Vector2(-42f, -46f),
                new Vector2(300f, 60f), FontSize, TextAlignmentOptions.TopRight, Color.white);
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
