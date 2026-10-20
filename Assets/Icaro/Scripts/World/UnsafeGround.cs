using UnityEngine;

namespace Icaro
{
    // Chão que pode sumir ou se mover (pontes que desabam, plataformas flutuantes): o SeaHazard não guarda ponto
    // seguro em cima dele, para quem cai no mar nunca reaparecer no ar.
    public class UnsafeGround : MonoBehaviour
    {
        public static bool IsUnsafe(Collider collider) =>
            collider && collider.GetComponentInParent<UnsafeGround>();
    }
}
