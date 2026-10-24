using System;

namespace Icaro
{
    // Uma oferta de uma loja (ShopTerminal). Itens, armas e a cura vendem uma vez por loja; a ficha de rerrolagem e
    // as melhorias da arma podem ser compradas de novo (as melhorias ficam mais caras a cada compra).
    [Serializable]
    public class ShopOffer
    {
        public enum OfferKind
        {
            Item,
            Heal,
            Reroll,
            Damage,
            FireRate,
            Weapon,
        }

        public OfferKind Kind;
        public ItemDefinition Item;
        public WeaponDefinition Weapon;
        public bool Sold;

        public bool Repeatable => Kind == OfferKind.Reroll || Kind == OfferKind.Damage || Kind == OfferKind.FireRate;
    }
}
