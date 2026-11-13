using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Mostra o multiplicador de tempo (ex.: "1,7x") com cor que esquenta conforme ele sobe.
    // Se nenhum texto for atribuído, cria sozinho o texto no canto superior direito do visor (HelmetVisor), discreto,
    // na fonte sci-fi fina, com a legenda "KAIRÓS" e um relógio pequeno à esquerda do número.
    // O tempo voltando aparece aqui: num abate, o número desce contando, pisca em ciano e o ponteiro do relógio gira
    // para trás. Durante a luta o ponteiro anda (mais rápido com o multiplicador); fora dela fica parado, o relógio fica
    // frio e a legenda diz "PARADO". Passar de um inteiro subindo pisca em magenta com um sino grave; descendo, em
    // ciano com um sino agudo.
    public class TimeMultiplierHUD : MonoBehaviour
    {
        [Tooltip("Texto onde o multiplicador é escrito. Deixe vazio para criar um automaticamente.")]
        public TMP_Text Label;

        [Tooltip("Cor do texto de 1,0x (esquerda) até o teto do TimeManager (direita, 6,0x por padrão)")]
        public Gradient MultiplierColors = DefaultGradient();

        [Tooltip("Tamanho da fonte do texto criado automaticamente")]
        public float FontSize = 46f;

        [Tooltip("Cor do número e do relógio quando o tempo volta")]
        public Color RewindColor = new Color(0.4f, 0.95f, 1f);

        [Tooltip("Cor do pisca ao passar de um inteiro subindo")]
        public Color MilestoneColor = new Color(1f, 0.35f, 0.85f);

        [Tooltip("Voltas do ponteiro para trás por 1,0x tirado do multiplicador")]
        public float TurnsPerStep = 3f;

        static readonly Color k_FrozenDial = new Color(0.62f, 0.78f, 0.95f, 0.75f);

        TextMeshProUGUI m_Caption;
        RectTransform m_Dial;
        Image m_DialImage;
        Image m_HandImage;
        RectTransform m_Hand;

        float m_Shown = 1f;            // valor mostrado (desce contando até o de verdade)
        float m_LastMultiplier = 1f;
        float m_DisplayedMultiplier = -1f;
        float m_RewindFlash;
        float m_MilestoneFlash;
        Color m_MilestoneTint;
        float m_HandAngle;
        float m_HandBack;              // graus que o ponteiro ainda tem de girar para trás
        int m_LastStep = 1;
        bool m_ShownFrozen;
        Color m_LastColor;
        float m_LastScale = -1f;

        void OnEnable() => TimeManager.OnReduced += OnReduced;
        void OnDisable() => TimeManager.OnReduced -= OnReduced;

        void OnReduced(float amount, Vector3 position)
        {
            m_RewindFlash = 1f;
            m_HandBack += amount * TurnsPerStep * 360f;
        }

        void Update()
        {
            if (!Label)
            {
                Label = CreateLabel();
                if (!Label)
                    return;
            }

            float dt = Time.unscaledDeltaTime;
            float multiplier = TimeManager.CurrentMultiplier;
            bool running = TimeManager.CurrentlyRunning;

            // Descendo até 1,0x no fim da sala: pisca em ciano o tempo todo e o ponteiro volta junto
            if (!running && multiplier < m_LastMultiplier - 0.0005f)
            {
                m_RewindFlash = 1f;
                m_HandBack += (m_LastMultiplier - multiplier) * TurnsPerStep * 180f;
            }

            m_LastMultiplier = multiplier;

            // O número desce contando (rápido quando a queda é grande); subindo, acompanha direto
            if (multiplier < m_Shown && running)
                m_Shown = Mathf.MoveTowards(m_Shown, multiplier, Mathf.Max(1.2f, (m_Shown - multiplier) * 7f) * dt);
            else
                m_Shown = multiplier;

            // Marcos: passar de um inteiro durante a luta (a volta a 1,0x ao limpar a sala não conta)
            int step = Mathf.FloorToInt(multiplier + 0.0001f);
            if (running && step != m_LastStep)
            {
                bool up = step > m_LastStep;
                m_MilestoneFlash = 1f;
                m_MilestoneTint = up ? MilestoneColor : RewindColor;
                UiSounds.Play(up ? TimeSounds.MilestoneUp : TimeSounds.MilestoneDown, up ? 0.7f : 0.5f);
            }

            m_LastStep = step;

            m_RewindFlash = Mathf.MoveTowards(m_RewindFlash, 0f, dt * 2.2f);
            m_MilestoneFlash = Mathf.MoveTowards(m_MilestoneFlash, 0f, dt * 1.6f);

            UpdateText(multiplier);
            UpdateDial(dt, multiplier, running);
        }

        void UpdateText(float multiplier)
        {
            // Arredonda para as três casas mostradas, para só reescrever o texto quando ele muda
            float rounded = Mathf.Round(m_Shown * 1000f) / 1000f;
            if (!Mathf.Approximately(rounded, m_DisplayedMultiplier))
            {
                m_DisplayedMultiplier = rounded;
                // Vírgula decimal fixa, sem depender dos dados de cultura da plataforma
                Label.text = rounded.ToString("0.000", CultureInfo.InvariantCulture).Replace('.', ',') + "x";
                PlaceDial();
            }

            float t = Mathf.InverseLerp(1f, TimeManager.CurrentMaxMultiplier, m_Shown);
            Color color = MultiplierColors.Evaluate(t);
            color = Color.Lerp(color, RewindColor, m_RewindFlash);
            color = Color.Lerp(color, m_MilestoneTint, m_MilestoneFlash * 0.85f);
            if (color != m_LastColor)
            {
                m_LastColor = color;
                Label.color = color;
            }

            float scale = 1f + 0.16f * Mathf.Max(m_RewindFlash * m_RewindFlash, m_MilestoneFlash * m_MilestoneFlash);
            if (Mathf.Abs(scale - m_LastScale) > 0.002f)
            {
                m_LastScale = scale;
                Label.rectTransform.localScale = Vector3.one * scale;
            }
        }

        void UpdateDial(float dt, float multiplier, bool running)
        {
            if (!m_Dial)
                return;

            // Para trás depois de um abate; senão anda com o tempo da luta (uma volta por minuto a 1,0x)
            if (m_HandBack > 0f)
            {
                float back = Mathf.Min(m_HandBack, Mathf.Max(540f, m_HandBack * 6f) * dt);
                m_HandBack -= back;
                m_HandAngle += back;
            }
            else
            {
                // Com o mundo andando o ponteiro anda (também em 1,0x antes dos inimigos); congelado, para
                m_HandAngle -= 6f * multiplier * dt * Mathf.Max(0f, KairosClock.Scale);
            }

            m_Hand.localRotation = Quaternion.Euler(0f, 0f, m_HandAngle);

            bool frozen = !running && KairosClock.Frozen;
            Color dial = frozen ? k_FrozenDial : Color.white;
            dial = Color.Lerp(dial, Color.white, m_RewindFlash);
            if (m_DialImage.color != dial)
                m_DialImage.color = m_HandImage.color = dial;

            if (frozen != m_ShownFrozen && m_Caption)
            {
                m_ShownFrozen = frozen;
                m_Caption.text = frozen ? "KAIRÓS · PARADO" : "KAIRÓS";
            }
        }

        // O relógio fica logo à esquerda do número (que é alinhado à direita e muda de largura)
        void PlaceDial()
        {
            if (!m_Dial)
                return;
            float width = Label.preferredWidth;
            m_Dial.anchoredPosition = new Vector2(-42f - width - 26f, -70f);
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
            m_ShownFrozen = false;
            m_Caption = VisorUI.Label("Kairos", group, new Vector2(1f, 1f), new Vector2(-44f, -30f),
                new Vector2(320f, 18f), 14f, TextAlignmentOptions.TopRight, VisorUI.Dim, true);
            m_Caption.text = "KAIRÓS";
            m_Caption.characterSpacing = 8f;
            CreateDial(group);
            TextMeshProUGUI label = VisorUI.Label("Multiplicador", group, new Vector2(1f, 1f), new Vector2(-42f, -46f),
                new Vector2(300f, 60f), FontSize, TextAlignmentOptions.TopRight, Color.white);
            // Cresce no pisca a partir do canto direito
            label.rectTransform.pivot = new Vector2(1f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(-42f, -76f);
            // Com o tempo parado, o número dá surtos de glitch
            label.gameObject.AddComponent<MultiplierGlitch>();
            return label;
        }

        void CreateDial(RectTransform group)
        {
            m_DialImage = Icon("Relogio", group, TimeRewindBurst.Dial());
            m_Dial = m_DialImage.rectTransform;
            m_Dial.anchorMin = m_Dial.anchorMax = new Vector2(1f, 1f);
            m_Dial.sizeDelta = new Vector2(36f, 36f);
            m_HandImage = Icon("Ponteiro", m_Dial, TimeRewindBurst.Hand());
            m_Hand = m_HandImage.rectTransform;
            m_Hand.anchorMin = Vector2.zero;
            m_Hand.anchorMax = Vector2.one;
            m_Hand.sizeDelta = Vector2.zero;
        }

        static Image Icon(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
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
