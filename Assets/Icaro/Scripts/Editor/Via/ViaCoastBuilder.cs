using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // "Ícaro > Via > Gerar Bosque e Arquipélago": a Via em grade numa cena só (Scenes/Via_Grade.unity): o Bosque
    // (GridBiomeBuilder, com a arena do chefe aberta para o norte), o desfiladeiro que desce dele até a costa e o
    // Arquipélago (ArquipelagoBuilder.BuildInto), com o alto do penhasco encostado no fim do desfiladeiro.
    // Desfiladeiro: ~220 m em S, descendo 6 m, entre paredões de rocha (a pedra roxa do Bosque no começo, a do penhasco
    // do Arquipélago depois); o chão é a trilha do Bosque, depois rocha molhada e, no fim, areia; as primeiras ruínas
    // (colunas, tambores caídos, um arco) aparecem na segunda metade, com trepadeiras nos paredões. No fim ele se abre
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
            GridTheme bosque = GridThemes.Bosque();
            bosque.SceneName = k_SceneName;
            bosque.OpenEnd = true;
            GridBiomeBuilder.Generate(bosque, (scene, world) => Extend(bosque, world));
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
            Vector3 start = GridBiomeBuilder.OpenEndPoint;
            Vector3 end = PathAt(start, 1f);
            string navMeshFolder = $"{IcaroAssetUtility.ScenesFolder}/{k_SceneName}_Arquipelago_NavMesh";
            ArquipelagoTextureKit.Set coast = ArquipelagoBuilder.BuildInto(world, end, navMeshFolder,
                out string report);

            var canyon = new GameObject("Desfiladeiro").transform;
            canyon.SetParent(world, false);
            BuildCanyon(start, bosque.Materials, coast, canyon);

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

            // Chão: trilha do Bosque, rocha molhada e areia, com uma margem por baixo dos paredões
            var trail = new MeshData();
            var rockFloor = new MeshData();
            var sand = new MeshData();
            for (int i = 0; i < k_Samples - 1; i++)
            {
                float t = (i + 0.5f) / (k_Samples - 1f);
                MeshData floor = t < 0.3f ? trail : t < 0.8f ? rockFloor : sand;
                float w0 = widths[i] * 0.5f + k_FloorMargin, w1 = widths[i + 1] * 0.5f + k_FloorMargin;
                floor.AddQuadAuto(points[i] - normals[i] * w0, points[i + 1] - normals[i + 1] * w1,
                    points[i + 1] + normals[i + 1] * w1, points[i] + normals[i] * w0, Vector3.up);
            }

            GridSceneKit.CreateObject(k_SceneName + "_Desfiladeiro_Trilha", "Chao_Trilha", trail, forest.Path, parent,
                true);
            GridSceneKit.CreateObject(k_SceneName + "_Desfiladeiro_Rocha", "Chao_Rocha", rockFloor, coast.Cliff, parent,
                true);
            GridSceneKit.CreateObject(k_SceneName + "_Desfiladeiro_Areia", "Chao_Areia", sand, coast.Sand, parent, true);

            // Paredões: pilhas de rocha dos dois lados (a pedra do Bosque no começo, a do penhasco depois) e pedras
            // soltas na beira do chão
            var forestRock = new RuinMesh(31, 1);
            var coastRock = new RuinMesh(32, 1);
            var ruins = new RuinMesh(33);
            ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
            ArquipelagoRuinKit.VineMaterial = coast.Vine;
            ArquipelagoRuinKit.VineSize = coast.VineSize;
            for (int i = 0; i < k_Samples; i++)
            {
                float t = i / (k_Samples - 1f);
                if (t > 0.97f)
                    continue;
                float height = Mathf.Lerp(12f, 26f, Smooth(0f, 0.15f, t)) - 6f * Smooth(0.85f, 1f, t);
                foreach (float side in new[] { -1f, 1f })
                {
                    bool fromForest = t + ((float)random.NextDouble() - 0.5f) * 0.2f < 0.35f;
                    RuinMesh mesh = fromForest ? forestRock : coastRock;
                    float offset = widths[i] * 0.5f + 3.5f + (float)random.NextDouble() * 1.5f;
                    Vector3 foot = points[i] + normals[i] * side * offset;
                    RockPile(mesh, foot, height * (0.85f + (float)random.NextDouble() * 0.3f), random);

                    if (random.NextDouble() < 0.45)
                    {
                        float size = 0.8f + (float)random.NextDouble() * 1.1f;
                        Vector3 stone = points[i] + normals[i] * side * (widths[i] * 0.5f - 0.6f) +
                                        Vector3.up * (size * 0.35f);
                        BlockMesh.ChamferBox(mesh.Pick(), stone, Vector3.one * size,
                            Quaternion.Euler((float)random.NextDouble() * 30f, (float)random.NextDouble() * 360f,
                                (float)random.NextDouble() * 30f), size * 0.2f);
                    }

                    // Trepadeiras na face voltada para o caminho, da metade para a frente
                    if (t > 0.4f)
                        ArquipelagoRuinKit.HangVine(points[i] + normals[i] * side * (offset - 3.3f) +
                                                    Vector3.up * (height * 0.6f), -normals[i] * side, random, 2f);
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

            ArquipelagoBuilder.CreateRuin("Desfiladeiro_Rocha_Bosque", forestRock, new[] { forest.Rock }, parent, true,
                -100f);
            ArquipelagoBuilder.CreateRuin("Desfiladeiro_Rocha_Costa", coastRock, new[] { coast.Cliff }, parent, true,
                -100f);
            ArquipelagoBuilder.CreateRuin("Desfiladeiro_Ruinas", ruins, coast.StoneTones, parent, true, -100f);
            ArquipelagoRuinKit.Vines.Build(k_SceneName + "_Desfiladeiro_Trepadeiras", parent);
            ArquipelagoRuinKit.Vines = null;
        }

        // Pilha de rocha: dois ou três blocos que afinam para cima, um pouco tortos, do chão ('foot') até 'height'
        // acima dele
        static void RockPile(RuinMesh mesh, Vector3 foot, float height, System.Random random)
        {
            float width = 6f + (float)random.NextDouble() * 3f;
            float y = foot.y - 3f;
            int parts = 2 + random.Next(2);
            for (int i = 0; i < parts; i++)
            {
                float h = (height + 3f) / parts * (0.8f + (float)random.NextDouble() * 0.4f);
                Quaternion tilt = Quaternion.Euler((float)random.NextDouble() * 6f - 3f,
                    (float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 6f - 3f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(foot.x, y + h * 0.5f, foot.z),
                    new Vector2(width, width * (0.8f + (float)random.NextDouble() * 0.3f)), h, tilt, width * 0.15f);
                y += h * 0.9f;
                width *= 0.75f;
            }
        }
    }
}
