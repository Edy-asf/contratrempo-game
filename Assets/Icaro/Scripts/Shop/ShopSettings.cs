using UnityEngine;

namespace Icaro
{
    // Cronitas (moeda da run) e lojas: quanto os inimigos soltam, como os cristais são recolhidos, o que cada loja
    // vende e por quanto. Os preços sobem com o multiplicador de tempo (Inflation por +1,0x): comprar cedo é mais
    // barato. Ligada ao CombatBalance.Shop.
    [CreateAssetMenu(menuName = "Ícaro/Loja", fileName = "ShopSettings")]
    public class ShopSettings : ScriptableObject
    {
        [Header("Prefabs (gerados por Ícaro > Gerar prefabs)")]
        public CronitaPickup CrystalPrefab;
        public ShopTerminal TerminalPrefab;

        [Tooltip("Armas que as lojas vendem (gerado por Ícaro > Gerar armas)")]
        public WeaponCatalog Weapons;

        [Header("Cronitas soltas")]
        [Tooltip("Multiplica as Cronitas de um elite")]
        public float EliteMultiplier = 3f;

        [Tooltip("Cronitas que um chefe solta")]
        [Min(0)] public int BossCronitas = 80;

        [Tooltip("Cronitas a mais por sala seguinte (0,05 = +5% por sala)")]
        public float BonusPerRoom = 0.05f;

        [Tooltip("Cristais por inimigo no máximo (o valor é dividido entre eles)")]
        [Min(1)] public int MaxCrystalsPerDrop = 5;

        [Tooltip("Distância em que os cristais começam a voar até o jogador")]
        public float MagnetRadius = 7f;

        public float MagnetSpeed = 22f;

        [Tooltip("Segundos até um cristal não recolhido sumir")]
        public float CrystalLifetime = 45f;

        [Header("Preços (a 1,0x)")]
        public int CommonItemPrice = 30;
        public int UncommonItemPrice = 70;
        public int RareItemPrice = 160;

        public int HealPrice = 25;
        [Range(0f, 1f)] public float HealFraction = 0.5f;

        [Tooltip("Ficha para rerrolar as cartas de uma escolha de itens")]
        public int RerollPrice = 20;

        [Tooltip("Calibrar a arma: dano extra por compra; o preço sobe a cada compra")]
        public int DamageUpgradePrice = 45;
        public int DamageUpgradePriceStep = 25;
        public float DamageUpgradeAmount = 0.1f;

        [Tooltip("Afinar o gatilho: cadência extra por compra; o preço sobe a cada compra")]
        public int FireRateUpgradePrice = 45;
        public int FireRateUpgradePriceStep = 25;
        public float FireRateUpgradeAmount = 0.08f;

        [Tooltip("Aumento de preço por +1,0x de multiplicador de tempo (0,15 = +75% a 6,0x)")]
        public float Inflation = 0.15f;

        [Header("Loja")]
        [Tooltip("Itens à venda em cada loja (sorteados pela raridade, diferentes entre si)")]
        [Min(0)] public int ItemOffers = 3;

        [Tooltip("Armas à venda em cada loja (sorteadas do catálogo, sem as que o jogador já carrega)")]
        [Min(0)] public int WeaponOffers = 2;

        [Tooltip("Distância do terminal em que dá para abrir a loja")]
        public float InteractRadius = 3.5f;

        [Tooltip("Depois de um chefe, distância do centro da arena até a loja e até o Metrônomo (um de cada lado)")]
        public float OffsetFromMetronome = 5f;

        // Preço com a inflação do multiplicador de tempo agora
        public int Inflate(int basePrice) =>
            Mathf.CeilToInt(basePrice * (1f + Inflation * Mathf.Max(0f, TimeManager.CurrentMultiplier - 1f)));

        public int ItemPrice(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Incomum => UncommonItemPrice,
            ItemRarity.Raro => RareItemPrice,
            _ => CommonItemPrice,
        };
    }
}
