using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;
using PropKind = Icaro.EditorTools.ScenePropKit.PropKind;

namespace Icaro.EditorTools
{
    // Um Terrain por bioma, lado a lado ao longo de +Z. A altura é uma função contínua do mundo (HeightAt),
    // então as emendas entre tiles batem: trilha, clareiras e arenas em nível base, montes dentro das
    // arenas e encostas com ruído subindo a partir da borda caminhável, que são o "muro" natural da Via.
    public static class ViaTerrainBuilder
    {
        public const float BaseHeight = 15f;
        public const float TerrainHeight = 60f;
        public const float MinX = -130f;
        public const float Width = 260f;

        const int k_HeightmapResolution = 1025;
        const int k_AlphamapResolution = 1024;
        const int k_DetailResolution = 1024;
        const float k_TreeSpacing = 6f;

        const string k_TerrainFolder = IcaroAssetUtility.Root + "/Terreno";
        const string k_TextureFolder = IcaroAssetUtility.Root + "/Textures/Terreno";
        const string k_TerrainMaterialPath = IcaroAssetUtility.MaterialsFolder + "/Terreno_URP.mat";

        // Camadas de textura, na ordem do splatmap
        static readonly string[] k_LayerNames = { "Chao", "Trilha", "Arena", "Encosta" };

        // ---------- altura ----------

        // Consulta de altura restrita aos nós/trilhas perto de uma faixa de Z (mesmo resultado, mais rápida)
        public class HeightField
        {
            readonly List<ViaLayout.Node> m_Nodes = new List<ViaLayout.Node>();
            readonly List<ViaLayout.Trail> m_Trails = new List<ViaLayout.Trail>();
            readonly List<ViaLayout.ArenaPlan> m_Arenas = new List<ViaLayout.ArenaPlan>();

            // Além desta distância da área caminhável o relevo já saturou, então os nós longe não mudam nada
            const float k_Margin = 160f;

            public HeightField(ViaLayout layout, float zMin = float.MinValue, float zMax = float.MaxValue)
            {
                foreach (ViaLayout.Node node in layout.Nodes)
                {
                    if (node.Center.y + node.Radius >= zMin - k_Margin && node.Center.y - node.Radius <= zMax + k_Margin)
                    {
                        m_Nodes.Add(node);
                        if (node.Arena != null)
                            m_Arenas.Add(node.Arena);
                    }
                }

                foreach (ViaLayout.Trail trail in layout.Trails)
                {
                    if (Mathf.Max(trail.A.y, trail.B.y) >= zMin - k_Margin && Mathf.Min(trail.A.y, trail.B.y) <= zMax + k_Margin)
                        m_Trails.Add(trail);
                }
            }

            public float WalkableDistance(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (ViaLayout.Node node in m_Nodes)
                    best = Mathf.Min(best, Vector2.Distance(p, node.Center) - node.Radius);
                foreach (ViaLayout.Trail trail in m_Trails)
                    best = Mathf.Min(best, ViaLayout.DistanceToSegment(p, trail.A, trail.B) - ViaLayout.TrailHalfWidth);
                return best;
            }

            public float TrailCenterDistance(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (ViaLayout.Trail trail in m_Trails)
                    best = Mathf.Min(best, ViaLayout.DistanceToSegment(p, trail.A, trail.B));
                return best;
            }

            // Distância com sinal até a borda da arena mais próxima e se o ponto está numa clareira
            public float ArenaDistance(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (ViaLayout.ArenaPlan arena in m_Arenas)
                    best = Mathf.Min(best, Vector2.Distance(p, arena.Node.Center) - arena.Node.Radius);
                return best;
            }

            public float ClearingDistance(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (ViaLayout.Node node in m_Nodes)
                {
                    if (node.Arena == null)
                        best = Mathf.Min(best, Vector2.Distance(p, node.Center) - node.Radius);
                }

                return best;
            }

            float MoundHeight(Vector2 p)
            {
                float height = 0f;
                foreach (ViaLayout.ArenaPlan arena in m_Arenas)
                {
                    if ((p - arena.Node.Center).sqrMagnitude > arena.Node.Radius * arena.Node.Radius)
                        continue;

                    foreach (ViaLayout.Mound mound in arena.Mounds)
                    {
                        float d = Vector2.Distance(p, mound.Position);
                        if (d < mound.Radius)
                            height = Mathf.Max(height, mound.Height * 0.5f * (1f + Mathf.Cos(Mathf.PI * d / mound.Radius)));
                    }
                }

                return height;
            }

