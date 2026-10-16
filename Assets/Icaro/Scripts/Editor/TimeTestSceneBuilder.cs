using Unity.FPS.AI;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Criar cena de teste do tempo": copia a MainScene (que continua intocada) e adiciona
    // o TimeManager com HUD, o EnemyTimeScaler em todos os inimigos e um Metrônomo à frente do jogador.
    public static class TimeTestSceneBuilder
    {
        const string k_SourceScene = "Assets/FPS/Scenes/MainScene.unity";
        const string k_TargetScene = IcaroAssetUtility.ScenesFolder + "/TesteTempo.unity";
        const float k_MetronomeDistance = 8f;

        [MenuItem("Ícaro/Criar cena de teste do tempo", priority = 20)]
        static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_TargetScene))
            {
                if (!EditorUtility.DisplayDialog("Cena de teste do tempo",
                        k_TargetScene + " já existe. Substituir por uma nova cópia da MainScene?",
                        "Substituir", "Cancelar"))
                    return;

                AssetDatabase.DeleteAsset(k_TargetScene);
            }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.MetronomePath))
                IcaroPrefabBuilder.GenerateAll();

            IcaroAssetUtility.EnsureFolder(IcaroAssetUtility.ScenesFolder);
            if (!AssetDatabase.CopyAsset(k_SourceScene, k_TargetScene))
            {
                Debug.LogError("[Ícaro] Não foi possível copiar " + k_SourceScene);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(k_TargetScene, OpenSceneMode.Single);

            var timeManager = new GameObject("TimeManager");
            timeManager.AddComponent<TimeManager>();
            timeManager.AddComponent<TimeMultiplierHUD>();

            int scaledEnemies = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include))
            {
                if (!enemy.GetComponent<EnemyTimeScaler>())
                {
                    enemy.gameObject.AddComponent<EnemyTimeScaler>();
                    scaledEnemies++;
                }
            }

            PlaceMetronome(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = timeManager;

            Debug.Log($"[Ícaro] Cena de teste criada em {k_TargetScene} ({scaledEnemies} inimigos com EnemyTimeScaler).");
        }

        static void PlaceMetronome(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.MetronomePath);
            var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            if (!prefab || !player)
            {
                Debug.LogWarning("[Ícaro] Metrônomo não posicionado (prefab ou Player não encontrado).");
                return;
            }

            // Garante que os colliders da cena recém-aberta estejam no mundo físico antes dos raycasts
            Physics.SyncTransforms();

            Transform playerTransform = player.transform;
            Vector3 forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = Vector3.forward;

            // Recua se houver parede no caminho, para não enterrar o Metrônomo nela
            Vector3 chest = playerTransform.position + Vector3.up * 1.2f;
            float distance = k_MetronomeDistance;
            if (Physics.Raycast(chest, forward, out RaycastHit wallHit, k_MetronomeDistance, ~0,
                    QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(2f, wallHit.distance - 2f);

            Vector3 position = playerTransform.position + forward * distance;
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit groundHit, 20f, ~0,
                    QueryTriggerInteraction.Ignore))
                position = groundHit.point;

            var metronome = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            metronome.transform.position = position;
            metronome.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        }
    }
}
