using System;
using static Icaro.PixelCanvas;
using static Icaro.PixelRamps;

namespace Icaro
{
    // Ícones em pixel art dos itens (48 x 48), desenhados em código a partir das formas sombreadas do PixelCanvas.
    // A chave é o nome da ficha sem o prefixo "Item_" (ex.: "CartuchoIncendiario"); chave desconhecida devolve null
    // (a interface usa o ícone provisório com a letra). Cada ícone: corpo, contorno escuro e, por cima, brilhos,
    // chamas e faíscas.
    public static class ItemIconPainter
    {
        public const int Size = 48;

        public static PixelCanvas Paint(string key)
        {
            var c = new PixelCanvas(Size, Size);
            switch (key)
            {
                case "LenteDeCalibracao": Lente(c); break;
                case "ServoGatilho": Servo(c); break;
                case "PlacaDeCasco": Placa(c); break;
                case "AtuadoresLeves": Atuadores(c); break;
                case "PolvoraSintetica": Polvora(c); break;
                case "TamborAmpliado": Tambor(c); break;
                case "ResfriadorAvancado": Resfriador(c); break;
                case "CanoCurto": CanoCurto(c); break;
                case "SoroDeBiomassa": Soro(c); break;
                case "NucleoImantado": Ima(c); break;
                case "CapacitorDeImpulso": Capacitor(c); break;
                case "BocalEstendido": Bocal(c); break;
                case "LaminaDeFase": Lamina(c); break;
                case "EgideDeImpulso": Egide(c); break;
                case "BobinaRessonante": Bobina(c); break;
                case "ResinaVolatil": Resina(c); break;
                case "PropulsorAuxiliar": Propulsor(c); break;
                case "EscudoDeSincronia": Escudo(c); break;
                case "EcoTemporal": Eco(c); break;
                case "CristalCeifador": Cristal(c); break;
                case "InjetorDeAdrenalina": Injetor(c); break;
                case "MunicaoAtordoante": Atordoante(c); break;
                case "DisparoPerfurante": Perfurante(c); break;
                case "CartuchoIncendiario": Cartucho(c); break;
                case "AceleradorTermico": Acelerador(c); break;
                case "CoracaoAeonida": Coracao(c); break;
                case "AmplificadorFocal": Amplificador(c); break;
                case "MisseisDeSondagem": Missil(c); break;
                case "SegundaIgnicao": Ignicao(c); break;
                case "CamaraDupla": Camara(c); break;
                default: return null;
            }

            return c;
        }

        // ---------- Comuns ----------

        static void Lente(PixelCanvas c)
        {
            c.Fill(Ring(24, 24, 18.5f, 12.5f), Steel);
            for (int i = 0; i < 4; i++)
            {
                double a = (45 + i * 90) * Math.PI / 180.0;
                c.Fill(Circle(24 + 15.5f * (float)Math.Cos(a), 24 + 15.5f * (float)Math.Sin(a), 2.4f), Dark, 0.15f);
            }

            c.Fill(Circle(24, 24, 12.5f), Ice, -0.05f);
            c.FillFlat(Ring(24, 24, 12.6f, 11.6f), Dark[1]);
            Px reticle = Px.Hex(0xff4a5a);
            c.Rect(23, 14, 2, 7, reticle);
            c.Rect(23, 27, 2, 7, reticle);
            c.Rect(14, 23, 7, 2, reticle);
            c.Rect(27, 23, 7, 2, reticle);
            c.Rect(23, 23, 2, 2, White);
            c.FillFlat(Ellipse(18.5f, 18, 3.2f, 2f), White.WithAlpha(200));
            c.Outline(Outline);
            c.Glow(24, 24, 23, Cyan[3], 0.35f);
        }

        static void Servo(PixelCanvas c)
        {
            c.Fill(Ring(19, 34, 10.5f, 7, 0, 180), Dark, 0.1f);
            c.Fill(Capsule(22, 27, 17, 38, 2.4f), Steel);
            c.Fill(Poly(2f, GearPoints(28, 19, 15, 11, 9)), Steel);
            c.Clear(Circle(28, 19, 4f));
            c.Fill(Ring(28, 19, 7f, 4f), Dark, 0.2f);
            c.Fill(Circle(39, 9, 2.6f), Bio, 0.2f);
            c.Outline(Outline);
            c.Glow(39, 9, 6, Bio[3], 0.7f);
        }

