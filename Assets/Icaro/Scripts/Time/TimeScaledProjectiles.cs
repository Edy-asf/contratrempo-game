using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // ProjectileStandard calcula a velocidade a partir de Speed no momento do disparo e a guarda num campo privado.
    // Para escalar a velocidade sem editar o Microgame nem o prefab original, cada prefab de projétil ganha um
    // "template" em runtime: um clone guardado sob um objeto inativo (então nunca roda OnEnable) cujo Speed
    // é mantido em base * multiplicador. As armas dos inimigos passam a instanciar o template.
    // Cada valor de dano (ficha do inimigo x bioma) tem seu próprio template, com Damage já ajustado.
    public static class TimeScaledProjectiles
    {
        class Entry
        {
            public ProjectileBase Template;
            public ProjectileStandard Standard;
            public float BaseSpeed;
        }

        static readonly Dictionary<(ProjectileBase, int, int), Entry> s_Entries =
            new Dictionary<(ProjectileBase, int, int), Entry>();
        static Transform s_Root;
        static float s_AppliedMultiplier = -1f;

        // Devolve o template correspondente ao prefab (ou o próprio prefab, se não for um ProjectileStandard).
        // damage < 0 mantém o dano do prefab; baseSpeed <= 0 mantém a velocidade do prefab (a 1,0x).
        public static ProjectileBase GetTemplate(ProjectileBase prefab, float damage = -1f, float baseSpeed = 0f)
        {
            if (!prefab)
                return prefab;

            var key = (prefab, damage < 0f ? -1 : Mathf.RoundToInt(damage * 100f),
                baseSpeed <= 0f ? -1 : Mathf.RoundToInt(baseSpeed * 100f));
            if (s_Entries.TryGetValue(key, out Entry entry) && entry.Template)
                return entry.Template;

            var standard = prefab.GetComponent<ProjectileStandard>();
            if (!standard)
                return prefab;

            EnsureRoot();
            ProjectileBase template = Object.Instantiate(prefab, s_Root);
            template.name = prefab.name + " (TimeScaled)";

            entry = new Entry
            {
                Template = template,
                Standard = template.GetComponent<ProjectileStandard>(),
                BaseSpeed = baseSpeed > 0f ? baseSpeed : standard.Speed,
            };
            s_Entries[key] = entry;

            if (damage >= 0f)
                entry.Standard.Damage = damage;
            entry.Standard.Speed = entry.BaseSpeed * TimeManager.CurrentMultiplier;
            return template;
        }

        // Chamado pelos EnemyTimeScaler; só faz trabalho quando o multiplicador muda
        public static void Apply(float multiplier)
        {
            if (Mathf.Approximately(multiplier, s_AppliedMultiplier))
                return;

            s_AppliedMultiplier = multiplier;
            foreach (Entry entry in s_Entries.Values)
            {
                if (entry.Standard)
                    entry.Standard.Speed = entry.BaseSpeed * multiplier;
            }
        }

        static void EnsureRoot()
        {
            if (s_Root)
                return;

            // Templates de uma cena anterior foram destruídos junto com ela
            s_Entries.Clear();
            s_AppliedMultiplier = -1f;

            var root = new GameObject("[Icaro] Projectile Templates");
            root.SetActive(false);
            s_Root = root.transform;
        }
    }
}
