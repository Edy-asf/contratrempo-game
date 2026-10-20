using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Tema de um bioma em grade, lido pelo GridBiomeBuilder: materiais (GridTextureKit), traçado das arenas, luz e
    // névoa da noite, cores do céu e dos esporos, água. Os temas prontos ficam em GridThemes.
    public class GridTheme
    {
        public string Biome = "Bosque";      // nome do BiomeZone (paleta, música)
        public int BiomeIndex;               // 0 Bosque, 1 Charco, 2 Ruínas, 3 Cratera (chefe e tabelas)
        public string SceneName = "Bosque_Grade";
        public GridTextureKit.Set Materials;
        public GridArena[] Arenas;

        // A última arena (a do chefe) também tem porta de saída, com uma trilha até a borda norte do mapa
        // (GridBiomeBuilder.OpenEndPoint), por onde a Via continua para o bioma seguinte
        public bool OpenEnd;

        // Noite
        public Color FogColor = new Color(0.15f, 0.11f, 0.26f);
        public float FogDensity = 0.014f;
        public Color AmbientSky = new Color(1f, 0.92f, 1.3f);
        public Color AmbientEquator = new Color(0.8f, 0.78f, 1.08f);
        public Color AmbientGround = new Color(0.6f, 0.47f, 0.42f);
        public Color MoonColor = new Color(0.62f, 0.68f, 1f);
        public float MoonIntensity = 1.4f;

        // Céu (material próprio por tema: Ceu_<Biome>)
        public Color SkyBlue = new Color(0.12f, 0.28f, 0.62f);
        public Color SkyPurple = new Color(0.24f, 0.07f, 0.38f);
        public float SkyBlueHeight = 0.22f;
        public float SkyTop = 0.55f;
        public Vector4 PlanetDirection = new Vector4(-0.55f, 0.3f, 0.78f, 0f);
        public float PlanetSize = 0.17f;
        public Color PlanetA = new Color(0.55f, 0.32f, 0.72f);
        public Color PlanetB = new Color(0.2f, 0.55f, 0.62f);
        public Color Ring = new Color(0.75f, 0.68f, 0.9f);
        public float StarDensity = 0.22f;

        // Aurora boreal (0 = sem aurora)
        public float AuroraStrength;
        public Color AuroraLow = new Color(0.2f, 1.4f, 0.8f);
        public Color AuroraHigh = new Color(0.5f, 0.3f, 1.2f);
        public float AuroraHeight = 0.42f;

        // Esporos e vaga-lumes (cores sorteadas)
        public Color[] SporeColors =
        {
            new Color(0.2f, 1f, 0.9f), new Color(1f, 0.3f, 0.85f), new Color(0.6f, 1f, 0.3f), new Color(1f, 0.7f, 0.2f),
        };

        // Água rasa (WaterMap): profundidade do fundo e velocidade dentro dela
        public float ShallowDepth = 0.25f;
        public float DeepDepth = 1.2f;
        public float WaterSlow = 0.6f;

        public string ScenePath => $"{IcaroAssetUtility.ScenesFolder}/{SceneName}.unity";
        public string NavMeshFolder => $"{IcaroAssetUtility.ScenesFolder}/{SceneName}_NavMesh";
        public string MeshPrefix => SceneName;
    }

    // Lóbulo de uma arena: elipse (em células) a partir do centro da arena
    public class GridLobe
    {
        public Vector2 Offset;
        public float RadiusX, RadiusZ;

        public static GridLobe L(float x, float z, float rx, float rz) =>
            new GridLobe { Offset = new Vector2(x, z), RadiusX = rx, RadiusZ = rz };
    }

    // Plano de uma arena do bioma (o GridBiomeBuilder calcula posição, portas, poleiros etc.)
    public class GridArena
    {
        public string Name;
        public int Number;              // sala no bioma (1, 2, ...); a última com Boss é a do chefe
        public bool Boss;
        public GridLobe[] Lobes;
        public float Noise;
        public float Phase;
        public int Rocks, Trees, Perches;
        public int OffsetX;             // desvio lateral (corredores com curva)
        public int Corridor;            // comprimento do corredor que chega nela

        // Água: rio raso atravessando a arena (com poços fundos fora da trilha) e lagoas fundas com margem rasa
        public bool River;
        public int Ponds;

        // Calculados pelo gerador
        public int Index;
        public Vector2 Center;
        public RectInt Bounds;
        public Vector2Int Entrance, Exit;
        public bool HasExit;
        public readonly List<Vector2Int> Perch = new List<Vector2Int>();
        public float ExtentDown, ExtentUp, ExtentSide;
    }
}
