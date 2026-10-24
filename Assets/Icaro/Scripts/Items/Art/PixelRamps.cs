namespace Icaro
{
    // Rampas de cor (do mais escuro ao mais claro) das artes em pixel dos itens e das cartas
    public static class PixelRamps
    {
        public static readonly Px[] Steel = R(0x1b2030, 0x3a4558, 0x64738a, 0x9aa8bc, 0xd8e0ea);
        public static readonly Px[] Silver = R(0x2a2e38, 0x5c6372, 0x9aa2b2, 0xd2d8e2, 0xf8fafd);
        public static readonly Px[] Dark =R(0x10131a, 0x1f2531, 0x323b4b, 0x4c586c, 0x6e7c92);
        public static readonly Px[] Cyan = R(0x0b3d4f, 0x137a8f, 0x25b8c9, 0x6fe8ef, 0xdcfffd);
        public static readonly Px[] Teal = R(0x0a2e33, 0x135a62, 0x1f8a8f, 0x3fc0b8, 0x9af0e0);
        public static readonly Px[] Copper = R(0x3a1a0c, 0x7a3a14, 0xb8641e, 0xe89a3a, 0xffd27a);
        public static readonly Px[] Fire = R(0x6a1206, 0xb02a06, 0xe85d0c, 0xffa31a, 0xfff27a);
        public static readonly Px[] BlueFire = R(0x0c2a6a, 0x1450c0, 0x2a8cff, 0x6fd0ff, 0xe8fbff);
        public static readonly Px[] Bio = R(0x0d3018, 0x1c6630, 0x34a84a, 0x7ee070, 0xd6ffc0);
        public static readonly Px[] Red = R(0x3a0710, 0x7a1020, 0xc02034, 0xee5060, 0xffb0b8);
        public static readonly Px[] Purple = R(0x22103d, 0x45207a, 0x7a3ec0, 0xb57cf0, 0xeed4ff);
        public static readonly Px[] Gold = R(0x3d2808, 0x7a5414, 0xc08a22, 0xeec455, 0xfff0b0);
        public static readonly Px[] Amber = R(0x4a2306, 0x8f4a0c, 0xd98314, 0xffc04a, 0xfff0b8);
        public static readonly Px[] Ice = R(0x14304f, 0x2a5f8f, 0x4f97c8, 0x96d4f0, 0xeafaff);
        public static readonly Px[] Magenta = R(0x3a0a30, 0x7a1866, 0xc03aa0, 0xf07ad6, 0xffd6f6);
        public static readonly Px[] Bone = R(0x3a3c44, 0x6e7180, 0xa6a9b6, 0xd6d8e0, 0xf8f8fc);
        public static readonly Px[] Wood = R(0x2a160c, 0x4f2c16, 0x7a4822, 0xa86a34, 0xd4985a);

        public static readonly Px Outline = Px.Hex(0x0a0c12);
        public static readonly Px White = Px.Hex(0xffffff);

        static Px[] R(params uint[] hex)
        {
            var ramp = new Px[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ramp[i] = Px.Hex(hex[i]);
            return ramp;
        }
    }
}
