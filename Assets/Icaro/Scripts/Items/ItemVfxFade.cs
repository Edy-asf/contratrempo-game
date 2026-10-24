using UnityEngine;

namespace Icaro
{
    // Apaga um visual de item: a cor some e a escala cresce até 'size' (explosão) em 'duration' segundos; depois
    // o objeto é destruído. Funciona com LineRenderer (cor por vértice) ou MeshRenderer (_BaseColor por bloco).
    public class ItemVfxFade : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        float m_Duration;
        float m_Age;
        float m_Size;
        Color m_Color;
        LineRenderer m_Line;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        public void Begin(float duration, Color color, float size)
        {
            m_Duration = Mathf.Max(0.01f, duration);
            m_Color = color;
            m_Size = size;
            m_Line = GetComponent<LineRenderer>();
            m_Renderer = m_Line ? null : GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
            Apply(0f);
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            float t = m_Age / m_Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Apply(t);
        }

        void Apply(float t)
        {
            Color color = m_Color * (2f * (1f - t));
            color.a = 1f - t;
            if (m_Line)
            {
                m_Line.startColor = m_Line.endColor = color;
                return;
            }

            transform.localScale = Vector3.one * (m_Size * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(t)));
            if (m_Renderer)
            {
                m_Block.SetColor(k_BaseColor, color);
                m_Renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
