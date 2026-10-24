using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Icaro
{
    // Leitura do multiplicador de tempo no mundo, além do HUD. Avança o pulso de Kairós (KairosBeat) e, conforme o
    // multiplicador sobe:
    // - a névoa "respira" no ritmo do pulso e esquenta de cor (o BiomeZone continua dono da névoa base: este
    //   componente só soma um desvio por cima e percebe quando a base muda);
    // - os esporos no ar (partículas "Esporos_") se movem mais rápido e piscam no pulso;
    // - um Volume global próprio soma vinheta avermelhada e aberração cromática, com peso 0 a 1,0x (sem mudar o
    //   visual base) até 1 no teto, pulsando com o batimento.
    // O TimeManager adiciona este componente. O jogador nunca é afetado pelo multiplicador: tudo aqui é só visual.
    [DefaultExecutionOrder(200)]
    public class TimeAtmosphere : MonoBehaviour
    {
        [Header("Névoa")]
        [Tooltip("Quanto a densidade da névoa varia no pulso, no teto do multiplicador (0,15 = ±15%)")]
        [Range(0f, 0.5f)] public float FogPulseAmount = 0.15f;

        [Tooltip("Cor que a névoa puxa conforme o multiplicador sobe")]
        public Color FogTint = new Color(0.45f, 0.16f, 0.12f);

        [Tooltip("Quanto a cor da névoa puxa para FogTint no teto")]
        [Range(0f, 1f)] public float FogTintAmount = 0.3f;

        [Header("Esporos")]
        [Tooltip("Velocidade dos esporos no teto do multiplicador (1 = normal)")]
        public float SporeSpeedAtMax = 3.5f;

        [Header("Pós-processamento (no teto)")]
        [Range(0f, 1f)] public float VignetteIntensity = 0.42f;
        public Color VignetteColor = new Color(0.35f, 0.04f, 0.04f);
        [Range(0f, 1f)] public float ChromaticAberration = 0.5f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        TimeMusic m_Music;
        readonly List<ParticleSystem> m_Spores = new List<ParticleSystem>();
        readonly List<ParticleSystemRenderer> m_SporeRenderers = new List<ParticleSystemRenderer>();
        MaterialPropertyBlock m_Block;
        float m_AppliedSporeSpeed = -1f;

        float m_LastFogDensity = -1f;
        Color m_LastFogColor;
        float m_BaseFogDensity;
        Color m_BaseFogColor;

        Volume m_Volume;
        Vignette m_Vignette;
        ChromaticAberration m_Chromatic;

        void Start()
        {
            m_Block = new MaterialPropertyBlock();
            m_Music = GetComponent<TimeMusic>();
            foreach (ParticleSystem system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                if (!system.name.StartsWith("Esporos_"))
                    continue;
                m_Spores.Add(system);
                m_SporeRenderers.Add(system.GetComponent<ParticleSystemRenderer>());
            }

            CreateVolume();
        }

        void OnDestroy()
        {
            if (m_Volume && m_Volume.profile)
                Destroy(m_Volume.profile);
        }

        void CreateVolume()
        {
            var go = new GameObject("Volume_Kairos");
            go.transform.SetParent(transform, false);
            m_Volume = go.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 50f;
            m_Volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Vignette = profile.Add<Vignette>(true);
            m_Vignette.color.overrideState = true;
            m_Vignette.color.value = VignetteColor;
            m_Vignette.smoothness.overrideState = true;
            m_Vignette.smoothness.value = 0.45f;
            m_Chromatic = profile.Add<ChromaticAberration>(true);
            m_Volume.profile = profile;
        }

        void Update()
        {
            // Com música, o pulso segue o relógio dela (bate junto com o tique); sem, avança sozinho
            if (m_Music && m_Music.isActiveAndEnabled && m_Music.HasBeat)
                KairosBeat.Phase = m_Music.BeatPosition;
            else
                KairosBeat.Phase += KairosBeat.BpmFor(TimeManager.CurrentMultiplier) / 60f * Time.deltaTime;
        }

        void LateUpdate()
        {
            float k = KairosBeat.Intensity;
            float pulse = KairosBeat.Pulse;

            UpdateFog(k, pulse);
            UpdateSpores(k, pulse);
            UpdateVolume(k, pulse);
        }

        // Desvio por cima da névoa do bioma; se outro script (BiomeZone) escreveu, aquela vira a nova base
        void UpdateFog(float k, float pulse)
        {
            if (!RenderSettings.fog)
                return;

            if (!Mathf.Approximately(RenderSettings.fogDensity, m_LastFogDensity) ||
                RenderSettings.fogColor != m_LastFogColor)
            {
                m_BaseFogDensity = RenderSettings.fogDensity;
                m_BaseFogColor = RenderSettings.fogColor;
            }

            float density = m_BaseFogDensity * (1f + FogPulseAmount * k * (pulse - 0.3f));
            Color color = Color.Lerp(m_BaseFogColor, FogTint, FogTintAmount * k);
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor = color;
            m_LastFogDensity = density;
            m_LastFogColor = color;
        }

        void UpdateSpores(float k, float pulse)
        {
            float speed = Mathf.Lerp(1f, SporeSpeedAtMax, k);
            bool speedChanged = Mathf.Abs(speed - m_AppliedSporeSpeed) > 0.01f;
            if (speedChanged)
                m_AppliedSporeSpeed = speed;

            float glow = 1f + 0.8f * k * pulse;
            for (int i = 0; i < m_Spores.Count; i++)
            {
                if (!m_Spores[i])
                    continue;
                if (speedChanged)
                {
                    ParticleSystem.MainModule main = m_Spores[i].main;
                    main.simulationSpeed = speed;
                }

                ParticleSystemRenderer renderer = m_SporeRenderers[i];
                if (!renderer || !renderer.sharedMaterial)
                    continue;
                Color baseColor = renderer.sharedMaterial.HasProperty(k_BaseColor)
                    ? renderer.sharedMaterial.GetColor(k_BaseColor)
                    : Color.white;
                renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, baseColor * glow);
                renderer.SetPropertyBlock(m_Block);
            }
        }

        void UpdateVolume(float k, float pulse)
        {
            if (!m_Volume)
                return;

            m_Volume.weight = k * GameSettings.ScreenEffects;
            m_Vignette.intensity.Override(VignetteIntensity * (0.85f + 0.15f * pulse));
            m_Vignette.color.Override(VignetteColor);
            m_Chromatic.intensity.Override(ChromaticAberration * (0.8f + 0.2f * pulse));
        }
    }
}
