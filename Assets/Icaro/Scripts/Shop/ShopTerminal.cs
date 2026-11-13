using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Terminal de loja no mundo: pedestal com um holograma girando. Com o jogador perto (InteractRadius), o HUD mostra
    // "[G] Abrir loja" e a tecla abre a loja (ShopUI). As ofertas são sorteadas na primeira vez que a loja abre e
    // ficam até o fim: alguns itens (pela raridade, diferentes entre si), armas novas (que o jogador ainda não
    // carrega), uma habilidade, o tradutor, cura e ficha de rerrolagem.
    public class ShopTerminal : MonoBehaviour
    {
        public string Title = "Loja";

        [Tooltip("Parte que gira e flutua (holograma)")]
        public Transform Hologram;

        public List<ShopOffer> Offers = new List<ShopOffer>();

        [Tooltip("Desligado: sem aviso nem tecla; a loja só abre por OpenShop (o Méton abre pela conversa)")]
        public bool Interactive = true;

        bool m_Stocked;
        Vector3 m_HologramRest;

        void Start()
        {
            if (Hologram)
                m_HologramRest = Hologram.localPosition;
        }

        void Update()
        {
            if (Hologram)
            {
                Hologram.Rotate(Vector3.up, 60f * Time.unscaledDeltaTime, Space.World);
                Hologram.localPosition = m_HologramRest + Vector3.up * (Mathf.Sin(Time.unscaledTime * 2f) * 0.12f);
            }

            if (!Interactive)
                return;

            ShopSettings settings = ShopDirector.Settings;
            bool near = BossTarget.Exists && BossTarget.FlatDistance(transform.position) <= settings.InteractRadius &&
                        Mathf.Abs(BossTarget.Feet.y - transform.position.y) < 3f;
            ShopUI ui = ShopUI.Instance;
            if (!ui)
                return;

            ui.SetPrompt(this, near);
            Keyboard keyboard = Keyboard.current;
            bool canOpen = near && !ui.IsOpen && !(ItemDraft.Instance && ItemDraft.Instance.IsOpen) &&
                           Cursor.lockState == CursorLockMode.Locked;
            if (canOpen && keyboard != null && keyboard[GameSettings.ShopKey].wasPressedThisFrame)
            {
                Stock();
                ui.Open(this);
            }
        }

        // Abre a loja (sorteia as ofertas na primeira vez)
        public void OpenShop()
        {
            ShopUI ui = ShopUI.Instance;
            if (!ui || ui.IsOpen)
                return;
            Stock();
            ui.Open(this);
        }

        // Sorteia as ofertas na primeira abertura
        void Stock()
        {
            if (m_Stocked)
                return;
            m_Stocked = true;

            ItemCatalog catalog = PlayerStats.Instance ? PlayerStats.Instance.Catalog : null;
            if (catalog)
            {
                foreach (ItemDefinition item in catalog.Roll(ShopDirector.Settings.ItemOffers))
                    Offers.Add(new ShopOffer { Kind = ShopOffer.OfferKind.Item, Item = item });
            }

            WeaponCatalog weapons = ShopDirector.Settings.Weapons;
            PlayerArsenal arsenal = PlayerArsenal.Instance;
            if (weapons)
            {
                foreach (WeaponDefinition weapon in weapons.Roll(ShopDirector.Settings.WeaponOffers,
                             w => arsenal && arsenal.Owns(w)))
                    Offers.Add(new ShopOffer { Kind = ShopOffer.OfferKind.Weapon, Weapon = weapon });
            }

            // Uma habilidade ativa que o jogador ainda não tem (sorteada)
            PlayerAbilities abilities = PlayerAbilities.Instance;
            var missing = new List<AbilityType>();
            foreach (AbilityType type in new[] { AbilityType.TimeRewind, AbilityType.TimeSlow })
                if (!abilities || !abilities.Owns(type))
                    missing.Add(type);
            if (missing.Count > 0)
                Offers.Add(new ShopOffer
                {
                    Kind = ShopOffer.OfferKind.Ability, Ability = missing[UnityEngine.Random.Range(0, missing.Count)],
                });

            // O tradutor aeônida, enquanto o jogador não tem
            if (!PlayerTranslator.Owned)
                Offers.Add(new ShopOffer { Kind = ShopOffer.OfferKind.Translator });

            Offers.Add(new ShopOffer { Kind = ShopOffer.OfferKind.Heal });
            Offers.Add(new ShopOffer { Kind = ShopOffer.OfferKind.Reroll });
        }
    }
}
