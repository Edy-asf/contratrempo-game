using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Inimigo marcado (o RoomManager marca alguns por sala, espalhados pela cota): um relógio âmbar em pixel art
    // flutua sobre ele, sempre virado para a câmera; ao morrer, solta o orbe do tempo (TimeOrb) do centro do corpo.
    public class TimeMark : MonoBehaviour
    {
        static Sprite s_Clock;

        TimeOrbSettings m_Settings;
        Transform m_Icon;
        Health m_Health;
        float m_Height;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Clock = null;

        public static void Apply(GameObject enemy, TimeOrbSettings settings)
        {
            if (!enemy || settings == null || enemy.GetComponent<TimeMark>())
                return;
            enemy.AddComponent<TimeMark>().Begin(settings);
        }

        void Begin(TimeOrbSettings settings)
        {
            m_Settings = settings;
            m_Health = GetComponent<Health>();
            if (m_Health)
                m_Health.OnDie += OnDie;

            // Acima do topo do corpo (colisores do inimigo)
            float top = transform.position.y + 2f;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger)
                    top = Mathf.Max(top, collider.bounds.max.y);
            }
            m_Height = top - transform.position.y + 0.7f;

            m_Icon = new GameObject("Marca_Tempo").transform;
            m_Icon.SetParent(transform, false);
            m_Icon.localPosition = Vector3.up * m_Height;
            var renderer = m_Icon.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Clock();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if (m_Health)
                m_Health.OnDie -= OnDie;
        }

        void OnDie()
        {
            var actor = GetComponent<Actor>();
            Vector3 center = actor && actor.AimPoint ? actor.AimPoint.position : transform.position + Vector3.up;
            TimeOrb.Spawn(m_Settings, center);
            if (m_Icon)
                Destroy(m_Icon.gameObject);
            if (m_Health)
                m_Health.OnDie -= OnDie;
        }

        void LateUpdate()
        {
            if (!m_Icon)
                return;
            Camera camera = Camera.main;
            m_Icon.position = transform.position + Vector3.up * (m_Height + Mathf.Sin(Time.time * 3f) * 0.08f);
            if (!camera)
                return;
            Vector3 away = m_Icon.position - camera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                m_Icon.rotation = Quaternion.LookRotation(away);
        }

        // Relógio de 20 x 20 pixels (1,25 m): aro âmbar, mostrador escuro, ponteiros claros e brilho
        static Sprite Clock()
        {
            if (s_Clock)
                return s_Clock;
            var canvas = new PixelCanvas(20, 20);
            canvas.Fill(PixelCanvas.Circle(10f, 10f, 8.5f), PixelRamps.Amber);
            canvas.Fill(PixelCanvas.Circle(10f, 10f, 6.2f), PixelRamps.Dark, -0.2f);
            for (int i = 0; i < 12; i += 3)
            {
                float a = i * Mathf.PI / 6f;
                canvas.Set(Mathf.RoundToInt(10f + Mathf.Cos(a) * 5f - 0.5f), Mathf.RoundToInt(10f + Mathf.Sin(a) * 5f - 0.5f),
                    PixelRamps.Amber[3]);
            }
            canvas.Line(10, 10, 10, 5, PixelRamps.White);
            canvas.Line(10, 10, 13, 10, PixelRamps.Amber[4]);
            canvas.Set(10, 10, PixelRamps.White);
            canvas.Rect(9, 0, 3, 2, PixelRamps.Amber[2]);
            canvas.Outline(PixelRamps.Outline);
            Texture2D texture = ItemArt.ToTexture(canvas, "Marca_Tempo");
            s_Clock = Sprite.Create(texture, new Rect(0f, 0f, 20f, 20f), new Vector2(0.5f, 0.5f), 16f);
            return s_Clock;
        }
    }
}
