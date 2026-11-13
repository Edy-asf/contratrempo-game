using System.Reflection;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Projétil do Microgame (inimigos) que segue a câmera lenta das habilidades em pleno voo: o ProjectileStandard fixa
    // a velocidade no disparo, então este componente reescala a velocidade guardada quando CombatTiming.WorldSpeed
    // muda (sem editar o script do Microgame). Adicionado pelo TimeScaledProjectiles aos modelos de projétil.
    [DefaultExecutionOrder(-50)]
    public class WorldSpeedProjectile : MonoBehaviour
    {
        // Nunca zero: o ProjectileStandard aponta o projétil pela direção da velocidade
        const float k_MinSpeed = 0.02f;

        static readonly FieldInfo s_Velocity =
            typeof(ProjectileStandard).GetField("m_Velocity", BindingFlags.NonPublic | BindingFlags.Instance);

        ProjectileStandard m_Projectile;
        float m_Applied = -1f;

        void Awake() => m_Projectile = GetComponent<ProjectileStandard>();

        void OnEnable() => m_Applied = -1f;

        void Update()
        {
            if (!m_Projectile || s_Velocity == null)
                return;
            float speed = Mathf.Max(k_MinSpeed, CombatTiming.WorldSpeed);
            // No primeiro frame a velocidade já é a do disparo, feita para o ritmo de agora
            if (m_Applied < 0f)
            {
                m_Applied = speed;
                return;
            }

            if (Mathf.Approximately(speed, m_Applied))
                return;
            var velocity = (Vector3)s_Velocity.GetValue(m_Projectile);
            s_Velocity.SetValue(m_Projectile, velocity * (speed / m_Applied));
            m_Applied = speed;
        }
    }
}
