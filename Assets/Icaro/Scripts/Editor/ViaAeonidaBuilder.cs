using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;
using PropKind = Icaro.EditorTools.ScenePropKit.PropKind;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Gerar Via Aeônida": monta a Via inteira em Unity Terrain, linear ao longo de +Z.
    // Bosque (6 arenas) → Charco (5) → Ruínas (5) → Cratera (4, a última com a nave Dédalo); em cada bioma,
    // Metrônomo numa clareira no meio e arena de chefe no fim. A trilha de pedra atravessa a mata; os limites
    // são relevo, rochas, árvores densas e colliders invisíveis. A MainScene não é usada nem alterada.
    public static class ViaAeonidaBuilder
    {
        const string k_ScenePath = ViaBakes.ScenePath;
        const string k_OldNavMeshPath = IcaroAssetUtility.ScenesFolder + "/ViaAeonida_NavMesh.asset";
        const string k_GameManagerPrefab = "Assets/FPS/Prefabs/GameManager.prefab";
        const string k_LightPrefab = "Assets/FPS/Prefabs/VFX/Directional Light.prefab";
        const string k_Title = "Gerar Via Aeônida";

        // Em modo batch (linha de comando) não há quem responda às confirmações: a cena é substituída direto
        [MenuItem("Ícaro/Gerar Via Aeônida", priority = 40)]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ScenePath) &&
                !EditorUtility.DisplayDialog(k_Title, k_ScenePath + " já existe e será substituída. Continuar?",
                    "Substituir", "Cancelar"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar(k_Title, "Prefabs e configurações", 0f);
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath) ||
                    !AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.MetronomePath) ||
                    !AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabBuilder.BrotoPath))
                    IcaroPrefabBuilder.GenerateAll();
                else
                    ScenePropKit.EnsureAll();

                BiomePalettes.Current = BiomePalettes.Bosque;
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);

                ViaLayout layout = ViaLayout.Build();
                var field = new ViaTerrainBuilder.HeightField(layout);

                IcaroAssetUtility.EnsureFolder(IcaroAssetUtility.ScenesFolder);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                Transform systems = BuildSystems(scene, visuals);
                ViaTerrainBuilder.Build(layout, new GameObject("Terreno").transform);

                var arenasParent = new GameObject("Arenas").transform;
                var arenas = new List<ArenaBuilder.Result>();
                for (int i = 0; i < layout.Arenas.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(k_Title, $"Arena {i + 1} de {layout.Arenas.Count}",
                        0.45f + 0.2f * i / layout.Arenas.Count);
                    arenas.Add(ArenaBuilder.Build(scene, layout.Arenas[i], field, arenasParent));
                }

                EditorUtility.DisplayProgressBar(k_Title, "Trilha, clareiras e limites", 0.66f);
                var trail = new GameObject("Trilha").transform;
                ArenaBuilder.AddTrailWalls(trail, layout, field);
                AddTrailRocks(scene, layout, field, trail);
                AddTrailProbes(layout, field, trail);
                Dictionary<BiomeId, BiomeZone> zones = BuildClearings(scene, layout, field, trail);

                PlacePlayer(scene, layout, field);
                AddDebugTeleport(systems, layout, arenas, zones);

                if (AssetDatabase.LoadAssetAtPath<Object>(k_OldNavMeshPath))
                    AssetDatabase.DeleteAsset(k_OldNavMeshPath);
                ArenaBuilder.DeleteNavMeshFolder();
                EditorSceneManager.SaveScene(scene, k_ScenePath);

                // NavMesh por arena e, com ele pronto, os pontos de spawn escondidos e as ondas provisórias
                for (int i = 0; i < arenas.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(k_Title, $"NavMesh e spawns: arena {i + 1} de {arenas.Count}",
                        0.7f + 0.28f * i / arenas.Count);
                    ArenaBuilder.BakeNavMesh(arenas[i]);
                    ArenaBuilder.PlaceSpawns(arenas[i], field);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AddToBuildSettings();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ViaBakes.StartOcclusionBake();
            Debug.Log($"[Ícaro] Via Aeônida gerada em {k_ScenePath}. Para as luzes bakeadas: Ícaro > Bake da iluminação da Via.");
        }

        // Os mesmos objetos de sistema que a MainScene usa, mais a atmosfera do Bosque
        public static Transform BuildSystems(Scene scene, VisualSettingsBuilder.Result visuals)
        {
            var systems = new GameObject("===== SISTEMAS =====").transform;

            InstantiatePrefab(k_GameManagerPrefab, scene, systems);
            GameObject sun = InstantiatePrefab(k_LightPrefab, scene, systems);
            SceneAtmosphere.ConfigureSun(sun ? sun.GetComponentInChildren<Light>() : null, BiomePalettes.Bosque);
            SceneAtmosphere.ApplyEnvironment(BiomePalettes.Bosque, visuals.Sky, visuals.Lighting);
            SceneAtmosphere.AddGlobalVolume(systems, visuals.Volume);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(systems, false);

            var timeManager = new GameObject("TimeManager", typeof(TimeManager), typeof(TimeMultiplierHUD), typeof(EnemiesRemainingHUD));
            timeManager.transform.SetParent(systems, false);

            // Hit marker, números de dano, sons de acerto e tremor da câmera
            InstantiatePrefab(CombatFeedbackBuilder.FeedbackPath, scene, systems);
            return systems;
        }

        // Player do Ícaro na clareira de queda, olhando para a trilha
        static void PlacePlayer(Scene scene, ViaLayout layout, ViaTerrainBuilder.HeightField field)
        {
            ViaLayout.Node start = layout.StartNode;
            GameObject player = InstantiatePrefab(IcaroPrefabBuilder.PlayerVariantPath, scene, null);
            if (!player)
                return;

            Vector2 spot = start.Center - start.ToNext * 4f;
            player.transform.SetPositionAndRotation(ArenaBuilder.Ground(field, spot) + Vector3.up * 0.2f,
                Quaternion.LookRotation(ArenaBuilder.Flat(start.ToNext)));
        }

        // Clareiras (queda, Metrônomo, transição de bioma): limites, rochas, probes, Metrônomo e gatilhos de bioma
        static Dictionary<BiomeId, BiomeZone> BuildClearings(Scene scene, ViaLayout layout,
            ViaTerrainBuilder.HeightField field, Transform parent)
        {
            var zones = new Dictionary<BiomeId, BiomeZone>();
            int index = 0;
            foreach (ViaLayout.Node node in layout.Nodes)
            {
                if (node.Kind == ViaLayout.NodeKind.Arena)
                    continue;

                var root = new GameObject($"Clareira_{node.Kind}_{node.Biome}").transform;
                root.SetParent(parent, false);
                root.position = ArenaBuilder.Ground(field, node.Center);

                var mouths = new List<Vector2>();
                if (node.ToPrevious != Vector2.zero)
                    mouths.Add(node.ToPrevious);
                if (node.ToNext != Vector2.zero)
                    mouths.Add(node.ToNext);
                ArenaBuilder.AddRing(root, field, node.Center, node.Radius, mouths);
                ArenaBuilder.AddEdgeRocks(scene, node.Biome, field, root, node.Center, node.Radius, mouths, 500 + index++);
                ArenaBuilder.AddProbes(root, field, node.Center, node.Radius);

                if (node.Kind == ViaLayout.NodeKind.Metronome)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.MetronomePath);
                    GameObject metronome = ArenaBuilder.Instantiate(scene, prefab, root);
                    // A frente (pêndulo) fica virada para quem chega pela trilha
                    metronome.transform.SetPositionAndRotation(ArenaBuilder.Ground(field, node.Center),
                        Quaternion.LookRotation(ArenaBuilder.Flat(node.ToPrevious)));
                }

                if (node.Kind == ViaLayout.NodeKind.Start || node.Kind == ViaLayout.NodeKind.Transition)
                    zones[node.Biome] = AddBiomeZone(root, node, field);
            }

            return zones;
        }

        // Faixa de gatilho atravessando a clareira: ao passar, a atmosfera transita para o novo bioma
        static BiomeZone AddBiomeZone(Transform parent, ViaLayout.Node node, ViaTerrainBuilder.HeightField field)
        {
            BiomePalettes.Palette p = ViaLayout.PaletteOf(node.Biome);
            var zoneObject = new GameObject("Bioma_" + node.Biome);
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.SetPositionAndRotation(ArenaBuilder.Ground(field, node.Center),
                Quaternion.LookRotation(ArenaBuilder.Flat(node.ToNext)));

            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(node.Radius * 2f, 20f, 4f);
            box.center = new Vector3(0f, 6f, 0f);

            var zone = zoneObject.AddComponent<BiomeZone>();
            zone.BiomeName = p.Name;
            zone.FogColor = p.FogColor;
            zone.FogDensity = p.FogDensity;
            zone.AmbientSky = p.AmbientSky;
            zone.AmbientEquator = p.AmbientEquator;
            zone.AmbientGround = p.AmbientGround;
            zone.SunColor = p.SunColor;
            zone.SunIntensity = p.SunIntensity;
            return zone;
        }

        // Rochas ao longo das duas bordas da trilha (o collider invisível fica logo atrás delas)
        static void AddTrailRocks(Scene scene, ViaLayout layout, ViaTerrainBuilder.HeightField field, Transform parent)
        {
            var rocks = new GameObject("Rochas_Trilha").transform;
            rocks.SetParent(parent, false);

            for (int t = 0; t < layout.Trails.Count; t++)
            {
                ViaLayout.Trail trail = layout.Trails[t];
                var random = new System.Random(9000 + t);
                Vector2 direction = trail.Direction;
                var normal = new Vector2(-direction.y, direction.x);

                for (float s = 4f; s < trail.Length - 4f; s += 9f + (float)random.NextDouble() * 4f)
                {
                    float side = random.NextDouble() < 0.5 ? -1f : 1f;
                    Vector2 point = trail.A + direction * s + normal * side * (6.8f + (float)random.NextDouble() * 1.5f);
                    PropKind kind = random.NextDouble() < 0.25 ? PropKind.RochaGrande : PropKind.RochaMedia;

                    GameObject rock = ArenaBuilder.Instantiate(scene, ScenePropKit.Get(trail.Biome, kind), rocks);
                    rock.transform.SetPositionAndRotation(ArenaBuilder.Ground(field, point),
                        Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    rock.transform.localScale *= 0.8f + (float)random.NextDouble() * 0.4f;
                    ArenaBuilder.SetStatic(rock, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                                 StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
                }
            }
        }

        // Light probes esparsas ao longo das trilhas, para quem passa por elas pegar a cor do lugar
        static void AddTrailProbes(ViaLayout layout, ViaTerrainBuilder.HeightField field, Transform parent)
        {
            var positions = new List<Vector3>();
            foreach (ViaLayout.Trail trail in layout.Trails)
            {
                for (float s = 0f; s <= trail.Length; s += 12f)
                {
                    Vector2 p = trail.A + trail.Direction * s;
                    float ground = field.HeightAt(p);
                    positions.Add(new Vector3(p.x, ground + 1f, p.y));
                    positions.Add(new Vector3(p.x, ground + 4f, p.y));
                }
            }

            var probes = new GameObject("LightProbes_Trilhas").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(parent, false);
            probes.transform.position = Vector3.zero;
            probes.probePositions = positions.ToArray();
        }

        // F6/F7/F8 para pular entre arenas e biomas durante os testes
        static void AddDebugTeleport(Transform systems, ViaLayout layout, List<ArenaBuilder.Result> arenas,
            Dictionary<BiomeId, BiomeZone> zones)
        {
            var teleport = new GameObject("Debug_Teleporte").AddComponent<ViaDebugTeleport>();
            teleport.transform.SetParent(systems, false);
            teleport.Points = arenas.Select(a => a.Arrival).ToArray();
            teleport.Zones = arenas.Select(a => zones.TryGetValue(a.Plan.Biome, out BiomeZone z) ? z : null).ToArray();
            teleport.BiomeStarts = layout.Biomes.Select(b => b.Arenas[0].GlobalIndex).ToArray();
        }

        static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == k_ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(k_ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static GameObject InstantiatePrefab(string path, Scene scene, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Prefab não encontrado: " + path);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (parent)
                instance.transform.SetParent(parent, true);
            return instance;
        }
    }
}
