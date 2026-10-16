using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Icaro.EditorTools
{
    // Bakes da Via Aeônida. O occlusion culling roda em segundo plano logo depois de gerar a cena.
    // O bake da iluminação (lightmaps, light probes, reflection probes) é longo para um mapa deste tamanho,
    // então fica num menu próprio: sem ele a cena continua legível (sol + ambiente em tempo real).
    public static class ViaBakes
    {
        public const string ScenePath = IcaroAssetUtility.ScenesFolder + "/ViaAeonida.unity";

        public static void StartOcclusionBake()
        {
            if (StaticOcclusionCulling.isRunning)
                StaticOcclusionCulling.Cancel();

            if (StaticOcclusionCulling.GenerateInBackground())
                Debug.Log("[Ícaro] Bake do occlusion culling iniciado em segundo plano (barra no canto inferior direito).");
            else
                Debug.LogWarning("[Ícaro] O occlusion culling não pôde começar. Use Window > Rendering > Occlusion Culling > Bake.");
        }

        [MenuItem("Ícaro/Bake da iluminação da Via", priority = 41)]
        static void BakeLighting()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Bake da iluminação",
                    "Abra a cena ViaAeonida (Assets/Icaro/Scenes) antes de fazer o bake.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Bake da iluminação",
                    "O bake de lightmaps e probes da Via inteira leva vários minutos. Começar agora?",
                    "Começar", "Cancelar"))
                return;

            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            if (!Lightmapping.BakeAsync())
            {
                Lightmapping.bakeCompleted -= OnBakeCompleted;
                Debug.LogWarning("[Ícaro] O bake não pôde começar. Use Window > Rendering > Lighting > Generate Lighting.");
            }
        }

        // Ao terminar, salva a cena: ela passa a referenciar os dados de iluminação gerados
        static void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ícaro] Bake da iluminação da Via Aeônida concluído.");
        }
    }
}
