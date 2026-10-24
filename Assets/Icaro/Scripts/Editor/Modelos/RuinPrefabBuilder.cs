using UnityEditor;
using UnityEngine;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;
using PropKind = Icaro.EditorTools.ScenePropKit.PropKind;

namespace Icaro.EditorTools
{
    // Peças modulares das Ruínas Aeônidas (Models/Ruinas/*.fbx) no lugar dos placeholders de ruína de cada bioma.
    // Pedra fria com runas ciano; nos outros biomas a pedra e as runas seguem a paleta (na Cratera, basalto escuro).
    // Collider: MeshCollider com o LOD1 (o vão do arco fica livre). A escadaria ganha uma rampa invisível por cima
    // dos degraus, para o jogador e o NavMesh subirem sem tropeçar.
    public static class RuinPrefabBuilder
    {
        const string k_Group = "Ruinas";

        static readonly (PropKind kind, string model)[] k_Pieces =
        {
            (PropKind.RuinaMuro, "Ruina_Muro"),
            (PropKind.RuinaColuna, "Ruina_Pilar"),
            (PropKind.RuinaPlataforma, "Ruina_Escadaria"),
            (PropKind.RuinaArco, "Ruina_Arco"),
            (PropKind.RuinaBloco, "Ruina_Bloco_Runas"),
        };

        static readonly float[] k_LodHeights = { 0.12f, 0.015f };

        // Escadaria: 7 degraus de 0,6 m entre a borda da plataforma (z = -3, 2 m de altura) e o chão (z = -7,2)
        const float k_StairTopZ = -3f;
        const float k_StairBottomZ = -7.2f;
        const float k_StairRise = 2f;
        const float k_StairWidth = 3f;

        public static bool Build()
        {
            bool any = false;
            foreach ((PropKind kind, string model) in k_Pieces)
            {
                if (!ModelLibrary.LoadModel(k_Group, model))
                    continue;

                ModelLibrary.BindMaterials(k_Group, model);
                foreach (BiomeId biome in System.Enum.GetValues(typeof(BiomeId)))
                    BuildPrefab(kind, model, biome);
                any = true;
            }

            return any;
        }

        static void BuildPrefab(PropKind kind, string model, BiomeId biome)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(ModelLibrary.LoadModel(k_Group, model));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = ScenePropKit.FileName(kind);

            BiomePalettes.Palette palette = ViaLayout.PaletteOf(biome);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i])
                        materials[i] = BiomeMaterial(materials[i], biome, palette);
                renderer.sharedMaterials = materials;
            }

            var lods = new LOD[k_LodHeights.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                Transform part = root.transform.Find($"{model}_LOD{i}");
                lods[i] = new LOD(k_LodHeights[i], part ? part.GetComponents<Renderer>() : new Renderer[0]);
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (!lodGroup)
                lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            Transform collisionSource = root.transform.Find($"{model}_LOD1") ?? root.transform.Find($"{model}_LOD0");
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = collisionSource.GetComponent<MeshFilter>().sharedMesh;

            if (kind == PropKind.RuinaPlataforma)
                AddStairRamp(root.transform);

            IcaroAssetUtility.EnsureFolder($"{ScenePropKit.Folder}/{biome}");
            IcaroPrefabBuilder.SavePrefab(root, ScenePropKit.PathOf(biome, kind));
        }

        static void AddStairRamp(Transform root)
        {
            float run = k_StairTopZ - k_StairBottomZ;
            float angle = Mathf.Atan2(k_StairRise, run) * Mathf.Rad2Deg;
            const float thickness = 0.2f;

            var ramp = new GameObject("RampaEscada").transform;
            ramp.SetParent(root, false);
            ramp.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            // Face de cima da caixa passando pela quina dos degraus
            ramp.localPosition = new Vector3(0f, k_StairRise * 0.5f, (k_StairTopZ + k_StairBottomZ) * 0.5f)
                                 - ramp.up * (thickness * 0.5f);
            var box = ramp.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(k_StairWidth, thickness, Mathf.Sqrt(run * run + k_StairRise * k_StairRise));
        }

        static Material BiomeMaterial(Material source, BiomeId biome, BiomePalettes.Palette palette)
        {
            string suffix = biome.ToString();
            if (ModelLibrary.IsGlow(source.name))
            {
                Color rune = biome == BiomeId.Cratera ? palette.AccentAmber : palette.AccentCyan;
                return ModelLibrary.GetOrCreateVariant(source, k_Group, suffix, Color.white, rune * 1.6f);
            }

            // A textura já é a pedra cinza-azulada das Ruínas; os outros biomas puxam para a cor de pedra deles
            // (1,25: a textura sozinha fica escura demais sob a luz baixa e a névoa da Via)
            Color reference = BiomePalettes.Ruinas.Stone;
            Color tint = new Color(palette.Stone.r / reference.r, palette.Stone.g / reference.g, palette.Stone.b / reference.b) * 1.25f;
            if (biome == BiomeId.Cratera)
                tint *= 0.7f;
            Material stone = ModelLibrary.GetOrCreateVariant(source, k_Group, suffix, tint, Color.black);
            stone.SetFloat("_Smoothness", 0.15f + palette.Wetness * 0.4f);
            return stone;
        }
    }
}
