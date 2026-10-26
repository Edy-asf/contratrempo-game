using System;

namespace Icaro
{
    // Sapo Rei do Brejo (provisório, até o chefe do Arquipélago ganhar o visual próprio): sapo colossal sentado, verde
    // escuro com verrugas, barriga clara, olhos saltados com a pupila âmbar em fenda, papo que infla, juncos e uma
    // folha de aguapé na cabeça como coroa. Anda aos saltos; no aviso se agacha e o papo incha; no ataque abre a boca
    // com o ácido verde aceso e cospe gotas; ao morrer, achata de barriga para cima. Sempre de frente.
    public sealed class SapoReiArt : EnemyArt
    {
        public override string Key => "Chefe_Sapo";
        public override int Width => 184;
        public override int Height => 150;
        public override bool SameAllDirections => true;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 4, 6f, true),
            new EnemyClipSpec("Windup", 3, 6f, false),
            new EnemyClipSpec("Attack", 4, 10f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 7f, false),
        };

        struct Pose
        {
            public float Rise, Squash, Sac, Mouth, Spit, Legs, Fall, Lean;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose();
            switch (clip)
            {
                case "Idle":
                    p.Sac = 0.25f + 0.25f * (Sin01(t) * 0.5f + 0.5f);
                    break;
                case "Move":
                    // Salto: agacha, estica no ar, cai e achata
                    p.Squash = frame == 0 ? 0.25f : frame == 3 ? 0.35f : -0.1f;
                    p.Rise = frame == 1 ? 14f : frame == 2 ? 20f : 0f;
                    p.Legs = frame == 1 || frame == 2 ? 1f : 0f;
                    break;
                case "Windup":
                    p.Squash = 0.3f * Ease(t);
                    p.Sac = 0.5f + 0.5f * t;
                    p.Mouth = 0.2f * t;
                    break;
                case "Attack":
                    p.Mouth = frame % 2 == 0 ? 1f : 0.75f;
                    p.Spit = t;
                    p.Lean = 3f;
                    break;
                case "Hit":
                    p.Squash = frame == 0 ? 0.2f : 0.05f;
                    p.Lean = frame == 0 ? -4f : -1f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    break;
            }

            // Achatado (squash > 0) ou esticado (< 0) em volta do chão
            float sy = 1f - p.Squash - p.Fall * 0.55f, sx = 1f + p.Squash * 0.6f + p.Fall * 0.25f;
            if (p.Rise > 0f)
                Shadow();
            Push(0f, -p.Rise, p.Lean, 0f, -40f);
            Frog(p, sx, sy);
            Pop();
        }

        void Shadow() => Ell(0f, -1.5f, 40f, 3f, EnemyRamps.Robe, -0.6f);

        void Frog(Pose p, float sx, float sy)
        {
            // Pernas de trás dobradas dos lados (esticadas no ar)
            for (int s = -1; s <= 1; s += 2)
            {
                if (p.Legs > 0f)
                {
                    Cap(s * 40f * sx, -24f * sy, s * 56f, -2f, 11f, EnemyRamps.Frog, -0.15f);
                    Foot(s * 62f, -2f, s);
                }
                else
                {
                    Ell(s * 50f * sx, -24f * sy, 23f, 17f * sy, EnemyRamps.Frog, -0.1f);
                    Foot(s * 66f * sx, -3f, s);
                }
            }

            // Corpo e barriga
            Ell(0f, -50f * sy, 54f * sx, 44f * sy, EnemyRamps.Frog);
            Ell(0f, -40f * sy, 36f * sx, 31f * sy, EnemyRamps.Belly, -0.05f);

            // Braços da frente
            for (int s = -1; s <= 1; s += 2)
            {
                Cap(s * 32f * sx, -44f * sy, s * 36f * sx, -7f, 6.5f, EnemyRamps.Frog, 0.05f);
                Ell(s * 37f * sx, -4f, 9f, 3.5f, EnemyRamps.Frog, -0.05f);
                for (int i = -1; i <= 1; i++)
                    Dot(s * 37f * sx + i * 4f, -1.5f, EnemyRamps.Frog[0]);
            }

            // Cabeça larga, papo e boca
            float headY = -80f * sy;
            Ell(0f, headY, 52f * sx, 27f * sy, EnemyRamps.Frog, 0.05f);
            float sac = p.Sac;
            if (p.Mouth < 0.3f)
                Ell(0f, headY + 22f * sy, 17f + sac * 8f, 10f + sac * 7f, EnemyRamps.Belly, 0.15f);
            Mouth(headY, p, sx, sy);

            // Verrugas
            float[] warts = { -36f, -6f, -20f, -14f, 30f, -10f, 42f, 2f, -46f, 6f, 14f, -20f, -10f, 8f };
            for (int i = 0; i < warts.Length; i += 2)
                Dot(warts[i] * sx, headY + warts[i + 1] * sy, EnemyRamps.Frog[4]);
            Dot(-40f * sx, -50f * sy, EnemyRamps.Frog[4]);
            Dot(44f * sx, -58f * sy, EnemyRamps.Frog[4]);

            // Olhos saltados com a pupila âmbar em fenda
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = s * 27f * sx, ey = headY - 22f * sy;
                Ell(ex, ey, 13f, 12f * Math.Max(0.6f, sy), EnemyRamps.Frog, 0.1f);
                Ell(ex, ey - 2f, 9f, 8.5f * Math.Max(0.6f, sy), EnemyRamps.Belly, 0.2f);
                if (p.Fall > 0.7f)
                {
                    Line(ex - 4f, ey - 6f, ex + 4f, ey + 2f, EnemyRamps.Dark, 1.5f);
                    Line(ex + 4f, ey - 6f, ex - 4f, ey + 2f, EnemyRamps.Dark, 1.5f);
                }
                else
                    EmitEll(ex, ey - 2f, 2.4f, 6f * Math.Max(0.6f, sy), EnemyRamps.Glow, -0.1f);
            }

            // Coroa: juncos e uma folha de aguapé entre os olhos
            Ell(0f, headY - 24f * sy, 12f, 3f, EnemyRamps.Moss, 0.1f);
            Path(EnemyRamps.Stem[3], 1.2f, -5f, headY - 25f * sy, -8f, headY - 40f * sy);
            Path(EnemyRamps.Stem[3], 1.2f, 2f, headY - 25f * sy, 3f, headY - 44f * sy);
            Path(EnemyRamps.Stem[2], 1.2f, 7f, headY - 25f * sy, 11f, headY - 38f * sy);
            Ell(3f, headY - 46f * sy, 2f, 4f, EnemyRamps.Bark, 0.1f);
        }

        // Boca: linha larga fechada; aberta, com o ácido verde aceso dentro e gotas saindo
        void Mouth(float headY, Pose p, float sx, float sy)
        {
            float y = headY + 9f * sy;
            if (p.Mouth < 0.3f)
            {
                Path(EnemyRamps.Dark, 1.6f, -44f * sx, y - 3f, -20f * sx, y + 1f, 0f, y + 2f, 20f * sx, y + 1f,
                    44f * sx, y - 3f);
                return;
            }

            float open = 10f + p.Mouth * 14f;
            Ell(0f, y + open * 0.5f, 34f * sx, open * 0.6f, EnemyRamps.Robe, -0.5f);
            EmitEll(0f, y + open * 0.6f, 22f * sx, open * 0.35f, EnemyRamps.Acid, p.Mouth - 0.4f);
            Ell(0f, y + open * 0.85f, 14f, 4f, EnemyRamps.Petal, 0.1f); // língua
            var random = new Random(3);
            for (int i = 0; i < 8; i++)
            {
                float dx = (float)(random.NextDouble() * 2.0 - 1.0) * 40f;
                float dy = (float)random.NextDouble() * 10f + p.Spit * 26f;
                EmitEll(dx * (0.4f + p.Spit), y + open * 0.4f - dy, 2f, 2.4f, EnemyRamps.Acid, 0.2f);
            }
        }

        void Foot(float x, float y, int s)
        {
            Ell(x, y, 12f, 3f, EnemyRamps.Frog, -0.1f);
            for (int i = 0; i < 3; i++)
                Ell(x + s * (6f + i * 3f), y + 0.5f - i, 2.5f, 1.8f, EnemyRamps.Frog, -0.2f);
        }
    }
}
