using System;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Evento central de dano: todo dano que tira vida de um Health com DamageReporter vira um DamageInfo em
    // OnDamage (feedback, sons, itens...). Quem causa dano no código do Ícaro usa ApplyDamage, que informa o ponto
    // do impacto e o crítico. O dano do Microgame (projéteis, área) passa pelo mesmo caminho via Health.OnDamaged;
    // para os tiros do jogador, o ponto e o crítico vêm do PlayerProjectile que acertou.
    public static class CombatEvents
    {
        public static event Action<DamageInfo> OnDamage;

        struct HitContext
        {
            public bool Active;
            public Vector3 Point;
            public bool Critical;
            public bool Explosion;
            public bool Item;
            public bool Heavy;
            public bool Continuous;
            public float ProcCoefficient;
        }

        static HitContext s_Context;

        // Acerto em andamento (dentro de ApplyDamage) é pesado: laser do Golem; o limite de dano por acerto no
        // jogador é maior (CombatBalance.HeavyMaxHitFraction)
        public static bool CurrentHitIsHeavy => s_Context.Active && s_Context.Heavy;

        // Acerto em andamento é dano contínuo (não liga nem respeita a invulnerabilidade curta do jogador)
        public static bool CurrentHitIsContinuous => s_Context.Active && s_Context.Continuous;

        // Causa dano por um Damageable (respeita multiplicadores e invencibilidade) e publica o evento com o
        // ponto do impacto e o crítico. item: dano causado por um efeito de item (não ativa outros efeitos de item).
        // heavy: ataque pesado (limite de dano por acerto maior). continuous: dano contínuo (ignora a invulnerabilidade
        // curta do jogador depois de um acerto). procCoefficient: peso do acerto nas chances dos efeitos de item (armas
        // com muitos projéteis por tiro, como a escopeta Liberdade, usam menos de 1).
        public static void ApplyDamage(Damageable target, float amount, GameObject attacker, Vector3 point,
            bool critical = false, bool explosion = false, bool item = false, bool heavy = false,
            bool continuous = false, float procCoefficient = 1f)
        {
            if (!target)
                return;

            // Acelerador Térmico: inimigos em chamas (EnemyBurn) recebem mais dano de todas as fontes
            PlayerStats stats = PlayerStats.Instance;
            if (stats && EnemyBurn.Burning(target.Health))
                amount *= 1f + stats.Get(PlayerStat.BurningVulnerability);

            HitContext previous = s_Context;
            s_Context = new HitContext
            {
                Active = true, Point = point, Critical = critical, Explosion = explosion, Item = item, Heavy = heavy,
                Continuous = continuous, ProcCoefficient = procCoefficient,
            };

            // Dano contínuo atravessa a invulnerabilidade curta pós-acerto (PlayerDefenses)
            var defenses = continuous && target.Health ? target.Health.GetComponent<PlayerDefenses>() : null;
            bool bypass = defenses && defenses.IsHitInvulnerable;
            if (bypass)
                target.Health.Invincible = false;
            try
            {
                target.InflictDamage(amount, explosion, attacker);
            }
            finally
            {
                s_Context = previous;
                if (bypass && defenses)
                    target.Health.Invincible = defenses.IsHitInvulnerable;
            }
        }

        // Chamado pelo DamageReporter quando um Health perde vida
        public static void Report(Health victim, float amount, GameObject attacker)
        {
            if (!victim || amount <= 0f)
                return;

            var info = new DamageInfo
            {
                Attacker = attacker,
                Victim = victim.gameObject,
                VictimHealth = victim,
                Amount = amount,
                Killed = victim.CurrentHealth <= 0f,
                AttackerIsPlayer = attacker && attacker.GetComponent<PlayerCharacterController>(),
                VictimIsPlayer = victim.GetComponent<PlayerCharacterController>(),
                ProcCoefficient = 1f,
            };

            if (s_Context.Active)
            {
                info.Point = s_Context.Point;
                info.IsCritical = s_Context.Critical;
                info.IsExplosion = s_Context.Explosion;
                info.IsItem = s_Context.Item;
                info.ProcCoefficient = s_Context.ProcCoefficient;
            }
            else if (info.AttackerIsPlayer && PlayerProjectile.FindHit(victim, out Vector3 point, out bool critical))
            {
                info.Point = point;
                info.IsCritical = critical;
            }
            else
            {
                info.Point = Center(victim);
            }

            OnDamage?.Invoke(info);
        }

        static Vector3 Center(Health victim)
        {
            var collider = victim.GetComponentInChildren<Collider>();
            return collider ? collider.bounds.center : victim.transform.position + Vector3.up;
        }
    }
}
