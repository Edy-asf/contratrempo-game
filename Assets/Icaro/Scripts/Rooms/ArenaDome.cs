using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Domo de energia que prende o jogador na arena durante a luta (no lugar dos portões). Montado em jogo pelo
    // RoomManager a partir da sala: planta circular com o raio que cobre o gatilho da sala e todos os pontos de spawn,
    // com folga; parede cilíndrica do fundo do mar até 40 m acima do ponto mais alto, com uma só grade de hexágonos
    // contínua (a malha traz a posição em metros ao longo da volta e da altura) que se apaga no alto; o teto é só
    // colisão (um disco invisível), porque uma cúpula não se cobre de hexágonos iguais sem costura. Invisível e sem
    // colisão fora da luta. Ao
    // trancar, sobe do chão com uma faixa de luz, fica aceso um instante e some; depois só aparece perto do jogador e
    // ondula onde ele encosta ou onde um tiro dele bate. Ao limpar a sala, acende inteiro e se estilhaça. Segurança: se
    // o jogador escapar (pulo por cima, quina), volta para o último ponto dentro; no modo livre (F11) atravessa à vontade.
    // Os inimigos também ficam dentro: o NavMesh, o voo do Zangão e os saltos dos chefes ignoram a colisão, então as IAs
    // usam o domo como limite (Contains, Center, Radius) e, durante a luta, todo inimigo vivo da sala (e o chefe) que
    // passa da borda volta para dentro no mesmo frame (pelo NavMesh quando anda nele).
    // Shader Icaro/ArenaDome.
    public class ArenaDome : MonoBehaviour
    {
        const float k_Margin = 6f;
        const float k_WallAbove = 40f;
        const float k_FadeFrom = 18f;
        const int k_Segments = 160;

        static readonly int k_Reveal = Shader.PropertyToID("_Reveal");
        static readonly int k_Sweep = Shader.PropertyToID("_Sweep");
        static readonly int k_Dissolve = Shader.PropertyToID("_Dissolve");
        static readonly int k_PlayerPos = Shader.PropertyToID("_PlayerPos");
        static readonly int k_FadeStart = Shader.PropertyToID("_FadeStart");
        static readonly int k_FadeEnd = Shader.PropertyToID("_FadeEnd");
        static readonly int[] k_Ripples =
        {
            Shader.PropertyToID("_Ripple0"), Shader.PropertyToID("_Ripple1"), Shader.PropertyToID("_Ripple2"),
            Shader.PropertyToID("_Ripple3"),
        };

        Vector2 m_Center;
        float m_Radius;
        RoomManager m_Room;

        // Para as IAs: o domo como limite da arena
        public Vector3 Center => new Vector3(m_Center.x, transform.position.y, m_Center.y);
        public float Radius => m_Radius;

        // O ponto está dentro, a pelo menos 'margin' metros da parede (só no plano)?
        public bool Contains(Vector3 position, float margin = 0f) =>
            Vector2.Distance(m_Center, new Vector2(position.x, position.z)) <= m_Radius - margin;
        float m_Base, m_Top;
        MeshRenderer m_Renderer;
        MeshCollider m_Collider;
        Material m_Material;
        ParticleSystem m_Shards;
        Transform m_Player;
        CharacterController m_PlayerBody;
        Vector3 m_LastInside;
        int m_NextRipple;
        float m_NextTouch;
        bool m_Locked;

        // ---------- montagem ----------

        public static ArenaDome Create(RoomManager room)
        {
            var go = new GameObject("Domo_Arena");
            go.transform.SetParent(room.transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var dome = go.AddComponent<ArenaDome>();
            dome.Build(room);
            return dome;
        }

        void Build(RoomManager room)
        {
            m_Room = room;
            // Centro: o do gatilho e dos pontos de spawn; raio: até o mais longe deles (o gatilho conta pelos lados)
            Bounds trigger = room.GetComponent<Collider>().bounds;
            Bounds area = trigger;
            float low = room.transform.position.y, high = low;
            var points = new System.Collections.Generic.List<Vector3>();
            void Add(Vector3 p)
            {
                points.Add(p);
                area.Encapsulate(new Vector3(p.x, area.center.y, p.z));
                low = Mathf.Min(low, p.y);
                high = Mathf.Max(high, p.y);
            }

            if (room.SpawnPoints != null)
                foreach (Transform point in room.SpawnPoints)
                    if (point)
                        Add(point.position);
            if (room.HighSpawnPoints != null)
                foreach (HighSpawnPoint point in room.HighSpawnPoints)
                    if (point)
                        Add(point.transform.position);
            if (room.BossPoint)
                Add(room.BossPoint.position);

            m_Center = new Vector2(area.center.x, area.center.z);
            float radius = Mathf.Max(trigger.extents.x, trigger.extents.z);
            foreach (Vector3 p in points)
                radius = Mathf.Max(radius, Vector2.Distance(m_Center, new Vector2(p.x, p.z)));
            m_Radius = radius + k_Margin;
            m_Base = low - 14f;
            m_Top = high + k_WallAbove;

            Mesh visual = BuildMesh(solid: false);
            Mesh solid = BuildMesh(solid: true);

            gameObject.AddComponent<MeshFilter>().sharedMesh = visual;
            m_Renderer = gameObject.AddComponent<MeshRenderer>();
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            var source = Resources.Load<Material>("Domo/Domo_Arena");
            Shader shader = Shader.Find("Icaro/ArenaDome");
            m_Material = source ? new Material(source) : shader ? new Material(shader) : null;
            m_Renderer.sharedMaterial = m_Material;
            if (m_Material)
            {
                // Apaga do alto do ponto mais alto + 18 m até o topo
                m_Material.SetFloat(k_FadeStart, high + k_FadeFrom - m_Base);
                m_Material.SetFloat(k_FadeEnd, m_Top - m_Base);
            }
            m_Renderer.enabled = false;

            m_Collider = gameObject.AddComponent<MeshCollider>();
            m_Collider.sharedMesh = solid;
            m_Collider.enabled = false;

            m_Shards = BuildShards(visual);
        }

        // Parede cilíndrica; UV em metros (u ao longo da volta, v do chão para cima), uma grade só. A versão sólida tem as
        // faces dos dois lados (virada só para um lado, o jogador atravessaria de dentro para fora) e um disco no topo.
        Mesh BuildMesh(bool solid)
        {
            int columns = k_Segments + 1;
            float circumference = Mathf.PI * 2f * m_Radius;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int r = 0; r < 2; r++)
            {
                float y = r == 0 ? m_Base : m_Top;
                for (int i = 0; i < columns; i++)
                {
                    float a = i * Mathf.PI * 2f / k_Segments;
                    vertices.Add(new Vector3(m_Center.x + Mathf.Cos(a) * m_Radius, y, m_Center.y + Mathf.Sin(a) * m_Radius));
                    uvs.Add(new Vector2(circumference * i / k_Segments, y - m_Base));
                }
            }

            for (int i = 0; i < k_Segments; i++)
            {
                int a = i, b = i + 1, c = i + columns, d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
                if (solid)
                    triangles.AddRange(new[] { a, b, c, b, d, c });
            }

            if (solid)
            {
                // Teto: disco no alto, dos dois lados
                int center = vertices.Count;
                vertices.Add(new Vector3(m_Center.x, m_Top, m_Center.y));
                uvs.Add(Vector2.zero);
                for (int i = 0; i < k_Segments; i++)
                {
                    int a = columns + i, b = columns + i + 1;
                    triangles.AddRange(new[] { center, a, b, center, b, a });
                }
            }

            var mesh = new Mesh { name = solid ? "Domo_Colisao" : "Domo_Visual" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Quanto do caminho do centro até a parede o ponto já percorreu (1 = na parede), só no plano
        float Inside(Vector3 position) =>
            Vector2.Distance(m_Center, new Vector2(position.x, position.z)) / m_Radius;

        // Estilhaços: quadradinhos de luz que saem da superfície, caem e somem
        ParticleSystem BuildShards(Mesh mesh)
        {
            var go = new GameObject("Estilhacos");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.9f, 1f), new Color(0.8f, 1f, 1f));
            main.gravityModifier = 0.8f;
            main.maxParticles = 1500;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh = mesh;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var material = Resources.Load<Material>("Domo/Domo_Estilhacos");
            if (!material)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = shader ? new Material(shader) : null;
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        void OnDestroy()
        {
            if (m_Material)
                Destroy(m_Material);
        }

        // ---------- luta ----------

        // A sala trancou: o domo sobe do chão, fica aceso um instante e some (fica só perto do jogador)
        public void Lock(Transform player)
        {
            m_Player = player;
            m_PlayerBody = player ? player.GetComponent<CharacterController>() : null;
            m_LastInside = player ? player.position : transform.position;
            m_Locked = true;
            m_Collider.enabled = true;
            m_Renderer.enabled = true;
            StopAllCoroutines();
            StartCoroutine(Appear());
        }

        IEnumerator Appear()
        {
            if (!m_Material)
                yield break;
            m_Material.SetFloat(k_Dissolve, 0f);
            m_Material.SetFloat(k_Reveal, 1f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                m_Material.SetFloat(k_Sweep, Mathf.SmoothStep(0f, 1.1f, t / 0.9f));
                yield return null;
            }

            m_Material.SetFloat(k_Sweep, 1.2f);
            yield return new WaitForSeconds(0.5f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                m_Material.SetFloat(k_Reveal, 1f - t / 0.8f);
                yield return null;
            }

            m_Material.SetFloat(k_Reveal, 0f);
        }

        // A sala foi limpa: acende inteiro e se estilhaça
        public void Shatter()
        {
            m_Locked = false;
            m_Collider.enabled = false;
            StopAllCoroutines();
            StartCoroutine(Break());
        }

        IEnumerator Break()
        {
            if (!m_Material)
            {
                m_Renderer.enabled = false;
                yield break;
            }

            m_Material.SetFloat(k_Sweep, 1.2f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                m_Material.SetFloat(k_Reveal, t / 0.2f);
                yield return null;
            }

            if (m_Shards)
                m_Shards.Emit(Mathf.Clamp(Mathf.RoundToInt(m_Radius * 12f), 300, 1500));
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                m_Material.SetFloat(k_Dissolve, t / 1.3f);
                yield return null;
            }

            m_Material.SetFloat(k_Dissolve, 1f);
            m_Renderer.enabled = false;
        }

        // Inimigos vivos da sala (e o chefe) que passaram da borda voltam para dentro, a uma folga do tamanho do corpo
        void KeepEnemiesInside()
        {
            if (!m_Room)
                return;
            var enemies = m_Room.AliveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i])
                    Contain(enemies[i].transform);
            }

            if (m_Room.Boss)
                Contain(m_Room.Boss.transform);
        }

        void Contain(Transform enemy)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            float margin = Mathf.Max(1.5f, agent ? agent.radius + 0.5f : 1.5f);
            Vector3 position = enemy.position;
            if (Contains(position, margin))
                return;

            Vector2 offset = new Vector2(position.x, position.z) - m_Center;
            if (offset.sqrMagnitude < 0.0001f)
                return;
            Vector2 inside = m_Center + offset.normalized * Mathf.Max(0f, m_Radius - margin - 0.5f);
            var target = new Vector3(inside.x, position.y, inside.y);

            if (agent && agent.enabled && agent.isOnNavMesh)
            {
                // Pelo NavMesh: o ponto válido mais perto da borda de dentro
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, agent.areaMask) && Contains(hit.position, margin))
                    agent.Warp(hit.position);
                else
                    agent.Warp(target);
                return;
            }

            enemy.position = target;
        }

        // Um tiro do jogador bateu no domo: ondulação no ponto
        public void Ripple(Vector3 point)
        {
            if (!m_Material || !m_Renderer.enabled)
                return;
            m_Material.SetVector(k_Ripples[m_NextRipple], new Vector4(point.x, point.y, point.z, Time.timeSinceLevelLoad));
            m_NextRipple = (m_NextRipple + 1) % k_Ripples.Length;
        }

        void LateUpdate()
        {
            if (!m_Locked)
                return;
            KeepEnemiesInside();
            if (!m_Player)
                return;

            Vector3 eye = m_Player.position + Vector3.up;
            if (m_Material)
                m_Material.SetVector(k_PlayerPos, eye);

            float inside = Inside(m_Player.position);
            if (inside < 0.95f && m_Player.position.y < m_Top - 2f)
                m_LastInside = m_Player.position;

            // Encostando na parede: ondula onde encosta
            if (inside > 0.97f && Time.time >= m_NextTouch)
            {
                m_NextTouch = Time.time + 0.3f;
                Vector3 local = m_Player.position - new Vector3(m_Center.x, 0f, m_Center.y);
                Vector3 onWall = new Vector3(m_Center.x, eye.y, m_Center.y) +
                                 new Vector3(local.x, 0f, local.z) / Mathf.Max(inside, 0.01f);
                Ripple(onWall);
            }

            // Modo livre (F11, para analisar o mapa): atravessa o domo sem ser puxado de volta
            if (PlayerFreeMode.Instance && PlayerFreeMode.Instance.Active)
                return;

            // Escapou (pela quina, por cima): volta para o último ponto dentro
            if (inside > 1.08f || m_Player.position.y > m_Top)
            {
                if (m_PlayerBody)
                    m_PlayerBody.enabled = false;
                m_Player.position = m_LastInside;
                if (m_PlayerBody)
                    m_PlayerBody.enabled = true;
                Ripple(m_LastInside + Vector3.up);
            }
        }
    }
}
