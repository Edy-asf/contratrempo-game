using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Projétil do jogador com assistência de mira, no lugar do ProjectileStandard do Microgame na arma do Ícaro.
    // No disparo, sorteia o crítico (PlayerStats) e pede o alvo ao AimAssist: com alvo, sai do cano já apontado
    // para ele e, a cada frame, gira em direção ao ponto de mira (taxa máxima de giro, velocidade constante); nos
    // últimos metros (LockDistance) a direção trava no alvo. A cada LineOfSightInterval confere a linha de visão
    // até o alvo: se uma parede bloquear, para de perseguir e segue reto (não contorna cantos). Se o alvo morrer,
    // segue reto. Sem alvo, sai reto para o ponto do centro da tela. Colide com paredes e com qualquer inimigo no
    // caminho (SphereCast sem alocação) e causa o dano pelo evento central (crítico e ponto do impacto). Não é
    // afetado pelo multiplicador de tempo. Reaproveitado pelo PrefabPool (ProjectileSpawner).
    // Armas do Ícaro (WeaponDefinition): com a ficha, velocidade, dano, raio, alcance, modo de mira e coeficiente de
    // efeitos de item vêm dela; o dano é o mesmo a qualquer distância. Subclasses podem mudar o que acontece no acerto
    // pelos ganchos OnEnemyHit, OnWallHit, DamageScale e OnLaunched (as cinco armas atuais usam a classe base).
    public class HomingProjectile : ProjectileBase
    {
        // O que o projétil faz depois de um acerto
        protected enum HitResponse
        {
            // Some (efeito de impacto e volta ao pool)
            Stop,

            // Atravessa e segue (o inimigo atingido é ignorado no resto do voo)
            Pierce,

            // Mudou de rumo (a subclasse já reposicionou e mudou a velocidade)
            Redirect,
        }

        public float Speed = 300f;
        public float Damage = 15f;

        [Tooltip("Raio do projétil para colisão")]
        public float Radius = 0.05f;

        public float MaxLifeTime = 3f;
        public GameObject ImpactVfx;
        public float ImpactVfxLifetime = 2f;
        public float ImpactVfxSpawnOffset = 0.1f;
        public AudioClip ImpactSfxClip;
        public LayerMask HittableLayers = -1;

        [Tooltip("Ficha da arma que dispara este projétil (vazio = usa os números acima)")]
        public WeaponDefinition Definition;

        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        public bool IsCritical { get; private set; }

        // Dano base de um projétil (antes do multiplicador do jogador e do crítico): o da ficha ou o do campo
        public float BaseDamage => Definition ? Definition.Damage : Damage;

        protected AimAssist Assist { get; private set; }
        protected Health Target { get; set; }
        protected bool Tracking { get; set; }
        protected Vector3 Velocity { get; set; }

        // Velocidade do voo (a da ficha, ajustável pelas subclasses no OnLaunched)
        protected float FlightSpeed { get; set; }

        // Dano de cada acerto (com multiplicadores e crítico)
        protected float HitDamage { get; set; }

        protected float ProcCoefficient { get; set; } = 1f;
        protected float Age { get; set; }
        protected float Lifetime { get; set; }

        // Inimigos que este projétil não acerta mais (já atravessados)
        protected readonly HashSet<Health> Ignored = new HashSet<Health>();

        protected TrailRenderer Trail { get; private set; }

        float m_NextLineOfSight;

        // Inimigos que este tiro ainda atravessa pelo Disparo Perfurante (sorteado no disparo)
        int m_ItemPierces;

        protected virtual void Awake()
        {
            Trail = GetComponentInChildren<TrailRenderer>();
        }

        protected virtual void OnEnable() => OnShoot += HandleShoot;
        protected virtual void OnDisable() => OnShoot -= HandleShoot;

        // Multiplicador do dano deste tiro (gancho para subclasses)
        protected virtual float DamageScale() => 1f;

        // Multiplicador da velocidade deste tiro (gancho para subclasses)
        protected virtual float SpeedScale() => 1f;

        // Depois da direção e do alvo decididos, antes do primeiro movimento (ecos, escala, cores)
        protected virtual void OnLaunched()
        {
        }

        // Escolhe a direção inicial; devolve falso para usar a mira normal (ecos já sabem para onde ir)
        protected virtual bool OverrideLaunch(ref Vector3 direction) => false;

        void HandleShoot()
        {
            Age = 0f;
            Ignored.Clear();
            FlightSpeed = (Definition ? Definition.ProjectileSpeed : Speed) * SpeedScale();
            if (Definition)
                Radius = Definition.ProjectileRadius;

            var stats = Owner ? Owner.GetComponent<PlayerStats>() : null;
            float damage = BaseDamage * DamageScale() * (stats ? stats.DamageMultiplier : 1f);
            IsCritical = stats && Random.value < stats.CritChance;
            HitDamage = IsCritical ? damage * stats.CritDamageMultiplier : damage;
            ProcCoefficient = Definition ? Definition.ProcCoefficient : 1f;

            // Disparo Perfurante: acima de 100% atravessa garantido e sorteia o resto (como o Eco Temporal)
            float pierce = stats ? stats.Get(PlayerStat.PierceChance) : 0f;
            m_ItemPierces = Mathf.FloorToInt(pierce) + (Random.value < pierce % 1f ? 1 : 0);

            Assist = Owner ? Owner.GetComponent<AimAssist>() : null;
            Target = null;
            Tracking = false;
            Vector3 direction = transform.forward;
            if (!OverrideLaunch(ref direction))
                direction = Aim(direction);

            Velocity = direction * FlightSpeed;
            transform.forward = direction;
            m_NextLineOfSight = Assist && Assist.Settings ? Assist.Settings.LineOfSightInterval : 0.05f;
            if (Trail)
                Trail.Clear();

            OnLaunched();
            // Alcance da ficha: o voo dura o tempo de percorrê-lo
            Lifetime = Definition && FlightSpeed > 0f ? Definition.Range / FlightSpeed : MaxLifeTime;
        }

        // Direção inicial pela assistência de mira (modo da ficha; sem ficha, teleguiado)
        Vector3 Aim(Vector3 direction)
        {
            WeaponAimMode mode = Definition ? Definition.AimMode : WeaponAimMode.Homing;
            if (!Assist || mode == WeaponAimMode.None)
                return direction;

            // Dispersão da arma (escopeta): o desvio do cano é mantido em volta da direção assistida
            Quaternion spread = mode == WeaponAimMode.AimOnly ? SpreadOffset(direction) : Quaternion.identity;
            if (Assist.TryPickTarget(transform.position, out Health target))
            {
                Target = target;
                Tracking = mode == WeaponAimMode.Homing;
                return spread * (Assist.AimPointOf(target) - transform.position).normalized;
            }

            return spread * Assist.CenterDirection(transform.position);
        }

        // Rotação do centro do cano até a direção sorteada pela dispersão da arma
        Quaternion SpreadOffset(Vector3 direction)
        {
            var weapons = Owner ? Owner.GetComponent<PlayerWeaponsManager>() : null;
            WeaponController weapon = weapons ? weapons.GetActiveWeapon() : null;
            return weapon && weapon.WeaponMuzzle
                ? Quaternion.FromToRotation(weapon.WeaponMuzzle.forward, direction)
                : Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;
            if (Age >= Lifetime)
            {
                Expire();
                return;
            }

            if (Tracking)
                Steer(dt);

            Vector3 start = transform.position;
            Vector3 step = Velocity * dt;
            if (CheckHit(start, step))
                return;

            transform.position = start + step;
            if (Velocity.sqrMagnitude > 0.0001f)
                transform.forward = Velocity;
        }

        // Fim do alcance ou do tempo de vida
        protected virtual void Expire() => PrefabPool.Release(gameObject);

        // Persegue um alvo (também usado pelas subclasses depois de trocar de alvo)
        protected void TrackTarget(Health target)
        {
            Target = target;
            Tracking = target && Assist;
            m_NextLineOfSight = Assist && Assist.Settings ? Assist.Settings.LineOfSightInterval : 0.05f;
        }

        void Steer(float dt)
        {
            if (!Target || Target.CurrentHealth <= 0f || !Target.gameObject.activeInHierarchy || !Assist)
            {
                Tracking = false;
                return;
            }

            AimAssistSettings settings = Assist.Settings;
            Vector3 position = transform.position;
            Vector3 aim = Assist.AimPointOf(Target);

            // Parede entre o projétil e o alvo: para de perseguir e segue reto
            m_NextLineOfSight -= dt;
            if (m_NextLineOfSight <= 0f)
            {
                m_NextLineOfSight = settings.LineOfSightInterval;
                if (!Assist.HasLineOfSight(position, aim, Target))
                {
                    Tracking = false;
                    return;
                }
            }

            Vector3 toTarget = aim - position;
            float distance = toTarget.magnitude;
            if (distance < 0.001f)
                return;

            Vector3 desired = toTarget / distance;
            // Trava final: perto o bastante (ou a um passo do alvo), aponta direto
            Vector3 direction = distance <= Mathf.Max(settings.LockDistance, FlightSpeed * dt * 1.5f)
                ? desired
                : Vector3.RotateTowards(Velocity.normalized, desired,
                    settings.EffectiveTurnRate * Mathf.Deg2Rad * dt, 0f);
            Velocity = direction * FlightSpeed;
        }

        // Colisão no trecho do frame, do acerto mais próximo para o mais distante (ignora o dono, gatilhos sem
        // Damageable e inimigos já atravessados). Verdadeiro se o projétil sumiu ou mudou de rumo.
        bool CheckHit(Vector3 start, Vector3 step)
        {
            float length = step.magnitude;
            if (length <= 0f)
                return false;

            Vector3 direction = step / length;
            int count = Physics.SphereCastNonAlloc(start, Radius, direction, s_Hits, length, HittableLayers,
                QueryTriggerInteraction.Collide);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_Hits[i];
                if (!IsHitValid(hit.collider))
                    continue;

                var damageable = hit.collider.GetComponent<Damageable>();
                Health health = damageable ? damageable.Health : null;
                if (health && Ignored.Contains(health))
                    continue;

                // Começou dentro de um colisor: o ponto é a posição atual
                Vector3 point = hit.distance <= 0f ? start : hit.point;
                Vector3 normal = hit.distance <= 0f ? -direction : hit.normal;
                HitResponse response = health
                    ? OnEnemyHit(damageable, health, point, normal)
                    : OnWallHit(point, normal, hit.collider);

                // Disparo Perfurante: um tiro que pararia no inimigo atravessa, com menos dano nos seguintes
                if (health && response == HitResponse.Stop && m_ItemPierces > 0)
                {
                    m_ItemPierces--;
                    HitDamage *= PlayerStats.Instance && PlayerStats.Instance.Catalog
                        ? PlayerStats.Instance.Catalog.PierceDamage
                        : 0.6f;
                    response = HitResponse.Pierce;
                }

                if (response == HitResponse.Pierce)
                {
                    Ignored.Add(health);
                    SpawnImpact(point, normal, sound: false);
                    continue;
                }

                if (response == HitResponse.Redirect)
                {
                    SpawnImpact(point, normal, sound: true);
                    return true;
                }

                SpawnImpact(point, normal, sound: true);
                PrefabPool.Release(gameObject);
                return true;
            }

            return false;
        }

        static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = s_Hits[i];
                int j = i - 1;
                while (j >= 0 && s_Hits[j].distance > current.distance)
                {
                    s_Hits[j + 1] = s_Hits[j];
                    j--;
                }

                s_Hits[j + 1] = current;
            }
        }

        bool IsHitValid(Collider collider)
        {
            if (collider.GetComponent<IgnoreHitDetection>())
                return false;
            if (collider.isTrigger && !collider.GetComponent<Damageable>())
                return false;
            return !Owner || !collider.transform.IsChildOf(Owner.transform);
        }

        // Acerto num inimigo: por padrão causa o dano e some
        protected virtual HitResponse OnEnemyHit(Damageable damageable, Health health, Vector3 point, Vector3 normal)
        {
            DealDamage(damageable, point);
            return HitResponse.Stop;
        }

        // Acerto em parede ou cenário: por padrão some
        protected virtual HitResponse OnWallHit(Vector3 point, Vector3 normal, Collider collider) => HitResponse.Stop;

        // Dano pelo evento central, com o crítico e o coeficiente de efeitos de item deste tiro (e o bônus de perto
        // do Cano Curto, pela distância do jogador até o acerto)
        protected void DealDamage(Damageable damageable, Vector3 point, float scale = 1f)
        {
            if (!damageable)
                return;
            var stats = Owner ? Owner.GetComponent<PlayerStats>() : null;
            if (stats)
                scale *= stats.CloseRangeMultiplier(Vector3.Distance(Owner.transform.position, point));
            CombatEvents.ApplyDamage(damageable, HitDamage * scale, Owner, point, IsCritical,
                procCoefficient: ProcCoefficient);
        }

        protected void SpawnImpact(Vector3 point, Vector3 normal, bool sound)
        {
            if (ImpactVfx)
            {
                GameObject vfx = PrefabPool.Get(ImpactVfx, point + normal * ImpactVfxSpawnOffset,
                    Quaternion.LookRotation(normal));
                var release = vfx.GetComponent<ReleaseAfter>();
                if (!release)
                    release = vfx.AddComponent<ReleaseAfter>();
                release.Begin(ImpactVfxLifetime > 0f ? ImpactVfxLifetime : 2f);
            }

            if (sound && ImpactSfxClip)
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
        }
    }
}
