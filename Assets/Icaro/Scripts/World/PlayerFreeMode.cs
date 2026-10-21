using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Modo livre para tirar prints (só no Editor e em builds de desenvolvimento; adicionado pelo PlayerStats).
    // F11 liga e desliga: o jogador fica imortal e voa atravessando tudo (sem gravidade nem colisão), olhando com o
    // mouse como sempre. WASD (ou as teclas de movimento configuradas) anda para onde a câmera olha, Espaço sobe,
    // Ctrl ou C desce, Shift acelera e a rodinha muda a velocidade. H esconde o HUD (1x: tudo menos a arma; 2x: a
    // arma também; 3x: volta). P salva um print sem o HUD do modo livre na pasta Prints/ da raiz do projeto.
    // Telas que pausam o jogo (pausa, cartas, loja) aparecem mesmo com o HUD escondido. Desligar o modo devolve o
    // controle normal (o jogador cai de onde estiver, ainda sem dano no frame da volta).
    [DefaultExecutionOrder(-200)]
    public class PlayerFreeMode : MonoBehaviour
    {
        public static PlayerFreeMode Instance { get; private set; }

        [Tooltip("Velocidade do voo (m/s); a rodinha muda em jogo")]
        public float Speed = 10f;

        [Tooltip("Multiplicador com Shift")]
        public float FastMultiplier = 3f;

        [Tooltip("Quão rápido a velocidade chega na pedida (maior = mais seco)")]
        public float Sharpness = 8f;

        public bool Active { get; private set; }

        // 0 = HUD normal, 1 = sem HUD (arma visível), 2 = sem HUD e sem arma
        int m_HudMode;

        PlayerCharacterController m_Controller;
        PlayerInputHandler m_Input;
        PlayerWeaponsManager m_Weapons;
        PlayerDash m_Dash;
        Health m_Health;
        float m_Pitch;
        Vector3 m_Velocity;
        bool m_DashWasEnabled;

        readonly List<Canvas> m_HiddenCanvases = new List<Canvas>();
        float m_NextScan;
        Camera m_HiddenWeaponCamera;

        int m_HideLabelFrames;
        string m_Message;
        float m_MessageUntil;
        GUIStyle m_Style;

        static readonly FieldInfo s_PitchField =
            typeof(PlayerCharacterController).GetField("m_CameraVerticalAngle",
                BindingFlags.Instance | BindingFlags.NonPublic);

        void Awake()
        {
            Instance = this;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
            m_Weapons = GetComponent<PlayerWeaponsManager>();
            m_Health = GetComponent<Health>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnDisable()
        {
            if (Active)
                SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (active == Active || !m_Controller)
                return;
            Active = active;
            if (!m_Dash)
                m_Dash = GetComponent<PlayerDash>();

            if (active)
            {
                // Assume a câmera de onde ela estava (o ângulo vertical do controle do Microgame é privado)
                float x = m_Controller.PlayerCamera.transform.localEulerAngles.x;
                m_Pitch = x > 180f ? x - 360f : x;
                m_Velocity = Vector3.zero;
                m_Controller.CharacterVelocity = Vector3.zero;
                m_Controller.enabled = false;
                if (m_Dash)
                {
                    m_DashWasEnabled = m_Dash.enabled;
                    m_Dash.enabled = false;
                }

                Show("MODO LIVRE ligado");
            }
            else
            {
                s_PitchField?.SetValue(m_Controller, m_Pitch);
                m_Controller.CharacterVelocity = Vector3.zero;
                m_Controller.enabled = true;
                if (m_Dash)
                    m_Dash.enabled = m_DashWasEnabled;
                if (m_Health)
                {
                    PlayerDefenses defenses = GetComponent<PlayerDefenses>();
                    m_Health.Invincible = defenses && defenses.IsHitInvulnerable;
                }

                SetHudMode(0);
                Show("MODO LIVRE desligado");
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f11Key.wasPressedThisFrame)
                SetActive(!Active);
            if (!Active)
                return;

            KeepAlive();
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame)
                    SetHudMode((m_HudMode + 1) % 3);
                if (keyboard.pKey.wasPressedThisFrame)
                    StartCoroutine(Capture());
            }

            if (Time.timeScale > 0f)
            {
                Look();
                Fly(keyboard);
            }

            UpdateHud();
        }

        // Depois de tudo, de novo: nenhum dano do frame fica
        void LateUpdate()
        {
            if (Active)
                KeepAlive();
        }

        void KeepAlive()
        {
            if (!m_Health)
                return;
            m_Health.Invincible = true;
            if (m_Health.CurrentHealth > 0f)
                m_Health.CurrentHealth = m_Health.MaxHealth;
        }

        // ---------- voo ----------

        void Look()
        {
            if (!m_Input)
                return;
            float speed = m_Controller.RotationSpeed * m_Controller.RotationMultiplier;
            transform.Rotate(0f, m_Input.GetLookInputsHorizontal() * speed, 0f, Space.Self);
            m_Pitch = Mathf.Clamp(m_Pitch + m_Input.GetLookInputsVertical() * speed, -89f, 89f);
            m_Controller.PlayerCamera.transform.localEulerAngles = new Vector3(m_Pitch, 0f, 0f);
        }

        void Fly(Keyboard keyboard)
        {
            Vector3 move = m_Input ? m_Input.GetMoveInput() : Vector3.zero;
            Transform view = m_Controller.PlayerCamera.transform;
            Vector3 wish = view.forward * move.z + view.right * move.x;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.isPressed)
                    wish += Vector3.up;
                if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed)
                    wish += Vector3.down;

                float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Speed = Mathf.Clamp(Speed * (scroll > 0f ? 1.2f : 1f / 1.2f), 1f, 120f);
                    Show($"Velocidade {Speed:0.#} m/s");
                }
            }

            float target = Speed * (keyboard != null && keyboard.leftShiftKey.isPressed ? FastMultiplier : 1f);
            Vector3 desired = Vector3.ClampMagnitude(wish, 1f) * target;
            // Tempo sem escala: o multiplicador de Kairós não mexe no jogador, e a câmera fica lisa
            float dt = Time.unscaledDeltaTime;
            m_Velocity = Vector3.Lerp(m_Velocity, desired, 1f - Mathf.Exp(-Sharpness * dt));
            transform.position += m_Velocity * dt;
        }

        // ---------- HUD ----------

        void SetHudMode(int mode)
        {
            m_HudMode = mode;
            RestoreHud();
            m_NextScan = 0f;
            if (mode > 0)
                Show(mode == 1 ? "HUD escondido" : "HUD e arma escondidos");
        }

        void UpdateHud()
        {
            // Pausa, cartas e loja precisam aparecer
            if (m_HudMode == 0 || Time.timeScale <= 0f)
            {
                RestoreHud();
                return;
            }

            if (Time.unscaledTime < m_NextScan)
                return;
            m_NextScan = Time.unscaledTime + 0.25f;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.enabled || canvas.GetComponentInParent<GameMenus>())
                    continue;
                canvas.enabled = false;
                m_HiddenCanvases.Add(canvas);
            }

            Camera weaponCamera = m_Weapons ? m_Weapons.WeaponCamera : null;
            if (m_HudMode == 2 && weaponCamera && weaponCamera.enabled)
            {
                weaponCamera.enabled = false;
                m_HiddenWeaponCamera = weaponCamera;
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
            if (m_HiddenWeaponCamera)
                m_HiddenWeaponCamera.enabled = true;
            m_HiddenWeaponCamera = null;
        }

        // ---------- prints ----------

        IEnumerator Capture()
        {
            // Um frame sem o texto do modo livre antes de capturar
            m_HideLabelFrames = 3;
            yield return null;
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Prints");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"print_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            Show($"Print salvo: Prints/{Path.GetFileName(path)}");
            Debug.Log($"[Ícaro] Print salvo em {path}");
        }

        void Show(string message)
        {
            m_Message = message;
            m_MessageUntil = Time.unscaledTime + 2.5f;
        }

        void OnGUI()
        {
            if (m_HideLabelFrames > 0)
            {
                if (Event.current.type == EventType.Repaint)
                    m_HideLabelFrames--;
                return;
            }

            bool message = Time.unscaledTime < m_MessageUntil;
            if (!Active && !message)
                return;

            m_Style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            var text = new System.Text.StringBuilder();
            if (Active && m_HudMode == 0)
                text.Append("<b>MODO LIVRE</b> (F11 sai) · imortal · WASD anda · Espaço sobe · Ctrl/C desce · " +
                            $"Shift rápido · rodinha velocidade ({Speed:0.#} m/s) · H esconde o HUD · P salva print\n");
            if (message)
                text.Append(m_Message);
            if (text.Length == 0)
                return;

            var rect = new Rect(16f, Screen.height - 64f, Screen.width - 32f, 56f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), StripTags(text.ToString()), m_Style);
            GUI.color = Color.white;
            GUI.Label(rect, text.ToString(), m_Style);
        }

        static string StripTags(string text) => text.Replace("<b>", "").Replace("</b>", "");
    }
}
