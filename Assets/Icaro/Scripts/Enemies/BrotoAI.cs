using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Broto: corre direto até o jogador e causa dano por contato, com recarga entre um golpe e outro.
    // A velocidade de corrida vem do NavMeshAgent (escalada pelo EnemyTimeScaler); recarga e animação
    // de pulo seguem o multiplicador do TimeManager.
    [RequireComponent(typeof(EnemyController))]
    public class BrotoAI : MonoBehaviour
    {
        [Header("Contato")]
        [Tooltip("Distância entre o Broto e o jogador em que o golpe acerta")]
        public float ContactRange = 1.4f;

        public float ContactDamage = 10f;

        [Tooltip("Segundos entre dois golpes a 1,0x")]
        public float ContactCooldown = 0.8f;

        [Tooltip("Intervalo para recalcular o caminho até o jogador, a 1,0x")]
        public float RepathInterval = 0.2f;

        [Header("Visual")]
        [Tooltip("Parte visual que quica enquanto o Broto corre")]
        public Transform Body;

        public float HopHeight = 0.25f;
        public float HopFrequency = 3f;

        EnemyController m_Controller;
        ActorsManager m_ActorsManager;
        Damageable m_PlayerDamageable;
        Vector3 m_BodyBasePosition;
        float m_HopPhase;
        float m_NextRepathTime;
        float m_NextHitTime;

        void Start()
        {
            m_Controller = GetComponent<EnemyController>();
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();

            // Dano e recarga vêm da ficha do inimigo (já com a escala do bioma)
            var stats = GetComponent<EnemyStats>();
            if (stats && stats.Definition)
            {
                ContactDamage = stats.Damage;
                ContactCooldown = stats.AttackCooldown;
            }

            // O EnemyController invoca estes eventos sem checar null; precisam ter assinante
            m_Controller.onDetectedTarget += () => { };
            m_Controller.onLostTarget += () => { };

            if (Body)
                m_BodyBasePosition = Body.localPosition;
        }

        void Update()
        {
            GameObject player = m_ActorsManager ? m_ActorsManager.Player : null;
            if (!player)
                return;

            float multiplier = TimeManager.CurrentMultiplier;
            Vector3 playerPosition = player.transform.position;

            if (Time.time >= m_NextRepathTime)
            {
                m_Controller.SetNavDestination(playerPosition);
                m_NextRepathTime = Time.time + RepathInterval / multiplier;
            }

            if (Time.time >= m_NextHitTime && IsTouching(playerPosition))
            {
                if (!m_PlayerDamageable)
                    m_PlayerDamageable = player.GetComponent<Damageable>();

                if (m_PlayerDamageable)
                    CombatEvents.ApplyDamage(m_PlayerDamageable, ContactDamage, gameObject,
                        playerPosition + Vector3.up);

                m_NextHitTime = Time.time + ContactCooldown / multiplier;
            }

            UpdateHop(multiplier);
        }

        bool IsTouching(Vector3 playerPosition)
        {
            Vector3 offset = playerPosition - transform.position;
            float vertical = Mathf.Abs(offset.y);
            offset.y = 0f;
            return offset.magnitude <= ContactRange && vertical < 2f;
        }

        void UpdateHop(float multiplier)
        {
            if (!Body)
                return;

            bool moving = m_Controller.NavMeshAgent && m_Controller.NavMeshAgent.velocity.sqrMagnitude > 0.1f;
            m_HopPhase += Time.deltaTime * HopFrequency * multiplier * Mathf.PI * (moving ? 2f : 0.6f);
            Body.localPosition = m_BodyBasePosition + Vector3.up * (Mathf.Abs(Mathf.Sin(m_HopPhase)) * HopHeight);
        }
    }
}
