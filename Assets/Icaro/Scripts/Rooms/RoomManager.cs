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

        [Tooltip("Onde surgem a loja e o Metrônomo depois do chefe (vazio = o meio da sala)")]
        public Transform RewardPoint;

        [Tooltip("Pontos de spawn da arena (longe da entrada e fora da visão inicial)")]
        public Transform[] SpawnPoints;

        [Tooltip("Pontos altos e visíveis, exclusivos de quem exige ponto alto (Espinhento)")]
        public HighSpawnPoint[] HighSpawnPoints;

        public RoomState State { get; private set; } = RoomState.Idle;

        // Inimigos da sala ao todo (cota + chefe) e quantos ainda faltam surgir ou estão vivos
        public int TotalEnemies { get; private set; }
        public int RemainingEnemies => m_ToSpawn + m_Pending + CountAlive() + (m_BossPending ? 1 : 0);

        // Cota (sem o chefe) e limite de vivos desta sala
        public int Quota { get; private set; }
        public int AliveLimit { get; private set; }

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

        // Inimigos marcados (soltam o orbe do tempo): espalhados pela cota, a cada m_MarkSpacing surgidos
        int m_Spawned;
        int m_MarksLeft;
        float m_MarkSpacing;
        float m_NextMarkAt;
        bool m_BossPending;
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

        IEnumerator RunRoom()
        {
            State = RoomState.Active;
            Active = this;
            if (m_Dome)
                m_Dome.Lock(m_Player);
            OnRoomActivated?.Invoke(this);
            VisorBanner.Show(RoomName, "Arena selada", BannerTone.Warning);

            yield return RunQuota();

            // Escolha de 2 itens entre 4 ao limpar a sala (no chefe, Raro com o dobro da chance); as portas abrem depois
            yield return ItemDraft.Offer(IsBoss);

            State = RoomState.Cleared;
            if (Active == this)
                Active = null;
            if (m_Dome)
                m_Dome.Shatter();
            OnRoomCleared?.Invoke(this);
            OnAnyRoomCleared?.Invoke(this);
            VisorBanner.Show("Arena limpa", "Domo desfeito", BannerTone.Success);
        }

        // ---------- cota contínua ----------

        IEnumerator RunQuota()
        {
            EnemyDefinition bossDefinition = BossDefinitionForRoom;
            bool bossRoom = bossDefinition && bossDefinition.Prefab;
            Quota = Balance.QuotaFor(ArenaNumber, bossRoom);
            AliveLimit = Balance.AliveLimitFor(ArenaNumber, bossRoom);
            m_ToSpawn = Quota;
            m_Pending = 0;
            m_Alive.Clear();
            m_PendingByType.Clear();
            TotalEnemies = Quota + (bossRoom ? 1 : 0);

            // Marcados: um a cada MarkEvery da cota (no mínimo MarkMin), no meio de cada trecho; a sala do chefe não
            // tem (o chefe solta os orbes dele)
            TimeOrbSettings orbs = Balance.TimeOrbs;
            m_Spawned = 0;
            m_MarksLeft = bossRoom || orbs == null || Quota <= 0
                ? 0
                : Mathf.Min(Quota, Mathf.Max(orbs.MarkMin, Mathf.RoundToInt(Quota / (float)orbs.MarkEvery)));
            m_MarkSpacing = m_MarksLeft > 0 ? Quota / (float)m_MarksLeft : 0f;
            m_NextMarkAt = m_MarkSpacing * 0.5f;

            if (bossRoom)
            {
                m_BossPending = true;
                BossName = string.IsNullOrEmpty(bossDefinition.DisplayName)
                    ? bossDefinition.name.Replace("Inimigo_", "").Replace("Chefe_", "")
                    : bossDefinition.DisplayName;
                VisorBanner.Show(BossName, RoomName, BannerTone.Danger);
                StartCoroutine(SpawnBoss());
            }

            yield return new WaitForSeconds(Balance.FirstSpawnDelay);

            while (m_ToSpawn > 0)
            {
                if (RegularAlive() + m_Pending < AliveLimit)
                {
                    EnemyDefinition next = SpawnDirector.Pick(Balance, ArenaNumber, ActiveByType(), FreeHighPoints());
                    if (next)
                    {
                        BeginSpawn(next, GroupSize(next));
                        yield return new WaitForSeconds(Balance.NextSpawnInterval());
                        continue;
                    }
                }

                // Sem vaga (ou todos os tipos no limite): espera alguém morrer
                yield return null;
            }

            while (RemainingEnemies > 0)
                yield return null;
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
                yield return new WaitForSeconds(duration);

                if (PlayerDistance(position) >= Balance.MinSpawnDistance * 0.5f)
                    break;
                position = PickSpawnPosition();
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 spread = count > 1 ? Random.insideUnitCircle * 1.5f : Vector2.zero;
                EndPending(definition);
                m_Alive.Add(Spawn(definition, position + new Vector3(spread.x, 0f, spread.y), false));
            }
        }

        // Chefe: entra primeiro, pelo ponto do chefe, depois do aviso
        IEnumerator SpawnBoss()
        {
            Vector3 position = BossPoint ? BossPoint.position : PickSpawnPosition();
            yield return new WaitForSeconds(ShowSpawnWarning(position));

            GameObject boss = Spawn(BossDefinitionForRoom, position, true);
            m_BossPending = false;
            Boss = boss.GetComponent<Health>();
            if (Boss)
                Boss.OnDie += () =>
                {
                    // Chefe derrotado: fragmentos garantidos, espalhados em volta dele
                    Vector3 center = Boss ? Boss.transform.position + Vector3.up : position;
                    ShopDirector.DropFor(BossDefinitionForRoom, center, ArenaNumber, false, true);
                    TimeOrbSettings orbs = Balance.TimeOrbs;
                    for (int i = 0; orbs != null && i < orbs.BossOrbs; i++)
                    {
                        float angle = i * Mathf.PI * 2f / Mathf.Max(1, orbs.BossOrbs);
                        TimeOrb.Spawn(orbs, center + new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)) * 3f);
                    }
                    Boss = null;
                };
        }

        // Espinhento: só em ponto alto livre e válido; a linha de visão é conferida de novo no fim do aviso.
        // Sem ponto que sirva, o inimigo não nasce (e sai da cota).
        IEnumerator SpawnOnHighPoint(EnemyDefinition definition)
        {
            HighSpawnPoint point = ReserveHighPoint(null);
            for (int attempt = 0; point && attempt < 3; attempt++)
            {
                float duration = ShowSpawnWarning(point.transform.position);
                yield return new WaitForSeconds(duration);

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
        GameObject Spawn(EnemyDefinition definition, Vector3 position, bool boss, bool highPoint = false)
        {
            if (highPoint && NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas) &&
                Mathf.Abs(hit.position.y - position.y) < 0.6f)
                position = hit.position;
            else if (!highPoint)
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
            if (!boss && !definition.Pooled && Random.value < Balance.EliteChance(ArenaNumber))
                EliteAffix.Apply(enemy, (EliteModifier)Random.Range(1, 4), Balance);

            // Marcado (solta o orbe do tempo ao morrer); os voadores do pool passam a vez para o próximo
            if (!boss)
            {
                m_Spawned++;
                if (m_MarksLeft > 0 && !definition.Pooled && m_Spawned >= m_NextMarkAt)
                {
                    TimeMark.Apply(enemy, Balance.TimeOrbs);
                    m_MarksLeft--;
                    m_NextMarkAt += m_MarkSpacing;
                }
            }

            // Sai da lista ao morrer (e desinscreve, porque instâncias do pool voltam a nascer)
            var health = enemy.GetComponent<Health>();
            if (health && !boss)
            {
                UnityEngine.Events.UnityAction onDie = null;
                onDie = () =>
                {
                    m_Alive.Remove(enemy);
                    health.OnDie -= onDie;
                    ShopDirector.DropFor(definition, enemy.transform.position + Vector3.up, ArenaNumber,
                        stats.Elite != EliteModifier.None, false);
                };
                health.OnDie += onDie;
            }
            return enemy;
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
