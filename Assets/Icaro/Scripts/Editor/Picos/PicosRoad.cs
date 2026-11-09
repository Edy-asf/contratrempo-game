using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Caminhos das Vilas do Alto: do Terraço do Portal até a Vila Escalonada e, depois, entre todas as vilas até o portão
    // da cidade (os trechos retos soltos de antes ficavam bugados e com pedaços faltando). Pedido do usuário: uma
    // construção só, calçada do começo ao fim, com balaústres dos dois lados como os da escadaria da costa do Arquipélago; nada de trechos
    // andando na rocha nem de escadas de tamanhos diferentes encaixadas umas nas outras. A planta vem do macico_vilas.py
    // (design_road): pontos a cada 1 m no máximo com a altura do piso, a direção e o tipo do trecho que começa em cada um
    // (degrau, patamar ou rampa), e se ele fica num vão. Aqui: o piso de lajes (degraus, patamares e rampas, pelos pisos
    // com neve), o muro de arrimo de cantaria até a rocha (em talude, com cornija e pilastras), pontes em arcos nos vãos
    // (pilares até a rocha ou até as nuvens), os balaústres e uma rampa invisível rente às quinas dos degraus.
    public static partial class PicosBuilder
    {
        [System.Serializable]
        class HighRoad
        {
            public string name, start, end;     // vilas da saída e da chegada
            public float width;
            public float[] points;      // (L, A, N) de cada ponto
            public float[] dirs;        // (L, N): direção do caminho em cada ponto
            public string kinds;        // trecho que começa no ponto: 's' degrau, 'l' patamar, 'r' rampa
            public int[] gaps;          // 1 = no vão (ponte em arcos)
            public int[] walls;         // lado do paredão: 1 = à esquerda, 2 = à direita (sem mureta nem muro de arrimo)
        }

        // Plataforma do zigue-zague da Vila Pendurada (as voltas entre os lances): centro do piso (L, A, N), tamanho (L, N)
        // e as aberturas nos balaústres em trincas (lado 0 N, 1 S, 2 L, 3 O; de; até, ao longo do lado, em L ou N)
        [System.Serializable]
        class HighPlatform
        {
            public string name;
            public float[] center;
            public float[] size;
            public float[] openings;
        }

        const float k_RoadTread = 0.35f;        // espessura das lajes do piso
        const float k_RoadPierEvery = 13f;      // vão máximo de um arco (de pilar a pilar)
        const float k_RoadPier = 2.4f;          // largura dos pilares, ao longo do caminho
        const float k_RoadDeck = 1.4f;          // do piso até o alto dos arcos
        const float k_RoadWallSkirt = 2.5f;     // do lado do paredão, a laje desce isto (encaixa na prateleira da rocha)
        const float k_RoadWallDefault = 2f;     // quanto a laje avança para o paredão se o raio não acha a rocha

        // 'inside': o ponto fica em cima do piso de uma das pontas (vila, torre do Terraço do Portal ou plataforma)
        static void BuildRoad(HighRoad road, System.Func<Vector3, bool> inside, List<Vector3> terraceExits,
            List<Vector3> villageExits)
        {
            // Pontos, com 2 m a mais nas duas pontas, por dentro das duas vilas (sem fresta na beira delas)
            var p = new List<Vector3>();
            var dir = new List<Vector3>();
            var kind = new List<char>();
            var gap = new List<bool>();
            var sideWall = new List<int>();
            for (int i = 0; i < road.kinds.Length; i++)
            {
                p.Add(k_High + new Vector3(road.points[i * 3], road.points[i * 3 + 1], road.points[i * 3 + 2]));
                dir.Add(new Vector3(road.dirs[i * 2], 0f, road.dirs[i * 2 + 1]).normalized);
                kind.Add(road.kinds[i]);
                gap.Add(road.gaps[i] != 0);
                sideWall.Add(road.walls != null && i < road.walls.Length ? road.walls[i] : 0);
            }

            p.Insert(0, p[0] - dir[0] * 2f);
            dir.Insert(0, dir[0]);
            kind.Insert(0, 'l');
            gap.Insert(0, false);
            sideWall.Insert(0, sideWall[0]);
            p.Add(p[p.Count - 1] + dir[dir.Count - 1] * 2f);
            dir.Add(dir[dir.Count - 1]);
            kind.Add('l');
            gap.Add(false);
            sideWall.Add(sideWall[sideWall.Count - 1]);
            terraceExits.Add(p[1]);
            villageExits.Add(p[p.Count - 2]);

            int count = p.Count;
            float half = road.width * 0.5f;
            var s = new float[count];
            var right = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                right[i] = new Vector3(dir[i].z, 0f, -dir[i].x);
                if (i > 0)
                    s[i] = s[i - 1] + Vector2.Distance(new Vector2(p[i - 1].x, p[i - 1].z), new Vector2(p[i].x, p[i].z));
            }

            // Altura do piso no fim de cada trecho e a linha das quinas (a mais alta das duas lajes em cada ponto: por
            // ela vão os balaústres e a rampa invisível)
            float EndHeight(int i) => kind[i] == 'r' ? p[i + 1].y : p[i].y;
            var edge = new float[count];
            edge[0] = p[0].y;
            for (int i = 1; i < count; i++)
                edge[i] = Mathf.Max(EndHeight(i - 1), p[i].y);

            int Locate(float at)
            {
                int lo = 0, hi = count - 2;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (s[mid] <= at)
                        lo = mid;
                    else
                        hi = mid - 1;
                }

                return lo;
            }

            float T(int i, float at) => s[i + 1] > s[i] ? Mathf.Clamp01((at - s[i]) / (s[i + 1] - s[i])) : 0f;

            Vector3 PointAt(float at)
            {
                int i = Locate(at);
                Vector3 q = Vector3.Lerp(p[i], p[i + 1], T(i, at));
                q.y = Mathf.Lerp(edge[i], edge[i + 1], T(i, at));
                return q;
            }

            Vector3 RightAt(float at)
            {
                int i = Locate(at);
                return Vector3.Lerp(right[i], right[i + 1], T(i, at)).normalized;
            }

            // Rocha embaixo (o mais baixo dos três pontos na largura do piso)
            float Ground(Vector3 at, Vector3 side)
            {
                float best = float.NaN;
                foreach (float o in new[] { -half + 0.5f, 0f, half - 0.5f })
                    if (Physics.Raycast(at + side * o + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 160f, ~0,
                            QueryTriggerInteraction.Ignore))
                        best = float.IsNaN(best) ? hit.point.y : Mathf.Min(best, hit.point.y);
                return best;
            }

            // A rocha de verdade embaixo de cada ponto: onde some ou fica a mais de 14 m do piso, também é vão (o relevo da
            // planta é o das colunas antes da escultura); trechos de muro curtos entre dois vãos viram ponte, e vãos
            // curtos demais para um arco ficam de muro
            // Lances da escadaria da Vila Pendurada: maciços de cantaria até o chão, sem arcos nem talude (pedido do usuário
            // em 2026-11-08: toda de pedra, como a escadaria da costa do Arquipélago)
            bool solid = road.name != null && road.name.StartsWith("Zz_Lance");
            var ground = new float[count];
            for (int i = 0; i < count; i++)
            {
                ground[i] = Ground(p[i], right[i]);
                if (solid)
                    gap[i] = false;
                else if (i > 0 && i + 1 < count && (float.IsNaN(ground[i]) || p[i].y - ground[i] > 14f))
                    gap[i] = true;
            }

            void Runs(bool value, float shorter, bool replace)
            {
                for (int i = 0; i < count; i++)
                {
                    if (gap[i] != value || (i > 0 && gap[i - 1] == value))
                        continue;
                    int j = i;
                    while (j + 1 < count && gap[j + 1] == value)
                        j++;
                    bool inside = i > 0 && j + 1 < count;
                    if (inside && s[j] - s[i] < shorter)
                        for (int k = i; k <= j; k++)
                            gap[k] = replace;
                    i = j;
                }
            }

            if (!solid)
            {
                Runs(false, 4f, true);
                Runs(true, 5f, false);
            }

            // Lado do paredão (pedido do usuário em 2026-11-01: o caminho fica colado à parede da montanha, sem mureta
            // desse lado): a laje avança até entrar 0,4 m na rocha (raio para o lado, 1 m acima do piso) e desce até a
            // prateleira; sem muro de arrimo, cornija, pilastras nem balaústres desse lado. No vão (ponte) não vale
            bool Wall(int i, float side) => !gap[i] && (sideWall[i] & (side < 0f ? 1 : 2)) != 0;
            bool SegWall(int i, float side) => Wall(i, side) && Wall(i + 1, side);
            var reach = new float[count, 2];
            for (int i = 0; i < count; i++)
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    if (!Wall(i, side))
                        continue;
                    // Escadaria da Vila Pendurada: do lado norte fica o lance de cima, colado; a laje para na face dele
                    // (no primeiro lance o raio acha o paredão)
                    if (solid && road.name != "Zz_Lance_1")
                    {
                        reach[i, k] = 0f;
                        continue;
                    }
                    Vector3 from = Xz(p[i]) + Vector3.up * (p[i].y + 1f);
                    reach[i, k] = Physics.Raycast(from, right[i] * side, out RaycastHit wallHit, half + 6f, ~0,
                        QueryTriggerInteraction.Ignore)
                        ? Mathf.Clamp(wallHit.distance - half + 0.4f, 0.2f, 5f)
                        : k_RoadWallDefault;
                }

            // Pontes: cada trecho seguido no vão vira arcos iguais entre pilares (os das pontas encostam na rocha)
            var arches = new List<(float From, float To, float Radius)>();
            var piers = new List<float>();
            for (int i = 0; i < count; i++)
            {
                if (!gap[i] || (i > 0 && gap[i - 1]))
                    continue;
                int j = i;
                while (j + 1 < count && gap[j + 1])
                    j++;
                float length = s[j] - s[i];
                int spans = Mathf.Max(1, Mathf.CeilToInt(length / k_RoadPierEvery));
                float span = length / spans;
                for (int k = 0; k <= spans; k++)
                    piers.Add(s[i] + span * k);
                for (int k = 0; k < spans; k++)
                    arches.Add((s[i] + span * k + k_RoadPier * 0.5f, s[i] + span * (k + 1) - k_RoadPier * 0.5f,
                        (span - k_RoadPier) * 0.5f));
            }

            // Fundo do muro em cada ponto: na rocha (1,5 m para dentro dela) ou, no vão, o arco (no pilar, o alto dele)
            float Bottom(int i)
            {
                if (gap[i])
                {
                    foreach ((float from, float to, float radius) in arches)
                    {
                        float spring = edge[i] - k_RoadDeck - radius;
                        if (s[i] >= from && s[i] <= to)
                        {
                            float x = s[i] - (from + to) * 0.5f;
                            return spring + Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
                        }
                    }

                    float r = arches.Count > 0 ? arches[0].Radius : 3f;
                    foreach ((float from, float to, float radius) in arches)
                        if (s[i] >= from - k_RoadPier - 0.01f && s[i] <= to + k_RoadPier + 0.01f)
                            r = radius;
                    return edge[i] - k_RoadDeck - r;
                }

                return float.IsNaN(ground[i]) ? p[i].y - 20f : ground[i] - 1.5f;
            }

            var bottom = new float[count];
            for (int i = 0; i < count; i++)
                bottom[i] = Bottom(i);

            // Em cima do piso de uma das pontas (as lajes do caminho ficam 3 cm abaixo dele, sem piscar)
            bool Inside(Vector3 q) => inside(q);

            MeshData wall = s_Ruins.Tones[0];       // muros, cornija, arcos e pilares num tom só
            var deck = new MeshData();              // lajes do piso (vão para os pisos com neve)
            var ramp = new MeshData();
            var root = new GameObject("Caminho_" + (string.IsNullOrEmpty(road.name) ? "Terraco_Escalonada" : road.name)).transform;
            root.SetParent(s_World, false);

            Vector2 SideUv(float along, float y) => new Vector2(along, y) / BlockMesh.Tile;
            Vector2 TopUv(Vector3 q, float tile) => new Vector2(q.x, q.z) / tile;

            void Face(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
                Vector3 facing)
            {
                Tri(m, a, b, c, ua, ub, uc, facing);
                Tri(m, a, c, d, ua, uc, ud, facing);
            }

            void SideFace(MeshData m, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float sa, float sb, Vector3 facing) =>
                Face(m, a0, a1, b1, b0, SideUv(sa, a0.y), SideUv(sa, a1.y), SideUv(sb, b1.y), SideUv(sb, b0.y), facing);

            for (int i = 0; i + 1 < count; i++)
            {
                Vector3 pa = p[i], pb = p[i + 1];
                Vector3 ra = right[i], rb = right[i + 1];
                float ta = p[i].y, tb = EndHeight(i);
                if (Inside(pa) || Inside(pb))
                {
                    ta -= 0.03f;
                    tb -= 0.03f;
                }

                float sa = s[i], sb = s[i + 1];
                Vector3 forward = dir[i];

                // Laje do piso: topo, lados e as duas pontas (os espelhos dos degraus); do lado do paredão ela vai até a
                // rocha e o lado desce até a prateleira
                bool wallL = SegWall(i, -1f), wallR = SegWall(i, 1f);
                float hlA = half + (wallL ? reach[i, 0] : 0f), hlB = half + (wallL ? reach[i + 1, 0] : 0f);
                float hrA = half + (wallR ? reach[i, 1] : 0f), hrB = half + (wallR ? reach[i + 1, 1] : 0f);
                Vector3 la = Xz(pa) - ra * hlA + Vector3.up * ta, ra2 = Xz(pa) + ra * hrA + Vector3.up * ta;
                Vector3 lb = Xz(pb) - rb * hlB + Vector3.up * tb, rb2 = Xz(pb) + rb * hrB + Vector3.up * tb;
                Vector3 down = Vector3.down * k_RoadTread;
                Vector3 downL = wallL ? Vector3.down * k_RoadWallSkirt : down;
                Vector3 downR = wallR ? Vector3.down * k_RoadWallSkirt : down;
                Face(deck, la, lb, rb2, ra2, TopUv(la, 8f), TopUv(lb, 8f), TopUv(rb2, 8f), TopUv(ra2, 8f), Vector3.up);
                SideFace(deck, la + downL, la, lb + downL, lb, sa, sb, -ra);
                SideFace(deck, ra2 + downR, ra2, rb2 + downR, rb2, sa, sb, ra);
                Face(deck, la + down, la, ra2, ra2 + down, SideUv(0f, la.y - k_RoadTread), SideUv(0f, la.y),
                    SideUv(road.width, ra2.y), SideUv(road.width, ra2.y - k_RoadTread), -forward);
                Face(deck, lb + down, lb, rb2, rb2 + down, SideUv(0f, lb.y - k_RoadTread), SideUv(0f, lb.y),
                    SideUv(road.width, rb2.y), SideUv(road.width, rb2.y - k_RoadTread), dir[i + 1]);

                // Muro de arrimo (em talude) ou, no vão, os tímpanos dos arcos até o intradorso
                float topA = ta - k_RoadTread + 0.05f, topB = tb - k_RoadTread + 0.05f;
                float botA = Mathf.Min(bottom[i], topA - 0.8f), botB = Mathf.Min(bottom[i + 1], topB - 0.8f);
                float batterA = gap[i] || solid ? 0f : Mathf.Min(1.2f, 0.06f * (topA - botA));
                float batterB = gap[i + 1] || solid ? 0f : Mathf.Min(1.2f, 0.06f * (topB - botB));
                foreach (float side in new[] { -1f, 1f })
                {
                    if (side < 0f ? wallL : wallR)
                        continue;
                    Vector3 a1 = Xz(pa) + ra * side * half + Vector3.up * topA;
                    Vector3 a0 = Xz(pa) + ra * side * (half + batterA) + Vector3.up * botA;
                    Vector3 b1 = Xz(pb) + rb * side * half + Vector3.up * topB;
                    Vector3 b0 = Xz(pb) + rb * side * (half + batterB) + Vector3.up * botB;
                    SideFace(wall, a0, a1, b0, b1, sa, sb, ra * side);

                    // Cornija logo abaixo do piso
                    Vector3 ca = Xz(pa) + ra * side * (half - 0.05f), cb = Xz(pb) + rb * side * (half - 0.05f);
                    Vector3 oa = Xz(pa) + ra * side * (half + 0.22f), ob = Xz(pb) + rb * side * (half + 0.22f);
                    float ya = topA - 0.05f, yb = topB - 0.05f;
                    SideFace(wall, oa + Vector3.up * (ya - 0.35f), oa + Vector3.up * ya, ob + Vector3.up * (yb - 0.35f),
                        ob + Vector3.up * yb, sa, sb, ra * side);
                    Face(wall, ca + Vector3.up * ya, oa + Vector3.up * ya, ob + Vector3.up * yb, cb + Vector3.up * yb,
                        TopUv(ca, 2f), TopUv(oa, 2f), TopUv(ob, 2f), TopUv(cb, 2f), Vector3.up);
                    Face(wall, ca + Vector3.up * (ya - 0.35f), oa + Vector3.up * (ya - 0.35f), ob + Vector3.up * (yb - 0.35f),
                        cb + Vector3.up * (yb - 0.35f), TopUv(ca, 2f), TopUv(oa, 2f), TopUv(ob, 2f), TopUv(cb, 2f),
                        Vector3.down);

                    // Arquivolta: faixa saliente acompanhando o arco, nas duas faces
                    if (gap[i] && gap[i + 1] && botA < topA - 1.2f && botB < topB - 1.2f && InArch(arches, sa, sb))
                    {
                        Vector3 ia = Xz(pa) + ra * side * (half + 0.12f), ib = Xz(pb) + rb * side * (half + 0.12f);
                        Vector3 ja = Xz(pa) + ra * side * half, jb = Xz(pb) + rb * side * half;
                        SideFace(wall, ia + Vector3.up * botA, ia + Vector3.up * (botA + 0.7f), ib + Vector3.up * botB,
                            ib + Vector3.up * (botB + 0.7f), sa, sb, ra * side);
                        Face(wall, ja + Vector3.up * botA, ia + Vector3.up * botA, ib + Vector3.up * botB, jb + Vector3.up * botB,
                            TopUv(ja, 2f), TopUv(ia, 2f), TopUv(ib, 2f), TopUv(jb, 2f), Vector3.down);
                        Face(wall, ja + Vector3.up * (botA + 0.7f), ia + Vector3.up * (botA + 0.7f),
                            ib + Vector3.up * (botB + 0.7f), jb + Vector3.up * (botB + 0.7f), TopUv(ja, 2f), TopUv(ia, 2f),
                            TopUv(ib, 2f), TopUv(jb, 2f), Vector3.up);
                    }
                }

                // Fundo (o intradorso dos arcos aparece por baixo) e as pontas do caminho
                Vector3 fa0 = Xz(pa) - ra * (half + batterA) + Vector3.up * botA, fa1 = Xz(pa) + ra * (half + batterA) + Vector3.up * botA;
                Vector3 fb0 = Xz(pb) - rb * (half + batterB) + Vector3.up * botB, fb1 = Xz(pb) + rb * (half + batterB) + Vector3.up * botB;
                Face(wall, fa0, fb0, fb1, fa1, TopUv(fa0, 2f), TopUv(fb0, 2f), TopUv(fb1, 2f), TopUv(fa1, 2f), Vector3.down);
                if (i == 0 || i + 2 == count)
                {
                    bool start = i == 0;
                    Vector3 c = start ? Xz(pa) : Xz(pb), r = start ? ra : rb;
                    float top = start ? topA : topB, bot = start ? botA : botB, batter = start ? batterA : batterB;
                    Face(wall, c - r * (half + batter) + Vector3.up * bot, c - r * half + Vector3.up * top,
                        c + r * half + Vector3.up * top, c + r * (half + batter) + Vector3.up * bot, SideUv(0f, bot), SideUv(0f, top),
                        SideUv(road.width, top), SideUv(road.width, bot), start ? -forward : dir[i + 1]);
                }

                // Rampa invisível rente às quinas (até a rocha do lado do paredão)
                Face(ramp, Xz(pa) - ra * hlA + Vector3.up * edge[i], Xz(pb) - rb * hlB + Vector3.up * edge[i + 1],
                    Xz(pb) + rb * hrB + Vector3.up * edge[i + 1], Xz(pa) + ra * hrA + Vector3.up * edge[i],
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            }

            // Pilares das pontes, com a imposta no alto, e pingentes no fecho dos arcos
            foreach (float at in piers)
            {
                Vector3 c = PointAt(at), r = RightAt(at);
                Quaternion yaw = Quaternion.LookRotation(Vector3.Cross(r, Vector3.up));
                float radius = arches.Count > 0 ? arches[0].Radius : 3f;
                foreach ((float from, float to, float rr) in arches)
                    if (at >= from - k_RoadPier - 0.01f && at <= to + k_RoadPier + 0.01f)
                        radius = rr;
                float top = c.y - k_RoadDeck - radius + 0.2f;
                float foot = Physics.Raycast(c + Vector3.down * 1f, Vector3.down, out RaycastHit hit, 400f, ~0,
                    QueryTriggerInteraction.Ignore) ? hit.point.y - 1f : k_CloudY - 40f;
                if (top - foot < 0.5f)
                    continue;
                BlockMesh.ChamferBox(wall, new Vector3(c.x, (top + foot) * 0.5f, c.z), new Vector3(road.width + 0.6f, top - foot, k_RoadPier),
                    yaw, 0.08f);
                BlockMesh.ChamferBox(wall, new Vector3(c.x, top - 0.1f, c.z), new Vector3(road.width + 1f, 0.45f, k_RoadPier + 0.5f), yaw,
                    0.06f);
            }

            foreach ((float from, float to, float radius) in arches)
            {
                Vector3 crown = PointAt((from + to) * 0.5f), r = RightAt((from + to) * 0.5f);
                for (int k = 0; k < 3; k++)
                    Spike(s_Ice, crown + Vector3.down * k_RoadDeck + r * R(-half * 0.8f, half * 0.8f), Vector3.down, R(0.5f, 2f),
                        R(0.1f, 0.25f), 5, 0f, 3f);
            }

            // Pilastras nos muros altos, a cada ~8 m
            for (float at = 4f; at < s[count - 1] - 4f; at += 8f)
            {
                int i = Locate(at);
                if (gap[i] || gap[i + 1])
                    continue;
                float top = Mathf.Min(p[i].y, p[i + 1].y) - k_RoadTread - 0.4f;
                float bot = Mathf.Min(bottom[i], bottom[i + 1]);
                if (top - bot < 3f)
                    continue;
                Vector3 c = PointAt(at), r = RightAt(at);
                Quaternion yaw = Quaternion.LookRotation(Vector3.Cross(r, Vector3.up));
                foreach (float side in new[] { -1f, 1f })
                    if (!Wall(i, side) && !Wall(i + 1, side))
                        BlockMesh.ChamferBox(wall, new Vector3(c.x, (top + bot) * 0.5f, c.z) + r * side * (half + 0.2f),
                            new Vector3(0.5f, top - bot, 0.9f), yaw, 0.06f);
            }

            // Balaústres dos dois lados (a cada ~2 m), fora de cima do terraço e da vila e do lado do paredão
            int posts = Mathf.Max(1, Mathf.CeilToInt(s[count - 1] / 2f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 last = Vector3.zero;
                bool open = false;
                for (int k = 0; k <= posts; k++)
                {
                    float at = s[count - 1] * k / posts;
                    Vector3 q = PointAt(at) + RightAt(at) * side * (half - 0.2f);
                    int seg = Locate(at);
                    if (Inside(q) || Wall(seg, side) || Wall(seg + 1, side))
                    {
                        open = false;
                        continue;
                    }

                    Quaternion yaw = Quaternion.LookRotation(Vector3.Cross(RightAt(at), Vector3.up));
                    BlockMesh.Box(s_Ruins.Pick(), q + Vector3.up * 0.4f, new Vector3(0.3f, 1.4f, 0.3f), yaw);
                    if (open)
                    {
                        Vector3 d = q - last;
                        Quaternion along = Quaternion.LookRotation(d);
                        Vector3 middle = (q + last) * 0.5f;
                        bool sloped = Mathf.Abs(d.y) > 0.01f;
                        BlockMesh.ChamferBox(s_Ruins.Pick(), middle + Vector3.up * 1.2f, new Vector3(0.5f, 0.25f, d.magnitude + 0.05f),
                            along, 0.05f);
                        BlockMesh.Box(wall, middle + Vector3.up * (sloped ? -0.1f : 0.1f),
                            new Vector3(0.6f, sloped ? 0.6f : 0.2f, d.magnitude + 0.05f), along);
                    }

                    last = q;
                    open = true;
                }
            }

            Pave(deck);
            GridSceneKit.CreateObject(k_Prefix + "_Caminho_Rampa_" + road.name, "Caminho_Rampa", ramp, null, root, true);
            for (int i = 0; i + 25 < count; i += 25)
                ClearLine(p[i], p[i + 25]);
            ClearLine(p[count - 26 > 0 ? count - 26 : 0], p[count - 1]);
        }

        // Plataforma retangular plana de uma volta do zigue-zague (pedido do usuário em 2026-10-31: como os patamares da
        // escadaria da costa do Arquipélago): piso de lajes, bloco de cantaria até a rocha com cornija, balaústres em
        // volta menos onde os lances (ou a calçada) chegam, e o piso invisível de colisão
        static void BuildPlatform(HighPlatform pl)
        {
            Vector3 c = HP(pl.center);
            float hx = pl.size[0] * 0.5f, hz = pl.size[1] * 0.5f, top = c.y;
            MeshData wall = s_Ruins.Tones[0];
            var deck = new MeshData();
            var ramp = new MeshData();
            var root = new GameObject("Plataforma_" + pl.name).transform;
            root.SetParent(s_World, false);

            // Rocha embaixo: o mais baixo de uma grade de raios
            float ground = float.NaN;
            for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 4; j++)
            {
                var q = new Vector3(c.x - hx + hx * i * 0.5f, top + 2f, c.z - hz + hz * j * 0.5f);
                if (Physics.Raycast(q, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
                    ground = float.IsNaN(ground) ? hit.point.y : Mathf.Min(ground, hit.point.y);
            }

            float under = top - k_RoadTread + 0.05f;
            float bottom = float.IsNaN(ground) ? top - 20f : Mathf.Min(ground - 1.5f, under - 0.8f);
            BlockMesh.ChamferBox(wall, new Vector3(c.x, (under + bottom) * 0.5f, c.z), new Vector3(hx * 2f, under - bottom, hz * 2f),
                Quaternion.identity, 0.08f);
            // Cornija logo abaixo do piso, em volta
            BlockMesh.ChamferBox(wall, new Vector3(c.x, under - 0.22f, c.z), new Vector3(hx * 2f + 0.44f, 0.35f, hz * 2f + 0.44f),
                Quaternion.identity, 0.05f);

            // Laje do piso (vai para os pisos com neve) e o piso de colisão
            Vector3 a = new Vector3(c.x - hx, top, c.z - hz), b = new Vector3(c.x + hx, top, c.z - hz);
            Vector3 d = new Vector3(c.x + hx, top, c.z + hz), e = new Vector3(c.x - hx, top, c.z + hz);
            Vector2 Uv(Vector3 q) => new Vector2(q.x, q.z) / 8f;
            Tri(deck, a, e, d, Uv(a), Uv(e), Uv(d), Vector3.up);
            Tri(deck, a, d, b, Uv(a), Uv(d), Uv(b), Vector3.up);
            Vector3 down = Vector3.down * k_RoadTread;
            Vector3[] ring = { a, b, d, e };
            for (int k = 0; k < 4; k++)
            {
                Vector3 p0 = ring[k], p1 = ring[(k + 1) % 4];
                Vector3 facing = Vector3.Cross(p1 - p0, Vector3.up).normalized;
                float along = Vector3.Distance(p0, p1);
                Vector2 u0 = new Vector2(0f, p0.y - k_RoadTread) / BlockMesh.Tile, u1 = new Vector2(0f, p0.y) / BlockMesh.Tile;
                Vector2 u2 = new Vector2(along, p1.y) / BlockMesh.Tile, u3 = new Vector2(along, p1.y - k_RoadTread) / BlockMesh.Tile;
                Tri(deck, p0 + down, p0, p1, u0, u1, u2, facing);
                Tri(deck, p0 + down, p1, p1 + down, u0, u2, u3, facing);
            }

            Tri(ramp, a, e, d, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            Tri(ramp, a, d, b, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);

            // Balaústres em volta (0,2 m para dentro da beira), a cada ~2 m, menos nas aberturas
            bool Open(int side, float along)
            {
                if (pl.openings == null)
                    return false;
                for (int k = 0; k + 2 < pl.openings.Length; k += 3)
                    if (Mathf.RoundToInt(pl.openings[k]) == side && along >= pl.openings[k + 1] - 0.05f &&
                        along <= pl.openings[k + 2] + 0.05f)
                        return true;
                return false;
            }

            float ix = hx - 0.2f, iz = hz - 0.2f;
            // Volta no sentido horário vista de cima: lado S (de oeste para leste), L, N e O
            var corners = new[]
            {
                (new Vector3(c.x - ix, top, c.z - iz), new Vector3(c.x + ix, top, c.z - iz), 1),
                (new Vector3(c.x + ix, top, c.z - iz), new Vector3(c.x + ix, top, c.z + iz), 2),
                (new Vector3(c.x + ix, top, c.z + iz), new Vector3(c.x - ix, top, c.z + iz), 0),
                (new Vector3(c.x - ix, top, c.z + iz), new Vector3(c.x - ix, top, c.z - iz), 3),
            };
            var posts = new List<(Vector3 At, bool Open)>();
            foreach ((Vector3 p0, Vector3 p1, int side) in corners)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p0, p1) / 2f));
                for (int k = 0; k < count; k++)
                {
                    Vector3 q = Vector3.Lerp(p0, p1, k / (float)count);
                    float along = side < 2 ? q.x - k_High.x : q.z - k_High.z;
                    bool open = Open(side, along) || (k == 0 && Open(side == 1 ? 3 : side == 2 ? 1 : side == 0 ? 2 : 0,
                        side == 1 || side == 0 ? q.z - k_High.z : q.x - k_High.x));
                    posts.Add((q, open));
                }
            }

            for (int k = 0; k < posts.Count; k++)
            {
                (Vector3 q, bool open) = posts[k];
                if (open)
                    continue;
                BlockMesh.Box(s_Ruins.Pick(), q + Vector3.up * 0.4f, new Vector3(0.3f, 1.4f, 0.3f), Quaternion.identity);
                (Vector3 next, bool nextOpen) = posts[(k + 1) % posts.Count];
                if (nextOpen)
                    continue;
                Vector3 span = next - q;
                Quaternion along = Quaternion.LookRotation(span);
                Vector3 middle = (q + next) * 0.5f;
                BlockMesh.ChamferBox(s_Ruins.Pick(), middle + Vector3.up * 1.2f, new Vector3(0.5f, 0.25f, span.magnitude + 0.05f), along,
                    0.05f);
                BlockMesh.Box(wall, middle + Vector3.up * 0.1f, new Vector3(0.6f, 0.2f, span.magnitude + 0.05f), along);
            }

            Pave(deck);
            GridSceneKit.CreateObject(k_Prefix + "_Plataforma_" + pl.name, "Plataforma_Piso", ramp, null, root, true);
            ClearLine(a, d);
        }

        // O ponto no chão do mundo (y = 0), sem normalizar (o Flat das Vilas normaliza)
        static Vector3 Xz(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // O trecho de 'a' a 'b' fica dentro de um arco (fora dos pilares)
        static bool InArch(List<(float From, float To, float Radius)> arches, float a, float b)
        {
            foreach ((float from, float to, float _) in arches)
                if (a >= from - 0.01f && b <= to + 0.01f)
                    return true;
            return false;
        }
    }
}
