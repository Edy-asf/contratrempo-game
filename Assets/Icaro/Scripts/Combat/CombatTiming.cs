using UnityEngine;

namespace Icaro
{
    // Durações de combate sob o multiplicador de tempo de Kairós.
    // Recargas e movimentos encurtam junto com o multiplicador; avisos de ataque (o tempo que o jogador tem para
    // reagir) encurtam também, mas nunca abaixo de WarningFloor da duração original.
    public static class CombatTiming
    {
        // Regra do jogo (CLAUDE.md): um aviso nunca dura menos que 60% da duração a 1,0x
        public const float WarningFloor = 0.6f;

        public static float Multiplier => TimeManager.CurrentMultiplier;

        // Recarga, intervalo ou duração de ação: base / multiplicador
        public static float Scaled(float baseDuration) => baseDuration / Multiplier;

        // Multiplicador sobre o movimento de um inimigo: o geral, limitado pelo teto de movimento da ficha (se houver)
        public static float Movement(EnemyStats stats)
        {
            float cap = stats && stats.Definition ? stats.Definition.MovementMultiplierCap : 0f;
            float time = cap > 0f ? Mathf.Min(Multiplier, cap) : Multiplier;
            // Elites velozes andam mais rápido por cima do tempo; a lentidão (TimeAnchorSlow) e a água rasa deixam mais lento
            return time * (stats ? stats.MoveMultiplier * stats.SlowMultiplier * stats.WaterMultiplier : 1f);
        }

        // Aviso de ataque: base / multiplicador, com piso de 60%
        public static float Warning(float baseDuration) =>
            baseDuration * Mathf.Max(1f / Multiplier, WarningFloor);
    }
}
