namespace Icaro
{
    // Um clipe da folha de sprites desenhada em código: nome (Idle, Move, Windup, Attack, Hit, Death), quadros,
    // quadros por segundo a 1,0x e se repete
    public readonly struct EnemyClipSpec
    {
        public readonly string Name;
        public readonly int Frames;
        public readonly float Fps;
        public readonly bool Loop;

        public EnemyClipSpec(string name, int frames, float fps, bool loop)
        {
            Name = name;
            Frames = frames;
            Fps = fps;
            Loop = loop;
        }
    }
}
