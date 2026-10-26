using Unity.FPS.AI;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Golem: inimigo grande, lento e resistente (GolemAI: laser com mira e pancada no chão). Aparece nas salas a
    // partir do Charco e é o chefe das Ruínas. Visual: Models/Golem/Golem.fbx (Golem_Pernas; Golem_Tronco, que gira
    // para mirar; Golem_Nucleo e Golem_Boca, de onde sai o laser). Sem o FBX, monta um placeholder de primitivas
    // com a mesma hierarquia. Vida, dano e tempos vêm da ficha Balance/Inimigo_Golem.
    public static class GolemPrefabBuilder
    {
        public const string GolemPath = IcaroAssetUtility.PrefabsFolder + "/Golem.prefab";
        const string k_HoverBotSource = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
        const string k_ImpactVfx = "Assets/FPS/Prefabs/VFX/VFX_Explosion.prefab";
        const string k_AimSound = "Assets/FPS/Audio/SFX/Weapons/Launcher_Charge_BuildUp.wav";
        const string k_FireSound = "Assets/FPS/Audio/SFX/Weapons/Launcher_Explosion.wav";

        const string k_Group = "Golem";
        const string k_Model = "Golem";
        const float k_Height = 4.2f;
        const float k_Radius = 1.1f;
        const float k_Speed = 2.2f;

        public static bool Build()
        {
            var hoverBot = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBotSource);
            EnemyController reference = hoverBot ? hoverBot.GetComponent<EnemyController>() : null;
            GolemDefinition definition = CombatAssetsBuilder.Golem;
            if (!reference || !definition)
                return false;

            GameObject model = ModelLibrary.LoadModel(k_Group, k_Model);
            Material stone = model ? ConfigureMaterials() : PlaceholderMaterials(out _, out _);

            GameObject root = EnemyPrefabBuilder.CreateEnemyRoot("Golem", reference, stone, definition.MaxHealth,
                height: k_Height, detectionRange: 50f, attackRange: definition.LaserRange);
            EnemyPrefabBuilder.AddCapsule(root, radius: k_Radius, height: k_Height);
            EnemyPrefabBuilder.ConfigureAgent(root, radius: k_Radius, height: k_Height, speed: k_Speed, acceleration: 4f,
                stoppingDistance: 2f);
            EnemyPrefabBuilder.AddStats(root, definition);
            EnemyPrefabBuilder.AddHitReaction(root, recoil: 0.05f, centerHeight: 2.2f, burstScale: 2.4f);

            Transform torso;
            Transform muzzle;
            EnemySpriteSet sprites = EnemySpriteImporter.SetFor("Golem");
            if (sprites)
            {
                // Sprite: o visual é desenhado; o tronco que gira para mirar e a origem do laser viram pontos vazios
                // na altura do núcleo do desenho
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.gameObject.AddComponent<EnemySpriteView>().Set = sprites;
                torso = new GameObject("Golem_Tronco").transform;
                torso.SetParent(root.transform, false);
                torso.localPosition = new Vector3(0f, 2.45f, 0f);
                var coreSpot = new GameObject("Golem_Nucleo").transform;
                coreSpot.SetParent(torso, false);
                coreSpot.localPosition = new Vector3(0f, 0.3f, 0.55f);
                muzzle = new GameObject("Laser_Origem").transform;
                muzzle.SetParent(coreSpot, false);
                muzzle.localPosition = new Vector3(0f, 0f, 0.35f);
            }
            else if (model)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Corpo";
                torso = body.transform.Find("Golem_Tronco");
                muzzle = torso ? CoreMuzzle(FindDeep(torso, "Golem_Nucleo")) : null;
            }
            else
            {
                BuildPlaceholder(root.transform, stone, out torso, out muzzle);
            }

            // O EnemyController exige uma arma; o laser do Golem é do GolemAI
            EnemyPrefabBuilder.AddEmptyWeapon(root, torso ? torso : root.transform, Vector3.forward);

            var ai = root.AddComponent<GolemAI>();
            ai.Torso = torso;
            ai.Muzzle = muzzle;
            ai.LineMaterial = CombatAssetsBuilder.LineMaterial;
            ai.ImpactVfx = AssetDatabase.LoadAssetAtPath<GameObject>(k_ImpactVfx);
            ai.AimSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_AimSound);
            ai.FireSound = AssetDatabase.LoadAssetAtPath<AudioClip>(k_FireSound);
            ai.SlamSound = ai.FireSound;

            // Brilho do núcleo no ambiente (luz pequena, só em tempo real)
            Transform core = muzzle ? muzzle.parent : null;
            if (core)
            {
                var light = new GameObject("LuzNucleo").AddComponent<Light>();
                light.transform.SetParent(core, false);
                light.transform.localPosition = new Vector3(0f, 0f, 0.4f);
                light.type = LightType.Point;
                light.color = new Color(0.2f, 0.9f, 1f);
                light.range = 7f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
            }

            IcaroPrefabBuilder.SavePrefab(root, GolemPath);
            return true;
        }

        // Pedra com as texturas das Ruínas; emissão ligada (preta) para o flash de dano do EnemyController
        static Material ConfigureMaterials()
        {
            ModelLibrary.BindMaterials(k_Group, k_Model);

            string ruins = ModelLibrary.TexturesFolder + "/Ruinas/Ruina_Pedra";
            Material stone = ModelLibrary.GetOrCreateMaterial(k_Group, "Golem_Pedra");
            stone.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ruins + "_BaseMap.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ruins + "_Normal.png");
            stone.SetTexture("_BumpMap", normal);
            if (normal)
                stone.EnableKeyword("_NORMALMAP");
            stone.SetFloat("_Smoothness", 0.2f);
            stone.EnableKeyword("_EMISSION");
            stone.SetColor("_EmissionColor", Color.black);

            Material runes = ModelLibrary.GetOrCreateMaterial(k_Group, "Golem_Runas_Brilho");
            ModelLibrary.SetEmission(runes, null, BiomePalettes.Ruinas.AccentCyan * 1.4f);

            Material core = ModelLibrary.GetOrCreateMaterial(k_Group, "Golem_Nucleo_Brilho");
            ModelLibrary.SetEmission(core, null, BiomePalettes.Ruinas.AccentCyan * 3f);

            EditorUtility.SetDirty(stone);
            EditorUtility.SetDirty(runes);
            EditorUtility.SetDirty(core);
            return stone;
        }

        static Material PlaceholderMaterials(out Material core, out Material runes)
        {
            core = IcaroMaterials.GetOrCreate("Golem_Placeholder_Nucleo", new Color(0.1f, 0.3f, 0.35f),
                BiomePalettes.Ruinas.AccentCyan * 2f, 0.6f);
            runes = core;
            return IcaroMaterials.GetOrCreate("Golem_Placeholder_Pedra", BiomePalettes.Ruinas.Stone, default, 0.2f);
        }

        // Placeholder de primitivas com a mesma hierarquia do modelo (pernas fixas, tronco com pivô na cintura)
        static void BuildPlaceholder(Transform root, Material stone, out Transform torso, out Transform muzzle)
        {
            PlaceholderMaterials(out Material coreMaterial, out _);

            var body = new GameObject("Corpo").transform;
            body.SetParent(root, false);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                Part(PrimitiveType.Cube, "Pe", body, new Vector3(x, 0.2f, 0.1f), new Vector3(0.95f, 0.4f, 1.2f), stone);
                Part(PrimitiveType.Cube, "Perna", body, new Vector3(x, 1.2f, 0f), new Vector3(0.75f, 1.6f, 0.8f), stone);
            }
            Part(PrimitiveType.Cube, "Pelve", body, new Vector3(0f, 2.2f, 0f), new Vector3(1.6f, 0.5f, 1f), stone);

            torso = new GameObject("Golem_Tronco").transform;
            torso.SetParent(body, false);
            torso.localPosition = new Vector3(0f, 2.45f, 0f);
            Part(PrimitiveType.Cube, "Peito", torso, new Vector3(0f, 0.65f, 0f), new Vector3(2.1f, 1.1f, 1.1f), stone);
            Part(PrimitiveType.Cube, "Cabeca", torso, new Vector3(0f, 1.4f, 0.1f), new Vector3(0.6f, 0.5f, 0.6f), stone);
            foreach (float x in new[] { -1.45f, 1.45f })
            {
                Part(PrimitiveType.Cube, "Ombro", torso, new Vector3(x, 1.1f, 0f), new Vector3(0.95f, 0.75f, 1f), stone);
                Part(PrimitiveType.Cube, "Braco", torso, new Vector3(x * 1.05f, -0.5f, 0.1f),
                    new Vector3(0.8f, 1.9f, 0.85f), stone);
            }

            var core = new GameObject("Golem_Nucleo").transform;
            core.SetParent(torso, false);
            core.localPosition = new Vector3(0f, 0.6f, 0.6f);
            Part(PrimitiveType.Sphere, "Cristal", core, Vector3.zero, new Vector3(0.5f, 0.5f, 0.4f), coreMaterial);

            muzzle = new GameObject("Golem_Boca").transform;
            muzzle.SetParent(core, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.35f);
        }

        // Origem do laser na face da frente da pedra ciano (Golem_Nucleo). O "Golem_Boca" do FBX ficou 1,5 m abaixo
        // do núcleo na conversão de eixos, então o ponto é recriado a partir da malha do núcleo.
        static Transform CoreMuzzle(Transform core)
        {
            if (!core)
                return null;

            var muzzle = new GameObject("Laser_Origem").transform;
            muzzle.SetParent(core, false);
            var renderer = core.GetComponent<Renderer>();
            Bounds bounds = renderer ? renderer.bounds : new Bounds(core.position, Vector3.zero);
            // O prefab é montado na origem, olhando para +Z: o mundo coincide com o espaço do Golem
            muzzle.SetPositionAndRotation(bounds.center + Vector3.forward * (bounds.extents.z + 0.05f),
                Quaternion.identity);
            return muzzle;
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material) =>
            IcaroPrefabBuilder.CreatePart(type, name, parent, position, scale, material, keepCollider: false);

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }
    }
}
