using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // O cerco da Ponte do Reino (pedido do usuário em 2026-11-09; os invasores ainda não foram decididos, então nada mostra
    // quem atacou). O ataque veio da cidade: o estrago cresce da conexão com a Vila da Guarda até a ilha.
    //   bastião   detalhado como uma arena (não é sala): banquetas de pedra de 0,7 m atrás do parapeito nas seis faces
    //             fora da ponte, com três escadas de cada lado (e uma barreira invisível acima do parapeito, para ninguém
    //             cair pelas ameias); ameias caídas nas faces do lado da cidade; a linha de defesa virada para a cidade
    //             (barricadas na estrada e na ala direita, armas largadas atrás), cápsulas tombadas, um guardião caído,
    //             uma cratera, autômatos destruídos, cristais estilhaçados, uma coluna-lanterna tombada e escombros
    //             queimados. O estrado do Portal do Reino e as banquetas são os níveis.
    //   ponte     do lado da Vila da Guarda pouco estrago; do bastião à rotatória mais; da rotatória à cidade muito:
    //             crateras no meio do tabuleiro (o trilho de levitação quebra nelas), balaústres arrancados (com os
    //             pedaços caídos no tabuleiro e uma barreira invisível no vão), cápsulas destruídas e tombadas,
    //             barricadas em chicana com armas largadas, autômatos, cristais, escombros queimados e mastros com
    //             estandartes queimados nos refúgios.
    //   cidade    a barbacã (AR_Portal_Barbaca, kit da Ponte do Reino) na beira da ilha, onde a ponte entra: portão com as
    //             duas torres de vigia, cortinas de muralha e o pátio fechado pelo portão de dentro, arrombado; piso de
    //             lajes do fim do tabuleiro até 10 m depois do portão de dentro; dentro do pátio, o pior do estrago.
    // Sorteio próprio (o resto do mapa não muda). A beira do topo da ilha da cidade é irregular (SkyIsland): a barbacã fica
    // onde o eixo da ponte cruza essa beira (CityRimT, refeito com o mesmo contorno), com alicerces até 40 m abaixo.
    public static partial class PicosBuilder
    {
        static float s_ReinoGateT;          // frente da barbacã, em t
        static readonly List<float> s_ReinoCraters = new List<float>();
        static readonly List<(float A, float B, float Side)> s_ReinoTorn = new List<(float, float, float)>();
        static readonly List<(Vector3 At, float Radius)> s_SiegeReserved = new List<(Vector3, float)>();
        static int s_SiegePlaced, s_SiegeSkipped;

        const float k_BarbicanDepth = 47.5f;        // da frente da barbacã ao fundo do portão de dentro
        const float k_BarbicanCourt = 13f;          // meia largura do pátio (face de dentro das muralhas)

        static float ReinoCityEdgeT => k_ReinoCityT + k_CityRadius;

        // Onde o eixo da ponte cruza a beira do topo da ilha da cidade (o mesmo contorno do SkyIsland da cidade: 40 lados,
        // semente 4101, ruído de ±8% no raio)
        static float CityRimT()
        {
            const int sides = 40;
            float r = SkyIslandRadius(k_CityRadius, 0f);
            var dir = new Vector2(ReinoDir.x, ReinoDir.z);

            Vector2 Vertex(int j)
            {
                j %= sides;
                float a = j * Mathf.PI * 2f / sides;
                float n = 1f + (Mathf.PerlinNoise(4101 * 0.37f + j * 0.5f, 0f) - 0.5f) * 0.16f;
                return new Vector2(Mathf.Cos(a) * r * n, Mathf.Sin(a) * r * n);
            }

            for (int j = 0; j < sides; j++)
            {
                Vector2 a = Vertex(j), e = Vertex(j + 1) - a;
                float den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-6f)
                    continue;
                float s = (a.x * e.y - a.y * e.x) / den, u = (a.x * dir.y - a.y * dir.x) / den;
                if (s > 0f && u >= -1e-4f && u <= 1f + 1e-4f)
                    return k_ReinoCityT + s;
            }

            return ReinoCityEdgeT;
        }

        // Pilares de dentro de um trecho reto (o mesmo cálculo do KingStraight)
        static List<float> ReinoPiers(float from, float to)
        {
            int spans = Mathf.Max(1, Mathf.RoundToInt((to - from) / k_ReinoSpan));
            var piers = new List<float>();
            for (int k = 1; k < spans; k++)
                piers.Add(from + (to - from) * k / spans);
            return piers;
        }

        // Trechos onde se anda, do mais estragado (cidade) ao menos: (de, até, pilares de dentro, nível)
        static (float A, float B, List<float> Piers, int Level)[] ReinoStretches()
        {
            float rotT = ReinoRotundaT, rotR = k_ReinoRotundaRadius, bastT = k_ReinoBastionT, inr = ReinoBastionInradius;
            var far = ReinoPiers(bastT + inr, k_ReinoStraightEnd);
            far.RemoveAll(t => t > k_ReinoCollapseFrom - 3f || Mathf.Abs(t - k_ReinoJoinT) <= 14f);
            return new[]
            {
                (s_ReinoGateT + 4f, rotT - rotR - 1f, ReinoPiers(ReinoCityEdgeT, rotT - (rotR - 0.5f)), 3),
                (rotT + rotR + 1f, bastT - inr - 1f, ReinoPiers(rotT + (rotR - 0.5f), bastT - inr), 2),
                (bastT + inr + 1f, k_ReinoCollapseFrom - 8f, far, 1),
            };
        }

        // Antes da ponte: a barbacã, as crateras (o trilho quebra nelas) e os balaústres arrancados (os trechos abertos
        // vão para o KingStraight)
        static void PlanReinoSiege()
        {
            s_ReinoGateT = CityRimT() + 1f;
            s_ReinoCraters.Clear();
            s_ReinoTorn.Clear();
            var rnd = new System.Random(4601);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float rotT = ReinoRotundaT, bastT = k_ReinoBastionT;
            var capsules = new List<float>(ReinoCarTs());

            foreach ((float a, float b, List<float> piers, int level) in ReinoStretches())
            {
                int craters = level - 1;
                for (int tries = 0; tries < 60 && craters > 0; tries++)
                {
                    float t = Range(a + 10f, b - 10f);
                    bool ok = true;
                    foreach (float c in capsules)
                        ok &= Mathf.Abs(t - c) > 15f;
                    foreach (float c in s_ReinoCraters)
                        ok &= Mathf.Abs(t - c) > 30f;
                    if (!ok)
                        continue;
                    s_ReinoCraters.Add(t);
                    craters--;
                }

                // Vãos entre os pilares (os refúgios ficam abertos em volta deles), longe das pontas do trecho
                var bounds = new List<float> { a - 30f };
                bounds.AddRange(piers);
                bounds.Add(b + 30f);
                int torn = level;
                for (int tries = 0; tries < 60 && torn > 0; tries++)
                {
                    int k = rnd.Next(bounds.Count - 1);
                    float length = Range(4f, 7.5f);
                    float lo = Mathf.Max(bounds[k] + 6f, a + 4f), hi = Mathf.Min(bounds[k + 1] - 6f, b - 4f) - length;
                    if (hi <= lo)
                        continue;
                    float t0 = Range(lo, hi), side = rnd.Next(2) == 0 ? 1f : -1f;
                    bool ok = true;
                    foreach ((float x0, float x1, float s) in s_ReinoTorn)
                        ok &= s != side || t0 > x1 + 12f || t0 + length < x0 - 12f;
                    if (!ok)
                        continue;
                    s_ReinoTorn.Add((t0, t0 + length, side));
                    torn--;
                }
            }
        }

        // Trechos abertos de um lado ('side' 1 = à direita, para o lado de fora) entre 'from' e 'to'
        static List<(float, float)> ReinoTornRuns(float side, float from, float to)
        {
            var runs = new List<(float, float)>();
            foreach ((float a, float b, float s) in s_ReinoTorn)
                if (s == side && a > from && b < to)
                    runs.Add((a, b));
            return runs;
        }

        // Trilho reto de 'a' a 'b' (em t) quebrado nas crateras
        static void TrackAroundCraters(float a, float b, End endA, End endB)
        {
            var cuts = new List<float>();
            foreach (float c in s_ReinoCraters)
                if (c > Mathf.Min(a, b) + 9f && c < Mathf.Max(a, b) - 9f)
                    cuts.Add(c);
            cuts.Sort();
            if (b < a)
                cuts.Reverse();
            float sign = Mathf.Sign(b - a), from = a;
            End e0 = endA;
            foreach (float c in cuts)
            {
                Track(ReinoAt(from), ReinoAt(c - sign * 8f), e0, End.Broken);
                from = c + sign * 8f;
                e0 = End.Broken;
            }

            Track(ReinoAt(from), ReinoAt(b), e0, endB);
        }

        // ---------- colocação ----------

        static Vector3 ReinoPoint(float t, float o) => ReinoAt(t) + ReinoRight * o;

        static (float T, float O) ReinoCoords(Vector3 w)
        {
            Vector3 rel = w - ReinoAt(0f);
            return (Vector3.Dot(rel, ReinoDir), Vector3.Dot(rel, ReinoRight));
        }

        // Peça do kit num ponto do piso, se cada ponto do contorno passar em 'fits', ficar fora das áreas reservadas e não
        // encostar em nenhum colisor já posto
        static GameObject SiegePiece(string model, Vector3 p, float yaw, Transform root, System.Func<Vector3, bool> fits,
            bool collider = true)
        {
            if (k_AttackFoot.TryGetValue(model, out (Vector2 Min, Vector2 Max, float Height) foot))
            {
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                foreach (Vector3 corner in Corners(foot.Min, foot.Max))
                {
                    Vector3 w = p + q * corner;
                    if (!fits(w))
                        return null;
                    foreach ((Vector3 at, float radius) in s_SiegeReserved)
                        if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(at.x, at.z)) < radius)
                            return null;
                }

                Vector3 center = p + q * new Vector3((foot.Min.x + foot.Max.x) * 0.5f, 0f, (foot.Min.y + foot.Max.y) * 0.5f);
                var half = new Vector3((foot.Max.x - foot.Min.x) * 0.5f, Mathf.Max(foot.Height, 0.5f) * 0.5f, (foot.Max.y - foot.Min.y) * 0.5f);
                if (Physics.CheckBox(center + Vector3.up * (0.15f + half.y), half, q, ~0, QueryTriggerInteraction.Ignore))
                    return null;
            }

            GameObject go = PlaceTech(model, p, yaw, root, 1f, collider);
            Physics.SyncTransforms();
            if (go)
                s_SiegePlaced++;
            return go;
        }

        // Tenta 'tries' pontos sorteados por 'pick' (ponto e giro) até a peça caber; conta a recusa no fim
        static GameObject SiegeScatter(System.Random rnd, string model, Transform root, System.Func<System.Random, (Vector3, float)> pick,
            System.Func<Vector3, bool> fits, int tries = 30, bool collider = true)
        {
            for (int k = 0; k < tries; k++)
            {
                (Vector3 p, float yaw) = pick(rnd);
                GameObject go = SiegePiece(model, p, yaw, root, fits, collider);
                if (go)
                    return go;
            }

            s_SiegeSkipped++;
            return null;
        }

        // Caixa livre de colisores em cima do piso (para as peças sem contorno na tabela)
        static bool SiegeBoxFree(Vector3 center, Vector3 size, float yaw, System.Func<Vector3, bool> fits)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector2 h = new Vector2(size.x, size.z) * 0.5f;
            foreach (Vector3 corner in Corners(-h, h))
            {
                Vector3 w = center + q * corner;
                if (!fits(w))
                    return false;
                foreach ((Vector3 at, float radius) in s_SiegeReserved)
                    if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(at.x, at.z)) < radius)
                        return false;
            }

            return !Physics.CheckBox(center + Vector3.up * (0.15f + size.y * 0.5f), size * 0.5f, q, ~0, QueryTriggerInteraction.Ignore);
        }

        // Assenta a peça: o vértice mais baixo fica 'embed' abaixo do piso (nada flutuando)
        static void SettleKit(GameObject go, float floorY, float embed = 0.06f)
        {
            float low = float.MaxValue;
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (!mesh)
                    continue;
                Matrix4x4 m = filter.transform.localToWorldMatrix;
                foreach (Vector3 v in mesh.vertices)
                    low = Mathf.Min(low, m.MultiplyPoint3x4(v).y);
            }

            if (low < float.MaxValue)
                go.transform.position += Vector3.up * (floorY - embed - low);
            Physics.SyncTransforms();
        }

        // Guardião (estátua com o pedestal) tombado: deitado de 'foot' para a frente de 'yaw', assentado no piso
        static bool FallenGuardian(Transform root, Vector3 foot, float yaw, System.Func<Vector3, bool> fits)
        {
            Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (!SiegeBoxFree(foot + f * 3.9f, new Vector3(3.6f, 2.8f, 8.4f), yaw, fits))
                return false;
            GameObject go = PicosGothicKit.Place(s_Materials, "PR_Guardiao", foot + Vector3.up * 1.5f, yaw, root, true, ReinoFolder);
            if (!go)
                return false;
            go.transform.rotation = Quaternion.AngleAxis(84f, go.transform.right) * Quaternion.AngleAxis(9f, go.transform.forward) *
                                    go.transform.rotation;
            SettleKit(go, foot.y);
            s_SiegePlaced++;
            return true;
        }

        // Cápsula tombada (Capsized) se a caixa dela couber
        static bool SiegeCapsized(Transform root, Vector3 floor, float yaw, float roll, System.Func<Vector3, bool> fits)
        {
            if (!SiegeBoxFree(floor, new Vector3(3.6f, 3.4f, 11.8f), yaw, fits))
                return false;
            Capsized(root, floor, yaw, roll);
            Physics.SyncTransforms();
            s_SiegePlaced++;
            return true;
        }

        static System.Func<Vector3, bool> OnReinoDeck(float from, float to, float track) => w =>
        {
            (float t, float o) = ReinoCoords(w);
            return t > from && t < to && Mathf.Abs(o) < k_ReinoWidth * 0.5f - 0.8f && Mathf.Abs(o) > track;
        };

        // ---------- vagões tombados ----------

        static readonly List<(GameObject Train, Vector3 Floor, float Yaw)> s_CapsizedTrains = new List<(GameObject, Vector3, float)>();

        // Assenta pelo corpo: a altura em que estão 3% dos vértices (o costado), 6 cm dentro do piso. Pelo vértice mais baixo,
        // as saliências do vagão (1,78 m contra 1,54 m do costado) o deixavam apoiado nelas, com o casco no ar
        static void BodySettle(GameObject go, float floorY)
        {
            var heights = new List<float>();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh)
                {
                    Matrix4x4 m = filter.transform.localToWorldMatrix;
                    foreach (Vector3 v in filter.sharedMesh.vertices)
                        heights.Add(m.MultiplyPoint3x4(v).y);
                }

            if (heights.Count == 0)
                return;
            heights.Sort();
            go.transform.position += Vector3.up * (floorY - 0.06f - heights[heights.Count * 3 / 100]);
            Physics.SyncTransforms();
        }

        // Piso livre em volta de um vagão: o tabuleiro (fora do trilho, dos vãos caídos e do desabamento) ou o bastião
        static bool WreckFloor(Vector3 w)
        {
            (float t, float o) = ReinoCoords(w);
            if (Mathf.Abs(o) < 1.3f)
                return false;
            Vector3 fromBastion = w - ReinoBastion;
            fromBastion.y = 0f;
            if (fromBastion.magnitude < ReinoBastionInradius - 5f)
                return true;
            if (Mathf.Abs(o) > k_ReinoWidth * 0.5f - 0.8f || t < s_ReinoGateT || t > k_ReinoStraightEnd - 5f ||
                (t > k_ReinoCollapseFrom - 1.5f && t < k_ReinoCollapseTo + 1.5f))
                return false;
            foreach ((float a, float b) in k_ReinoFarGaps)
                if (t > a - 1.5f && t < b + 1.5f)
                    return false;
            return true;
        }

        // Peça encostada no vagão: confere o piso e os outros colisores, sem contar o próprio vagão
        static bool WreckPiece(string model, Vector3 p, float yaw, Transform root, GameObject train)
        {
            (Vector2 min, Vector2 max, float height) = k_AttackFoot[model];
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            foreach (Vector3 corner in Corners(min, max))
            {
                Vector3 w = p + q * corner;
                if (!WreckFloor(w))
                    return false;
                foreach ((Vector3 at, float radius) in s_SiegeReserved)
                    if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(at.x, at.z)) < radius)
                        return false;
            }

            Vector3 center = p + q * new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
            var half = new Vector3((max.x - min.x) * 0.5f, Mathf.Max(height, 0.5f) * 0.5f, (max.y - min.y) * 0.5f);
            foreach (Collider hit in Physics.OverlapBox(center + Vector3.up * (0.15f + half.y), half, q, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(train.transform))
                    return false;
            if (!PlaceTech(model, p, yaw, root))
                return false;
            Physics.SyncTransforms();
            s_SiegePlaced++;
            return true;
        }

        // Em volta de cada vagão tombado: um monte de escombros em cada ponta, um comprido no costado e três robôs quebrados
        static void BuildCapsizedWreckage(Transform root)
        {
            for (int i = 0; i < s_CapsizedTrains.Count; i++)
            {
                (GameObject train, Vector3 floor, float yaw) = s_CapsizedTrains[i];
                if (!train)
                    continue;
                var rnd = new System.Random(4720 + i);
                float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 L(float x, float z) => floor + q * new Vector3(x, 0f, z);
                float Side() => rnd.Next(2) == 0 ? -1f : 1f;

                void Place(string model, System.Func<(Vector3, float)> pick)
                {
                    for (int k = 0; k < 40; k++)
                    {
                        (Vector3 p, float y) = pick();
                        if (WreckPiece(model, p, y, root, train))
                            return;
                    }

                    s_SiegeSkipped++;
                }

                Place("PR_Escombros_Queimados_A", () => (L(Range(-1.5f, 1.5f), Range(8f, 10f)), yaw + Range(-20f, 20f)));
                Place("PR_Escombros_Queimados_A", () => (L(Range(-1.5f, 1.5f), -Range(8f, 10f)), yaw + 180f + Range(-20f, 20f)));
                Place("PR_Escombros_Queimados_B", () => (L(Side() * Range(2.2f, 3f), Range(-2f, 2f)), yaw + (rnd.Next(2) == 0 ? 0f : 180f) + Range(-8f, 8f)));
                for (int robot = 0; robot < 3; robot++)
                    Place("PR_Sentinela_Destruida", () => (rnd.Next(3) == 0
                        ? L(Range(-3f, 3f), Side() * Range(9f, 12f))
                        : L(Side() * Range(3.4f, 6f), Range(-8f, 8f)), Range(0f, 360f)));
            }
        }

        // ---------- tudo ----------

        static void BuildReinoSiege(Transform props)
        {
            Physics.SyncTransforms();
            s_SiegeReserved.Clear();
            s_SiegePlaced = s_SiegeSkipped = 0;
            var root = new GameObject("Cerco_do_Reino").transform;
            root.SetParent(props, false);
            BuildBarbican(root);
            BuildReinoTornRails(root);
            BuildReinoDeckDamage(root);
            BuildBastionDefense(root);
            BuildCapsizedWreckage(root);
            Debug.Log($"[Picos] Cerco da Ponte do Reino: {s_SiegePlaced} peças; {s_SiegeSkipped} sem espaço ficaram de fora " +
                      $"(barbacã em t {s_ReinoGateT:0.0}, beira da cidade em t {ReinoCityEdgeT:0.0})");
        }

        // ---------- barbacã ----------

        static void BuildBarbican(Transform root)
        {
            Vector3 d = ReinoDir, up = Vector3.up;
            float gate = s_ReinoGateT, D = k_ReinoDeck;
            PicosGothicKit.Place(s_Materials, "AR_Portal_Barbaca", ReinoAt(gate), YawOf(d), root, true, ReinoFolder);
            Physics.SyncTransforms();

            // Piso de lajes do fim do tabuleiro (entra k_CityBridgeIn na ilha) até 10 m depois do portão de dentro, 5 cm
            // acima do topo da ilha, com o corpo do tabuleiro embaixo (onde a beira da ilha recua, fica sobre o vazio)
            float t0 = ReinoCityEdgeT - k_CityBridgeIn, t1 = gate - k_BarbicanDepth - 10f;
            if (t1 < t0 - 1f)
            {
                Vector3 a = ReinoAt(t0), b = ReinoAt(t1);
                Quaternion rotation = Quaternion.LookRotation(b - a);
                float width = k_BarbicanCourt * 2f, length = t0 - t1;
                var slab = new MeshData();
                BlockMesh.Box(slab, (a + b) * 0.5f + up * (0.05f - 0.175f), new Vector3(width, 0.35f, length), rotation);
                Pave(slab);
                BlockMesh.ChamferBox(s_Ruins.Pick(), (a + b) * 0.5f - up * (0.3f + (k_ReinoUnder - 0.3f) * 0.5f),
                    new Vector3(width - 0.1f, k_ReinoUnder - 0.3f, length), rotation, 0.08f);
                SolidRamp(s_PathColliders, a + up * 0.05f, b + up * 0.05f, width);
                s_Platforms.Add(((a + b) * 0.5f, width * 0.5f));
            }

            // Estandartes queimados na frente do portão, dos dois lados do arco
            foreach (float s in new[] { -1f, 1f })
            {
                PlaceTech("PR_Estandarte_Queimado", ReinoPoint(gate, s * 12.5f) + up * 13f + d * 0.03f, YawOf(d), root, 1f, false);
                s_SiegePlaced++;
            }

            // O pior do estrago, dentro do pátio (de 14 m a 40 m atrás da frente): cratera, a última barricada virada para a
            // cidade, armas largadas, corpos carbonizados e montes de escombros queimados encostados nas muralhas
            var rnd = new System.Random(4611);
            float Range(float x, float y) => x + (float)rnd.NextDouble() * (y - x);
            System.Func<Vector3, bool> court = w =>
            {
                (float t, float o) = ReinoCoords(w);
                return t < gate - 14.6f && t > gate - 39.4f && Mathf.Abs(o) < k_BarbicanCourt - 0.3f;
            };
            System.Func<Vector3, bool> courtWall = w =>
            {
                (float t, float o) = ReinoCoords(w);
                return t < gate - 14.6f && t > gate - 39.4f && Mathf.Abs(o) < k_BarbicanCourt + 0.7f;
            };
            GameObject crater = SiegeScatter(rnd, "PR_Cratera_A", root, r => (ReinoPoint(gate - Range(24f, 29f), Range(-5f, -2f)), Range(0f, 360f)), court);
            SiegeScatter(rnd, "PR_Barricada_A", root, r => (ReinoPoint(gate - Range(33f, 36f), Range(5.5f, 7f)), YawOf(-d) + Range(-8f, 8f)), court);
            SiegeScatter(rnd, "PR_Armas_Largadas", root, r => (ReinoPoint(gate - Range(29f, 31f), Range(4f, 8f)), Range(0f, 360f)), court, 30, false);
            for (int k = 0; k < 2; k++)
            {
                Vector3 near = crater ? crater.transform.position : ReinoPoint(gate - 26f, -3f);
                SiegeScatter(rnd, "PR_Corpos_Carbonizados", root, r => (near + Around(Range(0f, 6.3f)) * Range(6.5f, 8f), Range(0f, 360f)), court, 30, false);
            }

            foreach ((float t, float s) in new[] { (gate - 19f, 1f), (gate - 33f, -1f), (gate - 22f, -1f) })
                SiegeScatter(rnd, "PR_Escombros_Queimados_C", root, r => (ReinoPoint(t + Range(-2f, 2f), s * k_BarbicanCourt), YawOf(-ReinoRight * s)), courtWall);
            SiegeScatter(rnd, "PR_Sentinela_Destruida", root, r => (ReinoPoint(gate - Range(16f, 38f), Range(-11f, 11f)), Range(0f, 360f)), court);
            SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, r => (ReinoPoint(gate - Range(16f, 38f), Range(-11f, 11f)), Range(0f, 360f)), court);
        }

        // ---------- balaústres arrancados ----------

        static void BuildReinoTornRails(Transform root)
        {
            var rnd = new System.Random(4621);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 d = ReinoDir, right = ReinoRight, up = Vector3.up;
            float half = k_ReinoWidth * 0.5f, D = k_ReinoDeck;
            var walls = new GameObject("Ponte_Barreiras").transform;
            walls.SetParent(s_World, false);

            foreach ((float t0, float t1, float side) in s_ReinoTorn)
            {
                MeshData tone = s_Ruins.Pick();
                float o = side * (half - 0.35f);
                Quaternion along = Quaternion.LookRotation(d);

                // Tocos do plinto partido nas duas pontas do vão, tortos
                foreach ((float t, float dir) in new[] { (t0, 1f), (t1, -1f) })
                {
                    float length = Range(0.5f, 1.2f);
                    BlockMesh.ChamferBox(tone, ReinoPoint(t + dir * length * 0.5f, o) + up * 0.12f, new Vector3(0.7f, 0.24f, length),
                        along * Quaternion.Euler(Range(-6f, 6f), Range(-8f, 8f), Range(-5f, 5f)), 0.04f);
                    if (rnd.NextDouble() < 0.6)
                        BlockMesh.Prism(tone, ReinoPoint(t + dir * Range(0.4f, 0.8f), o) + up * 0.2f, up, Range(0.25f, 0.5f), 0.17f, 6, 0f, true);
                }

                // Balaústres caídos e um pedaço do corrimão no tabuleiro, do lado de dentro do vão
                int count = 3 + rnd.Next(3);
                for (int k = 0; k < count; k++)
                {
                    float a = Range(0f, Mathf.PI * 2f);
                    var axis = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 p = ReinoPoint(Range(t0 - 1.5f, t1 + 1.5f), side * (half - Range(1.4f, 3.6f))) + up * 0.16f - axis * 0.38f;
                    BlockMesh.Prism(tone, p, axis, 0.76f, 0.17f, 6, Range(0f, 60f), true);
                }

                float rail = Range(1.6f, 2.8f);
                BlockMesh.ChamferBox(tone, ReinoPoint(Range(t0, t1), side * (half - Range(2f, 3f))) + up * 0.11f, new Vector3(0.62f, 0.24f, rail),
                    along * Quaternion.Euler(0f, Range(-40f, 40f), 0f), 0.06f);
                s_SiegeReserved.Add((ReinoPoint((t0 + t1) * 0.5f, side * (half - 2.5f)), (t1 - t0) * 0.5f + 2.5f));

                // Barreira invisível no vão (não se cai da ponte por ele)
                var wall = new GameObject("Barreira_Balaustre");
                wall.transform.SetParent(walls, false);
                wall.transform.SetPositionAndRotation(ReinoPoint((t0 + t1) * 0.5f, o) + up * 1.3f, along);
                wall.AddComponent<BoxCollider>().size = new Vector3(0.7f, 2.6f, t1 - t0 + 0.6f);
            }
        }

        // ---------- estrago no tabuleiro ----------

        static void BuildReinoDeckDamage(Transform root)
        {
            var rnd = new System.Random(4631);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 d = ReinoDir, right = ReinoRight;
            float half = k_ReinoWidth * 0.5f;

            // Crateras primeiro (no meio do tabuleiro, por cima do trilho, que quebra nelas)
            foreach (float c in s_ReinoCraters)
            {
                GameObject crater = SiegeScatter(rnd, "PR_Cratera_A", root, r => (ReinoPoint(c + Range(-1f, 1f), Range(-1f, 1f)), Range(0f, 360f)),
                    OnReinoDeck(c - 9f, c + 9f, -1f));
                if (crater && rnd.NextDouble() < 0.7)
                {
                    Vector3 at = crater.transform.position;
                    SiegeScatter(rnd, "PR_Corpos_Carbonizados", root, r => (at + Around(Range(0f, 6.3f)) * Range(6.5f, 7.5f), Range(0f, 360f)),
                        OnReinoDeck(c - 12f, c + 12f, 1.3f), 20, false);
                }
            }

            foreach ((float a, float b, List<float> piers, int level) in ReinoStretches())
            {
                System.Func<Vector3, bool> lane = OnReinoDeck(a, b, 1.3f);
                (Vector3, float) Spot(System.Random r, float spread) =>
                    (ReinoPoint(Range(a + 4f, b - 4f), (r.Next(2) == 0 ? 1f : -1f) * Range(3f, half - 3f)), YawOf(d) + Range(-spread, spread));

                // Barricadas em chicana viradas para a cidade, armas largadas atrás (do lado do bastião)
                for (int k = 0; k < level - 1; k++)
                {
                    for (int tries = 0; tries < 25; tries++)
                    {
                        float t = Range(a + 14f, b - 8f), s = rnd.Next(2) == 0 ? 1f : -1f;
                        if (!SiegePiece("PR_Barricada_A", ReinoPoint(t, s * Range(6.6f, 7.1f)), YawOf(-d) + Range(-8f, 8f), root, lane))
                            continue;
                        SiegePiece("PR_Barricada_A", ReinoPoint(t - Range(8.5f, 10f), -s * Range(6.6f, 7.1f)), YawOf(-d) + Range(-8f, 8f), root, lane);
                        SiegePiece("PR_Armas_Largadas", ReinoPoint(t + 3f, s * Range(4f, 8f)), Range(0f, 360f), root, lane, false);
                        break;
                    }
                }

                // Cápsulas destruídas e tombadas fora do trilho
                if (level >= 2)
                {
                    bool done = false;
                    for (int tries = 0; tries < 25 && !done; tries++)
                    {
                        float s = rnd.Next(2) == 0 ? 1f : -1f;
                        done = SiegeCapsized(root, ReinoPoint(Range(a + 6f, b - 6f), s * Range(4.5f, 6.5f)), YawOf(d) + Range(-18f, 18f),
                            s * Range(84f, 96f), lane);
                    }

                    if (!done)
                        s_SiegeSkipped++;
                }

                if (level >= 3)
                    SiegeScatter(rnd, "PR_Capsula_Destruida", root, r => Spot(r, 180f), lane);

                for (int k = 0; k < level; k++)
                    SiegeScatter(rnd, k % 2 == 0 ? "PR_Escombros_Queimados_A" : "PR_Escombros_Queimados_B", root, r => Spot(r, 20f), lane);
                for (int k = 0; k < level - 1; k++)
                {
                    SiegeScatter(rnd, "PR_Sentinela_Destruida", root, r => Spot(r, 180f), lane);
                    SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, r => Spot(r, 180f), lane);
                }

                if (level >= 2)
                    SiegeScatter(rnd, "PR_Coluna_Bobinas_Tombada", root, r => Spot(r, 12f), lane);

                // Mastros com estandartes queimados nos refúgios, ao lado da estátua, do lado da cidade: todos perto da
                // cidade, um lado sim, outro não do bastião à rotatória e um só do lado da Vila da Guarda
                for (int i = 0; i < piers.Count; i++)
                {
                    float t = piers[i];
                    foreach (float s in new[] { -1f, 1f })
                    {
                        bool want = level >= 3 || (level == 2 && (i + (s > 0 ? 0 : 1)) % 2 == 0) || (level == 1 && i == 0 && s > 0);
                        if (!want)
                            continue;
                        // Só a base precisa do piso do refúgio (o pano fica acima dos balaústres, pendurado para o lado
                        // da cidade); a estátua fica no meio do refúgio, ao lado
                        Vector3 p = ReinoPoint(t - 2f, s * (half + 0.9f));
                        if (Physics.CheckBox(p + Vector3.up * 0.45f, new Vector3(0.5f, 0.3f, 0.5f), Quaternion.Euler(0f, YawOf(-d), 0f), ~0,
                                QueryTriggerInteraction.Ignore))
                        {
                            s_SiegeSkipped++;
                            continue;
                        }

                        PlaceTech("PR_Mastro_Queimado", p, YawOf(-d), root);
                        s_SiegePlaced++;
                    }
                }
            }
        }

        // ---------- bastião ----------

        static void BuildBastionDefense(Transform root)
        {
            Vector3 c = ReinoBastion, d = ReinoDir, right = ReinoRight, up = Vector3.up;
            float D = c.y, inr = ReinoBastionInradius;
            const float bank = 42f;                 // beira de dentro das banquetas
            Vector3 P(float u, float v) => c + d * u + right * v;

            // Banquetas (0,7 m) atrás do parapeito nas faces 1 a 3 e 5 a 7, com o cordão na beira, uma escada no meio de
            // cada face e a barreira invisível acima do parapeito
            float top = D + 0.7f, bottom = D - 0.4f, outer = inr - 0.95f, sec = 1f / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            var fence = new GameObject("Bastiao_Barreira").transform;
            fence.SetParent(s_World, false);
            foreach (int side in new[] { 1, -1 })
            {
                MeshData tone = s_Ruins.Pick();
                for (int f = 1; f <= 3; f++)
                {
                    int k = side > 0 ? f : 8 - f;
                    Vector3 c0 = BastionCorner(k - 1), c1 = BastionCorner(k);
                    Vector3 n = Xz(c0 + c1).normalized;
                    Vector3 Q(Vector3 corner, float dist, float y) => new Vector3(c.x, y, c.z) + corner * (dist * sec);
                    float mid = (top + bottom) * 0.5f;
                    ConvexPrism(tone, new[] { Q(c0, bank, mid), Q(c1, bank, mid), Q(c1, outer, mid), Q(c0, outer, mid) }, up, top - bottom);
                    float lip = top + 0.01f;
                    ConvexPrism(tone, new[] { Q(c0, bank - 0.15f, lip), Q(c1, bank - 0.15f, lip), Q(c1, bank + 0.4f, lip), Q(c0, bank + 0.4f, lip) },
                        up, 0.2f);
                    Stairs(new Vector3(c.x, D, c.z) + n * (bank - 2.1f), new Vector3(c.x, top, c.z) + n * bank, 3.2f);

                    var wall = new GameObject("Barreira_Parapeito");
                    wall.transform.SetParent(fence, false);
                    float line = inr - 0.55f;
                    wall.transform.SetPositionAndRotation(new Vector3(c.x, D + 2.4f, c.z) + n * line, Quaternion.LookRotation(n));
                    wall.AddComponent<BoxCollider>().size = new Vector3(2f * line * Mathf.Tan(22.5f * Mathf.Deg2Rad) + 0.6f, 2.2f, 0.9f);
                    s_SiegeReserved.Add((new Vector3(c.x, D, c.z) + n * (bank - 1.05f), 2.6f));
                }
            }

            // Fora das banquetas, da beira das faces da ponte e do trilho (que chega pelo lado da cidade até 25 m do meio)
            System.Func<Vector3, bool> floor = w =>
            {
                Vector3 rel = Xz(w - c);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 n = Xz(BastionCorner(k - 1) + BastionCorner(k)).normalized;
                    float limit = k == 0 || k == 4 ? inr - 1.6f : bank - 0.4f;
                    if (Vector3.Dot(rel, n) > limit)
                        return false;
                }

                float u = Vector3.Dot(rel, d), v = Vector3.Dot(rel, right);
                return !(u < -24f && Mathf.Abs(v) < 1.6f);
            };

            var rnd = new System.Random(4641);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // Linha de defesa virada para a cidade: duas barricadas na estrada (o meio fica livre) e uma na ala direita,
            // com as armas largadas atrás
            foreach ((float u, float v) in new[] { (-34f, 7f), (-34f, -7f), (-26f, 25f) })
            {
                GameObject b = SiegeScatter(rnd, "PR_Barricada_A", root, r => (P(u + Range(-1.5f, 1.5f), v + Range(-0.4f, 0.4f)), YawOf(-d) + Range(-10f, 10f)), floor);
                if (b)
                    SiegePiece("PR_Armas_Largadas", P(u + 3.2f, v + Range(-1.5f, 1.5f)), Range(0f, 360f), root, floor, false);
            }

            // Cratera na ala esquerda do lado da cidade, com corpos na beira
            GameObject crater = SiegeScatter(rnd, "PR_Cratera_A", root, r => (P(Range(-30f, -25f), Range(-23f, -19f)), Range(0f, 360f)), floor);
            if (crater)
            {
                Vector3 at = crater.transform.position;
                SiegeScatter(rnd, "PR_Corpos_Carbonizados", root, r => (at + Around(Range(0f, 6.3f)) * Range(6.5f, 7.5f), Range(0f, 360f)), floor, 20, false);
            }

            // Cápsula tombada e guardião caído na ala direita; cápsula destruída e coluna-lanterna tombada na esquerda
            bool capsized = false;
            for (int k = 0; k < 20 && !capsized; k++)
                capsized = SiegeCapsized(root, P(Range(-13f, -7f), Range(34f, 37f)), YawOf(d) + Range(-15f, 15f), Range(84f, 96f), floor);
            if (!capsized)
                s_SiegeSkipped++;
            bool fallen = false;
            for (int k = 0; k < 20 && !fallen; k++)
                fallen = FallenGuardian(root, P(Range(2f, 6f), Range(34f, 37f)), YawOf(d) + Range(-12f, 12f), floor);
            if (!fallen)
                s_SiegeSkipped++;
            SiegeScatter(rnd, "PR_Capsula_Destruida", root, r => (P(Range(6f, 14f), Range(-38f, -33f)), YawOf(d) + Range(40f, 100f)), floor);
            SiegeScatter(rnd, "PR_Coluna_Bobinas_Tombada", root, r => (P(Range(-8f, -4f), Range(-38f, -35f)), YawOf(d) + Range(-10f, 10f)), floor);

            // Autômatos, cristais e escombros queimados pelas alas
            (Vector3, float) Wing(System.Random r, bool city) =>
                (P(city ? Range(-36f, -10f) : Range(10f, 34f), (r.Next(2) == 0 ? 1f : -1f) * Range(18f, 38f)), Range(0f, 360f));
            SiegeScatter(rnd, "PR_Sentinela_Destruida", root, r => (P(Range(-31f, -28f), Range(-5.5f, -3.5f)), Range(0f, 360f)), floor);
            SiegeScatter(rnd, "PR_Sentinela_Destruida", root, r => Wing(r, true), floor);
            SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, r => Wing(r, true), floor);
            SiegeScatter(rnd, "PR_Cristais_Estilhacados", root, r => Wing(r, false), floor);
            SiegeScatter(rnd, "PR_Escombros_Queimados_A", root, r => Wing(r, true), floor);
            SiegeScatter(rnd, "PR_Escombros_Queimados_B", root, r => Wing(r, false), floor);
        }

        // Ameias caídas nas faces do lado da cidade (3, 4 e 5): chamado pelo BuildReinoBastion para cada ameia; devolve
        // verdadeiro se ela caiu (e põe o bloco deitado na banqueta ou no piso, do lado de dentro)
        static bool BastionMerlonFallen(RuinMesh m, int k, int i, Vector3 q, Vector3 normal, Vector3 along, float D)
        {
            if ((k != 3 && k != 4 && k != 5) || (i * 7 + k * 3) % 5 != 1)
                return false;
            // Nada cai em cima da escada do meio da banqueta
            if (k != 4 && Mathf.Abs(Vector3.Dot(Xz(q - ReinoBastion), along)) < 2.8f)
                return false;
            float h = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f + k * 78.233f)) % 1f;
            float floorY = k == 4 ? D : D + 0.7f;
            Vector3 p = Xz(q) - normal * (k == 4 ? 2.4f + h * 1.5f : 1.6f + h * 0.9f) + along * ((h - 0.5f) * 1.6f);
            BlockMesh.ChamferBox(m.Pick(), p + Vector3.up * (floorY + 0.43f), new Vector3(0.9f, 1.4f, 1.5f),
                Quaternion.LookRotation(along) * Quaternion.Euler(0f, (h - 0.5f) * 70f, 90f), 0.06f);
            return true;
        }
    }
}
