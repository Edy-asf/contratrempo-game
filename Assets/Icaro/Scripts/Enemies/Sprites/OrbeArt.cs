using System;

namespace Icaro
{
    // Orbe (o Cascudo): um orbe de vidro sem rosto, com energia azul girando dentro, sobre quatro patas grossas
    // curvas como foices, presas ao vidro só por juntinhas. Montado em volume (SpriteVolume) e projetado nas 8
    // direções, a 24 px/m. No aviso agacha, o orbe sobe um tom e solta uma aura de pixels; no golpe salta, cai e
    // abre uma onda no chão. Ao morrer, o vidro racha e apaga, e as patas cedem.
    public sealed class OrbeArt : EnemyArt
    {
        public override string Key => "Cascudo";
        public override int Width => 64;
        public override int Height => 64;
        public override float PivotY => 5f;
        public override int Directions => 8;
        public override float PixelDensity => 24f;
        public override bool OwnOutline => true;

        // Câmera um pouco de cima (graus)
        const float k_Pitch = 12f;

        // Patas: ângulo em volta do corpo (0 = frente, positivo = lado direito) e o par da passada
        static readonly float[] k_LegAngle = { -50f, 50f, -140f, 140f };
        static readonly int[] k_LegPair = { 0, 1, 1, 0 };

