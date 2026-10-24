using UnityEngine;

namespace Icaro
{
    // Cascudo: corpo a corpo, lento e resistente. Damage é o dano de cada golpe e AttackCooldown a recarga entre
    // golpes. A velocidade base vem do jogador: MaxSpeedOnGround do PlayerCharacterController / PlayerSpeedDivisor
    // (com o multiplicador no teto de 3x, anda tão rápido quanto o jogador).
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Cascudo", fileName = "Inimigo_Cascudo")]
    public class CascudoDefinition : EnemyDefinition
    {
        [Header("Movimento")]
        [Tooltip("Velocidade base = velocidade em solo (sem correr) do jogador / este valor")]
        public float PlayerSpeedDivisor = 3f;

        [Tooltip("Velocidade usada se o jogador não for encontrado (m/s a 1,0x)")]
        public float FallbackSpeed = 4.3f;

        [Header("Golpe")]
        [Tooltip("Distância ao jogador em que ele para e prepara o golpe")]
        public float AttackRange = 2.4f;

        [Tooltip("Aviso do golpe (preparação visível) a 1,0x; nunca abaixo de 60% com o multiplicador")]
        public float WindupDuration = 0.6f;

        [Tooltip("Tamanho da hitbox à frente dele (largura, altura, profundidade)")]
        public Vector3 HitboxSize = new Vector3(2.4f, 1.8f, 2.2f);

        [Tooltip("Distância do centro da hitbox à frente do corpo")]
        public float HitboxForward = 1.3f;
    }
}
