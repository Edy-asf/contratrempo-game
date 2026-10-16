using UnityEngine;

namespace Icaro
{
    // Pulso de Kairós: o ritmo que acompanha o multiplicador de tempo. Com música, a fase vem do relógio dela
    // (TimeMusic, 128 BPM acelerando até 1,35x no teto); sem música, avança sozinha em BpmFor. Acima de
    // DoubleTimeAbove (intensidade), o pulso visual bate em tempo dobrado.
    public static class KairosBeat
    {
        public const float BaseBpm = 128f;
        public const float BpmAtMaxFactor = 1.35f;
        public const float DoubleTimeAbove = 0.6f;

        // Andamento sem música (o mesmo da música padrão)
        public static float BpmFor(float multiplier) => BaseBpm * Mathf.Lerp(1f, BpmAtMaxFactor, Intensity);

        // Fase em batidas (avançada pela TimeAtmosphere uma vez por frame); a parte fracionária é a posição na batida
        public static double Phase { get; internal set; }

        // Batimento duplo ("tum-tum") de 0 a 1: pico no começo da batida e um menor logo depois
        public static float Pulse
        {
            get
            {
                double phase = Intensity > DoubleTimeAbove ? Phase * 2.0 : Phase;
                float t = (float)(phase - System.Math.Floor(phase));
                float first = Mathf.Exp(-t * 14f);
                float second = t > 0.22f ? 0.55f * Mathf.Exp(-(t - 0.22f) * 14f) : 0f;
                return Mathf.Clamp01(first + second);
            }
        }

        // 0 a 1,0x, 1 no teto do multiplicador
        public static float Intensity
        {
            get
            {
                float max = TimeManager.CurrentMaxMultiplier;
                return max > 1f ? Mathf.Clamp01((TimeManager.CurrentMultiplier - 1f) / (max - 1f)) : 0f;
            }
        }
    }
}
