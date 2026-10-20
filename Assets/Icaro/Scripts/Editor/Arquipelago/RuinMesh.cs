using System.Collections.Generic;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Malhas das ruínas separadas por material. Cada bloco sorteia um dos tons de pedra (Pick), para as peças não
    // ficarem de uma cor só; recessos (vãos de janela) vão para Dark. No fim, Split separa o musgo (faces viradas para
    // cima) e a pedra molhada com cracas (o que fica abaixo da linha d'água; as faces que cruzam a linha são cortadas
    // nela). Com 'single', tudo vai para uma malha só (silhuetas do fundo, que têm um material único).
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
        }

        struct Vert
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;
        }

        // Musgo nas faces com normal.y acima de 'mossSlope' (fora da água) e pedra molhada abaixo de 'waterLine'
        public Layers Split(float waterLine, float mossSlope = 0.7f)
        {
            var layers = new Layers
            {
                Tones = new MeshData[Tones.Length], Moss = new MeshData(), Wet = new MeshData(), Dark = m_Dark,
            };
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
                    AddPolygon(top ? layers.Moss : target, above);
                    AddPolygon(layers.Wet, below);
                }
            }

            return layers;
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
