using System;
using System.Collections.Generic;

namespace Icaro
{
    // Mariposa-lanterna (o Zangão) em 8 direções (2026-11-13; o MariposaArt de 4 direções ficou sem uso): corpo
    // escuro e peludo, antenas em pena, olhos ciano, asas claras lilás-pálido com um olho ciano e veios, e o abdômen
    // âmbar aceso como uma lanterna. Montada em volume (SpriteVolume) a 24 px/m: as asas são lâminas (polígonos
    // planos) que giram em volta do corpo ao bater, e o corpo voa inclinado, com o abdômen pendendo. No aviso o
    // abdômen se curva para a frente por baixo do corpo e o ferrão acende (a IA vira a mariposa para o jogador); ao
    // morrer, fecha as asas e cai girando. O pivô é o centro do corpo.
    public sealed class MariposaVolumeArt : EnemyArt
    {
        public override string Key => "Zangao";
        public override int Width => 56;
        public override int Height => 52;
        public override float PivotY => 28f;
        public override int Directions => 8;
        public override float PixelDensity => 24f;

        const float k_Pitch = 12f;
        const float k_Tilt = 38f;
        // As asas seguem o corpo inclinado e um pouco mais (o plano delas contém o eixo do corpo: sem a inclinação,
        // de frente ficariam de perfil)
        const float k_WingTilt = 45f;
        const float k_WingScale = 1.15f;
        const float k_CurlRest = 12f, k_CurlSting = 128f;

        static readonly Px[] k_Body = EnemyRamps.MothBody;
        static readonly Px[] k_Wing = EnemyRamps.MothWing;
        static readonly Px[] k_Amber = EnemyRamps.Glow;
        static readonly Px[] k_Bio = EnemyRamps.Bio;

        const int k_Thorax = 1, k_Head = 2, k_Leg = 3, k_Antenna = 4, k_Abdomen = 5, k_Fore = 10, k_Hind = 12,
            k_Eye = 20, k_Sting = 21;

        // Ponta do ferrão com o abdômen todo curvado (o aviso), em metros a partir do pivô: a origem dos tiros
        public static void StingTip(out float y, out float z)
        {
            Abdomen(k_CurlSting, out _, out _, out _, out _, out float ty, out float tz);
            Tilt(ref ty, ref tz);
            y = ty / 24f;
            z = tz / 24f;
        }

        public override EnemyClipSpec[] Clips { get; } =
        {
            new EnemyClipSpec("Idle", 4, 14f, true),
            new EnemyClipSpec("Move", 4, 16f, true),
            new EnemyClipSpec("Windup", 3, 8f, false),
            new EnemyClipSpec("Attack", 2, 10f, false),
            new EnemyClipSpec("Hit", 2, 12f, false),
            new EnemyClipSpec("Death", 5, 10f, false),
        };

        sealed class Part
        {
            public int Id;
            public bool Plane;
            public float X0, Y0, Z0, R0, X1, Y1, Z1, R1;
            public float[] U, V, Uv;
        }

        readonly List<Part> m_Parts = new List<Part>();

        // Asas (fora, frente) a partir da raiz: anterior grande e posterior menor
        static readonly float[] k_ForeShape =
            { 0.4f, 1.4f, 4.5f, 4.2f, 10.5f, 5.8f, 15f, 5.2f, 16.2f, 2f, 14.5f, -1.4f, 9f, -2.6f, 3.5f, -2f, 0.4f, -0.6f };
        static readonly float[] k_HindShape =
            { 0.4f, -0.4f, 5.5f, -1.2f, 10f, -3.4f, 10.2f, -6.5f, 7f, -8.6f, 3.5f, -8f, 0.6f, -4.2f };

