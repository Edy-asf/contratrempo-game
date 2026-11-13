using System;
using System.Collections.Generic;

namespace Icaro
{
    // Golem de pedras (pedido do usuário em 2026-11-13): braços longos e grandes, pernas curtas, anda apoiado nos
    // punhos como um gorila. Montado em volume (SpriteVolume) com pedras angulosas (caixas chanfradas, faces chapadas
    // e bisel), desgastadas (rachaduras com lábio claro, furinhos, líquen, musgo nas faces de cima) e trepadeiras;
    // núcleo ciano num encaixe em losango no peito, fenda ciano na cabeça e runas gastas nos antebraços. 8 direções a
    // 15 px/m. Mirando (sempre de frente), o núcleo cresce e os veios acendem (Windup); no disparo só o clarão no núcleo (o feixe é o
    // laser do GolemAI); para a pancada ergue os punhos (Windup2) e bate no chão com uma onda (Attack2). Ao morrer,
    // as pedras desabam e o núcleo apaga.
    public sealed class GolemPedraArt : EnemyArt
    {
        public override string Key => "Golem";
        public override int Width => 120;
        public override int Height => 96;
        public override float PivotY => 10f;
        public override int Directions => 8;
        public override float PixelDensity => 15f;
        public override bool OwnOutline => true;

        // Altura do núcleo acima do chão, parado e com a inclinação de 6° (m), e quanto fica à frente: a origem do laser
        // no prefab
        public const float CoreHeight = 28.5f / 15f;
        public const float CoreForward = 15.5f / 15f;

        const float k_Pitch = 10f;

        static readonly Px[] k_Stone = R(0x14161e, 0x2a2e3a, 0x424856, 0x5c6270, 0x7a808c, 0x9ea4ac, 0xc4c8cc);
        static readonly Px[] k_Moss = R(0x1a3422, 0x2c5832, 0x48823e, 0x7aac54);
        static readonly Px[] k_Cyan = R(0x0e3c4a, 0x18788c, 0x3cc4d2, 0x96f0f4, 0xecffff);
        static readonly Px k_Lichen = Px.Hex(0xa8ac78);
        static readonly Px k_SocketColor = Px.Hex(0x0e1018);

