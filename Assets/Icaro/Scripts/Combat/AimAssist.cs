using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icaro
{
    // Assistência de mira do jogador. A zona é um círculo no centro da tela com raio em fração da altura da tela
    // (AimAssistSettings.ZoneRadius, o mesmo valor que dimensiona os colchetes do AimAssistHUD). No instante do
    // disparo, o HomingProjectile pede o alvo (TryPickTarget): se o raio do centro da tela já acerta um inimigo, é
    // ele; senão, entre os inimigos vivos da sala atual (RoomManager.AliveEnemies e o chefe) cujo ponto de mira está
    // na frente da câmera, dentro da zona, dentro do alcance e com linha de visão livre do cano, ganha o mais
    // próximo do centro da zona, com um pequeno desempate pela distância no mundo. Sem alvo, o tiro sai reto para
    // o ponto do centro da tela (CenterDirection). A cada frame também calcula um alvo de prévia (PreviewTarget),
    // só para o feedback do HUD. O PlayerStats adiciona este componente ao jogador.
    public class AimAssist : MonoBehaviour
    {
        public AimAssistSettings Settings;

        // Inimigo que seria o alvo agora (feedback dos colchetes e destaque); null sem alvo válido
        public Health PreviewTarget { get; private set; }

        // Alvos fora das salas que a assistência também considera (alvos de treino do WeaponTestMode)
        public static readonly List<Health> ExtraTargets = new List<Health>();

        public Camera Camera => m_Weapons && m_Weapons.WeaponCamera ? m_Weapons.WeaponCamera : Camera.main;

        const float k_CenterRayRange = 1000f;

        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        PlayerWeaponsManager m_Weapons;

        void Awake()
        {
            m_Weapons = GetComponent<PlayerWeaponsManager>();
        }

        void Start()
        {
            if (!Settings)
            {
                var balance = GetComponent<PlayerBalance>();
                Settings = balance && balance.Balance ? balance.Balance.AimAssist : null;
            }
            // Cópia em jogo: as configurações do jogador mudam esta cópia, nunca o asset
            Settings = Settings ? Instantiate(Settings) : ScriptableObject.CreateInstance<AimAssistSettings>();

            if (!FindAnyObjectByType<AimAssistHUD>())
                new GameObject("AimAssistHUD").AddComponent<AimAssistHUD>().Assist = this;
        }

        void Update()
        {
            Settings.Enabled = GameSettings.AimAssist;
            Settings.Intensity = GameSettings.AimAssistIntensity;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Settings.DebugKey].wasPressedThisFrame)
                Settings.DebugView = !Settings.DebugView;

            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            Vector3 origin = weapon && weapon.WeaponMuzzle ? weapon.WeaponMuzzle.position : transform.position;
            // Arma sem assistência (Liberdade): os colchetes não travam em ninguém
            var icaro = weapon ? weapon.GetComponent<IcaroWeapon>() : null;
            bool assisted = !icaro || !icaro.Definition || icaro.Definition.AimMode != WeaponAimMode.None;
            PreviewTarget = assisted ? FindTarget(origin) : null;
        }

        // Alvo para um tiro que sai de 'origin' (cano). Falso sem alvo (assistência desligada ou nada na zona).
        public bool TryPickTarget(Vector3 origin, out Health target)
        {
            target = FindTarget(origin);
            return target;
        }

        Health FindTarget(Vector3 origin)
        {
            float zone = Settings ? Settings.EffectiveZoneRadius : 0f;
            Camera camera = Camera;
            if (zone <= 0f || !camera)
                return null;

            // Prioridade: o raio do centro da tela já acerta um inimigo
            Transform view = camera.transform;
            if (Physics.Raycast(view.position, view.forward, out RaycastHit direct, Settings.MaxRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Health health = EnemyHealth(direct.collider);
                if (health)
                    return health;
            }

            Health best = null;
            float bestScore = float.MaxValue;
            RoomManager room = RoomManager.Active;
            if (room)
            {
                IReadOnlyList<GameObject> enemies = room.AliveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject enemy = enemies[i];
                    if (enemy && enemy.activeInHierarchy)
                        Consider(enemy.GetComponent<Health>(), camera, origin, zone, ref best, ref bestScore);
                }

                if (room.Boss)
                    Consider(room.Boss, camera, origin, zone, ref best, ref bestScore);
            }

            foreach (Health extra in ExtraTargets)
                Consider(extra, camera, origin, zone, ref best, ref bestScore);
            return best;
        }

        void Consider(Health health, Camera camera, Vector3 origin, float zone, ref Health best, ref float bestScore)
        {
            if (!health || health.CurrentHealth <= 0f)
                return;

            Vector3 aim = AimPointOf(health);
            float worldDistance = Vector3.Distance(camera.transform.position, aim);
            if (worldDistance > Settings.MaxRange)
                return;

            // Distância ao centro da tela, em frações da altura (círculo na tela, em qualquer resolução)
            Vector3 viewport = camera.WorldToViewportPoint(aim);
            if (viewport.z <= 0f)
                return;
            float dx = (viewport.x - 0.5f) * camera.aspect;
            float dy = viewport.y - 0.5f;
            float screenDistance = Mathf.Sqrt(dx * dx + dy * dy);
            if (screenDistance > zone)
                return;

            float score = screenDistance / zone + Settings.WorldDistanceTieBreak * worldDistance / Settings.MaxRange;
            if (score >= bestScore || !HasLineOfSight(origin, aim, health))
                return;

            best = health;
            bestScore = score;
        }

        // Ponto de mira: o AimPoint do Actor (centro do corpo) ou o centro do colisor
        public Vector3 AimPointOf(Health health)
        {
            var actor = health.GetComponent<Actor>();
            if (actor && actor.AimPoint)
                return actor.AimPoint.position;
            var collider = health.GetComponentInChildren<Collider>();
            return collider ? collider.bounds.center : health.transform.position + Vector3.up;
        }

        // Linha livre de 'from' até 'to': só paredes e cenário bloqueiam (o alvo, outros inimigos e o jogador não)
        public bool HasLineOfSight(Vector3 from, Vector3 to, Health target)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 0.01f)
                return true;

            int count = Physics.RaycastNonAlloc(from, offset / distance, s_Hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = s_Hits[i].collider;
                if (collider.GetComponentInParent<EnemyStats>() || collider.GetComponentInParent<Health>() == target ||
                    collider.GetComponentInParent<PlayerCharacterController>())
                    continue;
                return false;
            }

            return true;
        }

        // Direção de um tiro sem alvo: do cano até o ponto que o centro da tela acerta (o tiro sai reto no centro)
        public Vector3 CenterDirection(Vector3 origin)
        {
            Camera camera = Camera;
            if (!camera)
                return transform.forward;

            Transform view = camera.transform;
            Vector3 point = view.position + view.forward * k_CenterRayRange;
            int count = Physics.RaycastNonAlloc(view.position, view.forward, s_Hits, k_CenterRayRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (s_Hits[i].distance < nearest && !s_Hits[i].collider.GetComponentInParent<PlayerCharacterController>())
                {
                    nearest = s_Hits[i].distance;
                    point = s_Hits[i].point;
                }
            }

            Vector3 direction = point - origin;
            // Ponto colado no cano (parede encostada): sai na direção da câmera
            if (direction.sqrMagnitude < 0.25f || Vector3.Dot(direction.normalized, view.forward) < 0.5f)
                return view.forward;
            return direction.normalized;
        }

        static Health EnemyHealth(Collider collider)
        {
            var stats = collider.GetComponentInParent<EnemyStats>();
            var health = stats ? stats.GetComponent<Health>() : null;
            return health && health.CurrentHealth > 0f ? health : null;
        }
    }
}
