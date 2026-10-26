using System;

namespace Icaro
{
    // Flor-canhão (o Espinhento): planta carnívora num caule grosso com raízes e folhas, e no alto um botão de
    // pétalas com espinhos. Fechada, balança devagar; ao atirar, as pétalas se abrem e mostram o miolo ciano de onde
    // sai o espinho. Ao levar dano, se fecha; ao morrer, o caule tomba e as pétalas murcham. A cabeça sempre olha
    // para o jogador, então as quatro direções usam a vista de frente.
    public sealed class FlorCanhaoArt : EnemyArt
    {
        public override string Key => "Espinhento";
        public override int Width => 34;
        public override int Height => 44;
        public override bool SameAllDirections => true;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Attack", 3, 8f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 5, 8f, false),
        };

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            float sway = 0f, open = 0.06f, bend = 0f, wilt = 0f, core = 0.4f;
            bool spike = false;
            switch (clip)
            {
                case "Idle":
                    sway = Sin01(t) * 1.2f;
                    open = 0.05f + 0.05f * (Sin01(t + 0.25f) * 0.5f + 0.5f);
                    break;
                case "Attack":
                    open = frame == 0 ? 0.6f : frame == 1 ? 1f : 0.55f;
                    core = 1f;
                    spike = frame == 1;
                    sway = frame == 1 ? -0.6f : 0f;
                    break;
                case "Hit":
                    open = 0f;
                    sway = frame == 0 ? 1.5f : -0.6f;
                    break;
                case "Death":
                    bend = Ease(t);
                    wilt = t;
                    open = 0.35f * t;
                    core = 0.4f * (1f - t);
                    break;
            }

            Roots();
            Cap(0f, -1f, sway * 0.3f, -11f, 2.8f, EnemyRamps.Stem, -0.05f);
            Leaf(-1f, -7f, -1f, wilt);
            Leaf(1f, -12f, 1f, wilt);

            // Parte de cima do caule e a cabeça tombam juntas ao morrer
            Push(-2f * bend, 0f, 55f * bend, sway * 0.3f, -11f);
            Cap(sway * 0.3f, -11f, sway, -23f, 2.4f, EnemyRamps.Stem, -wilt * 0.3f);
            for (int i = 0; i < 3; i++)
                Dot(sway * (0.4f + i * 0.2f) + (i % 2 == 0 ? -2.6f : 2.6f), -13f - i * 3.5f, EnemyRamps.Bone[3]);
            Head(sway, -29f, open, core, spike, wilt);
            Pop();
        }

        void Roots()
        {
            Cap(0f, -2f, -7f, -0.6f, 1.4f, EnemyRamps.Bark, -0.1f);
            Cap(0f, -2f, 6.5f, -0.5f, 1.4f, EnemyRamps.Bark, -0.15f);
            Cap(0f, -2f, -3f, -0.4f, 1.2f, EnemyRamps.Bark);
            Cap(0f, -2f, 3.5f, -0.4f, 1.2f, EnemyRamps.Bark, 0.05f);
            Ell(0f, -1.5f, 4.5f, 1.6f, EnemyRamps.Moss, -0.1f);
        }

        // Folha larga caindo para um lado (side = -1 esquerda, 1 direita)
        void Leaf(float x, float y, float side, float wilt)
        {
            float droop = 3f + wilt * 4f;
            Poly(EnemyRamps.Stem, 0.15f - wilt * 0.3f,
                x, y, x + side * 4f, y - 2.5f, x + side * 9f, y - 1f + droop * 0.4f, x + side * 10f, y + droop,
                x + side * 5f, y + 1.5f);
            Line(x, y, x + side * 9f, y + droop * 0.6f, EnemyRamps.Stem[1]);
        }

        // Botão de cinco pétalas com espinhos; 'open' afasta as pétalas e mostra o miolo ciano
        void Head(float hx, float hy, float open, float core, bool spike, float wilt)
        {
            // Cálice na base
            Ell(hx, hy + 5f, 4f, 2.2f, EnemyRamps.Stem, 0.05f);

            if (open > 0.15f)
            {
                EmitEll(hx, hy, 2.8f + open * 1.8f, 2.6f + open * 1.6f, EnemyRamps.Bio, core - 0.6f);
                Light(hx, hy, 4.5f, EnemyRamps.CyanLight, core * 0.6f);
            }

            // Pétalas de trás primeiro (as das pontas), depois as da frente
            int[] order = { -2, 2, -1, 1, 0 };
            foreach (int i in order)
            {
                if (open > 0.5f && i == 0)
                    continue; // aberta: a pétala da frente sai do caminho do miolo
                double angle = (i * (12.0 + open * 30.0) + (wilt > 0f ? i * 10.0 * wilt : 0.0)) * Math.PI / 180.0;
                float len = 10.5f - Math.Abs(i) * 0.8f;
                float bx = hx + i * (1.4f + open * 0.6f), by = hy + 4.2f;
                float dx = (float)Math.Sin(angle), dy = -(float)Math.Cos(angle);
                // Murchas: as pontas caem
                dy += wilt * 0.9f;
                float tx = bx + dx * len, ty = by + dy * len;
                float px = -dy, py = dx;
                float w = 2.6f + open * 0.4f;
                float bias = (i == 0 ? 0.1f : Math.Abs(i) == 2 ? -0.25f : 0f) - wilt * 0.35f;
                Poly(EnemyRamps.Petal, bias,
                    bx + px * w * 0.7f, by + py * w * 0.7f,
                    bx + dx * len * 0.5f + px * w, by + dy * len * 0.5f + py * w,
                    tx, ty,
                    bx + dx * len * 0.5f - px * w, by + dy * len * 0.5f - py * w,
                    bx - px * w * 0.7f, by - py * w * 0.7f);
                // Espinhos na borda e veio no meio
                Dot(bx + dx * len * 0.55f + px * (w + 0.8f), by + dy * len * 0.55f + py * (w + 0.8f), EnemyRamps.Bone[3]);
                Dot(bx + dx * len * 0.55f - px * (w + 0.8f), by + dy * len * 0.55f - py * (w + 0.8f), EnemyRamps.Bone[3]);
                Line(bx + dx * 2f, by + dy * 2f, bx + dx * len * 0.75f, by + dy * len * 0.75f, EnemyRamps.Petal[1]);
            }

            if (spike)
            {
                // O espinho saindo em direção ao jogador: ponta clara no meio do miolo
                EmitEll(hx, hy, 1.4f, 1.4f, EnemyRamps.Bone, 0.4f);
                EmitDot(hx, hy, PixelRamps.White);
            }
        }
    }
}
