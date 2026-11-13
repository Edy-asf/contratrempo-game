using UnityEngine;

namespace Icaro
{
    // Cascudo (para o jogador, o Orbe): corpo a corpo, lento e resistente. Anda aos pulinhos até o jogador e, perto,
    // agacha brilhando (o aviso) e salta até onde o jogador estava no começo do aviso, com dano em área ao cair. Uma
    // marca no chão mostra onde ele vai cair durante o aviso e o salto. Damage é o dano da queda e AttackCooldown a
    // recarga entre saltos. A velocidade base vem do jogador: MaxSpeedOnGround do PlayerCharacterController /
    // PlayerSpeedDivisor (com o multiplicador no teto de 3x, anda tão rápido quanto o jogador).
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Cascudo", fileName = "Inimigo_Cascudo")]
    public class CascudoDefinition : EnemyDefinition
    {
        [Header("Movimento")]
        [Tooltip("Velocidade base = velocidade em solo (sem correr) do jogador / este valor")]
        public float PlayerSpeedDivisor = 3f;

        [Tooltip("Velocidade usada se o jogador não for encontrado (m/s a 1,0x)")]
        public float FallbackSpeed = 4.3f;

        [Tooltip("Altura dos pulinhos ao andar (m)")]
        public float HopHeight = 0.15f;

        [Tooltip("Pulinhos por metro andado")]
        public float HopsPerMeter = 0.9f;

        [Header("Salto esmagador")]
        [Tooltip("Distância ao jogador em que ele para e começa o aviso do salto")]
        public float AttackRange = 7f;

        [Tooltip("Aviso do salto (agacha e brilha) a 1,0x; nunca abaixo de 60% com o multiplicador")]
        public float WindupDuration = 0.8f;

        [Tooltip("Distância máxima do salto; mais longe, cai no caminho até o jogador")]
        public float LeapMaxDistance = 9f;

        [Tooltip("Altura do arco do salto (m)")]
        public float LeapHeight = 1.8f;

        [Tooltip("Tempo no ar a 1,0x")]
        public float LeapTime = 0.55f;

        [Tooltip("Tempo agachado depois de cair (a onda no chão), a 1,0x")]
        public float LandTime = 0.4f;

        [Tooltip("Raio do dano em área ao cair")]
        public float SlamRadius = 3f;

        [Tooltip("Acima desta altura sobre o chão do impacto, o jogador (pulando) escapa do dano")]
        public float SlamDodgeHeight = 1.2f;
    }
}
