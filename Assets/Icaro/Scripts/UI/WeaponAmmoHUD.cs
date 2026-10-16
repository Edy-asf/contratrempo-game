using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Munição da arma na mão, no canto inferior direito (acima do painel de armas do Microgame): nome da arma,
    // "balas / pente" (laranja com pouca munição, vermelho vazio) e, durante a recarga do IcaroWeapon, a barra de
    // progresso com "RECARREGANDO". Criado pelo PlayerStats.
    public class WeaponAmmoHUD : MonoBehaviour
    {
        [Tooltip("Distância do canto inferior direito da tela")]
        public Vector2 Offset = new Vector2(-50f, 170f);

        [Tooltip("Fração do pente abaixo da qual o número fica laranja")]
        [Range(0f, 1f)] public float LowAmmo = 0.25f;

        static readonly Color k_Normal = new Color(0.92f, 0.96f, 1f);
        static readonly Color k_Low = new Color(1f, 0.65f, 0.25f);
        static readonly Color k_Empty = new Color(1f, 0.3f, 0.3f);
        static readonly Color k_Reload = new Color(0.55f, 0.9f, 1f);

        PlayerWeaponsManager m_Weapons;
        RectTransform m_Panel;
        TextMeshProUGUI m_Name;
        TextMeshProUGUI m_Count;
        TextMeshProUGUI m_ReloadLabel;
        RectTransform m_ReloadBar;
        Image m_ReloadFill;

        void Start()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "MunicaoCanvas", 5);
            m_Panel = ItemUI.Rect("Municao", canvas.transform, new Vector2(1f, 0f), Offset, new Vector2(260f, 110f));

            m_Name = Label("Arma", new Vector2(0f, 74f), new Vector2(260f, 30f), 24f, new Color(0.7f, 0.78f, 0.86f));
            m_Count = Label("Contagem", new Vector2(0f, 22f), new Vector2(260f, 56f), 48f, k_Normal);
            m_ReloadLabel = Label("Recarregando", new Vector2(0f, 22f), new Vector2(260f, 56f), 28f, k_Reload);
            m_ReloadLabel.text = "RECARREGANDO";

            Image back = ItemUI.Panel("BarraRecarga", m_Panel, new Vector2(1f, 0f), new Vector2(0f, 8f),
                new Vector2(200f, 10f), new Color(0.05f, 0.08f, 0.1f, 0.85f));
            m_ReloadBar = back.rectTransform;
            m_ReloadFill = ItemUI.Fill("Progresso", back.transform, 2f, k_Reload);
            m_ReloadFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }

        TextMeshProUGUI Label(string name, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            TextMeshProUGUI text = ItemUI.Text(name, m_Panel, new Vector2(1f, 0f), position, size, fontSize,
                TextAlignmentOptions.BottomRight, color);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (ItemFonts.Bold)
                text.font = ItemFonts.Bold;
            return text;
        }

        void Update()
        {
            if (!m_Weapons && PlayerStats.Instance)
                m_Weapons = PlayerStats.Instance.GetComponent<PlayerWeaponsManager>();
            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            m_Panel.gameObject.SetActive(weapon);
            if (!weapon)
                return;

            var icaro = weapon.GetComponent<IcaroWeapon>();
            bool reloading = icaro && icaro.IsReloading;
            int ammo = weapon.GetCurrentAmmo();
            int max = Mathf.Max(1, weapon.MaxAmmo);

            m_Name.text = weapon.WeaponName.ToUpperInvariant();
            m_Count.gameObject.SetActive(!reloading);
            m_ReloadLabel.gameObject.SetActive(reloading);
            m_ReloadBar.gameObject.SetActive(reloading);
            if (reloading)
            {
                m_ReloadFill.rectTransform.anchorMax = new Vector2(icaro.ReloadProgress, 1f);
                return;
            }

            m_Count.text = $"{ammo}<size=55%><color=#8A96A4> / {max}</color></size>";
            m_Count.color = ammo <= 0 ? k_Empty : ammo < max * LowAmmo ? k_Low : k_Normal;
        }
    }
}
