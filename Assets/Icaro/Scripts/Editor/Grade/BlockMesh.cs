using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Peças facetadas (sem curvas nem esferas) para os biomas: caixa, prisma (coluna de N lados) e arco de anel feito
    // de segmentos retos com seção quadrada, além de peças com perfil (caixa chanfrada, arco com perfil qualquer). Tudo
    // entra numa MeshData (uma malha por material), com a textura repetindo a cada Tile metros (2 m = o mesmo tamanho
    // de pixel do resto do mapa; peças colossais usam mais, para os blocos da textura não parecerem azulejo).
    public static class BlockMesh
    {
        public static float Tile = GridSceneKit.Cell;

        // Caixa com centro, tamanho e rotação
        public static void Box(MeshData mesh, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 x = rotation * Vector3.right * (size.x * 0.5f);
            Vector3 y = rotation * Vector3.up * (size.y * 0.5f);
            Vector3 z = rotation * Vector3.forward * (size.z * 0.5f);
            Face(mesh, center, center + x, z, y);
            Face(mesh, center, center - x, z, y);
            Face(mesh, center, center + z, x, y);
            Face(mesh, center, center - z, x, y);
            Face(mesh, center, center + y, x, z);
            Face(mesh, center, center - y, x, z);
        }

        // Face retangular com centro c e meio-eixos u (largura) e v (altura da textura); a normal aponta do centro da
        // caixa para a face
        static void Face(MeshData mesh, Vector3 boxCenter, Vector3 c, Vector3 u, Vector3 v)
        {
            mesh.AddQuadAuto(c - u - v, c - u + v, c + u + v, c + u - v, (c - boxCenter).normalized, Tile);
        }

        // Prisma: coluna de 'sides' lados ao longo de 'axis' (normalizado), começando em 'start'. 'angle' gira a seção
        // (graus); com 'caps', fecha as duas pontas.
        public static void Prism(MeshData mesh, Vector3 start, Vector3 axis, float length, float radius, int sides,
            float angle = 0f, bool caps = true)
        {
            axis = axis.normalized;
            Vector3 b = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 c = Vector3.Cross(axis, b);
            Vector3 end = start + axis * length;
            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float t = (angle + i * 360f / sides) * Mathf.Deg2Rad;
                Vector3 offset = (Mathf.Cos(t) * b + Mathf.Sin(t) * c) * radius;
                ring0[i] = start + offset;
                ring1[i] = end + offset;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 outward = ((ring0[i] + ring0[j]) * 0.5f - start).normalized;
                mesh.AddQuadAuto(ring0[i], ring1[i], ring1[j], ring0[j], outward, Tile);
            }

            if (!caps)
                return;
            for (int i = 1; i < sides - 1; i++)
            {
                mesh.AddTriangleAuto(ring0[0], ring0[i], ring0[i + 1], -axis, Tile);
                mesh.AddTriangleAuto(ring1[0], ring1[i], ring1[i + 1], axis, Tile);
            }
        }

        // Arco de anel: segmentos retos de seção quadrada em volta de 'center', no plano perpendicular a 'normal',
        // de 'fromDeg' a 'toDeg' (0° = direção 'zero'). 'thickness' é a espessura no raio e 'width' ao longo da
        // normal. As pontas ficam fechadas (anel quebrado).
        public static void RingArc(MeshData mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius,
            float thickness, float width, float fromDeg, float toDeg, int segments)
        {
            normal = normal.normalized;
            Vector3 u = Vector3.ProjectOnPlane(zero, normal).normalized;
            Vector3 w = Vector3.Cross(normal, u);
            float halfT = thickness * 0.5f, halfW = width * 0.5f;

            Vector3[] Corners(float deg)
            {
                float t = deg * Mathf.Deg2Rad;
                Vector3 dir = Mathf.Cos(t) * u + Mathf.Sin(t) * w;
                Vector3 inner = center + dir * (radius - halfT), outer = center + dir * (radius + halfT);
                return new[] { inner - normal * halfW, inner + normal * halfW, outer + normal * halfW, outer - normal * halfW };
            }

            Vector3[] previous = Corners(fromDeg);
            for (int s = 1; s <= segments; s++)
            {
                float deg = Mathf.Lerp(fromDeg, toDeg, s / (float)segments);
                Vector3[] next = Corners(deg);
                Vector3 mid = center + Vector3.Normalize((previous[0] + next[0]) * 0.5f - center) * radius;
                Vector3 radial = (mid - center).normalized;
                mesh.AddQuadAuto(previous[3], previous[2], next[2], next[3], radial, Tile);   // fora
                mesh.AddQuadAuto(previous[0], previous[1], next[1], next[0], -radial, Tile);  // dentro
                mesh.AddQuadAuto(previous[1], previous[2], next[2], next[1], normal, Tile);   // frente
                mesh.AddQuadAuto(previous[0], previous[3], next[3], next[0], -normal, Tile);  // trás
                previous = next;
            }

            // Pontas quebradas
            Vector3[] first = Corners(fromDeg), last = Corners(toDeg);
            Vector3 tangentStart = Vector3.Normalize(Corners(fromDeg + 1f)[0] - first[0]);
            Vector3 tangentEnd = Vector3.Normalize(last[0] - Corners(toDeg - 1f)[0]);
            mesh.AddQuadAuto(first[0], first[1], first[2], first[3], -tangentStart, Tile);
            mesh.AddQuadAuto(last[0], last[1], last[2], last[3], tangentEnd, Tile);
        }

        // ---------- peças com perfil ----------

        // Retângulo de 'w' x 'h' com as quinas chanfradas em 'c', centrado em 'offset', no sentido anti-horário
        public static Vector2[] Chamfered(float w, float h, float c, Vector2 offset = default)
        {
            float x = w * 0.5f, y = h * 0.5f;
            c = Mathf.Min(c, Mathf.Min(x, y) * 0.9f);
            return new[]
            {
                new Vector2(x - c, -y) + offset, new Vector2(x, -y + c) + offset,
                new Vector2(x, y - c) + offset, new Vector2(x - c, y) + offset,
                new Vector2(-x + c, y) + offset, new Vector2(-x, y - c) + offset,
                new Vector2(-x, -y + c) + offset, new Vector2(-x + c, -y) + offset,
            };
        }

        // Como Chamfered, com uma canaleta rebaixada no meio das faces de cima e de baixo (eixo y): 'groove' é a
        // fração da largura ocupada pela canaleta e 'depth' a profundidade
        public static Vector2[] Grooved(float w, float h, float c, float groove, float depth)
        {
            float x = w * 0.5f, y = h * 0.5f, g = w * groove * 0.5f;
            c = Mathf.Min(c, Mathf.Min(x - g, y) * 0.9f);
            return new[]
            {
                new Vector2(x - c, -y), new Vector2(x, -y + c), new Vector2(x, y - c), new Vector2(x - c, y),
                new Vector2(g, y), new Vector2(g, y - depth), new Vector2(-g, y - depth), new Vector2(-g, y),
                new Vector2(-x + c, y), new Vector2(-x, y - c), new Vector2(-x, -y + c), new Vector2(-x + c, -y),
                new Vector2(-g, -y), new Vector2(-g, -y + depth), new Vector2(g, -y + depth), new Vector2(g, -y),
            };
        }

        // Arco varrido: o 'profile' (x = no raio, y = ao longo da normal) gira em volta de 'center' de 'fromDeg' a
        // 'toDeg' (0° = 'zero'), em 'segments' trechos retos. As pontas são fechadas com 'cap' (perfil convexo; sem ele,
        // o próprio perfil).
        public static void SweepArc(MeshData mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius,
            Vector2[] profile, float fromDeg, float toDeg, int segments, Vector2[] cap = null)
        {
            normal = normal.normalized;
            Vector3 u = Vector3.ProjectOnPlane(zero, normal).normalized;
            Vector3 w = Vector3.Cross(normal, u);
            segments = Mathf.Max(1, segments);

            Vector3 Point(float deg, Vector2 p)
            {
                float t = deg * Mathf.Deg2Rad;
                Vector3 dir = Mathf.Cos(t) * u + Mathf.Sin(t) * w;
                return center + dir * (radius + p.x) + normal * p.y;
            }

            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, s / (float)segments);
                float a1 = Mathf.Lerp(fromDeg, toDeg, (s + 1) / (float)segments);
                float mid = (a0 + a1) * 0.5f * Mathf.Deg2Rad;
                Vector3 radial = Mathf.Cos(mid) * u + Mathf.Sin(mid) * w;
                for (int i = 0; i < profile.Length; i++)
                {
                    Vector2 p0 = profile[i], p1 = profile[(i + 1) % profile.Length];
                    Vector2 edge = p1 - p0;
                    Vector3 outward = radial * edge.y - normal * edge.x;
                    Quad(mesh, Point(a0, p0), Point(a0, p1), Point(a1, p1), Point(a1, p0), outward);
                }
            }

            cap ??= profile;
            Vector3 startTangent = Point(fromDeg + 0.5f, Vector2.zero) - Point(fromDeg, Vector2.zero);
            Vector3 endTangent = Point(toDeg, Vector2.zero) - Point(toDeg - 0.5f, Vector2.zero);
            if (toDeg < fromDeg)
            {
                startTangent = -startTangent;
                endTangent = -endTangent;
            }

            Fan(mesh, cap, p => Point(fromDeg, p), -startTangent);
            Fan(mesh, cap, p => Point(toDeg, p), endTangent);
        }

        // Prisma reto de seção 'profile' (no plano x-y de 'rotation') com 'length' ao longo do z, centrado em 'center'
        public static void Extrude(MeshData mesh, Vector3 center, Quaternion rotation, Vector2[] profile, float length)
        {
            Vector3 x = rotation * Vector3.right, y = rotation * Vector3.up, z = rotation * Vector3.forward;
            Vector3 back = center - z * (length * 0.5f), front = center + z * (length * 0.5f);
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[(i + 1) % profile.Length];
                Vector2 edge = p1 - p0;
                Vector3 outward = x * edge.y - y * edge.x;
                Vector3 o0 = x * p0.x + y * p0.y, o1 = x * p1.x + y * p1.y;
                Quad(mesh, back + o0, back + o1, front + o1, front + o0, outward);
            }

            Fan(mesh, profile, p => back + x * p.x + y * p.y, -z);
            Fan(mesh, profile, p => front + x * p.x + y * p.y, z);
        }

        // Caixa com as quinas ao longo do z chanfradas (as faces da frente e de trás ficam octogonais)
        public static void ChamferBox(MeshData mesh, Vector3 center, Vector3 size, Quaternion rotation, float chamfer) =>
            Extrude(mesh, center, rotation, Chamfered(size.x, size.y, chamfer), size.z);

        // Quadrilátero plano (a-b-c-d em volta) com a normal da própria face, virada para o lado de 'outward'
        static void Quad(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-8f)
                n = Vector3.Cross(c - b, a - b);
            if (n.sqrMagnitude < 1e-8f)
                return;
            if (Vector3.Dot(n, outward) < 0f)
                n = -n;
            mesh.AddQuadAuto(a, b, c, d, n.normalized, Tile);
        }

        // Tampa: leque a partir do centro do perfil
        static void Fan(MeshData mesh, Vector2[] profile, System.Func<Vector2, Vector3> place, Vector3 normal)
        {
            Vector2 middle = Vector2.zero;
            foreach (Vector2 p in profile)
                middle += p;
            middle /= profile.Length;
            Vector3 c = place(middle);
            for (int i = 0; i < profile.Length; i++)
                mesh.AddTriangleAuto(c, place(profile[i]), place(profile[(i + 1) % profile.Length]), normal, Tile);
        }
    }
}
