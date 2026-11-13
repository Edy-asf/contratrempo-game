using UnityEngine;

namespace Icaro
{
    // Relógio de Kairós: multiplicador = min(1 + t / SecondsPerStep, Ceiling), com t em segundos desde o último
    // reset ou redução. Cresce +1,0x a cada SecondsPerStep segundos até o teto, mas só durante a luta (Running).
    // Ordem de uma sala (RoomManager): o mundo acorda em 1,0x (WakeWorld, sem crescer), o domo sobe, a faixa da sala
    // aparece e, quando os inimigos começam a surgir, o multiplicador começa a crescer (BeginCombat). No último abate
    // ele para de crescer (EndCombat), o domo se desfaz, o multiplicador desce contando até 1,0x (DrainToOne) e o
    // mundo congela (FreezeWorld) antes das cartas. Cada abate de inimigo da sala tira do
    // multiplicador a vida da ficha dele dividida por HealthPerStep (RewardKill). Afeta só os inimigos (via
    // EnemyTimeScaler e as IAs do Ícaro), nunca o jogador nem Time.timeScale.
    public class TimeManager : MonoBehaviour
    {
        // Valores padrão (o Inspector de cada TimeManager pode mudar)
        public const float DefaultSecondsPerStep = 20f;
        public const float DefaultHealthPerStep = 2000f;
        public const float DefaultMaxMultiplier = 6f;

        public static TimeManager Instance { get; private set; }

        // Atalhos seguros para quem só quer ler (valores padrão se não houver TimeManager na cena)
        public static float CurrentMultiplier => Instance ? Instance.Multiplier : 1f;
        public static float CurrentMaxMultiplier => Instance ? Instance.MaxMultiplier : DefaultMaxMultiplier;

        [Tooltip("Segundos por +1,0x: tempo para o multiplicador subir 1,0x")]
        [Min(1f)] public float SecondsPerStep = DefaultSecondsPerStep;

        [Tooltip("Teto do multiplicador (o padrão é 6,0x)")]
        [Min(1f)] public float Ceiling = DefaultMaxMultiplier;

        [Header("Abates")]
        [Tooltip("Vida de ficha (antes da escala por sala, com o fator de elite) que um abate precisa ter para tirar " +
                 "1,0x do multiplicador: um inimigo de 420 de vida com 2000 tira 0,21x. A ficha de cada inimigo pode " +
                 "ter um valor próprio (EnemyDefinition.TimeReduction)")]
        [Min(1f)] public float HealthPerStep = DefaultHealthPerStep;

        [Header("Debug")]
        [Tooltip("Acelera o relógio de Kairós para testes (1 = normal). Não afeta o jogo em si, só a passagem de t. " +
                 "A 30x, o multiplicador vai de 1,0x ao teto de 6,0x em 20 s.")]
        [Range(1f, 30f)]
        public float DebugTimeScale = 1f;

        [Tooltip("Congela o relógio (útil para testar um multiplicador fixo)")]
        public bool DebugPaused;

        [Tooltip("O relógio anda também fora de combate (como antes); só para testes")]
        public bool DebugRunOutsideCombat;

        [Tooltip("Segundos desde o último reset ou redução (somente leitura em jogo)")]
        [SerializeField] float m_Elapsed;

        public float Elapsed => m_Elapsed;

        // Multiplicador crescendo (luta em andamento); fora dela fica parado
        public bool Running { get; private set; }
        public static bool CurrentlyRunning => Instance && (Instance.Running || Instance.DebugRunOutsideCombat);

        // Mundo andando (KairosClock): acorda antes do domo e congela depois que o multiplicador volta a 1,0x
        public bool WorldAwake { get; private set; }
        public static bool WorldAwakeNow => Instance && (Instance.WorldAwake || Instance.Running ||
                                                         Instance.DebugRunOutsideCombat);

        // O mundo acordou (true) ou congelou (false), para os efeitos das transições
        public static event System.Action<bool> OnWorldChanged;

        // O multiplicador começou a descer contando até 1,0x: quanto vai descer e em quantos segundos
        public static event System.Action<float, float> OnDrain;

        // Redução de um abate ou de uma fatia de vida de chefe: quanto tirou e de onde (para o feedback, depois)
        public static event System.Action<float, Vector3> OnReduced;

        // O multiplicador começou (true) ou parou (false) de crescer
        public static event System.Action<bool> OnCombatChanged;

        // Ritmo em que t avança (1 = normal). O atributo de item TimeSlow o reduz (sem item hoje); não é salvo.
        public float GrowthRate { get; set; } = 1f;

        public float MaxMultiplier => Mathf.Max(1f, Ceiling);
        public float Multiplier => Mathf.Min(1f + m_Elapsed / SecondsPerStep, MaxMultiplier);

