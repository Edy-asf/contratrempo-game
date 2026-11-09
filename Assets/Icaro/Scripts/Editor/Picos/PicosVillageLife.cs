using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Vida nas vilas (pedido do usuário em 2026-11-08: vila habitada tem bancos, lixeiras e áreas de convivência, cada
    // coisa no lugar onde faz sentido; manter esse nível de detalhe). Peças do kit AR_Mobiliario, por zona:
    //   mirante   na beira aberta para a cidade: a luneta no meio, a mesa de mapa, bancos virados para a vista e
    //             lixeiras entre eles;
    //   estação   o abrigo de espera virado para a estação, o painel de partidas e uma lixeira nas pontas dele e o
    //             relógio de rua do outro lado;
    //   convívio  num trecho da beira de trás reservado (sem casas): a pérgola com duas mesas, o aquecedor de cristal
    //             na frente dela com bancos virados para ele, o quadro de avisos e uma lixeira;
    //   casas     lanternas dos dois lados de cada porta (a porta é achada por raios: o vão fica mais fundo que a
    //             parede), o correio pneumático numa parede lisa ao lado e, em algumas, uma lixeira; estantes de
    //             ferramentas nas oficinas;
    //   chegadas  o marco com o nome da vila virado para quem chega e um banco de descanso do outro lado.
    // Nada encosta em nada: cada peça confere o espaço (caixa contra os colisores já postos), a beira, as linhas das
    // chegadas até o meio e as outras peças desta passada.
    public static partial class PicosBuilder
    {
        class LifeLayout
        {
            public Node N;
            public Vector3 C;
            public float View;
            public Transform Root;
            public List<Vector3> Gates;
            public readonly List<(Vector3 At, float Radius)> Taken = new List<(Vector3, float)>();
            public int Placed, Skipped;
        }

        static Vector3 Tangent(float angle) => new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));

        // 'p' está livre para uma peça de meia largura 'half' (x, z locais) e altura 'height', girada 'yaw'?
        static bool LifeFree(LifeLayout L, Vector3 p, float yaw, Vector2 half, float height, bool nearGates = false)
        {
            float clear = Mathf.Max(half.x, half.y);
            float r = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(L.C.x, L.C.z));
            if (r > L.N.Radius - 0.9f - clear)
                return false;
            if (!nearGates)
                foreach (Vector3 g in L.Gates)
                    if (SegmentDistance(p, L.C, g) < 3.2f + clear)
                        return false;
            foreach ((Vector3 at, float radius) in L.Taken)
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(at.x, at.z)) < radius + clear * 0.8f)
                    return false;
            Vector3 center = new Vector3(p.x, L.C.y + 0.15f + height * 0.5f, p.z);
            return !Physics.CheckBox(center, new Vector3(half.x, height * 0.5f, half.y), Quaternion.Euler(0f, yaw, 0f), ~0,
                QueryTriggerInteraction.Ignore);
        }

        // Põe a peça se couber; devolve se pôs
        static bool LifePiece(LifeLayout L, string model, Vector3 p, float yaw, Vector2 half, float height, bool collider = true,
            bool nearGates = false)
        {
            p.y = L.C.y;
            if (!LifeFree(L, p, yaw, half, height, nearGates))
            {
                L.Skipped++;
                return false;
            }

            PlaceTech(model, p, yaw, L.Root, 1f, collider);
            Physics.SyncTransforms();
            L.Taken.Add((p, Mathf.Max(half.x, half.y)));
            L.Placed++;
            return true;
        }

        // 'overlook': mirante na beira aberta para a cidade; 'station': abrigo e relógio da estação; 'market': ângulo da
        // feirinha de tendas (NaN = sem feirinha)
        static void BuildVillageLife(Transform root, Node n, List<Vector3> gates, Vector3 view, List<HouseSpot> houses, float gatherAngle,
            string villageName, bool overlook = true, bool station = true, float market = float.NaN, List<Vector3> paths = null)
        {
            Physics.SyncTransforms();
            var L = new LifeLayout
            {
                N = n, C = n.Center, View = Mathf.Atan2(view.x, view.z), Root = new GameObject("Mobiliario").transform, Gates = gates,
            };
            L.Root.SetParent(root, false);
            LifeArrivals(L);
            // Passagens que não são chegadas (a ponte do mirante): ficam livres, sem o marco nem o banco das chegadas
            if (paths != null)
            {
                L.Gates = new List<Vector3>(gates);
                L.Gates.AddRange(paths);
            }
            LifeHouses(L, houses);
            if (overlook)
                LifeOverlook(L);
            if (station)
                LifeStation(L);
            LifeGathering(L, gatherAngle);
            if (!float.IsNaN(market))
                LifeMarket(L, market);
            if (L.Skipped > 0)
                Debug.Log($"{villageName}: mobiliário com {L.Placed} peças; {L.Skipped} lugares sem espaço ficaram vazios");
        }

        // Mirante: na beira aberta para a cidade
        static void LifeOverlook(LifeLayout L)
        {
            float r = L.N.Radius;
            foreach ((float along, string model, float inset) in new[]
                     { (0f, "PR_Luneta", 1.6f), (-2.8f, "PR_Placa_Mapa", 1.7f), (3.4f, "PR_Banco", 2.0f), (5.9f, "PR_Lixeira", 1.6f),
                       (8.4f, "PR_Banco", 2.0f), (-6.2f, "PR_Banco", 2.0f), (-8.7f, "PR_Lixeira", 1.6f), (-11.2f, "PR_Banco", 2.0f) })
            {
                float radius = r - inset, a = L.View + along / radius;
                Vector3 p = L.C + Around(a) * radius;
                Vector3 outward = Around(a);
                switch (model)
                {
                    case "PR_Luneta":
                        LifePiece(L, model, p, YawOf(outward), new Vector2(0.45f, 0.6f), 2f);
                        break;
                    case "PR_Placa_Mapa":
                        LifePiece(L, model, p, YawOf(-outward), new Vector2(0.75f, 0.5f), 1.5f);
                        break;
                    case "PR_Banco":
                        LifePiece(L, model, p, YawOf(outward), new Vector2(1.05f, 0.35f), 1.6f);
                        break;
                    default:
                        LifePiece(L, model, p, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f);
                        break;
                }
            }
        }

        // Estação: abrigo virado para ela (fora do mirante), painel e lixeira nas pontas, relógio de rua do outro lado
        static void LifeStation(LifeLayout L)
        {
            float shelter = float.NaN;
            for (int k = 0; k < 24 && float.IsNaN(shelter); k++)
            {
                float a = L.View + (k % 2 == 0 ? 1f : -1f) * (80f + 10f * (k / 2)) * Mathf.Deg2Rad;
                foreach (float radius in new[] { 20f, 22.5f })
                {
                    Vector3 p = L.C + Around(a) * radius;
                    if (!LifeFree(L, p, YawOf(-Around(a)), new Vector2(2.9f, 1.5f), 3.5f))
                        continue;
                    LifePiece(L, "PR_Abrigo_Espera", p, YawOf(-Around(a)), new Vector2(2.9f, 1.5f), 3.5f);
                    Vector3 side = Tangent(a);
                    LifePiece(L, "PR_Painel_Partidas", p + side * 3.7f, YawOf(-Around(a)), new Vector2(0.5f, 0.3f), 2.9f);
                    LifePiece(L, "PR_Lixeira", p - side * 3.5f, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f);
                    shelter = a;
                    break;
                }
            }

            if (float.IsNaN(shelter))
            {
                L.Skipped++;
                return;
            }

            for (int k = 0; k < 18; k++)
            {
                float a = shelter + Mathf.PI + (k % 2 == 0 ? 1f : -1f) * 12f * (k / 2) * Mathf.Deg2Rad;
                if (AngleGap(a, L.View) < 60f * Mathf.Deg2Rad)
                    continue;
                if (LifeFree(L, L.C + Around(a) * 19.5f, 0f, new Vector2(0.6f, 0.6f), 5.6f))
                {
                    LifePiece(L, "PR_Relogio_Rua", L.C + Around(a) * 19.5f, R(0f, 360f), new Vector2(0.6f, 0.6f), 5.6f);
                    return;
                }
            }

            L.Skipped++;
        }

        // Área de convivência no trecho reservado da beira de trás
        static void LifeGathering(LifeLayout L, float angle)
        {
            foreach (float back in new[] { 10f, 12f, 8.5f })
            {
                Vector3 p = L.C + Around(angle) * (L.N.Radius - back);
                Vector3 forward = -Around(angle), right = Vector3.Cross(Vector3.up, forward);
                float yaw = YawOf(forward);
                if (!LifeFree(L, p, yaw, new Vector2(4.3f, 3.2f), 3.9f))
                    continue;
                LifePiece(L, "PR_Pergola", p, yaw, new Vector2(4.3f, 3.2f), 3.9f);
                // As mesas ficam embaixo da pérgola: entram direto (a caixa dela é a da pérgola)
                foreach (float x in new[] { -1.9f, 1.9f })
                {
                    Vector3 q = p + right * x;
                    PlaceTech("PR_Mesa_Bancos", new Vector3(q.x, L.C.y, q.z), yaw + 90f + R(-6f, 6f), L.Root);
                    L.Placed++;
                }

                Vector3 heater = p + forward * 6f;
                if (LifePiece(L, "PR_Aquecedor_Cristal", heater, R(0f, 45f), new Vector2(0.95f, 0.95f), 2.2f))
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 dir = (forward * 0.35f + right * s).normalized;
                        Vector3 bench = heater + dir * 2.6f;
                        LifePiece(L, "PR_Banco", bench, YawOf(-dir), new Vector2(1.05f, 0.35f), 1.6f);
                    }

                LifePiece(L, "PR_Quadro_Avisos", p + right * 5.4f + forward * 0.8f, yaw, new Vector2(1.05f, 0.25f), 2.5f);
                LifePiece(L, "PR_Lixeira", p - right * 5f + forward * 1.4f, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f);
                return;
            }

            L.Skipped++;
        }

        // Feirinha: três tendas num arco virado para o meio da praça, contêineres de carga atrás de uma ponta, uma lixeira
        // na outra e um banco de frente para elas
        static void LifeMarket(LifeLayout L, float angle)
        {
            Vector2 tent = new Vector2(1.9f, 1.35f);
            foreach (float radius in new[] { 9.5f, 8f, 11f })
            {
                Vector3 c = L.C + Around(angle) * radius;
                Vector3 inward = -Around(angle), side = Tangent(angle);
                if (!LifeFree(L, c, YawOf(inward), tent, 3.3f))
                    continue;
                int placed = 0;
                Vector3 first = c, last = c;
                foreach (float along in new[] { 0f, -4.3f, 4.3f })
                {
                    Vector3 p = c + side * along + inward * Mathf.Abs(along) * 0.18f;
                    Vector3 facing = (L.C - p);
                    facing.y = 0f;
                    if (!LifePiece(L, "PR_Tenda_Feira", p, YawOf(facing.normalized) + R(-4f, 4f), tent, 3.3f))
                        continue;
                    placed++;
                    if (along < 0f)
                        first = p;
                    else if (along > 0f)
                        last = p;
                }

                if (placed == 0)
                    continue;
                Vector3 back = -inward;
                Vector3 crates = first + back * 1.9f - side * 1.2f;
                if (LifePiece(L, "PR_Conteiner_Celulas", crates, YawOf(side) + R(-8f, 8f), new Vector2(0.8f, 0.6f), 2f))
                {
                    PlaceTech("PR_Conteiner_Celulas", new Vector3(crates.x, L.C.y + 1f, crates.z), YawOf(side) + R(-12f, 12f), L.Root);
                    L.Placed++;
                }

                LifePiece(L, "PR_Lixeira", last + side * 2.4f + inward * 0.6f, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f);
                Vector3 bench = c + inward * 4.6f;
                LifePiece(L, "PR_Banco", bench, YawOf(-inward), new Vector2(1.05f, 0.35f), 1.6f);
                return;
            }

            L.Skipped++;
        }

        // Chegadas: o marco com o nome virado para quem chega e um banco de descanso do outro lado do caminho
        static void LifeArrivals(LifeLayout L)
        {
            foreach (Vector3 g in L.Gates)
            {
                Vector3 inward = Flat(L.C - g).normalized, side = Vector3.Cross(Vector3.up, inward);
                float markSide = 0f;
                foreach (float s in new[] { 1f, -1f })
                    if (LifePiece(L, "PR_Marco_Entrada", g + inward * 2.5f + side * s * 4.4f, YawOf(-inward), new Vector2(0.6f, 0.35f), 3.4f,
                            true, true))
                    {
                        markSide = s;
                        break;
                    }

                float benchSide = markSide == 0f ? 1f : -markSide;
                Vector3 bench = g + inward * 6.5f + side * benchSide * 5f;
                if (LifePiece(L, "PR_Banco", bench, YawOf(-side * benchSide), new Vector2(1.05f, 0.35f), 1.6f, true, true))
                    LifePiece(L, "PR_Lixeira", bench + inward * 1.6f, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f, true, true);
            }
        }

        // Frente das casas: lanternas na porta, correio, lixeira e, nas oficinas, a estante
        static void LifeHouses(LifeLayout L, List<HouseSpot> houses)
        {
            foreach (HouseSpot h in houses)
            {
                if (h.Model.EndsWith(k_Broken))
                    continue;           // casa quebrada pelo ataque: sem lanternas, correio nem lixeira
                Transform house = null;
                foreach (Transform child in L.Root.parent)
                    if (child.name.StartsWith(h.Model) && Vector2.Distance(new Vector2(child.position.x, child.position.z),
                            new Vector2(h.Foot.x, h.Foot.z)) < 0.3f)
                        house = child;
                if (house == null)
                    continue;
                Vector3 front = h.Rotation * Vector3.forward, right = h.Rotation * Vector3.right;
                Vector3 origin = h.Foot + front * (h.D * 0.5f + 3f);

                // Distância da frente até a casa em cada ponto da fachada, nas alturas pedidas (só os colisores dela)
                float Hit(float s, float height)
                {
                    Vector3 o = origin + right * s + Vector3.up * (L.C.y + height - origin.y);
                    float best = float.MaxValue;
                    foreach (RaycastHit hit in Physics.RaycastAll(o, -front, h.D + 6f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.transform.IsChildOf(house) && hit.distance < best)
                            best = hit.distance;
                    return best;
                }

                const float step = 0.2f;
                int count = Mathf.FloorToInt((h.W - 0.6f) / step) + 1;
                var low = new float[count];
                var high = new float[count];
                var plain = new List<float>();
                for (int i = 0; i < count; i++)
                {
                    float s = -h.W * 0.5f + 0.3f + i * step;
                    low[i] = Hit(s, 0.5f);
                    high[i] = Hit(s, 1.9f);
                    if (low[i] < float.MaxValue)
                        plain.Add(low[i]);
                }

                if (plain.Count < 4)
                    continue;
                plain.Sort();
                float wall = plain[plain.Count * 2 / 5];

                // Porta: o trecho mais perto do meio em que o vão nas duas alturas fica 12 cm a 2 m mais fundo que a parede
                int bestStart = -1, bestEnd = -1;
                float bestScore = float.MaxValue;
                for (int i = 0; i < count;)
                {
                    bool deep = low[i] > wall + 0.12f && low[i] < wall + 2f && high[i] > wall + 0.12f && high[i] < wall + 2f;
                    if (!deep)
                    {
                        i++;
                        continue;
                    }

                    int j = i;
                    while (j + 1 < count && low[j + 1] > wall + 0.12f && low[j + 1] < wall + 2f && high[j + 1] > wall + 0.12f && high[j + 1] < wall + 2f)
                        j++;
                    float width = (j - i + 1) * step, mid = -h.W * 0.5f + 0.3f + (i + j) * 0.5f * step;
                    if (width >= 0.8f && Mathf.Abs(mid) < bestScore)
                    {
                        bestScore = Mathf.Abs(mid);
                        bestStart = i;
                        bestEnd = j;
                    }

                    i = j + 1;
                }

                if (bestStart < 0)
                {
                    L.Skipped++;
                    continue;
                }

                float doorLeft = -h.W * 0.5f + 0.3f + bestStart * step, doorRight = -h.W * 0.5f + 0.3f + bestEnd * step;
                float yaw = YawOf(front);

                // Ponto da parede lisa em 's' (todas as alturas batem na parede); devolve o ponto na altura do piso
                bool WallAt(float s, float[] heights, out Vector3 at)
                {
                    at = Vector3.zero;
                    float d = 0f;
                    foreach (float height in heights)
                    {
                        float hit = Hit(s, height);
                        if (Mathf.Abs(hit - wall) > 0.15f)
                            return false;
                        d = Mathf.Max(d, hit);
                    }

                    at = origin + right * s - front * Hit(s, heights[0]);
                    at.y = L.C.y;
                    return true;
                }

                foreach (float s in new[] { doorLeft - 0.45f, doorRight + 0.45f })
                    if (WallAt(s, new[] { 2.5f, 2.8f, 2.2f }, out Vector3 lamp))
                    {
                        PlaceTech("PR_Lanterna_Porta", lamp + Vector3.up * 2.5f, yaw, L.Root, 1f, false);
                        L.Placed++;
                    }

                bool workshop = h.Model.StartsWith("AR_Oficina");
                float mailSide = s_Random.Next(2) == 0 ? -1f : 1f;
                if (!workshop && s_Random.NextDouble() < 0.65)
                {
                    float s = mailSide < 0f ? doorLeft - 1.1f : doorRight + 1.1f;
                    if (WallAt(s, new[] { 1.1f, 1.6f, 2.4f }, out Vector3 mail) &&
                        LifeFree(L, mail + front * 0.2f, yaw, new Vector2(0.25f, 0.2f), 2.5f, true))
                    {
                        PlaceTech("PR_Correio_Tubo", mail, yaw, L.Root, 1f, false);
                        L.Taken.Add((mail + front * 0.2f, 0.3f));
                        L.Placed++;
                    }
                }

                if (workshop)
                {
                    foreach (float s in new[] { doorRight + 1.6f, doorLeft - 1.6f })
                        if (WallAt(s, new[] { 0.6f, 1.3f, 1.9f }, out Vector3 shelf) &&
                            LifeFree(L, shelf + front * 0.25f, yaw, new Vector2(0.8f, 0.22f), 1.9f, true))
                        {
                            PlaceTech("PR_Estante_Ferramentas", shelf - front * 0.03f, yaw, L.Root);
                            L.Taken.Add((shelf + front * 0.25f, 0.8f));
                            L.Placed++;
                            break;
                        }
                }
                else if (s_Random.NextDouble() < 0.4)
                {
                    float s = mailSide < 0f ? doorRight + 1.0f : doorLeft - 1.0f;
                    Vector3 bin = origin + right * s - front * (wall - 0.55f);
                    LifePiece(L, "PR_Lixeira", bin, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f, true, true);
                }
            }
        }
    }
}
