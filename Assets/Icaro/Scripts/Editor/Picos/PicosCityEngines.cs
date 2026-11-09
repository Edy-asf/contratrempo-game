using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Motores antigravidade da cidade flutuante (pedido do usuário em 2026-11-07, o 3.B do ar futurista): seis coroas
    // góticas de pedra de cabeça para baixo (PR_Motor_Antigravidade do kit AR_Tecno) presas na rocha da ilha, logo acima
    // do mar de nuvens, espaçadas em volta dela e longe da Ponte do Reino, cada uma com o cristal lilás no meio e um
    // feixe lilás fraco descendo do cristal até dentro das nuvens. Sem sorteio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        const float k_EngineScale = 1.3f;
        const float k_EngineDrop = 40f;         // do topo da ilha ao topo do colar (fica a -10 m; os pináculos acima do anel)
        const float k_EngineInset = 10f;        // do paredão ao meio da coroa, no kit (a mísula e o fundo do colar entram)
        const float k_EngineRadius = 18.6f;     // raio do colar, no kit
        const float k_EngineTip = 40f;          // do topo do colar à ponta do cristal, no kit
        const float k_CityRingInner = 359f, k_CityRingOuter = 381f;   // anel que gira sob a cidade (raio 370), com os colares

        static void BuildCityEngines(Vector3 city, Vector3 toBridge)
        {
            var root = new GameObject("Motores_Antigravidade").transform;
            root.SetParent(s_World, false);
            Material beamMaterial = EngineBeamMaterial();
            Mesh beamMesh = EngineBeamMesh(city.y - k_EngineDrop - k_EngineTip * k_EngineScale - (k_CloudY - 170f));
            float bridge = Mathf.Atan2(toBridge.z, toBridge.x);
            float y = city.y - k_EngineDrop;
            int placed = 0;
            for (int k = 0; k < 6; k++)
            {
                // A 30° da ponte e de 60 em 60 graus dali; a rocha ondula forte, então procura até 15° para os lados um
                // lugar onde o paredão não avance sobre a frente da coroa mais embaixo e o cristal e o feixe não passem
                // pelo anel de baixo (o corpo da coroa fica acima dele)
                foreach (float offset in new[] { 0f, 5f, -5f, 10f, -10f, 15f, -15f })
                {
                    float angle = bridge + (30f + 60f * k + offset) * Mathf.Deg2Rad;
                    float wall = IslandWall(city, k_CityRadius, 4101, 40, angle, y);
                    float center = wall + k_EngineInset * k_EngineScale;
                    bool bulge = false;
                    for (float drop = 6f; drop <= k_EngineTip * k_EngineScale; drop += 6f)
                        bulge |= IslandWall(city, k_CityRadius, 4101, 40, angle, y - drop) > wall + 3f;
                    if (bulge || (center > k_CityRingInner - 4f && center < k_CityRingOuter + 4f))
                        continue;
                    var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 top = new Vector3(city.x, y, city.z) + outward * center;
                    PlaceTech("PR_Motor_Antigravidade", top, YawOf(outward), root, k_EngineScale, false);

                    var beam = new GameObject("Feixe_Motor_" + k);
                    beam.transform.SetParent(root, false);
                    beam.transform.position = top + Vector3.down * (k_EngineTip * k_EngineScale - 0.5f);
                    beam.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                    var renderer = beam.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = beamMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    placed++;
                    break;
                }
            }

            if (placed < 6)
                Debug.LogWarning($"[Ícaro] Cidade flutuante: só {placed} dos 6 motores antigravidade acharam lugar na rocha.");
        }

        // Raio do paredão da ilha voadora (o mesmo contorno do SkyIsland: anéis com ondulação de Perlin) no ângulo
        // 'angle' (x = cos, z = sin) e na altura 'y', entre os vértices vizinhos
        static float IslandWall(Vector3 top, float radius, int seed, int sides, float angle, float y)
        {
            const int rings = 10;
            float depth = radius * 1.4f;
            float RingY(int i) => top.y - depth * (i / (float)rings) - (i > 0 ? 1.5f : 0f);
            float Vertex(int i, int j)
            {
                j = ((j % sides) + sides) % sides;
                float n = 1f + (Mathf.PerlinNoise(seed * 0.37f + j * 0.5f, i * 0.6f) - 0.5f) * (i == 0 ? 0.16f : 0.4f);
                return SkyIslandRadius(radius, i / (float)rings) * n;
            }

            int ring = 0;
            while (ring < rings - 1 && RingY(ring + 1) > y)
                ring++;
            float v = Mathf.Clamp01((RingY(ring) - y) / (RingY(ring) - RingY(ring + 1)));
            float u = Mathf.Repeat(angle, Mathf.PI * 2f) / (Mathf.PI * 2f / sides);
            int j0 = Mathf.FloorToInt(u);
            float fu = u - j0;
            float upper = Mathf.Lerp(Vertex(ring, j0), Vertex(ring, j0 + 1), fu);
            float lower = Mathf.Lerp(Vertex(ring + 1, j0), Vertex(ring + 1, j0 + 1), fu);
            return Mathf.Lerp(upper, lower, v);
        }

        // Feixe do motor: cone de 8 lados descendo 'length' metros do cristal, claro em cima e sumindo embaixo (a
        // transparência vem das cores dos vértices)
        static Mesh EngineBeamMesh(float length)
        {
            IcaroAssetUtility.EnsureFolder(GridSceneKit.MeshFolder);
            string path = $"{GridSceneKit.MeshFolder}/{k_Prefix}_FeixeMotor.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = k_Prefix + "_FeixeMotor" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var vertices = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var triangles = new System.Collections.Generic.List<int>();
            const int sides = 8;
            for (int i = 0; i < sides; i++)
            {
                float t = i * Mathf.PI * 2f / sides;
                var ring = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                vertices.Add(ring * 1.2f);
                vertices.Add(Vector3.down * length + ring * 8f);
                colors.Add(new Color(1f, 1f, 1f, 0.3f));
                colors.Add(new Color(1f, 1f, 1f, 0f));
            }

            for (int i = 0; i < sides; i++)
            {
                int a = i * 2, b = ((i + 1) % sides) * 2;
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Aditivo, dos dois lados, na cor da luz lilás da tecnologia dos Picos
        static Material EngineBeamMaterial()
        {
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(k_Prefix + "_FeixeMotor", null);
            material.SetColor("_BaseColor", new Color(0.8f, 0.62f, 1f, 1f));
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
