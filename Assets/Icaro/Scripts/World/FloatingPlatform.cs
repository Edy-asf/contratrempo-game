using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Destroço de ruína boiando numa rota fixa (Arquipélago): anda pela linha de pontos 'Route' com velocidade
    // constante, em volta (Loop) ou indo e voltando com uma parada em cada ponta, e balança de leve na água. Quem está
    // em pé em cima é levado junto (o jogador; inimigos não usam: o destroço fica fora do NavMesh). É chão não seguro
    // (UnsafeGround). Roda antes do controle do jogador, que anda por cima já no lugar novo.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(UnsafeGround))]
    public class FloatingPlatform : MonoBehaviour
    {
        public Vector3[] Route;

        [Tooltip("Volta pelo primeiro ponto depois do último; sem isso, vai e volta")]
        public bool Loop;

        [Tooltip("Velocidade ao longo da rota (m/s)")]
        public float Speed = 2f;

        [Tooltip("Segundos parado em cada ponta (só indo e voltando)")]
        public float Pause = 1.5f;

        [Tooltip("Onde começa, em metros ao longo da rota")]
        public float Offset;

        [Tooltip("Altura do balanço na água (m)")]
        public float Bob = 0.08f;

        public float BobSpeed = 1.1f;

        float[] m_Lengths;
        float m_Total;
        float m_Distance;
        float m_Direction = 1f;
        float m_Paused;
        float m_Phase;
        PlayerCharacterController m_Player;
        CharacterController m_Controller;

        void Start()
        {
            if (Route == null || Route.Length < 2)
            {
                enabled = false;
                return;
            }

            int segments = Loop ? Route.Length : Route.Length - 1;
            m_Lengths = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                m_Lengths[i] = Vector3.Distance(Route[i], Route[(i + 1) % Route.Length]);
                m_Total += m_Lengths[i];
            }

            m_Distance = Loop ? Mathf.Repeat(Offset, m_Total) : Mathf.PingPong(Offset, m_Total);
            m_Phase = Offset * 0.37f;
            transform.position = PointAt(m_Distance);
            m_Player = FindAnyObjectByType<PlayerCharacterController>();
            if (m_Player)
                m_Controller = m_Player.GetComponent<CharacterController>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Total <= 0f)
                return;

            if (m_Paused > 0f)
                m_Paused -= dt;
            else if (Loop)
                m_Distance = Mathf.Repeat(m_Distance + Speed * dt, m_Total);
            else
            {
                m_Distance += Speed * dt * m_Direction;
                if (m_Distance >= m_Total || m_Distance <= 0f)
                {
                    m_Distance = Mathf.Clamp(m_Distance, 0f, m_Total);
                    m_Direction = -m_Direction;
                    m_Paused = Pause;
                }
            }

            m_Phase += dt * BobSpeed;
            Vector3 next = PointAt(m_Distance) + Vector3.up * (Mathf.Sin(m_Phase) * Bob);
            Vector3 delta = next - transform.position;
            bool carrying = Carrying();
            transform.position = next;
            if (carrying && m_Controller && m_Controller.enabled)
                m_Controller.Move(delta);
        }

        // O jogador está em pé em cima deste destroço
        bool Carrying()
        {
            if (!m_Player || !m_Player.IsGrounded)
                return false;
            Vector3 feet = m_Player.transform.position + Vector3.up * 0.3f;
            return Physics.Raycast(feet, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore) &&
                   hit.collider.transform.IsChildOf(transform);
        }

        Vector3 PointAt(float distance)
        {
            for (int i = 0; i < m_Lengths.Length; i++)
            {
                if (distance <= m_Lengths[i] || i == m_Lengths.Length - 1)
                {
                    float t = m_Lengths[i] > 0f ? Mathf.Clamp01(distance / m_Lengths[i]) : 0f;
                    return Vector3.Lerp(Route[i], Route[(i + 1) % Route.Length], t);
                }

                distance -= m_Lengths[i];
            }

            return Route[0];
        }
    }
}
