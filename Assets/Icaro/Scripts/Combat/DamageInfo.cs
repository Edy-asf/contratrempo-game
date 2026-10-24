using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Um acerto: quem causou, quem recebeu, quanto, onde, se foi crítico e se matou. Publicado por CombatEvents.
    public struct DamageInfo
    {
        // Quem causou (jogador, inimigo; pode ser null)
        public GameObject Attacker;

        // Quem recebeu (o objeto com o Health)
        public GameObject Victim;
        public Health VictimHealth;

        // Dano que a vida realmente perdeu (depois de multiplicadores e do limite da vida restante)
        public float Amount;

        // Ponto do impacto, no mundo
        public Vector3 Point;

        public bool IsCritical;
        public bool IsExplosion;

        // Dano de um efeito de item (raio, míssil, explosão): não ativa outros efeitos de item
        public bool IsItem;

        // Peso do acerto nas chances dos efeitos de item (1 = normal; fragmentos de escopeta e ecos valem menos)
        public float ProcCoefficient;

        // O acerto zerou a vida da vítima
        public bool Killed;

        public bool AttackerIsPlayer;
        public bool VictimIsPlayer;
    }
}
