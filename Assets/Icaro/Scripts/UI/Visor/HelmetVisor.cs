using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icaro
{
    // Cantos do visor onde os painéis do HUD ficam
    public enum VisorAnchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        BottomCenter,
        TopCenter,
        Center,
    }

    // HUD do visor do capacete: um Canvas em World Space preso à câmera de armas do Microgame (filha da Main Camera,
    // mesma posição; desenha por cima do cenário, então o HUD nunca entra numa parede), como a projeção interna de um
    // visor holográfico. O canvas tem a proporção da tela em pixels de referência (1080 de altura) e é escalado a cada
    // frame para cobrir a vista no campo de visão atual, então o layout não muda com o FOV. Os grupos laterais são
    // girados no eixo vertical em volta da borda da tela (VisorSettings.Curvature), como um visor curvo, e o conjunto
    // todo segue a câmera com atraso (VisorInertia). Os números ficam no VisorSettings. Os painéis pedem o seu grupo
    // com HelmetVisor.TryGroup; o visor é criado na primeira vez, quando o jogador já existe. Também esconde as peças
    // do HUD do Microgame que ele substitui (vida, painel de armas, o aviso "Tab Pause/Options" e a bússola do topo).
    public class HelmetVisor : MonoBehaviour
    {
        [Tooltip("Ordem de desenho do canvas (acima do sprite da arma)")]
        public int SortingOrder = 100;

        public const float ReferenceHeight = 1080f;

        public static HelmetVisor Instance { get; private set; }

        public Camera Camera { get; private set; }
        public Canvas Canvas { get; private set; }

        RectTransform m_Root;
        readonly RectTransform[] m_Groups = new RectTransform[7];
        int m_Layer;
        int m_LastChildCount = -1;
        static readonly System.Collections.Generic.List<Transform> s_Children =
            new System.Collections.Generic.List<Transform>(256);
        float m_NextLayerCheck;
        bool m_MicrogameHidden;

        // Grupo do canto pedido; falso enquanto o jogador (e a câmera de armas) ainda não existe
        public static bool TryGroup(VisorAnchor anchor, out RectTransform group)
        {
            group = null;
            if (!Instance)
            {
                var weapons = FindAnyObjectByType<PlayerWeaponsManager>();
                if (!weapons || !weapons.WeaponCamera)
                    return false;
                new GameObject("VisorHUD").AddComponent<HelmetVisor>().Build(weapons);
            }

            group = Instance.m_Groups[(int)anchor];
            return group;
        }

        // O mouse está em cima deste elemento do visor? (cursor livre: cartas, pausa)
        public static bool MouseOver(RectTransform rect)
        {
            if (Mouse.current == null || !rect || !Instance)
                return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Mouse.current.position.ReadValue(),
                Instance.Camera);
        }

        void Build(PlayerWeaponsManager weapons)
        {
            Instance = this;
            Camera = weapons.WeaponCamera;
            m_Layer = weapons.FpsWeaponLayer.value != 0
                ? Mathf.RoundToInt(Mathf.Log(weapons.FpsWeaponLayer.value, 2))
                : Camera.gameObject.layer;

            // Pivô na câmera (a inércia gira em volta do olho) e o canvas na frente dele
            var pivot = new GameObject("VisorPivo") { layer = m_Layer };
            pivot.transform.SetParent(Camera.transform, false);
            pivot.AddComponent<VisorInertia>().Target = Camera.transform;

            var go = new GameObject("VisorCanvas", typeof(RectTransform), typeof(Canvas)) { layer = m_Layer };
            go.transform.SetParent(pivot.transform, false);
            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.worldCamera = Camera;
            Canvas.sortingOrder = SortingOrder;
            m_Root = (RectTransform)go.transform;
            m_Root.pivot = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < m_Groups.Length; i++)
                m_Groups[i] = NewGroup((VisorAnchor)i);
            Fit();
        }

        // Grupo do tamanho da tela inteira, com o pivô na borda do lado dele (é em volta dela que a curvatura gira)
        RectTransform NewGroup(VisorAnchor anchor)
        {
            var go = new GameObject("Visor_" + anchor, typeof(RectTransform)) { layer = m_Layer };
            go.transform.SetParent(m_Root, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            float side = anchor == VisorAnchor.TopLeft || anchor == VisorAnchor.BottomLeft ? 0f
                : anchor is VisorAnchor.BottomCenter or VisorAnchor.TopCenter or VisorAnchor.Center ? 0.5f : 1f;
            rect.pivot = new Vector2(side, 0.5f);
            return rect;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (m_Root)
                Destroy(m_Root.parent.gameObject);
        }

        void LateUpdate()
        {
            if (!Camera)
            {
                Destroy(gameObject);
                return;
            }

            Fit();
            if (!m_MicrogameHidden)
                m_MicrogameHidden = HideMicrogameHud();

            // Peças criadas depois (itens, avisos) entram na camada da câmera de armas
            if (Time.unscaledTime >= m_NextLayerCheck)
            {
                m_NextLayerCheck = Time.unscaledTime + 0.25f;
                HideCompass();
                m_Root.GetComponentsInChildren(true, s_Children);
                if (s_Children.Count != m_LastChildCount)
                {
                    m_LastChildCount = s_Children.Count;
                    foreach (Transform t in s_Children)
                        t.gameObject.layer = m_Layer;
                }
            }
        }

        // Canvas cobrindo a vista: altura de referência 1080, largura pela proporção da tela
        void Fit()
        {
            VisorSettings settings = VisorSettings.Current;
            float distance = settings.Distance, curvature = settings.Curvature;
            float aspect = Camera.aspect;
            m_Root.sizeDelta = new Vector2(ReferenceHeight * aspect, ReferenceHeight);
            float height = 2f * distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            m_Root.localScale = Vector3.one * (height / ReferenceHeight);
            m_Root.localPosition = new Vector3(0f, 0f, distance);
            m_Root.localRotation = Quaternion.identity;

            for (int i = 0; i < m_Groups.Length; i++)
            {
                float yaw = (VisorAnchor)i switch
                {
                    VisorAnchor.TopLeft or VisorAnchor.BottomLeft => -curvature,
                    VisorAnchor.TopRight or VisorAnchor.BottomRight => curvature,
                    _ => 0f,
                };
                m_Groups[i].localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        // Vida (canto inferior esquerdo), painel de armas e aviso de pausa do Microgame
        static bool HideMicrogameHud()
        {
            var health = FindAnyObjectByType<PlayerHealthBar>();
            if (!health)
                return false;
            Transform hud = health.transform.Find("HUD");
            if (!hud)
                return true;
            foreach (string name in new[] { "BottomLeftcorner", "WeaponHUDManager", "PauseMenuInfo" })
            {
                Transform part = hud.Find(name);
                if (part)
                    part.gameObject.SetActive(false);
            }

            return true;
        }

        // Bússola do topo do Microgame: o componente é desligado e todo desenho dela (inclusive os marcadores que os
        // inimigos registram depois, por isso a checagem periódica) fica apagado. O objeto continua ativo porque os
        // CompassElement dos inimigos procuram a bússola no Awake e dariam erro sem ela.
        static void HideCompass()
        {
            var compass = FindAnyObjectByType<Unity.FPS.UI.Compass>();
            if (!compass)
                return;
            compass.enabled = false;
            HideGraphics(compass.gameObject);
            if (compass.CompasRect)
                HideGraphics(compass.CompasRect.gameObject);
        }

        static void HideGraphics(GameObject root)
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (!group)
                group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.enabled = false;
        }
    }
}