        static void Placa(PixelCanvas c)
        {
            c.Fill(Poly(3f, 9, 7, 39, 7, 41, 11, 39, 29, 24, 43, 9, 29, 7, 11), Steel);
            c.Fill(Poly(2f, 14, 12, 34, 12, 35, 15, 33, 27, 24, 36, 15, 27, 13, 15), Dark, 0.15f);
            float[] rivets = { 11, 10, 37, 10, 11, 27, 37, 27, 24, 39.5f };
            for (int i = 0; i < rivets.Length; i += 2)
                c.Fill(Circle(rivets[i], rivets[i + 1], 1.7f), Steel, 0.25f);
            c.Rect(23, 16, 2, 13, Cyan[3]);
            c.Rect(18, 21, 12, 2, Cyan[3]);
            c.Rect(23, 21, 2, 2, Cyan[4]);
            c.Outline(Outline);
            c.Glow(24, 22, 9, Cyan[3], 0.45f, true);
        }

        static void Atuadores(PixelCanvas c)
        {
            c.Fill(Capsule(22, 6, 25, 22, 4.6f), Steel);
            c.Fill(Tube(30, 9, 30, 30, 1.3f), Copper);
            c.Fill(Capsule(25, 22, 21, 35, 3.9f), Steel);
            c.Fill(Circle(25, 22, 5.2f), Dark, 0.2f);
            c.Fill(Circle(25, 22, 2.2f), Cyan, 0.3f);
            c.Fill(Poly(2f, 14, 34, 30, 34, 37, 39, 37, 43, 12, 43, 12, 38), Dark, 0.1f);
            c.Rect(13, 42, 24, 1, Steel[2]);
            c.Outline(Outline);
            Px line = White.WithAlpha(170);
            c.Rect(3, 14, 10, 1, line);
            c.Rect(6, 20, 8, 1, line.WithAlpha(120));
            c.Rect(2, 27, 11, 1, line);
            c.Rect(5, 33, 6, 1, line.WithAlpha(110));
            c.Glow(25, 22, 7, Cyan[3], 0.5f, true);
        }

        static void Polvora(PixelCanvas c)
        {
            c.Fill(Tube(24, 8, 24, 21, 4.6f), Ice, 0.05f);
            c.Fill(Circle(24, 31, 12.5f), Ice);
            c.Fill(Where(Circle(24, 31, 11f), (x, y) => y > 29f + (float)Math.Sin(x * 0.8f) * 1.2f), Amber, 0.15f);
            c.Fill(Plate(18.5f, 3, 11, 7, 1.5f, 1), Copper);
            c.FillFlat(Ellipse(18.2f, 25, 2.2f, 3.4f), White.WithAlpha(190));
            c.Outline(Outline);
            c.Sparkle(19, 35, 1, Amber[4]);
            c.Sparkle(29, 38, 1, Amber[4]);
            c.Sparkle(26, 32, 1, Fire[4]);
            c.Glow(24, 34, 18, Amber[3], 0.4f);
        }

        static void Tambor(PixelCanvas c)
        {
            c.Fill(Circle(24, 24, 19.5f), Dark, 0.1f);
            c.Fill(Circle(24, 24, 17), Steel);
            for (int i = 0; i < 6; i++)
            {
                double a = (-90 + i * 60) * Math.PI / 180.0;
                float hx = 24 + 10.5f * (float)Math.Cos(a), hy = 24 + 10.5f * (float)Math.Sin(a);
                c.FillFlat(Circle(hx, hy, 4.6f), Dark[0]);
                c.Fill(Circle(hx, hy, 3.4f), Gold);
                c.Fill(Circle(hx, hy, 1.3f), Copper, 0.2f);
                double b = (-60 + i * 60) * Math.PI / 180.0;
                c.FillFlat(Circle(24 + 18.3f * (float)Math.Cos(b), 24 + 18.3f * (float)Math.Sin(b), 1.6f), Dark[0]);
            }

            c.Fill(Circle(24, 24, 3.6f), Steel, 0.25f);
            c.FillFlat(Circle(24, 24, 1.5f), Dark[0]);
            c.Outline(Outline);
        }

