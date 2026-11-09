using UnityEditor;
using UnityEngine;

namespace Icaro
{
    // Seleciona o asset de ajustes do movimento da arma (WeaponMotionSettings) no Inspector, criando se não existir
    public static class WeaponMotionSettingsMenu
    {
        [MenuItem("Ícaro/Ajustes do movimento da arma", priority = 62)]
        static void Select()
        {
            WeaponMotionSettings settings = WeaponMotionSettings.CreateAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
