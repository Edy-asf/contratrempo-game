using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // Peças comuns dos mapas quadriculados (usadas pelo GridBiomeBuilder): malhas de grade (faces de blocos e chão),
    // sprites que olham para o jogador (SpriteBillboard), "cartas" fixas fundidas numa malha por material (árvores e
    // plantas longe do espaço andável, viradas para ele: muito mais baratas que milhares de billboards), esporos neon,
    // céu noturno, portas de energia, pontos altos do Espinhento e NavMesh por arena.
    public static class GridSceneKit
    {
        public const float Cell = 2f;
        public const float BlockHeight = 6f;
        public const string MeshFolder = IcaroAssetUtility.Root + "/Art/Grade/Malhas";

        // Centro da célula no mundo (chão)
        public static Vector3 CellCenter(float x, float z) => new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell);

        // ---------- malhas ----------

        public class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public int Count => Vertices.Count;

            // Quadrado da célula na altura y, virado para cima
            public void AddHorizontal(int x, int z, float y)
            {
                float x0 = x * Cell, x1 = x0 + Cell, z0 = z * Cell, z1 = z0 + Cell;
                Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0),
                    Vector3.up, 1f, 1f);
            }

            // Quadrilátero qualquer (a, b, c, d em volta) virado para 'normal': acerta a ordem sozinho. A textura repete
            // a cada 'tile' metros nas duas direções (a-d e a-b), mantendo o mesmo tamanho de pixel em tudo.
            public void AddQuadAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float tile = Cell)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                {
                    (a, d) = (d, a);
                    (b, c) = (c, b);
                }

                Quad(a, b, c, d, normal.normalized, Vector3.Distance(a, d) / tile, Vector3.Distance(a, b) / tile);
            }

            // Triângulo virado para 'normal', com a textura projetada no plano dele
            public void AddTriangleAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, float tile = Cell)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                    (b, c) = (c, b);
                Vector3 n = normal.normalized;
                Vector3 u = Vector3.Cross(Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up, n).normalized;
                Vector3 v = Vector3.Cross(n, u);
                int start = Vertices.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    Vertices.Add(p);
                    Normals.Add(n);
                    Uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / tile);
                }

                Triangles.Add(start);
                Triangles.Add(start + 1);
                Triangles.Add(start + 2);
            }

            // Face lateral entre duas alturas (margem: do fundo da água até o chão)
            public void AddSideRange(int x, int z, Vector2Int d, float bottom, float top)
            {
                var normal = new Vector3(d.x, 0f, d.y);
                Vector3 edge = CellCenter(x, z) + normal * (Cell * 0.5f);
                Vector3 left = -Vector3.Cross(Vector3.up, -normal) * (Cell * 0.5f);
                Vector3 a = edge + left + Vector3.up * bottom, b = edge - left + Vector3.up * bottom;
                Vector3 up = Vector3.up * (top - bottom);
                Quad(a, a + up, b + up, b, normal, 1f, (top - bottom) / Cell);
            }

            // Face lateral da célula sólida (x, z) virada para a vizinha na direção d; u cobre os 2 m da face e v a
            // altura de um bloco de 6 m (a textura tem a altura inteira)
            public void AddSide(int x, int z, Vector2Int d, float height)
            {
                var normal = new Vector3(d.x, 0f, d.y);
                Vector3 edge = CellCenter(x, z) + normal * (Cell * 0.5f);
                Vector3 left = -Vector3.Cross(Vector3.up, -normal) * (Cell * 0.5f);
                Vector3 a = edge + left, b = edge - left, up = Vector3.up * height;
                Quad(a, a + up, b + up, b, normal, 1f, height / BlockHeight);
            }

            // Carta vertical (sprite fixo) com o pé em 'foot', virada para 'facing' (horizontal, normalizado);
            // 'flip' espelha o desenho
            public void AddCard(Vector3 foot, Vector3 facing, Vector2 size, bool flip = false)
            {
                // Quem olha vem de 'facing' e enxerga a frente: a esquerda dele fica em -right
                Vector3 right = Vector3.Cross(Vector3.up, -facing).normalized * (size.x * 0.5f);
                Vector3 up = Vector3.up * size.y;
                if (flip)
                    right = -right;
                Quad(foot - right, foot - right + up, foot + right + up, foot + right, facing, 1f, 1f);
            }

            void Quad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, float uScale, float vScale)
            {
                int start = Vertices.Count;
                Vertices.Add(v0);
                Vertices.Add(v1);
                Vertices.Add(v2);
                Vertices.Add(v3);
                for (int i = 0; i < 4; i++)
                    Normals.Add(normal);
                Uvs.Add(new Vector2(0f, 0f));
                Uvs.Add(new Vector2(0f, vScale));
                Uvs.Add(new Vector2(uScale, vScale));
                Uvs.Add(new Vector2(uScale, 0f));
                Triangles.Add(start);
                Triangles.Add(start + 1);
                Triangles.Add(start + 2);
                Triangles.Add(start);
                Triangles.Add(start + 2);
                Triangles.Add(start + 3);
            }
        }

        // Objeto com a malha (salva como asset em Art/Grade/Malhas); sem material, só colisão
        // Malhas grandes (mais de k_ChunkVertices vértices) são divididas em blocos de k_ChunkSize metros, filhos do
        // objeto devolvido: assim a câmera deixa de desenhar o que está fora da vista e o DistanceCulling, o que está
        // longe. Com 'split' falso a malha fica inteira (quem põe componentes no objeto devolvido, como a água).
        public const int k_ChunkVertices = 4000;
        public const float k_ChunkSize = 64f;

        public static GameObject CreateObject(string assetName, string name, MeshData data, Material material,
            Transform parent, bool collider, bool shadows = true, bool split = true)
        {
            if (data.Count == 0)
                return null;

            if (split && data.Count > k_ChunkVertices)
            {
                Dictionary<Vector2Int, MeshData> chunks = SplitByArea(data, k_ChunkSize);
                if (chunks.Count > 1)
                {
                    var root = new GameObject(name);
                    root.transform.SetParent(parent, false);
                    foreach (KeyValuePair<Vector2Int, MeshData> chunk in chunks)
                        CreateObject($"{assetName}_{chunk.Key.x}_{chunk.Key.y}", $"{name}_{chunk.Key.x}_{chunk.Key.y}",
                            chunk.Value, material, root.transform, collider, shadows, false);
                    GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
                    return root;
                }
            }

            IcaroAssetUtility.EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{assetName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.Uvs);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (material)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (!shadows)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (collider)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            // Só o que é opaco esconde o que está atrás (oclusão); recortado (sprites, trepadeiras) e transparente não
            bool opaque = material && material.renderQueue < (int)RenderQueue.AlphaTest;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                       (opaque ? StaticEditorFlags.OccluderStatic : 0) |
                                                       StaticEditorFlags.OccludeeStatic);
            return go;
        }

        // Separa os triângulos pelo bloco (x, z) em que fica o centro de cada um
        static Dictionary<Vector2Int, MeshData> SplitByArea(MeshData data, float size)
        {
            var chunks = new Dictionary<Vector2Int, MeshData>();
            for (int i = 0; i < data.Triangles.Count; i += 3)
            {
                int a = data.Triangles[i], b = data.Triangles[i + 1], c = data.Triangles[i + 2];
                Vector3 middle = (data.Vertices[a] + data.Vertices[b] + data.Vertices[c]) / 3f;
                var key = new Vector2Int(Mathf.FloorToInt(middle.x / size), Mathf.FloorToInt(middle.z / size));
                if (!chunks.TryGetValue(key, out MeshData chunk))
                    chunks[key] = chunk = new MeshData();
                foreach (int index in new[] { a, b, c })
                {
                    chunk.Triangles.Add(chunk.Vertices.Count);
                    chunk.Vertices.Add(data.Vertices[index]);
                    chunk.Normals.Add(data.Normals[index]);
                    chunk.Uvs.Add(data.Uvs[index]);
                }
            }

            return chunks;
        }

        // Volume onde a câmera pode estar, para o cálculo de oclusão (BakeOcclusion)
        public static void AddOcclusionArea(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var area = new GameObject(name).AddComponent<OcclusionArea>();
            area.transform.SetParent(parent, false);
            area.transform.position = center;
            area.size = size;
        }

        // Calcula a oclusão da cena aberta (já salva): o que fica escondido atrás do cenário opaco de cada ponto dos
        // volumes (AddOcclusionArea) deixa de ser desenhado. Demora alguns minutos em cenas grandes.
        public static void BakeOcclusion()
        {
            StaticOcclusionCulling.smallestOccluder = 5f;
            StaticOcclusionCulling.smallestHole = 0.5f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            StaticOcclusionCulling.Compute();
        }

        // Corte por distância (DistanceCulling) de todos os renderizadores do cenário sob 'root', menos os que estão
        // em objetos (ou dentro de objetos) cujo nome contém algum de 'keep' (mar, céu, silhuetas, o que se move)
        public static DistanceCulling AddDistanceCulling(Transform root, float distance, params string[] keep)
        {
            var renderers = new List<Renderer>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool kept = false;
                for (Transform t = renderer.transform; t && t != root && !kept; t = t.parent)
                {
                    foreach (string name in keep)
                    {
                        if (t.name.Contains(name))
                        {
                            kept = true;
                            break;
                        }
                    }
                }

                if (!kept && !(renderer is ParticleSystemRenderer))
                    renderers.Add(renderer);
            }

            var culling = new GameObject("Culling_Distancia").AddComponent<DistanceCulling>();
            culling.transform.SetParent(root, false);
            culling.Distance = distance;
            culling.Renderers = renderers.ToArray();
            return culling;
        }

        // ---------- sprites ----------

        // Sprite que olha para o jogador: objeto no chão com o quad num filho (o filho gira; colisores ficam no pai)
        // Com um lote ativo (ActiveBatch), o desenho do sprite entra no lote (giro feito na placa de vídeo, uma malha
        // por bloco) e o objeto devolvido fica só com a posição (e o que o chamador puser nele, como o colisor do
        // tronco); sem lote, o sprite é um objeto que o SpriteBillboardSystem gira
        public static BillboardBatch ActiveBatch;

        public static GameObject Billboard(string name, Mesh quad, Material material, Vector2 size, Vector3 position,
            Transform parent, bool flip = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            if (ActiveBatch != null)
            {
                ActiveBatch.Add(material, root.transform.position, size, flip);
                return root;
            }

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(flip ? -size.x : size.x, size.y, 1f);
            visual.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            visual.AddComponent<SpriteBillboard>();
            return root;
        }

        // Cartas fixas agrupadas por material: uma malha (e uma chamada de desenho) por material
        public class Cards
        {
            readonly Dictionary<Material, MeshData> m_ByMaterial = new Dictionary<Material, MeshData>();

            public int Count { get; private set; }

            public void Add(Material material, Vector3 foot, Vector3 facing, Vector2 size, bool flip = false)
            {
                if (!m_ByMaterial.TryGetValue(material, out MeshData data))
                    m_ByMaterial[material] = data = new MeshData();
                data.AddCard(foot, facing, size, flip);
                Count++;
            }

            public void Build(string prefix, Transform parent)
            {
                foreach (KeyValuePair<Material, MeshData> entry in m_ByMaterial)
                    CreateObject($"{prefix}_{entry.Key.name}", "Cartas_" + entry.Key.name, entry.Value, entry.Key,
                        parent, false, false);
            }
        }

        // Sprites que olham para o jogador, juntados por material e por bloco de 64 m numa malha só (shader
        // Icaro/BillboardSprite: cada vértice guarda o pé do sprite e o canto, e a placa de vídeo gira). As posições
        // ficam no espaço do objeto raiz do lote, para o lote andar junto se o mundo for deslocado.
        public class BillboardBatch
        {
            readonly Transform m_Root;
            readonly Dictionary<Material, List<(Vector3 pivot, Vector2 size, bool flip)>> m_Sprites =
                new Dictionary<Material, List<(Vector3, Vector2, bool)>>();

            public BillboardBatch(Transform root) => m_Root = root;

            public int Count { get; private set; }

            public void Add(Material material, Vector3 world, Vector2 size, bool flip)
            {
                if (!material)
                    return;
                if (!m_Sprites.TryGetValue(material, out var list))
                    m_Sprites[material] = list = new List<(Vector3, Vector2, bool)>();
                list.Add((m_Root.InverseTransformPoint(world), size, flip));
                Count++;
            }

            public void Build(string prefix)
            {
                var holder = new GameObject("Sprites").transform;
                holder.SetParent(m_Root, false);
                foreach (KeyValuePair<Material, List<(Vector3 pivot, Vector2 size, bool flip)>> entry in m_Sprites)
                {
                    Material material = GridTextureKit.BillboardMaterial(entry.Key);
                    var chunks = new Dictionary<Vector2Int, List<(Vector3 pivot, Vector2 size, bool flip)>>();
                    foreach (var sprite in entry.Value)
                    {
                        var key = new Vector2Int(Mathf.FloorToInt(sprite.pivot.x / k_ChunkSize),
                            Mathf.FloorToInt(sprite.pivot.z / k_ChunkSize));
                        if (!chunks.TryGetValue(key, out var list))
                            chunks[key] = list = new List<(Vector3, Vector2, bool)>();
                        list.Add(sprite);
                    }

                    foreach (var chunk in chunks)
                        BuildChunk($"{prefix}_{entry.Key.name}_{chunk.Key.x}_{chunk.Key.y}", chunk.Value, material,
                            holder);
                }
            }

            static void BuildChunk(string assetName, List<(Vector3 pivot, Vector2 size, bool flip)> sprites,
                Material material, Transform parent)
            {
                IcaroAssetUtility.EnsureFolder(MeshFolder);
                string path = $"{MeshFolder}/{assetName}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh)
                {
                    mesh = new Mesh { name = assetName };
                    AssetDatabase.CreateAsset(mesh, path);
                }

                var vertices = new List<Vector3>(sprites.Count * 4);
                var uvs = new List<Vector2>(sprites.Count * 4);
                var corners = new List<Vector2>(sprites.Count * 4);
                var pivots = new List<Vector3>(sprites.Count * 4);
                var triangles = new List<int>(sprites.Count * 6);
                var bounds = new Bounds(sprites[0].pivot, Vector3.zero);
                Vector2[] quad =
                {
                    new Vector2(-0.5f, 0f), new Vector2(-0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                };
                Vector2[] quadUv = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                foreach ((Vector3 pivot, Vector2 size, bool flip) in sprites)
                {
                    int start = vertices.Count;
                    float sx = flip ? -size.x : size.x;
                    for (int i = 0; i < 4; i++)
                    {
                        var corner = new Vector2(quad[i].x * sx, quad[i].y * size.y);
                        vertices.Add(pivot + new Vector3(corner.x, corner.y, 0f));
                        uvs.Add(quadUv[i]);
                        corners.Add(corner);
                        pivots.Add(pivot);
                    }

                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    // O sprite gira em volta do pé: o volume cobre o giro inteiro
                    float width = Mathf.Abs(size.x);
                    bounds.Encapsulate(new Bounds(pivot + Vector3.up * (size.y * 0.5f),
                        new Vector3(width, size.y, width)));
                }

                mesh.Clear();
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, corners);
                mesh.SetUVs(2, pivots);
                mesh.SetTriangles(triangles, 0);
                mesh.bounds = bounds;
                EditorUtility.SetDirty(mesh);

                var go = new GameObject(assetName);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            }
        }

        // Quad de 1 x 1 com o pivô no centro de baixo e a frente para -Z (como o Quad do Unity)
        public static Mesh SpriteQuad()
        {
            IcaroAssetUtility.EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/Sprite_Quad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh)
                return mesh;

            mesh = new Mesh { name = "Sprite_Quad" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
                new Vector3(0.5f, 0f, 0f),
            });
            mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Partículas quadradas e aditivas (pixel), cores neon sorteadas, flutuando devagar com ruído. Nome com
        // "Esporos_" para o TimeAtmosphere acelerar e pulsar com o multiplicador.
        public static void AddSpores(string name, Material material, Vector3 center, Vector3 size, int max, float rate,
            Vector2 particleSize, Color[] colors, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
            var keys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                keys[i] = new GradientColorKey(colors[i], colors.Length > 1 ? i / (colors.Length - 1f) : 0f);
            var gradient = new Gradient();
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            shape.randomDirectionAmount = 1f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            ParticleSystem.ColorOverLifetimeModule overLifetime = particles.colorOverLifetime;
            overLifetime.enabled = true;
            overLifetime.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sharedMaterial = material;
            particles.Play();
        }

        // ---------- noite ----------

        // Material do céu do tema (Ceu_<Bioma>), com as cores e o planeta dele
        public static Material NightSkyMaterial(GridTheme theme)
        {
            string path = $"{GridTextureKit.MaterialsFolder}/Ceu_{theme.Biome}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(IcaroAssetUtility.Root + "/Shaders/NightSky.shader");
                material = new Material(shader) { name = "Ceu_" + theme.Biome };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_StarDensity", theme.StarDensity);
            material.SetFloat("_AuroraStrength", theme.AuroraStrength);
            material.SetColor("_AuroraColorA", theme.AuroraLow);
            material.SetColor("_AuroraColorB", theme.AuroraHigh);
            material.SetFloat("_AuroraHeight", theme.AuroraHeight);
            material.SetFloat("_HorizonFog", 0.2f);
            material.SetColor("_HorizonTint", theme.SkyBlue);
            material.SetColor("_MidColor", theme.SkyPurple);
            material.SetFloat("_GradientBlue", theme.SkyBlueHeight);
            material.SetFloat("_GradientTop", theme.SkyTop);
            material.SetVector("_PlanetDir", theme.PlanetDirection);
            material.SetFloat("_PlanetSize", theme.PlanetSize);
            material.SetColor("_PlanetColorA", theme.PlanetA);
            material.SetColor("_PlanetColorB", theme.PlanetB);
            material.SetColor("_RingColor", theme.Ring);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Céu, névoa, ambiente e luar da noite do tema, mais o giro das estrelas (NightSkyMotion)
        public static void ApplyNight(GridTheme theme, Material sky, Transform systems)
        {
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = theme.FogColor;
            RenderSettings.fogDensity = theme.FogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
            if (RenderSettings.sun)
            {
                RenderSettings.sun.color = theme.MoonColor;
                RenderSettings.sun.intensity = theme.MoonIntensity;
                RenderSettings.sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

                // A luz do Microgame traz uma malha de sol emissiva (Mesh_Sun) que segue o jogador: com o Bloom, vira
                // um clarão no céu na direção da luz. À noite não tem sol.
                foreach (Transform child in RenderSettings.sun.transform.root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.StartsWith("Mesh_Sun"))
                        child.gameObject.SetActive(false);
                }
            }

            var motion = new GameObject("CeuNoturno").AddComponent<NightSkyMotion>();
            motion.transform.SetParent(systems, false);
        }

        // Gatilho de bioma com a atmosfera da noite do tema (senão ele voltaria para a do dia)
        public static BiomeZone AddBiomeZone(GridTheme theme, Vector3 position, Vector3 size, Transform parent)
        {
            var zoneObject = new GameObject("Bioma_" + theme.Biome);
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = position;

            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            var zone = zoneObject.AddComponent<BiomeZone>();
            zone.BiomeName = theme.Biome;
            zone.FogColor = theme.FogColor;
            zone.FogDensity = theme.FogDensity;
            zone.AmbientSky = theme.AmbientSky;
            zone.AmbientEquator = theme.AmbientEquator;
            zone.AmbientGround = theme.AmbientGround;
            zone.SunColor = theme.MoonColor;
            zone.SunIntensity = theme.MoonIntensity;
            return zone;
        }

        // ---------- arena ----------

        // Porta de energia de 6 m x 6 m no vão de 3 células: sobe para trancar e afunda no chão para abrir (RoomDoor)
        public static RoomDoor CreateDoor(string name, Vector2Int cell, Material material, Transform parent)
        {
            var door = new GameObject(name);
            door.transform.SetParent(parent, false);
            door.transform.position = CellCenter(cell.x, cell.y);

            var box = door.AddComponent<BoxCollider>();
            box.size = new Vector3(3f * Cell, BlockHeight, 1f);
            box.center = new Vector3(0f, BlockHeight * 0.5f, 0f);

            var obstacle = door.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = box.size;
            obstacle.center = box.center;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(door.transform, false);
            visual.transform.localPosition = box.center;
            visual.transform.localScale = box.size;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            material.SetTextureScale("_BaseMap", new Vector2(3f, 1f));

            var roomDoor = door.AddComponent<RoomDoor>();
            roomDoor.OpenDepth = BlockHeight + 0.2f;
            return roomDoor;
        }

        // Ponto alto do Espinhento no topo de um poleiro de 2 x 2 células, validado pela linha de visão
        public static HighSpawnPoint HighPoint(Vector2Int perch, Transform group, Vector3 center, Vector3 entrance,
            int index)
        {
            var point = new GameObject($"PontoAlto_{index:00}").AddComponent<HighSpawnPoint>();
            point.transform.SetParent(group, false);
            point.transform.position = CellCenter(perch.x + 0.5f, perch.y + 0.5f) + Vector3.up * BlockHeight;
            point.ArenaFloorHeight = 0f;
            point.ArenaCenter = center;
            point.ArenaEntrance = entrance;
            return point;
        }

        public static void BakeNavMesh(NavMeshSurface surface, string folder, string name)
        {
            IcaroAssetUtility.EnsureFolder(folder);
            string path = $"{folder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path))
                AssetDatabase.DeleteAsset(path);
            surface.BuildNavMesh();
            if (surface.navMeshData)
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            else
                Debug.LogError("[Ícaro] NavMesh vazio em " + name);
        }
    }
}
