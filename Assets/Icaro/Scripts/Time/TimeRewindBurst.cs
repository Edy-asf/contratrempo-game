using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // O tempo voltando onde um inimigo morreu: um relógio ciano em pixel art surge no lugar, o ponteiro gira para trás
    // (mais voltas quanto mais o abate tirou) e um anel se abre em volta antes de tudo sumir. Sempre virado para a
    // câmera, em tempo de jogo. Reaproveita as instâncias. O TimeTransitionFx chama Spawn em cada redução.
    public class TimeRewindBurst : MonoBehaviour
    {
        const float k_Duration = 0.75f;

        static Sprite s_Dial, s_Hand, s_Ring;
        static readonly Stack<TimeRewindBurst> s_Pool = new Stack<TimeRewindBurst>();

        SpriteRenderer m_Dial, m_Hand, m_Ring;
        float m_Age;
        float m_Size;
        float m_Turns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Dial = s_Hand = s_Ring = null;
            s_Pool.Clear();
        }

        // amount: quanto o multiplicador caiu (o tamanho e as voltas do ponteiro crescem com ele)
        public static void Spawn(Vector3 position, float amount)
        {
            TimeRewindBurst burst = null;
            while (s_Pool.Count > 0 && !burst)
                burst = s_Pool.Pop();
            if (!burst)
                burst = Create();
            float k = Mathf.Clamp01(amount / 0.4f);
            burst.transform.position = position;
            burst.m_Size = Mathf.Lerp(0.9f, 2.4f, k);
            burst.m_Turns = Mathf.Lerp(0.6f, 2.2f, k);
            burst.m_Age = 0f;
            burst.gameObject.SetActive(true);
            burst.Animate();
        }

        static TimeRewindBurst Create()
        {
            var go = new GameObject("Tempo_Voltando");
            var burst = go.AddComponent<TimeRewindBurst>();
            burst.m_Ring = Part(go.transform, "Anel", Ring(), 0);
            burst.m_Dial = Part(go.transform, "Mostrador", Dial(), 1);
            burst.m_Hand = Part(go.transform, "Ponteiro", Hand(), 2);
            return burst;
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        void LateUpdate()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= k_Duration)
            {
                gameObject.SetActive(false);
                s_Pool.Push(this);
                return;
            }

            Animate();
        }

        void Animate()
        {
            float u = m_Age / k_Duration;
            Camera camera = Camera.main;
            if (camera)
            {
                Vector3 away = transform.position - camera.transform.position;
                if (away.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(away);
            }

            // Surge esticando, fica e encolhe no fim
            float grow = u < 0.18f ? VisorUI.EaseOutBack(u / 0.18f) : 1f;
            float shrink = u > 0.72f ? 1f - (u - 0.72f) / 0.28f : 1f;
            float size = m_Size * grow * Mathf.Max(0f, shrink);
            m_Dial.transform.localScale = Vector3.one * size;
            m_Hand.transform.localScale = Vector3.one * size;
            // Ponteiro para trás (anti-horário visto de frente), rápido no começo e assentando no 12
            float sweep = 1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.7f), 3f);
            m_Hand.transform.localRotation = Quaternion.Euler(0f, 0f, -sweep * m_Turns * 360f);

            float alpha = Mathf.Clamp01(shrink * 1.5f);
            m_Dial.color = new Color(1f, 1f, 1f, alpha);
            m_Hand.color = new Color(1f, 1f, 1f, alpha);
            // Anel que se abre
            float ring = Mathf.Clamp01(u / 0.8f);
            m_Ring.transform.localScale = Vector3.one * (m_Size * (0.8f + ring * 1.8f));
            m_Ring.color = new Color(1f, 1f, 1f, (1f - ring) * 0.9f);
        }

        // Mostrador de 24 x 24 px: aro ciano, fundo escuro e as marcas das horas
        internal static Sprite Dial()
        {
            if (s_Dial)
                return s_Dial;
            var canvas = new PixelCanvas(24, 24);
            canvas.Fill(PixelCanvas.Circle(12f, 12f, 10.5f), PixelRamps.Cyan);
            canvas.Fill(PixelCanvas.Circle(12f, 12f, 8.6f), PixelRamps.Dark, -0.25f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                float r = i % 3 == 0 ? 6.8f : 7.4f;
                canvas.Set(Mathf.RoundToInt(12f + Mathf.Cos(a) * r - 0.5f), Mathf.RoundToInt(12f + Mathf.Sin(a) * r - 0.5f),
                    i % 3 == 0 ? PixelRamps.White : PixelRamps.Cyan[3]);
            }

            canvas.Rect(11, 11, 2, 2, PixelRamps.White);
            canvas.Outline(PixelRamps.Outline);
            return s_Dial = ToSprite(canvas, "Tempo_Mostrador");
        }

        // Ponteiro: do centro até perto do aro, com a ponta clara (gira em volta do centro do sprite)
        internal static Sprite Hand()
        {
            if (s_Hand)
                return s_Hand;
            var canvas = new PixelCanvas(24, 24);
            canvas.Line(11, 12, 11, 19, PixelRamps.Cyan[4]);
            canvas.Line(12, 12, 12, 19, PixelRamps.Cyan[3]);
            canvas.Set(11, 20, PixelRamps.White);
            canvas.Set(12, 20, PixelRamps.White);
            return s_Hand = ToSprite(canvas, "Tempo_Ponteiro");
        }

        // Anel fino de 32 px
        static Sprite Ring()
        {
            if (s_Ring)
                return s_Ring;
            var canvas = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x + 0.5f - 16f, dy = y + 0.5f - 16f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 13.6f && d < 15.4f)
                    canvas.Set(x, y, d > 14.5f ? PixelRamps.Cyan[2] : PixelRamps.Cyan[4]);
            }

            return s_Ring = ToSprite(canvas, "Tempo_Anel");
        }

        // O sprite inteiro mede 1 m (a escala do objeto dá o tamanho)
        static Sprite ToSprite(PixelCanvas canvas, string name)
        {
            Texture2D texture = ItemArt.ToTexture(canvas, name);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                texture.width);
        }
    }
}
