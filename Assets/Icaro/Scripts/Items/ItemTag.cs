using System;

namespace Icaro
{
    // Selos de um item, mostrados ao lado da arte da carta. Fogo, Elétrico e Tempo são os elementos (as cartas
    // elementais têm a frase de efeito no pé).
    [Flags]
    public enum ItemTag
    {
        None = 0,
        Dano = 1 << 0,
        Arma = 1 << 1,
        Defesa = 1 << 2,
        Vida = 1 << 3,
        Mobilidade = 1 << 4,
        AoAcertar = 1 << 5,
        Fogo = 1 << 6,
        Eletrico = 1 << 7,
        Tempo = 1 << 8,
        Recurso = 1 << 9,
    }
}
