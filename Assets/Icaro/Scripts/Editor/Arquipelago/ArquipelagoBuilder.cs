using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;
using NavMeshModifier = Unity.AI.Navigation.NavMeshModifier;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // "Ícaro > Arquipélago > Gerar Arquipélago": o bioma Arquipélago (substitui o Charco) numa cena própria
    // (Scenes/Arquipelago.unity); BuildInto monta o mesmo bioma dentro da cena da Via, depois do desfiladeiro que vem
    // do Bosque (ViaCoastBuilder). Noite verde-azulada com névoa densa e aurora boreal. Ilhas em degraus numa grade de
    // 2 m (topo de capim, penhascos de pedra até o mar, terraços com rampas), ligadas por travessias.
    // - Costa: o continente ocupa a borda sul de ponta a ponta, com um penhasco de 20 m sobre a praia. A Via chega na
    //   praça do alto (paredões dos lados, parapeito, arco de entrada) e desce por uma escadaria em zigue-zague até a
    //   praia; dali, pontes, uma coluna tombada e ilhotas levam à ilha de chegada.
    // Arenas (planejadas: Anel Partido, Farol, Cemitério de Anéis e a Ponte Colossal do chefe; feitas por etapas):
    // - Anel Partido (sala 9): ilha grande partida ao meio por um canal, com um anel colossal em pé sobre ele e
    //   semiafundado. Uma ponte de pedra cruza o canal no meio. Em cada metade, um mirante de runas (pirâmide de três
    //   degraus com rampas em volta e um arco do anel no topo).
    // - Travessia: ponte quebrada (pular ou dash) até uma ilhota e ponte inteira até a Enseada.
    // - Enseada (sala 10): ilha média com uma baía recortada, colunata em volta dela, um arco partido e um paredão de
    //   rocha mais alto com escadaria.
    // - Travessia longa: pedras até uma ilhota, ponte, um anel caído deitado como ponte em curva, pedras e ponte.
    // - Ilha da Cratera (sala 11): ilha em anel com a borda alta (6 m) em volta de uma lagoa funda, escadarias nos
    //   quatro lados e uma ponte de borda a borda sobre a lagoa.
    // - Travessia longa: ponte, um cais comprido, ponte quebrada e a ponte até a praia leste do Farol.
    // - Farol (sala 11): rochedo em três terraços (praia, platô e topo) ligados por escadarias, o farol no topo com
    //   escada em espiral por fora até a varanda (14 m) e o feixe girando (só visual).
    // - Travessia longa: do portão na praia oeste do Farol, ponte quebrada, uma torre tombada, pedras e ponte.
    // - Estaleiro de Anéis (sala 13): pátio de pedra com um anel colossal em pé sobre andaimes na costa oeste
    //   (cenário), guindastes, pilhas de blocos de anel e uma rampa de lançamento mais alta.
    // - Travessia longa: coluna tombada, uma calçada de blocos rente à água, pontes e a ponte até as Ilhotas.
    // - Ilhotas (sala 12): muitas ilhotas baixas ligadas por pontes, pontes quebradas, pedras e colunas tombadas, com
    //   uma ruína de cobertura em cada uma.
    // - Travessia: pontes por duas ilhotas e uma rampa de ponte subindo até o anel de fora do Cemitério.
    // - Cemitério de Anéis (sala 13): três anéis colossais deitados sobre o mar, um dentro do outro (topos a 9, 6 e
    //   3,5 m), descendo até uma ilha central. Entre o de fora e o do meio não há escadas: pedaços de anel levitam
    //   no vão girando em volta do centro (OrbitingPlatform), e atravessa-se pulando neles; os outros vãos têm
    //   escadarias. Em volta, uma moldura colossal em pé girando em volta do próprio eixo, anéis em pé quebrados e
    //   montes de anéis caídos.
    // Todas as escadas têm uma rampa invisível rente às quinas dos degraus (StairRamp): a subida fica lisa.
    // Cobertura de ruínas facetadas nas ilhas; no mar, anéis gigantes quebrados, torres e palácios afundados
    // (silhuetas na névoa). Mar: quem cai volta ao último ponto seguro perdendo vida (SeaHazard); inimigos que caem
    // morrem. Peças sem curvas nem esferas (BlockMesh). Rodar de novo refaz a cena.
    public static class ArquipelagoBuilder
    {
        const string k_ScenePath = IcaroAssetUtility.ScenesFolder + "/Arquipelago.unity";
        const string k_NavMeshFolder = IcaroAssetUtility.ScenesFolder + "/Arquipelago_NavMesh";
        const string k_Prefix = "Arquipelago";

        const float k_Cell = GridSceneKit.Cell;
        const int k_Size = 640;            // células de cada lado do mapa de ilhas
        const int k_CoastEast = 400;       // até onde o continente (e as pedras da costa) vai para o leste
        const int k_SouthSize = 270;       // sorteio de cobertura e spawns (as arenas do norte têm os próprios pontos)
        const int k_WestCoast = 200;       // o continente continua 200 células a oeste do mapa (fora da grade)
        const float k_SeaBed = -4f;        // fundo dos penhascos (abaixo da água)

        class Island
        {
            public Vector2 Center;         // células
            public float RadiusX, RadiusZ;
            public float Height;           // metros acima do mar
            public Vector2 PlateauCenter;
            public float PlateauRadiusX, PlateauRadiusZ;
            public float PlateauHeight;
            public Terrace[] Terraces;     // níveis mais altos, um dentro do outro (o último vale)
            public int Arena;              // 0 = fora de combate; 1 Anel Partido, 2 Enseada, 3 Farol, 4 Ilhotas,
                                           // 5 Cemitério, 6 Cratera, 7 Estaleiro
            public int Seed;
        }

        class Terrace
        {
            public Vector2 Center;
            public float RadiusX, RadiusZ;
            public float Height;
        }

        static readonly Island[] k_Islands =
        {
            // Chegada
            new Island { Center = new Vector2(52, 75), RadiusX = 7, RadiusZ = 6, Height = 2f, Seed = 1 },
            // Anel Partido: metade oeste e metade leste (o canal é aberto depois, em CarveChannel)
            new Island { Center = new Vector2(54, 110), RadiusX = 20, RadiusZ = 16, Height = 3f, Arena = 1,
                PlateauCenter = new Vector2(47, 116), PlateauRadiusX = 6, PlateauRadiusZ = 5, PlateauHeight = 4f, Seed = 2 },
            new Island { Center = new Vector2(90, 110), RadiusX = 20, RadiusZ = 16, Height = 3f, Arena = 1,
                PlateauCenter = new Vector2(98, 104), PlateauRadiusX = 6, PlateauRadiusZ = 6, PlateauHeight = 4f, Seed = 3 },
            // Ilhotas de pedra (cenário)
            new Island { Center = new Vector2(16, 146), RadiusX = 4, RadiusZ = 4, Height = 9f, Seed = 4 },
            new Island { Center = new Vector2(126, 80), RadiusX = 3, RadiusZ = 3, Height = 12f, Seed = 5 },
            new Island { Center = new Vector2(58, 200), RadiusX = 6, RadiusZ = 5, Height = 5f, Seed = 6 },
            // Travessia do Anel Partido ao Farol: ilhota da ponte quebrada e ilhota do primeiro destroço
            new Island { Center = new Vector2(114, 132), RadiusX = 4, RadiusZ = 3.5f, Height = 3f, Seed = 7 },
            new Island { Center = new Vector2(146, 172), RadiusX = 3, RadiusZ = 3, Height = 2f, Seed = 8 },
            // Farol: rochedo em três terraços (praia, platô do meio e topo com o farol)
            new Island
            {
                Center = new Vector2(148, 300), RadiusX = 24, RadiusZ = 19, Height = 2f, Arena = 3, Seed = 9,
                Terraces = new[]
                {
                    new Terrace { Center = new Vector2(149, 303), RadiusX = 16, RadiusZ = 12.5f, Height = 5f },
                    new Terrace { Center = new Vector2(150, 305), RadiusX = 7, RadiusZ = 6, Height = 8f },
                },
            },
            // Enseada: ilha média com um paredão de rocha mais alto e uma baía (terraço no fundo do mar = água)
            new Island
            {
                Center = new Vector2(134, 150), RadiusX = 17, RadiusZ = 13, Height = 2.5f, Arena = 2, Seed = 10,
                Terraces = new[]
                {
                    new Terrace { Center = new Vector2(140, 156), RadiusX = 8, RadiusZ = 6, Height = 5f },
                    new Terrace { Center = new Vector2(149, 147), RadiusX = 5, RadiusZ = 4.5f, Height = k_SeaBed },
                },
            },
            // Continente (a forma vem da linha da costa, em BuildHeights; a elipse fica fora do mapa)
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2f, Seed = 11 },
            // Ilhotas da costa, da praia até a chegada
            new Island { Center = new Vector2(60, 35), RadiusX = 4, RadiusZ = 3.5f, Height = 2.5f, Seed = 12 },
            new Island { Center = new Vector2(46, 46), RadiusX = 4, RadiusZ = 4, Height = 3f, Seed = 13 },
            new Island { Center = new Vector2(59, 54), RadiusX = 3.5f, RadiusZ = 3.5f, Height = 2.5f, Seed = 14 },
            // Cemitério de Anéis: a ilha central (os anéis são ruínas em volta dela), ao norte das Ilhotas
            new Island { Center = new Vector2(160, 553), RadiusX = 11f, RadiusZ = 11f, Height = 2f, Arena = 5, Seed = 15 },
            // (sobras da travessia antiga do Farol às Ilhotas: fora do mapa, mantêm os índices)
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.5f, Seed = 21 },
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.5f, Seed = 22 },
            // Ilhotas: quatro fileiras de sul a norte (a segunda do sul é a entrada; a segunda do norte, a saída)
            new Island { Center = new Vector2(143, 430), RadiusX = 4.6f, RadiusZ = 4.1f, Height = 2.4f, Arena = 4, Seed = 30 },
            new Island { Center = new Vector2(156, 428), RadiusX = 5.1f, RadiusZ = 4.6f, Height = 2.4f, Arena = 4, Seed = 31 },
            new Island { Center = new Vector2(171, 430), RadiusX = 4.6f, RadiusZ = 4.3f, Height = 2f, Arena = 4, Seed = 32 },
            new Island { Center = new Vector2(135, 441), RadiusX = 5.1f, RadiusZ = 4.9f, Height = 2.8f, Arena = 4, Seed = 33 },
            new Island { Center = new Vector2(148, 441), RadiusX = 4.1f, RadiusZ = 4.1f, Height = 2f, Arena = 4, Seed = 34 },
            new Island { Center = new Vector2(161, 441), RadiusX = 6.2f, RadiusZ = 5.7f, Height = 3.2f, Arena = 4, Seed = 35 },
            new Island { Center = new Vector2(176, 442), RadiusX = 5.1f, RadiusZ = 5.4f, Height = 2.4f, Arena = 4, Seed = 36 },
            new Island { Center = new Vector2(141, 454), RadiusX = 5.4f, RadiusZ = 4.9f, Height = 2.4f, Arena = 4, Seed = 37 },
            new Island { Center = new Vector2(154, 455), RadiusX = 4.6f, RadiusZ = 4.3f, Height = 2.8f, Arena = 4, Seed = 38 },
            new Island { Center = new Vector2(169, 454), RadiusX = 5.4f, RadiusZ = 5.1f, Height = 2f, Arena = 4, Seed = 39 },
            new Island { Center = new Vector2(148, 467), RadiusX = 4.6f, RadiusZ = 4.3f, Height = 2f, Arena = 4, Seed = 40 },
            new Island { Center = new Vector2(162, 468), RadiusX = 5.4f, RadiusZ = 4.9f, Height = 2.4f, Arena = 4, Seed = 41 },
            // Travessia das Ilhotas ao Cemitério
            new Island { Center = new Vector2(161, 480), RadiusX = 2.6f, RadiusZ = 2.4f, Height = 2.5f, Seed = 50 },
            new Island { Center = new Vector2(159, 491), RadiusX = 2.8f, RadiusZ = 2.6f, Height = 2.5f, Seed = 51 },
            // Travessia da Enseada à Cratera, depois da ilhota das pedras: três ilhotas para o norte, com curvas leves
            new Island { Center = new Vector2(153, 182), RadiusX = 3.2f, RadiusZ = 3f, Height = 2.5f, Seed = 60 },
            new Island { Center = new Vector2(148, 192), RadiusX = 3f, RadiusZ = 3.2f, Height = 3f, Seed = 61 },
            new Island { Center = new Vector2(154, 199), RadiusX = 3.4f, RadiusZ = 2.8f, Height = 2.2f, Seed = 62 },
            // Travessia da Cratera ao Farol: três ilhotas para o norte até a praia sul do Farol
            new Island { Center = new Vector2(150, 249), RadiusX = 3f, RadiusZ = 3f, Height = 2.6f, Seed = 67 },
            new Island { Center = new Vector2(146, 260), RadiusX = 3.2f, RadiusZ = 3f, Height = 2.6f, Seed = 68 },
            new Island { Center = new Vector2(151, 271), RadiusX = 3f, RadiusZ = 3f, Height = 2.4f, Seed = 69 },
            // Ilhas avulsas (cenário, sem acesso): sorteadas em PlaceFarIslands; a elipse fica fora do mapa
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 4f, Seed = 70 },
            // (sobra da rota antiga: fora do mapa)
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.6f, Seed = 66 },
            // Ilha da Cratera (sala 11): anel com a borda alta (o terraço) em volta de uma lagoa funda; a forma da borda
            // e da lagoa segue o contorno da ilha (ShapeCrater), o terraço daqui só dá a altura e o centro das escadas
            new Island
            {
                Center = new Vector2(152, 225), RadiusX = 24, RadiusZ = 18, Height = 2.5f, Arena = 6, Seed = 80,
                Terraces = new[] { new Terrace { Center = new Vector2(152, 225), RadiusX = 16f, RadiusZ = 12f, Height = 6f } },
            },
            // Estaleiro de Anéis (sala 13): pátio plano com uma rampa de lançamento mais alta a oeste
            new Island
            {
                Center = new Vector2(160, 375), RadiusX = 17, RadiusZ = 13, Height = 2.5f, Arena = 7, Seed = 81,
                Terraces = new[] { new Terrace { Center = new Vector2(150, 377), RadiusX = 5, RadiusZ = 6.5f, Height = 5f } },
            },
            // Travessia do Farol ao Estaleiro: três ilhotas para o norte (a quarta entrada sobrou: fora do mapa)
            new Island { Center = new Vector2(152, 330), RadiusX = 3f, RadiusZ = 3f, Height = 2.5f, Seed = 70 },
            new Island { Center = new Vector2(158, 340), RadiusX = 3.2f, RadiusZ = 3f, Height = 2.8f, Seed = 71 },
            new Island { Center = new Vector2(153, 350), RadiusX = 3f, RadiusZ = 3.2f, Height = 2.5f, Seed = 72 },
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.4f, Seed = 73 },
            // Travessia do Estaleiro às Ilhotas: três ilhotas para o norte até a ilhota do sudoeste das Ilhotas (as outras
            // duas entradas sobraram: fora do mapa)
            new Island { Center = new Vector2(162, 397), RadiusX = 3f, RadiusZ = 3f, Height = 2.5f, Seed = 74 },
            new Island { Center = new Vector2(156, 410), RadiusX = 3.2f, RadiusZ = 3f, Height = 2.6f, Seed = 75 },
            new Island { Center = new Vector2(150, 419), RadiusX = 3f, RadiusZ = 3f, Height = 2.4f, Seed = 76 },
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.6f, Seed = 77 },
            new Island { Center = new Vector2(-999, -999), RadiusX = 1, RadiusZ = 1, Height = 2.5f, Seed = 78 },
        };

        const int k_CrossingA = 6, k_CrossingB = 7, k_FarolIsland = 8, k_Enseada = 9, k_Mainland = 10,
            k_CoastIslets = 11, k_Cemetery = 14, k_FarolToIslets = 15, k_IsletArena = 17, k_IsletCount = 12,
            k_IsletsToCemetery = 29, k_FarolRoute = 31, k_FarIslands = 37,
            k_Crater = 39, k_Shipyard = 40;

        // Ilhotas das travessias, na ordem
        static readonly int[] k_CraterRoute = { 31, 32, 33 };
        static readonly int[] k_FarolRoute2 = { 34, 35, 36 };
        static readonly int[] k_ShipyardRoute = { 41, 42, 43 };
        static readonly int[] k_IsletsRoute = { 45, 46, 47 };

        // Rampas invisíveis das escadas (só colisores), dentro do bioma
        static Transform s_StairRamps;

        // Costa: o continente vai da borda sul até a linha da praia (CoastBeach, em células); o penhasco, com o topo
        // em k_CliffTop, até a linha dele (CoastCliff). A praça do alto (onde a Via chega) fica em volta de k_PlazaX.
        const float k_CliffTop = 20f;
        const float k_PlazaX = 104f;
        const float k_PlazaHalfWidth = 20f;
        static float CoastBeach(float x) => 23f + 2.5f * Mathf.Sin(x * 0.13f) + 1.5f * Mathf.Sin(x * 0.31f + 1f);
        static float CoastCliff(float x) => 10f + 1.5f * Mathf.Sin(x * 0.17f + 2f) + Mathf.Sin(x * 0.41f);

        // Onde a Via chega (topo do penhasco, na borda sul do mapa, no meio da praça)
        static readonly Vector3 k_Arrival = new Vector3(k_PlazaX, k_CliffTop, 0f);

        // Deslocamento do bioma na cena (zero na cena própria; na Via, para o topo do penhasco encostar no desfiladeiro)
        static Vector3 s_Offset;
        static readonly Vector2 k_CoveCenter = new Vector2(149, 147);

        // Anel Partido: canal entre as metades (células) e o anel em pé sobre ele (metros)
        const float k_ChannelX = 72f;
        const float k_ChannelHalfWidth = 3.5f;
        static readonly Vector3 k_RingCenter = new Vector3(k_ChannelX * k_Cell, -14f, 220f);
        const float k_RingRadius = 42f;
        const float k_BridgeZ = 111f;

        // Linha d'água das ruínas: abaixo dela a pedra fica molhada, com cracas
        const float k_WaterLine = 0.8f;

        static float[,] s_Height;      // metros; k_SeaBed = mar
        static int[,] s_Island;        // índice + 1
        static bool[,] s_Ramp;
        static Vector2Int[,] s_RampDir;
        static ArquipelagoTextureKit.Set s_Materials;

        static Vector2Int V(int x, int z) => new Vector2Int(x, z);

        [MenuItem("Ícaro/Arquipélago/Gerar Arquipélago", priority = 47)]
        static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Arquipélago", "Texturas", 0.1f);
                s_Materials = ArquipelagoTextureKit.Build();
                GridTheme theme = Theme();
                Material sky = GridSceneKit.NightSkyMaterial(theme);

                EditorUtility.DisplayProgressBar("Arquipélago", "Cena", 0.3f);
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = SceneSystemsBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(theme, sky, systems);

                var world = new GameObject("Mapa").transform;
                PlacePlayer(scene);
                EditorSceneManager.SaveScene(scene, k_ScenePath);
                string report = BuildContent(world, Vector3.zero, k_NavMeshFolder);
                GridSceneKit.AddDistanceCulling(world, 300f, "Mar", "Gigantes_Fundo", "Feixe_Farol", "Destroco_",
                    "AnelGirando");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorUtility.DisplayProgressBar("Arquipélago", "Oclusão (alguns minutos)", 0.97f);
                GridSceneKit.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Arquipélago gerado em {k_ScenePath}: {report}. Dê Play nessa cena.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Monta o Arquipélago numa cena já aberta (a Via), com o topo da praça do penhasco em 'arrival' (o fim do
        // desfiladeiro). Devolve as texturas do bioma (o desfiladeiro usa as mesmas).
        public static ArquipelagoTextureKit.Set BuildInto(Transform parent, Vector3 arrival, string navMeshFolder,
            out string report)
        {
            s_Materials = ArquipelagoTextureKit.Build();
            var world = new GameObject("Arquipelago").transform;
            world.SetParent(parent, false);
            report = BuildContent(world, arrival - k_Arrival, navMeshFolder);
            return s_Materials;
        }

        // Tudo do bioma dentro de 'world': montado na origem e depois deslocado por 'offset' (antes do NavMesh)
        static string BuildContent(Transform world, Vector3 offset, string navMeshFolder)
        {
            s_Offset = offset;
            GridTheme theme = Theme();
            BuildHeights();
            BuildTerrain(world);
            BuildSea(world);

            // Ruínas: cada bloco sorteia um tom; o musgo e a pedra molhada saem no fim (CreateRuin)
            var ruins = new RuinMesh(101);
            var ring = new RuinMesh(202);
            var giants = new RuinMesh(303);
            var silhouettes = new RuinMesh(404, single: true);
            var lighthouse = new RuinMesh(505);
            var rocks = new RuinMesh(606, 1);
            var shoreRocks = new RuinMesh(808, 1);
            ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
            ArquipelagoRuinKit.VineMaterial = s_Materials.Vine;
            ArquipelagoRuinKit.VineSize = s_Materials.VineSize;
            s_StairRamps = new GameObject("Rampas_Escadas").transform;
            s_StairRamps.SetParent(world, false);
            (Vector3 doorPoint, Vector3 doorForward) = BuildConnections(ruins);
            BuildCoast(ruins, rocks, shoreRocks, world);
            BuildChannelBridge(ruins);
            var crossing = new Crossing();
            BuildEnseadaCrossing(ruins, ref crossing);
            BuildCraterCrossings(ruins, ref crossing);
            BuildShipyardCrossings(ruins, ref crossing);
            BuildIsletCrossings(ruins, ref crossing);
            BuildCrater(ruins);
            BuildShipyard(ruins);
            var colossal = new RuinMesh(1616);
            s_TempleStairs = new RuinMesh(1618);
            BuildColossalBridge(colossal, world, ref crossing);
            List<Vector3> isletSpawns = BuildIsletArena(ruins);
            var cemetery = new RuinMesh(707);
            List<Vector3> cemeterySpawns = BuildCemetery(cemetery);
            BuildCemeteryArcs(world);
            BuildEnseada(ruins);
            BuildTerraceStairs(ruins);
            BuildLighthouse(lighthouse, world);
            BuildBrokenRing(ring);
            BuildLookouts(ruins);
            List<Vector3> cover = BuildCover(ruins);
            var seaRuins = new RuinMesh(909);
            BuildFarIslandDecor(ruins, rocks);
            BuildSeaRuins(seaRuins);
            BuildNearGiants(giants);
            BuildGiants(giants, silhouettes);
            var piles = new RuinMesh(1417);
            BuildCemeteryGiants(giants, piles, world);
            CreateRuin("Ruinas", ruins, s_Materials.StoneTones, world, true);
            CreateRuin("Rochas", rocks, new[] { s_Materials.Cliff }, world, true, -100f);
            CreateRuin("Rochas_Costa", shoreRocks, new[] { s_Materials.Cliff }, world, true);
            CreateRuin("AnelColossal", ring, s_Materials.StoneDarkTones, world, true);
            CreateRuin("Farol", lighthouse, s_Materials.StoneTones, world, true);
            CreateRuin("Cemiterio", cemetery, s_Materials.StoneTones, world, true, mossMask: CemeteryMoss);
            CreateRuin("PonteColossal", colossal, s_Materials.StoneTones, world, true, mossMask: BridgeMoss);
            CreateRuin("PonteColossal_Escadas", s_TempleStairs, s_Materials.StoneTones, world, true, mossSlope: 2f);
            s_TempleStairs = null;
            CreateRuin("Gigantes", giants, s_Materials.StoneDarkTones, world, false);
            CreateRuin("Destrocos_Aneis", piles, s_Materials.StoneTones, world, false, mossMask: RubbleMoss);
            CreateRuin("Ruinas_Mar", seaRuins, s_Materials.StoneTones, world, false);
            GridSceneKit.CreateObject(k_Prefix + "_Gigantes_Fundo", "Gigantes_Fundo", silhouettes.Tones[0],
                s_Materials.Silhouette, world, false, false);
            ArquipelagoRuinKit.Vines.Build(k_Prefix + "_Trepadeiras", world);
            ArquipelagoRuinKit.Vines = null;
            world.gameObject.AddComponent<CameraFarClip>().Distance = 2800f; // o mar aberto ao norte vai longe

            // Tufos e flores: um lote próprio (anda junto com o bioma quando ele é deslocado na Via)
            GridSceneKit.BillboardBatch outer = GridSceneKit.ActiveBatch;
            GridSceneKit.ActiveBatch = new GridSceneKit.BillboardBatch(world);
            PlaceDecor(world, cover);
            GridSceneKit.ActiveBatch.Build(k_Prefix + "_Sprite");
            GridSceneKit.ActiveBatch = outer;

            var hazard = new GameObject("Mar_Queda").AddComponent<SeaHazard>();
            hazard.transform.SetParent(world, false);

            RoomManager room = BuildRoom(world, "Arena_AnelPartido", "Anel Partido", 1, 9, doorPoint, doorForward,
                crossing.AnelExit, crossing.AnelExitForward, out NavMeshSurface surface);
            RoomManager enseadaRoom = BuildRoom(world, "Arena_Enseada", "Enseada", 2, 10, crossing.EnseadaDoor,
                crossing.EnseadaForward, crossing.EnseadaExit, crossing.EnseadaExitForward,
                out NavMeshSurface enseadaSurface);
            RoomManager craterRoom = BuildRoom(world, "Arena_Cratera", "Ilha da Cratera", 6, 11, crossing.CraterDoor,
                crossing.CraterForward, crossing.CraterExit, crossing.CraterExitForward, out NavMeshSurface craterSurface);
            RoomManager farolRoom = BuildRoom(world, "Arena_Farol", "Farol", 3, 12, crossing.FarolDoor,
                crossing.FarolForward, crossing.FarolExit, crossing.FarolExitForward, out NavMeshSurface farolSurface);
            RoomManager shipyardRoom = BuildRoom(world, "Arena_Estaleiro", "Estaleiro de Anéis", 7, 13,
                crossing.ShipyardDoor, crossing.ShipyardForward, crossing.ShipyardExit, crossing.ShipyardExitForward,
                out NavMeshSurface shipyardSurface);
            RoomManager isletsRoom = BuildRoom(world, "Arena_Ilhotas", "Ilhotas", 4, 14, crossing.IsletsDoor,
                crossing.IsletsForward, crossing.IsletsExit, crossing.IsletsExitForward, out NavMeshSurface isletsSurface);
            // O Cemitério é quase todo ruína sobre o mar: a área da sala é o quadrado em volta dos anéis
            Vector3 cemeteryCenter = CemeteryCenter;
            float cemeteryReach = k_CemeteryRings[0].radius + k_CemeteryRings[0].width * 0.5f + 4f;
            RoomManager cemeteryRoom = BuildRoom(world, "Arena_Cemiterio", "Cemitério de Anéis", 5, 15,
                crossing.CemeteryDoor, crossing.CemeteryForward, crossing.CemeteryExit, crossing.CemeteryExitForward,
                out NavMeshSurface cemeterySurface,
                new Rect(cemeteryCenter.x - cemeteryReach, cemeteryCenter.z - cemeteryReach, cemeteryReach * 2f,
                    cemeteryReach * 2f));

            // A saída do Cemitério fecha a rampa da Ponte Colossal na largura toda (logo depois do anel de fora)
            RoomDoor cemeteryExit = cemeteryRoom.Doors[1];
            float exitZ = BridgeStartZ + 3f;
            cemeteryExit.transform.SetPositionAndRotation(new Vector3(CemeteryCenter.x, BridgeTop, exitZ),
                Quaternion.identity);
            cemeteryExit.transform.localScale = new Vector3(k_BridgeWidth / (3f * k_Cell), 1f, 1f);
            RoomManager bridgeRoom = BuildBridgeRoom(world, out NavMeshSurface bridgeSurface,
                out List<Vector3> bridgeSpawnPoints);

            // Onde a câmera pode estar (oclusão): as ilhas do sul, a costa e o alto do farol; ao norte, só a faixa das
            // Ilhotas e do Cemitério (o resto é mar)
            GridSceneKit.AddOcclusionArea("Oclusao_Arquipelago", world,
                new Vector3(k_SouthSize * k_Cell * 0.5f, 12f, k_SouthSize * k_Cell * 0.5f),
                new Vector3(k_SouthSize * k_Cell, 40f, k_SouthSize * k_Cell));
            GridSceneKit.AddOcclusionArea("Oclusao_Arquipelago_Norte", world, new Vector3(310f, 12f, 780f),
                new Vector3(240f, 40f, 960f));
            GridSceneKit.AddOcclusionArea("Oclusao_Ponte_Colossal", world,
                new Vector3(CemeteryCenter.x, 30f, (BridgeStartZ + TempleFrontZ + 80f) * 0.5f),
                new Vector3(140f, 60f, TempleFrontZ + 80f - BridgeStartZ + 20f));

            // Entrada do bioma: logo depois de onde a Via chega (anuncia "Arquipélago" no visor)
            BiomeZone zone = GridSceneKit.AddBiomeZone(theme, k_Arrival + Vector3.forward * 8f,
                new Vector3(k_PlazaHalfWidth * 2f, 12f, 4f), world);
            zone.BiomeName = "Arquipélago";

            // Desloca o bioma para o lugar dele na cena (o nível do mar é guardado em coordenadas do mundo)
            world.position += offset;
            hazard.SeaLevel = offset.y;
            // As partículas de brilho começam na primeira ilhota depois da praia, ao pé das escadarias da costa
            Island firstIslet = k_Islands[k_CoastIslets];
            var sporeStart = world.gameObject.AddComponent<SporeStartZone>();
            sporeStart.Center = firstIslet.Center * k_Cell;
            sporeStart.Radius = Mathf.Max(firstIslet.RadiusX, firstIslet.RadiusZ) * k_Cell + 1f;

            EditorUtility.DisplayProgressBar("Arquipélago", "NavMesh e spawns", 0.7f);
            // Colisores no lugar novo (a máscara do mar livre usa a área deles)
            Physics.SyncTransforms();
            BuildWaterMask(world);
            GridSceneKit.BakeNavMesh(surface, navMeshFolder, "Arena_1");
            GridSceneKit.BakeNavMesh(enseadaSurface, navMeshFolder, "Arena_2");
            GridSceneKit.BakeNavMesh(farolSurface, navMeshFolder, "Arena_3");
            GridSceneKit.BakeNavMesh(isletsSurface, navMeshFolder, "Arena_4");
            GridSceneKit.BakeNavMesh(cemeterySurface, navMeshFolder, "Arena_5");
            GridSceneKit.BakeNavMesh(craterSurface, navMeshFolder, "Arena_6");
            GridSceneKit.BakeNavMesh(shipyardSurface, navMeshFolder, "Arena_7");
            GridSceneKit.BakeNavMesh(bridgeSurface, navMeshFolder, "Arena_8");
            int spawns = PlaceSpawns(room, doorPoint + offset, 1);
            int enseadaSpawns = PlaceSpawns(enseadaRoom, crossing.EnseadaDoor + offset, 2);
            int farolSpawns = PlaceSpawns(farolRoom, crossing.FarolDoor + offset, 3);
            int isletSpawnCount = PlaceSpawns(isletsRoom, crossing.IsletsDoor + offset, 4, isletSpawns);
            int cemeterySpawnCount = PlaceSpawns(cemeteryRoom, crossing.CemeteryDoor + offset, 5, cemeterySpawns);
            int craterSpawns = PlaceSpawns(craterRoom, crossing.CraterDoor + offset, 6);
            int shipyardSpawns = PlaceSpawns(shipyardRoom, crossing.ShipyardDoor + offset, 7);
            int bridgeSpawns = PlaceSpawns(bridgeRoom, bridgeRoom.Doors[0].transform.position, 8, bridgeSpawnPoints);
            return $"Anel Partido ({spawns} pontos de spawn), Enseada ({enseadaSpawns}), Cratera ({craterSpawns}), " +
                   $"Farol ({farolSpawns}), Estaleiro ({shipyardSpawns}), Ilhotas ({isletSpawnCount}) e Cemitério " +
                   $"({cemeterySpawnCount}), Ponte Colossal ({bridgeSpawns}), " +
                   $"{cover.Count} ruínas de cobertura";
        }

        // Ruína em objetos por material: um por tom de pedra, o musgo (faces de cima), a pedra molhada (abaixo da linha
        // d'água) e o fundo dos vãos
        // Materiais das ruínas para quem monta peças fora deste gerador (o Bosque novo)
        internal static void UseMaterials(ArquipelagoTextureKit.Set set) => s_Materials = set;

        internal static void CreateRuin(string name, RuinMesh mesh, Material[] tones, Transform parent, bool collider,
            float waterLine = k_WaterLine, float mossSlope = 0.7f, System.Func<Vector3, float> mossMask = null)
        {
            RuinMesh.Layers layers = mesh.SplitMasked(waterLine, mossSlope, mossMask, s_Materials.Pavings.Length,
                s_Materials.PavingMoss.Length + 1);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < layers.Tones.Length; i++)
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Tom{i + 1}", $"Pedra_{i + 1}", layers.Tones[i],
                    tones[i % tones.Length], root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Musgo", "Musgo", layers.Moss, s_Materials.Moss, root, collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Molhada", "Molhada", layers.Wet, s_Materials.Wet, root,
                collider);
            GridSceneKit.CreateObject($"{k_Prefix}_{name}_Vaos", "Vaos", layers.Dark, s_Materials.Shadow, root, collider);
            // Topos grandes pela máscara de musgo: lajes limpas (variações), transições e musgo
            int pavings = s_Materials.Pavings.Length, blends = s_Materials.PavingMoss.Length;
            for (int i = 0; i < layers.Masked.Length; i++)
            {
                Material material = i < pavings ? s_Materials.Pavings[i]
                    : i < pavings + blends ? s_Materials.PavingMoss[i - pavings] : s_Materials.Moss;
                GridSceneKit.CreateObject($"{k_Prefix}_{name}_Laje{i + 1}", $"Laje_{i + 1}", layers.Masked[i], material,
                    root, collider);
            }
        }

        // Noite verde-azulada, névoa densa (as estruturas viram silhuetas), aurora boreal, sem planeta
        internal static GridTheme Theme() => new GridTheme
        {
            Biome = "Arquipelago",
            BiomeIndex = 1,
            SceneName = "Arquipelago",
            FogColor = new Color(0.05f, 0.2f, 0.19f),
            FogDensity = 0.007f,
            AmbientSky = new Color(0.55f, 0.85f, 0.8f),
            AmbientEquator = new Color(0.4f, 0.62f, 0.58f),
            AmbientGround = new Color(0.2f, 0.3f, 0.28f),
            MoonColor = new Color(0.7f, 0.95f, 0.9f),
            MoonIntensity = 1.1f,
            SkyBlue = new Color(0.05f, 0.28f, 0.26f),
            SkyPurple = new Color(0.03f, 0.12f, 0.16f),
            SkyBlueHeight = 0.25f,
            SkyTop = 0.65f,
            PlanetSize = 0f,
            StarDensity = 0.14f,
            AuroraStrength = 1.6f,
            AuroraLow = new Color(0.15f, 1.3f, 0.7f),
            AuroraHigh = new Color(0.45f, 0.3f, 1.1f),
            AuroraHeight = 0.45f,
            SporeColors = new[] { new Color(0.6f, 1f, 0.95f), new Color(0.85f, 1f, 0.9f), new Color(0.3f, 0.95f, 0.7f) },
        };

        static Vector3 CellToWorld(Vector2 cell, float height) => new Vector3(cell.x * k_Cell, height, cell.y * k_Cell);

        // ---------- ilhas ----------

        static bool InEllipse(Vector2 cell, Vector2 center, float rx, float rz, int seed)
        {
            Vector2 d = cell - center;
            var q = new Vector2(d.x / rx, d.y / rz);
            float angle = Mathf.Atan2(q.y, q.x);
            float limit = 1f + 0.12f * Mathf.Sin(3f * angle + seed) + 0.07f * Mathf.Sin(5f * angle + seed * 2f);
            return q.magnitude <= limit;
        }

        static void BuildHeights()
        {
            s_Height = new float[k_Size, k_Size];
            s_Island = new int[k_Size, k_Size];
            s_Ramp = new bool[k_Size, k_Size];
            s_RampDir = new Vector2Int[k_Size, k_Size];
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                s_Height[x, z] = k_SeaBed;
                var cell = new Vector2(x + 0.5f, z + 0.5f);
                for (int i = 0; i < k_Islands.Length; i++)
                {
                    Island island = k_Islands[i];
                    if (!InEllipse(cell, island.Center, island.RadiusX, island.RadiusZ, island.Seed))
                        continue;
                    s_Island[x, z] = i + 1;
                    float height = island.PlateauHeight > 0f &&
                                   InEllipse(cell, island.PlateauCenter, island.PlateauRadiusX, island.PlateauRadiusZ,
                                       island.Seed + 10)
                        ? island.PlateauHeight
                        : island.Height;
                    for (int t = 0; island.Terraces != null && t < island.Terraces.Length; t++)
                    {
                        Terrace terrace = island.Terraces[t];
                        if (InEllipse(cell, terrace.Center, terrace.RadiusX, terrace.RadiusZ, island.Seed + 10 + t))
                            height = terrace.Height;
                    }

                    s_Height[x, z] = height;
                }
            }

            // Continente: praia e penhasco pela linha da costa, de ponta a ponta do mapa (e além dele a oeste, em
            // MainlandHeight)
            for (int x = 0; x < k_CoastEast; x++)
            for (int z = 0; z < k_SouthSize; z++)
            {
                if (s_Island[x, z] != 0 || z + 0.5f >= CoastBeach(x + 0.5f))
                    continue;
                s_Island[x, z] = k_Mainland + 1;
                s_Height[x, z] = MainlandHeight(x, z);
            }

            CarveChannel();
            ShapeCrater();
            PrepareTerraceStairs();
            PlaceFarIslands();

            // Rampas: em cada terraço, duas células do nível de baixo que sobem 1 m até ele (lados livres)
            for (int i = 0; i < k_Islands.Length; i++)
            {
                Island island = k_Islands[i];
                if (island.PlateauHeight <= 0f)
                    continue;
                var random = new System.Random(90 + i);
                var candidates = new List<(Vector2Int cell, Vector2Int dir)>();
                Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
                for (int x = 2; x < k_Size - 2; x++)
                for (int z = 2; z < k_Size - 2; z++)
                {
                    if (s_Island[x, z] != i + 1 || !Mathf.Approximately(s_Height[x, z], island.Height))
                        continue;
                    foreach (Vector2Int d in dirs)
                    {
                        var up = new Vector2Int(x + d.x, z + d.y);
                        var back = new Vector2Int(x - d.x, z - d.y);
                        var side1 = new Vector2Int(x + d.y, z + d.x);
                        var side2 = new Vector2Int(x - d.y, z - d.x);
                        if (Mathf.Approximately(s_Height[up.x, up.y], island.PlateauHeight) &&
                            Mathf.Approximately(s_Height[back.x, back.y], island.Height) &&
                            Mathf.Approximately(s_Height[side1.x, side1.y], island.Height) &&
                            Mathf.Approximately(s_Height[side2.x, side2.y], island.Height))
                            candidates.Add((V(x, z), d));
                    }
                }

                var chosen = new List<Vector2Int>();
                for (int n = 0; n < 2 && candidates.Count > 0; n++)
                {
                    // A segunda rampa fica longe da primeira
                    int pick = random.Next(candidates.Count);
                    for (int tries = 0; tries < 40 && chosen.Exists(c => Vector2Int.Distance(c, candidates[pick].cell) < 8f); tries++)
                        pick = random.Next(candidates.Count);
                    (Vector2Int cell, Vector2Int dir) = candidates[pick];
                    s_Ramp[cell.x, cell.y] = true;
                    s_RampDir[cell.x, cell.y] = dir;
                    chosen.Add(cell);
                    candidates.RemoveAt(pick);
                }
            }
        }

        // Canal do Anel Partido: abre o mar entre as duas metades, com a margem irregular
        static void CarveChannel()
        {
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                int island = s_Island[x, z];
                if (island == 0 || k_Islands[island - 1].Arena != 1)
                    continue;
                float wobble = 0.9f * Mathf.Sin(z * 0.45f) + 0.6f * Mathf.Sin(z * 1.3f + 1f);
                if (Mathf.Abs(x + 0.5f - k_ChannelX) < k_ChannelHalfWidth + wobble)
                {
                    s_Island[x, z] = 0;
                    s_Height[x, z] = k_SeaBed;
                }
            }
        }

        static float HeightAt(int x, int z)
        {
            if (x >= 0 && z >= 0 && x < k_Size && z < k_Size)
                return s_Height[x, z];
            // A oeste da grade, o continente continua
            return x < 0 && x >= -k_WestCoast && z >= 0 ? MainlandHeight(x, z) : k_SeaBed;
        }

        // Altura do continente numa célula: topo do penhasco, praia ou mar
        static float MainlandHeight(int x, int z)
        {
            if (z + 0.5f >= CoastBeach(x + 0.5f))
                return k_SeaBed;
            return z + 0.5f < CoastCliff(x + 0.5f) ? k_CliffTop : k_Islands[k_Mainland].Height;
        }

        static bool InGrid(int x, int z) => x >= 0 && z >= 0 && x < k_Size && z < k_Size;

        static bool IsLand(int x, int z) => HeightAt(x, z) > 0f;

        // Topo de capim, rampas e penhascos (lados entre alturas diferentes, até o fundo do mar)
        static void BuildTerrain(Transform parent)
        {
            var top = new MeshData();
            var cliffs = new MeshData();
            Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            for (int x = -k_WestCoast; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (!IsLand(x, z))
                    continue;
                float h = HeightAt(x, z);
                if (InGrid(x, z) && s_Ramp[x, z])
                {
                    AddRamp(top, cliffs, x, z, h, s_RampDir[x, z]);
                    continue;
                }

                top.AddHorizontal(x, z, h);
                foreach (Vector2Int d in dirs)
                {
                    int nx = x + d.x, nz = z + d.y;
                    float neighbor = HeightAt(nx, nz);
                    // A rampa vizinha cuida do próprio lado
                    if (neighbor < h && !(nx >= 0 && nz >= 0 && nx < k_Size && nz < k_Size && s_Ramp[nx, nz] &&
                                          s_RampDir[nx, nz] == -d))
                        cliffs.AddSideRange(x, z, d, neighbor, h);
                }
            }

            GridSceneKit.CreateObject(k_Prefix + "_Capim", "Ilhas_Topo", top, s_Materials.Grass, parent, true);
            GridSceneKit.CreateObject(k_Prefix + "_Penhascos", "Ilhas_Penhascos", cliffs, s_Materials.Cliff, parent, true);
        }

        // Rampa de uma célula subindo 1 m na direção 'dir', com os lados em triângulo
        static void AddRamp(MeshData top, MeshData cliffs, int x, int z, float h, Vector2Int dir)
        {
            Vector3 c = GridSceneKit.CellCenter(x, z);
            var forward = new Vector3(dir.x, 0f, dir.y);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (k_Cell * 0.5f);
            Vector3 low = c - forward * (k_Cell * 0.5f) + Vector3.up * h;
            Vector3 high = c + forward * (k_Cell * 0.5f) + Vector3.up * (h + 1f);
            Vector3 slopeNormal = Vector3.Cross(side, high - low).normalized;
            if (slopeNormal.y < 0f)
                slopeNormal = -slopeNormal;
            top.AddQuadAuto(low - side, high - side, high + side, low + side, slopeNormal);
            cliffs.AddTriangleAuto(low - side, high - side, high - side - Vector3.up, -side);
            cliffs.AddTriangleAuto(low + side, high + side, high + side - Vector3.up, side);
        }

        static void BuildSea(Transform parent)
        {
            var sea = new MeshData();
            // Vai bem além do portal, ao norte (o mar aberto com as ruínas de BuildNorthExpanse)
            float size = 5200f;
            Vector3 center = new Vector3(k_Size * k_Cell * 0.5f, 0f, k_Size * k_Cell * 0.5f + 1300f);
            Vector3 a = center + new Vector3(-size, 0f, -size) * 0.5f;
            sea.AddQuadAuto(a, a + Vector3.forward * size, a + new Vector3(size, 0f, size), a + Vector3.right * size,
                Vector3.up);
            GameObject go = GridSceneKit.CreateObject(k_Prefix + "_Mar", "Mar", sea, s_Materials.Sea, parent, false, false,
                false);
            if (go)
            {
                go.AddComponent<WaterRipple>().Speed = new Vector2(0.02f, 0.01f);
            }
        }

        // ---------- ligações ----------

        // Pontos de borda entre duas ilhas na reta de 'from' a 'to' (células): último chão da primeira e primeiro da
        // segunda, já em metros e na altura de cada uma
        static (Vector3 a, Vector3 b) EdgePoints(Vector2 from, Vector2 to)
        {
            Vector2 dir = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            Vector3 a = Vector3.zero, b = Vector3.zero;
            bool leftFirst = false;
            for (float s = 0f; s <= length; s += 0.25f)
            {
                Vector2 p = from + dir * s;
                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.y);
                bool land = IsLand(x, z);
                if (!leftFirst && land)
                    a = CellToWorld(p, HeightAt(x, z));
                else if (!leftFirst)
                    leftFirst = true;
                else if (land)
                {
                    b = CellToWorld(p, HeightAt(x, z));
                    break;
                }
            }

            return (a, b);
        }

        // Ponte de chegada (inteira, com o portão do lado da arena)
        static (Vector3 door, Vector3 forward) BuildConnections(RuinMesh mesh)
        {
            Island start = k_Islands[0], west = k_Islands[1];
            s_ConnectionPoints.Clear();
            s_Crossings.Clear();

            (Vector3 s0, Vector3 s1) = EdgePoints(start.Center, west.Center + new Vector2(0f, -4f));
            Bridge(mesh, s0, s1, 0f, 11);
            s_ConnectionPoints.Add(s1);

            Vector3 forward = s1 - s0;
            forward.y = 0f;
            return (s1 + forward.normalized * 1.5f, forward.normalized);
        }

        // Ponte de pedra de 4 m: tabuleiro de lajes chanfradas levemente tortas sobre arcos de aduelas entre pilares
        // (do fundo do mar ao tabuleiro) e parapeito de balaústres com corrimão, com falhas. Com 'gap' > 0, falta um
        // trecho no meio (pular ou dash).
        static void Bridge(RuinMesh mesh, Vector3 from, Vector3 to, float gap, int seed)
        {
            s_Crossings.Add((from, to));
            var random = new System.Random(seed);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            from -= flat * 1.5f;  // entra um pouco nas ilhas
            to += flat * 1.5f;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float segment = 3f;
            int count = Mathf.CeilToInt(length / segment);
            float gapStart = (length - gap) * 0.5f, gapEnd = (length + gap) * 0.5f;
            bool InGap(float s) => gap > 0f && s > gapStart && s < gapEnd;
            Vector3 Deck(float s) => from + dir.normalized * s;

            for (int i = 0; i < count; i++)
            {
                float s0 = i * segment, s1 = Mathf.Min(length, s0 + segment);
                if (gap > 0f && s1 > gapStart && s0 < gapEnd)
                {
                    // Pedaço quebrado na beira do vão
                    if (s0 < gapStart)
                        s1 = gapStart;
                    else if (s1 > gapEnd)
                        s0 = gapEnd;
                    else
                        continue;
                }

                float size = s1 - s0;
                Vector3 center = Deck((s0 + s1) * 0.5f) - Vector3.up * 0.3f;
                Quaternion tilt = rotation * Quaternion.Euler((float)random.NextDouble() * 2f - 1f, 0f,
                    (float)random.NextDouble() * 2f - 1f);
                BlockMesh.ChamferBox(mesh.Pick(), center, new Vector3(4f, 0.6f, size - 0.08f), tilt, 0.12f);

                // Parapeito: meio-fio, balaústres e corrimão (às vezes falta um trecho)
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() < 0.2)
                        continue;
                    Vector3 edge = center + right * side * 1.8f;
                    BlockMesh.Box(mesh.Pick(), edge + Vector3.up * 0.4f, new Vector3(0.45f, 0.2f, size - 0.1f), tilt);
                    int posts = Mathf.Max(2, Mathf.FloorToInt(size / 0.75f));
                    for (int k = 0; k < posts; k++)
                    {
                        float along = (k + 0.5f) / posts - 0.5f;
                        BlockMesh.Box(mesh.Pick(), edge + rotation * Vector3.forward * (along * size) + Vector3.up * 0.85f,
                            new Vector3(0.2f, 0.7f, 0.2f), tilt);
                    }

                    if (random.NextDouble() < 0.85)
                        BlockMesh.ChamferBox(mesh.Pick(), edge + Vector3.up * 1.3f, new Vector3(0.38f, 0.22f, size - 0.2f),
                            tilt, 0.06f);
                }
            }

            // Pilares e arcos só sobre a água
            float water0 = 1.5f, water1 = length - 1.5f;
            int spans = Mathf.Max(1, Mathf.RoundToInt((water1 - water0) / 9f));
            float spanLength = (water1 - water0) / spans;
            for (int k = 0; k <= spans; k++)
            {
                float s = water0 + k * spanLength;
                Vector3 top = Deck(s) - Vector3.up * 0.6f;
                float height = top.y - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(top.x, k_SeaBed + height * 0.5f, top.z),
                    new Vector2(4.4f, 1.6f), height, Quaternion.LookRotation(flat), 0.2f);
                if (k == spans || InGap(s + spanLength * 0.5f))
                    continue;

                // Arco entre este pilar e o próximo, com o alto encostando no tabuleiro
                float radius = spanLength * 0.5f - 0.8f;
                Vector3 mid = Deck(s + spanLength * 0.5f) - Vector3.up * (0.6f + radius);
                ArquipelagoRuinKit.RichRing(mesh, mid, right, Vector3.up, radius, 0.7f, 3.6f, -90f, 90f, random, false,
                    false, 0f, 0);
            }
        }

        // ---------- costa ----------

        // Praça no alto do penhasco (onde a Via chega pela borda sul): paredões de rocha dos lados, parapeito de
        // balaústres na beira, dois obeliscos e dois braseiros no topo, e duas escadarias espelhadas em zigue-zague que
        // descem até a praia. Da praia, pontes e ilhotas até a ilha de chegada: ponte inteira, ponte quebrada (pular ou
        // dash), coluna tombada e ponte.
        static void BuildCoast(RuinMesh mesh, RuinMesh rocks, RuinMesh shore, Transform parent)
        {
            var random = new System.Random(919);
            Island beach = k_Islands[k_Mainland];
            float x0 = k_PlazaX - k_PlazaHalfWidth, x1 = k_PlazaX + k_PlazaHalfWidth;

            // Beira do penhasco em cada coluna da praça (z do lado de fora da última célula do topo)
            float EdgeAt(float worldX)
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt(worldX / k_Cell), 0, k_Size - 1);
                int z = 0;
                while (z < k_Size - 1 && HeightAt(cx, z + 1) >= k_CliffTop - 0.01f)
                    z++;
                return (z + 1) * k_Cell;
            }

            // Beira simétrica em volta do meio da praça (a mais saliente das duas colunas espelhadas); onde a beira de
            // verdade recua, um bloco de pedra completa o chão até ela
            float EdgeSym(float worldX)
            {
                float d = Mathf.Abs(worldX - k_PlazaX);
                return Mathf.Max(EdgeAt(k_PlazaX + d), EdgeAt(k_PlazaX - d));
            }

            for (float x = x0; x < x1 - 0.01f; x += k_Cell)
            {
                float real = EdgeAt(x + k_Cell * 0.5f), sym = EdgeSym(x + k_Cell * 0.5f);
                if (sym > real + 0.01f)
                    Block(mesh, x, x + k_Cell, real - 1f, sym, k_CliffTop, mesh.Tones[0]);
            }

            // Paredões dos lados da praça, do fim do desfiladeiro até a beira
            foreach (float side in new[] { -1f, 1f })
            {
                float wallX = k_PlazaX + side * (k_PlazaHalfWidth + 3f);
                float end = EdgeAt(wallX) + 2f;
                for (float z = 0f; z < end; z += 4.5f)
                {
                    bool rim = z > EdgeAt(wallX) - 4f;
                    float x = wallX + side * ((float)random.NextDouble() * 2f + (rim ? 5f : 0f));
                    RockPile(rocks, new Vector3(x, k_CliffTop - (rim ? 6f : 0f), z),
                        12f + (float)random.NextDouble() * 9f, random, Vector3.right * -side);
                }
            }

            // Duas escadarias em zigue-zague espelhadas na frente do penhasco, saindo do patamar do alto (no meio da
            // praça), uma para o leste e outra para o oeste: 4 lances cada (cada um numa faixa mais longe da rocha), com
            // patamares entre eles; os patamares do segundo lance das duas se juntam no meio, e as duas chegam lado a
            // lado à praia. Parapeito de balaústres do lado de fora dos lances e em volta dos patamares.
            const float band = 3.4f, run = 0.84f;
            const int flights = 4;
            float flightRise = (k_CliffTop - beach.Height) / flights;
            int steps = Mathf.RoundToInt(flightRise / 0.27f);
            float rise = flightRise / steps, length = steps * run;
            float reach = band + length + band;
            // As faixas começam na parte mais saliente da beira; a primeira encosta na rocha em todo o comprimento
            // (onde a beira recua, não fica buraco entre a praça e a escada)
            float face = 0f, inner = float.MaxValue;
            for (float x = k_PlazaX - reach - 2f; x <= k_PlazaX + reach + 2f; x += k_Cell)
            {
                face = Mathf.Max(face, EdgeSym(x));
                inner = Mathf.Min(inner, EdgeSym(x));
            }

            inner -= 0.5f;
            float BandZ(int k) => face + k * band;
            float InnerZ(int k) => k == 0 ? inner : BandZ(k);
            float landing = k_CliffTop + 0.05f;
            MeshData stairTone = mesh.Tones[0];   // escadas e patamares num tom só
            Block(mesh, k_PlazaX - band, k_PlazaX + band, inner, BandZ(1), landing, stairTone);
            Balusters(mesh, new Vector3(k_PlazaX - band, landing, BandZ(1) - 0.2f),
                new Vector3(k_PlazaX + band, landing, BandZ(1) - 0.2f));
            foreach (float m in new[] { 1f, -1f })
            {
                float sx = k_PlazaX + m * band, dir = m;
                for (int k = 0; k < flights; k++)
                {
                    float top = k_CliffTop - k * flightRise, bottom = top - flightRise;
                    int count = k == flights - 1 ? steps - 1 : steps;
                    for (int i = 0; i < count; i++)
                    {
                        float near = sx + dir * i * run, far = sx + dir * (i + 1) * run;
                        Block(mesh, Mathf.Min(near, far), Mathf.Max(near, far), InnerZ(k), BandZ(k + 1),
                            top - (i + 1) * rise, stairTone);
                    }

                    float laneZ = (InnerZ(k) + BandZ(k + 1)) * 0.5f;
                    StairRamp(new Vector3(sx, top, laneZ), new Vector3(sx + dir * length, bottom, laneZ),
                        BandZ(k + 1) - InnerZ(k));

                    float end = sx + dir * length;
                    // Balaústres do lado de fora do lance, acompanhando a descida
                    float outerZ = BandZ(k + 1) - 0.2f;
                    Balusters(mesh, new Vector3(sx, top, outerZ), new Vector3(end, bottom, outerZ));
                    if (k < flights - 1)
                    {
                        // Patamar no fim do lance, ocupando esta faixa e a próxima, com balaústres do lado de fora e na
                        // ponta (o do segundo lance encosta no da outra escadaria, no meio: sem ponta)
                        Block(mesh, Mathf.Min(end, end + dir * band), Mathf.Max(end, end + dir * band), InnerZ(k),
                            BandZ(k + 2), bottom, stairTone);
                        float tip = end + dir * (band - 0.2f);
                        Balusters(mesh, new Vector3(end, bottom, BandZ(k + 2) - 0.2f),
                            new Vector3(end + dir * band, bottom, BandZ(k + 2) - 0.2f));
                        if (k != 1)
                            Balusters(mesh, new Vector3(tip, bottom, InnerZ(k)), new Vector3(tip, bottom, BandZ(k + 2) - 0.2f));
                    }

                    sx = end;
                    dir = -dir;
                }
            }

            // As duas chegam à praia lado a lado, no meio
            Vector3 foot = new Vector3(k_PlazaX, beach.Height, BandZ(flights) + 1.5f);

            // Parapeito de balaústres na beira da praça (menos em cima do patamar do alto), contínuo: onde a beira muda
            // de uma coluna para a outra, um trecho atravessado liga os dois
            float entry0 = k_PlazaX - band, entry1 = k_PlazaX + band;
            float lastX = float.NaN, lastZ = 0f;
            void Edge(float a, float b, float z)
            {
                if (Mathf.Approximately(lastX, a) && !Mathf.Approximately(lastZ, z))
                    Balusters(mesh, new Vector3(a, k_CliffTop, lastZ), new Vector3(a, k_CliffTop, z));
                Balusters(mesh, new Vector3(a, k_CliffTop, z), new Vector3(b, k_CliffTop, z));
                lastX = b;
                lastZ = z;
            }

            for (float x = x0; x < x1 - 0.01f; x += k_Cell)
            {
                float z = EdgeSym(x + k_Cell * 0.5f) - 0.3f, right = x + k_Cell;
                if (right <= entry0 || x >= entry1)
                    Edge(x, right, z);
                else
                {
                    if (x < entry0)
                        Edge(x, entry0, z);
                    if (right > entry1)
                    {
                        lastX = float.NaN;
                        Edge(entry1, right, z);
                    }
                }
            }

            // Obeliscos com anéis dos dois lados do topo e, mais perto do patamar, dois braseiros com chama azul-esverdeada
            var gate = new Vector3(k_PlazaX, k_CliffTop, face - 0.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                float ox = k_PlazaX + side * (band + 4.5f), bx = k_PlazaX + side * (band + 1f);
                Obelisk(mesh, new Vector3(ox, k_CliffTop, EdgeSym(ox) - 4.5f), random);
                Brazier(mesh, parent, new Vector3(bx, k_CliffTop, EdgeSym(bx) - 1.6f), side > 0 ? 1 : 2);
            }

            s_ConnectionPoints.AddRange(new[] { gate, foot });

            // Da praia à ilha de chegada
            Vector2 FootCell() => new Vector2(foot.x / k_Cell, foot.z / k_Cell);
            Island a = k_Islands[k_CoastIslets], b = k_Islands[k_CoastIslets + 1], c = k_Islands[k_CoastIslets + 2];
            Island arrival = k_Islands[0];
            (Vector3 p0, Vector3 p1) = EdgePoints(FootCell(), a.Center);
            Bridge(mesh, p0, p1, 0f, 81);
            (Vector3 q0, Vector3 q1) = EdgePoints(a.Center, b.Center);
            Bridge(mesh, q0, q1, 2.5f, 82);
            (Vector3 r0, Vector3 r1) = EdgePoints(b.Center, c.Center);
            FallenColumn(mesh.Pick(), r0, r1);
            (Vector3 t0, Vector3 t1) = EdgePoints(c.Center, arrival.Center);
            Bridge(mesh, t0, t1, 0f, 84);
            s_ConnectionPoints.AddRange(new[] { p0, p1, q0, q1, r0, r1, t0, t1 });

            BuildShoreRocks(shore, foot, p0, p1);
        }

        // Parapeito de balaústres de 'a' a 'b' (pontos do chão; pode descer junto com uma escada): balaústres a cada
        // ~2 m, corrimão e soleira (na escada, mais alta, para cobrir os degraus)
        static void Balusters(RuinMesh mesh, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.05f)
                return;
            Quaternion along = Quaternion.LookRotation(d);
            var flat = new Vector3(d.x, 0f, d.z);
            Quaternion yaw = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat) : Quaternion.identity;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            for (int i = 0; i <= count; i++)
                BlockMesh.Box(mesh.Pick(), Vector3.Lerp(a, b, (float)i / count) + Vector3.up * 0.4f,
                    new Vector3(0.3f, 1.4f, 0.3f), yaw);
            Vector3 middle = (a + b) * 0.5f;
            BlockMesh.ChamferBox(mesh.Pick(), middle + Vector3.up * 1.2f, new Vector3(0.5f, 0.25f, length + 0.05f), along,
                0.05f);
            bool sloped = Mathf.Abs(d.y) > 0.01f;
            BlockMesh.Box(mesh.Pick(), middle + Vector3.up * (sloped ? -0.1f : 0.1f),
                new Vector3(0.6f, sloped ? 0.6f : 0.2f, length + 0.05f), along);
        }

        // Braseiro de pedra (pé, coluna, bacia) com a chama em sprite animado azul-esverdeado que olha para a câmera
        // (SpriteFlame) e uma luz da mesma cor, baixa, que tremula
        static void Brazier(RuinMesh mesh, Transform parent, Vector3 foot, int index)
        {
            // A chama era uma malha 3D: apaga os assets antigos
            foreach (string old in new[]
            {
                $"{GridSceneKit.MeshFolder}/{k_Prefix}_Braseiro_Chama_{index}.asset",
                $"{GridTextureKit.MaterialsFolder}/Arquipelago_Brasa.mat",
                IcaroAssetUtility.Root + "/Art/Grade/Texturas/Arquipelago_Brasa.png",
                IcaroAssetUtility.Root + "/Art/Grade/Texturas/Arquipelago_Brasa_Brilho.png",
            })
            {
                if (AssetDatabase.LoadMainAssetAtPath(old))
                    AssetDatabase.DeleteAsset(old);
            }

            BlockMesh.Prism(mesh.Pick(), foot, Vector3.up, 0.25f, 0.75f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + Vector3.up * 0.25f, Vector3.up, 0.8f, 0.4f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + Vector3.up * 1.05f, Vector3.up, 0.15f, 0.6f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + Vector3.up * 1.2f, Vector3.up, 0.35f, 0.85f, 8, 22.5f);
            var root = new GameObject($"Braseiro_{index}").transform;
            root.SetParent(parent, false);
            root.position = foot + Vector3.up * 1.45f;
            var flame = new GameObject("Chama").AddComponent<SpriteFlame>();
            flame.transform.SetParent(root, false);
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = Vector3.up * 0.6f;
            light.type = LightType.Point;
            light.color = new Color(0.3f, 1f, 0.8f);
            light.range = 9f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>().FailuresPerSecond = 0f;
        }

        // Textura que brilha inteira, com uma leve variação (chamas, lentes)
        static GridTextureKit.Paint GlowPaint(Color color, int seed, bool bands = false)
        {
            GridTextureKit.Paint p = GridTextureKit.Tile(16, 16, seed, color);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, p.Vary(bands && y % 4 == 0 ? color * 0.7f : color, 0.08f), true);
            return p;
        }

        // Costa de pedras, de ponta a ponta: o paredão do penhasco é coberto por pilhas enormes de rocha irregular
        // (como os paredões do desfiladeiro, só que maiores: quase todas de 18 a 26 m, algumas bem maiores e outras
        // menores), encostadas na face e passando da beira de cima; no pé delas, uma pilha comprida de pedras no chão
        // da praia; pedras soltas na praia, rochas e agulhas na água e pedras na beira de cima. Na frente da escadaria
        // e no caminho da praia até a primeira ponte não há pedras; em volta da praça as pilhas ficam abaixo da beira.
        static void BuildShoreRocks(RuinMesh mesh, Vector3 stairsFoot, Vector3 bridgeStart, Vector3 bridgeEnd)
        {
            var random = new System.Random(4141);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            float beachTop = k_Islands[k_Mainland].Height;
            float CliffZ(float x) => Mathf.Ceil(CoastCliff(Mathf.FloorToInt(x / k_Cell) + 0.5f) - 0.5f) * k_Cell;
            float BeachZ(float x) => Mathf.Ceil(CoastBeach(Mathf.FloorToInt(x / k_Cell) + 0.5f) - 0.5f) * k_Cell;
            // A escadaria desce na frente do penhasco, ao lado da praça
            bool Stairs(float x) => Mathf.Abs(x - k_PlazaX) < 26f;
            bool Clear(Vector3 p, float size) => !NearConnection(p) &&
                                                 DistanceToSegment(p, stairsFoot, bridgeStart) > size + 3.5f &&
                                                 DistanceToSegment(p, bridgeStart, bridgeEnd) > size + 3f;

            // Paredão: pilhas encostadas na face, uma emendando na outra
            for (float x = -k_WestCoast * k_Cell; x < k_CoastEast * k_Cell; x += R(5f, 9f))
            {
                if (Stairs(x))
                    continue;
                double roll = random.NextDouble();
                float height = roll < 0.15 ? R(28f, 38f) : roll < 0.35 ? R(12f, 17f) : R(18f, 26f);
                float width = height > 27f ? R(14f, 20f) : R(9f, 14f);
                if (Mathf.Abs(x - k_PlazaX) < k_PlazaHalfWidth + 12f)
                    height = Mathf.Min(height, k_CliffTop - beachTop - 3f);
                CoastPile(mesh, new Vector3(x + R(-1f, 1f), beachTop, CliffZ(x) + R(1.5f, 4f)), height, width,
                    random);
            }

            for (float x = -k_WestCoast * k_Cell; x < k_CoastEast * k_Cell; x += R(1.5f, 3f))
            {
                float cliffZ = CliffZ(x), beachZ = BeachZ(x);
                if (!Stairs(x))
                {
                    // Pilha comprida de pedras no chão, no pé do paredão
                    int count = 1 + random.Next(2);
                    for (int k = 0; k < count; k++)
                    {
                        float size = R(1.4f, 3.6f);
                        var p = new Vector3(x + R(-1f, 1f), beachTop + size * 0.3f, cliffZ + R(3.5f, 6.5f));
                        if (Clear(p, size))
                            ArquipelagoRuinKit.IrregularRock(mesh, p, size, random);
                    }

                    // Pedras soltas na praia
                    if (random.NextDouble() < 0.25 && beachZ - cliffZ > 12f)
                    {
                        float size = R(0.7f, 2.4f);
                        var p = new Vector3(x + R(-1f, 1f), beachTop + size * 0.2f, R(cliffZ + 9f, beachZ - 2f));
                        if (Clear(p, size))
                            ArquipelagoRuinKit.IrregularRock(mesh, p, size, random);
                    }
                }

                // Na água, perto da praia
                if (random.NextDouble() < 0.2)
                {
                    float size = R(2f, 5f);
                    var p = new Vector3(x + R(-1f, 1f), R(-1f, 1.2f), beachZ + R(1f, 11f));
                    if (Clear(p, size))
                    {
                        if (random.NextDouble() < 0.15)
                        {
                            // Agulha de pedra
                            var stack = new Vector3(R(2.5f, 4.5f), R(8f, 16f), R(2.5f, 4.5f));
                            BlockMesh.ChamferBox(mesh.Pick(), new Vector3(p.x, k_SeaBed + stack.y * 0.5f, p.z), stack,
                                Quaternion.Euler(R(-6f, 6f), R(0f, 90f), R(-6f, 6f)), stack.x * 0.2f);
                            ArquipelagoRuinKit.IrregularRock(mesh, new Vector3(p.x, k_SeaBed + stack.y, p.z), stack.x * 0.8f, random);
                        }
                        else
                            ArquipelagoRuinKit.IrregularRock(mesh, p, size, random);
                    }
                }

                // Pedras na beira de cima (fora da praça)
                if (Mathf.Abs(x - k_PlazaX) > 40f && random.NextDouble() < 0.12)
                {
                    float size = R(1.2f, 3f);
                    ArquipelagoRuinKit.IrregularRock(mesh, new Vector3(x, k_CliffTop + size * 0.2f, cliffZ - R(4f, 8f)), size, random);
                }
            }
        }

        // Pilha do paredão da costa: dois ou três blocos que afinam para cima, um pouco tortos, a partir da praia
        // ('foot' = base, com a frente em foot.z, e o corpo entrando no penhasco); cada bloco recua um pouco da frente
        // do de baixo. Trepadeiras nas faces viradas para o mar.
        static void CoastPile(RuinMesh mesh, Vector3 foot, float height, float width, System.Random random)
        {
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            float y = foot.y - 2f, front = foot.z;
            int parts = 2 + random.Next(2);
            for (int i = 0; i < parts; i++)
            {
                float h = (height + 2f) / parts * R(0.8f, 1.2f);
                float depth = width * R(0.8f, 1.1f);
                Quaternion tilt = Quaternion.Euler(R(-4f, 4f), R(-18f, 18f), R(-4f, 4f));
                var center = new Vector3(foot.x + R(-1f, 1f), y + h * 0.5f, front - depth * 0.5f);
                var footprint = new Vector2(width, depth);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), center, footprint, h, tilt, width * 0.15f);
                ArquipelagoRuinKit.VinesOnBlock(center, footprint, h, tilt, Vector3.forward, random, 0.8f);
                y += h * 0.9f;
                width *= 0.78f;
                front -= R(0.3f, 1.5f);
            }
        }

        // Distância no plano entre um ponto e um segmento
        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var p2 = new Vector2(p.x, p.z);
            var a2 = new Vector2(a.x, a.z);
            Vector2 ab = new Vector2(b.x, b.z) - a2;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p2, a2 + ab * t);
        }

        // Coluna gigante tombada entre duas ilhas: prisma de 8 lados deitado com uma face plana em cima (dá para andar),
        // capitel quebrado numa ponta
        static void FallenColumn(MeshData mesh, Vector3 from, Vector3 to)
        {
            s_Crossings.Add((from, to));
            Vector3 dir = to - from;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            float radius = 2.8f;
            float apothem = radius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            Vector3 start = from - flat * 3f;
            Vector3 end = to + flat * 3f;
            Vector3 axis = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            // A face de cima fica na altura do chão: o eixo passa 'apothem' abaixo
            BlockMesh.Prism(mesh, start - Vector3.up * (apothem + 0.05f), axis, length, radius, 8, 22.5f);
            BlockMesh.Box(mesh, end - Vector3.up * apothem + axis * 1.5f + Vector3.up * 0.4f,
                new Vector3(radius * 2.4f, radius * 1.4f, 2.5f), Quaternion.LookRotation(axis) * Quaternion.Euler(0f, 0f, 12f));
        }

        // Bloco maciço da escadaria (do fundo do mar até 'top'), com as quinas chanfradas
        static void Block(RuinMesh mesh, float xMin, float xMax, float zMin, float zMax, float top,
            MeshData tone = null)
        {
            float height = top - k_SeaBed;
            ArquipelagoRuinKit.Pillar(tone ?? mesh.Pick(), new Vector3((xMin + xMax) * 0.5f, k_SeaBed + height * 0.5f,
                (zMin + zMax) * 0.5f), new Vector2(xMax - xMin, zMax - zMin), height, Quaternion.identity, 0.06f);
        }

        // Pilha de rocha (paredão): dois ou três blocos que afinam para cima, um pouco tortos, do chão ('foot') até
        // 'height' acima dele, com trepadeiras nas faces viradas para 'view'
        static void RockPile(RuinMesh mesh, Vector3 foot, float height, System.Random random, Vector3 view)
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
                ArquipelagoRuinKit.Pillar(mesh.Pick(), center, footprint, h, tilt, width * 0.15f);
                ArquipelagoRuinKit.VinesOnBlock(center, footprint, h, tilt, view, random);
                y += h * 0.9f;
                width *= 0.75f;
            }
        }

        // ---------- Anel Partido ----------

        // Ponte de pedra inteira cruzando o canal no meio
        static void BuildChannelBridge(RuinMesh mesh)
        {
            (Vector3 a, Vector3 b) = EdgePoints(new Vector2(k_ChannelX - 14f, k_BridgeZ),
                new Vector2(k_ChannelX + 14f, k_BridgeZ));
            Bridge(mesh, a, b, 0f, 31);
            s_ConnectionPoints.AddRange(new[] { a, b });
        }

        // Ponte que desaba (CollapsingBridge; o Anel Partido não usa, fica para outras arenas): cada pedaço do
        // tabuleiro é um objeto com a malha centrada nele (para cair girando); os pilares ficam no mar depois da queda
        // (vão na malha estática 'pillars')
        static CollapsingBridge CollapsibleBridge(RuinMesh pillars, Transform parent, Vector3 from, Vector3 to,
            int index, float collapseAt)
        {
            var random = new System.Random(300 + index);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            from -= flat * 1.5f;
            to += flat * 1.5f;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            var root = new GameObject($"PonteQueDesaba_{index}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation((from + to) * 0.5f, rotation);
            var bridge = root.AddComponent<CollapsingBridge>();
            bridge.CollapseAt = collapseAt;

            // Ao cair, o NavMesh perde só o trecho sobre a água
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = new Vector3(5f, 4f, Mathf.Max(1f, length - 4f));
            obstacle.enabled = false;

            float segment = 3f;
            int count = Mathf.CeilToInt(length / segment);
            var segments = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                float s0 = i * segment, s1 = Mathf.Min(length, s0 + segment);
                float size = s1 - s0 - 0.08f;
                var piece = new MeshData();
                BlockMesh.ChamferBox(piece, Vector3.zero, new Vector3(4f, 0.6f, size), Quaternion.identity, 0.12f);
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() < 0.3)
                        continue;
                    float height = 0.6f + (float)random.NextDouble() * 0.4f;
                    BlockMesh.Box(piece, new Vector3(side * 1.75f, 0.3f + height * 0.5f, 0f),
                        new Vector3(0.5f, height, size - 0.3f), Quaternion.identity);
                }

                GameObject go = GridSceneKit.CreateObject($"{k_Prefix}_Ponte{index}_{i:00}", $"Pedaco_{i:00}", piece,
                    s_Materials.Stone, root.transform, true);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                Vector3 center = from + dir.normalized * ((s0 + s1) * 0.5f) - Vector3.up * 0.3f;
                Quaternion tilt = rotation * Quaternion.Euler((float)random.NextDouble() * 3f - 1.5f, 0f,
                    (float)random.NextDouble() * 3f - 1.5f);
                go.transform.SetPositionAndRotation(center, tilt);
                segments.Add(go.transform);

                if (i % 3 == 1)
                    BlockMesh.Prism(pillars.Pick(), center - Vector3.up * 0.6f, Vector3.down, center.y + 6f, 0.9f, 8,
                        22.5f);
            }

            bridge.Segments = segments.ToArray();
            return bridge;
        }

        // O anel colossal em pé sobre o canal: semiafundado, de blocos chanfrados com canaleta e aros grossos, com o
        // alto partido (o trecho entre 38° e 58° caiu) e um anel interno mais fino, quebrado antes
        static void BuildBrokenRing(RuinMesh ring)
        {
            var random = new System.Random(808);
            BlockMesh.Tile = k_Cell * 2f;
            // Ângulo 0 = topo; positivo para o norte
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius, 6f, 7f, -78f, 38f,
                random, false, true);
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius, 6f, 7f, 58f, 112f,
                random, true, false);
            // A perna do anel interno desce até abaixo da água (a -62° ela ainda ficava 2 m acima, flutuando)
            ArquipelagoRuinKit.RichRing(ring, k_RingCenter, Vector3.right, Vector3.up, k_RingRadius - 7.5f, 3f, 4f, -74f,
                12f, random, false, true, 0f, 4);
            BlockMesh.Tile = k_Cell;
        }

        // Um mirante de runas em cada metade, com a rampa de baixo virada para o canal
        static void BuildLookouts(RuinMesh mesh)
        {
            float top = k_Islands[1].Height;
            var random = new System.Random(909);
            RuneLookout(mesh, new Vector2(52f, 106f), -90f, top, random);
            RuneLookout(mesh, new Vector2(93f, 116f), 90f, top, random);
        }

        // Mirante de runas (ponto alto): pirâmide de três degraus largos de pedra, 1,5 m cada (topo a 4,5 m do chão;
        // sobe-se pulando de degrau em degrau), com base, cornija em cada degrau e, no topo, um arco quebrado do anel
        // como monumento e cobertura. 'yaw' gira a pirâmide.
        static void RuneLookout(RuinMesh mesh, Vector2 centerCell, float yaw, float ground, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 origin = CellToWorld(centerCell, ground);
            Vector3 P(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);

            float[] sizes = { 12f, 8f, 4.5f };
            const float step = 1.5f;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), P(0f, 0.175f, 0f), Vector2.one * (sizes[0] + 0.6f), 0.35f, rotation,
                0.2f);
            for (int i = 0; i < sizes.Length; i++)
            {
                float height = step * (i + 1);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), P(0f, height * 0.5f, 0f), Vector2.one * sizes[i], height, rotation,
                    0.25f);
                // Cornija logo abaixo da borda (o topo do degrau continua liso para andar)
                for (int face = 0; face < 4; face++)
                {
                    Quaternion side = rotation * Quaternion.Euler(0f, 180f + face * 90f, 0f);
                    BlockMesh.ChamferBox(mesh.Pick(), origin + side * Vector3.forward * (sizes[i] * 0.5f + 0.1f) +
                                                      Vector3.up * (height - 0.4f),
                        new Vector3(sizes[i] + 0.4f, 0.3f, 0.2f), side, 0.06f);
                }

                ArquipelagoRuinKit.HangVine(P(sizes[i] * 0.3f, height - 0.25f, -sizes[i] * 0.5f - 0.25f),
                    rotation * Vector3.back, random);
                ArquipelagoRuinKit.HangVine(P(-sizes[i] * 0.5f - 0.25f, height - 0.25f, 0.3f * sizes[i]),
                    rotation * Vector3.left, random);
            }

            // Arco quebrado do anel no topo (uma perna presa no degrau, a outra partida no alto)
            ArquipelagoRuinKit.RichRing(mesh, P(0f, step * 3f - 0.8f, 0f), rotation * Vector3.forward, Vector3.up, 2f,
                0.6f, 0.9f, -95f, 60f, random, false, true, ground + step * 3f, 2);

            float half = sizes[0] * 0.5f;
            s_ConnectionPoints.AddRange(new[]
            {
                P(0f, 0f, 0f), P(half, 0f, half), P(-half, 0f, half), P(half, 0f, -half),
                P(-half, 0f, -half),
            });
        }

        // Rampa: laje cujo topo vai de 'low' a 'high' (pontos no meio da borda de baixo e de cima)
        static void Slope(MeshData mesh, Vector3 low, Vector3 high, float width)
        {
            const float thick = 0.8f;
            Vector3 dir = high - low;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 center = (low + high) * 0.5f - rotation * Vector3.up * (thick * 0.5f);
            BlockMesh.ChamferBox(mesh, center, new Vector3(width, thick, dir.magnitude), rotation, 0.1f);
        }

        // ---------- Farol: travessia ----------

        struct Crossing
        {
            public Vector3 AnelExit, AnelExitForward;        // saída do Anel Partido
            public Vector3 EnseadaDoor, EnseadaForward;      // entrada da Enseada (no fim da ponte)
            public Vector3 EnseadaExit, EnseadaExitForward;  // saída da Enseada (para as pedras)
            public Vector3 CraterDoor, CraterForward;        // entrada da Cratera (costa oeste)
            public Vector3 CraterExit, CraterExitForward;    // saída da Cratera (costa norte)
            public Vector3 FarolDoor, FarolForward;          // entrada do Farol (praia leste)
            public Vector3 FarolExit, FarolExitForward;      // saída do Farol (praia oeste)
            public Vector3 ShipyardDoor, ShipyardForward;    // entrada do Estaleiro (costa leste)
            public Vector3 ShipyardExit, ShipyardExitForward; // saída do Estaleiro (costa norte)
            public Vector3 IsletsDoor, IsletsForward;        // entrada das Ilhotas
            public Vector3 IsletsExit, IsletsExitForward;    // saída das Ilhotas
            public Vector3 CemeteryDoor, CemeteryForward;    // entrada do Cemitério (na ponte, antes do anel de fora)
            public Vector3 CemeteryExit, CemeteryExitForward; // saída do Cemitério (anel de fora, para a Ponte Colossal)
        }

        // Do Anel Partido à Enseada: ponte quebrada (vão no meio: pular ou dash) da metade leste até uma ilhota e ponte
        // inteira da ilhota até o portão da Enseada
        static void BuildEnseadaCrossing(RuinMesh mesh, ref Crossing crossing)
        {
            Island islet = k_Islands[k_CrossingA], enseada = k_Islands[k_Enseada];
            (Vector3 a, Vector3 b) = EdgePoints(new Vector2(96f, 116f), islet.Center);
            Bridge(mesh, a, b, 3f, 41);
            Vector3 flat = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            crossing.AnelExit = a - flat * 1.5f;
            crossing.AnelExitForward = flat;

            (Vector3 c, Vector3 d) = EdgePoints(islet.Center, enseada.Center);
            Bridge(mesh, c, d, 0f, 43);
            Vector3 into = Vector3.ProjectOnPlane(d - c, Vector3.up).normalized;
            crossing.EnseadaDoor = d + into * 1.5f;
            crossing.EnseadaForward = into;
            s_ConnectionPoints.AddRange(new[] { a, b, c, d });
        }

        // ---------- travessias longas (Enseada > Cratera > Farol > Estaleiro > Ilhotas) ----------

        // Tipos de ligação entre duas ilhas: 0 ponte, 1 ponte quebrada (com o vão), 2 pedras, 3 coluna tombada,
        // 4 anel caído deitado (passarela em curva), 5 cais comprido, 6 torre tombada, 7 calçada de blocos
        static void Link(RuinMesh mesh, int kind, float gap, Vector3 a, Vector3 b, int seed, System.Random random)
        {
            switch (kind)
            {
                case 2: Stones(mesh, a, b, random); break;
                case 3: FallenColumn(mesh.Pick(), a, b); break;
                case 4: RingBridge(mesh, a, b, random); break;
                case 5: Pier(mesh, a, b, random); break;
                case 6: FallenTower(mesh, a, b, random); break;
                case 7: Causeway(mesh, a, b, random); break;
                default: Bridge(mesh, a, b, gap, seed); break;
            }

            s_ConnectionPoints.AddRange(new[] { a, b });
        }

        // Uma cadeia de ilhotas de 'from' até 'to' (centros em células); devolve as pontas da última ligação
        static (Vector3 a, Vector3 b) Chain(RuinMesh mesh, Vector2 from, int[] islets, (int kind, float gap)[] links,
            Vector2 to, int seed, System.Random random)
        {
            Vector2 current = from;
            (Vector3 a, Vector3 b) last = default;
            for (int i = 0; i <= islets.Length; i++)
            {
                Vector2 next = i < islets.Length ? k_Islands[islets[i]].Center : to;
                last = EdgePoints(current, next);
                Link(mesh, links[i].kind, links[i].gap, last.a, last.b, seed + i, random);
                current = next;
            }

            return last;
        }

        // Da Enseada à Cratera (pedras até a primeira ilhota, ponte, anel caído deitado e pedras) e da Cratera ao Farol
        // (ponte, cais comprido, ponte quebrada e a ponte até a praia leste do Farol)
        static void BuildCraterCrossings(RuinMesh mesh, ref Crossing crossing)
        {
            var random = new System.Random(515);
            Island enseada = k_Islands[k_Enseada], first = k_Islands[k_CrossingB], crater = k_Islands[k_Crater];
            Island farol = k_Islands[k_FarolIsland];

            (Vector3 s0, Vector3 s1) = EdgePoints(enseada.Center, first.Center);
            Vector3 outward = Vector3.ProjectOnPlane(s1 - s0, Vector3.up).normalized;
            crossing.EnseadaExit = s0 - outward * 1.5f;
            crossing.EnseadaExitForward = outward;
            Link(mesh, 2, 0f, s0, s1, 510, random);

            (Vector3 c0, Vector3 c1) = Chain(mesh, first.Center, k_CraterRoute,
                new[] { (0, 0f), (4, 0f), (2, 0f), (0, 0f) }, crater.Center, 520, random);
            Vector3 into = Vector3.ProjectOnPlane(c1 - c0, Vector3.up).normalized;
            crossing.CraterDoor = c1 + into * 1.5f;
            crossing.CraterForward = into;

            (Vector3 x0, Vector3 x1) = EdgePoints(CraterPoint(90f, 0.5f),
                k_Islands[k_FarolRoute2[0]].Center);
            Vector3 onward = Vector3.ProjectOnPlane(x1 - x0, Vector3.up).normalized;
            crossing.CraterExit = x0 - onward * 1.5f;
            crossing.CraterExitForward = onward;
            Link(mesh, 0, 0f, x0, x1, 530, random);
            int[] rest = { k_FarolRoute2[1], k_FarolRoute2[2] };
            (Vector3 f0, Vector3 f1) = Chain(mesh, k_Islands[k_FarolRoute2[0]].Center, rest,
                new[] { (5, 0f), (1, 2.5f), (0, 0f) }, farol.Center, 540, random);
            Vector3 toFarol = Vector3.ProjectOnPlane(f1 - f0, Vector3.up).normalized;
            crossing.FarolDoor = f1 + toFarol * 1.5f;
            crossing.FarolForward = toFarol;
        }

        // Do Farol ao Estaleiro (ponte quebrada, torre tombada, pedras e ponte) e do Estaleiro às Ilhotas (coluna
        // tombada, calçada de blocos, ponte, ponte quebrada e a ponte até a ilhota do sudoeste das Ilhotas)
        static void BuildShipyardCrossings(RuinMesh mesh, ref Crossing crossing)
        {
            var random = new System.Random(616);
            Island farol = k_Islands[k_FarolIsland], yard = k_Islands[k_Shipyard];

            (Vector3 f0, Vector3 f1) = EdgePoints(farol.Center + new Vector2(0f, 6f), k_Islands[k_ShipyardRoute[0]].Center);
            Vector3 west = Vector3.ProjectOnPlane(f1 - f0, Vector3.up).normalized;
            crossing.FarolExit = f0 - west * 1.5f;
            crossing.FarolExitForward = west;
            Link(mesh, 1, 3f, f0, f1, 610, random);
            int[] rest = new int[k_ShipyardRoute.Length - 1];
            System.Array.Copy(k_ShipyardRoute, 1, rest, 0, rest.Length);
            (Vector3 y0, Vector3 y1) = Chain(mesh, k_Islands[k_ShipyardRoute[0]].Center, rest,
                new[] { (6, 0f), (2, 0f), (0, 0f) }, yard.Center, 620, random);
            Vector3 into = Vector3.ProjectOnPlane(y1 - y0, Vector3.up).normalized;
            crossing.ShipyardDoor = y1 + into * 1.5f;
            crossing.ShipyardForward = into;

            (Vector3 e0, Vector3 e1) = EdgePoints(yard.Center, k_Islands[k_IsletsRoute[0]].Center);
            Vector3 onward = Vector3.ProjectOnPlane(e1 - e0, Vector3.up).normalized;
            crossing.ShipyardExit = e0 - onward * 1.5f;
            crossing.ShipyardExitForward = onward;
            Link(mesh, 0, 0f, e0, e1, 630, random);
            rest = new int[k_IsletsRoute.Length - 1];
            System.Array.Copy(k_IsletsRoute, 1, rest, 0, rest.Length);
            (Vector3 i0, Vector3 i1) = Chain(mesh, k_Islands[k_IsletsRoute[0]].Center, rest,
                new[] { (3, 0f), (7, 0f), (0, 0f) }, Islet(0).Center, 640, random);
            Vector3 toIslets = Vector3.ProjectOnPlane(i1 - i0, Vector3.up).normalized;
            crossing.IsletsDoor = i1 + toIslets * 1.5f;
            crossing.IsletsForward = toIslets;
        }

        // Anel caído deitado como ponte: um arco de passarela larga em curva de 'a' até 'b', apoiado em pilares
        static void RingBridge(RuinMesh mesh, Vector3 a, Vector3 b, System.Random random)
        {
            s_Crossings.Add((a, b));
            Vector3 flat = b - a;
            flat.y = 0f;
            float length = flat.magnitude;
            Vector3 dir = flat / length;
            float radius = length * 0.8f;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 c = mid + Vector3.Cross(Vector3.up, dir) * Mathf.Sqrt(radius * radius - length * length * 0.25f);
            c.y = Mathf.Max(a.y, b.y) + 0.05f;
            Vector3 u = a - c;
            u.y = 0f;
            u.Normalize();
            Vector3 w = Vector3.Cross(Vector3.up, u);
            Vector3 toB = b - c;
            toB.y = 0f;
            float angle = Mathf.Atan2(Vector3.Dot(toB, w), Vector3.Dot(toB, u)) * Mathf.Rad2Deg;
            float extra = 1.6f / radius * Mathf.Rad2Deg * Mathf.Sign(angle);
            ArquipelagoRuinKit.LyingRing(mesh, c, Vector3.up, u, radius, 4.5f, 2.4f, -extra, angle + extra, random, true,
                true);
            int pillars = Mathf.Max(2, Mathf.RoundToInt(radius * Mathf.Abs(angle) * Mathf.Deg2Rad / 7f));
            for (int i = 1; i < pillars; i++)
            {
                float t = angle * i / pillars * Mathf.Deg2Rad;
                Vector3 p = c + (Mathf.Cos(t) * u + Mathf.Sin(t) * w) * radius;
                float height = c.y - 2.3f - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(p.x, k_SeaBed + height * 0.5f, p.z),
                    new Vector2(2.6f, 2.6f), height, Quaternion.Euler(0f, (float)random.NextDouble() * 90f, 0f), 0.25f);
            }
        }

        // Cais comprido: tabuleiro de lajes baixo sobre estacas de pedra, com meio-fio e frades de amarração
        static void Pier(RuinMesh mesh, Vector3 a, Vector3 b, System.Random random)
        {
            s_Crossings.Add((a, b));
            Vector3 flat = b - a;
            flat.y = 0f;
            Vector3 dir = flat.normalized;
            Vector3 start = a - dir * 1.5f, end = b + dir * 1.5f;
            float length = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(end.x, 0f, end.z));
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Quaternion yaw = Quaternion.LookRotation(dir);
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / 4f));
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments, t1 = (i + 1f) / segments;
                Vector3 p0 = Vector3.Lerp(start, end, t0), p1 = Vector3.Lerp(start, end, t1);
                Vector3 m = (p0 + p1) * 0.5f;
                BlockMesh.ChamferBox(mesh.Pick(), m - Vector3.up * 0.25f, new Vector3(3.6f, 0.5f, length / segments + 0.05f),
                    yaw * Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 2f - 1f), 0.08f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 post = m + side * s * 1.5f;
                    float height = post.y - 0.5f - k_SeaBed;
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(post.x, k_SeaBed + height * 0.5f, post.z),
                        new Vector2(0.8f, 0.8f), height, yaw, 0.1f);
                    BlockMesh.Box(mesh.Pick(), m + side * s * 1.65f + Vector3.up * 0.12f, new Vector3(0.3f, 0.25f,
                        length / segments), yaw);
                    if (i % 2 == 1)
                        BlockMesh.Prism(mesh.Pick(), post + Vector3.up * 0.05f, Vector3.up, 0.6f, 0.22f, 8, 0f);
                }
            }
        }

        // Torre tombada como ponte: fuste largo de 8 lados deitado com uma face plana em cima, cornijas, janelas escuras e
        // o topo partido em dentes na ponta
        static void FallenTower(RuinMesh mesh, Vector3 a, Vector3 b, System.Random random)
        {
            s_Crossings.Add((a, b));
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
            Vector3 start = a - flat * 3f, end = b + flat * 3f;
            Vector3 axis = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            const float radius = 3.4f;
            float apothem = radius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            Vector3 down = Vector3.up * (apothem + 0.05f);
            BlockMesh.Prism(mesh.Pick(), start - down, axis, length, radius, 8, 22.5f);
            Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
            Quaternion along = Quaternion.LookRotation(axis);
            for (float d = 3f; d < length - 2f; d += 6f)
            {
                BlockMesh.Prism(mesh.Pick(), start - down + axis * d, axis, 0.6f, radius * 1.08f, 8, 22.5f);
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.Box(mesh.Dark, start - down + axis * (d + 3f) + side * s * (apothem + 0.02f),
                        new Vector3(0.12f, 1.4f, 0.9f), along);
            }

            for (int k = 0; k < 5; k++)
            {
                float t = k / 5f * Mathf.PI * 2f;
                Vector3 offset = (side * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t)) * (radius * 0.75f);
                BlockMesh.ChamferBox(mesh.Pick(), end - down + offset + axis * (0.6f + (float)random.NextDouble()),
                    new Vector3(1.6f, 1.6f, 1.4f + (float)random.NextDouble()), along, 0.15f);
            }
        }

        // Calçada de blocos: uma estrada de blocos de pedra rente à água, com juntas abertas e um ou outro bloco
        // faltando (os vãos não passam de 2 m)
        static void Causeway(RuinMesh mesh, Vector3 a, Vector3 b, System.Random random)
        {
            s_Crossings.Add((a, b));
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float length = flat.magnitude;
            Vector3 dir = flat / length, side = Vector3.Cross(Vector3.up, dir);
            float s = -0.5f;
            int index = 0;
            while (s < length + 0.5f)
            {
                float block = 2.2f + (float)random.NextDouble() * 0.6f;
                bool missing = index > 0 && index % 4 == 2 && random.NextDouble() < 0.6;
                if (!missing)
                {
                    Vector3 p = a + dir * (s + block * 0.5f) + side * ((float)random.NextDouble() * 0.6f - 0.3f);
                    float top = Mathf.Lerp(a.y, b.y, Mathf.Clamp01(s / length)) + (float)random.NextDouble() * 0.2f - 0.1f;
                    float height = top - k_SeaBed;
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(p.x, k_SeaBed + height * 0.5f, p.z),
                        new Vector2(3.2f, block), height,
                        Quaternion.LookRotation(dir) * Quaternion.Euler(0f, (float)random.NextDouble() * 10f - 5f, 0f),
                        0.15f);
                }

                s += block + (missing ? 1.2f : 0.3f + (float)random.NextDouble() * 0.5f);
                index++;
            }
        }

        // ---------- Ilha da Cratera ----------

        // Ilha em anel: borda alta de 6 m em volta de uma lagoa funda (mar: só se cruza pela ponte ou dando a volta pela
        // borda), terreno de 2,5 m por fora e escadarias para a borda nos quatro lados. A lagoa e a beira da borda alta
        // seguem o contorno ondulado da ilha (frações do raio dela em cada direção), então a borda alta (10 a 35 m de
        // largura) e a faixa de fora (10 a 20 m) nunca afinam.
        const float k_CraterLagoonX = 0.32f, k_CraterLagoonZ = 0.37f, k_CraterRim = 0.66f;

        // Posição de uma célula na ilha da Cratera: 0 no centro, 1 na costa (em cada direção)
        static float CraterRadial(Vector2 cell, out Vector2 q, out float limit)
        {
            Island crater = k_Islands[k_Crater];
            Vector2 d = cell - crater.Center;
            q = new Vector2(d.x / crater.RadiusX, d.y / crater.RadiusZ);
            float angle = Mathf.Atan2(q.y, q.x);
            limit = 1f + 0.12f * Mathf.Sin(3f * angle + crater.Seed) + 0.07f * Mathf.Sin(5f * angle + crater.Seed * 2f);
            return q.magnitude / limit;
        }

        // Célula da Cratera a uma fração 'radial' do caminho do centro à costa, na direção 'angle' (graus)
        static Vector2 CraterPoint(float angle, float radial)
        {
            Island crater = k_Islands[k_Crater];
            float t = angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(t) * crater.RadiusX, Mathf.Sin(t) * crater.RadiusZ);
            float limit = 1f + 0.12f * Mathf.Sin(3f * t + crater.Seed) + 0.07f * Mathf.Sin(5f * t + crater.Seed * 2f);
            return crater.Center + dir * (radial * limit);
        }

        static void ShapeCrater()
        {
            Island crater = k_Islands[k_Crater];
            float high = crater.Terraces[0].Height;
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] != k_Crater + 1)
                    continue;
                float radial = CraterRadial(new Vector2(x + 0.5f, z + 0.5f), out Vector2 q, out float limit);
                // Lagoa: elipse própria (frações do raio da ilha), ondulando com a costa e um pouco mais
                float lagoon = new Vector2(q.x / k_CraterLagoonX, q.y / k_CraterLagoonZ).magnitude / limit;
                if (lagoon < 1f + 0.06f * Mathf.Sin(3f * Mathf.Atan2(q.y, q.x) + 1f))
                {
                    s_Island[x, z] = 0;
                    s_Height[x, z] = k_SeaBed;
                }
                else
                    s_Height[x, z] = radial < k_CraterRim ? high : crater.Height;
            }
        }

        // A ponte sobre a lagoa (de borda a borda, na altura da borda) e ruínas em cima da borda
        static void BuildCrater(RuinMesh mesh)
        {
            var random = new System.Random(808);
            float midRim = (k_CraterLagoonX + k_CraterRim) * 0.5f;
            (Vector3 p0, Vector3 p1) = EdgePoints(CraterPoint(180f, midRim), CraterPoint(0f, midRim));
            Bridge(mesh, p0, p1, 0f, 811);
            s_ConnectionPoints.AddRange(new[] { p0, p1 });
            foreach (float angle in new[] { 40f, 135f, 220f, 315f })
            {
                Vector2 cell = CraterPoint(angle, midRim);
                int x = Mathf.FloorToInt(cell.x), z = Mathf.FloorToInt(cell.y);
                if (!IsLand(x, z))
                    continue;
                Vector3 spot = CellToWorld(cell, HeightAt(x, z));
                if (NearConnection(spot))
                    continue;
                if (angle < 180f)
                    ArquipelagoRuinKit.Column(mesh, spot, 2.5f + (float)random.NextDouble() * 3f, 0.8f, random, true);
                else
                    ArquipelagoRuinKit.AshlarWall(mesh, spot, Quaternion.Euler(0f, angle + 90f, 0f), 5f, 2.4f, 1f,
                        random);
                s_ConnectionPoints.Add(spot);
            }
        }

        // ---------- Estaleiro de Anéis ----------

        // Pátio de pedra onde se constroem anéis colossais (cenário): um anel grande em pé sobre andaimes de pedra na
        // costa oeste, metade em cima do mar, com um guindaste; pelo pátio, pilhas de blocos de anel, aduelas curvas
        // apoiadas em calços, um segundo guindaste e uma rampa de lançamento (o terraço) descendo para o mar
        static void BuildShipyard(RuinMesh mesh)
        {
            var random = new System.Random(909);
            Island yard = k_Islands[k_Shipyard];
            float top = yard.Height;

            // Anel em pé em construção (falta um trecho), com andaimes
            Vector3 ring = CellToWorld(yard.Center + new Vector2(-yard.RadiusX - 4f, 0f), 9f);
            BlockMesh.Tile = k_Cell * 1.6f;
            ArquipelagoRuinKit.RichRing(mesh, ring, Vector3.right, Vector3.forward, 15f, 2.6f, 3.2f, -40f, 220f, random,
                false, true, 0f, 3);
            BlockMesh.Tile = k_Cell;
            for (int k = -2; k <= 2; k++)
            {
                float dz = k * 6f;
                float h = 9f + Mathf.Sqrt(Mathf.Max(0f, 15f * 15f - dz * dz)) - 1.5f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 foot = ring + new Vector3(s * 2.4f, 0f, dz);
                    float height = h - k_SeaBed;
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(foot.x, k_SeaBed + height * 0.5f, foot.z),
                        new Vector2(0.8f, 0.8f), height, Quaternion.identity, 0.1f);
                }

                for (float y = 3f; y < h; y += 4f)
                    BlockMesh.ChamferBox(mesh.Pick(), new Vector3(ring.x, y, ring.z + dz), new Vector3(5.6f, 0.5f, 0.6f),
                        Quaternion.identity, 0.05f);
            }

            // Guindastes: mastro, braço e um bloco pendurado
            void Crane(Vector3 foot, float yaw, float height)
            {
                Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), foot + Vector3.up * (height * 0.5f), new Vector2(1.4f, 1.4f),
                    height, turn, 0.15f);
                Vector3 jib = foot + Vector3.up * (height - 0.6f) + turn * Vector3.forward * 5f;
                BlockMesh.ChamferBox(mesh.Pick(), jib, new Vector3(0.9f, 0.9f, 12f), turn, 0.1f);
                Vector3 hook = foot + turn * Vector3.forward * 10f;
                BlockMesh.Box(mesh.Pick(), hook + Vector3.up * (height - 4f), new Vector3(0.15f, 6f, 0.15f), turn);
                BlockMesh.ChamferBox(mesh.Pick(), hook + Vector3.up * (height - 8f), new Vector3(2.4f, 1.6f, 1.6f),
                    turn * Quaternion.Euler(0f, 0f, 8f), 0.12f);
            }

            Crane(ring + new Vector3(4f, k_SeaBed - 9f, -18f), 20f, 24f);
            Crane(CellToWorld(yard.Center + new Vector2(9f, 6f), top), 200f, 16f);

            // Pilhas de blocos de anel e aduelas curvas em calços (cobertura do pátio)
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + (float)random.NextDouble() * 20f;
                float t = angle * Mathf.Deg2Rad;
                Vector2 cell = yard.Center + new Vector2(Mathf.Cos(t) * yard.RadiusX, Mathf.Sin(t) * yard.RadiusZ) * 0.5f;
                Vector3 spot = CellToWorld(cell, top);
                if (NearConnection(spot))
                    continue;
                Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
                if (i % 2 == 0)
                {
                    for (int k = 0; k < 3; k++)
                        BlockMesh.ChamferBox(mesh.Pick(), spot + Vector3.up * (0.6f + k * 1.15f) + yaw * new Vector3(
                                (float)random.NextDouble() * 0.6f - 0.3f, 0f, 0f), new Vector3(3.2f, 1.1f, 1.8f),
                            yaw * Quaternion.Euler(0f, k * 12f, 0f), 0.15f);
                }
                else
                {
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), spot + Vector3.up * 0.4f + yaw * Vector3.right * 2f,
                        new Vector2(1f, 1.6f), 0.8f, yaw, 0.1f);
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), spot + Vector3.up * 0.4f - yaw * Vector3.right * 2f,
                        new Vector2(1f, 1.6f), 0.8f, yaw, 0.1f);
                    ArquipelagoRuinKit.RichRing(mesh, spot + Vector3.up * 0.8f + yaw * Vector3.forward * 6f,
                        Vector3.up, yaw * Vector3.right, 6f, 1.2f, 1.4f, 60f, 120f, random, false, false, top, 0);
                }

                s_ConnectionPoints.Add(spot);
            }
        }

        // ---------- Cemitério de Anéis ----------

        // Três anéis colossais deitados sobre o mar, um dentro do outro, descendo em degraus até a ilha central
        // (raio do meio da passarela, largura, altura do topo); o do meio tem um trecho quebrado (de 4 m: pulo)
        static readonly (float radius, float width, float top)[] k_CemeteryRings =
        {
            (88f, 18f, 9f), (60f, 14f, 6f), (35f, 12f, 3.5f),
        };

        const float k_CemeteryBreak = 200f;   // onde o anel do meio quebrou (graus; 0 = leste, anti-horário)
        const float k_CemeteryStair = 7.2f;   // largura das escadarias radiais

        static Vector3 CemeteryCenter => CellToWorld(k_Islands[k_Cemetery].Center, 0f);

        // Ponto num anel do Cemitério: ângulo em graus (0 = leste, 90 = norte), na distância 'radius' do centro
        static Vector3 OnCemetery(float angle, float radius, float y)
        {
            float t = angle * Mathf.Deg2Rad;
            return CemeteryCenter + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius + Vector3.up * y;
        }

        // Os anéis (passarelas largas e lisas, canaleta e aros nas laterais), as escadarias entre eles, cobertura em
        // cima das passarelas e os pontos onde os inimigos podem surgir. Devolve os candidatos a spawn.
        static List<Vector3> BuildCemetery(RuinMesh mesh)
        {
            var random = new System.Random(1010);
            Vector3 center = CemeteryCenter;
            BlockMesh.Tile = k_Cell * 1.5f;
            for (int i = 0; i < k_CemeteryRings.Length; i++)
            {
                (float radius, float width, float top) = k_CemeteryRings[i];
                float depth = top - k_SeaBed;
                Vector3 ringCenter = center + Vector3.up * top;
                if (i == 1)
                {
                    float half = 4f / radius * Mathf.Rad2Deg * 0.5f;
                    LyingArc(mesh, ringCenter, radius, width, depth, k_CemeteryBreak + half,
                        k_CemeteryBreak + 360f - half, random, true, true);
                }
                else
                    LyingArc(mesh, ringCenter, radius, width, depth, 0f, 360f, random, false, false);
            }

            BlockMesh.Tile = k_Cell;

            // Escadarias: do anel do meio ao de dentro e do de dentro à ilha, cada nível girado (entre o de fora e o
            // do meio, só os arcos que giram: BuildCemeteryArcs)
            (float from, float to)[] gaps = { (0, 1), (1, 2), (2, 3) };
            float[][] angles = { new float[0], new[] { 90f, 210f + 20f, 330f }, new[] { 0f, 120f, 240f } };
            Island island = k_Islands[k_Cemetery];
            for (int g = 0; g < gaps.Length; g++)
            {
                (float rOuter, float wOuter, float hOuter) = k_CemeteryRings[g];
                float innerEdge = rOuter - wOuter * 0.5f;
                float lowEdge, lowTop;
                if (g < 2)
                {
                    (float rLow, float wLow, float hLow) = k_CemeteryRings[g + 1];
                    lowEdge = rLow + wLow * 0.5f;
                    lowTop = hLow;
                }
                else
                {
                    lowEdge = island.RadiusX * k_Cell * 0.7f;
                    lowTop = island.Height;
                }

                foreach (float angle in angles[g])
                    RadialStairs(mesh, angle, innerEdge + 0.4f, hOuter, lowEdge - 0.4f, lowTop, k_CemeteryStair);
            }

            // Cobertura em cima das passarelas (longe das escadas e da quebra)
            bool NearStair(float angle, int ring)
            {
                foreach (float[] list in angles)
                foreach (float a in list)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(a, angle)) < 14f)
                        return true;
                }

                return ring == 1 && Mathf.Abs(Mathf.DeltaAngle(k_CemeteryBreak, angle)) < 14f ||
                       ring == 0 && Mathf.Abs(Mathf.DeltaAngle(90f, angle)) < 40f;
            }

            var spawns = new List<Vector3>();
            for (int i = 0; i < k_CemeteryRings.Length; i++)
            {
                (float radius, float width, float top) = k_CemeteryRings[i];
                float step = i == 0 ? 30f : i == 1 ? 40f : 60f;
                for (float angle = random.Next(0, 20); angle < 360f; angle += step)
                {
                    if (NearStair(angle, i))
                        continue;
                    float side = random.NextDouble() < 0.5 ? -1f : 1f;
                    Vector3 spot = OnCemetery(angle, radius + side * width * 0.22f, top);
                    Quaternion along = Quaternion.LookRotation(Vector3.Cross(Vector3.up, spot - center).normalized);
                    switch (random.Next(3))
                    {
                        case 0:
                            ArquipelagoRuinKit.Column(mesh, spot, 2f + (float)random.NextDouble() * 2.5f, 0.75f,
                                random, true);
                            break;
                        case 1:
                            ArquipelagoRuinKit.AshlarWall(mesh, spot, along, 3.5f + (float)random.NextDouble() * 1.5f,
                                1.6f + (float)random.NextDouble(), 0.9f, random);
                            break;
                        default:
                            BlockMesh.ChamferBox(mesh.Pick(), spot + Vector3.up * 0.7f, new Vector3(2f, 1.4f, 1.6f),
                                along * Quaternion.Euler(0f, (float)random.NextDouble() * 40f, 0f), 0.15f);
                            break;
                    }

                    // Spawn no outro lado da passarela, ao lado da cobertura
                    spawns.Add(OnCemetery(angle + 6f, radius - side * width * 0.2f, top));
                }
            }

            // Parapeito de balaústres na borda de fora de cada anel, igual ao da ponte: o de fora abre nas duas pontes
            // (entrada ao sul, Ponte Colossal ao norte); o do meio abre a cada 30° para pular nos arcos que giram e na
            // quebra; o de dentro abre onde chegam as escadas do anel do meio
            float stairHalf = k_CemeteryStair * 0.5f + 1f;
            var middleOpenings = new List<(float, float)> { (k_CemeteryBreak, 4f) };
            for (float a = 15f; a < 360f; a += 30f)
                middleOpenings.Add((a, 6f));
            var openings = new[]
            {
                new List<(float, float)> { (90f, k_BridgeWidth * 0.5f - 0.6f), (270f, 5f) },
                middleOpenings,
                new List<(float, float)> { (angles[1][0], stairHalf), (angles[1][1], stairHalf), (angles[1][2], stairHalf) },
            };
            for (int i = 0; i < k_CemeteryRings.Length; i++)
            {
                (float radius, float width, float top) = k_CemeteryRings[i];
                RingBalustrade(mesh, radius + width * 0.5f - 0.5f, top, openings[i]);
            }

            spawns.Add(CemeteryCenter + Vector3.up * island.Height);
            return spawns;
        }

        // Parapeito de balaústres em volta do centro do Cemitério, no raio 'radius' e no topo 'y', com aberturas
        // (ângulo do meio em graus e meia largura em metros)
        static void RingBalustrade(RuinMesh mesh, float radius, float y, List<(float angle, float half)> openings)
        {
            int count = Mathf.CeilToInt(2f * Mathf.PI * radius / 2f);
            bool Open(int k)
            {
                float middle = (k + 0.5f) * 360f / count;
                foreach ((float angle, float half) in openings)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(middle, angle)) < half / radius * Mathf.Rad2Deg)
                        return true;
                }

                return false;
            }

            for (int k = 0; k < count; k++)
            {
                if (Open(k))
                    continue;
                Vector3 p0 = OnCemetery(k * 360f / count, radius, y), p1 = OnCemetery((k + 1) * 360f / count, radius, y);
                Vector3 middle = (p0 + p1) * 0.5f;
                float length = Vector3.Distance(p0, p1) + 0.05f;
                Quaternion along = Quaternion.LookRotation(p1 - p0);
                BlockMesh.Box(mesh.Pick(), p0 + Vector3.up * 0.55f, new Vector3(0.3f, 1.1f, 0.3f), along);
                if (Open((k + 1) % count))
                    BlockMesh.Box(mesh.Pick(), p1 + Vector3.up * 0.55f, new Vector3(0.3f, 1.1f, 0.3f), along);
                BlockMesh.ChamferBox(mesh.Pick(), middle + Vector3.up * 1.2f, new Vector3(0.5f, 0.25f, length), along, 0.05f);
                BlockMesh.Box(mesh.Pick(), middle + Vector3.up * 0.1f, new Vector3(0.6f, 0.2f, length), along);
            }
        }

        // Arco de anel deitado do Cemitério entre dois ângulos (0 = leste, anti-horário)
        static void LyingArc(RuinMesh mesh, Vector3 ringCenter, float radius, float width, float depth, float from,
            float to, System.Random random, bool brokenStart, bool brokenEnd)
        {
            // Com a normal para cima e zero = direita, o ângulo do SweepArc cresce para -z: os ângulos vão invertidos
            ArquipelagoRuinKit.LyingRing(mesh, ringCenter, Vector3.up, Vector3.right, radius, width, depth, -from, -to,
                random, brokenStart, brokenEnd);
        }

        // Escada radial de um anel mais alto (borda de dentro, 'fromRadius', no topo 'fromTop') para baixo até
        // 'toRadius' no topo 'toTop': degraus maciços do fundo do mar, com muretas dos dois lados
        static void RadialStairs(RuinMesh mesh, float angle, float fromRadius, float fromTop, float toRadius,
            float toTop, float width)
        {
            Vector3 start = OnCemetery(angle, fromRadius, fromTop);
            Vector3 inward = (CemeteryCenter - new Vector3(start.x, 0f, start.z)).normalized;
            inward.y = 0f;
            inward.Normalize();
            float run = fromRadius - toRadius;
            int steps = Mathf.Max(2, Mathf.RoundToInt((fromTop - toTop) / 0.27f));
            float rise = (fromTop - toTop) / steps, depth = run / steps;
            Quaternion rotation = Quaternion.LookRotation(inward);
            for (int k = 0; k < steps - 1; k++)
            {
                float y = fromTop - (k + 1) * rise;
                Vector3 c = start + inward * (depth * (k + 0.5f));
                float height = y - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(c.x, k_SeaBed + height * 0.5f, c.z),
                    new Vector2(width, depth + 0.02f), height, rotation, 0.05f);
            }

            // Último degrau: encosta no nível de baixo
            Vector3 lastCenter = start + inward * (depth * (steps - 0.5f));
            float lastHeight = toTop + rise * 0.5f - k_SeaBed;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(lastCenter.x, k_SeaBed + lastHeight * 0.5f, lastCenter.z),
                new Vector2(width, depth + 0.6f), lastHeight, rotation, 0.05f);

            Vector3 right = Vector3.Cross(Vector3.up, inward);
            Vector3 foot = start + inward * run + Vector3.up * (toTop - fromTop);
            StairRamp(start, foot, width);
            s_ConnectionPoints.AddRange(new[] { start, foot });
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 offset = right * s * (width * 0.5f + 0.25f) + Vector3.up * 0.5f;
                Slope(mesh.Pick(), foot + offset, start + offset, 0.5f);
            }
        }

        // ---------- Ilhotas ----------

        // Ligações entre as ilhotas da arena (índices dentro de k_IsletArena..) e o tipo de cada uma: 0 ponte, 1 ponte
        // quebrada (pular ou dash), 2 pedras, 3 coluna tombada
        static readonly (int a, int b, int kind)[] k_IsletLinks =
        {
            (0, 1, 0), (1, 2, 1), (0, 3, 2), (1, 5, 0), (2, 6, 3), (3, 4, 0), (4, 5, 2), (5, 6, 0), (3, 7, 1),
            (5, 9, 0), (6, 9, 2), (7, 8, 0), (8, 10, 3), (9, 11, 0), (10, 11, 1),
        };

        const int k_IsletExit = 11;

        static Island Islet(int index) => k_Islands[k_IsletArena + index];

        // Último ponto de terra de uma ilha andando do centro na direção 'dir' (células), já em metros
        static Vector3 ShorePoint(Island island, Vector2 dir)
        {
            dir.Normalize();
            Vector2 last = island.Center;
            for (float s = 0f; s < 60f; s += 0.25f)
            {
                Vector2 cell = island.Center + dir * s;
                if (!IsLand(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y)))
                    break;
                last = cell;
            }

            return CellToWorld(last, HeightAt(Mathf.FloorToInt(last.x), Mathf.FloorToInt(last.y)));
        }

        // Pedras e colunas partidas para pular entre dois pontos de terra (vãos de 1,5 a 2,5 m)
        static void Stones(RuinMesh mesh, Vector3 s0, Vector3 s1, System.Random random)
        {
            s_Crossings.Add((s0, s1));
            float gap = Vector3.Distance(new Vector3(s0.x, 0f, s0.z), new Vector3(s1.x, 0f, s1.z));
            int count = Mathf.Max(1, Mathf.RoundToInt(gap / 4.5f));
            Vector3 side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(s1 - s0, Vector3.up).normalized);
            for (int k = 0; k < count; k++)
            {
                float t = (k + 1f) / (count + 1f);
                Vector3 foot = Vector3.Lerp(s0, s1, t) + side * ((float)random.NextDouble() * 1.2f - 0.6f);
                float top = Mathf.Lerp(s0.y, s1.y, t) + 0.15f + (float)random.NextDouble() * 0.35f;
                float height = top - k_SeaBed;
                if (k % 2 == 0)
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(foot.x, k_SeaBed + height * 0.5f, foot.z),
                        new Vector2(2.6f, 2.6f), height, Quaternion.Euler(0f, (float)random.NextDouble() * 90f, 0f),
                        0.25f);
                else
                    BlockMesh.Prism(mesh.Pick(), new Vector3(foot.x, k_SeaBed, foot.z), Vector3.up, height, 1.5f, 8,
                        22.5f + (float)random.NextDouble() * 20f);
            }
        }

        // Do Farol às Ilhotas (portão de saída na costa norte do Farol; duas ilhotas com pontes, a segunda quebrada)
        // e das Ilhotas ao Cemitério (portão de saída na ilhota do norte; duas ilhotas e uma rampa de ponte subindo
        // até o anel de fora, onde fica o portão do Cemitério)
        static void BuildIsletCrossings(RuinMesh mesh, ref Crossing crossing)
        {
            Island exit = Islet(k_IsletExit);
            Island u1 = k_Islands[k_IsletsToCemetery], u2 = k_Islands[k_IsletsToCemetery + 1];

            (Vector3 k0, Vector3 k1) = EdgePoints(exit.Center, u1.Center);
            Bridge(mesh, k0, k1, 0f, 94);
            Vector3 onward = Vector3.ProjectOnPlane(k1 - k0, Vector3.up).normalized;
            crossing.IsletsExit = k0 - onward * 1.5f;
            crossing.IsletsExitForward = onward;
            (Vector3 m0, Vector3 m1) = EdgePoints(u1.Center, u2.Center);
            Bridge(mesh, m0, m1, 2.5f, 95);

            // Rampa de ponte da última ilhota até o topo do anel de fora (a ponta do tabuleiro chega na beira do anel,
            // na altura do topo); o portão fica no tabuleiro, logo antes do anel
            (float outerRadius, float outerWidth, float outerTop) = k_CemeteryRings[0];
            float outerEdge = outerRadius + outerWidth * 0.5f;
            Vector3 shore = ShorePoint(u2, Vector2.up);
            Vector3 ring = OnCemetery(270f, outerEdge + 1.5f, outerTop + 0.05f);
            Bridge(mesh, shore, ring, 0f, 96);
            Vector3 door = OnCemetery(270f, outerEdge + 1f, 0f);
            door.y = Mathf.Lerp(shore.y, ring.y, Mathf.InverseLerp(shore.z - 1.5f, ring.z + 1.5f, door.z));
            crossing.CemeteryDoor = door;
            crossing.CemeteryForward = Vector3.forward;
            s_ConnectionPoints.AddRange(new[] { k0, k1, m0, m1, shore, ring });
        }

        // A arena das Ilhotas: as ligações entre as ilhotas (pontes, pontes quebradas, pedras e colunas tombadas) e
        // uma ruína de cobertura em cada ilhota. Devolve os candidatos a spawn.
        static List<Vector3> BuildIsletArena(RuinMesh mesh)
        {
            var random = new System.Random(1212);
            foreach ((int a, int b, int kind) in k_IsletLinks)
            {
                (Vector3 p0, Vector3 p1) = EdgePoints(Islet(a).Center, Islet(b).Center);
                switch (kind)
                {
                    case 0: Bridge(mesh, p0, p1, 0f, 200 + a * 16 + b); break;
                    case 1: Bridge(mesh, p0, p1, 2.5f, 200 + a * 16 + b); break;
                    case 2: Stones(mesh, p0, p1, random); break;
                    default: FallenColumn(mesh.Pick(), p0, p1); break;
                }

                s_ConnectionPoints.AddRange(new[] { p0, p1 });
            }

            var spawns = new List<Vector3>();
            for (int i = 0; i < k_IsletCount; i++)
            {
                Island islet = Islet(i);
                Vector3 center = CellToWorld(islet.Center, islet.Height);
                float reach = Mathf.Min(islet.RadiusX, islet.RadiusZ) * k_Cell;
                Vector3 dir = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Vector3.forward;
                Vector3 spot = center + dir * (reach * 0.25f);
                Quaternion yaw = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
                switch (random.Next(4))
                {
                    case 0:
                        ArquipelagoRuinKit.Column(mesh, spot, 2.2f + (float)random.NextDouble() * 2.5f, 0.8f, random,
                            true);
                        break;
                    case 1:
                        ArquipelagoRuinKit.AshlarWall(mesh, spot, yaw, Mathf.Min(reach * 0.8f, 5.5f),
                            1.8f + (float)random.NextDouble(), 1f, random);
                        break;
                    case 2:
                        ArquipelagoRuinKit.FallenDrums(mesh, spot, yaw, 0.85f, random);
                        break;
                    default:
                        BlockMesh.ChamferBox(mesh.Pick(), spot + Vector3.up * 0.8f, new Vector3(2.2f, 1.6f, 1.8f), yaw,
                            0.2f);
                        BlockMesh.ChamferBox(mesh.Pick(), spot + yaw * new Vector3(1.6f, 0.35f, 0.8f),
                            new Vector3(0.9f, 0.7f, 0.8f), yaw * Quaternion.Euler(10f, 30f, 6f), 0.1f);
                        break;
                }

                spawns.Add(center - dir * (reach * 0.45f));
                s_ConnectionPoints.Add(center);
            }

            return spawns;
        }

        // ---------- Cemitério: arcos girando e anéis gigantes ----------

        // Pedaços de anel levitando no vão entre o anel de fora e o do meio (o vão sem escadas), girando a volta
        // inteira em volta do centro, alinhados com a circunferência (9 m de largura, 1,5 m de vão de cada lado).
        // Ficam 1,5 m acima do anel do meio e 1,5 m abaixo do de fora: dá para atravessar pulando nos dois sentidos.
        static void BuildCemeteryArcs(Transform parent)
        {
            (float outerRadius, float outerWidth, float outerTop) = k_CemeteryRings[0];
            (float midRadius, float midWidth, float midTop) = k_CemeteryRings[1];
            float radius = ((outerRadius - outerWidth * 0.5f) + (midRadius + midWidth * 0.5f)) * 0.5f;
            float top = (outerTop + midTop) * 0.5f;
            const int arcs = 4;
            const float span = 34f;
            for (int i = 0; i < arcs; i++)
            {
                var random = new System.Random(1300 + i);
                string name = $"Destroco_Arco_{i + 1}";
                var root = new GameObject(name);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(CemeteryCenter + Vector3.up * top,
                    Quaternion.Euler(0f, i * 360f / arcs, 0f));

                // Malha em volta do centro do anel (o objeto gira e leva o arco junto)
                var mesh = new RuinMesh(1300 + i);
                ArquipelagoRuinKit.LyingRing(mesh, Vector3.zero, Vector3.up, Vector3.right, radius, 9f, 1.4f, 0f, -span,
                    random, true, true, 0);
                // Pedaços pendurados por baixo
                for (int k = 0; k < 2; k++)
                {
                    float a = -(span * (0.3f + 0.4f * k)) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * radius;
                    BlockMesh.ChamferBox(mesh.Pick(), p + Vector3.down * 1.9f, new Vector3(2.4f, 1.2f, 2f),
                        Quaternion.Euler(8f, (float)random.NextDouble() * 90f, -6f), 0.2f);
                }

                var shift = new Vector3(i * 97f, 0f, i * 53f);
                CreateRuin(name, mesh, s_Materials.StoneTones, root.transform, true, -100f,
                    mossMask: q => RubbleMoss(q + shift));
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                root.AddComponent<OrbitingPlatform>().DegreesPerSecond = 4f;
            }
        }

        // Anéis gigantes em volta do Cemitério (fora do alcance do jogador, sem colisão): uma moldura colossal em pé
        // envolvendo a arena, girando em volta do próprio eixo, anéis em pé quebrados e semiafundados ao redor e
        // montes de anéis caídos uns sobre os outros. O lado sul (por onde se chega) fica livre.
        static void BuildCemeteryGiants(RuinMesh mesh, RuinMesh rubble, Transform parent)
        {
            // Chão de referência acima de tudo: as pontas quebradas não soltam pedaços
            const float k_NoRubble = 1e6f;
            var random = new System.Random(1414);
            Vector3 center = CemeteryCenter;

            // Moldura: anel de 140 m em pé no plano leste-oeste (menor que o portal), com o centro abaixo da água, girando em volta do
            // próprio eixo (objeto próprio, a malha em volta do centro); um trecho partido de 30° mostra o giro. Sem
            // pedaços caídos (cairiam dentro da arena), sem musgo e sem parte molhada (girando, nenhuma face fica
            // sempre para cima nem dentro da água)
            var frame = new GameObject("AnelGirando_Moldura");
            frame.transform.SetParent(parent, false);
            frame.transform.position = center + Vector3.down * 44f;
            var frameMesh = new RuinMesh(1415);
            BlockMesh.Tile = k_Cell * 8f;
            ArquipelagoRuinKit.RichRing(frameMesh, Vector3.zero, Vector3.forward, Vector3.right, 140f, 18f, 20f, 0f,
                330f, random, true, true, k_NoRubble, 3);
            BlockMesh.Tile = k_Cell;
            CreateRuin(frame.name, frameMesh, s_Materials.StoneDarkTones, frame.transform, false, -1000f, 2f);
            foreach (Transform child in frame.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            var spin = frame.AddComponent<SlowRotation>();
            spin.Axis = Vector3.forward;
            spin.DegreesPerSecond = 8f;
            spin.Bob = 0f;

            // Anéis em pé ao redor, virados para o centro, inclinados e semiafundados
            float[] standing = { 18f, 162f, 205f, 340f };
            foreach (float angle in standing)
            {
                float scale = (0.8f + (float)random.NextDouble() * 0.5f) * 2f;
                float radius = (26f + (float)random.NextDouble() * 18f) * scale;
                Vector3 dir = Quaternion.Euler(0f, 90f - angle, 0f) * Vector3.forward;
                Vector3 position = center + dir * (200f + (float)random.NextDouble() * 50f);
                Vector3 normal = Quaternion.Euler(0f, (float)random.NextDouble() * 50f - 25f, 0f) * dir;
                normal = Quaternion.AngleAxis((float)random.NextDouble() * 20f - 10f, Vector3.Cross(normal, Vector3.up)) *
                         normal;
                Vector3 ringCenter = position - Vector3.up * radius * (0.15f + (float)random.NextDouble() * 0.3f);
                float start = -20f + (float)random.NextDouble() * 12f;
                float span = 160f + (float)random.NextDouble() * 100f;
                float thickness = 8f + radius * 0.1f;
                BlockMesh.Tile = k_Cell * Mathf.Max(1f, scale * 1.8f);
                ArquipelagoRuinKit.RichRing(mesh, ringCenter, normal, Vector3.Cross(Vector3.up, normal), radius,
                    thickness, thickness * 0.9f, start, start + span, random);
            }

            // Montes de anéis caídos: dois ou três anéis deitados e tortos, um sobre o outro
            float[] piles = { 30f, 140f, 315f };
            BlockMesh.Tile = k_Cell * 3f;
            foreach (float angle in piles)
            {
                Vector3 dir = Quaternion.Euler(0f, 90f - angle, 0f) * Vector3.forward;
                Vector3 position = center + dir * (144f + (float)random.NextDouble() * 20f);
                int count = 2 + random.Next(2);
                float y = -3f;
                for (int k = 0; k < count; k++)
                {
                    float radius = (18f - k * 4f + (float)random.NextDouble() * 3f) * 2f;
                    Vector3 tilt = Quaternion.Euler((float)random.NextDouble() * 24f - 12f, 0f,
                        (float)random.NextDouble() * 24f - 12f) * Vector3.up;
                    Vector3 shift = new Vector3((float)random.NextDouble() * 12f - 6f, 0f,
                        (float)random.NextDouble() * 12f - 6f);
                    float from = (float)random.NextDouble() * 60f;
                    ArquipelagoRuinKit.LyingRing(rubble, position + shift + Vector3.up * y, tilt,
                        Vector3.Cross(tilt, Vector3.forward), radius, 9f, 7f, from, from + 200f +
                        (float)random.NextDouble() * 120f, random, true, true);
                    y += 6f;
                }
            }

            BlockMesh.Tile = k_Cell;
        }

        // ---------- mar: ilhas avulsas e ruínas ----------

        // Pontes, colunas tombadas e pedras das travessias (de ponta a ponta), para as ruínas no mar ficarem longe
        static readonly List<(Vector3 a, Vector3 b)> s_Crossings = new List<(Vector3, Vector3)>();

        // Ilhas avulsas: centro e raios (células) e altura
        static readonly List<(Vector2 center, float rx, float rz, float height)> s_FarIslands =
            new List<(Vector2, float, float, float)>();

        static readonly List<Vector3> s_SeaRuinSpots = new List<Vector3>();
        static readonly List<Vector3> s_NearGiantSpots = new List<Vector3>();

        const int k_FarIslandCount = 88;     // grupos (uma ilha, ou de 2 a 4 ligadas por pontes)
        const int k_FarIslandGap = 13;       // células (26 m) de mar, no mínimo, até qualquer outra terra

        // Pontes entre ilhas avulsas do mesmo grupo (índices em s_FarIslands; não levam a mais nada)
        static readonly List<(int a, int b, bool broken)> s_FarPairs = new List<(int, int, bool)>();

        // Distância (em células, contando em 8 direções) de cada célula do mapa até a terra mais próxima
        static int[,] LandDistance()
        {
            var distance = new int[k_Size, k_Size];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Height[x, z] > 0f)
                {
                    distance[x, z] = 0;
                    queue.Enqueue(V(x, z));
                }
                else
                    distance[x, z] = int.MaxValue;
            }

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int nx = c.x + dx, nz = c.y + dz;
                    if (!InGrid(nx, nz) || distance[nx, nz] <= distance[c.x, c.y] + 1)
                        continue;
                    distance[nx, nz] = distance[c.x, c.y] + 1;
                    queue.Enqueue(V(nx, nz));
                }
            }

            return distance;
        }

        // Ilhas avulsas pelo mar (cenário): baixas, capim e penhasco como as outras, algumas com um platô. Ficam a
        // 28 m ou mais de qualquer terra (as pontes e pedras das travessias ligam terras, então ficam mais longe ainda),
        // para ninguém chegar nelas pulando, e a no máximo ~130 m, para serem vistas. Fora da área do Cemitério.
        static void PlaceFarIslands()
        {
            s_FarIslands.Clear();
            s_FarPairs.Clear();
            var random = new System.Random(7070);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            int[,] distance = LandDistance();
            Vector2 cemetery = k_Islands[k_Cemetery].Center;
            int groups = 0;

            // Ilha numa elipse (só no mar), com platô às vezes
            void Raise(Vector2 center, float rx, float rz, float height)
            {
                bool plateau = random.NextDouble() < 0.4;
                Vector2 plateauCenter = center + new Vector2(R(-0.3f, 0.3f) * rx, R(-0.3f, 0.3f) * rz);
                float plateauHeight = height + R(2f, 4.5f);
                int seed = 70 + s_FarIslands.Count;
                int r = Mathf.CeilToInt(Mathf.Max(rx, rz) * 1.25f) + 1;
                for (int x = Mathf.FloorToInt(center.x) - r; x <= Mathf.FloorToInt(center.x) + r; x++)
                for (int z = Mathf.FloorToInt(center.y) - r; z <= Mathf.FloorToInt(center.y) + r; z++)
                {
                    var cell = new Vector2(x + 0.5f, z + 0.5f);
                    if (!InGrid(x, z) || s_Island[x, z] != 0 || !InEllipse(cell, center, rx, rz, seed))
                        continue;
                    s_Island[x, z] = k_FarIslands + 1;
                    s_Height[x, z] = plateau && rx > 4f &&
                                     InEllipse(cell, plateauCenter, rx * 0.5f, rz * 0.5f, seed + 10)
                        ? plateauHeight
                        : height;
                }

                s_FarIslands.Add((center, rx, rz, height));
            }

            // Lugar livre: longe o bastante de qualquer terra (ninguém chega pulando nem de dash) e perto o bastante
            // para ser visto, fora do Cemitério
            bool Free(Vector2 center, float reach)
            {
                int x = Mathf.FloorToInt(center.x), z = Mathf.FloorToInt(center.y);
                if (!InGrid(x, z))
                    return false;
                int d = distance[x, z];
                // Nem perto da Ponte Colossal, do templo e do portal (ao norte do Cemitério)
                bool bridge = Mathf.Abs(center.x - cemetery.x) < 60f + reach && center.y > cemetery.y;
                return d >= reach + k_FarIslandGap && d <= reach + 40f && !bridge &&
                       Vector2.Distance(center, cemetery) >= 135f + reach;
            }

            for (int attempt = 0; attempt < 20000 && groups < k_FarIslandCount; attempt++)
            {
                // Tamanhos variados: rochedos pequenos, ilhas médias e algumas grandes
                double size = random.NextDouble();
                float rx = size < 0.35 ? R(2f, 3.5f) : size < 0.85 ? R(3.5f, 6.5f) : R(6.5f, 10f);
                float rz = rx * R(0.7f, 1.3f);
                var center = new Vector2(R(12f, k_Size - 12f), R(45f, k_Size - 12f));
                float reach = Mathf.Max(rx, rz) * 1.25f;
                if (!Free(center, reach))
                    continue;

                // Grupo: 1 ilha (45%), par (30%), tripla (15%) ou quádrupla (10%); cada ilha nova encosta (a 4 a 6,5
                // células de mar) numa do grupo e ganha uma ponte até ela
                double roll = random.NextDouble();
                int count = roll < 0.45 ? 1 : roll < 0.75 ? 2 : roll < 0.9 ? 3 : 4;
                if (count > 1)
                {
                    rx = Mathf.Min(rx, R(3f, 6f));
                    rz = rx * R(0.75f, 1.25f);
                    reach = Mathf.Max(rx, rz) * 1.25f;
                }

                var members = new List<(Vector2 center, float rx, float rz, float reach)> { (center, rx, rz, reach) };
                var links = new List<(int from, int to)>();
                for (int k = 1; k < count; k++)
                {
                    for (int tries = 0; tries < 12; tries++)
                    {
                        int anchor = random.Next(members.Count);
                        float orx = R(2.5f, 5f), orz = orx * R(0.75f, 1.25f), oreach = Mathf.Max(orx, orz) * 1.25f;
                        float angle = R(0f, Mathf.PI * 2f);
                        Vector2 other = members[anchor].center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                            (members[anchor].reach + oreach + R(4f, 6.5f));
                        // Longe da terra de fora e sem encostar nas outras ilhas do grupo
                        if (!Free(other, oreach) ||
                            members.Exists(m => Vector2.Distance(m.center, other) < m.reach + oreach + 3.5f))
                            continue;
                        links.Add((anchor, members.Count));
                        members.Add((other, orx, orz, oreach));
                        break;
                    }
                }

                int first = s_FarIslands.Count;
                for (int k = 0; k < members.Count; k++)
                    Raise(members[k].center, members[k].rx, members[k].rz, k == 0 ? R(2.5f, 6f) : R(2.5f, 5f));
                foreach ((int a, int b) in links)
                    s_FarPairs.Add((first + a, first + b, random.NextDouble() < 0.35));

                groups++;
                distance = LandDistance();
            }
        }

        // Em cima das ilhas avulsas: colunas partidas, um muro ou pedras irregulares
        static void BuildFarIslandDecor(RuinMesh ruins, RuinMesh rocks)
        {
            var random = new System.Random(7171);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);

            // Pontes entre os pares (inteiras ou quebradas)
            for (int i = 0; i < s_FarPairs.Count; i++)
            {
                (int a, int b, bool broken) = s_FarPairs[i];
                (Vector3 p0, Vector3 p1) = EdgePoints(s_FarIslands[a].center, s_FarIslands[b].center);
                if (Vector3.Distance(p0, p1) > 1f)
                    Bridge(ruins, p0, p1, broken ? 2.5f : 0f, 700 + i);
            }

            foreach ((Vector2 center, float rx, float rz, float _) in s_FarIslands)
            {
                // Mais peças nas ilhas maiores; nas grandes, às vezes uma torre partida ou um arco
                int pieces = 1 + random.Next(2) + Mathf.FloorToInt(rx * rz / 14f);
                if (rx > 6f && random.NextDouble() < 0.5)
                {
                    Vector3 middle = CellToWorld(center, s_Height[Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y)]);
                    BlockMesh.Tile = k_Cell * 1.2f;
                    if (random.NextDouble() < 0.5)
                        ArquipelagoRuinKit.Tower(ruins, middle, R(0f, 360f), R(0.35f, 0.5f), random);
                    else
                        ArquipelagoRuinKit.Arch(ruins, middle, R(0f, 360f), R(0.22f, 0.32f), random);
                    BlockMesh.Tile = k_Cell;
                }
                for (int k = 0; k < pieces; k++)
                {
                    Vector2 cell = center + new Vector2(R(-0.45f, 0.45f) * rx, R(-0.45f, 0.45f) * rz);
                    int x = Mathf.FloorToInt(cell.x), z = Mathf.FloorToInt(cell.y);
                    if (!InGrid(x, z) || s_Island[x, z] != k_FarIslands + 1)
                        continue;
                    Vector3 spot = CellToWorld(cell, s_Height[x, z]);
                    switch (random.Next(3))
                    {
                        case 0:
                            ArquipelagoRuinKit.Column(ruins, spot, R(2.5f, 7f), R(0.7f, 1.1f), random, true);
                            break;
                        case 1:
                            ArquipelagoRuinKit.AshlarWall(ruins, spot, Quaternion.Euler(0f, R(0f, 180f), 0f), R(4f, 7f),
                                R(2f, 4f), 1f, random);
                            break;
                        default:
                            ArquipelagoRuinKit.IrregularRock(rocks, spot + Vector3.up * 0.4f, R(1.5f, 3.5f), random);
                            break;
                    }
                }
            }
        }

        // Ruínas no mar perto de onde se anda (de 10 a 70 m da terra, longe das travessias): tocos de coluna, colunas
        // inteiras, pedaços de muro, tambores caídos, arcos e pedaços de anel semiafundados. Sem colisão (cenário).
        // Fora da área do Cemitério, que tem os próprios anéis.
        static void BuildSeaRuins(RuinMesh mesh)
        {
            s_SeaRuinSpots.Clear();
            var random = new System.Random(9191);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            int[,] distance = LandDistance();
            Vector2 cemetery = k_Islands[k_Cemetery].Center;
            for (int attempt = 0; attempt < 8000 && s_SeaRuinSpots.Count < 90; attempt++)
            {
                var cell = new Vector2(R(2f, k_Size - 2f), R(28f, k_Size - 2f));
                int d = distance[Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y)];
                if (d < 5 || d > 35 || Vector2.Distance(cell, cemetery) < 130f)
                    continue;
                Vector3 p = CellToWorld(cell, 0f);
                if (s_SeaRuinSpots.Exists(q => Vector3.Distance(q, p) < 14f) ||
                    s_Crossings.Exists(c => DistanceToSegment(p, c.a, c.b) < 12f))
                    continue;

                float yawDeg = R(0f, 360f);
                Quaternion yaw = Quaternion.Euler(0f, yawDeg, 0f);
                double roll = random.NextDouble();
                if (roll < 0.3)
                    ArquipelagoRuinKit.Column(mesh, p + Vector3.down * R(0.5f, 2f), R(2.5f, 7f), R(0.7f, 1.2f), random,
                        true);
                else if (roll < 0.45)
                    ArquipelagoRuinKit.Column(mesh, p + Vector3.down * R(0.5f, 1.5f), R(6f, 10f), R(0.7f, 1f), random,
                        false);
                else if (roll < 0.6)
                    ArquipelagoRuinKit.AshlarWall(mesh, p + Vector3.down * R(0.5f, 1.5f), yaw, R(4f, 8f), R(2.5f, 5f),
                        R(0.9f, 1.3f), random);
                else if (roll < 0.75)
                    ArquipelagoRuinKit.FallenDrums(mesh, p + Vector3.down * 0.4f, yaw, R(0.8f, 1.3f), random);
                else if (roll < 0.85)
                    ArquipelagoRuinKit.Arch(mesh, p + Vector3.down * R(1f, 3f), yawDeg, R(0.25f, 0.45f), random);
                else
                {
                    float radius = R(6f, 12f);
                    Vector3 normal = yaw * Vector3.forward;
                    float from = R(0f, 60f);
                    ArquipelagoRuinKit.RichRing(mesh, p + Vector3.down * radius * R(0.3f, 0.6f), normal,
                        Vector3.Cross(Vector3.up, normal), radius, R(1.4f, 2.2f), R(1.6f, 2.4f), from,
                        from + R(70f, 150f), random, true, true, 0f);
                }

                s_SeaRuinSpots.Add(p);
            }
        }

        // Poucas ruínas gigantes mais perto (40 a 90 m da terra onde se anda, longe das travessias e umas das outras),
        // menores que as do fundo: anel quebrado, torre, arco, escadaria afundada ou palácio. Sem colisão (cenário).
        static void BuildNearGiants(RuinMesh mesh)
        {
            s_NearGiantSpots.Clear();
            var random = new System.Random(5151);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            int[,] distance = LandDistance();
            Vector2 cemetery = k_Islands[k_Cemetery].Center;
            int[] kinds = { 0, 1, 2, 3, 4, 0, 1 };
            int next = 0;
            for (int attempt = 0; attempt < 6000 && next < kinds.Length; attempt++)
            {
                var cell = new Vector2(R(10f, k_Size - 10f), R(40f, k_Size - 10f));
                int d = distance[Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y)];
                if (d < 20 || d > 45 || Vector2.Distance(cell, cemetery) < 140f)
                    continue;
                Vector3 p = CellToWorld(cell, 0f);
                if (s_NearGiantSpots.Exists(q => Vector3.Distance(q, p) < 90f) ||
                    s_SeaRuinSpots.Exists(q => Vector3.Distance(q, p) < 22f) ||
                    s_Crossings.Exists(c => DistanceToSegment(p, c.a, c.b) < 45f))
                    continue;

                float scale = R(0.45f, 0.7f);
                float yaw = R(0f, 360f);
                BlockMesh.Tile = k_Cell * Mathf.Max(1f, scale * 1.6f);
                switch (kinds[next])
                {
                    case 0: BrokenRing(mesh, p, yaw, scale, random); break;
                    case 1: ArquipelagoRuinKit.Tower(mesh, p, yaw, scale, random); break;
                    case 2: ArquipelagoRuinKit.Arch(mesh, p, yaw, scale, random); break;
                    case 3: SunkenStairs(mesh, p, yaw, scale); break;
                    default: BuildPalace(mesh, p, yaw, scale, random); break;
                }

                BlockMesh.Tile = k_Cell;
                s_NearGiantSpots.Add(p);
                next++;
            }
        }

        // ---------- Ponte Colossal (sala do chefe) ----------

        // Do anel de fora do Cemitério sai, para o norte, uma ponte de 50 m de largura e ~250 m: uma rampa de 60 m
        // subindo da altura do anel (9 m) até 18 m e o tabuleiro plano até um templo em três degraus, com uma escadaria
        // central até o portal. A luta acontece no trecho do meio (k_BridgeFight). Por baixo, pilares e arcos colossais
        // até o mar; nas bordas, parapeito de balaústres e, em pedestais por fora, guardiões encapuzados e obeliscos com
        // anéis se alternando; no chão, faixas e círculos de runas gravadas (sem brilho). O tabuleiro é reto, na altura
        // do anel de fora do Cemitério; no fim, dois lances de escada com patamares sobem até a base do templo (18 m).
        // O portal tem o centro no chão do degrau mais alto do templo (do anel de dentro, o do portal, aparece a metade
        // de cima); são sete anéis, de 215 m a 28 m de raio (todos com um pedaço faltando, menos o de dentro), todos de frente para a ponte, girando em sentidos e velocidades diferentes; quando o
        // chefe morre, aceleram e o centro se enche de luz (PortalActivation).
        const float k_BridgeWidth = 40f, k_BridgeLength = 450f;
        const float k_TempleBase = 18f;                // base do templo (o primeiro degrau sobe dela)
        const float k_BridgeFightStart = 160f, k_BridgeFightEnd = 410f;
        const float k_StairLanding = 10f, k_TempleLanding = 15f;
        const float k_PortalBehind = 46f;               // atrás da frente do templo: meio do degrau mais alto

        static float BridgeStartZ => CemeteryCenter.z + k_CemeteryRings[0].radius + k_CemeteryRings[0].width * 0.5f;
        static float BridgeTop => k_CemeteryRings[0].top + 0.05f;
        static float StairMid => (BridgeTop + k_TempleBase) * 0.5f;

        // Comprimento de um lance do PortalStairs de 'low' a 'high'
        static float StairRun(float low, float high) => Mathf.Max(2, Mathf.RoundToInt((high - low) / 0.27f)) * 0.42f;

        static float TempleFrontZ => BridgeStartZ + k_BridgeLength + StairRun(BridgeTop, StairMid) + k_StairLanding +
                                     StairRun(StairMid, k_TempleBase) + k_TempleLanding;

        static PortalActivation s_Portal;

        static void BuildColossalBridge(RuinMesh mesh, Transform parent, ref Crossing crossing)
        {
            var random = new System.Random(1616);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            float x0 = CemeteryCenter.x, z0 = BridgeStartZ, z2 = z0 + k_BridgeLength;
            float half = k_BridgeWidth * 0.5f;
            crossing.CemeteryExit = new Vector3(x0, k_CemeteryRings[0].top, z0 - 1f);
            crossing.CemeteryExitForward = Vector3.forward;
            s_Crossings.Add((new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z2)));
            s_Crossings.Add((new Vector3(x0 - half, 0f, z0), new Vector3(x0 - half, 0f, z2)));
            s_Crossings.Add((new Vector3(x0 + half, 0f, z0), new Vector3(x0 + half, 0f, z2)));

            float deck = BridgeTop;
            float ringEdge = k_CemeteryRings[0].radius + k_CemeteryRings[0].width * 0.5f;
            float zBack = CemeteryCenter.z + Mathf.Sqrt(ringEdge * ringEdge - half * half) - 2f;
            float joint = deck - 4f - k_SeaBed;
            ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(x0, k_SeaBed + joint * 0.5f, (zBack + z0 + 6f) * 0.5f),
                new Vector2(k_BridgeWidth - 1f, z0 + 6f - zBack), joint, Quaternion.identity, 0.5f);

            // Tabuleiro reto: lajes de 10 m sobre uma laje estrutural grossa
            for (float z = zBack; z < z2; z += 10f)
            {
                float za = z, zb = Mathf.Min(z + 10f, z2);
                var center = new Vector3(x0, deck, (za + zb) * 0.5f);
                float length = zb - za + 0.06f;
                for (int k = 0; k < 4; k++)
                {
                    float x = x0 - half + (k + 0.5f) * (k_BridgeWidth / 4f);
                    BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x, deck - 0.6f, center.z),
                        new Vector3(k_BridgeWidth / 4f + 0.02f, 1.2f, length), Quaternion.identity, 0.06f);
                }

                BlockMesh.ChamferBox(mesh.Pick(), center + Vector3.down * 2.6f, new Vector3(k_BridgeWidth - 1f, 2.8f, length),
                    Quaternion.identity, 0.2f);
            }

            // Pilares até o mar e arcos de aduelas entre eles nas duas faces (o alto dos arcos encosta no tabuleiro)
            const float span = 26f, pier = 6f;
            for (float z = z0 + 14f; z < z2 + 1f; z += span)
            {
                float top = deck - 4f;
                float height = top - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(x0, k_SeaBed + height * 0.5f, z),
                    new Vector2(k_BridgeWidth - 2f, pier), height, Quaternion.identity, 0.5f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(x0, 1.2f, z), new Vector2(k_BridgeWidth + 3f, pier + 3f),
                    2.4f, Quaternion.identity, 0.4f);
                if (z + span > z2 + 1f)
                    break;
                float radius = (span - pier) * 0.5f;
                float crown = deck - 4f;
                foreach (float s in new[] { -1f, 1f })
                    ArquipelagoRuinKit.RichRing(mesh, new Vector3(x0 + s * (half - 2f), crown - radius, z + span * 0.5f),
                        Vector3.right, Vector3.forward, radius, 2.6f, 4f, 0f, 180f, random, false, false, 0f, 3);
            }

            // Parapeito de balaústres nas duas bordas e pedestais por fora com guardiões e obeliscos alternados
            foreach (float s in new[] { -1f, 1f })
                Balustrade(mesh, x0 + s * (half - 0.5f), deck,
                    CemeteryCenter.z + Mathf.Sqrt((ringEdge - 0.5f) * (ringEdge - 0.5f) - (half - 0.5f) * (half - 0.5f)),
                    z2 - 1f);

            int statue = 0;
            for (float z = z0 + 30f; z < z2 - 10f; z += 30f, statue++)
            foreach (float s in new[] { -1f, 1f })
            {
                var foot = new Vector3(x0 + s * (half + 3.5f), deck, z);
                // Pedestal em balanço, preso na face da ponte
                ArquipelagoRuinKit.Pillar(mesh.Pick(), foot + Vector3.down * 2f, new Vector2(6f, 6f), 5f,
                    Quaternion.identity, 0.3f);
                BlockMesh.ChamferBox(mesh.Pick(), foot + Vector3.down * 6f + Vector3.right * s * -1f,
                    new Vector3(4f, 4f, 4f), Quaternion.Euler(0f, 0f, s * 35f), 0.3f);
                if (statue % 2 == 0)
                    Obelisk(mesh, foot + Vector3.up * 0.5f, random);
                else
                    BridgeBrazier(mesh, parent, foot + Vector3.up * 0.5f, statue * 2 + (s > 0 ? 1 : 0), random);
            }

            // Runas gravadas no chão: duas faixas ao longo do caminho e círculos a cada 40 m
            for (float z = z0 + 6f; z < z2 - 2f; z += 4f)
            {
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.Box(mesh.Dark, new Vector3(x0 + s * 7f, deck + 0.01f, z + 1.5f), new Vector3(0.6f, 0.04f, 3f),
                        Quaternion.identity);
            }

            for (float z = z0 + 30f; z < z2 - 15f; z += 40f)
            foreach (float radius in new[] { 9f, 5.5f })
            {
                const int pieces = 28;
                for (int i = 0; i < pieces; i++)
                {
                    float a = (i + 0.5f) / pieces * Mathf.PI * 2f;
                    var p = new Vector3(x0 + Mathf.Cos(a) * radius, deck + 0.01f, z + Mathf.Sin(a) * radius);
                    BlockMesh.Box(mesh.Dark, p, new Vector3(0.5f, 0.04f, radius * Mathf.PI * 2f / pieces + 0.05f),
                        Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                }
            }

            // Fim da ponte: lance até o patamar do meio, lance até o patamar da base do templo (sobre blocos até o mar)
            float run1 = StairRun(deck, StairMid), run2 = StairRun(StairMid, k_TempleBase);
            float zs1 = z2 + run1, zs2 = zs1 + k_StairLanding, zs3 = zs2 + run2, z3 = TempleFrontZ;
            void Block(float from, float to, float top)
            {
                float height = top - k_SeaBed;
                ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(x0, k_SeaBed + height * 0.5f, (from + to) * 0.5f),
                    new Vector2(k_BridgeWidth, to - from + 0.04f), height, Quaternion.identity, 0.5f);
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x0, top - 0.4f, (from + to) * 0.5f),
                    new Vector3(k_BridgeWidth + 1.5f, 0.8f, to - from + 0.04f), Quaternion.identity, 0.15f);
            }

            Block(z2, zs1, deck);
            Block(zs1, zs3, StairMid);
            Block(zs3, z3 + 2f, k_TempleBase);
            PortalStairs(mesh, new Vector3(x0, StairMid, zs1), deck, StairMid, 26f);
            PortalStairs(mesh, new Vector3(x0, k_TempleBase, zs3), StairMid, k_TempleBase, 26f);
            foreach (float s in new[] { -1f, 1f })
            {
                Balustrade(mesh, x0 + s * (half - 0.5f), StairMid, zs1 + 1f, zs2 - 0.5f);
                Balustrade(mesh, x0 + s * (half - 0.5f), k_TempleBase, zs3 + 1f, z3 - 1f);
            }

            BuildPortalTemple(mesh, parent, x0, z3, random);
            s_ConnectionPoints.Add(new Vector3(x0, k_CemeteryRings[0].top, z0));
        }

        // Parapeito de balaústres ao longo de z (de 'from' a 'to'), com corrimão e soleira
        static void Balustrade(RuinMesh mesh, float x, float y, float from, float to)
        {
            for (float z = from; z < to; z += 2f)
            {
                float length = Mathf.Min(2f, to - z) + 0.05f;
                BlockMesh.Box(mesh.Pick(), new Vector3(x, y + 0.55f, z), new Vector3(0.3f, 1.1f, 0.3f), Quaternion.identity);
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x, y + 1.2f, z + length * 0.5f), new Vector3(0.5f, 0.25f, length),
                    Quaternion.identity, 0.05f);
                BlockMesh.Box(mesh.Pick(), new Vector3(x, y + 0.1f, z + length * 0.5f), new Vector3(0.6f, 0.2f, length),
                    Quaternion.identity);
            }
        }

        // Braseiro grande da ponte (~6 m): base em dois degraus, coluna octogonal com colares e um anel encaixado no
        // meio, quatro mãos-francesas segurando a bacia em degraus com aro de anel, cinza no fundo e a chama em sprite
        // azul-esverdeada (SpriteFlame) com luz da mesma cor que tremula
        static void BridgeBrazier(RuinMesh mesh, Transform parent, Vector3 foot, int index, System.Random random)
        {
            Vector3 up = Vector3.up;
            BlockMesh.ChamferBox(mesh.Pick(), foot + up * 0.4f, new Vector3(4.2f, 0.8f, 4.2f), Quaternion.identity, 0.15f);
            BlockMesh.ChamferBox(mesh.Pick(), foot + up * 1.1f, new Vector3(3.2f, 0.6f, 3.2f),
                Quaternion.Euler(0f, 45f, 0f), 0.12f);
            BlockMesh.Prism(mesh.Pick(), foot + up * 1.4f, up, 0.35f, 1.2f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + up * 1.75f, up, 3f, 0.85f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + up * 4.65f, up, 0.35f, 1.15f, 8, 22.5f);
            ArquipelagoRuinKit.RichRing(mesh, foot + up * 3.2f, up, Vector3.right, 1.25f, 0.45f, 0.5f, 0f, 360f, random,
                false, false, 0f, 0);
            // Mãos-francesas: da coluna até a borda de baixo da bacia
            for (int k = 0; k < 4; k++)
            {
                Quaternion around = Quaternion.Euler(0f, 45f + k * 90f, 0f);
                Vector3 from = foot + up * 4.2f + around * Vector3.forward * 0.8f;
                Vector3 to = foot + up * 5.3f + around * Vector3.forward * 1.9f;
                BlockMesh.ChamferBox(mesh.Pick(), (from + to) * 0.5f, new Vector3(0.35f, 0.35f, Vector3.Distance(from, to)),
                    Quaternion.LookRotation(to - from), 0.06f);
            }

            // Bacia em degraus e o aro
            BlockMesh.Prism(mesh.Pick(), foot + up * 5f, up, 0.4f, 1.5f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), foot + up * 5.4f, up, 0.5f, 2.1f, 8, 22.5f);
            ArquipelagoRuinKit.RichRing(mesh, foot + up * 5.95f, up, Vector3.right, 2.15f, 0.4f, 0.45f, 0f, 360f, random,
                false, false, 0f, 4);
            BlockMesh.Prism(mesh.Dark, foot + up * 5.9f, up, 0.04f, 1.85f, 8, 22.5f);

            var root = new GameObject($"Braseiro_Ponte_{index}").transform;
            root.SetParent(parent, false);
            root.position = foot + up * 5.9f;
            var flame = new GameObject("Chama").AddComponent<SpriteFlame>();
            flame.transform.SetParent(root, false);
            flame.Height = 3.4f;
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = up * 1.8f;
            light.type = LightType.Point;
            light.color = new Color(0.3f, 1f, 0.8f);
            light.range = 16f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>().FailuresPerSecond = 0f;
        }

        // Obelisco de três trechos que afinam, com dois anéis de pedra encaixados em volta e o piramídio no topo
        static void Obelisk(RuinMesh mesh, Vector3 foot, System.Random random)
        {
            BlockMesh.ChamferBox(mesh.Pick(), foot + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 4f), Quaternion.identity, 0.15f);
            float y = 1.2f;
            float[] widths = { 2.6f, 2.2f, 1.8f };
            for (int i = 0; i < 3; i++)
            {
                BlockMesh.ChamferBox(mesh.Pick(), foot + Vector3.up * (y + 2.5f), new Vector3(widths[i], 5f, widths[i]),
                    Quaternion.Euler(0f, 45f * i, 0f), 0.12f);
                y += 5f;
            }

            BlockMesh.Prism(mesh.Pick(), foot + Vector3.up * y, Vector3.up, 1.6f, 1.1f, 4, 45f);
            foreach (float h in new[] { 5.5f, 11f })
                ArquipelagoRuinKit.RichRing(mesh, foot + Vector3.up * h, Vector3.up, Vector3.right, 2.4f, 0.7f, 0.8f, 0f,
                    360f, random, false, false, 0f, 0);
        }

        // Templo em três degraus no fim da ponte, com a escadaria central (três lances), colunatas nas laterais e o
        // portal de anéis. O templo é partido em duas metades pelo plano do portal (os anéis que passam por baixo do
        // centro giram no vão entre elas, sem encostar), ligadas no degrau mais alto por uma passarela lisa, sem
        // mureta, que atravessa o anel de dentro.
        const float k_TempleGap = 7.5f;   // meia largura do vão do portal (o aro mais largo dos anéis baixos é 6,1)

        static RuinMesh s_TempleStairs;   // escadas da subida e do templo: sem musgo no topo

        static void BuildPortalTemple(RuinMesh mesh, Transform parent, float x0, float z2, System.Random random)
        {
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            float portalZ = z2 + k_PortalBehind, gapFront = portalZ - k_TempleGap, gapBack = portalZ + k_TempleGap;
            (float width, float depth, float top, float front)[] tiers =
            {
                (120f, 72f, k_TempleBase + 4f, 0f), (100f, 58f, k_TempleBase + 8f, 12f), (80f, 44f, k_TempleBase + 12f, 24f),
            };
            foreach ((float width, float depth, float top, float front) in tiers)
            {
                float height = top - k_SeaBed;
                // As duas metades (de cá e de lá do vão), cada uma com a cornija saliente só nas faces de fora
                foreach ((float from, float to, bool first) in new[]
                {
                    (z2 + front, gapFront, true), (gapBack, z2 + front + depth, false),
                })
                {
                    float middle = (from + to) * 0.5f;
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), new Vector3(x0, k_SeaBed + height * 0.5f, middle),
                        new Vector2(width, to - from), height, Quaternion.identity, 0.8f);
                    float c0 = first ? from - 1f : from, c1 = first ? to : to + 1f;
                    BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x0, top - 0.4f, (c0 + c1) * 0.5f),
                        new Vector3(width + 2f, 0.8f, c1 - c0), Quaternion.identity, 0.2f);
                    // Friso de vãos escuros nas faces viradas para o vão
                    float face = first ? to + 0.05f : from - 0.05f;
                    for (float x = x0 - width * 0.5f + 4f; x < x0 + width * 0.5f - 3f; x += 6f)
                        BlockMesh.Box(mesh.Dark, new Vector3(x, top - 2.2f, face), new Vector3(2.4f, 1.6f, 0.2f),
                            Quaternion.identity);
                }

                // Friso na face da frente
                for (float x = x0 - width * 0.5f + 4f; x < x0 + width * 0.5f - 3f; x += 6f)
                {
                    if (Mathf.Abs(x - x0) < 12f)
                        continue;
                    BlockMesh.Box(mesh.Dark, new Vector3(x, top - 2.2f, z2 + front - 0.05f), new Vector3(2.4f, 1.6f, 0.2f),
                        Quaternion.identity);
                }

                // Escadaria central descendo do degrau: começa na beira da cornija (1 m à frente da face) e desce para o
                // sul, sem degrau entre o último lance e o topo
                float low = top - 4f;
                PortalStairs(mesh, new Vector3(x0, top, z2 + front - 1f), low, top, 20f);
            }

            // Passarela lisa no degrau mais alto, atravessando o anel de dentro (o centro do portal fica no chão dela)
            float walkTop = k_TempleBase + 12f;
            BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x0, walkTop - 0.6f, portalZ),
                new Vector3(10f, 1.2f, k_TempleGap * 2f + 2f), Quaternion.identity, 0.08f);
            BlockMesh.ChamferBox(mesh.Pick(), new Vector3(x0, walkTop - 1.6f, portalZ),
                new Vector3(8f, 0.8f, k_TempleGap * 2f + 1.6f), Quaternion.identity, 0.1f);

            // Colunatas nas laterais do primeiro degrau (fora do vão)
            foreach (float s in new[] { -1f, 1f })
            for (float z = z2 + 6f; z < z2 + 68f; z += 7f)
            {
                if (Mathf.Abs(z - portalZ) < k_TempleGap + 2f)
                    continue;
                bool broken = random.NextDouble() < 0.3;
                ArquipelagoRuinKit.Column(mesh, new Vector3(x0 + s * 55f, k_TempleBase + 4f, z), broken ? R(3f, 6f) : 10f,
                    1.1f, random, broken);
            }

            BuildPortalRings(parent, new Vector3(x0, walkTop, portalZ));
        }

        // Lance de escada de pedra de 'top' (meio da beira de cima) descendo para o sul até 'low': degraus, muretas e a
        // rampa invisível rente às quinas
        static void PortalStairs(RuinMesh mesh, Vector3 top, float low, float high, float width)
        {
            mesh = s_TempleStairs ?? mesh;
            int steps = Mathf.Max(2, Mathf.RoundToInt((high - low) / 0.27f));
            float rise = (high - low) / steps, depth = 0.42f;
            for (int k = 0; k < steps - 1; k++)
            {
                float y = high - (k + 1) * rise;
                Vector3 c = top + Vector3.back * (depth * (k + 0.5f));
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(c.x, (y + low - 0.3f) * 0.5f, c.z),
                    new Vector3(width, y - low + 0.3f, depth), Quaternion.identity, 0.05f);
            }

            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = Vector3.right * s * (width * 0.5f + 0.5f);
                for (int k = 0; k < steps; k++)
                {
                    float y = high - k * rise;
                    Vector3 c = top + Vector3.back * (depth * (k + 0.5f)) + side;
                    BlockMesh.ChamferBox(mesh.Pick(), new Vector3(c.x, (y + 0.7f + low - 0.3f) * 0.5f, c.z),
                        new Vector3(1f, y + 0.7f - low + 0.3f, depth + 0.02f), Quaternion.identity, 0.05f);
                }
            }

            StairRamp(top, top + Vector3.back * (depth * steps) + Vector3.up * (low - high), width);
        }

        // Portal: sete anéis no mesmo plano, de frente para a ponte, com o centro no chão do degrau mais alto do templo
        // (a metade de baixo fica dentro do templo e da água; do anel de dentro, o do portal, aparece a metade de cima);
        // todos menos o de dentro têm um pedaço faltando, e cada um gira num sentido e numa velocidade (objetos próprios, fora do corte por
        // distância), sem musgo nem parte molhada. O núcleo de luz e a luz do centro acendem quando o chefe morre.
        static void BuildPortalRings(Transform parent, Vector3 center)
        {
            (float radius, float thickness, float width, float speed, float gap)[] rings =
            {
                (270f, 26f, 30f, -0.4f, 10f), (215f, 22f, 26f, 0.6f, 12f), (170f, 18f, 22f, -1f, 14f), (130f, 14f, 18f, 1.5f, 16f), (104f, 10f, 13f, -3f, 20f), (82f, 8f, 11f, 5f, 22f), (62f, 7f, 9f, -8f, 26f),
                (44f, 6f, 8f, 12f, 30f), (28f, 5f, 6f, -18f, 0f),
            };
            var root = new GameObject("Portal").transform;
            root.SetParent(parent, false);
            var spins = new List<SlowRotation>();
            for (int i = 0; i < rings.Length; i++)
            {
                var random = new System.Random(1700 + i);
                var ring = new GameObject($"AnelGirando_Portal_{i + 1}").transform;
                ring.SetParent(root, false);
                ring.position = center;
                var mesh = new RuinMesh(1700 + i);
                BlockMesh.Tile = k_Cell * Mathf.Max(1.5f, rings[i].thickness * 0.3f);
                // Começa num ângulo sorteado; o pedaço que falta fica entre o fim e o começo, com as pontas quebradas
                float start = (float)random.NextDouble() * 360f, end = start + 360f - rings[i].gap;
                var zero = new Vector3(Mathf.Cos(start * Mathf.Deg2Rad), Mathf.Sin(start * Mathf.Deg2Rad), 0f);
                ArquipelagoRuinKit.RichRing(mesh, Vector3.zero, Vector3.forward, zero, rings[i].radius,
                    rings[i].thickness, rings[i].width, 0f, 360f - rings[i].gap, random, rings[i].gap > 0f, rings[i].gap > 0f, 1e6f, 3);
                // Dentes salientes na borda de fora (mostram o giro), fora do pedaço que falta
                int teeth = Mathf.RoundToInt(rings[i].radius / 6f);
                for (int k = 0; k < teeth; k++)
                {
                    float a = k * Mathf.PI * 2f / teeth;
                    float offset = Mathf.Repeat(a * Mathf.Rad2Deg - start, 360f);
                    if (rings[i].gap > 0f && (offset < 4f || offset > 360f - rings[i].gap - 4f))
                        continue;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    BlockMesh.ChamferBox(mesh.Pick(), dir * (rings[i].radius + rings[i].thickness * 0.5f + 1f),
                        new Vector3(rings[i].thickness * 0.35f, rings[i].thickness * 0.35f, rings[i].width * 0.8f),
                        Quaternion.LookRotation(Vector3.forward, dir), 0.1f);
                }

                BlockMesh.Tile = k_Cell;
                CreateRuin(ring.name, mesh, s_Materials.StoneTones, ring, false, -1000f, 2f);
                foreach (Transform child in ring.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                var spin = ring.gameObject.AddComponent<SlowRotation>();
                spin.Axis = Vector3.forward;
                spin.DegreesPerSecond = rings[i].speed;
                spin.Bob = 0f;
                spins.Add(spin);
            }

            // Núcleo de luz (apagado até o chefe morrer)
            var core = new MeshData();
            const int sides = 32;
            float coreRadius = rings[rings.Length - 1].radius - 4f;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * coreRadius;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * coreRadius;
                core.AddTriangleAuto(Vector3.zero, p0, p1, Vector3.back);
                core.AddTriangleAuto(Vector3.zero, p1, p0, Vector3.forward);
            }

            var coreRoot = new GameObject("AnelGirando_Portal_Nucleo").transform;
            coreRoot.SetParent(root, false);
            coreRoot.position = center;
            Material glow = GridTextureKit.Surface("Arquipelago_Portal_Luz", PortalGlowPaint(), GridTextureKit.k_NeonCyan);
            GridSceneKit.CreateObject(k_Prefix + "_Portal_Nucleo", "Nucleo", core, glow, coreRoot, false, false, false);
            var light = new GameObject("Luz_Portal").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = center + Vector3.back * 10f;
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.95f, 1f);
            light.range = 160f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            s_Portal = root.gameObject.AddComponent<PortalActivation>();
            s_Portal.Rings = spins.ToArray();
            s_Portal.Core = coreRoot;
            s_Portal.Glow = light;
        }

        static GridTextureKit.Paint PortalGlowPaint()
        {
            GridTextureKit.Paint p = GridTextureKit.Tile(16, 16, 1717, new Color(0.6f, 0.95f, 1f));
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, p.Vary(new Color(0.6f, 0.95f, 1f), 0.08f), true);
            return p;
        }

        // Sala do chefe na ponte: gatilho no trecho do meio, portões da largura da ponte nas duas pontas do trecho, o
        // ponto do chefe perto do templo (virado para quem chega), a loja e o Metrônomo no alto do templo, spawns no
        // tabuleiro e o NavMesh do trecho
        static RoomManager BuildBridgeRoom(Transform parent, out NavMeshSurface surface, out List<Vector3> spawns)
        {
            float x0 = CemeteryCenter.x, z0 = BridgeStartZ;
            float za = z0 + k_BridgeFightStart, zb = z0 + k_BridgeFightEnd;
            var root = new GameObject("Arena_PonteColossal");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(x0, BridgeTop, (za + zb) * 0.5f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(k_BridgeWidth, 30f, zb - za - 8f);
            trigger.center = new Vector3(0f, 10f, 0f);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = "Ponte Colossal";
            RoomDoor Door(string name, float z)
            {
                RoomDoor door = GridSceneKit.CreateDoor(name, V(0, 0), s_Materials.Door, root.transform);
                door.transform.SetPositionAndRotation(new Vector3(x0, BridgeTop, z), Quaternion.identity);
                door.transform.localScale = new Vector3(k_BridgeWidth / (3f * k_Cell), 1f, 1f);
                return door;
            }

            RoomDoor entry = Door("Porta_Entrada", za);
            RoomDoor exit = Door("Porta_Saida", zb);
            exit.StartOpen = false;
            room.Doors = new[] { entry, exit };
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = 1;
            room.ArenaNumber = 16;
            room.ArenaInBiome = 7;
            room.ArenasInBiome = 8;
            room.IsBoss = true;
            var bossPoint = new GameObject("ChefeSpawn").transform;
            bossPoint.SetParent(root.transform, false);
            bossPoint.SetPositionAndRotation(new Vector3(x0, BridgeTop, zb - 25f), Quaternion.LookRotation(Vector3.back));
            room.BossPoint = bossPoint;
            room.BossEnemy = room.Balance ? room.Balance.BossFor(1) : null;
            var reward = new GameObject("Recompensa").transform;
            reward.SetParent(root.transform, false);
            reward.position = new Vector3(x0, k_TempleBase + 12f, TempleFrontZ + 34f);
            room.RewardPoint = reward;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.up * 5f;
            surface.size = new Vector3(k_BridgeWidth + 4f, 30f, zb - za + 4f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            spawns = new List<Vector3>();
            for (float z = za + 25f; z < zb - 10f; z += 16f)
            foreach (float dx in new[] { -13f, 0f, 13f })
                spawns.Add(new Vector3(x0 + dx, BridgeTop, z + (dx == 0f ? 8f : 0f)));
            if (s_Portal)
                s_Portal.Room = room;
            return room;
        }

        // ---------- Enseada ----------

        // Colunata em volta da baía (umas inteiras, outras partidas, tambores caídos) e um arco
        // partido no meio da ilha
        static void BuildEnseada(RuinMesh mesh)
        {
            var random = new System.Random(626);
            Island enseada = k_Islands[k_Enseada];

            Vector3 cove = CellToWorld(k_CoveCenter, enseada.Height);
            for (float angle = 105f; angle <= 255f; angle += 18f)
            {
                var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 spot = cove + dir * 15f;
                int x = Mathf.FloorToInt(spot.x / k_Cell), z = Mathf.FloorToInt(spot.z / k_Cell);
                if (!IsLand(x, z) || !Mathf.Approximately(HeightAt(x, z), enseada.Height) || !Interior(x, z, 1))
                    continue;
                spot.y = enseada.Height;
                bool broken = random.NextDouble() < 0.45;
                ArquipelagoRuinKit.Column(mesh, spot, broken ? 2.5f + (float)random.NextDouble() * 2f : 6.5f, 0.85f,
                    random, broken);
                s_ConnectionPoints.Add(spot);
            }

            Vector3 arch = CellToWorld(new Vector2(126f, 148f), enseada.Height);
            ArquipelagoRuinKit.Arch(mesh, arch, 35f, 0.35f, random);
            s_ConnectionPoints.Add(arch);
        }

        // Escadas dos terraços (ilha, terraço, direção da descida, largura): o paredão da Enseada (para o oeste) e,
        // no Farol, da praia ao platô (sul e leste, para o portão) e do platô ao topo (sul e oeste)
        // (ilha, terraço, direção da descida, largura, de onde andar até a beira em relação ao centro do terraço)
        static readonly (int island, int terrace, Vector2Int dir, float width, Vector2 from)[] k_TerraceStairs =
        {
            (k_Enseada, 0, new Vector2Int(-1, 0), 4f, Vector2.zero),
            (k_FarolIsland, 0, new Vector2Int(0, -1), 4f, Vector2.zero),
            (k_FarolIsland, 0, new Vector2Int(1, 0), 4f, Vector2.zero),
            (k_FarolIsland, 1, new Vector2Int(0, -1), 3.5f, Vector2.zero),
            (k_FarolIsland, 1, new Vector2Int(-1, 0), 3.5f, Vector2.zero),
            // Borda da Cratera: descendo para fora nos quatro lados (o centro é a lagoa: começa no meio da borda)
            (k_Crater, 0, new Vector2Int(-1, 0), 4f, new Vector2(-13f, 0f)),
            (k_Crater, 0, new Vector2Int(1, 0), 4f, new Vector2(10.5f, 0f)),
            (k_Crater, 0, new Vector2Int(0, 1), 4f, new Vector2(0f, 8.8f)),
            (k_Crater, 0, new Vector2Int(0, -1), 4f, new Vector2(0f, -9.8f)),
            // Rampa de lançamento do Estaleiro, descendo para o pátio
            (k_Shipyard, 0, new Vector2Int(1, 0), 4f, Vector2.zero),
        };

        // Onde cada escada dos terraços fica: a última célula alta da faixa do meio, a direção da descida, os dois
        // níveis e a largura
        static readonly List<(Vector2Int edge, Vector2Int dir, float low, float high, float width)> s_StairSites =
            new List<(Vector2Int, Vector2Int, float, float, float)>();

        const float k_StairRise = 0.27f, k_StairDepth = 0.42f;

        // Antes do terreno: a beira dos terraços é curva na grade (em degraus), então em cada escada a beira é cortada
        // em linha reta numa faixa de 10 m (as células altas que passam da linha mais recuada descem para o nível de
        // baixo) e o chão da frente fica plano no nível de baixo em todo o comprimento da escada
        static void PrepareTerraceStairs()
        {
            const int half = 2;
            s_StairSites.Clear();
            foreach ((int index, int terraceIndex, Vector2Int dir, float width, Vector2 from) in k_TerraceStairs)
            {
                Island island = k_Islands[index];
                Terrace terrace = island.Terraces[terraceIndex];
                float high = terrace.Height;
                float low = terraceIndex == 0 ? island.Height : island.Terraces[terraceIndex - 1].Height;
                var side = new Vector2Int(-dir.y, dir.x);
                var center = new Vector2Int(Mathf.FloorToInt(terrace.Center.x + from.x),
                    Mathf.FloorToInt(terrace.Center.y + from.y));
                int steps = Mathf.Max(2, Mathf.RoundToInt((high - low) / k_StairRise));
                int runCells = Mathf.CeilToInt(k_StairDepth * steps / k_Cell) + 1;

                // Quantas células altas há depois de 'cell' na direção 'dir' até a beira
                int Edge(Vector2Int cell)
                {
                    for (int n = 0; n < 60; n++, cell += dir)
                    {
                        if (HeightAt(cell.x + dir.x, cell.y + dir.y) < high - 0.01f)
                            return n;
                    }

                    return 60;
                }

                int edge = int.MaxValue;
                for (int k = -half; k <= half; k++)
                    edge = Mathf.Min(edge, Edge(center + side * k));

                for (int k = -half; k <= half; k++)
                {
                    Vector2Int lane = center + side * k;
                    for (int r = edge + 1; r <= edge + 60; r++)
                    {
                        Vector2Int cell = lane + dir * r;
                        bool cutting = HeightAt(cell.x, cell.y) >= high - 0.01f;
                        if (!cutting && r > edge + runCells)
                            break;
                        // Corta o que passa da linha; na frente da escada, o chão fica no nível de baixo (sem mar)
                        if (cutting || !Mathf.Approximately(HeightAt(cell.x, cell.y), low))
                        {
                            s_Height[cell.x, cell.y] = low;
                            s_Island[cell.x, cell.y] = index + 1;
                        }
                    }
                }

                s_StairSites.Add((center + dir * edge, dir, low, high, width));
            }
        }

        static void BuildTerraceStairs(RuinMesh mesh)
        {
            foreach (var site in s_StairSites)
                TerraceStairs(mesh, site.edge, site.dir, site.low, site.high, site.width);
        }

        // Escada que desce da beira reta de um terraço ('edge' = última célula alta, PrepareTerraceStairs) até o nível
        // de baixo: degraus de 0,27 m com o bocel chanfrado, muretas inclinadas dos dois lados e a rampa invisível rente
        // aos degraus
        static void TerraceStairs(RuinMesh mesh, Vector2Int edge, Vector2Int dir, float low, float high, float width)
        {
            int steps = Mathf.Max(2, Mathf.RoundToInt((high - low) / k_StairRise));
            float rise = (high - low) / steps, depth = k_StairDepth;
            // A escada começa exatamente na beira (a face da célula)
            Vector2 edgeCell = new Vector2(edge.x + 0.5f, edge.y + 0.5f) + (Vector2)dir * 0.5f;
            Vector3 start = CellToWorld(edgeCell, high);
            var forward = new Vector3(dir.x, 0f, dir.y);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Quaternion rotation = Quaternion.LookRotation(right, Vector3.up);
            for (int k = 0; k < steps - 1; k++)
            {
                float y = high - (k + 1) * rise;
                float bottom = low - 0.3f;
                Vector3 c = start + forward * (depth * (k + 0.5f));
                BlockMesh.ChamferBox(mesh.Pick(), new Vector3(c.x, (y + bottom) * 0.5f, c.z),
                    new Vector3(depth, y - bottom, width), rotation, 0.06f);
            }

            StairRamp(start, start + forward * (depth * steps) + Vector3.up * (low - high), width);

            // Muretas: do topo ao pé da escada, 0,5 m acima da linha dos degraus
            Vector3 foot = start + forward * (depth * (steps - 1)) + Vector3.up * (low - high);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 offset = right * s * (width * 0.5f + 0.25f) + Vector3.up * 0.5f;
                Slope(mesh.Pick(), foot + offset, start + offset, 0.5f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), foot + right * s * (width * 0.5f + 0.25f) + Vector3.up * 0.45f,
                    new Vector2(0.7f, 0.7f), 0.9f, rotation, 0.1f);
            }

            s_ConnectionPoints.AddRange(new[] { start, foot });
        }

        // Rampa invisível (só colisor) rente às quinas dos degraus: a subida fica lisa, sem o tranco de cada degrau, e o
        // NavMesh também a usa. 'top' e 'bottom' são as pontas da linha que passa pelas quinas, no meio da escada; a
        // caixa fica toda abaixo dessa linha. 'extra' alonga um pouco a caixa (trechos em curva se sobrepõem).
        static void StairRamp(Vector3 top, Vector3 bottom, float width, float extra = 0f)
        {
            const float thickness = 0.3f;
            Vector3 along = top - bottom;
            Vector3 right = Vector3.Cross(Vector3.up, along).normalized;
            Vector3 normal = Vector3.Cross(along, right).normalized;
            var ramp = new GameObject("Rampa_Escada");
            ramp.transform.SetParent(s_StairRamps, false);
            ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f - normal * (thickness * 0.5f),
                Quaternion.LookRotation(along, normal));
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, thickness, along.magnitude + extra);
        }

        // ---------- Farol: a torre ----------

        // Farol no topo: base, fuste de pedra em três trechos que afinam (com frestas escuras), escada em
        // espiral por fora até a varanda (a 14 m, com parapeito, mísulas e uma abertura onde a escada chega), a
        // lanterna (pilares e telhado em degraus) e o feixe que gira (LighthouseBeam, com dois spots)
        static void BuildLighthouse(RuinMesh mesh, Transform parent)
        {
            Island farol = k_Islands[k_FarolIsland];
            Terrace top = farol.Terraces[1];
            Vector3 c = CellToWorld(top.Center, top.Height);
            var random = new System.Random(717);
            const float balcony = 14f;

            // Fuste: (de, até, raio)
            // Fuste: (de, até, raio dos cantos do octógono; as faces ficam a 0,92 dele). Sem cintas salientes, que
            // bateriam na cabeça de quem sobe a escada
            (float y0, float y1, float r)[] shaft = { (-0.3f, 4.8f, 3.6f), (4.8f, 9.6f, 3.35f), (9.6f, balcony - 0.1f, 3.1f) };
            float RadiusAt(float y) => (y < 4.8f ? 3.6f : y < 9.6f ? 3.35f : 3.1f) * 0.92f;
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * -0.3f, Vector3.up, 0.52f, 4.8f, 8, 22.5f);
            foreach ((float y0, float y1, float r) in shaft)
                BlockMesh.Prism(mesh.Pick(), c + Vector3.up * y0, Vector3.up, y1 - y0, r, 8, 22.5f);
            // Frestas no meio das faces (alternando de face a cada andar)
            foreach (float y in new[] { 3f, 7.6f, 11.8f })
            for (int k = 0; k < 4; k++)
            {
                Quaternion around = Quaternion.Euler(0f, k * 90f + (y > 7f && y < 10f ? 45f : 0f), 0f);
                BlockMesh.Box(mesh.Dark, c + Vector3.up * y + around * Vector3.forward * RadiusAt(y),
                    new Vector3(0.5f, 1.3f, 0.2f), around);
            }

            // Escada em espiral: degraus em balanço presos no fuste, subindo no sentido anti-horário a partir do sul
            const float stepRise = 0.25f, stepRun = 0.5f, stepWidth = 1.7f;
            float angle = 180f;
            int count = Mathf.RoundToInt((balcony - 0.22f) / stepRise) - 1;
            float lastAngle = angle;
            var noses = new List<Vector3>();   // quina de baixo de cada degrau (para a rampa invisível)
            for (int k = 0; k < count; k++)
            {
                float y = 0.22f + (k + 1) * stepRise;
                float r = RadiusAt(y) + stepWidth * 0.5f;
                Quaternion around = Quaternion.Euler(0f, angle, 0f);
                Vector3 radial = around * Vector3.forward;
                BlockMesh.ChamferBox(mesh.Pick(), c + radial * r + Vector3.up * (y - 0.175f),
                    new Vector3(0.62f, 0.35f, stepWidth), around, 0.05f);
                if (k % 3 == 0)
                    BlockMesh.Box(mesh.Pick(), c + radial * (r + stepWidth * 0.45f) + Vector3.up * (y + 0.5f),
                        new Vector3(0.15f, 1f, 0.15f), around);
                noses.Add(c + Quaternion.Euler(0f, angle - 0.31f / r * Mathf.Rad2Deg, 0f) * Vector3.forward * r +
                          Vector3.up * y);
                lastAngle = angle;
                angle += stepRun / r * Mathf.Rad2Deg;
            }

            // Rampa invisível em trechos retos de quina em quina, a partir de um degrau antes do primeiro (na base)
            noses.Insert(0, noses[0] * 2f - noses[1]);
            for (int k = 0; k + 1 < noses.Count; k++)
                StairRamp(noses[k + 1], noses[k], stepWidth, 0.06f);

            // Varanda: piso em 8 trechos, sem os que ficam em cima do último trecho da escada (por onde se sobe; a
            // abertura cobre os degraus acima de ~10,5 m, que não teriam espaço para a cabeça sob o piso e as mísulas)
            bool Opening(float a0, float a1)
            {
                float from = lastAngle - 100f, to = lastAngle + 12f;
                for (float a = a0; a <= a1; a += 3f)
                {
                    float d = Mathf.DeltaAngle(a, (from + to) * 0.5f);
                    if (Mathf.Abs(d) <= (to - from) * 0.5f)
                        return true;
                }

                return false;
            }

            // Onde a escada passa, só a faixa de dentro (em cima dos degraus) fica aberta; a borda de fora da varanda
            // continua inteira, e a escada chega sem vão no piso
            const float inner = 2.6f, outer = 6.2f, slot = 4.75f;
            for (int s = 0; s < 8; s++)
            {
                float a0 = s * 45f + 22.5f, a1 = a0 + 45f;
                if (Opening(a0, a1))
                {
                    for (float b0 = a0; b0 < a1 - 0.01f; b0 += 7.5f)
                    {
                        if (!Opening(b0, b0 + 7.5f))
                            BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony - 0.25f), Vector3.up,
                                Vector3.forward, (inner + slot) * 0.5f, BlockMesh.Chamfered(slot - inner, 0.5f, 0.08f),
                                b0, b0 + 7.5f, 1);
                    }

                    BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony - 0.25f), Vector3.up, Vector3.forward,
                        (slot + outer) * 0.5f, BlockMesh.Chamfered(outer - slot, 0.5f, 0.08f), a0, a1, 2);
                    BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony + 0.5f), Vector3.up, Vector3.forward,
                        outer - 0.15f, BlockMesh.Chamfered(0.3f, 1f, 0.05f), a0, a1, 2);
                    continue;
                }

                BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony - 0.25f), Vector3.up, Vector3.forward,
                    (inner + outer) * 0.5f, BlockMesh.Chamfered(outer - inner, 0.5f, 0.08f), a0, a1, 2);
                BlockMesh.SweepArc(mesh.Pick(), c + Vector3.up * (balcony + 0.5f), Vector3.up, Vector3.forward,
                    outer - 0.15f, BlockMesh.Chamfered(0.3f, 1f, 0.05f), a0, a1, 2);
                Quaternion around = Quaternion.Euler(0f, (a0 + a1) * 0.5f, 0f);
                BlockMesh.ChamferBox(mesh.Pick(), c + around * Vector3.forward * 4.3f + Vector3.up * (balcony - 1f),
                    new Vector3(0.6f, 1f, 2.2f), around, 0.1f);
            }

            // Lanterna e telhado; no meio, o pedestal da lente (a lente gira com o feixe)
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * (balcony - 0.05f), Vector3.up, 0.45f, 2.9f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * (balcony + 0.4f), Vector3.up, 0.5f, 0.75f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * (balcony + 0.85f), Vector3.up, 0.12f, 1.05f, 8, 22.5f);
            BlockMesh.Prism(mesh.Pick(), c + Vector3.up * (balcony + 2.85f), Vector3.up, 0.15f, 1.05f, 8, 22.5f);
            for (int k = 0; k < 8; k++)
            {
                Quaternion around = Quaternion.Euler(0f, k * 45f + 22.5f, 0f);
                BlockMesh.Box(mesh.Pick(), c + around * Vector3.forward * 2.5f + Vector3.up * (balcony + 1.9f),
                    new Vector3(0.35f, 3f, 0.35f), around);
            }

            (float y0, float h, float r)[] roof = { (17.4f, 0.5f, 3.2f), (17.9f, 0.7f, 2.4f), (18.6f, 0.7f, 1.5f),
                (19.3f, 1f, 0.6f) };
            foreach ((float y0, float h, float r) in roof)
                BlockMesh.Prism(mesh.Pick(), c + Vector3.up * y0, Vector3.up, h, r, 8, 22.5f);
            ArquipelagoRuinKit.HangVine(c + Vector3.up * (balcony - 0.5f) + Vector3.back * outer, Vector3.back, random,
                1.4f);
            ArquipelagoRuinKit.HangVine(c + Vector3.up * 9.5f + Vector3.left * (RadiusAt(9f) + 0.05f), Vector3.left,
                random, 1.2f);

            // Feixe e luzes
            var beam = new GameObject("Feixe_Farol");
            beam.transform.SetParent(parent, false);
            beam.transform.position = c + Vector3.up * (balcony + 1.9f);
            beam.AddComponent<LighthouseBeam>();
            // Lente: prisma de vidro em faixas (Fresnel), acesa, entre o pedestal e a tampa
            var lens = new MeshData();
            BlockMesh.Prism(lens, Vector3.up * -0.93f, Vector3.up, 1.86f, 0.95f, 8, 0f);
            Material lensGlow = GridTextureKit.Surface("Arquipelago_Farol_Lente",
                GlowPaint(new Color(1f, 0.93f, 0.7f), 2020, true), new Color(1f, 0.85f, 0.55f) * 1.8f);
            GridSceneKit.CreateObject(k_Prefix + "_Farol_Lente", "Lente", lens, lensGlow, beam.transform, false, false,
                false);
            var cone = new GameObject("Cone");
            cone.transform.SetParent(beam.transform, false);
            cone.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
            var renderer = cone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BeamMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (float s in new[] { -1f, 1f })
            {
                var spot = new GameObject("Luz").AddComponent<Light>();
                spot.transform.SetParent(beam.transform, false);
                spot.transform.localRotation = Quaternion.LookRotation(Vector3.right * s);
                spot.type = LightType.Spot;
                spot.range = 90f;
                spot.spotAngle = 12f;
                spot.intensity = 6f;
                spot.color = new Color(0.95f, 0.93f, 0.8f);
                spot.shadows = LightShadows.None;
            }

            var lamp = new GameObject("Lampada").AddComponent<Light>();
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = beam.transform.position;
            lamp.type = LightType.Point;
            lamp.range = 14f;
            lamp.intensity = 3f;
            lamp.color = new Color(0.95f, 0.93f, 0.8f);
            lamp.shadows = LightShadows.None;
            s_ConnectionPoints.Add(c);
        }

        // Feixe: dois cones de 8 lados para os dois lados (eixo x), claros perto da lanterna e sumindo na ponta
        static Mesh BeamMesh()
        {
            IcaroAssetUtility.EnsureFolder(GridSceneKit.MeshFolder);
            string path = $"{GridSceneKit.MeshFolder}/{k_Prefix}_FeixeFarol.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = k_Prefix + "_FeixeFarol" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int sides = 8;
            foreach (float s in new[] { -1f, 1f })
            {
                int start = vertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float t = i * Mathf.PI * 2f / sides;
                    var ring = new Vector3(0f, Mathf.Cos(t), Mathf.Sin(t));
                    vertices.Add(new Vector3(s * 0.8f, 0f, 0f) + ring * 0.35f);
                    vertices.Add(new Vector3(s * 110f, 0f, 0f) + ring * 7f);
                    colors.Add(new Color(1f, 1f, 1f, 0.32f));
                    colors.Add(new Color(1f, 1f, 1f, 0f));
                }

                for (int i = 0; i < sides; i++)
                {
                    int a = start + i * 2, b = start + ((i + 1) % sides) * 2;
                    triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
                }
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

        // Aditivo, dos dois lados, na cor da luz do farol (a transparência vem das cores dos vértices)
        static Material BeamMaterial()
        {
            Material material = IcaroMaterials.GetOrCreateAdditiveParticle(k_Prefix + "_FeixeFarol", null);
            material.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.78f, 1f));
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- ruínas de cobertura ----------

        // Muros de blocos aparelhados, colunas partidas, pilhas de blocos e tambores caídos nas ilhas de combate.
        // Devolve as posições (para a decoração e os spawns evitarem).
        static List<Vector3> BuildCover(RuinMesh mesh)
        {
            var random = new System.Random(1234);
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < 40000 && placed.Count < 40; attempt++)
            {
                int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                int island = s_Island[x, z];
                // As Ilhotas e o Cemitério montam a própria cobertura
                if (island == 0 || k_Islands[island - 1].Arena == 0 || k_Islands[island - 1].Arena == 4 ||
                    k_Islands[island - 1].Arena == 5 ||
                    !IsLand(x, z) || s_Ramp[x, z] ||
                    !Interior(x, z, 3))
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z];
                if (placed.Exists(p => Vector3.Distance(p, position) < 8f) || NearConnection(position))
                    continue;

                Quaternion yaw = Quaternion.Euler(0f, random.Next(0, 4) * 90f + (float)random.NextDouble() * 20f - 10f, 0f);
                switch (random.Next(4))
                {
                    case 0:
                        ArquipelagoRuinKit.AshlarWall(mesh, position, yaw, 5f + (float)random.NextDouble() * 3f,
                            2.5f + (float)random.NextDouble() * 1.5f, 1.2f, random);
                        break;
                    case 1:
                        ArquipelagoRuinKit.Column(mesh, position, 2.5f + (float)random.NextDouble() * 3f, 1.1f, random,
                            true);
                        break;
                    case 2: // pilha de blocos
                        BlockMesh.ChamferBox(mesh.Pick(), position + Vector3.up * 0.9f, new Vector3(2.4f, 1.8f, 2f), yaw,
                            0.2f);
                        BlockMesh.ChamferBox(mesh.Pick(), position + yaw * new Vector3(0.5f, 2.4f, 0.2f),
                            new Vector3(1.6f, 1.2f, 1.6f), yaw * Quaternion.Euler(0f, 25f, 0f), 0.15f);
                        BlockMesh.ChamferBox(mesh.Pick(), position + yaw * new Vector3(-1.9f, 0.35f, 0.9f),
                            new Vector3(0.9f, 0.7f, 0.8f), yaw * Quaternion.Euler(12f, 40f, 8f), 0.1f);
                        ArquipelagoRuinKit.HangVine(position + yaw * new Vector3(0f, 1.8f, 1.04f), yaw * Vector3.forward,
                            random, 0.6f);
                        break;
                    default:
                        ArquipelagoRuinKit.FallenDrums(mesh, position, yaw, 1f, random);
                        break;
                }

                placed.Add(position);
            }

            return placed;
        }

        // Célula com 'margin' células de chão da mesma ilha e na mesma altura em volta (longe da beira e das rampas)
        static bool Interior(int x, int z, int margin)
        {
            float h = s_Height[x, z];
            for (int dx = -margin; dx <= margin; dx++)
            for (int dz = -margin; dz <= margin; dz++)
            {
                int nx = x + dx, nz = z + dz;
                if (!Mathf.Approximately(HeightAt(nx, nz), h) || s_Ramp[Mathf.Clamp(nx, 0, k_Size - 1), Mathf.Clamp(nz, 0, k_Size - 1)])
                    return false;
            }

            return true;
        }

        static readonly List<Vector3> s_ConnectionPoints = new List<Vector3>();

        static bool NearConnection(Vector3 position)
        {
            foreach (Vector3 point in s_ConnectionPoints)
            {
                Vector3 d = point - position;
                d.y = 0f;
                if (d.magnitude < 9f)
                    return true;
            }

            return false;
        }

        // ---------- gigantes no mar ----------

        // Estruturas colossais no mar, em três faixas de distância (contadas do meio das ilhas):
        // - perto (40 a 140 m além da borda das ilhas): pedra com luz normal, musgo, base molhada e trepadeiras, onde
        //   dá para ver o detalhe;
        // - meio (140 a 410 m) e fundo (410 a 880 m): material de silhueta (vultos na névoa que nunca somem de vez),
        //   cada vez maiores para continuarem legíveis.
        // Anéis quebrados, torres partidas, arcos, escadarias que descem para a água e palácios afundados. A textura
        // cresce com a peça (BlockMesh.Tile), para os blocos não parecerem azulejo.
        static void BuildGiants(RuinMesh near, RuinMesh far)
        {
            var random = new System.Random(4321);
            // Contadas do meio das ilhas (sem o continente), a partir da borda delas
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                // Só as ilhas do sul (as Ilhotas e o Cemitério ficam de fora: têm os próprios gigantes)
                if (s_Island[x, z] == 0 || s_Island[x, z] == k_Mainland + 1 || s_Island[x, z] > k_Cemetery)
                    continue;
                low = Vector2.Min(low, new Vector2(x, z));
                high = Vector2.Max(high, new Vector2(x + 1, z + 1));
            }

            Vector3 middle = CellToWorld((low + high) * 0.5f, 0f);
            float reach = Vector2.Distance(low, high) * 0.5f * k_Cell;
            // As Ilhotas e o Cemitério (com os gigantes dele) também afastam os gigantes
            var placed = new List<(Vector3 position, float radius)>
            {
                (CellToWorld(new Vector2(155f, 450f), 0f), 140f), (CemeteryCenter, 420f),
            };
            // As fases e travessias ao norte (as ilhas do sul já contam pela distância mínima)
            for (int i = k_Cemetery + 1; i < k_Islands.Length; i++)
            {
                Island island = k_Islands[i];
                if (island.Center.x > -900f && i != k_FarIslands)
                    placed.Add((CellToWorld(island.Center, 0f), Mathf.Max(island.RadiusX, island.RadiusZ) * k_Cell + 70f));
            }

            placed.Add((CellToWorld(k_Islands[k_FarolIsland].Center, 0f), 120f));
            for (float z = BridgeStartZ; z <= TempleFrontZ + 60f; z += 40f)
                placed.Add((new Vector3(CemeteryCenter.x, 0f, z), 90f));
            placed.Add((new Vector3(CemeteryCenter.x, 0f, TempleFrontZ + k_PortalBehind), 330f));
            foreach ((Vector2 center, float rx, float rz, float _) in s_FarIslands)
                placed.Add((CellToWorld(center, 0f), Mathf.Max(rx, rz) * k_Cell * 1.4f));
            foreach (Vector3 spot in s_SeaRuinSpots)
                placed.Add((spot, 12f));
            foreach (Vector3 spot in s_NearGiantSpots)
                placed.Add((spot, 50f));
            GridSceneKit.Cards vines = ArquipelagoRuinKit.Vines;

            // (malha, distância mínima, máxima, escala, quantidade de cada peça: anéis, torres, arcos, escadas, palácios)
            var bands = new (RuinMesh mesh, float min, float max, float scale, int rings, int towers, int arches, int stairs, int palaces)[]
            {
                (near, reach + 40f, reach + 140f, 0.8f, 6, 4, 3, 2, 2),
                (far, reach + 140f, reach + 410f, 1.3f, 8, 7, 4, 2, 2),
                (far, reach + 410f, reach + 880f, 2.4f, 7, 6, 3, 0, 2),
            };

            foreach (var band in bands)
            {
                // Trepadeiras só nas peças de perto (as silhuetas têm um material só)
                ArquipelagoRuinKit.Vines = band.mesh == near ? vines : null;
                var kinds = new List<int>();
                for (int i = 0; i < band.rings; i++) kinds.Add(0);
                for (int i = 0; i < band.towers; i++) kinds.Add(1);
                for (int i = 0; i < band.arches; i++) kinds.Add(2);
                for (int i = 0; i < band.stairs; i++) kinds.Add(3);
                for (int i = 0; i < band.palaces; i++) kinds.Add(4);
                // Embaralha para os tipos se misturarem em volta
                for (int i = kinds.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
                }

                float step = 360f / kinds.Count;
                float offset = (float)random.NextDouble() * step;
                for (int k = 0; k < kinds.Count; k++)
                {
                    float footprint = (kinds[k] == 4 ? 60f : kinds[k] == 0 ? 45f : 25f) * band.scale;
                    Vector3 position = Vector3.zero;
                    float yaw = 0f;
                    bool found = false;
                    for (int attempt = 0; attempt < 30 && !found; attempt++)
                    {
                        float angle = offset + k * step + ((float)random.NextDouble() - 0.5f) * step * 0.8f;
                        float distance = Mathf.Lerp(band.min, band.max, (float)random.NextDouble());
                        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                        // Nada atrás do continente (ao sul): espelha para o lado do mar
                        if (direction.z < -0.15f)
                            direction.z = -direction.z;
                        position = middle + direction * distance;
                        // Virado mais ou menos para as ilhas, para mostrar a forma
                        yaw = angle + 180f + ((float)random.NextDouble() - 0.5f) * 70f;
                        Vector3 candidate = position;
                        found = !placed.Exists(p => Vector3.Distance(p.position, candidate) < (p.radius + footprint) * 0.6f);
                    }

                    if (!found)
                        continue;
                    placed.Add((position, footprint));
                    float scale = band.scale * (0.8f + (float)random.NextDouble() * 0.5f);
                    BlockMesh.Tile = k_Cell * Mathf.Max(1f, scale * 1.6f);
                    switch (kinds[k])
                    {
                        case 0: BrokenRing(band.mesh, position, yaw, scale, random); break;
                        case 1: ArquipelagoRuinKit.Tower(band.mesh, position, yaw, scale, random); break;
                        case 2: ArquipelagoRuinKit.Arch(band.mesh, position, yaw, scale, random); break;
                        case 3: SunkenStairs(band.mesh, position, yaw, scale); break;
                        default: BuildPalace(band.mesh, position, yaw, scale, random); break;
                    }
                }
            }

            BuildNorthExpanse(near, far, placed, random);
            BlockMesh.Tile = k_Cell;
            ArquipelagoRuinKit.Vines = vines;
        }

        // Mar aberto ao norte e dos lados da Ponte Colossal e do portal: muitas ruínas espalhadas até ~1,6 km além do
        // portal, cada vez maiores com a distância (as de perto com o material normal; as outras, silhuetas na névoa),
        // para dar a sensação de imensidão
        static void BuildNorthExpanse(RuinMesh near, RuinMesh far, List<(Vector3 position, float radius)> placed,
            System.Random random)
        {
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            float x0 = CemeteryCenter.x, zFrom = CemeteryCenter.z + 120f, zTo = TempleFrontZ + k_PortalBehind + 1600f;
            Vector3 portal = new Vector3(x0, 0f, TempleFrontZ + k_PortalBehind);
            GridSceneKit.Cards vines = ArquipelagoRuinKit.Vines;
            int made = 0;
            for (int attempt = 0; attempt < 6000 && made < 110; attempt++)
            {
                var position = new Vector3(x0 + R(-1500f, 1500f), 0f, R(zFrom, zTo));
                // Distância até a ponte, o templo e o portal (o eixo deles)
                float along = Mathf.Clamp(position.z, BridgeStartZ, portal.z);
                float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(x0, along));
                if (position.z > portal.z)
                    distance = Mathf.Min(distance, Vector3.Distance(position, portal));
                if (distance < 90f || Vector3.Distance(position, portal) < 350f)
                    continue;
                int cx = Mathf.FloorToInt(position.x / k_Cell), cz = Mathf.FloorToInt(position.z / k_Cell);
                if (cx >= 0 && cz >= 0 && cx < k_Size && cz < k_Size && s_Island[cx, cz] != 0)
                    continue;
                int kind = random.Next(10);
                kind = kind < 4 ? 0 : kind < 6 ? 1 : kind < 8 ? 2 : kind < 9 ? 3 : 4;
                float bandScale = Mathf.Lerp(0.7f, 2.8f, Mathf.Clamp01((distance - 90f) / 1300f));
                float footprint = (kind == 4 ? 60f : kind == 0 ? 45f : 25f) * bandScale;
                Vector3 candidate = position;
                if (placed.Exists(q => Vector3.Distance(q.position, candidate) < (q.radius + footprint) * 0.7f))
                    continue;
                placed.Add((position, footprint));
                made++;
                bool close = distance < 230f;
                RuinMesh mesh = close ? near : far;
                ArquipelagoRuinKit.Vines = close ? vines : null;
                // Virado mais ou menos para a ponte
                float yaw = Mathf.Atan2(x0 - position.x, along - position.z) * Mathf.Rad2Deg + R(-35f, 35f);
                float scale = bandScale * R(0.8f, 1.3f);
                BlockMesh.Tile = k_Cell * Mathf.Max(1f, scale * 1.6f);
                switch (kind)
                {
                    case 0: BrokenRing(mesh, position, yaw, scale, random); break;
                    case 1: ArquipelagoRuinKit.Tower(mesh, position, yaw, scale, random); break;
                    case 2: ArquipelagoRuinKit.Arch(mesh, position, yaw, scale, random); break;
                    case 3: SunkenStairs(mesh, position, yaw, scale); break;
                    default: BuildPalace(mesh, position, yaw, scale, random); break;
                }
            }

            ArquipelagoRuinKit.Vines = vines;
        }

        // Anel quebrado em pé e semi-afundado (blocos, canaleta, aros, pontas em dente), às vezes com um anel interno
        // e um pedaço caído na água
        static void BrokenRing(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            float radius = (25f + (float)random.NextDouble() * 30f) * scale;
            Vector3 normal = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
            normal = Quaternion.AngleAxis((float)random.NextDouble() * 16f - 8f, Vector3.Cross(normal, Vector3.up)) * normal;
            Vector3 center = position - Vector3.up * radius * (0.15f + (float)random.NextDouble() * 0.3f);
            // Começa sempre abaixo da água (ângulos positivos sobem: acima de ~-5° a ponta já sairia da água)
            float start = -20f + (float)random.NextDouble() * 15f;
            float span = 150f + (float)random.NextDouble() * 110f;
            float thickness = 4f * scale + radius * 0.1f;
            // 0° na horizontal, no plano do anel: os ângulos positivos sempre sobem
            Vector3 zero = Vector3.Cross(Vector3.up, normal);
            ArquipelagoRuinKit.RichRing(mesh, center, normal, zero, radius, thickness, thickness * 0.9f, start,
                start + span, random);

            if (random.NextDouble() < 0.35)
                ArquipelagoRuinKit.RichRing(mesh, center, normal, zero, radius * 0.7f, thickness * 0.6f,
                    thickness * 0.6f, start, start + span - 30f, random, false, true, 0f, 4);

            if (random.NextDouble() < 0.6)
            {
                Vector3 side = Vector3.Cross(normal, Vector3.up).normalized;
                ArquipelagoRuinKit.RichRing(mesh, center + Vector3.down * radius * 0.6f + side * radius * 0.4f,
                    Quaternion.Euler(0f, 30f, 0f) * normal, Quaternion.Euler(0f, 30f, 0f) * zero, radius * 0.9f,
                    thickness, thickness * 0.9f, start + span + 25f, start + span + 70f, random);
            }
        }

        // Escadaria larga que desce e some na água, com balaústres nos lados e dois blocos laterais
        static void SunkenStairs(RuinMesh mesh, Vector3 position, float yaw, float scale)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right;
            float width = 36f * scale, step = 2.5f * scale, rise = 1.6f * scale;
            for (int i = 0; i < 12; i++)
            {
                float y = 14f * scale - i * rise;
                BlockMesh.ChamferBox(mesh.Pick(), position + forward * (i * step) + Vector3.up * (y - 10f * scale),
                    new Vector3(width, 20f * scale, step), rotation, 0.3f * scale);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                ArquipelagoRuinKit.Pillar(mesh.Pick(), position + right * side * (width * 0.5f + 3f * scale) +
                                                         Vector3.up * 8f * scale,
                    new Vector2(6f * scale, 10f * scale), 26f * scale, rotation, 0.6f * scale);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), position + right * side * (width * 0.5f + 3f * scale) +
                                                         Vector3.up * 21.5f * scale,
                    new Vector2(7.2f * scale, 11.2f * scale), 1.4f * scale, rotation, 0.4f * scale);
            }
        }

        // Palácio afundado: fachada de pilares com base e capitel, vergas sobre os vãos, cornija no alto e pilastras;
        // na frente, uma colunata partida; atrás, um telhado em degraus caído. Tudo inclinado (afundando).
        static void BuildPalace(RuinMesh mesh, Vector3 position, float yaw, float scale, System.Random random)
        {
            Quaternion rotation = Quaternion.Euler(-6f, yaw, 4f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            Vector3 origin = position - up * 4f * scale;
            float pier = 5f * scale, opening = 7f * scale, height = 30f * scale, openingHeight = 16f * scale,
                depth = 6f * scale;
            int openings = 4;
            float total = openings * opening + (openings + 1) * pier;
            float cursor = -total * 0.5f;
            for (int i = 0; i <= openings; i++)
            {
                float pierHeight = i == openings ? height * 0.6f : height; // a ponta direita desabou
                Vector3 pierFoot = origin + right * (cursor + pier * 0.5f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (pierHeight * 0.5f), new Vector2(pier, depth),
                    pierHeight, rotation, pier * 0.08f);
                // Base e capitel salientes, e uma pilastra na frente do pilar
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (6f * scale), new Vector2(pier * 1.25f, depth * 1.2f),
                    1.6f * scale, rotation, pier * 0.1f);
                if (i < openings)
                    ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (openingHeight - 0.8f * scale),
                        new Vector2(pier * 1.25f, depth * 1.2f), 1.6f * scale, rotation, pier * 0.1f);
                ArquipelagoRuinKit.Pillar(mesh.Pick(), pierFoot + up * (pierHeight * 0.5f) + forward * (depth * 0.55f),
                    new Vector2(pier * 0.4f, depth * 0.15f), pierHeight * 0.9f, rotation, pier * 0.05f);
                cursor += pier;
                if (i == openings)
                    break;
                if (i < openings - 1)
                {
                    // Parede acima do vão, com uma verga saliente
                    BlockMesh.ChamferBox(mesh.Pick(), origin + right * (cursor + opening * 0.5f) +
                                                      up * ((openingHeight + height) * 0.5f),
                        new Vector3(opening, height - openingHeight, depth), rotation, pier * 0.05f);
                    BlockMesh.ChamferBox(mesh.Pick(), origin + right * (cursor + opening * 0.5f) +
                                                      up * (openingHeight + 0.8f * scale) + forward * (depth * 0.1f),
                        new Vector3(opening + pier * 0.6f, 1.6f * scale, depth * 1.2f), rotation, pier * 0.08f);
                }

                cursor += opening;
            }

            // Cornija no alto (só até onde a fachada ficou de pé)
            BlockMesh.ChamferBox(mesh.Pick(), origin + right * (-pier * 0.5f) + up * (height + 0.8f * scale),
                new Vector3(total - pier * 2f, 1.6f * scale, depth * 1.3f), rotation, pier * 0.1f);
            ArquipelagoRuinKit.HangVine(origin + right * (-total * 0.2f) + up * height + forward * (depth * 0.7f),
                forward, random, scale * 3f);
            ArquipelagoRuinKit.HangVine(origin + right * (total * 0.15f) + up * height + forward * (depth * 0.7f),
                forward, random, scale * 3f);

            for (int i = 0; i < 6; i++)
            {
                float x = -total * 0.45f + i * total * 0.18f;
                float h = (8f + (i * 7 % 5) * 3f) * scale;
                ArquipelagoRuinKit.Column(mesh, origin + right * x + forward * 14f * scale - up * 2f * scale, h,
                    2f * scale, random, true);
            }

            Vector3 roof = origin + right * (total * 0.6f) + forward * 10f * scale;
            Quaternion roofRotation = rotation * Quaternion.Euler(0f, 0f, 35f);
            for (int step = 0; step < 4; step++)
            {
                float w = (18f - step * 4f) * scale;
                BlockMesh.ChamferBox(mesh.Pick(), roof + roofRotation * Vector3.up * (step * 2.5f * scale),
                    new Vector3(w, 2.5f * scale, w * 0.8f), roofRotation, 0.4f * scale);
            }
        }

        // ---------- musgo dos pisos grandes ----------

        // Manchas: ruído de baixa frequência no mundo (manchas de ~20 m) com um pouco de detalhe (~7 m), de 0 a 1
        static float MossNoise(Vector3 p) => 0.65f * Mathf.PerlinNoise(p.x / 22f + 31.7f, p.z / 22f + 17.3f) +
                                             0.35f * Mathf.PerlinNoise(p.x / 7f + 5.1f, p.z / 7f + 9.9f);

        // Ponte Colossal: o meio do tabuleiro limpo (caminho gasto, runas à mostra), o musgo cresce para o parapeito e
        // toma os pedestais; no templo, limpo no meio e com musgo para as pontas dos degraus
        static float BridgeMoss(Vector3 p)
        {
            float x = Mathf.Abs(p.x - CemeteryCenter.x), half = k_BridgeWidth * 0.5f;
            float rule = p.z < TempleFrontZ - 0.5f
                ? 0.04f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half - 8f, half, x)) * 0.7f + (x > half ? 0.25f : 0f)
                : 0.08f + Mathf.InverseLerp(28f, 60f, x) * 0.55f;
            return rule + (MossNoise(p) - 0.5f) * 0.75f;
        }

        // Cemitério: quanto mais baixo o anel (mais perto da água), mais musgo; em cada passarela o musgo cresce para
        // as bordas; escadas e ilha central bem tomadas
        static float CemeteryMoss(Vector3 p)
        {
            Vector3 center = CemeteryCenter;
            float r = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z));
            float[] rule = { 0.1f, 0.32f, 0.5f };
            for (int i = 0; i < k_CemeteryRings.Length; i++)
            {
                (float radius, float width, float top) = k_CemeteryRings[i];
                float d = Mathf.Abs(r - radius) / (width * 0.5f);
                if (d <= 1.05f && Mathf.Abs(p.y - top) < 0.5f)
                    return rule[i] + Mathf.InverseLerp(0.55f, 1f, d) * 0.4f + (MossNoise(p) - 0.5f) * 0.75f;
            }

            return 0.6f + (MossNoise(p) - 0.5f) * 0.75f;
        }

        // Destroços de anel (montes caídos, arcos que giram): manchas de musgo sem regra, em ~40% do topo
        static float RubbleMoss(Vector3 p) => 0.4f + (MossNoise(p) - 0.5f) * 1.1f;

        // Placas de musgo deitadas e tufos de capim nas partes com musgo do tabuleiro e dos anéis (as placas suavizam
        // a passagem entre laje e musgo; os tufos saem das juntas)
        static void PlaceRuinMoss(Transform parent, Mesh quad, Transform decor, System.Random random)
        {
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var patches = new MeshData();
            void Patch(Vector3 p, float size)
            {
                Quaternion yaw = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Vector3 a = yaw * new Vector3(-size, 0f, -size) * 0.5f, b = yaw * new Vector3(-size, 0f, size) * 0.5f;
                int start = patches.Vertices.Count;
                Vector3 lift = p + Vector3.up * 0.05f;   // acima das runas gravadas (0,03 m)
                patches.Vertices.AddRange(new[] { lift + a, lift + b, lift - a, lift - b });
                for (int i = 0; i < 4; i++)
                    patches.Normals.Add(Vector3.up);
                patches.Uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
                patches.Triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            void Spot(Vector3 p, float moss)
            {
                if (moss > 0.2f && moss < 0.8f && random.NextDouble() < 0.55)
                    Patch(p, R(1.2f, 3.4f));
                if (moss > 0.4f && random.NextDouble() < 0.3)
                    GridSceneKit.Billboard("Tufo", quad, s_Materials.Tuft, s_Materials.TuftSize * R(0.6f, 1f), p, decor,
                        random.NextDouble() < 0.5);
            }

            // Ponte (o tabuleiro)
            float x0 = CemeteryCenter.x, half = k_BridgeWidth * 0.5f;
            for (int k = 0; k < 900; k++)
            {
                var p = new Vector3(x0 + R(-half + 1.2f, half - 1.2f), BridgeTop, R(BridgeStartZ + 2f, BridgeStartZ + k_BridgeLength - 2f));
                Spot(p, BridgeMoss(p));
            }

            // Anéis do Cemitério (fora das escadas e da cobertura, que ficam na borda e no meio das passarelas)
            for (int i = 0; i < k_CemeteryRings.Length; i++)
            {
                (float radius, float width, float top) = k_CemeteryRings[i];
                int count = Mathf.RoundToInt(radius * 6f);
                for (int k = 0; k < count; k++)
                {
                    float angle = R(0f, 360f);
                    if (i == 1 && Mathf.Abs(Mathf.DeltaAngle(angle, k_CemeteryBreak)) < 5f)
                        continue;
                    Vector3 p = OnCemetery(angle, radius + R(-width * 0.5f + 1f, width * 0.5f - 1f), top);
                    Spot(p, CemeteryMoss(p));
                }
            }

            GridSceneKit.CreateObject(k_Prefix + "_Placas_Musgo", "Placas_Musgo", patches, s_Materials.MossPatch, parent,
                false, false, false);
        }

        // ---------- máscara do mar livre ----------

        const float k_MaskCell = 2f;

        // Marca, numa imagem de 2 m por pixel, onde há terra ou estrutura acima da água (todo triângulo de malha do bioma
        // que sobe acima da linha d'água marca a área dele, com 1 pixel de folga); as partículas de brilho só nascem
        // nos pixels livres. A área é a dos colisores (onde o jogador anda) com 100 m de folga.
        static void BuildWaterMask(Transform world)
        {
            Vector3 root = world.position;
            Bounds area = default;
            bool any = false;
            foreach (Collider collider in world.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger)
                    continue;
                if (any)
                    area.Encapsulate(collider.bounds);
                else
                {
                    area = collider.bounds;
                    any = true;
                }
            }

            if (!any)
                return;
            area.Expand(new Vector3(200f, 0f, 200f));
            float minX = area.min.x - root.x, minZ = area.min.z - root.z;
            int width = Mathf.Min(4096, Mathf.CeilToInt(area.size.x / k_MaskCell));
            int height = Mathf.Min(4096, Mathf.CeilToInt(area.size.z / k_MaskCell));
            var blocked = new bool[width * height];
            float waterTop = root.y + 0.15f;

            foreach (MeshFilter filter in world.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (!mesh || filter.GetComponent<ParticleSystemRenderer>())
                    continue;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                int[] triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < waterTop)
                        continue;
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - root.x - minX) / k_MaskCell) - 1;
                    int x1 = Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - root.x - minX) / k_MaskCell) + 1;
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - root.z - minZ) / k_MaskCell) - 1;
                    int z1 = Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - root.z - minZ) / k_MaskCell) + 1;
                    if (x1 < 0 || z1 < 0 || x0 >= width || z0 >= height)
                        continue;
                    x0 = Mathf.Max(0, x0);
                    z0 = Mathf.Max(0, z0);
                    x1 = Mathf.Min(width - 1, x1);
                    z1 = Mathf.Min(height - 1, z1);
                    for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        blocked[z * width + x] = true;
                }
            }

            var pixels = new Color32[blocked.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = blocked[i] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = IcaroAssetUtility.Root + "/Art/Grade/Texturas/" + k_Prefix + "_MascaraAgua.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();

            var mask = world.gameObject.AddComponent<WaterSurfaceMask>();
            mask.Mask = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            mask.Origin = new Vector2(minX, minZ);
            mask.CellSize = k_MaskCell;
        }

        // ---------- decoração ----------

        // Tufos de capim e flores no topo das ilhas (sprites que olham para o jogador), longe das ruínas
        static void PlaceDecor(Transform parent, List<Vector3> cover)
        {
            Mesh quad = GridSceneKit.SpriteQuad();
            var random = new System.Random(55);
            var decor = new GameObject("Decoracao").transform;
            decor.SetParent(parent, false);
            for (int x = -k_WestCoast; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (!IsLand(x, z) || InGrid(x, z) && s_Ramp[x, z])
                    continue;
                double roll = random.NextDouble();
                if (roll > 0.16)
                    continue;
                Vector3 position = GridSceneKit.CellCenter(x, z) + Vector3.up * HeightAt(x, z) +
                                   new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 1.4f;
                if (cover.Exists(c => Vector3.Distance(c, position) < 3f))
                    continue;
                bool flower = roll < 0.04;
                float scale = 0.7f + (float)random.NextDouble() * 0.5f;
                GridSceneKit.Billboard(flower ? "Flor" : "Tufo", quad, flower ? s_Materials.Flower : s_Materials.Tuft,
                    (flower ? s_Materials.FlowerSize : s_Materials.TuftSize) * scale, position, decor,
                    random.NextDouble() < 0.5);
            }

            PlaceRuinMoss(parent, quad, decor, random);

            // Pontinhos de luz sobre as ilhas e o mar
            Vector3 center = CellToWorld(new Vector2(k_Size * 0.5f, k_Size * 0.5f), 6f);
            var size = new Vector3(k_Size * k_Cell, 12f, k_Size * k_Cell);
            GridSceneKit.AddSpores("Esporos_Arquipelago", s_Materials.Spores, center, size,
                Mathf.RoundToInt(260f * k_Size * k_Size / (k_SouthSize * k_SouthSize)), 26f,
                new Vector2(0.08f, 0.2f), Theme().SporeColors, parent);
        }

        // ---------- combate ----------

        // Sala de uma arena (as ilhas com Arena = 'arena'): gatilho em caixa cobrindo as ilhas, começando 4 m depois do
        // portão (quem para na ponte ou no cais não fica trancado do lado de fora; sem cápsula, as IAs usam 50 m em
        // volta do centro como limite da arena), portão de entrada e, se houver, o de saída (fechado até a sala ser
        // limpa) e o NavMesh da arena
        static RoomManager BuildRoom(Transform parent, string objectName, string roomName, int arena, int number,
            Vector3 doorPoint, Vector3 doorForward, Vector3? exitPoint, Vector3 exitForward, out NavMeshSurface surface,
            Rect? area = null)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            float ground = float.MaxValue;
            for (int x = 0; x < k_Size; x++)
            for (int z = 0; z < k_Size; z++)
            {
                if (s_Island[x, z] == 0 || k_Islands[s_Island[x, z] - 1].Arena != arena)
                    continue;
                min = Vector2.Min(min, new Vector2(x, z) * k_Cell);
                max = Vector2.Max(max, new Vector2(x + 1, z + 1) * k_Cell);
                if (IsLand(x, z))
                    ground = Mathf.Min(ground, s_Height[x, z]);
            }

            if (area.HasValue)
            {
                min = area.Value.min;
                max = area.Value.max;
            }

            var center = new Vector3((min.x + max.x) * 0.5f, ground, (min.y + max.y) * 0.5f);
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            // Corta a caixa no lado do portão
            Vector2 tmin = min, tmax = max;
            if (Mathf.Abs(doorForward.z) >= Mathf.Abs(doorForward.x))
            {
                if (doorForward.z > 0f)
                    tmin.y = Mathf.Max(tmin.y, doorPoint.z + 4f);
                else
                    tmax.y = Mathf.Min(tmax.y, doorPoint.z - 4f);
            }
            else if (doorForward.x > 0f)
                tmin.x = Mathf.Max(tmin.x, doorPoint.x + 4f);
            else
                tmax.x = Mathf.Min(tmax.x, doorPoint.x - 4f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(tmax.x - tmin.x, 40f, tmax.y - tmin.y);
            trigger.center = new Vector3((tmin.x + tmax.x) * 0.5f - center.x, 12f, (tmin.y + tmax.y) * 0.5f - center.z);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = roomName;
            RoomDoor door = GridSceneKit.CreateDoor("Porta_Entrada", V(0, 0), s_Materials.Door, root.transform);
            door.transform.SetPositionAndRotation(doorPoint, Quaternion.LookRotation(doorForward));
            var doors = new List<RoomDoor> { door };
            if (exitPoint.HasValue)
            {
                RoomDoor exit = GridSceneKit.CreateDoor("Porta_Saida", V(0, 0), s_Materials.Door, root.transform);
                exit.transform.SetPositionAndRotation(exitPoint.Value, Quaternion.LookRotation(exitForward));
                exit.StartOpen = false;
                doors.Add(exit);
            }

            room.Doors = doors.ToArray();
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = 1;
            room.ArenaNumber = number;
            room.ArenaInBiome = number - 9;   // o Arquipélago começa na sala 9
            room.ArenasInBiome = 8;
            room.IsBoss = false;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.up * 10f;
            surface.size = new Vector3(max.x - min.x + 20f, 50f, max.y - min.y + 20f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        // Pontos nas ilhas da arena, longe do portão, com ~2 m livres em volta
        // 'candidates' (opcional, coordenadas antes do deslocamento): pontos a testar em vez de células sorteadas
        static int PlaceSpawns(RoomManager room, Vector3 door, int arena, List<Vector3> candidates = null)
        {
            var random = new System.Random(77 + arena);
            var points = new List<Vector3>();
            int tries = candidates != null ? candidates.Count : 60000;
            for (int attempt = 0; attempt < tries && points.Count < 10; attempt++)
            {
                Vector3 candidate;
                if (candidates != null)
                    candidate = candidates[attempt] + s_Offset;
                else
                {
                    int x = random.Next(0, k_Size), z = random.Next(0, k_Size);
                    int island = s_Island[x, z];
                    if (island == 0 || k_Islands[island - 1].Arena != arena || !IsLand(x, z) || s_Ramp[x, z] ||
                        !Interior(x, z, 2))
                        continue;
                    candidate = GridSceneKit.CellCenter(x, z) + Vector3.up * s_Height[x, z] + s_Offset;
                }

                if (Vector3.Distance(candidate, door) < 26f)
                    continue;
                if (!NavMesh.SamplePosition(candidate + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    continue;
                if (points.Exists(p => Vector3.Distance(p, hit.position) < 8f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                points.Add(hit.position);
            }

            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(room.transform, false);
            var transforms = new List<Transform>();
            foreach (Vector3 position in points)
            {
                var point = new GameObject($"Spawn_{transforms.Count + 1:00}").transform;
                point.SetParent(spawns, false);
                point.position = position;
                transforms.Add(point);
            }

            room.SpawnPoints = transforms.ToArray();
            return points.Count;
        }

        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            // Na praça do alto do penhasco, de frente para o mar
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(k_Arrival + new Vector3(0f, 0.2f, 4f),
                Quaternion.LookRotation(Vector3.forward));
        }
    }
}
