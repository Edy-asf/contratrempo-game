using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Transição de atmosfera ao longo de um trecho (o desfiladeiro entre dois biomas): conforme o jogador avança de
    // From para To, a névoa, o ambiente, o luar e o céu passam do bioma A para o B. No meio a névoa fica bem mais
    // densa (MidFogDensity) e o céu se apaga (estrelas, aurora e planeta somem), para a troca não aparecer. Fora do
    // trecho não mexe em nada (os BiomeZone de cada lado cuidam da atmosfera). O TimeAtmosphere adota o que este
    // componente escreve como a névoa base.
    public class AtmosphereCorridor : MonoBehaviour
    {
        [System.Serializable]
        public class Atmosphere
        {
            public Color FogColor;
            public float FogDensity;
            public Color AmbientSky, AmbientEquator, AmbientGround;
            public Color MoonColor;
            public float MoonIntensity;
            public Material Sky;
        }

        public Vector3 From;
        public Vector3 To;
        public Atmosphere A = new Atmosphere();
        public Atmosphere B = new Atmosphere();

        [Tooltip("Densidade da névoa no meio do trecho")]
        public float MidFogDensity = 0.075f;

        [Tooltip("Quanto a cor da névoa escurece no meio (0 = nada)")]
        [Range(0f, 1f)] public float MidDarken = 0.35f;

        static readonly int k_Stars = Shader.PropertyToID("_StarDensity");
        static readonly int k_Aurora = Shader.PropertyToID("_AuroraStrength");
        static readonly int k_Planet = Shader.PropertyToID("_PlanetSize");
        static readonly int k_Horizon = Shader.PropertyToID("_HorizonTint");
        static readonly int k_Mid = Shader.PropertyToID("_MidColor");

        PlayerCharacterController m_Player;
        Material m_Sky;
        Material m_Hidden;
        float m_Last = -1f;

        void Update()
        {
            if (!m_Player)
            {
                m_Player = FindAnyObjectByType<PlayerCharacterController>();
                if (!m_Player)
                    return;
            }

            Vector3 path = To - From;
            float t = Mathf.Clamp01(Vector3.Dot(m_Player.transform.position - From, path) / path.sqrMagnitude);
            bool outside = t <= 0f || t >= 1f;
            // Fora do trecho: escreve uma última vez ao sair e depois deixa os BiomeZone cuidarem
            if (outside && (Mathf.Approximately(m_Last, t) || m_Last < 0f))
            {
                m_Last = t;
                return;
            }

            if (Mathf.Abs(t - m_Last) < 0.002f && !outside)
                return;
            m_Last = t;
            Apply(t);
        }

        void Apply(float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            // Max: em t = 1 o seno sai um negativo minúsculo, e a potência dele vira NaN (a névoa NaN pinta a tela de
            // preto)
            float middle = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 1.5f);

            Color fog = Color.Lerp(A.FogColor, B.FogColor, k);
            RenderSettings.fogColor = Color.Lerp(fog, fog * (1f - MidDarken), middle);
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(A.FogDensity, B.FogDensity, k), MidFogDensity, middle);
            RenderSettings.ambientSkyColor = Color.Lerp(A.AmbientSky, B.AmbientSky, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(A.AmbientEquator, B.AmbientEquator, k);
            RenderSettings.ambientGroundColor = Color.Lerp(A.AmbientGround, B.AmbientGround, k);
            BiomeZone.RefreshAmbient(t <= 0f || t >= 1f);

            Light moon = RenderSettings.sun;
            if (moon)
            {
                moon.color = Color.Lerp(A.MoonColor, B.MoonColor, k);
                moon.intensity = Mathf.Lerp(A.MoonIntensity, B.MoonIntensity, k);
            }

            UpdateSky(t, fog);
        }

        // Céu: de A para um céu apagado no meio (na cor da névoa, sem estrelas, aurora nem planeta) e dele para B
        void UpdateSky(float t, Color fog)
        {
            if (!A.Sky || !B.Sky || A.Sky.shader != B.Sky.shader)
                return;
            if (t <= 0f || t >= 1f)
            {
                RenderSettings.skybox = t <= 0f ? A.Sky : B.Sky;
                return;
            }

            if (!m_Sky)
            {
                m_Sky = new Material(A.Sky) { name = "Ceu_Transicao" };
                m_Hidden = new Material(B.Sky) { name = "Ceu_Nevoa" };
            }

            m_Hidden.SetFloat(k_Stars, 0f);
            m_Hidden.SetFloat(k_Aurora, 0f);
            m_Hidden.SetFloat(k_Planet, 0f);
            m_Hidden.SetColor(k_Horizon, fog);
            m_Hidden.SetColor(k_Mid, fog * 0.8f);
            if (t < 0.5f)
                m_Sky.Lerp(A.Sky, m_Hidden, Mathf.SmoothStep(0f, 1f, t * 2f));
            else
                m_Sky.Lerp(m_Hidden, B.Sky, Mathf.SmoothStep(0f, 1f, (t - 0.5f) * 2f));
            RenderSettings.skybox = m_Sky;
        }
    }
}