        protected override void Draw(EnemyView view, string clip, float t, int frame)
        {
            // Batida das asas: -1 embaixo, 1 em cima
            float flap = Sin01(t), glow = 0.55f, sting = 0f, fold = 0f, spin = 0f, curl = k_CurlRest;
            switch (clip)
            {
                case "Windup":
                    flap = Sin01(t * 2f);
                    glow = 0.6f + 0.4f * t;
                    sting = t;
                    curl = Lerp(k_CurlRest, k_CurlSting, Ease(Math.Min(1f, t * 2f)));
                    break;
                case "Attack":
                    flap = frame == 0 ? 0.6f : -0.4f;
                    glow = 1f;
                    sting = frame == 0 ? 1f : 0.3f;
                    curl = frame == 0 ? k_CurlSting : Lerp(k_CurlSting, k_CurlRest, 0.4f);
                    break;
                case "Hit":
                    flap = frame == 0 ? 1f : -1f;
                    glow = 0.8f;
                    curl = 40f;
                    break;
                case "Death":
                    fold = Ease(t);
                    flap = 1f;
                    glow = 0.5f * (1f - t);
                    spin = 230f * t;
                    curl = 60f;
                    break;
            }

            m_Parts.Clear();
            Build(flap, fold, curl, spin);

            float yaw = view == EnemyView.Front ? 0f : view == EnemyView.FrontSide ? 45f : view == EnemyView.Side ? 90f
                : view == EnemyView.BackSide ? 135f : 180f;
            var v = new SpriteVolume(Width, Height, CellPivotX, CellPivotY, yaw, k_Pitch);
            foreach (Part part in m_Parts)
            {
                if (part.Plane)
                    v.Polygon(part.Id, part.X0, part.Y0, part.Z0, part.U, part.V, part.Uv);
                else
                    v.Segment(part.Id, part.X0, part.Y0, part.Z0, part.R0, part.X1, part.Y1, part.Z1, part.R1);
            }

            Shade(v, glow, sting);
        }

        // ---------- montagem (no espaço do corpo: z para a cabeça; depois inclinado) ----------

        void Build(float flap, float fold, float curl, float spin)
        {
            // Asas: o ângulo em volta do eixo do corpo (0 = abertas na horizontal, positivo = para cima)
            float up = Lerp(30f + 40f * flap, 84f, fold);
            for (int k = -1; k <= 1; k += 2)
            {
                Wing(k_Fore + (k < 0 ? 0 : 1), k, up, 0.6f, 0.4f, k_ForeShape);
                Wing(k_Hind + (k < 0 ? 0 : 1), k, up * 0.82f - 6f, 0.2f, -0.6f, k_HindShape);
            }

            // Corpo: tórax peludo, cabeça, olhos, antenas em pena e perninhas
            Seg(k_Thorax, 0f, 0f, 1f, 3.3f, 0f, 0f, 1f, 3.3f);
            Seg(k_Head, 0f, 0.6f, 4.6f, 2.3f, 0f, 0.6f, 4.6f, 2.3f);
            for (int k = -1; k <= 1; k += 2)
            {
                Seg(k_Eye, k * 1.4f, 1.1f, 6.1f, 0.95f, k * 1.4f, 1.1f, 6.1f, 0.95f);
                float ax = k * 0.8f, ay = 2.4f, az = 5.8f;
                float bx = k * 3f, by = 5.5f, bz = 8.2f, cx = k * 5.6f, cy = 7f, cz = 7.6f;
                Seg(k_Antenna, ax, ay, az, 0.45f, bx, by, bz, 0.45f);
                Seg(k_Antenna, bx, by, bz, 0.45f, cx, cy, cz, 0.35f);
                // Penas da antena
                for (int j = 1; j <= 3; j++)
                {
                    float tt = j / 4f;
                    float px = Lerp(ax, cx, tt), py = Lerp(ay, cy, tt) + (j == 2 ? 0.8f : 0f), pz = Lerp(az, cz, tt) + 0.6f;
                    Seg(k_Antenna, px, py, pz, 0.35f, px + k * 0.6f, py + 1.4f, pz + 0.8f, 0.25f);
                }

                for (int j = 0; j < 3; j++)
                {
                    float z = 2.5f - j * 1.6f;
                    Seg(k_Leg, k * 1.4f, -2.4f, z, 0.45f, k * 3f, -4.6f, z + 0.8f - j * 0.6f, 0.35f);
                }
            }

            // Abdômen: base e ponta, que se curva para a frente por baixo do corpo; o ferrão na ponta
            Abdomen(curl, out float my, out float mz, out float r0, out float r1, out float ty, out float tz);
            Seg(k_Abdomen, 0f, -0.4f, -1.4f, r0, 0f, my, mz, r1);
            Seg(k_Abdomen + 1, 0f, my, mz, r1, 0f, ty, tz, 1.2f);
            Seg(k_Sting, 0f, ty, tz, 0.85f, 0f, ty, tz, 0.85f);

            // Giro da queda (em volta do eixo do corpo) e a inclinação do voo
            double a = spin * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            foreach (Part part in m_Parts)
            {
                if (part.Plane)
                {
                    Roll(c, s, ref part.X0, ref part.Y0);
                    Roll(c, s, ref part.U[0], ref part.U[1]);
                    Roll(c, s, ref part.V[0], ref part.V[1]);
                    Tilt(ref part.Y0, ref part.Z0);
                    Tilt(ref part.U[1], ref part.U[2], k_WingTilt);
                    Tilt(ref part.V[1], ref part.V[2], k_WingTilt);
                }
                else
                {
                    Roll(c, s, ref part.X0, ref part.Y0);
                    Roll(c, s, ref part.X1, ref part.Y1);
                    Tilt(ref part.Y0, ref part.Z0);
                    Tilt(ref part.Y1, ref part.Z1);
                }
            }
        }

