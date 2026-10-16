using UnityEngine;

namespace Icaro
{
    // Marca uma instância do PrefabPool com o prefab de origem (para Release saber em qual pilha guardar)
    public class PooledObject : MonoBehaviour
    {
        [HideInInspector] public GameObject Prefab;
    }
}
