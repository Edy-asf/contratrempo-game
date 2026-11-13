using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Nuvem de esporos do rugido do Pé-de-Musgo: fica no chão por um tempo, com dano contínuo em quem está dentro e a
    // vista curta (SporeVision). Visual em pixel art (EnemyFxArt.SporePuff): bolhas de névoa musgo que olham para a
    // câmera, crescem ao abrir, respiram e somem em pixels no fim. No relógio dos inimigos (para na volta no tempo e
    // desacelera na câmera lenta). Reaproveitada.
    public class SporeCloud : MonoBehaviour
    {
        const int k_Puffs = 7;
        const float k_TickTime = 0.25f;
        const float k_Height = 3.2f;

        static readonly Stack<SporeCloud> s_Pool = new Stack<SporeCloud>();
        static readonly List<SporeCloud> s_Active = new List<SporeCloud>();

        readonly SpriteRenderer[] m_Puffs = new SpriteRenderer[k_Puffs];
        readonly Vector3[] m_Offsets = new Vector3[k_Puffs];
        readonly float[] m_Phase = new float[k_Puffs];
        Sprite[] m_Frames;
        GameObject m_Owner;
        Vector3 m_Center;
        float m_Radius;
        float m_Duration;
        float m_Age;
        float m_Dps;
        float m_NextTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pool.Clear();
            s_Active.Clear();
        }

        // O jogador está dentro de alguma nuvem agora
        public static bool PlayerInside
        {
            get
            {
                if (!BossTarget.Exists)
                    return false;
                foreach (SporeCloud cloud in s_Active)
                    if (cloud && cloud.Contains(BossTarget.Feet))
                        return true;
                return false;
            }
        }

        public static void Spawn(GameObject owner, Vector3 position, float radius, float duration, float dps)
        {
            Vector3 ground = GroundTelegraph.GroundPoint(position);
            SporeCloud cloud = null;
            while (s_Pool.Count > 0 && !cloud)
                cloud = s_Pool.Pop();
            if (!cloud)
                cloud = Create();

            cloud.m_Owner = owner;
            cloud.m_Center = ground;
            cloud.m_Radius = radius;
            cloud.m_Duration = Mathf.Max(0.5f, duration);
            cloud.m_Dps = dps;
            cloud.m_Age = 0f;
            cloud.m_NextTick = 0.3f;
            cloud.m_Frames = EnemyFxSprites.SporePuff();
            cloud.transform.position = ground;
            for (int i = 0; i < k_Puffs; i++)
            {
                // um no meio e os outros em volta, em alturas um pouco diferentes
                float a = i * Mathf.PI * 2f / (k_Puffs - 1) + Random.Range(-0.3f, 0.3f);
                float r = i == 0 ? 0f : radius * Random.Range(0.45f, 0.75f);
                cloud.m_Offsets[i] = new Vector3(Mathf.Cos(a) * r, Random.Range(0.6f, 1.6f), Mathf.Sin(a) * r);
                cloud.m_Phase[i] = Random.value * 3f;
            }

            cloud.gameObject.SetActive(true);
            s_Active.Add(cloud);
            SporeVision.Ensure();
            EnemySounds.Play(EnemySounds.SporeCloud, ground, 0.8f, 1f, 0.08f, 6f);
        }

        public static void ClearAll()
        {
            for (int i = s_Active.Count - 1; i >= 0; i--)
                if (s_Active[i])
                    s_Active[i].Release();
            s_Active.Clear();
        }

        static SporeCloud Create()
        {
            var go = new GameObject("Nuvem_Esporos");
            var cloud = go.AddComponent<SporeCloud>();
            for (int i = 0; i < k_Puffs; i++)
                cloud.m_Puffs[i] = EnemyFxSprites.CreateRenderer(go.transform, "Bolha");
            return cloud;
        }

        bool Contains(Vector3 feet)
        {
            Vector3 d = feet - m_Center;
            if (d.y < -1.5f || d.y > k_Height)
                return false;
            d.y = 0f;
            return d.sqrMagnitude <= m_Radius * m_Radius;
        }

        void Update()
        {
            float dt = CombatTiming.DeltaTime;
            m_Age += dt;
            if (m_Age >= m_Duration)
            {
                Release();
                return;
            }

            m_NextTick -= dt;
            if (m_NextTick <= 0f)
            {
                m_NextTick += k_TickTime;
                if (BossTarget.Exists && Contains(BossTarget.Feet))
                    BossTarget.Damage(m_Dps * k_TickTime, m_Owner ? m_Owner : gameObject, BossTarget.Chest, true);
            }
        }

        void LateUpdate()
        {
            Camera camera = Camera.main;
            float grow = Mathf.Clamp01(m_Age / 0.45f);
            grow = 1f - (1f - grow) * (1f - grow);
            float left = m_Duration - m_Age;
            // some em degraus no último segundo (pixel art não desvanece liso)
            float fade = Mathf.Clamp01(left / 1f);
            float alpha = Mathf.Floor(fade * 4f) / 4f;
            for (int i = 0; i < k_Puffs; i++)
            {
                SpriteRenderer puff = m_Puffs[i];
                float breathe = 1f + 0.06f * Mathf.Sin((m_Age + m_Phase[i]) * 2.2f);
                float scale = (i == 0 ? 1.6f : 1.1f) * m_Radius / 2.2f * grow * breathe;
                Vector3 drift = new Vector3(Mathf.Sin(m_Age * 0.4f + m_Phase[i]) * 0.3f, 0f,
                    Mathf.Cos(m_Age * 0.3f + m_Phase[i]) * 0.3f);
                puff.transform.position = m_Center + m_Offsets[i] * grow + drift;
                puff.transform.localScale = Vector3.one * scale;
                if (camera)
                {
                    Vector3 away = puff.transform.position - camera.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.001f)
                        puff.transform.rotation = Quaternion.LookRotation(away);
                }

                puff.sprite = m_Frames[(Mathf.FloorToInt((m_Age + m_Phase[i]) * 3f) % m_Frames.Length)];
                puff.color = new Color(1f, 1f, 1f, alpha);
                puff.enabled = alpha > 0f;
            }
        }

        void Release()
        {
            s_Active.Remove(this);
            gameObject.SetActive(false);
            s_Pool.Push(this);
        }
    }
}
