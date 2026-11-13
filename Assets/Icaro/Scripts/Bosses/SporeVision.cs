using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Vista curta dentro das nuvens de esporos (SporeCloud): uma névoa verde-musgo em pixel art fecha a tela das
    // bordas para o meio, com pontos âmbar e ciano boiando, deixando só um círculo pequeno no centro. Entra em 0,35 s e
    // sai em 0,6 s (tempo sem escala). Canvas próprio por cima do cenário e abaixo dos menus; criado na primeira nuvem.
    public class SporeVision : MonoBehaviour
    {
        const int k_Width = 160, k_Height = 90;

        static SporeVision s_Instance;

        RawImage m_Image;
        Texture2D m_Texture;
        Color32[] m_Pixels;
        float m_Amount;
        float m_Drawn = -1f;
        float m_Clock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Instance = null;

        public static void Ensure()
        {
            if (s_Instance)
                return;
            var go = new GameObject("Visao_Esporos");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;
            s_Instance = go.AddComponent<SporeVision>();
        }

        void Awake()
        {
            m_Texture = new Texture2D(k_Width, k_Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "VisaoEsporos",
            };
            m_Pixels = new Color32[k_Width * k_Height];
            var image = new GameObject("Nevoa").AddComponent<RawImage>();
            image.transform.SetParent(transform, false);
            image.texture = m_Texture;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            m_Image = image;
            m_Image.enabled = false;
        }

        void OnDestroy()
        {
            if (m_Texture)
                Destroy(m_Texture);
        }

        void Update()
        {
            bool inside = SporeCloud.PlayerInside;
            float dt = Time.unscaledDeltaTime;
            m_Amount = Mathf.MoveTowards(m_Amount, inside ? 1f : 0f, dt / (inside ? 0.35f : 0.6f));
            m_Clock += dt;
            m_Image.enabled = m_Amount > 0.01f;
            if (!m_Image.enabled)
                return;

            // Redesenha a 12 quadros por segundo (os pontos boiando andam em degraus, como pixel art)
            float step = Mathf.Floor(m_Clock * 12f) / 12f;
            if (Mathf.Approximately(step, m_Drawn) && m_Amount >= 1f)
                return;
            m_Drawn = step;
            Draw(m_Amount, step);
        }

        static readonly float[] k_Bayer =
        {
            0f, 8f, 2f, 10f, 12f, 4f, 14f, 6f, 3f, 11f, 1f, 9f, 15f, 7f, 13f, 5f,
        };

        void Draw(float amount, float time)
        {
            // raio livre no meio: de bem aberto (entrando) a 22% da altura (dentro)
            float clear = Mathf.Lerp(1.3f, 0.22f, amount);
            float soft = 0.32f;
            var fog = new Color32(34, 58, 36, 0);
            var deep = new Color32(16, 30, 20, 0);
            float aspect = k_Width / (float)k_Height;
            for (int y = 0; y < k_Height; y++)
            for (int x = 0; x < k_Width; x++)
            {
                float u = (x + 0.5f) / k_Width * 2f - 1f;
                float v = (y + 0.5f) / k_Height * 2f - 1f;
                float d = Mathf.Sqrt(u * u * aspect * aspect * 0.25f + v * v);
                float wobble = 0.05f * Mathf.Sin(x * 0.21f + time * 1.7f) + 0.05f * Mathf.Sin(y * 0.33f - time * 1.3f);
                float k = Mathf.Clamp01((d + wobble - clear) / soft);
                float threshold = (k_Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                Color32 c = k > 0.75f ? deep : fog;
                c.a = (byte)(k * amount > threshold ? (k > 0.75f ? 235 : 190) : 0);
                m_Pixels[y * k_Width + x] = c;
            }

            // esporos boiando na névoa
            for (int i = 0; i < 26; i++)
            {
                float px = Mathf.Repeat(i * 37.3f + Mathf.Sin(i * 1.7f) * 20f + time * (3f + i % 4), k_Width);
                float py = Mathf.Repeat(i * 19.1f + time * (2f + i % 3) * (i % 2 == 0 ? 1f : -1f), k_Height);
                int index = (int)py * k_Width + (int)px;
                if (m_Pixels[index].a == 0)
                    continue;
                m_Pixels[index] = i % 3 == 0 ? new Color32(110, 230, 225, 255) : new Color32(255, 190, 80, 255);
            }

            m_Texture.SetPixels32(m_Pixels);
            m_Texture.Apply(false);
        }
    }
}
