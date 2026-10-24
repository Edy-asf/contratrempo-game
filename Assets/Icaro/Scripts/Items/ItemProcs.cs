using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Efeitos de item disparados pelo combate. Escuta o evento central de dano (acertos e abates do jogador) e os
    // tiros da arma ativa. Dano de item usa CombatEvents.ApplyDamage com item = true e não ativa outros efeitos de
    // item (como o "proc coefficient" do Risk of Rain 2), mas abates por item ainda explodem (Resina Volátil).
    // - Bobina Ressonante: chance de raio que salta entre inimigos próximos.
    // - Mísseis de Sondagem: chance de um míssil teleguiado.
    // - Cristal Ceifador: acertos críticos curam.
    // - Resina Volátil: inimigos mortos pelo jogador explodem.
    // - Eco Temporal: chance de o tiro se repetir.
    // - Munição Atordoante: chance de deixar o inimigo mais lento (TimeAnchorSlow).
    // - Cartucho Incendiário: chance de incendiar o inimigo (EnemyBurn).
    // As chances e a cura são multiplicadas pelo coeficiente do acerto (DamageInfo.ProcCoefficient, da ficha da arma).
    public class ItemProcs : MonoBehaviour
    {
        PlayerStats m_Stats;
        PlayerWeaponsManager m_Weapons;
        Health m_Health;
        ActorsManager m_Actors;
        WeaponController m_SubscribedWeapon;

        void Awake()
        {
            m_Stats = GetComponent<PlayerStats>();
            m_Weapons = GetComponent<PlayerWeaponsManager>();
            m_Health = GetComponent<Health>();
        }

        void OnEnable() => CombatEvents.OnDamage += OnDamage;

        void OnDisable()
        {
            CombatEvents.OnDamage -= OnDamage;
            SubscribeWeapon(null);
        }

        void Update()
        {
            // O Eco Temporal acompanha a arma ativa
            WeaponController active = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            if (active != m_SubscribedWeapon)
                SubscribeWeapon(active);
        }

        void SubscribeWeapon(WeaponController weapon)
        {
            if (m_SubscribedWeapon)
                m_SubscribedWeapon.OnShootProcessed -= OnShot;
            m_SubscribedWeapon = weapon;
            if (m_SubscribedWeapon)
                m_SubscribedWeapon.OnShootProcessed += OnShot;
        }

        ItemCatalog Catalog => m_Stats ? m_Stats.Catalog : null;

        void OnDamage(DamageInfo info)
        {
            if (!m_Stats || !info.AttackerIsPlayer || info.VictimIsPlayer || !info.VictimHealth)
                return;

            if (info.Killed && m_Stats.Get(PlayerStat.ExplosionDamage) > 0f)
                Explode(info);

            if (info.IsItem)
                return;

            // Acertos de armas com muitos projéteis por tiro pesam menos (coeficiente < 1)
            float proc = info.ProcCoefficient;
            if (info.IsCritical && m_Health && m_Stats.Get(PlayerStat.CritHeal) > 0f)
                m_Health.Heal(m_Stats.Get(PlayerStat.CritHeal) * proc);

            if (Random.value < m_Stats.Get(PlayerStat.LightningChance) * proc)
                Lightning(info);

            if (Random.value < m_Stats.Get(PlayerStat.MissileChance) * proc)
                LaunchMissile(info);

            if (Random.value < m_Stats.Get(PlayerStat.SlowChance) * proc)
                Slow(info);

            if (Random.value < m_Stats.Get(PlayerStat.BurnChance) * proc)
                EnemyBurn.Apply(info.VictimHealth, m_Stats.ShotDamage * m_Stats.Get(PlayerStat.BurnDamage), gameObject,
                    Catalog);
        }

        // ---------- Bobina Ressonante ----------

        void Lightning(DamageInfo info)
        {
            int targets = Mathf.RoundToInt(m_Stats.Get(PlayerStat.LightningTargets));
            float damage = m_Stats.ShotDamage * m_Stats.Get(PlayerStat.LightningDamage);
            float range = Catalog ? Catalog.LightningJumpRange : 15f;

            var hit = new HashSet<Health> { info.VictimHealth };
            var points = new List<Vector3> { info.Point };
            Vector3 from = info.Point;
            for (int i = 0; i < targets; i++)
            {
                Health next = NearestEnemy(from, range, hit);
                if (!next)
                    break;
                hit.Add(next);
                Vector3 point = CenterOf(next);
                points.Add(point);
                Damage(next, damage, point, explosion: false);
                from = point;
            }

            if (points.Count > 1)
                ItemVfx.Lightning(Catalog, points.ToArray());
        }

        // ---------- Mísseis de Sondagem ----------

        void LaunchMissile(DamageInfo info)
        {
            Vector3 origin = transform.position + Vector3.up * 1.6f + transform.right * 0.4f;
            var missile = new GameObject("MissilSondagem").AddComponent<HomingMissile>();
            missile.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(Vector3.up + transform.forward));
            missile.Launch(this, info.VictimHealth, m_Stats.ShotDamage * m_Stats.Get(PlayerStat.MissileDamage),
                Catalog);
        }

        // ---------- Munição Atordoante ----------

        void Slow(DamageInfo info)
        {
            if (!Catalog || info.Killed)
                return;
            float amount = Mathf.Min(m_Stats.Get(PlayerStat.SlowAmount), Catalog.MaxSlowAmount);
            var stats = info.VictimHealth.GetComponent<EnemyStats>();
            if (stats && stats.IsBoss)
                amount *= Catalog.BossSlowStrength;
            if (amount > 0f)
                TimeAnchorSlow.Apply(info.VictimHealth, 1f - amount, Catalog.SlowDuration, Catalog.SlowColor);
        }

        // ---------- Resina Volátil ----------

        void Explode(DamageInfo info)
        {
            float radius = m_Stats.Get(PlayerStat.ExplosionRadius);
            float damage = m_Stats.ShotDamage * m_Stats.Get(PlayerStat.ExplosionDamage);
            Vector3 center = CenterOf(info.VictimHealth);
            ItemVfx.Blast(Catalog, center, radius, new Color(1f, 0.55f, 0.15f));

            foreach (Health enemy in EnemiesInRadius(center, radius))
            {
                if (enemy != info.VictimHealth)
                    Damage(enemy, damage, CenterOf(enemy), explosion: true);
            }
        }

        // ---------- Eco Temporal ----------

        void OnShot()
        {
            if (!m_Stats || !m_SubscribedWeapon || !m_SubscribedWeapon.ProjectilePrefab)
                return;

            // Acima de 100% repete garantido e sorteia o resto (como as chances do Risk of Rain 2)
            float chance = m_Stats.Get(PlayerStat.EchoChance);
            int echoes = Mathf.FloorToInt(chance) + (Random.value < chance % 1f ? 1 : 0);
            if (echoes > 0)
                StartCoroutine(Echo(m_SubscribedWeapon, echoes));
        }

        IEnumerator Echo(WeaponController weapon, int count)
        {
            float delay = Catalog ? Catalog.EchoDelay : 0.08f;
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(delay);
                if (!weapon || !weapon.IsWeaponActive || !weapon.WeaponMuzzle)
                    yield break;

                for (int b = 0; b < Mathf.Max(1, weapon.BulletsPerShot); b++)
                {
                    Vector3 direction = weapon.GetShotDirectionWithinSpread(weapon.WeaponMuzzle);
                    // Mesmo caminho dos tiros da arma: pool e assistência de mira
                    ProjectileBase projectile = ProjectileSpawner.Spawn(weapon.ProjectilePrefab,
                        weapon.WeaponMuzzle.position, Quaternion.LookRotation(direction));
                    projectile.Shoot(weapon);
                }

                if (weapon.ShootSfx)
                    AudioUtility.CreateSFX(weapon.ShootSfx, weapon.WeaponMuzzle.position,
                        AudioUtility.AudioGroups.WeaponShoot, 0f);
            }
        }

        // ---------- utilidades ----------

        public void Damage(Health enemy, float amount, Vector3 point, bool explosion)
        {
            if (!enemy || enemy.CurrentHealth <= 0f)
                return;
            var damageable = enemy.GetComponentInChildren<Damageable>();
            if (damageable)
                CombatEvents.ApplyDamage(damageable, amount, gameObject, point, explosion: explosion, item: true);
        }

        public Health NearestEnemy(Vector3 from, float range, HashSet<Health> exclude)
        {
            Health best = null;
            float bestDistance = range;
            foreach (Health enemy in Enemies())
            {
                if (exclude != null && exclude.Contains(enemy))
                    continue;
                float distance = Vector3.Distance(from, CenterOf(enemy));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }

            return best;
        }

        IEnumerable<Health> EnemiesInRadius(Vector3 center, float radius)
        {
            foreach (Health enemy in Enemies())
            {
                if (Vector3.Distance(center, CenterOf(enemy)) <= radius)
                    yield return enemy;
            }
        }

        // Inimigos vivos: atores do Microgame com afiliação diferente da do jogador
        List<Health> Enemies()
        {
            var result = new List<Health>();
            if (!m_Actors)
                m_Actors = FindAnyObjectByType<ActorsManager>();
            var self = GetComponent<Actor>();
            if (!m_Actors || !self)
                return result;

            foreach (Actor actor in m_Actors.Actors)
            {
                if (!actor || actor.Affiliation == self.Affiliation)
                    continue;
                var health = actor.GetComponent<Health>();
                if (health && health.CurrentHealth > 0f)
                    result.Add(health);
            }

            return result;
        }

        public static Vector3 CenterOf(Health health)
        {
            var actor = health.GetComponent<Actor>();
            if (actor && actor.AimPoint)
                return actor.AimPoint.position;
            var collider = health.GetComponentInChildren<Collider>();
            return collider ? collider.bounds.center : health.transform.position + Vector3.up;
        }
    }
}
