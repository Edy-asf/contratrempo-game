using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Câmara de Cristal (teste de cenário, cena própria Camara_Teste): uma caverna colossal escura, com o teto de rocha
    // lá no alto, estalactites, abismo embaixo e névoa; plataformas de pedra monolítica lisa e polida (blocos com
    // juntas finas, piso de lajes, monólitos com entalhes geométricos) ligadas por lajes flutuantes e escadarias,
    // subindo até a base da estrutura de cristal: arcos triangulares aninhados de feixes de prismas, um dentro do
    // outro, até o núcleo branco-ciano que brilha. Aglomerados de cristal nas paredes e nas beiras, estilhaços
    // flutuando (alguns girando) e um par de portais de cristal (CrystalPortal) entre a plataforma do meio e um mirante
    // isolado de frente para a estrutura. Sem arena nem inimigos: só para avaliar o visual (modo livre F11).
    public static class CamaraBuilder
    {
        const string k_ScenePath = IcaroAssetUtility.ScenesFolder + "/Camara_Teste.unity";
        const string k_Prefix = "Camara";
        const float k_Abyss = -60f;           // fundo do abismo
        const float k_BaseTop = 15f;          // plataforma da base da estrutura
        static readonly Vector3 k_CaveCenter = new Vector3(0f, 0f, 120f);
        static readonly Vector3 k_CaveRadii = new Vector3(230f, 140f, 290f);

        static CamaraTextureKit.Set s_Materials;
        static MeshData s_Stone, s_Engraved, s_Rock, s_RockSolid, s_Crystal, s_CrystalPale, s_Core;
        static System.Random s_Random;
        static Transform s_World;
        static int s_Lights;

        static float R(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

        static GridTheme Theme() => new GridTheme
        {
            Biome = "Camara",
            BiomeIndex = 2,
            SceneName = "Camara_Teste",
            FogColor = new Color(0.035f, 0.085f, 0.095f),
            FogDensity = 0.0042f,
            AmbientSky = new Color(0.42f, 0.62f, 0.66f),
            AmbientEquator = new Color(0.22f, 0.32f, 0.35f),
            AmbientGround = new Color(0.08f, 0.1f, 0.12f),
            MoonColor = new Color(0.55f, 0.95f, 0.9f),
            MoonIntensity = 0.55f,
            SkyBlue = new Color(0.01f, 0.02f, 0.025f),
            SkyPurple = new Color(0.005f, 0.01f, 0.015f),
            PlanetSize = 0f,
            StarDensity = 0f,
            AuroraStrength = 0f,
            SporeColors = new[] { new Color(0.55f, 1f, 0.9f), new Color(0.85f, 1f, 1f), new Color(0.3f, 0.95f, 0.75f) },
        };

        [MenuItem("Ícaro/Câmara de Cristal/Gerar cena de teste", priority = 48)]
        static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Câmara de Cristal", "Texturas", 0.1f);
                s_Materials = CamaraTextureKit.Build();
                GridTheme theme = Theme();
                Material sky = GridSceneKit.NightSkyMaterial(theme);

                EditorUtility.DisplayProgressBar("Câmara de Cristal", "Cena", 0.3f);
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = ViaAeonidaBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);

                s_World = new GameObject("Camara").transform;
                PlacePlayer(scene);
                EditorSceneManager.SaveScene(scene, k_ScenePath);
                BuildContent(theme);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Câmara de Cristal (teste) gerada em {k_ScenePath}. Dê Play e use o modo livre (F11).");
            }
            finally
            {
                BlockMesh.Tile = GridSceneKit.Cell;
                EditorUtility.ClearProgressBar();
            }
        }

        static void BuildContent(GridTheme theme)
        {
            s_Random = new System.Random(2101);
            s_Stone = new MeshData();
            s_Engraved = new MeshData();
            s_Rock = new MeshData();
            s_RockSolid = new MeshData();
            s_Crystal = new MeshData();
            s_CrystalPale = new MeshData();
            s_Core = new MeshData();
            s_Lights = 0;
            BlockMesh.Tile = 4f;

            BuildCave();
            BuildPlatforms();
            BuildStructure();
            BuildPortals();
            BuildCrystalGrowth();

            // Pedra: os topos viram piso de lajes (UV no mundo); o resto, blocos
            var floor = new MeshData();
            var sides = new MeshData();
            SplitTops(s_Stone, floor, sides, 4f);
            GridSceneKit.CreateObject(k_Prefix + "_Pedra", "Pedra", sides, s_Materials.Stone, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Piso", "Piso", floor, s_Materials.Floor, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Entalhes", "Entalhes", s_Engraved, s_Materials.Engraved, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Caverna", "Caverna", s_Rock, s_Materials.Rock, s_World, false, false);
            GridSceneKit.CreateObject(k_Prefix + "_Rocha", "Rocha", s_RockSolid, s_Materials.Rock, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Cristais", "Cristais", s_Crystal, s_Materials.Crystal, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Cristais_Claros", "Cristais_Claros", s_CrystalPale,
                s_Materials.CrystalPale, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Nucleo", "Nucleo", s_Core, s_Materials.Core, s_World, false, false);

            GridSceneKit.AddSpores("Esporos_Camara", s_Materials.Spores, new Vector3(0f, 12f, 120f),
                new Vector3(300f, 70f, 320f), 420, 30f, new Vector2(0.05f, 0.16f), theme.SporeColors, s_World);
            s_World.gameObject.AddComponent<CameraFarClip>().Distance = 1200f;
        }

        // ---------- caverna ----------

        // Casca da caverna (elipsoide com ruído, faces para dentro) até o fundo do abismo, o chão do abismo, estalactites
        // penduradas no teto e agulhas de rocha subindo do abismo
        static void BuildCave()
        {
            const int rings = 44, around = 110;
            float thetaMax = Mathf.Acos(Mathf.Clamp((k_Abyss - 4f - k_CaveCenter.y) / k_CaveRadii.y, -1f, 1f));
            var grid = new Vector3[rings + 1, around + 1];
            for (int i = 0; i <= rings; i++)
            for (int j = 0; j <= around; j++)
            {
                float theta = thetaMax * i / rings, phi = Mathf.PI * 2f * j / around;
                var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                float noise = Mathf.PerlinNoise(phi * 3f + 7f, theta * 4f + 3f) * 0.16f +
                              Mathf.PerlinNoise(phi * 11f, theta * 13f + 9f) * 0.06f;
                grid[i, j] = k_CaveCenter + Vector3.Scale(dir, k_CaveRadii) * (0.92f + noise);
            }

            for (int i = 0; i < rings; i++)
            for (int j = 0; j < around; j++)
            {
                Vector3 a = grid[i, j], b = grid[i, j + 1], c = grid[i + 1, j + 1], d = grid[i + 1, j];
                Vector3 inward = k_CaveCenter - (a + c) * 0.5f;
                float u0 = j * 6f / 8f, u1 = (j + 1) * 6f / 8f, v0 = i * 4.5f / 8f, v1 = (i + 1) * 4.5f / 8f;
                Tri(s_Rock, a, b, c, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), inward);
                Tri(s_Rock, a, c, d, new Vector2(u0, v0), new Vector2(u1, v1), new Vector2(u0, v1), inward);
            }

            // Chão do abismo, com relevo
            const float step = 10f;
            for (float x = -300f; x < 300f; x += step)
            for (float z = -200f; z < 440f; z += step)
            {
                Vector3 Ground(float gx, float gz) => new Vector3(gx,
                    k_Abyss + Mathf.PerlinNoise(gx * 0.02f + 3f, gz * 0.02f + 5f) * 14f, gz);
                Vector3 a = Ground(x, z), b = Ground(x, z + step), c = Ground(x + step, z + step), d = Ground(x + step, z);
                Tri(s_RockSolid, a, b, c, new Vector2(x, z) / 8f, new Vector2(x, z + step) / 8f,
                    new Vector2(x + step, z + step) / 8f, Vector3.up);
                Tri(s_RockSolid, a, c, d, new Vector2(x, z) / 8f, new Vector2(x + step, z + step) / 8f,
                    new Vector2(x + step, z) / 8f, Vector3.up);
            }

            // Estalactites no teto e agulhas de rocha no abismo
            for (int k = 0; k < 70; k++)
            {
                float theta = R(0.05f, 1.1f), phi = R(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                Vector3 root = k_CaveCenter + Vector3.Scale(dir, k_CaveRadii) * 0.9f;
                float length = R(10f, 45f);
                Spike(s_Rock, root + Vector3.up * 4f, Vector3.down + new Vector3(R(-0.1f, 0.1f), 0f, R(-0.1f, 0.1f)), length,
                    R(2.5f, 7f), 7, R(0f, 60f), 0.6f);
            }

            for (int k = 0; k < 26; k++)
            {
                var foot = new Vector3(R(-200f, 200f), k_Abyss - 2f, R(-120f, 380f));
                if (Mathf.Abs(foot.x) < 80f && foot.z > -40f && foot.z < 250f)
                    foot.x += Mathf.Sign(foot.x) * 80f;
                Spike(s_RockSolid, foot, Vector3.up + new Vector3(R(-0.08f, 0.08f), 0f, R(-0.08f, 0.08f)), R(25f, 75f),
                    R(4f, 11f), 7, R(0f, 60f), 0.5f);
            }
        }

        // ---------- plataformas ----------

        // Plataforma monolítica: laje de topo grossa e, por baixo, blocos empilhados que afinam até sumir no abismo
        static void Platform(Vector3 top, float sizeX, float sizeZ, float depth = 40f)
        {
            BlockMesh.ChamferBox(s_Stone, top + Vector3.down * 0.9f, new Vector3(sizeX, 1.8f, sizeZ), Quaternion.identity, 0.08f);
            float y = top.y - 1.8f, sx = sizeX * 0.94f, sz = sizeZ * 0.94f;
            Vector3 shift = Vector3.zero;
            while (y > top.y - depth && sx > 2f && sz > 2f)
            {
                float height = R(3f, 8f);
                BlockMesh.ChamferBox(s_Stone, new Vector3(top.x, y - height * 0.5f, top.z) + shift,
                    new Vector3(sx, height, sz), Quaternion.Euler(0f, R(-2f, 2f), 0f), 0.08f);
                y -= height;
                sx *= R(0.72f, 0.92f);
                sz *= R(0.72f, 0.92f);
                shift += new Vector3(R(-1f, 1f), 0f, R(-1f, 1f));
            }
        }

        // Escada reta de 'bottom' até 'top' (centros da beira de baixo e de cima), com degraus maciços, blocos laterais
        // escalonados e a rampa invisível rente às quinas
        static void Stairs(Vector3 bottom, Vector3 top, float width, bool cheeks = true)
        {
            Vector3 flat = top - bottom;
            flat.y = 0f;
            Vector3 dir = flat.normalized;
            Quaternion rotation = Quaternion.LookRotation(dir);
            float rise = top.y - bottom.y, run = flat.magnitude;
            int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.3f));
            float stepRise = rise / steps, stepRun = run / steps;
            for (int k = 0; k < steps; k++)
            {
                float y = bottom.y + (k + 1) * stepRise;
                Vector3 c = bottom + dir * (stepRun * (k + 0.5f));
                float height = y - (bottom.y - 1f);
                BlockMesh.ChamferBox(s_Stone, new Vector3(c.x, y - height * 0.5f, c.z), new Vector3(width, height, stepRun + 0.02f),
                    rotation, 0.03f);
            }

            if (cheeks)
            {
                Vector3 right = rotation * Vector3.right;
                int blocks = Mathf.Max(1, Mathf.RoundToInt(run / 4f));
                foreach (float s in new[] { -1f, 1f })
                for (int b = 0; b < blocks; b++)
                {
                    float t0 = (float)b / blocks, t1 = (float)(b + 1) / blocks;
                    float y = bottom.y + rise * t1 + 0.9f;
                    Vector3 c = bottom + dir * (run * (t0 + t1) * 0.5f) + right * s * (width * 0.5f + 1.2f);
                    float height = y - (bottom.y - 2f);
                    BlockMesh.ChamferBox(s_Stone, new Vector3(c.x, y - height * 0.5f, c.z),
                        new Vector3(2.4f, height, run / blocks + 0.02f), rotation, 0.06f);
                }
            }

            var ramp = new GameObject("Rampa_Escada");
            ramp.transform.SetParent(s_World, false);
            Vector3 along = top - bottom;
            ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f + Quaternion.LookRotation(along) * Vector3.up * -0.12f,
                Quaternion.LookRotation(along));
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, along.magnitude);
        }

        // Monólito: bloco alto com faixas de entalhe nas quatro faces
        static void Monolith(Vector3 foot, Vector3 size, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            BlockMesh.ChamferBox(s_Stone, foot + Vector3.up * (size.y * 0.5f), size, rotation, 0.1f);
            BlockMesh.ChamferBox(s_Stone, foot + Vector3.up * (size.y + 0.3f), new Vector3(size.x + 0.5f, 0.6f, size.z + 0.5f),
                rotation, 0.1f);
            for (int f = 0; f < 4; f++)
            {
                Quaternion face = rotation * Quaternion.Euler(0f, f * 90f, 0f);
                float depth = f % 2 == 0 ? size.z : size.x, width = f % 2 == 0 ? size.x : size.z;
                Vector3 c = foot + Vector3.up * (size.y * 0.55f) + face * Vector3.forward * (depth * 0.5f + 0.03f);
                BlockMesh.Box(s_Engraved, c, new Vector3(width * 0.7f, size.y * 0.5f, 0.06f), face);
            }
        }

        static void BuildPlatforms()
        {
            // Início
            Platform(Vector3.zero, 26f, 30f);
            Monolith(new Vector3(-10f, 0f, 11f), new Vector3(2.6f, 9f, 2.6f), 0f);
            Monolith(new Vector3(10f, 0f, 11f), new Vector3(2.6f, 9f, 2.6f), 0f);
            for (int k = 0; k < 5; k++)
                BlockMesh.ChamferBox(s_Stone, new Vector3(R(-11f, 11f), 0.6f, R(-13f, 6f)), new Vector3(R(1.5f, 3f), 1.2f, R(1.5f, 3f)),
                    Quaternion.Euler(R(-6f, 6f), R(0f, 90f), R(-8f, 8f)), 0.06f);

            // Lajes flutuantes até a plataforma do meio (vãos de 0,6 a 1,4 m)
            for (float z = 15.5f; z < 59f; )
            {
                float length = R(5f, 8f), gap = R(0.6f, 1.4f);
                Platform(new Vector3(R(-1f, 1f), R(-0.2f, 0.2f), z + length * 0.5f), R(7f, 9f), length, 18f);
                z += length + gap;
            }

            // Plataforma do meio, com um terraço elevado e escada, monólitos nos cantos e blocos caídos
            Platform(new Vector3(0f, 0f, 84f), 52f, 48f);
            Platform(new Vector3(0f, 3f, 101f), 44f, 18f, 8f);
            Stairs(new Vector3(0f, 0f, 87f), new Vector3(0f, 3f, 92f), 14f);
            foreach (float s in new[] { -1f, 1f })
            {
                Monolith(new Vector3(s * 22f, 0f, 64f), new Vector3(3f, 12f, 3f), 0f);
                Monolith(new Vector3(s * 18f, 3f, 106f), new Vector3(3.4f, 14f, 3.4f), 0f);
                BlockMesh.ChamferBox(s_Stone, new Vector3(s * 12f, 1f, 72f), new Vector3(4f, 2f, 3f),
                    Quaternion.Euler(0f, R(0f, 40f), R(-10f, 10f)), 0.08f);
            }

            // Grande escadaria até a base da estrutura
            Platform(new Vector3(0f, 3f + 0.01f, 111f), 26f, 4f, 30f);
            Stairs(new Vector3(0f, 3f, 113f), new Vector3(0f, k_BaseTop, 137f), 22f);
            var support = new Vector3(0f, (3f + k_Abyss) * 0.5f, 125f);
            BlockMesh.ChamferBox(s_Stone, support, new Vector3(30f, 3f - k_Abyss, 24f), Quaternion.identity, 0.1f);

            // Base da estrutura: plataforma larga e, no fundo, um estrado em degraus até o núcleo
            Platform(new Vector3(0f, k_BaseTop, 186f), 132f, 98f, 70f);
            for (int t = 0; t < 3; t++)
            {
                float y = k_BaseTop + (t + 1) * 1.2f;
                BlockMesh.ChamferBox(s_Stone, new Vector3(0f, y - 0.6f - 1f, 214f), new Vector3(46f - t * 10f, 1.2f + 2f, 22f - t * 4f),
                    Quaternion.identity, 0.06f);
            }

            Stairs(new Vector3(0f, k_BaseTop, 198f), new Vector3(0f, k_BaseTop + 3.6f, 207f), 10f, false);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 3; k++)
                    Monolith(new Vector3(s * (24f + k * 14f), k_BaseTop, 150f + k * 6f), new Vector3(3.2f, 10f + k * 3f, 3.2f), 0f);
                // Degraus soltos e blocos tombados pela base
                for (int k = 0; k < 6; k++)
                    BlockMesh.ChamferBox(s_Stone, new Vector3(s * R(14f, 56f), k_BaseTop + 0.8f, R(142f, 230f)),
                        new Vector3(R(3f, 8f), R(1.2f, 2.4f), R(3f, 8f)), Quaternion.Euler(R(-4f, 4f), R(0f, 90f), R(-4f, 4f)), 0.08f);
            }

            // Mirante isolado (só se chega pelo portal), de frente para a estrutura
            Platform(new Vector3(-78f, 26f, 150f), 18f, 18f, 30f);
            Monolith(new Vector3(-71f, 26f, 143f), new Vector3(2.4f, 8f, 2.4f), 30f);

            // Blocos flutuando no vazio, entre as plataformas
            for (int k = 0; k < 14; k++)
            {
                var c = new Vector3(R(-110f, 110f), R(-25f, 45f), R(-20f, 260f));
                if (Mathf.Abs(c.x) < 40f)
                    c.x += Mathf.Sign(c.x) * 45f;
                BlockMesh.ChamferBox(s_Stone, c, new Vector3(R(3f, 12f), R(2f, 6f), R(3f, 12f)),
                    Quaternion.Euler(R(-15f, 15f), R(0f, 90f), R(-15f, 15f)), 0.1f);
            }
        }

        // ---------- estrutura de cristal ----------

        // Arcos triangulares aninhados (cada um de feixes de prismas, as pernas cruzando além do topo), recuando em
        // direção ao fundo, o núcleo triangular branco-ciano no estrado e estilhaços em volta
        static void BuildStructure()
        {
            const int arches = 6;
            for (int i = 0; i < arches; i++)
            {
                float z = 180f + i * 8f, half = 62f - i * 10f, height = 96f - i * 15f;
                var foot = new Vector3(0f, k_BaseTop - 2f, z);
                Vector3 apex = new Vector3(0f, k_BaseTop + height, z);
                float thickness = Mathf.Lerp(2.4f, 1.1f, i / (arches - 1f));
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 from = foot + Vector3.right * s * half;
                    Vector3 to = apex + (apex - from) * 0.14f;
                    Leg(from, to, thickness);
                }
            }

            // Núcleo: triângulo de luz no estrado, dos dois lados, e a luz forte
            var core = new Vector3(0f, k_BaseTop + 3.6f, 216f);
            Vector3 l = core + new Vector3(-10f, 0f, 0f), r = core + new Vector3(10f, 0f, 0f), t = core + new Vector3(0f, 18f, 0f);
            Tri(s_Core, l, t, r, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f), Vector3.back);
            Tri(s_Core, l, r, t, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), Vector3.forward);
            Light(core + new Vector3(0f, 7f, -6f), new Color(0.55f, 1f, 0.9f), 900f, 160f);
            Light(new Vector3(0f, k_BaseTop + 50f, 190f), new Color(0.7f, 1f, 0.95f), 500f, 140f);

            // Estilhaços parados no ar em volta da estrutura
            for (int k = 0; k < 160; k++)
            {
                var p = new Vector3(R(-80f, 80f), k_BaseTop + R(5f, 110f), R(160f, 240f));
                Spike(s_Random.NextDouble() < 0.3 ? s_CrystalPale : s_Crystal, p, Random3(), R(1f, 5f), R(0.15f, 0.6f), 5,
                    R(0f, 70f), 1.6f, true);
            }

            // Estilhaços maiores girando devagar (objetos próprios)
            for (int k = 0; k < 18; k++)
            {
                var p = new Vector3(R(-70f, 70f), k_BaseTop + R(10f, 80f), R(150f, 235f));
                var mesh = new MeshData();
                Spike(mesh, Random3() * -R(2f, 5f), Random3(), R(5f, 12f), R(0.6f, 1.6f), 6, R(0f, 60f), 1.6f, true);
                var root = new GameObject($"Estilhaco_{k + 1}");
                root.transform.SetParent(s_World, false);
                root.transform.position = p;
                GridSceneKit.CreateObject($"{k_Prefix}_Estilhaco_{k + 1}", "Cristal", mesh,
                    k % 3 == 0 ? s_Materials.CrystalPale : s_Materials.Crystal, root.transform, false, false, false);
                var spin = root.AddComponent<SlowRotation>();
                spin.Axis = Random3();
                spin.DegreesPerSecond = R(3f, 10f);
                spin.Bob = R(0.4f, 1.2f);
            }
        }

        // Perna de arco: feixe de prismas paralelos de 'from' a 'to', com pontas desencontradas e alguns claros
        static void Leg(Vector3 from, Vector3 to, float thickness)
        {
            Vector3 dir = (to - from).normalized;
            float length = Vector3.Distance(from, to);
            Vector3 u = Vector3.Cross(dir, Vector3.forward).normalized, w = Vector3.Cross(dir, u);
            int count = 8;
            for (int k = 0; k < count; k++)
            {
                Vector3 offset = u * R(-1.6f, 1.6f) * thickness + w * R(-1f, 1f) * thickness;
                float start = R(-0.04f, 0.1f) * length, end = R(0.86f, 1.04f) * length;
                MeshData target = s_Random.NextDouble() < 0.22 ? s_CrystalPale : s_Crystal;
                Spike(target, from + dir * start + offset, dir, end - start, thickness * R(0.35f, 0.75f), s_Random.Next(5, 7),
                    R(0f, 60f), 1.4f, true);
            }
        }

        // ---------- portais ----------

        static void BuildPortals()
        {
            var forwardB = new Vector3(0.83f, 0f, 0.55f).normalized;
            CrystalPortal a = Portal("Portal_A", new Vector3(-21f, 0f, 76f), Vector3.right);
            CrystalPortal b = Portal("Portal_B", new Vector3(-78f, 26f, 150f) - forwardB * 6f, forwardB);
            a.Target = b;
            b.Target = a;
        }

        // Portal triangular: moldura de prismas, superfície acesa (dos dois lados), gatilho e luz
        static CrystalPortal Portal(string name, Vector3 foot, Vector3 forward)
        {
            var root = new GameObject(name);
            root.transform.SetParent(s_World, false);
            root.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(forward));
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 l = foot - right * 3f, r = foot + right * 3f, t = foot + Vector3.up * 6.5f;
            Leg(l + Vector3.down * 0.5f, t + (t - l) * 0.1f, 0.35f);
            Leg(r + Vector3.down * 0.5f, t + (t - r) * 0.1f, 0.35f);
            BlockMesh.ChamferBox(s_Stone, foot + Vector3.down * 0.05f, new Vector3(7.5f, 0.5f, 2.2f), Quaternion.LookRotation(forward), 0.06f);

            var surface = new MeshData();
            Vector3 li = new Vector3(-2.5f, 0.3f, 0f), ri = new Vector3(2.5f, 0.3f, 0f), ti = new Vector3(0f, 5.5f, 0f);
            Tri(surface, li, ti, ri, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f), Vector3.back);
            Tri(surface, li, ri, ti, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), Vector3.forward);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Superficie", "Superficie", surface, s_Materials.Portal, root.transform,
                false, false, false);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3.2f, 4f, 0.8f);
            trigger.center = new Vector3(0f, 2.2f, 0f);
            Light(foot + Vector3.up * 3f + forward * 1.5f, new Color(0.3f, 1f, 0.8f), 30f, 16f);
            return root.AddComponent<CrystalPortal>();
        }

        // ---------- cristais nas paredes e nas beiras ----------

        static void BuildCrystalGrowth()
        {
            // Nas paredes e no teto da caverna
            for (int k = 0; k < 55; k++)
            {
                float theta = R(0.3f, 1.7f), phi = R(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                Vector3 p = k_CaveCenter + Vector3.Scale(dir, k_CaveRadii) * 0.9f;
                Cluster(p, (k_CaveCenter - p).normalized, s_Random.Next(5, 11), R(2f, 5f), k < 6);
            }

            // Nas beiras das plataformas e na base da estrutura
            var spots = new List<(Vector3 p, Vector3 n)>
            {
                (new Vector3(13f, -0.5f, -8f), Vector3.right), (new Vector3(-13f, -1f, 5f), Vector3.left),
                (new Vector3(26f, -0.5f, 90f), Vector3.right), (new Vector3(-26f, -0.5f, 70f), Vector3.left),
                (new Vector3(-24f, 2f, 104f), Vector3.left), (new Vector3(66f, k_BaseTop - 1f, 170f), Vector3.right),
                (new Vector3(-66f, k_BaseTop - 1f, 200f), Vector3.left), (new Vector3(40f, k_BaseTop, 228f), Vector3.up),
                (new Vector3(-36f, k_BaseTop, 160f), Vector3.up), (new Vector3(-86f, 25f, 156f), Vector3.left),
                (new Vector3(10f, -3f, 40f), Vector3.right), (new Vector3(0f, k_BaseTop - 4f, 138f), Vector3.back),
            };
            for (int k = 0; k < spots.Count; k++)
                Cluster(spots[k].p, (spots[k].n + Vector3.up * 0.6f).normalized, s_Random.Next(4, 9), R(0.8f, 1.8f), true);
        }

        // Aglomerado de prismas saindo de 'p' em leque em volta de 'normal' (às vezes com luz)
        static void Cluster(Vector3 p, Vector3 normal, int count, float scale, bool light)
        {
            for (int k = 0; k < count; k++)
            {
                Vector3 axis = (normal + Random3() * 0.55f).normalized;
                MeshData target = s_Random.NextDouble() < 0.3 ? s_CrystalPale : s_Crystal;
                Spike(target, p + Random3() * scale * 0.6f - axis * 0.4f * scale, axis, R(1.5f, 6f) * scale,
                    R(0.25f, 0.7f) * scale, s_Random.Next(5, 7), R(0f, 60f), 1.5f, true);
            }

            if (light)
                Light(p + normal * scale * 3f, new Color(0.35f, 1f, 0.85f), 40f * scale * scale, 18f * Mathf.Sqrt(scale));
        }

        // ---------- peças ----------

        // Prisma de 'sides' lados de 'start' na direção 'axis', com ponta em pirâmide ('tip' x raio). Com 'faceUv', cada
        // face vai de u 0 a 1 (as arestas acesas da textura do cristal); senão, UV pela distância (rocha)
        static void Spike(MeshData mesh, Vector3 start, Vector3 axis, float length, float radius, int sides, float angle,
            float tip, bool faceUv = false)
        {
            Vector3 dir = axis.normalized;
            Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 w = Vector3.Cross(dir, u);
            float tipLength = Mathf.Min(radius * tip, length * 0.5f), body = length - tipLength;
            Vector3 end = start + dir * body, apex = start + dir * length;
            float faceWidth = 2f * radius * Mathf.Sin(Mathf.PI / sides);
            float vBody = faceUv ? body / (faceWidth * 2f) : body / 8f, vTip = faceUv ? tipLength / (faceWidth * 2f) : tipLength / 8f;
            for (int k = 0; k < sides; k++)
            {
                float a0 = angle * Mathf.Deg2Rad + k * Mathf.PI * 2f / sides, a1 = a0 + Mathf.PI * 2f / sides;
                Vector3 r0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * radius, r1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * radius;
                Vector3 outward = r0 + r1;
                float uw = faceUv ? 1f : faceWidth / 8f;
                Tri(mesh, start + r0, end + r0, end + r1, new Vector2(0f, 0f), new Vector2(0f, vBody), new Vector2(uw, vBody), outward);
                Tri(mesh, start + r0, end + r1, start + r1, new Vector2(0f, 0f), new Vector2(uw, vBody), new Vector2(uw, 0f), outward);
                Tri(mesh, end + r0, apex, end + r1, new Vector2(0f, vBody), new Vector2(uw * 0.5f, vBody + vTip),
                    new Vector2(uw, vBody), outward + dir * radius);
            }
        }

        // Triângulo com a normal da face, virado para 'facing' (troca a ordem se precisar)
        static void Tri(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 facing)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, facing) < 0f)
            {
                (b, c) = (c, b);
                (ub, uc) = (uc, ub);
                normal = -normal;
            }

            normal.Normalize();
            int start = mesh.Vertices.Count;
            mesh.Vertices.AddRange(new[] { a, b, c });
            mesh.Normals.AddRange(new[] { normal, normal, normal });
            mesh.Uvs.AddRange(new[] { ua, ub, uc });
            mesh.Triangles.AddRange(new[] { start, start + 1, start + 2 });
        }

        // Separa os triângulos virados para cima (piso, UV no mundo xz / tile) dos outros
        static void SplitTops(MeshData source, MeshData tops, MeshData rest, float tile)
        {
            for (int i = 0; i < source.Triangles.Count; i += 3)
            {
                int a = source.Triangles[i], b = source.Triangles[i + 1], c = source.Triangles[i + 2];
                bool up = source.Normals[a].y > 0.7f;
                MeshData target = up ? tops : rest;
                int start = target.Vertices.Count;
                foreach (int index in new[] { a, b, c })
                {
                    Vector3 v = source.Vertices[index];
                    target.Vertices.Add(v);
                    target.Normals.Add(source.Normals[index]);
                    target.Uvs.Add(up ? new Vector2(v.x, v.z) / tile : source.Uvs[index]);
                }

                target.Triangles.AddRange(new[] { start, start + 1, start + 2 });
            }
        }

        static Vector3 Random3() => new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)).normalized;

        static void Light(Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject($"Luz_{++s_Lights}").AddComponent<Light>();
            light.transform.SetParent(s_World, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.2f, -8f), Quaternion.LookRotation(Vector3.forward));
        }
    }
}
