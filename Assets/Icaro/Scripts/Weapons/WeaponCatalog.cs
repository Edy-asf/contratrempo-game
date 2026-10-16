using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Lista das armas que as lojas podem vender e quantas o jogador carrega. Ligado ao ShopSettings.Weapons.
    [CreateAssetMenu(menuName = "Ícaro/Catálogo de armas", fileName = "WeaponCatalog")]
    public class WeaponCatalog : ScriptableObject
    {
        public List<WeaponDefinition> Weapons = new List<WeaponDefinition>();

        [Tooltip("Armas carregadas ao mesmo tempo (a inicial conta); comprar com o limite cheio troca a arma na mão")]
        [Min(1)] public int MaxCarried = 2;

        // Sorteia até 'count' armas diferentes, sem as excluídas (ex.: as que o jogador já tem)
        public List<WeaponDefinition> Roll(int count, Func<WeaponDefinition, bool> exclude)
        {
            var pool = Weapons.FindAll(w => w && w.WeaponPrefab && w.Projectile && (exclude == null || !exclude(w)));
            var result = new List<WeaponDefinition>();
            while (result.Count < count && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }
    }
}
