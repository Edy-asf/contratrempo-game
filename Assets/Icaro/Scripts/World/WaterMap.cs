using UnityEngine;

namespace Icaro
{
    // Mapa da água rasa de uma cena em grade (gerado pelo GridBiomeBuilder): quem anda dentro dela fica mais lento.
    // O jogador lê pelo PlayerStats (velocidade de movimento) e os inimigos pelo EnemyStats (entra no
    // CombatTiming.Movement, como a lentidão do TimeAnchorSlow). Quem voa (acima de MaxHeight) não é afetado.
    // A consulta é uma leitura de um array por posição, barata o bastante para todo inimigo a cada frame.
    public class WaterMap : MonoBehaviour
    {
        public static WaterMap Instance { get; private set; }

        [Tooltip("Velocidade dentro da água rasa (0,6 = 60%)")]
        [Range(0.1f, 1f)] public float SlowFactor = 0.6f;

        [Tooltip("Acima desta altura (pés), não conta como dentro da água (voadores, saltos)")]
        public float MaxHeight = 0.6f;

        [HideInInspector] public Vector3 Origin;
        [HideInInspector] public float CellSize = 2f;
        [HideInInspector] public int Width;
        [HideInInspector] public int Depth;
        [HideInInspector] public bool[] Shallow;

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsShallow(Vector3 position)
        {
            if (Shallow == null || position.y > Origin.y + MaxHeight)
                return false;
            int x = Mathf.FloorToInt((position.x - Origin.x) / CellSize);
            int z = Mathf.FloorToInt((position.z - Origin.z) / CellSize);
            return x >= 0 && z >= 0 && x < Width && z < Depth && Shallow[z * Width + x];
        }

        // Multiplicador de velocidade nesta posição (1 fora da água ou sem mapa na cena)
        public static float SpeedAt(Vector3 position) =>
            Instance && Instance.IsShallow(position) ? Instance.SlowFactor : 1f;
    }
}