        static void Resfriador(PixelCanvas c)
        {
            c.Fill(Tube(24, 10, 24, 38, 9f), Ice);
            for (int y = 15; y <= 33; y += 6)
                c.Rect(16, y, 17, 1, Ice[1]);
            c.Line(24, 18, 24, 30, White);
            c.Line(18, 24, 30, 24, White);
            c.Line(20, 20, 28, 28, White);
            c.Line(28, 20, 20, 28, White);
            c.Fill(Plate(13, 5, 22, 7, 2, 1), Steel);
            c.Fill(Plate(13, 37, 22, 7, 2, 1), Steel);
            c.Outline(Outline);
            c.Sparkle(8, 12, 2, Ice[4]);
            c.Sparkle(40, 30, 1, Ice[4]);
            c.Sparkle(39, 8, 1, Ice[4]);
            c.FillFlat(Circle(18, 2.5f, 1.6f), White.WithAlpha(120));
            c.FillFlat(Circle(28, 1.8f, 1.2f), White.WithAlpha(90));
            c.Glow(24, 24, 23, Ice[3], 0.35f);
        }

        static void CanoCurto(PixelCanvas c)
        {
            c.Fill(Poly(2f, 3, 21, 14, 18, 16, 31, 8, 41, 2, 39), Wood);
            c.Fill(Tube(12, 20, 38, 20, 4.3f), Dark, 0.1f);
            c.Fill(Tube(12, 29, 38, 29, 4.3f), Dark, 0.1f);
            c.Fill(Plate(20, 14, 5, 21, 1.5f), Copper);
            c.Fill(Ellipse(38, 20, 2.2f, 4.3f), Steel);
            c.FillFlat(Ellipse(38.6f, 20, 1f, 2.3f), Outline);
            c.Fill(Ellipse(38, 29, 2.2f, 4.3f), Steel);
            c.FillFlat(Ellipse(38.6f, 29, 1f, 2.3f), Outline);
            c.Outline(Outline);
            c.Glow(43, 24.5f, 6, Fire[3], 0.8f);
            c.Sparkle(44, 17, 2, Fire[4]);
            c.Sparkle(45, 32, 1, Fire[4]);
        }

        static void Soro(PixelCanvas c)
        {
            c.Fill(Tube(24, 10, 24, 40, 8f), Ice);
            c.Fill(Tube(24, 19, 24, 40, 6.8f), Bio, 0.05f);
            c.FillFlat(Ellipse(24, 19, 6.8f, 1.4f), Bio[4]);
            c.FillFlat(Circle(20, 23, 1.1f), Bio[4]);
            c.FillFlat(Circle(28, 37, 1.3f), Bio[4]);
            c.FillFlat(Circle(21, 38, 0.8f), Bio[4]);
            c.Fill(Plate(17, 25, 14, 10, 1, 1), Bone, 0.1f);
            c.Rect(23, 26, 2, 8, Bio[2]);
            c.Rect(20, 29, 8, 2, Bio[2]);
            c.FillFlat(Ellipse(18.6f, 15, 1.2f, 3f), White.WithAlpha(170));
            c.Fill(Plate(15, 4, 18, 7, 2, 1), Steel);
            c.Fill(Plate(16, 40, 16, 5, 1.5f, 1), Steel);
            c.Outline(Outline);
            c.Glow(24, 30, 21, Bio[3], 0.35f);
        }

        static void Ima(PixelCanvas c)
        {
            c.Fill(Ring(24, 22, 17, 9, 180, 360), Red);
            c.Fill(Tube(11, 22, 11, 34, 4f), Red);
            c.Fill(Tube(37, 22, 37, 34, 4f), Red);
            c.Fill(Plate(7, 32, 8, 7, 1.5f), Steel);
            c.Fill(Plate(33, 32, 8, 7, 1.5f), Steel);
            c.Fill(Circle(24, 22, 4.6f), Cyan, 0.15f);
            c.Fill(Facet(-0.4f, -0.5f, 21, 41, 24, 37, 27, 41, 24, 46), Cyan);
            c.Fill(Facet(0.4f, -0.3f, 31, 43, 33.5f, 40, 36, 43, 33.5f, 46.5f), Cyan);
            c.Outline(Outline);
            Px dot = Cyan[3].WithAlpha(200);
            for (int i = 0; i < 3; i++)
            {
                c.Set(16 + i * 2, 40 - i, dot);
                c.Set(31 - i * 2, 40 - i, dot);
            }

            c.Glow(24, 22, 9, Cyan[3], 0.75f, true);
            c.Glow(28, 43, 10, Cyan[3], 0.35f);
        }

