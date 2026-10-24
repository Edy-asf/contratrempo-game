using UnityEngine;

namespace Icaro
{
    // Visuais provisórios dos efeitos de item, montados na hora com o material aditivo do catálogo: raio (linha
    // em zigue-zague) e explosão (esfera que cresce e some). Cada um se apaga sozinho (ItemVfxFade).
    public static class ItemVfx
    {
        static readonly Color k_LightningColor = new Color(0.55f, 0.85f, 1f);

        public static void Lightning(ItemCatalog catalog, Vector3[] points)
        {
            if (!catalog || !catalog.EffectMaterial || points.Length < 2)
                return;

            // Cada trecho ganha pontos intermediários deslocados, para parecer um raio
            const int subdivisions = 5;
            var jagged = new Vector3[(points.Length - 1) * subdivisions + 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                for (int s = 0; s < subdivisions; s++)
                {
                    float t = s / (float)subdivisions;
                    Vector3 offset = s == 0 ? Vector3.zero : Random.insideUnitSphere * 0.35f;
                    jagged[i * subdivisions + s] = Vector3.Lerp(points[i], points[i + 1], t) + offset;
                }
            }
            jagged[jagged.Length - 1] = points[points.Length - 1];

            var go = new GameObject("RaioBobina");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = catalog.EffectMaterial;
            line.positionCount = jagged.Length;
            line.SetPositions(jagged);
            line.widthMultiplier = 0.12f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<ItemVfxFade>().Begin(0.25f, k_LightningColor, 1f);
        }

        public static void Blast(ItemCatalog catalog, Vector3 center, float radius, Color color)
        {
            if (!catalog || !catalog.EffectMaterial)
                return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExplosaoResina";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = center;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.EffectMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<ItemVfxFade>().Begin(0.35f, color, radius * 2f);
        }
    }
}
