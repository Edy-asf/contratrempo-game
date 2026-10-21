using UnityEngine;

namespace Icaro
{
    // Copia as propriedades do céu da cena (RenderSettings.skybox, shader Icaro/NightSky) para o material do mar
    // (Icaro/Sea) a cada frame, num MaterialPropertyBlock: o reflexo mostra o mesmo céu, inclusive nas transições de
    // bioma (o AtmosphereCorridor muda o céu ao longo do desfiladeiro).
    [RequireComponent(typeof(Renderer))]
    public class SeaSkySync : MonoBehaviour
    {
        static readonly string[] k_Colors =
        {
            "_TopColor", "_HorizonColor", "_GlowColor", "_PlanetColorA", "_PlanetColorB", "_RingColor", "_HorizonTint",
            "_MidColor", "_NebulaColor", "_AuroraColorA", "_AuroraColorB",
        };

        static readonly string[] k_Floats =
        {
            "_StarDensity", "_StarBrightness", "_PlanetSize", "_HorizonFog", "_GradientBlue", "_GradientTop",
            "_AuroraStrength", "_AuroraHeight",
        };

        static int[] s_ColorIds, s_FloatIds;
        static readonly int k_PlanetDir = Shader.PropertyToID("_PlanetDir");

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
            if (s_ColorIds == null)
            {
                s_ColorIds = System.Array.ConvertAll(k_Colors, Shader.PropertyToID);
                s_FloatIds = System.Array.ConvertAll(k_Floats, Shader.PropertyToID);
            }
        }

        void LateUpdate()
        {
            Material sky = RenderSettings.skybox;
            if (!sky)
                return;
            m_Renderer.GetPropertyBlock(m_Block);
            foreach (int id in s_ColorIds)
            {
                if (sky.HasProperty(id))
                    m_Block.SetColor(id, sky.GetColor(id));
            }

            foreach (int id in s_FloatIds)
            {
                if (sky.HasProperty(id))
                    m_Block.SetFloat(id, sky.GetFloat(id));
            }

            if (sky.HasProperty(k_PlanetDir))
                m_Block.SetVector(k_PlanetDir, sky.GetVector(k_PlanetDir));
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}
