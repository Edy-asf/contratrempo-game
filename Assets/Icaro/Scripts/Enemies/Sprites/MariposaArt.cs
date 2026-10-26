namespace Icaro
{
    // Mariposa-lanterna (o Zangão): mariposa escura de antenas em pena, asas com olhos ciano e o abdômen âmbar aceso
    // como uma lanterna; em bando parece um enxame de luzes na névoa. Bate as asas o tempo todo; no aviso o ferrão
    // (a ponta do abdômen) acende branco; ao morrer, fecha as asas e cai girando. O pivô é o centro do corpo.
    public sealed class MariposaArt : EnemyArt
    {
        public override string Key => "Zangao";
        public override int Width => 28;
        public override int Height => 24;
        public override float PivotY => 12f;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 14f, true),
            new EnemyClipSpec("Move", 4, 16f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 2, 10f, false),
            new EnemyClipSpec("Hit", 2, 12f, false),
            new EnemyClipSpec("Death", 5, 10f, false),
        };

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            // Batida das asas: -1 em cima, 1 embaixo
            float flap = Sin01(t), glow = 0.55f, sting = 0f, fold = 0f, spin = 0f;
            switch (clip)
            {
                case "Windup":
                    flap = Sin01(t * 2f);
                    glow = 0.6f + 0.4f * t;
                    sting = t;
                    break;
                case "Attack":
                    flap = frame == 0 ? -0.6f : 0.4f;
                    glow = 1f;
                    sting = frame == 0 ? 1f : 0.3f;
                    break;
                case "Hit":
                    flap = frame == 0 ? -1f : 1f;
                    glow = 0.8f;
                    break;
                case "Death":
                    fold = Ease(t);
                    flap = -1f;
                    glow = 0.5f * (1f - t);
                    spin = 200f * t;
                    break;
            }

            Push(0f, 0f, spin, 0f, 0f);
            if (view == EnemyView.Side)
                Side(flap, glow, sting, fold);
            else
                FrontBack(flap, glow, sting, fold, view == EnemyView.Front);
            Pop();
        }

        // ---------- de frente e de costas ----------

        void FrontBack(float flap, float glow, float sting, float fold, bool front)
        {
            float span = (1f - System.Math.Abs(flap) * 0.25f) * (1f - fold * 0.75f);
            float tip = flap * 4f - fold * 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                // Asa de trás (menor) e asa da frente, com o olho ciano
                Poly(EnemyRamps.MothWing, -0.2f,
                    s * 1.2f, 0.5f, s * 8f * span, 2.5f + tip * 0.4f, s * 6f * span, 6.5f + tip * 0.2f, s * 1.5f, 3.5f);
                Poly(EnemyRamps.MothWing, front ? 0.05f : 0.1f,
                    s * 1.4f, -2.5f, s * 12f * span, -6f + tip, s * 12.5f * span, -2f + tip * 0.8f,
                    s * 9f * span, 1.5f + tip * 0.5f, s * 1.5f, 1f);
                Line(s * 2f, -1.5f, s * 10.5f * span, -4.5f + tip * 0.9f, EnemyRamps.MothWing[1]);
                if (span > 0.4f)
                {
                    EmitEll(s * 7.5f * span, -2.5f + tip * 0.6f, 1.3f, 1.2f, EnemyRamps.Bio, front ? -0.2f : 0f);
                    Dot(s * 7.5f * span, -2.5f + tip * 0.6f, EnemyRamps.Dark);
                }
            }

            // Abdômen âmbar aceso embaixo e o ferrão
            EmitEll(0f, 4f, 2f, 3.4f, EnemyRamps.Glow, glow - 0.55f);
            Light(0f, 4f, 3f, EnemyRamps.AmberLight, glow * 0.6f);
            Line(-1.6f, 3f, 1.6f, 3f, EnemyRamps.Glow[0]);
            Line(-1.4f, 5f, 1.4f, 5f, EnemyRamps.Glow[0]);
            if (sting > 0.1f)
                EmitDot(0f, 7.5f, sting > 0.6f ? PixelRamps.White : EnemyRamps.Glow[4]);

            // Tórax peludo e cabeça com as antenas em pena
            Ell(0f, -1f, 2.4f, 2.8f, EnemyRamps.MothBody, 0.05f);
            if (front)
            {
                Ell(0f, -4f, 1.9f, 1.6f, EnemyRamps.MothBody, 0.1f);
                EmitDot(-1f, -4.3f, EnemyRamps.CyanLight);
                EmitDot(1f, -4.3f, EnemyRamps.CyanLight);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                Path(EnemyRamps.MothBody[3], 1f, s * 0.8f, -5f, s * 2.5f, -7.5f, s * 4.5f, -9f);
                Dot(s * 2.2f, -8.2f, EnemyRamps.MothBody[4]);
                Dot(s * 3.6f, -9.4f, EnemyRamps.MothBody[4]);
            }
        }

        // ---------- de lado (olhando para a direita) ----------

        void Side(float flap, float glow, float sting, float fold)
        {
            float lift = flap * 5f;
            // Asa de longe (aparece por trás) e corpo deitado: cabeça à direita, abdômen aceso atrás
            Poly(EnemyRamps.MothWing, -0.3f, -1f, -1f, -4f, -8f - lift * 0.6f + fold * 3f, 2f, -8.5f - lift * 0.6f + fold * 3f, 2f, -1f);
            EmitEll(-4.5f, 1f, 3.6f, 2f, EnemyRamps.Glow, glow - 0.55f);
            Light(-4.5f, 1f, 3f, EnemyRamps.AmberLight, glow * 0.6f);
            Line(-5.5f, -0.5f, -5.5f, 2.6f, EnemyRamps.Glow[0]);
            Line(-3.5f, -0.6f, -3.5f, 2.7f, EnemyRamps.Glow[0]);
            if (sting > 0.1f)
                EmitDot(-8.5f, 1.5f, sting > 0.6f ? PixelRamps.White : EnemyRamps.Glow[4]);

            Ell(1f, 0f, 2.6f, 2.3f, EnemyRamps.MothBody, 0.05f);
            Ell(4f, -0.4f, 1.6f, 1.6f, EnemyRamps.MothBody, 0.1f);
            EmitDot(4.8f, -0.8f, EnemyRamps.CyanLight);
            Path(EnemyRamps.MothBody[3], 1f, 4.5f, -1.8f, 6.5f, -5f, 8.5f, -6.5f);
            Dot(6.2f, -4f, EnemyRamps.MothBody[4]);

            // Asa de perto, por cima, batendo
            float top = -9f - lift + fold * 4f, bottom = fold > 0.5f ? -3f : 4f + lift * 0.3f;
            Poly(EnemyRamps.MothWing, 0.05f, -0.5f, -1.5f, -5.5f, top + 1f, 1.5f, top, 3f, -1.5f, 0.5f, bottom * 0.4f);
            Line(0.5f, -1.5f, -2f, top + 2f, EnemyRamps.MothWing[1]);
            if (fold < 0.5f)
                EmitEll(-1.5f, (top - 1.5f) * 0.55f, 1.1f, 1f, EnemyRamps.Bio, -0.1f);
        }
    }
}
