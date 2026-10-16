using System;
using UnityEngine;

namespace Icaro
{
    // Balanceamento do combate: cota de inimigos por sala, composição (pesos por faixa de salas), escala dos inimigos
    // por sala, justiça e jogador.
    // Cada sala de combate tem uma cota total de inimigos e um limite de vivos ao mesmo tempo, que crescem pelo
    // índice global da sala (1 a 20, sem zerar entre biomas). Enquanto houver vaga e cota, surge o próximo
    // inimigo, sorteado pelos pesos da faixa de salas (SpawnTables) entre os tipos que ainda não atingiram o
    // próprio MaxAlive.
    [CreateAssetMenu(menuName = "Ícaro/Balanceamento de combate", fileName = "CombatBalance")]
    public class CombatBalance : ScriptableObject
    {
        [Serializable]
        public class SpawnWeight
        {
            public EnemyDefinition Enemy;
            [Min(0f)] public float Weight = 1f;
        }

        // Pesos de sorteio de uma faixa de salas (ex.: "Bosque 1-3"); a primeira faixa que contém a sala vale
        [Serializable]
        public class SpawnTable
        {
            public string Name = "Faixa";
            [Min(1)] public int FromRoom = 1;
            [Min(1)] public int ToRoom = 20;
            public SpawnWeight[] Weights;
        }

        [Header("Cota de inimigos por sala (índice global 1 a 20)")]
        [Tooltip("Inimigos que surgem ao todo na sala 1")]
        [Min(0f)] public float QuotaBase = 12f;

        [Tooltip("Inimigos a mais na cota a cada sala seguinte")]
        [Min(0f)] public float QuotaPerRoom = 2.5f;

        [Tooltip("Limite de inimigos vivos ao mesmo tempo na sala 1")]
        [Min(1f)] public float AliveBase = 6f;

        [Tooltip("Vivos ao mesmo tempo a mais a cada sala seguinte")]
        [Min(0f)] public float AlivePerRoom = 0.75f;

        [Tooltip("Teto absoluto de inimigos vivos ao mesmo tempo (desempenho)")]
        [Min(1)] public int AliveCeiling = 40;

        [Tooltip("Multiplica a cota, o limite de vivos e o MaxAlive de cada tipo (o teto absoluto continua valendo)")]
        [Min(0.1f)] public float EnemyCountMultiplier = 1f;

        [Tooltip("Segundos entre um inimigo começar a surgir e o próximo (sorteado entre mínimo e máximo)")]
        [Min(0f)] public float SpawnIntervalMin = 0.5f;

        [Min(0f)] public float SpawnIntervalMax = 1f;

        [Tooltip("Segundos entre trancar as portas e o primeiro inimigo surgir")]
        [Min(0f)] public float FirstSpawnDelay = 2f;

        [Header("Salas de chefe")]
        [Tooltip("Reforços que surgem junto com o chefe (antes do EnemyCountMultiplier)")]
        [Min(0)] public int BossQuota = 8;

        [Tooltip("Reforços vivos ao mesmo tempo na sala do chefe (antes do EnemyCountMultiplier)")]
        [Min(1)] public int BossAliveLimit = 3;

        [Header("Composição (pesos por bioma e faixa de salas)")]
        public SpawnTable[] SpawnTables;

        [Header("Spawn")]
        [Tooltip("Aviso no ponto antes do inimigo aparecer, a 1,0x (encurta com o multiplicador até 60%)")]
        public float SpawnWarningDuration = 1f;

        [Tooltip("Distância mínima entre o ponto de spawn e o jogador")]
        public float MinSpawnDistance = 14f;

        [Tooltip("Deslocamento aleatório em volta do ponto de spawn, para inimigos não nascerem empilhados")]
        public float SpawnJitter = 2.5f;

        public GameObject SpawnWarningPrefab;

        [Header("Escala dos inimigos por sala (índice global 1 a 20)")]
        [Tooltip("Multiplicador da vida por sala (x = sala). Inicial: 1 + 0,07 x (sala - 1), cerca de x2,3 na sala 20")]
        public AnimationCurve HealthByRoom = LinearByRoom(0.07f);

        [Tooltip("Multiplicador do dano por sala (x = sala). Inicial: 1 + 0,035 x (sala - 1), cerca de x1,7 na sala 20")]
        public AnimationCurve DamageByRoom = LinearByRoom(0.035f);

