using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Estragos do ataque às Vilas do Alto (pedido do usuário em 2026-11-08; os invasores ainda não foram decididos, então
    // nada mostra quem atacou, só o que sobrou). Peças do kit AR_Ataque:
    //   casas     uma em cada três casas de empena, larga ou torre vira a versão quebrada (o estrago e os montes ficam na
    //             frente, do lado da praça), se os montes dela não caírem nas linhas das chegadas nem passarem da beira;
    //             as quebradas não ganham lanternas, correio nem lixeira;
    //   praça     crateras (a grande nas praças largas) com corpos carbonizados na beira, barricadas ao lado das chegadas
    //             (por onde os moradores se defenderam) com armas largadas atrás, montes de escombros soltos e encostados
    //             nas laterais das casas, autômatos de guarda destruídos, colunas-lanterna tombadas, cristais
    //             estilhaçados e estandartes queimados pendurados nas fachadas.
    // Cada peça confere o espaço (caixa contra os colisores já postos, a beira, as linhas das chegadas até o meio e as
    // outras peças desta passada), como o mobiliário. Sorteio próprio por vila (o resto do mapa não muda); 'level' de 1
    // (pouco) a 3 (muito).
    public static partial class PicosBuilder
    {
        // Contorno no chão de cada peça, em metros locais do Unity (x, z mínimos e máximos; a frente do Blender é +z)
        static readonly Dictionary<string, (Vector2 Min, Vector2 Max, float Height)> k_AttackFoot =
            new Dictionary<string, (Vector2, Vector2, float)>
            {
                { "PR_Escombros_Queimados_A", (new Vector2(-3.5f, -4.2f), new Vector2(3.6f, 2.6f), 2.3f) },
                { "PR_Escombros_Queimados_B", (new Vector2(-3.2f, -6.6f), new Vector2(3f, 3.7f), 1.5f) },
                { "PR_Escombros_Queimados_C", (new Vector2(-3f, 0.4f), new Vector2(2.8f, 4.2f), 2.8f) },   // a parte de trás entra na parede
                { "PR_Cratera_A", (new Vector2(-5f, -4.9f), new Vector2(5.9f, 5.9f), 1f) },
                { "PR_Cratera_B", (new Vector2(-9.8f, -8.4f), new Vector2(9.7f, 10.2f), 1f) },
                { "PR_Barricada_A", (new Vector2(-4.1f, -2f), new Vector2(4.2f, 2.7f), 1.9f) },
                { "PR_Sentinela_Destruida", (new Vector2(-1.2f, -1.4f), new Vector2(2.4f, 2.3f), 0.7f) },
                { "PR_Coluna_Bobinas_Tombada", (new Vector2(-0.9f, -8.5f), new Vector2(1.8f, 0.7f), 2.8f) },
                { "PR_Cristais_Estilhacados", (new Vector2(-2f, -2.3f), new Vector2(1.7f, 2.5f), 2.1f) },
                { "PR_Armas_Largadas", (new Vector2(-1.2f, -1.9f), new Vector2(1.1f, 2.3f), 0.5f) },
                { "PR_Corpos_Carbonizados", (new Vector2(-1.9f, -1.3f), new Vector2(1.9f, 1.4f), 0.5f) },
                { "PR_Tenda_Destruida_A", (new Vector2(-1.8f, -1.2f), new Vector2(1.8f, 3.2f), 3f) },
                { "PR_Tenda_Destruida_B", (new Vector2(-2.9f, -2.3f), new Vector2(3.3f, 2.8f), 3f) },
                { "PR_Banco_Quebrado", (new Vector2(-1.3f, -0.4f), new Vector2(1f, 0.8f), 1.6f) },
                { "PR_Capsula_Destruida", (new Vector2(-3.3f, -3.3f), new Vector2(3.2f, 2.8f), 1.5f) },
            };

        // Casas quebradas: mesmo corpo da inteira, com os montes na frente (contorno da versão quebrada)
        static readonly Dictionary<string, (Vector2 Min, Vector2 Max)> k_BrokenHouses = new Dictionary<string, (Vector2, Vector2)>
        {
            { "AR_Casa_Empena", (new Vector2(-7.8f, -5.1f), new Vector2(4.3f, 10.7f)) },
            { "AR_Casa_Larga", (new Vector2(-6.3f, -4.5f), new Vector2(6.3f, 9.1f)) },
            { "AR_Casa_Torre", (new Vector2(-6.1f, -4.2f), new Vector2(4.2f, 10.1f)) },
            { "AR_Casa_Terraco", (new Vector2(-5.1f, -4.7f), new Vector2(7.9f, 7.5f)) },
            { "AR_Casa_Esquina", (new Vector2(-9.8f, -4.6f), new Vector2(5.1f, 10.7f)) },
        };

        const string k_Broken = "_Quebrada";
        // Contagem por modelo (o contador único misturava os modelos e a vez de um caía sempre noutro) e a vez adiada
        // de quem não coube (a próxima casa do mesmo modelo quebra no lugar dela)
        static readonly Dictionary<string, int> s_AttackHouseSeen = new Dictionary<string, int>();
        static readonly HashSet<string> s_AttackHousePending = new HashSet<string>();

        static void ResetAttackHouses()
        {
            s_AttackHouseSeen.Clear();
            s_AttackHousePending.Clear();
        }

        // Chamado pelo KitHouseRing para cada casa: devolve o modelo quebrado (de cada modelo que tem versão quebrada, a
        // segunda casa de cada três) ou o mesmo. Não gasta sorteio.
        static string AttackHouse(string model, Node n, Vector3 p, float yaw, List<Vector3> gates)
        {
            if (!k_BrokenHouses.TryGetValue(model, out (Vector2 Min, Vector2 Max) foot))
                return model;
            s_AttackHouseSeen.TryGetValue(model, out int seen);
            s_AttackHouseSeen[model] = seen + 1;
            if (seen % 3 != 1 && !s_AttackHousePending.Contains(model))
                return model;
            if (!BrokenHouseFits(foot, n, p, yaw, gates))
            {
                s_AttackHousePending.Add(model);
                return model;
            }

            s_AttackHousePending.Remove(model);
            return model + k_Broken;
        }

        static bool BrokenHouseFits((Vector2 Min, Vector2 Max) foot, Node n, Vector3 p, float yaw, List<Vector3> gates)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            foreach (Vector3 corner in Corners(foot.Min, foot.Max))
            {
                Vector3 w = p + q * corner;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(n.Center.x, n.Center.z)) > n.Radius - 0.6f)
                    return false;
                foreach (Vector3 g in gates)
                    if (SegmentDistance(w, n.Center, g) < 3.5f)
                        return false;
            }

            return true;
        }

        static IEnumerable<Vector3> Corners(Vector2 min, Vector2 max)
        {
            yield return new Vector3(min.x, 0f, min.y);
            yield return new Vector3(max.x, 0f, min.y);
            yield return new Vector3(max.x, 0f, max.y);
            yield return new Vector3(min.x, 0f, max.y);
            yield return new Vector3((min.x + max.x) * 0.5f, 0f, min.y);
            yield return new Vector3((min.x + max.x) * 0.5f, 0f, max.y);
            yield return new Vector3(min.x, 0f, (min.y + max.y) * 0.5f);
            yield return new Vector3(max.x, 0f, (min.y + max.y) * 0.5f);
            yield return new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
        }

        class AttackLayout
        {
            public Node N;
            public Vector3 C;
            public List<Vector3> Gates;
            public Transform Root;
            public System.Random Rnd;
            public readonly List<(Vector3 At, float Radius)> Taken = new List<(Vector3, float)>();
            public int Placed, Skipped;

            public float Range(float min, float max) => min + (float)Rnd.NextDouble() * (max - min);
        }

        static bool AttackFree(AttackLayout A, string model, Vector3 p, float yaw, out Vector3 center, out float clear)
        {
            (Vector2 min, Vector2 max, float height) = k_AttackFoot[model];
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            center = p + q * new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
            var half = new Vector3((max.x - min.x) * 0.5f, Mathf.Max(height, 0.5f) * 0.5f, (max.y - min.y) * 0.5f);
            clear = Mathf.Max(half.x, half.z);
            foreach (Vector3 corner in Corners(min, max))
            {
                Vector3 w = p + q * corner;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(A.C.x, A.C.z)) > A.N.Radius - 1f)
                    return false;
                foreach (Vector3 g in A.Gates)
                    if (SegmentDistance(w, A.C, g) < 3.2f)
                        return false;
            }

            foreach ((Vector3 at, float radius) in A.Taken)
                if (Vector2.Distance(new Vector2(center.x, center.z), new Vector2(at.x, at.z)) < radius + clear * 0.8f)
                    return false;
            Vector3 box = new Vector3(center.x, A.C.y + 0.15f + half.y, center.z);
            return !Physics.CheckBox(box, half, q, ~0, QueryTriggerInteraction.Ignore);
        }

        static bool AttackPiece(AttackLayout A, string model, Vector3 p, float yaw, bool collider = true)
        {
            p.y = A.C.y;
            if (!AttackFree(A, model, p, yaw, out Vector3 center, out float clear))
                return false;
            PlaceTech(model, p, yaw, A.Root, 1f, collider);
            Physics.SyncTransforms();
            A.Taken.Add((center, clear));
            A.Placed++;
            return true;
        }

        // Tenta pôr a peça num ponto sorteado da praça (anel de 'inner' a 'outer' frações do raio); devolve onde pôs
        static Vector3? AttackScatter(AttackLayout A, string model, float inner, float outer, int tries = 40, bool collider = true)
        {
            for (int k = 0; k < tries; k++)
            {
                float a = A.Range(0f, Mathf.PI * 2f), r = A.N.Radius * A.Range(inner, outer);
                Vector3 p = A.C + Around(a) * r;
                if (AttackPiece(A, model, p, A.Range(0f, 360f), collider))
                    return p;
            }

            A.Skipped++;
            return null;
        }

        static void AttackDamage(Transform parent, Node n, List<Vector3> gates, List<HouseSpot> houses, string name, int seed, int level)
        {
            Physics.SyncTransforms();
            var A = new AttackLayout
            {
                N = n, C = n.Center, Gates = gates, Rnd = new System.Random(seed), Root = new GameObject("Estragos").transform,
            };
            A.Root.SetParent(parent, false);
            houses = houses ?? new List<HouseSpot>();

            // Crateras primeiro (as peças grandes), com corpos carbonizados na beira das mais fortes
            var craters = new List<string>();
            if (level >= 3 || (level == 2 && n.Radius >= 34f))
                craters.Add(n.Radius >= 34f ? "PR_Cratera_B" : "PR_Cratera_A");
            if (level != 2 || n.Radius < 34f)
                craters.Add("PR_Cratera_A");
            foreach (string crater in craters)
            {
                Vector3? at = AttackScatter(A, crater, 0.2f, 0.75f);
                if (at.HasValue && level >= 2)
                {
                    float reach = crater == "PR_Cratera_B" ? 11f : 7f;
                    for (int k = 0; k < 12; k++)
                        if (AttackPiece(A, "PR_Corpos_Carbonizados", at.Value + Around(A.Range(0f, 6.3f)) * reach, A.Range(0f, 360f), false))
                            break;
                }
            }

            // Barricadas ao lado das chegadas, viradas para fora (por onde vinham), com armas largadas atrás
            int barricades = level - 1;
            for (int i = 0; i < gates.Count && barricades > 0; i++)
            {
                Vector3 g = new Vector3(gates[i].x, n.Center.y, gates[i].z);
                Vector3 inward = Flat(n.Center - g), side = Vector3.Cross(Vector3.up, inward);
                const string model = "PR_Barricada_A";
                bool placed = false;
                foreach (float dist in new[] { 7f, 9.5f, 12f })
                {
                    foreach (float off in new[] { 7f, 9f, 11f })
                    {
                        float s = A.Rnd.Next(2) == 0 ? 1f : -1f;
                        foreach (float sign in new[] { s, -s })
                        {
                            Vector3 p = g + inward * dist + side * sign * off;
                            if (!AttackPiece(A, model, p, YawOf(-inward) + A.Range(-14f, 14f)))
                                continue;
                            AttackPiece(A, "PR_Armas_Largadas", p + inward * 3f + side * A.Range(-1.5f, 1.5f), A.Range(0f, 360f), false);
                            placed = true;
                            break;
                        }

                        if (placed)
                            break;
                    }

                    if (placed)
                        break;
                }

                if (placed)
                    barricades--;
                else
                    A.Skipped++;
            }

            // Escombros soltos pela praça e encostados nas laterais das casas
            for (int k = 0; k < level + 1; k++)
                AttackScatter(A, k % 2 == 0 ? "PR_Escombros_Queimados_A" : "PR_Escombros_Queimados_B", 0.25f, 0.85f);
            AttackLeaning(A, houses, level);

            // Peças menores
            for (int k = 0; k < (level >= 3 ? 2 : level - 1); k++)
                AttackScatter(A, "PR_Sentinela_Destruida", 0.2f, 0.85f);
            if (level >= 2)
                AttackScatter(A, "PR_Coluna_Bobinas_Tombada", 0.35f, 0.85f);
            for (int k = 0; k < (level >= 3 ? 2 : 1); k++)
                AttackScatter(A, "PR_Cristais_Estilhacados", 0.2f, 0.85f);
            // Tendas de feira destruídas, bancos partidos e uma cápsula de carga queimada
            for (int k = 0; k < level; k++)
                AttackScatter(A, k % 2 == 0 ? "PR_Tenda_Destruida_A" : "PR_Tenda_Destruida_B", 0.3f, 0.85f);
            for (int k = 0; k < level; k++)
                AttackScatter(A, "PR_Banco_Quebrado", 0.25f, 0.85f);
            if (level >= 2)
                AttackScatter(A, "PR_Capsula_Destruida", 0.25f, 0.8f);

            AttackBanners(A, houses, level);
            if (A.Skipped > 0)
                Debug.Log($"{name}: estragos com {A.Placed} peças; {A.Skipped} sem espaço ficaram de fora");
        }

        // Monte encostado (PR_Escombros_Queimados_C, a parte de trás reta entra 0,5 m na parede) nas laterais das casas
        // inteiras: a parede é achada por raio na altura de 1,2 m
        static void AttackLeaning(AttackLayout A, List<HouseSpot> houses, int count)
        {
            var order = new List<HouseSpot>(houses);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = A.Rnd.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            int placed = 0;
            foreach (HouseSpot h in order)
            {
                if (placed >= count)
                    break;
                if (h.Model.EndsWith(k_Broken))
                    continue;
                Vector3 right = h.Rotation * Vector3.right, front = h.Rotation * Vector3.forward;
                float s = A.Rnd.Next(2) == 0 ? 1f : -1f;
                foreach (float side in new[] { s, -s })
                {
                    Vector3 o = h.Foot + right * side * (h.W * 0.5f + 3f) + front * A.Range(-1.5f, 1f);
                    o.y = A.C.y + 1.2f;
                    if (!Physics.Raycast(o, -right * side, out RaycastHit hit, h.W * 0.5f + 3.5f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    Vector3 normal = Flat(hit.normal);
                    if (hit.distance < 1f || Vector3.Dot(normal, right * side) < 0.85f)
                        continue;
                    if (AttackPiece(A, "PR_Escombros_Queimados_C", hit.point, YawOf(normal)))
                    {
                        placed++;
                        break;
                    }
                }
            }
        }

        // Estandartes queimados (PR_Estandarte_Queimado: origem no varão, na parede; o pano desce 2,1 m e fica até 1 m à
        // frente) nas fachadas lisas, a 5,3 m do chão (acima das portas), primeiro nas casas quebradas
        static void AttackBanners(AttackLayout A, List<HouseSpot> houses, int count)
        {
            var order = new List<HouseSpot>(houses);
            order.Sort((x, y) => y.Model.EndsWith(k_Broken).CompareTo(x.Model.EndsWith(k_Broken)));
            int placed = 0;
            foreach (HouseSpot h in order)
            {
                if (placed >= count)
                    break;
                Vector3 right = h.Rotation * Vector3.right, front = h.Rotation * Vector3.forward;
                Vector3 origin = h.Foot + front * (h.D * 0.5f + 3f);

                float Hit(float s, float height, out Vector3 point)
                {
                    point = Vector3.zero;
                    Vector3 o = origin + right * s;
                    o.y = A.C.y + height;
                    if (!Physics.Raycast(o, -front, out RaycastHit hit, h.D + 6f, ~0, QueryTriggerInteraction.Ignore) ||
                        Vector3.Dot(Flat(hit.normal), front) < 0.85f)
                        return -1f;
                    point = hit.point;
                    return hit.distance;
                }

                foreach (float s in new[] { -h.W * 0.28f, h.W * 0.28f })
                {
                    float d = Hit(s, 5.3f, out Vector3 wall);
                    if (d < 0f)
                        continue;
                    bool flat = true;
                    foreach ((float ds, float height) in new[] { (-0.7f, 5.3f), (0.7f, 5.3f), (0f, 3.6f), (-0.7f, 3.6f), (0.7f, 3.6f) })
                        flat &= Mathf.Abs(Hit(s + ds, height, out _) - d) < 0.15f;
                    if (!flat)
                        continue;
                    PlaceTech("PR_Estandarte_Queimado", wall + front * 0.03f, YawOf(front), A.Root, 1f, false);
                    A.Placed++;
                    placed++;
                    break;
                }
            }
        }
    }
}
