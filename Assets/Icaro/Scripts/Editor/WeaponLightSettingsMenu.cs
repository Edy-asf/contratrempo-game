using UnityEditor;

namespace Icaro
{
    // Seleciona o asset de ajustes da luz da arma (WeaponLightSettings) no Inspector, criando se não existir
    public static class WeaponLightSettingsMenu
    {
        [MenuItem("Ícaro/Ajustes da luz da arma", priority = 61)]
        static void Select()
        {
            WeaponLightSettings settings = WeaponLightSettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
