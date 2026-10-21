using UnityEngine;

namespace Icaro
{
    // Luz que pisca como um equipamento danificado (a luz da escotilha da cápsula de evacuação): fica acesa com uma
    // leve oscilação e, de vez em quando, falha e apaga por um instante. Só visual.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("Falhas por segundo, em média")]
        public float FailuresPerSecond = 0.6f;

        Light m_Light;
        float m_Base;
        float m_OffUntil;

        void Awake()
        {
            m_Light = GetComponent<Light>();
            m_Base = m_Light.intensity;
        }

        void Update()
        {
            float t = Time.time;
            if (t >= m_OffUntil && Random.value < FailuresPerSecond * Time.deltaTime)
                m_OffUntil = t + Random.Range(0.05f, 0.25f);
            float hum = 0.85f + 0.15f * Mathf.PerlinNoise(t * 6f, 0.3f);
            m_Light.intensity = t < m_OffUntil ? m_Base * 0.1f : m_Base * hum;
        }
    }
}
