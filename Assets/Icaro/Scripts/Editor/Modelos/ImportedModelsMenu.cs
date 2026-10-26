using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Montar modelos importados": cria os materiais URP/Lit dos FBX de Assets/Icaro/Models e troca
    // os placeholders pelos modelos (Agulha, Golem e chefes). Cada parte só roda se o FBX existir.
    public static class ImportedModelsMenu
    {
        [MenuItem("Ícaro/Montar modelos importados", priority = 1)]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();

            var built = new System.Collections.Generic.List<string>();
            if (WeaponVisualBuilder.Build())
                built.Add("Agulha");
            if (GolemPrefabBuilder.Build())
                built.Add("Golem");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(built.Count > 0
                ? "[Ícaro] Modelos montados: " + string.Join(", ", built) + "."
                : "[Ícaro] Nenhum FBX encontrado em " + ModelLibrary.ModelsFolder);
        }

        // Linha de comando: refaz prefabs (jogador, inimigos, assets de combate) e modelos
        public static void RebuildEverything()
        {
            IcaroPrefabBuilder.GenerateAll();
            BuildAll();
        }
    }
}
