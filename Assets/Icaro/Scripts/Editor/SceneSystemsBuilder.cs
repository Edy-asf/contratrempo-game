using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Icaro.EditorTools
{
    // Objetos de sistema comuns às cenas geradas (Via, Bosque, Arquipélago, Câmara): GameManager e luz do Microgame,
    // atmosfera base, EventSystem, TimeManager com o HUD do multiplicador e o feedback de combate
    public static class SceneSystemsBuilder
    {
        // A cena do jogo (Via em grade: Bosque, desfiladeiro e Arquipélago)
        public const string ViaScenePath = IcaroAssetUtility.ScenesFolder + "/Via_Grade.unity";

        const string k_GameManagerPrefab = "Assets/FPS/Prefabs/GameManager.prefab";
        const string k_LightPrefab = "Assets/FPS/Prefabs/VFX/Directional Light.prefab";

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

            var timeManager = new GameObject("TimeManager", typeof(TimeManager), typeof(TimeMultiplierHUD));
            timeManager.transform.SetParent(systems, false);

            // Hit marker, números de dano, sons de acerto e tremor da câmera
            InstantiatePrefab(CombatFeedbackBuilder.FeedbackPath, scene, systems);
            return systems;
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
