using System;
using UnityEngine;

namespace Icaro
{
    // Música do jogo (TimeMusic): a faixa de cada bioma, o volume e quando cada camada dinâmica entra conforme o
    // multiplicador de tempo. Sem faixa própria (Clip vazio), a base é o tema composto em código (MusicComposer).
    // As camadas dinâmicas são sempre compostas em código, no andamento e na duração da faixa.
    [CreateAssetMenu(menuName = "Ícaro/Música", fileName = "MusicSettings")]
    public class MusicSettings : ScriptableObject
    {
        [Serializable]
        public class Track
        {
            [Tooltip("Bioma desta faixa (nome do BiomeZone: Bosque, Charco, Ruínas, Cratera). Vazio = todos os outros")]
            public string Biome;

            [Tooltip("Música base (opcional). Vazio = tema composto em código. Com uma faixa própria, Bpm e Bars " +
                     "precisam ser os dela, e as camadas seguem a progressão do tema padrão (lá menor)")]
            public AudioClip Clip;

            [Min(30f)] public float Bpm = 128f;

            [Tooltip("Compassos (4/4) da faixa em laço")]
            [Min(1)] public int Bars = 16;
        }

        public Track[] Tracks = { new Track() };

        [Range(0f, 1f)] public float Volume = 0.45f;

        [Tooltip("Aceleração da música (pitch de todas as faixas juntas) no teto do multiplicador; 1 = não acelera")]
        [Range(1f, 2f)] public float PitchAtMax = 1.35f;

        [Header("Camadas (multiplicador em que começa e em que fica cheia)")]
        [Tooltip("Volume do relógio (engrenagens e tique) a 1,0x; cresce até 1 no teto")]
        [Range(0f, 1f)] public float ClockAtStart = 0.35f;

        [Tooltip("Chimbais e palmas")]
        public Vector2 HatsFade = new Vector2(1.2f, 1.8f);

        [Tooltip("Linha acid com filtro ressonante")]
        public Vector2 AcidFade = new Vector2(1.7f, 2.6f);

        [Tooltip("Quebrada de caixas (sensação de drum'n'bass)")]
        public Vector2 BreaksFade = new Vector2(2.5f, 3.4f);

        [Tooltip("Lead agudo, sirene, rufadas e subidas")]
        public Vector2 LeadFade = new Vector2(3.4f, 4.4f);

        [Tooltip("Volume das camadas rítmicas fora de combate (sala ativa = 1)")]
        [Range(0f, 1f)] public float OutOfCombatLayers = 0.5f;

        [Tooltip("Segundos de transição ao trocar de faixa (bioma)")]
        [Min(0f)] public float CrossfadeTime = 3f;

        [Tooltip("Taxa de amostragem das faixas compostas em código")]
        public int SampleRate = 44100;

        // Faixa do bioma (pelo nome); sem uma específica, a de bioma vazio; sem essa, a primeira
        public Track TrackFor(string biome)
        {
            if (Tracks == null || Tracks.Length == 0)
                return new Track();

            Track fallback = null;
            foreach (Track track in Tracks)
            {
                if (track == null)
                    continue;
                if (!string.IsNullOrEmpty(biome) && track.Biome == biome)
                    return track;
                if (fallback == null && string.IsNullOrEmpty(track.Biome))
                    fallback = track;
            }

            return fallback ?? Tracks[0];
        }
    }
}
