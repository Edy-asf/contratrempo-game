using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Prefab do orbe do tempo (Prefabs/Orbe_Tempo): núcleo âmbar brilhante com uma casca ciano, luz pontual e o anel
    // que fecha (LineRenderer aditivo, desenhado em jogo pelo TimeOrb). Ligado ao CombatBalance.TimeOrbs.Prefab.
    // Apaga o prefab do fragmento de metrônomo, que o orbe substituiu. Chamado pelo menu "Ícaro > Gerar prefabs".
    public static class TimeOrbBuilder
    {
        public const string OrbPath = IcaroAssetUtility.PrefabsFolder + "/Orbe_Tempo.prefab";
        const string k_OldFragmentPath = IcaroAssetUtility.PrefabsFolder + "/Fragmento_Metronomo.prefab";
        const string k_HitSfx = "Assets/FPS/Audio/SFX/Pickups/Pickup_Weapon_Small.wav";

        public static GameObject Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_OldFragmentPath))
                AssetDatabase.DeleteAsset(k_OldFragmentPath);

            Material core = IcaroMaterials.GetOrCreate("Orbe_Nucleo", new Color(1f, 0.75f, 0.3f),
                new Color(4f, 2.2f, 0.5f), 0.6f);
            Material shell = IcaroMaterials.GetOrCreateAdditiveParticle("Orbe_Casca", null);
            shell.SetColor("_BaseColor", new Color(0.3f, 0.9f, 1f, 0.45f));
            shell.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(shell);
            Material ring = IcaroMaterials.GetOrCreateAdditiveParticle("Orbe_Anel", null);
            ring.SetColor("_BaseColor", Color.white);
            ring.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(ring);

            var root = new GameObject("Orbe_Tempo");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            var visual = new GameObject("Nucleo").transform;
            visual.SetParent(root.transform, false);
            visual.localScale = Vector3.one * 0.8f;
            Part(PrimitiveType.Sphere, "Centro", visual, Vector3.one * 0.6f, core);
            Part(PrimitiveType.Sphere, "Casca", visual, Vector3.one, shell);

            var line = new GameObject("Anel").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.sharedMaterial = ring;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.06f;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.35f);
            light.range = 6f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            var orb = root.AddComponent<TimeOrb>();
            orb.Core = visual;
            orb.Ring = line;
            orb.Glow = light;
            orb.HitSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(k_HitSfx);
            return IcaroPrefabBuilder.SavePrefab(root, OrbPath);
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 scale, Material material)
        {
            GameObject part = IcaroPrefabBuilder.CreatePart(type, name, parent, Vector3.zero, scale, material,
                keepCollider: false);
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
