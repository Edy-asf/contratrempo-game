using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Armas carregadas pelo Ícaro: até WeaponCatalog.MaxCarried (2, a inicial conta), trocadas com Q/E ou a rodinha
    // pelo PlayerWeaponsManager do Microgame. Comprar uma arma na loja com o limite cheio troca a arma na mão pela
    // nova. Como os itens, as armas duram a run e somem ao morrer. O PlayerStats adiciona este componente.
    public class PlayerArsenal : MonoBehaviour
    {
        const int k_Slots = 9;

        public static PlayerArsenal Instance { get; private set; }

        PlayerWeaponsManager m_Weapons;

        public WeaponCatalog Catalog => ShopDirector.Settings ? ShopDirector.Settings.Weapons : null;

        public int MaxCarried => Catalog ? Catalog.MaxCarried : 2;

        public WeaponController Active => m_Weapons ? m_Weapons.GetActiveWeapon() : null;

        public int CarriedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; m_Weapons && i < k_Slots; i++)
                {
                    if (m_Weapons.GetWeaponAtSlotIndex(i))
                        count++;
                }

                return count;
            }
        }

        public bool IsFull => CarriedCount >= MaxCarried;

        void Awake()
        {
            Instance = this;
            m_Weapons = GetComponent<PlayerWeaponsManager>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool Owns(WeaponDefinition weapon) =>
            weapon && weapon.WeaponPrefab && m_Weapons && m_Weapons.HasWeapon(weapon.WeaponPrefab);

        // Dá a arma ao jogador e a põe na mão; com o limite cheio, a arma na mão é descartada
        public bool Give(WeaponDefinition weapon)
        {
            if (!m_Weapons || !weapon || !weapon.WeaponPrefab || Owns(weapon))
                return false;

            if (IsFull && Active)
                m_Weapons.RemoveWeapon(Active);
            if (!m_Weapons.AddWeapon(weapon.WeaponPrefab))
                return false;

            WeaponController added = m_Weapons.HasWeapon(weapon.WeaponPrefab);
            for (int i = 0; i < k_Slots; i++)
            {
                if (m_Weapons.GetWeaponAtSlotIndex(i) == added)
                {
                    m_Weapons.SwitchToWeaponIndex(i);
                    break;
                }
            }

            return true;
        }
    }
}
