using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Detalhe vivo das arenas de Archen Kaedra (pedido do usuário em 2026-11-11, a começar pelo Bairro dos Relojoeiros):
    // fumaça subindo dos telhados atrás das fileiras e brasas no ar, luzes lilás das lanternas que ainda funcionam e as
    // linhas de bandeirolas da festa da guilda, que o PlaceFlags (PicosSprites) estica no fim da geração. Sem sorteio do
    // mapa: os pontos vêm do gerador da cidade.
    public static partial class PicosBuilder
    {
        // Bandeirolas da cidade: de 'A' a 'B', cedendo 'Sag' no meio (consumidas no fim do PlaceFlags)
        static readonly List<(Vector3 A, Vector3 B, float Sag)> s_CityFlagLines = new List<(Vector3, Vector3, float)>();

        static void CityFlagLines(MeshData ropes, MeshData flags)
        {
            foreach ((Vector3 a, Vector3 b, float sag) in s_CityFlagLines)
                FlagLine(ropes, flags, a, b, sag, 0.85f);
            s_CityFlagLines.Clear();
        }

        // Luz de lanterna (lilás), tremendo e falhando de vez em quando quando 'failing'
        static void CityLamp(Transform root, Vector3 position, float intensity, float range, bool failing)
        {
            var light = new GameObject("Luz_Lanterna").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = new Color(0.8f, 0.62f, 1f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (failing)
                light.gameObject.AddComponent<LightFlicker>();
        }

        // Coluna de fumaça (rolos cinza-escuros em pixel art, subindo e abrindo com o vento) com brasas laranja; 'scale'
        // 1 = incêndio de telhado, menor = rescaldo no chão
        static void CitySmoke(Transform root, Vector3 position, float scale, bool embers)
        {
            var go = new GameObject("Fumaca");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f * scale, 14f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * scale, 2.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f * scale, 4.5f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.17f, 0.85f), new Color(0.3f, 0.28f, 0.31f, 0.7f));
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 1.6f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            // Vento leve, sempre para o mesmo lado, e a fumaça abrindo e sumindo ao subir
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.7f * scale, 1.3f * scale);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.2f * scale, 0.5f * scale);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.85f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.5f * scale;
            noise.frequency = 0.12f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (!embers)
                return;
            var sparks = new GameObject("Brasas");
            sparks.transform.SetParent(go.transform, false);
            var emberSystem = sparks.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule em = emberSystem.main;
            em.loop = true;
            em.prewarm = true;
            em.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.12f), new Color(1f, 0.75f, 0.3f));
            em.maxParticles = 120;
            em.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule ee = emberSystem.emission;
            ee.rateOverTime = 14f * scale;
            ParticleSystem.ShapeModule es = emberSystem.shape;
            es.shapeType = ParticleSystemShapeType.Cone;
            es.angle = 25f;
            es.radius = 1.4f * scale;
            es.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.NoiseModule en = emberSystem.noise;
            en.enabled = true;
            en.strength = 1.2f;
            en.frequency = 0.6f;
            ParticleSystem.ColorOverLifetimeModule ec = emberSystem.colorOverLifetime;
            ec.enabled = true;
            var glow = new Gradient();
            glow.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ec.color = glow;
            var er = sparks.GetComponent<ParticleSystemRenderer>();
            er.sharedMaterial = GridTextureKit.SporeMaterial();
            er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Rolos de fumaça em pixel art (32 x 32: círculos sobrepostos em três tons, beira em degraus), no material de
        // partícula com transparência (não aditivo)
        static Material s_SmokeMaterial;

        static Material SmokeMaterial()
        {
            if (s_SmokeMaterial)
                return s_SmokeMaterial;
            const int N = 32;
            var pixels = new Color[N * N];
            var rnd = new System.Random(4421);
            var blobs = new List<(float X, float Y, float R)>();
            for (int k = 0; k < 7; k++)
                blobs.Add((10f + (float)rnd.NextDouble() * 12f, 9f + (float)rnd.NextDouble() * 12f, 5f + (float)rnd.NextDouble() * 4f));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float best = 0f, light = 0f;
                    foreach ((float bx, float by, float r) in blobs)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - bx) * (x + 0.5f - bx) + (y + 0.5f - by) * (y + 0.5f - by)) / r;
                        if (1f - d > best)
                        {
                            best = 1f - d;
                            light = Mathf.Clamp01((y - by) / r * 0.5f + 0.5f + (bx - x) / r * 0.2f);
                        }
                    }

                    if (best <= 0f)
                    {
                        pixels[y * N + x] = new Color(1f, 1f, 1f, 0f);
                        continue;
                    }

                    // três tons por pixel (claro em cima, escuro embaixo) e a beira mais transparente
                    float tone = light > 0.66f ? 1f : light > 0.33f ? 0.82f : 0.66f;
                    float alpha = best > 0.25f ? 1f : best > 0.1f ? 0.7f : 0.4f;
                    pixels[y * N + x] = new Color(tone, tone, tone * 1.03f, alpha);
                }

            Texture2D texture = GridTextureKit.Save("Picos_Fumaca", N, pixels);
            string path = $"{GridTextureKit.MaterialsFolder}/Picos_Fumaca.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Picos_Fumaca" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            s_SmokeMaterial = material;
            return material;
        }
    }
}
