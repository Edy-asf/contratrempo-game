using System;
using UnityEngine;

namespace Icaro
{
    // Troca cada trecho do cenário por versões mais simples conforme ele fica longe da câmera (as colisões continuam nas
    // peças completas). A cada 0,25 s, por trecho: perto, as peças completas; a partir de Distances[i], a versão i + 1
    // (cada uma mais leve que a anterior). A folga (Hysteresis) evita ficar trocando de um lado para o outro na divisa.
    public class ScenicLod : MonoBehaviour
    {
        [Serializable]
        public class Tier
        {
            public Renderer[] Renderers;
        }

        [Serializable]
        public class Cell
        {
            public Bounds Area;
            public Renderer[] Full;
            public Tier[] Tiers;
            [NonSerialized] public int Level;
        }

        [Tooltip("Distância (m) em que cada versão mais leve entra, em ordem crescente")]
        public float[] Distances = { 420f, 900f };

        [Tooltip("Folga para voltar ao detalhe maior (m)")]
        public float Hysteresis = 30f;

        public Cell[] Cells;

        Camera m_Camera;
        float m_Next;

        void Update()
        {
            if (Time.unscaledTime < m_Next)
                return;
            m_Next = Time.unscaledTime + 0.25f;
            if (!m_Camera || !m_Camera.isActiveAndEnabled)
                m_Camera = Camera.main;
            if (!m_Camera || Cells == null)
                return;

            Vector3 eye = m_Camera.transform.position;
            foreach (Cell cell in Cells)
            {
                float distance = Mathf.Sqrt(cell.Area.SqrDistance(eye));
                int level = 0;
                while (level < Distances.Length && level < cell.Tiers.Length && distance >= Distances[level])
                    level++;
                if (level < cell.Level && distance > Distances[cell.Level - 1] - Hysteresis)
                    level = cell.Level;
                if (level == cell.Level)
                    continue;

                Show(cell, cell.Level, false);
                Show(cell, level, true);
                cell.Level = level;
            }
        }

        static void Show(Cell cell, int level, bool visible)
        {
            Renderer[] list = level == 0 ? cell.Full : cell.Tiers[level - 1].Renderers;
            foreach (Renderer target in list)
                if (target)
                    target.enabled = visible;
        }
    }
}
