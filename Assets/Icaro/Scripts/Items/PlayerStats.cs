using System;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Componente central dos atributos do jogador: guarda os itens da run (com as pilhas), soma os efeitos de cada
    // um por atributo (PlayerStat) e aplica os passivos: vida máxima, regeneração, velocidade de movimento, cadência,
    // pente e recarga da arma ativa, ritmo do multiplicador de tempo (e a lentidão da água rasa, WaterMap). Crítico e multiplicador de dano
    // são lidos pelo PlayerProjectile; as defesas (escudo, revivida), os efeitos de acerto, o pulo no ar e o dash ficam em PlayerDefenses,
    // ItemProcs, PlayerAirJump e PlayerDash, que este componente adiciona se faltarem (junto com o AimAssist e o
    // PlayerArsenal, das armas compradas na loja). Os itens duram a run e
    // somem ao morrer (a cena é recarregada). Valores iniciais vêm do CombatBalance, pelo PlayerBalance.
    [DefaultExecutionOrder(-110)]
    public class PlayerStats : MonoBehaviour
    {
        [Serializable]
        public class ItemStack
        {
            public ItemDefinition Item;
            public int Count;
        }

        public static PlayerStats Instance { get; private set; }

        [Tooltip("Catálogo dos itens (vem do CombatBalance.Items)")]
        public ItemCatalog Catalog;

        [Header("Valores iniciais (o PlayerBalance copia do CombatBalance)")]
        [Range(0f, 1f)] public float BaseCritChance = 0.1f;
        public float CritMultiplier = 2f;
        public float BaseDamageMultiplier = 1f;

        [SerializeField] List<ItemStack> m_Items = new List<ItemStack>();

        public IReadOnlyList<ItemStack> Items => m_Items;

        // Itens mudaram (pegou ou consumiu)
        public event Action OnItemsChanged;

        // Velocidade em solo do jogador sem itens (o Cascudo usa como referência)
        public float BaseGroundSpeed { get; private set; }

        readonly float[] m_Values = new float[Enum.GetValues(typeof(PlayerStat)).Length];
        // Números originais de cada arma, antes dos itens (cadência e pente)
        struct WeaponBase
        {
            public float Delay;
            public int MaxAmmo;
        }

        readonly Dictionary<WeaponController, WeaponBase> m_WeaponBases = new Dictionary<WeaponController, WeaponBase>();

        PlayerCharacterController m_Controller;
        PlayerWeaponsManager m_Weapons;
        Health m_Health;
        float m_BaseAirSpeed;
        float m_BaseMaxHealth = -1f;
        float m_WaterFactor = 1f;
        float m_AdrenalineEnd = -1f;
        float m_AdrenalineReady;
        bool m_AdrenalineApplied;

        public float Get(PlayerStat stat) => m_Values[(int)stat];

        public float CritChance => Mathf.Clamp01(BaseCritChance + Get(PlayerStat.CritChance));

        // Multiplicador do crítico: o do CombatBalance e o Amplificador Focal
        public float CritDamageMultiplier => CritMultiplier + Get(PlayerStat.CritDamage);

        // Multiplicador do dano dos tiros (antes do crítico): o do CombatBalance, a Pólvora Sintética e o Coração Aeônida
        public float DamageMultiplier =>
            BaseDamageMultiplier * (1f + Get(PlayerStat.DamageBonus)) * (1f + HeartBonus) * (1f + ShopDamageBonus);

        // Coração Aeônida: +x de dano por 0,1x de multiplicador acima de 1,0x, até o teto do catálogo (pilhas extras
        // só chegam ao teto mais cedo)
        float HeartBonus
        {
            get
            {
                float tenths = Mathf.Max(0f, TimeManager.CurrentMultiplier - 1f) / 0.1f;
                return Mathf.Min(Get(PlayerStat.DamagePerTenthMultiplier) * tenths,
                    Catalog ? Catalog.HeartDamageCap : 2.5f);
            }
        }

        // Injetor de Adrenalina ativo (velocidade e cadência extras)
        public bool AdrenalineActive => Time.time < m_AdrenalineEnd;

        float AdrenalineBonus => AdrenalineActive ? (Catalog ? Catalog.AdrenalineBonus : 0.15f) : 0f;

        // Multiplicador de dano de um tiro que acerta a 'distance' metros do jogador (Cano Curto)
        public float CloseRangeMultiplier(float distance) =>
            distance <= (Catalog ? Catalog.CloseRangeDistance : 6f) ? 1f + Get(PlayerStat.CloseRangeDamage) : 1f;

        // Multiplicador do raio de atração das Cronitas (Núcleo Imantado)
        public float PickupRangeMultiplier => 1f + Get(PlayerStat.PickupRange);

        // Dano de um tiro normal da arma ativa (com multiplicadores, sem crítico): base dos efeitos de item
        public float ShotDamage
        {
            get
            {
                WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
                ProjectileBase prefab = weapon ? weapon.ProjectilePrefab : null;
                float damage = prefab is HomingProjectile homing ? homing.BaseDamage
                    : prefab is ProjectileStandard standard ? standard.Damage
                    : 20f;
                return damage * DamageMultiplier;
            }
        }

        public float MaxHealth => m_Health ? m_Health.MaxHealth : 0f;

        // Melhorias compradas na loja (dano e cadência), pela carteira
        float ShopDamageBonus => PlayerWallet.Instance
            ? PlayerWallet.Instance.DamageUpgrades * ShopDirector.Settings.DamageUpgradeAmount
            : 0f;

        float ShopFireRateBonus => PlayerWallet.Instance
            ? PlayerWallet.Instance.FireRateUpgrades * ShopDirector.Settings.FireRateUpgradeAmount
            : 0f;

        void Awake()
        {
            Instance = this;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Weapons = GetComponent<PlayerWeaponsManager>();
            m_Health = GetComponent<Health>();

            if (m_Controller)
            {
                BaseGroundSpeed = m_Controller.MaxSpeedOnGround;
                m_BaseAirSpeed = m_Controller.MaxSpeedInAir;
            }

            // As defesas precisam se inscrever no Health antes do DamageReporter (PlayerBalance)
            if (!GetComponent<PlayerDefenses>())
                gameObject.AddComponent<PlayerDefenses>();
            if (!GetComponent<ItemProcs>())
                gameObject.AddComponent<ItemProcs>();
            if (m_Controller && !GetComponent<PlayerAirJump>())
                gameObject.AddComponent<PlayerAirJump>();
            if (m_Controller && !GetComponent<PlayerDash>())
                gameObject.AddComponent<PlayerDash>();
            if (!GetComponent<AimAssist>())
                gameObject.AddComponent<AimAssist>();
            if (m_Weapons && !GetComponent<PlayerArsenal>())
                gameObject.AddComponent<PlayerArsenal>();
        }

        void OnEnable() => CombatEvents.OnDamage += OnDamage;
        void OnDisable() => CombatEvents.OnDamage -= OnDamage;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Injetor de Adrenalina: ao perder vida, liga por AdrenalineDuration segundos, com recarga fixa
        void OnDamage(DamageInfo info)
        {
            float duration = Get(PlayerStat.AdrenalineDuration);
            if (duration <= 0f || info.VictimHealth != m_Health || info.Killed || Time.time < m_AdrenalineReady)
                return;
            m_AdrenalineEnd = Time.time + duration;
            m_AdrenalineReady = Time.time + (Catalog ? Catalog.AdrenalineCooldown : 10f);
        }

        void Start()
        {
            // Interface das cartas e do inventário (uma por cena)
            if (Catalog && !FindAnyObjectByType<ItemDraft>())
            {
                var ui = new GameObject("Itens");
                ui.AddComponent<ItemDraft>().Catalog = Catalog;
                ui.AddComponent<ItemHUD>().Catalog = Catalog;
            }

            // Indicadores de dano direcional, ameaças fora da tela e Metrônomo
            if (!FindAnyObjectByType<ThreatIndicatorHUD>())
                new GameObject("IndicadoresHUD").AddComponent<ThreatIndicatorHUD>();

            // Munição e recarga da arma na mão
            if (!FindAnyObjectByType<WeaponAmmoHUD>())
                new GameObject("MunicaoHUD").AddComponent<WeaponAmmoHUD>();

            // Arma em sprite 2D com a mão robótica (no lugar do modelo 3D)
            if (!FindAnyObjectByType<WeaponSpriteHUD>())
                new GameObject("ArmaSpriteHUD").AddComponent<WeaponSpriteHUD>();

            Recalculate();
        }

        void Update()
        {
            ApplyWeaponStats();
            ApplyRegen();

            // Água rasa (WaterMap): mais lento enquanto anda dentro dela; a adrenalina liga e desliga a velocidade
            float water = WaterMap.SpeedAt(transform.position);
            if (!Mathf.Approximately(water, m_WaterFactor) || AdrenalineActive != m_AdrenalineApplied)
            {
                m_WaterFactor = water;
                ApplyMoveSpeed();
            }

            if (TimeManager.Instance)
                TimeManager.Instance.GrowthRate = 1f - Mathf.Min(Get(PlayerStat.TimeSlow),
                    Catalog ? Catalog.MaxTimeSlow : 0.75f);
        }

        public int CountOf(ItemDefinition item)
        {
            foreach (ItemStack stack in m_Items)
            {
                if (stack.Item == item)
                    return stack.Count;
            }

            return 0;
        }

        public void AddItem(ItemDefinition item)
        {
            if (!item)
                return;

            ItemStack stack = m_Items.Find(s => s.Item == item);
            if (stack == null)
                m_Items.Add(stack = new ItemStack { Item = item });
            stack.Count++;
            Recalculate();
        }

        // Gasta uma pilha do primeiro item consumível que dá este atributo (ex.: Segunda Ignição)
        public bool ConsumeItem(PlayerStat stat, out ItemDefinition consumed)
        {
            consumed = null;
            foreach (ItemStack stack in m_Items)
            {
                if (!stack.Item || !stack.Item.Consumable || !Gives(stack.Item, stat))
                    continue;

                consumed = stack.Item;
                stack.Count--;
                if (stack.Count <= 0)
                    m_Items.Remove(stack);
                Recalculate();
                return true;
            }

            return false;
        }

        static bool Gives(ItemDefinition item, PlayerStat stat)
        {
            if (item.Effects == null)
                return false;
            foreach (ItemEffect effect in item.Effects)
            {
                if (effect.Stat == stat)
                    return true;
            }

            return false;
        }

        // Soma os efeitos de todos os itens e aplica os passivos
        public void Recalculate()
        {
            Array.Clear(m_Values, 0, m_Values.Length);
            foreach (ItemStack stack in m_Items)
            {
                if (!stack.Item || stack.Item.Effects == null)
                    continue;
                foreach (ItemEffect effect in stack.Item.Effects)
                    m_Values[(int)effect.Stat] += effect.ValueFor(stack.Count);
            }

            ApplyMaxHealth();
            ApplyMoveSpeed();
            OnItemsChanged?.Invoke();
        }

        // Vida extra entra cheia (como no Risk of Rain 2): a vida atual sobe junto com a máxima
        void ApplyMaxHealth()
        {
            if (!m_Health)
                return;
            if (m_BaseMaxHealth < 0f)
                m_BaseMaxHealth = m_Health.MaxHealth;

            float previous = m_Health.MaxHealth;
            m_Health.MaxHealth = m_BaseMaxHealth + Get(PlayerStat.MaxHealth);
            float gained = m_Health.MaxHealth - previous;
            if (gained > 0f)
                m_Health.Heal(gained);
            else if (m_Health.CurrentHealth > m_Health.MaxHealth)
                m_Health.CurrentHealth = m_Health.MaxHealth;
        }

        void ApplyMoveSpeed()
        {
            if (!m_Controller)
                return;
            m_AdrenalineApplied = AdrenalineActive;
            float factor = (1f + Get(PlayerStat.MoveSpeed) + AdrenalineBonus) * m_WaterFactor;
            m_Controller.MaxSpeedOnGround = BaseGroundSpeed * factor;
            m_Controller.MaxSpeedInAir = m_BaseAirSpeed * factor;
        }

        // Fração do tempo de recarga do pente (Resfriador Avançado; nunca abaixo do piso do catálogo). O IcaroWeapon lê.
        public float ReloadTimeMultiplier =>
            Mathf.Max(1f - Get(PlayerStat.ReloadTimeReduction), Catalog ? Catalog.MinReloadTimeFactor : 0.25f);

        // Arma ativa: o intervalo entre tiros é o original / (1 + cadência) e o pente cresce com o Tambor Ampliado
        void ApplyWeaponStats()
        {
            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            if (!weapon)
                return;
            if (!m_WeaponBases.TryGetValue(weapon, out WeaponBase original))
                m_WeaponBases[weapon] = original = new WeaponBase
                {
                    Delay = weapon.DelayBetweenShots, MaxAmmo = weapon.MaxAmmo,
                };

            weapon.DelayBetweenShots = original.Delay /
                                       (1f + Get(PlayerStat.FireRate) + ShopFireRateBonus + AdrenalineBonus);
            weapon.MaxAmmo = Mathf.Max(1, Mathf.RoundToInt(original.MaxAmmo * (1f + Get(PlayerStat.AmmoCapacity))));
        }

        // Soro de Biomassa: sobe a vida direto (Heal piscaria a tela de cura a cada frame)
        void ApplyRegen()
        {
            float regen = Get(PlayerStat.HealthRegen);
            if (regen <= 0f || !m_Health || m_Health.CurrentHealth <= 0f || m_Health.CurrentHealth >= m_Health.MaxHealth)
                return;
            m_Health.CurrentHealth = Mathf.Min(m_Health.MaxHealth, m_Health.CurrentHealth + regen * Time.deltaTime);
        }
    }
}
