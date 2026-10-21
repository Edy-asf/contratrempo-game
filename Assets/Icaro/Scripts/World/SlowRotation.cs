using UnityEngine;

namespace Icaro
{
    // Gira devagar em volta de um eixo (no espaço do mundo) e sobe e desce de leve: anéis colossais levitando no céu do
    // Cemitério de Anéis. Só visual.
    public class SlowRotation : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;

        [Tooltip("Graus por segundo")]
        public float DegreesPerSecond = 2f;

        [Tooltip("Altura do sobe e desce (m)")]
        public float Bob = 1.5f;

        public float BobSpeed = 0.2f;

        Vector3 m_Base;
        float m_Phase;

        void Start()
        {
            m_Base = transform.position;
            m_Phase = transform.position.x * 0.01f;
        }

        void Update()
        {
            m_Phase += Time.deltaTime * BobSpeed;
            transform.Rotate(Axis, DegreesPerSecond * Time.deltaTime, Space.World);
            transform.position = m_Base + Vector3.up * (Mathf.Sin(m_Phase) * Bob);
        }
    }
}
