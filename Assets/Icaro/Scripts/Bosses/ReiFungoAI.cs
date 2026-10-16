using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Rei Fungo (chefe do Bosque). Alterna três ataques, sem repetir o mesmo duas vezes seguidas:
    // anéis de esporos com brecha giratória, salto esmagador com ondas de choque (pular) e chuva de esporos em marcas
    // no chão. Na fase 2: mais anéis com brecha menor, duas ondas por queda e mais bombas. Valores no ReiFungoDefinition.
    public class ReiFungoAI : BossAI
    {
        ReiFungoDefinition m_Def;
        int m_Last = -1;

        protected override void Start()
        {
            base.Start();
            m_Def = Definition as ReiFungoDefinition;
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
                case 0: yield return SporeRings(); break;
                case 1: yield return JumpSlam(); break;
                default: yield return SporeRain(); break;
            }
        }

        // Anéis de esporos: a brecha gira um pouco a cada anel, então é preciso se mover para atravessá-la
        IEnumerator SporeRings()
        {
            HoldPosition = true;
            Threat(1.5f);
            Squash(0.3f);
            yield return new WaitForSeconds(WarningTime(0.6f));

            int rings = m_Def.RingCount + (Phase >= 2 ? 2 : 0);
            int gap = Mathf.Max(2, m_Def.RingGap - (Phase >= 2 ? 1 : 0));
            int bullets = m_Def.BulletsPerRing + (Phase >= 2 ? 6 : 0);
            int gapStart = Random.Range(0, bullets);
            for (int r = 0; r < rings; r++)
            {
                Squash(0.25f);
                Ring(RingOrigin, bullets, m_Def.BulletSpeed, m_Def.BulletDamage,
                    0f, gapStart, gap, 0.7f);
                gapStart = (gapStart + Random.Range(1, 3)) % bullets;
                yield return Wait(m_Def.RingInterval);
            }
        }

        // Salto esmagador: marca o chão onde o jogador estava, salta, cai e solta as ondas de choque
        IEnumerator JumpSlam()
        {
            HoldPosition = true;
            float warning = WarningTime(m_Def.JumpWarning);
            Vector3 target = GroundTelegraph.Show(Kit, BossTarget.Feet, m_Def.SlamRadius,
                warning + CombatTiming.Scaled(m_Def.JumpAirTime), m_Def.AttackColor);
            Threat(warning + m_Def.JumpAirTime);
            Lean(-15f);
            yield return new WaitForSeconds(warning);

            yield return JumpTo(target, m_Def.JumpAirTime, 6f);

            if (BossTarget.FlatDistance(target) <= m_Def.SlamRadius && BossTarget.IsGrounded)
                BossTarget.Damage(m_Def.SlamDamage, gameObject, BossTarget.Chest);

            int waves = Phase >= 2 ? m_Def.Shockwaves.y : m_Def.Shockwaves.x;
            for (int i = 0; i < waves; i++)
            {
                Wave(target, m_Def.ShockwaveSpeed, m_Def.ShockwaveDamage);
                yield return Wait(0.6f);
            }
        }

        // Chuva de esporos: bombas caem em marcas em volta do jogador
        IEnumerator SporeRain()
        {
            HoldPosition = true;
            Squash(-0.3f);
            int bombs = m_Def.SporeBombs + (Phase >= 2 ? 4 : 0);
            float warning = WarningTime(m_Def.SporeWarning);
            Threat(warning + 0.5f);
            for (int i = 0; i < bombs; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 8f;
                Vector3 spot = BossTarget.Feet + new Vector3(spread.x, 0f, spread.y);
                if (i == 0)
                    spot = BossTarget.Feet;
                Vector3 ground = GroundTelegraph.Show(Kit, spot, m_Def.SporeRadius, warning, m_Def.AttackColor);
                StartCoroutine(DropFromSky(ground, warning, m_Def.SporeDamage, m_Def.SporeRadius, 1.1f));
                yield return Wait(0.12f);
            }

            yield return new WaitForSeconds(warning);
        }
    }
}