        // Segundos em que o multiplicador atinge o teto
        float MaxElapsed => (MaxMultiplier - 1f) * SecondsPerStep;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning("Há mais de um TimeManager na cena; este será destruído.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Leitura do multiplicador além do HUD: atmosfera (névoa, esporos, pós-processamento) e música dinâmica
            if (!GetComponent<TimeAtmosphere>())
                gameObject.AddComponent<TimeAtmosphere>();
            if (!GetComponent<TimeMusic>())
                gameObject.AddComponent<TimeMusic>();
            // Mundo congelado fora de combate e os efeitos das transições e do tempo voltando
            if (!GetComponent<WorldFreeze>())
                gameObject.AddComponent<WorldFreeze>();
            if (!GetComponent<TimeTransitionFx>())
                gameObject.AddComponent<TimeTransitionFx>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (DebugPaused || !(Running || DebugRunOutsideCombat))
                return;

            // Na câmera lenta das habilidades o multiplicador cresce no ritmo do mundo (CombatTiming.WorldSpeed)
            m_Elapsed = Mathf.Min(m_Elapsed + CombatTiming.DeltaTime * DebugTimeScale * GrowthRate, MaxElapsed);
        }

        // O mundo volta a andar em 1,0x, sem o multiplicador crescer (antes do domo subir)
        public void WakeWorld()
        {
            if (WorldAwake)
                return;
            WorldAwake = true;
            OnWorldChanged?.Invoke(true);
        }

        // O mundo congela (depois que o multiplicador voltou a 1,0x, antes das cartas)
        public void FreezeWorld()
        {
            m_Elapsed = 0f;
            Running = false;
            if (!WorldAwake)
                return;
            WorldAwake = false;
            OnWorldChanged?.Invoke(false);
        }

        // Os inimigos começaram a surgir: o multiplicador cresce a partir de 1,0x
        public void BeginCombat()
        {
            WakeWorld();
            if (Running)
                return;
            m_Elapsed = 0f;
            Running = true;
            OnCombatChanged?.Invoke(true);
        }

        // O último inimigo morreu: o multiplicador para de crescer (fica no valor até DrainToOne)
        public void EndCombat()
        {
            if (!Running)
                return;
            Running = false;
            OnCombatChanged?.Invoke(false);
        }

        // Desce contando até 1,0x em duration segundos (tempo sem escala), devagar no começo e no fim
        public System.Collections.IEnumerator DrainToOne(float duration)
        {
            float start = Multiplier;
            if (start > 1.005f && duration > 0f)
            {
                OnDrain?.Invoke(start - 1f, duration);
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float u = t / duration;
                    SetMultiplier(Mathf.Lerp(start, 1f, u * u * (3f - 2f * u)));
                    yield return null;
                }
            }

            m_Elapsed = 0f;
        }

        // Quanto um abate tira do multiplicador: o valor próprio da ficha ou a vida dela (com o fator de elite, sem a
        // escala por sala) dividida por HealthPerStep
        public float KillReduction(EnemyDefinition definition, float baseHealth)
        {
            if (definition && definition.TimeReduction > 0f)
                return definition.TimeReduction * Mathf.Max(1f, baseHealth / Mathf.Max(1f, definition.MaxHealth));
            return Mathf.Max(0f, baseHealth) / Mathf.Max(1f, HealthPerStep);
        }

        // Abate de um inimigo da sala (RoomManager)
        public static void RewardKill(EnemyDefinition definition, float baseHealth, Vector3 position)
        {
            if (Instance)
                Instance.RewardTime(Instance.KillReduction(definition, baseHealth), position);
        }

        // Tira 'amount' do multiplicador com o aviso para o feedback (abates e fatias de chefe); só durante a luta
        public void RewardTime(float amount, Vector3 position)
        {
            if (amount <= 0f || !(Running || DebugRunOutsideCombat))
                return;
            Reduce(amount);
            OnReduced?.Invoke(amount, position);
        }

        [ContextMenu("Reset Time")]
        public void ResetTime()
        {
            m_Elapsed = 0f;
        }

        // Diminui o multiplicador em 'amount', nunca abaixo de 1,0x, e recalcula t para continuar na mesma reta
        public void Reduce(float amount)
        {
            SetMultiplier(Multiplier - amount);
        }

        // Põe o multiplicador num valor (entre 1,0x e o teto) e ajusta t para a fórmula
        public void SetMultiplier(float multiplier)
        {
            float target = Mathf.Clamp(multiplier, 1f, MaxMultiplier);
            m_Elapsed = (target - 1f) * SecondsPerStep;
        }

        [ContextMenu("Reduce 0.5x")]
        void ReduceHalf() => Reduce(0.5f);

        [ContextMenu("Set 3x")]
        void SetThree() => SetMultiplier(3f);

        [ContextMenu("Set Max")]
        void SetMax() => SetMultiplier(MaxMultiplier);
    }
}
