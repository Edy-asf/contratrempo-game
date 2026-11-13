using System.Collections;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Pé-de-Musgo (chefe do Bosque, no lugar do Rei Fungo): caçador de emboscada coberto de líquen. Escolhe o ataque
    // pela distância e pela fase, sem repetir o mesmo duas vezes seguidas (ataques e números no PeDeMusgoDefinition):
    // - Salto esmagador: agacha; a sombra do pouso aparece no chão onde o jogador está; cai, machuca quem está na sombra
    //   e solta uma onda de terra (pular). Da fase 2 em diante, vem em par (o segundo com aviso mais curto).
    // - Agarrão (perto): arma o braço, avança e passa a garra num cone à frente; quem é pego leva o golpe pesado, fica
    //   sem controle e é arremessado para longe (o dash, invulnerável, escapa).
    // - Arremesso de torrão (longe): arranca um torrão do chão e joga em arco até onde o jogador está; a queda é
    //   marcada e espalha pedaços pelo chão. Mais torrões nas fases seguintes.
    // - Rugido de esporos (fase 2+): ergue a cabeça, os olhos piscam e os cogumelos do ombro incham; depois ruge e
    //   cobre o chão de nuvens de esporos (dano contínuo e vista curta). Os cogumelos são o ponto fraco: tiros neles
    //   causam dano extra sempre, e acertá-los durante o aviso do rugido interrompe o ataque e o atordoa.
    // - Caçada (fase 3, alternando com os outros): salta para a borda da arena e some na névoa (só os olhos ficam);
    //   depois de alguns segundos, rosna (a seta de ameaça do visor mostra de onde) e dá o bote até o jogador.
    // A animação de cada ataque é um clipe próprio da folha (ISpriteClipSource).
    public class PeDeMusgoAI : BossAI, ISpriteClipSource
    {
        enum Move { Jump, Grab, Clod, Roar, Hunt }

        PeDeMusgoDefinition m_Def;
        Move m_Last = Move.Hunt;
        bool m_LastWasHunt;
        int m_SinceRoar;

        string m_Clip;
        float m_ClipStart;
        float m_ClipLength;
        bool m_ClipWarning;
        bool m_Hidden;

        bool m_RoarWindup;
        bool m_Interrupted;
        bool m_ApplyingWeakDamage;
        PlayerDash m_Dash;

        // ---------- sprite ----------

        public string SpriteClip => m_Clip;

        public float SpriteClipProgress => m_ClipLength > 0f
            ? Mathf.Clamp01((CombatTiming.Now - m_ClipStart) / m_ClipLength)
            : -1f;

        public bool SpriteClipWarning => m_ClipWarning;
        public bool SpriteHidden => m_Hidden;

        // clipe preso à duração (length > 0) ou pelo relógio (length 0)
        void Clip(string name, float length = 0f, bool warning = false)
        {
            m_Clip = name;
            m_ClipStart = CombatTiming.Now;
            m_ClipLength = length;
            m_ClipWarning = warning;
        }

        void ClearClip()
        {
            m_Clip = null;
            m_ClipLength = 0f;
            m_ClipWarning = false;
        }

        // Dano da ficha com a escala da sala (EnemyStats)
        float Dmg(float baseDamage) => Stats ? Stats.ScaleDamage(baseDamage) : baseDamage;

        // ---------- ciclo ----------

        protected override void Start()
        {
            base.Start();
            m_Def = Definition as PeDeMusgoDefinition;
            CombatEvents.OnDamage += OnAnyDamage;
            Health.OnDie += () =>
            {
                ClearClip();
                m_Hidden = false;
                m_AimWeak = false;
            };
        }

        void OnDestroy()
        {
            CombatEvents.OnDamage -= OnAnyDamage;
            if (m_Hidden)
                m_Hidden = false;
        }

        protected override void OnPhaseChanged(int phase)
        {
            Squash(-0.4f);
            if (phase == 2)
                VisorBanner.Show("O Pé-de-Musgo ruge", "Os cogumelos do ombro brilham", BannerTone.Danger);
            else
                VisorBanner.Show("A caçada começa", "Ele some na névoa", BannerTone.Danger);
        }

        protected override IEnumerator NextAttack()
        {
            if (!m_Def)
                yield break;

            Move move = Choose();
            m_Last = move;
            m_LastWasHunt = move == Move.Hunt;
            m_SinceRoar = move == Move.Roar ? 0 : m_SinceRoar + 1;

            switch (move)
            {
                case Move.Jump:
                    yield return JumpSlam(1f);
                    if (Phase >= 2 && !m_Interrupted)
                    {
                        yield return Wait(0.35f);
                        yield return JumpSlam(m_Def.SecondJumpWarning);
                    }

                    break;
                case Move.Grab: yield return Grab(); break;
                case Move.Clod: yield return ThrowClods(); break;
                case Move.Roar: yield return Roar(); break;
                default: yield return Hunt(); break;
            }

            ClearClip();
            m_Hidden = false;
            m_Interrupted = false;
            m_RoarWindup = false;
            MoveAimPoint(false);
        }

        // Pela distância e pela fase; nunca o mesmo duas vezes seguidas
        Move Choose()
        {
            float distance = BossTarget.FlatDistance(transform.position);
            // fase 3: a caçada a cada dois ataques
            if (Phase >= 3 && !m_LastWasHunt && Random.value < 0.6f)
                return Move.Hunt;
            // fase 2+: o rugido volta a cada três ou quatro ataques
            if (Phase >= 2 && m_Last != Move.Roar && m_SinceRoar >= 2 && Random.value < 0.55f)
                return Move.Roar;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Move move;
                float r = Random.value;
                if (distance <= m_Def.GrabTriggerDistance)
                    move = r < 0.55f ? Move.Grab : r < 0.85f ? Move.Jump : Move.Clod;
                else if (distance >= m_Def.ClodMinDistance)
                    move = r < 0.5f ? Move.Clod : Move.Jump;
                else
                    move = r < 0.6f ? Move.Jump : r < 0.8f ? Move.Clod : Move.Grab;
                if (move != m_Last)
                    return move;
            }

            return m_Last == Move.Jump ? Move.Clod : Move.Jump;
        }

        // Sem o balanço contínuo do corpo (pedido do usuário em 2026-11-13): o Idle da folha já respira
        protected override float IdleBob => 0f;

        // ---------- salto esmagador ----------

        IEnumerator JumpSlam(float warningFactor)
        {
            HoldPosition = true;
            float warning = WarningTime(m_Def.JumpWarning * warningFactor);
            float air = CombatTiming.Scaled(m_Def.JumpAirTime);
            Threat(warning + air);
            Clip("Salto_Aviso", warning, true);
            Lean(-8f);
            yield return CombatTiming.Wait(warning);

            // a sombra aparece no chão onde o jogador está, ShadowLead antes de cair (ou na saída, se o voo for curto)
            Vector3 target = LandingPoint(BossTarget.Feet);
            float lead = Mathf.Min(CombatTiming.Scaled(m_Def.ShadowLead), air);
            Clip("Salto_Ar");
            IEnumerator jump = JumpTo(target, m_Def.JumpAirTime, m_Def.JumpHeight);
            float start = CombatTiming.Now;
            bool shown = false;
            while (jump.MoveNext())
            {
                if (!shown && CombatTiming.Now - start >= air - lead)
                {
                    shown = true;
                    target = GroundTelegraph.Show(Kit, target, m_Def.SlamRadius, lead, m_Def.AttackColor);
                }

                yield return jump.Current;
            }

            if (!shown)
                GroundTelegraph.Show(Kit, target, m_Def.SlamRadius, 0.1f, m_Def.AttackColor);
            Land(target, m_Def.SlamRadius, Dmg(m_Def.SlamDamage));
            Wave(new Vector3(target.x, GroundTelegraph.GroundPoint(target).y, target.z), m_Def.ShockwaveSpeed,
                Dmg(m_Def.ShockwaveDamage));
            Clip("Salto_Queda", 0.4f);
            yield return Wait(0.45f);
        }

        // Pouso: dano na área (quem está no ar acima escapa), terra voando, tremor e o baque
        void Land(Vector3 point, float radius, float damage)
        {
            Vector3 ground = GroundTelegraph.GroundPoint(point);
            if (BossTarget.Exists && BossTarget.FlatDistance(ground) <= radius &&
                BossTarget.Feet.y < ground.y + 1.5f && damage > 0f)
            {
                BossTarget.Damage(damage, gameObject, BossTarget.Chest, heavy: true);
                // o golpe joga para longe (mais forte perto do centro); o ShovePlayerOut já arremessou quem estava
                // embaixo do corpo
                if (!PlayerThrow.Active)
                {
                    float k = 1f - Mathf.Clamp01(BossTarget.FlatDistance(ground) / Mathf.Max(0.1f, radius));
                    KnockPlayer(ground, Mathf.Lerp(8f, 14f, k), Mathf.Lerp(4f, 6f, k), 0.4f);
                }
            }
            EnemyFxBurst.Play(EnemyFxSprites.Impact(EnemyFxStyle.Clod), ground + Vector3.up * 0.8f, 12f, radius * 0.9f);
            EnemyFxBurst.PlayFlat(EnemyFxSprites.Scorch(EnemyFxStyle.Clod), ground, Vector3.up, 1f, 5f, 2f,
                radius * 0.9f);
            EnemySounds.Play(EnemySounds.MossLand, ground, 1f, 1f, 0.05f, 12f);
            float near = BossTarget.Exists ? BossTarget.FlatDistance(ground) : 99f;
            CameraShake.AddTrauma(Mathf.Lerp(0.55f, 0.12f, Mathf.Clamp01(near / 30f)));
        }

        // Ponto do NavMesh mais perto do alvo, dentro do domo da arena
        Vector3 LandingPoint(Vector3 wanted)
        {
            ArenaDome dome = RoomManager.Active ? RoomManager.Active.Dome : null;
            if (dome)
            {
                Vector3 c = dome.Center;
                Vector3 d = wanted - c;
                d.y = 0f;
                float max = Mathf.Max(1f, dome.Radius - BodyRadius - 2f);
                if (d.magnitude > max)
                    wanted = c + d.normalized * max + Vector3.up * (wanted.y - c.y);
            }

            return NavMesh.SamplePosition(wanted, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : wanted;
        }

        // ---------- agarrão ----------

        IEnumerator Grab()
        {
            HoldPosition = true;
            float warning = WarningTime(m_Def.GrabWarning);
            Threat(warning + m_Def.GrabLungeTime);
            Clip("Agarrao_Aviso", warning, true);
            Lean(-10f);
            // mira enquanto arma: gira até o jogador
            for (float t = 0f; t < warning; t += CombatTiming.DeltaTime)
            {
                FacePlayer(5f);
                yield return null;
            }

            // trava a direção e avança
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Clip("Agarrao_Golpe", 0f);
            Lean(12f);
            EnemySounds.Play(EnemySounds.MossSwipe, transform.position + Vector3.up * 4f, 1f, 1f, 0.06f, 8f);
            bool agent = Agent && Agent.enabled;
            Vector3 start = transform.position;
            Vector3 end = LandingPoint(start + forward * m_Def.GrabLunge);
            float lunge = CombatTiming.Scaled(m_Def.GrabLungeTime);
            bool hit = false;
            for (float t = 0f; t < lunge; t += CombatTiming.DeltaTime)
            {
                Vector3 p = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / lunge));
                if (agent && Agent.isOnNavMesh)
                    Agent.Warp(p);
                else
                    transform.position = p;
                if (!hit && t >= lunge * 0.4f)
                    hit = TryGrab(forward);
                yield return null;
            }

            if (!hit)
                TryGrab(forward);
            Lean(0f);
            yield return Wait(0.5f);
        }

        bool TryGrab(Vector3 forward)
        {
            if (!BossTarget.Exists)
                return false;
            Vector3 to = BossTarget.Feet - transform.position;
            float height = to.y;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > m_Def.GrabRange + BodyRadius || height > 6f || height < -3f)
                return false;
            if (distance > 0.5f && Vector3.Angle(forward, to) > m_Def.GrabHalfAngle)
                return false;
            if (!m_Dash && BossTarget.Player)
                m_Dash = BossTarget.Player.GetComponent<PlayerDash>();
            if (m_Dash && m_Dash.IsDashing)
                return false;

            BossTarget.Damage(Dmg(m_Def.GrabDamage), gameObject, BossTarget.Chest, heavy: true);
            Vector3 away = distance > 0.5f ? to / distance : forward;
            away = Vector3.Slerp(away, forward, 0.5f).normalized;
            PlayerThrow.Throw(away * m_Def.GrabThrow.x + Vector3.up * m_Def.GrabThrow.y, m_Def.GrabStun);
            EnemySounds.Play(EnemySounds.MossLand, BossTarget.Chest, 0.6f, 1.4f, 0.05f, 6f);
            return true;
        }

        // ---------- arremesso de torrão ----------

        IEnumerator ThrowClods()
        {
            HoldPosition = true;
            int count = Phase >= 3 ? m_Def.ClodCount.z : Phase >= 2 ? m_Def.ClodCount.y : m_Def.ClodCount.x;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float warning = WarningTime(m_Def.ClodWarning * (i == 0 ? 1f : 0.7f));
                Threat(warning + 0.3f);
                Clip("Torrao_Aviso", warning, true);
                Lean(-6f);
                EnemySounds.Play(EnemySounds.MossDig, transform.position, 0.9f, 1f, 0.08f, 8f);
                yield return CombatTiming.Wait(warning);

                Clip("Torrao_Golpe", 0f);
                Lean(8f);
                // mira onde o jogador vai estar (um pouco à frente do movimento), mais espalhado nos seguintes
                float flight = Mathf.Max(CombatTiming.Scaled(m_Def.ClodFlightTime), m_Def.ClodFlightTime * 0.6f);
                Vector3 lead = BossTarget.Velocity;
                lead.y = 0f;
                Vector3 target = BossTarget.Feet + lead * (flight * 0.5f);
                if (i > 0)
                {
                    Vector2 spread = Random.insideUnitCircle * 4f;
                    target += new Vector3(spread.x, 0f, spread.y);
                }

                Vector3 hand = transform.position + Vector3.up * 9f - transform.right * 2f;
                EnemyMortar.Launch(gameObject, hand, target, flight, m_Def.ClodRadius,
                    Dmg(m_Def.ClodDamage), EnemyFxStyle.Clod, Shards);
                yield return Wait(0.35f);
            }

            Lean(0f);
            yield return Wait(0.3f);
        }

        // Pedaços espalhando a partir da queda, rentes ao chão
        void Shards(Vector3 point)
        {
            if (m_Def.ClodShards <= 0 || !Kit)
                return;
            Vector3 from = point + Vector3.up * 0.9f;
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < m_Def.ClodShards; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, offset + i * 360f / m_Def.ClodShards, 0f) * Vector3.forward;
                BossProjectile.Fire(Kit, from, dir, m_Def.ShardSpeed, Dmg(m_Def.ShardDamage), gameObject,
                    new Color(0.55f, 0.4f, 0.22f, 1f), 0.35f, 0f, 1.4f, 0f);
            }
        }

        // ---------- rugido de esporos ----------

        IEnumerator Roar()
        {
            HoldPosition = true;
            float warning = WarningTime(m_Def.RoarWarning);
            Threat(warning + m_Def.RoarTime);
            Clip("Rugido_Aviso", warning, true);
            Lean(-12f);
            m_RoarWindup = true;
            MoveAimPoint(true);
            for (float t = 0f; t < warning && !m_Interrupted; t += CombatTiming.DeltaTime)
                yield return null;
            m_RoarWindup = false;
            MoveAimPoint(false);

            if (m_Interrupted)
            {
                yield return Stunned();
                yield break;
            }

            Clip("Rugido_Golpe");
            Squash(-0.3f);
            EnemySounds.Play(EnemySounds.MossRoar, transform.position + Vector3.up * 8f, 1f, 1f, 0.04f, 20f);
            CameraShake.AddTrauma(0.35f);
            // uma nuvem no jogador e as outras em volta dele e do chefe
            int clouds = Mathf.Max(1, m_Def.SporeClouds + (Phase >= 3 ? 1 : 0));
            float dps = Dmg(m_Def.CloudDps);
            for (int i = 0; i < clouds; i++)
            {
                Vector3 at = i == 0 ? BossTarget.Feet
                    : i % 2 == 1 ? PointAroundPlayer(m_Def.CloudRadius * 2.2f)
                    : Vector3.Lerp(transform.position, BossTarget.Feet, Random.Range(0.3f, 0.6f));
                SporeCloud.Spawn(gameObject, at, m_Def.CloudRadius, m_Def.CloudDuration, dps);
                yield return Wait(m_Def.RoarTime / clouds);
            }

            Lean(0f);
        }

        IEnumerator Stunned()
        {
            Clip("Atordoado");
            Lean(10f);
            Squash(0.4f);
            EnemySounds.Play(EnemySounds.MossStun, transform.position + Vector3.up * 8f, 1f, 1f, 0.05f, 14f);
            VisorBanner.Show("Atordoado", "Os cogumelos estouraram", BannerTone.Success);
            yield return CombatTiming.Wait(m_Def.StunTime);
            Lean(0f);
        }

        // Os colchetes da mira assistida puxam para os cogumelos durante o aviso do rugido
        void MoveAimPoint(bool weak)
        {
            var actor = GetComponent<Actor>();
            if (!actor || !actor.AimPoint)
                return;
            if (m_AimHome == null)
                m_AimHome = actor.AimPoint.localPosition;
            if (!weak)
                actor.AimPoint.localPosition = m_AimHome.Value;
            m_AimWeak = weak;
        }

        Vector3? m_AimHome;
        bool m_AimWeak;

        void LateUpdate()
        {
            // o ponto de mira segue os cogumelos na tela (o sprite olha sempre para a câmera)
            if (!m_AimWeak)
                return;
            var actor = GetComponent<Actor>();
            Camera camera = Camera.main;
            if (!actor || !actor.AimPoint || !camera)
                return;
            actor.AimPoint.position = WeakPoint(camera.transform.position);
        }

        // Centro dos cogumelos no mundo visto de 'eye': o sprite fica de frente para quem olha, então o lado direito
        // do chefe é o lado esquerdo da tela
        Vector3 WeakPoint(Vector3 eye)
        {
            Vector3 toEye = eye - transform.position;
            toEye.y = 0f;
            if (toEye.sqrMagnitude < 0.01f)
                toEye = transform.forward;
            toEye.Normalize();
            Vector3 screenLeft = Vector3.Cross(Vector3.up, toEye);
            return transform.position + Vector3.up * m_Def.WeakPointOffset.y + screenLeft * m_Def.WeakPointOffset.x +
                   toEye * 0.5f;
        }

        // Tiros nos cogumelos: dano extra sempre; no aviso do rugido, interrompe e atordoa
        void OnAnyDamage(DamageInfo info)
        {
            if (m_ApplyingWeakDamage || !m_Def || !info.AttackerIsPlayer || info.VictimHealth != Health ||
                info.IsItem || info.Amount <= 0f || Health.CurrentHealth <= 0f)
                return;
            Camera camera = Camera.main;
            if (!camera)
                return;
            // compara no plano do sprite: a linha do tiro (da câmera ao ponto) contra o círculo dos cogumelos
            Vector3 eye = camera.transform.position;
            Vector3 weak = WeakPoint(eye);
            Vector3 ray = info.Point - eye;
            if (ray.sqrMagnitude < 0.01f)
                return;
            Vector3 dir = ray.normalized;
            Vector3 toWeak = weak - eye;
            float along = Vector3.Dot(toWeak, dir);
            float miss = (toWeak - dir * along).magnitude;
            if (along <= 0f || miss > m_Def.WeakPointRadius)
                return;

            float extra = info.Amount * Mathf.Max(0f, m_Def.WeakPointMultiplier - 1f);
            var damageable = GetComponent<Damageable>();
            if (extra > 0f && damageable)
            {
                m_ApplyingWeakDamage = true;
                CombatEvents.ApplyDamage(damageable, extra, info.Attacker, weak, critical: true, item: true);
                m_ApplyingWeakDamage = false;
            }

            if (m_RoarWindup && !m_Interrupted)
                m_Interrupted = true;
        }

        // ---------- caçada ----------

        IEnumerator Hunt()
        {
            HoldPosition = true;
            Vector3 edge = HuntSpot();
            // salta para a borda
            Clip("Salto_Aviso", WarningTime(0.5f), true);
            Threat(WarningTime(0.5f));
            yield return CombatTiming.Wait(WarningTime(0.5f));
            Clip("Salto_Ar");
            yield return JumpTo(edge, m_Def.HuntJumpAirTime, m_Def.JumpHeight * 0.8f);
            Land(edge, 2.5f, 0f);

            // some na névoa
            Clip("Cacada");
            m_Hidden = true;
            float hide = Random.Range(m_Def.HuntHide.x, m_Def.HuntHide.y);
            for (float t = 0f; t < CombatTiming.Scaled(hide); t += CombatTiming.DeltaTime)
            {
                FacePlayer(3f);
                yield return null;
            }

            // rosna e arma o bote (a seta de ameaça mostra de onde)
            float warning = WarningTime(m_Def.PounceWarning);
            Threat(warning + m_Def.PounceAirTime);
            Clip("Bote_Aviso", warning, true);
            EnemySounds.Play(EnemySounds.MossGrowl, transform.position + Vector3.up * 3f, 1f, 1f, 0.05f, 25f);
            for (float t = 0f; t < warning; t += CombatTiming.DeltaTime)
            {
                if (t > warning * 0.35f)
                    m_Hidden = false;
                FacePlayer(6f);
                yield return null;
            }

            m_Hidden = false;
            Vector3 target = LandingPoint(BossTarget.Feet);
            GroundTelegraph.Show(Kit, target, m_Def.PounceRadius, CombatTiming.Scaled(m_Def.PounceAirTime),
                m_Def.AttackColor);
            Clip("Salto_Ar");
            yield return JumpTo(target, m_Def.PounceAirTime, m_Def.JumpHeight * 0.45f);
            Land(target, m_Def.PounceRadius, Dmg(m_Def.PounceDamage));
            Clip("Salto_Queda", 0.4f);
            yield return Wait(0.6f);
        }

        // Ponto na borda da arena, do lado oposto ao jogador (mais ou menos), no NavMesh
        Vector3 HuntSpot()
        {
            ArenaDome dome = RoomManager.Active ? RoomManager.Active.Dome : null;
            Vector3 center = dome ? dome.Center : transform.position;
            float radius = dome ? dome.Radius * m_Def.HuntEdge : 20f;
            Vector3 away = center - BossTarget.Feet;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -transform.forward;
            away.Normalize();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away;
                Vector3 candidate = center + dir * radius;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas) &&
                    (!dome || dome.Contains(hit.position, BodyRadius + 1f)))
                    return hit.position;
            }

            return LandingPoint(center + away * radius);
        }
    }
}
