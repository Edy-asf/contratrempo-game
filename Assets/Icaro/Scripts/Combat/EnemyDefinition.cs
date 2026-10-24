using UnityEngine;

namespace Icaro
{
    // Ficha de um tipo de inimigo: regras de spawn na sala e os números de combate a 1,0x na sala 1. A chance de
    // aparecer vem das tabelas de pesos do CombatBalance (SpawnTables).
    // Vida e dano são multiplicados pela escala por sala (CombatBalance); as IAs leem os valores daqui pelo EnemyStats.
    [CreateAssetMenu(menuName = "Ícaro/Inimigo", fileName = "Inimigo_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Spawn")]
        public GameObject Prefab;

        [Tooltip("Nome mostrado no HUD (chefes); vazio = nome da ficha")]
        public string DisplayName;

        [Tooltip("Primeira arena (1 a 20, na ordem da Via) em que pode aparecer")]
        [Min(1)] public int MinArena = 1;

        [Tooltip("Máximo deste tipo vivo ao mesmo tempo na sala (0 = sem limite)")]
        [Min(0)] public int MaxAlive;

        [Tooltip("Só nasce em pontos altos (HighSpawnPoint) válidos; sem ponto livre, não é sorteado")]
        public bool RequiresHighSpawn;

        [Tooltip("Surge em grupo: de GroupMin a GroupMax do mesmo ponto de spawn, cada um contando 1 na cota")]
        [Min(1)] public int GroupMin = 1;

        [Min(1)] public int GroupMax = 1;

        [Tooltip("Reaproveita instâncias (PrefabPool) em vez de criar e destruir; a IA do tipo precisa suportar")]
        public bool Pooled;

        [Header("Combate (a 1,0x, antes da escala por sala)")]
        [Min(1f)] public float MaxHealth = 50f;

        [Tooltip("Dano do ataque principal (contato, projétil, investida ou laser)")]
        [Min(0f)] public float Damage = 10f;

        [Tooltip("Segundos entre ataques do ataque principal")]
        [Min(0f)] public float AttackCooldown = 1f;

        [Tooltip("Velocidade do projétil a 1,0x (0 = a do prefab do projétil)")]
        [Min(0f)] public float ProjectileSpeed;

        [Header("Multiplicador de tempo")]
        [Tooltip("Teto do efeito do multiplicador de tempo sobre o movimento (velocidade e aceleração). 0 = igual ao " +
                 "teto geral do TimeManager. Cadência, avisos e animação seguem o multiplicador inteiro.")]
        [Min(0f)] public float MovementMultiplierCap;

        [Tooltip("Fração máxima da vida do jogador que um acerto deste inimigo pode tirar (0 = a padrão do " +
                 "CombatBalance, 20% ou 25% em chefes; 1 = sem limite)")]
        [Range(0f, 1f)] public float MaxHitFraction;


        [Header("Fragmento de metrônomo")]
        [Tooltip("Chance (0 a 1) de deixar cair um fragmento de metrônomo ao morrer")]
        [Range(0f, 1f)] public float FragmentDropChance;

        [Header("Cronitas")]
        [Tooltip("Cronitas (moeda da run) que este inimigo solta ao morrer, antes dos bônus de sala e de elite")]
        [Min(0)] public int CronitaValue = 3;
    }
}
