using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icaro.EditorTools
{
    // Kit gótico do Bairro Voador, modelado no Blender (ArteFonte/Picos/BairroVoador_Kit, scripts por fase da skill
    // blender-image-to-3d; FBX em Models/Picos/BairroVoador). Os materiais do Blender têm nomes por função
    // (MAT_Pedra_Gotica, MAT_Pedra_Moldura, MAT_Escamas_Cupula, MAT_Vitral_Violeta, MAT_Neve_Monte, MAT_Madeira_Velha,
    // MAT_Ferro_Escuro, MAT_Vao_Escuro) e viram os materiais em pixel art dos Picos ao colocar a peça; a UV vem do
    // Blender em caixa de 4 m, a mesma escala de 16 px/m das texturas do jogo. A frente das peças (o -Y do Blender)
    // chega como o +Z local no Unity (yaw 0 = frente para +Z).
    public static class PicosGothicKit
    {
        public static string Folder => IcaroAssetUtility.Root + "/Models/Picos/BairroVoador";

        // Kit das Vilas do Alto (ArteFonte/Picos/VilasDoAlto_Kit): casas góticas inteiras e as peças das praças
        public static string VillagesFolder => IcaroAssetUtility.Root + "/Models/Picos/VilasDoAlto";

        public static Material MaterialFor(string name, PicosTextureKit.Set set)
        {
            if (name.StartsWith("MAT_Pedra_Gotica"))
                return set.HouseWalls[6];
            if (name.StartsWith("MAT_Pedra_Moldura"))
                return set.Stones[1];
            if (name.StartsWith("MAT_Escamas"))
                return set.Scales;
            if (name.StartsWith("MAT_Vitral"))
                return set.Glass;
            if (name.StartsWith("MAT_Neve"))
                return set.Snow;
            if (name.StartsWith("MAT_Madeira"))
                return set.Wood;
            if (name.StartsWith("MAT_Ferro"))
                return set.Iron;
            if (name.StartsWith("MAT_Tecido_Claro"))
                return set.ClothLight;
            if (name.StartsWith("MAT_Tecido"))
                return set.Cloth;
            if (name.StartsWith("MAT_Gelo"))
                return set.Ice;
            if (name.StartsWith("MAT_Vao"))
                return set.Dark;
            if (name.StartsWith("MAT_Rocha"))
                return set.Rock;
            if (name.StartsWith("MAT_Rua"))
                return set.Cobble[0];
            if (name.StartsWith("MAT_Laje"))
                return set.Pavings[0];
            return null;
        }

        // Coloca uma peça do kit ('model' sem extensão) com a frente virada para 'yaw' graus (0 = +Z), os materiais
        // dos Picos e colisão pela própria malha
        public static GameObject Place(PicosTextureKit.Set set, string model, Vector3 position, float yaw, Transform parent,
            bool collider = true, string folder = null)
        {
            folder ??= Folder;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{model}.fbx");
            if (!prefab)
            {
                Debug.LogWarning($"[Ícaro] Kit gótico: falta {folder}/{model}.fbx (rode os scripts do Blender em ArteFonte/Picos).");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && MaterialFor(materials[i].name, set) is Material replacement)
                        materials[i] = replacement;
                renderer.sharedMaterials = materials;
                if (collider && renderer.TryGetComponent(out MeshFilter filter))
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            return go;
        }

        // Cena de prévia do kit: as casinhas (inteira, rachada, caída) e a catedral num chão de neve, com a luz do
        // eclipse e a névoa dos Picos
        [MenuItem("Ícaro/Picos/Prévia do kit do Bairro Voador")]
        public static void BuildPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PicosTextureKit.Set set = PicosTextureKit.Build();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Chao";
            ground.transform.position = new Vector3(10f, -0.5f, 8f);
            ground.transform.localScale = new Vector3(170f, 1f, 80f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = set.FloorSnow;

            // Todas as peças com a frente virada para a câmera (que fica ao sul, olhando para o norte): no fundo os
            // prédios grandes, na frente as casinhas, as torres pequenas, a ruína, o santuário e os objetos
            var root = new GameObject("Kit_BairroVoador").transform;
            (string Model, float X, float Z)[] pieces =
            {
                ("AR_TorreGotica", -58f, 22f), ("AR_Salao", -30f, 22f), ("AR_Catedral", 2f, 22f), ("AR_Sineira", 30f, 22f), ("AR_Espiral", 48f, 22f),
                ("AR_Casinha_A", -50f, 0f), ("AR_Casinha_B", -36f, -2f), ("AR_Casinha_C", -22f, 0f), ("AR_Torrezinha", -8f, 0f),
                ("AR_RuinaFrente", 10f, -4f), ("AR_Santuario", 30f, -6f), ("PR_Poste", 40f, -10f), ("PR_Carroca", 46f, -10f),
                ("PR_Obelisco", 54f, -10f), ("PR_Lapide", 60f, -10f), ("PR_Pedestal", 66f, -10f),
            };
            foreach (var piece in pieces)
                Place(set, piece.Model, new Vector3(piece.X, 0f, piece.Z), 180f, root);

            var sun = new GameObject("Eclipse").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.38f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28f, 20f, 0f);   // vem do lado da câmera (sul), descendo para o norte
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.36f, 0.48f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.68f);
            RenderSettings.fogDensity = 0.006f;

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.47f, 0.56f);
            cam.transform.SetPositionAndRotation(new Vector3(6f, 22f, -70f), Quaternion.Euler(14f, 0f, 0f));

            EditorSceneManager.SaveScene(scene, $"{IcaroAssetUtility.Root}/Scenes/Picos_KitBairro.unity");
            Debug.Log("[Ícaro] Prévia do kit do Bairro Voador pronta (cena Picos_KitBairro).");
        }
    }
}
