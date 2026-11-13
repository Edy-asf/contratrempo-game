using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Icaro
{
    // Área de chão andável de uma arena, em m²: os triângulos do NavMesh dentro do círculo do domo
    // (ArenaDome.Measure) e entre 14 m abaixo do ponto mais baixo e 40 m acima do mais alto da sala. Cada triângulo
    // conta pela fração dos seus três cantos e do centro que cai dentro do círculo. Usada pelo limite de vivos ao
    // mesmo tempo (CombatBalance.AliveLimitFor): salas grandes comportam mais inimigos. A triangulação do NavMesh é
    // lida uma vez por cena e guardada.
    public static class ArenaFloor
    {
        static Vector3[] s_Vertices;
        static int[] s_Indices;
        static int s_SceneStamp = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Vertices = null;
            s_Indices = null;
            s_SceneStamp = -1;
        }

        // Esquece a triangulação guardada (o NavMesh mudou)
        public static void Invalidate() => s_SceneStamp = -1;

        public static float AreaOf(RoomManager room)
        {
            if (!room || !room.GetComponent<Collider>())
                return 0f;
            ArenaDome.Measure(room, out Vector2 center, out float radius, out float low, out float high);
            return Area(center, radius, low - 14f, high + 40f);
        }

        public static float Area(Vector2 center, float radius, float bottom, float top)
        {
            Load();
            if (s_Indices == null)
                return 0f;
            float r2 = radius * radius;
            float total = 0f;
            for (int i = 0; i + 2 < s_Indices.Length; i += 3)
            {
                Vector3 a = s_Vertices[s_Indices[i]], b = s_Vertices[s_Indices[i + 1]], c = s_Vertices[s_Indices[i + 2]];
                Vector3 mid = (a + b + c) / 3f;
                if (mid.y < bottom || mid.y > top)
                    continue;
                int inside = (Inside(a) ? 1 : 0) + (Inside(b) ? 1 : 0) + (Inside(c) ? 1 : 0) + (Inside(mid) ? 1 : 0);
                if (inside == 0)
                    continue;
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f * inside / 4f;
            }

            return total;

            bool Inside(Vector3 p)
            {
                float dx = p.x - center.x, dz = p.z - center.y;
                return dx * dx + dz * dz <= r2;
            }
        }

        static void Load()
        {
            // Uma leitura por conjunto de cenas carregadas (o portal troca a cena)
            int stamp = 17;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                stamp = stamp * 31 + SceneManager.GetSceneAt(i).GetHashCode();
            if (stamp == s_SceneStamp && s_Indices != null)
                return;
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            s_Vertices = triangulation.vertices;
            s_Indices = triangulation.indices;
            s_SceneStamp = stamp;
        }
    }
}
