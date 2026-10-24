using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Sapo Rei do Brejo (chefe do Charco). Não anda: se desloca só aos saltos. Alterna quatro ataques, sem repetir o
    // mesmo duas vezes seguidas: chuva ácida (gotas em marcas no chão que deixam poças), bolhas teleguiadas,
    // barrigada (salta para longe e a queda solta ondas de choque em sequência: pular cada uma) e cuspida em leque.
    // Na fase 2 tudo fica mais denso. Valores no SapoDefinition.
    public class SapoAI : BossAI
    {
        static readonly Color k_Acid = new Color(0.55f, 1f, 0.25f, 0.9f);

        SapoDefinition m_Def;
        int m_Last = -1;

        protected override void Start()
        {
            base.Start();
            m_Def = Definition as SapoDefinition;
            // Só se desloca aos saltos
            if (Agent)
                Agent.speed = 0f;
        }

        protected override IEnumerator NextAttack()
        {
            if (!m_Def)
                yield break;

            int attack;
            do
            {
                attack = Random.Range(0, 4);
            } while (attack == m_Last);
            m_Last = attack;

            HoldPosition = true;
            switch (attack)
            {
                case 0: yield return AcidRain(); break;
                case 1: yield return Bubbles(); break;
                case 2: yield return BellyFlop(); break;
                default: yield return FanSpit(); break;
            }
        }

        int ByPhase(Vector2Int values) => Phase >= 2 ? values.y : values.x;

        IEnumerator AcidRain()
        {
            Squash(-0.35f);
            Lean(-20f);
            int drops = ByPhase(m_Def.AcidDrops);
            float warning = WarningTime(m_Def.AcidWarning);
            Threat(warning + 0.5f);
            for (int i = 0; i < drops; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 10f;
                Vector3 spot = i == 0 ? BossTarget.Feet : BossTarget.Feet + new Vector3(spread.x, 0f, spread.y);
                Vector3 ground = GroundTelegraph.Show(Kit, spot, m_Def.AcidRadius, warning, k_Acid);
                StartCoroutine(AcidDrop(ground, warning));
                yield return Wait(0.1f);
            }

            yield return new WaitForSeconds(warning);
        }

        IEnumerator AcidDrop(Vector3 ground, float warning)
        {
            yield return DropFromSky(ground, warning, m_Def.AcidImpactDamage, m_Def.AcidRadius, 0.9f);
            yield return new WaitForSeconds(0.45f);
            AcidPuddle.Spawn(Kit, ground, m_Def.AcidRadius, m_Def.PuddleDuration, m_Def.PuddleTickDamage,
                m_Def.PuddleTickInterval, gameObject, k_Acid);
        }

        // Bolhas lentas que perseguem o jogador (dá para correr delas ou derrubá-las desviando atrás de obstáculos)
        IEnumerator Bubbles()
        {
            Threat(2f);
            int count = ByPhase(m_Def.Bubbles);
            for (int i = 0; i < count; i++)
            {
                Squash(0.2f);
                float angle = -60f + 120f * (count > 1 ? i / (float)(count - 1) : 0.5f);
                Vector3 direction = Quaternion.Euler(-25f, angle, 0f) * transform.forward;
                Shoot(MuzzlePosition, direction, m_Def.BubbleSpeed, m_Def.BubbleDamage, 1.3f, m_Def.BubbleTurnRate,
                    m_Def.BubbleLifetime);
                yield return Wait(0.25f);
            }
        }

        // Barrigada: salta para longe e cai soltando ondas em sequência
        IEnumerator BellyFlop()
        {
            Vector3 target = PointAroundPlayer(m_Def.LeapDistance);
            float warning = WarningTime(m_Def.LeapWarning);
            GroundTelegraph.Show(Kit, target, 4f, warning + CombatTiming.Scaled(m_Def.LeapAirTime), m_Def.AttackColor);
            Threat(warning + m_Def.LeapAirTime + 1f);
            Squash(0.4f);
            yield return new WaitForSeconds(warning);

            yield return JumpTo(target, m_Def.LeapAirTime, 8f);
            int waves = ByPhase(m_Def.Shockwaves);
            for (int i = 0; i < waves; i++)
            {
                Squash(0.4f);
                Wave(transform.position, m_Def.ShockwaveSpeed, m_Def.ShockwaveDamage);
                yield return Wait(m_Def.ShockwaveInterval);
            }
        }

        // Cuspida: leques de projéteis na direção do jogador
        IEnumerator FanSpit()
        {
            Threat(1.5f);
            int volleys = ByPhase(m_Def.FanVolleys);
            for (int v = 0; v < volleys; v++)
            {
                FacePlayer(30f);
                Squash(0.25f);
                Vector3 toPlayer = BossTarget.Chest - MuzzlePosition;
                float offset = v % 2 == 0 ? 0f : m_Def.FanAngle / (m_Def.FanBullets - 1) * 0.5f;
                for (int b = 0; b < m_Def.FanBullets; b++)
                {
                    float angle = -m_Def.FanAngle * 0.5f + m_Def.FanAngle * b / (m_Def.FanBullets - 1) + offset;
                    Shoot(MuzzlePosition, Quaternion.Euler(0f, angle, 0f) * toPlayer, m_Def.FanSpeed, m_Def.FanDamage,
                        0.55f);
                }
                yield return Wait(m_Def.FanInterval);
            }
        }
    }
}
