using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Recompensas depois de cada chefe e Cronitas da run. Lojas e Metrônomos só aparecem depois de um chefe: os
    // Metrônomos que a Via tem no meio de cada bioma ficam escondidos (um deles vira o modelo), e quando um chefe é
    // derrotado surgem na arena dele uma loja e um Metrônomo (de uso único). Também solta as Cronitas dos inimigos
    // (RoomManager). Tudo em jogo, sem precisar regenerar a Via. Valores e prefabs no ShopSettings (CombatBalance.Shop). O PlayerStats cria este componente.
    public class ShopDirector : MonoBehaviour
    {
        static ShopDirector s_Instance;
        static ShopSettings s_Fallback;

        ShopSettings m_Settings;
        Metronome m_MetronomeTemplate;

        // Ficha da loja (padrão, se a cena não tiver uma ligada)
        public static ShopSettings Settings
        {
            get
            {
                if (s_Instance && s_Instance.m_Settings)
                    return s_Instance.m_Settings;
                if (!s_Fallback)
                    s_Fallback = ScriptableObject.CreateInstance<ShopSettings>();
                return s_Fallback;
            }
        }

        public static void EnsureExists(ShopSettings settings)
        {
            if (s_Instance)
                return;
            var director = new GameObject("Lojas").AddComponent<ShopDirector>();
            director.m_Settings = settings;
            s_Instance = director;
        }

        void Start()
        {
            if (!FindAnyObjectByType<ShopUI>())
                gameObject.AddComponent<ShopUI>();
            if (!FindAnyObjectByType<CronitaHUD>())
                gameObject.AddComponent<CronitaHUD>();

            // Os Metrônomos da Via só aparecem depois dos chefes: esconde todos e guarda um como modelo
            foreach (Metronome metronome in Metronome.All.ToArray())
            {
                if (!metronome)
                    continue;
                if (!m_MetronomeTemplate)
                    m_MetronomeTemplate = metronome;
                metronome.gameObject.SetActive(false);
            }

            RoomManager.OnAnyRoomCleared += OnRoomCleared;
        }

        void OnDestroy()
        {
            RoomManager.OnAnyRoomCleared -= OnRoomCleared;
            if (s_Instance == this)
                s_Instance = null;
        }

        // Depois do chefe: uma loja e um Metrônomo lado a lado no meio da arena
        void OnRoomCleared(RoomManager room)
        {
            if (!room || !room.IsBoss)
                return;
            Vector3 center = room.transform.position;
            Vector3 side = Vector3.right * Settings.OffsetFromMetronome;
            // O Metrônomo é de uso único: nasce do lado mais longe do jogador, para não ser gasto sem querer
            if (BossTarget.Exists && BossTarget.FlatDistance(center + side) > BossTarget.FlatDistance(center - side))
                side = -side;
            SpawnTerminal(center + side, "Loja do Chefe");
            SpawnMetronome(center - side);
        }

        void SpawnMetronome(Vector3 position)
        {
            if (!m_MetronomeTemplate)
                return;
            Metronome metronome = Instantiate(m_MetronomeTemplate, GroundPoint(position),
                m_MetronomeTemplate.transform.rotation);
            metronome.gameObject.SetActive(true);
        }

        static Vector3 GroundPoint(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                return hit.position;
            return GroundTelegraph.GroundPoint(position);
        }

        void SpawnTerminal(Vector3 position, string title)
        {
            ShopTerminal prefab = Settings.TerminalPrefab;
            if (!prefab)
                return;
            ShopTerminal terminal = Instantiate(prefab, GroundPoint(position), Quaternion.identity);
            terminal.Title = title;
        }

        // Cronitas de um inimigo que morreu: valor da ficha, bônus da sala, elite ou chefe
        public static void DropFor(EnemyDefinition definition, Vector3 position, int room, bool elite, bool boss)
        {
            ShopSettings settings = Settings;
            if (!settings.CrystalPrefab || !definition)
                return;

            float value = boss ? settings.BossCronitas : definition.CronitaValue;
            if (elite)
                value *= settings.EliteMultiplier;
            value *= 1f + settings.BonusPerRoom * Mathf.Max(0, room - 1);
            int total = Mathf.RoundToInt(value);
            if (boss)
            {
                // Chefe: vários punhados, para a chuva de cristais
                for (int i = 0; i < 3; i++)
                    CronitaPickup.Drop(settings.CrystalPrefab, position, total / 3 + (i == 0 ? total % 3 : 0),
                        settings);
                return;
            }

            CronitaPickup.Drop(settings.CrystalPrefab, position, total, settings);
        }
    }
}
