using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Estaleiro Celeste (era a Vila das Engrenagens; pedido do usuário em 2026-11-08, depois de recusar a Vila Suspensa,
    // que não fazia sentido num ombro pequeno): a vila que construía naves. O elemento grande fica fora da área de jogo:
    // uma nave inacabada (VH_Nave_Estaleiro, kit AR_Estaleiro) atracada sobre o vazio, presa pelos braços de uma doca de
    // liga que se ergue na beira da praça (PR_Doca_Nave), com a passarela da beira até a escotilha e um guindaste de doca
    // (PR_Guindaste_Doca) segurando um painel do casco sobre a proa. A nave e as máquinas são futuristas (casco de
    // painéis, liga, vidro escuro, luz lilás), sem pedra nem gótico; a arquitetura da praça continua gótica: galpões de
    // casco, depósito de costelas e casas do mestre pela beira de trás, a torre de controle junto da doca e a estação do
    // teleférico virada para a torre do relógio. A beira escolhida para a doca é a que tem mais vazio embaixo (raios na
    // rocha), perto da vista da cidade, longe das saídas. Sorteio próprio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        const float k_DockInset = 0.5f;          // a origem da doca fica no parapeito (raio da vila - 0,5)
        const float k_DockHalfArc = 31f;         // meia abertura (graus) do trecho da beira tomado pela doca e o guindaste

        // Casas da beira de trás, na ordem em que entram (largura e fundo com folga)
        static readonly (string Model, float W, float D)[] k_ShipyardHouses =
        {
            ("AR_Galpao_Casco", 12.8f, 16.8f), ("AR_Casa_Mestre", 8.8f, 9.8f), ("PR_Deposito_Costelas", 15.6f, 6.8f),
            ("AR_Casa_Mestre", 8.8f, 9.8f), ("AR_Galpao_Casco", 12.8f, 16.8f),
        };

        static Vector3 BuildShipyardVillage(Node n, Vector3 city, List<Vector3> exits, Vector3 clockPeak)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3601);
            Vector3 f = Flat(city - n.Center);
            var root = new GameObject("Vila_Estaleiro").transform;
            root.SetParent(s_World, false);
            float floor = n.Center.y;
            Vector3 c = new Vector3(n.Center.x, floor, n.Center.z);
            float view = Mathf.Atan2(f.x, f.z);
            float AngleOf(Vector3 p) => Mathf.Atan2(p.x - c.x, p.z - c.z);

            var gates = new List<Vector3>();
            foreach (Vector3 g in exits)
                if (gates.TrueForAll(other => Vector3.Distance(other, g) > 1f))
                    gates.Add(g);
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(AngleOf(g));

            // A beira da doca: perto da vista da cidade, longe das saídas e com o vazio mais fundo embaixo da nave
            Physics.SyncTransforms();
            float dockRadius = n.Radius - k_DockInset;
            float dock = view, bestScore = float.MinValue;
            for (float deg = -84f; deg <= 84f; deg += 4f)
            {
                float a = view + deg * Mathf.Deg2Rad;
                bool ok = true;
                foreach (float g in gateAngles)
                    ok &= AngleGap(a, g) > (k_DockHalfArc + 14f) * Mathf.Deg2Rad;
                if (!ok)
                    continue;
                Vector3 origin = c + Around(a) * dockRadius;
                float yaw = YawOf(c - origin), clearance = float.MaxValue;
                foreach (float bx in new[] { -20f, -10f, 0f, 10f, 20f })
                foreach (float by in new[] { 7f, 12.5f, 18f })
                {
                    Vector3 p = KitPoint(origin, yaw, 1f, new Vector3(bx, by, 0f));
                    float depth = 60f;
                    if (Physics.Raycast(p + Vector3.down * 12f, Vector3.up, 24f, ~0, QueryTriggerInteraction.Ignore))
                        depth = -10f;                                   // rocha subindo na altura da nave
                    else if (Physics.Raycast(p + Vector3.up * 11f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                        depth = floor - hit.point.y;
                    clearance = Mathf.Min(clearance, depth);
                }

                float score = Mathf.Min(clearance, 30f) - Mathf.Abs(deg) * 0.08f;
                if (score > bestScore)
                {
                    bestScore = score;
                    dock = a;
                }
            }

            if (bestScore < 10f)
                Debug.LogWarning($"Estaleiro: a beira da doca tem só {bestScore:0.#} m de vazio embaixo da nave (pede 10)");
            Vector3 dockOrigin = c + Around(dock) * dockRadius;
            float dockYaw = YawOf(c - dockOrigin);
            PlaceTech("VH_Nave_Estaleiro", dockOrigin, dockYaw, root);
            PlaceTech("PR_Doca_Nave", dockOrigin, dockYaw, root);
            PlaceTech("PR_Guindaste_Doca", dockOrigin, dockYaw, root);
            Vector3 gangway = KitPoint(dockOrigin, dockYaw, 1f, new Vector3(6f, -0.5f, 0f));

            // Parapeito: aberto nas saídas e só na largura da passarela (o resto da beira da doca fica fechado; os
            // braços passam por cima)
            {
                float radius = n.Radius - 0.5f;
                int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / 4f));
                float walk = AngleOf(gangway);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + 0.5f) * Mathf.PI * 2f / count;
                    bool gap = AngleGap(angle, walk) < 2.6f / radius;
                    foreach (float g in gateAngles)
                        gap |= AngleGap(angle, g) < 5f / radius;
                    if (!gap)
                        PicosGothicKit.Place(s_Materials, "PR_Parapeito", c + Around(angle) * radius, YawOf(Around(angle)), root, true,
                            PicosGothicKit.VillagesFolder);
                }
            }

            // Estação do teleférico na beira virada para a torre do relógio, longe da doca e das saídas
            float station = AngleOf(c + Flat(clockPeak - c));
            for (int tries = 0; tries < 24; tries++)
            {
                bool ok = AngleGap(station, dock) > (k_DockHalfArc + 14f) * Mathf.Deg2Rad;
                foreach (float g in gateAngles)
                    ok &= AngleGap(station, g) > 24f * Mathf.Deg2Rad;
                if (ok)
                    break;
                float away = Mathf.Sign(Mathf.DeltaAngle(dock * Mathf.Rad2Deg, station * Mathf.Rad2Deg) + 0.01f);
                station += away * 5f * Mathf.Deg2Rad;
            }

            Vector3 stationAt = c + Around(station) * (n.Radius - 5f);
            float stationYaw = YawOf(Flat(clockPeak - stationAt));
            PlaceTech("PR_Estacao_Suspensa", stationAt, stationYaw, root);
            Vector3 cableStart = KitPoint(stationAt, stationYaw, 1f, new Vector3(0f, -4.4f, 6.45f));

            // Torre de controle junto da popa da nave, dentro da praça
            Vector3 tower = KitPoint(dockOrigin, dockYaw, 1f, new Vector3(22f, -12f, 0f));
            bool towerOk = Vector3.Distance(Flat(tower - c), Vector3.zero) < n.Radius - 4.5f;
            foreach (Vector3 g in gates)
                towerOk &= SegmentDistance(tower, c, g) > 7f;
            if (towerOk)
                PlaceTech("AR_Torre_Controle", tower, YawOf(Flat(KitPoint(dockOrigin, dockYaw, 1f, new Vector3(4f, 12.5f, 0f)) - tower)), root);

            // Galpões, depósito e casas pela beira de trás (fora do trecho da doca, longe da estação e da torre)
            var avoid = new List<Vector3> { stationAt };
            if (towerOk)
                avoid.Add(tower);
            List<HouseSpot> houses = KitHouseRing(root, n, Around(dock), k_DockHalfArc + 8f, gates, avoid.ToArray(), 6f,
                k_ShipyardHouses.Length, k_ShipyardHouses);

            var taken = new List<(Vector3 At, float Radius)> { (stationAt, 6f) };
            if (towerOk)
                taken.Add((tower, 5f));
            bool Free(Vector3 p, float clear)
            {
                float r = Vector3.Distance(Flat(p - c), Vector3.zero);
                if (r > n.Radius - 2f - clear)
                    return false;
                if (AngleGap(AngleOf(p), dock) < (k_DockHalfArc + 4f) * Mathf.Deg2Rad && r > n.Radius - 16f)
                    return false;
                foreach (Vector3 g in gates)
                    if (SegmentDistance(p, c, g) < 3.5f + clear)
                        return false;
                foreach (HouseSpot h in houses)
                    if (Vector3.Distance(Flat(h.Foot - p), Vector3.zero) < Mathf.Max(h.W, h.D) * 0.5f + 1f + clear)
                        return false;
                foreach ((Vector3 at, float radius) in taken)
                    if (Vector3.Distance(Flat(at - p), Vector3.zero) < radius + clear)
                        return false;
                return true;
            }

            // Na frente dos galpões, um trenó de painéis e uma pilha; mais pilhas perto da doca
            foreach (HouseSpot h in houses)
            {
                if (h.Model != "AR_Galpao_Casco")
                    continue;
                Vector3 front = h.Rotation * Vector3.forward, side = h.Rotation * Vector3.right;
                Vector3 sled = h.Foot + front * (h.D * 0.5f + 3.2f) + side * 2.6f;
                if (Free(sled, 2.4f))
                {
                    PlaceTech("PR_Treno_Paineis", sled, YawOf(front) + 90f + R(-15f, 15f), root);
                    taken.Add((sled, 2.8f));
                }

                Vector3 stack = h.Foot + front * (h.D * 0.5f + 2.4f) - side * 4.4f;
                if (Free(stack, 1.8f))
                {
                    PlaceTech("PR_Pilha_Paineis", stack, YawOf(front) + R(-20f, 20f), root);
                    taken.Add((stack, 2f));
                }
            }

            foreach (float bx in new[] { -8f, 15f })
            {
                Vector3 p = KitPoint(dockOrigin, dockYaw, 1f, new Vector3(bx, -10f, 0f));
                bool blocked = false;
                foreach (Vector3 g in gates)
                    blocked |= SegmentDistance(p, c, g) < 4f;
                foreach ((Vector3 at, float radius) in taken)
                    blocked |= Vector3.Distance(Flat(at - p), Vector3.zero) < radius + 1.8f;
                if (blocked)
                    continue;
                PlaceTech("PR_Pilha_Paineis", p, dockYaw + R(-25f, 25f), root);
                taken.Add((p, 2f));
            }

            // Colunas-lanterna pela praça
            foreach (float deg in new[] { 40f, -40f, 100f, -100f, 160f, -160f })
            {
                Vector3 p = c + Around(dock + deg * Mathf.Deg2Rad) * 24f;
                if (!Free(p, 1.2f))
                    continue;
                VillagePiece("PR_Poste", p, YawOf(c - p), root, PicosGothicKit.Folder);
                taken.Add((p, 1.5f));
            }

            s_Random = saved;
            return cableStart;
        }
    }
}
