using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Itens coletados na run, numa grade minimalista de ícones no canto superior esquerdo do visor (HelmetVisor):
    // cada célula é um vidro escuro com o ícone em pixel art, uma linha fina na cor da raridade e a quantidade quando
    // passa de 1; a grade quebra a cada PerRow ícones. Um item novo (ou mais uma pilha) dá um pulo na célula. Com o
    // cursor livre (na escolha de itens ou no menu de pausa), passar o mouse num ícone mostra o nome e a descrição.
    public class ItemHUD : MonoBehaviour
    {
        public ItemCatalog Catalog;

        public float IconSize = 40f;
        public float Spacing = 6f;
        public int PerRow = 8;

        static readonly Color k_Cell = new Color(0.03f, 0.07f, 0.1f, 0.5f);
        static readonly Color k_TooltipBack = new Color(0.04f, 0.08f, 0.11f, 0.88f);

        class Slot
        {
            public ItemDefinition Item;
            public int Count;
            public RectTransform Rect;
            public float Pop;
        }

        RectTransform m_Group;
        RectTransform m_Grid;
        readonly List<Slot> m_Slots = new List<Slot>();
        readonly Dictionary<ItemDefinition, int> m_KnownCounts = new Dictionary<ItemDefinition, int>();
        RectTransform m_Tooltip;
        TextMeshProUGUI m_TooltipText;
        PlayerStats m_Stats;
        bool m_Initialized;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.TopLeft, out m_Group))
                return false;
            m_Grid = ItemUI.Rect("Itens", m_Group, new Vector2(0f, 1f), new Vector2(44f, -40f), new Vector2(600f, 300f));
            BuildTooltip();
            return true;
        }

        void OnDestroy()
        {
            if (m_Stats)
                m_Stats.OnItemsChanged -= Rebuild;
        }

        void Update()
        {
            if (!m_Grid && !Build())
                return;
            if (!m_Stats && PlayerStats.Instance)
            {
                m_Stats = PlayerStats.Instance;
                m_Stats.OnItemsChanged += Rebuild;
                Rebuild();
            }

            foreach (Slot slot in m_Slots)
            {
                if (slot.Pop <= 0f || !slot.Rect)
                    continue;
                slot.Pop = Mathf.MoveTowards(slot.Pop, 0f, Time.unscaledDeltaTime * 2.5f);
                slot.Rect.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(slot.Pop * Mathf.PI));
            }

            UpdateTooltip();
        }

        void Rebuild()
        {
            foreach (Slot slot in m_Slots)
            {
                if (slot.Rect)
                    Destroy(slot.Rect.gameObject);
            }
            m_Slots.Clear();
            if (!m_Stats || !m_Grid)
                return;

            int index = 0;
            foreach (PlayerStats.ItemStack stack in m_Stats.Items)
            {
                if (!stack.Item || stack.Count <= 0)
                    continue;

                int column = index % PerRow;
                int row = index / PerRow;
                var position = new Vector2(column * (IconSize + Spacing), -row * (IconSize + Spacing));
                RectTransform cell = Cell(stack.Item, stack.Count, position);

                // Item novo ou pilha a mais: a célula pula
                m_KnownCounts.TryGetValue(stack.Item, out int known);
                var slot = new Slot { Item = stack.Item, Count = stack.Count, Rect = cell };
                if (stack.Count > known && m_Initialized)
                    slot.Pop = 1f;
                m_KnownCounts[stack.Item] = stack.Count;
                m_Slots.Add(slot);
                index++;
            }

            // A primeira montagem (itens que já existiam) não pula
            m_Initialized = true;
            if (index == 0)
                m_KnownCounts.Clear();
        }

        RectTransform Cell(ItemDefinition item, int count, Vector2 position)
        {
            Color rarity = Catalog ? Catalog.ColorOf(item.Rarity) : Color.white;
            Image cell = ItemUI.Panel("Item", m_Grid, new Vector2(0f, 1f), position, Vector2.one * IconSize, k_Cell);
            RectTransform rect = cell.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position + new Vector2(IconSize, -IconSize) * 0.5f;

            Sprite sprite = ItemArt.IconFor(item);
            if (sprite)
            {
                Image icon = ItemUI.Fill("Icone", rect, 3f, Color.white);
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }
            else
            {
                TextMeshProUGUI letter = VisorUI.Label("Letra", rect, new Vector2(0.5f, 0.5f), Vector2.zero,
                    Vector2.one * IconSize, IconSize * 0.55f, TextAlignmentOptions.Center, rarity);
                letter.text = item.IconLetter;
            }

            VisorUI.Line("Raridade", rect, Vector2.zero, Vector2.right, 2f, rarity);
            if (count > 1)
            {
                TextMeshProUGUI amount = VisorUI.Label("Quantidade", rect, new Vector2(1f, 0f), new Vector2(-2f, 3f),
                    new Vector2(IconSize, 16f), 14f, TextAlignmentOptions.BottomRight, Color.white);
                amount.outlineWidth = 0.3f;
                amount.outlineColor = Color.black;
                amount.text = "x" + count;
            }

            return rect;
        }

        // ---------- tooltip ----------

        void BuildTooltip()
        {
            Image back = ItemUI.Panel("Descricao", m_Group, new Vector2(0f, 1f), Vector2.zero, new Vector2(380f, 170f),
                k_TooltipBack);
            m_Tooltip = back.rectTransform;
            VisorUI.Line("Borda", m_Tooltip, Vector2.zero, Vector2.right, 1.5f, VisorUI.Edge);
            m_TooltipText = ItemUI.Text("Texto", m_Tooltip, new Vector2(0f, 1f), new Vector2(14f, -10f),
                new Vector2(352f, 150f), 19f, TextAlignmentOptions.TopLeft, Color.white);
            if (ItemFonts.Visor)
                m_TooltipText.font = ItemFonts.Visor;
            m_Tooltip.gameObject.SetActive(false);
        }

        void UpdateTooltip()
        {
            Slot hovered = null;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                foreach (Slot slot in m_Slots)
                {
                    if (HelmetVisor.MouseOver(slot.Rect))
                    {
                        hovered = slot;
                        break;
                    }
                }
            }

            m_Tooltip.gameObject.SetActive(hovered != null);
            if (hovered == null)
                return;

            ItemDefinition item = hovered.Item;
            Color rarity = Catalog ? Catalog.ColorOf(item.Rarity) : Color.white;
            string rarityName = Catalog ? Catalog.RarityName(item.Rarity) : "";
            m_TooltipText.text = $"<b><color=#{ItemUI.Hex(rarity)}>{item.DisplayName}</color></b>" +
                                 $"  <size=80%><color=#9AA4AE>{rarityName}</color></size>\n" +
                                 ItemUI.Describe(item, m_Stats ? m_Stats.CountOf(item) : 0);
            m_TooltipText.ForceMeshUpdate();
            m_Tooltip.sizeDelta = new Vector2(380f, m_TooltipText.preferredHeight + 24f);

            // Logo abaixo da célula
            Vector2 cell = hovered.Rect.anchoredPosition + m_Grid.anchoredPosition;
            m_Tooltip.anchoredPosition = cell + new Vector2(-IconSize * 0.5f, -(IconSize * 0.5f + 10f));
        }
    }
}
