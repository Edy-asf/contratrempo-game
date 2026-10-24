using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Tela de configurações (do título e da pausa), em abas:
    // - Jogo: mira assistida e intensidade, números de dano, marcador de acerto, tremor de câmera, setas de ameaça,
    //   contador de FPS.
    // - Vídeo: modo de tela, resolução, qualidade, VSync, limite de FPS, escala de renderização, sombras, brilho,
    //   campo de visão e intensidade dos efeitos de tempo na tela.
    // - Áudio: volume geral e da música.
    // - Controles: sensibilidade, inverter Y e as teclas (ações do Microgame, dash e loja), com "Restaurar teclas".
    // Tudo é aplicado e salvo na hora (GameSettings). Esc ou "Voltar" fecham.
    public class SettingsMenu
    {
        static readonly string[] k_Tabs = { "Jogo", "Vídeo", "Áudio", "Controles" };
        const float k_RowStep = 58f;

        readonly MenuKit m_Nav = new MenuKit();
        readonly MenuKit m_Rows = new MenuKit();
        readonly List<MenuKit.ButtonControl> m_TabButtons = new List<MenuKit.ButtonControl>();
        RectTransform m_Root;
        RectTransform m_Content;
        Action m_Back;
        int m_Tab;

        public bool IsWaitingKey => m_Rows.Waiting != null;

        public void Build(Transform parent, Action back)
        {
            m_Back = back;
            m_Root = ItemUI.Rect("Configuracoes", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 980f));
            ItemUI.Fill("Fundo", m_Root, 0f, new Color(0.04f, 0.06f, 0.08f, 0.96f));

            TextMeshProUGUI title = ItemUI.Text("Titulo", m_Root, new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(1100f, 60f), 46f, TextAlignmentOptions.Center, MenuKit.Accent);
            title.fontStyle = FontStyles.Bold;
            title.text = "Configurações";

            for (int i = 0; i < k_Tabs.Length; i++)
            {
                int tab = i;
                float x = (i - (k_Tabs.Length - 1) * 0.5f) * 250f;
                m_TabButtons.Add(m_Nav.Button(m_Root, k_Tabs[i], new Vector2(x, -110f), new Vector2(230f, 50f),
                    () => ShowTab(tab), new Vector2(0.5f, 1f)));
            }

            m_Nav.Button(m_Root, "Voltar", new Vector2(0f, 50f), new Vector2(260f, 56f), () => m_Back?.Invoke(),
                new Vector2(0.5f, 0f));

            m_Content = ItemUI.Rect("Conteudo", m_Root, new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                new Vector2(MenuKit.RowWidth, 700f));
            ShowTab(m_Tab);
        }

        public void Destroy()
        {
            if (m_Root)
                UnityEngine.Object.Destroy(m_Root.gameObject);
            m_Root = null;
            m_Nav.Clear();
            m_Rows.Clear();
            m_TabButtons.Clear();
        }

        public void Tick()
        {
            if (!m_Root)
                return;

            Keyboard keyboard = Keyboard.current;
            if (!IsWaitingKey && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                m_Back?.Invoke();
                return;
            }

            if (!IsWaitingKey)
                m_Nav.Tick();
            m_Rows.Tick();

            // Aba atual destacada
            for (int i = 0; i < m_TabButtons.Count; i++)
            {
                if (i == m_Tab)
                    m_TabButtons[i].Background.color = new Color(0.18f, 0.42f, 0.52f, 1f);
            }
        }

        void ShowTab(int tab)
        {
            m_Tab = tab;
            m_Rows.Clear();
            for (int i = m_Content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(m_Content.GetChild(i).gameObject);

            switch (tab)
            {
                case 0: BuildGame(); break;
                case 1: BuildVideo(); break;
                case 2: BuildAudio(); break;
                default: BuildControls(); break;
            }
        }

        static float Y(int row) => -row * k_RowStep;

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        void BuildGame()
        {
            Transform c = m_Content;
            m_Rows.Toggle(c, "Mira assistida", Y(0), () => GameSettings.AimAssist, v => GameSettings.AimAssist = v);
            m_Rows.Slider(c, "Intensidade da mira assistida", Y(1), 0f, 1f, 0.05f,
                () => GameSettings.AimAssistIntensity, v => GameSettings.AimAssistIntensity = v, Percent);
            m_Rows.Toggle(c, "Números de dano", Y(2), () => GameSettings.DamageNumbers,
                v => GameSettings.DamageNumbers = v);
            m_Rows.Toggle(c, "Marcador de acerto", Y(3), () => GameSettings.HitMarker, v => GameSettings.HitMarker = v);
            m_Rows.Toggle(c, "Tremor da câmera", Y(4), () => GameSettings.CameraShakeOn,
                v => GameSettings.CameraShakeOn = v);
            m_Rows.Toggle(c, "Setas de ameaça e de dano", Y(5), () => GameSettings.ThreatIndicators,
                v => GameSettings.ThreatIndicators = v);
            m_Rows.Toggle(c, "Mostrar FPS", Y(6), () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);
        }

        void BuildVideo()
        {
            Transform c = m_Content;
            m_Rows.Choice(c, "Modo de tela", Y(0), () => GameSettings.ScreenModes, () => GameSettings.ScreenMode,
                v => GameSettings.ScreenMode = v);
            m_Rows.Choice(c, "Resolução", Y(1), ResolutionNames, CurrentResolution,
                v => GameSettings.ResolutionIndex = v);
            m_Rows.Choice(c, "Qualidade", Y(2), () => QualitySettings.names,
                () => GameSettings.Quality >= 0 ? GameSettings.Quality : QualitySettings.GetQualityLevel(),
                v => GameSettings.Quality = v);
            m_Rows.Toggle(c, "VSync", Y(3), () => GameSettings.VSync, v => GameSettings.VSync = v);
            m_Rows.Choice(c, "Limite de FPS (sem VSync)", Y(4), FpsNames, () => GameSettings.FpsLimit,
                v => GameSettings.FpsLimit = v);
            m_Rows.Slider(c, "Escala de renderização", Y(5), 0.5f, 1f, 0.05f, () => GameSettings.RenderScale,
                v => GameSettings.RenderScale = v, Percent);
            m_Rows.Choice(c, "Sombras", Y(6), () => GameSettings.ShadowLevels, () => GameSettings.Shadows,
                v => GameSettings.Shadows = v);
            m_Rows.Slider(c, "Brilho", Y(7), -1f, 1f, 0.05f, () => GameSettings.Brightness,
                v => GameSettings.Brightness = v, v => (v >= 0f ? "+" : "") + v.ToString("0.00"));
            m_Rows.Slider(c, "Campo de visão", Y(8), 60f, 110f, 1f, () => GameSettings.FieldOfView,
                v => GameSettings.FieldOfView = v, v => $"{v:0}°");
            m_Rows.Slider(c, "Efeitos de tempo na tela", Y(9), 0f, 1f, 0.05f, () => GameSettings.ScreenEffects,
                v => GameSettings.ScreenEffects = v, Percent);
        }

        void BuildAudio()
        {
            Transform c = m_Content;
            m_Rows.Slider(c, "Volume geral", Y(0), 0f, 1f, 0.05f, () => GameSettings.MasterVolume,
                v => GameSettings.MasterVolume = v, Percent);
            m_Rows.Slider(c, "Música", Y(1), 0f, 1f, 0.05f, () => GameSettings.MusicVolume,
                v => GameSettings.MusicVolume = v, Percent);
        }

        void BuildControls()
        {
            Transform c = m_Content;
            m_Rows.Slider(c, "Sensibilidade do mouse", Y(0), 0.1f, 3f, 0.05f, () => GameSettings.MouseSensitivity,
                v => GameSettings.MouseSensitivity = v, v => v.ToString("0.00"));
            m_Rows.Toggle(c, "Inverter eixo Y", Y(1), () => GameSettings.InvertY, v => GameSettings.InvertY = v);

            int row = 2;
            foreach ((string label, string action) in GameSettings.RebindableActions)
                m_Rows.KeyAction(c, label, Y(row++), action);
            m_Rows.KeyCustom(c, "Dash", Y(row++), () => GameSettings.DashKey, k => GameSettings.DashKey = k);
            m_Rows.KeyCustom(c, "Abrir loja", Y(row++), () => GameSettings.ShopKey, k => GameSettings.ShopKey = k);

            m_Rows.Button(c, "Restaurar teclas", new Vector2(0f, Y(row) - 10f), new Vector2(300f, 48f), () =>
            {
                GameSettings.ResetBindings();
                m_Rows.RefreshAll();
            }, new Vector2(0.5f, 1f));
        }

        // ---------- listas ----------

        static string[] ResolutionNames()
        {
            Resolution[] resolutions = GameSettings.Resolutions;
            var names = new string[resolutions.Length];
            for (int i = 0; i < resolutions.Length; i++)
                names[i] = $"{resolutions[i].width} x {resolutions[i].height} " +
                           $"({resolutions[i].refreshRateRatio.value:0} Hz)";
            return names;
        }

        static int CurrentResolution()
        {
            if (GameSettings.ResolutionIndex >= 0)
                return GameSettings.ResolutionIndex;
            Resolution[] resolutions = GameSettings.Resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                    return i;
            }

            return resolutions.Length - 1;
        }

        static string[] FpsNames()
        {
            var names = new string[GameSettings.FpsLimits.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = GameSettings.FpsLimits[i] < 0 ? "Sem limite" : $"{GameSettings.FpsLimits[i]} FPS";
            return names;
        }
    }
}
