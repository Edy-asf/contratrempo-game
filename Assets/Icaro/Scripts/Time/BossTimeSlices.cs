using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Chefe que baixa o multiplicador de tempo por fatias de vida tirada: a cada 'Slice' da vida máxima perdida, o
    // TimeManager tira 'Reduction' (BossDefinition.TimeSlice e TimeSliceReduction). Cada fatia conta uma vez só, mesmo
    // que o chefe se cure. O RoomManager põe este componente no chefe quando ele surge.
    public class BossTimeSlices : MonoBehaviour
    {
        Health m_Health;
        float m_Slice;
        float m_Reduction;
        int m_Given;

        public static void Attach(Health health, float slice, float reduction)
        {
            if (!health || slice <= 0f || reduction <= 0f)
                return;
            var slices = health.GetComponent<BossTimeSlices>();
            if (!slices)
                slices = health.gameObject.AddComponent<BossTimeSlices>();
            slices.m_Health = health;
            slices.m_Slice = slice;
            slices.m_Reduction = reduction;
            slices.m_Given = 0;
        }

        // Voltar no tempo (TimeRewind): a vida voltou e o multiplicador também; as fatias contam de novo daqui
        void OnTimeRewound()
        {
            if (m_Health && m_Health.MaxHealth > 0f && m_Slice > 0f)
                m_Given = Mathf.FloorToInt((1f - Mathf.Clamp01(m_Health.CurrentHealth / m_Health.MaxHealth)) / m_Slice + 0.0001f);
        }

        void Update()
        {
            if (!m_Health || m_Health.MaxHealth <= 0f)
                return;

            float lost = 1f - Mathf.Clamp01(m_Health.CurrentHealth / m_Health.MaxHealth);
            int slices = Mathf.FloorToInt(lost / m_Slice + 0.0001f);
            if (slices <= m_Given)
                return;

            Vector3 point = transform.position + Vector3.up * 3f;
            var collider = GetComponentInChildren<Collider>();
            if (collider)
                point = collider.bounds.center;
            float amount = (slices - m_Given) * m_Reduction;
            m_Given = slices;
            if (TimeManager.Instance)
                TimeManager.Instance.RewardTime(amount, point);
        }
    }
}