            // Altura do terreno (y no mundo) no ponto XZ
            public float HeightAt(Vector2 p)
            {
                float d = WalkableDistance(p);
                if (d <= 0f)
                    return BaseHeight + MoundHeight(p);

                // Encosta: sobe ~13 m nos primeiros 16 m e ganha ruído mais longe da borda
                float rise = 13f * Mathf.SmoothStep(0f, 1f, d / 16f);
                float rough = (Fbm(p * 0.03f) - 0.5f) * 10f * Mathf.SmoothStep(0f, 1f, (d - 4f) / 30f);
                return BaseHeight + Mathf.Max(0f, rise + rough);
            }
        }

        // Ruído fractal simples (3 oitavas de Perlin), 0..1
        public static float Fbm(Vector2 p)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float frequency = 1f;
            for (int i = 0; i < 3; i++)
            {
                sum += amplitude * Mathf.PerlinNoise(p.x * frequency + 37.1f, p.y * frequency + 91.7f);
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }

            return sum / 0.875f;
        }

        // ---------- montagem ----------

        public static List<Terrain> Build(ViaLayout layout, Transform parent)
        {
            IcaroAssetUtility.EnsureFolder(k_TerrainFolder);
            Material terrainMaterial = GetTerrainMaterial();

            var terrains = new List<Terrain>();
            for (int i = 0; i < layout.Biomes.Count; i++)
            {
                ViaLayout.BiomePlan biome = layout.Biomes[i];
                EditorUtility.DisplayProgressBar("Gerar Via Aeônida", $"Terreno: {biome.Id}", 0.1f + 0.3f * i / layout.Biomes.Count);
                terrains.Add(BuildTile(layout, biome, parent, terrainMaterial));
            }

            return terrains;
        }

