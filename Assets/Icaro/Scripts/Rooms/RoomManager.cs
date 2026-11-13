using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Icaro
{
    // Sala de combate: quando o jogador entra no trigger, o domo de energia (ArenaDome) sela a arena e os inimigos
    // começam a surgir de forma
    // contínua. A sala tem uma cota total e um limite de vivos ao mesmo tempo, que crescem pelo índice global da
    // sala (CombatBalance.QuotaFor e AliveLimitFor): enquanto houver vaga e cota, surge o próximo (sorteado pelo
    // SpawnDirector), a cada 0,5 a 1 s. Quando um morre, outro pode entrar no lugar. A sala termina quando toda a
    // cota surgiu e todos morreram; então o jogador escolhe 2 itens entre 4 e o domo se estilhaça. Os portões das
    // cenas geradas (Doors) ficam desligados. Uma sala limpa não
    // reativa. Cada inimigo surge num ponto de spawn longe do jogador, depois de um aviso de 1 s; o Espinhento só em
    // pontos altos válidos. Na sala do chefe, o chefe entra primeiro pelo ponto BossPoint, com uma cota pequena de
    // reforços. Vida e dano dos inimigos crescem pelo índice da sala (ArenaNumber).
    [RequireComponent(typeof(Collider))]
    public class RoomManager : MonoBehaviour
    {
        public enum RoomState
        {
            Idle,
            Active,
            Cleared,
        }

        // Sala em combate agora (para o HUD); null fora de combate
        public static RoomManager Active { get; private set; }

        [Tooltip("Nome mostrado nas mensagens do HUD")]
        public string RoomName = "Sala";

        public RoomDoor[] Doors;

        [Header("Combate")]
        [Tooltip("Balanceamento do combate (cota, composição, escala por sala)")]
        public CombatBalance Balance;

        [Tooltip("0 Bosque, 1 Charco, 2 Ruínas, 3 Cratera")]
        public int BiomeIndex;

        [Tooltip("Índice global da sala na Via (1 a 20): define cota, limite de vivos, composição e escala")]
        public int ArenaNumber = 1;

        [Tooltip("Posição da arena no bioma (0 = primeira) e quantas arenas o bioma tem")]
        public int ArenaInBiome;

        public int ArenasInBiome = 1;

        public bool IsBoss;

        [Tooltip("Chefe que entra no começo da sala (opcional; o do bioma no CombatBalance.Bosses tem prioridade). " +
                 "Sem chefe, a sala usa a cota normal")]
        public EnemyDefinition BossEnemy;

        public Transform BossPoint;

        [Tooltip("Onde surge a loja depois do chefe (vazio = o meio da sala)")]
        public Transform RewardPoint;

        [Tooltip("Pontos de spawn da arena (longe da entrada e fora da visão inicial)")]
        public Transform[] SpawnPoints;

        [Tooltip("Pontos altos e visíveis, exclusivos de quem exige ponto alto (Espinhento)")]
        public HighSpawnPoint[] HighSpawnPoints;

        public RoomState State { get; private set; } = RoomState.Idle;

        // Inimigos da sala ao todo (cota + chefe) e quantos ainda faltam surgir ou estão vivos
        public int TotalEnemies { get; private set; }

        // A cota da sala já foi montada (na entrada, antes dela, ainda não há inimigos a contar)
        public bool QuotaStarted { get; private set; }
        public int RemainingEnemies => m_ToSpawn + m_Pending + CountAlive() + (m_BossPending ? 1 : 0);

        // Cota (sem o chefe) e limite de vivos desta sala
        public int Quota { get; private set; }
        public int AliveLimit { get; private set; }

        // Chão andável da arena em m² (NavMesh dentro do domo), medido ao começar a luta
        public float FloorArea { get; private set; }

        // Inimigos vivos da sala, sem o chefe (pode ter entradas destruídas: checar null). Usado pela mira assistida.
        public IReadOnlyList<GameObject> AliveEnemies => m_Alive;

        // Vida do chefe vivo (para o HUD); null sem chefe ou depois que ele morre
        public Health Boss { get; private set; }

        // Domo de energia da sala (limite da arena para as IAs; montado no Start)
        public ArenaDome Dome => m_Dome;
        public string BossName { get; private set; }

        // Chefe desta sala: o do bioma no CombatBalance ou, sem ele, o BossEnemy da cena
        EnemyDefinition BossDefinitionForRoom =>
            IsBoss ? (Balance && Balance.BossFor(BiomeIndex) ? Balance.BossFor(BiomeIndex) : BossEnemy) : null;

        public event Action<RoomManager> OnRoomActivated;
        public event Action<RoomManager> OnRoomCleared;

        // Qualquer sala limpa (lojas depois do chefe)
        public static event Action<RoomManager> OnAnyRoomCleared;

        // Inimigos vivos (sem o chefe); a morte vem do Health.OnDie, e null cobre quem foi destruído por outro
        // motivo (ex.: caiu do mapa)
        readonly List<GameObject> m_Alive = new List<GameObject>();
        readonly Dictionary<EnemyDefinition, int> m_PendingByType = new Dictionary<EnemyDefinition, int>();
        readonly Dictionary<EnemyDefinition, int> m_Active = new Dictionary<EnemyDefinition, int>();
        int m_ToSpawn;
        int m_Pending;

        bool m_BossPending;
        EnemyDefinition[] m_Reinforcements;
        bool m_ReinforcementsCut;
        Transform m_Player;
        ArenaDome m_Dome;
        int m_LastSpawnPoint = -1;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        // O domo de energia prende o jogador na luta (no lugar dos portões, que ficam desligados)
        void Start()
        {
            if (Doors != null)
            {
                foreach (RoomDoor door in Doors)
                {
                    if (door)
                        door.gameObject.SetActive(false);
                }
            }

            m_Dome = ArenaDome.Create(this);
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        void OnTriggerEnter(Collider other)
        {
            if (State != RoomState.Idle)
                return;

            var player = other.GetComponentInParent<PlayerCharacterController>();
            if (!player)
                return;
            if (!Balance)
            {
                Debug.LogWarning($"[Ícaro] {name} não tem CombatBalance; a sala não tem inimigos.", this);
                return;
            }

            m_Player = player.transform;
            StartCoroutine(RunRoom());
        }

        // Segundos de cada etapa da entrada e da saída da sala (tempo sem escala)
        const float k_WakeTime = 0.75f;
        const float k_DomeTime = 1f;
        const float k_BannerTimeout = 6f;
        const float k_DrainTime = 1.5f;

        // Ordem (pedido do usuário): o mundo acorda em 1,0x (sem o multiplicador crescer), o domo sobe, a faixa da
        // sala aparece e some, e só então os inimigos começam a surgir e o multiplicador a crescer. No último abate:
        // câmera lenta com "Arena limpa", o domo se desfaz, o multiplicador desce contando até 1,0x (com as Cronitas
        // voando até o jogador), o mundo congela e vêm as cartas; depois delas, "Siga em frente".
        IEnumerator RunRoom()
        {
            State = RoomState.Active;
            Active = this;
            TimeManager time = TimeManager.Instance;

            if (time && !time.WorldAwake)
            {
                time.WakeWorld();
                yield return Unscaled(k_WakeTime);
            }

            if (m_Dome)
                m_Dome.Lock(m_Player);
            OnRoomActivated?.Invoke(this);
            yield return Unscaled(k_DomeTime);

            EnemyDefinition bossDefinition = BossDefinitionForRoom;
            if (bossDefinition && bossDefinition.Prefab)
            {
                BossName = string.IsNullOrEmpty(bossDefinition.DisplayName)
                    ? bossDefinition.name.Replace("Inimigo_", "").Replace("Chefe_", "")
                    : bossDefinition.DisplayName;
                VisorBanner.Show(BossName, RoomName, BannerTone.Danger);
            }
            else
            {
                VisorBanner.Show(RoomName, "Arena selada", BannerTone.Warning);
            }

            for (float t = 0f; t < k_BannerTimeout && VisorBanner.Busy; t += Time.unscaledDeltaTime)
                yield return null;

            yield return RunQuota();

            // Último abate: o multiplicador para de crescer e fica no valor durante a câmera lenta
            if (time)
                time.EndCombat();
            yield return RoomClearSequence.Play(this);

            if (m_Dome)
                m_Dome.Shatter();
            CronitaPickup.AttractAll();
            if (time)
                yield return time.DrainToOne(k_DrainTime);
            yield return RoomClearSequence.Collect();

            if (time)
            {
                time.FreezeWorld();
                var freeze = time.GetComponent<WorldFreeze>();
                yield return Unscaled(freeze ? freeze.FreezeTime : 1.4f);
            }

            // Escolha de 2 itens entre 4 (no chefe, Raro com o dobro da chance)
            yield return ItemDraft.Offer(IsBoss);

            State = RoomState.Cleared;
            if (Active == this)
                Active = null;
            OnRoomCleared?.Invoke(this);
            OnAnyRoomCleared?.Invoke(this);
            VisorBanner.Show("Siga em frente", "", BannerTone.Success);
        }

        // Espera em tempo sem escala (não para no menu de pausa: a pausa já para o resto)
        static IEnumerator Unscaled(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        // ---------- cota contínua ----------

        IEnumerator RunQuota()
        {
            EnemyDefinition bossDefinition = BossDefinitionForRoom;
            bool bossRoom = bossDefinition && bossDefinition.Prefab;
            Quota = Balance.QuotaFor(ArenaNumber, bossRoom);
            FloorArea = ArenaFloor.AreaOf(this);
            AliveLimit = Balance.AliveLimitFor(ArenaNumber, bossRoom, FloorArea);
            // Reforços próprios do chefe (quais, quantos e quantos vivos)
            m_Reinforcements = null;
            m_ReinforcementsCut = false;
            if (bossRoom && bossDefinition is BossDefinition bossData)
            {
                if (bossData.ReinforcementQuota >= 0)
                    Quota = Mathf.RoundToInt(bossData.ReinforcementQuota * Balance.EnemyCountMultiplier);
                if (bossData.ReinforcementAlive >= 0)
                    AliveLimit = Mathf.Max(1, Mathf.RoundToInt(bossData.ReinforcementAlive * Balance.EnemyCountMultiplier));
                if (bossData.Reinforcements != null && bossData.Reinforcements.Length > 0)
                    m_Reinforcements = bossData.Reinforcements;
            }
            Debug.Log($"[Ícaro] Sala {ArenaNumber} ({RoomName}): {FloorArea:0} m² de chão, até {AliveLimit} vivos, cota {Quota}.");
            m_ToSpawn = Quota;
            QuotaStarted = true;
            m_Pending = 0;
            m_Alive.Clear();
            m_PendingByType.Clear();
            TotalEnemies = Quota + (bossRoom ? 1 : 0);

            // Os inimigos começam a surgir e o multiplicador a crescer juntos (a entrada da sala já deu o respiro
            // que o FirstSpawnDelay dava)
            if (TimeManager.Instance)
                TimeManager.Instance.BeginCombat();

            if (bossRoom)
            {
                m_BossPending = true;
                StartCoroutine(SpawnBoss());
            }

            // A volta no tempo (TimeRewind) pode devolver inimigos à cota depois que todos surgiram: o laço continua
            // surgindo enquanto restar alguém
            while (RemainingEnemies > 0)
            {
                if (m_ToSpawn > 0 && RegularAlive() + m_Pending < AliveLimit)
                {
                    EnemyDefinition next = m_Reinforcements != null
                        ? SpawnDirector.PickFrom(Balance, ArenaNumber, m_Reinforcements, ActiveByType(), FreeHighPoints())
                        : SpawnDirector.Pick(Balance, ArenaNumber, ActiveByType(), FreeHighPoints());
                    if (next)
                    {
                        BeginSpawn(next, GroupSize(next));
                        yield return CombatTiming.Wait(Balance.NextSpawnInterval());
                        continue;
                    }
                }

                // Sem vaga (ou todos os tipos no limite): espera alguém morrer
                yield return null;
            }
        }

        // Vivos e surgindo de cada tipo (limite MaxAlive)
        Dictionary<EnemyDefinition, int> ActiveByType()
        {
            m_Active.Clear();
            foreach (KeyValuePair<EnemyDefinition, int> pair in m_PendingByType)
                m_Active[pair.Key] = pair.Value;

            RegularAlive();
            foreach (GameObject enemy in m_Alive)
            {
                var stats = enemy.GetComponent<EnemyStats>();
                if (!stats || !stats.Definition)
                    continue;
                m_Active.TryGetValue(stats.Definition, out int count);
                m_Active[stats.Definition] = count + 1;
            }

            return m_Active;
        }

        // Tamanho do grupo (Zangão: 3 a 5 do mesmo ponto), limitado pela cota, pelas vagas e pelo MaxAlive do tipo
        int GroupSize(EnemyDefinition definition)
        {
            int size = definition.GroupMax > 1
                ? Random.Range(definition.GroupMin, Mathf.Max(definition.GroupMin, definition.GroupMax) + 1)
                : 1;
            size = Mathf.Min(size, m_ToSpawn, AliveLimit - (RegularAlive() + m_Pending));

            int cap = Balance.TypeAliveCap(definition);
            if (cap > 0)
            {
                m_Active.TryGetValue(definition, out int active);
                size = Mathf.Min(size, cap - active);
            }

            return Mathf.Max(1, size);
        }

        // Cada membro do grupo conta 1 na cota
        void BeginSpawn(EnemyDefinition definition, int count)
        {
            m_ToSpawn -= count;
            m_Pending += count;
            m_PendingByType.TryGetValue(definition, out int pending);
            m_PendingByType[definition] = pending + count;

            // StartCoroutine roda até o primeiro yield: o ponto alto é reservado já agora
            StartCoroutine(definition.RequiresHighSpawn
                ? SpawnOnHighPoint(definition)
                : SpawnWithWarning(definition, count));
        }

        void EndPending(EnemyDefinition definition)
        {
            m_Pending--;
            if (m_PendingByType.TryGetValue(definition, out int count))
            {
                if (count <= 1)
                    m_PendingByType.Remove(definition);
                else
                    m_PendingByType[definition] = count - 1;
            }
        }

        // Um aviso no ponto e o grupo inteiro surge dele, espalhado um pouco
        IEnumerator SpawnWithWarning(EnemyDefinition definition, int count)
        {
            Vector3 position = PickSpawnPosition();

            // Se o jogador chegar perto do ponto durante o aviso, o inimigo troca de ponto (nunca nasce colado nele)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float duration = ShowSpawnWarning(position);
                yield return CombatTiming.Wait(duration);

                if (PlayerDistance(position) >= Balance.MinSpawnDistance * 0.5f)
                    break;
                position = PickSpawnPosition();
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 spread = count > 1 ? Random.insideUnitCircle * 1.5f : Vector2.zero;
                EndPending(definition);
                if (m_ReinforcementsCut)
                    continue;
                m_Alive.Add(Spawn(definition, position + new Vector3(spread.x, 0f, spread.y), false));
            }
        }

        // Chefe: entra primeiro, pelo ponto do chefe, depois do aviso
        IEnumerator SpawnBoss()
        {
            Vector3 position = BossPoint ? BossPoint.position : PickSpawnPosition();
            yield return CombatTiming.Wait(ShowSpawnWarning(position));

            GameObject boss = Spawn(BossDefinitionForRoom, position, true);
            m_BossPending = false;
            Boss = boss.GetComponent<Health>();

            // Chefe que baixa o tempo por fatias de vida tirada (os que invocam baixam só pelos abates dos reforços)
            if (Boss && BossDefinitionForRoom is BossDefinition bossDefinition &&
                bossDefinition.TimeRewind == BossTimeRewind.DamageSlices)
                BossTimeSlices.Attach(Boss, bossDefinition.TimeSlice, bossDefinition.TimeSliceReduction);
            if (Boss)
                Boss.OnDie += () =>
                {
                    // Chefe derrotado: fragmentos garantidos, espalhados em volta dele
                    Vector3 center = Boss ? Boss.transform.position + Vector3.up : position;
                    ShopDirector.DropFor(BossDefinitionForRoom, center, ArenaNumber, false, true);
                    Boss = null;
                    if (BossDefinitionForRoom is BossDefinition data && data.ReinforcementsDieWithBoss)
                        CutReinforcements();
                };
        }

        // Chefe derrotado: os reforços vivos morrem juntos (com a recompensa de um abate normal) e os que faltam
        // surgir saem da cota, para a sala acabar junto com o chefe
        void CutReinforcements()
        {
            m_ReinforcementsCut = true;
            m_ToSpawn = 0;
            var alive = new List<GameObject>(m_Alive);
            foreach (GameObject enemy in alive)
            {
                if (!enemy)
                    continue;
                var health = enemy.GetComponent<Health>();
                if (health && health.CurrentHealth > 0f)
                {
                    health.Invincible = false;
                    health.Kill();
                }
            }
        }

        // Espinhento: só em ponto alto livre e válido; a linha de visão é conferida de novo no fim do aviso.
        // Sem ponto que sirva, o inimigo não nasce (e sai da cota).
        IEnumerator SpawnOnHighPoint(EnemyDefinition definition)
        {
            HighSpawnPoint point = ReserveHighPoint(null);
            for (int attempt = 0; point && attempt < 3; attempt++)
            {
                float duration = ShowSpawnWarning(point.transform.position);
                yield return CombatTiming.Wait(duration);

                if (m_ReinforcementsCut)
                    break;
                if (point.Validate() && PlayerDistance(point.transform.position) > 3f)
                {
                    EndPending(definition);
                    GameObject enemy = Spawn(definition, point.transform.position, false, highPoint: true);
                    m_Alive.Add(enemy);
                    point.Occupant = enemy;
                    yield break;
                }

                HighSpawnPoint previous = point;
                point = ReserveHighPoint(previous);
                previous.Occupant = null;
            }

            if (point)
                point.Occupant = null;
            EndPending(definition);
        }

        // Reserva um ponto alto livre e válido (diferente de 'except'); a reserva vira o inimigo ao nascer
        HighSpawnPoint ReserveHighPoint(HighSpawnPoint except)
        {
            if (HighSpawnPoints == null)
                return null;

            var free = new List<HighSpawnPoint>();
            foreach (HighSpawnPoint point in HighSpawnPoints)
            {
                if (point && point != except && point.IsFree && point.Validate())
                    free.Add(point);
            }

            if (free.Count == 0)
                return null;
            HighSpawnPoint chosen = free[Random.Range(0, free.Count)];
            chosen.Occupant = gameObject;
            return chosen;
        }

        // Pontos altos livres e válidos agora (quantos Espinhentos ainda cabem)
        int FreeHighPoints()
        {
            int count = 0;
            if (HighSpawnPoints == null)
                return 0;
            foreach (HighSpawnPoint point in HighSpawnPoints)
            {
                if (point && point.IsFree && point.Validate())
                    count++;
            }

            return count;
        }

        float ShowSpawnWarning(Vector3 position)
        {
            float duration = CombatTiming.Warning(Balance.SpawnWarningDuration);
            if (Balance.SpawnWarningPrefab)
            {
                GameObject warning = Instantiate(Balance.SpawnWarningPrefab, position, Quaternion.identity);
                var spawnWarning = warning.GetComponent<SpawnWarning>();
                if (spawnWarning)
                    spawnWarning.Begin(duration);
                else
                    Destroy(warning, duration);
            }

            return duration;
        }

        // highPoint: nasce exatamente no ponto (só encosta no NavMesh do topo do poleiro, nunca no chão abaixo)
        GameObject Spawn(EnemyDefinition definition, Vector3 position, bool boss, bool highPoint = false,
            EliteModifier? elite = null, bool exact = false)
        {
            if (!exact && highPoint && NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas) &&
                Mathf.Abs(hit.position.y - position.y) < 0.6f)
                position = hit.position;
            else if (!exact && !highPoint)
                position = ClearGround(definition, position, boss ? Balance.BossScale : 1f);

            Vector3 toPlayer = m_Player ? m_Player.position - position : transform.position - position;
            toPlayer.y = 0f;
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            // Tipos com pool (Zangão) reaproveitam instâncias mortas
            GameObject enemy = definition.Pooled
                ? PrefabPool.Get(definition.Prefab, position, rotation)
                : Instantiate(definition.Prefab, position, rotation);
            if (boss)
                enemy.transform.localScale *= Balance.BossScale;

            var stats = enemy.GetComponent<EnemyStats>();
            if (!stats)
                stats = enemy.AddComponent<EnemyStats>();
            if (!stats.Definition)
                stats.Definition = definition;

            // Vida e dano crescem pelo índice global da sala (curvas do CombatBalance); velocidade, cadência e avisos
            // não, porque já crescem pelo multiplicador de tempo
            stats.Configure(Balance.RoomHealthMultiplier(ArenaNumber, boss),
                Balance.RoomDamageMultiplier(ArenaNumber, boss), boss, ArenaNumber,
                Balance.RoomScrapMultiplier(ArenaNumber));

            // Elite: só inimigos comuns (sem chefe e sem os reaproveitados pelo pool), com a chance da sala
            // (a volta no tempo traz de volta o mesmo modificador, sem sortear)
            if (elite.HasValue)
            {
                if (elite.Value != EliteModifier.None)
                    EliteAffix.Apply(enemy, elite.Value, Balance);
            }
            else if (!boss && !definition.Pooled && Random.value < Balance.EliteChance(ArenaNumber))
                EliteAffix.Apply(enemy, (EliteModifier)Random.Range(1, 4), Balance);

            // Sai da lista ao morrer (e desinscreve, porque instâncias do pool voltam a nascer)
            var health = enemy.GetComponent<Health>();
            if (health && !boss)
            {
                UnityEngine.Events.UnityAction onDie = null;
                onDie = () =>
                {
                    m_Alive.Remove(enemy);
                    m_DieHandlers.Remove(enemy);
                    health.OnDie -= onDie;

                    // Abate: tira do multiplicador a vida da ficha (com o fator de elite, sem a escala por sala)
                    float baseHealth = health.MaxHealth / Mathf.Max(0.01f, stats.HealthMultiplier);
                    TimeManager.RewardKill(definition, baseHealth, enemy.transform.position + Vector3.up);
                    ShopDirector.DropFor(definition, enemy.transform.position + Vector3.up, ArenaNumber,
                        stats.Elite != EliteModifier.None, false);
                };
                health.OnDie += onDie;
                m_DieHandlers[enemy] = onDie;
            }
            return enemy;
        }

        // ---------- volta no tempo (TimeRewind) ----------

        readonly Dictionary<GameObject, UnityEngine.Events.UnityAction> m_DieHandlers =
            new Dictionary<GameObject, UnityEngine.Events.UnityAction>();

        // A sala está em luta (inimigos surgindo ou vivos)
        public bool Fighting => State == RoomState.Active && QuotaStarted && RemainingEnemies > 0;

        // Um inimigo que morreu no trecho voltado volta à vida no lugar, com a vida e o modificador de elite de antes
        public GameObject RewindRevive(EnemyDefinition definition, EliteModifier elite, Vector3 position,
            Quaternion rotation, float health)
        {
            if (!definition || !definition.Prefab || !Balance)
                return null;
            GameObject enemy = Spawn(definition, position, false, elite: elite, exact: true);
            enemy.transform.rotation = rotation;
            var h = enemy.GetComponent<Health>();
            if (h)
                h.CurrentHealth = Mathf.Clamp(health, 1f, h.MaxHealth);
            m_Alive.Add(enemy);

            // Espinhento: volta a ocupar o poleiro
            foreach (HighSpawnPoint point in HighSpawnPoints ?? new HighSpawnPoint[0])
            {
                if (point && !point.Occupant && Vector3.Distance(point.transform.position, position) < 1.5f)
                    point.Occupant = enemy;
            }

            return enemy;
        }

        // Um inimigo que surgiu no trecho voltado some e volta para a cota (vai surgir de novo)
        public void RewindUnspawn(GameObject enemy)
        {
            if (!enemy)
                return;
            m_Alive.Remove(enemy);
            var health = enemy.GetComponent<Health>();
            if (m_DieHandlers.TryGetValue(enemy, out UnityEngine.Events.UnityAction onDie))
            {
                if (health)
                    health.OnDie -= onDie;
                m_DieHandlers.Remove(enemy);
            }

            foreach (HighSpawnPoint point in HighSpawnPoints ?? new HighSpawnPoint[0])
            {
                if (point && point.Occupant == enemy)
                    point.Occupant = null;
            }

            m_ToSpawn++;
            var stats = enemy.GetComponent<EnemyStats>();
            if (stats && stats.Definition && stats.Definition.Pooled)
                PrefabPool.Release(enemy);
            else
                Destroy(enemy);
        }

        // Ponto de spawn aleatório entre os que estão longe do jogador (o mais longe, se nenhum estiver),
        // evitando repetir o último, com um deslocamento para os inimigos não nascerem empilhados
        Vector3 PickSpawnPosition()
        {
            if (SpawnPoints == null || SpawnPoints.Length == 0)
                return transform.position;

            var far = new List<int>();
            int farthest = 0;
            float farthestDistance = -1f;
            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                if (!SpawnPoints[i])
                    continue;
                float distance = PlayerDistance(SpawnPoints[i].position);
                if (distance >= Balance.MinSpawnDistance && i != m_LastSpawnPoint)
                    far.Add(i);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthest = i;
                }
            }

            int index = far.Count > 0 ? far[Random.Range(0, far.Count)] : farthest;
            m_LastSpawnPoint = index;

            Vector2 jitter = Random.insideUnitCircle * Balance.SpawnJitter;
            Vector3 position = SpawnPoints[index].position + new Vector3(jitter.x, 0f, jitter.y);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, Balance.SpawnJitter + 2f, NavMesh.AllAreas))
                return hit.position;
            return SpawnPoints[index].position;
        }

        static readonly Collider[] s_Overlap = new Collider[16];

        // Ponto do NavMesh perto de 'desired', no mesmo nível do chão (nunca em cima de uma pedra), onde o corpo do
        // inimigo (cápsula do raio e da altura do NavMeshAgent) não encosta em nenhum cenário. Tenta em volta, cada
        // vez mais longe; sem lugar livre, fica o ponto pedido (os pontos de spawn são validados no gerador).
        Vector3 ClearGround(EnemyDefinition definition, Vector3 desired, float scale)
        {
            float radius = 0.5f, height = 2f;
            NavMeshAgent agent = definition && definition.Prefab ? definition.Prefab.GetComponent<NavMeshAgent>() : null;
            if (agent)
            {
                float size = definition.Prefab.transform.localScale.x * scale;
                radius = Mathf.Max(0.3f, agent.radius * size);
                height = Mathf.Max(radius * 2f, agent.height * size);
            }

            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 candidate = desired;
                if (attempt > 0)
                {
                    Vector2 offset = Random.insideUnitCircle * (1f + attempt * 0.75f);
                    candidate += new Vector3(offset.x, 0f, offset.y);
                }

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - desired.y) > 1.5f)
                    continue;
                if (!TouchesScenery(hit.position, radius, height))
                    return hit.position;
            }

            return desired;
        }

        // A cápsula em pé neste ponto encosta em algum collider sólido que não é de personagem (inimigo ou jogador)?
        // Começa um pouco acima do chão (desníveis e rampas não contam) e ignora o terreno.
        static bool TouchesScenery(Vector3 feet, float radius, float height)
        {
            float lift = Mathf.Max(0.35f, radius * 0.6f);
            Vector3 bottom = feet + Vector3.up * (radius + lift);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + lift, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, s_Overlap, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = s_Overlap[i];
                if (!(other is TerrainCollider) && !other.GetComponentInParent<Health>())
                    return true;
            }

            return false;
        }

        float PlayerDistance(Vector3 position)
        {
            if (!m_Player)
                return float.MaxValue;
            Vector3 offset = position - m_Player.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        // ---------- utilidades ----------

        // Vivos sem o chefe (o limite de vivos vale só para eles)
        int RegularAlive()
        {
            m_Alive.RemoveAll(enemy => !enemy);
            return m_Alive.Count;
        }

        // Vivos com o chefe
        int CountAlive() => RegularAlive() + (Boss ? 1 : 0);

        void OnDrawGizmosSelected()
        {
            if (SpawnPoints == null)
                return;

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
            foreach (Transform point in SpawnPoints)
            {
                if (!point)
                    continue;

                Gizmos.DrawWireSphere(point.position + Vector3.up, 1f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 2f + Vector3.up);
            }
        }
    }
}
