using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Tela da loja: pausa o jogo (Time.timeScale = 0; o multiplicador de tempo também para) e mostra as ofertas do
    // terminal (itens, armas e serviços, 5 por linha) em cartas com o preço atual (com a inflação do multiplicador). Clique ou teclas 1 a 9 compram; G, Esc
    // ou o botão "Sair" fecham. Cartas sem dinheiro ficam com o preço em vermelho; vendidas ficam marcadas. Também
    // mostra o aviso "[G] Abrir loja" perto de um terminal. Durante a loja o menu de pausa do Microgame fica desligado
    // (ele trancaria o cursor no clique). O ShopDirector cria este componente.
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        const float k_CardWidth = 300f;
        const float k_CardHeight = 330f;
        const float k_CardSpacingX = 320f;
        const float k_CardSpacingY = 350f;
        const int k_PerRow = 5;

        static readonly Color k_Overlay = new Color(0.02f, 0.04f, 0.06f, 0.82f);
        static readonly Color k_CardBack = new Color(0.07f, 0.09f, 0.11f, 0.97f);
        static readonly Color k_ServiceColor = new Color(0.4f, 0.85f, 1f);
        static readonly Color k_Cronita = new Color(0.55f, 0.9f, 1f);
        static readonly Color k_TooExpensive = new Color(1f, 0.35f, 0.3f);
        static readonly Color k_Sold = new Color(0.4f, 0.42f, 0.45f);

        class Card
        {
            public ShopOffer Offer;
            public RectTransform Root;
            public Image Border;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI Description;
            public GameObject SoldTag;
        }

        Canvas m_PromptCanvas;
        TextMeshProUGUI m_Prompt;
        ShopTerminal m_PromptTerminal;

        Canvas m_Canvas;
        TextMeshProUGUI m_Wallet;
        TextMeshProUGUI m_Message;
        Image m_Close;
        readonly List<Card> m_Cards = new List<Card>();
        ShopTerminal m_Terminal;
        float m_MessageUntil;

        void Awake()
        {
            Instance = this;
            m_PromptCanvas = ItemUI.CreateCanvas(transform, "AvisoLoja", 45);
            m_Prompt = ItemUI.Text("Aviso", m_PromptCanvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f),
                new Vector2(600f, 40f), 28f, TextAlignmentOptions.Center, k_Cronita);
            m_Prompt.fontStyle = FontStyles.Bold;
            m_Prompt.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- aviso perto do terminal ----------

        public void SetPrompt(ShopTerminal terminal, bool near)
        {
            if (near)
                m_PromptTerminal = terminal;
            else if (m_PromptTerminal == terminal)
                m_PromptTerminal = null;

            bool show = m_PromptTerminal && !IsOpen;
            if (m_Prompt.gameObject.activeSelf != show)
                m_Prompt.gameObject.SetActive(show);
            if (show)
                m_Prompt.text = $"[{GameSettings.KeyName(GameSettings.ShopKey)}] " +
                                $"Abrir {m_PromptTerminal.Title.ToLowerInvariant()}";
        }

        // ---------- loja aberta ----------

        public void Open(ShopTerminal terminal)
        {
            if (IsOpen)
                return;
            m_Terminal = terminal;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            var menu = FindAnyObjectByType<InGameMenuManager>();
            if (menu)
            {
                if (menu.MenuRoot)
                    menu.MenuRoot.SetActive(false);
                menu.enabled = false;
            }

            IsOpen = true;
            Build();
            // Espera soltar a tecla que abriu, para ela não fechar na hora
            while (AnyKeyHeld())
            {
                Pause();
                yield return null;
            }

            bool closing = false;
            while (!closing)
            {
                Pause();
                closing = ReadInput();
                Refresh();
                yield return null;
            }

            while (AnyKeyHeld())
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

        bool ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[GameSettings.ShopKey].wasPressedThisFrame ||
                    keyboard.escapeKey.wasPressedThisFrame)
                    return true;
                for (int i = 0; i < m_Cards.Count && i < 9; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                        Buy(m_Cards[i]);
                }
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (ItemUI.MouseOver(m_Close.rectTransform))
                return true;
            foreach (Card card in m_Cards)
            {
                if (ItemUI.MouseOver(card.Root))
                {
                    Buy(card);
                    break;
                }
            }

            return false;
        }

        static bool AnyKeyHeld()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;
            if (keyboard == null)
                return false;
            if (keyboard[GameSettings.ShopKey].isPressed || keyboard.escapeKey.isPressed)
                return true;
            for (int i = 0; i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].isPressed || keyboard[Key.Numpad1 + i].isPressed)
                    return true;
            }

            return false;
        }

        // ---------- compras ----------

        int PriceOf(ShopOffer offer)
        {
            ShopSettings s = ShopDirector.Settings;
            PlayerWallet wallet = PlayerWallet.Instance;
            int basePrice = offer.Kind switch
            {
                ShopOffer.OfferKind.Item => offer.Item ? s.ItemPrice(offer.Item.Rarity) : 0,
                ShopOffer.OfferKind.Weapon => offer.Weapon ? offer.Weapon.Price : 0,
                ShopOffer.OfferKind.Heal => s.HealPrice,
                ShopOffer.OfferKind.Reroll => s.RerollPrice,
                ShopOffer.OfferKind.Damage => s.DamageUpgradePrice +
                                              s.DamageUpgradePriceStep * (wallet ? wallet.DamageUpgrades : 0),
                _ => s.FireRateUpgradePrice + s.FireRateUpgradePriceStep * (wallet ? wallet.FireRateUpgrades : 0),
            };
            return s.Inflate(basePrice);
        }

        void Buy(Card card)
        {
            ShopOffer offer = card.Offer;
            PlayerWallet wallet = PlayerWallet.Instance;
            if (offer.Sold || !wallet)
                return;

            var health = PlayerStats.Instance ? PlayerStats.Instance.GetComponent<Health>() : null;
            if (offer.Kind == ShopOffer.OfferKind.Heal && health && health.CurrentHealth >= health.MaxHealth)
            {
                ShowMessage("Vida já está cheia");
                return;
            }

            PlayerArsenal arsenal = PlayerArsenal.Instance;
            if (offer.Kind == ShopOffer.OfferKind.Weapon && (!arsenal || arsenal.Owns(offer.Weapon)))
            {
                ShowMessage("Você já carrega esta arma");
                return;
            }

            int price = PriceOf(offer);
            if (!wallet.TrySpend(price))
            {
                ShowMessage("Cronitas insuficientes");
                return;
            }

            ShopSettings s = ShopDirector.Settings;
            switch (offer.Kind)
            {
                case ShopOffer.OfferKind.Item:
                    PlayerStats.Instance.AddItem(offer.Item);
                    break;
                case ShopOffer.OfferKind.Weapon:
                    if (!arsenal.Give(offer.Weapon))
                    {
                        wallet.Add(price);
                        ShowMessage("Não foi possível pegar a arma");
                        return;
                    }

                    break;
                case ShopOffer.OfferKind.Heal:
                    health.Heal(health.MaxHealth * s.HealFraction);
                    break;
                case ShopOffer.OfferKind.Reroll:
                    wallet.AddRerollToken();
                    break;
                case ShopOffer.OfferKind.Damage:
                    wallet.AddDamageUpgrade();
                    break;
                default:
                    wallet.AddFireRateUpgrade();
                    break;
            }

            if (!offer.Repeatable)
                offer.Sold = true;
            // Uma compra pode mudar o texto de outras cartas (armas: qual arma será trocada)
            foreach (Card other in m_Cards)
                other.Description.text = Describe(other.Offer);
            ShowMessage("Comprado!");
        }

        void ShowMessage(string text)
        {
            m_Message.text = text;
            m_MessageUntil = Time.unscaledTime + 1.5f;
        }

        // ---------- interface ----------

        string NameOf(ShopOffer offer) => offer.Kind switch
        {
            ShopOffer.OfferKind.Item => offer.Item ? offer.Item.DisplayName : "Item",
            ShopOffer.OfferKind.Weapon => offer.Weapon ? offer.Weapon.DisplayName : "Arma",
            ShopOffer.OfferKind.Heal => "Reparo de Casco",
            ShopOffer.OfferKind.Reroll => "Ficha de Rerrolagem",
            ShopOffer.OfferKind.Damage => "Calibrar Arma",
            _ => "Afinar Gatilho",
        };

        string Describe(ShopOffer offer)
        {
            ShopSettings s = ShopDirector.Settings;
            PlayerWallet wallet = PlayerWallet.Instance;
            switch (offer.Kind)
            {
                case ShopOffer.OfferKind.Item:
                    return offer.Item ? ItemUI.Describe(offer.Item, PlayerStats.Instance ?
                        PlayerStats.Instance.CountOf(offer.Item) : 0) : "";
                case ShopOffer.OfferKind.Weapon:
                    return offer.Weapon
                        ? offer.Weapon.Describe() +
                          $"\n<size=85%><color=#9AA4AE>{WeaponSlotHint(offer.Weapon)}</color></size>"
                        : "";
                case ShopOffer.OfferKind.Heal:
                    return $"Recupera {s.HealFraction:P0} da vida máxima.";
                case ShopOffer.OfferKind.Reroll:
                    return "Na escolha de itens, troca as 4 cartas por outras (tecla R)." +
                           $"\n<size=85%><color=#9AA4AE>Você tem: {(wallet ? wallet.RerollTokens : 0)}</color></size>";
                case ShopOffer.OfferKind.Damage:
                    return $"+{s.DamageUpgradeAmount:P0} de dano dos tiros nesta run. Fica mais caro a cada compra." +
                           $"\n<size=85%><color=#9AA4AE>Compradas: {(wallet ? wallet.DamageUpgrades : 0)}</color></size>";
                default:
                    return $"+{s.FireRateUpgradeAmount:P0} de cadência nesta run. Fica mais caro a cada compra." +
                           $"\n<size=85%><color=#9AA4AE>Compradas: {(wallet ? wallet.FireRateUpgrades : 0)}</color></size>";
            }
        }

        // Onde a arma comprada entra: no espaço livre ou no lugar da arma na mão
        static string WeaponSlotHint(WeaponDefinition weapon)
        {
            PlayerArsenal arsenal = PlayerArsenal.Instance;
            if (!arsenal)
                return "";
            if (arsenal.Owns(weapon))
                return "Você já carrega esta arma.";
            return arsenal.IsFull && arsenal.Active
                ? $"Substitui a arma na mão: {arsenal.Active.WeaponName}."
                : $"Vai para o espaço livre ({arsenal.MaxCarried} armas; Q/E trocam).";
        }

        Color ColorOf(ShopOffer offer)
        {
            if (offer.Kind == ShopOffer.OfferKind.Weapon && offer.Weapon)
                return offer.Weapon.Color;

            ItemCatalog catalog = PlayerStats.Instance ? PlayerStats.Instance.Catalog : null;
            return offer.Kind == ShopOffer.OfferKind.Item && offer.Item && catalog
                ? catalog.ColorOf(offer.Item.Rarity)
                : k_ServiceColor;
        }

        void Build()
        {
            Clear();
            m_Canvas = ItemUI.CreateCanvas(transform, "Loja", 100);
            Transform root = m_Canvas.transform;
            ItemUI.Fill("Escurecer", root, 0f, k_Overlay);

            var center = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI title = ItemUI.Text("Titulo", root, center, new Vector2(0f, 420f), new Vector2(1400f, 60f),
                44f, TextAlignmentOptions.Center, new Color(0.85f, 0.95f, 1f));
            title.fontStyle = FontStyles.Bold;
            title.text = m_Terminal ? m_Terminal.Title : "Loja";

            m_Wallet = ItemUI.Text("Carteira", root, center, new Vector2(0f, 372f), new Vector2(1400f, 40f), 28f,
                TextAlignmentOptions.Center, k_Cronita);
            m_Message = ItemUI.Text("Mensagem", root, center, new Vector2(0f, -420f), new Vector2(1400f, 40f), 24f,
                TextAlignmentOptions.Center, Color.white);

            List<ShopOffer> offers = m_Terminal ? m_Terminal.Offers : new List<ShopOffer>();
            int rows = Mathf.CeilToInt(offers.Count / (float)k_PerRow);
            for (int i = 0; i < offers.Count; i++)
            {
                int row = i / k_PerRow;
                int inRow = Mathf.Min(k_PerRow, offers.Count - row * k_PerRow);
                float x = (i % k_PerRow - (inRow - 1) * 0.5f) * k_CardSpacingX;
                float y = ((rows - 1) * 0.5f - row) * k_CardSpacingY - 10f;
                m_Cards.Add(BuildCard(root, offers[i], i, new Vector2(x, y)));
            }

            m_Close = ItemUI.Panel("Sair", root, center, new Vector2(620f, -420f), new Vector2(220f, 54f),
                new Color(0.25f, 0.28f, 0.32f, 0.95f));
            TextMeshProUGUI close = ItemUI.Text("Texto", m_Close.transform, center, Vector2.zero, new Vector2(220f, 54f),
                24f, TextAlignmentOptions.Center, Color.white);
            close.text = $"Sair  [{GameSettings.KeyName(GameSettings.ShopKey)}]";
            Refresh();
        }

        Card BuildCard(Transform parent, ShopOffer offer, int index, Vector2 position)
        {
            Color color = ColorOf(offer);
            Image border = ItemUI.Panel("Oferta" + (index + 1), parent, new Vector2(0.5f, 0.5f), position,
                new Vector2(k_CardWidth, k_CardHeight), color);
            Transform card = border.transform;
            ItemUI.Fill("Fundo", card, 4f, k_CardBack);

            var top = new Vector2(0.5f, 1f);
            ItemUI.Text("Tecla", card, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(50f, 34f), 22f,
                TextAlignmentOptions.TopLeft, new Color(0.6f, 0.66f, 0.72f)).text = $"[{index + 1}]";

            if (offer.Kind == ShopOffer.OfferKind.Item && offer.Item)
                ItemUI.Icon(card, offer.Item, PlayerStats.Instance.Catalog, top, new Vector2(0f, -22f), 76f);
            else
                ServiceIcon(card, offer, color);

            TextMeshProUGUI name = ItemUI.Text("Nome", card, top, new Vector2(0f, -104f), new Vector2(280f, 36f), 24f,
                TextAlignmentOptions.Center, color);
            name.fontStyle = FontStyles.Bold;
            name.text = NameOf(offer);

            TextMeshProUGUI description = ItemUI.Text("Descricao", card, top, new Vector2(0f, -142f),
                new Vector2(272f, 130f), 18f, TextAlignmentOptions.Top, new Color(0.9f, 0.93f, 0.96f));
            description.enableAutoSizing = true;
            description.fontSizeMin = 12f;
            description.fontSizeMax = 18f;
            description.text = Describe(offer);

            TextMeshProUGUI price = ItemUI.Text("Preco", card, new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                new Vector2(280f, 34f), 26f, TextAlignmentOptions.Bottom, k_Cronita);
            price.fontStyle = FontStyles.Bold;

            Image sold = ItemUI.Panel("Vendido", card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 50f),
                new Color(0.1f, 0.1f, 0.12f, 0.92f));
            ItemUI.Text("Texto", sold.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 50f), 26f,
                TextAlignmentOptions.Center, k_Sold).text = "VENDIDO";

            return new Card
            {
                Offer = offer, Root = border.rectTransform, Border = border, Price = price, Description = description,
                SoldTag = sold.gameObject,
            };
        }

        // Ícone dos serviços: quadrado com uma letra, como os ícones provisórios dos itens
        static void ServiceIcon(Transform card, ShopOffer offer, Color color)
        {
            Image frame = ItemUI.Panel("Icone", card, new Vector2(0.5f, 1f), new Vector2(0f, -22f), Vector2.one * 76f,
                color);
            ItemUI.Fill("Fundo", frame.transform, 4f, Color.Lerp(color, Color.black, 0.65f));
            string letter = offer.Kind switch
            {
                ShopOffer.OfferKind.Weapon => offer.Weapon ? offer.Weapon.Letter : "A",
                ShopOffer.OfferKind.Heal => "+",
                ShopOffer.OfferKind.Reroll => "R",
                ShopOffer.OfferKind.Damage => "D",
                _ => "C",
            };
            TextMeshProUGUI text = ItemUI.Text("Letra", frame.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.one * 76f, 46f, TextAlignmentOptions.Center, color);
            text.fontStyle = FontStyles.Bold;
            text.text = letter;
        }

        void Refresh()
        {
            PlayerWallet wallet = PlayerWallet.Instance;
            int cronitas = wallet ? wallet.Cronitas : 0;
            m_Wallet.text = $"Cronitas: {cronitas}   " +
                            "<size=70%><color=#9AA4AE>(preços sobem com o multiplicador de tempo)</color></size>";
            m_Message.gameObject.SetActive(Time.unscaledTime < m_MessageUntil);

            foreach (Card card in m_Cards)
            {
                bool over = ItemUI.MouseOver(card.Root);
                int price = PriceOf(card.Offer);
                card.SoldTag.SetActive(card.Offer.Sold);
                card.Price.text = card.Offer.Sold ? "" : $"{price} Cronitas";
                card.Price.color = cronitas >= price ? k_Cronita : k_TooExpensive;
                Color color = card.Offer.Sold ? k_Sold : ColorOf(card.Offer);
                card.Border.color = over && !card.Offer.Sold ? Color.Lerp(color, Color.white, 0.35f) : color;
                card.Root.localScale = Vector3.one * (over && !card.Offer.Sold ? 1.04f : 1f);
            }
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
