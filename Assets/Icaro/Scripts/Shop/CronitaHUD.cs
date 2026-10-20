using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Cronitas no visor (HelmetVisor), no canto superior direito (abaixo do multiplicador de tempo): um cristal ciano e o total, que
    // pulsa ao recolher. Mostra também as fichas de rerrolagem, quando houver. O ShopDirector cria este HUD.
    public class CronitaHUD : MonoBehaviour
    {
        static readonly Color k_Color = new Color(0.55f, 0.9f, 1f);

        TextMeshProUGUI m_Label;
        int m_ShownCronitas = -1, m_ShownTokens = -1;
        RectTransform m_Root;
        PlayerWallet m_Wallet;
        float m_Pulse;

        // No canto superior direito do visor, abaixo do multiplicador
        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.TopRight, out RectTransform group))
                return false;
            m_Root = ItemUI.Rect("Cronitas", group, new Vector2(1f, 1f), new Vector2(-44f, -112f),
                new Vector2(320f, 34f));
            Image crystal = ItemUI.Panel("Cristal", m_Root, new Vector2(1f, 0.5f), new Vector2(-6f, 0f),
                Vector2.one * 14f, k_Color);
            crystal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m_Label = VisorUI.Label("Total", m_Root, new Vector2(1f, 0.5f), new Vector2(-30f, 0f),
                new Vector2(280f, 34f), 26f, TextAlignmentOptions.Right, k_Color);
            return true;
        }

        void OnDestroy()
        {
            if (m_Wallet)
                m_Wallet.OnCollected -= OnCollected;
        }

        void OnCollected(int amount) => m_Pulse = 1f;

        void Update()
        {
            if (!m_Root && !Build())
                return;
            if (!m_Wallet && PlayerWallet.Instance)
            {
                m_Wallet = PlayerWallet.Instance;
                m_Wallet.OnCollected += OnCollected;
            }
            if (!m_Wallet || !m_Label)
                return;

            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, Time.unscaledDeltaTime * 3f);
            VisorUI.SetScale(m_Root, Vector3.one * (1f + 0.18f * m_Pulse));

            // Texto só quando o total ou as fichas mudam
            if (m_Wallet.Cronitas == m_ShownCronitas && m_Wallet.RerollTokens == m_ShownTokens)
                return;
            m_ShownCronitas = m_Wallet.Cronitas;
            m_ShownTokens = m_Wallet.RerollTokens;
            string tokens = m_Wallet.RerollTokens > 0
                ? $"  <size=70%><color=#9AA4AE>R x{m_Wallet.RerollTokens}</color></size>"
                : "";
            m_Label.text = m_Wallet.Cronitas + tokens;
        }
    }
}
