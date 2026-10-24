using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Prefab do fragmento de metrônomo (Prefabs/Fragmento_Metronomo): cristal ciano bioluminescente com núcleo
    // âmbar que gira e flutua, luz pontual e um feixe vertical para ser visto de longe na névoa. Ligado ao
    // CombatBalance.FragmentPrefab. Chamado pelo menu "Ícaro > Gerar prefabs".
    public static class MetronomeFragmentBuilder
    {
        public const string FragmentPath = IcaroAssetUtility.PrefabsFolder + "/Fragmento_Metronomo.prefab";

        const string k_PickupSfx = "Assets/FPS/Audio/SFX/Pickups/Pickup_Weapon_Small.wav";

        public static GameObject Build()
        {
            Material crystal = IcaroMaterials.GetOrCreate("Fragmento_Cristal", new Color(0.35f, 0.85f, 0.95f),
                new Color(0.6f, 2.6f, 3.2f), 0.9f);
            Material core = IcaroMaterials.GetOrCreate("Fragmento_Nucleo", new Color(1f, 0.7f, 0.25f),
                new Color(3.5f, 1.8f, 0.3f), 0.6f);
            Material beam = IcaroMaterials.GetOrCreateAdditiveParticle("Fragmento_Feixe", null);
            beam.SetColor("_BaseColor", new Color(0.25f, 0.8f, 1f, 0.35f));
            beam.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(beam);

            var root = new GameObject("Fragmento_Metronomo");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // Dois cubos girados formam um cristal facetado; o núcleo âmbar fica no centro
            GameObject upper = Part(PrimitiveType.Cube, "Cristal_A", visual, new Vector3(0.22f, 0.36f, 0.22f), crystal);
            upper.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            GameObject lower = Part(PrimitiveType.Cube, "Cristal_B", visual, new Vector3(0.2f, 0.32f, 0.2f), crystal);
            lower.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            GameObject heart = Part(PrimitiveType.Sphere, "Nucleo", visual, Vector3.one * 0.12f, core);

            GameObject column = Part(PrimitiveType.Cylinder, "Feixe", root.transform, new Vector3(0.08f, 3f, 0.08f), beam);
            column.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            column.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.9f, 1f);
            light.range = 5f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            var fragment = root.AddComponent<MetronomeFragment>();
            fragment.Visual = visual;
            fragment.BlinkRenderers = new[]
            {
                upper.GetComponent<Renderer>(), lower.GetComponent<Renderer>(), heart.GetComponent<Renderer>(),
                column.GetComponent<Renderer>(),
            };
            fragment.Glow = light;
            fragment.PickupSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(k_PickupSfx);

            return IcaroPrefabBuilder.SavePrefab(root, FragmentPath);
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 scale, Material material)
        {
            GameObject part = IcaroPrefabBuilder.CreatePart(type, name, parent, Vector3.zero, scale, material,
                keepCollider: false);
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part;
        }
    }
}
