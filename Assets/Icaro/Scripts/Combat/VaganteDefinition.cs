using UnityEngine;

namespace Icaro
{
    // Vagante: humanoide que anda a esmo pela arena e atira em rajadas. Damage é o dano de cada projétil;
    // ProjectileSpeed (da ficha base) a velocidade do projétil a 1,0x.
    [CreateAssetMenu(menuName = "Ícaro/Inimigo Vagante", fileName = "Inimigo_Vagante")]
    public class VaganteDefinition : EnemyDefinition
    {
        [Header("Movimento")]
        [Tooltip("Velocidade de caminhada a 1,0x (m/s)")]
        public float MoveSpeed = 3.5f;

        [Tooltip("Distância do próximo ponto aleatório, a partir de onde está")]
        public float WanderMinDistance = 8f;
        public float WanderMaxDistance = 15f;

        [Tooltip("Pausa ao chegar em cada ponto, a 1,0x")]
        public float PauseMin = 1f;
        public float PauseMax = 2f;

        [Header("Rajada")]
        [Tooltip("Alcance máximo (com linha de visão)")]
        public float AttackRange = 35f;

        [Tooltip("Brilho de aviso no cano antes da rajada, a 1,0x; nunca abaixo de 60% com o multiplicador")]
        public float MuzzleWarning = 0.3f;

        public int ShotsPerBurst = 3;

        [Tooltip("Intervalo entre os tiros de uma rajada, a 1,0x")]
        public float TimeBetweenShots = 0.15f;

        [Tooltip("Dispersão de cada tiro, em graus (sorteada entre o mínimo e o máximo)")]
        public float SpreadMin = 3f;
        public float SpreadMax = 5f;

        [Tooltip("Intervalo entre rajadas, a 1,0x (sorteado entre o mínimo e o máximo)")]
        public float BurstIntervalMin = 2f;
        public float BurstIntervalMax = 3f;
    }
}
