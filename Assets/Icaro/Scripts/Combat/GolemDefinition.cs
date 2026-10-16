using UnityEngine;

namespace Icaro
{
    // Golem: lento, grande, laser com mira e pancada no chão. Damage é o dano do feixe e AttackCooldown a recarga
    // do laser.
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Golem", fileName = "Inimigo_Golem")]
    public class GolemDefinition : EnemyDefinition
    {
        [Header("Laser")]
        public float LaserRange = 45f;

        [Tooltip("Tempo mirando (linha fina acompanhando o jogador) a 1,0x")]
        public float AimDuration = 2f;

        [Tooltip("Tempo com a mira travada antes do disparo, a 1,0x")]
        public float LockDuration = 0.4f;

        [Tooltip("Raio do feixe para acertar o jogador")]
        public float BeamRadius = 0.45f;

        [Tooltip("Raio e dano da explosão no ponto de impacto")]
        public float ExplosionRadius = 3f;

        public float ExplosionDamage = 15f;

        [Header("Pancada no chão")]
        [Tooltip("Distância do jogador que dispara a pancada")]
        public float SlamTriggerRange = 5f;

        [Tooltip("Raio do dano em área")]
        public float SlamRadius = 6.5f;

        public float SlamDamage = 30f;

        [Tooltip("Duração do aviso da pancada a 1,0x")]
        public float SlamWarning = 0.9f;

        public float SlamCooldown = 3f;

        [Header("Movimento")]
        [Tooltip("Distância que ele tenta manter do jogador")]
        public float PreferredDistance = 18f;
    }
}
