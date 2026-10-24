using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icaro.EditorTools
{
    // Modelos importados (FBX exportados do Blender; os .blend de origem ficam em ArteFonte/, fora do git).
    // Convenções:
    //  - Assets/Icaro/Models/{Grupo}/{Modelo}.fbx, com os LODs como filhos {Modelo}_LOD0, {Modelo}_LOD1...
    //  - O material "X" do FBX vira Assets/Icaro/Materials/{Grupo}/X.mat (URP/Lit), com as texturas opcionais
    //    Assets/Icaro/Textures/{Grupo}/X_BaseMap.png, X_Normal.png e X_Emission.png.
    //  - Material com "Folhas" no nome: recorte por alfa e duas faces. Com "Brilho" ou "Fungo" no nome: só
    //    emissão (cor sólida), tingida por bioma quando o modelo tem variações por bioma.
    public static class ModelLibrary
    {
        public const string ModelsFolder = IcaroAssetUtility.Root + "/Models";
        public const string TexturesFolder = IcaroAssetUtility.Root + "/Textures";

        // Grupos de modelos (pasta em Models, Textures e Materials)
        public static readonly string[] Groups = { "Arvores", "Ruinas", "Dedalo", "Agulha", "Golem", "Chefes" };

        // Grupos de peças estáticas do cenário, que precisam de UV de lightmap
        public static bool IsLightmapped(string group) => group == "Ruinas" || group == "Dedalo";

        const float k_Smoothness = 0.35f;
        const float k_FoliageCutoff = 0.4f;

        public static string ModelPath(string group, string model) => $"{ModelsFolder}/{group}/{model}.fbx";

        public static GameObject LoadModel(string group, string model) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(group, model));

        public static bool IsFoliage(string materialName) => materialName.Contains("Folhas");
        public static bool IsGlow(string materialName) =>
            materialName.Contains("Brilho") || materialName.Contains("Fungo");

        // Cria/atualiza os materiais usados pelo FBX e liga cada um ao slot de mesmo nome (remap do importador)
        public static void BindMaterials(string group, string model)
        {
            string path = ModelPath(group, model);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!importer || !prefab)
                return;

            var names = new HashSet<string>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material)
                    names.Add(material.name);

            Dictionary<AssetImporter.SourceAssetIdentifier, Object> current = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (string name in names)
            {
                Material material = GetOrCreateMaterial(group, name);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (current.TryGetValue(id, out Object mapped) && mapped == material)
                    continue;

                importer.AddRemap(id, material);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        public static Material GetOrCreateMaterial(string group, string name)
        {
            string folder = $"{IcaroAssetUtility.MaterialsFolder}/{group}";
            IcaroAssetUtility.EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            string texturePrefix = $"{TexturesFolder}/{group}/{name}";
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePrefix + "_BaseMap.png");
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePrefix + "_Normal.png");
            var emissionMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePrefix + "_Emission.png");
            var metallicMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePrefix + "_MetallicSmoothness.png");

            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);

            // Metálico no R e suavidade no A; com o mapa, _Smoothness vira multiplicador
            material.SetTexture("_MetallicGlossMap", metallicMap);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallicMap);
            material.SetFloat("_Smoothness", metallicMap ? 1f : k_Smoothness);

            material.SetTexture("_BumpMap", normalMap);
            SetKeyword(material, "_NORMALMAP", normalMap);

            if (IsGlow(name))
            {
                material.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));
                SetEmission(material, emissionMap, Color.white * 2f);
            }
            else
            {
                SetEmission(material, emissionMap, emissionMap ? Color.white : Color.black);
            }

            SetAlphaClip(material, IsFoliage(name));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Cópia de um material com outra cor base e outra emissão (variações por bioma)
        public static Material GetOrCreateVariant(Material source, string group, string suffix, Color tint,
            Color emission)
        {
            string folder = $"{IcaroAssetUtility.MaterialsFolder}/{group}";
            string path = $"{folder}/{source.name}_{suffix}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(source) { name = $"{source.name}_{suffix}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            material.shaderKeywords = source.shaderKeywords;
            material.renderQueue = source.renderQueue;
            material.SetColor("_BaseColor", source.GetColor("_BaseColor") * tint);
            SetEmission(material, source.GetTexture("_EmissionMap"), emission);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetEmission(Material material, Texture map, Color color)
        {
            material.SetTexture("_EmissionMap", map);
            material.SetColor("_EmissionColor", color);
            bool on = color.maxColorComponent > 0f;
            SetKeyword(material, "_EMISSION", on);
            material.globalIlluminationFlags = on
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        static void SetAlphaClip(Material material, bool clip)
        {
            material.SetFloat("_AlphaClip", clip ? 1f : 0f);
            material.SetFloat("_Cutoff", k_FoliageCutoff);
            SetKeyword(material, "_ALPHATEST_ON", clip);
            material.SetFloat("_Cull", clip ? (float)CullMode.Off : (float)CullMode.Back);
            material.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
        }

        static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        // Grupo a partir do caminho de um asset dentro de Models/{Grupo}/ ou Textures/{Grupo}/
        public static string GroupOf(string assetPath)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(assetPath));
            return System.Array.IndexOf(Groups, folder) >= 0 ? folder : null;
        }
    }
}
