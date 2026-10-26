namespace Icaro
{
    // Galhada (o Predador): quadrúpede magro de pelo escuro, pernas finas e chifres que são galhos (como a árvore
    // Galhada), com pontas e listras bioluminescentes ciano no flanco. No aviso abaixa a cabeça com a galhada para a
    // frente, se encolhe e as listras acendem; na investida galopa esticado. Ao morrer, as pernas dobram e ele cai.
    public sealed class GalhadaArt : EnemyArt
    {
        public override string Key => "Predador";
        public override int Width => 48;
        public override int Height => 44;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 4, 8f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 4, 14f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 5, 9f, false),
        };

        struct Pose
        {
            public float Crouch, HeadDown, Glow, Gallop, Walk, Fall, Stretch, Idle;
            public bool Galloping;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose { Glow = 0.35f };
            switch (clip)
            {
                case "Idle":
                    p.Idle = Sin01(t);
                    break;
                case "Move":
                    p.Walk = t;
                    break;
                case "Windup":
                    p.Crouch = 2f * Ease(t);
                    p.HeadDown = Ease(t);
                    p.Glow = 0.4f + 0.6f * t;
                    break;
                case "Attack":
                    p.Galloping = true;
                    p.Gallop = t;
                    p.HeadDown = 1f;
                    p.Stretch = 1f;
                    p.Glow = 1f;
                    break;
                case "Hit":
                    p.Crouch = frame == 0 ? 1f : 0.3f;
                    p.HeadDown = frame == 0 ? -0.4f : 0f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    p.Glow = 0.35f * (1f - t);
                    break;
            }

            if (view == EnemyView.Side)
                Side(p);
            else
                FrontBack(p, view == EnemyView.Front);
        }

        // ---------- de lado (olhando para a direita) ----------

        void Side(Pose p)
        {
            float bodyY = -17f + p.Crouch + p.Fall * 10f;
            // Pernas: as de longe primeiro, mais escuras
            LegsSide(p, bodyY, far: true);

            Push(0f, 0f, -12f * p.Fall, 0f, bodyY);
            // Corpo: traseiro, barriga e peito
            Ell(-8f, bodyY - 0.5f, 5.5f, 5f, EnemyRamps.Hide, -0.05f);
            Ell(-1f, bodyY, 9f + p.Stretch, 4.8f, EnemyRamps.Hide);
            Ell(6f + p.Stretch, bodyY - 0.5f, 4.6f, 5.2f, EnemyRamps.Hide, 0.08f);
            Ell(-13.5f, bodyY - 2.5f, 1.6f, 1.2f, EnemyRamps.Bone, -0.1f); // rabo claro

            // Listras ciano no flanco
            Stripe(-7f, bodyY - 3f, -4f, bodyY - 3.6f, p.Glow);
            Stripe(-3f, bodyY - 3.4f, 0.5f, bodyY - 3.8f, p.Glow);
            Stripe(1.5f, bodyY - 3.3f, 4.5f, bodyY - 3f, p.Glow);

            // Pescoço e cabeça (abaixa no aviso e na investida)
            float neckX = 8.5f + p.Stretch, neckY = bodyY - 2f;
            float down = p.HeadDown;
            float headX = neckX + 5f + down * 2f, headY = bodyY - 8f + down * 6f + p.Idle * 0.4f;
            Cap(neckX, neckY, headX - 1.5f, headY + 0.5f, 2.3f, EnemyRamps.Hide, 0.05f);
            Ell(headX, headY, 3.6f, 2.4f, EnemyRamps.Hide, 0.1f);
            Cap(headX + 1f, headY + 0.6f, headX + 4.6f, headY + 1.8f, 1.5f, EnemyRamps.Hide, 0.12f);
            Dot(headX + 4.8f, headY + 1.8f, EnemyRamps.Dark);
            EmitDot(headX + 0.8f, headY - 0.6f, EnemyRamps.CyanLight);
            Ell(headX - 2.2f, headY - 1.8f, 1.2f, 1.8f, EnemyRamps.Hide, -0.1f); // orelha

            // Galhada: gira para a frente quando a cabeça abaixa
            Push(0f, 0f, 55f * down, headX - 0.5f, headY - 1.5f);
            Antler(headX - 1.5f, headY - 1.5f, -1f, -0.3f);
            Antler(headX - 0.5f, headY - 1.8f, 1f, 0f);
            Pop();
            Pop();

            LegsSide(p, bodyY, far: false);
        }

        void LegsSide(Pose p, float bodyY, bool far)
        {
            float bias = far ? -0.3f : 0f;
            float[] hips = { 5.5f + p.Stretch, -8.5f };
            for (int i = 0; i < 2; i++)
            {
                float phase = (i == 0 ? 0f : 0.5f) + (far ? 0.5f : 0f);
                float swing, lift;
                if (p.Galloping)
                {
                    // Galope: as da frente esticam para a frente, as de trás para trás, juntas
                    float g = Sin01(p.Gallop + (i == 0 ? 0f : 0.25f));
                    swing = (i == 0 ? 3.5f : -3.5f) * (0.5f + 0.5f * g) - 1.5f * g;
                    lift = (1f - g) * 2.5f;
                }
                else
                {
                    swing = 2.5f * Sin01(p.Walk + phase);
                    lift = System.Math.Max(0f, Cos01(p.Walk + phase)) * 1.5f;
                }

                float hx = hips[i] + (far ? 1f : 0f), hy = bodyY + 2.5f;
                float footX = hx + swing, footY = -0.5f - lift;
                if (p.Fall > 0f)
                {
                    // Dobra as pernas embaixo do corpo
                    footX = hx + (i == 0 ? 3f : -3f) * p.Fall;
                    footY = Lerp(footY, bodyY + 3f, p.Fall);
                }

                float kneeX = (hx + footX) * 0.5f + (i == 0 ? 1.4f : -1.4f), kneeY = (hy + footY) * 0.5f;
                Cap(hx, hy, kneeX, kneeY, 1.5f, EnemyRamps.Hide, bias);
                Cap(kneeX, kneeY, footX, footY, 0.9f, EnemyRamps.Hide, bias - 0.05f);
                Ell(footX + 0.3f, footY - 0.2f, 1.2f, 0.8f, EnemyRamps.Shell, bias - 0.2f); // casco
            }
        }

        // Um chifre de galho: haste curva para cima com duas pontas, pontas acesas em ciano
        void Antler(float x, float y, float side, float bias)
        {
            Px wood = bias < 0f ? EnemyRamps.Antler[1] : EnemyRamps.Antler[3];
            Path(wood, 1.2f, x, y, x - 2f, y - 4f, x - 2.5f, y - 8f, x - 1f, y - 11.5f);
            Path(wood, 1f, x - 2f, y - 4f, x + 2f * side - 2.5f, y - 6.5f);
            Path(wood, 1f, x - 2.5f, y - 8f, x - 5.5f, y - 10f);
            Path(wood, 1f, x - 1.6f, y - 9.5f, x + 1.5f, y - 11f);
            EmitDot(x - 1f, y - 12f, EnemyRamps.CyanLight);
            EmitDot(x - 6f, y - 10.5f, EnemyRamps.Bio[3]);
            EmitDot(x + 2f, y - 11.5f, EnemyRamps.Bio[3]);
        }

        void Stripe(float x0, float y0, float x1, float y1, float glow)
        {
            EmitLine(x0, y0, x1, y1, glow > 0.7f ? EnemyRamps.Bio[4] : glow > 0.4f ? EnemyRamps.Bio[3] : EnemyRamps.Bio[2]);
        }

        // ---------- de frente e de costas ----------

        void FrontBack(Pose p, bool front)
        {
            float bodyY = -17f + p.Crouch + p.Fall * 10f;
            Push(0f, 0f, 20f * p.Fall, 0f, 0f);

            // Pernas finas (frente: as dianteiras; costas: as traseiras)
            for (int s = -1; s <= 1; s += 2)
            {
                float step = p.Galloping ? Sin01(p.Gallop) * 1.5f * s : Sin01(p.Walk + (s > 0 ? 0f : 0.5f)) * 1f;
                float footY = -0.5f - System.Math.Max(0f, step);
                if (p.Fall > 0f)
                    footY = Lerp(footY, bodyY + 3f, p.Fall);
                float x = s * 3.2f;
                Cap(x, bodyY + 2f, x + s * 0.3f, (bodyY + footY) * 0.5f, 1.4f, EnemyRamps.Hide, -0.05f);
                Cap(x + s * 0.3f, (bodyY + footY) * 0.5f, x, footY, 0.9f, EnemyRamps.Hide, -0.1f);
                Ell(x, footY - 0.2f, 1.2f, 0.8f, EnemyRamps.Shell, -0.2f);
            }

            float headY = bodyY - 8f + p.HeadDown * 5f + p.Idle * 0.4f;
            if (!front)
            {
                // De costas: a cabeça e a galhada atrás do corpo
                HeadFront(headY, p, back: true);
                Ell(0f, bodyY, 6.5f, 6f, EnemyRamps.Hide, -0.05f);
                Ell(0f, bodyY - 3f, 1.8f, 2.2f, EnemyRamps.Bone, -0.05f); // rabo claro
                Stripe(-4.5f, bodyY - 4f, -2f, bodyY - 5.5f, p.Glow);
                Stripe(4.5f, bodyY - 4f, 2f, bodyY - 5.5f, p.Glow);
            }
            else
            {
                Ell(0f, bodyY, 5.8f, 6.2f, EnemyRamps.Hide, 0.05f);
                Ell(0f, bodyY + 1.5f, 3f, 3.6f, EnemyRamps.Hide, 0.2f); // peito claro
                HeadFront(headY, p, back: false);
            }

            Pop();
        }

        void HeadFront(float headY, Pose p, bool back)
        {
            float bias = back ? -0.2f : 0f;
            Cap(0f, headY + 5f, 0f, headY + 1f, 2.2f, EnemyRamps.Hide, bias);
            // Galhada dos dois lados (inclina para a frente no aviso: fica mais baixa e mais aberta)
            float spread = 1f + p.HeadDown * 0.25f;
            for (int s = -1; s <= 1; s += 2)
            {
                Px wood = back ? EnemyRamps.Antler[2] : EnemyRamps.Antler[3];
                float x = s * 1.6f, y = headY - 2.2f + p.HeadDown * 1.5f;
                Path(wood, 1.2f, x, y, x + s * 3f * spread, y - 4f, x + s * 6f * spread, y - 8f, x + s * 8.5f * spread, y - 9f);
                Path(wood, 1f, x + s * 3f * spread, y - 4f, x + s * 2.5f * spread, y - 9f);
                Path(wood, 1f, x + s * 6f * spread, y - 8f, x + s * 9f * spread, y - 5.5f);
                EmitDot(x + s * 9f * spread, y - 9.5f, EnemyRamps.CyanLight);
                EmitDot(x + s * 2.5f * spread, y - 9.5f, EnemyRamps.Bio[3]);
                EmitDot(x + s * 9.5f * spread, y - 5f, EnemyRamps.Bio[3]);
                Ell(s * 3.6f, headY - 1f, 1.8f, 1f, EnemyRamps.Hide, bias - 0.1f); // orelhas
            }

            Ell(0f, headY, 3f, 3.3f, EnemyRamps.Hide, bias + 0.1f);
            if (!back)
            {
                Ell(0f, headY + 2.6f, 1.8f, 1.8f, EnemyRamps.Hide, 0.2f);
                Dot(0f, headY + 3.6f, EnemyRamps.Dark);
                EmitDot(-1.6f, headY - 0.8f, EnemyRamps.CyanLight);
                EmitDot(1.6f, headY - 0.8f, EnemyRamps.CyanLight);
            }
        }
    }
}
