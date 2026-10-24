using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icaro.EditorTools
{
    // Aplica o "Estilo visual" do CLAUDE.md na cena aberta: céu, névoa, ambiente e luz da paleta do bioma,
    // Volume global, light/reflection probes, esporos no ar e flags Static para o bake.
    public static class SceneAtmosphere
    {
        const float k_ProbeSpacing = 7f;
        static readonly float[] k_ProbeHeights = { 0.8f, 3f };

        // Céu, névoa (ExponentialSquared) e ambiente trilight da paleta
        public static void ApplyEnvironment(BiomePalettes.Palette palette, Material sky, LightingSettings lighting)
        {
            if (sky)
                RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = palette.FogColor;
            RenderSettings.fogDensity = palette.FogDensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = palette.AmbientSky;
            RenderSettings.ambientEquatorColor = palette.AmbientEquator;
            RenderSettings.ambientGroundColor = palette.AmbientGround;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            if (lighting)
                Lightmapping.lightingSettings = lighting;
        }

        // Luz direcional fraca e fria, em modo Mixed (sombra em tempo real, indireta bakeada)
        public static void ConfigureSun(Light sun, BiomePalettes.Palette palette)
        {
            if (!sun)
                return;

            sun.color = palette.SunColor;
            sun.intensity = palette.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.transform.rotation = Quaternion.Euler(palette.SunRotation);
            RenderSettings.sun = sun;
        }

        public static void AddGlobalVolume(Transform parent, VolumeProfile profile)
        {
            var volumeObject = new GameObject("GlobalVolume");
            volumeObject.transform.SetParent(parent, false);
            volumeObject.layer = 0; // a câmera do Player lê volumes da camada Default

            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        // Grade de light probes (2 alturas) e uma reflection probe bakeada do tamanho da área
        public static void AddProbes(Transform parent, string name, Vector3 center, Vector2 size, float height)
        {
            var root = new GameObject("Probes_" + name).transform;
            root.SetParent(parent, false);
            root.position = center;

            var positions = new System.Collections.Generic.List<Vector3>();
            int countX = Mathf.Max(2, Mathf.RoundToInt(size.x / k_ProbeSpacing) + 1);
            int countZ = Mathf.Max(2, Mathf.RoundToInt(size.y / k_ProbeSpacing) + 1);
            for (int ix = 0; ix < countX; ix++)
            for (int iz = 0; iz < countZ; iz++)
            foreach (float y in k_ProbeHeights)
            {
                float x = Mathf.Lerp(-size.x * 0.5f + 1f, size.x * 0.5f - 1f, ix / (float)(countX - 1));
                float z = Mathf.Lerp(-size.y * 0.5f + 1f, size.y * 0.5f - 1f, iz / (float)(countZ - 1));
                positions.Add(new Vector3(x, y, z));
            }

            var lightProbes = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            lightProbes.transform.SetParent(root, false);
            lightProbes.probePositions = positions.ToArray();

            var reflection = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            reflection.transform.SetParent(root, false);
            reflection.mode = ReflectionProbeMode.Baked;
            reflection.size = new Vector3(size.x, height, size.y);
            reflection.center = new Vector3(0f, height * 0.5f, 0f);
            reflection.boxProjection = true;
            reflection.resolution = 128;
            reflection.hdr = true;
        }

        // Esporos: partículas aditivas lentas, ciano e âmbar, que derivam no ar. Leves (até ~150 por área).
        public static void AddSpores(Transform parent, string name, Vector3 center, Vector2 size,
            BiomePalettes.Palette palette)
        {
            var sporeObject = new GameObject("Esporos_" + name);
            sporeObject.transform.SetParent(parent, false);
            sporeObject.transform.position = center + Vector3.up * 2.5f;
            sporeObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // emissão "para cima"

            var particles = sporeObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // a duração só muda parado

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(palette.AccentCyan, palette.AccentAmber);
            main.maxParticles = 150;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 16f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x - 2f, size.y - 2f, 4f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.1f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Surge e some suavemente
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.9f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = fade;

            var renderer = sporeObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sharedMaterial = IcaroMaterials.GetOrCreateAdditiveParticle("Esporo",
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        }

        // Tudo que não se mexe vira Static: entra no bake, no static batching e na oclusão
        public static void MarkStatic(GameObject root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                                            StaticEditorFlags.ReflectionProbeStatic;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }
}
