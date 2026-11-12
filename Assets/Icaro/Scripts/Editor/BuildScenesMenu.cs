using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Icaro.EditorTools
{
    // "Ícaro > Usar a Via como cena inicial": o jogo passa a abrir direto na Via (Via_Grade), com o menu de título do
    // Ícaro (GameMenus), e os Picos (Picos_Teste) vêm em seguida, para o portal do Arquipélago levar até eles. Cenas que
    // não existem mais saem da lista do build.
    public static class BuildScenesMenu
    {
        const string k_Via = SceneSystemsBuilder.ViaScenePath;
        const string k_Picos = IcaroAssetUtility.ScenesFolder + "/Picos_Teste.unity";

        [MenuItem("Ícaro/Usar a Via como cena inicial", priority = 70)]
        static void UseVia()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(k_Via, true) };
            if (File.Exists(k_Picos))
                scenes.Add(new EditorBuildSettingsScene(k_Picos, true));
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path != k_Via && scene.path != k_Picos && File.Exists(scene.path))
                    scenes.Add(new EditorBuildSettingsScene(scene.path, scene.enabled));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log("[Ícaro] Build: a Via é a primeira cena, e os Picos a segunda.");
        }
    }
}
