using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Aviso de que um inimigo vai surgir neste ponto: anel no chão que se fecha e feixe vertical que pulsa.
    // O RoomManager define a duração (1 s a 1,0x, com o piso de 60% dos avisos) e cria o inimigo ao fim dela.
    public class SpawnWarning : MonoBehaviour
    {
        public Material LineMaterial;

        [Tooltip("Cor da linha (0 a 1); o brilho HDR vem do material")]
        public Color Color = new Color(1f, 0.3f, 0.12f, 1f);

        public float StartRadius = 2.2f;
        public float EndRadius = 0.6f;
        public float BeamHeight = 7f;

        public AudioClip Sound;

        float m_Duration = 1f;
        float m_Time;
        TelegraphLine m_Ring;
        TelegraphLine m_Beam;

        public void Begin(float duration)
        {
            m_Duration = Mathf.Max(0.05f, duration);
            m_Time = 0f;
            if (Sound)
                AudioUtility.CreateSFX(Sound, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f, 6f);
        }

        void Awake()
        {
            m_Ring = TelegraphLine.Create(transform, LineMaterial, "Anel");
            m_Beam = TelegraphLine.Create(transform, LineMaterial, "Feixe");
        }

        void Update()
        {
            m_Time += Time.deltaTime;
            float t = Mathf.Clamp01(m_Time / m_Duration);
            float pulse = 0.6f + 0.4f * Mathf.Sin(m_Time * Mathf.Lerp(10f, 30f, t));

            m_Ring.SetStyle(Mathf.Lerp(0.08f, 0.18f, t), Color * pulse);
            m_Ring.SetCircle(transform.position, Mathf.Lerp(StartRadius, EndRadius, t));

            m_Beam.SetStyle(Mathf.Lerp(0.05f, 0.35f, t), Color * (0.4f + 0.6f * t) * pulse);
            m_Beam.SetSegment(transform.position, transform.position + Vector3.up * BeamHeight * t);

            if (m_Time >= m_Duration + 0.15f)
                Destroy(gameObject);
        }
    }
}
