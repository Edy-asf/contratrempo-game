using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Projétil do Zangão: pequeno e rápido, reaproveitado pelo PrefabPool (sem Rigidbody nem colisor). Anda em linha
    // reta com a velocidade da ficha vezes o multiplicador de tempo e testa o trecho do frame com um raycast; ao
    // acertar o jogador, causa o dano pelo evento central. Passa através de outros inimigos.
    public class ZangaoProjectile : MonoBehaviour
    {
        GameObject m_Owner;
        Vector3 m_Direction;
        float m_Speed;
        float m_Damage;
        float m_Life;

        public void Launch(GameObject owner, Vector3 direction, float speed, float damage, float lifetime)
        {
            m_Owner = owner;
            m_Direction = direction.normalized;
            m_Speed = speed;
            m_Damage = damage;
            m_Life = lifetime;
            transform.rotation = Quaternion.LookRotation(m_Direction);
        }

        void Update()
        {
            m_Life -= Time.deltaTime;
            if (m_Life <= 0f)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            float step = m_Speed * TimeManager.CurrentMultiplier * Time.deltaTime;
            Vector3 start = transform.position;
            if (Physics.Raycast(start, m_Direction, out RaycastHit hit, step, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && !IsEnemy(hit.collider))
            {
                if (hit.collider.GetComponentInParent<PlayerCharacterController>())
                {
                    var damageable = hit.collider.GetComponentInParent<Damageable>();
                    if (damageable)
                        CombatEvents.ApplyDamage(damageable, m_Damage, m_Owner, hit.point);
                }

                PrefabPool.Release(gameObject);
                return;
            }

            transform.position = start + m_Direction * step;
        }

        static bool IsEnemy(Collider collider) => collider.GetComponentInParent<EnemyStats>();
    }
}
