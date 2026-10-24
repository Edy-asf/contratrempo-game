using System.Collections.Generic;
using UnityEditor;

namespace Icaro.EditorTools
{
    // "Ícaro > Usar a Via como cena inicial": o jogo passa a abrir direto na Via Aeônida, com o menu de título do
    // Ícaro (GameMenus). As cenas de menu, vitória e derrota do Microgame saem do build (o Ícaro tem as próprias telas).
    public static class BuildScenesMenu
    {
        const string k_Via = "Assets/Icaro/Scenes/ViaAeonida.unity";
        static readonly string[] k_Microgame =
        {
            "Assets/FPS/Scenes/IntroMenu.unity", "Assets/FPS/Scenes/WinScene.unity",
            "Assets/FPS/Scenes/LoseScene.unity", "Assets/FPS/Scenes/MainScene.unity",
        };

        [MenuItem("Ícaro/Usar a Via como cena inicial", priority = 70)]
        static void UseVia()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(k_Via, true) };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == k_Via)
                    continue;
                bool microgame = System.Array.IndexOf(k_Microgame, scene.path) >= 0;
                scenes.Add(new EditorBuildSettingsScene(scene.path, scene.enabled && !microgame));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log("[Ícaro] Build: a Via Aeônida é a primeira cena; as cenas do Microgame ficaram " +
                                  "desligadas.");
        }
    }
}
