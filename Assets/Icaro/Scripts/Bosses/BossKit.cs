using UnityEngine;

namespace Icaro
{
    // Prefabs dos ataques dos chefes (onda de choque, projétil, marca no chão, poça e feixe), compartilhados por todos
    // os chefes, e os limites de justiça desses ataques. Gerado por "Ícaro > Gerar prefabs" (BossPrefabBuilder).
    [CreateAssetMenu(menuName = "Ícaro/Kit dos chefes", fileName = "BossKit")]
    public class BossKit : ScriptableObject
    {
        public Shockwave ShockwavePrefab;
        public BossProjectile ProjectilePrefab;
        public GroundTelegraph TelegraphPrefab;
        public AcidPuddle PuddlePrefab;
        public SweepBeam BeamPrefab;

        [Header("Justiça")]
        [Tooltip("Teto do efeito do multiplicador de tempo na velocidade dos ataques dos chefes (projéteis, ondas e " +
                 "feixes). Acima disso só a cadência acelera, para os padrões continuarem desviáveis")]
        [Min(1f)] public float MaxSpeedMultiplier = 2.5f;

        // Multiplicador de velocidade dos ataques agora (o do tempo, com o teto acima)
        public float SpeedMultiplier => Mathf.Min(TimeManager.CurrentMultiplier, MaxSpeedMultiplier);
    }
}
