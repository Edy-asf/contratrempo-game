using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Atalhos de teste para percorrer a Via (só no Editor e em development build):
    // F6 = próxima arena, F7 = arena anterior, F8 = início do próximo bioma,
    // F9 = menu para ir direto a um bioma ou a uma arena.
    // O menu "Ícaro > Testar a partir de" (Editor) entra em Play já no bioma escolhido, via StartBiomeKey, e
    // "Ícaro > Simular sala" já na sala escolhida (índice global 1 a 20, que define a escala dos inimigos), via
    // StartRoomKey.
    public class ViaDebugTeleport : MonoBehaviour
    {
        // Bioma inicial pedido pelo Editor antes de entrar em Play (consumido no Start)
        public const string StartBiomeKey = "Icaro.Debug.BiomeInicial";

        // Sala inicial (1 a 20) pedida pelo Editor antes de entrar em Play (consumida no Start)
        public const string StartRoomKey = "Icaro.Debug.SalaInicial";

        [Tooltip("Pontos de chegada, em ordem: a entrada de cada arena, do lado da trilha")]
        public Transform[] Points;

        [Tooltip("Bioma de cada ponto, para aplicar a atmosfera certa ao teleportar")]
        public BiomeZone[] Zones;

        [Tooltip("Índices de Points onde começa cada bioma")]
        public int[] BiomeStarts;

        int m_Current = -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool m_MenuOpen;
        int m_MenuBiome;
        Vector2 m_Scroll;

        System.Collections.IEnumerator Start()
        {
            int biome = PlayerPrefs.GetInt(StartBiomeKey, -1);
            int room = PlayerPrefs.GetInt(StartRoomKey, -1);
            if (biome < 0 && room < 1)
                yield break;

            PlayerPrefs.DeleteKey(StartBiomeKey);
            PlayerPrefs.DeleteKey(StartRoomKey);
            // Um frame depois, com o jogador já inicializado
            yield return null;
            if (room >= 1)
                GoToRoom(room);
            else
                GoToBiome(biome);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Points == null || Points.Length == 0)
                return;

            if (keyboard.f6Key.wasPressedThisFrame)
                GoTo(Mathf.Min(m_Current + 1, Points.Length - 1));
            else if (keyboard.f7Key.wasPressedThisFrame)
                GoTo(Mathf.Max(m_Current - 1, 0));
            else if (keyboard.f8Key.wasPressedThisFrame)
                GoTo(NextBiomeStart());
            else if (keyboard.f9Key.wasPressedThisFrame)
                SetMenuOpen(!m_MenuOpen);
        }

        void SetMenuOpen(bool open)
        {
            m_MenuOpen = open;
            // Com o cursor solto, o PlayerInputHandler do Microgame ignora a mira e os tiros
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        void OnGUI()
        {
            if (!m_MenuOpen || BiomeStarts == null || BiomeStarts.Length == 0)
                return;

            const float width = 320f;
            GUILayout.BeginArea(new Rect(20f, 20f, width, Screen.height - 40f), GUI.skin.box);
            GUILayout.Label("<b>Teste: ir para</b>  (F9 fecha)", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.Space(6f);
            GUILayout.Label("Bioma (primeira arena):");
            for (int b = 0; b < BiomeStarts.Length; b++)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(BiomeName(b), GUILayout.Height(30f)))
                {
                    GoToBiome(b);
                    SetMenuOpen(false);
                }
                if (GUILayout.Toggle(m_MenuBiome == b, "arenas", GUI.skin.button, GUILayout.Width(70f), GUILayout.Height(30f)))
                    m_MenuBiome = b;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label($"Arenas do {BiomeName(m_MenuBiome)}:");
            m_Scroll = GUILayout.BeginScrollView(m_Scroll);
            int first = BiomeStarts[m_MenuBiome];
            int end = m_MenuBiome + 1 < BiomeStarts.Length ? BiomeStarts[m_MenuBiome + 1] : Points.Length;
            for (int i = first; i < end; i++)
            {
                string label = Points[i] ? Points[i].name.Replace("Chegada_", "") : $"Arena {i + 1}";
                if (GUILayout.Button(label))
                {
                    GoTo(i);
                    SetMenuOpen(false);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string BiomeName(int biome)
        {
            int index = BiomeStarts[Mathf.Clamp(biome, 0, BiomeStarts.Length - 1)];
            return Zones != null && index < Zones.Length && Zones[index] ? Zones[index].BiomeName : $"Bioma {biome + 1}";
        }
#endif

        // Vai para a entrada da sala de índice global 'room' (1 a 20)
        public void GoToRoom(int room)
        {
            if (Points != null && Points.Length > 0)
                GoTo(Mathf.Clamp(room - 1, 0, Points.Length - 1));
        }

        // Vai para a primeira arena do bioma (0 Bosque, 1 Charco, 2 Ruínas, 3 Cratera)
        public void GoToBiome(int biome)
        {
            if (BiomeStarts == null || BiomeStarts.Length == 0)
                return;
            GoTo(BiomeStarts[Mathf.Clamp(biome, 0, BiomeStarts.Length - 1)]);
        }

        int NextBiomeStart()
        {
            if (BiomeStarts != null)
            {
                foreach (int start in BiomeStarts)
                {
                    if (start > m_Current)
                        return start;
                }
            }

            return Points.Length - 1;
        }

        public void GoTo(int index)
        {
            var player = FindAnyObjectByType<PlayerCharacterController>();
            if (Points == null || index < 0 || index >= Points.Length)
                return;
            Transform target = Points[index];
            if (!player || !target)
                return;

            // O CharacterController sobrescreve a posição se estiver ligado
            var controller = player.GetComponent<CharacterController>();
            if (controller)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(target.position, target.rotation);
            if (controller)
                controller.enabled = true;

            m_Current = index;
            if (Zones != null && index < Zones.Length && Zones[index])
                Zones[index].ApplyInstant();

            DisplayMessageEvent evt = Events.DisplayMessageEvent;
            evt.Message = $"[Debug] {target.name}";
            evt.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(evt);
        }
    }
}
