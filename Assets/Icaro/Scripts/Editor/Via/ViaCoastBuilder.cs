using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // "Ícaro > Via > Gerar Bosque e Arquipélago": a Via numa cena só (Scenes/Via_Grade.unity): o Bosque
    // (BosqueBuilder, com a saída do chefe aberta para o norte), o desfiladeiro que desce dele até a costa e o
    // Arquipélago (ArquipelagoBuilder.BuildInto), com o alto do penhasco encostado no fim do desfiladeiro.
    // Desfiladeiro: ~220 m em S, descendo 6 m, entre paredões de rocha (a pedra roxa do Bosque no começo, a do penhasco
    // do Arquipélago depois, misturadas em manchas com a cor passando aos poucos de uma para a outra); o chão é a
    // trilha do Bosque, depois rocha e, no fim, areia, com texturas que misturam as duas em vários passos; as primeiras ruínas (colunas, tambores
    // caídos, um arco) aparecem na segunda metade; trepadeiras penduradas nos paredões. Atrás dos paredões (duas
    // fileiras desencontradas), um terreno baixo de capim (50 m para cada lado), escondido de quem olha do mar. No fim ele se abre
    // na praça do penhasco, de frente para o mar. A névoa, a luz e o céu passam de um bioma para o outro ao longo dele,
    // bem mais densos no meio (AtmosphereCorridor). Rodar de novo refaz a cena.
    public static class ViaCoastBuilder
    {
        const string k_SceneName = "Via_Grade";
        const float k_Length = 220f;
        const float k_Drop = 6f;
        const float k_Bend = 22f;
        const int k_Samples = 56;
        const float k_FloorMargin = 7f;   // o chão continua por baixo dos paredões

        [MenuItem("Ícaro/Via/Gerar Bosque e Arquipélago", priority = 44)]
        static void Generate()
        {
            // Pergunta antes de apagar qualquer coisa (o BosqueBuilder não pergunta de novo: já está tudo salvo)
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            DeleteOldBosque();
            BosqueBuilder.Generate(k_SceneName, "Via_Bosque", true,
                (scene, world) => Extend(BosqueBuilder.Theme, world));
        }

        // O Bosque antigo (em grade) saiu: apaga a cena própria dele, o NavMesh dela e as malhas que ele deixou (as da
        // Via são todas refeitas na geração)
        static void DeleteOldBosque()
        {
            string scenes = IcaroAssetUtility.ScenesFolder;
            foreach (string path in new[] { $"{scenes}/Bosque_Grade.unity", $"{scenes}/Bosque_Grade_NavMesh" })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) || AssetDatabase.IsValidFolder(path))
                    AssetDatabase.DeleteAsset(path);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { GridSceneKit.MeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileName(path);
                if (file.StartsWith("Bosque_Grade_") || file.StartsWith("Via_Grade_"))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        // Centro do caminho em t (0 a 1): curva em S (sem virar nas pontas), descendo suave
        static Vector3 PathAt(Vector3 start, float t) => start + new Vector3(
            k_Bend * Mathf.Sin(2f * Mathf.PI * t) * Mathf.Sin(Mathf.PI * t), -k_Drop * Mathf.SmoothStep(0f, 1f, t),
            k_Length * t);

        // Largura: 6 m (a trilha do Bosque), 14 m no desfiladeiro e 40 m no fim (a praça do penhasco)
        static float WidthAt(float t) => 6f + 8f * Smooth(0f, 0.2f, t) + 26f * Smooth(0.88f, 1f, t);

        static void Extend(GridTheme bosque, Transform world)
        {
            Vector3 start = BosqueBuilder.OpenEndPoint;
            Vector3 end = PathAt(start, 1f);
            string navMeshFolder = $"{IcaroAssetUtility.ScenesFolder}/{k_SceneName}_Arquipelago_NavMesh";
            ArquipelagoTextureKit.Set coast = ArquipelagoBuilder.BuildInto(world, end, navMeshFolder,
                out string report);

            var canyon = new GameObject("Desfiladeiro").transform;
            canyon.SetParent(world, false);
            BuildCanyon(start, bosque.Materials, coast, canyon);
            BuildForestWalls(start, bosque.Materials, coast, canyon);

            var transition = new GameObject("Transicao_Atmosfera").AddComponent<AtmosphereCorridor>();
            transition.transform.SetParent(canyon, false);
            transition.From = start;
            transition.To = end;
            Fill(transition.A, bosque);
            Vector3 middle = (start + end) * 0.5f;
            GridSceneKit.AddOcclusionArea("Oclusao_Desfiladeiro", canyon, middle + Vector3.up * 10f,
                new Vector3(Mathf.Abs(end.x - start.x) + 2f * k_Bend + 60f, 40f, k_Length + 10f));
            Fill(transition.B, ArquipelagoBuilder.Theme());
            Debug.Log($"[Ícaro] Via: Bosque, desfiladeiro ({k_Length:0} m) e Arquipélago: {report}.");
        }

        static void Fill(AtmosphereCorridor.Atmosphere atmosphere, GridTheme theme)
        {
            atmosphere.FogColor = theme.FogColor;
            atmosphere.FogDensity = theme.FogDensity;
            atmosphere.AmbientSky = theme.AmbientSky;
            atmosphere.AmbientEquator = theme.AmbientEquator;
            atmosphere.AmbientGround = theme.AmbientGround;
            atmosphere.MoonColor = theme.MoonColor;
            atmosphere.MoonIntensity = theme.MoonIntensity;
            atmosphere.Sky = GridSceneKit.NightSkyMaterial(theme);
        }

        // Transições entre os materiais dos dois biomas: cada peça sorteia um passo entre o material de um e o do outro
        // pela posição no caminho, com ruído (manchas irregulares, sem linha de corte). Os passos do meio levam a cor
        // de um para o outro, e a textura só troca onde as cores já estão iguais.
        const int k_BlendSteps = 8;

        static Material[] BlendMaterials(string name, Material from, Material to)
        {
            Color a = GridTextureKit.AverageColor(from), b = GridTextureKit.AverageColor(to);
            var steps = new Material[k_BlendSteps];
            for (int s = 0; s < k_BlendSteps; s++)
            {
                float f = s / (k_BlendSteps - 1f);
                Color target = Color.Lerp(a, b, f);
                bool first = f < 0.5f;
                Color source = first ? a : b;
                var tint = new Color(Ratio(target.r, source.r), Ratio(target.g, source.g), Ratio(target.b, source.b));
                steps[s] = s == 0 ? from : s == k_BlendSteps - 1 ? to
                    : GridTextureKit.Tinted($"{k_SceneName}_Transicao_{name}_{s}", first ? from : to, tint);
            }

            return steps;
        }

        // Passos de textura misturada (GridTextureKit.Blend): manchas da segunda textura crescendo sobre a primeira
        static Material[] TextureBlend(string name, Material from, Material to, int seed)
        {
            var steps = new Material[k_BlendSteps];
            for (int s = 0; s < k_BlendSteps; s++)
                steps[s] = s == 0 ? from : s == k_BlendSteps - 1 ? to
                    : GridTextureKit.Blend($"{k_SceneName}_Mistura_{name}_{s}", from, to, s / (k_BlendSteps - 1f),
                        seed);
            return steps;
        }

        static float Ratio(float target, float source) => Mathf.Clamp(target / Mathf.Max(source, 0.01f), 0f, 3f);

        // Manchas de ~15 m (0 a 1)
        static float Patches(Vector3 position, float seed) =>
            Mathf.PerlinNoise(position.x * 0.065f + seed, position.z * 0.065f + seed * 0.7f);

        // Passo da transição num ponto: 'blend' (0 = primeiro material, 1 = segundo) com as manchas e um pouco de
        // variação por peça, as duas só no meio da transição (nas pontas fica o material puro)
        static int Step(float blend, Vector3 position, float seed, float jitter)
        {
            float spread = Mathf.Clamp01(blend * (1f - blend) * 6f);
            float value = Mathf.Clamp01(blend + ((Patches(position, seed) - 0.5f) * 0.6f + jitter) * spread);
            return Mathf.RoundToInt(value * (k_BlendSteps - 1));
        }

        // Quadrilátero do chão com a textura projetada no mundo (os pedaços vizinhos não mostram emenda)
        static void FloorQuad(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            mesh.AddTriangleAuto(a, b, c, normal);
            mesh.AddTriangleAuto(a, c, d, normal);
        }

        static void BuildCanyon(Vector3 start, GridTextureKit.Set forest, ArquipelagoTextureKit.Set coast,
            Transform parent)
        {
            var random = new System.Random(2024);
            var points = new Vector3[k_Samples];
            var normals = new Vector3[k_Samples];
            var widths = new float[k_Samples];
            for (int i = 0; i < k_Samples; i++)
            {
                float t = i / (k_Samples - 1f);
                points[i] = PathAt(start, t);
                widths[i] = WidthAt(t);
            }

            for (int i = 0; i < k_Samples; i++)
            {
                Vector3 tangent = points[Mathf.Min(i + 1, k_Samples - 1)] - points[Mathf.Max(i - 1, 0)];
                tangent.y = 0f;
                normals[i] = Vector3.Cross(Vector3.up, tangent.normalized);
            }

            // Nas pontas o caminho segue reto para o norte: a beira do chão fica reta e encosta no Bosque e na praça
            normals[0] = normals[k_Samples - 1] = Vector3.right;

            // Materiais da transição: chão (trilha → rocha → areia), paredões (pedra do Bosque → do penhasco) e o
            // capim do planalto
            Material[] trailToRock = TextureBlend("Trilha_Rocha", forest.Path, coast.Cliff, 71);
            Material[] rockToSand = TextureBlend("Rocha_Areia", coast.Cliff, coast.Sand, 72);
            Material[] wallSteps = BlendMaterials("Paredao", forest.Rock, coast.Cliff);
            Material[] grassSteps = TextureBlend("Capim", forest.Grass, coast.Grass, 73);
            float WallBlend(float t) => Smooth(0.18f, 0.6f, t);

            // Chão: trilha do Bosque, rocha e areia, em pedaços de ~2 m que trocam de material em manchas
            var floorMeshes = new Dictionary<Material, MeshData>();
            MeshData FloorMesh(Material material)
            {
                if (!floorMeshes.TryGetValue(material, out MeshData mesh))
                    floorMeshes[material] = mesh = new MeshData();
                return mesh;
            }

            for (int i = 0; i < k_Samples - 1; i++)
            {
                float t = (i + 0.5f) / (k_Samples - 1f);
                float w0 = widths[i] * 0.5f + k_FloorMargin, w1 = widths[i + 1] * 0.5f + k_FloorMargin;
                int slices = Mathf.CeilToInt(Mathf.Max(w0, w1) * 2f / 2f);
                for (int j = 0; j < slices; j++)
                {
                    float u0 = j / (float)slices * 2f - 1f, u1 = (j + 1f) / slices * 2f - 1f;
                    Vector3 a = points[i] + normals[i] * (w0 * u0), b = points[i + 1] + normals[i + 1] * (w1 * u0);
                    Vector3 c = points[i + 1] + normals[i + 1] * (w1 * u1), d = points[i] + normals[i] * (w0 * u1);
                    Vector3 middle = (a + b + c + d) * 0.25f;
                    Material material = t < 0.55f
                        ? trailToRock[Step(Smooth(0.18f, 0.42f, t), middle, 3f, 0f)]
                        : rockToSand[Step(Smooth(0.66f, 0.86f, t), middle, 9f, 0f)];
                    FloorQuad(FloorMesh(material), a, b, c, d, Vector3.up);
                }
            }

            int floorIndex = 0;
            foreach (KeyValuePair<Material, MeshData> floor in floorMeshes)
                GridSceneKit.CreateObject($"{k_SceneName}_Desfiladeiro_Chao_{floorIndex++}", "Chao", floor.Value,
                    floor.Key, parent, true);

            // Planalto dos dois lados, atrás dos paredões: o desfiladeiro fica cavado num terreno (de cima não se vê
            // o vazio). A altura acompanha os paredões (que passam dele) e desce até a praça do penhasco no fim.
            BuildPlateau(points, normals, widths, grassSteps, wallSteps, WallBlend, parent);

            // Paredões: pilhas de rocha dos dois lados (a pedra do Bosque no começo, a do penhasco depois, misturadas
            // em manchas no meio) com trepadeiras penduradas nas faces viradas para o caminho, e pedras soltas na beira.
            // Uma segunda fileira atrás, desencontrada da primeira, fecha os vãos entre as pilhas (até a praça).
            var wallMeshes = new RuinMesh[k_BlendSteps];
            for (int s = 0; s < k_BlendSteps; s++)
                wallMeshes[s] = new RuinMesh(31 + s, 1);
            var ruins = new RuinMesh(33);
            ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
            ArquipelagoRuinKit.VineMaterial = coast.Vine;
            ArquipelagoRuinKit.VineSize = coast.VineSize;
            for (int i = 0; i < k_Samples; i++)
            {
                float t = i / (k_Samples - 1f);
                float height = Mathf.Lerp(12f, 26f, Smooth(0f, 0.15f, t)) - 6f * Smooth(0.85f, 1f, t);
                foreach (float side in new[] { -1f, 1f })
                {
                    float offset = widths[i] * 0.5f + 3.5f + (float)random.NextDouble() * 1.5f;
                    Vector3 foot = points[i] + normals[i] * side * offset;
                    RockPile(wallMeshes, WallBlend(t), foot, height * (0.85f + (float)random.NextDouble() * 0.3f),
                        -normals[i] * side, random);
                    if (i < k_Samples - 1)
                    {
                        Vector3 back = (points[i] + points[i + 1]) * 0.5f + (normals[i] + normals[i + 1]).normalized *
                            side * ((widths[i] + widths[i + 1]) * 0.25f + 8f + (float)random.NextDouble() * 1.5f);
                        RockPile(wallMeshes, WallBlend(t), back, height * (0.8f + (float)random.NextDouble() * 0.25f),
                            -normals[i] * side, random);
                    }

                    if (random.NextDouble() < 0.45)
                    {
                        float size = 0.8f + (float)random.NextDouble() * 1.1f;
                        Vector3 stone = points[i] + normals[i] * side * (widths[i] * 0.5f - 0.6f) +
                                        Vector3.up * (size * 0.35f);
                        BlockMesh.ChamferBox(wallMeshes[Step(WallBlend(t), stone, 5f, 0f)].Pick(), stone,
                            Vector3.one * size, Quaternion.Euler((float)random.NextDouble() * 30f,
                                (float)random.NextDouble() * 360f, (float)random.NextDouble() * 30f), size * 0.2f);
                    }
                }
            }

            // Primeiras ruínas: colunas partidas junto aos paredões, tambores caídos e um arco atravessando o caminho
            foreach (float t in new[] { 0.48f, 0.56f, 0.78f, 0.86f })
            {
                int i = Mathf.RoundToInt(t * (k_Samples - 1));
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 spot = points[i] + normals[i] * side * (widths[i] * 0.5f - 1.6f);
                ArquipelagoRuinKit.Column(ruins, spot, 2.5f + (float)random.NextDouble() * 3.5f, 0.8f, random, true);
            }

            int drums = Mathf.RoundToInt(0.62f * (k_Samples - 1));
            ArquipelagoRuinKit.FallenDrums(ruins, points[drums] + normals[drums] * (widths[drums] * 0.3f),
                Quaternion.LookRotation(normals[drums]), 0.9f, random);
            int arch = Mathf.RoundToInt(0.7f * (k_Samples - 1));
            float archScale = widths[arch] / 46f;
            ArquipelagoRuinKit.Arch(ruins, points[arch], Quaternion.LookRotation(Vector3.Cross(normals[arch],
                Vector3.up)).eulerAngles.y, archScale, random);

            for (int s = 0; s < k_BlendSteps; s++)
                ArquipelagoBuilder.CreateRuin($"Desfiladeiro_Rocha_{s}", wallMeshes[s], new[] { wallSteps[s] }, parent,
                    true, -100f);
            ArquipelagoBuilder.CreateRuin("Desfiladeiro_Ruinas", ruins, coast.StoneTones, parent, true, -100f);
            ArquipelagoRuinKit.Vines.Build(k_SceneName + "_Desfiladeiro_Trepadeiras", parent);
            ArquipelagoRuinKit.Vines = null;
        }

        // Paredão de rocha do Bosque onde a floresta acaba, na borda norte do mapa do Bosque, dos dois lados da boca do
        // desfiladeiro ('mouth' = começo do desfiladeiro), até onde começa a parede preta do fundo (60 m para cada
        // lado): duas fileiras desencontradas de pilhas de rocha com trepadeiras viradas para a floresta, emendando
        // nos paredões do desfiladeiro. Fecha a vista entre o fim das árvores e o desfiladeiro.
        static void BuildForestWalls(Vector3 mouth, GridTextureKit.Set forest, ArquipelagoTextureKit.Set coast,
            Transform parent)
        {
            var random = new System.Random(3030);
            var mesh = new RuinMesh(34, 1);
            var steps = new RuinMesh[k_BlendSteps];
            for (int s = 0; s < k_BlendSteps; s++)
                steps[s] = mesh;
            ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
            ArquipelagoRuinKit.VineMaterial = coast.Vine;
            ArquipelagoRuinKit.VineSize = coast.VineSize;
            foreach (float side in new[] { -1f, 1f })
            {
                for (float d = 7f; d < 66f; d += 4.5f)
                {
                    float height = Mathf.Lerp(12f, 20f, Mathf.InverseLerp(7f, 30f, d)) *
                                   (0.85f + (float)random.NextDouble() * 0.3f);
                    Vector3 foot = mouth + Vector3.right * side * d +
                                   Vector3.forward * (2.5f + (float)random.NextDouble() * 1.5f);
                    RockPile(steps, 0f, foot, height, Vector3.back, random);
                    Vector3 back = mouth + Vector3.right * side * (d + 2.2f) +
                                   Vector3.forward * (7.5f + (float)random.NextDouble() * 1.5f);
                    RockPile(steps, 0f, back, height * (0.85f + (float)random.NextDouble() * 0.2f), Vector3.back,
                        random);
                }
            }

            ArquipelagoBuilder.CreateRuin("Trilha_Bosque_Rocha", mesh, new[] { forest.Rock }, parent, true, -100f);
            ArquipelagoRuinKit.Vines.Build(k_SceneName + "_Trilha_Bosque_Trepadeiras", parent);
            ArquipelagoRuinKit.Vines = null;
        }

        // Terreno dos dois lados do desfiladeiro: topo de capim (do Bosque para o da costa, em texturas misturadas) da
        // beira de dentro (atrás dos paredões, onde o chão do desfiladeiro termina) até 50 m para fora, com a face de
        // dentro e a de fora em rocha e uma tampa no começo (do lado do Bosque). No fim ele desce até a altura da praça.
        static void BuildPlateau(Vector3[] points, Vector3[] normals, float[] widths, Material[] grassSteps,
            Material[] rockSteps, System.Func<float, float> rockBlend, Transform parent)
        {
            const float reach = 50f;
            const int lanes = 10;
            var meshes = new Dictionary<Material, MeshData>();
            MeshData Mesh(Material material)
            {
                if (!meshes.TryGetValue(material, out MeshData mesh))
                    meshes[material] = mesh = new MeshData();
                return mesh;
            }

            // Baixo (2 m acima do chão do desfiladeiro): fica escondido atrás dos paredões e, visto do mar, atrás da
            // beira do penhasco
            float HeightAt(int i)
            {
                float t = i / (k_Samples - 1f);
                return 2f * (1f - Smooth(0.9f, 1f, t));
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 Edge(int i, float d) => points[i] + normals[i] * side * (widths[i] * 0.5f + d) +
                                                Vector3.up * HeightAt(i);
                for (int i = 0; i < k_Samples - 1; i++)
                {
                    float t = (i + 0.5f) / (k_Samples - 1f);
                    float inner = k_FloorMargin;
                    for (int l = 0; l < lanes; l++)
                    {
                        float d0 = inner + (reach - inner) * l / lanes, d1 = inner + (reach - inner) * (l + 1) / lanes;
                        Vector3 a = Edge(i, d0), b = Edge(i + 1, d0), c = Edge(i + 1, d1), d = Edge(i, d1);
                        Vector3 middle = (a + b + c + d) * 0.25f;
                        FloorQuad(Mesh(grassSteps[Step(rockBlend(t), middle, 13f, 0f)]), a, b, c, d, Vector3.up);
                    }

                    // Face de dentro (atrás dos paredões) até o chão do desfiladeiro e face de fora até 10 m abaixo
                    Material rock = rockSteps[Step(rockBlend(t), Edge(i, inner), 5f, 0f)];
                    Vector3 i0 = Edge(i, inner), i1 = Edge(i + 1, inner);
                    if (HeightAt(i) > 0.01f || HeightAt(i + 1) > 0.01f)
                        FloorQuad(Mesh(rock), i0, i1, new Vector3(i1.x, points[i + 1].y - 1f, i1.z),
                            new Vector3(i0.x, points[i].y - 1f, i0.z), -normals[i] * side);
                    Vector3 o0 = Edge(i, reach), o1 = Edge(i + 1, reach);
                    FloorQuad(Mesh(rock), o0, o1, new Vector3(o1.x, points[i + 1].y - 10f, o1.z),
                        new Vector3(o0.x, points[i].y - 10f, o0.z), normals[i] * side);
                }

                // Tampa do começo, virada para o Bosque
                Vector3 s0 = Edge(0, k_FloorMargin), s1 = Edge(0, reach);
                FloorQuad(Mesh(rockSteps[0]), s0, s1, new Vector3(s1.x, points[0].y - 10f, s1.z),
                    new Vector3(s0.x, points[0].y - 1f, s0.z), -Vector3.forward);
            }

            int index = 0;
            foreach (KeyValuePair<Material, MeshData> part in meshes)
                GridSceneKit.CreateObject($"{k_SceneName}_Desfiladeiro_Planalto_{index++}", "Planalto", part.Value,
                    part.Key, parent, true);
        }

        // Pilha de rocha: dois ou três blocos que afinam para cima, um pouco tortos, do chão ('foot') até 'height'
        // acima dele. Cada bloco sorteia o passo da transição ('blend' + manchas + um pouco de variação) e ganha
        // trepadeiras nas faces viradas para 'view'.
        static void RockPile(RuinMesh[] steps, float blend, Vector3 foot, float height, Vector3 view,
            System.Random random)
        {
            float width = 6f + (float)random.NextDouble() * 3f;
            float y = foot.y - 3f;
            int parts = 2 + random.Next(2);
            for (int i = 0; i < parts; i++)
            {
                float h = (height + 3f) / parts * (0.8f + (float)random.NextDouble() * 0.4f);
                Quaternion tilt = Quaternion.Euler((float)random.NextDouble() * 6f - 3f,
                    (float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 6f - 3f);
                var center = new Vector3(foot.x, y + h * 0.5f, foot.z);
                var footprint = new Vector2(width, width * (0.8f + (float)random.NextDouble() * 0.3f));
                RuinMesh mesh = steps[Step(blend, center, 5f, ((float)random.NextDouble() - 0.5f) * 0.15f)];
                ArquipelagoRuinKit.Pillar(mesh.Pick(), center, footprint, h, tilt, width * 0.15f);
                ArquipelagoRuinKit.VinesOnBlock(center, footprint, h, tilt, view, random);
                y += h * 0.9f;
                width *= 0.75f;
            }
        }
    }
}
