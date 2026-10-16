using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Guardião Tiki (chefe das Ruínas). Flutua e alterna três ataques, sem repetir o mesmo duas vezes seguidas:
    // feixes de runa que giram em volta dele (baixos: pular; altos na fase 2: agachar), espiral de projéteis (na fase
    // 2, uma segunda espiral no sentido contrário) e salto de runa (some e reaparece em outro ponto soltando um anel).
    // Valores no TribalDefinition.
    public class TribalAI : BossAI
    {
        static readonly Color k_Rune = new Color(0.3f, 0.95f, 1f, 0.95f);

        TribalDefinition m_Def;
        int m_Last = -1;

        protected override void Start()
        {
            base.Start();
            m_Def = Definition as TribalDefinition;
            if (m_Def)
                HoverHeight = m_Def.HoverHeight;
        }

        protected override IEnumerator NextAttack()
        {
            if (!m_Def)
                yield break;

            int attack;
            do
            {
                attack = Random.Range(0, 3);
            } while (attack == m_Last);
            m_Last = attack;

            switch (attack)
            {
                case 0: yield return RuneBeams(); break;
                case 1: yield return Spiral(); break;
                default: yield return RuneBlink(); break;
            }
        }

        // Feixes que giram em volta dele; na fase 2 metade fica alta (agachar) e metade baixa (pular)
        IEnumerator RuneBeams()
        {
            HoldPosition = true;
            int count = Phase >= 2 ? m_Def.Beams.y : m_Def.Beams.x;
            float warning = WarningTime(m_Def.BeamWarning);
            Threat(warning + m_Def.BeamDuration);
            Squash(-0.3f);

            // Aviso: marca no chão do raio dos feixes
            GroundTelegraph.Show(Kit, transform.position, m_Def.BeamLength, warning, k_Rune);
            yield return new WaitForSeconds(warning);

            float start = Random.Range(0f, 360f);
            float direction = Random.value < 0.5f ? 1f : -1f;
            float duration = CombatTiming.Scaled(m_Def.BeamDuration);
            for (int i = 0; i < count; i++)
            {
                bool high = Phase >= 2 && i % 2 == 1;
                Beam(m_Def.BeamLength, high ? m_Def.BeamHighHeight : m_Def.BeamLowHeight, m_Def.BeamSpeed * direction,
                    start + i * 360f / count, duration, m_Def.BeamDamage);
            }

            // Enquanto os feixes giram, uma espiral leve na fase 2
            float end = Time.time + duration;
            float angle = 0f;
            while (Time.time < end)
            {
                if (Phase >= 2)
                {
                    angle += 37f;
                    Shoot(RingOrigin, Quaternion.Euler(0f, angle, 0f) * Vector3.forward, m_Def.SpiralBulletSpeed,
                        m_Def.SpiralDamage, 0.5f);
                }
                yield return Wait(0.35f);
            }
        }

        // Espiral: braços de projéteis girando; na fase 2, uma segunda espiral no sentido contrário
        IEnumerator Spiral()
        {
            HoldPosition = true;
            Threat(m_Def.SpiralDuration);
            Squash(0.3f);
            int arms = Phase >= 2 ? m_Def.SpiralArms.y : m_Def.SpiralArms.x;
            float interval = 1f / m_Def.SpiralShotsPerSecond;
            float angle = Random.Range(0f, 360f);
            float duration = CombatTiming.Scaled(m_Def.SpiralDuration);
            for (float t = 0f; t < duration; t += CombatTiming.Scaled(interval))
            {
                for (int a = 0; a < arms; a++)
                {
                    float armAngle = angle + a * 360f / arms;
                    Shoot(RingOrigin, Quaternion.Euler(0f, armAngle, 0f) * Vector3.forward,
                        m_Def.SpiralBulletSpeed, m_Def.SpiralDamage, 0.5f);
                    if (Phase >= 2)
                        Shoot(RingOrigin, Quaternion.Euler(0f, -armAngle * 1.3f, 0f) * Vector3.forward,
                            m_Def.SpiralBulletSpeed * 0.8f, m_Def.SpiralDamage, 0.45f);
                }

                angle += m_Def.SpiralTurnSpeed * interval;
                yield return Wait(interval);
            }
        }

        // Salto de runa: encolhe, some, reaparece em outro ponto e solta um anel
        IEnumerator RuneBlink()
        {
            HoldPosition = true;
            Vector3 target = PointAroundPlayer(m_Def.BlinkRange);
            float warning = WarningTime(m_Def.BlinkWarning);
            GroundTelegraph.Show(Kit, target, 3f, warning, k_Rune);
            Threat(warning + 0.5f);
            Squash(0.45f);
            yield return new WaitForSeconds(warning);

            bool agent = Agent && Agent.enabled;
            if (agent)
                Agent.enabled = false;
            transform.position = target;
            if (agent)
            {
                Agent.enabled = true;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    Agent.Warp(hit.position);
            }

            Squash(-0.45f);
            int rings = Phase >= 2 ? 2 : 1;
            for (int r = 0; r < rings; r++)
            {
                Ring(RingOrigin, m_Def.BlinkRingBullets, m_Def.BlinkRingSpeed, m_Def.BlinkRingDamage,
                    r * 180f / m_Def.BlinkRingBullets, 0, 0, 0.6f);
                yield return Wait(0.35f);
            }
        }
    }
}
