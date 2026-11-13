namespace Icaro
{
    // De que lado o jogador vê o inimigo no desenho: de frente, de lado (olhando para a direita da imagem; a
    // esquerda é o espelho) ou de costas. Nas folhas de 8 direções há também as diagonais, desenhadas do mesmo
    // lado da vista de lado: FrontSide (entre a frente e o lado) e BackSide (entre o lado e as costas).
    public enum EnemyView
    {
        Front,
        Side,
        Back,
        FrontSide,
        BackSide,
    }
}