        [Tooltip("Multiplicador da sucata dropada por sala, proporcional à vida extra (para o sistema de sucata)")]
        public AnimationCurve ScrapByRoom = LinearByRoom(0.07f);

        [Tooltip("Vida extra dos chefes sobre a escala por sala (1,25 = +25%)")]
        public float BossRoomHealthBonus = 1.25f;

        [Header("Chefes")]
        [Tooltip("Chefe de cada bioma (0 Bosque, 1 Charco, 2 Ruínas, 3 Cratera); a sala de chefe usa o do bioma dela")]
        public EnemyDefinition[] Bosses = new EnemyDefinition[4];

        [Tooltip("Efeitos dos ataques dos chefes (também usados pelas explosões dos elites)")]
        public BossKit Kit;

        [Tooltip("Vida do chefe = vida da ficha x sala x este valor x BossRoomHealthBonus (os chefes têm ficha própria)")]
        public float BossHealthMultiplier = 1f;

        public float BossDamageMultiplier = 1f;

        [Tooltip("Escala extra do modelo do chefe (o prefab já tem o tamanho dele)")]
        public float BossScale = 1f;

        [Header("Elites")]
        [Tooltip("Material aditivo do anel e do losango dos elites")]
        public Material EliteMaterial;

        [Tooltip("Chance (0 a 1) de um inimigo comum surgir elite, por sala (x = sala)")]
        public AnimationCurve EliteChanceByRoom = new AnimationCurve(new Keyframe(1f, 0f), new Keyframe(3f, 0f),
            new Keyframe(4f, 0.04f), new Keyframe(20f, 0.15f));

        [Tooltip("Blindado: multiplica a vida e o tamanho")]
        public float EliteArmoredHealth = 2.5f;
        public float EliteArmoredScale = 1.2f;

        [Tooltip("Veloz: multiplica o movimento (por cima do multiplicador de tempo)")]
        public float EliteSwiftSpeed = 1.5f;

        [Tooltip("Veloz: vida extra")]
        public float EliteSwiftHealth = 1.4f;

        [Tooltip("Explosivo: ao morrer, explode depois do aviso, acertando o jogador no raio")]
        public float EliteBlastRadius = 5f;
        public float EliteBlastDamage = 30f;
        public float EliteBlastWarning = 1.2f;
        public float EliteBlastHealth = 1.6f;

        public float EliteChance(int room) => EliteChanceByRoom != null && EliteChanceByRoom.length > 0
            ? Mathf.Clamp01(EliteChanceByRoom.Evaluate(room))
            : 0f;

        // Chefe da sala de chefe do bioma (null sem um)
        public EnemyDefinition BossFor(int biome) =>
            Bosses != null && biome >= 0 && biome < Bosses.Length ? Bosses[biome] : null;

        [Header("Loja")]
        [Tooltip("Cronitas e lojas (preços, o que vendem, cristais)")]
        public ShopSettings Shop;

        [Header("Itens")]
        [Tooltip("Catálogo dos itens oferecidos ao limpar cada sala")]
        public ItemCatalog Items;

        [Header("Jogador")]
        [Tooltip("Vida máxima do Ícaro (o Player do Microgame tem 100)")]
        public float PlayerMaxHealth = 125f;

        [Tooltip("Chance de cada tiro do jogador ser crítico (0 a 1)")]
        [Range(0f, 1f)] public float PlayerCritChance = 0.1f;

        [Tooltip("Multiplicador de dano do crítico")]
        public float PlayerCritMultiplier = 2f;

        [Tooltip("Multiplicador do dano dos tiros do jogador (antes do crítico). 1: o dano das fichas das armas já é o final")]
        [Min(0f)] public float PlayerDamageMultiplier = 1f;

        [Tooltip("Música do jogo (faixa por bioma e camadas dinâmicas)")]
        public MusicSettings Music;

        [Tooltip("Assistência de mira do jogador (zona, alcance, giro, intensidade)")]
        public AimAssistSettings AimAssist;

        [Header("Fragmento de metrônomo")]
        [Tooltip("Prefab do fragmento (MetronomeFragment)")]
        public GameObject FragmentPrefab;

        [Tooltip("Quanto o fragmento reduz o multiplicador de tempo ao ser pego")]
        [Min(0f)] public float FragmentReduction = 0.5f;

