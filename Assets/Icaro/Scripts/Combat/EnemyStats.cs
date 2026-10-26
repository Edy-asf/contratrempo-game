using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Liga um inimigo à sua ficha (EnemyDefinition) e aplica a escala por sala (vida e dano). O RoomManager
    // chama Configure logo depois de instanciar; inimigos colocados à mão na cena usam a ficha sem escala.
    // Roda antes do Health.Start (que copia MaxHealth para a vida atual) e antes das IAs, que leem Damage daqui.
    [DefaultExecutionOrder(-100)]
    public class EnemyStats : MonoBehaviour
    {
        public EnemyDefinition Definition;

        public float HealthMultiplier { get; private set; } = 1f;
        public float DamageMultiplier { get; private set; } = 1f;

        // Chefe (ataques pesados: limite de dano por acerto maior no jogador)
        public bool IsBoss { get; private set; }

        // Sala em que nasceu (índice global, 0 = colocado à mão) e multiplicador da sucata que ele vale
        public int Room { get; private set; }
        public float ScrapMultiplier { get; private set; } = 1f;

        // Elite (modificador aplicado pelo EliteAffix) e multiplicador de movimento por cima do multiplicador de tempo
        public EliteModifier Elite { get; set; }
        public float MoveMultiplier { get; set; } = 1f;

        // Lentidão temporária (Munição Atordoante, TimeAnchorSlow): multiplica movimento e animação; 1 = normal
        public float SlowMultiplier { get; set; } = 1f;

        // Água rasa (WaterMap): mais lento enquanto anda dentro dela; 1 = fora da água
        public float WaterMultiplier { get; private set; } = 1f;

        // Dano do ataque principal já com a escala por sala
        public float Damage => (Definition ? Definition.Damage : 0f) * DamageMultiplier;

        public float AttackCooldown => Definition ? Definition.AttackCooldown : 1f;

        bool m_Applied;

        public void Configure(float healthMultiplier, float damageMultiplier, bool boss = false, int room = 0,
            float scrapMultiplier = 1f)
        {
            HealthMultiplier = healthMultiplier;
            DamageMultiplier = damageMultiplier;
            IsBoss = boss;
            Room = room;
            ScrapMultiplier = scrapMultiplier;
            Elite = EliteModifier.None;
            MoveMultiplier = 1f;
            SlowMultiplier = 1f;
            WaterMultiplier = 1f;
            Apply();
        }

        // Escala qualquer outro dano da ficha (explosão, pancada...) pela sala
        public float ScaleDamage(float baseDamage) => baseDamage * DamageMultiplier;

        void Update()
        {
            WaterMultiplier = WaterMap.Instance ? WaterMap.SpeedAt(transform.position) : 1f;
        }

        void Awake()
        {
            // Todo inimigo publica o dano que recebe no evento central
            if (GetComponent<Health>() && !GetComponent<DamageReporter>())
                gameObject.AddComponent<DamageReporter>();

            // O EnemyController do Microgame mata quem fica abaixo de uma altura fixa (-20 m); na Via o Arquipélago
            // fica abaixo disso (o mar a -26 m), então o limite passa a ser relativo a onde o inimigo nasceu
            var controller = GetComponent<EnemyController>();
            if (controller)
            {
                controller.SelfDestructYHeight = Mathf.Min(controller.SelfDestructYHeight, transform.position.y - 40f);
                // Inimigos não deixam cura (o Ícaro regenera sozinho, CombatBalance.PlayerBaseRegen)
                controller.DropRate = 0f;
            }
        }

        void Start()
        {
            if (!m_Applied)
                Apply();
        }

        void Apply()
        {
            m_Applied = true;
            if (!Definition)
                return;

            var health = GetComponent<Health>();
            if (health)
            {
                health.MaxHealth = Definition.MaxHealth * HealthMultiplier;
                // Vida cheia também ao reaproveitar a instância (PrefabPool)
                health.CurrentHealth = health.MaxHealth;
            }
        }
    }
}
