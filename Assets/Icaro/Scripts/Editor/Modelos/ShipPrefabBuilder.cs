using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Nave Dédalo caída e partida (Models/Dedalo/Dedalo.fbx) no lugar do placeholder Prefabs/Cenario/Comum/Nave_Dedalo.
    // O FBX traz Dedalo_LOD0/LOD1, uma malha simplificada Dedalo_Colisao (vira MeshCollider, sem renderer) e o
    // marcador Embarque, diante do vão entre as metades, que o ArenaBuilder usa para posicionar o objetivo final.
    public static class ShipPrefabBuilder
    {
        const string k_Group = "Dedalo";
        const string k_Model = "Dedalo";
        public const string BoardingMarker = "Embarque";

        static readonly float[] k_LodHeights = { 0.08f, 0.01f };

        public static bool Build()
        {
            if (!ModelLibrary.LoadModel(k_Group, k_Model))
                return false;

            ModelLibrary.BindMaterials(k_Group, k_Model);
            ConfigureMaterials();

            var root = (GameObject)PrefabUtility.InstantiatePrefab(ModelLibrary.LoadModel(k_Group, k_Model));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Nave_Dedalo";

            var lods = new LOD[k_LodHeights.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                Transform part = root.transform.Find($"{k_Model}_LOD{i}");
                lods[i] = new LOD(k_LodHeights[i], part ? part.GetComponents<Renderer>() : new Renderer[0]);
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (!lodGroup)
                lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            Transform collision = root.transform.Find($"{k_Model}_Colisao");
            if (collision)
            {
                Mesh mesh = collision.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(collision.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(collision.GetComponent<MeshFilter>());
                collision.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            IcaroAssetUtility.EnsureFolder(ScenePropKit.Folder + "/Comum");
            IcaroPrefabBuilder.SavePrefab(root, ScenePropKit.ShipPath);
            return true;
        }

        // Interior sem textura: metal escuro queimado. Fagulhas âmbar. Luzes do casco quase apagadas.
        static void ConfigureMaterials()
        {
            Material interior = ModelLibrary.GetOrCreateMaterial(k_Group, "Dedalo_Interior");
            interior.SetColor("_BaseColor", new Color(0.04f, 0.035f, 0.03f));
            interior.SetFloat("_Metallic", 0.6f);
            interior.SetFloat("_Smoothness", 0.25f);

            Material sparks = ModelLibrary.GetOrCreateMaterial(k_Group, "Dedalo_Brilho");
            ModelLibrary.SetEmission(sparks, null, BiomePalettes.Cratera.AccentAmber * 1.5f);

            Material hull = ModelLibrary.GetOrCreateMaterial(k_Group, "Dedalo_Casco");
            ModelLibrary.SetEmission(hull, hull.GetTexture("_EmissionMap"), Color.white * 0.6f);

            EditorUtility.SetDirty(interior);
            EditorUtility.SetDirty(sparks);
            EditorUtility.SetDirty(hull);
        }
    }
}
