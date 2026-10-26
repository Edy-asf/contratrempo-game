using UnityEngine;

namespace Icaro
{
    // Ajustes das partículas de brilho de um bioma (esporos flutuando no ar), lidos pelo SporeField
    [System.Serializable]
    public class SporeTuning
    {
        public bool Enabled = true;

        [Tooltip("Quantas partículas ficam no ar ao mesmo tempo, em volta do jogador")]
        [Range(0, 3000)] public int Count = 500;

        [Tooltip("Raio da área em volta do jogador onde as partículas aparecem (m)")]
        [Range(5f, 300f)] public float Radius = 60f;

        [Tooltip("Nascem na superfície da água e sobem (as que descem abaixo da água somem); desligado, aparecem " +
                 "numa faixa de altura em volta do jogador")]
        public bool FromWater = true;

        [Tooltip("Velocidade com que sobem da água (m/s)")]
        [Range(0f, 3f)] public float RiseSpeed = 0.4f;

        [Tooltip("Ajuste fino da altura da água (m)")]
        [Range(-2f, 2f)] public float WaterLevelOffset = 0f;

        [Tooltip("Sem 'FromWater': faixa de altura em relação ao jogador (m), de baixo e de cima")]
        public Vector2 Height = new Vector2(-3f, 18f);

        [Tooltip("Tamanho de cada partícula (m): menor e maior")]
        public Vector2 Size = new Vector2(0.08f, 0.2f);

        [Tooltip("Brilho das partículas")]
        [Range(0f, 4f)] public float Brightness = 1f;

        [Tooltip("Velocidade com que flutuam para os lados (m/s)")]
        [Range(0f, 3f)] public float Speed = 0.15f;

        [Tooltip("Quanto o caminho delas é ondulado e irregular")]
        [Range(0f, 3f)] public float Wander = 0.5f;

        [Tooltip("Quanto tempo cada uma vive (s): menor e maior; elas surgem e somem aos poucos")]
        public Vector2 Lifetime = new Vector2(8f, 12f);

        [Tooltip("Cores sorteadas para cada partícula")]
        public Color ColorA = new Color(0.6f, 1f, 0.95f);
        public Color ColorB = new Color(0.85f, 1f, 0.9f);
        public Color ColorC = new Color(0.3f, 0.95f, 0.7f);
    }
}
