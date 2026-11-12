using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Blocos do traçado de Archen Kaedra como peças próprias (pedido do usuário em 2026-11-10: os blocos cinza do
    // esboço ficavam somados numa malha só e não dava para mexer neles). Cada Block() vira um objeto com a própria malha
    // (caixa chanfrada: os lados na cantaria do tom sorteado e o topo na neve, como no CreateRuin), BoxCollider e a marca
    // dos ajustes manuais, com o pivô no meio da base. Blocos do mesmo tamanho dividem a malha
    // (Art/Grade/Malhas/ArchenBlocos). Cópias feitas à mão (Ctrl+D) são recriadas pelo nome do modelo
    // ("Bloco_<x>x<y>x<z>_<tom>", em centímetros).
    public static partial class PicosBuilder
    {
        public const string BlockFolderTag = "Bloco";
        const string k_BlockFolder = GridSceneKit.MeshFolder + "/ArchenBlocos";
        static readonly Dictionary<string, Mesh> s_BlockMeshes = new Dictionary<string, Mesh>();
        static int s_BlockCount;

        static void BeginCityBlocks()
        {
            s_BlockMeshes.Clear();
            s_BlockCount = 0;
        }

        static int Cm(float v) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(v) * 100f));

        // Um bloco do traçado: 'baseCenter' é o meio da base, 'size' nos eixos de 'rotation'
        static GameObject CityBlock(Transform parent, Vector3 baseCenter, Quaternion rotation, Vector3 size, int tone)
        {
            string model = $"Bloco_{Cm(size.x)}x{Cm(size.y)}x{Cm(size.z)}_{Mathf.Max(0, tone)}";
            GameObject go = BlockFromModel(model, parent);
            if (!go)
                return null;
            go.transform.SetPositionAndRotation(baseCenter, rotation);
            ManualAdjustmentsTool.Register(go, model);
            return go;
        }

        // Monta o bloco pelo nome do modelo (também para os ajustes manuais recriarem as cópias)
        public static GameObject BlockFromModel(string model, Transform parent)
        {
            string[] parts = model.Split('_');
            if (parts.Length != 3 || parts[0] != "Bloco" || !int.TryParse(parts[2], out int tone))
                return null;
            string[] dims = parts[1].Split('x');
            if (dims.Length != 3 || !int.TryParse(dims[0], out int cx) || !int.TryParse(dims[1], out int cy) || !int.TryParse(dims[2], out int cz))
                return null;
            if (s_Materials == null)
                return null;

            var size = new Vector3(cx, cy, cz) * 0.01f;
            var go = new GameObject(model);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = CityBlockMesh(parts[1], size);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { s_Materials.Stones[tone % s_Materials.Stones.Length], s_Materials.Snow };
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic);
            s_BlockCount++;
            return go;
        }

        // Caixa chanfrada com o pivô no meio da base: submalha 0 = lados (cantaria), 1 = faces de cima (neve)
        static Mesh CityBlockMesh(string key, Vector3 size)
        {
            if (s_BlockMeshes.TryGetValue(key, out Mesh mesh) && mesh)
                return mesh;
            var data = new MeshData();
            BlockMesh.ChamferBox(data, new Vector3(0f, size.y * 0.5f, 0f), size, Quaternion.identity, 0.1f);
            var sides = new List<int>();
            var tops = new List<int>();
            for (int i = 0; i + 2 < data.Triangles.Count; i += 3)
            {
                List<int> target = data.Normals[data.Triangles[i]].y > 0.55f ? tops : sides;
                target.Add(data.Triangles[i]);
                target.Add(data.Triangles[i + 1]);
                target.Add(data.Triangles[i + 2]);
            }

            IcaroAssetUtility.EnsureFolder(k_BlockFolder);
            string path = $"{k_BlockFolder}/Picos_Bloco_{key}.asset";
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = $"Picos_Bloco_{key}" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.Uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sides, 0);
            mesh.SetTriangles(tops, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            s_BlockMeshes[key] = mesh;
            return mesh;
        }

        // Depois dos ajustes manuais (que recriam as cópias): apaga as malhas de bloco que nenhuma peça usa mais
        static void EndCityBlocks()
        {
            if (s_BlockMeshes.Count == 0)
                return;
            var used = new HashSet<string>();
            foreach (Mesh mesh in s_BlockMeshes.Values)
                if (mesh)
                    used.Add(AssetDatabase.GetAssetPath(mesh));
            int removed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { k_BlockFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!used.Contains(path) && AssetDatabase.DeleteAsset(path))
                    removed++;
            }

            Debug.Log($"[Ícaro] Archen Kaedra: {s_BlockCount} blocos como peças próprias ({s_BlockMeshes.Count} malhas; " +
                      $"{removed} malhas sem uso apagadas).");
            s_BlockMeshes.Clear();
        }
    }
}
