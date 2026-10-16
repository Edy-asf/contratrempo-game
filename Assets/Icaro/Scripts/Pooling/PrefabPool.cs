using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Pool simples de instâncias por prefab: Get reativa uma instância guardada (ou cria uma), Release desativa e
    // guarda para a próxima vez. Cada instância leva um PooledObject com o prefab de origem. Os componentes da
    // instância se preparam de novo no OnEnable. Usado pelo Zangão e pelos projéteis dele.
    public static class PrefabPool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> s_Free = new Dictionary<GameObject, Stack<GameObject>>();
        static Transform s_Root;

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!prefab)
                return null;

            if (s_Free.TryGetValue(prefab, out Stack<GameObject> free))
            {
                while (free.Count > 0)
                {
                    GameObject reused = free.Pop();
                    if (!reused)
                        continue;
                    reused.transform.SetParent(null, false);
                    reused.transform.SetPositionAndRotation(position, rotation);
                    reused.SetActive(true);
                    return reused;
                }
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            var pooled = instance.GetComponent<PooledObject>();
            if (!pooled)
                pooled = instance.AddComponent<PooledObject>();
            pooled.Prefab = prefab;
            return instance;
        }

        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject instance = Get(prefab ? prefab.gameObject : null, position, rotation);
            return instance ? instance.GetComponent<T>() : null;
        }

        // Guarda a instância (sem PooledObject, destrói)
        public static void Release(GameObject instance)
        {
            if (!instance)
                return;

            var pooled = instance.GetComponent<PooledObject>();
            if (!pooled || !pooled.Prefab)
            {
                Object.Destroy(instance);
                return;
            }

            if (!s_Root)
            {
                s_Root = new GameObject("PrefabPool").transform;
                s_Root.gameObject.SetActive(false);
            }

            instance.SetActive(false);
            instance.transform.SetParent(s_Root, false);
            if (!s_Free.TryGetValue(pooled.Prefab, out Stack<GameObject> free))
                s_Free[pooled.Prefab] = free = new Stack<GameObject>();
            free.Push(instance);
        }

        // Cria instâncias de antemão (evita picos de Instantiate durante o combate)
        public static void Prewarm(GameObject prefab, int count)
        {
            var created = new List<GameObject>();
            for (int i = 0; i < count; i++)
                created.Add(Get(prefab, Vector3.zero, Quaternion.identity));
            foreach (GameObject instance in created)
                Release(instance);
        }
    }
}
