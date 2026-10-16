using System;
using System.Collections.Generic;

namespace Icaro
{
    // Tela de pixels para as artes geradas em código (ícones dos itens, molduras e selos das cartas). (0, 0) é o canto
    // superior esquerdo. As formas são sombreadas por uma rampa de cores com a luz vindo do alto à esquerda (a normal
    // de cada pixel escolhe o tom, com um pontilhado leve entre os tons), e há contorno escuro, brilhos aditivos,
    // chamas, raios e faíscas. Não usa UnityEngine: o ItemArt converte em Sprite, e as prévias fora do Unity usam o
    // mesmo desenho.
    public sealed class PixelCanvas
    {
        // Forma: diz se o ponto (centro do pixel) está dentro e a normal da superfície ali (x para a direita, y para
        // baixo; o z sai da tela e é calculado)
        public delegate bool Shape(float x, float y, out float nx, out float ny);

        public readonly int Width;
        public readonly int Height;
        public readonly Px[] Pixels;

        // Luz do alto à esquerda, um pouco de frente (normalizada)
        const float k_Lx = -0.5f;
        const float k_Ly = -0.62f;
        const float k_Lz = 0.6f;

        static readonly float[] k_Bayer = { 0f, 0.5f, 0.75f, 0.25f };

        // Quanto o pontilhado mistura tons vizinhos da rampa (0 = sem pontilhado)
        public float Dither = 0.4f;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Px[width * height];
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Px Get(int x, int y) => Contains(x, y) ? Pixels[y * Width + x] : default;

        public void Set(int x, int y, Px color)
        {
            if (!Contains(x, y) || color.A == 0)
                return;
            int i = y * Width + x;
            Pixels[i] = color.A == 255 ? color : Over(Pixels[i], color);
        }

        public void Erase(int x, int y)
        {
            if (Contains(x, y))
                Pixels[y * Width + x] = default;
        }

        // Luz somada (brilhos): clareia o que já existe e, no vazio, vira um halo translúcido
        public void Add(int x, int y, Px color, float amount)
        {
            if (!Contains(x, y) || amount <= 0f)
                return;
            int i = y * Width + x;
            Px d = Pixels[i];
            float a = Math.Min(1f, amount * color.A / 255f);
            float da = d.A / 255f;
            float oa = Math.Min(1f, da + a * (1f - da));
            float r = d.R * da + color.R * a;
            float g = d.G * da + color.G * a;
            float b = d.B * da + color.B * a;
            float k = oa > 0f ? 1f / Math.Max(oa, da + a * 0.5f) : 0f;
            Pixels[i] = new Px(ToByte(r * k), ToByte(g * k), ToByte(b * k), ToByte(oa * 255f));
        }

        static Px Over(Px d, Px s)
        {
            float sa = s.A / 255f;
            float da = d.A / 255f;
            float oa = sa + da * (1f - sa);
            if (oa <= 0f)
                return default;
            return new Px(ToByte((s.R * sa + d.R * da * (1f - sa)) / oa), ToByte((s.G * sa + d.G * da * (1f - sa)) / oa),
                ToByte((s.B * sa + d.B * da * (1f - sa)) / oa), ToByte(oa * 255f));
        }

        static byte ToByte(float v) => (byte)(v < 0f ? 0 : v > 255f ? 255 : v);

        // ---------- preenchimento ----------

