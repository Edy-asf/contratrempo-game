using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Ao ser tocado pelo jogador, reduz o multiplicador de tempo em Reduction (3,0x; nunca abaixo de 1,0x). Com
    // SingleUse (padrão), funciona uma vez só e depois fica apagado; sem ele, volta a ficar pronto depois de Cooldown
    // segundos. Os Metrônomos só aparecem depois de cada chefe (ShopDirector).
    [RequireComponent(typeof(Collider))]
    public class Metronome : MonoBehaviour
    {
        [Tooltip("Funciona uma vez só e depois fica apagado")]
        public bool SingleUse = true;

        [Tooltip("Segundos até o Metrônomo poder ser ativado de novo (só sem SingleUse)")]
        public float Cooldown = 10f;

        [Tooltip("Quanto o multiplicador cai ao ativar (nunca abaixo de 1,0x)")]
        [Min(0f)] public float Reduction = 3f;

        [Tooltip("Renderers que mudam de cor conforme o estado")]
        public Renderer[] Indicators;

        [Tooltip("Pêndulo que oscila no ritmo do multiplicador atual (opcional)")]
        public Transform Pendulum;

        [Tooltip("Luz pontual que ilumina o entorno conforme o estado (opcional)")]
        public Light IndicatorLight;

        [Tooltip("Feixe vertical visível de longe (opcional): pulsa no ritmo do pêndulo quando pronto, apagado " +
                 "na recarga")]
        public Renderer Beacon;

        [Tooltip("Cor do feixe quando pronto (alfa = intensidade)")]
        public Color BeaconColor = new Color(0.2f, 0.85f, 1f, 0.4f);

        [Tooltip("Intensidade da luz quando pronto")]
        public float LightIntensity = 3f;

        [Tooltip("Amplitude da oscilação do pêndulo, em graus")]
        public float PendulumAngle = 30f;

        [Tooltip("Oscilações por segundo a 1,0x")]
        public float PendulumFrequency = 1f;

        [Header("Cores")]
        public Color ReadyColor = new Color(0.2f, 0.9f, 1f);
        public Color CooldownColor = new Color(0.35f, 0.35f, 0.38f);

        [ColorUsage(true, true)] public Color ReadyEmission = new Color(0f, 1.5f, 2f);
        [ColorUsage(true, true)] public Color FlashEmission = new Color(4f, 6f, 8f);

        [Tooltip("Duração do clarão ao ativar")]
        public float FlashDuration = 0.6f;

        [Tooltip("Mensagem exibida no HUD ao ativar")]
        public string ActivationMessage = "Metrônomo ativado: tempo sincronizado";

        [Tooltip("Som ao ativar (opcional)")]
        public AudioClip ActivationSfx;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        float m_LastActivationTime = Mathf.NegativeInfinity;
        float m_PendulumPhase;
        MaterialPropertyBlock m_Block;
        MaterialPropertyBlock m_BeaconBlock;

        bool m_Used;

        public bool IsReady => !(SingleUse && m_Used) && Time.time >= m_LastActivationTime + Cooldown;

        // Metrônomos ativos na cena (marcador do HUD)
        public static readonly System.Collections.Generic.List<Metronome> All =
            new System.Collections.Generic.List<Metronome>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            // CharacterController só dispara triggers de forma confiável contra um Rigidbody (como no Pickup)
            var body = GetComponent<Rigidbody>();
            if (!body)
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            m_Block = new MaterialPropertyBlock();
            m_BeaconBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            UpdatePendulum();
            UpdateIndicators();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsReady || !other.GetComponentInParent<PlayerCharacterController>())
                return;

            Activate();
        }

        public void Activate()
        {
            m_LastActivationTime = Time.time;
            m_Used = true;

            if (TimeManager.Instance)
                TimeManager.Instance.Reduce(Reduction);
            else
                Debug.LogWarning("Metrônomo ativado, mas não há TimeManager na cena.", this);

            if (ActivationSfx)
                AudioUtility.CreateSFX(ActivationSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);

            if (!string.IsNullOrEmpty(ActivationMessage))
            {
                DisplayMessageEvent evt = Events.DisplayMessageEvent;
                evt.Message = ActivationMessage;
                evt.DelayBeforeDisplay = 0f;
                EventManager.Broadcast(evt);
            }
        }

        // Feixe: a cada passagem do pêndulo pelo centro, um brilho; mais rápido com o multiplicador (como o pêndulo)
        void UpdateBeacon()
        {
            if (!Beacon)
                return;

            float swing = Mathf.Abs(Mathf.Cos(m_PendulumPhase));
            Color color = BeaconColor;
            color.a *= IsReady ? 0.55f + 0.45f * swing * swing : 0.08f;
            m_BeaconBlock.SetColor(k_BaseColor, color);
            Beacon.SetPropertyBlock(m_BeaconBlock);
        }

        void UpdatePendulum()
        {
            if (!Pendulum)
                return;

            // O pêndulo acompanha o ritmo de Kairós: quanto maior o multiplicador, mais rápido ele bate
            m_PendulumPhase += Time.deltaTime * PendulumFrequency * TimeManager.CurrentMultiplier * Mathf.PI * 2f;
            Pendulum.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(m_PendulumPhase) * PendulumAngle);
        }

        void UpdateIndicators()
        {
            if (Indicators == null || Indicators.Length == 0)
                return;

            float sinceActivation = Time.time - m_LastActivationTime;
            Color baseColor;
            Color emission;

            if (sinceActivation < FlashDuration)
            {
                // Clarão logo após a ativação
                float t = sinceActivation / FlashDuration;
                baseColor = Color.Lerp(ReadyColor, CooldownColor, t);
                emission = Color.Lerp(FlashEmission, Color.black, t);
            }
            else if (SingleUse && m_Used)
            {
                // Usado: apagado de vez
                baseColor = CooldownColor;
                emission = Color.black;
            }
            else if (!IsReady)
            {
                // Recarga: volta gradualmente do cinza para a cor de pronto
                float t = Mathf.Clamp01(sinceActivation / Cooldown);
                baseColor = Color.Lerp(CooldownColor, ReadyColor, t * t);
                emission = Color.Lerp(Color.black, ReadyEmission, t * t) * 0.3f;
            }
            else
            {
                // Pronto: pulsa suavemente
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
                baseColor = ReadyColor;
                emission = ReadyEmission * pulse;
            }

            m_Block.SetColor(k_BaseColor, baseColor);
            m_Block.SetColor(k_EmissionColor, emission);
            foreach (var indicator in Indicators)
            {
                if (indicator)
                    indicator.SetPropertyBlock(m_Block);
            }

            UpdateBeacon();

            // A luz acompanha o brilho: forte no clarão, fraca na recarga, pulsando quando pronto
            if (IndicatorLight)
            {
                float glow = emission.maxColorComponent / Mathf.Max(ReadyEmission.maxColorComponent, 0.0001f);
                IndicatorLight.color = Color.Lerp(CooldownColor, ReadyColor, Mathf.Clamp01(glow));
                IndicatorLight.intensity = LightIntensity * Mathf.Clamp(glow, 0.15f, 3f);
            }
        }
    }
}
