using System;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Icaro.EditorTools
{
    // Armas do Ícaro: as fichas (Balance/Armas/Arma_*.asset, criadas só se faltarem, para preservar o que foi ajustado
    // no Inspector), o catálogo (Balance/Armas/WeaponCatalog.asset, ligado ao ShopSettings.Weapons) e os prefabs
    // (Prefabs/Armas). A Agulha é a arma inicial (Weapon_Blaster_Icaro, com o visual próprio, ganha o IcaroWeapon);
    // as outras são variantes de armas do Microgame com as cores da ficha e o IcaroWeapon. O Ion akimbos ganha uma
    // segunda pistola espelhada e um segundo cano. Cada projétil é o visual de um projétil do Microgame com o
    // HomingProjectile. Fichas, prefabs e materiais das armas antigas são apagados. Menu "Ícaro > Gerar armas";
    // também roda em "Ícaro > Gerar prefabs".
    public static class WeaponAssetsBuilder
    {
        public const string Folder = CombatAssetsBuilder.Folder + "/Armas";
        public const string CatalogPath = Folder + "/WeaponCatalog.asset";
        const string k_PrefabFolder = IcaroAssetUtility.PrefabsFolder + "/Armas";
        const string k_MaterialFolder = IcaroAssetUtility.MaterialsFolder + "/Armas";

        const string k_Blaster = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";
        const string k_Launcher = "Assets/FPS/Prefabs/Weapons/Weapon_Launcher.prefab";
        const string k_Shotgun = "Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab";
        const string k_BlasterShot = "Assets/FPS/Prefabs/Projectiles/Projectile_Blaster.prefab";
        const string k_ShotgunShot = "Assets/FPS/Prefabs/Projectiles/Projectile_Shotgun.prefab";

        // Armas antigas, removidas do jogo
        static readonly string[] k_Removed =
            { "Cronocanhao", "RifleAncora", "EscopetaFase", "LancaEcos", "DiscoRicochete", "ArcoPlasma" };

        // Quanto a cor da ficha tinge os materiais do modelo da arma
        const float k_WeaponTint = 0.45f;

        // Distância entre as duas pistolas do akimbo (a arma fica à direita da câmera; a cópia, espelhada à esquerda)
        const float k_AkimboOffset = 0.528f;
        const string k_LeftGun = "Arma_Esquerda";
        const string k_LeftMuzzle = "GunMuzzle_Esquerda";

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        public static WeaponCatalog Catalog => AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);

        [MenuItem("Ícaro/Gerar armas", priority = 3)]
        public static WeaponCatalog EnsureAll()
        {
            IcaroAssetUtility.EnsureFolder(Folder);
            IcaroAssetUtility.EnsureFolder(k_PrefabFolder);
            IcaroAssetUtility.EnsureFolder(k_MaterialFolder);
            DeleteRemoved();

            var weapons = new List<WeaponDefinition>
            {
                Build(Definition("Agulha", d =>
                {
                    Shop(d, "Agulha", "Fuzil de assalto inicial. Versátil, serve para tudo.", "A",
                        new Color(0.55f, 0.95f, 1f), 60);
                    Gun(d, 700f, 50, 1.8f, 0f, 0.4f);
                    Shot(d, 30f, 300f, 0.05f, 120f);
                }), IcaroPrefabBuilder.BlasterVariantPath, k_BlasterShot, 1f, tint: false),

                Build(Definition("Titan", d =>
                {
                    Shop(d, "Titan", "Metralhadora pesada. Alto dano e impacto.", "T", new Color(1f, 0.55f, 0.2f), 110);
                    Gun(d, 400f, 25, 2.5f, 1.5f, 0.9f);
                    Shot(d, 65f, 220f, 0.09f, 120f);
                }), k_Launcher, k_BlasterShot, 1.6f),

                Build(Definition("IonAkimbos", d =>
                {
                    Shop(d, "Ion akimbos", "Revólveres de tambor duplos. Tiros cadenciados, pesados e precisos.", "I",
                        new Color(0.55f, 0.6f, 1f), 110);
                    Gun(d, 240f, 16, 1.6f, 0f, 1.2f);
                    d.Akimbo = true;
                    Shot(d, 120f, 350f, 0.07f, 140f);
                }), k_Blaster, k_BlasterShot, 1.4f),

                Build(Definition("Viper", d =>
                {
                    Shop(d, "Viper", "Submetralhadora. Altíssima cadência para ativar os efeitos de acerto das cartas.",
                        "V", new Color(0.45f, 1f, 0.45f), 95);
                    Gun(d, 1000f, 60, 1.4f, 2.5f, 0.25f);
                    Shot(d, 22f, 320f, 0.04f, 100f);
                    d.Suppressed = true;
                }), k_Blaster, k_BlasterShot, 0.8f),

                Build(Definition("Liberdade", d =>
                {
                    Shop(d, "Liberdade", "Escopeta automática. Mesmo dano a qualquer distância, mira toda manual.", "L",
                        new Color(1f, 0.82f, 0.3f), 120);
                    Gun(d, 150f, 12, 2.5f, 9f, 1.6f);
                    d.PelletsPerShot = 10;
                    Shot(d, 25f, 140f, 0.08f, 60f);
                    d.AimMode = WeaponAimMode.None;
                    d.ProcCoefficient = 0.3f;
                }), k_Shotgun, k_ShotgunShot, 1f),
            };

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Weapons = weapons.FindAll(w => w);
            EditorUtility.SetDirty(catalog);

            var shop = AssetDatabase.LoadAssetAtPath<ShopSettings>(ShopPrefabBuilder.SettingsPath);
            if (shop)
            {
                shop.Weapons = catalog;
                EditorUtility.SetDirty(shop);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Ícaro] {catalog.Weapons.Count} armas geradas em {k_PrefabFolder}");
            return catalog;
        }

        // Fichas, prefabs e materiais das armas que saíram do jogo (o AssetDatabase apaga junto os .meta)
        static void DeleteRemoved()
        {
            foreach (string id in k_Removed)
            {
                foreach (string path in new[]
                         {
                             $"{Folder}/Arma_{id}.asset", $"{k_PrefabFolder}/Arma_{id}.prefab",
                             $"{k_PrefabFolder}/Projetil_{id}.prefab",
                         })
                {
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                        AssetDatabase.DeleteAsset(path);
                }

                foreach (string guid in AssetDatabase.FindAssets($"Arma_{id}_ t:Material", new[] { k_MaterialFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileName(path).StartsWith($"Arma_{id}_"))
                        AssetDatabase.DeleteAsset(path);
                }
            }
        }

        // ---------- fichas ----------

        static WeaponDefinition Definition(string id, Action<WeaponDefinition> configure)
        {
            string path = $"{Folder}/Arma_{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition)
                return definition;

            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            configure(definition);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        static void Shop(WeaponDefinition d, string name, string role, string letter, Color color, int price)
        {
            d.DisplayName = name;
            d.Role = role;
            d.Letter = letter;
            d.Color = color;
            d.Price = price;
        }

        static void Gun(WeaponDefinition d, float rpm, int magazine, float reload, float spread, float recoil)
        {
            d.ShootType = WeaponShootType.Automatic;
            d.RoundsPerMinute = rpm;
            d.MagazineSize = magazine;
            d.ReloadTime = reload;
            d.SpreadAngle = spread;
            d.RecoilForce = recoil;
        }

        static void Shot(WeaponDefinition d, float damage, float speed, float radius, float range)
        {
            d.Damage = damage;
            d.ProjectileSpeed = speed;
            d.ProjectileRadius = radius;
            d.Range = range;
        }

        // ---------- prefabs ----------

        static WeaponDefinition Build(WeaponDefinition definition, string weaponSource, string projectileSource,
            float projectileScale, bool tint = true)
        {
            string id = definition.name.Replace("Arma_", "");
            HomingProjectile projectile = BuildProjectile(definition, id, projectileSource, projectileScale);
            WeaponController weapon = weaponSource == IcaroPrefabBuilder.BlasterVariantPath
                ? LinkExistingWeapon(definition, weaponSource, projectile)
                : BuildWeapon(definition, id, weaponSource, projectile, tint);
            if (!projectile || !weapon)
                Debug.LogError("[Ícaro] Falha ao montar a arma " + definition.DisplayName);

            definition.Projectile = projectile;
            definition.WeaponPrefab = weapon;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // Visual de um projétil do Microgame, sem a lógica dele, com o HomingProjectile e as cores da ficha
        static HomingProjectile BuildProjectile(WeaponDefinition definition, string id, string sourcePath, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source)
            {
                Debug.LogError("[Ícaro] Prefab não encontrado: " + sourcePath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Projetil_" + id;

            var standard = instance.GetComponent<ProjectileStandard>();
            GameObject impactVfx = standard ? standard.ImpactVfx : null;
            float impactLifetime = standard ? standard.ImpactVfxLifetime : 2f;
            float impactOffset = standard ? standard.ImpactVfxSpawnOffset : 0.1f;
            AudioClip impactSfx = standard ? standard.ImpactSfxClip : null;
            LayerMask layers = standard ? standard.HittableLayers : (LayerMask)(-1);

            // Componentes dependentes primeiro (alguns exigem o ProjectileStandard)
            RemoveAll<ChargedProjectileEffectsHandler>(instance);
            RemoveAll<ProjectileChargeParameters>(instance);
            RemoveAll<PlayerProjectile>(instance);
            RemoveAll<DamageArea>(instance);
            RemoveAll<ProjectileStandard>(instance);
            RemoveAll<HomingProjectile>(instance);
            RemoveAll<Collider>(instance);
            RemoveAll<Rigidbody>(instance);

            var projectile = instance.AddComponent<HomingProjectile>();
            projectile.Definition = definition;
            projectile.Speed = definition.ProjectileSpeed;
            projectile.Damage = definition.Damage;
            projectile.Radius = definition.ProjectileRadius;
            projectile.ImpactVfx = impactVfx;
            projectile.ImpactVfxLifetime = impactLifetime;
            projectile.ImpactVfxSpawnOffset = impactOffset;
            projectile.ImpactSfxClip = impactSfx;
            projectile.HittableLayers = layers;

            Color color = definition.Color;
            Material glow = IcaroMaterials.GetOrCreate("Arma_Projetil_" + id, color, color * 3f, 0.8f);
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = glow;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            instance.transform.localScale *= scale;
            ColorTrail(instance, color);

            GameObject saved = IcaroPrefabBuilder.SavePrefab(instance, $"{k_PrefabFolder}/Projetil_{id}.prefab");
            return saved ? saved.GetComponent<HomingProjectile>() : null;
        }

        static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                Object.DestroyImmediate(component);
        }

        // Rastro na cor da ficha (o do projétil original ou um curto)
        static void ColorTrail(GameObject instance, Color color)
        {
            var trail = instance.GetComponentInChildren<TrailRenderer>(true);
            if (!trail)
            {
                trail = instance.AddComponent<TrailRenderer>();
                trail.sharedMaterial = CombatAssetsBuilder.LineMaterial;
                trail.time = 0.08f;
                trail.minVertexDistance = 0.3f;
                trail.widthCurve = AnimationCurve.Linear(0f, 0.08f, 1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        // Arma inicial: o prefab já existe (com o visual da Agulha); só liga a ficha
        static WeaponController LinkExistingWeapon(WeaponDefinition definition, string path, HomingProjectile projectile)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                Debug.LogError("[Ícaro] Arma inicial não encontrada (rode Ícaro > Gerar prefabs): " + path);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LinkDefinition(contents, definition, projectile);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return saved ? saved.GetComponent<WeaponController>() : null;
        }

        static IcaroWeapon LinkDefinition(GameObject contents, WeaponDefinition definition, HomingProjectile projectile)
        {
            var link = contents.GetComponent<IcaroWeapon>();
            if (!link)
                link = contents.AddComponent<IcaroWeapon>();
            link.Definition = definition;
            definition.Projectile = projectile;
            definition.ApplyTo(contents.GetComponent<WeaponController>());
            return link;
        }

        // Variante de uma arma do Microgame: materiais tingidos com a cor da ficha, o IcaroWeapon e os números da ficha
        static WeaponController BuildWeapon(WeaponDefinition definition, string id, string sourcePath,
            HomingProjectile projectile, bool tint)
        {
            string path = $"{k_PrefabFolder}/Arma_{id}.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source)
            {
                Debug.LogError("[Ícaro] Prefab não encontrado: " + sourcePath);
                return null;
            }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = "Arma_" + id;
                IcaroPrefabBuilder.SavePrefab(instance, path);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                IcaroWeapon link = LinkDefinition(contents, definition, projectile);
                if (tint)
                    Tint(contents, source, id, definition.Color);
                link.SecondMuzzle = definition.Akimbo ? BuildAkimbo(contents) : null;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return saved ? saved.GetComponent<WeaponController>() : null;
        }

        // Segunda pistola: cópia espelhada do modelo à esquerda (dentro do WeaponRoot, para sumir junto ao trocar de
        // arma) e um segundo cano na mesma posição relativa. Devolve o cano novo.
        static Transform BuildAkimbo(GameObject contents)
        {
            var weapon = contents.GetComponent<WeaponController>();
            if (!weapon || !weapon.WeaponRoot || !weapon.WeaponMuzzle)
                return null;

            Transform root = weapon.WeaponRoot.transform;
            if (!root.Find(k_LeftGun))
            {
                Transform model = null;
                foreach (Transform child in root)
                {
                    if (child.GetComponentInChildren<Renderer>(true))
                    {
                        model = child;
                        break;
                    }
                }

                if (!model)
                    return null;
                Transform left = Object.Instantiate(model.gameObject, root).transform;
                left.name = k_LeftGun;
                left.localPosition = model.localPosition + Vector3.left * k_AkimboOffset;
                left.localRotation = model.localRotation;
                left.localScale = Vector3.Scale(model.localScale, new Vector3(-1f, 1f, 1f));
            }

            Transform muzzle = weapon.WeaponMuzzle;
            Transform secondMuzzle = muzzle.parent.Find(k_LeftMuzzle);
            if (!secondMuzzle)
            {
                secondMuzzle = new GameObject(k_LeftMuzzle).transform;
                secondMuzzle.SetParent(muzzle.parent, false);
            }

            secondMuzzle.localPosition = muzzle.localPosition + Vector3.left * k_AkimboOffset;
            secondMuzzle.localRotation = muzzle.localRotation;
            secondMuzzle.localScale = muzzle.localScale;
            return secondMuzzle;
        }

        // Cópias dos materiais do modelo original, tingidas (refeitas a partir do original a cada geração)
        static void Tint(GameObject contents, GameObject source, string id, Color color)
        {
            Renderer[] originals = ModelRenderers(source);
            Renderer[] renderers = ModelRenderers(contents);
            int count = Mathf.Min(originals.Length, renderers.Length);
            var copies = new Dictionary<Material, Material>();
            for (int r = 0; r < count; r++)
            {
                Material[] materials = originals[r].sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!original)
                        continue;
                    if (!copies.TryGetValue(original, out Material copy))
                        copies[original] = copy = TintedCopy(original, id, color);
                    materials[i] = copy;
                }

                renderers[r].sharedMaterials = materials;
            }
        }

        static Renderer[] ModelRenderers(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    result.Add(renderer);
            }

            return result.ToArray();
        }

        static Material TintedCopy(Material original, string id, Color color)
        {
            string path = $"{k_MaterialFolder}/Arma_{id}_{original.name}.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!copy)
            {
                copy = new Material(original) { name = $"Arma_{id}_{original.name}" };
                AssetDatabase.CreateAsset(copy, path);
            }
            else
            {
                copy.shader = original.shader;
                copy.CopyPropertiesFromMaterial(original);
            }

            if (copy.HasProperty(k_BaseColor))
            {
                Color baseColor = original.GetColor(k_BaseColor);
                Color tinted = Color.Lerp(baseColor, color, k_WeaponTint);
                tinted.a = baseColor.a;
                copy.SetColor(k_BaseColor, tinted);
            }

            EditorUtility.SetDirty(copy);
            return copy;
        }
    }
}
