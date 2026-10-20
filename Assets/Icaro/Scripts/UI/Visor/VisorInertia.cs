using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Parallax / inércia do visor: o HUD segue a câmera com um atraso suave, como o peso do capacete. Ao virar, ele
    // fica um pouco para trás e volta amortecido (Smoothing); ao andar, escorrega levemente contra o movimento e
    // inclina nos passos laterais; ao cair e pousar, dá um pequeno solavanco. Fica no pivô do visor (filho da câmera,
    // na posição do olho), criado pelo HelmetVisor. Os números ficam no VisorSettings. Usa o tempo sem escala, então assenta mesmo com o jogo pausado.
    public class VisorInertia : MonoBehaviour
    {
        [Tooltip("Transform que o visor segue (a câmera)")]
        public Transform Target;

        [Tooltip("Ajustes (vazio = o asset Resources/VisorSettings)")]
        public VisorSettings Settings;

        Quaternion m_LastRotation;
        Vector2 m_Angle;
        Vector2 m_AngleVelocity;
        Vector3 m_Offset;
        Vector3 m_OffsetVelocity;
        float m_Roll;
        float m_RollVelocity;
        bool m_Started;
        PlayerCharacterController m_Controller;

        void LateUpdate()
        {
            if (!Target)
                return;
            float dt = Time.unscaledDeltaTime;
            if (!Settings)
                Settings = VisorSettings.Current;
            VisorSettings v = Settings;
            if (!m_Controller)
                m_Controller = Target.GetComponentInParent<PlayerCharacterController>();

            // Giro: a parte do giro deste frame que o visor deixa para trás, que depois volta amortecida
            Quaternion rotation = Target.rotation;
            if (m_Started)
            {
                Vector3 delta = (Quaternion.Inverse(m_LastRotation) * rotation).eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, delta.y), pitch = Mathf.DeltaAngle(0f, delta.x);
                // Uma virada enorme num frame (teleporte, recomeço) não puxa o visor
                if (Mathf.Abs(yaw) < 45f && Mathf.Abs(pitch) < 45f)
                    m_Angle -= new Vector2(yaw, pitch) * v.RotationLag * v.Strength;
            }

            m_LastRotation = rotation;
            m_Started = true;
            m_Angle = Vector2.ClampMagnitude(m_Angle, v.MaxAngle * v.Strength);
            m_Angle = Vector2.SmoothDamp(m_Angle, Vector2.zero, ref m_AngleVelocity, v.Smoothing, Mathf.Infinity, dt);

            // Movimento: escorrega contra a velocidade (no espaço da câmera) e inclina no passo lateral
            Vector3 targetOffset = Vector3.zero;
            float targetRoll = 0f;
            if (m_Controller && Time.timeScale > 0f)
            {
                Vector3 velocity = Target.InverseTransformDirection(m_Controller.CharacterVelocity);
                targetOffset = Vector3.ClampMagnitude(-velocity * v.MoveLag * v.Strength, v.MaxOffset * v.Strength);
                targetOffset.z = 0f;
                targetRoll = -velocity.x * v.StrafeRoll * v.Strength;
            }

            m_Offset = Vector3.SmoothDamp(m_Offset, targetOffset, ref m_OffsetVelocity, v.MoveSmoothing, Mathf.Infinity,
                dt);
            m_Roll = Mathf.SmoothDamp(m_Roll, targetRoll, ref m_RollVelocity, v.MoveSmoothing, Mathf.Infinity, dt);

            transform.localRotation = Quaternion.Euler(m_Angle.y, m_Angle.x, m_Roll);
            transform.localPosition = m_Offset;
        }
    }
}
