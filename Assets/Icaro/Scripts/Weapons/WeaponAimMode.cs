namespace Icaro
{
    // Como a assistência de mira (AimAssist) age sobre os projéteis de uma arma
    public enum WeaponAimMode
    {
        // Sai apontado para o alvo e o persegue (Agulha, Titan, Ion, Viper)
        Homing,

        // Sai apontado para o alvo, mantendo a dispersão da arma, e voa reto
        AimOnly,

        // Sai reto do cano, sem assistência nem magnetismo (Liberdade)
        None,
    }
}
