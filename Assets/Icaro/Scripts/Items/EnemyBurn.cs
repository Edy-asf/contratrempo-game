using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Queimadura do Cartucho Incendiário num inimigo: cada queimadura causa o seu dano total aos poucos durante
    // ItemCatalog.BurnDuration (a cada BurnTickInterval), como dano de item. Até MaxBurnStacks ao mesmo tempo; uma nova
    // além disso substitui a mais antiga. Enquanto queima, uma luz laranja tremula no inimigo e o Acelerador Térmico
    // aumenta o dano que ele recebe (CombatEvents.ApplyDamage lê IsBurning). Adicionado no primeiro acerto; com o
    // PrefabPool, desligar a instância apaga o fogo.
    public class EnemyBurn : MonoBehaviour
    {
        class Stack
        {
            public float DamagePerSecond;
            public float Remaining;
        }

        readonly List<Stack> m_Stacks = new List<Stack>();
        Health m_Health;
        Damageable m_Damageable;
        GameObject m_Attacker;
        Light m_Light;
        float m_NextTick;

        public bool IsBurning => m_Stacks.Count > 0;

        // Inimigo em chamas (para o Acelerador Térmico)
        public static bool Burning(Health health)
        {
            var burn = health ? health.GetComponent<EnemyBurn>() : null;
            return burn && burn.IsBurning;
        }

        // Incendeia 'target': 'totalDamage' ao longo da duração da queimadura
        public static void Apply(Health target, float totalDamage, GameObject attacker, ItemCatalog catalog)
        {
            if (!target || target.CurrentHealth <= 0f || totalDamage <= 0f || !catalog)
                return;

            var burn = target.GetComponent<EnemyBurn>();
            if (!burn)
                burn = target.gameObject.AddComponent<EnemyBurn>();
            burn.Add(totalDamage, attacker, catalog);
        }

        ItemCatalog Catalog => PlayerStats.Instance ? PlayerStats.Instance.Catalog : null;

        void Add(float totalDamage, GameObject attacker, ItemCatalog catalog)
        {
            m_Health = GetComponent<Health>();
            m_Damageable = GetComponentInChildren<Damageable>();
            m_Attacker = attacker;

            if (m_Stacks.Count >= catalog.MaxBurnStacks)
                m_Stacks.RemoveAt(0);
            float duration = Mathf.Max(0.1f, catalog.BurnDuration);
            if (!IsBurning)
                m_NextTick = catalog.BurnTickInterval;
            m_Stacks.Add(new Stack { DamagePerSecond = totalDamage / duration, Remaining = duration });
            ShowLight(catalog);
        }

        void Update()
        {
            if (!IsBurning)
                return;

            ItemCatalog catalog = Catalog;
            float interval = catalog ? Mathf.Max(0.05f, catalog.BurnTickInterval) : 0.5f;
            float dt = Time.deltaTime;
            m_NextTick -= dt;
            bool tick = m_NextTick <= 0f;
            if (tick)
                m_NextTick += interval;

            // Soma o dano de cada queimadura neste intervalo (a última fatia é a que sobrou)
            float damage = 0f;
            for (int i = m_Stacks.Count - 1; i >= 0; i--)
            {
                Stack stack = m_Stacks[i];
                if (tick)
                    damage += stack.DamagePerSecond * Mathf.Min(interval, stack.Remaining);
                stack.Remaining -= dt;
                if (stack.Remaining <= 0f)
                    m_Stacks.RemoveAt(i);
            }

            if (damage > 0f && m_Damageable && m_Health && m_Health.CurrentHealth > 0f)
                CombatEvents.ApplyDamage(m_Damageable, damage, m_Attacker, ItemProcs.CenterOf(m_Health), item: true);

            if (m_Light)
            {
                m_Light.enabled = IsBurning;
                m_Light.intensity = (1.5f + 0.4f * m_Stacks.Count) * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0f));
            }
        }

        void OnDisable()
        {
            m_Stacks.Clear();
            if (m_Light)
                m_Light.enabled = false;
        }

        void ShowLight(ItemCatalog catalog)
        {
            if (!m_Light)
            {
                var go = new GameObject("FogoCartucho");
                go.transform.SetParent(transform, false);
                m_Light = go.AddComponent<Light>();
                m_Light.type = LightType.Point;
                m_Light.range = 3.5f;
                m_Light.shadows = LightShadows.None;
            }

            m_Light.transform.position = m_Health ? ItemProcs.CenterOf(m_Health) : transform.position + Vector3.up;
            m_Light.color = catalog.BurnColor;
            m_Light.enabled = true;
        }
    }
}
