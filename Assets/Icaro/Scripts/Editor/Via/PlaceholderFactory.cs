using UnityEngine;
using UnityEngine.AI;
using BiomeId = Icaro.EditorTools.ViaLayout.BiomeId;
using PropKind = Icaro.EditorTools.ScenePropKit.PropKind;

namespace Icaro.EditorTools
{
    // Geometria provisória (primitivas) de cada peça de cenário, com as convenções do CENARIO_LEIAME.md:
    // pivô na base, frente em +Z, escala real, colliders só nas peças de cobertura.
    public static class PlaceholderFactory
    {
        public static GameObject Build(BiomeId biome, PropKind kind, BiomePalettes.Palette p)
        {
            // Detalhes do Terrain: o mesh precisa ficar na raiz
            if (kind == PropKind.Arbusto)
                return DetailMesh(PrimitiveType.Sphere, Mat(biome, "Arbusto", p.Foliage * 1.25f, default, 0.3f));
            if (kind == PropKind.BrotoLuminoso)
                return DetailMesh(PrimitiveType.Capsule, Glow(biome, p, biome == BiomeId.Cratera));

            var root = new GameObject(ScenePropKit.FileName(kind));
            switch (kind)
            {
                case PropKind.Arvore: Tree(root.transform, biome, p); break;
                case PropKind.RochaGrande: BigRock(root.transform, biome, p); break;
                case PropKind.RochaMedia: MediumRock(root.transform, biome, p); break;
                case PropKind.TroncoCaido: FallenLog(root.transform, biome, p); break;
                case PropKind.RuinaMuro: RuinWall(root.transform, biome, p); break;
                case PropKind.RuinaColuna: RuinColumn(root.transform, biome, p); break;
                case PropKind.RuinaPlataforma: RuinPlatform(root.transform, biome, p); break;
                case PropKind.CogumeloBio: Mushrooms(root.transform, biome, p, amber: false); break;
                case PropKind.CogumeloAmbar: Mushrooms(root.transform, biome, p, amber: true); break;
                case PropKind.FendaLava: LavaCrack(root.transform, biome, p); break;
                case PropKind.Portal: Portal(root.transform, biome, p); break;
                case PropKind.Barreira: Barrier(root, p); break;
                case PropKind.RuinaArco: RuinArch(root.transform, biome, p); break;
                case PropKind.RuinaBloco: RuinBlock(root.transform, biome, p); break;
                case PropKind.Poleiro: Perch(root.transform, biome, p); break;
            }

            return root;
        }

        // ---------- materiais ----------

        static Material Mat(BiomeId biome, string name, Color color, Color emission, float smoothness) =>
            IcaroMaterials.GetOrCreate($"{biome}_{name}", color, emission, smoothness);

        static Material Stone(BiomeId b, BiomePalettes.Palette p) => Mat(b, "Pedra", p.Stone, default, p.Wetness);
        static Material Wood(BiomeId b, BiomePalettes.Palette p) => Mat(b, "Madeira", p.Wood, default, p.Wetness * 0.6f);
        static Material Foliage(BiomeId b, BiomePalettes.Palette p) => Mat(b, "Folhagem", p.Foliage, p.Foliage * 0.1f, 0.3f);

        static Material Glow(BiomeId b, BiomePalettes.Palette p, bool amber) => amber
            ? Mat(b, "Bio_Ambar", p.AccentAmber * 0.25f, p.AccentAmber, 0.6f)
            : Mat(b, "Bio_Ciano", p.AccentCyan * 0.25f, p.AccentCyan, 0.6f);

        // ---------- peças ----------

