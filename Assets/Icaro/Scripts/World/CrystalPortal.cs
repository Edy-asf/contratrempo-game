using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Portal de cristal em par: o jogador que entra pela frente de um sai pela frente do outro (Target), mantendo o
    // deslocamento lateral, a altura, a direção do olhar e a velocidade, giradas pela diferença entre os dois portais.
    // Só o jogador atravessa (inimigos e projéteis não). Precisa de um colisor de gatilho no mesmo objeto; a frente do
    // portal é o transform.forward.
    [RequireComponent(typeof(Collider))]
    public class CrystalPortal : MonoBehaviour
    {
        public CrystalPortal Target;

        [Tooltip("Distância à frente do portal de saída onde o jogador aparece (m)")]
        public float ExitDistance = 1.6f;

        [Tooltip("Tempo sem atravessar de novo depois de uma passagem (s)")]
        public float Cooldown = 0.5f;

        static float s_LastTeleport = -10f;

        void OnTriggerEnter(Collider other)
        {
            if (!Target || Time.time - s_LastTeleport < Cooldown)
                return;
            var player = other.GetComponentInParent<PlayerCharacterController>();
            if (!player)
                return;

            Transform body = player.transform;
            // Entra só por quem vem da frente
            if (Vector3.Dot(body.position - transform.position, transform.forward) < -0.2f)
                return;

            s_LastTeleport = Time.time;
            Quaternion turn = Target.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(transform.rotation);
            Vector3 local = transform.InverseTransformPoint(body.position);
            Vector3 exit = Target.transform.TransformPoint(new Vector3(-local.x, local.y, ExitDistance));

            var controller = player.GetComponent<CharacterController>();
            bool enabled = controller && controller.enabled;
            if (controller)
                controller.enabled = false;
            body.SetPositionAndRotation(exit, Quaternion.Euler(0f, (turn * body.rotation).eulerAngles.y, 0f));
            player.CharacterVelocity = turn * player.CharacterVelocity;
            if (controller)
                controller.enabled = enabled;
            Physics.SyncTransforms();
        }
    }
}
