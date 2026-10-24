using UnityEngine;

namespace Icaro
{
    // Espinhento: torre fixa num ponto alto; atira em qualquer ponto da arena com linha de visão, sem aviso de mira.
    // Damage é o dano de cada espinho e AttackCooldown o intervalo entre rajadas a 1,0x (encurta com o
    // multiplicador até MinShotInterval).
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Espinhento", fileName = "Inimigo_Espinhento")]
    public class EspinhentoDefinition : EnemyDefinition
    {
        [Header("Cadência")]
        [Tooltip("Intervalo mínimo entre rajadas, com qualquer multiplicador")]
        public float MinShotInterval = 1.4f;
    }
}
