using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Tela de configurações (do título e da pausa): as abas são faixas na esquerda (a atual larga, com o texto ciano) e
    // a aba aberta fica num painel de vidro à direita, com "Restaurar padrão" (só daquela aba). Abas:
    // - Jogo: mira assistida e intensidade, números de dano, marcador de acerto, tremor de câmera, setas de ameaça,
    //   contador de FPS.
    // - Vídeo: modo de tela, resolução, qualidade, VSync, limite de FPS, escala de renderização, sombras, brilho,
    //   campo de visão e intensidade dos efeitos de tempo na tela.
    // - Áudio: volume geral e da música.
    // - Controles: sensibilidade, inverter Y e as teclas (ações do Microgame, dash e loja).
    // Teclado: W/S passam pelas linhas, A/D ajustam, Enter altera, Q/E (ou Tab) trocam de aba, Esc volta. Tudo é
    // aplicado e salvo na hora (GameSettings).
    public class SettingsMenu
    {
        static readonly string[] k_Tabs = { "Jogo", "Vídeo", "Áudio", "Controles" };
        const float k_RowStep = 55f;
        const float k_TabFade = 0.18f;

        static readonly Vector2 k_PanelPosition = new Vector2(660f, -240f);
        static readonly Vector2 k_PanelSize = new Vector2(1170f, 690f);

        readonly MenuKit m_Nav = new MenuKit { Keyboard = false };
        readonly MenuKit m_Rows = new MenuKit();
        readonly List<MenuKit.StripButton> m_TabStrips = new List<MenuKit.StripButton>();
        RectTransform m_Root;
        RectTransform m_Content;
        CanvasGroup m_ContentGroup;
        TextMeshProUGUI m_Header;
        Action m_Back;
        int m_Tab;
        float m_TabAge;
        bool m_Closing;

        public bool IsWaitingKey => m_Rows.Waiting != null;

        public void Build(Transform parent, Action back)
        {
            m_Back = back;
            m_Closing = false;
            m_Root = ItemUI.Rect("Configuracoes", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            m_Root.anchorMin = Vector2.zero;
            m_Root.anchorMax = Vector2.one;
            m_Root.sizeDelta = Vector2.zero;

            ItemUI.Fill("Escurecer", m_Root, 0f, new Color(MenuKit.Dark.r, MenuKit.Dark.g, MenuKit.Dark.b, 0.37f));
            MenuKit.LeftShade(m_Root, 900f, 0.9f);
            MenuTitle.Create(m_Root, "CONFIGURAÇÕES", new Vector2(96f, -70f), 96f, GameMenus.TitleBlue, false);

            for (int i = 0; i < k_Tabs.Length; i++)
            {
                int tab = i;
                m_TabStrips.Add(m_Nav.Strip(m_Root, k_Tabs[i], -300f - i * 75f, () => ShowTab(tab, false), i * 0.04f,
                    430f, 560f));
            }

            m_Nav.Strip(m_Root, "Voltar", -870f, Close, 0.2f, 430f, 560f);

            RectTransform panel = MenuKit.Glass(m_Root, "Painel", k_PanelPosition, k_PanelSize);
            m_Header = MenuKit.Label("Aba", panel, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(600f, 44f), 34f,
                TextAlignmentOptions.TopLeft, MenuKit.Accent, ItemFonts.VisorWide);
            m_Header.rectTransform.pivot = new Vector2(0f, 1f);

            m_Content = ItemUI.Rect("Conteudo", panel, new Vector2(0.5f, 1f), new Vector2(0f, -96f), Vector2.zero);
            m_Content.anchorMin = new Vector2(0f, 1f);
            m_Content.anchorMax = new Vector2(1f, 1f);
            m_Content.pivot = new Vector2(0.5f, 1f);
            m_Content.sizeDelta = new Vector2(-20f, 580f);
            m_Content.anchoredPosition = new Vector2(0f, -96f);
            m_ContentGroup = m_Content.gameObject.AddComponent<CanvasGroup>();

            MenuKit.Footer(m_Root, new[]
            {
                ("ENTER", "ALTERAR"), ("W/S", "NAVEGAR"), ("A/D", "AJUSTAR"), ("Q/E", "ABAS"), ("ESC", "VOLTAR"),
            }, "AS MUDANÇAS SÃO SALVAS NA HORA");

            ShowTab(m_Tab, true);
        }

        public void Destroy()
        {
            if (m_Root)
                UnityEngine.Object.Destroy(m_Root.gameObject);
            m_Root = null;
            m_Nav.Clear();
            m_Rows.Clear();
            m_TabStrips.Clear();
        }

        public void Tick()
        {
            if (!m_Root)
                return;

            Keyboard keyboard = Keyboard.current;
            if (!IsWaitingKey && !m_Nav.Busy && keyboard != null && !m_Closing)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    UiSounds.Play(UiSounds.Unclick, 0.7f);
                    Close();
                    return;
                }

                int step = keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame &&
                    !keyboard.shiftKey.isPressed ? 1 :
                    keyboard.qKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame ? -1 : 0;
                if (step != 0)
                {
                    UiSounds.Play(UiSounds.Click, 0.6f);
                    ShowTab((m_Tab + step + k_Tabs.Length) % k_Tabs.Length, true);
                    return;
                }
            }

            for (int i = 0; i < m_TabStrips.Count; i++)
                m_TabStrips[i].Selected = i == m_Tab;
            if (!IsWaitingKey)
                m_Nav.Tick();
            if (!m_Root)
                return;
            m_Rows.Tick();

            // Linhas da aba nova entram deslizando
            m_TabAge += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(m_TabAge / k_TabFade);
            m_ContentGroup.alpha = Mathf.Ceil(u * 4f) / 4f;
            m_Content.anchoredPosition = new Vector2(Mathf.Round((1f - u * (2f - u)) * 24f / 3f) * 3f, -96f);
        }

        void Close()
        {
            if (m_Closing)
                return;
            m_Closing = true;
            m_Back?.Invoke();
        }

        void ShowTab(int tab, bool focusFirst)
        {
            m_Tab = tab;
            m_TabAge = 0f;
            m_Rows.Clear();
            for (int i = m_Content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(m_Content.GetChild(i).gameObject);
            m_Header.text = k_Tabs[tab].ToUpperInvariant();

            switch (tab)
            {
                case 0: BuildGame(); break;
                case 1: BuildVideo(); break;
                case 2: BuildAudio(); break;
                default: BuildControls(); break;
            }

            // O botão de restaurar fica no cabeçalho do painel, mas entra na lista depois das linhas (último com W/S)
            RectTransform panel = (RectTransform)m_Content.parent;
            Transform old = panel.Find("Botao_Restaurar padrão");
            if (old)
                UnityEngine.Object.Destroy(old.gameObject);
            m_Rows.OutlineButton(panel, "Restaurar padrão", new Vector2(-40f, -30f), new Vector2(280f, 42f), () =>
            {
                GameSettings.ResetTab(m_Tab);
                m_Rows.RefreshAll();
            });

            if (focusFirst)
                m_Rows.FocusFirst();
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
            m_Rows.Slider(c, "Campo de visão", Y(8), GameSettings.MinFieldOfView, GameSettings.MaxFieldOfView, 1f,
                () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => $"{v:0}°");
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
            m_Rows.KeyCustom(c, "Abrir loja", Y(row), () => GameSettings.ShopKey, k => GameSettings.ShopKey = k);
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
