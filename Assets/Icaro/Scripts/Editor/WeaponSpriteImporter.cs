using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Configura a importação das artes das armas em 1ª pessoa (Assets/Icaro/Resources/Armas/<Arma>/*.png): pixel art
    // nítido (filtro Point), sem compressão nem mipmaps, transparência ligada e tamanho máximo grande. Roda sozinho ao
    // importar ou reimportar a imagem.
    public class WeaponSpriteImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Icaro/Resources/Armas/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
        }
    }
}
