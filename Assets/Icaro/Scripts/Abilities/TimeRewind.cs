using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Voltar no tempo (habilidade): durante a luta grava, a cada RecordInterval, a posição e a vida do jogador, o
    // multiplicador, as Cronitas e cada inimigo da sala (posição, giro e vida). Ao usar, o mundo para
    // (CombatTiming.WorldSpeed 0) e a gravação corre de trás para frente na tela por 'playback' segundos: o jogador
    // desliza de volta, o multiplicador conta para trás, os inimigos voltam aos lugares; quem morreu no trecho volta à
    // vida quando a fita passa pelo instante da morte (mesma ficha, mesmo elite, a vida de então) e quem surgiu no
    // trecho some e volta para a cota da sala. No fim, tudo fica como no começo do trecho; os tiros e ataques em voo, os
    // cadáveres e os cristais soltos no trecho somem, e cada inimigo recebe OnTimeRewound (as IAs voltam a um estado
    // neutro). Adicionado pelo PlayerAbilities.
    public class TimeRewind : MonoBehaviour
    {
        struct EnemySnap
        {
            public int Id;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Health;
        }

        class Frame
        {
            public float Time;
            public Vector3 PlayerPosition;
            public float PlayerHealth;
            public float Multiplier;
            public int Cronitas;
            public readonly List<EnemySnap> Enemies = new List<EnemySnap>();
        }

        class Tracked
        {
            public GameObject Enemy;
            public EnemyDefinition Definition;
            public EliteModifier Elite;
            public bool Boss;
            public float FirstSeen;
            public bool Removed;     // saiu na volta (surgiu dentro do trecho)
        }

        // A fita está voltando agora
        public static bool Rewinding { get; private set; }

        // Progresso da volta na tela (0 a 1), para os efeitos
        public static float Progress { get; private set; }

        readonly List<Frame> m_Frames = new List<Frame>();
        readonly Stack<Frame> m_FramePool = new Stack<Frame>();
        readonly Dictionary<GameObject, int> m_Ids = new Dictionary<GameObject, int>();
        readonly Dictionary<int, Tracked> m_Tracked = new Dictionary<int, Tracked>();
        int m_NextId = 1;
        float m_NextRecord;
        RoomManager m_Room;

        Health m_Health;
        PlayerCharacterController m_Controller;

        static AbilitySettings Settings => AbilitySettings.Current;

        // Há pelo menos meio segundo gravado
        public bool CanRewind => !Rewinding && m_Frames.Count > 1 && Time.time - m_Frames[0].Time >= 0.5f;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Controller = GetComponent<PlayerCharacterController>();
        }

        void OnDisable()
        {
            if (Rewinding)
            {
                Rewinding = false;
                CombatTiming.WorldSpeed = 1f;
            }
        }

        // ---------- gravação ----------

        void LateUpdate()
        {
            if (Rewinding)
                return;

            RoomManager room = RoomManager.Active;
            if (!room || !room.Fighting || !TimeManager.CurrentlyRunning || (m_Controller && m_Controller.IsDead))
            {
                Clear();
                return;
            }

            if (room != m_Room)
            {
                Clear();
                m_Room = room;
            }

            if (Time.time < m_NextRecord)
                return;
            m_NextRecord = Time.time + Settings.RecordInterval;
            Record(room);

            // Só o trecho que dá para voltar (com uma folga)
            float oldest = Time.time - Settings.RewindSeconds;
            while (m_Frames.Count > 1 && m_Frames[1].Time <= oldest)
            {
                m_FramePool.Push(m_Frames[0]);
                m_Frames.RemoveAt(0);
            }
        }

        void Record(RoomManager room)
        {
            Frame frame = m_FramePool.Count > 0 ? m_FramePool.Pop() : new Frame();
            frame.Enemies.Clear();
            frame.Time = Time.time;
            frame.PlayerPosition = transform.position;
            frame.PlayerHealth = m_Health ? m_Health.CurrentHealth : 0f;
            frame.Multiplier = TimeManager.CurrentMultiplier;
            frame.Cronitas = PlayerWallet.Instance ? PlayerWallet.Instance.Cronitas : 0;

            // Quem morreu ou sumiu desde a última gravação deixa de ter objeto (a ficha fica para voltar à vida)
            s_Gone.Clear();
            foreach (KeyValuePair<GameObject, int> pair in m_Ids)
            {
                if (!IsAlive(pair.Key))
                    s_Gone.Add(pair.Key);
            }

            foreach (GameObject gone in s_Gone)
            {
                if (m_Tracked.TryGetValue(m_Ids[gone], out Tracked tracked))
                    tracked.Enemy = null;
                m_Ids.Remove(gone);
            }

            foreach (GameObject enemy in room.AliveEnemies)
                Snap(frame, enemy, false);
            if (room.Boss)
                Snap(frame, room.Boss.gameObject, true);
            m_Frames.Add(frame);
        }

        static readonly List<GameObject> s_Gone = new List<GameObject>();

        void Snap(Frame frame, GameObject enemy, bool boss)
        {
            if (!IsAlive(enemy))
                return;
            if (!m_Ids.TryGetValue(enemy, out int id))
            {
                id = m_NextId++;
                m_Ids[enemy] = id;
                var stats = enemy.GetComponent<EnemyStats>();
                m_Tracked[id] = new Tracked
                {
                    Enemy = enemy,
                    Definition = stats ? stats.Definition : null,
                    Elite = stats ? stats.Elite : EliteModifier.None,
                    Boss = boss,
                    FirstSeen = Time.time,
                };
            }

            var health = enemy.GetComponent<Health>();
            frame.Enemies.Add(new EnemySnap
            {
                Id = id,
                Position = enemy.transform.position,
                Rotation = enemy.transform.rotation,
                Health = health ? health.CurrentHealth : 1f,
            });
        }

        static bool IsAlive(GameObject enemy)
        {
            if (!enemy || !enemy.activeInHierarchy)
                return false;
            var health = enemy.GetComponent<Health>();
            return !health || health.CurrentHealth > 0f;
        }

        void Clear()
        {
            foreach (Frame frame in m_Frames)
                m_FramePool.Push(frame);
            m_Frames.Clear();
            m_Ids.Clear();
            m_Tracked.Clear();
            m_Room = null;
        }

        // ---------- volta ----------

        public bool Rewind(float seconds, float playback)
        {
            if (!CanRewind || !m_Room)
                return false;
            StartCoroutine(RunRewind(Mathf.Max(0.2f, playback)));
            return true;
        }

        IEnumerator RunRewind(float playback)
        {
            Rewinding = true;
            Progress = 0f;
            CombatTiming.WorldSpeed = 0f;
            AbilityFx.RewindBegin(playback);

            // O jogador fica parado e intocável enquanto a fita volta
            bool wasInvincible = m_Health && m_Health.Invincible;
            if (m_Health)
                m_Health.Invincible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            // O controle do Microgame aplicaria gravidade e movimento por cima da posição gravada
            bool controllerWasOn = m_Controller && m_Controller.enabled;
            if (controllerWasOn)
                m_Controller.enabled = false;

            RoomManager room = m_Room;
            Frame oldest = m_Frames[0];
            int last = m_Frames.Count - 1;

            for (float t = 0f; t < playback; t += Time.deltaTime)
            {
                // Mais devagar no começo e no fim, como uma fita sendo puxada
                float u = t / playback;
                Progress = u;
                float k = u * u * (3f - 2f * u);
                float index = Mathf.Lerp(last, 0f, k);
                int a = Mathf.FloorToInt(index), b = Mathf.Min(a + 1, last);
                Apply(room, m_Frames[a], m_Frames[b], index - a);
                yield return null;
            }

            Apply(room, oldest, oldest, 0f);
            Finish(room, oldest);
            if (controllerWasOn && m_Controller)
                m_Controller.enabled = true;

            if (m_Health)
                m_Health.Invincible = wasInvincible;
            if (!GameMenusOpen())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Clear();
            Progress = 1f;
            CombatTiming.WorldSpeed = 1f;
            Rewinding = false;
            AbilityFx.RewindEnd();
        }

        static bool GameMenusOpen() => Time.timeScale <= 0f;

        // Põe o mundo no instante entre 'from' (mais antigo) e 'to' (mais novo), a fração 't' de 'from' até 'to'
        void Apply(RoomManager room, Frame from, Frame to, float t)
        {
            float time = Mathf.Lerp(from.Time, to.Time, t);

            // Jogador
            var controller = m_Controller;
            transform.position = Vector3.Lerp(from.PlayerPosition, to.PlayerPosition, t);
            if (controller)
                controller.CharacterVelocity = Vector3.zero;
            if (m_Health)
                m_Health.CurrentHealth = Mathf.Max(1f, Mathf.Lerp(from.PlayerHealth, to.PlayerHealth, t));
            if (TimeManager.Instance)
                TimeManager.Instance.SetMultiplier(Mathf.Lerp(from.Multiplier, to.Multiplier, t));

            // Quem surgiu depois deste instante sai e volta para a cota
            foreach (Tracked tracked in m_Tracked.Values)
            {
                if (tracked.Removed || tracked.Boss || tracked.FirstSeen <= time + 0.001f)
                    continue;
                tracked.Removed = true;
                if (tracked.Enemy)
                {
                    AbilityFx.Vanish(tracked.Enemy.transform.position);
                    m_Ids.Remove(tracked.Enemy);
                    room.RewindUnspawn(tracked.Enemy);
                }

                tracked.Enemy = null;
            }

            // Inimigos do instante: no lugar, com a vida de então (quem morreu depois volta à vida)
            foreach (EnemySnap snap in from.Enemies)
            {
                if (!m_Tracked.TryGetValue(snap.Id, out Tracked tracked) || tracked.Removed)
                    continue;
                Vector3 position = snap.Position;
                Quaternion rotation = snap.Rotation;
                float health = snap.Health;
                if (TryFind(to, snap.Id, out EnemySnap next))
                {
                    position = Vector3.Lerp(snap.Position, next.Position, t);
                    rotation = Quaternion.Slerp(snap.Rotation, next.Rotation, t);
                    health = Mathf.Lerp(snap.Health, next.Health, t);
                }

                if (!tracked.Enemy)
                {
                    if (tracked.Boss || !tracked.Definition)
                        continue;
                    tracked.Enemy = room.RewindRevive(tracked.Definition, tracked.Elite, position, rotation, health);
                    if (!tracked.Enemy)
                        continue;
                    m_Ids[tracked.Enemy] = snap.Id;
                    AbilityFx.Revive(position);
                }

                Place(tracked.Enemy, position, rotation, health);
            }

            Physics.SyncTransforms();
        }

        static bool TryFind(Frame frame, int id, out EnemySnap snap)
        {
            foreach (EnemySnap s in frame.Enemies)
            {
                if (s.Id == id)
                {
                    snap = s;
                    return true;
                }
            }

            snap = default;
            return false;
        }

        static void Place(GameObject enemy, Vector3 position, Quaternion rotation, float health)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(position);
                agent.velocity = Vector3.zero;
            }
            else
                enemy.transform.position = position;
            enemy.transform.rotation = rotation;

            var h = enemy.GetComponent<Health>();
            if (h && h.CurrentHealth > 0f)
                h.CurrentHealth = Mathf.Clamp(health, 1f, h.MaxHealth);
        }

        // O fim da volta: Cronitas, cadáveres, cristais e tudo o que estava no ar no trecho
        void Finish(RoomManager room, Frame oldest)
        {
            PlayerWallet wallet = PlayerWallet.Instance;
            if (wallet)
                wallet.Restore(oldest.Cronitas, wallet.RerollTokens);
            CronitaPickup.ReleaseSince(oldest.Time);
            EnemySpriteCorpse.ClearSince(oldest.Time);
            ClearHostiles();

            if (m_Controller)
                m_Controller.CharacterVelocity = Vector3.zero;

            foreach (Tracked tracked in m_Tracked.Values)
            {
                if (tracked.Enemy)
                    tracked.Enemy.SendMessage("OnTimeRewound", SendMessageOptions.DontRequireReceiver);
            }
        }

        // Tiros e ataques dos inimigos e dos chefes que estavam no ar somem (eram do futuro)
        static void ClearHostiles()
        {
            foreach (ZangaoProjectile p in FindObjectsByType<ZangaoProjectile>(FindObjectsInactive.Exclude))
                PrefabPool.Release(p.gameObject);
            EnemyBolt.ClearAll();
            EnemyMortar.ClearAll();
            SporeCloud.ClearAll();
            EnemyFxBurst.ClearAll();
            foreach (BossProjectile p in FindObjectsByType<BossProjectile>(FindObjectsInactive.Exclude))
                PrefabPool.Release(p.gameObject);
            foreach (AcidPuddle p in FindObjectsByType<AcidPuddle>(FindObjectsInactive.Exclude))
                PrefabPool.Release(p.gameObject);
            foreach (GroundTelegraph p in FindObjectsByType<GroundTelegraph>(FindObjectsInactive.Exclude))
                PrefabPool.Release(p.gameObject);
            foreach (Shockwave p in FindObjectsByType<Shockwave>(FindObjectsInactive.Exclude))
                Destroy(p.gameObject);
            foreach (SweepBeam p in FindObjectsByType<SweepBeam>(FindObjectsInactive.Exclude))
                Destroy(p.gameObject);
            foreach (DelayedBlast p in FindObjectsByType<DelayedBlast>(FindObjectsInactive.Exclude))
                Destroy(p.gameObject);
            // Projéteis do Microgame dos inimigos (os modelos do TimeScaledProjectiles têm o WorldSpeedProjectile)
            foreach (WorldSpeedProjectile p in FindObjectsByType<WorldSpeedProjectile>(FindObjectsInactive.Exclude))
                Destroy(p.gameObject);
        }
    }
}
