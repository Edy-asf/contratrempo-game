using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Cargas do dash embaixo, no centro da tela: um bloco por carga (cheio = pronta; o próximo enche conforme
    // recarrega) e o nome da tecla. Mostra também a barreira do dash (Égide de Impulso) quando houver. O PlayerDash
    // cria este HUD se a cena não tiver um.
    public class DashHUD : MonoBehaviour
    {
        public PlayerDash Dash;

        public Color ReadyColor = new Color(0.45f, 0.95f, 1f, 0.95f);
        public Color RechargeColor = new Color(0.3f, 0.55f, 0.65f, 0.8f);
        public Color BarrierColor = new Color(1f, 0.85f, 0.4f);

        const float k_PipWidth = 46f;
        const float k_PipHeight = 9f;
        const float k_PipSpacing = 6f;

        RectTransform m_Root;
        TextMeshProUGUI m_Label;
        TextMeshProUGUI m_Barrier;
        readonly List<Image> m_Fills = new List<Image>();
        PlayerDefenses m_Defenses;

        void Start()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "Dash", 45);
            m_Root = ItemUI.Rect("Cargas", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 48f),
                new Vector2(400f, 50f));
            m_Label = ItemUI.Text("Tecla", m_Root, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 24f),
                17f, TextAlignmentOptions.Bottom, ReadyColor);
            m_Barrier = ItemUI.Text("Barreira", m_Root, new Vector2(0.5f, 0f), new Vector2(190f, 0f),
                new Vector2(160f, 24f), 18f, TextAlignmentOptions.BottomLeft, BarrierColor);
            if (Dash)
                m_Defenses = Dash.GetComponent<PlayerDefenses>();
        }

        void Update()
        {
            if (!Dash)
            {
                m_Root.gameObject.SetActive(false);
                return;
            }

            EnsurePips(Dash.MaxCharges);
            for (int i = 0; i < m_Fills.Count; i++)
            {
                float fill = i < Dash.Charges ? 1f : i == Dash.Charges ? Dash.RechargeProgress : 0f;
                m_Fills[i].rectTransform.anchorMax = new Vector2(fill, 1f);
                m_Fills[i].color = i < Dash.Charges ? ReadyColor : RechargeColor;
            }

            m_Label.text = $"DASH [{GameSettings.KeyName(GameSettings.DashKey)}]";
            m_Label.color = Dash.Charges > 0 ? ReadyColor : RechargeColor;

            float barrier = m_Defenses ? m_Defenses.Barrier : 0f;
            m_Barrier.gameObject.SetActive(barrier > 0.5f);
            if (barrier > 0.5f)
                m_Barrier.text = $"Barreira {Mathf.CeilToInt(barrier)}";
        }

        void EnsurePips(int count)
        {
            if (m_Fills.Count == count)
                return;

            foreach (Image fill in m_Fills)
                Destroy(fill.transform.parent.gameObject);
            m_Fills.Clear();

            float total = count * k_PipWidth + (count - 1) * k_PipSpacing;
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + k_PipWidth * 0.5f + i * (k_PipWidth + k_PipSpacing);
                Image back = ItemUI.Panel("Carga" + (i + 1), m_Root, new Vector2(0.5f, 0f), new Vector2(x, 0f),
                    new Vector2(k_PipWidth, k_PipHeight), new Color(0f, 0f, 0f, 0.55f));
                Image fill = ItemUI.Fill("Nivel", back.transform, 1.5f, ReadyColor);
                m_Fills.Add(fill);
            }
        }
    }
}
