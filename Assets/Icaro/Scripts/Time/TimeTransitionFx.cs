using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Icaro
{
    // Efeitos das transições do tempo (o TimeManager adiciona este componente):
    // - mundo congelado (fora de combate): a cena fica um pouco dessaturada e fria (Volume próprio, peso pelo
    //   KairosClock), voltando às cores quando a luta começa;
    // - congelar (sala limpa): som de fita parando com o tique final, uma onda fria que se abre do centro da tela e
    //   uma distorção de lente rápida;
    // - descongelar (o primeiro inimigo surge): o "taque" e a fita voltando, uma onda âmbar e a lente no sentido
    //   contrário;
    // - o tempo voltando (abate ou fatia de chefe): o relógio ciano no lugar (TimeRewindBurst), um tique ao contrário e
    //   uma pulsação ciana na tela, mais forte quanto mais o multiplicador caiu.
    // Tudo em tempo sem escala e multiplicado pelos "efeitos de tempo na tela" das configurações.
    public class TimeTransitionFx : MonoBehaviour
    {
        [Header("Mundo congelado")]
        [Tooltip("Força do visual frio com o mundo parado (0 = sem)")]
        [Range(0f, 1f)] public float FrozenLook = 0.7f;

        [Range(-100f, 0f)] public float FrozenSaturation = -35f;
        public Color FrozenFilter = new Color(0.86f, 0.94f, 1.06f);

        [Header("Pulsos")]
        public Color FreezeColor = new Color(0.62f, 0.86f, 1f);
        public Color UnfreezeColor = new Color(1f, 0.66f, 0.3f);
        public Color RewindColor = new Color(0.35f, 0.95f, 1f);

        [Tooltip("Segundos mínimos entre dois tiques de abate (grupos de Zangões morrendo juntos)")]
        [Min(0f)] public float RewindSoundGap = 0.07f;

        Volume m_FrozenVolume;
        Volume m_PulseVolume;
        ColorAdjustments m_Adjust;
        Vignette m_PulseVignette;
        LensDistortion m_Lens;
        ChromaticAberration m_Chromatic;

        RawImage m_Wave;
        float m_WaveAge = 99f;
        float m_WaveTime = 1f;
        float m_Pulse;
        float m_PulseDecay = 3f;
        float m_LensAmount;
        float m_LastRewindSound = -1f;

        void OnEnable()
        {
            TimeManager.OnWorldChanged += OnWorldChanged;
            TimeManager.OnReduced += OnReduced;
            TimeManager.OnDrain += OnDrain;
        }

        void OnDisable()
        {
            TimeManager.OnWorldChanged -= OnWorldChanged;
            TimeManager.OnReduced -= OnReduced;
            TimeManager.OnDrain -= OnDrain;
        }

        void Start()
        {
            m_FrozenVolume = CreateVolume("Volume_Congelado", 55f, out VolumeProfile frozen);
            m_Adjust = frozen.Add<ColorAdjustments>(true);
            m_Adjust.saturation.Override(FrozenSaturation);
            m_Adjust.colorFilter.Override(FrozenFilter);

            m_PulseVolume = CreateVolume("Volume_PulsoTempo", 60f, out VolumeProfile pulse);
            m_PulseVignette = pulse.Add<Vignette>(true);
            m_PulseVignette.intensity.Override(0.45f);
            m_PulseVignette.smoothness.Override(0.5f);
            m_Lens = pulse.Add<LensDistortion>(true);
            m_Chromatic = pulse.Add<ChromaticAberration>(true);
            m_Chromatic.intensity.Override(0.8f);

            CreateWave();
        }

        void OnDestroy()
        {
            if (m_FrozenVolume && m_FrozenVolume.profile)
                Destroy(m_FrozenVolume.profile);
            if (m_PulseVolume && m_PulseVolume.profile)
                Destroy(m_PulseVolume.profile);
        }

        Volume CreateVolume(string name, float priority, out VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
            return volume;
        }

        // Onda que se abre do centro da tela (anel suave numa imagem por cima do jogo, abaixo dos menus)
        void CreateWave()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "OndaDoTempo", 40);
            var go = new GameObject("Onda", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            m_Wave = go.AddComponent<RawImage>();
            m_Wave.texture = WaveTexture();
            m_Wave.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(256f, 256f);
            m_Wave.enabled = false;
        }

        // O multiplicador descendo até 1,0x no fim da sala: tiques acelerando e a tela pulsando em ciano
        void OnDrain(float amount, float duration)
        {
            UiSounds.Play(TimeSounds.Drain, 0.75f, TimeSounds.DrainLength / Mathf.Max(0.2f, duration));
            Pulse(RewindColor, Mathf.Lerp(0.25f, 0.5f, Mathf.Clamp01(amount / 4f)), 0.6f, -0.1f);
        }

        void OnWorldChanged(bool awake)
        {
            if (awake)
            {
                UiSounds.Play(TimeSounds.Unfreeze, 0.8f);
                StartWave(UnfreezeColor, 0.7f);
                Pulse(UnfreezeColor, 0.55f, 2.2f, 0.35f);
            }
            else
            {
                UiSounds.Play(TimeSounds.Freeze, 0.75f);
                StartWave(FreezeColor, 1.3f);
                Pulse(FreezeColor, 0.6f, 1.2f, -0.3f);
            }
        }

        void OnReduced(float amount, Vector3 position)
        {
            float k = Mathf.Clamp01(amount / 0.4f);
            TimeRewindBurst.Spawn(position, amount);
            if (Time.unscaledTime - m_LastRewindSound >= RewindSoundGap)
            {
                m_LastRewindSound = Time.unscaledTime;
                UiSounds.Play(TimeSounds.Rewind, Mathf.Lerp(0.45f, 0.9f, k), Mathf.Lerp(1.25f, 0.8f, k));
            }

            // Só os abates que tiram bastante mexem na tela inteira
            float strength = Mathf.Lerp(0.12f, 0.55f, k);
            if (strength > m_Pulse)
                Pulse(RewindColor, strength, 3.5f, -0.12f * k);
        }

        void Pulse(Color color, float strength, float decay, float lens)
        {
            m_Pulse = Mathf.Max(m_Pulse, strength);
            m_PulseDecay = decay;
            m_LensAmount = lens;
            m_PulseVignette.color.Override(color);
        }

        void StartWave(Color color, float time)
        {
            m_WaveAge = 0f;
            m_WaveTime = time;
            m_Wave.color = color;
            m_Wave.enabled = true;
        }

        void Update()
        {
            float effects = GameSettings.ScreenEffects;
            float dt = Time.unscaledDeltaTime;

            if (m_FrozenVolume)
                m_FrozenVolume.weight = (1f - KairosClock.Scale) * FrozenLook * effects;

            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, dt * m_PulseDecay * Mathf.Max(0.2f, m_Pulse));
            if (m_PulseVolume)
            {
                m_PulseVolume.weight = Mathf.Clamp01(m_Pulse) * effects;
                m_Lens.intensity.Override(m_LensAmount * Mathf.Clamp01(m_Pulse * 1.6f));
            }

            if (!m_Wave || !m_Wave.enabled)
                return;
            m_WaveAge += dt;
            float u = m_WaveAge / m_WaveTime;
            if (u >= 1f || effects <= 0f)
            {
                m_Wave.enabled = false;
                return;
            }

            // Cresce até cobrir a tela, sumindo
            float size = Mathf.Max(Screen.width, Screen.height) * 2.4f * (1f - Mathf.Pow(1f - u, 2f));
            m_Wave.rectTransform.sizeDelta = new Vector2(size, size);
            Color c = m_Wave.color;
            c.a = (1f - u) * 0.55f * effects;
            m_Wave.color = c;
        }

        // Anel suave de 256 px: borda nítida por fora e um rastro que some para dentro
        static Texture2D WaveTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Onda_Tempo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d > 1f ? 0f : d > 0.94f ? (1f - d) / 0.06f : Mathf.Pow(Mathf.Clamp01((d - 0.55f) / 0.39f), 3f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
