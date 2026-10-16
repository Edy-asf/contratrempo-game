using System.Globalization;
using Unity.FPS.Game;
using UnityEngine;

namespace Icaro
{
    // Ficha de uma arma do Ícaro (Balance/Armas/Arma_*.asset): o que aparece na loja (nome, papel, cor, letra,
    // preço), os números da arma (cadência em RPM, pente, recarga, bagos por disparo, dispersão, coice), aplicados ao
    // WeaponController pelo IcaroWeapon, e os do projétil (dano, velocidade, alcance, assistência de mira), lidos pelo
    // HomingProjectile. O dano é o final de cada bala ou bago (sem multiplicador do jogador; CombatBalance
    // PlayerDamageMultiplier = 1); itens, crítico e melhorias da loja somam por cima. As armas não regeneram munição:
    // o pente esvazia e a recarga leva ReloadTime segundos (IcaroWeapon).
    [CreateAssetMenu(menuName = "Ícaro/Arma", fileName = "Arma")]
    public class WeaponDefinition : ScriptableObject
    {
        static readonly CultureInfo k_Culture = CultureInfo.GetCultureInfo("pt-BR");

        [Header("Loja")]
        public string DisplayName = "Arma";

        [Tooltip("Papel da arma (texto curto da carta da loja)")]
        [TextArea(1, 3)] public string Role;

        [Tooltip("Letra do ícone provisório")]
        public string Letter = "A";

        public Color Color = Color.white;

        [Tooltip("Preço em Cronitas a 1,0x (sobe com o multiplicador, como o resto da loja)")]
        [Min(0)] public int Price = 90;

        [Header("Prefabs (gerados por Ícaro > Gerar armas)")]
        public WeaponController WeaponPrefab;
        public HomingProjectile Projectile;

        [Header("Arma")]
        [Tooltip("Automatic: segurar o gatilho atira na cadência máxima; Manual: um tiro por clique")]
        public WeaponShootType ShootType = WeaponShootType.Automatic;

        [Tooltip("Disparos por minuto (os itens de cadência multiplicam)")]
        [Min(1f)] public float RoundsPerMinute = 600f;

        [Tooltip("Disparos no pente (o Tambor Ampliado aumenta)")]
        [Min(1)] public int MagazineSize = 30;

        [Tooltip("Segundos para recarregar o pente (o Resfriador Avançado encurta)")]
        [Min(0.1f)] public float ReloadTime = 1.5f;

        [Tooltip("Balas por disparo (escopeta: bagos por cartucho; o disparo inteiro gasta 1 do pente)")]
        [Min(1)] public int PelletsPerShot = 1;

        [Tooltip("Dispersão em graus (0 = reto). Com mira assistida teleguiada, a dispersão não vale (o tiro vai no alvo)")]
        [Min(0f)] public float SpreadAngle;

        [Range(0f, 2f)] public float RecoilForce = 0.6f;

        [Tooltip("Duas armas, uma em cada mão: os disparos alternam entre os dois canos")]
        public bool Akimbo;

        [Tooltip("Silenciador: o tiro usa o som abafado (WeaponSounds.Suppressed) e o sprite solta fumaça em vez de clarão")]
        public bool Suppressed;

        [Tooltip("Arte importada (Resources/Armas/<arma>/parada.png): ponta do cano na imagem, em fração da largura e da " +
                 "altura a partir do canto superior esquerdo. O tiro sai daí (WeaponSpriteHUD)")]
        public Vector2 SpriteMuzzle = new Vector2(0.52f, 0.39f);

        [Header("Sprite na tela (ajuste em jogo: F10)")]
        [Tooltip("Altura do sprite em fração da altura da tela. O tamanho não muda com o campo de visão; acima do " +
                 "campo de visão de referência (WeaponSpriteHUD.ReferenceFov) a arma fica esticada como as bordas de uma " +
                 "lente grande-angular")]
        [Range(0.1f, 1.5f)] public float SpriteHeight = 0.6f;

        [Tooltip("Canto inferior direito do sprite em relação ao canto da tela, em fração da altura da tela (x para a " +
                 "direita, y para cima; valores positivos em x e negativos em y escondem a ponta do braço fora da tela)")]
        public Vector2 SpriteOffset = new Vector2(0.04f, -0.04f);

        [Tooltip("Giro do sprite no eixo vertical, em graus (positivo leva o cano para dentro da cena)")]
        [Range(-45f, 45f)] public float SpriteTilt;

        [Tooltip("Desenho em código: o tiro sai de SpriteMuzzle em vez da ponta do cano calculada pelo desenho")]
        public bool OverrideSpriteMuzzle;

        [Header("Projétil")]
        [Tooltip("Dano final de cada bala ou bago (antes de itens, crítico e melhorias)")]
        [Min(0f)] public float Damage = 30f;

        [Min(1f)] public float ProjectileSpeed = 250f;

        [Min(0.01f)] public float ProjectileRadius = 0.06f;

        [Tooltip("Distância máxima do voo em metros (o dano é o mesmo em qualquer distância)")]
        [Min(1f)] public float Range = 120f;

        public WeaponAimMode AimMode = WeaponAimMode.Homing;

        [Tooltip("Peso de cada acerto nas chances dos efeitos de item (1 = normal; escopeta, com muitos bagos, menos)")]
        [Range(0f, 1f)] public float ProcCoefficient = 1f;

        public float DelayBetweenShots => 60f / Mathf.Max(1f, RoundsPerMinute);

        // Dano de um disparo inteiro com todos os bagos acertando
        public float DamagePerShot => Damage * PelletsPerShot;

        // Texto da carta da loja (com os números da ficha)
        public string Describe()
        {
            string damage = PelletsPerShot > 1
                ? $"{Num(Damage)} x {PelletsPerShot} bagos ({Num(DamagePerShot)})"
                : Num(Damage);
            string text = $"Dano {damage} · {Num(RoundsPerMinute)} RPM\nPente {MagazineSize} · recarga {Num(ReloadTime)} s";
            if (AimMode == WeaponAimMode.None)
                text += "\nSem mira assistida";
            return string.IsNullOrEmpty(Role) ? text : Role + "\n" + text;
        }

        // Copia os números da arma para um WeaponController (instância em jogo ou o prefab, no gerador). A munição
        // não regenera sozinha (AutomaticReload desligado): a recarga é do IcaroWeapon.
        public void ApplyTo(WeaponController weapon)
        {
            if (!weapon)
                return;
            weapon.WeaponName = DisplayName;
            weapon.ShootType = ShootType;
            weapon.DelayBetweenShots = DelayBetweenShots;
            weapon.BulletsPerShot = PelletsPerShot;
            weapon.BulletSpreadAngle = SpreadAngle;
            weapon.MaxAmmo = MagazineSize;
            weapon.AutomaticReload = false;
            weapon.HasPhysicalBullets = false;
            weapon.AmmoReloadRate = MagazineSize / ReloadTime;
            weapon.AmmoReloadDelay = 0f;
            weapon.RecoilForce = RecoilForce;
            weapon.AutomaticReleaseOnCharged = false;
            if (Projectile)
                weapon.ProjectilePrefab = Projectile;
        }

        // Número com vírgula decimal (até uma casa)
        static string Num(float value) => value.ToString("0.#", k_Culture);
    }
}
