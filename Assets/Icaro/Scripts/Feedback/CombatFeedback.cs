using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Feedback de combate a partir do evento central de dano (CombatEvents.OnDamage):
    // acertos do jogador -> hit marker, número de dano, som de acerto/crítico/abate e tremor no abate;
    // dano no jogador -> tremor proporcional ao dano. Põe o CameraShake na câmera do jogador e a opção de
    // desligá-lo no menu de pausa.
    [RequireComponent(typeof(HitMarker), typeof(DamageNumbers), typeof(CombatAudio))]
    public class CombatFeedback : MonoBehaviour
    {
        [Header("Tremor da câmera (trauma de 0 a 1)")]
        public float KillTrauma = 0.22f;

        [Tooltip("Trauma a mais no abate de inimigos com muita vida (Golem), proporcional à vida máxima / 1000")]
        public float BigKillTraumaPer1000Health = 0.2f;

        [Tooltip("Trauma ao levar dano: mínimo + proporcional à fração da vida perdida")]
        public float PlayerHitTraumaMin = 0.12f;
        public float PlayerHitTraumaPerHealthFraction = 1.2f;

        HitMarker m_HitMarker;
        DamageNumbers m_Numbers;
        CombatAudio m_Audio;

        void Awake()
        {
            m_HitMarker = GetComponent<HitMarker>();
            m_Numbers = GetComponent<DamageNumbers>();
            m_Audio = GetComponent<CombatAudio>();
            if (!GetComponent<CameraShakeMenuOption>())
                gameObject.AddComponent<CameraShakeMenuOption>();
        }

        void OnEnable() => CombatEvents.OnDamage += OnDamage;
        void OnDisable() => CombatEvents.OnDamage -= OnDamage;

        void Start()
        {
            var player = FindAnyObjectByType<PlayerCharacterController>();
            if (player && player.PlayerCamera && !player.PlayerCamera.GetComponent<CameraShake>())
                player.PlayerCamera.gameObject.AddComponent<CameraShake>();
        }

        void OnDamage(DamageInfo info)
        {
            if (info.VictimIsPlayer)
            {
                float fraction = info.VictimHealth ? info.Amount / info.VictimHealth.MaxHealth : 0.1f;
                CameraShake.AddTrauma(PlayerHitTraumaMin + fraction * PlayerHitTraumaPerHealthFraction);
                return;
            }

            if (!info.AttackerIsPlayer)
                return;

            if (GameSettings.HitMarker)
                m_HitMarker.Show(info.IsCritical || info.Killed);
            if (GameSettings.DamageNumbers)
                m_Numbers.Spawn(info.Point, info.Amount, info.IsCritical);
            m_Audio.PlayHit(info.IsCritical, info.Killed);

            if (info.Killed)
            {
                float maxHealth = info.VictimHealth ? info.VictimHealth.MaxHealth : 0f;
                CameraShake.AddTrauma(KillTrauma + BigKillTraumaPer1000Health * maxHealth / 1000f);
            }
        }
    }
}
