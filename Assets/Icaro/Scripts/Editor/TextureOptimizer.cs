using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Otimizar texturas": deixa as texturas do Ícaro mais leves no jogo exportado. Limita o tamanho a
    // MaxSize (1024 px) e liga a compressão Crunch (qualidade 50), que encolhe bastante o build com pouca perda
    // visível. O céu (HDRI) fica de fora, para não perder a faixa de luz. Os arquivos originais não mudam: só a
    // importação; para voltar, é só mudar o Max Size e desligar o Crunch no Inspector da textura.
    public static class TextureOptimizer
    {
        const string k_Folder = "Assets/Icaro/Textures";
        const int k_MaxSize = 1024;
        const int k_CrunchQuality = 50;

        [MenuItem("Ícaro/Otimizar texturas", priority = 71)]
        static void Optimize()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { k_Folder });
            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Otimizando texturas", path, (float)i / guids.Length);
                    if (path.Contains("/HDRI/") || !(AssetImporter.GetAtPath(path) is TextureImporter importer))
                        continue;

                    bool dirty = false;
                    if (importer.maxTextureSize > k_MaxSize)
                    {
                        importer.maxTextureSize = k_MaxSize;
                        dirty = true;
                    }
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Compressed;
                        dirty = true;
                    }
                    if (!importer.crunchedCompression)
                    {
                        importer.crunchedCompression = true;
                        importer.compressionQuality = k_CrunchQuality;
                        dirty = true;
                    }

                    if (dirty)
                    {
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[Ícaro] Texturas otimizadas: {changed} de {guids.Length} (máx. {k_MaxSize} px, Crunch " +
                      $"{k_CrunchQuality}). O céu (HDRI) ficou como estava.");
        }
    }
}
