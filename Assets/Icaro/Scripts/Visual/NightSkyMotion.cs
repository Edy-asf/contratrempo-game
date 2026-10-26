using UnityEngine;

namespace Icaro
{
    // Gira as estrelas do céu noturno (shader Icaro/NightSky) em volta do polo celeste, deixando rastro. A velocidade
    // segue o multiplicador de tempo de Kairós (a 6x o céu gira 6 vezes mais rápido) e o rastro é o caminho que a
    // estrela fez nos últimos segundos, até um máximo em graus. Pausa junto com o jogo (Time.deltaTime).
    // Os polos ficam no horizonte, a leste e a oeste (dentro da névoa): as estrelas nascem no sul, cruzam o alto do céu
    // em traços retos e se põem no norte (+Z, para onde a Via avança). Perto dos polos o rastro encurta até sumir e as
    // estrelas coladas neles somem, sem redemoinho nem bolas de luz. Todos os valores vêm do asset de ajustes do céu
    // (SkySettings, menu "Ícaro > Ajustes do céu"), lido a cada frame.
    public class NightSkyMotion : MonoBehaviour
    {
        static readonly int k_Angle = Shader.PropertyToID("_IcaroStarAngle");
        static readonly int k_Trail = Shader.PropertyToID("_IcaroStarTrail");
        static readonly int k_Axis = Shader.PropertyToID("_IcaroStarAxis");
        static readonly int k_PoleFade = Shader.PropertyToID("_IcaroPoleFade");
        static readonly int k_Look = Shader.PropertyToID("_IcaroStarLook");
        static readonly int k_Aurora0 = Shader.PropertyToID("_IcaroAurora0");
        static readonly int k_Aurora1 = Shader.PropertyToID("_IcaroAurora1");
        static readonly int k_Aurora2 = Shader.PropertyToID("_IcaroAurora2");
        static readonly int k_Aurora3 = Shader.PropertyToID("_IcaroAurora3");
        static readonly int k_Aurora4 = Shader.PropertyToID("_IcaroAurora4");

        float m_Angle;
        int m_Version = -1;

        // Bioma em que o jogador começa (antes de atravessar o primeiro BiomeZone)
        static BiomeZone ZoneAtPlayer()
        {
            var player = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            if (!player)
                return null;
            foreach (BiomeZone zone in FindObjectsByType<BiomeZone>(FindObjectsSortMode.None))
            {
                var box = zone.GetComponent<Collider>();
                if (box && box.bounds.Contains(player.transform.position))
                    return zone;
            }

            return null;
        }

        // As partículas de brilho do Arquipélago passam a acompanhar o jogador (SporeField)
        void Start()
        {
            foreach (ParticleSystem system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (system.name == "Esporos_Arquipelago" && !system.GetComponent<SporeField>())
                    system.gameObject.AddComponent<SporeField>();
            }
        }

        void Update()
        {
            SkySettings settings = SkySettings.Current;
            settings.ApplyBiomes();
            // Claridade do mapa: na primeira vez e a cada mudança nos ajustes, reaplica o bioma em que o jogador está
            if (m_Version != SkySettings.Version)
            {
                m_Version = SkySettings.Version;
                BiomeZone zone = BiomeZone.Current ? BiomeZone.Current : ZoneAtPlayer();
                if (zone)
                    zone.ReapplyLighting();
            }
            float speed = settings.DegreesPerSecond *
                          Mathf.Pow(Mathf.Max(1f, TimeManager.CurrentMultiplier), settings.SpeedExponent);
            m_Angle = Mathf.Repeat(m_Angle + speed * Time.deltaTime, 360f);
            Shader.SetGlobalFloat(k_Angle, m_Angle * Mathf.Deg2Rad);
            // O rastro fica atrás do sentido do giro: com o giro invertido, o shader recebe o rastro negativo
            float trail = Mathf.Min(Mathf.Abs(speed) * settings.TrailSeconds, settings.MaxTrailDegrees) * Mathf.Deg2Rad;
            Shader.SetGlobalFloat(k_Trail, speed < 0f ? -trail : trail);
            float azimuth = settings.PoleAzimuth * Mathf.Deg2Rad, elevation = settings.PoleElevation * Mathf.Deg2Rad;
            Shader.SetGlobalVector(k_Axis, new Vector4(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                Mathf.Cos(azimuth) * Mathf.Cos(elevation), 0f));
            Shader.SetGlobalFloat(k_PoleFade, settings.PoleFadeDegrees * Mathf.Deg2Rad);
            Shader.SetGlobalVector(k_Look, new Vector4(settings.Density, settings.Brightness, settings.Thickness, 1f));
            // Aurora (o w de _IcaroAurora3 marca que os ajustes foram passados)
            Shader.SetGlobalVector(k_Aurora0, new Vector4(settings.AuroraIntensity, settings.AuroraBands,
                settings.AuroraSpacing, settings.AuroraWaviness));
            Shader.SetGlobalVector(k_Aurora1, new Vector4(settings.AuroraWaveLength, settings.AuroraHeight,
                settings.AuroraSpeed, settings.AuroraRays));
            Shader.SetGlobalVector(k_Aurora2, new Vector4(settings.AuroraRayDensity, settings.AuroraPieces,
                settings.AuroraPieceLength, settings.AuroraPieceHeight));
            Shader.SetGlobalVector(k_Aurora3, new Vector4(settings.AuroraPieceIntensity, settings.AuroraPieceDistance,
                settings.AuroraOffset, 1f));
            Shader.SetGlobalVector(k_Aurora4, new Vector4(settings.AuroraPieceOffsetEastWest,
                settings.AuroraPieceOffsetSouthNorth, settings.AuroraPieceSpread, settings.AuroraPieceDrift));
        }
    }
}
