using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Ajuste em jogo da posição do sprite da arma na mão (só no Editor e em builds de desenvolvimento; adicionado pelo
    // WeaponSpriteHUD). F10 abre e fecha: o jogo pausa, o cursor fica livre e um painel mostra os números da ficha.
    // Arrastar a arma com o botão esquerdo move o sprite; a rodinha muda o tamanho; o botão direito (ou arrastar a cruz
    // ciano) põe a ponta do cano, que é de onde o tiro sai (a linha tracejada vai dela até o centro da mira). Setas
    // movem 1 pixel (com Alt, movem a ponta do cano). Os botões do painel giram o sprite, mudam o campo de visão só
    // para conferir o tamanho, desfazem e salvam. As mudanças vão direto para a ficha da arma (WeaponDefinition), e
    // "Salvar na ficha" grava o asset; fora do Editor, os valores vão para a área de transferência.
    public class WeaponSpriteTuner : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        const float k_PanelWidth = 380f;
        const float k_MarkerGrab = 16f;

        enum Drag { None, Weapon, Muzzle }

        struct Snapshot
        {
            public float Height, Tilt;
            public Vector2 Offset, Muzzle;
            public bool Override;
        }

        WeaponSpriteHUD m_Hud;
        PlayerWeaponsManager m_Weapons;
        WeaponDefinition m_Definition;
        Snapshot m_Snapshot;
        Drag m_Drag;
        Vector2 m_LastMouse;
        float m_PreviousTimeScale = 1f;
        Rect m_PanelRect;
        string m_Status = "";
        GUIStyle m_Label;

        void Awake() => m_Hud = GetComponent<WeaponSpriteHUD>();

        void OnDisable()
        {
            if (IsOpen)
                SetOpen(false);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (!m_Weapons && PlayerStats.Instance)
                m_Weapons = PlayerStats.Instance.GetComponent<PlayerWeaponsManager>();

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (IsOpen)
                    SetOpen(false);
                else if (CanOpen())
                    SetOpen(true);
            }

            if (!IsOpen)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame || !m_Hud.Showing)
            {
                SetOpen(false);
                return;
            }

            // Trocar de arma no meio (não acontece com o jogo pausado, mas por garantia) começa outro desfazer
            if (m_Hud.ActiveDefinition != m_Definition)
                TakeSnapshot();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateMouse();
            UpdateKeys(keyboard);
        }

        bool CanOpen() =>
            m_Hud.Showing && m_Hud.ActiveDefinition &&
            !(GameMenus.Instance && GameMenus.Instance.IsOpen) &&
            !(ItemDraft.Instance && ItemDraft.Instance.IsOpen) &&
            !(ShopUI.Instance && ShopUI.Instance.IsOpen);

        void SetOpen(bool open)
        {
            IsOpen = open;
            m_Drag = Drag.None;
            if (open)
            {
                m_PreviousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                TakeSnapshot();
                m_Status = "";
            }
            else
            {
                Time.timeScale = m_PreviousTimeScale > 0f ? m_PreviousTimeScale : 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void TakeSnapshot()
        {
            m_Definition = m_Hud.ActiveDefinition;
            if (!m_Definition)
                return;
            m_Snapshot = new Snapshot
            {
                Height = m_Definition.SpriteHeight, Tilt = m_Definition.SpriteTilt, Offset = m_Definition.SpriteOffset,
                Muzzle = m_Definition.SpriteMuzzle, Override = m_Definition.OverrideSpriteMuzzle,
            };
        }

        // ---------- mouse e teclado ----------

        void UpdateMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !m_Definition)
                return;
            Vector2 position = mouse.position.ReadValue();
            // IMGUI conta de cima para baixo; o mouse, de baixo para cima
            bool overPanel = m_PanelRect.Contains(new Vector2(position.x, Screen.height - position.y));

            if (mouse.leftButton.wasPressedThisFrame && !overPanel)
            {
                if (Vector2.Distance(position, m_Hud.MuzzleScreen) <= k_MarkerGrab)
                    m_Drag = Drag.Muzzle;
                else if (m_Hud.ScreenToUv(position, out _))
                    m_Drag = Drag.Weapon;
            }

            if (mouse.rightButton.wasPressedThisFrame && !overPanel)
                m_Drag = Drag.Muzzle;
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
                m_Drag = Drag.None;

            if (m_Drag == Drag.Weapon)
                MoveWeapon(position - m_LastMouse);
            else if (m_Drag == Drag.Muzzle)
                PlaceMuzzle(position);

            float scroll = mouse.scroll.ReadValue().y;
            if (!overPanel && Mathf.Abs(scroll) > 0.01f)
                Resize(Mathf.Sign(scroll) * (Keyboard.current.shiftKey.isPressed ? 0.002f : 0.01f));
            m_LastMouse = position;
        }

        void UpdateKeys(Keyboard keyboard)
        {
            Vector2 step = Vector2.zero;
            if (keyboard.leftArrowKey.wasPressedThisFrame) step.x -= 1f;
            if (keyboard.rightArrowKey.wasPressedThisFrame) step.x += 1f;
            if (keyboard.upArrowKey.wasPressedThisFrame) step.y += 1f;
            if (keyboard.downArrowKey.wasPressedThisFrame) step.y -= 1f;
            if (step == Vector2.zero || !m_Definition)
                return;
            if (keyboard.altKey.isPressed)
                PlaceMuzzle(m_Hud.MuzzleScreen + step);
            else
                MoveWeapon(step);
        }

        void MoveWeapon(Vector2 pixels)
        {
            if (pixels == Vector2.zero)
                return;
            m_Definition.SpriteOffset += pixels * m_Hud.OffsetPerPixel;
            Changed();
        }

        void PlaceMuzzle(Vector2 screen)
        {
            if (!m_Hud.ScreenToUv(screen, out Vector2 uv))
                return;
            m_Definition.SpriteMuzzle = new Vector2(Round(uv.x), Round(uv.y));
            if (!m_Hud.ActiveImported)
                m_Definition.OverrideSpriteMuzzle = true;
            Changed();
        }

        void Resize(float amount)
        {
            m_Definition.SpriteHeight = Mathf.Clamp(m_Definition.SpriteHeight + amount, 0.1f, 1.5f);
            Changed();
        }

        void Changed()
        {
            m_Status = "alterado (não salvo)";
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_Definition);
#endif
        }

        static float Round(float value) => Mathf.Round(value * 10000f) / 10000f;

        // ---------- painel ----------

        void OnGUI()
        {
            if (!IsOpen || !m_Definition)
                return;
            m_Label ??= new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, wordWrap = true };

            DrawMarkers();

            m_PanelRect = new Rect(16f, 16f, k_PanelWidth, Screen.height - 32f);
            GUILayout.BeginArea(m_PanelRect, GUI.skin.box);
            WeaponDefinition d = m_Definition;
            GUILayout.Label($"<b><size=17>AJUSTE DA ARMA</size></b>  ·  {d.DisplayName}  <color=#8A96A4>(F10 fecha)</color>",
                m_Label);
            GUILayout.Label("<color=#B8C4D0>Arrastar a arma: mover · rodinha: tamanho (Shift: fino)\n" +
                            "Botão direito ou arrastar a cruz ciano: ponta do cano\n" +
                            "Setas: 1 pixel · Alt + setas: ponta do cano 1 pixel\n" +
                            "A linha tracejada vai da ponta do cano ao centro da mira.</color>", m_Label);
            GUILayout.Space(6f);

            float fov = m_Weapons ? m_Weapons.DefaultFov : m_Hud.ReferenceFov;
            string muzzle = m_Hud.ActiveImported || d.OverrideSpriteMuzzle ? "manual" : "automática (desenho)";
            GUILayout.Label(
                $"Altura (SpriteHeight): <b>{d.SpriteHeight:0.000}</b>\n" +
                $"Deslocamento (SpriteOffset): <b>{d.SpriteOffset.x:0.000}, {d.SpriteOffset.y:0.000}</b>\n" +
                $"Giro (SpriteTilt): <b>{d.SpriteTilt:0}°</b>\n" +
                $"Ponta do cano (SpriteMuzzle): <b>{m_Hud.ActiveMuzzleUv.x:0.0000}, {m_Hud.ActiveMuzzleUv.y:0.0000}</b> " +
                $"({muzzle})\n" +
                $"Campo de visão: <b>{fov:0}°</b> (referência {m_Hud.ReferenceFov:0}°)", m_Label);

            Row("Altura", () => Resize(-0.01f), () => Resize(0.01f));
            Row("Giro", () => Tilt(-1f), () => Tilt(1f));
            Row("Campo de visão", () => SetFov(fov - 5f), () => SetFov(fov + 5f));
            if (GUILayout.Button($"Campo de visão de referência ({m_Hud.ReferenceFov:0}°)"))
                SetFov(m_Hud.ReferenceFov);
            if (!m_Hud.ActiveImported && d.OverrideSpriteMuzzle && GUILayout.Button("Ponta do cano automática"))
            {
                d.OverrideSpriteMuzzle = false;
                Changed();
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Desfazer tudo"))
                Revert();
            if (GUILayout.Button("Salvar na ficha"))
                Save();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Copiar valores"))
            {
                GUIUtility.systemCopyBuffer = ValuesText();
                m_Status = "valores copiados";
            }

            if (m_Status.Length > 0)
                GUILayout.Label($"<color=#FFD24A>{m_Status}</color>", m_Label);
            GUILayout.EndArea();
        }

        void Row(string label, System.Action minus, System.Action plus)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, m_Label, GUILayout.Width(150f));
            if (GUILayout.Button("−", GUILayout.Width(60f)))
                minus();
            if (GUILayout.Button("+", GUILayout.Width(60f)))
                plus();
            GUILayout.EndHorizontal();
        }

        void Tilt(float degrees)
        {
            m_Definition.SpriteTilt = Mathf.Clamp(m_Definition.SpriteTilt + degrees, -45f, 45f);
            Changed();
        }

        // Só para conferir: muda o campo de visão desta sessão sem gravar nas configurações
        void SetFov(float fov)
        {
            if (!m_Weapons)
                return;
            fov = Mathf.Clamp(fov, 60f, 110f);
            GameSettings.FieldOfView = fov;
            m_Weapons.DefaultFov = fov;
            m_Weapons.SetFov(fov);
        }

        void Revert()
        {
            WeaponDefinition d = m_Definition;
            d.SpriteHeight = m_Snapshot.Height;
            d.SpriteTilt = m_Snapshot.Tilt;
            d.SpriteOffset = m_Snapshot.Offset;
            d.SpriteMuzzle = m_Snapshot.Muzzle;
            d.OverrideSpriteMuzzle = m_Snapshot.Override;
            Changed();
            m_Status = "valores de quando o ajuste abriu";
        }

        void Save()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_Definition);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(m_Definition);
            m_Status = $"salvo em {m_Definition.name}.asset";
