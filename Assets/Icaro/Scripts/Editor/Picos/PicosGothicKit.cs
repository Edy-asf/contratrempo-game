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

        // 'model': a parede de tijolo e as escamas saem do tipo de construção (WallKind) e a moldura usa a pedra própria
        // (Vilas do Alto e Bairro Voador); sem ele, as texturas antigas. 'worn': peça do Bairro Voador (abandonado)
        public static Material MaterialFor(string name, PicosTextureKit.Set set, string model = null, bool worn = false)
        {
            bool kit = model != null && set.GothWalls != null;
            // Estragos do ataque, antes de MAT_Madeira, MAT_Pedra e MAT_Vidro (os prefixos pegariam esses nomes)
            if (name.StartsWith("MAT_Madeira_Queimada"))
                return set.CharWood ?? set.Wood;
            if (name.StartsWith("MAT_Pedra_Queimada"))
                return set.BurntStone ?? set.Dark;
            if (name.StartsWith("MAT_Chao_Queimado"))
                return set.Scorch ?? set.Dark;
            if (name.StartsWith("MAT_Vidro_Fundido"))
                return set.FusedGlass ?? set.Glass;
            if (name.StartsWith("MAT_Cristal_Apagado"))
                return set.DeadCrystal ?? set.Dark;
            if (name.StartsWith("MAT_Carbonizado"))
                return set.Carbon ?? set.Dark;
            if (name.StartsWith("MAT_Liga_Clara"))
                return set.Steel;
            if (name.StartsWith("MAT_Liga_Lavrada"))
                return set.Engraved;
            if (name.StartsWith("MAT_Pedra_Soco"))
                return set.StoneSocle;
            if (name.StartsWith("MAT_Pedra_Coluna"))
                return set.StoneColumn;
            if (name.StartsWith("MAT_Pedra_Escultura"))
                return set.StoneCarving;
            if (name.StartsWith("MAT_Pedra_Movel"))
                return set.StoneFurniture;
            if (name.StartsWith("MAT_Pedra_Gotica"))
                return kit ? set.GothWalls[WallKind(model, worn)] : set.HouseWalls[6];
            if (name.StartsWith("MAT_Pedra_Moldura"))
                return kit ? set.StoneMolding : set.Stones[1];
            if (name.StartsWith("MAT_Escamas"))
                return kit ? set.GothScales[ScaleKind(model, worn)] : set.Scales;
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
            if (name.StartsWith("MAT_Casco"))
                return set.Hull;
            if (name.StartsWith("MAT_Vidro_Nave"))
                return set.ShipGlass;
            if (name.StartsWith("MAT_Liga"))
                return set.Alloy;
            if (name.StartsWith("MAT_Luz"))
                return set.Glow;
            return null;
        }

        // Parede pelo tipo de construção (pedido do usuário em 2026-11-08: nada de sorteio; tijolos de pedra da mesma
        // família): 0 comum, 1 alternado, 2 monumental, 3 gasto. O Bairro Voador abandonado é todo gasto; igrejas,
        // catedral, campanários, torres, salões, estações, o portal e o monumento são monumentais; tudo que fica em cima
        // da Vila-Ponte (moinho, torres do portão, casas debruçadas, arcos) usa o comum; as outras casas alternam entre o
        // comum e o alternado pelo nome do modelo (cada modelo sai sempre igual)
        static readonly string[] k_Monumental =
            { "Catedral", "Capela", "Campanario", "Torre", "Salao", "Sineira", "Espiral", "Estacao", "Portal", "Monumento" };
        static readonly string[] k_BridgeVillage = { "Moinho", "Torre_Portao", "Debrucada", "Arco_Rua", "Grua" };

        static bool HasAny(string model, string[] keys)
        {
            foreach (string k in keys)
                if (model.Contains(k))
                    return true;
            return false;
        }

        static int WallKind(string model, bool worn)
        {
            model = model.Replace("_Quebrada", "");     // a casa quebrada pelo ataque fica com a parede da inteira
            if (worn)
                return 3;
            if (HasAny(model, k_BridgeVillage))
                return 0;
            if (model.StartsWith("AR_Casa_"))
                return PieceHash(model) % 2;
            if (HasAny(model, k_Monumental))
                return 2;
            return model.StartsWith("AR_") ? PieceHash(model) % 2 : 0;
        }

        // Escamas pelo mesmo tipo: monumentais em ardósia, Vila-Ponte e Bairro Voador no cinza, casas entre os três
        static int ScaleKind(string model, bool worn)
        {
            model = model.Replace("_Quebrada", "");
            if (worn || HasAny(model, k_BridgeVillage))
                return 0;
            if (model.StartsWith("AR_Casa_"))
                return PieceHash(model) / 7 % 3;
            return HasAny(model, k_Monumental) ? 1 : 0;
        }

        // Sorteio estável pelo nome da peça (o string.GetHashCode pode mudar entre versões do runtime)
        static int PieceHash(string model)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in model)
                    h = h * 31 + c;
                return h & 0x7fffffff;
            }
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
                    if (materials[i] && MaterialFor(materials[i].name, set, model, folder == Folder) is Material replacement)
                        materials[i] = replacement;
                renderer.sharedMaterials = materials;
                if (collider && renderer.TryGetComponent(out MeshFilter filter))
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            ManualAdjustmentsTool.Register(go, model);   // ajuste fino à mão (menu "Ícaro > Ajustes manuais")
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
