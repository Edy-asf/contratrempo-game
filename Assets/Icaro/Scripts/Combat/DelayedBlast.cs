using UnityEngine;

namespace Icaro
{
    // Explosão com aviso, independente de quem a criou (o elite Explosivo morre antes dela): marca o chão e, no fim
    // do aviso, acerta o jogador dentro do raio. Usa a marca de chão do BossKit quando houver.
    public class DelayedBlast : MonoBehaviour
    {
        float m_Radius;
        float m_Damage;
        float m_Remaining;
        Vector3 m_Ground;

        public static void Create(Vector3 position, float radius, float damage, float warning, Color color,
            BossKit kit)
        {
            Vector3 ground = GroundTelegraph.Show(kit, position, radius, warning, color);
            var blast = new GameObject("ExplosaoElite").AddComponent<DelayedBlast>();
            blast.m_Ground = ground;
            blast.m_Radius = radius;
            blast.m_Damage = damage;
            blast.m_Remaining = warning;
            ThreatSignals.Report(blast.transform, warning);
            blast.transform.position = ground;
        }

        void Update()
        {
            m_Remaining -= Time.deltaTime;
            if (m_Remaining > 0f)
                return;

            if (BossTarget.Exists && Vector3.Distance(BossTarget.Chest, m_Ground + Vector3.up) <= m_Radius + 0.5f)
                BossTarget.Damage(m_Damage, gameObject, BossTarget.Chest);
            Destroy(gameObject);
        }
    }
}
