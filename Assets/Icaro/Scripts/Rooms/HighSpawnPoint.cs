using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Ponto de spawn exclusivo do Espinhento: num lugar alto e bem visível (topo de um poleiro, rocha alta, ruína),
    // a pelo menos MinHeightAboveFloor do chão da arena. Só é usado se, da altura do cano, houver linha de visão
    // livre até o centro e até a entrada da arena (validado na geração da Via e de novo na hora do spawn).
    // Gizmos: verde = válido, vermelho = inválido (com as linhas de visão testadas).
    public class HighSpawnPoint : MonoBehaviour
    {
        [Tooltip("Altura do cano do Espinhento acima do ponto")]
        public float MuzzleHeight = 2f;

        [Tooltip("Altura mínima do ponto acima do chão da arena")]
        public float MinHeightAboveFloor = 4f;

        [Tooltip("Altura (y) do chão da arena")]
        public float ArenaFloorHeight;

        [Tooltip("Centro e entrada da arena (no chão); a linha de visão é testada até 1,2 m acima deles")]
        public Vector3 ArenaCenter;
        public Vector3 ArenaEntrance;

        const float k_TargetHeight = 1.2f;

        public Vector3 Muzzle => transform.position + Vector3.up * MuzzleHeight;

        // Resultado da última validação
        public bool IsValid { get; private set; }

        // Inimigo que está neste ponto agora (um por vez)
        public GameObject Occupant { get; set; }

        public bool IsFree => !Occupant;

        public bool Validate()
        {
            IsValid = transform.position.y - ArenaFloorHeight >= MinHeightAboveFloor &&
                      HasLineOfSight(ArenaCenter) && HasLineOfSight(ArenaEntrance);
            return IsValid;
        }

        // Linha livre do cano até o alvo; personagens (inimigos, jogador) não contam como obstáculo
        public bool HasLineOfSight(Vector3 groundTarget) => !FirstBlocker(groundTarget);

        // Primeiro obstáculo entre o cano e o alvo (null = linha livre)
        public Collider FirstBlocker(Vector3 groundTarget)
        {
            Vector3 from = Muzzle;
            Vector3 to = groundTarget + Vector3.up * k_TargetHeight;
            Vector3 direction = to - from;
            RaycastHit[] hits = Physics.RaycastAll(from, direction.normalized, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            Collider closest = null;
            float closestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Actor>() || hit.collider.GetComponent<IgnoreHitDetection>())
                    continue;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closest = hit.collider;
                }
            }

            return closest;
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                Validate();

            Color color = IsValid ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.15f);
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.3f, 0.6f);
            Gizmos.DrawLine(transform.position, Muzzle);

            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawLine(Muzzle, ArenaCenter + Vector3.up * k_TargetHeight);
            Gizmos.DrawLine(Muzzle, ArenaEntrance + Vector3.up * k_TargetHeight);
        }
    }
}
