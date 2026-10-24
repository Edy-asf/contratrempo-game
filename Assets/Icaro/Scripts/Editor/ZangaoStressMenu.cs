using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Teste: 30 Zangões": em Play, solta 30 Zangões em volta do jogador e mede o FPS (ZangaoStressTest)
    public static class ZangaoStressMenu
    {
        [MenuItem("Ícaro/Teste: 30 Zangões", priority = 66)]
        static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Ícaro] Entre em Play (ex.: Ícaro > Simular sala) antes de rodar o teste de Zangões.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabBuilder.ZangaoPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Zangao.prefab não encontrado; rode Ícaro > Gerar prefabs.");
                return;
            }

            var test = new GameObject("TesteZangoes").AddComponent<ZangaoStressTest>();
            test.Prefab = prefab;
            test.Definition = CombatAssetsBuilder.Zangao;
            test.Count = 30;
        }
    }
}
