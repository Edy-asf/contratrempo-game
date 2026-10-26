using System;
using static Icaro.PixelCanvas;

namespace Icaro
{
    // Oráculo (o chefe das Ruínas, no lugar do Guardião Tiki): figura alta de pedra cinza-azulada e luz, sem pernas,
    // com o corpo terminando numa ponta de placas soltas, uma máscara alta com uma fenda ciano e um halo de runas atrás
    // da cabeça; quatro braços seguram anéis de runa (os de cima maiores) que viram os feixes. Pedaços de pedra
    // orbitam em volta. No aviso ergue os anéis, que crescem e acendem; no ataque os anéis faíscam e soltam raios; ao
    // morrer, as placas se soltam, os anéis caem e a luz apaga. Flutua (o BossAI levanta o visual). Sempre de frente.
    public sealed class OraculoArt : EnemyArt
    {
        public override string Key => "Chefe_Tribal";
        public override int Width => 164;
        public override int Height => 176;
        public override bool SameAllDirections => true;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 4f, true),
            new EnemyClipSpec("Move", 4, 5f, true),
            new EnemyClipSpec("Windup", 3, 6f, false),
            new EnemyClipSpec("Attack", 4, 10f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 7f, false),
        };

        struct Pose
        {
            public float Bob, Spin, Raise, Glow, Flare, Lean, Fall;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose { Glow = 0.5f, Spin = t };
            switch (clip)
            {
                case "Idle":
                case "Move":
                    p.Bob = Sin01(t) * 1.5f;
                    break;
                case "Windup":
                    p.Raise = Ease(t);
                    p.Glow = 0.5f + 0.5f * t;
                    p.Spin = t * 0.5f;
                    break;
                case "Attack":
                    p.Raise = 1f;
                    p.Glow = 1f;
                    p.Flare = frame % 2 == 0 ? 1f : 0.6f;
                    break;
                case "Hit":
                    p.Lean = frame == 0 ? -5f : -2f;
                    p.Glow = frame == 0 ? 0.2f : 0.6f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    p.Glow = 0.5f * (1f - t);
                    p.Spin = 0f;
                    break;
            }

            Fragments(p);
            Push(0f, p.Bob, p.Lean, 0f, -60f);
            Halo(p);
            Robe(p);
            Torso(p);
            for (int s = -1; s <= 1; s += 2)
            {
                LowerArm(s, p);
                UpperArm(s, p);
            }
            Head(p);
            Pop();
        }

        // ---------- partes ----------

        // Corpo em ponta, de placas de pedra empilhadas, com a costura de luz no meio; ao morrer as placas caem
        void Robe(Pose p)
        {
            float[] bands = { -72f, -58f, -44f, -30f, -16f };
            for (int i = 0; i < bands.Length - 1; i++)
            {
                float y0 = bands[i], y1 = bands[i + 1];
                float w0 = 22f - i * 5f, w1 = 22f - (i + 1) * 5f;
                float drop = p.Fall * (30f + i * 18f);
                float side = (i % 2 == 0 ? -1f : 1f) * p.Fall * (6f + i * 3f);
                Push(side, drop, (i % 2 == 0 ? -1f : 1f) * p.Fall * 25f, 0f, (y0 + y1) * 0.5f);
                Poly(EnemyRamps.Stone, -0.05f - i * 0.04f, -w0, y0 + 1f, w0, y0 + 1f, w1, y1, -w1, y1);
                Line(-w0 + 1f, y0 + 1f, w0 - 1f, y0 + 1f, EnemyRamps.Stone[1]);
                EmitLine(0f, y0 + 2f, 0f, y1 - 1f, p.Glow > 0.3f ? EnemyRamps.Bio[2] : EnemyRamps.Bio[0]);
                Pop();
            }

            // Ponta
            Push(0f, p.Fall * 110f, p.Fall * 40f, 0f, -10f);
            Poly(EnemyRamps.Stone, -0.25f, -2f, -16f, 2f, -16f, 0f, -4f);
            Pop();
        }

        void Torso(Pose p)
        {
            Push(0f, p.Fall * 95f, p.Fall * 10f, 0f, -92f);
            Poly(EnemyRamps.Robe, 0.05f, -31f, -114f, 31f, -114f, 23f, -72f, -23f, -72f);
            Poly(EnemyRamps.Stone, 0.1f, -27f, -114f, 27f, -114f, 18f, -96f, -18f, -96f);
            Line(-22f, -100f, 22f, -100f, EnemyRamps.Stone[1]);
            // Glifo no peito
            EmitPoly(EnemyRamps.Bio, p.Glow - 0.4f, 0f, -104f, 6f, -94f, 0f, -84f, -6f, -94f);
            Ell(0f, -94f, 2f, 3f, EnemyRamps.Robe, -0.5f);
            Light(0f, -94f, 10f, EnemyRamps.CyanLight, p.Glow * 0.5f);
            Pop();
        }

        // Máscara alta com a fenda acesa
        void Head(Pose p)
        {
            Push(p.Fall * 6f, p.Fall * 115f, p.Fall * 35f, 0f, -128f);
            Cap(0f, -114f, 0f, -118f, 5f, EnemyRamps.Stone, -0.2f);
            Plate(-10f, -144f, 20f, 26f, EnemyRamps.Stone, 0.12f, 2.5f);
            Poly(EnemyRamps.Stone, 0.2f, -10f, -144f, 0f, -150f, 10f, -144f);
            Line(-10f, -131f, 10f, -131f, EnemyRamps.Stone[1]);
            EmitLine(0f, -142f, 0f, -124f, p.Glow > 0.2f ? EnemyRamps.CyanLight : EnemyRamps.Bio[0], 1.6f);
            EmitDot(-5f, -136f, EnemyRamps.Bio[3]);
            EmitDot(5f, -136f, EnemyRamps.Bio[3]);
            Pop();
        }

        // Halo de runas atrás da cabeça, girando (as runas são os pontos claros)
        void Halo(Pose p)
        {
            if (p.Fall > 0.6f)
                return;
            Push(0f, p.Fall * 40f, 0f, 0f, 0f);
            EmitShape(Ring(0f, -134f, 23f, 20f), EnemyRamps.Bio, p.Glow - 0.55f);
            Runes(0f, -134f, 21.5f, 10, p.Spin * 36f, p);
            Pop();
        }

        // Braço de cima: sobe do ombro e segura o anel grande acima da cabeça
        void UpperArm(int s, Pose p)
        {
            float raise = p.Raise * 14f;
            float hx = s * (50f + p.Raise * 4f), hy = -134f - raise;
            Push(-s * p.Fall * 6f, p.Fall * 110f, s * p.Fall * 30f, s * 30f, -110f);
            Cap(s * 29f, -110f, s * 41f, -122f - raise * 0.5f, 4.5f, EnemyRamps.Robe, 0.1f);
            Cap(s * 41f, -122f - raise * 0.5f, hx, hy + 4f, 4f, EnemyRamps.Stone, 0.05f);
            RuneRing(hx, hy - 6f, 13f + p.Raise * 2f + p.Flare * 1.5f, 2.6f, p, s);
            Ell(hx, hy + 4f, 4f, 3.5f, EnemyRamps.Stone, 0.15f);
            Pop();
        }

        // Braço de baixo: estendido para o lado com o anel menor
        void LowerArm(int s, Pose p)
        {
            float raise = p.Raise * 10f;
            float hx = s * 48f, hy = -80f - raise;
            Push(-s * p.Fall * 4f, p.Fall * 90f, s * p.Fall * 25f, s * 24f, -92f);
            Cap(s * 22f, -92f, s * 36f, -86f - raise * 0.5f, 4f, EnemyRamps.Robe, 0f);
            Cap(s * 36f, -86f - raise * 0.5f, hx, hy, 3.5f, EnemyRamps.Stone, 0f);
            RuneRing(hx + s * 5f, hy, 9f + p.Flare, 2f, p, -s);
            Ell(hx, hy, 3.5f, 3f, EnemyRamps.Stone, 0.1f);
            Pop();
        }

        // Anel de runa aceso; no ataque solta raios para fora
        void RuneRing(float x, float y, float r, float thick, Pose p, int dir)
        {
            EmitShape(Ring(x, y, r, r - thick), EnemyRamps.Bio, p.Glow - 0.45f + p.Flare * 0.3f);
            Runes(x, y, r - thick * 0.5f, 6, p.Spin * 60f * dir, p);
            if (p.Flare > 0f)
            {
                for (int i = 0; i < 4; i++)
                {
                    double a = (i * 90.0 + 45.0 + p.Spin * 90.0) * Math.PI / 180.0;
                    float cx = (float)Math.Cos(a), cy = (float)Math.Sin(a);
                    EmitLine(x + cx * (r + 1f), y + cy * (r + 1f), x + cx * (r + 4f + p.Flare * 4f),
                        y + cy * (r + 4f + p.Flare * 4f), EnemyRamps.Bio[4]);
                }
                Light(x, y, r + 2f, EnemyRamps.CyanLight, 0.4f * p.Flare);
            }
        }

        // Runas: pontos claros espaçados no anel, girando
        void Runes(float x, float y, float r, int count, float angle, Pose p)
        {
            for (int i = 0; i < count; i++)
            {
                double a = (angle + i * 360.0 / count) * Math.PI / 180.0;
                EmitDot(x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r,
                    p.Glow > 0.3f ? PixelRamps.White : EnemyRamps.Bio[2]);
            }
        }

        // Pedaços de pedra orbitando a parte de baixo
        void Fragments(Pose p)
        {
            for (int i = 0; i < 5; i++)
            {
                double a = (p.Spin * 360.0 / 5.0 + i * 72.0) * Math.PI / 180.0;
                float x = (float)Math.Cos(a) * 34f;
                float y = -46f + (float)Math.Sin(a) * 9f + p.Bob + p.Fall * (40f + i * 4f);
                float size = 3f + (i % 3);
                Plate(x - size * 0.5f, y - size * 0.5f, size, size * 0.8f, EnemyRamps.Stone, Math.Sin(a) < 0 ? -0.3f : 0f, 0.8f);
                if (i % 2 == 0 && p.Glow > 0.2f)
                    EmitDot(x, y, EnemyRamps.Bio[2]);
            }
        }
    }
}
