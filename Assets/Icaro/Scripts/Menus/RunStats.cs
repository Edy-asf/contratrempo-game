using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Estatísticas da run, para a pausa e a tela de morte: tempo de jogo (sem contar pausas), salas limpas, chefes
    // derrotados, abates, maior multiplicador de tempo, dano causado e recebido, Cronitas ganhas e itens. Escuta o
    // evento central de dano e os avisos das salas e da carteira. O GameMenus cria este componente.
    public class RunStats : MonoBehaviour
    {
        public static RunStats Instance { get; private set; }

        public float PlayTime { get; private set; }
        public int RoomsCleared { get; private set; }
        public int BossesDefeated { get; private set; }
        public int Kills { get; private set; }
        public float MaxMultiplier { get; private set; } = 1f;
        public float DamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public int CronitasEarned { get; private set; }

        public int ItemsCollected
        {
            get
            {
                int count = 0;
                if (PlayerStats.Instance)
                {
                    foreach (PlayerStats.ItemStack stack in PlayerStats.Instance.Items)
                        count += stack.Count;
                }

                return count;
            }
        }

        PlayerWallet m_Wallet;

        // Números da run guardados ao trocar de cena (RunCarry)
        public class Totals
        {
            public float PlayTime, MaxMultiplier, DamageDealt, DamageTaken;
            public int RoomsCleared, BossesDefeated, Kills, CronitasEarned;
        }

        public Totals Save() => new Totals
        {
            PlayTime = PlayTime, MaxMultiplier = MaxMultiplier, DamageDealt = DamageDealt, DamageTaken = DamageTaken,
            RoomsCleared = RoomsCleared, BossesDefeated = BossesDefeated, Kills = Kills, CronitasEarned = CronitasEarned,
        };

        public void Load(Totals totals)
        {
            PlayTime = totals.PlayTime;
            MaxMultiplier = Mathf.Max(MaxMultiplier, totals.MaxMultiplier);
            DamageDealt = totals.DamageDealt;
            DamageTaken = totals.DamageTaken;
            RoomsCleared = totals.RoomsCleared;
            BossesDefeated = totals.BossesDefeated;
            Kills = totals.Kills;
            CronitasEarned = totals.CronitasEarned;
        }

        void Awake() => Instance = this;

        void OnEnable()
        {
            CombatEvents.OnDamage += OnDamage;
            RoomManager.OnAnyRoomCleared += OnRoomCleared;
        }

        void OnDisable()
        {
            CombatEvents.OnDamage -= OnDamage;
            RoomManager.OnAnyRoomCleared -= OnRoomCleared;
            if (m_Wallet)
                m_Wallet.OnCollected -= OnCollected;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            // Tempo de jogo: Time.deltaTime é zero nas pausas
            PlayTime += UnityEngine.Time.deltaTime;
            MaxMultiplier = Mathf.Max(MaxMultiplier, TimeManager.CurrentMultiplier);

            if (!m_Wallet && PlayerWallet.Instance)
            {
                m_Wallet = PlayerWallet.Instance;
                m_Wallet.OnCollected += OnCollected;
            }
        }

        void OnCollected(int amount) => CronitasEarned += amount;

        void OnDamage(DamageInfo info)
        {
            if (info.VictimIsPlayer)
            {
                DamageTaken += info.Amount;
                return;
            }

            if (!info.AttackerIsPlayer)
                return;
            DamageDealt += info.Amount;
            if (info.Killed)
                Kills++;
        }

        void OnRoomCleared(RoomManager room)
        {
            RoomsCleared++;
            if (room && room.IsBoss)
                BossesDefeated++;
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
