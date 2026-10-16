using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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

            m_Canvas = ItemUI.CreateCanvas(transform, "MenusIcaro", 300);
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
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnPlayerDeath(PlayerDeathEvent evt)
        {
            m_Dead = true;
            m_DeathTime = Time.unscaledTime + k_DeathScreenDelay;
        }

        // Fundo escuro de tela cheia e um título
        RectTransform Backdrop(string title, string subtitle, float alpha, Color titleColor)
        {
            ItemUI.Fill("Fundo", m_Layer, 0f, new Color(0.02f, 0.03f, 0.05f, alpha));
            RectTransform panel = ItemUI.Rect("Painel", m_Layer, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1200f, 1000f));
            TextMeshProUGUI head = ItemUI.Text("Titulo", panel, new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(1200f, 130f), 96f, TextAlignmentOptions.Center, titleColor);
            head.fontStyle = FontStyles.Bold;
            head.text = title;
            if (!string.IsNullOrEmpty(subtitle))
                ItemUI.Text("Subtitulo", panel, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 50f),
                    30f, TextAlignmentOptions.Center, MenuKit.Muted).text = subtitle;
            return panel;
        }

        // Coluna de botões centralizada a partir de 'top'
        void Buttons(RectTransform panel, float top, params (string Label, System.Action Action)[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                m_Kit.Button(panel, buttons[i].Label, new Vector2(0f, top - i * 76f), new Vector2(420f, 62f),
                    buttons[i].Action, new Vector2(0.5f, 1f));
        }

        void BuildTitle()
        {
            RectTransform panel = Backdrop("ÍCARO", "A Via Aeônida", 0.88f, MenuKit.Accent);
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
