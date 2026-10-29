using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Vilas do Alto (esqueleto em blocos; decisões em Design/Picos_Acima_das_Nuvens.md). O portal quebrado do Bastião
    // (sala 19) leva para cá, uns 5 km ao sul. Um maciço grande ao norte da cidade flutuante, esculpido no Blender
    // (Ferramentas/Picos/macico_vilas.py: cristas com cumes e uma sela, contrafortes descendo para o vale de nuvens, uma
    // garganta), com as vilas em lugares naturais dele, cada uma um pouco mais baixa e sempre com a cidade à vista do
    // outro lado do vale (a chegada fica uns 600 m ao norte da cidade e uns 150 m acima dela):
    //   Vila Escalonada (chegada pelo portal gêmeo, inteiro; na ponta de um contraforte, com dois terraços mais baixos),
    //   Vila dos Sinos (na sela entre dois cumes, em volta do campanário),
    //   Vila das Engrenagens (num ombro largo: oficinas, engrenagens paradas, trilhos e a estação do teleférico),
    //   Vila-Ponte (casas dos dois lados da rua numa ponte larga sobre a garganta, arcos por cima da rua),
    //   Vila Pendurada (no alto de um paredão e nos patamares do zigue-zague que desce por ele) e
    //   o portão da cidade (sala 22, na ponta do último contraforte), de onde a ponte colossal atravessa o vale.
    // O caminho é escavado na montanha (calçadas e escadas pelos cortes do Blender); pontes só onde há um vão de verdade.
    // A planta (vilas, trechos, patamares, pontes) vem do Macico_Vilas_Layout.json que o script do Blender grava.
    // Ilhas voadoras (do Anel, Tombada, Cacho de Ilhotas, do Aqueduto) e torres em picos sozinhos (do relógio, ligada às
    // Engrenagens por um teleférico parado; duas de vigia; o farol de runas; a torre gêmea partida) ficam só como
    // cenário, longe do caminho. Montanhas do gerador espalhadas em volta, sem formar anel. Sem salas por enquanto: o
    // número de arenas ainda está em aberto, então as vilas têm tamanho de arena.
    // Coordenadas: HP(leste, norte, altura) a partir do centro da cidade no nível 0 (k_High).
    public static partial class PicosBuilder
    {
        static readonly Vector3 k_High = new Vector3(-200f, 0f, -5800f);
        const float k_CityTop = 30f, k_CityRadius = 170f;
        static Transform s_HighArrival;

        [System.Serializable]
        class HighLayout
        {
            public HighVillage[] villages;
            public HighSegment[] segments;
            public HighLanding[] landings;
            public HighSpan deck, bridge;
        }

        [System.Serializable]
        class HighVillage
        {
            public string name;
            public float[] center;
            public float radius;
        }

        [System.Serializable]
        class HighSegment
        {
            public float[] a, b;
            public bool stairs;
            public string route;
            public float width;
        }

        [System.Serializable]
        class HighLanding
        {
            public float[] position;
        }

        [System.Serializable]
        class HighSpan
        {
            public float[] a, b;
            public float width;
        }

        // Plataforma: centro do chão, raio e inclinação (ilha tombada)
        struct Node
        {
            public Vector3 Center;
            public float Radius;
            public Quaternion Tilt;
        }

        enum Roof { Flat, Gable, Steep }

        struct HouseSpot
        {
            public Vector3 Foot;
            public Quaternion Rotation;
            public float W, D, H;
        }

        static Vector3 HP(float east, float north, float height) => k_High + new Vector3(east, height, north);

        static Vector3 HP(float[] p) => k_High + new Vector3(p[0], p[1], p[2]);

        static Node Peak(Vector3 center, float radius) => new Node { Center = center, Radius = radius, Tilt = Quaternion.identity };

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        // Ponto na beira do chão de uma plataforma, virado para 'toward' (no plano dela, se for inclinada)
        static Vector3 Rim(Node node, Vector3 toward)
        {
            Vector3 p = node.Center + Flat(toward - node.Center) * node.Radius * 0.72f;
            Vector3 normal = node.Tilt * Vector3.up;
            p.y = node.Center.y - (normal.x * (p.x - node.Center.x) + normal.z * (p.z - node.Center.z)) / normal.y;
            return p;
        }

        static void BuildHighlands()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{IcaroAssetUtility.Root}/Models/Picos/Macico_Vilas_Layout.json");
            HighLayout layout = json ? JsonUtility.FromJson<HighLayout>(json.text) : null;
            if (layout == null || !PlaceHighMassif())
            {
                Debug.LogWarning("[Ícaro] Vilas do Alto: falta o maciço ou a planta (rode Ferramentas/Picos/macico_vilas.py no " +
                                 "Blender); a região ficou de fora e o portal do Bastião não leva a lugar nenhum.");
                return;
            }

            // Sorteio próprio: o resto do mapa continua igual
            System.Random saved = s_Random;
            s_Random = new System.Random(2801);
            try
            {
                var city = new Vector3(k_High.x, k_CityTop, k_High.z);
                var villages = new Dictionary<string, Node>();
                foreach (HighVillage village in layout.villages)
                    villages[village.name] = Peak(HP(village.center), village.radius);

                // Caminho escavado: calçadas e escadas; as pontas de cada trecho dentro de uma vila ficam livres de casas
                var exits = new Dictionary<string, List<Vector3>>();
                foreach (string name in villages.Keys)
                    exits[name] = new List<Vector3>();
                foreach (HighSegment segment in layout.segments)
                {
                    Vector3 a = HP(segment.a), b = HP(segment.b);
                    if (segment.stairs)
                        Stairs(a.y < b.y ? a : b, a.y < b.y ? b : a, segment.width);
                    else
                        Path(a, b, segment.width);
                    ClearLine(a, b);
                    foreach (KeyValuePair<string, Node> pair in villages)
                    foreach (Vector3 end in new[] { a, b })
                        if (Vector3.Distance(end, pair.Value.Center) < pair.Value.Radius + 1f)
                            exits[pair.Key].Add(end);
                }

                foreach (Node node in villages.Values)
                    VillageFloor(node);

                Node gate = villages["Portao"];
                Vector3 clockPeak = HP(130f, 330f, 110f);
                Vector3 deckA = HP(layout.deck.a), deckB = HP(layout.deck.b);
                s_HighArrival = BuildStepVillage(villages["Escalonada"], villages["Escalonada_T1"], villages["Escalonada_T2"], city,
                    exits);
                BuildBellVillage(villages["Sinos"], exits["Sinos"].ToArray());
                BuildGearVillage(villages["Engrenagens"], exits["Engrenagens"].ToArray(), clockPeak);
                BuildBridgeVillage(villages["Ponte_O"], villages["Ponte_L"], deckA, deckB, layout.deck.width, exits);
                var landings = new List<Vector3>();
                foreach (HighLanding landing in layout.landings)
                    landings.Add(HP(landing.position));
                BuildHangingVillage(villages["Pendurada"], exits["Pendurada"].ToArray(), landings);
                BuildGate(gate, city);

                // Ponte colossal até a cidade (poucos arcos altos saindo das nuvens) e a cidade em esboço
                Vector3 bridgeA = HP(layout.bridge.a), bridgeB = HP(layout.bridge.b);
                Arcade(bridgeA, bridgeB, layout.bridge.width, 60f, 2);
                BuildCitySketch(city, Flat(gate.Center - city));

                // Cenário: torres em picos sozinhos, o teleférico e as ilhas voadoras, longe do caminho
                BuildClockTower(clockPeak);
                Cableway(Rim(villages["Engrenagens"], clockPeak), Rim(Peak(clockPeak, 16f), villages["Engrenagens"].Center));
                BuildWatchTower(HP(-560f, 380f, 140f), 7101, true, city);
                BuildWatchTower(HP(480f, 300f, 80f), 7102, false, city);
                BuildRuneBeacon(HP(-90f, -450f, 60f));
                BuildTwinTowers(HP(540f, -130f, 110f), city);
                BuildRingIsle(Peak(HP(-430f, 140f, 95f), 46f));
                BuildIsletCluster(HP(400f, 60f, 75f), HP(300f, 60f, 75f), HP(500f, 80f, 75f));
                BuildFallenIsle(new Node
                {
                    Center = HP(-280f, -330f, 70f), Radius = 40f, Tilt = Quaternion.AngleAxis(13f, Vector3.forward),
                });
                BuildAqueductIsle(Peak(HP(360f, -280f, 85f), 40f), HP(270f, -350f, 82f), HP(455f, -215f, 82f));

                // Montanhas do gerador espalhadas em volta: fora do maciço, do vale da cidade e da ponte
                Clear(city, k_CityRadius + 90f);
                ClearLine(bridgeA, bridgeB);
                foreach ((float e, float n, float r) in new[]
                         { (-330f, 620f, 280f), (-60f, 720f, 300f), (230f, 680f, 300f), (0f, 900f, 320f), (520f, 650f, 160f), (-560f, 620f, 160f) })
                    Clear(HP(e, n, 0f), r);
                DistantField(new Vector2(k_High.x, k_High.z), 150, 100, 1400, "Macicos_Vilas");

                Snowfall(villages["Escalonada"].Center + Vector3.up * 30f);
                Snowfall(villages["Engrenagens"].Center + Vector3.up * 30f);
                Snowfall(villages["Pendurada"].Center + Vector3.up * 10f);
                Snowfall(gate.Center + Vector3.up * 30f);
            }
            finally
            {
                s_Random = saved;
            }

            if (s_BastionPortal)
            {
                s_BastionPortal.Destination = s_HighArrival;
                s_BastionPortal.ArrivalTitle = "Vilas do Alto";
                s_BastionPortal.ArrivalSubtitle = "Picos acima das Nuvens";
            }
        }

        // O maciço do Blender, posto sem girar no centro da cidade (nível 0), com colisão na rocha
        static bool PlaceHighMassif()
        {
            string path = $"{IcaroAssetUtility.Root}/Models/Picos/Macico_Vilas.fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model)
                return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, s_World);
            go.name = "Macico_Vilas";
            go.transform.SetPositionAndRotation(k_High, model.transform.rotation);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                bool ice = renderer.name.EndsWith("_Gelo");
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = ice ? s_Materials.Ice : s_Materials.Mountain;
                renderer.sharedMaterials = materials;
                if (!ice)
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }

            // Para os fiapos de névoa se enrolarem no pé do maciço
            foreach ((float e, float n, float r) in new[] { (-300f, 470f, 70f), (40f, 500f, 80f), (300f, 420f, 70f), (-460f, 520f, 60f) })
                PicosMountainKit.Pillars.Add(new PicosMountainKit.Placed
                {
                    Top = HP(e, n, 60f), TopRadius = r, BaseRadius = r * 1.3f, Taper = 1f,
                });
            Physics.SyncTransforms();
            return true;
        }

        // ---------- plataformas ----------

        // Chão calçado de uma vila (a rocha do maciço fica 0,45 m abaixo)
        static void VillageFloor(Node node)
        {
            var slab = new MeshData();
            s_Platforms.Add((node.Center, node.Radius));
            Prism(slab, node.Center, node.Radius, node.Radius, 24, node.Center.y, node.Center.y - 0.8f, true);
            Pave(slab);
            Clear(node.Center, node.Radius + 40f);
        }

        // Topo plano de um pico sozinho (torres): pilar de rocha até a nuvem e o calçamento por cima
        static void Plateau(Vector3 top, float radius, int seed)
        {
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = top + Vector3.down * 0.5f, TopRadius = radius, BaseRadius = radius * 1.7f, Taper = 3f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 36, RingStep = 3f, Corners = 7, Flutes = 4, FluteDepth = 0.05f,
                BedHeight = R(10f, 16f), BedShift = 0.05f, Waist = 0.04f, Roughness = 0.05f, Seed = seed, Bundle = false,
            });
            var slab = new MeshData();
            s_Platforms.Add((top, radius * 0.78f));
            Prism(slab, top, radius * 0.78f, radius * 0.78f, 8, top.y, top.y - 0.8f, true);
            Pave(slab);
            Clear(top, radius + 40f);
        }

        // Ilha voadora: rocha em cone invertido com o topo plano (inclinado por 'tilt')
        static void SkyIsland(Vector3 top, float radius, Quaternion tilt, int seed, bool paved, int sides = 24)
        {
            const int rings = 10;
            float depth = radius * 1.4f;
            var grid = new Vector3[rings + 1, sides + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float r = SkyIslandRadius(radius, t);
                for (int j = 0; j <= sides; j++)
                {
                    float a = (j % sides) * Mathf.PI * 2f / sides;
                    float n = 1f + (Mathf.PerlinNoise(seed * 0.37f + (j % sides) * 0.5f, i * 0.6f) - 0.5f) * (i == 0 ? 0.16f : 0.4f);
                    grid[i, j] = top + tilt * new Vector3(Mathf.Cos(a) * r * n, -depth * t - (i > 0 ? 1.5f : 0f), Mathf.Sin(a) * r * n);
                }
            }

            Vector3 up = tilt * Vector3.up;
            for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                Vector3 a = grid[i, j], b = grid[i, j + 1], c = grid[i + 1, j + 1], d = grid[i + 1, j];
                Vector3 mid = (a + c) * 0.5f;
                Vector3 outward = mid - (top + up * Vector3.Dot(mid - top, up));
                Tri(s_MountainNear, a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, outward);
                Tri(s_MountainNear, a, c, d, Vector2.zero, Vector2.zero, Vector2.zero, outward);
            }

            for (int j = 0; j < sides; j++)
                Tri(s_MountainNear, grid[0, j], grid[0, j + 1], top, Vector2.right, Vector2.right, Vector2.right, up);

            if (paved)
            {
                var slab = new MeshData();
                s_Platforms.Add((top, radius * 0.66f));
                Prism(slab, top + Vector3.up * 0.05f, radius * 0.66f, radius * 0.66f, 8, top.y + 0.05f, top.y - 0.8f, true);
                Pave(slab);
            }

            Clear(top, radius + 40f);
        }

        // Raio do cone da ilha a 't' (0 = topo, 1 = ponta) da altura dela
        static float SkyIslandRadius(float radius, float t) => radius * (1f - Mathf.Pow(t, 1.5f)) + 0.6f;

        // Anel de pedra deitado que gira em volta do eixo vertical (o que segura as ilhas)
        static void TurningRing(string name, Vector3 center, float radius, float thickness, float width, float fromDeg,
            float toDeg, float speed)
        {
            var ring = new RuinMesh(s_Random.Next());
            ArquipelagoRuinKit.RichRing(ring, Vector3.zero, Vector3.up, Vector3.forward, radius, thickness, width, fromDeg, toDeg,
                s_Random, false, false, -1000f, 3);
            Transform root = CreateRuin(name, ring, false);
            root.position = center;
            Dynamic(root);
            var spin = root.gameObject.AddComponent<SlowRotation>();
            spin.Axis = Vector3.up;
            spin.DegreesPerSecond = speed;
            spin.Bob = 0f;
        }

        // ---------- pontes ----------

        // Ponte reta sobre pilares de cantaria com arcos: style 0 ponte de pedra (meio-fio partido, pilar de cantaria em
        // cima de um pilar de rocha), 1 aqueduto (canal com mureta e o gelo da água congelada), 2 ponte colossal
        // (parapeito; pilares de cantaria inteiros, do tabuleiro até dentro da nuvem, e arcos grandes). Rampa invisível
        // lisa por cima.
        static void Arcade(Vector3 from, Vector3 to, float width, float spacing, int style)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            float length = flat.magnitude;
            Quaternion rotation = Quaternion.LookRotation(flat);
            Vector3 right = rotation * Vector3.right, forward = flat / length;
            float deck = style == 2 ? 2.5f : 1.2f;
            int blocks = Mathf.CeilToInt(length / 3f);
            float segment = length / blocks;
            for (int b = 0; b < blocks; b++)
            {
                Vector3 c = Vector3.Lerp(from, to, (b + 0.5f) / blocks);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c + Vector3.down * (deck * 0.5f), new Vector3(width, deck, segment + 0.02f), rotation, 0.06f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 edge = c + right * s * (width * 0.5f - 0.4f);
                    if (style == 1)
                        BlockMesh.ChamferBox(s_Ruins.Pick(), edge + Vector3.up * 0.7f, new Vector3(0.8f, 1.4f, segment + 0.02f), rotation, 0.05f);
                    else if (s_Random.NextDouble() < (style == 2 ? 0.9 : 0.7))
                        BlockMesh.ChamferBox(s_Ruins.Pick(), edge + Vector3.up * (style == 2 ? 0.6f : 0.35f),
                            new Vector3(0.8f, style == 2 ? 1.2f : 0.7f, segment * R(0.6f, 0.95f)), rotation, 0.05f);
                }

                if (style == 1)
                    BlockMesh.Box(s_Ice, c + Vector3.up * 0.06f, new Vector3(width - 1.8f, 0.12f, segment + 0.02f), rotation);
                if (s_Random.NextDouble() < 0.5)
                    Spike(s_Ice, c + right * R(-width * 0.4f, width * 0.4f) + Vector3.down * (deck + 0.1f), Vector3.down,
                        R(0.6f, 2.4f), R(0.12f, 0.3f), 5, 0f, 3f);
            }

            SolidRamp(s_PathColliders, from, to, width);

            // Pilares
            int spans = Mathf.Max(2, Mathf.RoundToInt(length / spacing));
            float pierWidth = width * (style == 0 ? 0.8f : 0.9f), pierDepth = style == 2 ? 8f : style == 1 ? 3.6f : 3f;
            var tops = new List<Vector3>();
            for (int k = 1; k < spans; k++)
            {
                Vector3 top = Vector3.Lerp(from, to, k / (float)spans) + Vector3.down * deck;
                float masonry = style == 0 ? 6f : style == 1 ? 26f : top.y - (k_CloudY - 40f);
                ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), top + Vector3.down * (masonry * 0.5f), new Vector2(pierWidth, pierDepth), masonry,
                    rotation, 0.1f);
                if (style == 2)
                    for (float y = 14f; y < masonry - 20f; y += 16f)   // cornijas ao longo do pilar
                        BlockMesh.ChamferBox(s_Ruins.Pick(), top + Vector3.down * y, new Vector3(pierWidth + 1.2f, 1f, pierDepth + 1.2f), rotation, 0.1f);
                else
                {
                    float r = Mathf.Max(pierWidth, pierDepth) * 0.75f + 1.5f;
                    PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                    {
                        Top = top + Vector3.down * (masonry - 0.5f), TopRadius = r, BaseRadius = r * 2.2f, Taper = 2.5f,
                        Cap = PicosMountainKit.Cap.Flat, Sides = 14, RingStep = 0f, Corners = 5, Flutes = 3, FluteDepth = 0.08f,
                        BedHeight = R(8f, 14f), BedShift = 0.08f, Waist = 0.1f, Roughness = 0.08f, Seed = R(0f, 1000f), Bundle = false,
                    });
                }

                tops.Add(top);
            }

            // Arcos de aduelas entre os pilares (e das pontas até o primeiro e o último), logo abaixo do tabuleiro
            if (style != 0)
            {
                float thickness = style == 2 ? 3.2f : 1.6f;
                float radius = length / spans * 0.5f - pierDepth * 0.5f + thickness * 0.5f;
                var springs = new List<Vector3>(tops);
                springs.Insert(0, from + Vector3.down * deck - forward * pierDepth * 0.5f);
                springs.Add(to + Vector3.down * deck + forward * pierDepth * 0.5f);
                for (int k = 0; k + 1 < springs.Count; k++)
                {
                    Vector3 mid = (springs[k] + springs[k + 1]) * 0.5f;
                    ArquipelagoRuinKit.RichRing(s_Ruins, mid + Vector3.down * (radius + thickness * 0.5f - 0.3f), -right, forward, radius,
                        thickness, pierWidth * 0.95f, 0f, 180f, s_Random, false, false, mid.y - 2f * radius - 10f, 4);
                }
            }

            ClearLine(from, to);
        }

        // Distância no plano de 'p' até o segmento 'a'-'b'
        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var p2 = new Vector2(p.x, p.z);
            var a2 = new Vector2(a.x, a.z);
            var b2 = new Vector2(b.x, b.z);
            Vector2 ab = b2 - a2;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p2, a2 + ab * t);
        }

        // ---------- casas ----------

        // Casa em bloco: corpo, porta e janelas (vãos escuros) e o telhado: plano com laje (rústica), de duas águas (45°) ou
        // íngreme (gótica, com torrezinha de canto e coruchéu se 'spire')
        static void House(RuinMesh m, Vector3 foot, Quaternion rotation, float w, float d, float h, Roof roof, bool spire)
        {
            Vector3 up = rotation * Vector3.up, forward = rotation * Vector3.forward, right = rotation * Vector3.right;
            BlockMesh.ChamferBox(m.Pick(), foot + up * (h * 0.5f - 0.6f), new Vector3(w, h + 1.2f, d), rotation, 0.1f);
            BlockMesh.Box(m.Dark, foot + forward * (d * 0.5f + 0.02f) + up * 1.2f, new Vector3(Mathf.Min(1.4f, w * 0.3f), 2.4f, 0.1f), rotation);
            float windowHeight = roof == Roof.Steep ? 2.2f : 1.3f;
            for (float y = 2.3f; y + windowHeight * 0.5f < h - 0.6f; y += 3.4f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    if (y < 3f && w < 6f)
                        continue;
                    foreach (float face in new[] { -1f, 1f })
                        BlockMesh.Box(m.Dark, foot + forward * face * (d * 0.5f + 0.02f) + right * s * w * 0.3f + up * y,
                            new Vector3(0.9f, windowHeight, 0.1f), rotation);
                }
            }

            switch (roof)
            {
                case Roof.Flat:
                    BlockMesh.ChamferBox(m.Pick(), foot + up * (h + 0.25f), new Vector3(w + 0.6f, 0.5f, d + 0.6f), rotation, 0.08f);
                    if (s_Random.NextDouble() < 0.35)
                        BlockMesh.ChamferBox(m.Pick(), foot + up * (h + 1.2f) + right * w * 0.3f - forward * d * 0.25f,
                            new Vector3(0.9f, 2f, 0.9f), rotation, 0.06f);
                    break;
                default:
                    Gable(m.Pick(), foot + up * h, rotation, w + 0.8f, d + 0.6f, roof == Roof.Steep ? w * 1.05f : w * 0.5f + 0.4f);
                    break;
            }

            if (spire)
            {
                Vector3 corner = foot + right * (w * 0.5f) + forward * (d * 0.5f);
                Prism(m.Pick(), corner, 1.3f, 1.3f, 6, foot.y + h + 2f, foot.y - 0.5f, true);
                Spike(m.Pick(), corner + up * (h + 2f), up, h * 0.6f + 4f, 1.5f, 6, 0f, 100f);
            }
        }

        // Telhado de duas águas: base retangular w x d centrada em 'baseCenter', cumeeira ao longo do comprimento (d)
        static void Gable(MeshData mesh, Vector3 baseCenter, Quaternion rotation, float w, float d, float rise)
        {
            Vector3 up = rotation * Vector3.up, side = rotation * Vector3.right;
            Vector3 r = side * (w * 0.5f), f = rotation * Vector3.forward * (d * 0.5f), top = up * rise;
            Vector3 lb = baseCenter - r - f, rb = baseCenter + r - f, lf = baseCenter - r + f, rf = baseCenter + r + f;
            Vector3 tb = baseCenter + top - f, tf = baseCenter + top + f;
            mesh.AddQuadAuto(lb, lf, tf, tb, (-side * rise + up * (w * 0.5f)).normalized, 4f);
            mesh.AddQuadAuto(rb, rf, tf, tb, (side * rise + up * (w * 0.5f)).normalized, 4f);
            mesh.AddTriangleAuto(lb, rb, tb, -(rotation * Vector3.forward), 4f);
            mesh.AddTriangleAuto(lf, rf, tf, rotation * Vector3.forward, 4f);
            mesh.AddQuadAuto(lb, rb, rf, lf, -up, 4f);
        }

        // Casas espalhadas numa coroa em volta de 'center' (de 'inner' a 'outer', no plano inclinado por 'tilt'), viradas
        // mais ou menos para o centro, sem se encostarem nem cobrir os corredores do centro até os pontos de 'keep'
        // (entradas, saídas, escadas); 'size' aumenta as casas (oficinas, quarteirões da cidade)
        static List<HouseSpot> ScatterHouses(RuinMesh m, Vector3 center, Quaternion tilt, float inner, float outer, int count,
            Roof roof, float spireChance, Vector3[] keep, float size = 1f, float tall = 1f)
        {
            var spots = new List<HouseSpot>();
            for (int attempt = 0; attempt < count * 40 && spots.Count < count; attempt++)
            {
                float w = (roof == Roof.Steep ? R(5f, 8f) : R(5.5f, 9f)) * size, d = R(5f, 8f) * size;
                float h = (roof == Roof.Steep ? R(7f, 12f) : roof == Roof.Flat ? R(3.8f, 6f) : R(4.5f, 7f)) * tall;
                float foot = 0.5f * Mathf.Sqrt(w * w + d * d);
                if (outer - foot < inner + foot)
                    continue;
                float a = R(0f, Mathf.PI * 2f), distance = R(inner + foot, outer - foot);
                var local = new Vector3(Mathf.Cos(a) * distance, 0f, Mathf.Sin(a) * distance);
                Vector3 p = center + tilt * local;
                bool blocked = false;
                foreach (HouseSpot other in spots)
                    blocked |= Vector3.Distance(p, other.Foot) < foot + 0.5f * Mathf.Sqrt(other.W * other.W + other.D * other.D) + 2.5f;
                if (keep != null)
                    foreach (Vector3 k in keep)
                        blocked |= SegmentDistance(p, center, k) < foot + 5f;
                if (blocked)
                    continue;
                float yaw = Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg + R(-20f, 20f);
                Quaternion rotation = tilt * Quaternion.Euler(0f, yaw, 0f);
                House(m, p, rotation, w, d, h, roof, s_Random.NextDouble() < spireChance);
                spots.Add(new HouseSpot { Foot = p, Rotation = rotation, W = w, D = d, H = h });
            }

            return spots;
        }

        // Torre de cantaria em andares que afinam um pouco, com cornijas e vãos de janela; ameias no alto se 'crenel'.
        // Devolve o centro do topo e a largura lá em cima.
        static (Vector3 Top, float Width) TowerShaft(RuinMesh m, Vector3 foot, float yaw, float width, float height, bool crenel)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            int levels = Mathf.Max(2, Mathf.RoundToInt(height / 12f));
            float levelHeight = (height + 2f) / levels, y = -2f, w = width;
            for (int i = 0; i < levels; i++)
            {
                BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * (y + levelHeight * 0.5f), new Vector3(w, levelHeight, w), rotation, 0.15f);
                BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * (y + levelHeight), new Vector3(w * 1.1f, 0.7f, w * 1.1f), rotation, 0.1f);
                if (y + levelHeight * 0.5f > 2f)
                    for (int face = 0; face < 4; face++)
                    {
                        Quaternion faceRotation = rotation * Quaternion.Euler(0f, face * 90f, 0f);
                        BlockMesh.Box(m.Dark, foot + Vector3.up * (y + levelHeight * 0.55f) + faceRotation * Vector3.forward * (w * 0.5f + 0.02f),
                            new Vector3(w * 0.16f, levelHeight * 0.4f, 0.1f), faceRotation);
                    }
                y += levelHeight;
                if (i < levels - 1)
                    w *= 0.94f;
            }

            Vector3 top = foot + Vector3.up * (y + 0.35f);
            if (crenel)
                for (int face = 0; face < 4; face++)
                {
                    Quaternion faceRotation = rotation * Quaternion.Euler(0f, face * 90f, 0f);
                    for (int k = 0; k < 3; k++)
                        BlockMesh.ChamferBox(m.Pick(), top + faceRotation * new Vector3((k - 1) * w * 0.36f, 0.6f, w * 0.5f),
                            new Vector3(w * 0.18f, 1.2f, 0.6f), faceRotation, 0.06f);
                }

            return (top, w * 1.1f);
        }

        // Engrenagem de pedra parada: aro, dentes, raios e cubo, no plano perpendicular a 'normal'
        static void Gear(RuinMesh m, Vector3 center, Vector3 normal, float radius)
        {
            normal = normal.normalized;
            Vector3 zero = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.Cross(normal, Vector3.up).normalized;
            float rim = radius * 0.16f, width = Mathf.Max(0.6f, radius * 0.18f);
            ArquipelagoRuinKit.RichRing(m, center, normal, zero, radius - rim * 0.5f, rim, width, 0f, 360f, s_Random, false, false,
                -1000f, 0);
            int teeth = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / (radius * 0.35f + 0.8f)));
            float tooth = radius * 0.14f + 0.3f;
            for (int k = 0; k < teeth; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 360f / teeth, normal) * zero;
                BlockMesh.ChamferBox(m.Pick(), center + dir * (radius + tooth * 0.4f), new Vector3(tooth, tooth * 1.2f, width),
                    Quaternion.LookRotation(normal, dir), 0.04f);
            }

            for (int k = 0; k < 4; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 90f + 45f, normal) * zero;
                BlockMesh.ChamferBox(m.Pick(), center + dir * (radius * 0.5f), new Vector3(rim * 0.8f, radius, width * 0.8f),
                    Quaternion.LookRotation(normal, dir), 0.04f);
            }

            BlockMesh.ChamferBox(m.Pick(), center, new Vector3(radius * 0.35f, radius * 0.35f, width * 1.4f),
                Quaternion.LookRotation(normal, zero), 0.06f);
        }

        // ---------- vilas ----------

        // Casas do kit das Vilas do Alto (VilasDoAlto_Kit/AR_Casas): largura da frente e fundo, com folga para os degraus,
        // os contrafortes e a neve encostada
        static readonly (string Model, float W, float D)[] k_KitHouses =
        {
            ("AR_Casa_Empena", 8.6f, 10.6f), ("AR_Casa_Larga", 12.6f, 9f), ("AR_Casa_Torre", 8.4f, 8.4f),
            ("AR_Casa_Terraco", 10.2f, 9.7f), ("AR_Casa_Esquina", 11.2f, 10.2f),
        };

        static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        static Vector3 Around(float angle) => new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        // Diferença entre dois ângulos (radianos), de 0 a pi
        static float AngleGap(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

        // Casas do kit em fila pela beira de uma vila, de costas para o vazio e de frente para o meio, sem grade (folgas e
        // tipos sorteados, cada uma um pouco para dentro ou para fora): fora do lado da vista ('view', meia abertura
        // 'viewHalf' em graus; ali fica só o parapeito), das saídas ('gates', pontos dentro da vila na direção de cada
        // caminho) e de 'avoid'; no máximo 'count' casas
        static List<HouseSpot> KitHouseRing(Transform root, Node n, Vector3 view, float viewHalf, List<Vector3> gates, Vector3[] avoid,
            float avoidRadius, int count)
        {
            var spots = new List<HouseSpot>();
            float viewAngle = Mathf.Atan2(view.x, view.z), half = viewHalf * Mathf.Deg2Rad;
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));

            float angle = viewAngle + half, end = viewAngle + Mathf.PI * 2f - half;
            while (angle < end && spots.Count < count)
            {
                (string model, float w, float d) = k_KitHouses[s_Random.Next(k_KitHouses.Length)];
                float radius = n.Radius - d * 0.5f - R(1f, 2.6f);
                float center = angle + w * 0.5f / radius;
                if (center + w * 0.5f / radius > end)
                    break;
                bool blocked = false;
                foreach (float gate in gateAngles)
                    blocked |= AngleGap(center, gate) < (w * 0.5f + 5f) / radius;
                Vector3 p = n.Center + Around(center) * radius;
                if (avoid != null)
                    foreach (Vector3 a in avoid)
                        blocked |= Vector3.Distance(p, a) < avoidRadius + Mathf.Max(w, d) * 0.5f;
                if (blocked)
                {
                    angle += 2f / radius;
                    continue;
                }

                float yaw = YawOf(-Around(center)) + R(-8f, 8f);
                PicosGothicKit.Place(s_Materials, model, p, yaw, root, true, PicosGothicKit.VillagesFolder);
                spots.Add(new HouseSpot { Foot = p, Rotation = Quaternion.Euler(0f, yaw, 0f), W = w, D = d, H = 8f });
                angle += (w + R(1.2f, 4f)) / radius;
            }

            return spots;
        }

        // Parapeito do kit pela beira de uma vila (trechos de 4 m com pilaretes), aberto nas saídas
        static void KitParapet(Transform root, Node n, List<Vector3> gates)
        {
            float radius = n.Radius - 0.5f;
            int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / 4f));
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count;
                bool open = false;
                foreach (float gate in gateAngles)
                    open |= AngleGap(angle, gate) < 5f / radius;
                if (!open)
                    PicosGothicKit.Place(s_Materials, "PR_Parapeito", n.Center + Around(angle) * radius, YawOf(Around(angle)), root, true,
                        PicosGothicKit.VillagesFolder);
            }
        }

        // Vila Escalonada: a chegada, na ponta do contraforte, com dois terraços mais baixos na frente (escadas da planta).
        // Casas góticas do kit inteiras e só abandonadas (portas trancadas, venezianas) em fila pela beira de trás de cada
        // nível, de frente para a praça; o lado virado para a cidade fica aberto, só com o parapeito, para a vista. Na
        // praça de cima, a fonte congelada no meio, barracas de feira largadas e postes; o portal gêmeo inteiro no fundo,
        // virado para a cidade; devolve a chegada
        static Transform BuildStepVillage(Node main, Node t1, Node t2, Vector3 city, Dictionary<string, List<Vector3>> exits)
        {
            Vector3 f = Flat(city - main.Center);
            Vector3 portal = main.Center - f * (main.Radius * 0.62f);
            var root = new GameObject("Vila_Escalonada").transform;
            root.SetParent(s_World, false);

            KitHouseRing(root, main, f, 62f, exits["Escalonada"], new[] { portal }, 13f, 12);
            KitParapet(root, main, exits["Escalonada"]);
            foreach ((Node terrace, string name) in new[] { (t1, "Escalonada_T1"), (t2, "Escalonada_T2") })
            {
                KitHouseRing(root, terrace, Flat(city - terrace.Center), 80f, exits[name], null, 0f, 3);
                KitParapet(root, terrace, exits[name]);
            }

            // Praça de cima: fonte, barracas viradas para ela e postes, fora dos caminhos das saídas
            string kit = PicosGothicKit.VillagesFolder;
            PicosGothicKit.Place(s_Materials, "PR_Fonte_Congelada", main.Center, YawOf(f), root, true, kit);
            var gateAngles = new List<float>();
            foreach (Vector3 g in exits["Escalonada"])
                gateAngles.Add(Mathf.Atan2(g.x - main.Center.x, g.z - main.Center.z));
            float view = Mathf.Atan2(f.x, f.z);
            foreach ((float deg, float distance, string model) in new[]
                     { (58f, 15f, "PR_Barraca"), (-66f, 14f, "PR_Barraca"), (118f, 17f, "PR_Barraca"), (30f, 22f, "PR_Poste"),
                       (-30f, 22f, "PR_Poste"), (95f, 25f, "PR_Poste"), (-95f, 25f, "PR_Poste"), (150f, 21f, "PR_Poste"),
                       (-150f, 21f, "PR_Poste") })
            {
                float angle = view + deg * Mathf.Deg2Rad;
                bool blocked = false;
                foreach (float gate in gateAngles)
                    blocked |= AngleGap(angle, gate) < 0.3f;
                if (blocked)
                    continue;
                Vector3 p = main.Center + Around(angle) * distance;
                PicosGothicKit.Place(s_Materials, model, p, YawOf(-Around(angle)) + R(-10f, 10f), root, true,
                    model == "PR_Poste" ? PicosGothicKit.Folder : kit);
            }

            return IntactPortal(portal, f, "VilasDoAlto");
        }

        // Portal gêmeo do da praça da chegada, inteiro: estrado em degraus, anel parado, dois anéis girando, superfície
        // acesa e monólitos de runas; 'facing' é para onde a superfície olha. Devolve o ponto de chegada na frente dele.
        static Transform IntactPortal(Vector3 floor, Vector3 facing, string name)
        {
            Quaternion rotation = Quaternion.LookRotation(facing);
            Vector3 right = rotation * Vector3.right;
            Vector3 center = floor + Vector3.up * 9.5f;
            var m = new RuinMesh(s_Random.Next());
            for (int k = 0; k < 3; k++)
                BlockMesh.ChamferBox(m.Pick(), floor + Vector3.up * (0.3f + k * 0.6f) - facing * (k * 0.6f),
                    new Vector3(22f - k * 4f, 0.6f, 6f - k * 1.2f), rotation, 0.08f);
            ArquipelagoRuinKit.RichRing(m, center, facing, Vector3.up, 9.5f, 1.6f, 2f, 0f, 360f, s_Random, false, false, 0f, 3);
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(m.Pick(), floor + right * s * 4.5f + Vector3.up * 2.6f, new Vector3(3f, 2.8f, 2.6f),
                    rotation * Quaternion.Euler(0f, 0f, s * -25f), 0.1f);

            var runes = new MeshData();
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = floor + right * s * 8.5f - facing * 0.5f;
                BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * 2.5f, new Vector3(2f, 5f, 1.4f), rotation, 0.1f);
                BlockMesh.Box(runes, foot + Vector3.up * 2.8f + facing * 0.72f, new Vector3(1.4f, 3.4f, 0.04f), rotation);
            }

            CreateRuin("Portal_" + name, m, true);
            GridSceneKit.CreateObject($"{k_Prefix}_Runas_{name}", "Runas_" + name, runes, s_Materials.Rune, s_World, false, false);

            foreach ((string ring, float radius, float thickness, float width, float speed) in new[]
                     { ("Giro_1", 7.6f, 0.9f, 1.2f, 14f), ("Giro_2", 6f, 0.6f, 0.8f, -22f) })
            {
                var mesh = new RuinMesh(s_Random.Next());
                ArquipelagoRuinKit.RichRing(mesh, Vector3.zero, Vector3.forward, Vector3.up, radius, thickness, width, 0f, 360f,
                    s_Random, false, false, -100f, 2);
                Transform root = CreateRuin($"Portal_{name}_{ring}", mesh, false, snow: false);
                root.SetPositionAndRotation(center, rotation);
                Dynamic(root);
                var spin = root.gameObject.AddComponent<SlowRotation>();
                spin.Axis = facing;
                spin.DegreesPerSecond = speed;
                spin.Bob = 0f;
            }

            GameObject surface = GridSceneKit.CreateObject($"{k_Prefix}_Portal_{name}", "Portal_Superficie_" + name, PortalDisc(5.4f),
                s_Materials.Portal, s_World, false, false, false);
            surface.transform.SetPositionAndRotation(center, rotation);
            Dynamic(surface.transform);
            var discSpin = surface.AddComponent<SlowRotation>();
            discSpin.Axis = facing;
            discSpin.DegreesPerSecond = 8f;
            discSpin.Bob = 0f;
            Light(center + facing * 2f, new Color(0.35f, 1f, 0.95f), 5f, 28f);

            var point = new GameObject("Chegada_" + name).transform;
            point.SetParent(s_World, false);
            point.SetPositionAndRotation(floor + facing * 8f + Vector3.up * 0.3f, Quaternion.LookRotation(facing));
            return point;
        }

        // Vila dos Sinos: casas de duas águas em volta do campanário, na sela
        static void BuildBellVillage(Node n, Vector3[] keep)
        {
            var m = new RuinMesh(3301);
            Campanile(m, n.Center, 8f, 26f);
            ScatterHouses(m, n.Center, Quaternion.identity, 12f, n.Radius * 0.95f, 12, Roof.Gable, 0f, keep);
            CreateRuin("Vila_dos_Sinos", m, true);
        }

        // Campanário: fuste quadrado, cornija, quatro pilares abertos com o sino pendurado na viga e o telhado em pirâmide
        static void Campanile(RuinMesh m, Vector3 foot, float size, float height)
        {
            BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * (height * 0.5f - 0.5f), new Vector3(size, height + 1f, size), Quaternion.identity, 0.12f);
            BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * height, new Vector3(size + 0.8f, 0.6f, size + 0.8f), Quaternion.identity, 0.08f);
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                BlockMesh.ChamferBox(m.Pick(), foot + new Vector3(sx * (size * 0.5f - 0.6f), height + 3.3f, sz * (size * 0.5f - 0.6f)),
                    new Vector3(1.2f, 6f, 1.2f), Quaternion.identity, 0.06f);
            BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * (height + 5.6f), new Vector3(size - 1.4f, 0.5f, 0.5f), Quaternion.identity, 0.04f);
            Prism(m.Pick(), foot, 0.8f, 1.5f, 10, foot.y + height + 5.3f, foot.y + height + 3f, true);
            BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * (height + 6.7f), new Vector3(size + 0.6f, 0.8f, size + 0.6f), Quaternion.identity, 0.08f);
            Spike(m.Pick(), foot + Vector3.up * (height + 7.1f), Vector3.up, size * 1.1f, size * 0.62f, 4, 45f, 100f);
        }

        // Vila das Engrenagens: oficinas grandes com engrenagens paradas nas fachadas, a engrenagem grande em pé num berço,
        // trilhos atravessando o ombro e a estação do teleférico na beira virada para a torre do relógio
        static void BuildGearVillage(Node n, Vector3[] keep, Vector3 clockPeak)
        {
            var m = new RuinMesh(3501);
            Vector3 across = Flat(clockPeak - n.Center), side = Vector3.Cross(Vector3.up, across);

            Vector3 cradle = n.Center - across * 4f;
            BlockMesh.ChamferBox(m.Pick(), cradle + Vector3.up * 1f, new Vector3(4f, 2f, 9f), Quaternion.LookRotation(side), 0.1f);
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(m.Pick(), cradle + side * s * 3.2f + Vector3.up * 3.5f, new Vector3(1.2f, 5f, 3f),
                    Quaternion.LookRotation(side), 0.08f);
            Gear(m, cradle + Vector3.up * 12.5f, across, 10f);

            Vector3 railA = n.Center - side * n.Radius * 0.9f, railB = n.Center + side * n.Radius * 0.9f;
            foreach (float s in new[] { -0.8f, 0.8f })
                Bar(railA + across * (s + 9f) + Vector3.up * 0.25f, railB + across * (s + 9f) + Vector3.up * 0.25f, 0.25f, 0.25f);
            for (float d = 0f; d <= Vector3.Distance(railA, railB); d += 1.6f)
                BlockMesh.ChamferBox(s_Wood, railA + side * d + across * 9f + Vector3.up * 0.06f, new Vector3(2.4f, 0.14f, 0.3f),
                    Quaternion.LookRotation(side), 0.02f);

            var allKeep = new List<Vector3>(keep) { railA + across * 9f, railB + across * 9f, n.Center + across * n.Radius };
            List<HouseSpot> shops = ScatterHouses(m, n.Center, Quaternion.identity, 16f, n.Radius * 0.95f, 10, Roof.Gable, 0f,
                allKeep.ToArray(), 1.35f, 1.2f);
            for (int i = 0; i < shops.Count; i += 2)
            {
                HouseSpot shop = shops[i];
                Vector3 front = shop.Rotation * Vector3.forward;
                float radius = Mathf.Min(shop.W, shop.H) * 0.32f;
                Gear(m, shop.Foot + front * (shop.D * 0.5f + 0.4f) + Vector3.up * (shop.H * 0.55f) + shop.Rotation * Vector3.right * (shop.W * 0.15f),
                    front, radius);
            }

            CreateRuin("Vila_das_Engrenagens", m, true);
        }

        // Vila-Ponte: ponte larga sobre a garganta entre as duas cabeceiras, casas dos dois lados da rua e arcos de pedra
        // por cima dela; duas casas em cada cabeceira
        static void BuildBridgeVillage(Node a, Node b, Vector3 from, Vector3 to, float width, Dictionary<string, List<Vector3>> exits)
        {
            var m = new RuinMesh(3701);
            Arcade(from, to, width, 40f, 2);
            Vector3 along = Flat(to - from), right = Vector3.Cross(Vector3.up, along);
            float length = Vector3.Distance(from, to);
            foreach (float s in new[] { -1f, 1f })
            {
                for (float d = 8f; d < length - 10f;)
                {
                    float w = R(7f, 10f), depth = R(6f, 7.5f), h = R(6f, 9f);
                    Vector3 foot = from + along * (d + w * 0.5f) + right * s * (4f + depth * 0.5f);
                    House(m, foot, Quaternion.LookRotation(-right * s), w, depth, h, Roof.Gable, false);
                    d += w + R(0.6f, 2f);
                }
            }

            for (float d = 18f; d < length - 12f; d += 26f)
            {
                Vector3 c = from + along * d;
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.ChamferBox(m.Pick(), c + right * s * 4.6f + Vector3.up * 2.5f, new Vector3(1.2f, 5f, 1.6f),
                        Quaternion.LookRotation(along), 0.06f);
                ArquipelagoRuinKit.RichRing(m, c + Vector3.up * 5f, along, right, 4.6f, 0.9f, 1.6f, 0f, 180f, s_Random, false, false,
                    c.y, 1000);
            }

            ScatterHouses(m, a.Center, Quaternion.identity, 4f, a.Radius * 0.95f, 2, Roof.Gable, 0f,
                new List<Vector3>(exits["Ponte_O"]) { from }.ToArray());
            ScatterHouses(m, b.Center, Quaternion.identity, 4f, b.Radius * 0.95f, 2, Roof.Gable, 0f,
                new List<Vector3>(exits["Ponte_L"]) { to }.ToArray());
            CreateRuin("Vila_Ponte", m, true);
        }

        // Vila Pendurada: casas no alto do paredão e uma em cada patamar do zigue-zague, encostada na parede de trás
        // (norte), com a frente para o vale; mãos-francesas de madeira sob a beira de cada patamar
        static void BuildHangingVillage(Node n, Vector3[] keep, List<Vector3> landings)
        {
            var m = new RuinMesh(3901);
            ScatterHouses(m, n.Center, Quaternion.identity, 8f, n.Radius * 0.95f, 4, Roof.Gable, 0f, keep);
            Quaternion south = Quaternion.LookRotation(Vector3.back);
            foreach (Vector3 landing in landings)
            {
                House(m, landing, south, R(5f, 6f), 5.5f, R(4.5f, 6.5f), s_Random.NextDouble() < 0.5 ? Roof.Flat : Roof.Gable, false);
                foreach (float s in new[] { -3f, 3f })
                    Beam(landing + new Vector3(s, -9f, 4f), landing + new Vector3(s, -0.4f, -8.4f), 0.35f, 0.35f);
            }

            CreateRuin("Vila_Pendurada", m, true);
        }

        // ---------- portão e cidade ----------

        // Portão da cidade (sala 22): arco monumental sobre o caminho para a ponte colossal, ladeado por duas torres de
        // vigia com ameias
        static void BuildGate(Node gate, Vector3 city)
        {
            var m = new RuinMesh(4001);
            Vector3 toCity = Flat(city - gate.Center), right = Vector3.Cross(Vector3.up, toCity);
            float yaw = Mathf.Atan2(toCity.x, toCity.z) * Mathf.Rad2Deg;
            Vector3 arch = gate.Center + toCity * 12f;
            foreach (float s in new[] { -1f, 1f })
            {
                BlockMesh.ChamferBox(m.Pick(), arch + right * s * 13f + Vector3.up * 9f, new Vector3(4f, 18f, 5f), Quaternion.LookRotation(toCity), 0.15f);
                BlockMesh.ChamferBox(m.Pick(), arch + right * s * 13f + Vector3.up * 1f, new Vector3(5.2f, 2f, 6.2f), Quaternion.LookRotation(toCity), 0.15f);
            }
            ArquipelagoRuinKit.RichRing(m, arch + Vector3.up * 18f, toCity, right, 13f, 3f, 5f, 0f, 180f, s_Random, false, false, gate.Center.y, 4);
            foreach (float s in new[] { -1f, 1f })
                TowerShaft(m, gate.Center + toCity * 15f + right * s * 18f, yaw, 7f, 30f, true);
            CreateRuin("Portao_da_Cidade", m, true);
        }

        // A cidade em esboço (a etapa 3 detalha pela referência): ilha grande, praça com as duas colunatas em semicírculo
        // abertas para a ponte, obelisco, a basílica com a cúpula do lado oposto, quarteirões góticos e torres em volta e
        // um anel enorme girando por baixo
        static void BuildCitySketch(Vector3 city, Vector3 toBridge)
        {
            var m = new RuinMesh(4101);
            SkyIsland(city, k_CityRadius, Quaternion.identity, 4101, false, 40);
            var slab = new MeshData();
            Prism(slab, city + Vector3.up * 0.1f, 46f, 46f, 24, city.y + 0.1f, city.y - 0.6f, true);
            Pave(slab);
            s_Platforms.Add((city, 46f));
            foreach ((float from, float to) in new[] { (28f, 152f), (208f, 332f) })
                ArquipelagoRuinKit.RichRing(m, city + Vector3.up * 6.5f, Vector3.up, toBridge, 52f, 7f, 13f, from, to, s_Random, false, false,
                    city.y, 1000);
            BlockMesh.ChamferBox(m.Pick(), city + Vector3.up * 11f, new Vector3(2.2f, 22f, 2.2f), Quaternion.identity, 0.1f);
            Spike(m.Pick(), city + Vector3.up * 22f, Vector3.up, 3f, 1.4f, 4, 45f, 100f);

            // Basílica e cúpula
            Quaternion facing = Quaternion.LookRotation(toBridge);
            Vector3 basilica = city - toBridge * 100f;
            BlockMesh.ChamferBox(m.Pick(), basilica + Vector3.up * 15f, new Vector3(40f, 32f, 64f), facing, 0.3f);
            BlockMesh.ChamferBox(m.Pick(), basilica + toBridge * 31f + Vector3.up * 18f, new Vector3(56f, 38f, 6f), facing, 0.3f);
            Vector3 dome = basilica - toBridge * 8f;
            float baseY = city.y + 31f;
            Prism(m.Pick(), dome, 15f, 15f, 16, baseY + 14f, baseY, true);
            const float domeRadius = 16f;
            for (int i = 0; i < 6; i++)
            {
                float p0 = i * 15f * Mathf.Deg2Rad, p1 = (i + 1) * 15f * Mathf.Deg2Rad;
                Prism(m.Pick(), dome, domeRadius * Mathf.Cos(p1) + 0.4f, domeRadius * Mathf.Cos(p0), 16,
                    baseY + 14f + domeRadius * Mathf.Sin(p1), baseY + 14f + domeRadius * Mathf.Sin(p0), i == 5);
            }

            Prism(m.Pick(), dome, 2.6f, 2.6f, 8, baseY + 14f + domeRadius + 7f, baseY + 14f + domeRadius - 1f, true);
            Spike(m.Pick(), dome + Vector3.up * (baseY + 14f + domeRadius + 7f - city.y), Vector3.up, 6f, 1.2f, 8, 0f, 100f);

            // Quarteirões e torres
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            Vector3[] keep =
            {
                city + toBridge * (k_CityRadius + 10f), city - toBridge * (k_CityRadius + 10f),
                city - toBridge * (k_CityRadius + 10f) + across * 16f, city - toBridge * (k_CityRadius + 10f) - across * 16f,
            };
            ScatterHouses(m, city, Quaternion.identity, 62f, k_CityRadius * 0.86f, 60, Roof.Steep, 0.4f, keep, 2.2f, 2f);
            for (int k = 0; k < 7; k++)
            {
                float a = R(0f, 360f), d = R(70f, 135f);
                Vector3 foot = city + Quaternion.Euler(0f, a, 0f) * Vector3.forward * d;
                if (SegmentDistance(foot, keep[0], keep[1]) < 30f)
                    continue;
                (Vector3 top, float width) = TowerShaft(m, foot, R(0f, 90f), R(8f, 12f), R(36f, 64f), false);
                Spike(m.Pick(), top, Vector3.up, width * 2.2f, width * 0.68f, 4, 45f, 100f);
            }

            TurningRing("Anel_Cidade", city + Vector3.down * 70f, 205f, 7f, 10f, 0f, 360f, 1.2f);
            CreateRuin("Cidade_Esboco", m, true);
            Clear(city, k_CityRadius + 60f);
        }

        // ---------- cenário: ilhas voadoras ----------

        // Ilha do Anel: casas góticas em volta de uma praça com um obelisco, sobre o anel que gira embaixo dela
        static void BuildRingIsle(Node n)
        {
            var m = new RuinMesh(3201);
            SkyIsland(n.Center, n.Radius, Quaternion.identity, 3201, true);
            float depth = n.Radius * 1.4f;
            TurningRing("Anel_IlhaDoAnel", n.Center + Vector3.down * (depth * 0.4f), SkyIslandRadius(n.Radius, 0.4f) + 6f, 3.2f, 4.5f,
                0f, 360f, 3f);
            ScatterHouses(m, n.Center, Quaternion.identity, 13f, n.Radius * 0.66f, 11, Roof.Steep, 0.35f, null);
            BlockMesh.ChamferBox(m.Pick(), n.Center + Vector3.up * 0.4f, new Vector3(3.2f, 0.8f, 3.2f), Quaternion.identity, 0.1f);
            BlockMesh.ChamferBox(m.Pick(), n.Center + Vector3.up * 5f, new Vector3(1.4f, 8.4f, 1.4f), Quaternion.identity, 0.1f);
            Spike(m.Pick(), n.Center + Vector3.up * 9.2f, Vector3.up, 2f, 0.9f, 4, 45f, 100f);
            CreateRuin("Ilha_do_Anel", m, true);
        }

        // Cacho de Ilhotas: seis ilhotas em fila torta, uma ou duas casas góticas em cada, ligadas por pontes de corrente,
        // sobre um anel partido que gira
        static void BuildIsletCluster(Vector3 center, Vector3 fromSide, Vector3 toSide)
        {
            var m = new RuinMesh(3401);
            Vector3 along = Flat(toSide - fromSide), side = Vector3.Cross(Vector3.up, along);
            var offsets = new[]
            {
                new Vector3(-50f, 8f, 0f), new Vector3(-30f, -11f, 3f), new Vector3(-9f, 12f, -2f), new Vector3(11f, -9f, 4f),
                new Vector3(31f, 10f, 0f), new Vector3(50f, -4f, -3f),
            };
            var islets = new Node[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 o = offsets[i];
                islets[i] = Peak(center + along * o.x + side * o.y + Vector3.up * o.z, R(11f, 14f));
                SkyIsland(islets[i].Center, islets[i].Radius, Quaternion.identity, 3401 + i, true, 16);
            }

            for (int i = 0; i < islets.Length; i++)
            {
                var keep = new List<Vector3>();
                if (i > 0)
                    keep.Add(Rim(islets[i], islets[i - 1].Center));
                if (i < islets.Length - 1)
                    keep.Add(Rim(islets[i], islets[i + 1].Center));
                ScatterHouses(m, islets[i].Center, Quaternion.identity, 0f, islets[i].Radius * 0.62f, 1 + s_Random.Next(2), Roof.Steep,
                    0.5f, keep.ToArray());
                if (i > 0)
                {
                    Vector3 p = Rim(islets[i - 1], islets[i].Center), q = Rim(islets[i], islets[i - 1].Center);
                    ChainBridge(p, q, 3f, Mathf.Clamp(Vector3.Distance(p, q) * 0.05f, 0.8f, 2.5f));
                }
            }

            TurningRing("Anel_CachoDeIlhotas", center + Vector3.down * 38f, 66f, 3.6f, 5f, 0f, 290f, 2f);
            CreateRuin("Cacho_de_Ilhotas", m, true);
            Clear(center, 110f);
        }

        // Ilha do Aqueduto: casas góticas e uma caixa-d'água redonda, presa a dois picos vizinhos pelos aquedutos
        static void BuildAqueductIsle(Node n, Vector3 peakA, Vector3 peakB)
        {
            var m = new RuinMesh(3601);
            SkyIsland(n.Center, n.Radius, Quaternion.identity, 3601, true);
            Vector3 tank = n.Center + Vector3.forward * 3f;
            Prism(m.Pick(), tank, 4f, 4.4f, 12, tank.y + 12f, tank.y - 0.5f, true);
            Prism(m.Pick(), tank, 4.8f, 4.8f, 12, tank.y + 12.8f, tank.y + 12f, true);
            var keep = new List<Vector3>();
            foreach (Vector3 peak in new[] { peakA, peakB })
            {
                Plateau(peak, 14f, s_Random.Next(10000));
                Vector3 rim = Rim(n, peak);
                keep.Add(rim);
                Arcade(Rim(Peak(peak, 14f), n.Center), rim, 6f, 24f, 1);
            }

            ScatterHouses(m, n.Center, Quaternion.identity, 11f, n.Radius * 0.66f, 9, Roof.Steep, 0.3f, keep.ToArray());
            CreateRuin("Ilha_do_Aqueduto", m, true);
        }

        // Ilha Tombada: a ilha inclinada caída em cima de um pico (a ponta dele entra na rocha de baixo), casas góticas
        // tortas com ela e o anel parado, encalhado no cone da ilha
        static void BuildFallenIsle(Node n)
        {
            var m = new RuinMesh(3801);
            float depth = n.Radius * 1.4f;
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = n.Center + Vector3.down * (depth * 0.55f), TopRadius = 15f, BaseRadius = 30f, Taper = 2.5f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 20, RingStep = 3f, Corners = 6, Flutes = 3, FluteDepth = 0.06f,
                BedHeight = R(9f, 13f), BedShift = 0.06f, Waist = 0.08f, Roughness = 0.06f, Seed = 3801f, Bundle = false,
            });
            SkyIsland(n.Center, n.Radius, n.Tilt, 3801, false);
            Vector3 up = n.Tilt * Vector3.up;
            const float t = 0.3f;
            ArquipelagoRuinKit.RichRing(m, n.Center - up * (depth * t + 1.5f), up, n.Tilt * Vector3.forward,
                SkyIslandRadius(n.Radius, t) + 1f, 3.2f, 4.5f, 20f, 300f, s_Random, false, false, -1000f, 3);
            ScatterHouses(m, n.Center, n.Tilt, 10f, n.Radius * 0.66f, 9, Roof.Steep, 0.3f, null);
            CreateRuin("Ilha_Tombada", m, true);
        }

        // ---------- cenário: torres ----------

        // Torre do relógio num pico sozinho: fuste alto, um mostrador em cada face (ponteiros parados), campanário e flecha
        static void BuildClockTower(Vector3 peak)
        {
            var m = new RuinMesh(5001);
            Plateau(peak, 16f, 5001);
            (Vector3 top, float width) = TowerShaft(m, peak, 0f, 12f, 58f, false);
            float wall = width / 1.1f * 0.5f;
            for (int face = 0; face < 4; face++)
            {
                Quaternion rotation = Quaternion.Euler(0f, face * 90f, 0f);
                Vector3 normal = rotation * Vector3.forward;
                Vector3 c = top + Vector3.down * 8f + normal * wall;
                BlockMesh.Box(m.Dark, c + normal * 0.05f, new Vector3(7f, 7f, 0.1f), rotation);
                ArquipelagoRuinKit.RichRing(m, c + normal * 0.3f, normal, Vector3.up, 3.6f, 0.6f, 0.6f, 0f, 360f, s_Random, false, false,
                    -1000f, 1000);
                Bar(c + normal * 0.45f, c + normal * 0.45f + Quaternion.AngleAxis(-60f, normal) * Vector3.up * 2f, 0.3f, 0.15f);
                Bar(c + normal * 0.5f, c + normal * 0.5f + Quaternion.AngleAxis(40f, normal) * Vector3.up * 3f, 0.2f, 0.12f);
            }

            Campanile(m, top + Vector3.down * 0.4f, width * 0.85f, 0.6f);
            CreateRuin("Torre_do_Relogio", m, true);
        }

        // Torre de vigia esguia com balcão; uma delas com a ponte de corrente partida pendurada (duas correntes caídas)
        static void BuildWatchTower(Vector3 peak, int seed, bool brokenBridge, Vector3 city)
        {
            var m = new RuinMesh(seed);
            Plateau(peak, 10f, seed);
            Vector3 toCity = Flat(city - peak);
            (Vector3 top, float width) = TowerShaft(m, peak, Mathf.Atan2(toCity.x, toCity.z) * Mathf.Rad2Deg, 6.5f, 34f, true);
            Vector3 balcony = top + Vector3.down * 7f;
            BlockMesh.ChamferBox(m.Pick(), balcony, new Vector3(width + 3f, 0.6f, width + 3f), Quaternion.LookRotation(toCity), 0.08f);
            if (brokenBridge)
            {
                Vector3 side = Vector3.Cross(Vector3.up, toCity);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 anchor = balcony + toCity * (width * 0.5f + 1.3f) + side * s * 1.4f;
                    ChainLinks(t => anchor + toCity * (Mathf.Sin(t * Mathf.PI * 0.5f) * 3f) + Vector3.down * (t * 24f), 0.55f);
                }
            }

            CreateRuin($"Torre_de_Vigia_{seed}", m, true);
        }

        // Farol de runas: torre com a lanterna de painéis de runa acesos e a luz ciano que se vê de longe
        static void BuildRuneBeacon(Vector3 peak)
        {
            var m = new RuinMesh(5201);
            Plateau(peak, 14f, 5201);
            (Vector3 top, float width) = TowerShaft(m, peak, 20f, 9f, 44f, false);
            var runes = new MeshData();
            Quaternion yaw = Quaternion.Euler(0f, 20f, 0f);
            float half = width * 0.38f;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                BlockMesh.ChamferBox(m.Pick(), top + yaw * new Vector3(sx * half, 2.6f, sz * half), new Vector3(1f, 5.2f, 1f), yaw, 0.05f);
            for (int face = 0; face < 4; face++)
            {
                Quaternion rotation = yaw * Quaternion.Euler(0f, face * 90f, 0f);
                BlockMesh.Box(runes, top + rotation * new Vector3(0f, 2.6f, half - 0.3f), new Vector3(half * 1.4f, 3.8f, 0.08f), rotation);
            }

            BlockMesh.ChamferBox(m.Pick(), top + Vector3.up * 5.6f, new Vector3(width, 0.8f, width), yaw, 0.08f);
            Spike(m.Pick(), top + Vector3.up * 6f, Vector3.up, width * 1.2f, width * 0.7f, 4, 65f, 100f);
            GridSceneKit.CreateObject(k_Prefix + "_Farol_Runas", "Farol_Runas", runes, s_Materials.Rune, s_World, false, false);
            Light(top + Vector3.up * 2.6f, new Color(0.35f, 1f, 0.95f), 8f, 90f);
            CreateRuin("Farol_de_Runas", m, true);
        }

        // Torre gêmea partida: duas torres ligadas no alto por um arco; a da direita quebrou no meio e o arco ficou só
        // do lado da inteira, com a ponta partida (os pedaços caíram no topo do pico)
        static void BuildTwinTowers(Vector3 peak, Vector3 city)
        {
            var m = new RuinMesh(5301);
            Plateau(peak, 22f, 5301);
            Vector3 toCity = Flat(city - peak), right = Vector3.Cross(Vector3.up, toCity);
            float yaw = Mathf.Atan2(toCity.x, toCity.z) * Mathf.Rad2Deg;
            (Vector3 topA, float widthA) = TowerShaft(m, peak - right * 12f, yaw, 8f, 46f, true);
            TowerShaft(m, peak + right * 12f, yaw, 8f, 22f, false);
            ArquipelagoRuinKit.RichRing(m, peak + Vector3.up * (topA.y - peak.y - 6f), toCity, right, 12f, 2.2f, 3f, 100f, 180f, s_Random,
                true, false, peak.y, 3);
            for (int k = 0; k < 5; k++)
                ArquipelagoRuinKit.IrregularRock(m, peak + right * R(4f, 14f) + toCity * R(-8f, 8f) + Vector3.up * 0.4f, R(0.8f, 1.6f), s_Random);
            CreateRuin("Torre_Gemea_Partida", m, true);
        }

        // Teleférico parado entre a Vila das Engrenagens e a torre do relógio: um pórtico de pedra com roda em cada
        // ponta, dois cabos de ferro cedendo e duas cabines de madeira penduradas, uma em cada cabo
        static void Cableway(Vector3 a, Vector3 b)
        {
            var m = new RuinMesh(5401);
            Vector3 along = Flat(b - a), side = Vector3.Cross(Vector3.up, along);
            foreach ((Vector3 end, Vector3 back) in new[] { (a, -along), (b, along) })
            {
                Vector3 foot = end + back * 2f;
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.ChamferBox(m.Pick(), foot + side * s * 3f + Vector3.up * 4.5f, new Vector3(1.2f, 9f, 1.2f), Quaternion.LookRotation(along), 0.08f);
                BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * 9.3f, new Vector3(7.6f, 0.9f, 1.4f), Quaternion.LookRotation(along), 0.08f);
                Gear(m, foot + Vector3.up * 10.2f, Vector3.up, 2.6f);
            }

            float length = Vector3.Distance(a, b), sag = length * 0.05f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 Cable(float t) => Vector3.Lerp(a, b, t) + side * s * 2.6f + Vector3.up * 10.2f + Vector3.down * (sag * 4f * t * (1f - t));
                const int pieces = 24;
                for (int i = 0; i < pieces; i++)
                    Bar(Cable(i / (float)pieces), Cable((i + 1f) / pieces), 0.12f, 0.12f);
                float at = s < 0f ? 0.32f : 0.68f;
                Vector3 hook = Cable(at);
                Bar(hook, hook + Vector3.down * 2.2f, 0.12f, 0.12f);
                Vector3 cabin = hook + Vector3.down * 3.6f;
                BlockMesh.ChamferBox(s_Wood, cabin, new Vector3(2.4f, 2.6f, 3.2f), Quaternion.LookRotation(along), 0.08f);
                BlockMesh.ChamferBox(m.Pick(), cabin + Vector3.up * 1.5f, new Vector3(2.8f, 0.4f, 3.6f), Quaternion.LookRotation(along), 0.06f);
                foreach (float face in new[] { -1f, 1f })
                    BlockMesh.Box(m.Dark, cabin + side * face * 1.22f + Vector3.up * 0.3f, new Vector3(0.1f, 1f, 2.2f), Quaternion.LookRotation(along));
            }

            CreateRuin("Teleferico", m, true);
        }
    }
}
