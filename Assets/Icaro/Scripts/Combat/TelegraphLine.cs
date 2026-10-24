using UnityEngine;

namespace Icaro
{
    // Linha de aviso de ataque (LineRenderer com material aditivo): segmento reto, trajetória colada no chão ou
    // círculo no chão. Usada pelo Predador (trajetória), Golem (mira do laser, feixe, área da pancada) e pelo
    // aviso de spawn.
    [RequireComponent(typeof(LineRenderer))]
    public class TelegraphLine : MonoBehaviour
    {
        const float k_GroundLift = 0.08f;
        const int k_GroundSamples = 16;
        const int k_CircleSegments = 40;

        LineRenderer m_Line;

        public static TelegraphLine Create(Transform parent, Material material, string name)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.positionCount = 0;
            return go.AddComponent<TelegraphLine>();
        }

        LineRenderer Line => m_Line ? m_Line : m_Line = GetComponent<LineRenderer>();

        public void Hide() => Line.positionCount = 0;

        public void SetStyle(float width, Color color)
        {
            Line.startWidth = Line.endWidth = width;
            Line.startColor = Line.endColor = color;
        }

        public void SetSegment(Vector3 from, Vector3 to)
        {
            Line.alignment = LineAlignment.View;
            Line.loop = false;
            Line.positionCount = 2;
            Line.SetPosition(0, from);
            Line.SetPosition(1, to);
        }

        // Trajetória de 'from' a 'to' acompanhando o relevo
        public void SetGroundPath(Vector3 from, Vector3 to)
        {
            Line.alignment = LineAlignment.TransformZ;
            Line.loop = false;
            transform.rotation = Quaternion.LookRotation(Vector3.down);
            Line.positionCount = k_GroundSamples;
            for (int i = 0; i < k_GroundSamples; i++)
                Line.SetPosition(i, Ground(Vector3.Lerp(from, to, i / (k_GroundSamples - 1f))));
        }

        // Círculo no chão
        public void SetCircle(Vector3 center, float radius)
        {
            Line.alignment = LineAlignment.TransformZ;
            Line.loop = true;
            transform.rotation = Quaternion.LookRotation(Vector3.down);
            Line.positionCount = k_CircleSegments;
            for (int i = 0; i < k_CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / k_CircleSegments;
                Line.SetPosition(i, Ground(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius));
            }
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * k_GroundLift;
            return p + Vector3.up * k_GroundLift;
        }
    }
}
