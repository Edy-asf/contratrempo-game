using UnityEngine;

namespace Icaro
{
    // Sons de arma gerados em código (como a música do MusicComposer), criados na primeira vez que são pedidos.
    // Suppressed: tiro com silenciador, um "pft" abafado: estalo mecânico curto do ferrolho, sopro de gás filtrado
    // (ruído passando por um passa-baixa que vai fechando) e um baque grave bem baixo, tudo em ~0,16 s. Cada chamada
    // devolve o mesmo AudioClip; a variação de tom fica por conta do Microgame (AudioSource) ou de quem toca.
    public static class WeaponSounds
    {
        const int k_SampleRate = 44100;
        static AudioClip s_Suppressed;

        public static AudioClip Suppressed
        {
            get
            {
                if (s_Suppressed)
                    return s_Suppressed;

                const float duration = 0.16f;
                int count = Mathf.CeilToInt(duration * k_SampleRate);
                var samples = new float[count];
                var random = new System.Random(7);
                float low = 0f, lower = 0f;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)k_SampleRate;
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                    // Sopro: o filtro começa aberto e fecha rápido (o "pft")
                    float cutoff = Mathf.Lerp(0.55f, 0.06f, Mathf.Clamp01(t / 0.07f));
                    low += (noise - low) * cutoff;
                    lower += (low - lower) * 0.5f;
                    float puff = lower * Mathf.Exp(-t * 38f) * 1.6f;

                    // Estalo do ferrolho nos primeiros 4 ms
                    float click = t < 0.004f ? noise * (1f - t / 0.004f) * 0.55f : 0f;

                    // Baque grave
                    float thump = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 45f) * 0.35f;

                    samples[i] = Mathf.Clamp(puff + click + thump, -1f, 1f);
                }

                s_Suppressed = AudioClip.Create("TiroSilenciado", count, 1, k_SampleRate, false);
                s_Suppressed.SetData(samples, 0);
                return s_Suppressed;
            }
        }
    }
}
