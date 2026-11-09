using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Vila Suspensa (era a Vila das Engrenagens; pedido do usuário em 2026-11-08, depois de recusar a versão dos
    // Artífices, que reaproveitava as peças do AR_Casas): o ombro da montanha é só a base, e a praça da vila flutua
    // 12 m acima dele numa ilha de pedra (AR_Ilha_Suspensa, kit AR_Suspensa no Blender) sustentada por quatro pilares
    // de luz que sobem de emissores góticos no chão até os receptores sob a ilha. A barriga fica à mostra (coroa de
    // cabeça para baixo, bobinas, cristais e correntes) e a ponta da quilha aponta para a bacia do campo no chão. Uma
    // escada de degraus flutuantes sobe de cada chegada até a beira da ilha (rampa invisível por baixo); em cima, o
    // calçamento com neve, a balaustrada de veio aceso na beira, o farol regulador no meio, três oficinas de térreo
    // aberto na metade de trás e a estação do teleférico na beira virada para a torre do relógio (os cabos saem das
    // polias dela). No chão: casas dos aprendizes pela beira de trás, o parapeito, um mastro-guindaste que segura um
    // bloco num feixe, um anel sendo montado no ar (gira devagar sobre o feixe da base) e colunas-lanterna. Sorteio
    // próprio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        const float k_IsleLift = 12f;           // do chão da vila ao calçamento da ilha
        const float k_IslePlaza = 17f;          // raio do calçamento (a cornija do kit vai até 18,4)
        const float k_IsleRim = 18.4f;
        const float k_IsleReceiverRadius = 15.5f, k_IsleReceiverY = -3.54f;    // no kit, ângulos 45° + 90° k
        const float k_EmitterTip = 2.9f;
        const float k_StepRise = 0.3f, k_StepRun = 0.62f;
        const int k_StepCount = 40;             // 12 m de subida, 24,8 m de comprido
        const float k_StepWidth = 3.6f;
        const float k_RingLift = 6.5f;          // do chão ao centro do anel da montagem (raio de fora 2,95; a base tem 0,68)

        // Casas dos aprendizes, na ordem em que entram pela beira (largura e fundo com folga)
        static readonly (string Model, float W, float D)[] k_ApprenticeHouses =
        {
            ("AR_Casa_Aprendiz", 9f, 9.6f), ("AR_Casa_Aprendiz_B", 10.8f, 8.8f),
        };

        static Vector3 BuildSuspendedVillage(Node n, Vector3 city, List<Vector3> exits, Vector3 clockPeak)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3501);
            Vector3 f = Flat(city - n.Center);
            var root = new GameObject("Vila_Suspensa").transform;
            root.SetParent(s_World, false);
            float floor = n.Center.y;
            Vector3 c = new Vector3(n.Center.x, floor, n.Center.z);
            Vector3 top = c + Vector3.up * k_IsleLift;
            float view = Mathf.Atan2(f.x, f.z), back = view + Mathf.PI;
            float AngleOf(Vector3 p) => Mathf.Atan2(p.x - c.x, p.z - c.z);

            var gates = new List<Vector3>();
            foreach (Vector3 g in exits)
                if (gates.TrueForAll(other => Vector3.Distance(other, g) > 1f))
                    gates.Add(g);
            var stairs = new List<float>();
            foreach (Vector3 g in gates)
                stairs.Add(AngleOf(g));

            // A estação na beira, virada para a torre do relógio (longe das escadas)
            float station = AngleOf(c + Flat(clockPeak - c));
            foreach (float s in stairs)
                if (AngleGap(station, s) < 30f * Mathf.Deg2Rad)
                    station = s + Mathf.Sign(Mathf.DeltaAngle(s * Mathf.Rad2Deg, station * Mathf.Rad2Deg) + 0.01f) * 30f * Mathf.Deg2Rad;

            // A ilha girada para os receptores (e os emissores embaixo) ficarem longe das escadas e da estação
            Vector3 Receiver(float yaw, int k)
            {
                float a = (45f + 90f * k) * Mathf.Deg2Rad;
                return KitPoint(top, yaw, 1f, new Vector3(Mathf.Cos(a) * k_IsleReceiverRadius, Mathf.Sin(a) * k_IsleReceiverRadius, k_IsleReceiverY));
            }

            float isleYaw = 0f, bestGap = -1f;
            for (float yaw = 0f; yaw < 90f; yaw += 3f)
            {
                float gap = Mathf.PI;
                for (int k = 0; k < 4; k++)
                {
                    float a = AngleOf(Receiver(yaw, k));
                    foreach (float s in stairs)
                        gap = Mathf.Min(gap, AngleGap(a, s));
                    gap = Mathf.Min(gap, AngleGap(a, station));
                }
                if (gap > bestGap)
                {
                    bestGap = gap;
                    isleYaw = yaw;
                }
            }

            PlaceTech("AR_Ilha_Suspensa", top, isleYaw, root);
            var slab = new MeshData();
            s_Platforms.Add((top, k_IslePlaza));
            Prism(slab, top, k_IslePlaza, k_IslePlaza, 24, top.y, top.y - 0.8f, true);
            Pave(slab);

            // Pilares de luz: emissor no chão, feixe até o receptor; a bacia do campo sob a quilha
            Mesh shaft = LightShaftMesh();
            Material shaftMaterial = EngineBeamMaterial();
            var emitters = new List<Vector3>();
            for (int k = 0; k < 4; k++)
            {
                Vector3 receiver = Receiver(isleYaw, k);
                var foot = new Vector3(receiver.x, floor, receiver.z);
                emitters.Add(foot);
                PlaceTech("PR_Emissor_Pilar", foot, YawOf(c - foot), root);
                LightShaft(root, "Pilar_Luz_" + k, shaft, shaftMaterial, foot + Vector3.up * k_EmitterTip, receiver, 0.55f);
            }
            PlaceTech("PR_Bacia_Campo", c, YawOf(f), root);

            // Escadas de degraus flutuantes, uma de cada chegada até a beira da ilha
            foreach (float s in stairs)
            {
                Vector3 outward = Around(s);
                for (int i = 0; i < k_StepCount; i++)
                {
                    float r = k_IsleRim + 0.05f + (k_StepCount - 1 - i) * k_StepRun + k_StepRun * 0.5f;
                    PlaceTech("PR_Degrau_Flutuante", c + outward * r + Vector3.up * ((i + 1) * k_StepRise), YawOf(outward), root, 1f, false);
                }
                Vector3 low = c + outward * (k_IsleRim + 0.05f + k_StepCount * k_StepRun);
                Vector3 high = c + outward * (k_IsleRim + 0.05f) + Vector3.up * (k_StepCount * k_StepRise);
                SolidRamp(s_PathColliders, low, high, k_StepWidth);
                SolidRamp(s_PathColliders, high, c + outward * (k_IslePlaza - 0.5f) + Vector3.up * k_IsleLift, k_StepWidth);
            }

            // Em cima: farol no meio, estação na beira, balaustrada aberta nas escadas e na estação
            PlaceTech("PR_Farol_Suspensao", top, YawOf(f), root);
            Vector3 stationOut = Around(station);
            Vector3 stationAt = top + stationOut * (k_IslePlaza - 3.6f);
            float stationYaw = YawOf(Flat(clockPeak - stationAt));
            PlaceTech("PR_Estacao_Suspensa", stationAt, stationYaw, root);
            Vector3 cableStart = KitPoint(stationAt, stationYaw, 1f, new Vector3(0f, -4.4f, 6.45f));

            const int rail = 28;
            const float railRadius = 17.7f;
            for (int i = 0; i < rail; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / rail;
                bool gap = AngleGap(a, station) < 4.4f / railRadius;
                foreach (float s in stairs)
                    gap |= AngleGap(a, s) < (k_StepWidth * 0.5f + 1.6f) / railRadius;
                if (!gap)
                    PlaceTech("PR_Balaustrada_Suspensa", c + Around(a) * railRadius + Vector3.up * (k_IsleLift - 0.04f), YawOf(Around(a)), root);
            }

            // Oficinas na metade de trás, de frente para o farol, longe das chegadas das escadas e da estação
            var workshops = new List<float>();
            var candidates = new List<float>();
            for (float deg = 0f; deg <= 130f; deg += 5f)
            {
                candidates.Add(back + deg * Mathf.Deg2Rad);
                if (deg > 0f)
                    candidates.Add(back - deg * Mathf.Deg2Rad);
            }
            foreach (float a in candidates)
            {
                if (workshops.Count >= 3)
                    break;
                bool ok = AngleGap(a, station) > 52f * Mathf.Deg2Rad;
                foreach (float s in stairs)
                    ok &= AngleGap(a, s) > 38f * Mathf.Deg2Rad;
                foreach (float w in workshops)
                    ok &= AngleGap(a, w) > 62f * Mathf.Deg2Rad;
                if (!ok)
                    continue;
                workshops.Add(a);
                Vector3 p = top + Around(a) * 10.5f;
                PlaceTech("AR_Oficina_Suspensa", p, YawOf(top - p) + R(-4f, 4f), root);
            }

            // No chão: parapeito e casas dos aprendizes pela beira de trás
            KitParapet(root, n, gates);
            List<HouseSpot> houses = KitHouseRing(root, n, f, 62f, gates, null, 0f, 10, k_ApprenticeHouses);

            var taken = new List<(Vector3 At, float Radius)>();
            bool Free(Vector3 p, float clear)
            {
                float r = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
                if (r < k_IsleRim + 1f + clear || r > n.Radius - 2f - clear)
                    return false;
                foreach (float s in stairs)
                    if (SegmentDistance(p, c + Around(s) * k_IsleRim, c + Around(s) * n.Radius) < k_StepWidth * 0.5f + 1.5f + clear)
                        return false;
                foreach (Vector3 g in gates)
                    if (SegmentDistance(p, c, g) < 3f + clear)
                        return false;
                foreach (HouseSpot h in houses)
                    if (Vector3.Distance(Flat(h.Foot - p), Vector3.zero) < Mathf.Max(h.W, h.D) * 0.5f + 1f + clear)
                        return false;
                foreach ((Vector3 at, float radius) in taken)
                    if (Vector3.Distance(Flat(at - p), Vector3.zero) < radius + clear)
                        return false;
                return true;
            }

            // Mastro-guindaste: a lança aponta para a ilha e o bloco fica no ar fora da beira dela
            foreach (float a in candidates)
            {
                Vector3 p = c + Around(a) * 31f;
                Vector3 block = c + Around(a) * 23f;
                if (!Free(p, 2.6f) || !Free(block, 1.6f))
                    continue;
                PlaceTech("PR_Mastro_Guindaste", p, YawOf(c - p), root);
                taken.Add((p, 3f));
                taken.Add((block, 2f));
                break;
            }

            // Anel sendo montado no ar, girando devagar sobre o feixe da base
            foreach (float a in candidates)
            {
                Vector3 p = c + Around(a + 0.4f) * 28f;
                if (!Free(p, 3.4f))
                    continue;
                PlaceTech("PR_Montagem_Base", p, YawOf(c - p), root);
                GameObject ring = PlaceTech("PR_Montagem_Anel", p + Vector3.up * k_RingLift, YawOf(c - p) + 90f, root, 1f, false);
                if (ring)
                {
                    var spin = ring.AddComponent<SlowRotation>();
                    spin.DegreesPerSecond = 9f;
                    spin.Bob = 0.12f;
                    spin.BobSpeed = 0.6f;
                }
                LightShaft(root, "Feixe_Montagem", shaft, shaftMaterial, p + Vector3.up * 0.68f, p + Vector3.up * (k_RingLift - 2.95f), 0.3f);
                taken.Add((p, 3.6f));
                break;
            }

            // Colunas-lanterna pelo chão
            foreach (float deg in new[] { 35f, -35f, 95f, -95f, 150f, -150f })
            {
                Vector3 p = c + Around(view + deg * Mathf.Deg2Rad) * 25f;
                if (!Free(p, 1.2f))
                    continue;
                VillagePiece("PR_Poste", p, YawOf(c - p), root, PicosGothicKit.Folder);
                taken.Add((p, 1.5f));
            }

            s_Random = saved;
            return cableStart;
        }

        // Feixe de luz reto de 'a' a 'b' (cilindro aditivo de raio 'radius', mais claro nas pontas), sem sombra
        static void LightShaft(Transform root, string name, Mesh mesh, Material material, Vector3 a, Vector3 b, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = a;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            go.transform.localScale = new Vector3(radius, (b - a).magnitude, radius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Cilindro de 12 lados de altura 1 e raio 1 (base na origem, para cima), com a transparência nas cores dos
        // vértices: mais forte nas pontas, onde encosta no emissor e no receptor
        static Mesh LightShaftMesh()
        {
            IcaroAssetUtility.EnsureFolder(GridSceneKit.MeshFolder);
            string path = $"{GridSceneKit.MeshFolder}/{k_Prefix}_PilarLuz.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = k_Prefix + "_PilarLuz" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int sides = 12;
            float[] heights = { 0f, 0.2f, 0.5f, 0.8f, 1f };
            float[] alphas = { 0.5f, 0.3f, 0.22f, 0.3f, 0.5f };
            for (int i = 0; i < sides; i++)
            {
                float t = i * Mathf.PI * 2f / sides;
                var ring = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                for (int j = 0; j < heights.Length; j++)
                {
                    vertices.Add(ring + Vector3.up * heights[j]);
                    colors.Add(new Color(1f, 1f, 1f, alphas[j]));
                }
            }

            int rows = heights.Length;
            for (int i = 0; i < sides; i++)
            {
                int a = i * rows, b = ((i + 1) % sides) * rows;
                for (int j = 0; j < rows - 1; j++)
                    triangles.AddRange(new[] { a + j, a + j + 1, b + j + 1, a + j, b + j + 1, b + j });
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
    }
}
