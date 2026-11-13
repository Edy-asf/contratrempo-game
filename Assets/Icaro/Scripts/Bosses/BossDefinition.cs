using UnityEngine;

namespace Icaro
{
    // Ficha comum dos chefes (os campos de EnemyDefinition valem: vida, dano de contato, MaxHitFraction...). Cada
    // chefe tem a própria subclasse com os números dos ataques. Fases: abaixo de PhaseTwoAt da vida entra na fase 2 e,
    // se PhaseThreeAt > 0, abaixo dela entra na fase 3; a cada fase o intervalo entre ataques encolhe por
    // PhaseGapFactor e os padrões ficam mais densos.
    public enum BossTimeRewind
    {
        DamageSlices,
        KillsOnly,
    }

    public abstract class BossDefinition : EnemyDefinition
    {
        [Header("Chefe")]
        [Tooltip("Velocidade de deslocamento a 1,0x (m/s)")]
        public float MoveSpeed = 3.5f;

        [Tooltip("Distância que tenta manter do jogador entre os ataques")]
        public float PreferredDistance = 14f;

        [Tooltip("Segundos entre um ataque e o próximo, a 1,0x")]
        public float AttackGap = 1.6f;

        [Range(0f, 1f)] public float PhaseTwoAt = 0.5f;
        [Range(0f, 1f)] public float PhaseThreeAt;

        [Tooltip("Multiplica o intervalo entre ataques a cada fase (0,75 = 25% mais rápido)")]
        [Range(0.3f, 1f)] public float PhaseGapFactor = 0.75f;

        [Tooltip("Cor principal dos ataques")]
        public Color AttackColor = new Color(0.4f, 1f, 0.6f, 0.9f);

        [Tooltip("Efeitos compartilhados dos ataques")]
        public BossKit Kit;

        [Header("Multiplicador de tempo")]
        [Tooltip("Como o chefe baixa o multiplicador: por fatias de vida tirada (DamageSlices) ou só pelos abates " +
                 "dos inimigos que vêm com ele (KillsOnly, para chefes que invocam)")]
        public BossTimeRewind TimeRewind = BossTimeRewind.DamageSlices;

        [Tooltip("Fração da vida de cada fatia (0,1 = a cada 10% da vida tirada)")]
        [Range(0.02f, 0.5f)] public float TimeSlice = 0.1f;

        [Tooltip("Quanto cada fatia tira do multiplicador")]
        [Min(0f)] public float TimeSliceReduction = 0.15f;

        [Header("Reforços")]
        [Tooltip("Inimigos que surgem junto com o chefe (sorteados por igual, com o MaxAlive de cada um). Vazio = a " +
                 "tabela normal da sala")]
        public EnemyDefinition[] Reinforcements;

        [Tooltip("Total de reforços na luta (-1 = o BossQuota do CombatBalance; 0 = sem reforços)")]
        [Min(-1)] public int ReinforcementQuota = -1;

        [Tooltip("Reforços vivos ao mesmo tempo (-1 = o BossAliveLimit do CombatBalance)")]
        [Min(-1)] public int ReinforcementAlive = -1;

        [Tooltip("Ao morrer o chefe, os reforços vivos morrem juntos e os que faltam surgir não surgem mais")]
        public bool ReinforcementsDieWithBoss = true;
    }
}
