using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Escolhe o próximo inimigo de uma sala: sorteia pelos pesos da faixa de salas (CombatBalance.SpawnTables) entre
    // os tipos liberados na sala (MinArena), que ainda não atingiram o próprio limite de vivos (MaxAlive) e, para
    // quem só nasce em ponto alto (Espinhento), que ainda têm ponto alto livre.
    public static class SpawnDirector
    {
        static readonly List<CombatBalance.SpawnWeight> s_Candidates = new List<CombatBalance.SpawnWeight>();

        // active: quantos de cada tipo estão vivos ou surgindo. highSpawnSlots: pontos altos livres agora (quem
        // está surgindo num ponto alto já o reservou).
        // Devolve null se nenhum tipo puder surgir agora (todos no limite).
        public static EnemyDefinition Pick(CombatBalance balance, int room, Dictionary<EnemyDefinition, int> active,
            int highSpawnSlots)
        {
            CombatBalance.SpawnTable table = balance.TableFor(room);
            if (table == null || table.Weights == null)
                return null;

            s_Candidates.Clear();
            float totalWeight = 0f;
            foreach (CombatBalance.SpawnWeight entry in table.Weights)
            {
                EnemyDefinition enemy = entry?.Enemy;
                if (!enemy || !enemy.Prefab || entry.Weight <= 0f || enemy.MinArena > room)
                    continue;
                active.TryGetValue(enemy, out int count);
                int cap = balance.TypeAliveCap(enemy);
                if (cap > 0 && count >= cap)
                    continue;
                if (enemy.RequiresHighSpawn && highSpawnSlots <= 0)
                    continue;

                s_Candidates.Add(entry);
                totalWeight += entry.Weight;
            }

            if (s_Candidates.Count == 0)
                return null;

            float roll = Random.value * totalWeight;
            foreach (CombatBalance.SpawnWeight entry in s_Candidates)
            {
                roll -= entry.Weight;
                if (roll <= 0f)
                    return entry.Enemy;
            }

            return s_Candidates[s_Candidates.Count - 1].Enemy;
        }
    }
}
