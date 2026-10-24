using System;

namespace Icaro
{
    // Compõe e renderiza as faixas da música do Ícaro (C# puro, seguro fora da thread principal, para renderizar em
    // segundo plano). Todas as faixas de um tema têm o mesmo andamento e duram um número inteiro de compassos, então
    // tocadas juntas ficam sincronizadas. Tema padrão "Via Aeônida": eletrônica sci-fi em lá menor, 4/4, 16
    // compassos, progressão Am F Dm E Am F G E (2 compassos cada), pensada para 128 BPM.
    // Faixas (o TimeMusic liga as camadas com o multiplicador):
    // - Base: bumbo em todo tempo, baixo nos contratempos, pads "supersaw" com bombeamento (sidechain) e pluck em
    //   colcheias. Toca sempre.
    // - Clock: engrenagens do relógio em semicolcheias, com o tique forte em cada tempo.
    // - Hats: chimbais em semicolcheias, chimbal aberto nos contratempos e palmas no 2 e no 4.
    // - Acid: linha de baixo em semicolcheias com filtro ressonante, acentos e glissandos, abrindo ao longo do tema.
    // - Breaks: quebrada de caixas e bumbos sincopados (sensação de drum'n'bass) com virada a cada 2 compassos.
    // - Lead: riff de serra agudo em semicolcheias, sirene, rufada de chimbal em fusas e subida de ruído a cada
    //   8 compassos.
    public static class MusicComposer
    {
        public enum Layer
        {
            Base,
            Clock,
            Hats,
            Acid,
            Breaks,
            Lead,
        }

        public const int BeatsPerBar = 4;
        const int k_ChordBars = 2;
        const double TwoPi = Math.PI * 2.0;

        // Fundamental (MIDI, oitava do baixo) e se é menor, por par de compassos
        static readonly int[] k_ChordRoots = { 45, 41, 38, 40, 45, 41, 43, 40 };
        static readonly bool[] k_ChordMinor = { true, false, true, false, true, false, false, false };

        // Linha acid: semitons sobre a fundamental, acentos e glissandos (16 semicolcheias)
        static readonly int[] k_AcidNotes = { 0, 0, 12, 0, 3, 0, 7, 10, 0, 12, 0, 3, 5, 7, 12, 15 };
        static readonly bool[] k_AcidAccent =
            { true, false, false, true, false, false, true, false, true, false, false, true, false, false, true, false };
        static readonly bool[] k_AcidSlide =
            { false, false, true, false, false, false, false, true, false, false, false, false, true, false, false, false };

        // Quebrada (32 semicolcheias = 2 compassos): 1 bumbo, 2 caixa, 3 caixa fantasma. Os bumbos ficam fora dos
        // tempos fortes, que já têm o bumbo da base
        static readonly int[] k_Breaks =
        {
            0, 0, 0, 1, 2, 0, 3, 0, 0, 0, 1, 3, 2, 0, 3, 3,
            0, 0, 0, 1, 2, 0, 3, 0, 0, 0, 1, 0, 2, 3, 2, 2,
        };

        // Riff do lead: grau da tríade (0 fundamental, 1 terça, 2 quinta, 3 oitava) por semicolcheia
        static readonly int[] k_LeadRiff = { 3, 2, 1, 2, 0, 2, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2 };

        // Compassos da faixa: o relógio e os chimbais repetem 1 compasso, a quebrada 2; o resto segue o tema
        public static int BarsOf(Layer layer, int themeBars) => layer switch
        {
            Layer.Clock => 1,
            Layer.Hats => 1,
            Layer.Breaks => 2,
            _ => themeBars,
        };

