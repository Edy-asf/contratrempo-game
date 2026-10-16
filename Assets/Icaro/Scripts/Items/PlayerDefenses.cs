using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Defesas do jogador, aplicadas no instante em que ele perde vida (Health.OnDamaged), antes do evento central
    // de dano e da checagem de morte do Health. Em ordem:
    // 1. Justiça: um acerto tira no máximo MaxHitFraction da vida máxima (20%; 25% para chefes e ataques pesados,
    //    HeavyMaxHitFraction). A ficha do inimigo pode ter o próprio limite (1 = sem limite: Predador e Golem).
    //    O excesso volta para a vida.
    // 2. Barreira do dash (Égide de Impulso) e depois o Escudo de Sincronia absorvem o resto do dano; a barreira
    //    some sozinha em alguns segundos, o escudo recarrega depois de alguns segundos sem levar dano.
    // 3. Justiça: depois de um acerto, HitInvulnerability segundos (0,3 s) de invulnerabilidade, para um enxame não
    //    matar com vários acertos no mesmo instante. Dano contínuo (CombatEvents, continuous) ignora e não liga.
    // 4. Segunda Ignição: se a vida zerar, revive com metade da vida e o item é consumido.
    // Os limites vêm do CombatBalance (pelo PlayerBalance). O PlayerStats adiciona este componente no Awake, antes
    // do PlayerBalance ligar o DamageReporter.
    public class PlayerDefenses : MonoBehaviour
    {
        const float k_DefaultMaxHitFraction = 0.2f;
        const float k_DefaultHeavyMaxHitFraction = 0.25f;
        const float k_DefaultHitInvulnerability = 0.3f;

        public float Shield { get; private set; }
        public float ShieldMax { get; private set; }

        // Barreira temporária (ganha a cada dash com a Égide de Impulso)
        public float Barrier { get; private set; }

        // Dentro da janela de invulnerabilidade curta depois de um acerto
        public bool IsHitInvulnerable => Time.time < m_InvulnerableUntil;

        PlayerStats m_Stats;
        Health m_Health;
        PlayerCharacterController m_Controller;
        CombatBalance m_Balance;
        float m_LastDamageTime = float.NegativeInfinity;
        float m_InvulnerableUntil = float.NegativeInfinity;
        bool m_SetInvincible;
        float m_BarrierDecay;

        void Awake()
        {
            m_Stats = GetComponent<PlayerStats>();
            m_Health = GetComponent<Health>();
            m_Controller = GetComponent<PlayerCharacterController>();
            if (m_Health)
                m_Health.OnDamaged += OnDamaged;
        }

        void Start()
        {
            var balance = GetComponent<PlayerBalance>();
            m_Balance = balance ? balance.Balance : null;
        }

        void OnDestroy()
        {
            if (m_Health)
                m_Health.OnDamaged -= OnDamaged;
        }

        void Update()
        {
            if (!m_Health)
                return;

            // Fim da invulnerabilidade curta (só desliga o que este componente ligou)
            if (m_SetInvincible && !IsHitInvulnerable)
            {
                m_Health.Invincible = false;
                m_SetInvincible = false;
            }

            UpdateShield();

            if (Barrier > 0f)
                Barrier = Mathf.Max(0f, Barrier - m_BarrierDecay * Time.deltaTime);
        }

        // Invulnerável por alguns segundos (dash); não encurta uma invulnerabilidade maior em andamento
        public void GrantInvulnerability(float seconds)
        {
            if (!m_Health || seconds <= 0f)
                return;
            m_InvulnerableUntil = Mathf.Max(m_InvulnerableUntil, Time.time + seconds);
            m_Health.Invincible = true;
            m_SetInvincible = true;
        }

        // Barreira que absorve dano antes do escudo e some linearmente em 'duration' segundos
        public void AddBarrier(float amount, float duration)
        {
            if (amount <= 0f)
                return;
            Barrier += amount;
            m_BarrierDecay = Barrier / Mathf.Max(0.1f, duration);
        }

        void UpdateShield()
        {
            if (!m_Stats)
                return;

            float previousMax = ShieldMax;
            ShieldMax = m_Stats.Get(PlayerStat.ShieldFraction) * m_Health.MaxHealth;
            // Escudo novo (ou maior) entra cheio na parte que cresceu
            if (ShieldMax > previousMax)
                Shield += ShieldMax - previousMax;
            Shield = Mathf.Min(Shield, ShieldMax);

            ItemCatalog catalog = m_Stats.Catalog;
            float delay = catalog ? catalog.ShieldRechargeDelay : 3f;
            float rechargeTime = catalog ? Mathf.Max(0.05f, catalog.ShieldRechargeTime) : 1f;
            if (Shield < ShieldMax && Time.time - m_LastDamageTime >= delay)
                Shield = Mathf.Min(ShieldMax, Shield + ShieldMax / rechargeTime * Time.deltaTime);
        }

        void OnDamaged(float amount, GameObject source)
        {
            m_LastDamageTime = Time.time;

            // Caiu do mapa (Health.Kill): morte sem proteções
            if (m_Controller && transform.position.y < m_Controller.KillHeight)
                return;

            // 1. Limite por acerto: devolve à vida o que passou do teto
            float cap = m_Health.MaxHealth * HitFraction(source);
            if (amount > cap)
            {
                m_Health.CurrentHealth = Mathf.Min(m_Health.MaxHealth, m_Health.CurrentHealth + (amount - cap));
                amount = cap;
            }

            // 2. Barreira do dash e escudo: devolvem à vida o que absorvem
            float fromBarrier = Mathf.Min(Barrier, amount);
            if (fromBarrier > 0f)
            {
                Barrier -= fromBarrier;
                amount -= fromBarrier;
                m_Health.CurrentHealth = Mathf.Min(m_Health.MaxHealth, m_Health.CurrentHealth + fromBarrier);
            }

            float absorbed = Mathf.Min(Shield, amount);
            if (absorbed > 0f)
            {
                Shield -= absorbed;
                m_Health.CurrentHealth = Mathf.Min(m_Health.MaxHealth, m_Health.CurrentHealth + absorbed);
            }

            // 3. Invulnerabilidade curta (o Health ignora os próximos acertos enquanto Invincible)
            float invulnerability = m_Balance ? m_Balance.HitInvulnerability : k_DefaultHitInvulnerability;
            if (invulnerability > 0f && !CombatEvents.CurrentHitIsContinuous)
            {
                m_InvulnerableUntil = Time.time + invulnerability;
                m_Health.Invincible = true;
                m_SetInvincible = true;
            }

            TryRevive();
        }

        // Fração máxima da vida que este acerto pode tirar: a da ficha do atacante, a de ataque pesado (chefes,
        // acertos marcados como heavy) ou a padrão
        float HitFraction(GameObject source)
        {
            float normal = m_Balance ? m_Balance.MaxHitFraction : k_DefaultMaxHitFraction;
            float heavy = m_Balance ? m_Balance.HeavyMaxHitFraction : k_DefaultHeavyMaxHitFraction;

            // A ficha do atacante manda (1 = sem limite, ex.: Predador e Golem); senão, pesado ou padrão
            var stats = source ? source.GetComponentInParent<EnemyStats>() : null;
            if (stats && stats.Definition && stats.Definition.MaxHitFraction > 0f)
                return stats.Definition.MaxHitFraction;
            if (CombatEvents.CurrentHitIsHeavy || (stats && stats.IsBoss))
                return heavy;
            return normal;
        }

        // 4. Segunda Ignição (quem caiu do mapa já saiu antes, no OnDamaged)
        void TryRevive()
        {
            if (!m_Stats || m_Health.CurrentHealth > 0f || m_Stats.Get(PlayerStat.Revives) < 1f)
                return;
            if (!m_Stats.ConsumeItem(PlayerStat.Revives, out ItemDefinition item))
                return;

            m_Health.CurrentHealth = m_Health.MaxHealth * 0.5f;
            Shield = ShieldMax;

            DisplayMessageEvent message = Events.DisplayMessageEvent;
            message.Message = item.DisplayName + ": revivido!";
            message.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(message);
        }
    }
}
