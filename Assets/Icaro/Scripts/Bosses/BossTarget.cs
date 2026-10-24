using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // O jogador visto pelos ataques dos chefes: posição dos pés, altura do corpo, se está no ar e por onde levar dano.
    // Guarda as referências (sem procurar na cena a cada frame).
    public static class BossTarget
    {
        static PlayerCharacterController s_Player;
        static CharacterController s_Character;
        static Damageable s_Damageable;

        public static PlayerCharacterController Player
        {
            get
            {
                if (!s_Player)
                {
                    s_Player = Object.FindAnyObjectByType<PlayerCharacterController>();
                    s_Character = s_Player ? s_Player.GetComponent<CharacterController>() : null;
                    s_Damageable = s_Player ? s_Player.GetComponentInChildren<Damageable>() : null;
                }

                return s_Player;
            }
        }

        public static bool Exists => Player && !s_Player.IsDead;

        public static Vector3 Feet => Player ? s_Player.transform.position : Vector3.zero;

        // Altura do corpo agora (menor agachado)
        public static float BodyHeight => s_Character ? s_Character.height : 1.8f;

        public static Vector3 Chest => Feet + Vector3.up * (BodyHeight * 0.6f);

        public static bool IsGrounded => Player && s_Player.IsGrounded;

        public static Vector3 Velocity => Player ? s_Player.CharacterVelocity : Vector3.zero;

        // Causa dano no jogador pelo evento central (os limites de justiça do PlayerDefenses valem)
        public static void Damage(float amount, GameObject attacker, Vector3 point, bool continuous = false)
        {
            if (!Exists || !s_Damageable || amount <= 0f)
                return;
            CombatEvents.ApplyDamage(s_Damageable, amount, attacker, point, continuous: continuous);
        }

        // Distância horizontal dos pés do jogador até um ponto
        public static float FlatDistance(Vector3 point)
        {
            Vector3 offset = Feet - point;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
