using UnityEditor;
using UnityEngine;

namespace Icaro
{
    // Ajustes das habilidades ativas (AbilitySettings) e um atalho de teste que dá as duas em Play
    public static class AbilitySettingsMenu
    {
        [MenuItem("Ícaro/Ajustes das habilidades", priority = 61)]
        static void Select()
        {
            AbilitySettings settings = AbilitySettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Ícaro/Teste: dar as habilidades", priority = 62)]
        static void GiveAll()
        {
            PlayerAbilities abilities = PlayerAbilities.Instance;
            if (!abilities)
            {
                Debug.LogWarning("Ícaro: entre em Play com o jogador na cena para dar as habilidades.");
                return;
            }

            abilities.Give(AbilityType.TimeRewind);
            abilities.Give(AbilityType.TimeSlow);
            Debug.Log("Ícaro: habilidades dadas (Voltar no tempo e Câmera lenta).");
        }

        [MenuItem("Ícaro/Teste: dar as habilidades", true)]
        static bool CanGiveAll() => Application.isPlaying;
    }
}
