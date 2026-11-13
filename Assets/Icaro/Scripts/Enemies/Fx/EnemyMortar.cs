using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Morteiro de esporos do Explorador Tomado: uma bola âmbar lançada em arco por cima da cobertura até onde o
    // jogador estava. No chão do alvo, uma marca em pixel art (aro vermelho tracejado e um aro âmbar fechando até o
    // centro) avisa durante todo o voo e pisca no fim; ao cair, explosão de esporos e dano em área. Tempo de voo
    // pelo aviso (nunca abaixo de 60%), no relógio dos inimigos. Sem colisão no caminho (vem por cima).
    public class EnemyMortar : MonoBehaviour
    {
        const int k_Ghosts = 3;

        static readonly Stack<EnemyMortar> s_Pool = new Stack<EnemyMortar>();
        static readonly List<EnemyMortar> s_Active = new List<EnemyMortar>();

        GameObject m_Owner;
        Vector3 m_From;
        Vector3 m_Target;
        Vector3 m_Normal;
        float m_Apex;
        float m_Flight;
        float m_Age;
        float m_Radius;
        float m_Damage;
        float m_GhostTimer;
        EnemyFxStyle m_Style;
        System.Action<Vector3> m_OnLand;
        SpriteRenderer m_Shell;
        GroundMarker m_Marker;
        readonly SpriteRenderer[] m_Ghosts = new SpriteRenderer[k_Ghosts];
        readonly Vector3[] m_GhostPositions = new Vector3[k_Ghosts];
        Sprite[] m_ShellFrames;
        Sprite[] m_MarkerFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pool.Clear();
            s_Active.Clear();
        }

        // target: ponto no chão (procura o chão logo abaixo); flight: segundos de voo (o aviso). style: Spore (o
        // morteiro do Explorador) ou Clod (o torrão do Pé-de-Musgo); onLand: chamado no ponto da queda
        public static void Launch(GameObject owner, Vector3 from, Vector3 target, float flight, float radius,
            float damage, EnemyFxStyle style = EnemyFxStyle.Spore, System.Action<Vector3> onLand = null)
        {
            Vector3 normal = Vector3.up;
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 12f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                target = ground.point;
                normal = ground.normal;
            }

            EnemyMortar mortar = null;
            while (s_Pool.Count > 0 && !mortar)
                mortar = s_Pool.Pop();
            if (!mortar)
                mortar = Create();

            mortar.m_Owner = owner;
            mortar.m_From = from;
            mortar.m_Target = target;
            mortar.m_Normal = normal;
            Vector3 flat = target - from;
            flat.y = 0f;
            mortar.m_Apex = Mathf.Max(5f, flat.magnitude * 0.4f);
            mortar.m_Flight = Mathf.Max(0.2f, flight);
            mortar.m_Age = 0f;
            mortar.m_Radius = radius;
            mortar.m_Damage = damage;
            mortar.m_GhostTimer = 0f;
            mortar.m_Style = style;
            mortar.m_OnLand = onLand;
            mortar.m_ShellFrames = EnemyFxSprites.Bolt(style);
            mortar.m_MarkerFrames = EnemyFxSprites.Marker(radius);
            Sprite[] ghosts = EnemyFxSprites.Ghost(style);
            for (int i = 0; i < k_Ghosts; i++)
            {
                mortar.m_Ghosts[i].sprite = ghosts[i];
                mortar.m_GhostPositions[i] = from;
            }

            mortar.m_Marker.Place(target, radius, mortar.m_MarkerFrames[0]);
            mortar.m_Shell.transform.position = from;
            mortar.gameObject.SetActive(true);
            s_Active.Add(mortar);
            ThreatSignals.Report(mortar.m_Marker.transform, mortar.m_Flight);
            EnemySounds.Play(EnemySounds.MortarWhistle, target, style == EnemyFxStyle.Clod ? 0.45f : 0.6f,
                (style == EnemyFxStyle.Clod ? 0.7f : 1.2f) / mortar.m_Flight, 0.03f, 6f);
        }

        static EnemyMortar Create()
        {
            var go = new GameObject("Morteiro_Esporos");
            var mortar = go.AddComponent<EnemyMortar>();
            mortar.m_Shell = EnemyFxSprites.CreateRenderer(go.transform, "Bola");
            mortar.m_Marker = GroundMarker.Create("Marca");
            mortar.m_Marker.transform.SetParent(go.transform, false);
            for (int i = 0; i < k_Ghosts; i++)
                mortar.m_Ghosts[i] = EnemyFxSprites.CreateRenderer(go.transform, "Eco");
            return mortar;
        }

        public static void ClearAll()
        {
            for (int i = s_Active.Count - 1; i >= 0; i--)
                if (s_Active[i])
                    s_Active[i].Release();
            s_Active.Clear();
        }

        Vector3 PositionAt(float u)
        {
            Vector3 p = Vector3.Lerp(m_From, m_Target, u);
            float top = Mathf.Max(m_From.y, m_Target.y) + m_Apex - Mathf.Lerp(m_From.y, m_Target.y, 0.5f);
            p.y += 4f * top * u * (1f - u);
            return p;
        }

        void Update()
        {
            float dt = CombatTiming.DeltaTime;
            if (dt <= 0f)
                return;
            m_Age += dt;
            float u = Mathf.Clamp01(m_Age / m_Flight);
            m_Shell.transform.position = PositionAt(u);

            m_GhostTimer -= Time.deltaTime;
            if (m_GhostTimer <= 0f)
            {
                m_GhostTimer = 1f / 30f;
                for (int i = k_Ghosts - 1; i > 0; i--)
                    m_GhostPositions[i] = m_GhostPositions[i - 1];
                m_GhostPositions[0] = PositionAt(Mathf.Max(0f, u - 0.03f));
            }

            if (u >= 1f)
                Land();
        }

        void LateUpdate()
        {
            float u = Mathf.Clamp01(m_Age / m_Flight);
            m_Shell.sprite = m_ShellFrames[Mathf.FloorToInt(m_Age * 12f) % m_ShellFrames.Length];
            int frame = Mathf.Clamp(Mathf.FloorToInt(u * m_MarkerFrames.Length), 0, m_MarkerFrames.Length - 1);
            // Pisca nos últimos 30% do voo, cada vez mais rápido
            float rate = Mathf.Lerp(6f, 16f, Mathf.InverseLerp(0.7f, 1f, u));
            m_Marker.SetFrame(m_MarkerFrames[frame], u, u < 0.7f || Mathf.FloorToInt(m_Age * rate) % 2 == 0);

            Camera camera = Camera.main;
            if (!camera)
                return;
            Quaternion facing = Quaternion.LookRotation(m_Shell.transform.position - camera.transform.position,
                camera.transform.up);
            m_Shell.transform.rotation = facing;
            for (int i = 0; i < k_Ghosts; i++)
            {
                m_Ghosts[i].transform.SetPositionAndRotation(m_GhostPositions[i], facing);
                m_Ghosts[i].enabled = m_Age > 0.05f;
            }
        }

        void Land()
        {
            Vector3 center = m_Target + m_Normal * (m_Radius * 0.55f);
            if (m_Style == EnemyFxStyle.Clod)
            {
                EnemyFxBurst.Play(EnemyFxSprites.Impact(EnemyFxStyle.Clod), center, 14f, m_Radius * 1.4f);
                EnemyFxBurst.PlayFlat(EnemyFxSprites.Scorch(EnemyFxStyle.Clod), m_Target, m_Normal, 1f, 5f,
                    m_Radius * 0.8f, 4f);
                EnemySounds.Play(EnemySounds.ClodHit, m_Target, 1f, 1f, 0.06f, 8f);
            }
            else
            {
                EnemyFxBurst.Play(EnemyFxSprites.Blast(m_Radius), center, 14f);
                EnemyFxBurst.PlayFlat(EnemyFxSprites.Scorch(EnemyFxStyle.Spore), m_Target, m_Normal, 1f, 4f, 1.5f, 4f);
                EnemySounds.Play(EnemySounds.MortarBlast, m_Target, 1f, 1f, 0.06f, 8f);
            }

            if (BossTarget.Exists && Vector3.Distance(BossTarget.Chest, m_Target + Vector3.up) <= m_Radius + 0.5f)
                BossTarget.Damage(m_Damage, m_Owner ? m_Owner : gameObject, BossTarget.Chest);
            System.Action<Vector3> onLand = m_OnLand;
            m_OnLand = null;
            Release();
            onLand?.Invoke(m_Target);
        }

        void Release()
        {
            s_Active.Remove(this);
            gameObject.SetActive(false);
            s_Pool.Push(this);
        }
    }
}