        [Tooltip("Segundos até o fragmento sumir (pisca nos últimos FragmentBlinkTime)")]
        [Min(1f)] public float FragmentLifetime = 20f;

        [Min(0f)] public float FragmentBlinkTime = 5f;

        [Tooltip("Fragmentos garantidos ao derrotar o chefe")]
        [Min(0)] public int BossFragmentDrops = 2;

        [Header("Dash do Ícaro")]
        [Tooltip("Distância do dash, em metros")]
        [Min(0f)] public float DashDistance = 7f;

        [Tooltip("Duração do dash, em segundos (a velocidade é distância / duração)")]
        [Min(0.01f)] public float DashDuration = 0.18f;

        [Tooltip("Segundos para recarregar uma carga de dash")]
        [Min(0f)] public float DashCooldown = 2.5f;

        [Tooltip("Cargas de dash sem itens")]
        [Min(1)] public int DashCharges = 1;

        [Tooltip("Invulnerável durante o dash")]
        public bool DashInvulnerable = true;

        [Header("Justiça")]
        [Tooltip("Fração máxima da vida máxima do jogador que um golpe ou projétil comum tira de uma vez")]
        [Range(0.01f, 1f)] public float MaxHitFraction = 0.2f;

        [Tooltip("Mesmo limite para ataques pesados: laser do Golem e chefes")]
        [Range(0.01f, 1f)] public float HeavyMaxHitFraction = 0.25f;

        [Tooltip("Segundos de invulnerabilidade do jogador depois de levar dano (dano contínuo ignora)")]
        [Min(0f)] public float HitInvulnerability = 0.3f;

        // Curva reta de 1,0 na sala 1 até 1 + perRoom x 19 na sala 20 (depois disso fica constante)
        static AnimationCurve LinearByRoom(float perRoom) => AnimationCurve.Linear(1f, 1f, 20f, 1f + perRoom * 19f);

        public float RoomHealthMultiplier(int room, bool boss) =>
            Evaluate(HealthByRoom, room) * (boss ? BossHealthMultiplier * BossRoomHealthBonus : 1f);

        public float RoomDamageMultiplier(int room, bool boss) =>
            Evaluate(DamageByRoom, room) * (boss ? BossDamageMultiplier : 1f);

        public float RoomScrapMultiplier(int room) => Evaluate(ScrapByRoom, room);

        static float Evaluate(AnimationCurve curve, int room) =>
            curve != null && curve.length > 0 ? Mathf.Max(0f, curve.Evaluate(room)) : 1f;

        // Cota total da sala (sem o chefe)
        public int QuotaFor(int room, bool bossRoom)
        {
            float quota = bossRoom ? BossQuota : QuotaBase + QuotaPerRoom * (Mathf.Max(1, room) - 1);
            return Mathf.Max(0, Mathf.RoundToInt(quota * EnemyCountMultiplier));
        }

        // Limite de vivos ao mesmo tempo (sem o chefe), com o teto absoluto
        public int AliveLimitFor(int room, bool bossRoom)
        {
            float alive = bossRoom ? BossAliveLimit : AliveBase + AlivePerRoom * (Mathf.Max(1, room) - 1);
            return Mathf.Clamp(Mathf.RoundToInt(alive * EnemyCountMultiplier), 1, AliveCeiling);
        }

        // Limite de vivos de um tipo, já com o EnemyCountMultiplier (0 = sem limite)
        public int TypeAliveCap(EnemyDefinition enemy) =>
            enemy.MaxAlive > 0 ? Mathf.Max(1, Mathf.RoundToInt(enemy.MaxAlive * EnemyCountMultiplier)) : 0;

        public float NextSpawnInterval() =>
            UnityEngine.Random.Range(SpawnIntervalMin, Mathf.Max(SpawnIntervalMin, SpawnIntervalMax));

        // Tabela de pesos da sala: a primeira faixa que a contém (ou a última, se nenhuma)
        public SpawnTable TableFor(int room)
        {
            if (SpawnTables == null || SpawnTables.Length == 0)
                return null;
            foreach (SpawnTable table in SpawnTables)
            {
                if (table != null && room >= table.FromRoom && room <= table.ToRoom)
                    return table;
            }

            return SpawnTables[SpawnTables.Length - 1];
        }
    }
}
