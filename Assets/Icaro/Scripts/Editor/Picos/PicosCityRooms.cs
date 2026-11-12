using System.Collections.Generic;
using CollectObjects = Unity.AI.Navigation.CollectObjects;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro.EditorTools
{
    // Salas das arenas de Archen Kaedra (2026-11-10): Bairro dos Relojoeiros (sala 24) e Mercado Alto (sala 25). Como as
    // outras dos Picos (BuildRoom), só o espaço: gatilho, RoomManager sem CombatBalance (não tranca), NavMesh próprio e
    // até 16 pontos de spawn bem espalhados. A cidade fica girada em relação ao mundo, então a sala inteira (gatilho e
    // volume do NavMesh) segue o giro dela. Sorteio próprio, para não mudar o resto do mapa.
    public static partial class PicosBuilder
    {
        static bool s_ArchenBuilt;
        static Vector3 s_ArchenCity, s_ArchenToBridge;

        static void BuildArchenKaedraRooms()
        {
            if (!s_ArchenBuilt)
                return;
            s_ArchenBuilt = false;
            // (x, z) do meio, largura em x e fundo em z, nas coordenadas da cidade; piso, faixa de altura dos spawns e entrada
            CityRoom("Arena_Relojoeiros", "Bairro dos Relojoeiros", 24, -126f, 170f, 112f, 104f, 0.12f, -0.6f, 8.6f, (-70f, 190f), 2401);
            CityRoom("Arena_MercadoAlto", "Mercado Alto", 25, 126f, 150f, 112f, 100f, 8f, 7.4f, 12.1f, (70f, 150f), 2501);
        }

        static void CityRoom(string objectName, string roomName, int number, float cx, float cz, float width, float depth, float floor,
            float minY, float maxY, (float X, float Z) door, int seed)
        {
            var rnd = new System.Random(seed);
            Vector3 across = Vector3.Cross(Vector3.up, s_ArchenToBridge);
            Vector3 P(float x, float z, float y) => s_ArchenCity + across * x + s_ArchenToBridge * z + Vector3.up * y;
            Vector3 entrance = P(door.X, door.Z, floor);

            var root = new GameObject(objectName);
            root.transform.SetParent(s_World, false);
            root.transform.SetPositionAndRotation(P(cx, cz, floor), Quaternion.LookRotation(s_ArchenToBridge));

            // O gatilho fica 3 m para dentro das fachadas (quem passa na rua de fora não entra) e sobe 24 m
            const float Height = 24f;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, Height * 0.5f - 2f, 0f);
            box.size = new Vector3(width - 6f, Height, depth - 6f);

            var room = root.AddComponent<RoomManager>();
            room.RoomName = roomName;
            room.Doors = new RoomDoor[0];
            room.HighSpawnPoints = new HighSpawnPoint[0];
            room.Balance = null;    // sem inimigos até o bioma ter os dele
            room.BiomeIndex = 2;
            room.ArenaNumber = number;
            room.ArenaInBiome = number - 16;
            room.ArenasInBiome = number - 16;
            room.IsBoss = false;

            var navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(root.transform, false);
            var surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = box.center;
            surface.size = new Vector3(width + 4f, Height + 10f, depth + 4f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            GridSceneKit.BakeNavMesh(surface, $"{IcaroAssetUtility.ScenesFolder}/Picos_Teste_NavMesh", $"Arena_{number - 16}");

            // Todos os pontos válidos (a 9 m uns dos outros, longe da entrada); depois, até 16 bem espalhados
            var candidates = new List<Vector3>();
            float top = floor + Height;
            for (float x = -width * 0.5f + 4f; x <= width * 0.5f - 4f; x += 3f)
                for (float z = -depth * 0.5f + 4f; z <= depth * 0.5f - 4f; z += 3f)
                {
                    float jx = (float)rnd.NextDouble() * 2f - 1f, jz = (float)rnd.NextDouble() * 2f - 1f;
                    Vector3 above = P(cx + x + jx, cz + z + jz, top);
                    if (!Physics.Raycast(above, Vector3.down, out RaycastHit ground, Height + 20f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (ground.point.y < minY + s_ArchenCity.y || ground.point.y > maxY + s_ArchenCity.y ||
                        Vector3.Distance(ground.point, entrance) < 22f)
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
            Debug.Log($"[Ícaro] Archen Kaedra: sala {number} {roomName} com {points.Count} pontos de spawn (sem inimigos por enquanto).");
        }
    }
}
