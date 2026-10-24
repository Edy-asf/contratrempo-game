using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using PropKind = Icaro.EditorTools.ScenePropKit.PropKind;

namespace Icaro.EditorTools
{
    // Monta cada arena sobre o Terrain: cobertura (instâncias de prefab), portões que trancam, RoomManager,
    // limites invisíveis, probes, esporos e NavMesh próprio. Depois do NavMesh, escolhe de 8 a 12 pontos de
    // spawn escondidos da entrada e configura o combate da sala (cota de inimigos do CombatBalance).
    public static class ArenaBuilder
    {
        public const string NavMeshFolder = IcaroAssetUtility.ScenesFolder + "/ViaAeonida_NavMesh";

        const float k_BoundaryHeight = 30f;
        const float k_RingOffset = 4f;

        // Distância lateral das paredes invisíveis da trilha até a linha central
        const float k_TrailWallOffset = ViaLayout.TrailHalfWidth + 3.5f;
        const float k_MinSpawnSpacing = 8f;

        const StaticEditorFlags k_SolidFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                               StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
                                               StaticEditorFlags.ReflectionProbeStatic;

        // Decoração pequena: sem static batching, para o GPU instancing valer
        const StaticEditorFlags k_DecorFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic;

        public class Result
        {
            public ViaLayout.ArenaPlan Plan;
            public GameObject Root;
            public RoomManager Room;
            public NavMeshSurface Surface;
            public Transform Arrival; // ponto na trilha, antes do portão de entrada (teleporte de depuração)
        }

        // ---------- montagem (antes do NavMesh) ----------

        public static Result Build(Scene scene, ViaLayout.ArenaPlan arena, ViaTerrainBuilder.HeightField field,
            Transform parent)
        {
            ViaLayout.Node node = arena.Node;
            BiomePalettes.Palette palette = ViaLayout.PaletteOf(arena.Biome);
            string label = arena.IsBoss ? $"Chefe do {palette.Name}" : $"{palette.Name} {arena.IndexInBiome + 1}";

            var root = new GameObject($"Arena_{arena.GlobalIndex + 1:00}_{arena.Biome}{(arena.IsBoss ? "_Chefe" : "")}");
            root.transform.SetParent(parent, false);
            root.transform.position = Ground(field, node.Center);

            PlaceCover(scene, arena, field, root.transform);

            // Portões: entrada e saída (a arena final só tem entrada)
            var doors = new List<RoomDoor>();
            doors.Add(PlaceGate(scene, arena, field, root.transform, node.ToPrevious, "Entrada"));
            if (!arena.IsFinal)
                doors.Add(PlaceGate(scene, arena, field, root.transform, node.ToNext, "Saida"));

            // Tranca quando o jogador já está 10 m para dentro da borda
            var trigger = root.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 1;
            trigger.radius = node.Radius - 10f;
            trigger.height = 40f;
            trigger.center = new Vector3(0f, 10f, 0f);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = label;
            room.Doors = doors.ToArray();

            var mouths = new List<Vector2> { node.ToPrevious };
            if (!arena.IsFinal)
                mouths.Add(node.ToNext);
            AddRing(root.transform, field, node.Center, node.Radius, mouths);
            AddEdgeRocks(scene, arena.Biome, field, root.transform, node.Center, node.Radius, mouths, arena.Seed);

            AddProbes(root.transform, field, node.Center, node.Radius);
            SceneAtmosphere.AddSpores(root.transform, root.name, Ground(field, node.Center),
                Vector2.one * node.Radius * 1.6f, palette);

            if (arena.IsBoss)
            {
                var bossPoint = new GameObject("ChefeSpawn").transform;
                bossPoint.SetParent(root.transform, false);
                Vector2 spot = node.Center - node.ToPrevious * node.Radius * 0.4f;
                bossPoint.position = Ground(field, spot);
                bossPoint.rotation = Quaternion.LookRotation(Flat(node.ToPrevious));
            }

            if (arena.IsFinal)
                PlaceShip(scene, arena, field, root.transform);

            room.HighSpawnPoints = PlaceHighPoints(arena, field, root.transform);

            // NavMesh só do volume da arena: os inimigos nunca saem dela
            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            var surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 8f, 0f);
            surface.size = new Vector3(node.Radius * 2f + 8f, 40f, node.Radius * 2f + 8f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var arrival = new GameObject($"Chegada_{root.name}").transform;
            arrival.SetParent(root.transform, false);
            Vector2 outside = node.EdgePoint(node.ToPrevious) + node.ToPrevious * 6f;
            arrival.position = Ground(field, outside) + Vector3.up * 0.2f;
            arrival.rotation = Quaternion.LookRotation(Flat(-node.ToPrevious));

            return new Result { Plan = arena, Root = root, Room = room, Surface = surface, Arrival = arrival };
        }

