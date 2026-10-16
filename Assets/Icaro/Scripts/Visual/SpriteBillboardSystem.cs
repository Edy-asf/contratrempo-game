using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Gira todos os SpriteBillboard para a câmera principal, uma vez por frame (criado sozinho no primeiro registro)
    [DefaultExecutionOrder(500)]
    public class SpriteBillboardSystem : MonoBehaviour
    {
        static readonly List<Transform> s_Sprites = new List<Transform>(256);
        static SpriteBillboardSystem s_Instance;

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
