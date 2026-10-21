using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Peças trabalhadas das ruínas do Arquipélago (todas facetadas, sem curvas): anéis feitos de blocos chanfrados com
    // canaleta na face, aros grossos a cada trecho e pontas quebradas em dente com pedaços caídos; muros de blocos
    // aparelhados com o topo quebrado; colunas com base, tambores e capitel; torres com cornijas, contrafortes, vãos de
    // janela e topo em ruína; arcos de aduelas; trepadeiras penduradas (cartas). Cada bloco sorteia um tom de pedra
    // (RuinMesh), e o musgo e a pedra molhada saem no RuinMesh.Split.
    public static class ArquipelagoRuinKit
    {
        // Trepadeiras: o gerador liga antes de montar (sem elas, nada é pendurado)
        public static GridSceneKit.Cards Vines;
        public static Material VineMaterial;
        public static Vector2 VineSize = new Vector2(1f, 3f);

        static float R(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ---------- anéis ----------

        // Anel de blocos: 'thickness' no raio e 'width' ao longo da normal, de 'fromDeg' a 'toDeg' (0° = 'zero').
        // Pontas quebradas (brokenStart/End) terminam em dente e soltam pedaços até 'groundY'.
        public static void RichRing(RuinMesh mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius,
            float thickness, float width, float fromDeg, float toDeg, System.Random random, bool brokenStart = true,
            bool brokenEnd = true, float groundY = 0f, int collarEvery = 3)
        {
            normal = normal.normalized;
            float span = toDeg - fromDeg, sign = Mathf.Sign(span);
            float arcLength = radius * Mathf.Abs(span) * Mathf.Deg2Rad;
            int blocks = Mathf.Max(2, Mathf.RoundToInt(arcLength / Mathf.Max(thickness * 1.5f, 1.2f)));
            float step = span / blocks;
            float chamfer = Mathf.Min(thickness, width) * 0.12f;
            Vector2[] body = BlockMesh.Grooved(thickness, width, chamfer, 0.42f, width * 0.08f);
            Vector2[] cap = BlockMesh.Chamfered(thickness, width, chamfer);
            Vector2[] collar = BlockMesh.Chamfered(thickness * 1.5f, width * 1.35f, chamfer * 1.5f);
            float collarDeg = thickness * 0.45f / radius * Mathf.Rad2Deg * sign;
            int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(step) / 5f));

            for (int i = 0; i < blocks; i++)
            {
                // Blocos encostados (as pontas fechadas cobrem a junta) e levemente desalinhados
                float a0 = fromDeg + i * step, a1 = fromDeg + (i + 1) * step;
                float r = radius + R(random, -0.03f, 0.03f) * thickness;
                Vector3 shifted = center + normal * (R(random, -0.025f, 0.025f) * width);
                bool jagStart = i == 0 && brokenStart, jagEnd = i == blocks - 1 && brokenEnd;
                if (jagStart || jagEnd)
                    Jagged(mesh.Pick(), shifted, normal, zero, r, thickness, width, chamfer, a0, a1, jagStart,
                        segments);
                else
                    BlockMesh.SweepArc(mesh.Pick(), shifted, normal, zero, r, body, a0, a1, segments, cap);

                if (collarEvery > 0 && i > 0 && i % collarEvery == 0)
                {
                    float a = fromDeg + i * step;
                    BlockMesh.SweepArc(mesh.Pick(), center, normal, zero, radius, collar, a - collarDeg, a + collarDeg,
                        1);
                }
            }

            if (brokenStart)
                Rubble(mesh, center, normal, zero, radius, thickness, fromDeg, -sign, random, groundY);
            if (brokenEnd)
                Rubble(mesh, center, normal, zero, radius, thickness, toDeg, sign, random, groundY);
        }

        // Bloco da ponta quebrada: a metade de dentro e a de fora param em ângulos diferentes (dente)
        static void Jagged(MeshData mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius, float thickness,
            float width, float chamfer, float a0, float a1, bool atStart, int segments)
        {
            float cut = (a1 - a0) * 0.4f;
            Vector2[] inner = BlockMesh.Chamfered(thickness * 0.5f, width, chamfer,
                new Vector2(-thickness * 0.25f, 0f));
            Vector2[] outer = BlockMesh.Chamfered(thickness * 0.5f, width * 0.92f, chamfer,
                new Vector2(thickness * 0.25f, 0f));
            if (atStart)
            {
                BlockMesh.SweepArc(mesh, center, normal, zero, radius, inner, a0, a1, segments);
                BlockMesh.SweepArc(mesh, center, normal, zero, radius, outer, a0 + cut, a1, segments);
            }
            else
            {
                BlockMesh.SweepArc(mesh, center, normal, zero, radius, inner, a0, a1 - cut, segments);
                BlockMesh.SweepArc(mesh, center, normal, zero, radius, outer, a0, a1, segments);
            }
        }

        // Pedaços que caíram da quebra: um bloco grande meio enterrado (ou afundado) e dois menores perto dele
        static void Rubble(RuinMesh mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius, float thickness,
            float deg, float outwardSign, System.Random random, float groundY)
        {
            Vector3 u = Vector3.ProjectOnPlane(zero, normal).normalized, w = Vector3.Cross(normal, u);
            float t = deg * Mathf.Deg2Rad;
            Vector3 dir = Mathf.Cos(t) * u + Mathf.Sin(t) * w;
            Vector3 tangent = (-Mathf.Sin(t) * u + Mathf.Cos(t) * w) * outwardSign;
            Vector3 tip = center + dir * radius;
            if (tip.y < groundY + thickness * 0.5f)
                return;

            Vector3 flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
            if (flat.sqrMagnitude < 0.01f)
                flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            flat = flat.sqrMagnitude < 0.01f ? Vector3.forward : flat.normalized;
            Vector3 foot = new Vector3(tip.x, groundY, tip.z) + flat * thickness * 1.2f;
            Quaternion fallen = Quaternion.LookRotation(flat) *
                                Quaternion.Euler(R(random, 15f, 35f), R(random, -30f, 30f), R(random, -20f, 20f));
            BlockMesh.ChamferBox(mesh.Pick(), foot + Vector3.up * thickness * 0.15f,
                new Vector3(thickness, thickness * 0.8f, thickness * 1.3f), fallen, thickness * 0.12f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, R(random, 0f, 360f), 0f) * Vector3.forward *
                                 (thickness * R(random, 0.9f, 1.6f));
                float size = thickness * R(random, 0.25f, 0.4f);
                BlockMesh.ChamferBox(mesh.Pick(), foot + offset + Vector3.up * size * 0.3f, Vector3.one * size,
                    Quaternion.Euler(R(random, 0f, 40f), R(random, 0f, 360f), R(random, 0f, 40f)), size * 0.15f);
            }
        }

        // Anel deitado (o topo é uma passarela larga e lisa): blocos chanfrados com canaleta nas faces de dentro e de
        // fora, aros mais largos dos lados a cada 'collarEvery' blocos (rentes abaixo do topo, sem virar degrau) e
        // pontas quebradas em dente. 'center' fica na altura do topo; o corpo desce 'depth' ao longo de -normal (com a
        // normal inclinada, o topo vira uma rampa curva). 'width' é a largura da passarela.
        public static void LyingRing(RuinMesh mesh, Vector3 center, Vector3 normal, Vector3 zero, float radius,
            float width, float depth, float fromDeg, float toDeg, System.Random random, bool brokenStart = false,
            bool brokenEnd = false, int collarEvery = 3)
        {
            normal = normal.normalized;
            float span = toDeg - fromDeg, sign = Mathf.Sign(span);
            float arcLength = radius * Mathf.Abs(span) * Mathf.Deg2Rad;
            int blocks = Mathf.Max(2, Mathf.RoundToInt(arcLength / Mathf.Max(width * 1.2f, 2f)));
            float step = span / blocks;
            float chamfer = Mathf.Min(width, depth) * 0.08f;
            Vector2[] body = TopAtZero(BlockMesh.Grooved(depth, width, chamfer, 0.4f, Mathf.Min(0.5f, width * 0.06f)),
                depth);
            Vector2[] cap = BlockMesh.Chamfered(width, depth, chamfer, new Vector2(0f, -depth * 0.5f));
            float collarDepth = depth - 0.25f;
            Vector2[] collar = BlockMesh.Chamfered(width * 1.08f, collarDepth, chamfer * 1.5f,
                new Vector2(0f, -0.25f - collarDepth * 0.5f));
            float collarDeg = width * 0.3f / radius * Mathf.Rad2Deg * sign;
            int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(step) / 4f));
            Vector2[] inner = BlockMesh.Chamfered(width * 0.5f, depth, chamfer,
                new Vector2(-width * 0.25f, -depth * 0.5f));
            Vector2[] outer = BlockMesh.Chamfered(width * 0.5f, depth, chamfer,
                new Vector2(width * 0.25f, -depth * 0.5f));

            for (int i = 0; i < blocks; i++)
            {
                float a0 = fromDeg + i * step, a1 = fromDeg + (i + 1) * step;
                // Blocos encostados e um pouco desalinhados só no raio (o topo continua no mesmo nível)
                float r = radius + R(random, -0.02f, 0.02f) * width;
                bool jagStart = i == 0 && brokenStart, jagEnd = i == blocks - 1 && brokenEnd;
                if (jagStart || jagEnd)
                {
                    float cut = (a1 - a0) * 0.4f;
                    BlockMesh.SweepArc(mesh.Pick(), center, normal, zero, r, inner, jagStart ? a0 : a0,
                        jagEnd ? a1 - cut : a1, segments);
                    BlockMesh.SweepArc(mesh.Pick(), center, normal, zero, r, outer, jagStart ? a0 + cut : a0, a1,
                        segments);
                }
                else
                    BlockMesh.SweepArc(mesh.Pick(), center, normal, zero, r, body, a0, a1, segments, cap);

                if (collarEvery > 0 && i > 0 && i % collarEvery == 0)
                {
                    float a = fromDeg + i * step;
                    BlockMesh.SweepArc(mesh.Pick(), center, normal, zero, radius, collar, a - collarDeg,
                        a + collarDeg, 1);
                }
            }
        }

        // Troca os eixos de um perfil (x <-> y), mantém o sentido anti-horário e desce para o topo ficar em y = 0
        static Vector2[] TopAtZero(Vector2[] profile, float depth)
        {
            var result = new Vector2[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 p = profile[profile.Length - 1 - i];
                result[i] = new Vector2(p.y, p.x - depth * 0.5f);
            }

            return result;
        }

        // ---------- muros, colunas ----------

        // Muro de blocos aparelhados (fiadas desencontradas, blocos um pouco fora do prumo) sobre uma base mais larga,
        // com o topo quebrado de forma irregular. 'position' é o meio da base no chão.
        public static void AshlarWall(RuinMesh mesh, Vector3 position, Quaternion yaw, float length, float height,
            float thick, System.Random random)
        {
            Vector3 right = yaw * Vector3.right, forward = yaw * Vector3.forward;
            BlockMesh.ChamferBox(mesh.Pick(), position + Vector3.up * 0.2f,
                new Vector3(length + 0.6f, 0.4f, thick + 0.5f), yaw, 0.1f);

            float course = 0.7f;
            int courses = Mathf.Max(1, Mathf.RoundToInt(height / course));
            float seedA = R(random, 0f, 10f), seedB = R(random, 0f, 10f);
            // Topo quebrado: a altura útil varia ao longo do muro
            float Limit(float x)
            {
                float t = x / Mathf.Max(1f, length);
                return height * (0.55f + 0.3f * Mathf.Sin(t * 5f + seedA) + 0.15f * Mathf.Sin(t * 13f + seedB));
            }

            for (int c = 0; c < courses; c++)
            {
                float y = 0.4f + c * course;
                float x = -length * 0.5f + (c % 2 == 0 ? 0f : -0.45f);
                while (x < length * 0.5f)
                {
                    float blockLength = Mathf.Min(R(random, 0.9f, 1.7f), length * 0.5f - x);
                    if (x < -length * 0.5f)
                    {
                        blockLength += x + length * 0.5f;
                        x = -length * 0.5f;
                    }

                    float middle = x + blockLength * 0.5f;
                    if (blockLength > 0.3f && (c == 0 || y + course <= Limit(middle) + course * 0.5f))
                    {
                        Vector3 center = position + right * middle + Vector3.up * (y + course * 0.5f) +
                                         forward * R(random, -0.04f, 0.04f);
                        Quaternion tilt = yaw * Quaternion.Euler(0f, R(random, -1.5f, 1.5f), R(random, -1f, 1f));
                        BlockMesh.ChamferBox(mesh.Pick(), center,
                            new Vector3(blockLength - 0.06f, course - 0.06f, thick), tilt, 0.08f);
                    }

                    x += blockLength;
                }
            }

            // Trepadeira caindo do topo de cada lado (abaixo da quebra naquele ponto)
            float vineX = R(random, -0.3f, 0.3f) * length;
            Vector3 top = Vector3.up * (0.4f + Mathf.Max(course, Limit(vineX) - course * 0.5f));
            HangVine(position + top + right * vineX + forward * (thick * 0.5f + 0.04f), forward, random);
            HangVine(position + top * 0.8f - right * vineX - forward * (thick * 0.5f + 0.04f), -forward, random);
        }

        // Coluna: base em dois degraus, fuste de tambores (8 lados, levemente girados entre si) e capitel; partida, o
        // último tambor fica torto e um tambor caído fica ao lado
        public static void Column(RuinMesh mesh, Vector3 position, float height, float radius, System.Random random,
            bool broken)
        {
            // Medidas proporcionais ao raio (1 = coluna de 1,1 m de raio das ilhas)
            float k = radius / 1.1f;
            Pillar(mesh.Pick(), position + Vector3.up * (0.25f * k), Vector2.one * (radius * 2.8f), 0.5f * k,
                Quaternion.identity, 0.25f * k);
            Pillar(mesh.Pick(), position + Vector3.up * (0.7f * k), Vector2.one * (radius * 2.3f), 0.4f * k,
                Quaternion.identity, 0.2f * k);
            float y = 0.9f * k;
            float shaft = height - (broken ? 0.9f : 1.6f) * k;
            while (y < shaft)
            {
                float drum = Mathf.Min(R(random, 1.1f, 1.6f) * k, shaft - y);
                if (drum < 0.3f * k)
                    break;
                bool last = y + drum >= shaft - 0.01f;
                Vector3 axis = broken && last
                    ? Quaternion.Euler(R(random, 4f, 9f), R(random, 0f, 360f), 0f) * Vector3.up
                    : Vector3.up;
                BlockMesh.Prism(mesh.Pick(), position + Vector3.up * y, axis, drum - 0.05f * k, radius, 8,
                    22.5f + R(random, -6f, 6f));
                y += drum;
            }

            if (!broken)
            {
                Pillar(mesh.Pick(), position + Vector3.up * (y + 0.2f * k), Vector2.one * (radius * 2.1f), 0.4f * k,
                    Quaternion.identity, 0.2f * k);
                Pillar(mesh.Pick(), position + Vector3.up * (y + 0.6f * k), Vector2.one * (radius * 2.6f), 0.4f * k,
                    Quaternion.identity, 0.25f * k);
                return;
            }

            Vector3 side = Quaternion.Euler(0f, R(random, 0f, 360f), 0f) * Vector3.forward;
            Vector3 lying = Quaternion.Euler(0f, R(random, 0f, 360f), 0f) * Vector3.right;
            BlockMesh.Prism(mesh.Pick(), position + side * radius * 3.2f + Vector3.up * radius * 0.92f - lying * (0.7f * k),
                lying, 1.4f * k, radius, 8, 22.5f);
        }

        // Tambores de coluna caídos, um encostado no outro
        public static void FallenDrums(RuinMesh mesh, Vector3 position, Quaternion yaw, float radius,
            System.Random random)
        {
            Vector3 axis = yaw * Vector3.right;
            BlockMesh.Prism(mesh.Pick(), position - axis * 1.6f + Vector3.up * radius * 0.92f, axis, 1.5f, radius, 8,
                22.5f);
            Vector3 second = Quaternion.Euler(0f, R(random, 15f, 40f), 0f) * axis;
            BlockMesh.Prism(mesh.Pick(), position + axis * 0.2f + Vector3.up * radius * 0.92f, second, 1.4f, radius, 8,
                30f);
            BlockMesh.ChamferBox(mesh.Pick(), position + yaw * new Vector3(2.4f, 0.35f, 1.2f),
                new Vector3(radius * 2.4f, 0.7f, radius * 2.4f), yaw * Quaternion.Euler(8f, 20f, 0f), 0.1f);
        }

        // ---------- peças colossais ----------

        // Torre: andares que afinam, cada um com cornija, contrafortes nas quinas e vãos de janela (moldura saliente
        // com o fundo escuro), e o topo partido em dentes de alturas diferentes
        public static void Tower(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(R(random, -4f, 4f), yaw, R(random, -4f, 4f));
            Vector3 up = rotation * Vector3.up;
            float width = 14f * scale;
            float y = -6f * scale;
            int levels = 4 + random.Next(4);
            for (int i = 0; i < levels; i++)
            {
                float height = R(random, 8f, 14f) * scale;
                Quaternion twist = rotation * Quaternion.Euler(0f, R(random, -3f, 3f), 0f);
                Vector3 bottom = position + up * y;
                Pillar(mesh.Pick(), bottom + up * (height * 0.5f), new Vector2(width, width), height, twist,
                    width * 0.06f);

                // Contrafortes nas quinas, afinando para cima
                for (int corner = 0; corner < 4; corner++)
                {
                    Quaternion around = twist * Quaternion.Euler(0f, 45f + corner * 90f, 0f);
                    Vector3 out1 = around * Vector3.forward * (width * 0.68f);
                    BlockMesh.Box(mesh.Pick(), bottom + out1 + up * (height * 0.4f),
                        new Vector3(width * 0.16f, height * 0.8f, width * 0.16f), around);
                }

                // Vãos de janela nas quatro faces (nos andares acima da água)
                if (y + height * 0.5f > 2f)
                {
                    for (int face = 0; face < 4; face++)
                    {
                        Quaternion faceRotation = twist * Quaternion.Euler(0f, face * 90f, 0f);
                        int windows = width > 9f * scale ? 2 : 1;
                        for (int k = 0; k < windows; k++)
                        {
                            float offset = windows == 1 ? 0f : (k - 0.5f) * width * 0.42f;
                            Vector3 spot = bottom + up * (height * 0.55f) +
                                           faceRotation * new Vector3(offset, 0f, width * 0.5f);
                            Window(mesh, spot, faceRotation, width * 0.16f, height * 0.4f);
                        }
                    }
                }

                Pillar(mesh.Pick(), bottom + up * (height + 0.6f * scale), new Vector2(width * 1.14f, width * 1.14f),
                    1.2f * scale, twist, 0.3f * scale);
                if (i == levels - 2)
                    HangVine(bottom + up * (height + 0.4f * scale) + twist * Vector3.forward * (width * 0.58f),
                        twist * Vector3.forward, random, scale * 2.5f);
                y += height + 1.2f * scale;
                width *= 0.86f;
            }

            // Topo em ruína: dentes de alturas diferentes em volta da borda
            Vector3 top = position + up * y;
            for (int k = 0; k < 6; k++)
            {
                if (random.NextDouble() < 0.3)
                    continue;
                Quaternion around = rotation * Quaternion.Euler(0f, k * 60f + R(random, -10f, 10f), 0f);
                float tooth = R(random, 2f, 9f) * scale;
                Pillar(mesh.Pick(), top + around * Vector3.forward * (width * 0.38f) + up * (tooth * 0.5f),
                    new Vector2(width * 0.3f, width * 0.22f), tooth, around, 0.2f * scale);
            }

            // Bloco caído na água
            Vector3 side = rotation * Vector3.right;
            BlockMesh.ChamferBox(mesh.Pick(), position + side * width * 1.5f + Vector3.up * scale,
                new Vector3(width * 0.8f, width * 0.5f, width * 0.6f), Quaternion.Euler(20f, yaw + 35f, 15f), scale);
        }

        // Vão de janela: moldura saliente (vergas e ombreiras) e o fundo escuro rente à parede
        static void Window(RuinMesh mesh, Vector3 center, Quaternion faceRotation, float width, float height)
        {
            Vector3 right = faceRotation * Vector3.right, up = faceRotation * Vector3.up,
                out1 = faceRotation * Vector3.forward;
            float frame = width * 0.22f, depth = width * 0.25f;
            BlockMesh.Box(mesh.Dark, center + out1 * 0.02f, new Vector3(width, height, 0.1f), faceRotation);
            BlockMesh.Box(mesh.Pick(), center + up * (height * 0.5f + frame * 0.5f) + out1 * depth * 0.5f,
                new Vector3(width + frame * 2.4f, frame, depth), faceRotation);
            BlockMesh.Box(mesh.Pick(), center - up * (height * 0.5f + frame * 0.5f) + out1 * depth * 0.5f,
                new Vector3(width + frame * 2.8f, frame, depth * 1.2f), faceRotation);
            foreach (float side in new[] { -1f, 1f })
                BlockMesh.Box(mesh.Pick(), center + right * side * (width * 0.5f + frame * 0.5f) + out1 * depth * 0.5f,
                    new Vector3(frame, height, depth), faceRotation);
        }

        // Arco monumental: dois pilares com base e capitel, o arco de aduelas por cima (às vezes partido de um lado)
        public static void Arch(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, R(random, -3f, 3f));
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            float span = 30f * scale, pillar = 7f * scale, height = 30f * scale;
            bool broken = random.NextDouble() < 0.5;
            for (int side = -1; side <= 1; side += 2)
            {
                float h = broken && side == -1 ? height * 0.55f : height;
                Vector3 foot = position + right * side * (span * 0.5f + pillar * 0.5f) - up * 4f * scale;
                Pillar(mesh.Pick(), foot + up * (h * 0.5f), new Vector2(pillar, pillar * 1.2f), h, rotation,
                    pillar * 0.08f);
                Pillar(mesh.Pick(), foot + up * (5f * scale), new Vector2(pillar * 1.3f, pillar * 1.5f), 2f * scale,
                    rotation, pillar * 0.1f);
                if (!(broken && side == -1))
                    Pillar(mesh.Pick(), foot + up * (h - 1f * scale), new Vector2(pillar * 1.3f, pillar * 1.5f),
                        2f * scale, rotation, pillar * 0.1f);
            }

            float radius = span * 0.5f + pillar * 0.5f;
            // 0° = direita, 180° = esquerda; partido, o arco vem da direita e para no alto
            RichRing(mesh, position + up * (height - 4f * scale), forward, right, radius, pillar, pillar * 1.2f, 0f,
                broken ? 95f : 180f, random, false, broken, -2f, 4);
        }

        // Bloco em pé (prisma vertical) com as quinas verticais chanfradas: 'footprint' = largura x profundidade
        public static void Pillar(MeshData mesh, Vector3 center, Vector2 footprint, float height, Quaternion rotation,
            float chamfer) =>
            BlockMesh.ChamferBox(mesh, center, new Vector3(footprint.x, footprint.y, height),
                rotation * Quaternion.Euler(-90f, 0f, 0f), chamfer);

        // Pedra irregular: dois ou três blocos chanfrados tortos, um entrando no outro
        public static void IrregularRock(RuinMesh mesh, Vector3 center, float size, System.Random random)
        {
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            int parts = 2 + random.Next(2);
            for (int k = 0; k < parts; k++)
            {
                float scale = k == 0 ? 1f : R(0.45f, 0.8f);
                var extent = new Vector3(R(0.7f, 1.2f), R(0.5f, 0.95f), R(0.7f, 1.2f)) * (size * scale);
                Vector3 offset = k == 0 ? Vector3.zero
                    : new Vector3(R(-0.4f, 0.4f), R(0f, 0.35f), R(-0.4f, 0.4f)) * size;
                BlockMesh.ChamferBox(mesh.Pick(), center + offset, extent,
                    Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)),
                    Mathf.Min(extent.x, Mathf.Min(extent.y, extent.z)) * 0.22f);
            }
        }

        // ---------- trepadeiras ----------

        // Trepadeira pendurada: carta vertical na frente da face 'facing', do ponto 'top' (no topo da face) para baixo
        public static void HangVine(Vector3 top, Vector3 facing, System.Random random, float scale = 1f)
        {
            if (Vines == null || !VineMaterial || random.NextDouble() < 0.35)
                return;
            Vector2 size = VineSize * scale * R(random, 0.8f, 1.25f);
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.5f)
                return;
            Vines.Add(VineMaterial, top - Vector3.up * size.y, flat, size, random.NextDouble() < 0.5);
        }

        // Trepadeiras penduradas da beira de cima de um bloco de rocha (Pillar: 'center', 'footprint', 'height',
        // 'rotation'), nas faces viradas para 'view' (de onde o jogador olha), espalhadas pela largura da face e com
        // comprimentos variados (até ~90% da altura do bloco). 'density' multiplica a quantidade.
        public static void VinesOnBlock(Vector3 center, Vector2 footprint, float height, Quaternion rotation,
            Vector3 view, System.Random random, float density = 1f)
        {
            if (Vines == null)
                return;
            Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
            (Vector3 normal, float half, float span)[] faces =
            {
                (right, footprint.x * 0.5f, footprint.y), (-right, footprint.x * 0.5f, footprint.y),
                (forward, footprint.y * 0.5f, footprint.x), (-forward, footprint.y * 0.5f, footprint.x),
            };
            view = Vector3.ProjectOnPlane(view, Vector3.up).normalized;
            foreach ((Vector3 normal, float half, float span) in faces)
            {
                Vector3 n = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
                if (Vector3.Dot(n, view) < 0.15f)
                    continue;
                Vector3 along = Vector3.Cross(Vector3.up, n);
                int count = Mathf.Max(1, Mathf.RoundToInt(span / 2.2f * density));
                for (int i = 0; i < count; i++)
                {
                    float s = ((i + R(random, 0.2f, 0.8f)) / count - 0.5f) * span * 0.85f;
                    float scale = Mathf.Clamp(height / VineSize.y * R(random, 0.35f, 0.9f), 0.6f, 4f);
                    HangVine(center + normal * (half + 0.05f) + along * s + Vector3.up * (height * 0.5f - 0.15f), n,
                        random, scale);
                }
            }
        }
    }
}
