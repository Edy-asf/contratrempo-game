using System;

namespace Icaro
{
    // Uma oferta de uma loja (ShopTerminal). Itens, armas, habilidades, o tradutor e a cura vendem uma vez por loja; a
    // ficha de rerrolagem pode ser comprada de novo.
    [Serializable]
    public class ShopOffer
    {
        public enum OfferKind
        {
            Item,
            Heal,
            Reroll,
            Weapon,
            Ability,
            Translator,
        }

        public OfferKind Kind;
        public ItemDefinition Item;
        public WeaponDefinition Weapon;
        public AbilityType Ability;
        public bool Sold;

        public bool Repeatable => Kind == OfferKind.Reroll;
    }
}
