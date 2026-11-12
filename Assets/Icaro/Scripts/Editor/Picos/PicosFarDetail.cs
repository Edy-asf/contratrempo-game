using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Otimização da vista aberta dos Picos, sem tirar nada da composição: a cidade e as montanhas trocam por versões
    // simplificadas, trecho por trecho, conforme ficam longe (ScenicLod), e as peças pequenas somem numa distância em que já
    // não dá para vê-las (DistanceCulling). Montanhas, torres e prédios grandes nunca são cortados.
    public static partial class PicosBuilder
    {
        // Cada versão mais leve: distância em que entra, tamanho do cubo que junta os vértices e quantos passos por eixo a
        // direção da face tem (menos passos = junta faces mais tortas umas com as outras)
        struct LodTier
        {
            public string Name;
            public float Distance, Grid, DirectionSteps;

            public LodTier(string name, float distance, float grid, float directionSteps)
            {
                Name = name;
                Distance = distance;
                Grid = grid;
                DirectionSteps = directionSteps;
            }
        }

        static readonly LodTier[] k_CityTiers =
        {
            new LodTier("Media", 420f, 1.2f, 1.5f),
            new LodTier("Leve", 900f, 3f, 1.5f)
        };

        // Montanhas: tudo o que usa o material de rocha e neve, também as que forem postas depois no fundo
        static readonly LodTier[] k_MountainTiers =
        {
            new LodTier("Media", 380f, 2.5f, 1.5f),
            new LodTier("Leve", 850f, 6f, 1.5f),
            new LodTier("Silhueta", 1700f, 14f, 1f)
        };

        const float k_LodCell = 128f, k_MountainLodCell = 256f;
        const string k_MountainShader = "Icaro/SnowRock";

        static Vector3 s_CityCenter;
        static bool s_HasCity;

        static void BuildFarDetail()
        {
            var handled = new HashSet<Renderer>();
            if (s_HasCity)
                BuildCityLod(handled);
            s_HasCity = false;
            BuildMountainLod(handled);
            BuildDetailCulling(handled);
        }

        static bool MovesOrChanges(Transform t) =>
            t.GetComponentInParent<SlowRotation>(true) || t.GetComponentInParent<BrokenPortal>(true) ||
            t.GetComponentInParent<CollapsingBridge>(true) || t.GetComponentInParent<UnsafeGround>(true);

        static bool OpaqueScenery(MeshRenderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (!material || material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest)
                    return false;
            return renderer.sharedMaterials.Length > 0;
        }

        static bool AllMaterials(MeshRenderer renderer, string shader)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (!material || material.shader.name != shader)
                    return false;
            return renderer.sharedMaterials.Length > 0;
        }

        static bool HasMesh(MeshRenderer renderer) => renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh;

        // ---------- cidade ----------

        static void BuildCityLod(HashSet<Renderer> handled)
        {
            var cells = new Dictionary<Vector2Int, List<MeshRenderer>>();
            foreach (MeshRenderer renderer in s_World.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!OpaqueScenery(renderer) || !HasMesh(renderer))
                    continue;
                Bounds bounds = renderer.bounds;
                Vector3 offset = bounds.center - s_CityCenter;
                if (bounds.size.magnitude > 700f || new Vector2(offset.x, offset.z).magnitude > k_CityRadius + 60f ||
                    offset.y < -130f || offset.y > 260f || MovesOrChanges(renderer.transform))
                    continue;
                AddToCell(cells, renderer, offset, k_LodCell);
            }

            BuildLod("Cidade_Longe", "CidadeLonge", cells, k_LodCell, s_CityCenter, s_CityCenter.y, k_CityTiers, handled,
                "Cidade de longe");
        }

        // ---------- montanhas ----------

        // Os trechos de montanha que o gerador já dividiu (64 m) e os maciços do Blender entram por trecho de 256 m. Uma
        // malha só maior que 1300 m (o maciço das Vilas, em volta do caminho) fica sempre completa: um trecho é trocado
        // inteiro, e ela nunca fica toda longe. A rocha da ilha da cidade (~1080 m de diagonal) entra; a distância é até a
        // caixa dela, então em cima da cidade ela fica completa.
        static void BuildMountainLod(HashSet<Renderer> handled)
        {
            var cells = new Dictionary<Vector2Int, List<MeshRenderer>>();
            int whole = 0;
            foreach (MeshRenderer renderer in s_World.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (handled.Contains(renderer) || !renderer.enabled || !AllMaterials(renderer, k_MountainShader) ||
                    !HasMesh(renderer) || MovesOrChanges(renderer.transform))
                    continue;
                if (renderer.bounds.size.magnitude > 1300f)
                {
                    whole++;
                    continue;
                }

                AddToCell(cells, renderer, renderer.bounds.center, k_MountainLodCell);
            }

            if (whole > 0)
                Debug.Log($"[Ícaro] Montanhas de longe: {whole} malhas grandes demais para trocar por trecho ficam sempre completas.");
            BuildLod("Montanhas_Longe", "MontanhasLonge", cells, k_MountainLodCell, Vector3.zero, float.NaN, k_MountainTiers,
                handled, "Montanhas de longe");
        }

        // ---------- comum ----------

        static void AddToCell(Dictionary<Vector2Int, List<MeshRenderer>> cells, MeshRenderer renderer, Vector3 offset, float size)
        {
            var key = new Vector2Int(Mathf.FloorToInt(offset.x / size), Mathf.FloorToInt(offset.z / size));
            if (!cells.TryGetValue(key, out List<MeshRenderer> list))
                cells[key] = list = new List<MeshRenderer>();
            list.Add(renderer);
        }

        // Um trecho por vez (as versões das montanhas todas de uma vez não caberiam na memória). A origem de cada versão fica
        // no meio do trecho, na altura originY (NaN = altura do meio das peças do trecho).
        static void BuildLod(string rootName, string meshName, Dictionary<Vector2Int, List<MeshRenderer>> cells, float cellSize,
            Vector3 gridOrigin, float originY, LodTier[] tiers, HashSet<Renderer> handled, string label)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(s_World, false);
            var result = new List<ScenicLod.Cell>();
            long before = 0;
            var after = new long[tiers.Length];
            int done = 0;
            try
            {
                foreach (KeyValuePair<Vector2Int, List<MeshRenderer>> pair in cells)
                {
                    if (done++ % 8 == 0)
                        EditorUtility.DisplayProgressBar("Ícaro", $"{label}: trecho {done} de {cells.Count}", (float)done / cells.Count);

                    Vector2Int key = pair.Key;
                    var raw = new Dictionary<Material, GridSceneKit.MeshData>();
                    Bounds area = pair.Value[0].bounds;
                    foreach (MeshRenderer renderer in pair.Value)
                    {
                        area.Encapsulate(renderer.bounds);
                        handled.Add(renderer);
                        AppendWorld(raw, renderer, renderer.GetComponent<MeshFilter>().sharedMesh);
                    }

                    Vector3 origin = gridOrigin + new Vector3((key.x + 0.5f) * cellSize, 0f, (key.y + 0.5f) * cellSize);
                    origin.y = float.IsNaN(originY) ? area.center.y : originY;
                    var lists = new ScenicLod.Tier[tiers.Length];
                    for (int t = 0; t < tiers.Length; t++)
                    {
                        var list = new List<Renderer>();
                        int index = 0;
                        foreach (KeyValuePair<Material, GridSceneKit.MeshData> mesh in raw)
                        {
                            if (t == 0)
                                before += mesh.Value.Triangles.Count / 3;
                            after[t] += MakeProxy(meshName, tiers[t], mesh.Value, mesh.Key, origin, key, index++, root, list);
                        }

                        lists[t] = new ScenicLod.Tier { Renderers = list.ToArray() };
                    }

                    result.Add(new ScenicLod.Cell { Area = area, Full = pair.Value.ToArray(), Tiers = lists });
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var lod = root.gameObject.AddComponent<ScenicLod>();
            lod.Distances = new float[tiers.Length];
            for (int t = 0; t < tiers.Length; t++)
                lod.Distances[t] = tiers[t].Distance;
            lod.Cells = result.ToArray();

            var report = new System.Text.StringBuilder($"[Ícaro] {label}: {result.Count} trechos, {before} triângulos completos");
            for (int t = 0; t < tiers.Length; t++)
                report.Append($", {after[t]} na versão {tiers[t].Name} (a {tiers[t].Distance:0} m)");
            Debug.Log(report.Append('.').ToString());
        }

        static void AppendWorld(Dictionary<Material, GridSceneKit.MeshData> raw, MeshRenderer renderer, Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Color[] colors = mesh.colors;
            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            Material[] materials = renderer.sharedMaterials;
            for (int submesh = 0; submesh < mesh.subMeshCount && submesh < materials.Length; submesh++)
            {
                if (!raw.TryGetValue(materials[submesh], out GridSceneKit.MeshData data))
                    raw[materials[submesh]] = data = new GridSceneKit.MeshData();
                int start = data.Count;
                // A cor do vértice leva o relevo e a capa de neve (alfa 0 = neve) para o shader. Antes, a primeira peça de
                // um trecho nunca ganhava cor (o preenchimento só valia com vértices já postos), e uma peça sozinha no
                // trecho, como a rocha da ilha da cidade, ficava sem cor e toda de neve de longe
                bool useColors = colors.Length == vertices.Length || data.Colors.Count > 0;
                if (useColors)
                    data.PadColors();
                for (int i = 0; i < vertices.Length; i++)
                {
                    data.Vertices.Add(matrix.MultiplyPoint3x4(vertices[i]));
                    data.Normals.Add(normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up);
                    data.Uvs.Add(uvs.Length == vertices.Length ? uvs[i] : Vector2.zero);
                    if (useColors)
                        data.Colors.Add(colors.Length == vertices.Length ? colors[i] : Color.white);
                }

                foreach (int index in mesh.GetTriangles(submesh))
                    data.Triangles.Add(start + index);
            }
        }

        // Junta os vértices que caem no mesmo cubo da grade e têm a mesma direção de face (as quinas continuam vivas). Cada
        // grupo fica com um vértice de verdade, com a UV e a cor dele (a marca de neve e o relevo das montanhas vão junto),
        // então a textura não escorrega; o que encolhe a nada some.
        static GridSceneKit.MeshData Simplify(GridSceneKit.MeshData source, float size, float steps, Vector3 origin)
        {
            var result = new GridSceneKit.MeshData();
            bool colors = source.Colors.Count == source.Count && source.Count > 0;
            int half = Mathf.CeilToInt(steps);
            int span = half * 2 + 1;
            var map = new Dictionary<(int, int, int, int), int>();
            var remap = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 p = source.Vertices[i], n = source.Normals[i];
                int direction = ((Mathf.RoundToInt(n.x * steps) + half) * span + Mathf.RoundToInt(n.y * steps) + half) * span +
                                Mathf.RoundToInt(n.z * steps) + half;
                var key = (Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size), direction);
                if (!map.TryGetValue(key, out int target))
                {
                    map[key] = target = result.Count;
                    result.Vertices.Add(p - origin);
                    result.Normals.Add(n);
                    result.Uvs.Add(source.Uvs[i]);
                    result.Colors.Add(colors ? source.Colors[i] : Color.white);    // sem cor = nada (branco)
                }

                remap[i] = target;
            }

            var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i + 2 < source.Triangles.Count; i += 3)
            {
                int a = remap[source.Triangles[i]], b = remap[source.Triangles[i + 1]], c = remap[source.Triangles[i + 2]];
                if (a == b || b == c || a == c)
                    continue;
                // Mesmo triângulo com a mesma orientação, começando sempre do menor índice
                (int, int, int) id = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
                if (!seen.Add(id))
                    continue;
                result.Triangles.Add(a);
                result.Triangles.Add(b);
                result.Triangles.Add(c);
            }

            return result;
        }

        static int MakeProxy(string meshName, LodTier tier, GridSceneKit.MeshData raw, Material material, Vector3 origin,
            Vector2Int key, int index, Transform root, List<Renderer> list)
        {
            GridSceneKit.MeshData data = Simplify(raw, tier.Grid, tier.DirectionSteps, origin);
            if (data.Triangles.Count == 0)
                return 0;
            string name = $"{tier.Name}_{key.x}_{key.y}_{index}";
            GameObject go = GridSceneKit.CreateObject($"{k_Prefix}_{meshName}_{name}", name, data, material, root, false, false, false);
            go.transform.position = origin;     // na cidade, a altura da origem é a da rua: o desgaste das pedras conta a sujeira a partir dela
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            list.Add(renderer);
            return data.Triangles.Count / 3;
        }

        // ---------- peças pequenas ----------

        // Distância em que a peça some (0 = nunca)
        static float CullDistance(MeshRenderer renderer)
        {
            if (renderer.GetComponentInParent<GeneratedPiece>(true))
            {
                float size = renderer.bounds.size.magnitude;
                return size < 5f ? 220f : size < 15f ? 450f : size < 30f ? 800f : 0f;
            }

            Material material = renderer.sharedMaterial;
            string shader = material ? material.shader.name : "";
            if (shader == "Icaro/BillboardSprite")
                return 300f;
            if (shader == "Icaro/FlagCloth")
                return 250f;

            Transform top = renderer.transform;
            while (top.parent && top.parent != s_World)
                top = top.parent;
            switch (top.name)
            {
                case "Gelo": case "Madeira": case "Liga": return 350f;
                case "Ferro": return 400f;
                case "Calcada": return 300f;
                default: return 0f;
            }
        }

        static void BuildDetailCulling(HashSet<Renderer> handled)
        {
            var tiers = new SortedDictionary<float, List<Renderer>>();
            foreach (MeshRenderer renderer in s_World.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (handled.Contains(renderer) || !renderer.enabled || MovesOrChanges(renderer.transform))
                    continue;
                float distance = CullDistance(renderer);
                if (distance <= 0f)
                    continue;
                if (!tiers.TryGetValue(distance, out List<Renderer> list))
                    tiers[distance] = list = new List<Renderer>();
                list.Add(renderer);
            }

            foreach (KeyValuePair<float, List<Renderer>> tier in tiers)
            {
                var culling = new GameObject($"Culling_Detalhe_{tier.Key:0}").AddComponent<DistanceCulling>();
                culling.transform.SetParent(s_World, false);
                culling.Distance = tier.Key;
                culling.Renderers = tier.Value.ToArray();
                Debug.Log($"[Ícaro] Peças pequenas: {tier.Value.Count} somem a {tier.Key:0} m.");
            }
        }
    }
}