#else
            GUIUtility.systemCopyBuffer = ValuesText();
            m_Status = "fora do Editor não grava: valores copiados";
#endif
            TakeSnapshot();
            Debug.Log($"[Ícaro] Sprite da arma {m_Definition.name}: {ValuesText()}");
        }

        string ValuesText()
        {
            WeaponDefinition d = m_Definition;
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(c, "SpriteHeight {0:0.000} · SpriteOffset ({1:0.000}, {2:0.000}) · SpriteTilt {3:0} · " +
                                    "SpriteMuzzle ({4:0.0000}, {5:0.0000}) · OverrideSpriteMuzzle {6}",
                d.SpriteHeight, d.SpriteOffset.x, d.SpriteOffset.y, d.SpriteTilt, d.SpriteMuzzle.x, d.SpriteMuzzle.y,
                d.OverrideSpriteMuzzle);
        }

        // Cruz ciano na ponta do cano, cruz branca no centro da mira e uma linha tracejada entre elas
        void DrawMarkers()
        {
            var muzzle = new Vector2(m_Hud.MuzzleScreen.x, Screen.height - m_Hud.MuzzleScreen.y);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Color previous = GUI.color;

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            float length = Vector2.Distance(muzzle, center);
            for (float t = 0f; t < length; t += 14f)
            {
                Vector2 p = Vector2.Lerp(muzzle, center, t / Mathf.Max(1f, length));
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), Texture2D.whiteTexture);
            }

            Cross(center, Color.white, 10f);
            Cross(muzzle, new Color(0.3f, 1f, 1f), 14f);
            GUI.color = previous;
        }

        static void Cross(Vector2 at, Color color, float size)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(at.x - size - 1f, at.y - 2f, size * 2f + 2f, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(at.x - 2f, at.y - size - 1f, 4f, size * 2f + 2f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x - size, at.y - 1f, size * 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(at.x - 1f, at.y - size, 2f, size * 2f), Texture2D.whiteTexture);
        }
    }
}
