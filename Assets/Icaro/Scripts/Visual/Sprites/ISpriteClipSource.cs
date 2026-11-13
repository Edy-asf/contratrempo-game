namespace Icaro
{
    // IA que escolhe o clipe da folha pelo nome (chefes com uma animação para cada ataque). Tem prioridade sobre o
    // ISpriteActionSource enquanto SpriteClip não for vazio e existir na folha.
    public interface ISpriteClipSource
    {
        // Nome do clipe agora; vazio ou null = deixa o ISpriteActionSource decidir
        string SpriteClip { get; }

        // Quanto do clipe já passou (0 a 1) para clipes presos à duração da ação; negativo = pelo relógio
        float SpriteClipProgress { get; }

        // O clipe é um aviso de ataque (pulsa em vermelho)
        bool SpriteClipWarning { get; }

        // Sprite camuflado: sem contorno de destaque (só os pixels que a folha deixou aparecem)
        bool SpriteHidden { get; }
    }
}