        // Peças de detalhe (depois das pedras)
        const int k_Socket = 60, k_Crystal = 61, k_Slit = 62, k_Rune = 63, k_Vein = 64, k_Vine = 65, k_Leaf = 66,
            k_Leaf2 = 67;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 8, 6f, true),
            new EnemyClipSpec("Windup", 3, 6f, false),
            new EnemyClipSpec("Attack", 2, 12f, true),
            new EnemyClipSpec("Windup2", 3, 6f, false),
            new EnemyClipSpec("Attack2", 3, 10f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 8f, false),
        };

        struct Pose
        {
            public float Up, Slam, Step, Glow, Fire, Bob, Lean, Crumble, Wave;
        }

        // Uma peça montada: caixa ou tubo, com o tom, o musgo e as rachaduras (pedras)
        sealed class Part
        {
            public int Id;
            public bool Tube;
            public float X, Y, Z, Ha, Hb, Hc;
            public float[] Axes;
            public float X1, Y1, Z1, R0, R1;
            public int Tone;
            public float Moss;
            public int Cracks;
        }

        readonly List<Part> m_Parts = new List<Part>();
        readonly float[] m_Moss = new float[80];
        readonly int[] m_Tone = new int[80];
        readonly float[][] m_CrackPlanes = new float[80][];
        int m_NextStone;

        static Pose PoseFor(string clip, int f, float t)
        {
            var p = new Pose { Glow = 0.45f };
            switch (clip)
            {
                case "Idle":
                    p.Bob = new[] { 0f, 0.6f, 1f, 0.6f }[f];
                    p.Glow = 0.4f + 0.1f * f % 2;
                    break;
                case "Move":
                    p.Step = Sin01(t);
                    p.Bob = Math.Abs(Cos01(t)) * 1.5f;
                    break;
                case "Windup":
                    p.Glow = 0.35f + 0.3f * f;
                    p.Lean = 1.5f + 1.25f * f;
                    break;
                case "Attack":
                    p.Glow = 1f;
                    p.Fire = f == 0 ? 1f : 0.5f;
                    p.Lean = 4f;
                    p.Bob = f == 0 ? 1f : 0f;
                    break;
                case "Windup2":
                    p.Up = 0.35f + 0.325f * f;
                    break;
                case "Attack2":
                    p.Slam = 1f;
                    p.Wave = 0.35f + 0.325f * f;
                    break;
                case "Hit":
                    p.Lean = f == 0 ? -4f : -1.5f;
                    p.Glow = 0.9f;
                    break;
                case "Death":
                    p.Crumble = Ease(t);
                    p.Glow = 0.4f * (1f - t);
                    break;
            }

            return p;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            Pose p = PoseFor(clip, frame, t);
            // Carregando e disparando o laser ele está sempre de frente para o jogador (o GolemAI vira o corpo antes):
            // essas vistas usam a de frente em todas as direções
            if (clip == "Windup" || clip == "Attack")
                view = EnemyView.Front;
            m_Parts.Clear();
            m_NextStone = 1;
            Build(p, out float coreX, out float coreY, out float coreZ);

            float yaw = view == EnemyView.Front ? 0f : view == EnemyView.FrontSide ? 45f : view == EnemyView.Side ? 90f
                : view == EnemyView.BackSide ? 135f : 180f;
            var v = new SpriteVolume(Width, Height, CellPivotX, CellPivotY, yaw, k_Pitch);
            foreach (Part part in m_Parts)
            {
                if (part.Tube)
                    v.Segment(part.Id, part.X, part.Y, part.Z, part.R0, part.X1, part.Y1, part.Z1, part.R1);
                else
                    v.Box(part.Id, part.X, part.Y, part.Z, part.Axes, part.Ha, part.Hb, part.Hc, 1f, 2.4f);
            }

            Shade(v, p);
            Canvas.Outline(EnemyRamps.Outline);

            if (p.Fire > 0f)
                Flash(v, p, coreX, coreY, coreZ);
            if (p.Wave > 0f)
                Wave(p.Wave);
        }

        // ---------- montagem ----------

        void Build(Pose p, out float coreX, out float coreY, out float coreZ)
        {
            float sway = p.Step * 1.2f;
            float drop = 4f * p.Slam - p.Up * 1.5f + p.Bob;
            float hip = 14f - drop;

            // Pernas curtas: coxa e pé em lajes, arrastando
            for (int k = -1; k <= 1; k += 2)
            {
                float lift = Math.Max(0f, p.Step * k) * 2f;
                Stone(k * 8f + sway, hip - 5f + lift * 0.5f, 0f, 4.5f, 5.25f, 4.5f, 0f, 0f, -k * 4f, 2, 0.2f, 0);
                Stone(k * 9.5f + sway, 2.7f + lift, 1f + p.Step * k * 1.5f, 5.6f, 2.7f, 6.5f, k * 3f, 0f, 0f, 2, 0.2f, 0);
            }

            // Tronco: barriga, peito, corcova, dois peitorais
            float ty = hip + 19f;
            Stone(sway, hip + 4f, 0f, 10.5f, 6f, 9.5f, 0f, 0f, 2f, 2, 0.2f, 1);
            Stone(sway, ty + 4f, -10f, 12.5f, 9f, 9f, 0f, 22f, 0f, 3, 0.6f, 1);
            Stone(sway, ty - 3f, 1.5f, 14.25f, 9.75f, 10.5f, 0f, 0f, -2f, 3, 0.3f, 2);
            for (int k = -1; k <= 1; k += 2)
                Stone(sway + k * 8f, ty + 6f, 2.5f, 8.25f, 5.25f, 8.5f, 0f, -6f, -k * 7f, 3, 0.6f, 0);

            // Núcleo: encaixe escuro em losango e o cristal
            coreX = sway;
            coreY = ty - 3f;
            coreZ = 12.4f;
            Detail(k_Socket, sway, ty - 3f, 11.6f, 3.6f, 3.6f, 0.7f, 0f, 0f, 45f);
            float s = 2.4f + p.Glow * 1.6f;
            Detail(k_Crystal, sway, ty - 3f, 12.4f, s * 0.55f, s * 0.55f, 0.9f, 0f, 0f, 45f);
            if (p.Glow > 0.5f && p.Crumble <= 0f)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    DetailAlong(k_Vein, sway + k * 4.5f, ty - 3f, 11.65f, sway + k * 8f, ty - 1.5f, 11.65f, 0.4f, 0.3f);
                    DetailAlong(k_Vein, sway + k * 8f, ty - 1.5f, 11.65f, sway + k * 11.5f, ty - 4f, 11.65f, 0.4f, 0.3f);
                }
            }

            // Cabeça pequena e angulosa, afundada entre os ombros, com a fenda
            float hy = ty + 13f - p.Up * 1.5f;
            Stone(sway, hy, 5f, 6f, 4.9f, 5.5f, 0f, 0f, -3f, 3, 0.6f, 1);
            Detail(k_Slit, sway, hy - 1f, 10.7f, 3.5f, 0.5f, 0.4f, 0f, 0f, 0f);

            // Braços longos: ombro, braço, antebraço e o punho com três dedos
            for (int k = -1; k <= 1; k += 2)
            {
                float sx = sway + k * 24f, sy = ty + 5f;
                Stone(sx, sy, 1f, 8.25f, 7.1f, 8f, 0f, 0f, -k * 10f, 3, 0.6f, 1);
                float sw = p.Step * k, spread = p.Slam * 7f;
                float edx = sway + k * (32f + spread * 0.5f), edy = sy - 17f + p.Slam * 3f, edz = 10f + sw * 3f;
                float fdx = sway + k * (31f + spread), fdy = 7.5f + Math.Max(0f, sw) * 4f, fdz = 19f + sw * 6f;
                float eux = sway + k * 31f, euy = ty + 17f, euz = 5f;
                float fux = sway + k * 9f, fuy = ty + 32f, fuz = 6f;
                float u = p.Up;
                float ex = Lerp(edx, eux, u), ey = Lerp(edy, euy, u), ez = Lerp(edz, euz, u);
                float fx = Lerp(fdx, fux, u), fy = Lerp(fdy, fuy, u), fz = Lerp(fdz, fuz, u);

                StoneAlong(sx, sy - 3f, 0.5f, ex, ey, ez, 2.5f, 4.5f, 4.5f, 2, 0.3f, 0);
                StoneAlong(ex, ey, ez, fx, fy, fz, 0f, 6.75f, 6.75f, 3, 0.3f, 2);

                // Runas gastas no antebraço, do lado de fora
                for (int r = 0; r < 2; r++)
                {
                    float rt = r == 0 ? 0.3f : 0.55f;
                    Detail(k_Rune, Lerp(ex, fx, rt) + k * 6.9f, Lerp(ey, fy, rt), Lerp(ez, fz, rt), 0.35f, 1.3f, 1f,
                        0f, 0f, 0f);
                }

                Stone(fx, fy, fz, 9.4f, 6.75f, 7.5f, 0f, 0f, -k * 5f, 3, 0.3f, 1);
                for (int j = -1; j <= 1; j++)
                {
                    if (u < 0.5f)
                        Stone(fx + j * 5.7f, fy - 3.6f, fz + 6.6f, 2.6f, 2.5f, 2.6f, 0f, 0f, j * 4f, 2, 0.2f, 0);
                    else
                        Stone(fx + j * 5.7f, fy + 5.6f, fz + 1f, 2.6f, 2.5f, 2.6f, 0f, 0f, j * 4f, 2, 0.2f, 0);
                }

                // Trepadeira do alto do ombro descendo pelo braço
                if (p.Crumble <= 0f)
                {
                    float ox = k * 8.8f;
                    Vine(100 + k, sx - k * 3f, sy + 7.4f, 1f, sx + k * 3f, sy + 6.5f, 8.9f, ex + k * 5f, ey + 2f, ez + 4.8f,
                        Lerp(ex, fx, 0.45f) + ox * 0.85f, Lerp(ey, fy, 0.45f), Lerp(ez, fz, 0.45f) + 2f);
                }
            }

            if (p.Crumble <= 0f)
            {
                Vine(110, sway + 6f, ty + 8f, 11.2f, sway + 9f, ty, 11.8f, sway + 8f, ty - 7f, 11.8f, sway + 10f, ty - 13f,
                    10.1f);
                Vine(111, sway - 12f, ty + 9f, 9.7f, sway - 13.5f, ty + 1f, 11.8f, sway - 12.5f, ty - 6f, 11.8f);
                Vine(112, sway - 2f, hy + 5.1f, 5.5f, sway - 5f, hy + 2f, 10.8f, sway - 6f, hy - 2.5f, 10.8f);
            }

            ApplyLean(p.Lean + 6f);
            if (p.Crumble > 0f)
                ApplyCrumble(p.Crumble);
            Rotate(p.Lean + 6f, ref coreY, ref coreZ);
        }

        // Pedra (caixa chanfrada): centro, meias medidas, giro (graus), tom base, musgo (0 a 1) e rachaduras
        void Stone(float x, float y, float z, float ha, float hb, float hc, float yaw, float pitch, float roll,
            int tone, float moss, int cracks)
        {
            int id = m_NextStone++;
            m_Tone[id] = tone;
            m_Moss[id] = moss;
            m_CrackPlanes[id] = CrackPlanes(id, cracks, ha, hb, hc);
            m_Parts.Add(new Part
            {
                Id = id, X = x, Y = y, Z = z, Ha = ha, Hb = hb, Hc = hc,
                Axes = SpriteVolume.Axes(yaw + Jitter(id, 1) * 4f, pitch + Jitter(id, 2) * 4f, roll + Jitter(id, 3) * 4f),
                Tone = tone,
            });
        }

        // Pedra deitada de um ponto a outro (passa 'extra' das pontas)
        void StoneAlong(float x0, float y0, float z0, float x1, float y1, float z1, float extra, float hb, float hc,
            int tone, float moss, int cracks)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            int id = m_NextStone++;
            m_Tone[id] = tone;
            m_Moss[id] = moss;
            float ha = len * 0.5f + extra;
            m_CrackPlanes[id] = CrackPlanes(id, cracks, ha, hb, hc);
            m_Parts.Add(new Part
            {
                Id = id, X = (x0 + x1) * 0.5f, Y = (y0 + y1) * 0.5f, Z = (z0 + z1) * 0.5f, Ha = ha, Hb = hb, Hc = hc,
                Axes = SpriteVolume.AxesAlong(dx, dy, dz, Jitter(id, 4) * 8f), Tone = tone,
            });
        }

        void Detail(int id, float x, float y, float z, float ha, float hb, float hc, float yaw, float pitch, float roll)
        {
            m_Parts.Add(new Part
            {
                Id = id, X = x, Y = y, Z = z, Ha = ha, Hb = hb, Hc = hc, Axes = SpriteVolume.Axes(yaw, pitch, roll),
            });
        }

        void DetailAlong(int id, float x0, float y0, float z0, float x1, float y1, float z1, float hb, float hc)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            m_Parts.Add(new Part
            {
                Id = id, X = (x0 + x1) * 0.5f, Y = (y0 + y1) * 0.5f, Z = (z0 + z1) * 0.5f, Ha = len * 0.5f, Hb = hb,
                Hc = hc, Axes = SpriteVolume.AxesAlong(dx, dy, dz),
            });
        }

        // Trepadeira: caule fino pelos pontos e folhas alternadas a cada ~3 px
        void Vine(int seed, params float[] xyz)
        {
            int n = xyz.Length / 3;
            int leaf = 0;
            for (int i = 1; i < n; i++)
            {
                float x0 = xyz[i * 3 - 3], y0 = xyz[i * 3 - 2], z0 = xyz[i * 3 - 1];
                float x1 = xyz[i * 3], y1 = xyz[i * 3 + 1], z1 = xyz[i * 3 + 2];
                m_Parts.Add(new Part { Id = k_Vine, Tube = true, X = x0, Y = y0, Z = z0, X1 = x1, Y1 = y1, Z1 = z1, R0 = 0.55f, R1 = 0.55f });
                float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
                float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                int count = Math.Max(1, (int)(len / 3f));
                for (int j = 0; j < count; j++, leaf++)
                {
                    float tt = (j + 0.5f) / count;
                    float side = leaf % 2 == 0 ? 1f : -1f;
                    // de lado ao caule, na tela de frente e de lado
                    float px = x0 + dx * tt + side * 1.1f * (Math.Abs(dy) > Math.Abs(dx) ? 1f : 0.3f);
                    float py = y0 + dy * tt + side * 1.1f * (Math.Abs(dy) > Math.Abs(dx) ? 0.2f : 1f);
                    float pz = z0 + dz * tt + 0.2f;
                    bool light = Hash(seed, leaf, 0, 7) < 0.5f;
                    m_Parts.Add(new Part
                    {
                        Id = light ? k_Leaf2 : k_Leaf, Tube = true, X = px, Y = py, Z = pz, X1 = px + side * 0.6f, Y1 = py,
                        Z1 = pz, R0 = 0.75f, R1 = 0.6f,
                    });
                }
            }
        }

        // Inclinação do corpo inteiro para a frente (positivo) ou para trás, em volta do pé
        void ApplyLean(float degrees)
        {
            if (degrees == 0f)
                return;
            foreach (Part part in m_Parts)
            {
                Rotate(degrees, ref part.Y, ref part.Z);
                if (part.Tube)
                    Rotate(degrees, ref part.Y1, ref part.Z1);
                else
                    for (int c = 0; c < 3; c++)
                        Rotate(degrees, ref part.Axes[c * 3 + 1], ref part.Axes[c * 3 + 2]);
            }
        }

        static void Rotate(float degrees, ref float y, ref float z)
        {
            double a = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float ny = y * c - z * s, nz = y * s + z * c;
            y = ny;
            z = nz;
        }

        // Desmoronar: cada pedra cai até o chão, se afasta do meio e tomba; as de baixo caem primeiro
        void ApplyCrumble(float amount)
        {
            foreach (Part part in m_Parts)
            {
                if (part.Tube)
                    continue;
                float delay = Math.Min(0.35f, part.Y / 80f * 0.35f);
                float f = Ease(Clamp01((amount - delay) / (1f - delay)));
                float rest = Math.Min(part.Ha, Math.Min(part.Hb, part.Hc)) + Hash(part.Id, 5, 0, 1) * 3f;
                part.Y = Lerp(part.Y, rest, f);
                float out_ = 4f + Math.Abs(part.X) * 0.3f;
                part.X += Math.Sign(part.X) * out_ * f;
                part.Z += (part.Z * 0.3f + Jitter(part.Id, 6) * 5f) * f;
                float[] spin = SpriteVolume.Axes(Jitter(part.Id, 7) * 30f * f, Jitter(part.Id, 8) * 40f * f,
                    Jitter(part.Id, 9) * 50f * f);
                part.Axes = Mul(spin, part.Axes);
            }
        }

        static float[] Mul(float[] a, float[] b)
        {
            // a e b em colunas: (a * b) coluna c = a * (coluna c de b)
            var r = new float[9];
            for (int c = 0; c < 3; c++)
            {
                float x = b[c * 3], y = b[c * 3 + 1], z = b[c * 3 + 2];
                r[c * 3] = a[0] * x + a[3] * y + a[6] * z;
                r[c * 3 + 1] = a[1] * x + a[4] * y + a[7] * z;
                r[c * 3 + 2] = a[2] * x + a[5] * y + a[8] * z;
            }

            return r;
        }

        // Rachaduras: planos sorteados que cortam a pedra perto de um ponto; o risco é onde o plano cruza a superfície
        static float[] CrackPlanes(int id, int count, float ha, float hb, float hc)
        {
            var planes = new float[count * 7];
            for (int i = 0; i < count; i++)
            {
                float nx = Jitter(id, 20 + i), ny = Jitter(id, 30 + i), nz = Jitter(id, 40 + i);
                float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz) + 1e-4f;
                planes[i * 7] = nx / l;
                planes[i * 7 + 1] = ny / l;
                planes[i * 7 + 2] = nz / l;
                planes[i * 7 + 3] = Jitter(id, 50 + i) * ha * 0.6f;
                planes[i * 7 + 4] = Jitter(id, 60 + i) * hb * 0.6f;
                planes[i * 7 + 5] = hc * 0.7f * (Jitter(id, 70 + i) > -0.3f ? 1f : -1f);
                planes[i * 7 + 6] = 3.5f + Hash(id, 80 + i, 0, 3) * 4f;
            }

            return planes;
        }

        // ---------- cor ----------

        void Shade(SpriteVolume v, Pose p)
        {
            var ignore = new bool[80];
            for (int i = k_Crystal; i <= k_Leaf2; i++)
                ignore[i] = true;
            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            bool lit = p.Crumble < 0.5f;
            var moss = new bool[Width * Height];
            var crack = new bool[Width * Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int id = v.IdAt(x, y);
                    if (id == 0)
                        continue;
                    float light = v.LightAt(x, y);
                    Px col;
                    bool glow = false;
                    switch (id)
                    {
                        case k_Socket:
                            col = p.Glow > 0.5f && lit ? k_Cyan[1] : k_SocketColor;
                            break;
                        case k_Crystal:
                            col = !lit ? k_Cyan[0] : light > 0.3f || p.Glow >= 0.5f ? k_Cyan[p.Glow < 0.5f ? 3 : 4]
                                : k_Cyan[p.Glow < 0.5f ? 2 : 3];
                            glow = lit;
                            break;
                        case k_Slit:
                            col = lit ? k_Cyan[p.Glow < 0.5f ? 3 : 4] : k_Cyan[0];
                            glow = lit;
                            break;
                        case k_Rune:
                        case k_Vein:
                            col = lit ? k_Cyan[p.Glow < 0.5f ? 2 : 3] : k_Stone[2];
                            glow = lit;
                            break;
                        case k_Vine:
                            col = k_Moss[0];
                            break;
                        case k_Leaf:
                            col = light > 0.3f ? k_Moss[2] : k_Moss[1];
                            break;
                        case k_Leaf2:
                            col = light > 0.3f ? k_Moss[3] : k_Moss[2];
                            break;
                        default:
                            col = StoneColor(v, x, y, id, light, moss, crack);
                            break;
                    }

                    if (id < k_Socket && v.IsSeam(x, y, 0f, ignore))
                        col = k_Stone[0];
                    c.Set(x, y, col);
                    if (glow)
                        mask.Set(x, y, PixelRamps.White);
                }
            }

            // Lábio claro em cima das rachaduras e musgo escorrendo um pixel abaixo dos tufos
            for (int y = Height - 2; y >= 1; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    int id = v.IdAt(x, y);
                    if (id == 0 || id >= k_Socket)
                        continue;
                    if (crack[i] && !crack[i - Width] && v.IdAt(x, y - 1) == id && Hash(x, y, id, 11) < 0.55f)
                        c.Set(x, y - 1, k_Stone[Math.Min(6, m_Tone[id] + 2)]);
                    if (moss[i] && !moss[i + Width] && v.IdAt(x, y + 1) == id && v.UpAt(x, y + 1) < 0.5f &&
                        Hash(x, y, id, 12) < 0.55f)
                        c.Set(x, y + 1, k_Moss[1]);
                }
            }
        }

        Px StoneColor(SpriteVolume v, int x, int y, int id, float light, bool[] moss, bool[] crack)
        {
            int tone = m_Tone[id];
            int off = light > 0.8f ? 2 : light > 0.4f ? 1 : light > -0.05f ? 0 : light > -0.45f ? -1 : -2;
            int i = Math.Max(1, Math.Min(6, tone + off));
            v.LocalAt(x, y, out float lx, out float ly, out float lz);
            int qx = (int)Math.Floor(lx), qy = (int)Math.Floor(ly), qz = (int)Math.Floor(lz);
            float h = Hash(qx * 3 + qz, qy, id, 1);

            float[] planes = m_CrackPlanes[id];
            for (int k = 0; planes != null && k < planes.Length; k += 7)
            {
                float ex = lx - planes[k + 3], ey = ly - planes[k + 4], ez = lz - planes[k + 5];
                float d = ex * planes[k] + ey * planes[k + 1] + ez * planes[k + 2];
                if (Math.Abs(d) < 0.55f && ex * ex + ey * ey + ez * ez < planes[k + 6] * planes[k + 6])
                {
                    crack[y * Width + x] = true;
                    return h < m_Moss[id] * 0.35f ? k_Moss[1] : k_Stone[Math.Max(0, tone - 2)];
                }
            }

            if (v.UpAt(x, y) > 0.55f && Hash(qx * 3 + qz, qy, id, 2) < m_Moss[id] * 0.65f)
            {
                moss[y * Width + x] = true;
                return Hash(qx, qy * 5 + qz, id, 3) < 0.6f ? k_Moss[2] : k_Moss[3];
            }

            if (h < 0.035f)
                return k_Stone[Math.Max(0, i - 1)];
            if (h > 0.985f)
                return k_Lichen;
            return k_Stone[i];
        }

        // ---------- efeitos ----------

        // Clarão do disparo no núcleo (só se o núcleo estiver à vista): cristal branco, estrela de raios curtos e um
        // anel tracejado; o feixe é o laser do jogo
        void Flash(SpriteVolume v, Pose p, float cx, float cy, float cz)
        {
            bool visible = false;
            for (int i = 0; i < Width * Height && !visible; i++)
                visible = v.IdAt(i % Width, i / Width) == k_Crystal;
            if (!visible)
                return;
            v.Project(cx, cy, cz, out float sx, out float sy, out _);
            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            float s = 2.4f + p.Glow * 1.6f;
            float rays = 4f + p.Fire * 4f;
            int[] dirs = { 1, 0, -1, 0, 0, 1, 0, -1, 1, 1, -1, 1, 1, -1, -1, -1 };
            for (int d = 0; d < 8; d++)
            {
                float n = d < 4 ? rays : rays * 0.5f;
                for (int i = 0; i < (int)n; i++)
                {
                    float r = s * 1.2f + i;
                    int X = (int)(sx + dirs[d * 2] * r), Y = (int)(sy + dirs[d * 2 + 1] * r);
                    c.Set(X, Y, i < n * 0.5f ? k_Cyan[4] : k_Cyan[3]);
                    mask.Set(X, Y, PixelRamps.White);
                }
            }

            float ring = 6f + p.Fire * 4f;
            for (int i = 0; i < 24; i++)
            {
                if (i % 3 == 2)
                    continue;
                double a = i / 24.0 * Math.PI * 2.0;
                int X = (int)(sx + Math.Cos(a) * ring), Y = (int)(sy + Math.Sin(a) * ring);
                c.Set(X, Y, k_Cyan[3]);
                mask.Set(X, Y, PixelRamps.White);
            }
        }

        // Onda da pancada no chão, em pixels saltados, e lascas de pedra subindo
        void Wave(float amount)
        {
            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            float sr = 28f + amount * 20f;
            float sry = sr * (float)Math.Sin(k_Pitch * Math.PI / 180.0);
            float gx = CellPivotX, gy = CellPivotY;
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
                        c.Set(x, y, k_Cyan[3]);
                        mask.Set(x, y, PixelRamps.White);
                    }
                }
            }

            for (int k = -1; k <= 1; k += 2)
            {
                for (int j = 0; j < 5; j++)
                {
                    int X = (int)(gx + k * (sr * 0.62f + j * 2.5f)), Y = (int)(gy - 4f - j * 2f);
                    if (c.Get(X, Y).A == 0)
                        c.Set(X, Y, k_Stone[j % 2 == 0 ? 3 : 4]);
                }
            }
        }

        // ---------- utilidades ----------

        static float Hash(int a, int b, int c, int salt)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791) ^ (uint)(salt * 2654435761u);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        // -1 a 1, fixo por peça
        static float Jitter(int id, int salt) => Hash(id, salt, 17, 5) * 2f - 1f;

        static Px[] R(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
