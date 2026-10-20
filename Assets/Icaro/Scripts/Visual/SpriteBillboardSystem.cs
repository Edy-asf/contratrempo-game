using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Gira os SpriteBillboard para a câmera principal (criado sozinho no primeiro registro): só quando a câmera muda
    // de lugar (o giro depende só da posição dela) e só os que estão a menos de MaxDistance (os de longe nem são
    // desenhados). Os sprites do cenário gerado não passam por aqui: giram na placa de vídeo (Icaro/BillboardSprite).
    [DefaultExecutionOrder(500)]
    public class SpriteBillboardSystem : MonoBehaviour
    {
        static readonly List<Transform> s_Sprites = new List<Transform>(256);
        static SpriteBillboardSystem s_Instance;

        const float MaxDistance = 300f;
        Vector3 m_LastEye = new Vector3(float.MaxValue, 0f, 0f);
        int m_LastCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Sprites.Clear();
            s_Instance = null;
        }

        public static void Register(Transform sprite)
        {
            s_Sprites.Add(sprite);
            if (!s_Instance && Application.isPlaying)
                s_Instance = new GameObject("SpritesBillboard").AddComponent<SpriteBillboardSystem>();
        }

        public static void Unregister(Transform sprite) => s_Sprites.Remove(sprite);

        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (!camera)
                return;

            Vector3 eye = camera.transform.position;
            // Câmera parada e nenhum sprite novo: nada muda
            if ((eye - m_LastEye).sqrMagnitude < 0.0001f && s_Sprites.Count == m_LastCount)
                return;
            m_LastEye = eye;
            m_LastCount = s_Sprites.Count;

            const float limit = MaxDistance * MaxDistance;
            for (int i = s_Sprites.Count - 1; i >= 0; i--)
            {
                Transform sprite = s_Sprites[i];
                if (!sprite)
                {
                    s_Sprites.RemoveAt(i);
                    continue;
                }

                Vector3 away = sprite.position - eye;
                away.y = 0f;
                if (away.sqrMagnitude > limit)
                    continue;
                if (away.sqrMagnitude > 0.0001f)
                    sprite.rotation = Quaternion.LookRotation(away);
            }
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }
    }
}