        static void PlaceCover(Scene scene, ViaLayout.ArenaPlan arena, ViaTerrainBuilder.HeightField field, Transform root)
        {
            var cover = new GameObject("Cobertura").transform;
            cover.SetParent(root, false);

            foreach (ViaLayout.PropPlacement placement in arena.Props)
            {
                GameObject instance = Instantiate(scene, ScenePropKit.Get(arena.Biome, placement.Kind), cover);
                instance.transform.position = Ground(field, placement.Position);

                // Plataformas viram a rampa para o centro da arena; o resto gira livre
                Quaternion rotation = placement.Kind == PropKind.RuinaPlataforma
                    ? Quaternion.LookRotation(Flat(placement.Position - arena.Node.Center))
                    : Quaternion.Euler(0f, placement.Yaw, 0f);
                instance.transform.rotation = rotation;
                instance.transform.localScale *= placement.Scale;
                SetStatic(instance, ScenePropKit.IsSmallDecor(placement.Kind) ? k_DecorFlags : k_SolidFlags);
            }
        }

        // Pontos altos do Espinhento no topo dos poleiros (filho "Topo"). Valida a linha de visão do cano até o centro
        // e até a entrada; se menos de k_MinHighPoints forem válidos, eleva os poleiros bloqueados (x1,5 e x2) e
        // testa de novo. Acima de k_MaxHighPoints válidos, os excedentes ficam só como cenário.
        const int k_MinHighPoints = 3;
        const int k_MaxHighPoints = 6;

        static HighSpawnPoint[] PlaceHighPoints(ViaLayout.ArenaPlan arena, ViaTerrainBuilder.HeightField field,
            Transform root)
        {
            ViaLayout.Node node = arena.Node;
            Vector3 center = Ground(field, node.Center);
            Vector2 entryEdge = node.EdgePoint(node.ToPrevious);
            Vector3 entrance = Ground(field, entryEdge + (node.Center - entryEdge).normalized * 3f);

            var group = new GameObject("PontosAltos").transform;
            group.SetParent(root, false);

            var perches = new List<Transform>();
            var points = new List<HighSpawnPoint>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Topo" || !child.parent || child.parent.name != ScenePropKit.FileName(PropKind.Poleiro))
                    continue;

                var point = new GameObject($"PontoAlto_{points.Count + 1:00}").AddComponent<HighSpawnPoint>();
                point.transform.SetParent(group, false);
                point.ArenaFloorHeight = root.position.y;
                point.ArenaCenter = center;
                point.ArenaEntrance = entrance;
                perches.Add(child.parent);
                points.Add(point);
            }

            int valid = ValidateHighPoints(perches, points);
            foreach (float raise in new[] { 1.5f, 2f })
            {
                if (valid >= k_MinHighPoints)
                    break;
                for (int i = 0; i < points.Count; i++)
                {
                    if (!points[i].IsValid)
                        perches[i].localScale = new Vector3(perches[i].localScale.x, raise, perches[i].localScale.z);
                }
                valid = ValidateHighPoints(perches, points);
            }

