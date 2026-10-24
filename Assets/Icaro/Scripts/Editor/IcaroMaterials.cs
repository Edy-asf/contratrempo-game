using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Materiais URP Lit dos placeholders, salvos como assets para que prefabs e cenas possam referenciá-los
    public static class IcaroMaterials
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_Smoothness = Shader.PropertyToID("_Smoothness");

        // Cria (ou atualiza) Assets/Icaro/Materials/{name}.mat. A emissão fica sempre ligada, mesmo preta,
        // porque o EnemyController e o Metrônomo mudam _EmissionColor por MaterialPropertyBlock.
        public static Material GetOrCreate(string name, Color color, Color emission = default, float smoothness = 0.3f)
        {
            IcaroAssetUtility.EnsureFolder(IcaroAssetUtility.MaterialsFolder);
            string path = $"{IcaroAssetUtility.MaterialsFolder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(FindLitShader()) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor(k_BaseColor, color);
            material.color = color;
            material.SetFloat(k_Smoothness, smoothness);
            material.EnableKeyword("_EMISSION");
            material.SetColor(k_EmissionColor, emission);

            // Emissão entra no bake da iluminação: objetos estáticos emissivos tingem o cenário em volta
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        // Material de partícula URP Unlit aditivo (brilho que soma, sem escrever profundidade)
        public static Material GetOrCreateAdditiveParticle(string name, Texture texture)
        {
            IcaroAssetUtility.EnsureFolder(IcaroAssetUtility.MaterialsFolder);
            string path = $"{IcaroAssetUtility.MaterialsFolder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = new Material(shader ? shader : Shader.Find("Particles/Standard Unlit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor(k_BaseColor, Color.white);
            material.SetFloat("_Surface", 1f);   // Transparent
            material.SetFloat("_Blend", 2f);     // Additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader ? shader : Shader.Find("Standard");
        }
    }
}