        static Terrain BuildTile(ViaLayout layout, ViaLayout.BiomePlan biome, Transform parent, Material material)
        {
            float length = biome.ZEnd - biome.ZStart;
            var field = new HeightField(layout, biome.ZStart, biome.ZEnd);

            string dataPath = $"{k_TerrainFolder}/ViaAeonida_{biome.Id}.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath))
                AssetDatabase.DeleteAsset(dataPath);

            var data = new TerrainData
            {
                heightmapResolution = k_HeightmapResolution,
                alphamapResolution = k_AlphamapResolution,
            };
            data.size = new Vector3(Width, TerrainHeight, length);
            AssetDatabase.CreateAsset(data, dataPath);

            SetHeights(data, field, biome.ZStart, length);
            data.terrainLayers = GetLayers(biome.Id, biome.Palette);
            PaintLayers(data, field, biome.ZStart, length);
            PlantTrees(data, field, biome, length);
            PlaceDetails(data, field, biome, length);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = $"Terreno_{biome.Id}";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(MinX, 0f, biome.ZStart);

            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = true;
            terrain.groupingID = 7;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 150f;
            terrain.treeDistance = 120f;
            terrain.treeBillboardDistance = 100f;
            terrain.detailObjectDistance = 50f;
            terrain.detailObjectDensity = 1f;

            // Terreno grande: menos resolução no lightmap (o padrão tornaria o bake muito pesado)
            var so = new SerializedObject(terrain);
            SerializedProperty scaleInLightmap = so.FindProperty("m_ScaleInLightmap");
            if (scaleInLightmap != null)
            {
                scaleInLightmap.floatValue = 0.25f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObjectUtility.SetStaticEditorFlags(terrainObject,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);

            EditorUtility.SetDirty(data);
            return terrain;
        }

        static void SetHeights(TerrainData data, HeightField field, float zStart, float length)
        {
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            {
                float z = zStart + length * iz / (res - 1);
                for (int ix = 0; ix < res; ix++)
                {
                    float x = MinX + Width * ix / (res - 1);
                    heights[iz, ix] = field.HeightAt(new Vector2(x, z)) / TerrainHeight;
                }
            }

            data.SetHeights(0, 0, heights);
        }

        // Pintura: pedra na trilha (e no miolo das clareiras), chão batido nas arenas, rocha nas encostas
        static void PaintLayers(TerrainData data, HeightField field, float zStart, float length)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, k_LayerNames.Length];
            for (int iz = 0; iz < res; iz++)
            {
                float v = (iz + 0.5f) / res;
                float z = zStart + length * v;
                for (int ix = 0; ix < res; ix++)
                {
                    float u = (ix + 0.5f) / res;
                    var p = new Vector2(MinX + Width * u, z);
                    float breakup = (Fbm(p * 0.25f) - 0.5f) * 2f;

                    float trail = 1f - Mathf.SmoothStep(0f, 1f, (field.TrailCenterDistance(p) + breakup - 3.2f) / 1.8f);
                    float clearing = 1f - Mathf.SmoothStep(0f, 1f, (field.ClearingDistance(p) + 4f + breakup * 2f) / 4f);
                    trail = Mathf.Max(trail, clearing * 0.75f);

                    float arena = 1f - Mathf.SmoothStep(0f, 1f, (field.ArenaDistance(p) + 3f + breakup * 2f) / 5f);
                    float slope = Mathf.SmoothStep(0f, 1f, (data.GetSteepness(u, v) - 28f) / 14f);

                    float wTrail = Mathf.Clamp01(trail);
                    float wArena = Mathf.Clamp01(arena) * (1f - wTrail);
                    float wSlope = slope * (1f - wTrail) * (1f - wArena * 0.5f);
                    float wFloor = Mathf.Max(0f, 1f - wTrail - wArena - wSlope);
                    float sum = wTrail + wArena + wSlope + wFloor;

                    maps[iz, ix, 0] = wFloor / sum;
                    maps[iz, ix, 1] = wTrail / sum;
                    maps[iz, ix, 2] = wArena / sum;
                    maps[iz, ix, 3] = wSlope / sum;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        // Mata densa fora da área caminhável: grade com jitter, árvores do Terrain (instanciadas)
        static void PlantTrees(TerrainData data, HeightField field, ViaLayout.BiomePlan biome, float length)
        {
            GameObject[] variants = ScenePropKit.TreeVariants(biome.Id);
            var prototypes = new TreePrototype[variants.Length];
            for (int i = 0; i < variants.Length; i++)
                prototypes[i] = new TreePrototype { prefab = variants[i] };
            data.treePrototypes = prototypes;

            var random = new System.Random(4242 + (int)biome.Id);
            // Sorteio da variação separado, para não mudar a distribuição das árvores
            var variantRandom = new System.Random(77 + (int)biome.Id);
            var trees = new List<TreeInstance>();
            for (float z = biome.ZStart + 2f; z < biome.ZEnd - 2f; z += k_TreeSpacing)
            for (float x = MinX + 2f; x < MinX + Width - 2f; x += k_TreeSpacing)
            {
                var p = new Vector2(x + Jitter(random, 2.5f), z + Jitter(random, 2.5f));
                float d = field.WalkableDistance(p);
                if (d < 4.5f)
                    continue;
                if (random.NextDouble() > (d < 25f ? 0.92f : 0.72f))
                    continue;

                float scale = 0.8f + (float)random.NextDouble() * 0.6f;
                trees.Add(new TreeInstance
                {
                    position = new Vector3((p.x - MinX) / Width, 0f, (p.y - biome.ZStart) / length),
                    heightScale = scale,
                    widthScale = scale * (0.9f + (float)random.NextDouble() * 0.2f),
                    rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                    prototypeIndex = variantRandom.Next(variants.Length),
                });
            }

            data.SetTreeInstances(trees.ToArray(), true);
        }

        // Arbustos na borda da mata e brotos luminosos esparsos; detalhes com GPU instancing, nunca na trilha
        static void PlaceDetails(TerrainData data, HeightField field, ViaLayout.BiomePlan biome, float length)
        {
            data.SetDetailResolution(k_DetailResolution, 16);
            data.detailPrototypes = new[]
            {
                DetailFor(ScenePropKit.Get(biome.Id, PropKind.Arbusto), 0.6f, 1.4f, 0.4f, 0.9f),
                DetailFor(ScenePropKit.Get(biome.Id, PropKind.BrotoLuminoso), 0.15f, 0.25f, 0.25f, 0.45f),
            };

            int res = k_DetailResolution;
            var bushes = new int[res, res];
            var sprouts = new int[res, res];
            var random = new System.Random(777 + (int)biome.Id);

            for (int iz = 0; iz < res; iz++)
            {
                float z = biome.ZStart + length * (iz + 0.5f) / res;
                for (int ix = 0; ix < res; ix++)
                {
                    // Amostragem esparsa primeiro, para só calcular distâncias onde pode haver detalhe
                    double roll = random.NextDouble();
                    if (roll > 0.07)
                        continue;

                    var p = new Vector2(MinX + Width * (ix + 0.5f) / res, z);
                    float d = field.WalkableDistance(p);
                    if (d < -6f || d > 30f || field.TrailCenterDistance(p) < 6.5f)
                        continue;

                    if (roll < 0.012 && d > -3f && d < 14f)
                        sprouts[iz, ix] = 1;
                    else if (roll < (d < 8f ? 0.07 : 0.035))
                        bushes[iz, ix] = 1;
                }
            }

            data.SetDetailLayer(0, 0, 0, bushes);
            data.SetDetailLayer(0, 0, 1, sprouts);
        }

        static DetailPrototype DetailFor(GameObject prefab, float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            return new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                noiseSpread = 0.4f,
                healthyColor = Color.white,
                dryColor = Color.white,
            };
        }

        static float Jitter(System.Random random, float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;

        // ---------- materiais e camadas ----------

        static Material GetTerrainMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_TerrainMaterialPath);
            if (material)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Terreno_URP", enableInstancing = true };
            AssetDatabase.CreateAsset(material, k_TerrainMaterialPath);
            return material;
        }

