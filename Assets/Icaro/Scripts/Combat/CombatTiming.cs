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

        // ---------- relógio do mundo (habilidades de tempo) ----------
        // Velocidade do mundo dos inimigos: 1 normal, menos de 1 na câmera lenta da habilidade (TimeSlowAbility), 0
        // enquanto o tempo volta (TimeRewind). Inimigos, projéteis deles, salas e cenário contam o tempo por DeltaTime,
        // Now e Wait; o jogador continua no tempo normal (nunca usar Time.timeScale para isso).
        static float s_WorldSpeed = 1f, s_BaseNow, s_BaseTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_WorldSpeed = 1f;
            s_BaseNow = s_BaseTime = 0f;
        }

        public static float WorldSpeed
        {
            get => s_WorldSpeed;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(value, s_WorldSpeed))
                    return;
                // O relógio continua de onde estava, só muda o ritmo
                s_BaseNow = Now;
                s_BaseTime = Time.time;
                s_WorldSpeed = value;
            }
        }

        // Segundos do mundo dos inimigos (no lugar de Time.time) e o passo do frame (no lugar de Time.deltaTime)
        public static float Now => s_BaseNow + (Time.time - s_BaseTime) * s_WorldSpeed;
        public static float DeltaTime => Time.deltaTime * s_WorldSpeed;

        // Espera em segundos do mundo (no lugar de WaitForSeconds)
        public static CustomYieldInstruction Wait(float seconds) => new WorldWait(seconds);

        sealed class WorldWait : CustomYieldInstruction
        {
            readonly float m_End;
            public WorldWait(float seconds) => m_End = Now + Mathf.Max(0f, seconds);
            public override bool keepWaiting => Now < m_End;
        }

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
