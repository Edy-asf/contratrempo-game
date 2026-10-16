using UnityEngine;
using UnityEngine.AI;

namespace Icaro
{
    // Porta de sala: fica afundada no chão quando aberta e sobe para trancar a passagem.
    // A posição em que é colocada na cena é a posição fechada.
    public class RoomDoor : MonoBehaviour
    {
        [Tooltip("Quanto a porta desce para dentro do chão ao abrir")]
        public float OpenDepth = 4.2f;

        [Tooltip("Velocidade de abertura/fechamento, em m/s")]
        public float MoveSpeed = 8f;

        [Tooltip("Estado inicial ao carregar a cena")]
        public bool StartOpen = true;

        [Tooltip("Som ao começar a se mover (opcional)")]
        public AudioClip MoveSfx;

        Vector3 m_ClosedPosition;
        Vector3 m_OpenPosition;
        NavMeshObstacle m_Obstacle;
        AudioSource m_AudioSource;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            m_ClosedPosition = transform.localPosition;
            m_OpenPosition = m_ClosedPosition + Vector3.down * OpenDepth;
            m_Obstacle = GetComponent<NavMeshObstacle>();
            m_AudioSource = GetComponent<AudioSource>();

            IsOpen = StartOpen;
            transform.localPosition = IsOpen ? m_OpenPosition : m_ClosedPosition;
            UpdateObstacle();
        }

        void Update()
        {
            Vector3 target = IsOpen ? m_OpenPosition : m_ClosedPosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, MoveSpeed * Time.deltaTime);
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;

            IsOpen = open;
            UpdateObstacle();

            if (MoveSfx && m_AudioSource)
                m_AudioSource.PlayOneShot(MoveSfx);
        }

        // O obstáculo só recorta o NavMesh enquanto a porta está fechada
        void UpdateObstacle()
        {
            if (m_Obstacle)
                m_Obstacle.enabled = !IsOpen;
        }
    }
}
