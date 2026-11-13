using System;

namespace Icaro
{
    // Montagem em volume para os sprites de 8 direções: o inimigo é feito de esferas, tubos (esferas varridas, com o
    // raio afinando) e caixas chanfradas num espaço 3D em pixels (x para a direita do inimigo, y para cima, z para a
    // frente; origem no chão, no pivô) e projetado sem perspectiva por uma câmera girada em volta do inimigo (yaw) e
    // um pouco de cima (pitch). Cada pixel guarda a peça mais perto da câmera, a profundidade, a normal na tela (x
    // para a direita, y para baixo, z saindo da tela), quanto ela olha para cima no mundo e o ponto local da peça,
    // para a arte sombrear por faixas de tom e pôr detalhes presos à superfície. Sem UnityEngine (prévias fora do
    // Unity).
    public sealed class SpriteVolume
    {
        public readonly int Width, Height;
        readonly float m_Ox, m_Oy;
        readonly float m_Rx, m_Rz, m_Ux, m_Uy, m_Uz, m_Cx, m_Cy, m_Cz;
        readonly float[] m_Depth, m_Nx, m_Ny, m_Nz, m_Wy, m_Lx, m_Ly, m_Lz;
        readonly byte[] m_Id;
        bool[] m_Ignore;

        // Luz do alto à esquerda, um pouco de frente, fixa na tela (todas as direções ficam iluminadas igual)
        const float k_Lx = -0.5547f, k_Ly = -0.7060f, k_Lz = 0.4538f;

        // yaw: 0 = de frente; 90 = vista de lado (o inimigo virado para a direita da imagem); 180 = de costas
        public SpriteVolume(int width, int height, float pivotX, float pivotY, float yaw, float pitch)
        {
            Width = width;
            Height = height;
            m_Ox = pivotX;
            m_Oy = pivotY;
            double t = yaw * Math.PI / 180.0, f = pitch * Math.PI / 180.0;
            float st = (float)Math.Sin(t), ct = (float)Math.Cos(t), sf = (float)Math.Sin(f), cf = (float)Math.Cos(f);
            m_Rx = ct;
            m_Rz = st;
            m_Cx = -st * cf;
            m_Cy = sf;
            m_Cz = ct * cf;
            m_Ux = st * sf;
            m_Uy = cf;
            m_Uz = -ct * sf;
            int n = width * height;
            m_Depth = new float[n];
            m_Nx = new float[n];
            m_Ny = new float[n];
            m_Nz = new float[n];
            m_Wy = new float[n];
            m_Lx = new float[n];
            m_Ly = new float[n];
            m_Lz = new float[n];
            m_Id = new byte[n];
            for (int i = 0; i < n; i++)
                m_Depth[i] = float.NegativeInfinity;
        }

        // Ponto 3D -> tela (pixels da célula) e profundidade (maior = mais perto da câmera)
        public void Project(float x, float y, float z, out float sx, out float sy, out float depth)
        {
            sx = m_Ox + x * m_Rx + z * m_Rz;
            sy = m_Oy - (x * m_Ux + y * m_Uy + z * m_Uz);
            depth = x * m_Cx + y * m_Cy + z * m_Cz;
        }

        public void Sphere(int id, float x, float y, float z, float r) => Segment(id, x, y, z, r, x, y, z, r);

