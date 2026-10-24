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
using UnityEngine.EventSystems;
using UnityEngine.Video;

namespace Icaro
{
    [DefaultExecutionOrder(-200)]
    public class GameMenus : MonoBehaviour
    {
        public static GameMenus Instance { get; private set; }

        public static bool SkipTitleOnce;

        enum Page { None, Title, Pause, Death, Settings, Credits }

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
        
        RenderTexture m_VideoTexture; 

        public bool IsOpen => m_Page != Page.None;

        public static void EnsureExists()
        {
            if (!Instance)
                new GameObject("Menus").AddComponent<GameMenus>();
        }

        void Awake()
        {
            Instance = this;

            m_VideoTexture = new RenderTexture(1920, 1080, 0);

            var flow = FindAnyObjectByType<GameFlowManager>();
            if (flow) flow.enabled = false;
            
            var menu = FindAnyObjectByType<InGameMenuManager>(FindObjectsInactive.Include);
            if (menu)
            {
                if (menu.MenuRoot) menu.MenuRoot.SetActive(false);
                Destroy(menu);
            }

            m_Canvas = ItemUI.CreateCanvas(transform, "MenusIcaro", 999);

            if (m_Canvas.GetComponent<GraphicRaycaster>() == null)
                m_Canvas.gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem_Menus");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }

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
            
            if (m_VideoTexture != null) m_VideoTexture.Release();
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            GameSettings.Apply();
            bool debugStart = PlayerPrefs.HasKey(ViaDebugTeleport.StartBiomeKey) ||
                              PlayerPrefs.HasKey(ViaDebugTeleport.StartRoomKey);
            if (SkipTitleOnce || debugStart) SkipTitleOnce = false;
            else ShowTitle();
        }

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
                if (m_Page == Page.Pause) { Resume(); return; }
                if (m_Page == Page.Credits) { ShowPage(m_ReturnPage); return; }
            }

