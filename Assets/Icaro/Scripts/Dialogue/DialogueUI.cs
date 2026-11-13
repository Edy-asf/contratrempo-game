using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Caixa de diálogo dos NPCs (criada pelo primeiro DialogueNpc). Embaixo, no meio da tela: painel de vidro com o
    // nome de quem fala numa aba em cima, a fala escrita letra a letra (DialogueText) e, à direita, as respostas.
    // Conversando, o jogador não anda, não atira e a câmera vira e fica olhando para o rosto do NPC; a arma abaixa. O
    // jogo não pausa (fora da luta o mundo já está parado). Sem o tradutor (PlayerTranslator), o nome e a primeira fala
    // aparecem em hieróglifos e a única resposta é "Ignorar"; com ele, cada letra se decodifica.
    // Teclas: Espaço, Enter ou clique pulam a escrita e seguem; W/S ou setas escolhem a resposta, 1 a 4 escolhem
    // direto; Esc sai da conversa. O mouse que se mexe leva o foco.
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        // Há uma conversa aberta (para os menus não abrirem por cima)
        public static bool Talking => Instance && Instance.IsOpen;

        public bool IsOpen { get; private set; }

        const float k_BoxWidth = 1240f, k_BoxHeight = 232f, k_BoxBottom = 56f;
        const float k_TextWidth = 820f;
        const float k_ChoiceWidth = 340f, k_ChoiceHeight = 44f;
        static readonly Color k_Back = new Color(0.03f, 0.045f, 0.07f, 0.9f);
        static readonly Color k_ChoiceBack = new Color(0.07f, 0.09f, 0.12f, 0.85f);
        static readonly Color k_ChoiceLit = new Color(0.1f, 0.14f, 0.19f, 0.97f);

        static readonly FieldInfo s_PitchField = typeof(PlayerCharacterController).GetField("m_CameraVerticalAngle",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Canvas m_PromptCanvas;
        TextMeshProUGUI m_Prompt;
        DialogueNpc m_PromptNpc;

        Canvas m_Canvas;
        CanvasGroup m_Group;
        RectTransform m_Box;
        Image m_Accent, m_NameTab;
        TextMeshProUGUI m_NameLabel, m_LineLabel, m_Hint;
        DialogueText m_Name, m_Line;
        readonly List<Choice> m_Choices = new List<Choice>();

        class Choice
        {
            public RectTransform Root;
            public Image Back, Bar;
            public TextMeshProUGUI Label;
        }

        DialogueNpc m_Npc;
        PlayerCharacterController m_Controller;
        int m_Focus;
        Vector2 m_LastMouse;
        bool m_Closing;
        float m_LastBlip;

        static DialogueSettings Settings => DialogueSettings.Current;

        public static DialogueUI Ensure()
        {
            if (Instance)
                return Instance;
            return new GameObject("Dialogos").AddComponent<DialogueUI>();
        }

        void Awake()
        {
            Instance = this;
            m_PromptCanvas = ItemUI.CreateCanvas(transform, "AvisoConversa", 45);
            m_Prompt = ItemUI.Text("Aviso", m_PromptCanvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f),
                new Vector2(700f, 40f), 28f, TextAlignmentOptions.Center, MenuKit.Accent);
            TMP_FontAsset font = ItemFonts.VisorWide;
            if (font)
                m_Prompt.font = font;
            m_Prompt.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            RestoreHud();
        }

        // ---------- o resto do HUD some durante a conversa ----------

        struct FadedCanvas
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Added;
        }

        readonly List<FadedCanvas> m_Faded = new List<FadedCanvas>();
        float m_HudShown = 1f;
        float m_HudTarget = 1f;

        // Some (false) ou volta (true) com os Canvas raiz ligados do HUD, menos os da conversa e dos menus, pelo alfa de
        // um CanvasGroup (o original volta no fim)
        void FadeHud(bool show)
        {
            m_HudTarget = show ? 1f : 0f;
            if (show || m_Faded.Count > 0)
                return;

            Transform menus = GameMenus.Instance ? GameMenus.Instance.transform : null;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                    continue;
                if (canvas.transform.IsChildOf(transform) || (menus && canvas.transform.IsChildOf(menus)))
                    continue;
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                bool added = !group;
                if (added)
                    group = canvas.gameObject.AddComponent<CanvasGroup>();
                m_Faded.Add(new FadedCanvas { Group = group, Alpha = group.alpha, Added = added });
            }
        }

        void Update()
        {
            if (m_Faded.Count == 0 || Mathf.Approximately(m_HudShown, m_HudTarget) && m_HudTarget < 1f)
                return;

            float speed = 1f / Mathf.Max(0.01f, Settings.HudFadeTime);
            m_HudShown = Mathf.MoveTowards(m_HudShown, m_HudTarget, speed * Time.unscaledDeltaTime);
            float k = Mathf.SmoothStep(0f, 1f, m_HudShown);
            foreach (FadedCanvas faded in m_Faded)
                if (faded.Group)
                    faded.Group.alpha = faded.Alpha * k;

            if (m_HudShown >= 1f)
                RestoreHud();
        }

        void RestoreHud()
        {
            foreach (FadedCanvas faded in m_Faded)
            {
                if (!faded.Group)
                    continue;
                if (faded.Added)
                    Destroy(faded.Group);
                else
                    faded.Group.alpha = faded.Alpha;
            }

            m_Faded.Clear();
            m_HudShown = 1f;
        }

        // ---------- aviso perto do NPC ----------

        public void SetPrompt(DialogueNpc npc, bool near)
        {
            if (near)
                m_PromptNpc = npc;
            else if (m_PromptNpc == npc)
                m_PromptNpc = null;

            bool show = m_PromptNpc && !IsOpen;
            if (m_Prompt.gameObject.activeSelf != show)
                m_Prompt.gameObject.SetActive(show);
            if (show)
            {
                string who = PlayerTranslator.Owned || (m_PromptNpc.Dialogue && !m_PromptNpc.Dialogue.IgnoreWithoutTranslator)
                    ? m_PromptNpc.DisplayName
                    : "???";
                m_Prompt.text = $"[{GameSettings.KeyName(GameSettings.ShopKey)}] Conversar  <color=#{ItemUI.Hex(MenuKit.Muted)}>{who}</color>";
            }
        }

        // ---------- conversa ----------

        public void Open(DialogueNpc npc, DialogueDefinition dialogue)
        {
            if (IsOpen || !dialogue || dialogue.Nodes.Count == 0)
                return;
            StartCoroutine(Run(npc, dialogue));
        }

        IEnumerator Run(DialogueNpc npc, DialogueDefinition dialogue)
        {
            IsOpen = true;
            m_Npc = npc;
            m_Closing = false;
            SetPrompt(npc, false);
            m_Prompt.gameObject.SetActive(false);
            LockPlayer(true);
            FadeHud(false);
            // Primeiro o HUD some; só depois a caixa abre
            while (m_Faded.Count > 0 && m_HudShown > 0f)
                yield return null;
            Build(dialogue);
            UiSounds.Play(UiSounds.Whoosh, 0.35f, 1.3f);

            bool understood = PlayerTranslator.Owned || !dialogue.IgnoreWithoutTranslator;
            m_Name.Show(dialogue.Speaker, understood, true);

            // Abre subindo
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                SetOpen(t / 0.18f);
                yield return null;
            }
            SetOpen(1f);

            yield return WaitRelease();

            int index = 0;
            while (index >= 0 && index < dialogue.Nodes.Count && !m_Closing)
            {
                DialogueNode node = dialogue.Nodes[index];
                DialogueEvents.Raise(node.Event, npc);
                m_Line.Show(node.Text, understood);
                m_LastBlip = 0f;
                ShowChoices(null);
                m_Hint.text = "";

                // Escrevendo: Espaço/Enter/clique completa
                while (!m_Line.Done && !m_Closing)
                {
                    Blip(understood);
                    if (Escape())
                        m_Closing = true;
                    else if (Advance())
                    {
                        m_Line.Complete();
                        yield return WaitRelease();
                        break;
                    }

                    yield return null;
                }

                if (m_Closing)
                    break;

                // Sem o tradutor: as respostas sem palavras da conversa e "Ignorar"
                List<DialogueChoice> choices = understood ? node.Choices : Wordless(dialogue);
                if (choices != null && choices.Count > 0)
                {
                    ShowChoices(choices);
                    m_Hint.text = Hint("W/S", "escolher", "Enter", "confirmar");
                    int picked = -1;
                    while (picked < 0 && !m_Closing)
                    {
                        if (Escape())
                            m_Closing = true;
                        picked = ReadChoice(choices.Count);
                        UpdateChoices();
                        yield return null;
                    }

                    if (m_Closing)
                        break;
                    UiSounds.Play(UiSounds.Confirm, 0.5f);
                    DialogueChoice choice = choices[picked];
                    yield return WaitRelease();
                    DialogueEvents.Raise(choice.Event, npc);
                    if (!understood)
                        break;
                    index = dialogue.Follow(index, choice.Next);
                }
                else
                {
                    m_Hint.text = Hint("Espaço", "continuar", "Esc", "sair");
                    while (!Advance() && !m_Closing)
                    {
                        if (Escape())
                            m_Closing = true;
                        yield return null;
                    }

                    if (m_Closing)
                        break;
                    UiSounds.Play(UiSounds.Click, 0.35f, 1.2f);
                    yield return WaitRelease();
                    index = dialogue.Follow(index, node.Next);
                }
            }

            // Fecha descendo; o HUD volta depois
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                SetOpen(1f - t / 0.15f);
                yield return null;
            }

            yield return WaitRelease();
            Clear();
            FadeHud(true);
            LockPlayer(false);
            IsOpen = false;
            m_Npc = null;
        }

        static List<DialogueChoice> Wordless(DialogueDefinition dialogue)
        {
            if (dialogue.WithoutTranslator == null || dialogue.WithoutTranslator.Count == 0)
                return s_Ignore;
            var list = new List<DialogueChoice>(dialogue.WithoutTranslator);
            list.AddRange(s_Ignore);
            return list;
        }

        static readonly List<DialogueChoice> s_Ignore = new List<DialogueChoice>
        {
            new DialogueChoice { Label = "Ignorar", Next = DialogueDefinition.End },
        };

        // Um "tique" a cada duas letras novas (agudo com o tradutor, grave e áspero nos hieróglifos)
        void Blip(bool understood)
        {
            if (m_Line.Revealed - m_LastBlip < 2f)
                return;
            m_LastBlip = m_Line.Revealed;
            UiSounds.Play(UiSounds.Hover, 0.22f, understood ? Random.Range(1.1f, 1.3f) : Random.Range(0.55f, 0.7f));
        }

        // ---------- jogador e câmera ----------

        void LockPlayer(bool locked)
        {
            if (locked)
            {
                m_Controller = BossTarget.Player;
                if (m_Controller)
                    m_Controller.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
                WeaponSpriteHUD.Lower(this);
                StartCoroutine(TurnCamera());
                return;
            }

            if (m_Controller)
            {
                // O controle do Microgame continua do olhar da conversa
                Camera cam = m_Controller.PlayerCamera;
                if (cam && s_PitchField != null)
                {
                    float pitch = cam.transform.localEulerAngles.x;
                    if (pitch > 180f)
                        pitch -= 360f;
                    s_PitchField.SetValue(m_Controller, pitch);
                }

                m_Controller.enabled = true;
            }

            m_Controller = null;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            WeaponSpriteHUD.Raise(this);
        }

        // Vira o corpo (giro) e a câmera (inclinação) até o rosto do NPC, e fica olhando enquanto a conversa dura
        IEnumerator TurnCamera()
        {
            PlayerCharacterController controller = m_Controller;
            if (!controller || !controller.PlayerCamera || !m_Npc)
                yield break;
            Transform body = controller.transform, cam = controller.PlayerCamera.transform;
            float startYaw = body.eulerAngles.y;
            float startPitch = cam.localEulerAngles.x;
            if (startPitch > 180f)
                startPitch -= 360f;
            float turn = Mathf.Max(0.05f, Settings.TurnTime);

            for (float t = 0f; IsOpen && m_Npc && controller; t += Time.unscaledDeltaTime)
            {
                Vector3 to = m_Npc.LookPoint - cam.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                float yaw = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat).eulerAngles.y : startYaw;
                float pitch = -Mathf.Atan2(to.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -60f, 60f);
                float k = Mathf.Clamp01(t / turn);
                k = k * k * (3f - 2f * k);
                body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(startYaw, yaw, k), 0f);
                cam.localEulerAngles = new Vector3(Mathf.LerpAngle(startPitch, pitch, k), 0f, 0f);
                yield return null;
            }
        }

        // ---------- entrada ----------

        static bool Advance()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                        keyboard.numpadEnterKey.wasPressedThisFrame ||
                                        keyboard[GameSettings.ShopKey].wasPressedThisFrame) ||
                   mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        static bool Escape() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        static bool AnyHeld()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed ||
                                        keyboard.escapeKey.isPressed || keyboard[GameSettings.ShopKey].isPressed) ||
                   mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
        }

        static IEnumerator WaitRelease()
        {
            while (AnyHeld())
                yield return null;
        }

        int ReadChoice(int count)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                    MoveFocus(-1, count);
                if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                    MoveFocus(1, count);
                for (int i = 0; i < count && i < 4; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        return i;
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                    keyboard.numpadEnterKey.wasPressedThisFrame)
                    return m_Focus;
            }

            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                bool moved = (position - m_LastMouse).sqrMagnitude > 4f;
                m_LastMouse = position;
                for (int i = 0; i < count; i++)
                {
                    if (!ItemUI.MouseOver(m_Choices[i].Root))
                        continue;
                    if (moved && m_Focus != i)
                    {
                        m_Focus = i;
                        UiSounds.Play(UiSounds.Hover, 0.4f);
                    }
                    if (mouse.leftButton.wasPressedThisFrame)
                        return i;
                }
            }

            return -1;
        }

        void MoveFocus(int step, int count)
        {
            m_Focus = (m_Focus + step + count) % count;
            UiSounds.Play(UiSounds.Hover, 0.4f);
        }

        // ---------- interface ----------

        void Build(DialogueDefinition dialogue)
        {
            Clear();
            m_Canvas = ItemUI.CreateCanvas(transform, "Conversa", 95);
            m_Group = m_Canvas.gameObject.AddComponent<CanvasGroup>();
            Transform root = m_Canvas.transform;
            var bottom = new Vector2(0.5f, 0f);
            Color accent = dialogue.SpeakerColor;

            m_Box = ItemUI.Rect("Caixa", root, bottom, new Vector2(0f, k_BoxBottom), new Vector2(k_BoxWidth, k_BoxHeight));
            Image back = ItemUI.Fill("Fundo", m_Box, 0f, k_Back);
            back.sprite = MenuArt.GlassFill;
            back.type = Image.Type.Sliced;
            Image line = ItemUI.Fill("Contorno", m_Box, 0f, new Color(accent.r, accent.g, accent.b, 0.35f));
            line.sprite = MenuArt.GlassLine;
            line.type = Image.Type.Sliced;
            m_Accent = ItemUI.Panel("Traco", m_Box, new Vector2(0f, 1f), new Vector2(MenuArt.GlassCut + 14f, -2f),
                new Vector2(160f, 4f), accent);
            m_Accent.rectTransform.pivot = new Vector2(0f, 1f);

            // Aba do nome em cima, à esquerda
            m_NameTab = ItemUI.Panel("Aba", m_Box, new Vector2(0f, 1f), new Vector2(28f, 44f), new Vector2(380f, 46f),
                new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.3f, 0.95f));
            m_NameTab.rectTransform.pivot = new Vector2(0f, 1f);
            ItemUI.Panel("Linha", m_NameTab.transform, new Vector2(0f, 0f), Vector2.zero, new Vector2(380f, 2f), accent)
                .rectTransform.pivot = Vector2.zero;
            m_NameLabel = MenuKit.Label("Nome", m_NameTab.transform, new Vector2(0f, 0.5f), new Vector2(18f, 0f),
                new Vector2(350f, 40f), 26f, TextAlignmentOptions.Left, accent, ItemFonts.VisorWide);
            m_NameLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Name = m_NameLabel.gameObject.AddComponent<DialogueText>();

            // Fala
            m_LineLabel = MenuKit.Label("Fala", m_Box, new Vector2(0f, 1f), new Vector2(36f, -30f),
                new Vector2(k_TextWidth, k_BoxHeight - 70f), 30f, TextAlignmentOptions.TopLeft, MenuKit.Text, ItemFonts.Visor);
            m_LineLabel.rectTransform.pivot = new Vector2(0f, 1f);
            m_LineLabel.textWrappingMode = TextWrappingModes.Normal;
            m_LineLabel.lineSpacing = 12f;
            m_Line = m_LineLabel.gameObject.AddComponent<DialogueText>();

            // Dica embaixo, à direita
            m_Hint = MenuKit.Label("Dica", m_Box, new Vector2(1f, 0f), new Vector2(-28f, 16f), new Vector2(600f, 28f),
                20f, TextAlignmentOptions.BottomRight, MenuKit.Muted, ItemFonts.Visor);
            m_Hint.rectTransform.pivot = new Vector2(1f, 0f);

            // Respostas à direita, de cima para baixo
            for (int i = 0; i < 4; i++)
            {
                var choice = new Choice();
                choice.Back = ItemUI.Panel("Resposta" + (i + 1), m_Box, new Vector2(1f, 1f),
                    new Vector2(-28f, -26f - i * (k_ChoiceHeight + 8f)), new Vector2(k_ChoiceWidth, k_ChoiceHeight), k_ChoiceBack);
                choice.Root = choice.Back.rectTransform;
                choice.Root.pivot = new Vector2(1f, 1f);
                choice.Bar = ItemUI.Panel("Barra", choice.Root, new Vector2(0f, 0.5f), Vector2.zero,
                    new Vector2(4f, k_ChoiceHeight), accent);
                choice.Bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                choice.Label = MenuKit.Label("Texto", choice.Root, new Vector2(0f, 0.5f), new Vector2(18f, 0f),
                    new Vector2(k_ChoiceWidth - 28f, k_ChoiceHeight), 22f, TextAlignmentOptions.Left, MenuKit.Text,
                    ItemFonts.Visor);
                choice.Label.rectTransform.pivot = new Vector2(0f, 0.5f);
                choice.Root.gameObject.SetActive(false);
                m_Choices.Add(choice);
            }

            SetOpen(0f);
        }

        void ShowChoices(List<DialogueChoice> choices)
        {
            int count = choices?.Count ?? 0;
            for (int i = 0; i < m_Choices.Count; i++)
            {
                bool on = i < count;
                m_Choices[i].Root.gameObject.SetActive(on);
                if (on)
                    m_Choices[i].Label.text = $"<color=#{ItemUI.Hex(MenuKit.Muted)}>{i + 1}</color>  {choices[i].Label}";
            }

            m_Focus = 0;
            if (Mouse.current != null)
                m_LastMouse = Mouse.current.position.ReadValue();
            Cursor.visible = count > 0;
            UpdateChoices();
        }

        void UpdateChoices()
        {
            for (int i = 0; i < m_Choices.Count; i++)
            {
                Choice choice = m_Choices[i];
                if (!choice.Root.gameObject.activeSelf)
                    continue;
                bool focus = i == m_Focus;
                choice.Back.color = focus ? k_ChoiceLit : k_ChoiceBack;
                choice.Bar.rectTransform.sizeDelta = new Vector2(focus ? 8f : 3f, k_ChoiceHeight);
                choice.Label.color = focus ? Color.white : MenuKit.Text;
                choice.Root.anchoredPosition = new Vector2(focus ? -20f : -28f, choice.Root.anchoredPosition.y);
            }
        }

        void SetOpen(float t)
        {
            if (!m_Group)
                return;
            t = Mathf.Clamp01(t);
            m_Group.alpha = t;
            m_Box.anchoredPosition = new Vector2(0f, k_BoxBottom - 30f * (1f - t) * (1f - t));
        }

        static string Hint(string key1, string label1, string key2, string label2) =>
            $"<color=#{ItemUI.Hex(MenuKit.Accent)}>[{key1}]</color> {label1}    <color=#{ItemUI.Hex(MenuKit.Accent)}>[{key2}]</color> {label2}";

        void Clear()
        {
            if (m_Canvas)
                Destroy(m_Canvas.gameObject);
            m_Canvas = null;
            m_Choices.Clear();
            m_LastBlip = 0f;
        }
    }
}
