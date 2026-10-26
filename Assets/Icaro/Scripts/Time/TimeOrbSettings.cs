using UnityEngine;

namespace Icaro
{
    // Ajustes do orbe do tempo (CombatBalance.TimeOrbs): quantos inimigos nascem marcados por sala, quanto cada acerto
    // reduz do multiplicador pela precisão e o ritmo do anel que fecha.
    [System.Serializable]
    public class TimeOrbSettings
    {
        [Tooltip("Prefab do orbe (TimeOrb), criado por Ícaro > Gerar prefabs")]
        public GameObject Prefab;

        [Header("Inimigos marcados")]
        [Tooltip("Um inimigo marcado a cada tantos da cota da sala (os voadores do pool não são marcados)")]
        [Min(1)] public int MarkEvery = 8;

        [Tooltip("Marcados no mínimo em cada sala de combate")]
        [Min(0)] public int MarkMin = 1;

        [Tooltip("Orbes que o chefe solta ao morrer (a sala do chefe não tem marcados)")]
        [Min(0)] public int BossOrbs = 2;

        [Header("Redução do multiplicador")]
        [Tooltip("Acerto no toque do anel (dentro da janela Perfeito)")]
        [Min(0f)] public float PerfectReduction = 0.5f;

        [Tooltip("Acerto quase no toque (dentro da janela Bom)")]
        [Min(0f)] public float GoodReduction = 0.3f;

        [Tooltip("Acerto fora das janelas (cedo ou tarde)")]
        [Min(0f)] public float OffReduction = 0.1f;

        [Header("Timing")]
        [Tooltip("Segundos do anel encolhendo até encostar no orbe")]
        [Min(0.3f)] public float RingDuration = 1.4f;

        [Tooltip("Pausa depois do toque antes do próximo anel (s)")]
        [Min(0f)] public float CycleGap = 0.35f;

        [Tooltip("Quantos anéis o orbe mostra antes de sumir")]
        [Min(1)] public int Cycles = 3;

        [Tooltip("Janela Perfeito: segundos antes ou depois do toque")]
        [Min(0.01f)] public float PerfectWindow = 0.08f;

        [Tooltip("Janela Bom: segundos antes ou depois do toque")]
        [Min(0.01f)] public float GoodWindow = 0.22f;

        [Header("Orbe")]
        [Tooltip("Impulso inicial: quanto o orbe sobe logo ao sair do inimigo (m)")]
        public float RiseHeight = 1.6f;

        [Tooltip("Depois do impulso, continua subindo sem parar a esta velocidade (m/s)")]
        [Min(0f)] public float RiseSpeed = 0.35f;

        [Tooltip("Quanto deriva para os lados, como uma partícula no ar (m)")]
        [Min(0f)] public float Wander = 0.8f;

        [Tooltip("Rapidez da deriva para os lados")]
        [Min(0f)] public float WanderSpeed = 0.45f;

        [Tooltip("Raio do anel quando começa a fechar (m)")]
        public float RingStartRadius = 2.4f;

        [Tooltip("Raio do orbe (o anel encosta nele) e da área que o tiro precisa acertar (m)")]
        public float OrbRadius = 0.4f;
        public float HitRadius = 0.6f;
    }
}
