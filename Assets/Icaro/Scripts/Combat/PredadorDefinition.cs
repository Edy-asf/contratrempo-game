using UnityEngine;

namespace Icaro
{
    // Predador: investe em linha reta até onde o jogador estava no começo do aviso. Damage é o dano da investida
    // e AttackCooldown a recarga depois dela (em que ele circula o jogador).
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Predador", fileName = "Inimigo_Predador")]
    public class PredadorDefinition : EnemyDefinition
    {
        [Header("Investida")]
        [Tooltip("Distância máxima (com linha de visão) em que ele para e começa o aviso")]
        public float AttackRange = 25f;

        [Tooltip("Duração do aviso (brilho, som e linha no chão) a 1,0x")]
        public float WarningDuration = 0.8f;

        [Tooltip("Velocidade da investida a 1,0x, em m/s")]
        public float DashSpeed = 26f;

        [Tooltip("Distância ao jogador em que a investida acerta")]
        public float DashHitRange = 1.7f;

        [Header("Recarga")]
        [Tooltip("Distância a que ele circula o jogador durante a recarga")]
        public float CircleDistance = 12f;
    }
}
