using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Tiro dos inimigos em pixel art (Explorador Tomado e Mariposa-lanterna), no lugar dos projéteis 3D do Microgame:
    // um sprite que treme em 3 quadros, de frente para a câmera e deitado ao longo da direção do voo, com 3 ecos
    // atrás que só se movem 30 vezes por segundo (rastro em degraus, sem partícula lisa). Anda em linha reta com a
    // velocidade vezes o multiplicador, no relógio dos inimigos, e testa o trecho do frame com raycast: passa por
    // inimigos, acerta o jogador pelo evento central de dano e, em parede ou chão, deixa o impacto e um queimado.
    // Criado e reaproveitado aqui mesmo (Fire), sem prefab.
    public class EnemyBolt : MonoBehaviour
    {
        const int k_Ghosts = 3;
        const float k_GhostStep = 1f / 30f;
        const int k_HitBuffer = 8;

        static readonly Stack<EnemyBolt> s_Pool = new Stack<EnemyBolt>();
        static readonly List<EnemyBolt> s_Active = new List<EnemyBolt>();
        static readonly RaycastHit[] s_Hits = new RaycastHit[k_HitBuffer];

        EnemyFxStyle m_Style;
        GameObject m_Owner;
        Vector3 m_Direction;
        float m_Speed;
        float m_Damage;
        float m_Life;
        float m_Age;
        float m_Travelled;
        float m_GhostTimer;
        SpriteRenderer m_Head;
        readonly SpriteRenderer[] m_Ghosts = new SpriteRenderer[k_Ghosts];
        readonly Vector3[] m_GhostPositions = new Vector3[k_Ghosts];
        Sprite[] m_HeadFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pool.Clear();
            s_Active.Clear();
        }

        public static void Fire(EnemyFxStyle style, GameObject owner, Vector3 origin, Vector3 direction, float speed,
            float damage, float lifetime)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;
            EnemyBolt bolt = null;
            while (s_Pool.Count > 0 && !bolt)
                bolt = s_Pool.Pop();
            if (!bolt)
                bolt = Create();

            bolt.m_Style = style;
            bolt.m_Owner = owner;
            bolt.m_Direction = direction.normalized;
            bolt.m_Speed = speed;
            bolt.m_Damage = damage;
            bolt.m_Life = lifetime;
            bolt.m_Age = 0f;
            bolt.m_Travelled = 0f;
            bolt.m_GhostTimer = 0f;
            bolt.m_HeadFrames = EnemyFxSprites.Bolt(style);
            Sprite[] ghosts = EnemyFxSprites.Ghost(style);
            bolt.m_Head.sprite = bolt.m_HeadFrames[0];
            for (int i = 0; i < k_Ghosts; i++)
            {
                bolt.m_Ghosts[i].sprite = ghosts[i];
                bolt.m_Ghosts[i].enabled = false;
                bolt.m_GhostPositions[i] = origin;
            }

            bolt.transform.position = origin;
            bolt.gameObject.SetActive(true);
            s_Active.Add(bolt);
            bolt.Orient();
        }

        static EnemyBolt Create()
        {
            var go = new GameObject("Tiro_Inimigo");
            var bolt = go.AddComponent<EnemyBolt>();
            bolt.m_Head = EnemyFxSprites.CreateRenderer(go.transform, "Cabeca");
            for (int i = 0; i < k_Ghosts; i++)
            {
                bolt.m_Ghosts[i] = EnemyFxSprites.CreateRenderer(go.transform, "Eco");
            }

            return bolt;
        }

        // Some com todos (volta no tempo)
        public static void ClearAll()
        {
            for (int i = s_Active.Count - 1; i >= 0; i--)
                if (s_Active[i])
                    s_Active[i].Release();
            s_Active.Clear();
        }

        void Update()
        {
            float dt = CombatTiming.DeltaTime;
            if (dt <= 0f)
                return;
            m_Life -= dt;
            m_Age += dt;
            if (m_Life <= 0f)
            {
                Release();
                return;
            }

            float step = m_Speed * TimeManager.CurrentMultiplier * dt;
            Vector3 start = transform.position;
            if (FindHit(start, step, out RaycastHit hit))
            {
                Impact(hit);
                return;
            }

            transform.position = start + m_Direction * step;
            m_Travelled += step;

            // Ecos: só andam a cada 1/30 s, cada um onde o anterior estava
            m_GhostTimer -= Time.deltaTime;
            if (m_GhostTimer <= 0f)
            {
                m_GhostTimer = k_GhostStep;
                for (int i = k_Ghosts - 1; i > 0; i--)
                    m_GhostPositions[i] = m_GhostPositions[i - 1];
                float gap = m_Style == EnemyFxStyle.Plasma ? 0.42f : 0.26f;
                m_GhostPositions[0] = transform.position - m_Direction * Mathf.Min(gap, m_Travelled);
                for (int i = 0; i < k_Ghosts; i++)
                {
                    // Nunca atrás do cano
                    float back = Vector3.Dot(transform.position - m_GhostPositions[i], m_Direction);
                    m_Ghosts[i].enabled = back > 0.05f && back <= m_Travelled;
                }
            }
        }

        void LateUpdate()
        {
            m_Head.sprite = m_HeadFrames[Mathf.FloorToInt(m_Age * 20f) % m_HeadFrames.Length];
            for (int i = 0; i < k_Ghosts; i++)
                m_Ghosts[i].transform.position = m_GhostPositions[i];
            Orient();
        }

        // De frente para a câmera e deitado ao longo do voo; visto de ponta, encolhe
        void Orient()
        {
            Camera camera = Camera.main;
            if (!camera)
                return;
            Quaternion rotation = EnemyFxSprites.FacingAlong(transform.position, m_Direction, camera, out float shown);
            float length = Mathf.Lerp(0.5f, 1f, shown);
            m_Head.transform.rotation = rotation;
            m_Head.transform.localScale = new Vector3(length, 1f, 1f);
            for (int i = 0; i < k_Ghosts; i++)
            {
                m_Ghosts[i].transform.rotation = rotation;
                m_Ghosts[i].transform.localScale = new Vector3(length, 1f, 1f);
            }
        }

        // O acerto mais perto no trecho que não seja um inimigo (o tiro passa por eles)
        bool FindHit(Vector3 start, float step, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(start, m_Direction, s_Hits, step, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_Hits[i];
                if (hit.distance >= nearest || hit.collider.GetComponentInParent<EnemyStats>())
                    continue;
                nearest = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        void Impact(RaycastHit hit)
        {
            bool player = hit.collider.GetComponentInParent<PlayerCharacterController>();
            if (player)
            {
                var damageable = hit.collider.GetComponentInParent<Damageable>();
                if (damageable)
                    CombatEvents.ApplyDamage(damageable, m_Damage, m_Owner, hit.point);
            }

            bool plasma = m_Style == EnemyFxStyle.Plasma;
            Vector3 point = hit.point - m_Direction * 0.15f;
            EnemyFxBurst.Play(EnemyFxSprites.Impact(m_Style), point, 24f);
            // No domo da arena (parede invisível) não fica queimado
            if (!player && !hit.collider.GetComponentInParent<ArenaDome>())
            {
                EnemyFxBurst.PlayFlat(EnemyFxSprites.Scorch(m_Style), hit.point, hit.normal, 1f, 2.5f, 1f,
                    plasma ? 1f : 0.7f);
            }

            EnemySounds.Play(plasma ? EnemySounds.PlasmaHit : EnemySounds.MothHit, point, plasma ? 0.8f : 0.55f,
                1f, 0.08f, plasma ? 3f : 2f);
            Release();
        }

        void Release()
        {
            s_Active.Remove(this);
            gameObject.SetActive(false);
            s_Pool.Push(this);
        }
    }
}
