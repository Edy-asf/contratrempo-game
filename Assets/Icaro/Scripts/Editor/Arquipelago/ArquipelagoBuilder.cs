using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;
using NavMeshModifier = Unity.AI.Navigation.NavMeshModifier;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // "Ícaro > Arquipélago > Gerar Arquipélago": o bioma Arquipélago (substitui o Charco) numa cena própria
    // (Scenes/Arquipelago.unity); BuildInto monta o mesmo bioma dentro da cena da Via, depois do desfiladeiro que vem
    // do Bosque (ViaCoastBuilder). Noite verde-azulada com névoa densa e aurora boreal. Ilhas em degraus numa grade de
    // 2 m (topo de capim, penhascos de pedra até o mar, terraços com rampas), ligadas por travessias.
    // - Costa: o continente ocupa a borda sul de ponta a ponta, com um penhasco de 20 m sobre a praia. A Via chega na
    //   praça do alto (paredões dos lados, parapeito, arco de entrada) e desce por uma escadaria em zigue-zague até a
    //   praia; dali, pontes, uma coluna tombada e ilhotas levam à ilha de chegada.
    // Arenas (planejadas: Anel Partido, Farol, Cemitério de Anéis e a Ponte Colossal do chefe; feitas por etapas):
    // - Anel Partido (sala 7): ilha grande partida ao meio por um canal, com um anel colossal em pé sobre ele e
    //   semiafundado. Uma ponte de pedra cruza o canal no meio. Em cada metade, um mirante de runas (pirâmide de três
    //   degraus com rampas em volta e um arco do anel no topo).
    // - Travessia: ponte quebrada (pular ou dash) até uma ilhota e ponte inteira até a Enseada.
    // - Enseada (sala 8): ilha média com uma baía recortada, colunata em volta dela, um arco partido e um paredão de
    //   rocha mais alto com escadaria.
    // - Travessia: pedras e colunas para pular e dois destroços flutuantes que vão e voltam (FloatingPlatform) até o
    //   cais do Farol.
    // - Farol (sala 9): rochedo em três terraços (praia, platô e topo) ligados por escadarias, o farol no topo com
    //   escada em espiral por fora até a varanda (14 m) e o feixe girando (só visual), e destroços boiando junto à
    //   praia norte que levam quem sobe neles.
    // Cobertura de ruínas facetadas nas ilhas; no mar, anéis gigantes quebrados, torres e palácios afundados
    // (silhuetas na névoa). Mar: quem cai volta ao último ponto seguro perdendo vida (SeaHazard); inimigos que caem
    // morrem. Peças sem curvas nem esferas (BlockMesh). Rodar de novo refaz a cena.
    public static class ArquipelagoBuilder
    {
        const string k_ScenePath = IcaroAssetUtility.ScenesFolder + "/Arquipelago.unity";
        const string k_NavMeshFolder = IcaroAssetUtility.ScenesFolder + "/Arquipelago_NavMesh";
        const string k_Prefix = "Arquipelago";

        const float k_Cell = GridSceneKit.Cell;
        const int k_Size = 270;            // células de cada lado do mapa de ilhas
        const float k_SeaBed = -4f;        // fundo dos penhascos (abaixo da água)

        class Island
        {
            public Vector2 Center;         // células
            public float RadiusX, RadiusZ;
            public float Height;           // metros acima do mar
            public Vector2 PlateauCenter;
            public float PlateauRadiusX, PlateauRadiusZ;
            public float PlateauHeight;
            public Terrace[] Terraces;     // níveis mais altos, um dentro do outro (o último vale)
            public int Arena;              // 0 = fora de combate, 1 = Anel Partido, 2 = Farol
            public int Seed;
        }

        class Terrace
        {
            public Vector2 Center;
            public float RadiusX, RadiusZ;
            public float Height;
        }

        static readonly Island[] k_Islands =
        {
            // Chegada
            new Island { Center = new Vector2(52, 75), RadiusX = 7, RadiusZ = 6, Height = 2f, Seed = 1 },
            // Anel Partido: metade oeste e metade leste (o canal é aberto depois, em CarveChannel)
            new Island { Center = new Vector2(54, 110), RadiusX = 20, RadiusZ = 16, Height = 3f, Arena = 1,
                PlateauCenter = new Vector2(47, 116), PlateauRadiusX = 6, PlateauRadiusZ = 5, PlateauHeight = 4f, Seed = 2 },
            new Island { Center = new Vector2(90, 110), RadiusX = 20, RadiusZ = 16, Height = 3f, Arena = 1,
                PlateauCenter = new Vector2(98, 104), PlateauRadiusX = 6, PlateauRadiusZ = 6, PlateauHeight = 4f, Seed = 3 },
            // Ilhotas de pedra (cenário)
            new Island { Center = new Vector2(16, 146), RadiusX = 4, RadiusZ = 4, Height = 9f, Seed = 4 },
            new Island { Center = new Vector2(126, 80), RadiusX = 3, RadiusZ = 3, Height = 12f, Seed = 5 },
            new Island { Center = new Vector2(58, 200), RadiusX = 6, RadiusZ = 5, Height = 5f, Seed = 6 },
            // Travessia do Anel Partido ao Farol: ilhota da ponte quebrada e ilhota do primeiro destroço
            new Island { Center = new Vector2(114, 132), RadiusX = 4, RadiusZ = 3.5f, Height = 3f, Seed = 7 },
            new Island { Center = new Vector2(157, 170), RadiusX = 3, RadiusZ = 3, Height = 2f, Seed = 8 },
            // Farol: rochedo em três terraços (praia, platô do meio e topo com o farol)
            new Island
            {
                Center = new Vector2(170, 198), RadiusX = 24, RadiusZ = 19, Height = 2f, Arena = 3, Seed = 9,
                Terraces = new[]
                {
                    new Terrace { Center = new Vector2(171, 201), RadiusX = 16, RadiusZ = 12.5f, Height = 5f },
                    new Terrace { Center = new Vector2(172, 203), RadiusX = 7, RadiusZ = 6, Height = 8f },
                },
            },
            // Enseada: ilha média com um paredão de rocha mais alto e uma baía (terraço no fundo do mar = água)
            new Island
            {
                Center = new Vector2(134, 150), RadiusX = 17, RadiusZ = 13, Height = 2.5f, Arena = 2, Seed = 10,
                Terraces = new[]
                {
                    new Terrace { Center = new Vector2(140, 156), RadiusX = 8, RadiusZ = 6, Height = 5f },
                    new Terrace { Center = new Vector2(149, 147), RadiusX = 5, RadiusZ = 4.5f, Height = k_SeaBed },
                },
            },
            // Continente (a forma vem da linha da costa, em BuildHeights; a elipse fica fora do mapa)
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2f, Seed = 11 },
            // Ilhotas da costa, da praia até a chegada
            new Island { Center = new Vector2(60, 35), RadiusX = 4, RadiusZ = 3.5f, Height = 2.5f, Seed = 12 },
            new Island { Center = new Vector2(46, 46), RadiusX = 4, RadiusZ = 4, Height = 3f, Seed = 13 },
            new Island { Center = new Vector2(59, 54), RadiusX = 3.5f, RadiusZ = 3.5f, Height = 2.5f, Seed = 14 },
        };

        const int k_CrossingA = 6, k_CrossingB = 7, k_FarolIsland = 8, k_Enseada = 9, k_Mainland = 10,
            k_CoastIslets = 11;

        // Costa: o continente vai da borda sul até a linha da praia (CoastBeach, em células); o penhasco, com o topo
        // em k_CliffTop, até a linha dele (CoastCliff). A praça do alto (onde a Via chega) fica em volta de k_PlazaX.
        const float k_CliffTop = 20f;
        const float k_PlazaX = 104f;
        const float k_PlazaHalfWidth = 20f;
        static float CoastBeach(float x) => 23f + 2.5f * Mathf.Sin(x * 0.13f) + 1.5f * Mathf.Sin(x * 0.31f + 1f);
        static float CoastCliff(float x) => 10f + 1.5f * Mathf.Sin(x * 0.17f + 2f) + Mathf.Sin(x * 0.41f);

        // Onde a Via chega (topo do penhasco, na borda sul do mapa, no meio da praça)
        static readonly Vector3 k_Arrival = new Vector3(k_PlazaX, k_CliffTop, 0f);

        // Deslocamento do bioma na cena (zero na cena própria; na Via, para o topo do penhasco encostar no desfiladeiro)
        static Vector3 s_Offset;
        static readonly Vector2 k_CoveCenter = new Vector2(149, 147);

        // Topo dos cais e dos destroços da travessia (um pouco acima da praia, para as superfícies não coincidirem)
        const float k_DockTop = 2.1f;

        // Anel Partido: canal entre as metades (células) e o anel em pé sobre ele (metros)
        const float k_ChannelX = 72f;
        const float k_ChannelHalfWidth = 3.5f;
        static readonly Vector3 k_RingCenter = new Vector3(k_ChannelX * k_Cell, -14f, 220f);
        const float k_RingRadius = 42f;
        const float k_BridgeZ = 111f;

        // Linha d'água das ruínas: abaixo dela a pedra fica molhada, com cracas
        const float k_WaterLine = 0.8f;

        static float[,] s_Height;      // metros; k_SeaBed = mar
        static int[,] s_Island;        // índice + 1
        static bool[,] s_Ramp;
        static Vector2Int[,] s_RampDir;
        static ArquipelagoTextureKit.Set s_Materials;

        static Vector2Int V(int x, int z) => new Vector2Int(x, z);

        [MenuItem("Ícaro/Arquipélago/Gerar Arquipélago", priority = 47)]
        static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Arquipélago", "Texturas", 0.1f);
                s_Materials = ArquipelagoTextureKit.Build();
                GridTheme theme = Theme();
                Material sky = GridSceneKit.NightSkyMaterial(theme);

                EditorUtility.DisplayProgressBar("Arquipélago", "Cena", 0.3f);
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = ViaAeonidaBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);

                var world = new GameObject("Mapa").transform;
                PlacePlayer(scene);
                EditorSceneManager.SaveScene(scene, k_ScenePath);
                string report = BuildContent(world, Vector3.zero, k_NavMeshFolder);
                GridSceneKit.AddDistanceCulling(world, 300f, "Mar", "Gigantes_Fundo", "Feixe_Farol", "Destroco_");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorUtility.DisplayProgressBar("Arquipélago", "Oclusão (alguns minutos)", 0.97f);
                GridSceneKit.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Arquipélago gerado em {k_ScenePath}: {report}. Dê Play nessa cena.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Monta o Arquipélago numa cena já aberta (a Via), com o topo da praça do penhasco em 'arrival' (o fim do
        // desfiladeiro). Devolve as texturas do bioma (o desfiladeiro usa as mesmas).
        public static ArquipelagoTextureKit.Set BuildInto(Transform parent, Vector3 arrival, string navMeshFolder,
            out string report)
        {
            s_Materials = ArquipelagoTextureKit.Build();
            var world = new GameObject("Arquipelago").transform;
            world.SetParent(parent, false);
            report = BuildContent(world, arrival - k_Arrival, navMeshFolder);
            return s_Materials;
        }

        // Tudo do bioma dentro de 'world': montado na origem e depois deslocado por 'offset' (antes do NavMesh)
        static string BuildContent(Transform world, Vector3 offset, string navMeshFolder)
        {
            s_Offset = offset;
            GridTheme theme = Theme();
            BuildHeights();
            BuildTerrain(world);
            BuildSea(world);

            // Ruínas: cada bloco sorteia um tom; o musgo e a pedra molhada saem no fim (CreateRuin)
            var ruins = new RuinMesh(101);
            var ring = new RuinMesh(202);
            var giants = new RuinMesh(303);
            var silhouettes = new RuinMesh(404, single: true);
            var lighthouse = new RuinMesh(505);
            var rocks = new RuinMesh(606, 1);
            ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
            ArquipelagoRuinKit.VineMaterial = s_Materials.Vine;
            ArquipelagoRuinKit.VineSize = s_Materials.VineSize;
            (Vector3 doorPoint, Vector3 doorForward) = BuildConnections(ruins);
            BuildCoast(ruins, rocks);
            BuildChannelBridge(ruins);
            var crossing = new Crossing();
            BuildEnseadaCrossing(ruins, ref crossing);
            BuildFarolCrossing(ruins, world, ref crossing);
            BuildEnseada(ruins);
            BuildFarolStairs(ruins);
            BuildLighthouse(lighthouse, world);
            BuildFarolDrift(world);
            BuildBrokenRing(ring);
            BuildLookouts(ruins);
            List<Vector3> cover = BuildCover(ruins);
            BuildGiants(giants, silhouettes);
            CreateRuin("Ruinas", ruins, s_Materials.StoneTones, world, true);
            CreateRuin("Rochas", rocks, new[] { s_Materials.Cliff }, world, true, -100f);
            CreateRuin("AnelColossal", ring, s_Materials.StoneDarkTones, world, true);
            CreateRuin("Farol", lighthouse, s_Materials.StoneTones, world, true);
            CreateRuin("Gigantes", giants, s_Materials.StoneDarkTones, world, false);
            GridSceneKit.CreateObject(k_Prefix + "_Gigantes_Fundo", "Gigantes_Fundo", silhouettes.Tones[0],
                s_Materials.Silhouette, world, false, false);
            ArquipelagoRuinKit.Vines.Build(k_Prefix + "_Trepadeiras", world);
            ArquipelagoRuinKit.Vines = null;
            world.gameObject.AddComponent<CameraFarClip>();

            // Tufos e flores: um lote próprio (anda junto com o bioma quando ele é deslocado na Via)
            GridSceneKit.BillboardBatch outer = GridSceneKit.ActiveBatch;
            GridSceneKit.ActiveBatch = new GridSceneKit.BillboardBatch(world);
            PlaceDecor(world, cover);
            GridSceneKit.ActiveBatch.Build(k_Prefix + "_Sprite");
            GridSceneKit.ActiveBatch = outer;

            var hazard = new GameObject("Mar_Queda").AddComponent<SeaHazard>();
            hazard.transform.SetParent(world, false);

            RoomManager room = BuildRoom(world, "Arena_AnelPartido", "Anel Partido", 1, 7, doorPoint, doorForward,
                crossing.AnelExit, crossing.AnelExitForward, out NavMeshSurface surface);
            RoomManager enseadaRoom = BuildRoom(world, "Arena_Enseada", "Enseada", 2, 8, crossing.EnseadaDoor,
                crossing.EnseadaForward, crossing.EnseadaExit, crossing.EnseadaExitForward,
                out NavMeshSurface enseadaSurface);
            RoomManager farolRoom = BuildRoom(world, "Arena_Farol", "Farol", 3, 9, crossing.FarolDoor,
                crossing.FarolForward, null, Vector3.zero, out NavMeshSurface farolSurface);

            // Onde a câmera pode estar (oclusão): as ilhas, a costa e o alto do farol
            GridSceneKit.AddOcclusionArea("Oclusao_Arquipelago", world,
                new Vector3(k_Size * k_Cell * 0.5f, 12f, k_Size * k_Cell * 0.5f),
                new Vector3(k_Size * k_Cell, 40f, k_Size * k_Cell));

            // Entrada do bioma: logo depois de onde a Via chega (anuncia "Arquipélago" no visor)
            BiomeZone zone = GridSceneKit.AddBiomeZone(theme, k_Arrival + Vector3.forward * 8f,
                new Vector3(k_PlazaHalfWidth * 2f, 12f, 4f), world);
            zone.BiomeName = "Arquipélago";

            // Desloca o bioma para o lugar dele na cena (as rotas dos destroços e o nível do mar são guardados em
            // coordenadas do mundo)
            world.position += offset;
            foreach (FloatingPlatform platform in world.GetComponentsInChildren<FloatingPlatform>())
            {
                for (int i = 0; i < platform.Route.Length; i++)
                    platform.Route[i] += offset;
            }

            hazard.SeaLevel = offset.y;

            EditorUtility.DisplayProgressBar("Arquipélago", "NavMesh e spawns", 0.7f);
            Physics.SyncTransforms();
            GridSceneKit.BakeNavMesh(surface, navMeshFolder, "Arena_1");
            GridSceneKit.BakeNavMesh(enseadaSurface, navMeshFolder, "Arena_2");
            GridSceneKit.BakeNavMesh(farolSurface, navMeshFolder, "Arena_3");
            int spawns = PlaceSpawns(room, doorPoint + offset, 1);
            int enseadaSpawns = PlaceSpawns(enseadaRoom, crossing.EnseadaDoor + offset, 2);
            int farolSpawns = PlaceSpawns(farolRoom, crossing.FarolDoor + offset, 3);
            return $"Anel Partido ({spawns} pontos de spawn), Enseada ({enseadaSpawns}) e Farol ({farolSpawns}), " +
                   $"{cover.Count} ruínas de cobertura";
        }

        // Ruína em objetos por material: um por tom de pedra, o musgo (faces de cima), a pedra molhada (abaixo da linha
        // d'água) e o fundo dos vãos
        internal static void CreateRuin(string name, RuinMesh mesh, Material[] tones, Transform parent, bool collider,
            float waterLine = k_WaterLine)
        {
            RuinMesh.Layers layers = mesh.Split(waterLine);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < layers.Tones.Length; i++)
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Tom{i + 1}", $"Pedra_{i + 1}", layers.Tones[i],
                    tones[i % tones.Length], root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Musgo", "Musgo", layers.Moss, s_Materials.Moss, root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Molhada", "Molhada", layers.Wet, s_Materials.Wet, root,
                collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Vaos", "Vaos", layers.Dark, s_Materials.Shadow, root, collider);
        }

        // Noite verde-azulada, névoa densa (as estruturas viram silhuetas), aurora boreal, sem planeta
        internal static GridTheme Theme() => new GridTheme
        {
            Biome = "Arquipelago",
            BiomeIndex = 1,
            SceneName = "Arquipelago",
            FogColor = new Color(0.05f, 0.2f, 0.19f),
            FogDensity = 0.007f,
            AmbientSky = new Color(0.55f, 0.85f, 0.8f),
            AmbientEquator = new Color(0.4f, 0.62f, 0.58f),
            AmbientGround = new Color(0.2f, 0.3f, 0.28f),
            MoonColor = new Color(0.7f, 0.95f, 0.9f),
            MoonIntensity = 1.1f,
            SkyBlue = new Color(0.05f, 0.28f, 0.26f),
            SkyPurple = new Color(0.03f, 0.12f, 0.16f),
            SkyBlueHeight = 0.25f,
            SkyTop = 0.65f,
            PlanetSize = 0f,
            StarDensity = 0.14f,
            AuroraStrength = 1.6f,
            AuroraLow = new Color(0.15f, 1.3f, 0.7f),
            AuroraHigh = new Color(0.45f, 0.3f, 1.1f),
            AuroraHeight = 0.45f,
            SporeColors = new[] { new Color(0.6f, 1f, 0.95f), new Color(0.85f, 1f, 0.9f), new Color(0.3f, 0.95f, 0.7f) },
        };

        static Vector3 CellToWorld(Vector2 cell, float height) => new Vector3(cell.x * k_Cell, height, cell.y * k_Cell);

        // ---------- ilhas ----------

        static bool InEllipse(Vector2 cell, Vector2 center, float rx, float rz, int seed)
        {
            Vector2 d = cell - center;
            var q = new Vector2(d.x / rx, d.y / rz);
            float angle = Mathf.Atan2(q.y, q.x);
            float limit = 1f + 0.12f * Mathf.Sin(3f * angle + seed) + 0.07f * Mathf.Sin(5f * angle + seed * 2f);
            return q.magnitude <= limit;
        }

        static void BuildHeights()
        {
            s_Height = new float[k_Size, k_Size];
            s_Island = new int[k_Size, k_Size];
            s_Ramp = new bool[k_Size, k_Size];
            s_RampDir = new Vector2Int[k_Size, k_Size];
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                s_Height[x, z] = k_SeaBed;
                var cell = new Vector2(x + 0.5f, z + 0.5f);
                for (int i = 0; i < k_Islands.Length; i++)
                {
                    Island island = k_Islands[i];
                    if (!InEllipse(cell, island.Center, island.RadiusX, island.RadiusZ, island.Seed))
                        continue;
                    s_Island[x, z] = i + 1;
                    float height = island.PlateauHeight > 0f &&
                                   InEllipse(cell, island.PlateauCenter, island.PlateauRadiusX, island.PlateauRadiusZ,
                                       island.Seed + 10)
                        ? island.PlateauHeight
                        : island.Height;
                    for (int t = 0; island.Terraces != null && t < island.Terraces.Length; t++)
                    {
                        Terrace terrace = island.Terraces[t];
                        if (InEllipse(cell, terrace.Center, terrace.RadiusX, terrace.RadiusZ, island.Seed + 10 + t))
                            height = terrace.Height;
                    }

                    s_Height[x, z] = height;
                }
            }

            // Continente: praia e penhasco pela linha da costa, de ponta a ponta do mapa
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] != 0 || z + 0.5f >= CoastBeach(x + 0.5f))
                    continue;
                s_Island[x, z] = k_Mainland + 1;
                s_Height[x, z] = z + 0.5f < CoastCliff(x + 0.5f) ? k_CliffTop : k_Islands[k_Mainland].Height;
            }

            CarveChannel();

            // Rampas: em cada terraço, duas células do nível de baixo que sobem 1 m até ele (lados livres)
            for (int i = 0; i < k_Islands.Length; i++)
            {
                Island island = k_Islands[i];
                if (island.PlateauHeight <= 0f)
                    continue;
                var random = new System.Random(90 + i);
                var candidates = new List<(Vector2Int cell, Vector2Int dir)>();
                Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
                for (int x = 2; x < k_Size - 2; x++)
                for (int z = 2; z < k_Size - 2; z++)
                {
                    if (s_Island[x, z] != i + 1 || !Mathf.Approximately(s_Height[x, z], island.Height))
                        continue;
                    foreach (Vector2Int d in dirs)
                    {
                        var up = new Vector2Int(x + d.x, z + d.y);
                        var back = new Vector2Int(x - d.x, z - d.y);
                        var side1 = new Vector2Int(x + d.y, z + d.x);
                        var side2 = new Vector2Int(x - d.y, z - d.x);
                        if (Mathf.Approximately(s_Height[up.x, up.y], island.PlateauHeight) &&
                            Mathf.Approximately(s_Height[back.x, back.y], island.Height) &&
                            Mathf.Approximately(s_Height[side1.x, side1.y], island.Height) &&
                            Mathf.Approximately(s_Height[side2.x, side2.y], island.Height))
                            candidates.Add((V(x, z), d));
                    }
                }

                var chosen = new List<Vector2Int>();
                for (int n = 0; n < 2 && candidates.Count > 0; n++)
                {
                    // A segunda rampa fica longe da primeira
                    int pick = random.Next(candidates.Count);
                    for (int tries = 0; tries < 40 && chosen.Exists(c => Vector2Int.Distance(c, candidates[pick].cell) < 8f); tries++)
                        pick = random.Next(candidates.Count);
                    (Vector2Int cell, Vector2Int dir) = candidates[pick];
                    s_Ramp[cell.x, cell.y] = true;
                    s_RampDir[cell.x, cell.y] = dir;
                    chosen.Add(cell);
                    candidates.RemoveAt(pick);
                }
            }
        }

        // Canal do Anel Partido: abre o mar entre as duas metades, com a margem irregular
        static void CarveChannel()
        {
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                int island = s_Island[x, z];
                if (island == 0 || k_Islands[island - 1].Arena != 1)
                    continue;
                float wobble = 0.9f * Mathf.Sin(z * 0.45f) + 0.6f * Mathf.Sin(z * 1.3f + 1f);
                if (Mathf.Abs(x + 0.5f - k_ChannelX) < k_ChannelHalfWidth + wobble)
                {
                    s_Island[x, z] = 0;
                    s_Height[x, z] = k_SeaBed;
                }
            }
        }

        static float HeightAt(int x, int z) =>
            x < 0 || z < 0 || x >= k_Size || z >= k_Size ? k_SeaBed : s_Height[x, z];

        static bool IsLand(int x, int z) => HeightAt(x, z) > 0f;

        // Topo de capim, rampas e penhascos (lados entre alturas diferentes, até o fundo do mar)
        static void BuildTerrain(Transform parent)
        {
            var top = new MeshData();
            var cliffs = new MeshData();
            Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (!IsLand(x, z))
                    continue;
                float h = s_Height[x, z];
                if (s_Ramp[x, z])
                {
                    AddRamp(top, cliffs, x, z, h, s_RampDir[x, z]);
                    continue;
                }

                top.AddHorizontal(x, z, h);
                foreach (Vector2Int d in dirs)
                {
                    int nx = x + d.x, nz = z + d.y;
                    float neighbor = HeightAt(nx, nz);
                    // A rampa vizinha cuida do próprio lado
                    if (neighbor < h && !(nx >= 0 && nz >= 0 && nx < k_Size && nz < k_Size && s_Ramp[nx, nz] &&
                                          s_RampDir[nx, nz] == -d))
                        cliffs.AddSideRange(x, z, d, neighbor, h);
                }
            }

            GridSceneKit.CreateObject(k_Prefix + "_Capim", "Ilhas_Topo", top, s_Materials.Grass, parent, true);
            GridSceneKit.CreateObject(k_Prefix + "_Penhascos", "Ilhas_Penhascos", cliffs, s_Materials.Cliff, parent, true);
        }

        // Rampa de uma célula subindo 1 m na direção 'dir', com os lados em triângulo
        static void AddRamp(MeshData top, MeshData cliffs, int x, int z, float h, Vector2Int dir)
        {
            Vector3 c = GridSceneKit.CellCenter(x, z);
            var forward = new Vector3(dir.x, 0f, dir.y);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (k_Cell * 0.5f);
            Vector3 low = c - forward * (k_Cell * 0.5f) + Vector3.up * h;
            Vector3 high = c + forward * (k_Cell * 0.5f) + Vector3.up * (h + 1f);
            Vector3 slopeNormal = Vector3.Cross(side, high - low).normalized;
            if (slopeNormal.y < 0f)
                slopeNormal = -slopeNormal;
            top.AddQuadAuto(low - side, high - side, high + side, low + side, slopeNormal);
            cliffs.AddTriangleAuto(low - side, high - side, high - side - Vector3.up, -side);
            cliffs.AddTriangleAuto(low + side, high + side, high + side - Vector3.up, side);
        }

        static void BuildSea(Transform parent)
        {
            var sea = new MeshData();
            float size = 1600f;
            Vector3 center = new Vector3(k_Size * k_Cell * 0.5f, 0f, k_Size * k_Cell * 0.5f);
            Vector3 a = center + new Vector3(-size, 0f, -size) * 0.5f;
            sea.AddQuadAuto(a, a + Vector3.forward * size, a + new Vector3(size, 0f, size), a + Vector3.right * size,
                Vector3.up);
            GameObject go = GridSceneKit.CreateObject(k_Prefix + "_Mar", "Mar", sea, s_Materials.Sea, parent, false, false,
                false);
            if (go)
                go.AddComponent<WaterRipple>().Speed = new Vector2(0.02f, 0.01f);
        }

        // ---------- ligações ----------

        // Pontos de borda entre duas ilhas na reta de 'from' a 'to' (células): último chão da primeira e primeiro da
        // segunda, já em metros e na altura de cada uma
        static (Vector3 a, Vector3 b) EdgePoints(Vector2 from, Vector2 to)
        {
            Vector2 dir = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            Vector3 a = Vector3.zero, b = Vector3.zero;
            bool leftFirst = false;
            for (float s = 0f; s <= length; s += 0.25f)
            {
                Vector2 p = from + dir * s;
                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.y);
                bool land = IsLand(x, z);
                if (!leftFirst && land)
                    a = CellToWorld(p, HeightAt(x, z));
                else if (!leftFirst)
                    leftFirst = true;
                else if (land)
                {
                    b = CellToWorld(p, HeightAt(x, z));
                    break;
                }
            }

            return (a, b);
        }

        // Ponte de chegada (inteira, com o portão do lado da arena)
        static (Vector3 door, Vector3 forward) BuildConnections(RuinMesh mesh)
        {
            Island start = k_Islands[0], west = k_Islands[1];
            s_ConnectionPoints.Clear();

            (Vector3 s0, Vector3 s1) = EdgePoints(start.Center, west.Center + new Vector2(0f, -4f));
            Bridge(mesh, s0, s1, 0f, 11);
            s_ConnectionPoints.Add(s1);

            Vector3 forward = s1 - s0;
            forward.y = 0f;
            return (s1 + forward.normalized * 1.5f, forward.normalized);
        }

        // Ponte de pedra de 4 m: tabuleiro de lajes chanfradas levemente tortas sobre arcos de aduelas entre pilares
        // (do fundo do mar ao tabuleiro) e parapeito de balaústres com corrimão, com falhas. Com 'gap' > 0, falta um
        // trecho no meio (pular ou dash).
        static void Bridge(RuinMesh mesh, Vector3 from, Vector3 to, float gap, int seed)
        {
            var random = new System.Random(seed);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            from -= flat * 1.5f;  // entra um pouco nas ilhas
            to += flat * 1.5f;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float segment = 3f;
            int count = Mathf.CeilToInt(length / segment);
            float gapStart = (length - gap) * 0.5f, gapEnd = (length + gap) * 0.5f;
            bool InGap(float s) => gap > 0f && s > gapStart && s < gapEnd;
            Vector3 Deck(float s) => from + dir.normalized * s;

            for (int i = 0; i < count; i++)
            {
                float s0 = i * segment, s1 = Mathf.Min(length, s0 + segment);
                if (gap > 0f && s1 > gapStart && s0 < gapEnd)
                {
                    // Pedaço quebrado na beira do vão
                    if (s0 < gapStart)
                        s1 = gapStart;
                    else if (s1 > gapEnd)
                        s0 = gapEnd;
                    else
                        continue;
                }

                float size = s1 - s0;
                Vector3 center = Deck((s0 + s1) * 0.5f) - Vector3.up * 0.3f;
                Quaternion tilt = rotation * Quaternion.Euler((float)random.NextDouble() * 2f - 1f, 0f,
                    (float)random.NextDouble() * 2f - 1f);
                BlockMesh.ChamferBox(mesh.Pick(), center, new Vector3(4f, 0.6f, size - 0.08f), tilt, 0.12f);

                // Parapeito: meio-fio, balaústres e corrimão (às vezes falta um trecho)
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() < 0.2)
                        continue;
                    Vector3 edge = center + right * side * 1.8f;
                    BlockMesh.Box(mesh.Pick(), edge + Vector3.up * 0.4f, new Vector3(0.45f, 0.2f, size - 0.1f), tilt);
                    int posts = Mathf.Max(2, Mathf.FloorToInt(size / 0.75f));
                    for (int k = 0; k < posts; k++)
                    {
                        float along = (k + 0.5f) / posts - 0.5f;
                        BlockMesh.Box(mesh.Pick(), edge + rotation * Vector3.forward * (along * size) + Vector3.up * 0.85f,
                            new Vector3(0.2f, 0.7f, 0.2f), tilt);
                    }

                    if (random.NextDouble() < 0.85)
                        BlockMesh.ChamferBox(mesh.Pick(), edge + Vector3.up * 1.3f, new Vector3(0.38f, 0.22f, size - 0.2f),
                            tilt, 0.06f);
                }
            }

            // Pilares e arcos só sobre a água
            float water0 = 1.5f, water1 = length - 1.5f;
            int spans = Mathf.Max(1, Mathf.RoundToInt((water1 - water0) / 9f));
            float spanLength = (water1 - water0) / spans;
            for (int k = 0; k <= spans; k++)
            {
                float s = water0 + k * spanLength;
                Vector3 top = Deck(s) - Vector3.up * 0.6f;
                float height = top.y - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(top.x, k_SeaBed + height * 0.5f, top.z),
                    new Vector2(4.4f, 1.6f), height, Quaternion.LookRotation(flat), 0.2f);
                if (k == spans || InGap(s + spanLength * 0.5f))
                    continue;

                // Arco entre este pilar e o próximo, com o alto encostando no tabuleiro
                float radius = spanLength * 0.5f - 0.8f;
                Vector3 mid = Deck(s + spanLength * 0.5f) - Vector3.up * (0.6f + radius);
                ArquipelagoRuinKit.RichRing(mesh, mid, right, Vector3.up, radius, 0.7f, 3.6f, -90f, 90f, random, false,
                    false, 0f, 0);
            }
        }

        // ---------- costa ----------

        // Praça no alto do penhasco (onde o desfiladeiro chega pela borda sul): paredões de rocha dos lados, parapeito
        // na beira com um arco de entrada e duas colunas, e a escadaria em zigue-zague que desce até a praia. Da praia,
        // pontes e ilhotas até a ilha de chegada: ponte inteira, ponte quebrada (pular ou dash), coluna tombada e ponte.
        static void BuildCoast(RuinMesh mesh, RuinMesh rocks)
        {
            var random = new System.Random(919);
            Island beach = k_Islands[k_Mainland];
            float x0 = k_PlazaX - k_PlazaHalfWidth, x1 = k_PlazaX + k_PlazaHalfWidth;

            // Beira do penhasco em cada coluna da praça (z do lado de fora da última célula do topo)
            float EdgeAt(float worldX)
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt(worldX / k_Cell), 0, k_Size - 1);
                int z = 0;
                while (z < k_Size - 1 && HeightAt(cx, z + 1) >= k_CliffTop - 0.01f)
                    z++;
                return (z + 1) * k_Cell;
            }

            // Paredões dos lados da praça, do fim do desfiladeiro até a beira
            foreach (float side in new[] { -1f, 1f })
            {
                float wallX = k_PlazaX + side * (k_PlazaHalfWidth + 3f);
                float end = EdgeAt(wallX) + 2f;
                for (float z = 0f; z < end; z += 4.5f)
                    RockPile(rocks, new Vector3(wallX + side * (float)random.NextDouble() * 2f, k_CliffTop, z),
                        12f + (float)random.NextDouble() * 9f, random);
            }

            // Escadaria em zigue-zague na frente do penhasco: 4 lances (cada um numa faixa mais longe da rocha) com
            // patamares entre eles; o primeiro sai do arco, no alto, indo para o leste
            const float band = 3.4f, run = 0.42f;
            const int flights = 4;
            float flightRise = (k_CliffTop - beach.Height) / flights;
            int steps = Mathf.RoundToInt(flightRise / 0.27f);
            float rise = flightRise / steps, length = steps * run;
            // As faixas começam na parte mais saliente da beira; a primeira encosta na rocha em todo o comprimento
            // (onde a beira recua, não fica buraco entre a praça e a escada)
            float face = 0f, inner = float.MaxValue;
            for (float x = k_PlazaX - band - 2f; x <= k_PlazaX + length + band + 2f; x += k_Cell)
            {
                face = Mathf.Max(face, EdgeAt(x));
                inner = Mathf.Min(inner, EdgeAt(x));
            }

            inner -= 0.5f;
            float BandZ(int k) => face + k * band;
            float InnerZ(int k) => k == 0 ? inner : BandZ(k);
            Block(mesh, k_PlazaX - band, k_PlazaX, inner, BandZ(1), k_CliffTop + 0.05f);
            float sx = k_PlazaX, dir = 1f;
            Vector3 foot = Vector3.zero;
            for (int k = 0; k < flights; k++)
            {
                float top = k_CliffTop - k * flightRise;
                int count = k == flights - 1 ? steps - 1 : steps;
                for (int i = 0; i < count; i++)
                {
                    float near = sx + dir * i * run, far = sx + dir * (i + 1) * run;
                    Block(mesh, Mathf.Min(near, far), Mathf.Max(near, far), InnerZ(k), BandZ(k + 1),
                        top - (i + 1) * rise);
                }

                float end = sx + dir * length;
                // Mureta do lado de fora do lance, acompanhando a descida
                Vector3 outer = Vector3.forward * (BandZ(k + 1) - 0.2f);
                Slope(mesh.Pick(), new Vector3(end, top - flightRise + 0.5f, 0f) + outer,
                    new Vector3(sx, top + 0.5f, 0f) + outer, 0.4f);
                if (k < flights - 1)
                {
                    // Patamar no fim do lance, ocupando esta faixa e a próxima, com mureta na ponta e do lado de fora
                    float bottom = top - flightRise;
                    Block(mesh, Mathf.Min(end, end + dir * band), Mathf.Max(end, end + dir * band), InnerZ(k),
                        BandZ(k + 2), bottom);
                    BlockMesh.Box(mesh.Pick(), new Vector3(end + dir * (band - 0.2f), bottom + 0.45f, (BandZ(k) +
                        BandZ(k + 2)) * 0.5f), new Vector3(0.4f, 0.9f, band * 2f), Quaternion.identity);
                }
                else
                    foot = new Vector3(end, beach.Height, BandZ(k + 1) + 1.5f);

                sx = end;
                dir = -dir;
            }

            // Parapeito na beira da praça (menos em cima da escadaria), arco de entrada e duas colunas
            float entry0 = k_PlazaX - band, entry1 = k_PlazaX;
            for (float x = x0; x < x1; x += k_Cell)
            {
                if (x + k_Cell > entry0 && x < entry1)
                    continue;
                float z = EdgeAt(x + k_Cell * 0.5f) - 0.3f;
                BlockMesh.Box(mesh.Pick(), new Vector3(x + k_Cell * 0.5f, k_CliffTop + 0.15f, z),
                    new Vector3(k_Cell, 0.3f, 0.5f), Quaternion.identity);
                BlockMesh.Box(mesh.Pick(), new Vector3(x + 0.5f, k_CliffTop + 0.6f, z), new Vector3(0.22f, 0.7f, 0.22f),
                    Quaternion.identity);
                BlockMesh.Box(mesh.Pick(), new Vector3(x + 1.5f, k_CliffTop + 0.6f, z), new Vector3(0.22f, 0.7f, 0.22f),
                    Quaternion.identity);
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x + k_Cell * 0.5f, k_CliffTop + 1.05f, z),
                    new Vector3(0.4f, 0.2f, k_Cell), Quaternion.Euler(0f, 90f, 0f), 0.05f);
            }

            var gate = new Vector3((entry0 + entry1) * 0.5f, k_CliffTop, face - 0.5f);
            ArquipelagoRuinKit.RichRing(mesh, gate, Vector3.forward, Vector3.right, 2.6f, 0.7f, 1f, 0f, 180f, random,
                false, false, k_CliffTop, 3);
            foreach (float side in new[] { -1f, 1f })
                ArquipelagoRuinKit.Column(mesh, gate + Vector3.right * side * 5.5f - Vector3.forward * 1.5f, 5.5f,
                    0.7f, random, false);
            s_ConnectionPoints.AddRange(new[] { gate, foot });

            // Da praia à ilha de chegada
            Vector2 FootCell() => new Vector2(foot.x / k_Cell, foot.z / k_Cell);
            Island a = k_Islands[k_CoastIslets], b = k_Islands[k_CoastIslets + 1], c = k_Islands[k_CoastIslets + 2];
            Island arrival = k_Islands[0];
            (Vector3 p0, Vector3 p1) = EdgePoints(FootCell(), a.Center);
            Bridge(mesh, p0, p1, 0f, 81);
            (Vector3 q0, Vector3 q1) = EdgePoints(a.Center, b.Center);
            Bridge(mesh, q0, q1, 2.5f, 82);
            (Vector3 r0, Vector3 r1) = EdgePoints(b.Center, c.Center);
            FallenColumn(mesh.Pick(), r0, r1);
            (Vector3 t0, Vector3 t1) = EdgePoints(c.Center, arrival.Center);
            Bridge(mesh, t0, t1, 0f, 84);
            s_ConnectionPoints.AddRange(new[] { p0, p1, q0, q1, r0, r1, t0, t1 });
        }

        // Coluna gigante tombada entre duas ilhas: prisma de 8 lados deitado com uma face plana em cima (dá para andar),
        // capitel quebrado numa ponta
        static void FallenColumn(MeshData mesh, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            float radius = 2.8f;
            float apothem = radius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            Vector3 start = from - flat * 3f;
            Vector3 end = to + flat * 3f;
            Vector3 axis = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            // A face de cima fica na altura do chão: o eixo passa 'apothem' abaixo
            BlockMesh.Prism(mesh, start - Vector3.up * (apothem + 0.05f), axis, length, radius, 8, 22.5f);
            BlockMesh.Box(mesh, end - Vector3.up * apothem + axis * 1.5f + Vector3.up * 0.4f,
                new Vector3(radius * 2.4f, radius * 1.4f, 2.5f), Quaternion.LookRotation(axis) * Quaternion.Euler(0f, 0f, 12f));
        }

        // Bloco maciço da escadaria (do fundo do mar até 'top'), com as quinas chanfradas
        static void Block(RuinMesh mesh, float xMin, float xMax, float zMin, float zMax, float top)
        {
            float height = top - k_SeaBed;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3((xMin + xMax) * 0.5f, k_SeaBed + height * 0.5f,
                (zMin + zMax) * 0.5f), new Vector2(xMax - xMin, zMax - zMin), height, Quaternion.identity, 0.06f);
        }

        // Pilha de rocha (paredão): dois ou três blocos que afinam para cima, um pouco tortos, do chão ('foot') até
        // 'height' acima dele
        static void RockPile(RuinMesh mesh, Vector3 foot, float height, System.Random random)
        {
            float width = 6f + (float)random.NextDouble() * 3f;
            float y = foot.y - 3f;
            int parts = 2 + random.Next(2);
            for (int i = 0; i < parts; i++)
            {
                float h = (height + 3f) / parts * (0.8f + (float)random.NextDouble() * 0.4f);
                Quaternion tilt = Quaternion.Euler((float)random.NextDouble() * 6f - 3f,
                    (float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 6f - 3f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(foot.x, y + h * 0.5f, foot.z),
                    new Vector2(width, width * (0.8f + (float)random.NextDouble() * 0.3f)), h, tilt, width * 0.15f);
                y += h * 0.9f;
                width *= 0.75f;
            }
        }

        // ---------- Anel Partido ----------

        // Ponte de pedra inteira cruzando o canal no meio
        static void BuildChannelBridge(RuinMesh mesh)
        {
            (Vector3 a, Vector3 b) = EdgePoints(new Vector2(k_ChannelX - 14f, k_BridgeZ),
                new Vector2(k_ChannelX + 14f, k_BridgeZ));
            Bridge(mesh, a, b, 0f, 31);
            s_ConnectionPoints.AddRange(new[] { a, b });
        }

        // Ponte que desaba (CollapsingBridge; o Anel Partido não usa, fica para outras arenas): cada pedaço do
        // tabuleiro é um objeto com a malha centrada nele (para cair girando); os pilares ficam no mar depois da queda
        // (vão na malha estática 'pillars')
        static CollapsingBridge CollapsibleBridge(RuinMesh pillars, Transform parent, Vector3 from, Vector3 to,
            int index, float collapseAt)
        {
            var random = new System.Random(300 + index);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            from -= flat * 1.5f;
            to += flat * 1.5f;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            var root = new GameObject($"PonteQueDesaba_{index}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation((from + to) * 0.5f, rotation);
            var bridge = root.AddComponent<CollapsingBridge>();
            bridge.CollapseAt = collapseAt;

            // Ao cair, o NavMesh perde só o trecho sobre a água
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = new Vector3(5f, 4f, Mathf.Max(1f, length - 4f));
            obstacle.enabled = false;

            float segment = 3f;
            int count = Mathf.CeilToInt(length / segment);
            var segments = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                float s0 = i * segment, s1 = Mathf.Min(length, s0 + segment);
                float size = s1 - s0 - 0.08f;
                var piece = new MeshData();
                BlockMesh.ChamferBox(piece, Vector3.zero, new Vector3(4f, 0.6f, size), Quaternion.identity, 0.12f);
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() < 0.3)
                        continue;
                    float height = 0.6f + (float)random.NextDouble() * 0.4f;
                    BlockMesh.Box(piece, new Vector3(side * 1.75f, 0.3f + height * 0.5f, 0f),
                        new Vector3(0.5f, height, size - 0.3f), Quaternion.identity);
                }

                GameObject go = GridSceneKit.CreateObject($"{k_Prefix}_Ponte{index}_{i:00}", $"Pedaco_{i:00}", piece,
                    s_Materials.Stone, root.transform, true);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                Vector3 center = from + dir.normalized * ((s0 + s1) * 0.5f) - Vector3.up * 0.3f;
                Quaternion tilt = rotation * Quaternion.Euler((float)random.NextDouble() * 3f - 1.5f, 0f,
                    (float)random.NextDouble() * 3f - 1.5f);
                go.transform.SetPositionAndRotation(center, tilt);
                segments.Add(go.transform);

                if (i % 3 == 1)
                    BlockMesh.Prism(pillars.Pick(), center - Vector3.up * 0.6f, Vector3.down, center.y + 6f, 0.9f, 8,
                        22.5f);
            }

            bridge.Segments = segments.ToArray();
            return bridge;
        }

        // O anel colossal em pé sobre o canal: semiafundado, de blocos chanfrados com canaleta e aros grossos, com o
        // alto partido (o trecho entre 38° e 58° caiu) e um anel interno mais fino, quebrado antes
        static void BuildBrokenRing(RuinMesh ring)
        {
            var random = new System.Random(808);
            BlockMesh.Tile = k_Cell * 2f;
            // Ângulo 0 = topo; positivo para o norte
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius, 6f, 7f, -78f, 38f,
                random, false, true);
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius, 6f, 7f, 58f, 112f,
                random, true, false);
            // A perna do anel interno desce até abaixo da água (a -62° ela ainda ficava 2 m acima, flutuando)
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius - 7.5f, 3f, 4f, -74f,
                12f, random, false, true, 0f, 4);
            BlockMesh.Tile = k_Cell;
        }

        // Um mirante de runas em cada metade, com a rampa de baixo virada para o canal
        static void BuildLookouts(RuinMesh mesh)
        {
            float top = k_Islands[1].Height;
            var random = new System.Random(909);
            RuneLookout(mesh, new Vector2(52f, 106f), -90f, top, random);
            RuneLookout(mesh, new Vector2(93f, 116f), 90f, top, random);
        }

        // Mirante de runas (ponto alto): pirâmide de três degraus largos de pedra, 1,5 m cada (topo a 4,5 m do chão;
        // sobe-se pulando de degrau em degrau), com base, cornija em cada degrau e, no topo, um arco quebrado do anel
        // como monumento e cobertura. 'yaw' gira a pirâmide.
        static void RuneLookout(RuinMesh mesh, Vector2 centerCell, float yaw, float ground, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 origin = CellToWorld(centerCell, ground);
            Vector3 P(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);

            float[] sizes = { 12f, 8f, 4.5f };
            const float step = 1.5f;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), P(0f, 0.175f, 0f), Vector2.one * (sizes[0] + 0.6f), 0.35f, rotation,
                0.2f);
            for (int i = 0; i < sizes.Length; i++)
            {
                float height = step * (i + 1);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), P(0f, height * 0.5f, 0f), Vector2.one * sizes[i], height, rotation,
                    0.25f);
                // Cornija logo abaixo da borda (o topo do degrau continua liso para andar)
                for (int face = 0; face < 4; face++)
                {
                    Quaternion side = rotation * Quaternion.Euler(0f, 180f + face * 90f, 0f);
                    BlockMesh.ChamferBox(mesh.Pick(), origin + side * Vector3.forward * (sizes[i] * 0.5f + 0.1f) +
                                                      Vector3.up * (height - 0.4f),
                        new Vector3(sizes[i] + 0.4f, 0.3f, 0.2f), side, 0.06f);
                }

                ArquipelagoRuinKit.HangVine(P(sizes[i] * 0.3f, height - 0.25f, -sizes[i] * 0.5f - 0.25f),
                    rotation * Vector3.back, random);
                ArquipelagoRuinKit.HangVine(P(-sizes[i] * 0.5f - 0.25f, height - 0.25f, 0.3f * sizes[i]),
                    rotation * Vector3.left, random);
            }

            // Arco quebrado do anel no topo (uma perna presa no degrau, a outra partida no alto)
            ArquipelagoRuinKit.RichRing(mesh, P(0f, step * 3f - 0.8f, 0f), rotation * Vector3.forward, Vector3.up, 2f,
                0.6f, 0.9f, -95f, 60f, random, false, true, ground + step * 3f, 2);

            float half = sizes[0] * 0.5f;
            s_ConnectionPoints.AddRange(new[]
            {
                P(0f, 0f, 0f), P(half, 0f, half), P(-half, 0f, half), P(half, 0f, -half),
                P(-half, 0f, -half),
            });
        }

        // Rampa: laje cujo topo vai de 'low' a 'high' (pontos no meio da borda de baixo e de cima)
        static void Slope(MeshData mesh, Vector3 low, Vector3 high, float width)
        {
            const float thick = 0.8f;
            Vector3 dir = high - low;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 center = (low + high) * 0.5f - rotation * Vector3.up * (thick * 0.5f);
            BlockMesh.ChamferBox(mesh, center, new Vector3(width, thick, dir.magnitude), rotation, 0.1f);
        }

        // ---------- Farol: travessia ----------

        struct Crossing
        {
            public Vector3 AnelExit, AnelExitForward;        // saída do Anel Partido
            public Vector3 EnseadaDoor, EnseadaForward;      // entrada da Enseada (no fim da ponte)
            public Vector3 EnseadaExit, EnseadaExitForward;  // saída da Enseada (para as pedras)
            public Vector3 FarolDoor, FarolForward;          // entrada do Farol (no cais)
        }

        // Do Anel Partido à Enseada: ponte quebrada (vão no meio: pular ou dash) da metade leste até uma ilhota e ponte
        // inteira da ilhota até o portão da Enseada
        static void BuildEnseadaCrossing(RuinMesh mesh, ref Crossing crossing)
        {
            Island islet = k_Islands[k_CrossingA], enseada = k_Islands[k_Enseada];
            (Vector3 a, Vector3 b) = EdgePoints(new Vector2(96f, 116f), islet.Center);
            Bridge(mesh, a, b, 3f, 41);
            Vector3 flat = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            crossing.AnelExit = a - flat * 1.5f;
            crossing.AnelExitForward = flat;

            (Vector3 c, Vector3 d) = EdgePoints(islet.Center, enseada.Center);
            Bridge(mesh, c, d, 0f, 43);
            Vector3 into = Vector3.ProjectOnPlane(d - c, Vector3.up).normalized;
            crossing.EnseadaDoor = d + into * 1.5f;
            crossing.EnseadaForward = into;
            s_ConnectionPoints.AddRange(new[] { a, b, c, d });
        }

        // Da Enseada ao Farol: pedras e colunas partidas para pular até uma ilhota, e dois destroços flutuantes que vão
        // e voltam: da ilhota até uma plataforma de ruína no mar, e dela até o cais do Farol (onde fica o portão)
        static void BuildFarolCrossing(RuinMesh mesh, Transform parent, ref Crossing crossing)
        {
            var random = new System.Random(515);
            Island first = k_Islands[k_Enseada], second = k_Islands[k_CrossingB], farol = k_Islands[k_FarolIsland];

            // Pedras e colunas partidas da Enseada à ilhota (vãos de 2 a 3 m, descendo do paredão até a ilhota); o portão de
            // saída da Enseada fica no começo delas
            (Vector3 s0, Vector3 s1) = EdgePoints(first.Center, second.Center);
            Vector3 outward = Vector3.ProjectOnPlane(s1 - s0, Vector3.up).normalized;
            crossing.EnseadaExit = s0 - outward * 1.5f;
            crossing.EnseadaExitForward = outward;
            float gap = Vector3.Distance(new Vector3(s0.x, 0f, s0.z), new Vector3(s1.x, 0f, s1.z));
            int stones = Mathf.Max(2, Mathf.RoundToInt(gap / 5f) - 1);
            Vector3 side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(s1 - s0, Vector3.up).normalized);
            for (int k = 0; k < stones; k++)
            {
                float t = (k + 1f) / (stones + 1f);
                Vector3 foot = Vector3.Lerp(s0, s1, t) + side * ((float)random.NextDouble() * 1.6f - 0.8f);
                float top = Mathf.Lerp(s0.y, s1.y, t) + 0.1f + (float)random.NextDouble() * 0.4f;
                float height = top - k_SeaBed;
                if (k % 2 == 0)
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(foot.x, k_SeaBed + height * 0.5f, foot.z),
                        new Vector2(3f, 3f), height, Quaternion.Euler(0f, (float)random.NextDouble() * 90f, 0f), 0.3f);
                else
                    BlockMesh.Prism(mesh.Pick(), new Vector3(foot.x, k_SeaBed, foot.z), Vector3.up, height, 1.7f, 8,
                        22.5f + (float)random.NextDouble() * 20f);
            }

            // Plataforma de ruína no mar, ao sul do Farol, e o cais do Farol (todos com o topo a k_DockTop)
            Vector3 farolCenter = CellToWorld(farol.Center, farol.Height);
            float coast = farolCenter.z;
            for (float s = 0f; s < farol.RadiusZ * 2f; s += 0.25f)
            {
                if (!IsLand(Mathf.FloorToInt(farol.Center.x), Mathf.FloorToInt(farol.Center.y - s)))
                    break;
                coast = (farol.Center.y - s) * k_Cell;
            }

            float x = farolCenter.x;
            float pierTip = coast - 8f;
            Dock(mesh, new Vector3(x, 0f, (coast + 1.5f + pierTip) * 0.5f), new Vector2(5f, coast + 1.5f - pierTip), random);
            crossing.FarolDoor = new Vector3(x, k_DockTop, coast + 0.5f);
            crossing.FarolForward = Vector3.forward;

            float shuttleGap = 12f;
            Vector3 middle = new Vector3(x, 0f, pierTip - shuttleGap - 3.5f);
            Dock(mesh, middle, new Vector2(7f, 7f), random);
            ArquipelagoRuinKit.Column(mesh, middle + new Vector3(2.6f, k_DockTop, 2.6f) - Vector3.up * 0.05f, 2.6f,
                0.55f, random, true);

            // Cais da segunda ilhota, apontando para a plataforma
            Vector3 secondCenter = CellToWorld(second.Center, second.Height);
            float secondTip = middle.x - 3.5f - shuttleGap;
            float secondStart = secondCenter.x;
            Dock(mesh, new Vector3((secondStart + secondTip) * 0.5f, 0f, middle.z),
                new Vector2(secondTip - secondStart, 4.5f), random);

            // Destroços que vão e voltam (param 0,4 m antes de cada cais)
            const float half = 2.3f, clearance = 0.4f;
            Platform("Destroco_Travessia_1", parent, new[]
            {
                new Vector3(secondTip + clearance + half, k_DockTop, middle.z),
                new Vector3(middle.x - 3.5f - clearance - half, k_DockTop, middle.z),
            }, false, 2.2f, 0f, 61);
            Platform("Destroco_Travessia_2", parent, new[]
            {
                new Vector3(x, k_DockTop, middle.z + 3.5f + clearance + half),
                new Vector3(x, k_DockTop, pierTip - clearance - half),
            }, false, 2.2f, 6f, 62);

            s_ConnectionPoints.AddRange(new[] { crossing.FarolDoor, middle, secondCenter, s0 });
        }

        // ---------- Enseada ----------

        // Escadaria do paredão, colunata em volta da baía (umas inteiras, outras partidas, tambores caídos) e um arco
        // partido no meio da ilha
        static void BuildEnseada(RuinMesh mesh)
        {
            var random = new System.Random(626);
            Island enseada = k_Islands[k_Enseada];
            Terrace shelf = enseada.Terraces[0];
            TerraceStairs(mesh, shelf.Center, new Vector2(-0.7f, -0.7f), enseada.Height, shelf.Height, 4f);

            Vector3 cove = CellToWorld(k_CoveCenter, enseada.Height);
            for (float angle = 105f; angle <= 255f; angle += 18f)
            {
                var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 spot = cove + dir * 15f;
                int x = Mathf.FloorToInt(spot.x / k_Cell), z = Mathf.FloorToInt(spot.z / k_Cell);
                if (!IsLand(x, z) || !Mathf.Approximately(HeightAt(x, z), enseada.Height) || !Interior(x, z, 1))
                    continue;
                spot.y = enseada.Height;
                bool broken = random.NextDouble() < 0.45;
                ArquipelagoRuinKit.Column(mesh, spot, broken ? 2.5f + (float)random.NextDouble() * 2f : 6.5f, 0.85f,
                    random, broken);
                s_ConnectionPoints.Add(spot);
            }

            Vector3 arch = CellToWorld(new Vector2(126f, 148f), enseada.Height);
            ArquipelagoRuinKit.Arch(mesh, arch, 35f, 0.35f, random);
            s_ConnectionPoints.Add(arch);
        }

        // Cais de pedra do fundo do mar até k_DockTop ('size' = largura em x e comprimento em z), com meio-fio e
        // blocos soltos
        static void Dock(RuinMesh mesh, Vector3 center, Vector2 size, System.Random random)
        {
            float height = k_DockTop - k_SeaBed;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(center.x, k_SeaBed + height * 0.5f, center.z), size,
                height, Quaternion.identity, 0.3f);
            ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(center.x, k_DockTop - 0.55f, center.z),
                size + Vector2.one * 0.4f, 0.3f, Quaternion.identity, 0.12f);
            if (random.NextDouble() < 0.7)
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(center.x + size.x * 0.4f, k_DockTop + 0.25f,
                        center.z - size.y * 0.4f), new Vector3(0.6f, 0.5f, 0.6f),
                    Quaternion.Euler(10f, (float)random.NextDouble() * 90f, 6f), 0.08f);
        }

        // Destroço flutuante: laje de ruína com um pedaço quebrado por baixo, a malha centrada no objeto (que anda)
        static void Platform(string name, Transform parent, Vector3[] route, bool loop, float speed, float offset,
            int seed)
        {
            var random = new System.Random(seed);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = route[0];

            var mesh = new RuinMesh(seed);
            BlockMesh.ChamferBox(mesh.Pick(), new Vector3(0f, -0.45f, 0f), new Vector3(4.6f, 0.9f, 4.6f),
                Quaternion.Euler(0f, (float)random.NextDouble() * 6f - 3f, 0f), 0.2f);
            BlockMesh.ChamferBox(mesh.Pick(), new Vector3(0.2f, -1.25f, -0.15f), new Vector3(3.4f, 1f, 3f),
                Quaternion.Euler(6f, 20f, -5f), 0.25f);
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? 1.85f : -1.85f, sz = i == 0 ? 1.85f : -1.85f;
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(sx, 0.18f, sz), new Vector3(0.6f, 0.4f, 0.6f),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 90f, 0f), 0.08f);
            }

            CreateRuin(name, mesh, s_Materials.StoneTones, root.transform, true, -0.7f);
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var platform = root.AddComponent<FloatingPlatform>();
            platform.Route = route;
            platform.Loop = loop;
            platform.Speed = speed;
            platform.Offset = offset;
        }

        // Destroços que boiam junto à praia norte do Farol, indo e voltando em dois trechos da costa (um de cada lado),
        // para subir neles e flanquear
        static void BuildFarolDrift(Transform parent)
        {
            Island farol = k_Islands[k_FarolIsland];
            (float from, float to)[] arcs = { (28f, 84f), (96f, 152f) };
            for (int a = 0; a < arcs.Length; a++)
            {
                var points = new List<Vector3>();
                for (float angle = arcs[a].from; angle <= arcs[a].to + 0.01f; angle += 7f)
                {
                    var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    float edge = 0f;
                    for (float s = 0f; s < 40f; s += 0.25f)
                    {
                        Vector2 cell = farol.Center + dir * s;
                        if (s_Island[Mathf.Clamp(Mathf.FloorToInt(cell.x), 0, k_Size - 1),
                                Mathf.Clamp(Mathf.FloorToInt(cell.y), 0, k_Size - 1)] == k_FarolIsland + 1)
                            edge = s;
                    }

                    Vector3 coast = CellToWorld(farol.Center + dir * (edge + 0.5f), k_DockTop);
                    points.Add(coast + new Vector3(dir.x, 0f, dir.y) * 3.8f);
                }

                // Suaviza a costa recortada (média com os vizinhos, duas vezes)
                for (int pass = 0; pass < 2; pass++)
                {
                    var smooth = new List<Vector3>(points);
                    for (int i = 1; i < points.Count - 1; i++)
                        smooth[i] = (points[i - 1] + points[i] * 2f + points[i + 1]) * 0.25f;
                    points = smooth;
                }

                Platform($"Destroco_Farol_{a + 1}", parent, points.ToArray(), false, 1.6f, a * 9f, 71 + a);
            }
        }

        // Escadarias entre os terraços do Farol: da praia ao platô (sul, para o cais, e nordeste) e do platô ao topo
        // (sul e oeste)
        static void BuildFarolStairs(RuinMesh mesh)
        {
            Island farol = k_Islands[k_FarolIsland];
            Terrace middle = farol.Terraces[0], top = farol.Terraces[1];
            TerraceStairs(mesh, middle.Center, new Vector2(0f, -1f), farol.Height, middle.Height, 4f);
            TerraceStairs(mesh, middle.Center, new Vector2(0.7f, 0.7f), farol.Height, middle.Height, 4f);
            TerraceStairs(mesh, top.Center, new Vector2(0f, -1f), middle.Height, top.Height, 3.5f);
            TerraceStairs(mesh, top.Center, new Vector2(-1f, 0f), middle.Height, top.Height, 3.5f);
        }

        // Escada que desce da borda de um terraço (achada andando do centro na direção 'dir') até o nível de baixo:
        // degraus de 0,27 m com o bocel chanfrado e muretas inclinadas dos dois lados
        static void TerraceStairs(RuinMesh mesh, Vector2 centerCell, Vector2 dir, float low, float high, float width)
        {
            dir.Normalize();
            Vector2 edge = centerCell;
            for (float s = 0f; s < 40f; s += 0.25f)
            {
                Vector2 cell = centerCell + dir * s;
                if (HeightAt(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y)) < high - 0.01f)
                    break;
                edge = cell;
            }

            Vector3 start = CellToWorld(edge, high);
            var forward = new Vector3(dir.x, 0f, dir.y);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            int steps = Mathf.Max(2, Mathf.RoundToInt((high - low) / 0.27f));
            float rise = (high - low) / steps, depth = 0.42f;
            Quaternion rotation = Quaternion.LookRotation(right, Vector3.up);
            for (int k = 0; k < steps - 1; k++)
            {
                float y = high - (k + 1) * rise;
                float bottom = low - 0.3f;
                Vector3 c = start + forward * (depth * (k + 0.5f));
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(c.x, (y + bottom) * 0.5f, c.z),
                    new Vector3(depth, y - bottom, width), rotation, 0.06f);
            }

            // Muretas: do topo ao pé da escada, 0,5 m acima da linha dos degraus
            Vector3 foot = start + forward * (depth * (steps - 1)) + Vector3.up * (low - high);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 offset = right * s * (width * 0.5f + 0.25f) + Vector3.up * 0.5f;
                Slope(mesh.Pick(), foot + offset, start + offset, 0.5f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), foot + right * s * (width * 0.5f + 0.25f) + Vector3.up * 0.45f,
                    new Vector2(0.7f, 0.7f), 0.9f, rotation, 0.1f);
            }

            s_ConnectionPoints.AddRange(new[] { start, foot });
        }

        // ---------- Farol: a torre ----------

        // Farol no topo: base, fuste de pedra em três trechos que afinam (com frestas escuras), escada em
        // espiral por fora até a varanda (a 14 m, com parapeito, mísulas e uma abertura onde a escada chega), a
        // lanterna (pilares e telhado em degraus) e o feixe que gira (LighthouseBeam, com dois spots)
        static void BuildLighthouse(RuinMesh mesh, Transform parent)
        {
            Island farol = k_Islands[k_FarolIsland];
            Terrace top = farol.Terraces[1];
            Vector3 c = CellToWorld(top.Center, top.Height);
            var random = new System.Random(717);
            const float balcony = 14f;

            // Fuste: (de, até, raio)
            // Fuste: (de, até, raio dos cantos do octógono; as faces ficam a 0,92 dele). Sem cintas salientes, que
            // bateriam na cabeça de quem sobe a escada
            (float y0, float y1, float r)[] shaft = { (-0.3f, 4.8f, 3.6f), (4.8f, 9.6f, 3.35f), (9.6f, balcony - 0.1f, 3.1f) };
            float RadiusAt(float y) => (y < 4.8f ? 3.6f : y < 9.6f ? 3.35f : 3.1f) * 0.92f;
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * -0.3f, Vector3.up, 0.52f, 4.8f, 8, 22.5f);
            foreach ((float y0, float y1, float r) in shaft)
                BlockMesh.Prism(mesh.Pick(), c + Vector3.up * y0, Vector3.up, y1 - y0, r, 8, 22.5f);
            // Frestas no meio das faces (alternando de face a cada andar)
            foreach (float y in new[] { 3f, 7.6f, 11.8f })
            for (int k = 0; k < 4; k++)
            {
                Quaternion around = Quaternion.Euler(0f, k * 90f + (y > 7f && y < 10f ? 45f : 0f), 0f);
                BlockMesh.Box(mesh.Dark, c + Vector3.up * y + around * Vector3.forward * RadiusAt(y),
                    new Vector3(0.5f, 1.3f, 0.2f), around);
            }

            // Escada em espiral: degraus em balanço presos no fuste, subindo no sentido anti-horário a partir do sul
            const float stepRise = 0.25f, stepRun = 0.5f, stepWidth = 1.7f;
            float angle = 180f;
            int count = Mathf.RoundToInt((balcony - 0.22f) / stepRise) - 1;
            float lastAngle = angle;
            for (int k = 0; k < count; k++)
            {
                float y = 0.22f + (k + 1) * stepRise;
                float r = RadiusAt(y) + stepWidth * 0.5f;
                Quaternion around = Quaternion.Euler(0f, angle, 0f);
                Vector3 radial = around * Vector3.forward;
                BlockMesh.ChamferBox(mesh.Pick(), c + radial * r + Vector3.up * (y - 0.175f),
                    new Vector3(0.62f, 0.35f, stepWidth), around, 0.05f);
                if (k % 3 == 0)
                    BlockMesh.Box(mesh.Pick(), c + radial * (r + stepWidth * 0.45f) + Vector3.up * (y + 0.5f),
                        new Vector3(0.15f, 1f, 0.15f), around);
                lastAngle = angle;
                angle += stepRun / r * Mathf.Rad2Deg;
            }

            // Varanda: piso em 8 trechos, sem os que ficam em cima do último trecho da escada (por onde se sobe; a
            // abertura cobre os degraus acima de ~10,5 m, que não teriam espaço para a cabeça sob o piso e as mísulas)
            bool Opening(float a0, float a1)
            {
                float from = lastAngle - 100f, to = lastAngle + 12f;
                for (float a = a0; a <= a1; a += 3f)
                {
                    float d = Mathf.DeltaAngle(a, (from + to) * 0.5f);
                    if (Mathf.Abs(d) <= (to - from) * 0.5f)
                        return true;
                }

                return false;
            }

            const float inner = 2.6f, outer = 6.2f;
            for (int s = 0; s < 8; s++)
            {
                float a0 = s * 45f + 22.5f, a1 = a0 + 45f;
                if (Opening(a0, a1))
                    continue;
                BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony - 0.25f), Vector3.up, Vector3.forward,
                    (inner + outer) * 0.5f, BlockMesh.Chamfered(outer - inner, 0.5f, 0.08f), a0, a1, 2);
                BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony + 0.5f), Vector3.up, Vector3.forward,
                    outer - 0.15f, BlockMesh.Chamfered(0.3f, 1f, 0.05f), a0, a1, 2);
                Quaternion around = Quaternion.Euler(0f, (a0 + a1) * 0.5f, 0f);
                BlockMesh.ChamferBox(mesh.Pick(), c + around * Vector3.forward * 4.3f + Vector3.up * (balcony - 1f),
                    new Vector3(0.6f, 1f, 2.2f), around, 0.1f);
            }

            // Lanterna e telhado
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * (balcony - 0.05f), Vector3.up, 0.45f, 2.9f, 8, 22.5f);
            for (int k = 0; k < 8; k++)
            {
                Quaternion around = Quaternion.Euler(0f, k * 45f + 22.5f, 0f);
                BlockMesh.Box(mesh.Pick(), c + around * Vector3.forward * 2.5f + Vector3.up * (balcony + 1.9f),
                    new Vector3(0.35f, 3f, 0.35f), around);
            }

            (float y0, float h, float r)[] roof = { (17.4f, 0.5f, 3.2f), (17.9f, 0.7f, 2.4f), (18.6f, 0.7f, 1.5f),
                (19.3f, 1f, 0.6f) };
            foreach ((float y0, float h, float r) in roof)
                BlockMesh.Prism(mesh.Pick(), c + Vector3.up * y0, Vector3.up, h, r, 8, 22.5f);
            ArquipelagoRuinKit.HangVine(c + Vector3.up * (balcony - 0.5f) + Vector3.back * outer, Vector3.back, random,
                1.4f);
            ArquipelagoRuinKit.HangVine(c + Vector3.up * 9.5f + Vector3.left * (RadiusAt(9f) + 0.05f), Vector3.left,
                random, 1.2f);

            // Feixe e luzes
            var beam = new GameObject("Feixe_Farol");
            beam.transform.SetParent(parent, false);
            beam.transform.position = c + Vector3.up * (balcony + 1.9f);
            beam.AddComponent<LighthouseBeam>();
            var cone = new GameObject("Cone");
            cone.transform.SetParent(beam.transform, false);
            cone.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
            var renderer = cone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BeamMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (float s in new[] { -1f, 1f })
            {
                var spot = new GameObject("Luz").AddComponent<Light>();
                spot.transform.SetParent(beam.transform, false);
                spot.transform.localRotation = Quaternion.LookRotation(Vector3.right * s);
                spot.type = LightType.Spot;
                spot.range = 90f;
                spot.spotAngle = 12f;
                spot.intensity = 6f;
                spot.color = new Color(0.95f, 0.93f, 0.8f);
                spot.shadows = LightShadows.None;
            }

            var lamp = new GameObject("Lampada").AddComponent<Light>();
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = beam.transform.position;
            lamp.type = LightType.Point;
            lamp.range = 14f;
            lamp.intensity = 3f;
            lamp.color = new Color(0.95f, 0.93f, 0.8f);
            lamp.shadows = LightShadows.None;
            s_ConnectionPoints.Add(c);
        }

        // Feixe: dois cones de 8 lados para os dois lados (eixo x), claros perto da lanterna e sumindo na ponta
        static Mesh BeamMesh()
        {
            IcaroAssetUtility.EnsureFolder(GridSceneKit.MeshFolder);
            string path = $"{GridSceneKit.MeshFolder}/{k_Prefix}_FeixeFarol.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = k_Prefix + "_FeixeFarol" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int sides = 8;
            foreach (float s in new[] { -1f, 1f })
            {
                int start = vertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float t = i * Mathf.PI * 2f / sides;
                    var ring = new Vector3(0f, Mathf.Cos(t), Mathf.Sin(t));
                    vertices.Add(new Vector3(s * 0.8f, 0f, 0f) + ring * 0.35f);
                    vertices.Add(new Vector3(s * 110f, 0f, 0f) + ring * 7f);
                    colors.Add(new Color(1f, 1f, 1f, 0.32f));
                    colors.Add(new Color(1f, 1f, 1f, 0f));
                }

                for (int i = 0; i < sides; i++)
                {
                    int a = start + i * 2, b = start + ((i + 1) % sides) * 2;
                    triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Aditivo, dos dois lados, na cor da luz do farol (a transparência vem das cores dos vértices)
        static Material BeamMaterial()
        {
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(k_Prefix + "_FeixeFarol", null);
            material.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.78f, 1f));
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- ruínas de cobertura ----------

        // Muros de blocos aparelhados, colunas partidas, pilhas de blocos e tambores caídos nas ilhas de combate.
        // Devolve as posições (para a decoração e os spawns evitarem).
        static List<Vector3> BuildCover(RuinMesh mesh)
        {
            var random = new System.Random(1234);
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < 1500 && placed.Count < 26; attempt++)
            {
                int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                int island = s_Island[x, z];
                if (island == 0 || k_Islands[island - 1].Arena == 0 || !IsLand(x, z) || s_Ramp[x, z] ||
                    !Interior(x, z, 3))
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z];
                if (placed.Exists(p => Vector3.Distance(p, position) < 8f) || NearConnection(position))
                    continue;

                Quaternion yaw = Quaternion.Euler(0f, random.Next(0, 4) * 90f + (float)random.NextDouble() * 20f - 10f, 0f);
                switch (random.Next(4))
                {
                    case 0:
                        ArquipelagoRuinKit.AshlarWall(mesh, position, yaw, 5f + (float)random.NextDouble() * 3f,
                            2.5f + (float)random.NextDouble() * 1.5f, 1.2f, random);
                        break;
                    case 1:
                        ArquipelagoRuinKit.Column(mesh, position, 2.5f + (float)random.NextDouble() * 3f, 1.1f, random,
                            true);
                        break;
                    case 2: // pilha de blocos
                        BlockMesh.ChamferBox(mesh.Pick(), position + Vector3.up * 0.9f, new Vector3(2.4f, 1.8f, 2f), yaw,
                            0.2f);
                        BlockMesh.ChamferBox(mesh.Pick(), position + yaw * new Vector3(0.5f, 2.4f, 0.2f),
                            new Vector3(1.6f, 1.2f, 1.6f), yaw * Quaternion.Euler(0f, 25f, 0f), 0.15f);
                        BlockMesh.ChamferBox(mesh.Pick(), position + yaw * new Vector3(-1.9f, 0.35f, 0.9f),
                            new Vector3(0.9f, 0.7f, 0.8f), yaw * Quaternion.Euler(12f, 40f, 8f), 0.1f);
                        ArquipelagoRuinKit.HangVine(position + yaw * new Vector3(0f, 1.8f, 1.04f), yaw * Vector3.forward,
                            random, 0.6f);
                        break;
                    default:
                        ArquipelagoRuinKit.FallenDrums(mesh, position, yaw, 1f, random);
                        break;
                }

                placed.Add(position);
            }

            return placed;
        }

        // Célula com 'margin' células de chão da mesma ilha e na mesma altura em volta (longe da beira e das rampas)
        static bool Interior(int x, int z, int margin)
        {
            float h = s_Height[x, z];
            for (int dx = -margin; dx <= margin; dx++)
            for (int dz = -margin; dz <= margin; dz++)
            {
                int nx = x + dx, nz = z + dz;
                if (!Mathf.Approximately(HeightAt(nx, nz), h) || s_Ramp[Mathf.Clamp(nx, 0, k_Size - 1), Mathf.Clamp(nz, 0, k_Size - 1)])
                    return false;
            }

            return true;
        }

        static readonly List<Vector3> s_ConnectionPoints = new List<Vector3>();

        static bool NearConnection(Vector3 position)
        {
            foreach (Vector3 point in s_ConnectionPoints)
            {
                Vector3 d = point - position;
                d.y = 0f;
                if (d.magnitude < 9f)
                    return true;
            }

            return false;
        }

        // ---------- gigantes no mar ----------

        // Estruturas colossais no mar, em três faixas de distância (contadas do meio das ilhas):
        // - perto (40 a 140 m além da borda das ilhas): pedra com luz normal, musgo, base molhada e trepadeiras, onde
        //   dá para ver o detalhe;
        // - meio (140 a 410 m) e fundo (410 a 880 m): material de silhueta (vultos na névoa que nunca somem de vez),
        //   cada vez maiores para continuarem legíveis.
        // Anéis quebrados, torres partidas, arcos, escadarias que descem para a água e palácios afundados. A textura
        // cresce com a peça (BlockMesh.Tile), para os blocos não parecerem azulejo.
        static void BuildGiants(RuinMesh near, RuinMesh far)
        {
            var random = new System.Random(4321);
            // Contadas do meio das ilhas (sem o continente), a partir da borda delas
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] == 0 || s_Island[x, z] == k_Mainland + 1)
                    continue;
                low = Vector2.Min(low, new Vector2(x, z));
                high = Vector2.Max(high, new Vector2(x + 1, z + 1));
            }

            Vector3 middle = CellToWorld((low + high) * 0.5f, 0f);
            float reach = Vector2.Distance(low, high) * 0.5f * k_Cell;
            var placed = new List<(Vector3 position, float radius)>();
            GridSceneKit.Cards vines = ArquipelagoRuinKit.Vines;

            // (malha, distância mínima, máxima, escala, quantidade de cada peça: anéis, torres, arcos, escadas, palácios)
            var bands = new (RuinMesh mesh, float min, float max, float scale, int rings, int towers, int arches, int stairs, int palaces)[]
            {
                (near, reach + 40f, reach + 140f, 0.8f, 6, 4, 3, 2, 2),
                (far, reach + 140f, reach + 410f, 1.3f, 8, 7, 4, 2, 2),
                (far, reach + 410f, reach + 880f, 2.4f, 7, 6, 3, 0, 2),
            };

            foreach (var band in bands)
            {
                // Trepadeiras só nas peças de perto (as silhuetas têm um material só)
                ArquipelagoRuinKit.Vines = band.mesh == near ? vines : null;
                var kinds = new List<int>();
                for (int i = 0; i < band.rings; i++) kinds.Add(0);
                for (int i = 0; i < band.towers; i++) kinds.Add(1);
                for (int i = 0; i < band.arches; i++) kinds.Add(2);
                for (int i = 0; i < band.stairs; i++) kinds.Add(3);
                for (int i = 0; i < band.palaces; i++) kinds.Add(4);
                // Embaralha para os tipos se misturarem em volta
                for (int i = kinds.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
                }

                float step = 360f / kinds.Count;
                float offset = (float)random.NextDouble() * step;
                for (int k = 0; k < kinds.Count; k++)
                {
                    float footprint = (kinds[k] == 4 ? 60f : kinds[k] == 0 ? 45f : 25f) * band.scale;
                    Vector3 position = Vector3.zero;
                    float yaw = 0f;
                    bool found = false;
                    for (int attempt = 0; attempt < 30 && !found; attempt++)
                    {
                        float angle = offset + k * step + ((float)random.NextDouble() - 0.5f) * step * 0.8f;
                        float distance = Mathf.Lerp(band.min, band.max, (float)random.NextDouble());
                        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                        // Nada atrás do continente (ao sul): espelha para o lado do mar
                        if (direction.z < -0.15f)
                            direction.z = -direction.z;
                        position = middle + direction * distance;
                        // Virado mais ou menos para as ilhas, para mostrar a forma
                        yaw = angle + 180f + ((float)random.NextDouble() - 0.5f) * 70f;
                        Vector3 candidate = position;
                        found = !placed.Exists(p => Vector3.Distance(p.position, candidate) < (p.radius + footprint) * 0.6f);
                    }

                    if (!found)
                        continue;
                    placed.Add((position, footprint));
                    float scale = band.scale * (0.8f + (float)random.NextDouble() * 0.5f);
                    BlockMesh.Tile = k_Cell * Mathf.Max(1f, scale * 1.6f);
                    switch (kinds[k])
                    {
                        case 0: BrokenRing(band.mesh, position, yaw, scale, random); break;
                        case 1: ArquipelagoRuinKit.Tower(band.mesh, position, yaw, scale, random); break;
                        case 2: ArquipelagoRuinKit.Arch(band.mesh, position, yaw, scale, random); break;
                        case 3: SunkenStairs(band.mesh, position, yaw, scale); break;
                        default: BuildPalace(band.mesh, position, yaw, scale, random); break;
                    }
                }
            }

            BlockMesh.Tile = k_Cell;
            ArquipelagoRuinKit.Vines = vines;
        }

        // Anel quebrado em pé e semi-afundado (blocos, canaleta, aros, pontas em dente), às vezes com um anel interno
        // e um pedaço caído na água
        static void BrokenRing(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            float radius = (25f + (float)random.NextDouble() * 30f) * scale;
            Vector3 normal = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
            normal = Quaternion.AngleAxis((float)random.NextDouble() * 16f - 8f, Vector3.Cross(normal, Vector3.up)) * normal;
            Vector3 center = position - Vector3.up * radius * (0.15f + (float)random.NextDouble() * 0.3f);
            // Começa sempre abaixo da água (ângulos positivos sobem: acima de ~-5° a ponta já sairia da água)
            float start = -20f + (float)random.NextDouble() * 15f;
            float span = 150f + (float)random.NextDouble() * 110f;
            float thickness = 4f * scale + radius * 0.1f;
            // 0° na horizontal, no plano do anel: os ângulos positivos sempre sobem
            Vector3 zero = Vector3.Cross(Vector3.up, normal);
            ArquipelagoRuinKit.RichRing(mesh, center, normal, zero, radius, thickness, thickness * 0.9f, start,
                start + span, random);

            if (random.NextDouble() < 0.35)
                ArquipelagoRuinKit.RichRing(mesh, center, normal, zero, radius * 0.7f, thickness * 0.6f,
                    thickness * 0.6f, start, start + span - 30f, random, false, true, 0f, 4);

            if (random.NextDouble() < 0.6)
            {
                Vector3 side = Vector3.Cross(normal, Vector3.up).normalized;
                ArquipelagoRuinKit.RichRing(mesh, center + Vector3.down * radius * 0.6f + side * radius * 0.4f,
                    Quaternion.Euler(0f, 30f, 0f) * normal, Quaternion.Euler(0f, 30f, 0f) * zero, radius * 0.9f,
                    thickness, thickness * 0.9f, start + span + 25f, start + span + 70f, random);
            }
        }

        // Escadaria larga que desce e some na água, com balaústres nos lados e dois blocos laterais
        static void SunkenStairs(RuinMesh mesh, Vector3 position, float yaw, float scale)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right;
            float width = 36f * scale, step = 2.5f * scale, rise = 1.6f * scale;
            for (int i = 0; i < 12; i++)
            {
                float y = 14f * scale - i * rise;
                BlockMesh.ChamferBox(mesh.Pick(), position + forward * (i * step) + Vector3.up * (y - 10f * scale),
                    new Vector3(width, 20f * scale, step), rotation, 0.3f * scale);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                ArquipelagoRuinKit.Pillar(mesh.Pick(), position + right * side * (width * 0.5f + 3f * scale) +
                                                         Vector3.up * 8f * scale,
                    new Vector2(6f * scale, 10f * scale), 26f * scale, rotation, 0.6f * scale);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), position + right * side * (width * 0.5f + 3f * scale) +
                                                         Vector3.up * 21.5f * scale,
                    new Vector2(7.2f * scale, 11.2f * scale), 1.4f * scale, rotation, 0.4f * scale);
            }
        }

        // Palácio afundado: fachada de pilares com base e capitel, vergas sobre os vãos, cornija no alto e pilastras;
        // na frente, uma colunata partida; atrás, um telhado em degraus caído. Tudo inclinado (afundando).
        static void BuildPalace(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(-6f, yaw, 4f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            Vector3 origin = position - up * 4f * scale;
            float pier = 5f * scale, opening = 7f * scale, height = 30f * scale, openingHeight = 16f * scale,
                depth = 6f * scale;
            int openings = 4;
            float total = openings * opening + (openings + 1) * pier;
            float cursor = -total * 0.5f;
            for (int i = 0; i <= openings; i++)
            {
                float pierHeight = i == openings ? height * 0.6f : height; // a ponta direita desabou
                Vector3 pierFoot = origin + right * (cursor + pier * 0.5f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (pierHeight * 0.5f), new Vector2(pier, depth),
                    pierHeight, rotation, pier * 0.08f);
                // Base e capitel salientes, e uma pilastra na frente do pilar
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (6f * scale), new Vector2(pier * 1.25f, depth * 1.2f),
                    1.6f * scale, rotation, pier * 0.1f);
                if (i < openings)
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (openingHeight - 0.8f * scale),
                        new Vector2(pier * 1.25f, depth * 1.2f), 1.6f * scale, rotation, pier * 0.1f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (pierHeight * 0.5f) + forward * (depth * 0.55f),
                    new Vector2(pier * 0.4f, depth * 0.15f), pierHeight * 0.9f, rotation, pier * 0.05f);
                cursor += pier;
                if (i == openings)
                    break;
                if (i < openings - 1)
                {
                    // Parede acima do vão, com uma verga saliente
                    BlockMesh.ChamferBox(mesh.Pick(), origin + right * (cursor + opening * 0.5f) +
                                                      up * ((openingHeight + height) * 0.5f),
                        new Vector3(opening, height - openingHeight, depth), rotation, pier * 0.05f);
                    BlockMesh.ChamferBox(mesh.Pick(), origin + right * (cursor + opening * 0.5f) +
                                                      up * (openingHeight + 0.8f * scale) + forward * (depth * 0.1f),
                        new Vector3(opening + pier * 0.6f, 1.6f * scale, depth * 1.2f), rotation, pier * 0.08f);
                }

                cursor += opening;
            }

            // Cornija no alto (só até onde a fachada ficou de pé)
            BlockMesh.ChamferBox(mesh.Pick(), origin + right * (-pier * 0.5f) + up * (height + 0.8f * scale),
                new Vector3(total - pier * 2f, 1.6f * scale, depth * 1.3f), rotation, pier * 0.1f);
            ArquipelagoRuinKit.HangVine(origin + right * (-total * 0.2f) + up * height + forward * (depth * 0.7f),
                forward, random, scale * 3f);
            ArquipelagoRuinKit.HangVine(origin + right * (total * 0.15f) + up * height + forward * (depth * 0.7f),
                forward, random, scale * 3f);

            for (int i = 0; i < 6; i++)
            {
                float x = -total * 0.45f + i * total * 0.18f;
                float h = (8f + (i * 7 % 5) * 3f) * scale;
                ArquipelagoRuinKit.Column(mesh, origin + right * x + forward * 14f * scale - up * 2f * scale, h,
                    2f * scale, random, true);
            }

            Vector3 roof = origin + right * (total * 0.6f) + forward * 10f * scale;
            Quaternion roofRotation = rotation * Quaternion.Euler(0f, 0f, 35f);
            for (int step = 0; step < 4; step++)
            {
                float w = (18f - step * 4f) * scale;
                BlockMesh.ChamferBox(mesh.Pick(), roof + roofRotation * Vector3.up * (step * 2.5f * scale),
                    new Vector3(w, 2.5f * scale, w * 0.8f), roofRotation, 0.4f * scale);
            }
        }

        // ---------- decoração ----------

        // Tufos de capim e flores no topo das ilhas (sprites que olham para o jogador), longe das ruínas
        static void PlaceDecor(Transform parent, List<Vector3> cover)
        {
            Mesh quad = GridSceneKit.SpriteQuad();
            var random = new System.Random(55);
            var decor = new GameObject("Decoracao").transform;
            decor.SetParent(parent, false);
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (!IsLand(x, z) || s_Ramp[x, z])
                    continue;
                double roll = random.NextDouble();
                if (roll > 0.16)
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z] +
                                   new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 1.4f;
                if (cover.Exists(c => Vector3.Distance(c, position) < 3f))
                    continue;
                bool flower = roll < 0.04;
                float scale = 0.7f + (float)random.NextDouble() * 0.5f;
                GridSceneKit.Billboard(flower ? "Flor" : "Tufo", quad, flower ? s_Materials.Flower : s_Materials.Tuft,
                    (flower ? s_Materials.FlowerSize : s_Materials.TuftSize) * scale, position, decor,
                    random.NextDouble() < 0.5);
            }

            // Pontinhos de luz sobre as ilhas e o mar
            Vector3 center = CellToWorld(new Vector2(k_Size * 0.5f, k_Size * 0.5f), 6f);
            var size = new Vector3(k_Size * k_Cell, 12f, k_Size * k_Cell);
            GridSceneKit.AddSpores("Esporos_Arquipelago", s_Materials.Spores, center, size, 260, 26f,
                new Vector2(0.08f, 0.2f), Theme().SporeColors, parent);
        }

        // ---------- combate ----------

        // Sala de uma arena (as ilhas com Arena = 'arena'): gatilho em caixa cobrindo as ilhas, começando 4 m depois do
        // portão (quem para na ponte ou no cais não fica trancado do lado de fora; sem cápsula, as IAs usam 50 m em
        // volta do centro como limite da arena), portão de entrada e, se houver, o de saída (fechado até a sala ser
        // limpa) e o NavMesh da arena
        static RoomManager BuildRoom(Transform parent, string objectName, string roomName, int arena, int number,
            Vector3 doorPoint, Vector3 doorForward, Vector3? exitPoint, Vector3 exitForward, out NavMeshSurface surface)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            float ground = float.MaxValue;
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] == 0 || k_Islands[s_Island[x, z] - 1].Arena != arena)
                    continue;
                min = Vector2.Min(min, new Vector2(x, z) * k_Cell);
                max = Vector2.Max(max, new Vector2(x + 1, z + 1) * k_Cell);
                if (IsLand(x, z))
                    ground = Mathf.Min(ground, s_Height[x, z]);
            }

            var center = new Vector3((min.x + max.x) * 0.5f, ground, (min.y + max.y) * 0.5f);
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            // Corta a caixa no lado do portão
            Vector2 tmin = min, tmax = max;
            if (Mathf.Abs(doorForward.z) >= Mathf.Abs(doorForward.x))
            {
                if (doorForward.z > 0f)
                    tmin.y = Mathf.Max(tmin.y, doorPoint.z + 4f);
                else
                    tmax.y = Mathf.Min(tmax.y, doorPoint.z - 4f);
            }
            else if (doorForward.x > 0f)
                tmin.x = Mathf.Max(tmin.x, doorPoint.x + 4f);
            else
                tmax.x = Mathf.Min(tmax.x, doorPoint.x - 4f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(tmax.x - tmin.x, 40f, tmax.y - tmin.y);
            trigger.center = new Vector3((tmin.x + tmax.x) * 0.5f - center.x, 12f, (tmin.y + tmax.y) * 0.5f - center.z);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = roomName;
            RoomDoor door = GridSceneKit.CreateDoor("Porta_Entrada", V(0, 0), s_Materials.Door, root.transform);
            door.transform.SetPositionAndRotation(doorPoint, Quaternion.LookRotation(doorForward));
            var doors = new List<RoomDoor> { door };
            if (exitPoint.HasValue)
            {
                RoomDoor exit = GridSceneKit.CreateDoor("Porta_Saida", V(0, 0), s_Materials.Door, root.transform);
                exit.transform.SetPositionAndRotation(exitPoint.Value, Quaternion.LookRotation(exitForward));
                exit.StartOpen = false;
                doors.Add(exit);
            }

            room.Doors = doors.ToArray();
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = 1;
            room.ArenaNumber = number;
            room.ArenaInBiome = arena - 1;
            room.ArenasInBiome = 4;
            room.IsBoss = false;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.up * 10f;
            surface.size = new Vector3(max.x - min.x + 20f, 50f, max.y - min.y + 20f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        // Pontos nas ilhas da arena, longe do portão, com ~2 m livres em volta
        static int PlaceSpawns(RoomManager room, Vector3 door, int arena)
        {
            var random = new System.Random(77 + arena);
            var points = new List<Vector3>();
            for (int attempt = 0; attempt < 4000 && points.Count < 10; attempt++)
            {
                int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                int island = s_Island[x, z];
                if (island == 0 || k_Islands[island - 1].Arena != arena || !IsLand(x, z) || s_Ramp[x, z] ||
                    !Interior(x, z, 2))
                    continue;
                Vector3 candidate = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z] + s_Offset;
                if (Vector3.Distance(candidate, door) < 26f)
                    continue;
                if (!NavMesh.SamplePosition(candidate + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    continue;
                if (points.Exists(p => Vector3.Distance(p, hit.position) < 8f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                points.Add(hit.position);
            }

            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(room.transform, false);
            var transforms = new List<Transform>();
            foreach (Vector3 position in points)
            {
                var point = new GameObject($"Spawn_{transforms.Count + 1:00}").transform;
                point.SetParent(spawns, false);
                point.position = position;
                transforms.Add(point);
            }

            room.SpawnPoints = transforms.ToArray();
            return points.Count;
        }

        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            // Na praça do alto do penhasco, de frente para o mar
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(k_Arrival + new Vector3(0f, 0.2f, 4f),
                Quaternion.LookRotation(Vector3.forward));
        }
    }
}