        // Tubo de (x0, y0, z0) a (x1, y1, z1), raio de r0 a r1, pontas redondas
        public void Segment(int id, float x0, float y0, float z0, float r0, float x1, float y1, float z1, float r1)
        {
            Project(x0, y0, z0, out float ax, out float ay, out float ad);
            Project(x1, y1, z1, out float bx, out float by, out float bd);
            float rm = Math.Max(r0, r1) + 1f;
            int xa = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - rm));
            int xb = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(ax, bx) + rm));
            int ya = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - rm));
            int yb = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(ay, by) + rm));
            float dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            for (int py = ya; py <= yb; py++)
            {
                for (int px = xa; px <= xb; px++)
                {
                    float X = px + 0.5f, Y = py + 0.5f;
                    float t = len2 > 1e-5f ? ((X - ax) * dx + (Y - ay) * dy) / len2 : 0f;
                    t = t < 0f ? 0f : t > 1f ? 1f : t;
                    float ex = X - (ax + dx * t), ey = Y - (ay + dy * t);
                    float r = r0 + (r1 - r0) * t;
                    float d2 = ex * ex + ey * ey;
                    if (d2 >= r * r)
                        continue;
                    float h = (float)Math.Sqrt(r * r - d2);
                    float depth = ad + (bd - ad) * t + h;
                    int i = py * Width + px;
                    if (depth <= m_Depth[i])
                        continue;
                    m_Depth[i] = depth;
                    m_Id[i] = (byte)id;
                    m_Nx[i] = ex / r;
                    m_Ny[i] = ey / r;
                    m_Nz[i] = h / r;
                    m_Wy[i] = -m_Uy * m_Ny[i] + m_Cy * m_Nz[i];
                    m_Lx[i] = ex;
                    m_Ly[i] = ey;
                    m_Lz[i] = t;
                }
            }
        }

        // Caixa de centro (cx, cy, cz), eixos locais em 'axes' (a, b, c unitários e perpendiculares, 9 valores) e
        // meias medidas (ha, hb, hc). As quinas têm bisel em duas faixas (b1 e b2 pixels a partir da beira): a normal
        // puxa para a face vizinha, e a faixa acende ou escurece como uma aresta chanfrada.
        public void Box(int id, float cx, float cy, float cz, float[] axes, float ha, float hb, float hc,
            float b1 = 1f, float b2 = 2.2f)
        {
            float h0 = ha, h1 = hb, h2 = hc;
            float sxMin = float.MaxValue, sxMax = float.MinValue, syMin = float.MaxValue, syMax = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                float u = (k & 1) == 0 ? -ha : ha, v = (k & 2) == 0 ? -hb : hb, w = (k & 4) == 0 ? -hc : hc;
                Project(cx + axes[0] * u + axes[3] * v + axes[6] * w, cy + axes[1] * u + axes[4] * v + axes[7] * w,
                    cz + axes[2] * u + axes[5] * v + axes[8] * w, out float qx, out float qy, out _);
                sxMin = Math.Min(sxMin, qx);
                sxMax = Math.Max(sxMax, qx);
                syMin = Math.Min(syMin, qy);
                syMax = Math.Max(syMax, qy);
            }

            int xa = Math.Max(0, (int)Math.Floor(sxMin)), xb = Math.Min(Width - 1, (int)Math.Ceiling(sxMax));
            int ya = Math.Max(0, (int)Math.Floor(syMin)), yb = Math.Min(Height - 1, (int)Math.Ceiling(syMax));
            const float far = 1000f;
            // Direção do raio (da câmera para a cena) nos eixos da caixa
            float d0 = -(m_Cx * axes[0] + m_Cy * axes[1] + m_Cz * axes[2]);
            float d1 = -(m_Cx * axes[3] + m_Cy * axes[4] + m_Cz * axes[5]);
            float d2 = -(m_Cx * axes[6] + m_Cy * axes[7] + m_Cz * axes[8]);
            for (int py = ya; py <= yb; py++)
            {
                for (int px = xa; px <= xb; px++)
                {
                    float sx = px + 0.5f - m_Ox, su = m_Oy - (py + 0.5f);
                    float wx = m_Rx * sx + m_Ux * su + m_Cx * far - cx;
                    float wy = m_Uy * su + m_Cy * far - cy;
                    float wz = m_Rz * sx + m_Uz * su + m_Cz * far - cz;
                    float o0 = wx * axes[0] + wy * axes[1] + wz * axes[2];
                    float o1 = wx * axes[3] + wy * axes[4] + wz * axes[5];
                    float o2 = wx * axes[6] + wy * axes[7] + wz * axes[8];
                    float tmin = float.MinValue, tmax = float.MaxValue;
                    int axis = -1;
                    if (!Slab(o0, d0, h0, 0, ref tmin, ref tmax, ref axis) ||
                        !Slab(o1, d1, h1, 1, ref tmin, ref tmax, ref axis) ||
                        !Slab(o2, d2, h2, 2, ref tmin, ref tmax, ref axis) || axis < 0 || tmin > tmax)
                        continue;
                    float depth = far - tmin;
                    int idx = py * Width + px;
                    if (depth <= m_Depth[idx])
                        continue;
                    float p0 = o0 + d0 * tmin, p1 = o1 + d1 * tmin, p2 = o2 + d2 * tmin;

                    // Normal local da face, puxada para as faces vizinhas no bisel
                    float n0 = Bevel(0, axis, p0, h0, b1, b2);
                    float n1 = Bevel(1, axis, p1, h1, b1, b2);
                    float n2 = Bevel(2, axis, p2, h2, b1, b2);
                    float len = (float)Math.Sqrt(n0 * n0 + n1 * n1 + n2 * n2);
                    n0 /= len;
                    n1 /= len;
                    n2 /= len;
                    float nx = axes[0] * n0 + axes[3] * n1 + axes[6] * n2;
                    float ny = axes[1] * n0 + axes[4] * n1 + axes[7] * n2;
                    float nz = axes[2] * n0 + axes[5] * n1 + axes[8] * n2;
                    m_Depth[idx] = depth;
                    m_Id[idx] = (byte)id;
                    m_Nx[idx] = nx * m_Rx + nz * m_Rz;
                    m_Ny[idx] = -(nx * m_Ux + ny * m_Uy + nz * m_Uz);
                    m_Nz[idx] = nx * m_Cx + ny * m_Cy + nz * m_Cz;
                    m_Wy[idx] = ny;
                    m_Lx[idx] = p0;
                    m_Ly[idx] = p1;
                    m_Lz[idx] = p2;
                }
            }
        }

        // Polígono plano (as duas faces), no plano que passa por (ox, oy, oz) com os eixos u e v (unitários e
        // perpendiculares, 3 valores cada); 'uv' são os cantos em sequência (u, v). Guarda o ponto (u, v) do pixel em
        // LocalAt (x = u, y = v), para desenhar os detalhes da lâmina (veios, manchas)
        public void Polygon(int id, float ox, float oy, float oz, float[] u, float[] v, float[] uv)
        {
            int n = uv.Length / 2;
            float sxMin = float.MaxValue, sxMax = float.MinValue, syMin = float.MaxValue, syMax = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                float a = uv[k * 2], b = uv[k * 2 + 1];
                Project(ox + u[0] * a + v[0] * b, oy + u[1] * a + v[1] * b, oz + u[2] * a + v[2] * b,
                    out float qx, out float qy, out _);
                sxMin = Math.Min(sxMin, qx);
                sxMax = Math.Max(sxMax, qx);
                syMin = Math.Min(syMin, qy);
                syMax = Math.Max(syMax, qy);
            }

            float nx = u[1] * v[2] - u[2] * v[1], ny = u[2] * v[0] - u[0] * v[2], nz = u[0] * v[1] - u[1] * v[0];
            float dn = -(m_Cx * nx + m_Cy * ny + m_Cz * nz);
            if (Math.Abs(dn) < 1e-4f)
                return;
            // Face virada para a câmera
            float flip = dn < 0f ? 1f : -1f;
            int xa = Math.Max(0, (int)Math.Floor(sxMin)), xb = Math.Min(Width - 1, (int)Math.Ceiling(sxMax));
            int ya = Math.Max(0, (int)Math.Floor(syMin)), yb = Math.Min(Height - 1, (int)Math.Ceiling(syMax));
            const float far = 1000f;
            for (int py = ya; py <= yb; py++)
            {
                for (int px = xa; px <= xb; px++)
                {
                    float sx = px + 0.5f - m_Ox, su = m_Oy - (py + 0.5f);
                    float wx = m_Rx * sx + m_Ux * su + m_Cx * far - ox;
                    float wy = m_Uy * su + m_Cy * far - oy;
                    float wz = m_Rz * sx + m_Uz * su + m_Cz * far - oz;
                    float t = -(wx * nx + wy * ny + wz * nz) / dn;
                    float hx = wx - m_Cx * t, hy = wy - m_Cy * t, hz = wz - m_Cz * t;
                    float a = hx * u[0] + hy * u[1] + hz * u[2], b = hx * v[0] + hy * v[1] + hz * v[2];
                    if (!Inside(uv, n, a, b))
                        continue;
                    float depth = far - t;
                    int idx = py * Width + px;
                    if (depth <= m_Depth[idx])
                        continue;
                    float fx = nx * flip, fy = ny * flip, fz = nz * flip;
                    m_Depth[idx] = depth;
                    m_Id[idx] = (byte)id;
                    m_Nx[idx] = fx * m_Rx + fz * m_Rz;
                    m_Ny[idx] = -(fx * m_Ux + fy * m_Uy + fz * m_Uz);
                    m_Nz[idx] = fx * m_Cx + fy * m_Cy + fz * m_Cz;
                    m_Wy[idx] = fy;
                    m_Lx[idx] = a;
                    m_Ly[idx] = b;
                    m_Lz[idx] = 0f;
                }
            }
        }

        static bool Inside(float[] uv, int n, float a, float b)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ai = uv[i * 2], bi = uv[i * 2 + 1], aj = uv[j * 2], bj = uv[j * 2 + 1];
                if (bi > b != bj > b && a < (aj - ai) * (b - bi) / (bj - bi) + ai)
                    inside = !inside;
            }

            return inside;
        }

        static bool Slab(float o, float d, float h, int i, ref float tmin, ref float tmax, ref int axis)
        {
            if (Math.Abs(d) < 1e-6f)
                return Math.Abs(o) <= h;
            float t1 = (-h - o) / d, t2 = (h - o) / d;
            if (t1 > t2)
            {
                float tt = t1;
                t1 = t2;
                t2 = tt;
            }

            if (t1 > tmin)
            {
                tmin = t1;
                axis = i;
            }

            if (t2 < tmax)
                tmax = t2;
            return true;
        }

        static float Bevel(int i, int axis, float p, float h, float b1, float b2)
        {
            float sign = p >= 0f ? 1f : -1f;
            if (i == axis)
                return sign;
            float edge = h - Math.Abs(p);
            return edge < b1 ? sign : edge < b2 ? 0.45f * sign : 0f;
        }

        // Eixos de uma caixa girada: yaw em volta de y, depois pitch em volta de x, depois roll em volta de z (graus)
        public static float[] Axes(float yaw, float pitch, float roll)
        {
            double y = yaw * Math.PI / 180.0, p = pitch * Math.PI / 180.0, r = roll * Math.PI / 180.0;
            float cy = (float)Math.Cos(y), sy = (float)Math.Sin(y), cp = (float)Math.Cos(p), sp = (float)Math.Sin(p);
            float cr = (float)Math.Cos(r), sr = (float)Math.Sin(r);
            // M = Ry * Rx * Rz; colunas = eixos locais
            float m00 = cy * cr + sy * sp * sr, m01 = -cy * sr + sy * sp * cr, m02 = sy * cp;
            float m10 = cp * sr, m11 = cp * cr, m12 = -sp;
            float m20 = -sy * cr + cy * sp * sr, m21 = sy * sr + cy * sp * cr, m22 = cy * cp;
            return new[] { m00, m10, m20, m01, m11, m21, m02, m12, m22 };
        }

        // Eixos de uma caixa deitada ao longo de (dx, dy, dz) (o eixo a), com o eixo c o mais perto possível da frente
        public static float[] AxesAlong(float dx, float dy, float dz, float twist = 0f)
        {
            float l = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx /= l;
            dy /= l;
            dz /= l;
            // b = a x frente (ou a x direita, se a já aponta para a frente)
            float fx = 0f, fy = 0f, fz = 1f;
            if (Math.Abs(dz) > 0.9f)
            {
                fx = 1f;
                fz = 0f;
            }

            float bx = dy * fz - dz * fy, by = dz * fx - dx * fz, bz = dx * fy - dy * fx;
            float bl = (float)Math.Sqrt(bx * bx + by * by + bz * bz);
            bx /= bl;
            by /= bl;
            bz /= bl;
            float cx = dy * bz - dz * by, cy = dz * bx - dx * bz, cz = dx * by - dy * bx;
            if (twist != 0f)
            {
                double t = twist * Math.PI / 180.0;
                float c = (float)Math.Cos(t), s = (float)Math.Sin(t);
                float nbx = bx * c + cx * s, nby = by * c + cy * s, nbz = bz * c + cz * s;
                cx = cx * c - bx * s;
                cy = cy * c - by * s;
                cz = cz * c - bz * s;
                bx = nbx;
                by = nby;
                bz = nbz;
            }

            return new[] { dx, dy, dz, bx, by, bz, cx, cy, cz };
        }

        // Tubo por vários pontos (x, y, z em sequência), o raio indo de r0 a r1 pelo comprimento
        public void Chain(int id, float r0, float r1, params float[] xyz)
        {
            int n = xyz.Length / 3;
            float total = 0f;
            for (int i = 1; i < n; i++)
                total += Dist(xyz, i - 1, i);
            float s = 0f;
            for (int i = 1; i < n; i++)
            {
                float l = Dist(xyz, i - 1, i);
                float ra = r0 + (r1 - r0) * (total > 0f ? s / total : 0f);
                float rb = r0 + (r1 - r0) * (total > 0f ? (s + l) / total : 1f);
                Segment(id, xyz[i * 3 - 3], xyz[i * 3 - 2], xyz[i * 3 - 1], ra, xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2], rb);
                s += l;
            }
        }

        static float Dist(float[] p, int a, int b)
        {
            float dx = p[b * 3] - p[a * 3], dy = p[b * 3 + 1] - p[a * 3 + 1], dz = p[b * 3 + 2] - p[a * 3 + 2];
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // 0 = vazio
        public int IdAt(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : m_Id[y * Width + x];

        public float DepthAt(int x, int y) => m_Depth[y * Width + x];

        public void NormalAt(int x, int y, out float nx, out float ny, out float nz)
        {
            int i = y * Width + x;
            nx = m_Nx[i];
            ny = m_Ny[i];
            nz = m_Nz[i];
        }

        // Quanto a superfície olha para cima no mundo (1 = face de cima), para o musgo
        public float UpAt(int x, int y) => m_Wy[y * Width + x];

        // Ponto local da peça no pixel (nas caixas, a partir do centro e nos eixos da caixa)
        public void LocalAt(int x, int y, out float lx, out float ly, out float lz)
        {
            int i = y * Width + x;
            lx = m_Lx[i];
            ly = m_Ly[i];
            lz = m_Lz[i];
        }

        // Ponto 3D do pixel (na superfície da peça mais perto da câmera), no espaço do inimigo
        public void WorldAt(int x, int y, out float wx, out float wy, out float wz)
        {
            float sx = x + 0.5f - m_Ox, su = m_Oy - (y + 0.5f), d = m_Depth[y * Width + x];
            wx = sx * m_Rx + su * m_Ux + d * m_Cx;
            wy = su * m_Uy + d * m_Cy;
            wz = sx * m_Rz + su * m_Uz + d * m_Cz;
        }

        // Luz da normal (sem a faixa): cerca de -1 (de costas para a luz) a 1
        public float LightAt(int x, int y)
        {
            int i = y * Width + x;
            return m_Nx[i] * k_Lx + m_Ny[i] * k_Ly + m_Nz[i] * k_Lz;
        }

        // Tom chapado da rampa pela luz: as faixas de 0,15, 0,45, 0,75 e 0,93 (rampas de 5 tons; menos, as primeiras)
        public static Px Band(Px[] ramp, float light)
        {
            int i = 0;
            if (light > 0.15f) i++;
            if (light > 0.45f) i++;
            if (light > 0.75f) i++;
            if (light > 0.93f) i++;
            return ramp[i < ramp.Length ? i : ramp.Length - 1];
        }

        // Junta escura onde uma peça encosta noutra mais perto da câmera (a linha fica na de trás). Com gap 0, também
        // entre peças na mesma profundidade. ignore: peças que não fazem junta (detalhes)
        public bool IsSeam(int x, int y, float gap, bool[] ignore = null)
        {
            int id = IdAt(x, y);
            if (id == 0)
                return false;
            m_Ignore = ignore;
            float d = DepthAt(x, y);
            return Closer(x + 1, y, id, d, gap) || Closer(x - 1, y, id, d, gap) || Closer(x, y + 1, id, d, gap) ||
                   Closer(x, y - 1, id, d, gap);
        }

        bool Closer(int x, int y, int id, float depth, float gap)
        {
            int other = IdAt(x, y);
            if (other == 0 || other == id || m_Ignore != null && other < m_Ignore.Length && m_Ignore[other])
                return false;
            float od = DepthAt(x, y);
            return od > depth + gap || gap <= 0f && od == depth && other > id;
        }
    }
}
