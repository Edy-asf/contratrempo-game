using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Predador: quando vê o jogador a até AttackRange, para e avisa (brilho, som e uma linha no chão até onde o
    // jogador está agora). Ao fim do aviso investe em linha reta até aquele ponto, sem mudar de direção, causando
    // dano no contato; para antes se bater em algo. Depois circula o jogador a média distância durante a recarga.
    // Os valores vêm da ficha (PredadorDefinition); os campos abaixo valem só sem ficha.
    // O aviso encurta com o multiplicador de tempo até 60%; investida e recarga seguem o multiplicador inteiro.
    [RequireComponent(typeof(EnemyController))]
    public class PredadorAI : MonoBehaviour, ISpriteActionSource
    {
        public enum State
        {
            Approach,
            Warning,
            Dash,
            Cooldown,
        }

        [Header("Ataque (sem ficha)")]
        public float AttackRange = 25f;
        public float WarningDuration = 0.8f;
        public float DashSpeed = 26f;
        public float DashDamage = 35f;
        public float DashHitRange = 1.7f;
        public float Cooldown = 4f;
        public float CircleDistance = 12f;

        [Tooltip("Intervalo para recalcular o caminho, a 1,0x")]
        public float RepathInterval = 0.25f;

        [Header("Aviso visual")]
        [Tooltip("Partes que brilham durante o aviso (não use as que têm o BodyMaterial do EnemyController)")]
        public Renderer[] WarningRenderers;

        [ColorUsage(true, true)] public Color IdleEmission = new Color(1.2f, 0.6f, 0.1f);
        [ColorUsage(true, true)] public Color WarningEmission = new Color(8f, 0.4f, 0.1f);

        [Tooltip("Parte visual que encolhe e estica durante o aviso")]
        public Transform Body;

        [Tooltip("Material aditivo da linha de trajetória no chão")]
        public Material LineMaterial;

        [Tooltip("Cor da linha (0 a 1); o brilho HDR vem do material")]
        public Color LineColor = new Color(1f, 0.35f, 0.08f, 1f);

        public AudioClip WarningSound;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        public State CurrentState { get; private set; } = State.Approach;

        // Para o sprite (EnemySpriteView): aviso preso à duração; investida em galope; senão parado ou andando
        public SpriteAction SpriteAction => CurrentState switch
        {
            State.Warning => SpriteAction.Windup,
            State.Dash => SpriteAction.Attack,
            _ => m_Agent && m_Agent.velocity.sqrMagnitude > 0.04f ? SpriteAction.Move : SpriteAction.Idle,
        };

        public float SpriteActionProgress => CurrentState == State.Warning
            ? Mathf.Clamp01(m_StateTime / Mathf.Max(0.0001f, m_StateDuration))
            : -1f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        ActorsManager m_ActorsManager;
        MaterialPropertyBlock m_Block;
        TelegraphLine m_Line;
        Vector3 m_BodyBaseScale;
        Vector3 m_DashStart;
        Vector3 m_DashTarget;
        Vector3 m_DashDirection;
        float m_DashDistance;
        float m_DashTraveled;
        bool m_DashHit;
        float m_StateTime;
        float m_StateDuration;
        float m_NextRepathTime;
        float m_CircleSign = 1f;
        EnemyStats m_Stats;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            m_Block = new MaterialPropertyBlock();

            var stats = m_Stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                DashDamage = stats.Damage;
                Cooldown = stats.AttackCooldown;
                if (stats.Definition is PredadorDefinition predador)
                {
                    AttackRange = predador.AttackRange;
                    WarningDuration = predador.WarningDuration;
                    DashSpeed = predador.DashSpeed;
                    DashHitRange = predador.DashHitRange;
                    CircleDistance = predador.CircleDistance;
                }
            }

            // O EnemyController invoca estes eventos sem checar null; precisam ter assinante
            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };

            if (Body)
                m_BodyBaseScale = Body.localScale;

            if (LineMaterial)
            {
                m_Line = TelegraphLine.Create(null, LineMaterial, name + "_Trajetoria");
                m_Line.Hide();
            }

            SetGlow(0f);
        }

        void OnDestroy()
        {
            if (m_Line)
                Destroy(m_Line.gameObject);
        }

        void Update()
        {
            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (!player)
                return;

            m_StateTime += Time.deltaTime;
            float multiplier = TimeManager.CurrentMultiplier;

            switch (CurrentState)
            {
                case State.Approach:
                    UpdateApproach(player, multiplier);
                    break;
                case State.Warning:
                    UpdateWarning();
                    break;
                case State.Dash:
                    UpdateDash(player, multiplier);
                    break;
                case State.Cooldown:
                    UpdateCooldown(player, multiplier);
                    break;
            }
        }

        bool CanAttack(GameObject player) =>
            m_Controller.IsSeeingTarget && FlatDistance(player.transform.position) <= AttackRange;

        void UpdateApproach(GameObject player, float multiplier)
        {
            if (CanAttack(player))
            {
                EnterWarning(player);
                return;
            }

            if (Time.time >= m_NextRepathTime)
            {
                m_Controller.SetNavDestination(player.transform.position);
                m_NextRepathTime = Time.time + RepathInterval / multiplier;
            }
        }

        void EnterWarning(GameObject player)
        {
            EnterState(State.Warning, CombatTiming.Warning(WarningDuration));
            ThreatSignals.Report(transform, CombatTiming.Warning(WarningDuration) + 0.6f);
            m_Agent.isStopped = true;
            m_Agent.ResetPath();
            m_Agent.updateRotation = false;

            // O alvo é a posição do jogador agora; a investida não corrige a direção depois
            m_DashStart = transform.position;
            m_DashTarget = player.transform.position;
            Vector3 toTarget = m_DashTarget - m_DashStart;
            toTarget.y = 0f;
            m_DashDistance = toTarget.magnitude;
            m_DashDirection = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;

            if (WarningSound)
                AudioUtility.CreateSFX(WarningSound, transform.position, AudioUtility.AudioGroups.EnemyAttack, 1f, 5f);
        }

        void UpdateWarning()
        {
            float progress = Mathf.Clamp01(m_StateTime / m_StateDuration);

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(m_DashDirection),
                1f - Mathf.Exp(-14f * Time.deltaTime));

            // Pisca cada vez mais rápido e encolhe como uma mola
            float blink = 0.5f + 0.5f * Mathf.Sin(m_StateTime * Mathf.Lerp(10f, 45f, progress));
            SetGlow(Mathf.Lerp(0.35f, 1f, progress) * blink + progress * 0.3f);
            if (Body)
                Body.localScale = Vector3.Scale(m_BodyBaseScale, new Vector3(1f + 0.15f * progress, 1f - 0.2f * progress, 1f));

            if (m_Line)
            {
                m_Line.SetStyle(Mathf.Lerp(0.25f, 0.6f, progress), LineColor * (0.5f + 0.5f * blink));
                m_Line.SetGroundPath(m_DashStart, m_DashStart + m_DashDirection * m_DashDistance);
            }

            if (progress >= 1f)
            {
                EnterState(State.Dash, 0f);
                m_DashTraveled = 0f;
                m_DashHit = false;
                SetGlow(1f);
                if (m_Line)
                    m_Line.Hide();
                if (Body)
                    Body.localScale = Vector3.Scale(m_BodyBaseScale, new Vector3(0.9f, 0.9f, 1.25f));
            }
        }

        void UpdateDash(GameObject player, float multiplier)
        {
            float remaining = m_DashDistance - m_DashTraveled;
            float step = Mathf.Min(DashSpeed * CombatTiming.Movement(m_Stats) * Time.deltaTime, remaining);
            Vector3 before = transform.position;
            if (step > 0f)
                m_Agent.Move(m_DashDirection * step);
            float moved = Vector3.Distance(before, transform.position);
            m_DashTraveled += moved;

            if (!m_DashHit)
            {
                Vector3 offset = player.transform.position - transform.position;
                bool touching = Mathf.Abs(offset.y) < 2f && new Vector2(offset.x, offset.z).magnitude <= DashHitRange;
                if (touching)
                {
                    var damageable = player.GetComponent<Damageable>();
                    if (damageable)
                        CombatEvents.ApplyDamage(damageable, DashDamage, gameObject,
                            player.transform.position + Vector3.up);
                    m_DashHit = true;
                }
            }

            // Fim: chegou ao ponto marcado ou bateu em algo (andou bem menos do que devia)
            bool blocked = step > 0.01f && moved < step * 0.25f;
            if (m_DashTraveled >= m_DashDistance - 0.05f || blocked)
                EnterCooldown();
        }

        void EnterCooldown()
        {
            EnterState(State.Cooldown, CombatTiming.Scaled(Cooldown));
            SetGlow(0f);
            if (Body)
                Body.localScale = m_BodyBaseScale;
            m_Agent.isStopped = false;
            m_Agent.updateRotation = true;
            m_NextRepathTime = 0f;
            m_CircleSign = Random.value < 0.5f ? -1f : 1f;
        }

        // Circula o jogador a CircleDistance: o destino é sempre um pouco à frente, girando em volta dele
        void UpdateCooldown(GameObject player, float multiplier)
        {
            if (Time.time >= m_NextRepathTime)
            {
                Vector3 center = player.transform.position;
                Vector3 away = transform.position - center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = -player.transform.forward;
                Vector3 next = Quaternion.Euler(0f, 40f * m_CircleSign, 0f) * away.normalized * CircleDistance;
                if (NavMesh.SamplePosition(center + next, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    m_Controller.SetNavDestination(hit.position);
                else
                    m_CircleSign = -m_CircleSign;
                m_NextRepathTime = Time.time + 0.3f / multiplier;
            }

            if (m_StateTime >= m_StateDuration)
            {
                if (CanAttack(player))
                    EnterWarning(player);
                else
                    EnterState(State.Approach, 0f);
            }
        }

        void EnterState(State state, float duration)
        {
            CurrentState = state;
            m_StateTime = 0f;
            m_StateDuration = duration;
            if (state == State.Approach)
            {
                m_Agent.isStopped = false;
                m_Agent.updateRotation = true;
                m_NextRepathTime = 0f;
            }
        }

        float FlatDistance(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        void SetGlow(float amount)
        {
            if (WarningRenderers == null)
                return;

            m_Block.SetColor(k_EmissionColor, Color.Lerp(IdleEmission, WarningEmission, amount));
            foreach (Renderer warningRenderer in WarningRenderers)
            {
                if (warningRenderer)
                    warningRenderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
