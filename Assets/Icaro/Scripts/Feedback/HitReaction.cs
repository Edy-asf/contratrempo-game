using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Reação do inimigo ao dano (pelo evento central): um pequeno recuo para longe do ponto de impacto e, no
    // abate, uma explosão de partículas. O piscar branco é o flash do EnemyController (gradiente branco e curto,
    // configurado nos prefabs). O recuo usa o NavMeshAgent, então não atravessa paredes nem sai do NavMesh.
    public class HitReaction : MonoBehaviour
    {
        [Tooltip("Distância do recuo por acerto (crítico: x1,5). Inimigos grandes recuam pouco")]
        public float RecoilDistance = 0.25f;

        [Tooltip("Tempo em que o recuo é aplicado")]
        public float RecoilDuration = 0.08f;

        public GameObject DeathBurst;
        public float DeathBurstScale = 1f;

        [Tooltip("Altura do centro do corpo, de onde sai a explosão")]
        public float CenterHeight = 0.8f;

        NavMeshAgent m_Agent;
        Vector3 m_RecoilVelocity;
        float m_RecoilTime;

        void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
        }

        void OnEnable() => CombatEvents.OnDamage += OnDamage;
        void OnDisable() => CombatEvents.OnDamage -= OnDamage;

        void OnDamage(DamageInfo info)
        {
            if (info.Victim != gameObject)
                return;

            if (info.Killed)
            {
                SpawnBurst();
                return;
            }

            Vector3 away = transform.position - info.Point;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -transform.forward;
            float distance = RecoilDistance * (info.IsCritical ? 1.5f : 1f);
            m_RecoilVelocity = away.normalized * (distance / RecoilDuration);
            m_RecoilTime = RecoilDuration;
        }

        void Update()
        {
            if (m_RecoilTime <= 0f)
                return;

            float dt = Mathf.Min(Time.deltaTime, m_RecoilTime);
            m_RecoilTime -= dt;
            if (m_Agent && m_Agent.enabled && m_Agent.isOnNavMesh)
                m_Agent.Move(m_RecoilVelocity * dt);
        }

        void SpawnBurst()
        {
            if (!DeathBurst)
                return;

            float scale = DeathBurstScale * transform.lossyScale.x;
            GameObject burst = Instantiate(DeathBurst, transform.position + Vector3.up * CenterHeight * scale,
                Quaternion.identity);
            burst.transform.localScale = Vector3.one * scale;
            Destroy(burst, 4f);
        }
    }
}
