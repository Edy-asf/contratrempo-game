using UnityEngine;

namespace Icaro
{
    // Pé-de-Musgo, chefe do Bosque (substitui o Rei Fungo): caçador de emboscada coberto de líquen. Ataques:
    // - Fase 1: salto esmagador (agacha, a sombra do pouso aparece no chão, cai e solta uma onda de terra para pular),
    //   agarrão (arma o braço e passa a garra num cone à frente; quem é pego fica sem controle e é arremessado) e
    //   arremesso de torrão (de longe, arranca um torrão do chão e joga em arco; a queda é marcada e espalha pedaços).
    // - Fase 2: rugido de esporos (ergue a cabeça e os olhos piscam; depois, nuvens de esporos com dano contínuo e vista
    //   curta); os cogumelos acesos do ombro são o ponto fraco (dano extra sempre; acertá-los no aviso do rugido
    //   interrompe e o atordoa); os saltos vêm em pares.
    // - Fase 3 (caçada): salta para a borda da arena e some na névoa, só os olhos aparecem; depois de alguns segundos,
    //   rosna (a seta de ameaça mostra de onde) e dá o bote. Alterna com os outros ataques.
    [CreateAssetMenu(menuName = "Ícaro/Chefe Pé-de-Musgo", fileName = "Chefe_PeDeMusgo")]
    public class PeDeMusgoDefinition : BossDefinition
    {
        [Header("Salto esmagador")]
        [Tooltip("Agachado antes de saltar (aviso, nunca abaixo de 60%)")]
        public float JumpWarning = 0.8f;
        public float JumpAirTime = 0.9f;
        public float JumpHeight = 7f;

        [Tooltip("A sombra do pouso aparece este tanto antes de cair")]
        public float ShadowLead = 1f;

        public float SlamRadius = 4.5f;
        public float SlamDamage = 30f;
        public float ShockwaveSpeed = 13f;
        public float ShockwaveDamage = 18f;

        [Tooltip("Aviso do segundo salto do par (fase 2 em diante), em fração do primeiro")]
        [Range(0.6f, 1f)] public float SecondJumpWarning = 0.75f;

        [Header("Agarrão")]
        [Tooltip("Só tenta o agarrão com o jogador até esta distância")]
        public float GrabTriggerDistance = 10f;
        public float GrabWarning = 0.8f;

        [Tooltip("Quanto avança no golpe (m)")]
        public float GrabLunge = 3.5f;
        public float GrabLungeTime = 0.22f;
        public float GrabRange = 6f;
        [Range(10f, 90f)] public float GrabHalfAngle = 50f;
        public float GrabDamage = 32f;

        [Tooltip("Sem controle depois de pego (s)")]
        public float GrabStun = 0.8f;

        [Tooltip("Velocidade do arremesso (para longe e para cima, m/s)")]
        public Vector2 GrabThrow = new Vector2(16f, 7f);

        [Header("Arremesso de torrão")]
        [Tooltip("Prefere o torrão com o jogador além desta distância")]
        public float ClodMinDistance = 12f;
        public float ClodWarning = 0.9f;
        public float ClodFlightTime = 1.3f;
        public float ClodRadius = 3f;
        public float ClodDamage = 24f;

        [Tooltip("Torrões por arremesso (fase 1, 2, 3)")]
        public Vector3Int ClodCount = new Vector3Int(1, 2, 3);

        [Tooltip("Pedaços que espalham pelo chão na queda")]
        public int ClodShards = 6;
        public float ShardSpeed = 11f;
        public float ShardDamage = 8f;

        [Header("Rugido de esporos (fase 2)")]
        public float RoarWarning = 1f;
        public float RoarTime = 1.2f;
        public int SporeClouds = 3;
        public float CloudRadius = 4f;
        public float CloudDuration = 8f;

        [Tooltip("Dano por segundo dentro da nuvem")]
        public float CloudDps = 9f;

        [Header("Ponto fraco (cogumelos do ombro)")]
        [Tooltip("Posição em relação aos pés (x para a direita dele, y altura); a frente vem do corpo")]
        public Vector2 WeakPointOffset = new Vector2(1.6f, 8.6f);
        public float WeakPointRadius = 0.9f;

        [Tooltip("Multiplica o dano de tiros nos cogumelos")]
        public float WeakPointMultiplier = 2f;

        [Tooltip("Atordoado ao ser acertado no aviso do rugido (s)")]
        public float StunTime = 2f;

        [Header("Caçada (fase 3)")]
        [Tooltip("Pulo até a borda da arena, em fração do raio do domo")]
        [Range(0.4f, 0.95f)] public float HuntEdge = 0.8f;
        public float HuntJumpAirTime = 0.9f;

        [Tooltip("Escondido antes do bote (s, sorteado)")]
        public Vector2 HuntHide = new Vector2(2f, 3f);

        [Tooltip("Rosnado e seta de ameaça antes do bote")]
        public float PounceWarning = 0.8f;
        public float PounceAirTime = 0.65f;
        public float PounceRadius = 3.8f;
        public float PounceDamage = 34f;
    }
}
