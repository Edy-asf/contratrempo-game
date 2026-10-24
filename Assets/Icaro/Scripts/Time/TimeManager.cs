using UnityEngine;

namespace Icaro
{
    // Relógio de Kairós: multiplicador = min(1 + t / SecondsPerStep, Ceiling), com t em segundos desde o último
    // reset ou redução. Cresce +1,0x a cada SecondsPerStep segundos até o teto. Afeta só os inimigos (via
    // EnemyTimeScaler e as IAs do Ícaro), nunca o jogador nem Time.timeScale.
    public class TimeManager : MonoBehaviour
    {
        // Valores padrão (o Inspector de cada TimeManager pode mudar)
        public const float DefaultSecondsPerStep = 120f;
        public const float DefaultMaxMultiplier = 6f;

        public static TimeManager Instance { get; private set; }

        // Atalhos seguros para quem só quer ler (valores padrão se não houver TimeManager na cena)
        public static float CurrentMultiplier => Instance ? Instance.Multiplier : 1f;
        public static float CurrentMaxMultiplier => Instance ? Instance.MaxMultiplier : DefaultMaxMultiplier;

        [Tooltip("Segundos por +1,0x: tempo para o multiplicador subir 1,0x")]
        [Min(1f)] public float SecondsPerStep = DefaultSecondsPerStep;

        [Tooltip("Teto do multiplicador (o padrão é 6,0x)")]
        [Min(1f)] public float Ceiling = DefaultMaxMultiplier;

        [Header("Debug")]
        [Tooltip("Acelera o relógio de Kairós para testes (1 = normal). Não afeta o jogo em si, só a passagem de t. " +
                 "A 30x, o multiplicador vai de 1,0x ao teto de 6,0x em 20 s.")]
        [Range(1f, 30f)]
        public float DebugTimeScale = 1f;

        [Tooltip("Congela o relógio (útil para testar um multiplicador fixo)")]
        public bool DebugPaused;

        [Tooltip("Segundos desde o último reset ou redução (somente leitura em jogo)")]
        [SerializeField] float m_Elapsed;

        public float Elapsed => m_Elapsed;

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
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (DebugPaused)
                return;

            m_Elapsed = Mathf.Min(m_Elapsed + Time.deltaTime * DebugTimeScale * GrowthRate, MaxElapsed);
        }

        [ContextMenu("Reset Time")]
        public void ResetTime()
        {
            m_Elapsed = 0f;
        }

        // Diminui o multiplicador em 'amount' (Metrônomo 3,0x, fragmento 0,5x), nunca abaixo de 1,0x, e recalcula
        // t para continuar na mesma reta
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
