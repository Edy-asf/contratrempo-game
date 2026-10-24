using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Zangão: inseto voador que atira. Não usa NavMesh nem o EnemyController do Microgame (para poder voltar ao
    // PrefabPool ao morrer); usa o Health/Damageable/Actor do Microgame e a ficha (ZangaoDefinition) pelo EnemyStats.
    // Voo: mantém de HeightMin a HeightMax acima do chão e circula o jogador a OrbitMin..OrbitMax metros, mudando o
    // ângulo em zigue-zague aleatório, com ruído suave; desvia de paredes por raycast, se afasta dos outros Zangões
    // e nunca sai do raio da arena. Tiro: com linha de visão, a cada ShotIntervalMin..Max o ferrão brilha
    // (MuzzleWarning, nunca abaixo de 60%) e dispara um projétil pequeno e rápido; no máximo MaxShooters Zangões
    // atiram ao mesmo tempo, os outros esperam a vez. Velocidade de voo, cadência e projétil seguem o multiplicador.
    [RequireComponent(typeof(Health))]
    public class ZangaoAI : MonoBehaviour
    {
        [Tooltip("Objeto visual (trocável por um modelo)")]
        public Transform Visual;

        [Tooltip("Asas que batem (opcional; reatribua ao trocar o modelo)")]
        public Transform[] Wings;

        [Tooltip("Ponta do ferrão: de onde sai o tiro")]
        public Transform Muzzle;

        [Tooltip("Brilho de aviso no ferrão (ligado só durante o aviso)")]
        public Transform MuzzleGlow;

        public ZangaoProjectile ProjectilePrefab;

        public AudioClip ShotSound;

        [Tooltip("Renderers que piscam ao levar dano")]
        public Renderer[] FlashRenderers;

        const float k_ChestHeight = 1.1f;
        const float k_GroundProbeInterval = 0.25f;
        const float k_WingFrequency = 28f;
        const float k_FlashDuration = 0.1f;
        const float k_ReleaseDelay = 0.05f;

        static readonly List<ZangaoAI> s_All = new List<ZangaoAI>();
        static readonly RaycastHit[] s_GroundHits = new RaycastHit[8];
        static int s_Shooters;
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        // Zangões ativos agora (teste de desempenho)
        public static int ActiveCount => s_All.Count;

        ZangaoDefinition m_Definition;
        EnemyStats m_Stats;
        Health m_Health;
        Transform m_Player;
        Vector3 m_Velocity;
        Vector3 m_Target;
        Vector3 m_ArenaCenter;
        float m_ArenaRadius = float.MaxValue;
        float m_OrbitAngle;
        float m_OrbitRadius;
        float m_Height;
        float m_GroundY;
        float m_NextGroundProbe;
        float m_NextZigzag;
        float m_NoiseSeed;
        float m_ShotTimer;
        float m_WarningTimer;
        float m_FlashTimer;
        float m_ReleaseTimer = -1f;
        bool m_HoldsShooterSlot;
        float m_WingPhase;
        Quaternion[] m_WingRest;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Stats = GetComponent<EnemyStats>();
            m_Definition = m_Stats ? m_Stats.Definition as ZangaoDefinition : null;
            if (!m_Definition)
                m_Definition = ScriptableObject.CreateInstance<ZangaoDefinition>();
            m_Block = new MaterialPropertyBlock();

            if (Wings != null)
            {
                m_WingRest = new Quaternion[Wings.Length];
                for (int i = 0; i < Wings.Length; i++)
                    m_WingRest[i] = Wings[i] ? Wings[i].localRotation : Quaternion.identity;
            }

            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += (amount, source) => m_FlashTimer = k_FlashDuration;
        }

        // Também roda ao sair do pool: prepara uma vida nova
        void OnEnable()
        {
            s_All.Add(this);
            ResetDeathFlag(m_Health);
            if (m_Health.CurrentHealth <= 0f)
                m_Health.CurrentHealth = m_Health.MaxHealth;
            m_ReleaseTimer = -1f;
            m_Velocity = Vector3.up * 2f;
            m_NoiseSeed = Random.value * 100f;
            m_OrbitAngle = Random.Range(0f, 360f);
            m_OrbitRadius = Random.Range(m_Definition.OrbitMin, m_Definition.OrbitMax);
            m_Height = Random.Range(m_Definition.HeightMin, m_Definition.HeightMax);
            m_NextZigzag = 0f;
            m_NextGroundProbe = 0f;
            m_GroundY = transform.position.y;
            m_ShotTimer = Random.Range(0.5f, m_Definition.ShotIntervalMax);
            m_WarningTimer = -1f;
            SetGlow(false, 0f);
            FindArena();
        }

        void OnDisable()
        {
            s_All.Remove(this);
            ReleaseShooterSlot();
        }

        void FindArena()
        {
            RoomManager room = RoomManager.Active;
            if (!room)
            {
                float best = float.MaxValue;
                foreach (RoomManager candidate in FindObjectsByType<RoomManager>(FindObjectsInactive.Exclude))
                {
                    float distance = Vector3.Distance(candidate.transform.position, transform.position);
                    if (distance < best)
                    {
                        best = distance;
                        room = candidate;
                    }
                }
            }

            if (!room)
            {
                m_ArenaRadius = float.MaxValue;
                return;
            }

            m_ArenaCenter = room.transform.position;
            var capsule = room.GetComponent<CapsuleCollider>();
            m_ArenaRadius = capsule ? capsule.radius + 2f : 40f;
        }

        void Update()
        {
            if (m_ReleaseTimer >= 0f)
            {
                m_ReleaseTimer -= Time.deltaTime;
                if (m_ReleaseTimer < 0f)
                    PrefabPool.Release(gameObject);
                return;
            }

            if (!m_Player)
            {
                var player = FindAnyObjectByType<PlayerCharacterController>();
                m_Player = player ? player.transform : null;
                if (!m_Player)
                    return;
            }

            float multiplier = TimeManager.CurrentMultiplier;
            UpdateFlight(multiplier);
            UpdateAttack(multiplier);
            UpdateVisual(multiplier);
        }

        // ---------- voo ----------

        void UpdateFlight(float multiplier)
        {
            ZangaoDefinition d = m_Definition;
            float dt = Time.deltaTime;

            if (Time.time >= m_NextGroundProbe)
            {
                m_NextGroundProbe = Time.time + k_GroundProbeInterval;
                ProbeGround();
            }

            // Zigue-zague: de tempos em tempos, muda o ângulo em volta do jogador para um lado ou o outro
            if (Time.time >= m_NextZigzag)
            {
                m_NextZigzag = Time.time + CombatTiming.Scaled(Random.Range(d.ZigzagIntervalMin, d.ZigzagIntervalMax));
                m_OrbitAngle += Random.Range(0.4f, 1f) * d.ZigzagAngle * (Random.value < 0.5f ? -1f : 1f);
                m_OrbitRadius = Random.Range(d.OrbitMin, d.OrbitMax);
                m_Height = Random.Range(d.HeightMin, d.HeightMax);
            }

            Vector3 around = Quaternion.Euler(0f, m_OrbitAngle, 0f) * Vector3.forward * m_OrbitRadius;
            m_Target = m_Player.position + around;
            m_Target.y = m_GroundY + m_Height;

            float speed = d.FlySpeed * CombatTiming.Movement(m_Stats);
            Vector3 desired = Vector3.ClampMagnitude(m_Target - transform.position, speed);

            // Ruído suave (Perlin), para o voo não ficar mecânico
            float t = Time.time * 0.8f + m_NoiseSeed;
            desired += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, (Mathf.PerlinNoise(0f, t) - 0.5f) * 0.5f,
                Mathf.PerlinNoise(t, t) - 0.5f) * (2f * d.NoiseStrength);

            desired += Separation(d.SeparationRadius) * speed;
            desired = Avoid(desired, d.AvoidDistance);
            desired = KeepInArena(desired, speed);

            m_Velocity = Vector3.MoveTowards(m_Velocity, desired, d.Acceleration * multiplier * dt);
            transform.position += m_Velocity * dt;

            // Limite duro da arena (o KeepInArena já puxa de volta antes)
            Vector3 offset = transform.position - m_ArenaCenter;
            offset.y = 0f;
            if (offset.magnitude > m_ArenaRadius)
            {
                Vector3 clamped = m_ArenaCenter + offset.normalized * m_ArenaRadius;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
            }

            // Altura sempre dentro da faixa acima do chão (HeightMin a HeightMax); dentro dela, alterna de altura pelo
            // zigue-zague e pelo ruído
            // (fora da faixa, como ao surgir ou quando o chão muda de altura, volta suave, na velocidade de voo)
            float y = transform.position.y;
            float clampedY = Mathf.Clamp(y, m_GroundY + d.HeightMin, m_GroundY + d.HeightMax);
            if (!Mathf.Approximately(y, clampedY))
                y = Mathf.MoveTowards(y, clampedY, Mathf.Max(speed, d.FlySpeed) * dt);
            if (!Mathf.Approximately(y, transform.position.y))
            {
                transform.position = new Vector3(transform.position.x, y, transform.position.z);
                m_Velocity.y = 0f;
            }
        }

        // Altura do chão logo abaixo: o primeiro acerto que não é inimigo (nem este Zangão) nem o jogador. Sem
        // chão (vão), mantém a última altura conhecida.
        void ProbeGround()
        {
            int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, s_GroundHits, 60f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_GroundHits[i];
                if (hit.distance >= best || hit.collider.GetComponentInParent<EnemyStats>() ||
                    hit.collider.GetComponentInParent<PlayerCharacterController>())
                    continue;
                best = hit.distance;
                m_GroundY = hit.point.y;
            }
        }

        // Afasta dos Zangões mais próximos que SeparationRadius (lista pequena: até algumas dezenas)
        Vector3 Separation(float radius)
        {
            Vector3 push = Vector3.zero;
            float radiusSqr = radius * radius;
            Vector3 position = transform.position;
            foreach (ZangaoAI other in s_All)
            {
                if (other == this)
                    continue;
                Vector3 offset = position - other.transform.position;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr < radiusSqr && distanceSqr > 0.0001f)
                    push += offset / Mathf.Sqrt(distanceSqr) * (1f - Mathf.Sqrt(distanceSqr) / radius);
            }

            return Vector3.ClampMagnitude(push, 1f);
        }

        // Um raycast à frente: se vai bater, troca a direção pela que escorrega na parede (e sobe um pouco)
        Vector3 Avoid(Vector3 desired, float distance)
        {
            if (desired.sqrMagnitude < 0.01f)
                return desired;

            Vector3 direction = desired.normalized;
            if (!Physics.Raycast(transform.position, direction, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return desired;
            if (hit.collider.GetComponentInParent<PlayerCharacterController>())
                return desired;

            Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal).normalized + hit.normal * 0.6f + Vector3.up * 0.3f;
            return slide.normalized * desired.magnitude;
        }

        // Fora do raio da arena, volta para dentro
        Vector3 KeepInArena(Vector3 desired, float speed)
        {
            Vector3 fromCenter = transform.position - m_ArenaCenter;
            fromCenter.y = 0f;
            float distance = fromCenter.magnitude;
            if (distance <= m_ArenaRadius * 0.85f)
                return desired;

            Vector3 inward = -fromCenter / Mathf.Max(0.01f, distance);
            float strength = Mathf.InverseLerp(m_ArenaRadius * 0.85f, m_ArenaRadius, distance);
            return Vector3.Lerp(desired, inward * speed, strength);
        }

        // ---------- tiro ----------

        void UpdateAttack(float multiplier)
        {
            ZangaoDefinition d = m_Definition;
            Vector3 aimPoint = m_Player.position + Vector3.up * k_ChestHeight;

            if (m_WarningTimer >= 0f)
            {
                m_WarningTimer -= Time.deltaTime;
                float warning = CombatTiming.Warning(d.MuzzleWarning);
                SetGlow(true, 1f - Mathf.Clamp01(m_WarningTimer / warning));
                if (m_WarningTimer < 0f)
                {
                    SetGlow(false, 0f);
                    Fire(aimPoint);
                    ReleaseShooterSlot();
                    m_ShotTimer = CombatTiming.Scaled(Random.Range(d.ShotIntervalMin, d.ShotIntervalMax));
                }
                return;
            }

            m_ShotTimer -= Time.deltaTime;
            if (m_ShotTimer > 0f)
                return;

            // Pronto: precisa de vaga entre os atiradores e linha de visão; senão, tenta de novo daqui a pouco
            if (s_Shooters >= d.MaxShooters || !HasLineOfSight(aimPoint, d.AttackRange))
            {
                m_ShotTimer = 0.2f;
                return;
            }

            s_Shooters++;
            m_HoldsShooterSlot = true;
            m_WarningTimer = CombatTiming.Warning(d.MuzzleWarning);
            ThreatSignals.Report(transform, m_WarningTimer + 0.4f);
        }

        bool HasLineOfSight(Vector3 aimPoint, float range)
        {
            Vector3 origin = Muzzle ? Muzzle.position : transform.position;
            Vector3 toPlayer = aimPoint - origin;
            if (toPlayer.magnitude > range)
                return false;
            if (!Physics.Raycast(origin, toPlayer.normalized, out RaycastHit hit, toPlayer.magnitude + 0.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.GetComponentInParent<PlayerCharacterController>() ||
                   hit.collider.GetComponentInParent<ZangaoAI>();
        }

        void Fire(Vector3 aimPoint)
        {
            if (!ProjectilePrefab)
                return;

            Vector3 origin = Muzzle ? Muzzle.position : transform.position;
            float damage = m_Stats ? m_Stats.Damage : m_Definition.Damage;
            float speed = m_Definition.ProjectileSpeed > 0f ? m_Definition.ProjectileSpeed : 40f;
            ZangaoProjectile projectile = PrefabPool.Get(ProjectilePrefab, origin, Quaternion.identity);
            projectile.Launch(gameObject, aimPoint - origin, speed, damage, m_Definition.ProjectileLifetime);

            if (ShotSound)
                AudioUtility.CreateSFX(ShotSound, origin, AudioUtility.AudioGroups.EnemyAttack, 1f, 3f);
        }

        void ReleaseShooterSlot()
        {
            if (!m_HoldsShooterSlot)
                return;
            m_HoldsShooterSlot = false;
            s_Shooters = Mathf.Max(0, s_Shooters - 1);
        }

        // ---------- visual ----------

        void UpdateVisual(float multiplier)
        {
            // Olha para o jogador durante o aviso; senão, para onde voa
            Vector3 look = m_WarningTimer >= 0f ? m_Player.position + Vector3.up * k_ChestHeight - transform.position
                : m_Velocity;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look),
                    1f - Mathf.Exp(-10f * multiplier * Time.deltaTime));

            if (Wings != null && m_WingRest != null)
            {
                m_WingPhase += Time.deltaTime * k_WingFrequency * multiplier;
                float flap = Mathf.Sin(m_WingPhase) * 35f;
                for (int i = 0; i < Wings.Length; i++)
                {
                    if (Wings[i])
                        Wings[i].localRotation = m_WingRest[i] * Quaternion.Euler(0f, 0f, i % 2 == 0 ? flap : -flap);
                }
            }

            if (m_FlashTimer > -1f)
            {
                m_FlashTimer -= Time.deltaTime;
                Color emission = m_FlashTimer > 0f ? Color.white * 3f : Color.black;
                SetFlash(emission);
                if (m_FlashTimer <= 0f)
                    m_FlashTimer = -1f;
            }
        }

        void SetFlash(Color emission)
        {
            if (FlashRenderers == null)
                return;
            foreach (Renderer renderer in FlashRenderers)
            {
                if (!renderer)
                    continue;
                if (emission == Color.black)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_EmissionColor, emission);
                renderer.SetPropertyBlock(m_Block);
            }
        }

        void SetGlow(bool on, float progress)
        {
            if (!MuzzleGlow)
                return;
            MuzzleGlow.gameObject.SetActive(on);
            MuzzleGlow.localScale = Vector3.one * (0.18f * Mathf.Lerp(0.4f, 1.3f, progress));
        }

        // ---------- morte ----------

        void OnDie()
        {
            ReleaseShooterSlot();
            SetGlow(false, 0f);
            SetFlash(Color.black);
            // A explosão vem do HitReaction (evento central); volta ao pool logo depois
            m_ReleaseTimer = k_ReleaseDelay;
        }

        // O Health do Microgame só morre uma vez (campo privado); ao reaproveitar a instância, o pool precisa
        // liberar a próxima morte
        static readonly System.Reflection.FieldInfo s_IsDead = typeof(Health).GetField("m_IsDead",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        static void ResetDeathFlag(Health health)
        {
            if (health && s_IsDead != null)
                s_IsDead.SetValue(health, false);
        }
    }
}
