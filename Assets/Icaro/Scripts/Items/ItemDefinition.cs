using UnityEngine;

namespace Icaro
{
    // Ficha de um item: nome, descrição, regra de acúmulo, raridade, ícone e efeitos. O ícone é opcional: sem ele, a
    // interface desenha um quadrado na cor da raridade com IconLetter.
    [CreateAssetMenu(menuName = "Ícaro/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string DisplayName = "Item";

        [TextArea(2, 4)] public string Description;

        [Tooltip("Como o efeito cresce com mais pilhas (mostrado abaixo da descrição)")]
        [TextArea(1, 3)] public string StackRule;

        public ItemRarity Rarity;

        public Sprite Icon;

        [Tooltip("Letra do ícone provisório (quando não há Icon)")]
        public string IconLetter = "?";

        public ItemEffect[] Effects;

        [Tooltip("Item consumido ao ser usado (ex.: Segunda Ignição)")]
        public bool Consumable;

        [Tooltip("Selos ao lado da arte da carta (categorias e elementos)")]
        public ItemTag Tags;

        [Tooltip("Frase de efeito no pé da carta (só nas cartas elementais: Fogo, Elétrico e Tempo)")]
        [TextArea(1, 3)] public string Flavor;
    }
}
