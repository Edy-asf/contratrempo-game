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
            return Pick(balance, room, table.Weights, active, highSpawnSlots);
        }

        // Reforços de um chefe (BossDefinition.Reinforcements): todos com o mesmo peso
        public static EnemyDefinition PickFrom(CombatBalance balance, int room, EnemyDefinition[] enemies,
            Dictionary<EnemyDefinition, int> active, int highSpawnSlots)
        {
            s_List.Clear();
            foreach (EnemyDefinition enemy in enemies)
                if (enemy)
                    s_List.Add(new CombatBalance.SpawnWeight { Enemy = enemy, Weight = 1f });
            return Pick(balance, room, s_List, active, highSpawnSlots, ignoreMinArena: true);
        }

        static readonly List<CombatBalance.SpawnWeight> s_List = new List<CombatBalance.SpawnWeight>();

        static EnemyDefinition Pick(CombatBalance balance, int room, IEnumerable<CombatBalance.SpawnWeight> weights,
            Dictionary<EnemyDefinition, int> active, int highSpawnSlots, bool ignoreMinArena = false)
        {

            s_Candidates.Clear();
            float totalWeight = 0f;
            foreach (CombatBalance.SpawnWeight entry in weights)
            {
                EnemyDefinition enemy = entry?.Enemy;
                if (!enemy || !enemy.Prefab || entry.Weight <= 0f || (!ignoreMinArena && enemy.MinArena > room))
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
