using UnityEngine;

namespace Icaro
{
    // Feixe do farol (Arquipélago): gira devagar em volta do eixo vertical, varrendo a névoa e o mar. Só visual.
    public class LighthouseBeam : MonoBehaviour
    {
        [Tooltip("Graus por segundo")]
        public float Speed = 24f;

        void Update() => transform.Rotate(Vector3.up, Speed * Time.deltaTime, Space.World);
    }
}
