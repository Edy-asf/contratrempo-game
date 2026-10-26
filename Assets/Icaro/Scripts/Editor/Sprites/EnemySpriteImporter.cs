using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Sprites dos inimigos: cada pasta em Art/Sprites/Inimigos/<Nome> tem <Nome>.png (a folha) e <Nome>.json (a
    // grade e os clipes; formato no LEIAME.md da pasta). "Ícaro > Sprites > Importar sprites dos inimigos" cria ou
    // atualiza, na mesma pasta, a folha <Nome>_Sprites.asset (EnemySpriteSet) e o material <Nome>_Sprite.mat; o
    // "Gerar prefabs" troca o visual do inimigo pelo sprite quando a folha existe. As imagens da pasta são
    // importadas como pixel art (filtro Point, sem compressão nem mipmaps).
    public class EnemySpriteImporter : AssetPostprocessor
    {
        public const string Root = IcaroAssetUtility.Root + "/Art/Sprites/Inimigos";

        [System.Serializable]
        class Meta
        {
            public string name;
            public int cellWidth, cellHeight, columns, rows;
            public float pixelsPerMeter, pivotX, pivotY, bodyGlow;
            public string emission;
            public ClipMeta[] clips;
        }

        [System.Serializable]
        class ClipMeta
        {
            public string name;
            public int row, frames;
            public float fps;
            public bool loop;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Root + "/"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
        }

        // Folha de sprites de um inimigo, se já foi importada
        public static EnemySpriteSet SetFor(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemySpriteSet>($"{Root}/{name}/{name}_Sprites.asset");

        [MenuItem("Ícaro/Sprites/Importar sprites dos inimigos")]
        public static void ImportAll()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogWarning("[Ícaro] Nenhuma pasta de sprites de inimigos em " + Root);
                return;
            }

            var done = new List<string>();
            foreach (string folder in AssetDatabase.GetSubFolders(Root))
            {
                string name = Path.GetFileName(folder);
                if (Import(folder, name))
                    done.Add(name);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Ícaro] Sprites importados: " + (done.Count > 0 ? string.Join(", ", done) : "nenhum") +
                      ". Rode Ícaro > Gerar prefabs para trocar o visual dos inimigos.");
        }

        static bool Import(string folder, string name)
        {
            string png = $"{folder}/{name}.png", json = $"{folder}/{name}.json";
            if (!File.Exists(png) || !File.Exists(json))
            {
                Debug.LogWarning($"[Ícaro] {folder}: faltam {name}.png e {name}.json");
                return false;
            }

            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(json));
            if (!atlas || meta == null || meta.clips == null || meta.cellWidth <= 0 || meta.cellHeight <= 0)
            {
                Debug.LogError($"[Ícaro] {json}: folha ou descrição inválida");
                return false;
            }

            Texture2D emission = null;
            if (!string.IsNullOrEmpty(meta.emission))
            {
                string emissionPath = $"{folder}/{meta.emission}";
                AssetDatabase.ImportAsset(emissionPath, ImportAssetOptions.ForceUpdate);
                emission = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);
            }

            float bodyGlow = emission && meta.bodyGlow > 0f ? meta.bodyGlow : 1f;
            Material material = SpriteMaterial($"{folder}/{name}_Sprite.mat", atlas, emission, 0.25f / bodyGlow);

            string setPath = $"{folder}/{name}_Sprites.asset";
            var set = AssetDatabase.LoadAssetAtPath<EnemySpriteSet>(setPath);
            if (!set)
            {
                set = ScriptableObject.CreateInstance<EnemySpriteSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }

            set.Atlas = atlas;
            set.Emission = emission;
            set.BodyGlow = bodyGlow;
            set.Material = material;
            set.CellWidth = meta.cellWidth;
            set.CellHeight = meta.cellHeight;
            set.Columns = meta.columns > 0 ? meta.columns : atlas.width / meta.cellWidth;
            set.Rows = meta.rows > 0 ? meta.rows : atlas.height / meta.cellHeight;
            set.PixelsPerMeter = meta.pixelsPerMeter > 0f ? meta.pixelsPerMeter : 32f;
            set.Pivot = new Vector2(meta.pivotX, meta.pivotY);
            set.Clips.Clear();
            foreach (ClipMeta clip in meta.clips)
            {
                set.Clips.Add(new EnemySpriteSet.Clip
                {
                    Name = clip.name, Row = clip.row, Frames = Mathf.Max(1, clip.frames), Fps = clip.fps, Loop = clip.loop,
                });
            }

            EditorUtility.SetDirty(set);
            return true;
        }

        // URP Lit com recorte (sem semitransparência), sem brilho especular nem reflexo (sprites virados para a câmera
        // refletiriam a lua juntos) e com o mapa de brilho (ou a própria folha) na emissão: o brilho mínimo e o piscar
        // vêm do _EmissionColor
        static Material SpriteMaterial(string path, Texture2D atlas, Texture2D emission, float emissionLevel)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", atlas);
            material.SetTexture("_MainTex", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", emission ? emission : atlas);
            material.SetColor("_EmissionColor", Color.white * emissionLevel);
            // O URP só mantém a palavra-chave _EMISSION se o material tiver uma flag de GI emissiva
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
