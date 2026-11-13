using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Vagante (Explorador Tomado): humanoide que anda a esmo pela arena e atira em rajadas, inclusive andando.
    // Movimento: sorteia um ponto do NavMesh a WanderMin..Max metros, dentro da arena (nunca sai dela), anda até lá,
    // para PauseMin..Max segundos e repete. Ataque: com linha de visão e até AttackRange, o cano carrega
    // (MuzzleWarning, nunca abaixo de 60% com o multiplicador) e dispara ShotsPerBurst tiros com dispersão leve;
    // depois espera BurstIntervalMin..Max. Movimento, cadência e projétil seguem o multiplicador de tempo.
    // Tiro em pixel art (EnemyBolt, plasma ciano com rastro em degraus), carga no cano (EnemyCharge) e sons gerados
    // em código (EnemySounds). Recuo: depois de cada rajada, com o jogador a menos de KiteTrigger, recua KiteDistance
    // mancando, sem dar as costas. Morteiro: sem ver o jogador por MortarHiddenTime (atrás de cobertura), o
    // cogumelo do visor acende (MortarWarning) e ele lança uma bola de esporos em arco até onde o jogador está
    // (EnemyMortar: marca no chão durante o voo, explosão em área).
    // Valores da ficha (VaganteDefinition); os campos abaixo valem sem ficha.
    [RequireComponent(typeof(EnemyController))]
    public class VaganteAI : MonoBehaviour, ISpriteActionSource
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
            MortarWarning,
            MortarRecoil,
        }

        [Tooltip("Parte que gira para mirar a arma (fica dentro do visual; reatribua ao trocar o modelo)")]
        public Transform AimPivot;

        [Tooltip("Ponta do cano: de onde saem os tiros")]
        public Transform Muzzle;

        [Tooltip("Brilho de aviso no cano (opcional; a carga em pixel art já avisa)")]
        public Transform MuzzleGlow;

        [Tooltip("Onde fica o cogumelo do visor, de onde sai o morteiro (local, a partir dos pés)")]
        public Vector3 MortarPoint = new Vector3(0f, 1.75f, 0.15f);

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
        public float ProjectileLifetime = 2.5f;

        [Header("Recuo e morteiro (sem ficha)")]
        public float KiteTrigger = 8f;
        public float KiteDistance = 6f;
        public float KiteSpeed = 0.8f;
        public float MortarHiddenTime = 3f;
        public float MortarRange = 40f;
        public float MortarWarning = 0.6f;
        public float MortarFlightTime = 1.2f;
        public float MortarRadius = 3f;
        public float MortarDamage = 2.5f;
        public float MortarCooldown = 7f;

        const float k_ChestHeight = 1.1f;
        const float k_GlowSize = 0.35f;
        const float k_DefaultDamage = 6f;
        const float k_DefaultSpeed = 24f;
        const float k_RecoilTime = 0.35f;

        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        EnemyStats m_Stats;
        ActorsManager m_ActorsManager;
        EnemyCharge m_Charge;
        EnemyCharge m_MortarCharge;
        Vector3 m_ArenaCenter;
        float m_ArenaRadius = float.MaxValue;

        MoveState m_MoveState = MoveState.Paused;
        float m_MoveTimer;
        AttackState m_AttackState = AttackState.Cooldown;
        float m_AttackTimer;
        float m_StateDuration = 1f;
        int m_ShotsLeft;
        bool m_HasSeen;
        float m_LastSeen;
        float m_MortarTimer;
        bool m_Kiting;

        // Para o sprite (EnemySpriteView): cano carregando no aviso, coice na rajada, cogumelo acendendo e o
        // lançamento do morteiro (segundo ataque); senão parado ou andando
        public SpriteAction SpriteAction => m_AttackState switch
        {
            AttackState.Warning => SpriteAction.Windup,
            AttackState.Firing => SpriteAction.Attack,
            AttackState.MortarWarning => SpriteAction.Windup2,
            AttackState.MortarRecoil => SpriteAction.Attack2,
            _ => m_Agent && m_Agent.velocity.sqrMagnitude > 0.04f ? SpriteAction.Move : SpriteAction.Idle,
        };

        public float SpriteActionProgress => m_AttackState == AttackState.Warning ||
                                             m_AttackState == AttackState.MortarWarning ||
                                             m_AttackState == AttackState.MortarRecoil
            ? 1f - Mathf.Clamp01(m_AttackTimer / Mathf.Max(0.0001f, m_StateDuration))
            : -1f;

        Vector3 MuzzlePosition => Muzzle ? Muzzle.position : transform.position + Vector3.up * 1.4f;

        Vector3 MortarOrigin => transform.TransformPoint(MortarPoint);

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();

            m_Stats = GetComponent<EnemyStats>();
            if (m_Stats && m_Stats.Definition is VaganteDefinition vagante)
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
                ProjectileLifetime = vagante.ProjectileLifetime;
                KiteTrigger = vagante.KiteTrigger;
                KiteDistance = vagante.KiteDistance;
                KiteSpeed = vagante.KiteSpeed;
                MortarHiddenTime = vagante.MortarHiddenTime;
                MortarRange = vagante.MortarRange;
                MortarWarning = vagante.MortarWarning;
                MortarFlightTime = vagante.MortarFlightTime;
                MortarRadius = vagante.MortarRadius;
                MortarDamage = vagante.MortarDamage;
                MortarCooldown = vagante.MortarCooldown;
            }

            // Antes do EnemyTimeScaler (ordem 100) guardar a velocidade base
            m_Agent.speed = MoveSpeed;
            FindArena();

            if (MuzzleGlow)
                MuzzleGlow.gameObject.SetActive(false);
            m_Charge = EnemyCharge.Create(Muzzle ? Muzzle : transform, EnemyFxStyle.Plasma,
                Muzzle ? Vector3.zero : Vector3.up * 1.4f);
            m_MortarCharge = EnemyCharge.Create(transform, EnemyFxStyle.Spore, MortarPoint);
            m_MoveTimer = Random.Range(PauseMin, PauseMax);
            m_AttackTimer = Random.Range(0.5f, BurstIntervalMin);
            m_MortarTimer = CombatTiming.Scaled(MortarCooldown * 0.5f);

            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };
        }

        void OnDestroy()
        {
            if (m_Kiting)
                EndKite();
        }

        // A arena é a sala (RoomManager) mais próxima: o domo dela (com folga para não andar colado na parede) ou, sem
        // domo, o raio do trigger
        void FindArena()
        {
            float best = float.MaxValue;
            foreach (RoomManager room in FindObjectsByType<RoomManager>(FindObjectsInactive.Exclude))
            {
                float distance = Vector3.Distance(room.transform.position, transform.position);
                if (distance >= best)
                    continue;
                best = distance;
                if (room.Dome)
                {
                    m_ArenaCenter = room.Dome.Center;
                    m_ArenaRadius = room.Dome.Radius - 3f;
                    continue;
                }

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
            if (!player)
                return;
            UpdateAttack(player, multiplier);
            if (m_Kiting)
                FaceTowards(player.transform.position, multiplier);
        }

        // ---------- movimento ----------

        void UpdateMovement(float multiplier)
        {
            if (m_MoveState == MoveState.Paused)
            {
                m_MoveTimer -= CombatTiming.DeltaTime * multiplier;
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
                if (m_Kiting)
                    EndKite();
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
                if (TryReach(candidate, 3f, path, out destination))
                    return true;
            }

            destination = transform.position;
            return false;
        }

        // Ponto do NavMesh perto de "candidate", dentro da arena e com caminho completo até ele
        bool TryReach(Vector3 candidate, float sampleRadius, NavMeshPath path, out Vector3 destination)
        {
            destination = transform.position;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                return false;
            Vector3 fromCenter = hit.position - m_ArenaCenter;
            fromCenter.y = 0f;
            if (fromCenter.magnitude > m_ArenaRadius)
                return false;
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                return false;
            destination = hit.position;
            return true;
        }

        // ---------- recuo ----------

        // Recua para longe do jogador (com um desvio sorteado), mais devagar e de frente para ele
        void StartKite(Vector3 playerPosition)
        {
            Vector3 away = transform.position - playerPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -transform.forward;
            away.Normalize();

            var path = new NavMeshPath();
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float spread = attempt < 5 ? 35f : 75f;
                Vector3 direction = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * away;
                if (!TryReach(transform.position + direction * KiteDistance, 2f, path, out Vector3 destination))
                    continue;

                m_Kiting = true;
                m_Agent.updateRotation = false;
                if (m_Stats)
                    m_Stats.MoveMultiplier *= KiteSpeed;
                m_Agent.isStopped = false;
                m_Controller.SetNavDestination(destination);
                m_MoveState = MoveState.Walking;
                return;
            }
        }

        void EndKite()
        {
            m_Kiting = false;
            if (m_Agent)
                m_Agent.updateRotation = true;
            if (m_Stats)
                m_Stats.MoveMultiplier /= Mathf.Max(0.01f, KiteSpeed);
        }

        void FaceTowards(Vector3 target, float multiplier)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                1f - Mathf.Exp(-8f * multiplier * CombatTiming.DeltaTime));
        }

        // ---------- ataque ----------

        void UpdateAttack(GameObject player, float multiplier)
        {
            Vector3 aimPoint = player.transform.position + Vector3.up * k_ChestHeight;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            bool seeing = m_Controller.IsSeeingTarget;
            bool canShoot = seeing && distance <= AttackRange;
            if (canShoot)
                AimAt(aimPoint, multiplier);
            if (seeing)
            {
                m_HasSeen = true;
                m_LastSeen = CombatTiming.Now;
            }

            float dt = CombatTiming.DeltaTime;
            m_AttackTimer -= dt;
            m_MortarTimer -= dt;
            switch (m_AttackState)
            {
                case AttackState.Cooldown:
                    if (TryStartMortar(player, distance))
                        break;
                    if (m_AttackTimer <= 0f)
                        m_AttackState = AttackState.Ready;
                    break;

                case AttackState.Ready:
                    if (TryStartMortar(player, distance) || !canShoot)
                        break;
                    m_AttackState = AttackState.Warning;
                    m_StateDuration = m_AttackTimer = CombatTiming.Warning(MuzzleWarning);
                    ThreatSignals.Report(transform, m_StateDuration + 0.6f);
                    SetGlow(true, 0f);
                    // O zumbido dura 0,3 s a 1,0x; o tom sobe junto com o aviso mais curto
                    EnemySounds.Play(EnemySounds.PlasmaCharge, MuzzlePosition, 0.5f, 0.3f / m_StateDuration, 0.02f);
                    break;

                case AttackState.Warning:
                    SetGlow(true, 1f - Mathf.Clamp01(m_AttackTimer / m_StateDuration));
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
                        if (!m_Kiting && distance < KiteTrigger)
                            StartKite(player.transform.position);
                    }
                    break;

                case AttackState.MortarWarning:
                    if (m_MortarCharge)
                        m_MortarCharge.Show(1f - Mathf.Clamp01(m_AttackTimer / m_StateDuration));
                    if (m_AttackTimer <= 0f)
                        LaunchMortar();
                    break;

                case AttackState.MortarRecoil:
                    if (m_AttackTimer <= 0f)
                    {
                        m_AttackState = AttackState.Cooldown;
                        m_AttackTimer = CombatTiming.Scaled(Random.Range(BurstIntervalMin, BurstIntervalMax)) * 0.5f;
                        m_MoveState = MoveState.Paused;
                        m_MoveTimer = 0.2f;
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
                    1f - Mathf.Exp(-12f * multiplier * CombatTiming.DeltaTime));
        }

        // Um tiro de plasma com dispersão leve (dano e velocidade da ficha)
        void Fire(Vector3 aimPoint)
        {
            Vector3 origin = MuzzlePosition;
            Quaternion aim = Quaternion.LookRotation(aimPoint - origin);
            float spread = Random.Range(SpreadMin, SpreadMax);
            Quaternion offset = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward) *
                                Quaternion.AngleAxis(spread, Vector3.right);
            Vector3 direction = aim * offset * Vector3.forward;
            float damage = m_Stats ? m_Stats.Damage : k_DefaultDamage;
            float speed = m_Stats && m_Stats.Definition && m_Stats.Definition.ProjectileSpeed > 0f
                ? m_Stats.Definition.ProjectileSpeed
                : k_DefaultSpeed;
            EnemyBolt.Fire(EnemyFxStyle.Plasma, gameObject, origin, direction, speed, damage, ProjectileLifetime);
            EnemyFxBurst.Play(EnemyFxSprites.Flash(EnemyFxStyle.Plasma), origin, 30f, 1f, Muzzle);
            EnemySounds.Play(EnemySounds.PlasmaShot, origin, 0.85f, 1f, 0.07f, 5f);
        }

        // ---------- morteiro ----------

        // Sem ver o jogador há MortarHiddenTime (depois de já tê-lo visto), com o morteiro pronto e dentro do alcance
        bool TryStartMortar(GameObject player, float distance)
        {
            if (!m_HasSeen || m_MortarTimer > 0f || distance > MortarRange || !BossTarget.Exists)
                return false;
            if (CombatTiming.Now - m_LastSeen < MortarHiddenTime)
                return false;

            m_AttackState = AttackState.MortarWarning;
            m_StateDuration = m_AttackTimer = CombatTiming.Warning(MortarWarning);
            ThreatSignals.Report(transform, m_StateDuration + 0.4f);
            if (m_Kiting)
                EndKite();
            m_Agent.isStopped = true;
            m_MoveState = MoveState.Paused;
            m_MoveTimer = float.MaxValue;
            FaceTowards(player.transform.position, 100f);
            EnemySounds.Play(EnemySounds.PlasmaCharge, MortarOrigin, 0.4f, 0.55f * 0.3f / m_StateDuration, 0.02f);
            return true;
        }

        void LaunchMortar()
        {
            if (m_MortarCharge)
                m_MortarCharge.Hide();
            Vector3 origin = MortarOrigin;
            float damage = (m_Stats ? m_Stats.Damage : k_DefaultDamage) * MortarDamage;
            EnemyMortar.Launch(gameObject, origin, BossTarget.Feet, CombatTiming.Warning(MortarFlightTime),
                MortarRadius, damage);
            EnemyFxBurst.Play(EnemyFxSprites.Flash(EnemyFxStyle.Spore), origin, 24f);
            EnemySounds.Play(EnemySounds.MortarLaunch, origin, 0.9f, 1f, 0.05f, 5f);

            m_MortarTimer = CombatTiming.Scaled(MortarCooldown);
            m_AttackState = AttackState.MortarRecoil;
            m_StateDuration = m_AttackTimer = CombatTiming.Scaled(k_RecoilTime);
        }

        void SetGlow(bool on, float progress)
        {
            if (m_Charge)
            {
                if (on)
                    m_Charge.Show(progress);
                else
                    m_Charge.Hide();
            }

            if (!MuzzleGlow)
                return;
            MuzzleGlow.gameObject.SetActive(on);
            float pulse = 0.8f + 0.2f * Mathf.Sin(CombatTiming.Now * 40f);
            MuzzleGlow.localScale = Vector3.one * (k_GlowSize * Mathf.Lerp(0.4f, 1.2f, progress) * pulse);
        }
    }
}
