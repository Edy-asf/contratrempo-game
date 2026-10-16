using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Testar a partir de": abre a Via Aeônida e entra em Play já na primeira arena do bioma escolhido.
    // Em Play, teleporta na hora. Dentro do jogo, F9 abre o mesmo atalho (ViaDebugTeleport).
    public static class ViaTestMenu
    {
        const string k_Menu = "Ícaro/Testar a partir de/";

        [MenuItem(k_Menu + "Bosque", priority = 60)]
        static void Bosque() => Start(0);

        [MenuItem(k_Menu + "Charco", priority = 61)]
        static void Charco() => Start(1);

        [MenuItem(k_Menu + "Ruínas", priority = 62)]
        static void Ruinas() => Start(2);

        [MenuItem(k_Menu + "Cratera", priority = 63)]
        static void Cratera() => Start(3);

        static void Start(int biome)
        {
            if (EditorApplication.isPlaying)
            {
                var teleport = Object.FindAnyObjectByType<ViaDebugTeleport>();
                if (teleport)
                    teleport.GoToBiome(biome);
                else
                    Debug.LogWarning("[Ícaro] Esta cena não tem o teleporte de teste (ViaDebugTeleport).");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != ViaBakes.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(ViaBakes.ScenePath);
            }

            PlayerPrefs.SetInt(ViaDebugTeleport.StartBiomeKey, biome);
            PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }
    }
}
