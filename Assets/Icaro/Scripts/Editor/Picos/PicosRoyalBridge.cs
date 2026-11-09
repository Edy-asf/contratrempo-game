using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Ponte do Reino (Vilas do Alto). Primeira versão em 2026-11-01 (a ponte colossal só ligava a cidade ao caminho
    // estreito do jogador); refeita em 2026-11-02 a pedido do usuário: a ponte é uma linha reta só, mais longe da
    // montanha (no lugar riscado por ele), com a cidade alinhada a ela. Saindo da cidade:
    //   cidade → ponte → rotatória com o monumento (no meio do trecho entre a cidade e o bastião) → ponte → bastião com o
    //   Portal do Reino (atravessado pela estrada) → conexão com a Vila da Guarda → desabamento → a ponte segue reta por
    //   ~800 m, com vãos caídos, pilares partidos e escombros → curva para a esquerda até uma área redonda vazia, escondida
    //   pelos picos na frente da curva.
    // O jogador chega pela Vila da Guarda (sala 23, num pico próprio entre o platô do antigo portão e a ponte; as duas
    // pontes dela são caminhos construídos, PicosRoad) e pode seguir até a cidade; o desabamento corta o resto.
    // - bastião oitavado (uma face virada para cada lado da ponte, piso, cornija com mísulas, corpo em três andares com
    //   talude e seteiras, contrafortes, parapeito com ameias aberto para a ponte dos dois lados) com o Portal do Reino
    //   (kit PonteDoReino) de frente para quem chega da vila, com degraus nas duas pontas do estrado dele;
    // - ponte: tabuleiro de lajes com cornija e dentículos, balaústres com pilaretes, arcos de aduelas com tímpanos
    //   maciços, pilares até a rocha (raio) ou até dentro da nuvem, com impostas e faixas, e refúgios oitavados com uma
    //   estátua de guardião ou uma coluna de lampiões, alternando (depois do desabamento, num pilar sim, noutro não);
    // - vãos caídos: pontas do tabuleiro em dente, uma laje pendurada, meios-arcos partidos e pilares partidos no meio
    //   do vão; no trecho de lá, balaústres faltando, montes de blocos e estátuas tombadas no tabuleiro;
    // - rotatória redonda com o monumento do relógio, tambor até as nuvens, cornija e balaústres; a área do fundo é o
    //   mesmo tambor, sem nada em cima.
    // Coordenadas: o eixo da ponte sai de k_ReinoOrigin (planta: leste, norte a partir de k_High) no ângulo
    // k_ReinoAngle; 't' é a distância ao longo dele (negativo para o lado da cidade).
    public static partial class PicosBuilder
    {
        static readonly Vector2 k_ReinoOrigin = new Vector2(510f, 250f);
        const float k_ReinoAngle = 55f;            // direção do eixo na planta (graus, do leste para o norte)
        const float k_ReinoCityT = -1176f;         // centro da cidade
        const float k_ReinoBastionT = -300f;
        const float k_ReinoJoinT = -170f;          // onde a ponte da Vila da Guarda chega
        const float k_ReinoCollapseFrom = -135f, k_ReinoCollapseTo = -60f;
        const float k_ReinoStraightEnd = 760f;     // fim da reta; dali a curva
        static readonly (float A, float B)[] k_ReinoFarGaps = { (190f, 240f), (390f, 440f), (600f, 640f) };
        const float k_ReinoCurveTurn = 45f, k_ReinoCurveLength = 126f;
        const int k_ReinoCurveSteps = 8;
        const float k_ReinoEndRadius = 40f;

        const float k_ReinoDeck = 30f;             // altura do piso da ponte, da rotatória e do bastião
        const float k_ReinoWidth = 24f;
        const float k_ReinoSpan = 37f;             // vão de pilar a pilar (aproximado)
        const float k_ReinoBastionRadius = 50f;    // do centro às quinas do bastião (oitavado)
        const float k_ReinoRotundaRadius = 36f;
        const float k_ReinoUnder = 1.6f;           // do piso até a base do tabuleiro (onde começam os tímpanos)
        const float k_ReinoRing = 2.6f;            // espessura das aduelas dos arcos
        const float k_ReinoPier = 7f;              // largura dos pilares ao longo da ponte
        const float k_ReinoRefuge = 4.2f;          // raio do refúgio oitavado sobre cada pilar

        // Vila da Guarda: a 120 m da ponte, para o lado da montanha, na altura do platô do antigo portão
        const float k_ReinoVillageOffset = 120f, k_ReinoVillageRadius = 42f, k_ReinoVillageFloor = 34f;

        static bool s_ReinoBuilt;
        static Vector3 s_ReinoGate;     // centro do platô do antigo portão (de onde vem a ponte para a vila)

        static string ReinoFolder => IcaroAssetUtility.Root + "/Models/Picos/PonteDoReino";
        static Vector3 ReinoDir => new Vector3(Mathf.Cos(k_ReinoAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(k_ReinoAngle * Mathf.Deg2Rad));
        static Vector3 ReinoRight => Vector3.Cross(Vector3.up, ReinoDir);      // lado de fora (sudeste)
        static Vector3 ReinoAt(float t) => HP(k_ReinoOrigin.x, k_ReinoOrigin.y, k_ReinoDeck) + ReinoDir * t;
        static float ReinoBastionInradius => k_ReinoBastionRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
        // A rotatória fica no meio do trecho entre a beira da cidade e a face do bastião (pedido do usuário)
        static float ReinoRotundaT => (k_ReinoCityT + k_CityRadius + k_ReinoBastionT - ReinoBastionInradius) * 0.5f;
        static Vector3 ReinoBastion => ReinoAt(k_ReinoBastionT);
        static Vector3 ReinoRotunda => ReinoAt(ReinoRotundaT);
        static Vector3 ReinoJoin => ReinoAt(k_ReinoJoinT);

        static Vector3 ReinoCity
        {
            get
            {
                Vector3 c = ReinoAt(k_ReinoCityT);
                c.y = k_CityTop;
                return c;
            }
        }

        static Node ReinoVillage =>
            Peak(ReinoJoin - ReinoRight * k_ReinoVillageOffset + Vector3.up * (k_ReinoVillageFloor - k_ReinoDeck), k_ReinoVillageRadius);

        // Bastião, ponte (com os desabamentos), rotatória, a ponte até a cidade, a curva, a área do fundo e os picos que a
        // escondem; a Vila da Guarda e as pontes dela ficam no BuildReinoVillage
        static void BuildRoyalBridge(Vector3 city)
        {
            Physics.SyncTransforms();
            var root = new GameObject("Ponte_do_Reino").transform;
            root.SetParent(s_World, false);
            Vector3 d = ReinoDir;
            float half = k_ReinoWidth * 0.5f, inr = ReinoBastionInradius;
            float rotT = ReinoRotundaT, rotR = k_ReinoRotundaRadius, bastT = k_ReinoBastionT;
            float inRot = Mathf.Sqrt(rotR * rotR - half * half);
            float railRot = Mathf.Sqrt(Mathf.Pow(rotR - 0.6f, 2f) - Mathf.Pow(half - 0.35f, 2f));
            int statue = 0;

            PlanReinoSiege();
            BuildReinoBastion(root, d);
            BuildReinoRotunda(root, d);

            // 1. Da cidade (a ponte entra k_CityBridgeIn na ilha) até a rotatória, inteira
            float cityEdge = k_ReinoCityT + k_CityRadius;
            // (os balaústres começam na barbacã, que fica onde o eixo cruza a beira da ilha; trechos arrancados pelo cerco)
            KingStraight(root, cityEdge, rotT - (rotR - 0.5f), cityEdge - k_CityBridgeIn, rotT - inRot, Mathf.Max(cityEdge - k_CityBridgeIn, s_ReinoGateT + 0.5f),
                rotT - railRot, new (float, float)[0], _ => true, _ => 1, ReinoTornRuns(-1f, cityEdge, rotT), ReinoTornRuns(1f, cityEdge, rotT),
                ref statue);

            // 2. Da rotatória ao bastião, inteira
            KingStraight(root, rotT + (rotR - 0.5f), bastT - inr, rotT + inRot, bastT - inr + 2f, rotT + railRot, bastT - inr,
                new (float, float)[0], _ => true, _ => 1, ReinoTornRuns(-1f, rotT, bastT), ReinoTornRuns(1f, rotT, bastT), ref statue);

            // 3. Do bastião ao fim da reta: a conexão da vila, o desabamento logo depois dela e os vãos caídos de lá;
            // só o trecho até o desabamento se anda. No trecho de lá faltam pedaços de balaustrada
            var gaps = new List<(float, float)> { (k_ReinoCollapseFrom, k_ReinoCollapseTo) };
            gaps.AddRange(k_ReinoFarGaps);
            var openLeft = new List<(float, float)> { (k_ReinoJoinT - 4.6f, k_ReinoJoinT + 4.6f) };
            var openRight = new List<(float, float)>();
            for (float t = k_ReinoCollapseTo + 10f; t < k_ReinoStraightEnd - 10f; t += R(30f, 70f))
                (s_Random.Next(2) == 0 ? openLeft : openRight).Add((t, t + R(5f, 13f)));
            openLeft.AddRange(ReinoTornRuns(-1f, bastT, k_ReinoCollapseFrom));
            openRight.AddRange(ReinoTornRuns(1f, bastT, k_ReinoCollapseFrom));
            KingStraight(root, bastT + inr, k_ReinoStraightEnd, bastT + inr - 2f, k_ReinoStraightEnd, bastT + inr, k_ReinoStraightEnd,
                gaps.ToArray(), t => t < k_ReinoCollapseFrom, t => t < k_ReinoCollapseFrom ? 1 : 2, openLeft, openRight, ref statue);
            BuildReinoRubble(root, gaps);
            BuildReinoTrack(root);
            BuildReinoSiege(root);

            // 4. Curva para a esquerda e a área redonda do fundo (vazia), escondida pelos picos na frente da curva
            Vector3 endDir;
            Vector3 last = BuildReinoCurve(root, out endDir);
            Vector3 end = last + endDir * (k_ReinoEndRadius - 1f);
            KingDrum(end, k_ReinoEndRadius, new[] { -endDir });
            BuildReinoHiders();

            ClearLine(ReinoAt(k_ReinoCityT), ReinoAt(k_ReinoStraightEnd));
            ClearLine(ReinoAt(k_ReinoStraightEnd), end);
            Clear(ReinoBastion, 110f);
            Clear(ReinoRotunda, 90f);
            Clear(end, k_ReinoEndRadius + 25f);
            s_ReinoBuilt = true;
        }

        // Trecho reto do eixo: pilares de 'pierFrom' a 'pierTo' (as pontas são faces do bastião, da rotatória ou da beira
        // da cidade), tabuleiro de 'deckFrom' a 'deckTo' e balaústres de 'railFrom' a 'railTo' (em t). 'gaps' são os vãos
        // caídos: o tabuleiro termina em dente, os arcos partem 1,5 m antes e os pilares no meio deles ficam partidos.
        // 'walk' diz se o trecho que começa em t tem a rampa invisível; 'refugeEvery' põe refúgio a cada n pilares
        static void KingStraight(Transform props, float pierFrom, float pierTo, float deckFrom, float deckTo, float railFrom,
            float railTo, (float A, float B)[] gaps, System.Func<float, bool> walk, System.Func<float, int> refugeEvery,
            List<(float, float)> extraLeft, List<(float, float)> extraRight, ref int statue)
        {
            Vector3 d = ReinoDir, origin = ReinoAt(0f);
            float w = k_ReinoWidth, half = w * 0.5f;
            int spans = Mathf.Max(1, Mathf.RoundToInt((pierTo - pierFrom) / k_ReinoSpan));
            float span = (pierTo - pierFrom) / spans;
            var piers = new float[spans + 1];
            for (int k = 0; k <= spans; k++)
                piers[k] = pierFrom + span * k;

            bool InGap(float t, float margin)
            {
                foreach ((float a, float b) in gaps)
                    if (t > a + margin && t < b - margin)
                        return true;
                return false;
            }

            bool Standing(int k) => !InGap(piers[k], 3f);

            // Arcos e tímpanos (os pedaços fora dos vãos caídos); vão inteiro caído: só a ponta do arco junto de um pilar
            // que ficou em pé
            for (int k = 0; k < spans; k++)
            {
                float a = piers[k], b = piers[k + 1];
                List<(float, float)> keep = Subtract(a, b, gaps, 1.5f);
                if (keep.Count == 0)
                {
                    if (Standing(k))
                        KingSpan(ReinoAt(a), ReinoAt(b), w, k_ReinoPier, 0f, 0.09f, false);
                    if (Standing(k + 1))
                        KingSpan(ReinoAt(a), ReinoAt(b), w, k_ReinoPier, 0.91f, 1f, false);
                    continue;
                }

                foreach ((float u0, float u1) in keep)
                    if (u1 - u0 > 0.5f)
                        KingSpan(ReinoAt(a), ReinoAt(b), w, k_ReinoPier, (u0 - a) / (b - a), (u1 - a) / (b - a), true);
            }

            var openLeft = extraLeft != null ? new List<(float, float)>(extraLeft) : new List<(float, float)>();
            var openRight = extraRight != null ? new List<(float, float)>(extraRight) : new List<(float, float)>();
            for (int k = 1; k < spans; k++)
            {
                float t = piers[k];
                bool broken = InGap(t, 3f);
                // Sem refúgio junto da chegada da ponte da Vila da Guarda
                bool refuge = !broken && k % Mathf.Max(1, refugeEvery(t)) == 0 && Mathf.Abs(t - k_ReinoJoinT) > 14f;
                KingPier(ReinoAt(t), d, w, k_ReinoPier, span, broken ? R(9f, 32f) : 0f, refuge, props, ref statue);
                if (refuge)
                {
                    float r = k_ReinoRefuge * Mathf.Cos(22.5f * Mathf.Deg2Rad) - 0.35f;
                    openLeft.Add((t - r, t + r));
                    openRight.Add((t - r, t + r));
                }
            }

            // Tabuleiro e balaústres, trecho por trecho entre os vãos caídos
            foreach ((float a, float b) in Subtract(deckFrom, deckTo, gaps, 0f))
            {
                bool brokenStart = a > deckFrom + 0.01f, brokenEnd = b < deckTo - 0.01f;
                float a2 = brokenStart ? a + 3.5f : a, b2 = brokenEnd ? b - 3.5f : b;
                if (b2 - a2 < 1f)
                    continue;
                KingDeck(ReinoAt(a2), ReinoAt(b2), w, walk(a), true);
                if (brokenStart)
                    KingJagged(ReinoAt(a2), -d, w);
                if (brokenEnd)
                    KingJagged(ReinoAt(b2), d, w);
                float r0 = Mathf.Max(a2, railFrom) + (brokenStart ? R(1.5f, 3.5f) : 0f);
                float r1 = Mathf.Min(b2, railTo) - (brokenEnd ? R(1.5f, 4f) : 0f);
                KingRails(origin, d, 1f, half - 0.35f, r0, r1, openRight);
                KingRails(origin, d, -1f, half - 0.35f, r0, r1, openLeft);
            }
        }

        // [from, to] menos os vãos (alargados por 'pad'), em ordem
        static List<(float, float)> Subtract(float from, float to, (float A, float B)[] gaps, float pad)
        {
            var runs = new List<(float, float)> { (from, to) };
            foreach ((float g0, float g1) in gaps)
            {
                float a = g0 - pad, b = g1 + pad;
                var next = new List<(float, float)>();
                foreach ((float r0, float r1) in runs)
                {
                    if (b <= r0 || a >= r1)
                    {
                        next.Add((r0, r1));
                        continue;
                    }

                    if (a > r0)
                        next.Add((r0, a));
                    if (b < r1)
                        next.Add((b, r1));
                }

                runs = next;
            }

            return runs;
        }

        // ---------- peças da ponte ----------

        // Tabuleiro reto de 'a' a 'b' (pontos no piso, no meio): lajes por cima (pisos com neve), a estrutura até a base
        // do tabuleiro, cornija com dentículos dos dois lados, faixa na base, pingentes e a rampa invisível
        static void KingDeck(Vector3 a, Vector3 b, float width, bool collider, bool rich)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < 0.2f)
                return;
            Quaternion rotation = Quaternion.LookRotation(along);
            Vector3 flat = Xz(along).normalized, right = Vector3.Cross(Vector3.up, flat), up = Vector3.up;
            var slab = new MeshData();
            BlockMesh.Box(slab, (a + b) * 0.5f + up * -0.175f, new Vector3(width, 0.35f, length), rotation);
            Pave(slab);

            int blocks = Mathf.Max(1, Mathf.CeilToInt(length / 8f));
            float block = length / blocks;
            for (int i = 0; i < blocks; i++)
            {
                Vector3 c = Vector3.Lerp(a, b, (i + 0.5f) / blocks);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c - up * (0.35f + (k_ReinoUnder - 0.35f) * 0.5f),
                    new Vector3(width - 0.1f, k_ReinoUnder - 0.35f, block + 0.02f), rotation, 0.08f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 edge = c + right * s * (width * 0.5f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), edge + right * s * 0.25f - up * 0.62f,
                        new Vector3(1.1f, 0.55f, block + 0.02f), rotation, 0.1f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), edge + right * s * 0.08f - up * (k_ReinoUnder - 0.15f),
                        new Vector3(0.5f, 0.32f, block + 0.02f), rotation, 0.05f);
                    if (rich)
                    {
                        int dentils = Mathf.Max(1, Mathf.RoundToInt(block / 1.5f));
                        for (int k = 0; k < dentils; k++)
                        {
                            Vector3 q = Vector3.Lerp(c - along / blocks * 0.5f, c + along / blocks * 0.5f, (k + 0.5f) / dentils);
                            BlockMesh.Box(s_Ruins.Pick(), q + right * s * (width * 0.5f + 0.14f) - up * 1.1f,
                                new Vector3(0.42f, 0.38f, 0.55f), rotation);
                        }
                    }

                    if (s_Random.NextDouble() < 0.35)
                        Spike(s_Ice, edge + right * s * R(0.2f, 0.6f) + along / blocks * R(-0.4f, 0.4f) - up * 0.9f, Vector3.down,
                            R(0.5f, 1.8f), R(0.1f, 0.22f), 5, 0f, 3f);
                }
            }

            if (collider)
                SolidRamp(s_PathColliders, a, b, width);
        }

        // Balaústres de um lado ('side' 1 = à direita de 'dir', -1 = à esquerda) a 'offset' do eixo que sai de 'origin'
        // na direção 'dir', de 'from' a 'to' em s, abertos nos trechos 'open'; pilaretes a cada ~7 m e nas pontas
        static void KingRails(Vector3 origin, Vector3 dir, float side, float offset, float from, float to,
            List<(float From, float To)> open)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            var runs = new List<(float, float)> { (from, to) };
            foreach ((float o0, float o1) in open)
            {
                var next = new List<(float, float)>();
                foreach ((float r0, float r1) in runs)
                {
                    if (o1 <= r0 || o0 >= r1)
                    {
                        next.Add((r0, r1));
                        continue;
                    }

                    if (o0 > r0)
                        next.Add((r0, o0));
                    if (o1 < r1)
                        next.Add((o1, r1));
                }

                runs = next;
            }

            foreach ((float r0, float r1) in runs)
                if (r1 - r0 > 0.6f)
                    KingRailRun(origin + dir * r0 + right * side * offset, origin + dir * r1 + right * side * offset, true);
        }

        // Balaustrada de 'a' a 'b' (no piso): plinto, balaústres (prismas, ou blocos na versão simples), corrimão e
        // pilaretes com capa a cada ~7 m e nas pontas
        static void KingRailRun(Vector3 a, Vector3 b, bool rich)
        {
            Vector3 up = Vector3.up;
            float length = (b - a).magnitude;
            if (length < 0.3f)
                return;
            int parts = Mathf.Max(1, Mathf.CeilToInt(length / 7f));
            for (int p = 0; p < parts; p++)
            {
                Vector3 p0 = Vector3.Lerp(a, b, p / (float)parts), p1 = Vector3.Lerp(a, b, (p + 1) / (float)parts);
                Vector3 d = p1 - p0;
                float l = d.magnitude;
                Quaternion along = Quaternion.LookRotation(d);
                Vector3 mid = (p0 + p1) * 0.5f;
                BlockMesh.ChamferBox(s_Ruins.Pick(), mid + up * 0.15f, new Vector3(0.7f, 0.3f, l), along, 0.05f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), mid + up * 1.18f, new Vector3(0.62f, 0.24f, l), along, 0.06f);
                int count = Mathf.Max(1, Mathf.RoundToInt((l - 0.9f) / (rich ? 0.9f : 1.8f)));
                for (int i = 0; i < count; i++)
                {
                    Vector3 q = Vector3.Lerp(p0, p1, Mathf.Lerp(0.45f / l, 1f - 0.45f / l, (i + 0.5f) / count));
                    if (rich)
                        BlockMesh.Prism(s_Ruins.Pick(), q + up * 0.3f, up, 0.76f, 0.17f, 6, 0f, false);
                    else
                        BlockMesh.Box(s_Ruins.Pick(), q + up * 0.68f, new Vector3(0.28f, 0.76f, 0.28f), along);
                }
            }

            for (int p = 0; p <= parts; p++)
            {
                Vector3 q = Vector3.Lerp(a, b, p / (float)parts);
                Quaternion yaw = Quaternion.LookRotation(Xz(b - a));
                BlockMesh.ChamferBox(s_Ruins.Pick(), q + up * 0.72f, new Vector3(0.85f, 1.44f, 0.85f), yaw, 0.08f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), q + up * 1.52f, new Vector3(1.05f, 0.18f, 1.05f), yaw, 0.05f);
            }
        }

        // Vão em arco entre os pilares de 'a' e 'b' (pontos no piso, sobre o meio de cada pilar): arco de aduelas um pouco
        // mais largo que o tabuleiro e tímpanos maciços do arco até a base do tabuleiro. 'keep0'/'keep1' (0 a 1 do vão)
        // deixam só esse pedaço (o resto caiu; a aduela da ponta fica em dente); 'spandrel' falso: só o arco
        static void KingSpan(Vector3 a, Vector3 b, float width, float pier, float keep0, float keep1, bool spandrel)
        {
            Vector3 along = Xz(b - a);
            float length = along.magnitude;
            Vector3 f = along / length, right = Vector3.Cross(Vector3.up, f), up = Vector3.up;
            float clear = length - pier, ri = clear * 0.5f, t = k_ReinoRing, re = ri + t, rc = ri + t * 0.5f;
            Vector3 mid = (a + b) * 0.5f;
            float crown = mid.y - k_ReinoUnder, centerY = crown - t - ri;
            var center = new Vector3(mid.x, centerY, mid.z);
            float u0 = -length * 0.5f + keep0 * length, u1 = -length * 0.5f + keep1 * length;

            // Arco: 0° do lado de 'b', 180° do lado de 'a'
            float th0 = u1 >= rc ? 0f : Mathf.Acos(Mathf.Clamp(u1 / rc, -1f, 1f)) * Mathf.Rad2Deg;
            float th1 = u0 <= -rc ? 180f : Mathf.Acos(Mathf.Clamp(u0 / rc, -1f, 1f)) * Mathf.Rad2Deg;
            if (th1 - th0 > 2f)
            {
                bool cut = th0 > 0.5f || th1 < 179.5f;
                float ground = cut ? RockBelow(new Vector3(mid.x, centerY, mid.z), k_CloudY - 80f) : 0f;
                ArquipelagoRuinKit.RichRing(s_Ruins, center, -right, f, rc, t, width + 0.6f, th0, th1, s_Random, th0 > 0.5f,
                    th1 < 179.5f, ground, 4);
            }

            if (!spandrel)
                return;

            // Tímpanos: faixas verticais do extradorso (ou da linha das impostas, sobre o pilar) até a base do tabuleiro
            var cuts = new List<float> { -length * 0.5f, length * 0.5f };
            const int arcSteps = 14;
            for (int i = 0; i <= arcSteps; i++)
                cuts.Add(-re + 2f * re * i / arcSteps);
            cuts.Sort();
            float Bottom(float u) => Mathf.Abs(u) >= re ? centerY : centerY + Mathf.Sqrt(re * re - u * u);
            float Top(float u) => Mathf.Lerp(a.y, b.y, (u + length * 0.5f) / length) - k_ReinoUnder;
            Vector3 P(float u, float v) => new Vector3(mid.x, v, mid.z) + f * u;
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float x0 = Mathf.Max(cuts[i], u0), x1 = Mathf.Min(cuts[i + 1], u1);
                if (x1 - x0 < 0.05f)
                    continue;
                float b0 = Bottom(x0), b1 = Bottom(x1);
                var poly = new[] { P(x0, b0), P(x1, b1), P(x1, Top(x1)), P(x0, Top(x0)) };
                if (Top(x0) - b0 < 0.05f && Top(x1) - b1 < 0.05f)
                    continue;
                ConvexPrism(s_Ruins.Pick(), poly, right, width);
            }

            // Ponta quebrada dos tímpanos: blocos tortos na face do corte
            foreach ((float u, float sign) in new[] { (u0, -1f), (u1, 1f) })
            {
                if (Mathf.Abs(Mathf.Abs(u) - length * 0.5f) < 0.05f)
                    continue;
                for (int k = 0; k < 5; k++)
                {
                    float v = Mathf.Lerp(Bottom(u), Top(u), R(0.1f, 0.9f));
                    float size = R(0.9f, 1.8f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), P(u + sign * size * 0.25f, v) + right * R(-width * 0.4f, width * 0.4f),
                        new Vector3(size, size * R(0.6f, 1f), size), Quaternion.LookRotation(f) *
                                                                       Quaternion.Euler(R(-12f, 12f), R(-20f, 20f), R(-12f, 12f)), 0.1f);
                }
            }
        }

        // Pilar sob o ponto 'top' (no piso): até a rocha (raio, 4 m para dentro dela, com sapata) ou até dentro da nuvem;
        // impostas, faixas a cada ~18 m e, com 'refuge', um refúgio oitavado de cada lado (contraforte do pilar até o pé,
        // cornija, piso com neve, balaústres e uma estátua de guardião ou uma coluna de lampiões, alternando). 'cut' > 0:
        // pilar partido (o topo fica 'cut' abaixo da base do tabuleiro, em dente)
        static void KingPier(Vector3 top, Vector3 f, float width, float depth, float span, float cut, bool refuge, Transform props,
            ref int statue)
        {
            Vector3 up = Vector3.up, right = Vector3.Cross(up, f);
            Quaternion rotation = Quaternion.LookRotation(f);
            float deckBottom = top.y - k_ReinoUnder, high = deckBottom - cut;
            float rock = float.MaxValue;
            foreach (float x in new[] { -0.35f, 0f, 0.35f })
            foreach (float z in new[] { -0.3f, 0.3f })
            {
                float y = RockBelow(new Vector3(top.x, deckBottom - 1f, top.z) + right * x * width + f * z * depth, float.NaN);
                if (!float.IsNaN(y))
                    rock = Mathf.Min(rock, y);
            }

            bool onRock = rock < float.MaxValue;
            float bottom = onRock ? rock - 4f : k_CloudY - 45f;
            Vector3 foot = new Vector3(top.x, 0f, top.z);
            float height = high - bottom;
            if (height < 1f)
                return;
            ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), foot + up * (bottom + height * 0.5f), new Vector2(width + 1f, depth), height,
                rotation, 0.15f);
            if (cut > 0f)
                for (int k = 0; k < 6; k++)     // topo partido em dente
                {
                    float size = R(1.4f, 2.6f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), foot + up * (high + size * R(0.1f, 0.4f)) + right * R(-width * 0.45f, width * 0.45f) +
                                                         f * R(-depth * 0.35f, depth * 0.35f), new Vector3(size, size * R(0.8f, 1.6f), size),
                        rotation * Quaternion.Euler(R(-8f, 8f), R(0f, 90f), R(-8f, 8f)), 0.12f);
                }

            // Imposta na linha de onde os arcos nascem e faixas abaixo dela
            float spring = deckBottom - k_ReinoRing - (span - depth) * 0.5f;
            if (spring < high - 1f && spring > bottom + 4f)
                BlockMesh.ChamferBox(s_Ruins.Pick(), foot + up * (spring - 0.45f), new Vector3(width + 1.8f, 0.9f, depth + 1f), rotation, 0.12f);
            for (float y = spring - 18f; y > bottom + 8f; y -= 18f)
                BlockMesh.ChamferBox(s_Ruins.Pick(), foot + up * y, new Vector3(width + 1.6f, 0.8f, depth + 0.8f), rotation, 0.12f);
            if (onRock)
                BlockMesh.ChamferBox(s_Ruins.Pick(), foot + up * (rock + 1f), new Vector3(width + 4f, 10f, depth + 4f), rotation, 0.3f);

            if (!refuge || cut > 0f)
                return;
            float r = k_ReinoRefuge;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = foot + right * side * (width * 0.5f);
                Vector3 outward = right * side;
                // Contraforte oitavado do pilar até o pé, com a cornija em cima
                ConvexPrism(s_Ruins.Pick(), Octagon(c, f, r, (top.y - 0.35f + bottom) * 0.5f), up, top.y - 0.35f - bottom);
                ConvexPrism(s_Ruins.Pick(), Octagon(c, f, r + 0.5f, top.y - 0.8f), up, 0.6f);
                ConvexPrism(s_Ruins.Pick(), Octagon(c, f, r + 0.25f, top.y - 1.4f), up, 0.4f);
                if (spring < high - 1f && spring > bottom + 4f)
                    ConvexPrism(s_Ruins.Pick(), Octagon(c, f, r + 0.4f, spring - 0.45f), up, 0.9f);

                // Piso do refúgio (a metade de fora; a de dentro fica sob as lajes do tabuleiro)
                var outer = HalfOctagon(c, f, outward, r, top.y - 0.175f);
                var slab = new MeshData();
                ConvexPrism(slab, outer, up, 0.35f);
                Pave(slab);

                // Balaústres na volta do refúgio
                var ring = HalfOctagon(c, f, outward, r - 0.45f, top.y);
                for (int i = 0; i + 1 < ring.Length; i++)
                    KingRailRun(ring[i], ring[i + 1], true);

                // Estátua de guardião ou, alternando, coluna de lampiões e coluna-lanterna de luz lilás (kit AR_Tecno),
                // viradas para o tabuleiro
                Vector3 spot = new Vector3(c.x, top.y, c.z) + outward * (r * 0.42f);
                int slot = statue++;
                if (slot % 2 == 0)
                    PicosGothicKit.Place(s_Materials, "PR_Guardiao", spot, YawOf(-outward), props, true, ReinoFolder);
                else if (slot / 2 % 2 == 1)
                    PlaceTech("PR_Coluna_Bobinas", spot, YawOf(-outward), props);
                else
                    PicosGothicKit.Place(s_Materials, "PR_Candelabro", spot, YawOf(-outward), props, true, ReinoFolder);
            }
        }

        // Ponta do tabuleiro onde a ponte caiu: faixas de laje e de estrutura de comprimentos diferentes passando de 'end'
        // na direção 'f' (até 3,5 m), uma delas com uma laje pendurada presa na beira
        static void KingJagged(Vector3 end, Vector3 f, float width)
        {
            Vector3 up = Vector3.up, right = Vector3.Cross(up, f);
            const int strips = 6;
            int hanging = s_Random.Next(1, strips - 1);
            for (int i = 0; i < strips; i++)
            {
                float o = -width * 0.5f + width * (i + 0.5f) / strips, w = width / strips;
                float length = R(0.4f, 3.4f);
                if (i == hanging)
                    length = 3.4f;
                Quaternion rotation = Quaternion.LookRotation(f);
                Vector3 c = end + right * o + f * (length * 0.5f);
                var slab = new MeshData();
                BlockMesh.Box(slab, c - up * 0.175f, new Vector3(w + 0.02f, 0.35f, length), rotation);
                Pave(slab);
                float depth = (k_ReinoUnder - 0.35f) * R(0.5f, 1f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c - up * (0.35f + depth * 0.5f), new Vector3(w + 0.02f, depth, length), rotation, 0.06f);
                if (i == 0 || i == strips - 1)
                    BlockMesh.ChamferBox(s_Ruins.Pick(), c + right * Mathf.Sign(o) * (w * 0.5f + 0.25f) - up * 0.62f,
                        new Vector3(1.1f, 0.55f, length * R(0.4f, 0.9f)), rotation, 0.1f);
                if (i == hanging)
                {
                    // Laje pendurada: a ponta de cima encosta na beira de baixo da faixa
                    Vector3 hinge = end + right * o + f * length - up * k_ReinoUnder * 0.6f;
                    Vector3 dir = (f * Mathf.Cos(58f * Mathf.Deg2Rad) - up * Mathf.Sin(58f * Mathf.Deg2Rad)).normalized;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), hinge + dir * 2.1f, new Vector3(w * 1.4f, 0.9f, 4.2f),
                        Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, 0f, R(-10f, 10f)), 0.08f);
                }
            }
        }

        // Escombros em cima do tabuleiro: um monte de blocos perto de cada ponta partida (do lado de cá, cobertura junto do
        // desabamento) e, no trecho de lá, montes soltos e estátuas e colunas de lampiões tombadas
        static void BuildReinoRubble(Transform props, List<(float A, float B)> gaps)
        {
            Vector3 d = ReinoDir, right = ReinoRight, up = Vector3.up;
            float half = k_ReinoWidth * 0.5f;

            void Pile(float t, float o, int count, float spread)
            {
                Vector3 c = ReinoAt(t) + right * o;
                for (int k = 0; k < count; k++)
                {
                    float size = R(0.9f, 2.4f);
                    Vector3 p = c + d * R(-spread, spread) + right * R(-spread, spread) * 0.6f;
                    float o2 = Vector3.Dot(p - ReinoAt(0f), right);
                    if (Mathf.Abs(o2) > half - 1.4f)
                        continue;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), p + up * (size * R(0.25f, 0.4f) + (k > count * 0.6f ? size * 0.5f : 0f)),
                        new Vector3(size * R(1f, 1.9f), size * R(0.5f, 0.9f), size), Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)), 0.12f);
                }
            }

            foreach ((float a, float b) in gaps)
            {
                if (a > k_ReinoStraightEnd)
                    continue;
                Pile(a - R(6f, 10f), R(-half * 0.5f, half * 0.5f), 9, 3.5f);
                if (b < k_ReinoStraightEnd)
                    Pile(b + R(6f, 10f), R(-half * 0.5f, half * 0.5f), 9, 3.5f);
            }

            // Trecho de lá: montes soltos e peças tombadas, fora dos vãos
            int fallen = 0;
            for (float t = k_ReinoCollapseTo + 25f; t < k_ReinoStraightEnd - 15f; t += R(35f, 60f))
            {
                bool inGap = false;
                foreach ((float a, float b) in gaps)
                    inGap |= t > a - 8f && t < b + 8f;
                if (inGap)
                    continue;
                float o = R(-half * 0.55f, half * 0.55f);
                if (fallen < 5 && s_Random.NextDouble() < 0.45)
                {
                    GameObject go = PicosGothicKit.Place(s_Materials, fallen % 2 == 0 ? "PR_Guardiao" : "PR_Candelabro",
                        ReinoAt(t) + right * o + up * 0.7f, R(0f, 360f), props, true, ReinoFolder);
                    if (go)
                        go.transform.rotation = Quaternion.AngleAxis(R(80f, 88f), go.transform.right) * go.transform.rotation;
                    fallen++;
                    Pile(t + R(-4f, 4f), o, 4, 2.5f);
                }
                else
                    Pile(t, o, s_Random.Next(4, 9), 3f);
            }
        }

        // Curva para a esquerda no fim da reta: tabuleiro em trechos retos (sem rampa invisível: lá ninguém chega),
        // arcos pela corda a cada dois pontos e pilares; devolve o último ponto e a direção dele
        static Vector3 BuildReinoCurve(Transform props, out Vector3 endDir)
        {
            float w = k_ReinoWidth, step = k_ReinoCurveLength / k_ReinoCurveSteps, heading = k_ReinoAngle;
            var pts = new List<Vector3> { ReinoAt(k_ReinoStraightEnd) };
            Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));
            for (int k = 0; k < k_ReinoCurveSteps; k++)
            {
                heading += k_ReinoCurveTurn / k_ReinoCurveSteps;
                pts.Add(pts[pts.Count - 1] + Dir(heading) * step);
            }

            endDir = Dir(heading);
            int statue = 1;
            for (int i = 0; i + 2 < pts.Count; i += 2)
                KingSpan(pts[i], pts[i + 2], w, k_ReinoPier, 0f, 1f, true);
            for (int i = 0; i + 1 < pts.Count; i += 2)      // o último ponto encosta no tambor da área do fundo
            {
                Vector3 f = Xz(pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                KingPier(pts[i], f, w, k_ReinoPier, step * 2f, 0f, i % 4 == 2, props, ref statue);
            }

            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 f = Xz(pts[i + 1] - pts[i]).normalized, right = Vector3.Cross(Vector3.up, f);
                KingDeck(pts[i] - f * 0.6f, pts[i + 1] + f * 0.6f, w, false, false);
                foreach (float side in new[] { -1f, 1f })
                    KingRailRun(pts[i] + right * side * (w * 0.5f - 0.35f), pts[i + 1] + right * side * (w * 0.5f - 0.35f), false);
            }

            return pts[pts.Count - 1];
        }

        // Picos do gerador na frente da curva, para a área do fundo ficar escondida de quem olha da ponte; posições no
        // eixo (t, metros para a esquerda), longe do tabuleiro da curva
        static void BuildReinoHiders()
        {
            foreach ((float t, float left, float size, float top, PicosMountainKit.Kind kind) in new[]
                     {
                         (800f, 95f, 32f, 150f, PicosMountainKit.Kind.Tower), (965f, 15f, 30f, 130f, PicosMountainKit.Kind.Cluster),
                         (935f, 170f, 38f, 175f, PicosMountainKit.Kind.Column), (850f, -60f, 26f, 110f, PicosMountainKit.Kind.Broken),
                     })
            {
                Vector3 p = ReinoAt(t) - ReinoRight * left;
                PicosMountainKit.Mountain(s_MountainFar, kind, new Vector3(p.x, top, p.z), size, s_Random, 1);
                Clear(p, size + 20f);
            }
        }

        // ---------- bastião e portal ----------

        // Quina 'k' do octógono do bastião: uma face virada para cada lado da ponte (normais em k_ReinoAngle + k x 45°)
        static Vector3 BastionCorner(int k)
        {
            float a = (k_ReinoAngle + (k + 0.5f) * 45f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // Prisma oitavado do bastião (como o Prism, girado para as faces ficarem de frente para a ponte)
        static void BastionPrism(MeshData mesh, Vector3 center, float radiusTop, float radiusBottom, float yTop, float yBottom, bool caps)
        {
            for (int k = 0; k < 8; k++)
            {
                Vector3 at = center + BastionCorner(k - 1) * radiusTop, bt = center + BastionCorner(k) * radiusTop;
                Vector3 ab = center + BastionCorner(k - 1) * radiusBottom, bb = center + BastionCorner(k) * radiusBottom;
                at.y = bt.y = yTop;
                ab.y = bb.y = yBottom;
                Vector3 normal = Xz(BastionCorner(k - 1) + BastionCorner(k)).normalized;
                Vector3 slope = normal + Vector3.up * ((radiusTop - radiusBottom) / Mathf.Max(0.01f, yTop - yBottom));
                mesh.AddQuadAuto(at, bt, bb, ab, slope.normalized, 4f);
                if (!caps)
                    continue;
                mesh.AddTriangleAuto(new Vector3(center.x, yTop, center.z), at, bt, Vector3.up, 4f);
                mesh.AddTriangleAuto(new Vector3(center.x, yBottom, center.z), ab, bb, Vector3.down, 4f);
            }
        }

        static void BuildReinoBastion(Transform props, Vector3 d)
        {
            RuinMesh m = s_Ruins;
            Vector3 c = ReinoBastion, up = Vector3.up;
            float D = c.y, r = k_ReinoBastionRadius, inr = ReinoBastionInradius;

            var slab = new MeshData();
            BastionPrism(slab, c, r, r, D - 0.03f, D - 0.45f, true);
            Pave(slab);
            s_Platforms.Add((c, r));
            BastionPrism(m.Pick(), c, r + 1.2f, r + 1.2f, D - 0.45f, D - 1.15f, true);
            BastionPrism(m.Pick(), c, r - 0.4f, r - 0.4f, D - 1.15f, D - 28f, false);
            BastionPrism(m.Pick(), c, r + 0.5f, r + 0.5f, D - 28f, D - 29.4f, false);
            BastionPrism(m.Pick(), c, r, r + 5f, D - 29.4f, D - 72f, false);
            BastionPrism(m.Pick(), c, r + 6f, r + 6f, D - 72f, D - 73.6f, false);
            BastionPrism(m.Pick(), c, r + 5f, r + 10f, D - 73.6f, k_CloudY - 70f, false);

            for (int k = 0; k < 8; k++)
            {
                Vector3 v0 = c + BastionCorner(k - 1) * r, v1 = c + BastionCorner(k) * r;
                Vector3 normal = Xz((v0 + v1) * 0.5f - c).normalized;
                Quaternion facing = Quaternion.LookRotation(normal);
                float faceLength = Vector3.Distance(v0, v1);

                // Mísulas sob a cornija e seteiras escuras em dois níveis
                int corbels = Mathf.RoundToInt(faceLength / 2.4f);
                for (int i = 1; i < corbels; i++)
                {
                    Vector3 q = Vector3.Lerp(v0, v1, i / (float)corbels) - normal * 0.1f;
                    BlockMesh.ChamferBox(m.Pick(), new Vector3(q.x, D - 1.75f, q.z), new Vector3(0.7f, 1.2f, 1.2f), facing, 0.06f);
                }

                foreach (float y in new[] { D - 9f, D - 19f })
                foreach (float t in new[] { 0.3f, 0.7f })
                {
                    Vector3 q = Vector3.Lerp(v0, v1, t) - normal * 0.38f;
                    BlockMesh.Box(m.Dark, new Vector3(q.x, y, q.z), new Vector3(0.7f, 3.4f, 0.1f), facing);
                }

                // Contraforte em degraus em cada quina
                Vector3 corner = Xz(c + BastionCorner(k) * r);
                Quaternion diag = Quaternion.LookRotation(BastionCorner(k));
                float y0 = D - 4f;
                foreach ((float w, float h) in new[] { (5f, 30f), (6.5f, 45f), (8f, 200f) })
                {
                    float y1 = Mathf.Max(y0 - h, k_CloudY - 70f);
                    BlockMesh.ChamferBox(m.Pick(), corner + BastionCorner(k) * (w * 0.2f) + up * ((y0 + y1) * 0.5f), new Vector3(w, y0 - y1, w), diag, 0.2f);
                    y0 = y1;
                    if (y0 <= k_CloudY - 70f)
                        break;
                }

                // Parapeito com ameias (aberto para a ponte dos dois lados)
                bool bridge = Vector3.Angle(normal, d) < 10f || Vector3.Angle(normal, -d) < 10f;
                float open = bridge ? 0.68f : 0f;
                var walls = new List<(float, float)>();
                if (open > 0f)
                {
                    walls.Add((0f, (1f - open) * 0.5f));
                    walls.Add(((1f + open) * 0.5f, 1f));
                }
                else
                    walls.Add((0f, 1f));
                foreach ((float t0, float t1) in walls)
                {
                    Vector3 a = Vector3.Lerp(v0, v1, t0) - normal * 0.55f, b = Vector3.Lerp(v0, v1, t1) - normal * 0.55f;
                    float length = Vector3.Distance(a, b);
                    BlockMesh.ChamferBox(m.Pick(), Xz((a + b) * 0.5f) + up * (D + 0.65f), new Vector3(0.9f, 1.3f, length), Quaternion.LookRotation(b - a), 0.08f);
                    int merlons = Mathf.Max(1, Mathf.RoundToInt(length / 3f));
                    for (int i = 0; i < merlons; i++)
                    {
                        Vector3 q = Xz(Vector3.Lerp(a, b, (i + 0.5f) / merlons));
                        // Ameias derrubadas pelo cerco nas faces do lado da cidade (PicosReinoSiege)
                        if (BastionMerlonFallen(m, k, i, q, normal, (b - a).normalized, D))
                            continue;
                        BlockMesh.ChamferBox(m.Pick(), q + up * (D + 2f), new Vector3(0.9f, 1.4f, 1.5f), Quaternion.LookRotation(b - a), 0.06f);
                    }
                }
            }

            // Portal do Reino de frente para quem chega da Vila da Guarda (a frente com os guardiões dentro do piso). O
            // estrado dele tem 0,8 m (de 14,2 m na frente a 15 m atrás da origem, 32 m de largura, passagem de 16 m):
            // degraus nas duas pontas, com a rampa invisível
            Vector3 portal = c - d * 6f;
            PicosGothicKit.Place(s_Materials, "AR_Portal_do_Reino", portal, YawOf(d), props, true, ReinoFolder);
            Stairs(portal + d * 16.7f, portal + d * 14.2f + up * 0.8f, 15.6f);
            Stairs(portal - d * 17.5f, portal - d * 15f + up * 0.8f, 15.6f);

            // Nas duas bocas da ponte: colunas de lampiões e dois obeliscos
            Vector3 right = Vector3.Cross(up, d);
            foreach (float dir in new[] { 1f, -1f })
            {
                Vector3 f = d * dir;
                float from = dir > 0f ? 20f : 26f;
                foreach (float s in new[] { from, from + 11f })
                foreach (float side in new[] { -1f, 1f })
                    PicosGothicKit.Place(s_Materials, "PR_Candelabro", c + f * s + right * side * 13f, YawOf(-right * side), props, true, ReinoFolder);
                foreach (float side in new[] { -1f, 1f })
                    PicosGothicKit.Place(s_Materials, "PR_Obelisco", c + f * (inr - 5f) + right * side * 16.5f, YawOf(f), props, true);
            }

            Snowfall(c + up * 30f);
        }

        // ---------- rotatória e área do fundo ----------

        // Tambor redondo: piso, cornija, corpo até as nuvens com dentículos, arcada cega e faixas, contrafortes longe das
        // pontes e balaústres na volta, abertos para as pontes ('openings': direções delas a partir do centro)
        static void KingDrum(Vector3 c, float r, Vector3[] openings)
        {
            RuinMesh m = s_Ruins;
            Vector3 up = Vector3.up;
            float D = c.y;
            const int sides = 32;

            var slab = new MeshData();
            Prism(slab, c, r, r, sides, D - 0.03f, D - 0.45f, true);
            Pave(slab);
            s_Platforms.Add((c, r));
            Prism(m.Pick(), c, r + 0.9f, r + 0.9f, sides, D - 0.45f, D - 1.1f, true);
            Prism(m.Pick(), c, r - 0.2f, r - 0.2f, sides, D - 1.1f, D - 17f, false);
            Prism(m.Pick(), c, r + 0.6f, r + 0.6f, sides, D - 17f, D - 18.2f, false);
            Prism(m.Pick(), c, r - 1.2f, r + 3f, sides, D - 18.2f, k_CloudY - 60f, false);
            for (float y = D - 40f; y > k_CloudY - 40f; y -= 22f)
            {
                float t = Mathf.InverseLerp(D - 18.2f, k_CloudY - 60f, y);
                float rr = Mathf.Lerp(r - 1.2f, r + 3f, t) + 0.5f;
                Prism(m.Pick(), c, rr, rr, sides, y + 0.6f, y - 0.6f, false);
            }

            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                BlockMesh.Box(m.Pick(), Xz(c) + dir * (r - 0.05f) + up * (D - 1.45f), new Vector3(0.6f, 0.4f, 0.5f), Quaternion.LookRotation(dir));
                if (i % 3 == 0)
                    BlockMesh.Box(m.Dark, Xz(c) + dir * (r - 0.15f) + up * (D - 9f), new Vector3(1.5f, 5.5f, 0.1f), Quaternion.LookRotation(dir));
            }

            bool NearOpening(Vector3 dir, float degrees)
            {
                foreach (Vector3 o in openings)
                    if (Vector3.Angle(dir, o) < degrees)
                        return true;
                return false;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, (i + 0.5f) * 45f, 0f) * Vector3.forward;
                if (NearOpening(dir, 28f))
                    continue;
                BlockMesh.ChamferBox(m.Pick(), Xz(c) + dir * (r + 1.5f) + up * ((D - 19f + k_CloudY - 60f) * 0.5f),
                    new Vector3(5f, D - 19f - (k_CloudY - 60f), 6f), Quaternion.LookRotation(dir), 0.2f);
            }

            float rail = r - 0.6f, gap = Mathf.Asin((k_ReinoWidth * 0.5f - 0.35f) / rail) * Mathf.Rad2Deg;
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Xz(c) + Corner(i - 1, sides) * rail + up * D, b = Xz(c) + Corner(i, sides) * rail + up * D;
                if (NearOpening(Xz((a + b) * 0.5f - c).normalized, gap))
                    continue;
                KingRailRun(a, b, true);
            }
        }

        // Rotatória: o tambor com o monumento do relógio no meio (de frente para o bastião) e colunas de lampiões na volta
        // (o trilho de levitação dá a volta no monumento e as cápsulas param nas pontes vizinhas: BuildReinoTrack)
        static void BuildReinoRotunda(Transform props, Vector3 d)
        {
            Vector3 c = ReinoRotunda, up = Vector3.up;
            float r = k_ReinoRotundaRadius;
            KingDrum(c, r, new[] { d, -d });
            PicosGothicKit.Place(s_Materials, "AR_Monumento_Rotunda", c, YawOf(d), props, true, ReinoFolder);
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f + 22.5f, 0f) * Vector3.forward;
                if (Vector3.Angle(dir, d) < 30f || Vector3.Angle(dir, -d) < 30f)
                    continue;
                PicosGothicKit.Place(s_Materials, "PR_Candelabro", c + dir * (r - 4f), YawOf(-dir), props, true, ReinoFolder);
            }

            // O comboio de carroças e as barricadas que ficavam aqui deram lugar às cápsulas paradas no trilho de
            // levitação (BuildReinoTrack); os sorteios delas continuam gastos para o resto do mapa não mudar
            for (int k = 0; k < 40; k++)
                s_Random.NextDouble();

            Snowfall(c + up * 30f);
        }

        // ---------- Vila da Guarda ----------

        // A vila nova (pedido do usuário em 2026-11-02: substitui o platô do antigo portão, um pouco maior): num pico
        // próprio que sai das nuvens entre o platô e a ponte. Do platô (a chegada do zigue-zague; fica só o piso, o
        // parapeito e duas colunas de lampiões) uma ponte plana leva à vila; da vila, outra ponte desce 4 m até a conexão
        // na Ponte do Reino. Na vila: um arco com sino em cada chegada (o alarme do posto), a torre de vigia, o alojamento
        // e casas do kit pela beira (o lado virado para o bastião fica aberto, só com o parapeito), fonte congelada,
        // barracas, postes e as cargas do posto
        static void BuildReinoVillage(Node gate, List<Vector3> gateExits)
        {
            var root = new GameObject("Vila_da_Guarda").transform;
            root.SetParent(s_World, false);
            Node v = ReinoVillage;
            s_ReinoGate = gate.Center;
            Vector3 up = Vector3.up, right = ReinoRight;
            string kit = PicosGothicKit.VillagesFolder;

            // Pico: um pilar de topo plano até as nuvens e três colunas mais baixas encostadas (fora das duas pontes)
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = v.Center + Vector3.down * 0.5f, TopRadius = v.Radius + 4f, BaseRadius = (v.Radius + 4f) * 1.45f, Taper = 3f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 40, RingStep = 3f, Corners = 7, Flutes = 5, FluteDepth = 0.05f,
                BedHeight = R(10f, 15f), BedShift = 0.05f, Waist = 0.05f, Roughness = 0.05f, Seed = 2311, Bundle = false,
            });
            Vector3 toGate = Flat(gate.Center - v.Center);
            foreach ((float deg, float drop, float radius) in new[] { (100f, 9f, 15f), (200f, 17f, 19f), (285f, 7f, 12f) })
            {
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * toGate;
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = v.Center + dir * (v.Radius + 1f) + Vector3.down * drop, TopRadius = radius, BaseRadius = radius * 1.5f,
                    Taper = 3f, Cap = PicosMountainKit.Cap.Crag, CapHeight = R(2f, 4f), Sides = 28, RingStep = 3f, Corners = 6,
                    BedHeight = R(9f, 14f), Seed = 2312 + deg, Bundle = false,
                });
            }

            VillageFloor(v);

            // Ponte do platô até a vila (plana) e da vila até a Ponte do Reino (rampa de 4 m)
            var villageExits = new List<Vector3>();
            Vector3 toVillage = Flat(v.Center - gate.Center);
            ReinoRoad("Ponte_Plato_Vila", "Portao", "VilaDaGuarda", gate.Center + toVillage * (gate.Radius - 3f),
                v.Center - toVillage * (v.Radius - 3f), 7f, 3f, 3f,
                q => Inside(q, gate) || Inside(q, v), gateExits, villageExits);
            Vector3 join = ReinoJoin;
            float half = k_ReinoWidth * 0.5f;
            ReinoRoad("Ponte_Vila_Reino", "VilaDaGuarda", "PonteReino", v.Center + right * (v.Radius - 3f),
                join - right * (half - 2f), 8f, 3f, 4f,
                q =>
                {
                    if (Inside(q, v))
                        return true;
                    Vector3 local = Xz(q - ReinoAt(0f));
                    float t = Vector3.Dot(local, ReinoDir);
                    return Mathf.Abs(Vector3.Dot(local, right)) < half - 0.05f && t > k_ReinoBastionT && t < k_ReinoCollapseFrom;
                }, villageExits, new List<Vector3>());

            // Cabeceira no platô: parapeito aberto na chegada do zigue-zague e na ponte, duas colunas de lampiões
            KitParapet(root, gate, gateExits);
            foreach (float side in new[] { -1f, 1f })
                PicosGothicKit.Place(s_Materials, "PR_Candelabro", gate.Center + toVillage * (gate.Radius - 3f) +
                                                                   Vector3.Cross(up, toVillage) * side * 5.5f, YawOf(-toVillage), root, true, ReinoFolder);
            BuildMonumentSquare(root, gate, gateExits, toVillage);     // Praça do Monumento ao Reino (PicosMonumentSquare)

            // Arco com sino em cada chegada, atravessado pelo caminho, e colunas de lampiões dos lados
            KitParapet(root, v, villageExits);
            var avoid = new List<Vector3>();
            foreach (Vector3 g in villageExits)
            {
                Vector3 inward = Flat(v.Center - g);
                Vector3 p = new Vector3(g.x, v.Center.y, g.z) + inward * 1f;
                PicosGothicKit.Place(s_Materials, "PR_Arco_Sino", p, YawOf(inward), root, true, kit);
                avoid.Add(p);
                foreach (float side in new[] { -1f, 1f })
                    PicosGothicKit.Place(s_Materials, "PR_Candelabro", p + inward * 3f + Vector3.Cross(up, inward) * side * 5f, YawOf(inward),
                        root, true, ReinoFolder);
            }

            // Casas pela beira: a torre de vigia e o alojamento primeiro, depois casas do kit
            Vector3 view = Flat(ReinoBastion - v.Center);
            var models = new[]
            {
                ("AR_Casa_Torre", 8.4f, 8.4f), ("AR_Casa_Larga", 12.6f, 9f), ("AR_Casa_Empena", 8.6f, 10.6f),
                ("AR_Casa_Esquina", 11.2f, 10.2f), ("AR_Casa_Terraco", 10.2f, 9.7f), ("AR_Casa_Larga", 12.6f, 9f),
                ("AR_Casa_Empena", 8.6f, 10.6f), ("AR_Casa_Torre", 8.4f, 8.4f), ("AR_Casa_Terraco", 10.2f, 9.7f),
                ("AR_Casa_Esquina", 11.2f, 10.2f), ("AR_Casa_Empena", 8.6f, 10.6f), ("AR_Casa_Larga", 12.6f, 9f),
            };
            List<HouseSpot> houses = KitHouseRing(root, v, view, 50f, villageExits, avoid.ToArray(), 7f, 14, models);

            // Praça: fonte, barracas, postes e as cargas do posto, fora dos caminhos das chegadas
            PicosGothicKit.Place(s_Materials, "PR_Fonte_Congelada", v.Center, YawOf(view), root, true, kit);
            var gateAngles = new List<float>();
            foreach (Vector3 g in villageExits)
                gateAngles.Add(Mathf.Atan2(g.x - v.Center.x, g.z - v.Center.z));
            float viewAngle = Mathf.Atan2(view.x, view.z);
            foreach ((float deg, float distance, string model) in new[]
                     {
                         (60f, 16f, "PR_Barraca"), (-70f, 15f, "PR_Barraca"), (150f, 18f, "PR_Barraca"), (25f, 24f, "PR_Poste"),
                         (-25f, 24f, "PR_Poste"), (100f, 27f, "PR_Poste"), (-100f, 27f, "PR_Poste"), (165f, 24f, "PR_Poste"),
                         (-160f, 24f, "PR_Poste"), (125f, 26f, "PR_Cargas_A"), (-125f, 26f, "PR_Cargas_B"), (-40f, 29f, "PR_Carroca"),
                     })
            {
                float angle = viewAngle + deg * Mathf.Deg2Rad;
                bool blocked = false;
                foreach (float g in gateAngles)
                    blocked |= AngleGap(angle, g) < 0.3f;
                if (blocked)
                    continue;
                Vector3 p = v.Center + Around(angle) * distance;
                bool kitModel = model == "PR_Barraca";
                VillagePiece(model, p, YawOf(-Around(angle)) + R(-10f, 10f), root, kitModel ? kit : PicosGothicKit.Folder);
            }

            // Tecnologia: uma agulha ao lado de uma chegada, e um tríptico (o relicário saiu: ficava na frente de uma casa)
            VillageTech(root, v, "Vila da Guarda", villageExits, view, 50f, 1, false, 0, 1);
            AttackDamage(root, v, villageExits, houses, "Vila da Guarda", 9601, 3);

            Clear(v.Center, v.Radius + 45f);
            ClearLine(gate.Center, v.Center);
            ClearLine(v.Center, join);
            Snowfall(v.Center + up * 30f);
            Snowfall(gate.Center + up * 30f);
        }

        static bool Inside(Vector3 q, Node n) =>
            Vector2.Distance(new Vector2(q.x, q.z), new Vector2(n.Center.x, n.Center.z)) < n.Radius - 0.3f;

        // Caminho construído (PicosRoad) reto de 'a' a 'b' (no piso): plano nos primeiros 'flatA' e nos últimos 'flatB'
        // metros, rampa calçada no meio; pontes em arcos onde não há rocha embaixo
        static void ReinoRoad(string name, string start, string end, Vector3 a, Vector3 b, float width, float flatA, float flatB,
            System.Func<Vector3, bool> inside, List<Vector3> startExits, List<Vector3> endExits)
        {
            Vector3 dir = Flat(b - a);
            float length = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int count = Mathf.Max(2, Mathf.CeilToInt(length));
            var points = new List<float>();
            var dirs = new List<float>();
            var kinds = new System.Text.StringBuilder();
            bool slope = Mathf.Abs(b.y - a.y) > 0.05f;
            for (int i = 0; i <= count; i++)
            {
                float s = length * i / count;
                float h = s <= flatA ? a.y : s >= length - flatB ? b.y : Mathf.Lerp(a.y, b.y, (s - flatA) / (length - flatA - flatB));
                Vector3 q = a + dir * s;
                points.Add(q.x - k_High.x);
                points.Add(h - k_High.y);
                points.Add(q.z - k_High.z);
                dirs.Add(dir.x);
                dirs.Add(dir.z);
                kinds.Append(slope && s >= flatA - 1e-3f && s < length - flatB - 1e-3f ? 'r' : 'l');
            }

            var road = new HighRoad
            {
                name = name, start = start, end = end, width = width, points = points.ToArray(), dirs = dirs.ToArray(),
                kinds = kinds.ToString(), gaps = new int[kinds.Length], walls = new int[kinds.Length],
            };
            BuildRoad(road, inside, startExits, endExits);
        }

        // Sala 23, Vila da Guarda: a vila inteira; como as outras salas dos Picos, sem inimigos por enquanto. Chamada depois
        // das ruínas terem colisão (o NavMesh e os spawns precisam delas)
        static void BuildGuardVillageRoom()
        {
            if (!s_ReinoBuilt)
                return;
            s_ReinoBuilt = false;
            Node v = ReinoVillage;
            float D = v.Center.y, r = v.Radius;
            var bounds = new Bounds(v.Center + Vector3.up * 14f, new Vector3(r * 2f, 32f, r * 2f));
            Vector3 entrance = v.Center + Flat(s_ReinoGate - v.Center) * (r - 2f);
            BuildRoom("Arena_VilaDaGuarda", "Vila da Guarda", 23, v.Center, bounds, entrance, D - 0.6f, D + 1.6f);
        }

        // ---------- utilidades ----------

        // Altura da rocha abaixo de 'p' (raio para baixo); 'fallback' se não achar nada acima da nuvem
        static float RockBelow(Vector3 p, float fallback) =>
            Physics.Raycast(p, Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > k_CloudY - 30f
                ? hit.point.y
                : fallback;

        // Prisma de seção convexa 'poly' (pontos num plano, em volta), com 'length' ao longo de 'axis', centrado no plano
        static void ConvexPrism(MeshData mesh, IList<Vector3> poly, Vector3 axis, float length)
        {
            axis = axis.normalized;
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in poly)
                c += p;
            c /= poly.Count;
            Vector3 h = axis * (length * 0.5f);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % poly.Count];
                Vector3 outward = Vector3.ProjectOnPlane((a + b) * 0.5f - c, axis);
                mesh.AddQuadAuto(a - h, b - h, b + h, a + h, outward, BlockMesh.Tile);
                mesh.AddTriangleAuto(c - h, a - h, b - h, -axis, BlockMesh.Tile);
                mesh.AddTriangleAuto(c + h, a + h, b + h, axis, BlockMesh.Tile);
            }
        }

        // Octógono deitado de raio 'r' (até as quinas) em volta de 'c', com uma face virada para 'f', na altura 'y'
        static Vector3[] Octagon(Vector3 c, Vector3 f, float r, float y)
        {
            Vector3 right = Vector3.Cross(Vector3.up, f);
            var pts = new Vector3[8];
            for (int k = 0; k < 8; k++)
            {
                float a = (22.5f + k * 45f) * Mathf.Deg2Rad;
                pts[k] = new Vector3(c.x, y, c.z) + (f * Mathf.Cos(a) + right * Mathf.Sin(a)) * r;
            }

            return pts;
        }

        // A metade de fora do octógono (do lado 'outward'), começando e terminando na linha do meio
        static Vector3[] HalfOctagon(Vector3 c, Vector3 f, Vector3 outward, float r, float y)
        {
            Vector3 along = Vector3.Cross(outward, Vector3.up).normalized;      // perpendicular a 'outward', no plano
            float inr = r * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            var center = new Vector3(c.x, y, c.z);
            var pts = new List<Vector3> { center - along * inr };
            foreach (float deg in new[] { -67.5f, -22.5f, 22.5f, 67.5f })
            {
                float a = deg * Mathf.Deg2Rad;
                pts.Add(center + (outward * Mathf.Cos(a) + along * Mathf.Sin(a)) * r);
            }

            pts.Add(center + along * inr);
            return pts.ToArray();
        }
    }
}
