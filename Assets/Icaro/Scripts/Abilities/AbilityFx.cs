using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Icaro
{
    // Efeitos de tela e sons das habilidades de tempo (criado pelo PlayerAbilities), em tempo sem escala e multiplicados
    // pelos "efeitos de tempo na tela" das configurações:
    // - câmera lenta: a cena perde cor e puxa para um lilás frio, com vinheta violeta, um pouco de aberração e a lente
    //   esticando ao entrar; um "vuuum" grave descendo com o coração batendo, e subindo ao sair (no último segundo a
    //   vinheta pisca para avisar que vai acabar);
    // - voltar no tempo: a cena fica quase sem cor e ciano, com aberração forte, a lente puxando, faixas de fita
    //   correndo pela tela e o som da fita rebobinando; um relógio ciano marca cada inimigo que volta à vida ou some.
    public class AbilityFx : MonoBehaviour
    {
        static AbilityFx s_Instance;

        public static float SlowProgress { get; set; }

        Volume m_SlowVolume, m_RewindVolume;
        LensDistortion m_SlowLens, m_RewindLens;
        Vignette m_SlowVignette;
        RawImage m_Tape;

        float m_SlowTarget, m_Slow;
        bool m_SlowEnding;
        float m_SlowLensKick;
        float m_RewindLength = 1f, m_RewindAge = 99f;

        void Awake()
        {
            s_Instance = this;
            m_SlowVolume = CreateVolume("Volume_CameraLenta", 62f, out VolumeProfile slow);
            var adjust = slow.Add<ColorAdjustments>(true);
            adjust.saturation.Override(-45f);
            adjust.colorFilter.Override(new Color(0.86f, 0.82f, 1.08f));
            adjust.contrast.Override(8f);
            m_SlowVignette = slow.Add<Vignette>(true);
            m_SlowVignette.color.Override(new Color(0.35f, 0.15f, 0.6f));
            m_SlowVignette.intensity.Override(0.38f);
            m_SlowVignette.smoothness.Override(0.6f);
            slow.Add<ChromaticAberration>(true).intensity.Override(0.35f);
            m_SlowLens = slow.Add<LensDistortion>(true);

            m_RewindVolume = CreateVolume("Volume_VoltarTempo", 64f, out VolumeProfile rewind);
            var rewindAdjust = rewind.Add<ColorAdjustments>(true);
            rewindAdjust.saturation.Override(-70f);
            rewindAdjust.colorFilter.Override(new Color(0.75f, 1f, 1.1f));
            rewindAdjust.contrast.Override(18f);
            var rewindVignette = rewind.Add<Vignette>(true);
            rewindVignette.color.Override(new Color(0.1f, 0.6f, 0.7f));
            rewindVignette.intensity.Override(0.45f);
            rewind.Add<ChromaticAberration>(true).intensity.Override(1f);
            m_RewindLens = rewind.Add<LensDistortion>(true);

            CreateTape();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            if (m_SlowVolume && m_SlowVolume.profile)
                Destroy(m_SlowVolume.profile);
            if (m_RewindVolume && m_RewindVolume.profile)
                Destroy(m_RewindVolume.profile);
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

        // Faixas de fita: linhas horizontais claras e escuras que correm para cima, por cima do jogo e abaixo dos menus
        void CreateTape()
        {
            Canvas canvas = ItemUI.CreateCanvas(transform, "FitaDoTempo", 41);
            var go = new GameObject("Fita", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            m_Tape = go.AddComponent<RawImage>();
            m_Tape.texture = TapeTexture();
            m_Tape.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            m_Tape.enabled = false;
        }

        // ---------- chamadas das habilidades ----------

        public static void SlowBegin()
        {
            UiSounds.Play(TimeSounds.SlowIn, 0.9f);
            SlowProgress = 0f;
            if (!s_Instance)
                return;
            s_Instance.m_SlowTarget = 1f;
            s_Instance.m_SlowEnding = false;
            s_Instance.m_SlowLensKick = 1f;
        }

        public static void SlowEnding()
        {
            UiSounds.Play(TimeSounds.SlowOut, 0.8f);
            if (s_Instance)
                s_Instance.m_SlowEnding = true;
        }

        public static void SlowEnd()
        {
            if (s_Instance)
                s_Instance.m_SlowTarget = 0f;
        }

        public static void RewindBegin(float playback)
        {
            UiSounds.Play(TimeSounds.Tape, 0.9f, TimeSounds.TapeLength / Mathf.Max(0.2f, playback));
            if (!s_Instance)
                return;
            s_Instance.m_RewindLength = playback;
            s_Instance.m_RewindAge = 0f;
            s_Instance.m_Tape.enabled = GameSettings.ScreenEffects > 0f;
        }

        public static void RewindEnd()
        {
            UiSounds.Play(TimeSounds.Unfreeze, 0.6f, 1.15f);
            if (!s_Instance)
                return;
            s_Instance.m_RewindAge = s_Instance.m_RewindLength;
        }

        // Um inimigo voltou à vida ou sumiu (surgiu dentro do trecho voltado)
        public static void Revive(Vector3 position) => TimeRewindBurst.Spawn(position + Vector3.up * 1.2f, 0.4f);
        public static void Vanish(Vector3 position) => TimeRewindBurst.Spawn(position + Vector3.up * 1.2f, 0.25f);

        // ---------- quadro a quadro ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float effects = GameSettings.ScreenEffects;

            // Câmera lenta
            m_Slow = Mathf.MoveTowards(m_Slow, m_SlowTarget, dt * (m_SlowTarget > m_Slow ? 5f : 2f));
            m_SlowLensKick = Mathf.MoveTowards(m_SlowLensKick, 0f, dt * 2.5f);
            float blink = m_SlowEnding ? 0.75f + 0.25f * Mathf.Cos(Time.unscaledTime * 18f) : 1f;
            if (m_SlowVolume)
            {
                m_SlowVolume.weight = m_Slow * effects * blink;
                m_SlowLens.intensity.Override(-0.35f * m_SlowLensKick * m_SlowLensKick);
            }

            // Voltar no tempo: forte durante a volta e some logo depois
            m_RewindAge += dt;
            float u = m_RewindAge / Mathf.Max(0.01f, m_RewindLength);
            float rewind = u < 1f ? Mathf.Clamp01(m_RewindAge / 0.12f) : Mathf.Clamp01(1f - (u - 1f) * m_RewindLength / 0.35f);
            if (m_RewindVolume)
            {
                m_RewindVolume.weight = rewind * effects;
                m_RewindLens.intensity.Override(u < 1f ? 0.3f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) : 0f);
            }

            if (m_Tape && m_Tape.enabled)
            {
                if (rewind <= 0f)
                    m_Tape.enabled = false;
                else
                {
                    // As faixas correm para cima, tremendo
                    float scroll = Time.unscaledTime * 2.3f + Mathf.Sin(Time.unscaledTime * 37f) * 0.02f;
                    m_Tape.uvRect = new Rect(0f, scroll, 1f, Screen.height / 360f);
                    m_Tape.color = new Color(0.7f, 1f, 1f, 0.22f * rewind * effects);
                }
            }
        }

        // 4 x 128: faixas claras finas e grossas espalhadas, como uma fita rebobinando
        static Texture2D TapeTexture()
        {
            const int h = 128;
            var texture = new Texture2D(4, h, TextureFormat.RGBA32, false)
            {
                name = "Fita_Tempo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };
            var pixels = new Color32[4 * h];
            var random = new System.Random(911);
            for (int y = 0; y < h; y++)
            {
                byte a = 0;
                double r = random.NextDouble();
                if (r < 0.08)
                    a = 255;
                else if (r < 0.2)
                    a = 90;
                else if (y % 2 == 0)
                    a = 25;
                for (int x = 0; x < 4; x++)
                    pixels[y * 4 + x] = new Color32(255, 255, 255, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
