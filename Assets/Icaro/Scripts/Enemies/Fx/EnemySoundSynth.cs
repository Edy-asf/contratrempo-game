namespace Icaro
{
    // Síntese dos sons dos ataques dos inimigos (sem UnityEngine: o EnemySounds converte em AudioClip e a prévia fora
    // do Unity grava os mesmos sons em WAV). Sabor de 8 e 16 bits (onda quadrada, bits reduzidos, amostra segurada a
    // 22 kHz) com corpo grave. Cada som devolve as amostras mono a 44,1 kHz, normalizadas.
    public static class EnemySoundSynth
    {
        public const int Rate = 44100;
        const int k_Rate = Rate;

        // Todos os sons, pelo nome do clipe (para a prévia)
        public static readonly string[] Names =
        {
            "PlasmaCarga", "PlasmaTiro", "PlasmaImpacto", "MariposaCarga", "MariposaTiro", "MariposaImpacto",
            "MorteiroTiro", "MorteiroAssobio", "MorteiroExplosao", "MusgoRugido", "MusgoRosnado", "MusgoPouso",
            "MusgoGarra", "MusgoArranca", "MusgoTorrao", "MusgoNuvem", "MusgoAtordoado",
        };

        public static float[] ByName(string name)
        {
            switch (name)
            {
                case "PlasmaCarga": return PlasmaCharge();
                case "PlasmaTiro": return PlasmaShot();
                case "PlasmaImpacto": return PlasmaHit();
                case "MariposaCarga": return MothCharge();
                case "MariposaTiro": return MothShot();
                case "MariposaImpacto": return MothHit();
                case "MorteiroTiro": return MortarLaunch();
                case "MorteiroAssobio": return MortarWhistle();
                case "MorteiroExplosao": return MortarBlast();
                case "MusgoRugido": return MossRoar();
                case "MusgoRosnado": return MossGrowl();
                case "MusgoPouso": return MossLand();
                case "MusgoGarra": return MossSwipe();
                case "MusgoArranca": return MossDig();
                case "MusgoTorrao": return ClodHit();
                case "MusgoNuvem": return SporeCloud();
                case "MusgoAtordoado": return MossStun();
                default: return new float[0];
            }
        }

        // Plasma do Explorador: zumbido subindo com o cano carregando (0,3 s; o tom acompanha a duração do aviso)
        public static float[] PlasmaCharge() => Render("PlasmaCarga", 0.3f, (t, d, s) =>
        {
            float u = t / d;
            float f = 150f + 760f * u * u;
            float saw = Saw(s.Phase(0, f)) + Saw(s.Phase(1, f * 1.012f));
            float lp = s.Low(0, saw * 0.5f, 0.05f + 0.4f * u);
            float tremolo = 0.7f + 0.3f * SynthMath.Sin(2f * SynthMath.PI * (10f + 50f * u) * t);
            float suck = s.Band(0, s.Noise(), 600f + 2400f * u, 0.9f) * u * u * 0.8f;
            float sub = SynthMath.Sin(s.Phase(2, f * 0.5f) * 2f * SynthMath.PI) * 0.35f;
            return (lp * tremolo + sub) * SynthMath.Pow(u, 1.3f) * 0.6f + suck;
        });

        // Disparo do plasma: estalo, zap descendo em onda quadrada de bits reduzidos e um corpo grave
        public static float[] PlasmaShot() => Render("PlasmaTiro", 0.36f, (t, d, s) =>
        {
            float f = 110f + 920f * SynthMath.Exp(-t * 26f);
            float zap = Crush(Square(s.Phase(0, f), 0.3f), 10f) * SynthMath.Exp(-t * 13f) * 0.38f;
            float pew = SynthMath.Sin(s.Phase(1, f * 2.02f) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 32f) * 0.22f;
            float body = SynthMath.Sin(s.Phase(2, 52f + 90f * SynthMath.Exp(-t * 28f)) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 9f) * 0.7f;
            float crack = s.Low(0, s.Noise(), SynthMath.Lerp(0.65f, 0.04f, SynthMath.Clamp01(t / 0.12f))) * SynthMath.Exp(-t * 24f) * 0.65f;
            float click = t < 0.003f ? s.Noise() * 0.6f : 0f;
            return Drive(zap + pew + body + crack + click, 1.5f);
        });

        // Impacto do plasma: chiado crepitando e uma pancada grave curta
        public static float[] PlasmaHit() => Render("PlasmaImpacto", 0.3f, (t, d, s) =>
        {
            float density = SynthMath.Exp(-t * 10f);
            float crackle = s.Random() < density * 0.25f ? s.Noise() * 0.9f : 0f;
            crackle = s.Low(0, crackle, 0.5f) * 1.6f;
            float thump = SynthMath.Sin(s.Phase(0, 60f + 110f * SynthMath.Exp(-t * 30f)) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 22f) * 0.7f;
            float blip = t < 0.03f ? Crush(Square(s.Phase(1, 620f), 0.5f), 6f) * (1f - t / 0.03f) * 0.25f : 0f;
            float hiss = s.Band(0, s.Noise(), 3200f, 0.7f) * SynthMath.Exp(-t * 14f) * 0.35f;
            return Drive(crackle + thump + blip + hiss, 1.3f);
        });

        // Mariposa-lanterna: chilro subindo com o zumbido das asas (0,22 s)
        public static float[] MothCharge() => Render("MariposaCarga", 0.22f, (t, d, s) =>
        {
            float u = t / d;
            float flutter = SynthMath.Sin(2f * SynthMath.PI * 46f * t);
            float chirp = SynthMath.Sin(s.Phase(0, 700f + 1300f * u + 120f * flutter) * 2f * SynthMath.PI);
            float hum = Crush(Square(s.Phase(1, 96f), 0.5f), 4f) * (0.6f + 0.4f * flutter);
            return (chirp * 0.35f + hum * 0.18f) * u;
        });

        // Disparo da mariposa: "tsip" de triângulo descendo, com um tique e um grave leve
        public static float[] MothShot() => Render("MariposaTiro", 0.17f, (t, d, s) =>
        {
            float f = 340f + 1500f * SynthMath.Exp(-t * 20f);
            float tri = Crush(Tri(s.Phase(0, f)), 8f) * SynthMath.Exp(-t * 19f) * 0.42f;
            float tick = t < 0.004f ? s.Noise() * 0.5f : 0f;
            float sub = SynthMath.Sin(s.Phase(1, 170f) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 30f) * 0.35f;
            float air = s.Band(0, s.Noise(), 4500f, 0.6f) * SynthMath.Exp(-t * 40f) * 0.25f;
            return Drive(tri + tick + sub + air, 1.2f);
        });

        // Impacto da mariposa: estalinho com chiado
        public static float[] MothHit() => Render("MariposaImpacto", 0.15f, (t, d, s) =>
        {
            float pop = SynthMath.Sin(s.Phase(0, 150f + 380f * SynthMath.Exp(-t * 45f)) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 38f) * 0.55f;
            float fizz = s.Low(0, s.Noise(), 0.35f) * SynthMath.Exp(-t * 28f) * 0.4f;
            return Drive(Crush(pop + fizz, 16f), 1.2f);
        });

        // Morteiro de esporos: baque oco de tubo e um "blop" molhado
        public static float[] MortarLaunch() => Render("MorteiroTiro", 0.5f, (t, d, s) =>
        {
            float thunk = SynthMath.Sin(s.Phase(0, 44f + 60f * SynthMath.Exp(-t * 14f)) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 9f) * 0.85f;
            float tube = s.Band(0, s.Noise(), 330f - 140f * SynthMath.Clamp01(t / 0.3f), 0.985f) * SynthMath.Exp(-t * 10f) * 0.9f;
            float blop = t < 0.06f ? Crush(Square(s.Phase(1, 230f - 900f * t), 0.5f), 6f) * (1f - t / 0.06f) * 0.3f : 0f;
            float puff = s.Low(0, s.Noise(), 0.12f) * SynthMath.Exp(-t * 16f) * 0.5f;
            return Drive(thunk + tube + blop + puff, 1.4f);
        });

        // Assobio da bola caindo (1,2 s, desce de tom; o tom acompanha o tempo de voo)
        public static float[] MortarWhistle() => Render("MorteiroAssobio", 1.2f, (t, d, s) =>
        {
            float u = t / d;
            float f = 1500f - 900f * u;
            float tone = SynthMath.Sin(s.Phase(0, f + 18f * SynthMath.Sin(2f * SynthMath.PI * 7f * t)) * 2f * SynthMath.PI);
            float breath = s.Band(0, s.Noise(), f, 0.95f) * 1.2f;
            float env = SynthMath.Clamp01(u * 4f) * (0.4f + 0.6f * u);
            return (tone * 0.22f + breath * 0.08f) * env;
        });

        // Explosão de esporos: estrondo grave com ruído granulado e crepitar no fim
        public static float[] MortarBlast() => Render("MorteiroExplosao", 1.15f, (t, d, s) =>
        {
            float attack = SynthMath.Clamp01(t / 0.004f);
            float boom = s.Low(0, Crush(s.Noise(), 24f), SynthMath.Lerp(0.4f, 0.02f, SynthMath.Clamp01(t / 0.5f))) *
                         SynthMath.Exp(-t * 4.2f) * 1.5f;
            float sub = SynthMath.Sin(s.Phase(0, 30f + 45f * SynthMath.Exp(-t * 7f)) * 2f * SynthMath.PI) * SynthMath.Exp(-t * 4.5f) * 0.95f;
            float crackle = t > 0.12f && s.Random() < 0.05f * SynthMath.Exp(-(t - 0.12f) * 3f) ? s.Noise() * 0.7f : 0f;
            float snap = t < 0.02f ? s.Noise() * (1f - t / 0.02f) * 0.8f : 0f;
            return Drive((boom + sub + crackle + snap) * attack, 1.6f);
        });

        // ---------- Pé-de-Musgo (chefe do Bosque) ----------

        // Rugido de esporos (1,6 s): garganta grave com vibrato, chiado de folhas subindo e um sopro de esporos no fim
        public static float[] MossRoar() => Render("MusgoRugido", 1.6f, (t, d, s) =>
        {
            float u = t / d;
            float env = SynthMath.Clamp01(t / 0.12f) * SynthMath.Clamp01((d - t) / 0.5f);
            float f = 62f + 26f * SynthMath.Sin(SynthMath.PI * SynthMath.Clamp01(u * 1.3f)) +
                      4f * SynthMath.Sin(2f * SynthMath.PI * 7f * t);
            float throat = Saw(s.Phase(0, f)) * 0.6f + Saw(s.Phase(1, f * 1.5f)) * 0.25f +
                           Square(s.Phase(2, f * 0.5f), 0.4f) * 0.3f;
            float body = s.Low(0, throat, 0.08f + 0.05f * SynthMath.Sin(2f * SynthMath.PI * 11f * t));
            float rasp = s.Band(0, s.Noise(), 420f + 300f * u, 0.93f) *
                         (0.6f + 0.4f * SynthMath.Sin(2f * SynthMath.PI * 23f * t));
            float leaves = s.Band(1, s.Noise(), 3200f, 0.7f) * SynthMath.Clamp01((u - 0.5f) * 3f) * 0.35f;
            return Drive((body * 1.5f + rasp * 1.2f + leaves) * env, 1.8f);
        });

        // Rosnado baixo da caçada (1 s): aviso do bote, de onde ele está escondido
        public static float[] MossGrowl() => Render("MusgoRosnado", 1.0f, (t, d, s) =>
        {
            float u = t / d;
            float env = SynthMath.Clamp01(t / 0.25f) * (0.6f + 0.4f * u) * SynthMath.Clamp01((d - t) / 0.08f);
            float pulse = 0.55f + 0.45f * SynthMath.Sin(2f * SynthMath.PI * (13f + 9f * u) * t);
            float f = 48f + 14f * u;
            float throat = s.Low(0, Saw(s.Phase(0, f)) + Saw(s.Phase(1, f * 1.01f)), 0.05f);
            float rasp = s.Band(0, s.Noise(), 260f, 0.95f) * 1.4f;
            return Drive((throat * 1.2f + rasp) * pulse * env, 2f);
        });

        // Pouso do salto (1 s): baque enorme de terra, estalo de galhos e terra caindo
        public static float[] MossLand() => Render("MusgoPouso", 1.0f, (t, d, s) =>
        {
            float sub = SynthMath.Sin(s.Phase(0, 28f + 70f * SynthMath.Exp(-t * 9f)) * 2f * SynthMath.PI) *
                        SynthMath.Exp(-t * 4f) * 1.2f;
            float thud = s.Low(0, Crush(s.Noise(), 20f), SynthMath.Lerp(0.3f, 0.02f, SynthMath.Clamp01(t / 0.3f))) *
                         SynthMath.Exp(-t * 6f) * 1.6f;
            float snap = t < 0.03f ? s.Noise() * (1f - t / 0.03f) : 0f;
            float dirt = t > 0.08f && s.Random() < 0.035f * SynthMath.Exp(-(t - 0.08f) * 3.5f) ? s.Noise() * 0.5f : 0f;
            return Drive(sub + thud + snap + dirt, 1.7f);
        });

        // Garra passando (0,4 s): rasgo de ar descendo e um raspar de osso
        public static float[] MossSwipe() => Render("MusgoGarra", 0.4f, (t, d, s) =>
        {
            float u = t / d;
            float env = SynthMath.Sin(SynthMath.PI * SynthMath.Clamp01(u * 1.2f));
            float whoosh = s.Band(0, s.Noise(), 1800f - 1300f * u, 0.88f) * 1.6f;
            float bone = Crush(Square(s.Phase(0, 340f - 200f * u), 0.3f), 5f) * SynthMath.Clamp01((u - 0.35f) * 6f) *
                         SynthMath.Exp(-(t - 0.14f) * 18f) * 0.25f;
            return (whoosh + bone) * env;
        });

        // Mão arrancando o torrão do chão (0,6 s): terra rasgando e raízes partindo
        public static float[] MossDig() => Render("MusgoArranca", 0.6f, (t, d, s) =>
        {
            float u = t / d;
            float tear = s.Low(0, s.Noise(), 0.25f) * (0.4f + 0.6f * SynthMath.Sin(SynthMath.PI * u)) * 1.2f;
            float roots = s.Random() < 0.025f ? s.Noise() * 0.9f : 0f;
            float body = SynthMath.Sin(s.Phase(0, 70f - 30f * u) * 2f * SynthMath.PI) * 0.4f * SynthMath.Sin(SynthMath.PI * u);
            return Drive(tear + roots + body, 1.4f);
        });

        // Torrão caindo (0,8 s): baque de terra molhada e pedaços espalhando
        public static float[] ClodHit() => Render("MusgoTorrao", 0.8f, (t, d, s) =>
        {
            float sub = SynthMath.Sin(s.Phase(0, 40f + 60f * SynthMath.Exp(-t * 12f)) * 2f * SynthMath.PI) *
                        SynthMath.Exp(-t * 6f);
            float splat = s.Low(0, s.Noise(), 0.2f) * SynthMath.Exp(-t * 9f) * 1.4f;
            float bits = t > 0.05f && s.Random() < 0.04f * SynthMath.Exp(-(t - 0.05f) * 5f) ? s.Noise() * 0.6f : 0f;
            return Drive(sub + splat + bits, 1.5f);
        });

        // Nuvem de esporos se abrindo (1,2 s): sopro chiado e estalinhos
        public static float[] SporeCloud() => Render("MusgoNuvem", 1.2f, (t, d, s) =>
        {
            float u = t / d;
            float env = SynthMath.Clamp01(t / 0.05f) * SynthMath.Exp(-t * 2.4f);
            float hiss = s.Band(0, s.Noise(), 2400f - 1200f * u, 0.8f) * 1.3f + s.Low(0, s.Noise(), 0.1f) * 0.6f;
            float pops = s.Random() < 0.012f ? s.Noise() * 0.6f : 0f;
            return (hiss + pops) * env;
        });

        // Cogumelos do ombro estourando (atordoado, 0,7 s): estalos ocos e um gemido grave caindo
        public static float[] MossStun() => Render("MusgoAtordoado", 0.7f, (t, d, s) =>
        {
            float pop = 0f;
            for (int i = 0; i < 3; i++)
            {
                float k = t - i * 0.075f;
                if (k >= 0f && k < 0.05f)
                    pop += SynthMath.Sin(s.Phase(i, 520f - 3000f * k) * 2f * SynthMath.PI) * (1f - k / 0.05f) * 0.6f;
            }

            float moan = Saw(s.Phase(3, 90f - 40f * t / d)) * SynthMath.Clamp01((t - 0.1f) * 5f) * SynthMath.Exp(-t * 2f);
            return Drive(pop + s.Low(0, moan, 0.06f) * 1.2f, 1.4f);
        });

        delegate float Voice(float t, float duration, Synth s);

        static float[] Render(string name, float duration, Voice voice)
        {
            int count = (int)System.Math.Ceiling(duration * k_Rate);
            var samples = new float[count];
            var synth = new Synth(Seed(name));
            float peak = 0.0001f;
            float held = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)k_Rate;
                float value = voice(t, duration, synth);
                // Amostra segurada a 22 kHz (aspereza de 16 bits)
                if ((i & 1) == 0)
                    held = value;
                samples[i] = held;
                peak = System.Math.Max(peak, System.Math.Abs(held));
            }

            // Normaliza e tira o clique do fim
            float gain = 0.9f / peak;
            int fade = System.Math.Min(count / 4, k_Rate / 200);
            for (int i = 0; i < count; i++)
            {
                float edge = System.Math.Min(1f, (count - 1 - i) / (float)System.Math.Max(1, fade));
                samples[i] *= gain * edge;
            }

            return samples;
        }

        // Semente estável pelo nome (o GetHashCode de string muda entre execuções)
        static int Seed(string name)
        {
            int h = 17;
            foreach (char c in name)
                h = h * 31 + c;
            return h;
        }

        static float Saw(float phase) => 2f * (phase - SynthMath.Floor(phase)) - 1f;

        static float Tri(float phase)
        {
            float p = phase - SynthMath.Floor(phase);
            return p < 0.5f ? 4f * p - 1f : 3f - 4f * p;
        }

        static float Square(float phase, float duty) => phase - SynthMath.Floor(phase) < duty ? 1f : -1f;

        static float Crush(float value, float levels) => SynthMath.Round(value * levels) / levels;

        static float Drive(float value, float amount) => (float)System.Math.Tanh(value * amount);

        // Estado da síntese: fases dos osciladores, filtros e ruído, um por voz
        sealed class Synth
        {
            readonly float[] m_Phase = new float[4];
            readonly float[] m_Low = new float[4];
            readonly float[] m_B1 = new float[4];
            readonly float[] m_B2 = new float[4];
            readonly System.Random m_Random;

            public Synth(int seed) => m_Random = new System.Random(seed);

            // Fase acumulada (em voltas) do oscilador i na frequência f
            public float Phase(int i, float frequency)
            {
                m_Phase[i] += frequency / k_Rate;
                if (m_Phase[i] > 1000f)
                    m_Phase[i] -= 1000f;
                return m_Phase[i];
            }

            public float Noise() => (float)(m_Random.NextDouble() * 2.0 - 1.0);

            public float Random() => (float)m_Random.NextDouble();

            // Passa-baixa de um polo (amount 0..1 por amostra)
            public float Low(int i, float input, float amount)
            {
                m_Low[i] += (input - m_Low[i]) * SynthMath.Clamp01(amount);
                return m_Low[i];
            }

            // Ressonador de dois polos em "frequency" (r perto de 1 = mais estreito)
            public float Band(int i, float input, float frequency, float r)
            {
                float w = 2f * SynthMath.PI * frequency / k_Rate;
                float y = 2f * r * SynthMath.Cos(w) * m_B1[i] - r * r * m_B2[i] + input * (1f - r);
                m_B2[i] = m_B1[i];
                m_B1[i] = y;
                return y;
            }
        }
    }

    // Matemática em float sem UnityEngine
    static class SynthMath
    {
        public const float PI = (float)System.Math.PI;
        public static float Sin(float x) => (float)System.Math.Sin(x);
        public static float Cos(float x) => (float)System.Math.Cos(x);
        public static float Exp(float x) => (float)System.Math.Exp(x);
        public static float Pow(float x, float y) => (float)System.Math.Pow(x, y);
        public static float Floor(float x) => (float)System.Math.Floor(x);
        public static float Round(float x) => (float)System.Math.Round(x);
        public static float Abs(float x) => System.Math.Abs(x);
        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
