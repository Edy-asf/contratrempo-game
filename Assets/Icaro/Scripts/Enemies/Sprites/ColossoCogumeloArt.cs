using System;
using static Icaro.PixelCanvas;

namespace Icaro
{
    // Colosso-cogumelo (o Rei Fungo, chefe do Bosque): criatura enorme de corpo de cogumelo, com pernas e braços de
    // raízes de micélio claras com veios ciano, um rosto escavado no caule (olhos e boca acesos), um chapéu largo
    // magenta com manchas claras e lamelas ciano por baixo, e cogumelos menores de chapéu âmbar crescendo em cima. No
    // aviso se agacha e o chapéu incha; no ataque solta nuvens de esporos pelas lamelas; ao morrer, desaba e o chapéu
    // tomba no chão. Os chefes sempre olham para o jogador: as quatro direções usam a vista de frente.
    public sealed class ColossoCogumeloArt : EnemyArt
    {
        public override string Key => "Chefe_ReiFungo";
        public override int Width => 168;
        public override int Height => 164;
        public override bool SameAllDirections => true;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 4, 5f, true),
            new EnemyClipSpec("Windup", 3, 6f, false),
            new EnemyClipSpec("Attack", 4, 10f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 7f, false),
        };

        struct Pose
        {
            public float Bob, Sway, Lift, Crouch, Puff, Gills, Spores, Tilt, Arms, Fall;
            public int Step;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            var p = new Pose { Gills = 0.45f, Step = -1 };
            switch (clip)
            {
                case "Idle":
                    p.Bob = Sin01(t) > 0f ? 0f : 1f;
                    p.Gills = 0.4f + 0.2f * Sin01(t);
                    break;
                case "Move":
                    p.Step = frame;
                    p.Sway = Sin01(t) * 2f;
                    p.Tilt = Sin01(t) * 2f;
                    p.Bob = frame % 2 == 0 ? 0f : 2f;
                    break;
                case "Windup":
                    p.Crouch = 6f * Ease(t);
                    p.Puff = Ease(t);
                    p.Gills = 0.5f + 0.5f * t;
                    p.Arms = t;
                    break;
                case "Attack":
                    p.Spores = t;
                    p.Gills = 1f;
                    p.Lift = frame % 2 == 0 ? 2f : 0f;
                    p.Puff = 0.4f;
                    p.Arms = 1f;
                    break;
                case "Hit":
                    p.Tilt = frame == 0 ? -5f : -2f;
                    p.Gills = 0.8f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    p.Gills = 0.45f * (1f - t);
                    break;
            }

            Legs(p);
            Push(p.Sway, p.Bob + p.Crouch + p.Fall * 40f, 0f, 0f, 0f);
            Body(p);
            Pop();

            // O chapéu tomba para o lado e cai no chão ao morrer
            Push(p.Sway * 1.5f + p.Fall * 3f, p.Bob + p.Crouch - p.Lift + p.Fall * 74f, p.Tilt + p.Fall * 13f, 0f, -100f);
            Cap(p);
            Pop();

            if (p.Spores > 0f)
                Spores(p.Spores);
        }

        // Pernas de raiz, afastadas, com dedos de raiz no chão; andando, um pé sobe de cada vez
        void Legs(Pose p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                bool lifted = p.Step >= 0 && p.Step % 2 == 1 && ((p.Step / 2) % 2 == 0 ? s < 0 : s > 0);
                float lift = lifted ? 4f : 0f;
                float fold = p.Fall * 20f + p.Crouch * 0.6f;
                float hipY = -42f + p.Crouch + p.Fall * 40f;
                float kneeX = s * (22f + fold * 0.4f), kneeY = -16f - lift + fold * 0.3f;
                Cap(s * 16f, hipY, kneeX, kneeY, 10f, EnemyRamps.Mycelium, -0.05f);
                Cap(kneeX, kneeY, s * 26f, -4f - lift, 8.5f, EnemyRamps.Mycelium, -0.1f);
                EmitLine(s * 17f, hipY + 3f, kneeX, kneeY + 2f, EnemyRamps.Bio[1]);
                for (int i = 0; i < 3; i++)
                    Cap(s * 26f, -3f - lift, s * (30f + i * 3f), -0.8f - lift * 0.5f + i * 0.2f, 2.2f,
                        EnemyRamps.Mycelium, -0.2f);
            }
        }

        // Caule com o rosto, o véu embaixo do chapéu e os braços de raiz
        void Body(Pose p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                // Braços: pendurados; erguem um pouco no aviso e no ataque
                float up = p.Arms * 14f;
                Cap(s * 22f, -74f, s * 36f, -46f - up, 5.5f, EnemyRamps.Mycelium, -0.1f);
                Cap(s * 36f, -46f - up, s * 41f, -32f - up * 1.3f, 5f, EnemyRamps.Mycelium, -0.05f);
                for (int i = -1; i <= 1; i++)
                    Cap(s * 41f, -32f - up * 1.3f, s * (42f + i * 3f), -24f - up * 1.3f, 1.8f, EnemyRamps.Mycelium, -0.2f);
                EmitLine(s * 24f, -71f, s * 35f, -48f - up, EnemyRamps.Bio[1]);
            }

            Ell(0f, -62f, 24f, 27f, EnemyRamps.Mycelium);
            Ell(0f, -86f, 29f, 6f, EnemyRamps.Mycelium, -0.15f); // véu (anel do cogumelo)
            // Fibras do caule e musgo subindo pela base
            Line(-20f, -45f, -14f, -72f, EnemyRamps.Mycelium[2]);
            Line(19f, -46f, 14f, -70f, EnemyRamps.Mycelium[2]);
            Line(-11f, -38f, -16f, -60f, EnemyRamps.Mycelium[2]);
            Line(12f, -37f, 17f, -58f, EnemyRamps.Mycelium[2]);
            Ell(-12f, -38f, 9f, 3.5f, EnemyRamps.Moss, -0.1f);
            Ell(13f, -40f, 6f, 2.5f, EnemyRamps.Moss, -0.15f);

            // Rosto escavado: olhos e boca escuros com o fundo aceso
            float glow = p.Gills;
            for (int s = -1; s <= 1; s += 2)
            {
                Ell(s * 9f, -70f, 4.5f, 5.5f, EnemyRamps.Robe, -0.6f);
                EmitEll(s * 9f, -69.5f, 2.2f, 2.8f, EnemyRamps.Bio, glow - 0.5f);
            }
            // Sobrancelhas franzidas de casca
            for (int s = -1; s <= 1; s += 2)
                Line(s * 4f, -74f, s * 14f, -78f, EnemyRamps.Mycelium[0], 2f);

            // Boca larga e torta, com dentes
            float open = 5f + p.Arms * 3f;
            Poly(EnemyRamps.Robe, -0.6f, -15f, -58f, -6f, -60f, 6f, -59f, 15f, -57f, 11f, -57f + open, -11f, -57f + open);
            EmitEll(0f, -57.5f + open * 0.5f, 8f, open * 0.35f, EnemyRamps.Bio, glow - 0.75f);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0)
                    continue;
                Dot(i * 3.5f, -58.5f, EnemyRamps.Bone[3]);
                Dot(i * 3.5f + 1f, -57f + open - 1f, EnemyRamps.Bone[2]);
            }
        }

        // Chapéu: domo magenta com manchas, lamelas ciano por baixo e cogumelos âmbar em cima
        void Cap(Pose p)
        {
            float rx = 72f + p.Puff * 5f, ry = 34f + p.Puff * 4f, cy = -108f;
            // Lamelas: a face de baixo do chapéu, escura, com riscos ciano saindo do centro
            Shape under = Where(Ellipse(0f, -94f, rx - 6f, 9f), (x, y) => true);
            Shape(under, EnemyRamps.Robe, -0.3f);
            for (int i = -9; i <= 9; i++)
            {
                float ex = i * (rx - 10f) / 9f;
                float ey = -94f + 8f * (float)Math.Sqrt(Math.Max(0.0, 1.0 - (ex / (rx - 6f)) * (ex / (rx - 6f))));
                EmitLine(i * 2f, -92f, ex, ey, p.Gills > 0.7f ? EnemyRamps.Bio[3] : EnemyRamps.Bio[2]);
            }

            Shape dome = Where(Ellipse(0f, cy, rx, ry), (x, y) => y <= -95f);
            Shape(dome, EnemyRamps.Petal, 0.05f);
            Line(-rx + 3f, -96f, rx - 3f, -96f, EnemyRamps.Petal[1]);

            // Manchas claras
            float[] spots = { -40f, -113f, 7f, 4f, -10f, -129f, 9f, 5f, 25f, -119f, 8f, 4.5f, 52f, -106f, 5f, 3f,
                -60f, -103f, 4f, 3f, 5f, -105f, 5f, 3f, -28f, -101f, 4f, 2.5f };
            for (int i = 0; i < spots.Length; i += 4)
                Ell(spots[i], spots[i + 1], spots[i + 2], spots[i + 3], EnemyRamps.Mycelium, 0.15f);

            // Cogumelos pequenos de chapéu âmbar aceso em cima
            float[] small = { -26f, 8f, 34f, -52f };
            for (int i = 0; i < small.Length; i++)
            {
                float x = small[i];
                float top = cy - ry * (float)Math.Sqrt(Math.Max(0.0, 1.0 - (x / rx) * (x / rx)));
                float h = i == 1 ? 10f : 7f;
                Cap(x, top + 2f, x + 1f, top - h, 1.8f, EnemyRamps.Mycelium, 0.05f);
                EmitEll(x + 1f, top - h - 1f, i == 1 ? 6f : 4.5f, i == 1 ? 3f : 2.2f, EnemyRamps.Glow, p.Gills - 0.6f);
            }
        }

        // Nuvem de esporos saindo das lamelas, abrindo em anéis
        void Spores(float t)
        {
            var random = new Random(7);
            for (int i = 0; i < 36; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                float r = 20f + (float)random.NextDouble() * 20f + t * 34f;
                float x = (float)Math.Cos(angle) * r * 1.2f, y = -86f + (float)Math.Sin(angle) * r * 0.45f + t * 6f;
                EmitDot(x, y, i % 3 == 0 ? EnemyRamps.Glow[3] : EnemyRamps.Bio[3]);
                if (i % 4 == 0)
                    EmitDot(x + 1f, y, EnemyRamps.Bio[2]);
            }
        }
    }
}
