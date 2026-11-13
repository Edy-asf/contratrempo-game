using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Icaro.EditorTools
{
    // "Ícaro > Tabela de vivos pelo tamanho da sala": mede o chão andável (NavMesh dentro do domo, ArenaFloor) de cada
    // sala da cena aberta e mostra o limite de vivos ao mesmo tempo com e sem o tamanho, e a cota. Imprime no Console
    // e grava em Balanceamento/TabelaVivos.md (na raiz do projeto). Precisa do NavMesh da cena carregado.
    public static class RoomSizeTable
    {
        const string k_Output = "Balanceamento/TabelaVivos.md";

        [MenuItem("Ícaro/Tabela de vivos pelo tamanho da sala", priority = 66)]
        public static void Print()
        {
            CombatBalance balance = CombatAssetsBuilder.Balance;
            if (!balance)
            {
                Debug.LogError("[Ícaro] CombatBalance não encontrado.");
                return;
            }

            ArenaFloor.Invalidate();
            RoomManager[] rooms = Object.FindObjectsByType<RoomManager>(FindObjectsInactive.Include)
                .OrderBy(r => r.ArenaNumber).ToArray();
            if (rooms.Length == 0)
            {
                Debug.LogWarning("[Ícaro] Nenhuma sala (RoomManager) na cena aberta.");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine("# Vivos ao mesmo tempo pelo tamanho da sala");
            text.AppendLine();
            text.AppendLine($"Área de referência {balance.ReferenceArea:0} m², expoente {balance.AreaExponent:0.##}, " +
                            $"fator entre {balance.AreaFactorRange.x:0.##} e {balance.AreaFactorRange.y:0.##}, " +
                            $"mínimo {balance.AliveMin}, teto {balance.AliveCeiling}" +
                            (balance.AliveByRoomSize ? "." : " (desligado: vale só a fórmula por sala)."));
            text.AppendLine();
            text.AppendLine("| Sala | Nome | Chão (m²) | Fator | Vivos sem tamanho | Vivos | Cota |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            foreach (RoomManager room in rooms)
            {
                bool boss = room.IsBoss;
                float area = ArenaFloor.AreaOf(room);
                text.AppendLine($"| {room.ArenaNumber} | {room.RoomName}{(boss ? " (chefe)" : "")} | {area:0} | " +
                                $"{(boss ? 1f : balance.AreaFactor(area)):0.00} | " +
                                $"{balance.AliveLimitFor(room.ArenaNumber, boss)} | " +
                                $"{balance.AliveLimitFor(room.ArenaNumber, boss, area)} | " +
                                $"{balance.QuotaFor(room.ArenaNumber, boss)} |");
            }

            if (rooms.All(r => ArenaFloor.AreaOf(r) <= 0f))
                text.AppendLine().AppendLine("Nenhum NavMesh encontrado: abra a cena com o NavMesh gerado.");

            Directory.CreateDirectory(Path.GetDirectoryName(k_Output));
            File.WriteAllText(k_Output, text.ToString());
            Debug.Log("[Ícaro] " + text);
        }
    }
}