        // Protótipo de árvore do Terrain: LODGroup e CapsuleCollider na raiz, partes sem collider
        static void Tree(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material trunk = Wood(biome, p);
            Material leaves = Foliage(biome, p);

            switch (biome)
            {
                case BiomeId.Charco:
                    Part(PrimitiveType.Cylinder, "Tronco", root, new Vector3(0f, 3.5f, 0f), new Vector3(0.6f, 3.5f, 0.6f), trunk, false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
                    Part(PrimitiveType.Sphere, "Copa", root, new Vector3(0.4f, 7.6f, 0f), new Vector3(6.5f, 2.4f, 6.5f), leaves, false);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.1f;
                        Part(PrimitiveType.Cube, "Musgo", root, new Vector3(Mathf.Cos(a) * 2.2f, 5.8f, Mathf.Sin(a) * 2.2f),
                            new Vector3(0.15f, 2.2f, 0.15f), leaves, false);
                    }
                    break;

                case BiomeId.Ruinas:
                    Part(PrimitiveType.Cylinder, "Tronco", root, new Vector3(0f, 4.5f, 0f), new Vector3(0.5f, 4.5f, 0.5f), trunk, false);
                    Part(PrimitiveType.Sphere, "Copa", root, new Vector3(0f, 9.6f, 0f), new Vector3(3.4f, 3.2f, 3.4f), leaves, false);
                    break;

                case BiomeId.Cratera:
                    Part(PrimitiveType.Cylinder, "Tronco", root, new Vector3(0f, 3.5f, 0f), new Vector3(0.6f, 3.5f, 0.6f), trunk, false);
                    Part(PrimitiveType.Cube, "Galho", root, new Vector3(0.8f, 5.5f, 0f), new Vector3(2.2f, 0.25f, 0.25f), trunk, false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    Part(PrimitiveType.Cube, "Galho", root, new Vector3(-0.6f, 4.6f, 0.3f), new Vector3(1.6f, 0.2f, 0.2f), trunk, false)
                        .transform.localRotation = Quaternion.Euler(0f, 30f, -40f);
                    Part(PrimitiveType.Cube, "Brasa", root, new Vector3(0f, 1.5f, 0.31f), new Vector3(0.12f, 1.4f, 0.04f), Glow(biome, p, true), false);
                    break;

                default:
                    Part(PrimitiveType.Cylinder, "Tronco", root, new Vector3(0f, 4f, 0f), new Vector3(0.7f, 4f, 0.7f), trunk, false);
                    Part(PrimitiveType.Sphere, "Copa", root, new Vector3(0f, 9f, 0f), new Vector3(5f, 4f, 5f), leaves, false);
                    Part(PrimitiveType.Sphere, "Copa2", root, new Vector3(1f, 10.6f, 0.6f), new Vector3(3.4f, 2.8f, 3.4f), leaves, false);
                    foreach (Vector3 f in new[] { new Vector3(1.6f, 7.3f, 0.5f), new Vector3(-1.2f, 7.2f, -1.1f), new Vector3(-0.4f, 7.4f, 1.6f) })
                        Part(PrimitiveType.Sphere, "Fruto", root, f, Vector3.one * 0.3f, Glow(biome, p, true), false);
                    break;
            }

            var capsule = root.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 8f;
            capsule.center = new Vector3(0f, 4f, 0f);

            var lod = root.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.015f, root.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }

        static void BigRock(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = biome == BiomeId.Cratera ? Mat(biome, "Rocha_Queimada", p.Stone * 0.7f, default, 0.2f) : Stone(biome, p);
            Part(PrimitiveType.Cube, "Bloco", root, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 3f, 5f), stone, true)
                .transform.localRotation = Quaternion.Euler(8f, 25f, 5f);
            Part(PrimitiveType.Cube, "Bloco", root, new Vector3(1.6f, 0.8f, -1.2f), new Vector3(2.5f, 2f, 3f), stone, true)
                .transform.localRotation = Quaternion.Euler(-5f, 60f, 10f);
            Part(PrimitiveType.Cube, "Bloco", root, new Vector3(-1.8f, 0.6f, 1.3f), new Vector3(2f, 1.5f, 2f), stone, true)
                .transform.localRotation = Quaternion.Euler(10f, -20f, -6f);
        }

