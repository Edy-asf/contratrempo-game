using System.Reflection;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Recuo da mira: cada tiro da arma na mão empurra a câmera para cima (e um pouco para um lado sorteado), com os
    // valores da ficha da arma (WeaponDefinition.AimKickUp/AimKickSide/AimRecovery). O empurrão entra suave em poucos
    // centésimos de segundo e, quando o jogador para de atirar, parte dele volta sozinha. Se o jogador já puxou a mira
    // para baixo para compensar, essa volta é cancelada (senão a mira passaria do ponto). O ângulo vertical é o do
    // PlayerCharacterController (campo privado, por reflexão, como no PlayerFreeMode); o horizontal gira o jogador.
    // Roda antes do controlador para o recuo aparecer no mesmo frame. Adicionado pelo PlayerStats.
    [DefaultExecutionOrder(-50)]
    public class AimRecoil : MonoBehaviour
    {
        public static AimRecoil Instance { get; private set; }

        [Tooltip("Multiplica o recuo de todas as armas (0 = desligado)")]
        [Range(0f, 3f)] public float Intensity = 1f;

        [Tooltip("Rapidez com que o empurrão de cada tiro entra (por segundo; maior = mais seco)")]
        public float Snap = 30f;

        [Tooltip("Segundos sem atirar até a mira começar a voltar")]
        public float RecoveryDelay = 0.12f;

        [Tooltip("Velocidade da volta da mira (graus por segundo)")]
        public float RecoverySpeed = 12f;

        static readonly FieldInfo s_PitchField =
            typeof(PlayerCharacterController).GetField("m_CameraVerticalAngle",
                BindingFlags.Instance | BindingFlags.NonPublic);

        PlayerCharacterController m_Controller;
        PlayerInputHandler m_Input;
        Vector2 m_Pending;        // empurrão ainda por aplicar (x = para cima, y = para a direita), graus
        float m_Recoverable;     // quanto da subida ainda volta sozinha
        float m_LastShot = -10f;

        void Awake()
        {
            Instance = this;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Chamado pela IcaroWeapon a cada tiro da arma na mão
        public void Kick(WeaponDefinition definition)
        {
            if (!definition || Intensity <= 0f)
                return;
            float up = definition.AimKickUp * Intensity;
            float side = Random.Range(-1f, 1f) * definition.AimKickSide * Intensity;
            m_Pending += new Vector2(up, side);
            m_Recoverable += up * definition.AimRecovery;
            m_LastShot = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !m_Controller || !m_Controller.enabled || s_PitchField == null)
            {
                if (m_Controller && !m_Controller.enabled)
                {
                    m_Pending = Vector2.zero;
                    m_Recoverable = 0f;
                }

                return;
            }

            float pitch = (float)s_PitchField.GetValue(m_Controller);
            float yaw = 0f;

            // Empurrão do tiro
            Vector2 step = m_Pending * (1f - Mathf.Exp(-Snap * dt));
            m_Pending -= step;
            pitch -= step.x;                    // ângulo positivo olha para baixo
            yaw += step.y;

            // O jogador puxando a mira para baixo já compensa o recuo
            if (m_Input && m_Input.GetLookInputsVertical() > 0f)
                m_Recoverable = Mathf.Max(0f, m_Recoverable - m_Input.GetLookInputsVertical() *
                    m_Controller.RotationSpeed * m_Controller.RotationMultiplier);

            // Volta depois de parar de atirar
            if (m_Recoverable > 0f && Time.time - m_LastShot > RecoveryDelay && m_Pending.x < 0.01f)
            {
                float back = Mathf.Min(m_Recoverable, RecoverySpeed * dt);
                m_Recoverable -= back;
                pitch += back;
            }

            s_PitchField.SetValue(m_Controller, Mathf.Clamp(pitch, -89f, 89f));
            if (Mathf.Abs(yaw) > 0f)
                m_Controller.transform.Rotate(0f, yaw, 0f, Space.Self);
        }
    }
}
