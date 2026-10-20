using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Bloco unificado do canto inferior direito do visor, num painel holográfico translúcido (50%): nome da arma na
    // mão, "balas / pente" (laranja com pouca munição, vermelho vazio) com a barra do pente e, durante a recarga do
    // IcaroWeapon, "RECARREGANDO" com a barra de progresso; à esquerda, as cargas do dash (cheia = pronta; a próxima
    // enche conforme recarrega) com a tecla. O tiro dá um leve pulso no número. Criado pelo PlayerStats (substitui o
    // antigo DashHUD).
    public class WeaponAmmoHUD : MonoBehaviour
    {
        [Tooltip("Fração do pente abaixo da qual o número fica laranja")]
        [Range(0f, 1f)] public float LowAmmo = 0.25f;

        const float k_Width = 360f;
        const float k_Height = 118f;
        const float k_PipWidth = 34f;

        RectTransform m_Root;
        TextMeshProUGUI m_Name;
        TextMeshProUGUI m_Count;
        TextMeshProUGUI m_ReloadLabel;
        TextMeshProUGUI m_DashLabel;
        Image m_MagFill;
        RectTransform m_PipRow;
        readonly List<Image> m_Pips = new List<Image>();

        PlayerWeaponsManager m_Weapons;
        PlayerDash m_Dash;
        int m_LastAmmo = -1;
        float m_Pulse;
        WeaponController m_ShownWeapon;
        int m_ShownAmmo = -1, m_ShownMax = -1, m_ShownCharges = -1;
        bool m_ShownReloading;
        string m_ShownKey;

        bool Build()
        {
            if (!HelmetVisor.TryGroup(VisorAnchor.BottomRight, out RectTransform group))
                return false;
            var anchor = new Vector2(1f, 0f);
            m_Root = ItemUI.Rect("Arma", group, anchor, new Vector2(-48f, 44f), new Vector2(k_Width, k_Height));
            VisorUI.GlassPanel("Vidro", m_Root, anchor, Vector2.zero, m_Root.sizeDelta);

            m_Name = VisorUI.Label("Nome", m_Root, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(220f, 24f),
                17f, TextAlignmentOptions.TopRight, VisorUI.Dim, true);
            m_Count = VisorUI.Label("Contagem", m_Root, anchor, new Vector2(-14f, 20f), new Vector2(200f, 64f), 54f,
                TextAlignmentOptions.BottomRight, VisorUI.Text);
            m_ReloadLabel = VisorUI.Label("Recarregando", m_Root, anchor, new Vector2(-14f, 26f),
                new Vector2(220f, 40f), 24f, TextAlignmentOptions.BottomRight, VisorUI.Accent, true);
            m_ReloadLabel.text = "RECARREGANDO";

            // Barra do pente (e da recarga) na base do painel
            m_MagFill = VisorUI.Bar("Pente", m_Root, anchor, new Vector2(-14f, 12f), new Vector2(k_Width - 28f, 5f),
                new Color(0f, 0.02f, 0.04f, 0.5f), VisorUI.Text, out _);

            // Dash à esquerda
            m_DashLabel = VisorUI.Label("Dash", m_Root, Vector2.zero, new Vector2(14f, 52f), new Vector2(140f, 22f),
                15f, TextAlignmentOptions.BottomLeft, VisorUI.Accent, true);
            m_PipRow = ItemUI.Rect("Cargas", m_Root, Vector2.zero, new Vector2(14f, 32f), new Vector2(140f, 14f));
            return true;
        }

        void Update()
        {
            if (!m_Root && !Build())
                return;
            if (!m_Weapons && PlayerStats.Instance)
            {
                m_Weapons = PlayerStats.Instance.GetComponent<PlayerWeaponsManager>();
                m_Dash = PlayerStats.Instance.GetComponent<PlayerDash>();
            }

            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            m_Root.gameObject.SetActive(weapon);
            if (!weapon)
                return;
            UpdateWeapon(weapon);
            UpdateDash();
        }

        void UpdateWeapon(WeaponController weapon)
        {
            var icaro = weapon.GetComponent<IcaroWeapon>();
            bool reloading = icaro && icaro.IsReloading;
            int ammo = weapon.GetCurrentAmmo();
            int max = Mathf.Max(1, weapon.MaxAmmo);

            if (weapon != m_ShownWeapon)
            {
                m_ShownWeapon = weapon;
                m_Name.text = weapon.WeaponName.ToUpperInvariant();
                m_ShownAmmo = -1;
            }

            if (reloading != m_ShownReloading || m_Count.gameObject.activeSelf == reloading)
            {
                m_ShownReloading = reloading;
                m_Count.gameObject.SetActive(!reloading);
                m_ReloadLabel.gameObject.SetActive(reloading);
            }

            if (reloading)
            {
                VisorUI.SetFill(m_MagFill, icaro.ReloadProgress);
                m_MagFill.color = VisorUI.Accent;
                m_LastAmmo = -1;
                return;
            }

            // Pulso no número a cada tiro
            if (m_LastAmmo >= 0 && ammo < m_LastAmmo)
                m_Pulse = 1f;
            m_LastAmmo = ammo;
            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, Time.deltaTime * 8f);
            VisorUI.SetScale(m_Count.rectTransform, Vector3.one * (1f + 0.08f * m_Pulse));

            Color color = ammo <= 0 ? VisorUI.Danger : ammo < max * LowAmmo ? VisorUI.Warning : VisorUI.Text;
            if (ammo != m_ShownAmmo || max != m_ShownMax)
            {
                m_ShownAmmo = ammo;
                m_ShownMax = max;
                m_Count.text = $"{ammo}<size=45%><color=#{ItemUI.Hex(VisorUI.Dim)}> / {max}</color></size>";
            }

            m_Count.color = color;
            VisorUI.SetFill(m_MagFill, (float)ammo / max);
            m_MagFill.color = color;
        }

        void UpdateDash()
        {
            bool show = m_Dash;
            m_DashLabel.gameObject.SetActive(show);
            m_PipRow.gameObject.SetActive(show);
            if (!show)
                return;

            EnsurePips(m_Dash.MaxCharges);
            Color ready = VisorUI.Accent, recharge = new Color(0.3f, 0.55f, 0.65f, 0.8f);
            for (int i = 0; i < m_Pips.Count; i++)
            {
                float fill = i < m_Dash.Charges ? 1f : i == m_Dash.Charges ? m_Dash.RechargeProgress : 0f;
                VisorUI.SetFill(m_Pips[i], fill);
                m_Pips[i].color = i < m_Dash.Charges ? ready : recharge;
            }

            string key = GameSettings.KeyName(GameSettings.DashKey);
            if (key != m_ShownKey)
            {
                m_ShownKey = key;
                m_DashLabel.text = $"DASH [{key}]";
            }

            if (m_Dash.Charges != m_ShownCharges)
            {
                m_ShownCharges = m_Dash.Charges;
                m_DashLabel.color = m_Dash.Charges > 0 ? ready : recharge;
            }
        }

        void EnsurePips(int count)
        {
            if (m_Pips.Count == count)
                return;
            foreach (Image pip in m_Pips)
                Destroy(pip.transform.parent.gameObject);
            m_Pips.Clear();
            for (int i = 0; i < count; i++)
            {
                Image fill = VisorUI.Bar("Carga" + (i + 1), m_PipRow, Vector2.zero,
                    new Vector2(i * (k_PipWidth + 6f), 0f), new Vector2(k_PipWidth, 6f), new Color(0f, 0f, 0f, 0.5f),
                    VisorUI.Accent, out _);
                m_Pips.Add(fill);
            }
        }
    }
}
