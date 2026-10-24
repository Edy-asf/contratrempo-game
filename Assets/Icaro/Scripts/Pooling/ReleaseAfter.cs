using UnityEngine;

namespace Icaro
{
    // Devolve a instância ao PrefabPool depois de alguns segundos (efeitos de impacto reaproveitados)
    public class ReleaseAfter : MonoBehaviour
    {
        float m_Remaining;

        public void Begin(float seconds) => m_Remaining = seconds;

        void Update()
        {
            m_Remaining -= Time.deltaTime;
            if (m_Remaining <= 0f)
                PrefabPool.Release(gameObject);
        }
    }
}