        static void Capacitor(PixelCanvas c)
        {
            c.Fill(Plate(17, 5, 5, 7, 1), Steel);
            c.Fill(Plate(26, 5, 5, 7, 1), Steel);
            c.Fill(Plate(12, 10, 24, 33, 3, 3), Dark, 0.1f);
            c.Rect(16, 14, 16, 25, Px.Hex(0x081820));
            for (int i = 0; i < 4; i++)
                c.Rect(18, 33 - i * 6, 12, 4, i < 3 ? Cyan[3] : Cyan[1]);
            c.Fill(Facet(-0.3f, -0.6f, 27, 13, 19, 27, 24, 27, 21, 38, 30, 23, 25, 23, 29, 13), Gold);
            c.Outline(Outline);
            c.Glow(24, 26, 21, Cyan[3], 0.35f);
        }

        static void Bocal(PixelCanvas c)
        {
            c.Fill(Poly(3f, 19, 5, 29, 5, 30, 10, 37, 28, 11, 28, 18, 10), Steel);
            c.Fill(Plate(17, 9, 14, 4, 1), Copper);
            c.Fill(Plate(13, 21, 22, 3, 1), Copper);
            c.FillFlat(Ellipse(24, 28, 13, 2.6f), Outline);
            c.FillFlat(Ellipse(24, 28, 10, 1.4f), BlueFire[3]);
            c.Outline(Outline);
            c.Flame(24, 30, 0, 1, 19, 17, BlueFire, 1.3f);
            c.Glow(24, 36, 12, BlueFire[3], 0.45f);
        }

        static void Lamina(PixelCanvas c)
        {
            c.FillFlat(Poly(0f, 11, 36, 15, 40, 43, 12), Purple[3].WithAlpha(90));
            c.Fill(Poly(1.5f, 14, 31, 18, 35, 45, 4), Cyan, 0.1f);
            c.Line(17, 32, 42, 7, White);
            c.Fill(Capsule(9, 30, 19, 40, 2.5f), Steel);
            c.Fill(Capsule(6, 42, 13, 35, 2.7f), Dark, 0.15f);
            c.Fill(Circle(5, 43, 2.3f), Gold);
            c.Outline(Outline);
        }

        static void Egide(PixelCanvas c)
        {
            float[] hex = RegularPoints(24, 24, 19, 6);
            c.FillFlat(Poly(0f, hex), Cyan[2].WithAlpha(110));
            float[] inner = RegularPoints(24, 24, 12, 6);
            Edges(c, inner, Cyan[3].WithAlpha(170));
            Edges(c, hex, Cyan[4]);
            for (int dx = 0; dx <= 1; dx++)
            {
                c.Polyline(White, 15 + dx, 17, 21 + dx, 24, 15 + dx, 31);
                c.Polyline(White, 24 + dx, 17, 30 + dx, 24, 24 + dx, 31);
            }

            c.Outline(Outline);
            c.Glow(24, 24, 24, Cyan[3], 0.3f);
        }

        // ---------- Incomuns ----------

        static void Bobina(PixelCanvas c)
        {
            c.Fill(Plate(13, 39, 22, 6, 2, 1), Dark, 0.1f);
            c.Fill(Tube(24, 17, 24, 39, 6.5f), Copper);
            for (int y = 18; y < 39; y += 2)
                c.Rect(18, y, 13, 1, Copper[1]);
            c.Fill(Ellipse(24, 15, 12, 4.6f), Steel);
            c.FillFlat(Ellipse(24, 14, 5, 1.5f), Steel[1]);
            c.Fill(Circle(24, 9, 3.2f), Steel, 0.2f);
            c.Outline(Outline);
            var random = new Random(15);
            c.Bolt(random, White, Cyan[3], 22, 8, 12, 6, 4, 13);
            c.Bolt(random, White, Cyan[3], 26, 8, 36, 3, 44, 9);
            c.Bolt(random, White, Cyan[3], 35, 16, 40, 24, 45, 22);
            c.Glow(24, 9, 10, Cyan[3], 0.8f);
        }

