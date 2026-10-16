using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Golem: lento, grande e resistente (inspirado nos golens de pedra do Risk of Rain 2).
    // Laser: mira com uma linha fina vermelha que acompanha o jogador, trava a mira um instante e dispara um feixe
    // grosso na direção travada, com uma pequena explosão no ponto de impacto (quem se mexe depois da trava desvia).
    // Pancada: se o jogador chega perto, levanta o tronco (anel de aviso no chão) e bate no chão com dano em área.
    // Valores da ficha (GolemDefinition); os campos abaixo valem só sem ficha. Avisos encurtam com o multiplicador
    // até 60%; recargas seguem o multiplicador inteiro.
    [RequireComponent(typeof(EnemyController))]
    public class GolemAI : MonoBehaviour
    {
        public enum State
        {
            Move,
            Aim,
            Lock,
            Fire,
            SlamWarning,
        }

        [Header("Partes")]
        [Tooltip("Parte que gira para mirar (tronco)")]
        public Transform Torso;

        [Tooltip("De onde sai o laser (núcleo do peito)")]
        public Transform Muzzle;

        [Header("Laser (sem ficha)")]
        public float LaserRange = 45f;
        public float AimDuration = 2f;
        public float LockDuration = 0.4f;
        public float LaserDamage = 45f;
        public float LaserCooldown = 5f;
        public float BeamRadius = 0.45f;
        public float ExplosionRadius = 3f;
        public float ExplosionDamage = 15f;

        [Header("Pancada (sem ficha)")]
        public float SlamTriggerRange = 5f;
        public float SlamRadius = 6.5f;
        public float SlamDamage = 30f;
        public float SlamWarning = 0.9f;
        public float SlamCooldown = 3f;

        [Header("Movimento (sem ficha)")]
        public float PreferredDistance = 18f;
        public float RepathInterval = 0.4f;
        public float TurnSharpness = 3f;

        [Header("Visual e som")]
        public Material LineMaterial;

        [Tooltip("Cores das linhas (0 a 1); o brilho HDR vem do material")]
        public Color AimColor = new Color(1f, 0.08f, 0.06f, 1f);
        public Color LockColor = new Color(1f, 0.55f, 0.25f, 1f);
        public Color BeamColor = new Color(0.7f, 0.9f, 1f, 1f);
        public Color SlamColor = new Color(1f, 0.7f, 0.2f, 1f);

        public GameObject ImpactVfx;
        public AudioClip AimSound;
        public AudioClip FireSound;
        public AudioClip SlamSound;

        public State CurrentState { get; private set; } = State.Move;

        const float k_BeamShowTime = 0.3f;
        const float k_TorsoLift = 0.35f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        ActorsManager m_ActorsManager;
        Collider[] m_SelfColliders;
        TelegraphLine m_AimLine;
        TelegraphLine m_SlamRing;
        Vector3 m_TorsoBasePosition;
        Vector3 m_LockedDirection;
        Vector3 m_BeamEnd;
        float m_StateTime;
        float m_StateDuration;
        float m_NextLaserTime;
        float m_NextSlamTime;
        float m_NextRepathTime;
        float m_ExplosionScale = 1f;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            m_SelfColliders = GetComponentsInChildren<Collider>(true);

            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                LaserDamage = stats.Damage;
                LaserCooldown = stats.AttackCooldown;
                if (stats.Definition is GolemDefinition golem)
                {
                    LaserRange = golem.LaserRange;
                    AimDuration = golem.AimDuration;
                    LockDuration = golem.LockDuration;
                    BeamRadius = golem.BeamRadius;
                    ExplosionRadius = golem.ExplosionRadius;
                    ExplosionDamage = stats.ScaleDamage(golem.ExplosionDamage);
                    SlamTriggerRange = golem.SlamTriggerRange;
                    SlamRadius = golem.SlamRadius;
                    SlamDamage = stats.ScaleDamage(golem.SlamDamage);
                    SlamWarning = golem.SlamWarning;
                    SlamCooldown = golem.SlamCooldown;
                    PreferredDistance = golem.PreferredDistance;
                }
            }

            // Chefe maior (escala na raiz): alcance da pancada e explosão crescem junto
            float scale = transform.lossyScale.x;
            SlamTriggerRange *= scale;
            SlamRadius *= scale;
            m_ExplosionScale = scale;

            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };

            if (Torso)
                m_TorsoBasePosition = Torso.localPosition;
            if (!Muzzle)
                Muzzle = Torso ? Torso : transform;

            if (LineMaterial)
            {
                m_AimLine = TelegraphLine.Create(null, LineMaterial, name + "_Laser");
                m_SlamRing = TelegraphLine.Create(null, LineMaterial, name + "_Pancada");
                m_AimLine.Hide();
                m_SlamRing.Hide();
            }

            // Primeiro tiro só depois de um respiro
            m_NextLaserTime = Time.time + CombatTiming.Scaled(1.5f);
        }

        void OnDestroy()
        {
            if (m_AimLine)
                Destroy(m_AimLine.gameObject);
            if (m_SlamRing)
                Destroy(m_SlamRing.gameObject);
        }

        void Update()
        {
            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (!player)
                return;

            m_StateTime += Time.deltaTime;
            Vector3 target = AimPoint(player);

            switch (CurrentState)
            {
                case State.Move:
                    UpdateMove(player, target);
                    break;
                case State.Aim:
                    UpdateAim(player, target);
                    break;
                case State.Lock:
                    UpdateLock();
                    break;
                case State.Fire:
                    UpdateFire();
                    break;
                case State.SlamWarning:
                    UpdateSlamWarning(player);
                    break;
            }
        }

        // ---------- movimento ----------

        void UpdateMove(GameObject player, Vector3 target)
        {
            float distance = FlatDistance(player.transform.position);

            if (ShouldSlam(player, distance))
            {
                EnterSlam();
                return;
            }

            bool seeing = m_Controller.IsSeeingTarget;
            if (seeing && distance <= LaserRange && Time.time >= m_NextLaserTime)
            {
                EnterState(State.Aim, CombatTiming.Warning(AimDuration));
                ThreatSignals.Report(transform,
                    CombatTiming.Warning(AimDuration) + CombatTiming.Warning(LockDuration) + 0.4f);
                m_Agent.isStopped = true;
                m_Agent.ResetPath();
                if (AimSound)
                    AudioUtility.CreateSFX(AimSound, Muzzle.position, AudioUtility.AudioGroups.WeaponChargeBuildup, 1f, 8f);
                return;
            }

            if (!seeing || distance > PreferredDistance)
            {
                m_Agent.isStopped = false;
                if (Time.time >= m_NextRepathTime)
                {
                    m_Controller.SetNavDestination(player.transform.position);
                    m_NextRepathTime = Time.time + CombatTiming.Scaled(RepathInterval);
                }
            }
            else
            {
                m_Agent.isStopped = true;
            }

            TurnTorso(target);
        }

        bool ShouldSlam(GameObject player, float distance) =>
            distance <= SlamTriggerRange && Time.time >= m_NextSlamTime &&
            Mathf.Abs(player.transform.position.y - transform.position.y) < 3f;

        // ---------- laser ----------

        void UpdateAim(GameObject player, Vector3 target)
        {
            if (ShouldSlam(player, FlatDistance(player.transform.position)))
            {
                HideAimLine();
                EnterSlam();
                return;
            }

            TurnTorso(target);
            Vector3 direction = (target - Muzzle.position).normalized;
            float progress = Mathf.Clamp01(m_StateTime / m_StateDuration);
            DrawLaserLine(direction, Mathf.Lerp(0.03f, 0.06f, progress), AimColor * (0.6f + 0.4f * progress));

            if (m_StateTime >= m_StateDuration)
            {
                // A mira trava aqui: o feixe sai nesta direção, mesmo que o jogador se mova
                m_LockedDirection = direction;
                EnterState(State.Lock, CombatTiming.Warning(LockDuration));
            }
        }

        void UpdateLock()
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(m_StateTime * 60f);
            DrawLaserLine(m_LockedDirection, 0.08f, LockColor * (0.6f + 0.6f * blink));

            if (m_StateTime >= m_StateDuration)
                FireLaser();
        }

        void FireLaser()
        {
            GameObject player = m_ActorsManager.Player;
            bool playerHit = false;
            m_BeamEnd = CastBeam(m_LockedDirection, out Collider hitCollider);

            if (hitCollider && hitCollider.GetComponentInParent<PlayerCharacterController>())
            {
                var damageable = hitCollider.GetComponentInParent<Damageable>();
                if (damageable)
                    CombatEvents.ApplyDamage(damageable, LaserDamage, gameObject, m_BeamEnd, heavy: true);
                playerHit = true;
            }

            // Explosão pequena no impacto (não soma com o acerto direto)
            if (!playerHit && player && Vector3.Distance(AimPoint(player), m_BeamEnd) <= ExplosionRadius * m_ExplosionScale)
            {
                var damageable = player.GetComponent<Damageable>();
                if (damageable)
                    CombatEvents.ApplyDamage(damageable, ExplosionDamage, gameObject, AimPoint(player), false, true);
            }

            SpawnImpact(m_BeamEnd, 0.6f * m_ExplosionScale);
            if (FireSound)
                AudioUtility.CreateSFX(FireSound, m_BeamEnd, AudioUtility.AudioGroups.Impact, 1f, 10f);

            m_NextLaserTime = Time.time + CombatTiming.Scaled(LaserCooldown);
            EnterState(State.Fire, k_BeamShowTime);
        }

        void UpdateFire()
        {
            float fade = 1f - Mathf.Clamp01(m_StateTime / m_StateDuration);
            if (m_AimLine)
            {
                m_AimLine.SetStyle(Mathf.Lerp(0.15f, 0.7f, fade), BeamColor * fade);
                m_AimLine.SetSegment(Muzzle.position, m_BeamEnd);
            }

            if (m_StateTime >= m_StateDuration)
            {
                HideAimLine();
                EnterState(State.Move, 0f);
            }
        }

        // Feixe com espessura; o primeiro obstáculo (que não seja o próprio Golem) para o feixe
        Vector3 CastBeam(Vector3 direction, out Collider hitCollider)
        {
            hitCollider = null;
            RaycastHit[] hits = Physics.SphereCastAll(Muzzle.position, BeamRadius, direction, LaserRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float closest = LaserRange;
            Vector3 end = Muzzle.position + direction * LaserRange;
            foreach (RaycastHit hit in hits)
            {
                if (IsSelf(hit.collider) || hit.distance >= closest)
                    continue;
                closest = hit.distance;
                hitCollider = hit.collider;
                end = hit.distance > 0f ? hit.point : Muzzle.position + direction * hit.distance;
            }

            return end;
        }

        void DrawLaserLine(Vector3 direction, float width, Color color)
        {
            if (!m_AimLine)
                return;

            Vector3 end = Muzzle.position + direction * LaserRange;
            if (Physics.Raycast(Muzzle.position, direction, out RaycastHit hit, LaserRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !IsSelf(hit.collider))
                end = hit.point;

            m_AimLine.SetStyle(width, color);
            m_AimLine.SetSegment(Muzzle.position, end);
        }

        void HideAimLine()
        {
            if (m_AimLine)
                m_AimLine.Hide();
        }

        // ---------- pancada ----------

        void EnterSlam()
        {
            EnterState(State.SlamWarning, CombatTiming.Warning(SlamWarning));
            ThreatSignals.Report(transform, CombatTiming.Warning(SlamWarning) + 0.3f);
            m_Agent.isStopped = true;
            m_Agent.ResetPath();
        }

        void UpdateSlamWarning(GameObject player)
        {
            float progress = Mathf.Clamp01(m_StateTime / m_StateDuration);
            float blink = 0.5f + 0.5f * Mathf.Sin(m_StateTime * Mathf.Lerp(12f, 40f, progress));

            if (Torso)
                Torso.localPosition = m_TorsoBasePosition + Vector3.up * (k_TorsoLift * progress);
            if (m_SlamRing)
            {
                m_SlamRing.SetStyle(Mathf.Lerp(0.1f, 0.3f, progress), SlamColor * (0.5f + 0.5f * blink));
                m_SlamRing.SetCircle(transform.position, SlamRadius);
            }

            if (m_StateTime < m_StateDuration)
                return;

            if (Torso)
                Torso.localPosition = m_TorsoBasePosition;
            if (m_SlamRing)
                m_SlamRing.Hide();

            Vector3 offset = player.transform.position - transform.position;
            if (Mathf.Abs(offset.y) < 3f && new Vector2(offset.x, offset.z).magnitude <= SlamRadius)
            {
                var damageable = player.GetComponent<Damageable>();
                if (damageable)
                    CombatEvents.ApplyDamage(damageable, SlamDamage, gameObject, player.transform.position + Vector3.up,
                        false, true);
            }

            SpawnImpact(transform.position, SlamRadius / 4f);
            if (SlamSound)
                AudioUtility.CreateSFX(SlamSound, transform.position, AudioUtility.AudioGroups.Impact, 1f, 10f);

            m_NextSlamTime = Time.time + CombatTiming.Scaled(SlamCooldown);
            EnterState(State.Move, 0f);
        }

        // ---------- utilidades ----------

        void EnterState(State state, float duration)
        {
            CurrentState = state;
            m_StateTime = 0f;
            m_StateDuration = duration;
        }

        void TurnTorso(Vector3 target)
        {
            Transform pivot = Torso ? Torso : transform;
            Vector3 direction = Vector3.ProjectOnPlane(target - pivot.position, Vector3.up);
            if (direction.sqrMagnitude < 0.01f)
                return;

            float sharpness = TurnSharpness * TimeManager.CurrentMultiplier;
            pivot.rotation = Quaternion.Slerp(pivot.rotation, Quaternion.LookRotation(direction),
                1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // Peito do jogador (o pivô do Player do Microgame fica nos pés)
        static Vector3 AimPoint(GameObject player) => player.transform.position + Vector3.up * 1.1f;

        float FlatDistance(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        bool IsSelf(Collider other)
        {
            foreach (Collider own in m_SelfColliders)
            {
                if (own == other)
                    return true;
            }

            return false;
        }

        void SpawnImpact(Vector3 position, float scale)
        {
            if (!ImpactVfx)
                return;

            GameObject vfx = Instantiate(ImpactVfx, position, Quaternion.identity);
            vfx.transform.localScale *= Mathf.Max(0.3f, scale);
            Destroy(vfx, 5f);
        }
    }
}
