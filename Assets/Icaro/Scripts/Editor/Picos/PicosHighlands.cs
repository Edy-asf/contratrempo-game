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
    //   Vila da Ressonância, antes Vila dos Sinos (na sela entre dois cumes, em volta do campanário),
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
        // A cidade dobrou de tamanho (pedido do usuário em 2026-11-01: a cidade e a basílica com o dobro do tamanho). O centro
        // recuou na linha da ponte para ela continuar entrando k_CityBridgeIn na ilha, sem chegar perto do portão
        const float k_CityTop = 30f, k_CityRadius = 340f, k_CityBridgeIn = 20f;
        static Transform s_HighArrival;

        [System.Serializable]
        class HighLayout
        {
            public HighVillage[] villages;
            public HighSegment[] segments;
            public HighLanding[] landings;
            public HighSpan deck, bridge;
            public HighRoad[] roads;    // caminhos pavimentados entre as vilas (PicosRoad)
            public HighPlatform[] platforms;    // plataformas das voltas do zigue-zague da Vila Pendurada (PicosRoad)
            public HighRoad road;       // planta antiga: só o caminho do Terraço até a Escalonada
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
            public string Model;        // peça do kit (só as casas do kit)
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

            PlaceGorgeIce();

            // Sorteio próprio: o resto do mapa continua igual
            System.Random saved = s_Random;
            s_Random = new System.Random(2801);
            ResetAttackHouses();
            try
            {
                // A cidade fica longe da montanha, alinhada à Ponte do Reino, que é reta (2026-11-02, PicosRoyalBridge)
                Vector3 city = ReinoCity;
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
                    if (segment.route.EndsWith("-ponte"))      // vão entre colunas: ponte reta ou escada em ponte
                    {
                        float run = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                        if (Mathf.Abs(b.y - a.y) > run * 0.2f)
                            StairBridge(a.y < b.y ? a : b, a.y < b.y ? b : a, segment.width);
                        else
                            Bridge(a, b, segment.width, run > 14f);
                    }
                    else if (segment.stairs)
                        Stairs(a.y < b.y ? a : b, a.y < b.y ? b : a, segment.width);
                    else
                        Path(a, b, segment.width);
                    ClearLine(a, b);
                    foreach (KeyValuePair<string, Node> pair in villages)
                    foreach (Vector3 end in new[] { a, b })
                        if (Vector3.Distance(end, pair.Value.Center) < pair.Value.Radius + 1f)
                            exits[pair.Key].Add(end);
                }

                // Caminhos entre as vilas (do terraço do portal até o portão): cada um uma construção só (degraus, patamares,
                // rampas, arcos, balaústres)
                // As pontas de cada um são vilas ou plataformas do zigue-zague (montadas à parte)
                var platforms = new Dictionary<string, HighPlatform>();
                if (layout.platforms != null)
                    foreach (HighPlatform platform in layout.platforms)
                    {
                        platforms[platform.name] = platform;
                        BuildPlatform(platform);
                    }

                System.Func<Vector3, bool> Area(string name, bool tower)
                {
                    if (villages.TryGetValue(name, out Node v))
                    {
                        float r = tower ? v.Radius * 0.93f : v.Radius - 0.3f;
                        return q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(v.Center.x, v.Center.z)) < r;
                    }

                    if (platforms.TryGetValue(name, out HighPlatform pl))
                    {
                        Vector3 c = HP(pl.center);
                        float hx = pl.size[0] * 0.5f - 0.05f, hz = pl.size[1] * 0.5f - 0.05f;
                        return q => Mathf.Abs(q.x - c.x) < hx && Mathf.Abs(q.z - c.z) < hz;
                    }

                    return null;
                }

                if ((layout.roads == null || layout.roads.Length == 0) && layout.road != null &&
                    !string.IsNullOrEmpty(layout.road.kinds))
                {
                    layout.road.start = string.IsNullOrEmpty(layout.road.start) ? "Terraco" : layout.road.start;
                    layout.road.end = string.IsNullOrEmpty(layout.road.end) ? "Escalonada" : layout.road.end;
                    layout.roads = new[] { layout.road };
                }

                if (layout.roads != null)
                    foreach (HighRoad road in layout.roads)
                    {
                        System.Func<Vector3, bool> from = Area(road.start, road.start == "Terraco"), to = Area(road.end, false);
                        if (string.IsNullOrEmpty(road.kinds) || from == null || to == null)
                            continue;
                        BuildRoad(road, q => from(q) || to(q),
                            exits.TryGetValue(road.start, out List<Vector3> a) ? a : new List<Vector3>(),
                            exits.TryGetValue(road.end, out List<Vector3> b) ? b : new List<Vector3>());
                    }

                foreach (KeyValuePair<string, Node> pair in villages)
                    if (pair.Key != "Terraco")      // o terraço do portal tem o piso da torre lavrada
                        VillageFloor(pair.Value);

                Node gate = villages["Portao"];
                Vector3 clockPeak = HP(130f, 330f, 110f);
                Vector3 deckA = HP(layout.deck.a), deckB = HP(layout.deck.b);
                s_HighArrival = BuildPortalTerrace(villages["Terraco"], exits["Terraco"]);
                BuildStepVillage(villages["Escalonada"], villages["Escalonada_T1"], villages["Escalonada_T2"], city, exits);
                BuildBellVillage(villages["Sinos"], city, exits["Sinos"]);
                Vector3 cableStart = BuildGearVillage(villages["Engrenagens"], city, exits["Engrenagens"], clockPeak);
                BuildBridgeVillage(villages["Ponte_O"], villages["Ponte_L"], deckA, deckB, layout.deck.width, exits);
                var landings = new List<Vector3>();
                foreach (HighLanding landing in layout.landings)
                    landings.Add(HP(landing.position));
                BuildHangingVillage(villages["Pendurada"], city, exits["Pendurada"]);
                // A Ponte do Reino (reta, da cidade ao desabamento, e a reta de lá até a área do fundo) e a Vila da Guarda
                // (sala 23), entre o platô do antigo portão e a ponte, ligada aos dois por pontes
                BuildRoyalBridge(city);
                BuildReinoVillage(gate, exits["Portao"]);
                BuildCitySketch(city, Flat(ReinoRotunda - city));
                BuildCityEngines(city, Flat(ReinoRotunda - city));

                // Cenário: torres em picos sozinhos, o teleférico e as ilhas voadoras, longe do caminho
                BuildClockTower(clockPeak);
                Cableway(cableStart, Rim(Peak(clockPeak, 16f), villages["Engrenagens"].Center), 3.2f);
                BuildWatchTower(HP(-560f, 380f, 140f), 7101, true, city);
                BuildWatchTower(HP(620f, 150f, 80f), 7102, false, city);
                BuildRuneBeacon(HP(-620f, -420f, 60f));
                BuildTwinTowers(HP(540f, -130f, 110f), city);
                BuildRingIsle(Peak(HP(-430f, 140f, 95f), 46f));
                BuildIsletCluster(HP(680f, -20f, 75f), HP(600f, -40f, 75f), HP(760f, 10f, 75f));
                BuildFallenIsle(new Node
                {
                    Center = HP(-560f, -150f, 70f), Radius = 40f, Tilt = Quaternion.AngleAxis(13f, Vector3.forward),
                });
                BuildAqueductIsle(Peak(HP(360f, -280f, 85f), 40f), HP(270f, -350f, 82f), HP(455f, -215f, 82f));

                // Montanhas do gerador espalhadas em volta: fora do maciço, do vale da cidade e da ponte
                Clear(city, k_CityRadius + 90f);
                foreach ((float e, float n, float r) in new[]
                         { (-330f, 620f, 280f), (-60f, 720f, 300f), (230f, 680f, 300f), (0f, 900f, 320f), (520f, 650f, 160f), (-560f, 620f, 160f) })
                    Clear(HP(e, n, 0f), r);
                DistantField(new Vector2(k_High.x, k_High.z), 150, 100, 1400, "Macicos_Vilas");
                CityBackdrop(city);

                Snowfall(villages["Escalonada"].Center + Vector3.up * 30f);
                Snowfall(villages["Engrenagens"].Center + Vector3.up * 30f);
                Snowfall(villages["Pendurada"].Center + Vector3.up * 10f);
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

        // Montanhas ao fundo da cidade (pedido do usuário em 2026-11-10: mais montanhas no fundo, principalmente em volta da
        // cidade), com sorteio próprio, sem mudar o resto do mapa. Um anel do meio (520 a 1250 m do centro da cidade) de
        // maciços do Blender e montanhas grandes do gerador, fora do lado norte (o vale entre as vilas e a cidade), e um
        // anel do fundo (1250 a 2300 m) de maciços leves grandes, menos para o norte (o maciço das Vilas fica lá). Cada uma
        // confere a própria pegada contra as áreas do caminho (ponte, vilas, ilhas, torres), o maciço das Vilas, as
        // montanhas já postas e a cidade.
        static void CityBackdrop(Vector3 city)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(9301);
            try
            {
                GameObject[] near = LoadMassifs(""), far = LoadMassifs("_Longe");
                if (near.Length == 0 || far.Length == 0)
                    return;
                float[] nearRadius = MassifRadii(near), farRadius = MassifRadii(far);
                var kinds = new[]
                {
                    PicosMountainKit.Kind.Tower, PicosMountainKit.Kind.Crown, PicosMountainKit.Kind.Ridge,
                    PicosMountainKit.Kind.Broken, PicosMountainKit.Kind.Cluster,
                };
                var root = new GameObject("Macicos_Cidade").transform;
                root.SetParent(s_World, false);
                var placed = new List<(Vector2 Center, float Radius)>();
                const int k_Mid = 34, k_Back = 46;
                int mid = 0, back = 0;
                for (int attempt = 0; attempt < 4000 && (mid < k_Mid || back < k_Back); attempt++)
                {
                    bool background = mid >= k_Mid;
                    float a = R(0f, Mathf.PI * 2f);
                    float north = Mathf.Sin(a);
                    if (!background && north > 0.64f)
                        continue;
                    if (background && north > 0f && s_Random.NextDouble() < 0.7 * north)
                        continue;
                    float d = background ? R(1250f, 2300f) : R(520f, 1250f);
                    var p = new Vector2(city.x + Mathf.Cos(a) * d, city.z + Mathf.Sin(a) * d);

                    // O que vai ali: maciço (com a pegada do modelo) ou montanha grande do gerador
                    bool generated = !background && s_Random.NextDouble() < 0.35;
                    int model = s_Random.Next(near.Length);
                    float scale = background ? R(1.5f, 2.5f) : R(0.9f, 1.5f);
                    float size = R(40f, 80f);
                    float footprint = generated ? size * 2f : (background ? farRadius[model % far.Length] : nearRadius[model]) * scale;
                    float margin = background ? 20f : 50f;

                    if (Vector2.Distance(p, new Vector2(city.x, city.z)) < k_CityRadius + 120f + footprint)
                        continue;
                    bool blocked = false;
                    foreach (var clearing in s_Clearings)
                        blocked |= Vector2.Distance(p, clearing.Center) < clearing.Radius + footprint + margin;
                    float e = p.x - k_High.x, n = p.y - k_High.z;
                    blocked |= e > -820f - footprint && e < 820f + footprint && n > 230f - footprint && n < 1260f + footprint;
                    foreach (PicosMountainKit.Placed pillar in PicosMountainKit.Pillars)
                        blocked |= Vector2.Distance(p, new Vector2(pillar.Top.x, pillar.Top.z)) < pillar.BaseRadius + footprint * 0.6f;
                    foreach (var other in placed)
                        blocked |= Vector2.Distance(p, other.Center) < other.Radius + footprint * 0.7f;
                    if (blocked)
                        continue;

                    placed.Add((p, footprint));
                    if (generated)
                        PicosMountainKit.Mountain(s_MountainFar, kinds[s_Random.Next(kinds.Length)],
                            new Vector3(p.x, R(60f, 200f), p.y), size, s_Random, 2);
                    else
                        PlaceMassif(background ? far[model % far.Length] : near[model], new Vector3(p.x, 0f, p.y), scale, root);
                    if (background)
                        back++;
                    else
                        mid++;
                }

                Debug.Log($"[Ícaro] Fundo da cidade: {mid} montanhas no anel do meio e {back} no do fundo.");
            }
            finally
            {
                s_Random = saved;
            }
        }

        // Raio da pegada de cada maciço na planta, em escala 1 e em qualquer giro (do pivô até o canto mais longe da caixa)
        static float[] MassifRadii(GameObject[] models)
        {
            var radii = new float[models.Length];
            for (int i = 0; i < models.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(models[i]);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
                {
                    Bounds b = renderer.bounds;
                    float x = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), z = Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                    radii[i] = Mathf.Max(radii[i], Mathf.Sqrt(x * x + z * z));
                }

                Object.DestroyImmediate(go);
            }

            return radii;
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
            // A rocha que aparece é detalhada; a colisão vem de uma cópia mais leve (Macico_Vilas_Colisao, sem desenho).
            // FBX antigo, sem a cópia: a colisão é a própria rocha
            bool separateCollision = false;
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
                separateCollision |= renderer.name.EndsWith("_Colisao");
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name.EndsWith("_Colisao"))
                {
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(renderer);
                    continue;
                }
                bool ice = renderer.name.EndsWith("_Gelo");
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = ice ? s_Materials.Ice : s_Materials.Mountain;
                renderer.sharedMaterials = materials;
                if (!ice && !separateCollision)
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }

            // Remendo de rocha no platô do antigo portão (Ferramentas/Picos/remendo_plato.py): fecha o corte que a ponte
            // antiga abria na montanha; mesma origem e giro do maciço, com colisão
            string patchPath = $"{IcaroAssetUtility.Root}/Models/Picos/Macico_Vilas_Remendo.fbx";
            if (AssetImporter.GetAtPath(patchPath) is ModelImporter patchImporter && !patchImporter.isReadable)
            {
                patchImporter.isReadable = true;
                patchImporter.SaveAndReimport();
            }

            var patchModel = AssetDatabase.LoadAssetAtPath<GameObject>(patchPath);
            if (patchModel)
            {
                var patch = (GameObject)PrefabUtility.InstantiatePrefab(patchModel, s_World);
                patch.name = "Macico_Vilas_Remendo";
                patch.transform.SetPositionAndRotation(k_High, patchModel.transform.rotation);
                foreach (MeshRenderer renderer in patch.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = s_Materials.Mountain;
                    renderer.sharedMaterials = materials;
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            else
                Debug.LogWarning("[Picos] Falta o Macico_Vilas_Remendo.fbx: o corte da ponte antiga fica aberto no platô do portão.");

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
        // lisa por cima ('solid' falso: sem ela, para o que não se anda, como o aqueduto da Vila-Ponte).
        // 'parapet' (opcional): diz se um bloco do parapeito, de t0 a t1 metros ao longo e no lado 'side' (+1 = direita),
        // entra (a Vila-Ponte tira os que ficariam dentro das casas, do moinho e das muralhas das torres); 'ice': malha do
        // gelo do canal (estilo 1)
        static void Arcade(Vector3 from, Vector3 to, float width, float spacing, int style, bool solid = true,
            System.Func<float, float, float, bool> parapet = null, MeshData ice = null)
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
                // Blocos lado a lado, sem sobrepor (sobrepostos, os topos ficavam no mesmo plano e piscavam)
                Vector3 c = Vector3.Lerp(from, to, (b + 0.5f) / blocks);
                float tc = (b + 0.5f) * segment;
                BlockMesh.ChamferBox(s_Ruins.Pick(), c + Vector3.down * (deck * 0.5f), new Vector3(width, deck, segment), rotation, 0.06f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 edge = c + right * s * (width * 0.5f - 0.4f);
                    if (style == 1)
                    {
                        if (parapet == null || parapet(tc - segment * 0.5f, tc + segment * 0.5f, s))
                            BlockMesh.ChamferBox(s_Ruins.Pick(), edge + Vector3.up * 0.7f, new Vector3(0.8f, 1.4f, segment), rotation, 0.05f);
                    }
                    else if (s_Random.NextDouble() < (style == 2 ? 0.9 : 0.7))
                    {
                        float piece = segment * R(0.6f, 0.95f);
                        if (parapet == null || parapet(tc - piece * 0.5f, tc + piece * 0.5f, s))
                            BlockMesh.ChamferBox(s_Ruins.Pick(), edge + Vector3.up * (style == 2 ? 0.6f : 0.35f),
                                new Vector3(0.8f, style == 2 ? 1.2f : 0.7f, piece), rotation, 0.05f);
                    }
                }

                if (style == 1)
                    BlockMesh.Box(ice ?? s_Ice, c + Vector3.up * 0.06f, new Vector3(width - 1.8f, 0.12f, segment), rotation);
                if (s_Random.NextDouble() < 0.5)
                    Spike(s_Ice, c + right * R(-width * 0.4f, width * 0.4f) + Vector3.down * (deck + 0.1f), Vector3.down,
                        R(0.6f, 2.4f), R(0.12f, 0.3f), 5, 0f, 3f);
            }

            if (solid)
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
            Roof roof, float spireChance, Vector3[] keep, float size = 1f, float tall = 1f, (Vector3 Center, float Radius)[] avoid = null,
            (Vector3 Center, float Radius)[] hide = null)
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
                if (avoid != null)
                    foreach ((Vector3 c, float r) in avoid)
                        blocked |= Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)) < r + foot;
                if (blocked)
                    continue;
                float yaw = Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg + R(-20f, 20f);
                Quaternion rotation = tilt * Quaternion.Euler(0f, yaw, 0f);
                // Casas dentro de 'hide' (a barbacã da Ponte do Reino) são montadas numa malha descartada: os sorteios
                // continuam os mesmos e o resto da cidade não muda
                bool hidden = false;
                if (hide != null)
                    foreach ((Vector3 c, float r) in hide)
                        hidden |= Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)) < r + foot;
                House(hidden ? new RuinMesh(0) : m, p, rotation, w, d, h, roof, s_Random.NextDouble() < spireChance);
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
        // caminho) e de 'avoid'; no máximo 'count' casas; com 'models', as casas entram na ordem dessa lista
        static List<HouseSpot> KitHouseRing(Transform root, Node n, Vector3 view, float viewHalf, List<Vector3> gates, Vector3[] avoid,
            float avoidRadius, int count, (string Model, float W, float D)[] models = null)
        {
            var spots = new List<HouseSpot>();
            float viewAngle = Mathf.Atan2(view.x, view.z), half = viewHalf * Mathf.Deg2Rad;
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));

            float angle = viewAngle + half, end = viewAngle + Mathf.PI * 2f - half;
            while (angle < end && spots.Count < count)
            {
                (string model, float w, float d) = models != null ? models[spots.Count % models.Length]
                    : k_KitHouses[s_Random.Next(k_KitHouses.Length)];
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
                model = AttackHouse(model, n, p, yaw, gates);       // estragos do ataque (PicosAttack)
                PicosGothicKit.Place(s_Materials, model, p, yaw, root, true, PicosGothicKit.VillagesFolder);
                spots.Add(new HouseSpot { Foot = p, Rotation = Quaternion.Euler(0f, yaw, 0f), W = w, D = d, H = 8f, Model = model });
                angle += (w + R(1.2f, 4f)) / radius;
            }

            return spots;
        }

        // Parapeito do kit pela beira de uma vila (trechos de 4 m com pilaretes), aberto nas saídas e, com 'open', num
        // arco em volta da direção 'open.Dir' (meia abertura 'open.Half' em graus)
        static void KitParapet(Transform root, Node n, List<Vector3> gates, (Vector3 Dir, float Half)? open = null)
        {
            float radius = n.Radius - 0.5f;
            int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / 4f));
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count;
                bool gap = open.HasValue && AngleGap(angle, Mathf.Atan2(open.Value.Dir.x, open.Value.Dir.z)) < open.Value.Half * Mathf.Deg2Rad;
                foreach (float gate in gateAngles)
                    gap |= AngleGap(angle, gate) < 5f / radius;
                if (!gap)
                    PicosGothicKit.Place(s_Materials, "PR_Parapeito", n.Center + Around(angle) * radius, YawOf(Around(angle)), root, true,
                        PicosGothicKit.VillagesFolder);
            }
        }

        // Escada em ponte sobre um vão do caminho natural (o chão das duas pontas em alturas diferentes): degraus de pedra
        // sobre uma laje inclinada, meio-fio partido dos dois lados, pingentes embaixo, dois pilares até as nuvens nos vãos
        // longos e a rampa invisível rente às quinas (como nas outras escadas)
        static void StairBridge(Vector3 bottom, Vector3 top, float width)
        {
            Vector3 flat = top - bottom;
            flat.y = 0f;
            Vector3 dir = flat.normalized;
            Quaternion rotation = Quaternion.LookRotation(dir);
            Vector3 right = rotation * Vector3.right;
            float rise = top.y - bottom.y, run = flat.magnitude;
            int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.3f));
            float stepRise = rise / steps, stepRun = run / steps;
            for (int k = 0; k < steps; k++)
            {
                float y = bottom.y + (k + 1) * stepRise;
                Vector3 c = bottom + dir * (stepRun * (k + 0.5f));
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(c.x, y - 0.5f, c.z), new Vector3(width, 1f, stepRun + 0.02f),
                    rotation, 0.03f);
            }

            // Laje inclinada por baixo dos degraus e meio-fio partido
            Vector3 along = top - bottom;
            Quaternion slope = Quaternion.LookRotation(along);
            Vector3 under = slope * Vector3.up;
            BlockMesh.ChamferBox(s_Ruins.Pick(), (top + bottom) * 0.5f - under * 1.6f, new Vector3(width * 0.85f, 1.4f, along.magnitude),
                slope, 0.06f);
            foreach (float side in new[] { -1f, 1f })
            {
                int pieces = Mathf.Max(1, Mathf.RoundToInt(along.magnitude / 3f));
                for (int i = 0; i < pieces; i++)
                {
                    if (s_Random.NextDouble() < 0.3)
                        continue;
                    Vector3 c = Vector3.Lerp(bottom, top, (i + 0.5f) / pieces) + right * side * (width * 0.5f - 0.25f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), c + under * 0.35f,
                        new Vector3(0.5f, 0.7f, along.magnitude / pieces * R(0.6f, 0.95f)), slope, 0.05f);
                }
            }

            int spikes = Mathf.RoundToInt(run / 3f);
            for (int i = 0; i < spikes; i++)
                Spike(s_Ice, Vector3.Lerp(bottom, top, (i + 0.5f) / spikes) + right * R(-width * 0.4f, width * 0.4f) - under * 2.4f,
                    Vector3.down, R(0.6f, 2.4f), R(0.12f, 0.3f), 5, 0f, 3f);

            if (run > 14f)
                foreach (float t in new[] { 0.33f, 0.67f })
                {
                    Vector3 c = Vector3.Lerp(bottom, top, t);
                    float height = c.y - k_CloudY + 50f;
                    ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), c + Vector3.down * (2.6f + height * 0.5f),
                        new Vector2(width * 0.7f, 3f), height, rotation, 0.1f);
                }

            var ramp = new GameObject("Rampa_Escada");
            ramp.transform.SetParent(s_World, false);
            ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f + slope * Vector3.up * -0.12f, slope);
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, along.magnitude);
        }

        // Terraço do Portal: a chegada, um terraço de pedra lavrada (torre esculpida dos Picos: lajes, cornija com mísulas,
        // parapeito com blocos nas quinas e o fuste) em pé no vale ao norte da crista principal, no lugar marcado pelo
        // usuário, do maior tamanho que cabe sem encostar na montanha (raio da planta, achado pelo macico_vilas.py); a
        // crista esconde a cidade. O parapeito abre só na saída do caminho natural, e o portal gêmeo inteiro fica no fundo,
        // de frente para ela. Devolve a chegada, na frente do portal.
        static Transform BuildPortalTerrace(Node terrace, List<Vector3> exits)
        {
            Vector3 exit = exits.Count > 0 ? exits[0] : terrace.Center + Vector3.back;
            Vector3 f = Flat(exit - terrace.Center);
            var open = new List<int>(SidesToward(terrace.Center, exit));
            int door = -1;
            foreach (int side in SidesToward(terrace.Center, terrace.Center - f))
            {
                door = side;                    // a porta escura do fuste fica do lado do vale, oposto à saída
                break;
            }
            TowerTop(terrace.Center, terrace.Radius, 7301, open.ToArray(), open.Contains(door) ? -1 : door);
            return IntactPortal(terrace.Center - f * terrace.Radius * 0.3f, f, "VilasDoAlto");
        }

        // Vila Escalonada: a primeira vila, na ponta do contraforte, com dois terraços mais baixos na frente (escadas da
        // planta); chega-se pelo caminho que vem do Terraço do Portal. Casas góticas do kit inteiras e só abandonadas
        // (portas trancadas, venezianas) em fila pela beira de trás de cada nível, de frente para a praça; o lado virado
        // para a cidade fica aberto, só com o parapeito, para a vista. Na praça de cima, a fonte congelada no meio,
        // barracas de feira largadas e postes
        static void BuildStepVillage(Node main, Node t1, Node t2, Vector3 city, Dictionary<string, List<Vector3>> exits)
        {
            Vector3 f = Flat(city - main.Center);
            var root = new GameObject("Vila_Escalonada").transform;
            root.SetParent(s_World, false);

            List<HouseSpot> mainHouses = KitHouseRing(root, main, f, 62f, exits["Escalonada"], null, 0f, 12);
            KitParapet(root, main, exits["Escalonada"]);
            var terraceHouses = new List<List<HouseSpot>>();
            foreach ((Node terrace, string name) in new[] { (t1, "Escalonada_T1"), (t2, "Escalonada_T2") })
            {
                terraceHouses.Add(KitHouseRing(root, terrace, Flat(city - terrace.Center), 80f, exits[name], null, 0f, 3));
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
                float yaw = YawOf(-Around(angle)) + R(-10f, 10f);
                // A coluna-lanterna não fica colada na frente de uma casa (pedido do usuário em 2026-11-07: uma ficava
                // na porta): nada de casa a menos de 3,5 m dela, de 1,5 a 8,5 m de altura (acima do parapeito)
                if (model == "PR_Poste")
                {
                    Physics.SyncTransforms();
                    if (Physics.CheckBox(p + Vector3.up * 5f, new Vector3(3.5f, 3.5f, 3.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                }

                VillagePiece(model, p, yaw, root, kit);
            }

            // Tecnologia (peças soltas do kit AR_Tecno): na praça de cima uma agulha, o relicário com o atril e um
            // tríptico; um tríptico em cada terraço (as colunas-lanterna ficam no lugar dos postes)
            VillageTech(root, main, "Vila Escalonada", exits["Escalonada"], f, 62f, 1, true, 0, 1);
            VillageTech(root, t1, "Vila Escalonada (terraço 1)", exits["Escalonada_T1"], Flat(city - t1.Center), 80f, 0, false, 0, 1);
            VillageTech(root, t2, "Vila Escalonada (terraço 2)", exits["Escalonada_T2"], Flat(city - t2.Center), 80f, 0, false, 0, 1);

            // Estragos do ataque (PicosAttack)
            AttackDamage(root, main, exits["Escalonada"], mainHouses, "Vila Escalonada", 9101, 2);
            AttackDamage(root, t1, exits["Escalonada_T1"], terraceHouses[0], "Vila Escalonada (terraço 1)", 9102, 1);
            AttackDamage(root, t2, exits["Escalonada_T2"], terraceHouses[1], "Vila Escalonada (terraço 2)", 9103, 1);
        }

        // Portal gêmeo do da praça da chegada, inteiro: estrado em degraus, anel parado, dois anéis girando, superfície
        // acesa e monólitos de runas; 'facing' é para onde a superfície olha. Devolve o ponto de chegada na frente dele.
        static Transform IntactPortal(Vector3 floor, Vector3 facing, string name)
        {
            Quaternion rotation = Quaternion.LookRotation(facing);
            Vector3 right = rotation * Vector3.right;
            Vector3 center = floor + Vector3.up * 9.5f;
            s_SpritePortals.Add((floor, facing));
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

        // Vila da Ressonância (era a Vila dos Sinos; pedido do usuário em 2026-11-07: pegada futurista), na sela: o campanário
        // de ressonância do kit AR_Tecno no meio (o sino grande numa bobina e o orbe transmissor na agulha), a capela no fundo, de frente para ele, um arco com sino no pé de cada chegada, casas do kit pela beira de trás e o lado
        // da cidade aberto, só com o parapeito; na praça da frente, o pátio do fundidor (sinos novos nos berços e a
        // cabrilha com um sino içado pela metade), barracas e postes
        static void BuildBellVillage(Node n, Vector3 city, List<Vector3> exits)
        {
            Vector3 f = Flat(city - n.Center);
            var root = new GameObject("Vila_da_Ressonancia").transform;
            root.SetParent(s_World, false);
            string kit = PicosGothicKit.VillagesFolder;

            var gates = new List<Vector3>();
            foreach (Vector3 g in exits)
                if (gates.TrueForAll(other => Vector3.Distance(other, g) > 1f))
                    gates.Add(g);

            // O campanário saiu da praça (pedido do usuário em 2026-11-07): fica numa plataforma-torre própria do lado de fora,
            // ligada à beira por uma ponte curta; no meio da praça, o monumento de adoração (o diapasão gótico)
            Vector3 towerDir = ResonanceTower(root, n);
            PlaceTech("PR_Monumento_Diapasao", n.Center, YawOf(f), root);
            s_SpriteShrines.Add((n.Center + f * 6.4f, f));
            Vector3 chapel = n.Center - f * (n.Radius - 12.5f);
            PicosGothicKit.Place(s_Materials, "AR_Capela_Ressonancia", chapel, YawOf(f), root, true, kit);
            s_SpriteShrines.Add((chapel + f * 8.5f, f));

            // Arco no pé de cada chegada, atravessado pelo caminho
            var avoid = new List<Vector3> { n.Center - f * 17f, chapel, n.Center - f * 30f, n.Center + towerDir * (n.Radius - 2f) };
            foreach (Vector3 g in gates)
            {
                Vector3 inward = Flat(n.Center - g);
                Vector3 p = new Vector3(g.x, n.Center.y, g.z) + inward * 1f;
                PicosGothicKit.Place(s_Materials, "PR_Arco_Sino_Ressonancia", p, YawOf(inward), root, true, kit);
                avoid.Add(p);
            }

            List<HouseSpot> houses = KitHouseRing(root, n, f, 62f, gates, avoid.ToArray(), 6f, 10);
            KitParapet(root, n, gates, (towerDir, 9f));

            // Praça da frente e os lados do monumento, longe das linhas das chegadas até o meio
            float view = Mathf.Atan2(f.x, f.z);
            foreach ((float deg, float distance, string model) in new[]
                     { (35f, 16f, "PR_Orgao_Cristal"), (-28f, 15f, "PR_Carrilhao_Cristal"), (-50f, 21f, "PR_Esfera_Harmonica"), (150f, 13f, "PR_Barraca"),
                       (-160f, 14f, "PR_Barraca"), (18f, 24f, "PR_Poste"), (-18f, 24f, "PR_Poste"), (100f, 15f, "PR_Poste"),
                       (-100f, 15f, "PR_Poste"), (160f, 20f, "PR_Poste"), (-165f, 21f, "PR_Poste") })
            {
                Vector3 p = n.Center + Around(view + deg * Mathf.Deg2Rad) * distance;
                bool blocked = false;
                foreach (Vector3 g in gates)
                    blocked |= SegmentDistance(p, n.Center, g) < 6f;
                if (blocked)
                    continue;
                VillagePiece(model, p, YawOf(n.Center - p) + R(-12f, 12f), root, kit);
            }

            // Tecnologia: uma agulha ao lado de uma chegada, o relicário com o atril e um tríptico
            VillageTech(root, n, "Vila da Ressonância", gates, f, 62f, 1, true, 0, 1);
            AttackDamage(root, n, gates, houses, "Vila da Ressonância", 9201, 3);
        }

        // Plataforma do campanário da Vila da Ressonância (pedido do usuário em 2026-11-07, desenhada por ele): uma torre de
        // pedra lavrada (TowerTop, 32 m de quina a quina) do lado de fora da praça, a sudeste (k_ResonanceTowerAngle, na planta),
        // ligada à beira por uma ponte curta de pedra; o campanário no meio dela, com espaço para andar em volta, e o cristal
        // lilás grande flutuando e girando no sineiro sobre o emissor (PR_Cristal_Grande, CristalCentro do kit). Sorteio
        // próprio (o resto do mapa não muda). Devolve a direção da praça para a plataforma
        const float k_ResonanceTowerAngle = -65f;     // graus, do leste para o norte (entre a vista da cidade e a saída leste)
        const float k_ResonanceTowerRadius = 16f;
        const float k_ResonanceCrystalHeight = 26.57f;

        static Vector3 ResonanceTower(Transform root, Node n)
        {
            float a = k_ResonanceTowerAngle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float inner = k_ResonanceTowerRadius * Mathf.Cos(Mathf.PI / 8f);
            Vector3 center = n.Center + dir * (n.Radius + 4f + inner);

            System.Random saved = s_Random;
            s_Random = new System.Random(7419);
            var open = new List<int>(SidesToward(center, n.Center));
            int door = -1;
            foreach (int side in SidesToward(center, center + dir))
                door = side;
            TowerTop(center, k_ResonanceTowerRadius, 7419, open.ToArray(), open.Contains(door) ? -1 : door);
            Bridge(n.Center + dir * (n.Radius - 1.5f), center - dir * (inner - 1f), 8f, false);
            s_Random = saved;

            PlaceTech("AR_Campanario_Ressonancia", center, YawOf(-dir), root);
            GameObject crystal = PlaceTech("PR_Cristal_Grande", center + Vector3.up * k_ResonanceCrystalHeight, 0f, root, 1f, false);
            if (crystal)
            {
                Dynamic(crystal.transform);
                var spin = crystal.AddComponent<SlowRotation>();
                spin.DegreesPerSecond = 14f;
                spin.Bob = 0.22f;
                spin.BobSpeed = 0.7f;
                // Luz lilás do cristal, que acende o sineiro por dentro e escapa pelos arcos
                var glow = crystal.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.8f, 0.62f, 1f);
                glow.range = 16f;
                glow.intensity = 3f;
                glow.shadows = LightShadows.None;
            }

            return dir;
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

        // Casas da Vila das Engrenagens, na ordem em que entram pela beira (as peças novas do kit e algumas casas):
        // largura e fundo com folga (o fundo é o dobro do lado de trás: o guindaste e a viga de içar do depósito saem
        // pela frente)
        static readonly (string Model, float W, float D)[] k_GearHouses =
        {
            ("AR_Oficina_Cristal", 14.6f, 13.2f), ("AR_Casa_Empena", 8.6f, 10.6f), ("AR_Casa_Relojoeiro_Cristal", 9.8f, 11.2f),
            ("AR_Deposito_Cristal", 14.4f, 13.2f), ("AR_Casa_Larga", 12.6f, 9f), ("AR_Oficina_Cristal", 14.6f, 13.2f),
            ("AR_Casa_Torre", 8.4f, 8.4f), ("AR_Deposito_Cristal", 14.4f, 13.2f), ("AR_Casa_Empena", 8.6f, 10.6f),
            ("AR_Oficina_Cristal", 14.6f, 13.2f), ("AR_Casa_Relojoeiro_Cristal", 9.8f, 11.2f),
        };

        // Vila dos Artífices (era a Vila das Engrenagens), no ombro largo: a vila dos mecânicos e relojoeiros que fizeram
        // o relógio da torre e o elevador de cristal (pedido do usuário em 2026-10-31; ar futurista em 2026-11-07: as
        // máquinas são de cristal e as engrenagens ficam só entalhadas na pedra). A estação do elevador de cristal do kit
        // AR_Tecno no meio da praça, de frente para a torre do relógio (os cabos saem do anel de liga em volta do cristal
        // pendurado); a Casa do Coração (o anel de liga em pé com o núcleo de luz, o relógio-mestre parado) na beira de
        // trás (saiu em 2026-11-08); oficinas com forno de cristal, depósitos com guindaste, casas do relojoeiro e casas
        // do kit pela beira de trás, de frente para a praça; o lado da cidade aberto, só com o parapeito. Cápsulas de carga
        // flutuantes paradas na frente dos depósitos (o trilho de levitação em volta da estação saiu
        // em 2026-11-08: não levava a lugar nenhum); pilhas de células, bancadas de cristal na frente das oficinas,
        // cargas congeladas e colunas-lanterna. Sorteio próprio (o resto do mapa não muda). Devolve o ponto
        // no meio dos dois cabos, onde eles saem do anel da estação.
        static Vector3 BuildGearVillage(Node n, Vector3 city, List<Vector3> exits, Vector3 clockPeak)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3501);
            Vector3 f = Flat(city - n.Center), toPeak = Flat(clockPeak - n.Center);
            var root = new GameObject("Vila_dos_Artifices").transform;
            root.SetParent(s_World, false);
            float floor = n.Center.y;

            var gates = new List<Vector3>();
            foreach (Vector3 g in exits)
                if (gates.TrueForAll(other => Vector3.Distance(other, g) > 1f))
                    gates.Add(g);
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));
            float view = Mathf.Atan2(f.x, f.z);

            // Estação no meio, a frente (o cristal e os cabos) para a torre do relógio
            PlaceTech("AR_Estacao_Cristal", n.Center, YawOf(toPeak), root);

            // A Casa do Coração saiu em 2026-11-08 (pedido do usuário: não fazia mais sentido); no trecho da beira de trás
            // mais longe das chegadas fica a área de convivência (sem casas)
            float gather = view + Mathf.PI, best = -1f;
            for (float deg = 100f; deg <= 260f; deg += 5f)
            {
                float a = view + deg * Mathf.Deg2Rad, gap = Mathf.PI;
                foreach (float g in gateAngles)
                    gap = Mathf.Min(gap, AngleGap(a, g));
                if (gap > best)
                {
                    best = gap;
                    gather = a;
                }
            }

            Vector3 gatherAt = n.Center + Around(gather) * (n.Radius - 7f);
            List<HouseSpot> spots = KitHouseRing(root, n, f, 62f, gates, new[] { gatherAt }, 8f, 11, k_GearHouses);
            KitParapet(root, n, gates);

            // Cápsulas de carga paradas (os outros enfeites ficam fora delas pelo 'Free')
            var pods = new List<Vector3>();

            // Fora da estação, das linhas das chegadas até o meio e das cápsulas de carga
            bool Free(Vector3 p, float clear)
            {
                float r = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(n.Center.x, n.Center.z));
                if (r < 17f)
                    return false;
                foreach (Vector3 g in gates)
                    if (SegmentDistance(p, n.Center, g) < 4.5f + clear)
                        return false;
                foreach (Vector3 q in pods)
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) < 2.2f + clear)
                        return false;
                return true;
            }

            // Cápsula de carga flutuante (veículo de casco claro e liga, sobre placas de luz) parada na base de recarga
            // dela (o poste e o cabo atrás); origem no chão
            void Pod(Vector3 p, Vector3 along)
            {
                if (!Free(p, 2.2f))
                    return;
                float yaw = YawOf(along) + R(-8f, 8f);
                PlaceTech("PR_Base_Recarga", new Vector3(p.x, floor, p.z), yaw, root);
                PlaceTech("PR_Vagoneta_Capsula", new Vector3(p.x, floor + 0.1f, p.z), yaw, root);
                pods.Add(p);
            }

            // Bancadas na frente das oficinas e uma cápsula de carga na frente de cada depósito (as cargas de barris e
            // caixotes de madeira e o carrinho de mão saíram em 2026-11-08: lembravam a antiguidade)
            foreach (HouseSpot spot in spots)
            {
                Vector3 front = spot.Rotation * Vector3.forward, right = spot.Rotation * Vector3.right;
                if (spot.Model == "AR_Oficina_Cristal")
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 p = spot.Foot + front * (spot.D * 0.5f + 0.4f) + right * side * (spot.W * 0.3f);
                        if (Free(p, 1.4f))
                            PlaceTech("PR_Bancada_Cristal", p, YawOf(-front) + R(-6f, 6f), root);
                    }
                else if (spot.Model == "AR_Deposito_Cristal")
                {
                    Pod(spot.Foot + front * (spot.D * 0.5f + 1.8f) + right * (spot.W * 0.28f), right);
                    // Contêineres de carga empilhados do outro lado (no lugar dos barris e caixotes de madeira)
                    Vector3 c = spot.Foot + front * (spot.D * 0.5f + 0.9f) - right * (spot.W * 0.3f);
                    if (Free(c, 1.2f))
                    {
                        float yaw = YawOf(front) + R(-6f, 6f);
                        PlaceTech("PR_Conteiner_Celulas", new Vector3(c.x, floor, c.z), yaw, root);
                        PlaceTech("PR_Conteiner_Celulas", new Vector3(c.x, floor + 1f, c.z), yaw + R(-10f, 10f), root);
                        Vector3 beside = c + right * 1.75f;
                        if (s_Random.NextDouble() < 0.5 && Free(beside, 1.1f))
                            PlaceTech("PR_Conteiner_Celulas", new Vector3(beside.x, floor, beside.z), YawOf(front) + R(-15f, 15f), root);
                        pods.Add(c);
                    }
                }
            }

            // Colunas-lanterna pela praça
            foreach ((float deg, float distance, string model) in new[]
                     { (25f, 26.5f, "PR_Poste"), (-25f, 26.5f, "PR_Poste"), (75f, 26f, "PR_Poste"), (-75f, 26f, "PR_Poste"),
                       (125f, 26f, "PR_Poste"), (-125f, 26f, "PR_Poste"), (175f, 26f, "PR_Poste") })
            {
                float a = view + deg * Mathf.Deg2Rad;
                Vector3 p = new Vector3(n.Center.x, floor, n.Center.z) + Around(a) * distance;
                if (!Free(p, 1.1f))
                    continue;
                VillagePiece(model, p, YawOf(n.Center - p) + R(-15f, 15f), root, PicosGothicKit.Folder);
            }

            // Vida na vila: mirante, abrigo da estação, convivência, portas das casas e chegadas
            BuildVillageLife(root, n, gates, f, spots, gather, "Vila dos Artífices");
            AttackDamage(root, n, gates, spots, "Vila dos Artífices", 9301, 2);

            s_Random = saved;
            return n.Center + toPeak * 10.5f + Vector3.up * 10f;
        }
        // Vila-Ponte (Ponte dos Moleiros): a ponte larga sobre a garganta em três vãos, com os moinhos de quem vivia
        // nela. Um aqueduto desce pela fenda ao norte desde uma cascata congelada na parede oeste (~170 m acima, no rio)
        // até a roda grande sob o vão do meio; a água que caía dela congelou numa coluna até o fundo. Na rua: a torre do
        // portão em cada cabeceira (a frente para a vila), o Moinho Grande atravessando a rua no meio (ela passa pelo
        // túnel), casas debruçadas sobre o vazio dos dois lados com um arco de pedra por cima da rua em cada metade, a
        // grua de pisar na beira norte e sacos e mós encostados. Embaixo: uma roda menor perto de cada cabeceira, sobre
        // um cavalete na bancada de rocha (só onde os pés acham a rocha, por raio), alimentada por uma calha que corre
        // pela face norte da ponte, e a coluna de gelo dela. Duas casas do kit e o parapeito em cada cabeceira (aberto
        // onde a torre fica). Sorteio próprio (o resto do mapa não muda).
        static void BuildBridgeVillage(Node a, Node b, Vector3 from, Vector3 to, float width, Dictionary<string, List<Vector3>> exits)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3701);
            var root = new GameObject("Vila_Ponte").transform;
            root.SetParent(s_World, false);
            string kit = PicosGothicKit.VillagesFolder;
            Vector3 along = Flat(to - from), right = Vector3.Cross(Vector3.up, along);      // right: sul
            float length = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z)), mid = length * 0.5f;
            Vector3 At(float t, float s) => Vector3.Lerp(from, to, t / length) + right * s;
            float south = YawOf(right), north = YawOf(-right);

            // Rodas dos lados: os quatro pés do cavalete (20 m abaixo do eixo, 32,5 m abaixo do tabuleiro) precisam achar a
            // bancada de rocha a até 3,5 m acima deles; perto de 'prefer'
            Physics.SyncTransforms();
            const float wheelS = -6.7f;     // a ponta da calha dela (6,6 m para o norte) fica na linha das calhas, s = -13,3
            float? Bench(float t0, float t1, float prefer)
            {
                float? best = null;
                for (float t = t0; t <= t1; t += 0.5f)
                {
                    bool ok = true;
                    foreach (float dx in new[] { -1.55f, 1.55f })
                    foreach (float dy in new[] { -2.2f, 2.2f })
                    {
                        Vector3 q = At(t + dx, wheelS - dy) + Vector3.down * 12f;
                        ok &= Physics.Raycast(q, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) &&
                              hit.point.y >= q.y - 20.5f && hit.point.y <= q.y - 17f;
                    }

                    if (ok && (best == null || Mathf.Abs(t - prefer) < Mathf.Abs(best.Value - prefer)))
                        best = t;
                }

                return best;
            }

            float? westWheel = Bench(2f, 22f, 10f), eastWheel = Bench(length - 22f, length - 2f, length - 9f);

            // Casas debruçadas (frente para a rua, fundo na beira), com folgas de 2 m onde os arcos se apoiam; espelhadas nas
            // duas metades, a não ser pela grua no lugar da última casa do norte
            var houses = new List<(float T0, float Side, string Model, float W)>
            {
                (20.5f, 1f, "AR_Casa_Debrucada_A", 7.2f), (29.7f, 1f, "AR_Casa_Debrucada_C", 9f),
                (20.5f, -1f, "AR_Casa_Debrucada_A", 7.2f), (29.7f, -1f, "AR_Casa_Debrucada_B", 6f),
                (length - 27.7f, 1f, "AR_Casa_Debrucada_A", 7.2f), (length - 38.7f, 1f, "AR_Casa_Debrucada_C", 9f),
                (length - 38.7f, -1f, "AR_Casa_Debrucada_C", 9f),
            };

            // Parapeito do tabuleiro: sai onde ficaria dentro do moinho, das casas ou das muralhas das torres (as faces de
            // fora ficavam no mesmo plano das paredes deles e piscavam)
            bool Parapet(float t0, float t1, float side)
            {
                bool Overlaps(float a0, float a1) => t1 > a0 && t0 < a1;
                if (Overlaps(mid - 8.5f, mid + 8.5f) || Overlaps(11.9f, 15.1f) || Overlaps(length - 15.1f, length - 11.9f))
                    return false;
                foreach ((float h0, float hs, _, float hw) in houses)
                    if (hs == side && Overlaps(h0 - 0.5f, h0 + hw + 0.5f))
                        return false;
                return true;
            }

            // A ponte: três vãos de ~32 m (o do meio entre as faces dos pilares, onde fica a roda grande). O tabuleiro fica
            // 4 cm abaixo das pontas: o chão das cabeceiras cobre a parte dele que entra nelas, sem as duas faces no mesmo plano
            Arcade(from + Vector3.down * 0.04f, to + Vector3.down * 0.04f, width, 32f, 2, true, Parapet);

            // Moinho Grande no meio (frente para o sul; a rua passa pelo túnel) e, no mesmo referencial, a roda grande
            Vector3 center = At(mid, 0f);
            PicosGothicKit.Place(s_Materials, "AR_Moinho_Grande", center, south, root, true, kit);
            PicosGothicKit.Place(s_Materials, "PR_Roda_Agua_Grande", center, south, root, false, kit);

            // Torres do portão, a frente para a vila de cada cabeceira
            PicosGothicKit.Place(s_Materials, "AR_Torre_Portao", At(13.5f, 0f), YawOf(-along), root, true, kit);
            PicosGothicKit.Place(s_Materials, "AR_Torre_Portao", At(length - 13.5f, 0f), YawOf(along), root, true, kit);

            // Casas: o centro a s = ±8,18, o fundo 8 cm além da beira do tabuleiro (rente a ela, a parede de trás ficava no
            // mesmo plano da face do tabuleiro)
            foreach ((float t0, float side, string model, float w) in houses)
                PicosGothicKit.Place(s_Materials, model, At(t0 + w * 0.5f, side * 8.18f), side > 0f ? north : south, root, true, kit);
            foreach (float t in new[] { 28.7f, length - 28.7f })
                PicosGothicKit.Place(s_Materials, "PR_Arco_Rua", At(t, 0f), YawOf(along), root, true, kit);
            PicosGothicKit.Place(s_Materials, "PR_Grua_Pisar", At(length - 24.1f, -9.4f), north, root, true, kit);

            // Nas beiras da rua, células de energia e painéis de controle parados (no lugar dos sacos de farinha e das mós
            // encostadas: o teste do ar futurista começou por esta vila, pedido do usuário em 2026-11-07)
            PicosGothicKit.Place(s_Materials, "PR_Celulas", At(mid - 9.6f, 3f), north + R(-12f, 12f), root, true, kit);
            PicosGothicKit.Place(s_Materials, "PR_Celulas", At(mid + 9.6f, -3f), south + R(-12f, 12f), root, true, kit);
            PicosGothicKit.Place(s_Materials, "PR_Painel_Controle", At(length - 33f, -2.6f), south, root, true, kit);
            PicosGothicKit.Place(s_Materials, "PR_Painel_Controle", At(24.1f, 2.6f), north, root, true, kit);

            // Aqueduto: um trecho curto da boca da calha da roda grande (12,6 m para o norte) até onde ele começa, longe da
            // roda, e de lá pela fenda acima, subindo devagar, até entrar na cascata gigante congelada (Gelo_Garganta, frente
            // em n ~684 nessa altura): a ponta fica enterrada no gelo, de onde ele tira a água
            Vector3 mouth = At(mid - 1.8f, -12.6f) + Vector3.down * 6.85f;
            var line = new List<Vector3> { mouth };
            foreach (Vector2 p in new[] { new Vector2(184.5f, 580.8f), new Vector2(191f, 640f), new Vector2(191f, 688f) })
                line.Add(HP(p.x, p.y, 0f));
            Vector3 first = Flat(line[1] - mouth);
            line.Insert(1, mouth + first * 2.4f);
            float run = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                run += Vector2.Distance(new Vector2(line[i - 1].x, line[i - 1].z), new Vector2(line[i].x, line[i].z));
                line[i] = new Vector3(line[i].x, mouth.y + run * 0.016f, line[i].z);
            }

            Quaternion firstRotation = Quaternion.LookRotation(line[1] - mouth);
            Vector3 neck = (mouth + line[1]) * 0.5f;
            BlockMesh.ChamferBox(s_Ruins.Pick(), neck + Vector3.down * 0.6f, new Vector3(4.6f, 1.2f, 2.5f), firstRotation, 0.06f);
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(s_Ruins.Pick(), neck + firstRotation * Vector3.right * s * 1.9f + Vector3.up * 0.7f,
                    new Vector3(0.8f, 1.4f, 2.5f), firstRotation, 0.05f);
            BlockMesh.Box(s_IceSheet, neck + Vector3.up * 0.06f, new Vector3(2.8f, 0.12f, 2.5f), firstRotation);
            for (int i = 1; i + 1 < line.Count; i++)
                Arcade(line[i], line[i + 1], 4.6f, 14f, 1, false, null, s_IceSheet);
            for (int i = 1; i < line.Count; i++)      // pilares nas juntas e nas pontas (o Arcade só põe os do meio)
                AqueductPier(line[i], Flat(line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 1)]));

            // Rodas dos lados, as calhas pela face norte (descendo da lateral do aqueduto até a ponta da calha de cada roda)
            // e as colunas de gelo
            foreach ((float? t, float start) in new[] { (westWheel, mid - 4.1f), (eastWheel, mid + 0.5f) })
            {
                if (t == null)
                {
                    Debug.LogWarning("[Ícaro] Vila-Ponte: uma das rodas dos lados não achou a bancada de rocha (ficou de fora).");
                    continue;
                }

                PicosGothicKit.Place(s_Materials, "PR_Roda_Agua", At(t.Value, wheelS), south, root, false, kit);
                Vector3 feederA = At(start, -13.3f) + Vector3.down * 7f, feederB = At(t.Value, -13.3f) + Vector3.down * 8.4f;
                Feeder(feederA, feederB, At(start, -12.75f), right);
                IceColumn(At(t.Value, wheelS + 0.85f) + Vector3.down * 15.4f, 0.8f, 1.7f);
            }

            IceColumn(At(mid - 1.8f, -4.2f) + Vector3.down * 18f, 1.4f, 3.4f);

            // Cabeceiras: duas casas do kit e o parapeito, abertos onde fica a torre do portão; dois postes na frente dela;
            // a torre de antena, o gerador com o painel e a coluna de bobinas (PicosTech; a arquitetura fica gótica)
            foreach ((Node n, Vector3 toDeck, string key) in new[] { (a, along, "Ponte_O"), (b, -along, "Ponte_L") })
            {
                var gates = new List<Vector3>(exits[key]);
                List<HouseSpot> headHouses = KitHouseRing(root, n, toDeck, 55f, gates, null, 0f, 2);
                KitParapet(root, n, gates, (toDeck, 40f));
                foreach (float s in new[] { -5.5f, 5.5f })
                    VillagePiece("PR_Poste", n.Center + toDeck * 11.8f + Vector3.Cross(Vector3.up, toDeck) * s, YawOf(-toDeck), root,
                        PicosGothicKit.Folder);
                CabeceiraTech(root, n, toDeck, gates);
                // A torre do portão fica na abertura para a ponte: os estragos não passam por ela
                var blocked = new List<Vector3>(gates) { n.Center + toDeck * (n.Radius - 1f) };
                AttackDamage(root, n, blocked, headHouses, "Vila-Ponte (" + key + ")", key == "Ponte_O" ? 9401 : 9402, 1);
            }

            s_Random = saved;
        }

        // Pilar do aqueduto da Vila-Ponte (como os do Arcade de canal): cantaria de 26 m sob a laje e a rocha até a nuvem
        static void AqueductPier(Vector3 slab, Vector3 dir)
        {
            Quaternion rotation = Quaternion.LookRotation(dir);
            Vector3 top = slab + Vector3.down * 1.2f;
            const float masonry = 26f, pierWidth = 4.14f, pierDepth = 3.6f;
            ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), top + Vector3.down * (masonry * 0.5f), new Vector2(pierWidth, pierDepth), masonry, rotation, 0.1f);
            float r = pierWidth * 0.75f + 1.5f;
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = top + Vector3.down * (masonry - 0.5f), TopRadius = r, BaseRadius = r * 2.2f, Taper = 2.5f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 14, RingStep = 0f, Corners = 5, Flutes = 3, FluteDepth = 0.08f,
                BedHeight = R(8f, 14f), BedShift = 0.08f, Waist = 0.1f, Roughness = 0.08f, Seed = R(0f, 1000f), Bundle = false,
            });
        }

        // Calha de madeira de 'a' a 'b' (fundos), pendurada na face norte da Vila-Ponte ('face': um ponto da face, logo
        // abaixo do tabuleiro; 'outward': o lado de dentro da ponte, para onde as vigas entram): fundo, lados e o gelo
        // da água; a cada ~3 m uma cinta, uma viga saindo de baixo do tabuleiro, dois tirantes de ferro até ela e pingentes
        static void Feeder(Vector3 a, Vector3 b, Vector3 face, Vector3 inward)
        {
            const float inner = 1.4f, wallHeight = 0.55f;
            Vector3 d = b - a;
            Quaternion rotation = Quaternion.LookRotation(d);
            Vector3 up = rotation * Vector3.up, side = rotation * Vector3.right, middle = (a + b) * 0.5f;
            float length = d.magnitude;
            BlockMesh.ChamferBox(s_Wood, middle - up * 0.07f, new Vector3(inner + 0.24f, 0.14f, length), rotation, 0.02f);
            foreach (float k in new[] { -1f, 1f })
                BlockMesh.ChamferBox(s_Wood, middle + side * k * (inner * 0.5f + 0.06f) + up * (wallHeight * 0.5f),
                    new Vector3(0.12f, wallHeight, length), rotation, 0.02f);
            BlockMesh.Box(s_IceSheet, middle + up * (wallHeight * 0.27f), new Vector3(inner - 0.04f, wallHeight * 0.55f, length - 0.1f), rotation);

            int count = Mathf.Max(1, Mathf.RoundToInt(length / 3f));
            for (int i = 0; i <= count; i++)
            {
                Vector3 c = Vector3.Lerp(a, b, i / (float)count);
                BlockMesh.ChamferBox(s_Wood, c - up * 0.2f, new Vector3(inner + 0.5f, 0.14f, 0.14f), rotation, 0.02f);
                // Viga: da face (por dentro, sob o tabuleiro) até 1,1 m além da calha, 2,75 m abaixo do topo do tabuleiro
                float inside = Vector3.Dot(face - c, inward);       // distância da calha até a face, para dentro
                Vector3 beamA = new Vector3(c.x, face.y - 2.75f, c.z) + inward * (inside + 1.75f);
                Vector3 beamB = new Vector3(c.x, face.y - 2.75f, c.z) - inward * 1.1f;
                Beam(beamA, beamB, 0.24f, 0.3f);
                foreach (float k in new[] { -1f, 1f })
                {
                    Vector3 bottom = c + side * k * (inner * 0.5f + 0.2f) - up * 0.2f;
                    Bar(bottom, new Vector3(bottom.x, beamA.y - 0.15f, bottom.z), 0.05f, 0.05f);
                }

                if (s_Random.NextDouble() < 0.55)
                    Spike(s_IceFall, c - up * 0.3f + side * R(-0.6f, 0.6f), Vector3.down, R(0.3f, 1.2f), R(0.05f, 0.12f), 5, 0f, 3f);
            }
        }

        // Coluna de gelo da água que caía de uma roda, de 'top' até o chão embaixo (por raio): trechos tortos que engrossam
        // para baixo (de 'r0' a 'r1'), barrigas com pingentes e um monte no pé
        static void IceColumn(Vector3 top, float r0, float r1)
        {
            if (!Physics.Raycast(top + Vector3.down * 0.5f, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                return;
            float bottom = hit.point.y, height = top.y - bottom;
            for (float y = top.y; y > bottom + 1f;)
            {
                float y1 = Mathf.Max(bottom, y - R(3f, 6f));
                float f0 = (top.y - y) / height, f1 = (top.y - y1) / height;
                float ra = Mathf.Lerp(r0, r1, f0) * R(0.8f, 1.15f), rb = Mathf.Lerp(r0, r1, f1) * R(0.8f, 1.15f);
                var c = new Vector3(top.x + R(-0.35f, 0.35f) * r0, 0f, top.z + R(-0.35f, 0.35f) * r0);
                Prism(s_IceFall, c, ra, rb, 7, y, y1 - 0.4f, false);
                if (s_Random.NextDouble() < 0.4)
                {
                    float fat = Mathf.Max(ra, rb), bulge = fat * R(1.25f, 1.6f), by = Mathf.Lerp(y, y1, R(0.3f, 0.7f));
                    Vector3 bc = c + new Vector3(R(-0.3f, 0.3f), 0f, R(-0.3f, 0.3f));
                    Prism(s_IceFall, bc, fat, bulge, 7, by + 0.8f, by - 0.6f, false);
                    Prism(s_IceFall, bc, bulge, fat * 0.9f, 7, by - 0.6f, by - 1.3f, false);
                    for (int k = 0; k < 4; k++)
                    {
                        float angle = R(0f, Mathf.PI * 2f);
                        Spike(s_IceFall, bc + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * bulge * 0.8f + Vector3.up * (by - 0.9f),
                            Vector3.down, R(0.5f, 1.8f), R(0.08f, 0.2f), 5, 0f, 3f);
                    }
                }

                y = y1;
            }

            Prism(s_IceFall, new Vector3(top.x, 0f, top.z), r1 * 1.2f, r1 * 3f, 8, bottom + Mathf.Min(4f, height * 0.3f), bottom - 1f, true);
        }

        // Água congelada da garganta da Vila-Ponte (Ferramentas/Picos/gelo_garganta.py): a cascata gigante que enche a
        // fenda, o gelo nas paredes dela e o rio congelado no fundo, embaixo da ponte; mesma origem e giro do maciço,
        // materiais trocados pelo nome e colisão (menos nos pingentes)
        static void PlaceGorgeIce()
        {
            string path = $"{IcaroAssetUtility.Root}/Models/Picos/Gelo_Garganta.fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model)
            {
                Debug.LogWarning("[Ícaro] Vila-Ponte: falta o Gelo_Garganta.fbx (rode Ferramentas/Picos/gelo_garganta.py no Blender); " +
                                 "a garganta ficou sem a cascata e o rio congelados.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, s_World);
            go.name = "Gelo_Garganta";
            go.transform.SetPositionAndRotation(k_High, model.transform.rotation);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] ? materials[i].name : "";
                    materials[i] = name.StartsWith("MAT_Gelo_Lago") ? s_Materials.IceSheet : s_Materials.IceFall;
                }

                renderer.sharedMaterials = materials;
                if (!renderer.name.EndsWith("_Pingentes"))
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                else
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Physics.SyncTransforms();
        }

        // UV pelo mundo nas malhas de gelo com textura nova (16 px/m): cada face pelo eixo da normal; de pé, a horizontal
        // e a altura ('tileV' metros por volta da textura na vertical), de cima, o plano (4 x 4 m)
        static void WorldIceUvs(MeshData mesh, float tileV)
        {
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Vector3 p = mesh.Vertices[i], n = mesh.Normals[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                mesh.Uvs[i] = ay >= ax && ay >= az ? new Vector2(p.x / 4f, p.z / 4f)
                    : ax >= az ? new Vector2(p.z / 4f * Mathf.Sign(n.x), p.y / tileV)
                    : new Vector2(-p.x / 4f * Mathf.Sign(n.z), p.y / tileV);
            }
        }

        // Vila Pendurada (pedido do usuário em 2026-11-08: passagem, sem arena por ser pequena; uma vila genérica com o
        // que as outras já têm): no alto do paredão, de onde a escadaria de cantaria desce; casas góticas do kit pela
        // beira de trás, de frente para a praça, o lado da cidade aberto com o parapeito; colunas-lanterna, uma feirinha
        // de tendas, a área de convivência no trecho da beira mais longe das chegadas e o mobiliário das portas e das
        // chegadas (BuildVillageLife). As casas dos patamares e as casas em bloco saíram.
        static void BuildHangingVillage(Node n, Vector3 city, List<Vector3> gates)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3901);
            var root = new GameObject("Vila_Pendurada").transform;
            root.SetParent(s_World, false);
            Vector3 f = Flat(city - n.Center);
            float view = Mathf.Atan2(f.x, f.z);
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(Mathf.Atan2(g.x - n.Center.x, g.z - n.Center.z));

            // O mirante na torre a oeste, ligado à beira por uma ponte curta: a passagem fica livre como uma chegada
            Vector3 towerDir = HangingTower(n, city);
            Vector3 towerGate = n.Center + towerDir * n.Radius;
            gateAngles.Add(Mathf.Atan2(towerDir.x, towerDir.z));

            // Convivência: o trecho da beira de trás mais longe das chegadas; feirinha: o ângulo mais longe das
            // chegadas, da convivência e da vista
            float Far(float a, params float[] others)
            {
                float gap = Mathf.PI;
                foreach (float g in gateAngles)
                    gap = Mathf.Min(gap, AngleGap(a, g));
                foreach (float o in others)
                    gap = Mathf.Min(gap, AngleGap(a, o));
                return gap;
            }

            float gather = view + Mathf.PI, market = view, best = -1f;
            for (float deg = 100f; deg <= 260f; deg += 5f)
            {
                float a = view + deg * Mathf.Deg2Rad, gap = Far(a);
                if (gap > best)
                {
                    best = gap;
                    gather = a;
                }
            }

            best = -1f;
            for (float deg = 0f; deg < 360f; deg += 5f)
            {
                float a = view + deg * Mathf.Deg2Rad, gap = Far(a, gather, view);
                if (gap > best)
                {
                    best = gap;
                    market = a;
                }
            }

            Vector3 gatherAt = n.Center + Around(gather) * (n.Radius - 7f);
            List<HouseSpot> spots = KitHouseRing(root, n, f, 62f, gates, new[] { gatherAt, n.Center + towerDir * (n.Radius - 2f) }, 8f, 7);
            KitParapet(root, n, gates, (towerDir, 13f));

            // Colunas-lanterna em volta da praça, fora das chegadas e sem casa colada
            string kit = PicosGothicKit.VillagesFolder;
            foreach ((float deg, float distance) in new[] { (30f, 17f), (-30f, 17f), (100f, 16f), (-100f, 16f), (160f, 15f), (-160f, 15f) })
            {
                float angle = view + deg * Mathf.Deg2Rad;
                bool blocked = false;
                foreach (float gate in gateAngles)
                    blocked |= AngleGap(angle, gate) < 0.35f;
                if (blocked)
                    continue;
                Vector3 p = n.Center + Around(angle) * distance;
                Physics.SyncTransforms();
                if (Physics.CheckBox(p + Vector3.up * 5f, new Vector3(3.5f, 3.5f, 3.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                VillagePiece("PR_Poste", p, YawOf(-Around(angle)) + R(-10f, 10f), root, kit);
            }

            // Tecnologia: o relicário com o atril encostado numa casa
            VillageTech(root, n, "Vila Pendurada", gates, f, 62f, 0, true, 0, 0);

            BuildVillageLife(root, n, gates, f, spots, gather, "Vila Pendurada", overlook: false, station: false, market: market,
                paths: new List<Vector3> { towerGate });
            AttackDamage(root, n, new List<Vector3>(gates) { towerGate }, spots, "Vila Pendurada", 9501, 2);
            s_Random = saved;
        }

        // Mirante da Vila Pendurada (pedido do usuário em 2026-11-08: tapar o vão grande atrás da escadaria, onde a rocha
        // saiu, com uma plataforma como a do campanário da Vila da Ressonância): uma torre de pedra lavrada (TowerTop, 42 m
        // de quina a quina, o maior tamanho que cabe entre a escadaria, a beira da vila, a rocha a oeste e o caminho que
        // chega do oeste) na altura da vila, com o fuste de cantaria descendo até o chão do vão (39 m) e sem a rocha do pico
        // em volta (encostaria na escadaria); ligada à beira por uma ponte curta de pedra. Em cima, o mirante virado para a
        // cidade (luneta, placa do mapa, bancos e lixeiras) e colunas-lanterna. Sorteio próprio. Devolve a direção da vila
        // para a torre
        static readonly Vector2 k_HangingTowerPlan = new Vector2(276f, 474.5f);
        const float k_HangingTowerRadius = 21f;
        const float k_HangingTowerGround = 39f;

        static Vector3 HangingTower(Node n, Vector3 city)
        {
            Vector3 center = HP(k_HangingTowerPlan.x, k_HangingTowerPlan.y, n.Center.y);
            Vector3 dir = Flat(center - n.Center);
            var root = new GameObject("Vila_Pendurada_Mirante").transform;
            root.SetParent(s_World, false);

            System.Random saved = s_Random;
            s_Random = new System.Random(7421);
            var open = new List<int>(SidesToward(center, n.Center));
            float inner = TowerTop(center, k_HangingTowerRadius, 7421, open.ToArray(), -1, k_HangingTowerGround);
            Bridge(n.Center + dir * (n.Radius - 1.5f), center - dir * (inner - 1f), 8f, false);

            // Mirante virado para a cidade, longe da passagem da ponte
            Physics.SyncTransforms();
            Vector3 view = Flat(city - center);
            var L = new LifeLayout
            {
                N = new Node { Center = center, Radius = inner - 0.4f, Tilt = Quaternion.identity }, C = center,
                View = Mathf.Atan2(view.x, view.z), Root = root, Gates = new List<Vector3> { center - dir * inner },
            };
            LifeOverlook(L);

            // Colunas-lanterna nas quinas de dentro, fora da passagem e do mirante
            float viewAngle = Mathf.Atan2(view.x, view.z), bridgeAngle = Mathf.Atan2(-dir.x, -dir.z);
            foreach (float deg in new[] { 22.5f, 67.5f, 112.5f, 157.5f, 202.5f, 247.5f, 292.5f, 337.5f })
            {
                float a = deg * Mathf.Deg2Rad;
                if (AngleGap(a, bridgeAngle) < 40f * Mathf.Deg2Rad || AngleGap(a, viewAngle) < 40f * Mathf.Deg2Rad)
                    continue;
                LifePiece(L, "PR_Coluna_Bobinas", center + Around(a) * (inner - 3.2f), YawOf(-Around(a)), new Vector2(0.6f, 0.6f), 5.6f);
            }

            // Dois bancos de frente um para o outro no meio, com uma lixeira
            Vector3 side = Vector3.Cross(Vector3.up, view.normalized);
            LifePiece(L, "PR_Banco", center + side * 2.6f, YawOf(-side), new Vector2(1.05f, 0.35f), 1.6f);
            LifePiece(L, "PR_Banco", center - side * 2.6f, YawOf(side), new Vector2(1.05f, 0.35f), 1.6f);
            LifePiece(L, "PR_Lixeira", center + view.normalized * 2.2f, R(0f, 360f), new Vector2(0.35f, 0.35f), 1f);
            if (L.Skipped > 0)
                Debug.Log($"Mirante da Vila Pendurada: {L.Placed} peças; {L.Skipped} lugares sem espaço ficaram vazios");
            s_Random = saved;
            return dir;
        }

        // ---------- cidade ----------

        const float k_CityPodium = 28f;     // altura do pódio da basílica sobre o resto da cidade

        // O pódio em peças convexas, sem escadaria própria (só se chega pela rampa do Mercado Alto). Coordenadas (x, z): z para a ponte. A frente recua nas
        // pontas para não entrar nos prédios do Mercado e dos Relojoeiros; atrás, afina acompanhando a beira da ilha.
        static void BuildCityPodium(MeshData slab, Vector3 city, Vector3 toBridge)
        {
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            float top = city.y + k_CityPodium + 0.1f, bottom = city.y - 14f, mid = (top + bottom) * 0.5f;

            void Piece(params Vector2[] points)
            {
                var poly = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 p = city + across * points[i].x + toBridge * points[i].y;
                    poly[i] = new Vector3(p.x, mid, p.z);
                }

                ConvexPrism(slab, poly, Vector3.up, top - bottom);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                var outline = new List<Vector2> { new Vector2(side * 11.5f, 104f), new Vector2(side * 60f, 100f) };
                const float R = 113f;
                float from = Mathf.Atan2(88f, 70f), to = Mathf.Atan2(66f, -92f);
                for (int i = 0; i <= 10; i++)
                {
                    float a = Mathf.Lerp(from, to, i / 10f);
                    outline.Add(new Vector2(side * Mathf.Sin(a) * R, Mathf.Cos(a) * R));
                }

                outline.Add(new Vector2(side * 11.5f, -92f));
                Piece(outline.ToArray());
            }

            Piece(new Vector2(-11.5f, 104f), new Vector2(11.5f, 104f), new Vector2(11.5f, -92f), new Vector2(-11.5f, -92f));
            Piece(new Vector2(-66f, -92f), new Vector2(66f, -92f), new Vector2(66f, -270f), new Vector2(40f, -332f), new Vector2(-40f, -332f), new Vector2(-66f, -270f));
        }

        // A cidade em esboço (a etapa 3 detalha pela referência): ilha grande, praça com as duas colunatas em semicírculo
        // abertas para a ponte, obelisco, a basílica com a cúpula do lado oposto, quarteirões góticos e torres em volta e
        // um anel enorme girando por baixo
        static void BuildCitySketch(Vector3 city, Vector3 toBridge)
        {
            s_CityCenter = city;
            s_HasCity = true;
            var m = new RuinMesh(4101);
            if (PlaceCityRock(city))
                Clear(city, k_CityRadius + 40f);
            else
                SkyIsland(city, k_CityRadius, Quaternion.identity, 4101, false, 40);
            PlaceCityWalls(city);
            // Toda a área da basílica (praça, colunatas e igreja) fica num pódio, para engrandecer e ser vista de longe nas vilas
            Vector3 plaza = city + Vector3.up * k_CityPodium;
            var slab = new MeshData();
            // Praça, colunatas e obelisco: o dobro do esboço antigo
            const float k = 2f;
            BuildCityPodium(slab, city, toBridge);
            Pave(slab);
            s_Platforms.Add((plaza, 46f * k));
            foreach ((float from, float to) in new[] { (28f, 152f), (208f, 332f) })
                ArquipelagoRuinKit.RichRing(new RuinMesh(0), plaza + Vector3.up * 6.5f * k, Vector3.up, toBridge, 52f * k, 7f * k, 13f * k, from, to, s_Random,
                    false, false, plaza.y, 1000);       // numa malha descartada: as colunatas vêm do kit, e os sorteios não mudam
            BlockMesh.ChamferBox(m.Pick(), plaza + Vector3.up * 11f * k, new Vector3(2.2f, 22f, 2.2f) * k, Quaternion.identity, 0.2f);
            Spike(m.Pick(), plaza + Vector3.up * 22f * k, Vector3.up, 3f * k, 1.4f * k, 4, 45f, 100f);

            // A basílica e a cúpula em blocos saíram para o kit (PicosBasilica); continuam montadas numa malha descartada, para os sorteios não mudarem
            var oldBasilica = new RuinMesh(0);
            Quaternion facing = Quaternion.LookRotation(toBridge);
            Vector3 basilica = plaza - toBridge * 100f * k;
            BlockMesh.ChamferBox(oldBasilica.Pick(), basilica + Vector3.up * 15f * k, new Vector3(40f, 32f, 64f) * k, facing, 0.6f);
            Vector3 dome = basilica - toBridge * 8f * k;
            float baseY = plaza.y + 31f * k;
            Prism(oldBasilica.Pick(), dome, 15f * k, 15f * k, 24, baseY + 14f * k, baseY, true);
            const float domeRadius = 16f * k;
            for (int i = 0; i < 6; i++)
            {
                float p0 = i * 15f * Mathf.Deg2Rad, p1 = (i + 1) * 15f * Mathf.Deg2Rad;
                Prism(oldBasilica.Pick(), dome, domeRadius * Mathf.Cos(p1) + 0.4f * k, domeRadius * Mathf.Cos(p0), 24,
                    baseY + 14f * k + domeRadius * Mathf.Sin(p1), baseY + 14f * k + domeRadius * Mathf.Sin(p0), i == 5);
            }

            Prism(oldBasilica.Pick(), dome, 2.6f * k, 2.6f * k, 8, baseY + 21f * k + domeRadius, baseY + 13f * k + domeRadius, true);
            Spike(oldBasilica.Pick(), dome + Vector3.up * (baseY + 21f * k + domeRadius - plaza.y), Vector3.up, 6f * k, 1.2f * k, 8, 0f, 100f);

            // Quarteirões e torres
            Vector3 across = Vector3.Cross(Vector3.up, toBridge);
            Vector3[] keep =
            {
                city + toBridge * (k_CityRadius + 10f), city - toBridge * (k_CityRadius + 10f),
                city - toBridge * (k_CityRadius + 10f) + across * 16f * k, city - toBridge * (k_CityRadius + 10f) - across * 16f * k,
            };
            // A basílica dobrada é mais larga que os corredores: casas e torres ficam fora dela
            (Vector3, float)[] avoid = { (city - toBridge * 165f, 70f), (city - toBridge * 240f, 50f) };
            // As casas continuam do tamanho de antes; a cidade maior ganha mais quarteirões (a área ficou 4x maior)
            // (as casas onde fica a barbacã da Ponte do Reino, na beira da ilha, ficam de fora: PicosReinoSiege)
            // (os quarteirões do esboço saíram para o traçado de Archen Kaedra; continuam montados numa malha descartada,
            // para os sorteios não mudarem o resto do mapa)
            var scrap = new RuinMesh(0);
            ScatterHouses(scrap, city, Quaternion.identity, 62f * k, k_CityRadius * 0.86f, 220, Roof.Steep, 0.4f, keep, 2.2f, 2f, avoid,
                new[] { (ReinoAt(CityRimT() + 1f - 28f), 56f) });
            for (int t = 0; t < 14; t++)
            {
                float a = R(0f, 360f), d = R(70f, 135f) * k;
                Vector3 foot = city + Quaternion.Euler(0f, a, 0f) * Vector3.forward * d;
                bool inside = SegmentDistance(foot, keep[0], keep[1]) < 30f * k;
                foreach ((Vector3 c, float r) in avoid)
                    inside |= Vector2.Distance(new Vector2(foot.x, foot.z), new Vector2(c.x, c.z)) < r + 12f;
                if (inside)
                    continue;
                (Vector3 top, float width) = TowerShaft(scrap, foot, R(0f, 90f), R(9f, 14f), R(50f, 96f), false);
                Spike(scrap.Pick(), top, Vector3.up, width * 2.2f, width * 0.68f, 4, 45f, 100f);
            }

            BuildBasilicaSquare(city, toBridge);
            BuildArchenKaedraBlocks(m, city, toBridge);
            TurningRing("Anel_Cidade", city + Vector3.down * 100f, 370f, 10f, 14f, 0f, 360f, 1.2f);
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

        // Teleférico parado entre a estação da Vila dos Artífices e a torre do relógio: os dois cabos saem do anel de liga
        // da estação ('a' é o meio entre eles, 'half' a metade da distância entre os dois), cedem o quanto der sem
        // encostar no chão nem no parapeito da vila (raios para baixo) e chegam a um pórtico de pedra com outro anel de
        // liga deitado na outra ponta (era uma engrenagem de pedra; pedido do usuário em 2026-11-08); uma cabine pendurada
        // num dos cabos, cápsula de casco claro e vidro escuro como a parada na estação (veículo não é de pedra)
        static void Cableway(Vector3 a, Vector3 b, float half)
        {
            var m = new RuinMesh(5401);
            Vector3 along = Flat(b - a), side = Vector3.Cross(Vector3.up, along);
            Vector3 foot = b + along * 2f;
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(m.Pick(), foot + side * s * (half + 0.6f) + Vector3.up * 4.5f, new Vector3(1.2f, 9f, 1.2f),
                    Quaternion.LookRotation(along), 0.08f);
            BlockMesh.ChamferBox(m.Pick(), foot + Vector3.up * 9.3f, new Vector3(half * 2f + 2.6f, 0.9f, 1.4f), Quaternion.LookRotation(along), 0.08f);
            Vector3 end = foot + Vector3.up * 10.2f;
            var hull = new MeshData();
            var glass = new MeshData();
            var glow = new MeshData();
            {
                // Anel de liga deitado onde os cabos prendem, com o veio de luz por cima, seis raios e o cubo aceso,
                // assentado na viga do pórtico por um colar
                BlockMesh.RingArc(s_Alloy, end, Vector3.up, along, half, 0.32f, 0.5f, 0f, 360f, 48);
                BlockMesh.RingArc(glow, end + Vector3.up * 0.26f, Vector3.up, along, half, 0.1f, 0.04f, 0f, 360f, 48);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 dir = Quaternion.AngleAxis(k * 60f + 30f, Vector3.up) * along;
                    BlockMesh.Box(s_Alloy, end + dir * (half * 0.5f + 0.25f), new Vector3(0.14f, 0.14f, half - 0.5f), Quaternion.LookRotation(dir));
                }

                BlockMesh.Prism(s_Alloy, end + Vector3.down * 0.5f, Vector3.up, 0.8f, 0.55f, 12);
                BlockMesh.Prism(glow, end + Vector3.up * 0.28f, Vector3.up, 0.04f, 0.4f, 12);
            }

            float length = Vector3.Distance(a, end), sag = length * 0.04f;
            Physics.SyncTransforms();
            for (int tries = 0; tries < 12; tries++)
            {
                bool clear = true;
                for (int i = 1; i < 40 && clear; i++)
                {
                    float t = i / 40f;
                    Vector3 q = Vector3.Lerp(a, end, t) + Vector3.down * (sag * 4f * t * (1f - t));
                    if (Vector2.Distance(new Vector2(q.x, q.z), new Vector2(a.x, a.z)) < 3f)
                        continue;
                    foreach (float s in new[] { -1f, 1f })
                        if (Physics.Raycast(q + side * s * half, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                            clear = false;
                }

                if (clear)
                    break;
                sag *= 0.7f;
            }

            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 Cable(float t) => Vector3.Lerp(a, end, t) + side * s * half + Vector3.down * (sag * 4f * t * (1f - t));
                const int pieces = 32;
                for (int i = 0; i < pieces; i++)
                    Bar(Cable(i / (float)pieces), Cable((i + 1f) / pieces), 0.12f, 0.12f);
                if (s < 0f)
                    continue;
                Vector3 hook = Cable(0.6f);
                Bar(hook, hook + Vector3.down * 2.2f, 0.12f, 0.12f);
                // A cabine é um veículo (nada de pedra; pedido do usuário em 2026-11-08): cápsula de casco claro com as
                // quinas bem arredondadas, faixa de vidro escuro dos lados e nas pontas, veio lilás e a placa de liga
                // da suspensão no teto
                Vector3 cabin = hook + Vector3.down * 3.6f;
                Quaternion turn = Quaternion.LookRotation(along);
                BlockMesh.ChamferBox(hull, cabin, new Vector3(2.4f, 2.4f, 3.2f), turn, 0.55f);
                BlockMesh.ChamferBox(glass, cabin + Vector3.up * 0.3f, new Vector3(2.46f, 0.8f, 2.0f), turn, 0.15f);
                BlockMesh.ChamferBox(glass, cabin + Vector3.up * 0.3f, new Vector3(1.2f, 0.7f, 3.26f), turn, 0.15f);
                foreach (float face in new[] { -1f, 1f })
                    BlockMesh.Box(glow, cabin + side * face * 1.215f - Vector3.up * 0.28f, new Vector3(0.03f, 0.07f, 2.0f), turn);
                BlockMesh.ChamferBox(s_Alloy, cabin + Vector3.up * 1.3f, new Vector3(1.0f, 0.3f, 1.3f), turn, 0.08f);
            }

            CreateRuin("Teleferico", m, true);
            GridSceneKit.CreateObject(k_Prefix + "_Teleferico_Casco", "Teleferico_Casco", hull, s_Materials.Hull, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Teleferico_Vidro", "Teleferico_Vidro", glass, s_Materials.ShipGlass, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Teleferico_Luz", "Teleferico_Luz", glow, s_Materials.Glow, s_World, false);
        }
    }
}
