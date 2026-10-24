using UnityEngine;

namespace Icaro
{
    // Gira as estrelas do céu noturno (shader Icaro/NightSky) em volta do polo celeste, deixando rastro. A velocidade
    // segue o multiplicador de tempo de Kairós (a 6x o céu gira 6 vezes mais rápido) e o rastro é o caminho que a
    // estrela fez nos últimos TrailSeconds, até MaxTrailDegrees. Pausa junto com o jogo (Time.deltaTime).
    public class NightSkyMotion : MonoBehaviour
    {
        [Tooltip("Graus por segundo a 1,0x")]
        public float DegreesPerSecond = 2f;

        [Tooltip("1 = velocidade proporcional ao multiplicador; maior = acelera mais no fim")]
        public float SpeedExponent = 1f;

        [Tooltip("Segundos de caminho que o rastro mostra")]
        public float TrailSeconds = 3f;

        [Tooltip("Rastro máximo, em graus (o shader cobre até ~24°)")]
        [Range(0f, 24f)] public float MaxTrailDegrees = 20f;

        static readonly int k_Angle = Shader.PropertyToID("_IcaroStarAngle");
        static readonly int k_Trail = Shader.PropertyToID("_IcaroStarTrail");

        float m_Angle;

        void Update()
        {
            float speed = DegreesPerSecond * Mathf.Pow(Mathf.Max(1f, TimeManager.CurrentMultiplier), SpeedExponent);
            m_Angle = Mathf.Repeat(m_Angle + speed * Time.deltaTime, 360f);
            Shader.SetGlobalFloat(k_Angle, m_Angle * Mathf.Deg2Rad);
            Shader.SetGlobalFloat(k_Trail, Mathf.Min(speed * TrailSeconds, MaxTrailDegrees) * Mathf.Deg2Rad);
        }
    }
}
