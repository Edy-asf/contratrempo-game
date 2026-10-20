using UnityEditor;

namespace Icaro
{
    // Seleciona o asset de ajustes do visor (VisorSettings) no Inspector, criando se não existir
    public static class VisorSettingsMenu
    {
        [MenuItem("Ícaro/Ajustes do visor", priority = 60)]
        static void Select()
        {
            VisorSettings settings = VisorSettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
