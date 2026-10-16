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
    public class ItemDraft : MonoBehaviour
    {
        public static ItemDraft Instance { get; private set; }

        public ItemCatalog Catalog;

        public bool IsOpen { get; private set; }

        const float k_CardSpacing = 390f;
        const float k_CardY = -20f;

        static readonly Color k_Overlay = new Color(0.01f, 0.015f, 0.025f, 0.88f);
        static readonly Color k_Hint = new Color(0.65f, 0.72f, 0.8f);
        static readonly Color k_Warn = new Color(1f, 0.7f, 0.3f);
        static readonly Color k_ConfirmOn = new Color(0.25f, 0.75f, 0.95f, 1f);
        static readonly Color k_ConfirmOff = new Color(0.22f, 0.25f, 0.28f, 0.9f);

        class Card
        {
            public ItemDefinition Item;
            public ItemCardView View;
            public RectTransform Root;
            public bool Selected;
        }

        Canvas m_Canvas;
        readonly List<Card> m_Cards = new List<Card>();
        TextMeshProUGUI m_Hint;
        Image m_Confirm;
        Image m_Reroll;
        TextMeshProUGUI m_RerollLabel;
        bool m_BossRoom;
        TextMeshProUGUI m_ConfirmLabel;
        float m_WarnUntil;

        int Picks => Catalog ? Mathf.Max(1, Catalog.Picks) : 2;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
            m_BossRoom = bossRoom;
            Build(items, bossRoom);
            bool confirmed = false;
            while (!confirmed)
            {
                Pause();
                confirmed = ReadInput();
                Refresh();
                yield return null;
            }

            foreach (Card card in m_Cards)
            {
                if (card.Selected)
                    PlayerStats.Instance.AddItem(card.Item);
            }

            // Solta o clique/tecla antes de voltar, para a confirmação não virar um tiro
            while (AnyInputHeld())
            {
                Pause();
                yield return null;
            }

            Clear();
            IsOpen = false;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (menu)
                menu.enabled = true;
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

                if (CanConfirm && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                    return true;
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;

            if (m_Reroll && m_Reroll.gameObject.activeSelf && ItemUI.MouseOver(m_Reroll.rectTransform))
            {
                TryReroll();
                return false;
            }

            if (CanConfirm && ItemUI.MouseOver(m_Confirm.rectTransform))
                return true;
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
                m_WarnUntil = Time.unscaledTime + 1.5f;
                return;
            }

            card.Selected = !card.Selected;
        }

        void Refresh()
        {
            foreach (Card card in m_Cards)
            {
                bool over = ItemUI.MouseOver(card.Root);
                card.Root.localScale = Vector3.one * (card.Selected ? 1.05f : over ? 1.03f : 1f);
                card.Root.anchoredPosition = new Vector2(card.Root.anchoredPosition.x,
                    card.Selected ? k_CardY + 18f : over ? k_CardY + 8f : k_CardY);
                card.View.Selected = card.Selected;
                card.View.Hovered = over;
            }

            int selected = SelectedCount;
            bool warn = Time.unscaledTime < m_WarnUntil;
            m_Hint.color = warn ? k_Warn : k_Hint;
            m_Hint.text = warn
                ? $"Já tem {Picks} marcadas: desmarque uma para trocar"
                : $"Marque {Picks} cartas (clique ou teclas 1 a {m_Cards.Count}) e confirme";

            bool canConfirm = selected == Picks;
            bool overConfirm = canConfirm && ItemUI.MouseOver(m_Confirm.rectTransform);
            m_Confirm.color = canConfirm ? (overConfirm ? Color.Lerp(k_ConfirmOn, Color.white, 0.3f) : k_ConfirmOn)
                : k_ConfirmOff;
            m_ConfirmLabel.color = canConfirm ? Color.white : new Color(0.55f, 0.58f, 0.62f);
            m_ConfirmLabel.text = canConfirm ? "Confirmar  [Enter]" : $"Confirmar  ({selected}/{Picks})";

            int tokens = PlayerWallet.Instance ? PlayerWallet.Instance.RerollTokens : 0;
            if (m_Reroll)
            {
                m_Reroll.gameObject.SetActive(tokens > 0);
                m_RerollLabel.text = $"Rerrolar  [R]  (x{tokens})";
            }
        }

        // Gasta uma ficha de rerrolagem (comprada na loja) e sorteia outras cartas
        void TryReroll()
        {
            if (!PlayerWallet.Instance || !PlayerWallet.Instance.UseRerollToken())
                return;
            List<ItemDefinition> items = Catalog.Roll(Catalog.Choices, m_BossRoom ? Catalog.BossRareMultiplier : 1f);
            if (items.Count > 0)
                Build(items, m_BossRoom);
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

        // ---------- interface ----------

        void Build(List<ItemDefinition> items, bool bossRoom)
        {
            Clear();
            m_Canvas = ItemUI.CreateCanvas(transform, "CartasDeItem", 100);
            Transform root = m_Canvas.transform;
            ItemUI.Fill("Escurecer", root, 0f, k_Overlay);

            var center = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI title = ItemUI.Text("Titulo", root, center, new Vector2(0f, 350f), new Vector2(1400f, 60f),
                46f, TextAlignmentOptions.Center, new Color(0.85f, 0.95f, 1f));
            title.fontStyle = FontStyles.Bold;
            string rareBonus = $"<size=60%><color=#{ItemUI.Hex(Catalog.RareColor)}>" +
                               $"(chance de Raro x{Catalog.BossRareMultiplier:0.#})</color></size>";
            title.text = bossRoom
                ? $"Chefe derrotado: escolha {Picks} itens {rareBonus}"
                : $"Sala limpa: escolha {Picks} itens";
            m_Hint = ItemUI.Text("Dica", root, center, new Vector2(0f, 302f), new Vector2(1400f, 40f), 24f,
                TextAlignmentOptions.Center, k_Hint);

            for (int i = 0; i < items.Count; i++)
            {
                float x = (i - (items.Count - 1) * 0.5f) * k_CardSpacing;
                m_Cards.Add(BuildCard(root, items[i], i, new Vector2(x, k_CardY)));
            }

            m_Reroll = ItemUI.Panel("Rerrolar", root, center, new Vector2(-420f, -330f), new Vector2(300f, 54f),
                new Color(0.3f, 0.32f, 0.36f, 0.95f));
            m_RerollLabel = ItemUI.Text("Texto", m_Reroll.transform, center, Vector2.zero, new Vector2(300f, 54f), 24f,
                TextAlignmentOptions.Center, new Color(0.55f, 0.9f, 1f));

            m_Confirm = ItemUI.Panel("Confirmar", root, center, new Vector2(0f, -330f), new Vector2(360f, 64f),
                k_ConfirmOff);
            m_ConfirmLabel = ItemUI.Text("Texto", m_Confirm.transform, center, Vector2.zero, new Vector2(360f, 64f), 28f,
                TextAlignmentOptions.Center, Color.white);
            m_ConfirmLabel.fontStyle = FontStyles.Bold;

            // Fonte em pixel das cartas também nos textos da escolha
            if (ItemFonts.Bold)
            {
                title.font = m_ConfirmLabel.font = m_RerollLabel.font = ItemFonts.Bold;
                m_Hint.font = ItemFonts.Regular;
                title.fontStyle = m_ConfirmLabel.fontStyle = FontStyles.Normal;
            }

            Refresh();
        }

        Card BuildCard(Transform parent, ItemDefinition item, int index, Vector2 position)
        {
            int owned = PlayerStats.Instance ? PlayerStats.Instance.CountOf(item) : 0;
            ItemCardView view = ItemCardView.Create(parent, item, Catalog, index, owned, position);
            return new Card { Item = item, View = view, Root = view.Root };
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
