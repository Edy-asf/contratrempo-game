using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Assistência de mira do jogador (AimAssist + HomingProjectile + AimAssistHUD). Pronta para virar opção do menu
    // de configurações: Enabled e Intensity são os campos que o jogador mudaria.
    [CreateAssetMenu(menuName = "Ícaro/Assistência de mira", fileName = "AimAssistSettings")]
    public class AimAssistSettings : ScriptableObject
    {
        [Tooltip("Liga a assistência; desligada, o tiro sai reto no centro da tela")]
        public bool Enabled = true;

        [Tooltip("0 a 100%: em 0 o tiro sai reto; em valores intermediários a zona e a taxa de giro diminuem na mesma " +
                 "proporção")]
        [Range(0f, 1f)] public float Intensity = 1f;

        [Tooltip("Raio da zona de assistência, em fração da altura da tela (0,08 = 8%). O mesmo valor define o tamanho " +
                 "dos colchetes da mira, então os dois nunca ficam diferentes")]
        [Range(0.01f, 0.4f)] public float ZoneRadius = 0.08f;

        [Tooltip("Alcance máximo para um inimigo ser alvo, em metros (o bastante para cruzar a arena)")]
        [Min(1f)] public float MaxRange = 80f;

        [Tooltip("Giro máximo do projétil em direção ao alvo, em graus por segundo")]
        [Min(0f)] public float TurnRate = 720f;

        [Tooltip("Nos últimos metros, a direção trava no alvo (não passa de raspão em alvos pequenos e rápidos)")]
        [Min(0f)] public float LockDistance = 2.5f;

        [Tooltip("Segundos entre as checagens de linha de visão do projétil até o alvo (bloqueou: segue reto)")]
        [Min(0.01f)] public float LineOfSightInterval = 0.05f;

        [Tooltip("Peso do desempate pela distância no mundo (pequeno: o que manda é a distância do centro da zona)")]
        [Range(0f, 1f)] public float WorldDistanceTieBreak = 0.15f;

        [Header("Depuração")]
        [Tooltip("Desenha a zona e destaca o alvo escolhido")]
        public bool DebugView;

        [Tooltip("Tecla que liga e desliga a depuração em jogo")]
        public Key DebugKey = Key.F3;

        public bool IsActive => Enabled && Intensity > 0f;

        // Zona e giro efetivos, já com a intensidade
        public float EffectiveZoneRadius => IsActive ? ZoneRadius * Intensity : 0f;
        public float EffectiveTurnRate => IsActive ? TurnRate * Intensity : 0f;
    }
}
