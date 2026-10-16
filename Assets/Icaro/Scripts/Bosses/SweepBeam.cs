using UnityEngine;

namespace Icaro
{
    // Feixe giratório de chefe: uma barra de luz que sai do chefe e gira em volta dele numa altura fixa. Feixe baixo
    // (Height ~0,5 m): é preciso pular por cima. Feixe alto (~1,5 m): é preciso agachar (C). Acerta o jogador no
    // máximo uma vez a cada 0,6 s. A velocidade de giro segue o multiplicador até o teto do BossKit. Reaproveitado
    // pelo PrefabPool.
    public class SweepBeam : MonoBehaviour
    {
        [Tooltip("Barra visual (cubo de 1 m, esticado pelo componente)")]
        public Transform Bar;

        const float k_Thickness = 0.35f;
        const float k_HitCooldown = 0.6f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        BossKit m_Kit;
        Transform m_Center;
        GameObject m_Owner;
        float m_Length;
        float m_Height;
        float m_Speed;
        float m_Angle;
        float m_Life;
        float m_Damage;
        float m_NextHit;
        float m_GroundY;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        public static SweepBeam Spawn(BossKit kit, Transform center, float groundY, float length, float height,
            float degreesPerSecond, float startAngle, float duration, float damage, GameObject owner, Color color)
        {
            if (!kit || !kit.BeamPrefab)
                return null;
            SweepBeam beam = PrefabPool.Get(kit.BeamPrefab, center.position, Quaternion.identity);
            beam.Begin(kit, center, groundY, length, height, degreesPerSecond, startAngle, duration, damage, owner,
                color);
            return beam;
        }

        void Awake()
        {
            m_Renderer = Bar ? Bar.GetComponent<Renderer>() : null;
            m_Block = new MaterialPropertyBlock();
        }

        void Begin(BossKit kit, Transform center, float groundY, float length, float height, float speed,
            float startAngle, float duration, float damage, GameObject owner, Color color)
        {
            m_Kit = kit;
            m_Center = center;
            m_GroundY = groundY;
            m_Length = length;
            m_Height = height;
            m_Speed = speed;
            m_Angle = startAngle;
            m_Life = duration;
            m_Damage = damage;
            m_Owner = owner;
            m_NextHit = 0f;
            if (m_Renderer)
            {
                m_Block.SetColor(k_BaseColor, color);
                m_Renderer.SetPropertyBlock(m_Block);
            }
            Place();
        }

        // Some antes da hora (chefe morreu ou mudou de ataque)
        public void Stop() => m_Life = 0f;

        void Update()
        {
            m_Life -= Time.deltaTime;
            if (m_Life <= 0f || !m_Center)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            m_Angle += m_Speed * m_Kit.SpeedMultiplier * Time.deltaTime;
            Place();
            CheckPlayer();
        }

        void Place()
        {
            Vector3 center = m_Center.position;
            transform.SetPositionAndRotation(new Vector3(center.x, m_GroundY + m_Height, center.z),
                Quaternion.Euler(0f, m_Angle, 0f));
            if (Bar)
            {
                Bar.localPosition = new Vector3(0f, 0f, m_Length * 0.5f);
                Bar.localScale = new Vector3(k_Thickness, k_Thickness, m_Length);
            }
        }

        // Acerta se o jogador está perto da linha do feixe (no plano) e o corpo cruza a altura dele
        void CheckPlayer()
        {
            if (!BossTarget.Exists || Time.time < m_NextHit)
                return;

            Vector3 origin = transform.position;
            Vector3 toPlayer = BossTarget.Feet - origin;
            toPlayer.y = 0f;
            Vector3 along = transform.forward;
            float projection = Vector3.Dot(toPlayer, along);
            if (projection < 0f || projection > m_Length)
                return;
            float side = (toPlayer - along * projection).magnitude;
            if (side > 0.55f)
                return;

            float beamY = origin.y;
            float feet = BossTarget.Feet.y;
            float head = feet + BossTarget.BodyHeight;
            if (beamY + k_Thickness * 0.5f < feet || beamY - k_Thickness * 0.5f > head)
                return;

            m_NextHit = Time.time + k_HitCooldown;
            BossTarget.Damage(m_Damage, m_Owner, new Vector3(BossTarget.Feet.x, beamY, BossTarget.Feet.z));
        }
    }
}
