namespace Icaro
{
    // Rampas de cor (do mais escuro ao mais claro) dos sprites dos inimigos do Bosque: casca, musgo, carapaça,
    // pétalas, caule, pelo do cervo, metal velho, mariposa, pedra, micélio, sapo e o manto do Oráculo
    public static class EnemyRamps
    {
        public static readonly Px[] Bark = R(0x1c120d, 0x352217, 0x553722, 0x7a5331, 0xa47b4b);
        public static readonly Px[] Antler = R(0x2a1d14, 0x4d3624, 0x76573a, 0xa3825a, 0xd2b88e);
        public static readonly Px[] Moss = R(0x0e2619, 0x1a4429, 0x2b6635, 0x4a8c42, 0x7fb85c);
        public static readonly Px[] Shell = R(0x111821, 0x1d2a35, 0x2c414d, 0x445f68, 0x6a8a88);
        public static readonly Px[] Petal = R(0x260d2b, 0x4a1850, 0x742c76, 0xa24e9a, 0xd385c4);
        public static readonly Px[] Stem = R(0x0c231f, 0x153e37, 0x205b4f, 0x377f6c, 0x63a993);
        public static readonly Px[] Hide = R(0x15111d, 0x271f33, 0x3c324b, 0x574969, 0x7c6d92);
        public static readonly Px[] Metal = R(0x181b20, 0x2c3139, 0x464e58, 0x68727e, 0x939ea9);
        public static readonly Px[] Rust = R(0x2a120a, 0x4f2312, 0x7a3a1a, 0xa45a2a, 0xc98244);
        public static readonly Px[] MothBody = R(0x130e17, 0x241a2a, 0x382a3e, 0x504056, 0x6c5874);
        public static readonly Px[] MothWing = R(0x1d1420, 0x34233a, 0x4e3652, 0x6d4f6c, 0x92718c);
        public static readonly Px[] Glow = R(0x6a3208, 0xb0600e, 0xe89a22, 0xffcf5a, 0xfff2c0);
        public static readonly Px[] Bio = R(0x0b4a5a, 0x168a9a, 0x2cc6cf, 0x7aeef0, 0xe0fffd);
        public static readonly Px[] Bone = R(0x4a4436, 0x7a7158, 0xaaa07e, 0xd2c9a6, 0xf2ecd2);
        public static readonly Px[] Stone = R(0x14161d, 0x272c37, 0x3f4756, 0x5e6878, 0x8a95a5);
        public static readonly Px[] Mycelium = R(0x353044, 0x5a546e, 0x878198, 0xb5afc6, 0xe3dfed);
        public static readonly Px[] Frog = R(0x0d2318, 0x183f25, 0x2a6135, 0x498944, 0x79b35d);
        public static readonly Px[] Belly = R(0x45452a, 0x737446, 0xa1a467, 0xcbcd8e, 0xececbb);
        public static readonly Px[] Acid = R(0x1f4a0a, 0x3d8213, 0x6cbf25, 0xa8e65e, 0xe6ffb8);
        public static readonly Px[] Robe = R(0x0f1220, 0x1c2236, 0x2d3650, 0x434f6e, 0x5f6e92);

        public static readonly Px Outline = Px.Hex(0x07060b);
        public static readonly Px Dark = Px.Hex(0x0d0b12);
        public static readonly Px AmberLight = Px.Hex(0xffb840);
        public static readonly Px CyanLight = Px.Hex(0x6ff2ff);

        static Px[] R(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
