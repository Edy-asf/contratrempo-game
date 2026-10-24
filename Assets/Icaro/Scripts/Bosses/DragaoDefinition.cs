using UnityEngine;

namespace Icaro
{
    // Dragão de Magma, chefe da Cratera (último e mais difícil). Três fases. Ataques:
    // - Sopro de fogo: rajada contínua de projéteis varrendo um arco à frente.
    // - Chuva de meteoros: meteoros caem em marcas no chão, perseguindo a posição do jogador.
    // - Pisões: ondas de choque em sequência rápida (pular cada uma).
    // - Flor de fogo: anéis densos alternando o giro, "tiros por todo lado".
    // Na fase 2, ruge e acelera o tempo de Kairós em AccelerateOnPhaseTwo. Na fase 3, combina dois ataques ao mesmo tempo.
    [CreateAssetMenu(menuName = "Ícaro/Chefe Dragão", fileName = "Chefe_Dragao")]
    public class DragaoDefinition : BossDefinition
    {
        [Header("Fases")]
        [Tooltip("Quanto o multiplicador de tempo sobe ao entrar na fase 2")]
        public float AccelerateOnPhaseTwo = 1f;

        [Header("Sopro de fogo")]
        public float BreathDuration = 2.6f;
        public float BreathShotsPerSecond = 22f;
        public float BreathArc = 100f;
        public float BreathSpeed = 15f;
        public float BreathDamage = 8f;

        [Header("Chuva de meteoros")]
        public Vector3Int Meteors = new Vector3Int(9, 13, 16);
        public float MeteorWarning = 1.2f;
        public float MeteorRadius = 3.5f;
        public float MeteorDamage = 30f;
        public float MeteorInterval = 0.18f;

        [Header("Pisões")]
        public Vector3Int Stomps = new Vector3Int(3, 4, 5);
        public float StompInterval = 0.6f;
        public float ShockwaveSpeed = 15f;
        public float ShockwaveDamage = 22f;

        [Header("Flor de fogo")]
        public Vector3Int FlowerRings = new Vector3Int(4, 6, 7);
        public int FlowerBullets = 28;
        public float FlowerSpeed = 9f;
        public float FlowerDamage = 10f;
        public float FlowerInterval = 0.4f;
    }
}
