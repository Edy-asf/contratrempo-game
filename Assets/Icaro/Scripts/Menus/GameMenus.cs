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
    // - Título (ao abrir o jogo): Jogar, Configurações, Créditos, Sair. Pulado ao recomeçar uma run e nos atalhos de
    //   teste do Editor (Testar a partir de / Simular sala).
    // - Pausa (Esc): resumo da run e Continuar, Configurações, Reiniciar run, Menu principal, Sair.
    // - Morte: "ÍCARO-9 foi destruído" com as estatísticas da run (RunStats) e Recomeçar, Menu principal, Sair.
    // - Configurações (SettingsMenu) e Créditos.
    // Com uma tela aberta, o jogo fica pausado (Time.timeScale = 0) e o cursor livre; a pausa também pausa o áudio.
    // Aplica as configurações que dependem da cena (sensibilidade, inverter Y, campo de visão, brilho) e mostra o
    // contador de FPS. Desliga o InGameMenuManager e o GameFlowManager do Microgame (este último antes do Start dele,
    // para não zerar o volume nem carregar a cena de derrota). O PlayerBalance cria este componente.
    // Visual (feito pelo Luciano): títulos alinhados à esquerda no alto da tela, na fonte FonteRE (Resources) com
    // contorno branco e sombra; botões em faixa encostados na borda esquerda, que alargam ao passar o mouse
    // (MenuKit.Strip); no título, "CONTRATEMPO" sobre um vídeo em loop (StreamingAssets/bg_title.webm), com a imagem
    // Resources/bg_titleimg enquanto o vídeo carrega ou se ele faltar. Com uma tela aberta, o HUD do jogo some (os
    // outros Canvas ligados são desligados e, ao fechar, só esses voltam).
    [DefaultExecutionOrder(-200)]
    public class GameMenus : MonoBehaviour
    {
        public static GameMenus Instance { get; private set; }

        // Ao recarregar a cena para recomeçar a run, pula o título uma vez
        public static bool SkipTitleOnce;

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
        const string k_TitleVideo = "bg_title.webm";        // em StreamingAssets
        const string k_TitleImage = "bg_titleimg";          // em Resources
        const string k_TitleFont = "FonteRE";               // em Resources

        Canvas m_Canvas;
        RectTransform m_Layer;
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
        TMP_FontAsset m_TitleFont;
        readonly List<Canvas> m_HiddenCanvases = new List<Canvas>();

        public bool IsOpen => m_Page != Page.None;

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
            m_TitleFont = Resources.Load<TMP_FontAsset>(k_TitleFont);
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
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            GameSettings.Apply();
            bool debugStart = PlayerPrefs.HasKey(ViaDebugTeleport.StartBiomeKey) ||
                              PlayerPrefs.HasKey(ViaDebugTeleport.StartRoomKey);
            if (SkipTitleOnce || debugStart)
                SkipTitleOnce = false;
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

            if (m_Page == Page.Settings)
            {
                m_Settings.Tick();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (m_Page == Page.Pause)
                {
                    Resume();
                    return;
                }
                if (m_Page == Page.Credits)
                {
                    ShowPage(m_ReturnPage);
                    return;
                }
            }

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
            WeaponSpriteTuner.IsOpen;

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
                weapons.DefaultFov = GameSettings.FieldOfView;

            if (m_Exposure)
                m_Exposure.postExposure.Override(GameSettings.Brightness);
        }

        // ---------- telas ----------

        void ShowPage(Page page)
        {
            ClearPage();
            m_Page = page;
            HideHud();
            switch (page)
            {
                case Page.Title: BuildTitle(); break;
                case Page.Pause: BuildPause(); break;
                case Page.Death: BuildDeath(); break;
                case Page.Credits: BuildCredits(); break;
                case Page.Settings:
                    m_Settings = new SettingsMenu();
                    m_Settings.Build(m_Layer, () => ShowPage(m_ReturnPage));
                    break;
            }
        }

        void ClearPage()
        {
            m_Kit.Clear();
            if (m_Settings != null)
            {
                m_Settings.Destroy();
                m_Settings = null;
            }
            for (int i = m_Layer.childCount - 1; i >= 0; i--)
            {
                Transform child = m_Layer.GetChild(i);
                if (child != m_Fps.transform)
                    Destroy(child.gameObject);
            }
        }

        void ShowTitle() => ShowPage(Page.Title);

        void ShowPause()
        {
            AudioListener.pause = true;
            ShowPage(Page.Pause);
        }

        void ShowDeath()
        {
            AudioListener.pause = false;
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

        // Fundo de tela cheia (escuro ou, com 'video', o vídeo em loop) e o título no alto à esquerda
        RectTransform Backdrop(string title, string subtitle, float alpha, Color titleColor, bool video = false)
        {
            if (video)
                VideoBackground(alpha);
            else
                ItemUI.Fill("Fundo", m_Layer, 0f, new Color(0.02f, 0.03f, 0.05f, alpha));

            RectTransform panel = ItemUI.Rect("Painel", m_Layer, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1920f, 1080f));
            TextMeshProUGUI head = ItemUI.Text("Titulo", panel, new Vector2(0f, 1f), new Vector2(96f, -70f),
                new Vector2(1200f, 200f), 120f, TextAlignmentOptions.TopLeft, titleColor);
            head.rectTransform.pivot = new Vector2(0f, 1f);
            head.fontStyle = FontStyles.Bold;
            head.text = title;
            if (m_TitleFont)
                head.font = m_TitleFont;
            // Contorno branco e sombra deslocada (instância própria do material, destruída com o texto)
            Material material = head.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.05f, 0.05f, 0.05f, 0.9f));

            if (!string.IsNullOrEmpty(subtitle))
            {
                TextMeshProUGUI sub = ItemUI.Text("Subtitulo", panel, new Vector2(0f, 1f), new Vector2(100f, -185f),
                    new Vector2(1200f, 50f), 30f, TextAlignmentOptions.TopLeft, MenuKit.Muted);
                sub.rectTransform.pivot = new Vector2(0f, 1f);
                sub.text = subtitle;
            }

            return panel;
        }

        // Vídeo de fundo em loop, lido de StreamingAssets (sem som, em tempo sem escala: o menu pausa o jogo). Até o
        // vídeo ficar pronto (ou se o arquivo faltar ou falhar), aparece a imagem de fundo do título.
        void VideoBackground(float alpha)
        {
            var go = new GameObject("FundoVideo", typeof(RectTransform));
            go.transform.SetParent(m_Layer, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var image = go.AddComponent<RawImage>();
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            Texture2D still = Resources.Load<Texture2D>(k_TitleImage);
            image.texture = still;
            if (!still)
                image.color = new Color(0.02f, 0.03f, 0.05f, alpha);

            if (!m_VideoTexture)
                m_VideoTexture = new RenderTexture(1920, 1080, 0) { name = "Menu_Video" };

            var player = go.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = m_VideoTexture;
            player.source = VideoSource.Url;
            player.url = System.IO.Path.Combine(Application.streamingAssetsPath, k_TitleVideo);
            player.prepareCompleted += source =>
            {
                if (image)
                {
                    image.texture = m_VideoTexture;
                    image.color = new Color(1f, 1f, 1f, alpha);
                }
                source.Play();
            };
            player.errorReceived += (source, message) => Debug.LogWarning("[Ícaro] Vídeo do menu: " + message);
            player.Prepare();
        }

        // Coluna de botões em faixa, encostados na borda esquerda, a partir de 'top'
        void Buttons(RectTransform panel, float top, params (string Label, System.Action Action)[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                m_Kit.Strip(panel, buttons[i].Label, top - i * 74f, buttons[i].Action);
        }

        void BuildTitle()
        {
            RectTransform panel = Backdrop("CONTRATEMPO", null, 1f, new Color(0.05f, 0.15f, 0.55f), video: true);
            Buttons(panel, -320f,
                ("Jogar", Resume),
                ("Configurações", () => OpenSettings(Page.Title)),
                ("Créditos", () =>
                {
                    m_ReturnPage = Page.Title;
                    ShowPage(Page.Credits);
                }),
                ("Sair", Quit));
        }

        void BuildPause()
        {
            RectTransform panel = Backdrop("Pausado", null, 0.75f, Color.white);
            Summary(panel, -200f);
            Buttons(panel, -420f,
                ("Continuar", Resume),
                ("Configurações", () => OpenSettings(Page.Pause)),
                ("Reiniciar run", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit));
        }

        void BuildDeath()
        {
            RectTransform panel = Backdrop("ÍCARO-9 FOI DESTRUÍDO", "Os itens e as Cronitas desta run se perderam",
                0.85f, new Color(1f, 0.35f, 0.3f));
            Summary(panel, -270f);
            Buttons(panel, -560f,
                ("Recomeçar", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit));
        }

        // Resumo da run em duas colunas
        void Summary(RectTransform panel, float top)
        {
            RunStats stats = RunStats.Instance;
            if (!stats)
                return;
            string left =
                $"Tempo de run\nSalas limpas\nChefes derrotados\nAbates\nMaior multiplicador\nItens";
            string right =
                $"{RunStats.FormatTime(stats.PlayTime)}\n{stats.RoomsCleared}\n{stats.BossesDefeated}\n{stats.Kills}\n" +
                $"{stats.MaxMultiplier:0.0}x\n{stats.ItemsCollected}";
            string left2 = "Dano causado\nDano recebido\nCronitas ganhas\nMultiplicador agora";
            string right2 = $"{stats.DamageDealt:0}\n{stats.DamageTaken:0}\n{stats.CronitasEarned}\n" +
                            $"{TimeManager.CurrentMultiplier:0.0}x";

            Column(panel, left, right, new Vector2(-280f, top));
            Column(panel, left2, right2, new Vector2(280f, top));
        }

        void Column(RectTransform panel, string labels, string values, Vector2 position)
        {
            ItemUI.Text("Rotulos", panel, new Vector2(0.5f, 1f), position + new Vector2(-60f, 0f),
                new Vector2(320f, 240f), 26f, TextAlignmentOptions.TopLeft, MenuKit.Muted).text = labels;
            ItemUI.Text("Valores", panel, new Vector2(0.5f, 1f), position + new Vector2(130f, 0f),
                new Vector2(180f, 240f), 26f, TextAlignmentOptions.TopRight, MenuKit.Text).text = values;
        }

        void BuildCredits()
        {
            RectTransform panel = Backdrop("Créditos", null, 0.92f, MenuKit.Accent);
            ItemUI.Text("Texto", panel, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1000f, 520f), 26f,
                TextAlignmentOptions.Top, MenuKit.Text).text =
                "<b>ÍCARO</b>\n\n" +
                "Base: Unity FPS Microgame\n" +
                "Chefes: Ultimate Monsters Pack, de Quaternius (CC BY 4.0)\n" +
                "Nave Dédalo: SPACESHIP - CARGO, de Keremz (CC BY 4.0)\n" +
                "Texturas e céu: Poly Haven (CC0)\n" +
                "Sons de combate: Kenney (CC0)\n" +
                "Música: composta em código para o jogo\n\n" +
                "<size=80%><color=#9AA4AE>Lista completa em Assets/Icaro/CREDITOS.md</color></size>";
            Buttons(panel, -800f, ("Voltar", () => ShowPage(m_ReturnPage)));
        }

        // ---------- recomeçar e sair ----------

        static void Reload(bool skipTitle)
        {
            SkipTitleOnce = skipTitle;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Scene scene = SceneManager.GetActiveScene();
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
