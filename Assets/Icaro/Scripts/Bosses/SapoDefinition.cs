using UnityEngine;

namespace Icaro
{
    // Sapo Rei do Brejo, chefe do Charco (segundo). Ataques:
    // - Chuva ácida: gotas caem em marcas no chão e deixam poças que queimam quem pisa.
    // - Bolhas teleguiadas: bolhas lentas que perseguem o jogador.
    // - Barrigada: salta para outro ponto e a queda solta ondas de choque em sequência (pular cada uma).
    // - Cuspida em leque: rajadas de projéteis em leque na direção do jogador.
    [CreateAssetMenu(menuName = "Ícaro/Chefe Sapo", fileName = "Chefe_Sapo")]
    public class SapoDefinition : BossDefinition
    {
        [Header("Chuva ácida")]
        public Vector2Int AcidDrops = new Vector2Int(7, 11);
        public float AcidWarning = 1.1f;
        public float AcidRadius = 2.8f;
        public float AcidImpactDamage = 14f;
        public float PuddleDuration = 7f;
        public float PuddleTickDamage = 5f;
        public float PuddleTickInterval = 0.4f;

        [Header("Bolhas teleguiadas")]
        public Vector2Int Bubbles = new Vector2Int(5, 8);
        public float BubbleSpeed = 6f;
        public float BubbleTurnRate = 70f;
        public float BubbleDamage = 14f;
        public float BubbleLifetime = 7f;

        [Header("Barrigada")]
        public float LeapWarning = 0.9f;
        public float LeapAirTime = 1f;
        public float LeapDistance = 14f;
        public float ShockwaveSpeed = 13f;
        public float ShockwaveDamage = 20f;
        public Vector2Int Shockwaves = new Vector2Int(2, 3);
        public float ShockwaveInterval = 0.55f;

        [Header("Cuspida em leque")]
        public Vector2Int FanVolleys = new Vector2Int(3, 5);
        public int FanBullets = 7;
        public float FanAngle = 60f;
        public float FanSpeed = 13f;
        public float FanDamage = 9f;
        public float FanInterval = 0.35f;
    }
}
