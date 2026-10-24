using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Teste: todas as armas": liga o modo de teste das armas (WeaponTestMode) na cena aberta. Fora do Play,
    // entra em Play (pulando a tela de título) e liga quando o jogo começa; em Play, liga ou desliga na hora. Os alvos
    // de treino são um Cascudo perto, três Cascudos lado a lado (para atravessar e encadear), um Vagante e um Golem
    // longe (para testar alcance) e um Zangão no ar.
    [InitializeOnLoad]
    public static class WeaponTestMenu
    {
        const string k_Pending = "Icaro.TesteDeArmas.Pendente";

        static WeaponTestMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Ícaro/Teste: todas as armas", priority = 67)]
        static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Toggle();
                return;
            }

            if (!WeaponAssetsBuilder.Catalog)
            {
                Debug.LogError("[Ícaro] Catálogo de armas não encontrado; rode Ícaro > Gerar armas.");
                return;
            }

            SessionState.SetBool(k_Pending, true);
            GameMenus.SkipTitleOnce = true;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(k_Pending, false))
                return;
            SessionState.SetBool(k_Pending, false);
            Toggle();
        }

        static void Toggle()
        {
            var existing = Object.FindAnyObjectByType<WeaponTestMode>();
            if (existing)
            {
                Object.Destroy(existing.gameObject);
                Debug.Log("[Ícaro] Modo de teste de armas desligado.");
                return;
            }

            var test = new GameObject("TesteDeArmas").AddComponent<WeaponTestMode>();
            test.Catalog = WeaponAssetsBuilder.Catalog;
            test.Targets = new[]
            {
                Target(EnemyPrefabBuilder.CascudoPath, CombatAssetsBuilder.Cascudo, new Vector3(0f, 0f, 8f)),
                Target(EnemyPrefabBuilder.CascudoPath, CombatAssetsBuilder.Cascudo, new Vector3(-3f, 0f, 15f)),
                Target(EnemyPrefabBuilder.CascudoPath, CombatAssetsBuilder.Cascudo, new Vector3(0f, 0f, 16f)),
                Target(EnemyPrefabBuilder.CascudoPath, CombatAssetsBuilder.Cascudo, new Vector3(3f, 0f, 17f)),
                Target(EnemyPrefabBuilder.VagantePath, CombatAssetsBuilder.Vagante, new Vector3(-7f, 0f, 25f)),
                Target(GolemPrefabBuilder.GolemPath, CombatAssetsBuilder.Golem, new Vector3(6f, 0f, 40f)),
                Target(EnemyPrefabBuilder.ZangaoPath, CombatAssetsBuilder.Zangao, new Vector3(8f, 5f, 18f), true),
            };
        }

        static WeaponTestMode.Target Target(string prefabPath, EnemyDefinition definition, Vector3 offset,
            bool flying = false) => new WeaponTestMode.Target
        {
            Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
            Definition = definition,
            Offset = offset,
            Flying = flying,
        };
    }
}
