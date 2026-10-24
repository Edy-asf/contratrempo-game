namespace Icaro
{
    // Cor de um pixel das artes geradas em código (ícones e molduras das cartas). Não usa UnityEngine, para o mesmo
    // código desenhar as prévias fora do Unity.
    public struct Px
    {
        public byte R, G, B, A;

        public Px(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        // 0xRRGGBB
        public static Px Hex(uint rgb, byte a = 255) => new Px((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

        public Px WithAlpha(int a) => new Px(R, G, B, (byte)Clamp(a));

        // Mais clara (> 1) ou mais escura (< 1)
        public Px Scaled(float factor) =>
            new Px((byte)Clamp(R * factor), (byte)Clamp(G * factor), (byte)Clamp(B * factor), A);

        public static Px Lerp(Px a, Px b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new Px((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t),
                (byte)(a.A + (b.A - a.A) * t));
        }

        static int Clamp(float v) => v < 0f ? 0 : v > 255f ? 255 : (int)v;
    }
}
