using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Ponte que desaba de vez durante a luta (Arquipélago): quando a sala (Room) já derrotou a fração CollapseAt da
    // cota, a ponte avisa por WarningTime segundos (os pedaços tremem e piscam em âmbar, e o visor mostra um aviso) e
    // cai pedaço por pedaço a partir do meio. Ao cair, os colisores somem (quem estiver em cima cai no mar) e um
    // NavMeshObstacle corta a ponte do NavMesh (os inimigos passam a dar a volta). É chão não seguro (UnsafeGround).
    // Montada pelo gerador do bioma: cada pedaço é um filho com a malha centrada nele.
    [RequireComponent(typeof(UnsafeGround))]
    public class CollapsingBridge : MonoBehaviour
    {
        public RoomManager Room;

        [Tooltip("Fração da cota da sala já derrotada em que a ponte começa a desabar")]
        [Range(0f, 1f)] public float CollapseAt = 0.5f;

        [Tooltip("Segundos de aviso antes de cair")]
        public float WarningTime = 3f;

        [Tooltip("Segundos de queda de cada pedaço")]
        public float FallTime = 2.5f;

        [Tooltip("Atraso entre um pedaço e o seguinte (do meio para as pontas)")]
        public float SegmentDelay = 0.12f;

        public Transform[] Segments;

        public Color WarningColor = new Color(1f, 0.55f, 0.15f);

        enum State
        {
            Standing,
            Warning,
            Falling,
            Gone,
        }

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        State m_State;
        float m_Since;
        Vector3[] m_RestPosition;
        Quaternion[] m_RestRotation;
        Vector3[] m_Spin;
        float[] m_Delay;
        Renderer[] m_Renderers;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            int count = Segments != null ? Segments.Length : 0;
            m_RestPosition = new Vector3[count];
            m_RestRotation = new Quaternion[count];
            m_Spin = new Vector3[count];
            m_Delay = new float[count];
            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                m_RestPosition[i] = Segments[i].position;
                m_RestRotation[i] = Segments[i].rotation;
                m_Spin[i] = Random.insideUnitSphere * 70f;
                m_Delay[i] = Mathf.Abs(i - middle) * SegmentDelay;
            }

            m_Renderers = GetComponentsInChildren<Renderer>();
            m_Block = new MaterialPropertyBlock();
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle)
                obstacle.enabled = false;
        }

        void Update()
        {
            m_Since += Time.deltaTime;
            switch (m_State)
            {
                case State.Standing:
                    if (Progress() >= CollapseAt)
                        StartWarning();
                    break;
                case State.Warning:
                    Shake();
                    if (m_Since >= WarningTime)
                        StartFall();
                    break;
                case State.Falling:
                    Fall();
                    break;
            }
        }

        // Fração da cota da sala já derrotada (0 fora de combate)
        float Progress()
        {
            if (!Room || Room.State != RoomManager.RoomState.Active || Room.TotalEnemies <= 0)
                return 0f;
            return 1f - Mathf.Clamp01(Room.RemainingEnemies / (float)Room.TotalEnemies);
        }

        void StartWarning()
        {
            m_State = State.Warning;
            m_Since = 0f;
            VisorBanner.Show("Ponte desabando", "Saia da ponte", BannerTone.Warning);
        }

        // Tremor que cresce até a queda e pisca em âmbar cada vez mais rápido
        void Shake()
        {
            float t = Mathf.Clamp01(m_Since / WarningTime);
            float amplitude = Mathf.Lerp(0.02f, 0.12f, t);
            for (int i = 0; i < Segments.Length; i++)
            {
                Segments[i].position = m_RestPosition[i] + Random.insideUnitSphere * amplitude;
                Segments[i].rotation = m_RestRotation[i] * Quaternion.Euler(Random.insideUnitSphere * amplitude * 8f);
            }

            float blink = Mathf.Abs(Mathf.Sin(m_Since * Mathf.Lerp(5f, 16f, t)));
            Tint(Color.Lerp(Color.white, WarningColor, blink * 0.8f));
        }

        void StartFall()
        {
            m_State = State.Falling;
            m_Since = 0f;
            Tint(Color.white);
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle)
                obstacle.enabled = true;
        }

        void Fall()
        {
            bool done = true;
            for (int i = 0; i < Segments.Length; i++)
            {
                float t = m_Since - m_Delay[i];
                if (t < FallTime)
                    done = false;
                if (t <= 0f)
                    continue;
                if (t >= FallTime)
                {
                    if (Segments[i].gameObject.activeSelf)
                        Segments[i].gameObject.SetActive(false);
                    continue;
                }

                Segments[i].position = m_RestPosition[i] + Vector3.down * (0.5f * 14f * t * t);
                Segments[i].rotation = m_RestRotation[i] * Quaternion.Euler(m_Spin[i] * t);
            }

            if (done)
                m_State = State.Gone;
        }

        void Tint(Color color)
        {
            foreach (Renderer renderer in m_Renderers)
            {
                if (!renderer)
                    continue;
                renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, color);
                renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
