using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Abertura da run: o ÍCARO-9 religa caído ao lado da cápsula. Versão completa (Jogar no título, ~15 s): tela preta
    // com o registro de inicialização sendo escrito, os "olhos" abrem de cima e de baixo com linhas de varredura e a
    // vista desfocada (MenuBackdropFx), uma piscada, a câmera deitada olhando o céu senta e levanta até a altura de
    // sempre, e o visor liga piscando com "ÍCARO-9 // SISTEMAS ONLINE". Versão curta (ao recomeçar a run, ~3,5 s): só o
    // visor religando, já em pé. Qualquer tecla ou clique pula para o fim (o visor ligando). Durante a abertura o
    // jogador não se mexe nem atira (o cursor fica solto e invisível, então o Input do Microgame não lê nada), não
    // leva dano, e o HUD e a arma ficam escondidos até o visor ligar. A câmera é posta por cima da do
    // PlayerCharacterController no LateUpdate e termina na pose dele, sem salto. Tudo em tempo sem escala.
    [DefaultExecutionOrder(500)]
    public class IntroSequence : MonoBehaviour
    {
        public static bool Playing => s_Instance;

        static IntroSequence s_Instance;

        // Tempos da versão completa (segundos)
        static readonly (string Text, bool Warn)[] k_Log =
        {
            ("ÍCARO-9 // REINICIALIZANDO...", false),
            ("NÚCLEO TEMPORAL .............. OK", false),
            ("SERVOMOTORES ................. 87%", false),
            ("SENSORES ÓPTICOS ............. OK", false),
            ("ALERTA: DESCOMPASSO TEMPORAL DETECTADO", true),
        };

        // Registro: letras por segundo, pausa entre uma linha e a próxima e quanto tempo o registro inteiro fica na tela
        // depois da última linha (pedido do usuário: as frases passavam rápido demais para ler)
        const float k_LogStart = 0.6f;
        const float k_CharsPerSecond = 30f;
        const float k_LinePause = 0.45f;
        const float k_LogHold = 1.6f;

        // O resto conta a partir de quando os olhos abrem (o fim do registro)
        static readonly float k_EyesOpen = LogEnd() + k_LogHold;
        static readonly float k_SitUp = k_EyesOpen + 1.6f;
        static readonly float k_StandUp = k_SitUp + 1.4f;
        static readonly float k_Standing = k_StandUp + 1.0f;
        static readonly float k_VisorOn = k_StandUp + 0.6f;
        static readonly float k_End = k_VisorOn + 2.6f;

        // Versão curta: a primeira linha inteira, legível, e o visor religando
        const float k_ShortEyes = 0.25f;
        static readonly float k_ShortLineEnd = 0.2f + TypeTime(0);
        static readonly float k_ShortVisor = k_ShortLineEnd + 0.3f;
        static readonly float k_ShortEnd = k_ShortVisor + 2.2f;

        static readonly Color k_Cyan = new Color(0.45f, 0.9f, 1f);
        static readonly Color k_Amber = new Color(1f, 0.7f, 0.3f);


        // Tempo para escrever a linha (os pontos de preenchimento vão mais rápido que as letras)
        static float TypeTime(int index)
        {
            string text = k_Log[index].Text;
            int dots = 0;
            foreach (char c in text)
            {
                if (c == '.')
                    dots++;
            }

            return (text.Length - dots) / k_CharsPerSecond + dots / (k_CharsPerSecond * 3f);
        }

        // Quando cada linha começa: depois que a anterior termina, com uma pausa
        static float LineStart(int index)
        {
            float t = k_LogStart;
            for (int i = 0; i < index; i++)
                t += TypeTime(i) + k_LinePause;
            return t;
        }

        static float LogEnd() => LineStart(k_Log.Length - 1) + TypeTime(k_Log.Length - 1);

        bool m_Full;
        float m_Time;
        bool m_Skipped;
        int m_LogShown;
        bool m_VisorShown;
        bool m_WeaponRaised;
        bool m_Online;

        PlayerCharacterController m_Player;
        Transform m_Camera;
        Camera m_WeaponCamera;
        bool m_WeaponCameraWasOn;
        Health m_Health;
        bool m_WasInvincible;

        Canvas m_Canvas;
        Image m_TopLid;
        Image m_BottomLid;
        RawImage m_Scanlines;
        RectTransform m_LogRoot;
        readonly List<TextMeshProUGUI> m_LogLines = new List<TextMeshProUGUI>();
        TextMeshProUGUI m_OnlineText;
        Texture2D m_ScanTexture;

        readonly List<Canvas> m_Hidden = new List<Canvas>();
        float m_NextScan;

        // Começa a abertura (completa ao sair do título, curta ao recomeçar a run)
        public static void Play(bool full)
        {
            if (s_Instance)
                Destroy(s_Instance.gameObject);
            var go = new GameObject("Abertura");
            s_Instance = go.AddComponent<IntroSequence>();
            s_Instance.m_Full = full;
        }

        void Awake()
        {
            m_Player = FindAnyObjectByType<PlayerCharacterController>();
            if (m_Player)
            {
                m_Camera = m_Player.PlayerCamera ? m_Player.PlayerCamera.transform : null;
                m_Health = m_Player.GetComponent<Health>();
                var weapons = m_Player.GetComponent<PlayerWeaponsManager>();
                m_WeaponCamera = weapons ? weapons.WeaponCamera : null;
            }

            if (m_Health)
            {
                m_WasInvincible = m_Health.Invincible;
                m_Health.Invincible = true;
            }

            if (m_WeaponCamera)
            {
                m_WeaponCameraWasOn = m_WeaponCamera.enabled;
                m_WeaponCamera.enabled = false;
            }

            BuildOverlay();
            HideCanvases();
            MenuBackdropFx.Target = 1f;
            // A arma começa abaixada (fora da tela) e só levanta depois que o ÍCARO-9 fica de pé
            WeaponSpriteHUD.Lower(this, true);
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            Finish();
            if (m_ScanTexture)
                Destroy(m_ScanTexture);
        }

        bool m_Finished;

        // Devolve tudo como estava (também se a cena trocar no meio)
        void Finish()
        {
            if (m_Finished)
                return;
            m_Finished = true;
            RestoreCamera();
            WeaponSpriteHUD.Raise(this);
            if (m_Health)
                m_Health.Invincible = m_WasInvincible;
            if (m_WeaponCamera)
                m_WeaponCamera.enabled = m_WeaponCameraWasOn || m_WeaponCamera.enabled;
            RestoreCanvases();
            MenuBackdropFx.Target = 0f;
            if (!(GameMenus.Instance && GameMenus.Instance.IsOpen))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            // Com a pausa aberta (não deveria: Esc pula a abertura), espera
            if (GameMenus.Instance && GameMenus.Instance.IsOpen)
                return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            m_Time += dt;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;

            float visorAt = m_Full ? k_VisorOn : k_ShortVisor;
            float end = m_Full ? k_End : k_ShortEnd;
            if (!m_Skipped && m_Time > 0.4f && m_Time < visorAt && SkipPressed())
            {
                m_Skipped = true;
                m_Time = visorAt - 0.15f;
            }

            if (Time.unscaledTime >= m_NextScan && !m_VisorShown)
            {
                m_NextScan = Time.unscaledTime + 0.25f;
                HideCanvases();
            }

            UpdateLog();
            UpdateEyes();
            UpdateVisor(visorAt);

            // Levanta a arma (em arco, pelo WeaponSpriteHUD) quando o visor termina de piscar e, na versão completa,
            // depois de ficar de pé
            float raiseAt = visorAt + 0.5f;
            if (m_Full && !m_Skipped)
                raiseAt = Mathf.Max(raiseAt, k_Standing + 0.1f);
            if (!m_WeaponRaised && m_Time >= raiseAt)
            {
                m_WeaponRaised = true;
                WeaponSpriteHUD.Raise(this);
            }

            if (m_Time >= end)
                Destroy(gameObject);
        }

        static bool SkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame ||
                   mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        // ---------- câmera ----------

        // Pose de pé da câmera, guardada antes da primeira mudança. O controlador do Microgame só põe a posição da
        // câmera quando a altura muda (agachar), não a cada frame: ler a posição atual como "de pé" somava o recuo de
        // todo frame e jogava a câmera centenas de metros para trás.
        Vector3 m_Stand;
        bool m_HasStand;
        bool m_CameraMoved;

        void LateUpdate()
        {
            if (!m_Camera)
                return;
            if (!m_HasStand)
            {
                m_Stand = m_Camera.localPosition;
                m_HasStand = true;
            }

            if (!m_Full || m_Skipped)
            {
                RestoreCamera();
                return;
            }

            Vector3 stand = m_Stand;
            float t = m_Time;
            float height, pitch, roll, forward;
            if (t < k_SitUp)
            {
                // Deitado olhando o céu, respirando
                float breath = Mathf.Sin(t * 1.6f) * 0.012f;
                height = 0.32f + breath;
                pitch = -68f + Mathf.Sin(t * 0.7f) * 2f;
                roll = 16f;
                forward = -0.5f;
            }
            else if (t < k_StandUp)
            {
                // Senta: o olhar desce do céu para a frente
                float u = Smooth((t - k_SitUp) / (k_StandUp - k_SitUp));
                height = Mathf.Lerp(0.32f, 1.05f, u);
                pitch = Mathf.Lerp(-68f, 18f, u);
                roll = Mathf.Lerp(16f, 4f, u);
                forward = Mathf.Lerp(-0.5f, 0.15f, u);
            }
            else if (t < k_Standing)
            {
                // Levanta até a altura de sempre
                float u = Smooth((t - k_StandUp) / (k_Standing - k_StandUp));
                height = Mathf.Lerp(1.05f, stand.y, u);
                pitch = Mathf.Lerp(18f, 0f, u);
                roll = Mathf.Lerp(4f, 0f, u);
                forward = Mathf.Lerp(0.15f, 0f, u);
            }
            else
            {
                RestoreCamera();
                return;
            }

            m_CameraMoved = true;

            m_Camera.localPosition = new Vector3(stand.x, height, stand.z + forward);
            m_Camera.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        // Devolve a câmera exatamente à pose de pé (uma vez)
        void RestoreCamera()
        {
            if (!m_CameraMoved || !m_Camera || !m_HasStand)
                return;
            m_CameraMoved = false;
            m_Camera.localPosition = m_Stand;
            m_Camera.localRotation = Quaternion.identity;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---------- tela ----------

        void BuildOverlay()
        {
            m_Canvas = ItemUI.CreateCanvas(transform, "AberturaTela", 950);
            Transform root = m_Canvas.transform;

            // Linhas de varredura: textura de 1 x 4 (duas linhas escuras) repetida a cada 4 px de referência
            m_ScanTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
            {
                name = "Abertura_Varredura", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
            };
            m_ScanTexture.SetPixels32(new[]
            {
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0),
            });
            m_ScanTexture.Apply(false, false);
            var scanGo = new GameObject("Varredura", typeof(RectTransform));
            scanGo.transform.SetParent(root, false);
            var scanRect = (RectTransform)scanGo.transform;
            scanRect.anchorMin = Vector2.zero;
            scanRect.anchorMax = Vector2.one;
            scanRect.sizeDelta = Vector2.zero;
            m_Scanlines = scanGo.AddComponent<RawImage>();
            m_Scanlines.texture = m_ScanTexture;
            m_Scanlines.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);
            m_Scanlines.raycastTarget = false;

            m_TopLid = Lid(root, "PalpebraCima", new Vector2(0.5f, 1f));
            m_BottomLid = Lid(root, "PalpebraBaixo", new Vector2(0.5f, 0f));

            m_LogRoot = ItemUI.Rect("Registro", root, new Vector2(0f, 1f), new Vector2(120f, -380f), new Vector2(1200f, 400f));
            for (int i = 0; i < k_Log.Length; i++)
            {
                TextMeshProUGUI line = MenuKit.Label("Linha", m_LogRoot, new Vector2(0f, 1f), new Vector2(0f, -i * 40f),
                    new Vector2(1200f, 36f), 28f, TextAlignmentOptions.TopLeft, k_Log[i].Warn ? k_Amber : k_Cyan,
                    ItemFonts.Bold);
                line.rectTransform.pivot = new Vector2(0f, 1f);
                line.text = "";
                m_LogLines.Add(line);
            }

            m_OnlineText = MenuKit.Label("Online", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(1400f, 60f), 40f, TextAlignmentOptions.Center, k_Cyan, ItemFonts.VisorWide);
            m_OnlineText.characterSpacing = 8f;
            m_OnlineText.text = "ÍCARO-9 // SISTEMAS ONLINE";
            m_OnlineText.alpha = 0f;
        }

        // Metade da tela (de cima ou de baixo), esticada pelas âncoras: cobre qualquer proporção de tela
        static Image Lid(Transform root, string name, Vector2 anchor)
        {
            Image lid = ItemUI.Panel(name, root, anchor, Vector2.zero, Vector2.zero, Color.black);
            lid.rectTransform.anchorMin = new Vector2(0f, anchor.y > 0.5f ? 0.5f : 0f);
            lid.rectTransform.anchorMax = new Vector2(1f, anchor.y > 0.5f ? 1f : 0.5f);
            lid.rectTransform.sizeDelta = Vector2.zero;
            return lid;
        }

        // Registro escrito letra a letra, uma linha por vez; some quando os olhos abrem
        void UpdateLog()
        {
            if (!m_Full)
            {
                // Versão curta: só a primeira linha
                SetLine(0, Mathf.Clamp01((m_Time - 0.2f) / TypeTime(0)), m_Time < k_ShortVisor + 1.2f ? 1f : 0f);
                return;
            }

            float fade = 1f - Mathf.Clamp01((m_Time - k_EyesOpen - 0.3f) / 0.4f);
            for (int i = 0; i < k_Log.Length; i++)
            {
                float typed = Mathf.Clamp01((m_Time - LineStart(i)) / TypeTime(i));
                SetLine(i, typed, fade);
                if (typed > 0f && i >= m_LogShown)
                {
                    m_LogShown = i + 1;
                    UiSounds.Play(k_Log[i].Warn ? UiSounds.Deny : UiSounds.Hover, 0.45f, k_Log[i].Warn ? 1f : 1.5f);
                }
            }
        }

        void SetLine(int index, float typed, float alpha)
        {
            if (index >= m_LogLines.Count)
                return;
            string text = k_Log[index].Text;
            int count = Mathf.RoundToInt(text.Length * typed);
            bool cursor = typed < 1f && typed > 0f || index == m_LogShown - 1 && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f;
            m_LogLines[index].text = text.Substring(0, count) + (cursor && typed > 0f ? "_" : "");
            m_LogLines[index].alpha = Mathf.Ceil(alpha * 4f) / 4f;
            if (index == 0 && !m_Full && m_LogShown == 0 && typed > 0f)
            {
                m_LogShown = 1;
                UiSounds.Play(UiSounds.Hover, 0.45f, 1.5f);
            }
        }

        // Pálpebras: abrem de cima e de baixo, com uma piscada; varredura e desfoque somem aos poucos
        void UpdateEyes()
        {
            float open;
            float scan;
            if (m_Full && !m_Skipped)
            {
                open = Smooth((m_Time - k_EyesOpen) / 0.9f);
                // Piscada
                float blink = m_Time - (k_EyesOpen + 1.1f);
                if (blink > 0f && blink < 0.3f)
                    open *= blink < 0.12f ? 1f - blink / 0.12f : (blink - 0.12f) / 0.18f;
                scan = 0.45f * (1f - Mathf.Clamp01((m_Time - k_EyesOpen) / 3.5f));
                MenuBackdropFx.Target = m_Time < k_EyesOpen + 0.6f ? 1f : m_Time < k_SitUp + 0.5f ? 0.6f : 0f;
            }
            else
            {
                float start = m_Full ? k_VisorOn - 0.15f : k_ShortEyes;
                open = Smooth((m_Time - start) / 0.35f);
                scan = 0.35f * (1f - Mathf.Clamp01((m_Time - start) / 1f));
                MenuBackdropFx.Target = open < 1f ? 0.6f : 0f;
            }

            // Cada pálpebra cobre de 0 a meia tela, em degraus de 1/90 da altura (pixel art); 1 px a mais no meio
            float lid = Mathf.Round((1f - open) * 90f) / 180f;
            m_TopLid.rectTransform.anchorMin = new Vector2(0f, 1f - lid);
            m_BottomLid.rectTransform.anchorMax = new Vector2(1f, lid);
            m_TopLid.rectTransform.offsetMin = new Vector2(0f, lid > 0f ? -1f : 0f);
            m_BottomLid.rectTransform.offsetMax = new Vector2(0f, lid > 0f ? 1f : 0f);
            m_Scanlines.color = new Color(1f, 1f, 1f, scan);
            float scroll = Mathf.Repeat(Time.unscaledTime * 0.5f, 1f);
            m_Scanlines.uvRect = new Rect(0f, scroll, 1f, 1080f / 4f);
        }

        // Visor ligando: o HUD volta piscando (a arma continua abaixada e levanta depois) e o aviso de sistemas online
        // aparece e some
        void UpdateVisor(float visorAt)
        {
            float t = m_Time - visorAt;
            if (t < 0f)
                return;

            if (!m_VisorShown)
            {
                m_VisorShown = true;
                RestoreCanvases();
                UiSounds.Play(UiSounds.Boot, 0.6f);
            }

            // Pisca nos primeiros 0,45 s (liga, desliga, liga, meio, liga)
            bool on = t > 0.45f || t > 0.05f && t < 0.12f || t > 0.2f && t < 0.3f || t > 0.36f;
            if (m_WeaponCamera)
                m_WeaponCamera.enabled = on;

            if (!m_Online && t > 0.3f)
            {
                m_Online = true;
                UiSounds.Play(UiSounds.Confirm, 0.5f);
            }

            float shown = Mathf.Clamp01((t - 0.3f) / 0.15f) * (1f - Mathf.Clamp01((t - 1.9f) / 0.4f));
            bool flicker = t < 0.6f && Mathf.Repeat(Time.unscaledTime * 18f, 1f) < 0.25f;
            m_OnlineText.alpha = Mathf.Ceil(shown * 4f) / 4f * (flicker ? 0.4f : 1f);
            if (!m_Full)
                m_OnlineText.alpha *= 0.85f;
        }

        // ---------- HUD ----------

        void HideCanvases()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.enabled || canvas == m_Canvas ||
                    canvas.GetComponentInParent<GameMenus>())
                    continue;
                canvas.enabled = false;
                m_Hidden.Add(canvas);
            }
        }

        void RestoreCanvases()
        {
            foreach (Canvas canvas in m_Hidden)
            {
                if (canvas)
                    canvas.enabled = true;
            }

            m_Hidden.Clear();
        }
    }
}
