using UnityEditor;
using UnityEngine;

namespace Icaro
{
    // Inspector dos ajustes do céu: os campos de sempre e, no topo, um botão para voltar ao padrão cada parte
    // separada (estrelas, aurora, partículas, céu do Bosque, céu do Arquipélago), sem mexer nas outras. Os botões têm
    // desfazer.
    [CustomEditor(typeof(SkySettings))]
    public class SkySettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (SkySettings)target;
            EditorGUILayout.LabelField("Voltar ao padrão", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Estrelas"))
                    Reset(settings, "estrelas", settings.ResetStars);
                if (GUILayout.Button("Aurora"))
                    Reset(settings, "aurora", settings.ResetAurora);
                if (GUILayout.Button("Partículas"))
                    Reset(settings, "partículas", settings.ResetSpores);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Céu do Bosque"))
                    Reset(settings, "céu do Bosque", settings.ResetBosque);
                if (GUILayout.Button("Céu do Arquipélago"))
                    Reset(settings, "céu do Arquipélago", settings.ResetArquipelago);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        void Reset(SkySettings settings, string part, System.Action reset)
        {
            Undo.RecordObject(settings, "Voltar " + part + " ao padrão");
            reset();
            serializedObject.Update();
        }
    }
}
