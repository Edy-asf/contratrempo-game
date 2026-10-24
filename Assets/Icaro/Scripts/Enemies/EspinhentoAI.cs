using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Espinhento: planta fixa num ponto alto que atira em qualquer ponto da arena, desde que veja o jogador.
    // Sem aviso de mira: ao ver o jogador, dispara a rajada e espera o intervalo da ficha, que encurta com o
    // multiplicador mas nunca fica abaixo de MinShotInterval. Valores da ficha (EspinhentoDefinition); os campos
    // abaixo valem sem ficha.
    [RequireComponent(typeof(EnemyController))]
    public class EspinhentoAI : MonoBehaviour
    {
        [Tooltip("Parte que gira para mirar; a arma deve ser filha dela")]
        public Transform Head;

        [Tooltip("Rapidez com que a cabeça acompanha o jogador, a 1,0x")]
        public float AimSharpness = 5f;

        [Tooltip("Velocidade de giro dos espinhos decorativos, em graus/s a 1,0x")]
        public float IdleSpinSpeed = 30f;

        [Tooltip("Espinhos decorativos que giram em volta do corpo (opcional)")]
        public Transform SpikeRing;

        [Header("Cadência (sem ficha)")]
        public float ShotInterval = 2.2f;
        public float MinShotInterval = 1.4f;

        const float k_ChestHeight = 1.1f;

        EnemyController m_Controller;
        float m_NextShotTime;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();

            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                ShotInterval = stats.AttackCooldown;
                if (stats.Definition is EspinhentoDefinition espinhento)
                    MinShotInterval = espinhento.MinShotInterval;
            }

            // A cadência é controlada aqui; a arma só não pode atrasar o disparo
            foreach (var weapon in GetComponentsInChildren<WeaponController>(true))
                weapon.DelayBetweenShots = 0.05f;

            // O EnemyController invoca estes eventos sem checar null; precisam ter assinante
            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };
        }

        void Update()
        {
            float multiplier = TimeManager.CurrentMultiplier;

            if (SpikeRing)
                SpikeRing.Rotate(Vector3.up, IdleSpinSpeed * multiplier * Time.deltaTime, Space.Self);

            GameObject target = m_Controller.KnownDetectedTarget;
            if (!target)
                return;

            Vector3 aimPosition = target.transform.position + Vector3.up * k_ChestHeight;
            TurnHead(aimPosition, multiplier);

            if (m_Controller.IsSeeingTarget && Time.time >= m_NextShotTime)
            {
                m_Controller.TryAtack(aimPosition);
                ThreatSignals.Report(transform, 1f);
                m_NextShotTime = Time.time + Mathf.Max(MinShotInterval, CombatTiming.Scaled(ShotInterval));
            }
        }

        void TurnHead(Vector3 aimPosition, float multiplier)
        {
            if (!Head)
                return;

            Vector3 direction = Vector3.ProjectOnPlane(aimPosition - Head.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
                Head.rotation = Quaternion.Slerp(Head.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-AimSharpness * multiplier * Time.deltaTime));
        }
    }
}
