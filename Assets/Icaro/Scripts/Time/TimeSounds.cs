using UnityEngine;

namespace Icaro
{
    // Sons do tempo gerados em código (tocados pelo UiSounds, no grupo HUDObjective do mixer): o mundo parando
    // (Freeze), voltando a andar (Unfreeze), o tempo voltando num abate (Rewind) e os marcos do multiplicador
    // (MilestoneUp ao passar de um inteiro subindo, MilestoneDown descendo).
    public static class TimeSounds
    {
        const int k_Rate = 44100;

        static AudioClip s_Freeze, s_Unfreeze, s_Rewind, s_Up, s_Down, s_Drain, s_SlowIn, s_SlowOut, s_Tape;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() =>
            s_Freeze = s_Unfreeze = s_Rewind = s_Up = s_Down = s_Drain = s_SlowIn = s_SlowOut = s_Tape = null;

        // Duração da descida até 1,0x para a qual o som foi feito (tocado com o tom ajustado a outras durações)
        public const float DrainLength = 1.5f;

        public static AudioClip Drain => s_Drain ? s_Drain : s_Drain = Make("Tempo_Descer", DrainLength + 0.6f, DrainWave);

        public static AudioClip Freeze => s_Freeze ? s_Freeze : s_Freeze = Make("Tempo_Parar", 1.5f, FreezeWave);
        public static AudioClip Unfreeze => s_Unfreeze ? s_Unfreeze : s_Unfreeze = Make("Tempo_Voltar", 0.9f, UnfreezeWave);
        public static AudioClip Rewind => s_Rewind ? s_Rewind : s_Rewind = Make("Tempo_Rebobinar", 0.42f, RewindWave);
        public static AudioClip MilestoneUp => s_Up ? s_Up : s_Up = Make("Tempo_Marco_Sobe", 1.6f, t => Bell(t, 98f, 1.6f));
        public static AudioClip MilestoneDown => s_Down ? s_Down : s_Down = Make("Tempo_Marco_Desce", 0.9f, t => Bell(t, 392f, 0.9f) * 0.7f);

        // Habilidades: o mundo entrando em câmera lenta (um "vuuum" grave que desce, com o coração batendo devagar),
        // saindo dela (o mesmo subindo) e a fita voltando (TapeLength segundos de rebobinar)
        public const float TapeLength = 0.9f;
        public static AudioClip SlowIn => s_SlowIn ? s_SlowIn : s_SlowIn = Make("Tempo_Lento", 1.6f, SlowInWave);
        public static AudioClip SlowOut => s_SlowOut ? s_SlowOut : s_SlowOut = Make("Tempo_Lento_Fim", 0.8f, SlowOutWave);
        public static AudioClip Tape => s_Tape ? s_Tape : s_Tape = Make("Tempo_Fita", TapeLength + 0.35f, TapeWave);

        static float SlowInWave(float t)
        {
            // Varredura descendo de 260 Hz a 45 Hz, com um sopro grave e duas batidas de coração
            float u = Mathf.Clamp01(t / 1.2f);
            float phase = 260f * t - 215f * t * u * 0.5f;
            float env = Mathf.Min(1f, t / 0.03f) * Mathf.Clamp01((1.6f - t) / 0.6f);
            float sweep = (Mathf.Sin(2f * Mathf.PI * phase) * 0.5f + Mathf.Sin(2f * Mathf.PI * phase * 0.5f) * 0.35f) * env;
            float air = Hash(t) * 0.06f * env * (1f - u);
            float beat = Mathf.Sin(2f * Mathf.PI * 52f * t) * (Pulse(t - 0.35f) + Pulse(t - 1.05f) * 0.8f);
            return sweep * 0.4f + air + beat * 0.55f;
        }

        static float SlowOutWave(float t)
        {
            float u = Mathf.Clamp01(t / 0.6f);
            float phase = 50f * t + 300f * t * u * 0.5f;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Clamp01((0.8f - t) / 0.3f);
            float sweep = (Mathf.Sin(2f * Mathf.PI * phase) * 0.45f + Mathf.Sin(2f * Mathf.PI * phase * 2f) * 0.15f) * env;
            return sweep * 0.4f + Tick(t - 0.62f, 1800f, 0.35f);
        }

        // Fita rebobinando: chiado com tom subindo e estalos invertidos cada vez mais juntos, cortando no fim
        static float TapeWave(float t)
        {
            float u = Mathf.Clamp01(t / TapeLength);
            float env = Mathf.Min(1f, t / 0.04f) * (t < TapeLength ? 1f : Mathf.Clamp01(1f - (t - TapeLength) / 0.05f));
            float phase = 320f * t + 900f * t * u * 0.5f;
            float whir = (Mathf.Sin(2f * Mathf.PI * phase) * 0.3f + Mathf.Sin(2f * Mathf.PI * phase * 1.5f) * 0.15f) *
                         (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t));
            float hiss = Hash(t) * 0.12f;
            float ticks = 0f;
            float at = 0.03f;
            for (int i = 0; i < 40 && at < TapeLength; i++)
            {
                float d = at - t;
                if (d >= 0f && d < 0.025f)
                    ticks += Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-d * 180f) * 0.3f;
                at += Mathf.Lerp(0.11f, 0.03f, at / TapeLength);
            }

