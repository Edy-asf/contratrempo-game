using UnityEngine;

namespace Icaro
{
    // Tremor leve da câmera do jogador (modelo de "trauma": cada evento soma trauma, que decai com o tempo; o tremor
    // é proporcional ao trauma ao quadrado). Aplicado no LateUpdate por cima da rotação que o
    // PlayerCharacterController define a cada frame, então nunca acumula. Pode ser desligado (Enabled, salvo).
    public class CameraShake : MonoBehaviour
    {
        const string k_PrefKey = "Icaro.TremorDaCamera";

        // Opção do jogador (menu de pausa), salva entre sessões
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(k_PrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(k_PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static CameraShake s_Instance;

        [Tooltip("Inclinação máxima (graus) em pitch, yaw e roll com trauma 1")]
        public Vector3 MaxAngles = new Vector3(2.2f, 2.2f, 3f);

        [Tooltip("Trauma perdido por segundo")]
        public float Decay = 1.8f;

        public float Frequency = 22f;

        float m_Trauma;
        float m_Seed;

        // Soma trauma (0 a 1) ao tremor atual
        public static void AddTrauma(float amount)
        {
            if (s_Instance && Enabled)
                s_Instance.m_Trauma = Mathf.Clamp01(s_Instance.m_Trauma + amount);
        }

        void OnEnable()
        {
            s_Instance = this;
            m_Seed = Random.value * 100f;
        }

        void OnDisable()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        void LateUpdate()
        {
            if (m_Trauma <= 0f)
                return;

            m_Trauma = Mathf.Max(0f, m_Trauma - Decay * Time.unscaledDeltaTime);
            if (!Enabled)
            {
                m_Trauma = 0f;
                return;
            }

            float shake = m_Trauma * m_Trauma;
            float t = Time.unscaledTime * Frequency;
            var angles = new Vector3(
                MaxAngles.x * shake * (Mathf.PerlinNoise(m_Seed, t) * 2f - 1f),
                MaxAngles.y * shake * (Mathf.PerlinNoise(m_Seed + 1f, t) * 2f - 1f),
                MaxAngles.z * shake * (Mathf.PerlinNoise(m_Seed + 2f, t) * 2f - 1f));
            transform.localRotation *= Quaternion.Euler(angles);
        }
    }
}
