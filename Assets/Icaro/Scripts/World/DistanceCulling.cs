using UnityEngine;

namespace Icaro
{
    // Desliga o desenho do cenário longe da câmera (as colisões continuam): a cada 0,25 s, cada renderizador da lista
    // cujo volume fica a mais de Distance some, e volta ao entrar no raio. A névoa já esconde quase tudo nessa
    // distância. A lista vem do gerador (malhas do cenário divididas em blocos); mar, céu, silhuetas do fundo e o que
    // se move ficam de fora.
    public class DistanceCulling : MonoBehaviour
    {
        [Tooltip("Distância máxima de desenho do cenário (m)")]
        public float Distance = 300f;

        public Renderer[] Renderers;

        Bounds[] m_Bounds;
        Camera m_Camera;
        float m_Next;

        void Start()
        {
            // O volume é guardado no começo (o cenário não se move; desligado, o renderizador não informa o volume)
            m_Bounds = new Bounds[Renderers.Length];
            for (int i = 0; i < Renderers.Length; i++)
            {
                if (Renderers[i])
                    m_Bounds[i] = Renderers[i].bounds;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < m_Next)
                return;
            m_Next = Time.unscaledTime + 0.25f;
            if (!m_Camera || !m_Camera.isActiveAndEnabled)
                m_Camera = Camera.main;
            if (!m_Camera)
                return;

            Vector3 eye = m_Camera.transform.position;
            float limit = Distance * Distance;
            for (int i = 0; i < Renderers.Length; i++)
            {
                Renderer target = Renderers[i];
                if (!target)
                    continue;
                bool show = m_Bounds[i].SqrDistance(eye) <= limit;
                if (target.enabled != show)
                    target.enabled = show;
            }
        }
    }
}
