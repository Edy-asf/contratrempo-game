using UnityEngine;

namespace Icaro
{
    // Sprite 2D no mundo que sempre olha para o jogador (estilo Doom): gira só em volta do eixo vertical, nunca
    // inclina. Não tem Update próprio: o SpriteBillboardSystem gira todos de uma vez no LateUpdate (barato mesmo com
    // centenas de sprites). O sprite é um quad com a frente para -Z (como o Quad do Unity).
    public class SpriteBillboard : MonoBehaviour
    {
        void OnEnable() => SpriteBillboardSystem.Register(transform);
        void OnDisable() => SpriteBillboardSystem.Unregister(transform);
    }
}