        static void Resina(PixelCanvas c)
        {
            c.Fill(Drop(24, 5, 30, 13.5f), Amber);
            c.FillFlat(Circle(29, 34, 2.2f), Amber[1]);
            c.FillFlat(Circle(20, 28, 1.5f), Amber[1]);
            c.Polyline(Px.Hex(0xfff8d0), 25, 21, 27, 27, 23, 32, 26, 37);
            c.FillFlat(Ellipse(18.5f, 26, 2.2f, 4.2f), White.WithAlpha(170));
            c.Outline(Outline);
            c.Glow(24, 30, 21, Amber[3], 0.45f);
            c.Sparkle(10, 16, 2, Fire[4]);
            c.Sparkle(39, 20, 1, Fire[4]);
            c.Sparkle(37, 43, 1, Fire[4]);
        }

        static void Propulsor(PixelCanvas c)
        {
            foreach (float x in new[] { 15f, 33f })
            {
                c.Fill(Capsule(x, 10, x, 31, 5.6f), Steel);
                c.Fill(Poly(1f, x - 4, 33, x + 4, 33, x + 5, 37, x - 5, 37), Dark, 0.1f);
            }

            c.Fill(Plate(18, 12, 12, 20, 2, 2), Dark, 0.15f);
            c.Fill(Circle(24, 20, 2.6f), Cyan, 0.25f);
            c.Outline(Outline);
            c.Flame(15, 37, 0, 1, 9, 10, Fire, 0.4f);
            c.Flame(33, 37, 0, 1, 9, 10, Fire, 2.1f);
            c.Glow(24, 20, 6, Cyan[3], 0.6f, true);
        }

        static void Escudo(PixelCanvas c)
        {
            c.Fill(Circle(24, 24, 19.5f), Steel);
            c.Fill(Circle(24, 24, 15.5f), Teal);
            c.FillFlat(Ring(24, 24, 11.6f, 10.6f), Cyan[4].WithAlpha(210));
            c.FillFlat(Ring(24, 24, 7f, 6f), Cyan[4].WithAlpha(210));
            c.Fill(Circle(24, 24, 3.6f), Cyan, 0.35f);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4.0;
                c.Fill(Circle(24 + 17.5f * (float)Math.Cos(a), 24 + 17.5f * (float)Math.Sin(a), 1.4f), Steel, 0.3f);
            }

            c.Outline(Outline);
            c.Glow(24, 24, 24, Cyan[3], 0.3f);
            c.Glow(24, 24, 7, White, 0.4f, true);
        }

        static void Eco(PixelCanvas c)
        {
            Bullet(c, -16, Purple[3].WithAlpha(70));
            Bullet(c, -8, Purple[3].WithAlpha(120));
            c.Fill(Tube(16, 24, 31, 24, 5.6f), Gold);
            c.Fill(Where(Ellipse(31, 24, 10, 5.6f), (x, y) => x >= 31f), Copper, 0.1f);
            c.Fill(Plate(14, 18, 3, 12, 1), Gold, -0.2f);
            c.Outline(Outline);
            c.Glow(30, 24, 20, Purple[3], 0.3f);
        }

        static void Bullet(PixelCanvas c, float ox, Px color)
        {
            c.FillFlat(Tube(16 + ox, 24, 31 + ox, 24, 5.6f), color);
            c.FillFlat(Where(Ellipse(31 + ox, 24, 10, 5.6f), (x, y) => x >= 31f + ox), color);
        }

        static void Cristal(PixelCanvas c)
        {
            c.Fill(Ellipse(24, 43, 16, 3.6f), Dark, 0.1f);
            c.Fill(Facet(-0.5f, -0.3f, 10, 27, 15, 22, 17, 43, 11, 43), Magenta);
            c.Fill(Facet(0.5f, 0.1f, 38, 25, 33, 21, 31, 43, 37, 43), Magenta);
            c.Fill(Facet(-0.6f, -0.2f, 24, 3, 16, 17, 20, 43, 24, 20), Magenta);
            c.Fill(Facet(0.6f, 0.15f, 24, 3, 32, 17, 28, 43, 24, 20), Magenta);
            c.Fill(Facet(0f, -0.05f, 24, 20, 20, 43, 28, 43), Magenta);
            c.Outline(Outline);
            c.FillFlat(Circle(36, 9, 1.7f), Red[3]);
            c.FillFlat(Circle(39.4f, 9, 1.7f), Red[3]);
            c.FillFlat(Poly(0f, 34.2f, 9.6f, 41.2f, 9.6f, 37.7f, 13.8f), Red[3]);
            c.Sparkle(12, 12, 2, Magenta[4]);
            c.Sparkle(23, 9, 1, White);
            c.Glow(24, 24, 22, Magenta[3], 0.35f);
        }

