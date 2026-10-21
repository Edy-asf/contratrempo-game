using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Malhas das ruínas separadas por material. Cada bloco sorteia um dos tons de pedra (Pick), para as peças não
    // ficarem de uma cor só; recessos (vãos de janela) vão para Dark. No fim, Split separa o musgo (faces viradas para
    // cima) e a pedra molhada com cracas (o que fica abaixo da linha d'água; as faces que cruzam a linha são cortadas
    // nela). Com 'single', tudo vai para uma malha só (silhuetas do fundo, que têm um material único).
    // Com uma máscara de musgo (SplitMasked), os topos grandes (pisos, tabuleiros, passarelas) são cortados numa grade
    // no mundo e cada pedaço vira laje limpa, um dos passos de transição laje-musgo ou musgo, pelo valor da máscara no
    // meio dele (0 = laje, 1 = musgo); as peças pequenas continuam com o musgo de sempre no topo.
    public sealed class RuinMesh
    {
        public readonly MeshData[] Tones;
        readonly MeshData m_Dark;
        readonly System.Random m_Random;
        readonly bool m_Single;

        public RuinMesh(int seed, int tones = 3, bool single = false)
        {
            m_Single = single;
            m_Random = new System.Random(seed);
            Tones = new MeshData[single ? 1 : tones];
            for (int i = 0; i < Tones.Length; i++)
                Tones[i] = new MeshData();
            m_Dark = single ? Tones[0] : new MeshData();
        }

        public MeshData Pick() => Tones[m_Random.Next(Tones.Length)];

        public MeshData Dark => m_Dark;

        public struct Layers
        {
            public MeshData[] Tones;
            public MeshData Moss;
            public MeshData Wet;
            public MeshData Dark;
            public MeshData[] Masked;   // topos grandes: lajes (variações), passos de transição e musgo (SplitMasked)
        }

        struct Vert
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;
        }

        // Musgo nas faces com normal.y acima de 'mossSlope' (fora da água) e pedra molhada abaixo de 'waterLine'
        public Layers Split(float waterLine, float mossSlope = 0.7f) => SplitMasked(waterLine, mossSlope, null);

        // Como Split, mas os topos com área acima de 'minArea' são cortados em quadrados de 'cell' metros alinhados no
        // mundo; cada pedaço vai para Masked[camada] pelo nível da máscara no meio dele, em 'steps' passos: 0 = laje
        // (sorteia uma das 'pavings' variações pelo quadrado), 1 a steps-1 = transições, steps = musgo. UVs no mundo:
        // xz / 'pavingTile' nas lajes e transições, xz / 'mossTile' no musgo.
        public Layers SplitMasked(float waterLine, float mossSlope, System.Func<Vector3, float> mask, int pavings = 3,
            int steps = 6, float cell = 4f, float minArea = 6f, float pavingTile = 8f, float mossTile = 2f)
        {
            var layers = new Layers
            {
                Tones = new MeshData[Tones.Length], Moss = new MeshData(), Wet = new MeshData(), Dark = m_Dark,
                Masked = new MeshData[mask != null ? pavings + steps : 0],
            };
            for (int i = 0; i < layers.Masked.Length; i++)
                layers.Masked[i] = new MeshData();
            var above = new List<Vert>(6);
            var below = new List<Vert>(6);
            var triangle = new Vert[3];
            for (int t = 0; t < Tones.Length; t++)
            {
                MeshData source = Tones[t];
                var target = layers.Tones[t] = new MeshData();
                for (int i = 0; i < source.Triangles.Count; i += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int index = source.Triangles[i + k];
                        triangle[k] = new Vert
                        {
                            Position = source.Vertices[index], Normal = source.Normals[index], Uv = source.Uvs[index],
                        };
                    }

                    bool top = triangle[0].Normal.y > mossSlope;
                    Clip(triangle, waterLine, above, below);
                    if (top && mask != null && Area(triangle) > minArea)
                        AddMasked(layers.Masked, above, mask, pavings, steps, cell, pavingTile, mossTile);
                    else
                        AddPolygon(top ? layers.Moss : target, above);
                    AddPolygon(layers.Wet, below);
                }
            }

            return layers;
        }

        static float Area(Vert[] t) =>
            Vector3.Cross(t[1].Position - t[0].Position, t[2].Position - t[0].Position).magnitude * 0.5f;

        // Corta o polígono (topo) em quadrados de 'cell' alinhados no mundo e manda cada pedaço para a camada do nível
        // da máscara no meio dele
        static void AddMasked(MeshData[] layers, List<Vert> polygon, System.Func<Vector3, float> mask, int pavings,
            int steps, float cell, float pavingTile, float mossTile)
        {
            if (polygon.Count < 3)
                return;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vert v in polygon)
            {
                minX = Mathf.Min(minX, v.Position.x);
                maxX = Mathf.Max(maxX, v.Position.x);
                minZ = Mathf.Min(minZ, v.Position.z);
                maxZ = Mathf.Max(maxZ, v.Position.z);
            }

            var strip = new List<Vert>();
            var piece = new List<Vert>();
            var scratch = new List<Vert>();
            for (int cx = Mathf.FloorToInt(minX / cell); cx * cell < maxX; cx++)
            {
                ClipAxis(polygon, 0, cx * cell, true, scratch);
                ClipAxis(scratch, 0, (cx + 1) * cell, false, strip);
                if (strip.Count < 3)
                    continue;
                for (int cz = Mathf.FloorToInt(minZ / cell); cz * cell < maxZ; cz++)
                {
                    ClipAxis(strip, 2, cz * cell, true, scratch);
                    ClipAxis(scratch, 2, (cz + 1) * cell, false, piece);
                    if (piece.Count < 3)
                        continue;
                    Vector3 middle = Vector3.zero;
                    foreach (Vert v in piece)
                        middle += v.Position;
                    middle /= piece.Count;
                    int level = Mathf.RoundToInt(Mathf.Clamp01(mask(middle)) * steps);
                    int layer;
                    if (level == 0)
                    {
                        int hash = (cx * 73856093) ^ (cz * 19349663);
                        layer = (hash % pavings + pavings) % pavings;
                    }
                    else
                        layer = pavings + level - 1;
                    float tile = level == steps ? mossTile : pavingTile;
                    for (int i = 0; i < piece.Count; i++)
                    {
                        Vert v = piece[i];
                        v.Uv = new Vector2(v.Position.x, v.Position.z) / tile;
                        piece[i] = v;
                    }

                    AddPolygon(layers[layer], piece);
                }
            }
        }

        // Corta o polígono no plano (eixo 0 = x, 2 = z) = h, ficando com o lado de cima ('keepAbove') ou o de baixo
        static void ClipAxis(List<Vert> polygon, int axis, float h, bool keepAbove, List<Vert> result)
        {
            result.Clear();
            float sign = keepAbove ? 1f : -1f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vert a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                float da = (a.Position[axis] - h) * sign, db = (b.Position[axis] - h) * sign;
                if (da >= 0f)
                    result.Add(a);
                if (da >= 0f == db >= 0f)
                    continue;
                float t = da / (da - db);
                result.Add(new Vert
                {
                    Position = Vector3.Lerp(a.Position, b.Position, t), Normal = a.Normal,
                    Uv = Vector2.Lerp(a.Uv, b.Uv, t),
                });
            }
        }

        // Corta o triângulo no plano y = h (Sutherland-Hodgman): a parte de cima e a de baixo, na mesma ordem
        static void Clip(Vert[] triangle, float h, List<Vert> above, List<Vert> below)
        {
            above.Clear();
            below.Clear();
            for (int i = 0; i < 3; i++)
            {
                Vert a = triangle[i], b = triangle[(i + 1) % 3];
                bool aUp = a.Position.y >= h, bUp = b.Position.y >= h;
                (aUp ? above : below).Add(a);
                if (aUp == bUp)
                    continue;
                float t = (h - a.Position.y) / (b.Position.y - a.Position.y);
                var cut = new Vert
                {
                    Position = Vector3.Lerp(a.Position, b.Position, t), Normal = a.Normal,
                    Uv = Vector2.Lerp(a.Uv, b.Uv, t),
                };
                above.Add(cut);
                below.Add(cut);
            }
        }

        static void AddPolygon(MeshData mesh, List<Vert> polygon)
        {
            if (polygon.Count < 3)
                return;
            int start = mesh.Vertices.Count;
            foreach (Vert v in polygon)
            {
                mesh.Vertices.Add(v.Position);
                mesh.Normals.Add(v.Normal);
                mesh.Uvs.Add(v.Uv);
            }

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                mesh.Triangles.Add(start);
                mesh.Triangles.Add(start + i);
                mesh.Triangles.Add(start + i + 1);
            }
        }
    }
}
