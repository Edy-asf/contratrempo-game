using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Dragão de Magma (chefe da Cratera, o último e mais difícil). Paira acima do chão e alterna quatro ataques, sem
    // repetir o mesmo duas vezes seguidas: sopro de fogo (rajada varrendo um arco), chuva de meteoros (marcas que
    // perseguem o jogador), pisões (ondas de choque em sequência: pular cada uma) e flor de fogo (anéis densos
    // girando). Fase 2: ruge e acelera o tempo de Kairós. Fase 3: combina dois ataques ao mesmo tempo.
    // Valores no DragaoDefinition.
    public class DragaoAI : BossAI
    {
        static readonly Color k_Fire = new Color(1f, 0.45f, 0.1f, 0.95f);

        DragaoDefinition m_Def;
        int m_Last = -1;

        protected override void Start()
        {
            base.Start();
            m_Def = Definition as DragaoDefinition;
            HoverHeight = 0.8f;
        }

        protected override void OnPhaseChanged(int phase)
        {
            base.OnPhaseChanged(phase);
            // Fase 2: o dragão acelera o tempo de Kairós
            if (phase == 2 && m_Def && TimeManager.Instance)
                TimeManager.Instance.SetMultiplier(TimeManager.CurrentMultiplier + m_Def.AccelerateOnPhaseTwo);
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

            // Fase 3: um segundo ataque (meteoros ou flor) roda junto
            if (Phase >= 3)
                StartCoroutine(attack == 1 ? FireFlower() : MeteorRain());

            switch (attack)
            {
                case 0: yield return FireBreath(); break;
                case 1: yield return MeteorRain(); break;
                case 2: yield return Stomps(); break;
                default: yield return FireFlower(); break;
            }
        }

        int ByPhase(Vector3Int values) => Phase >= 3 ? values.z : Phase == 2 ? values.y : values.x;

        // Sopro: rajada contínua de projéteis varrendo um arco à frente, mirando no jogador
        IEnumerator FireBreath()
        {
            HoldPosition = true;
            Threat(m_Def.BreathDuration + 0.8f);
            Lean(-20f);
            Squash(-0.3f);
            yield return new WaitForSeconds(WarningTime(0.8f));

            float duration = CombatTiming.Scaled(m_Def.BreathDuration);
            float interval = 1f / m_Def.BreathShotsPerSecond;
            float sweep = Random.value < 0.5f ? 1f : -1f;
            for (float t = 0f; t < duration; t += CombatTiming.Scaled(interval))
            {
                Vector3 toPlayer = BossTarget.Chest - MuzzlePosition;
                float angle = sweep * Mathf.Lerp(-m_Def.BreathArc * 0.5f, m_Def.BreathArc * 0.5f, t / duration);
                Vector3 direction = Quaternion.Euler(Random.Range(-3f, 3f), angle, 0f) * toPlayer;
                Shoot(MuzzlePosition, direction, m_Def.BreathSpeed, m_Def.BreathDamage, 0.7f, 0f, 4f);
                Lean(12f);
                yield return Wait(interval);
            }
        }

        // Meteoros caem em marcas, os primeiros sobre o jogador e os seguintes onde ele está indo
        IEnumerator MeteorRain()
        {
            HoldPosition = true;
            int count = ByPhase(m_Def.Meteors);
            float warning = WarningTime(m_Def.MeteorWarning);
            Threat(warning + count * m_Def.MeteorInterval);
            Squash(-0.4f);
            for (int i = 0; i < count; i++)
            {
                Vector3 lead = BossTarget.Velocity * 0.6f;
                lead.y = 0f;
                Vector2 spread = Random.insideUnitCircle * (i < 3 ? 1f : 7f);
                Vector3 spot = BossTarget.Feet + lead + new Vector3(spread.x, 0f, spread.y);
                Vector3 ground = GroundTelegraph.Show(Kit, spot, m_Def.MeteorRadius, warning, k_Fire);
                StartCoroutine(DropFromSky(ground, warning, m_Def.MeteorDamage, m_Def.MeteorRadius, 1.6f));
                yield return Wait(m_Def.MeteorInterval);
            }

            yield return new WaitForSeconds(warning);
        }

        // Pisões: ondas de choque em sequência rápida, saindo do dragão
        IEnumerator Stomps()
        {
            HoldPosition = true;
            int count = ByPhase(m_Def.Stomps);
            Threat(count * m_Def.StompInterval + 1f);
            yield return new WaitForSeconds(WarningTime(0.6f));
            for (int i = 0; i < count; i++)
            {
                Squash(0.5f);
                Wave(transform.position, m_Def.ShockwaveSpeed, m_Def.ShockwaveDamage);
                yield return Wait(m_Def.StompInterval);
            }
        }

        // Flor de fogo: anéis densos, cada um girado em relação ao anterior
        IEnumerator FireFlower()
        {
            HoldPosition = true;
            int rings = ByPhase(m_Def.FlowerRings);
            Threat(rings * m_Def.FlowerInterval + 0.6f);
            Squash(-0.35f);
            yield return new WaitForSeconds(WarningTime(0.6f));
            float offset = 0f;
            for (int r = 0; r < rings; r++)
            {
                Ring(RingOrigin, m_Def.FlowerBullets, m_Def.FlowerSpeed, m_Def.FlowerDamage, offset, 0, 0, 0.6f);
                offset += 360f / m_Def.FlowerBullets * (r % 2 == 0 ? 0.5f : -0.25f);
                Squash(0.2f);
                yield return Wait(m_Def.FlowerInterval);
            }
        }
    }
}
