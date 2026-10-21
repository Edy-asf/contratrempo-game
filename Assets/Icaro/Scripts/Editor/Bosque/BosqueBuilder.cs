using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

namespace Icaro.EditorTools
{
    // "Ícaro > Bosque > Gerar Bosque": o Bosque novo, com salas desenhadas à mão e relevo (substitui o Bosque gerado
    // em grade; feito por etapas, numa cena própria, Scenes/Bosque.unity). Grade de 2 m com altura por célula:
    // terraços com barrancos de rocha, rampas de terra, riacho raso, trilhas entre as salas e a floresta de sprites
    // em volta (as mesmas espécies do Bosque em grade). A cobertura é de rochas irregulares (pilhas tortas com musgo
    // e trepadeiras, pedras soltas) e ruínas engolidas pela mata (colunas, muros); os barrancos ganham pilhas de
    // rocha encostadas. Sem troncos nem raízes (não combinam com as árvores em sprite). Salas (8 no total; feitas por
    // etapas):
    // - 1 Clareira da Queda: entrada, com um terraço baixo a oeste e uma rampa.
    // - 2 Riacho Raso: um riacho atravessa a arena (água rasa, deixa lento), com uma laje de pedra servindo de ponte
    //   e pedras no leito; ao norte, dois terraços com rampas e um corredor baixo no meio até a saída.
    // - 3 Ravina das Lajes: uma ravina corta a arena de sul a norte entre dois platôs de 3,2 m, ligados por uma laje
    //   de pedra; rampas dos platôs para as bacias da entrada e da saída.
    // - 4 Lago: lago raso (inteiro andável) com duas ilhotas e colunas afundando; um terraço a noroeste com rampa.
    // - 5 Encosta em Terraços: a arena sobe de oeste para leste em três níveis ligados por rampas, com ruína no alto.
    // - 6 Templo Engolido: pódio de pedra de 2,4 m com escadarias, colunatas e altar, num pátio de lajes quebradas.
    // - 7 Portal: muralha baixa cortada por uma passagem de lajes sob um arco colossal, avenida de colunas até a saída.
    // - 8 Clareira de Anéis (chefe, Rei Fungo): clareira plana com anéis de lajes, degrau de ruína em volta e pedras em
    //   pé; a saída do chefe segue até a borda norte do mapa (OpenEndStart/OpenEndPoint).
    // Cada sala tem RoomManager, portas, NavMesh próprio, pontos de spawn escondidos da entrada e poleiros do
    // Espinhento (rochas altas de topo plano). Rodar de novo refaz a cena.
    public static class BosqueBuilder
    {
        const string k_SceneName = "Bosque";
        const float k_Cell = GridSceneKit.Cell;
        const int k_Width = 170;
        const int k_ForestDepth = 30;   // árvores de perto: sprites que olham para o jogador, na cor normal
        const int k_DarkDepth = 60;     // daí até aqui, cartas fixas escurecendo até o preto
        const int k_FloorDepth = 62;    // quanto o chão continua além da borda
        const int k_Margin = 64;        // células além da grade (a floresta de fundo, o chão e a parede preta)
        const int k_DarkSteps = 8;      // passos de cor das árvores do fundo
        const float k_WallHeight = 34f; // parede preta no fim da floresta (da altura das árvores)
        const float k_Barrier = 20f;    // altura da parede invisível da borda
        const float k_RiverBed = -0.45f;
        const float k_RiverSurface = -0.12f;

        // Cena da geração atual (a própria do Bosque ou a da Via) e o prefixo das malhas (cada cena tem as suas: na
        // Via o norte do mapa fica aberto para o desfiladeiro, então o desenho muda)
        static string s_SceneName = k_SceneName, s_Prefix = k_SceneName;
        static bool s_OpenNorth;
        static string ScenePath => $"{IcaroAssetUtility.ScenesFolder}/{s_SceneName}.unity";
        static string NavMeshFolder => $"{IcaroAssetUtility.ScenesFolder}/{s_SceneName}_NavMesh";

        // Tema e materiais da geração atual (a Via usa no desfiladeiro)
        public static GridTheme Theme => s_Theme;
        public static GridTextureKit.Set Materials => s_Forest;

        enum Kind : byte
        {
            Void,     // fora do espaço andável (chão da floresta, árvores)
            Floor,
            Path,     // trilha (chão de terra)
            Shallow,  // água rasa: anda, mais lento
            Deep,     // água fora das salas: mesma cara da rasa, mas não passa (parede invisível)
            Stone,    // lajes de pedra (pátio e pódio do templo)
        }

        class Arena
        {
            public string Name;
            public int Number;
            public Vector2 Center;
            public float RadiusX, RadiusZ, Noise, Phase;
            public int EntranceX, ExitX;
            public bool Boss;
            public System.Action<Arena> Shape;     // relevo e água (antes do terreno)
            public System.Action<Arena> Dress;     // cobertura, poleiros e árvores (depois do terreno)

            // Preenchidos na geração
            public int Index;
            public Vector2Int Entrance, Exit;
            public RectInt Bounds;
            public readonly List<Vector3> Perches = new List<Vector3>();
            public readonly List<Vector3> Reserved = new List<Vector3>();   // onde não pôr árvores nem cobertura
        }

        class Ramp
        {
            public Vector2Int Edge;    // última célula do terraço na faixa do meio
            public Vector2Int Dir;     // para onde a rampa desce
            public int Length, HalfWidth;
            public float Low, High;
            public bool Path;
        }

        static int s_Depth;
        static Kind[,] s_Kind;
        static float[,] s_Height;      // chão (na água, o leito)
        static int[,] s_Owner;         // arena + 1
        static int[,] s_Ramp;          // rampa + 1
        static int[,] s_Distance;      // células até o espaço andável (com a margem: índice + k_Margin)
        static Vector2Int[,] s_Nearest;
        static readonly List<Ramp> s_Ramps = new List<Ramp>();
        static Arena[] s_Arenas;
        static GridTheme s_Theme;
        static GridTextureKit.Set s_Forest;
        static ArquipelagoTextureKit.Set s_Stone;
        static RuinMesh s_Rocks, s_Ruins;
        static Transform s_World;
        static System.Random s_Random;

        static readonly Vector2 k_StartCenter = new Vector2(85f, 18f);

        // Fim aberto: começo (porta de saída do chefe) e fim (borda norte do mapa) da trilha por onde a Via continua
        public static Vector3 OpenEndStart { get; private set; }
        public static Vector3 OpenEndPoint { get; private set; }

