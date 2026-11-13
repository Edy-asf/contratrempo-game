using System;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Sons dos ataques dos inimigos gerados em código (como o WeaponSounds): sabor de 8 e 16 bits (onda quadrada,
    // bits reduzidos, ruído amostrado) mas com corpo grave, para não soarem finos. Cada clipe é criado na primeira
    // vez que é pedido. Play toca em 3D pelo grupo EnemyAttack do mixer, com fontes reaproveitadas; o tom segue a
    // câmera lenta (WorldSpeed) e varia um pouco a cada tiro.
    public static class EnemySounds
    {
        const int k_MaxSources = 24;

        static readonly Dictionary<string, AudioClip> s_Clips = new Dictionary<string, AudioClip>();
        static readonly List<AudioSource> s_Sources = new List<AudioSource>();
        static UnityEngine.Audio.AudioMixerGroup s_Group;
        static bool s_GroupTried;
        static Transform s_Root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Sources.Clear();
            s_Group = null;
            s_GroupTried = false;
            s_Root = null;
        }

        // ---------- tocar ----------

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
            float pitchVariation = 0.05f, float minDistance = 4f)
        {
            if (!clip)
                return;
            AudioSource source = FreeSource();
            if (!source)
                return;
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            float world = Mathf.Clamp(CombatTiming.WorldSpeed, 0.45f, 1f);
            source.pitch = pitch * world * (1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation));
            source.minDistance = minDistance;
            source.Play();
        }

        static AudioSource FreeSource()
        {
            for (int i = s_Sources.Count - 1; i >= 0; i--)
            {
                if (!s_Sources[i])
                {
                    s_Sources.RemoveAt(i);
                    continue;
                }

                if (!s_Sources[i].isPlaying)
                    return s_Sources[i];
            }

            if (s_Sources.Count >= k_MaxSources)
                return null;
            if (!s_Root)
            {
                s_Root = new GameObject("Sons_Inimigos").transform;
            }

            var go = new GameObject("Som");
            go.transform.SetParent(s_Root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = 90f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = Group();
            s_Sources.Add(source);
            return source;
        }

        static UnityEngine.Audio.AudioMixerGroup Group()
        {
            if (s_Group || s_GroupTried)
                return s_Group;
            s_GroupTried = true;
            try
            {
                if (UnityEngine.Object.FindAnyObjectByType<AudioManager>())
                    s_Group = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.EnemyAttack);
            }
            catch (Exception)
            {
                s_Group = null;
            }

            return s_Group;
        }

        // ---------- clipes (síntese no EnemySoundSynth) ----------

        public static AudioClip PlasmaCharge => Clip("PlasmaCarga", EnemySoundSynth.PlasmaCharge);
        public static AudioClip PlasmaShot => Clip("PlasmaTiro", EnemySoundSynth.PlasmaShot);
        public static AudioClip PlasmaHit => Clip("PlasmaImpacto", EnemySoundSynth.PlasmaHit);
        public static AudioClip MothCharge => Clip("MariposaCarga", EnemySoundSynth.MothCharge);
        public static AudioClip MothShot => Clip("MariposaTiro", EnemySoundSynth.MothShot);
        public static AudioClip MothHit => Clip("MariposaImpacto", EnemySoundSynth.MothHit);
        public static AudioClip MortarLaunch => Clip("MorteiroTiro", EnemySoundSynth.MortarLaunch);
        public static AudioClip MortarWhistle => Clip("MorteiroAssobio", EnemySoundSynth.MortarWhistle);
        public static AudioClip MortarBlast => Clip("MorteiroExplosao", EnemySoundSynth.MortarBlast);
        public static AudioClip MossRoar => Clip("MusgoRugido", EnemySoundSynth.MossRoar);
        public static AudioClip MossGrowl => Clip("MusgoRosnado", EnemySoundSynth.MossGrowl);
        public static AudioClip MossLand => Clip("MusgoPouso", EnemySoundSynth.MossLand);
        public static AudioClip MossSwipe => Clip("MusgoGarra", EnemySoundSynth.MossSwipe);
        public static AudioClip MossDig => Clip("MusgoArranca", EnemySoundSynth.MossDig);
        public static AudioClip ClodHit => Clip("MusgoTorrao", EnemySoundSynth.ClodHit);
        public static AudioClip SporeCloud => Clip("MusgoNuvem", EnemySoundSynth.SporeCloud);
        public static AudioClip MossStun => Clip("MusgoAtordoado", EnemySoundSynth.MossStun);

        static AudioClip Clip(string name, Func<float[]> render)
        {
            if (s_Clips.TryGetValue(name, out AudioClip cached) && cached)
                return cached;
            float[] samples = render();
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, EnemySoundSynth.Rate, false);
            clip.SetData(samples, 0);
            s_Clips[name] = clip;
            return clip;
        }
    }
}
