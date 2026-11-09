using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Marca de uma peça posta pelo gerador de cenário (kit do Blender): a chave estável (modelo + onde o código a pôs)
    // e a pose gerada, para o menu "Ícaro > Ajustes manuais" saber o que o usuário moveu, girou ou desligou à mão.
    // Só dados; nada roda em jogo.
    [DisallowMultipleComponent, SelectionBase]
    public class GeneratedPiece : MonoBehaviour
    {
        public string Key;
        public Vector3 GeneratedPosition;
        public Quaternion GeneratedRotation = Quaternion.identity;
        public Vector3 GeneratedScale = Vector3.one;
    }
}
