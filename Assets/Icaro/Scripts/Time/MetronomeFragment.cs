using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Fragmento de metrônomo: pickup que alguns inimigos deixam cair ao morrer (RoomManager). Flutua e gira onde o
    // inimigo morreu, com um feixe de luz para ser visto de longe. Quando o jogador encosta, reduz o multiplicador de
    // tempo em Reduction (CombatBalance.FragmentReduction, nunca abaixo de 1,0x). Some depois de Lifetime segundos,
    // piscando nos últimos BlinkTime, então pegar ou não é uma decisão no meio do combate. Reaproveitado pelo
    // PrefabPool. O jogador nunca é afetado pelo multiplicador: o giro e a flutuação usam o tempo normal.
    [RequireComponent(typeof(Collider))]
    public class MetronomeFragment : MonoBehaviour
    {
        [Tooltip("Parte que gira e flutua (o cristal)")]
        public Transform Visual;

        [Tooltip("Renderers e luz que piscam quando o fragmento está para sumir")]
        public Renderer[] BlinkRenderers;
        public Light Glow;

        public AudioClip PickupSfx;

        public float SpinSpeed = 90f;
        public float BobHeight = 0.2f;
        public float BobFrequency = 1.2f;

        const float k_GroundHeight = 1f;
        static readonly RaycastHit[] s_GroundHits = new RaycastHit[8];

        float m_Reduction = 0.5f;
        float m_Lifetime = 20f;
        float m_BlinkTime = 5f;
        float m_Age;
        Vector3 m_VisualRest;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            // CharacterController só dispara triggers de forma confiável contra um Rigidbody (como no Metrônomo)
            var body = GetComponent<Rigidbody>();
            if (!body)
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            if (Visual)
                m_VisualRest = Visual.localPosition;
        }

        // Chamado ao surgir: valores do CombatBalance e posição no chão abaixo de onde o inimigo morreu
        public void Drop(Vector3 position, float reduction, float lifetime, float blinkTime)
        {
            m_Reduction = reduction;
            m_Lifetime = lifetime;
            m_BlinkTime = blinkTime;
            m_Age = 0f;
            transform.position = GroundPoint(position) + Vector3.up * k_GroundHeight;
            SetVisible(true);
        }

        // Primeiro chão abaixo (ignora inimigos, como o Zangão no ar, e o jogador)
        static Vector3 GroundPoint(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 0.5f, Vector3.down, s_GroundHits, 60f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = position;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_GroundHits[i];
                if (hit.distance >= best || hit.collider.GetComponentInParent<EnemyStats>() ||
                    hit.collider.GetComponentInParent<PlayerCharacterController>())
                    continue;
                best = hit.distance;
                ground = hit.point;
            }

            return ground;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= m_Lifetime)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            if (Visual)
            {
                Visual.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
                Visual.localPosition = m_VisualRest +
                                       Vector3.up * (Mathf.Sin(m_Age * BobFrequency * Mathf.PI * 2f) * BobHeight);
            }

            // Pisca cada vez mais rápido nos últimos segundos
            float left = m_Lifetime - m_Age;
            if (left < m_BlinkTime)
            {
                float speed = Mathf.Lerp(10f, 3f, left / Mathf.Max(0.01f, m_BlinkTime));
                SetVisible(Mathf.Sin(m_Age * speed * Mathf.PI) > -0.3f);
            }
        }

        void SetVisible(bool visible)
        {
            if (BlinkRenderers != null)
            {
                foreach (Renderer renderer in BlinkRenderers)
                {
                    if (renderer)
                        renderer.enabled = visible;
                }
            }

            if (Glow)
                Glow.enabled = visible;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponentInParent<PlayerCharacterController>())
                return;

            if (TimeManager.Instance)
                TimeManager.Instance.Reduce(m_Reduction);
            if (PickupSfx)
                AudioUtility.CreateSFX(PickupSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);

            DisplayMessageEvent message = Events.DisplayMessageEvent;
            message.Message = $"Fragmento de metrônomo: -{m_Reduction:0.0}x".Replace('.', ',');
            message.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(message);

            PrefabPool.Release(gameObject);
        }
    }
}
