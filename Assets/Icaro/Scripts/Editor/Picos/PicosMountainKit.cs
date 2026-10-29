using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Montanhas dos Picos acima das Nuvens no estilo dos pilares de arenito de Zhangjiajie: colunas de paredes quase
    // verticais (o alargamento fica escondido dentro da nuvem), sem pontas. Cada montanha junta vários pilares que se
    // sobrepõem, de alturas e larguras diferentes, e um pilar largo vira um feixe de colunas mais finas coladas (com
    // fendas fundas entre elas), para nada ficar com cara de tronco. Cada coluna é uma malha em anéis do topo até dentro
    // da nuvem fechada, facetada (cada face com a própria normal: rocha angulosa), e o contorno em cada altura soma:
    // - planta de blocos: um polígono irregular de 4 a 7 cantos (faces retas e quinas levemente arredondadas);
    // - camadas fortes empilhadas (lajes de alturas diferentes): cada camada tem a planta, o giro, o centro e a largura
    //   um pouco diferentes da de baixo, e entre duas camadas há uma junta recuada e escura; onde a de baixo é mais
    //   larga sobra um degrau (os estreitos ficam marcados para não pegar neve), onde é mais estreita, uma saliência;
    // - cinturas (a coluna afina e engrossa ao longo da altura), fendas verticais estreitas que serpenteiam, aspereza.
    // Topos: penhasco (ombro em dois degraus e o alto irregular), mesa (borda e meio quase plano) ou partido
    // (inclinado). Os anéis ficam mais espaçados abaixo das nuvens, onde nada aparece. UV0.x marca o alto (o shader
    // Icaro/SnowRock põe mais neve ali); a cor do vértice leva o que o shader precisa (branco = nada): R = quanto
    // escurece (juntas e fendas), G = 1 - quanto clareia (beira de cada camada), B = 0 nos degraus estreitos (sem
    // neve), A = 0 na capa de neve dos topos (sempre neve). A textura é projetada pelo mundo, então não há UV de verdade.
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

        // Malha do gelo (pingentes sob as cornijas de neve); o gerador do mapa liga antes de montar as montanhas
        public static MeshData Ice;

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
            public bool Bundle = true;          // largo (e sem topo de mesa): vira um feixe de colunas

            public PillarSpec Clone() => (PillarSpec)MemberwiseClone();
        }

        const float k_BundleRadius = 12f;       // acima disso, um pilar sem topo de mesa vira feixe
        const float k_NarrowStep = 2.5f;        // degraus mais estreitos que isso não pegam neve

        static System.Random s_Random;

        static float R(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

        // Detalhe pela distância: 0 perto do caminho, 1 meio, 2 longe
        static void Detail(PillarSpec spec, int detail, float radius)
        {
            spec.Sides = detail == 0 ? 30 : detail == 1 ? 22 : 15;
            spec.RingStep = detail == 0 ? 3f : detail == 1 ? 6f : 0f;     // anéis no meio das camadas (0 = nenhum)
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

        // Um pilar: uma coluna ou, se for largo, um feixe de colunas
        public static void Pillar(MeshData mesh, PillarSpec spec)
        {
            if (spec.Track)
                Pillars.Add(new Placed { Top = spec.Top, TopRadius = spec.TopRadius, BaseRadius = spec.BaseRadius, Taper = spec.Taper });
            if (spec.Bundle && spec.Cap != Cap.Flat && spec.TopRadius > k_BundleRadius)
                BuildBundle(mesh, spec);
            else
                Column(mesh, spec);
        }

        // Feixe: colunas mais finas coladas (a do meio mais alta e as outras espalhadas em espiral, caindo de altura
        // para fora), cada uma com as próprias camadas, cantos e topo
        static void BuildBundle(MeshData mesh, PillarSpec spec)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(spec.TopRadius / 7f), 3, 10);
            float baseRatio = spec.BaseRadius / spec.TopRadius;
            float capRatio = spec.TopRadius > 0f ? spec.CapHeight / spec.TopRadius : 0.25f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 137.5f + spec.Seed * 7f) * Mathf.Deg2Rad;
                float distance = i == 0 ? 0f : spec.TopRadius * 0.62f * Mathf.Sqrt(i / (float)(count - 1));
                float radius = spec.TopRadius * (i == 0 ? 0.56f : Mathf.Lerp(0.36f, 0.52f, R01(spec.Seed, i * 3.3f)));
                float drop = i == 0 ? 0f : spec.TopRadius * Mathf.Lerp(0.1f, 1.2f, R01(spec.Seed, i * 5.1f)) * (0.5f + distance / spec.TopRadius);
                PillarSpec sub = spec.Clone();
                sub.Top = spec.Top + new Vector3(Mathf.Cos(angle) * distance, -drop, Mathf.Sin(angle) * distance);
                sub.TopRadius = radius;
                sub.BaseRadius = radius * baseRatio;
                sub.CapHeight = radius * capRatio;
                sub.Seed = spec.Seed + i * 13.7f;
                sub.Corners = 4 + Mathf.FloorToInt(R01(sub.Seed, 9.2f) * 4f);
                sub.Bundle = false;
                sub.Track = false;
                Column(mesh, sub);
            }
        }

        struct Ring
        {
            public float Y;
            public int Bed;
            public float Scale;     // largura extra deste anel (a junta é mais estreita)
            public float Cavity;    // quanto escurece (1 = nada)
            public float Edge;      // quanto clareia (0 = nada)
            public bool Cap;
            public float Shrink;
        }

        // Anéis do topo à base: o ombro do penhasco (se houver) e, em cada camada, a beira de cima (clara), anéis do
        // meio, o pé da camada e a junta recuada (escura) antes da camada de baixo
        static List<Ring> Rings(PillarSpec spec)
        {
            var rings = new List<Ring>();
            if (spec.Cap == Cap.Crag)
            {
                rings.Add(new Ring { Y = spec.Top.y + spec.CapHeight, Cap = true, Shrink = 0.74f, Scale = 1f, Cavity = 1f, Edge = 0.4f });
                rings.Add(new Ring { Y = spec.Top.y + spec.CapHeight * 0.62f, Cap = true, Shrink = 0.94f, Scale = 1f, Cavity = 1f, Edge = 0.25f });
            }

            float y = spec.Top.y;
            for (int bed = 0; y > spec.BottomY; bed++)
            {
                if (y < k_HiddenBelow)
                {
                    rings.Add(new Ring { Y = y, Bed = bed, Scale = 1f, Cavity = 1f });
                    y -= k_HiddenStep;
                    continue;
                }

                float height = spec.BedHeight * Mathf.Lerp(0.55f, 1.45f, R01(spec.Seed, bed * 2.3f + 0.7f));
                float bottom = Mathf.Max(y - height, spec.BottomY);
                rings.Add(new Ring { Y = y, Bed = bed, Scale = 1f, Cavity = 1f, Edge = 0.55f });
                if (spec.RingStep > 0.5f)
                    for (float inner = y - spec.RingStep; inner > bottom + 1f; inner -= spec.RingStep)
                        rings.Add(new Ring { Y = inner, Bed = bed, Scale = 1f, Cavity = 1f });
                rings.Add(new Ring { Y = bottom + 0.5f, Bed = bed, Scale = 1f, Cavity = 0.85f });
                rings.Add(new Ring { Y = bottom, Bed = bed, Scale = 0.9f, Cavity = 0.45f });
                y = bottom - 0.35f;
            }

            rings.Add(new Ring { Y = spec.BottomY, Bed = rings.Count, Scale = 1f, Cavity = 1f });
            return rings;
        }

        // Uma coluna: os anéis pela forma das camadas, as faces facetadas com a cor do vértice e o fecho do alto
        static void Column(MeshData mesh, PillarSpec spec)
        {
            int sides = Mathf.Clamp(spec.Corners * 3 + Mathf.RoundToInt(spec.TopRadius * 0.5f), spec.Corners * 2, Mathf.Max(spec.Sides, spec.Corners * 2));
            List<Ring> rings = Rings(spec);
            int count = rings.Count;
            var grid = new Vector3[count, sides];
            var colors = new Color[count, sides];
            var radii = new float[count, sides];
            var bias = new float[count];
            float height = spec.Top.y - spec.BottomY;
            float tilt = R01(spec.Seed, 3.1f) * Mathf.PI * 2f;
            var angles = new float[spec.Corners];
            var radiiPlan = new float[spec.Corners];
            int planBed = int.MinValue;
            Vector2 bedOffset = Vector2.zero;
            float bedScale = 1f;

            for (int i = 0; i < count; i++)
            {
                Ring ring = rings[i];
                float t = ring.Cap ? 0f : Mathf.Clamp01((spec.Top.y - ring.Y) / height);
                if (!ring.Cap && ring.Bed != planBed)
                {
                    planBed = ring.Bed;
                    Plan(spec, planBed, angles, radiiPlan, out bedOffset);
                    // Largura de cada camada: umas mais largas, outras mais estreitas (degraus e saliências)
                    bedScale = 1f + (R01(spec.Seed, planBed * 1.7f + 0.3f) - 0.5f) * spec.BedShift * 3.2f;
                }
                else if (ring.Cap && planBed == int.MinValue)
                {
                    planBed = 0;
                    Plan(spec, 0, angles, radiiPlan, out bedOffset);
                    bedScale = 1f + (R01(spec.Seed, 0.3f) - 0.5f) * spec.BedShift * 3.2f;
                }

                float waist = 1f + spec.Waist * (Mathf.PerlinNoise(spec.Seed * 0.7f, ring.Y * 0.018f) - 0.5f) * 2f;
                Vector2 center = new Vector2(spec.Top.x, spec.Top.z) - spec.Lean * Mathf.Pow(t, 0.7f);
                float radius = Mathf.Lerp(spec.TopRadius, spec.BaseRadius, Mathf.Pow(t, spec.Taper)) * waist * bedScale * ring.Scale *
                               (ring.Cap ? ring.Shrink : 1f);
                center += bedOffset * radius;
                bias[i] = Mathf.Clamp01(1f - (spec.Top.y - ring.Y) / 6f) * 0.6f + (ring.Cap ? 0.4f : 0f);

                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);

                    // Fendas: estreitas, serpenteando com a altura; o fundo delas escurece
                    float warp = (Mathf.PerlinNoise(spec.Seed + ring.Y * 0.012f, spec.Seed * 0.5f) - 0.5f) * 2f;
                    float phase = a * spec.Flutes + Mathf.Sin(a * 2f + spec.Seed) * 1.1f + warp;
                    float dip = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(phase * 0.5f)), 8f);
                    float flute = 1f - spec.FluteDepth * 1.6f * dip;

                    float rough = 1f + spec.Roughness * (Mathf.PerlinNoise(a * 3f + spec.Seed, ring.Y * 0.06f) - 0.5f);
                    float r = radius * PlanRadius(a, angles, radiiPlan) * flute * rough;
                    float yy = ring.Y;
                    if (ring.Cap)
                        yy += spec.CapHeight * 0.6f * (Mathf.PerlinNoise(spec.Seed + ca * 1.4f, spec.Seed + sa * 1.4f) - 0.5f);
                    else if (spec.Cap == Cap.Tilted && i == 0)
                        yy += spec.CapHeight * (0.5f + 0.5f * Mathf.Cos(a - tilt));
                    grid[i, j] = new Vector3(center.x + ca * r, yy, center.y + sa * r);
                    radii[i, j] = r;
                    colors[i, j] = new Color(Mathf.Min(ring.Cavity, 1f - 0.55f * dip), 1f - ring.Edge, 1f, 1f);
                }
            }

            // Faces facetadas; os degraus estreitos (face virada para cima e pouca largura) não pegam neve
            for (int i = 0; i + 1 < count; i++)
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                Vector3 a = grid[i, j], b = grid[i, k], c = grid[i + 1, k], d = grid[i + 1, j];
                Color ca = colors[i, j], cb = colors[i, k], cc = colors[i + 1, k], cd = colors[i + 1, j];
                Vector3 normal = Vector3.Cross(b - d, c - a).normalized;
                // O ombro do penhasco faz parte do alto: sempre neve (estreito, ele virava um anel de rocha em volta da capa)
                if (rings[i].Cap || rings[i + 1].Cap)
                    ca.a = cb.a = cc.a = cd.a = 0f;
                else if (normal.y > 0.45f)
                {
                    float width = Mathf.Max(Mathf.Abs(radii[i + 1, j] - radii[i, j]), Mathf.Abs(radii[i + 1, k] - radii[i, k]));
                    if (width < k_NarrowStep)
                        ca.b = cb.b = cc.b = cd.b = 0f;
                }
                FacetQuad(mesh, a, b, c, d, normal, ca, cb, cc, cd, bias[i], bias[i + 1]);
            }

            // Fecho do alto: na mesa (embaixo de praças e ressaltos), um leque plano; nos outros, a capa de neve
            Vector3 sum = Vector3.zero;
            var rim = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                rim[j] = grid[0, j];
                sum += rim[j];
            }
            Vector3 middle = sum / sides;
            if (spec.Cap != Cap.Flat)
            {
                SnowCap(mesh, spec, rim, middle);
                return;
            }

            Vector3 apex = middle + Vector3.up * spec.Dome;
            for (int j = 0; j < sides; j++)
            {
                Vector3 a = rim[j], b = rim[(j + 1) % sides];
                Vector3 normal = Vector3.Cross(b - a, apex - a).normalized;
                if (normal.y < 0f)
                    normal = -normal;
                FacetTri(mesh, a, b, apex, normal, new Color(1f, 0.6f, 1f, 1f), 1f);
            }
        }

        // Capa de neve no alto de uma coluna, toda coberta (cor do vértice com A = 0: o shader põe neve até nas faces
        // íngremes da cornija): uma cornija que deborda na beira (o pé dela fica para fora e abaixo da rocha), montes
        // ondulados subindo até o meio e pingentes de gelo pendurados sob a cornija
        static void SnowCap(MeshData mesh, PillarSpec spec, Vector3[] rim, Vector3 middle)
        {
            int sides = rim.Length;
            float depth = Mathf.Clamp(spec.TopRadius * 0.07f, 0.4f, 2.4f);
            float lip = Mathf.Clamp(spec.TopRadius * 0.05f, 0.3f, 1.4f);
            var rings = new Vector3[4, sides];
            for (int j = 0; j < sides; j++)
            {
                Vector3 edge = rim[j];
                Vector3 outward = new Vector3(edge.x - middle.x, 0f, edge.z - middle.z).normalized;
                float a = j * Mathf.PI * 2f / sides;
                float Dune(float t) => depth * 0.9f * (Mathf.PerlinNoise(spec.Seed + Mathf.Cos(a) * 2.2f * t + 3f,
                    spec.Seed + Mathf.Sin(a) * 2.2f * t) - 0.5f);
                Vector3 inner = new Vector3(middle.x, edge.y, middle.z);
                float sag = R01(spec.Seed, j * 0.71f) * 0.3f;   // a cornija não é uniforme
                rings[0, j] = edge + outward * lip * (1f + sag) + Vector3.down * lip * 0.6f;
                rings[1, j] = edge + outward * lip * 0.55f + Vector3.up * depth * (0.5f + sag);
                rings[2, j] = Vector3.Lerp(inner, edge, 0.72f) + Vector3.up * (depth + Dune(0.72f));
                rings[3, j] = Vector3.Lerp(inner, edge, 0.4f) + Vector3.up * (depth * 1.15f + Dune(0.4f));
            }
            Vector3 apex = middle + Vector3.up * (depth * 1.25f + (spec.Cap == Cap.Crag ? spec.CapHeight * 0.2f : 0f));

            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                Vector3 outward = new Vector3(rim[j].x - middle.x, 0f, rim[j].z - middle.z).normalized;
                // Embaixo da cornija (da beira da rocha até o pé dela), a face da cornija e os montes
                SnowQuad(mesh, rim[j], rim[k], rings[0, k], rings[0, j], outward + Vector3.down, 0f);
                SnowQuad(mesh, rings[0, j], rings[0, k], rings[1, k], rings[1, j], outward + Vector3.up * 0.5f, 1f);
                SnowQuad(mesh, rings[1, j], rings[1, k], rings[2, k], rings[2, j], Vector3.up, 1f);
                SnowQuad(mesh, rings[2, j], rings[2, k], rings[3, k], rings[3, j], Vector3.up, 1f);
                Vector3 normal = Vector3.Cross(rings[3, k] - rings[3, j], apex - rings[3, j]).normalized;
                if (normal.y < 0f)
                    normal = -normal;
                FacetTri(mesh, rings[3, j], rings[3, k], apex, normal, k_SnowCover, 1f);

                // Pingentes pendurados sob a cornija
                if (Ice != null && j % 2 == 0 && R01(spec.Seed, j * 1.9f) > 0.3f)
                    Icicle(rings[0, j] + Vector3.down * 0.05f, lip * Mathf.Lerp(1.5f, 4.5f, R01(spec.Seed, j * 2.7f)),
                        lip * 0.35f);
            }
        }

        static readonly Color k_SnowCover = new Color(1f, 1f, 1f, 0f);     // A = 0: sempre neve

        // Quadrilátero da capa de neve virado para o lado 'facing' ('bias' 1 = mais neve em volta)
        static void SnowQuad(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, float bias)
        {
            Vector3 normal = Vector3.Cross(b - d, c - a).normalized;
            if (Vector3.Dot(normal, facing) < 0f)
                normal = -normal;
            FacetQuad(mesh, a, b, c, d, normal, k_SnowCover, k_SnowCover, k_SnowCover, k_SnowCover, bias, bias);
        }

        // Pingente de gelo: cone de quatro faces apontando para baixo
        static void Icicle(Vector3 top, float length, float radius)
        {
            Vector3 tip = top + Vector3.down * length;
            for (int c = 0; c < 4; c++)
            {
                float a0 = c * Mathf.PI * 0.5f, a1 = (c + 1) * Mathf.PI * 0.5f;
                Vector3 a = top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 b = top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 normal = Vector3.Cross(b - a, tip - a).normalized;
                if (Vector3.Dot(normal, (a + b) * 0.5f - top) < 0f)
                    normal = -normal;
                FacetTri(Ice, a, b, tip, normal, Color.white, 0f);
            }
        }

        // Quadrilátero facetado (4 vértices próprios com a normal da face), virado para a normal
        static void FacetQuad(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color ca, Color cb,
            Color cc, Color cd, float biasTop, float biasBottom)
        {
            mesh.PadColors();
            int start = mesh.Vertices.Count;
            mesh.Vertices.Add(a);
            mesh.Vertices.Add(b);
            mesh.Vertices.Add(c);
            mesh.Vertices.Add(d);
            for (int i = 0; i < 4; i++)
                mesh.Normals.Add(normal);
            mesh.Uvs.Add(new Vector2(biasTop, 0f));
            mesh.Uvs.Add(new Vector2(biasTop, 0f));
            mesh.Uvs.Add(new Vector2(biasBottom, 0f));
            mesh.Uvs.Add(new Vector2(biasBottom, 0f));
            mesh.Colors.Add(ca);
            mesh.Colors.Add(cb);
            mesh.Colors.Add(cc);
            mesh.Colors.Add(cd);
            AddTri(mesh, start, start + 1, start + 2);
            AddTri(mesh, start, start + 2, start + 3);
        }

        static void FacetTri(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color, float bias)
        {
            mesh.PadColors();
            int start = mesh.Vertices.Count;
            foreach (Vector3 p in new[] { a, b, c })
            {
                mesh.Vertices.Add(p);
                mesh.Normals.Add(normal);
                mesh.Uvs.Add(new Vector2(bias, 0f));
                mesh.Colors.Add(color);
            }
            AddTri(mesh, start, start + 1, start + 2);
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
