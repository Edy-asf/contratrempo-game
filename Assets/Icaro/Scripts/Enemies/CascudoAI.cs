using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Cascudo (o Orbe): corpo a corpo, lento e resistente. Anda aos pulinhos até o jogador pelo NavMesh; a uma
    // distância média, para, agacha e brilha (o aviso) e salta até onde o jogador estava no começo do aviso (até
    // LeapMaxDistance), em arco, causando dano em área ao cair (quem estiver pulando alto escapa). Uma marca no chão
    // mostra o ponto da queda durante o aviso e o salto, piscando no fim. Depois, recarga. A velocidade base é a do
    // jogador (sem os itens de velocidade, PlayerStats.BaseGroundSpeed) / PlayerSpeedDivisor, e o EnemyTimeScaler a
    // multiplica pelo multiplicador de tempo. Tempos no relógio dos inimigos (CombatTiming). Valores da ficha
    // (CascudoDefinition).
    [RequireComponent(typeof(EnemyController))]
    public class CascudoAI : MonoBehaviour, ISpriteActionSource
    {
        public enum State
        {
            Chase,
            Windup,
            Leap,
            Cooldown,
        }

        [Tooltip("Objeto visual (o sprite); dá os pulinhos ao andar")]
        public Transform Visual;

        [Tooltip("Sem uso (era a casca do visual antigo)")]
        public Transform Shell;

        [Header("Sem ficha")]
        public float AttackRange = 7f;
        public float WindupDuration = 0.8f;
        public float Damage = 22f;
        public float Cooldown = 1.6f;
        public float LeapMaxDistance = 9f;
        public float LeapHeight = 1.8f;
        public float LeapTime = 0.55f;
        public float LandTime = 0.4f;
        public float SlamRadius = 3f;
        public float SlamDodgeHeight = 1.2f;
        public float HopHeight = 0.15f;
        public float HopsPerMeter = 0.9f;
        public float PlayerSpeedDivisor = 3f;
        public float FallbackSpeed = 4.3f;

        [Tooltip("Intervalo para recalcular o caminho até o jogador, a 1,0x")]
        public float RepathInterval = 0.25f;

        public State CurrentState { get; private set; } = State.Chase;

        // Velocidade base usada (m/s a 1,0x), para conferência
        public float BaseSpeed { get; private set; }

        // Para o sprite: o aviso e o salto (com a queda) presos à duração; fora deles, parado ou andando
        public SpriteAction SpriteAction => CurrentState switch
        {
            State.Windup => SpriteAction.Windup,
            State.Leap => SpriteAction.Attack,
            _ => m_Agent && m_Agent.velocity.sqrMagnitude > 0.04f ? SpriteAction.Move : SpriteAction.Idle,
        };

        public float SpriteActionProgress => CurrentState == State.Windup || CurrentState == State.Leap
            ? Mathf.Clamp01(m_StateTime / Mathf.Max(0.0001f, m_StateDuration))
            : -1f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        ActorsManager m_ActorsManager;
        Vector3 m_VisualPosition;
        float m_StateTime;
        float m_StateDuration;
        float m_NextRepathTime;
        float m_HopPhase;
        Vector3 m_LastPosition;

        // Salto
        Vector3 m_LeapFrom;
        Vector3 m_LeapTarget;
        Vector3 m_LeapNormal;
        float m_AirTime;
        bool m_Landed;
        GroundMarker m_Marker;
        Sprite[] m_MarkerFrames;
        float m_MarkerAge;
        float m_MarkerDuration;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            // O EnemyController invoca estes eventos sem checar null; precisam ter assinante
            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };
            m_Agent = GetComponent<NavMeshAgent>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();

            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                Damage = stats.Damage;
                Cooldown = stats.AttackCooldown;
                if (stats.Definition is CascudoDefinition d)
                {
                    AttackRange = d.AttackRange;
                    WindupDuration = d.WindupDuration;
                    LeapMaxDistance = d.LeapMaxDistance;
                    LeapHeight = d.LeapHeight;
                    LeapTime = d.LeapTime;
                    LandTime = d.LandTime;
                    SlamRadius = d.SlamRadius;
                    SlamDodgeHeight = d.SlamDodgeHeight;
                    HopHeight = d.HopHeight;
                    HopsPerMeter = d.HopsPerMeter;
                    PlayerSpeedDivisor = d.PlayerSpeedDivisor;
                    FallbackSpeed = d.FallbackSpeed;
                }
            }

            // Antes do EnemyTimeScaler (ordem 100) guardar a velocidade base
            var player = FindAnyObjectByType<PlayerCharacterController>();
            var playerStats = player ? player.GetComponent<PlayerStats>() : null;
            float playerSpeed = playerStats ? playerStats.BaseGroundSpeed : player ? player.MaxSpeedOnGround : 0f;
            BaseSpeed = playerSpeed > 0f ? playerSpeed / PlayerSpeedDivisor : FallbackSpeed;
            m_Agent.speed = BaseSpeed;

            if (Visual)
                m_VisualPosition = Visual.localPosition;
            m_LastPosition = transform.position;
        }

        void OnDisable()
        {
            if (m_Marker)
                m_Marker.gameObject.SetActive(false);
            if (m_Agent && !m_Agent.updatePosition)
                m_Agent.updatePosition = true;
        }

        void OnDestroy()
        {
            if (m_Marker)
                Destroy(m_Marker.gameObject);
        }

        void Update()
        {
            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (!player)
                return;

            m_StateTime += CombatTiming.DeltaTime;
            switch (CurrentState)
            {
                case State.Chase:
                case State.Cooldown:
                    UpdateChase(player);
                    break;
                case State.Windup:
                    UpdateWindup(player);
                    break;
                case State.Leap:
                    UpdateLeap();
                    break;
            }

            UpdateHop();
            UpdateMarker();
        }

        void UpdateChase(GameObject player)
        {
            if (CurrentState == State.Cooldown && m_StateTime >= m_StateDuration)
                EnterState(State.Chase, 0f);

            if (CurrentState == State.Chase && FlatDistance(player.transform.position) <= AttackRange &&
                m_Agent.isOnNavMesh)
            {
                BeginWindup(player.transform.position);
                return;
            }

            if (!m_Agent.isOnNavMesh)
                return;
            m_Agent.isStopped = false;
            if (CombatTiming.Now >= m_NextRepathTime)
            {
                m_Controller.SetNavDestination(player.transform.position);
                m_NextRepathTime = CombatTiming.Now + CombatTiming.Scaled(RepathInterval);
            }
        }

        // O ponto da queda fica preso onde o jogador estava agora (quem se mexe durante o aviso escapa)
        void BeginWindup(Vector3 playerPosition)
        {
            Vector3 offset = playerPosition - transform.position;
            offset.y = 0f;
            if (offset.magnitude > LeapMaxDistance)
                offset = offset.normalized * LeapMaxDistance;
            // Cai no ponto do NavMesh mais perto (o jogador fica dentro do domo, então a queda também)
            Vector3 target = transform.position + offset;
            if (NavMesh.SamplePosition(target, out NavMeshHit onMesh, 3f, NavMesh.AllAreas))
                target = onMesh.position;

            m_LeapNormal = Vector3.up;
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                m_LeapNormal = ground.normal;
            m_LeapTarget = target;

            m_Agent.isStopped = true;
            m_Agent.ResetPath();
            float windup = CombatTiming.Warning(WindupDuration);
            EnterState(State.Windup, windup);
            m_AirTime = CombatTiming.Scaled(LeapTime);
            ShowMarker(windup + m_AirTime);
            ThreatSignals.Report(transform, windup + m_AirTime);
            EnemySounds.Play(EnemySounds.PlasmaCharge, transform.position + Vector3.up, 0.8f, 0.55f / windup * 0.8f,
                0.05f, 4f);
        }

        void UpdateWindup(GameObject player)
        {
            Vector3 toTarget = m_LeapTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                toTarget = player.transform.position - transform.position;
                toTarget.y = 0f;
            }
            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget),
                    1f - Mathf.Exp(-10f * CombatTiming.DeltaTime));

            if (m_StateTime < m_StateDuration)
                return;

            m_LeapFrom = transform.position;
            m_Landed = false;
            m_Agent.updatePosition = false;
            EnterState(State.Leap, m_AirTime + CombatTiming.Scaled(LandTime));
            EnemySounds.Play(EnemySounds.MortarLaunch, transform.position, 0.7f, 0.7f, 0.05f, 4f);
        }

        // Arco até o ponto da queda; ao cair, dano em área e a onda no chão (do sprite)
        void UpdateLeap()
        {
            if (!m_Landed)
            {
                float u = Mathf.Clamp01(m_StateTime / Mathf.Max(0.0001f, m_AirTime));
                Vector3 p = Vector3.Lerp(m_LeapFrom, m_LeapTarget, u);
                p.y += 4f * LeapHeight * u * (1f - u);
                transform.position = p;
                if (u >= 1f)
                    Land();
                return;
            }

            if (m_StateTime >= m_StateDuration)
                EnterState(State.Cooldown, CombatTiming.Scaled(Cooldown));
        }

        void Land()
        {
            m_Landed = true;
            if (m_Agent.isOnNavMesh || NavMesh.SamplePosition(m_LeapTarget, out _, 2f, NavMesh.AllAreas))
                m_Agent.Warp(m_LeapTarget);
            m_Agent.updatePosition = true;
            transform.position = m_LeapTarget;
            m_LastPosition = m_LeapTarget;
            HideMarker();

            EnemyFxBurst.PlayFlat(EnemyFxSprites.Scorch(EnemyFxStyle.Plasma), m_LeapTarget, m_LeapNormal, 1f, 3f,
                SlamRadius, 3f);
            EnemySounds.Play(EnemySounds.MortarBlast, m_LeapTarget, 1f, 0.75f, 0.06f, 8f);

            if (BossTarget.Exists && BossTarget.FlatDistance(m_LeapTarget) <= SlamRadius &&
                BossTarget.Feet.y - m_LeapTarget.y <= SlamDodgeHeight)
                BossTarget.Damage(Damage, gameObject, BossTarget.Chest);
        }

        // Pulinhos ao andar: o visual sobe e desce pelo caminho andado
        void UpdateHop()
        {
            if (!Visual)
                return;
            Vector3 moved = transform.position - m_LastPosition;
            moved.y = 0f;
            m_LastPosition = transform.position;
            bool walking = CurrentState == State.Chase || CurrentState == State.Cooldown;
            if (walking)
                m_HopPhase += moved.magnitude * HopsPerMeter * Mathf.PI;
            float hop = walking ? Mathf.Abs(Mathf.Sin(m_HopPhase)) * HopHeight : 0f;
            Visual.localPosition = m_VisualPosition + Vector3.up * hop;
        }

        // ---------- marca da queda ----------

        void ShowMarker(float duration)
        {
            if (!m_Marker)
                m_Marker = GroundMarker.Create("Orbe_Marca");
            m_MarkerFrames = EnemyFxSprites.Marker(SlamRadius);
            m_MarkerAge = 0f;
            m_MarkerDuration = Mathf.Max(0.1f, duration);
            m_Marker.Place(m_LeapTarget, SlamRadius, m_MarkerFrames[0]);
        }

        void HideMarker()
        {
            if (m_Marker)
                m_Marker.gameObject.SetActive(false);
        }

        void UpdateMarker()
        {
            if (!m_Marker || !m_Marker.gameObject.activeSelf)
                return;
            m_MarkerAge += CombatTiming.DeltaTime;
            float u = Mathf.Clamp01(m_MarkerAge / m_MarkerDuration);
            int frame = Mathf.Clamp(Mathf.FloorToInt(u * m_MarkerFrames.Length), 0, m_MarkerFrames.Length - 1);
            float rate = Mathf.Lerp(6f, 16f, Mathf.InverseLerp(0.7f, 1f, u));
            m_Marker.SetFrame(m_MarkerFrames[frame], u, u < 0.7f || Mathf.FloorToInt(m_MarkerAge * rate) % 2 == 0);
        }

        // Retorno de Kairós: volta a perseguir, sem salto nem marca no meio
        void OnTimeRewound()
        {
            HideMarker();
            if (m_Agent)
            {
                m_Agent.updatePosition = true;
                if (m_Agent.isOnNavMesh)
                    m_Agent.Warp(transform.position);
            }
            EnterState(State.Cooldown, CombatTiming.Scaled(Cooldown * 0.5f));
        }

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
            Gizmos.DrawWireSphere(transform.position, AttackRange);
            if (CurrentState == State.Windup || CurrentState == State.Leap)
                Gizmos.DrawWireSphere(m_LeapTarget, SlamRadius);
        }
    }
}
