using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Escrita aeônida: 32 hieróglifos em pixel art desenhados em código (9 x 12 px, brancos; a cor vem do texto). Cada
    // um nasce de traços entre os nós de uma grade de 3 x 4, sempre com uma "prateleira" embaixo ou uma haste ao lado
    // (lembram os anéis e as colunas que eles construíram), às vezes com um anel pequeno. A cifra é fixa: a mesma letra
    // vira sempre o mesmo glifo (sem acento e sem diferença de maiúscula), os números também, e a pontuação vira um
    // sinal de dois pontos. Assim o jogador atento vê palavras repetidas mesmo sem o tradutor.
    public static class AlienGlyphs
    {
        public const int Count = 32;
        public const int Width = 9, Height = 12;
        public const int Separator = Count;     // pontuação

        static Sprite[] s_Sprites;

        public static Sprite Get(int index)
        {
            // sem recarregar o domínio, as texturas do Play anterior já foram destruídas
            if (s_Sprites == null || !s_Sprites[0])
                Build();
            return s_Sprites[Mathf.Clamp(index, 0, s_Sprites.Length - 1)];
        }

        // Glifo de uma letra (-1 = espaço, sem glifo)
        public static int IndexOf(char c)
        {
            if (char.IsWhiteSpace(c))
                return -1;
            c = char.ToLowerInvariant(Plain(c));
            if (c >= 'a' && c <= 'z')
                return c - 'a';
            if (c >= '0' && c <= '9')
                return 26 + (c - '0') % 6;
            return Separator;
        }

        // Um glifo qualquer, para o embaralhar da decodificação
        public static int Random(System.Random random) => random.Next(Count);

        static char Plain(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'á': case 'à': case 'â': case 'ã': case 'ä': return 'a';
                case 'é': case 'ê': case 'è': return 'e';
                case 'í': case 'î': return 'i';
                case 'ó': case 'ô': case 'õ': case 'ö': return 'o';
                case 'ú': case 'ü': return 'u';
                case 'ç': return 'c';
                default: return c;
            }
        }

        static void Build()
        {
            s_Sprites = new Sprite[Count + 1];
            var random = new System.Random(4242);
            var seen = new HashSet<string>();
            for (int i = 0; i < Count; i++)
            {
                bool[,] pixels;
                string key;
                int tries = 0;
                do
                {
                    pixels = Paint(random);
                    key = Key(pixels);
                } while (!seen.Add(key) && ++tries < 50);

                s_Sprites[i] = ToSprite(pixels, "Glifo_" + i);
            }

            // Separador: dois pontos na altura do meio, como um sinal de pausa
            var separator = new bool[Width, Height];
            Dot(separator, 4, 4);
            Dot(separator, 4, 8);
            s_Sprites[Count] = ToSprite(separator, "Glifo_Pausa");
        }

        // Nós da grade (x 1, 4, 7; y 1, 4, 7, 10)
        static Vector2Int Node(int gx, int gy) => new Vector2Int(1 + gx * 3, 1 + gy * 3);

        static bool[,] Paint(System.Random random)
        {
            var p = new bool[Width, Height];

            // Base: prateleira embaixo, haste à esquerda ou haste no meio
            switch (random.Next(3))
            {
                case 0:
                    Line(p, Node(0, 3), Node(2, 3));
                    break;
                case 1:
                    Line(p, Node(0, 0), Node(0, 3));
                    break;
                default:
                    Line(p, Node(1, 0), Node(1, 3));
                    break;
            }

            // Dois a três traços entre nós vizinhos (no máximo um passo em cada direção, para ficar legível)
            int strokes = 2 + random.Next(2);
            for (int s = 0; s < strokes; s++)
            {
                int gx = random.Next(3), gy = random.Next(4);
                int dx = random.Next(3) - 1, dy = random.Next(3) - 1;
                if (dx == 0 && dy == 0)
                    dy = 1;
                int hx = Mathf.Clamp(gx + dx, 0, 2), hy = Mathf.Clamp(gy + dy, 0, 3);
                if (hx == gx && hy == gy)
                    continue;
                Line(p, Node(gx, gy), Node(hx, hy));
            }

            // Às vezes um anel pequeno ou um ponto solto
            int extra = random.Next(4);
            if (extra == 0)
                Ring(p, Node(random.Next(3), random.Next(3)));
            else if (extra == 1)
                Dot(p, Node(random.Next(3), random.Next(4)).x, Node(0, random.Next(4)).y);
            return p;
        }

        static void Line(bool[,] p, Vector2Int a, Vector2Int b)
        {
            int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Set(p, Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)));
            }
        }

        static void Ring(bool[,] p, Vector2Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        Set(p, c.x + dx, c.y + dy);
        }

        static void Dot(bool[,] p, int x, int y) => Set(p, x, y);

        static void Set(bool[,] p, int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
                p[x, y] = true;
        }

        static string Key(bool[,] p)
        {
            var chars = new char[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    chars[y * Width + x] = p[x, y] ? '1' : '0';
            return new string(chars);
        }

        // y = 0 é o alto do glifo
        static Sprite ToSprite(bool[,] p, string name)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[(Height - 1 - y) * Width + x] = p[x, y] ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 12f);
        }
    }
}
