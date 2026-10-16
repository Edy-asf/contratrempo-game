using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Itens coletados no HUD: ícone e quantidade, em linhas no canto superior esquerdo. Com o cursor livre (na
    // escolha de itens ou no menu de pausa), passar o mouse num ícone mostra o nome e a descrição. Também mostra a
    // barra do Escudo de Sincronia, quando o jogador tem escudo.
    public class ItemHUD : MonoBehaviour
    {
        public ItemCatalog Catalog;

        public float IconSize = 52f;
        public float Spacing = 8f;
        public int PerRow = 10;

        static readonly Color k_TooltipBack = new Color(0.05f, 0.07f, 0.09f, 0.95f);
        static readonly Color k_ShieldColor = new Color(0.35f, 0.8f, 1f, 0.9f);

        class Slot
        {
            public ItemDefinition Item;
            public RectTransform Rect;
        }

        Canvas m_Canvas;
        RectTransform m_Row;
        readonly List<Slot> m_Slots = new List<Slot>();
        RectTransform m_Tooltip;
        TextMeshProUGUI m_TooltipText;
        Image m_ShieldBack;
        Image m_ShieldFill;
        TextMeshProUGUI m_ShieldText;
        PlayerStats m_Stats;
        PlayerDefenses m_Defenses;

        void Start()
        {
            m_Canvas = ItemUI.CreateCanvas(transform, "ItensHUD", 120);
            Transform root = m_Canvas.transform;
            m_Row = ItemUI.Rect("Itens", root, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(800f, 200f));
            BuildTooltip(root);
            BuildShieldBar(root);
        }

        void OnDestroy()
        {
            if (m_Stats)
                m_Stats.OnItemsChanged -= Rebuild;
        }

        void Update()
        {
            if (!m_Stats && PlayerStats.Instance)
            {
                m_Stats = PlayerStats.Instance;
                m_Defenses = m_Stats.GetComponent<PlayerDefenses>();
                m_Stats.OnItemsChanged += Rebuild;
                Rebuild();
            }

            UpdateTooltip();
            UpdateShield();
        }

        void Rebuild()
        {
            foreach (Slot slot in m_Slots)
            {
                if (slot.Rect)
                    Destroy(slot.Rect.gameObject);
            }
            m_Slots.Clear();
            if (!m_Stats || !m_Row)
                return;

            int index = 0;
            foreach (PlayerStats.ItemStack stack in m_Stats.Items)
            {
                if (!stack.Item || stack.Count <= 0)
                    continue;

                int column = index % PerRow;
                int row = index / PerRow;
                var position = new Vector2(column * (IconSize + Spacing), -row * (IconSize + Spacing));
                RectTransform icon = ItemUI.Icon(m_Row, stack.Item, Catalog, new Vector2(0f, 1f), position, IconSize);

                if (stack.Count > 1)
                {
                    TextMeshProUGUI count = ItemUI.Text("Quantidade", icon, new Vector2(1f, 0f), new Vector2(-3f, 2f),
                        new Vector2(IconSize, 24f), 20f, TextAlignmentOptions.BottomRight, Color.white);
                    count.fontStyle = FontStyles.Bold;
                    count.outlineWidth = 0.25f;
                    count.outlineColor = Color.black;
                    count.text = "x" + stack.Count;
                }

                m_Slots.Add(new Slot { Item = stack.Item, Rect = icon });
                index++;
            }
        }

        // ---------- tooltip ----------

        void BuildTooltip(Transform root)
        {
            Image back = ItemUI.Panel("Descricao", root, new Vector2(0f, 1f), Vector2.zero, new Vector2(380f, 170f),
                k_TooltipBack);
            m_Tooltip = back.rectTransform;
            m_TooltipText = ItemUI.Text("Texto", m_Tooltip, new Vector2(0f, 1f), new Vector2(14f, -10f),
                new Vector2(352f, 150f), 21f, TextAlignmentOptions.TopLeft, Color.white);
            m_Tooltip.gameObject.SetActive(false);
        }

        void UpdateTooltip()
        {
            if (!m_Tooltip)
                return;

            Slot hovered = null;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                foreach (Slot slot in m_Slots)
                {
                    if (ItemUI.MouseOver(slot.Rect))
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

            // Logo abaixo do ícone
            Vector2 iconPosition = hovered.Rect.anchoredPosition + m_Row.anchoredPosition;
            m_Tooltip.anchoredPosition = iconPosition + new Vector2(0f, -(IconSize + 10f));
        }

        // ---------- escudo ----------

        void BuildShieldBar(Transform root)
        {
            var anchor = new Vector2(0.5f, 0f);
            m_ShieldBack = ItemUI.Panel("Escudo", root, anchor, new Vector2(0f, 130f), new Vector2(320f, 14f),
                new Color(0f, 0f, 0f, 0.55f));
            m_ShieldFill = ItemUI.Fill("Carga", m_ShieldBack.transform, 2f, k_ShieldColor);
            m_ShieldFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_ShieldText = ItemUI.Text("Valor", root, anchor, new Vector2(0f, 146f), new Vector2(320f, 26f), 18f,
                TextAlignmentOptions.Bottom, k_ShieldColor);
            m_ShieldBack.gameObject.SetActive(false);
            m_ShieldText.gameObject.SetActive(false);
        }

        void UpdateShield()
        {
            bool show = m_Defenses && m_Defenses.ShieldMax > 0f;
            if (m_ShieldBack.gameObject.activeSelf != show)
            {
                m_ShieldBack.gameObject.SetActive(show);
                m_ShieldText.gameObject.SetActive(show);
            }
            if (!show)
                return;

            float ratio = Mathf.Clamp01(m_Defenses.Shield / m_Defenses.ShieldMax);
            RectTransform fill = m_ShieldFill.rectTransform;
            fill.anchorMax = new Vector2(ratio, 1f);
            m_ShieldText.text = $"Escudo {Mathf.CeilToInt(m_Defenses.Shield)}/{Mathf.CeilToInt(m_Defenses.ShieldMax)}";
        }
    }
}
