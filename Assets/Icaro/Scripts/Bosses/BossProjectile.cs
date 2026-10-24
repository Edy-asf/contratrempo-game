using UnityEngine;

namespace Icaro
{
    // Projétil de chefe, para padrões com muitos tiros (anéis, espirais, leques, chuva): esfera brilhante tingida que
    // anda reto ou persegue o jogador devagar (Homing, graus por segundo), testando o trecho do frame com um
    // SphereCast sem alocação. Atravessa inimigos; no jogador causa dano; no cenário some. Com ImpactRadius, explode
    // ao bater (meteoros, bombas de esporo) e acerta o jogador dentro do raio. A velocidade segue o multiplicador de
    // tempo até o teto do BossKit. Reaproveitado pelo PrefabPool.
    public class BossProjectile : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        BossKit m_Kit;
        GameObject m_Owner;
        Vector3 m_Velocity;
        float m_Damage;
        float m_Life;
        float m_Homing;
        float m_Radius;
        float m_ImpactRadius;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;
        TrailRenderer m_Trail;

        public static BossProjectile Fire(BossKit kit, Vector3 position, Vector3 direction, float speed, float damage,
            GameObject owner, Color color, float size = 0.5f, float homing = 0f, float lifetime = 6f,
            float impactRadius = 0f)
        {
            if (!kit || !kit.ProjectilePrefab || direction.sqrMagnitude < 0.0001f)
                return null;
            BossProjectile projectile = PrefabPool.Get(kit.ProjectilePrefab, position,
                Quaternion.LookRotation(direction));
            projectile.Begin(kit, direction.normalized * speed, damage, owner, color, size, homing, lifetime,
                impactRadius);
            return projectile;
        }

        void Awake()
        {
            m_Renderer = GetComponentInChildren<MeshRenderer>();
            m_Trail = GetComponentInChildren<TrailRenderer>();
            m_Block = new MaterialPropertyBlock();
        }

        void Begin(BossKit kit, Vector3 velocity, float damage, GameObject owner, Color color, float size,
            float homing, float lifetime, float impactRadius)
        {
            m_Kit = kit;
            m_Velocity = velocity;
            m_Damage = damage;
            m_Owner = owner;
            m_Homing = homing;
            m_Life = lifetime;
            m_Radius = size * 0.5f;
            m_ImpactRadius = impactRadius;
            transform.localScale = Vector3.one * size;

            if (m_Renderer)
            {
                m_Block.SetColor(k_BaseColor, color);
                m_Renderer.SetPropertyBlock(m_Block);
            }
            if (m_Trail)
            {
                m_Trail.Clear();
                m_Trail.startColor = color;
                Color end = color;
                end.a = 0f;
                m_Trail.endColor = end;
                m_Trail.widthMultiplier = size * 0.8f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Life -= dt;
            if (m_Life <= 0f)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            if (m_Homing > 0f && BossTarget.Exists)
            {
                Vector3 toPlayer = BossTarget.Chest - transform.position;
                Vector3 direction = Vector3.RotateTowards(m_Velocity.normalized, toPlayer.normalized,
                    m_Homing * Mathf.Deg2Rad * dt, 0f);
                m_Velocity = direction * m_Velocity.magnitude;
            }

            Vector3 step = m_Velocity * (m_Kit.SpeedMultiplier * dt);
            float length = step.magnitude;
            if (length > 0f && CheckHit(transform.position, step / length, length))
                return;

            transform.position += step;
            if (step.sqrMagnitude > 0f)
                transform.forward = step;
        }

        bool CheckHit(Vector3 start, Vector3 direction, float length)
        {
            int count = Physics.SphereCastNonAlloc(start, m_Radius, direction, s_Hits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (s_Hits[i].distance >= bestDistance || s_Hits[i].collider.GetComponentInParent<EnemyStats>())
                    continue;
                best = i;
                bestDistance = s_Hits[i].distance;
            }

            if (best < 0)
                return false;

            RaycastHit hit = s_Hits[best];
            Vector3 point = hit.distance <= 0f ? start : hit.point;
            bool hitPlayer = hit.collider.GetComponentInParent<Unity.FPS.Gameplay.PlayerCharacterController>();
            if (m_ImpactRadius > 0f)
            {
                // Explosão: acerta o jogador dentro do raio (e marca o impacto no chão por um instante)
                if (hitPlayer || Vector3.Distance(BossTarget.Chest, point) <= m_ImpactRadius + 0.5f)
                    BossTarget.Damage(m_Damage, m_Owner, point);
                GroundTelegraph.Show(m_Kit, point, m_ImpactRadius, 0.12f, new Color(1f, 0.6f, 0.2f, 0.9f));
            }
            else if (hitPlayer)
            {
                BossTarget.Damage(m_Damage, m_Owner, point);
            }

            PrefabPool.Release(gameObject);
            return true;
        }
    }
}
