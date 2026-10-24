using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Montar modelos importados": cria os materiais URP/Lit dos FBX de Assets/Icaro/Models e troca
    // os placeholders pelos modelos (árvores, ruínas, Dédalo, Agulha e Golem). Cada parte só roda se o FBX existir.
    // Depois, "Ícaro > Gerar Via Aeônida" recria o Terrain (árvores) e a cena com as peças novas.
    public static class ImportedModelsMenu
    {
        [MenuItem("Ícaro/Montar modelos importados", priority = 1)]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();

            var built = new System.Collections.Generic.List<string>();
            if (TreePrefabBuilder.Build())
                built.Add("árvores");
            if (RuinPrefabBuilder.Build())
                built.Add("ruínas");
            if (ShipPrefabBuilder.Build())
                built.Add("Dédalo");
            if (WeaponVisualBuilder.Build())
                built.Add("Agulha");
            if (GolemPrefabBuilder.Build())
                built.Add("Golem");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(built.Count > 0
                ? "[Ícaro] Modelos montados: " + string.Join(", ", built) + ". Rode Ícaro > Gerar Via Aeônida para atualizar a cena."
                : "[Ícaro] Nenhum FBX encontrado em " + ModelLibrary.ModelsFolder);
        }

        // Linha de comando: Unity -batchmode -executeMethod Icaro.EditorTools.ImportedModelsMenu.BuildAllAndGenerateVia
        public static void BuildAllAndGenerateVia()
        {
            BuildAll();
            ViaAeonidaBuilder.Generate();
        }

        // Linha de comando: refaz prefabs (jogador, inimigos, assets de combate), modelos e a Via inteira
        public static void RebuildEverything()
        {
            IcaroPrefabBuilder.GenerateAll();
            BuildAllAndGenerateVia();
        }
    }
}
