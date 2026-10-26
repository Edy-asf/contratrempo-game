using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Gatilho de entrada de bioma: ao passar por ele, névoa, ambiente e sol transitam suavemente para a
    // atmosfera do bioma. Só uma transição fica ativa por vez. A claridade do mapa do bioma (SkySettings, menu "Ícaro >
    // Ajustes do céu") multiplica a névoa, o ambiente e o luar.
    [RequireComponent(typeof(Collider))]
    public class BiomeZone : MonoBehaviour
    {
        public string BiomeName = "Bosque";

        [Header("Névoa")]
        public Color FogColor = new Color(0.16f, 0.27f, 0.29f);
        public float FogDensity = 0.028f;

        [Header("Ambiente (trilight)")]
        public Color AmbientSky = new Color(0.38f, 0.52f, 0.58f);
        public Color AmbientEquator = new Color(0.2f, 0.28f, 0.3f);
        public Color AmbientGround = new Color(0.08f, 0.1f, 0.1f);

        [Header("Sol")]
        public Color SunColor = new Color(0.62f, 0.74f, 0.92f);
        public float SunIntensity = 0.9f;

        [Tooltip("Duração da transição, em segundos")]
        public float BlendDuration = 3f;

        [Tooltip("Mostra o nome do bioma no HUD ao entrar")]
        public bool AnnounceOnEnter = true;

        static BiomeZone s_Current;

        // Bioma em que o jogador está (último BiomeZone atravessado); null antes do primeiro
        public static BiomeZone Current => s_Current;
        static Coroutine s_Blend;
        static MonoBehaviour s_BlendOwner;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (s_Current == this || !other.GetComponentInParent<PlayerCharacterController>())
                return;

            Enter(BlendDuration);

            if (AnnounceOnEnter)
            {
                DisplayMessageEvent evt = Events.DisplayMessageEvent;
                evt.Message = BiomeName;
                evt.DelayBeforeDisplay = 0f;
                EventManager.Broadcast(evt);
            }
        }

        // Aplica a atmosfera na hora (usado pelo teleporte de depuração)
        public void ApplyInstant() => Enter(0f);

        // Reaplica a atmosfera do bioma na hora, sem trocar o bioma atual nem anunciar (ajustes do céu mudaram)
        public void ReapplyLighting()
        {
            if (s_Blend != null)
                return;   // no meio de uma transição: ela já termina com os valores novos
            Light sun = RenderSettings.sun;
            Apply(1f, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.ambientSkyColor,
                RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor, sun,
                sun ? sun.color : SunColor, sun ? sun.intensity : SunIntensity);
        }

        void Enter(float duration)
        {
            if (s_Blend != null && s_BlendOwner)
                s_BlendOwner.StopCoroutine(s_Blend);

            s_Current = this;
            s_BlendOwner = this;
            s_Blend = StartCoroutine(Blend(duration));
        }

        IEnumerator Blend(float duration)
        {
            Color fogColor = RenderSettings.fogColor;
            float fogDensity = RenderSettings.fogDensity;
            Color sky = RenderSettings.ambientSkyColor;
            Color equator = RenderSettings.ambientEquatorColor;
            Color ground = RenderSettings.ambientGroundColor;
            Light sun = RenderSettings.sun;
            Color sunColor = sun ? sun.color : SunColor;
            float sunIntensity = sun ? sun.intensity : SunIntensity;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Apply(Mathf.SmoothStep(0f, 1f, t / duration), fogColor, fogDensity, sky, equator, ground, sun,
                    sunColor, sunIntensity);
                yield return null;
            }

            Apply(1f, fogColor, fogDensity, sky, equator, ground, sun, sunColor, sunIntensity);
            s_Blend = null;
        }

        void Apply(float k, Color fogColor, float fogDensity, Color sky, Color equator, Color ground, Light sun,
            Color sunColor, float sunIntensity)
        {
            BiomeSkyTuning tuning = SkySettings.Current.TuningFor(BiomeName);
            float ambient = tuning != null ? tuning.AmbientBrightness : 1f;
            float fog = tuning != null ? tuning.FogBrightness : 1f;
            float density = tuning != null ? tuning.FogDensity : 1f;
            float moon = tuning != null ? tuning.MoonBrightness : 1f;
            RenderSettings.fogColor = Color.Lerp(fogColor, FogColor * fog, k);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, FogDensity * density, k);
            RenderSettings.ambientSkyColor = Color.Lerp(sky, AmbientSky * ambient, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(equator, AmbientEquator * ambient, k);
            RenderSettings.ambientGroundColor = Color.Lerp(ground, AmbientGround * ambient, k);

            // O ambiente trilight só chega aos objetos dinâmicos depois de recalcular a probe ambiente
            RefreshAmbient(k >= 1f);

            if (sun)
            {
                sun.color = Color.Lerp(sunColor, SunColor, k);
                sun.intensity = Mathf.Lerp(sunIntensity, SunIntensity * moon, k);
            }
        }

        static float s_NextAmbient;

        // Recalcula a luz ambiente no máximo 5 vezes por segundo (a mudança é lenta; 'force' = a última, no fim)
        public static void RefreshAmbient(bool force)
        {
            if (!force && Time.unscaledTime < s_NextAmbient)
                return;
            s_NextAmbient = Time.unscaledTime + 0.2f;
            DynamicGI.UpdateEnvironment();
        }

        void OnDestroy()
        {
            if (s_Current == this)
                s_Current = null;
        }
    }
}
