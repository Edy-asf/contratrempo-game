using UnityEngine;

namespace Icaro
{
    // Cristal de Cronita: sai do inimigo morto com um pulo, cai no chão e gira; quando o jogador chega a MagnetRadius,
    // voa até ele e entra na carteira. Some depois de CrystalLifetime (piscando no fim). Reaproveitado pelo
    // PrefabPool. O jogador nunca é afetado pelo multiplicador: o voo usa o tempo normal.
    public class CronitaPickup : MonoBehaviour
    {
        [Tooltip("Parte que gira (o cristal)")]
        public Transform Visual;

        public AudioClip PickupSfx;

        const float k_Gravity = 22f;
        const float k_GroundHeight = 0.5f;

        static readonly RaycastHit[] s_Hits = new RaycastHit[6];

        int m_Value;
        float m_Age;
        float m_Lifetime;
        float m_GroundY;
        bool m_Magnet;
        Vector3 m_Velocity;
        Renderer[] m_Renderers;

        // Espalha 'total' Cronitas em alguns cristais saindo de 'position'
        public static void Drop(CronitaPickup prefab, Vector3 position, int total, ShopSettings settings)
        {
            if (!prefab || total <= 0)
                return;
            int crystals = Mathf.Clamp(total, 1, settings ? settings.MaxCrystalsPerDrop : 5);
            int each = total / crystals;
            int extra = total % crystals;
            for (int i = 0; i < crystals; i++)
            {
                CronitaPickup crystal = PrefabPool.Get(prefab, position, Quaternion.identity);
                crystal.Begin(each + (i < extra ? 1 : 0), settings);
            }
        }

        void Awake()
        {
            m_Renderers = GetComponentsInChildren<Renderer>();
        }

        void Begin(int value, ShopSettings settings)
        {
            m_Value = value;
            m_Age = 0f;
            m_Lifetime = settings ? settings.CrystalLifetime : 45f;
            m_Magnet = false;
            Vector2 side = Random.insideUnitCircle * 3.5f;
            m_Velocity = new Vector3(side.x, Random.Range(5f, 8f), side.y);
            m_GroundY = GroundBelow(transform.position) + k_GroundHeight;
            transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.4f, Mathf.Clamp01(value / 20f));
            SetVisible(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Age += dt;
            if (m_Age >= m_Lifetime)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            if (Visual)
                Visual.Rotate(Vector3.up, 180f * dt, Space.World);

            ShopSettings settings = ShopDirector.Settings;
            if (BossTarget.Exists && m_Age > 0.35f)
            {
                Vector3 toPlayer = BossTarget.Chest - transform.position;
                float distance = toPlayer.magnitude;
                float magnetRadius = settings.MagnetRadius *
                                     (PlayerStats.Instance ? PlayerStats.Instance.PickupRangeMultiplier : 1f);
                if (!m_Magnet && distance <= magnetRadius)
                    m_Magnet = true;
                if (m_Magnet)
                {
                    float speed = settings.MagnetSpeed * (1f + m_Age * 0.5f);
                    if (distance <= Mathf.Max(0.6f, speed * dt))
                    {
                        Collect();
                        return;
                    }
                    transform.position += toPlayer / distance * (speed * dt);
                    return;
                }
            }

            // Pulo e queda até o chão
            m_Velocity.y -= k_Gravity * dt;
            Vector3 position = transform.position + m_Velocity * dt;
            if (position.y <= m_GroundY)
            {
                position.y = m_GroundY + Mathf.Sin(m_Age * 3f) * 0.1f;
                m_Velocity = Vector3.zero;
            }
            transform.position = position;

            // Pisca nos últimos 5 s
            float left = m_Lifetime - m_Age;
            if (left < 5f)
                SetVisible(Mathf.Sin(m_Age * Mathf.Lerp(20f, 6f, left / 5f)) > -0.2f);
        }

        void Collect()
        {
            if (PlayerWallet.Instance)
                PlayerWallet.Instance.Add(m_Value);
            if (PickupSfx)
                Unity.FPS.Game.AudioUtility.CreateSFX(PickupSfx, transform.position,
                    Unity.FPS.Game.AudioUtility.AudioGroups.Pickup, 0f);
            PrefabPool.Release(gameObject);
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer renderer in m_Renderers)
            {
                if (renderer)
                    renderer.enabled = visible;
            }
        }

        // Chão abaixo (ignora inimigos e o jogador)
        static float GroundBelow(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up, Vector3.down, s_Hits, 40f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float ground = position.y - 1f;
            for (int i = 0; i < count; i++)
            {
                Collider collider = s_Hits[i].collider;
                if (s_Hits[i].distance >= best || collider.GetComponentInParent<EnemyStats>() ||
                    collider.GetComponentInParent<Unity.FPS.Gameplay.PlayerCharacterController>())
                    continue;
                best = s_Hits[i].distance;
                ground = s_Hits[i].point.y;
            }

            return ground;
        }
    }
}
