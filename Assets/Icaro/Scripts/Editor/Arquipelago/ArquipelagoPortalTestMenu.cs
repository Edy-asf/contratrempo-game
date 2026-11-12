using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Arquipélago > Abrir portal (teste)": em Play, abre o portal da Ponte Colossal sem derrotar o chefe
    public static class ArquipelagoPortalTestMenu
    {
        [MenuItem("Ícaro/Arquipélago/Abrir portal (teste)", priority = 40)]
        static void Open()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Ícaro] Abrir portal: entre em Play primeiro (o portal fica no fim da Ponte Colossal; F11 voa).");
                return;
            }

            PortalActivation.OpenAll();
            Debug.Log("[Ícaro] Portal do Arquipélago aberto: atravesse o disco de luz no alto do templo.");
        }
    }
}
