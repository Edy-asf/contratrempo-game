using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Cronitas no HUD, no canto superior direito (abaixo do multiplicador de tempo): um cristal ciano e o total, que
    // pulsa ao recolher. Mostra também as fichas de rerrolagem, quando houver. O ShopDirector cria este HUD.
    public class CronitaHUD : MonoBehaviour
    {
        static readonly Color k_Color = new Color(0.55f, 0.9f, 1f);

        TextMeshProUGUI m_Label;
        RectTransform m_Root;
        PlayerWallet m_Wallet;
        float m_Pulse;

        void Start()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "Cronitas", 46);
            m_Root = ItemUI.Rect("Cronitas", canvas.transform, new Vector2(1f, 1f), new Vector2(-40f, -92f),
                new Vector2(320f, 40f));
            Image crystal = ItemUI.Panel("Cristal", m_Root, new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
                Vector2.one * 18f, k_Color);
            crystal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m_Label = ItemUI.Text("Total", m_Root, new Vector2(1f, 0.5f), new Vector2(-36f, 0f),
                new Vector2(280f, 40f), 30f, TextAlignmentOptions.Right, k_Color);
            m_Label.fontStyle = FontStyles.Bold;
        }

        void OnDestroy()
        {
            if (m_Wallet)
                m_Wallet.OnCollected -= OnCollected;
        }

        void OnCollected(int amount) => m_Pulse = 1f;

        void Update()
        {
            if (!m_Wallet && PlayerWallet.Instance)
            {
                m_Wallet = PlayerWallet.Instance;
                m_Wallet.OnCollected += OnCollected;
            }
            if (!m_Wallet || !m_Label)
                return;

            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, Time.unscaledDeltaTime * 3f);
            m_Root.localScale = Vector3.one * (1f + 0.18f * m_Pulse);
            string tokens = m_Wallet.RerollTokens > 0
                ? $"  <size=70%><color=#9AA4AE>R x{m_Wallet.RerollTokens}</color></size>"
                : "";
            m_Label.text = m_Wallet.Cronitas + tokens;
        }
    }
}
