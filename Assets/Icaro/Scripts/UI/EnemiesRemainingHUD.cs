using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Enquanto uma sala está em combate (RoomManager.Active), mostra no topo da tela "Inimigos restantes" (os que
    // faltam surgir somados aos vivos) com uma barra que esvazia até a sala ser limpa. Na sala do chefe, mostra
    // também o chefe em destaque: nome e barra de vida. Cria a própria interface; se não houver um na cena, o
    // RoomManager cria um ao ativar a primeira sala (EnsureExists).
    public class EnemiesRemainingHUD : MonoBehaviour
    {
        public float FontSize = 30f;

        public Color TextColor = new Color(0.85f, 0.95f, 1f);
        public Color LastEnemiesColor = new Color(1f, 0.75f, 0.3f);
        public Color BarColor = new Color(0.4f, 0.85f, 1f, 0.9f);
        public Color BossColor = new Color(1f, 0.3f, 0.25f, 0.95f);

        const float k_BarWidth = 420f;

        GameObject m_Root;
        TextMeshProUGUI m_Label;
        Image m_BarFill;
        GameObject m_BossRoot;
        TextMeshProUGUI m_BossLabel;
        Image m_BossFill;
        int m_ShownRemaining = -1;
        int m_ShownTotal = -1;

        public static void EnsureExists()
        {
            if (!FindAnyObjectByType<EnemiesRemainingHUD>())
                new GameObject("InimigosRestantesHUD").AddComponent<EnemiesRemainingHUD>();
        }

        void Awake()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "InimigosRestantes", 50);
            m_Root = ItemUI.Rect("Painel", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(k_BarWidth, 90f)).gameObject;

            m_Label = ItemUI.Text("Texto", m_Root.transform, new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(k_BarWidth, 40f), FontSize, TextAlignmentOptions.Top, TextColor);
            m_Label.fontStyle = FontStyles.Bold;
            m_BarFill = Bar(m_Root.transform, new Vector2(0f, -44f), 10f, BarColor);

            m_BossRoot = ItemUI.Rect("Chefe", m_Root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -66f),
                new Vector2(k_BarWidth + 200f, 60f)).gameObject;
            m_BossLabel = ItemUI.Text("Nome", m_BossRoot.transform, new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(k_BarWidth + 200f, 34f), FontSize * 0.9f, TextAlignmentOptions.Top, BossColor);
            m_BossLabel.fontStyle = FontStyles.Bold;
            m_BossFill = Bar(m_BossRoot.transform, new Vector2(0f, -36f), 16f, BossColor, k_BarWidth + 200f);

            m_Root.SetActive(false);
        }

        static Image Bar(Transform parent, Vector2 position, float height, Color color, float width = k_BarWidth)
        {
            Image back = ItemUI.Panel("Barra", parent, new Vector2(0.5f, 1f), position, new Vector2(width, height),
                new Color(0f, 0f, 0f, 0.55f));
            Image fill = ItemUI.Fill("Carga", back.transform, 2f, color);
            return fill;
        }

        void Update()
        {
            RoomManager room = RoomManager.Active;
            bool show = room && room.TotalEnemies > 0;
            if (m_Root.activeSelf != show)
            {
                m_Root.SetActive(show);
                m_ShownRemaining = m_ShownTotal = -1;
            }
            if (!show)
                return;

            int remaining = room.RemainingEnemies;
            int total = room.TotalEnemies;
            if (remaining != m_ShownRemaining || total != m_ShownTotal)
            {
                m_ShownRemaining = remaining;
                m_ShownTotal = total;
                m_Label.text = $"Inimigos restantes: {remaining}";
                m_Label.color = remaining <= 2 ? LastEnemiesColor : TextColor;
                m_BarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(remaining / (float)total), 1f);
            }

            Health boss = room.Boss;
            m_BossRoot.SetActive(boss);
            if (boss)
            {
                m_BossLabel.text = $"CHEFE: {room.BossName.ToUpperInvariant()}";
                m_BossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(boss.GetRatio()), 1f);
            }
        }
    }
}
