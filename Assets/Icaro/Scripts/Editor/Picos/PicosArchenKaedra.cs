using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Archen Kaedra, a Cidade Perdida: etapa 1, o traçado em blocos (sem detalhe, para aprovar escala e caminho). Sorteio
    // próprio, para não mudar o resto do mapa. Coordenadas (x, z) a partir do centro da cidade: z para a ponte, x de lado.
    // Caminho: a Rua do Cerco sai da barbacã e dá num bloqueio; a saída é a oeste, pelo Bairro dos Relojoeiros (arena em
    // três níveis); do terraço alto do bairro, o viaduto passa por cima da rua até o Mercado Alto (arena a +8 m); a
    // rampa do mercado sobe até o meio da entrada do pódio da basílica, o único acesso; o trecho da rua depois do bloqueio virou torres e prédios altos.
    public static partial class PicosBuilder
    {
        static void BuildArchenKaedraBlocks(RuinMesh m, Vector3 city, Vector3 toBridge)
        {
            var rnd = new System.Random(4801);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            Quaternion q = Quaternion.LookRotation(toBridge);
            Vector3 P(float x, float z, float y = 0f) => city + across * x + toBridge * z + Vector3.up * y;

            // O fim do piso da barbacã
            float zStart = s_ReinoGateT - k_BarbicanDepth - 10f - k_ReinoCityT;
            Vector3 barbican = ReinoAt(s_ReinoGateT - 28f);

            void Block(float x0, float x1, float z0, float z1, float y0, float y1) =>
                BlockMesh.ChamferBox(m.Pick(), P((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, (y0 + y1) * 0.5f),
                    new Vector3(x1 - x0, y1 - y0, z1 - z0), q, 0.1f);

            // Rampa maciça de (xa, za, ya) a (xb, zb, yb), com largura 'width'
            void Ramp(float xa, float za, float ya, float xb, float zb, float yb, float width)
            {
                Vector3 flat = (P(xb, zb) - P(xa, za)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, flat);
                ConvexPrism(m.Pick(), new[] { P(xa, za, -0.5f), P(xb, zb, -0.5f), P(xb, zb, yb), P(xa, za, ya) }, side, width);
            }

            // Prédio em bloco com empena: (cx, cz) centro, 'wide' ao longo da frente, 'deep' para trás
            void Building(float cx, float cz, float sizeX, float sizeZ, float top)
            {
                Block(cx - sizeX * 0.5f, cx + sizeX * 0.5f, cz - sizeZ * 0.5f, cz + sizeZ * 0.5f, -1f, top);
                float rise = Mathf.Min(sizeX, sizeZ) * 0.55f;
                if (sizeX <= sizeZ)
                    ConvexPrism(m.Pick(), new[] { P(cx - sizeX * 0.5f, cz, top - 0.1f), P(cx + sizeX * 0.5f, cz, top - 0.1f), P(cx, cz, top + rise) }, toBridge, sizeZ + 0.8f);
                else
                    ConvexPrism(m.Pick(), new[] { P(cx, cz - sizeZ * 0.5f, top - 0.1f), P(cx, cz + sizeZ * 0.5f, top - 0.1f), P(cx, cz, top + rise) }, across, sizeX + 0.8f);
            }

            // Prédios do kit AR_Cidade: largura ao longo da frente e fundo
            (string Model, float Wide, float Deep)[] houses =
            {
                ("AR_Cidade_Sobrado", 11f, 12f), ("AR_Cidade_Arcada", 13f, 12f), ("AR_Cidade_Mansao", 15f, 14f), ("AR_Cidade_Torreada", 11f, 12f),
                ("AR_Cidade_Relojoeiro", 15f, 13f), ("AR_Cidade_Palacete", 17f, 14f),
            };
            var buildings = new GameObject("ArchenKaedra_Predios").transform;
            buildings.SetParent(s_World, false);
            var taken = new List<(float X0, float X1, float Z0, float Z1)>();
            var mouths = new List<(bool AlongX, float FixedAt, float Outward, float A, float B)>();

            // Máscara da cidade em células de 1 m: 0 = vira quadra construída, 1 = aberto (rua, beco, pátio), 2 = já ocupado
            const int Half = 340;
            var cells = new byte[Half * 2 + 1, Half * 2 + 1];
            var laneLevel = new float[Half * 2 + 1, Half * 2 + 1];
            for (int i = 0; i <= Half * 2; i++)
                for (int j = 0; j <= Half * 2; j++)
                    laneLevel[i, j] = -1f;

            void Mark(float x0, float x1, float z0, float z1, byte value, float level = -1f)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt(x0) + Half), i1 = Mathf.Min(Half * 2, Mathf.CeilToInt(x1) + Half - 1);
                int j0 = Mathf.Max(0, Mathf.FloorToInt(z0) + Half), j1 = Mathf.Min(Half * 2, Mathf.CeilToInt(z1) + Half - 1);
                for (int i = i0; i <= i1; i++)
                    for (int j = j0; j <= j1; j++)
                    {
                        if (value == 2 && cells[i, j] == 1)
                            continue;
                        cells[i, j] = value;
                        if (level >= 0f)
                            laneLevel[i, j] = Mathf.Max(laneLevel[i, j], level);
                    }
            }


            // Fileira de fachadas numa linha reta, com os prédios para o lado 'outward', de frente para a linha, e vãos nas aberturas
            void Row(bool alongX, float fixedAt, float from, float to, float outward, float level, params (float A, float B)[] gaps)
            {
                var all = new List<(float A, float B)>(gaps);
                foreach (var mouth in mouths)
                    if (mouth.AlongX == alongX && Mathf.Abs(mouth.FixedAt - fixedAt) < 0.6f && mouth.Outward == outward && mouth.B > from && mouth.A < to)
                        all.Add((mouth.A, mouth.B));
                all.Sort((u, v) => u.A.CompareTo(v.A));

                float at = from;
                foreach ((float a, float b) in all)
                {
                    if (a > at)
                        Run(alongX, fixedAt, at, Mathf.Min(a, to), outward, level);
                    at = Mathf.Max(at, b);
                }

                if (at < to)
                    Run(alongX, fixedAt, at, to, outward, level);
            }

            // Bloco liso na linha da fachada, 'depth' para trás: (a, b) ao longo da linha, (d0, d1) de profundidade
            void LineBlock(bool alongX, float fixedAt, float a, float b, float d0, float d1, float outward, float y0, float y1)
            {
                float n0 = Mathf.Min(fixedAt + outward * d0, fixedAt + outward * d1), n1 = Mathf.Max(fixedAt + outward * d0, fixedAt + outward * d1);
                if (alongX)
                    Block(a, b, n0, n1, y0, y1);
                else
                    Block(n0, n1, a, b, y0, y1);
            }

            // Um trecho sem aberturas: sorteia a combinação de prédios que melhor enche o trecho e divide a sobra entre eles
            void Run(bool alongX, float fixedAt, float start, float end, float outward, float level)
            {
                const float Gap = 0.6f;
                float length = end - start;
                if (length < 2f)
                    return;
                float line0 = fixedAt + outward * 0f, line1 = fixedAt + outward * 14.5f;
                if (alongX)
                    Mark(start, end, Mathf.Min(line0, line1), Mathf.Max(line0, line1), 2);
                else
                    Mark(Mathf.Min(line0, line1), Mathf.Max(line0, line1), start, end, 2);
                List<int> best = null;
                float bestLeft = float.MaxValue;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var pick = new List<int>();
                    float left = length;
                    while (true)
                    {
                        var fits = new List<int>();
                        for (int i = 0; i < houses.Length; i++)
                            if (houses[i].Wide + Gap <= left)
                                fits.Add(i);
                        if (fits.Count == 0)
                            break;
                        int chosen = fits[Mathf.Min((int)Range(0f, fits.Count), fits.Count - 1)];
                        pick.Add(chosen);
                        left -= houses[chosen].Wide + Gap;
                    }

                    if (left < bestLeft)
                    {
                        bestLeft = left;
                        best = pick;
                    }
                }

                if (best.Count == 0)
                {
                    LineBlock(alongX, fixedAt, start, end, 0f, 8f, outward, -1f, level + 11f);
                    return;
                }

                // Parede lisa recuada atrás da fileira, que fecha as frestas entre um prédio e outro
                LineBlock(alongX, fixedAt, start, end, 1.2f, 6f, outward, -1f, level + 8f);
                float extra = bestLeft / best.Count, cursor = start;
                bool street = !alongX && Mathf.Abs(fixedAt) < 12f;
                foreach (int index in best)
                {
                    (string model, float wide, float deep) = houses[index];
                    float slot = wide + Gap + extra, along = cursor + slot * 0.5f;
                    cursor += slot;
                    float depthAt = fixedAt + outward * deep * 0.5f;
                    float cx = alongX ? along : depthAt, cz = alongX ? depthAt : along;
                    float halfX = (alongX ? wide : deep) * 0.5f, halfZ = (alongX ? deep : wide) * 0.5f;
                    bool blocked = false;
                    foreach (var r in taken)
                        if (cx + halfX > r.X0 && cx - halfX < r.X1 && cz + halfZ > r.Z0 && cz - halfZ < r.Z1)
                            blocked = true;
                    if (blocked)
                    {
                        // Esquina: outro prédio já ocupa o fundo; só um bloco raso fecha a frente
                        LineBlock(alongX, fixedAt, along - slot * 0.5f, along + slot * 0.5f, 0f, 6f, outward, -1f, level + 11f);
                        continue;
                    }

                    taken.Add((cx - halfX, cx + halfX, cz - halfZ, cz + halfZ));
                    if (level > 0.5f)
                        LineBlock(alongX, fixedAt, along - wide * 0.5f, along + wide * 0.5f, 0f, deep, outward, -1f, level + 0.05f);
                    bool canBreak = index <= 2;
                    if (canBreak && Range(0f, 1f) < (street ? 0.45f : 0.25f))
                        model += "_Quebrada";
                    Vector3 front = (alongX ? toBridge : across) * -outward;
                    PlaceTech(model, P(cx, cz, level), YawOf(front), buildings, 1f, true);
                }
            }

            var barriers = new GameObject("ArchenKaedra_Barreiras").transform;
            barriers.SetParent(s_World, false);
            void Barrier(float x0, float x1, float z0, float z1, float y0, float y1)
            {
                var wall = new GameObject("Barreira");
                wall.transform.SetParent(barriers, false);
                wall.transform.SetPositionAndRotation(P((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, (y0 + y1) * 0.5f), q);
                wall.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            }

            // ---------- espaços principais: nada de quadra construída em cima deles ----------
            Mark(-11.5f, 11.5f, 143f, zStart + 6f, 2);
            Mark(-13f, 13f, 158f, 174f, 2);
            Mark(-70f, 11.5f, 183f, 197f, 2);
            Mark(-182f, -70f, 118f, 222f, 2);
            Mark(-71f, 71f, 143f, 157f, 2);
            Mark(70f, 182f, 100f, 200f, 2);
            Mark(-10f, 72f, 104f, 125f, 2);
            // O pódio da basílica: nenhum lote nem torre em cima dele
            for (int x = -125; x <= 125; x++)
                for (int z = -340; z <= 106; z++)
                {
                    float ax = Mathf.Abs(x);
                    bool front = x * x + z * z <= 116f * 116f && z <= 106f && (ax < 60f || z <= 101f - (ax - 60f) * 30f / 28f);
                    if (front || (z < -90 && z > -336 && ax <= 68f))
                        Mark(x, x + 1f, z, z + 1f, 2);
                }


            // ---------- becos e vielas (2026-11-09, pedido do usuário: as ruas principais não podem ser as únicas) ----------
            // Só o traçado em blocos. Coordenadas inteiras, trechos retos em X ou Z: a primeira ponta fica na linha das fachadas.

            // Beco: 'w' de largura; (x, z, altura do piso) em cada ponta. Altura diferente vira rampa (a escadaria, no detalhe).
            void Lane(float w, params (float X, float Z, float Y)[] pts)
            {
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    bool isX = Mathf.Abs(b.X - a.X) > Mathf.Abs(b.Z - a.Z);
                    float dir = Mathf.Sign(isX ? b.X - a.X : b.Z - a.Z);
                    float from = isX ? a.X : a.Z, to = isX ? b.X : b.Z;
                    bool flat = Mathf.Abs(a.Y - b.Y) < 0.01f;
                    bool turnBefore = i > 0 && (Mathf.Abs(pts[i].X - pts[i - 1].X) > Mathf.Abs(pts[i].Z - pts[i - 1].Z)) != isX;
                    bool turnAfter = i + 2 < pts.Length && (Mathf.Abs(pts[i + 2].X - b.X) > Mathf.Abs(pts[i + 2].Z - b.Z)) != isX;
                    if (turnBefore)
                        from += dir * w * 0.5f;     // a esquina fica com o trecho anterior (sem dois pisos no mesmo plano)
                    if (turnAfter)
                        to += dir * w * 0.5f;
                    float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
                    float level = Mathf.Max(a.Y, b.Y);
                    if (isX)
                    {
                        Mark(lo, hi, a.Z - w * 0.5f, a.Z + w * 0.5f, 1, level);
                        if (flat)
                            Block(lo, hi, a.Z - w * 0.5f, a.Z + w * 0.5f, -1f, a.Y);
                    }
                    else
                    {
                        Mark(a.X - w * 0.5f, a.X + w * 0.5f, lo, hi, 1, level);
                        if (flat)
                            Block(a.X - w * 0.5f, a.X + w * 0.5f, lo, hi, -1f, a.Y);
                    }

                    if (!flat)
                        Ramp(isX ? from : a.X, isX ? a.Z : from, a.Y, isX ? to : a.X, isX ? a.Z : to, b.Y, w);
                    if (i == 0)
                        mouths.Add((!isX, isX ? a.X : a.Z, dir, (isX ? a.Z : a.X) - w * 0.5f, (isX ? a.Z : a.X) + w * 0.5f));
                }
            }

            void Yard(float x0, float x1, float z0, float z1, float y)
            {
                Mark(x0, x1, z0, z1, 1, y);
                Block(x0, x1, z0, z1, -1f, y);
            }

            // Destroços que fecham o caminho: miolo maciço, blocos tombados por cima e barreira invisível alta
            void Pile(float x, float z, float w, float y, bool acrossX)
            {
                float halfA = w * 0.5f + 0.5f;
                float x0 = acrossX ? x - halfA : x - 2.5f, x1 = acrossX ? x + halfA : x + 2.5f;
                float z0 = acrossX ? z - 2.5f : z - halfA, z1 = acrossX ? z + 2.5f : z + halfA;
                Block(x0, x1, z0, z1, y - 0.5f, y + 4.2f);
                for (int k = 0; k < 9; k++)
                {
                    float size = Range(0.9f, 2.4f);
                    BlockMesh.ChamferBox(m.Pick(), P(Range(x0, x1), Range(z0, z1), y + Range(0.6f, 4.3f)), new Vector3(size * 1.6f, size, size),
                        q * Quaternion.Euler(Range(-35f, 35f), Range(0f, 360f), Range(-35f, 35f)), 0.1f);
                }

                float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                if (acrossX)
                    Barrier(x0 - 1f, x1 + 1f, cz - 0.3f, cz + 0.3f, y, y + 48f);
                else
                    Barrier(cx - 0.3f, cx + 0.3f, z0 - 1f, z1 + 1f, y, y + 48f);
            }

            void Prop(float cx, float cz, float w, float d, float y, float h) => Block(cx - w * 0.5f, cx + w * 0.5f, cz - d * 0.5f, cz + d * 0.5f, y - 0.3f, y + h);

            // Arco ou escora atravessada de parede a parede, entrando nos prédios dos dois lados
            void Span(float x, float z, float w, bool acrossX, float y0, float y1, float thick = 0.7f)
            {
                if (acrossX)
                    Block(x - w * 0.5f - 0.6f, x + w * 0.5f + 0.6f, z - thick * 0.5f, z + thick * 0.5f, y0, y1);
                else
                    Block(x - thick * 0.5f, x + thick * 0.5f, z - w * 0.5f - 0.6f, z + w * 0.5f + 0.6f, y0, y1);
            }

            int zN = Mathf.RoundToInt(zStart * 0.5f) * 2;
            int zFar = Mathf.Max(zN - 24, 240);
            const float Street = 0.18f, Court = 0.12f;

            // Rua do Cerco, leste: fechado logo na entrada, com a viela continuando atrás dos destroços
            Lane(4f, (11.5f, 190f, Street), (46f, 190f, Street));
            Pile(29f, 190f, 4f, Street, false);
            Prop(20f, 188.6f, 1.2f, 1.2f, Street, 1.1f);

            // Rua do Cerco, oeste: viela larga que vira depois de uma quadra; o braço norte fecha só depois da curva
            Lane(6f, (-11.5f, 216f, Street), (-52f, 216f, Street));
            Lane(4f, (-30f, 219f, Street), (-30f, 230f, Street));
            Pile(-30f, 226f, 4f, Street, true);
            Span(-22f, 216f, 6f, false, 5.2f, 6.1f);
            Span(-40f, 216f, 6f, false, 5.2f, 6.1f);
            Lane(4f, (-50f, 213f, Street), (-50f, 204f, Street));      // e ela desce até a rua de entrada, dando a volta (única que liga dois caminhos)
            Mark(-52f, -48f, 197f, 204f, 1, Street);
            Block(-52f, -48f, 197f, 204f, -1f, Street);
            mouths.Add((true, 197f, 1f, -52f, -48f));

            // Rua de entrada, sul: travessa curta com escoras de madeira entre as paredes; fecha antes do viaduto
            Lane(4f, (-30f, 183f, Street), (-30f, 164f, Street));
            for (float z = 179f; z >= 168f; z -= 3.5f)
                Span(-30f, z, 4f, true, 4.4f + (z % 2f), 4.8f + (z % 2f), 0.3f);
            Pile(-30f, 167f, 4f, Street, true);

            // Rua do Cerco, leste: escadaria que sobe até um pátio alto com a lojinha
            Lane(4f, (11.5f, 214f, Street), (30f, 214f, Street), (46f, 214f, 4.2f));
            Yard(46f, 68f, 205f, 223f, 4.2f);
            Prop(62f, 214f, 2f, 7f, 4.2f, 2.8f);       // balcão da lojinha
            Prop(62f, 214f, 4f, 9f, 7.0f, 0.3f);       // toldo
            Prop(50f, 219f, 2f, 2f, 4.2f, 1.2f);
            Prop(54f, 208f, 3f, 1.4f, 4.2f, 0.9f);

            // Rua do Cerco, leste: viela do canal: duas calçadas altas e o canal congelado no meio; uma ponte e o fim soterrado
            Lane(6f, (11.5f, zFar, Street), (72f, zFar, Street));
            foreach (float side in new[] { -2f, 2f })
            {
                Block(26f, 64f, zFar + side - 1f, zFar + side + 1f, -1f, 0.75f);
                Ramp(22f, zFar + side, Street, 26f, zFar + side, 0.75f, 2f);
            }
            Block(42f, 45f, zFar - 1f, zFar + 1f, 0.75f, 1.0f);       // ponte sobre o canal
            Pile(58f, zFar, 6f, 0.75f, false);

            // Rua do Cerco, oeste: passagem coberta por baixo de um prédio, abrindo num pátio com poço e o nicho de um NPC
            Lane(4f, (-11.5f, zFar, Street), (-40f, zFar, Street));
            Block(-38f, -24f, zFar - 2.6f, zFar + 2.6f, 4.2f, 15f);
            Yard(-64f, -40f, zFar - 10f, zFar + 10f, Street);
            Prop(-52f, zFar, 3f, 3f, Street, 1.1f);        // poço
            Prop(-62.5f, zFar + 6f, 3f, 3f, Street, 0.4f);   // nicho do NPC
            Block(-64.6f, -60.3f, zFar + 4.5f, zFar + 7.5f, 3.8f, 4.4f);     // cobertura do nicho, entrando na parede
            foreach (float dz in new[] { 4.8f, 7.2f })
                Prop(-60.6f, zFar + dz, 0.5f, 0.5f, Street, 3.8f);

            // Relojoeiros, norte: viela em L saindo do pátio
            Lane(4f, (-120f, 222f, Court), (-120f, 252f, Court), (-96f, 252f, Court));
            Pile(-104f, 252f, 4f, Court, false);

            // Relojoeiros, terraço oeste, norte: fechado logo de cara
            Lane(4f, (-160f, 222f, 4f), (-160f, 233f, 4f));
            Pile(-160f, 228f, 4f, 4f, true);

            // Relojoeiros, terraço oeste, oeste: rua alta que dobra e desce em escadaria longa até um largo soterrado
            Lane(5f, (-182f, 170f, 4f), (-220f, 170f, 4f), (-220f, 140f, 4f), (-220f, 110f, Court));
            Yard(-232f, -208f, 94f, 110f, Court);
            Prop(-220f, 102f, 4f, 4f, Court, 3.5f);       // base de monumento sem estátua
            Pile(-220f, 95f, 12f, Court, true);

            // Relojoeiros, sul: viela em zigue-zague com arcos atravessados
            Lane(4f, (-132f, 118f, Court), (-132f, 96f, Court), (-108f, 96f, Court), (-108f, 70f, Court));
            Span(-132f, 108f, 4f, true, 4.2f, 5f);
            Span(-120f, 96f, 4f, false, 4.2f, 5f);
            Span(-108f, 86f, 4f, true, 4.2f, 5f);
            Pile(-108f, 76f, 4f, Court, true);

            // Mercado, norte: escadaria que desce da plataforma até uma ruela lá embaixo
            Lane(5f, (110f, 200f, 8f), (110f, 216f, 8f), (110f, 238f, 1f), (110f, 254f, 1f));
            Pile(110f, 248f, 5f, 1f, true);

            // Mercado, leste: passagem para um pátio de colunas (a segunda lojinha)
            Lane(4f, (182f, 150f, 8f), (212f, 150f, 8f));
            Yard(212f, 236f, 136f, 164f, 8f);
            for (float x = 216f; x <= 232.1f; x += 8f)
                foreach (float z in new[] { 140f, 160f })
                    Prop(x, z, 1f, 1f, 8f, 5.5f);
            Prop(232f, 150f, 3f, 7f, 8f, 2.6f);

            // Mercado, sul: rua alta coberta por arcadas, que vira e acaba soterrada
            Lane(4f, (130f, 100f, 8f), (130f, 84f, 8f), (156f, 84f, 8f));
            Block(127.4f, 132.6f, 88f, 98f, 12.4f, 13f);
            Pile(150f, 84f, 4f, 8f, false);

            // ---------- avenidas soterradas e vielas com portão de ferro (a continuação aparece atrás) ----------

            // Desabamento grande: três camadas de miolo, cada uma mais curta, com blocos assentados na altura da camada
            void BigPile(float x, float z, float w, float y, bool acrossX, float depth = 12f, float height = 8f)
            {
                float half = w * 0.5f + 0.6f;
                float Top(float d)
                {
                    float t = 0f;
                    for (int k = 0; k < 3; k++)
                        if (Mathf.Abs(d) <= depth * 0.5f * (1f - k * 0.3f))
                            t = height * (k + 1) / 3f;
                    return t;
                }

                for (int k = 0; k < 3; k++)
                {
                    float dd = depth * 0.5f * (1f - k * 0.3f), y0 = k == 0 ? y - 0.5f : y + height * k / 3f - 0.2f, y1 = y + height * (k + 1) / 3f;
                    if (acrossX)
                        Block(x - half, x + half, z - dd, z + dd, y0, y1);
                    else
                        Block(x - dd, x + dd, z - half, z + half, y0, y1);
                }

                for (int k = 0; k < 26; k++)
                {
                    float size = Range(1.2f, 3.4f), a = Range(-half, half), d = Range(-depth * 0.5f, depth * 0.5f);
                    float cx = acrossX ? x + a : x + d, cz = acrossX ? z + d : z + a;
                    BlockMesh.ChamferBox(m.Pick(), P(cx, cz, y + Mathf.Max(Top(d), 0.3f)), new Vector3(size * 1.7f, size, size),
                        q * Quaternion.Euler(Range(-30f, 30f), Range(0f, 360f), Range(-30f, 30f)), 0.12f);
                }

                if (acrossX)
                    Barrier(x - half - 1f, x + half + 1f, z - 0.3f, z + 0.3f, y, y + 48f);
                else
                    Barrier(x - 0.3f, x + 0.3f, z - half - 1f, z + half + 1f, y, y + 48f);
            }

            // Portão de ferro gradeado de parede a parede, sob uma verga de pedra: dá para ver através, não para passar
            void Gate(float x, float z, float w, float y, bool acrossX)
            {
                void Bar(float a, float y0, float y1, float along, float thick)
                {
                    Vector3 size = acrossX ? new Vector3(along, y1 - y0, thick) : new Vector3(thick, y1 - y0, along);
                    BlockMesh.ChamferBox(s_Iron, acrossX ? P(x + a, z, (y0 + y1) * 0.5f) : P(x, z + a, (y0 + y1) * 0.5f), size, q, 0.015f);
                }

                float half = w * 0.5f;
                for (float a = -half + 0.14f; a < half - 0.06f; a += 0.26f)
                    Bar(a, y - 0.1f, y + 3.5f, 0.07f, 0.07f);
                Bar(0f, y + 0.5f, y + 0.62f, w + 0.5f, 0.11f);
                Bar(0f, y + 3.0f, y + 3.12f, w + 0.5f, 0.11f);
                Bar(0f, y + 3.45f, y + 3.65f, w + 0.5f, 0.14f);
                Span(x, z, w, acrossX, y + 3.65f, y + 4.5f, 0.8f);

                if (acrossX)
                    Barrier(x - half - 0.4f, x + half + 0.4f, z - 0.15f, z + 0.15f, y, y + 48f);
                else
                    Barrier(x - 0.15f, x + 0.15f, z - half - 0.4f, z + half + 0.4f, y, y + 48f);
            }

            // Vielas nas quinas das arenas: as fachadas não foram feitas para se encontrar em 90°, então a fileira ao longo de X
            // recua e uma viela barrada ocupa a quina (a fileira ao longo de Z vai até o fim e termina na boca da viela)
            Lane(4f, (-180f, 222f, 4f), (-180f, 238f, 4f));
            Gate(-180f, 228f, 4f, 4f, true);
            Lane(4f, (-72f, 222f, Court), (-72f, 240f, Court));
            Pile(-72f, 232f, 4f, Court, true);
            Lane(4f, (-180f, 118f, 4f), (-180f, 100f, 4f));
            Pile(-180f, 108f, 4f, 4f, true);
            Lane(4f, (-72f, 118f, 8f), (-72f, 106f, 8f));
            Gate(-72f, 112f, 4f, 8f, true);
            Lane(4f, (72f, 200f, 8f), (72f, 218f, 8f));
            Gate(72f, 207f, 4f, 8f, true);
            Lane(4f, (180f, 200f, 8f), (180f, 216f, 8f));
            Pile(180f, 210f, 4f, 8f, true);
            Lane(4f, (180f, 100f, 11.5f), (180f, 82f, 11.5f));
            Gate(180f, 92f, 4f, 11.5f, true);
            Lane(4f, (72f, 100f, 8f), (72f, 92f, 8f));
            Pile(72f, 96f, 4f, 8f, true);

            // Torres perto do que o jogador vê: cada uma abre um vão na fileira, com a frente alinhada às fachadas
            var towerRoot = new GameObject("ArchenKaedra_Torres").transform;
            towerRoot.SetParent(s_World, false);

            void PlaceTower(string model, float cx, float cz, float half, float level, Vector3 front, float scale = 1f)
            {
                float h = half * scale;
                Mark(cx - h, cx + h, cz - h, cz + h, 2);
                if (level > 0.5f)
                    Block(cx - h, cx + h, cz - h, cz + h, -1f, level - 0.03f);
                PlaceTech(model, P(cx, cz, level), YawOf(front), towerRoot, scale, false);
                Barrier(cx - h, cx + h, cz - h, cz + h, level, level + 48f);
            }

            void RowTower(string model, bool alongX, float fixedAt, float outward, float at, float half, float level)
            {
                mouths.Add((alongX, fixedAt, outward, at - half, at + half));
                float depth = fixedAt + outward * half;
                PlaceTower(model, alongX ? at : depth, alongX ? depth : at, half, level, (alongX ? toBridge : across) * -outward);
            }

            RowTower("AR_Torre_Sineira", true, 222f, 1f, -104f, 10f, 0f);               // Relojoeiros, norte
            RowTower("AR_Torre_Ressonancia", true, 118f, -1f, -121f, 9f, 0f);           // Relojoeiros, sul
            RowTower("AR_Torre_Palacio", false, -182f, -1f, 195f, 14f, 4f);             // Relojoeiros, terraço oeste
            RowTower("AR_Torre_Sineira", true, 183f, -1f, -59.8f, 10f, 0f);             // rua de entrada dos Relojoeiros
            RowTower("AR_Torre_Sineira", true, 200f, 1f, 160f, 10f, 8f);                // Mercado, norte
            RowTower("AR_Torre_Ressonancia", false, 182f, 1f, 190f, 9f, 8f);            // Mercado, leste
            RowTower("AR_Torre_Palacio", true, 100f, -1f, 104f, 14f, 8f);               // Mercado, sul

            // No trecho morto da rua, entre o bloqueio e a rampa: aparecem por cima do desabamento
            PlaceTower("AR_Torre_Sineira", -12f, 133.5f, 10f, 0f, toBridge, 0.95f);
            PlaceTower("AR_Torre_Ressonancia", 11f, 133.5f, 9f, 0f, toBridge);

            // Avenidas largas, fechadas por desabamentos que a rua toda não vence
            Lane(10f, (11.5f, 178f, Street), (54f, 178f, Street));              // Cerco, leste: a grande rua transversal
            BigPile(36f, 178f, 10f, Street, false);
            foreach (float px in new[] { 18f, 28f })
                foreach (float pz in new[] { 174f, 182f })
                    Prop(px, pz, 0.5f, 0.5f, Street, 5f);                       // postes
            Prop(24f, 179f, 3f, 7f, Street, 2.2f);                              // cápsula tombada

            Lane(10f, (-145f, 118f, Court), (-145f, 102f, Court), (-145f, 90f, 4f), (-145f, 62f, 4f));   // Relojoeiros, sul: sobe em degraus
            BigPile(-145f, 76f, 10f, 4f, true);
            Prop(-148f, 110f, 2f, 2f, Court, 3f);
            Prop(-142f, 110f, 2f, 2f, Court, 3f);

            Lane(10f, (-85f, 222f, Court), (-85f, 268f, Court));                // Relojoeiros, norte: avenida reta com tambores caídos
            BigPile(-85f, 248f, 10f, Court, true, 14f, 10f);
            Prop(-88f, 232f, 2.4f, 2.4f, Court, 1.2f);
            Prop(-81f, 238f, 2.4f, 2.4f, Court, 1.2f);

            Lane(10f, (182f, 176f, 8f), (220f, 176f, 8f));                      // Mercado, leste: rua alta larga
            BigPile(204f, 176f, 10f, 8f, false);
            Prop(192f, 172.5f, 4f, 1.6f, 8f, 1.4f);

            Lane(12f, (140f, 200f, 8f), (140f, 216f, 2f), (140f, 232f, 2f));    // Mercado, norte: descida larga até um largo soterrado
            BigPile(140f, 225f, 12f, 2f, true);

            // Vielas apertadas de 3 m, com portão; atrás dele o caminho segue, às vezes virando
            Lane(3f, (11.5f, 202f, Street), (42f, 202f, Street), (42f, 195f, Street));
            Gate(19f, 202f, 3f, Street, false);
            Prop(30f, 203f, 1f, 1f, Street, 0.9f);

            Lane(3f, (11.5f, 230f, Street), (60f, 230f, Street));
            Gate(20f, 230f, 3f, Street, false);
            Span(38f, 230f, 3f, false, 4.2f, 5f);
            Span(50f, 230f, 3f, false, 4.2f, 5f);

            Lane(3f, (-11.5f, 234f, Street), (-38f, 234f, Street));
            Gate(-20f, 234f, 3f, Street, false);

            Lane(3f, (-165f, 118f, 4f), (-165f, 88f, 4f), (-178f, 88f, 4f));    // Relojoeiros, terraço oeste
            Gate(-165f, 110f, 3f, 4f, true);

            Lane(3f, (-140f, 222f, Court), (-140f, 244f, Court));               // Relojoeiros, pátio
            Gate(-140f, 230f, 3f, Court, true);

            Lane(3f, (165f, 100f, 11.5f), (165f, 72f, 11.5f), (180f, 72f, 11.5f));   // Mercado, terraço alto
            Gate(165f, 92f, 3f, 11.5f, true);

            Lane(3f, (85f, 200f, 8f), (85f, 236f, 8f), (96f, 236f, 8f));        // Mercado, norte
            Gate(85f, 208f, 3f, 8f, true);
            Span(85f, 222f, 3f, true, 12.2f, 13f);

            // Ruas de fora (ninguém chega nelas; dão a textura de cidade de verdade vista do alto)
            void Avenue(float x0, float x1, float z0, float z1)
            {
                for (int i = Mathf.Max(0, Mathf.FloorToInt(x0) + Half); i <= Mathf.Min(Half * 2, Mathf.CeilToInt(x1) + Half - 1); i++)
                    for (int j = Mathf.Max(0, Mathf.FloorToInt(z0) + Half); j <= Mathf.Min(Half * 2, Mathf.CeilToInt(z1) + Half - 1); j++)
                        if (cells[i, j] == 0)
                            cells[i, j] = 1;
            }

            Avenue(-300f, 300f, -204f, -196f);
            Avenue(-300f, 300f, -64f, -56f);
            Avenue(-300f, -70f, 36f, 44f);
            Avenue(70f, 300f, 36f, 44f);
            Avenue(-234f, -226f, -280f, 260f);
            Avenue(226f, 234f, -280f, 260f);
            Avenue(-124f, -116f, -300f, 90f);
            Avenue(116f, 124f, -300f, 90f);
            Avenue(-204f, -198f, -180f, 90f);
            Avenue(190f, 196f, -180f, 90f);
            Avenue(-60f, -54f, -120f, -60f);
            Avenue(54f, 60f, -120f, -60f);
            Avenue(-300f, -240f, 186f, 192f);
            Avenue(70f, 260f, 262f, 268f);

            // ---------- Rua do Cerco ----------
            Block(-11.5f, 11.5f, 143f, zStart + 1f, -0.6f, 0.18f);
            Row(false, -11.5f, 159f, zStart, -1f, 0f, (182f, 198f));
            Row(false, 11.5f, 159f, zStart, 1f, 0f);
            foreach (float s in new[] { -1f, 1f })
                Block(Mathf.Min(s * 11.5f, s * 15f), Mathf.Max(s * 11.5f, s * 15f), 141f, 159f, -1f, 7.3f);    // fecha embaixo do viaduto

            // O bloqueio: desabamento que fecha a rua toda
            Block(-13f, 13f, 160f, 172f, -1f, 9f);
            for (int k = 0; k < 16; k++)
            {
                float size = Range(2.5f, 5.5f);
                BlockMesh.ChamferBox(m.Pick(), P(Range(-12f, 12f), Range(158f, 174f), Range(3f, 13f)), new Vector3(size * 1.5f, size, size),
                    q * Quaternion.Euler(Range(-30f, 30f), Range(0f, 360f), Range(-30f, 30f)), 0.15f);
            }

            // ---------- Bairro dos Relojoeiros (x -182 a -70, z 118 a 222) ----------
            Block(-69.8f, 10f, 183f, 197f, -0.6f, 0.14f);       // rua de entrada, a oeste do bloqueio
            Row(true, 197f, -69.8f, -11.5f, 1f, 0f);
            Row(true, 183f, -69.8f, -11.5f, -1f, 0f);
            Block(-182f, -70f, 118f, 222f, -0.6f, 0.12f);       // pátio
            Block(-182f, -150f, 118f, 222f, -1f, 4f);           // terraço oeste, +4 m
            Block(-112f, -70f, 118f, 172f, -1f, 8f);            // terraço leste, +8 m (de onde sai o viaduto)
            Ramp(-142f, 172f, 0.12f, -158f, 172f, 4f, 14f);
            Ramp(-106f, 212f, 0.12f, -106f, 172f, 8f, 12f);
            // Passarela do terraço oeste à rampa (na altura do meio dela)
            Block(-150.2f, -111.8f, 186f, 198f, 3.5f, 4.04f);
            foreach (float x in new[] { -142f, -130f, -118f })
                Block(x - 0.7f, x + 0.7f, 191.3f, 192.7f, -1f, 3.5f);
            // Cobertura no pátio
            foreach ((float x, float z, float w, float d, float h) in new[]
                     {
                         (-138f, 215f, 10f, 3f, 2.6f), (-124f, 208f, 3f, 9f, 2.2f), (-92f, 208f, 10f, 3f, 2.8f), (-84f, 180f, 3f, 8f, 2.2f),
                         (-140f, 150f, 3f, 11f, 2.8f), (-128f, 135f, 11f, 3f, 2.2f), (-122f, 158f, 3f, 8f, 3f), (-136f, 178f, 7f, 3f, 2f),
                     })
                Block(x - w * 0.5f, x + w * 0.5f, z - d * 0.5f, z + d * 0.5f, -1f, h);
            Row(true, 222f, -182f, -150f, 1f, 4f);
            Row(true, 222f, -150f, -70f, 1f, 0f);
            Row(true, 118f, -182f, -150f, -1f, 4f);
            Row(true, 118f, -150f, -112f, -1f, 0f);
            Row(true, 118f, -112f, -70f, -1f, 8f);
            Row(false, -182f, 118f, 222f, -1f, 4f);
            Row(false, -70f, 172f, 222f, 1f, 0f, (182f, 198f));
            Row(false, -70f, 118f, 172f, 1f, 8f, (143f, 157f));

            // ---------- viaduto, por cima da rua (o chão dele fica a +8 m) ----------
            Block(-70.5f, 70.5f, 143.5f, 156.5f, 7.3f, 8.06f);
            foreach (float z in new[] { 143.8f, 156.2f })
                Block(-70f, 70f, z - 0.3f, z + 0.3f, 8f, 9.2f);
            foreach (float x in new[] { -60f, -40f, -20f, 20f, 40f, 60f })
                Block(x - 1.5f, x + 1.5f, 144.5f, 155.5f, -1f, 7.4f);

            // Galeria coberta com grades (só visual; quem segura o jogador são as barreiras abaixo) e um pórtico em cada ponta
            var gallery = new GameObject("ArchenKaedra_Galeria").transform;
            gallery.SetParent(s_World, false);
            float galleryYaw = YawOf(toBridge);
            for (float x = -65f; x <= 65.1f; x += 10f)
                PlaceTech("AR_Viaduto_Galeria", P(x, 150f, 8.06f), galleryYaw, gallery, 1f, false);
            foreach (float x in new[] { -70f, 70f })
                PlaceTech("AR_Viaduto_Portal", P(x, 150f, 8.06f), galleryYaw, gallery, 1f, false);

            // Barreiras invisíveis altas (também contra pulo extra e dash): dos dois lados do viaduto, para ninguém pular dele
            // para a rua depois do bloqueio e pular o mercado, e em cima do bloqueio
            foreach (float z in new[] { 143.8f, 156.2f })
                Barrier(-70f, 70f, z - 0.3f, z + 0.3f, 8f, 48f);
            Barrier(-15f, 15f, 165.7f, 166.3f, 0f, 48f);

            // ---------- Mercado Alto (x 70 a 182, z 100 a 200, a +8 m) ----------
            Block(70f, 182f, 100f, 200f, -1f, 8f);
            Row(true, 200f, 70f, 182f, 1f, 8f);
            Row(true, 100f, 70f, 182f, -1f, 8f);
            Row(false, 182f, 100f, 200f, 1f, 8f);
            Row(false, 70f, 100f, 200f, -1f, 8f, (110f, 124f), (143f, 157f));
            // Salão coberto: pilares e teto
            for (float x = 100f; x <= 150.1f; x += 10f)
                for (float z = 130f; z <= 175.1f; z += 15f)
                    Block(x - 0.7f, x + 0.7f, z - 0.7f, z + 0.7f, 7.5f, 15.2f);
            Block(97f, 153f, 127f, 178f, 15f, 16f);
            // Terraço a +11,5 m, com a rampa
            Block(150f, 182f, 100f, 128f, 7f, 11.5f);
            Ramp(130f, 114f, 8f, 150.2f, 114f, 11.5f, 10f);
            // Trilho elevado cruzando o mercado (só o volume)
            Block(72f, 180f, 186f, 189f, 13f, 13.7f);
            for (float x = 80f; x < 180f; x += 16f)
                Block(x - 0.6f, x + 0.6f, 186.9f, 188.1f, 7.5f, 13f);
            // Bancas
            foreach ((float x, float z, float w, float d) in new[]
                     {
                         (84f, 130f, 8f, 3f), (88f, 168f, 3f, 8f), (88f, 182f, 9f, 3f), (125f, 186f, 3f, 7f), (163f, 150f, 8f, 3f),
                         (168f, 178f, 3f, 9f), (112f, 112f, 9f, 3f), (150f, 192f, 8f, 3f),
                     })
                Block(x - w * 0.5f, x + w * 0.5f, z - d * 0.5f, z + d * 0.5f, 7.5f, 8f + Range(1.6f, 2.8f));

            // A saída do mercado só sobe: escadaria de +8 m até um patamar encostado na frente do pódio da basílica
            // (o patamar entra 5 cm abaixo do piso do pódio, para os dois não ficarem no mesmo plano)
            Ramp(70.2f, 117f, 8f, 9f, 117f, k_CityPodium + 0.05f, 14f);
            Block(-9f, 9.2f, 102f, 124f, -1f, k_CityPodium + 0.05f);
            // Sem descida para a rua cortada lá embaixo (de onde não haveria volta)
            Barrier(9f, 70f, 109.7f, 110f, 0f, k_CityPodium + 48f);
            Barrier(-9f, 70f, 124f, 124.3f, 0f, k_CityPodium + 48f);
            Barrier(-9.3f, -9f, 104f, 124.3f, 0f, k_CityPodium + 48f);
            Barrier(9.2f, 9.5f, 104f, 110f, 0f, k_CityPodium + 48f);

            // ---------- o resto da cidade: quarteirões em volta ----------
            var reserved = new[]
            {
                (X0: -32f, X1: 32f, Z0: 90f, Z1: zStart + 6f), (X0: -202f, X1: -50f, Z0: 98f, Z1: 242f), (X0: 50f, X1: 202f, Z0: 80f, Z1: 220f),
                (X0: -72f, X1: 72f, Z0: 138f, Z1: 212f), (X0: 8f, X1: 72f, Z0: 104f, Z1: 130f), (X0: -66f, X1: 66f, Z0: -300f, Z1: -120f),
            };

            var towerAreas = new System.Collections.Generic.List<(float X0, float X1, float Z0, float Z1)>();

            bool Free(float x, float z, float halfX, float halfZ)
            {
                foreach (var r in reserved)
                    if (x + halfX > r.X0 - 3f && x - halfX < r.X1 + 3f && z + halfZ > r.Z0 - 3f && z - halfZ < r.Z1 + 3f)
                        return false;
                foreach (var r in towerAreas)
                    if (x + halfX > r.X0 - 3f && x - halfX < r.X1 + 3f && z + halfZ > r.Z0 - 3f && z - halfZ < r.Z1 + 3f)
                        return false;
                float radius = Mathf.Sqrt(x * x + z * z) + Mathf.Max(halfX, halfZ);
                if (radius > k_CityRadius * 0.84f || radius - 2f * Mathf.Max(halfX, halfZ) < 128f)
                    return false;
                Vector3 p = P(x, z);
                return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(barbican.x, barbican.z)) > 62f + Mathf.Max(halfX, halfZ);
            }

            bool MaskClear(float x, float z, float half)
            {
                for (int i = Mathf.FloorToInt(x - half) + Half; i <= Mathf.CeilToInt(x + half) + Half; i++)
                    for (int j = Mathf.FloorToInt(z - half) + Half; j <= Mathf.CeilToInt(z + half) + Half; j++)
                        if (i < 0 || j < 0 || i > Half * 2 || j > Half * 2 || cells[i, j] != 0)
                            return false;
                return true;
            }

            // Torres, entre os quarteirões
            (string Model, float Half)[] towers =
            {
                ("AR_Torre_Sineira", 10f), ("AR_Torre_Ressonancia", 9f), ("AR_Torre_Palacio", 14f), ("AR_Torre_Sineira", 10f),
                ("AR_Torre_Partida", 20f), ("AR_Torre_Ressonancia", 9f), ("AR_Torre_Palacio", 14f),
            };
            for (int k = 0, placed = 0; k < 400 && placed < 14; k++)
            {
                (string model, float half) = towers[placed % towers.Length];
                float a = Range(0f, Mathf.PI * 2f), d = Range(140f, 275f), scale = Range(0.8f, 1.15f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                if (!Free(x, z, half * scale, half * scale) || !MaskClear(x, z, half * scale + 2f))
                    continue;
                // Frente para o meio da cidade; só cenário, longe das ruas
                PlaceTech(model, P(x, z), YawOf((city - P(x, z)).normalized), towerRoot, scale, false);
                towerAreas.Add((x - half * scale, x + half * scale, z - half * scale, z + half * scale));
                Mark(x - half * scale, x + half * scale, z - half * scale, z + half * scale, 2);
                placed++;
            }

            // Quadras: tudo o que a máscara deixou livre vira lotes construídos, encostados nos becos e nas ruas (sem frestas)
            var nearLane = new float[Half * 2 + 1, Half * 2 + 1];
            for (int i = 0; i <= Half * 2; i++)
                for (int j = 0; j <= Half * 2; j++)
                    nearLane[i, j] = -1f;
            const int Reach = 16;
            for (int i = 0; i <= Half * 2; i++)
                for (int j = 0; j <= Half * 2; j++)
                {
                    float level = laneLevel[i, j];
                    if (level < 0f)
                        continue;
                    for (int a = Mathf.Max(0, i - Reach); a <= Mathf.Min(Half * 2, i + Reach); a++)
                        for (int b = Mathf.Max(0, j - Reach); b <= Mathf.Min(Half * 2, j + Reach); b++)
                            nearLane[a, b] = Mathf.Max(nearLane[a, b], level);
                }

            bool Buildable(int i, int j)
            {
                if (cells[i, j] != 0)
                    return false;
                float x = i - Half + 0.5f, z = j - Half + 0.5f;
                float radius = Mathf.Sqrt(x * x + z * z);
                if (radius > k_CityRadius * (nearLane[i, j] >= 0f ? 0.9f : 0.84f))
                    return false;
                Vector3 p = P(x, z);
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(barbican.x, barbican.z)) < 62f)
                    return false;
                if (nearLane[i, j] >= 0f || (x > -66f && x < 55f && z > 105f && z < 143f))
                    return true;
                if (radius < 128f)
                    return false;
                foreach (var r in reserved)
                    if (x > r.X0 - 3f && x < r.X1 + 3f && z > r.Z0 - 3f && z < r.Z1 + 3f)
                        return false;
                return true;
            }

            int lots = 0;
            var done = new bool[Half * 2 + 1, Half * 2 + 1];
            for (int i = 0; i < Half * 2; i++)
                for (int j = 0; j < Half * 2; j++)
                {
                    if (done[i, j] || !Buildable(i, j))
                        continue;
                    int maxW = (int)Range(9f, 20f), maxD = (int)Range(9f, 20f), w = 1, d = 1;
                    while (w < maxW && i + w <= Half * 2 && !done[i + w, j] && Buildable(i + w, j))
                        w++;
                    bool grow = true;
                    while (grow && d < maxD && j + d <= Half * 2)
                    {
                        for (int a = 0; a < w; a++)
                            if (done[i + a, j + d] || !Buildable(i + a, j + d))
                            {
                                grow = false;
                                break;
                            }
                        if (grow)
                            d++;
                    }

                    float lotLevel = -1f;
                    for (int a = 0; a < w; a++)
                        for (int b = 0; b < d; b++)
                        {
                            done[i + a, j + b] = true;
                            lotLevel = Mathf.Max(lotLevel, nearLane[i + a, j + b]);
                        }

                    float x0 = i - Half, z0 = j - Half;
                    float top = lotLevel >= 0f ? lotLevel + Range(11f, 22f) : Range(14f, 34f);
                    if (x0 > -66f && x0 < 55f && z0 > 105f && z0 < 143f)
                        top = Range(26f, 44f);      // o buraco atrás da rampa: prédios altos
                    if (Mathf.Min(w, d) < 5)
                        Block(x0, x0 + w, z0, z0 + d, -1f, top * 0.8f);
                    else
                        Building(x0 + w * 0.5f, z0 + d * 0.5f, w, d, top);
                    lots++;
                }

            Debug.Log($"[Ícaro] Archen Kaedra: {lots} lotes em blocos, {mouths.Count} bocas de beco.");

            // Ponto de chegada para o teste
            var arrival = new GameObject("Chegada_ArchenKaedra");
            arrival.transform.SetParent(s_World, false);
            arrival.transform.SetPositionAndRotation(P(0f, zStart - 5f, 2f), Quaternion.LookRotation(-toBridge));
        }
    }
}
