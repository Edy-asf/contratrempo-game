using UnityEngine;

namespace Icaro
{
    // Antigo HUD de "Inimigos restantes" e da barra do chefe no topo da tela. Saiu do jogo: a contagem fica na aba de
    // objetivo (ObjectivePanel) e a barra do chefe no visor (BossHealthBar). O componente continua existindo, vazio,
    // só porque as cenas já salvas o têm (apagar o script deixaria "Missing Script" nelas); pode ser removido delas.
    public class EnemiesRemainingHUD : MonoBehaviour
    {
    }
}