        static Vector2Int V(int x, int z) => new Vector2Int(x, z);
        static float R(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

        // ---------- salas ----------

        static Arena[] Arenas() => new[]
        {
            new Arena
            {
                Name = "Clareira da Queda", Number = 1, Center = new Vector2(85f, 48f), RadiusX = 14f, RadiusZ = 12f,
                Noise = 0.08f, Phase = 0.4f, EntranceX = 85, ExitX = 85, Shape = ShapeClearing, Dress = DressClearing,
            },
            new Arena
            {
                Name = "Riacho Raso", Number = 2, Center = new Vector2(70f, 92f), RadiusX = 16f, RadiusZ = 13f,
                Noise = 0.07f, Phase = 1.9f, EntranceX = 70, ExitX = 70, Shape = ShapeCreek, Dress = DressCreek,
            },
            new Arena
            {
                Name = "Ravina das Lajes", Number = 3, Center = new Vector2(100f, 136f), RadiusX = 17f,
                RadiusZ = 15f, Noise = 0.06f, Phase = 3.1f, EntranceX = 100, ExitX = 100, Shape = ShapeRavine,
                Dress = DressRavine,
            },
            new Arena
            {
                Name = "Lago", Number = 4, Center = new Vector2(78f, 182f), RadiusX = 18f, RadiusZ = 15f,
                Noise = 0.06f, Phase = 4.4f, EntranceX = 78, ExitX = 88, Shape = ShapeLake, Dress = DressLake,
            },
            new Arena
            {
                Name = "Encosta em Terraços", Number = 5, Center = new Vector2(104f, 226f), RadiusX = 17f,
                RadiusZ = 15f, Noise = 0.06f, Phase = 5.6f, EntranceX = 93, ExitX = 93, Shape = ShapeSlope,
                Dress = DressSlope,
            },
            new Arena
            {
                Name = "Templo Engolido", Number = 6, Center = new Vector2(84f, 270f), RadiusX = 18f, RadiusZ = 16f,
                Noise = 0.05f, Phase = 0.9f, EntranceX = 84, ExitX = 84, Shape = ShapeTemple, Dress = DressTemple,
            },
            new Arena
            {
                Name = "Portal", Number = 7, Center = new Vector2(104f, 318f), RadiusX = 17f, RadiusZ = 15f,
                Noise = 0.05f, Phase = 2.4f, EntranceX = 104, ExitX = 104, Shape = ShapePortal, Dress = DressPortal,
            },
            new Arena
            {
                Name = "Clareira de Anéis", Number = 8, Boss = true, Center = new Vector2(104f, 362f), RadiusX = 22f,
                RadiusZ = 20f, Noise = 0.03f, Phase = 1.3f, EntranceX = 104, ExitX = 104, Shape = ShapeRings,
                Dress = DressRings,
            },
        };

        // Trilhas (pontos em células, 3 células de largura): do lugar da queda até a sala 1 e entre as salas
        static Vector2[][] Trails(Arena[] arenas) => new[]
        {
            new[] { k_StartCenter, new Vector2(85f, arenas[0].Entrance.y - 1) },
            new[] { new Vector2(85f, arenas[0].Exit.y + 1), new Vector2(85f, 72f), new Vector2(70f, 72f),
                new Vector2(70f, arenas[1].Entrance.y - 1) },
            new[] { new Vector2(70f, arenas[1].Exit.y + 1), new Vector2(70f, 113f), new Vector2(100f, 113f),
                new Vector2(100f, arenas[2].Entrance.y - 1) },
            new[] { new Vector2(100f, arenas[2].Exit.y + 1), new Vector2(100f, 160f), new Vector2(78f, 160f),
                new Vector2(78f, arenas[3].Entrance.y - 1) },
            new[] { new Vector2(88f, arenas[3].Exit.y + 1), new Vector2(88f, 205f), new Vector2(93f, 205f),
                new Vector2(93f, arenas[4].Entrance.y - 1) },
            new[] { new Vector2(93f, arenas[4].Exit.y + 1), new Vector2(93f, 248f), new Vector2(84f, 248f),
                new Vector2(84f, arenas[5].Entrance.y - 1) },
            new[] { new Vector2(84f, arenas[5].Exit.y + 1), new Vector2(84f, 294f), new Vector2(104f, 294f),
                new Vector2(104f, arenas[6].Entrance.y - 1) },
            new[] { new Vector2(104f, arenas[6].Exit.y + 1), new Vector2(104f, arenas[7].Entrance.y - 1) },
            // Saída do chefe até a borda norte do mapa (onde o próximo trecho da Via continua)
            new[] { new Vector2(104f, arenas[7].Exit.y + 1), new Vector2(104f, s_Depth - 1) },
        };

        // 1 Clareira da Queda: terraço baixo a oeste com uma rampa para o leste
        static void ShapeClearing(Arena a)
        {
            Terrace(a, a.Center + new Vector2(-8f, 3f), 7f, 7f, 1.6f, 11);
            AddRamp(a, a.Center + new Vector2(-8f, 3f), V(1, 0), 4, 1, 0f, 1.6f, false);
        }

        static void DressClearing(Arena a)
        {
            float floor = 0f;
            RockPile(Spot(a, 5f, -3f, floor), R(2.2f, 3f), R(2.5f, 3.5f), V3(1, 0, 0));
            RockPile(Spot(a, 8f, 5f, floor), R(2.5f, 3.2f), R(3f, 4f), V3(-1, 0, 0));
            RockCluster(Spot(a, -2f, -5f, floor), 3f, 4);
            RockCluster(Spot(a, 10f, -1f, floor), 2.5f, 3);
            RockPile(Spot(a, 6f, 9f, floor), R(2.2f, 2.8f), R(2.2f, 3f), V3(0, 0, -1));
            BrokenColumn(Spot(a, -9f, 2f, 1.6f), 3f);
            RuinWall(Spot(a, -6f, 7f, 1.6f), 15f, 5f, 2.2f);
            Perch(a, Spot(a, 11f, 7f, floor), floor);
            Perch(a, Spot(a, -10f, -6f, floor), floor);
            Perch(a, Spot(a, -11f, 5f, 1.6f), floor);
            ArenaTrees(a, 6);
        }

        // 2 Riacho Raso: o riacho atravessa a arena (e segue fundo pela mata dos dois lados); ao norte, dois terraços
        // com rampas para o corredor do meio, que leva à saída
        static float CreekZ(float x) => 92f + 2.2f * Mathf.Sin(x * 0.23f) + 0.8f * Mathf.Sin(x * 0.61f + 1f);

        static void ShapeCreek(Arena a)
        {
            for (int x = Mathf.RoundToInt(a.Center.x - 40f); x <= a.Center.x + 40f; x++)
            for (int z = Mathf.FloorToInt(CreekZ(x)) - 3; z <= CreekZ(x) + 3; z++)
            {
                if (!InMap(x, z) || Mathf.Abs(z + 0.5f - CreekZ(x + 0.5f)) > 2.1f)
                    continue;
                s_Kind[x, z] = s_Owner[x, z] == a.Index + 1 ? Kind.Shallow : Kind.Deep;
                s_Height[x, z] = k_RiverBed;
            }

            Terrace(a, a.Center + new Vector2(-11f, 8f), 5.5f, 4.5f, 1.6f, 21);
            Terrace(a, a.Center + new Vector2(11f, 8f), 5.5f, 4.5f, 1.6f, 22);
            AddRamp(a, a.Center + new Vector2(-11f, 8f), V(1, 0), 4, 1, 0f, 1.6f, false);
            AddRamp(a, a.Center + new Vector2(11f, 8f), V(-1, 0), 4, 1, 0f, 1.6f, false);
        }

        static void DressCreek(Arena a)
        {
            // Laje de pedra atravessando o riacho (dá para passar por cima) e pedras no leito
            float bridgeX = (a.Center.x - 8f + 0.5f) * k_Cell, creek = CreekZ(a.Center.x - 8f) * k_Cell;
            StoneSlab(new Vector3(bridgeX, 0f, creek - 5.5f), new Vector3(bridgeX, 0f, creek + 5.5f), 3f, 0.35f);
            ReserveLine(a, new Vector3(bridgeX, 0f, creek - 6f), new Vector3(bridgeX, 0f, creek + 6f));
            for (int i = 0; i < 4; i++)
            {
                float x = a.Center.x + 2f + i * 2.2f;
                ArquipelagoRuinKit.IrregularRock(s_Rocks, new Vector3(x * k_Cell + R(-0.5f, 0.5f), k_RiverBed + 0.3f,
                    CreekZ(x) * k_Cell + R(-1f, 1f)), R(1.2f, 1.8f), s_Random);
            }

            RockPile(Spot(a, -3f, -6f, 0f), R(2.4f, 3.2f), R(2.5f, 3.5f), V3(0, 0, 1));
            RockPile(Spot(a, 10f, -5f, 0f), R(2.2f, 3f), R(2.5f, 3.2f), V3(-1, 0, 1));
            RockCluster(Spot(a, 4f, -8f, 0f), 2.5f, 3);
            BrokenColumn(Spot(a, -13f, 7f, 1.6f), 4f);
            RuinWall(Spot(a, 11f, 9f, 1.6f), 0f, 5f, 2.4f);
            RockPile(Spot(a, -9f, 10f, 1.6f), R(2f, 2.6f), R(2f, 2.8f), V3(0, 0, -1));
            Perch(a, Spot(a, -13f, -4f, 0f), 0f);
            Perch(a, Spot(a, 14f, -2f, 0f), 0f);
            Perch(a, Spot(a, -11f, 11f, 1.6f), 0f);
            Perch(a, Spot(a, 12f, 7f, 1.6f), 0f);
            ArenaTrees(a, 8);
        }

        // 3 Ravina das Lajes: platôs de 3,2 m (24 m de sul a norte) dos dois lados de uma ravina que corta a arena, com
        // bacias baixas na entrada e na saída e rampas dos platôs para elas
        const float k_Plateau = 3.2f;
        static float RavineX(float z) => 100f + 2f * Mathf.Sin(z * 0.2f);

        static void ShapeRavine(Arena a)
        {
            float south = a.Center.y - 6f, north = a.Center.y + 6f;
            foreach (Vector2Int cell in Cells(a))
            {
                float z = cell.y + 0.5f;
                if (z > south && z < north && Mathf.Abs(cell.x + 0.5f - RavineX(z)) > 3.5f)
                    s_Height[cell.x, cell.y] = k_Plateau;
            }

            AddRamp(a, new Vector2(a.Center.x - 10f, a.Center.y), V(0, -1), 5, 1, 0f, k_Plateau, false);
            AddRamp(a, new Vector2(a.Center.x + 10f, a.Center.y), V(0, -1), 5, 1, 0f, k_Plateau, false);
            AddRamp(a, new Vector2(a.Center.x - 8f, a.Center.y), V(0, 1), 5, 1, 0f, k_Plateau, false);
            AddRamp(a, new Vector2(a.Center.x + 8f, a.Center.y), V(0, 1), 5, 1, 0f, k_Plateau, false);
        }

        static void DressRavine(Arena a)
        {
            // Uma laje de pedra caída atravessando a ravina na altura dos platôs (dá para andar em cima)
            float slabZ = a.Center.y - 3f;
            Vector3 SideOf(float z, float side) => new Vector3((RavineX(z) + side * 6f) * k_Cell, k_Plateau, z * k_Cell);
            StoneSlab(SideOf(slabZ, -1f), SideOf(slabZ, 1f), 3.2f, k_Plateau);
            ReserveLine(a, SideOf(slabZ, -1f), SideOf(slabZ, 1f));

            // Trepadeiras penduradas nas paredes da ravina e cobertura nos platôs e no fundo
            WallVines(a, k_Plateau, 0.5f);

            RockPile(Spot(a, -12f, 2f, k_Plateau), R(2.4f, 3.2f), R(2.5f, 3.5f), V3(1, 0, 0));
            RockPile(Spot(a, 12f, -2f, k_Plateau), R(2.4f, 3.2f), R(2.5f, 3.5f), V3(-1, 0, 0));
            RockCluster(Spot(a, -10f, 3f, k_Plateau), 2.5f, 3);
            BrokenColumn(Spot(a, 10f, 2f, k_Plateau), 3.5f);
            RuinWall(Spot(a, -13f, -4f, k_Plateau), 90f, 5f, 2.2f);
            ArquipelagoRuinKit.IrregularRock(s_Rocks, Spot(a, 0.5f, 0f, 0.6f), 2.2f, s_Random);
            ArquipelagoRuinKit.IrregularRock(s_Rocks, Spot(a, -0.5f, 7f, 0.5f), 1.8f, s_Random);
            Perch(a, Spot(a, -14f, 0f, k_Plateau), 0f);
            Perch(a, Spot(a, 14f, 3f, k_Plateau), 0f);
            Perch(a, Spot(a, 6f, -10f, 0f), 0f);
            Perch(a, Spot(a, 12f, 8f, 0f), 0f);
            ArenaTrees(a, 6);
        }

        // 4 Lago: lago raso no meio (deixa lento, dá para atravessar inteiro) com duas ilhotas; a noroeste, um terraço de
        // 3,2 m com uma rampa para o leste

        static void ShapeLake(Arena a)
        {
            var lake = new Vector2(78f, 180f);
            foreach (Vector2Int cell in Cells(a))
            {
                var c = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (InEllipse(c, lake, 10f, 6.5f, 0.1f, 2.2f))
                {
                    s_Kind[cell.x, cell.y] = Kind.Shallow;
                    s_Height[cell.x, cell.y] = k_RiverBed;
                }

                if (InEllipse(c, new Vector2(72f, 182f), 1.7f, 1.5f, 0.1f, 3f) ||
                    InEllipse(c, new Vector2(85f, 178f), 1.6f, 1.6f, 0.1f, 4f))
                {
                    s_Kind[cell.x, cell.y] = Kind.Floor;
                    s_Height[cell.x, cell.y] = 0f;
                }
            }

            Terrace(a, new Vector2(68f, 191f), 7.5f, 4.5f, k_Plateau, 41);
            AddRamp(a, new Vector2(68f, 191f), V(1, 0), 6, 1, 0f, k_Plateau, false);

        }

        static void DressLake(Arena a)
        {
            // Ruína afundando no lago, pedras nas ilhotas e nas margens
            BrokenColumn(Spot(a, -3f, -1f, k_RiverBed), 3.5f);
            BrokenColumn(Spot(a, 5f, -4f, k_RiverBed), 2.5f);
            ArquipelagoRuinKit.IrregularRock(s_Rocks, CellToWorld(new Vector2(72f, 182f), 0.4f), 1.6f, s_Random);
            ArquipelagoRuinKit.IrregularRock(s_Rocks, CellToWorld(new Vector2(85f, 178f), 0.4f), 1.4f, s_Random);
            RockPile(Spot(a, 12f, -4f, 0f), R(2.4f, 3f), R(2.6f, 3.4f), V3(-1, 0, 0));
            RockPile(Spot(a, -12f, -6f, 0f), R(2.2f, 3f), R(2.4f, 3.2f), V3(1, 0, 0));
            RockCluster(Spot(a, 6f, 8f, 0f), 2.5f, 3);
            RockCluster(Spot(a, -6f, -10f, 0f), 2.5f, 3);
            RuinWall(Spot(a, -10f, 10f, k_Plateau), 80f, 4.5f, 2.4f);
            BrokenColumn(Spot(a, -14f, 7f, k_Plateau), 3f);
            Perch(a, Spot(a, -15f, -3f, 0f), 0f);
            Perch(a, Spot(a, 15f, 4f, 0f), 0f);
            Perch(a, Spot(a, -11f, 9f, k_Plateau), 0f);
            Perch(a, Spot(a, 10f, -10f, 0f), 0f);
            ArenaTrees(a, 6);
        }

        // 5 Encosta em Terraços: a arena sobe de oeste para leste em três níveis (0, 1,6 e 3,2 m), com a beira de cada
        // um ondulada, duas rampas do baixo para o do meio e uma do meio para o alto; ruína no alto
        static void ShapeSlope(Arena a)
        {
            foreach (Vector2Int cell in Cells(a))
            {
                float x = cell.x + 0.5f - 1.5f * Mathf.Sin((cell.y + 0.5f) * 0.35f);
                s_Height[cell.x, cell.y] = x > 112f ? k_Plateau : x > 102f ? 1.6f : 0f;
            }

            AddRamp(a, new Vector2(107f, 219f), V(-1, 0), 4, 1, 0f, 1.6f, false);
            AddRamp(a, new Vector2(107f, 233f), V(-1, 0), 4, 1, 0f, 1.6f, false);
            AddRamp(a, new Vector2(117f, 227f), V(-1, 0), 4, 1, 1.6f, k_Plateau, false);
        }

        static void DressSlope(Arena a)
        {
            RockPile(Spot(a, -9f, 0f, 0f), R(2.4f, 3.2f), R(2.6f, 3.6f), V3(1, 0, 0));
            RockPile(Spot(a, 3f, -6f, 1.6f), R(2.2f, 3f), R(2.4f, 3.2f), V3(-1, 0, 0));
            RockPile(Spot(a, 4f, 8f, 1.6f), R(2.2f, 3f), R(2.4f, 3.2f), V3(-1, 0, 0));
            RockCluster(Spot(a, -2f, 2f, 1.6f), 2.5f, 3);
            RuinWall(Spot(a, 13f, -3f, k_Plateau), 90f, 6f, 2.8f);
            BrokenColumn(Spot(a, 11f, 5f, k_Plateau), 4.5f);
            BrokenColumn(Spot(a, 14f, 8f, k_Plateau), 2.5f);
            Perch(a, Spot(a, -13f, 2f, 0f), 0f);
            Perch(a, Spot(a, 5f, -10f, 1.6f), 0f);
            Perch(a, Spot(a, 15f, 3f, k_Plateau), 0f);
            Perch(a, Spot(a, 9f, 10f, k_Plateau), 0f);
            ArenaTrees(a, 8);
        }

        // 6 Templo Engolido: pátio de lajes quebradas (a mata tomou partes dele) em volta de um pódio de pedra de
        // 2,4 m no meio da arena, com escadarias ao sul e ao norte, colunatas dos dois lados, um altar e muros partidos
        const int k_TempleX0 = 77, k_TempleX1 = 91, k_TempleZ0 = 264, k_TempleZ1 = 278;
        const float k_Podium = 2.4f;

        static void ShapeTemple(Arena a)
        {
            foreach (Vector2Int cell in Cells(a))
            {
                int x = cell.x, z = cell.y;
                bool podium = x >= k_TempleX0 && x <= k_TempleX1 && z >= k_TempleZ0 && z <= k_TempleZ1;
                bool court = x >= k_TempleX0 - 7 && x <= k_TempleX1 + 7 && z >= k_TempleZ0 - 7 && z <= k_TempleZ1 + 7;
                if (podium)
                {
                    s_Kind[x, z] = Kind.Stone;
                    s_Height[x, z] = k_Podium;
                }
                // Lajes do pátio, com buracos de capim (mais perto da borda do pátio, mais buracos)
                else if (court && Mathf.PerlinNoise(x * 0.31f + 3f, z * 0.31f + 7f) > 0.3f +
                         0.25f * Mathf.InverseLerp(4f, 7f, Mathf.Max(Mathf.Max(k_TempleX0 - x, x - k_TempleX1),
                             Mathf.Max(k_TempleZ0 - z, z - k_TempleZ1))))
                    s_Kind[x, z] = Kind.Stone;
            }
        }

        static void DressTemple(Arena a)
        {
            float cx = (k_TempleX0 + k_TempleX1 + 1) * 0.5f * k_Cell;
            float south = k_TempleZ0 * k_Cell, north = (k_TempleZ1 + 1) * k_Cell;
            StoneStairs(new Vector3(cx, k_Podium, south), Vector3.back, 0f, k_Podium, 8f);
            StoneStairs(new Vector3(cx, k_Podium, north), Vector3.forward, 0f, k_Podium, 8f);
            a.Reserved.Add(new Vector3(cx, 0f, south - 3f));
            a.Reserved.Add(new Vector3(cx, 0f, north + 3f));

            // Colunatas nos dois lados do pódio (inteiras e partidas) e as duas colunas altas da entrada
            for (int z = k_TempleZ0 + 2; z <= k_TempleZ1 - 1; z += 3)
            foreach (int x in new[] { k_TempleX0 + 2, k_TempleX1 - 2 })
            {
                bool broken = s_Random.NextDouble() < 0.5;
                ArquipelagoRuinKit.Column(s_Ruins, CellToWorld(new Vector2(x + 0.5f, z + 0.5f), k_Podium),
                    broken ? R(2f, 4f) : 6.5f, 0.75f, s_Random, broken);
            }

            foreach (float side in new[] { -1f, 1f })
                ArquipelagoRuinKit.Column(s_Ruins, new Vector3(cx + side * 6f, 0f, south - 2.5f), 7f, 0.85f, s_Random,
                    false);

            // Altar no meio, com a laje de cima partida
            Vector3 altar = new Vector3(cx, k_Podium, (south + north) * 0.5f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), altar + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 2.6f),
                Quaternion.identity, 0.12f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), altar + new Vector3(0.4f, 1.45f, 0.1f), new Vector3(3.2f, 0.5f, 2f),
                Quaternion.Euler(0f, 8f, 4f), 0.08f);
            BlockMesh.ChamferBox(s_Ruins.Pick(), altar + new Vector3(2.8f, 0.3f, 1.2f), new Vector3(1.4f, 0.5f, 1.1f),
                Quaternion.Euler(14f, 30f, 6f), 0.08f);
            a.Reserved.Add(altar);

