using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Aplica ao jogador os valores do CombatBalance: altura e altura dos olhos, vida máxima e os valores iniciais do PlayerStats (crítico,
    // multiplicador de dano, regeneração base, catálogo de itens). Roda no Awake, antes do Health.Start copiar a vida máxima para a
    // vida atual. Também liga a vida do jogador ao evento central de dano, depois do PlayerStats (as defesas dos
    // itens precisam ver o dano antes do evento).
    [DefaultExecutionOrder(-100)]
    public class PlayerBalance : MonoBehaviour
    {
        public CombatBalance Balance;

        void Awake()
        {
            var health = GetComponent<Health>();
            if (Balance && health)
                health.MaxHealth = Balance.PlayerMaxHealth;

            // Altura (antes do Start do controlador, que acerta a cápsula e a câmera)
            var controller = GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
            if (Balance && controller)
            {
                controller.CapsuleHeightStanding = Balance.PlayerHeight;
                controller.CameraHeightRatio = Mathf.Clamp01(Balance.PlayerEyeHeight / Balance.PlayerHeight);
            }

            var stats = GetComponent<PlayerStats>();
            if (!stats)
                stats = gameObject.AddComponent<PlayerStats>();
            if (Balance)
            {
                stats.BaseCritChance = Balance.PlayerCritChance;
                stats.CritMultiplier = Balance.PlayerCritMultiplier;
                stats.BaseDamageMultiplier = Balance.PlayerDamageMultiplier;
                stats.BaseRegen = Balance.PlayerBaseRegen;
                if (Balance.Items)
                    stats.Catalog = Balance.Items;
            }

            // Menus (título, pausa, morte, configurações)
            GameMenus.EnsureExists();

            // Carteira da run (Cronitas) e lojas
            if (!GetComponent<PlayerWallet>())
                gameObject.AddComponent<PlayerWallet>();
            ShopDirector.EnsureExists(Balance ? Balance.Shop : null);

            if (health && !GetComponent<DamageReporter>())
                gameObject.AddComponent<DamageReporter>();
        }
    }
}
