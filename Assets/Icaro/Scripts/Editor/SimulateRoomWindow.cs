using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Simular sala": escolhe um índice global de sala (1 a 20) e entra em Play já na entrada dela, para
    // testar o balanceamento daquela sala (vida e dano dos inimigos escalam pelo índice) sem jogar as anteriores.
    // Em Play, teleporta na hora. Mostra também a escala da sala escolhida.
    public class SimulateRoomWindow : EditorWindow
    {
        const string k_RoomPref = "Icaro.SimularSala.Indice";

        int m_Room = 1;

        [MenuItem("Ícaro/Simular sala...", priority = 64)]
        static void Open()
        {
            var window = GetWindow<SimulateRoomWindow>(true, "Simular sala");
            window.minSize = new Vector2(300f, 150f);
            window.m_Room = EditorPrefs.GetInt(k_RoomPref, 1);
        }

        void OnGUI()
        {
            m_Room = EditorGUILayout.IntSlider("Sala (índice global)", m_Room, 1, 20);

            CombatBalance balance = CombatAssetsBuilder.Balance;
            if (balance)
            {
                EditorGUILayout.LabelField("Vida dos inimigos", $"x{balance.RoomHealthMultiplier(m_Room, false):0.00}");
                EditorGUILayout.LabelField("Dano dos inimigos", $"x{balance.RoomDamageMultiplier(m_Room, false):0.00}");
                EditorGUILayout.LabelField("Vida do chefe", $"x{balance.RoomHealthMultiplier(m_Room, true):0.00}");
            }

            GUILayout.Space(8f);
            if (GUILayout.Button(EditorApplication.isPlaying ? "Ir para a sala" : "Jogar a partir desta sala",
                    GUILayout.Height(30f)))
                Play(m_Room);
        }

        static void Play(int room)
        {
            EditorPrefs.SetInt(k_RoomPref, room);
            if (EditorApplication.isPlaying)
            {
                var teleport = Object.FindAnyObjectByType<ViaDebugTeleport>();
                if (teleport)
                    teleport.GoToRoom(room);
                else
                    Debug.LogWarning("[Ícaro] Esta cena não tem o teleporte de teste (ViaDebugTeleport).");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != SceneSystemsBuilder.ViaScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(SceneSystemsBuilder.ViaScenePath);
            }

            PlayerPrefs.SetInt(ViaDebugTeleport.StartRoomKey, room);
            PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }
    }
}
