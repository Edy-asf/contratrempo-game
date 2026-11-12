using UnityEngine;

namespace Icaro
{
    // Gatilho do portal aberto do Arquipélago (montado em jogo pelo PortalActivation): passa a entrada para ele
    public class PortalGate : MonoBehaviour
    {
        public PortalActivation Portal;

        void OnTriggerEnter(Collider other)
        {
            if (Portal)
                Portal.Enter(other);
        }
    }
}
