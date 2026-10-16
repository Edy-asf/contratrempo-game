using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Peças facetadas (sem curvas nem esferas) para os biomas: caixa, prisma (coluna de N lados) e arco de anel feito
    // de segmentos retos com seção quadrada. Tudo entra numa MeshData (uma malha por material), com a textura repetindo
    // a cada 2 m (o mesmo tamanho de pixel do resto do mapa).
    public static class BlockMesh
    {
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
            mesh.AddQuadAuto(c - u - v, c - u + v, c + u + v, c + u - v, (c - boxCenter).normalized);
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
                mesh.AddQuadAuto(ring0[i], ring1[i], ring1[j], ring0[j], outward);
            }

            if (!caps)
                return;
            for (int i = 1; i < sides - 1; i++)
            {
                mesh.AddTriangleAuto(ring0[0], ring0[i], ring0[i + 1], -axis);
                mesh.AddTriangleAuto(ring1[0], ring1[i], ring1[i + 1], axis);
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
                mesh.AddQuadAuto(previous[3], previous[2], next[2], next[3], radial);   // fora
                mesh.AddQuadAuto(previous[0], previous[1], next[1], next[0], -radial);  // dentro
                mesh.AddQuadAuto(previous[1], previous[2], next[2], next[1], normal);   // frente
                mesh.AddQuadAuto(previous[0], previous[3], next[3], next[0], -normal);  // trás
                previous = next;
            }

            // Pontas quebradas
            Vector3[] first = Corners(fromDeg), last = Corners(toDeg);
            Vector3 tangentStart = Vector3.Normalize(Corners(fromDeg + 1f)[0] - first[0]);
            Vector3 tangentEnd = Vector3.Normalize(last[0] - Corners(toDeg - 1f)[0]);
            mesh.AddQuadAuto(first[0], first[1], first[2], first[3], -tangentStart);
            mesh.AddQuadAuto(last[0], last[1], last[2], last[3], tangentEnd);
        }
    }
}
