using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

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

            // Cada bloco é uma peça própria (PicosCityBlocks), com a marca dos ajustes manuais; o tom continua saindo do
            // sorteio da malha do esboço, para os outros tons não mudarem
            s_ArchenBuilt = true;
            s_ArchenCity = city;
            s_ArchenToBridge = toBridge;
            BeginCityBlocks();
            var blockRoot = new GameObject("ArchenKaedra_Blocos").transform;
            blockRoot.SetParent(s_World, false);

            void Block(float x0, float x1, float z0, float z1, float y0, float y1)
            {
                int tone = System.Array.IndexOf(m.Tones, m.Pick());
                CityBlock(blockRoot, P((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, Mathf.Min(y0, y1)), q,
                    new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), tone);
            }

            // Rampa maciça de (xa, za, ya) a (xb, zb, yb), com largura 'width'
            void Ramp(float xa, float za, float ya, float xb, float zb, float yb, float width)
            {
                Vector3 flat = (P(xb, zb) - P(xa, za)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, flat);
                ConvexPrism(m.Pick(), new[] { P(xa, za, -0.5f), P(xb, zb, -0.5f), P(xb, zb, yb), P(xa, za, ya) }, side, width);
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
            var fronts = new List<(bool AlongX, float FixedAt, float A, float B, float Level)>();    // fachadas de prédio do kit

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


            // Prédios leves: esticados para caber no lote
            (string Model, float W, float D, float H)[] lotModels =
            {
                ("AR_Lote_Empena", 12f, 12f, 15f), ("AR_Lote_Torreada", 12f, 12f, 15f), ("AR_Lote_Terraco", 14f, 14f, 14f),
                ("AR_Lote_Alto", 10f, 10f, 24f), ("AR_Lote_Quebrada", 12f, 12f, 13f), ("AR_Lote_Cumeeira", 16f, 11f, 13f),
                ("AR_Lote_Estreito", 6f, 12f, 16f), ("AR_Lote_Estreito_Alto", 6f, 10f, 21f),
            };
            // Cada prédio leve é uma peça do kit (com a marca dos ajustes manuais: dá para mover, girar, esticar ou apagar
            // à mão e o ajuste sobrevive a gerar de novo; pedido do usuário em 2026-11-10), sem colisão (as barreiras seguram)
            var lotRoot = new GameObject("ArchenKaedra_Lotes").transform;
            lotRoot.SetParent(s_World, false);
            int lotObjects = 0;
            // As malhas somadas da versão anterior (uma por material e trecho) não são mais usadas
            foreach (string guid in AssetDatabase.FindAssets("Picos_ArchenLotes_ t:Mesh"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path).StartsWith("Picos_ArchenLotes_"))
                    AssetDatabase.DeleteAsset(path);
            }

            // 'size' multiplica o tamanho do modelo: x na largura da frente, y na altura, z no fundo
            void AddLotMesh(string model, Vector3 position, float yaw, Vector3 size)
            {
                GameObject go = PicosGothicKit.Place(s_Materials, model, position, yaw, lotRoot, false, PicosGothicKit.VillagesFolder);
                if (!go)
                    return;
                lotObjects++;
                // a raiz do FBX pode vir girada: cada eixo dela recebe a escala do eixo da frente com que ela se alinha
                Quaternion local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * go.transform.rotation;
                Vector3 scale = go.transform.localScale;
                Vector3[] axes = { local * Vector3.right, local * Vector3.up, local * Vector3.forward };
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v = axes[k];
                    float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
                    scale[k] *= ax >= ay && ax >= az ? size.x : ay >= az ? size.y : size.z;
                }

                go.transform.localScale = scale;
            }

            int streetLots = 0;
            // Prédio leve na linha de uma fachada, de frente para a rua: (a, b) ao longo da linha, 'd0' de recuo e 'deep' de
            // fundo; trechos compridos viram vários prédios (os estreitos até 8,5 m, os largos acima)
            void StreetLot(bool alongX, float fixedAt, float a, float b, float d0, float deep, float outward, float level, float top)
            {
                float length = b - a;
                if (length < 2.5f)
                    return;
                if (level > 0.5f)
                    LineBlock(alongX, fixedAt, a, b, d0, d0 + deep, outward, -1f, level - 0.01f);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / 13f));
                float piece = length / count;
                Vector3 front = (alongX ? toBridge : across) * -outward;
                for (int k = 0; k < count; k++)
                {
                    float along = a + piece * (k + 0.5f), depthAt = fixedAt + outward * (d0 + deep * 0.5f);
                    int pick = piece < 8.5f ? (Range(0f, 1f) < 0.55f ? 6 : 7) : (Range(0f, 1f) < 0.5f ? 0 : 1);
                    var lot = lotModels[pick];
                    float cx = alongX ? along : depthAt, cz = alongX ? depthAt : along;
                    float height = Mathf.Clamp((top - level) / lot.H, 0.75f, 1.5f);
                    AddLotMesh(lot.Model, P(cx, cz, level), YawOf(front), new Vector3((piece - 0.12f) / lot.W, height, deep / lot.D));
                    streetLots++;
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
                    StreetLot(alongX, fixedAt, start, end, 0.3f, 10f, outward, level, level + Range(13f, 19f));
                    return;
                }

                // Parede lisa recuada atrás da fileira, que fecha as frestas entre um prédio e outro
                LineBlock(alongX, fixedAt, start, end, 1.2f, 6f, outward, -1f, level + 8f);
                // Sobra de 4 m ou mais: prédios estreitos recuados entre os do kit (-1 na sequência), em lugares sorteados
                var sequence = new List<int>(best);
                int fillers = bestLeft >= 4f ? Mathf.Min(Mathf.FloorToInt(bestLeft / 4f), best.Count + 1) : 0;
                float fillerSlot = fillers > 0 ? bestLeft / fillers : 0f;
                for (int k = 0; k < fillers; k++)
                    sequence.Insert(Mathf.Min((int)Range(0f, sequence.Count + 1), sequence.Count), -1);
                float extra = fillers > 0 ? 0f : bestLeft / best.Count, cursor = start;
                bool street = !alongX && Mathf.Abs(fixedAt) < 12f;
                foreach (int index in sequence)
                {
                    if (index < 0)
                    {
                        StreetLot(alongX, fixedAt, cursor + 0.04f, cursor + fillerSlot - 0.04f, 0.7f, 10f, outward, level, level + Range(13f, 19f));
                        cursor += fillerSlot;
                        continue;
                    }

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
                        // Esquina: outro prédio já ocupa o fundo; um prédio leve raso fecha a frente
                        StreetLot(alongX, fixedAt, along - slot * 0.5f, along + slot * 0.5f, 0.3f, 6f, outward, level, level + Range(12f, 17f));
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
                    fronts.Add((alongX, fixedAt, along - wide * 0.5f, along + wide * 0.5f, level));
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

            // Peças do kit das arenas (AR_Arenas, pedido do usuário em 2026-11-10: no lugar dos blocos das arenas). 'turn' gira
            // em volta do eixo vertical a partir da pose em que o X do Blender fica ao longo do x da cidade e o Y ao longo do
            // z (a frente, -Y, para -z): 180 vira a frente para +z, 90 para -x (X do Blender em -z) e -90 para +x (X do
            // Blender em +z); 'size' estica nos eixos do Blender (x, y, z). Todas com a marca dos ajustes manuais.
            var arenaRoot = new GameObject("ArchenKaedra_Arenas").transform;
            arenaRoot.SetParent(s_World, false);
            float cityYaw = YawOf(toBridge);
            int arenaPieces = 0;

            GameObject ArenaPiece(string model, float x, float z, float y, float turn, Vector3 size, bool collider = true)
            {
                float yaw = cityYaw + 180f + turn;
                GameObject go = PicosGothicKit.Place(s_Materials, model, P(x, z, y), yaw, arenaRoot, collider, PicosGothicKit.VillagesFolder);
                if (!go)
                    return null;
                arenaPieces++;
                // a raiz do FBX pode vir girada: x do Blender no x local, y no z local e z (altura) no y local
                Quaternion local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * go.transform.rotation;
                Vector3 scale = go.transform.localScale;
                Vector3[] axes = { local * Vector3.right, local * Vector3.up, local * Vector3.forward };
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v = axes[k];
                    float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
                    scale[k] *= ax >= ay && ax >= az ? size.x : ay >= az ? size.z : size.y;
                }

                go.transform.localScale = scale;
                return go;
            }

            // Cenário das arenas (kits dos ataques, da tecnologia e das casas), com a mesma regra de giro do ArenaPiece
            int dressPieces = 0;
            void Dress(string model, float x, float z, float y, float turn, bool collider = true)
            {
                if (ArenaPiece(model, x, z, y, turn, Vector3.one, collider))
                {
                    arenaPieces--;
                    dressPieces++;
                }
            }

            // Balaustrada de cantaria de 'from' a 'to' (pontos no piso; pode subir, como numa escada): plinto, balaústres,
            // corrimão e pilaretes com tampa nas pontas e a cada 6,4 m no máximo
            void Balustrade(Vector3 from, Vector3 to)
            {
                Vector3 along = to - from;
                Vector3 flat = new Vector3(along.x, 0f, along.z);
                Quaternion slope = Quaternion.LookRotation(along), upright = Quaternion.LookRotation(flat);
                float length = along.magnitude, run = flat.magnitude;
                Vector3 middle = (from + to) * 0.5f;
                BlockMesh.ChamferBox(m.Tones[0], middle + Vector3.up * 0.13f, new Vector3(0.44f, 0.3f, length), slope, 0.02f);
                BlockMesh.ChamferBox(m.Tones[0], middle + Vector3.up * 1.12f, new Vector3(0.4f, 0.15f, length), slope, 0.02f);
                int bays = Mathf.Max(1, Mathf.CeilToInt(run / 6.4f));
                for (int k = 0; k <= bays; k++)
                {
                    Vector3 post = Vector3.Lerp(from, to, (float)k / bays);
                    BlockMesh.ChamferBox(m.Tones[0], post + Vector3.up * 0.55f, new Vector3(0.6f, 1.25f, 0.6f), upright, 0.03f);
                    BlockMesh.ChamferBox(m.Tones[0], post + Vector3.up * 1.22f, new Vector3(0.75f, 0.12f, 0.75f), upright, 0.02f);
                }

                int count = Mathf.RoundToInt(run / 0.62f);
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count, nearPost = Mathf.Abs(t * bays - Mathf.Round(t * bays)) * run / bays;
                    if (nearPost > 0.45f)
                        BlockMesh.ChamferBox(m.Tones[0], Vector3.Lerp(from, to, t) + Vector3.up * 0.66f, new Vector3(0.17f, 0.8f, 0.17f), upright, 0.01f);
                }
            }

            // Muro de arrimo de um terraço na linha 'fixedAt' (de 'a' a 'b'), virado para 'outward': talude no pé, faixa no
            // meio, cornija sob a beira e a balaustrada em cima, com um trecho caído a cada três (por onde dá para pular); as
            // passagens ('gaps', em ordem) ficam abertas e 'trimA'/'trimB' encurtam a balaustrada nas pontas (quinas). As
            // peças da linha ao longo de z ficam 3 cm abaixo das da linha ao longo de x, para as quinas não ficarem no mesmo
            // plano.
            void TerraceEdge(bool alongX, float fixedAt, float a, float b, float outward, float floor, float top, float trimA, float trimB,
                params (float A, float B)[] gaps)
            {
                Vector3 W(float along, float off, float y) => alongX ? P(along, fixedAt + outward * off, y) : P(fixedAt + outward * off, along, y);
                var spans = new List<(float A, float B)>();
                float cursor = a;
                foreach ((float ga, float gb) in gaps)
                {
                    if (ga > cursor)
                        spans.Add((cursor, ga));
                    cursor = Mathf.Max(cursor, gb);
                }

                if (b > cursor)
                    spans.Add((cursor, b));
                float drop = alongX ? 0f : 0.03f;
                foreach ((float s0, float s1) in spans)
                {
                    float length = s1 - s0, mid = (s0 + s1) * 0.5f;
                    Vector3 dir = (W(s1, 0f, 0f) - W(s0, 0f, 0f)).normalized;
                    Quaternion rotation = Quaternion.LookRotation(dir);
                    ConvexPrism(m.Pick(), new[] { W(mid, -0.1f, floor - 0.3f), W(mid, 0.75f, floor - 0.3f), W(mid, -0.1f, floor + 1.7f) }, dir, length);
                    BlockMesh.ChamferBox(m.Pick(), W(mid, 0.06f, floor + (top - floor) * 0.55f - drop), new Vector3(0.32f, 0.26f, length + 0.4f), rotation, 0.03f);
                    BlockMesh.ChamferBox(m.Pick(), W(mid, 0.12f, top - 0.27f - drop), new Vector3(0.64f, 0.44f, length + 0.4f), rotation, 0.05f);

                    // balaustrada em trechos de ~6,4 m, com um trecho caído a cada três
                    float r0 = s0 + (s0 == a ? trimA : 0f), r1 = s1 - (s1 == b ? trimB : 0f);
                    int pieces = Mathf.Max(1, Mathf.RoundToInt((r1 - r0) / 6.4f));
                    int start = 0;
                    for (int k = 0; k <= pieces; k++)
                    {
                        bool fallen = k < pieces && pieces >= 3 && k % 3 == 1;
                        if (k == pieces || fallen)
                        {
                            if (k > start)
                                Balustrade(W(Mathf.Lerp(r0, r1, (float)start / pieces), -0.4f, top), W(Mathf.Lerp(r0, r1, (float)k / pieces), -0.4f, top));
                            start = k + 1;
                        }
                    }
                }
            }

            // Balaustradas dos dois lados de uma escadaria de 'a' (embaixo) a 'b' (em cima), 0,3 m para dentro, acabando 0,4 m
            // antes do topo (a do terraço começa ali); 'gaps' são trechos abertos no lado de Cross(up, a para b) (o direito, olhando de 'a'),
            // como frações do comprimento
            void StairRails(Vector3 a, Vector3 b, float width, params (float T0, float T1)[] gaps)
            {
                Vector3 flat = b - a;
                flat.y = 0f;
                float run = flat.magnitude, end = 1f - 0.4f / run;
                Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * (width * 0.5f - 0.3f);
                Balustrade(a - side, Vector3.Lerp(a, b, end) - side);
                float t = 0f;
                foreach ((float t0, float t1) in gaps)
                {
                    Balustrade(Vector3.Lerp(a, b, t) + side, Vector3.Lerp(a, b, t0) + side);
                    t = t1;
                }

                Balustrade(Vector3.Lerp(a, b, t) + side, Vector3.Lerp(a, b, end) + side);
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

            // Escadaria maciça entre as paredes do beco, com a rampa invisível rente às quinas
            void Steps(Vector3 a, Vector3 b, float width, float bottom = float.NaN)
            {
                if (a.y > b.y)
                    (a, b) = (b, a);
                Vector3 flat = b - a;
                flat.y = 0f;
                Vector3 dir = flat.normalized;
                Quaternion rotation = Quaternion.LookRotation(dir);
                int steps = Mathf.Max(2, Mathf.RoundToInt((b.y - a.y) / 0.3f));
                float stepRise = (b.y - a.y) / steps, stepRun = flat.magnitude / steps;
                for (int k = 0; k < steps; k++)
                {
                    float y = a.y + (k + 1) * stepRise, height = y - (float.IsNaN(bottom) ? a.y - 1.5f : bottom);
                    Vector3 c = a + dir * (stepRun * (k + 0.5f));
                    BlockMesh.ChamferBox(m.Tones[0], new Vector3(c.x, y - height * 0.5f, c.z),
                        new Vector3(width, height, k < steps - 1 ? stepRun + 0.02f : stepRun), rotation, 0.03f);
                }

                var ramp = new GameObject("Rampa_Beco");
                ramp.transform.SetParent(s_World, false);
                Quaternion slope = Quaternion.LookRotation(b - a);
                ramp.transform.SetPositionAndRotation((a + b) * 0.5f + slope * Vector3.up * -0.12f, slope);
                ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, (b - a).magnitude);
            }

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
                        Steps(P(isX ? from : a.X, isX ? a.Z : from, a.Y), P(isX ? to : a.X, isX ? a.Z : to, b.Y), w);
                    if (i == 0)
                        mouths.Add((!isX, isX ? a.X : a.Z, dir, (isX ? a.Z : a.X) - w * 0.5f, (isX ? a.Z : a.X) + w * 0.5f));
                }
            }

            void Yard(float x0, float x1, float z0, float z1, float y)
            {
                Mark(x0, x1, z0, z1, 1, y);
                Block(x0, x1, z0, z1, -1f, y);
            }

            // Peças de bloqueio e portões do kit AR_CidadeLeve
            var debrisRoot = new GameObject("ArchenKaedra_Bloqueios").transform;
            debrisRoot.SetParent(s_World, false);
            int debrisCount = 0;

            // Bloqueio de parede a parede: 'modelWidth' é a largura da peça, e (width, depth, height) a escala em cada sentido
            void Blockage(string model, float x, float z, float y, bool acrossX, float width, float depth, float height)
            {
                Vector3 facing = acrossX ? toBridge : across;
                float yaw = YawOf(debrisCount++ % 2 == 0 ? facing : -facing);
                GameObject go = PlaceTech(model, P(x, z, y), yaw, debrisRoot, 1f, true);
                if (!go)
                    return;
                Vector3 widthAxis = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 scale = go.transform.localScale;
                Vector3[] axes = { go.transform.right, go.transform.up, go.transform.forward };
                for (int a = 0; a < 3; a++)
                    scale[a] *= Mathf.Abs(Vector3.Dot(axes[a], Vector3.up)) > 0.7f ? height
                        : Mathf.Abs(Vector3.Dot(axes[a], widthAxis)) > 0.7f ? width : depth;
                go.transform.localScale = scale;
            }

            // Destroços que fecham uma viela; a barreira invisível alta impede pular por cima
            void Pile(float x, float z, float w, float y, bool acrossX)
            {
                Blockage(debrisCount % 2 == 0 ? "PR_Bloqueio_Viela_A" : "PR_Bloqueio_Viela_B", x, z, y, acrossX, (w + 0.8f) / 5f, 0.85f, 1f);
                if (acrossX)
                    Barrier(x - w * 0.5f - 1f, x + w * 0.5f + 1f, z - 0.3f, z + 0.3f, y, y + 48f);
                else
                    Barrier(x - 0.3f, x + 0.3f, z - w * 0.5f - 1f, z + w * 0.5f + 1f, y, y + 48f);
            }

            void Prop(float cx, float cz, float w, float d, float y, float h) => Block(cx - w * 0.5f, cx + w * 0.5f, cz - d * 0.5f, cz + d * 0.5f, y - 0.3f, y + h);

            // Arco ou escora atravessada de parede a parede, entrando nos prédios dos dois lados
            // (a escora do kit: travessas a 4,5 e 5,9 m da origem, 5,2 m de comprido)
            void Span(float x, float z, float w, bool acrossX, float y0, float y1) =>
                Blockage("PR_Beco_Escora", x, z, y0 - 4.2f, acrossX, (w + 1.2f) / 5.2f, 1f, 1f);

            // Só o que o jogador alcança leva peça do kit; o que fica atrás dos portões continua em Prop e Span
            void Deco(string model, float x, float z, float y, Vector3 front, float scale = 1f, bool collider = true) =>
                PlaceTech(model, P(x, z, y), YawOf(front), debrisRoot, scale, collider);

            void Arch(float x, float z, float w, bool acrossX, float floor) =>
                Blockage("PR_Beco_Arco", x, z, floor, acrossX, (w + 1.2f) / 5.2f, 1f, 1f);

            int zN = Mathf.RoundToInt(zStart * 0.5f) * 2;
            int zFar = Mathf.Max(zN - 24, 240);
            const float Street = 0.18f, Court = 0.12f;

            // Rua do Cerco, leste: fechado logo na entrada, com a viela continuando atrás dos destroços
            Lane(4f, (11.5f, 190f, Street), (46f, 190f, Street));
            Pile(29f, 190f, 4f, Street, false);
            Deco("PR_Conteiner_Celulas", 20f, 188.7f, Street, toBridge);

            // Rua do Cerco, oeste: viela larga que vira depois de uma quadra; o braço norte fecha só depois da curva
            Lane(6f, (-11.5f, 216f, Street), (-52f, 216f, Street));
            Lane(4f, (-30f, 219f, Street), (-30f, 230f, Street));
            Pile(-30f, 226f, 4f, Street, true);
            Arch(-22f, 216f, 6f, false, Street);
            Arch(-40f, 216f, 6f, false, Street);
            Lane(4f, (-50f, 213f, Street), (-50f, 204f, Street));      // e ela desce até a rua de entrada, dando a volta (única que liga dois caminhos)
            Mark(-52f, -48f, 197f, 204f, 1, Street);
            Block(-52f, -48f, 197f, 204f, -1f, Street);
            mouths.Add((true, 197f, 1f, -52f, -48f));

            // Rua de entrada, sul: travessa curta com escoras de madeira entre as paredes; fecha antes do viaduto
            Lane(4f, (-30f, 183f, Street), (-30f, 164f, Street));
            for (float z = 179f; z >= 168f; z -= 3.5f)
                Blockage("PR_Beco_Escora", -30f, z, Street, true, 1f, 1f, 1f);
            Pile(-30f, 167f, 4f, Street, true);

            // Rua do Cerco, leste: escadaria que sobe até um pátio alto com a lojinha
            Lane(4f, (11.5f, 214f, Street), (30f, 214f, Street), (46f, 214f, 4.2f));
            Yard(46f, 68f, 205f, 223f, 4.2f);
            Deco("AR_Lojinha_A", 66f, 214f, 4.2f, -across);
            Deco("PR_Conteiner_Celulas", 49.5f, 221.8f, 4.2f, -toBridge);
            Deco("PR_Conteiner_Celulas", 51.4f, 221.8f, 4.2f, -toBridge);
            Deco("PR_Banco_Quebrado", 54f, 206.2f, 4.2f, toBridge);

            // Rua do Cerco, leste: viela do canal: duas calçadas altas e o canal congelado no meio; uma ponte e o fim soterrado
            Lane(6f, (11.5f, zFar, Street), (72f, zFar, Street));
            foreach (float side in new[] { -2f, 2f })
            {
                Block(26f, 64f, zFar + side - 1f, zFar + side + 1f, -1f, 0.75f);
                Steps(P(22f, zFar + side, Street), P(26f, zFar + side, 0.75f), 2f);
            }
            Block(25.7f, 26.1f, zFar - 1.05f, zFar + 1.05f, -1f, 0.75f);        // a cabeceira do canal
            BlockMesh.Box(s_IceSheet, P(45f, zFar, 0.38f), new Vector3(38f, 0.12f, 2.06f), q);
            Deco("PR_Ponte_Canal", 43.5f, zFar, 0.75f, across);
            Pile(58f, zFar, 6f, 0.75f, false);

            // Rua do Cerco, oeste: passagem coberta por baixo de um prédio, abrindo num pátio com poço e o nicho de um NPC
            Lane(4f, (-11.5f, zFar, Street), (-40f, zFar, Street));
            Deco("AR_Passagem_Coberta", -31f, zFar, Street, across);
            Yard(-64f, -40f, zFar - 10f, zFar + 10f, Street);
            Deco("PR_Poco", -52f, zFar, Street, across);
            Deco("PR_Nicho_NPC", -62.4f, zFar + 6f, Street, across);

            // Relojoeiros, norte: viela em L saindo do pátio
            Lane(4f, (-120f, 222f, Court), (-120f, 252f, Court), (-96f, 252f, Court));
            Pile(-104f, 252f, 4f, Court, false);

            // Relojoeiros, terraço oeste, norte: fechado logo de cara
            Lane(4f, (-160f, 222f, 4f), (-160f, 233f, 4f));
            Pile(-160f, 228f, 4f, 4f, true);

            // Relojoeiros, terraço oeste, oeste: rua alta que dobra e desce em escadaria longa até um largo soterrado
            Lane(5f, (-182f, 170f, 4f), (-220f, 170f, 4f), (-220f, 140f, 4f), (-220f, 110f, Court));
            Yard(-232f, -208f, 94f, 110f, Court);
            Deco("PR_Base_Monumento", -220f, 102f, Court, toBridge);
            Pile(-220f, 95f, 12f, Court, true);

            // Relojoeiros, sul: viela em zigue-zague com arcos atravessados
            Lane(4f, (-132f, 118f, Court), (-132f, 96f, Court), (-108f, 96f, Court), (-108f, 70f, Court));
            Arch(-132f, 108f, 4f, true, Court);
            Arch(-120f, 96f, 4f, false, Court);
            Arch(-108f, 86f, 4f, true, Court);
            Pile(-108f, 76f, 4f, Court, true);

            // Mercado, norte: escadaria que desce da plataforma até uma ruela lá embaixo
            Lane(5f, (110f, 200f, 8f), (110f, 216f, 8f), (110f, 238f, 1f), (110f, 254f, 1f));
            Pile(110f, 248f, 5f, 1f, true);

            // Mercado, leste: passagem para um pátio de colunas (a segunda lojinha)
            Lane(4f, (182f, 150f, 8f), (212f, 150f, 8f));
            Yard(212f, 236f, 136f, 164f, 8f);
            Deco("AR_Portico_Patio", 224f, 161.85f, 8f, -toBridge);
            Deco("AR_Portico_Patio", 224f, 138.15f, 8f, toBridge);
            Deco("AR_Lojinha_B", 234f, 150f, 8f, -across);

            // Mercado, sul: rua alta coberta por arcadas, que vira e acaba soterrada
            Lane(4f, (130f, 100f, 8f), (130f, 84f, 8f), (156f, 84f, 8f));
            foreach (float z in new[] { 97f, 93.7f, 90.4f })
                Arch(130f, z, 4f, true, 8f);
            Pile(150f, 84f, 4f, 8f, false);

            // ---------- avenidas soterradas e vielas com portão de ferro (a continuação aparece atrás) ----------

            // Desabamento grande: três camadas de miolo, cada uma mais curta, com blocos assentados na altura da camada
            void BigPile(float x, float z, float w, float y, bool acrossX, float depth = 12f, float height = 8f)
            {
                Blockage(debrisCount % 2 == 0 ? "PR_Bloqueio_Avenida_A" : "PR_Bloqueio_Avenida_B", x, z, y, acrossX, (w + 1.2f) / 12f, depth / 16f, height / 9.5f);
                float half = w * 0.5f + 0.6f;
                if (acrossX)
                    Barrier(x - half - 1f, x + half + 1f, z - 0.3f, z + 0.3f, y, y + 48f);
                else
                    Barrier(x - 0.3f, x + 0.3f, z - half - 1f, z + half + 1f, y, y + 48f);
            }

            // Portão de ferro gradeado de parede a parede, sob uma verga de pedra: dá para ver através, não para passar
            void Gate(float x, float z, float w, float y, bool acrossX)
            {
                PlaceTech($"PR_Portao_Ferro_{Mathf.RoundToInt(w)}", P(x, z, y), YawOf(acrossX ? toBridge : across), debrisRoot, 1f, false);
                float half = w * 0.5f;
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
                    Deco("PR_Coluna_Bobinas", px, pz, Street, toBridge, 0.65f);
            Deco("PR_Capsula_Destruida", 24f, 178f, Street, across);

            Lane(10f, (-145f, 118f, Court), (-145f, 102f, Court), (-145f, 90f, 4f), (-145f, 62f, 4f));   // Relojoeiros, sul: sobe em degraus
            BigPile(-145f, 76f, 10f, 4f, true);
            Deco("PR_Cristais_Estilhacados", -147.5f, 109f, Court, toBridge);
            Deco("PR_Banco_Quebrado", -142f, 110f, Court, toBridge);

            Lane(10f, (-85f, 222f, Court), (-85f, 268f, Court));                // Relojoeiros, norte: avenida reta com tambores caídos
            BigPile(-85f, 248f, 10f, Court, true, 14f, 10f);
            Deco("PR_Tambores_Caidos", -86.5f, 231f, Court, toBridge);
            Deco("PR_Tambores_Caidos", -83f, 239f, Court, -across);

            Lane(10f, (182f, 176f, 8f), (220f, 176f, 8f));                      // Mercado, leste: rua alta larga
            BigPile(204f, 176f, 10f, 8f, false);
            Deco("PR_Banco_Quebrado", 192f, 172.5f, 8f, toBridge);

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

            // Passadiços (kit AR_Passadicos): galerias cobertas e um arco de escora ligando as fachadas dos dois lados das
            // ruas que o jogador percorre, a 8 a 14 m; cada um só onde há prédio do kit dos dois lados (fora das bocas de beco)
            void BuildPassages()
            {
                var root = new GameObject("ArchenKaedra_Passadicos").transform;
                root.SetParent(s_World, false);
                bool Covered(bool alongX, float fixedAt, float a, float b)
                {
                    foreach (var f in fronts)
                        if (f.AlongX == alongX && Mathf.Abs(f.FixedAt - fixedAt) < 0.01f && f.Level < 0.5f && f.A <= a && f.B >= b)
                            return true;
                    return false;
                }
                // Procura, a partir de 'target' e indo para os dois lados, um ponto da rua com prédio dos dois lados numa faixa
                // de 'half' m; devolve NaN se não houver
                float Find(bool alongX, float sideA, float sideB, float target, float half)
                {
                    for (float d = 0f; d <= 24f; d += 0.5f)
                        foreach (float t in new[] { target + d, target - d })
                            if (Covered(alongX, sideA, t - half, t + half) && Covered(alongX, sideB, t - half, t + half))
                                return t;
                    return float.NaN;
                }
                int placed = 0, missing = 0;
                void Place(string model, bool alongX, float sideA, float sideB, float target, float half)
                {
                    float t = Find(alongX, sideA, sideB, target, half);
                    if (float.IsNaN(t))
                    {
                        missing++;
                        Debug.LogWarning($"[Ícaro] Archen Kaedra: {model} perto de {target} ficou de fora (falta prédio dos dois lados).");
                        return;
                    }
                    float mid = (sideA + sideB) * 0.5f;
                    // a frente da peça (o -Y do Blender) aponta ao longo da rua; o vão (X do Blender) cruza de fachada a fachada
                    Vector3 position = alongX ? P(t, mid, 0f) : P(mid, t, 0f);
                    float yaw = YawOf(alongX ? across : toBridge);
                    PlaceTech(model, position, yaw, root, 1f, false);
                    placed++;
                    if (model != "PR_Passadico_Partido_23")
                        return;
                    // Os montes de escombros do miolo que caiu, na metade +X do Blender (debaixo do vão), com colisão
                    PlaceTech("PR_Escombros_Queimados_A", KitPoint(position, yaw, 1f, new Vector3(5.6f, -0.6f, 0f)), yaw + 20f, root, 1f, true);
                    PlaceTech("PR_Escombros_Queimados_B", KitPoint(position, yaw, 1f, new Vector3(8.3f, 2.8f, 0f)), yaw - 70f, root, 1f, true);
                }

                // Rua do Cerco (23 m, ao longo de Z): galeria inteira perto da barbacã e a partida mais adiante
                Place("PR_Passadico_23", false, -11.5f, 11.5f, 262f, 2.6f);
                Place("PR_Passadico_Partido_23", false, -11.5f, 11.5f, 224f, 2.6f);
                // Rua de entrada dos Relojoeiros (14 m, ao longo de X): arco de escora e galeria
                Place("PR_Arcobotante_14", true, 183f, 197f, -26f, 1.2f);
                Place("PR_Passadico_14", true, 183f, 197f, -54f, 2.6f);
                Debug.Log($"[Ícaro] Archen Kaedra: {placed} passadiços ({missing} sem lugar).");
            }

            // ---------- Rua do Cerco ----------
            Block(-11.5f, 11.5f, 143f, zStart + 1f, -0.6f, 0.18f);
            Row(false, -11.5f, 159f, zStart, -1f, 0f, (182f, 198f));
            Row(false, 11.5f, 159f, zStart, 1f, 0f);

            // O bloqueio: desabamento que fecha a rua toda
            Blockage("PR_Bloqueio_Avenida_B", 0f, 166f, 0.1f, true, 26.5f / 12f, 0.85f, 1.15f);

            // ---------- Bairro dos Relojoeiros (x -182 a -70, z 118 a 222) ----------
            Block(-69.8f, 10f, 183f, 197f, -0.6f, 0.14f);       // rua de entrada, a oeste do bloqueio
            Row(true, 197f, -69.8f, -11.5f, 1f, 0f);
            Row(true, 183f, -69.8f, -11.5f, -1f, 0f);

            // ---------- passadiços por cima das ruas (só visual, de fachada a fachada) ----------
            BuildPassages();
            Block(-182f, -70f, 118f, 222f, -0.6f, 0.12f);       // pátio
            Block(-182f, -150f, 118f, 222f, -1f, 4f);           // terraço oeste, +4 m
            Block(-112f, -70f, 118f, 172f, -1f, 8f);            // terraço leste, +8 m (de onde sai o viaduto)
            // Escadarias no lugar das rampas (com a rampa invisível rente às quinas): a do terraço oeste, de 8 m (a rampa
            // antiga entrava no terraço e deixava um degrau de 2 m na beira), e a do terraço leste, de 20 m (era de 40 m;
            // pedido do usuário em 2026-11-10: mais curta)
            Steps(P(-142f, 172f, Court), P(-150f, 172f, 4f), 14f);
            Steps(P(-106f, 192f, Court), P(-106f, 172f, 8f), 12f);
            StairRails(P(-142f, 172f, Court), P(-150f, 172f, 4f), 14f);
            StairRails(P(-106f, 192f, Court), P(-106f, 172f, 8f), 12f);
            // Muros de arrimo com talude, cornija e balaustrada nas beiras viradas para o pátio
            TerraceEdge(false, -150f, 118f, 222f, 1f, Court, 4f, 0f, 0f, (144.2f, 155.8f), (164.6f, 179.4f));
            TerraceEdge(false, -112f, 118f, 172f, -1f, Court, 8f, 0f, 0.4f, (144.6f, 155.4f));
            TerraceEdge(true, 172f, -99.6f, -70f, 1f, Court, 8f, 0f, 0f);
            // Ponte na linha do viaduto (pedido do usuário em 2026-11-10: a ponte caía no meio da escadaria comprida e
            // depois ficou baixa demais): sai do terraço leste (+8, o alto) e atravessa o pátio reta nessa altura até a
            // beira do terraço oeste (+4); só ali, em cima do terraço oeste, uma escadaria maciça desce até ele. O piso é um
            // bloco; pilares, arcos, cornija e balaustrada são do kit
            Block(-149.95f, -112.05f, 145f, 155f, 7.5f, 8.04f);
            ArenaPiece("AR_Passarela_Patio", -131f, 150f, 0f, 0f, Vector3.one);
            Steps(P(-160f, 150f, 4f), P(-150f, 150f, 8.04f), 10f, 3.7f);
            StairRails(P(-160f, 150f, 4f), P(-150.6f, 150f, 7.8f), 10f);
            // Cobertura no pátio (kit: a mesma área no chão e a mesma altura dos blocos de antes), de frente para o meio
            foreach ((string model, float x, float z, float w, float d) in new[]
                     {
                         ("PR_Cobertura_Muro", -138f, 215f, 10f, 3f), ("PR_Cobertura_Banco", -131f, 204f, 3f, 9f),
                         ("PR_Cobertura_Muro", -92f, 208f, 10f, 3f), ("PR_Cobertura_Banco", -84f, 180f, 3f, 8f),
                         ("PR_Cobertura_Conteineres", -140f, 132f, 3f, 11f), ("PR_Cobertura_Bancada", -128f, 135f, 11f, 3f),
                         ("PR_Cobertura_Fonte", -124f, 164f, 3f, 8f), ("PR_Cobertura_Carga", -136f, 178f, 7f, 3f),
                     })
            {
                bool alongX = w > d;
                float turn = alongX ? (z < 180f ? 180f : 0f) : (x < -120f ? -90f : 90f);
                float stretch = model == "PR_Cobertura_Banco" ? Mathf.Max(w, d) / 8.5f : 1f;
                ArenaPiece(model, x, z, Court, turn, new Vector3(stretch, 1f, 1f));
            }

            // O cerco no pátio (kits AR_Ataque, AR_Tecno e AR_Casas): cratera com corpos na chegada da rua, a fonte do
            // pátio, barricada na boca da viela do sul com armas largadas, autômato destruído, cápsula queimada, coluna-
            // lanterna tombada, cristais estilhaçados, escombros queimados e o mastro com o estandarte queimado; colunas-
            // lanterna nas quinas. Posições à mão, fora das escadarias, da ponte e das bocas das ruas.
            Dress("PR_Cratera_A", -88f, 196f, Court, 30f);
            Dress("PR_Corpos_Carbonizados", -95.5f, 201.5f, Court, 70f, false);
            Dress("PR_Corpos_Carbonizados", -82f, 189f, Court, 200f, false);
            Dress("PR_Barricada_A", -132f, 121.4f, Court, 180f);
            Dress("PR_Armas_Largadas", -127.5f, 127f, Court, 15f, false);
            Dress("PR_Sentinela_Destruida", -117f, 127f, Court, 40f);
            Dress("PR_Cristais_Estilhacados", -145f, 160f, Court, 20f);
            Dress("PR_Escombros_Queimados_C", -149.4f, 128f, Court, -90f);
            Dress("PR_Capsula_Destruida", -130f, 192f, Court, 25f);
            Dress("PR_Coluna_Bobinas_Tombada", -140.5f, 195.5f, Court, 0f);
            Dress("PR_Escombros_Queimados_A", -80f, 208f, Court, 160f);
            Dress("PR_Mastro_Queimado", -100f, 216f, Court, 180f);
            foreach ((float x, float z) in new[] { (-146f, 214f), (-76f, 214f), (-76f, 175f) })
                Dress("PR_Coluna_Bobinas", x, z, Court, 0f);
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
            // Por baixo, pedra lavrada do kit AR_Arenas: um encontro de cada lado da rua (o bloco fecha a ponta das fileiras
            // de prédios, de z 141 a 159, com o torreão do lado da Rua do Cerco; a passagem ogival e o pilar de x ±20), os
            // pilares de x ±40 e ±60 e os arcos de pilar a pilar (o de 23 m por cima da rua)
            ArenaPiece("AR_Viaduto_Encontro_A", 16.5f, 150f, 0f, 0f, Vector3.one);
            ArenaPiece("AR_Viaduto_Encontro_B", -16.5f, 150f, 0f, 180f, Vector3.one);
            foreach (float x in new[] { -60f, -40f, 40f, 60f })
                ArenaPiece("AR_Viaduto_Pilar", x, 150f, 0f, 0f, Vector3.one);
            ArenaPiece("AR_Viaduto_Arco_23", 0f, 150f, 0f, 0f, Vector3.one, false);
            foreach (float s in new[] { -1f, 1f })
            {
                ArenaPiece("AR_Viaduto_Arco_17", s * 30f, 150f, 0f, 0f, Vector3.one, false);
                ArenaPiece("AR_Viaduto_Arco_17", s * 50f, 150f, 0f, 0f, Vector3.one, false);
                ArenaPiece("AR_Viaduto_Arco_8", s * 65.75f, 150f, 0f, 0f, Vector3.one, false);
            }

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
            // Salão da feira (kit AR_Salao_Feira): os 24 pilares de antes (x 100 a 150, z 130 a 175), três naves com
            // telhado de escamas; a nave do norte, do lado do trilho, perdeu o telhado em dois vãos
            ArenaPiece("AR_Salao_Feira", 125f, 152.5f, 8f, -90f, Vector3.one);
            // Terraço a +11,5 m, com a escadaria, o muro de arrimo e a balaustrada
            Block(150f, 182f, 100f, 128f, 7f, 11.5f);
            Steps(P(130f, 114f, 8f), P(150f, 114f, 11.5f), 10f);
            StairRails(P(130f, 114f, 8f), P(150f, 114f, 11.5f), 10f);
            TerraceEdge(false, 150f, 100f, 128f, -1f, 8f, 11.5f, 0f, 0.4f, (108.6f, 119.4f));
            TerraceEdge(true, 128f, 150f, 182f, 1f, 8f, 11.5f, 1f, 0f);
            // Trilho elevado cruzando o mercado (kit): pilares a cada 16 m, vigas de pilar a pilar e uma ponta com o
            // para-choque em cada lado
            for (float x = 80f; x < 180f; x += 16f)
            {
                ArenaPiece("PR_Trilho_Pilar", x, 187.5f, 8f, 0f, Vector3.one);
                if (x + 16f < 180f)
                    ArenaPiece("PR_Trilho_Viga", x + 8f, 187.5f, 8f, 0f, Vector3.one, false);
            }

            ArenaPiece("PR_Trilho_Ponta", 80f, 187.5f, 8f, 180f, Vector3.one, false);
            ArenaPiece("PR_Trilho_Ponta", 176f, 187.5f, 8f, 0f, Vector3.one, false);
            // Bancas (kit: 8 x 3 m, esticadas no comprimento), de frente para o meio do mercado; inteira, saqueada ou
            // queimada pelo mesmo sorteio que dava a altura dos blocos de antes (o resto do sorteio não muda)
            var burnedStalls = new List<Vector3>();
            foreach ((float x, float z, float w, float d) in new[]
                     {
                         (84f, 130f, 8f, 3f), (88f, 168f, 3f, 8f), (88f, 182f, 9f, 3f), (125f, 186f, 3f, 7f), (163f, 150f, 8f, 3f),
                         (168f, 178f, 3f, 9f), (112f, 112f, 9f, 3f), (150f, 192f, 8f, 3f),
                     })
            {
                float roll = Range(1.6f, 2.8f);
                string kind = roll < 2f ? "C" : roll < 2.4f ? "B" : "A";
                float turn = w > d ? (z < 150f ? 180f : 0f) : (x < 126f ? -90f : 90f);
                ArenaPiece("PR_Banca_Mercado_" + kind, x, z, 8f, turn, new Vector3(Mathf.Max(w, d) / 8f, 1f, 1f));
                if (kind == "C")
                    burnedStalls.Add(P(x, z, 8.1f));
            }

            // O cerco no mercado: cratera com corpos na chegada do viaduto, tendas da feira inteiras e destruídas nas naves
            // do salão (mesas e contêineres de células entre elas), barricada perto da escadaria do pódio com armas
            // largadas, autômatos destruídos, cápsula queimada sob o trilho, coluna-lanterna tombada, escombros queimados
            // no terraço, mastros com estandarte queimado, relógio de rua e quadro de avisos; colunas-lanterna nas quinas
            // (as peças rentes ao chão ficam no piso de lajes, 4 cm acima do bloco)
            Dress("PR_Cratera_A", 84f, 149f, 8.04f, 20f);
            Dress("PR_Corpos_Carbonizados", 79f, 142.5f, 8.04f, 10f, false);
            Dress("PR_Corpos_Carbonizados", 90f, 156f, 8.04f, 200f, false);
            foreach ((string model, float x, float z, float turn) in new[]
                     {
                         ("PR_Tenda_Destruida_A", 105f, 137.5f, 180f), ("PR_Tenda_Feira", 125f, 137.5f, 180f),
                         ("PR_Tenda_Destruida_B", 145f, 137.5f, 180f), ("PR_Tenda_Feira", 115f, 152.5f, 0f),
                         ("PR_Tenda_Destruida_A", 135f, 152.5f, 0f),
                     })
                Dress(model, x, z, 8f, turn);
            Dress("PR_Mesa_Bancos", 105f, 152.5f, 8f, 90f);
            Dress("PR_Mesa_Bancos", 145f, 152.5f, 8f, 90f);
            Dress("PR_Conteiner_Celulas", 110f, 141f, 8f, 10f);
            Dress("PR_Conteiner_Celulas", 131f, 133f, 8f, -20f);
            Dress("PR_Barricada_A", 90f, 112f, 8f, 180f);
            Dress("PR_Armas_Largadas", 90f, 117.5f, 8.04f, 15f, false);
            Dress("PR_Sentinela_Destruida", 100f, 120f, 8f, 30f);
            Dress("PR_Sentinela_Destruida", 176f, 150f, 8f, -60f);
            Dress("PR_Capsula_Destruida", 120f, 194f, 8f, 10f);
            Dress("PR_Coluna_Bobinas_Tombada", 95f, 192f, 8f, 90f);
            Dress("PR_Escombros_Queimados_B", 172f, 114f, 11.5f, 0f);
            Dress("PR_Mastro_Queimado", 160f, 197f, 8f, 180f);
            Dress("PR_Mastro_Queimado", 74f, 175f, 8f, -90f);
            Dress("PR_Relogio_Rua", 108f, 195f, 8f, 0f);
            Dress("PR_Quadro_Avisos", 72f, 133f, 8f, -90f);
            Dress("PR_Cristais_Estilhacados", 166f, 194f, 8f, 40f);
            foreach ((float x, float z) in new[] { (76f, 106f), (76f, 194f), (176f, 194f), (146f, 105f) })
                Dress("PR_Coluna_Bobinas", x, z, 8f, 0f);

            // ---------- Bairro dos Relojoeiros: o detalhe da primeira fase (pedido do usuário em 2026-11-11: "só tem uns
            // itens soltos"; kit AR_Relojoeiros) ----------
            // Piso de lajes por cima dos blocos (o pátio e os topos dos terraços, 4 cm acima deles), cortado com a neve dos
            // pisos: mais neve perto das fachadas e dos muros, o meio mais limpo
            var paving = new MeshData();
            void Slab(float x0, float x1, float z0, float z1, float y) =>
                BlockMesh.Box(paving, P((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, y - 0.03f), new Vector3(x1 - x0, 0.14f, z1 - z0), q);
            Slab(-150f, -70f, 172f, 222f, Court);
            Slab(-150f, -112f, 118f, 172f, Court);
            Slab(-182f, -150f, 118f, 222f, 4f);
            Slab(-112f, -70f, 118f, 172f, 8f);
            Pave(paving);
            s_Platforms.Add((P(-126f, 170f, Court), 60f));
            const float Floor = Court + 0.04f;

            // O mecanismo-mestre da guilda (pedestal gótico e esfera armilar com o cristal apagado) no lugar da fonte, sobre
            // o mostrador de 24 horas no chão; o trecho do anel que caiu fica cravado no buraco do mostrador, na direção da
            // quebra (para a escadaria do terraço leste)
            ArenaPiece("PR_Mosaico_Relojoeiros", -116f, 204f, Floor, 0f, Vector3.one, false);
            ArenaPiece("PR_Mecanismo_Mestre", -116f, 204f, Court, 0f, Vector3.one);
            ArenaPiece("PR_Anel_Caido", -112.2f, 197.5f, Floor, -30f, Vector3.one);
            // Muros de arrimo: edículas com estátua nos de 8 m e a fonte de parede congelada no de 4 m; urnas ao pé das
            // escadarias; ralos e manchas de fuligem no chão
            foreach (float z in new[] { 123f, 135f, 164f })
                ArenaPiece("PR_Edicula_Muro", -112f, z, Court, 90f, Vector3.one);
            foreach (float x in new[] { -94f, -86f })
                ArenaPiece("PR_Edicula_Muro", x, 172f, Court, 180f, Vector3.one);
            ArenaPiece("PR_Fonte_Parede", -150f, 204f, Court, -90f, Vector3.one);
            foreach ((float x, float z) in new[] { (-141.2f, 163.6f), (-141.2f, 180.4f), (-112.6f, 192.8f), (-99.4f, 192.8f) })
                ArenaPiece("PR_Urna", x, z, Floor, 0f, Vector3.one);
            foreach ((float x, float z) in new[] { (-120f, 182f), (-125f, 150f), (-96f, 186f) })
                ArenaPiece("PR_Ralo", x, z, Floor, 0f, Vector3.one, false);
            ArenaPiece("PR_Mancha_Fuligem_A", -130.5f, 191.5f, Floor, 40f, Vector3.one, false);
            ArenaPiece("PR_Mancha_Fuligem_B", -118f, 128f, Floor, 0f, Vector3.one, false);
            // Terraço oeste: a oficina a céu aberto da guilda (pérgola de liga e vidro, bancada, células) e o forno de cristal
            ArenaPiece("PR_Pergola_Oficina", -166f, 200f, 4f, -90f, Vector3.one);
            Dress("PR_Bancada_Cristal", -165f, 197f, 4f, -90f);
            Dress("PR_Conteiner_Celulas", -170.5f, 197.5f, 4f, 0f);
            ArenaPiece("PR_Forno_Cristal", -167f, 210.5f, 4f, -90f, Vector3.one);
            // Terraço leste: a última defesa do bairro, o canhão de ressonância partido apontado para o pátio, com
            // barricada, armas, corpos e um autômato
            ArenaPiece("PR_Canhao_Quebrado", -99f, 131f, 8f, 90f, Vector3.one);
            Dress("PR_Barricada_A", -106.5f, 140f, 8f, 90f);
            Dress("PR_Armas_Largadas", -101.5f, 140f, 8f, 100f, false);
            Dress("PR_Corpos_Carbonizados", -96f, 124f, 8f, 30f, false);
            Dress("PR_Sentinela_Destruida", -91f, 137f, 8f, -50f);
            // Embaixo da ponte, entre dois pilares: o abrigo improvisado de quem resistiu
            ArenaPiece("PR_Acampamento_Ponte", -137.4f, 150f, Court, 0f, Vector3.one);

            // Fachadas (por raio, depois de tudo posto): toldos rasgados nas lojas do térreo e lanternas em cabos nas bocas
            // das ruas (as bandeirolas que iam do mecanismo às fachadas saíram: o usuário as tirou, ficam no Mercado Alto)
            Physics.SyncTransforms();
            int awnings = 0, lanterns = 0, smokes = 0;
            bool FlatWall(Vector3 from, Vector3 dir, float reach, out RaycastHit hit)
            {
                if (!Physics.Raycast(from, dir, out hit, reach, ~0, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, -dir) < 0.92f)
                    return false;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float s in new[] { -1.8f, 1.8f })
                    foreach (float h in new[] { 0f, -1.1f })
                        if (!Physics.Raycast(from + side * s + Vector3.up * h, dir, out RaycastHit other, reach + 1f, ~0, QueryTriggerInteraction.Ignore) ||
                            Mathf.Abs(other.distance - hit.distance) > 0.35f)
                            return false;
                return true;
            }

            void Awning(float x, float z, float floor, Vector3 dir)
            {
                if (!FlatWall(P(x, z, floor + 3.6f), dir, 9f, out RaycastHit hit))
                    return;
                if (PlaceTech("PR_Toldo_Rasgado", hit.point, YawOf(hit.normal), arenaRoot, 1f, false))
                    awnings++;
            }

            foreach (float x in new[] { -130f, -112f, -104f, -96f, -76f })
                Awning(x, 216f, Court, toBridge);
            foreach (float x in new[] { -138f, -122f })
                Awning(x, 124f, Court, -toBridge);
            foreach (float x in new[] { -84f, -76f })
                Awning(x, 124f, 8f, -toBridge);
            foreach (float z in new[] { 176f, 206f, 214f })
                Awning(-73.5f, z, Court, across);
            foreach (float z in new[] { 130f, 145f, 185f, 212f })
                Awning(-176f, z, 4f, -across);

            // Cabo de liga de parede a parede (achadas por raio dos dois lados de 'middle'), com 'count' lanternas
            // penduradas; as ímpares acesas (uma luz lilás cada, metade delas falhando)
            void LanternLine(Vector3 middle, Vector3 dir, float reach, int count)
            {
                if (!Physics.Raycast(middle, dir, out RaycastHit a, reach, ~0, QueryTriggerInteraction.Ignore) ||
                    !Physics.Raycast(middle, -dir, out RaycastHit b, reach, ~0, QueryTriggerInteraction.Ignore))
                    return;
                Vector3 pa = a.point - dir * 0.05f, pb = b.point + dir * 0.05f;
                float sag = Vector3.Distance(pa, pb) * 0.06f;
                Wire(pa, pb, sag);
                for (int i = 1; i <= count; i++)
                {
                    float t = i / (count + 1f);
                    Vector3 hook = Vector3.Lerp(pa, pb, t) + Vector3.down * (sag * 4f * t * (1f - t) + 0.03f);
                    bool lit = i % 2 == 1;
                    if (!PlaceTech(lit ? "PR_Lanterna_Cabo" : "PR_Lanterna_Cabo_Apagada", hook, YawOf(dir), arenaRoot, 1f, false))
                        continue;
                    if (lit)
                        CityLamp(arenaRoot, hook + Vector3.down * 0.85f, 1.6f, 9f, lanterns % 2 == 1);
                    lanterns++;
                }
            }

            LanternLine(P(-66f, 190f, Court + 7.5f), toBridge, 12f, 3);       // a rua de entrada, ao chegar no pátio
            LanternLine(P(-85f, 226f, Court + 7f), across, 9f, 2);             // a avenida do norte
            LanternLine(P(-120f, 225f, Court + 6.5f), across, 6f, 1);          // a viela do norte
            LanternLine(P(-145f, 114f, Court + 6.5f), across, 8f, 2);          // a viela larga do sul
            LanternLine(P(-132f, 115f, Court + 6f), across, 6f, 1);
            LanternLine(P(-185f, 170f, 4f + 6.5f), toBridge, 6f, 1);           // a viela do terraço oeste

            // Luzes que ainda funcionam: a boca do forno de cristal e a lâmpada acesa da pérgola
            CityLamp(arenaRoot, P(-165.6f, 210.5f, 4f + 0.9f), 1.2f, 5f, false);
            CityLamp(arenaRoot, P(-166f, 197.1f, 4f + 3.4f), 1.1f, 6f, true);

            // Fumaça: incêndios nos telhados atrás das fileiras (o raio de cima acha o telhado) e o rescaldo no chão
            foreach ((float x, float z, bool embers) in new[] { (-128f, 238f, true), (-197f, 186f, true), (-88f, 104f, false) })
                if (Physics.Raycast(P(x, z, 90f), Vector3.down, out RaycastHit roof, 120f, ~0, QueryTriggerInteraction.Ignore) &&
                    roof.point.y > P(0f, 0f, 6f).y)
                {
                    CitySmoke(arenaRoot, roof.point, 1f, embers);
                    smokes++;
                }

            CitySmoke(arenaRoot, P(-88f, 196f, Court), 0.45f, true);
            CitySmoke(arenaRoot, P(-130f, 192f, Court), 0.35f, false);
            CitySmoke(arenaRoot, P(-102f, 130f, 8f), 0.3f, false);
            smokes += 3;
            Debug.Log($"[Ícaro] Archen Kaedra, Relojoeiros: {awnings} toldos, {lanterns} lanternas em cabos e {smokes} fumaças.");

            // ---------- Mercado Alto: o detalhe da segunda fase (pedido do usuário em 2026-11-11; kit AR_Mercado) ----------
            // Piso de lajes com neve por cima do bloco do mercado e do terraço (4 cm acima deles)
            var marketPaving = new MeshData();
            void MarketSlab(float x0, float x1, float z0, float z1, float y) =>
                BlockMesh.Box(marketPaving, P((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, y - 0.03f), new Vector3(x1 - x0, 0.14f, z1 - z0), q);
            MarketSlab(70f, 150f, 100f, 128f, 8f);
            MarketSlab(70f, 182f, 128f, 200f, 8f);
            MarketSlab(150f, 182f, 100f, 128f, 11.5f);
            Pave(marketPaving);
            s_Platforms.Add((P(126f, 150f, 8f), 56f));
            const float MarketFloor = 8.04f;
            int marketPieces = 0, marketLights = 0, signs = 0;
            awnings = 0;
            lanterns = 0;
            smokes = 0;
            GameObject MarketPiece(string model, float x, float z, float y, float turn, bool collider = true)
            {
                GameObject go = ArenaPiece(model, x, z, y, turn, Vector3.one, collider);
                if (go)
                {
                    arenaPieces--;
                    marketPieces++;
                }

                return go;
            }

            // A balança pública da feira no meio da praça leste, de frente para o salão (o prato de um lado caído, os pesos
            // espalhados), e a casa de câmbio arrombada perto da escadaria do terraço
            MarketPiece("PR_Balanca_Publica", 166f, 160f, 8f, 90f);
            MarketPiece("PR_Quiosque_Cambio", 174f, 138f, 8f, 90f);
            // Guindaste de carga ao lado do trilho, com um contêiner pendurado sobre a linha
            MarketPiece("PR_Guindaste_Carga", 104f, 181f, 8f, 180f);
            // O que sobrou da feira: carrinhos de vendedor (um tombado), varais de tecidos e mercadoria derramada
            MarketPiece("PR_Carrinho_Feira", 94f, 160f, 8f, 180f);
            MarketPiece("PR_Carrinho_Feira_Tombado", 156f, 172f, MarketFloor, 0f);
            MarketPiece("PR_Varal_Tecidos", 160f, 143f, MarketFloor, 0f);
            MarketPiece("PR_Varal_Tecidos", 96f, 104.5f, MarketFloor, 180f);
            MarketPiece("PR_Mercadoria_A", 93f, 176f, MarketFloor, 0f);
            MarketPiece("PR_Mercadoria_B", 84f, 122f, MarketFloor, 90f);
            foreach ((float x, float z) in new[] { (96f, 145f), (140f, 120f), (156f, 160f), (130f, 182f) })
                MarketPiece("PR_Ralo", x, z, MarketFloor, 0f, false);
            MarketPiece("PR_Mancha_Fuligem_A", 114f, 166f, MarketFloor, 20f, false);
            MarketPiece("PR_Mancha_Fuligem_B", 118f, 197f, MarketFloor, 0f, false);

            // Mastros da feira (anel das cordas no alto, flâmula e lanterna no braço, acesa ou apagada): dois na chegada do
            // viaduto, os outros pela praça e um no terraço; devolve o anel das cordas
            Vector3? Mast(float x, float z, float y, float turn, bool lit)
            {
                if (!MarketPiece(lit ? "PR_Mastro_Feira" : "PR_Mastro_Feira_Apagado", x, z, y, turn))
                    return null;
                if (lit)
                {
                    CityLamp(arenaRoot, KitPoint(P(x, z, y), cityYaw + 180f + turn, 1f, new Vector3(0f, -1.05f, 3.9f)), 1.5f, 9f, marketLights % 2 == 1);
                    marketLights++;
                }

                return P(x, z, y + 8.21f);
            }

            Vector3? w1 = Mast(76f, 138f, 8f, -90f, true), w2 = Mast(76f, 161f, 8f, -90f, true), w3 = Mast(80f, 176f, 8f, -90f, false);
            Vector3? e1 = Mast(156f, 134f, 8f, 90f, false), e2 = Mast(158f, 181f, 8f, 0f, true), e3 = Mast(178f, 166f, 8f, 90f, false);
            Vector3? s1 = Mast(118f, 122f, 8f, 180f, false), s2 = Mast(138f, 104.5f, 8f, 180f, false);
            Vector3? t1 = Mast(164f, 122f, 11.5f, 180f, true);

            // Bandeirolas da feira: só entre peças do mercado (mastros, o alto da balança, o remate do quiosque, os pináculos
            // das empenas e dos contrafortes do salão e as colunas-lanterna), nunca nas fachadas; cada ponta sai da beira da
            // peça, e a linha que bater em algo no caminho (a corda cedendo e as bandeirolas embaixo) fica de fora
            Physics.SyncTransforms();
            int flagLines = 0, flagsBlocked = 0;
            Vector3 balance = P(166f, 160f, 8f + 11.8f), kiosk = P(174f, 138f, 8f + 7.25f);
            Vector3 Gable(float x, float z) => P(x, z, 8f + 11.5f);          // pináculo da empena (haste de 1 m)
            Vector3 Buttress(float x, float z) => P(x, z, 8f + 8.9f);        // pináculo do contraforte (haste de 0,7 m)
            Vector3 Column(float x, float z) => P(x, z, 8f + 6.45f);         // cornija da coluna-lanterna
            const float MastR = 0.24f, GableR = 0.52f, ButtressR = 0.37f, ColumnR = 0.6f;

            void Bunting(Vector3? from, Vector3? to, float fromRadius, float toRadius)
            {
                if (from == null || to == null)
                    return;
                Vector3 a = from.Value, b = to.Value, flat = b - a;
                flat.y = 0f;
                flat.Normalize();
                a += flat * fromRadius;
                b -= flat * toRadius;
                float length = Vector3.Distance(a, b), sag = length * 0.05f + 0.4f;
                Vector3 d = (b - a) / length, low = (a + b) * 0.5f + Vector3.down * sag;
                if (Physics.Linecast(a + d * 0.4f, low, ~0, QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(low, b - d * 0.4f, ~0, QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(low, low + Vector3.down * 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    flagsBlocked++;
                    return;
                }

                s_CityFlagLines.Add((a, b, sag));
                flagLines++;
            }

            // por cima da chegada do viaduto e dos dois mastros dela até as empenas do salão
            Bunting(w1, w2, MastR, MastR);
            Bunting(w1, Gable(100f, 130f), MastR, GableR);
            Bunting(w1, Gable(100f, 145f), MastR, GableR);
            Bunting(w2, Gable(100f, 160f), MastR, GableR);
            Bunting(w2, Gable(100f, 175f), MastR, GableR);
            Bunting(w3, Gable(100f, 175f), MastR, GableR);
            Bunting(w3, Column(76f, 194f), MastR, ColumnR);
            // do alto da balança para a empena leste do salão e os mastros em volta
            Bunting(balance, Gable(150f, 160f), 0.34f, GableR);
            Bunting(balance, Gable(150f, 145f), 0.34f, GableR);
            Bunting(balance, e1, 0.34f, MastR);
            Bunting(balance, e2, 0.34f, MastR);
            Bunting(balance, e3, 0.34f, MastR);
            Bunting(e1, kiosk, MastR, MastR);
            Bunting(e3, kiosk, MastR, MastR);
            Bunting(e2, Gable(150f, 175f), MastR, GableR);
            Bunting(e2, Column(176f, 194f), MastR, ColumnR);
            // na frente do salão, do lado da escadaria do terraço
            Bunting(s1, Buttress(110f, 128.75f), MastR, ButtressR);
            Bunting(s1, Buttress(130f, 128.75f), MastR, ButtressR);
            Bunting(s1, Gable(100f, 130f), MastR, GableR);
            Bunting(s1, s2, MastR, MastR);
            Bunting(s2, Column(146f, 105f), MastR, ColumnR);
            // do mastro do terraço até a empena, a coluna-lanterna do pé da escadaria e o mastro da praça
            Bunting(t1, Gable(150f, 130f), MastR, GableR);
            Bunting(t1, Column(146f, 105f), MastR, ColumnR);
            Bunting(t1, e1, MastR, MastR);

            // Fachadas (por raio): toldos rasgados e letreiros de loja nos braços de ferro, longe das torres das fileiras,
            // das bocas das ruas e do que fica encostado nelas
            void ShopSign(float x, float z, float floor, Vector3 dir, string model)
            {
                if (!FlatWall(P(x, z, floor + 4.3f), dir, 9f, out RaycastHit hit))
                    return;
                if (PlaceTech(model, hit.point, YawOf(hit.normal), arenaRoot, 1f, false))
                    signs++;
            }

            foreach (float x in new[] { 86f, 112f, 138f, 170f })
                Awning(x, 194f, 8f, toBridge);
            ShopSign(92f, 194f, 8f, toBridge, "PR_Letreiro_Loja_A");
            ShopSign(128f, 194f, 8f, toBridge, "PR_Letreiro_Loja_B");
            ShopSign(146f, 194f, 8f, toBridge, "PR_Letreiro_Loja_A");
            foreach (float x in new[] { 82f, 122f })
                Awning(x, 106f, 8f, -toBridge);
            ShopSign(112f, 106f, 8f, -toBridge, "PR_Letreiro_Loja_B");
            ShopSign(136f, 106f, 8f, -toBridge, "PR_Letreiro_Loja_A");
            Awning(156f, 106f, 11.5f, -toBridge);
            ShopSign(174f, 106f, 11.5f, -toBridge, "PR_Letreiro_Loja_B");
            Awning(176f, 148f, 8f, across);
            ShopSign(176f, 158f, 8f, across, "PR_Letreiro_Loja_A");
            Awning(176f, 112f, 11.5f, across);
            ShopSign(176f, 120f, 11.5f, across, "PR_Letreiro_Loja_B");
            foreach (float z in new[] { 128f, 185f })
                Awning(75f, z, 8f, -across);
            ShopSign(75f, 167f, 8f, -across, "PR_Letreiro_Loja_B");
            ShopSign(75f, 188f, 8f, -across, "PR_Letreiro_Loja_A");

            // Lanternas em cabos nas bocas das ruas que saem do mercado
            LanternLine(P(188f, 176f, 8f + 7f), toBridge, 8f, 2);              // a rua alta larga do leste
            LanternLine(P(140f, 206f, 8f + 7f), across, 9f, 2);                // a descida larga do norte
            LanternLine(P(85f, 205f, 8f + 6f), across, 4f, 1);                 // a viela do norte
            LanternLine(P(165f, 96f, 11.5f + 6f), across, 4f, 1);              // a viela do terraço

            // Fumaça: o rescaldo debaixo do buraco do telhado do salão, as bancas queimadas, a cápsula sob o trilho e
            // incêndios nos telhados atrás das fileiras
            CitySmoke(arenaRoot, P(120f, 167.5f, 8.1f), 0.5f, true);
            CitySmoke(arenaRoot, P(120f, 194f, 8.1f), 0.3f, false);
            smokes += 2;
            foreach (Vector3 stall in burnedStalls)
            {
                CitySmoke(arenaRoot, stall, 0.28f, false);
                smokes++;
            }

            foreach ((float x, float z, bool embers) in new[] { (118f, 214f, true), (196f, 150f, false), (125f, 88f, true) })
                if (Physics.Raycast(P(x, z, 90f), Vector3.down, out RaycastHit roof, 120f, ~0, QueryTriggerInteraction.Ignore) &&
                    roof.point.y > P(0f, 0f, 12f).y)
                {
                    CitySmoke(arenaRoot, roof.point, 1f, embers);
                    smokes++;
                }

            Debug.Log($"[Ícaro] Archen Kaedra, Mercado Alto: {marketPieces} peças do kit AR_Mercado e do cenário, {flagLines} linhas de bandeirolas " +
                      $"({flagsBlocked} batiam em algo e ficaram de fora), {signs} letreiros, {awnings} toldos, {lanterns} lanternas em cabos, " +
                      $"{marketLights} mastros acesos e {smokes} fumaças.");

            Debug.Log($"[Ícaro] Archen Kaedra: {arenaPieces} peças do kit das arenas (AR_Arenas) e {dressPieces} de cenário do cerco em ArchenKaedra_Arenas.");

            // A saída do mercado só sobe: escadaria monumental de três lances, do mercado (+8 m) ao meio da entrada do pódio
            // (o patamar de cima entra 5 cm abaixo do piso do pódio, para os dois não ficarem no mesmo plano)
            float floorY = P(0f, 0f, -1f).y;
            float stairTop = k_CityPodium + 0.05f, riseEach = (stairTop - 8f) / 3f;

            void Rail(float x0, float y0, float x1, float y1, float z)
            {
                Vector3 from = P(x0, z, y0), to = P(x1, z, y1), along = to - from;
                Quaternion slope = Quaternion.LookRotation(along);
                float length = along.magnitude;
                Vector3 middle = (from + to) * 0.5f;
                BlockMesh.ChamferBox(m.Tones[0], middle + Vector3.up * 0.13f, new Vector3(0.44f, 0.3f, length), slope, 0.02f);
                BlockMesh.ChamferBox(m.Tones[0], middle + Vector3.up * 1.12f, new Vector3(0.4f, 0.15f, length), slope, 0.02f);
                int count = Mathf.RoundToInt(length / 0.62f);
                for (int i = 0; i < count; i++)
                    BlockMesh.ChamferBox(m.Tones[0], Vector3.Lerp(from, to, (i + 0.5f) / count) + Vector3.up * 0.64f, new Vector3(0.17f, 0.74f, 0.17f), q, 0.01f);
            }

            void RailPost(float x, float z, float y)
            {
                BlockMesh.ChamferBox(m.Tones[0], P(x, z, y + 0.3f), new Vector3(0.8f, 1.9f, 0.8f), q, 0.04f);
                BlockMesh.ChamferBox(m.Tones[0], P(x, z, y + 1.3f), new Vector3(1.1f, 0.14f, 1.1f), q, 0.03f);
            }

            float[] stairX = { 70.2f, 53.2f, 48.2f, 31.2f, 26.2f, 9.2f };
            for (int f = 0; f < 3; f++)
            {
                float y0 = 8f + f * riseEach, y1 = y0 + riseEach, x0 = stairX[f * 2], x1 = stairX[f * 2 + 1];
                Steps(P(x0, 117f, y0), P(x1, 117f, y1), 14f, floorY);
                foreach (float z in new[] { 110.25f, 123.75f })
                {
                    Rail(x0 - 0.42f, y0 + (0.42f / (x0 - x1)) * riseEach, x1 + 0.42f, y1 - (0.42f / (x0 - x1)) * riseEach, z);
                    RailPost(x0, z, y0);
                    RailPost(x1, z, y1);
                }

                if (f == 2)
                    continue;
                float nextX = stairX[f * 2 + 2];
                BlockMesh.ChamferBox(m.Tones[0], P((x1 + nextX) * 0.5f, 117f, (y1 - 1f) * 0.5f), new Vector3(x1 - nextX, y1 + 1f, 14f), q, 0.08f);
                foreach (float z in new[] { 110.25f, 123.75f })
                {
                    Rail(x1 - 0.42f, y1, nextX + 0.42f, y1, z);
                    RailPost(nextX, z, y1);
                    Deco("PR_Coluna_Bobinas", (x1 + nextX) * 0.5f, z < 117f ? 111.1f : 122.9f, y1, z < 117f ? toBridge : -toBridge, 0.8f);
                }
            }

            BlockMesh.ChamferBox(m.Tones[0], P(0.1f, 113f, (stairTop - 1f) * 0.5f), new Vector3(18.2f, stairTop + 1f, 22f), q, 0.08f);
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

            // A muralha (kit AR_Muralha): a face de dentro fica uns 6,5 m para dentro da beira (o polígono de 40 lados do
            // SkyIsland) e as torres, 1 m para dentro de cada quina, com 7 a 9 m de raio. Os lotes vão até 13 m da beira (sobra
            // uma rua de ronda de ~6 m) e ficam a 13 m do meio das torres
            const int RimBins = 1440;
            var rimAt = new float[RimBins];
            var wallTowers = new Vector2[40];
            {
                float rimRadius = SkyIslandRadius(k_CityRadius, 0f);
                Vector2 Vertex(int j)
                {
                    j %= 40;
                    float a = j * Mathf.PI * 2f / 40f;
                    float n = 1f + (Mathf.PerlinNoise(4101 * 0.37f + j * 0.5f, 0f) - 0.5f) * 0.16f;
                    return new Vector2(Mathf.Cos(a) * rimRadius * n, Mathf.Sin(a) * rimRadius * n);
                }

                for (int j = 0; j < 40; j++)
                    wallTowers[j] = Vertex(j) - Vertex(j).normalized;
                for (int bin = 0; bin < RimBins; bin++)
                {
                    float a = bin * Mathf.PI * 2f / RimBins;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    rimAt[bin] = rimRadius;
                    for (int j = 0; j < 40; j++)
                    {
                        Vector2 va = Vertex(j), e = Vertex(j + 1) - va;
                        float den = dir.x * e.y - dir.y * e.x;
                        if (Mathf.Abs(den) < 1e-6f)
                            continue;
                        float s = (va.x * e.y - va.y * e.x) / den, u = (va.x * dir.y - va.y * dir.x) / den;
                        if (s > 0f && u >= -1e-4f && u <= 1f + 1e-4f)
                        {
                            rimAt[bin] = s;
                            break;
                        }
                    }
                }
            }

            bool NearWall(float x, float z)
            {
                Vector3 offset = across * x + toBridge * z;
                var p = new Vector2(offset.x, offset.z);
                float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;
                if (p.magnitude > rimAt[Mathf.Min((int)(angle * RimBins / 360f), RimBins - 1)] - 13f)
                    return true;
                foreach (Vector2 t in wallTowers)
                    if ((p - t).sqrMagnitude < 13f * 13f)
                        return true;
                return false;
            }

            bool Buildable(int i, int j)
            {
                if (cells[i, j] != 0)
                    return false;
                float x = i - Half + 0.5f, z = j - Half + 0.5f;
                float radius = Mathf.Sqrt(x * x + z * z);
                if (NearWall(x, z))
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

            // 'narrowLong': lote estreito, com a frente numa das pontas do lado comprido (0 = não é estreito, 1 = comprido em
            // X, 2 = comprido em Z)
            void PlaceLot(float x0, float z0, float w, float d, float lotLevel, float top, int narrowLong = 0)
            {
                float cx = x0 + w * 0.5f, cz = z0 + d * 0.5f;
                float level = Mathf.Max(lotLevel, 0f);

                // A frente fica para o beco mais perto; sem beco, para o meio da cidade
                bool Lane(float px, float pz)
                {
                    int a = Mathf.FloorToInt(px) + Half, b = Mathf.FloorToInt(pz) + Half;
                    return a >= 0 && b >= 0 && a <= Half * 2 && b <= Half * 2 && laneLevel[a, b] >= 0f;
                }

                Vector2 face;
                if (narrowLong == 1)
                    face = Lane(x0 + w + 2f, cz) ? Vector2.right : Lane(x0 - 2f, cz) ? Vector2.left : new Vector2(-Mathf.Sign(cx), 0f);
                else if (narrowLong == 2)
                    face = Lane(cx, z0 + d + 2f) ? Vector2.up : Lane(cx, z0 - 2f) ? Vector2.down : new Vector2(0f, -Mathf.Sign(cz));
                else if (Lane(cx, z0 + d + 2f)) face = Vector2.up;
                else if (Lane(cx, z0 - 2f)) face = Vector2.down;
                else if (Lane(x0 + w + 2f, cz)) face = Vector2.right;
                else if (Lane(x0 - 2f, cz)) face = Vector2.left;
                else face = Mathf.Abs(cx) > Mathf.Abs(cz) ? new Vector2(-Mathf.Sign(cx), 0f) : new Vector2(0f, -Mathf.Sign(cz));

                bool sideways = Mathf.Abs(face.x) > 0.5f;
                float wide = sideways ? d : w, deep = sideways ? w : d;
                int pick;
                if (narrowLong != 0)
                    pick = Range(0f, 1f) < 0.55f ? 6 : 7;
                else if (wide / deep > 1.35f)
                    pick = 5;
                else
                {
                    float roll = Range(0f, 1f);
                    pick = roll < 0.32f ? 0 : roll < 0.52f ? 1 : roll < 0.7f ? 2 : roll < 0.82f && wide <= 14f && deep <= 14f ? 3 : roll < 0.95f ? 0 : 4;
                }

                var lot = lotModels[pick];
                if (level > 0.5f)
                    Block(x0 + 0.04f, x0 + w - 0.04f, z0 + 0.04f, z0 + d - 0.04f, -1f, level - 0.01f);
                float height = Mathf.Clamp((top - lotLevel * (lotLevel > 0f ? 1f : 0f)) / lot.H, 0.8f, 1.6f);
                Vector3 front = across * face.x + toBridge * face.y;
                AddLotMesh(lot.Model, P(cx, cz, level), YawOf(front), new Vector3(wide / lot.W, height, deep / lot.D));
                if (lotLevel >= 0f)
                    Barrier(x0, x0 + w, z0, z0 + d, level, level + 48f);
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
                    bool hole = x0 > -66f && x0 < 55f && z0 > 105f && z0 < 143f;
                    float top = lotLevel >= 0f ? lotLevel + Range(11f, 22f) : Range(14f, 34f);
                    if (hole)
                        top = Range(26f, 44f);      // o buraco atrás da rampa: prédios altos
                    else if (lotLevel < 0f && new Vector2(x0 + w * 0.5f, z0 + d * 0.5f).magnitude > k_CityRadius * 0.84f)
                        top = Range(12f, 20f);      // junto da muralha: mais baixos que o adarve
                    if (Mathf.Min(w, d) >= 7)
                    {
                        PlaceLot(x0, z0, w, d, lotLevel, top);
                        lots++;
                        continue;
                    }

                    if (Mathf.Min(w, d) < 3)
                        continue;       // fresta: fica como passagem entre os prédios

                    // Lote estreito: prédios estreitos em fila ao longo do lado comprido, em pedaços de até 12 m
                    bool longX = w >= d;
                    int length = longX ? w : d, pieces = Mathf.CeilToInt(length / 12f);
                    float piece = (float)length / pieces;
                    for (int k = 0; k < pieces; k++)
                    {
                        if (longX)
                            PlaceLot(x0 + piece * k, z0, piece, d, lotLevel, top, 1);
                        else
                            PlaceLot(x0, z0 + piece * k, w, piece, lotLevel, top, 2);
                        lots++;
                    }
                }

            Debug.Log($"[Ícaro] Archen Kaedra: {lots} lotes e {streetLots} prédios leves nas fileiras ({lotObjects} peças em ArchenKaedra_Lotes), {mouths.Count} bocas de beco.");

            // Ponto de chegada para o teste
            var arrival = new GameObject("Chegada_ArchenKaedra");
            arrival.transform.SetParent(s_World, false);
            arrival.transform.SetPositionAndRotation(P(0f, zStart - 5f, 2f), Quaternion.LookRotation(-toBridge));
        }
    }
}