            m_Kit.Tick();
        }

        void UpdateGameplay()
        {
            if (m_Dead || OtherOverlayOpen()) return;

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
            if (m_Fps.gameObject.activeSelf != show) m_Fps.gameObject.SetActive(show);
            if (!show) return;
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            m_FpsSmoothed = Mathf.Lerp(m_FpsSmoothed, 1f / dt, 0.05f);
            m_Fps.text = $"{m_FpsSmoothed:0} FPS";
        }

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
            if (weapons) weapons.DefaultFov = GameSettings.FieldOfView;

            if (m_Exposure) m_Exposure.postExposure.Override(GameSettings.Brightness);
        }

        void ShowPage(Page page)
        {
            ClearPage();
            m_Page = page;
            SetHudVisible(page == Page.None);

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
                if (child != m_Fps.transform) Destroy(child.gameObject);
            }
        }

        void ShowTitle() => ShowPage(Page.Title);
        void ShowPause() { AudioListener.pause = true; ShowPage(Page.Pause); }
        void ShowDeath() { AudioListener.pause = false; ShowPage(Page.Death); }

        void OpenSettings(Page returnTo)
        {
            m_ReturnPage = returnTo;
            ShowPage(Page.Settings);
        }

        void Resume()
        {
            ClearPage();
            m_Page = Page.None;
            SetHudVisible(true);
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void SetHudVisible(bool visible)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                if (c == m_Canvas || c.transform.IsChildOf(transform)) continue;
                c.enabled = visible;
            }
        }

        void OnPlayerDeath(PlayerDeathEvent evt)
        {
            m_Dead = true;
            m_DeathTime = Time.unscaledTime + k_DeathScreenDelay;
        }

        // Função modificada para ler ficheiros diretamente da pasta StreamingAssets
        RectTransform Backdrop(string title, string subtitle, float alpha, Color titleColor, string bgVideoFile = null)
        {
            if (!string.IsNullOrEmpty(bgVideoFile))
            {
                GameObject videoObj = new GameObject("FundoVideo");
                videoObj.transform.SetParent(m_Layer, false);

                RectTransform rt = videoObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                RawImage rawImage = videoObj.AddComponent<RawImage>();
                rawImage.color = new Color(1f, 1f, 1f, alpha);

                VideoPlayer vp = videoObj.AddComponent<VideoPlayer>();
                vp.playOnAwake = true;
                vp.isLooping = true;
                vp.audioOutputMode = VideoAudioOutputMode.None;
                vp.renderMode = VideoRenderMode.RenderTexture;
                vp.targetTexture = m_VideoTexture;
                
                // Lê o ficheiro diretamente pelo caminho nativo no disco
                vp.source = VideoSource.Url;
                vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, bgVideoFile);
                
                rawImage.texture = m_VideoTexture;
                vp.Play();
            }
            else
            {
                ItemUI.Fill("Fundo", m_Layer, 0f, new Color(0.02f, 0.03f, 0.05f, alpha));
            }

            RectTransform panel = ItemUI.Rect("Painel", m_Layer, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1920f, 1080f));

            TextMeshProUGUI head = ItemUI.Text("Titulo", panel, new Vector2(0f, 1f), new Vector2(96f, -70f),
                new Vector2(1200f, 200f), 120f, TextAlignmentOptions.TopLeft, titleColor);
            head.fontStyle = FontStyles.Bold;
            head.text = title;
            head.rectTransform.pivot = new Vector2(0f, 1f);

            TMP_FontAsset fonteCustomizada = Resources.Load<TMP_FontAsset>("FonteRE");
                        if (fonteCustomizada != null)
                        {
                            head.font = fonteCustomizada;
                        }
            
            head.fontMaterial = new Material(head.fontSharedMaterial);
            head.fontMaterial.EnableKeyword("OUTLINE_ON");
            head.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            head.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);

            head.fontMaterial.EnableKeyword("UNDERLAY_ON");
            head.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            head.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            head.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            head.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.05f, 0.05f, 0.05f, 0.9f));

            if (!string.IsNullOrEmpty(subtitle))
            {
                TextMeshProUGUI sub = ItemUI.Text("Subtitulo", panel, new Vector2(0f, 1f), new Vector2(100f, -185f),
                    new Vector2(1200f, 50f), 30f, TextAlignmentOptions.TopLeft, MenuKit.Muted);
                sub.text = subtitle;
                sub.rectTransform.pivot = new Vector2(0f, 1f);
            }

            return panel;
        }

        void Buttons(RectTransform panel, float top, params (string Label, System.Action Action)[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                CreateBreachButton(panel, buttons[i].Label, top - i * 74f, buttons[i].Action);
        }

        void CreateBreachButton(RectTransform parent, string label, float yPos, System.Action onClick)
        {
            const float normalWidth = 460f;
            const float hoverWidth = 640f;
            const float height = 58f;

            RectTransform rt = ItemUI.Rect($"Btn_{label}", parent, new Vector2(0f, 1f), new Vector2(0f, yPos),
                new Vector2(normalWidth, height));
            rt.pivot = new Vector2(0f, 1f);

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            
            Color normalBg = new Color(0.08f, 0.09f, 0.12f, 0.72f);
            Color hoverBg = new Color(0.09f, 0.12f, 0.15f, 0.96f);
            bg.color = normalBg;

            TextMeshProUGUI txt = ItemUI.Text("Label", rt, new Vector2(0f, 0.5f), new Vector2(96f, 0f),
                new Vector2(hoverWidth - 110f, height), 32f, TextAlignmentOptions.MidlineLeft,
                new Color(0.82f, 0.85f, 0.88f, 1f));
            txt.rectTransform.pivot = new Vector2(0f, 0.5f);
            txt.fontStyle = FontStyles.Bold;
            txt.text = label;
            txt.raycastTarget = false;

            BreachButtonHover hover = rt.gameObject.AddComponent<BreachButtonHover>();
            hover.Setup(rt, bg, txt, onClick, normalWidth, hoverWidth, normalBg, hoverBg);
        }

        private void BuildTitle()
        {
            Color darkBlueTitle = new Color(0.05f, 0.15f, 0.55f, 1f);

            RectTransform panel = Backdrop(
                title: "CONTRATEMPO",
                subtitle: "",
                alpha: 1f,
                titleColor: darkBlueTitle, 
                bgVideoFile: "bg_title.webm"
            );

            Buttons(panel, -320f,
                ("Jogar", Resume),
                ("Configurações", () => OpenSettings(Page.Title)),
                ("Créditos", () =>
                {
                    m_ReturnPage = Page.Title;
                    ShowPage(Page.Credits);
                }),
                ("Sair", Quit)
            );
        }

        private void BuildPause()
        {
            RectTransform panel = Backdrop("Pausado", null, 0.75f, Color.white);
            Summary(panel, -200f);
            Buttons(panel, -420f,
                ("Continuar", Resume),
                ("Configurações", () => OpenSettings(Page.Pause)),
                ("Reiniciar run", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit)
            );
        }

        private void BuildDeath()
        {
            Color deathAccent = new Color(1f, 0.35f, 0.3f);
            RectTransform panel = Backdrop("ÍCARO-9 FOI DESTRUÍDO", "Os itens e as Cronitas desta run se perderam",
                0.85f, deathAccent);
            Summary(panel, -270f);
            Buttons(panel, -560f,
                ("Recomeçar", () => Reload(true)),
                ("Menu principal", () => Reload(false)),
                ("Sair do jogo", Quit)
            );
        }

        void Summary(RectTransform panel, float top)
        {
            RunStats stats = RunStats.Instance;
            if (!stats) return;
            string left = $"Tempo de run\nSalas limpas\nChefes derrotados\nAbates\nMaior multiplicador\nItens";
            string right = $"{RunStats.FormatTime(stats.PlayTime)}\n{stats.RoomsCleared}\n{stats.BossesDefeated}\n{stats.Kills}\n{stats.MaxMultiplier:0.0}x\n{stats.ItemsCollected}";
            string left2 = "Dano causado\nDano recebido\nCronitas ganhas\nMultiplicador agora";
            string right2 = $"{stats.DamageDealt:0}\n{stats.DamageTaken:0}\n{stats.CronitasEarned}\n{TimeManager.CurrentMultiplier:0.0}x";
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

        static void Reload(bool skipTitle)
        {
            SkipTitleOnce = skipTitle;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
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

    public class BreachButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        RectTransform m_Rect;
        Image m_Bg;
        TextMeshProUGUI m_Text;
        System.Action m_OnClick;
        float m_NormalWidth, m_HoverWidth;
        Color m_NormalBg, m_HoverBg;
        bool m_Hovered;

        public void Setup(RectTransform rect, Image bg, TextMeshProUGUI text, System.Action onClick,
            float normalWidth, float hoverWidth, Color normalBg, Color hoverBg)
        {
            m_Rect = rect; m_Bg = bg; m_Text = text; m_OnClick = onClick;
            m_NormalWidth = normalWidth; m_HoverWidth = hoverWidth;
            m_NormalBg = normalBg; m_HoverBg = hoverBg;
        }

        public void OnPointerEnter(PointerEventData eventData) => m_Hovered = true;
        public void OnPointerExit(PointerEventData eventData) => m_Hovered = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) m_OnClick?.Invoke();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime * 16f;
            float targetWidth = m_Hovered ? m_HoverWidth : m_NormalWidth;
            Vector2 size = m_Rect.sizeDelta;
            size.x = Mathf.Lerp(size.x, targetWidth, dt);
            m_Rect.sizeDelta = size;
            m_Bg.color = Color.Lerp(m_Bg.color, m_Hovered ? m_HoverBg : m_NormalBg, dt);
            m_Text.color = Color.Lerp(m_Text.color, m_Hovered ? Color.white : new Color(0.82f, 0.85f, 0.88f, 1f), dt);
        }
    }
}