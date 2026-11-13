using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Jogador arremessado por um golpe (o agarrão do Pé-de-Musgo): fica sem controle (o PlayerCharacterController,
    // que também cuida do olhar, é desligado) e voa num arco pelo CharacterController, batendo em paredes e no domo da
    // arena; no fim, o controle volta com um pouco da velocidade. No tempo do jogador (não sofre o multiplicador nem
    // a câmera lenta dos inimigos). Adicionado ao jogador na primeira vez.
    public class PlayerThrow : MonoBehaviour
    {
        PlayerCharacterController m_Controller;
        CharacterController m_Character;
        Vector3 m_Velocity;
        float m_Time;
        bool m_DisabledController;

        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;

        // velocity: velocidade inicial (m/s); duration: segundos sem controle
        public static void Throw(Vector3 velocity, float duration)
        {
            PlayerCharacterController player = BossTarget.Player;
            if (!player || player.IsDead)
                return;
            var thrower = player.GetComponent<PlayerThrow>();
            if (!thrower)
                thrower = player.gameObject.AddComponent<PlayerThrow>();
            thrower.Begin(velocity, duration);
        }

        void Awake()
        {
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Character = GetComponent<CharacterController>();
        }

        void Begin(Vector3 velocity, float duration)
        {
            m_Velocity = velocity;
            m_Time = duration;
            if (m_Controller && m_Controller.enabled)
            {
                m_Controller.enabled = false;
                m_DisabledController = true;
            }

            Active = true;
            enabled = true;
            CameraShake.AddTrauma(0.6f);
        }

        void Update()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            m_Time -= dt;
            m_Velocity += Physics.gravity * dt;
            // um pouco de resistência para o voo não ir longe demais
            m_Velocity.x *= 1f - 0.8f * dt;
            m_Velocity.z *= 1f - 0.8f * dt;
            if (m_Character && m_Character.enabled)
            {
                CollisionFlags flags = m_Character.Move(m_Velocity * dt);
                if ((flags & CollisionFlags.Below) != 0 && m_Velocity.y < 0f)
                {
                    m_Velocity.y = 0f;
                    m_Velocity.x *= 1f - 6f * dt;
                    m_Velocity.z *= 1f - 6f * dt;
                }

                if ((flags & CollisionFlags.Sides) != 0)
                {
                    m_Velocity.x *= 0.3f;
                    m_Velocity.z *= 0.3f;
                }
            }

            if (m_Time <= 0f || (m_Controller && m_Controller.IsDead))
                End();
        }

        void End()
        {
            Active = false;
            enabled = false;
            if (m_DisabledController && m_Controller)
            {
                m_Controller.enabled = true;
                m_Controller.CharacterVelocity = new Vector3(m_Velocity.x * 0.3f, Mathf.Min(0f, m_Velocity.y),
                    m_Velocity.z * 0.3f);
            }

            m_DisabledController = false;
        }

        void OnDisable()
        {
            if (Active)
                End();
        }
    }
}
