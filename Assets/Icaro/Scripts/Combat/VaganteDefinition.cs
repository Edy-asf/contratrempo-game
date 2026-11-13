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

        [Tooltip("Segundos que cada tiro de plasma vive")]
        public float ProjectileLifetime = 2.5f;

        [Header("Recuo")]
        [Tooltip("Depois de cada rajada, se o jogador estiver mais perto que isso, ele recua mancando")]
        public float KiteTrigger = 8f;

        [Tooltip("Quanto ele recua (m), para longe do jogador")]
        public float KiteDistance = 6f;

        [Tooltip("Velocidade do recuo em relação à caminhada (mancando)")]
        public float KiteSpeed = 0.8f;

        [Header("Morteiro de esporos")]
        [Tooltip("Segundos sem ver o jogador (atrás de cobertura) até lançar o morteiro")]
        public float MortarHiddenTime = 3f;

        [Tooltip("Alcance máximo do morteiro (m)")]
        public float MortarRange = 40f;

        [Tooltip("Aviso antes do lançamento (o cogumelo do visor acende), a 1,0x; nunca abaixo de 60%")]
        public float MortarWarning = 0.6f;

        [Tooltip("Tempo de voo da bola, a 1,0x: a marca no chão avisa durante ele; nunca abaixo de 60%")]
        public float MortarFlightTime = 1.2f;

        [Tooltip("Raio da explosão (m)")]
        public float MortarRadius = 3f;

        [Tooltip("Dano da explosão em múltiplos do dano do tiro")]
        public float MortarDamage = 2.5f;

        [Tooltip("Recarga do morteiro, a 1,0x")]
        public float MortarCooldown = 7f;
    }
}
