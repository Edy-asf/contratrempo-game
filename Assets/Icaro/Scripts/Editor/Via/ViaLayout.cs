using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Desenho da Via Aeônida em dados, sem nada de cena: a sequência de clareiras e arenas ao longo de +Z,
    // as trilhas entre elas e o plano de cada arena (montes, peças de cobertura, portões).
    // Tudo sai de uma semente fixa, então cada geração produz o mesmo mapa.
    public class ViaLayout
    {
        public enum BiomeId
        {
            Bosque,
            Charco,
            Ruinas,
            Cratera,
        }

        public enum NodeKind
        {
            Start,
            Arena,
            Metronome,
            Transition,
        }

        // Área caminhável circular: clareira ou arena
        public class Node
        {
            public NodeKind Kind;
            public BiomeId Biome;
            public Vector2 Center;
            public float Radius;
            public ArenaPlan Arena; // só para NodeKind.Arena

            // Direções (unitárias, no plano XZ) para o nó anterior e o próximo; zero se não houver
            public Vector2 ToPrevious;
            public Vector2 ToNext;

            public Vector2 EdgePoint(Vector2 direction) => Center + direction * Radius;
        }

        public class Trail
        {
            public Vector2 A;
            public Vector2 B;
            public BiomeId Biome;
            public float Length => Vector2.Distance(A, B);
            public Vector2 Direction => (B - A).normalized;
        }

        public struct Mound
        {
            public Vector2 Position;
            public float Radius;
            public float Height;
        }

        public struct PropPlacement
        {
            public ScenePropKit.PropKind Kind;
            public Vector2 Position;
            public float Yaw;
            public float Scale;
        }

        public class ArenaPlan
        {
            public int GlobalIndex;   // 0..19 em toda a Via
            public int IndexInBiome;  // 0..n-1 dentro do bioma
            public int ArenasInBiome; // n (a última é a do chefe)
            public BiomeId Biome;
            public bool IsBoss;
            public bool IsFinal;      // última arena da Cratera, com a nave Dédalo
            public Node Node;
            public int Seed;
            public readonly List<Mound> Mounds = new List<Mound>();
            public readonly List<PropPlacement> Props = new List<PropPlacement>();
        }

        public class BiomePlan
        {
            public BiomeId Id;
            public BiomePalettes.Palette Palette;
            public float ZStart;
            public float ZEnd;
            public Node Entry;           // clareira onde o bioma começa (início ou transição)
            public Node Metronome;
            public readonly List<ArenaPlan> Arenas = new List<ArenaPlan>();
        }

        // ---------- parâmetros do percurso ----------

        public const float TrailHalfWidth = 5f;
        public const float ArenaRadius = 51f;      // ~102 m de diâmetro: 3× o lado das salas antigas
        public const float BossArenaRadius = 62f;
        public const float StartClearingRadius = 16f;
        public const float MetronomeClearingRadius = 14f;
        public const float TransitionClearingRadius = 12f;
        public const float MaxLateralOffset = 22f;

        const int k_Seed = 1337;

        static readonly (BiomeId biome, int arenas, int metronomeAfter)[] k_Biomes =
        {
            (BiomeId.Bosque, 6, 3),
            (BiomeId.Charco, 5, 2),
            (BiomeId.Ruinas, 5, 2),
            (BiomeId.Cratera, 4, 2),
        };

        public readonly List<Node> Nodes = new List<Node>();
        public readonly List<Trail> Trails = new List<Trail>();
        public readonly List<BiomePlan> Biomes = new List<BiomePlan>();
        public readonly List<ArenaPlan> Arenas = new List<ArenaPlan>();

        public Node StartNode => Nodes[0];
        public ArenaPlan FinalArena => Arenas[Arenas.Count - 1];
        public float MinZ { get; private set; }
        public float MaxZ { get; private set; }

        public static BiomePalettes.Palette PaletteOf(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Charco: return BiomePalettes.Charco;
                case BiomeId.Ruinas: return BiomePalettes.Ruinas;
                case BiomeId.Cratera: return BiomePalettes.Cratera;
                default: return BiomePalettes.Bosque;
            }
        }

        public static ViaLayout Build()
        {
            var layout = new ViaLayout();
            layout.BuildRoute();
            foreach (ArenaPlan arena in layout.Arenas)
                PlanArena(arena);
            return layout;
        }

        // ---------- percurso ----------

        void BuildRoute()
        {
            var random = new System.Random(k_Seed);
            float side = 1f;
            int globalArena = 0;

            AddNode(NodeKind.Start, BiomeId.Bosque, new Vector2(0f, 0f), StartClearingRadius, null);

            for (int b = 0; b < k_Biomes.Length; b++)
            {
                (BiomeId id, int arenaCount, int metronomeAfter) = k_Biomes[b];
                var biome = new BiomePlan { Id = id, Palette = PaletteOf(id) };
                Biomes.Add(biome);

                if (b == 0)
                {
                    biome.Entry = Nodes[0];
                }
                else
                {
                    // Clareira de transição: o gatilho do bioma fica nela
                    biome.Entry = Append(NodeKind.Transition, id, TransitionClearingRadius, 40f, 0f, null);
                }

                for (int i = 0; i < arenaCount; i++)
                {
                    bool boss = i == arenaCount - 1;
                    var arena = new ArenaPlan
                    {
                        GlobalIndex = globalArena,
                        IndexInBiome = i,
                        ArenasInBiome = arenaCount,
                        Biome = id,
                        IsBoss = boss,
                        IsFinal = boss && b == k_Biomes.Length - 1,
                        Seed = k_Seed * 31 + globalArena * 7919,
                    };

                    side = -side;
                    float lateral = side * (8f + (float)random.NextDouble() * (MaxLateralOffset - 8f));
                    float gap = 45f + (float)random.NextDouble() * 15f;
                    arena.Node = Append(NodeKind.Arena, id, boss ? BossArenaRadius : ArenaRadius, gap, lateral, arena);

                    biome.Arenas.Add(arena);
                    Arenas.Add(arena);
                    globalArena++;

                    // Metrônomo no meio do bioma, numa clareira na trilha
                    if (i + 1 == metronomeAfter)
                        biome.Metronome = Append(NodeKind.Metronome, id, MetronomeClearingRadius, 32f, 0f, null);
                }
            }

            LinkNodes();
            ComputeBiomeRanges();
        }

        // Novo nó depois do último, a 'gap' metros de borda a borda, com deslocamento lateral 'lateral'
        Node Append(NodeKind kind, BiomeId biome, float radius, float gap, float lateral, ArenaPlan arena)
        {
            Node previous = Nodes[Nodes.Count - 1];
            float reach = previous.Radius + gap + radius;
            float dx = Mathf.Clamp(lateral - previous.Center.x, -reach * 0.6f, reach * 0.6f);
            float dz = Mathf.Sqrt(reach * reach - dx * dx);
            return AddNode(kind, biome, new Vector2(previous.Center.x + dx, previous.Center.y + dz), radius, arena);
        }

        Node AddNode(NodeKind kind, BiomeId biome, Vector2 center, float radius, ArenaPlan arena)
        {
            var node = new Node { Kind = kind, Biome = biome, Center = center, Radius = radius, Arena = arena };
            Nodes.Add(node);
            return node;
        }

        void LinkNodes()
        {
            for (int i = 0; i < Nodes.Count - 1; i++)
            {
                Node a = Nodes[i];
                Node b = Nodes[i + 1];
                Vector2 direction = (b.Center - a.Center).normalized;
                a.ToNext = direction;
                b.ToPrevious = -direction;
                Trails.Add(new Trail { A = a.EdgePoint(direction), B = b.EdgePoint(-direction), Biome = b.Biome });
            }
        }

        // Cada bioma ocupa uma faixa de Z; a fronteira fica no centro da clareira de transição
        void ComputeBiomeRanges()
        {
            MinZ = StartNode.Center.y - StartNode.Radius - 60f;
            Node last = Nodes[Nodes.Count - 1];
            MaxZ = last.Center.y + last.Radius + 60f;

            for (int b = 0; b < Biomes.Count; b++)
            {
                Biomes[b].ZStart = b == 0 ? MinZ : Biomes[b].Entry.Center.y;
                Biomes[b].ZEnd = b == Biomes.Count - 1 ? MaxZ : Biomes[b + 1].Entry.Center.y;
            }
        }

        // ---------- consultas geométricas ----------

        // Distância (com sinal) até a área caminhável: negativa dentro de trilhas, clareiras e arenas
        public float WalkableDistance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Node node in Nodes)
                best = Mathf.Min(best, Vector2.Distance(p, node.Center) - node.Radius);
            foreach (Trail trail in Trails)
                best = Mathf.Min(best, DistanceToSegment(p, trail.A, trail.B) - TrailHalfWidth);
            return best;
        }

        // Distância até a linha central da trilha (para pintar a pedra)
        public float TrailCenterDistance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Trail trail in Trails)
                best = Mathf.Min(best, DistanceToSegment(p, trail.A, trail.B));
            return best;
        }

        // Distância com sinal até a borda da arena mais próxima (negativa dentro)
        public float ArenaDistance(Vector2 p, out ArenaPlan nearest)
        {
            nearest = null;
            float best = float.MaxValue;
            foreach (ArenaPlan arena in Arenas)
            {
                float d = Vector2.Distance(p, arena.Node.Center) - arena.Node.Radius;
                if (d < best)
                {
                    best = d;
                    nearest = arena;
                }
            }

            return best;
        }

        // Elevação dos montes das arenas no ponto (0 fora deles); perfil cosseno, inclinação ≤ ~25°
        public float MoundHeight(Vector2 p)
        {
            float height = 0f;
            foreach (ArenaPlan arena in Arenas)
            {
                if ((p - arena.Node.Center).sqrMagnitude > arena.Node.Radius * arena.Node.Radius)
                    continue;

                foreach (Mound mound in arena.Mounds)
                {
                    float d = Vector2.Distance(p, mound.Position);
                    if (d < mound.Radius)
                        height = Mathf.Max(height, mound.Height * 0.5f * (1f + Mathf.Cos(Mathf.PI * d / mound.Radius)));
                }
            }

            return height;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- plano das arenas ----------

        // Pesos de cada peça de cobertura por bioma
        static (ScenePropKit.PropKind kind, int weight)[] CoverWeights(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Charco:
                    return new[]
                    {
                        (ScenePropKit.PropKind.TroncoCaido, 5), (ScenePropKit.PropKind.RochaMedia, 3),
                        (ScenePropKit.PropKind.Arvore, 3), (ScenePropKit.PropKind.RochaGrande, 2),
                        (ScenePropKit.PropKind.CogumeloBio, 3),
                    };
                case BiomeId.Ruinas:
                    return new[]
                    {
                        (ScenePropKit.PropKind.RuinaMuro, 5), (ScenePropKit.PropKind.RuinaColuna, 4),
                        (ScenePropKit.PropKind.RuinaArco, 2), (ScenePropKit.PropKind.RuinaBloco, 2),
                        (ScenePropKit.PropKind.RochaMedia, 3), (ScenePropKit.PropKind.RochaGrande, 2),
                        (ScenePropKit.PropKind.CogumeloBio, 1),
                    };
                case BiomeId.Cratera:
                    return new[]
                    {
                        (ScenePropKit.PropKind.RochaGrande, 5), (ScenePropKit.PropKind.RochaMedia, 4),
                        (ScenePropKit.PropKind.RuinaColuna, 2), (ScenePropKit.PropKind.RuinaArco, 1),
                        (ScenePropKit.PropKind.TroncoCaido, 1),
                        (ScenePropKit.PropKind.FendaLava, 3), (ScenePropKit.PropKind.CogumeloAmbar, 1),
                    };
                default:
                    return new[]
                    {
                        (ScenePropKit.PropKind.RochaGrande, 3), (ScenePropKit.PropKind.RochaMedia, 4),
                        (ScenePropKit.PropKind.TroncoCaido, 4), (ScenePropKit.PropKind.Arvore, 3),
                        (ScenePropKit.PropKind.CogumeloBio, 2), (ScenePropKit.PropKind.CogumeloAmbar, 2),
                        (ScenePropKit.PropKind.RuinaColuna, 1), (ScenePropKit.PropKind.RuinaBloco, 1),
                    };
            }
        }

        static void PlanArena(ArenaPlan arena)
        {
            var random = new System.Random(arena.Seed);
            Node node = arena.Node;
            float r = node.Radius;
            Vector2 entry = node.EdgePoint(node.ToPrevious);
            Vector2? exit = arena.IsFinal ? (Vector2?)null : node.EdgePoint(node.ToNext);

            bool FarFromMouths(Vector2 p, float clearance) =>
                Vector2.Distance(p, entry) > clearance && (!exit.HasValue || Vector2.Distance(p, exit.Value) > clearance);

            // A arena final reserva o fundo para a nave
            Vector2 shipSpot = ShipSpot(arena);

            // Montes: pontos elevados suaves (raio ≥ 3,6× a altura mantém a encosta abaixo de ~25°)
            int moundCount = 2 + random.Next(3);
            for (int attempt = 0; attempt < 200 && arena.Mounds.Count < moundCount; attempt++)
            {
                Vector2 p = node.Center + RandomInDisc(random) * r * 0.62f;
                float height = 2.5f + (float)random.NextDouble() * 1.5f;
                float radius = height * 3.6f + 2f;
                if (!FarFromMouths(p, radius + 12f))
                    continue;
                if (arena.IsFinal && Vector2.Distance(p, shipSpot) < radius + 16f)
                    continue;
                if (arena.Mounds.Exists(m => Vector2.Distance(m.Position, p) < m.Radius + radius + 4f))
                    continue;

                arena.Mounds.Add(new Mound { Position = p, Radius = radius, Height = height });
            }

            PlanPerches(arena, random, FarFromMouths, shipSpot);

            (ScenePropKit.PropKind kind, int weight)[] weights = CoverWeights(arena.Biome);
            int total = 0;
            foreach (var w in weights)
                total += w.weight;

            int propCount = 18 + random.Next(9) + (arena.IsBoss ? 6 : 0);

            // Ruínas: 1 ou 2 plataformas com rampa garantidas (pontos altos extras)
            int platforms = arena.Biome == BiomeId.Ruinas ? 1 + random.Next(2) : 0;
            int reserved = arena.Props.Count; // poleiros já planejados

            for (int attempt = 0; attempt < 800 && arena.Props.Count - reserved < propCount + platforms; attempt++)
            {
                ScenePropKit.PropKind kind;
                if (platforms > 0 && arena.Props.Count - reserved < platforms)
                {
                    kind = ScenePropKit.PropKind.RuinaPlataforma;
                }
                else
                {
                    int roll = random.Next(total);
                    kind = weights[0].kind;
                    foreach (var w in weights)
                    {
                        if (roll < w.weight)
                        {
                            kind = w.kind;
                            break;
                        }

                        roll -= w.weight;
                    }
                }

                float spacing = ScenePropKit.Footprint(kind);
                Vector2 p = node.Center + RandomInDisc(random) * r * 0.85f;
                if (!FarFromMouths(p, 12f + spacing))
                    continue;
                if (arena.IsFinal && Vector2.Distance(p, shipSpot) < 18f)
                    continue;
                if (arena.Mounds.Exists(m => Vector2.Distance(m.Position, p) < m.Radius * 0.8f))
                    continue;
                if (arena.Props.Exists(o => Vector2.Distance(o.Position, p) < spacing + ScenePropKit.Footprint(o.Kind)))
                    continue;

                arena.Props.Add(new PropPlacement
                {
                    Kind = kind,
                    Position = p,
                    Yaw = (float)random.NextDouble() * 360f,
                    Scale = 0.85f + (float)random.NextDouble() * 0.35f,
                });
            }
        }

        // Poleiros (rochas altas de topo plano) para o Espinhento: 4 a 6 candidatos espalhados na borda da arena,
        // virados para o centro. O ArenaBuilder valida a linha de visão de cada um e usa de 3 a 6.
        static void PlanPerches(ArenaPlan arena, System.Random random, System.Func<Vector2, float, bool> farFromMouths,
            Vector2 shipSpot)
        {
            Node node = arena.Node;
            int count = 4 + random.Next(3);
            float start = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    float angle = start + (i + (float)random.NextDouble() * 0.3f) * Mathf.PI * 2f / count + attempt * 0.35f;
                    float radius = node.Radius * (0.68f + (float)random.NextDouble() * 0.1f);
                    Vector2 p = node.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!farFromMouths(p, 16f))
                        continue;
                    if (arena.IsFinal && Vector2.Distance(p, shipSpot) < 22f)
                        continue;
                    if (arena.Mounds.Exists(m => Vector2.Distance(m.Position, p) < m.Radius * 0.5f))
                        continue;
                    if (arena.Props.Exists(o => Vector2.Distance(o.Position, p) < 10f))
                        continue;

                    Vector2 toCenter = node.Center - p;
                    arena.Props.Add(new PropPlacement
                    {
                        Kind = ScenePropKit.PropKind.Poleiro,
                        Position = p,
                        Yaw = Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg,
                        Scale = 1f,
                    });
                    break;
                }
            }
        }

        // Onde fica a nave Dédalo na arena final: no fundo, oposto à entrada
        public static Vector2 ShipSpot(ArenaPlan arena) =>
            arena.Node.Center - arena.Node.ToPrevious * arena.Node.Radius * 0.55f;

        static Vector2 RandomInDisc(System.Random random)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
