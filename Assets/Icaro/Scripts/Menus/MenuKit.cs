using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Icaro
{
    // Peças dos menus (título, pausa, morte, configurações): botões (comuns e em faixa), linhas de configuração
    // (liga/desliga, barra, lista de opções, tecla), com clique e arrasto do mouse testados à mão (sem EventSystem,
    // como as cartas de itens e a loja; o projeto usa só o Input System novo). Cada tela tem um MenuKit com os controles dela; o GameMenus chama Tick a cada frame.
    public class MenuKit
    {
        public static readonly Color Accent = new Color(0.45f, 0.9f, 1f);
        public static readonly Color Text = new Color(0.9f, 0.94f, 0.97f);
        public static readonly Color Muted = new Color(0.6f, 0.66f, 0.72f);
        public static readonly Color ButtonColor = new Color(0.12f, 0.15f, 0.19f, 0.95f);
        public static readonly Color ButtonHover = new Color(0.2f, 0.32f, 0.4f, 0.98f);
        public static readonly Color RowColor = new Color(0.08f, 0.1f, 0.13f, 0.85f);

        public const float RowWidth = 1040f;
        public const float RowHeight = 50f;
        const float k_ControlWidth = 420f;

        readonly List<Control> m_Controls = new List<Control>();
        readonly List<Control> m_Snapshot = new List<Control>();

        // Linha de tecla esperando uma tecla nova (as outras ficam paradas enquanto isso)
        public KeyRow Waiting { get; set; }

        public void Add(Control control) => m_Controls.Add(control);

        public void Clear()
        {
            m_Controls.Clear();
            m_Version++;
        }

        // Muda quando a lista é limpa (um botão que troca de tela limpa a lista no meio do Tick)
        int m_Version;

        public void Tick()
        {
            Mouse mouse = Mouse.current;
            Vector2 position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool held = mouse != null && mouse.leftButton.isPressed;

            if (Waiting != null)
            {
                Waiting.Listen();
                return;
            }

            // Percorre uma cópia e para se um controle trocar a tela (a lista foi limpa e os controles destruídos)
            int version = m_Version;
            m_Snapshot.Clear();
            m_Snapshot.AddRange(m_Controls);
            foreach (Control control in m_Snapshot)
            {
                if (!control.Rect || !control.Rect.gameObject.activeInHierarchy)
                    continue;
                bool hover = RectTransformUtility.RectangleContainsScreenPoint(control.HitRect, position, null);
                control.Tick(this, hover, click && hover, held, position);
                if (version != m_Version || Waiting != null)
                    return;
            }
        }

        public void RefreshAll()
        {
            foreach (Control control in m_Controls)
                control.Refresh();
        }

        // ---------- controles ----------

        public abstract class Control
        {
            public RectTransform Rect;

            // Área clicável (por padrão, o próprio controle)
            public virtual RectTransform HitRect => Rect;

            public virtual void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse) { }
            public virtual void Refresh() { }
        }

        public class ButtonControl : Control
        {
            public Image Background;
            public TextMeshProUGUI Label;
            public Action OnClick;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                Background.color = hover ? ButtonHover : ButtonColor;
                Label.color = hover ? Color.white : Text;
                if (click)
                    OnClick?.Invoke();
            }
        }

        // Botão em faixa dos menus principais (título, pausa, morte, créditos): encostado na borda esquerda da tela, com
        // o texto recuado; ao passar o mouse, a faixa alarga para a direita, o fundo fica mais opaco e o texto clareia,
        // com transição suave em tempo sem escala (os menus pausam o jogo)
        public class StripButton : Control
        {
            public static readonly Color Normal = new Color(0.08f, 0.09f, 0.12f, 0.72f);
            public static readonly Color Hovered = new Color(0.09f, 0.12f, 0.15f, 0.96f);
            public static readonly Color TextNormal = new Color(0.82f, 0.85f, 0.88f, 1f);
            public const float Width = 460f;
            public const float HoverWidth = 640f;
            public const float Height = 58f;

            public Image Background;
            public TextMeshProUGUI Label;
            public Action OnClick;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                float t = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
                Vector2 size = Rect.sizeDelta;
                size.x = Mathf.Lerp(size.x, hover ? HoverWidth : Width, t);
                Rect.sizeDelta = size;
                Background.color = Color.Lerp(Background.color, hover ? Hovered : Normal, t);
                Label.color = Color.Lerp(Label.color, hover ? Color.white : TextNormal, t);
                if (click)
                    OnClick?.Invoke();
            }
        }

        // Linha: rótulo à esquerda e o controle à direita
        public abstract class RowControl : Control
        {
            public RectTransform Area;
            public TextMeshProUGUI Value;
            public override RectTransform HitRect => Area;
        }

        public class ToggleRow : RowControl
        {
            public Func<bool> Get;
            public Action<bool> Set;
            public Image Box;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                if (click)
                {
                    Set(!Get());
                    GameSettings.Commit();
                }
                Box.color = hover ? ButtonHover : ButtonColor;
                Refresh();
            }

            public override void Refresh()
            {
                bool on = Get();
                Value.text = on ? "Ligado" : "Desligado";
                Value.color = on ? Accent : Muted;
            }
        }

        public class SliderRow : RowControl
        {
            public Func<float> Get;
            public Action<float> Set;
            public float Min;
            public float Max;
            public float Step;
            public Func<float, string> Format;
            public RectTransform Bar;
            public Image Fill;
            public RectTransform Knob;
            bool m_Dragging;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                // O arrasto começa num clique sobre a barra (com folga), não sobre o número
                if (click)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Bar, mouse, null, out Vector2 start);
                    m_Dragging = start.x >= Bar.rect.xMin - 16f;
                }
                if (m_Dragging && held)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Bar, mouse, null, out Vector2 local);
                    // Posição do mouse na barra, da borda esquerda (0) à direita (1), qualquer que seja o pivô
                    Rect bar = Bar.rect;
                    float t = Mathf.Clamp01((local.x - bar.xMin) / bar.width);
                    float value = Mathf.Lerp(Min, Max, t);
                    if (Step > 0f)
                        value = Mathf.Round(value / Step) * Step;
                    if (!Mathf.Approximately(value, Get()))
                    {
                        Set(value);
                        GameSettings.Apply();
                    }
                }
                else if (m_Dragging)
                {
                    m_Dragging = false;
                    GameSettings.Commit();
                }

                Refresh();
            }

            public override void Refresh()
            {
                float value = Get();
                float t = Mathf.InverseLerp(Min, Max, value);
                Fill.rectTransform.anchorMax = new Vector2(t, 1f);
                Knob.anchorMin = Knob.anchorMax = new Vector2(t, 0.5f);
                Knob.anchoredPosition = Vector2.zero;
                Value.text = Format != null ? Format(value) : value.ToString("0.0");
            }
        }

        public class ChoiceRow : RowControl
        {
            public Func<int> Get;
            public Action<int> Set;
            public Func<string[]> Options;
            public RectTransform Left;
            public RectTransform Right;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                if (click)
                {
                    string[] options = Options();
                    if (options.Length > 0)
                    {
                        // Metade esquerda volta uma opção; a direita avança
                        float centerX = Area.TransformPoint(Area.rect.center).x;
                        bool left = mouse.x < centerX;
                        int step = left ? -1 : 1;
                        Set((Get() + step + options.Length) % options.Length);
                        GameSettings.Commit();
                    }
                }

                Refresh();
            }

            public override void Refresh()
            {
                string[] options = Options();
                int index = Get();
                Value.text = index >= 0 && index < options.Length ? options[index] : "—";
            }
        }

        // Tecla: clicar e apertar a tecla nova (Esc cancela). Ação do Microgame (troca a ligação do Input System) ou
        // tecla do Ícaro (Key salva no GameSettings).
        public class KeyRow : RowControl
        {
            public string ActionPath;
            public Func<Key> GetKey;
            public Action<Key> SetKey;

            public override void Tick(MenuKit kit, bool hover, bool click, bool held, Vector2 mouse)
            {
                if (click)
                {
                    kit.Waiting = this;
                    Value.text = "Aperte uma tecla...";
                    Value.color = Accent;
                    m_Kit = kit;
                    m_Armed = false;
                    return;
                }

                Refresh();
            }

            MenuKit m_Kit;
            bool m_Armed;

            // Espera soltar o clique e então captura a próxima tecla ou botão
            public void Listen()
            {
                Keyboard keyboard = Keyboard.current;
                Mouse mouse = Mouse.current;
                if (!m_Armed)
                {
                    m_Armed = mouse == null || !mouse.leftButton.isPressed;
                    return;
                }

                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    Done();
                    return;
                }

                string path = null;
                Key pressed = Key.None;
                if (keyboard != null)
                {
                    foreach (KeyControl key in keyboard.allKeys)
                    {
                        if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape)
                        {
                            pressed = key.keyCode;
                            path = key.path;
                            break;
                        }
                    }
                }

                if (path == null && mouse != null)
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                        path = "<Mouse>/leftButton";
                    else if (mouse.rightButton.wasPressedThisFrame)
                        path = "<Mouse>/rightButton";
                    else if (mouse.middleButton.wasPressedThisFrame)
                        path = "<Mouse>/middleButton";
                }

                if (path == null)
                    return;

                if (!string.IsNullOrEmpty(ActionPath))
                    Rebind(path);
                else if (pressed != Key.None)
                    SetKey(pressed);
                GameSettings.Commit();
                Done();
            }

            void Rebind(string path)
            {
                InputAction action = InputSystem.actions ? InputSystem.actions.FindAction(ActionPath) : null;
                int index = BindingIndex(action);
                if (index < 0)
                    return;
                // Caminho do controle (ex.: /Keyboard/space) para o formato de ligação (<Keyboard>/space)
                if (path.StartsWith("/"))
                {
                    int slash = path.IndexOf('/', 1);
                    path = "<" + path.Substring(1, slash - 1) + ">" + path.Substring(slash);
                }
                action.ApplyBindingOverride(index, path);
            }

            // A primeira ligação de teclado ou mouse (não composta) da ação
            static int BindingIndex(InputAction action)
            {
                if (action == null)
                    return -1;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite)
                        continue;
                    string path = binding.path ?? "";
                    if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))
                        return i;
                }

                return -1;
            }

            void Done()
            {
                if (m_Kit != null)
                    m_Kit.Waiting = null;
                Refresh();
            }

            public override void Refresh()
            {
                Value.color = Text;
                if (!string.IsNullOrEmpty(ActionPath))
                {
                    InputAction action = InputSystem.actions ? InputSystem.actions.FindAction(ActionPath) : null;
                    int index = BindingIndex(action);
                    Value.text = index >= 0
                        ? action.GetBindingDisplayString(index).ToUpperInvariant()
                        : "—";
                }
                else
                {
                    Value.text = GameSettings.KeyName(GetKey());
                }
            }
        }

        // ---------- construção ----------

        public ButtonControl Button(Transform parent, string label, Vector2 position, Vector2 size, Action onClick,
            Vector2? anchor = null)
        {
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            Image background = ItemUI.Panel(label, parent, a, position, size, ButtonColor);
            TextMeshProUGUI text = ItemUI.Text("Texto", background.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                size, size.y * 0.48f, TextAlignmentOptions.Center, Text);
            text.fontStyle = FontStyles.Bold;
            text.text = label;
            var button = new ButtonControl
            {
                Rect = background.rectTransform, Background = background, Label = text, OnClick = onClick,
            };
            Add(button);
            return button;
        }

        // Botão em faixa com o canto de cima à esquerda em (0, y) do 'parent' (que tem a borda esquerda na da tela)
        public StripButton Strip(Transform parent, string label, float y, Action onClick)
        {
            Image background = ItemUI.Panel("Botao_" + label, parent, new Vector2(0f, 1f), new Vector2(0f, y),
                new Vector2(StripButton.Width, StripButton.Height), StripButton.Normal);
            background.rectTransform.pivot = new Vector2(0f, 1f);
            TextMeshProUGUI text = ItemUI.Text("Texto", background.transform, new Vector2(0f, 0.5f), new Vector2(96f, 0f),
                new Vector2(StripButton.HoverWidth - 110f, StripButton.Height), 32f, TextAlignmentOptions.MidlineLeft,
                StripButton.TextNormal);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.fontStyle = FontStyles.Bold;
            text.text = label;
            var button = new StripButton
            {
                Rect = background.rectTransform, Background = background, Label = text, OnClick = onClick,
            };
            Add(button);
            return button;
        }

        // Fundo da linha, rótulo e área do controle; devolve a área (à direita)
        static RectTransform Row(Transform parent, string label, float y, out Image area)
        {
            Image row = ItemUI.Panel("Linha_" + label, parent, new Vector2(0.5f, 1f), new Vector2(0f, y),
                new Vector2(RowWidth, RowHeight), RowColor);
            TextMeshProUGUI text = ItemUI.Text("Rotulo", row.transform, new Vector2(0f, 0.5f), new Vector2(24f, 0f),
                new Vector2(RowWidth - k_ControlWidth - 40f, RowHeight), 24f, TextAlignmentOptions.Left, Text);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.text = label;
            area = ItemUI.Panel("Controle", row.transform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f),
                new Vector2(k_ControlWidth, RowHeight - 10f), new Color(0f, 0f, 0f, 0f));
            area.rectTransform.pivot = new Vector2(1f, 0.5f);
            return row.rectTransform;
        }

        static TextMeshProUGUI ValueText(Transform area, float width, TextAlignmentOptions alignment, Vector2 anchor,
            Vector2 offset)
        {
            TextMeshProUGUI value = ItemUI.Text("Valor", area, anchor, offset, new Vector2(width, RowHeight), 22f,
                alignment, Text);
            return value;
        }

        public ToggleRow Toggle(Transform parent, string label, float y, Func<bool> get, Action<bool> set)
        {
            RectTransform row = Row(parent, label, y, out Image area);
            Image box = ItemUI.Fill("Caixa", area.transform, 0f, ButtonColor);
            var control = new ToggleRow
            {
                Rect = row, Area = area.rectTransform, Box = box, Get = get, Set = set,
                Value = ValueText(area.transform, k_ControlWidth, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 0.5f), Vector2.zero),
            };
            control.Refresh();
            Add(control);
            return control;
        }

        public SliderRow Slider(Transform parent, string label, float y, float min, float max, float step,
            Func<float> get, Action<float> set, Func<float, string> format)
        {
            RectTransform row = Row(parent, label, y, out Image area);
            // Valor à esquerda e barra à direita, sem se sobrepor
            Image bar = ItemUI.Panel("Barra", area.transform, new Vector2(0f, 0.5f), new Vector2(130f, 0f),
                new Vector2(270f, 10f), new Color(0.02f, 0.03f, 0.04f, 0.9f));
            Image fill = ItemUI.Fill("Nivel", bar.transform, 0f, Accent);
            Image knob = ItemUI.Panel("Marcador", bar.transform, new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(14f, 26f), Color.white);
            knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var control = new SliderRow
            {
                Rect = row, Area = area.rectTransform, Bar = bar.rectTransform, Fill = fill, Knob = knob.rectTransform,
                Min = min, Max = max,
                Step = step, Get = get, Set = set, Format = format,
                Value = ValueText(area.transform, 110f, TextAlignmentOptions.Center, new Vector2(0f, 0.5f),
                    Vector2.zero),
            };
            control.Refresh();
            Add(control);
            return control;
        }

        public ChoiceRow Choice(Transform parent, string label, float y, Func<string[]> options, Func<int> get,
            Action<int> set)
        {
            RectTransform row = Row(parent, label, y, out Image area);
            ItemUI.Fill("Caixa", area.transform, 0f, ButtonColor);
            TextMeshProUGUI left = ItemUI.Text("Esquerda", area.transform, new Vector2(0f, 0.5f), new Vector2(24f, 0f),
                new Vector2(40f, RowHeight), 26f, TextAlignmentOptions.Center, Accent);
            left.text = "<";
            TextMeshProUGUI right = ItemUI.Text("Direita", area.transform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f),
                new Vector2(40f, RowHeight), 26f, TextAlignmentOptions.Center, Accent);
            right.text = ">";
            var control = new ChoiceRow
            {
                Rect = row, Area = area.rectTransform, Left = left.rectTransform, Right = right.rectTransform,
                Options = options, Get = get, Set = set,
                Value = ValueText(area.transform, k_ControlWidth - 100f, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 0.5f), Vector2.zero),
            };
            control.Refresh();
            Add(control);
            return control;
        }

        public KeyRow KeyAction(Transform parent, string label, float y, string actionPath)
        {
            KeyRow control = KeyBase(parent, label, y);
            control.ActionPath = actionPath;
            control.Refresh();
            return control;
        }

        public KeyRow KeyCustom(Transform parent, string label, float y, Func<Key> get, Action<Key> set)
        {
            KeyRow control = KeyBase(parent, label, y);
            control.GetKey = get;
            control.SetKey = set;
            control.Refresh();
            return control;
        }

        KeyRow KeyBase(Transform parent, string label, float y)
        {
            RectTransform row = Row(parent, label, y, out Image area);
            ItemUI.Fill("Caixa", area.transform, 0f, ButtonColor);
            var control = new KeyRow
            {
                Rect = row, Area = area.rectTransform,
                Value = ValueText(area.transform, k_ControlWidth, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 0.5f), Vector2.zero),
            };
            Add(control);
            return control;
        }
    }
}
