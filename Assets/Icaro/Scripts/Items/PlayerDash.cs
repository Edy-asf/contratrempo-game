using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Dash do Ícaro: ao apertar a tecla do dash (GameSettings.DashKey, F por padrão), avança DashDistance metros em
    // DashDuration segundos na direção do movimento (ou para a frente, parado), no chão ou no ar, sem atravessar
    // paredes (CharacterController.Move).
    // Gasta uma carga; cada carga recarrega em DashCooldown segundos. Durante o dash o jogador é invulnerável
    // (DashInvulnerable). Valores do CombatBalance (pelo PlayerBalance); os itens de dash mudam pelo PlayerStats:
    // recarga mais rápida, mais distância, dano nos inimigos do caminho, barreira a cada dash e cargas extras.
    // O jogador nunca é afetado pelo multiplicador de tempo. O PlayerStats adiciona este componente.
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(PlayerCharacterController))]
    public class PlayerDash : MonoBehaviour
    {
        const float k_InvulnerabilityMargin = 0.05f;

        static readonly Collider[] s_Hits = new Collider[24];

        // Cargas prontas, total de cargas e progresso da próxima (0 a 1), para o HUD
        public int Charges { get; private set; }
        public int MaxCharges { get; private set; } = 1;
        public float RechargeProgress => Charges >= MaxCharges ? 1f : Mathf.Clamp01(m_RechargeTimer / Cooldown);
        public bool IsDashing => m_DashTime > 0f;

        PlayerCharacterController m_Controller;
        CharacterController m_Character;
        PlayerInputHandler m_Input;
        PlayerStats m_Stats;
        PlayerDefenses m_Defenses;
        ItemProcs m_Procs;
        CombatBalance m_Balance;

        float m_RechargeTimer;
        float m_DashTime;
        float m_DashSpeed;
        Vector3 m_Direction;
        readonly HashSet<Health> m_HitThisDash = new HashSet<Health>();

        float BaseDistance => m_Balance ? m_Balance.DashDistance : 7f;
        float Duration => m_Balance ? m_Balance.DashDuration : 0.18f;
        float BaseCooldown => m_Balance ? m_Balance.DashCooldown : 2.5f;
        int BaseCharges => m_Balance ? m_Balance.DashCharges : 1;
        bool Invulnerable => !m_Balance || m_Balance.DashInvulnerable;

        // Recarga: a base dividida por (1 + bônus), como a cadência
        float Cooldown => BaseCooldown / (1f + Stat(PlayerStat.DashCooldownReduction));

        float Stat(PlayerStat stat) => m_Stats ? m_Stats.Get(stat) : 0f;

        void Awake()
        {
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Character = GetComponent<CharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
            m_Stats = GetComponent<PlayerStats>();
            m_Defenses = GetComponent<PlayerDefenses>();
            m_Procs = GetComponent<ItemProcs>();
        }

        void Start()
        {
            var balance = GetComponent<PlayerBalance>();
            m_Balance = balance ? balance.Balance : null;
            MaxCharges = BaseCharges;
            Charges = MaxCharges;

            if (!FindAnyObjectByType<DashHUD>())
                new GameObject("DashHUD").AddComponent<DashHUD>().Dash = this;
        }

        void Update()
        {
            if (m_Controller.IsDead)
            {
                m_DashTime = 0f;
                return;
            }

            UpdateCharges();

            if (m_DashTime > 0f)
            {
                UpdateDash();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool pressed = keyboard != null && keyboard[GameSettings.DashKey].wasPressedThisFrame;
            if (pressed && Charges > 0 && m_Input && m_Input.CanProcessInput())
                StartDash();
        }

        void UpdateCharges()
        {
            // Cargas extras de item entram (ou saem) na hora
            int max = BaseCharges + Mathf.FloorToInt(Stat(PlayerStat.DashCharges) + 0.01f);
            if (max != MaxCharges)
            {
                Charges = Mathf.Clamp(Charges + (max - MaxCharges), 0, max);
                MaxCharges = max;
            }

            if (Charges >= MaxCharges)
            {
                m_RechargeTimer = 0f;
                return;
            }

            m_RechargeTimer += Time.deltaTime;
            if (m_RechargeTimer >= Cooldown)
            {
                m_RechargeTimer -= Cooldown;
                Charges++;
            }
        }

        void StartDash()
        {
            Vector3 input = m_Input.GetMoveInput();
            Vector3 direction = transform.TransformDirection(new Vector3(input.x, 0f, input.z));
            direction.y = 0f;
            m_Direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;

            float distance = BaseDistance * (1f + Stat(PlayerStat.DashDistance));
            m_DashTime = Duration;
            m_DashSpeed = distance / Duration;
            Charges--;
            m_HitThisDash.Clear();

            if (m_Defenses)
            {
                if (Invulnerable)
                    m_Defenses.GrantInvulnerability(Duration + k_InvulnerabilityMargin);
                float barrier = Stat(PlayerStat.DashBarrier);
                if (barrier > 0f)
                    m_Defenses.AddBarrier(barrier, m_Stats.Catalog ? m_Stats.Catalog.DashBarrierDuration : 3f);
            }

            if (m_Controller.AudioSource && m_Controller.JumpSfx)
                m_Controller.AudioSource.PlayOneShot(m_Controller.JumpSfx, 0.7f);
        }

        void UpdateDash()
        {
            float dt = Mathf.Min(Time.deltaTime, m_DashTime);
            m_DashTime -= dt;

            // O controlador não soma o próprio movimento (nem a gravidade) durante o dash
            m_Controller.CharacterVelocity = Vector3.zero;
            if (m_Character && m_Character.enabled)
                m_Character.Move(m_Direction * (m_DashSpeed * dt));

            HitEnemies();

            // Fim: sai com a velocidade de corrida normal na mesma direção, sem parar seco
            if (m_DashTime <= 0f)
                m_Controller.CharacterVelocity = m_Direction * m_Controller.MaxSpeedOnGround;
        }

        // Lâmina de Fase: cada inimigo no caminho leva o dano uma vez por dash (dano de item)
        void HitEnemies()
        {
            float multiplier = Stat(PlayerStat.DashDamage);
            if (multiplier <= 0f || !m_Procs)
                return;

            float radius = m_Stats.Catalog ? m_Stats.Catalog.DashHitRadius : 1.6f;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, s_Hits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float damage = m_Stats.ShotDamage * multiplier;
            for (int i = 0; i < count; i++)
            {
                var stats = s_Hits[i].GetComponentInParent<EnemyStats>();
                var health = stats ? stats.GetComponent<Health>() : null;
                if (!health || health.CurrentHealth <= 0f || !m_HitThisDash.Add(health))
                    continue;
                m_Procs.Damage(health, damage, s_Hits[i].ClosestPointOnBounds(transform.position + Vector3.up),
                    explosion: false);
            }
        }
    }
}
