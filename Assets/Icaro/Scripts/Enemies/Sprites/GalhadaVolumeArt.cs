using System;
using System.Collections.Generic;

namespace Icaro
{
    // Galhada (o Predador) em 8 direções (2026-11-13; o GalhadaArt antigo, de 4 direções, ficou sem uso): cervo magro
    // de pelo escuro, pernas finas, chifres de galho com pontas acesas e listras ciano no flanco. Montado em volume
    // (SpriteVolume: esferas e tubos) a 24 px/m. No aviso abaixa a galhada para a frente, se encolhe e as listras
    // acendem; na investida galopa esticado, a galhada à frente. Ao morrer, as pernas dobram e ele tomba de lado.
    public sealed class GalhadaVolumeArt : EnemyArt
    {
        public override string Key => "Predador";
        public override int Width => 84;
        public override int Height => 76;
        public override float PivotY => 4f;
        public override int Directions => 8;
        public override float PixelDensity => 24f;

        const float k_Pitch = 12f;

        static readonly Px[] k_Hide = EnemyRamps.Hide;
        static readonly Px[] k_Antler = EnemyRamps.Antler;
        static readonly Px[] k_Bio = EnemyRamps.Bio;
        static readonly Px[] k_Bone = EnemyRamps.Bone;
        static readonly Px[] k_Hoof = EnemyRamps.Shell;