        // Pontos do abdômen para a curva (graus: 0 = reto para trás, 90 = para baixo, mais = para a frente)
        static void Abdomen(float curl, out float my, out float mz, out float r0, out float r1, out float ty, out float tz)
        {
            double a1 = curl * 0.45f * Math.PI / 180.0, a2 = curl * Math.PI / 180.0;
            my = -0.4f - (float)Math.Sin(a1) * 4.2f;
            mz = -1.4f - (float)Math.Cos(a1) * 4.2f;
            ty = my - (float)Math.Sin(a2) * 4.8f;
            tz = mz - (float)Math.Cos(a2) * 4.8f;
            r0 = 3f;
            r1 = 2.4f;
        }

        void Wing(int id, int side, float degrees, float y, float z, float[] shape)
        {
            double a = degrees * Math.PI / 180.0;
            m_Parts.Add(new Part
            {
                Id = id, Plane = true, X0 = side * 1.6f, Y0 = y, Z0 = z,
                U = new[] { side * (float)Math.Cos(a), (float)Math.Sin(a), 0f }, V = new[] { 0f, 0f, 1f }, Uv = Scaled(shape),
            });
        }

        static float[] Scaled(float[] shape)
        {
            var r = new float[shape.Length];
            for (int i = 0; i < shape.Length; i++)
                r[i] = shape[i] * k_WingScale;
            return r;
        }

        static void Tilt(ref float y, ref float z, float degrees = k_Tilt)
        {
            double a = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float ny = y * c + z * s, nz = -y * s + z * c;
            y = ny;
            z = nz;
        }

        static void Roll(float c, float s, ref float x, ref float y)
        {
            float nx = x * c - y * s, ny = x * s + y * c;
            x = nx;
            y = ny;
        }

        void Seg(int id, float x0, float y0, float z0, float r0, float x1, float y1, float z1, float r1)
        {
            m_Parts.Add(new Part { Id = id, X0 = x0, Y0 = y0, Z0 = z0, R0 = r0, X1 = x1, Y1 = y1, Z1 = z1, R1 = r1 });
        }

        // ---------- cor ----------

