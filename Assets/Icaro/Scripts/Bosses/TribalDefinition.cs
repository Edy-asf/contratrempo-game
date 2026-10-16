using UnityEngine;

namespace Icaro
{
    // Guardião Tiki, chefe das Ruínas (terceiro). Flutua acima do chão. Ataques:
    // - Feixes de runa: feixes que giram em volta dele; os baixos pedem pulo, os altos (fase 2) pedem agachar (C).
    // - Espiral: braços de projéteis girando (na fase 2, uma segunda espiral no sentido contrário).
    // - Salto de runa: some e reaparece em outro ponto da arena, soltando um anel de projéteis.
    [CreateAssetMenu(menuName = "Ícaro/Chefe Tribal", fileName = "Chefe_Tribal")]
    public class TribalDefinition : BossDefinition
    {
        [Header("Flutuar")]
        public float HoverHeight = 1.2f;

        [Header("Feixes de runa")]
        public Vector2Int Beams = new Vector2Int(2, 3);
        public float BeamLength = 30f;
        public float BeamLowHeight = 0.5f;
        public float BeamHighHeight = 1.5f;
        public float BeamSpeed = 45f;
        public float BeamDuration = 7f;
        public float BeamDamage = 22f;
        public float BeamWarning = 1f;

        [Header("Espiral")]
        public Vector2Int SpiralArms = new Vector2Int(4, 5);
        public float SpiralShotsPerSecond = 10f;
        public float SpiralTurnSpeed = 80f;
        public float SpiralDuration = 4.5f;
        public float SpiralBulletSpeed = 9f;
        public float SpiralDamage = 9f;

        [Header("Salto de runa")]
        public float BlinkWarning = 0.8f;
        public float BlinkRange = 16f;
        public int BlinkRingBullets = 20;
        public float BlinkRingSpeed = 10f;
        public float BlinkRingDamage = 11f;
    }
}
