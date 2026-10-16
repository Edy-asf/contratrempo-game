using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Vagante: humanoide que anda a esmo pela arena e atira em rajadas, inclusive andando.
    // Movimento: sorteia um ponto do NavMesh a WanderMin..Max metros, dentro da arena (nunca sai dela), anda até lá,
    // para PauseMin..Max segundos e repete. Ataque: com linha de visão e até AttackRange, o cano brilha
    // (MuzzleWarning, nunca abaixo de 60% com o multiplicador) e dispara ShotsPerBurst tiros com dispersão leve;
    // depois espera BurstIntervalMin..Max. Movimento, cadência e projétil seguem o multiplicador de tempo.
    // Valores da ficha (VaganteDefinition); os campos abaixo valem sem ficha.
    [RequireComponent(typeof(EnemyController))]
    public class VaganteAI : MonoBehaviour
    {
        enum MoveState
        {
            Walking,
            Paused,
        }

        enum AttackState
        {
            Ready,
            Warning,
            Firing,
            Cooldown,
        }

        [Tooltip("Parte que gira para mirar a arma (fica dentro do visual; reatribua ao trocar o modelo)")]
        public Transform AimPivot;

        [Tooltip("Ponta do cano: de onde saem os tiros")]
        public Transform Muzzle;

        [Tooltip("Brilho de aviso no cano (ligado só durante o aviso)")]
        public Transform MuzzleGlow;

        public AudioClip ShotSound;

        [Header("Movimento (sem ficha)")]
        public float MoveSpeed = 3.5f;
        public float WanderMinDistance = 8f;
        public float WanderMaxDistance = 15f;
        public float PauseMin = 1f;
        public float PauseMax = 2f;

        [Header("Rajada (sem ficha)")]
        public float AttackRange = 35f;
        public float MuzzleWarning = 0.3f;
        public int ShotsPerBurst = 3;
        public float TimeBetweenShots = 0.15f;
        public float SpreadMin = 3f;
        public float SpreadMax = 5f;
        public float BurstIntervalMin = 2f;
        public float BurstIntervalMax = 3f;

        const float k_ChestHeight = 1.1f;
        const float k_GlowSize = 0.35f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        WeaponController m_Weapon;
        ActorsManager m_ActorsManager;
        Vector3 m_ArenaCenter;
        float m_ArenaRadius = float.MaxValue;

        MoveState m_MoveState = MoveState.Paused;
        float m_MoveTimer;
        AttackState m_AttackState = AttackState.Cooldown;
        float m_AttackTimer;
        int m_ShotsLeft;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_Weapon = GetComponentInChildren<WeaponController>(true);
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();

            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition is VaganteDefinition vagante)
            {
                MoveSpeed = vagante.MoveSpeed;
                WanderMinDistance = vagante.WanderMinDistance;
                WanderMaxDistance = vagante.WanderMaxDistance;
                PauseMin = vagante.PauseMin;
                PauseMax = vagante.PauseMax;
                AttackRange = vagante.AttackRange;
                MuzzleWarning = vagante.MuzzleWarning;
                ShotsPerBurst = vagante.ShotsPerBurst;
                TimeBetweenShots = vagante.TimeBetweenShots;
                SpreadMin = vagante.SpreadMin;
                SpreadMax = vagante.SpreadMax;
                BurstIntervalMin = vagante.BurstIntervalMin;
                BurstIntervalMax = vagante.BurstIntervalMax;
            }

            // Antes do EnemyTimeScaler (ordem 100) guardar a velocidade base
            m_Agent.speed = MoveSpeed;
            FindArena();

            if (MuzzleGlow)
                MuzzleGlow.gameObject.SetActive(false);
            m_MoveTimer = Random.Range(PauseMin, PauseMax);
            m_AttackTimer = Random.Range(0.5f, BurstIntervalMin);

            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };
        }

        // A arena é a sala (RoomManager) mais próxima; o raio é o do trigger dela
        void FindArena()
        {
            float best = float.MaxValue;
            foreach (RoomManager room in FindObjectsByType<RoomManager>(FindObjectsInactive.Exclude))
            {
                float distance = Vector3.Distance(room.transform.position, transform.position);
                if (distance >= best)
                    continue;
                best = distance;
                m_ArenaCenter = room.transform.position;
                var capsule = room.GetComponent<CapsuleCollider>();
                m_ArenaRadius = capsule ? capsule.radius + 6f : 50f;
            }
        }

        void Update()
        {
            float multiplier = TimeManager.CurrentMultiplier;
            UpdateMovement(multiplier);

            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (player)
                UpdateAttack(player, multiplier);
        }

        // ---------- movimento ----------

        void UpdateMovement(float multiplier)
        {
            if (m_MoveState == MoveState.Paused)
            {
                m_MoveTimer -= Time.deltaTime * multiplier;
                if (m_MoveTimer <= 0f && TryPickDestination(out Vector3 destination))
                {
                    m_Agent.isStopped = false;
                    m_Controller.SetNavDestination(destination);
                    m_MoveState = MoveState.Walking;
                }
                return;
            }

            bool arrived = !m_Agent.pathPending &&
                           (m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.3f || !m_Agent.hasPath);
            if (arrived)
            {
                m_Agent.isStopped = true;
                m_MoveState = MoveState.Paused;
                m_MoveTimer = Random.Range(PauseMin, PauseMax);
            }
        }

        // Ponto alcançável do NavMesh a WanderMin..Max metros, dentro do raio da arena
        bool TryPickDestination(out Vector3 destination)
        {
            var path = new NavMeshPath();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                float distance = Random.Range(WanderMinDistance, WanderMaxDistance);
                Vector3 candidate = transform.position + new Vector3(direction.x, 0f, direction.y) * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    continue;
                Vector3 fromCenter = hit.position - m_ArenaCenter;
                fromCenter.y = 0f;
                if (fromCenter.magnitude > m_ArenaRadius)
                    continue;
                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    continue;

                destination = hit.position;
                return true;
            }

            destination = transform.position;
            return false;
        }

        // ---------- ataque ----------

        void UpdateAttack(GameObject player, float multiplier)
        {
            Vector3 aimPoint = player.transform.position + Vector3.up * k_ChestHeight;
            bool canShoot = m_Controller.IsSeeingTarget &&
                            Vector3.Distance(transform.position, player.transform.position) <= AttackRange;
            if (canShoot)
                AimAt(aimPoint, multiplier);

            m_AttackTimer -= Time.deltaTime;
            switch (m_AttackState)
            {
                case AttackState.Cooldown:
                    if (m_AttackTimer <= 0f)
                        m_AttackState = AttackState.Ready;
                    break;

                case AttackState.Ready:
                    if (!canShoot)
                        break;
                    m_AttackState = AttackState.Warning;
                    ThreatSignals.Report(transform, CombatTiming.Warning(MuzzleWarning) + 0.6f);
                    m_AttackTimer = CombatTiming.Warning(MuzzleWarning);
                    SetGlow(true, 0f);
                    break;

                case AttackState.Warning:
                    SetGlow(true, 1f - Mathf.Clamp01(m_AttackTimer / CombatTiming.Warning(MuzzleWarning)));
                    if (m_AttackTimer <= 0f)
                    {
                        SetGlow(false, 0f);
                        m_AttackState = AttackState.Firing;
                        m_ShotsLeft = ShotsPerBurst;
                        m_AttackTimer = 0f;
                    }
                    break;

                case AttackState.Firing:
                    if (m_AttackTimer > 0f)
                        break;
                    Fire(aimPoint);
                    m_ShotsLeft--;
                    m_AttackTimer = CombatTiming.Scaled(TimeBetweenShots);
                    if (m_ShotsLeft <= 0)
                    {
                        m_AttackState = AttackState.Cooldown;
                        m_AttackTimer = CombatTiming.Scaled(Random.Range(BurstIntervalMin, BurstIntervalMax));
                    }
                    break;
            }
        }

        void AimAt(Vector3 aimPoint, float multiplier)
        {
            if (!AimPivot)
                return;
            Vector3 direction = aimPoint - AimPivot.position;
            if (direction.sqrMagnitude > 0.01f)
                AimPivot.rotation = Quaternion.Slerp(AimPivot.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-12f * multiplier * Time.deltaTime));
        }

        // Um tiro com dispersão leve: o projétil é o template do EnemyTimeScaler (dano e velocidade da ficha)
        void Fire(Vector3 aimPoint)
        {
            if (!m_Weapon || !m_Weapon.ProjectilePrefab)
                return;

            Vector3 origin = Muzzle ? Muzzle.position : transform.position + Vector3.up * 1.4f;
            Quaternion aim = Quaternion.LookRotation(aimPoint - origin);
            float spread = Random.Range(SpreadMin, SpreadMax);
            Quaternion offset = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward) *
                                Quaternion.AngleAxis(spread, Vector3.right);
            ProjectileBase projectile = Instantiate(m_Weapon.ProjectilePrefab, origin, aim * offset);
            projectile.Shoot(m_Weapon);

            if (ShotSound)
                AudioUtility.CreateSFX(ShotSound, origin, AudioUtility.AudioGroups.EnemyAttack, 1f, 5f);
        }

        void SetGlow(bool on, float progress)
        {
            if (!MuzzleGlow)
                return;
            MuzzleGlow.gameObject.SetActive(on);
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 40f);
            MuzzleGlow.localScale = Vector3.one * (k_GlowSize * Mathf.Lerp(0.4f, 1.2f, progress) * pulse);
        }
    }
}
