using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Assets do feedback de combate: projétil do jogador com crítico e assistência de mira (Projectile_Agulha), explosão de morte dos
    // inimigos (VFX_Morte), material dos números de dano (sempre por cima da geometria) e o prefab CombatFeedback
    // (hit marker, números, sons e tremor), que o gerador da Via coloca na cena.
    public static class CombatFeedbackBuilder
    {
        public const string FeedbackPath = IcaroAssetUtility.PrefabsFolder + "/CombatFeedback.prefab";
        public const string DeathBurstPath = IcaroAssetUtility.PrefabsFolder + "/VFX_Morte.prefab";
        const string k_ProjectilePath = IcaroAssetUtility.PrefabsFolder + "/Projectile_Agulha.prefab";
        const string k_ProjectileSource = "Assets/FPS/Prefabs/Projectiles/Projectile_Blaster.prefab";
        const string k_AudioFolder = IcaroAssetUtility.Root + "/Audio/Combate";
        const string k_VfxTextures = "Assets/FPS/Art/Textures/VFX";

        public static GameObject DeathBurst => AssetDatabase.LoadAssetAtPath<GameObject>(DeathBurstPath);

        public static void BuildAll()
        {
            BuildPlayerProjectile();
            BuildDeathBurst();
            BuildFeedbackPrefab();
            AssetDatabase.SaveAssets();
        }

        // Projétil do jogador: o visual do projétil do Blaster com o HomingProjectile (assistência de mira, crítico,
        // dano pelo evento central, PrefabPool) no lugar do ProjectileStandard, com os mesmos números (velocidade,
        // dano, raio, impacto) e um rastro curto. A arma do Ícaro passa a disparar este projétil.
        static void BuildPlayerProjectile()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(k_ProjectileSource);
            if (!source)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Projectile_Agulha";

            var standard = instance.GetComponent<ProjectileStandard>();
            var homing = instance.AddComponent<HomingProjectile>();
            if (standard)
            {
                homing.Speed = standard.Speed;
                homing.Damage = standard.Damage;
                homing.Radius = Mathf.Max(standard.Radius, 0.05f);
                homing.MaxLifeTime = 3f;
                homing.ImpactVfx = standard.ImpactVfx;
                homing.ImpactVfxLifetime = standard.ImpactVfxLifetime;
                homing.ImpactVfxSpawnOffset = standard.ImpactVfxSpawnOffset;
                homing.ImpactSfxClip = standard.ImpactSfxClip;
                homing.HittableLayers = standard.HittableLayers;
            }
            foreach (var old in instance.GetComponents<PlayerProjectile>())
                Object.DestroyImmediate(old);
            if (standard)
                Object.DestroyImmediate(standard);

            // Rastro curto para a curva ficar legível
            var trail = instance.AddComponent<TrailRenderer>();
            trail.sharedMaterial = CombatAssetsBuilder.LineMaterial;
            trail.time = 0.07f;
            trail.minVertexDistance = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.07f, 1f, 0f);
            trail.startColor = new Color(0.55f, 0.95f, 1f, 0.9f);
            trail.endColor = new Color(0.3f, 0.7f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject projectile = IcaroPrefabBuilder.SavePrefab(instance, k_ProjectilePath);

            GameObject contents = PrefabUtility.LoadPrefabContents(IcaroPrefabBuilder.BlasterVariantPath);
            try
            {
                var weapon = contents.GetComponent<WeaponController>();
                if (weapon && projectile)
                    weapon.ProjectilePrefab = projectile.GetComponent<ProjectileBase>();
                PrefabUtility.SaveAsPrefabAsset(contents, IcaroPrefabBuilder.BlasterVariantPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // Explosão de morte: faíscas esticadas, estilhaços e um clarão, em ciano e âmbar bioluminescentes
        static void BuildDeathBurst()
        {
            Material sparks = Additive("VFX_Morte_Faisca", "Dot.png");
            Material flare = Additive("VFX_Morte_Clarao", "Flare.png");
            Material shards = Additive("VFX_Morte_Estilhaco", "ShrapnelTriangle.png");

            var root = new GameObject("VFX_Morte");
            var cyan = new Color(0.4f, 1f, 1f);
            var amber = new Color(1f, 0.65f, 0.2f);

            ParticleSystem main = AddSystem(root, "Faiscas", sparks, 70, new Vector2(0.35f, 0.9f),
                new Vector2(6f, 15f), new Vector2(0.05f, 0.14f), 1.2f, cyan, amber);
            var mainRenderer = main.GetComponent<ParticleSystemRenderer>();
            mainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            mainRenderer.velocityScale = 0.06f;
            mainRenderer.lengthScale = 1.5f;

            ParticleSystem shardSystem = AddSystem(root, "Estilhacos", shards, 18, new Vector2(0.5f, 1.1f),
                new Vector2(3f, 8f), new Vector2(0.12f, 0.3f), 2f, cyan, Color.white);
            var rotation = shardSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            ParticleSystem flash = AddSystem(root, "Clarao", flare, 1, new Vector2(0.12f, 0.16f),
                Vector2.zero, new Vector2(3.2f, 3.6f), 0f, Color.white, cyan);
            var flashShape = flash.shape;
            flashShape.enabled = false;

            IcaroPrefabBuilder.SavePrefab(root, DeathBurstPath);
        }

        static ParticleSystem AddSystem(GameObject root, string name, Material material, int burst, Vector2 lifetime,
            Vector2 speed, Vector2 size, float gravity, Color colorA, Color colorB)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(root.transform, false);
            var system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(burst, 1);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        static Material Additive(string name, string texture)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{k_VfxTextures}/{texture}");
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(name, tex);
            material.SetColor("_BaseColor", new Color(2.5f, 2.5f, 2.5f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // Números de dano: a fonte padrão do TextMeshPro com o shader Overlay (desenha por cima de tudo) e contorno
        static Material NumberMaterial()
        {
            string path = IcaroAssetUtility.MaterialsFolder + "/NumeroDano_Overlay.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (!font || !overlay)
                return null;

            if (!material)
            {
                material = new Material(font.material) { name = "NumeroDano_Overlay" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(font.material);
            material.shader = overlay;
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.22f);
            material.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat("_FaceDilate", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildFeedbackPrefab()
        {
            var root = new GameObject("CombatFeedback");
            root.AddComponent<CombatFeedback>();

            root.GetComponent<DamageNumbers>().FontMaterial = NumberMaterial();

            var audio = root.GetComponent<CombatAudio>();
            audio.HitClips = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(k_AudioFolder + "/Acerto_A.ogg"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(k_AudioFolder + "/Acerto_B.ogg"),
            };
            audio.CriticalClip = AssetDatabase.LoadAssetAtPath<AudioClip>(k_AudioFolder + "/Critico.ogg");
            audio.KillClip = AssetDatabase.LoadAssetAtPath<AudioClip>(k_AudioFolder + "/Abate.ogg");

            IcaroPrefabBuilder.SavePrefab(root, FeedbackPath);
        }
    }
}
