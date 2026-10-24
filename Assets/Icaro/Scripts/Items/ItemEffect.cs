using System;

namespace Icaro
{
    // Um efeito de item: quanto ele soma a um atributo. Acúmulo linear como no Risk of Rain 2: a primeira pilha
    // dá FirstStack e cada pilha a mais soma PerExtraStack.
    [Serializable]
    public struct ItemEffect
    {
        public PlayerStat Stat;
        public float FirstStack;
        public float PerExtraStack;

        public float ValueFor(int stacks) => stacks <= 0 ? 0f : FirstStack + PerExtraStack * (stacks - 1);
    }
}
