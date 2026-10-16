namespace Icaro
{
    // Selos dos itens (13 x 13) ao lado da arte da carta: um símbolo de 9 x 9 em pixel ('#' cor do selo, '+' tom
    // claro) dentro de um quadrado escuro com borda. Um selo por ItemTag.
    public static class ItemBadgePainter
    {
        public const int Size = 13;

        public static PixelCanvas Paint(ItemTag tag)
        {
            string[] symbol = SymbolOf(tag);
            if (symbol == null)
                return null;
            Px color = ColorOf(tag);

            var c = new PixelCanvas(Size, Size);
            c.Rect(1, 0, Size - 2, Size, PixelRamps.Outline);
            c.Rect(0, 1, Size, Size - 2, PixelRamps.Outline);
            c.Rect(1, 1, Size - 2, Size - 2, color.Scaled(0.45f));
            c.Rect(2, 2, Size - 4, Size - 4, Px.Hex(0x12161e));
            Px light = Px.Lerp(color, PixelRamps.White, 0.55f);
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    char p = symbol[y][x];
                    if (p == '#')
                        c.Set(2 + x, 2 + y, color);
                    else if (p == '+')
                        c.Set(2 + x, 2 + y, light);
                }
            }

            return c;
        }

        public static Px ColorOf(ItemTag tag) => tag switch
        {
            ItemTag.Dano => Px.Hex(0xff6a5a),
            ItemTag.Arma => Px.Hex(0xc8d0dc),
            ItemTag.Defesa => Px.Hex(0x6fb0ff),
            ItemTag.Vida => Px.Hex(0x6ee07a),
            ItemTag.Mobilidade => Px.Hex(0x8affd0),
            ItemTag.AoAcertar => Px.Hex(0xffd04a),
            ItemTag.Fogo => Px.Hex(0xff8a2a),
            ItemTag.Eletrico => Px.Hex(0x8ae8ff),
            ItemTag.Tempo => Px.Hex(0xc08cff),
            ItemTag.Recurso => Px.Hex(0x46e0e8),
            _ => Px.Hex(0xffffff),
        };

        static string[] SymbolOf(ItemTag tag) => tag switch
        {
            ItemTag.Dano => new[]
            {
                "....#....",
                "..#####..",
                ".#..#..#.",
                ".#.....#.",
                "####+####",
                ".#.....#.",
                ".#..#..#.",
                "..#####..",
                "....#....",
            },
            ItemTag.Arma => new[]
            {
                "..#.#.#..",
                ".#######.",
                "##+++++##",
                ".#+...+#.",
                "##+...+##",
                ".#+...+#.",
                "##+++++##",
                ".#######.",
                "..#.#.#..",
            },
            ItemTag.Defesa => new[]
            {
                "#########",
                "#+++++++#",
                "#+#####+#",
                "#+#####+#",
                ".#+###+#.",
                ".#+###+#.",
                "..#+#+#..",
                "...#+#...",
                "....#....",
            },
            ItemTag.Vida => new[]
            {
                ".##...##.",
                "#+##.####",
                "#+#######",
                "#########",
                ".#######.",
                "..#####..",
                "...###...",
                "....#....",
                ".........",
            },
            ItemTag.Mobilidade => new[]
            {
                "#...#....",
                "##..##...",
                ".##..##..",
                "..+#..+#.",
                "...+#..+#",
                "..+#..+#.",
                ".##..##..",
                "##..##...",
                "#...#....",
            },
            ItemTag.AoAcertar => new[]
            {
                "...###...",
                "...#+#...",
                "...#+#...",
                "...#+#...",
                "...###...",
                "....#....",
                ".........",
                "...###...",
                "...###...",
            },
            ItemTag.Fogo => new[]
            {
                "....#....",
                "....##...",
                "...###...",
                "..####.#.",
                "..######.",
                ".###+###.",
                ".##+++##.",
                ".##+++##.",
                "..#+++#..",
            },
            ItemTag.Eletrico => new[]
            {
                ".....###.",
                "....###..",
                "...###...",
                "..##+###.",
                ".###+##..",
                "....###..",
                "...###...",
                "..##.....",
                ".#.......",
            },
            ItemTag.Tempo => new[]
            {
                "#########",
                ".#+++++#.",
                "..#+++#..",
                "...#+#...",
                "....#....",
                "...#.#...",
                "..#.+.#..",
                ".#+++++#.",
                "#########",
            },
            ItemTag.Recurso => new[]
            {
                "....#....",
                "...#+#...",
                "..#++##..",
                ".#++####.",
                "#+++#####",
                ".#++####.",
                "..#+###..",
                "...#+#...",
                "....#....",
            },
            _ => null,
        };
    }
}
