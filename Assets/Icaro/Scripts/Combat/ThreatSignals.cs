using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Ameaças em andamento, para o HUD (ThreatIndicatorHUD) indicar inimigos fora da tela que estão prestes a atacar
    // ou atacando: as IAs chamam Report no início do aviso de ataque (e no disparo, quem não tem aviso). Cada
    // ameaça dura alguns segundos; reportar de novo a mesma fonte renova. Sem alocação no uso normal.
    public static class ThreatSignals
    {
        public struct Threat
        {
            public Transform Source;
            public float Until;
            public float Duration;
        }

        static readonly List<Threat> s_Threats = new List<Threat>(32);

        public static IReadOnlyList<Threat> Active => s_Threats;

        // 'source' vai atacar o jogador nos próximos 'seconds' (aviso) ou acabou de atirar
        public static void Report(Transform source, float seconds)
        {
            if (!source)
                return;

            float until = Time.time + Mathf.Max(0.2f, seconds);
            for (int i = 0; i < s_Threats.Count; i++)
            {
                if (s_Threats[i].Source != source)
                    continue;
                Threat threat = s_Threats[i];
                threat.Until = Mathf.Max(threat.Until, until);
                threat.Duration = Mathf.Max(0.2f, seconds);
                s_Threats[i] = threat;
                return;
            }

            s_Threats.Add(new Threat { Source = source, Until = until, Duration = Mathf.Max(0.2f, seconds) });
        }

        // Remove as que acabaram ou cuja fonte sumiu (chamado pelo HUD uma vez por frame)
        public static void Prune()
        {
            float now = Time.time;
            for (int i = s_Threats.Count - 1; i >= 0; i--)
            {
                Threat threat = s_Threats[i];
                if (!threat.Source || !threat.Source.gameObject.activeInHierarchy || now > threat.Until)
                    s_Threats.RemoveAt(i);
            }
        }
    }
}
