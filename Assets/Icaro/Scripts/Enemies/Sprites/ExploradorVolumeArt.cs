using System;
using System.Collections.Generic;

namespace Icaro
{
    // Explorador Tomado (o Vagante) em 8 direções (2026-11-13; o ExploradorArt de 4 direções ficou sem uso), montado em
    // volume (SpriteVolume) sobre o conceito aprovado pelo usuário: robô explorador de uma missão anterior, tomado pela
    // mata. Capacete redondo com o visor ciano grande e rachado, antena partida e um cogumelo âmbar saindo da
    // rachadura; placas do peito com juntas e rebites, cinto de pano com fivela de latão; ombreiras com musgo e
    // cogumelinhos; o braço direito virou um canhão comprido com aros, uma faixa ciano e a boca acesa, pendendo ao lado
    // do corpo; o outro braço caído com a mão; joelheiras, botas com musgo, mochila atrás e trepadeiras descendo pelo
    // peito e pelo braço. Ferrugem e musgo em manchas presas a cada peça. Sombreado como o conceito: rampas de 6 tons,
    // textura e pontilhado, juntas escuras onde as peças se encostam e a beira de cima e da esquerda acesa. No aviso
    // ergue o canhão para a frente e a boca e a faixa acendem; na rajada, coice e clarão. Morteiro: o cogumelo acende e
    // cresce com a cabeça para trás (Windup2) e solta a nuvem de esporos num tranco (Attack2). Ao morrer, os joelhos
    // cedem e ele tomba de lado.
    public sealed class ExploradorVolumeArt : EnemyArt
    {
        public override string Key => "Vagante";
        public override int Width => 72;
        public override int Height => 64;
        public override float PivotY => 3f;
        public override int Directions => 8;
        public override float PixelDensity => 24f;

        const float k_Pitch = 12f;
        const float k_Ppm = 24f;

        // Pontos para o prefab (m, a partir do pé, no espaço local do Unity: x para a direita dele, que de frente fica à
        // esquerda da imagem): o pivô da mira (ombro do canhão), a boca do canhão erguido a
        // partir dele e de onde sai a bola de esporos (o chapéu do cogumelo)
        public static readonly float[] AimPivot = { 11f / k_Ppm, 31f / k_Ppm, 0f };
        public static readonly float[] MuzzleFromPivot = { 2.8f / k_Ppm, -1f / k_Ppm, 22f / k_Ppm };
        public static readonly float[] MortarPoint = { -4.6f / k_Ppm, 45f / k_Ppm, 4.8f / k_Ppm };

        // Rampas do conceito
        static readonly Px[] k_Metal = R(0x121019, 0x222330, 0x353a49, 0x4f5766, 0x717c89, 0x9fa9b1);
        static readonly Px[] k_Rust = R(0x26120e, 0x452015, 0x67351f, 0x8b502b, 0xb0723d);
        static readonly Px[] k_Moss = R(0x0b1c16, 0x143223, 0x204d2f, 0x316a3a, 0x4d8a47, 0x7aae5a);
        static readonly Px[] k_Bio = R(0x0b3644, 0x136e7c, 0x20a6b0, 0x5cdfe4, 0xc6fffa);
        static readonly Px[] k_Amber = R(0x572405, 0x984a0b, 0xdc861c, 0xffc24a, 0xffefb6);
        static readonly Px[] k_Bone = R(0x2e2a22, 0x4f4838, 0x7a7056, 0xa69b78, 0xcfc6a0, 0xf0ead0);
        static readonly Px[] k_Cloth = R(0x1c1414, 0x332322, 0x4d3532, 0x6a4b44, 0x8c665a);
        static readonly Px[] k_Brass = R(0x372210, 0x63401a, 0x966a29, 0xc49842, 0xe9c978);
        static readonly Px[] k_Dark = R(0x07060b, 0x0f0c14, 0x1a1520);
        static readonly Px[][] k_Ramps = { k_Metal, k_Rust, k_Moss, k_Bio, k_Amber, k_Bone, k_Cloth, k_Brass, k_Dark };
        const int r_Metal = 0, r_Rust = 1, r_Moss = 2, r_Bio = 3, r_Amber = 4, r_Bone = 5, r_Cloth = 6, r_Brass = 7,
            r_Dark = 8;

        static readonly int[] k_Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static readonly float[] k_Ident = SpriteVolume.Axes(0f, 0f, 0f);

