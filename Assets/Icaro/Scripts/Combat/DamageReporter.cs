using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Liga o Health deste objeto ao evento central de dano (CombatEvents.OnDamage). Inimigos (via EnemyStats) e o
    // jogador (via PlayerBalance) ganham um automaticamente.
    [RequireComponent(typeof(Health))]
    public class DamageReporter : MonoBehaviour
    {
        Health m_Health;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Health.OnDamaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (m_Health)
                m_Health.OnDamaged -= OnDamaged;
        }

        void OnDamaged(float amount, GameObject source) => CombatEvents.Report(m_Health, amount, source);
    }
}
