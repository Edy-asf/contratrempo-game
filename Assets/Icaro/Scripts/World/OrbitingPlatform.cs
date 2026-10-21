using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Pedaço de anel levitando que gira em volta de um eixo vertical (o centro do anel de onde ele caiu), sempre
    // alinhado com a circunferência, como um carrossel (Cemitério de Anéis). O objeto fica no centro e a malha, no
    // raio. Quem está em pé em cima é levado junto (o jogador gira com ele em volta do centro, sem mudar para onde
    // olha); balança de leve. Fica fora do NavMesh e é chão não seguro (UnsafeGround). Roda antes do controle do
    // jogador.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(UnsafeGround))]
    public class OrbitingPlatform : MonoBehaviour
    {
        [Tooltip("Graus por segundo (negativo = sentido horário visto de cima)")]
        public float DegreesPerSecond = 4f;

        [Tooltip("Altura do balanço (m)")]
        public float Bob = 0.15f;

        public float BobSpeed = 0.7f;

        Vector3 m_Base;
        float m_Phase;
        PlayerCharacterController m_Player;
        CharacterController m_Controller;

        void Start()
        {
            m_Base = transform.position;
            m_Phase = transform.eulerAngles.y * 0.05f;
            m_Player = FindAnyObjectByType<PlayerCharacterController>();
            if (m_Player)
                m_Controller = m_Player.GetComponent<CharacterController>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            bool carrying = Carrying();
            Vector3 before = m_Player ? m_Player.transform.position : Vector3.zero;
            float step = DegreesPerSecond * dt;
            m_Phase += dt * BobSpeed;
            Vector3 position = m_Base + Vector3.up * (Mathf.Sin(m_Phase) * Bob);
            float lift = position.y - transform.position.y;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, step, 0f) * transform.rotation);

            if (!carrying || !m_Controller || !m_Controller.enabled)
                return;
            // O ponto do jogador gira em volta do centro junto com a peça (e sobe ou desce com o balanço)
            Vector3 offset = before - m_Base;
            Vector3 turned = Quaternion.Euler(0f, step, 0f) * new Vector3(offset.x, 0f, offset.z);
            Vector3 target = new Vector3(m_Base.x + turned.x, before.y + lift, m_Base.z + turned.z);
            m_Controller.Move(target - before);
        }

        // O jogador está em pé em cima desta peça
        bool Carrying()
        {
            if (!m_Player || !m_Player.IsGrounded)
                return false;
            Vector3 feet = m_Player.transform.position + Vector3.up * 0.3f;
            return Physics.Raycast(feet, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore) &&
                   hit.collider.transform.IsChildOf(transform);
        }
    }
}
