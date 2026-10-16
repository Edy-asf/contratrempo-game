using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Projétil do jogador: no disparo aplica o multiplicador de dano e sorteia o crítico (valores do PlayerStats
    // do dono) sobre o dano do ProjectileStandard. Enquanto voa fica registrado, para o CombatEvents descobrir qual
    // tiro acertou um Health (o ProjectileStandard só se destrói depois de aplicar o dano) e onde: o projétil é
    // rápido (centenas de m/s), então a busca usa o trecho percorrido no frame, não só a posição atual.
    [RequireComponent(typeof(ProjectileStandard))]
    public class PlayerProjectile : MonoBehaviour
    {
        // Distância máxima entre o trecho do projétil e a vítima para atribuir o acerto a ele
        const float k_MaxMatchDistance = 1.5f;
        const int k_SegmentSamples = 12;

        static readonly List<PlayerProjectile> s_Active = new List<PlayerProjectile>();

        public bool IsCritical { get; private set; }

        ProjectileStandard m_Projectile;
        ProjectileBase m_Base;
        float m_BaseDamage;
        Vector3 m_PreviousPosition;

        void Awake()
        {
            m_Projectile = GetComponent<ProjectileStandard>();
            m_Base = m_Projectile;
            m_BaseDamage = m_Projectile.Damage;
        }

        void OnEnable()
        {
            s_Active.Add(this);
            m_Base.OnShoot += OnShoot;
            m_PreviousPosition = transform.position;
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            m_Base.OnShoot -= OnShoot;
        }

        void OnShoot()
        {
            var stats = m_Base.Owner ? m_Base.Owner.GetComponent<PlayerStats>() : null;
            float chance = stats ? stats.CritChance : 0f;
            float multiplier = stats ? stats.CritDamageMultiplier : 2f;
            float damage = m_BaseDamage * (stats ? stats.DamageMultiplier : 1f);

            IsCritical = Random.value < chance;
            m_Projectile.Damage = IsCritical ? damage * multiplier : damage;
            m_PreviousPosition = m_Base.InitialPosition;
        }

        // Depois do ProjectileStandard mover: o trecho do próximo frame começa aqui
        void LateUpdate() => m_PreviousPosition = transform.position;

        // O projétil cujo trecho deste frame passa mais perto da vítima; o ponto é o da superfície da vítima mais
        // próximo desse trecho
        public static bool FindHit(Health victim, out Vector3 point, out bool critical)
        {
            point = default;
            critical = false;
            Collider[] colliders = victim.GetComponentsInChildren<Collider>();
            float best = k_MaxMatchDistance;
            bool found = false;

            foreach (PlayerProjectile projectile in s_Active)
            {
                Vector3 from = projectile.m_PreviousPosition;
                Vector3 to = projectile.transform.position;
                for (int s = 0; s <= k_SegmentSamples; s++)
                {
                    Vector3 sample = Vector3.Lerp(from, to, s / (float)k_SegmentSamples);
                    foreach (Collider collider in colliders)
                    {
                        if (collider.isTrigger)
                            continue;
                        Vector3 closest = ClosestPoint(collider, sample);
                        float distance = Vector3.Distance(closest, sample);
                        if (distance < best)
                        {
                            best = distance;
                            point = closest;
                            critical = projectile.IsCritical;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        static Vector3 ClosestPoint(Collider collider, Vector3 position)
        {
            bool supported = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                             (collider is MeshCollider mesh && mesh.convex);
            return supported ? collider.ClosestPoint(position) : collider.ClosestPointOnBounds(position);
        }
    }
}