        static readonly Px[] k_Shell = R(0x1e2234, 0x424e66, 0x76869c, 0xb2c0d0, 0xe8eef4);
        static readonly Px[] k_Orb = R(0x101e5c, 0x2246aa, 0x3e80e2, 0x78c4ff, 0xdcf6ff);
        static readonly Px k_Line = Px.Hex(0x0e101c);
        static readonly Px k_Accent = Px.Hex(0x78e6ec);
        static readonly Px k_White = Px.Hex(0xffffff);

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 6, 6f, true),
            new EnemyClipSpec("Move", 6, 12f, true),
            new EnemyClipSpec("Windup", 6, 9f, false),
            new EnemyClipSpec("Attack", 7, 11f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 4, 6f, false),
        };

        struct Pose
        {
            public float Phase, Crouch, Lift, Step, Glow, Open, Crack, Dead, Slam;
        }

        static Pose PoseFor(string clip, int f)
        {
            var p = new Pose();
            switch (clip)
            {
                case "Idle":
                    p.Phase = f / 6f;
                    p.Crouch = new[] { 0f, 0.5f, 1f, 1f, 0.5f, 0f }[f];
                    break;
                case "Move":
                    p.Phase = f / 6f;
                    p.Step = Sin01(f / 6f);
                    p.Crouch = Math.Abs(p.Step) * 0.8f;
                    break;
                case "Windup":
                    if (f < 4)
                    {
                        p.Phase = 0.25f * f;
                        p.Crouch = 1f + f;
                        p.Glow = 0.2f + f * 0.27f;
                        p.Open = f / 3f;
                    }
                    else
                    {
                        p.Phase = f == 4 ? 1f : 1.25f;
                        p.Crouch = 4f;
                        p.Glow = f == 4 ? 1f : 0.85f;
                        p.Open = 1f;
                    }

                    break;
                case "Attack":
                    p.Phase = new[] { 1.4f, 1.6f, 1.8f, 2f, 2.1f, 2.2f, 2.3f }[f];
                    p.Lift = new[] { 6f, 13f, 15f, 7f, 0f, 0f, 0f }[f];
                    p.Crouch = new[] { 0f, 0f, 0f, 0f, 5f, 3f, 1f }[f];
                    p.Glow = new[] { 1f, 1f, 1f, 1f, 0.7f, 0.4f, 0.15f }[f];
                    p.Open = new[] { 1f, 0.8f, 0.6f, 0.3f, 0f, 0f, 0f }[f];
                    p.Slam = new[] { 0f, 0f, 0f, 0f, 0.2f, 0.6f, 1f }[f];
                    break;
                case "Hit":
                    p.Phase = f * 0.1f;
                    p.Crouch = f == 0 ? 2f : 0f;
                    break;
                case "Death":
                    p.Crack = f == 0 ? 0.4f : 1f;
                    p.Crouch = f == 0 ? 2f : f == 1 ? 3f : 0f;
                    p.Dead = f < 2 ? 0f : f == 2 ? 0.8f : 1f;
                    break;
            }

            return p;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            Pose p = PoseFor(clip, frame);
            float yaw = view == EnemyView.Front ? 0f : view == EnemyView.FrontSide ? 45f : view == EnemyView.Side ? 90f
                : view == EnemyView.BackSide ? 135f : 180f;
            var v = new SpriteVolume(Width, Height, CellPivotX, CellPivotY, yaw, k_Pitch);

            // Altura das juntas, raio e centro do orbe (em pixels; y para cima)
            float joint = 17f + p.Lift - p.Crouch - p.Dead * 7f;
            float r = 14f + p.Glow * 0.6f;
            float oy = joint + 3f + p.Open * 1.5f;
            v.Sphere(1, 0f, oy, 0f, r);

            // Patas em foice: saem de baixo do orbe, arqueiam para fora e descem afinando até a ponta
            var near = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                double a = k_LegAngle[i] * Math.PI / 180.0;
                float dx = (float)Math.Sin(a), dz = (float)Math.Cos(a);
                float offs = (k_LegPair[i] == 0 ? 1f : -1f) * p.Step * 2f;
                float spread = 1f + p.Crouch * 0.06f + (p.Lift > 4f ? 0.25f : 0f);
                float hipR = 12f, hipH = joint - 2f;
                float kneeR = 21f * spread, kneeH = joint + 3f + (p.Lift > 4f ? 2f : 0f) - offs * 0.5f;
                float footR = 22f * spread, footH = 1f + p.Lift * 0.8f + Math.Max(0f, offs) * 0.8f;
                float stride = offs * 1.5f;
                if (p.Dead > 0.6f)
                {
                    kneeR = hipR + 6f;
                    kneeH = hipH;
                    footR = hipR + 10f;
                    footH = 1f;
                    stride = 0f;
                }

                float m1R = hipR + (kneeR - hipR) * 0.55f, m1H = hipH + 3.5f - offs * 0.3f;
                float m2R = kneeR + (footR - kneeR) * 0.45f + 0.8f, m2H = kneeH + (footH - kneeH) * 0.45f;
                v.Chain(2 + i, 4.4f, 1.1f,
                    dx * hipR, hipH, dz * hipR,
                    dx * m1R, m1H, dz * m1R + stride * 0.3f,
                    dx * kneeR, kneeH, dz * kneeR + stride * 0.6f,
                    dx * m2R, m2H, dz * m2R + stride * 0.8f,
                    dx * footR, footH, dz * footR + stride);
                v.Sphere(6 + i, dx * 12.5f, hipH, dz * 12.5f, 2.6f);
                v.Project(dx * hipR, hipH, dz * hipR, out _, out _, out float depth);
                near[i] = depth >= 0f;
            }

            v.Project(0f, oy, 0f, out float pcx, out float pcy, out _);
            v.Project(0f, oy - 1f, r * 0.45f, out float corex, out float corey, out _);
            float coreR = 2.2f + p.Glow * 1.5f;
            bool lit = p.Dead < 0.7f;
            int up = p.Glow > 0.65f && p.Dead < 0.5f ? 1 : 0;
            float spin = p.Phase * (float)(2.0 * Math.PI);

            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int id = v.IdAt(x, y);
                    if (id == 0)
                        continue;
                    float light = v.LightAt(x, y);
                    float X = x + 0.5f, Y = y + 0.5f;
                    Px col;
                    if (id == 1)
                    {
                        col = Tone(k_Orb, light - 0.05f + 0.25f * p.Glow, up);
                        float ang = (float)Math.Atan2(Y - pcy, X - pcx);
                        float rad = (float)Math.Sqrt((X - pcx) * (X - pcx) + (Y - pcy) * (Y - pcy)) / r;
                        float band = (float)Math.Sin(ang * 2f + rad * 5f - spin);
                        if (lit && band > 0.55f && rad < 0.85f)
                            col = k_Orb[Math.Min(4, 3 + up)];
                        if (lit && band > 0.9f && rad < 0.6f)
                            col = k_Orb[4];
                        if (lit && In(X, Y, corex, corey, coreR, coreR))
                            col = k_Orb[4];
                        if (In(X, Y, pcx - r * 0.42f, pcy - r * 0.48f, 2.6f, 1.6f))
                            col = lit ? k_White : k_Orb[2];
                        if (In(X, Y, pcx - r * 0.05f, pcy - r * 0.72f, 0.9f, 0.7f))
                            col = k_White;
                        if (p.Crack > 0f && Crack(X, Y, pcx, pcy, r, p.Crack))
                            col = k_Orb[0];
                        if (p.Dead >= 0.7f)
                            col = col.Scaled(0.55f);
                        if (p.Dead < 0.7f)
                            mask.Set(x, y, PixelRamps.White);
                    }
                    else if (id < 6)
                    {
                        col = near[id - 2]
                            ? Tone(k_Shell, light + 0.15f, 1)
                            : Tone(k_Shell, light - 0.1f, 0, 3);
                    }
                    else
                    {
                        col = Tone(k_Shell, light + 0.1f, 1);
                        int leg = id - 6;
                        double a = k_LegAngle[leg] * Math.PI / 180.0;
                        v.Project((float)Math.Sin(a) * 12.5f, joint - 2f, (float)Math.Cos(a) * 12.5f, out float jx, out float jy, out _);
                        if (lit && In(X, Y, jx, jy, 0.9f, 0.9f))
                        {
                            col = k_Accent;
                            mask.Set(x, y, PixelRamps.White);
                        }
                    }

                    if (v.IsSeam(x, y, 3f))
                        col = k_Line;
                    c.Set(x, y, col);
                }
            }

            c.Outline(EnemyRamps.Outline);

            // Aura de pixels em volta do orbe no aviso (sem contorno)
            if (p.Glow > 0.3f && p.Dead < 0.5f)
            {
                float ringR = r + 1.5f + p.Glow * 1.5f;
                int shift = (int)(p.Phase * 8f);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (c.Get(x, y).A > 0)
                            continue;
                        float X = x + 0.5f, Y = y + 0.5f;
                        float d = (float)Math.Sqrt((X - pcx) * (X - pcx) + (Y - pcy) * (Y - pcy));
                        bool ring = d > ringR - 0.9f && d <= ringR && (x + y + shift) % 3 != 0;
                        bool ring2 = p.Glow > 0.9f && d > ringR + 1.6f && d <= ringR + 2.4f && (x * 2 + y) % 5 == 0;
                        if (ring || ring2)
                        {
                            c.Set(x, y, ring && p.Glow > 0.9f ? k_Orb[4] : k_Orb[3]);
                            mask.Set(x, y, PixelRamps.White);
                        }
                    }
                }
            }

            // Onda no chão ao cair do salto, em pixels saltados, e lascas subindo
            if (p.Slam > 0f)
            {
                float sr = 14f + p.Slam * 12f;
                float sry = sr * (float)Math.Sin(k_Pitch * Math.PI / 180.0);
                float gx = CellPivotX, gy = CellPivotY;
                Px wave = p.Slam < 0.6f ? k_Orb[4] : k_Orb[3];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x += 2)
                    {
                        if (c.Get(x, y).A > 0)
                            continue;
                        float ex = (x + 0.5f - gx) / sr, ey = (y + 0.5f - gy) / sry;
                        float d = (float)Math.Sqrt(ex * ex + ey * ey);
                        if (d > 0.86f && d <= 1f)
                        {
                            c.Set(x, y, wave);
                            mask.Set(x, y, PixelRamps.White);
                        }
                    }
                }

                for (int k = -1; k <= 1; k += 2)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int X = (int)(gx + k * (sr * 0.7f + j * 2f)), Y = (int)(gy - 2f - j * 2f - p.Slam * 3f);
                        c.Set(X, Y, k_Orb[3]);
                        mask.Set(X, Y, PixelRamps.White);
                    }
                }
            }
        }

        // Tom da rampa pela luz, subindo 'up' degraus, só até o tom 'max'
        static Px Tone(Px[] ramp, float light, int up, int max = 4)
        {
            int i = 0;
            if (light > 0.15f) i++;
            if (light > 0.45f) i++;
            if (light > 0.75f) i++;
            if (light > 0.93f) i++;
            i = Math.Min(max, Math.Min(ramp.Length - 1, i + up));
            return ramp[i];
        }

        static bool In(float x, float y, float cx, float cy, float rx, float ry)
        {
            float ex = (x - cx) / rx, ey = (y - cy) / ry;
            return ex * ex + ey * ey <= 1f;
        }

        // Rachaduras do vidro (na tela, a partir do centro do orbe)
        static bool Crack(float x, float y, float cx, float cy, float r, float amount)
        {
            if (Near(x, y, cx - 2f, cy - r + 1f, cx + 1f, cy - 5f))
                return true;
            if (amount > 0.3f && Near(x, y, cx + 1f, cy - 5f, cx - 1f, cy - 1f))
                return true;
            if (amount > 0.7f && Near(x, y, cx - 1f, cy - 1f, cx + 3f, cy + 3f))
                return true;
            return amount > 0.5f && Near(x, y, cx + 1f, cy - 5f, cx + 5f, cy - 7f);
        }

        static bool Near(float x, float y, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = ((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float ex = x - (x0 + dx * t), ey = y - (y0 + dy * t);
            return ex * ex + ey * ey <= 0.3f;
        }

        static Px[] R(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
