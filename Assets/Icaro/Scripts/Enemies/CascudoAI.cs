using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Cascudo: corpo a corpo, lento e resistente. Corre até o jogador pelo NavMesh; perto dele, para e prepara o
    // golpe (o visual recua e ergue a casca), depois golpeia: a hitbox à frente aplica o dano uma única vez por
    // golpe. Entre golpes há uma recarga. A velocidade base é a do jogador (MaxSpeedOnGround) / PlayerSpeedDivisor,
    // lida no Start (sem os itens de velocidade do jogador, via PlayerStats.BaseGroundSpeed), e o EnemyTimeScaler a
    // multiplica pelo multiplicador de tempo. O aviso do golpe encurta com o multiplicador, mas nunca abaixo de 60%. Valores da ficha (CascudoDefinition).
    [RequireComponent(typeof(EnemyController))]
    public class CascudoAI : MonoBehaviour, ISpriteActionSource
    {
        public enum State
        {
            Chase,
            Windup,
            Strike,
            Cooldown,
        }

        [Tooltip("Objeto visual (trocável por um modelo); recua e avança no golpe")]
        public Transform Visual;

        [Tooltip("Casca (opcional): ergue durante a preparação")]
        public Transform Shell;

        [Header("Golpe (sem ficha)")]
        public float AttackRange = 2.4f;
        public float WindupDuration = 0.6f;
        public float Damage = 22f;
        public float Cooldown = 1.6f;
        public Vector3 HitboxSize = new Vector3(2.4f, 1.8f, 2.2f);
        public float HitboxForward = 1.3f;

        [Header("Movimento (sem ficha)")]
        public float PlayerSpeedDivisor = 3f;
        public float FallbackSpeed = 4.3f;

        [Tooltip("Intervalo para recalcular o caminho até o jogador, a 1,0x")]
        public float RepathInterval = 0.25f;

        const float k_StrikeDuration = 0.18f;

        public State CurrentState { get; private set; } = State.Chase;

        // Velocidade base usada (m/s a 1,0x), para conferência
        public float BaseSpeed { get; private set; }

        // Para o sprite (EnemySpriteView): preparação e golpe presos à duração; fora deles, parado ou andando
        public SpriteAction SpriteAction => CurrentState switch
        {
            State.Windup => SpriteAction.Windup,
            State.Strike => SpriteAction.Attack,
            _ => m_Agent && m_Agent.velocity.sqrMagnitude > 0.04f ? SpriteAction.Move : SpriteAction.Idle,
        };

        public float SpriteActionProgress => CurrentState == State.Windup || CurrentState == State.Strike
            ? Mathf.Clamp01(m_StateTime / Mathf.Max(0.0001f, m_StateDuration))
            : -1f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        ActorsManager m_ActorsManager;
        Vector3 m_VisualPosition;
        Quaternion m_VisualRotation;
        Vector3 m_ShellPosition;
        Quaternion m_ShellRotation;
        float m_StateTime;
        float m_StateDuration;
        float m_NextRepathTime;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();

            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                Damage = stats.Damage;
                Cooldown = stats.AttackCooldown;
                if (stats.Definition is CascudoDefinition cascudo)
                {
                    AttackRange = cascudo.AttackRange;
                    WindupDuration = cascudo.WindupDuration;
                    HitboxSize = cascudo.HitboxSize;
                    HitboxForward = cascudo.HitboxForward;
                    PlayerSpeedDivisor = cascudo.PlayerSpeedDivisor;
                    FallbackSpeed = cascudo.FallbackSpeed;
                }
            }

            // Antes do EnemyTimeScaler (ordem 100) guardar a velocidade base
            var player = FindAnyObjectByType<PlayerCharacterController>();
            var playerStats = player ? player.GetComponent<PlayerStats>() : null;
            float playerSpeed = playerStats ? playerStats.BaseGroundSpeed : player ? player.MaxSpeedOnGround : 0f;
            BaseSpeed = playerSpeed > 0f ? playerSpeed / PlayerSpeedDivisor : FallbackSpeed;
            m_Agent.speed = BaseSpeed;

            if (Visual)
            {
                m_VisualPosition = Visual.localPosition;
                m_VisualRotation = Visual.localRotation;
            }
            if (Shell)
            {
                m_ShellPosition = Shell.localPosition;
                m_ShellRotation = Shell.localRotation;
            }

            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };
        }

        void Update()
        {
            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (!player)
                return;

            m_StateTime += Time.deltaTime;
            switch (CurrentState)
            {
                case State.Chase:
                    UpdateChase(player);
                    break;
                case State.Windup:
                    UpdateWindup(player);
                    break;
                case State.Strike:
                    UpdateStrike();
                    break;
                case State.Cooldown:
                    UpdateChase(player, canAttack: m_StateTime >= m_StateDuration);
                    break;
            }
        }

        void UpdateChase(GameObject player, bool canAttack = true)
        {
            if (CurrentState == State.Cooldown && canAttack)
                EnterState(State.Chase, 0f);

            if (CurrentState == State.Chase && FlatDistance(player.transform.position) <= AttackRange)
            {
                EnterState(State.Windup, CombatTiming.Warning(WindupDuration));
                ThreatSignals.Report(transform, m_StateDuration + 0.3f);
                m_Agent.isStopped = true;
                m_Agent.ResetPath();
                return;
            }

            m_Agent.isStopped = false;
            if (Time.time >= m_NextRepathTime)
            {
                m_Controller.SetNavDestination(player.transform.position);
                m_NextRepathTime = Time.time + CombatTiming.Scaled(RepathInterval);
            }
        }

        // Preparação visível: o corpo recua e inclina para trás, a casca sobe
        void UpdateWindup(GameObject player)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer),
                    1f - Mathf.Exp(-10f * Time.deltaTime));

            float t = Mathf.Clamp01(m_StateTime / m_StateDuration);
            float ease = t * t * (3f - 2f * t);
            if (Visual)
            {
                Visual.localPosition = m_VisualPosition + new Vector3(0f, 0.1f, -0.35f) * ease;
                Visual.localRotation = m_VisualRotation * Quaternion.Euler(-18f * ease, 0f, 0f);
            }
            if (Shell)
            {
                Shell.localPosition = m_ShellPosition + Vector3.up * 0.25f * ease;
                Shell.localRotation = m_ShellRotation * Quaternion.Euler(-25f * ease, 0f, 0f);
            }

            if (t >= 1f)
            {
                EnterState(State.Strike, k_StrikeDuration);
                ApplyHit();
            }
        }

        // Golpe: o corpo avança de uma vez e volta ao lugar
        void UpdateStrike()
        {
            float t = Mathf.Clamp01(m_StateTime / m_StateDuration);
            float lunge = Mathf.Sin(t * Mathf.PI);
            if (Visual)
            {
                Visual.localPosition = m_VisualPosition + new Vector3(0f, 0f, 0.6f) * lunge;
                Visual.localRotation = m_VisualRotation * Quaternion.Euler(12f * lunge, 0f, 0f);
            }
            if (Shell)
            {
                Shell.localPosition = m_ShellPosition;
                Shell.localRotation = m_ShellRotation;
            }

            if (t >= 1f)
            {
                if (Visual)
                {
                    Visual.localPosition = m_VisualPosition;
                    Visual.localRotation = m_VisualRotation;
                }
                EnterState(State.Cooldown, CombatTiming.Scaled(Cooldown));
            }
        }

        // Hitbox à frente: o dano vale uma vez por golpe, mesmo que vários colliders do jogador estejam dentro
        void ApplyHit()
        {
            Collider[] hits = Physics.OverlapBox(HitboxCenter, HitboxSize * 0.5f, transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (!hit.GetComponentInParent<PlayerCharacterController>())
                    continue;

                var damageable = hit.GetComponentInParent<Damageable>();
                if (damageable)
                    CombatEvents.ApplyDamage(damageable, Damage, gameObject, hit.bounds.ClosestPoint(HitboxCenter));
                return;
            }
        }

        Vector3 HitboxCenter => transform.position + transform.forward * HitboxForward + Vector3.up * HitboxSize.y * 0.5f;

        void EnterState(State state, float duration)
        {
            CurrentState = state;
            m_StateTime = 0f;
            m_StateDuration = duration;
        }

        float FlatDistance(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(HitboxCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, HitboxSize);
        }
    }
}
