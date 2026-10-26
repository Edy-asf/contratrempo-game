namespace Icaro
{
    // Golem: colosso de pedra cinza-azulada com o núcleo ciano no peito (de onde sai o laser), ombros de pedregulho
    // cobertos de musgo, trepadeiras penduradas e runas ciano nos antebraços. Mirando, o núcleo cresce e acende
    // (Windup) e dispara (Attack); para a pancada ergue os braços (Windup2) e bate os punhos no chão (Attack2). Ao
    // morrer, desmorona em pedras e o núcleo apaga.
    public sealed class GolemArt : EnemyArt
    {
        public override string Key => "Golem";
        public override int Width => 124;
        public override int Height => 92;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 4, 5f, true),
            new EnemyClipSpec("Windup", 3, 6f, false),
            new EnemyClipSpec("Attack", 2, 12f, true),
            new EnemyClipSpec("Windup2", 3, 6f, false),
            new EnemyClipSpec("Attack2", 3, 10f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 8f, false),
        };

        struct Pose
        {
            public float Bob, Step, Core, Charge, Fire, Raise, Crouch, Dust, Lean, Crumble;
            public int StepFrame;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose { Core = 0.5f };
            switch (clip)
            {
                case "Idle":
                    p.Bob = Sin01(t) > 0f ? 0f : 1f;
                    p.Core = 0.45f + 0.15f * Sin01(t);
                    break;
                case "Move":
                    p.Step = Sin01(t);
                    p.StepFrame = frame;
                    p.Bob = frame % 2 == 0 ? 0f : 1f;
                    break;
                case "Windup":
                    p.Charge = Ease(t);
                    p.Core = 0.6f + 0.4f * t;
                    p.Lean = 4f * t;
                    break;
                case "Attack":
                    p.Charge = 1f;
                    p.Fire = frame == 0 ? 1f : 0.7f;
                    p.Core = 1f;
                    p.Lean = 4f;
                    break;
                case "Windup2":
                    p.Raise = Ease(t);
                    p.Bob = -2f * t;
                    break;
                case "Attack2":
                    p.Raise = frame == 0 ? 0.35f : 0f;
                    p.Crouch = frame == 0 ? 2f : frame == 1 ? 3.5f : 1.5f;
                    p.Dust = frame == 0 ? 0.5f : frame == 1 ? 1f : 0.6f;
                    break;
                case "Hit":
                    p.Lean = frame == 0 ? -4f : -1.5f;
                    p.Core = 0.9f;
                    break;
                case "Death":
                    p.Crumble = Ease(t);
                    p.Core = 0.5f * (1f - t);
                    break;
            }

            if (p.Dust > 0f)
                Dust(p.Dust, view == EnemyView.Side);

            Push(0f, p.Bob + p.Crouch, view == EnemyView.Back ? -p.Lean : p.Lean, 0f, 0f);
            if (view == EnemyView.Side)
                Side(p);
            else
                FrontBack(p, view == EnemyView.Front);
            Pop();

            if (p.Crumble > 0.35f)
                Rubble(p.Crumble);
        }

        // ---------- de frente e de costas ----------

        void FrontBack(Pose p, bool front)
        {
            float c = p.Crumble;
            // Pernas e pés (andando, um pé sobe de cada vez)
            for (int s = -1; s <= 1; s += 2)
            {
                bool lifted = p.StepFrame % 2 == 1 && (p.Step > 0f ? s < 0 : s > 0);
                float lift = lifted ? 2f : 0f;
                float x = s < 0 ? -14f : 4f;
                Push(0f, c * 6f, 0f, 0f, 0f);
                Plate(x - 1f, -5f - lift, 11f, 5f, EnemyRamps.Stone, -0.15f);
                Plate(x + 1f, -23f - lift, 8f, 19f, EnemyRamps.Stone, -0.05f);
                Ell(x + 5f, -15f - lift, 3f, 1.4f, EnemyRamps.Moss, -0.15f);
                Pop();
            }

            // Tronco (afunda ao desmoronar)
            Push(0f, c * 16f, c * 8f, 0f, -30f);
            Plate(-14f, -29f, 28f, 8f, EnemyRamps.Stone, -0.1f);

            Poly(EnemyRamps.Stone, 0.05f, -21f, -59f, 21f, -59f, 17f, -27f, -17f, -27f);
            Line(-10f, -56f, -12f, -30f, EnemyRamps.Stone[1]);
            Line(10f, -56f, 12f, -30f, EnemyRamps.Stone[1]);
            Line(-16f, -42f, 16f, -42f, EnemyRamps.Stone[1]);
            if (front)
                Core(0f, -44f, p);
            else
            {
                // Costas tomadas de musgo, com trepadeiras
                Ell(0f, -47f, 15f, 9f, EnemyRamps.Moss, -0.05f);
                Ell(-6f, -36f, 6f, 3f, EnemyRamps.Moss, -0.15f);
                Path(EnemyRamps.Moss[2], 1f, -8f, -40f, -9f, -33f, -8f, -28f);
                Path(EnemyRamps.Moss[1], 1f, 7f, -42f, 8f, -34f);
            }

            // Cabeça afundada entre os ombros
            Push(c * 6f, c * 6f, c * 30f, 0f, -61f);
            Plate(-6f, -66f, 12f, 9f, EnemyRamps.Stone, front ? 0.1f : -0.05f);
            Ell(-1f, -66f, 4.5f, 1.5f, EnemyRamps.Moss);
            if (front)
            {
                Line(-6f, -63f, 6f, -63f, EnemyRamps.Stone[1]);
                EmitLine(-4f, -61f, -2f, -61f, p.Core > 0.2f ? EnemyRamps.CyanLight : EnemyRamps.Bio[0]);
                EmitLine(2f, -61f, 4f, -61f, p.Core > 0.2f ? EnemyRamps.CyanLight : EnemyRamps.Bio[0]);
            }
            Pop();

            // Ombros e braços
            for (int s = -1; s <= 1; s += 2)
            {
                float raise = -s * (150f * p.Raise + 10f * p.Charge);
                Push(s * c * 10f, c * 14f, s * c * 40f, s * 24f, -55f);
                Push(0f, 0f, raise, s * 24f, -55f);
                Arm(s, front);
                Pop();
                Ell(s * 23f, -56f, 8.5f, 7.5f, EnemyRamps.Stone, 0.1f);
                Ell(s * 22f, -62f, 6f, 2.2f, EnemyRamps.Moss);
                Path(EnemyRamps.Moss[2], 1f, s * 28f, -58f, s * 30f, -50f, s * 29f, -45f);
                Pop();
            }

            Pop();
        }

        // Braço pendurado a partir do ombro (s = lado), com o punho e as runas
        void Arm(int s, bool front)
        {
            Cap(s * 25f, -52f, s * 27f, -38f, 5f, EnemyRamps.Stone, -0.05f);
            Cap(s * 27f, -38f, s * 27f, -25f, 5.5f, EnemyRamps.Stone);
            Ell(s * 27f, -21f, 6.5f, 5.5f, EnemyRamps.Stone, 0.05f);
            Line(s * 24f, -23f, s * 30f, -23f, EnemyRamps.Stone[1]);
            if (front)
            {
                EmitLine(s * 28.5f, -35f, s * 28.5f, -31f, EnemyRamps.Bio[2]);
                EmitDot(s * 26f, -33f, EnemyRamps.Bio[2]);
            }
        }

        // Núcleo ciano no peito, com rachaduras acesas em volta
        void Core(float x, float y, Pose p)
        {
            float r = 4.5f + p.Charge * 1.5f + p.Fire * 2f;
            float glow = p.Core + p.Fire * 0.4f;
            EmitEll(x, y, r, r + 0.5f, EnemyRamps.Bio, glow - 0.55f);
            Light(x, y, r + 3f, EnemyRamps.CyanLight, glow * 0.7f);
            Px crack = glow > 0.3f ? EnemyRamps.Bio[2] : EnemyRamps.Bio[0];
            EmitLine(x - r, y, x - r - 4f, y - 3f, crack);
            EmitLine(x + r, y + 1f, x + r + 4f, y + 4f, crack);
            EmitLine(x, y - r, x + 1f, y - r - 4f, crack);
            if (p.Fire > 0f)
            {
                EmitEll(x, y, r * 0.6f, r * 0.6f, EnemyRamps.Bio, 0.6f);
                EmitDot(x, y, PixelRamps.White);
            }
        }

        // ---------- de lado (olhando para a direita) ----------

        void Side(Pose p)
        {
            float c = p.Crumble;
            float raise = -150f * p.Raise + 10f * p.Charge;

            // Braço de longe (atrás do corpo, mais escuro)
            Push(-3f, c * 14f, -c * 30f, 0f, -55f);
            Push(0f, 0f, raise, -3f, -55f);
            Cap(-3f, -52f, -1f, -38f, 4.6f, EnemyRamps.Stone, -0.35f);
            Cap(-1f, -38f, 1f, -25f, 5f, EnemyRamps.Stone, -0.35f);
            Ell(2f, -21f, 6f, 5.5f, EnemyRamps.Stone, -0.3f);
            Pop();
            Pop();

            // Pernas: a de longe e a de perto, alternando o passo
            for (int i = 0; i < 2; i++)
            {
                float swing = (i == 0 ? 1f : -1f) * p.Step * 3f;
                float bias = i == 0 ? -0.3f : -0.05f;
                float x = i == 0 ? -2f : -5f;
                Push(0f, c * 6f, 0f, 0f, 0f);
                Plate(x - 2f + swing, -5f, 13f, 5f, EnemyRamps.Stone, bias - 0.1f);
                Plate(x + swing * 0.5f, -23f, 9f, 19f, EnemyRamps.Stone, bias);
                Pop();
            }

            Push(0f, c * 16f, c * 25f, 0f, -30f);
            Plate(-9f, -29f, 17f, 8f, EnemyRamps.Stone, -0.1f);
            Poly(EnemyRamps.Stone, 0.05f, -11f, -59f, 10f, -59f, 12f, -29f, -10f, -27f);
            Line(-2f, -57f, -1f, -30f, EnemyRamps.Stone[1]);
            // Costas de musgo e trepadeiras
            Ell(-10f, -48f, 3f, 9f, EnemyRamps.Moss, -0.05f);
            Path(EnemyRamps.Moss[2], 1f, -11f, -42f, -12f, -34f, -11f, -29f);
            Core(11.5f, -44f, new Pose { Core = p.Core, Charge = p.Charge * 0.5f, Fire = p.Fire });

            // Cabeça para a frente
            Push(c * 5f, c * 5f, c * 40f, 3f, -61f);
            Plate(0f, -66f, 9f, 8f, EnemyRamps.Stone, 0.1f);
            Ell(2f, -66f, 4f, 1.4f, EnemyRamps.Moss);
            EmitLine(7f, -61f, 8.5f, -61f, p.Core > 0.2f ? EnemyRamps.CyanLight : EnemyRamps.Bio[0]);
            Pop();

            // Ombro e braço de perto
            Push(0f, 0f, raise, 0f, -55f);
            Cap(0f, -52f, 2f, -38f, 5f, EnemyRamps.Stone, -0.05f);
            Cap(2f, -38f, 4f, -25f, 5.5f, EnemyRamps.Stone);
            Ell(5f, -21f, 6.5f, 5.5f, EnemyRamps.Stone, 0.05f);
            EmitLine(4f, -35f, 5f, -31f, EnemyRamps.Bio[2]);
            Pop();
            Ell(0f, -56f, 8f, 7.5f, EnemyRamps.Stone, 0.1f);
            Ell(-1f, -62f, 6f, 2.2f, EnemyRamps.Moss);
            Path(EnemyRamps.Moss[2], 1f, -5f, -58f, -6f, -50f);
            Pop();
        }

        // ---------- efeitos ----------

        // Poeira levantada pela pancada, nos dois lados dos punhos
        void Dust(float amount, bool side)
        {
            float spread = 14f + amount * 12f;
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0)
                    continue;
                float x = (side ? 6f : 0f) + i * spread * 0.5f;
                Ell(x, -2f - amount * 2f, 3f + amount * 2f, 1.6f + amount * 1.5f, EnemyRamps.Bone, -0.3f);
            }
        }

        // Pedras no chão onde ele desmoronou
        void Rubble(float amount)
        {
            float k = (amount - 0.35f) / 0.65f;
            Ell(-14f, -3f, 6f * k, 3.5f * k, EnemyRamps.Stone, -0.1f);
            Ell(12f, -3f, 7f * k, 4f * k, EnemyRamps.Stone);
            Ell(0f, -5f, 9f * k, 5f * k, EnemyRamps.Stone, 0.05f);
            Ell(-3f, -9f, 4f * k, 2f * k, EnemyRamps.Moss, -0.1f);
        }
    }
}
