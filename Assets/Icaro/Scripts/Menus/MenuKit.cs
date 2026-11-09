using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Icaro
{
    // Peças dos menus (título, pausa, morte, créditos, configurações), no estilo do Luciano polido: faixas encostadas na
    // borda esquerda com a ponta em degraus de pixel, linha ciano embaixo e seta (Strip); linhas de configuração com
    // chave em pixel, barra de 20 blocos, lista com setas e tecla em caixa; botão com contorno; painel de vidro com
    // cantos chanfrados; rodapé com as teclas. Sem EventSystem (o projeto usa só o Input System novo): o mouse é testado
    // à mão e o teclado navega pelo foco (W/S ou setas movem, Enter ou Espaço ativam, A/D ou setas ajustam as linhas,
    // com repetição ao segurar). O mouse que se mexe leva o foco para o que está embaixo dele. Cada tela tem um MenuKit
    // com os controles dela; quem é dono chama Tick a cada frame. Tudo em tempo sem escala (os menus pausam o jogo).
    // Sons: UiSounds (passar, clicar, ajustar).
    public class MenuKit
    {
        public static readonly Color Accent = new Color(0.45f, 0.9f, 1f);
        public static readonly Color Text = new Color(0.82f, 0.85f, 0.88f);
        public static readonly Color Muted = new Color(0.55f, 0.6f, 0.65f);
        public static readonly Color Dark = new Color(0.024f, 0.035f, 0.059f);
        public static readonly Color Slot = new Color(0.15f, 0.18f, 0.22f, 1f);

        public const float RowHeight = 50f;
        const float k_ControlWidth = 430f;
        const float k_RepeatDelay = 0.35f;
        const float k_RepeatRate = 0.055f;

        readonly List<Control> m_Controls = new List<Control>();
        readonly List<Control> m_Snapshot = new List<Control>();
        int m_Version;
        int m_Focus = -1;
        Vector2 m_LastMouse;
        bool m_MouseSeen;
        Action m_Deferred;
        float m_DeferUntil;
        float m_VerticalHeld, m_HorizontalHeld;
        float m_VerticalNext, m_HorizontalNext;

        // Linha de tecla esperando uma tecla nova (as outras ficam paradas enquanto isso)
        public KeyRow Waiting { get; set; }

        // Teclado navega por este kit (desligado nos kits que só respondem ao mouse, como as abas das configurações)
        public bool Keyboard { get; set; } = true;

        // Ações adiadas (o clique pisca antes da tela trocar): enquanto isso, nada responde
        public bool Busy => m_Deferred != null;

        public void Add(Control control) => m_Controls.Add(control);

        public void Clear()
        {
            m_Controls.Clear();
            m_Focus = -1;
            m_Deferred = null;
            Waiting = null;
            m_Version++;
        }

        // Faixas deste kit (para animar a saída de uma tela)
        public IEnumerable<StripButton> Strips
        {
            get
            {
                foreach (Control control in m_Controls)
                {
                    if (control is StripButton strip)
                        yield return strip;
                }
            }
        }

        public Control Focused => m_Focus >= 0 && m_Focus < m_Controls.Count ? m_Controls[m_Focus] : null;

        // Foco inicial (no primeiro controle que aceita foco) ou num controle dado
        public void FocusFirst()
        {
            m_Focus = -1;
            for (int i = 0; i < m_Controls.Count; i++)
            {
                if (m_Controls[i].Focusable)
                {
                    m_Focus = i;
                    return;
                }
            }
        }

        public void FocusOn(Control control) => m_Focus = m_Controls.IndexOf(control);

        public void Defer(Action action, float delay)
        {
            m_Deferred = action;
            m_DeferUntil = Time.unscaledTime + delay;
        }

        public void Tick()
        {
            if (m_Deferred != null)
            {
                foreach (Control control in m_Controls)
                    control.Tick(this, m_Controls.IndexOf(control) == m_Focus, false, false, Vector2.zero);
                if (Time.unscaledTime < m_DeferUntil)
                    return;
                Action action = m_Deferred;
                m_Deferred = null;
                action?.Invoke();
                return;
            }

            if (Waiting != null)
            {
                Waiting.Listen();
                return;
            }

            Mouse mouse = Mouse.current;
            Vector2 position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool held = mouse != null && mouse.leftButton.isPressed;
            bool moved = m_MouseSeen && (position - m_LastMouse).sqrMagnitude > 4f;
            m_LastMouse = position;
            m_MouseSeen = true;

            int hovered = -1;
            for (int i = 0; i < m_Controls.Count; i++)
            {
                Control control = m_Controls[i];
                if (!control.Focusable || !control.Rect || !control.Rect.gameObject.activeInHierarchy)
                    continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(control.HitRect, position, null))
                {
                    hovered = i;
                    break;
                }
            }

            int focus = m_Focus;
            if (moved || click)
            {
                if (hovered >= 0)
                    focus = hovered;
                else if (moved && !(Focused is RowControl))
                    focus = -1;
            }

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            int vertical = 0, horizontal = 0;
            bool activate = false;
            if (Keyboard && keyboard != null)
            {
                vertical = Repeat(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                               ref m_VerticalHeld, ref m_VerticalNext) ? 1 :
                           Repeat(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                               ref m_VerticalHeld, ref m_VerticalNext) ? -1 : 0;
                if (!(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.wKey.isPressed ||
                      keyboard.upArrowKey.isPressed))
                    m_VerticalHeld = 0f;
                bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                horizontal = Repeat(right, ref m_HorizontalHeld, ref m_HorizontalNext) ? 1 :
                    Repeat(left, ref m_HorizontalHeld, ref m_HorizontalNext) ? -1 : 0;
                if (!right && !left)
                    m_HorizontalHeld = 0f;
                activate = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                           keyboard.spaceKey.wasPressedThisFrame;
            }

            if (vertical != 0)
                focus = Step(focus, vertical);
            if (focus != m_Focus && focus >= 0)
                UiSounds.Play(UiSounds.Hover, 0.45f);
            m_Focus = focus;

            int version = m_Version;
            if (activate && Focused != null)
            {
                Focused.Activate(this);
                if (version != m_Version || Busy || Waiting != null)
                    return;
            }

            if (horizontal != 0 && Focused != null)
                Focused.Adjust(this, horizontal);

            // Percorre uma cópia e para se um controle trocar a tela (a lista foi limpa e os controles destruídos)
            m_Snapshot.Clear();
            m_Snapshot.AddRange(m_Controls);
            for (int i = 0; i < m_Snapshot.Count; i++)
            {
                Control control = m_Snapshot[i];
                if (!control.Rect || !control.Rect.gameObject.activeInHierarchy)
                    continue;
                control.Tick(this, i == m_Focus, click && i == hovered, held, position);
                if (version != m_Version || Waiting != null || Busy)
                    return;
            }
        }

        // Próximo controle com foco na direção 'dir' (dá a volta)
        int Step(int from, int dir)
        {
            int count = m_Controls.Count;
            if (count == 0)
                return -1;
            int i = from < 0 ? (dir > 0 ? -1 : 0) : from;
            for (int n = 0; n < count; n++)
            {
                i = (i + dir + count) % count;
                if (m_Controls[i].Focusable && m_Controls[i].Rect && m_Controls[i].Rect.gameObject.activeInHierarchy)
                    return i;
            }

            return from;
        }

        // Tecla segurada: dispara ao apertar e, depois de k_RepeatDelay, a cada k_RepeatRate
        static bool Repeat(bool pressed, ref float heldSince, ref float next)
        {
            if (!pressed)
                return false;
            float now = Time.unscaledTime;
            if (heldSince <= 0f)
            {
                heldSince = now;
                next = now + k_RepeatDelay;
                return true;
            }

            if (now < next)
                return false;
            next = now + k_RepeatRate;
            return true;
        }

        public void RefreshAll()
        {
            foreach (Control control in m_Controls)
                control.Refresh();
        }

        static float Ease(float speed) => 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

        // ---------- controles ----------

        public abstract class Control
        {
            public RectTransform Rect;

            // Área clicável (por padrão, o próprio controle)
            public virtual RectTransform HitRect => Rect;
            public virtual bool Focusable => true;

            public virtual void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse) { }
            public virtual void Activate(MenuKit kit) { }
            public virtual void Adjust(MenuKit kit, int direction) { }
            public virtual void Refresh() { }
        }

        // Botão com contorno (Restaurar padrão)
        public class ButtonControl : Control
        {
            public Image Fill;
            public Image Border;
            public TextMeshProUGUI Label;
            public Action OnClick;
            float m_Lit, m_Flash;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                m_Lit = Mathf.Lerp(m_Lit, focused ? 1f : 0f, Ease(18f));
                m_Flash = Mathf.MoveTowards(m_Flash, 0f, Time.unscaledDeltaTime * 5f);
                Fill.color = Color.Lerp(new Color(Accent.r, Accent.g, Accent.b, 0.06f + 0.14f * m_Lit), Color.white,
                    m_Flash * 0.6f);
                Border.color = Color.Lerp(Muted, Accent, m_Lit);
                Label.color = Color.Lerp(Text, Color.white, m_Lit);
                if (click)
                    Activate(kit);
            }

            public override void Activate(MenuKit kit)
            {
                m_Flash = 1f;
                UiSounds.Play(UiSounds.Click, 0.7f);
                OnClick?.Invoke();
            }
        }

        // Faixa dos menus principais: encostada na borda esquerda, com a ponta direita em degraus de pixel e uma linha
        // ciano embaixo (um tique curto, ou inteira com o foco). Com o foco, alarga, escurece menos, mostra a seta e
        // empurra o texto; marcada (aba atual), fica larga e com o texto ciano. Entra deslizando da esquerda
        // (Delay) e, ao ativar, pisca e só então chama a ação.
        public class StripButton : Control
        {
            public static readonly Color Normal = new Color(0.07f, 0.086f, 0.118f, 0.75f);
            public static readonly Color Lit = new Color(0.09f, 0.122f, 0.157f, 0.96f);
            public const float Height = 57f;
            const float k_LineTick = 36f;
            const float k_Enter = 0.34f;

            public RectTransform Body;
            public Image Fill;
            public Image Cap;
            public Image Line;
            public Image Arrow;
            public TextMeshProUGUI Label;
            public CanvasGroup Group;
            public Action OnClick;
            public float Width = 460f;
            public float WideWidth = 640f;
            public float Delay;
            public bool Selected;
            public bool Leaving;

            float m_Age;
            float m_Lit;
            float m_Flash;
            float m_Width;
            float m_LeaveAge;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                float dt = Time.unscaledDeltaTime;
                m_Age += dt;
                bool wide = focused || Selected;
                if (m_Width <= 0f)
                    m_Width = Width;
                m_Width = Mathf.Lerp(m_Width, wide ? WideWidth : Width, Ease(16f));
                m_Lit = Mathf.Lerp(m_Lit, focused ? 1f : 0f, Ease(18f));
                m_Flash = Mathf.MoveTowards(m_Flash, 0f, dt * 6f);

                // Entrada (da esquerda, em degraus de opacidade) e saída (de volta para a esquerda)
                float enter = Mathf.Clamp01((m_Age - Delay) / k_Enter);
                float slide = 1f - Mathf.Pow(1f - enter, 3f);
                float leave = 0f;
                if (Leaving)
                {
                    m_LeaveAge += dt;
                    leave = Mathf.Clamp01((m_LeaveAge - Delay * 0.5f) / 0.25f);
                    leave *= leave;
                }

                float width = Mathf.Round(m_Width / 3f) * 3f;
                Rect.sizeDelta = new Vector2(width, Height);
                Body.anchoredPosition = new Vector2(Mathf.Round(((1f - slide) + leave) * -(WideWidth + 40f)), 0f);
                Group.alpha = Mathf.Ceil(Mathf.Clamp01(enter * 1.6f) * (1f - leave) * 4f) / 4f;

                float bodyWidth = width - MenuArt.StripCapWidth;
                Fill.rectTransform.sizeDelta = new Vector2(bodyWidth, Height);
                Cap.rectTransform.anchoredPosition = new Vector2(bodyWidth, 0f);
                Color fill = Color.Lerp(Selected ? Lit : Normal, Lit, m_Lit);
                fill = Color.Lerp(fill, new Color(0.75f, 0.95f, 1f, 1f), m_Flash * 0.75f);
                Fill.color = Cap.color = fill;

                float lineFull = bodyWidth + (MenuArt.StripSteps - 1) * MenuArt.StripStep;
                float lineAmount = Mathf.Max(m_Lit, Selected ? 1f : 0f);
                Line.rectTransform.sizeDelta = new Vector2(Mathf.Round(Mathf.Lerp(k_LineTick, lineFull, lineAmount)), 3f);
                Line.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Lerp(0.6f, 1f, lineAmount));

                Arrow.color = new Color(1f, 1f, 1f, Mathf.Round(m_Lit * 4f) / 4f);
                Arrow.rectTransform.anchoredPosition = new Vector2(40f + Mathf.Round((1f - m_Lit) * -8f), 0f);
                Label.rectTransform.anchoredPosition = new Vector2(64f + Mathf.Round(m_Lit * 18f), 1f);
                Color text = Selected ? Accent : Text;
                Label.color = Color.Lerp(text, Color.white, m_Lit);

                if (click && enter >= 1f && !Leaving)
                    Activate(kit);
            }

            public override void Activate(MenuKit kit)
            {
                if (Leaving || m_Age < Delay)
                    return;
                m_Flash = 1f;
                UiSounds.Play(UiSounds.Click, 0.75f);
                kit.Defer(OnClick, 0.09f);
            }
        }

        // Linha das configurações: realce ciano e barra à esquerda com o foco, rótulo e o controle à direita
        public abstract class RowControl : Control
        {
            public RectTransform Area;
            public Image Tint;
            public Image Bar;
            public TextMeshProUGUI Caption;
            public TextMeshProUGUI Value;
            protected float Lit;

            public override RectTransform HitRect => Rect;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                Lit = Mathf.Lerp(Lit, focused ? 1f : 0f, Ease(20f));
                Tint.color = new Color(Accent.r, Accent.g, Accent.b, 0.1f * Lit);
                Bar.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Round(Lit * 4f) / 4f);
                Caption.color = Color.Lerp(Text, Color.white, Lit);
            }
        }

        public class ToggleRow : RowControl
        {
            public Func<bool> Get;
            public Action<bool> Set;
            public Image Switch;
            public RectTransform Knob;
            public Image KnobImage;
            float m_Knob = -1f;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                base.Tick(kit, focused, click, held, mouse);
                if (click)
                    Activate(kit);
                bool on = Get();
                if (m_Knob < 0f)
                    m_Knob = on ? 1f : 0f;
                m_Knob = Mathf.MoveTowards(m_Knob, on ? 1f : 0f, Time.unscaledDeltaTime * 9f);
                Knob.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(3f, 48f, m_Knob) / 3f) * 3f, 0f);
                Switch.color = Color.Lerp(Slot, Accent, m_Knob);
                KnobImage.color = Color.Lerp(new Color(0.47f, 0.51f, 0.55f), new Color(0.94f, 0.98f, 1f), m_Knob);
                Refresh();
            }

            public override void Activate(MenuKit kit) => Flip();
            public override void Adjust(MenuKit kit, int direction) => Flip();

            void Flip()
            {
                Set(!Get());
                GameSettings.Commit();
                UiSounds.Play(Get() ? UiSounds.Click : UiSounds.Unclick, 0.7f);
            }

            public override void Refresh()
            {
                bool on = Get();
                Value.text = on ? "Ligado" : "Desligado";
                Value.color = on ? Text : Muted;
            }
        }

        // Barra de 20 blocos: arrastar com o mouse, A/D em passos (o último bloco aceso fica branco com o foco)
        public class SliderRow : RowControl
        {
            public const int Blocks = 20;
            public Func<float> Get;
            public Action<float> Set;
            public float Min;
            public float Max;
            public float Step;
            public Func<float, string> Format;
            public RectTransform Bar_;
            public Image[] Cells;
            bool m_Dragging;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                base.Tick(kit, focused, click, held, mouse);
                // O arrasto começa num clique sobre a barra (com folga)
                if (click)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Bar_, mouse, null, out Vector2 start);
                    Rect area = Bar_.rect;
                    m_Dragging = start.x >= area.xMin - 16f && start.x <= area.xMax + 16f;
                }

                if (m_Dragging && held)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Bar_, mouse, null, out Vector2 local);
                    Rect bar = Bar_.rect;
                    float t = Mathf.Clamp01((local.x - bar.xMin) / bar.width);
                    SetValue(Mathf.Lerp(Min, Max, t), false);
                }
                else if (m_Dragging)
                {
                    m_Dragging = false;
                    GameSettings.Commit();
                }

                Refresh();
            }

            public override void Adjust(MenuKit kit, int direction)
            {
                float step = Mathf.Max(Step, (Max - Min) / 40f);
                if (Step > 0f)
                    step = Mathf.Round(step / Step) * Step;
                SetValue(Get() + direction * step, true);
            }

            void SetValue(float value, bool commit)
            {
                value = Mathf.Clamp(value, Min, Max);
                if (Step > 0f)
                    value = Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);
                if (Mathf.Approximately(value, Get()))
                    return;
                Set(value);
                if (commit)
                    GameSettings.Commit();
                else
                    GameSettings.Apply();
                UiSounds.Play(UiSounds.Hover, 0.4f, Mathf.Lerp(0.8f, 1.4f, Mathf.InverseLerp(Min, Max, value)));
            }

            public override void Refresh()
            {
                float value = Get();
                int lit = Mathf.RoundToInt(Mathf.InverseLerp(Min, Max, value) * Blocks);
                for (int i = 0; i < Cells.Length; i++)
                {
                    Color color = i < lit ? Accent : Slot;
                    if (i == lit - 1 && Lit > 0.5f)
                        color = new Color(0.92f, 0.99f, 1f);
                    Cells[i].color = color;
                }

                Value.text = Format != null ? Format(value) : value.ToString("0.0");
            }
        }

        // Lista: setas em pixel dos dois lados; clique na metade esquerda volta, na direita avança
        public class ChoiceRow : RowControl
        {
            public Func<int> Get;
            public Action<int> Set;
            public Func<string[]> Options;
            public Image Left;
            public Image Right;
            float m_Nudge;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                base.Tick(kit, focused, click, held, mouse);
                if (click)
                {
                    float centerX = Area.TransformPoint(Area.rect.center).x;
                    Change(mouse.x < centerX ? -1 : 1);
                }

                m_Nudge = Mathf.MoveTowards(m_Nudge, 0f, Time.unscaledDeltaTime * 40f);
                float alpha = Mathf.Lerp(0.65f, 1f, Lit);
                var color = new Color(Accent.r, Accent.g, Accent.b, alpha);
                Left.color = Right.color = color;
                Left.rectTransform.anchoredPosition = new Vector2(12f + Mathf.Min(0f, m_Nudge) * 1f, 0f);
                Right.rectTransform.anchoredPosition = new Vector2(-12f + Mathf.Max(0f, m_Nudge), 0f);
                Refresh();
            }

            public override void Activate(MenuKit kit) => Change(1);
            public override void Adjust(MenuKit kit, int direction) => Change(direction);

            void Change(int step)
            {
                string[] options = Options();
                if (options.Length == 0)
                    return;
                Set((Get() + step + options.Length) % options.Length);
                GameSettings.Commit();
                m_Nudge = step * 6f;
                UiSounds.Play(UiSounds.Click, 0.55f, step > 0 ? 1.08f : 0.94f);
            }

            public override void Refresh()
            {
                string[] options = Options();
                int index = Get();
                Value.text = index >= 0 && index < options.Length ? options[index] : "—";
            }
        }

        // Tecla: ativar e apertar a tecla nova (Esc cancela). Ação do Microgame (troca a ligação do Input System) ou
        // tecla do Ícaro (Key salva no GameSettings).
        public class KeyRow : RowControl
        {
            public string ActionPath;
            public Func<Key> GetKey;
            public Action<Key> SetKey;
            public Image Cap;
            public Image CapBorder;
            MenuKit m_Kit;
            bool m_Armed;
            bool m_Listening;

            public override void Tick(MenuKit kit, bool focused, bool click, bool held, Vector2 mouse)
            {
                base.Tick(kit, focused, click, held, mouse);
                if (click)
                {
                    Activate(kit);
                    return;
                }

                CapBorder.color = Color.Lerp(Muted, Accent, Lit);
                Refresh();
            }

            public override void Activate(MenuKit kit)
            {
                kit.Waiting = this;
                m_Kit = kit;
                m_Armed = false;
                m_Listening = true;
                Value.text = "Aperte uma tecla";
                UiSounds.Play(UiSounds.Click, 0.7f);
            }

            // Espera soltar o clique e o Enter e então captura a próxima tecla ou botão
            public void Listen()
            {
                UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
                Mouse mouse = Mouse.current;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
                Value.color = Color.Lerp(Accent, Color.white, pulse);
                CapBorder.color = Value.color;
                if (!m_Armed)
                {
                    m_Armed = (mouse == null || !mouse.leftButton.isPressed) &&
                              (keyboard == null || !keyboard.enterKey.isPressed && !keyboard.spaceKey.isPressed);
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
                UiSounds.Play(UiSounds.Confirm, 0.5f);
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
                m_Listening = false;
                if (m_Kit != null)
                    m_Kit.Waiting = null;
                Refresh();
            }

            public override void Refresh()
            {
                if (m_Listening)
                    return;
                Value.color = Text;
                if (!string.IsNullOrEmpty(ActionPath))
                {
                    InputAction action = InputSystem.actions ? InputSystem.actions.FindAction(ActionPath) : null;
                    int index = BindingIndex(action);
                    Value.text = index >= 0 ? action.GetBindingDisplayString(index).ToUpperInvariant() : "—";
                }
                else
                {
                    Value.text = GameSettings.KeyName(GetKey());
                }
            }
        }

        // ---------- construção ----------

        // Texto na fonte do visor (larga ou estreita) ou em pixel
        public static TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI text = ItemUI.Text(name, parent, anchor, position, size, fontSize, alignment, color);
            if (font)
                text.font = font;
            text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        public static Image Sprite(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color color, bool sliced = false)
        {
            Image image = ItemUI.Panel(name, parent, anchor, position, size, color);
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.rectTransform.pivot = pivot;
            return image;
        }

        // Faixa com o canto de cima à esquerda em (0, y) do 'parent' (que tem a borda esquerda na da tela)
        public StripButton Strip(Transform parent, string label, float y, Action onClick, float delay = 0f,
            float width = 460f, float wideWidth = 640f)
        {
            var topLeft = new Vector2(0f, 1f);
            RectTransform root = ItemUI.Rect("Faixa_" + label, parent, topLeft, new Vector2(0f, y),
                new Vector2(width, StripButton.Height));
            root.pivot = topLeft;
            RectTransform body = ItemUI.Rect("Corpo", root, topLeft, Vector2.zero, new Vector2(wideWidth, StripButton.Height));
            body.pivot = topLeft;
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image fill = ItemUI.Panel("Fundo", body, topLeft, Vector2.zero, new Vector2(width, StripButton.Height),
                StripButton.Normal);
            fill.rectTransform.pivot = topLeft;
            Image cap = Sprite("Ponta", body, MenuArt.StripCap, topLeft, topLeft, new Vector2(width - MenuArt.StripCapWidth, 0f),
                new Vector2(MenuArt.StripCapWidth, StripButton.Height), StripButton.Normal);
            Image line = ItemUI.Panel("Linha", body, new Vector2(0f, 0f), Vector2.zero, new Vector2(36f, 3f), Accent);
            line.rectTransform.pivot = Vector2.zero;
            Image arrow = Sprite("Seta", body, MenuArt.Arrow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(40f, 0f), new Vector2(12f, 24f), new Color(1f, 1f, 1f, 0f));
            TextMeshProUGUI text = Label("Texto", body, new Vector2(0f, 0.5f), new Vector2(64f, 1f),
                new Vector2(wideWidth - 80f, StripButton.Height), 28f, TextAlignmentOptions.MidlineLeft, Text,
                ItemFonts.VisorWide);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.text = label.ToUpperInvariant();

            var button = new StripButton
            {
                Rect = root, Body = body, Fill = fill, Cap = cap, Line = line, Arrow = arrow, Label = text, Group = group,
                OnClick = onClick, Width = width, WideWidth = wideWidth, Delay = delay,
            };
            Add(button);
            return button;
        }

        // Botão com contorno, com o canto de cima à direita em 'position' (âncora no alto à direita do 'parent')
        public ButtonControl OutlineButton(Transform parent, string label, Vector2 position, Vector2 size, Action onClick)
        {
            var topRight = new Vector2(1f, 1f);
            Image fill = ItemUI.Panel("Botao_" + label, parent, topRight, position, size, new Color(1f, 1f, 1f, 0.06f));
            fill.rectTransform.pivot = topRight;
            Image border = Sprite("Contorno", fill.transform, MenuArt.Outline, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size, Muted, true);
            TextMeshProUGUI text = Label("Texto", fill.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, 18f,
                TextAlignmentOptions.Center, Text, ItemFonts.VisorWide);
            text.text = label.ToUpperInvariant();
            var button = new ButtonControl
            {
                Rect = fill.rectTransform, Fill = fill, Border = border, Label = text, OnClick = onClick,
            };
            Add(button);
            return button;
        }

        // Fundo da linha, realce, rótulo e área do controle (à direita); a linha ocupa a largura do 'parent'
        static RectTransform Row(Transform parent, string label, float y, out RectTransform area, out Image tint,
            out Image bar, out TextMeshProUGUI caption)
        {
            RectTransform row = ItemUI.Rect("Linha_" + label, parent, new Vector2(0.5f, 1f), new Vector2(0f, y),
                new Vector2(0f, RowHeight));
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowHeight);
            row.anchoredPosition = new Vector2(0f, y);

            tint = ItemUI.Fill("Realce", row, 0f, new Color(0f, 0f, 0f, 0f));
            bar = ItemUI.Panel("Barra", row, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, RowHeight),
                new Color(0f, 0f, 0f, 0f));
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            Image separator = ItemUI.Panel("Separador", row, new Vector2(0.5f, 0f), new Vector2(0f, -3f),
                new Vector2(0f, 2f), new Color(1f, 1f, 1f, 0.06f));
            separator.rectTransform.anchorMin = new Vector2(0f, 0f);
            separator.rectTransform.anchorMax = new Vector2(1f, 0f);
            separator.rectTransform.sizeDelta = new Vector2(-48f, 2f);

            caption = Label("Rotulo", row, new Vector2(0f, 0.5f), new Vector2(30f, 0f),
                new Vector2(560f, RowHeight), 24f, TextAlignmentOptions.MidlineLeft, Text, ItemFonts.Visor);
            caption.rectTransform.pivot = new Vector2(0f, 0.5f);
            caption.text = label;

            area = ItemUI.Rect("Controle", row, new Vector2(1f, 0.5f), new Vector2(-30f, 0f),
                new Vector2(k_ControlWidth, RowHeight));
            area.pivot = new Vector2(1f, 0.5f);
            return row;
        }

        static TextMeshProUGUI Value(Transform area, Vector2 anchor, Vector2 pivot, Vector2 offset, float width,
            float size, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI value = Label("Valor", area, anchor, offset, new Vector2(width, RowHeight), size, alignment,
                Text, ItemFonts.Bold);
            value.rectTransform.pivot = pivot;
            return value;
        }

        T Setup<T>(T control, RectTransform row, RectTransform area, Image tint, Image bar, TextMeshProUGUI caption)
            where T : RowControl
        {
            control.Rect = row;
            control.Area = area;
            control.Tint = tint;
            control.Bar = bar;
            control.Caption = caption;
            control.Refresh();
            Add(control);
            return control;
        }

        public ToggleRow Toggle(Transform parent, string label, float y, Func<bool> get, Action<bool> set)
        {
            RectTransform row = Row(parent, label, y, out RectTransform area, out Image tint, out Image bar,
                out TextMeshProUGUI caption);
            var right = new Vector2(1f, 0.5f);
            Image track = ItemUI.Panel("Chave", area, right, new Vector2(-3f, 0f), new Vector2(75f, 26f), Slot);
            track.rectTransform.pivot = right;
            Image knob = ItemUI.Panel("Pino", track.transform, new Vector2(0f, 0.5f), new Vector2(3f, 0f),
                new Vector2(24f, 20f), Color.white);
            knob.rectTransform.pivot = new Vector2(0f, 0.5f);
            var control = new ToggleRow
            {
                Get = get, Set = set, Switch = track, Knob = knob.rectTransform, KnobImage = knob,
                Value = Value(area, right, right, new Vector2(-96f, 1f), 260f, 26f, TextAlignmentOptions.MidlineRight),
            };
            return Setup(control, row, area, tint, bar, caption);
        }

        public SliderRow Slider(Transform parent, string label, float y, float min, float max, float step,
            Func<float> get, Action<float> set, Func<float, string> format)
        {
            RectTransform row = Row(parent, label, y, out RectTransform area, out Image tint, out Image bar,
                out TextMeshProUGUI caption);
            const float cell = 15f, gap = 3f;
            float width = SliderRow.Blocks * (cell + gap) - gap;
            var right = new Vector2(1f, 0.5f);
            RectTransform cells = ItemUI.Rect("Blocos", area, right, new Vector2(-84f, 0f), new Vector2(width, 25f));
            cells.pivot = right;
            var images = new Image[SliderRow.Blocks];
            for (int i = 0; i < images.Length; i++)
            {
                images[i] = ItemUI.Panel("Bloco", cells, new Vector2(0f, 0.5f), new Vector2(i * (cell + gap), 0f),
                    new Vector2(cell, 25f), Slot);
                images[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            var control = new SliderRow
            {
                Bar_ = cells, Cells = images, Min = min, Max = max, Step = step, Get = get, Set = set, Format = format,
                Value = Value(area, right, right, new Vector2(0f, 1f), 76f, 26f, TextAlignmentOptions.MidlineRight),
            };
            return Setup(control, row, area, tint, bar, caption);
        }

        public ChoiceRow Choice(Transform parent, string label, float y, Func<string[]> options, Func<int> get,
            Action<int> set)
        {
            RectTransform row = Row(parent, label, y, out RectTransform area, out Image tint, out Image bar,
                out TextMeshProUGUI caption);
            Image left = Sprite("Esquerda", area, MenuArt.Arrow, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(12f, 0f), new Vector2(12f, 24f), Accent);
            left.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image right = Sprite("Direita", area, MenuArt.Arrow, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(12f, 24f), Accent);
            var center = new Vector2(0.5f, 0.5f);
            var control = new ChoiceRow
            {
                Left = left, Right = right, Options = options, Get = get, Set = set,
                Value = Value(area, center, center, new Vector2(0f, 1f), k_ControlWidth - 60f, 26f,
                    TextAlignmentOptions.Center),
            };
            control.Value.enableAutoSizing = true;
            control.Value.fontSizeMin = 16f;
            control.Value.fontSizeMax = 26f;
            return Setup(control, row, area, tint, bar, caption);
        }

        public KeyRow KeyAction(Transform parent, string label, float y, string actionPath)
        {
            KeyRow control = KeyBase(parent, label, y, k => k.ActionPath = actionPath);
            return control;
        }

        public KeyRow KeyCustom(Transform parent, string label, float y, Func<Key> get, Action<Key> set)
        {
            return KeyBase(parent, label, y, k =>
            {
                k.GetKey = get;
                k.SetKey = set;
            });
        }

        KeyRow KeyBase(Transform parent, string label, float y, Action<KeyRow> configure)
        {
            RectTransform row = Row(parent, label, y, out RectTransform area, out Image tint, out Image bar,
                out TextMeshProUGUI caption);
            var right = new Vector2(1f, 0.5f);
            var size = new Vector2(240f, 36f);
            Image cap = ItemUI.Panel("Tecla", area, right, Vector2.zero, size, new Color(0.12f, 0.15f, 0.19f, 1f));
            cap.rectTransform.pivot = right;
            Image border = Sprite("Contorno", cap.transform, MenuArt.Outline, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size, Muted, true);
            var center = new Vector2(0.5f, 0.5f);
            var control = new KeyRow
            {
                Cap = cap, CapBorder = border,
                Value = Value(cap.transform, center, center, new Vector2(0f, 1f), size.x - 12f, 24f,
                    TextAlignmentOptions.Center),
            };
            configure(control);
            return Setup(control, row, area, tint, bar, caption);
        }

        // ---------- peças de tela ----------

        // Degradê escuro saindo da borda esquerda (atrás das faixas)
        public static RawImage LeftShade(Transform parent, float width, float strength)
        {
            var go = new GameObject("Degrade", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            var image = go.AddComponent<RawImage>();
            image.texture = MenuArt.Shade;
            image.color = new Color(Dark.r, Dark.g, Dark.b, strength);
            image.raycastTarget = false;
            return image;
        }

        // Painel de vidro com os cantos chanfrados, o contorno ciano fraco e um traço aceso no alto. 'position' é o
        // canto de cima à esquerda (âncora no alto à esquerda do 'parent')
        public static RectTransform Glass(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var topLeft = new Vector2(0f, 1f);
            Image fill = Sprite(name, parent, MenuArt.GlassFill, topLeft, topLeft, position, size,
                new Color(0.035f, 0.05f, 0.078f, 0.86f), true);
            Sprite("Contorno", fill.transform, MenuArt.GlassLine, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size, new Color(Accent.r, Accent.g, Accent.b, 0.35f), true);
            Image accent = ItemUI.Panel("Traco", fill.transform, topLeft, new Vector2(MenuArt.GlassCut + 12f, 2f),
                new Vector2(120f, 4f), Accent);
            accent.rectTransform.pivot = topLeft;
            return fill.rectTransform;
        }

        // Rodapé: faixa escura no pé da tela com as teclas (tecla em caixa e o que ela faz) e um texto à direita
        public static RectTransform Footer(Transform parent, (string Key, string Label)[] hints, string right)
        {
            RectTransform bar = ItemUI.Rect("Rodape", parent, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 54f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 54f);
            ItemUI.Fill("Fundo", bar, 0f, new Color(Dark.r, Dark.g, Dark.b, 0.75f));
            Image line = ItemUI.Panel("Linha", bar, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f),
                new Color(Accent.r, Accent.g, Accent.b, 0.27f));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);

            float x = 48f;
            var left = new Vector2(0f, 0.5f);
            foreach ((string key, string label) in hints)
            {
                TextMeshProUGUI keyText = Label("Tecla", bar, left, Vector2.zero, new Vector2(200f, 26f), 17f,
                    TextAlignmentOptions.Center, Text, ItemFonts.VisorWide);
                keyText.text = key;
                keyText.ForceMeshUpdate();
                float keyWidth = Mathf.Ceil(keyText.preferredWidth) + 18f;
                Image cap = ItemUI.Panel("Caixa", bar, left, new Vector2(x, -1f), new Vector2(keyWidth, 26f),
                    new Color(0.12f, 0.15f, 0.19f, 1f));
                cap.rectTransform.pivot = left;
                Sprite("Contorno", cap.transform, MenuArt.Outline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(keyWidth, 26f), Muted, true);
                keyText.transform.SetParent(cap.transform, false);
                keyText.rectTransform.anchorMin = keyText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                keyText.rectTransform.anchoredPosition = new Vector2(0f, 1f);
                keyText.rectTransform.sizeDelta = new Vector2(keyWidth, 26f);
                x += keyWidth + 10f;

                TextMeshProUGUI text = Label("Acao", bar, left, new Vector2(x, 0f), new Vector2(400f, 30f), 20f,
                    TextAlignmentOptions.MidlineLeft, Muted, ItemFonts.Visor);
                text.rectTransform.pivot = left;
                text.text = label;
                text.ForceMeshUpdate();
                x += Mathf.Ceil(text.preferredWidth) + 36f;
            }

            if (!string.IsNullOrEmpty(right))
            {
                var rightAnchor = new Vector2(1f, 0.5f);
                TextMeshProUGUI version = Label("Direita", bar, rightAnchor, new Vector2(-48f, 0f), new Vector2(900f, 30f),
                    20f, TextAlignmentOptions.MidlineRight, new Color(Muted.r, Muted.g, Muted.b, 0.8f), ItemFonts.Visor);
                version.rectTransform.pivot = rightAnchor;
                version.text = right;
            }

            return bar;
        }

        // Versão do jogo para o rodapé
        public static string VersionText =>
            $"v{Application.version}" + (Debug.isDebugBuild ? "  ·  BUILD DE DESENVOLVIMENTO" : "");
    }
}
