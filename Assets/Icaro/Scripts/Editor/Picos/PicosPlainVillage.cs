using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Vila do ombro (era a Vila das Engrenagens; na planta continua "Engrenagens"; nome definitivo em aberto). Pedido do
    // usuário em 2026-11-08, depois de recusar os Artífices, a Vila Suspensa e o Estaleiro Celeste: uma vila genérica,
    // como a Escalonada. Casas do kit pela beira de trás, de frente para a praça (o lado da cidade aberto, só com o
    // parapeito), a fonte congelada no meio, barracas, colunas-lanterna, as peças de tecnologia encostadas nas paredes
    // e a estação do teleférico na beira virada para a torre do relógio (os cabos saem das polias dela). Sorteio
    // próprio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        static Vector3 BuildPlainVillage(Node n, Vector3 city, List<Vector3> exits, Vector3 clockPeak)
        {
            System.Random saved = s_Random;
            s_Random = new System.Random(3701);
            Vector3 f = Flat(city - n.Center);
            var root = new GameObject("Vila_Ombro").transform;
            root.SetParent(s_World, false);
            Vector3 c = n.Center;
            float view = Mathf.Atan2(f.x, f.z);
            float AngleOf(Vector3 p) => Mathf.Atan2(p.x - c.x, p.z - c.z);

            var gates = new List<Vector3>();
            foreach (Vector3 g in exits)
                if (gates.TrueForAll(other => Vector3.Distance(other, g) > 1f))
                    gates.Add(g);
            var gateAngles = new List<float>();
            foreach (Vector3 g in gates)
                gateAngles.Add(AngleOf(g));

            // Estação do teleférico na beira virada para a torre do relógio, longe das saídas
            float station = AngleOf(c + Flat(clockPeak - c));
            foreach (float g in gateAngles)
                if (AngleGap(station, g) < 24f * Mathf.Deg2Rad)
                    station = g + Mathf.Sign(Mathf.DeltaAngle(g * Mathf.Rad2Deg, station * Mathf.Rad2Deg) + 0.01f) * 24f * Mathf.Deg2Rad;
            Vector3 stationAt = c + Around(station) * (n.Radius - 5f);
            float stationYaw = YawOf(Flat(clockPeak - stationAt));
            PlaceTech("PR_Estacao_Suspensa", stationAt, stationYaw, root);
            Vector3 cableStart = KitPoint(stationAt, stationYaw, 1f, new Vector3(0f, -4.4f, 6.45f));

            // Casas pela beira (fora da vista da cidade e da estação) e o parapeito
            KitHouseRing(root, n, f, 62f, gates, new[] { stationAt }, 6f, 12);
            KitParapet(root, n, gates);

            // Praça: fonte no meio, barracas viradas para ela e colunas-lanterna, fora dos caminhos das saídas e da estação
            string kit = PicosGothicKit.VillagesFolder;
            PicosGothicKit.Place(s_Materials, "PR_Fonte_Congelada", c, YawOf(f), root, true, kit);
            foreach ((float deg, float distance, string model) in new[]
                     { (55f, 15f, "PR_Barraca"), (-62f, 14f, "PR_Barraca"), (125f, 16f, "PR_Barraca"), (-130f, 16f, "PR_Barraca"),
                       (30f, 22f, "PR_Poste"), (-30f, 22f, "PR_Poste"), (95f, 25f, "PR_Poste"), (-95f, 25f, "PR_Poste"),
                       (155f, 21f, "PR_Poste"), (-155f, 21f, "PR_Poste") })
            {
                float angle = view + deg * Mathf.Deg2Rad;
                bool blocked = AngleGap(angle, station) < 0.3f;
                foreach (float gate in gateAngles)
                    blocked |= AngleGap(angle, gate) < 0.3f;
                if (blocked)
                    continue;
                Vector3 p = c + Around(angle) * distance;
                float yaw = YawOf(-Around(angle)) + R(-10f, 10f);
                if (model == "PR_Poste")
                {
                    Physics.SyncTransforms();
                    if (Physics.CheckBox(p + Vector3.up * 5f, new Vector3(3.5f, 3.5f, 3.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                }

                VillagePiece(model, p, yaw, root, kit);
            }

            // Tecnologia encostada nas paredes das casas: uma agulha, o relicário com o atril e um tríptico
            VillageTech(root, n, "Vila do ombro", gates, f, 62f, 1, true, 0, 1);

            s_Random = saved;
            return cableStart;
        }
    }
}