        void Shade(SpriteVolume v, float glow, float sting)
        {
            var ignore = new bool[32];
            ignore[k_Eye] = ignore[k_Sting] = ignore[k_Antenna] = ignore[k_Leg] = true;
            PixelCanvas c = Canvas;
            PixelCanvas mask = GlowMask;
            bool lit = glow > 0.15f;
            int amber = glow > 0.85f ? 1 : glow > 0.5f ? 0 : -1;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int id = v.IdAt(x, y);
                    if (id == 0)
                        continue;
                    float light = v.LightAt(x, y);
                    Px col;
                    bool emit = false;
                    if (id == k_Fore || id == k_Fore + 1 || id == k_Hind || id == k_Hind + 1)
                    {
                        v.LocalAt(x, y, out float a, out float b, out _);
                        col = WingColor(id >= k_Hind, a / k_WingScale, b / k_WingScale, light, lit, out emit);
                    }
                    else if (id == k_Abdomen || id == k_Abdomen + 1)
                    {
                        v.LocalAt(x, y, out _, out _, out float t);
                        // Anéis escuros entre os segmentos; o resto aceso
                        float along = id == k_Abdomen ? t * 0.5f : 0.5f + t * 0.5f;
                        bool ring = Math.Abs(along * 4f - (float)Math.Round(along * 4f)) < 0.09f && along > 0.1f;
                        int i = (light > 0.6f ? 3 : light > 0.15f ? 2 : 1) + amber;
                        col = !lit ? k_Body[2] : ring ? k_Amber[0] : k_Amber[Math.Max(0, Math.Min(4, i))];
                        emit = lit && !ring;
                    }
                    else if (id == k_Sting)
                    {
                        col = sting > 0.6f ? PixelRamps.White : sting > 0.1f ? k_Amber[4] : k_Amber[1];
                        emit = sting > 0.1f;
                    }
                    else if (id == k_Eye)
                    {
                        col = lit ? (light > 0.3f ? EnemyRamps.CyanLight : k_Bio[3]) : k_Body[1];
                        emit = lit;
                    }
                    else if (id == k_Antenna || id == k_Leg)
                        col = light > 0.4f ? k_Body[3] : k_Body[2];
                    else
                    {
                        // Pelo do tórax: tom pela luz, com tufos claros salteados
                        col = SpriteVolume.Band(k_Body, light + 0.1f);
                        if (light > 0.3f && ((x * 7 + y * 13) % 11 == 0))
                            col = k_Body[4];
                    }

                    if (!ignore[id] && v.IsSeam(x, y, 1.5f, ignore))
                        col = id >= k_Fore && id < k_Eye ? k_Wing[0] : k_Body[0];
                    c.Set(x, y, col);
                    if (emit)
                        mask.Set(x, y, PixelRamps.White);
                }
            }
        }

        // Asa: claro lilás-pálido, a raiz mais escura e peluda, veios, a beira clara e o olho ciano na anterior
        Px WingColor(bool hind, float a, float b, float light, bool lit, out bool emit)
        {
            emit = false;
            if (!hind)
            {
                float ex = a - 9.5f, ey = b - 1.6f, d2 = ex * ex + ey * ey;
                if (d2 < 5.3f)
                {
                    if (d2 < 0.55f)
                        return EnemyRamps.Dark;
                    emit = lit;
                    return lit ? k_Bio[d2 < 2.2f ? 3 : 2] : k_Wing[1];
                }

                if (d2 < 7.6f)
                    return EnemyRamps.Dark;
            }
            else
            {
                // Mancha âmbar fraca na posterior
                float ex = a - 6f, ey = b + 4.5f;
                if (ex * ex + ey * ey < 3.2f)
                    return k_Amber[lit ? 2 : 1];
            }

            if (Vein(a, b, 0.4f, 0f, hind ? 9.5f : 15.5f, hind ? -4.5f : 3.2f) ||
                Vein(a, b, 0.8f, 0.4f, hind ? 6f : 10f, hind ? -1f : 5.2f) ||
                Vein(a, b, 0.8f, -0.6f, hind ? 5.5f : 11f, hind ? -7.5f : -2f))
                return k_Wing[1];
            // As duas faces claras (asas lilás-pálido que não somem nas árvores)
            int tone = light > 0.45f ? 3 : 2;
            if (hind)
                tone--;
            if (a < 2.6f)
                return k_Body[3];
            if (a > (hind ? 8.8f : 14f))
                tone++;
            return k_Wing[Math.Max(0, Math.Min(4, tone))];
        }

        static bool Vein(float a, float b, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
            float t = ((a - x0) * dx + (b - y0) * dy) / l2;
            if (t < 0.1f || t > 0.95f)
                return false;
            float ex = a - (x0 + dx * t), ey = b - (y0 + dy * t);
            return ex * ex + ey * ey < 0.2f;
        }
    }
}
