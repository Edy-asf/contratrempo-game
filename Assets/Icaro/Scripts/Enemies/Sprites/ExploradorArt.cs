namespace Icaro
{
    // Explorador Tomado (o Vagante): robô explorador de uma missão anterior à do ÍCARO-9, tomado pela mata. Corpo de
    // metal velho com ferrugem, mochila coberta de musgo com trepadeiras penduradas, visor ciano rachado com um
    // cogumelo âmbar saindo da rachadura, e o braço direito virou um canhão que ainda funciona. No aviso o cano acende
    // e ele ergue o braço; na rajada, coice e clarão. Ao morrer, os joelhos cedem e ele tomba.
    public sealed class ExploradorArt : EnemyArt
    {
        public override string Key => "Vagante";
        public override int Width => 50;
        public override int Height => 40;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 4, 7f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 2, 12f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 5, 9f, false),
        };

        struct Pose
        {
            public float Walk, Bob, Aim, Charge, Recoil, Flash, Lean, Fall;
            public bool Walking;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose();
            switch (clip)
            {
                case "Idle":
                    p.Bob = Sin01(t) > 0f ? 0f : 0.6f;
                    break;
                case "Move":
                    p.Walking = true;
                    p.Walk = t;
                    p.Bob = frame % 2 == 0 ? 0f : -0.8f;
                    break;
                case "Windup":
                    p.Aim = Ease(t);
                    p.Charge = 0.3f + 0.7f * t;
                    break;
                case "Attack":
                    p.Aim = 1f;
                    p.Charge = 0.5f;
                    p.Recoil = frame == 0 ? 1f : 0f;
                    p.Flash = frame == 0 ? 1f : 0f;
                    break;
                case "Hit":
                    p.Lean = frame == 0 ? -1f : -0.4f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    break;
            }

            // Os joelhos cedem (afunda) e ele tomba de lado em volta dos joelhos
            Push(0f, p.Bob + p.Fall * 6.5f, (view == EnemyView.Back ? 1f : -1f) * 70f * p.Fall + p.Lean * 6f, 0f, -7f);
            if (view == EnemyView.Side)
                Side(p);
            else if (view == EnemyView.Front)
                Front(p);
            else
                Back(p);
            Pop();
        }

        // ---------- de lado (olhando para a direita) ----------

        void Side(Pose p)
        {
            // Braço de trás (mais escuro), pernas, mochila, tronco, cabeça e o braço-canhão na frente
            Cap(-1f, -22f, -2f, -15f, 1.3f, EnemyRamps.Metal, -0.35f);
            LegSide(p, 0.5f, -0.3f);
            LegSide(p, 0f, 0f);

            Plate(-7.5f, -24f, 4f, 9f, EnemyRamps.Metal, -0.2f);
            Ell(-6f, -24.5f, 3f, 1.6f, EnemyRamps.Moss, 0.05f);
            Path(EnemyRamps.Moss[2], 1f, -7f, -23f, -7.5f, -19f, -7f, -16f);
            Path(EnemyRamps.Moss[1], 1f, -5f, -23f, -5.5f, -18f);

            Plate(-3.5f, -24.5f, 7.5f, 10.5f, EnemyRamps.Metal);
            Ell(2f, -18f, 1.5f, 1f, EnemyRamps.Rust, -0.1f);
            Line(-3f, -15f, 3.5f, -15f, EnemyRamps.Dark);

            Head(1f, p, side: true);

            // Braço-canhão: ombro, cotovelo e o cano para a frente; ergue para mirar
            float aimY = -18f - p.Aim * 2.5f, back = p.Recoil;
            Ell(1f, -22.5f, 2.4f, 2f, EnemyRamps.Metal, 0.1f);
            Cap(1f, -22f, 2.5f - back, aimY + 1.5f, 1.4f, EnemyRamps.Metal, 0.05f);
            Plate(2f - back, aimY - 1f, 8f, 3.2f, EnemyRamps.Metal, -0.1f, 1f);
            Plate(9.5f - back, aimY - 0.5f, 1.8f, 2.2f, EnemyRamps.Shell, 0.1f, 0.5f);
            Muzzle(11.5f - back, aimY + 0.6f, p);
        }

        void LegSide(Pose p, float phase, float bias)
        {
            float swing = p.Walking ? 2.8f * Sin01(p.Walk + phase) : 0f;
            float lift = p.Walking ? System.Math.Max(0f, Cos01(p.Walk + phase)) * 1.2f : 0f;
            float footX = swing, footY = -1.2f - lift;
            Cap(0f, -14f, swing * 0.5f + 0.8f, -7.5f, 1.6f, EnemyRamps.Metal, bias);
            Cap(swing * 0.5f + 0.8f, -7.5f, footX, footY, 1.4f, EnemyRamps.Metal, bias - 0.05f);
            Plate(footX - 1.5f, footY - 0.8f, 4f, 2f, EnemyRamps.Metal, bias - 0.15f, 0.6f);
        }

        // ---------- de frente ----------

        void Front(Pose p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float step = p.Walking ? Sin01(p.Walk + (s > 0 ? 0f : 0.5f)) : 0f;
                float footY = -1.2f - System.Math.Max(0f, step) * 1.2f;
                Cap(s * 2.4f, -14f, s * 2.6f, -7.5f, 1.6f, EnemyRamps.Metal, -0.05f);
                Cap(s * 2.6f, -7.5f, s * 2.6f, footY, 1.4f, EnemyRamps.Metal, -0.1f);
                Plate(s * 2.6f - 1.6f, footY - 1f, 3.2f, 2f, EnemyRamps.Metal, -0.15f, 0.6f);
            }

            // Mochila aparece pelos lados e trepadeiras caindo do ombro
            Plate(-5.5f, -24f, 11f, 9f, EnemyRamps.Metal, -0.3f);
            Plate(-4.5f, -24.5f, 9f, 10.5f, EnemyRamps.Metal);
            Ell(-3.5f, -24.5f, 2.6f, 1.3f, EnemyRamps.Moss, 0.05f);
            Path(EnemyRamps.Moss[2], 1f, -5f, -24f, -5.5f, -19f);
            Ell(1.5f, -19f, 1.5f, 1.1f, EnemyRamps.Rust, -0.1f);
            Line(-4f, -15f, 4f, -15f, EnemyRamps.Dark);

            // Braço esquerdo dele (à direita da imagem) caído
            Cap(5f, -22.5f, 5.8f, -15f, 1.3f, EnemyRamps.Metal, 0f);

            Head(0f, p, side: false);

            // Braço-canhão (à esquerda da imagem) apontado para o jogador: o cano visto de frente
            float y = -18f - p.Aim * 2.5f;
            Cap(-5f, -22.5f, -5.6f, y + 1f, 1.5f, EnemyRamps.Metal, 0.05f);
            Ell(-5.8f, y, 2.8f + p.Recoil * 0.5f, 2.6f, EnemyRamps.Metal, 0.1f);
            Ell(-5.8f, y, 1.6f, 1.5f, EnemyRamps.Shell, -0.4f);
            Muzzle(-5.8f, y, p);
        }

        // ---------- de costas ----------

        void Back(Pose p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float step = p.Walking ? Sin01(p.Walk + (s > 0 ? 0.5f : 0f)) : 0f;
                float footY = -1.2f - System.Math.Max(0f, step) * 1.2f;
                Cap(s * 2.4f, -14f, s * 2.6f, -7.5f, 1.6f, EnemyRamps.Metal, -0.1f);
                Cap(s * 2.6f, -7.5f, s * 2.6f, footY, 1.4f, EnemyRamps.Metal, -0.15f);
                Plate(s * 2.6f - 1.6f, footY - 1f, 3.2f, 2f, EnemyRamps.Metal, -0.2f, 0.6f);
            }

            Cap(-5f, -22.5f, -5.8f, -15f, 1.3f, EnemyRamps.Metal, -0.1f);
            Cap(5f, -22.5f, 5.6f, -17f, 1.5f, EnemyRamps.Metal, -0.1f);
            Plate(-4.5f, -24.5f, 9f, 10.5f, EnemyRamps.Metal, -0.1f);
            Head(0f, p, side: false, back: true);

            // Mochila tomada de musgo, com trepadeiras penduradas
            Plate(-4f, -24f, 8f, 8.5f, EnemyRamps.Metal, -0.2f);
            Ell(0f, -23.5f, 4.2f, 2f, EnemyRamps.Moss, 0.05f);
            Ell(-1.5f, -20f, 2f, 1.4f, EnemyRamps.Moss, -0.1f);
            Path(EnemyRamps.Moss[2], 1f, -3f, -22f, -3.5f, -17f, -3f, -14f);
            Path(EnemyRamps.Moss[1], 1f, 2f, -21f, 2.5f, -16f);
            Ell(2f, -18f, 1.3f, 1f, EnemyRamps.Rust, -0.15f);
        }

        // ---------- partes ----------

        // Cabeça em caixa com visor ciano rachado e o cogumelo âmbar saindo da rachadura
        void Head(float x, Pose p, bool side, bool back = false)
        {
            Cap(x, -24.5f, x, -26f, 1.2f, EnemyRamps.Metal, -0.2f); // pescoço
            Plate(x - 3.2f, -31.5f, 6.4f, 6f, EnemyRamps.Metal, back ? -0.1f : 0.05f, 1.2f);
            if (!back)
            {
                if (side)
                    EmitPoly(EnemyRamps.Bio, -0.3f, x + 1.5f, -30f, x + 3.4f, -30f, x + 3.4f, -27.5f, x + 1.5f, -27.5f);
                else
                    EmitPoly(EnemyRamps.Bio, -0.3f, x - 2.5f, -30f, x + 2.5f, -30f, x + 2.5f, -28f, x - 2.5f, -28f);
                Line(x + (side ? 2.4f : 0.5f), -30f, x + (side ? 2f : 1f), -28f, EnemyRamps.Dark); // rachadura
            }

            // Cogumelo: haste saindo do visor e chapéu âmbar
            float mx = x + (side ? 2f : 1f);
            Cap(mx, -29.5f, mx + 1f, -33f, 0.7f, EnemyRamps.Bone, back ? -0.2f : 0f);
            EmitEll(mx + 1.2f, -33.5f, 3f, 1.5f, EnemyRamps.Glow, back ? -0.45f : -0.3f);
            Dot(mx, -33.8f, EnemyRamps.Glow[4]);
            Ell(x - 2f, -31.5f, 1.8f, 0.8f, EnemyRamps.Moss, -0.1f);
        }

        // Boca do canhão: acende no aviso e solta o clarão no tiro
        void Muzzle(float x, float y, Pose p)
        {
            if (p.Flash > 0f)
            {
                EmitEll(x + 0.5f, y, 2.8f, 2.4f, EnemyRamps.Glow, 0.3f);
                EmitDot(x + 0.5f, y, PixelRamps.White);
            }
            else if (p.Charge > 0f)
            {
                EmitEll(x, y, 0.8f + p.Charge * 1.2f, 0.8f + p.Charge * 1.1f, EnemyRamps.Glow, p.Charge - 0.6f);
            }
            else
            {
                EmitDot(x, y, EnemyRamps.Glow[1]);
            }
        }
    }
}
