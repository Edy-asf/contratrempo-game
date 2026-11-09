using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Tecnologia das Vilas do Alto (pedido do usuário em 2026-11-07; testado primeiro na Vila-Ponte): só peças soltas,
    // a arquitetura gótica fica como era (o metal encaixado nas casas e torres foi recusado: não casava com o gótico).
    // Torres de antena e máquinas paradas e congeladas nas praças (kit AR_Tecno no Blender: PR_Torre_Antena,
    // PR_Gerador, PR_Coluna_Bobinas, PR_Painel_Controle, PR_Celulas). PowerLine liga pontas de cabos (sem uso por enquanto)
    public static partial class PicosBuilder
    {
        // Pontas dos cabos nos mastros, no referencial de cada peça no Blender (x, y, z; frente -Y): as cruzetas ficam
        // atravessadas na linha, um isolador de cada lado
        static readonly Vector3[] k_AntennaWires = { new Vector3(-2.2f, 0f, 14.85f), new Vector3(2.2f, 0f, 14.85f) };

        // Ponto de uma peça do kit (coordenadas do Blender) no mundo: o x do Blender vira o -x local do Unity, o z vira
        // o y e o -y vira o z (a frente, -Y, fica para +Z)
        static Vector3 KitPoint(Vector3 position, float yaw, float scale, Vector3 blender) =>
            position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-blender.x, blender.z, -blender.y) * scale;

        static Vector3[] KitPoints(Vector3 position, float yaw, float scale, Vector3[] blender)
        {
            var points = new Vector3[blender.Length];
            for (int i = 0; i < blender.Length; i++)
                points[i] = KitPoint(position, yaw, scale, blender[i]);
            return points;
        }

        static GameObject PlaceTech(string model, Vector3 position, float yaw, Transform root, float scale = 1f, bool collider = true)
        {
            GameObject go = PicosGothicKit.Place(s_Materials, model, position, yaw, root, collider, PicosGothicKit.VillagesFolder);
            if (go)
                go.transform.localScale *= scale;
            return go;
        }

        // Cabos de energia de um mastro ao outro: cada ponta de 'a' liga à de 'b' do mesmo lado da linha, cedendo 3% do
        // vão no meio (catenária aproximada por parábola)
        // Enfeite de praça das Vilas do Alto: o poste de lampião antigo (PR_Poste) virou a coluna-lanterna do kit AR_Tecno
        // (pedido do usuário em 2026-11-07), nos mesmos lugares; o resto vai para o kit gótico
        static void VillagePiece(string model, Vector3 p, float yaw, Transform root, string folder)
        {
            if (model == "PR_Poste")
                PlaceTech("PR_Coluna_Bobinas", p, yaw, root);
            else
                PicosGothicKit.Place(s_Materials, model, p, yaw, root, true, folder);
        }

        static void PowerLine(Vector3[] a, Vector3[] b)
        {
            Vector3 mid = Vector3.zero;
            foreach (Vector3 p in a)
                mid += p / a.Length;
            Vector3 other = Vector3.zero;
            foreach (Vector3 p in b)
                other += p / b.Length;
            Vector3 side = Vector3.Cross(Vector3.up, Flat(other - mid));
            foreach (Vector3 p in a)
            {
                float best = float.MinValue;
                Vector3 q = b[0];
                foreach (Vector3 c in b)
                {
                    float score = -Mathf.Abs(Vector3.Dot(c - other, side) - Vector3.Dot(p - mid, side));
                    if (score > best)
                    {
                        best = score;
                        q = c;
                    }
                }

                Wire(p, q, Vector3.Distance(p, q) * 0.03f);
            }
        }

        static void Wire(Vector3 a, Vector3 b, float sag)
        {
            Vector3 At(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            int pieces = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / 2f), 6, 40);
            for (int i = 0; i < pieces; i++)
            {
                Vector3 p = At(i / (float)pieces), q = At((i + 1f) / pieces);
                Vector3 d = q - p;
                // Cada pedaço passa 2 cm do seguinte (sem fresta nas dobras)
                BlockMesh.Box(s_Alloy, (p + q) * 0.5f, new Vector3(0.07f, 0.07f, d.magnitude + 0.02f), Quaternion.LookRotation(d));
            }
        }

        // Chão livre para uma peça de 'size' (largura, altura, fundo) com o pé em 'foot' (a altura do pé é a do piso da
        // praça, que ainda não tem colisão: o raio de cima precisa achar a rocha ou outro chão até 3 m abaixo dele, e
        // nada mais alto que o piso, como um telhado) e a frente para 'yaw': nada encostando na caixa da peça (a partir
        // de 25 cm acima do piso); devolve o pé
        static Vector3? FreeSpot(Vector3 foot, Vector3 size, float yaw)
        {
            if (!Physics.Raycast(foot + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore) ||
                hit.point.y > foot.y + 0.3f || hit.point.y < foot.y - 3f)
                return null;
            Vector3 center = foot + Vector3.up * (0.25f + size.y * 0.5f);
            if (Physics.CheckBox(center, size * 0.5f, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore))
                return null;
            return foot;
        }

        // Piso de colisão provisório na altura da praça (o piso de verdade só ganha colisão no fim da geração, e o pico da
        // Vila da Guarda também), para os raios do FreeSpot; apagar depois
        static GameObject TempFloor(Node n)
        {
            var floor = new GameObject("Piso_Provisorio");
            floor.transform.position = n.Center + Vector3.down * 0.1f;
            floor.AddComponent<BoxCollider>().size = new Vector3(n.Radius * 2f, 0.2f, n.Radius * 2f);
            Physics.SyncTransforms();
            return floor;
        }

        static void DropTempFloor(GameObject floor)
        {
            Object.DestroyImmediate(floor);
            Physics.SyncTransforms();
        }

        // Peças de tecnologia de uma cabeceira da Vila-Ponte (praça redonda 'n' com o tabuleiro para 'toDeck'): a torre
        // de antena na beira, ao lado da abertura do parapeito (de frente para o meio da praça), e o relicário com o atril
        // e uma coluna-lanterna encostados numa parede (TechAgainstWalls). Devolve as pontas dos cabos da antena (ou null)
        static Vector3[] CabeceiraTech(Transform root, Node n, Vector3 toDeck, List<Vector3> gates)
        {
            GameObject floor = TempFloor(n);
            float deck = Mathf.Atan2(toDeck.x, toDeck.z);

            // Agulha de Ressonância (1,2x: ~27 m): dos dois lados da abertura, do mais perto dela para longe
            const float antennaScale = 1.2f;
            Vector3[] wires = null;
            for (float step = 50f; step <= 130f && wires == null; step += 6f)
            foreach (float sign in new[] { 1f, -1f })
            {
                float angle = deck + sign * step * Mathf.Deg2Rad;
                Vector3 foot = n.Center + Around(angle) * (n.Radius - 4.6f);
                float yaw = YawOf(Flat(n.Center - foot));
                Vector3? spot = FreeSpot(foot, new Vector3(6f, 8f, 6f), yaw);
                if (spot == null)
                    continue;
                PlaceTech("PR_Torre_Antena", spot.Value, yaw, root, antennaScale);
                wires = KitPoints(spot.Value, yaw, antennaScale, k_AntennaWires);
                Physics.SyncTransforms();
                break;
            }

            if (wires == null)
                Debug.LogWarning("[Ícaro] Vila-Ponte: a torre de antena de uma cabeceira não achou lugar.");

            var paths = new List<Vector3>(gates) { n.Center + toDeck * n.Radius };
            string missing = TechAgainstWalls(root, n, paths, true, 0, 0);
            DropTempFloor(floor);
            if (missing != null)
                Debug.LogWarning("[Ícaro] Vila-Ponte: numa cabeceira " + missing);
            return wires;
        }

        // Peças de tecnologia de uma praça de vila redonda 'n' (as casas, o parapeito e os enfeites já postos, com
        // colisão): 'antennas' Agulhas de Ressonância na beira, ao lado das chegadas ('gates') e fora da vista ('view',
        // meia abertura 'viewHalf' em graus; Vector3.zero = sem vista), e o relicário com o atril, as colunas-lanterna e
        // os trípticos encostados nas paredes das casas (TechAgainstWalls). Sem sorteio: o resto do mapa não muda
        static void VillageTech(Transform root, Node n, string name, List<Vector3> gates, Vector3 view, float viewHalf,
            int antennas, bool generator, int columns, int cells)
        {
            GameObject floor = TempFloor(n);
            float r = n.Radius;
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));
            bool hasView = view.sqrMagnitude > 0.01f;
            float viewAngle = hasView ? Mathf.Atan2(view.x, view.z) : 0f;

            // Agulhas (1,2x): na beira, primeiro ao lado das chegadas (de 16 a 70 graus delas), depois onde couber
            var tries = new List<float>();
            foreach (float g in gateAngles)
                for (float step = 16f; step <= 70f; step += 6f)
                    foreach (float sign in new[] { 1f, -1f })
                        tries.Add(g + sign * step * Mathf.Deg2Rad);
            float start = hasView ? viewAngle + Mathf.PI : gateAngles.Count > 0 ? gateAngles[0] + Mathf.PI : 0f;
            for (int k = 0; k < 72; k++)
                tries.Add(start + (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 5f * Mathf.Deg2Rad);
            int placedAntennas = 0;
            var antennaAngles = new List<float>();
            foreach (float angle in tries)
            {
                if (placedAntennas >= antennas)
                    break;
                bool nearGate = gateAngles.Exists(g => AngleGap(angle, g) < 12f * Mathf.Deg2Rad);
                if ((hasView && AngleGap(angle, viewAngle) < (viewHalf + 6f) * Mathf.Deg2Rad) || nearGate ||
                    antennaAngles.Exists(a => AngleGap(a, angle) < 50f * Mathf.Deg2Rad))
                    continue;
                Vector3 foot = n.Center + Around(angle) * (r - 4.6f);
                float yaw = YawOf(Flat(n.Center - foot));
                if (OnTechPath(n, gates, foot, 5.5f) || !(FreeSpot(foot, new Vector3(6f, 8f, 6f), yaw) is Vector3 spot))
                    continue;
                PlaceTech("PR_Torre_Antena", spot, yaw, root, 1.2f);
                Physics.SyncTransforms();
                antennaAngles.Add(angle);
                placedAntennas++;
            }

            string missing = TechAgainstWalls(root, n, gates, generator, columns, cells);
            DropTempFloor(floor);
            if (placedAntennas < antennas || missing != null)
                Debug.LogWarning($"[Ícaro] {name}: " + (placedAntennas < antennas ? $"agulhas {placedAntennas}/{antennas}; " : "") + (missing ?? ""));
        }

        static bool OnTechPath(Node n, List<Vector3> gates, Vector3 p, float clearance)
        {
            foreach (Vector3 g in gates)
                if (SegmentDistance(p, n.Center, new Vector3(g.x, n.Center.y, g.z)) < clearance)
                    return true;
            return false;
        }

        struct WallSpot
        {
            public Vector3 Point, Normal;     // na parede, na altura do piso; a normal para fora dela, no plano
            public float Lateral;             // 1 = parede de lado para o meio da praça (a lateral de uma casa)
        }

        // Paredes de verdade em volta da praça, achadas por raios na horizontal a 1,8 m do piso (acima dos parapeitos)
        // saindo de círculos pela praça: para fora e para os dois lados. As laterais das casas (de lado para o meio)
        // vêm primeiro, depois as fachadas
        static List<WallSpot> WallSpots(Node n)
        {
            var spots = new List<WallSpot>();
            Vector3 up = Vector3.up;
            for (float r = 4f; r < n.Radius - 1f; r += 2.5f)
            for (float deg = 0f; deg < 360f; deg += 3f)
            {
                Vector3 radial = Around(deg * Mathf.Deg2Rad), tangent = Vector3.Cross(up, radial);
                Vector3 origin = n.Center + radial * r + up * 1.8f;
                foreach (Vector3 dir in new[] { radial, tangent, -tangent })
                {
                    if (!Physics.Raycast(origin, dir, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y) > 0.15f)
                        continue;
                    Vector3 p = new Vector3(hit.point.x, n.Center.y, hit.point.z);
                    Vector3 fromCenter = Flat(p - n.Center);
                    if (Vector3.Distance(p, n.Center) > n.Radius - 0.5f || Vector3.Distance(p, n.Center) < 2f)
                        continue;
                    Vector3 normal = Flat(hit.normal);
                    if (spots.Exists(s => (s.Point - p).sqrMagnitude < 1.5f && Vector3.Dot(s.Normal, normal) > 0.9f))
                        continue;
                    spots.Add(new WallSpot { Point = p, Normal = normal, Lateral = 1f - Mathf.Abs(Vector3.Dot(normal, fromCenter)) });
                }
            }

            // Laterais primeiro (estável: na ordem em que foram achadas dentro de cada faixa)
            var ordered = new List<WallSpot>();
            foreach ((float low, float high) in new[] { (0.7f, 2f), (0.35f, 0.7f), (-1f, 0.35f) })
                foreach (WallSpot s in spots)
                    if (s.Lateral > low && s.Lateral <= high)
                        ordered.Add(s);
            return ordered;
        }

        // A parede em 'p' (normal 'normal') é lisa de 'half' metros para cada lado ao longo dela, de 1,8 a 3,6 m de altura:
        // raios de frente para ela batem todos à mesma distância (sem portas, janelas fundas, quinas nem contrafortes)
        static bool WallRun(Vector3 p, Vector3 normal, float half)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, normal);
            foreach ((float h, float tolerance) in new[] { (1f, 0.25f), (2.4f, 0.6f) })
                foreach (float s in new[] { -1f, 0f, 1f })
                {
                    Vector3 origin = p + tangent * (s * half) + normal * 1.2f + Vector3.up * h;
                    if (!Physics.Raycast(origin, -normal, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) ||
                        Mathf.Abs(hit.distance - 1.2f) > tolerance || Vector3.Dot(Flat(hit.normal), normal) < 0.85f)
                        return false;
                }

            return true;
        }

        // Peça encostada na parede: o pé a 'back' metros dela (o fundo da peça a 0,3 m) e a frente para fora. A caixa
        // conferida deixa de fora a faixa de 0,5 m junto da parede (o soco e o beiral das casas); devolve se coube
        static bool FitsAgainst(Vector3 foot, Vector3 normal, Vector3 size)
        {
            float yaw = YawOf(normal);
            return FreeSpot(foot + normal * 0.25f, new Vector3(size.x, size.y, size.z - 0.5f), yaw) != null;
        }

        // Relicário com o atril e as colunas-lanterna, e os trípticos, encostados nas paredes das casas e alinhados com
        // elas (pedido do usuário em 2026-11-07: soltos pela praça pareciam aleatórios): o relicário de lado na parede
        // (as rosáceas para os lados) com uma coluna em cada ponta, na mesma linha, e o atril na frente dele; as colunas
        // que sobram em par numa parede, e os trípticos de costas para uma parede. Nunca no caminho das chegadas, e cada
        // grupo a 8 m ou mais do outro. Devolve o que faltou (ou null)
        static string TechAgainstWalls(Transform root, Node n, List<Vector3> gates, bool generator, int columns, int cells)
        {
            List<WallSpot> spots = WallSpots(n);
            var groups = new List<Vector3>();
            Vector3 up = Vector3.up;
            var genSize = new Vector3(4.9f, 5.5f, 3.2f);
            var colSize = new Vector3(2f, 9f, 2f);
            var cellSize = new Vector3(2.9f, 3.6f, 2.2f);
            const float gap = 0.3f;

            int nearGroup = 0, onPath = 0, notFlat = 0, blocked = 0;
            bool Usable(WallSpot s, float half, float reach)
            {
                if (groups.Exists(g => Vector3.Distance(g, s.Point) < 8f + half))
                {
                    nearGroup++;
                    return false;
                }

                Vector3 side = Vector3.Cross(up, s.Normal) * half;
                if (OnTechPath(n, gates, s.Point + s.Normal * reach, 3.5f) || OnTechPath(n, gates, s.Point + s.Normal * reach + side, 2f) ||
                    OnTechPath(n, gates, s.Point + s.Normal * reach - side, 2f))
                {
                    onPath++;
                    return false;
                }

                if (!WallRun(s.Point, s.Normal, half))
                {
                    notFlat++;
                    return false;
                }

                return true;
            }

            // Relicário (o comprido ao longo da parede), as colunas nas pontas e o atril na frente
            bool generatorDone = !generator;
            int flank = generator ? Mathf.Min(columns, 2) : 0;
            foreach (WallSpot s in spots)
            {
                if (generatorDone)
                    break;
                Vector3 tangent = Vector3.Cross(up, s.Normal);
                float colAt = 2.45f + 0.25f + 1f;
                float half = flank > 0 ? colAt + 0.9f : 2.2f;
                if (!Usable(s, half, 3f))
                    continue;
                Vector3 gen = s.Point + s.Normal * (genSize.z * 0.5f - 0.1f + gap);
                // A caixa conferida para no ombro do telhado (4,4 m): só a cumeeira e as bobinas passam disso, e os beirais
                // das casas ficam acima
                if (!FitsAgainst(gen, s.Normal, new Vector3(genSize.x, 4.2f, genSize.z)))
                {
                    blocked++;
                    continue;
                }
                var cols = new List<Vector3>();
                foreach (float side in flank == 2 ? new[] { 1f, -1f } : flank == 1 ? new[] { 1f } : new float[0])
                    cols.Add(s.Point + tangent * (side * colAt) + s.Normal * (colSize.z * 0.5f + gap));
                if (!cols.TrueForAll(c => FitsAgainst(c, s.Normal, colSize)))
                    continue;
                float yaw = YawOf(s.Normal);
                PlaceTech("PR_Gerador", gen, yaw, root);
                foreach (Vector3 c in cols)
                    PlaceTech("PR_Coluna_Bobinas", c, yaw, root);
                Physics.SyncTransforms();
                // Atril 1,4 m na frente do relicário, com a tela para a praça (quem mexe fica de frente para o relicário)
                Vector3 panel = gen + s.Normal * (genSize.z * 0.5f + 1.4f);
                if (!OnTechPath(n, gates, panel, 2f) && FreeSpot(panel, new Vector3(1.4f, 1.8f, 1.4f), yaw) is Vector3 pf)
                {
                    PlaceTech("PR_Painel_Controle", pf, yaw, root);
                    Physics.SyncTransforms();
                }

                groups.Add(s.Point);
                generatorDone = true;
            }

            // Colunas que sobram: em par numa parede lisa (6 m entre elas) ou, a última, sozinha
            int placedColumns = generatorDone && generator ? flank : 0;
            foreach (WallSpot s in spots)
            {
                int left = columns - placedColumns;
                if (left <= 0)
                    break;
                int pair = left >= 2 ? 2 : 1;
                float half = pair == 2 ? 3.9f : 1f;
                if (!Usable(s, half, colSize.z))
                    continue;
                Vector3 tangent = Vector3.Cross(up, s.Normal);
                var cols = new List<Vector3>();
                foreach (float side in pair == 2 ? new[] { 1f, -1f } : new[] { 0f })
                    cols.Add(s.Point + tangent * (side * 3f) + s.Normal * (colSize.z * 0.5f + gap));
                if (!cols.TrueForAll(c => FitsAgainst(c, s.Normal, colSize)))
                    continue;
                foreach (Vector3 c in cols)
                    PlaceTech("PR_Coluna_Bobinas", c, YawOf(s.Normal), root);
                Physics.SyncTransforms();
                groups.Add(s.Point);
                placedColumns += pair;
            }

            // Trípticos de costas para a parede (o fundo do retábulo fica a 0,5 m da origem, a célula caída na frente)
            int placedCells = 0;
            foreach (WallSpot s in spots)
            {
                if (placedCells >= cells)
                    break;
                if (!Usable(s, 1.6f, 1.5f))
                    continue;
                Vector3 foot = s.Point + s.Normal * (0.5f + gap);
                if (FreeSpot(foot + s.Normal * 0.55f, new Vector3(cellSize.x, cellSize.y, cellSize.z - 0.4f), YawOf(s.Normal)) == null)
                    continue;
                PlaceTech("PR_Celulas", foot, YawOf(s.Normal), root);
                Physics.SyncTransforms();
                groups.Add(s.Point);
                placedCells++;
            }

            if (generatorDone && placedColumns >= columns && placedCells >= cells)
                return null;
            return $"peças encostadas nas paredes que não acharam lugar: relicário {(generatorDone ? 1 : 0)}/{(generator ? 1 : 0)}, " +
                   $"colunas {placedColumns}/{columns}, células {placedCells}/{cells} ({spots.Count} trechos de parede; recusados: " +
                   $"{nearGroup} perto de outra peça, {onPath} no caminho, {notFlat} parede irregular, {blocked} sem espaço).";
        }
    }
}
