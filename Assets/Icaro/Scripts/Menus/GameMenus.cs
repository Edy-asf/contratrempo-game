using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Icaro
{
    // Menus do jogo, no lugar do menu de pausa e das cenas de fim do Microgame:
    // - Título (ao abrir o jogo): "CONTRATEMPO" com as letras caindo, "Pressione qualquer tecla" na primeira vez e
    //   depois as faixas Jogar, Configurações, Créditos, Sair entrando em cascata. Pulado ao recomeçar uma run e nos
    //   atalhos de teste do Editor (Testar a partir de / Simular sala). Jogar: as faixas saem, a tela some e o jogo
    //   começa.
    // - Pausa (Esc): a cena desfocada atrás (MenuBackdropFx), Continuar, Configurações, Reiniciar run, Menu principal,
    //   Sair e, à direita, o resumo da run e a grade dos itens (passar o mouse mostra a descrição).
    // - Morte: tom vermelho, "ÍCARO-9 foi destruído" e as estatísticas da run (RunStats) aparecendo uma a uma e
    //   contando; depois Recomeçar, Menu principal, Sair.
    // - Configurações (SettingsMenu) e Créditos.
    // Com uma tela aberta, o jogo fica pausado (Time.timeScale = 0) e o cursor livre; a pausa também pausa o áudio.
    // Teclado em todas as telas (MenuKit: W/S, Enter, Esc). Rodapé com as teclas e a versão.
    // Aplica as configurações que dependem da cena (sensibilidade, inverter Y, campo de visão, brilho) e mostra o
    // contador de FPS. Desliga o InGameMenuManager e o GameFlowManager do Microgame (este último antes do Start dele,
    // para não zerar o volume nem carregar a cena de derrota). O PlayerBalance cria este componente.
    // Visual (do Luciano, polido): títulos no alto à esquerda na fonte FonteRE (MenuTitle: contorno branco, sombra e
    // os acentos desenhados), faixas encostadas na borda esquerda (MenuKit.Strip) sobre um degradê escuro; no título,
    // um vídeo em loop (StreamingAssets/bg_title.webm), com a imagem Resources/bg_titleimg enquanto o vídeo carrega ou
    // se ele faltar. Com uma tela aberta, o HUD do jogo some (os outros Canvas ligados são desligados e, ao fechar, só
    // esses voltam).
    [DefaultExecutionOrder(-200)]
    public class GameMenus : MonoBehaviour
    {
        public static GameMenus Instance { get; private set; }

        // Ao recarregar a cena para recomeçar a run, pula o título uma vez
        public static bool SkipTitleOnce;

        public static readonly Color TitleBlue = new Color(0.051f, 0.149f, 0.549f);
        static readonly Color k_DeathRed = new Color(0.78f, 0.1f, 0.12f);

        // "Pressione qualquer tecla" só na primeira vez que o título aparece
        static bool s_TitleIntroDone;

        enum Page
        {
            None,
            Title,
            Pause,
            Death,
            Settings,
            Credits,
        }

        const float k_DeathScreenDelay = 1.6f;
        const float k_PageFade = 0.16f;
        const float k_StartFade = 0.5f;
        const string k_TitleVideo = "bg_title.webm";        // em StreamingAssets
        const string k_TitleImage = "bg_titleimg";          // em Resources

        Canvas m_Canvas;
        RectTransform m_Layer;
        RectTransform m_PageRoot;
        CanvasGroup m_PageGroup;
        float m_PageAge;
        readonly MenuKit m_Kit = new MenuKit();
        SettingsMenu m_Settings;
        Page m_Page = Page.None;
        Page m_ReturnPage;
        TextMeshProUGUI m_Fps;
        float m_FpsSmoothed = 60f;
        ColorAdjustments m_Exposure;
        bool m_Dead;
        float m_DeathTime;
        RenderTexture m_VideoTexture;
        RawImage m_Video;
        VideoPlayer m_VideoPlayer;
        Texture2D m_Still;
        readonly List<Canvas> m_HiddenCanvases = new List<Canvas>();

        // Título esperando a primeira tecla
        TextMeshProUGUI m_PressAnyKey;
        RectTransform m_TitlePanel;

        // Saída do título para o jogo
        bool m_Starting;
        float m_StartAge;

        // Pausa: grade de itens e descrição
        readonly List<(RectTransform Rect, ItemDefinition Item, int Count)> m_ItemCells =
            new List<(RectTransform, ItemDefinition, int)>();
        TextMeshProUGUI m_ItemTooltip;

        // Morte: estatísticas aparecendo e contando
        readonly List<StatLine> m_Stats = new List<StatLine>();

        class StatLine
        {
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public float Target;
            public System.Func<float, string> Format;
            public float Delay;
            public bool Shown;
        }

        const float k_StatFirst = 0.55f;
        const float k_StatGap = 0.14f;
        const float k_StatCount = 0.45f;

        public bool IsOpen => m_Page != Page.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Play sem recarregar o domínio: a primeira vez de cada Play mostra "Pressione qualquer tecla"
            s_TitleIntroDone = false;
            SkipTitleOnce = false;
        }

        public static void EnsureExists()
        {
            if (!Instance)
                new GameObject("Menus").AddComponent<GameMenus>();
        }

        void Awake()
        {
            Instance = this;

            var flow = FindAnyObjectByType<GameFlowManager>();
            if (flow)
                flow.enabled = false;
            var menu = FindAnyObjectByType<InGameMenuManager>(FindObjectsInactive.Include);
            if (menu)
            {
                if (menu.MenuRoot)
                    menu.MenuRoot.SetActive(false);
                Destroy(menu);
            }

            m_Canvas = ItemUI.CreateCanvas(transform, "MenusIcaro", 999);
            m_Layer = (RectTransform)m_Canvas.transform;
            m_Fps = ItemUI.Text("FPS", m_Layer, new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(200f, 30f), 20f,
                TextAlignmentOptions.TopLeft, new Color(0.7f, 1f, 0.7f));
            m_Fps.rectTransform.pivot = new Vector2(0f, 1f);

            gameObject.AddComponent<RunStats>();
            CreateBrightnessVolume();
            GameSettings.Changed += ApplySceneSettings;
            EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeath);
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplySceneSettings;
            EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeath);
            if (m_VideoTexture)
            {
                m_VideoTexture.Release();
                Destroy(m_VideoTexture);
            }
            MenuBackdropFx.Target = 0f;
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            GameSettings.Apply();
            bool debugStart = PlayerPrefs.HasKey(ViaDebugTeleport.StartBiomeKey) ||
                              PlayerPrefs.HasKey(ViaDebugTeleport.StartRoomKey);
            if (RunCarry.Arriving)
            {
                // Chegando por um portal de outra cena: a run continua, sem título nem abertura
                SkipTitleOnce = false;
            }
            else if (SkipTitleOnce)
            {
                // Recomeçar a run: só o visor religando
                SkipTitleOnce = false;
                IntroSequence.Play(false);
            }
            else if (debugStart)
            {
                // Atalhos de teste do Editor: direto no jogo
            }
            else
                ShowTitle();
        }

        // ---------- quadro a quadro ----------

        void Update()
        {
            UpdateFps();

            if (m_Dead && m_Page == Page.None && Time.unscaledTime >= m_DeathTime)
                ShowDeath();

            if (m_Page == Page.None)
            {
                UpdateGameplay();
                return;
            }

            // Tela aberta: jogo pausado e cursor livre
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            m_PageAge += Time.unscaledDeltaTime;
            if (m_PageGroup)
                m_PageGroup.alpha = Mathf.Ceil(Mathf.Clamp01(m_PageAge / k_PageFade) * 4f) / 4f;

            if (m_Starting)
            {
                UpdateStart();
                return;
            }

            if (m_Page == Page.Settings)
            {
                m_Settings?.Tick();
                return;
            }

            if (m_Page == Page.Title && m_PressAnyKey)
            {
                UpdatePressAnyKey();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !m_Kit.Busy)
            {
                if (m_Page == Page.Pause)
                {
                    UiSounds.Play(UiSounds.Unclick, 0.7f);
                    Resume();
                    return;
                }
                if (m_Page == Page.Credits)
                {
                    UiSounds.Play(UiSounds.Unclick, 0.7f);
                    ShowPage(m_ReturnPage);
                    return;
                }
            }

            if (m_Page == Page.Pause)
                UpdateItemTooltip();
            if (m_Page == Page.Death)
                UpdateStats();
            m_Kit.Tick();
        }

        // Sem tela aberta: Esc abre a pausa e um clique tranca o cursor (o que o menu do Microgame fazia)
        void UpdateGameplay()
        {
            if (m_Dead || OtherOverlayOpen())
                return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ShowPause();
                return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        static bool OtherOverlayOpen() =>
            ItemDraft.Instance && ItemDraft.Instance.IsOpen || ShopUI.Instance && ShopUI.Instance.IsOpen ||
            WeaponSpriteTuner.IsOpen || IntroSequence.Playing || DialogueUI.Talking;

        void UpdateFps()
        {
            bool show = GameSettings.ShowFps;
            if (m_Fps.gameObject.activeSelf != show)
                m_Fps.gameObject.SetActive(show);
            if (!show)
                return;
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            m_FpsSmoothed = Mathf.Lerp(m_FpsSmoothed, 1f / dt, 0.05f);
            m_Fps.text = $"{m_FpsSmoothed:0} FPS";
        }

        // ---------- configurações da cena ----------

        void CreateBrightnessVolume()
        {
            var go = new GameObject("Volume_Brilho");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 60f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Exposure = profile.Add<ColorAdjustments>(true);
            volume.profile = profile;
        }

        void ApplySceneSettings()
        {
            var input = FindAnyObjectByType<PlayerInputHandler>();
            if (input)
            {
                input.LookSensitivity = GameSettings.MouseSensitivity;
                input.InvertYAxis = GameSettings.InvertY;
            }

            var weapons = FindAnyObjectByType<PlayerWeaponsManager>();
            if (weapons)
                weapons.DefaultFov = Mathf.Clamp(GameSettings.FieldOfView, GameSettings.MinFieldOfView,
                    GameSettings.MaxFieldOfView);

            if (m_Exposure)
                m_Exposure.postExposure.Override(GameSettings.Brightness);
        }

        // ---------- telas ----------

        void ShowPage(Page page)
        {
            ClearPage();
            m_Page = page;
            HideHud();

            // Fundo: o vídeo nas telas do título; a cena desfocada nas telas abertas durante o jogo
            bool fromTitle = page == Page.Title || (page == Page.Settings || page == Page.Credits) &&
                m_ReturnPage == Page.Title;
            ShowVideo(fromTitle);
            MenuBackdropFx.Target = fromTitle ? 0f : 1f;

            m_PageRoot = ItemUI.Rect("Pagina", m_Layer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            m_PageRoot.anchorMin = Vector2.zero;
            m_PageRoot.anchorMax = Vector2.one;
            m_PageRoot.sizeDelta = Vector2.zero;
            m_PageGroup = m_PageRoot.gameObject.AddComponent<CanvasGroup>();
            m_PageGroup.alpha = 0f;
            m_PageAge = 0f;

            switch (page)
            {
                case Page.Title: BuildTitle(); break;
                case Page.Pause: BuildPause(); break;
                case Page.Death: BuildDeath(); break;
                case Page.Credits: BuildCredits(); break;
                case Page.Settings:
                    m_Settings = new SettingsMenu();
                    m_Settings.Build(m_PageRoot, () => ShowPage(m_ReturnPage));
                    break;
            }

            m_Kit.FocusFirst();
            m_Fps.transform.SetAsLastSibling();
        }

        void ClearPage()
        {
            m_Kit.Clear();
            if (m_Settings != null)
            {
                m_Settings.Destroy();
                m_Settings = null;
            }
            if (m_PageRoot)
                Destroy(m_PageRoot.gameObject);
            m_PageRoot = null;
            m_PageGroup = null;
            m_PressAnyKey = null;
            m_TitlePanel = null;
            m_ItemCells.Clear();
            m_ItemTooltip = null;
            m_Stats.Clear();
        }

        void ShowTitle() => ShowPage(Page.Title);

        void ShowPause()
        {
            AudioListener.pause = true;
            m_ReturnPage = Page.Pause;
            UiSounds.Play(UiSounds.Whoosh, 0.35f, 1.2f);
            ShowPage(Page.Pause);
        }

        void ShowDeath()
        {
            AudioListener.pause = false;
            m_ReturnPage = Page.Death;
            ShowPage(Page.Death);
        }

        void OpenSettings(Page returnTo)
        {
            m_ReturnPage = returnTo;
            ShowPage(Page.Settings);
        }

        void Resume()
        {
            ClearPage();
            m_Page = Page.None;
            m_Starting = false;
            ShowVideo(false);
            MenuBackdropFx.Target = 0f;
            RestoreHud();
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Desliga os outros Canvas ligados (HUD, visor, barras) e guarda quais foram, para religar só esses
        void HideHud()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled || canvas == m_Canvas || canvas.transform.IsChildOf(transform))
                    continue;
                canvas.enabled = false;
                m_HiddenCanvases.Add(canvas);
            }
        }

        void RestoreHud()
        {
            foreach (Canvas canvas in m_HiddenCanvases)
            {
                if (canvas)
                    canvas.enabled = true;
            }
            m_HiddenCanvases.Clear();
        }

        void OnPlayerDeath(PlayerDeathEvent evt)
        {
            m_Dead = true;
            m_DeathTime = Time.unscaledTime + k_DeathScreenDelay;
        }

        // ---------- fundo em vídeo ----------

        // Vídeo de fundo em loop, lido de StreamingAssets (sem som, em tempo sem escala: o menu pausa o jogo). Até o
        // vídeo ficar pronto (ou se o arquivo faltar ou falhar), aparece a imagem de fundo do título. Fica fora da
        // página (não recomeça ao trocar entre o título, as configurações e os créditos).
        void ShowVideo(bool show)
        {
            if (!show)
            {
                if (m_Video)
                {
                    m_Video.gameObject.SetActive(false);
                    if (m_VideoPlayer)
                        m_VideoPlayer.Pause();
                }
                return;
            }

            if (m_Video)
            {
                m_Video.gameObject.SetActive(true);
                m_Video.color = Color.white;
                if (m_VideoPlayer && m_VideoPlayer.isPrepared)
                    m_VideoPlayer.Play();
                m_Video.transform.SetAsFirstSibling();
                return;
            }

            var go = new GameObject("FundoVideo", typeof(RectTransform));
            go.transform.SetParent(m_Layer, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            m_Video = go.AddComponent<RawImage>();
            m_Video.raycastTarget = false;
            m_Still = Resources.Load<Texture2D>(k_TitleImage);
            m_Video.texture = m_Still;
            m_Video.color = m_Still ? Color.white : MenuKit.Dark;

            if (!m_VideoTexture)
                m_VideoTexture = new RenderTexture(1920, 1080, 0) { name = "Menu_Video" };

            m_VideoPlayer = go.AddComponent<VideoPlayer>();
            m_VideoPlayer.playOnAwake = false;
            m_VideoPlayer.isLooping = true;
            m_VideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            m_VideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            m_VideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            m_VideoPlayer.targetTexture = m_VideoTexture;
            m_VideoPlayer.source = VideoSource.Url;
            m_VideoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, k_TitleVideo);
            m_VideoPlayer.prepareCompleted += source =>
            {
                if (m_Video)
                {
                    m_Video.texture = m_VideoTexture;
                    m_Video.color = new Color(1f, 1f, 1f, m_Video.color.a);
                }
                if (m_Video && m_Video.gameObject.activeInHierarchy)
                    source.Play();
            };
            m_VideoPlayer.errorReceived += (source, message) => Debug.LogWarning("[Ícaro] Vídeo do menu: " + message);
            m_VideoPlayer.Prepare();
        }

        // ---------- peças comuns ----------

        // Coluna de faixas encostadas na borda esquerda, a partir de 'top', entrando em cascata depois de 'delay'
        void Buttons(Transform panel, float top, float delay, params (string Label, System.Action Action)[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                m_Kit.Strip(panel, buttons[i].Label, top - i * 75f, buttons[i].Action, delay + i * 0.055f);
        }

        void Footer(params (string Key, string Label)[] hints) => MenuKit.Footer(m_PageRoot, hints, MenuKit.VersionText);

        static TextMeshProUGUI Header(Transform panel, string text, Vector2 position)
        {
            TextMeshProUGUI header = MenuKit.Label("Cabecalho", panel, new Vector2(0f, 1f), position,
                new Vector2(900f, 40f), 28f, TextAlignmentOptions.TopLeft, MenuKit.Accent, ItemFonts.VisorWide);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.text = text;
            return header;
        }

        // ---------- título ----------

        void BuildTitle()
        {
            MenuKit.LeftShade(m_PageRoot, 1150f, 0.86f);
            m_TitlePanel = m_PageRoot;
            MenuTitle.Create(m_PageRoot, "CONTRATEMPO", new Vector2(96f, -70f), 128f, TitleBlue, !s_TitleIntroDone);

            if (!s_TitleIntroDone)
            {
                m_PressAnyKey = MenuKit.Label("PressioneQualquerTecla", m_PageRoot, new Vector2(0f, 1f),
                    new Vector2(102f, -420f), new Vector2(900f, 40f), 26f, TextAlignmentOptions.TopLeft, MenuKit.Text,
                    ItemFonts.VisorWide);
                m_PressAnyKey.rectTransform.pivot = new Vector2(0f, 1f);
                m_PressAnyKey.text = "PRESSIONE QUALQUER TECLA";
                MenuKit.Footer(m_PageRoot, new (string, string)[0], MenuKit.VersionText);
                return;
            }

            BuildTitleButtons(0.05f);
        }

        void BuildTitleButtons(float delay)
        {
            Buttons(m_PageRoot, -390f, delay,
                ("Jogar", StartGame),
                ("Configurações", () => OpenSettings(Page.Title)),
                ("Créditos", () =>
                {
                    m_ReturnPage = Page.Title;
                    ShowPage(Page.Credits);
                }),
                ("Sair", Quit));
            Footer(("ENTER", "SELECIONAR"), ("W/S", "NAVEGAR"));
            m_Kit.FocusFirst();
        }

        // Texto piscando em degraus até qualquer tecla ou clique (depois que as letras do título caem)
        void UpdatePressAnyKey()
        {
            float blink = Mathf.Repeat(Time.unscaledTime * 1.1f, 1f) < 0.6f ? 1f : 0.35f;
            float enter = Mathf.Clamp01((m_PageAge - 0.7f) / 0.2f);
            m_PressAnyKey.alpha = Mathf.Ceil(enter * 4f) / 4f * blink;
            if (m_PageAge < 0.8f)
                return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool pressed = keyboard != null && keyboard.anyKey.wasPressedThisFrame ||
                           mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
            if (!pressed)
                return;

            s_TitleIntroDone = true;
            UiSounds.Play(UiSounds.Confirm, 0.6f);
            Destroy(m_PressAnyKey.gameObject);
            m_PressAnyKey = null;
            Transform footer = m_PageRoot.Find("Rodape");
            if (footer)
                Destroy(footer.gameObject);
            BuildTitleButtons(0f);
        }

        // Jogar: as faixas voltam para a esquerda e a tela some; então o jogo começa
        void StartGame()
        {
            m_Starting = true;
            m_StartAge = 0f;
            foreach (MenuKit.StripButton strip in m_Kit.Strips)
                strip.Leaving = true;
            UiSounds.Play(UiSounds.Whoosh, 0.55f, 0.9f);
        }

        void UpdateStart()
        {
            m_StartAge += Time.unscaledDeltaTime;
            m_Kit.Tick();
            float fade = Mathf.Clamp01((m_StartAge - 0.15f) / k_StartFade);
            float alpha = 1f - fade * fade;
            if (m_PageGroup)
                m_PageGroup.alpha = Mathf.Ceil(alpha * 6f) / 6f;
            // O vídeo escurece até o preto, sempre opaco: ficar transparente deixava a cena aparecer por trás antes
            // da abertura cobrir a tela
            if (m_Video)
                m_Video.color = Color.Lerp(m_Video.texture ? Color.white : MenuKit.Dark, Color.black, fade);
            if (fade >= 1f)
            {
                Resume();
                IntroSequence.Play(true);
            }
        }

        // ---------- pausa ----------

        void BuildPause()
        {
            ItemUI.Fill("Escurecer", m_PageRoot, 0f, new Color(MenuKit.Dark.r, MenuKit.Dark.g, MenuKit.Dark.b, 0.45f));
            MenuKit.LeftShade(m_PageRoot, 1000f, 0.85f);
            MenuTitle.Create(m_PageRoot, "PAUSADO", new Vector2(96f, -70f), 112f, TitleBlue, false);
            Buttons(m_PageRoot, -330f, 0f,
                ("Continuar", Resume),
                ("Configurações", () => OpenSettings(Page.Pause)),
                ("Reiniciar run", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit));

            RectTransform panel = MenuKit.Glass(m_PageRoot, "Resumo", new Vector2(780f, -240f), new Vector2(1050f, 690f));
            Header(panel, "RESUMO DA RUN", new Vector2(40f, -30f));
            RunStats stats = RunStats.Instance;
            if (stats)
            {
                StatColumn(panel, new Vector2(40f, -90f), new[]
                {
                    ("Tempo de run", RunStats.FormatTime(stats.PlayTime)),
                    ("Salas limpas", stats.RoomsCleared.ToString()),
                    ("Chefes derrotados", stats.BossesDefeated.ToString()),
                    ("Abates", stats.Kills.ToString()),
                });
                StatColumn(panel, new Vector2(540f, -90f), new[]
                {
                    ("Multiplicador agora", $"{TimeManager.CurrentMultiplier:0.0}x"),
                    ("Maior multiplicador", $"{stats.MaxMultiplier:0.0}x"),
                    ("Dano causado", $"{stats.DamageDealt:0}"),
                    ("Cronitas ganhas", stats.CronitasEarned.ToString()),
                });
            }

            Header(panel, "ITENS", new Vector2(40f, -330f));
            ItemGrid(panel, new Vector2(40f, -380f));
            m_ItemTooltip = MenuKit.Label("Descricao", panel, new Vector2(0f, 0f), new Vector2(40f, 28f),
                new Vector2(970f, 96f), 22f, TextAlignmentOptions.BottomLeft, MenuKit.Text, ItemFonts.Regular);
            m_ItemTooltip.rectTransform.pivot = Vector2.zero;
            m_ItemTooltip.textWrappingMode = TextWrappingModes.Normal;
            m_ItemTooltip.text = "";

            Footer(("ENTER", "SELECIONAR"), ("W/S", "NAVEGAR"), ("ESC", "CONTINUAR"));
        }

        // Linhas de rótulo e valor (rótulo na fonte do visor, valor em pixel à direita)
        static void StatColumn(Transform panel, Vector2 position, (string Label, string Value)[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
                StatRow(panel, position + new Vector2(0f, -i * 50f), lines[i].Label, lines[i].Value, 440f);
        }

        static (TextMeshProUGUI Label, TextMeshProUGUI Value) StatRow(Transform panel, Vector2 position, string label,
            string value, float width)
        {
            var topLeft = new Vector2(0f, 1f);
            TextMeshProUGUI caption = MenuKit.Label("Rotulo", panel, topLeft, position, new Vector2(width, 40f), 24f,
                TextAlignmentOptions.MidlineLeft, MenuKit.Muted, ItemFonts.Visor);
            caption.rectTransform.pivot = topLeft;
            caption.text = label;
            TextMeshProUGUI number = MenuKit.Label("Valor", panel, topLeft, position, new Vector2(width, 40f), 28f,
                TextAlignmentOptions.MidlineRight, MenuKit.Text, ItemFonts.Bold);
            number.rectTransform.pivot = topLeft;
            number.text = value;
            Image line = ItemUI.Panel("Separador", panel, topLeft, position + new Vector2(0f, -42f),
                new Vector2(width, 2f), new Color(1f, 1f, 1f, 0.06f));
            line.rectTransform.pivot = topLeft;
            return (caption, number);
        }

        // Ícones dos itens da run (célula de vidro, linha na cor da raridade e quantidade), 13 por linha
        void ItemGrid(Transform panel, Vector2 position)
        {
            PlayerStats player = PlayerStats.Instance;
            if (!player || player.Items.Count == 0)
            {
                TextMeshProUGUI none = MenuKit.Label("Vazio", panel, new Vector2(0f, 1f), position,
                    new Vector2(900f, 40f), 22f, TextAlignmentOptions.TopLeft, MenuKit.Muted, ItemFonts.Visor);
                none.rectTransform.pivot = new Vector2(0f, 1f);
                none.text = "Nenhum item ainda. Limpe uma arena para escolher cartas.";
                return;
            }

            const float cell = 64f, gap = 10f;
            const int perRow = 13;
            var topLeft = new Vector2(0f, 1f);
            for (int i = 0; i < player.Items.Count; i++)
            {
                PlayerStats.ItemStack stack = player.Items[i];
                if (!stack.Item)
                    continue;
                var at = position + new Vector2(i % perRow * (cell + gap), -(i / perRow) * (cell + gap));
                Image slot = ItemUI.Panel("Item_" + stack.Item.name, panel, topLeft, at, new Vector2(cell, cell),
                    new Color(1f, 1f, 1f, 0.05f));
                slot.rectTransform.pivot = topLeft;
                Color rarity = player.Catalog ? player.Catalog.ColorOf(stack.Item.Rarity) : Color.white;
                Image line = ItemUI.Panel("Raridade", slot.transform, new Vector2(0.5f, 0f), Vector2.zero,
                    new Vector2(cell, 3f), rarity);
                line.rectTransform.pivot = new Vector2(0.5f, 0f);
                Sprite icon = ItemArt.IconFor(stack.Item);
                if (icon)
                {
                    Image image = ItemUI.Fill("Icone", slot.transform, 6f, Color.white);
                    image.sprite = icon;
                    image.preserveAspect = true;
                }
                if (stack.Count > 1)
                {
                    TextMeshProUGUI count = MenuKit.Label("Quantidade", slot.transform, new Vector2(1f, 0f),
                        new Vector2(-4f, 4f), new Vector2(50f, 24f), 20f, TextAlignmentOptions.BottomRight, Color.white,
                        ItemFonts.Bold);
                    count.rectTransform.pivot = new Vector2(1f, 0f);
                    count.text = $"x{stack.Count}";
                }

                m_ItemCells.Add((slot.rectTransform, stack.Item, stack.Count));
            }
        }

        void UpdateItemTooltip()
        {
            if (!m_ItemTooltip)
                return;
            foreach ((RectTransform rect, ItemDefinition item, int count) in m_ItemCells)
            {
                if (!ItemUI.MouseOver(rect))
                    continue;
                PlayerStats player = PlayerStats.Instance;
                Color rarity = player && player.Catalog ? player.Catalog.ColorOf(item.Rarity) : Color.white;
                m_ItemTooltip.text = $"<color=#{ItemUI.Hex(rarity)}>{item.DisplayName}</color>  " +
                                     ItemUI.Describe(item, count).Replace("\n", "   ");
                return;
            }

            m_ItemTooltip.text = "";
        }

        // ---------- morte ----------

        void BuildDeath()
        {
            ItemUI.Fill("Vermelho", m_PageRoot, 0f, new Color(0.1f, 0.01f, 0.02f, 0.62f));
            MenuKit.LeftShade(m_PageRoot, 1150f, 0.8f);
            MenuTitle.Create(m_PageRoot, "ÍCARO-9 FOI DESTRUÍDO", new Vector2(96f, -70f), 104f, k_DeathRed, true, false);
            TextMeshProUGUI sub = MenuKit.Label("Subtitulo", m_PageRoot, new Vector2(0f, 1f), new Vector2(102f, -222f),
                new Vector2(1200f, 40f), 26f, TextAlignmentOptions.TopLeft, MenuKit.Muted, ItemFonts.Visor);
            sub.rectTransform.pivot = new Vector2(0f, 1f);
            sub.text = "Os itens e as Cronitas desta run se perderam";

            RectTransform panel = MenuKit.Glass(m_PageRoot, "Estatisticas", new Vector2(900f, -300f), new Vector2(930f, 600f));
            Header(panel, "RELATÓRIO DA RUN", new Vector2(40f, -30f));
            RunStats stats = RunStats.Instance;
            var lines = new List<(string, float, System.Func<float, string>)>();
            if (stats)
            {
                lines.Add(("Tempo de run", stats.PlayTime, RunStats.FormatTime));
                lines.Add(("Salas limpas", stats.RoomsCleared, v => $"{v:0}"));
                lines.Add(("Chefes derrotados", stats.BossesDefeated, v => $"{v:0}"));
                lines.Add(("Abates", stats.Kills, v => $"{v:0}"));
                lines.Add(("Maior multiplicador", stats.MaxMultiplier, v => $"{v:0.0}x"));
                lines.Add(("Dano causado", stats.DamageDealt, v => $"{v:0}"));
                lines.Add(("Dano recebido", stats.DamageTaken, v => $"{v:0}"));
                lines.Add(("Cronitas ganhas", stats.CronitasEarned, v => $"{v:0}"));
                lines.Add(("Itens", stats.ItemsCollected, v => $"{v:0}"));
            }

            for (int i = 0; i < lines.Count; i++)
            {
                (string label, float value, System.Func<float, string> format) = lines[i];
                (TextMeshProUGUI caption, TextMeshProUGUI number) =
                    StatRow(panel, new Vector2(40f, -86f - i * 54f), label, format(0f), 850f);
                caption.alpha = 0f;
                number.alpha = 0f;
                m_Stats.Add(new StatLine
                {
                    Label = caption, Value = number, Target = value, Format = format, Delay = k_StatFirst + i * k_StatGap,
                });
            }

            float buttons = k_StatFirst + lines.Count * k_StatGap + 0.2f;
            Buttons(m_PageRoot, -330f, buttons,
                ("Recomeçar", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit));
            Footer(("ENTER", "SELECIONAR"), ("W/S", "NAVEGAR"));
        }

        // Cada linha aparece na sua vez, com um tique, e o número conta de 0 até o valor
        void UpdateStats()
        {
            foreach (StatLine line in m_Stats)
            {
                float t = m_PageAge - line.Delay;
                if (t < 0f)
                    continue;
                if (!line.Shown)
                {
                    line.Shown = true;
                    UiSounds.Play(UiSounds.Hover, 0.5f, 1.3f);
                }

                float appear = Mathf.Ceil(Mathf.Clamp01(t / 0.12f) * 4f) / 4f;
                line.Label.alpha = appear;
                line.Value.alpha = appear;
                float count = Mathf.Clamp01(t / k_StatCount);
                count = 1f - (1f - count) * (1f - count);
                line.Value.text = line.Format(line.Target * count);
            }
        }

        // ---------- créditos ----------

        void BuildCredits()
        {
            if (m_ReturnPage != Page.Title)
                ItemUI.Fill("Escurecer", m_PageRoot, 0f, new Color(MenuKit.Dark.r, MenuKit.Dark.g, MenuKit.Dark.b, 0.45f));
            else
                ItemUI.Fill("Escurecer", m_PageRoot, 0f, new Color(MenuKit.Dark.r, MenuKit.Dark.g, MenuKit.Dark.b, 0.37f));
            MenuKit.LeftShade(m_PageRoot, 900f, 0.9f);
            MenuTitle.Create(m_PageRoot, "CRÉDITOS", new Vector2(96f, -70f), 112f, TitleBlue, false);

            RectTransform panel = MenuKit.Glass(m_PageRoot, "Lista", new Vector2(660f, -240f), new Vector2(1170f, 690f));
            Header(panel, "ÍCARO", new Vector2(40f, -30f));
            TextMeshProUGUI text = MenuKit.Label("Texto", panel, new Vector2(0f, 1f), new Vector2(40f, -96f),
                new Vector2(1090f, 560f), 26f, TextAlignmentOptions.TopLeft, MenuKit.Text, ItemFonts.Visor);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 18f;
            text.text =
                "Base: Unity FPS Microgame\n" +
                "Menus: visual de Luciano\n" +
                "Chefes: Ultimate Monsters Pack, de Quaternius (CC BY 4.0)\n" +
                "Nave Dédalo: SPACESHIP - CARGO, de Keremz (CC BY 4.0)\n" +
                "Texturas e céu: Poly Haven (CC0)\n" +
                "Sons de combate: Kenney (CC0)\n" +
                "Fontes: Kenney Future e Pixel Operator (CC0), BLANGKON\n" +
                "Música: composta em código para o jogo\n\n" +
                "<color=#8C98A6>Lista completa em Assets/Icaro/CREDITOS.md</color>";

            m_Kit.Strip(m_PageRoot, "Voltar", -870f, () => ShowPage(m_ReturnPage), 0.05f, 430f, 560f);
            Footer(("ENTER", "VOLTAR"), ("ESC", "VOLTAR"));
        }

        // ---------- recomeçar e sair ----------

        static void Reload(bool skipTitle)
        {
            SkipTitleOnce = skipTitle;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            MenuBackdropFx.Target = 0f;
            Scene scene = SceneManager.GetActiveScene();
            // A run trocou de cena por um portal: recomeçar (ou o menu) volta para a cena onde ela começou
            string home = RunCarry.TakeHomeScene(scene.path);
            if (home != scene.path)
            {
                RunCarry.LoadScene(home);
                return;
            }
#if UNITY_EDITOR
            // No Editor a cena pode não estar nas Build Settings
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
