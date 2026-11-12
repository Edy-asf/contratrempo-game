using UnityEngine;

namespace Icaro.EditorTools
{
    // Muralha de Archen Kaedra (pedido do usuário em 2026-11-10): o kit AR_Muralha
    // (ArteFonte/Picos/VilasDoAlto_Kit/AR_Muralha/build/03_forms.py) faz a volta toda da beira da ilha em oito trechos de
    // 45 graus, todos com a origem no meio do topo da ilha: postos sem girar no centro da cidade. A planta segue o mesmo
    // polígono de 40 lados da beira do SkyIsland e da rocha do Blender; as pontas encostam nos torreões da barbacã e,
    // atrás da basílica, nas torres das quinas do pódio. Sem sorteio (o resto do mapa não muda)
    public static partial class PicosBuilder
    {
        const int k_CityWallSectors = 8;

        static void PlaceCityWalls(Vector3 city)
        {
            var root = new GameObject("Muralha_Cidade").transform;
            root.SetParent(s_World, false);
            int placed = 0;
            for (int i = 1; i <= k_CityWallSectors; i++)
                if (PicosGothicKit.Place(s_Materials, $"AR_Muralha_Cidade_{i}", city, 0f, root, true, PicosGothicKit.VillagesFolder))
                    placed++;
            if (placed < k_CityWallSectors)
                Debug.LogWarning($"[Ícaro] Cidade: só {placed} dos {k_CityWallSectors} trechos da muralha (rode o kit AR_Muralha no Blender).");
        }
    }
}
