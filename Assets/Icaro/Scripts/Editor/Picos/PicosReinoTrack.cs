using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MeshData = Icaro.EditorTools.GridSceneKit.MeshData;

namespace Icaro.EditorTools
{
    // Trilho de levitação da Ponte do Reino (pedido do usuário em 2026-11-07, o 3.A do ar futurista): um trilho baixo e
    // contínuo no meio do tabuleiro, peça à parte por cima das lajes (cantaria lavrada com um friso de liga dos lados,
    // dois trilhos de liga e o veio de luz lilás correndo no meio), da cidade até a rotatória, em anel em volta do
    // monumento, até o bastião (para-choque antes do estrado do portal), do outro lado do portal até o desabamento, e nos
    // trechos de lá entre os vãos caídos, sempre terminando partido junto deles. Cápsulas-relicário (PR_Capsula do kit
    // AR_Tecno) paradas flutuando sobre ele perto da rotatória, no lugar do comboio de carroças, e algumas tombadas e
    // apagadas perto dos vãos. Sorteio próprio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        const float k_TrackHeight = 0.22f;      // topo da cantaria acima das lajes
        const float k_TrackWidth = 1.8f;
        const float k_TrackRing = 18f;           // raio do anel em volta do monumento da rotatória (degraus até 13,5 m)

        static RuinMesh s_TrackStone;
        static MeshData s_TrackAlloy, s_TrackGlow;
        static System.Random s_TrackRandom;

        static float TR(float min, float max) => min + (float)s_TrackRandom.NextDouble() * (max - min);

        static void BuildReinoTrack(Transform props)
        {
            s_TrackStone = new RuinMesh(4401);
            s_TrackAlloy = new MeshData();
            s_TrackGlow = new MeshData();
            s_TrackRandom = new System.Random(4401);
            s_CapsizedTrains.Clear();
            Vector3 d = ReinoDir, up = Vector3.up;
            float rotT = ReinoRotundaT, bastT = k_ReinoBastionT;
            float join = k_TrackRing + k_TrackWidth * 0.5f + 0.02f;     // encosta no lado de fora do anel

            // Da cidade (para-choque na beira dela) à rotatória, o anel e da rotatória ao bastião (para-choque antes da
            // escada do estrado do portal, que começa 23,5 m antes do meio dele)
            // (começa logo depois do pátio da barbacã e quebra nas crateras do cerco, PicosReinoSiege)
            TrackAroundCraters(s_ReinoGateT + 3f, rotT - join, End.Buffer, End.Open);
            TrackRing(ReinoRotunda, k_TrackRing);
            TrackAroundCraters(rotT + join, bastT - 25f, End.Open, End.Buffer);

            // Do outro lado do portal (a escada de lá termina 10,7 m depois do meio) até o desabamento e, nos trechos de
            // lá, entre os vãos caídos (o tabuleiro termina em dente 3,5 m antes de cada um)
            Track(ReinoAt(bastT + 12.5f), ReinoAt(k_ReinoCollapseFrom - 6f), End.Buffer, End.Broken);
            foreach ((float a, float b) in Subtract(k_ReinoCollapseTo, k_ReinoStraightEnd, k_ReinoFarGaps, 0f))
                Track(ReinoAt(a + 6f), ReinoAt(b - (b < k_ReinoStraightEnd - 0.01f ? 6f : 2f)), End.Broken, End.Broken);

            // Vagões (PR_Capsula, 11,2 m) parados e pousados no trilho perto da rotatória: um par engatado no trecho do
            // bastião, um sozinho no da cidade e um junto do para-choque do bastião (sem energia: nada aceso)
            foreach ((float t, bool forward) in k_ReinoCars.Select(c => (c.T + (c.FromBastion ? bastT : rotT), c.Forward)))
                PlaceTech("PR_Capsula", ReinoAt(t) + up * k_TrackHeight, YawOf(forward ? d : -d), props);

            // Tombadas e apagadas perto dos vãos: de lado junto do desabamento (do lado de cá, que se anda) e perto dos
            // vãos de lá, com o teto para o meio da ponte (a flecha não passa do parapeito); uma fora do trilho, pousada
            // no tabuleiro
            Vector3 right = ReinoRight;
            Capsized(props, ReinoAt(k_ReinoCollapseFrom - 17f) + right * 5.4f, YawOf(d) + 22f, 90f);
            Capsized(props, ReinoAt(k_ReinoFarGaps[0].A - 15f) - right * 4.6f, YawOf(-d) + 14f, 90f);
            Capsized(props, ReinoAt(k_ReinoFarGaps[1].B + 13f) + right * 5.2f, YawOf(d) - 28f, 90f);
            Capsized(props, ReinoAt(k_ReinoFarGaps[2].A - 12f) + right * 4.8f, YawOf(d) + 35f, 92f);

            CreateRuin("Trilho_Reino", s_TrackStone, true, false);
            GridSceneKit.CreateObject(k_Prefix + "_Trilho_Liga", "Trilho_Liga", s_TrackAlloy, s_Materials.Alloy, s_World, false);
            // O veio e os pontos de luz do trilho ficam apagados (pedido do usuário em 2026-11-09): vão escuro
            GridSceneKit.CreateObject(k_Prefix + "_Trilho_Luz", "Trilho_Luz", s_TrackGlow, s_Materials.Dark, s_World, false);
        }

