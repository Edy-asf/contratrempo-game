using System;
using System.IO;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Gerar prefabs": monta os prefabs do Ícaro a partir de primitivas e dos prefabs do Microgame.
    // Pode ser rodado de novo a qualquer momento: Metrônomo, inimigos e Player são refeitos (mantendo o GUID);
    // as variantes de arma e de projétil só são criadas se faltarem, para preservar ajustes feitos no Inspector.
    public static class IcaroPrefabBuilder
    {
        public const string MetronomePath = IcaroAssetUtility.PrefabsFolder + "/Metronomo.prefab";
        public const string HoverBotVariantPath = IcaroAssetUtility.PrefabsFolder + "/Enemy_HoverBot_Icaro.prefab";
        public const string TurretVariantPath = IcaroAssetUtility.PrefabsFolder + "/Enemy_Turret_Icaro.prefab";

        public const string PlayerVariantPath = IcaroAssetUtility.PrefabsFolder + "/Player_Icaro.prefab";
        public const string BlasterVariantPath = IcaroAssetUtility.PrefabsFolder + "/Weapon_Blaster_Icaro.prefab";

        const string k_HoverBotSource = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
        const string k_TurretSource = "Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab";
        const string k_PlayerSource = "Assets/FPS/Prefabs/Player.prefab";
        const string k_BlasterSource = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";

        // Velocidade base (a 1,0x) dos projéteis inimigos; os originais voam a 100 m/s
        const float k_EnemyProjectileSpeed = 22f;

        // Munição do Blaster do jogador em relação ao original
        const int k_PlayerAmmoMultiplier = 4;

        // Distância máxima de desenho da câmera do jogador (o original é 300 m)
        const float k_PlayerFarClip = 150f;

        [MenuItem("Ícaro/Gerar prefabs", priority = 0)]
        public static void GenerateAll()
        {
            IcaroAssetUtility.EnsureFolder(IcaroAssetUtility.PrefabsFolder);
            CombatAssetsBuilder.EnsureAll();

            BuildMetronome();
            BuildEnemyVariant(k_HoverBotSource, HoverBotVariantPath);
            BuildEnemyVariant(k_TurretSource, TurretVariantPath);
            BuildPlayerVariant(BuildBlasterVariant());
            CombatFeedbackBuilder.BuildAll();
            EnemyPrefabBuilder.BuildAll();
            ScenePropKit.EnsureAll();

            AssetDatabase.SaveAssets();
            Debug.Log("[Ícaro] Prefabs gerados em " + IcaroAssetUtility.PrefabsFolder);
        }

        static void BuildMetronome()
        {
            Material baseMaterial = IcaroMaterials.GetOrCreate("Metronomo_Base", new Color(0.18f, 0.2f, 0.24f));
            Material crystalMaterial = IcaroMaterials.GetOrCreate("Metronomo_Cristal", new Color(0.2f, 0.9f, 1f),
                new Color(0f, 1.5f, 2f), 0.8f);

            var root = new GameObject("Metronomo");

            // Área de ativação: maior que o pilar, para o jogador entrar nela antes de encostar no objeto
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(3f, 3f, 3f);

            CreatePart(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.15f, 0f),
                new Vector3(1.6f, 0.15f, 1.6f), baseMaterial, keepCollider: true);
            GameObject pillar = CreatePart(PrimitiveType.Cube, "Pilar", root.transform, new Vector3(0f, 1.35f, 0f),
                new Vector3(0.35f, 2.4f, 0.35f), crystalMaterial, keepCollider: true);

            var pendulumPivot = new GameObject("Pendulo").transform;
            pendulumPivot.SetParent(root.transform, false);
            pendulumPivot.localPosition = new Vector3(0f, 2.5f, 0.3f);
            CreatePart(PrimitiveType.Cube, "Haste", pendulumPivot, new Vector3(0f, -0.75f, 0f),
                new Vector3(0.06f, 1.5f, 0.06f), baseMaterial, keepCollider: false);
            GameObject weight = CreatePart(PrimitiveType.Cube, "Peso", pendulumPivot, new Vector3(0f, -1.45f, 0f),
                new Vector3(0.3f, 0.3f, 0.12f), crystalMaterial, keepCollider: false);

            var metronome = root.AddComponent<Metronome>();
            metronome.Indicators = new[] { pillar.GetComponent<Renderer>(), weight.GetComponent<Renderer>() };
            metronome.Pendulum = pendulumPivot;

            // Luz ciano realtime (é a única luz dinâmica do cenário), sem sombra para ficar barata
            var lightObject = new GameObject("Luz");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.2f, 0.9f, 1f);
            light.range = 7f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            metronome.IndicatorLight = light;

            // Feixe alto, aditivo, que atravessa a névoa: o Metrônomo pronto é visível de longe
            Material beamMaterial = IcaroMaterials.GetOrCreateAdditiveParticle("Metronomo_Feixe", null);
            beamMaterial.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f, 0.4f));
            beamMaterial.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(beamMaterial);
            GameObject beam = CreatePart(PrimitiveType.Cylinder, "Feixe", root.transform, new Vector3(0f, 14f, 0f),
                new Vector3(0.35f, 12f, 0.35f), beamMaterial, keepCollider: false);
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            metronome.Beacon = beam.GetComponent<Renderer>();

            SavePrefab(root, MetronomePath);
        }

        // Variante do prefab original com o EnemyTimeScaler; o original do Microgame não é alterado
        static void BuildEnemyVariant(string sourcePath, string variantPath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source)
            {
                Debug.LogError("[Ícaro] Prefab não encontrado: " + sourcePath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (!instance.GetComponent<EnemyTimeScaler>())
                instance.AddComponent<EnemyTimeScaler>();

            // Armas do inimigo passam a disparar a versão lenta do projétil
            foreach (var weapon in instance.GetComponentsInChildren<WeaponController>(true))
            {
                if (weapon.ProjectilePrefab)
                    weapon.ProjectilePrefab = GetSlowProjectile(weapon.ProjectilePrefab);
            }

            instance.name = Path.GetFileNameWithoutExtension(variantPath);
            SavePrefab(instance, variantPath);
        }

        // Variante "{nome}_Icaro" do projétil com velocidade reduzida, para dar para desviar.
        // Só é criada se não existir, para preservar ajustes feitos à mão no Inspector.
        static ProjectileBase GetSlowProjectile(ProjectileBase original)
        {
            string sourcePath = AssetDatabase.GetAssetPath(original);
            string variantPath = $"{IcaroAssetUtility.PrefabsFolder}/{original.name}_Icaro.prefab";

            GameObject variant = CreateVariantIfMissing(sourcePath, variantPath, instance =>
            {
                var projectile = instance.GetComponent<ProjectileStandard>();
                if (projectile)
                    projectile.Speed = k_EnemyProjectileSpeed;
            });

            return variant ? variant.GetComponent<ProjectileBase>() : original;
        }

        // Blaster com 4× a munição; a recarga também é ×4 para continuar enchendo no mesmo tempo
        static GameObject BuildBlasterVariant()
        {
            return CreateVariantIfMissing(k_BlasterSource, BlasterVariantPath, instance =>
            {
                var weapon = instance.GetComponent<WeaponController>();
                weapon.MaxAmmo *= k_PlayerAmmoMultiplier;
                weapon.AmmoReloadRate *= k_PlayerAmmoMultiplier;
            });
        }

        // Jogador do Ícaro: o Player do Microgame começando com o Blaster do Ícaro
        static void BuildPlayerVariant(GameObject blasterVariant)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(k_PlayerSource);
            var originalBlaster = AssetDatabase.LoadAssetAtPath<WeaponController>(k_BlasterSource);
            if (!source || !blasterVariant)
            {
                Debug.LogError("[Ícaro] Não foi possível montar o Player_Icaro.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            var weapons = instance.GetComponent<PlayerWeaponsManager>();
            var newBlaster = blasterVariant.GetComponent<WeaponController>();
            for (int i = 0; i < weapons.StartingWeapons.Count; i++)
            {
                if (weapons.StartingWeapons[i] == originalBlaster)
                    weapons.StartingWeapons[i] = newBlaster;
            }

            // Depois de ~150 m tudo é névoa: encurtar o far clip poupa culling e desenho
            var character = instance.GetComponent<PlayerCharacterController>();
            if (character && character.PlayerCamera)
                character.PlayerCamera.farClipPlane = k_PlayerFarClip;

            // Vida base e outros valores do jogador vêm do balanceamento de combate
            instance.AddComponent<PlayerBalance>().Balance = CombatAssetsBuilder.Balance;

            // Atributos e itens da run (defesas, efeitos de acerto e pulo no ar são adicionados em jogo)
            instance.AddComponent<PlayerStats>().Catalog = ItemAssetsBuilder.Catalog;

            instance.name = Path.GetFileNameWithoutExtension(PlayerVariantPath);
            SavePrefab(instance, PlayerVariantPath);
        }

        static GameObject CreateVariantIfMissing(string sourcePath, string variantPath, Action<GameObject> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (existing)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source)
            {
                Debug.LogError("[Ícaro] Prefab não encontrado: " + sourcePath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            configure(instance);
            instance.name = Path.GetFileNameWithoutExtension(variantPath);
            return SavePrefab(instance, variantPath);
        }

        public static GameObject CreatePart(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;

            if (!keepCollider)
                Object.DestroyImmediate(part.GetComponent<Collider>());

            return part;
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);

            if (!success)
                Debug.LogError("[Ícaro] Falha ao salvar o prefab " + path);

            return prefab;
        }
    }
}