            // Último recurso: tira a peça de cobertura que corta a linha de visão (nunca um poleiro)
            Transform cover = root.Find("Cobertura");
            for (int round = 0; round < 8 && valid < k_MinHighPoints && cover; round++)
            {
                Transform blocker = null;
                foreach (HighSpawnPoint point in points)
                {
                    if (point.IsValid)
                        continue;
                    blocker = CoverPiece(cover, point.FirstBlocker(point.ArenaCenter)) ??
                              CoverPiece(cover, point.FirstBlocker(point.ArenaEntrance));
                    if (blocker)
                        break;
                }

                if (!blocker)
                    break;
                Object.DestroyImmediate(blocker.gameObject);
                valid = ValidateHighPoints(perches, points);
            }

            // Mantém no máximo k_MaxHighPoints válidos; inválidos ficam (vermelhos no Gizmo) e não são usados
            int kept = 0;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (!points[i].IsValid)
                    continue;
                if (++kept > k_MaxHighPoints)
                {
                    Object.DestroyImmediate(points[i].gameObject);
                    points.RemoveAt(i);
                }
            }

            if (valid < k_MinHighPoints)
                Debug.LogWarning($"[Ícaro] {root.name}: só {valid} pontos altos com linha de visão (mínimo {k_MinHighPoints}).");
            return points.ToArray();
        }

        // Peça de cobertura (filho direto de "Cobertura") a que o collider pertence; null se não for cobertura ou
        // se for um poleiro
        static Transform CoverPiece(Transform cover, Collider collider)
        {
            if (!collider)
                return null;

            Transform piece = collider.transform;
            while (piece && piece.parent != cover)
                piece = piece.parent;
            if (!piece || piece.name == ScenePropKit.FileName(PropKind.Poleiro))
                return null;
            return piece;
        }

        static int ValidateHighPoints(List<Transform> perches, List<HighSpawnPoint> points)
        {
            for (int i = 0; i < points.Count; i++)
                points[i].transform.position = perches[i].Find("Topo").position;

            Physics.SyncTransforms();
            int valid = 0;
            foreach (HighSpawnPoint point in points)
            {
                if (point.Validate())
                    valid++;
            }

            return valid;
        }

        static RoomDoor PlaceGate(Scene scene, ViaLayout.ArenaPlan arena, ViaTerrainBuilder.HeightField field,
            Transform root, Vector2 direction, string name)
        {
            Vector2 point = arena.Node.EdgePoint(direction);
            Vector3 position = Ground(field, point);
            Quaternion rotation = Quaternion.LookRotation(Flat(direction));

            GameObject portal = Instantiate(scene, ScenePropKit.Get(arena.Biome, PropKind.Portal), root);
            portal.name = "Portal_" + name;
            portal.transform.SetPositionAndRotation(position, rotation);
            SetStatic(portal, k_SolidFlags);

            GameObject barrier = Instantiate(scene, ScenePropKit.Get(arena.Biome, PropKind.Barreira), root);
            barrier.name = "Barreira_" + name;
            barrier.transform.SetPositionAndRotation(position, rotation);
            return barrier.GetComponent<RoomDoor>();
        }

        static void PlaceShip(Scene scene, ViaLayout.ArenaPlan arena, ViaTerrainBuilder.HeightField field, Transform root)
        {
            Vector2 spot = ViaLayout.ShipSpot(arena);
            GameObject ship = Instantiate(scene, ScenePropKit.Ship(), root);
            // A rampa do placeholder fica em -Z: virada para a entrada da arena
            ship.transform.SetPositionAndRotation(Ground(field, spot), Quaternion.LookRotation(Flat(-arena.Node.ToPrevious)));
            SetStatic(ship, k_SolidFlags);

            // Objetivo do Microgame (ObjectiveReachPoint → WinScene) com trigger pequeno no pé da rampa, ou no
            // marcador Embarque do modelo (vão entre as metades da nave caída)
            Vector2 boardingSpot = spot + arena.Node.ToPrevious * 7f;
            Transform marker = ship.transform.Find(ShipPrefabBuilder.BoardingMarker);
            if (marker)
                boardingSpot = new Vector2(marker.position.x, marker.position.z);
            var goal = new GameObject("Objetivo_Embarque");
            goal.transform.SetParent(root, false);
            goal.transform.position = Ground(field, boardingSpot);

            var trigger = goal.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(5f, 3f, 3f);

            var objective = goal.AddComponent<ObjectiveReachPoint>();
            objective.Title = "Chegue à nave Dédalo";
            objective.Description = string.Empty;

            Material mark = IcaroMaterials.GetOrCreate("Via_Embarque", new Color(1f, 0.85f, 0.35f), new Color(1.6f, 1.2f, 0.3f));
            IcaroPrefabBuilder.CreatePart(PrimitiveType.Cylinder, "Marca", goal.transform, new Vector3(0f, 0.03f, 0f),
                new Vector3(3.5f, 0.02f, 3.5f), mark, false);
        }

        // ---------- limites ----------

        // Anel de colliders invisíveis em volta de uma área circular, com vãos nas bocas das trilhas
        public static void AddRing(Transform parent, ViaTerrainBuilder.HeightField field, Vector2 center, float radius,
            List<Vector2> mouths)
        {
            var ring = new GameObject("Limites").transform;
            ring.SetParent(parent, false);

            float ringRadius = radius + k_RingOffset;

            // Cada boca é um vão angular exato: o anel termina onde cruza as paredes da trilha (k_TrailWallOffset),
            // então anel e paredes se encontram sem fresta e sem invadir a trilha
            float gap = Mathf.Asin(Mathf.Clamp01(k_TrailWallOffset / ringRadius));
            var cuts = new List<(float from, float to)>();
            foreach (Vector2 mouth in mouths)
            {
                float a = Mathf.Atan2(mouth.y, mouth.x);
                cuts.Add((a - gap, a + gap));
            }

            foreach ((float from, float to) arc in FreeArcs(cuts))
            {
                float arcLength = (arc.to - arc.from) * ringRadius;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(arcLength / 4f));
                float step = (arc.to - arc.from) / pieces;
                for (int i = 0; i < pieces; i++)
                {
                    Vector2 a = center + Polar(arc.from + step * i) * ringRadius;
                    Vector2 b = center + Polar(arc.from + step * (i + 1)) * ringRadius;
                    Vector2 chord = b - a;
                    var outward = new Vector2(chord.y, -chord.x).normalized;
                    if (Vector2.Dot(outward, (a + b) * 0.5f - center) < 0f)
                        outward = -outward;

                    AddWall(ring, Ground(field, (a + b) * 0.5f), Quaternion.LookRotation(Flat(outward)), chord.magnitude + 0.3f);
                }
            }
        }

        // Arcos do círculo (em radianos) que sobram depois de tirar os vãos
        static List<(float from, float to)> FreeArcs(List<(float from, float to)> cuts)
        {
            var arcs = new List<(float from, float to)>();
            if (cuts.Count == 0)
            {
                arcs.Add((0f, Mathf.PI * 2f));
                return arcs;
            }

            cuts.Sort((x, y) => x.from.CompareTo(y.from));
            for (int i = 0; i < cuts.Count; i++)
            {
                float start = cuts[i].to;
                float end = i + 1 < cuts.Count ? cuts[i + 1].from : cuts[0].from + Mathf.PI * 2f;
                if (end > start)
                    arcs.Add((start, end));
            }

            return arcs;
        }

        static Vector2 Polar(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        // Paredes invisíveis dos dois lados de cada trilha, um pouco além da borda caminhável
        public static void AddTrailWalls(Transform parent, ViaLayout layout, ViaTerrainBuilder.HeightField field)
        {
            var walls = new GameObject("Limites_Trilhas").transform;
            walls.SetParent(parent, false);

            foreach (ViaLayout.Trail trail in layout.Trails)
            {
                Vector2 direction = trail.Direction;
                var normal = new Vector2(-direction.y, direction.x);
                Vector2 middle = (trail.A + trail.B) * 0.5f;
                // Começa 1 m antes da borda das áreas: encontra o anel (raio + 4 m) sem entrar nelas
                float length = trail.Length + 2f;

                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 point = middle + normal * side * k_TrailWallOffset;
                    AddWall(walls, Ground(field, point), Quaternion.LookRotation(Flat(normal * side)), length);
                }
            }
        }

        static void AddWall(Transform parent, Vector3 basePosition, Quaternion facing, float length)
        {
            var wall = new GameObject("Limite");
            wall.transform.SetParent(parent, false);
            wall.transform.SetPositionAndRotation(basePosition + Vector3.up * (k_BoundaryHeight * 0.5f - 4f), facing);

            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(length, k_BoundaryHeight, 1f);

            // Projéteis atravessam (não aparecem faíscas "no ar")
            wall.AddComponent<IgnoreHitDetection>();
        }

        // Rochas espalhadas na borda das áreas: deixam o limite natural (o collider invisível fica atrás)
        public static void AddEdgeRocks(Scene scene, ViaLayout.BiomeId biome, ViaTerrainBuilder.HeightField field,
            Transform parent, Vector2 center, float radius, List<Vector2> mouths, int seed)
        {
            var rocks = new GameObject("Rochas_Borda").transform;
            rocks.SetParent(parent, false);
            var random = new System.Random(seed + 99);

            float rockRadius = radius + 2f;
            int count = Mathf.CeilToInt(2f * Mathf.PI * rockRadius / 9f);
            float mouthHalfAngle = (ViaLayout.TrailHalfWidth + 6f) / rockRadius;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) * Mathf.PI * 2f / count;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (mouths.Exists(m => Vector2.Angle(m, direction) * Mathf.Deg2Rad < mouthHalfAngle))
                    continue;

                PropKind kind = random.NextDouble() < 0.4 ? PropKind.RochaGrande : PropKind.RochaMedia;
                Vector2 point = center + direction * (rockRadius + (float)random.NextDouble() * 2f);
                GameObject rock = Instantiate(scene, ScenePropKit.Get(biome, kind), rocks);
                rock.transform.SetPositionAndRotation(Ground(field, point), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                rock.transform.localScale *= 0.9f + (float)random.NextDouble() * 0.5f;
                SetStatic(rock, k_SolidFlags);
            }
        }

        // ---------- probes ----------

        // Light probes em grade de 10 m (1 m e 4 m acima do chão) e uma reflection probe do tamanho da área
        public static void AddProbes(Transform parent, ViaTerrainBuilder.HeightField field, Vector2 center, float radius)
        {
            var root = new GameObject("Probes").transform;
            root.SetParent(parent, false);
            root.position = Vector3.zero;

            var positions = new List<Vector3>();
            for (float x = -radius; x <= radius; x += 10f)
            for (float z = -radius; z <= radius; z += 10f)
            {
                var p = new Vector2(center.x + x, center.y + z);
                if (Vector2.Distance(p, center) > radius)
                    continue;

                float ground = field.HeightAt(p);
                positions.Add(new Vector3(p.x, ground + 1f, p.y));
                positions.Add(new Vector3(p.x, ground + 4f, p.y));
            }

            var probes = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(root, false);
            probes.transform.position = Vector3.zero;
            probes.probePositions = positions.ToArray();

            var reflection = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            reflection.transform.SetParent(root, false);
            reflection.transform.position = Ground(field, center);
            reflection.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            reflection.size = new Vector3(radius * 2f, 24f, radius * 2f);
            reflection.center = new Vector3(0f, 8f, 0f);
            reflection.boxProjection = true;
            reflection.resolution = 128;
            reflection.hdr = true;
        }

        // ---------- depois do NavMesh: spawns e ondas ----------

        public static void BakeNavMesh(Result arena)
        {
            IcaroAssetUtility.EnsureFolder(NavMeshFolder);
            string path = $"{NavMeshFolder}/{arena.Root.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path))
                AssetDatabase.DeleteAsset(path);

            arena.Surface.BuildNavMesh();
            if (arena.Surface.navMeshData)
                AssetDatabase.CreateAsset(arena.Surface.navMeshData, path);
            else
                Debug.LogError("[Ícaro] NavMesh vazio em " + arena.Root.name);
        }

        public static void PlaceSpawns(Result arena, ViaTerrainBuilder.HeightField field)
        {
            ViaLayout.ArenaPlan plan = arena.Plan;
            ViaLayout.Node node = plan.Node;
            var random = new System.Random(plan.Seed + 17);
            int target = 8 + random.Next(5);

            Vector2 entry = node.EdgePoint(node.ToPrevious);
            Vector2 inward = -node.ToPrevious;
            Vector3 eye = Ground(field, entry + inward * 4f) + Vector3.up * 1.7f;
            float minDistance = 0.45f * node.Radius * 2f;
            Vector2 shipSpot = ViaLayout.ShipSpot(plan);

            Physics.SyncTransforms();

            var points = new List<(Vector3 position, bool elevated)>();
            int visibleRejected = 0;
            for (int attempt = 0; attempt < 900 && points.Count < target; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)random.NextDouble()) * node.Radius * 0.88f;
                Vector2 candidate = node.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                if (Vector2.Distance(candidate, entry) < minDistance)
                    continue;
                if (plan.IsFinal && Vector2.Distance(candidate, shipSpot) < 14f)
                    continue;
                if (!NavMesh.SamplePosition(Ground(field, candidate) + Vector3.up, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    continue;

                // Só no chão ou nos montes: o NavMesh pode ter ilhas em cima de rochas, onde o inimigo ficaria preso
                if (Mathf.Abs(hit.position.y - field.HeightAt(new Vector2(hit.position.x, hit.position.z))) > 0.6f)
                    continue;
                if (points.Exists(o => Vector3.Distance(o.position, hit.position) < k_MinSpawnSpacing))
                    continue;

                // Fora da linha de visão inicial: algo (terreno, monte, cobertura) tem que bloquear o olhar
                if (!Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    visibleRejected++;
                    continue;
                }

                bool elevated = hit.position.y - ViaTerrainBuilder.BaseHeight > 1.5f;
                points.Add((hit.position, elevated));
            }

            if (points.Count < 8)
                Debug.LogWarning($"[Ícaro] {arena.Root.name}: só {points.Count} pontos de spawn escondidos " +
                                 $"({visibleRejected} candidatos visíveis da entrada). Considere mais cobertura.");

            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(arena.Root.transform, false);
            var transforms = new List<Transform>();
            for (int i = 0; i < points.Count; i++)
            {
                var point = new GameObject($"Spawn_{i + 1:00}{(points[i].elevated ? "_Alto" : "")}").transform;
                point.SetParent(spawns, false);
                point.position = points[i].position;
                Vector3 toEntry = Ground(field, entry) - points[i].position;
                toEntry.y = 0f;
                point.rotation = Quaternion.LookRotation(toEntry.sqrMagnitude > 0.01f ? toEntry : Vector3.forward);
                transforms.Add(point);
            }

            arena.Room.SpawnPoints = transforms.ToArray();
            ConfigureDirector(arena.Room, plan);
        }

        // Combate da sala (RoomManager + CombatBalance): a cota e o limite de vivos crescem pelo índice global da sala
        // (ArenaNumber). Na sala de chefe, o chefe do bioma (CombatBalance.Bosses) entra primeiro pelo ponto ChefeSpawn.
        static void ConfigureDirector(RoomManager room, ViaLayout.ArenaPlan plan)
        {
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = (int)plan.Biome;
            room.ArenaNumber = plan.GlobalIndex + 1;
            room.ArenaInBiome = plan.IndexInBiome;
            room.ArenasInBiome = plan.ArenasInBiome;
            room.IsBoss = plan.IsBoss;
            room.BossPoint = room.transform.Find("ChefeSpawn");
            room.BossEnemy = plan.IsBoss && room.Balance ? room.Balance.BossFor((int)plan.Biome) : null;
        }

        // ---------- utilidades ----------

        public static Vector3 Ground(ViaTerrainBuilder.HeightField field, Vector2 p) => new Vector3(p.x, field.HeightAt(p), p.y);

        public static Vector3 Flat(Vector2 direction) => new Vector3(direction.x, 0f, direction.y);

        public static GameObject Instantiate(Scene scene, GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(parent, true);
            return instance;
        }

        public static void SetStatic(GameObject root, StaticEditorFlags flags)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        public static void DeleteNavMeshFolder()
        {
            if (AssetDatabase.IsValidFolder(NavMeshFolder))
                AssetDatabase.DeleteAsset(NavMeshFolder);
        }
    }
}
