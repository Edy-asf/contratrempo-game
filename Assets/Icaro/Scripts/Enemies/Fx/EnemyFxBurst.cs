using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Animação de efeito que toca uma vez (clarão do disparo, impacto, explosão, queimado): quadros em pixel art de
    // frente para a câmera ou deitados numa superfície. Anda no relógio dos inimigos (CombatTiming), um pouco mais
    // rápido com o multiplicador (até 2x), e para na volta no tempo. Instâncias reaproveitadas.
    public class EnemyFxBurst : MonoBehaviour
    {
        static readonly Stack<EnemyFxBurst> s_Pool = new Stack<EnemyFxBurst>();
        static readonly List<EnemyFxBurst> s_Active = new List<EnemyFxBurst>();

        SpriteRenderer m_Renderer;
        Sprite[] m_Frames;
        float m_Fps;
        float m_Age;
        float m_Hold;
        float m_Fade;
        bool m_Flat;
        Transform m_Follow;
        Vector3 m_FollowOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pool.Clear();
            s_Active.Clear();
        }

        // De frente para a câmera; follow: acompanha o cano enquanto toca
        public static EnemyFxBurst Play(Sprite[] frames, Vector3 position, float fps, float scale = 1f,
            Transform follow = null)
        {
            EnemyFxBurst burst = Take(frames, position, fps, scale);
            if (!burst)
                return null;
            burst.m_Follow = follow;
            burst.m_FollowOffset = follow ? position - follow.position : Vector3.zero;
            burst.Face();
            return burst;
        }

        // Deitado na superfície de normal "normal"; hold: segundos parado no último quadro; fade: segundos sumindo
        public static EnemyFxBurst PlayFlat(Sprite[] frames, Vector3 position, Vector3 normal, float fps, float hold,
            float fade, float scale = 1f)
        {
            EnemyFxBurst burst = Take(frames, position + normal * 0.03f, fps, scale);
            if (!burst)
                return null;
            burst.m_Flat = true;
            burst.m_Hold = hold;
            burst.m_Fade = fade;
            // Giro de 90 em 90 graus (pixel art não gira em ângulos quebrados)
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            burst.transform.rotation = Quaternion.LookRotation(-normal, up) *
                                       Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
            return burst;
        }

        static EnemyFxBurst Take(Sprite[] frames, Vector3 position, float fps, float scale)
        {
            if (frames == null || frames.Length == 0)
                return null;
            EnemyFxBurst burst = null;
            while (s_Pool.Count > 0 && !burst)
                burst = s_Pool.Pop();
            if (!burst)
            {
                var go = new GameObject("Efeito_Inimigo");
                burst = go.AddComponent<EnemyFxBurst>();
                burst.m_Renderer = go.AddComponent<SpriteRenderer>();
                burst.m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                burst.m_Renderer.receiveShadows = false;
            }

            burst.m_Frames = frames;
            burst.m_Fps = fps;
            burst.m_Age = 0f;
            burst.m_Hold = 0f;
            burst.m_Fade = 0f;
            burst.m_Flat = false;
            burst.m_Follow = null;
            burst.transform.position = position;
            burst.transform.localScale = Vector3.one * scale;
            burst.m_Renderer.sprite = frames[0];
            burst.m_Renderer.color = Color.white;
            burst.gameObject.SetActive(true);
            s_Active.Add(burst);
            return burst;
        }

        // Some com todos (volta no tempo)
        public static void ClearAll()
        {
            for (int i = s_Active.Count - 1; i >= 0; i--)
                if (s_Active[i])
                    s_Active[i].Release();
            s_Active.Clear();
        }

        void LateUpdate()
        {
            m_Age += CombatTiming.DeltaTime * Mathf.Clamp(CombatTiming.Multiplier, 1f, 2f);
            float duration = m_Frames.Length / m_Fps;
            if (m_Age >= duration + m_Hold + m_Fade)
            {
                Release();
                return;
            }

            int frame = Mathf.Min(Mathf.FloorToInt(m_Age * m_Fps), m_Frames.Length - 1);
            m_Renderer.sprite = m_Frames[frame];
            if (m_Fade > 0f && m_Age > duration + m_Hold)
            {
                // Some em degraus de 25% (sem degradê liso)
                float u = (m_Age - duration - m_Hold) / m_Fade;
                m_Renderer.color = new Color(1f, 1f, 1f, 1f - Mathf.Floor(u * 4f) / 4f);
            }

            if (m_Follow)
                transform.position = m_Follow.position + m_FollowOffset;
            if (!m_Flat)
                Face();
        }

        void Face()
        {
            Camera camera = Camera.main;
            if (!camera)
                return;
            Vector3 away = transform.position - camera.transform.position;
            if (away.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(away, camera.transform.up);
        }

        void Release()
        {
            s_Active.Remove(this);
            gameObject.SetActive(false);
            s_Pool.Push(this);
        }
    }
}
