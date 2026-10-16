using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // X na mira que aparece por um instante a cada acerto do jogador, com um "pop" de escala. Crítico ou abate:
    // maior, vermelho e um pouco mais demorado. Cria sozinho o canvas e os quatro traços do X.
    public class HitMarker : MonoBehaviour
    {
        public Color NormalColor = Color.white;
        public Color StrongColor = new Color(1f, 0.15f, 0.1f);

        [Tooltip("Tempo na tela do acerto normal e do crítico/abate")]
        public float NormalDuration = 0.12f;
        public float StrongDuration = 0.22f;

        [Tooltip("Comprimento, espessura e afastamento do centro de cada traço, em pixels a 1080p")]
        public float StrokeLength = 12f;
        public float StrokeWidth = 3f;
        public float Gap = 7f;

        [Tooltip("Escala do X no crítico/abate e escala inicial do pop")]
        public float StrongScale = 1.5f;
        public float PopScale = 1.6f;

        RectTransform m_Root;
        Image[] m_Strokes;
        float m_Time = 1f;
        float m_Duration = 1f;
        float m_Scale = 1f;
        Color m_Color;

        public void Show(bool strong)
        {
            m_Time = 0f;
            m_Duration = strong ? StrongDuration : NormalDuration;
            m_Scale = strong ? StrongScale : 1f;
            m_Color = strong ? StrongColor : NormalColor;
            m_Root.gameObject.SetActive(true);
        }

        void Awake()
        {
            var canvasObject = new GameObject("HitMarkerCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            m_Root = new GameObject("X", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Root.SetParent(canvasObject.transform, false);
            m_Root.anchorMin = m_Root.anchorMax = new Vector2(0.5f, 0.5f);
            m_Root.sizeDelta = Vector2.zero;

            // Um traço em cada diagonal, afastado do centro para não cobrir a mira
            m_Strokes = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                var stroke = new GameObject("Traco", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                stroke.SetParent(m_Root, false);
                stroke.localRotation = Quaternion.Euler(0f, 0f, angle);
                stroke.pivot = new Vector2(0f, 0.5f);
                stroke.anchoredPosition = Quaternion.Euler(0f, 0f, angle) * Vector3.right * Gap;
                stroke.sizeDelta = new Vector2(StrokeLength, StrokeWidth);
                m_Strokes[i] = stroke.GetComponent<Image>();
                m_Strokes[i].raycastTarget = false;
            }

            m_Root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!m_Root.gameObject.activeSelf)
                return;

            m_Time += Time.unscaledDeltaTime;
            float t = m_Time / m_Duration;
            if (t >= 1f)
            {
                m_Root.gameObject.SetActive(false);
                return;
            }

            // Pop: começa grande e assenta rápido; some no último terço
            float pop = Mathf.Lerp(PopScale, 1f, Mathf.Clamp01(t * 4f));
            m_Root.localScale = Vector3.one * (m_Scale * pop);
            Color color = m_Color;
            color.a = t < 0.66f ? 1f : Mathf.InverseLerp(1f, 0.66f, t);
            foreach (Image stroke in m_Strokes)
                stroke.color = color;
        }
    }
}
