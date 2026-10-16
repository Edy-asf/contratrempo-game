using UnityEngine;

namespace Icaro
{
    // Ficha comum dos chefes (os campos de EnemyDefinition valem: vida, dano de contato, MaxHitFraction...). Cada
    // chefe tem a própria subclasse com os números dos ataques. Fases: abaixo de PhaseTwoAt da vida entra na fase 2 e,
    // se PhaseThreeAt > 0, abaixo dela entra na fase 3; a cada fase o intervalo entre ataques encolhe por
    // PhaseGapFactor e os padrões ficam mais densos.
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
    }
}
