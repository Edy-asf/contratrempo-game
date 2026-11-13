using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Marca de queda em holograma que toma a forma do chão (morteiro do Explorador, torrão do Pé-de-Musgo, salto do
    // Orbe): um disco em malha cujos vértices são postos por raio no terreno (8 anéis x 32 lados, 10 cm acima),
    // com a pixel art da marca (EnemyFxArt.Marker) projetada de cima, e uma borda de luz em degraus subindo do chão
    // no raio do dano. Antes era um sprite chapado inclinado pela normal de um ponto só, que ficava de lado em
    // terreno irregular. Os raios ignoram personagens, inimigos e o domo da arena; onde não há chão (beira de
    // barranco), o vértice some.
    public class GroundMarker : MonoBehaviour
    {
        const int k_Rings = 8;
        const int k_Sides = 32;
        const float k_Lift = 0.1f;
        const float k_RimHeight = 0.45f;
        const int k_RimBands = 3;

        static Material s_DiskMaterial;
        static Material s_RimMaterial;
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        static readonly int k_MainTex = Shader.PropertyToID("_MainTex");

        Mesh m_Mesh;
        MaterialPropertyBlock m_Block;
        MeshRenderer m_Renderer;
        Vector3[] m_Vertices;
        Vector2[] m_Uvs;
        Color32[] m_Colors;
        bool[] m_Grounded;
        Vector3 m_Center;
        float m_Extent;
        float m_Radius;
        Sprite m_Sprite;
        int m_DiskVertices;

        public static GroundMarker Create(string name)
        {
            var go = new GameObject(name);
            var marker = go.AddComponent<GroundMarker>();
            marker.Build();
            return marker;
        }

        void Build()
        {
            m_Mesh = new Mesh { name = "MarcaHolograma" };
            m_Mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
            m_Renderer = gameObject.AddComponent<MeshRenderer>();
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            EnsureMaterials();
            m_Renderer.sharedMaterials = new[] { s_DiskMaterial, s_RimMaterial };

            m_DiskVertices = 1 + k_Rings * k_Sides;
            int rimVertices = (k_RimBands + 1) * (k_Sides + 1);
            m_Vertices = new Vector3[m_DiskVertices + rimVertices];
            m_Uvs = new Vector2[m_Vertices.Length];
            m_Colors = new Color32[m_Vertices.Length];
            m_Grounded = new bool[m_DiskVertices];

            var disk = new List<int>();
            for (int s = 0; s < k_Sides; s++)
            {
                int n = (s + 1) % k_Sides;
                disk.Add(0);
                disk.Add(1 + n);
                disk.Add(1 + s);
                for (int r = 0; r < k_Rings - 1; r++)
                {
                    int a = 1 + r * k_Sides + s, b = 1 + r * k_Sides + n;
                    int c = a + k_Sides, d = b + k_Sides;
                    disk.Add(a); disk.Add(b); disk.Add(c);
                    disk.Add(b); disk.Add(d); disk.Add(c);
                }
            }

            var rim = new List<int>();
            for (int band = 0; band < k_RimBands; band++)
            for (int s = 0; s < k_Sides; s++)
            {
                int a = m_DiskVertices + band * (k_Sides + 1) + s;
                int b = a + 1, c = a + k_Sides + 1, d = c + 1;
                // dos dois lados (visto de dentro e de fora)
                rim.Add(a); rim.Add(b); rim.Add(c);
                rim.Add(b); rim.Add(d); rim.Add(c);
                rim.Add(a); rim.Add(c); rim.Add(b);
                rim.Add(b); rim.Add(c); rim.Add(d);
            }

            m_Mesh.vertices = m_Vertices;
            m_Mesh.subMeshCount = 2;
            m_Mesh.SetTriangles(disk, 0);
            m_Mesh.SetTriangles(rim, 1);
        }

        // Os materiais dos sprites (o mesmo shader que os SpriteRenderer usam, com cor do vértice e transparência)
        static void EnsureMaterials()
        {
            if (s_DiskMaterial)
                return;
            var probe = new GameObject("SondaMaterial").AddComponent<SpriteRenderer>();
            Material sprite = probe.sharedMaterial;
            Destroy(probe.gameObject);
            s_DiskMaterial = new Material(sprite) { name = "Marca_Holograma" };
            s_RimMaterial = new Material(sprite) { name = "Marca_Holograma_Borda", mainTexture = Texture2D.whiteTexture };
        }

        // Põe a marca em 'center' (no chão) com o raio do dano; o tamanho do disco vem da pixel art
        public void Place(Vector3 center, float radius, Sprite firstFrame)
        {
            m_Center = center;
            m_Radius = radius;
            m_Sprite = null;
            m_Extent = firstFrame ? Mathf.Max(radius, firstFrame.bounds.extents.x) : radius * 1.15f;
            transform.SetPositionAndRotation(center, Quaternion.identity);
            transform.localScale = Vector3.one;

            // disco: cada vértice no chão de verdade
            m_Vertices[0] = Ground(Vector3.zero, out m_Grounded[0]);
            for (int r = 0; r < k_Rings; r++)
            {
                float d = m_Extent * (r + 1) / k_Rings;
                for (int s = 0; s < k_Sides; s++)
                {
                    float a = s * Mathf.PI * 2f / k_Sides;
                    int i = 1 + r * k_Sides + s;
                    m_Vertices[i] = Ground(new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), out m_Grounded[i]);
                }
            }

            // borda: no raio do dano, subindo do chão em faixas
            for (int s = 0; s <= k_Sides; s++)
            {
                float a = (s % k_Sides) * Mathf.PI * 2f / k_Sides;
                Vector3 foot = Ground(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), out bool grounded);
                for (int band = 0; band <= k_RimBands; band++)
                {
                    int i = m_DiskVertices + band * (k_Sides + 1) + s;
                    m_Vertices[i] = foot + Vector3.up * (k_RimHeight * band / k_RimBands);
                    m_Uvs[i] = new Vector2(0.5f, 0.5f);
                    m_Colors[i] = grounded ? new Color32(255, 60, 60, 0) : new Color32(0, 0, 0, 0);
                }
            }

            m_Mesh.vertices = m_Vertices;
            m_Mesh.RecalculateBounds();
            SetFrame(firstFrame, 0f, true);
            gameObject.SetActive(true);
        }

        // Ponto do chão abaixo de 'local' (em relação ao centro), 10 cm acima dele
        Vector3 Ground(Vector3 local, out bool grounded)
        {
            Vector3 from = m_Center + local + Vector3.up * 3f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, s_Hits, 9f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = m_Center + local;
            grounded = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_Hits[i];
                if (hit.distance >= best || Ignored(hit.collider))
                    continue;
                best = hit.distance;
                point = hit.point;
                grounded = true;
            }

            return point + Vector3.up * k_Lift - m_Center;
        }

        static bool Ignored(Collider collider)
        {
            if (collider is CharacterController)
                return true;
            if (collider.attachedRigidbody && !collider.attachedRigidbody.isKinematic)
                return true;
            return collider.GetComponentInParent<Health>() || collider.GetComponentInParent<ArenaDome>();
        }

        // Quadro da pixel art (pelo progresso do aviso), visível ou apagado (para piscar), e a borda subindo
        public void SetFrame(Sprite sprite, float progress, bool visible)
        {
            m_Renderer.enabled = visible;
            if (!visible)
                return;

            if (sprite != m_Sprite && sprite)
            {
                m_Sprite = sprite;
                // textura por marca (várias marcas ao mesmo tempo com quadros diferentes)
                m_Block ??= new MaterialPropertyBlock();
                m_Block.SetTexture(k_MainTex, sprite.texture);
                m_Renderer.SetPropertyBlock(m_Block, 0);
                Rect rect = sprite.textureRect;
                Vector2 size = new Vector2(sprite.texture.width, sprite.texture.height);
                Vector2 extents = sprite.bounds.extents;
                Vector2 pivot = (Vector2)sprite.bounds.center;
                for (int i = 0; i < m_DiskVertices; i++)
                {
                    // planta: x do mundo para a direita da imagem, z para cima
                    float u = Mathf.InverseLerp(pivot.x - extents.x, pivot.x + extents.x, m_Vertices[i].x);
                    float v = Mathf.InverseLerp(pivot.y - extents.y, pivot.y + extents.y, m_Vertices[i].z);
                    m_Uvs[i] = new Vector2((rect.x + u * rect.width) / size.x, (rect.y + v * rect.height) / size.y);
                    m_Colors[i] = m_Grounded[i] ? new Color32(255, 255, 255, 235) : new Color32(255, 255, 255, 0);
                }
            }

            // borda: as faixas acendem de baixo para cima com o aviso, cada uma mais fraca, em degraus
            float rise = Mathf.Floor(Mathf.Clamp01(progress) * 4f) / 4f;
            for (int band = 0; band <= k_RimBands; band++)
            {
                float k = band / (float)k_RimBands;
                byte alpha = (byte)(k <= rise * 0.999f + 0.001f ? Mathf.Lerp(200f, 40f, k) : 0f);
                if (band == k_RimBands)
                    alpha = 0;
                for (int s = 0; s <= k_Sides; s++)
                {
                    int i = m_DiskVertices + band * (k_Sides + 1) + s;
                    if (m_Colors[i].r == 0)
                        continue;
                    Color32 c = m_Colors[i];
                    c.a = alpha;
                    m_Colors[i] = c;
                }
            }

            m_Mesh.uv = m_Uvs;
            m_Mesh.colors32 = m_Colors;
        }

        public void Hide() => gameObject.SetActive(false);

        void OnDestroy()
        {
            if (m_Mesh)
                Destroy(m_Mesh);
        }
    }
}