        static void Injetor(PixelCanvas c)
        {
            c.Fill(Capsule(13, 37, 32, 18, 5.6f), Red);
            c.Fill(Tube(18, 32, 28, 22, 2.7f), Amber, 0.15f);
            c.Fill(Capsule(7, 43, 12, 38, 3.1f), Steel);
            c.Fill(Capsule(31, 19, 35, 15, 3.7f), Steel);
            c.Line(36, 14, 44, 6, Steel[4]);
            c.Line(37, 14, 44, 7, Steel[2]);
            c.Outline(Outline);
            c.Polyline(Red[3], 2, 13, 8, 13, 10, 8, 13, 18, 16, 10, 18, 13, 24, 13);
            c.Polyline(Red[4], 2, 12, 8, 12, 10, 7, 13, 17, 16, 9, 18, 12, 24, 12);
            c.Glow(22, 28, 21, Red[3], 0.3f);
        }

        static void Atordoante(PixelCanvas c)
        {
            c.Fill(Tube(24, 22, 24, 41, 6.6f), Purple);
            c.Fill(Plate(16.5f, 39, 15, 5, 1), Purple, -0.15f);
            c.Fill(Where(Ellipse(24, 22.5f, 6.6f, 11f), (x, y) => y <= 22.5f), Purple, 0.2f);
            c.Fill(Circle(24, 31, 4.2f), Bone, 0.1f);
            c.Line(24, 31, 24, 28, Outline);
            c.Line(24, 31, 26, 31, Outline);
            c.Outline(Outline);
            for (int x = 6; x <= 42; x++)
            {
                if (x % 3 == 0)
                    continue;
                float u = (x - 24) / 17f;
                int y = (int)Math.Round(17 + 4.5f * Math.Sqrt(Math.Max(0f, 1f - u * u)));
                c.Set(x, y, Purple[4].WithAlpha(220));
            }

            c.Sparkle(8, 12, 2, Gold[4]);
            c.Sparkle(40, 15, 2, Gold[4]);
            c.Sparkle(24, 5, 1, Gold[4]);
            c.Glow(24, 20, 19, Purple[3], 0.4f);
        }

        static void Perfurante(PixelCanvas c)
        {
            c.Fill(Plate(20, 5, 7, 38, 2, 1), Steel);
            c.Polyline(Dark[0], 23, 9, 22, 14, 24, 18);
            c.Polyline(Dark[0], 24, 31, 22, 35, 23, 40);
            c.Fill(Tube(4, 24, 19, 24, 4.6f), Gold);
            c.Fill(Plate(2, 19, 3, 10, 0.8f), Gold, -0.2f);
            c.Fill(Tube(27, 24, 36, 24, 4.1f), Copper);
            c.Fill(Poly(1f, 36, 20, 46, 24, 36, 28), Copper, 0.15f);
            c.Outline(Outline);
            c.Glow(27, 24, 9, Fire[3], 0.7f);
            c.Sparkle(30, 17, 1, Fire[4]);
            c.Set(34, 31, Steel[4]);
            c.Set(38, 15, Steel[4]);
            c.Set(41, 33, Steel[3]);
            c.Set(33, 13, Steel[3]);
        }

        static void Cartucho(PixelCanvas c)
        {
            // Eixo do cartucho, de baixo à esquerda para cima à direita
            Func<float, float> ax = t => 11 + 26 * t;
            Func<float, float> ay = t => 37 - 20 * t;
            c.Fill(Capsule(ax(0), ay(0), ax(1), ay(1), 7.6f), Steel);
            c.Fill(Tube(ax(0.16f), ay(0.16f), ax(0.84f), ay(0.84f), 7.7f), Teal);
            c.Fill(Tube(ax(0.44f), ay(0.44f), ax(0.58f), ay(0.58f), 7.8f), Copper);
            c.Fill(Tube(ax(0.66f), ay(0.66f), ax(0.8f), ay(0.8f), 2.2f), Cyan, 0.2f);
            c.Fill(Circle(ax(0) - 1.5f, ay(0) + 1.2f, 2.6f), Cyan, 0.25f);
            c.Outline(Outline);
            // Chamas azuis saindo do lado de cima
            const float nx = -0.6f, ny = -0.8f;
            for (int i = 0; i < 3; i++)
            {
                float t = 0.25f + i * 0.26f;
                c.Flame(ax(t) + nx * 6f, ay(t) + ny * 6f, nx * 0.6f + 0.15f, -1f, 9 - i, 13 + i * 2, BlueFire, i * 1.7f);
            }

            c.Glow(24, 24, 24, BlueFire[3], 0.35f);
        }

