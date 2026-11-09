using UnityEditor;
using UnityEngine;

namespace Icaro
{
    // Inspector do WeaponMotionSettings: no Play, botões para abaixar e levantar a arma e conferir o movimento
    [CustomEditor(typeof(WeaponMotionSettings))]
    public class WeaponMotionSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Os valores valem na hora durante o Play e ficam salvos no asset (também ao " +
                                    "recomeçar a run e depois de sair do Play).", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(WeaponSpriteHUD.TestLowered ? "Levantar a arma" : "Abaixar a arma",
                        GUILayout.Height(26f)))
                    WeaponSpriteHUD.TestLowered = !WeaponSpriteHUD.TestLowered;
                if (GUILayout.Button("Abaixar e levantar", GUILayout.Height(26f)))
                    WeaponSpriteHUD.TestCycle();
                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
                EditorGUILayout.LabelField("Os botões de teste funcionam no Play.", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            DrawDefaultInspector();
            if (Application.isPlaying)
                Repaint();
        }
    }
}
