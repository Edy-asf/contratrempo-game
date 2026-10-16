using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // "Ícaro > Arquipélago > Gerar sala de teste": protótipo do bioma Arquipélago (substitui o Charco) numa cena
    // própria (Scenes/Arquipelago_Teste.unity). Noite verde-azulada com névoa densa e aurora boreal.
    // - Ilhas em degraus numa grade de 2 m: topo de capim, penhascos de pedra até o mar, terraço mais alto com rampas.
    //   Ilha de chegada e duas ilhas de combate.
    // - Ligações diferentes: ponte de pedra da chegada (com o portão da sala), ponte quebrada entre as ilhas de combate
    //   (pular ou dash) e uma coluna gigante tombada por onde dá para andar.
    // - Cobertura de ruínas facetadas nas ilhas: muros quebrados, colunas partidas, pilhas de blocos e tambores de
    //   coluna caídos.
    // - No mar: anéis gigantes quebrados e semi-afundados e um palácio afundado (silhuetas na névoa), ilhotas de pedra.
    // - Mar: quem cai volta ao último ponto seguro perdendo vida (SeaHazard); inimigos que caem morrem.
    // Peças sem curvas nem esferas (BlockMesh). Rodar de novo refaz a cena.
    public static class ArquipelagoBuilder
    {
        const string k_ScenePath = IcaroAssetUtility.ScenesFolder + "/Arquipelago_Teste.unity";
        const string k_NavMeshFolder = IcaroAssetUtility.ScenesFolder + "/Arquipelago_Teste_NavMesh";
        const string k_Prefix = "Arquipelago_Teste";

        const float k_Cell = GridSceneKit.Cell;
        const int k_Size = 140;            // células de cada lado do mapa de ilhas
        const float k_SeaBed = -4f;        // fundo dos penhascos (abaixo da água)

        class Island
        {
            public Vector2 Center;         // células
            public float RadiusX, RadiusZ;
            public float Height;           // metros acima do mar
            public Vector2 PlateauCenter;
            public float PlateauRadiusX, PlateauRadiusZ;
            public float PlateauHeight;
            public bool Combat;
            public int Seed;
        }

        static readonly Island[] k_Islands =
        {
            // Chegada
            new Island { Center = new Vector2(70, 16), RadiusX = 7, RadiusZ = 6, Height = 2f, Seed = 1 },
            // Combate A (terraço a oeste) e B (terraço a leste)
            new Island { Center = new Vector2(54, 56), RadiusX = 17, RadiusZ = 13, Height = 3f, Combat = true,
                PlateauCenter = new Vector2(47, 61), PlateauRadiusX = 7, PlateauRadiusZ = 5, PlateauHeight = 4f, Seed = 2 },
            new Island { Center = new Vector2(97, 74), RadiusX = 15, RadiusZ = 16, Height = 3f, Combat = true,
                PlateauCenter = new Vector2(102, 79), PlateauRadiusX = 7, PlateauRadiusZ = 7, PlateauHeight = 4f, Seed = 3 },
            // Ilhotas de pedra (cenário)
            new Island { Center = new Vector2(22, 92), RadiusX = 4, RadiusZ = 4, Height = 9f, Seed = 4 },
            new Island { Center = new Vector2(124, 36), RadiusX = 3, RadiusZ = 3, Height = 12f, Seed = 5 },
            new Island { Center = new Vector2(118, 118), RadiusX = 6, RadiusZ = 5, Height = 5f, Seed = 6 },
        };

        static float[,] s_Height;      // metros; k_SeaBed = mar
        static int[,] s_Island;        // índice + 1
        static bool[,] s_Ramp;
        static Vector2Int[,] s_RampDir;
        static ArquipelagoTextureKit.Set s_Materials;

        static Vector2Int V(int x, int z) => new Vector2Int(x, z);

        [MenuItem("Ícaro/Arquipélago/Gerar sala de teste", priority = 47)]
        static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Arquipélago (teste)", "Texturas", 0.1f);
                s_Materials = ArquipelagoTextureKit.Build();
                GridTheme theme = Theme();
                Material sky = GridSceneKit.NightSkyMaterial(theme);

                BuildHeights();

                EditorUtility.DisplayProgressBar("Arquipélago (teste)", "Cena", 0.3f);
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = ViaAeonidaBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);

                var world = new GameObject("Mapa").transform;
                BuildTerrain(world);
                BuildSea(world);

                var ruins = new MeshData();
                var giants = new MeshData();
                var silhouettes = new MeshData();
                (Vector3 doorPoint, Vector3 doorForward) = BuildConnections(ruins);
                List<Vector3> cover = BuildCover(ruins);
                BuildGiants(giants, silhouettes);
                GridSceneKit.CreateObject(k_Prefix + "_Ruinas", "Ruinas", ruins, s_Materials.Stone, world, true);
                GridSceneKit.CreateObject(k_Prefix + "_Gigantes", "Gigantes", giants, s_Materials.StoneDark, world, false);
                GridSceneKit.CreateObject(k_Prefix + "_Gigantes_Fundo", "Gigantes_Fundo", silhouettes,
                    s_Materials.Silhouette, world, false, false);
                world.gameObject.AddComponent<CameraFarClip>();

                PlaceDecor(world, cover);

                var hazard = new GameObject("Mar_Queda").AddComponent<SeaHazard>();
                hazard.transform.SetParent(world, false);

                RoomManager room = BuildRoom(world, doorPoint, doorForward, out NavMeshSurface surface);
                PlacePlayer(scene);
                GridSceneKit.AddBiomeZone(theme, CellToWorld(k_Islands[0].Center, k_Islands[0].Height),
                    new Vector3(30f, 12f, 30f), world);

                EditorSceneManager.SaveScene(scene, k_ScenePath);
                EditorUtility.DisplayProgressBar("Arquipélago (teste)", "NavMesh e spawns", 0.7f);
                Physics.SyncTransforms();
                GridSceneKit.BakeNavMesh(surface, k_NavMeshFolder, "Arena_1");
                int spawns = PlaceSpawns(room, doorPoint);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Arquipélago (teste) gerado em {k_ScenePath}: {cover.Count} ruínas de cobertura, " +
                          $"{spawns} pontos de spawn. Dê Play nessa cena.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Noite verde-azulada, névoa densa (as estruturas viram silhuetas), aurora boreal, sem planeta
        static GridTheme Theme() => new GridTheme
        {
            Biome = "Arquipelago",
            BiomeIndex = 1,
            SceneName = "Arquipelago_Teste",
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
                    s_Height[x, z] = island.PlateauHeight > 0f &&
                                     InEllipse(cell, island.PlateauCenter, island.PlateauRadiusX, island.PlateauRadiusZ,
                                         island.Seed + 10)
                        ? island.PlateauHeight
                        : island.Height;
                }
            }

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
            GameObject go = GridSceneKit.CreateObject(k_Prefix + "_Mar", "Mar", sea, s_Materials.Sea, parent, false, false);
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

        // Ponte de chegada (inteira, com o portão do lado da arena), ponte quebrada e coluna tombada entre as ilhas
        static (Vector3 door, Vector3 forward) BuildConnections(MeshData mesh)
        {
            Island start = k_Islands[0], a = k_Islands[1], b = k_Islands[2];
            s_ConnectionPoints.Clear();

            (Vector3 s0, Vector3 s1) = EdgePoints(start.Center, a.Center + new Vector2(6f, 0f));
            Bridge(mesh, s0, s1, 0f, 11);

            (Vector3 b0, Vector3 b1) = EdgePoints(a.Center + new Vector2(4f, -2f), b.Center + new Vector2(-2f, -6f));
            Bridge(mesh, b0, b1, 3.2f, 23);

            (Vector3 c0, Vector3 c1) = EdgePoints(a.Center + new Vector2(2f, 9f), b.Center + new Vector2(-6f, 6f));
            FallenColumn(mesh, c0, c1);
            s_ConnectionPoints.AddRange(new[] { s1, b0, b1, c0, c1 });

            Vector3 forward = s1 - s0;
            forward.y = 0f;
            return (s1 + forward.normalized * 1.5f, forward.normalized);
        }

        // Ponte de pedra de 4 m: tábuas de pedra em pedaços levemente tortos, parapeitos baixos com falhas e pilares
        // até o mar. Com 'gap' > 0, falta um trecho no meio (pular ou dash).
        static void Bridge(MeshData mesh, Vector3 from, Vector3 to, float gap, int seed)
        {
            var random = new System.Random(seed);
            Vector3 dir = to - from;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            from -= flat.normalized * 1.5f;  // entra um pouco nas ilhas
            to += flat.normalized * 1.5f;
            dir = to - from;
            float length = dir.magnitude;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float segment = 3f;
            int count = Mathf.CeilToInt(length / segment);
            float gapStart = (length - gap) * 0.5f, gapEnd = (length + gap) * 0.5f;
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

                float mid = (s0 + s1) * 0.5f;
                Vector3 center = from + dir.normalized * mid - Vector3.up * 0.3f;
                Quaternion tilt = rotation * Quaternion.Euler((float)random.NextDouble() * 3f - 1.5f, 0f,
                    (float)random.NextDouble() * 3f - 1.5f);
                BlockMesh.Box(mesh, center, new Vector3(4f, 0.6f, s1 - s0 - 0.08f), tilt);

                // Parapeitos com falhas
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() < 0.3)
                        continue;
                    float height = 0.6f + (float)random.NextDouble() * 0.4f;
                    BlockMesh.Box(mesh, center + right * side * 1.75f + Vector3.up * (0.3f + height * 0.5f),
                        new Vector3(0.5f, height, s1 - s0 - 0.3f), tilt);
                }

                // Pilar até o mar a cada ~9 m
                if (i % 3 == 1)
                    BlockMesh.Prism(mesh, center - Vector3.up * 0.3f, Vector3.down, center.y + 6f, 0.9f, 8, 22.5f);
            }
        }

        // Coluna gigante tombada entre as ilhas: prisma de 8 lados deitado com uma face plana em cima (dá para andar),
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

        // ---------- ruínas de cobertura ----------

        // Muros quebrados, colunas partidas, pilhas de blocos e tambores caídos nas ilhas de combate. Devolve as
        // posições (para a decoração e os spawns evitarem).
        static List<Vector3> BuildCover(MeshData mesh)
        {
            var random = new System.Random(1234);
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < 600 && placed.Count < 14; attempt++)
            {
                int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                int island = s_Island[x, z];
                if (island == 0 || !k_Islands[island - 1].Combat || s_Ramp[x, z] || !Interior(x, z, 3))
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z];
                if (placed.Exists(p => Vector3.Distance(p, position) < 8f) || NearConnection(position))
                    continue;

                Quaternion yaw = Quaternion.Euler(0f, random.Next(0, 4) * 90f + (float)random.NextDouble() * 20f - 10f, 0f);
                switch (random.Next(4))
                {
                    case 0: // muro quebrado com degrau no topo
                        float width = 5f + (float)random.NextDouble() * 3f, height = 2.5f + (float)random.NextDouble() * 1.5f;
                        BlockMesh.Box(mesh, position + Vector3.up * (height * 0.5f), new Vector3(width, height, 1.2f), yaw);
                        BlockMesh.Box(mesh, position + yaw * Vector3.right * (width * 0.25f) + Vector3.up * (height + 0.6f),
                            new Vector3(width * 0.5f, 1.2f, 1.2f), yaw);
                        break;
                    case 1: // coluna partida
                        BlockMesh.Prism(mesh, position, Vector3.up, 2f + (float)random.NextDouble() * 3f, 1.1f, 8, 22.5f);
                        BlockMesh.Box(mesh, position + Vector3.up * 0.3f, new Vector3(2.8f, 0.6f, 2.8f), yaw);
                        break;
                    case 2: // pilha de blocos
                        BlockMesh.Box(mesh, position + Vector3.up * 0.9f, new Vector3(2.4f, 1.8f, 2f), yaw);
                        BlockMesh.Box(mesh, position + yaw * new Vector3(0.5f, 2.4f, 0.2f), new Vector3(1.6f, 1.2f, 1.6f),
                            yaw * Quaternion.Euler(0f, 25f, 0f));
                        break;
                    default: // tambor de coluna caído
                        Vector3 axis = yaw * Vector3.right;
                        BlockMesh.Prism(mesh, position - axis * 1.6f + Vector3.up * 0.9f, axis, 3.2f, 1f, 8, 22.5f);
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
        // - perto (110 a 210 m): pedra com luz normal, onde dá para ver detalhe e as runas;
        // - meio (210 a 480 m) e fundo (480 a 950 m): material de silhueta (vultos na névoa que nunca somem de vez),
        //   cada vez maiores para continuarem legíveis.
        // Anéis quebrados, torres partidas, arcos, escadarias que descem para a água e palácios afundados.
        static void BuildGiants(MeshData near, MeshData far)
        {
            var random = new System.Random(4321);
            Vector3 middle = CellToWorld(new Vector2(k_Size * 0.5f, k_Size * 0.55f), 0f);
            var placed = new List<(Vector3 position, float radius)>();

            // (malha, distância mínima, máxima, escala, quantidade de cada peça: anéis, torres, arcos, escadas, palácios)
            var bands = new (MeshData mesh, float min, float max, float scale, int rings, int towers, int arches, int stairs, int palaces)[]
            {
                (near, 110f, 210f, 0.8f, 4, 3, 2, 2, 1),
                (far, 210f, 480f, 1.3f, 7, 6, 4, 2, 2),
                (far, 480f, 950f, 2.4f, 6, 5, 3, 0, 2),
            };

            foreach (var band in bands)
            {
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
                    switch (kinds[k])
                    {
                        case 0: BrokenRing(band.mesh, position, yaw, scale, random); break;
                        case 1: BrokenTower(band.mesh, position, yaw, scale, random); break;
                        case 2: Arch(band.mesh, position, yaw, scale, random); break;
                        case 3: SunkenStairs(band.mesh, position, yaw, scale); break;
                        default: BuildPalace(band.mesh, position, yaw, scale); break;
                    }
                }
            }
        }

        // Anel quebrado em pé e semi-afundado, às vezes com um anel interno e um pedaço caído na água
        static void BrokenRing(MeshData mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            float radius = (25f + (float)random.NextDouble() * 30f) * scale;
            Vector3 normal = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
            normal = Quaternion.AngleAxis((float)random.NextDouble() * 16f - 8f, Vector3.Cross(normal, Vector3.up)) * normal;
            Vector3 center = position - Vector3.up * radius * (0.15f + (float)random.NextDouble() * 0.3f);
            float start = -20f + (float)random.NextDouble() * 30f;
            float span = 150f + (float)random.NextDouble() * 110f;
            float thickness = 4f * scale + radius * 0.1f;
            BlockMesh.RingArc(mesh, center, normal, Vector3.right, radius, thickness, thickness * 0.9f, start, start + span,
                Mathf.RoundToInt(span / 12f));

            if (random.NextDouble() < 0.35)
                BlockMesh.RingArc(mesh, center, normal, Vector3.right, radius * 0.7f, thickness * 0.6f, thickness * 0.6f,
                    start + 40f, start + span - 30f, Mathf.RoundToInt(span / 14f));

            if (random.NextDouble() < 0.6)
            {
                Vector3 side = Vector3.Cross(normal, Vector3.up).normalized;
                BlockMesh.RingArc(mesh, center + Vector3.down * radius * 0.6f + side * radius * 0.4f,
                    Quaternion.Euler(0f, 30f, 0f) * normal, Vector3.right, radius * 0.9f, thickness, thickness * 0.9f,
                    start + span + 25f, start + span + 70f, 4);
            }
        }

        // Torre partida: andares que afinam (caixa e octógono alternados, com cornija), cada um levemente torto, topo
        // quebrado e um bloco caído na água
        static void BrokenTower(MeshData mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 8f - 4f, yaw, (float)random.NextDouble() * 8f - 4f);
            Vector3 up = rotation * Vector3.up, right = rotation * Vector3.right;
            float width = 14f * scale;
            float y = -6f * scale;
            int levels = 4 + random.Next(4);
            for (int i = 0; i < levels; i++)
            {
                float height = (8f + (float)random.NextDouble() * 6f) * scale;
                Quaternion twist = rotation * Quaternion.Euler(0f, (float)random.NextDouble() * 6f - 3f, 0f);
                if (i % 2 == 1)
                    BlockMesh.Prism(mesh, position + up * y, up, height, width * 0.62f, 8, 22.5f + yaw);
                else
                    BlockMesh.Box(mesh, position + up * (y + height * 0.5f), new Vector3(width, height, width), twist);
                BlockMesh.Box(mesh, position + up * (y + height), new Vector3(width * 1.15f, 1.2f * scale, width * 1.15f), twist);
                y += height + 1.2f * scale;
                width *= 0.86f;
            }

            Vector3 top = position + up * y;
            BlockMesh.Box(mesh, top + up * 4f * scale + right * width * 0.3f, new Vector3(width * 0.35f, 8f * scale, width), rotation);
            BlockMesh.Box(mesh, top + up * 2f * scale - right * width * 0.35f, new Vector3(width * 0.25f, 4f * scale, width * 0.7f), rotation);
            BlockMesh.Box(mesh, position + right * width * 1.4f + Vector3.up * scale, new Vector3(width * 0.8f, width * 0.5f, width * 0.6f),
                Quaternion.Euler(20f, yaw + 35f, 15f));
        }

        // Arco monumental: dois pilares e o arco por cima (segmentos retos), às vezes partido de um lado
        static void Arch(MeshData mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, (float)random.NextDouble() * 6f - 3f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            float span = 30f * scale, pillar = 7f * scale, height = 30f * scale;
            bool broken = random.NextDouble() < 0.5;
            for (int side = -1; side <= 1; side += 2)
            {
                float h = broken && side == -1 ? height * 0.55f : height;
                BlockMesh.Box(mesh, position + right * side * (span * 0.5f + pillar * 0.5f) + up * (h * 0.5f - 4f * scale),
                    new Vector3(pillar, h, pillar * 1.2f), rotation);
            }

            float radius = span * 0.5f + pillar * 0.5f;
            BlockMesh.RingArc(mesh, position + up * (height - 4f * scale), forward, right, radius, pillar, pillar * 1.2f,
                0f, broken ? 85f : 180f, broken ? 5 : 10);
        }

        // Escadaria larga que desce e some na água, com dois blocos laterais
        static void SunkenStairs(MeshData mesh, Vector3 position, float yaw, float scale)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right;
            float width = 36f * scale, step = 2.5f * scale, rise = 1.6f * scale;
            for (int i = 0; i < 12; i++)
            {
                float y = 14f * scale - i * rise;
                BlockMesh.Box(mesh, position + forward * (i * step) + Vector3.up * (y - 10f * scale),
                    new Vector3(width, 20f * scale, step), rotation);
            }

            for (int side = -1; side <= 1; side += 2)
                BlockMesh.Box(mesh, position + right * side * (width * 0.5f + 3f * scale) + Vector3.up * 8f * scale,
                    new Vector3(6f * scale, 26f * scale, 10f * scale), rotation);
        }

        // Palácio afundado: fachada com vãos, fileira de colunas partidas e um telhado em degraus caído, inclinados
        static void BuildPalace(MeshData mesh, Vector3 position, float yaw, float scale)
        {
            Quaternion rotation = Quaternion.Euler(-6f, yaw, 4f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            Vector3 origin = position - up * 4f * scale;
            float pier = 5f * scale, opening = 7f * scale, height = 30f * scale, openingHeight = 16f * scale, depth = 6f * scale;
            int openings = 4;
            float total = openings * opening + (openings + 1) * pier;
            float cursor = -total * 0.5f;
            for (int i = 0; i <= openings; i++)
            {
                float pierHeight = i == openings ? height * 0.6f : height; // a ponta direita desabou
                BlockMesh.Box(mesh, origin + right * (cursor + pier * 0.5f) + up * (pierHeight * 0.5f),
                    new Vector3(pier, pierHeight, depth), rotation);
                cursor += pier;
                if (i == openings)
                    break;
                if (i < openings - 1)
                    BlockMesh.Box(mesh, origin + right * (cursor + opening * 0.5f) + up * ((openingHeight + height) * 0.5f),
                        new Vector3(opening, height - openingHeight, depth), rotation);
                cursor += opening;
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -total * 0.45f + i * total * 0.18f;
                float h = (8f + (i * 7 % 5) * 3f) * scale;
                BlockMesh.Prism(mesh, origin + right * x + forward * 14f * scale - up * 2f * scale, up, h, 2f * scale, 8, 22.5f);
            }

            Vector3 roof = origin + right * (total * 0.6f) + forward * 10f * scale;
            Quaternion roofRotation = rotation * Quaternion.Euler(0f, 0f, 35f);
            for (int step = 0; step < 4; step++)
            {
                float w = (18f - step * 4f) * scale;
                BlockMesh.Box(mesh, roof + roofRotation * Vector3.up * (step * 2.5f * scale), new Vector3(w, 2.5f * scale, w * 0.8f),
                    roofRotation);
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

        static RoomManager BuildRoom(Transform parent, Vector3 doorPoint, Vector3 doorForward, out NavMeshSurface surface)
        {
            Island a = k_Islands[1], b = k_Islands[2];
            Vector3 center = (CellToWorld(a.Center, a.Height) + CellToWorld(b.Center, b.Height)) * 0.5f;
            var root = new GameObject("Arena_Ilhas");
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            // Gatilho em caixa: cobre as duas ilhas de combate e começa 4 m depois do portão (quem para na ponte não
            // fica trancado do lado de fora). Sem cápsula, as IAs usam 50 m em volta do centro como limite da arena.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] == 0 || !k_Islands[s_Island[x, z] - 1].Combat)
                    continue;
                min = Vector2.Min(min, new Vector2(x, z) * k_Cell);
                max = Vector2.Max(max, new Vector2(x + 1, z + 1) * k_Cell);
            }

            min.y = Mathf.Max(min.y, doorPoint.z + 4f);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(max.x - min.x, 30f, max.y - min.y);
            trigger.center = new Vector3((min.x + max.x) * 0.5f - center.x, 8f, (min.y + max.y) * 0.5f - center.z);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = "Arquipélago (teste)";
            RoomDoor door = GridSceneKit.CreateDoor("Porta_Entrada", V(0, 0), s_Materials.Door, root.transform);
            door.transform.SetPositionAndRotation(doorPoint, Quaternion.LookRotation(doorForward));
            room.Doors = new[] { door };
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = 1;
            room.ArenaNumber = 7;
            room.ArenaInBiome = 0;
            room.ArenasInBiome = 5;
            room.IsBoss = false;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.up * 2f;
            surface.size = new Vector3(180f, 30f, 130f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        // Pontos nas ilhas de combate, longe do portão, com ~2 m livres em volta
        static int PlaceSpawns(RoomManager room, Vector3 door)
        {
            var random = new System.Random(77);
            var points = new List<Vector3>();
            for (int attempt = 0; attempt < 3000 && points.Count < 10; attempt++)
            {
                int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                int island = s_Island[x, z];
                if (island == 0 || !k_Islands[island - 1].Combat || s_Ramp[x, z] || !Interior(x, z, 2))
                    continue;
                Vector3 candidate = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z];
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

            Island start = k_Islands[0];
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(CellToWorld(start.Center - new Vector2(0f, 2f), start.Height + 0.2f),
                Quaternion.LookRotation(Vector3.forward));
        }
    }
}
