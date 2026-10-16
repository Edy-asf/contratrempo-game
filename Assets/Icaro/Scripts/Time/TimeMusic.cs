using System.Threading.Tasks;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Música do jogo inteiro, em camadas verticais: a faixa base (o tema do bioma, MusicSettings) toca sempre, e
    // camadas sincronizadas entram conforme o multiplicador de tempo sobe: relógio (sempre, mais alto com o
    // multiplicador), chimbais e palmas, linha acid, quebrada de caixas e lead com sirene. Todas as faixas começam juntas (PlayScheduled) e têm
    // o mesmo andamento e um número inteiro de compassos, então nunca saem de sincronia; o pitch de todas sobe junto
    // com o multiplicador (a música acelera). Fora de combate, as camadas rítmicas ficam mais baixas. Ao entrar num
    // bioma com outra faixa, troca com uma transição. As faixas compostas em código (MusicComposer) são renderizadas
    // em segundo plano no início, sem travar o jogo. Toca pelo mixer do Microgame (o volume do menu vale).
    // O TimeManager adiciona este componente.
    public class TimeMusic : MonoBehaviour
    {
        public MusicSettings Settings;

        static readonly MusicComposer.Layer[] k_Layers =
        {
            MusicComposer.Layer.Base, MusicComposer.Layer.Clock, MusicComposer.Layer.Hats, MusicComposer.Layer.Acid,
            MusicComposer.Layer.Breaks, MusicComposer.Layer.Lead,
        };

        // Um conjunto de faixas tocando (ou sendo preparado) para uma música
        class TrackSet
        {
            public MusicSettings.Track Track;
            public Task<float[][]> Rendering;
            public AudioSource[] Sources;
            public float Fade;
            public float FadeTarget = 1f;
        }

        TrackSet m_Current;
        TrackSet m_Next;
        string m_Biome;
        float m_Combat;

        // Posição musical em batidas da faixa atual (para o pulso visual); falso enquanto a música não começou
        public bool HasBeat => m_Current != null && m_Current.Sources != null && m_Current.Sources[0].isPlaying;

        public double BeatPosition
        {
            get
            {
                AudioSource source = m_Current.Sources[0];
                if (!source.clip)
                    return 0.0;
                return (double)source.timeSamples / source.clip.frequency * m_Current.Track.Bpm / 60.0;
            }
        }

        void Start()
        {
            if (!Settings)
            {
                var balance = FindAnyObjectByType<PlayerBalance>();
                Settings = balance && balance.Balance ? balance.Balance.Music : null;
            }
            if (!Settings)
                Settings = ScriptableObject.CreateInstance<MusicSettings>();

            m_Biome = CurrentBiome();
            m_Current = Prepare(Settings.TrackFor(m_Biome));
        }

        void OnDestroy()
        {
            Dispose(m_Current);
            Dispose(m_Next);
        }

        static string CurrentBiome() => BiomeZone.Current ? BiomeZone.Current.BiomeName : null;

        // Começa a renderizar as faixas compostas em código fora da thread principal
        TrackSet Prepare(MusicSettings.Track track)
        {
            var set = new TrackSet { Track = track };
            int sampleRate = Settings.SampleRate;
            bool composeBase = !track.Clip;
            set.Rendering = Task.Run(() =>
            {
                var data = new float[k_Layers.Length][];
                for (int i = 0; i < k_Layers.Length; i++)
                {
                    if (i == 0 && !composeBase)
                        continue;
                    data[i] = MusicComposer.Render(k_Layers[i], track.Bpm, track.Bars, sampleRate);
                }

                return data;
            });
            return set;
        }

        // Faixas prontas: cria os clipes e as fontes e agenda todas para começar no mesmo instante
        void Begin(TrackSet set, float fade)
        {
            float[][] data = set.Rendering.Result;
            set.Rendering = null;
            set.Sources = new AudioSource[k_Layers.Length];
            double start = AudioSettings.dspTime + 0.15;
            for (int i = 0; i < k_Layers.Length; i++)
            {
                AudioClip clip = data[i] == null ? set.Track.Clip : CreateClip(k_Layers[i].ToString(), data[i]);
                var source = new GameObject("Musica_" + k_Layers[i]).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 0;
                source.volume = 0f;
                source.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDObjective);
                source.PlayScheduled(start);
                set.Sources[i] = source;
            }

            set.Fade = fade;
        }

        AudioClip CreateClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create("Musica_" + name, samples.Length, 1, Settings.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void Update()
        {
            if (m_Current.Sources == null && m_Current.Rendering.IsCompleted)
                Begin(m_Current, 1f);

            UpdateBiome();

            float multiplier = TimeManager.CurrentMultiplier;
            float k = KairosBeat.Intensity;
            m_Combat = Mathf.MoveTowards(m_Combat, RoomManager.Active ? 1f : Settings.OutOfCombatLayers,
                Time.unscaledDeltaTime * 0.5f);
            float pitch = Mathf.Lerp(1f, Settings.PitchAtMax, k);

            Apply(m_Current, multiplier, k, pitch);
            Apply(m_Next, multiplier, k, pitch);
        }

        // Troca de bioma com outra faixa: prepara a nova e, quando pronta, faz a transição
        void UpdateBiome()
        {
            string biome = CurrentBiome();
            if (biome != m_Biome)
            {
                m_Biome = biome;
                MusicSettings.Track wanted = Settings.TrackFor(biome);
                if (wanted != m_Current.Track && (m_Next == null || m_Next.Track != wanted))
                {
                    Dispose(m_Next);
                    m_Next = Prepare(wanted);
                }
            }

            if (m_Next == null)
                return;

            if (m_Next.Sources == null)
            {
                if (!m_Next.Rendering.IsCompleted)
                    return;
                Begin(m_Next, 0f);
                m_Current.FadeTarget = 0f;
            }

            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, Settings.CrossfadeTime);
            m_Next.Fade = Mathf.MoveTowards(m_Next.Fade, 1f, step);
            m_Current.Fade = Mathf.MoveTowards(m_Current.Fade, 0f, step);
            if (m_Next.Fade >= 1f && m_Current.Fade <= 0f)
            {
                Dispose(m_Current);
                m_Current = m_Next;
                m_Next = null;
            }
        }

        void Apply(TrackSet set, float multiplier, float k, float pitch)
        {
            if (set == null || set.Sources == null)
                return;

            for (int i = 0; i < set.Sources.Length; i++)
            {
                AudioSource source = set.Sources[i];
                if (!source)
                    continue;
                float target = Settings.Volume * GameSettings.MusicVolume * set.Fade *
                               LayerGain(k_Layers[i], multiplier, k);
                source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.6f);
                source.pitch = pitch;
            }
        }

        float LayerGain(MusicComposer.Layer layer, float multiplier, float k) => layer switch
        {
            MusicComposer.Layer.Base => 1f,
            MusicComposer.Layer.Clock => Mathf.Lerp(Settings.ClockAtStart, 1f, k),
            MusicComposer.Layer.Hats => Fade(multiplier, Settings.HatsFade) * m_Combat,
            MusicComposer.Layer.Acid => Fade(multiplier, Settings.AcidFade) * m_Combat,
            MusicComposer.Layer.Breaks => Fade(multiplier, Settings.BreaksFade) * m_Combat,
            _ => Fade(multiplier, Settings.LeadFade) * m_Combat,
        };

        static float Fade(float multiplier, Vector2 range) =>
            Mathf.Clamp01((multiplier - range.x) / Mathf.Max(0.01f, range.y - range.x));

        void Dispose(TrackSet set)
        {
            if (set?.Sources == null)
                return;
            foreach (AudioSource source in set.Sources)
            {
                if (!source)
                    continue;
                if (source.clip && source.clip != set.Track.Clip)
                    Destroy(source.clip);
                Destroy(source.gameObject);
            }

            set.Sources = null;
        }
    }
}
