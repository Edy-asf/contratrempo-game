using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Ajustes de importação dos modelos de Assets/Icaro/Models/{Grupo} e das texturas de Textures/{Grupo}
    // (só os grupos de ModelLibrary.Groups; as texturas geradas do Terreno ficam de fora)
    public class ModelImportSettings : AssetPostprocessor
    {
        const int k_MaxTextureSize = 2048;

        void OnPreprocessModel()
        {
            string group = ModelLibrary.GroupOf(assetPath);
            if (group == null || !assetPath.StartsWith(ModelLibrary.ModelsFolder + "/"))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.generateSecondaryUV = ModelLibrary.IsLightmapped(group);
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
        }

        void OnPreprocessTexture()
        {
            if (ModelLibrary.GroupOf(assetPath) == null || !assetPath.StartsWith(ModelLibrary.TexturesFolder + "/"))
                return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.maxTextureSize = k_MaxTextureSize;
            importer.mipmapEnabled = true;

            if (file.EndsWith("_Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (file.EndsWith("_BaseMap"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                bool foliage = ModelLibrary.IsFoliage(file);
                importer.alphaSource = foliage ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = foliage;
                // Mantém a cobertura das folhas nos mips distantes (senão a copa "some" ao longe)
                importer.mipMapsPreserveCoverage = foliage;
                importer.alphaTestReferenceValue = 0.4f;
            }
            else if (file.EndsWith("_MetallicSmoothness"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            else if (file.EndsWith("_Emission"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
            }
        }
    }
}
