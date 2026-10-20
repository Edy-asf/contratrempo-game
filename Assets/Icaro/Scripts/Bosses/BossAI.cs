using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Base dos chefes: liga a ficha (BossDefinition pelo EnemyStats), roda o ciclo de ataques (Brain: escolhe um
    // ataque, espera o intervalo, repete), troca de fase pela vida, mantém distância do jogador circulando em volta
    // dele pelo NavMesh, dá dano de contato a quem encosta e anima o modelo por código no estilo cartoon (flutua,
    // estica e achata, inclina, gira para o jogador). Cada chefe implementa NextAttack. Os ataques usam o BossKit
    // (ondas, projéteis, marcas, poças, feixes); cadências e avisos seguem o multiplicador de tempo
    // (CombatTiming.Scaled e Warning). Usa Health/Damageable/Actor do Microgame, sem EnemyController.
    [RequireComponent(typeof(Health))]
    public abstract class BossAI : MonoBehaviour
    {
        [Tooltip("Modelo (trocável); é ele que a animação procedural move")]
        public Transform Visual;

        [Tooltip("De onde saem os projéteis (boca, máscara...); vazio = centro do corpo")]
        public Transform Muzzle;

        [Tooltip("Renderers que piscam ao levar dano")]
        public Renderer[] FlashRenderers;

        [Tooltip("Raio do corpo para o dano de contato")]
        public float BodyRadius = 2f;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        protected BossDefinition Definition { get; private set; }
        protected BossKit Kit => Definition ? Definition.Kit : null;
        protected EnemyStats Stats { get; private set; }
        protected Health Health { get; private set; }
        protected NavMeshAgent Agent { get; private set; }

        // 1, 2 ou 3
        public int Phase { get; private set; } = 1;

        // Durante um ataque que precisa ficar parado, o chefe não circula
        protected bool HoldPosition { get; set; }

        // Altura extra do visual (flutuar)
        protected float HoverHeight { get; set; }

        readonly List<SweepBeam> m_Beams = new List<SweepBeam>();
        Vector3 m_VisualPosition;
        Vector3 m_VisualScale = Vector3.one;
        Quaternion m_VisualRotation = Quaternion.identity;
        float m_Squash;
        float m_SquashVelocity;
        float m_Lean;
        float m_Flash = -1f;
        float m_NextContact;
        float m_NextRepath;
        float m_OrbitSign = 1f;
        bool m_Dead;
        MaterialPropertyBlock m_Block;

        protected virtual void Awake()
        {
            Health = GetComponent<Health>();
            Stats = GetComponent<EnemyStats>();
            Agent = GetComponent<NavMeshAgent>();
            m_Block = new MaterialPropertyBlock();
            Health.OnDamaged += (amount, source) => m_Flash = 0.1f;
            Health.OnDie += OnDie;
        }

        protected virtual void Start()
        {
            Definition = Stats ? Stats.Definition as BossDefinition : null;
            if (Visual)
            {
                m_VisualPosition = Visual.localPosition;
                m_VisualScale = Visual.localScale;
                m_VisualRotation = Visual.localRotation;
            }

            if (Agent && Definition)
                Agent.speed = Definition.MoveSpeed;
            m_OrbitSign = Random.value < 0.5f ? -1f : 1f;
            if (Definition)
                StartCoroutine(Brain());
        }

        // ---------- ciclo de ataques ----------

        IEnumerator Brain()
        {
            // Entrada: ruge (estica) antes do primeiro ataque
            yield return new WaitForSeconds(0.8f);
            Squash(-0.35f);
            yield return new WaitForSeconds(0.8f);

            while (!m_Dead)
            {
                if (!BossTarget.Exists)
                {
                    yield return null;
                    continue;
                }

                yield return NextAttack();
                HoldPosition = false;
                float gap = Definition.AttackGap * Mathf.Pow(Definition.PhaseGapFactor, Phase - 1);
                yield return Wait(gap);
            }
        }

        // O próximo ataque (o chefe escolhe pela fase e por sorteio)
        protected abstract IEnumerator NextAttack();

        // Chamado ao entrar numa fase nova (2 ou 3)
        protected virtual void OnPhaseChanged(int phase)
        {
            Squash(-0.4f);
            VisorBanner.Show(phase == 2 ? "O chefe se enfurece" : "Fúria final", $"Fase {phase}",
                BannerTone.Danger);
        }

        // ---------- quadro a quadro ----------

        protected virtual void Update()
        {
            if (m_Dead || !Definition)
                return;

            UpdatePhase();
            UpdateMovement();
            UpdateContact();
            Animate();
        }

        void UpdatePhase()
        {
            float ratio = Health.MaxHealth > 0f ? Health.CurrentHealth / Health.MaxHealth : 1f;
            int phase = 1;
            if (ratio <= Definition.PhaseTwoAt)
                phase = 2;
            if (Definition.PhaseThreeAt > 0f && ratio <= Definition.PhaseThreeAt)
                phase = 3;
            if (phase <= Phase)
                return;
            Phase = phase;
            OnPhaseChanged(phase);
        }

        // Circula o jogador na distância preferida (quando não está segurando posição para um ataque)
        void UpdateMovement()
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh || !BossTarget.Exists)
                return;

            Agent.speed = Definition.MoveSpeed * CombatTiming.Movement(Stats);
            if (HoldPosition)
            {
                Agent.isStopped = true;
                FacePlayer(6f);
                return;
            }

            Agent.isStopped = false;
            if (Time.time >= m_NextRepath)
            {
                m_NextRepath = Time.time + 0.4f;
                Vector3 fromPlayer = transform.position - BossTarget.Feet;
                fromPlayer.y = 0f;
                if (fromPlayer.sqrMagnitude < 0.01f)
                    fromPlayer = Vector3.forward;
                Vector3 around = Quaternion.Euler(0f, 30f * m_OrbitSign, 0f) * fromPlayer.normalized;
                Vector3 goal = BossTarget.Feet + around * Definition.PreferredDistance;
                if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    Agent.SetDestination(hit.position);
                if (Random.value < 0.05f)
                    m_OrbitSign = -m_OrbitSign;
            }

            FacePlayer(4f);
        }

        // Encostar no chefe machuca (desencoraja ficar colado nele)
        void UpdateContact()
        {
            if (!BossTarget.Exists || Time.time < m_NextContact || !Stats)
                return;
            if (BossTarget.FlatDistance(transform.position) > BodyRadius + 0.6f)
                return;
            if (BossTarget.Feet.y > transform.position.y + 4f)
                return;
            m_NextContact = Time.time + 1f;
            BossTarget.Damage(Stats.Damage, gameObject, BossTarget.Chest);
        }

        protected void FacePlayer(float sharpness)
        {
            if (!BossTarget.Exists)
                return;
            Vector3 toPlayer = BossTarget.Feet - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
                return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer),
                1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // ---------- animação procedural (cartoon) ----------

        // Positivo achata (pancada, queda); negativo estica (rugido, salto)
        protected void Squash(float amount) => m_SquashVelocity += amount * 12f;

        // Inclina para a frente (positivo) ou para trás, em graus
        protected void Lean(float degrees) => m_Lean = degrees;

        void Animate()
        {
            if (!Visual)
                return;

            // Mola do squash: volta ao normal com um pouco de balanço
            float dt = Time.deltaTime;
            m_SquashVelocity += (-m_Squash * 140f - m_SquashVelocity * 11f) * dt;
            m_Squash += m_SquashVelocity * dt;
            float squash = Mathf.Clamp(m_Squash, -0.5f, 0.5f);
            float bob = Mathf.Sin(Time.time * 2.2f * Mathf.Max(1f, Mathf.Sqrt(TimeManager.CurrentMultiplier))) * 0.06f;

            Visual.localScale = new Vector3(m_VisualScale.x * (1f + squash * 0.5f),
                m_VisualScale.y * (1f - squash + bob), m_VisualScale.z * (1f + squash * 0.5f));
            Visual.localPosition = m_VisualPosition + Vector3.up * (HoverHeight + bob * 2f);
            m_Lean = Mathf.MoveTowards(m_Lean, 0f, 40f * dt);
            Visual.localRotation = m_VisualRotation * Quaternion.Euler(m_Lean, 0f, 0f);

            // Piscar ao levar dano
            if (m_Flash > -1f)
            {
                m_Flash -= dt;
                SetFlash(m_Flash > 0f ? Color.white * 2.5f : Color.black);
                if (m_Flash <= 0f)
                    m_Flash = -1f;
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

        // ---------- utilidades para os ataques ----------

        // Espera um tempo de cadência (encurta com o multiplicador)
        protected static WaitForSeconds Wait(float seconds) => new WaitForSeconds(CombatTiming.Scaled(seconds));

        // Duração de um aviso (encurta com o multiplicador, nunca abaixo de 60%)
        protected static float WarningTime(float seconds) => CombatTiming.Warning(seconds);

        protected Vector3 MuzzlePosition => Muzzle ? Muzzle.position : transform.position + Vector3.up * 2f;

        protected float GroundY => GroundTelegraph.GroundPoint(transform.position).y;

        // Origem dos padrões horizontais (anéis, espirais): sob o chefe, na altura do peito do jogador, para os
        // projéteis passarem por onde ele está (os tiros mirados saem da boca, MuzzlePosition)
        protected Vector3 RingOrigin => new Vector3(transform.position.x, GroundY + 1.2f, transform.position.z);

        protected void Threat(float seconds) => ThreatSignals.Report(transform, seconds);

        protected BossProjectile Shoot(Vector3 from, Vector3 direction, float speed, float damage, float size = 0.6f,
            float homing = 0f, float lifetime = 7f, float impactRadius = 0f) =>
            BossProjectile.Fire(Kit, from, direction, speed, damage, gameObject, Definition.AttackColor, size, homing,
                lifetime, impactRadius);

        // Anel de projéteis no plano horizontal; 'gap' projéteis faltando a partir de 'gapStart' (a brecha)
        protected void Ring(Vector3 center, int count, float speed, float damage, float angleOffset, int gapStart = 0,
            int gap = 0, float size = 0.6f)
        {
            for (int i = 0; i < count; i++)
            {
                int fromGap = (i - gapStart + count) % count;
                if (gap > 0 && fromGap < gap)
                    continue;
                float angle = angleOffset + i * 360f / count;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Shoot(center, direction, speed, damage, size);
            }
        }

        protected Shockwave Wave(Vector3 origin, float speed, float damage) =>
            Shockwave.Spawn(Kit, origin, speed, 60f, damage, gameObject, Definition.AttackColor);

        protected SweepBeam Beam(float length, float height, float speed, float angle, float duration, float damage)
        {
            SweepBeam beam = SweepBeam.Spawn(Kit, transform, GroundY, length, height, speed, angle, duration, damage,
                gameObject, Definition.AttackColor);
            if (beam)
                m_Beams.Add(beam);
            return beam;
        }

        // Algo cai do alto em 'ground' e chega no fim do aviso (a queda acelera com o multiplicador); explode no raio
        protected IEnumerator DropFromSky(Vector3 ground, float warning, float damage, float radius, float size)
        {
            const float height = 14f;
            const float speed = 30f;
            float fall = height / (speed * Kit.SpeedMultiplier);
            yield return new WaitForSeconds(Mathf.Max(0f, warning - fall));
            Shoot(ground + Vector3.up * height, Vector3.down, speed, damage, size, 0f, 3f, radius);
        }

        // Salto em arco até 'target' (sem o NavMesh no ar); estica ao sair e achata ao cair
        protected IEnumerator JumpTo(Vector3 target, float airTime, float height)
        {
            bool agent = Agent && Agent.enabled;
            if (agent)
                Agent.enabled = false;

            Squash(0.35f);
            yield return new WaitForSeconds(0.15f);
            Squash(-0.45f);

            Vector3 start = transform.position;
            float duration = CombatTiming.Scaled(airTime);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                transform.position = Vector3.Lerp(start, target, k) + Vector3.up * (4f * height * k * (1f - k));
                FacePlayer(5f);
                yield return null;
            }

            transform.position = target;
            Squash(0.5f);
            if (agent)
            {
                Agent.enabled = true;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    Agent.Warp(hit.position);
            }
        }

        // Ponto aleatório do NavMesh a 'distance' do jogador (para saltos e teleportes)
        protected Vector3 PointAroundPlayer(float distance)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 candidate = BossTarget.Feet + new Vector3(dir.x, 0f, dir.y) * distance;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    return hit.position;
            }

            return transform.position;
        }

        // ---------- morte ----------

        void OnDie()
        {
            m_Dead = true;
            StopAllCoroutines();
            foreach (SweepBeam beam in m_Beams)
            {
                if (beam && beam.isActiveAndEnabled)
                    beam.Stop();
            }
            m_Beams.Clear();
            if (Agent)
                Agent.enabled = false;
            SetFlash(Color.black);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            // Murcha e some
            Vector3 scale = Visual ? Visual.localScale : Vector3.one;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (Visual)
                    Visual.localScale = Vector3.Lerp(scale, new Vector3(scale.x * 1.4f, 0.05f, scale.z * 1.4f),
                        t / 1.2f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
