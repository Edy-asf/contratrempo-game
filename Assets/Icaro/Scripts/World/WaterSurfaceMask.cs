using UnityEngine;

namespace Icaro
{
    // Máscara do mar livre (gerada com o Arquipélago, na raiz do bioma): cada pixel (CellSize metros) marca se há
    // terra ou estrutura acima da água ali (ilhas, pontes, ruínas, inclusive as sem colisão, destroços). As partículas
    // de brilho (SporeField) só nascem nos pixels livres. As coordenadas são relativas à raiz do bioma, que na Via fica
    // deslocada.
    public class WaterSurfaceMask : MonoBehaviour
    {
        [Tooltip("Imagem da máscara (branco = terra ou estrutura); precisa estar com Read/Write ligado")]
        public Texture2D Mask;

        [Tooltip("Canto (x, z) do pixel (0, 0), em relação à raiz do bioma (m)")]
        public Vector2 Origin;

        [Tooltip("Tamanho de cada pixel (m)")]
        public float CellSize = 2f;

        Color32[] m_Pixels;
        int m_Width, m_Height;

        void Awake()
        {
            if (Mask && Mask.isReadable)
            {
                m_Pixels = Mask.GetPixels32();
                m_Width = Mask.width;
                m_Height = Mask.height;
            }
        }

        // Há terra ou estrutura acima da água neste ponto? Fora da máscara, mar livre.
        public bool IsBlocked(Vector3 world)
        {
            if (m_Pixels == null)
                return false;
            Vector3 local = world - transform.position;
            int x = Mathf.FloorToInt((local.x - Origin.x) / CellSize);
            int z = Mathf.FloorToInt((local.z - Origin.y) / CellSize);
            if (x < 0 || z < 0 || x >= m_Width || z >= m_Height)
                return false;
            return m_Pixels[z * m_Width + x].r > 127;
        }
    }
}
