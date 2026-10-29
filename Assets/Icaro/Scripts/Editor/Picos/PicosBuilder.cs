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
    // partidas e montes de neve; a oeste o parapeito despencou;
    // um aqueduto quebrado a leste com a água congelada caindo pela parede do pico. A saída desce por
    // uma escadaria até a trilha da crista e uma ponte de pedra até a torre da primeira arena (sala 17, Terraços
    // Partidos, ainda sem inimigos). Em volta, dezenas de montanhas saindo do mar de nuvens de volume, quatro montanhas de
    // destaque do Blender e uma ilha flutuante sobre um anel. Céu: eclipse congelado (disco negro
    // com a coroa), horizonte alaranjado em toda a volta, estrelas. Neve só estética; sem vida nenhuma.
    public static partial class PicosBuilder
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
        static readonly Vector2 k_MapCenter = new Vector2(-80f, -520f);   // meio do caminho, centro das montanhas de longe

        // Sala 19, Bastião em Terraços: numa plataforma plana na encosta norte de uma montanha (não no topo), uns 45° à
        // direita da sela (sudoeste). Coordenadas locais do forte (B): x para oeste, z para o sul (para a montanha), y
        // para cima a partir do chão do pátio; a frente aberta (norte) é por onde se chega
        static readonly Vector3 k_Bastion = new Vector3(-300f, -8f, -960f);
        const float k_BastionFront = -55f, k_BastionBack = 45f, k_BastionHalfWidth = 58f;
        static bool s_BastionMountain;
        static BrokenPortal s_BastionPortal;
        static Renderer s_NicheRune;

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
        static MeshData s_Snow, s_Ice, s_MountainNear, s_MountainFar, s_Iron, s_Wood;
        static RuinMesh s_Paving, s_PathStones;     // pisos das construções (lajes e neve) e as pedras das trilhas
        static Transform s_PathColliders;
        // Plataformas (centro e raio) para a neve dos pisos acumular mais perto da beira
        static readonly List<(Vector3 Center, float Radius)> s_Platforms = new List<(Vector3, float)>();
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

        [MenuItem("Ícaro/Picos/Abrir portais quebrados (teste)", priority = 50)]
        static void OpenBrokenPortals()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Ícaro] Abrir portais quebrados: entre em Play primeiro.");
                return;
            }

            BrokenPortal.OpenAll();
        }

        [MenuItem("Ícaro/Picos/Ir para as Vilas do Alto (teste)", priority = 51)]
        static void GoToHighlands()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Ícaro] Ir para as Vilas do Alto: entre em Play primeiro.");
                return;
            }

            GameObject arrival = GameObject.Find("Chegada_VilasDoAlto");
            var controller = Object.FindFirstObjectByType<CharacterController>();
            if (!arrival || !controller)
            {
                Debug.LogWarning("[Ícaro] Ir para as Vilas do Alto: faltam a chegada (gere a cena de novo) ou o jogador.");
                return;
            }

            controller.enabled = false;
            controller.transform.SetPositionAndRotation(arrival.transform.position, arrival.transform.rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

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
            s_Paving = new RuinMesh(1703, 1);
            s_PathStones = new RuinMesh(1704);
            s_Platforms.Clear();
            s_PathColliders = new GameObject("Trilhas_Colisao").transform;
            s_PathColliders.SetParent(s_World, false);
            s_Ice = new MeshData();
            PicosMountainKit.Ice = s_Ice;     // pingentes sob as cornijas de neve das montanhas
            s_Iron = new MeshData();
            s_Wood = new MeshData();
            s_Ruins = new RuinMesh(1702);
            InitIsle();
            BlockMesh.Tile = 4f;

            BuildArrivalPeak();
            BuildPlaza();
            BuildPortal();
            BuildAqueduct();
            BuildCrest();
            BuildArena();
            BuildSaddle();
            BuildBastion();
            BuildIsle();
            BuildHighlands();
            BuildHeroes();
            BuildDistant();
            BuildClouds();

            // Montanhas: as do caminho com colisão, as de longe sem
            GridSceneKit.CreateObject(k_Prefix + "_Montanhas_Perto", "Montanhas_Perto", s_MountainNear, s_Materials.Mountain,
                s_World, true);
            GridSceneKit.CreateObject(k_Prefix + "_Montanhas", "Montanhas", s_MountainFar, s_Materials.Mountain, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Neve", "Neve", s_Snow, s_Materials.Snow, s_World, true);
            CreatePaving();
            CreateRuin("Calcada", s_PathStones, true, false);
            GridSceneKit.CreateObject(k_Prefix + "_Gelo", "Gelo", s_Ice, s_Materials.Ice, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Ferro", "Ferro", s_Iron, s_Materials.Iron, s_World, false);
            GridSceneKit.CreateObject(k_Prefix + "_Madeira", "Madeira", s_Wood, s_Materials.Wood, s_World, false);
            CreateRuin("Ruinas", s_Ruins, true);
            BuildArenaRoom();
            BuildSaddleRoom();
            BuildBastionRoom();
            BuildIsleRoom();

            Snowfall(new Vector3(-6f, 30f, -50f));
            Snowfall(k_NextPeak + Vector3.up * 30f);
            Snowfall(k_Saddle + Vector3.up * 30f);
            Snowfall(k_Bastion + Vector3.up * 30f);
            Snowfall(k_Isle + Vector3.up * 30f);
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
            s_Platforms.Add((top, inner));
            var slab = new MeshData();
            Prism(slab, top, radius, radius, sides, top.y, slabBottom, true);
            Pave(slab);
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

                // Bloco da quina (no canto entre este lado e o próximo), menos junto de um lado aberto (a passagem fica livre)
                bool openCorner = System.Array.IndexOf(open, k) >= 0 || System.Array.IndexOf(open, (k + 1) % sides) >= 0;
                Vector3 corner = top + Corner(k, sides) * (radius - 1.2f);
                Quaternion cornerFacing = Quaternion.LookRotation(Corner(k, sides));
                if (!openCorner)
                {
                    BlockMesh.ChamferBox(s_Ruins.Pick(), corner + Vector3.up * 1.1f, new Vector3(2.2f, 2.2f, 2.2f), cornerFacing, 0.12f);
                    BlockMesh.ChamferBox(s_Ruins.Pick(), corner + Vector3.up * 2.4f, new Vector3(1.6f, 0.4f, 1.6f), cornerFacing, 0.08f);
                }

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
                Bundle = false,     // uma rocha só, mais larga que o fuste da torre
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
                SnowPile(p, k_PlazaRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad) - 1f - d, R(1.5f, 4f), R(1.5f, 3.5f), R(0.4f, 1.3f));
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
            MeshData disc = PortalDisc(5.4f);
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

        // Lados da planta oitavada da torre (0 = leste, 2 = norte...) virados para 'toward': o mais virado e, se a
        // direção cai perto de uma quina, os dois que se encontram nela
        static IEnumerable<int> SidesToward(Vector3 center, Vector3 toward)
        {
            float side = Mathf.Repeat(Mathf.Atan2(toward.z - center.z, toward.x - center.x) * Mathf.Rad2Deg, 360f) / 45f;
            int nearest = Mathf.RoundToInt(side) % 8;
            yield return nearest;
            float off = side - Mathf.Round(side);
            if (Mathf.Abs(off) > 0.22f)
                yield return (nearest + (off > 0f ? 1 : 7)) % 8;
        }

        // Torrezinha esculpida de passagem (TowerTop pequena, aberta para quem chega e para onde se vai), com uma coluna
        // partida e neve no topo; devolve o raio de dentro
        static float SmallTower(Vector3 center, float radius, int seed, Vector3 from, Vector3 to)
        {
            var open = new List<int>(SidesToward(center, from));
            foreach (int side in SidesToward(center, to))
                if (!open.Contains(side))
                    open.Add(side);
            // Porta longe das aberturas; senão em qualquer lado fechado; senão nenhuma (-1)
            int door = -1;
            for (int side = 0; side < 8 && door < 0; side++)
                if (!open.Contains(side) && !open.Contains((side + 1) % 8) && !open.Contains((side + 7) % 8))
                    door = side;
            for (int side = 0; side < 8 && door < 0; side++)
                if (!open.Contains(side))
                    door = side;
            float inner = TowerTop(center, radius, seed, open.ToArray(), door);
            Vector3 dir = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            ArquipelagoRuinKit.Column(s_Ruins, center + dir * inner * 0.45f, R(2f, 4f), 0.7f, s_Random, true);
            for (int k = 0; k < 3; k++)
            {
                float d = inner * R(0.45f, 0.7f);
                SnowPile(center + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * d, inner - 0.8f - d,
                    R(1f, 2.2f), R(1f, 2.2f), R(0.3f, 0.7f));
            }
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
            s_Platforms.Add((center, radius * 0.75f));
            Prism(slab, center, radius * 0.75f, radius * 0.75f, 7, center.y, center.y - 0.8f, true);
            Pave(slab);

            Vector3 dir = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            if (s_Random.NextDouble() < 0.5)
                ArquipelagoRuinKit.Column(s_Ruins, center + dir * radius * 0.4f, R(1.5f, 3.5f), 0.7f, s_Random, true);
            else
                ArquipelagoRuinKit.AshlarWall(s_Ruins, center + dir * radius * 0.45f, Quaternion.LookRotation(dir), R(2.5f, 4f),
                    R(0.8f, 1.6f), 0.8f, s_Random);
            // Pedra e neve em cima do calçamento (a rocha em volta dele fica 0,5 m mais baixa: nada fica no ar)
            float paved = radius * 0.75f * 0.9f;
            for (int k = 0; k < 4; k++)
            {
                float d = paved * R(0.4f, 0.75f);
                Vector3 at = center + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * d;
                if (k == 0)
                    ArquipelagoRuinKit.IrregularRock(s_Ruins, at + Vector3.up * 0.3f, R(0.6f, 1.1f), s_Random);
                else
                    SnowPile(at, paved - d, R(1f, 2.4f), R(1f, 2.4f), R(0.3f, 0.8f));
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

        // Trilha pelos pontos, sobre um espinhaço de rocha natural: pilares de apoio a cada ~9 m (o corpo da crista,
        // descendo até a nuvem) e, por cima, cristas mais baixas e irregulares a cada ~2,6 m formando o caminho (topos
        // logo abaixo das pedras); por cima, a calçada antiga (Path); em cima das cristas (nunca além da beira delas),
        // pedras soltas, montes de neve e, de vez em quando, um marco de pedras empilhadas
        static void Trail(Vector3[] trail, int extras)
        {
            float walked = 0f, nextCairn = 12f;
            var ridges = new List<(Vector3 top, float radius, Vector3 right)>();
            for (int i = 0; i + 1 < trail.Length; i++)
            {
                Vector3 a = trail[i], b = trail[i + 1];
                Path(a, b, 4.2f);
                Vector3 along = b - a;
                float length = along.magnitude;
                Vector3 right = Vector3.Cross(Vector3.up, new Vector3(along.x, 0f, along.z)).normalized;

                for (float d = 1f; d < length + 0.5f; d += 9f)
                {
                    Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(d / length));
                    float r = R(4f, 5.5f);
                    PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                    {
                        Top = p + Vector3.down * 1.4f + right * R(-0.8f, 0.8f), TopRadius = r, BaseRadius = r * 2.2f, Taper = 2.5f,
                        Cap = PicosMountainKit.Cap.Flat, Sides = 14, RingStep = 0f, Corners = 5, Flutes = 3, FluteDepth = 0.08f,
                        BedHeight = R(6f, 10f), BedShift = 0.09f, Waist = 0.12f, Roughness = 0.08f, Seed = R(0f, 1000f),
                        Bundle = false,
                    });
                }

                // Cristas de 5 ou 6 cantos e pouco desvio para o lado: mesmo no lado mais estreito, a rocha cobre ~2 m para
                // cada lado do centro (mais que as pedras da calçada)
                for (float d = 0.5f; d < length; d += 2.6f)
                {
                    Vector3 p = Vector3.Lerp(a, b, d / length);
                    float r = R(3f, 3.8f);
                    Vector3 top = p + Vector3.down * 0.2f + right * R(-0.4f, 0.4f);
                    PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                    {
                        Top = top, BottomY = p.y - R(5f, 9f), TopRadius = r,
                        BaseRadius = r * 1.25f, Taper = 1.5f, Cap = PicosMountainKit.Cap.Flat, Sides = 10, RingStep = 0f,
                        Corners = 5 + s_Random.Next(2), Flutes = 2, FluteDepth = 0.08f, BedHeight = R(2f, 3.5f),
                        BedShift = 0.12f, Waist = 0.08f, Roughness = 0.1f, Seed = R(0f, 1000f), Bundle = false, Track = false,
                    });
                    ridges.Add((top, r, right));

                    // Neve encostada na beira das pedras, de um lado, sem passar da beira da crista
                    if (s_Random.NextDouble() < 0.45)
                        SnowOnRidge(top, r, right, R(0.5f, 0.9f), R(0.2f, 0.4f));

                    // Marco de pedras empilhadas, de vez em quando
                    if (walked + d >= nextCairn)
                    {
                        nextCairn += 34f;
                        Vector3 at = top + right * (s_Random.NextDouble() < 0.5 ? -1f : 1f) * (r * 0.6f - 0.5f);
                        float y = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            float size = 0.9f - k * 0.17f;
                            BlockMesh.ChamferBox(s_PathStones.Pick(), at + Vector3.up * (y + size * 0.3f), new Vector3(size, size * 0.6f, size * 0.85f),
                                Quaternion.Euler(R(-6f, 6f), R(0f, 90f), R(-6f, 6f)), 0.08f);
                            y += size * 0.6f;
                        }
                    }
                }

                walked += length;
            }

            // Pedras soltas e mais neve, sempre em cima de uma crista
            for (int k = 0; k < extras && ridges.Count > 0; k++)
            {
                var (top, r, right) = ridges[s_Random.Next(ridges.Count)];
                if (s_Random.NextDouble() < 0.5)
                {
                    float size = R(0.5f, 0.9f);
                    float side = s_Random.NextDouble() < 0.5 ? -1f : 1f;
                    ArquipelagoRuinKit.IrregularRock(s_Ruins, top + right * side * R(1.3f, Mathf.Max(1.3f, r * 0.62f - size)) + Vector3.up * 0.15f,
                        size, s_Random);
                }
                else
                    SnowOnRidge(top, r, right, R(0.6f, 1f), R(0.25f, 0.45f));
            }
        }

        // Monte de neve em cima de uma crista da trilha, de um dos lados das pedras: o monte inteiro fica dentro da parte
        // garantida do topo da crista (~62% do raio, descontando cantos e fendas); se não couber, não põe
        static void SnowOnRidge(Vector3 top, float radius, Vector3 right, float size, float height)
        {
            float reach = radius * 0.62f - size;
            if (reach < 1f)
                return;
            float side = s_Random.NextDouble() < 0.5 ? -1f : 1f;
            Mound(s_Snow, top + right * side * R(1f, reach), size, size * R(0.75f, 1f), height, 8);
        }

        // Monte de neve num piso em 'p' com 'room' metros até a beira: o tamanho é cortado para o monte não passar da
        // beira (sem sobrar pendurado no ar); sem espaço, não põe
        static void SnowPile(Vector3 p, float room, float size, float sizeZ, float height)
        {
            float limit = room - 0.3f;
            if (limit < 0.6f)
                return;
            Mound(s_Snow, p, Mathf.Min(size, limit), Mathf.Min(sizeZ, limit), height, 10);
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
        // Entrada ao norte sob um arco, onde a ponte da crista chega; saída ao sul (para a sala 18).
        // Sem inimigos por enquanto (os do bioma ainda não existem): a sala tem gatilho,
        // domo, NavMesh e pontos de spawn, mas nenhuma ficha de combate.
        static void BuildArena()
        {
            float inner = TowerTop(k_NextPeak, k_ArenaRadius, 23, new[] { 2, 6 }, 0);

            // Terraços: setores de anel (topo vira piso de lajes)
            var slab = new MeshData();
            Sector(slab, k_NextPeak, 21f, k_TerraceOuter, 140f, 215f, 3f);
            Sector(slab, k_NextPeak, 21f, k_TerraceOuter, 215f, 255f, 1.5f);
            Sector(slab, k_NextPeak, 18f, k_TerraceOuter, 285f, 345f, 5.5f);
            Pave(slab);

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

            // Montes de neve perto da borda e encostados nos muros
            for (int k = 0; k < 40; k++)
            {
                float a = R(0f, 360f), d = R(10f, inner - 3f);
                Vector3 p = Polar(a, d, 0f) - k_NextPeak;
                if (d < 25f && s_Random.NextDouble() < 0.8)
                    continue;
                p.y = FloorHeight(p.x, p.z);
                SnowPile(k_NextPeak + p, inner - d, R(1.5f, 3.5f), R(1.5f, 3.5f), R(0.3f, 1f));
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

        // Sala de combate dos Picos: gatilho em caixa ('trigger', no mundo), RoomManager (bioma 2), NavMesh da área e até
        // 16 pontos de spawn espalhados pela área toda, no chão entre 'minY' e 'maxY' (raio para baixo numa grade), longe
        // da entrada, no NavMesh e com ~2 m livres em volta. Sem CombatBalance: o domo existe, mas a sala não tranca nem chama inimigos até o
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

            // Todos os pontos válidos da área (a 9 m uns dos outros); depois, até 16 bem espalhados
            var candidates = new List<Vector3>();
            for (float x = trigger.min.x + 3f; x <= trigger.max.x - 3f; x += 3f)
            for (float z = trigger.min.z + 3f; z <= trigger.max.z - 3f; z += 3f)
            {
                var above = new Vector3(x + R(-1f, 1f), trigger.max.y, z + R(-1f, 1f));
                if (!Physics.Raycast(above, Vector3.down, out RaycastHit ground, trigger.size.y + 20f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (ground.point.y < minY || ground.point.y > maxY || Vector3.Distance(ground.point, entrance) < 22f)
                    continue;
                if (!NavMesh.SamplePosition(ground.point + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    continue;
                if (candidates.Exists(p => Vector3.Distance(p, hit.position) < 9f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                candidates.Add(hit.position);
            }

            // Escolha espalhada: começa pelo mais longe da entrada e vai pegando o que fica mais longe dos já escolhidos
            var points = new List<Vector3>();
            while (points.Count < 16 && candidates.Count > 0)
            {
                int best = 0;
                float bestDistance = -1f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float d = points.Count == 0 ? Vector3.Distance(candidates[i], entrance) : float.MaxValue;
                    foreach (Vector3 p in points)
                        d = Mathf.Min(d, Vector3.Distance(candidates[i], p));
                    if (d > bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }

                points.Add(candidates[best]);
                candidates.RemoveAt(best);
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
                    Waist = 0.1f, Roughness = 0.07f, Seed = 300 + k * 13, Bundle = false,
                });

            // Laje da sela
            s_Platforms.Add((k_Saddle + Vector3.left * k_SaddleHalf * 0.5f, k_SaddleRadius + k_SaddleHalf * 0.5f));
            s_Platforms.Add((k_Saddle + Vector3.right * k_SaddleHalf * 0.5f, k_SaddleRadius + k_SaddleHalf * 0.5f));
            var slab = new MeshData();
            Stadium(slab, k_Saddle, k_SaddleHalf, k_SaddleRadius, k_Saddle.y, k_Saddle.y - 2.2f);
            Pave(slab);
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
                // Distância até a beira do estádio (reta no meio, meio círculo nas pontas)
                float dx = Mathf.Max(0f, Mathf.Abs(p.x - k_Saddle.x) - k_SaddleHalf);
                SnowPile(p, k_SaddleRadius - Mathf.Sqrt(dx * dx + (p.z - k_Saddle.z) * (p.z - k_Saddle.z)), R(1.2f, 3f), R(1.2f, 3f),
                    R(0.3f, 0.9f));
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
                // Tampas: os arcos em leque a partir do centro da ponta; as bordas retas fecham o retângulo inteiro até o
                // eixo (um leque só a partir do meio deixava quatro triângulos sem chão)
                float innerA = Mathf.Clamp(a.x, -half, half), innerB = Mathf.Clamp(b.x, -half, half);
                foreach ((float y, Vector3 p, Vector3 q, Vector3 facing) in new[] { (yTop, at, bt, Vector3.up), (yBottom, ab, bb, Vector3.down) })
                {
                    var ia = new Vector3(center.x + innerA, y, center.z);
                    var ib = new Vector3(center.x + innerB, y, center.z);
                    if (Mathf.Abs(innerA - innerB) < 0.01f)
                        mesh.AddTriangleAuto(ia, p, q, facing, 4f);
                    else
                        mesh.AddQuadAuto(ia, p, q, ib, facing, 4f);
                }
            }
        }

        // Ponte de corrente de 'from' a 'to' (na altura do tabuleiro nas pontas), com o tabuleiro cedendo 'sag' metros no
        // meio: pilares de ancoragem de pedra com capitel e argola em cada canto, duas correntes de elos alternados (o
        // corrimão: 4,2 m nas pontas, 1,1 m no meio), pendurais de ferro a cada 2,5 m, tábuas de pedra sobre duas
        // longarinas de ferro (três faltando, vãos pequenos) e pingentes de gelo embaixo. Colisão invisível: passarela em trechos e muretas dos lados (não dá para cair pelas correntes).
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

        // ---------- sala 19: Bastião em Terraços ----------

        // Ponto do forte em coordenadas locais (x para oeste, z para o sul, y a partir do chão do pátio)
        static Vector3 B(float x, float y, float z) => k_Bastion + new Vector3(-x, y, -z);

        // Giro local do forte (o eixo z local aponta para o sul)
        static Quaternion BRot(float yaw = 0f) => Quaternion.Euler(0f, 180f + yaw, 0f);

        // Caixa de cantaria no forte, em coordenadas locais
        static void BBox(MeshData mesh, float x, float y, float z, Vector3 size, float yaw = 0f, float chamfer = 0.1f) =>
            BlockMesh.ChamferBox(mesh, B(x, y, z), size, BRot(yaw), chamfer);

        const float k_TerraceMid = 3.5f, k_TerraceHigh = 7f;

        // Altura do chão do forte num ponto local: pátio (0), terraço do meio (+3,5) ou de cima (+7)
        static float BastionFloor(float x, float z)
        {
            if (z >= 16f && z <= 56f && x >= -44f && x <= 40f)
                return k_TerraceHigh;
            if (z >= -12f && z <= 56f && x >= -52f && x <= 48f)
                return k_TerraceMid;
            return 0f;
        }

        // A sala 19 e o caminho até ela. Do patamar depois da ponte de corrente o caminho vira para o sudoeste e sobe aos
        // poucos, longo e irregular: ponte de pedra até um ressalto, trilha estreita escavada colada num paredão (mureta
        // do lado do vazio), zigue-zague de pedras soltas subindo, um arco natural de rocha servindo de ponte, trilha até
        // a boca de uma fenda entre dois paredões altos (com uma rocha entalada no alto) e, da saída da fenda, a
        // escadaria até o portão do forte, chegando por baixo. O forte ocupa uma plataforma plana na encosta norte de um
        // maciço (paredão atrás e esporões de rocha nas laterais do fundo), em três terraços.
        static void BuildBastion()
        {
            s_BastionPortal = null;
            s_NicheRune = null;
            s_BastionMountain = PlaceBastionMountain();
            BuildBastionApproach();
            BuildBastionShelf();
            BuildBastionTerraces();
            BuildBastionWalls();
            BuildBastionInterior();
            Clear(k_Bastion + Vector3.back * 10f, 110f);
        }

        static void BuildBastionApproach()
        {
            Vector3 landingEdge = new Vector3(k_Landing.x, k_Landing.y,
                k_Landing.z - k_LandingRadius * Mathf.Cos(22.5f * Mathf.Deg2Rad) + 0.3f);
            var ledgeA = new Vector3(-48f, -24f, -848f);
            var ledgeB = new Vector3(-100f, -19f, -874f);
            var ledgeD = new Vector3(-150f, -11f, -886f);
            var ledgeE1 = new Vector3(-200f, -9f, -872f);
            var ledgeE = new Vector3(-232f, -11f, -880f);
            Vector3 gate = B(-10f, 0f, k_BastionFront);
            Vector3 ledgeF = gate + new Vector3(0f, -5f, 21f);

            // 1. Ponte de pedra do patamar até o primeiro ressalto (descendo um pouco)
            RockLedge(ledgeA, 10f, 701);
            Bridge(landingEdge, Edge(ledgeA, 10f, landingEdge), 5f);
            ClearLine(landingEdge, ledgeA);

            // 2. Trilha escavada colada num paredão, com mureta do lado do vazio
            RockLedge(ledgeB, 8f, 711);
            Vector3 a = Edge(ledgeA, 10f, ledgeB), b = Edge(ledgeB, 8f, ledgeA);
            Vector3 dir = (b - a).normalized;
            Vector3 left = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z)).normalized;
            Vector3 cliffSide = left.z > 0f ? left : -left;      // o paredão fica do lado norte da trilha
            CliffPath(a, b, cliffSide, 712);
            ClearLine(ledgeA, ledgeB);

            // 3. Zigue-zague de pedras soltas subindo
            RockLedge(ledgeD, 9f, 721);
            Vector3 s0 = Edge(ledgeB, 8f, ledgeD), s3 = Edge(ledgeD, 9f, ledgeB);
            Trail(new[]
            {
                s0, new Vector3(-118f, -16f, -892f), new Vector3(-112f, -14f, -903f), new Vector3(-132f, -12.5f, -906f), s3,
            }, 22);
            ClearLine(ledgeB, ledgeD);

            // 4. Arco natural de rocha servindo de ponte
            RockLedge(ledgeE1, 9f, 731);
            NaturalArch(Edge(ledgeD, 9f, ledgeE1), Edge(ledgeE1, 9f, ledgeD), 5f);
            ClearLine(ledgeD, ledgeE1);

            // 5. Trilha curta até a boca da fenda
            RockLedge(ledgeE, 8f, 741);
            Trail(new[] { Edge(ledgeE1, 9f, ledgeE), Edge(ledgeE, 8f, ledgeE1) }, 6);
            ClearLine(ledgeE1, ledgeE);

            // 6. A fenda entre dois paredões
            RockLedge(ledgeF, 7f, 751);
            Fissure(Edge(ledgeE, 8f, ledgeF), Edge(ledgeF, 7f, ledgeE), 752);
            ClearLine(ledgeE, ledgeF);

            // 7. Escadaria até o portão do forte
            Stairs(Edge(ledgeF, 7f, gate), gate + Vector3.back * 0.5f, 6f);
            if (!s_BastionMountain)
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = (Edge(ledgeF, 7f, gate) + gate) * 0.5f + Vector3.down * 5f, TopRadius = 6f, BaseRadius = 14f, Taper = 2.5f,
                Cap = PicosMountainKit.Cap.Flat, Sides = 20, RingStep = 2.5f, Corners = 5, Flutes = 3, FluteDepth = 0.07f,
                BedHeight = 9f, BedShift = 0.08f, Waist = 0.1f, Roughness = 0.07f, Seed = 753,
            });
        }

        // Trilha estreita escavada na lateral de um paredão: o paredão (pilares altos) de um lado, a trilha de neve no
        // meio e uma mureta de blocos do lado do vazio, interrompida aqui e ali
        static void CliffPath(Vector3 a, Vector3 b, Vector3 cliffSide, int seed)
        {
            Trail(new[] { a, b }, 0);
            Vector3 along = b - a;
            float length = along.magnitude;
            Vector3 mid = (a + b) * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float t = (k + 0.5f) / 3f;
                float r = R(13f, 17f);
                Vector3 c = Vector3.Lerp(a, b, t) + cliffSide * (r + 5f);
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = new Vector3(c.x, mid.y + R(26f, 42f), c.z), TopRadius = r, BaseRadius = r * 1.6f, Taper = 3f,
                    Cap = PicosMountainKit.Cap.Crag, CapHeight = r * 0.3f, Sides = 34, RingStep = 2.5f, Corners = 6,
                    Flutes = 5, FluteDepth = 0.08f, BedHeight = R(9f, 14f), BedShift = 0.08f, Waist = 0.12f,
                    Roughness = 0.07f, Seed = seed + k * 7, Bundle = false,  // o paredão não invade a trilha
                });
            }

            // Mureta do lado do vazio, cada bloco assentado num beiral de rocha próprio (o espinhaço da trilha não chega
            // tão longe do centro); a rocha segue o caminho mesmo onde falta um bloco
            Quaternion rotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.z));
            for (float d = 1.5f; d < length - 1.5f; d += 2.6f)
            {
                Vector3 p = Vector3.Lerp(a, b, d / length) - cliffSide * 2.1f;
                float r = R(1.7f, 2.1f);
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = p + Vector3.down * 0.2f, BottomY = p.y - R(5f, 8f), TopRadius = r, BaseRadius = r * 1.3f, Taper = 1.5f,
                    Cap = PicosMountainKit.Cap.Flat, Sides = 10, RingStep = 0f, Corners = 5 + s_Random.Next(2), Flutes = 2,
                    FluteDepth = 0.08f, BedHeight = R(2f, 3.5f), BedShift = 0.1f, Waist = 0.08f, Roughness = 0.1f,
                    Seed = R(0f, 1000f), Bundle = false, Track = false,
                });
                if (s_Random.NextDouble() < 0.18)
                    continue;
                float h = R(0.6f, 0.95f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), p + Vector3.up * (h * 0.5f - 0.3f), new Vector3(0.6f, h, 2.3f), rotation, 0.06f);
            }

            // Pedras caídas do paredão encostadas na base dele
            for (int k = 0; k < 6; k++)
                ArquipelagoRuinKit.IrregularRock(s_Ruins, Vector3.Lerp(a, b, R(0.1f, 0.9f)) + cliffSide * 1.6f + Vector3.up * 0.3f,
                    R(0.5f, 1.1f), s_Random);
        }

        // Arco natural de rocha entre dois ressaltos: um tabuleiro de rocha que afina no meio (3 m) e engrossa nas pontas
        // (até 18 m), em fatias de rocha com ruído, e a trilha de neve por cima
        static void NaturalArch(Vector3 a, Vector3 b, float width)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            Quaternion rotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.z));
            int slices = Mathf.CeilToInt(length / 2f);
            for (int i = 0; i < slices; i++)
            {
                float t = (i + 0.5f) / slices;
                Vector3 top = Vector3.Lerp(a, b, t);
                float centered = 2f * t - 1f;
                float thick = 3f + 15f * centered * centered + R(-0.6f, 0.6f);
                float w = width + 1.5f + 3f * centered * centered + R(-0.4f, 0.4f);
                BlockMesh.ChamferBox(s_MountainNear, top + Vector3.down * (thick * 0.5f + 0.3f) + rotation * Vector3.right * R(-0.4f, 0.4f),
                    new Vector3(w, thick, length / slices + 0.8f), rotation * Quaternion.Euler(R(-3f, 3f), R(-4f, 4f), R(-3f, 3f)), 0.4f);
            }

            Path(a, b, width);
            for (int k = 0; k < 8; k++)
                Spike(s_Ice, Vector3.Lerp(a, b, R(0.3f, 0.7f)) + Vector3.down * 3.2f + rotation * Vector3.right * R(-1.8f, 1.8f),
                    Vector3.down, R(0.6f, 2.6f), R(0.12f, 0.3f), 5, R(0f, 60f), 3f);
        }

        // Fenda: a trilha passa entre dois paredões altos. Com a montanha do Blender (Montanha_Bastiao) os paredões são
        // dela; sem ela, pilares de rocha dos dois lados. (A rocha entalada no alto saiu: parecia solta no ar.)
        static void Fissure(Vector3 a, Vector3 b, int seed)
        {
            Trail(new[] { a, b }, 4);
            if (s_BastionMountain)
                return;
            Vector3 along = b - a;
            Vector3 side = Vector3.Cross(Vector3.up, new Vector3(along.x, 0f, along.z)).normalized;
            // Os paredões ficam na primeira metade (a saída se abre de frente para o forte)
            foreach (float t in new[] { 0.15f, 0.38f, 0.6f })
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    float r = R(7f, 10f);
                    Vector3 c = Vector3.Lerp(a, b, t) + side * s * (r + 3.4f);
                    PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                    {
                        Top = new Vector3(c.x, a.y + R(18f, 28f), c.z), TopRadius = r, BaseRadius = r * 1.5f, Taper = 3f,
                        Cap = PicosMountainKit.Cap.Crag, CapHeight = r * 0.3f, Sides = 26, RingStep = 2.5f, Corners = 5,
                        Flutes = 4, FluteDepth = 0.08f, BedHeight = R(7f, 11f), BedShift = 0.09f, Waist = 0.12f,
                        Roughness = 0.08f, Seed = seed + (int)(t * 100f) + (s > 0f ? 5 : 0),
                    });
                }
            }

        }

        // A plataforma: chão de rocha e neve de contorno irregular (com a beira da frente recortada), pilares de rocha
        // embaixo até a nuvem, o maciço atrás e esporões de rocha nas laterais do fundo
        static void BuildBastionShelf()
        {
            // Retângulo arredondado (superelipse) com a beira da frente e dos lados irregular; atrás entra no maciço
            var outline = new List<Vector3>();
            const int count = 48;
            const float halfWidth = k_BastionHalfWidth + 6f;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                float depth = cz < 0f ? -k_BastionFront + 4f : k_BastionBack + 12f;
                float radius = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(cx) / halfWidth, 4f) + Mathf.Pow(Mathf.Abs(cz) / depth, 4f), 0.25f);
                if (cz < 0.3f)
                    radius *= 1f + R(-0.05f, 0.04f);
                outline.Add(B(cx * radius, -0.35f, cz * radius));
            }
            ExtrudePolygon(s_MountainNear, outline, k_Bastion.y - 0.35f, k_Bastion.y - 7f);

            // Pedras soltas ao pé do paredão
            for (int k = 0; k < 26; k++)
            {
                float x = R(-40f, 38f), z = R(50f, 54f);
                if (!BastionReserved(x, z))
                    ArquipelagoRuinKit.IrregularRock(s_Ruins, B(x, k_TerraceHigh + 0.3f, z), R(0.8f, 2.2f), s_Random);
            }

            // A rocha em volta (embaixo, atrás e dos lados) é a Montanha_Bastiao, com pingentes próprios; sem ela, os
            // pilares do gerador
            if (s_BastionMountain)
                return;

            // Pilares de rocha embaixo (a encosta descendo até a nuvem)
            float[] xs = { -40f, 0f, 40f };
            float[] zs = { -35f, 5f };
            int seed = 760;
            foreach (float z in zs)
            foreach (float x in xs)
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = B(x + R(-6f, 6f), -7f, z + R(-5f, 5f)), TopRadius = R(24f, 30f), BaseRadius = 44f, Taper = 2.2f,
                    Cap = PicosMountainKit.Cap.Flat, Sides = 40, RingStep = 3f, Corners = 7, Flutes = 6, FluteDepth = 0.07f,
                    BedHeight = R(10f, 16f), BedShift = 0.08f, Waist = 0.1f, Roughness = 0.06f, Seed = seed++,
                });

            // O maciço atrás (a montanha cuja encosta segura o forte) e os esporões das laterais do fundo: uma coluna só
            // cada (um feixe espalha colunas até ~1,45x o raio, e elas atravessavam o forte), longe o bastante para a
            // rocha (até ~1,3x o raio, com as camadas mais largas) ficar fora da plataforma
            PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
            {
                Top = B(0f, 115f, 118f), TopRadius = 52f, Bundle = false, BaseRadius = 80f, Taper = 3f, Cap = PicosMountainKit.Cap.Crag,
                CapHeight = 14f, Sides = 56, RingStep = 3f, Corners = 7, Flutes = 8, FluteDepth = 0.08f, BedHeight = 18f,
                BedShift = 0.07f, Waist = 0.1f, Roughness = 0.06f, Seed = 790,
            });
            (float x, float y, float z, float r)[] spurs =
            {
                (-62f, 70f, 89f, 30f), (60f, 58f, 87f, 28f), (-85f, 22f, 26f, 16f), (84f, 16f, 30f, 15f), (-30f, 40f, 79f, 22f),
                (34f, 48f, 82f, 24f),
            };
            foreach (var spur in spurs)
                PicosMountainKit.Pillar(s_MountainNear, new PicosMountainKit.PillarSpec
                {
                    Top = B(spur.x, spur.y, spur.z), TopRadius = spur.r, BaseRadius = spur.r * 1.6f, Taper = 3f,
                    Cap = PicosMountainKit.Cap.Crag, CapHeight = spur.r * 0.3f, Sides = 40, RingStep = 3f, Corners = 6,
                    Flutes = 6, FluteDepth = 0.08f, BedHeight = R(12f, 18f), BedShift = 0.08f, Waist = 0.12f,
                    Roughness = 0.07f, Seed = seed++, Bundle = false,
                });

            // Pingentes na beira da frente
            for (int k = 0; k < 60; k++)
                Spike(s_Ice, B(R(-k_BastionHalfWidth + 8f, k_BastionHalfWidth - 8f), -0.6f, k_BastionFront - 2.5f), Vector3.down,
                    R(0.6f, 3f), R(0.12f, 0.35f), 5, R(0f, 60f), 3f);
        }

        // Extrusão de um polígono (pontos em volta, no mundo) de yTop a yBottom, com tampa em leque a partir do meio
        static void ExtrudePolygon(MeshData mesh, List<Vector3> outline, float yTop, float yBottom)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in outline)
                center += p;
            center /= outline.Count;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 at = new Vector3(a.x, yTop, a.z), bt = new Vector3(b.x, yTop, b.z);
                Vector3 ab = new Vector3(a.x, yBottom, a.z), bb = new Vector3(b.x, yBottom, b.z);
                Vector3 outward = (a + b) * 0.5f - center;
                outward.y = 0f;
                mesh.AddQuadAuto(at, bt, bb, ab, outward.normalized, 4f);
                mesh.AddTriangleAuto(new Vector3(center.x, yTop, center.z), at, bt, Vector3.up, 4f);
            }
        }

        // Os três terraços (pátio, meio e alto) com piso de lajes, os muros de arrimo com fiadas e talude, rampas e
        // escadarias entre eles
        static void BuildBastionTerraces()
        {
            var slab = new MeshData();
            s_Platforms.Add((B(0f, 0f, -5f), 62f));
            // Pátio (frente) até o pé do maciço, meio e alto: cada um uma laje grossa que desce até a rocha
            BlockMesh.ChamferBox(slab, B(0f, -2f, -10f), new Vector3(k_BastionHalfWidth * 2f, 4f, 92f), BRot(), 0.2f);
            BlockMesh.ChamferBox(slab, B(-2f, k_TerraceMid * 0.5f - 1.5f, 22f), new Vector3(100f, k_TerraceMid + 3f, 68f), BRot(), 0.15f);
            BlockMesh.ChamferBox(slab, B(-2f, k_TerraceHigh * 0.5f - 1.5f, 36f), new Vector3(84f, k_TerraceHigh + 3f, 40f), BRot(), 0.15f);
            Pave(slab);

            // Talude e fiadas salientes nos muros de arrimo (frente de cada terraço), abertos nas rampas e escadas
            (float x0, float x1, float z, float top)[] fronts = { (-52f, 48f, -12f, k_TerraceMid), (-44f, 40f, 16f, k_TerraceHigh) };
            float[] openings = { -34f, 30f, 0f, -36f };
            foreach (var front in fronts)
            {
                float bottom = front.top - k_TerraceMid;
                for (float x = front.x0 + 2f; x < front.x1 - 1f; x += 4f)
                {
                    bool open = false;
                    foreach (float o in openings)
                        open |= Mathf.Abs(x - o) < 6f;
                    if (open)
                        continue;
                    BBox(s_Ruins.Pick(), x, bottom + 0.5f, front.z - 0.9f, new Vector3(4.02f, 1f, 1.2f), 0f, 0.08f);
                    BBox(s_Ruins.Pick(), x, front.top - 0.3f, front.z - 0.25f, new Vector3(4.02f, 0.5f, 0.7f), 0f, 0.06f);
                }
            }

            // Rampas do pátio ao meio (com muretas) e do meio ao alto; escadaria larga no meio
            Ramp(-34f, -12f, 0f, k_TerraceMid, 8f);
            Ramp(30f, -12f, 0f, k_TerraceMid, 8f);
            Ramp(-36f, 16f, k_TerraceMid, k_TerraceHigh, 7f);
            Stairs(B(0f, k_TerraceMid, 6f), B(0f, k_TerraceHigh, 16f), 8f);
        }

        // Rampa pavimentada da frente de um terraço (x local, z da beira) subindo para o sul, com muretas dos lados
        static void Ramp(float x, float edgeZ, float low, float high, float width)
        {
            float run = (high - low) * 3f;
            Vector3 from = B(x, low, edgeZ - run), to = B(x, high, edgeZ + 0.5f);
            Vector3 along = to - from;
            Quaternion rotation = Quaternion.LookRotation(along);
            var ramp = new MeshData();
            BlockMesh.ChamferBox(ramp, (from + to) * 0.5f + rotation * Vector3.down * 0.6f, new Vector3(width, 1.2f, along.magnitude),
                rotation, 0.08f);
            Pave(ramp);
            // Enchimento embaixo da rampa, em degraus que acompanham a subida (sem furar a rampa)
            const int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float top = (high - low) * t - 1.1f;
                if (top < 0.3f)
                    continue;
                BBox(s_Ruins.Pick(), x, low + top * 0.5f, edgeZ - run * (1f - t), new Vector3(width - 0.3f, top, run / steps + 0.02f), 0f, 0.05f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 offset = BRot() * Vector3.right * s * (width * 0.5f + 0.35f);
                BlockMesh.ChamferBox(s_Ruins.Pick(), (from + to) * 0.5f + offset + rotation * Vector3.up * 0.35f,
                    new Vector3(0.6f, 0.9f, along.magnitude), rotation, 0.06f);
            }
        }

        // Muralhas: a cortina da frente quase toda destruída (trechos inteiros com ameias, tocos e montes de escombros),
        // o portão entre duas bases de torre, os dois baluartes dos cantos e um pedaço de muralha tombado que virou
        // rampa até o baluarte leste; parapeitos com ameias na beira dos terraços de cima
        static void BuildBastionWalls()
        {
            float wallZ = k_BastionFront + 2f;
            for (float x = -k_BastionHalfWidth + 4f; x < k_BastionHalfWidth - 4f; x += 6f)
            {
                if (Mathf.Abs(x + 10f) < 7f || Mathf.Abs(x) > 42f)
                    continue;   // o portão e os baluartes
                float roll = (float)s_Random.NextDouble();
                if (roll < 0.32f)
                    CurtainSegment(x, wallZ, 4.6f, true);
                else if (roll < 0.7f)
                    CurtainSegment(x, wallZ, R(0.8f, 1.8f), false);
                else
                    for (int k = 0; k < 4; k++)
                        ArquipelagoRuinKit.IrregularRock(s_Ruins, B(x + R(-3f, 3f), 0.4f, wallZ + R(-2.5f, 2.5f)), R(0.7f, 1.5f), s_Random);
            }

            // Portão: duas bases de torre (uma bem mais arruinada) e a soleira, com os blocos do arco caídos na frente
            foreach ((float x, float h) in new[] { (-17f, 9f), (-3f, 4f) })
            {
                BBox(s_Ruins.Pick(), x, h * 0.5f, wallZ, new Vector3(6f, h, 6.5f), 0f, 0.15f);
                BBox(s_Ruins.Pick(), x, h + 0.3f, wallZ, new Vector3(6.6f, 0.6f, 7f), 0f, 0.1f);
                for (int k = 0; k < 3; k++)
                    BBox(s_Ruins.Pick(), x + R(-2f, 2f), h + 1f, wallZ + R(-2f, 2f), new Vector3(R(1f, 1.8f), R(0.8f, 1.6f), R(1f, 1.8f)), R(0f, 90f));
            }
            BBox(s_Ruins.Pick(), -10f, 0.15f, wallZ, new Vector3(8f, 0.3f, 6.5f), 0f, 0.05f);
            for (int k = 0; k < 5; k++)
                BBox(s_Ruins.Pick(), -10f + R(-5f, 5f), R(0.4f, 0.8f), wallZ - R(3f, 8f), new Vector3(R(1.2f, 2f), 0.9f, R(0.8f, 1.2f)),
                    R(0f, 180f), 0.08f);

            // Baluartes nos cantos da frente: plataforma de 3 m saindo da linha da muralha, parapeito com ameias por fora
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = B(side * 48f, 0f, k_BastionFront + 6f);
                var bastion = new MeshData();
                Prism(bastion, c + Vector3.up * 3f, 10f, 10.6f, 5, c.y + 3f, c.y - 7f, true);
                Pave(bastion);
                for (int k = 0; k < 5; k++)
                {
                    Vector3 normal = SideNormal(k, 5);
                    if (Vector3.Dot(normal, B(0f, 0f, 0f) - c) > 0.3f * 10f)
                        continue;   // o lado virado para dentro do pátio fica aberto
                    if (side < 0f && Vector3.Dot(normal, BDir(0f, 0f, 1f)) > 0.5f)
                        continue;   // no baluarte leste, o lado por onde a passarela chega
                    Vector3 edge = c + normal * 7.4f + Vector3.up * 3f;
                    Quaternion facing = Quaternion.LookRotation(normal);
                    if (s_Random.NextDouble() < 0.3)
                        continue;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), edge + Vector3.up * 0.55f, new Vector3(9.5f, 1.1f, 0.9f), facing, 0.08f);
                    for (int m = -1; m <= 1; m++)
                        BlockMesh.ChamferBox(s_Ruins.Pick(), edge + facing * Vector3.right * m * 3.2f + Vector3.up * 1.6f,
                            new Vector3(1.4f, 1f, 0.9f), facing, 0.08f);
                }
            }
            // Oeste: escada do pátio para o baluarte; leste: um pedaço de muralha tombado serve de rampa
            Stairs(B(36f, 0f, k_BastionFront + 10f), B(42f, 3f, k_BastionFront + 7f), 4f);
            Vector3 low = B(-34f, 0f, -36f), high = B(-43f, 3f, k_BastionFront + 9f);
            Quaternion lean = Quaternion.LookRotation(high - low);
            BlockMesh.ChamferBox(s_Ruins.Pick(), (low + high) * 0.5f + lean * Vector3.down * 1.1f, new Vector3(5.5f, 2.2f, (high - low).magnitude + 1f),
                lean * Quaternion.Euler(0f, 0f, R(-4f, 4f)), 0.15f);
            for (int k = 0; k < 4; k++)
                ArquipelagoRuinKit.IrregularRock(s_Ruins, Vector3.Lerp(low, high, R(0f, 1f)) + BRot() * Vector3.right * R(-3.5f, 3.5f),
                    R(0.6f, 1.2f), s_Random);

            // Parapeitos com ameias na beira dos terraços de cima (abertos nas rampas e na escadaria)
            (float x0, float x1, float z, float top)[] edges = { (-52f, 48f, -12f, k_TerraceMid), (-44f, 40f, 16f, k_TerraceHigh) };
            float[] gaps = { -34f, 30f, 0f, -36f };
            foreach (var edge in edges)
            {
                for (float x = edge.x0 + 3f; x < edge.x1 - 2f; x += 5f)
                {
                    bool gap = false;
                    foreach (float g in gaps)
                        gap |= Mathf.Abs(x - g) < 6.5f;
                    if (gap || s_Random.NextDouble() < 0.28 || edge.z < 0f && (InCloister(x, -14f) || Mathf.Abs(x + 46f) < 5.5f))
                        continue;
                    float h = s_Random.NextDouble() < 0.3 ? R(0.4f, 0.7f) : 1f;
                    BBox(s_Ruins.Pick(), x, edge.top + h * 0.5f, edge.z + 0.5f, new Vector3(4.6f, h, 0.8f), 0f, 0.06f);
                    if (h > 0.9f && s_Random.NextDouble() < 0.6)
                        BBox(s_Ruins.Pick(), x + R(-1f, 1f), edge.top + 1.5f, edge.z + 0.5f, new Vector3(1.3f, 1f, 0.8f), 0f, 0.06f);
                }
            }
        }

        // Trecho de 6 m da cortina: inteiro (com caminho de ronda e ameias) ou só um toco
        static void CurtainSegment(float x, float z, float height, bool merlons)
        {
            BBox(s_Ruins.Pick(), x, 0.5f, z - 0.4f, new Vector3(6.02f, 1f, 3f), 0f, 0.1f);    // base em talude
            BBox(s_Ruins.Pick(), x, height * 0.5f, z, new Vector3(6.02f, height, 2.2f), 0f, 0.1f);
            if (!merlons)
            {
                ArquipelagoRuinKit.IrregularRock(s_Ruins, B(x + R(-2f, 2f), 0.4f, z + R(1.8f, 3f)), R(0.6f, 1.2f), s_Random);
                return;
            }
            BBox(s_Ruins.Pick(), x, height + 0.25f, z, new Vector3(6.3f, 0.5f, 2.6f), 0f, 0.06f);
            for (int m = -1; m <= 1; m += 2)
                BBox(s_Ruins.Pick(), x + m * 1.6f, height + 1.2f, z - 0.7f, new Vector3(1.6f, 1.4f, 0.8f), 0f, 0.06f);
        }

        // Dentro do forte: no pátio, a arcada encostada no muro do meio, a cisterna congelada, um anel de pedra caído e
        // escombros; no meio, as fundações dos alojamentos (labirinto de muros baixos); em cima, a torre de menagem
        // partida, a torre redonda quebrada encostada na rocha com o topo tombado e um monólito de runas. Neve encostada
        // nos muros.
        static void BuildBastionInterior()
        {
            BuildCloister();
            BuildForge();
            BuildStores();
            BuildScaffold();
            BuildWalkway();
            BuildRingMachine();

            // Cisterna congelada
            Vector3 cistern = B(16f, 0f, -32f);
            for (int k = 0; k < 16; k++)
            {
                if (k == 5 || k == 6)
                    continue;   // um pedaço do parapeito caiu
                float a = k / 16f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                BlockMesh.ChamferBox(s_Ruins.Pick(), cistern + dir * 4.6f + Vector3.up * 0.55f, new Vector3(0.9f, 1.1f, 1.9f),
                    Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir)), 0.06f);
            }
            var ice = new List<Vector3>();
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                ice.Add(cistern + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.2f);
            }
            ExtrudePolygon(s_Ice, ice, cistern.y + 0.2f, cistern.y + 0.1f);

            // Anel de pedra caído (uma peça das máquinas de anéis), partido em três pedaços
            Vector3 ringCenter = B(-26f, 0f, -34f);
            foreach ((float from, float to) in new[] { (10f, 120f), (150f, 230f), (260f, 330f) })
                ArquipelagoRuinKit.RichRing(s_Ruins, ringCenter + Vector3.up * 1.3f, Vector3.up, Vector3.forward, 6f, 1.2f, 2.6f, from, to,
                    s_Random, true, true, ringCenter.y, 3);

            // Escombros e colunas partidas espalhados pelo pátio
            (float x, float z)[] columns = { (-2f, -33f), (-20f, -22f), (-41f, -19f), (26f, -42f), (-20f, -46f) };
            foreach (var c in columns)
                ArquipelagoRuinKit.Column(s_Ruins, B(c.x, 0f, c.z), R(2f, 4.5f), 0.8f, s_Random, true);
            for (int k = 0; k < 14; k++)
            {
                float x = R(-50f, 50f), z = R(-48f, -16f);
                if (Mathf.Abs(x + 10f) < 6f && z < -40f || BastionReserved(x, z))
                    continue;   // a entrada do portão e o claustro ficam livres
                ArquipelagoRuinKit.IrregularRock(s_Ruins, B(x, 0.4f, z), R(0.7f, 1.6f), s_Random);
            }
            for (int k = 0; k < 4; k++)
            {
                float x = R(-46f, 46f), z = R(-44f, -18f);
                if (!BastionReserved(x, z))
                    BBox(s_Ruins.Pick(), x, 0.5f, z, new Vector3(R(2.5f, 4f), 1f, R(1f, 1.6f)), R(0f, 180f), 0.08f);
            }

            // Alojamentos: fundações em grade no oeste do terraço do meio (muros baixos com portas)
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 3; j++)
            {
                float x0 = -48f + i * 8f, z0 = -9f + j * 7.5f;
                if (z0 + 7.5f > 16f)
                    continue;
                foreach ((float dx, float dz, bool alongX) in new[] { (4f, 0f, true), (0f, 3.75f, false) })
                {
                    if (s_Random.NextDouble() < 0.3)
                        continue;
                    float h = R(0.6f, 1.8f);
                    float length = alongX ? 8f : 7.5f;
                    // Porta: o muro tem um vão no meio
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float part = length * 0.5f - 0.8f;
                        float cx = x0 + (alongX ? dx + s * (0.8f + part * 0.5f) : 0f);
                        float cz = z0 + (alongX ? 0f : dz + s * (0.8f + part * 0.5f));
                        // As chegadas das rampas (do pátio e para o terraço de cima) ficam livres
                        bool rampTop = Mathf.Abs(cx + 34f) < 6f && cz < -3f;
                        bool rampUp = cx > -42f && cx < -30f && cz > 3f;
                        if (rampTop || rampUp)
                            continue;
                        BBox(s_Ruins.Pick(), cx, k_TerraceMid + h * 0.5f, cz,
                            alongX ? new Vector3(part, h, 0.7f) : new Vector3(0.7f, h, part), 0f, 0.06f);
                    }
                }
            }

            // Torre de menagem partida no terraço de cima (paredes de alturas diferentes, porta ao norte, canto caído)
            const float keepX = -14f, keepZ = 30f, half = 7f;
            for (int side = 0; side < 4; side++)
            {
                for (float t = -half + 1f; t <= half - 1f; t += 2f)
                {
                    bool door = side == 0 && Mathf.Abs(t) < 2f;
                    bool collapsed = side == 1 && t > 2f || side == 2 && t < -3f;
                    if (door)
                        continue;
                    float h = collapsed ? R(0.6f, 1.6f) : 4f + Mathf.PerlinNoise(side * 3.1f, t * 0.4f) * 5f;
                    float x = side == 0 || side == 2 ? keepX + t : keepX + (side == 1 ? half : -half);
                    float z = side == 0 ? keepZ - half : side == 2 ? keepZ + half : keepZ + t;
                    bool alongX = side == 0 || side == 2;
                    BBox(s_Ruins.Pick(), x, k_TerraceHigh + h * 0.5f, z, alongX ? new Vector3(2.02f, h, 1.2f) : new Vector3(1.2f, h, 2.02f),
                        0f, 0.06f);
                }
            }
            for (int k = 0; k < 6; k++)
                ArquipelagoRuinKit.IrregularRock(s_Ruins, B(keepX + R(-5f, 5f), k_TerraceHigh + 0.4f, keepZ + R(-5f, 5f)), R(0.8f, 1.6f), s_Random);
            for (int k = 0; k < 5; k++)
                BBox(s_Ruins.Pick(), keepX + half + R(1f, 5f), k_TerraceHigh + 0.5f, keepZ + R(-1f, 6f), new Vector3(R(1.2f, 2.2f), 1f, R(1.2f, 2.2f)),
                    R(0f, 90f));

            // Torre redonda quebrada encostada na rocha, com o topo tombado sobre o terraço
            Vector3 tower = B(28f, k_TerraceHigh, 36f);
            Prism(s_Ruins.Pick(), tower, 5.2f, 5.6f, 14, tower.y + 13f, tower.y - 4f, true);
            for (float y = tower.y + 3f; y < tower.y + 13f; y += 3.4f)
                Prism(s_Ruins.Pick(), tower, 5.5f, 5.5f, 14, y + 0.25f, y - 0.25f, false);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f + R(-10f, 10f);
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                BlockMesh.ChamferBox(s_Ruins.Pick(), tower + dir * 4.6f + Vector3.up * (13f + R(0.5f, 2.5f)),
                    new Vector3(2.2f, R(1f, 3f), 1.2f), Quaternion.LookRotation(dir), 0.08f);
            }
            Vector3 fallen = B(16f, k_TerraceHigh, 26f);
            Quaternion fall = BRot(-50f) * Quaternion.Euler(0f, 0f, 90f);
            for (int k = 0; k < 3; k++)
                BlockMesh.ChamferBox(s_Ruins.Pick(), fallen + BRot(-50f) * Vector3.forward * (k * 3.6f - 3.6f) + Vector3.up * 2.4f,
                    new Vector3(4.8f, 4.8f, 3.3f), fall * Quaternion.Euler(R(-6f, 6f), 0f, R(0f, 40f)), 0.3f);

            // Monólito de runas no terraço de cima (as runas ainda acesas, fracas)
            Vector3 monolith = B(-34f, k_TerraceHigh, 34f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), monolith + Vector3.up * 0.5f, new Vector3(4f, 1f, 3f), BRot(15f), 0.1f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), monolith + Vector3.up * 4.5f, new Vector3(2.4f, 7f, 1.4f), BRot(15f), 0.1f);
            var runes = new MeshData();
            Vector3 face = monolith + BRot(15f) * Vector3.back * 0.72f + Vector3.up * 4.5f;
            Vector3 across = BRot(15f) * Vector3.right;
            runes.AddQuadAuto(face - across * 0.8f - Vector3.up * 2.6f, face + across * 0.8f - Vector3.up * 2.6f,
                face + across * 0.8f + Vector3.up * 2.6f, face - across * 0.8f + Vector3.up * 2.6f, BRot(15f) * Vector3.back, 4f);
            GridSceneKit.CreateObject(k_Prefix + "_Runas_Bastiao", "Runas_Bastiao", runes, s_Materials.Rune, s_World, false, false);
            Light(monolith + Vector3.up * 4f + BRot(15f) * Vector3.back * 2f, new Color(0.3f, 1f, 0.95f), 1.6f, 12f);

            // Neve encostada nos muros e nos cantos dos três terraços
            for (int k = 0; k < 70; k++)
            {
                float x = R(-54f, 54f), z = R(-50f, 42f);
                float floor = BastionFloor(x, z);
                bool nearWall = z < -46f || Mathf.Abs(z + 12f) < 3f || Mathf.Abs(z - 16f) < 3f || z > 38f || Mathf.Abs(x) > 50f;
                if (!nearWall && s_Random.NextDouble() < 0.75 || BastionReserved(x, z))
                    continue;
                Mound(s_Snow, B(x, floor, z), R(1.4f, 3.4f), R(1.4f, 3.4f), R(0.3f, 1.1f), 10);
            }
        }

        // ---------- claustro ----------

        // Claustro encostado no muro de arrimo do terraço do meio, no lado leste do pátio (referência: claustro românico
        // nevado): galeria de 7 vãos ao longo do muro, com uma mureta baixa e, em cima dela, pares de colunas finas com
        // capitel e ábaco (pilares quadrados de cantaria nas pontas e no meio), arcos de aduelas, paredes de fiadas entre
        // os arcos, teto de laje com cornija e o forro de vigas de madeira por baixo; o corredor coberto fica entre os
        // arcos e o muro. Dois vãos desabaram: o par de colunas do meio deles partiu (uma caída), os arcos e o teto
        // caíram num monte de aduelas e blocos, e as vigas do forro ficaram penduradas do muro até o chão. Entra-se
        // pelos escombros ou por um vão sem mureta perto da outra ponta.
        const float k_CloisterX0 = 1f, k_CloisterPitch = 3.15f, k_CloisterFront = -18f, k_CloisterBack = -12f;
        const int k_CloisterBays = 7;
        const float k_CloisterSpring = 3.05f, k_CloisterRoof = 4.95f;

        static float CloisterSupport(int i) => k_CloisterX0 + i * k_CloisterPitch;

        // Área do claustro e dos escombros na frente dele (nada de pedras soltas, colunas ou montes do pátio ali)
        static bool InCloister(float x, float z) =>
            x > CloisterSupport(0) - 2.5f && x < CloisterSupport(k_CloisterBays) + 2.5f && z > k_CloisterFront - 4.5f &&
            z < k_CloisterBack + 0.5f;

        static void BuildCloister()
        {
            const float z0 = k_CloisterFront, ys = k_CloisterSpring, roof = k_CloisterRoof;
            const float inner = k_CloisterPitch * 0.5f - 0.38f, ring = 0.42f, outer = inner + ring;
            int bays = k_CloisterBays;
            bool Pier(int i) => i == 0 || i == 4 || i == bays;
            bool Collapsed(int bay) => bay == 1 || bay == 2;
            const int brokenSupport = 2, openBay = 5;
            float collapseFrom = CloisterSupport(1) + 0.4f, collapseTo = CloisterSupport(3) - 0.4f;

            // Apoios: pilares quadrados de três fiadas com base e imposta; pares de colunas finas sobre um pedestal
            for (int i = 0; i <= bays; i++)
            {
                float x = CloisterSupport(i);
                if (Pier(i))
                {
                    BBox(s_Ruins.Pick(), x, 0.15f, z0, new Vector3(1.4f, 0.3f, 1.4f), 0f, 0.06f);
                    for (int c = 0; c < 3; c++)
                        BBox(s_Ruins.Pick(), x, 0.3f + c * 0.86f + 0.43f, z0, new Vector3(1.1f, 0.86f, 1.1f), 0f, 0.05f);
                    BBox(s_Ruins.Pick(), x, 2.965f, z0, new Vector3(1.32f, 0.17f, 1.32f), 0f, 0.04f);
                    continue;
                }

                BBox(s_Ruins.Pick(), x, 0.46f, z0, new Vector3(0.75f, 0.92f, 0.95f), 0f, 0.05f);
                foreach (float dz in new[] { -0.24f, 0.24f })
                {
                    Vector3 foot = B(x, 0f, z0 + dz);
                    BBox(s_Ruins.Pick(), x, 1f, z0 + dz, new Vector3(0.42f, 0.16f, 0.42f), 0f, 0.04f);
                    if (i == brokenSupport)
                    {
                        // Partida: só um toco (o fuste da outra caiu nos escombros)
                        if (dz < 0f)
                            Prism(s_Ruins.Pick(), foot, 0.15f, 0.17f, 8, foot.y + 1.08f + R(0.5f, 0.9f), foot.y + 1.08f, true);
                        continue;
                    }

                    Prism(s_Ruins.Pick(), foot, 0.15f, 0.17f, 8, foot.y + 2.62f, foot.y + 1.08f, true);
                    Prism(s_Ruins.Pick(), foot, 0.3f, 0.16f, 8, foot.y + 2.88f, foot.y + 2.62f, true);     // capitel em sino
                }
                if (i != brokenSupport)
                    BBox(s_Ruins.Pick(), x, 2.965f, z0, new Vector3(0.85f, 0.17f, 1.05f), 0f, 0.04f);    // ábaco
            }

            // Mureta entre os apoios, com capa; sem mureta no vão aberto e partida nos vãos desabados
            for (int bay = 0; bay < bays; bay++)
            {
                if (bay == openBay || bay == 2)
                    continue;
                float a = CloisterSupport(bay) + (Pier(bay) ? 0.55f : 0.3f);
                float b = CloisterSupport(bay + 1) - (Pier(bay + 1) ? 0.55f : 0.3f);
                if (bay == 1)
                    b = a + 0.9f;
                float h = bay == 1 ? 0.5f : 0.8f;
                BBox(s_Ruins.Pick(), (a + b) * 0.5f, h * 0.5f, z0, new Vector3(b - a, h, 0.8f), 0f, 0.05f);
                if (bay != 1)
                    BBox(s_Ruins.Pick(), (a + b) * 0.5f, 0.86f, z0, new Vector3(b - a + 0.02f, 0.12f, 0.95f), 0f, 0.03f);
            }

            // Arcos de aduelas (9 por arco, a do meio maior); nos vãos desabados só sobram as do lado do apoio inteiro
            for (int bay = 0; bay < bays; bay++)
            {
                float center = (CloisterSupport(bay) + CloisterSupport(bay + 1)) * 0.5f;
                for (int k = 0; k < 9; k++)
                {
                    if (bay == 1 && k < 7 || bay == 2 && k > 0)
                        continue;
                    float theta = (k + 0.5f) * 20f;
                    float extra = k == 4 ? 0.12f : 0f;
                    float middle = inner + (ring + extra) * 0.5f;
                    float t = theta * Mathf.Deg2Rad;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), B(center + Mathf.Cos(t) * middle, ys + Mathf.Sin(t) * middle, z0),
                        new Vector3(middle * 20f * Mathf.Deg2Rad * 0.97f, ring + extra, 0.75f), BRot() * Quaternion.Euler(0f, 0f, theta - 90f),
                        0.03f);
                }
            }

            // Paredes de fiadas entre os arcos e por cima deles, até o teto: cada fiada vai de um arco ao outro (a largura
            // ocupada por um arco na altura dela sai do círculo de fora); nos vãos desabados a parede caiu em degraus
            const float course = 0.4f;
            int row = 0;
            for (float y = ys + 0.3f; y < roof - 0.01f; y += course, row++)
            {
                float mid = y + course * 0.5f - ys;
                float dx = mid < outer ? Mathf.Sqrt(outer * outer - mid * mid) : 0f;
                float start = CloisterSupport(0) - 0.55f;
                var pieces = new List<(float a, float b)>();
                for (int bay = 0; bay <= bays; bay++)
                {
                    float end = bay < bays ? (CloisterSupport(bay) + CloisterSupport(bay + 1)) * 0.5f - dx : CloisterSupport(bays) + 0.55f;
                    if (end - start > 0.06f)
                        pieces.Add((start, end));
                    if (bay < bays)
                        start = (CloisterSupport(bay) + CloisterSupport(bay + 1)) * 0.5f + dx;
                }

                float left = collapseFrom - row * 0.18f, right = collapseTo + row * 0.12f;
                foreach (var (a, b) in pieces)
                foreach (var (from, to) in new[] { (a, Mathf.Min(b, left)), (Mathf.Max(a, right), b) })
                    if (to - from > 0.06f)
                        BBox(s_Ruins.Pick(), (from + to) * 0.5f, y + course * 0.5f, z0, new Vector3(to - from, course, 0.7f), 0f, 0.02f);
            }

            // Teto de laje por vão com cornija na frente e o muro de trás por cima da beira do terraço; o forro de vigas
            // de madeira por baixo
            float roofDepth = k_CloisterBack + 0.8f - (z0 - 0.55f), roofZ = (k_CloisterBack + 0.8f + z0 - 0.55f) * 0.5f;
            for (int bay = 0; bay < bays; bay++)
            {
                float a = CloisterSupport(bay) - (bay == 0 ? 0.7f : 0f), b = CloisterSupport(bay + 1) + (bay == bays - 1 ? 0.7f : 0f);
                bool fallen = Collapsed(bay);
                float wallTop = fallen ? k_TerraceMid + R(0.3f, 0.7f) : roof;
                BBox(s_Ruins.Pick(), (a + b) * 0.5f, (k_TerraceMid + wallTop) * 0.5f, k_CloisterBack + 0.4f,
                    new Vector3(b - a, wallTop - k_TerraceMid, 0.8f), 0f, 0.04f);
                if (fallen)
                    continue;
                BBox(s_Ruins.Pick(), (a + b) * 0.5f, roof + 0.2f, roofZ, new Vector3(b - a, 0.4f, roofDepth), 0f, 0.06f);
                BBox(s_Ruins.Pick(), (a + b) * 0.5f, roof - 0.1f, z0 - 0.5f, new Vector3(b - a, 0.2f, 0.4f), 0f, 0.04f);
                for (float x = a + 0.45f; x < b - 0.2f; x += 0.9f)
                    BlockMesh.ChamferBox(s_Wood, B(x, roof - 0.13f, (z0 + 0.35f + k_CloisterBack) * 0.5f),
                        new Vector3(0.18f, 0.24f, k_CloisterBack - z0 - 0.3f), BRot(), 0.03f);
                BlockMesh.ChamferBox(s_Wood, B((a + b) * 0.5f, roof - 0.37f, k_CloisterBack - 0.2f), new Vector3(b - a, 0.26f, 0.3f), BRot(), 0.03f);
            }

            // Desabamento: vigas do forro penduradas do alto do muro até o chão e vigas partidas sobre os escombros; no teto
            // do vão inteiro ao lado, duas vigas da cobertura soltas, saindo por cima do buraco
            for (int k = 0; k < 4; k++)
            {
                float x = R(collapseFrom + 0.3f, collapseTo - 0.3f);
                Vector3 top = B(x + R(-0.3f, 0.3f), k_TerraceMid + R(0.4f, 1f), k_CloisterBack - 0.2f);
                Vector3 bottom = B(x + R(-1.2f, 1.2f), 0.15f, R(z0 - 1.5f, z0 + 2.5f));
                Beam(top, bottom, 0.18f, 0.24f);
            }
            for (int k = 0; k < 5; k++)
            {
                Vector3 c = B(R(collapseFrom, collapseTo), 0.35f + R(0f, 0.5f), R(z0 - 2.5f, z0 + 1.5f));
                Vector3 dir = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), 0f) * Vector3.forward;
                float length = R(1.2f, 2.6f);
                Beam(c - dir * length * 0.5f, c + dir * length * 0.5f, 0.18f, 0.24f);
            }
            foreach (float offset in new[] { 0.6f, 1.6f })
            {
                float x = CloisterSupport(1) - offset;
                Beam(B(x, roof + 0.52f, k_CloisterBack - 0.8f), B(x + R(2.2f, 3.2f), roof + R(1f, 1.6f), z0 - R(0.2f, 1.2f)), 0.26f, 0.3f);
            }

            // Monte de escombros: aduelas, blocos do teto e das paredes, o fuste caído e neve por cima
            float pileX = (collapseFrom + collapseTo) * 0.5f, pileHalf = (collapseTo - collapseFrom) * 0.5f + 0.8f;
            // Núcleo do monte (os blocos de cima se apoiam nele)
            for (int k = 0; k < 3; k++)
                ArquipelagoRuinKit.IrregularRock(s_Ruins, B(pileX + (k - 1) * pileHalf * 0.45f, 0.25f, z0 - 0.5f + R(-0.6f, 0.6f)),
                    R(1.1f, 1.5f), s_Random);
            for (int k = 0; k < 46; k++)
            {
                float u = R(-1f, 1f), v = R(-1f, 1f);
                float r = u * u + v * v;
                if (r > 1f)
                    continue;
                float x = pileX + u * pileHalf, z = z0 - 0.5f + v * 3.2f;
                float height = (1f - r) * 1.1f;
                var size = new Vector3(R(0.4f, 0.8f), R(0.28f, 0.45f), R(0.45f, 0.8f));
                BlockMesh.ChamferBox(s_Ruins.Pick(), B(x, R(0f, height) + size.y * 0.5f, z), size,
                    BRot(R(0f, 180f)) * Quaternion.Euler(R(-14f, 14f), 0f, R(-14f, 14f)), 0.04f);
            }
            Vector3 shaftFoot = B(CloisterSupport(brokenSupport) + 0.4f, 0.17f, z0 - 1.2f);
            Spike(s_Ruins.Pick(), shaftFoot, BRot(R(20f, 50f)) * Vector3.right, 1.5f, 0.16f, 8, 0f, 0f, true);
            for (int k = 0; k < 3; k++)
                Mound(s_Snow, B(pileX + R(-pileHalf, pileHalf) * 0.6f, R(0.3f, 0.7f), z0 - 0.5f + R(-1.5f, 1.5f)), R(0.8f, 1.4f),
                    R(0.8f, 1.4f), R(0.25f, 0.45f), 8);

            // Neve soprada para dentro do corredor, encostada na mureta, e no pé dos apoios por fora
            for (int bay = 0; bay < bays; bay++)
            {
                if (Collapsed(bay) || s_Random.NextDouble() < 0.35)
                    continue;
                float x = (CloisterSupport(bay) + CloisterSupport(bay + 1)) * 0.5f + R(-0.6f, 0.6f);
                Mound(s_Snow, B(x, 0f, z0 + R(1f, 1.6f)), R(0.7f, 1.2f), R(0.5f, 0.8f), R(0.2f, 0.4f), 8);
            }
            for (int i = 0; i <= bays; i++)
                if (s_Random.NextDouble() < 0.5 && i != brokenSupport)
                    Mound(s_Snow, B(CloisterSupport(i), 0f, z0 - R(0.9f, 1.3f)), R(0.6f, 1f), R(0.4f, 0.7f), R(0.2f, 0.35f), 8);
        }

        // ---------- elementos do forte ----------

        // Áreas ocupadas pelos elementos do forte (claustro, forja, andaime, passarela, máquina de anéis e as pilhas de
        // barris e caixotes): pedras soltas, blocos, colunas e montes de neve espalhados ficam fora delas
        static readonly (float x0, float x1, float z0, float z1)[] k_BastionReserved =
        {
            (37f, 52f, -37.5f, -23f),       // forja
            (-14.5f, -2f, -27f, -11f),      // andaime e a escada dele
            (-49f, -43f, -42f, -11f),       // passarela
            (-10f, 24f, 26f, 50f),          // portal quebrado e o que caiu dela
            (-42f, -35.5f, -30f, -22.5f),   // carroça
            (19f, 25f, -51f, -45f), (3f, 9.5f, -43f, -37f), (-23f, -17f, -8f, -2.5f), (-15f, -9f, 7f, 12.5f),
            (21f, 28f, 3f, 9f), (-17.5f, -14.5f, -22f, -18f),      // pilhas de barris e caixotes
            (40f, 52f, -7f, 3f),            // saída oeste, para a sala 20
        };

        static bool BastionReserved(float x, float z)
        {
            if (InCloister(x, z))
                return true;
            foreach (var r in k_BastionReserved)
                if (x > r.x0 - 1.5f && x < r.x1 + 1.5f && z > r.z0 - 1.5f && z < r.z1 + 1.5f)
                    return true;
            return false;
        }

        // Direção local do forte no mundo
        static Vector3 BDir(float x, float y, float z) => BRot() * new Vector3(x, y, z);

        // Peça de madeira no forte (caixa em coordenadas locais)
        static void WBox(float x, float y, float z, Vector3 size, float yaw = 0f) =>
            BlockMesh.ChamferBox(s_Wood, B(x, y, z), size, BRot(yaw), 0.03f);

        // Colisão invisível (a madeira não tem colisão própria): caixa no mundo
        static void SolidBox(Transform parent, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = new GameObject("Caixa").transform;
            box.SetParent(parent, false);
            box.SetPositionAndRotation(center, rotation);
            box.gameObject.AddComponent<BoxCollider>().size = size;
        }

        // Rampa invisível de 'a' até 'b' (no mundo), rente ao topo
        static void SolidRamp(Transform parent, Vector3 a, Vector3 b, float width)
        {
            Quaternion rotation = Quaternion.LookRotation(b - a);
            SolidBox(parent, (a + b) * 0.5f + rotation * Vector3.down * 0.1f, rotation, new Vector3(width, 0.2f, (b - a).magnitude));
        }

        // Escada de madeira de 'a' até 'b' (locais, no pé e no alto): duas longarinas e degraus, com a rampa invisível
        static void WoodStair(Transform colliders, Vector3 a, Vector3 b, float width)
        {
            Vector3 low = B(a.x, a.y, a.z), high = B(b.x, b.y, b.z);
            Vector3 flat = high - low;
            flat.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, flat).normalized;
            foreach (float s in new[] { -1f, 1f })
                Beam(low + side * s * (width * 0.5f - 0.08f) + Vector3.down * 0.1f, high + side * s * (width * 0.5f - 0.08f) + Vector3.down * 0.1f,
                    0.14f, 0.26f);
            int steps = Mathf.Max(2, Mathf.RoundToInt((high.y - low.y) / 0.28f));
            Quaternion facing = Quaternion.LookRotation(flat);
            for (int i = 0; i < steps; i++)
            {
                Vector3 c = Vector3.Lerp(low, high, (i + 0.5f) / steps);
                BlockMesh.ChamferBox(s_Wood, c, new Vector3(width - 0.1f, 0.07f, flat.magnitude / steps * 0.95f), facing, 0.02f);
            }
            SolidRamp(colliders, low, high, width);
        }

        // Forja abandonada no oeste do pátio: forno de pedra com a boca escura, coifa e chaminé partida (um bloco caído),
        // bancada de pedra na boca, bigorna de ferro num toco, foles de madeira com o bico entrando no forno, pórtico de
        // madeira com correntes e ganchos, tina de têmpera com a água congelada, lingotes num estrado e bancada de
        // ferramentas
        static void BuildForge()
        {
            const float fx = 47f, fz = -30f;
            BBox(s_Ruins.Pick(), fx, 1.3f, fz, new Vector3(3.2f, 2.6f, 3.6f), 0f, 0.12f);
            BBox(s_Ruins.Pick(), fx - 0.1f, 2.8f, fz, new Vector3(3.6f, 0.4f, 4f), 0f, 0.08f);
            BBox(s_Ruins.Pick(), fx - 2.2f, 0.45f, fz, new Vector3(1.2f, 0.9f, 3f), 0f, 0.06f);
            BBox(s_Ruins.Pick(), fx - 1.7f, 2.3f, fz, new Vector3(0.35f, 0.35f, 2.1f), 0f, 0.04f);
            Vector3 mouth = B(fx - 1.62f, 0f, fz), along = BDir(0f, 0f, 1f);
            s_Ruins.Dark.AddQuadAuto(mouth + along * 0.75f + Vector3.up * 1f, mouth - along * 0.75f + Vector3.up * 1f,
                mouth - along * 0.75f + Vector3.up * 2.1f, mouth + along * 0.75f + Vector3.up * 2.1f, BDir(-1f, 0f, 0f), 4f);
            for (int c = 0; c < 3; c++)
                BBox(s_Ruins.Pick(), fx + 0.6f, 3.65f + c * 1.3f, fz, new Vector3(1.6f - c * 0.1f, 1.3f, 1.6f - c * 0.1f), c == 2 ? R(-8f, 8f) : 0f,
                    0.06f);
            BBox(s_Ruins.Pick(), fx + 2.8f, 0.4f, fz + 3.6f, new Vector3(1.4f, 0.8f, 1.3f), 25f, 0.06f);

            // Bigorna
            BBox(s_Ruins.Pick(), 41.5f, 0.35f, -31.5f, new Vector3(0.8f, 0.7f, 0.8f), 0f, 0.05f);
            BlockMesh.ChamferBox(s_Iron, B(41.5f, 0.83f, -31.5f), new Vector3(0.5f, 0.26f, 0.32f), BRot(), 0.04f);
            BlockMesh.ChamferBox(s_Iron, B(41.5f, 1.11f, -31.5f), new Vector3(1f, 0.3f, 0.42f), BRot(), 0.05f);
            Spike(s_Iron, B(41f, 1.13f, -31.5f), BDir(-1f, 0f, 0f), 0.55f, 0.15f, 6, 0f, 3f, true);

            // Foles: toco de pedra, duas tábuas (a de cima aberta) e o bico de ferro até o forno
            BBox(s_Ruins.Pick(), fx - 0.4f, 0.25f, fz + 2.6f, new Vector3(0.7f, 0.5f, 0.7f), 0f, 0.05f);
            WBox(fx - 0.4f, 0.56f, fz + 2.6f, new Vector3(1.8f, 0.12f, 1f));
            BlockMesh.ChamferBox(s_Wood, B(fx - 0.4f, 0.8f, fz + 2.6f), new Vector3(1.8f, 0.1f, 1f), BRot() * Quaternion.Euler(0f, 0f, 12f), 0.03f);
            Bar(B(fx - 0.4f, 0.62f, fz + 2.1f), B(fx - 0.4f, 0.62f, fz + 1.75f), 0.12f, 0.12f);

            // Pórtico de madeira com correntes e ganchos
            foreach (float z in new[] { -26f, -34f })
                WBox(41f, 1.75f, z, new Vector3(0.3f, 3.5f, 0.3f));
            Beam(B(41f, 3.6f, -25.7f), B(41f, 3.6f, -34.3f), 0.3f, 0.3f);
            foreach ((float z, float bottom) in new[] { (-28f, 1.7f), (-32f, 2.3f) })
            {
                ChainLinks(t => Vector3.Lerp(B(41f, 3.42f, z), B(41f, bottom, z), t), 0.4f);
                Bar(B(41f, bottom, z), B(41f, bottom - 0.35f, z), 0.06f, 0.06f);
                Bar(B(41f, bottom - 0.35f, z), B(41.25f, bottom - 0.2f, z), 0.06f, 0.06f);
            }

            // Tina de têmpera com a água congelada
            BBox(s_Ruins.Pick(), 44f, 0.125f, -35.6f, new Vector3(2.6f, 0.25f, 1.1f), 0f, 0.03f);
            foreach (float s in new[] { -1f, 1f })
            {
                BBox(s_Ruins.Pick(), 44f, 0.5f, -35.6f + s * 0.49f, new Vector3(2.6f, 0.75f, 0.12f), 0f, 0.02f);
                BBox(s_Ruins.Pick(), 44f + s * 1.24f, 0.5f, -35.6f, new Vector3(0.12f, 0.75f, 0.86f), 0f, 0.02f);
            }
            BlockMesh.ChamferBox(s_Ice, B(44f, 0.68f, -35.6f), new Vector3(2.36f, 0.06f, 0.86f), BRot(), 0.01f);

            // Lingotes num estrado
            WBox(39.6f, 0.08f, -36.4f, new Vector3(1.4f, 0.16f, 1f));
            for (int layer = 0; layer < 3; layer++)
            for (int i = 0; i < 4 - layer; i++)
                BlockMesh.ChamferBox(s_Iron, B(39.6f + (i - (3 - layer) * 0.5f) * 0.32f, 0.225f + layer * 0.13f, -36.4f),
                    new Vector3(0.28f, 0.13f, 0.62f), BRot(), 0.03f);

            // Bancada de ferramentas (tenaz e martelo em cima)
            WBox(42.8f, 0.92f, -24.6f, new Vector3(2.2f, 0.1f, 0.9f));
            foreach (float sx in new[] { -0.95f, 0.95f })
            foreach (float sz in new[] { -0.33f, 0.33f })
                WBox(42.8f + sx, 0.435f, -24.6f + sz, new Vector3(0.12f, 0.87f, 0.12f));
            Bar(B(42.2f, 0.99f, -24.8f), B(42.9f, 0.99f, -24.5f), 0.05f, 0.05f);
            Bar(B(42.2f, 0.99f, -24.7f), B(42.9f, 0.99f, -24.6f), 0.05f, 0.05f);
            BlockMesh.ChamferBox(s_Iron, B(43.4f, 1.03f, -24.4f), new Vector3(0.26f, 0.12f, 0.12f), BRot(), 0.02f);
            Beam(B(43.4f, 1.01f, -24.45f), B(43.4f, 1.01f, -24.95f), 0.05f, 0.05f);

            Mound(s_Snow, B(fx + 1.9f, 0f, fz - 2.6f), 1f, 0.8f, 0.35f, 8);
            Mound(s_Snow, B(fx + 1.9f, 0f, fz + 1.6f), 0.8f, 0.7f, 0.3f, 8);
        }

        // Barril em pé (dois troncos de cone de madeira e dois aros de ferro) ou deitado
        static void Barrel(float x, float y, float z) => BarrelAt(B(x, y, z));

        static void BarrelAt(Vector3 c)
        {
            Prism(s_Wood, c, 0.42f, 0.36f, 10, c.y + 0.5f, c.y, true);
            Prism(s_Wood, c, 0.36f, 0.42f, 10, c.y + 1f, c.y + 0.5f, true);
            foreach (float h in new[] { 0.18f, 0.82f })
                Prism(s_Iron, c, 0.405f, 0.405f, 10, c.y + h + 0.04f, c.y + h - 0.04f, false);
            Mound(s_Snow, c + Vector3.up * 1f, 0.3f, 0.28f, 0.1f, 8);
        }

        static void LyingBarrel(float x, float y, float z, float yaw)
        {
            Vector3 dir = BDir(1f, 0f, 0f);
            dir = Quaternion.Euler(0f, yaw, 0f) * dir;
            Vector3 start = B(x, y + 0.4f, z) - dir * 0.5f;
            Spike(s_Wood, start, dir, 1f, 0.4f, 10, 0f, 0f, true);
            foreach (float h in new[] { 0.14f, 0.78f })
                Spike(s_Iron, start + dir * h, dir, 0.08f, 0.415f, 10, 0f, 0f);
        }

        // Caixote de madeira com faixas de reforço em dois lados e neve em cima
        static void Crate(float x, float y, float z, float size, float yaw) => CrateAt(B(x, y, z), size, BRot(yaw));

        static void CrateAt(Vector3 bottom, float size, Quaternion rotation)
        {
            Vector3 c = bottom + Vector3.up * size * 0.5f;
            BlockMesh.ChamferBox(s_Wood, c, Vector3.one * size, rotation, 0.03f);
            foreach (float s in new[] { -1f, 1f })
            foreach (float h in new[] { -1f, 1f })
                BlockMesh.ChamferBox(s_Wood, c + rotation * new Vector3(0f, h * (size * 0.5f - 0.09f), s * (size * 0.5f + 0.02f)),
                    new Vector3(size * 1.02f, 0.1f, 0.05f), rotation, 0.01f);
            Mound(s_Snow, c + Vector3.up * size * 0.5f, size * 0.38f, size * 0.32f, 0.12f, 8);
        }

        // Barris, caixotes e uma carroça congelados e cobertos de neve, no pátio e entre os alojamentos (cobertura baixa)
        static void BuildStores()
        {
            float mid = k_TerraceMid;
            // Pátio, perto da muralha da frente
            Barrel(20.8f, 0f, -47.6f);
            Barrel(21.8f, 0f, -48.7f);
            LyingBarrel(23.6f, 0f, -46.6f, 20f);
            Crate(23f, 0f, -49.3f, 1.1f, 8f);
            Crate(23.1f, 1.1f, -49.2f, 0.85f, 30f);
            // Pátio, ao lado do portão
            Crate(4.5f, 0f, -40f, 1.2f, -10f);
            Crate(5.8f, 0f, -38.6f, 1f, 15f);
            Crate(4.6f, 1.2f, -40.1f, 0.9f, 5f);
            Barrel(7.6f, 0f, -40.8f);
            LyingBarrel(7.2f, 0f, -38.6f, -60f);
            // Alojamentos (terraço do meio)
            Barrel(-21.2f, mid, -5.6f);
            Barrel(-20.2f, mid, -4.6f);
            Crate(-19f, mid, -6.2f, 1.1f, 12f);
            Crate(-12.6f, mid, 9.4f, 1.2f, -5f);
            Crate(-11.2f, mid, 10.4f, 0.9f, 25f);
            LyingBarrel(-12.2f, mid, 11.6f, 80f);
            // Terraço do meio, lado oeste
            Crate(23f, mid, 5f, 1.2f, 4f);
            Crate(24.3f, mid, 6f, 1.1f, -12f);
            Crate(23.2f, mid + 1.2f, 5.1f, 0.9f, 18f);
            Barrel(25.8f, mid, 4.2f);
            Barrel(26.4f, mid, 5.4f);
            // Caixote quebrado: tábuas soltas no chão
            for (int k = 0; k < 5; k++)
                WBox(22.4f + R(-0.8f, 0.8f), mid + 0.05f + k * 0.07f, 7.8f + R(-0.6f, 0.6f), new Vector3(1.1f, 0.06f, 0.22f), R(0f, 180f));
            // Tábuas empilhadas ao pé do andaime
            for (int k = 0; k < 6; k++)
                WBox(-16f + R(-0.2f, 0.2f), 0.06f + k * 0.1f, -20f, new Vector3(0.28f, 0.08f, 3.4f), R(-6f, 6f));

            // Carroça: uma roda partida, o canto dela apoiado no eixo; dois barris deitados na caçamba e o varal no chão
            const float cx = -39f, cz = -27f;
            Vector3 hubUp = B(cx + 0.85f, 0.62f, cz), hubDown = B(cx - 0.85f, 0.2f, cz);
            float roll = Mathf.Atan2(0.42f, 1.7f) * Mathf.Rad2Deg;
            Quaternion bed = BRot() * Quaternion.Euler(0f, 0f, roll);
            Vector3 bedCenter = (hubUp + hubDown) * 0.5f + bed * Vector3.up * 0.3f;
            BlockMesh.ChamferBox(s_Wood, bedCenter, new Vector3(1.5f, 0.12f, 2.6f), bed, 0.02f);
            foreach (float s in new[] { -1f, 1f })
                BlockMesh.ChamferBox(s_Wood, bedCenter + bed * new Vector3(s * 0.72f, 0.25f, 0f), new Vector3(0.07f, 0.45f, 2.6f), bed, 0.01f);
            BlockMesh.ChamferBox(s_Wood, bedCenter + bed * new Vector3(0f, 0.25f, 1.27f), new Vector3(1.5f, 0.45f, 0.07f), bed, 0.01f);
            Bar(hubUp, hubDown, 0.1f, 0.1f);
            Spike(s_Wood, hubUp + BDir(1f, 0f, 0f) * 0.02f, BDir(1f, 0f, 0f), 0.14f, 0.62f, 12, 0f, 0f, true);
            Spike(s_Wood, B(cx - 1.9f, 0f, cz + 0.8f), Vector3.up, 0.14f, 0.62f, 12, 0f, 0f, true);      // a roda partida no chão
            foreach (float s in new[] { -1f, 1f })
                Beam(bedCenter + bed * new Vector3(s * 0.55f, -0.05f, -1.3f), B(cx + s * 0.55f, 0.08f, cz - 3.4f), 0.1f, 0.12f);
            foreach (float s in new[] { -0.6f, 0.6f })
                Spike(s_Wood, bedCenter + bed * new Vector3(-0.6f, 0.45f, s), bed * Vector3.right, 1.2f, 0.36f, 10, 0f, 0f, true);
            Mound(s_Snow, bedCenter + Vector3.up * 0.3f, 0.7f, 1f, 0.2f, 8);
        }

        // Andaime de madeira encostado no muro do terraço do meio, perto da escadaria: escada do pátio até o primeiro
        // piso (na altura do terraço, que se alcança pelo vão do parapeito), uma escada encostada no muro até o
        // segundo piso (3 m acima do terraço, ponto alto sobre o pátio), postes, travessas em X, guarda-corpo e
        // tábuas faltando; colisão invisível nos pisos, nas escadas e no guarda-corpo
        static void BuildScaffold()
        {
            var colliders = new GameObject("Andaime_Colisao").transform;
            colliders.SetParent(s_World, false);
            const float x0 = -13f, x1 = -3f, zf = -18f, zb = -12.1f, deck1 = k_TerraceMid, deck2 = k_TerraceMid + 3.1f, x2 = -9f;

            // Postes (os do lado do segundo piso sobem até o guarda-corpo dele)
            foreach (float x in new[] { -12.6f, -8.6f, -3.4f })
            foreach (float z in new[] { zf + 0.3f, zb - 0.3f })
            {
                float top = x < x2 + 0.5f ? deck2 + 1.1f : deck1 + 1.1f;
                WBox(x, top * 0.5f, z, new Vector3(0.26f, top, 0.26f));
            }

            // Travessas em X na frente e no lado, embaixo do primeiro piso e entre os pisos
            foreach ((float a, float b) in new[] { (-12.6f, -8.6f), (-8.6f, -3.4f) })
            {
                Beam(B(a, 0.3f, zf + 0.15f), B(b, deck1 - 0.3f, zf + 0.15f), 0.1f, 0.18f);
                Beam(B(b, 0.3f, zf + 0.15f), B(a, deck1 - 0.3f, zf + 0.15f), 0.1f, 0.18f);
            }
            Beam(B(-12.75f, 0.3f, zf + 0.3f), B(-12.75f, deck1 - 0.3f, zb - 0.3f), 0.1f, 0.18f);
            Beam(B(-12.75f, deck1 + 0.2f, zb - 0.3f), B(-12.75f, deck2 - 0.3f, zf + 0.3f), 0.1f, 0.18f);
            Beam(B(-12.6f, deck1 + 0.2f, zf + 0.15f), B(-8.6f, deck2 - 0.3f, zf + 0.15f), 0.1f, 0.18f);

            // Pisos de tábuas (uma ou outra faltando) sobre vigas
            foreach ((float from, float to, float y) in new[] { (x0, x1, deck1), (x0, x2, deck2) })
            {
                foreach (float z in new[] { zf + 0.3f, zb - 0.3f })
                    Beam(B(from, y - 0.22f, z), B(to, y - 0.22f, z), 0.18f, 0.24f);
                for (float z = zf + 0.16f; z < zb; z += 0.34f)
                    if (s_Random.NextDouble() > 0.08)
                        WBox((from + to) * 0.5f + R(-0.08f, 0.08f), y - 0.05f, z, new Vector3(to - from + R(-0.2f, 0.1f), 0.08f, 0.3f));
                SolidBox(colliders, B((from + to) * 0.5f, y - 0.1f, (zf + zb) * 0.5f), BRot(), new Vector3(to - from, 0.2f, zb - zf));
            }

            // Guarda-corpo do segundo piso (frente e lado de fora; um trecho da frente quebrado)
            Beam(B(x0 + 0.1f, deck2 + 1f, zf + 0.15f), B(-10.8f, deck2 + 1f, zf + 0.15f), 0.1f, 0.14f);
            Beam(B(-10.8f, deck2 + 1f, zf + 0.15f), B(-10.2f, deck2 + 0.2f, zf - 0.1f), 0.1f, 0.14f);
            Beam(B(x0 + 0.15f, deck2 + 1f, zf + 0.3f), B(x0 + 0.15f, deck2 + 1f, zb - 0.3f), 0.1f, 0.14f);
            Beam(B(x0 + 0.15f, deck2 + 0.5f, zf + 0.3f), B(x0 + 0.15f, deck2 + 0.5f, zb - 0.3f), 0.1f, 0.14f);
            SolidBox(colliders, B(x0 + 0.15f, deck2 + 0.55f, (zf + zb) * 0.5f), BRot(), new Vector3(0.15f, 1.1f, zb - zf));
            SolidBox(colliders, B((x0 + x2) * 0.5f, deck2 + 0.55f, zf + 0.15f), BRot(), new Vector3(x2 - x0, 1.1f, 0.15f));

            // Escadas: do pátio até o primeiro piso (de frente) e do primeiro ao segundo (encostada no muro)
            WoodStair(colliders, new Vector3(-5.6f, 0f, -26.4f), new Vector3(-5.6f, deck1, zf), 2.2f);
            WoodStair(colliders, new Vector3(-3.6f, deck1, zb - 1.45f), new Vector3(x2 + 0.1f, deck2, zb - 1.45f), 1.5f);

            Mound(s_Snow, B(-11f, deck2, -16f), 0.9f, 0.7f, 0.18f, 8);
            Mound(s_Snow, B(-5f, deck1, -16.6f), 0.8f, 0.6f, 0.15f, 8);
        }

        // Passarela de madeira do baluarte leste ao terraço do meio, por cima do pátio: tabuado sobre longarinas,
        // cavaletes a cada 4 m com travessas em X, guarda-corpo com falhas e um trecho de ~2,2 m quebrado no meio
        // (pula-se ou usa-se o dash), com as tábuas das pontas penduradas e o cavalete de lá caído no chão
        static void BuildWalkway()
        {
            var colliders = new GameObject("Passarela_Colisao").transform;
            colliders.SetParent(s_World, false);
            const float x = -46f, half = 1.1f, zStart = -41.5f, zEnd = -11.5f, yStart = 3f, yEnd = k_TerraceMid;
            const float gapFrom = -27.4f, gapTo = -25.2f;
            float Y(float z) => Mathf.Lerp(yStart, yEnd, Mathf.InverseLerp(zStart, zEnd, z));

            foreach ((float a, float b) in new[] { (zStart, gapFrom), (gapTo, zEnd) })
            {
                foreach (float s in new[] { -1f, 1f })
                    Beam(B(x + s * (half - 0.2f), Y(a) - 0.2f, a), B(x + s * (half - 0.2f), Y(b) - 0.2f, b), 0.16f, 0.24f);
                for (float z = a + 0.16f; z < b - 0.1f; z += 0.33f)
                    if (s_Random.NextDouble() > 0.07)
                        WBox(x + R(-0.06f, 0.06f), Y(z) - 0.03f, z, new Vector3(half * 2f + R(-0.15f, 0.1f), 0.08f, 0.29f), R(-3f, 3f));
                SolidRamp(colliders, B(x, Y(a) - 0.01f, a), B(x, Y(b) - 0.01f, b), half * 2f);

                // Guarda-corpo: postes a cada 2 m e corrimão, com uma ou outra falha (a colisão segue o trecho todo)
                foreach (float s in new[] { -1f, 1f })
                {
                    for (float z = a + 0.3f; z < b - 0.2f; z += 2f)
                    {
                        WBox(x + s * half, Y(z) + 0.45f, z, new Vector3(0.12f, 1f, 0.12f));
                        if (z + 2f < b - 0.2f && s_Random.NextDouble() > 0.2)
                            Beam(B(x + s * half, Y(z) + 0.9f, z), B(x + s * half, Y(z + 2f) + 0.9f, z + 2f), 0.08f, 0.1f);
                    }
                    Vector3 low = B(x + s * half, Y(a) + 0.5f, a), high = B(x + s * half, Y(b) + 0.5f, b);
                    SolidBox(colliders, (low + high) * 0.5f, Quaternion.LookRotation(high - low), new Vector3(0.12f, 1f, (high - low).magnitude));
                }
            }

            // Cavaletes
            for (float z = -36.5f; z < zEnd - 3f; z += 4f)
            {
                float top = Y(z) - 0.32f;
                foreach (float s in new[] { -1f, 1f })
                    WBox(x + s * (half - 0.2f), top * 0.5f, z, new Vector3(0.24f, top, 0.24f));
                WBox(x, top - 0.1f, z, new Vector3(half * 2f + 0.3f, 0.2f, 0.26f));
                Beam(B(x - half + 0.2f, 0.3f, z), B(x + half - 0.2f, top - 0.3f, z), 0.08f, 0.14f);
                Beam(B(x + half - 0.2f, 0.3f, z), B(x - half + 0.2f, top - 0.3f, z), 0.08f, 0.14f);
            }

            // O vão: tábuas penduradas das duas pontas, uma longarina partida e tábuas e um cavalete velho caídos no chão
            foreach ((float edge, float dir) in new[] { (gapFrom, 1f), (gapTo, -1f) })
                for (int k = 0; k < 3; k++)
                {
                    float sx = x + R(-0.7f, 0.7f);
                    Beam(B(sx, Y(edge) - 0.05f, edge - dir * 0.1f), B(sx + R(-0.3f, 0.3f), Y(edge) - R(0.9f, 1.6f), edge + dir * R(0.2f, 0.6f)),
                        0.28f, 0.07f);
                }
            Beam(B(x + 0.9f, Y(gapFrom) - 0.2f, gapFrom), B(x + 0.9f, Y(gapFrom) - 1.4f, gapFrom + 0.8f), 0.16f, 0.24f);
            WBox(x + 1.6f, 0.13f, -26.2f, new Vector3(0.24f, 0.24f, Y(-26f) - 0.3f), 70f);
            WBox(x - 1.2f, 0.12f, -25.4f, new Vector3(half * 2f + 0.3f, 0.2f, 0.26f), 20f);
            for (int k = 0; k < 4; k++)
                WBox(x + R(-1.8f, 1.8f), 0.05f + k * 0.07f, -26.3f + R(-1f, 1f), new Vector3(2f, 0.06f, 0.28f), R(0f, 180f));

            Mound(s_Snow, B(x, Y(-34f), -34f), 0.7f, 1f, 0.12f, 8);
            Mound(s_Snow, B(x, Y(-18f), -18f), 0.6f, 0.9f, 0.1f, 8);
        }

        // Portal quebrado no fundo do terraço de cima (era a máquina de anéis): plinto, dois montantes de cantaria (o da
        // direita partido, os quatro blocos de cima caídos em volta), a viga do alto partida (a metade caída na frente),
        // o anel de pedra de 7 m em pé num berço com um trecho faltando (caído deitado mais à frente), engrenagens,
        // correntes e degraus até a boca do anel. Os pedaços que voltam ao lugar, o anel, as engrenagens, as runas e a
        // superfície de luz são objetos próprios ligados ao BrokenPortal, que reconstrói tudo quando a sala é concluída
        static void BuildRingMachine()
        {
            const float mx = 4f, mz = 46f;
            float y = k_TerraceHigh;
            var pieces = new List<BrokenPortal.Piece>();
            var runes = new List<(float angle, Renderer renderer)>();

            Vector3 center = B(mx, 8.6f, mz) + Vector3.up * y;
            Vector3 normal = BDir(0f, 0f, 1f), zero = BDir(1f, 0f, 0f), up = Vector3.Cross(normal, zero);
            var root = new GameObject("Portal_Quebrado");
            root.transform.SetParent(s_World, false);
            root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(normal, up));
            var portal = root.AddComponent<BrokenPortal>();
            var gate = root.AddComponent<BoxCollider>();
            gate.isTrigger = true;
            gate.size = new Vector3(12.2f, 12.2f, 1.4f);
            s_BastionPortal = portal;

            BBox(s_Ruins.Pick(), mx, y + 0.5f, mz, new Vector3(26f, 1f, 5f), 0f, 0.15f);
            // Degraus até a boca do anel (2,2 m acima do terraço)
            for (int k = 0; k < 4; k++)
            {
                float front = 40f + k * 1.1f, h = 0.55f * (k + 1);
                BBox(s_Ruins.Pick(), mx, y + h * 0.5f, (front + 44.6f) * 0.5f, new Vector3(6f - k * 0.3f, h, 44.6f - front), 0f, 0.06f);
            }

            // Montantes: o da esquerda inteiro (18 m); o da direita partido em 10 m, com os quatro blocos de cima caídos
            for (int c = 0; c < 9; c++)
                BBox(s_Ruins.Pick(), mx - 10.5f + R(-0.05f, 0.05f), y + 1f + c * 2f + 1f, mz, CourseSize(c), 0f, 0.1f);
            for (int c = 0; c < 5; c++)
                BBox(s_Ruins.Pick(), mx + 10.5f + R(-0.05f, 0.05f), y + 1f + c * 2f + 1f, mz, CourseSize(c), 0f, 0.1f);
            (float x, float h, float z, float yaw, Vector3 tilt)[] fallen =
            {
                (mx + 11.7f, 12.1f, mz + 0.4f, 25f, new Vector3(0f, 0f, -18f)),   // tombado em cima do toco
                (mx + 13.6f, 1f, mz - 2.6f, 40f, new Vector3(8f, 0f, 0f)),
                (mx + 17.5f, 1f, mz + 1.5f, 70f, new Vector3(0f, 0f, 12f)),
                (mx + 11.5f, 1f, mz - 5f, -20f, new Vector3(-10f, 0f, 0f)),
            };
            for (int i = 0; i < fallen.Length; i++)
            {
                int c = 5 + i;
                var block = new RuinMesh(s_Random.Next());
                BlockMesh.ChamferBox(block.Pick(), Vector3.zero, CourseSize(c), Quaternion.identity, 0.1f);
                Transform t = MovingPiece($"Portal_Bloco{i + 1}", block, true, root.transform);
                t.SetPositionAndRotation(B(fallen[i].x, y + fallen[i].h, fallen[i].z), BRot(fallen[i].yaw) * Quaternion.Euler(fallen[i].tilt));
                pieces.Add(new BrokenPortal.Piece
                {
                    Target = t, FixedPosition = B(mx + 10.5f, y + 1f + c * 2f + 1f, mz), FixedRotation = BRot(), Delay = i * 0.3f,
                });
            }

            // Viga do alto: o toco preso no montante inteiro e a outra metade caída na frente
            BBox(s_Ruins.Pick(), mx - 10.5f + 4.5f, y + 19f + 0.9f, mz, new Vector3(12f, 1.8f, 3.4f), 0f, 0.1f);
            var beam = new RuinMesh(s_Random.Next());
            BlockMesh.ChamferBox(beam.Pick(), Vector3.zero, new Vector3(12f, 1.8f, 3.4f), Quaternion.identity, 0.1f);
            Transform half = MovingPiece("Portal_Viga", beam, true, root.transform);
            half.SetPositionAndRotation(B(mx + 9f, y + 0.9f, mz - 8f), BRot(12f) * Quaternion.Euler(0f, 0f, 4f));
            pieces.Add(new BrokenPortal.Piece { Target = half, FixedPosition = B(mx + 6f, y + 19.9f, mz), FixedRotation = BRot(), Delay = 1.3f });

            // O anel em pé (raio 7) no berço, sem o trecho de 100° a 170° (de 'zero' para cima); gira quando o portal abre
            var ringMesh = new RuinMesh(s_Random.Next());
            ArquipelagoRuinKit.RichRing(ringMesh, Vector3.zero, Vector3.forward, Vector3.right, 7f, 1.1f, 2.4f, 170f, 460f, s_Random,
                true, true, 1e4f, 3);
            Transform ring = CreateRuin("Portal_Anel", ringMesh, true, snow: false);
            ring.SetParent(root.transform, false);
            Dynamic(ring);
            foreach (float s in new[] { -1f, 1f })
                BBox(s_Ruins.Pick(), mx + s * 2.4f, y + 1.6f, mz, new Vector3(2.4f, 1.2f, 3.2f), s * 18f, 0.08f);

            // O trecho que falta, deitado mais à frente
            var arc = new RuinMesh(s_Random.Next());
            ArquipelagoRuinKit.RichRing(arc, Vector3.zero, Vector3.forward, Vector3.right, 7f, 1.1f, 2.4f, 0f, 70f, s_Random, true, true,
                1e4f, 3);
            Transform piece = MovingPiece("Portal_Trecho", arc, false, root.transform);
            Vector3 lyingZero = BDir(0f, 0f, 1f);
            piece.SetPositionAndRotation(B(-5f, y + 1.2f, 33f), Quaternion.LookRotation(Vector3.up, Vector3.Cross(Vector3.up, lyingZero)));
            float a100 = 100f * Mathf.Deg2Rad;
            Vector3 d100 = Mathf.Cos(a100) * zero + Mathf.Sin(a100) * up;
            pieces.Add(new BrokenPortal.Piece
            {
                Target = piece, FixedPosition = center, FixedRotation = Quaternion.LookRotation(normal, Vector3.Cross(normal, d100)),
                Delay = 1.9f, AttachTo = ring,
            });

            // Runas na face do anel virada para o forte (o -z do anel); as do trecho caído vão com ele
            for (float deg = 175f; deg < 460f; deg += 19f)
                runes.Add((deg, RunePlate(ring, deg, runes.Count)));
            foreach (float deg in new[] { 12f, 35f, 58f })
                runes.Add((deg + 100f, RunePlate(piece, deg, runes.Count)));
            // Acendem de baixo (270°) para cima pelos dois lados; a do nicho por último
            runes.Sort((p, q) => Mathf.Abs(Mathf.DeltaAngle(p.angle, 270f)).CompareTo(Mathf.Abs(Mathf.DeltaAngle(q.angle, 270f))));
            var order = new List<Renderer>();
            foreach (var rune in runes)
                order.Add(rune.renderer);
            if (s_NicheRune)
                order.Add(s_NicheRune);

            // Engrenagens no lado de fora do montante inteiro (giram quando o portal abre)
            var gears = new List<Transform>();
            Vector3 axis = BDir(-1f, 0f, 0f);
            foreach ((float gy, float radius, float x) in new[] { (8.6f, 2.2f, mx - 12.4f), (5.4f, 1.3f, mx - 12.4f) })
            {
                Vector3 hub = B(x, y + gy, mz);
                var gear = new MeshData();
                Spike(gear, -axis * 0.2f, axis, 0.4f, radius, 16, 0f, 0f, true);
                int teeth = Mathf.RoundToInt(radius * 8f);
                for (int k = 0; k < teeth; k++)
                {
                    float t = k / (float)teeth * Mathf.PI * 2f;
                    Vector3 dir = Mathf.Cos(t) * Vector3.up + Mathf.Sin(t) * BDir(0f, 0f, 1f);
                    BlockMesh.ChamferBox(gear, dir * (radius + 0.12f), new Vector3(0.4f, 0.3f, 0.26f), Quaternion.LookRotation(axis, dir), 0.03f);
                }

                GameObject go = GridSceneKit.CreateObject($"{k_Prefix}_Portal_Engrenagem{gears.Count + 1}", "Portal_Engrenagem", gear,
                    s_Materials.Iron, root.transform, false);
                go.transform.SetPositionAndRotation(hub, Quaternion.identity);
                Dynamic(go.transform);
                gears.Add(go.transform);
                Bar(hub, B(mx - 10.5f + 1.4f, y + gy, mz), 0.35f, 0.35f);
            }

            // Correntes penduradas do toco da viga, na frente do anel
            foreach ((float x, float bottom) in new[] { (mx - 6f, 11f), (mx - 2.5f, 13.5f) })
            {
                ChainLinks(t => Vector3.Lerp(B(x, y + 19f, mz - 1.5f), B(x, y + bottom, mz - 1.5f), t), 0.5f);
                Bar(B(x, y + bottom, mz - 1.5f), B(x, y + bottom - 0.6f, mz - 1.5f), 0.1f, 0.1f);
            }

            // Superfície de luz (cresce ao abrir) e a luz
            GameObject surface = GridSceneKit.CreateObject(k_Prefix + "_Portal_Quebrado_Superficie", "Portal_Superficie", PortalDisc(6.1f),
                s_Materials.Portal, root.transform, false, false, false);
            Dynamic(surface.transform);
            var spin = surface.AddComponent<SlowRotation>();
            spin.Axis = normal;
            spin.DegreesPerSecond = 10f;
            spin.Bob = 0f;
            var glow = new GameObject("Luz_Portal").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -2.5f);
            glow.type = LightType.Point;
            glow.color = new Color(0.35f, 1f, 0.95f);
            glow.range = 30f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            Vector3 zone0 = B(-44f, y, 16f), zone1 = B(40f, y + 6f, 56f);
            portal.Pieces = pieces.ToArray();
            portal.Ring = ring;
            portal.RingAxis = normal;
            portal.RingSpeed = 7f;
            portal.Gears = gears.ToArray();
            portal.GearSpeeds = new[] { 20f, -34f };
            portal.GearAxis = axis;
            portal.Runes = order.ToArray();
            portal.Surface = surface.transform;
            portal.Glow = glow;
            portal.OpenZone = new Bounds((zone0 + zone1) * 0.5f, new Vector3(Mathf.Abs(zone1.x - zone0.x), zone1.y - zone0.y,
                Mathf.Abs(zone1.z - zone0.z)));
        }

        static Vector3 CourseSize(int course) => new Vector3(3f - course * 0.05f, 2f, 3f - course * 0.05f);

        // Peça de pedra que se move (o portal reconstrói): objeto próprio, fora da junção estática
        static Transform MovingPiece(string name, RuinMesh mesh, bool snow, Transform parent)
        {
            Transform t = CreateRuin(name, mesh, true, snow);
            t.SetParent(parent, true);
            Dynamic(t);
            return t;
        }

        // Tira as marcas de estático (a junção estática prenderia o objeto no lugar)
        static void Dynamic(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        // Placa de runa na face do anel virada para o forte (o -z do anel), no ângulo 'deg' a partir de 'zero'
        static Renderer RunePlate(Transform parent, float deg, int index)
        {
            float t = deg * Mathf.Deg2Rad;
            var plate = new MeshData();
            BlockMesh.Box(plate, new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * 7f + Vector3.back * 1.24f, new Vector3(0.5f, 0.75f, 0.04f),
                Quaternion.Euler(0f, 0f, deg - 90f));
            GameObject go = GridSceneKit.CreateObject($"{k_Prefix}_Portal_Runa{index + 1}", "Runa", plate, s_Materials.Rune, parent, false,
                false, false);
            Dynamic(go.transform);
            return go.GetComponent<Renderer>();
        }

        // Disco da superfície acesa de um portal (plano xy, dos dois lados), centrado na origem
        static MeshData PortalDisc(float radius)
        {
            var disc = new MeshData();
            const int sides = 40;
            float size = radius * 2f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                Tri(disc, Vector3.zero, p0, p1, new Vector2(0.5f, 0.5f), new Vector2(0.5f + p0.x / size, 0.5f + p0.y / size),
                    new Vector2(0.5f + p1.x / size, 0.5f + p1.y / size), Vector3.back);
                Tri(disc, Vector3.zero, p1, p0, new Vector2(0.5f, 0.5f), new Vector2(0.5f + p1.x / size, 0.5f + p1.y / size),
                    new Vector2(0.5f + p0.x / size, 0.5f + p0.y / size), Vector3.forward);
            }

            return disc;
        }

        // A montanha do Blender (Ferramentas/Picos/montanha_bastiao.py) atrás, embaixo e dos lados do forte, com os
        // paredões da fenda; nas coordenadas locais do forte, sem girar. Rocha com colisão e o material das montanhas,
        // gelo (cascata e pingentes) sem colisão. Sem o FBX, volta aos pilares do gerador.
        static bool PlaceBastionMountain()
        {
            string path = $"{IcaroAssetUtility.Root}/Models/Picos/Montanha_Bastiao.fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model)
            {
                Debug.LogWarning($"[Ícaro] Picos: falta {path} (rode Ferramentas/Picos/montanha_bastiao.py no Blender); o bastião " +
                                 "usa os pilares do gerador.");
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, s_World);
            go.name = "Montanha_Bastiao";
            go.transform.SetPositionAndRotation(k_Bastion, model.transform.rotation);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                bool ice = renderer.name.EndsWith("_Gelo");
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = ice ? s_Materials.Ice : s_Materials.Mountain;
                renderer.sharedMaterials = materials;
                if (!ice)
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }

            PicosMountainKit.Pillars.Add(new PicosMountainKit.Placed
            {
                Top = B(0f, 80f, 130f), TopRadius = 110f, BaseRadius = 150f, Taper = 1f,
            });
            Clear(B(0f, 0f, 90f), 190f);
            Physics.SyncTransforms();
            BuildNicheMonolith();
            return true;
        }

        // Monólito de runas apagadas no nicho da parede de trás (acende por último quando o portal abre), assentado no chão
        // do nicho que a montanha tem (x -36, 30 m acima do pátio)
        static void BuildNicheMonolith()
        {
            const float x = -36f, floor = 30f;
            if (!Physics.Raycast(B(x, floor + 9f, 59f), Vector3.down, out RaycastHit ground, 14f) ||
                Mathf.Abs(ground.point.y - B(0f, floor, 0f).y) > 1f)
            {
                Debug.LogWarning("[Ícaro] Bastião: o nicho do monólito não foi achado na montanha; o monólito ficou de fora.");
                return;
            }

            float back = 64f;
            if (Physics.Raycast(B(x, floor + 3f, 52f), BDir(0f, 0f, 1f), out RaycastHit wall, 14f))
                back = -(wall.point.z - k_Bastion.z);
            float z = back - 1.1f;
            BBox(s_Ruins.Pick(), x, floor + 3f, z, new Vector3(2.4f, 6f, 1.6f), 0f, 0.1f);
            BBox(s_Ruins.Pick(), x, floor + 0.3f, z, new Vector3(3.2f, 0.6f, 2.4f), 0f, 0.08f);
            var plate = new MeshData();
            BlockMesh.Box(plate, B(x, floor + 3.3f, z - 0.82f), new Vector3(1.6f, 4f, 0.04f), BRot());
            GameObject go = GridSceneKit.CreateObject(k_Prefix + "_Portal_RunaNicho", "Runa_Nicho", plate, s_Materials.Rune, s_World,
                false, false, false);
            Dynamic(go.transform);
            s_NicheRune = go.GetComponent<Renderer>();
        }

        // Viga de madeira de 'a' até 'b' (seção w x h)
        static void Beam(Vector3 a, Vector3 b, float w, float h)
        {
            Vector3 along = b - a;
            BlockMesh.ChamferBox(s_Wood, (a + b) * 0.5f, new Vector3(w, h, along.magnitude), Quaternion.LookRotation(along), 0.03f);
        }

        // Sala 19: gatilho cobrindo os três terraços, começando 5 m depois do portão
        static void BuildBastionRoom()
        {
            float north = k_BastionFront + 5f;
            Vector3 corner0 = B(-k_BastionHalfWidth, 0f, north), corner1 = B(k_BastionHalfWidth, 0f, k_BastionBack + 8f);
            var trigger = new Bounds((corner0 + corner1) * 0.5f + Vector3.up * 14f,
                new Vector3(Mathf.Abs(corner1.x - corner0.x), 44f, Mathf.Abs(corner1.z - corner0.z)));
            RoomManager room = BuildRoom("Arena_BastiaoEmTerracos", "Bastião em Terraços", 19, k_Bastion, trigger,
                B(-10f, 0f, k_BastionFront), k_Bastion.y - 1f, k_Bastion.y + k_TerraceHigh + 1f);
            if (s_BastionPortal)
                s_BastionPortal.Room = room;
        }

        // ---------- longe ----------

        // Dezenas de montanhas nevadas saindo das nuvens em volta (fora do caminho), de seis formatos e tamanhos bem
        // diferentes (colunas, torres de topo plano, grupos de colunas, coroas de blocos, paredões e topos partidos), algumas
        // colossais ao longe; uma ilha flutuante sobre um anel quebrado a oeste
        static void BuildDistant()
        {
            DistantField(k_MapCenter, 230, 160, 1600, "Macicos");

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

        // 'count' montanhas em volta de 'center' (as primeiras até 1100 m, as de 'backgroundFrom' em diante no anel do
        // fundo, de 1100 a 1700 m), fora das áreas do caminho (s_Clearings)
        static void DistantField(Vector2 center, int count, int backgroundFrom, int attempts, string rootName)
        {
            var kinds = new[]
            {
                PicosMountainKit.Kind.Column, PicosMountainKit.Kind.Column, PicosMountainKit.Kind.Cluster,
                PicosMountainKit.Kind.Cluster, PicosMountainKit.Kind.Cluster, PicosMountainKit.Kind.Tower,
                PicosMountainKit.Kind.Crown, PicosMountainKit.Kind.Ridge, PicosMountainKit.Kind.Broken,
                PicosMountainKit.Kind.Broken,
            };
            // Em volta do caminho todo (de 0 a 1100 m do meio dele) e, ao fundo, um anel de montanhas grandes (1100 a
            // 1700 m) que aparecem como silhuetas na névoa. As gigantes e as do fundo são maciços do Blender
            // (Ferramentas/Picos/macicos.py), girados e escalados; sem eles, saem do gerador como as outras.
            GameObject[] massifs = LoadMassifs(""), massifsFar = LoadMassifs("_Longe");
            var massifRoot = new GameObject(rootName).transform;
            massifRoot.SetParent(s_World, false);
            int placed = 0;
            for (int attempt = 0; attempt < attempts && placed < count; attempt++)
            {
                bool background = placed >= backgroundFrom;
                float a = R(0f, Mathf.PI * 2f);
                float d = background ? R(1100f, 1700f) : Mathf.Lerp(0f, 1100f, Mathf.Sqrt(R(0f, 1f)));
                var p = new Vector3(center.x + Mathf.Cos(a) * d, 0f, center.y + Mathf.Sin(a) * d);
                // Longe do caminho (praça, crista, plataformas, pontes e arenas) e da ilha
                bool onPath = false;
                foreach (var clearing in s_Clearings)
                    onPath |= Vector2.Distance(new Vector2(p.x, p.z), clearing.Center) < clearing.Radius;
                if (onPath)
                    continue;
                bool nearHero = false;
                foreach (var hero in k_Heroes)
                    nearHero |= Vector2.Distance(new Vector2(p.x, p.z), new Vector2(hero.Position.x, hero.Position.z)) < hero.Radius + 40f;
                if (nearHero || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(-320f, -380f)) < 120f)
                    continue;

                // Distância até o caminho mais perto (o detalhe e o tamanho crescem com ela)
                float near = float.MaxValue;
                foreach (var clearing in s_Clearings)
                    near = Mathf.Min(near, Vector2.Distance(new Vector2(p.x, p.z), clearing.Center));
                bool giant = background || (near > 500f && s_Random.NextDouble() < 0.2);
                if (giant && massifs.Length > 0)
                {
                    bool far = background || near > 800f;
                    PlaceMassif(far ? massifsFar : massifs, p, background ? R(1.2f, 1.8f) : R(0.8f, 1.4f), massifRoot);
                    placed++;
                    continue;
                }

                float size = giant ? R(40f, 80f) : R(7f, 24f) * (1f + near / 1500f);
                float top = giant ? R(60f, 200f) : R(-60f, 70f) + near * 0.05f;
                PicosMountainKit.Kind kind = kinds[s_Random.Next(kinds.Length)];
                int detail = near < 200f ? 0 : near < 500f ? 1 : 2;
                PicosMountainKit.Mountain(s_MountainFar, kind, new Vector3(p.x, top, p.z), size, s_Random, detail);
                placed++;
            }
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

        // Maciços do Blender (Macico_1..8 e as versões _Longe, mais leves)
        static GameObject[] LoadMassifs(string suffix)
        {
            var list = new List<GameObject>();
            for (int i = 1; i <= 8; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{IcaroAssetUtility.Root}/Models/Picos/Macico_{i}{suffix}.fbx");
                if (model)
                    list.Add(model);
            }

            if (list.Count == 0)
                Debug.LogWarning("[Ícaro] Picos: faltam os maciços (rode Ferramentas/Picos/macicos.py no Blender); as montanhas " +
                                 "grandes saem do gerador.");
            return list.ToArray();
        }

        // Um maciço sorteado no ponto, girado e escalado, um pouco mais alto ou mais baixo, com o material das montanhas;
        // entra na lista dos fiapos de névoa
        static void PlaceMassif(GameObject[] models, Vector3 at, float scale, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(models[s_Random.Next(models.Length)], parent);
            go.transform.SetPositionAndRotation(new Vector3(at.x, R(-30f, 30f), at.z), Quaternion.Euler(0f, R(0f, 360f), 0f));
            go.transform.localScale = Vector3.one * scale;
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = s_Materials.Mountain;
                renderer.sharedMaterials = materials;
            }

            PicosMountainKit.Pillars.Add(new PicosMountainKit.Placed
            {
                Top = go.transform.position + Vector3.up * 60f * scale, TopRadius = 40f * scale, BaseRadius = 60f * scale, Taper = 1f,
            });
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
            cloudSea.FollowCamera = true;   // as Vilas do Alto ficam a 5 km: a caixa vai com a câmera
            cloudSea.enabled = false;       // refaz a caixa com os valores acima
            cloudSea.enabled = true;

            var wisps = new List<Wisp>();
            // Em volta de cada pico, na altura das nuvens (mais nos perto)
            foreach (PicosMountainKit.Placed spire in PicosMountainKit.Pillars)
            {
                var top2 = new Vector2(spire.Top.x, spire.Top.z);
                float distance = Mathf.Min(Vector2.Distance(top2, k_MapCenter), Vector2.Distance(top2, new Vector2(k_High.x, k_High.z)));
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
            WispRing(k_Bastion + Vector3.back * 10f, 70f, k_Bastion.y - 8f, k_Bastion.y - 40f, 40, 9f, 18f, wisps);

            // Soltos sobre o mar, rentes ao topo dos montes
            for (int k = 0; k < 320; k++)
            {
                float a = R(0f, Mathf.PI * 2f), d = Mathf.Lerp(60f, 1500f, Mathf.Sqrt(R(0f, 1f)));
                var p = new Vector3(k_MapCenter.x + Mathf.Cos(a) * d, R(k_CloudY - 6f, k_CloudY + 8f), k_MapCenter.y + Mathf.Sin(a) * d);
                wisps.Add(new Wisp(p, R(30f, 70f), R(0.3f, 0.6f)));
            }

            for (int k = 0; k < 260; k++)
            {
                float a = R(0f, Mathf.PI * 2f), d = Mathf.Lerp(60f, 1500f, Mathf.Sqrt(R(0f, 1f)));
                var p = new Vector3(k_High.x + Mathf.Cos(a) * d, R(k_CloudY - 6f, k_CloudY + 8f), k_High.z + Mathf.Sin(a) * d);
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

        // Calçada antiga de 'a' até 'b': lajes chatas de pedra em duas fileiras desencontradas, tortas, só em trechos
        // (manchas de ruído no mundo: a calçada sobrou aqui e ali, com uma pedra solta ou outra entre os trechos); por
        // baixo, uma passarela invisível lisa na linha do caminho (anda-se sem tropeçar nas pedras)
        static void Path(Vector3 a, Vector3 b, float width)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            Quaternion rotation = Quaternion.LookRotation(along);
            Vector3 right = rotation * Vector3.right;

            var walk = new GameObject("Trilha").transform;
            walk.SetParent(s_PathColliders, false);
            walk.SetPositionAndRotation((a + b) * 0.5f + rotation * Vector3.down * 0.1f, rotation);
            walk.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, length + 0.6f);

            const float step = 1.2f;
            for (float d = 0.3f; d < length - 0.2f; d += step)
            {
                for (int column = 0; column < 2; column++)
                {
                    float t = Mathf.Clamp01((d + (column == 1 ? step * 0.5f : 0f)) / length);
                    Vector3 p = Vector3.Lerp(a, b, t) + right * ((column - 0.5f) * width * 0.42f + R(-0.15f, 0.15f));
                    float patch = Mathf.PerlinNoise(p.x * 0.07f + 31.3f, p.z * 0.07f + 17.9f);
                    if (s_Random.NextDouble() > (patch > 0.55f ? 0.8f : 0.05f))
                        continue;
                    BlockMesh.ChamferBox(s_PathStones.Pick(), p + rotation * Vector3.down * 0.08f,
                        new Vector3(R(1.1f, 1.6f), 0.24f, R(0.9f, 1.2f)), rotation * Quaternion.Euler(R(-2f, 2f), R(-9f, 9f), R(-2f, 2f)),
                        0.08f);
                }
            }
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

        // Piso de uma construção: o bloco inteiro vai para os pisos; o topo é cortado em quadrados de 4 m pela máscara
        // de neve (CreatePaving) e os lados ficam de cantaria
        static void Pave(MeshData slab)
        {
            MeshData target = s_Paving.Tones[0];
            int start = target.Vertices.Count;
            target.Vertices.AddRange(slab.Vertices);
            target.Normals.AddRange(slab.Normals);
            target.Uvs.AddRange(slab.Uvs);
            foreach (int index in slab.Triangles)
                target.Triangles.Add(start + index);
        }

        // Pisos como o musgo do Arquipélago: cada quadrado de 4 m dos topos grandes vira uma das três lajes, um dos
        // cinco passos de transição laje-neve ou neve cheia, pela máscara de neve no meio dele (SnowMask); os topos
        // pequenos ficam de laje e os lados de cantaria
        static void CreatePaving()
        {
            CreateMaskedFloor("Pisos", s_Paving, s_Materials.Pavings, s_Materials.PavingSnow);
            CreateMaskedFloor("Ruas", s_Cobble, s_Materials.Cobble, s_Materials.CobbleSnow);
        }

        static void CreateMaskedFloor(string name, RuinMesh mesh, Material[] pavings, Material[] blends)
        {
            RuinMesh.Layers layers = mesh.SplitMasked(-10000f, 0.7f, SnowMask, pavings.Length, blends.Length + 1, 4f, 6f, 8f, 8f);
            var root = new GameObject(name).transform;
            root.SetParent(s_World, false);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Lados", "Lados", layers.Tones[0], s_Materials.Stones[0], root, true);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Pequenos", "Pequenos", layers.Moss, pavings[0], root, true);
            for (int i = 0; i < layers.Masked.Length; i++)
            {
                Material material = i < pavings.Length ? pavings[i]
                    : i < pavings.Length + blends.Length ? blends[i - pavings.Length] : s_Materials.FloorSnow;
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Laje{i + 1}", $"Laje_{i + 1}", layers.Masked[i], material, root, true);
            }
        }

        // Neve dos pisos (0 = laje limpa, 1 = neve cheia): manchas de ruído no mundo e mais neve perto da beira de cada
        // plataforma (onde o vento acumula); o meio fica mais limpo
        static float SnowMask(Vector3 p)
        {
            float noise = Mathf.PerlinNoise(p.x / 16f + 31f, p.z / 16f + 17f) * 0.7f + Mathf.PerlinNoise(p.x / 5f + 3f, p.z / 5f + 9f) * 0.3f;
            float edge = 0f;
            foreach (var platform in s_Platforms)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(platform.Center.x, platform.Center.z)) / platform.Radius;
                if (d < 1.15f && Mathf.Abs(p.y - platform.Center.y) < 12f)
                    edge = Mathf.Max(edge, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d)));
            }

            return Mathf.Clamp01((noise - 0.42f) * 1.7f + edge * 0.85f);
        }

        // Ruína em objetos por material: três tons de cantaria, a neve nos topos (no lugar do musgo do Arquipélago) e os
        // vãos escuros; sem parte molhada (não há água)
        static Transform CreateRuin(string name, RuinMesh mesh, bool collider, bool snow = true, Material[] materials = null,
            float snowSlope = 0.55f)
        {
            RuinMesh.Layers layers = mesh.Split(-10000f, snow ? snowSlope : 2f);
            materials ??= s_Materials.Stones;
            var root = new GameObject(name).transform;
            root.SetParent(s_World, false);
            for (int i = 0; i < layers.Tones.Length; i++)
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Tom{i + 1}", $"Pedra_{i + 1}", layers.Tones[i],
                    materials[i % materials.Length], root, collider);
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
