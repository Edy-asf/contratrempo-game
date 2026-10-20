using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // Gera um bioma no mapa quadriculado a partir de um tema (GridTheme), numa cena própria, sem mexer na Via. Grade
    // de 2 m. Menus: "Ícaro > Grade > Gerar Bosque" e "Ícaro > Grade > Gerar Charco (teste)" (GridThemes).
    // - Clareira de chegada e as arenas do tema em sequência, ligadas por corredores na floresta (alguns com curva).
    //   Cada arena tem RoomManager (salas do bioma), portas de energia, NavMesh próprio, pontos de spawn escondidos
    //   da entrada e poleiros do Espinhento; a do chefe é ampla, plana e sem cobertura.
    // - Água (se a arena pedir): rio raso atravessando a arena, com poços fundos longe da trilha, e lagoas fundas com
    //   margem rasa. A água rasa fica 0,25 m abaixo do chão (dá para sentir a descida) e deixa mais lento (WaterMap);
    //   a funda não deixa passar (borda invisível).
    // - A borda do espaço andável é uma parede invisível de 20 m; o chão continua 100 m além dela e 40 fileiras de
    //   árvores 2D (espécies do tema misturadas, o destaque em manchas raras com cogumelos em volta; tons e lados
    //   variados; maiores quanto mais longe) escondem o resto: cor e brilho perto, escuras no meio, silhueta no fundo.
    //   As de perto e as da arena olham para o jogador (SpriteBillboard); as de longe são cartas fixas viradas para o
    //   espaço andável, fundidas numa malha por material (desempenho).
    // - Noite de Kairós (céu estrelado que gira com o multiplicador), esporos neon, plantas baixas sem brilho.
    // - Teleporte de depuração: F6/F7 próxima/anterior arena, F9 menu (ViaDebugTeleport).
    // Rodar de novo refaz a cena.
    public static class GridBiomeBuilder
    {
        const float k_Cell = GridSceneKit.Cell;
        const float k_BlockHeight = GridSceneKit.BlockHeight;
        const float k_BarrierHeight = 20f;   // parede invisível da borda
        const float k_WaterSurface = -0.08f; // altura da superfície da água
        const int k_ForestDepth = 40;        // fileiras de árvores em volta do espaço andável
        const int k_FloorDepth = 50;         // quanto o chão continua além da borda (100 m)
        const int k_BillboardDepth = 4;      // árvores até esta fileira olham para o jogador; depois, cartas fixas
        const int k_Margin = 52;             // margem do mapa em volta do espaço andável
        const int k_HalfWidth = 46;          // meia largura do miolo do mapa (maior arena + desvio dos corredores)

        enum Cell : byte
        {
            Void,
            Floor,
            Wall,    // borda invisível
            Rock,    // cobertura
            Perch,   // poleiro (toco gigante)
            Tree,    // árvore em sprite: chão por baixo, colisão só no tronco
            Shallow, // água rasa: andável, mais funda e mais lenta
            Deep,    // água funda: não passa
        }

        // Tema e mapa da geração atual
        static GridTheme s_Theme;
        static int s_Width, s_Depth;
        static Cell[,] s_Map;
        static int[,] s_Owner;          // 0 = fora; i + 1 = arena i
        static bool[,] s_Path;
        static int[,] s_Distance;       // distância (células) até o espaço andável mais próximo
        static Vector2Int[,] s_Nearest; // célula andável mais próxima (para virar as cartas)
        static GridArena[] s_Arenas;
        static int s_CenterX;
        static RectInt s_Clearing;

        static Vector2Int V(int x, int z) => new Vector2Int(x, z);

        // Com GridTheme.OpenEnd: ponto (no mundo, no chão) onde a trilha que sai da arena do chefe chega à borda norte
        // do mapa, para o próximo trecho da Via continuar dali
        public static Vector3 OpenEndPoint { get; private set; }

        [MenuItem("Ícaro/Grade/Gerar Bosque", priority = 45)]
        static void GenerateBosque() => Generate(GridThemes.Bosque());

        [MenuItem("Ícaro/Grade/Gerar Charco (teste)", priority = 46)]
        static void GenerateCharcoTest() => Generate(GridThemes.CharcoTeste());

        // 'extend' monta mais coisas na mesma cena antes de salvar (ex.: o desfiladeiro e o bioma seguinte)
        public static void Generate(GridTheme theme, System.Action<Scene, Transform> extend = null)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string title = $"{theme.Biome} (grade)";
            try
            {
                s_Theme = theme;
                GridTextureKit.Set materials = theme.Materials;
                Material sky = GridSceneKit.NightSkyMaterial(theme);

                EditorUtility.DisplayProgressBar(title, "Mapa", 0.15f);
                BuildMap();

                EditorUtility.DisplayProgressBar(title, "Cena", 0.25f);
                BiomePalettes.Current = BiomePalettes.Bosque;
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = ViaAeonidaBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);

                var world = new GameObject("Mapa").transform;
                // Sprites que olham para o jogador: juntados em malhas por bloco, giradas na placa de vídeo
                GridSceneKit.ActiveBatch = new GridSceneKit.BillboardBatch(world);
                BuildGeometry(materials, world);
                AddWaterMap(world);

                EditorUtility.DisplayProgressBar(title, "Floresta", 0.4f);
                int forest = PlaceForest(materials, world);

                var arenasRoot = new GameObject("Arenas").transform;
                arenasRoot.SetParent(world, false);
                var rooms = new List<(GridArena arena, RoomManager room, NavMeshSurface surface)>();
                foreach (GridArena arena in s_Arenas)
                {
                    RoomManager room = BuildArena(arena, materials, arenasRoot, out NavMeshSurface surface);
                    rooms.Add((arena, room, surface));
                }

                PlacePlayer(scene);
                BiomeZone zone = GridSceneKit.AddBiomeZone(theme,
                    GridSceneKit.CellCenter(s_CenterX, s_Clearing.yMax - 1), new Vector3(24f, 12f, 2f), world);
                AddDebugTeleport(systems, zone);
                extend?.Invoke(scene, world);

                GridSceneKit.ActiveBatch.Build(theme.MeshPrefix + "_Sprite");
                GridSceneKit.ActiveBatch = null;
                GridSceneKit.AddOcclusionArea("Oclusao_" + theme.Biome, world,
                    new Vector3(s_Width * k_Cell * 0.5f, 10f, s_Depth * k_Cell * 0.5f),
                    new Vector3(s_Width * k_Cell, 30f, s_Depth * k_Cell));

                // Desenho do cenário só até 300 m (o mar, o céu, as silhuetas e o que se move ficam de fora)
                GridSceneKit.AddDistanceCulling(world, 300f, "Mar", "Gigantes_Fundo", "Feixe_Farol", "Destroco_");

                Physics.SyncTransforms();
                int perches = 0, validPerches = 0;
                foreach ((GridArena arena, RoomManager room, NavMeshSurface _) in rooms)
                    room.HighSpawnPoints = PlaceHighPoints(arena, room.transform, ref perches, ref validPerches);

                EditorSceneManager.SaveScene(scene, theme.ScenePath);
                int spawns = 0;
                for (int i = 0; i < rooms.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(title, $"NavMesh e spawns: {rooms[i].arena.Name}",
                        0.6f + 0.35f * i / rooms.Count);
                    GridSceneKit.BakeNavMesh(rooms[i].surface, theme.NavMeshFolder, $"Arena_{rooms[i].arena.Number}");
                    spawns += PlaceSpawns(rooms[i].arena, rooms[i].room);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorUtility.DisplayProgressBar(title, "Oclusão (alguns minutos)", 0.97f);
                GridSceneKit.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] {theme.Biome} em grade gerado em {theme.ScenePath}: {s_Arenas.Length} arenas, " +
                          $"{forest} árvores no cerco, {validPerches} de {perches} poleiros com linha de visão, " +
                          $"{spawns} pontos de spawn. Dê Play nessa cena (F6/F7 pulam entre as arenas).");
            }
            finally
            {
                GridSceneKit.ActiveBatch = null;
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------- mapa ----------

        // Dentro do lóbulo: elipse com o contorno ondulado pelo ruído da arena
        static bool InLobe(GridArena arena, GridLobe lobe, float x, float z)
        {
            Vector2 d = new Vector2(x, z) - (arena.Center + lobe.Offset);
            var q = new Vector2(d.x / lobe.RadiusX, d.y / lobe.RadiusZ);
            float angle = Mathf.Atan2(q.y, q.x);
            float limit = 1f + arena.Noise * (Mathf.Sin(3f * angle + arena.Phase) +
                                              0.7f * Mathf.Sin(5f * angle + 2f * arena.Phase));
            return q.magnitude <= limit;
        }

        static bool InArena(GridArena arena, int x, int z)
        {
            foreach (GridLobe lobe in arena.Lobes)
            {
                if (InLobe(arena, lobe, x + 0.5f, z + 0.5f))
                    return true;
            }

            return false;
        }

        static void BuildMap()
        {
            s_Arenas = s_Theme.Arenas;

            // Posições: as arenas sobem em z, uma depois da outra, com o corredor entre elas
            s_CenterX = k_Margin + k_HalfWidth;
            s_Width = 2 * (k_Margin + k_HalfWidth);
            s_Clearing = new RectInt(s_CenterX - 5, k_Margin, 11, 9);
            int cursor = s_Clearing.yMax;
            for (int i = 0; i < s_Arenas.Length; i++)
            {
                GridArena arena = s_Arenas[i];
                arena.Index = i;
                arena.HasExit = i < s_Arenas.Length - 1 || s_Theme.OpenEnd;
                arena.Perch.Clear();
                float grow = 1f + arena.Noise * 1.7f;
                arena.ExtentDown = 0f;
                arena.ExtentUp = 0f;
                arena.ExtentSide = 0f;
                foreach (GridLobe lobe in arena.Lobes)
                {
                    arena.ExtentDown = Mathf.Max(arena.ExtentDown, lobe.RadiusZ * grow - lobe.Offset.y);
                    arena.ExtentUp = Mathf.Max(arena.ExtentUp, lobe.RadiusZ * grow + lobe.Offset.y);
                    arena.ExtentSide = Mathf.Max(arena.ExtentSide, Mathf.Abs(lobe.Offset.x) + lobe.RadiusX * grow);
                }

                float centerZ = cursor + arena.Corridor + arena.ExtentDown + 1f;
                arena.Center = new Vector2(s_CenterX + arena.OffsetX + 0.5f, Mathf.Round(centerZ) + 0.5f);
                cursor = Mathf.CeilToInt(arena.Center.y + arena.ExtentUp) + 1;
                arena.Bounds = new RectInt(Mathf.FloorToInt(arena.Center.x - arena.ExtentSide) - 1,
                    Mathf.FloorToInt(arena.Center.y - arena.ExtentDown) - 1,
                    Mathf.CeilToInt(arena.ExtentSide * 2f) + 3, Mathf.CeilToInt(arena.ExtentDown + arena.ExtentUp) + 3);
            }

            s_Depth = cursor + k_Margin;
            s_Map = new Cell[s_Width, s_Depth];
            s_Owner = new int[s_Width, s_Depth];
            s_Path = new bool[s_Width, s_Depth];

            // Clareira de chegada
            for (int x = s_Clearing.xMin; x < s_Clearing.xMax; x++)
            for (int z = s_Clearing.yMin; z < s_Clearing.yMax; z++)
                s_Map[x, z] = Cell.Floor;
            for (int z = s_Clearing.yMin; z < s_Clearing.yMax; z++)
            for (int x = s_CenterX - 1; x <= s_CenterX + 1; x++)
                s_Path[x, z] = true;

            // Arenas
            foreach (GridArena arena in s_Arenas)
            {
                for (int x = arena.Bounds.xMin; x < arena.Bounds.xMax; x++)
                for (int z = arena.Bounds.yMin; z < arena.Bounds.yMax; z++)
                {
                    if (!InArena(arena, x, z))
                        continue;
                    s_Map[x, z] = Cell.Floor;
                    s_Owner[x, z] = arena.Index + 1;
                }
            }

            // Corredores: da saída anterior (ou da clareira) até a entrada de cada arena
            int fromX = s_CenterX, fromZ = s_Clearing.yMax;
            foreach (GridArena arena in s_Arenas)
            {
                int toX = Mathf.FloorToInt(arena.Center.x);
                arena.Entrance = new Vector2Int(toX, EntranceRow(arena, toX, fromZ));
                Corridor(fromX, fromZ, toX, arena.Entrance.y);
                FillGap(arena, toX, arena.Entrance.y + 1, 1);

                if (!arena.HasExit)
                    break;
                arena.Exit = new Vector2Int(toX, ExitRow(arena, toX));
                FillGap(arena, toX, arena.Exit.y - 1, -1);
                fromX = toX;
                fromZ = arena.Exit.y;
            }

            // Fim aberto: trilha da porta de saída da última arena até a borda norte do mapa
            if (s_Theme.OpenEnd)
            {
                CarveColumn(fromX, fromZ, s_Depth - 1);
                OpenEndPoint = GridSceneKit.CellCenter(fromX, s_Depth - 1) + Vector3.forward * (k_Cell * 0.5f);
            }

            foreach (GridArena arena in s_Arenas)
            {
                MarkArenaPath(arena);
                if (arena.River)
                    CarveRiver(arena);
                CarvePonds(arena);
            }

            // Borda invisível: toda célula vazia encostada no espaço andável
            for (int x = 1; x < s_Width - 1; x++)
            for (int z = 1; z < s_Depth - 1; z++)
            {
                if (s_Map[x, z] != Cell.Void)
                    continue;
                for (int dx = -1; dx <= 1 && s_Map[x, z] == Cell.Void; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (IsWalkable(s_Map[x + dx, z + dz]))
                    {
                        s_Map[x, z] = Cell.Wall;
                        break;
                    }
                }
            }

            // Fim aberto: a última fileira (onde o próximo trecho encosta) também ganha parede dos lados da trilha
            if (s_Theme.OpenEnd)
            {
                int z = s_Depth - 1;
                for (int x = 1; x < s_Width - 1; x++)
                {
                    if (s_Map[x, z] == Cell.Void && (IsWalkable(s_Map[x - 1, z]) || IsWalkable(s_Map[x + 1, z]) ||
                                                     IsWalkable(s_Map[x - 1, z - 1]) || IsWalkable(s_Map[x + 1, z - 1])))
                        s_Map[x, z] = Cell.Wall;
                }
            }

            foreach (GridArena arena in s_Arenas)
            {
                PlacePerches(arena);
                PlaceRocks(arena);
                PlaceTrees(arena);
            }

            ComputeDistances();
        }

        // Linha da porta de entrada: logo abaixo da primeira célula da arena nas 5 colunas em volta do corredor (as
        // colunas dos lados da porta ficam fora da arena e viram parede)
        static int EntranceRow(GridArena arena, int x, int fromZ)
        {
            int row = int.MaxValue;
            for (int c = x - 2; c <= x + 2; c++)
            {
                int z = fromZ;
                while (z < s_Depth - 1 && s_Owner[c, z] != arena.Index + 1)
                    z++;
                row = Mathf.Min(row, z - 1);
            }

            return row;
        }

        static int ExitRow(GridArena arena, int x)
        {
            int row = int.MinValue;
            for (int c = x - 2; c <= x + 2; c++)
            {
                int z = arena.Bounds.yMax;
                while (z > 0 && s_Owner[c, z] != arena.Index + 1)
                    z--;
                row = Mathf.Max(row, z + 1);
            }

            return row;
        }

        // Liga a linha da porta à arena nas 3 colunas do meio (o contorno pode não ser reto ali)
        static void FillGap(GridArena arena, int x, int startZ, int step)
        {
            for (int c = x - 1; c <= x + 1; c++)
            for (int z = startZ; z > 0 && z < s_Depth - 1 && s_Owner[c, z] != arena.Index + 1; z += step)
            {
                s_Map[c, z] = Cell.Floor;
                s_Path[c, z] = true;
            }
        }

        // Corredor de 3 células: sobe, vira para o lado (se precisar) e sobe de novo; tudo é trilha
        static void Corridor(int fromX, int fromZ, int toX, int toZ)
        {
            int turn = (fromZ + toZ) / 2;
            CarveColumn(fromX, fromZ, turn);
            for (int x = Mathf.Min(fromX, toX) - 1; x <= Mathf.Max(fromX, toX) + 1; x++)
            for (int z = turn - 1; z <= turn + 1; z++)
                Carve(x, z);
            CarveColumn(toX, turn, toZ);
        }

        static void CarveColumn(int x, int fromZ, int toZ)
        {
            for (int z = Mathf.Min(fromZ, toZ); z <= Mathf.Max(fromZ, toZ); z++)
            for (int c = x - 1; c <= x + 1; c++)
                Carve(c, z);
        }

        static void Carve(int x, int z)
        {
            if (s_Owner[x, z] != 0)
                return;
            s_Map[x, z] = Cell.Floor;
            s_Path[x, z] = true;
        }

        // Trilha serpenteando pela arena, da porta de entrada até a de saída (no chefe, até o centro)
        static void MarkArenaPath(GridArena arena)
        {
            var start = new Vector2(arena.Entrance.x, arena.Entrance.y + 1);
            Vector2 end = arena.HasExit ? new Vector2(arena.Exit.x, arena.Exit.y - 1) : arena.Center;
            float amplitude = arena.ExtentSide * 0.3f;
            for (int i = 0; i <= 300; i++)
            {
                float t = i / 300f;
                Vector2 point = Vector2.Lerp(start, end, t) + Vector2.right * Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
                int px = Mathf.RoundToInt(point.x), pz = Mathf.RoundToInt(point.y);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = px + dx, z = pz + dz;
                    if (s_Owner[x, z] == arena.Index + 1)
                        s_Path[x, z] = true;
                }
            }
        }

        static bool NearPath(int x, int z, int margin)
        {
            for (int dx = -margin; dx <= margin; dx++)
            for (int dz = -margin; dz <= margin; dz++)
            {
                int px = x + dx, pz = z + dz;
                if (px >= 0 && pz >= 0 && px < s_Width && pz < s_Depth && s_Path[px, pz])
                    return true;
            }

            return false;
        }

        // ---------- água ----------

        static bool NearDoor(GridArena arena, int x, int z, float distance) =>
            Vector2Int.Distance(V(x, z), arena.Entrance) < distance ||
            (arena.HasExit && Vector2Int.Distance(V(x, z), arena.Exit) < distance);

        // Rio atravessando a arena de um lado ao outro, serpenteando, com largura variável. É raso (a trilha e as
        // partes estreitas cruzam a pé) e tem poços fundos no meio, longe da trilha e das portas.
        static void CarveRiver(GridArena arena)
        {
            var random = new System.Random(700 + arena.Number);
            float phase = (float)random.NextDouble() * Mathf.PI * 2f;
            float amplitude = Mathf.Min(arena.ExtentUp, arena.ExtentDown) * 0.25f;
            float baseZ = arena.Center.y + ((float)random.NextDouble() - 0.5f) * 4f;
            int minX = arena.Bounds.xMin, maxX = arena.Bounds.xMax;
            for (int x = minX; x < maxX; x++)
            {
                float t = (x - minX) / (float)Mathf.Max(1, maxX - minX);
                float centerZ = baseZ + Mathf.Sin(t * Mathf.PI * 1.8f + phase) * amplitude;
                float half = Mathf.Max(1.2f, 1.7f + 1.3f * Mathf.Sin(t * Mathf.PI * 3.4f + phase * 2f));
                for (int z = Mathf.FloorToInt(centerZ - half - 1f); z <= Mathf.CeilToInt(centerZ + half + 1f); z++)
                {
                    if (s_Owner[x, z] != arena.Index + 1 || s_Map[x, z] != Cell.Floor || NearDoor(arena, x, z, 5f))
                        continue;
                    float distance = Mathf.Abs(z + 0.5f - centerZ);
                    if (distance > half)
                        continue;
                    bool deep = distance <= half - 1.4f && !NearPath(x, z, 3) && !NearDoor(arena, x, z, 8f);
                    s_Map[x, z] = deep ? Cell.Deep : Cell.Shallow;
                }
            }
        }

        // Lagoas: miolo fundo e margem rasa, longe da trilha e das portas
        static void CarvePonds(GridArena arena)
        {
            var random = new System.Random(800 + arena.Number);
            int made = 0;
            for (int attempt = 0; attempt < 400 && made < arena.Ponds; attempt++)
            {
                Vector2Int center = RandomCell(arena, random);
                float radius = 2.6f + (float)random.NextDouble() * 1.6f;
                int reach = Mathf.CeilToInt(radius) + 1;
                if (!Free(arena, center.x, center.y, reach) || NearPath(center.x, center.y, reach + 2) ||
                    NearDoor(arena, center.x, center.y, 10f))
                    continue;

                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    int x = center.x + dx, z = center.y + dz;
                    float angle = Mathf.Atan2(dz, dx);
                    float limit = radius * (1f + 0.15f * Mathf.Sin(3f * angle + phase));
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance <= limit)
                        s_Map[x, z] = distance <= limit - 1.3f ? Cell.Deep : Cell.Shallow;
                }

                made++;
            }
        }

        // ---------- cobertura, poleiros e árvores da arena ----------

        // Ponto que os poleiros precisam ver: o centro da arena (ou do primeiro lóbulo, se o centro cair fora)
        static Vector2 Focus(GridArena arena)
        {
            Vector2Int center = Vector2Int.FloorToInt(arena.Center);
            return s_Owner[center.x, center.y] == arena.Index + 1 ? arena.Center : arena.Center + arena.Lobes[0].Offset;
        }

        static List<(Vector2 a, Vector2 b)> SightLines(GridArena arena)
        {
            var entrance = new Vector2(arena.Entrance.x, arena.Entrance.y + 3);
            var lines = new List<(Vector2 a, Vector2 b)>();
            foreach (Vector2Int perch in arena.Perch)
            {
                var top = new Vector2(perch.x + 0.5f, perch.y + 0.5f);
                lines.Add((top, Focus(arena)));
                lines.Add((top, entrance));
            }

            return lines;
        }

        // Célula de chão seco da própria arena, com 'clearance' células de chão seco em volta
        static bool Free(GridArena arena, int x, int z, int clearance)
        {
            for (int dx = -clearance; dx <= clearance; dx++)
            for (int dz = -clearance; dz <= clearance; dz++)
            {
                if (s_Owner[x + dx, z + dz] != arena.Index + 1 || s_Map[x + dx, z + dz] != Cell.Floor)
                    return false;
            }

            return true;
        }

        // Poleiros (2 x 2) em anel a ~58% do raio de cada lóbulo, longe das portas e da trilha
        static void PlacePerches(GridArena arena)
        {
            var random = new System.Random(500 + arena.Number);
            for (int attempt = 0; attempt < 300 && arena.Perch.Count < arena.Perches; attempt++)
            {
                GridLobe lobe = arena.Lobes[attempt % arena.Lobes.Length];
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector2 p = arena.Center + lobe.Offset +
                            new Vector2(Mathf.Cos(angle) * lobe.RadiusX, Mathf.Sin(angle) * lobe.RadiusZ) * 0.58f;
                var cell = new Vector2Int(Mathf.RoundToInt(p.x - 1f), Mathf.RoundToInt(p.y - 1f));
                if (NearDoor(arena, cell.x, cell.y, 9f))
                    continue;
                if (!Free(arena, cell.x, cell.y, 2) || !Free(arena, cell.x + 1, cell.y + 1, 2) ||
                    NearPath(cell.x, cell.y, 2) || NearPath(cell.x + 1, cell.y + 1, 2))
                    continue;
                if (arena.Perch.Exists(other => Vector2Int.Distance(other, cell) < 9f))
                    continue;

                arena.Perch.Add(cell);
                for (int dx = 0; dx < 2; dx++)
                for (int dz = 0; dz < 2; dz++)
                    s_Map[cell.x + dx, cell.y + dz] = Cell.Perch;
            }
        }

        static readonly Vector2Int[][] k_Shapes =
        {
            new[] { V(0, 0) },
            new[] { V(0, 0), V(1, 0) },
            new[] { V(0, 0), V(1, 0), V(2, 0) },
            new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) },
            new[] { V(0, 0), V(1, 0), V(0, 1) },
            new[] { V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(0, 2) },
            new[] { V(0, 0), V(1, 0), V(2, 0), V(1, 1) },
        };

        static Vector2Int Rotate(Vector2Int v, int quarterTurns)
        {
            for (int i = 0; i < quarterTurns; i++)
                v = new Vector2Int(-v.y, v.x);
            return v;
        }

        static Vector2Int RandomCell(GridArena arena, System.Random random) =>
            new Vector2Int(random.Next(arena.Bounds.xMin, arena.Bounds.xMax), random.Next(arena.Bounds.yMin, arena.Bounds.yMax));

        // Cobertura: formas sorteadas longe das portas e da trilha, com 2 células (4 m) livres em volta (passa até o
        // Golem) e fora das linhas de visão dos poleiros
        static void PlaceRocks(GridArena arena)
        {
            var random = new System.Random(4242 + arena.Number * 31);
            List<(Vector2 a, Vector2 b)> sightLines = SightLines(arena);
            int placed = 0;
            for (int attempt = 0; attempt < 4000 && placed < arena.Rocks; attempt++)
            {
                Vector2Int[] shape = k_Shapes[random.Next(k_Shapes.Length)];
                int rotation = random.Next(4);
                Vector2Int origin = RandomCell(arena, random);
                var cells = new List<Vector2Int>();
                foreach (Vector2Int offset in shape)
                    cells.Add(origin + Rotate(offset, rotation));

                bool ok = true;
                foreach (Vector2Int cell in cells)
                {
                    ok = Free(arena, cell.x, cell.y, 2) && !NearPath(cell.x, cell.y, 1) &&
                         Vector2Int.Distance(cell, arena.Entrance) >= 8f &&
                         (!arena.HasExit || Vector2Int.Distance(cell, arena.Exit) >= 5f) &&
                         Vector2.Distance(cell, Focus(arena)) >= 3f;
                    foreach ((Vector2 a, Vector2 b) line in sightLines)
                        ok &= DistanceToSegment(cell, line.a, line.b) >= 1.4f;
                    if (!ok)
                        break;
                }

                if (!ok)
                    continue;
                foreach (Vector2Int cell in cells)
                    s_Map[cell.x, cell.y] = Cell.Rock;
                placed++;
            }
        }

        // Árvores soltas na arena (sprites com colisão no tronco)
        static void PlaceTrees(GridArena arena)
        {
            var random = new System.Random(911 + arena.Number * 17);
            List<(Vector2 a, Vector2 b)> sightLines = SightLines(arena);
            int placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < arena.Trees; attempt++)
            {
                Vector2Int cell = RandomCell(arena, random);
                if (!Free(arena, cell.x, cell.y, 1) || NearPath(cell.x, cell.y, 1) ||
                    Vector2Int.Distance(cell, arena.Entrance) < 7f ||
                    (arena.HasExit && Vector2Int.Distance(cell, arena.Exit) < 4f))
                    continue;

                bool blocks = false;
                foreach ((Vector2 a, Vector2 b) line in sightLines)
                    blocks |= DistanceToSegment(cell, line.a, line.b) < 0.9f;
                if (blocks)
                    continue;

                s_Map[cell.x, cell.y] = Cell.Tree;
                placed++;
            }
        }

        // Distância (passos de 8 vizinhos) de cada célula até o espaço andável (ou bloco) mais próximo
        static void ComputeDistances()
        {
            s_Distance = new int[s_Width, s_Depth];
            s_Nearest = new Vector2Int[s_Width, s_Depth];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                Cell cell = s_Map[x, z];
                bool inside = IsWalkable(cell) || cell == Cell.Rock || cell == Cell.Perch || cell == Cell.Deep;
                s_Distance[x, z] = inside ? 0 : int.MaxValue;
                s_Nearest[x, z] = V(x, z);
                if (inside)
                    queue.Enqueue(V(x, z));
            }

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                int next = s_Distance[c.x, c.y] + 1;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = c.x + dx, z = c.y + dz;
                    if (x < 0 || z < 0 || x >= s_Width || z >= s_Depth || s_Distance[x, z] <= next)
                        continue;
                    s_Distance[x, z] = next;
                    s_Nearest[x, z] = s_Nearest[c.x, c.y];
                    queue.Enqueue(V(x, z));
                }
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(p, a + ab * t);
        }

        static bool IsWalkable(Cell cell) => cell == Cell.Floor || cell == Cell.Tree || cell == Cell.Shallow;

        static bool IsWater(Cell cell) => cell == Cell.Shallow || cell == Cell.Deep;

        static Cell At(int x, int z) => x < 0 || z < 0 || x >= s_Width || z >= s_Depth ? Cell.Void : s_Map[x, z];

        // Altura do chão da célula (água mais funda)
        static float GroundHeight(Cell cell) =>
            cell == Cell.Shallow ? -s_Theme.ShallowDepth : cell == Cell.Deep ? -s_Theme.DeepDepth : 0f;

        // ---------- geometria ----------

        static void BuildGeometry(GridTextureKit.Set materials, Transform parent)
        {
            var rockSides = new GridSceneKit.MeshData();
            var rockTops = new GridSceneKit.MeshData();
            var perchSides = new GridSceneKit.MeshData();
            var perchTops = new GridSceneKit.MeshData();
            var barrier = new GridSceneKit.MeshData();
            var grass = new GridSceneKit.MeshData();
            var farGrass = new GridSceneKit.MeshData();
            var path = new GridSceneKit.MeshData();
            var bed = new GridSceneKit.MeshData();
            var banks = new GridSceneKit.MeshData();
            var shallowSurface = new GridSceneKit.MeshData();
            var deepSurface = new GridSceneKit.MeshData();

            Vector2Int[] directions = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                Cell cell = s_Map[x, z];

                if (IsWater(cell))
                {
                    // Fundo, superfície e margens até as células mais altas em volta
                    float bottom = GroundHeight(cell);
                    bed.AddHorizontal(x, z, bottom);
                    (cell == Cell.Shallow ? shallowSurface : deepSurface).AddHorizontal(x, z, k_WaterSurface);
                    foreach (Vector2Int d in directions)
                    {
                        float neighbor = GroundHeight(At(x + d.x, z + d.y));
                        if (neighbor > bottom)
                            banks.AddSideRange(x + d.x, z + d.y, -d, bottom, neighbor);
                    }
                }
                else if (IsWalkable(cell) || s_Distance[x, z] <= 2)
                {
                    // Chão perto do espaço andável tem colisão; o resto (até 100 m além da borda) é só visual
                    (s_Path[x, z] ? path : grass).AddHorizontal(x, z, 0f);
                }
                else if (s_Distance[x, z] <= k_FloorDepth)
                {
                    farGrass.AddHorizontal(x, z, 0f);
                }

                // Água funda: borda invisível nos lados que dão para onde se anda
                if (cell == Cell.Deep)
                {
                    foreach (Vector2Int d in directions)
                    {
                        if (IsWalkable(At(x + d.x, z + d.y)))
                            barrier.AddSide(x, z, d, k_BarrierHeight);
                    }

                    continue;
                }

                GridSceneKit.MeshData sides = cell == Cell.Rock ? rockSides : cell == Cell.Perch ? perchSides :
                    cell == Cell.Wall ? barrier : null;
                if (sides == null)
                    continue;

                float height = cell == Cell.Wall ? k_BarrierHeight : k_BlockHeight;
                if (cell != Cell.Wall)
                    (cell == Cell.Rock ? rockTops : perchTops).AddHorizontal(x, z, height);
                foreach (Vector2Int d in directions)
                {
                    if (IsWalkable(At(x + d.x, z + d.y)))
                        sides.AddSide(x, z, d, height);
                }
            }

            string prefix = s_Theme.MeshPrefix;
            GridSceneKit.CreateObject(prefix + "_Rochedos", "Cobertura", rockSides, materials.Rock, parent, true);
            GridSceneKit.CreateObject(prefix + "_Rochedos_Topo", "Cobertura_Topo", rockTops, materials.RockTop, parent,
                true);
            GridSceneKit.CreateObject(prefix + "_Tocos", "Tocos", perchSides, materials.Perch, parent, true);
            GridSceneKit.CreateObject(prefix + "_Tocos_Topo", "Tocos_Topo", perchTops, materials.PerchTop, parent, true);
            GridSceneKit.CreateObject(prefix + "_Grama", "Chao", grass, materials.Grass, parent, true);
            GridSceneKit.CreateObject(prefix + "_Grama_Longe", "Chao_Longe", farGrass, materials.Grass, parent, false);
            GridSceneKit.CreateObject(prefix + "_Trilha", "Trilha", path, materials.Path, parent, true);

            if (materials.WaterBed)
            {
                GridSceneKit.CreateObject(prefix + "_Agua_Fundo", "Agua_Fundo", bed, materials.WaterBed, parent, true);
                GridSceneKit.CreateObject(prefix + "_Agua_Margem", "Agua_Margem", banks, materials.WaterBed, parent, true);
                GameObject shallow = GridSceneKit.CreateObject(prefix + "_Agua_Rasa", "Agua_Rasa", shallowSurface,
                    materials.WaterSurface, parent, false, false, false);
                GameObject deep = GridSceneKit.CreateObject(prefix + "_Agua_Funda", "Agua_Funda", deepSurface,
                    materials.DeepSurface, parent, false, false, false);
                if (shallow)
                    shallow.AddComponent<WaterRipple>();
                if (deep)
                    deep.AddComponent<WaterRipple>().Speed *= 0.6f;
            }

            // Borda: colisão sem desenho; projéteis atravessam (não aparecem faíscas "no ar")
            GameObject edge = GridSceneKit.CreateObject(prefix + "_Borda", "Borda_Invisivel", barrier, null, parent,
                true, true, false);
            if (edge)
                edge.AddComponent<IgnoreHitDetection>();
        }

        // Mapa da água rasa para a lentidão (WaterMap); sem água, não cria nada
        static void AddWaterMap(Transform parent)
        {
            var shallow = new bool[s_Width * s_Depth];
            bool any = false;
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                if (s_Map[x, z] != Cell.Shallow)
                    continue;
                shallow[z * s_Width + x] = true;
                any = true;
            }

            if (!any)
                return;
            var map = new GameObject("AguaRasa").AddComponent<WaterMap>();
            map.transform.SetParent(parent, false);
            map.Origin = Vector3.zero;
            map.CellSize = k_Cell;
            map.Width = s_Width;
            map.Depth = s_Depth;
            map.Shallow = shallow;
            map.SlowFactor = s_Theme.WaterSlow;
        }

        // ---------- floresta e decoração ----------

        // Árvores das arenas, cerco de 40 fileiras, plantas e esporos. Devolve quantas árvores o cerco tem.
        static int PlaceForest(GridTextureKit.Set materials, Transform parent)
        {
            Mesh quad = GridSceneKit.SpriteQuad();
            var random = new System.Random(1301);
            var groves = new Groves(materials.Trees, random, s_Width * k_Cell, s_Depth * k_Cell);
            var cards = new GridSceneKit.Cards();
            var spacing = new TreeSpacing();

            // Árvores soltas nas arenas: tamanhos menores, cor e brilho de perto, tronco sólido
            var treeRoot = new GameObject("Arvores").transform;
            treeRoot.SetParent(parent, false);
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                if (s_Map[x, z] != Cell.Tree)
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Jitter(random, 0.3f);
                GridTextureKit.TreeSpecies species = groves.SpeciesAt(position, random);
                GridTextureKit.TreeSize size = species.Sizes[random.NextDouble() < 0.6 ? 0 : 1];
                float jitter = 0.92f + (float)random.NextDouble() * 0.16f;
                GameObject tree = GridSceneKit.Billboard(species.Name, quad, Pick(size.NearTints, random),
                    size.Size * jitter, position, treeRoot, random.NextDouble() < 0.5);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = Mathf.Min(size.TrunkRadius * jitter, 0.9f);
                trunk.height = k_BlockHeight;
                trunk.center = new Vector3(0f, k_BlockHeight * 0.5f, 0f);
                spacing.Add(new Vector2(position.x, position.z), 2f);
                if (species.Accent)
                    MushroomCluster(materials, quad, random, position, size.Scale, treeRoot, null, default);
            }

            // Cerco: as de perto vêm primeiro (fecham a vista na altura dos olhos), com cor e brilho; no meio, mais
            // escuras; no fundo, silhuetas. Quanto mais longe, maior o desenho (mesmo pixel). Cada árvore guarda uma
            // distância das outras proporcional ao tamanho, então nenhuma fica grudada em outra.
            var candidates = new List<(int distance, float order, Vector2Int cell)>();
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                int distance = s_Distance[x, z];
                if (distance >= 1 && distance <= k_ForestDepth && !IsWalkable(s_Map[x, z]) && s_Map[x, z] != Cell.Deep)
                    candidates.Add((distance, (float)random.NextDouble(), V(x, z)));
            }

            candidates.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance) : a.order.CompareTo(b.order));

            var forest = new GameObject("Floresta").transform;
            forest.SetParent(parent, false);
            int count = 0;
            foreach ((int distance, float order, Vector2Int cell) candidate in candidates)
            {
                int d = candidate.distance;
                Vector3 position = GridSceneKit.CellCenter(candidate.cell.x, candidate.cell.y) + Jitter(random, 0.9f);
                GridTextureKit.TreeSpecies species = groves.SpeciesAt(position, random);
                int sizeIndex = d <= 3 ? (random.NextDouble() < 0.3 ? 1 : 0) :
                    d <= 12 ? (random.NextDouble() < 0.3 ? 2 : 1) : 2;
                GridTextureKit.TreeSize size = species.Sizes[sizeIndex];
                float jitter = 0.9f + (float)random.NextDouble() * 0.2f;

                // Espaço livre em volta: menor perto da borda (paredão denso que marca os corredores)
                float radius = size.Size.x * jitter * (d <= 4 ? 0.15f : 0.21f);
                var flat = new Vector2(position.x, position.z);
                if (!spacing.IsFree(flat, radius))
                    continue;
                spacing.Add(flat, radius);

                // Tom sorteado (perto e meio) e metade espelhada: a floresta não parece carimbo repetido
                Material material = d <= 6 ? Pick(size.NearTints, random) : d <= 14 ? Pick(size.MidTints, random) : size.Far;
                bool flip = random.NextDouble() < 0.5;
                if (d <= k_BillboardDepth)
                    GridSceneKit.Billboard(species.Name, quad, material, size.Size * jitter, position, forest, flip);
                else
                    cards.Add(material, position, FacingWalkable(candidate.cell, position), size.Size * jitter, flip);
                count++;

                // Árvore de destaque: cogumelos pequenos em volta do pé (perto e no meio do cerco)
                if (species.Accent && d <= 14)
                    MushroomCluster(materials, quad, random, position, size.Scale, forest,
                        d <= k_BillboardDepth ? null : cards, FacingWalkable(candidate.cell, position));
            }

            PlaceDecor(materials, quad, random, cards, parent);
            cards.Build(s_Theme.MeshPrefix, forest);

            // Esporos neon: um enxame baixo em cada arena e um alto e esparso sobre a floresta toda
            foreach (GridArena arena in s_Arenas)
            {
                Vector3 center = GridSceneKit.CellCenter(arena.Center.x - 0.5f, arena.Center.y - 0.5f);
                var size = new Vector3(arena.Bounds.width * k_Cell, 8f, arena.Bounds.height * k_Cell);
                int max = Mathf.RoundToInt(size.x * size.z * 0.013f);
                GridSceneKit.AddSpores($"Esporos_Neon_{arena.Number}", materials.Spores, center + Vector3.up * 4f, size,
                    max, max / 10f, new Vector2(0.08f, 0.2f), s_Theme.SporeColors, parent);
            }

            var all = new Vector3(s_Width * k_Cell - 40f, 22f, s_Depth * k_Cell - 40f);
            int forestMax = Mathf.RoundToInt(all.x * all.z * 0.0019f);
            GridSceneKit.AddSpores("Esporos_Neon_Floresta", materials.Spores,
                new Vector3(s_Width * k_Cell * 0.5f, 12f, s_Depth * k_Cell * 0.5f), all, forestMax, forestMax / 12f,
                new Vector2(0.15f, 0.35f), s_Theme.SporeColors, parent);
            return count;
        }

        // Plantas e detalhes do tema: densos no fundo (cartas fixas) e raros no chão seco do espaço andável (sprites
        // que olham para o jogador), fora da trilha e da água
        static void PlaceDecor(GridTextureKit.Set materials, Mesh quad, System.Random random, GridSceneKit.Cards cards,
            Transform parent)
        {
            var decor = new GameObject("Decoracao").transform;
            decor.SetParent(parent, false);
            for (int x = 0; x < s_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                Cell cell = s_Map[x, z];
                int distance = s_Distance[x, z];
                bool inside = cell == Cell.Floor;
                if (s_Path[x, z] || cell == Cell.Tree || cell == Cell.Rock || cell == Cell.Perch || IsWater(cell) ||
                    distance > 12)
                    continue;

                // Dentro, cerca de 4 vezes mais raro que no fundo
                float roll = (float)random.NextDouble() / (inside ? 0.25f : 1f);
                GridTextureKit.DecorItem item = null;
                foreach (GridTextureKit.DecorItem candidate in materials.Decor)
                {
                    if (roll < candidate.Chance)
                    {
                        item = candidate;
                        break;
                    }

                    roll -= candidate.Chance;
                }

                if (item == null)
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Jitter(random, 0.7f);
                Material material = Pick(item.Materials, random);
                Vector2 size = item.Size * Mathf.Lerp(item.Scale.x, item.Scale.y, (float)random.NextDouble());
                bool flip = random.NextDouble() < 0.5;
                if (inside)
                    GridSceneKit.Billboard("Planta", quad, material, size, position, decor, flip);
                else
                    cards.Add(material, position, FacingWalkable(V(x, z), position), size, flip);
            }
        }

        // De 5 a 9 cogumelos pequenos espalhados em volta do pé de uma árvore de destaque (sprites que olham para o
        // jogador, ou cartas fixas viradas para 'facing' quando 'cards' é dado)
        static void MushroomCluster(GridTextureKit.Set materials, Mesh quad, System.Random random, Vector3 foot,
            float treeScale, Transform parent, GridSceneKit.Cards cards, Vector3 facing)
        {
            if (!materials.MushroomCyan)
                return;
            int count = 5 + random.Next(5);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = (1.2f + (float)random.NextDouble() * 3.2f) * Mathf.Sqrt(treeScale);
                Vector3 position = foot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Material material = random.NextDouble() < 0.7 ? materials.MushroomCyan : materials.MushroomAmber;
                Vector2 size = materials.MushroomSize * (0.5f + (float)random.NextDouble() * 0.9f);
                bool flip = random.NextDouble() < 0.5;
                if (cards != null)
                    cards.Add(material, position, facing, size, flip);
                else
                    GridSceneKit.Billboard("Cogumelo", quad, material, size, position, parent, flip);
            }
        }

        // Direção horizontal de uma carta para o espaço andável mais próximo (de onde o jogador a vê)
        static Vector3 FacingWalkable(Vector2Int cell, Vector3 position)
        {
            Vector2Int nearest = s_Nearest[cell.x, cell.y];
            Vector3 toward = GridSceneKit.CellCenter(nearest.x, nearest.y) - position;
            toward.y = 0f;
            return toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.back;
        }

        static Material Pick(Material[] options, System.Random random) => options[random.Next(options.Length)];

        static Vector3 Jitter(System.Random random, float amount) =>
            new Vector3(((float)random.NextDouble() * 2f - 1f) * amount, 0f, ((float)random.NextDouble() * 2f - 1f) * amount);

        // Espécies: as normais se misturam livremente (pelo peso de cada uma); a de destaque cresce em manchas
        // próprias: centros espalhados numa grade de ~30 m, alguns marcados como mancha de destaque. Perto de um
        // centro desses, a árvore é do destaque na maior parte das vezes.
        class Groves
        {
            const float k_Spacing = 30f;
            const float k_AccentChance = 0.2f;   // fração dos centros que viram mancha de destaque
            const float k_AccentRadius = 11f;    // raio da mancha, em metros

            readonly List<GridTextureKit.TreeSpecies> m_Mixed = new List<GridTextureKit.TreeSpecies>();
            readonly List<GridTextureKit.TreeSpecies> m_Accents = new List<GridTextureKit.TreeSpecies>();
            readonly Dictionary<Vector2Int, (Vector2 center, bool accent)> m_Centers =
                new Dictionary<Vector2Int, (Vector2 center, bool accent)>();

            public Groves(GridTextureKit.TreeSpecies[] species, System.Random random, float width, float depth)
            {
                foreach (GridTextureKit.TreeSpecies s in species)
                    (s.Accent ? m_Accents : m_Mixed).Add(s);
                for (int gx = 0; gx * k_Spacing < width + k_Spacing; gx++)
                for (int gz = 0; gz * k_Spacing < depth + k_Spacing; gz++)
                {
                    var center = new Vector2((gx + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.8f) * k_Spacing,
                        (gz + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.8f) * k_Spacing);
                    m_Centers[new Vector2Int(gx, gz)] = (center, m_Accents.Count > 0 && random.NextDouble() < k_AccentChance);
                }
            }

            static GridTextureKit.TreeSpecies Pick(List<GridTextureKit.TreeSpecies> list, System.Random random)
            {
                float total = 0f;
                foreach (GridTextureKit.TreeSpecies species in list)
                    total += species.Weight;
                float roll = (float)random.NextDouble() * total;
                foreach (GridTextureKit.TreeSpecies species in list)
                {
                    roll -= species.Weight;
                    if (roll <= 0f)
                        return species;
                }

                return list[0];
            }

            public GridTextureKit.TreeSpecies SpeciesAt(Vector3 position, System.Random random)
            {
                var flat = new Vector2(position.x, position.z);
                var key = new Vector2Int(Mathf.FloorToInt(flat.x / k_Spacing), Mathf.FloorToInt(flat.y / k_Spacing));
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!m_Centers.TryGetValue(new Vector2Int(key.x + dx, key.y + dz), out var grove) || !grove.accent)
                        continue;
                    if ((grove.center - flat).magnitude < k_AccentRadius && random.NextDouble() < 0.75)
                        return Pick(m_Accents, random);
                }

                return Pick(m_Mixed, random);
            }
        }

        // Grade espacial simples para manter as árvores do cerco afastadas umas das outras
        class TreeSpacing
        {
            const float k_Bucket = 6f;
            readonly Dictionary<Vector2Int, List<(Vector2 position, float radius)>> m_Buckets =
                new Dictionary<Vector2Int, List<(Vector2 position, float radius)>>();
            float m_MaxRadius;

            static Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / k_Bucket), Mathf.FloorToInt(p.y / k_Bucket));

            public void Add(Vector2 position, float radius)
            {
                Vector2Int key = Key(position);
                if (!m_Buckets.TryGetValue(key, out var list))
                    m_Buckets[key] = list = new List<(Vector2 position, float radius)>();
                list.Add((position, radius));
                m_MaxRadius = Mathf.Max(m_MaxRadius, radius);
            }

            public bool IsFree(Vector2 position, float radius)
            {
                int reach = Mathf.CeilToInt((radius + m_MaxRadius) / k_Bucket);
                Vector2Int key = Key(position);
                for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    if (!m_Buckets.TryGetValue(new Vector2Int(key.x + dx, key.y + dz), out var list))
                        continue;
                    foreach ((Vector2 other, float otherRadius) in list)
                    {
                        if (Vector2.Distance(position, other) < radius + otherRadius)
                            return false;
                    }
                }

                return true;
            }
        }

        // ---------- arenas ----------

        static RoomManager BuildArena(GridArena arena, GridTextureKit.Set materials, Transform parent,
            out NavMeshSurface surface)
        {
            var root = new GameObject($"Arena_{arena.Number:00}_{arena.Name.Replace(' ', '_')}");
            root.transform.SetParent(parent, false);
            Vector3 center = GridSceneKit.CellCenter(arena.Center.x - 0.5f, arena.Center.y - 0.5f);
            root.transform.position = center;

            // Tranca quando o jogador já está ~10 m para dentro da porta mais próxima do centro (o gatilho nunca
            // alcança os corredores); o raio também limita o voo dos Zangões
            Vector3 entrance = GridSceneKit.CellCenter(arena.Entrance.x, arena.Entrance.y);
            float reach = Vector3.Distance(center, entrance);
            if (arena.HasExit)
                reach = Mathf.Min(reach, Vector3.Distance(center, GridSceneKit.CellCenter(arena.Exit.x, arena.Exit.y)));
            var trigger = root.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 1;
            trigger.radius = Mathf.Clamp(reach - 10f, 8f, 60f);
            trigger.height = 40f;
            trigger.center = new Vector3(0f, 10f, 0f);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = arena.Name;
            var doors = new List<RoomDoor> { GridSceneKit.CreateDoor("Porta_Entrada", arena.Entrance, materials.Door, root.transform) };
            if (arena.HasExit)
                doors.Add(GridSceneKit.CreateDoor("Porta_Saida", arena.Exit, materials.Door, root.transform));
            room.Doors = doors.ToArray();
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = s_Theme.BiomeIndex;
            room.ArenaNumber = arena.Number;
            room.ArenaInBiome = arena.Number - 1;
            room.ArenasInBiome = s_Arenas.Length;
            room.IsBoss = arena.Boss;

            if (arena.Boss)
            {
                // O chefe entra do lado oposto à entrada, virado para ela
                var bossPoint = new GameObject("ChefeSpawn").transform;
                bossPoint.SetParent(root.transform, false);
                bossPoint.position = center + Vector3.forward * arena.Lobes[0].RadiusZ * k_Cell * 0.35f;
                bossPoint.rotation = Quaternion.LookRotation(Vector3.back);
                room.BossPoint = bossPoint;
                room.BossEnemy = room.Balance ? room.Balance.BossFor(s_Theme.BiomeIndex) : null;
            }

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            Vector3 boundsCenter = GridSceneKit.CellCenter(arena.Bounds.center.x - 0.5f, arena.Bounds.center.y - 0.5f);
            surface.center = boundsCenter - center + Vector3.up * 8f;
            surface.size = new Vector3(arena.Bounds.width * k_Cell + 4f, 40f, arena.Bounds.height * k_Cell + 4f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        static HighSpawnPoint[] PlaceHighPoints(GridArena arena, Transform room, ref int total, ref int valid)
        {
            var group = new GameObject("PontosAltos").transform;
            group.SetParent(room, false);
            Vector2 focus = Focus(arena);
            Vector3 center = GridSceneKit.CellCenter(focus.x - 0.5f, focus.y - 0.5f);
            Vector3 entrance = GridSceneKit.CellCenter(arena.Entrance.x, arena.Entrance.y + 3);

            var points = new List<HighSpawnPoint>();
            foreach (Vector2Int perch in arena.Perch)
            {
                HighSpawnPoint point = GridSceneKit.HighPoint(perch, group, center, entrance, points.Count + 1);
                total++;
                if (point.Validate())
                    valid++;
                else
                    Debug.LogWarning($"[Ícaro] {arena.Name}: {point.name} sem linha de visão.", point);
                points.Add(point);
            }

            return points.ToArray();
        }

        // De 6 a 12 pontos no chão seco da arena, longe da entrada, escondidos de quem entra, com ~2 m livres em volta
        static int PlaceSpawns(GridArena arena, RoomManager room)
        {
            var random = new System.Random(77 + arena.Number);
            Vector3 entry = GridSceneKit.CellCenter(arena.Entrance.x, arena.Entrance.y);
            Vector3 eye = GridSceneKit.CellCenter(arena.Entrance.x, arena.Entrance.y + 2) + Vector3.up * 1.7f;
            float minDistance = (arena.ExtentDown + arena.ExtentUp) * k_Cell * 0.4f;
            int target = arena.Boss ? 6 : 8 + random.Next(5);

            Physics.SyncTransforms();
            var points = new List<Vector3>();
            for (int attempt = 0; attempt < 3000 && points.Count < target; attempt++)
            {
                Vector2Int cell = RandomCell(arena, random);
                if (s_Owner[cell.x, cell.y] != arena.Index + 1 || s_Map[cell.x, cell.y] != Cell.Floor)
                    continue;
                Vector3 candidate = GridSceneKit.CellCenter(cell.x, cell.y);
                if (Vector3.Distance(candidate, entry) < minDistance)
                    continue;
                if (!NavMesh.SamplePosition(candidate + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ||
                    hit.position.y > 0.5f)
                    continue;
                if (points.Exists(p => Vector3.Distance(p, hit.position) < 8f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                // Na sala do chefe (plana e sem cobertura) não dá para esconder: vale qualquer ponto longe
                if (!arena.Boss &&
                    !Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore))
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
                Vector3 toEntry = entry - position;
                toEntry.y = 0f;
                point.rotation = Quaternion.LookRotation(toEntry.sqrMagnitude > 0.01f ? toEntry : Vector3.forward);
                transforms.Add(point);
            }

            room.SpawnPoints = transforms.ToArray();
            if (points.Count < 6)
                Debug.LogWarning($"[Ícaro] {arena.Name}: só {points.Count} pontos de spawn.");
            return points.Count;
        }

        // ---------- jogador e depuração ----------

        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(GridSceneKit.CellCenter(s_CenterX, s_Clearing.yMin + 2) +
                                                    Vector3.up * 0.2f, Quaternion.identity);
        }

        // F6/F7 levam ao corredor logo antes de cada arena; F9 abre o menu
        static void AddDebugTeleport(Transform systems, BiomeZone zone)
        {
            var points = new List<Transform>();
            var zones = new List<BiomeZone>();
            var root = new GameObject("Chegadas").transform;
            root.SetParent(systems, false);
            foreach (GridArena arena in s_Arenas)
            {
                var arrival = new GameObject($"Chegada_{arena.Name}").transform;
                arrival.SetParent(root, false);
                arrival.position = GridSceneKit.CellCenter(arena.Entrance.x, arena.Entrance.y - 3) + Vector3.up * 0.2f;
                arrival.rotation = Quaternion.identity;
                points.Add(arrival);
                zones.Add(zone);
            }

            var teleport = new GameObject("Debug_Teleporte").AddComponent<ViaDebugTeleport>();
            teleport.transform.SetParent(systems, false);
            teleport.Points = points.ToArray();
            teleport.Zones = zones.ToArray();
            teleport.BiomeStarts = new[] { 0 };
        }
    }
}
