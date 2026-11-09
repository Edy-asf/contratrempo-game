using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Sons da interface gerados em código (como a música do MusicComposer e o tiro silenciado do WeaponSounds), criados
    // na primeira vez que são pedidos. Tocam num AudioSource 2D próprio que ignora a pausa do áudio (os menus pausam o
    // AudioListener) e sai pelo grupo HUDObjective do mixer do Microgame, então o volume geral vale para eles.
    // - Hover: tique curto e agudo ao passar o mouse.
    // - Click: clique seco com um corpo grave (confirmar um botão, marcar uma carta).
    // - Unclick: o mesmo, mais grave (desmarcar).
    // - Deny: zumbido baixo de duas batidas (ação recusada).
    // - Deal: sopro curto de papel (uma carta saindo do monte).
    // - Flip: estalo de carta virando.
    // - Reveal: brilho subindo em arpejo (carta Incomum ou Rara aparecendo; Rare = uma oitava acima e mais longo).
    // - Confirm: acorde de dois toques subindo (escolha confirmada).
    // - Whoosh: sopro mais longo (cartas voando para o visor, telas trocando).
    // - Boot: varredura subindo com um zumbido (o visor do ÍCARO-9 ligando na abertura).
    public static class UiSounds
    {
        const int k_Rate = 44100;

        static AudioSource s_Source;
        static AudioClip s_Hover, s_Click, s_Unclick, s_Deny, s_Deal, s_Flip, s_Reveal, s_RevealRare, s_Confirm, s_Whoosh, s_Boot;

        public static AudioClip Hover => s_Hover ? s_Hover : s_Hover = Make("UI_Hover", 0.035f, HoverWave);
        public static AudioClip Click => s_Click ? s_Click : s_Click = Make("UI_Clique", 0.09f, t => ClickWave(t, 1f));
        public static AudioClip Unclick => s_Unclick ? s_Unclick : s_Unclick = Make("UI_Desmarcar", 0.09f, t => ClickWave(t, 0.72f));
        public static AudioClip Deny => s_Deny ? s_Deny : s_Deny = Make("UI_Recusar", 0.22f, DenyWave);
        public static AudioClip Deal => s_Deal ? s_Deal : s_Deal = Noise("UI_Carta", 0.16f, 0.5f, 0.12f, 30f, 3);
        public static AudioClip Flip => s_Flip ? s_Flip : s_Flip = Make("UI_Virar", 0.07f, FlipWave);
        public static AudioClip Reveal => s_Reveal ? s_Reveal : s_Reveal = Make("UI_Revelar", 0.5f, t => Arpeggio(t, 1f, 0.5f));
        public static AudioClip RevealRare => s_RevealRare ? s_RevealRare : s_RevealRare = Make("UI_RevelarRara", 0.85f, t => Arpeggio(t, 2f, 0.85f));
        public static AudioClip Confirm => s_Confirm ? s_Confirm : s_Confirm = Make("UI_Confirmar", 0.45f, ConfirmWave);
        public static AudioClip Whoosh => s_Whoosh ? s_Whoosh : s_Whoosh = Noise("UI_Sopro", 0.4f, 0.32f, 0.05f, 7f, 11);
        public static AudioClip Boot => s_Boot ? s_Boot : s_Boot = Make("UI_Ligar", 1.1f, BootWave);

        // Toca um som da interface (volume de 0 a 1, tom multiplicado por 'pitch')
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!clip)
                return;
            if (!s_Source)
            {
                var go = new GameObject("SonsDaInterface");
                Object.DontDestroyOnLoad(go);
                s_Source = go.AddComponent<AudioSource>();
                s_Source.playOnAwake = false;
                s_Source.spatialBlend = 0f;
                s_Source.ignoreListenerPause = true;
                if (Object.FindAnyObjectByType<AudioManager>())
                    s_Source.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDObjective);
            }

            s_Source.pitch = pitch;
            s_Source.PlayOneShot(clip, volume);
        }

        // ---------- ondas ----------

        static float HoverWave(float t)
        {
            float env = Mathf.Exp(-t * 140f);
            return (Square(t * 2400f) * 0.18f + Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.12f) * env;
        }

        static float ClickWave(float t, float pitch)
        {
            float snap = Mathf.Sin(2f * Mathf.PI * 1800f * pitch * t) * Mathf.Exp(-t * 120f) * 0.35f;
            float body = Mathf.Sin(2f * Mathf.PI * 220f * pitch * t) * Mathf.Exp(-t * 40f) * 0.45f;
            float tick = t < 0.003f ? (1f - t / 0.003f) * 0.4f : 0f;
            return snap + body + tick;
        }

        static float DenyWave(float t)
        {
            float beat = t < 0.09f ? Mathf.Exp(-t * 18f) : t > 0.11f ? Mathf.Exp(-(t - 0.11f) * 18f) : 0f;
            return Square(t * 110f) * 0.22f * beat + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.2f * beat;
        }

        static float FlipWave(float t)
        {
            float env = Mathf.Exp(-t * 90f);
            return (Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 2700f * t) * 0.12f) * env;
        }

        // Arpejo de lá menor (o tom da música) subindo, com um brilho agudo por cima
        static float Arpeggio(float t, float octave, float length)
        {
            float[] notes = { 440f, 523.25f, 659.25f, 880f };
            float step = length * 0.16f;
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * step;
                if (t < start)
                    break;
                float u = t - start;
                float env = Mathf.Min(1f, u / 0.005f) * Mathf.Exp(-u * 7f / length);
                float f = notes[i] * octave;
                sum += (Mathf.Sin(2f * Mathf.PI * f * u) * 0.6f + Triangle(f * 2f * u) * 0.15f) * env;
            }

            float shimmer = Mathf.Sin(2f * Mathf.PI * 5274f * octave * 0.5f * t) * 0.05f *
                            Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length)) * (0.5f + 0.5f * Mathf.Sin(t * 90f));
            return (sum * 0.32f + shimmer) * Mathf.Clamp01((length - t) / 0.05f);
        }

        static float ConfirmWave(float t)
        {
            float a = Note(t, 0f, 659.25f) + Note(t, 0f, 880f) * 0.6f;
            float b = Note(t, 0.09f, 987.77f) + Note(t, 0.09f, 1318.5f) * 0.5f;
            return (a + b) * 0.22f;
        }

        // Varredura de 140 Hz a 1100 Hz (fase integrada) com tremolo rápido e um zumbido grave que some
        static float BootWave(float t)
        {
            const float length = 1.1f;
            float u = t / length;
            float phase = 140f * t + (1100f - 140f) * 0.5f * t * u;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Clamp01((length - t) / 0.25f);
            float sweep = (Mathf.Sin(2f * Mathf.PI * phase) * 0.5f + Triangle(phase * 2f) * 0.12f) *
                          (0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 22f));
            float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f * Mathf.Exp(-t * 3f);
            return (sweep * 0.28f + hum * 0.3f) * env;
        }

        static float Note(float t, float start, float f)
        {
            if (t < start)
                return 0f;
            float u = t - start;
            return Mathf.Sin(2f * Mathf.PI * f * u) * Mathf.Min(1f, u / 0.004f) * Mathf.Exp(-u * 9f);
        }

        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Triangle(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);

        // ---------- montagem ----------

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

        // Ruído filtrado com envelope em sino (sopro): o filtro abre de 'open' até 'close' ao longo do som
        static AudioClip Noise(string name, float duration, float open, float close, float decay, int seed)
        {
            var random = new System.Random(seed);
            float low = 0f, lower = 0f;
            return Make(name, duration, t =>
            {
                float u = t / duration;
                float cutoff = Mathf.Lerp(open, close, u);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * cutoff;
                lower += (low - lower) * cutoff;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f)) * Mathf.Exp(-u * decay * 0.1f);
                return lower * env * 0.9f;
            });
        }
    }
}