            float thud = t >= TapeLength ? Mathf.Sin(2f * Mathf.PI * 60f * (t - TapeLength)) *
                                           Mathf.Exp(-(t - TapeLength) * 9f) * 0.6f : 0f;
            return (whir + hiss) * env * 0.5f + ticks + thud;
        }

        // Batida de coração (envelope curto)
        static float Pulse(float t) => t < 0f ? 0f : Mathf.Exp(-t * 18f) * Mathf.Min(1f, t / 0.005f);

        // Fita parando: um acorde que desce de tom até sumir, com chiado, e um "tique" seco de relógio no fim
        static float FreezeWave(float t)
        {
            const float stop = 1.15f;
            float u = Mathf.Clamp01(t / stop);
            // Frequência caindo como uma fita: a fase é a integral de f0 * (1 - u)^2
            float phase = 220f * stop * (1f - Mathf.Pow(1f - u, 3f)) / 3f;
            float env = Mathf.Min(1f, t / 0.02f) * (1f - u) * (1f - u);
            float tone = (Mathf.Sin(2f * Mathf.PI * phase) * 0.5f + Mathf.Sin(2f * Mathf.PI * phase * 1.5f) * 0.25f +
                          Mathf.Sin(2f * Mathf.PI * phase * 2f) * 0.18f) * env;
            float hiss = Hash(t) * 0.05f * env;
            float tick = Tick(t - stop, 2100f, 0.5f) + Tick(t - stop, 420f, 0.35f);
            return tone * 0.45f + hiss + tick;
        }

        // Um "taque" de relógio e a fita voltando a rodar (varredura subindo) com um baque grave
        static float UnfreezeWave(float t)
        {
            float tock = Tick(t, 1500f, 0.55f) + Tick(t, 300f, 0.45f);
            float u = Mathf.Clamp01(t / 0.6f);
            float phase = 60f * t + 520f * t * u * 0.5f;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Clamp01((0.85f - t) / 0.3f);
            float sweep = (Mathf.Sin(2f * Mathf.PI * phase) * 0.4f + Mathf.Sin(2f * Mathf.PI * phase * 2f) * 0.15f) * env;
            float boom = Mathf.Sin(2f * Mathf.PI * 48f * t) * Mathf.Exp(-t * 6f) * 0.5f;
            return tock + sweep * 0.35f + boom * 0.6f;
        }

        // Tique ao contrário: estalos que se aproximam, um sopro que cresce e corta seco (como um som invertido)
        static readonly float[] s_RewindTicks = { 0.04f, 0.16f, 0.25f, 0.31f };

        static float RewindWave(float t)
        {
            const float length = 0.42f;
            float sum = 0f;
            for (int i = 0; i < s_RewindTicks.Length; i++)
            {
                // Estalo invertido: cresce até o ponto e corta
                float d = s_RewindTicks[i] - t;
                if (d >= 0f && d < 0.03f)
                    sum += Mathf.Sin(2f * Mathf.PI * (1700f + i * 260f) * t) * Mathf.Exp(-d * 160f) * 0.32f;
            }

            float u = Mathf.Clamp01(t / length);
            float cut = Mathf.Clamp01((length - t) / 0.01f);
            float swell = Hash(t) * Mathf.Pow(u, 3f) * cut * 0.22f;
            float glide = Mathf.Sin(2f * Mathf.PI * (900f - 500f * u) * t) * Mathf.Pow(u, 2.5f) * cut * 0.18f;
            return sum + swell + glide;
        }

        // Relógio voltando: tiques que aceleram e sobem de tom durante a descida e um sino suave quando chega a 1,0x
        static float DrainWave(float t)
        {
            float sum = 0f;
            // Instantes dos tiques: o intervalo vai de 0,12 s a 0,028 s
            float at = 0f;
            for (int i = 0; i < 80 && at < DrainLength; i++)
            {
                float u = at / DrainLength;
                float d = t - at;
                if (d >= 0f && d < 0.05f)
                    sum += Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1300f, 2600f, u) * d) * Mathf.Exp(-d * 120f) *
                           Mathf.Lerp(0.22f, 0.32f, u);
                at += Mathf.Lerp(0.12f, 0.028f, u * (2f - u));
            }

            float chime = t >= DrainLength ? Bell(t - DrainLength, 659.25f, 0.6f) * 0.6f : 0f;
            return sum + chime;
        }

        // Sino de bronze (parciais inarmônicas)
        static float Bell(float t, float f, float length)
        {
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4f / length);
            float sum = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f +
                        Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * 0.25f * Mathf.Exp(-t * 3f) +
                        Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * 0.12f * Mathf.Exp(-t * 6f) +
                        Mathf.Sin(2f * Mathf.PI * f * 0.5f * t) * 0.2f;
            return sum * env * 0.5f;
        }

        static float Tick(float t, float f, float gain)
        {
            if (t < 0f)
                return 0f;
            return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 90f) * gain;
        }

        static float Hash(float t)
        {
            float x = Mathf.Sin(t * 12345.678f) * 43758.5453f;
            return (x - Mathf.Floor(x)) * 2f - 1f;
        }

        static AudioClip Make(string name, float duration, System.Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(duration * k_Rate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Clamp(wave(i / (float)k_Rate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, count, 1, k_Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
