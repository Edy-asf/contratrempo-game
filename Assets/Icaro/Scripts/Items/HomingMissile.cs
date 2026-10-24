using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Míssil teleguiado dos Mísseis de Sondagem: sai para cima, curva até o alvo e causa o dano ao chegar (dano de
    // item). Se o alvo morrer, procura o inimigo mais próximo; sem alvo, segue reto até acabar o tempo de vida.
    public class HomingMissile : MonoBehaviour
    {
        const float k_Lifetime = 5f;
        const float k_HitDistance = 0.8f;
        const float k_RetargetRange = 40f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        ItemProcs m_Owner;
        Health m_Target;
        float m_Damage;
        float m_Speed = 28f;
        float m_TurnRate = 540f;
        float m_Age;

        public void Launch(ItemProcs owner, Health target, float damage, ItemCatalog catalog)
        {
            m_Owner = owner;
            m_Target = target;
            m_Damage = damage;
            if (catalog)
            {
                m_Speed = catalog.MissileSpeed;
                m_TurnRate = catalog.MissileTurnRate;
            }

            BuildVisual(catalog);
        }

        void BuildVisual(ItemCatalog catalog)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.18f, 0.18f, 0.4f);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!catalog || !catalog.EffectMaterial)
                return;

            renderer.sharedMaterial = catalog.EffectMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(k_BaseColor, new Color(3f, 0.6f, 0.4f, 1f));
            renderer.SetPropertyBlock(block);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = catalog.EffectMaterial;
            trail.time = 0.35f;
            trail.widthMultiplier = 0.12f;
            trail.startColor = new Color(1f, 0.5f, 0.3f, 1f);
            trail.endColor = new Color(1f, 0.3f, 0.1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= k_Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if ((!m_Target || m_Target.CurrentHealth <= 0f) && m_Owner)
                m_Target = m_Owner.NearestEnemy(transform.position, k_RetargetRange, null);

            if (m_Target)
            {
                Vector3 targetPoint = ItemProcs.CenterOf(m_Target);
                Vector3 toTarget = targetPoint - transform.position;
                if (toTarget.magnitude <= Mathf.Max(k_HitDistance, m_Speed * Time.deltaTime))
                {
                    if (m_Owner)
                        m_Owner.Damage(m_Target, m_Damage, targetPoint, explosion: true);
                    Destroy(gameObject);
                    return;
                }

                // Curva fraca no impulso inicial e fechada depois
                float turn = m_TurnRate * Mathf.Clamp01(m_Age / 0.25f) * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget),
                    turn);
            }

            transform.position += transform.forward * (m_Speed * Time.deltaTime);
        }
    }
}
