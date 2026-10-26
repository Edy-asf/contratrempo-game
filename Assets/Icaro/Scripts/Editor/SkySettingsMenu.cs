using UnityEditor;

namespace Icaro
{
    // Seleciona o asset de ajustes do céu (SkySettings) no Inspector, criando se não existir
    public static class SkySettingsMenu
    {
        [MenuItem("Ícaro/Ajustes do céu", priority = 62)]
        static void Select()
        {
            SkySettings settings = SkySettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