        // Material de cada peça
        enum Mat : byte
        {
            Metal, Chest, Belly, Cannon, Boot, Pack, Helmet, Cloth, Brass, Visor, Strip, MuzzleRim, MuzzleCore, Stem,
            Cap, Vine, Leaf, Spore
        }

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 8, 9f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 2, 12f, true),
            new EnemyClipSpec("Windup2", 3, 6f, false),
            new EnemyClipSpec("Attack2", 3, 10f, false),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 6, 9f, false),
        };

        struct Pose
        {
            public float Walk, Bob, Aim, Charge, Recoil, Flash, Lean, Fall, Shroom, Puff, HeadBack;
            public bool Walking;
        }

        sealed class Part
        {
            public int Id;
            public bool Box;
            public float X, Y, Z, R0, X1, Y1, Z1, R1, Ha, Hb, Hc;
            public float[] Axes;
            public float Ax, Ay, Az; // âncora das manchas (acompanha a peça)
        }

        readonly List<Part> m_Parts = new List<Part>();
        readonly Mat[] m_Mat = new Mat[256];
        readonly float[] m_Bias = new float[256];
        readonly float[] m_MossW = new float[256];
        int m_Next;
        bool m_Alive;
        Pose m_Pose;

        static Pose PoseFor(string clip, int f, float t)
        {
            var p = new Pose { Shroom = 0.3f };
            switch (clip)
            {
                case "Idle":
                    p.Bob = f == 1 || f == 2 ? -0.6f : 0f;
                    break;
                case "Move":
                    p.Walking = true;
                    p.Walk = t;
                    p.Bob = -Math.Abs(Sin01(t * 2f)) * 0.8f;
                    break;
                case "Windup":
                    p.Aim = Ease(Math.Min(1f, t * 1.6f));
                    p.Charge = 0.3f + 0.7f * t;
                    break;
                case "Attack":
                    p.Aim = 1f;
                    p.Charge = 0.5f;
                    p.Recoil = f == 0 ? 1f : 0f;
                    p.Flash = f == 0 ? 1f : 0f;
                    break;
                case "Windup2":
                    p.Shroom = 0.4f + 0.6f * t;
                    p.HeadBack = 12f * Ease(t);
                    p.Bob = -0.5f * t;
                    break;
                case "Attack2":
                    p.Shroom = f == 0 ? 1f : 0.6f - 0.15f * f;
                    p.Puff = f == 0 ? 0.4f : f == 1 ? 1f : 0.6f;
                    p.HeadBack = f == 0 ? -9f : f == 1 ? -3f : 0f;
                    break;
                case "Hit":
                    p.Lean = f == 0 ? -8f : -3f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    p.Shroom = 0.3f * (1f - t);
                    break;
            }

            return p;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            m_Pose = PoseFor(clip, frame, t);
            m_Alive = m_Pose.Fall < 0.6f;
            m_Parts.Clear();
            m_Next = 1;
            Build(m_Pose);

            float yaw = view == EnemyView.Front ? 0f : view == EnemyView.FrontSide ? 45f : view == EnemyView.Side ? 90f
                : view == EnemyView.BackSide ? 135f : 180f;
            var v = new SpriteVolume(Width, Height, CellPivotX, CellPivotY, yaw, k_Pitch);
            // Montado como o conceito visto de frente e espelhado aqui: o canhão fica à esquerda da imagem de frente e
            // do lado da câmera na vista de lado
            foreach (Part part in m_Parts)
            {
                part.X = -part.X;
                part.X1 = -part.X1;
                part.Ax = -part.Ax;
                if (part.Box)
                    for (int k = 0; k < 3; k++)
                        part.Axes[k * 3] = -part.Axes[k * 3];
            }

            foreach (Part part in m_Parts)
            {
                if (part.Box)
                    v.Box(part.Id, part.X, part.Y, part.Z, part.Axes, part.Ha, part.Hb, part.Hc, 0.7f, 1.6f);
                else
                    v.Segment(part.Id, part.X, part.Y, part.Z, part.R0, part.X1, part.Y1, part.Z1, part.R1);
            }

            Shade(v);
        }

        // ---------- montagem (pixels: y para cima a partir do chão, z para a frente) ----------

        void Build(Pose p)
        {
            float bob = p.Bob;
            const Mat metal = Mat.Metal;

            // Pernas: bota, canela, joelheira e coxa
            for (int k = -1; k <= 1; k += 2)
            {
                float phase = p.Walk + (k < 0 ? 0f : 0.5f);
                float swing = p.Walking ? 4f * Sin01(phase) : 0f;
                float lift = p.Walking ? Math.Max(0f, Cos01(phase)) * 2.2f : 0f;
                float fx = k * 5f, fy = 2f + lift, fz = swing;
                float hipY = 16.5f + bob;
                float kx = k * 5.1f, ky = 10f + bob * 0.5f + lift * 0.6f, kz = swing * 0.5f + 0.6f + lift * 0.4f;
                Box(Mat.Boot, -0.1f, 0f, fx, fy, fz + 0.9f, k_Ident, 3.7f, 2f, 3.4f);
                Seg(metal, 0f, 0.1f, fx, fy + 1.5f, fz, 2.2f, kx, ky, kz, 2.5f);
                Seg(metal, 0f, 0.1f, kx, ky, kz, 2.6f, k * 5f, hipY, 0f, 2.9f);
                Seg(metal, 0.12f, 0.1f, kx, ky + 0.2f, kz + 1.3f, 2.3f, kx, ky + 0.2f, kz + 1.3f, 2.3f);
            }

            // Mochila atrás, com musgo em cima e trepadeiras
            float ty = bob;
            Box(Mat.Pack, -0.15f, 1f, 0f, 26f + ty, -7.6f, k_Ident, 10.2f, 7.4f, 3.4f);
            Seg(metal, -0.1f, 1f, -10.6f, 26f + ty, -6.4f, 1.1f, -10.6f, 20f + ty, -6.4f, 1.1f);
            Vine(1, -4f, 33.4f + ty, -8.5f, -4.6f, 27f + ty, -9.6f, -3.8f, 21f + ty, -9.6f, -4.4f, 16f + ty, -9f);
            Vine(2, 5f, 33.4f + ty, -8.5f, 5.4f, 26f + ty, -9.6f, 6.2f, 21.5f + ty, -9.5f);

            // Cinto de pano e fivela, barriga, placa do peito e gola
            Box(Mat.Cloth, 0f, 0f, 0f, 16.6f + ty, 0.2f, k_Ident, 7.6f, 1.6f, 4.2f);
            Box(Mat.Brass, 0f, 0f, 0f, 16.6f + ty, 4.4f, k_Ident, 2.1f, 1.5f, 0.45f);
            Box(Mat.Belly, 0f, 0.15f, 0f, 20.5f + ty, 0.4f, k_Ident, 7f, 3f, 3.9f);
            Box(Mat.Chest, 0f, 0.4f, 0f, 27.5f + ty, 0.4f, SpriteVolume.Axes(0f, 4f, 0f), 9.6f, 4.8f, 5f);
            Box(metal, -0.25f, 0.6f, 0f, 32.8f + ty, 0.3f, k_Ident, 5.8f, 1.4f, 3.6f);

            // Trepadeiras pelo peito e pelo braço caído
            Vine(3, -9f, 33.5f + ty, 2.5f, -7.2f, 29f + ty, 5.1f, -8.6f, 24f + ty, 4.7f, -11.6f, 21f + ty, 3f,
                -13.2f, 14f + ty, 2.6f, -12.8f, 10.5f + ty, 2.6f);
            Vine(4, 9f, 33.4f + ty, 3f, 5.4f, 29.5f + ty, 5.2f, 3.2f, 24.5f + ty, 4.9f);

            // Braço esquerdo, caído, com a mão
            float armSwing = p.Walking ? -2.5f * Sin01(p.Walk) : 0f;
            float lx = -11f, ly = 31f + ty;
            Seg(metal, -0.05f, 0.35f, lx - 1f, ly - 2f, 0.5f, 2.3f, lx - 2f, ly - 12f, 0.8f + armSwing, 2f);
            Seg(metal, -0.1f, 0.2f, lx - 2.2f, ly - 14.5f, 1f + armSwing, 2.4f, lx - 2.2f, ly - 14.5f,
                1f + armSwing, 2.4f);
            Seg(metal, -0.2f, 0f, lx - 1.5f, ly - 16f, 1.6f + armSwing, 0.65f, lx - 1.5f, ly - 18f, 1.9f + armSwing,
                0.6f);
            Seg(metal, -0.2f, 0f, lx - 3f, ly - 16f, 1.4f + armSwing, 0.65f, lx - 3.4f, ly - 18f, 1.6f + armSwing,
                0.6f);
            Seg(metal, 0.05f, 1f, lx, ly, 0.2f, 4f, lx, ly, 0.2f, 3.6f); // ombreira

            // Braço-canhão pendendo ao lado do corpo; ergue para a frente para mirar
            float sx = 11f, sy = 31f + ty, sz = 0f;
            int cannon = m_Parts.Count;
            Seg(metal, 0f, 0.35f, sx + 1f, sy - 2f, sz, 2.4f, sx + 2f, sy - 8.5f, sz + 0.4f, 2.4f);
            float c0x = sx + 2.6f, c0y = sy - 9f, c0z = sz + 0.5f, c1x = sx + 3.4f, c1y = sy - 21.2f, c1z = sz + 3.2f;
            Seg(Mat.Cannon, 0.08f, 0.3f, c0x, c0y, c0z, 3.5f, c1x, c1y, c1z, 3.3f);
            // Faixa ciano no lado de dentro do canhão
            Seg(Mat.Strip, 0f, 0f, Lerp(c0x, c1x, 0.3f) - 2.1f, Lerp(c0y, c1y, 0.3f), Lerp(c0z, c1z, 0.3f) + 1.7f,
                1.15f, Lerp(c0x, c1x, 0.75f) - 2f, Lerp(c0y, c1y, 0.75f), Lerp(c0z, c1z, 0.75f) + 1.6f, 1.1f);
            // Boca: aro escuro e o miolo aceso
            float ux = c1x - c0x, uy = c1y - c0y, uz = c1z - c0z, ul = (float)Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            Seg(Mat.MuzzleRim, 0f, 0f, c1x + ux * 0.6f, c1y + uy * 0.6f, c1z + uz * 0.6f, 2.6f, c1x + ux * 1.6f,
                c1y + uy * 1.6f, c1z + uz * 1.6f, 2.5f);
            Seg(Mat.MuzzleCore, 0f, 0f, c1x + ux * 1.9f, c1y + uy * 1.9f, c1z + uz * 1.9f, 1.5f, c1x + ux * 1.9f,
                c1y + uy * 1.9f, c1z + uz * 1.9f, 1.5f);
            int cannonEnd = m_Parts.Count;
            Seg(metal, 0.05f, 1f, sx, sy, 0.2f, 4.3f, sx, sy, 0.2f, 3.8f); // ombreira
            // Cogumelinhos na ombreira
            Seg(Mat.Cap, 0.1f, 0f, sx + 0.3f, sy + 3.9f, 0.6f, 1.1f, sx + 2.6f, sy + 3.5f, 0.6f, 1f);
            Seg(Mat.Cap, -0.05f, 0f, sx - 2f, sy + 4.2f, -0.6f, 0.8f, sx - 0.6f, sy + 4.4f, -0.6f, 0.8f);

            if (p.Aim > 0f)
                RotateX(cannon, cannonEnd, -86f * p.Aim, sx, sy, sz);
            if (p.Recoil > 0f)
                Move(cannon, cannonEnd, 0f, 0f, -1.6f * p.Recoil);
            if (p.Walking && p.Aim <= 0f)
                RotateX(cannon, cannonEnd, 6f * Sin01(p.Walk), sx, sy, sz);

            // Cabeça: capacete, visor rachado, antena partida e o cogumelo
            int head = m_Parts.Count;
            float hy = 38.6f + ty;
            Seg(Mat.Helmet, 0.05f, 0.5f, 0f, hy, 0.4f, 6.3f, 0f, hy, 0.4f, 6.3f);
            Seg(metal, 0.1f, 0f, 6f, hy + 4.2f, -1f, 0.55f, 8.2f, hy + 8.8f, -1.2f, 0.5f);
            Seg(metal, 0.1f, 0f, 8.2f, hy + 8.8f, -1.2f, 0.5f, 10.6f, hy + 7.8f, -1.2f, 0.45f);
            float grow = 1f + p.Shroom * 0.3f + p.Puff * 0.15f, squash = 1f - p.Puff * 0.3f;
            Seg(Mat.Stem, 0f, 0f, -2.2f, hy + 1.6f, 5.3f, 0.6f, -3.6f, hy + 4.6f, 5f, 0.55f);
            Seg(Mat.Cap, 0f, 0f, -4.6f - 2.4f * grow, hy + 5.1f, 4.8f, 1.6f * grow * squash, -4.6f + 2.4f * grow,
                hy + 5.1f, 4.8f, 1.6f * grow * squash);
            Seg(Mat.Cap, 0.1f, 0f, -4.6f, hy + 5.6f + 0.6f * (grow - 1f), 4.8f, 1.7f * grow, -4.6f,
                hy + 5.6f + 0.6f * (grow - 1f), 4.8f, 1.7f * grow);
            Seg(Mat.Cap, -0.05f, 0f, -8.5f, hy + 2.4f, 4f, 0.95f, -6.6f, hy + 2.4f, 4.2f, 0.95f);
            if (p.Puff > 0f)
            {
                for (int j = 0; j < 7; j++)
                {
                    double a = j * 0.9;
                    float r = 2.2f + p.Puff * 4f + (j % 2) * 1.2f;
                    float px = -4.6f + (float)Math.Cos(a) * r, py = hy + 8f + p.Puff * 3.5f + (j % 3) * 1.4f;
                    float pz = 4.8f + (float)Math.Sin(a) * r * 0.6f;
                    Seg(Mat.Spore, 0f, 0f, px, py, pz, 0.75f, px, py, pz, 0.75f);
                }
            }

            if (p.HeadBack != 0f)
                RotateX(head, m_Parts.Count, -p.HeadBack, 0f, 33f + ty, 0.3f);
            if (p.Lean != 0f)
                RotateX(0, m_Parts.Count, p.Lean, 0f, 0f, 0f);
            if (p.Fall > 0f)
                Fall(p.Fall);
        }

        int NewId(Mat mat, float bias, float moss)
        {
            int id = m_Next++;
            m_Mat[id] = mat;
            m_Bias[id] = bias;
            m_MossW[id] = moss;
            return id;
        }

        void Box(Mat mat, float bias, float moss, float x, float y, float z, float[] axes, float ha, float hb, float hc)
        {
            m_Parts.Add(new Part
            {
                Id = NewId(mat, bias, moss), Box = true, X = x, Y = y, Z = z, Axes = (float[])axes.Clone(), Ha = ha,
                Hb = hb, Hc = hc, Ax = x, Ay = y, Az = z
            });
        }

        void Seg(Mat mat, float bias, float moss, float x0, float y0, float z0, float r0, float x1, float y1, float z1,
            float r1)
        {
            m_Parts.Add(new Part
            {
                Id = NewId(mat, bias, moss), X = x0, Y = y0, Z = z0, R0 = r0, X1 = x1, Y1 = y1, Z1 = z1, R1 = r1,
                Ax = x0, Ay = y0, Az = z0
            });
        }

        // Trepadeira: caule fino e folhas alternadas
        void Vine(int seed, params float[] xyz)
        {
            int n = xyz.Length / 3, leaf = seed * 10;
            for (int i = 1; i < n; i++)
            {
                float x0 = xyz[i * 3 - 3], y0 = xyz[i * 3 - 2], z0 = xyz[i * 3 - 1];
                float x1 = xyz[i * 3], y1 = xyz[i * 3 + 1], z1 = xyz[i * 3 + 2];
                Seg(Mat.Vine, 0f, 0f, x0, y0, z0, 0.55f, x1, y1, z1, 0.55f);
                for (int j = 0; j < 2; j++, leaf++)
                {
                    float t = j == 0 ? 0.3f : 0.75f, s = Hash(leaf, seed, 7, 1) < 0.5f ? 1f : -1f;
                    float px = Lerp(x0, x1, t) + s * 1.3f, py = Lerp(y0, y1, t), pz = Lerp(z0, z1, t) + 0.3f;
                    Seg(Mat.Leaf, 0f, 0f, px - s * 0.5f, py + 0.3f, pz, 1.15f, px + s * 0.7f, py - 0.4f, pz, 0.85f);
                }
            }
        }

        void Move(int from, int to, float dx, float dy, float dz)
        {
            for (int i = from; i < to; i++)
            {
                Part q = m_Parts[i];
                q.X += dx;
                q.Y += dy;
                q.Z += dz;
                q.X1 += dx;
                q.Y1 += dy;
                q.Z1 += dz;
                q.Ax += dx;
                q.Ay += dy;
                q.Az += dz;
            }
        }

        // Positivo = inclina para a frente (o alto vai para +z; o que pende vai para trás)
        void RotateX(int from, int to, float degrees, float px, float py, float pz)
        {
            if (degrees == 0f)
                return;
            double a = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            for (int i = from; i < to; i++)
            {
                Part q = m_Parts[i];
                RotYZ(c, s, py, pz, ref q.Y, ref q.Z);
                RotYZ(c, s, py, pz, ref q.Ay, ref q.Az);
                if (q.Box)
                    for (int k = 0; k < 3; k++)
                        RotYZ(c, s, 0f, 0f, ref q.Axes[k * 3 + 1], ref q.Axes[k * 3 + 2]);
                else
                    RotYZ(c, s, py, pz, ref q.Y1, ref q.Z1);
            }
        }

        static void RotYZ(float c, float s, float py, float pz, ref float y, ref float z)
        {
            float dy = y - py, dz = z - pz;
            y = py + dy * c - dz * s;
            z = pz + dy * s + dz * c;
        }

        // Os joelhos cedem e ele tomba de lado, assentando no chão; o corpo caído volta para o meio da célula
        void Fall(float f)
        {
            float sink = Math.Min(1f, f * 2f) * 5f;
            foreach (Part q in m_Parts)
            {
                if (q.Y > 10f)
                    q.Y -= sink;
                if (!q.Box && q.Y1 > 10f)
                    q.Y1 -= sink;
                if (q.Ay > 10f)
                    q.Ay -= sink;
            }

            float tilt = Math.Max(0f, f * 1.25f - 0.25f);
            double a = -84.0 * tilt * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float low = float.MaxValue;
            foreach (Part q in m_Parts)
            {
                RotXY(c, s, ref q.X, ref q.Y);
                RotXY(c, s, ref q.Ax, ref q.Ay);
                if (q.Box)
                {
                    for (int k = 0; k < 3; k++)
                        RotXY(c, s, ref q.Axes[k * 3], ref q.Axes[k * 3 + 1]);
                    low = Math.Min(low, q.Y - Math.Max(q.Ha, Math.Max(q.Hb, q.Hc)) * 0.8f);
                }
                else
                {
                    RotXY(c, s, ref q.X1, ref q.Y1);
                    low = Math.Min(low, Math.Min(q.Y - q.R0, q.Y1 - q.R1));
                }
            }

            float lift = Math.Max(0f, -low) + 0.2f;
            float shift = -20f * (float)Math.Sin(-a);
            foreach (Part q in m_Parts)
            {
                q.Y += lift;
                q.X += shift;
                q.Ay += lift;
                q.Ax += shift;
                if (!q.Box)
                {
                    q.Y1 += lift;
                    q.X1 += shift;
                }
            }
        }

        static void RotXY(float c, float s, ref float x, ref float y)
        {
            float nx = x * c - y * s, ny = x * s + y * c;
            x = nx;
            y = ny;
        }

        // ---------- cor ----------

        void Shade(SpriteVolume v)
        {
            int n = Width * Height;
            var lvl = new int[n];
            var rid = new int[n];
            var glow = new bool[n];
            for (int i = 0; i < n; i++)
                lvl[i] = -1;

            var anchor = new float[256 * 3];
            foreach (Part q in m_Parts)
            {
                anchor[q.Id * 3] = q.Ax;
                anchor[q.Id * 3 + 1] = q.Ay;
                anchor[q.Id * 3 + 2] = q.Az;
            }

            var ignore = new bool[256];
            for (int i = 1; i < m_Next; i++)
                ignore[i] = m_Mat[i] == Mat.Vine || m_Mat[i] == Mat.Leaf || m_Mat[i] == Mat.Spore ||
                            m_Mat[i] == Mat.Strip;
            Pose p = m_Pose;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int id = v.IdAt(x, y);
                    if (id == 0)
                        continue;
                    int i = y * Width + x;
                    Mat mat = m_Mat[id];
                    float light = v.LightAt(x, y);
                    float bay = (k_Bayer[(y & 3) * 4 + (x & 3)] / 16f - 0.47f) * 0.5f;
                    v.WorldAt(x, y, out float wx, out float wy, out float wz);
                    float ox = wx - anchor[id * 3], oy = wy - anchor[id * 3 + 1], oz = wz - anchor[id * 3 + 2];
                    float val = 0.64f + (light - 0.45f) * 1.15f + m_Bias[id];
                    int ramp = r_Metal;
                    bool g = false;

                    // Visor: a frente do capacete
                    if (mat == Mat.Helmet && oz > 2f && oy > -3.6f && oy < 1.8f)
                        mat = Math.Abs(oy + 3.6f) < 0.6f || Math.Abs(oy - 1.8f) < 0.6f || oz < 2.7f ? Mat.MuzzleRim
                            : Mat.Visor;
                    switch (mat)
                    {
                        case Mat.Visor:
                        {
                            float lx = ox, ly = oy + 1f;
                            // Rachadura em zigue-zague do alto até o cogumelo
                            float cx = ly > 0f ? -2f + (2.5f - ly) * 0.4f : -1f + ly * 0.8f;
                            if (Math.Abs(lx - cx) < 0.55f)
                            {
                                ramp = r_Dark;
                                val = 0.2f;
                            }
                            else
                            {
                                ramp = r_Bio;
                                val = m_Alive ? 0.62f + (ly > 1.4f ? 0.25f : 0f) + light * 0.12f : 0.05f;
                                g = m_Alive;
                            }

                            break;
                        }
                        case Mat.Strip:
                            ramp = r_Bio;
                            val = !m_Alive ? 0.05f : p.Flash > 0f || p.Charge > 0.6f ? 0.9f : 0.55f + p.Charge * 0.3f;
                            g = m_Alive;
                            break;
                        case Mat.MuzzleRim:
                            ramp = r_Dark;
                            val = 0.3f + light * 0.3f;
                            break;
                        case Mat.MuzzleCore:
                            ramp = r_Bio;
                            val = !m_Alive ? 0.05f : p.Flash > 0f ? 1.2f : 0.5f + p.Charge * 0.5f;
                            g = m_Alive;
                            break;
                        case Mat.Stem:
                            ramp = r_Bone;
                            val += 0.1f;
                            break;
                        case Mat.Cap:
                            ramp = r_Amber;
                            val = m_Alive ? val * 0.6f + 0.15f + p.Shroom * 0.35f : 0.05f;
                            g = m_Alive;
                            break;
                        case Mat.Spore:
                            ramp = (x + y) % 3 == 0 ? r_Moss : r_Amber;
                            val = 0.85f;
                            g = true;
                            break;
                        case Mat.Vine:
                            ramp = r_Moss;
                            val = 0.5f + light * 0.2f;
                            break;
                        case Mat.Leaf:
                            ramp = r_Moss;
                            val += 0.2f;
                            break;
                        case Mat.Cloth:
                            ramp = r_Cloth;
                            val += (Noise(ox, oy * 3f, oz, 2f, id) - 0.5f) * 0.3f;
                            break;
                        case Mat.Brass:
                            ramp = r_Brass;
                            break;
                        default:
                        {
                            val += (Noise(ox, oy, oz, 2f, id + 31) - 0.5f) * 0.24f;
                            // Musgo nas faces de cima (mais nas peças marcadas) e ferrugem em manchas
                            float up = v.UpAt(x, y), w = m_MossW[id];
                            float moss = Noise(ox, oy, oz, 2.6f, id + 7);
                            bool mossy = up > 0.72f - 0.32f * w && moss > 0.64f - 0.16f * w;
                            if (mat == Mat.Boot && wy < 1.4f && moss > 0.45f)
                                mossy = true;
                            float rust = Noise(ox, oy, oz, 3.2f, id + 13);
                            if (mossy)
                            {
                                ramp = r_Moss;
                                val = 0.35f + light * 0.45f + (Noise(ox, oy, oz, 1.2f, id) - 0.5f) * 0.4f;
                            }
                            else if (rust > (mat == Mat.Helmet ? 0.82f : 0.73f))
                            {
                                ramp = r_Rust;
                                val += (Noise(ox, oy, oz, 1.4f, id + 3) - 0.5f) * 0.35f;
                            }

                            if (ramp == r_Metal)
                            {
                                float d = Details(v, x, y, mat);
                                if (d > 5f)
                                {
                                    ramp = r_Bio;
                                    val = 0.2f + light * 0.1f;
                                }
                                else
                                    val += d;
                            }
                            break;
                        }
                    }

                    int count = k_Ramps[ramp].Length;
                    int l = (int)Math.Floor(val * (count - 1) + 0.5f + (g ? 0f : bay));
                    if (!g && !ignore[id] && v.IsSeam(x, y, 1.2f, ignore))
                    {
                        l--;
                        if (v.IdAt(x, y + 1) != id)
                            l--;
                    }

                    lvl[i] = Math.Max(0, Math.Min(count - 1, l));
                    rid[i] = ramp;
                    glow[i] = g;
                }
            }

            // Beira de cima e da esquerda acesa (como o conceito)
            PixelCanvas canvas = Canvas, mask = GlowMask;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (lvl[i] < 0)
                        continue;
                    int l = lvl[i];
                    bool edge = y == 0 || lvl[i - Width] < 0 || x == 0 || lvl[i - 1] < 0;
                    if (edge && !glow[i])
                        l = Math.Min(k_Ramps[rid[i]].Length - 1, l + 1);
                    canvas.Set(x, y, k_Ramps[rid[i]][l]);
                    if (glow[i])
                        mask.Set(x, y, PixelRamps.White);
                }
            }

            if (p.Flash > 0f && m_Alive)
                Flash(v);
        }

        // Juntas, rebites e arranhão das placas do peito e da barriga; aros do canhão
        static float Details(SpriteVolume v, int x, int y, Mat mat)
        {
            v.LocalAt(x, y, out float lx, out float ly, out float lz);
            switch (mat)
            {
                case Mat.Chest:
                {
                    if (lz < 4.4f)
                        return 0f;
                    if (Near(lx, ly, 7f, 3f) || Near(lx, ly, -7f, 3f) || Near(lx, ly, 2.2f, -0.3f) ||
                        Near(lx, ly, -2.2f, -0.3f))
                        return 0.6f;
                    if (Math.Abs(ly + 2.4f) < 0.5f)
                        return -0.4f;
                    if (ly < -2.9f && ly > -3.9f)
                        return 0.18f;
                    if (Math.Abs(lx) < 0.5f && ly > -2.4f)
                        return -0.4f;
                    if (Math.Abs(ly - (lx - 3f) * 0.45f - 1f) < 0.4f && lx > 3f && lx < 7f)
                        return -0.2f;
                    return 0f;
                }
                case Mat.Belly:
                {
                    if (lz < 3.3f)
                        return 0f;
                    if (Near(lx, ly, 5.6f, -1.6f) || Near(lx, ly, -5.6f, -1.6f))
                        return 0.6f;
                    if (lx > -6f && lx < -2f && ly > 0.9f && ly < 2.7f)
                        return 9f; // luz apagada
                    if (Math.Abs(ly - 0.4f) < 0.45f)
                        return -0.3f;
                    return 0f;
                }
                case Mat.Pack:
                {
                    // Placas de trás com juntas, rebites e a grade de ventilação
                    if (lz > -2.6f)
                        return 0f;
                    if (Math.Abs(ly - 1.5f) < 0.45f || Math.Abs(lx) < 0.45f && ly < 1.5f)
                        return -0.4f;
                    if (Math.Abs(ly - 0.9f) < 0.35f)
                        return 0.15f;
                    if (Near(lx, ly, 8.5f, 5.6f) || Near(lx, ly, -8.5f, 5.6f) || Near(lx, ly, 8.5f, -5.6f) ||
                        Near(lx, ly, -8.5f, -5.6f))
                        return 0.6f;
                    if (lx > 2f && lx < 7.5f && ly < -1.5f && ly > -5f && ((int)Math.Floor(ly + 10f)) % 2 == 0)
                        return -0.5f;
                    return 0f;
                }
                case Mat.Cannon:
                {
                    // lz é a posição ao longo do canhão (0 no cotovelo, 1 na boca)
                    if (Math.Abs(lz - 0.24f) < 0.035f || Math.Abs(lz - 0.55f) < 0.035f)
                        return -0.45f;
                    if (Math.Abs(lz - 0.3f) < 0.035f || Math.Abs(lz - 0.61f) < 0.035f)
                        return 0.18f;
                    return 0f;
                }
            }

            return 0f;
        }

        static bool Near(float x, float y, float cx, float cy) => Math.Abs(x - cx) < 0.6f && Math.Abs(y - cy) < 0.6f;

        // Clarão do tiro em volta da boca do canhão
        void Flash(SpriteVolume v)
        {
            int mx = -1, my = -1;
            for (int y = 0; y < Height && mx < 0; y++)
                for (int x = 0; x < Width; x++)
                {
                    int id = v.IdAt(x, y);
                    if (id != 0 && m_Mat[id] == Mat.MuzzleCore)
                    {
                        mx = x;
                        my = y;
                        break;
                    }
                }

            if (mx < 0)
                return;
            int[] d = { 1, 0, -1, 0, 0, 1, 0, -1, 1, 1, -1, -1, 1, -1, -1, 1 };
            for (int k = 0; k < 8; k++)
            {
                int len = k < 4 ? 5 : 3;
                for (int i = 1; i <= len; i++)
                {
                    int X = mx + 1 + d[k * 2] * (i + 1), Y = my + 1 + d[k * 2 + 1] * (i + 1);
                    if (X < 0 || Y < 0 || X >= Width || Y >= Height)
                        continue;
                    int id = v.IdAt(X, Y);
                    if (id != 0 && m_Mat[id] != Mat.MuzzleCore)
                        continue;
                    Canvas.Set(X, Y, i < 3 ? k_Bio[4] : k_Bio[3]);
                    GlowMask.Set(X, Y, PixelRamps.White);
                }
            }
        }

        // Ruído de valor 3D suave (manchas presas à peça)
        static float Noise(float x, float y, float z, float scale, int salt)
        {
            x /= scale;
            y /= scale;
            z /= scale;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y), z0 = (int)Math.Floor(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float c00 = Lerp(Hash(x0, y0, z0, salt), Hash(x0 + 1, y0, z0, salt), fx);
            float c10 = Lerp(Hash(x0, y0 + 1, z0, salt), Hash(x0 + 1, y0 + 1, z0, salt), fx);
            float c01 = Lerp(Hash(x0, y0, z0 + 1, salt), Hash(x0 + 1, y0, z0 + 1, salt), fx);
            float c11 = Lerp(Hash(x0, y0 + 1, z0 + 1, salt), Hash(x0 + 1, y0 + 1, z0 + 1, salt), fx);
            return Lerp(Lerp(c00, c10, fy), Lerp(c01, c11, fy), fz);
        }

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

        static Px[] R(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
