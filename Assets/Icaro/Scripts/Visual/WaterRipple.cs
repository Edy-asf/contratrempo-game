using UnityEngine;

namespace Icaro
{
    // Faz a superfície da água correr devagar: desloca a textura (e o mapa de brilho das algas) por
    // MaterialPropertyBlock, sem mexer no material. A velocidade segue o multiplicador de tempo de Kairós.
    [RequireComponent(typeof(Renderer))]
    public class WaterRipple : MonoBehaviour
    {
        [Tooltip("Deslocamento da textura por segundo, em repetições (x, z)")]
        public Vector2 Speed = new Vector2(0.03f, 0.015f);

        static readonly int k_BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;
        Vector2 m_Offset;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
        }

        void Update()
        {
            m_Offset += Speed * (Time.deltaTime * Mathf.Max(1f, TimeManager.CurrentMultiplier));
            m_Offset.x = Mathf.Repeat(m_Offset.x, 1f);
            m_Offset.y = Mathf.Repeat(m_Offset.y, 1f);
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_BaseMapST, new Vector4(1f, 1f, m_Offset.x, m_Offset.y));
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}