        // Forma sombreada pela rampa; bias clareia (> 0) ou escurece (< 0) o resultado
        public void Fill(Shape shape, Px[] ramp, float bias = 0f, byte alpha = 255)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (shape(x + 0.5f, y + 0.5f, out float nx, out float ny))
                    {
                        Px c = Pick(ramp, nx, ny, x, y, bias);
                        c.A = alpha;
                        Set(x, y, c);
                    }
                }
            }
        }

        public void FillFlat(Shape shape, Px color)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (shape(x + 0.5f, y + 0.5f, out _, out _))
                        Set(x, y, color);
                }
            }
        }

        public void Clear(Shape shape)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (shape(x + 0.5f, y + 0.5f, out _, out _))
                        Erase(x, y);
                }
            }
        }

        Px Pick(Px[] ramp, float nx, float ny, int x, int y, float bias)
        {
            float nz2 = 1f - nx * nx - ny * ny;
            float nz = nz2 > 0f ? (float)Math.Sqrt(nz2) : 0f;
            float light = nx * k_Lx + ny * k_Ly + nz * k_Lz;
            float t = 0.45f + 0.5f * light + bias;
            float dither = (k_Bayer[(x & 1) + (y & 1) * 2] - 0.375f) * Dither;
            int index = (int)Math.Round(t * (ramp.Length - 1) + dither);
            return ramp[index < 0 ? 0 : index >= ramp.Length ? ramp.Length - 1 : index];
        }

        public void Rect(int x, int y, int w, int h, Px color)
        {
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                    Set(i, j, color);
            }
        }

        // Linha de 1 pixel (Bresenham)
        public void Line(int x0, int y0, int x1, int y1, Px color)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                    break;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        public void Polyline(Px color, params int[] xy)
        {
            for (int i = 0; i + 3 < xy.Length; i += 2)
                Line(xy[i], xy[i + 1], xy[i + 2], xy[i + 3], color);
        }

        // Linha grossa (cápsula sem sombreado)
        public void Stroke(float x0, float y0, float x1, float y1, float width, Px color) =>
            FillFlat(Capsule(x0, y0, x1, y1, width * 0.5f), color);

        // ---------- formas ----------

        public static Shape Circle(float cx, float cy, float r) => (float x, float y, out float nx, out float ny) =>
        {
            nx = (x - cx) / r;
            ny = (y - cy) / r;
            return nx * nx + ny * ny <= 1f;
        };

        public static Shape Ellipse(float cx, float cy, float rx, float ry) =>
            (float x, float y, out float nx, out float ny) =>
            {
                nx = (x - cx) / rx;
                ny = (y - cy) / ry;
                return nx * nx + ny * ny <= 1f;
            };

        // Cilindro com pontas redondas entre dois pontos
        public static Shape Capsule(float x0, float y0, float x1, float y1, float r) =>
            (float x, float y, out float nx, out float ny) =>
            {
                float dx = x1 - x0, dy = y1 - y0;
                float len2 = dx * dx + dy * dy;
                float t = len2 > 0f ? ((x - x0) * dx + (y - y0) * dy) / len2 : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                nx = (x - (x0 + dx * t)) / r;
                ny = (y - (y0 + dy * t)) / r;
                return nx * nx + ny * ny <= 1f;
            };

        // Cilindro com pontas retas: sombreado só através do eixo
        public static Shape Tube(float x0, float y0, float x1, float y1, float r) =>
            (float x, float y, out float nx, out float ny) =>
            {
                float dx = x1 - x0, dy = y1 - y0;
                float len2 = dx * dx + dy * dy;
                float t = len2 > 0f ? ((x - x0) * dx + (y - y0) * dy) / len2 : 0f;
                nx = (x - (x0 + dx * t)) / r;
                ny = (y - (y0 + dy * t)) / r;
                return t >= 0f && t <= 1f && nx * nx + ny * ny <= 1f;
            };

        // Placa retangular com chanfro nas bordas (bevel) e cantos arredondados (corner)
        public static Shape Plate(float px, float py, float w, float h, float bevel, float corner = 0f) =>
            (float x, float y, out float nx, out float ny) =>
            {
                nx = ny = 0f;
                float l = x - px, r = px + w - x, t = y - py, b = py + h - y;
                if (l < 0f || r < 0f || t < 0f || b < 0f)
                    return false;
                if (corner > 0f)
                {
                    float cx = l < corner ? corner - l : r < corner ? -(corner - r) : 0f;
                    float cy = t < corner ? corner - t : b < corner ? -(corner - b) : 0f;
                    if (cx * cx + cy * cy > corner * corner && cx != 0f && cy != 0f)
                        return false;
                }

                if (bevel > 0f)
                {
                    if (l < bevel) nx -= 1f - l / bevel;
                    if (r < bevel) nx += 1f - r / bevel;
                    if (t < bevel) ny -= 1f - t / bevel;
                    if (b < bevel) ny += 1f - b / bevel;
                    Normalize(ref nx, ref ny, 0.92f);
                }

                return true;
            };

        // Polígono (pontos x, y em sequência) com chanfro nas bordas
        public static Shape Poly(float bevel, params float[] xy)
        {
            int n = xy.Length / 2;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < n; i++)
            {
                cx += xy[i * 2];
                cy += xy[i * 2 + 1];
            }

            cx /= n;
            cy /= n;
            return (float x, float y, out float nx, out float ny) =>
            {
                nx = ny = 0f;
                if (!InsidePoly(xy, x, y))
                    return false;
                if (bevel <= 0f)
                    return true;

                float best = float.MaxValue, bx = 0f, by = 0f;
                for (int i = 0; i < n; i++)
                {
                    float ax = xy[i * 2], ay = xy[i * 2 + 1];
                    float qx = xy[(i + 1) % n * 2], qy = xy[(i + 1) % n * 2 + 1];
                    float d = DistanceToSegment(x, y, ax, ay, qx, qy);
                    if (d < best)
                    {
                        best = d;
                        // Normal da aresta apontando para fora do polígono
                        float ex = qx - ax, ey = qy - ay;
                        float len = (float)Math.Sqrt(ex * ex + ey * ey);
                        bx = ey / len;
                        by = -ex / len;
                        if (bx * ((ax + qx) * 0.5f - cx) + by * ((ay + qy) * 0.5f - cy) < 0f)
                        {
                            bx = -bx;
                            by = -by;
                        }
                    }
                }

                if (best < bevel)
                {
                    float k = (1f - best / bevel) * 0.92f;
                    nx = bx * k;
                    ny = by * k;
                }

                return true;
            };
        }

        // Polígono com uma face plana voltada para (nx, ny): facetas de cristal e lâminas
        public static Shape Facet(float faceX, float faceY, params float[] xy) =>
            (float x, float y, out float nx, out float ny) =>
            {
                nx = faceX;
                ny = faceY;
                return InsidePoly(xy, x, y);
            };

        // Anel (faixa entre ri e ro), opcionalmente só entre os ângulos a0 e a1 (graus; 0 = direita, 90 = baixo)
        public static Shape Ring(float cx, float cy, float ro, float ri, float a0 = 0f, float a1 = 360f) =>
            (float x, float y, out float nx, out float ny) =>
            {
                nx = ny = 0f;
                float dx = x - cx, dy = y - cy;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < ri || d > ro || d <= 0f)
                    return false;
                if (a1 - a0 < 360f)
                {
                    float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                    if (angle < 0f)
                        angle += 360f;
                    bool inside = a0 <= a1 ? angle >= a0 && angle <= a1 : angle >= a0 || angle <= a1;
                    if (!inside)
                        return false;
                }

                float u = (d - (ro + ri) * 0.5f) / Math.Max(0.5f, (ro - ri) * 0.5f);
                nx = dx / d * u * 0.95f;
                ny = dy / d * u * 0.95f;
                return true;
            };

        // Gota: ponta em (cx, tipY) e corpo redondo de raio r centrado em (cx, cy)
        public static Shape Drop(float cx, float tipY, float cy, float r) =>
            (float x, float y, out float nx, out float ny) =>
            {
                float dx = x - cx;
                if (y >= cy)
                {
                    nx = dx / r;
                    ny = (y - cy) / r;
                    return nx * nx + ny * ny <= 1f;
                }

                float t = (y - tipY) / (cy - tipY);
                float hw = r * (float)Math.Pow(Math.Max(0f, t), 0.85);
                nx = hw > 0.3f ? dx / hw * 0.9f : 0f;
                ny = -(1f - t) * 0.5f - 0.1f;
                return t >= 0f && Math.Abs(dx) <= hw;
            };

        // Mantém só a parte da forma onde keep(x, y) é verdadeiro
        public static Shape Where(Shape shape, Func<float, float, bool> keep) =>
            (float x, float y, out float nx, out float ny) =>
            {
                if (!shape(x, y, out nx, out ny))
                    return false;
                return keep(x, y);
            };

        // Pontos de uma engrenagem (para Poly)
        public static float[] GearPoints(float cx, float cy, float ro, float ri, int teeth)
        {
            var points = new List<float>();
            for (int i = 0; i < teeth; i++)
            {
                double a = i * Math.PI * 2.0 / teeth;
                double step = Math.PI * 2.0 / teeth;
                AddPolar(points, cx, cy, ri, a - step * 0.5);
                AddPolar(points, cx, cy, ri, a - step * 0.28);
                AddPolar(points, cx, cy, ro, a - step * 0.18);
                AddPolar(points, cx, cy, ro, a + step * 0.18);
                AddPolar(points, cx, cy, ri, a + step * 0.28);
            }

            return points.ToArray();
        }

        // Pontos de um polígono regular (hexágono etc.)
        public static float[] RegularPoints(float cx, float cy, float r, int sides, float startDegrees = -90f)
        {
            var points = new List<float>();
            for (int i = 0; i < sides; i++)
                AddPolar(points, cx, cy, r, (startDegrees + i * 360.0 / sides) * Math.PI / 180.0);
            return points.ToArray();
        }

        static void AddPolar(List<float> points, float cx, float cy, float r, double angle)
        {
            points.Add(cx + r * (float)Math.Cos(angle));
            points.Add(cy + r * (float)Math.Sin(angle));
        }

        // ---------- efeitos ----------

        // Halo de luz em degraus (4 níveis, para continuar parecendo pixel art). Por padrão só pinta em volta do que já
        // está desenhado; over = true também clareia o desenho (luzes pequenas, núcleos)
        public void Glow(float cx, float cy, float r, Px color, float strength, bool over = false)
        {
            int x0 = (int)(cx - r) - 1, x1 = (int)(cx + r) + 1, y0 = (int)(cy - r) - 1, y1 = (int)(cy + r) + 1;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / r;
                    if (d >= 1f || (!over && Get(x, y).A == 255))
                        continue;
                    float amount = strength * (1f - d) * (1f - d);
                    amount = (float)Math.Floor(amount * 4f + 0.35f) / 4f;
                    Add(x, y, color, amount);
                }
            }
        }

        // Brilho em cruz: centro branco e braços na cor
        public void Sparkle(int x, int y, int size, Px color)
        {
            for (int i = 1; i <= size; i++)
            {
                Px c = color.WithAlpha(255 - (i - 1) * 160 / Math.Max(1, size));
                Set(x + i, y, c);
                Set(x - i, y, c);
                Set(x, y + i, c);
                Set(x, y - i, c);
            }

            Set(x, y, PixelRamps.White);
        }

        // Chama saindo de (bx, by) na direção (dx, dy), com largura w e altura h. A rampa vai da borda (escura) ao
        // núcleo (claro); seed muda as línguas de fogo.
        public void Flame(float bx, float by, float dx, float dy, float w, float h, Px[] ramp, float seed)
        {
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            dx /= len;
            dy /= len;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float px = x + 0.5f - bx, py = y + 0.5f - by;
                    float s = px * dx + py * dy;
                    float u = px * -dy + py * dx;
                    float t = s / h;
                    if (t < -0.12f || t > 1f)
                        continue;
                    float tc = Math.Max(0f, t);
                    float profile = (float)(Math.Pow(Math.Sin(Math.PI * Math.Min(1f, tc * 0.9f + 0.12f)), 0.55) *
                                            Math.Pow(1f - tc, 0.3));
                    float tongues = 0.82f + 0.18f * (float)Math.Sin(tc * 15f + seed * 3.1f);
                    float hw = w * 0.5f * profile * tongues;
                    if (t < 0f)
                        hw *= 1f + t * 6f;
                    float offset = (float)Math.Sin(tc * 5.5f + seed) * w * 0.14f * tc;
                    float across = Math.Abs(u - offset);
                    if (hw <= 0.2f || across > hw)
                        continue;
                    float heat = 1.05f - tc * 0.8f - across / hw * 0.55f;
                    float dither = (k_Bayer[(x & 1) + (y & 1) * 2] - 0.375f) * Dither;
                    int index = (int)Math.Round(heat * (ramp.Length - 1) + dither);
                    Set(x, y, ramp[index < 0 ? 0 : index >= ramp.Length ? ramp.Length - 1 : index]);
                }
            }
        }

        // Raio em zigue-zague passando pelos pontos (x, y em sequência), com halo
        public void Bolt(Random random, Px core, Px glow, params float[] xy)
        {
            var points = new List<int>();
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                float ax = xy[i], ay = xy[i + 1], bx = xy[i + 2], by = xy[i + 3];
                int steps = Math.Max(2, (int)(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / 3f));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float jitter = s == 0 ? 0f : (float)(random.NextDouble() * 4.0 - 2.0);
                    float ex = bx - ax, ey = by - ay;
                    float len = (float)Math.Sqrt(ex * ex + ey * ey);
                    points.Add((int)Math.Round(ax + ex * t + -ey / len * jitter));
                    points.Add((int)Math.Round(ay + ey * t + ex / len * jitter));
                }
            }

            points.Add((int)Math.Round(xy[xy.Length - 2]));
            points.Add((int)Math.Round(xy[xy.Length - 1]));
            int[] p = points.ToArray();
            for (int i = 0; i + 3 < p.Length; i += 2)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox != 0 || oy != 0)
                            AddLine(p[i] + ox, p[i + 1] + oy, p[i + 2] + ox, p[i + 3] + oy, glow, 0.35f);
                    }
                }
            }

            Polyline(core, p);
        }

        void AddLine(int x0, int y0, int x1, int y1, Px color, float amount)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Add(x0, y0, color, amount);
                if (x0 == x1 && y0 == y1)
                    break;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        // Contorno de 1 pixel em volta de tudo o que é opaco
        public void Outline(Px color)
        {
            var opaque = new bool[Pixels.Length];
            for (int i = 0; i < Pixels.Length; i++)
                opaque[i] = Pixels[i].A >= 160;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (opaque[i])
                        continue;
                    bool edge = (x > 0 && opaque[i - 1]) || (x < Width - 1 && opaque[i + 1]) ||
                                (y > 0 && opaque[i - Width]) || (y < Height - 1 && opaque[i + Width]);
                    if (edge)
                        Pixels[i] = color;
                }
            }
        }

        // Copia outra tela por cima desta, com o canto superior esquerdo em (ox, oy)
        public void Draw(PixelCanvas other, int ox, int oy)
        {
            for (int y = 0; y < other.Height; y++)
            {
                for (int x = 0; x < other.Width; x++)
                    Set(ox + x, oy + y, other.Pixels[y * other.Width + x]);
            }
        }

        // ---------- utilidades ----------

        static bool InsidePoly(float[] xy, float x, float y)
        {
            bool inside = false;
            int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        static float DistanceToSegment(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 > 0f ? ((x - ax) * dx + (y - ay) * dy) / len2 : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float ex = x - (ax + dx * t), ey = y - (ay + dy * t);
            return (float)Math.Sqrt(ex * ex + ey * ey);
        }

        static void Normalize(ref float x, ref float y, float max)
        {
            float len = (float)Math.Sqrt(x * x + y * y);
            if (len > max)
            {
                x *= max / len;
                y *= max / len;
            }
        }
    }
}
