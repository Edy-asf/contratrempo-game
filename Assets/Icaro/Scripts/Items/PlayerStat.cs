namespace Icaro
{
    // Atributos que os itens alteram. O PlayerStats soma as contribuições de todos os itens; os componentes do
    // jogador (armas, movimento, defesas, efeitos de acerto) leem o total.
    public enum PlayerStat
    {
        CritChance,              // +chance de crítico (0,1 = 10%)
        FireRate,                // +cadência (0,15 = 15% mais tiros por segundo)
        MaxHealth,               // +vida máxima (pontos)
        MoveSpeed,               // +velocidade de movimento (0,14 = 14%)
        LightningChance,         // chance de raio ao acertar
        LightningTargets,        // inimigos que o raio alcança
        LightningDamage,         // dano do raio (fração do dano do tiro)
        ExplosionDamage,         // dano da explosão ao matar (fração do dano do tiro)
        ExplosionRadius,         // raio da explosão (m)
        AirJumps,                // pulos extras no ar
        ShieldFraction,          // escudo (fração da vida máxima)
        EchoChance,              // chance de o tiro se repetir (acima de 1 = repete mais de uma vez)
        CritHeal,                // vida curada por acerto crítico
        DamagePerTenthMultiplier, // +dano para cada 0,1x de multiplicador de tempo acima de 1,0x (teto no ItemCatalog)
        TimeSlow,                // quanto o multiplicador de tempo cresce mais devagar (0,3 = 30%; sem item hoje)
        MissileChance,           // chance de míssil ao acertar
        MissileDamage,           // dano do míssil (fração do dano do tiro)
        Revives,                 // revividas restantes
        DashCooldownReduction,   // +velocidade de recarga do dash (0,2 = recarga em 1/1,2 do tempo)
        DashDistance,            // +distância do dash (0,25 = 25%)
        DashDamage,              // dano do dash nos inimigos do caminho (fração do dano do tiro)
        DashBarrier,             // barreira temporária ganha a cada dash (pontos)
        DashCharges,             // cargas extras de dash
        DamageBonus,             // +dano de todos os tiros (0,08 = 8%)
        AmmoCapacity,            // +munição no pente (0,2 = 20%; a recarga completa leva o mesmo tempo)
        ReloadTimeReduction,     // -tempo de recarga da munição (0,15 = 15% mais curto; piso no ItemCatalog)
        CloseRangeDamage,        // +dano dos tiros em inimigos perto do jogador (distância no ItemCatalog)
        HealthRegen,             // vida regenerada por segundo
        PickupRange,             // +raio em que as Cronitas são atraídas (0,4 = 40%)
        SlowChance,              // chance de deixar o inimigo atingido mais lento (Munição Atordoante)
        SlowAmount,              // lentidão aplicada (0,3 = 30% mais lento; teto no ItemCatalog)
        PierceChance,            // chance de o tiro atravessar um inimigo (acima de 1 = atravessa mais de um)
        BurnChance,              // chance de incendiar o inimigo atingido
        BurnDamage,              // dano total de cada queimadura (fração do dano do tiro)
        BurningVulnerability,    // +dano que inimigos em chamas recebem de todas as fontes
        CritDamage,              // +multiplicador de crítico (1 = de 2x para 3x)
        AdrenalineDuration,      // segundos de adrenalina ao levar dano (0 = sem o item)
    }
}
