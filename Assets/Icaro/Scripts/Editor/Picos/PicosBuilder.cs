using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Picos acima das Nuvens (bioma 3, no lugar das Ruínas; ideias em Design/Picos_Acima_das_Nuvens.md), em cena própria
    // de teste (Picos_Teste). A Chegada pelo portal: no alto de um pico, uma torre esculpida (TowerTop) com o portal-anel
    // ainda girando no topo (anel de pedra parado, dois anéis girando em sentidos opostos e a superfície acesa), colunas
    // partidas e montes de neve; a oeste o parapeito despencou e alguns blocos ficaram parados no ar no meio da queda (o
    // tempo parou ali); um aqueduto quebrado a leste com a água congelada caindo pela parede do pico. A saída desce por
    // uma escadaria até a trilha da crista e uma ponte de pedra até a torre da primeira arena (sala 17, Terraços
    // Partidos, ainda sem inimigos). Em volta, dezenas de montanhas saindo do mar de nuvens de volume, quatro montanhas de
    // destaque do Blender, um mosteiro ao longe e uma ilha flutuante sobre um anel. Céu: eclipse congelado (disco negro
    // com a coroa), horizonte alaranjado em toda a volta, estrelas. Neve só estética; sem vida nenhuma.
    public static class PicosBuilder
    {
        const string k_ScenePath = IcaroAssetUtility.ScenesFolder + "/Picos_Teste.unity";
        const string k_Prefix = "Picos";
        const float k_CloudY = -90f;            // superfície do mar de nuvens (os montes de fumaça vão de ~-110 a -70)
        const float k_CloudBottom = -260f;      // base da camada de volume (tudo abaixo de ~-150 é nuvem fechada)
        const float k_CloudTop = -70f;          // topo dos montes mais altos
        static readonly Vector3 k_EclipseDir = new Vector3(-0.25f, 0.33f, -0.91f).normalized;
        static readonly Vector3 k_PortalCenter = new Vector3(0f, 9.5f, 15.5f);
        // Centro do chão da primeira arena (sala 17, Terraços Partidos), no pico ao sul da praça do portal
        static readonly Vector3 k_NextPeak = new Vector3(-10f, -15f, -430f);
        const float k_ArenaRadius = 38f;    // até as quinas do topo da torre da arena 1
        const float k_PlazaRadius = 25f;    // até as quinas do topo da torre da chegada

        // Sala 18, Sela da Crista: a sela (laje em estádio) entre duas torres baixas e a ponte de corrente ao sul até o
        // patamar da sala 19
        static readonly Vector3 k_Saddle = new Vector3(-20f, -20f, -670f);
        const float k_SaddleHalf = 18f, k_SaddleRadius = 15f;      // meio comprimento da parte reta e raio das pontas
        static readonly Vector3 k_TowerWest = k_Saddle + new Vector3(-36f, 7f, 0f), k_TowerEast = k_Saddle + new Vector3(36f, 8f, -2f);
        const float k_TowerWestRadius = 15f, k_TowerEastRadius = 14f;
        static readonly Vector3 k_Landing = new Vector3(-20f, -21f, -790f);
        const float k_LandingRadius = 10f;
        static readonly Vector2 k_MapCenter = new Vector2(0f, -400f);   // meio do caminho, centro das montanhas de longe

        // Áreas do caminho onde as montanhas de longe não entram (centro no plano e raio), preenchidas enquanto o caminho
        // é montado
        static readonly List<(Vector2 Center, float Radius)> s_Clearings = new List<(Vector2, float)>();

        // Montanhas de destaque esculpidas no Blender (Ferramentas/Picos/montanhas_destaque.py, Models/Picos):
        // Catedral, Torres gêmeas, Arco e Mesa em degraus, em volta do caminho (posição, giro, raio para afastar as outras)
        static readonly (string Name, Vector3 Position, float Yaw, float Radius)[] k_Heroes =
        {
            ("Pico_Destaque_1", new Vector3(170f, 0f, -200f), 30f, 75f),
            ("Pico_Destaque_2", new Vector3(-170f, -10f, -110f), 110f, 55f),
            ("Pico_Destaque_3", new Vector3(-150f, -15f, -470f), 15f, 70f),
            ("Pico_Destaque_4", new Vector3(-210f, -25f, 70f), 200f, 80f),
        };

        static PicosTextureKit.Set s_Materials;
        static MeshData s_Snow, s_Floor, s_Ice, s_Path, s_MountainNear, s_MountainFar, s_Iron;
        static RuinMesh s_Ruins;
        static System.Random s_Random;
        static Transform s_World;

        static float R(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

        static GridTheme Theme() => new GridTheme
        {
            Biome = "Picos",
            BiomeIndex = 2,
            SceneName = "Picos_Teste",
            FogColor = new Color(0.46f, 0.33f, 0.4f),
            FogDensity = 0.0008f,
            AmbientSky = new Color(0.42f, 0.46f, 0.72f),
            AmbientEquator = new Color(0.78f, 0.52f, 0.46f),
            AmbientGround = new Color(0.32f, 0.3f, 0.44f),
            MoonColor = new Color(1f, 0.62f, 0.4f),
            MoonIntensity = 1.15f,
            SkyBlue = new Color(0.95f, 0.46f, 0.22f),
            SkyPurple = new Color(0.34f, 0.15f, 0.38f),
            SkyBlueHeight = 0.16f,
            SkyTop = 0.62f,
            PlanetSize = 0f,
            StarDensity = 0.3f,
            AuroraStrength = 0f,
        };

        [MenuItem("Ícaro/Picos/Gerar Chegada (teste)", priority = 49)]
        static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Sem plantas no bioma da neve: as peças de ruína não penduram trepadeiras
            GridSceneKit.Cards vines = ArquipelagoRuinKit.Vines;
            Material vineMaterial = ArquipelagoRuinKit.VineMaterial;
            ArquipelagoRuinKit.Vines = null;
            ArquipelagoRuinKit.VineMaterial = null;
            try
            {
                EditorUtility.DisplayProgressBar("Picos", "Texturas", 0.1f);
                s_Materials = PicosTextureKit.Build();
                GridTheme theme = Theme();
                Material sky = GridSceneKit.NightSkyMaterial(theme);
                Eclipse(sky);

                EditorUtility.DisplayProgressBar("Picos", "Cena", 0.3f);
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = SceneSystemsBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);
                // A luz vem do eclipse (laranja, baixa)
                if (RenderSettings.sun)
                    RenderSettings.sun.transform.rotation = Quaternion.LookRotation(-k_EclipseDir);

                s_World = new GameObject("Picos").transform;
                PlacePlayer(scene);
                EditorSceneManager.SaveScene(scene, k_ScenePath);

                EditorUtility.DisplayProgressBar("Picos", "Picos e ruínas", 0.5f);
                BuildContent();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Picos: Chegada pelo portal (teste) gerada em {k_ScenePath}. Dê Play; F11 voa.");
            }
            finally
            {
                ArquipelagoRuinKit.Vines = vines;
                ArquipelagoRuinKit.VineMaterial = vineMaterial;
                BlockMesh.Tile = GridSceneKit.Cell;
                EditorUtility.ClearProgressBar();
            }
        }

        // Eclipse congelado no material do céu: disco e coroa, horizonte alaranjado em toda a volta (mais forte do lado
        // do eclipse) e o zênite índigo
        static void Eclipse(Material sky)
        {
            sky.SetVector("_EclipseDir", k_EclipseDir);
            sky.SetFloat("_EclipseSize", 0.085f);
            sky.SetColor("_CoronaColor", new Color(3.2f, 2.3f, 1.4f));
            sky.SetFloat("_CoronaSize", 2.6f);
            sky.SetColor("_TopColor", new Color(0.03f, 0.03f, 0.1f));
            sky.SetFloat("_HorizonFog", 0.06f);
            sky.SetFloat("_GlowStrength", 1.3f);
            sky.SetFloat("_GlowHeight", 0.35f);
            sky.SetFloat("_GlowFalloff", 2.2f);
            sky.SetColor("_GlowColorLow", new Color(1.5f, 0.62f, 0.26f));
            sky.SetColor("_GlowColorHigh", new Color(0.45f, 0.2f, 0.6f));
            sky.SetFloat("_GlowAzimuth", Mathf.Repeat(Mathf.Atan2(k_EclipseDir.x, k_EclipseDir.z) * Mathf.Rad2Deg, 360f));
            sky.SetFloat("_GlowDirectional", 0.35f);
            sky.SetFloat("_GlowWidth", 90f);
            sky.SetFloat("_GlowOverFog", 0.8f);
            EditorUtility.SetDirty(sky);
        }

        static void BuildContent()
        {
            s_Random = new System.Random(1701);
            s_MountainNear = new MeshData();
            s_Clearings.Clear();
            Clear(Vector3.zero, 60f);
            s_MountainFar = new MeshData();
            PicosMountainKit.Pillars.Clear();
            s_Snow = new MeshData();
            s_Floor = new MeshData();
            s_Ice = new MeshData();
            s_Path = new MeshData();
            s_Iron = new MeshData();
            s_Ruins = new RuinMesh(1702);
            BlockMesh.Tile = 4f;

            BuildArrivalPeak();
            BuildPlaza();
            BuildPortal();
            BuildFrozenFall();
            BuildAqueduct();
            BuildCrest();
            BuildArena();
            BuildSaddle();
            BuildHeroes();
            BuildDistant();
            BuildClouds();

            // Montanhas: as do caminho com colisão, as de longe sem
            GridSceneKit.CreateObject(k_Prefix + "_Montanhas_Perto", "Montanhas_Perto", s_MountainNear, s_Materials.Mountain,
                s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Montanhas", "Montanhas", s_MountainFar, s_Materials.Mountain, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Neve", "Neve", s_Snow, s_Materials.Snow, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Piso", "Piso", s_Floor, s_Materials.Floor, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Trilha", "Trilha", s_Path, s_Materials.Snow, s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Gelo", "Gelo", s_Ice, s_Materials.Ice, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Ferro", "Ferro", s_Iron, s_Materials.Iron, s_World, false);
            CreateRuin("Ruinas", s_Ruins, true);
            BuildArenaRoom();
            BuildSaddleRoom();

            Snowfall(new Vector3(-6f, 30f, -50f));
            Snowfall(k_NextPeak + Vector3.up * 30f);
            Snowfall(k_Saddle + Vector3.up * 30f);
            var hazard = s_World.gameObject.AddComponent<SeaHazard>();
            hazard.SeaLevel = k_CloudY - 25f;   // dentro da nuvem, já fora da vista
            s_World.gameObject.AddComponent<CameraFarClip>().Distance = 3800f;   // o mar de nuvens cobre o caminho todo
        }

        // ---------- picos ----------

        // Torre esculpida no topo de um pico (a praça e cada arena ficam em cima de uma): planta oitavada com uma face
        // para cada ponto cardeal (lado 0 = leste, 2 = norte, 4 = oeste, 6 = sul), como uma torre de pedra lavrada:
        // - o topo é uma laje de lajes grandes com a borda em cornija saliente; embaixo, uma saia inclinada até o fuste
        //   com uma fileira de mísulas (o topo fica mais largo que o fuste, como a coroa da torre);
        // - parapeito baixo em cada lado com um bloco mais alto em cada quina (aberto nos lados 'open': entrada, saída,
        //   aqueduto ou onde despencou); um ou outro trecho partido;
        // - o fuste, um pouco mais estreito, é de blocos aparelhados em fiadas desencontradas, com pilastras nas quinas
        //   e uma porta escura num dos lados ('doorSide', só cenário);
        // - a rocha do pico abraça a base: o pilar de rocha logo abaixo, mais largo que o fuste, e penhascos menores
        //   subindo em volta (fora dos lados abertos), como os cristais em volta da torre da referência.
        // 'top' é o centro do chão; 'radius' vai até as quinas do topo. Devolve o raio de dentro (meio de cada lado).
        static float TowerTop(Vector3 top, float radius, int seed, int[] open, int doorSide)
        {
            const int sides = 8;
            float half = 180f / sides, inner = radius * Mathf.Cos(half * Mathf.Deg2Rad);
            float shaft = radius * 0.84f, shaftInner = shaft * Mathf.Cos(half * Mathf.Deg2Rad);
            float slabBottom = top.y - 1.6f, corniceBottom = top.y - 2.4f, skirtBottom = corniceBottom - 4f;
            float masonryBottom = top.y - 18f;

            // Laje do topo (vira piso) e cornija
            var slab = new MeshData();
            Prism(slab, top, radius, radius, sides, top.y, slabBottom, true);
            SplitTops(slab, s_Floor, s_Ruins.Tones[0], 4f);
            Prism(s_Ruins.Pick(), top, radius + 0.7f, radius + 0.7f, sides, slabBottom, corniceBottom, true);
            // Saia inclinada da cornija até o fuste (o topo é mais largo, como a coroa da torre)
            Prism(s_Ruins.Pick(), top, radius, shaft, sides, corniceBottom, skirtBottom, false);

            for (int k = 0; k < sides; k++)
            {
                Vector3 normal = SideNormal(k, sides);
                Vector3 along = Vector3.Cross(Vector3.up, normal);
                Quaternion facing = Quaternion.LookRotation(normal);
                float length = 2f * radius * Mathf.Sin(half * Mathf.Deg2Rad);
                Vector3 edge = top + normal * inner;

                // Mísulas na saia, sob a cornija
                int corbels = Mathf.FloorToInt(length / 2.6f);
                Vector3 skirt = top + normal * (inner + shaftInner) * 0.5f;
                for (int i = 0; i < corbels; i++)
                {
                    float t = (i + 0.5f) / corbels - 0.5f;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(skirt.x, corniceBottom - 2f, skirt.z) + along * t * length * 0.85f,
                        new Vector3(1f, 2.4f, (inner - shaftInner) * 0.9f), facing, 0.06f);
                }

                // Pingentes de gelo pendurados da cornija
                for (int i = 0; i < 9; i++)
                    Spike(s_Ice, new Vector3(edge.x, corniceBottom - 0.1f, edge.z) + normal * 0.6f + along * R(-0.45f, 0.45f) * length,
                        Vector3.down, R(0.5f, 2.6f), R(0.1f, 0.3f), 5, R(0f, 60f), 3f);

                // Parapeito do lado (entre os blocos das quinas)
                if (System.Array.IndexOf(open, k) < 0)
                {
                    float wall = length - 3.2f;
                    Vector3 c = top + normal * (inner - 0.7f);
                    if (s_Random.NextDouble() < 0.25)
                    {
                        // Trecho partido: dois tocos com um vão no meio
                        foreach (float side in new[] { -1f, 1f })
                            BlockMesh.ChamferBox(s_Ruins.Pick(), c + along * side * wall * 0.32f + Vector3.up * 0.45f,
                                new Vector3(wall * 0.3f, 0.9f, 1f), facing, 0.08f);
                    }
                    else
                        BlockMesh.ChamferBox(s_Ruins.Pick(), c + Vector3.up * 0.55f, new Vector3(wall, 1.1f, 1f),
                            facing, 0.08f);
                }

                // Bloco da quina (no canto entre este lado e o próximo)
                Vector3 corner = top + Corner(k, sides) * (radius - 1.2f);
                Quaternion cornerFacing = Quaternion.LookRotation(Corner(k, sides));
                BlockMesh.ChamferBox(s_Ruins.Pick(), corner + Vector3.up * 1.1f, new Vector3(2.2f, 2.2f, 2.2f), cornerFacing, 0.12f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), corner + Vector3.up * 2.4f, new Vector3(1.6f, 0.4f, 1.6f), cornerFacing, 0.08f);

                // Fuste: fiadas de blocos aparelhados (desencontradas) na face deste lado
                float shaftLength = 2f * shaft * Mathf.Sin(half * Mathf.Deg2Rad);
                Vector3 face = top + normal * shaftInner;
                int course = 0;
                for (float y = skirtBottom; y > masonryBottom; y -= 1.8f, course++)
                {
                    float x = -shaftLength * 0.5f - (course % 2 == 0 ? 0f : 1.6f);
                    while (x < shaftLength * 0.5f - 0.3f)
                    {
                        float w = R(2.6f, 3.6f);
                        float a0 = Mathf.Max(x, -shaftLength * 0.5f), a1 = Mathf.Min(x + w, shaftLength * 0.5f);
                        if (a1 - a0 > 0.4f)
                            BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(face.x, y - 0.9f, face.z) + along * (a0 + a1) * 0.5f +
                                                                 normal * R(-0.05f, 0.12f), new Vector3(a1 - a0 - 0.06f, 1.74f, 0.7f),
                                facing, 0.08f);
                        x += w;
                    }
                }

                // Pilastra na quina do fuste
                Vector3 shaftCorner = top + Corner(k, sides) * (shaft - 0.2f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(shaftCorner.x, (skirtBottom + masonryBottom) * 0.5f, shaftCorner.z),
                    new Vector3(2.2f, skirtBottom - masonryBottom, 2.2f), cornerFacing, 0.15f);

                // Porta escura (só cenário): batentes, verga e o vão
                if (k == doorSide)
                {
                    Vector3 door = new Vector3(face.x, top.y - 12f, face.z) + normal * 0.4f;
                    s_Ruins.Dark.AddQuadAuto(door + along * 1.3f, door - along * 1.3f, door - along * 1.3f + Vector3.up * 4f,
                        door + along * 1.3f + Vector3.up * 4f, normal, 4f);
                    foreach (float side in new[] { -1f, 1f })
                        BlockMesh.ChamferBox(s_Ruins.Pick(), door + along * side * 1.8f + Vector3.up * 2.2f + normal * 0.2f,
                            new Vector3(1f, 4.4f, 1f), facing, 0.08f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), door + Vector3.up * 4.7f + normal * 0.2f, new Vector3(4.8f, 1f, 1.1f), facing, 0.08f);
                }
            }

            // Miolo do fuste (atrás dos blocos) até dentro da rocha
            Prism(s_Ruins.Pick(), top, shaft - 0.3f, shaft - 0.3f, sides, skirtBottom, top.y - 30f, false);

            // A rocha do pico abraça a base: pilar logo abaixo, mais largo que o fuste, e penhascos em volta
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = top + Vector3.down * 13f, TopRadius = shaft * 1.12f, BaseRadius = shaft * 1.8f, Taper = 3f,
                Cap = PicosMountainKit.Cap.Crag, CapHeight = 3f, Sides = 48, RingStep = 2.5f, Corners = 8, Flutes = 5,
                FluteDepth = 0.07f, BedHeight = 12f, BedShift = 0.07f, Waist = 0.1f, Roughness = 0.06f, Seed = seed,
            });
            int crags = 7;
            for (int k = 0; k < crags; k++)
            {
                float deg = (k + R(-0.25f, 0.25f)) * 360f / crags;
                bool nearOpen = false;
                foreach (int o in open)
                    nearOpen |= Mathf.Abs(Mathf.DeltaAngle(deg, o * 360f / sides)) < 28f;
                if (nearOpen)
                    continue;
                Vector3 dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));
                float r = R(3.5f, 6.5f);
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = top + dir * (shaft + r * 0.5f) + Vector3.down * R(6f, 11f), TopRadius = r, BaseRadius = r * 2.2f,
                    Taper = 2.5f, Cap = k % 3 == 0 ? PicosMountainKit.Cap.Tilted : PicosMountainKit.Cap.Crag,
                    CapHeight = r * R(0.3f, 0.6f), Sides = 22, RingStep = 2.5f, Corners = 5 + k % 3, Flutes = 3,
                    FluteDepth = 0.08f, BedHeight = R(6f, 10f), BedShift = 0.09f, Waist = 0.12f, Roughness = 0.07f,
                    Seed = seed + k * 31, Track = false,
                });
            }

            return inner;
        }

        // Direção do meio do lado k e da quina entre o lado k e o k + 1 (ângulos a partir de +X para +Z)
        static Vector3 SideNormal(int k, int sides)
        {
            float a = k * 360f / sides * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        static Vector3 Corner(int k, int sides)
        {
            float a = (k + 0.5f) * 360f / sides * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // Prisma de planta regular (quinas a meio caminho entre os lados de SideNormal), de yTop a yBottom
        // (raio do alto e de baixo podem ser diferentes: tronco de pirâmide)
        static void Prism(MeshData mesh, Vector3 center, float radiusTop, float radiusBottom, int sides, float yTop,
            float yBottom, bool caps)
        {
            for (int k = 0; k < sides; k++)
            {
                Vector3 at = center + Corner(k - 1, sides) * radiusTop, bt = center + Corner(k, sides) * radiusTop;
                Vector3 ab = center + Corner(k - 1, sides) * radiusBottom, bb = center + Corner(k, sides) * radiusBottom;
                at.y = bt.y = yTop;
                ab.y = bb.y = yBottom;
                Vector3 slope = SideNormal(k, sides) + Vector3.up * ((radiusTop - radiusBottom) / Mathf.Max(0.01f, yTop - yBottom));
                mesh.AddQuadAuto(at, bt, bb, ab, slope.normalized, 4f);
                if (!caps)
                    continue;
                mesh.AddTriangleAuto(new Vector3(center.x, yTop, center.z), at, bt, Vector3.up, 4f);
                mesh.AddTriangleAuto(new Vector3(center.x, yBottom, center.z), ab, bb, Vector3.down, 4f);
            }
        }

        static void BuildArrivalPeak()
        {
            // A torre da chegada: aberta ao sul (escadaria), a leste (aqueduto) e a oeste (o parapeito despencou); a
            // porta escura fica virada para o norte, atrás do portal
            TowerTop(Vector3.zero, k_PlazaRadius, 11, new[] { 0, 4, 6 }, 2);
        }

        // ---------- praça ----------

        static void BuildPlaza()
        {
            // O chão, o parapeito e a cornija são do topo da torre (TowerTop); aqui ficam as colunatas e a neve

            // Colunatas partidas dos dois lados e as duas altas que ladeiam o portal
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 3; k++)
                    ArquipelagoRuinKit.Column(s_Ruins, new Vector3(s * 14f, 0f, -10f + k * 8f), R(2.5f, 6.5f), 0.8f,
                        s_Random, true);
                ArquipelagoRuinKit.Column(s_Ruins, new Vector3(s * 11.5f, 0f, 13.5f), 10f, 1f, s_Random, false);
            }

            // Montes de neve perto da borda e em volta das colunas
            for (int k = 0; k < 26; k++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(8f, k_PlazaRadius - 5f);
                var p = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (d < 15f && s_Random.NextDouble() < 0.7)
                    continue;
                Mound(s_Snow, p, R(1.5f, 4f), R(1.5f, 3.5f), R(0.4f, 1.3f), 10);
            }
        }

        // ---------- portal ----------

        // Estrado em degraus ao norte, com o anel de pedra parado, dois anéis girando em sentidos opostos e a superfície
        // acesa (também gira, devagar); monólitos com runas dos lados e a luz ciano
        static void BuildPortal()
        {
            for (int k = 0; k < 3; k++)
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(0f, 0.3f + k * 0.6f, 15.5f + k * 0.6f),
                    new Vector3(22f - k * 4f, 0.6f, 6f - k * 1.2f), Quaternion.identity, 0.08f);

            ArquipelagoRuinKit.RichRing(s_Ruins, k_PortalCenter, Vector3.forward, Vector3.up, 9.5f, 1.6f, 2f, 0f, 360f,
                s_Random, false, false, 0f, 3);

            // Pés do anel apoiados no estrado
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(s * 4.5f, 2.6f, 15.5f), new Vector3(3f, 2.8f, 2.6f),
                    Quaternion.Euler(0f, 0f, s * -25f), 0.1f);

            Spinning("Anel_Giro_1", 7.6f, 0.9f, 1.2f, 14f);
            Spinning("Anel_Giro_2", 6f, 0.6f, 0.8f, -22f);

            // Superfície acesa (disco), girando devagar
            var disc = new MeshData();
            const int sides = 40;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 5.4f;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 5.4f;
                Tri(disc, Vector3.zero, p0, p1, new Vector2(0.5f, 0.5f), new Vector2(0.5f + p0.x / 10.8f, 0.5f + p0.y / 10.8f),
                    new Vector2(0.5f + p1.x / 10.8f, 0.5f + p1.y / 10.8f), Vector3.back);
                Tri(disc, Vector3.zero, p1, p0, new Vector2(0.5f, 0.5f), new Vector2(0.5f + p1.x / 10.8f, 0.5f + p1.y / 10.8f),
                    new Vector2(0.5f + p0.x / 10.8f, 0.5f + p0.y / 10.8f), Vector3.forward);
            }
            GameObject surface = GridSceneKit.CreateObject(k_Prefix + "_Portal", "Portal_Superficie", disc,
                s_Materials.Portal, s_World, false, false, false);
            surface.transform.position = k_PortalCenter;
            var spin = surface.AddComponent<SlowRotation>();
            spin.Axis = Vector3.forward;
            spin.DegreesPerSecond = 8f;
            spin.Bob = 0f;

            // Monólitos de runas dos lados do estrado
            var runes = new MeshData();
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * 8.5f, 0f, 16f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), foot + Vector3.up * 2.5f, new Vector3(2f, 5f, 1.4f), Quaternion.identity, 0.1f);
                BlockMesh.Box(runes, foot + new Vector3(0f, 2.8f, -0.72f), new Vector3(1.4f, 3.4f, 0.04f), Quaternion.identity);
            }
            GridSceneKit.CreateObject(k_Prefix + "_Runas", "Runas", runes, s_Materials.Rune, s_World, false, false);

            Light(k_PortalCenter + Vector3.back * 2f, new Color(0.35f, 1f, 0.95f), 5f, 28f);
        }

        // Anel de pedra (sem neve: gira) com o pivô no centro do portal
        static void Spinning(string name, float radius, float thickness, float width, float degrees)
        {
            var ring = new RuinMesh(s_Random.Next());
            ArquipelagoRuinKit.RichRing(ring, Vector3.zero, Vector3.forward, Vector3.up, radius, thickness, width, 0f, 360f,
                s_Random, false, false, -100f, 2);
            Transform root = CreateRuin(name, ring, false, snow: false);
            root.position = k_PortalCenter;
            var spin = root.gameObject.AddComponent<SlowRotation>();
            spin.Axis = Vector3.forward;
            spin.DegreesPerSecond = degrees;
            spin.Bob = 0f;
        }

        // ---------- tempo parado ----------

        // A oeste o parapeito despencou: alguns blocos parados no ar no meio da queda, para fora e para baixo da praça
        // (só cenário)
        static void BuildFrozenFall()
        {
            for (int k = 0; k < 10; k++)
            {
                float t = R(0f, 1f);
                var p = new Vector3(-25f - t * 16f - R(0f, 3f), 1f - t * t * 26f + R(-1.5f, 1.5f), R(-10f, 10f));
                Quaternion rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
                if (s_Random.NextDouble() < 0.7)
                    BlockMesh.ChamferBox(s_Ruins.Pick(), p, new Vector3(R(0.8f, 2.4f), R(0.6f, 1.2f), R(0.8f, 1.8f)), rotation, 0.06f);
                else
                    Spike(s_Ruins.Pick(), p, rotation * Vector3.up, R(0.8f, 1.6f), R(0.5f, 0.9f), 8, 0f, 0f, true);
            }
        }

        // ---------- aqueduto e cachoeira congelada ----------

        // Canal de pedra saindo da praça para leste sobre arcos, quebrado na ponta; a água que caía virou gelo pela parede
        static void BuildAqueduct()
        {
            for (int k = 0; k < 4; k++)
            {
                float x = 23f + k * 3f;
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(x, -0.4f, 4f), new Vector3(3f, 0.6f, 3.2f), Quaternion.identity, 0.05f);
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(x, 0.3f, 4f + s * 1.4f), new Vector3(3f, 0.9f, 0.4f),
                        Quaternion.identity, 0.05f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(x, -4f, 4f), new Vector3(1f, 6.5f, 1.2f), Quaternion.identity, 0.05f);
            }

            // Lâmina de gelo caindo da ponta do canal, ondulada, até sumir nas nuvens; pingentes na borda
            Vector3 start = new Vector3(34f, 0f, 4f);
            const int rows = 30;
            for (int i = 0; i < rows; i++)
            {
                float t0 = i / (float)rows, t1 = (i + 1) / (float)rows;
                Vector3 c0 = start + new Vector3(Mathf.Sqrt(t0) * 9f, -t0 * 90f, 0f);
                Vector3 c1 = start + new Vector3(Mathf.Sqrt(t1) * 9f, -t1 * 90f, 0f);
                float w0 = 1.2f + t0 * 3f + Mathf.Sin(i * 1.7f) * 0.3f, w1 = 1.2f + t1 * 3f + Mathf.Sin((i + 1) * 1.7f) * 0.3f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 bulge = Vector3.right * 0.6f;
                    Quad(s_Ice, c0 + Vector3.forward * w0 * s, c1 + Vector3.forward * w1 * s, c1 + bulge, c0 + bulge,
                        Vector3.right + Vector3.forward * s * 0.3f, t0 * 22f, t1 * 22f);
                }
            }

            for (int k = 0; k < 12; k++)
                Spike(s_Ice, new Vector3(34f + R(0f, 1.5f), -0.2f, 4f + R(-1.4f, 1.4f)), Vector3.down, R(0.8f, 2.4f),
                    R(0.12f, 0.3f), 5, 0f, 3f);
        }

        // ---------- crista e ponte ----------

        // Escadaria escavada saindo da praça para o sul, a trilha da crista descendo por cima de um espinhaço de rocha até
        // a sela e a ponte de pedra até o pico da próxima arena
        static void BuildCrest()
        {
            float edge = k_PlazaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            Stairs(new Vector3(0f, 0f, -edge), new Vector3(0f, -9f, -edge - 15f), 5f);

            Vector3[] trail =
            {
                new Vector3(0f, -9f, -k_PlazaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad) - 15f), new Vector3(-6f, -11f, -48f),
                new Vector3(-12f, -13f, -60f),
                new Vector3(-14f, -14f, -76f),
            };
            Trail(trail, 18);
            ClearLine(trail[0], trail[trail.Length - 1]);
            BuildApproach(trail[trail.Length - 1]);
        }

        // ---------- caminho longo até a arena 1 ----------

        // Do fim da crista até a arena 1, um caminho longo e sinuoso por plataformas menores (ressaltos de rocha com
        // calçamento partido e torrezinhas esculpidas), subindo e descendo, ligadas por uma ponte de pedra, pontes de
        // corrente e trilhas de crista; a última ponte chega na entrada norte da arena
        static void BuildApproach(Vector3 start)
        {
            var ledge1 = new Vector3(-34f, -16f, -125f);
            var tower1 = new Vector3(-18f, -11f, -185f);
            var ledge2 = new Vector3(22f, -17f, -235f);
            var tower2 = new Vector3(38f, -13f, -300f);
            var ledge3 = new Vector3(12f, -19f, -358f);
            Vector3 arena = A(0f, 0f, k_ArenaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad) - 0.3f);

            RockLedge(ledge1, 9f, 601);
            Bridge(start, Edge(ledge1, 9f, start), 5f);
            ClearLine(start, ledge1);

            float t1 = SmallTower(tower1, 8f, 611, ledge1, ledge2);
            ChainBridge(Edge(ledge1, 9f, tower1), TowerEdge(tower1, t1, ledge1), 3.4f, 2.5f);
            ClearLine(ledge1, tower1);

            RockLedge(ledge2, 10f, 621);
            Trail(new[] { TowerEdge(tower1, t1, ledge2), (TowerEdge(tower1, t1, ledge2) + Edge(ledge2, 10f, tower1)) * 0.5f + Vector3.left * 6f,
                Edge(ledge2, 10f, tower1) }, 8);
            ClearLine(tower1, ledge2);

            float t2 = SmallTower(tower2, 9f, 631, ledge2, ledge3);
            Bridge(Edge(ledge2, 10f, tower2), TowerEdge(tower2, t2, ledge2), 5f);
            ClearLine(ledge2, tower2);

            RockLedge(ledge3, 8f, 641);
            ChainBridge(TowerEdge(tower2, t2, ledge3), Edge(ledge3, 8f, tower2), 3.4f, 2.5f);
            ClearLine(tower2, ledge3);

            Bridge(Edge(ledge3, 8f, arena), arena, 5f);
            ClearLine(ledge3, arena);
            Clear(k_NextPeak, k_ArenaRadius + 40f);
        }

        // Ponto na beira do calçamento de um ressalto, virado para 'toward'
        static Vector3 Edge(Vector3 center, float radius, Vector3 toward)
        {
            Vector3 dir = toward - center;
            dir.y = 0f;
            return center + dir.normalized * radius * 0.72f;
        }

        // Ponto na beira do topo de uma torre (raio de dentro 'inner'), virado para 'toward'
        static Vector3 TowerEdge(Vector3 center, float inner, Vector3 toward)
        {
            Vector3 dir = toward - center;
            dir.y = 0f;
            return center + dir.normalized * (inner - 0.3f);
        }

        // Lado da planta oitavada da torre (0 = leste, 2 = norte...) mais virado para 'toward'
        static int SideToward(Vector3 center, Vector3 toward) =>
            Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(toward.z - center.z, toward.x - center.x) * Mathf.Rad2Deg, 360f) / 45f) % 8;

        // Torrezinha esculpida de passagem (TowerTop pequena, aberta para quem chega e para onde se vai), com uma coluna
        // partida e neve no topo; devolve o raio de dentro
        static float SmallTower(Vector3 center, float radius, int seed, Vector3 from, Vector3 to)
        {
            int a = SideToward(center, from), b = SideToward(center, to);
            float inner = TowerTop(center, radius, seed, new[] { a, b }, (a + 4) % 8 == b ? (a + 2) % 8 : (a + 4) % 8);
            Vector3 dir = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            ArquipelagoRuinKit.Column(s_Ruins, center + dir * inner * 0.45f, R(2f, 4f), 0.7f, s_Random, true);
            for (int k = 0; k < 3; k++)
                Mound(s_Snow, center + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * inner * R(0.5f, 0.8f),
                    R(1f, 2.2f), R(1f, 2.2f), R(0.3f, 0.7f), 8);
            Clear(center, radius + 30f);
            return inner;
        }

        // Ressalto natural: o topo de um pilar de rocha com um calçamento partido por cima (a rocha aparece em volta),
        // neve, uma ruína pequena e pedras
        static void RockLedge(Vector3 center, float radius, int seed)
        {
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = center + Vector3.down * 0.5f, TopRadius = radius, BaseRadius = radius * 2f, Taper = 2.5f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 30, RingStep = 2.5f, Corners = 6, Flutes = 4, FluteDepth = 0.07f,
                BedHeight = R(8f, 12f), BedShift = 0.07f, Waist = 0.1f, Roughness = 0.06f, Seed = seed,
            });
            var slab = new MeshData();
            Prism(slab, center, radius * 0.75f, radius * 0.75f, 7, center.y, center.y - 0.8f, true);
            SplitTops(slab, s_Floor, s_Ruins.Tones[0], 4f);

            Vector3 dir = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            if (s_Random.NextDouble() < 0.5)
                ArquipelagoRuinKit.Column(s_Ruins, center + dir * radius * 0.4f, R(1.5f, 3.5f), 0.7f, s_Random, true);
            else
                ArquipelagoRuinKit.AshlarWall(s_Ruins, center + dir * radius * 0.45f, Quaternion.LookRotation(dir), R(2.5f, 4f),
                    R(0.8f, 1.6f), 0.8f, s_Random);
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = center + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * radius * R(0.55f, 0.8f);
                if (k == 0)
                    ArquipelagoRuinKit.IrregularRock(s_Ruins, at + Vector3.up * 0.3f, R(0.6f, 1.2f), s_Random);
                else
                    Mound(s_Snow, at, R(1f, 2.4f), R(1f, 2.4f), R(0.3f, 0.8f), 8);
            }

            Clear(center, radius + 30f);
        }

        // Marca uma área do caminho (as montanhas de longe não entram nela)
        static void Clear(Vector3 center, float radius) => s_Clearings.Add((new Vector2(center.x, center.z), radius));

        // Marca o caminho entre dois pontos (de 25 em 25 m)
        static void ClearLine(Vector3 a, Vector3 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 25f));
            for (int i = 0; i <= steps; i++)
                Clear(Vector3.Lerp(a, b, i / (float)steps), 30f);
        }

        // Trilha de neve pisada pelos pontos, sobre um espinhaço de rocha (pilares estreitos com o topo logo abaixo da
        // trilha), com pedras e montes de neve na beira
        static void Trail(Vector3[] trail, int extras)
        {
            for (int i = 0; i + 1 < trail.Length; i++)
            {
                Path(trail[i], trail[i + 1], 4.5f);
                for (float t = 0f; t < 1f; t += 0.34f)
                {
                    Vector3 p = Vector3.Lerp(trail[i], trail[i + 1], t);
                    float r = R(3.8f, 5f);
                    PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                    {
                        Top = p + Vector3.down * 0.6f, TopRadius = r, BaseRadius = r * R(2f, 2.6f), Taper = 2.5f,
                        Cap = PicosMountainKit.Cap.Flat, Sides = 20, RingStep = 2.5f, Corners = 5, Flutes = 3,
                        FluteDepth = 0.08f, BedHeight = R(8f, 12f), BedShift = 0.08f, Waist = 0.12f, Roughness = 0.08f,
                        Seed = R(0f, 1000f),
                    });
                }
            }

            for (int k = 0; k < extras; k++)
            {
                int i = s_Random.Next(trail.Length - 1);
                Vector3 p = Vector3.Lerp(trail[i], trail[i + 1], R(0f, 1f));
                Vector3 side = Vector3.Cross(Vector3.up, trail[i + 1] - trail[i]).normalized * (s_Random.NextDouble() < 0.5 ? -1f : 1f);
                Vector3 at = p + side * R(2.6f, 3.4f);
                if (s_Random.NextDouble() < 0.5)
                    ArquipelagoRuinKit.IrregularRock(s_Ruins, at + Vector3.up * 0.4f, R(0.8f, 1.6f), s_Random);
                else
                    Mound(s_Snow, at, R(1f, 2f), R(1f, 2f), R(0.4f, 0.9f), 8);
            }
        }

        // Ponte de pedra: tabuleiro de blocos, meio-fio partido, dois pilares até as nuvens e pingentes embaixo
        static void Bridge(Vector3 from, Vector3 to, float width)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            Quaternion rotation = Quaternion.LookRotation(flat);
            float length = flat.magnitude;
            int blocks = Mathf.CeilToInt(length / 3f);
            Vector3 right = rotation * Vector3.right;
            for (int b = 0; b < blocks; b++)
            {
                float t = (b + 0.5f) / blocks;
                Vector3 c = Vector3.Lerp(from, to, t);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c + Vector3.down * 0.5f, new Vector3(width, 1f, length / blocks + 0.02f),
                    rotation, 0.06f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c + Vector3.down * 1.6f, new Vector3(width * 0.8f, 1.2f, length / blocks),
                    rotation, 0.06f);
                foreach (float s in new[] { -1f, 1f })
                {
                    if (s_Random.NextDouble() < 0.3)
                        continue;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), c + right * s * (width * 0.5f - 0.25f) + Vector3.up * 0.35f,
                        new Vector3(0.5f, 0.7f, length / blocks * R(0.6f, 0.95f)), rotation, 0.05f);
                }

                if (s_Random.NextDouble() < 0.6)
                    Spike(s_Ice, c + right * R(-width * 0.4f, width * 0.4f) + Vector3.down * 2.2f, Vector3.down,
                        R(0.6f, 2.4f), R(0.12f, 0.3f), 5, 0f, 3f);
            }

            foreach (float t in new[] { 0.33f, 0.67f })
            {
                Vector3 c = Vector3.Lerp(from, to, t);
                float height = c.y - k_CloudY + 50f;
                ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), c + Vector3.down * (2.2f + height * 0.5f),
                    new Vector2(width * 0.7f, 3f), height, rotation, 0.1f);
            }
        }

        // ---------- próxima arena (esboço) ----------

        // Ponto relativo ao centro do chão da arena 1
        static Vector3 A(float x, float y, float z) => k_NextPeak + new Vector3(x, y, z);

        // Sala 17, Terraços Partidos: no topo da torre esculpida do pico ao sul (TowerTop, 76 m de quina a quina). Praça
        // central de lajes e três terraços em ruína em setores de anel ligados por escadarias (oeste a +3 m, sudoeste a
        // +1,5 m, a um degrau do oeste, e sudeste a +5,5 m com um pequeno templo), muros e colunas partidos para cobertura.
        // Entrada ao norte sob um arco, onde a ponte da crista chega; saída ao sul (para a sala 18). Algumas pedras paradas
        // no ar em volta, só de cenário. Sem inimigos por enquanto (os do bioma ainda não existem): a sala tem gatilho,
        // domo, NavMesh e pontos de spawn, mas nenhuma ficha de combate.
        static void BuildArena()
        {
            float inner = TowerTop(k_NextPeak, k_ArenaRadius, 23, new[] { 2, 6 }, 0);

            // Terraços: setores de anel (topo vira piso de lajes)
            var slab = new MeshData();
            Sector(slab, k_NextPeak, 21f, k_TerraceOuter, 140f, 215f, 3f);
            Sector(slab, k_NextPeak, 21f, k_TerraceOuter, 215f, 255f, 1.5f);
            Sector(slab, k_NextPeak, 18f, k_TerraceOuter, 285f, 345f, 5.5f);
            SplitTops(slab, s_Floor, s_Ruins.Tones[0], 4f);

            Stairs(Polar(180f, 15f, 0f), Polar(180f, 21f, 3f), 5f);
            Stairs(Polar(235f, 16f, 0f), Polar(235f, 21f, 1.5f), 5f);
            Stairs(Polar(315f, 9f, 0f), Polar(315f, 18f, 5.5f), 6f);

            // Entrada sob um arco, ladeada por duas colunas inteiras
            ArquipelagoRuinKit.Arch(s_Ruins, A(0f, 0f, inner - 3f), 0f, 0.9f, s_Random);
            foreach (float x in new[] { -6f, 6f })
                ArquipelagoRuinKit.Column(s_Ruins, A(x, 0f, inner - 6f), 9f, 0.9f, s_Random, false);

            // Templo no terraço sudeste: colunata em volta de um altar, com o muro do fundo
            foreach (float deg in new[] { 295f, 315f, 335f })
            {
                ArquipelagoRuinKit.Column(s_Ruins, Polar(deg, 21.5f, 5.5f), deg == 315f ? 7f : R(3f, 6f), 0.8f, s_Random, deg != 315f);
                ArquipelagoRuinKit.Column(s_Ruins, Polar(deg, 30f, 5.5f), deg == 315f ? R(3f, 6f) : 7f, 0.8f, s_Random, deg == 315f);
            }
            ArquipelagoRuinKit.AshlarWall(s_Ruins, Polar(315f, 32f, 5.5f), Quaternion.Euler(0f, -(315f + 90f), 0f), 14f, 4.5f, 1f,
                s_Random);
            BlockMesh.ChamferBox(s_Ruins.Pick(), Polar(315f, 26f, 6.1f), new Vector3(3f, 1.2f, 2f),
                Quaternion.Euler(0f, -(315f + 90f), 0f), 0.08f);

            // Cobertura na praça central e nos terraços: muros partidos, colunas partidas e pedras
            (Vector3 at, float yaw, float length, float height)[] walls =
            {
                (A(-2f, 0f, 12f), 20f, 6f, 1.7f), (A(7f, 0f, -1f), -35f, 5f, 1.4f), (A(-5f, 0f, -6f), 70f, 4.5f, 2.2f),
                (A(12f, 0f, 18f), 0f, 4f, 1.2f), (A(-24f, 3f, 6f), 90f, 5f, 1.6f), (A(-17f, 1.5f, -22f), 40f, 5f, 1.3f),
            };
            foreach (var wall in walls)
                ArquipelagoRuinKit.AshlarWall(s_Ruins, wall.at, Quaternion.Euler(0f, wall.yaw, 0f), wall.length, wall.height,
                    0.9f, s_Random);
            foreach (Vector3 at in new[] { A(4f, 0f, 14f), A(-8f, 0f, 21f), A(10f, 0f, 8f), A(-2f, 0f, -16f), A(8f, 0f, -14f),
                         A(-27f, 3f, -10f) })
                ArquipelagoRuinKit.Column(s_Ruins, at, R(2f, 5f), 0.8f, s_Random, true);
            for (int k = 0; k < 6; k++)
            {
                float a = R(0f, 360f), d = R(4f, 16f);
                ArquipelagoRuinKit.IrregularRock(s_Ruins, Polar(a, d, 0.4f), R(0.8f, 1.5f), s_Random);
            }

            // Algumas pedras paradas no ar em volta da torre (só cenário, fora do alcance)
            for (int k = 0; k < 8; k++)
            {
                float a = R(0f, 360f), d = R(k_ArenaRadius + 6f, k_ArenaRadius + 22f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), Polar(a, d, R(4f, 16f)), new Vector3(R(0.8f, 2.4f), R(0.6f, 1.6f), R(0.8f, 2.2f)),
                    Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)), 0.06f);
            }

            // Montes de neve perto da borda e encostados nos muros
            for (int k = 0; k < 40; k++)
            {
                float a = R(0f, 360f), d = R(10f, inner - 3f);
                Vector3 p = Polar(a, d, 0f) - k_NextPeak;
                if (d < 25f && s_Random.NextDouble() < 0.8)
                    continue;
                p.y = FloorHeight(p.x, p.z);
                Mound(s_Snow, k_NextPeak + p, R(1.5f, 3.5f), R(1.5f, 3.5f), R(0.3f, 1f), 10);
            }
        }

        const float k_TerraceOuter = 32.5f;     // os terraços vão até perto do parapeito

        // Ponto da arena 1 pelo ângulo (graus, de +X para +Z), distância do centro e altura
        static Vector3 Polar(float deg, float distance, float height) =>
            k_NextPeak + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * distance, height, Mathf.Sin(deg * Mathf.Deg2Rad) * distance);

        // Terraço em setor de anel (de 'from' a 'to' graus, entre os raios), do chão até 'height'
        static void Sector(MeshData mesh, Vector3 center, float inner, float outer, float from, float to, float height)
        {
            int steps = Mathf.CeilToInt((to - from) / 4f);
            Vector3 P(float deg, float r, float y) =>
                center + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
            for (int i = 0; i < steps; i++)
            {
                float a0 = from + (to - from) * i / steps, a1 = from + (to - from) * (i + 1) / steps, mid = (a0 + a1) * 0.5f;
                Vector3 radial = P(mid, 1f, 0f) - center;
                mesh.AddQuadAuto(P(a0, inner, height), P(a1, inner, height), P(a1, outer, height), P(a0, outer, height), Vector3.up, 4f);
                mesh.AddQuadAuto(P(a0, outer, height), P(a1, outer, height), P(a1, outer, -0.2f), P(a0, outer, -0.2f), radial, 4f);
                mesh.AddQuadAuto(P(a0, inner, height), P(a1, inner, height), P(a1, inner, -0.2f), P(a0, inner, -0.2f), -radial, 4f);
            }

            foreach (float deg in new[] { from, to })
            {
                Vector3 tangent = P(deg + 90f, 1f, 0f) - center;
                mesh.AddQuadAuto(P(deg, inner, height), P(deg, outer, height), P(deg, outer, -0.2f), P(deg, inner, -0.2f),
                    deg == from ? -tangent : tangent, 4f);
            }
        }

        // Altura do chão da arena 1 num ponto (relativo ao centro): praça ou um dos terraços
        static float FloorHeight(float x, float z)
        {
            float r = new Vector2(x, z).magnitude, a = Mathf.Repeat(Mathf.Atan2(z, x) * Mathf.Rad2Deg, 360f);
            if (r >= 21f && r <= k_TerraceOuter && a >= 140f && a < 215f)
                return 3f;
            if (r >= 21f && r <= k_TerraceOuter && a >= 215f && a < 255f)
                return 1.5f;
            if (r >= 18f && r <= k_TerraceOuter && a >= 285f && a < 345f)
                return 5.5f;
            return 0f;
        }

        // Sala de combate da arena 1: gatilho começando 5 m depois da entrada ao norte
        static void BuildArenaRoom()
        {
            float inner = k_ArenaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad), north = inner - 5f;
            var trigger = new Bounds(k_NextPeak + new Vector3(0f, 12f, (north - inner) * 0.5f), new Vector3(inner * 2f, 40f, north + inner));
            BuildRoom("Arena_TerracosPartidos", "Terraços Partidos", 17, k_NextPeak, trigger, A(0f, 0f, inner),
                k_NextPeak.y - 1f, k_NextPeak.y + 6f);
        }

        // Sala de combate dos Picos: gatilho em caixa ('trigger', no mundo), RoomManager (bioma 2, de 8 salas), NavMesh da
        // área e pontos de spawn no chão entre 'minY' e 'maxY' (raio para baixo numa grade), longe da entrada, no NavMesh
        // e com ~2 m livres em volta. Sem CombatBalance: o domo existe, mas a sala não tranca nem chama inimigos até o
        // bioma ter os dele.
        static RoomManager BuildRoom(string objectName, string roomName, int number, Vector3 floor, Bounds trigger,
            Vector3 entrance, float minY, float maxY)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(s_World, false);
            root.transform.position = floor;

            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = trigger.center - floor;
            box.size = trigger.size;

            var room = root.AddComponent<RoomManager>();
            room.RoomName = roomName;
            room.Doors = new RoomDoor[0];
            room.HighSpawnPoints = new HighSpawnPoint[0];
            room.Balance = null;    // sem inimigos até o bioma ter os dele
            room.BiomeIndex = 2;
            room.ArenaNumber = number;
            room.ArenaInBiome = number - 16;    // os Picos começam na sala 17
            room.ArenasInBiome = 8;
            room.IsBoss = false;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            var surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = trigger.center - floor;
            surface.size = trigger.size + new Vector3(8f, 10f, 8f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            GridSceneKit.BakeNavMesh(surface, $"{IcaroAssetUtility.ScenesFolder}/Picos_Teste_NavMesh", $"Arena_{number - 16}");

            var points = new List<Vector3>();
            for (float x = trigger.min.x + 3f; x <= trigger.max.x - 3f && points.Count < 12; x += 3f)
            for (float z = trigger.min.z + 3f; z <= trigger.max.z - 3f && points.Count < 12; z += 3f)
            {
                var above = new Vector3(x + R(-1f, 1f), trigger.max.y, z + R(-1f, 1f));
                if (!Physics.Raycast(above, Vector3.down, out RaycastHit ground, trigger.size.y + 20f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (ground.point.y < minY || ground.point.y > maxY || Vector3.Distance(ground.point, entrance) < 22f)
                    continue;
                if (!NavMesh.SamplePosition(ground.point + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    continue;
                if (points.Exists(p => Vector3.Distance(p, hit.position) < 9f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                points.Add(hit.position);
            }

            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root.transform, false);
            var transforms = new List<Transform>();
            foreach (Vector3 position in points)
            {
                var point = new GameObject($"Spawn_{transforms.Count + 1:00}").transform;
                point.SetParent(spawns, false);
                point.position = position;
                transforms.Add(point);
            }

            room.SpawnPoints = transforms.ToArray();
            Debug.Log($"[Ícaro] Picos: sala {number} {roomName} com {points.Count} pontos de spawn (sem inimigos por enquanto).");
            return room;
        }

        // ---------- sala 18: Sela da Crista ----------

        // A sela entre dois picos: da saída sul da arena 1, uma escadaria e a trilha da crista descem até a sela, uma laje
        // em estádio (66 x 30 m, piso de lajes, borda de cantaria e uma saia mais estreita embaixo) sobre a rocha, com uma
        // torre baixa em cada ponta (TowerTop: oeste a +7 m e leste a +8 m, abertas para a sela, com escadarias subindo
        // dos dois lados). No meio, um santuário: colunata em volta de um pedestal com um anel de pedra em pé, partido no
        // alto. Parapeito partido nas beiras compridas (aberto ao norte, por onde se chega, e ao sul, onde sai a ponte de
        // corrente), muros e pedras de cobertura, montes de neve e pingentes de gelo. Ao sul, a
        // ponte de corrente até o patamar da sala 19.
        static void BuildSaddle()
        {
            // Da arena 1 até a sela: escadaria, trilha da crista até um ressalto, ponte de pedra até uma torrezinha e a
            // ponte de corrente até a entrada norte da sela
            Vector3 exit = A(0f, 0f, -k_ArenaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad));
            Vector3 trailStart = exit + new Vector3(0f, -4f, -13f);
            Stairs(exit, trailStart, 5f);
            var ledge = new Vector3(-34f, -21f, -525f);
            var tower = new Vector3(-12f, -17f, -590f);
            Vector3 saddleNorth = new Vector3(k_Saddle.x, k_Saddle.y, k_Saddle.z + k_SaddleRadius - 0.3f);
            RockLedge(ledge, 9f, 651);
            Trail(new[] { trailStart, (trailStart + Edge(ledge, 9f, trailStart)) * 0.5f + Vector3.right * 5f, Edge(ledge, 9f, trailStart) }, 8);
            ClearLine(exit, ledge);
            float inner = SmallTower(tower, 8f, 661, ledge, saddleNorth);
            Bridge(Edge(ledge, 9f, tower), TowerEdge(tower, inner, ledge), 5f);
            ClearLine(ledge, tower);
            ChainBridge(TowerEdge(tower, inner, saddleNorth), saddleNorth, 3.4f, 3f);
            ClearLine(tower, saddleNorth);
            Clear(k_Saddle, 80f);

            // Rocha embaixo da sela
            for (int k = 0; k < 4; k++)
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = new Vector3(k_Saddle.x - 27f + k * 18f, k_Saddle.y - 8f, k_Saddle.z + R(-3f, 3f)), TopRadius = 14f,
                    BaseRadius = 24f, Taper = 2.5f, Cap = PicosMountainKit.Cap.Crag, CapHeight = 2f, Sides = 36,
                    RingStep = 2.5f, Corners = 6, Flutes = 4, FluteDepth = 0.08f, BedHeight = R(10f, 14f), BedShift = 0.08f,
                    Waist = 0.1f, Roughness = 0.07f, Seed = 300 + k * 13,
                });

            // Laje da sela
            var slab = new MeshData();
            Stadium(slab, k_Saddle, k_SaddleHalf, k_SaddleRadius, k_Saddle.y, k_Saddle.y - 2.2f);
            SplitTops(slab, s_Floor, s_Ruins.Tones[0], 4f);
            Stadium(s_Ruins.Pick(), k_Saddle, k_SaddleHalf, k_SaddleRadius - 1.5f, k_Saddle.y - 2.2f, k_Saddle.y - 6f);

            // Torres das pontas e as escadarias que sobem da sela
            float west = TowerTop(k_TowerWest, k_TowerWestRadius, 41, new[] { 0 }, 4);
            float east = TowerTop(k_TowerEast, k_TowerEastRadius, 43, new[] { 4 }, 0);
            Stairs(new Vector3(k_Saddle.x - 11f, k_Saddle.y, k_TowerWest.z), new Vector3(k_TowerWest.x + west, k_TowerWest.y, k_TowerWest.z), 5f);
            Stairs(new Vector3(k_Saddle.x + 12f, k_Saddle.y, k_TowerEast.z), new Vector3(k_TowerEast.x - east, k_TowerEast.y, k_TowerEast.z), 5f);

            // Santuário do anel no meio
            BlockMesh.ChamferBox(s_Ruins.Pick(), k_Saddle + Vector3.up * 0.3f, new Vector3(5f, 0.6f, 5f), Quaternion.identity, 0.08f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), k_Saddle + Vector3.up * 0.85f, new Vector3(3.6f, 0.5f, 3.6f), Quaternion.identity, 0.08f);
            ArquipelagoRuinKit.RichRing(s_Ruins, k_Saddle + Vector3.up * 4.2f, Vector3.forward, Vector3.up, 3f, 0.55f, 0.7f, 28f, 332f,
                s_Random, true, true, k_Saddle.y, 3);
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                ArquipelagoRuinKit.Column(s_Ruins, k_Saddle + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7f,
                    k % 3 == 0 ? 6.5f : R(2f, 4.5f), 0.7f, s_Random, k % 3 != 0);
            }

            // Parapeito partido nas beiras compridas (aberto no meio: chegada ao norte, ponte ao sul)
            foreach (float side in new[] { -1f, 1f })
            {
                for (float x = k_Saddle.x - k_SaddleHalf - 4f; x <= k_Saddle.x + k_SaddleHalf + 4f; x += 6f)
                {
                    if (Mathf.Abs(x - k_Saddle.x) < 5f || s_Random.NextDouble() < 0.25)
                        continue;
                    ArquipelagoRuinKit.AshlarWall(s_Ruins, new Vector3(x, k_Saddle.y, k_Saddle.z + side * (k_SaddleRadius - 0.8f)),
                        Quaternion.identity, R(3.5f, 5.5f), R(0.8f, 1.5f), 0.9f, s_Random);
                }
            }

            // Cobertura: muros partidos e pedras
            (Vector3 at, float yaw, float length, float height)[] walls =
            {
                (new Vector3(-33f, 0f, -274f), 25f, 5f, 1.6f), (new Vector3(-8f, 0f, -289f), -20f, 4.5f, 1.4f),
                (new Vector3(-31f, 0f, -290f), 70f, 4f, 2f), (new Vector3(-9f, 0f, -272f), 0f, 5f, 1.2f),
            };
            foreach (var wall in walls)
                ArquipelagoRuinKit.AshlarWall(s_Ruins, new Vector3(wall.at.x, k_Saddle.y, wall.at.z), Quaternion.Euler(0f, wall.yaw, 0f),
                    wall.length, wall.height, 0.9f, s_Random);
            for (int k = 0; k < 5; k++)
            {
                var p = new Vector3(k_Saddle.x + R(-20f, 20f), k_Saddle.y + 0.4f, k_Saddle.z + R(-11f, 11f));
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(k_Saddle.x, k_Saddle.z)) < 9f)
                    continue;
                ArquipelagoRuinKit.IrregularRock(s_Ruins, p, R(0.8f, 1.5f), s_Random);
            }

            // Neve encostada nas beiras e pingentes de gelo na borda da laje
            for (int k = 0; k < 30; k++)
            {
                var p = new Vector3(k_Saddle.x + R(-k_SaddleHalf - 6f, k_SaddleHalf + 6f), k_Saddle.y, k_Saddle.z + R(-k_SaddleRadius + 1.5f, k_SaddleRadius - 1.5f));
                if (Mathf.Abs(p.z - k_Saddle.z) < 9f && s_Random.NextDouble() < 0.85)
                    continue;
                Mound(s_Snow, p, R(1.2f, 3f), R(1.2f, 3f), R(0.3f, 0.9f), 10);
            }
            foreach (Vector2 edge in StadiumOutline(k_SaddleHalf, k_SaddleRadius + 0.1f, 6))
            {
                if (s_Random.NextDouble() < 0.5)
                    continue;
                Spike(s_Ice, new Vector3(k_Saddle.x + edge.x, k_Saddle.y - 2.3f, k_Saddle.z + edge.y), Vector3.down, R(0.5f, 2.6f),
                    R(0.1f, 0.3f), 5, R(0f, 60f), 3f);
            }

            // Patamar ao sul (entrada da sala 19) e a ponte de corrente, longa, até ele
            float landing = TowerTop(k_Landing, k_LandingRadius, 47, new[] { 2, 6 }, 0);
            Vector3 bridgeStart = new Vector3(k_Saddle.x, k_Saddle.y, k_Saddle.z - k_SaddleRadius + 0.3f);
            Vector3 bridgeEnd = new Vector3(k_Landing.x, k_Landing.y, k_Landing.z + landing - 0.3f);
            ChainBridge(bridgeStart, bridgeEnd, 3.6f, 4.5f);
            ClearLine(bridgeStart, bridgeEnd);
            Clear(k_Landing, 45f);
        }

        // Contorno do estádio (meio comprimento reto 'half', pontas de raio 'radius' no eixo X), relativo ao centro, com
        // 'steps' pontos por meia-volta; em volta no sentido anti-horário visto de cima
        static List<Vector2> StadiumOutline(float half, float radius, int steps)
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float a = (-90f + 180f * i / steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(half + Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i <= steps; i++)
            {
                float a = (90f + 180f * i / steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(-half + Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            return points;
        }

        // Laje em estádio de yTop a yBottom, com tampa em cima e embaixo
        static void Stadium(MeshData mesh, Vector3 center, float half, float radius, float yTop, float yBottom)
        {
            List<Vector2> outline = StadiumOutline(half, radius, 12);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 at = new Vector3(center.x + a.x, yTop, center.z + a.y), bt = new Vector3(center.x + b.x, yTop, center.z + b.y);
                Vector3 ab = new Vector3(at.x, yBottom, at.z), bb = new Vector3(bt.x, yBottom, bt.z);
                Vector2 mid = (a + b) * 0.5f;
                Vector2 inner = new Vector2(Mathf.Clamp(mid.x, -half, half), 0f);
                Vector2 outward = (mid - inner).normalized;
                mesh.AddQuadAuto(at, bt, bb, ab, new Vector3(outward.x, 0f, outward.y), 4f);
                mesh.AddTriangleAuto(new Vector3(center.x + inner.x, yTop, center.z), at, bt, Vector3.up, 4f);
                mesh.AddTriangleAuto(new Vector3(center.x + inner.x, yBottom, center.z), ab, bb, Vector3.down, 4f);
            }
        }

        // Ponte de corrente de 'from' a 'to' (na altura do tabuleiro nas pontas), com o tabuleiro cedendo 'sag' metros no
        // meio: pilares de ancoragem de pedra com capitel e argola em cada canto, duas correntes de elos alternados (o
        // corrimão: 4,2 m nas pontas, 1,1 m no meio), pendurais de ferro a cada 2,5 m, tábuas de pedra sobre duas
        // longarinas de ferro (três faltando, vãos pequenos), pingentes de gelo embaixo e uma tábua que caiu e parou no
        // ar. Colisão invisível: passarela em trechos e muretas dos lados (não dá para cair pelas correntes).
        static void ChainBridge(Vector3 from, Vector3 to, float width, float sag)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            float length = flat.magnitude;
            Vector3 dir = flat.normalized, right = Vector3.Cross(Vector3.up, dir);
            Quaternion facing = Quaternion.LookRotation(dir);
            const float postHeight = 4.2f, railMid = 1.1f;
            float edge = width * 0.5f + 0.35f;
            Vector3 Deck(float t) => Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            Vector3 Chain(float t, float side) => Vector3.Lerp(from, to, t) + right * side * edge +
                                                  Vector3.up * (postHeight - (sag + postHeight - railMid) * 4f * t * (1f - t));

            // Pilares de ancoragem
            foreach ((Vector3 end, Vector3 back) in new[] { (from, -dir), (to, dir) })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 post = end + right * side * edge + back * 0.4f;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), post + Vector3.up * 0.5f, new Vector3(1.7f, 1.4f, 1.7f), facing, 0.1f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), post + Vector3.up * (postHeight * 0.5f + 0.4f), new Vector3(1f, postHeight, 1f),
                        facing, 0.08f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), post + Vector3.up * (postHeight + 0.55f), new Vector3(1.4f, 0.5f, 1.4f),
                        facing, 0.08f);
                    ChainLink(post + Vector3.up * postHeight - back * 0.55f, -back, right, 0.7f);
                }
            }

            // Tábuas de pedra e pingentes
            int planks = Mathf.RoundToInt(length / 0.95f);
            var missing = new HashSet<int> { planks * 3 / 10, planks * 11 / 20, planks * 4 / 5 };
            for (int i = 0; i < planks; i++)
            {
                if (missing.Contains(i))
                    continue;
                Vector3 a = Deck(i / (float)planks), b = Deck((i + 1) / (float)planks);
                Quaternion tilt = Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 0f, R(-1.5f, 1.5f));
                BlockMesh.ChamferBox(s_Ruins.Pick(), (a + b) * 0.5f + Vector3.down * 0.11f,
                    new Vector3(width * R(0.92f, 1f), 0.22f, (b - a).magnitude - 0.08f), tilt, 0.03f);
                if (s_Random.NextDouble() < 0.25)
                    Spike(s_Ice, (a + b) * 0.5f + Vector3.down * 0.25f + right * R(-width * 0.4f, width * 0.4f), Vector3.down,
                        R(0.3f, 1.4f), R(0.06f, 0.15f), 5, 0f, 3f);
            }

            // Longarinas e pendurais de ferro
            int segments = Mathf.CeilToInt(length / 2f);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
                foreach (float side in new[] { -1f, 1f })
                    Bar(Deck(t0) + right * side * (width * 0.5f - 0.25f) + Vector3.down * 0.34f,
                        Deck(t1) + right * side * (width * 0.5f - 0.25f) + Vector3.down * 0.34f, 0.18f, 0.24f);
            }
            int hangers = Mathf.RoundToInt(length / 2.5f);
            for (int i = 1; i < hangers; i++)
            {
                float t = i / (float)hangers;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 bottom = Deck(t) + right * side * edge + Vector3.down * 0.3f;
                    Bar(Chain(t, side), bottom, 0.06f, 0.06f);
                    Bar(bottom, bottom - right * side * (edge - width * 0.5f + 0.3f), 0.08f, 0.08f);
                }
            }

            // Correntes principais
            foreach (float side in new[] { -1f, 1f })
                ChainLinks(t => Chain(t, side), 0.5f);

            // Uma tábua que caiu e parou no ar, embaixo de um dos vãos
            int first = planks * 3 / 10;
            BlockMesh.ChamferBox(s_Ruins.Pick(), Deck((first + 0.5f) / planks) + Vector3.down * 7f + right * 1.5f,
                new Vector3(width * 0.9f, 0.22f, 0.85f), Quaternion.Euler(R(20f, 60f), R(0f, 360f), R(10f, 40f)), 0.03f);

            // Colisão invisível
            var root = new GameObject("Ponte_Corrente_Colisao").transform;
            root.SetParent(s_World, false);
            int parts = Mathf.CeilToInt(length / 3f);
            for (int i = 0; i < parts; i++)
            {
                Vector3 a = Deck(i / (float)parts), b = Deck((i + 1) / (float)parts);
                Quaternion rotation = Quaternion.LookRotation(b - a);
                var walk = new GameObject("Passarela").transform;
                walk.SetParent(root, false);
                walk.SetPositionAndRotation((a + b) * 0.5f + rotation * Vector3.down * 0.1f, rotation);
                walk.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, (b - a).magnitude + 0.1f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var rail = new GameObject("Mureta").transform;
                    rail.SetParent(root, false);
                    rail.SetPositionAndRotation((a + b) * 0.5f + right * side * (width * 0.5f + 0.3f) + Vector3.up * 0.6f, rotation);
                    rail.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.4f, (b - a).magnitude + 0.1f);
                }
            }
        }

        // Barra de ferro de seção w x h entre dois pontos
        static void Bar(Vector3 a, Vector3 b, float w, float h)
        {
            Vector3 d = b - a;
            Quaternion rotation = Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            BlockMesh.ChamferBox(s_Iron, (a + b) * 0.5f, new Vector3(w, h, d.magnitude), rotation, Mathf.Min(w, h) * 0.2f);
        }

        // Corrente ao longo de uma curva (t de 0 a 1): elos encadeados, um deitado e o seguinte em pé
        static void ChainLinks(System.Func<float, Vector3> curve, float linkLength)
        {
            const int samples = 400;
            var points = new Vector3[samples + 1];
            var distance = new float[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                points[i] = curve(i / (float)samples);
                distance[i] = i == 0 ? 0f : distance[i - 1] + Vector3.Distance(points[i], points[i - 1]);
            }

            float step = linkLength * 0.78f;
            int index = 0, link = 0;
            for (float d = step * 0.5f; d < distance[samples]; d += step, link++)
            {
                while (index < samples - 1 && distance[index + 1] < d)
                    index++;
                float t = Mathf.InverseLerp(distance[index], distance[index + 1], d);
                Vector3 p = Vector3.Lerp(points[index], points[index + 1], t);
                Vector3 tangent = (points[index + 1] - points[index]).normalized;
                Vector3 flatSide = Vector3.Cross(tangent, Vector3.up).normalized;
                Vector3 side = link % 2 == 0 ? flatSide : Vector3.Cross(tangent, flatSide).normalized;
                ChainLink(p, tangent, side, linkLength);
            }
        }

        // Um elo: anel oval (comprimento ao longo de 'along', largura ao longo de 'side'), arame de seção redonda
        static void ChainLink(Vector3 center, Vector3 along, Vector3 side, float length)
        {
            const int around = 14, tube = 6;
            float halfLength = length * 0.5f, halfWidth = length * 0.3f, wire = length * 0.11f;
            Vector3 plane = Vector3.Cross(side, along).normalized;
            int start = s_Iron.Vertices.Count;
            for (int i = 0; i <= around; i++)
            {
                float a = i / (float)around * Mathf.PI * 2f;
                Vector3 c = center + side * Mathf.Cos(a) * halfWidth + along * Mathf.Sin(a) * halfLength;
                Vector3 m = (side * (Mathf.Cos(a) / halfWidth) + along * (Mathf.Sin(a) / halfLength)).normalized;
                for (int j = 0; j <= tube; j++)
                {
                    float b = j / (float)tube * Mathf.PI * 2f;
                    Vector3 n = m * Mathf.Cos(b) + plane * Mathf.Sin(b);
                    s_Iron.Vertices.Add(c + n * wire);
                    s_Iron.Normals.Add(n);
                    s_Iron.Uvs.Add(new Vector2(i / (float)around, j / (float)tube));
                }
            }

            for (int i = 0; i < around; i++)
            for (int j = 0; j < tube; j++)
            {
                int a = start + i * (tube + 1) + j, b = a + tube + 1, c = b + 1, d = a + 1;
                foreach ((int x, int y, int z) in new[] { (a, b, c), (a, c, d) })
                {
                    Vector3 px = s_Iron.Vertices[x], py = s_Iron.Vertices[y], pz = s_Iron.Vertices[z];
                    bool flip = Vector3.Dot(Vector3.Cross(py - px, pz - px), s_Iron.Normals[x] + s_Iron.Normals[y] + s_Iron.Normals[z]) < 0f;
                    s_Iron.Triangles.Add(x);
                    s_Iron.Triangles.Add(flip ? z : y);
                    s_Iron.Triangles.Add(flip ? y : z);
                }
            }
        }

        // Sala 18: gatilho cobrindo a sela e as duas torres, começando 5 m depois da chegada ao norte
        static void BuildSaddleRoom()
        {
            float north = k_Saddle.z + k_SaddleRadius - 5f, south = k_Saddle.z - k_SaddleRadius - 1f;
            float west = k_TowerWest.x - k_TowerWestRadius, east = k_TowerEast.x + k_TowerEastRadius;
            var trigger = new Bounds(new Vector3((west + east) * 0.5f, k_Saddle.y + 14f, (north + south) * 0.5f),
                new Vector3(east - west, 44f, north - south));
            BuildRoom("Arena_SelaDaCrista", "Sela da Crista", 18, k_Saddle, trigger,
                new Vector3(k_Saddle.x, k_Saddle.y, k_Saddle.z + k_SaddleRadius), k_Saddle.y - 1f, k_TowerEast.y + 1f);
        }

        // ---------- longe ----------

        // Dezenas de montanhas nevadas saindo das nuvens em volta (fora do caminho), de seis formatos e tamanhos bem
        // diferentes (colunas, torres de topo plano, grupos de colunas, coroas de blocos, paredões e topos partidos), algumas
        // colossais ao longe; um mosteiro num pico ao sudoeste e uma ilha flutuante sobre um anel quebrado a oeste
        static void BuildDistant()
        {
            var kinds = new[]
            {
                PicosMountainKit.Kind.Column, PicosMountainKit.Kind.Column, PicosMountainKit.Kind.Cluster,
                PicosMountainKit.Kind.Cluster, PicosMountainKit.Kind.Cluster, PicosMountainKit.Kind.Tower,
                PicosMountainKit.Kind.Crown, PicosMountainKit.Kind.Ridge, PicosMountainKit.Kind.Broken,
                PicosMountainKit.Kind.Broken,
            };
            // Em volta do caminho todo (de 0 a 1100 m do meio dele) e, ao fundo, um anel de montanhas grandes (1100 a
            // 1700 m) que aparecem como silhuetas na névoa
            int placed = 0;
            for (int attempt = 0; attempt < 1600 && placed < 230; attempt++)
            {
                bool background = placed >= 160;
                float a = R(0f, Mathf.PI * 2f);
                float d = background ? R(1100f, 1700f) : Mathf.Lerp(0f, 1100f, Mathf.Sqrt(R(0f, 1f)));
                var p = new Vector3(k_MapCenter.x + Mathf.Cos(a) * d, 0f, k_MapCenter.y + Mathf.Sin(a) * d);
                // Longe do caminho (praça, crista, plataformas, pontes e arenas) e do mosteiro e da ilha
                bool onPath = false;
                foreach (var clearing in s_Clearings)
                    onPath |= Vector2.Distance(new Vector2(p.x, p.z), clearing.Center) < clearing.Radius;
                if (onPath)
                    continue;
                bool nearHero = false;
                foreach (var hero in k_Heroes)
                    nearHero |= Vector2.Distance(new Vector2(p.x, p.z), new Vector2(hero.Position.x, hero.Position.z)) < hero.Radius + 40f;
                if (nearHero || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(280f, -330f)) < 110f ||
                    Vector2.Distance(new Vector2(p.x, p.z), new Vector2(-320f, -380f)) < 120f)
                    continue;

                // Distância até o caminho mais perto (o detalhe e o tamanho crescem com ela)
                float near = float.MaxValue;
                foreach (var clearing in s_Clearings)
                    near = Mathf.Min(near, Vector2.Distance(new Vector2(p.x, p.z), clearing.Center));
                bool giant = background || (near > 500f && s_Random.NextDouble() < 0.2);
                float size = giant ? R(40f, 80f) : R(7f, 24f) * (1f + near / 1500f);
                float top = giant ? R(60f, 200f) : R(-60f, 70f) + near * 0.05f;
                PicosMountainKit.Kind kind = kinds[s_Random.Next(kinds.Length)];
                int detail = near < 200f ? 0 : near < 500f ? 1 : 2;
                PicosMountainKit.Mountain(s_MountainFar, kind, new Vector3(p.x, top, p.z), size, s_Random, detail);
                placed++;
            }

            // Mosteiro: pico alto com terraços e uma torre no topo
            var monastery = new Vector3(280f, 55f, -330f);
            PicosMountainKit.Mountain(s_MountainFar, PicosMountainKit.Kind.Tower, monastery + Vector3.down * 1f, 16f, s_Random, 1);
            ArquipelagoRuinKit.Tower(s_Ruins, monastery, 30f, 1.4f, s_Random);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 70f + 200f) * Mathf.Deg2Rad;
                var c = monastery + new Vector3(Mathf.Cos(a) * 15f, -8f - k * 7f, Mathf.Sin(a) * 15f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), c, new Vector3(10f, 6f, 8f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), 0.2f);
            }

            // Ilha flutuante: rocha em cone invertido com casas no topo, sobre um anel quebrado
            var island = new Vector3(-320f, 40f, -380f);
            FloatingIsland(island, 42f);
            ArquipelagoRuinKit.RichRing(s_Ruins, island + Vector3.down * 18f, Vector3.up, Vector3.forward, 52f, 3f, 4f,
                20f, 320f, s_Random, true, true, k_CloudY, 3);
            for (int k = 0; k < 6; k++)
                BlockMesh.ChamferBox(s_Ruins.Pick(), island + new Vector3(R(-28f, 28f), R(2f, 6f), R(-28f, 28f)),
                    new Vector3(R(5f, 9f), R(4f, 12f), R(5f, 9f)), Quaternion.Euler(0f, R(0f, 90f), 0f), 0.2f);
            ArquipelagoRuinKit.Tower(s_Ruins, island + new Vector3(8f, 0f, -6f), 0f, 1.6f, s_Random);
        }

        // Montanhas de destaque: os modelos do Blender com o material das montanhas (sem colisão: ficam fora do caminho)
        static void BuildHeroes()
        {
            foreach (var hero in k_Heroes)
            {
                string path = $"{IcaroAssetUtility.Root}/Models/Picos/{hero.Name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model)
                {
                    Debug.LogWarning($"[Ícaro] Picos: falta {path} (rode Ferramentas/Picos/montanhas_destaque.py no Blender).");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, s_World);
                go.name = hero.Name;
                go.transform.SetPositionAndRotation(hero.Position, Quaternion.Euler(0f, hero.Yaw, 0f));
                foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = s_Materials.Mountain;
                    renderer.sharedMaterials = materials;
                }

                // Os fiapos de névoa se enrolam nela na altura das nuvens
                PicosMountainKit.Pillars.Add(new PicosMountainKit.Placed
                {
                    Top = hero.Position + Vector3.up * 40f, TopRadius = hero.Radius * 0.45f, BaseRadius = hero.Radius * 0.9f, Taper = 1f,
                });
            }
        }

        // Cone de rocha com a ponta para baixo e o topo plano nevado
        static void FloatingIsland(Vector3 top, float radius)
        {
            const int sides = 18, rings = 8;
            float depth = radius * 1.6f;
            var grid = new Vector3[rings + 1, sides + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings, r = radius * (1f - Mathf.Pow(t, 0.8f)) + 0.5f;
                for (int j = 0; j <= sides; j++)
                {
                    float a = (j % sides) * Mathf.PI * 2f / sides;
                    float n = 1f + (Mathf.PerlinNoise(j * 0.6f, i * 0.8f + 4f) - 0.5f) * 0.4f;
                    grid[i, j] = top + new Vector3(Mathf.Cos(a) * r * n, -depth * t, Mathf.Sin(a) * r * n);
                }
            }

            for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                Vector3 a = grid[i, j], b = grid[i, j + 1], c = grid[i + 1, j + 1], d = grid[i + 1, j];
                Vector3 outward = (a + c) * 0.5f - new Vector3(top.x, (a.y + c.y) * 0.5f, top.z);
                Tri(s_MountainFar, a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, outward);
                Tri(s_MountainFar, a, c, d, Vector2.zero, Vector2.zero, Vector2.zero, outward);
            }
            for (int j = 0; j < sides; j++)
                Tri(s_MountainFar, grid[0, j], grid[0, j + 1], top, Vector2.right, Vector2.right, Vector2.right, Vector3.up);
        }

        // ---------- nuvens ----------

        // Mar de nuvens de fumaça, sem nada sólido: um volume (CloudSea + Icaro/CloudVolume) com os montes de nuvem
        // em ruído 3D, e por cima fiapos de névoa (Icaro/CloudWisp) enrolados nos picos na altura das nuvens, na
        // beira da praça e da próxima arena e soltos sobre o mar. Paradas a 1,0x; mexem com o multiplicador.
        static void BuildClouds()
        {
            var sea = new GameObject("Mar_de_Nuvens");
            sea.transform.SetParent(s_World, false);
            sea.transform.position = new Vector3(k_MapCenter.x, 0f, k_MapCenter.y);
            sea.AddComponent<MeshFilter>();
            var renderer = sea.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_Materials.CloudVolume;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var cloudSea = sea.AddComponent<CloudSea>();
            cloudSea.Bottom = k_CloudBottom;
            cloudSea.Top = k_CloudTop;
            cloudSea.Extent = 2300f;

            var wisps = new List<Wisp>();
            // Em volta de cada pico, na altura das nuvens (mais nos perto)
            foreach (PicosMountainKit.Placed spire in PicosMountainKit.Pillars)
            {
                float distance = Vector2.Distance(new Vector2(spire.Top.x, spire.Top.z), k_MapCenter);
                if (distance > 1300f)
                    continue;
                int count = distance < 400f ? 9 : 5;
                for (int k = 0; k < count; k++)
                {
                    float y = R(k_CloudY - 8f, k_CloudY + 16f);
                    if (y > spire.Top.y - 4f)
                        continue;
                    float a = R(0f, Mathf.PI * 2f), r = spire.RadiusAt(y) * R(0.9f, 1.4f);
                    var p = new Vector3(spire.Top.x + Mathf.Cos(a) * r, y, spire.Top.z + Mathf.Sin(a) * r);
                    wisps.Add(new Wisp(p, R(14f, 34f) * (1f + distance / 1500f), R(0.45f, 0.85f)));
                }
            }

            // Névoa subindo pela parede do pico da chegada e do pico da próxima arena, logo abaixo da beira
            WispRing(Vector3.zero, 30f, -4f, -30f, 34, 7f, 15f, wisps);
            WispRing(k_NextPeak, 42f, k_NextPeak.y - 4f, k_NextPeak.y - 30f, 34, 7f, 15f, wisps);
            WispRing(k_Saddle, 30f, k_Saddle.y - 5f, k_Saddle.y - 30f, 30, 7f, 15f, wisps);
            WispRing(k_Landing, 14f, k_Landing.y - 4f, k_Landing.y - 25f, 14, 6f, 12f, wisps);

            // Soltos sobre o mar, rentes ao topo dos montes
            for (int k = 0; k < 320; k++)
            {
                float a = R(0f, Mathf.PI * 2f), d = Mathf.Lerp(60f, 1500f, Mathf.Sqrt(R(0f, 1f)));
                var p = new Vector3(k_MapCenter.x + Mathf.Cos(a) * d, R(k_CloudY - 6f, k_CloudY + 8f), k_MapCenter.y + Mathf.Sin(a) * d);
                wisps.Add(new Wisp(p, R(30f, 70f), R(0.3f, 0.6f)));
            }

            WispMesh(wisps, s_World);
        }

        struct Wisp
        {
            public Vector3 Center;
            public float Size, Opacity;

            public Wisp(Vector3 center, float size, float opacity)
            {
                Center = center;
                Size = size;
                Opacity = opacity;
            }
        }

        // Anel de fiapos em volta de um pico, entre duas alturas, colados na parede (raio do pico naquela altura)
        static void WispRing(Vector3 center, float radius, float yTop, float yBottom, int count, float minSize,
            float maxSize, List<Wisp> wisps)
        {
            for (int k = 0; k < count; k++)
            {
                float a = (k + R(0f, 0.8f)) / count * Mathf.PI * 2f;
                float y = R(yBottom, yTop);
                float r = radius * R(0.95f, 1.25f) + (yTop - y) * 0.25f;
                wisps.Add(new Wisp(center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r) + Vector3.up * (y - center.y),
                    R(minSize, maxSize), R(0.3f, 0.6f)));
            }
        }

        // Malha estática dos fiapos: um quadrado por tufo, com o centro em todos os vértices (o shader vira para a
        // câmera), o canto em UV0 e tamanho, semente, opacidade e luz em UV1
        static void WispMesh(List<Wisp> wisps, Transform parent)
        {
            var vertices = new List<Vector3>();
            var corners = new List<Vector2>();
            var data = new List<Vector4>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Vector2[] quad = { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) };
            var bounds = new Bounds(wisps[0].Center, Vector3.zero);
            foreach (Wisp wisp in wisps)
            {
                int start = vertices.Count;
                float seed = R(0f, 1f);
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(wisp.Center);
                    corners.Add(quad[i]);
                    data.Add(new Vector4(wisp.Size, seed, wisp.Opacity, 1f));
                    colors.Add(Color.white);
                }

                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                bounds.Encapsulate(new Bounds(wisp.Center, Vector3.one * (wisp.Size * 2.5f + 20f)));
            }

            IcaroAssetUtility.EnsureFolder(GridSceneKit.MeshFolder);
            string path = $"{GridSceneKit.MeshFolder}/{k_Prefix}_Fiapos.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = k_Prefix + "_Fiapos" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, data);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = bounds;
            EditorUtility.SetDirty(mesh);

            var go = new GameObject("Fiapos_Nevoa");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_Materials.Wisp;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        // ---------- neve caindo ----------

        // A neve cai devagar numa caixa em volta de 'center' (a praça, a arena 1 e a sela)
        static void Snowfall(Vector3 center)
        {
            var go = new GameObject("Neve_Caindo");
            go.transform.SetParent(s_World, false);
            go.transform.position = center;
            var system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 26f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(0.95f, 0.97f, 1f, 0.9f);
            main.gravityModifier = 0.12f;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 220f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(140f, 2f, 220f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GridTextureKit.SporeMaterial();
        }

        // ---------- utilidades ----------

        // Escada reta de 'bottom' até 'top', com degraus maciços, blocos laterais e a rampa invisível rente às quinas
        static void Stairs(Vector3 bottom, Vector3 top, float width)
        {
            Vector3 flat = top - bottom;
            flat.y = 0f;
            Vector3 dir = flat.normalized;
            Quaternion rotation = Quaternion.LookRotation(dir);
            float rise = top.y - bottom.y, run = flat.magnitude;
            int steps = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(rise) / 0.3f));
            float stepRise = rise / steps, stepRun = run / steps;
            for (int k = 0; k < steps; k++)
            {
                float y = bottom.y + (k + 1) * stepRise;
                Vector3 c = bottom + dir * (stepRun * (k + 0.5f));
                float height = Mathf.Max(0.4f, y - (Mathf.Min(bottom.y, top.y) - 1.5f));
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(c.x, y - height * 0.5f, c.z), new Vector3(width, height, stepRun + 0.02f),
                    rotation, 0.03f);
            }

            Vector3 right = rotation * Vector3.right;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 a = bottom + right * s * (width * 0.5f + 0.6f), b = top + right * s * (width * 0.5f + 0.6f);
                float high = Mathf.Max(bottom.y, top.y) + 0.8f, low = Mathf.Min(bottom.y, top.y) - 2f;
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3((a.x + b.x) * 0.5f, (high + low) * 0.5f, (a.z + b.z) * 0.5f),
                    new Vector3(1.2f, high - low, run), rotation, 0.06f);
            }

            var ramp = new GameObject("Rampa_Escada");
            ramp.transform.SetParent(s_World, false);
            Vector3 along = top - bottom;
            ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f + Quaternion.LookRotation(along) * Vector3.up * -0.12f,
                Quaternion.LookRotation(along));
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, along.magnitude);
        }

        // Trilha de neve pisada de 'a' até 'b' (placa grossa inclinada, com colisão)
        static void Path(Vector3 a, Vector3 b, float width)
        {
            Vector3 along = b - a;
            Quaternion rotation = Quaternion.LookRotation(along);
            var block = new MeshData();
            BlockMesh.ChamferBox(block, (a + b) * 0.5f + rotation * Vector3.down * 0.4f, new Vector3(width, 0.8f, along.magnitude + 1f),
                rotation, 0.2f);
            SplitTops(block, s_Path, s_Snow, 4f);
        }

        // Monte arredondado (neve ou nuvem): meia elipsoide com ruído
        static void Mound(MeshData mesh, Vector3 center, float rx, float rz, float height, int sides)
        {
            const int rings = 4;
            float seed = R(0f, 50f);
            var grid = new Vector3[rings + 1, sides + 1];
            for (int i = 0; i <= rings; i++)
            {
                float phi = i / (float)rings * Mathf.PI * 0.5f;
                for (int j = 0; j <= sides; j++)
                {
                    float a = (j % sides) * Mathf.PI * 2f / sides;
                    float n = 1f + (Mathf.PerlinNoise(seed + Mathf.Cos(a), seed + Mathf.Sin(a) + i * 0.3f) - 0.5f) * 0.35f;
                    grid[i, j] = center + new Vector3(Mathf.Cos(a) * rx * n * Mathf.Cos(phi), height * Mathf.Sin(phi) - 0.1f,
                        Mathf.Sin(a) * rz * n * Mathf.Cos(phi));
                }
            }

            for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                Vector3 a = grid[i, j], b = grid[i, j + 1], c = grid[i + 1, j + 1], d = grid[i + 1, j];
                Vector3 outward = (a + c) * 0.5f - center;
                Tri(mesh, a, b, c, new Vector2(a.x, a.z) / 4f, new Vector2(b.x, b.z) / 4f, new Vector2(c.x, c.z) / 4f, outward);
                Tri(mesh, a, c, d, new Vector2(a.x, a.z) / 4f, new Vector2(c.x, c.z) / 4f, new Vector2(d.x, d.z) / 4f, outward);
            }
        }

        // Prisma de 'sides' lados de 'start' ao longo de 'axis', com a ponta afinando (tip = comprimento da ponta em raios)
        static void Spike(MeshData mesh, Vector3 start, Vector3 axis, float length, float radius, int sides, float angle,
            float tip, bool capped = false)
        {
            Vector3 dir = axis.normalized;
            Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 w = Vector3.Cross(dir, u);
            float tipLength = Mathf.Min(radius * tip, length * 0.8f), body = length - tipLength;
            Vector3 end = start + dir * body, apex = start + dir * length;
            for (int k = 0; k < sides; k++)
            {
                float a0 = angle * Mathf.Deg2Rad + k * Mathf.PI * 2f / sides, a1 = a0 + Mathf.PI * 2f / sides;
                Vector3 r0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * radius, r1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * radius;
                Vector3 outward = r0 + r1;
                float v = body / 4f;
                Tri(mesh, start + r0, end + r0, end + r1, new Vector2(0f, 0f), new Vector2(0f, v), new Vector2(0.5f, v), outward);
                Tri(mesh, start + r0, end + r1, start + r1, new Vector2(0f, 0f), new Vector2(0.5f, v), new Vector2(0.5f, 0f), outward);
                if (tipLength > 0.01f)
                    Tri(mesh, end + r0, apex, end + r1, new Vector2(0f, v), new Vector2(0.25f, v + 0.3f), new Vector2(0.5f, v),
                        outward + dir * radius);
                else
                    Tri(mesh, end + r0, end, end + r1, Vector2.zero, new Vector2(0.25f, 0.25f), new Vector2(0.5f, 0f), dir);
                if (capped)
                    Tri(mesh, start + r0, start, start + r1, Vector2.zero, new Vector2(0.25f, 0.25f), new Vector2(0.5f, 0f), -dir);
            }
        }

        static void Quad(MeshData mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, float v0 = 0f,
            float v1 = 1f, float tile = 0f)
        {
            Vector2 ua, ub, uc, ud;
            if (tile > 0f)
            {
                ua = new Vector2(a.x, a.z) / tile;
                ub = new Vector2(b.x, b.z) / tile;
                uc = new Vector2(c.x, c.z) / tile;
                ud = new Vector2(d.x, d.z) / tile;
            }
            else
            {
                ua = new Vector2(0f, v0);
                ub = new Vector2(0f, v1);
                uc = new Vector2(1f, v1);
                ud = new Vector2(1f, v0);
            }

            Tri(mesh, a, b, c, ua, ub, uc, facing);
            Tri(mesh, a, c, d, ua, uc, ud, facing);
        }

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

        // Separa os triângulos virados para cima (UV no mundo xz / tile) dos outros
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

        // Ruína em objetos por material: três tons de cantaria, a neve nos topos (no lugar do musgo do Arquipélago) e os
        // vãos escuros; sem parte molhada (não há água)
        static Transform CreateRuin(string name, RuinMesh mesh, bool collider, bool snow = true)
        {
            RuinMesh.Layers layers = mesh.Split(-10000f, snow ? 0.55f : 2f);
            var root = new GameObject(name).transform;
            root.SetParent(s_World, false);
            for (int i = 0; i < layers.Tones.Length; i++)
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Tom{i + 1}", $"Pedra_{i + 1}", layers.Tones[i],
                    s_Materials.Stones[i % s_Materials.Stones.Length], root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Neve", "Neve", layers.Moss, s_Materials.Snow, root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Vaos", "Vaos", layers.Dark, s_Materials.Dark, root, collider);
            return root;
        }

        static void Light(Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject("Luz_Portal").AddComponent<Light>();
            light.transform.SetParent(s_World, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // Na praça, de frente para o sul (a saída e o eclipse), logo à frente do portal de onde chegou
        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.2f, 8f), Quaternion.LookRotation(Vector3.back));
        }
    }
}
