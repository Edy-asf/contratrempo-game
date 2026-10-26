using static Icaro.PixelCanvas;

namespace Icaro
{
    // Tatu-lanterna (o Cascudo): bicho atarracado de carapaça em placas de casca de árvore com musgo, quatro patas
    // curtas e uma lanterna âmbar pendurada numa haste na testa, como o peixe-diabo. No aviso as placas se abrem e
    // mostram o miolo âmbar aceso; o golpe é uma cabeçada. Ao morrer, rola de costas e a lanterna apaga.
    public sealed class TatuLanternaArt : EnemyArt
    {
        public override string Key => "Cascudo";
        public override int Width => 44;
        public override int Height => 36;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 4f, true),
            new EnemyClipSpec("Move", 4, 8f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 3, 10f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 5, 9f, false),
        };

        // Pose de um quadro
        struct Pose
        {
            public float Bob, Lunge, Open, HeadDip, Legs, Lantern, Roll;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose { Lantern = 0.6f };
            switch (clip)
            {
                case "Idle":
                    p.Bob = Sin01(t) > 0.3f ? -0.6f : 0f;
                    p.Lantern = 0.5f + 0.2f * Sin01(t);
                    break;
                case "Move":
                    p.Legs = Sin01(t);
                    p.Bob = frame % 2 == 0 ? 0f : -1f;
                    break;
                case "Windup":
                    p.Open = Ease(t);
                    p.Lunge = -1.5f * t;
                    p.HeadDip = 1f * t;
                    p.Lantern = 0.6f + 0.4f * t;
                    break;
                case "Attack":
                    p.Open = 1f - t;
                    p.Lunge = frame == 0 ? 3f : frame == 1 ? 4f : 1.5f;
                    p.HeadDip = frame < 2 ? 1.5f : 0.5f;
                    p.Lantern = 1f;
                    break;
                case "Hit":
                    p.Lunge = frame == 0 ? -1.5f : -0.5f;
                    p.Bob = frame == 0 ? 1f : 0f;
                    p.Lantern = 0.9f;
                    break;
                case "Death":
                    p.Roll = Ease(t);
                    p.Lantern = 0.6f * (1f - t);
                    p.Legs = 0.6f * t;
                    break;
            }

            // Ao morrer, rola de costas em volta do centro da carapaça e assenta no chão
            Push(p.Lunge, p.Bob - 1.5f * p.Roll, 175f * p.Roll * (view == EnemyView.Back ? -1f : 1f), -1f, -10f);
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
            float a = p.Legs * 2.2f;
            // Patas de trás (do outro lado, mais escuras)
            Leg(-7f + 1f, -a, -0.35f);
            Leg(7f + 1f, a, -0.35f);

            // Miolo âmbar (aparece entre as placas abertas) e carapaça
            Shell(-1f, p.Open, side: true);
            Ell(-14.5f, -5f, 2.2f, 1.6f, EnemyRamps.Bark, -0.1f); // rabo

            // Cabeça com focinho, olho e boca
            Push(0f, p.HeadDip);
            Ell(13f, -7f, 5f, 4.2f, EnemyRamps.Bark);
            Ell(17.5f, -5.5f, 2.6f, 2.1f, EnemyRamps.Bark, 0.1f);
            Line(15f, -3.6f, 19f, -4.4f, EnemyRamps.Dark);
            EmitDot(14.5f, -8.5f, EnemyRamps.CyanLight);
            Lantern(12f, -10.5f, 15.5f, -19f, 19.5f, -17.5f, p.Lantern);
            Pop();

            // Patas da frente (deste lado)
            Leg(-7f, a, 0f);
            Leg(7f, -a, 0f);
        }

        void Leg(float x, float swing, float bias)
        {
            Cap(x, -5f, x + swing, -1.2f, 2.3f, EnemyRamps.Bark, bias - 0.05f);
            Ell(x + swing + 0.5f, -1f, 2.4f, 1f, EnemyRamps.Bark, bias - 0.3f);
        }

        // ---------- de frente ----------

        void Front(Pose p)
        {
            float a = p.Legs * 1.2f;
            Cap(-8f, -5f, -8f, -1.2f - a, 2.4f, EnemyRamps.Bark, -0.1f);
            Cap(8f, -5f, 8f, -1.2f + a, 2.4f, EnemyRamps.Bark, -0.1f);
            Shell(0f, p.Open, side: false);

            Push(0f, p.HeadDip);
            Ell(0f, -6.5f, 5.6f, 4.6f, EnemyRamps.Bark);
            Ell(0f, -4.5f, 2.8f, 2f, EnemyRamps.Bark, 0.12f);
            Line(-1.5f, -2.6f, 1.5f, -2.6f, EnemyRamps.Dark);
            EmitDot(-2.5f, -7.5f, EnemyRamps.CyanLight);
            EmitDot(2.5f, -7.5f, EnemyRamps.CyanLight);
            Lantern(0f, -10.5f, 1.5f, -20f, 5f, -18.5f, p.Lantern);
            Pop();
        }

        // ---------- de costas ----------

        void Back(Pose p)
        {
            float a = p.Legs * 1.2f;
            // A lanterna aparece por cima da carapaça
            Lantern(0f, -10f, -1f, -21.5f, -3f, -21f, p.Lantern);
            Cap(-8f, -5f, -8f, -1.2f + a, 2.4f, EnemyRamps.Bark, -0.15f);
            Cap(8f, -5f, 8f, -1.2f - a, 2.4f, EnemyRamps.Bark, -0.15f);
            Shell(0f, p.Open, side: false);
            Ell(0f, -4.5f, 2.2f, 2f, EnemyRamps.Bark, -0.1f); // rabo
        }

        // ---------- partes ----------

        // Carapaça em domo dividida em placas (de lado, faixas na vertical; de frente e de costas, na horizontal), com
        // musgo no alto. As placas se afastam e sobem com 'open', e o miolo âmbar aparece entre elas.
        void Shell(float cx, float open, bool side)
        {
            const float cy = -9f, rx = 14.5f, ry = 10.5f, bottom = -3.5f;
            Shape dome = Where(Ellipse(cx, cy, rx, ry), (x, y) => y <= bottom);
            if (open > 0.05f)
                EmitEll(cx, cy - 1f, rx - 3f, ry - 3.5f, EnemyRamps.Glow, open * 0.3f);
            else
                Shape(dome, EnemyRamps.Shell, -0.55f);

            float[] cuts = side ? new[] { -16f, -8f, -1f, 6f, 16f } : new[] { -21f, -16f, -11.5f, -7.5f, bottom };
            float gap = 0.6f + open * 1.8f;
            for (int i = 0; i < cuts.Length - 1; i++)
            {
                float a0 = cuts[i] + gap * 0.5f, a1 = cuts[i + 1] - gap * 0.5f;
                // Placas do meio sobem mais
                float lift = open * (side ? 2.2f - System.Math.Abs(i - 1.5f) * 0.8f : 0.6f + (3 - i) * 0.7f);
                Shape plate = side
                    ? Where(Ellipse(cx, cy - lift, rx, ry), (x, y) => x - cx >= a0 && x - cx <= a1 && y <= bottom - lift)
                    : Where(Ellipse(cx, cy - lift, rx, ry), (x, y) => y >= a0 - lift && y <= a1 - lift);
                Shape(plate, EnemyRamps.Shell, 0.05f);
            }

            // Musgo no alto e na borda de baixo
            Ell(cx - 4f, -18.5f - open * 1.5f, 4.5f, 1.6f, EnemyRamps.Moss);
            Ell(cx + 4.5f, -17.8f - open * 1.5f, 3f, 1.3f, EnemyRamps.Moss, -0.1f);
            Ell(cx + (side ? -10f : -9f), -5f, 2.5f, 1f, EnemyRamps.Moss, -0.2f);
        }

        // Haste curva saindo da testa (x0, y0), passando por (x1, y1), com a lanterna pendurada em (x2, y2)
        void Lantern(float x0, float y0, float x1, float y1, float x2, float y2, float glow)
        {
            Path(EnemyRamps.Antler[2], 1f, x0, y0, (x0 + x1) * 0.5f - 0.5f, (y0 + y1) * 0.5f, x1, y1, x2, y2 - 1.5f);
            EmitEll(x2, y2 + 0.8f, 2.4f, 2.6f, EnemyRamps.Glow, glow - 0.5f);
            Light(x2, y2 + 0.8f, 3f, EnemyRamps.AmberLight, glow * 0.8f);
        }
    }
}
