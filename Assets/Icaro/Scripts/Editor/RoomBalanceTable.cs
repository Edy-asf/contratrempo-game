using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Tabela de vida e dano por sala": vida e dano de cada inimigo das tabelas de composição (e do chefe) nas salas
    // 1, 5, 10, 15 e 20, com as curvas do CombatBalance. Imprime no Console e grava em Balanceamento/TabelaSalas.md
    // (na raiz do projeto, fora de Assets). O dano mostrado é o da ficha escalado; entre parênteses, o que chega ao
    // jogador depois do limite por acerto (sem itens de vida).
    public static class RoomBalanceTable
    {
        public const string OutputPath = "Balanceamento/TabelaSalas.md";

        static readonly int[] k_Rooms = { 1, 5, 10, 15, 20 };

        [MenuItem("Ícaro/Tabela de vida e dano por sala", priority = 65)]
        public static void Print()
        {
            CombatBalance balance = CombatAssetsBuilder.Balance;
            if (!balance)
            {
                Debug.LogError("[Ícaro] CombatBalance não encontrado.");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine("# Vida e dano dos inimigos por sala");
            text.AppendLine();
            text.AppendLine($"Jogador: {balance.PlayerMaxHealth:0} de vida; um acerto tira no máximo " +
                            $"{balance.MaxHitFraction:P0} ({balance.PlayerMaxHealth * balance.MaxHitFraction:0.#}) ou " +
                            $"{balance.HeavyMaxHitFraction:P0} ({balance.PlayerMaxHealth * balance.HeavyMaxHitFraction:0.#}) " +
                            "no laser do Golem e nos chefes.");
            text.AppendLine();

            text.Append("| Inimigo |");
            foreach (int room in k_Rooms)
                text.Append($" Sala {room} vida | Sala {room} dano |");
            text.AppendLine();
            text.Append("|---|");
            foreach (int _ in k_Rooms)
                text.Append("---:|---:|");
            text.AppendLine();

            // Cada tipo que aparece em alguma tabela de composição, uma vez
            var rows = new List<EnemyDefinition>();
            foreach (CombatBalance.SpawnTable table in balance.SpawnTables ?? new CombatBalance.SpawnTable[0])
            {
                if (table?.Weights == null)
                    continue;
                foreach (CombatBalance.SpawnWeight entry in table.Weights)
                {
                    if (entry?.Enemy && !rows.Contains(entry.Enemy))
                        rows.Add(entry.Enemy);
                }
            }
            foreach (EnemyDefinition enemy in rows)
            {
                if (!enemy)
                    continue;
                AppendRow(text, balance, enemy.name.Replace("Inimigo_", ""), enemy, enemy.Damage, false,
                    enemy is GolemDefinition);
                if (enemy is GolemDefinition golem)
                {
                    AppendRow(text, balance, "  Golem (explosão do laser)", golem, golem.ExplosionDamage, false, false,
                        showHealth: false);
                    AppendRow(text, balance, "  Golem (pancada)", golem, golem.SlamDamage, false, false,
                        showHealth: false);
                }
            }

            // Chefes: cada um só aparece na sala de chefe do bioma dele (6, 11, 16 e 20)
            text.AppendLine();
            text.AppendLine("| Chefe | Sala | Vida | Dano de contato |");
            text.AppendLine("|---|---:|---:|---:|");
            int[] bossRooms = { 6, 11, 16, 20 };
            for (int b = 0; b < bossRooms.Length; b++)
            {
                EnemyDefinition boss = balance.BossFor(b);
                if (!boss)
                    continue;
                int room = bossRooms[b];
                string bossName = string.IsNullOrEmpty(boss.DisplayName) ? boss.name : boss.DisplayName;
                text.AppendLine($"| {bossName} | {room} | {boss.MaxHealth * balance.RoomHealthMultiplier(room, true):0} | " +
                                $"{boss.Damage * balance.RoomDamageMultiplier(room, true):0.#} |");
            }

            text.AppendLine();
            text.AppendLine($"Escala: vida x{balance.RoomHealthMultiplier(20, false):0.00} e dano " +
                            $"x{balance.RoomDamageMultiplier(20, false):0.00} na sala 20. Chefe: vida x" +
                            $"{balance.BossHealthMultiplier:0.##} x {balance.BossRoomHealthBonus:0.##} sobre a sala e " +
                            $"dano x{balance.BossDamageMultiplier:0.##}. Velocidade, cadência e avisos não escalam por sala.");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, text.ToString(), Encoding.UTF8);
            Debug.Log("[Ícaro] Tabela gravada em " + Path.GetFullPath(OutputPath) + "\n" + text);
        }

        // showHealth false = linha só de dano (ataque secundário)
        static void AppendRow(StringBuilder text, CombatBalance balance, string name, EnemyDefinition definition,
            float baseDamage, bool boss, bool heavy, bool showHealth = true)
        {
            float fraction = definition && definition.MaxHitFraction > 0f ? definition.MaxHitFraction
                : heavy || boss ? balance.HeavyMaxHitFraction
                : balance.MaxHitFraction;
            float cap = balance.PlayerMaxHealth * fraction;

            text.Append($"| {name} |");
            foreach (int room in k_Rooms)
            {
                string health = definition && showHealth
                    ? (definition.MaxHealth * balance.RoomHealthMultiplier(room, boss)).ToString("0")
                    : "";
                float damage = baseDamage * balance.RoomDamageMultiplier(room, boss);
                string dealt = damage > cap ? $" ({cap:0.#})" : "";
                text.Append($" {health} | {damage:0.#}{dealt} |");
            }
            text.AppendLine();
        }
    }
}