        // Vagões parados: t a partir da rotatória (ou do bastião) e o sentido
        static readonly (float T, bool FromBastion, bool Forward)[] k_ReinoCars =
            { (46f, false, true), (58.4f, false, true), (-72f, false, false), (-33f, true, true) };

        static IEnumerable<float> ReinoCarTs() => k_ReinoCars.Select(c => c.T + (c.FromBastion ? k_ReinoBastionT : ReinoRotundaT));

        enum End { Open, Buffer, Broken }

        // Trilho reto de 'a' a 'b' (pontos no piso, no meio do trilho): blocos de cantaria de ~3 m com as pontas
        // chanfradas, o friso de liga dos dois lados, dois trilhos de liga e o veio de luz por cima e, a cada ~9 m, uma
        // braçadeira de liga com dois pontos de luz. Pontas: aberta (encosta em outro trilho), para-choque ou partida
        static void Track(Vector3 a, Vector3 b, End endA, End endB)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < 2f)
                return;
            Vector3 f = along / length, up = Vector3.up, right = Vector3.Cross(up, f);
            Quaternion rotation = Quaternion.LookRotation(f);
            float h = k_TrackHeight, w = k_TrackWidth;

            // Partida: os últimos ~1,6 m viram um bloco caído inclinado, o trilho de liga e o veio param antes dele, e
            // dois pedaços soltos no tabuleiro do lado
            float cutA = endA == End.Broken ? TR(1.4f, 1.9f) : 0f, cutB = endB == End.Broken ? TR(1.4f, 1.9f) : 0f;
            Vector3 a2 = a + f * cutA, b2 = b - f * cutB;
            float run = length - cutA - cutB;
            int blocks = Mathf.Max(1, Mathf.RoundToInt(run / 3f));
            float block = run / blocks;
            for (int i = 0; i < blocks; i++)
            {
                Vector3 c = a2 + f * (block * (i + 0.5f));
                BlockMesh.ChamferBox(s_TrackStone.Pick(), c + up * (h * 0.5f - 0.03f), new Vector3(w, h + 0.06f, block + 0.02f), rotation, 0.04f);
            }

            float metal0 = endA == End.Broken ? 0.6f : 0.05f, metal1 = endB == End.Broken ? 0.6f : 0.05f;
            Vector3 m0 = a2 + f * metal0, m1 = b2 - f * metal1;
            float metal = Vector3.Distance(m0, m1);
            Vector3 mc = (m0 + m1) * 0.5f;
            if (metal > 0.2f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    BlockMesh.Box(s_TrackAlloy, mc + right * s * (w * 0.5f + 0.015f) + up * (h * 0.55f), new Vector3(0.03f, 0.07f, metal), rotation);
                    BlockMesh.Box(s_TrackAlloy, mc + right * s * 0.55f + up * (h + 0.01f), new Vector3(0.18f, 0.08f, metal), rotation);
                }

                BlockMesh.Box(s_TrackGlow, mc + up * (h + 0.005f), new Vector3(0.1f, 0.05f, metal), rotation);
            }

            // Braçadeiras de liga, com um ponto de luz em cada lado
            int clamps = Mathf.FloorToInt(run / 9f);
            for (int k = 1; k <= clamps; k++)
            {
                Vector3 c = a2 + f * (run * k / (clamps + 1f));
                BlockMesh.ChamferBox(s_TrackAlloy, c + up * (h * 0.5f + 0.03f), new Vector3(w + 0.16f, h + 0.12f, 0.36f), rotation, 0.03f);
                foreach (float s in new[] { -1f, 1f })
                    BlockMesh.Box(s_TrackGlow, c + right * s * (w * 0.5f + 0.09f) + up * (h * 0.6f), new Vector3(0.02f, 0.08f, 0.16f), rotation);
            }

            TrackEnd(a, a2, -f, endA);
            TrackEnd(b, b2, f, endB);
        }

        // Ponta do trilho em 'tip' (o fim da cantaria inteira em 'cut', 'f' para fora)
        static void TrackEnd(Vector3 tip, Vector3 cut, Vector3 f, End end)
        {
            Vector3 up = Vector3.up, right = Vector3.Cross(up, f);
            Quaternion rotation = Quaternion.LookRotation(f);
            float h = k_TrackHeight, w = k_TrackWidth;
            if (end == End.Buffer)
            {
                // Para-choque: bloco de cantaria com a tampa de liga e a faixa de luz virada para o trilho
                Vector3 c = tip + f * 0.6f;
                BlockMesh.ChamferBox(s_TrackStone.Pick(), c + up * 0.42f, new Vector3(w + 0.5f, 0.9f, 1.2f), rotation, 0.08f);
                BlockMesh.ChamferBox(s_TrackAlloy, c + up * 0.91f, new Vector3(w + 0.3f, 0.12f, 1.0f), rotation, 0.03f);
                BlockMesh.Box(s_TrackGlow, c - f * 0.61f + up * 0.6f, new Vector3(1.1f, 0.12f, 0.03f), rotation);
            }
            else if (end == End.Broken)
            {
                // Bloco partido inclinado para fora e para baixo e dois pedaços soltos do lado
                float length = Vector3.Distance(tip, cut);
                Quaternion tilt = rotation * Quaternion.Euler(TR(4f, 9f), TR(-6f, 6f), TR(-5f, 5f));
                BlockMesh.ChamferBox(s_TrackStone.Pick(), cut + f * (length * 0.5f) + up * (h * 0.5f - 0.1f),
                    new Vector3(w * TR(0.75f, 0.95f), h + 0.06f, length - 0.1f), tilt, 0.05f);
                for (int k = 0; k < 2; k++)
                {
                    float size = TR(0.35f, 0.7f);
                    BlockMesh.ChamferBox(s_TrackStone.Pick(), cut + f * TR(-0.6f, 1.2f) + right * (k == 0 ? 1f : -1f) * TR(1.3f, 2.2f) +
                                                             up * (size * 0.3f), new Vector3(size * 1.4f, size * 0.6f, size),
                        Quaternion.Euler(TR(-15f, 15f), TR(0f, 360f), TR(-15f, 15f)), 0.05f);
                }
            }
        }

        // Anel de trilho em volta de 'center' com raio 'radius' (pelo meio do trilho): trechos retos curtos, cada um
        // um pouco mais comprido que a corda para fechar as dobras
        static void TrackRing(Vector3 center, float radius)
        {
            const int pieces = 64;
            Vector3 up = Vector3.up;
            float h = k_TrackHeight, w = k_TrackWidth;
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / pieces);
            for (int i = 0; i < pieces; i++)
            {
                float a0 = i * 2f * Mathf.PI / pieces, a1 = (i + 1) * 2f * Mathf.PI / pieces;
                Vector3 p0 = center + Around(a0) * radius, p1 = center + Around(a1) * radius;
                Vector3 c = (p0 + p1) * 0.5f, f = (p1 - p0).normalized, right = Vector3.Cross(up, f);
                Quaternion rotation = Quaternion.LookRotation(f);
                BlockMesh.ChamferBox(s_TrackStone.Pick(), c + up * (h * 0.5f - 0.03f), new Vector3(w, h + 0.06f, chord + 0.08f), rotation, 0.04f);
                foreach (float s in new[] { -1f, 1f })
                {
                    float r = radius + s * (w * 0.5f + 0.015f);
                    BlockMesh.Box(s_TrackAlloy, c + right * s * (w * 0.5f + 0.015f) + up * (h * 0.55f),
                        new Vector3(0.03f, 0.07f, chord * r / radius + 0.04f), rotation);
                    BlockMesh.Box(s_TrackAlloy, c + right * s * 0.55f + up * (h + 0.01f),
                        new Vector3(0.18f, 0.08f, chord * (radius + s * 0.55f) / radius + 0.03f), rotation);
                }

                BlockMesh.Box(s_TrackGlow, c + up * (h + 0.005f), new Vector3(0.1f, 0.05f, chord + 0.02f), rotation);
                if (i % 8 == 4)
                {
                    BlockMesh.ChamferBox(s_TrackAlloy, c + up * (h * 0.5f + 0.03f), new Vector3(w + 0.16f, h + 0.12f, 0.36f), rotation, 0.03f);
                    foreach (float s in new[] { -1f, 1f })
                        BlockMesh.Box(s_TrackGlow, c + right * s * (w * 0.5f + 0.09f) + up * (h * 0.6f), new Vector3(0.02f, 0.08f, 0.16f), rotation);
                }
            }
        }

        // Vagão tombado e apagado: deitado de lado, reto (inclinado ele encostava só numa ponta e o resto ficava no ar),
        // assentado pelo costado (BodySettle); as luzes viram vão escuro. Os escombros em volta vêm depois (BuildCapsizedWreckage)
        static void Capsized(Transform props, Vector3 floor, float yaw, float roll)
        {
            GameObject go = PlaceTech("PR_Capsula", floor, yaw, props);
            if (!go)
                return;
            TR(-3f, 3f);        // sorteio mantido, para o resto do mapa não mudar
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll >= 0f ? 90f : -90f);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == s_Materials.Glow)
                        materials[i] = s_Materials.Dark;
                renderer.sharedMaterials = materials;
            }

            BodySettle(go, floor.y);
            s_CapsizedTrains.Add((go, floor, yaw));
        }
    }
}
