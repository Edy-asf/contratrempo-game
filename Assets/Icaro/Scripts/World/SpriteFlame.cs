using UnityEngine;

namespace Icaro
{
    // Chama em sprite animado (pixel art, azul-esverdeada) que sempre olha para a câmera girando só em volta do eixo
    // vertical. Os quadros são desenhados em código na primeira vez e divididos por todas as chamas; cada uma começa
    // num quadro diferente. Só visual.
    public class SpriteFlame : MonoBehaviour
    {
        [Tooltip("Quadros por segundo da animação")]
        public float FramesPerSecond = 14f;

        [Tooltip("Altura da chama (m)")]
        public float Height = 1.1f;

        const int k_Frames = 12, k_Width = 40, k_HeightPx = 40;

        static Sprite[] s_Frames;
        static Material s_Material;

        SpriteRenderer m_Renderer;
        Transform m_Camera;
        float m_Phase;
        int m_Frame = -1;

        void Awake()
        {
            if (s_Frames == null || s_Frames[0] == null)
                Build();
            m_Renderer = GetComponent<SpriteRenderer>();
            if (!m_Renderer)
                m_Renderer = gameObject.AddComponent<SpriteRenderer>();
            if (s_Material)
                m_Renderer.sharedMaterial = s_Material;
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            transform.localScale = Vector3.one * (Height / (k_HeightPx / PixelsPerUnit));
            m_Phase = Random.value * k_Frames;
        }

        const float PixelsPerUnit = 36f;

        void LateUpdate()
        {
            int frame = (int)(Time.time * FramesPerSecond + m_Phase) % k_Frames;
            if (frame != m_Frame)
            {
                m_Frame = frame;
                m_Renderer.sprite = s_Frames[frame];
            }

            if (!m_Camera)
            {
                Camera main = Camera.main;
                if (!main)
                    return;
                m_Camera = main.transform;
            }

            Vector3 toCamera = m_Camera.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(-toCamera);
        }

        // Fogo que sobe: forma larga afinando para cima, que balança de um lado para o outro (mais no alto), recortada
        // por ruído que corre para cima (duas amostras misturadas para o último quadro emendar no primeiro) e pintada
        // em quatro tons, do miolo claro à borda escura; fagulhas soltas acima da chama
        static void Build()
        {
            Shader shader = Shader.Find("Sprites/Default");
            s_Material = shader ? new Material(shader) { name = "Chama_Sprite" } : null;
            var core = new Color(0.85f, 1f, 0.95f);
            var bright = new Color(0.3f, 1f, 0.8f);
            var middle = new Color(0.05f, 0.8f, 0.68f);
            var dark = new Color(0.02f, 0.45f, 0.48f, 0.9f);
            const float scale = 0.17f, climb = 3.4f;
            s_Frames = new Sprite[k_Frames];
            for (int f = 0; f < k_Frames; f++)
            {
                var texture = new Texture2D(k_Width, k_HeightPx, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"Chama_{f}",
                };
                var pixels = new Color[k_Width * k_HeightPx];
                float blend = (float)f / k_Frames, phase = blend * Mathf.PI * 2f;
                for (int y = 0; y < k_HeightPx; y++)
                for (int x = 0; x < k_Width; x++)
                {
                    float v = (y + 0.5f) / k_HeightPx;
                    // Balanço: cada altura desloca para um lado, em onda que sobe com o tempo
                    float sway = (Mathf.Sin(phase - v * 5f) * 0.16f + Mathf.Sin(phase * 2f - v * 9f + 1.3f) * 0.07f) * v;
                    float u = (x + 0.5f) / k_Width * 2f - 1f - sway;
                    float a = Mathf.PerlinNoise(x * scale + 3.1f, y * scale - f * climb);
                    float b = Mathf.PerlinNoise(x * scale + 3.1f, y * scale - (f - k_Frames) * climb);
                    float noise = Mathf.Lerp(a, b, blend);
                    float width = 0.95f * (1f - v * 0.7f);
                    float heat = (1f - Mathf.Abs(u) / Mathf.Max(width, 0.05f)) * (1.15f - v) + (noise - 0.5f) * 1.3f * v +
                                 (1f - v) * 0.15f;
                    Color c = heat > 0.72f ? core : heat > 0.5f ? bright : heat > 0.3f ? middle : heat > 0.16f ? dark
                        : Color.clear;
                    pixels[y * k_Width + x] = c;
                }

                // Fagulhas subindo acima da chama (a mesma fagulha sobe um pouco a cada quadro)
                var sparks = new System.Random(91);
                for (int k = 0; k < 6; k++)
                {
                    int sx = sparks.Next(k_Width / 4, k_Width * 3 / 4);
                    int start = sparks.Next(0, k_Frames);
                    int sy = k_HeightPx / 2 + ((f - start + k_Frames) % k_Frames) * (k_HeightPx / 2) / k_Frames;
                    int wobble = (int)(Mathf.Sin(phase * 2f + k) * 1.5f);
                    if (sy < k_HeightPx)
                        pixels[sy * k_Width + Mathf.Clamp(sx + wobble, 0, k_Width - 1)] = k % 2 == 0 ? bright : core;
                }

                texture.SetPixels(pixels);
                texture.Apply(false, true);
                s_Frames[f] = Sprite.Create(texture, new Rect(0, 0, k_Width, k_HeightPx), new Vector2(0.5f, 0f),
                    PixelsPerUnit);
            }
        }
    }
}
