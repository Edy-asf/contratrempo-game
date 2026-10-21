using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Portal de anéis no fim da Ponte Colossal: quando a sala do chefe é limpa, os anéis aceleram aos poucos e o centro
    // do portal se enche de luz (o núcleo cresce e a luz acende). Só visual por enquanto (o portal ainda não leva a
    // lugar nenhum).
    public class PortalActivation : MonoBehaviour
    {
        public RoomManager Room;
        public SlowRotation[] Rings;
        public Transform Core;
        public Light Glow;

        [Tooltip("Quanto os anéis aceleram")]
        public float SpeedUp = 3f;

        [Tooltip("Duração da ativação (s)")]
        public float Duration = 4f;

        public float GlowIntensity = 6f;

        bool m_Active;

        void Start()
        {
            if (Core)
            {
                Core.localScale = Vector3.zero;
                Core.gameObject.SetActive(false);
            }

            if (Glow)
                Glow.intensity = 0f;
            if (Room)
                Room.OnRoomCleared += OnCleared;
        }

        void OnDestroy()
        {
            if (Room)
                Room.OnRoomCleared -= OnCleared;
        }

        void OnCleared(RoomManager room)
        {
            if (!m_Active)
                StartCoroutine(Activate());
        }

        IEnumerator Activate()
        {
            m_Active = true;
            var speeds = new float[Rings.Length];
            for (int i = 0; i < Rings.Length; i++)
                speeds[i] = Rings[i] ? Rings[i].DegreesPerSecond : 0f;
            if (Core)
                Core.gameObject.SetActive(true);
            for (float t = 0f; t < Duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Duration);
                for (int i = 0; i < Rings.Length; i++)
                {
                    if (Rings[i])
                        Rings[i].DegreesPerSecond = speeds[i] * Mathf.Lerp(1f, SpeedUp, k);
                }

                if (Core)
                    Core.localScale = Vector3.one * k;
                if (Glow)
                    Glow.intensity = GlowIntensity * k;
                yield return null;
            }
        }
    }
}
