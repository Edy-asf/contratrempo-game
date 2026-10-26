using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Montanhas dos Picos acima das Nuvens no estilo dos pilares de arenito de Zhangjiajie: colunas de paredes quase
    // verticais (o alargamento fica escondido dentro da nuvem), sem pontas. Cada montanha junta vários pilares que se
    // sobrepõem, de alturas e larguras diferentes. Cada pilar é uma malha em anéis do topo até dentro da nuvem fechada,
    // com normais suaves, e o contorno em cada altura soma:
    // - planta de blocos: um polígono irregular de 4 a 7 cantos (faces retas e quinas levemente arredondadas);
    // - camadas empilhadas: a cada camada (10 a 26 m) o bloco muda um pouco (cantos, giro e centro deslocados), o que
    //   dá os degraus, saliências e reentrâncias das pilhas de blocos;
    // - cinturas (o pilar afina e engrossa ao longo da altura), fendas verticais estreitas que serpenteiam, e aspereza.
    // Topos: penhasco (ombro arredondado em dois degraus e o alto irregular, um pouco mais alto em partes), mesa (borda
    // e meio quase plano) ou partido (inclinado, mais alto de um lado). Os anéis ficam mais espaçados abaixo das nuvens,
    // onde nada aparece. UV0.x marca o alto (o shader Icaro/SnowRock põe mais neve ali); a textura é projetada pelo
    // mundo, então não há UV de verdade.
    public static class PicosMountainKit
    {
        public enum Kind { Column, Tower, Cluster, Crown, Ridge, Broken }
        public enum Cap { Crag, Flat, Tilted }

        public const float Bottom = -230f;      // os pilares descem até dentro da nuvem fechada
        const float k_HiddenBelow = -125f;      // abaixo daqui os anéis espaçam (nada aparece dentro da nuvem)
        const float k_HiddenStep = 18f;

        // Pilar já colocado (para os fiapos de névoa se enrolarem nele)
        public struct Placed
        {
            public Vector3 Top;
            public float TopRadius, BaseRadius, Taper;

            public float RadiusAt(float y)
            {
                float t = Mathf.Clamp01((Top.y - y) / (Top.y - Bottom));
                return Mathf.Lerp(TopRadius, BaseRadius, Mathf.Pow(t, Taper));
            }
        }

        public static readonly List<Placed> Pillars = new List<Placed>();

        public class PillarSpec
        {
            public Vector3 Top;                 // centro do topo do corpo (o ombro do penhasco sobe um pouco acima)
            public float BottomY = Bottom;
            public float TopRadius, BaseRadius;
            public float Taper = 3.5f;          // alto: as paredes ficam quase retas acima das nuvens
            public Vector2 Lean;                // deslocamento do topo em relação à base (m)
            public Cap Cap = Cap.Crag;
            public float CapHeight;             // penhasco: altura do ombro; partido: desnível do topo
            public float Dome;                  // mesa: quanto o meio sobe
            public int Sides = 32;
            public float RingStep = 3f;
            public int Corners = 5;             // cantos da planta de blocos
            public int Flutes = 4;
            public float FluteDepth = 0.08f;
            public float BedHeight = 16f;       // altura de cada camada de blocos
            public float BedShift = 0.07f;      // quanto cada camada muda em relação à de baixo
            public float Waist = 0.16f;         // afina e engrossa ao longo da altura
            public float Roughness = 0.06f;
            public float Seed;
            public bool Track = true;           // entra na lista de pilares (fiapos)
        }

        static System.Random s_Random;

        static float R(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

        // Detalhe pela distância: 0 perto do caminho, 1 meio, 2 longe
        static void Detail(PillarSpec spec, int detail, float radius)
        {
            int maxSides = detail == 0 ? 48 : detail == 1 ? 32 : 20;
            spec.Sides = Mathf.Clamp(Mathf.RoundToInt(radius * (detail == 0 ? 2.4f : detail == 1 ? 1.5f : 1f)), 14, maxSides);
            spec.RingStep = detail == 0 ? 2.5f : detail == 1 ? 4f : 7f;
        }

        // Montanha inteira de um tipo, com 'size' de raio do corpo principal no alto e o topo em 'top'
        public static void Mountain(MeshData mesh, Kind kind, Vector3 top, float size, System.Random random, int detail)
        {
            s_Random = random;
            float above = Mathf.Max(20f, top.y + 100f);  // altura acima da superfície das nuvens
            switch (kind)
            {
                case Kind.Column:
                {
                    // Coluna alta e esguia, com colunas mais baixas encostadas
                    Pillar(mesh, Spec(top, size * 0.5f, size * 0.8f, detail));
                    int subs = s_Random.Next(1, 4);
                    for (int k = 0; k < subs; k++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = size * R(0.5f, 0.85f);
                        Vector3 p = top + new Vector3(Mathf.Cos(a) * d, -above * R(0.25f, 0.6f), Mathf.Sin(a) * d);
                        float r = size * R(0.25f, 0.4f);
                        Pillar(mesh, Spec(p, r, r * R(1.3f, 1.6f), detail));
                    }

                    break;
                }
                case Kind.Tower:
                case Kind.Broken:
                case Kind.Crown:
                {
                    PillarSpec main = Spec(top, size, size * R(1.3f, 1.5f), detail);
                    main.Cap = kind == Kind.Broken ? Cap.Tilted : kind == Kind.Tower ? Cap.Flat : Cap.Crag;
                    main.CapHeight = kind == Kind.Broken ? size * R(0.35f, 0.6f) : size * 0.18f;
                    main.Dome = size * 0.05f;
                    Pillar(mesh, main);

                    // Colunas mais baixas encostadas no corpo
                    int buttresses = s_Random.Next(2, 5);
                    for (int k = 0; k < buttresses; k++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = size * R(0.65f, 0.95f);
                        Vector3 p = top + new Vector3(Mathf.Cos(a) * d, -above * R(0.3f, 0.7f), Mathf.Sin(a) * d);
                        float r = size * R(0.3f, 0.5f);
                        Pillar(mesh, Spec(p, r, r * R(1.3f, 1.6f), detail));
                    }

                    if (kind == Kind.Crown)
                    {
                        // Blocos soltos no alto: tocos de coluna curtos e largos sobre o topo, perto da borda
                        int stubs = s_Random.Next(3, 6);
                        for (int k = 0; k < stubs; k++)
                        {
                            float a = (k + R(0f, 0.6f)) / stubs * Mathf.PI * 2f, d = size * R(0.35f, 0.6f);
                            float r = size * R(0.2f, 0.32f);
                            Vector3 p = top + new Vector3(Mathf.Cos(a) * d, size * 0.18f + R(2f, 7f), Mathf.Sin(a) * d);
                            PillarSpec stub = Spec(p, r, r * 1.05f, detail);
                            stub.BottomY = top.y;
                            stub.BedHeight = R(3f, 6f);
                            stub.CapHeight = r * 0.25f;
                            stub.Track = false;
                            Pillar(mesh, stub);
                        }
                    }

                    break;
                }
                case Kind.Cluster:
                {
                    int count = s_Random.Next(4, 8);
                    for (int k = 0; k < count; k++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = k == 0 ? 0f : size * R(0.55f, 1.2f);
                        Vector3 p = top + new Vector3(Mathf.Cos(a) * d, k == 0 ? 0f : -above * R(0.05f, 0.65f), Mathf.Sin(a) * d);
                        float r = size * (k == 0 ? 0.55f : R(0.3f, 0.5f));
                        PillarSpec spec = Spec(p, r, r * R(1.3f, 1.7f), detail);
                        spec.Cap = s_Random.NextDouble() < 0.2 ? Cap.Tilted : Cap.Crag;
                        spec.CapHeight = spec.Cap == Cap.Tilted ? r * R(0.3f, 0.6f) : r * 0.2f;
                        spec.Lean = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * R(0f, 0.35f);
                        Pillar(mesh, spec);
                    }

                    break;
                }
                case Kind.Ridge:
                {
                    // Paredão: colunas lado a lado, de alturas diferentes, formando uma muralha recortada
                    float heading = R(0f, Mathf.PI * 2f);
                    var dir = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
                    int count = s_Random.Next(5, 9);
                    for (int k = 0; k < count; k++)
                    {
                        float along = (k - (count - 1) * 0.5f) * size * R(0.6f, 0.8f);
                        float rise = Mathf.Cos((k / (float)(count - 1) - 0.5f) * Mathf.PI) * R(0.7f, 1f);
                        Vector3 p = top + dir * along + Vector3.up * (-above * (1f - rise) * 0.6f);
                        float r = size * R(0.4f, 0.6f);
                        PillarSpec spec = Spec(p, r, r * R(1.4f, 1.8f), detail);
                        if (s_Random.NextDouble() < 0.3)
                        {
                            spec.Cap = Cap.Tilted;
                            spec.CapHeight = r * R(0.3f, 0.6f);
                        }
                        Pillar(mesh, spec);
                    }

                    break;
                }
            }
        }

        static PillarSpec Spec(Vector3 top, float topRadius, float baseRadius, int detail)
        {
            var spec = new PillarSpec
            {
                Top = top,
                TopRadius = topRadius,
                BaseRadius = baseRadius,
                Taper = R(3f, 4.5f),
                Cap = Cap.Crag,
                CapHeight = topRadius * R(0.15f, 0.3f),
                Corners = s_Random.Next(4, 8),
                Flutes = s_Random.Next(3, 7),
                FluteDepth = R(0.05f, 0.11f),
                BedHeight = R(10f, 26f),
                BedShift = R(0.05f, 0.1f),
                Waist = R(0.08f, 0.22f),
                Roughness = R(0.04f, 0.08f),
                Seed = R(0f, 1000f),
            };
            Detail(spec, detail, topRadius * 1.5f + 4f);
            return spec;
        }

        // Planta de uma camada: polígono irregular (ângulo e distância de cada canto)
        static void Plan(PillarSpec spec, int bed, float[] angles, float[] radii, out Vector2 offset)
        {
            int n = angles.Length;
            float spin = (R01(spec.Seed + bed, 5.3f) - 0.5f) * spec.BedShift * 1.5f;
            for (int k = 0; k < n; k++)
            {
                float baseAngle = (k + (R01(spec.Seed, k * 1.7f) - 0.5f) * 0.6f) / n * Mathf.PI * 2f;
                angles[k] = baseAngle + spin;
                float baseRadius = 0.85f + R01(spec.Seed, k * 2.9f + 0.4f) * 0.3f;
                radii[k] = baseRadius * (1f + (R01(spec.Seed + bed * 3.1f, k * 4.3f) - 0.5f) * spec.BedShift * 2f);
            }

            offset = new Vector2(R01(spec.Seed + bed, 7.7f) - 0.5f, R01(spec.Seed + bed, 9.1f) - 0.5f) * spec.BedShift;
        }

        // Distância do centro até a borda do polígono na direção 'a' (faces retas), com as quinas arredondadas
        static float PlanRadius(float a, float[] angles, float[] radii)
        {
            int n = angles.Length;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float best = float.MaxValue, sum = 0f;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = new Vector2(Mathf.Cos(angles[k]), Mathf.Sin(angles[k])) * radii[k];
                Vector2 q = new Vector2(Mathf.Cos(angles[(k + 1) % n]), Mathf.Sin(angles[(k + 1) % n])) * radii[(k + 1) % n];
                sum += radii[k];
                // Raio a partir do centro contra o segmento p-q
                Vector2 e = q - p;
                float den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-6f)
                    continue;
                float t = (p.x * e.y - p.y * e.x) / den;
                float u = (p.x * dir.y - p.y * dir.x) / den;
                if (t > 0f && u >= -0.001f && u <= 1.001f)
                    best = Mathf.Min(best, t);
            }

            float round = sum / n;
            return Mathf.Lerp(best == float.MaxValue ? round : best, round, 0.18f);
        }

        // Um pilar: anéis do topo à base, o topo pela forma pedida e as normais suaves pela grade
        public static void Pillar(MeshData mesh, PillarSpec spec)
        {
            if (spec.Track)
                Pillars.Add(new Placed { Top = spec.Top, TopRadius = spec.TopRadius, BaseRadius = spec.BaseRadius, Taper = spec.Taper });

            int sides = spec.Sides;
            var heights = new List<float>();
            for (float y = spec.Top.y; y > spec.BottomY; y -= y > k_HiddenBelow ? spec.RingStep : k_HiddenStep)
                heights.Add(y);
            heights.Add(spec.BottomY);

            // Penhasco: dois anéis do ombro acima do topo do corpo (altura e encolhimento), do mais alto para baixo
            var shoulder = new List<Vector2>();
            if (spec.Cap == Cap.Crag)
            {
                shoulder.Add(new Vector2(1f, 0.74f));
                shoulder.Add(new Vector2(0.62f, 0.94f));
            }

            int rings = shoulder.Count + heights.Count;
            var grid = new Vector3[rings, sides];
            var bias = new float[rings];
            float height = spec.Top.y - spec.BottomY;
            float tilt = R01(spec.Seed, 3.1f) * Mathf.PI * 2f;
            var angles = new float[spec.Corners];
            var radii = new float[spec.Corners];
            int planBed = int.MinValue;
            Vector2 bedOffset = Vector2.zero;

            for (int i = 0; i < rings; i++)
            {
                bool cap = i < shoulder.Count;
                float y, t, shrink = 1f;
                if (cap)
                {
                    y = spec.Top.y + spec.CapHeight * shoulder[i].x;
                    shrink = shoulder[i].y;
                    t = 0f;
                }
                else
                {
                    y = heights[i - shoulder.Count];
                    t = (spec.Top.y - y) / height;
                }

                // Camada de blocos (a borda entre camadas ondula de leve)
                int bed = Mathf.FloorToInt((spec.Top.y - y) / spec.BedHeight + 0.0001f);
                if (bed != planBed)
                {
                    planBed = bed;
                    Plan(spec, bed, angles, radii, out bedOffset);
                }

                float waist = 1f + spec.Waist * (Mathf.PerlinNoise(spec.Seed * 0.7f, y * 0.018f) - 0.5f) * 2f;
                Vector2 center = new Vector2(spec.Top.x, spec.Top.z) - spec.Lean * Mathf.Pow(t, 0.7f);
                float radius = Mathf.Lerp(spec.TopRadius, spec.BaseRadius, Mathf.Pow(t, spec.Taper)) * shrink * waist;
                center += bedOffset * radius;
                bias[i] = Mathf.Clamp01(1f - (spec.Top.y - y) / 6f) * 0.6f + (cap ? 0.4f : 0f);

                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);

                    // Fendas: estreitas, serpenteando com a altura, espaçamento irregular
                    float warp = (Mathf.PerlinNoise(spec.Seed + y * 0.012f, spec.Seed * 0.5f) - 0.5f) * 2f;
                    float phase = a * spec.Flutes + Mathf.Sin(a * 2f + spec.Seed) * 1.1f + warp;
                    float s = Mathf.Abs(Mathf.Sin(phase * 0.5f));
                    float flute = 1f - spec.FluteDepth * Mathf.Pow(1f - s, 12f);

                    float rough = 1f + spec.Roughness * (Mathf.PerlinNoise(a * 3f + spec.Seed, y * 0.06f) - 0.5f) +
                                  spec.Roughness * 0.5f * (Mathf.PerlinNoise(a * 9f + spec.Seed, y * 0.2f) - 0.5f);

                    float r = radius * PlanRadius(a, angles, radii) * flute * rough;
                    float yy = y;
                    if (cap)
                        // O alto do penhasco fica irregular: partes mais altas que outras
                        yy += spec.CapHeight * 0.6f * (Mathf.PerlinNoise(spec.Seed + ca * 1.4f, spec.Seed + sa * 1.4f) - 0.5f);
                    else if (spec.Cap == Cap.Tilted && i == 0)
                        yy += spec.CapHeight * (0.5f + 0.5f * Mathf.Cos(a - tilt));
                    grid[i, j] = new Vector3(center.x + ca * r, yy, center.y + sa * r);
                }
            }

            // Vértices com normais suaves (diferenças pela grade; a volta fecha)
            int start = mesh.Vertices.Count;
            for (int i = 0; i < rings; i++)
            for (int j = 0; j <= sides; j++)
            {
                int jj = j % sides;
                Vector3 around = grid[i, (jj + 1) % sides] - grid[i, (jj + sides - 1) % sides];
                Vector3 down = grid[Mathf.Min(i + 1, rings - 1), jj] - grid[Mathf.Max(i - 1, 0), jj];
                Vector3 normal = Vector3.Cross(around, down).normalized;
                Vector3 outward = grid[i, jj] - new Vector3(spec.Top.x, grid[i, jj].y, spec.Top.z);
                if (Vector3.Dot(normal, outward) < 0f)
                    normal = -normal;
                mesh.Vertices.Add(grid[i, jj]);
                mesh.Normals.Add(normal);
                mesh.Uvs.Add(new Vector2(bias[i], 0f));
            }

            for (int i = 0; i + 1 < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = start + i * (sides + 1) + j, b = a + 1, c = a + sides + 2, d = a + sides + 1;
                AddTri(mesh, a, b, c);
                AddTri(mesh, a, c, d);
            }

            // Fecho do alto: leque até o meio, que sobe um pouco; a borda ganha vértices próprios virados para cima
            Vector3 sum = Vector3.zero;
            for (int j = 0; j < sides; j++)
                sum += grid[0, j];
            Vector3 apex = sum / sides + Vector3.up * (spec.Cap == Cap.Crag ? spec.CapHeight * 0.25f : spec.Dome);

            int rim = mesh.Vertices.Count;
            for (int j = 0; j <= sides; j++)
            {
                Vector3 p = grid[0, j % sides];
                Vector3 outward = new Vector3(p.x - apex.x, 0f, p.z - apex.z).normalized;
                mesh.Vertices.Add(p);
                mesh.Normals.Add((Vector3.up * 3f + outward).normalized);
                mesh.Uvs.Add(new Vector2(1f, 0f));
            }

            int apexIndex = mesh.Vertices.Count;
            mesh.Vertices.Add(apex);
            mesh.Normals.Add(Vector3.up);
            mesh.Uvs.Add(new Vector2(1f, 0f));
            for (int j = 0; j < sides; j++)
                AddTri(mesh, rim + j, apexIndex, rim + j + 1);
        }

        // Triângulo virado para fora (pela normal dos vértices)
        static void AddTri(MeshData mesh, int a, int b, int c)
        {
            Vector3 pa = mesh.Vertices[a], pb = mesh.Vertices[b], pc = mesh.Vertices[c];
            Vector3 normal = mesh.Normals[a] + mesh.Normals[b] + mesh.Normals[c];
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), normal) < 0f)
                (b, c) = (c, b);
            mesh.Triangles.Add(a);
            mesh.Triangles.Add(b);
            mesh.Triangles.Add(c);
        }

        static float R01(float seed, float salt) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f, 1f);
    }
}