        public static float[] Render(Layer layer, float bpm, int themeBars, int sampleRate)
        {
            int bars = BarsOf(layer, themeBars);
            double secondsPerBeat = 60.0 / bpm;
            int length = (int)Math.Round(bars * BeatsPerBar * secondsPerBeat * sampleRate);
            var output = new float[length];
            var s = new Sequencer(secondsPerBeat, sampleRate, themeBars);

            switch (layer)
            {
                case Layer.Base: RenderBase(output, s); break;
                case Layer.Clock: RenderClock(output, s); break;
                case Layer.Hats: RenderHats(output, s); break;
                case Layer.Acid: RenderAcid(output, s); break;
                case Layer.Breaks: RenderBreaks(output, s); break;
                default: RenderLead(output, s); break;
            }

            return output;
        }

        // ---------- Base ----------

        static void RenderBase(float[] output, Sequencer s)
        {
            var bassOsc = new Saw();
            var bassFilter = new Filter();
            double subPhase = 0.0;
            var padOsc = new Saw[6];
            for (int v = 0; v < padOsc.Length; v++)
                padOsc[v] = new Saw();
            var padFilter = new Filter();
            var pluckOsc = new Saw();
            var pluckFilter = new Filter();
            var noise = new Noise(11u);

            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                int chord = s.Chord;
                double duck = s.Sidechain(0.65, 9.0);

                // Bumbo em todo tempo
                double sample = Kick(s.SinceBeat, ref noise) * 0.55;

                // Baixo nos contratempos: serra filtrada + sub
                double sinceOff = s.SinceEighth;
                if (s.EighthIndex % 2 == 1)
                {
                    double f = Freq(k_ChordRoots[chord]);
                    double env = Env(sinceOff, 0.004, 7.0);
                    double cutoff = 350.0 + 1800.0 * Math.Exp(-sinceOff * 12.0);
                    double saw = bassFilter.LowPass(bassOsc.Next(f, s.Rate), cutoff, 0.6, s.Rate);
                    subPhase += Freq(k_ChordRoots[chord] - 12) / s.Rate;
                    sample += (saw * 0.6 + Math.Sin(subPhase * TwoPi) * 0.7) * env * 0.32;
                }

                // Pads: tríade com duas serras desafinadas por nota, filtrada e bombeando com o bumbo
                double padEnv = Smooth(0.0, 0.25, s.SinceChord) * duck;
                double pad = 0.0;
                for (int v = 0; v < 3; v++)
                {
                    double f = Freq(TriadNote(chord, 24, v));
                    pad += padOsc[v * 2].Next(f * 1.006, s.Rate) + padOsc[v * 2 + 1].Next(f * 0.994, s.Rate);
                }
                sample += padFilter.LowPass(pad, 1500.0, 0.8, s.Rate) * padEnv * 0.05;

                // Pluck em colcheias com as notas do acorde
                int degree = s.EighthIndex % 4 == 3 ? 1 : s.EighthIndex % 4;
                double pf = Freq(TriadNote(chord, 36, Math.Min(degree, 2)));
                double pcut = 400.0 + 3200.0 * Math.Exp(-sinceOff * 16.0);
                double pluck = pluckFilter.LowPass(pluckOsc.Next(pf, s.Rate), pcut, 0.5, s.Rate);
                sample += pluck * Env(sinceOff, 0.002, 9.0) * (0.5 + 0.5 * duck) * 0.11;

                output[i] = (float)Math.Tanh(sample * 1.2);
            }
        }

        // ---------- camadas ----------

