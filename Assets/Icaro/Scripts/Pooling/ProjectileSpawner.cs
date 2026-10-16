using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Cria projéteis de arma: os do Ícaro que suportam reaproveitamento (HomingProjectile) saem do PrefabPool; os
    // outros são instanciados como no Microgame. Registrado no WeaponController.ProjectileFactory ao carregar o jogo,
    // e usado também por itens que disparam tiros extras (Eco Temporal).
    public static class ProjectileSpawner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            WeaponController.ProjectileFactory = Spawn;
        }

        public static ProjectileBase Spawn(ProjectileBase prefab, Vector3 position, Quaternion rotation)
        {
            if (!prefab)
                return null;
            if (prefab is HomingProjectile)
                return PrefabPool.Get(prefab, position, rotation);
            return Object.Instantiate(prefab, position, rotation);
        }
    }
}
