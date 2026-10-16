using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;

namespace Icaro.EditorTools
{
    // Árvores alienígenas (Models/Arvores/Arvore_A..C.fbx) viram os protótipos de árvore do Terrain de cada bioma:
    // Prefabs/Cenario/{Bioma}/Arvore.prefab (A), Arvore_B.prefab e Arvore_C.prefab. O Terrain exige o LODGroup e o
    // CapsuleCollider na raiz. Cada bioma recebe sua variação de material (folhagem, casca, veios e fungos).
    public static class TreePrefabBuilder
    {
        const string k_Group = "Arvores";
        const float k_ColliderHeight = 6f;

        // Altura relativa na tela em que cada LOD deixa de valer (árvores de ~11 m; o Terrain corta em 120 m)
        static readonly float[] k_LodHeights = { 0.25f, 0.09f, 0.012f };

        struct TreeSpec
        {
            public string Model;
            public string PrefabName;
            public Vector3 TrunkCenter;
            public float TrunkRadius;
        }

        // Centro e raio do tronco medidos no Blender, já no eixo do Unity (x e z com sinal trocado)
        static readonly TreeSpec[] k_Trees =
        {
            new TreeSpec { Model = "Arvore_A", PrefabName = "Arvore", TrunkCenter = new Vector3(0.43f, 0f, -0.48f), TrunkRadius = 0.5f },
            new TreeSpec { Model = "Arvore_B", PrefabName = "Arvore_B", TrunkCenter = new Vector3(-0.15f, 0f, -0.32f), TrunkRadius = 0.45f },
            new TreeSpec { Model = "Arvore_C", PrefabName = "Arvore_C", TrunkCenter = new Vector3(-0.1f, 0f, -0.08f), TrunkRadius = 0.25f },
        };

        struct BiomeLook
        {
            public Color Foliage;
            public Color Bark;
            public float VeinIntensity;
            public bool AmberVeins;
            public bool CyanFungi;
        }

        // Bosque: verde-azulado e âmbar. Charco: verde escuro. Ruínas: frio, fungos ciano. Cratera: queimado, veios âmbar.
        static BiomeLook LookOf(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Charco:
                    return new BiomeLook { Foliage = new Color(0.62f, 0.85f, 0.6f), Bark = new Color(0.85f, 0.9f, 0.82f), VeinIntensity = 1.2f };
                case BiomeId.Ruinas:
                    return new BiomeLook { Foliage = new Color(0.8f, 0.9f, 1f), Bark = new Color(0.9f, 0.95f, 1f), VeinIntensity = 1.8f, CyanFungi = true };
                case BiomeId.Cratera:
                    return new BiomeLook { Foliage = new Color(1f, 0.55f, 0.35f), Bark = new Color(0.55f, 0.45f, 0.4f), VeinIntensity = 2f, AmberVeins = true };
                default:
                    return new BiomeLook { Foliage = Color.white, Bark = Color.white, VeinIntensity = 1.5f };
            }
        }

        public static bool Build()
        {
            bool any = false;
            foreach (TreeSpec tree in k_Trees)
            {
                if (!ModelLibrary.LoadModel(k_Group, tree.Model))
                    continue;

                ModelLibrary.BindMaterials(k_Group, tree.Model);
                foreach (BiomeId biome in System.Enum.GetValues(typeof(BiomeId)))
                    BuildPrefab(tree, biome);
                any = true;
            }

            return any;
        }

        static void BuildPrefab(TreeSpec tree, BiomeId biome)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(ModelLibrary.LoadModel(k_Group, tree.Model));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = tree.PrefabName;

            BiomePalettes.Palette palette = ViaLayout.PaletteOf(biome);
            BiomeLook look = LookOf(biome);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i])
                        materials[i] = BiomeMaterial(materials[i], biome, palette, look);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            var lods = new LOD[k_LodHeights.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                Transform part = root.transform.Find($"{tree.Model}_LOD{i}");
                Renderer[] renderers = part ? part.GetComponents<Renderer>() : new Renderer[0];
                lods[i] = new LOD(k_LodHeights[i], renderers);
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (!lodGroup)
                lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(lods);
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.RecalculateBounds();

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = tree.TrunkRadius;
            capsule.height = k_ColliderHeight;
            capsule.center = tree.TrunkCenter + Vector3.up * (k_ColliderHeight * 0.5f);

            IcaroAssetUtility.EnsureFolder($"{ScenePropKit.Folder}/{biome}");
            IcaroPrefabBuilder.SavePrefab(root, $"{ScenePropKit.Folder}/{biome}/{tree.PrefabName}.prefab");
        }

        static Material BiomeMaterial(Material source, BiomeId biome, BiomePalettes.Palette palette, BiomeLook look)
        {
            string suffix = biome.ToString();
            if (ModelLibrary.IsGlow(source.name))
            {
                Color glow = look.CyanFungi ? palette.AccentCyan : palette.AccentAmber;
                return ModelLibrary.GetOrCreateVariant(source, k_Group, suffix, Color.white, glow * 0.8f);
            }

            if (ModelLibrary.IsFoliage(source.name))
                return ModelLibrary.GetOrCreateVariant(source, k_Group, suffix, look.Foliage, Color.black);

            // Casca: a textura de emissão já traz os veios em ciano; na Cratera a cor âmbar puxa para o quente
            Color veins = (look.AmberVeins ? palette.AccentAmber : Color.white) * look.VeinIntensity;
            Material bark = ModelLibrary.GetOrCreateVariant(source, k_Group, suffix, look.Bark, veins);
            bark.SetFloat("_Smoothness", 0.25f + palette.Wetness * 0.2f);
            return bark;
        }
    }
}