            // Muros partidos nos cantos do pódio e no pátio
            RuinWall(CellToWorld(new Vector2(k_TempleX0 + 1.5f, k_TempleZ1 - 1f), k_Podium), 0f, 6f, 3f);
            RuinWall(CellToWorld(new Vector2(k_TempleX1 - 0.5f, k_TempleZ0 + 2f), k_Podium), 90f, 5f, 2.6f);
            RuinWall(Spot(a, -12f, 4f, 0f), 90f, 7f, 2.6f);
            RuinWall(Spot(a, 12f, -4f, 0f), 90f, 6f, 2.2f);
            BrokenColumn(Spot(a, -11f, -8f, 0f), 3.5f);
            BrokenColumn(Spot(a, 11f, 10f, 0f), 4f);
            RockPile(Spot(a, -14f, -2f, 0f), R(2.4f, 3f), R(2.6f, 3.4f), V3(1, 0, 0));
            RockCluster(Spot(a, 14f, 6f, 0f), 2.5f, 3);
            Perch(a, Spot(a, -15f, -4f, 0f), 0f);
            Perch(a, Spot(a, 15f, 4f, 0f), 0f);
            Perch(a, Spot(a, -12f, 11f, 0f), 0f);
            Perch(a, Spot(a, 12f, -11f, 0f), 0f);
            ArenaTrees(a, 6);
        }

        // 7 Portal: uma muralha baixa de terra e pedra (1,6 m) atravessa a arena de leste a oeste, cortada no meio por
        // uma passagem de lajes sob um arco colossal inteiro; duas rampas sobem do sul para cima da muralha. Ao norte, a
        // passagem segue como uma avenida de lajes entre colunas partidas até a saída, que leva ao chefe.
        const float k_Ridge = 1.6f;

        static void ShapePortal(Arena a)
        {
            float south = a.Center.y - 3f, north = a.Center.y + 4f;
            foreach (Vector2Int cell in Cells(a))
            {
                float x = cell.x + 0.5f, z = cell.y + 0.5f;
                bool passage = Mathf.Abs(x - a.Center.x - 0.5f) <= 3.5f;
                bool ridge = z > south + 0.8f * Mathf.Sin(x * 0.4f) && z < north + 0.8f * Mathf.Sin(x * 0.33f + 1f);
                if (passage && z > south - 1f)
                    s_Kind[cell.x, cell.y] = Kind.Stone;
                else if (ridge)
                    s_Height[cell.x, cell.y] = k_Ridge;
            }

            AddRamp(a, a.Center + new Vector2(-11f, 0f), V(0, -1), 4, 1, 0f, k_Ridge, false);
            AddRamp(a, a.Center + new Vector2(11f, 0f), V(0, -1), 4, 1, 0f, k_Ridge, false);
        }

        static void DressPortal(Arena a)
        {
            // Arco colossal inteiro sobre a passagem: dois pilares na muralha e o arco de blocos por cima
            var gate = CellToWorld(a.Center + new Vector2(0.5f, 0.5f), k_Ridge);
            const float span = 15f, pillar = 4f, height = 15f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 foot = gate + Vector3.right * side * (span * 0.5f + pillar * 0.5f) + Vector3.down * 0.6f;
                ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), foot + Vector3.up * (height * 0.5f),
                    new Vector2(pillar, pillar * 1.2f), height, Quaternion.identity, pillar * 0.08f);
                ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), foot + Vector3.up * 1.6f,
                    new Vector2(pillar * 1.3f, pillar * 1.5f), 1.4f, Quaternion.identity, pillar * 0.1f);
                ArquipelagoRuinKit.Pillar(s_Ruins.Pick(), foot + Vector3.up * (height - 0.6f),
                    new Vector2(pillar * 1.3f, pillar * 1.5f), 1.2f, Quaternion.identity, pillar * 0.1f);
                ArquipelagoRuinKit.VinesOnBlock(foot + Vector3.up * (height * 0.5f), new Vector2(pillar, pillar * 1.2f),
                    height, Quaternion.identity, Vector3.back, s_Random, 0.8f);
                a.Reserved.Add(new Vector3(foot.x, gate.y, foot.z));
            }

            ArquipelagoRuinKit.RichRing(s_Ruins, gate + Vector3.up * (height - 0.6f), Vector3.forward, Vector3.right,
                span * 0.5f + pillar * 0.5f, pillar, pillar * 1.2f, 0f, 180f, s_Random, false, false, 0f, 4);

            // Muros partidos em cima da muralha e colunas partidas ladeando a avenida até a saída
            RuinWall(Spot(a, -8f, 0.5f, k_Ridge), 90f, 6f, 2.4f);
            RuinWall(Spot(a, 8f, 0.5f, k_Ridge), 90f, 6f, 2.4f);
            RuinWall(Spot(a, -14f, 1f, k_Ridge), 90f, 4f, 1.8f);
            for (float dz = 6f; dz < 13f; dz += 3f)
            foreach (float side in new[] { -1f, 1f })
                ArquipelagoRuinKit.Column(s_Ruins, Spot(a, side * 4.6f + 0.5f, dz, 0f),
                    s_Random.NextDouble() < 0.5 ? R(2f, 4f) : 6f, 0.7f, s_Random, true);

            RockPile(Spot(a, -9f, -8f, 0f), R(2.4f, 3f), R(2.6f, 3.4f), V3(1, 0, 0));
            RockPile(Spot(a, 10f, -9f, 0f), R(2.4f, 3f), R(2.6f, 3.4f), V3(-1, 0, 0));
            RockCluster(Spot(a, -11f, 8f, 0f), 2.5f, 3);
            RockCluster(Spot(a, 12f, 9f, 0f), 2.5f, 3);
            Perch(a, Spot(a, -15f, -4f, 0f), 0f);
            Perch(a, Spot(a, 15f, -3f, 0f), 0f);
            Perch(a, Spot(a, -12f, 2f, k_Ridge), 0f);
            Perch(a, Spot(a, 13f, 2f, k_Ridge), 0f);
            ArenaTrees(a, 6);
        }

        // 8 Clareira de Anéis (chefe, Rei Fungo): clareira redonda e plana (as ondas de choque do chefe cruzam o chão),
        // com anéis de lajes no chão e um degrau de ruína de 1,2 m em volta (duas rampas) e pedras em pé formando um
        // círculo em cima dele; portas ao sul e ao norte (a saída segue até a borda norte do mapa)
        static float RingDistance(Arena a, Vector2Int cell)
        {
            var d = new Vector2((cell.x + 0.5f - a.Center.x) / a.RadiusX, (cell.y + 0.5f - a.Center.y) / a.RadiusZ);
            return d.magnitude;
        }

        const float k_RingStep = 1.2f;

        static void ShapeRings(Arena a)
        {
            foreach (Vector2Int cell in Cells(a))
            {
                float q = RingDistance(a, cell);
                bool door = Mathf.Abs(cell.x + 0.5f - a.Center.x - 0.5f) <= 2.5f;
                if (q > 0.84f && !door)
                {
                    s_Kind[cell.x, cell.y] = Kind.Stone;
                    s_Height[cell.x, cell.y] = k_RingStep;
                }
                else if (q < 0.12f || q > 0.3f && q < 0.36f || q > 0.6f && q < 0.66f)
                    s_Kind[cell.x, cell.y] = Kind.Stone;
            }

            AddRamp(a, a.Center + new Vector2(-a.RadiusX * 0.95f, 0f), V(1, 0), 3, 1, 0f, k_RingStep, false);
            AddRamp(a, a.Center + new Vector2(a.RadiusX * 0.95f, 0f), V(-1, 0), 3, 1, 0f, k_RingStep, false);
        }

        static void DressRings(Arena a)
        {
            // Pedras em pé em volta, em cima do degrau (menos na frente das portas e das rampas)
            for (int i = 0; i < 18; i++)
            {
                float angle = i * 20f + R(-4f, 4f);
                if (Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) < 14f || Mathf.Abs(Mathf.DeltaAngle(angle, 270f)) < 14f ||
                    Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 12f || Mathf.Abs(Mathf.DeltaAngle(angle, 180f)) < 12f)
                    continue;
                float t = angle * Mathf.Deg2Rad;
                Vector3 foot = Spot(a, Mathf.Cos(t) * a.RadiusX * 0.92f, Mathf.Sin(t) * a.RadiusZ * 0.92f, k_RingStep);
                float height = R(3.5f, 6.5f);
                Quaternion yaw = Quaternion.Euler(R(-4f, 4f), -angle + R(-10f, 10f), R(-4f, 4f));
                var footprint = new Vector2(R(1.6f, 2.2f), R(1f, 1.4f));
                if (s_Random.NextDouble() < 0.25)
                {
                    // Caída
                    BlockMesh.ChamferBox(s_Rocks.Pick(), foot + Vector3.up * 0.5f, new Vector3(footprint.x, 1f, height),
                        yaw * Quaternion.Euler(0f, 90f, 0f), 0.15f);
                    continue;
                }

                ArquipelagoRuinKit.Pillar(s_Rocks.Pick(), foot + Vector3.up * (height * 0.5f - 0.3f), footprint, height,
                    yaw, 0.18f);
                ArquipelagoRuinKit.VinesOnBlock(foot + Vector3.up * (height * 0.5f), footprint, height, yaw,
                    Spot(a, 0f, 0f, 0f) - foot, s_Random, 0.6f);
            }

            Perch(a, Spot(a, -a.RadiusX * 0.9f, a.RadiusZ * 0.35f, k_RingStep), 0f);
            Perch(a, Spot(a, a.RadiusX * 0.9f, -a.RadiusZ * 0.35f, k_RingStep), 0f);
            Perch(a, Spot(a, a.RadiusX * 0.62f, a.RadiusZ * 0.7f, k_RingStep), 0f);
        }

        // ---------- geração ----------

        [MenuItem("Ícaro/Bosque/Gerar Bosque", priority = 43)]
        static void GenerateScene() => Generate(k_SceneName, k_SceneName, false, null);

        // Gera o Bosque na cena 'sceneName' (malhas com o prefixo 'prefix'). Com 'openNorth', a trilha da saída do
        // chefe chega à borda norte do mapa sem parede nem floresta na frente (OpenEndPoint: o próximo trecho da Via
        // continua dali) e a parede preta do fundo fecha a borda norte nos dois lados. 'extend' monta mais coisas na
        // mesma cena antes de salvar (o desfiladeiro e o Arquipélago).
        public static void Generate(string sceneName, string prefix, bool openNorth,
            System.Action<Scene, Transform> extend)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            s_SceneName = sceneName;
            s_Prefix = prefix;
            s_OpenNorth = openNorth;

            try
            {
                EditorUtility.DisplayProgressBar("Bosque", "Texturas", 0.1f);
                s_Theme = GridThemes.Bosque();
                s_Theme.SceneName = s_SceneName;
                s_Forest = s_Theme.Materials;
                CharcoTextureKit.AddWater(s_Forest);
                s_Stone = ArquipelagoTextureKit.Build();
                ArquipelagoBuilder.UseMaterials(s_Stone);
                Material sky = GridSceneKit.NightSkyMaterial(s_Theme);
                s_Random = new System.Random(2027);

                // Os troncos e raízes saíram: apaga as malhas que as gerações anteriores deixaram
                foreach (string guid in AssetDatabase.FindAssets("Arquipelago_Bosque_Troncos", new[] { GridSceneKit.MeshFolder }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

                EditorUtility.DisplayProgressBar("Bosque", "Mapa", 0.2f);
                BuildMap();

                EditorUtility.DisplayProgressBar("Bosque", "Cena", 0.3f);
                BiomePalettes.Current = BiomePalettes.Bosque;
                VisualSettingsBuilder.Result visuals = VisualSettingsBuilder.Apply(BiomePalettes.Bosque);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform systems = ViaAeonidaBuilder.BuildSystems(scene, visuals);
                GridSceneKit.ApplyNight(s_Theme, sky, systems);

                s_World = new GameObject("Mapa").transform;
                GridSceneKit.ActiveBatch = new GridSceneKit.BillboardBatch(s_World);
                s_Rocks = new RuinMesh(41, 1);
                s_Ruins = new RuinMesh(42);
                ArquipelagoRuinKit.Vines = new GridSceneKit.Cards();
                ArquipelagoRuinKit.VineMaterial = s_Stone.Vine;
                ArquipelagoRuinKit.VineSize = s_Stone.VineSize;

                BuildTerrain();
                BuildRamps();
                AddWaterMap();
                BankRocks();
                BuildEscapePod();
                foreach (Arena arena in s_Arenas)
                    arena.Dress(arena);

                EditorUtility.DisplayProgressBar("Bosque", "Floresta", 0.45f);
                int forest = PlaceForest();
                ArquipelagoBuilder.CreateRuin(s_Prefix + "_Rochas", s_Rocks, new[] { s_Forest.Rock }, s_World, true, -100f);
                ArquipelagoBuilder.CreateRuin(s_Prefix + "_Ruinas", s_Ruins, s_Stone.StoneTones, s_World, true, -100f);
                ArquipelagoRuinKit.Vines.Build(s_Prefix + "_Trepadeiras", s_World);
                ArquipelagoRuinKit.Vines = null;

                var arenasRoot = new GameObject("Arenas").transform;
                arenasRoot.SetParent(s_World, false);
                var rooms = new List<(Arena arena, RoomManager room, NavMeshSurface surface)>();
                foreach (Arena arena in s_Arenas)
                    rooms.Add((arena, BuildRoom(arena, arenasRoot, out NavMeshSurface surface), surface));

                PlacePlayer(scene);
                BiomeZone zone = GridSceneKit.AddBiomeZone(s_Theme, CellToWorld(k_StartCenter + new Vector2(0f, 3f)),
                    new Vector3(20f, 12f, 2f), s_World);
                AddDebugTeleport(systems, zone);
                extend?.Invoke(scene, s_World);

                GridSceneKit.ActiveBatch.Build(s_Prefix + "_Sprite");
                GridSceneKit.ActiveBatch = null;
                GridSceneKit.AddOcclusionArea("Oclusao_Bosque", s_World,
                    new Vector3(k_Width * k_Cell * 0.5f, 10f, s_Depth * k_Cell * 0.5f),
                    new Vector3(k_Width * k_Cell, 30f, s_Depth * k_Cell));
                GridSceneKit.AddDistanceCulling(s_World, 300f, "Mar", "Gigantes_Fundo", "Feixe_Farol", "Destroco_",
                    "AnelGirando");

                Physics.SyncTransforms();
                int perches = 0, valid = 0;
                foreach ((Arena arena, RoomManager room, NavMeshSurface _) in rooms)
                    room.HighSpawnPoints = PlaceHighPoints(arena, room.transform, ref perches, ref valid);

                EditorSceneManager.SaveScene(scene, ScenePath);
                int spawns = 0;
                for (int i = 0; i < rooms.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Bosque", $"NavMesh e spawns: {rooms[i].arena.Name}",
                        0.6f + 0.35f * i / rooms.Count);
                    GridSceneKit.BakeNavMesh(rooms[i].surface, NavMeshFolder, $"Arena_{rooms[i].arena.Number}");
                    spawns += PlaceSpawns(rooms[i].arena, rooms[i].room);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorUtility.DisplayProgressBar("Bosque", "Oclusão (alguns minutos)", 0.97f);
                GridSceneKit.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ícaro] Bosque gerado em {ScenePath}: {s_Arenas.Length} salas, {forest} árvores no cerco, " +
                          $"{valid} de {perches} poleiros com linha de visão, {spawns} pontos de spawn. Dê Play nessa " +
                          "cena (F6/F7 pulam entre as salas).");
            }
            finally
            {
                GridSceneKit.ActiveBatch = null;
                ArquipelagoRuinKit.Vines = null;
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------- mapa ----------

        static bool InMap(int x, int z) => x >= 0 && z >= 0 && x < k_Width && z < s_Depth;

        static Vector3 CellToWorld(Vector2 cell, float y = 0f) => new Vector3(cell.x * k_Cell, y, cell.y * k_Cell);

        static Vector3 CellCenter(Vector2Int cell) =>
            GridSceneKit.CellCenter(cell.x, cell.y) + Vector3.up * s_Height[cell.x, cell.y];

        static Vector3 V3(float x, float y, float z) => new Vector3(x, y, z);

        // Ponto da sala em células a partir do centro, na altura dada
        static Vector3 Spot(Arena a, float dx, float dz, float y) => CellToWorld(a.Center + new Vector2(dx, dz), y);

        static bool InEllipse(Vector2 cell, Vector2 center, float rx, float rz, float noise, float phase)
        {
            Vector2 d = cell - center;
            var q = new Vector2(d.x / rx, d.y / rz);
            float angle = Mathf.Atan2(q.y, q.x);
            float limit = 1f + noise * (Mathf.Sin(3f * angle + phase) + 0.7f * Mathf.Sin(5f * angle + 2f * phase));
            return q.magnitude <= limit;
        }

        static IEnumerable<Vector2Int> Cells(Arena a)
        {
            for (int x = a.Bounds.xMin; x < a.Bounds.xMax; x++)
            for (int z = a.Bounds.yMin; z < a.Bounds.yMax; z++)
            {
                if (s_Owner[x, z] == a.Index + 1)
                    yield return V(x, z);
            }
        }

        static bool IsWalkable(Kind kind) =>
            kind == Kind.Floor || kind == Kind.Path || kind == Kind.Shallow || kind == Kind.Stone;

        static bool Walkable(int x, int z) => InMap(x, z) && IsWalkable(s_Kind[x, z]);

        static bool IsWater(Kind kind) => kind == Kind.Shallow || kind == Kind.Deep;

        static void BuildMap()
        {
            s_Arenas = Arenas();
            float top = 0f;
            foreach (Arena arena in s_Arenas)
                top = Mathf.Max(top, arena.Center.y + arena.RadiusZ * 1.3f);
            s_Depth = Mathf.CeilToInt(top) + 70;
            s_Kind = new Kind[k_Width, s_Depth];
            s_Height = new float[k_Width, s_Depth];
            s_Owner = new int[k_Width, s_Depth];
            s_Ramp = new int[k_Width, s_Depth];
            s_Ramps.Clear();

            // Clareira da queda (onde o jogador começa)
            for (int x = 0; x < k_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                if (InEllipse(new Vector2(x + 0.5f, z + 0.5f), k_StartCenter, 9f, 7f, 0.08f, 0.5f))
                    s_Kind[x, z] = Kind.Floor;
            }

            // Salas: elipses com o contorno ondulado; entrada e saída nas colunas das trilhas
            for (int i = 0; i < s_Arenas.Length; i++)
            {
                Arena a = s_Arenas[i];
                a.Index = i;
                int reachX = Mathf.CeilToInt(a.RadiusX * 1.3f), reachZ = Mathf.CeilToInt(a.RadiusZ * 1.3f);
                a.Bounds = new RectInt(Mathf.FloorToInt(a.Center.x) - reachX, Mathf.FloorToInt(a.Center.y) - reachZ,
                    reachX * 2 + 1, reachZ * 2 + 1);
                for (int x = a.Bounds.xMin; x < a.Bounds.xMax; x++)
                for (int z = a.Bounds.yMin; z < a.Bounds.yMax; z++)
                {
                    if (!InMap(x, z) ||
                        !InEllipse(new Vector2(x + 0.5f, z + 0.5f), a.Center, a.RadiusX, a.RadiusZ, a.Noise, a.Phase))
                        continue;
                    s_Kind[x, z] = Kind.Floor;
                    s_Owner[x, z] = i + 1;
                }

                int ez = Mathf.FloorToInt(a.Center.y);
                while (s_Owner[a.EntranceX, ez - 1] == i + 1)
                    ez--;
                a.Entrance = V(a.EntranceX, ez);
                int xz = Mathf.FloorToInt(a.Center.y);
                while (s_Owner[a.ExitX, xz + 1] == i + 1)
                    xz++;
                a.Exit = V(a.ExitX, xz);
            }

            // Trilhas
            foreach (Vector2[] trail in Trails(s_Arenas))
            {
                for (int k = 0; k + 1 < trail.Length; k++)
                    CarveTrail(trail[k], trail[k + 1]);
            }

            // Relevo e água de cada sala
            foreach (Arena arena in s_Arenas)
                arena.Shape(arena);

            Arena boss = s_Arenas[s_Arenas.Length - 1];
            OpenEndStart = GridSceneKit.CellCenter(boss.Exit.x, boss.Exit.y);
            OpenEndPoint = GridSceneKit.CellCenter(boss.Exit.x, s_Depth - 1) + Vector3.forward * (k_Cell * 0.5f);

            ComputeDistances();
        }

        // Trilha reta (horizontal ou vertical) de 3 células de largura, sem entrar nas salas
        static void CarveTrail(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            int steps = Mathf.CeilToInt(d.magnitude);
            Vector2 side = new Vector2(-d.normalized.y, d.normalized.x);
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = from + d * (steps == 0 ? 0f : (float)s / steps);
                for (int w = -1; w <= 1; w++)
                {
                    int x = Mathf.FloorToInt(p.x + side.x * w), z = Mathf.FloorToInt(p.y + side.y * w);
                    // Cantos: um pouco mais largos
                    for (int ex = 0; ex <= (s == 0 || s == steps ? 1 : 0); ex++)
                    {
                        int cx = x + (side.x == 0 ? ex * (w >= 0 ? 1 : -1) : 0);
                        int cz = z + (side.y == 0 ? ex * (w >= 0 ? 1 : -1) : 0);
                        if (InMap(cx, cz) && s_Owner[cx, cz] == 0 && s_Kind[cx, cz] != Kind.Floor)
                            s_Kind[cx, cz] = Kind.Path;
                    }
                }
            }
        }

        // Terraço numa elipse dentro da sala (só as células da sala)
        static void Terrace(Arena a, Vector2 center, float rx, float rz, float height, int seed)
        {
            foreach (Vector2Int cell in Cells(a))
            {
                if (s_Kind[cell.x, cell.y] == Kind.Floor &&
                    InEllipse(new Vector2(cell.x + 0.5f, cell.y + 0.5f), center, rx, rz, 0.08f, seed))
                    s_Height[cell.x, cell.y] = height;
            }
        }

        // Rampa de terra descendo de um terraço: anda do 'center' na direção 'dir' até a beira, e dali a rampa ocupa
        // 'length' células (largura de 2 x halfWidth + 1). A beira fica reta na largura da rampa (as células do
        // terraço que passam dela viram rampa; as que faltam são preenchidas), e as faixas dos lados ficam no nível
        // de baixo.
        static void AddRamp(Arena a, Vector2 center, Vector2Int dir, int length, int halfWidth, float low, float high,
            bool path)
        {
            var side = new Vector2Int(-dir.y, dir.x);
            var start = new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
            int edge = 0;
            while (edge < 60 && s_Height[start.x + dir.x * (edge + 1), start.y + dir.y * (edge + 1)] >= high - 0.01f)
                edge++;
            var ramp = new Ramp
            {
                Edge = start + dir * edge, Dir = dir, Length = length, HalfWidth = halfWidth, Low = low, High = high,
                Path = path,
            };
            s_Ramps.Add(ramp);
            int index = s_Ramps.Count;
            for (int k = -halfWidth - 1; k <= halfWidth + 1; k++)
            {
                Vector2Int lane = ramp.Edge + side * k;
                bool inRamp = Mathf.Abs(k) <= halfWidth;
                // Beira reta: as duas células antes da beira no nível de cima (só na largura da rampa)
                for (int r = -1; r <= 0 && inRamp; r++)
                {
                    Vector2Int c = lane + dir * r;
                    if (InMap(c.x, c.y) && s_Owner[c.x, c.y] == a.Index + 1)
                        s_Height[c.x, c.y] = high;
                }

                for (int r = 1; r <= length + 1; r++)
                {
                    Vector2Int c = lane + dir * r;
                    if (!InMap(c.x, c.y) || s_Owner[c.x, c.y] != a.Index + 1)
                        continue;
                    s_Height[c.x, c.y] = low;
                    if (s_Kind[c.x, c.y] != Kind.Floor && s_Kind[c.x, c.y] != Kind.Path)
                        s_Kind[c.x, c.y] = Kind.Floor;
                    if (inRamp && r <= length)
                        s_Ramp[c.x, c.y] = index;
                }
            }

            a.Reserved.Add(CellToWorld(ramp.Edge + new Vector2(0.5f, 0.5f) + (Vector2)dir * (length * 0.5f + 0.5f),
                (low + high) * 0.5f));
        }

        // Distância de cada célula até o espaço andável e a célula andável mais próxima; o chão da floresta fica na
        // altura dela (as células de água ficam na altura da margem)
        static void ComputeDistances()
        {
            int w = k_Width + 2 * k_Margin, d = s_Depth + 2 * k_Margin;
            s_Distance = new int[w, d];
            s_Nearest = new Vector2Int[w, d];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                s_Distance[x, z] = int.MaxValue;
                int mx = x - k_Margin, mz = z - k_Margin;
                if (!InMap(mx, mz) || !IsWalkable(s_Kind[mx, mz]))
                    continue;
                s_Distance[x, z] = 0;
                s_Nearest[x, z] = V(mx, mz);
                queue.Enqueue(V(x, z));
            }

            Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int step in dirs)
                {
                    Vector2Int n = c + step;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= d ||
                        s_Distance[n.x, n.y] <= s_Distance[c.x, c.y] + 1)
                        continue;
                    s_Distance[n.x, n.y] = s_Distance[c.x, c.y] + 1;
                    s_Nearest[n.x, n.y] = s_Nearest[c.x, c.y];
                    int mx = n.x - k_Margin, mz = n.y - k_Margin;
                    if (InMap(mx, mz) && s_Kind[mx, mz] == Kind.Void)
                        s_Height[mx, mz] = NearestGround(s_Nearest[n.x, n.y]);
                    queue.Enqueue(n);
                }
            }
        }

        // Altura do chão da floresta perto de uma célula andável (a água vale como a margem)
        static float NearestGround(Vector2Int walkable) =>
            s_Kind[walkable.x, walkable.y] == Kind.Shallow ? 0f : s_Height[walkable.x, walkable.y];

        // Distância (células) até o espaço andável, também além da grade
        static int Dist(int x, int z)
        {
            int i = x + k_Margin, j = z + k_Margin;
            return i < 0 || j < 0 || i >= s_Distance.GetLength(0) || j >= s_Distance.GetLength(1)
                ? int.MaxValue
                : s_Distance[i, j];
        }

        static Vector2Int NearestOf(int x, int z) => s_Nearest[x + k_Margin, z + k_Margin];

        static Kind KindAt(int x, int z) => InMap(x, z) ? s_Kind[x, z] : Kind.Void;

        static bool RampAt(int x, int z) => InMap(x, z) && s_Ramp[x, z] != 0;

        // Além da borda norte, com o norte aberto (ali fica o desfiladeiro da Via)
        static bool Beyond(int z) => s_OpenNorth && z >= s_Depth;

        // Altura do chão, também além da grade (o da célula andável mais próxima)
        static float GroundAt(int x, int z)
        {
            if (InMap(x, z))
                return s_Height[x, z];
            return Dist(x, z) == int.MaxValue ? 0f : NearestGround(NearestOf(x, z));
        }

        // ---------- terreno ----------

        static void BuildTerrain()
        {
            var grass = new MeshData();
            var farGrass = new MeshData();
            var path = new MeshData();
            var cliffs = new MeshData();
            var bed = new MeshData();
            var banks = new MeshData();
            var shallow = new MeshData();
            var barrier = new MeshData();
            var stone = new MeshData();
            var stoneSides = new MeshData();
            Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            for (int x = -k_Margin; x < k_Width + k_Margin; x++)
            for (int z = -k_Margin; z < s_Depth + k_Margin; z++)
            {
                Kind kind = KindAt(x, z);
                if (kind == Kind.Void && Dist(x, z) > k_FloorDepth || Beyond(z))
                    continue;
                if (RampAt(x, z))
                    continue;
                float h = GroundAt(x, z);
                if (IsWater(kind))
                {
                    bed.AddHorizontal(x, z, h);
                    shallow.AddHorizontal(x, z, h + k_RiverSurface - k_RiverBed);
                }
                else if (kind == Kind.Stone)
                    stone.AddHorizontal(x, z, h);
                else if (kind == Kind.Path)
                    path.AddHorizontal(x, z, h);
                else if (kind == Kind.Floor || Dist(x, z) <= 2)
                    grass.AddHorizontal(x, z, h);
                else
                    farGrass.AddHorizontal(x, z, h);

                foreach (Vector2Int d in dirs)
                {
                    int nx = x + d.x, nz = z + d.y;
                    Kind neighborKind = KindAt(nx, nz);
                    if (neighborKind == Kind.Void && Dist(nx, nz) > k_FloorDepth || Beyond(nz))
                        continue;
                    // As rampas fazem os próprios lados
                    if (RampAt(nx, nz))
                        continue;
                    float neighbor = GroundAt(nx, nz);
                    if (neighbor < h - 0.01f)
                        (kind == Kind.Stone ? stoneSides : h - neighbor > 0.8f ? cliffs : banks).AddSideRange(x, z, d,
                            neighbor, h);

                    // Borda invisível entre o espaço andável e o resto (faces dos dois lados)
                    if (IsWalkable(kind) && !IsWalkable(neighborKind))
                    {
                        float bottom = Mathf.Min(h, neighbor) - 1f, top = Mathf.Max(h, neighbor) + k_Barrier;
                        barrier.AddSideRange(x, z, d, bottom, top);
                        barrier.AddSideRange(nx, nz, -d, bottom, top);
                    }
                }
            }

            GridSceneKit.CreateObject(s_Prefix + "_Grama", "Chao", grass, s_Forest.Grass, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Grama_Longe", "Chao_Longe", farGrass, s_Forest.Grass, s_World,
                false);
            GridSceneKit.CreateObject(s_Prefix + "_Trilha", "Trilha", path, s_Forest.Path, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Barrancos", "Barrancos", cliffs, s_Forest.Rock, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Lajes", "Lajes", stone, s_Stone.Stone, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Lajes_Lados", "Lajes_Lados", stoneSides, s_Stone.Stone, s_World,
                true);
            GridSceneKit.CreateObject(s_Prefix + "_Leito", "Leito", bed, s_Forest.WaterBed, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Margens", "Margens", banks, s_Forest.WaterBed, s_World, true);
            GameObject shallowSurface = GridSceneKit.CreateObject(s_Prefix + "_Agua_Rasa", "Agua_Rasa", shallow,
                s_Forest.WaterSurface, s_World, false, false, false);
            if (shallowSurface)
                shallowSurface.AddComponent<WaterRipple>();
            GameObject edge = GridSceneKit.CreateObject(s_Prefix + "_Borda", "Borda_Invisivel", barrier, null,
                s_World, true, true, false);
            if (edge)
                edge.AddComponent<IgnoreHitDetection>();
        }

        // Rampas: topo inclinado (capim ou terra) e os lados em rocha até o nível de baixo
        static void BuildRamps()
        {
            var tops = new MeshData();
            var paths = new MeshData();
            var sides = new MeshData();
            foreach (Ramp r in s_Ramps)
            {
                var dir = new Vector3(r.Dir.x, 0f, r.Dir.y);
                Vector3 side = Vector3.Cross(Vector3.up, dir) * ((r.HalfWidth + 0.5f) * k_Cell);
                Vector3 top = GridSceneKit.CellCenter(r.Edge.x, r.Edge.y) + dir * (k_Cell * 0.5f);
                Vector3 bottom = top + dir * (r.Length * k_Cell);
                Vector3 a = top - side + Vector3.up * r.High, b = top + side + Vector3.up * r.High;
                Vector3 c = bottom + side + Vector3.up * r.Low, d = bottom - side + Vector3.up * r.Low;
                Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                if (normal.y < 0f)
                    normal = -normal;
                MeshData surface = r.Path ? paths : tops;
                surface.AddTriangleAuto(a, b, c, normal);
                surface.AddTriangleAuto(a, c, d, normal);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 edgeTop = top + side * s;
                    sides.AddTriangleAuto(edgeTop + Vector3.up * r.Low, edgeTop + Vector3.up * r.High,
                        bottom + side * s + Vector3.up * r.Low, side * s);
                }
            }

            GridSceneKit.CreateObject(s_Prefix + "_Rampas", "Rampas", tops, s_Forest.Grass, s_World, true);
            GridSceneKit.CreateObject(s_Prefix + "_Rampas_Trilha", "Rampas_Trilha", paths, s_Forest.Path, s_World,
                true);
            GridSceneKit.CreateObject(s_Prefix + "_Rampas_Lados", "Rampas_Lados", sides, s_Forest.Rock, s_World,
                true);
        }

        static void AddWaterMap()
        {
            var shallow = new bool[k_Width * s_Depth];
            bool any = false;
            for (int x = 0; x < k_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                if (s_Kind[x, z] != Kind.Shallow)
                    continue;
                shallow[z * k_Width + x] = true;
                any = true;
            }

            if (!any)
                return;
            var map = new GameObject("AguaRasa").AddComponent<WaterMap>();
            map.transform.SetParent(s_World, false);
            map.Origin = Vector3.up * k_RiverBed;
            map.CellSize = k_Cell;
            map.Width = k_Width;
            map.Depth = s_Depth;
            map.Shallow = shallow;
            map.SlowFactor = s_Theme.WaterSlow;
        }

        // Pilhas de rocha encostadas nos barrancos dentro das salas (de 1,2 m para cima), longe das rampas, das
        // portas e do que já foi reservado
        static void BankRocks()
        {
            foreach (Arena a in s_Arenas)
            {
                Vector3 entrance = CellCenter(a.Entrance), exit = CellCenter(a.Exit);
                foreach (Vector2Int cell in Cells(a))
                {
                    if (s_Ramp[cell.x, cell.y] != 0 || s_Kind[cell.x, cell.y] == Kind.Stone || s_Random.NextDouble() > 0.4)
                        continue;
                    float h = s_Height[cell.x, cell.y];
                    foreach (Vector2Int d in new[] { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) })
                    {
                        Vector2Int n = cell + d;
                        if (!InMap(n.x, n.y) || s_Owner[n.x, n.y] != a.Index + 1 || s_Ramp[n.x, n.y] != 0 ||
                            s_Kind[n.x, n.y] != Kind.Floor)
                            continue;
                        float low = s_Height[n.x, n.y];
                        if (h - low < 1.2f)
                            continue;
                        Vector3 face = GridSceneKit.CellCenter(cell.x, cell.y) + new Vector3(d.x, 0f, d.y) * k_Cell * 0.5f;
                        var foot = new Vector3(face.x, low, face.z) + new Vector3(d.x, 0f, d.y) * R(0.2f, 0.7f) +
                                   new Vector3(d.y, 0f, d.x) * R(-0.6f, 0.6f);
                        if (Vector3.Distance(foot, entrance) < 8f || Vector3.Distance(foot, exit) < 8f ||
                            NearReserved(a, foot, 5f))
                            continue;
                        float width = R(1.8f, 3.2f);
                        RockPile(foot, width, h - low + R(0.2f, 1.4f), new Vector3(d.x, 0f, d.y));
                        break;
                    }
                }
            }
        }

        // Trepadeiras penduradas nas faces dos barrancos da sala com 'height' de altura (na face, a partir da beira de
        // cima, viradas para o lado de baixo), numa fração 'chance' das células da beira
        static void WallVines(Arena a, float height, float chance)
        {
            foreach (Vector2Int cell in Cells(a))
            {
                if (Mathf.Abs(s_Height[cell.x, cell.y] - height) > 0.01f || s_Ramp[cell.x, cell.y] != 0)
                    continue;
                foreach (Vector2Int d in new[] { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) })
                {
                    Vector2Int n = cell + d;
                    if (!InMap(n.x, n.y) || s_Owner[n.x, n.y] != a.Index + 1 || s_Ramp[n.x, n.y] != 0 ||
                        height - s_Height[n.x, n.y] < 1.2f || s_Random.NextDouble() > chance)
                        continue;
                    var normal = new Vector3(d.x, 0f, d.y);
                    Vector3 top = GridSceneKit.CellCenter(cell.x, cell.y) + normal * (k_Cell * 0.5f + 0.04f) +
                                  Vector3.Cross(Vector3.up, normal) * R(-0.6f, 0.6f) + Vector3.up * height;
                    ArquipelagoRuinKit.HangVine(top, normal, s_Random, R(0.8f, 1.1f));
                }
            }
        }

        // Reserva a linha inteira de uma peça (ponte): pontos a cada 3 m
        static void ReserveLine(Arena a, Vector3 from, Vector3 to)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 3f));
            for (int i = 0; i <= count; i++)
                a.Reserved.Add(Vector3.Lerp(from, to, (float)i / count));
        }

        static bool NearReserved(Arena a, Vector3 p, float distance)
        {
            foreach (Vector3 r in a.Reserved)
            {
                if (Vector2.Distance(new Vector2(r.x, r.z), new Vector2(p.x, p.z)) < distance)
                    return true;
            }

            return false;
        }

        // ---------- peças ----------

        // Pilha de rocha irregular: dois ou três blocos tortos que afinam para cima, a partir de 'foot', com musgo em
        // cima e trepadeiras nas faces viradas para 'view'
        static void RockPile(Vector3 foot, float width, float height, Vector3 view)
        {
            float y = foot.y - 0.6f;
            int parts = 2 + s_Random.Next(2);
            for (int i = 0; i < parts; i++)
            {
                float h = (height + 0.6f) / parts * R(0.8f, 1.2f);
                Quaternion tilt = Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f));
                var center = new Vector3(foot.x + R(-0.3f, 0.3f), y + h * 0.5f, foot.z + R(-0.3f, 0.3f));
                var footprint = new Vector2(width, width * R(0.75f, 1.1f));
                ArquipelagoRuinKit.Pillar(s_Rocks.Pick(), center, footprint, h, tilt, width * 0.15f);
                ArquipelagoRuinKit.VinesOnBlock(center, footprint, h, tilt, view, s_Random, 0.7f);
                y += h * 0.9f;
                width *= 0.75f;
            }

            ArquipelagoRuinKit.IrregularRock(s_Rocks, foot + new Vector3(R(-1f, 1f), 0.25f, R(-1f, 1f)) +
                                                      view.normalized * width, R(0.8f, 1.4f), s_Random);
        }

        // Três ou mais pedras irregulares juntas no chão (cobertura baixa)
        static void RockCluster(Vector3 center, float spread, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float size = i == 0 ? R(1.4f, 2f) : R(0.8f, 1.4f);
                Vector3 offset = i == 0 ? Vector3.zero : new Vector3(R(-spread, spread), 0f, R(-spread, spread));
                ArquipelagoRuinKit.IrregularRock(s_Rocks, center + offset + Vector3.up * (size * 0.3f), size,
                    s_Random);
            }
        }

        // Laje de pedra atravessando um vão (de 'from' a 'to', com o topo em 'top'; dá para andar em cima), com as pontas
        // apoiadas em pedras irregulares e um pedaço partido por baixo
        static void StoneSlab(Vector3 from, Vector3 to, float width, float top)
        {
            Vector3 axis = to - from;
            axis.y = 0f;
            float length = axis.magnitude + 4f;
            axis.Normalize();
            const float thickness = 1.3f;
            Vector3 middle = (from + to) * 0.5f;
            middle.y = top - thickness * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(axis) * Quaternion.Euler(0f, 0f, R(-2f, 2f));
            BlockMesh.ChamferBox(s_Rocks.Pick(), middle, new Vector3(width, thickness, length), rotation, 0.25f);
            BlockMesh.ChamferBox(s_Rocks.Pick(), middle + axis * R(-2f, 2f) - Vector3.up * (thickness * 0.7f),
                new Vector3(width * 0.7f, thickness * 0.6f, length * 0.35f), rotation * Quaternion.Euler(6f, 10f, 0f),
                0.2f);
            foreach (float s in new[] { -1f, 1f })
                ArquipelagoRuinKit.IrregularRock(s_Rocks, middle + axis * (s * length * 0.5f) + Vector3.down * 0.4f,
                    R(1.6f, 2.2f), s_Random);
        }

        // Escadaria de pedra descendo da beira de um pódio ('top' = meio da beira de cima, 'dir' = para onde desce):
        // degraus de 0,27 m com muretas dos lados e uma rampa invisível rente às quinas (a subida fica lisa)
        static void StoneStairs(Vector3 top, Vector3 dir, float low, float high, float width)
        {
            int steps = Mathf.Max(2, Mathf.RoundToInt((high - low) / 0.27f));
            float rise = (high - low) / steps, depth = 0.42f;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Quaternion rotation = Quaternion.LookRotation(dir);
            for (int k = 0; k < steps - 1; k++)
            {
                float y = high - (k + 1) * rise;
                Vector3 c = top + dir * (depth * (k + 0.5f));
                BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(c.x, (y + low - 0.3f) * 0.5f, c.z),
                    new Vector3(width, y - low + 0.3f, depth), rotation, 0.05f);
            }

            float run = depth * steps;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = right * s * (width * 0.5f + 0.3f);
                for (int k = 0; k < steps; k++)
                {
                    float y = high - k * rise;
                    Vector3 c = top + dir * (depth * (k + 0.5f)) + side;
                    BlockMesh.ChamferBox(s_Ruins.Pick(), new Vector3(c.x, (y + 0.5f + low - 0.3f) * 0.5f, c.z),
                        new Vector3(0.6f, y + 0.5f - low + 0.3f, depth + 0.02f), rotation, 0.05f);
                }
            }

            // Rampa invisível (só colisor), toda abaixo da linha das quinas
            Vector3 bottom = top + dir * run + Vector3.up * (low - high);
            Vector3 along = top - bottom;
            Vector3 normal = Vector3.Cross(along, Vector3.Cross(Vector3.up, along).normalized).normalized;
            var ramp = new GameObject("Rampa_Escada");
            ramp.transform.SetParent(s_World, false);
            ramp.transform.SetPositionAndRotation((top + bottom) * 0.5f - normal * 0.15f,
                Quaternion.LookRotation(along, normal));
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.3f, along.magnitude);
        }

        // Coluna partida da ruína engolida, com trepadeiras e tambores caídos em volta
        static void BrokenColumn(Vector3 foot, float height)
        {
            ArquipelagoRuinKit.Column(s_Ruins, foot, height, R(0.7f, 0.9f), s_Random, true);
            if (s_Random.NextDouble() < 0.6)
                ArquipelagoRuinKit.FallenDrums(s_Ruins, foot + new Vector3(R(-3f, 3f), 0f, R(-3f, 3f)),
                    Quaternion.Euler(0f, R(0f, 360f), 0f), 0.75f, s_Random);
        }

        // Muro da ruína engolida, com o topo quebrado e trepadeiras
        static void RuinWall(Vector3 foot, float yaw, float length, float height) =>
            ArquipelagoRuinKit.AshlarWall(s_Ruins, foot, Quaternion.Euler(0f, yaw, 0f), length, height, 1f, s_Random);

        // Poleiro do Espinhento: rocha alta de topo plano (5,5 m acima do chão da sala) com pedras no pé
        static void Perch(Arena a, Vector3 foot, float arenaFloor)
        {
            float top = arenaFloor + 5.5f;
            float height = top - foot.y + 0.6f;
            float width = R(2.6f, 3.2f);
            Quaternion yaw = Quaternion.Euler(0f, R(0f, 90f), 0f);
            ArquipelagoRuinKit.Pillar(s_Rocks.Pick(), new Vector3(foot.x, top - height * 0.5f, foot.z),
                new Vector2(width, width), height, yaw, width * 0.12f);
            ArquipelagoRuinKit.Pillar(s_Rocks.Pick(), new Vector3(foot.x + R(-0.6f, 0.6f), foot.y + height * 0.2f,
                foot.z + R(-0.6f, 0.6f)), new Vector2(width * 1.35f, width * 1.2f), height * 0.45f,
                yaw * Quaternion.Euler(0f, 20f, 0f), width * 0.15f);
            Vector3 toCenter = Spot(a, 0f, 0f, 0f) - foot;
            toCenter.y = 0f;
            ArquipelagoRuinKit.VinesOnBlock(new Vector3(foot.x, top - height * 0.5f, foot.z), new Vector2(width, width),
                height, yaw, toCenter, s_Random, 0.6f);
            ArquipelagoRuinKit.IrregularRock(s_Rocks, foot + toCenter.normalized * (width * 0.9f) + Vector3.up * 0.3f,
                R(1f, 1.5f), s_Random);
            a.Perches.Add(new Vector3(foot.x, top, foot.z));
            a.Reserved.Add(foot);
        }

        // Árvores soltas na sala (sprites com o tronco sólido), no chão seco longe das portas, das rampas e das peças
        static void ArenaTrees(Arena a, int count)
        {
            Mesh quad = GridSceneKit.SpriteQuad();
            var groves = new GridBiomeBuilder.Groves(s_Forest.Trees, s_Random, k_Width * k_Cell, s_Depth * k_Cell);
            var root = new GameObject($"Arvores_{a.Number}").transform;
            root.SetParent(s_World, false);
            Vector3 entrance = CellCenter(a.Entrance), exit = CellCenter(a.Exit);
            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < count; attempt++)
            {
                var cell = new Vector2Int(s_Random.Next(a.Bounds.xMin, a.Bounds.xMax),
                    s_Random.Next(a.Bounds.yMin, a.Bounds.yMax));
                if (s_Owner[cell.x, cell.y] != a.Index + 1 || s_Kind[cell.x, cell.y] != Kind.Floor ||
                    s_Ramp[cell.x, cell.y] != 0 || !FlatAround(cell, 1))
                    continue;
                Vector3 p = CellCenter(cell);
                if (Vector3.Distance(p, entrance) < 10f || Vector3.Distance(p, exit) < 10f || NearReserved(a, p, 6f) ||
                    Vector3.Distance(p, Spot(a, 0f, 0f, p.y)) < 6f)
                    continue;
                GridTextureKit.TreeSpecies species = groves.SpeciesAt(p, s_Random);
                GridTextureKit.TreeSize size = species.Sizes[s_Random.NextDouble() < 0.6 ? 0 : 1];
                float jitter = R(0.92f, 1.08f);
                GameObject tree = GridSceneKit.Billboard(species.Name, quad,
                    size.NearTints[s_Random.Next(size.NearTints.Length)], size.Size * jitter, p, root,
                    s_Random.NextDouble() < 0.5);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = Mathf.Min(size.TrunkRadius * jitter, 0.9f);
                trunk.height = 6f;
                trunk.center = new Vector3(0f, 3f, 0f);
                a.Reserved.Add(p);
                placed++;
            }
        }

        // Célula com as vizinhas na mesma altura e no chão seco
        static bool FlatAround(Vector2Int cell, int margin)
        {
            float h = s_Height[cell.x, cell.y];
            for (int dx = -margin; dx <= margin; dx++)
            for (int dz = -margin; dz <= margin; dz++)
            {
                int x = cell.x + dx, z = cell.y + dz;
                if (!InMap(x, z) || s_Kind[x, z] != Kind.Floor || s_Ramp[x, z] != 0 ||
                    Mathf.Abs(s_Height[x, z] - h) > 0.01f)
                    return false;
            }

            return true;
        }

        // ---------- floresta ----------

        // Floresta em volta do espaço andável: as 30 primeiras fileiras são sprites que olham para o jogador, na cor
        // normal; as 30 seguintes são cartas fixas que escurecem fileira a fileira até o preto (profundidade), e no fim
        // uma parede preta da altura das árvores fecha o fundo. Devolve quantas árvores há.
        static int PlaceForest()
        {
            Mesh quad = GridSceneKit.SpriteQuad();
            var random = new System.Random(1301);
            var groves = new GridBiomeBuilder.Groves(s_Forest.Trees, random, (k_Width + 2 * k_Margin) * k_Cell,
                (s_Depth + 2 * k_Margin) * k_Cell);
            var cards = new GridSceneKit.Cards();
            var spacing = new GridBiomeBuilder.TreeSpacing();
            var dark = new Dictionary<(Material, int), Material>();
            var candidates = new List<(int distance, float order, Vector2Int cell)>();
            for (int x = -k_Margin; x < k_Width + k_Margin; x++)
            for (int z = -k_Margin; z < s_Depth + k_Margin; z++)
            {
                int distance = Dist(x, z);
                if (distance >= 1 && distance <= k_DarkDepth && KindAt(x, z) == Kind.Void && !Beyond(z))
                    candidates.Add((distance, (float)random.NextDouble(), V(x, z)));
            }

            candidates.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance) : a.order.CompareTo(b.order));
            var forest = new GameObject("Floresta").transform;
            forest.SetParent(s_World, false);
            int count = 0;
            foreach ((int d, float _, Vector2Int cell) in candidates)
            {
                Vector3 position = Jittered(cell, random, 0.9f);
                GridTextureKit.TreeSpecies species = groves.SpeciesAt(position, random);
                int sizeIndex = d <= 3 ? (random.NextDouble() < 0.3 ? 1 : 0) : d <= 12 ? (random.NextDouble() < 0.3 ? 2 : 1) : 2;
                GridTextureKit.TreeSize size = species.Sizes[sizeIndex];
                float jitter = 0.9f + (float)random.NextDouble() * 0.2f;
                float radius = size.Size.x * jitter * (d <= 4 ? 0.15f : 0.21f);
                var flat = new Vector2(position.x, position.z);
                if (!spacing.IsFree(flat, radius))
                    continue;
                spacing.Add(flat, radius);
                Material material = size.NearTints[random.Next(size.NearTints.Length)];
                bool flip = random.NextDouble() < 0.5;
                if (d <= k_ForestDepth)
                    GridSceneKit.Billboard(species.Name, quad, material, size.Size * jitter, position, forest, flip);
                else
                {
                    int step = Mathf.Clamp((d - k_ForestDepth - 1) * k_DarkSteps / (k_DarkDepth - k_ForestDepth), 0,
                        k_DarkSteps - 1);
                    // Um tom por espécie no fundo (poucos materiais)
                    cards.Add(Darkened(size.NearTints[0], step, dark), position, Facing(cell, position),
                        size.Size * jitter, flip);
                }

                count++;
            }

            BuildBlackWall(forest);
            PlaceDecor(quad, random, cards);
            cards.Build(s_Prefix, forest);

            foreach (Arena a in s_Arenas)
            {
                Vector3 center = Spot(a, 0f, 0f, 4f);
                var size = new Vector3(a.RadiusX * 2f * k_Cell, 8f, a.RadiusZ * 2f * k_Cell);
                int max = Mathf.RoundToInt(size.x * size.z * 0.013f);
                GridSceneKit.AddSpores($"Esporos_Neon_{a.Number}", s_Forest.Spores, center, size, max, max / 10f,
                    new Vector2(0.08f, 0.2f), s_Theme.SporeColors, s_World);
            }

            var all = new Vector3(k_Width * k_Cell - 40f, 22f, s_Depth * k_Cell - 40f);
            int forestMax = Mathf.RoundToInt(all.x * all.z * 0.0019f);
            GridSceneKit.AddSpores("Esporos_Neon_Floresta", s_Forest.Spores,
                new Vector3(k_Width * k_Cell * 0.5f, 12f, s_Depth * k_Cell * 0.5f), all, forestMax, forestMax / 12f,
                new Vector2(0.15f, 0.35f), s_Theme.SporeColors, s_World);
            return count;
        }

        // Plantas do tema: densas no fundo (cartas fixas) e raras no chão seco das salas e trilhas (sprites)
        static void PlaceDecor(Mesh quad, System.Random random, GridSceneKit.Cards cards)
        {
            var decor = new GameObject("Decoracao").transform;
            decor.SetParent(s_World, false);
            for (int x = 0; x < k_Width; x++)
            for (int z = 0; z < s_Depth; z++)
            {
                Kind kind = s_Kind[x, z];
                bool inside = kind == Kind.Floor;
                if (kind == Kind.Path || kind == Kind.Stone || IsWater(kind) || s_Ramp[x, z] != 0 ||
                    Dist(x, z) > 12)
                    continue;
                float roll = (float)random.NextDouble() / (inside ? 0.25f : 1f);
                GridTextureKit.DecorItem item = null;
                foreach (GridTextureKit.DecorItem candidate in s_Forest.Decor)
                {
                    if (roll < candidate.Chance)
                    {
                        item = candidate;
                        break;
                    }

                    roll -= candidate.Chance;
                }

                if (item == null)
                    continue;
                Vector3 position = Jittered(V(x, z), random, 0.7f);
                Material material = item.Materials[random.Next(item.Materials.Length)];
                Vector2 size = item.Size * Mathf.Lerp(item.Scale.x, item.Scale.y, (float)random.NextDouble());
                bool flip = random.NextDouble() < 0.5;
                if (inside)
                    GridSceneKit.Billboard("Planta", quad, material, size, position, decor, flip);
                else
                    cards.Add(material, position, Facing(V(x, z), position), size, flip);
            }
        }

        // Centro da célula com um desvio sorteado, desde que o ponto caia numa célula do mesmo chão (mesma altura, sem
        // rampa nem água); senão, o centro
        static Vector3 Jittered(Vector2Int cell, System.Random random, float amount)
        {
            Vector3 center = GridSceneKit.CellCenter(cell.x, cell.y) + Vector3.up * GroundAt(cell.x, cell.y);
            Vector3 p = center + new Vector3(((float)random.NextDouble() * 2f - 1f) * amount, 0f,
                ((float)random.NextDouble() * 2f - 1f) * amount);
            int x = Mathf.FloorToInt(p.x / k_Cell), z = Mathf.FloorToInt(p.z / k_Cell);
            if (RampAt(x, z) || KindAt(x, z) != KindAt(cell.x, cell.y) ||
                Mathf.Abs(GroundAt(x, z) - GroundAt(cell.x, cell.y)) > 0.01f)
                return center;
            return p;
        }

        // Cópia escurecida de um material de árvore: passo 0 quase na cor normal, o último quase preto (cor e brilho)
        static Material Darkened(Material source, int step, Dictionary<(Material, int), Material> cache)
        {
            if (cache.TryGetValue((source, step), out Material material))
                return material;
            float f = Mathf.Lerp(0.85f, 0.04f, step / (k_DarkSteps - 1f));
            material = GridTextureKit.Tinted($"{source.name}_Fundo{step + 1}", source, new Color(f, f, f));
            if (source.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", source.GetColor("_EmissionColor") * f * f);
            EditorUtility.SetDirty(material);
            cache[(source, step)] = material;
            return material;
        }

        // Parede preta no fim da floresta de fundo, da altura das árvores: faces na beira da última fileira, viradas
        // para dentro
        static void BuildBlackWall(Transform parent)
        {
            var wall = new MeshData();
            Vector2Int[] dirs = { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) };
            for (int x = -k_Margin; x < k_Width + k_Margin; x++)
            for (int z = -k_Margin; z < s_Depth + k_Margin; z++)
            {
                if (Dist(x, z) != k_DarkDepth + 1 || Beyond(z))
                    continue;
                float h = GroundAt(x, z);
                foreach (Vector2Int d in dirs)
                {
                    if (Dist(x + d.x, z + d.y) <= k_DarkDepth)
                        wall.AddSideRange(x, z, d, h - 2f, h + k_WallHeight);
                }
            }

            // Norte aberto: a borda norte do mapa também fecha, menos na boca do desfiladeiro (60 m para cada lado da
            // trilha, onde ficam os paredões e o terreno dele)
            if (s_OpenNorth)
            {
                int exitX = Mathf.FloorToInt(OpenEndPoint.x / k_Cell);
                for (int x = -k_Margin; x < k_Width + k_Margin; x++)
                {
                    if (Mathf.Abs(x - exitX) <= 30 || Dist(x, s_Depth - 1) > k_DarkDepth)
                        continue;
                    float h = GroundAt(x, s_Depth - 1);
                    wall.AddSideRange(x, s_Depth, V(0, -1), h - 2f, h + k_WallHeight);
                }
            }

            Material black = GridTextureKit.Tinted("Bosque_Parede_Fundo", s_Forest.Grass, Color.black);
            if (black.HasProperty("_EmissionColor"))
                black.SetColor("_EmissionColor", Color.black);
            GridSceneKit.CreateObject(s_Prefix + "_Parede_Fundo", "Parede_Fundo", wall, black, parent, false, false);
        }

        static Vector3 Facing(Vector2Int cell, Vector3 position)
        {
            Vector2Int nearest = NearestOf(cell.x, cell.y);
            Vector3 toward = GridSceneKit.CellCenter(nearest.x, nearest.y) - position;
            toward.y = 0f;
            return toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.back;
        }

        // ---------- cápsula de evacuação ----------

        // Onde a cápsula caiu (metros): na clareira da queda, a oeste de onde o jogador começa
        static readonly Vector3 k_PodSpot = new Vector3(162f, 0f, 38f);

        // Cápsula de evacuação do ÍCARO-9 caída do céu: casco cilíndrico de metal enterrado de bico na terra, torto, com
        // o escudo de calor queimado, aletas e bocais na traseira para o alto, faixas de luz ciano, a escotilha
        // aberta virada para o jogador (porta arrancada caída ao lado), uma cratera de terra revirada e pedaços do casco
        // espalhados; uma luz âmbar piscando na escotilha
        static void BuildEscapePod()
        {
            Material hull = GridTextureKit.Surface("Bosque_Capsula_Casco", PaintHull(), Color.black);
            Material scorched = GridTextureKit.Surface("Bosque_Capsula_Queimado", PaintScorched(), Color.black);
            Material stripe = GridTextureKit.Surface("Bosque_Capsula_Luz", PaintGlowStrip(), GridTextureKit.k_NeonCyan);
            var metal = new MeshData();
            var burnt = new MeshData();
            var lights = new MeshData();
            var dark = new MeshData();

            // Eixo: o bico entra na terra para oeste e um pouco para o sul; a traseira fica para o alto, a leste
            Vector3 axis = Quaternion.Euler(0f, -100f, 0f) * Quaternion.Euler(-32f, 0f, 0f) * Vector3.forward;
            axis = new Vector3(axis.x, -Mathf.Abs(axis.y), axis.z).normalized;   // aponta para baixo (o bico)
            const float radius = 1.6f, length = 6f;
            Vector3 nose = k_PodSpot + Vector3.down * 1.2f;
            Vector3 tail = nose - axis * length;
            Quaternion frame = Quaternion.LookRotation(-axis, Vector3.up);
            Vector3 up = frame * Vector3.up, side = frame * Vector3.right;

            // Casco, aros e o escudo de calor (o bico, enterrado)
            BlockMesh.Prism(metal, tail, axis, length, radius, 12, 15f);
            foreach (float t in new[] { 0.25f, 0.6f })
                BlockMesh.Prism(metal, tail + axis * (length * t), axis, 0.35f, radius * 1.07f, 12, 15f);
            BlockMesh.Prism(burnt, nose - axis * 0.4f, axis, 0.9f, radius * 1.12f, 12, 15f);
            BlockMesh.Prism(burnt, nose + axis * 0.5f, axis, 0.8f, radius * 0.8f, 12, 15f);

            // Traseira: tampa, três bocais e três aletas
            BlockMesh.Prism(metal, tail - axis * 0.3f, axis, 0.35f, radius * 0.9f, 12, 15f);
            for (int i = 0; i < 3; i++)
            {
                Quaternion around = Quaternion.AngleAxis(i * 120f, axis);
                Vector3 offset = around * up * (radius * 0.45f);
                BlockMesh.Prism(burnt, tail + offset - axis * 0.25f, -axis, 0.9f, 0.38f, 8, 0f);
                Vector3 fin = around * (side * Mathf.Cos(30f * Mathf.Deg2Rad) + up * Mathf.Sin(30f * Mathf.Deg2Rad));
                BlockMesh.ChamferBox(metal, tail + axis * 0.9f + fin * (radius + 0.45f),
                    new Vector3(0.18f, 1.6f, 0.9f), Quaternion.LookRotation(axis, fin), 0.04f);
            }

            // Faixas de luz ciano em volta do casco (perto da traseira)
            BlockMesh.Prism(lights, tail + axis * 0.7f, axis, 0.15f, radius * 1.02f, 12, 15f);
            BlockMesh.Prism(lights, tail + axis * (length * 0.42f), axis, 0.12f, radius * 1.02f, 12, 15f);

            // Escotilha aberta: vão escuro no casco virado para o jogador e a porta caída ao lado
            Vector3 toPlayer = (CellToWorld(k_StartCenter) - k_PodSpot);
            toPlayer.y = 0f;
            Vector3 face = Vector3.ProjectOnPlane(toPlayer.normalized, axis).normalized;
            Vector3 hatch = tail + axis * (length * 0.42f) + face * (radius * 0.98f);
            BlockMesh.ChamferBox(dark, hatch, new Vector3(1.3f, 1.7f, 0.12f),
                Quaternion.LookRotation(face, -axis), 0.05f);
            BlockMesh.ChamferBox(metal, hatch + face * 0.5f + Vector3.down * 0.3f, new Vector3(1.5f, 1.9f, 0.16f),
                Quaternion.LookRotation(face, -axis) * Quaternion.Euler(-35f, 0f, 0f), 0.05f);
            Vector3 door = new Vector3(k_PodSpot.x, 0.1f, k_PodSpot.z) + toPlayer.normalized * 3.2f +
                           Vector3.Cross(Vector3.up, toPlayer.normalized) * 1.4f;
            BlockMesh.ChamferBox(metal, door, new Vector3(1.5f, 0.16f, 1.9f), Quaternion.Euler(4f, 35f, -6f), 0.05f);

            // Pedaços do casco espalhados e chamuscados
            for (int i = 0; i < 7; i++)
            {
                float angle = R(0f, 360f), distance = R(3.5f, 9f);
                Vector3 p = k_PodSpot + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                p.y = 0.08f;
                BlockMesh.ChamferBox(i % 3 == 0 ? burnt : metal, p, new Vector3(R(0.4f, 1.1f), 0.1f, R(0.3f, 0.8f)),
                    Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)), 0.02f);
            }

            // Cratera: terra revirada em volta do bico (pedras e montes) e uma mancha queimada
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + R(-12f, 12f);
                Vector3 p = new Vector3(k_PodSpot.x, 0f, k_PodSpot.z) - new Vector3(axis.x, 0f, axis.z).normalized * 1.5f +
                            Quaternion.Euler(0f, angle, 0f) * Vector3.forward * R(3f, 4.2f);
                ArquipelagoRuinKit.IrregularRock(s_Rocks, p + Vector3.up * 0.15f, R(0.9f, 1.6f), s_Random);
            }

            var mark = new MeshData();
            Vector3 center = new Vector3(k_PodSpot.x, 0.03f, k_PodSpot.z);
            const int ring = 16;
            for (int i = 0; i < ring; i++)
            {
                float a0 = i * Mathf.PI * 2f / ring, a1 = (i + 1) * Mathf.PI * 2f / ring;
                float r0 = 4.5f + Mathf.Sin(i * 1.7f) * 0.8f, r1 = 4.5f + Mathf.Sin((i + 1) * 1.7f) * 0.8f;
                mark.AddTriangleAuto(center, center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r0,
                    center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r1, Vector3.up);
            }

            var root = new GameObject("Capsula_Evacuacao").transform;
            root.SetParent(s_World, false);
            GridSceneKit.CreateObject(s_Prefix + "_Capsula_Casco", "Casco", metal, hull, root, true);
            GridSceneKit.CreateObject(s_Prefix + "_Capsula_Queimado", "Queimado", burnt, scorched, root, true);
            GridSceneKit.CreateObject(s_Prefix + "_Capsula_Luz", "Luz", lights, stripe, root, false);
            GridSceneKit.CreateObject(s_Prefix + "_Capsula_Vao", "Vao", dark, s_Stone.Shadow, root, false);
            GridSceneKit.CreateObject(s_Prefix + "_Capsula_Cratera", "Cratera", mark, scorched, root, false, false);

            var lamp = new GameObject("Luz_Escotilha").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = hatch + face * 1.2f;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.62f, 0.25f);
            lamp.intensity = 2.5f;
            lamp.range = 9f;
            lamp.shadows = LightShadows.None;
            lamp.gameObject.AddComponent<LightFlicker>();
        }

        // Casco: placas de metal azul-acinzentado com juntas escuras, rebites e uma faixa laranja de aviso
        static GridTextureKit.Paint PaintHull()
        {
            var plate = new Color(0.42f, 0.46f, 0.52f);
            GridTextureKit.Paint p = GridTextureKit.Tile(32, 32, 881, plate);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color c = x % 16 == 0 || y % 16 == 0 ? plate * 0.62f : y >= 26 && y < 29 ? new Color(0.85f, 0.45f, 0.12f)
                    : plate;
                if ((x % 16 == 2 || x % 16 == 13) && (y % 16 == 2 || y % 16 == 13))
                    c = plate * 1.25f;
                if (p.Next < 0.05f)
                    c *= 0.85f;
                p.Set(x, y, p.Vary(c, 0.04f));
            }

            return p;
        }

        // Metal queimado do escudo de calor: preto-acastanhado com riscos alaranjados apagados
        static GridTextureKit.Paint PaintScorched()
        {
            var soot = new Color(0.1f, 0.08f, 0.08f);
            GridTextureKit.Paint p = GridTextureKit.Tile(32, 32, 882, soot);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float roll = p.Next;
                Color c = roll < 0.06f ? new Color(0.35f, 0.16f, 0.06f) : roll < 0.3f ? soot * 1.5f : soot;
                p.Set(x, y, p.Vary(c, 0.08f));
            }

            return p;
        }

        // Faixa de luz: ciano que brilha
        static GridTextureKit.Paint PaintGlowStrip()
        {
            GridTextureKit.Paint p = GridTextureKit.Tile(8, 8, 883, new Color(0.3f, 0.9f, 1f));
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                p.Set(x, y, p.Vary(new Color(0.3f, 0.9f, 1f), 0.05f), true);
            return p;
        }

        // ---------- salas: combate ----------

        static RoomManager BuildRoom(Arena a, Transform parent, out NavMeshSurface surface)
        {
            var root = new GameObject($"Arena_{a.Number:00}_{a.Name.Replace(' ', '_')}");
            root.transform.SetParent(parent, false);
            Vector3 center = Spot(a, 0f, 0f, 0f);
            root.transform.position = center;

            // Tranca quando o jogador já está ~10 m para dentro da porta mais próxima do centro
            Vector3 entrance = CellCenter(a.Entrance), exit = CellCenter(a.Exit);
            float reach = Mathf.Min(Vector3.Distance(center, entrance), Vector3.Distance(center, exit));
            var trigger = root.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 1;
            trigger.radius = Mathf.Clamp(reach - 10f, 8f, 60f);
            trigger.height = 40f;
            trigger.center = new Vector3(0f, 10f, 0f);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = a.Name;
            RoomDoor entry = GridSceneKit.CreateDoor("Porta_Entrada", a.Entrance, s_Forest.Door, root.transform);
            entry.transform.position = entrance;
            RoomDoor leave = GridSceneKit.CreateDoor("Porta_Saida", a.Exit, s_Forest.Door, root.transform);
            leave.transform.position = exit;
            room.Doors = new[] { entry, leave };
            room.Balance = CombatAssetsBuilder.Balance;
            room.BiomeIndex = 0;
            room.ArenaNumber = a.Number;
            room.ArenaInBiome = a.Number - 1;
            room.ArenasInBiome = 8;
            room.IsBoss = a.Boss;
            if (a.Boss)
            {
                // O chefe entra do lado oposto à entrada, virado para ela
                var bossPoint = new GameObject("ChefeSpawn").transform;
                bossPoint.SetParent(root.transform, false);
                bossPoint.position = center + Vector3.forward * (a.RadiusZ * k_Cell * 0.35f);
                bossPoint.rotation = Quaternion.LookRotation(Vector3.back);
                room.BossPoint = bossPoint;
                room.BossEnemy = room.Balance ? room.Balance.BossFor(0) : null;
            }

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            Vector3 boundsCenter = CellToWorld(a.Bounds.center);
            surface.center = boundsCenter - center + Vector3.up * 8f;
            surface.size = new Vector3(a.Bounds.width * k_Cell + 4f, 40f, a.Bounds.height * k_Cell + 4f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return room;
        }

        static HighSpawnPoint[] PlaceHighPoints(Arena a, Transform room, ref int total, ref int valid)
        {
            var group = new GameObject("PontosAltos").transform;
            group.SetParent(room, false);
            Vector3 center = Spot(a, 0f, 0f, 0f);
            Vector3 entrance = CellCenter(a.Entrance) + Vector3.forward * 6f;
            var points = new List<HighSpawnPoint>();
            foreach (Vector3 perch in a.Perches)
            {
                var point = new GameObject($"PontoAlto_{points.Count + 1:00}").AddComponent<HighSpawnPoint>();
                point.transform.SetParent(group, false);
                point.transform.position = perch;
                point.ArenaFloorHeight = 0f;
                point.ArenaCenter = center;
                point.ArenaEntrance = entrance;
                total++;
                if (point.Validate())
                    valid++;
                else
                    Debug.LogWarning($"[Ícaro] {a.Name}: {point.name} sem linha de visão.", point);
                points.Add(point);
            }

            return points.ToArray();
        }

        // De 8 a 12 pontos no chão da sala (seco ou raso), longe da entrada, escondidos de quem entra, com ~2 m livres
        static int PlaceSpawns(Arena a, RoomManager room)
        {
            var random = new System.Random(77 + a.Number);
            Vector3 entry = CellCenter(a.Entrance);
            Vector3 eye = entry + Vector3.forward * 4f + Vector3.up * 1.7f;
            float minDistance = a.RadiusZ * k_Cell * 0.7f;
            int target = 8 + random.Next(5);
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            for (int attempt = 0; attempt < 3000 && points.Count < target; attempt++)
            {
                var cell = new Vector2Int(random.Next(a.Bounds.xMin, a.Bounds.xMax), random.Next(a.Bounds.yMin, a.Bounds.yMax));
                if (s_Owner[cell.x, cell.y] != a.Index + 1 || s_Ramp[cell.x, cell.y] != 0 ||
                    s_Kind[cell.x, cell.y] != Kind.Floor && s_Kind[cell.x, cell.y] != Kind.Shallow &&
                    s_Kind[cell.x, cell.y] != Kind.Stone)
                    continue;
                Vector3 candidate = CellCenter(cell);
                if (Vector3.Distance(candidate, entry) < minDistance)
                    continue;
                if (!NavMesh.SamplePosition(candidate + Vector3.up, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - candidate.y) > 0.6f)
                    continue;
                if (points.Exists(p => Vector3.Distance(p, hit.position) < 8f))
                    continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 2.2f, hit.position + Vector3.up * 4f, 1.8f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (!Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore) &&
                    attempt < 2000)
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
                Vector3 toEntry = entry - position;
                toEntry.y = 0f;
                point.rotation = Quaternion.LookRotation(toEntry.sqrMagnitude > 0.01f ? toEntry : Vector3.forward);
                transforms.Add(point);
            }

            room.SpawnPoints = transforms.ToArray();
            if (points.Count < 6)
                Debug.LogWarning($"[Ícaro] {a.Name}: só {points.Count} pontos de spawn.");
            return points.Count;
        }

        // ---------- jogador e depuração ----------

        static void PlacePlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcaroPrefabBuilder.PlayerVariantPath);
            if (!prefab)
            {
                Debug.LogError("[Ícaro] Player_Icaro não encontrado: rode Ícaro > Gerar prefabs.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(CellToWorld(k_StartCenter + new Vector2(0.5f, -2f), 0.2f),
                Quaternion.identity);
        }

        // F6/F7 levam à trilha logo antes de cada sala; F9 abre o menu
        static void AddDebugTeleport(Transform systems, BiomeZone zone)
        {
            var points = new List<Transform>();
            var zones = new List<BiomeZone>();
            var root = new GameObject("Chegadas").transform;
            root.SetParent(systems, false);
            foreach (Arena a in s_Arenas)
            {
                var arrival = new GameObject($"Chegada_{a.Name}").transform;
                arrival.SetParent(root, false);
                arrival.position = GridSceneKit.CellCenter(a.Entrance.x, a.Entrance.y - 3) + Vector3.up * 0.2f;
                points.Add(arrival);
                zones.Add(zone);
            }

            var teleport = new GameObject("Debug_Teleporte").AddComponent<ViaDebugTeleport>();
            teleport.transform.SetParent(systems, false);
            teleport.Points = points.ToArray();
            teleport.Zones = zones.ToArray();
            teleport.BiomeStarts = new[] { 0 };
        }
    }
}
