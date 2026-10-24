using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Aplica o multiplicador do TimeManager a um inimigo do Microgame (ou do Ícaro) sem editar seus scripts:
    // velocidade do NavMeshAgent, Animator.speed, cadência das armas e velocidade dos projéteis.
    // Roda depois dos scripts padrão para capturar os valores base já sobrescritos pelo EnemyController.Start.
    // A lentidão temporária (TimeAnchorSlow, EnemyStats.SlowMultiplier) entra por cima, só neste inimigo (os projéteis em voo
    // continuam no multiplicador geral).
    [DefaultExecutionOrder(100)]
    public class EnemyTimeScaler : MonoBehaviour
    {
        struct WeaponBase
        {
            public WeaponController Weapon;
            public float DelayBetweenShots;
            public float AmmoReloadDelay;
            public float AmmoReloadRate;
        }

        struct AnimatorBase
        {
            public Animator Animator;
            public float Speed;
        }

        NavMeshAgent m_Agent;
        float m_BaseAgentSpeed;
        float m_BaseAgentAcceleration;
        float m_BaseAgentAngularSpeed;

        EnemyController m_EnemyController;
        float m_BaseOrientationSpeed;

        EnemyTurret m_Turret;
        float m_BaseTurretFireDelay;
        float m_BaseTurretAimSharpness;
        float m_BaseTurretLookSharpness;

        readonly List<WeaponBase> m_Weapons = new List<WeaponBase>();
        readonly List<AnimatorBase> m_Animators = new List<AnimatorBase>();

        EnemyStats m_Stats;
        float m_AppliedMultiplier = -1f;
        float m_AppliedSlow = 1f;

        void Start()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            if (m_Agent)
            {
                m_BaseAgentSpeed = m_Agent.speed;
                m_BaseAgentAcceleration = m_Agent.acceleration;
                m_BaseAgentAngularSpeed = m_Agent.angularSpeed;
            }

            m_EnemyController = GetComponent<EnemyController>();
            if (m_EnemyController)
                m_BaseOrientationSpeed = m_EnemyController.OrientationSpeed;

            m_Turret = GetComponent<EnemyTurret>();
            if (m_Turret)
            {
                m_BaseTurretFireDelay = m_Turret.DetectionFireDelay;
                m_BaseTurretAimSharpness = m_Turret.AimRotationSharpness;
                m_BaseTurretLookSharpness = m_Turret.LookAtRotationSharpness;
            }

            var stats = m_Stats = GetComponent<EnemyStats>();
            foreach (var weapon in GetComponentsInChildren<WeaponController>(true))
            {
                m_Weapons.Add(new WeaponBase
                {
                    Weapon = weapon,
                    DelayBetweenShots = weapon.DelayBetweenShots,
                    AmmoReloadDelay = weapon.AmmoReloadDelay,
                    AmmoReloadRate = weapon.AmmoReloadRate,
                });

                // Com ficha (EnemyStats), o projétil leva o dano da ficha (já escalado pelo bioma) e a velocidade dela
                weapon.ProjectilePrefab = TimeScaledProjectiles.GetTemplate(weapon.ProjectilePrefab,
                    stats && stats.Definition ? stats.Damage : -1f,
                    stats && stats.Definition ? stats.Definition.ProjectileSpeed : 0f);
            }

            foreach (var animator in GetComponentsInChildren<Animator>(true))
            {
                m_Animators.Add(new AnimatorBase { Animator = animator, Speed = animator.speed });
            }

            Apply(TimeManager.CurrentMultiplier);
        }

        void Update()
        {
            float multiplier = TimeManager.CurrentMultiplier;
            float slow = m_Stats ? m_Stats.SlowMultiplier : 1f;
            if (!Mathf.Approximately(multiplier, m_AppliedMultiplier) || !Mathf.Approximately(slow, m_AppliedSlow))
                Apply(multiplier);
        }

        void Apply(float multiplier)
        {
            m_AppliedMultiplier = multiplier;
            m_AppliedSlow = m_Stats ? m_Stats.SlowMultiplier : 1f;
            // Tempo deste inimigo: o geral com a lentidão própria por cima
            float m = multiplier * m_AppliedSlow;

            if (m_Agent)
            {
                // O movimento pode ter um teto próprio (ficha); giro, cadência e animação seguem o multiplicador inteiro
                float move = CombatTiming.Movement(m_Stats);
                m_Agent.speed = m_BaseAgentSpeed * move;
                m_Agent.acceleration = m_BaseAgentAcceleration * move;
                m_Agent.angularSpeed = m_BaseAgentAngularSpeed * m;
            }

            if (m_EnemyController)
                m_EnemyController.OrientationSpeed = m_BaseOrientationSpeed * m;

            if (m_Turret)
            {
                m_Turret.DetectionFireDelay = m_BaseTurretFireDelay / m;
                m_Turret.AimRotationSharpness = m_BaseTurretAimSharpness * m;
                m_Turret.LookAtRotationSharpness = m_BaseTurretLookSharpness * m;
            }

            foreach (var w in m_Weapons)
            {
                if (!w.Weapon)
                    continue;

                w.Weapon.DelayBetweenShots = w.DelayBetweenShots / m;
                w.Weapon.AmmoReloadDelay = w.AmmoReloadDelay / m;
                w.Weapon.AmmoReloadRate = w.AmmoReloadRate * m;
            }

            foreach (var a in m_Animators)
            {
                if (a.Animator)
                    a.Animator.speed = a.Speed * m;
            }

            TimeScaledProjectiles.Apply(multiplier);
        }
    }
}
