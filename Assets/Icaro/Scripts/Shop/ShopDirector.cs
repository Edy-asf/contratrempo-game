using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Recompensas e Cronitas da run. A loja é o Méton (MerchantNpc): ele aparece na frente do jogador depois de cada
    // chefe e das salas de ShopSettings.MerchantRooms, quando a escolha de cartas termina; num chefe com RewardPoint
    // (alto do templo do Arquipélago), aparece lá.
    // O Metrônomo saiu do jogo (o tempo volta a 1,0x ao limpar cada sala): os que a Via ainda tem nas cenas ficam
    // escondidos. Também solta as Cronitas dos inimigos
    // (RoomManager). Tudo em jogo, sem precisar regenerar a Via. Valores e prefabs no ShopSettings (CombatBalance.Shop). O PlayerStats cria este componente.
    public class ShopDirector : MonoBehaviour
    {
        static ShopDirector s_Instance;
        static ShopSettings s_Fallback;

        ShopSettings m_Settings;

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

            // Os Metrônomos que as cenas da Via ainda têm saíram do jogo: ficam escondidos
            foreach (Metronome metronome in Metronome.All.ToArray())
            {
                if (metronome)
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

        // Depois do chefe e das salas marcadas: o Méton
        void OnRoomCleared(RoomManager room)
        {
            if (!room)
                return;
            bool listed = System.Array.IndexOf(Settings.MerchantRooms ?? new int[0], room.ArenaNumber) >= 0;
            if (!room.IsBoss && !listed)
                return;
            if (room.IsBoss && room.RewardPoint)
                MerchantNpc.SpawnAt(GroundPoint(room.RewardPoint.position));
            else
                MerchantNpc.SpawnNearPlayer();
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
