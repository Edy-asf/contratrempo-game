using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Icaro.EditorTools
{
    // Menu "Ícaro > Configurar gráficos": prepara tudo o que é do projeto (não da cena) para o visual do Ícaro.
    // - Pipeline URP próprio (cópia do Default_PipelineAsset do Microgame, que fica intacto) com sombras e SSAO
    //   ajustados para um PC médio, e Graphics/Quality apontando para ele.
    // - Quality Settings.
    // - Import do HDRI e material do céu.
    // - Perfil do Volume global (ACES, Bloom, Color Adjustments, Vignette) e LightingSettings do bake.
    // O "Gerar Via Aeônida" chama isto automaticamente.
    public static class VisualSettingsBuilder
    {
        public const string SettingsFolder = IcaroAssetUtility.Root + "/Settings";

        const string k_SourcePipeline = "Assets/Rendering/Default_PipelineAsset.asset";
        const string k_SourceRenderer = "Assets/Rendering/Default_PipelineAsset_ForwardRenderer.asset";
        const string k_PipelinePath = SettingsFolder + "/Icaro_PipelineAsset.asset";
        const string k_RendererPath = SettingsFolder + "/Icaro_Renderer.asset";
        const string k_VolumeProfilePath = SettingsFolder + "/ViaAeonida_Volume.asset";
        const string k_LightingSettingsPath = SettingsFolder + "/ViaAeonida.lighting";
        const string k_HdriPath = IcaroAssetUtility.Root + "/Textures/HDRI/qwantani_dusk_2_puresky_2k.hdr";
        const string k_SkyMaterialPath = IcaroAssetUtility.MaterialsFolder + "/Ceu_Crepusculo.mat";

        public class Result
        {
            public Material Sky;
            public VolumeProfile Volume;
            public LightingSettings Lighting;
        }

        [MenuItem("Ícaro/Configurar gráficos", priority = 60)]
        static void ApplyFromMenu()
        {
            Apply(BiomePalettes.Bosque);
            Debug.Log("[Ícaro] Gráficos configurados: pipeline do Ícaro, SSAO, Quality, céu, Volume e iluminação.");
        }

        public static Result Apply(BiomePalettes.Palette palette)
        {
            IcaroAssetUtility.EnsureFolder(SettingsFolder);

            ConfigurePipeline();
            ConfigureQuality();

            var result = new Result
            {
                Sky = BuildSkyMaterial(palette),
                Volume = GetOrCreateVolumeProfile(),
                Lighting = GetOrCreateLightingSettings(),
            };

            AssetDatabase.SaveAssets();
            return result;
        }

        // ---------- Pipeline e SSAO ----------

        static void ConfigurePipeline()
        {
            if (!AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(k_RendererPath))
                AssetDatabase.CopyAsset(k_SourceRenderer, k_RendererPath);
            if (!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_PipelinePath))
                AssetDatabase.CopyAsset(k_SourcePipeline, k_PipelinePath);

            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(k_RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_PipelinePath);
            if (!renderer || !pipeline)
            {
                Debug.LogError("[Ícaro] Não foi possível criar a cópia do pipeline URP.");
                return;
            }

            var so = new SerializedObject(pipeline);
            SerializedProperty rendererList = so.FindProperty("m_RendererDataList");
            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetInt(so, "m_DefaultRendererIndex", 0);

            // Sombras: a névoa esconde o que está longe, então 40 m bastam; 2 cascatas deixam o perto nítido
            SetFloat(so, "m_ShadowDistance", 40f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetFloat(so, "m_Cascade2Split", 0.25f);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetInt(so, "m_SoftShadowQuality", 1); // Low

            // Imagem: HDR para o Bloom; sem MSAA (a câmera já usa FXAA)
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 1);
            SetFloat(so, "m_RenderScale", 1f);
            SetBool(so, "m_UseSRPBatcher", true);
            SetBool(so, "m_ReflectionProbeBlending", true);
            SetBool(so, "m_ReflectionProbeBoxProjection", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            ConfigureSsao(renderer);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        // O SSAO já existe como renderer feature (vindo do Microgame), mas quase invisível; aqui fica visível e barato
        static void ConfigureSsao(ScriptableRendererData renderer)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(k_RendererPath))
            {
                if (!asset || asset.GetType().Name != "ScreenSpaceAmbientOcclusion")
                    continue;

                var so = new SerializedObject(asset);
                SetBool(so, "m_Active", true);
                SetFloat(so, "m_Settings.Intensity", 0.8f);
                SetFloat(so, "m_Settings.Radius", 0.3f);
                SetBool(so, "m_Settings.Downsample", true);
                SetInt(so, "m_Settings.Samples", 1);     // Medium
                SetInt(so, "m_Settings.BlurQuality", 1); // Medium
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                return;
            }

            Debug.LogWarning("[Ícaro] SSAO não encontrado no renderer " + k_RendererPath +
                             ". Adicione a feature Screen Space Ambient Occlusion nele pelo Inspector.");
        }

        // ---------- Quality ----------

        static void ConfigureQuality()
        {
            QualitySettings.vSyncCount = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.lodBias = 1f;
            QualitySettings.realtimeReflectionProbes = false; // as reflection probes do Ícaro são bakeadas
            QualitySettings.softParticles = false;
        }

        // ---------- Céu ----------

        static Material BuildSkyMaterial(BiomePalettes.Palette palette)
        {
            ConfigureHdriImport();
            var hdri = AssetDatabase.LoadAssetAtPath<Texture>(k_HdriPath);
            if (!hdri)
            {
                Debug.LogError("[Ícaro] HDRI não encontrado em " + k_HdriPath);
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_SkyMaterialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Skybox/Panoramic")) { name = "Ceu_Crepusculo" };
                AssetDatabase.CreateAsset(material, k_SkyMaterialPath);
            }

            material.SetTexture("_MainTex", hdri);
            material.SetColor("_Tint", palette.SkyTint);
            material.SetFloat("_Exposure", palette.SkyExposure);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 1f);   // Latitude-Longitude
            material.SetFloat("_ImageType", 0f); // 360°
            EditorUtility.SetDirty(material);
            return material;
        }

        // Céu panorâmico: sem mipmaps (evitam a costura), compressão HDR de alta qualidade
        static void ConfigureHdriImport()
        {
            if (!(AssetImporter.GetAtPath(k_HdriPath) is TextureImporter importer))
            {
                AssetDatabase.ImportAsset(k_HdriPath);
                importer = AssetImporter.GetAtPath(k_HdriPath) as TextureImporter;
                if (!importer)
                    return;
            }

            if (!importer.mipmapEnabled && importer.wrapModeU == TextureWrapMode.Repeat &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ)
                return;

            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ---------- Volume ----------

        // Cria o perfil se faltar e sempre reaplica os valores abaixo (a calibragem do visual mora aqui)
        static VolumeProfile GetOrCreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(k_VolumeProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, k_VolumeProfilePath);
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(false);

            // O ACES escurece os tons baixos; a exposição compensa para a cena escura continuar legível
            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.6f);
            color.contrast.Override(12f);
            color.saturation.Override(-8f);
            color.colorFilter.Override(new Color(0.92f, 1f, 1f));

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Componente do perfil; os novos viram sub-assets do perfil para serem salvos
        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;

            var component = profile.Add<T>();
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ---------- Iluminação bakeada ----------

        // Bake leve: poucos texels por metro, lightmaps não-direcionais e comprimidos; luz mista em Baked Indirect
        static LightingSettings GetOrCreateLightingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(k_LightingSettingsPath);
            if (!settings)
            {
                settings = new LightingSettings { name = "ViaAeonida" };
                AssetDatabase.CreateAsset(settings, k_LightingSettingsPath);
            }

            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapResolution = 2f; // mapa de ~3 km: 2 texels/m (o terreno ainda usa escala 0,25)
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 2048;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.ao = true;
            settings.aoMaxDistance = 1.5f;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 2;

            // Luz rebatida mais forte: é ela que ilumina cantos e faces longe do sol
            settings.indirectScale = 1.4f;
            settings.albedoBoost = 1.2f;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        // ---------- utilidades de SerializedObject ----------

        static void SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty p = Find(so, path);
            if (p != null) p.floatValue = value;
        }

        static void SetInt(SerializedObject so, string path, int value)
        {
            SerializedProperty p = Find(so, path);
            if (p != null) p.intValue = value;
        }

        static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty p = Find(so, path);
            if (p != null) p.boolValue = value;
        }

        static SerializedProperty Find(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null)
                Debug.LogWarning($"[Ícaro] Propriedade '{path}' não encontrada em {so.targetObject.name}.");
            return p;
        }
    }
}
