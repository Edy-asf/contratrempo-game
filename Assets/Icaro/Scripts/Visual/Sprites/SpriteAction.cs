namespace Icaro
{
    // O que o inimigo está fazendo, para o sprite escolher o clipe (EnemySpriteView; o nome do clipe é o do valor).
    // Dano e morte vêm da vida. Windup2/Attack2: um segundo ataque com animação própria (a pancada do Golem).
    public enum SpriteAction
    {
        Idle,
        Move,
        Windup,
        Attack,
        Windup2,
        Attack2,
    }
}
