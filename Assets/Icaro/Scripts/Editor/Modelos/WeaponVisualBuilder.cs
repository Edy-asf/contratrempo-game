using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icaro.EditorTools
{
    // Agulha: troca só o visual do Blaster do Ícaro (Weapon_Blaster_Icaro, variante do Blaster do Microgame).
    // O WeaponController, a boca (GunMuzzle), o FuelCellHandler e o OverheatBehavior continuam os mesmos; a malha
    // original (WeaponMesh_Pistol) fica desligada e a sonda (Models/Agulha/Agulha.fbx) entra sob o GunRoot, que é o
    // WeaponRoot mostrado/escondido pelo WeaponController. O FBX já vem no espaço da arma: ponta na GunMuzzle.
    public static class WeaponVisualBuilder
    {
        const string k_Group = "Agulha";
        const string k_Model = "Agulha";
        const string k_VisualName = "Agulha_Visual";
        const string k_OriginalMesh = "WeaponMesh_Pistol";
        const string k_WeaponRoot = "GunRoot";

        public static bool Build()
        {
            GameObject model = ModelLibrary.LoadModel(k_Group, k_Model);
            if (!model || !AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.BlasterVariantPath))
                return false;

            ModelLibrary.BindMaterials(k_Group, k_Model);
            ConfigureMaterials();

            GameObject contents = PrefabUtility.LoadPrefabContents(IcaroPrefabBuilder.BlasterVariantPath);
            try
            {
                Transform weaponRoot = FindDeep(contents.transform, k_WeaponRoot) ?? contents.transform;
                Transform original = FindDeep(contents.transform, k_OriginalMesh);
                if (original)
                    original.gameObject.SetActive(false);

                Transform old = weaponRoot.Find(k_VisualName);
                if (old)
                    Object.DestroyImmediate(old.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, weaponRoot);
                visual.name = k_VisualName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;

                var weapon = contents.GetComponent<WeaponController>();
                if (weapon)
                    weapon.WeaponName = "Agulha";

                PrefabUtility.SaveAsPrefabAsset(contents, IcaroPrefabBuilder.BlasterVariantPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return true;
        }

        // Peças sem textura: aço escovado, borracha preta e o fluido de amostra ciano
        static void ConfigureMaterials()
        {
            Material metal = ModelLibrary.GetOrCreateMaterial(k_Group, "Agulha_Metal");
            metal.SetColor("_BaseColor", new Color(0.42f, 0.43f, 0.45f));
            metal.SetFloat("_Metallic", 1f);
            metal.SetFloat("_Smoothness", 0.62f);

            Material rubber = ModelLibrary.GetOrCreateMaterial(k_Group, "Agulha_Borracha");
            rubber.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.032f));
            rubber.SetFloat("_Smoothness", 0.3f);

            Material glow = ModelLibrary.GetOrCreateMaterial(k_Group, "Agulha_Brilho");
            ModelLibrary.SetEmission(glow, null, BiomePalettes.Bosque.AccentCyan * 1.5f);

            EditorUtility.SetDirty(metal);
            EditorUtility.SetDirty(rubber);
            EditorUtility.SetDirty(glow);
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }
    }
}
