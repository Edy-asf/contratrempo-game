using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Materiais do domo da arena (ArenaDome) em Resources/Domo, para o shader Icaro/ArenaDome e o das partículas
    // entrarem no build (o domo é montado em jogo e carrega os materiais por nome). Chamado pelo "Gerar prefabs".
    public static class ArenaDomeBuilder
    {
        const string k_Folder = IcaroAssetUtility.Root + "/Resources/Domo";

        public static void Build()
        {
            IcaroAssetUtility.EnsureFolder(k_Folder);
            GetOrCreate("Domo_Arena", "Icaro/ArenaDome");

            Material shards = GetOrCreate("Domo_Estilhacos", "Universal Render Pipeline/Particles/Unlit");
            if (shards)
            {
                // Aditivo, a cor vem das partículas
                shards.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));
                shards.SetFloat("_Surface", 1f);
                shards.SetFloat("_Blend", 2f);
                shards.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                shards.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                shards.SetFloat("_ZWrite", 0f);
                shards.SetFloat("_Cull", 0f);
                shards.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                shards.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(shards);
            }
        }

        static Material GetOrCreate(string name, string shaderName)
        {
            string path = $"{k_Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;
            Shader shader = Shader.Find(shaderName);
            if (!shader)
            {
                Debug.LogError($"[Ícaro] Shader {shaderName} não encontrado para o domo da arena.");
                return null;
            }

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
