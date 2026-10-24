using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Sons de feedback dos acertos do jogador (2D): acerto curto com leve variação de tom e limite de repetições
    // por segundo, um som próprio para crítico e um de abate bem marcante, que nunca é cortado pelo limite.
    public class CombatAudio : MonoBehaviour
    {
        public AudioClip[] HitClips;
        public AudioClip CriticalClip;
        public AudioClip KillClip;

        [Range(0f, 1f)] public float HitVolume = 0.55f;
        [Range(0f, 1f)] public float CriticalVolume = 0.7f;
        [Range(0f, 1f)] public float KillVolume = 0.9f;

        [Tooltip("Variação de tom do acerto (0,08 = entre 92% e 108%)")]
        public float HitPitchVariation = 0.08f;

        [Tooltip("Máximo de sons de acerto (e de crítico) por segundo")]
        public float MaxHitsPerSecond = 14f;

        const int k_Voices = 6;

        AudioSource[] m_Voices;
        int m_NextVoice;
        float m_NextHitTime;
        float m_NextCriticalTime;

        public void PlayHit(bool critical, bool killed)
        {
            if (killed)
            {
                Play(KillClip, KillVolume, Random.Range(0.96f, 1.04f));
                return;
            }

            float now = Time.unscaledTime;
            if (critical)
            {
                if (now < m_NextCriticalTime)
                    return;
                m_NextCriticalTime = now + 1f / MaxHitsPerSecond;
                Play(CriticalClip, CriticalVolume, Random.Range(0.97f, 1.03f));
                return;
            }

            if (now < m_NextHitTime || HitClips == null || HitClips.Length == 0)
                return;
            m_NextHitTime = now + 1f / MaxHitsPerSecond;
            Play(HitClips[Random.Range(0, HitClips.Length)], HitVolume,
                1f + Random.Range(-HitPitchVariation, HitPitchVariation));
        }

        void Awake()
        {
            m_Voices = new AudioSource[k_Voices];
            for (int i = 0; i < k_Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 64;
                m_Voices[i] = source;
            }
        }

        void Start()
        {
            // Mixer do Microgame (só existe com o AudioManager na cena)
            if (!FindAnyObjectByType<AudioManager>())
                return;
            var group = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.Impact);
            foreach (AudioSource source in m_Voices)
                source.outputAudioMixerGroup = group;
        }

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (!clip)
                return;

            AudioSource source = m_Voices[m_NextVoice];
            m_NextVoice = (m_NextVoice + 1) % m_Voices.Length;
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }
    }
}