        static void Acelerador(PixelCanvas c)
        {
            c.Flame(33, 45, 0, -1, 17, 26, Fire, 0.7f);
            c.Flame(41, 45, 0, -1, 9, 15, Fire, 2.4f);
            var gauge = new PixelCanvas(Size, Size);
            gauge.Fill(Tube(17, 7, 17, 33, 4.2f), Ice);
            gauge.Fill(Circle(17, 7, 4.2f), Ice);
            gauge.Fill(Tube(17, 15, 17, 34, 2.3f), Red, 0.1f);
            gauge.Fill(Circle(17, 37, 7.2f), Red);
            for (int y = 10; y <= 30; y += 4)
                gauge.Rect(22, y, 3, 1, Steel[3]);
            gauge.FillFlat(Ellipse(14.5f, 34.5f, 1.4f, 2f), White.WithAlpha(180));
            gauge.Outline(Outline);
            c.Draw(gauge, 0, 0);
            c.Polyline(Fire[4], 31, 13, 35, 9, 39, 13);
            c.Polyline(Fire[3], 31, 18, 35, 14, 39, 18);
            c.Glow(33, 32, 15, Fire[3], 0.45f);
        }

        // ---------- Raros ----------

        static void Coracao(PixelCanvas c)
        {
            Func<float, float, bool> heart = (x, y) =>
                Inside(Circle(16, 20, 10.5f), x, y) || Inside(Circle(32, 20, 10.5f), x, y) ||
                Inside(Poly(0f, 6.5f, 23, 41.5f, 23, 24, 43), x, y);
            c.Fill(Circle(16, 20, 10.5f), Red);
            c.Fill(Circle(32, 20, 10.5f), Red);
            c.Fill(Poly(2f, 6.5f, 23, 41.5f, 23, 24, 43), Red);
            c.Fill(Where(Plate(4, 9, 16, 26, 2), heart), Steel, -0.05f);
            c.Fill(Circle(9, 18, 1.3f), Steel, 0.3f);
            c.Fill(Circle(15, 29, 1.3f), Steel, 0.3f);
            c.Fill(Tube(20, 12, 18, 2, 2.7f), Red, 0.1f);
            c.Fill(Tube(29, 12, 31, 3, 2.9f), Steel);
            c.Fill(Poly(1f, GearPoints(36, 30, 5.5f, 3.8f, 7)), Gold);
            c.FillFlat(Circle(36, 30, 1.4f), Dark[0]);
            c.FillFlat(Ring(26, 24, 7.2f, 5.6f), Dark[1]);
            c.Fill(Circle(26, 24, 5.6f), Cyan, 0.15f);
            c.Outline(Outline);
            c.FillFlat(Circle(17, 1.8f, 2f), Bone[3].WithAlpha(130));
            c.FillFlat(Circle(34, 1.5f, 1.5f), Bone[3].WithAlpha(110));
            c.Sparkle(42, 11, 1, Gold[4]);
            c.Glow(26, 24, 12, Cyan[3], 0.65f, true);
            c.Glow(24, 24, 24, Red[3], 0.25f);
        }

