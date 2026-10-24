using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro.EditorTools
{
    // Chefes e o kit dos ataques deles. Kit (Balance/BossKit + Prefabs/Chefes/Ataque_*): onda de choque, projétil,
    // marca no chão, poça e feixe, com materiais aditivos tingidos em jogo. Chefes (Prefabs/Chefes/Chefe_*): o modelo
    // cartoon de Models/Chefes (Quaternius, ver CREDITOS.md) com materiais URP foscos nas cores do modelo, Health,
    // Damageable, Actor, NavMeshAgent, EnemyStats, HitReaction e a IA de cada um. Sem o FBX, um placeholder de
    // primitivas. Chamado pelo menu "Ícaro > Gerar prefabs" (CombatAssetsBuilder.EnsureAll).
    public static class BossPrefabBuilder
    {
        public const string Folder = IcaroAssetUtility.PrefabsFolder + "/Chefes";
        public const string KitPath = CombatAssetsBuilder.Folder + "/BossKit.asset";
        const string k_Group = "Chefes";

        // Afiliação dos inimigos no Microgame (o jogador é 1)
        const int k_EnemyAffiliation = 0;

        public static BossKit Kit => AssetDatabase.LoadAssetAtPath<BossKit>(KitPath);

        // ---------- kit dos ataques ----------

        public static BossKit BuildKit()
        {
            IcaroAssetUtility.EnsureFolder(Folder);
            BossKit kit = Kit;
            if (!kit)
            {
                kit = ScriptableObject.CreateInstance<BossKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            kit.ShockwavePrefab = BuildShockwave();
            kit.ProjectilePrefab = BuildProjectile();
            kit.TelegraphPrefab = BuildTelegraph();
            kit.PuddlePrefab = BuildPuddle();
            kit.BeamPrefab = BuildBeam();
            EditorUtility.SetDirty(kit);
            return kit;
        }

        // Material aditivo branco (a cor vem por objeto, em jogo)
        public static Material Additive(string name)
        {
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(name, null);
            material.SetFloat("_Cull", 0f);
            material.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shockwave BuildShockwave()
        {
            var root = new GameObject("Ataque_OndaDeChoque", typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshRenderer>().sharedMaterial = Additive("Chefe_Onda");
            root.AddComponent<Shockwave>();
            return Save(root, "Ataque_OndaDeChoque").GetComponent<Shockwave>();
        }

        static BossProjectile BuildProjectile()
        {
            var root = new GameObject("Ataque_Projetil");
            GameObject ball = Part(PrimitiveType.Sphere, "Esfera", root.transform, Vector3.zero, Vector3.one,
                Additive("Chefe_Projetil"));
            ball.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Additive("Chefe_Rastro");
            trail.time = 0.12f;
            trail.minVertexDistance = 0.2f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<BossProjectile>();
            return Save(root, "Ataque_Projetil").GetComponent<BossProjectile>();
        }

        static GroundTelegraph BuildTelegraph()
        {
            var root = new GameObject("Ataque_Marca");
            Material material = Additive("Chefe_Marca");
            GameObject ring = Part(PrimitiveType.Cylinder, "Borda", root.transform, Vector3.zero, Vector3.one, material);
            GameObject fill = Part(PrimitiveType.Cylinder, "Enchimento", root.transform, Vector3.up * 0.01f,
                Vector3.one, material);
            var telegraph = root.AddComponent<GroundTelegraph>();
            telegraph.Ring = ring.transform;
            telegraph.Fill = fill.transform;
            return Save(root, "Ataque_Marca").GetComponent<GroundTelegraph>();
        }

        static AcidPuddle BuildPuddle()
        {
            var root = new GameObject("Ataque_Poca");
            Part(PrimitiveType.Cylinder, "Disco", root.transform, Vector3.zero, Vector3.one, Additive("Chefe_Poca"));
            root.AddComponent<AcidPuddle>();
            return Save(root, "Ataque_Poca").GetComponent<AcidPuddle>();
        }

        static SweepBeam BuildBeam()
        {
            var root = new GameObject("Ataque_Feixe");
            GameObject bar = Part(PrimitiveType.Cube, "Barra", root.transform, Vector3.zero, Vector3.one,
                Additive("Chefe_Feixe"));
            var beam = root.AddComponent<SweepBeam>();
            beam.Bar = bar.transform;
            return Save(root, "Ataque_Feixe").GetComponent<SweepBeam>();
        }

        // ---------- chefes ----------

        // model: nome do FBX em Models/Chefes; muzzleHeight: altura da boca em fração da altura do modelo
        public static void BuildBoss<T>(BossDefinition definition, string model, float muzzleHeight,
            Color placeholderColor) where T : BossAI
        {
            if (!definition)
                return;

            string name = "Chefe_" + model.Replace("Chefe_", "");
            var root = new GameObject(name);
            var health = root.AddComponent<Health>();
            health.MaxHealth = definition.MaxHealth;

            // Modelo (ou placeholder) no filho "Visual"
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            string fbxPath = $"{ModelLibrary.ModelsFolder}/{k_Group}/{model}.fbx";
            // FBX importado antes de o grupo Chefes existir: reimporta para valerem os ajustes (sem animação)
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter importer &&
                importer.animationType != ModelImporterAnimationType.None)
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, visual);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Modelo";
                ConvertMaterials(body);
            }
            else
            {
                Material material = IcaroMaterials.GetOrCreate("Chefe_Placeholder_" + model, placeholderColor);
                Part(PrimitiveType.Capsule, "Corpo", visual, new Vector3(0f, 2f, 0f), new Vector3(2.5f, 2f, 2.5f),
                    material);
            }

            Bounds bounds = ModelBounds(visual);
            float height = Mathf.Max(1f, bounds.size.y);
            float radius = Mathf.Max(0.8f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f);

            var aimPoint = new GameObject("AimPoint").transform;
            aimPoint.SetParent(root.transform, false);
            aimPoint.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var actor = root.AddComponent<Actor>();
            actor.Affiliation = k_EnemyAffiliation;
            actor.AimPoint = aimPoint;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            root.AddComponent<Damageable>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Min(radius, 1.5f);
            agent.height = height;
            agent.speed = definition.MoveSpeed;
            agent.acceleration = 12f;
            agent.angularSpeed = 0f; // o BossAI gira o chefe para o jogador
            agent.stoppingDistance = 1f;
            agent.autoBraking = false;

            var muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(0f, height * muzzleHeight, bounds.max.z * 0.85f);

            var ai = root.AddComponent<T>();
            ai.Visual = visual;
            ai.Muzzle = muzzle;
            ai.BodyRadius = radius;
            ai.FlashRenderers = visual.GetComponentsInChildren<Renderer>();

            root.AddComponent<EnemyStats>().Definition = definition;
            var reaction = root.AddComponent<HitReaction>();
            reaction.RecoilDistance = 0f;
            reaction.CenterHeight = height * 0.5f;
            reaction.DeathBurst = CombatFeedbackBuilder.DeathBurst;
            reaction.DeathBurstScale = Mathf.Clamp(height * 0.7f, 2f, 5f);

            GameObject prefab = Save(root, name);
            definition.Prefab = prefab;
            EditorUtility.SetDirty(definition);
        }

        // Os materiais do FBX (cores sólidas do modelo) viram URP/Lit foscos, um por cor, em Materials/Chefes
        static void ConvertMaterials(GameObject body)
        {
            var created = new Dictionary<string, Material>();
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (!source)
                        continue;
                    string key = "Chefe_" + source.name.Replace(".", "_");
                    if (!created.TryGetValue(key, out Material converted))
                    {
                        Color color = source.HasProperty("_Color") ? source.color : Color.white;
                        bool eye = source.name.StartsWith("Eye_");
                        converted = IcaroMaterials.GetOrCreate(key, color, default, eye ? 0.6f : 0.12f);
                        created[key] = converted;
                    }
                    materials[i] = converted;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        static Bounds ModelBounds(Transform visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.up, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            // Em coordenadas locais da raiz (que está na origem)
            return bounds;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material) =>
            IcaroPrefabBuilder.CreatePart(type, name, parent, position, scale, material, keepCollider: false);

        static GameObject Save(GameObject root, string name) =>
            IcaroPrefabBuilder.SavePrefab(root, $"{Folder}/{name}.prefab");
    }
}
