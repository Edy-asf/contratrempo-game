using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Escolha de itens ao limpar cada sala de combate: pausa o jogo (Time.timeScale = 0, então o multiplicador de
    // tempo também para), mostra ItemCatalog.Choices cartas (4, itens diferentes, sorteados pela raridade) e o
    // jogador marca exatamente ItemCatalog.Picks (2) e confirma. Clique ou teclas 1 a 4 marcam e desmarcam; o botão
    // "Confirmar" (ou Enter) só funciona com 2 marcadas. Na sala do chefe, a chance de Raro dobra. O RoomManager
    // espera o fim da escolha (Offer) antes de abrir as portas. Durante a escolha, o menu de pausa do Microgame fica
    // desligado (ele trancaria o cursor no clique).
    // Animação (tempo sem escala): a cena desfoca e escurece (MenuBackdropFx), o título entra e as cartas saem de baixo
    // uma a uma, viradas para baixo (ItemArt.CardBack), e viram ao chegar; antes de virar, uma Incomum brilha verde e uma
    // Rara treme e brilha vermelho, com som próprio. Clique ou tecla durante a distribuição pula para o fim dela. Ao
    // confirmar, as escolhidas voam até a célula delas na grade de itens do visor (ItemHUD, que pula quando a carta
    // chega) e as outras caem e somem; o fundo clareia e o tempo volta numa rampa curta, sem salto. Sons: UiSounds.
    public class ItemDraft : MonoBehaviour
    {
        public static ItemDraft Instance { get; private set; }

        // Cada carta escolhida, quando a escolha fecha (o CardPickupToast mostra o aviso)
        public static event System.Action<ItemDefinition> OnCardPicked;

        public ItemCatalog Catalog;

        public bool IsOpen { get; private set; }

        const float k_CardSpacing = 390f;
        const float k_CardY = -20f;

        // Tempos (segundos sem escala)
        const float k_FadeIn = 0.55f;
        const float k_FirstDeal = 0.5f;
        const float k_DealGap = 0.26f;
        const float k_DealTime = 0.7f;
        const float k_FlipTime = 0.4f;
        const float k_ChargeUncommon = 0.55f;
        const float k_ChargeRare = 1.0f;
        const float k_FlyTime = 0.85f;
        const float k_FallTime = 0.55f;
        const float k_TimeRamp = 0.6f;

        // Antes disso, clique e tecla não pulam a distribuição (o clique do último tiro não conta)
        const float k_SkipGuard = 0.5f;

        static readonly Color k_Overlay = new Color(0.01f, 0.015f, 0.025f, 0.8f);
        static readonly Color k_Hint = new Color(0.65f, 0.72f, 0.8f);
        static readonly Color k_Warn = new Color(1f, 0.7f, 0.3f);
        static readonly Color k_ConfirmOn = new Color(0.25f, 0.75f, 0.95f, 1f);
        static readonly Color k_ConfirmOff = new Color(0.22f, 0.25f, 0.28f, 0.9f);

        enum CardState
        {
            Waiting,
            Dealing,
            Charging,
            Flipping,
            Ready,
            Flying,
            Falling,
            Done,
        }

        class Card
        {
            public ItemDefinition Item;
            public ItemCardView View;
            public RectTransform Root;
            public CanvasGroup Group;
            public bool Selected;
            public Vector2 Target;
            public CardState State;
            public float Delay;
            public float T;
            public float Tilt;
            public bool Revealed;
            public float Lift;
            public float Scale = 1f;
            public Vector2 From;
            public Vector2 To;
            public float Spin;
        }

        Canvas m_Canvas;
        RectTransform m_CanvasRect;
        Image m_Overlay;
        CanvasGroup m_Header;
        CanvasGroup m_Controls;
        readonly List<Card> m_Cards = new List<Card>();
        TextMeshProUGUI m_Hint;
        Image m_Confirm;
        Image m_Reroll;
        TextMeshProUGUI m_RerollLabel;
        bool m_BossRoom;
        TextMeshProUGUI m_ConfirmLabel;
        float m_WarnUntil;
        float m_Clock;
        float m_DealStart;
        bool m_Outro;
        Card m_Hovered;
        int m_HoveredButton;

        int Picks => Catalog ? Mathf.Max(1, Catalog.Picks) : 2;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            WeaponSpriteHUD.Raise(this);
        }

        // Para o RoomManager: abre a escolha e espera terminar (não faz nada sem catálogo ou sem jogador vivo).
        // bossRoom: sala do chefe (chance de Raro multiplicada por ItemCatalog.BossRareMultiplier).
        public static IEnumerator Offer(bool bossRoom)
        {
            if (!Instance || !Instance.Catalog || !PlayerStats.Instance)
                yield break;
            var health = PlayerStats.Instance.GetComponent<Unity.FPS.Game.Health>();
            if (health && health.CurrentHealth <= 0f)
                yield break;

            yield return Instance.Run(bossRoom);
        }

        IEnumerator Run(bool bossRoom)
        {
            List<ItemDefinition> items = Catalog.Roll(Catalog.Choices, bossRoom ? Catalog.BossRareMultiplier : 1f);
            if (items.Count == 0)
                yield break;

            float previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            var menu = FindAnyObjectByType<InGameMenuManager>();
            if (menu)
            {
                if (menu.MenuRoot)
                    menu.MenuRoot.SetActive(false);
                menu.enabled = false;
            }

            IsOpen = true;
            // O ÍCARO-9 abaixa a arma enquanto escolhe (levanta de novo depois da escolha)
            WeaponSpriteHUD.Lower(this);
            m_BossRoom = bossRoom;
            Pause();
            BuildFrame(bossRoom);
            Deal(items);
            MenuBackdropFx.Target = 1f;

            bool confirmed = false;
            while (!confirmed)
            {
                Pause();
                m_Clock += Time.unscaledDeltaTime;
                AnimateIntro();
                if (AllReady)
                    confirmed = ReadInput();
                else if (m_Clock > k_SkipGuard && SkipPressed())
                    SkipDeal();
                Refresh();
                yield return null;
            }

            // As escolhidas entram no inventário agora; a célula de cada uma só pula quando a carta chegar a ela
            UiSounds.Play(UiSounds.Confirm, 0.8f);
            UiSounds.Play(UiSounds.Whoosh, 0.5f);
            ItemHUD hud = ItemHUD.Instance;
            if (hud)
                hud.HoldPops = true;
            var picked = new List<ItemDefinition>();
            foreach (Card card in m_Cards)
            {
                if (!card.Selected)
                    continue;
                PlayerStats.Instance.AddItem(card.Item);
                picked.Add(card.Item);
            }

            MenuBackdropFx.Target = 0f;
            StartOutro();
            float outro = 0f;
            while (!OutroDone(outro))
            {
                Pause();
                outro += Time.unscaledDeltaTime;
                AnimateOutro(outro);
                yield return null;
            }

            WeaponSpriteHUD.Raise(this);

            // Solta o clique/tecla antes de voltar, para a confirmação não virar um tiro
            while (AnyInputHeld())
            {
                Pause();
                yield return null;
            }

            if (hud)
                hud.HoldPops = false;
            Clear();
            IsOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (menu)
                menu.enabled = true;
            foreach (ItemDefinition item in picked)
                OnCardPicked?.Invoke(item);

            // O tempo volta numa rampa curta (o menu de pausa, se abrir no meio, manda no tempo)
            for (float t = 0f; t < k_TimeRamp; t += Time.unscaledDeltaTime)
            {
                if (!(GameMenus.Instance && GameMenus.Instance.IsOpen))
                    Time.timeScale = Mathf.Lerp(0.05f, previousTimeScale, Smooth(t / k_TimeRamp));
                yield return null;
            }

            if (!(GameMenus.Instance && GameMenus.Instance.IsOpen))
                Time.timeScale = previousTimeScale;
        }

        static void Pause()
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        int SelectedCount
        {
            get
            {
                int count = 0;
                foreach (Card card in m_Cards)
                {
                    if (card.Selected)
                        count++;
                }

                return count;
            }
        }

        bool CanConfirm => SelectedCount == Picks;

        bool AllReady
        {
            get
            {
                foreach (Card card in m_Cards)
                {
                    if (card.State != CardState.Ready)
                        return false;
                }

                return m_Cards.Count > 0;
            }
        }

        // ---------- entrada ----------

        // Marca/desmarca pelas teclas e pelo clique; devolve true quando o jogador confirma
        bool ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Ficha da loja: troca as cartas por outras
                if (keyboard.rKey.wasPressedThisFrame)
                    TryReroll();
                for (int i = 0; i < m_Cards.Count && i < 9; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        Toggle(m_Cards[i]);
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    if (CanConfirm)
                        return true;
                    Refuse();
                }
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;

            if (m_Reroll && m_Reroll.gameObject.activeSelf && ItemUI.MouseOver(m_Reroll.rectTransform))
            {
                TryReroll();
                return false;
            }

            if (ItemUI.MouseOver(m_Confirm.rectTransform))
            {
                if (CanConfirm)
                    return true;
                Refuse();
                return false;
            }

            foreach (Card card in m_Cards)
            {
                if (ItemUI.MouseOver(card.Root))
                {
                    Toggle(card);
                    break;
                }
            }

            return false;
        }

        // Com 2 já marcadas, uma terceira não entra: o aviso pede para desmarcar uma antes
        void Toggle(Card card)
        {
            if (!card.Selected && SelectedCount >= Picks)
            {
                Refuse();
                return;
            }

            card.Selected = !card.Selected;
            UiSounds.Play(card.Selected ? UiSounds.Click : UiSounds.Unclick, 0.8f);
        }

        void Refuse()
        {
            m_WarnUntil = Time.unscaledTime + 1.5f;
            UiSounds.Play(UiSounds.Deny, 0.7f);
        }

        // Durante a distribuição: clique ou tecla pula para o fim
        static bool SkipPressed()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame ||
                   keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        void SkipDeal()
        {
            foreach (Card card in m_Cards)
            {
                if (card.State == CardState.Ready)
                    continue;
                card.State = CardState.Ready;
                card.Group.alpha = 1f;
                card.Root.anchoredPosition = card.Target;
                card.Root.localRotation = Quaternion.identity;
                card.Root.localScale = Vector3.one;
                card.View.Charge = 0f;
                if (!card.Revealed)
                {
                    card.Revealed = true;
                    card.View.SetFaceDown(false);
                    card.View.Flash();
                }
            }

            m_Clock = Mathf.Max(m_Clock, k_FadeIn + 0.3f);
            UiSounds.Play(UiSounds.Flip, 0.7f);
        }

        // Gasta uma ficha de rerrolagem (comprada na loja) e sorteia outras cartas
        void TryReroll()
        {
            if (!PlayerWallet.Instance || !PlayerWallet.Instance.UseRerollToken())
            {
                Refuse();
                return;
            }

            List<ItemDefinition> items = Catalog.Roll(Catalog.Choices, m_BossRoom ? Catalog.BossRareMultiplier : 1f);
            if (items.Count == 0)
                return;
            UiSounds.Play(UiSounds.Whoosh, 0.5f, 1.15f);
            Deal(items);
        }

        static bool AnyInputHeld()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;
            if (keyboard == null)
                return false;
            if (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed)
                return true;
            for (int i = 0; i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].isPressed || keyboard[Key.Numpad1 + i].isPressed)
                    return true;
            }

            return false;
        }

        // ---------- animação ----------

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float s = 1.4f;
            return 1f + t * t * ((s + 1f) * t + s);
        }

        static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        // Fundo e título entrando, cartas saindo do monte e virando
        void AnimateIntro()
        {
            float dt = Time.unscaledDeltaTime;
            m_Overlay.color = new Color(k_Overlay.r, k_Overlay.g, k_Overlay.b, k_Overlay.a * Smooth(m_Clock / k_FadeIn));
            m_Header.alpha = Smooth((m_Clock - 0.2f) / 0.5f);
            RectTransform header = (RectTransform)m_Header.transform;
            header.anchoredPosition = new Vector2(0f, Mathf.Round((1f - Smooth((m_Clock - 0.2f) / 0.5f)) * 24f / 3f) * 3f);
            m_Controls.alpha = Mathf.MoveTowards(m_Controls.alpha, AllReady ? 1f : 0f, dt * 5f);

            foreach (Card card in m_Cards)
            {
                switch (card.State)
                {
                    case CardState.Waiting:
                        if (m_Clock < card.Delay)
                            break;
                        card.State = CardState.Dealing;
                        card.T = 0f;
                        card.Group.alpha = 1f;
                        UiSounds.Play(UiSounds.Deal, 0.55f, Random.Range(0.92f, 1.08f));
                        break;

                    case CardState.Dealing:
                    {
                        card.T += dt / k_DealTime;
                        Vector2 start = card.Target + new Vector2(card.Target.x * -0.3f, -820f);
                        card.Root.anchoredPosition = Vector2.LerpUnclamped(start, card.Target, EaseOutBack(card.T));
                        card.Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(card.Tilt, 0f, Smooth(card.T)));
                        if (card.T < 1f)
                            break;
                        card.Root.anchoredPosition = card.Target;
                        card.Root.localRotation = Quaternion.identity;
                        card.T = 0f;
                        card.State = card.Item.Rarity == ItemRarity.Comum ? CardState.Flipping : CardState.Charging;
                        if (card.State == CardState.Flipping)
                            UiSounds.Play(UiSounds.Flip, 0.7f, Random.Range(0.95f, 1.05f));
                        break;
                    }

                    case CardState.Charging:
                    {
                        bool rare = card.Item.Rarity == ItemRarity.Raro;
                        card.T += dt / (rare ? k_ChargeRare : k_ChargeUncommon);
                        card.View.Charge = card.T;
                        // Treme em degraus de 3 px (um pixel da arte), mais forte perto de virar
                        float amplitude = (rare ? 3f : 1f) * Mathf.Clamp01(card.T);
                        Vector2 shake = new Vector2(Mathf.Round(Random.Range(-amplitude, amplitude)),
                            Mathf.Round(Random.Range(-amplitude, amplitude))) * 3f;
                        card.Root.anchoredPosition = card.Target + shake;
                        if (card.T < 1f)
                            break;
                        card.Root.anchoredPosition = card.Target;
                        card.T = 0f;
                        card.State = CardState.Flipping;
                        UiSounds.Play(UiSounds.Flip, 0.7f);
                        break;
                    }

                    case CardState.Flipping:
                        card.T += dt / k_FlipTime;
                        card.Root.localScale = new Vector3(Mathf.Max(0.02f, Mathf.Abs(Mathf.Cos(Mathf.PI * Mathf.Clamp01(card.T)))), 1f, 1f);
                        if (!card.Revealed && card.T >= 0.5f)
                        {
                            card.Revealed = true;
                            card.View.Charge = 0f;
                            card.View.SetFaceDown(false);
                            card.View.Flash();
                            if (card.Item.Rarity == ItemRarity.Raro)
                                UiSounds.Play(UiSounds.RevealRare, 0.9f);
                            else if (card.Item.Rarity == ItemRarity.Incomum)
                                UiSounds.Play(UiSounds.Reveal, 0.75f);
                        }

                        if (card.T < 1f)
                            break;
                        card.Root.localScale = Vector3.one;
                        card.State = CardState.Ready;
                        break;
                }
            }
        }

        // Cartas prontas: erguem com o mouse e ao serem marcadas; textos e botões
        void Refresh()
        {
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            Card hovered = null;
            foreach (Card card in m_Cards)
            {
                if (card.State != CardState.Ready)
                    continue;
                bool over = ItemUI.MouseOver(card.Root);
                if (over)
                    hovered = card;
                card.Lift = Mathf.Lerp(card.Lift, card.Selected ? 18f : over ? 8f : 0f, k);
                card.Scale = Mathf.Lerp(card.Scale, card.Selected ? 1.05f : over ? 1.03f : 1f, k);
                card.Root.anchoredPosition = card.Target + new Vector2(0f, card.Lift);
                card.Root.localScale = Vector3.one * card.Scale;
                card.View.Selected = card.Selected;
                card.View.Hovered = over;
            }

            if (hovered != null && hovered != m_Hovered)
                UiSounds.Play(UiSounds.Hover, 0.5f);
            m_Hovered = hovered;

            int selected = SelectedCount;
            bool warn = Time.unscaledTime < m_WarnUntil;
            m_Hint.color = warn ? k_Warn : k_Hint;
            m_Hint.text = warn
                ? CanConfirm ? $"Já tem {Picks} marcadas: desmarque uma para trocar" : $"Marque {Picks} cartas antes de confirmar"
                : $"Marque {Picks} cartas (clique ou teclas 1 a {m_Cards.Count}) e confirme";

            bool canConfirm = selected == Picks;
            bool overConfirm = AllReady && ItemUI.MouseOver(m_Confirm.rectTransform);
            bool overReroll = AllReady && m_Reroll && m_Reroll.gameObject.activeSelf && ItemUI.MouseOver(m_Reroll.rectTransform);
            int button = overConfirm ? 1 : overReroll ? 2 : 0;
            if (button != 0 && button != m_HoveredButton)
                UiSounds.Play(UiSounds.Hover, 0.5f);
            m_HoveredButton = button;

            m_Confirm.color = canConfirm ? (overConfirm ? Color.Lerp(k_ConfirmOn, Color.white, 0.3f) : k_ConfirmOn)
                : k_ConfirmOff;
            m_ConfirmLabel.color = canConfirm ? Color.white : new Color(0.55f, 0.58f, 0.62f);
            m_ConfirmLabel.text = canConfirm ? "Confirmar  [Enter]" : $"Confirmar  ({selected}/{Picks})";

            int tokens = PlayerWallet.Instance ? PlayerWallet.Instance.RerollTokens : 0;
            if (m_Reroll)
            {
                m_Reroll.gameObject.SetActive(tokens > 0);
                m_Reroll.color = overReroll ? new Color(0.38f, 0.42f, 0.48f, 0.95f) : new Color(0.3f, 0.32f, 0.36f, 0.95f);
                m_RerollLabel.text = $"Rerrolar  [R]  (x{tokens})";
            }
        }

        // Escolhidas voam até a grade de itens do visor; as outras caem e somem
        void StartOutro()
        {
            m_Outro = true;
            int order = 0;
            foreach (Card card in m_Cards)
            {
                card.T = 0f;
                card.From = card.Root.anchoredPosition;
                card.Spin = Random.value < 0.5f ? -1f : 1f;
                card.View.Hovered = false;
                if (!card.Selected)
                {
                    card.State = CardState.Falling;
                    continue;
                }

                card.State = CardState.Flying;
                card.Delay = order++ * 0.16f;
                card.To = HudPoint(card.Item);
                card.Root.SetAsLastSibling();
            }
        }

        void AnimateOutro(float clock)
        {
            float dt = Time.unscaledDeltaTime;
            m_Header.alpha = Mathf.MoveTowards(m_Header.alpha, 0f, dt * 6f);
            m_Controls.alpha = Mathf.MoveTowards(m_Controls.alpha, 0f, dt * 6f);
            m_Overlay.color = new Color(k_Overlay.r, k_Overlay.g, k_Overlay.b,
                k_Overlay.a * (1f - Smooth((clock - 0.15f) / 0.5f)));

            foreach (Card card in m_Cards)
            {
                switch (card.State)
                {
                    case CardState.Falling:
                    {
                        card.T += dt / k_FallTime;
                        float u = Mathf.Clamp01(card.T);
                        card.Root.anchoredPosition = card.From + new Vector2(0f, -170f * u * u);
                        card.Root.localRotation = Quaternion.Euler(0f, 0f, card.Spin * 9f * u);
                        card.Group.alpha = Mathf.Ceil((1f - u) * 4f) / 4f;
                        if (card.T >= 1f)
                            card.State = CardState.Done;
                        break;
                    }

                    case CardState.Flying:
                    {
                        if (clock < card.Delay)
                            break;
                        card.T += dt / k_FlyTime;
                        float u = EaseInOutCubic(card.T);
                        Vector2 arc = new Vector2(0f, Mathf.Sin(Mathf.PI * u) * 110f);
                        card.Root.anchoredPosition = Vector2.Lerp(card.From, card.To, u) + arc;
                        card.Root.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.12f, u * u);
                        card.Root.localRotation = Quaternion.Euler(0f, 0f, card.Spin * 8f * Mathf.Sin(Mathf.PI * u));
                        card.Group.alpha = card.T > 0.8f ? Mathf.Ceil((1f - card.T) / 0.2f * 3f) / 3f : 1f;
                        if (card.T < 1f)
                            break;
                        card.State = CardState.Done;
                        card.Group.alpha = 0f;
                        if (ItemHUD.Instance)
                            ItemHUD.Instance.PopItem(card.Item);
                        UiSounds.Play(UiSounds.Click, 0.55f, 1.5f);
                        break;
                    }
                }
            }
        }

        bool OutroDone(float clock)
        {
            if (clock < 1f)
                return false;
            foreach (Card card in m_Cards)
            {
                if (card.State != CardState.Done)
                    return false;
            }

            return true;
        }

        // Centro da célula do item na grade do visor, nas coordenadas do canvas das cartas (sem a célula, o canto
        // superior esquerdo, onde a grade fica)
        Vector2 HudPoint(ItemDefinition item)
        {
            Rect area = m_CanvasRect.rect;
            var fallback = new Vector2(area.xMin + 130f, area.yMax - 90f);
            if (!ItemHUD.Instance || !ItemHUD.Instance.TryGetScreenPoint(item, out Vector2 screen))
                return fallback;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(m_CanvasRect, screen, null, out Vector2 local)
                ? local
                : fallback;
        }

        // ---------- interface ----------

        // Fundo, título, dica e botões (as cartas entram pelo Deal)
        void BuildFrame(bool bossRoom)
        {
            Clear();
            m_Outro = false;
            m_Clock = 0f;
            m_Hovered = null;
            m_HoveredButton = 0;
            m_Canvas = ItemUI.CreateCanvas(transform, "CartasDeItem", 100);
            m_CanvasRect = (RectTransform)m_Canvas.transform;
            Transform root = m_Canvas.transform;
            m_Overlay = ItemUI.Fill("Escurecer", root, 0f, new Color(k_Overlay.r, k_Overlay.g, k_Overlay.b, 0f));

            var center = new Vector2(0.5f, 0.5f);
            RectTransform headerRoot = ItemUI.Rect("Cabecalho", root, center, Vector2.zero, new Vector2(1920f, 1080f));
            m_Header = headerRoot.gameObject.AddComponent<CanvasGroup>();
            m_Header.alpha = 0f;
            TextMeshProUGUI title = ItemUI.Text("Titulo", headerRoot, center, new Vector2(0f, 372f), new Vector2(1400f, 70f),
                54f, TextAlignmentOptions.Center, new Color(0.85f, 0.95f, 1f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;
            title.text = "ESCOLHA DE CARTAS";
            TextMeshProUGUI subtitle = ItemUI.Text("Subtitulo", headerRoot, center, new Vector2(0f, 322f),
                new Vector2(1400f, 40f), 28f, TextAlignmentOptions.Center, new Color(0.45f, 0.9f, 1f));
            string rareBonus = $"  <color=#{ItemUI.Hex(Catalog.RareColor)}>(chance de Raro x{Catalog.BossRareMultiplier:0.#})</color>";
            subtitle.text = bossRoom ? "Recompensa do chefe" + rareBonus : "Recompensa da arena";
            m_Hint = ItemUI.Text("Dica", headerRoot, center, new Vector2(0f, 286f), new Vector2(1400f, 34f), 22f,
                TextAlignmentOptions.Center, k_Hint);

            RectTransform controls = ItemUI.Rect("Botoes", root, center, Vector2.zero, new Vector2(1920f, 1080f));
            m_Controls = controls.gameObject.AddComponent<CanvasGroup>();
            m_Controls.alpha = 0f;
            m_Reroll = ItemUI.Panel("Rerrolar", controls, center, new Vector2(-420f, -330f), new Vector2(300f, 54f),
                new Color(0.3f, 0.32f, 0.36f, 0.95f));
            m_RerollLabel = ItemUI.Text("Texto", m_Reroll.transform, center, Vector2.zero, new Vector2(300f, 54f), 24f,
                TextAlignmentOptions.Center, new Color(0.55f, 0.9f, 1f));

            m_Confirm = ItemUI.Panel("Confirmar", controls, center, new Vector2(0f, -330f), new Vector2(360f, 64f),
                k_ConfirmOff);
            m_ConfirmLabel = ItemUI.Text("Texto", m_Confirm.transform, center, Vector2.zero, new Vector2(360f, 64f), 28f,
                TextAlignmentOptions.Center, Color.white);
            m_ConfirmLabel.fontStyle = FontStyles.Bold;

            // Fonte em pixel das cartas também nos textos da escolha
            if (ItemFonts.Bold)
            {
                title.font = subtitle.font = m_ConfirmLabel.font = m_RerollLabel.font = ItemFonts.Bold;
                m_Hint.font = ItemFonts.Regular;
                title.fontStyle = m_ConfirmLabel.fontStyle = FontStyles.Normal;
            }
        }

        // Troca as cartas pelas de 'items', que saem do monte viradas para baixo, uma depois da outra
        void Deal(List<ItemDefinition> items)
        {
            foreach (Card old in m_Cards)
            {
                if (old.Root)
                    Destroy(old.Root.gameObject);
            }
            m_Cards.Clear();
            m_Hovered = null;
            m_DealStart = Mathf.Max(m_Clock, k_FirstDeal) + (m_Clock > 0f ? 0.05f : 0f);

            for (int i = 0; i < items.Count; i++)
            {
                float x = (i - (items.Count - 1) * 0.5f) * k_CardSpacing;
                var target = new Vector2(x, k_CardY);
                int owned = PlayerStats.Instance ? PlayerStats.Instance.CountOf(items[i]) : 0;
                ItemCardView view = ItemCardView.Create(m_Canvas.transform, items[i], Catalog, i, owned, target);
                var card = new Card
                {
                    Item = items[i], View = view, Root = view.Root, Target = target, State = CardState.Waiting,
                    Delay = m_DealStart + i * k_DealGap, Tilt = Random.Range(-14f, 14f),
                    Group = view.gameObject.AddComponent<CanvasGroup>(),
                };
                card.Group.alpha = 0f;
                view.SetFaceDown(true);
                card.Root.anchoredPosition = target + new Vector2(0f, -820f);
                m_Cards.Add(card);
            }

            // Os botões ficam por cima das cartas
            m_Controls.transform.SetAsLastSibling();
        }

        void Clear()
        {
            m_Cards.Clear();
            if (m_Canvas)
                Destroy(m_Canvas.gameObject);
            m_Canvas = null;
        }
    }
}