        // Ids: corpo e pernas 1 a 19; cabeça 20 a 29; galhada 30 a 39; brilho 40+
        const int k_Body = 1, k_Chest = 2, k_Tail = 3, k_LegBase = 4, k_HoofBase = 12, k_Neck = 20, k_Head = 21,
            k_Snout = 22, k_Ear = 23, k_Antler1 = 30, k_Tip = 40, k_Stripe = 41, k_Eye = 42, k_Nose = 43;

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 3f, true),
            new EnemyClipSpec("Move", 8, 10f, true),
            new EnemyClipSpec("Windup", 4, 8f, false),
            new EnemyClipSpec("Attack", 6, 14f, true),
            new EnemyClipSpec("Hit", 2, 10f, false),
            new EnemyClipSpec("Death", 5, 9f, false),
        };

        struct Pose
        {
            public float Walk, Gallop, Crouch, HeadDown, Glow, Stretch, Breath, Fall, Recoil;
            public bool Galloping, Walking;
        }

        sealed class Part
        {
            public int Id;
            public bool Head;
            public float X0, Y0, Z0, R0, X1, Y1, Z1, R1;
        }

        readonly List<Part> m_Parts = new List<Part>();

        static Pose PoseFor(string clip, int f, float t)
        {
            var p = new Pose { Glow = 0.3f };
            switch (clip)
            {
                case "Idle":
                    p.Breath = Sin01(t);
                    p.HeadDown = -0.05f + 0.05f * Sin01(t + 0.25f);
                    break;
                case "Move":
                    p.Walking = true;
                    p.Walk = t;
                    break;
                case "Windup":
                    p.Crouch = 3f * Ease(Math.Min(1f, t * 1.4f));
                    p.HeadDown = Ease(Math.Min(1f, t * 1.4f));
                    p.Glow = 0.4f + 0.6f * t;
                    break;
                case "Attack":
                    p.Galloping = true;
                    p.Gallop = t;
                    p.HeadDown = 1f;
                    p.Stretch = 2f;
                    p.Glow = 1f;
                    break;
                case "Hit":
                    p.Recoil = f == 0 ? 1f : 0.4f;
                    p.HeadDown = f == 0 ? -0.35f : -0.1f;
                    p.Glow = 0.6f;
                    break;
                case "Death":
                    p.Fall = Ease(t);
                    p.Glow = 0.3f * (1f - t);
                    p.HeadDown = -0.3f * t;
                    break;
            }

            return p;
        }

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            Pose p = PoseFor(clip, frame, t);
            m_Parts.Clear();
            Build(p);

            float yaw = view == EnemyView.Front ? 0f : view == EnemyView.FrontSide ? 45f : view == EnemyView.Side ? 90f
                : view == EnemyView.BackSide ? 135f : 180f;
            var v = new SpriteVolume(Width, Height, CellPivotX, CellPivotY, yaw, k_Pitch);
            foreach (Part part in m_Parts)
                v.Segment(part.Id, part.X0, part.Y0, part.Z0, part.R0, part.X1, part.Y1, part.Z1, part.R1);
            Shade(v, p);
        }

        // ---------- montagem ----------

        void Build(Pose p)
        {
            float bob = p.Galloping ? Math.Abs(Sin01(p.Gallop)) * 1.5f : p.Walking ? Math.Abs(Sin01(p.Walk * 2f)) * 0.5f : 0f;
            float by = 26f - p.Crouch + bob - p.Recoil * 1f;
            float bz = -p.Recoil * 1.5f;
            float s = p.Stretch;

            // Corpo: traseiro, barriga e peito; peito claro e rabo
            Sphere(k_Body, 0f, by + 0.5f, bz - 11f - s * 0.5f, 7.2f);
            Seg(k_Body, 0f, by, bz - 9f - s * 0.5f, 7f + p.Breath * 0.2f, 0f, by + 0.5f, bz + 7f + s * 0.5f, 7.6f);
            Sphere(k_Body, 0f, by + 1f, bz + 9f + s * 0.5f, 8f);
            Sphere(k_Chest, 0f, by - 2.5f, bz + 13f + s * 0.5f, 4.6f);
            Seg(k_Tail, 0f, by + 4f, bz - 17f - s * 0.5f, 2f, 0f, by + 2f, bz - 19.5f - s * 0.5f, 1.5f);

            // Listras ciano no flanco, nos dois lados (rente à superfície)
            for (int k = -1; k <= 1; k += 2)
            {
                StripeOn(k, by, bz - 9f - s * 0.3f, 2.5f, bz - 5.5f - s * 0.3f, 4.5f);
                StripeOn(k, by, bz - 4f, 3f, bz - 0.5f, 5f);
                StripeOn(k, by, bz + 1f + s * 0.2f, 2.5f, bz + 4.5f + s * 0.2f, 4f);
                StripeOn(k, by, bz - 6f, -1f, bz - 2.5f, 1f);
            }

            // Pernas
            for (int i = 0; i < 4; i++)
                Leg(i, p, by, bz, s);

            // Pescoço e cabeça: abaixam em volta da base do pescoço
            float nx = 0f, ny = by + 4f, nz = bz + 11f + s * 0.5f;
            int first = m_Parts.Count;
            Seg(k_Neck, 0f, ny, nz, 4.2f, 0f, ny + 10f, nz + 5f, 3.2f);
            float hy = ny + 11.5f, hz = nz + 6.5f;
            Sphere(k_Head, 0f, hy, hz, 4f);
            Seg(k_Snout, 0f, hy - 0.5f, hz + 2f, 2.8f, 0f, hy - 2f, hz + 7.5f, 2f);
            Sphere(k_Nose, 0f, hy - 2.2f, hz + 9.2f, 0.9f);
            for (int k = -1; k <= 1; k += 2)
            {
                Sphere(k_Eye, k * 2.9f, hy + 0.8f, hz + 2.4f, 0.9f);
                Seg(k_Ear, k * 2.8f, hy + 2.5f, hz - 1.5f, 1.4f, k * 7f, hy + 3.5f, hz - 2.5f, 0.6f);
                Antler(k, hy + 3.5f, hz - 0.5f);
            }

            float angle = 62f * p.HeadDown;
            for (int i = first; i < m_Parts.Count; i++)
            {
                Part part = m_Parts[i];
                part.Head = true;
                RotX(angle, nx, ny, nz, ref part.Y0, ref part.Z0);
                RotX(angle, nx, ny, nz, ref part.Y1, ref part.Z1);
            }

            if (p.Fall > 0f)
                Fall(p.Fall);
        }

        // Uma perna: 0 dianteira esquerda, 1 dianteira direita, 2 traseira esquerda, 3 traseira direita
        void Leg(int i, Pose p, float by, float bz, float s)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * 4.2f;
            float hipY = by - 2f, hipZ = bz + (front ? 9f + s * 0.5f : -10.5f - s * 0.5f);
            float baseZ = front ? bz + 10f : bz - 11.5f;
            float swing, lift;
            if (p.Galloping)
            {
                // Galope: as da frente esticam juntas para a frente e as de trás para trás, e se juntam embaixo
                float g = Sin01(p.Gallop + (front ? 0f : 0.15f) + (side > 0 ? 0.06f : 0f));
                swing = (front ? 1f : -1f) * 6f * g;
                lift = Math.Max(0f, -g) * 3.5f + (1f - Math.Abs(g)) * 1.5f;
                swing -= (front ? 1f : -1f) * Math.Max(0f, -g) * 9f;
            }
            else if (p.Walking)
            {
                // Andar: pares diagonais (dianteira esquerda com traseira direita)
                float phase = p.Walk + (i == 0 || i == 3 ? 0f : 0.5f);
                swing = 3.5f * Sin01(phase);
                lift = Math.Max(0f, Cos01(phase)) * 2.5f;
            }
            else
            {
                swing = 0f;
                lift = 0f;
            }

            float fy = 1.3f + lift, fz = baseZ + swing;
            if (p.Fall > 0f)
            {
                // Dobra a perna embaixo do corpo
                fy = Lerp(fy, by - 6f, p.Fall);
                fz = Lerp(fz, hipZ + (front ? -4f : 4f), p.Fall);
            }

            int id = k_LegBase + i;
            float r = side < 0f ? 0f : 0f;
            if (front)
            {
                // Cotovelo atrás, joelho à frente
                float ky = Lerp(hipY, fy, 0.55f), kz = (hipZ + fz) * 0.5f + 1f + lift * 0.4f;
                Seg(id, x, hipY + 1f, hipZ, 3f, x, ky, kz, 1.6f);
                Seg(id, x, ky, kz, 1.4f, x, fy + 0.6f, fz, 1.05f);
            }
            else
            {
                // Coxa grossa, jarrete para trás
                float sy = hipY - 7f, sz = hipZ + 2.5f;
                float jy = Lerp(sy, fy, 0.5f), jz = fz - 3.5f + lift * 0.3f;
                Seg(id, x, hipY + 2f, hipZ, 4.2f, x, sy, sz, 2.2f);
                Seg(id, x, sy, sz, 1.9f, x, jy, jz, 1.4f);
                Seg(id, x, jy, jz, 1.25f, x, fy + 0.6f, fz, 1.05f);
            }

            Sphere(k_HoofBase + i, x, fy, fz + 0.4f, 1.5f + r);
        }

        // Um chifre de galho: haste que sobe para trás e para fora com três pontas acesas
        void Antler(float k, float y, float z)
        {
            float x = k * 1.8f;
            float ax = x + k * 3f, ay = y + 6f, az = z - 1f;
            float bx = x + k * 6.5f, by = y + 12f, bz = z - 3f;
            float cx = x + k * 8f, cy = y + 18.5f, cz = z - 1.5f;
            Seg(k_Antler1, x, y, z, 1.4f, ax, ay, az, 1.15f);
            Seg(k_Antler1, ax, ay, az, 1.15f, bx, by, bz, 1f);
            Seg(k_Antler1, bx, by, bz, 1f, cx, cy, cz, 0.75f);
            // Pontas: uma para a frente no primeiro galho, uma para fora no segundo
            float tx = ax + k * 0.5f, ty = ay + 5f, tz = az + 4f;
            Seg(k_Antler1, ax, ay, az, 0.9f, tx, ty, tz, 0.6f);
            float ux = bx + k * 5f, uy = by + 2.5f, uz = bz - 2f;
            Seg(k_Antler1, bx, by, bz, 0.9f, ux, uy, uz, 0.6f);
            Sphere(k_Tip, cx, cy + 0.6f, cz, 1.1f);
            Sphere(k_Tip, tx, ty + 0.5f, tz, 0.95f);
            Sphere(k_Tip, ux + k * 0.5f, uy, uz, 0.95f);
        }

        // Listra rente ao flanco (o corpo é um tubo de raio ~7,4 com o centro em by): de (z0, altura a0) a (z1, a1)
        void StripeOn(float k, float by, float z0, float a0, float z1, float a1)
        {
            const float r = 7.4f;
            float x0 = k * (float)Math.Sqrt(Math.Max(1f, r * r - a0 * a0)) * 0.97f;
            float x1 = k * (float)Math.Sqrt(Math.Max(1f, r * r - a1 * a1)) * 0.97f;
            Seg(k_Stripe, x0, by + a0, z0, 0.75f, x1, by + a1, z1, 0.75f);
        }

        // Tomba de lado (para a direita do bicho), assentando no chão
        void Fall(float f)
        {
            double a = -78.0 * f * Math.PI / 180.0;
            float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            float low = float.MaxValue;
            foreach (Part part in m_Parts)
            {
                Roll(c, sn, ref part.X0, ref part.Y0);
                Roll(c, sn, ref part.X1, ref part.Y1);
                low = Math.Min(low, Math.Min(part.Y0 - part.R0, part.Y1 - part.R1));
            }

            float lift = -low + 0.5f;
            foreach (Part part in m_Parts)
            {
                part.Y0 += lift;
                part.Y1 += lift;
            }
        }

        static void Roll(float c, float s, ref float x, ref float y)
        {
            float nx = x * c - y * s, ny = x * s + y * c;
            x = nx;
            y = ny;
        }

        // Gira (y, z) em volta do eixo x que passa por (py, pz); positivo = a cabeça desce para a frente
        static void RotX(float degrees, float px, float py, float pz, ref float y, ref float z)
        {
            double a = -degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float dy = y - py, dz = z - pz;
            // descer para a frente: o ponto de cima vai para a frente e para baixo
            y = py + dy * c + dz * s;
            z = pz - dy * s + dz * c;
        }

        void Sphere(int id, float x, float y, float z, float r) => Seg(id, x, y, z, r, x, y, z, r);

        void Seg(int id, float x0, float y0, float z0, float r0, float x1, float y1, float z1, float r1)
        {
            m_Parts.Add(new Part { Id = id, X0 = x0, Y0 = y0, Z0 = z0, R0 = r0, X1 = x1, Y1 = y1, Z1 = z1, R1 = r1 });
        }

        // ---------- cor ----------

        void Shade(SpriteVolume v, Pose p)
        {
            var ignore = new bool[64];
            ignore[k_Stripe] = ignore[k_Eye] = ignore[k_Nose] = ignore[k_Tip] = true;
            ignore[k_Chest] = true;
            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            bool lit = p.Fall < 0.6f;
            int stripe = p.Glow > 0.75f ? 4 : p.Glow > 0.45f ? 3 : 2;

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
                    if (id == k_Stripe)
                    {
                        col = lit ? k_Bio[stripe] : k_Hide[1];
                        glow = lit;
                    }
                    else if (id == k_Eye || id == k_Tip)
                    {
                        col = lit ? (light > 0.3f || p.Glow > 0.7f ? EnemyRamps.CyanLight : k_Bio[3]) : k_Hide[1];
                        glow = lit;
                    }
                    else if (id == k_Nose)
                        col = EnemyRamps.Dark;
                    else if (id >= k_Antler1 && id < k_Tip)
                        col = SpriteVolume.Band(k_Antler, light * 0.9f + 0.05f);
                    else if (id >= k_HoofBase && id < k_HoofBase + 4)
                        col = SpriteVolume.Band(k_Hoof, light * 0.8f);
                    else if (id == k_Tail)
                        col = SpriteVolume.Band(k_Bone, light * 0.8f + 0.15f);
                    else if (id == k_Chest)
                        col = SpriteVolume.Band(k_Hide, light + 0.25f);
                    else
                    {
                        // Pelo: patas de longe e barriga um pouco mais escuras
                        float bias = id >= k_LegBase && id < k_LegBase + 4 ? -0.1f : 0f;
                        col = SpriteVolume.Band(k_Hide, light + bias);
                    }

                    if (!ignore[id] && v.IsSeam(x, y, 2.5f, ignore))
                        col = k_Hide[0];
                    c.Set(x, y, col);
                    if (glow)
                        mask.Set(x, y, PixelRamps.White);
                }
            }
        }
    }
}
