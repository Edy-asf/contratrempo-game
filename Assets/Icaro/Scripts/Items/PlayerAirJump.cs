using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Propulsor Auxiliar: pulos extras no ar (PlayerStat.AirJumps). Roda depois do PlayerCharacterController, que
    // só pula no chão: se o jogador está no ar e aperta pular, a velocidade vertical vira a do pulo normal. Os
    // pulos voltam ao tocar o chão.
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(PlayerCharacterController))]
    public class PlayerAirJump : MonoBehaviour
    {
        PlayerCharacterController m_Controller;
        PlayerInputHandler m_Input;
        PlayerStats m_Stats;
        int m_Used;

        void Awake()
        {
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
            m_Stats = GetComponent<PlayerStats>();
        }

        void Update()
        {
            if (!m_Input || !m_Stats || m_Controller.IsDead)
                return;

            if (m_Controller.IsGrounded)
            {
                m_Used = 0;
                return;
            }

            int available = Mathf.FloorToInt(m_Stats.Get(PlayerStat.AirJumps) + 0.01f);
            if (m_Controller.HasJumpedThisFrame || m_Used >= available || !m_Input.GetJumpInputDown())
                return;

            m_Used++;
            Vector3 velocity = m_Controller.CharacterVelocity;
            m_Controller.CharacterVelocity = new Vector3(velocity.x, m_Controller.JumpForce, velocity.z);
            if (m_Controller.AudioSource && m_Controller.JumpSfx)
                m_Controller.AudioSource.PlayOneShot(m_Controller.JumpSfx);
        }
    }
}
