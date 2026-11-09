using System.Collections.Generic;
using UnityEngine;

namespace Icaro.EditorTools
{
    // Praça do Monumento ao Reino (pedido do usuário em 2026-11-09): o platô do antigo portão, no pé da escadaria em
    // zigue-zague, vira a entrada cerimonial antes da ponte que leva à Vila da Guarda e ao Reino. Só passagem, sem sala.
    // No meio, o obelisco (PR_Monumento_Reino) sobre o brasão do reino no chão (PR_Mosaico_Brasao, o mostrador do relógio
    // às 11h58, com o "12" virado para a ponte); quatro mastros de estandartes violeta, dois de cada lado de cada chegada,
    // como um portal; bancos e lixeiras virados para o monumento nos trechos longe dos caminhos. Peças do kit da Ponte do
    // Reino (ArteFonte/Picos/PonteDoReino_Kit) e do mobiliário das vilas. Sem sorteio.
    public static partial class PicosBuilder
    {
        static void BuildMonumentSquare(Transform parent, Node n, List<Vector3> exits, Vector3 toBridge)
        {
            var root = new GameObject("Praca_Monumento").transform;
            root.SetParent(parent, false);
            Vector3 c = n.Center;
            float yaw = YawOf(toBridge);
            PicosGothicKit.Place(s_Materials, "PR_Mosaico_Brasao", c, yaw, root, false, ReinoFolder);
            PicosGothicKit.Place(s_Materials, "PR_Monumento_Reino", c, yaw, root, true, ReinoFolder);

            // Mastros: emolduram cada chegada a 15,5 m do meio, 24° para cada lado do caminho, os estandartes de frente
            // para quem chega
            var dirs = new List<Vector3>();
            foreach (Vector3 e in exits)
            {
                Vector3 dir = Flat(e - c);
                dirs.Add(dir);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = c + Quaternion.Euler(0f, side * 24f, 0f) * dir * 15.5f;
                    PicosGothicKit.Place(s_Materials, "PR_Mastro_Estandarte", p, YawOf(dir), root, true, ReinoFolder);
                }
            }

            // Bancos de frente para o monumento, com uma lixeira ao lado, nos trechos do círculo mais longe dos caminhos
            var spots = new List<Vector3>();
            for (int k = 0; k < 24; k++)
            {
                Vector3 dir = Around(k * Mathf.PI * 2f / 24f);
                Vector3 p = c + dir * 13f;
                float clear = float.MaxValue;
                foreach (Vector3 e in exits)
                    clear = Mathf.Min(clear, SegmentDistance(p, c, new Vector3(e.x, c.y, e.z)));
                if (clear > 7f)
                    spots.Add(p);
            }

            int placed = 0;
            var used = new List<Vector3>();
            foreach (Vector3 p in spots)
            {
                if (placed == 4)
                    break;
                bool near = false;
                foreach (Vector3 u in used)
                    near |= Vector3.Distance(u, p) < 9f;
                if (near)
                    continue;
                Vector3 toCenter = Flat(c - p);
                PicosGothicKit.Place(s_Materials, "PR_Banco", p, YawOf(toCenter), root, true, PicosGothicKit.VillagesFolder);
                Vector3 side = Vector3.Cross(Vector3.up, toCenter);
                PicosGothicKit.Place(s_Materials, "PR_Lixeira", p + side * 1.6f, YawOf(toCenter), root, true, PicosGothicKit.VillagesFolder);
                used.Add(p);
                placed++;
            }

            if (placed == 0)
                Debug.Log("[Picos] Praça do Monumento: nenhum trecho livre dos caminhos para os bancos.");
        }
    }
}