        // Relógio: tique metálico em cada tempo e engrenagens nas semicolcheias
        static void RenderClock(float[] output, Sequencer s)
        {
            var noise = new Noise(23u);
            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                double sample;
                if (s.SixteenthIndex % 4 == 0)
                {
                    double f = s.BeatIndex % 4 == 0 ? 2600.0 : 2000.0;
                    sample = (Math.Sin(TwoPi * f * s.SinceSixteenth) * 0.6 + noise.HighPassed() * 0.8) *
                             Math.Exp(-s.SinceSixteenth * 70.0);
                }
                else
                {
                    sample = noise.HighPassed() * Math.Exp(-s.SinceSixteenth * 260.0) * 0.45;
                }

                output[i] = (float)(sample * 0.35);
            }
        }

        // Chimbais em semicolcheias (acentos nos contratempos), chimbal aberto e palmas
        static void RenderHats(float[] output, Sequencer s)
        {
            var noise = new Noise(37u);
            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                double hiss = noise.HighPassed();
                int step = s.SixteenthIndex % 4;
                double accent = step == 2 ? 1.0 : step == 0 ? 0.45 : 0.7;
                double sample = hiss * Math.Exp(-s.SinceSixteenth * 75.0) * accent * 0.3;

                if (step == 2)
                    sample += hiss * Math.Exp(-s.SinceSixteenth * 11.0) * 0.12;

                // Palma: três estalos de ruído bem juntos no 2 e no 4
                if (s.BeatIndex % 2 == 1)
                {
                    double t = s.SinceBeat;
                    double clap = Math.Exp(-t * 28.0) + (t > 0.011 ? Math.Exp(-(t - 0.011) * 28.0) : 0.0) +
                                  (t > 0.022 ? Math.Exp(-(t - 0.022) * 16.0) : 0.0);
                    sample += hiss * clap * 0.32;
                }

                output[i] = (float)Math.Tanh(sample * 1.1);
            }
        }

        // Acid: serra em semicolcheias pelo filtro ressonante, com acento, glissando e o filtro abrindo no tema
        static void RenderAcid(float[] output, Sequencer s)
        {
            var osc = new Saw();
            var filter = new Filter();
            double frequency = Freq(k_ChordRoots[0] + 12);
            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                int step = s.SixteenthIndex % 16;
                double target = Freq(k_ChordRoots[s.Chord] + 12 + k_AcidNotes[step]);
                // Glissando nas notas marcadas; nas outras, a nota troca na hora
                frequency = k_AcidSlide[step] ? frequency + (target - frequency) * 0.0025 : target;

                bool accent = k_AcidAccent[step];
                // O filtro abre e fecha uma vez ao longo do tema
                double opening = 0.55 - 0.45 * Math.Cos(TwoPi * s.LoopPosition);
                double envelope = Math.Exp(-s.SinceSixteenth * (accent ? 7.0 : 13.0));
                double cutoff = 180.0 + (accent ? 3200.0 : 1800.0) * envelope * opening + 400.0 * opening;
                double wave = filter.LowPass(osc.Next(frequency, s.Rate), cutoff, 0.12, s.Rate);
                double amp = Env(s.SinceSixteenth, 0.002, accent ? 4.0 : 7.0) * (accent ? 1.0 : 0.7);
                output[i] = (float)(Math.Tanh(wave * 2.6) * amp * 0.24);
            }
        }

        // Quebrada sincopada com virada no último tempo de cada 2 compassos
        static void RenderBreaks(float[] output, Sequencer s)
        {
            var noise = new Noise(53u);
            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                int step = s.SixteenthIndex % 32;
                int hit = k_Breaks[step];
                double t = s.SinceSixteenth;
                double sample = 0.0;
                if (hit == 1)
                {
                    sample += Kick(t, ref noise) * 0.5;
                }
                else if (hit >= 2)
                {
                    double gain = hit == 2 ? 1.0 : 0.4;
                    double body = Math.Sin(TwoPi * 210.0 * t) * Math.Exp(-t * 32.0);
                    sample += (body * 0.5 + noise.HighPassed() * 1.5 * Math.Exp(-t * 20.0)) * gain * 0.3;
                }

                // Virada: tons descendo nas últimas 4 semicolcheias
                if (step >= 28)
                {
                    double tom = 260.0 - (step - 28) * 40.0;
                    sample += Math.Sin(TwoPi * tom * t) * Math.Exp(-t * 18.0) * 0.25;
                }

                output[i] = (float)Math.Tanh(sample * 1.3);
            }
        }

        // Lead agudo, sirene, rufada em fusas e subida de ruído a cada 8 compassos
        static void RenderLead(float[] output, Sequencer s)
        {
            var leadA = new Saw();
            var leadB = new Saw();
            var leadFilter = new Filter();
            var riserFilter = new Filter();
            var noise = new Noise(71u);
            double sirenPhase = 0.0;

            for (int i = 0; i < output.Length; i++)
            {
                s.At(i);
                int degree = k_LeadRiff[s.SixteenthIndex % 16];
                int note = degree == 3 ? TriadNote(s.Chord, 48, 0) : TriadNote(s.Chord, 36, degree);
                double vibrato = 1.0 + 0.004 * Math.Sin(TwoPi * 6.0 * s.Time);
                double f = Freq(note) * vibrato;
                double lead = leadA.Next(f * 1.004, s.Rate) + leadB.Next(f * 0.996, s.Rate);
                lead = leadFilter.LowPass(lead, 900.0 + 3500.0 * Math.Exp(-s.SinceSixteenth * 10.0), 0.4, s.Rate);
                double sample = lead * Env(s.SinceSixteenth, 0.002, 5.0) * 0.07;

                // Sirene: varredura de 700 a 1400 Hz duas vezes por compasso
                double sweep = 0.5 + 0.5 * Math.Sin(TwoPi * s.Beat / 2.0);
                sirenPhase += (700.0 + 700.0 * sweep) / s.Rate;
                sample += Math.Sin(sirenPhase * TwoPi) * 0.03;

                // Rufada de chimbal em fusas
                double inThirtySecond = (s.Beat % 0.125) * s.SecondsPerBeat;
                double hiss = noise.HighPassed();
                sample += hiss * Math.Exp(-inThirtySecond * 140.0) * 0.07;

                // Subida de ruído nos 2 últimos compassos de cada 8
                double inEight = s.Beat % (BeatsPerBar * 8);
                if (inEight >= BeatsPerBar * 6)
                {
                    double rise = (inEight - BeatsPerBar * 6) / (BeatsPerBar * 2);
                    sample += riserFilter.LowPass(hiss * 3.0, 300.0 + 6000.0 * rise * rise, 0.7, s.Rate) *
                              rise * rise * 0.12;
                }

                output[i] = (float)Math.Tanh(sample * 1.2);
            }
        }

        // ---------- sons e utilidades ----------

        // Bumbo: seno que desce de 150 para 48 Hz, com um clique de ataque
        static double Kick(double age, ref Noise noise)
        {
            if (age > 0.45)
                return 0.0;
            double phase = TwoPi * (48.0 * age + 102.0 / 30.0 * (1.0 - Math.Exp(-age * 30.0)));
            double body = Math.Sin(phase) * Math.Exp(-age * 7.0) * Math.Min(1.0, age * 600.0);
            double click = noise.HighPassed() * Math.Exp(-age * 350.0) * 0.4;
            return Math.Tanh((body + click) * 1.5);
        }

        // Nota 'voice' (0 fundamental, 1 terça, 2 quinta) da tríade do acorde, deslocada 'octaveOffset' semitons
        static int TriadNote(int chord, int octaveOffset, int voice)
        {
            int root = k_ChordRoots[chord] + octaveOffset;
            return voice == 0 ? root : voice == 1 ? root + (k_ChordMinor[chord] ? 3 : 4) : root + 7;
        }

        static double Freq(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        // Ataque linear curto e decaimento exponencial
        static double Env(double age, double attack, double decay) =>
            Math.Min(1.0, age / attack) * Math.Exp(-age * decay);

        static double Smooth(double from, double to, double x)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (x - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }

        // Posição musical de uma amostra (batida, semicolcheia, tempo desde cada divisão e acorde atual)
        struct Sequencer
        {
            public readonly double SecondsPerBeat;
            public readonly double Rate;
            readonly int m_ThemeBars;

            public double Time;
            public double Beat;
            public int BeatIndex;
            public int EighthIndex;
            public int SixteenthIndex;
            public double SinceBeat;
            public double SinceEighth;
            public double SinceSixteenth;
            public double SinceChord;
            public int Chord;
            public double LoopPosition;

            public Sequencer(double secondsPerBeat, int sampleRate, int themeBars)
            {
                SecondsPerBeat = secondsPerBeat;
                Rate = sampleRate;
                m_ThemeBars = Math.Max(1, themeBars);
                Time = Beat = SinceBeat = SinceEighth = SinceSixteenth = SinceChord = LoopPosition = 0.0;
                BeatIndex = EighthIndex = SixteenthIndex = Chord = 0;
            }

            public void At(int sample)
            {
                Time = sample / Rate;
                Beat = Time / SecondsPerBeat;
                BeatIndex = (int)Math.Floor(Beat);
                EighthIndex = (int)Math.Floor(Beat * 2.0);
                SixteenthIndex = (int)Math.Floor(Beat * 4.0);
                SinceBeat = (Beat - BeatIndex) * SecondsPerBeat;
                SinceEighth = (Beat * 2.0 - EighthIndex) * 0.5 * SecondsPerBeat;
                SinceSixteenth = (Beat * 4.0 - SixteenthIndex) * 0.25 * SecondsPerBeat;

                double themeBeats = m_ThemeBars * BeatsPerBar;
                double loopBeat = Beat % themeBeats;
                LoopPosition = loopBeat / themeBeats;
                double chordBeats = k_ChordBars * BeatsPerBar;
                Chord = (int)Math.Floor(loopBeat / chordBeats) % k_ChordRoots.Length;
                SinceChord = (loopBeat % chordBeats) * SecondsPerBeat;
            }

            // Bombeamento: o volume cai logo depois de cada bumbo e volta ao longo do tempo
            public double Sidechain(double depth, double recovery) =>
                1.0 - depth * Math.Exp(-SinceBeat * recovery) * Math.Min(1.0, SinceBeat * 200.0 + 0.5);
        }

        // Serra com polyBLEP (sem serrilhado audível)
        sealed class Saw
        {
            double m_Phase;

            public double Next(double frequency, double rate)
            {
                double dt = frequency / rate;
                double value = 2.0 * m_Phase - 1.0 - PolyBlep(m_Phase, dt);
                m_Phase += dt;
                if (m_Phase >= 1.0)
                    m_Phase -= 1.0;
                return value;
            }

            static double PolyBlep(double t, double dt)
            {
                if (t < dt)
                {
                    t /= dt;
                    return t + t - t * t - 1.0;
                }
                if (t > 1.0 - dt)
                {
                    t = (t - 1.0) / dt;
                    return t * t + t + t + 1.0;
                }
                return 0.0;
            }
        }

        // Filtro de estado variável (Chamberlin) com 2x de sobreamostragem; damping baixo = mais ressonância
        sealed class Filter
        {
            double m_Low;
            double m_Band;

            public double LowPass(double input, double cutoff, double damping, double rate)
            {
                double fc = Math.Min(cutoff, rate * 0.24);
                double f = 2.0 * Math.Sin(Math.PI * fc / (rate * 2.0));
                for (int pass = 0; pass < 2; pass++)
                {
                    m_Low += f * m_Band;
                    double high = input - m_Low - damping * m_Band;
                    m_Band += f * high;
                }

                return m_Low;
            }
        }

        // Ruído branco (xorshift) com passa-alta simples; uma instância por faixa
        struct Noise
        {
            uint m_State;
            float m_Previous;

            public Noise(uint seed)
            {
                m_State = seed;
                m_Previous = 0f;
            }

            public float HighPassed()
            {
                m_State ^= m_State << 13;
                m_State ^= m_State >> 17;
                m_State ^= m_State << 5;
                float white = (m_State & 0xFFFFFF) / 8388608f - 1f;
                float high = white - m_Previous;
                m_Previous = white;
                return high * 0.5f;
            }
        }
    }
}
