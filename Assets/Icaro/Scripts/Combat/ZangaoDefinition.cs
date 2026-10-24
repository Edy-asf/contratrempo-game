using UnityEngine;

namespace Icaro
{
    // Zangão: inseto voador pequeno que atira, com vida mínima, em grupos. Damage é o dano de cada projétil e
    // ProjectileSpeed a velocidade dele a 1,0x; MaxAlive, GroupMin/GroupMax e Pooled vêm da ficha base.
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Zangão", fileName = "Inimigo_Zangao")]
    public class ZangaoDefinition : EnemyDefinition
    {
        [Header("Voo (a 1,0x)")]
        [Tooltip("Velocidade de voo, em m/s")]
        public float FlySpeed = 8f;

        [Tooltip("Quão rápido muda de direção (m/s²)")]
        public float Acceleration = 14f;

        [Tooltip("Altura acima do chão (mínima e máxima)")]
        public float HeightMin = 3f;
        public float HeightMax = 6f;

        [Tooltip("Distância horizontal até o jogador em que circula (mínima e máxima). Longe, ele se aproxima até essa " +
                 "faixa; nunca vem encostar como os inimigos corpo a corpo")]
        public float OrbitMin = 27f;
        public float OrbitMax = 33f;

        [Tooltip("Segundos entre mudanças de direção do zigue-zague (mínimo e máximo)")]
        public float ZigzagIntervalMin = 0.6f;
        public float ZigzagIntervalMax = 1.4f;

        [Tooltip("Quanto o ângulo em volta do jogador muda a cada zigue-zague, em graus (para um lado ou o outro)")]
        public float ZigzagAngle = 55f;

        [Tooltip("Ruído suave somado ao movimento, em m/s")]
        public float NoiseStrength = 1.5f;

        [Tooltip("Distância à frente em que procura parede ou obstáculo para desviar")]
        public float AvoidDistance = 3f;

        [Tooltip("Distância mínima desejada entre Zangões")]
        public float SeparationRadius = 2.5f;

        [Header("Tiro")]
        [Tooltip("Alcance do tiro, com linha de visão")]
        public float AttackRange = 40f;

        [Tooltip("Segundos entre tiros a 1,0x (mínimo e máximo)")]
        public float ShotIntervalMin = 2f;
        public float ShotIntervalMax = 3f;

        [Tooltip("Brilho de aviso no ferrão antes do tiro, a 1,0x (nunca abaixo de 60%)")]
        public float MuzzleWarning = 0.3f;

        [Tooltip("Zangões que podem estar atirando (aviso + tiro) ao mesmo tempo; os outros esperam circulando")]
        [Min(1)] public int MaxShooters = 4;

        [Tooltip("Segundos de vida do projétil")]
        public float ProjectileLifetime = 2.5f;
    }
}
