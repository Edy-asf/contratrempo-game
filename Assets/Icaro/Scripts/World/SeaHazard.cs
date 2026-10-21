using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Mar em volta das ilhas (Arquipélago): quem cai fundo demais na água sai do jogo daquele jeito. O jogador volta
    // para o último ponto seguro (chão firme, longe da borda) e perde uma parte da vida; inimigos que caem morrem.
    // O ponto seguro é guardado algumas vezes por segundo enquanto o jogador está no chão e há chão firme em volta dele
    // (assim ele nunca reaparece na beirada de onde caiu).
    public class SeaHazard : MonoBehaviour
    {
        [Tooltip("Altura da superfície do mar")]
        public float SeaLevel;

        [Tooltip("Abaixo desta distância da superfície, conta como caído no mar")]
        public float FallDepth = 1.5f;

        [Tooltip("Fração da vida máxima perdida ao cair (0,15 = 15%)")]
        [Range(0f, 1f)] public float DamageFraction = 0.15f;

        [Tooltip("Quanto de chão firme precisa haver em volta para o ponto valer como seguro, em metros")]
        public float SafeMargin = 1.2f;

        PlayerCharacterController m_Player;
        CharacterController m_Controller;
        Health m_Health;
        Vector3 m_SafePoint;
        bool m_HasSafePoint;
        float m_NextCheck;

        void Start()
        {
            m_Player = FindAnyObjectByType<PlayerCharacterController>();
            if (!m_Player)
                return;
            m_Controller = m_Player.GetComponent<CharacterController>();
            m_Health = m_Player.GetComponent<Health>();
            m_SafePoint = m_Player.transform.position;
            m_HasSafePoint = true;
        }

        void Update()
        {
            if (m_Player)
                UpdatePlayer();
            UpdateEnemies();
        }

        void UpdatePlayer()
        {
            // No modo livre (voo para prints) o mar não puxa de volta
            if (PlayerFreeMode.Instance && PlayerFreeMode.Instance.Active)
                return;
            Vector3 position = m_Player.transform.position;
            if (position.y < SeaLevel - FallDepth)
            {
                Respawn();
                return;
            }

            if (Time.time < m_NextCheck || !m_Player.IsGrounded || position.y < SeaLevel + 0.3f)
                return;
            m_NextCheck = Time.time + 0.25f;
            if (SolidAround(position))
            {
                m_SafePoint = position;
                m_HasSafePoint = true;
            }
        }

        // Há chão firme logo abaixo nos quatro lados (o ponto não está na beirada nem numa ponte que pode cair)
        bool SolidAround(Vector3 position)
        {
            Vector3[] offsets = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (Vector3 offset in offsets)
            {
                Vector3 from = position + offset * SafeMargin + Vector3.up * 1f;
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore) ||
                    UnsafeGround.IsUnsafe(hit.collider))
                    return false;
            }

            return true;
        }

        void Respawn()
        {
            if (!m_HasSafePoint)
                return;

            // O CharacterController sobrescreve a posição se estiver ligado
            if (m_Controller)
                m_Controller.enabled = false;
            m_Player.transform.position = m_SafePoint + Vector3.up * 0.2f;
            m_Player.CharacterVelocity = Vector3.zero;
            if (m_Controller)
                m_Controller.enabled = true;

            if (m_Health && m_Health.CurrentHealth > 0f)
                m_Health.TakeDamage(m_Health.MaxHealth * DamageFraction, gameObject);

            DisplayMessageEvent evt = Events.DisplayMessageEvent;
            evt.Message = "Caiu no mar";
            evt.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(evt);
        }

        // Inimigos empurrados para fora das ilhas (explosões) morrem no mar
        void UpdateEnemies()
        {
            RoomManager room = RoomManager.Active;
            if (!room)
                return;
            var alive = room.AliveEnemies;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                GameObject enemy = alive[i];
                if (!enemy || enemy.transform.position.y >= SeaLevel - FallDepth)
                    continue;
                var health = enemy.GetComponent<Health>();
                if (health && health.CurrentHealth > 0f)
                    health.Kill();
            }
        }
    }
}
