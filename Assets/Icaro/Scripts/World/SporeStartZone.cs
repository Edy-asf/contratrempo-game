using UnityEngine;

namespace Icaro
{
    // Onde as partículas de brilho do bioma começam (gerado com o Arquipélago, na raiz do bioma): o SporeField só
    // começa a soltá-las quando a câmera entra neste círculo (no Arquipélago, a primeira ilhota depois da praia, ao pé
    // das escadarias da costa) e, dali em diante, continuam. As coordenadas são relativas à raiz do bioma.
    public class SporeStartZone : MonoBehaviour
    {
        [Tooltip("Centro (x, z) do círculo, em relação à raiz do bioma (m)")]
        public Vector2 Center;

        [Tooltip("Raio do círculo (m)")]
        public float Radius = 9f;

        public bool Contains(Vector3 world)
        {
            Vector3 local = world - transform.position;
            return new Vector2(local.x - Center.x, local.z - Center.y).sqrMagnitude <= Radius * Radius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.9f);
            Vector3 center = transform.position + new Vector3(Center.x, 0f, Center.y);
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f, b = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius,
                    center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * Radius);
            }
        }
    }
}
