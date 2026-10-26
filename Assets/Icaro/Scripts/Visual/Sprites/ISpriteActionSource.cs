namespace Icaro
{
    // IA que diz ao sprite o que está fazendo. Sem ela, o EnemySpriteView usa só parado e andando (pelo NavMeshAgent).
    public interface ISpriteActionSource
    {
        SpriteAction SpriteAction { get; }

        // Quanto da ação já passou (0 a 1) para clipes presos à duração dela (aviso e golpe); negativo = pelo relógio
        float SpriteActionProgress { get; }
    }
}