        static void MediumRock(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Bloco", root, new Vector3(0f, 0.55f, 0f), new Vector3(2f, 1.4f, 2.4f), stone, true)
                .transform.localRotation = Quaternion.Euler(6f, 30f, -8f);
            Part(PrimitiveType.Cube, "Bloco", root, new Vector3(0.9f, 0.3f, 0.8f), new Vector3(1f, 0.8f, 1f), stone, true)
                .transform.localRotation = Quaternion.Euler(-8f, 15f, 12f);
        }

        static void FallenLog(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material wood = biome == BiomeId.Cratera ? Mat(biome, "Madeira_Queimada", p.Wood * 0.5f, default, 0.1f) : Wood(biome, p);
            Part(PrimitiveType.Cylinder, "Tronco", root, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 3f, 0.9f), wood, true)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, "Galho", root, new Vector3(1.2f, 0.9f, 0.6f), new Vector3(0.35f, 0.9f, 0.35f), wood, false)
                .transform.localRotation = Quaternion.Euler(50f, 0f, 20f);
        }

        static void RuinWall(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Muro", root, new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.8f), stone, true);
            Part(PrimitiveType.Cube, "Topo", root, new Vector3(-1.5f, 3.5f, 0f), new Vector3(2.6f, 1.1f, 0.8f), stone, true)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            // Runas ciano nas duas faces
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(0.6f, 1.8f, 0.42f), new Vector3(3.6f, 0.12f, 0.04f), Glow(biome, p, false), false);
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(-0.4f, 1.2f, -0.42f), new Vector3(2.4f, 0.12f, 0.04f), Glow(biome, p, false), false);
        }

        static void RuinColumn(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = biome == BiomeId.Cratera ? Mat(biome, "Basalto", p.Stone * 0.55f, default, 0.25f) : Stone(biome, p);
            Part(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.25f, 0f), new Vector3(1.7f, 0.5f, 1.7f), stone, true);
            Part(PrimitiveType.Cylinder, "Fuste", root, new Vector3(0f, 2.9f, 0f), new Vector3(1.1f, 2.4f, 1.1f), stone, true);
            if (biome != BiomeId.Cratera)
                Part(PrimitiveType.Cube, "Runa", root, new Vector3(0f, 2.6f, 0.56f), new Vector3(0.12f, 1.6f, 0.04f), Glow(biome, p, false), false);
        }

        // Arco: dois pilares e uma verga, vão de 4 m
        static void RuinArch(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "PilarE", root, new Vector3(-2.55f, 2.2f, 0f), new Vector3(1.05f, 4.4f, 1.1f), stone, true);
            Part(PrimitiveType.Cube, "PilarD", root, new Vector3(2.55f, 2.2f, 0f), new Vector3(1.05f, 4.4f, 1.1f), stone, true);
            Part(PrimitiveType.Cube, "Verga", root, new Vector3(0f, 5f, 0f), new Vector3(6.2f, 1.2f, 1.1f), stone, true);
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(0f, 5f, 0.57f), new Vector3(0.12f, 0.6f, 0.04f), Glow(biome, p, false), false);
        }

        // Monólito com runas nas quatro faces
        static void RuinBlock(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Monolito", root, new Vector3(0f, 1.15f, 0f), new Vector3(1.25f, 2.3f, 1.25f), stone, true);
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(0f, 1.4f, 0.64f), new Vector3(0.8f, 0.12f, 0.04f), Glow(biome, p, false), false);
        }

        // Poleiro: rocha alta de topo plano (5 m) onde o Espinhento nasce. O filho "Topo" marca o ponto de spawn;
        // a faixa ciano fica virada para o centro da arena (+Z), para o poleiro ser visto de longe.
        static void Perch(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Torre", root, new Vector3(0f, 2.3f, 0f), new Vector3(3f, 4.6f, 3f), stone, true);
            Part(PrimitiveType.Cube, "Laje", root, new Vector3(0f, 4.8f, 0f), new Vector3(3.8f, 0.4f, 3.8f), stone, true);
            Part(PrimitiveType.Cube, "Rocha", root, new Vector3(1.6f, 0.9f, 0.4f), new Vector3(1.4f, 1.8f, 1.6f), stone, true)
                .transform.localRotation = Quaternion.Euler(0f, 20f, 12f);
            Part(PrimitiveType.Cube, "Rocha", root, new Vector3(-1.2f, 0.7f, -1.3f), new Vector3(1.6f, 1.4f, 1.4f), stone, true)
                .transform.localRotation = Quaternion.Euler(8f, -30f, 0f);
            Part(PrimitiveType.Cube, "Faixa", root, new Vector3(0f, 4.8f, 1.92f), new Vector3(2.4f, 0.12f, 0.05f), Glow(biome, p, false), false);

            var top = new GameObject("Topo").transform;
            top.SetParent(root, false);
            top.localPosition = new Vector3(0f, 5f, 0f);
        }

        // Ponto elevado: plataforma de 2 m com rampa na frente (-Z → +Z)
        static void RuinPlatform(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Plataforma", root, new Vector3(0f, 1f, 0f), new Vector3(6f, 2f, 6f), stone, true);
            Ramp(root, new Vector3(0f, 0f, -8f), 5f, 2f, 3f, Mat(biome, "Rampa", p.Stone * 1.1f, default, p.Wetness));
            Part(PrimitiveType.Cube, "Parapeito", root, new Vector3(0f, 2.5f, 2.8f), new Vector3(5f, 1f, 0.4f), stone, true);
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(0f, 1.2f, 3.02f), new Vector3(3f, 0.12f, 0.04f), Glow(biome, p, false), false);
        }

        // Cacho de cogumelos bioluminescentes com uma luz Baked (sem custo em runtime). Sem collider.
        static void Mushrooms(Transform root, BiomeId biome, BiomePalettes.Palette p, bool amber)
        {
            Material cap = Glow(biome, p, amber);
            Material stem = Mat(biome, "Haste", new Color(0.55f, 0.6f, 0.55f), default, 0.4f);
            var shapes = new[]
            {
                new Vector4(0f, 0f, 0.55f, 1f), new Vector4(0.45f, 0.3f, 0.38f, 0.75f),
                new Vector4(-0.35f, 0.4f, 0.3f, 0.6f), new Vector4(0.15f, -0.45f, 0.25f, 0.5f),
            };
            foreach (Vector4 s in shapes)
            {
                // x, y = deslocamento em X e Z; z = altura do talo; w = escala do chapéu
                Part(PrimitiveType.Cylinder, "Talo", root, new Vector3(s.x, s.z * 0.5f, s.y), new Vector3(0.1f, s.z * 0.5f, 0.1f), stem, false);
                Part(PrimitiveType.Sphere, "Chapeu", root, new Vector3(s.x, s.z, s.y), new Vector3(0.5f * s.w, 0.18f * s.w, 0.5f * s.w), cap, false);
            }

            BakedLight(root, new Vector3(0f, 0.8f, 0f), amber ? p.AccentAmber : p.AccentCyan, 7f, 4f);
        }

        static void LavaCrack(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material lava = Glow(biome, p, true);
            Part(PrimitiveType.Cube, "Fenda", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.04f, 3f), lava, false)
                .transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            Part(PrimitiveType.Cube, "Fenda", root, new Vector3(0.6f, 0.02f, 1.6f), new Vector3(0.25f, 0.04f, 1.8f), lava, false)
                .transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            Part(PrimitiveType.Cube, "Fenda", root, new Vector3(-0.5f, 0.02f, -1.5f), new Vector3(0.2f, 0.04f, 1.4f), lava, false)
                .transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
            BakedLight(root, new Vector3(0f, 0.6f, 0f), p.AccentAmber, 6f, 3f);
        }

        // Pórtico de entrada de arena: vão de 12 m para a trilha de 10 m
        static void Portal(Transform root, BiomeId biome, BiomePalettes.Palette p)
        {
            Material stone = Stone(biome, p);
            Part(PrimitiveType.Cube, "Pilar", root, new Vector3(-6.75f, 3.5f, 0f), new Vector3(1.5f, 7f, 1.5f), stone, true);
            Part(PrimitiveType.Cube, "Pilar", root, new Vector3(6.75f, 3.5f, 0f), new Vector3(1.5f, 7f, 1.5f), stone, true);
            Part(PrimitiveType.Cube, "Verga", root, new Vector3(0f, 7.6f, 0f), new Vector3(15f, 1.2f, 1.5f), stone, true);
            Part(PrimitiveType.Cube, "Runa", root, new Vector3(0f, 7.6f, -0.77f), new Vector3(6f, 0.15f, 0.04f), Glow(biome, p, biome == BiomeId.Cratera), false);
        }

        // Barreira que tranca a arena: RoomDoor e NavMeshObstacle na raiz, campo de energia âmbar como filho
        static void Barrier(GameObject root, BiomePalettes.Palette p)
        {
            Material field = IcaroMaterials.GetOrCreate("Barreira_Energia", new Color(0.3f, 0.14f, 0.05f), p.AccentAmber * 0.6f, 0.8f);
            Part(PrimitiveType.Cube, "Campo", root.transform, new Vector3(0f, 3f, 0f), new Vector3(12f, 6f, 0.6f), field, true);

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 3f, 0f);
            obstacle.size = new Vector3(12f, 6f, 0.6f);
            obstacle.carving = true;

            var door = root.AddComponent<RoomDoor>();
            door.OpenDepth = 6.4f;
        }

        // Nave Dédalo (placeholder): casco deitado, asas e rampa voltada para -Z
        public static GameObject BuildShip(BiomePalettes.Palette p)
        {
            var root = new GameObject("Nave_Dedalo");
            Material hull = IcaroMaterials.GetOrCreate("Nave_Dedalo", new Color(0.9f, 0.76f, 0.32f), new Color(0.25f, 0.18f, 0.05f), 0.8f);
            Part(PrimitiveType.Capsule, "Casco", root.transform, new Vector3(0f, 2.2f, 0f), new Vector3(3f, 3.5f, 3f), hull, true)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, "Asas", root.transform, new Vector3(0f, 2.2f, 0.5f), new Vector3(11f, 0.3f, 2.5f), hull, true);
            Part(PrimitiveType.Cube, "Rampa", root.transform, new Vector3(0f, 0.55f, -4.6f), new Vector3(2.2f, 0.15f, 2.6f), hull, true)
                .transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            return root;
        }

        // ---------- utilidades ----------

        // Detalhe do Terrain: mesh e renderer na raiz, sem collider, material com instancing
        static GameObject DetailMesh(PrimitiveType type, Material material)
        {
            GameObject detail = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(detail.GetComponent<Collider>());
            detail.GetComponent<Renderer>().sharedMaterial = material;
            return detail;
        }

        // Rampa que começa no chão em 'start' e sobe 'rise' metros ao longo de 'run' metros em +Z;
        // o topo termina exatamente na altura 'rise'
        static void Ramp(Transform parent, Vector3 start, float run, float rise, float width, Material material)
        {
            float slope = Mathf.Sqrt(run * run + rise * rise);
            float angle = Mathf.Atan2(rise, run);
            const float thickness = 0.4f;
            float half = thickness * 0.5f;
            var center = start + new Vector3(0f, rise * 0.5f - half * Mathf.Cos(angle), run * 0.5f + half * Mathf.Sin(angle));

            Part(PrimitiveType.Cube, "Rampa", parent, center, new Vector3(width, thickness, slope), material, true)
                .transform.localRotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
        }

        static void BakedLight(Transform parent, Vector3 position, Color hdrColor, float range, float intensity)
        {
            var lightObject = new GameObject("LuzBio");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = hdrColor / Mathf.Max(hdrColor.maxColorComponent, 0.0001f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Baked;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider) =>
            IcaroPrefabBuilder.CreatePart(type, name, parent, position, scale, material, collider);
    }
}
