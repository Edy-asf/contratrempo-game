using UnityEngine;

namespace Icaro
{
    // Rei Fungo, chefe do Bosque (o primeiro e mais fácil). Ataques:
    // - Anéis de esporos: anéis de projéteis lentos em volta dele, com uma brecha que gira (passar pela brecha).
    // - Salto esmagador: marca o lugar do jogador, salta e cai ali; a queda solta ondas de choque (pular por cima).
    // - Chuva de esporos: bombas caem em marcas no chão em volta do jogador.
    [CreateAssetMenu(menuName = "Ícaro/Chefe Rei Fungo", fileName = "Chefe_ReiFungo")]
    public class ReiFungoDefinition : BossDefinition
    {
        [Header("Anéis de esporos")]
        public int RingCount = 3;
        public int BulletsPerRing = 16;
        public float BulletSpeed = 9f;
        public float BulletDamage = 10f;
        public float RingInterval = 0.5f;

        [Tooltip("Projéteis que faltam em cada anel (a brecha)")]
        public int RingGap = 3;

        [Header("Salto esmagador")]
        public float JumpWarning = 1.1f;
        public float JumpAirTime = 0.8f;
        public float SlamRadius = 5f;
        public float SlamDamage = 28f;
        public float ShockwaveSpeed = 12f;
        public float ShockwaveDamage = 18f;

        [Tooltip("Ondas por queda (fase 1, fase 2)")]
        public Vector2Int Shockwaves = new Vector2Int(1, 2);

        [Header("Chuva de esporos")]
        public int SporeBombs = 6;
        public float SporeWarning = 1.2f;
        public float SporeRadius = 2.5f;
        public float SporeDamage = 18f;
    }
}
