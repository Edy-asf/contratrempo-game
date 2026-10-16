using System.Collections.Generic;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Mira do Ícaro: um ponto central e dois colchetes [ ] cuja área interna é a zona da assistência de mira
    // (AimAssistSettings.ZoneRadius x intensidade, em fração da altura da tela, então colchetes e detecção nunca
    // ficam diferentes). Com um alvo válido na zona, os colchetes se aproximam e ficam âmbar, e um losango discreto
    // marca o inimigo escolhido. Depuração (AimAssistSettings.DebugView ou a tecla): desenha o círculo da zona e
    // marca o alvo em vermelho. Esconde a mira por sprite do Microgame (CrosshairManager), sem editá-lo.
    public class AimAssistHUD : MonoBehaviour
    {
        public AimAssist Assist;

        public Color IdleColor = new Color(0.85f, 0.95f, 1f, 0.85f);
        public Color TargetColor = new Color(1f, 0.72f, 0.2f, 1f);
        public Color DebugColor = new Color(1f, 0.25f, 0.2f, 0.9f);

        [Tooltip("Quanto os colchetes se aproximam com alvo (1 = não se mexem)")]
        public float TargetSqueeze = 0.88f;

        const float k_Thickness = 2.5f;
        const float k_TickLength = 9f;
        const int k_DebugDots = 32;

        Canvas m_Canvas;
        RectTransform m_Left;
        RectTransform m_Right;
        readonly List<Image> m_BracketParts = new List<Image>();
        Image m_Dot;
        RectTransform m_Marker;
        Image m_MarkerImage;
        readonly List<RectTransform> m_DebugRing = new List<RectTransform>();
        GameObject m_HiddenCrosshair;
        float m_Squeeze = 1f;
        Color m_Color;

        void Start()
        {
            m_Canvas = ItemUI.CreateCanvas(transform, "Mira", 40);
            Transform root = m_Canvas.transform;
            var center = new Vector2(0.5f, 0.5f);

            m_Left = Bracket(root, -1f);
            m_Right = Bracket(root, 1f);
            m_Dot = ItemUI.Panel("Ponto", root, center, Vector2.zero, Vector2.one * 4f, IdleColor);

            m_MarkerImage = ItemUI.Panel("Alvo", root, new Vector2(0f, 0f), Vector2.zero, Vector2.one * 12f, TargetColor);
            m_Marker = m_MarkerImage.rectTransform;
            m_Marker.pivot = new Vector2(0.5f, 0.5f);
            m_Marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m_Marker.gameObject.SetActive(false);

            for (int i = 0; i < k_DebugDots; i++)
            {
                Image dot = ItemUI.Panel("Zona", root, center, Vector2.zero, Vector2.one * 3f, DebugColor);
                m_DebugRing.Add(dot.rectTransform);
                dot.gameObject.SetActive(false);
            }

            m_Color = IdleColor;
            HideMicrogameCrosshair();
        }

        // Colchete: barra vertical com duas pontas para dentro (side -1 = "[", +1 = "]")
        RectTransform Bracket(Transform parent, float side)
        {
            RectTransform bracket = ItemUI.Rect(side < 0f ? "Colchete_E" : "Colchete_D", parent, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.one);
            m_BracketParts.Add(ItemUI.Panel("Barra", bracket, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(k_Thickness, 1f), IdleColor));
            m_BracketParts.Add(ItemUI.Panel("Topo", bracket, new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(k_TickLength, k_Thickness), IdleColor));
            m_BracketParts.Add(ItemUI.Panel("Base", bracket, new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(k_TickLength, k_Thickness), IdleColor));
            return bracket;
        }

        void HideMicrogameCrosshair()
        {
            var crosshair = FindAnyObjectByType<CrosshairManager>();
            if (crosshair && crosshair.CrosshairImage)
            {
                m_HiddenCrosshair = crosshair.CrosshairImage.gameObject;
                m_HiddenCrosshair.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (m_HiddenCrosshair)
                m_HiddenCrosshair.SetActive(true);
        }

        void LateUpdate()
        {
            if (!Assist || !Assist.Settings)
                return;

            AimAssistSettings settings = Assist.Settings;
            // Raio em unidades do canvas: fração da altura da tela convertida pelo fator de escala do canvas
            float zone = settings.IsActive ? settings.EffectiveZoneRadius : settings.ZoneRadius;
            float radius = zone * Screen.height / Mathf.Max(0.01f, m_Canvas.scaleFactor);

            bool hasTarget = Assist.PreviewTarget;
            float dt = Time.unscaledDeltaTime;
            m_Squeeze = Mathf.MoveTowards(m_Squeeze, hasTarget ? TargetSqueeze : 1f, dt * 2.5f);
            m_Color = Color.Lerp(m_Color, hasTarget ? TargetColor : IdleColor, 1f - Mathf.Exp(-14f * dt));

            float x = radius * m_Squeeze;
            float height = radius * 2f * m_Squeeze;
            PlaceBracket(m_Left, -x, height, -1f);
            PlaceBracket(m_Right, x, height, 1f);
            foreach (Image part in m_BracketParts)
                part.color = m_Color;
            m_Dot.color = m_Color;

            UpdateMarker(hasTarget, settings.DebugView);
            UpdateDebugRing(settings.DebugView, radius);
        }

        void PlaceBracket(RectTransform bracket, float x, float height, float side)
        {
            bracket.anchoredPosition = new Vector2(x, 0f);
            bracket.sizeDelta = new Vector2(k_TickLength * 2f, height);
            // As pontas apontam para dentro da zona
            for (int i = 0; i < bracket.childCount; i++)
            {
                var part = (RectTransform)bracket.GetChild(i);
                if (part.name == "Barra")
                    part.sizeDelta = new Vector2(k_Thickness, height);
                else
                    part.anchoredPosition = new Vector2(-side * k_TickLength * 0.5f, 0f);
            }
        }

        // Losango sobre o ponto de mira do alvo (vermelho na depuração)
        void UpdateMarker(bool hasTarget, bool debug)
        {
            Camera camera = Assist.Camera;
            bool show = hasTarget && camera;
            if (show)
            {
                Vector3 screen = camera.WorldToScreenPoint(Assist.AimPointOf(Assist.PreviewTarget));
                show = screen.z > 0f;
                m_Marker.anchoredPosition = new Vector2(screen.x, screen.y) / Mathf.Max(0.01f, m_Canvas.scaleFactor);
                m_Marker.sizeDelta = Vector2.one * (debug ? 16f : 10f);
                Color color = debug ? DebugColor : TargetColor;
                color.a *= debug ? 1f : 0.7f;
                m_MarkerImage.color = color;
            }

            if (m_Marker.gameObject.activeSelf != show)
                m_Marker.gameObject.SetActive(show);
        }

        void UpdateDebugRing(bool debug, float radius)
        {
            for (int i = 0; i < m_DebugRing.Count; i++)
            {
                RectTransform dot = m_DebugRing[i];
                if (dot.gameObject.activeSelf != debug)
                    dot.gameObject.SetActive(debug);
                if (!debug)
                    continue;
                float angle = i * Mathf.PI * 2f / m_DebugRing.Count;
                dot.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }
    }
}
