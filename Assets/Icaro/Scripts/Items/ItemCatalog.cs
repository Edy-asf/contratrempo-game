using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Todos os itens que podem aparecer nas cartas, os pesos e as cores das raridades e os materiais dos efeitos
    // visuais. Ligado ao CombatBalance (campo Items).
    [CreateAssetMenu(menuName = "Ícaro/Catálogo de itens", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        public ItemDefinition[] Items;

        [Header("Raridades (peso de sorteio)")]
        public float CommonWeight = 70f;
        public float UncommonWeight = 25f;
        public float RareWeight = 5f;

        public Color CommonColor = new Color(0.92f, 0.92f, 0.92f);
        public Color UncommonColor = new Color(0.35f, 0.9f, 0.35f);
        public Color RareColor = new Color(0.95f, 0.25f, 0.22f);

        [Tooltip("Cartas oferecidas ao limpar cada sala (sempre itens diferentes entre si)")]
        [Min(1)] public int Choices = 4;

        [Tooltip("Cartas que o jogador escolhe entre as oferecidas")]
        [Min(1)] public int Picks = 2;

        [Tooltip("Multiplica a chance de Raro nas cartas da sala do chefe (2 = o dobro; Comum e Incomum dividem o resto " +
                 "na mesma proporção)")]
        [Min(1f)] public float BossRareMultiplier = 2f;

        [Header("Efeitos")]
        [Tooltip("Material aditivo para raios, rastros e explosões (cor por vértice ou _BaseColor)")]
        public Material EffectMaterial;

        [Tooltip("Distância máxima de um salto do raio da Bobina Ressonante (m)")]
        public float LightningJumpRange = 15f;

        [Tooltip("Segundos sem levar dano para o Escudo de Sincronia começar a recarregar")]
        public float ShieldRechargeDelay = 3f;

        [Tooltip("Segundos para o escudo recarregar do zero ao máximo")]
        public float ShieldRechargeTime = 1f;

        [Tooltip("Atraso do tiro repetido pelo Eco Temporal (s)")]
        public float EchoDelay = 0.08f;

        public float MissileSpeed = 28f;
        public float MissileTurnRate = 540f;

        [Tooltip("Raio em volta do jogador em que o dash acerta inimigos (Lâmina de Fase)")]
        public float DashHitRadius = 1.6f;

        [Tooltip("Segundos até a barreira do dash sumir (Égide de Impulso)")]
        public float DashBarrierDuration = 3f;

        [Tooltip("Teto do quanto o multiplicador de tempo pode ficar mais lento (atributo TimeSlow; sem item hoje)")]
        [Range(0f, 0.95f)] public float MaxTimeSlow = 0.75f;

        [Tooltip("Teto do dano extra do Coração Aeônida (2,5 = +250%)")]
        public float HeartDamageCap = 2.5f;

        [Header("Munição Atordoante")]
        [Tooltip("Segundos de lentidão")]
        public float SlowDuration = 2f;

        [Tooltip("Lentidão máxima com todas as pilhas (0,7 = 70% mais lento)")]
        [Range(0f, 0.95f)] public float MaxSlowAmount = 0.7f;

        [Tooltip("Fração da lentidão que vale nos chefes")]
        [Range(0f, 1f)] public float BossSlowStrength = 0.4f;

        public Color SlowColor = new Color(0.75f, 0.45f, 1f);

        [Header("Disparo Perfurante")]
        [Tooltip("Dano do tiro depois de atravessar (fração do dano original)")]
        [Range(0f, 1f)] public float PierceDamage = 0.6f;

        [Header("Cartucho Incendiário")]
        [Tooltip("Segundos de cada queimadura")]
        public float BurnDuration = 3f;

        [Tooltip("Queimaduras ao mesmo tempo no mesmo inimigo (uma nova além disso substitui a mais antiga)")]
        [Min(1)] public int MaxBurnStacks = 3;

        [Tooltip("Segundos entre os danos da queimadura")]
        public float BurnTickInterval = 0.5f;

        public Color BurnColor = new Color(1f, 0.45f, 0.1f);

        [Header("Injetor de Adrenalina")]
        [Tooltip("Bônus de velocidade e de cadência durante a adrenalina (0,15 = 15%)")]
        public float AdrenalineBonus = 0.15f;

        [Tooltip("Segundos entre ativações, contados da ativação (não muda com as pilhas)")]
        public float AdrenalineCooldown = 10f;

        [Tooltip("Distância do jogador até o acerto em que o Cano Curto dá o bônus de dano (m)")]
        public float CloseRangeDistance = 6f;

        [Tooltip("Piso do tempo de recarga da munição com o Resfriador Avançado (0,25 = no mínimo 25% do original)")]
        [Range(0.05f, 1f)] public float MinReloadTimeFactor = 0.25f;

        public Color ColorOf(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Incomum => UncommonColor,
            ItemRarity.Raro => RareColor,
            _ => CommonColor,
        };

        public string RarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Incomum => "Incomum",
            ItemRarity.Raro => "Raro",
            _ => "Comum",
        };

        // Sorteia 'count' itens diferentes: primeiro a raridade (pelos pesos), depois um item dela. Se a raridade
        // sorteada não tiver item disponível, tenta as outras. rareMultiplier multiplica a chance de Raro (sala do
        // chefe: 5% vira 10%).
        public List<ItemDefinition> Roll(int count, float rareMultiplier = 1f)
        {
            var result = new List<ItemDefinition>();
            for (int i = 0; i < count; i++)
            {
                ItemDefinition item = RollOne(result, rareMultiplier);
                if (!item)
                    break;
                result.Add(item);
            }

            return result;
        }

        ItemDefinition RollOne(List<ItemDefinition> exclude, float rareMultiplier)
        {
            float total = CommonWeight + UncommonWeight + RareWeight;
            float rareChance = total > 0f ? Mathf.Clamp01(RareWeight / total * rareMultiplier) : 0f;
            float others = CommonWeight + UncommonWeight;
            float commonChance = others > 0f ? (1f - rareChance) * CommonWeight / others : 0f;

            float roll = Random.value;
            ItemRarity rarity = roll < commonChance ? ItemRarity.Comum
                : roll < 1f - rareChance ? ItemRarity.Incomum
                : ItemRarity.Raro;

            ItemDefinition item = RandomOf(rarity, exclude);
            for (int r = 0; !item && r < 3; r++)
                item = RandomOf((ItemRarity)r, exclude);
            return item;
        }

        ItemDefinition RandomOf(ItemRarity rarity, List<ItemDefinition> exclude)
        {
            var pool = new List<ItemDefinition>();
            if (Items != null)
            {
                foreach (ItemDefinition item in Items)
                {
                    if (item && item.Rarity == rarity && !exclude.Contains(item))
                        pool.Add(item);
                }
            }

            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }
    }
}
