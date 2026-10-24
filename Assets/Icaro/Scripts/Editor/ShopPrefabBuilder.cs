using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Loja: a ficha (Balance/ShopSettings, criada só se faltar), o cristal de Cronita (Prefabs/Cronita) e o terminal
    // de loja (Prefabs/Loja_Terminal: pedestal, tela e um holograma girando, com luz). Chamado pelo menu
    // "Ícaro > Gerar prefabs" (CombatAssetsBuilder.EnsureAll).
    public static class ShopPrefabBuilder
    {
        public const string SettingsPath = CombatAssetsBuilder.Folder + "/ShopSettings.asset";
        const string k_CrystalPath = IcaroAssetUtility.PrefabsFolder + "/Cronita.prefab";
        const string k_TerminalPath = IcaroAssetUtility.PrefabsFolder + "/Loja_Terminal.prefab";
        const string k_PickupSfx = "Assets/FPS/Audio/SFX/Pickups/Pickup_Health.wav";

        public static ShopSettings Build()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShopSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<ShopSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.CrystalPrefab = BuildCrystal();
            settings.TerminalPrefab = BuildTerminal();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static CronitaPickup BuildCrystal()
        {
            Material crystal = IcaroMaterials.GetOrCreate("Cronita", new Color(0.35f, 0.85f, 1f),
                new Color(0.5f, 2.2f, 3f), 0.85f);
            var root = new GameObject("Cronita");
            var visual = new GameObject("Cristal").transform;
            visual.SetParent(root.transform, false);
            GameObject top = Part(PrimitiveType.Cube, "Ponta", visual, Vector3.zero, new Vector3(0.16f, 0.26f, 0.16f),
                crystal);
            top.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var pickup = root.AddComponent<CronitaPickup>();
            pickup.Visual = visual;
            pickup.PickupSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(k_PickupSfx);
            return IcaroPrefabBuilder.SavePrefab(root, k_CrystalPath).GetComponent<CronitaPickup>();
        }

        static ShopTerminal BuildTerminal()
        {
            Material metal = IcaroMaterials.GetOrCreate("Loja_Metal", new Color(0.18f, 0.2f, 0.24f), default, 0.5f);
            Material screen = IcaroMaterials.GetOrCreate("Loja_Tela", new Color(0.1f, 0.25f, 0.3f),
                new Color(0.3f, 1.4f, 1.8f), 0.7f);
            Material crystal = IcaroMaterials.GetOrCreate("Cronita", new Color(0.35f, 0.85f, 1f),
                new Color(0.5f, 2.2f, 3f), 0.85f);

            var root = new GameObject("Loja_Terminal");
            Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.15f, 0f),
                new Vector3(1.6f, 0.15f, 1.6f), metal, keepCollider: true);
            Part(PrimitiveType.Cube, "Coluna", root.transform, new Vector3(0f, 0.75f, 0f),
                new Vector3(0.5f, 1.2f, 0.5f), metal, keepCollider: true);
            GameObject panel = Part(PrimitiveType.Cube, "Tela", root.transform, new Vector3(0f, 1.45f, 0.18f),
                new Vector3(0.9f, 0.5f, 0.08f), screen);
            panel.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

            // Holograma: um cristal grande girando acima do terminal
            var hologram = new GameObject("Holograma").transform;
            hologram.SetParent(root.transform, false);
            hologram.localPosition = new Vector3(0f, 2.3f, 0f);
            GameObject gem = Part(PrimitiveType.Cube, "Cristal", hologram, Vector3.zero, new Vector3(0.4f, 0.6f, 0.4f),
                crystal);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.9f, 1f);
            light.range = 6f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            var terminal = root.AddComponent<ShopTerminal>();
            terminal.Hologram = hologram;
            return IcaroPrefabBuilder.SavePrefab(root, k_TerminalPath).GetComponent<ShopTerminal>();
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false) =>
            IcaroPrefabBuilder.CreatePart(type, name, parent, position, scale, material, keepCollider);
    }
}