        // TerrainLayers por bioma; só são criadas se faltarem, para preservar texturas trocadas à mão
        static TerrainLayer[] GetLayers(BiomeId biome, BiomePalettes.Palette p)
        {
            Color[] colors = { p.Floor * 0.9f, p.Stone * 1.1f, Color.Lerp(p.Floor, p.Stone, 0.4f), p.Stone * 0.8f };
            float[] tiles = { 6f, 3f, 6f, 8f };

            string folder = $"{k_TerrainFolder}/{biome}";
            IcaroAssetUtility.EnsureFolder(folder);

            var layers = new TerrainLayer[k_LayerNames.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                string path = $"{folder}/{k_LayerNames[i]}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!layer)
                {
                    layer = new TerrainLayer
                    {
                        diffuseTexture = GetNoiseTexture($"{biome}_{k_LayerNames[i]}", colors[i], cobbles: i == 1),
                        tileSize = new Vector2(tiles[i], tiles[i]),
                        smoothness = p.Wetness * 0.6f,
                        metallic = 0f,
                    };
                    AssetDatabase.CreateAsset(layer, path);
                }

                layers[i] = layer;
            }

            return layers;
        }

        // Textura de ruído tingida (placeholder). Na trilha, um padrão de pedras (células com junta escura).
        static Texture2D GetNoiseTexture(string name, Color tint, bool cobbles)
        {
            IcaroAssetUtility.EnsureFolder(k_TextureFolder);
            string path = $"{k_TextureFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing)
                return existing;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2(x, y) / size;
                // Ruído periódico (amostra num círculo) para a textura repetir sem costura
                float n = TileableNoise(uv, 6f) * 0.6f + TileableNoise(uv, 24f) * 0.4f;
                float shade = 0.75f + 0.5f * n;

                if (cobbles)
                {
                    Vector2 cell = uv * 6f;
                    var fractional = new Vector2(cell.x - Mathf.Floor(cell.x), cell.y - Mathf.Floor(cell.y));
                    // Fileiras desencontradas, como pedras de calçamento
                    if (((int)Mathf.Floor(cell.y) & 1) == 1)
                        fractional.x = (fractional.x + 0.5f) % 1f;
                    float edge = Mathf.Min(Mathf.Min(fractional.x, 1f - fractional.x), Mathf.Min(fractional.y, 1f - fractional.y));
                    shade *= Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0f, 1f, edge / 0.08f));
                }

                pixels[y * size + x] = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float TileableNoise(Vector2 uv, float scale)
        {
            float angleX = uv.x * Mathf.PI * 2f;
            float angleY = uv.y * Mathf.PI * 2f;
            float radius = scale / (Mathf.PI * 2f);
            float a = Mathf.PerlinNoise(Mathf.Cos(angleX) * radius + 13f, Mathf.Sin(angleX) * radius + 7f);
            float b = Mathf.PerlinNoise(Mathf.Cos(angleY) * radius + 51f, Mathf.Sin(angleY) * radius + 29f);
            return (a + b) * 0.5f;
        }
    }
}