        static void Amplificador(PixelCanvas c)
        {
            Px beam = White.WithAlpha(150);
            c.Rect(1, 18, 13, 1, beam);
            c.Rect(1, 24, 13, 1, beam);
            c.Rect(1, 30, 13, 1, beam);
            c.Fill(Ellipse(19, 24, 6.2f, 17.5f), Ice);
            c.Fill(Plate(16, 3, 6, 4, 1), Gold);
            c.Fill(Plate(16, 41, 6, 4, 1), Gold);
            c.FillFlat(Ellipse(17, 17, 1.6f, 5f), White.WithAlpha(180));
            c.Outline(Outline);
            c.Line(24, 14, 37, 24, Gold[4]);
            c.Line(24, 34, 37, 24, Gold[4]);
            c.Line(26, 24, 37, 24, White);
            c.FillFlat(Poly(0f, 38, 12, 40.5f, 21.5f, 47.5f, 24, 40.5f, 26.5f, 38, 36, 35.5f, 26.5f, 28.5f, 24, 35.5f, 21.5f),
                Gold[3]);
            c.FillFlat(Poly(0f, 38, 17, 39.5f, 22.5f, 44, 24, 39.5f, 25.5f, 38, 31, 36.5f, 25.5f, 32, 24, 36.5f, 22.5f),
                Gold[4]);
            c.Rect(37, 23, 2, 2, White);
            c.Glow(38, 24, 13, Gold[3], 0.7f);
        }

        static void Missil(PixelCanvas c)
        {
            c.FillFlat(Circle(6, 42, 3.6f), Bone[2].WithAlpha(140));
            c.FillFlat(Circle(2, 46, 2.6f), Bone[2].WithAlpha(100));
            c.Fill(Poly(1f, 10, 30, 3, 29, 7, 37), Red);
            c.Fill(Poly(1f, 18, 38, 19, 45, 11, 41), Red);
            c.Fill(Tube(13, 35, 33, 15, 5.1f), Bone);
            c.Fill(Tube(18, 30, 20.5f, 27.5f, 5.3f), Red);
            c.Fill(Poly(1.5f, 29.4f, 11.4f, 41, 7, 36.6f, 18.6f), Red, 0.05f);
            c.Fill(Circle(27, 21, 2.3f), Cyan, 0.25f);
            c.Outline(Outline);
            c.Flame(11, 37, -0.707f, 0.707f, 8, 11, Fire, 1.1f);
            c.Glow(10, 38, 8, Fire[3], 0.5f);
        }

        static void Ignicao(PixelCanvas c)
        {
            c.Flame(24, 19, 0, -1, 15, 18, Fire, 0.3f);
            c.Flame(20, 17, -0.8f, -0.6f, 8, 13, Fire, 1.7f);
            c.Flame(28, 17, 0.8f, -0.6f, 8, 13, Fire, 2.9f);
            var plug = new PixelCanvas(Size, Size);
            plug.Fill(Tube(24, 40, 24, 46, 3.3f), Steel);
            for (int y = 41; y <= 45; y += 2)
                plug.Rect(21, y, 7, 1, Steel[1]);
            plug.Fill(Plate(16, 34, 16, 6, 1.5f), Steel);
            plug.Fill(Tube(24, 21, 24, 34, 5.1f), Bone);
            plug.Rect(19, 25, 11, 1, Bone[1]);
            plug.Rect(19, 29, 11, 1, Bone[1]);
            plug.Fill(Tube(24, 16, 24, 21, 2f), Steel);
            plug.Outline(Outline);
            c.Draw(plug, 0, 0);
            c.Glow(24, 12, 11, Fire[3], 0.45f);
        }

        static void Camara(PixelCanvas c)
        {
            c.Fill(Plate(19, 19, 10, 10, 1.5f), Copper);
            foreach (float x in new[] { 14f, 34f })
            {
                c.Fill(Capsule(x, 11, x, 37, 6.6f), Steel);
                c.Fill(Tube(x, 16, x, 32, 3.3f), Cyan, 0.2f);
                c.Rect((int)x - 4, 23, 9, 1, Steel[1]);
            }

            c.Polyline(Gold[4], 23, 21, 26, 24, 22, 25, 25, 28);
            c.Outline(Outline);
            c.Glow(14, 24, 10, Cyan[3], 0.5f);
            c.Glow(34, 24, 10, Cyan[3], 0.5f);
        }

        // ---------- utilidades ----------

        static bool Inside(Shape shape, float x, float y) => shape(x, y, out _, out _);

        static void Edges(PixelCanvas c, float[] xy, Px color)
        {
            int n = xy.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                c.Line((int)Math.Round(xy[i * 2]), (int)Math.Round(xy[i * 2 + 1]), (int)Math.Round(xy[j * 2]),
                    (int)Math.Round(xy[j * 2 + 1]), color);
            }
        }
    }
}
