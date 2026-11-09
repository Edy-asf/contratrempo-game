using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Lista de todas as chaves que a geração pôs na cena (na raiz do cenário), para o salvamento perceber as peças que
    // o usuário apagou
    public class GeneratedPieceIndex : MonoBehaviour
    {
        public string Scope;
        public List<string> Keys = new List<string>();
    }
}
