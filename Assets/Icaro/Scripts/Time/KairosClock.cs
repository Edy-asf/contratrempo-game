using UnityEngine;

namespace Icaro
{
    // Relógio do mundo de Kairós: fora de combate o mundo fica congelado (céu, nuvens, névoa, partículas, chamas,
    // anéis, bandeirolas) e só o jogador se mexe. Scale vai de 1 (luta) a 0 (congelado) com uma rampa (WorldFreeze);
    // scripts do cenário usam DeltaTime e WorldTime no lugar de Time.deltaTime e Time.time, e os shaders leem o global
    // _IcaroWorldTime. Sem TimeManager na cena, Scale fica em 1 e tudo anda como antes.
    public static class KairosClock
    {
        // 1 = o mundo anda, 0 = congelado
        public static float Scale { get; internal set; } = 1f;

        // Segundos do mundo (só andam com ele)
        public static float WorldTime { get; internal set; }

        public static float DeltaTime => Time.deltaTime * Scale * CombatTiming.WorldSpeed;

        public static bool Frozen => Scale <= 0.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Scale = 1f;
            WorldTime = 0f;
        }
    }
}
