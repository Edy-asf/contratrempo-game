using Unity.FPS.Gameplay;
using UnityEngine;

namespace Icaro
{
    // Distância máxima de renderização da câmera do jogador nesta cena (o prefab usa 150 m, que basta para o Bosque).
    // Biomas abertos com estruturas ao fundo (Arquipélago) pedem mais.
    public class CameraFarClip : MonoBehaviour
    {
        [Tooltip("Distância máxima de renderização da câmera do jogador, em metros")]
        public float Distance = 1200f;

        void Start()
        {
            var player = FindAnyObjectByType<PlayerCharacterController>();
            if (player && player.PlayerCamera)
                player.PlayerCamera.farClipPlane = Distance;
        }
    }
}
