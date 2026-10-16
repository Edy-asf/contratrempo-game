using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Arma do Ícaro ligada à sua ficha (WeaponDefinition). No Awake, antes do WeaponController encher o pente, copia
    // os números da ficha para a instância (o ScriptableObject é a fonte dos valores). Também cuida do pente: a
    // munição não regenera; com o pente vazio, ou apertando recarregar (tecla do Microgame, "Player/Reload"), a arma
    // fica ReloadTime segundos sem atirar (encurtado pelo Resfriador Avançado, PlayerStats.ReloadTimeMultiplier) e
    // volta cheia. Trocar de arma no meio cancela a recarga (ela recomeça ao voltar, se o pente estiver vazio). Nas
    // armas Akimbo, os disparos alternam entre o cano principal e o SecondMuzzle.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(WeaponController))]
    public class IcaroWeapon : MonoBehaviour
    {
        public WeaponDefinition Definition;

        [Tooltip("Cano da segunda arma (só Akimbo)")]
        public Transform SecondMuzzle;

        public WeaponController Weapon { get; private set; }

        // Cano da arma da mão direita (no akimbo, o WeaponMuzzle alterna entre este e o SecondMuzzle)
        public Transform FirstMuzzle => m_FirstMuzzle ? m_FirstMuzzle : Weapon ? Weapon.WeaponMuzzle : null;

        public bool IsReloading { get; private set; }

        // 0 a 1 durante a recarga
        public float ReloadProgress => IsReloading && m_ReloadDuration > 0f
            ? Mathf.Clamp01(1f - (m_ReloadEnd - Time.time) / m_ReloadDuration)
            : 0f;

        PlayerInputHandler m_Input;
        Transform m_FirstMuzzle;
        float m_ReloadEnd;
        float m_ReloadDuration;
        bool m_Refilling;

        void Awake()
        {
            Weapon = GetComponent<WeaponController>();
            if (Definition)
                Definition.ApplyTo(Weapon);
            m_FirstMuzzle = Weapon.WeaponMuzzle;
            if (Definition && Definition.Akimbo && SecondMuzzle)
                Weapon.OnShootProcessed += AlternateMuzzle;
            if (Definition && Definition.Suppressed)
                Weapon.ShootSfx = WeaponSounds.Suppressed;
        }

        void OnDestroy()
        {
            if (Weapon)
                Weapon.OnShootProcessed -= AlternateMuzzle;
        }

        // Akimbo: o último disparo saiu da segunda arma (a da mão esquerda)?
        public bool LastShotSecond { get; private set; }

        void AlternateMuzzle()
        {
            LastShotSecond = Weapon.WeaponMuzzle == SecondMuzzle;
            Weapon.WeaponMuzzle = LastShotSecond ? m_FirstMuzzle : SecondMuzzle;
        }

        void Update()
        {
            // Só a arma na mão do jogador (o prefab na loja ou sem dono não recarrega)
            if (!Definition || !Weapon.Owner)
                return;
            if (!m_Input)
                m_Input = Weapon.Owner.GetComponent<PlayerInputHandler>();

            if (IsReloading)
            {
                if (!Weapon.IsWeaponActive)
                    IsReloading = false;
                else if (Time.time >= m_ReloadEnd)
                    Refill();
                return;
            }

            if (!Weapon.IsWeaponActive || Time.timeScale <= 0f)
                return;
            bool empty = Weapon.GetCurrentAmmo() < 1;
            bool pressed = m_Input && m_Input.GetReloadButtonDown() && Weapon.GetCurrentAmmo() < Weapon.MaxAmmo;
            if (empty || pressed)
                StartReload();
        }

        // Esvazia o pente (a arma não atira durante a recarga) e marca o fim
        void StartReload()
        {
            PlayerStats stats = PlayerStats.Instance;
            m_ReloadDuration = Definition.ReloadTime * (stats ? stats.ReloadTimeMultiplier : 1f);
            m_ReloadEnd = Time.time + m_ReloadDuration;
            IsReloading = true;
            Weapon.UseAmmo(Weapon.MaxAmmo);
        }

        // O WeaponController não deixa pôr munição direto: liga a recarga automática dele com taxa enorme por um
        // frame (ele roda depois deste componente) e desliga no LateUpdate
        void Refill()
        {
            IsReloading = false;
            Weapon.AutomaticReload = true;
            Weapon.AmmoReloadDelay = -1f;
            Weapon.AmmoReloadRate = 1e6f;
            m_Refilling = true;
        }

        void LateUpdate()
        {
            if (!m_Refilling)
                return;
            m_Refilling = false;
            Weapon.AutomaticReload = false;
            Weapon.AmmoReloadDelay = 0f;
            Weapon.AmmoReloadRate = Weapon.MaxAmmo / Mathf.Max(0.1f, Definition.ReloadTime);
        }
    }
}
