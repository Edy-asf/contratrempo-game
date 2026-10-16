using System;
using static Icaro.PixelCanvas;
using static Icaro.PixelRamps;

namespace Icaro
{
    // Sprites das armas em primeira pessoa (estilo Doom, mas com a arma no canto inferior direito), desenhados em
    // código: cada arma é montada de peças em coordenadas locais (empunhadura em (0, 0), cano para -x) e girada para
    // apontar ao centro da tela, segurada pela mão robótica do ÍCARO-9 (braço saindo do canto) e, nas armas longas,
    // pela mão de apoio. Os quadros são poses (WeaponPose): parada, tiro (coice e clarão) e recarga (pente saindo e
    // entrando com a mão de apoio; nos revólveres, o tambor abre e as células caem). Braços, arma e mãos são
    // desenhados em camadas com contorno próprio, para as peças não se fundirem. Não usa UnityEngine (prévias fora do
    // Unity); o WeaponSpriteHUD converte em Sprite.
    public static class WeaponSpritePainter
    {
        public const int Width = 224;
        public const int Height = 160;
        public const int ReloadFrames = 8;

        public static readonly string[] Keys = { "Agulha", "Titan", "IonAkimbos", "Viper", "Liberdade" };

        static readonly Px k_Glass = Px.Hex(0x9ad8f0, 170);

        class Spec
        {
            public float OriginX, OriginY, Angle;
            public float MuzzleX, MuzzleY, AltMuzzleX, AltMuzzleY;
            public bool Support;
            public float SupportX, SupportY;
            public float MagBottomX, MagBottomY;
            public bool Revolver;
            public Action<SpriteRig, WeaponPose> Gun;
            public Action<SpriteRig, WeaponPose> Fx;
            public Px[] FlashRamp;
            public float FlashSize = 1f;
            public bool Suppressed;
        }

        // ---------- quadros ----------

        public static WeaponPose IdlePose() => default;

        public static WeaponPose FirePose(int frame, bool alt = false, float heat = 0f) => new WeaponPose
        {
            Kick = frame == 0 ? 4f : 2f, Flash = frame == 0 ? 1 : 2, Alt = alt, Heat = heat,
        };

        // Pose da recarga no instante t (0 a 1)
        public static WeaponPose ReloadPose(string key, float t)
        {
            var pose = new WeaponPose();
            Spec spec = SpecOf(key);
            if (spec != null && spec.Revolver)
            {
                // Levanta o cano, abre o tambor, as células gastas caem, as novas acendem, fecha e volta
                pose.Angle = 32f * Seg(t, 0f, 0.15f) * (1f - Seg(t, 0.85f, 1f));
                pose.Dy = -4f * Seg(t, 0f, 0.15f) * (1f - Seg(t, 0.85f, 1f));
                pose.Open = Seg(t, 0.15f, 0.3f) * (1f - Seg(t, 0.72f, 0.85f));
                pose.Eject = t >= 0.3f && t < 0.58f ? Seg(t, 0.3f, 0.58f) : -1f;
                pose.Charge = Seg(t, 0.55f, 0.72f);
                return pose;
            }

            // Inclina, o pente cai, a mão busca outro, encaixa, dá um tapa e volta
            float down = Seg(t, 0f, 0.15f) * (1f - Seg(t, 0.82f, 1f));
            pose.Angle = -10f * down + 4f * Seg(t, 0.7f, 0.82f) * (1f - Seg(t, 0.82f, 1f));
            pose.Dy = 6f * down;
            if (t < 0.35f)
            {
                pose.MagDrop = Seg(t, 0.15f, 0.35f);
                pose.HandAway = 0.3f * Seg(t, 0.15f, 0.35f);
            }
            else if (t < 0.5f)
            {
                pose.MagGone = true;
                pose.HandAway = 0.3f + 0.7f * Seg(t, 0.35f, 0.5f);
            }
            else if (t < 0.7f)
            {
                pose.HandOnMag = true;
                pose.MagDrop = 1f - Seg(t, 0.5f, 0.7f);
            }

            pose.Kick = t >= 0.7f && t < 0.82f ? 2f : 0f;
            return pose;
        }

        static float Seg(float t, float a, float b) => t <= a ? 0f : t >= b ? 1f : (t - a) / (b - a);

        // Ponta do cano no quadro (pixels do sprite, origem no alto à esquerda): de onde o tiro sai na tela
        public static bool MuzzlePoint(string key, WeaponPose pose, out float x, out float y)
        {
            x = y = 0f;
            Spec s = SpecOf(key);
            if (s == null)
                return false;
            float angle = s.Angle + pose.Angle + pose.Kick * 0.6f;
            double radians = angle * Math.PI / 180.0;
            float ox = s.OriginX + pose.Dx + (float)Math.Cos(radians) * pose.Kick;
            float oy = s.OriginY + pose.Dy + (float)Math.Sin(radians) * pose.Kick;
            new SpriteRig(null, ox, oy, angle).ToCanvas(pose.Alt ? s.AltMuzzleX : s.MuzzleX,
                pose.Alt ? s.AltMuzzleY : s.MuzzleY, out x, out y);
            return true;
        }

        // ---------- desenho ----------

        public static PixelCanvas Paint(string key, WeaponPose pose)
        {
            Spec s = SpecOf(key);
            if (s == null)
                return null;

            float angle = s.Angle + pose.Angle + pose.Kick * 0.6f;
            double radians = angle * Math.PI / 180.0;
            float backX = (float)Math.Cos(radians), backY = (float)Math.Sin(radians);
            float ox = s.OriginX + pose.Dx + backX * pose.Kick;
            float oy = s.OriginY + pose.Dy + backY * pose.Kick;

            var arms = new PixelCanvas(Width, Height);
            var gunLayer = new PixelCanvas(Width, Height);
            var hands = new PixelCanvas(Width, Height);
            var gun = new SpriteRig(gunLayer, ox, oy, angle);
            var handRig = new SpriteRig(hands, ox, oy, angle);

            // Braço direito, do pulso até fora da tela
            gun.ToCanvas(18f, 15f, out float wx, out float wy);
            Arm(arms, wx, wy, Width + 24f, Height + 30f, 10f);

            // Mão de apoio: no ponto de apoio da arma ou no pente (trazendo o novo); afastada desce para fora da tela
            SpriteRig support = null;
            float sx = s.SupportX, sy = s.SupportY;
            if (s.Support)
            {
                if (pose.HandOnMag)
                {
                    sx = s.MagBottomX;
                    sy = s.MagBottomY + pose.MagDrop * 55f;
                }

                support = new SpriteRig(hands, ox + pose.HandAway * 18f, oy + pose.HandAway * 80f, angle);
                support.ToCanvas(sx, sy + 12f, out float px, out float py);
                Arm(arms, px, py, -14f, Height + 64f, 9f);
            }

            s.Gun(gun, pose);
            RightHand(handRig);
            if (support != null && pose.HandAway < 0.95f)
                SupportHand(support, sx, sy);

            arms.Outline(Outline);
            gunLayer.Outline(Outline);
            hands.Outline(Outline);
            var result = new PixelCanvas(Width, Height);
            result.Draw(arms, 0, 0);
            result.Draw(gunLayer, 0, 0);
            result.Draw(hands, 0, 0);

            var fx = new SpriteRig(result, ox, oy, angle);
            s.Fx?.Invoke(fx, pose);
            if (pose.Flash > 0)
                MuzzleFlash(fx, s, pose);
            return result;
        }

        // Clarão do cano (ou fumaça, com silenciador)
        static void MuzzleFlash(SpriteRig r, Spec s, WeaponPose pose)
        {
            float mx = pose.Alt ? s.AltMuzzleX : s.MuzzleX, my = pose.Alt ? s.AltMuzzleY : s.MuzzleY;
            float k = (pose.Flash == 1 ? 1f : 0.55f) * s.FlashSize;
            if (s.Suppressed)
            {
                Px smoke = Bone[3].WithAlpha(pose.Flash == 1 ? 150 : 90);
                r.Flat(Circle(mx - 5f, my, 3.5f * k + 1f), smoke);
                r.Flat(Circle(mx - 10f, my - 2f, 2.5f * k + 1f), smoke.WithAlpha(smoke.A / 2));
                r.Glow(mx - 2f, my, 4f, Fire[3], 0.5f * k, true);
                return;
            }

            r.Glow(mx - 6f * k, my, 20f * k, s.FlashRamp[3], 0.55f);
            r.Flame(mx + 1f, my, -1f, 0f, 14f * k, 26f * k, s.FlashRamp, pose.Flash * 1.7f);
            r.Flame(mx - 2f, my, -0.35f, -1f, 7f * k, 11f * k, s.FlashRamp, 2.3f);
            r.Flame(mx - 2f, my, -0.35f, 1f, 7f * k, 11f * k, s.FlashRamp, 4.1f);
            r.Glow(mx - 3f, my, 6f * k, White, 0.9f, true);
        }

        // ---------- mão e braço do ÍCARO-9 ----------

        // Braço robótico: cano grosso com placas, pistão e faixa ciano
        static void Arm(PixelCanvas layer, float x0, float y0, float x1, float y1, float thickness)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            var arm = new SpriteRig(layer, x0, y0, (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI));
            arm.Tube(0f, 0f, length, 0f, thickness, Dark, 0.05f);
            arm.Tube(14f, thickness * 0.55f, length, thickness * 0.55f, 2f, Copper);
            arm.Plate(6f, -thickness, 30f, thickness * 1.25f, 2f, Steel, 0.05f, 2f);
            arm.Plate(40f, -thickness, 30f, thickness * 1.25f, 2f, Steel, 0f, 2f);
            arm.FlatRect(6f, thickness * 0.2f, length - 6f, 1.5f, Cyan[2]);
            arm.Ring(2f, 0f, thickness + 1f, thickness - 2f, Dark, 0.2f);
        }

        // Mão direita na empunhadura (coordenadas da arma): dorso, dedos fechados na frente do cabo, polegar
        static void RightHand(SpriteRig r)
        {
            r.Capsule(14f, 8f, 21f, 17f, 6.5f, Dark, 0.05f);
            r.Poly(2f, Dark, 0.1f, 1f, -13f, 16f, -11f, 21f, 8f, 9f, 15f, -1f, 6f);
            r.Plate(5f, -10f, 11f, 6f, 1.5f, Steel, 0.1f, 1f);
            r.Plate(7f, -3f, 11f, 6f, 1.5f, Steel, 0f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float y = -4f + i * 6f;
                r.Capsule(1f + i, y, -7f + i, y + 2f, 2.7f, Dark, 0.15f);
                r.FlatRect(-8f + i, y + 1f, 1.5f, 1.5f, Cyan[3]);
            }

            r.Capsule(6f, -13f, -7f, -16f, 2.5f, Steel, 0.1f);
            r.FlatRect(15f, 4f, 2f, 2f, Cyan[3]);
        }

        // Mão de apoio por baixo da arma (coordenadas da arma): palma e três dedos subindo pelo lado
        static void SupportHand(SpriteRig r, float x, float y)
        {
            r.Poly(2f, Dark, 0.05f, x - 9f, y + 1f, x + 9f, y + 1f, x + 7f, y + 13f, x - 7f, y + 13f);
            r.Plate(x - 6f, y + 4f, 12f, 6f, 1.5f, Steel, 0.05f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float fx = x - 6f + i * 5f;
                r.Capsule(fx, y + 3f, fx - 1f, y - 6f, 2.4f, Dark, 0.15f);
                r.FlatRect(fx - 2f, y - 7f, 1.5f, 1.5f, Cyan[3]);
            }
        }

        // ---------- armas ----------

        static Spec SpecOf(string key)
        {
            switch (key)
            {
                case "Agulha":
                    return new Spec
                    {
                        OriginX = 168f, OriginY = 108f, Angle = 20f, MuzzleX = -144f, MuzzleY = -24f,
                        Support = true, SupportX = -64f, SupportY = -16f, MagBottomX = -20f, MagBottomY = 16f,
                        Gun = Agulha, Fx = AgulhaFx, FlashRamp = Cyan,
                    };
                case "Titan":
                    return new Spec
                    {
                        OriginX = 172f, OriginY = 112f, Angle = 17f, MuzzleX = -134f, MuzzleY = -24f,
                        Support = true, SupportX = -71f, SupportY = 4f, MagBottomX = -23f, MagBottomY = 12f,
                        Gun = Titan, Fx = TitanFx, FlashRamp = Fire, FlashSize = 1.25f,
                    };
                case "IonAkimbos":
                    return new Spec
                    {
                        OriginX = 180f, OriginY = 118f, Angle = 16f, MuzzleX = -80f, MuzzleY = -21f,
                        Revolver = true, Gun = Revolver, Fx = RevolverFx, FlashRamp = BlueFire, FlashSize = 0.9f,
                    };
                case "Viper":
                    return new Spec
                    {
                        OriginX = 172f, OriginY = 108f, Angle = 20f, MuzzleX = -110f, MuzzleY = -23f,
                        Support = true, SupportX = -47f, SupportY = 4f, MagBottomX = -21f, MagBottomY = 27f,
                        Gun = Viper, Fx = ViperFx, FlashRamp = Fire, Suppressed = true,
                    };
                case "Liberdade":
                    return new Spec
                    {
                        OriginX = 170f, OriginY = 110f, Angle = 18f, MuzzleX = -132f, MuzzleY = -21f,
                        AltMuzzleX = -130f, AltMuzzleY = -31f, Support = true, SupportX = -96f, SupportY = -16f,
                        MagBottomX = -58f, MagBottomY = 8f, Gun = Liberdade, FlashRamp = Fire, FlashSize = 1.35f,
                    };
                default:
                    return null;
            }
        }

        static void Grip(SpriteRig r, Px[] ramp)
        {
            r.Poly(2f, ramp, 0f, -5f, -11f, 6f, -11f, 11f, 16f, -1f, 18f);
            r.Ring(-11f, -3f, 6.5f, 4.8f, Dark, 0.1f);
            r.Capsule(-7f, -6f, -9f, 1f, 1.4f, Steel, 0.2f);
        }

        static void Agulha(SpriteRig r, WeaponPose p)
        {
            // Coronha vazada
            r.Poly(2f, Steel, 0f, 8f, -27f, 56f, -23f, 60f, -10f, 56f, 5f, 46f, 7f, 42f, -6f, 14f, -10f);
            r.Clear(Poly(0f, 22f, -20f, 46f, -18f, 48f, -10f, 24f, -12f));
            r.Plate(52f, -23f, 8f, 29f, 1.5f, Dark, 0f, 1f);
            Grip(r, Dark);
            // Pente translúcido com as agulhas (cai na recarga)
            if (!p.MagGone)
            {
                float d = p.MagDrop * 55f;
                r.Fill(Poly(1.5f, -31f, -12f + d, -16f, -12f + d, -12f, 15f + d, -25f, 17f + d), Ice, 0.05f, 210);
                for (int i = 0; i < 3; i++)
                    r.Line(-27f + i * 4f, -9f + d, -22f + i * 4f, 12f + d, Cyan[3]);
            }

            // Corpo, trilho e janela de ejeção
            r.Plate(-44f, -33f, 56f, 21f, 3f, Steel, 0f, 2f);
            r.Plate(-38f, -37f, 42f, 4f, 1f, Dark, 0.1f);
            r.Plate(-14f, -28f, 13f, 6f, 1f, Dark, -0.2f);
            r.FlatRect(-12f, -26f, 9f, 1.5f, Cyan[3]);
            // Mira holográfica
            r.Plate(-30f, -45f, 15f, 8f, 1.5f, Dark, 0.1f, 1f);
            // Guarda-mão com respiros
            r.Plate(-86f, -32f, 44f, 17f, 2f, Dark, 0.05f, 2f);
            for (int i = 0; i < 4; i++)
                r.FlatRect(-80f + i * 9f, -28f, 2.5f, 8f, Cyan[2]);
            // Cano agulha com anéis de foco
            r.Tube(-86f, -24f, -130f, -24f, 3f, Steel, 0.1f);
            foreach (float x in new[] { -100f, -116f })
            {
                r.Plate(x, -30f, 5f, 12f, 1f, Steel, 0.05f);
                r.FlatRect(x + 1.5f, -29f, 2f, 1.5f, Cyan[4]);
            }

            r.Poly(1f, Steel, 0.2f, -130f, -27f, -145f, -24f, -130f, -21f);
        }

        static void AgulhaFx(SpriteRig r, WeaponPose p)
        {
            // Losango da mira holográfica flutuando
            r.Flat(Poly(0f, -22f, -57f, -18f, -52f, -22f, -47f, -26f, -52f), Cyan[3].WithAlpha(200));
            r.Flat(Poly(0f, -22f, -55f, -20f, -52f, -22f, -49f, -24f, -52f), Cyan[4]);
            r.Glow(-22f, -52f, 8f, Cyan[3], 0.4f);
            r.Glow(-145f, -24f, 5f, Cyan[3], 0.5f);
        }

        static void Titan(SpriteRig r, WeaponPose p)
        {
            // Coronha maciça
            r.Plate(10f, -31f, 42f, 27f, 3f, Dark, 0.05f, 3f);
            r.Plate(50f, -32f, 7f, 30f, 1.5f, Dark, -0.25f, 2f);
            r.Plate(16f, -27f, 26f, 7f, 1.5f, Copper, 0f, 1f);
            Grip(r, Dark);
            // Caixa de munição com a fita entrando no corpo
            if (!p.MagGone)
            {
                float d = p.MagDrop * 55f;
                r.Plate(-38f, -12f + d, 28f, 24f, 2f, Copper, 0f, 2f);
                r.FlatRect(-34f, -6f + d, 20f, 2f, Copper[1]);
                for (int i = 0; i < 4; i++)
                    r.Plate(-33f + i * 5f, -17f + d * 0.6f, 4f, 7f, 1f, Gold, 0.1f);
            }

            // Corpo com placa laranja e alça
            r.Plate(-48f, -40f, 62f, 32f, 3f, Dark, 0f, 3f);
            r.Plate(-42f, -37f, 32f, 14f, 2f, Copper, 0.05f, 2f);
            r.Capsule(-34f, -47f, -8f, -47f, 2.4f, Steel, 0.1f);
            r.Plate(-32f, -46f, 3f, 7f, 1f, Steel);
            r.Plate(-12f, -46f, 3f, 7f, 1f, Steel);
            // Camisa de refrigeração com aletas (esquentam ao atirar)
            r.Tube(-48f, -24f, -114f, -24f, 9f, Dark, 0.05f);
            Px[] fins = p.Heat > 0.66f ? Fire : p.Heat > 0.33f ? Copper : Steel;
            for (float x = -54f; x >= -108f; x -= 6f)
                r.Plate(x, -34f, 3f, 20f, 0.8f, fins, 0.05f);
            r.Tube(-114f, -24f, -126f, -24f, 5f, Steel, 0.1f);
            r.Plate(-133f, -31f, 8f, 14f, 1.5f, Dark, 0.1f, 1f);
            // Empunhadura vertical
            r.Capsule(-70f, -14f, -72f, 7f, 3.6f, Dark, 0.1f);
        }

        static void TitanFx(SpriteRig r, WeaponPose p)
        {
            if (p.Heat > 0.33f)
                r.Glow(-80f, -24f, 32f, Fire[3], 0.25f * p.Heat);
        }

        static void Revolver(SpriteRig r, WeaponPose p)
        {
            Grip(r, Dark);
            r.Plate(-2f, -9f, 6f, 20f, 1f, Silver, -0.1f);
            // Armação, cão e cano sobre o tambor
            r.Plate(-38f, -24f, 44f, 17f, 2f, Silver, -0.12f, 2f);
            r.Poly(1f, Silver, 0.1f, 2f, -24f, 8f, -31f, 12f, -29f, 7f, -21f);
            r.Plate(-76f, -27f, 42f, 9f, 1.5f, Silver, -0.05f, 1f);
            r.Plate(-72f, -18f, 34f, 5f, 1f, Dark, 0f);
            r.FlatRect(-72f, -26f, 32f, 1.5f, Purple[3]);
            r.Plate(-80f, -29f, 6f, 13f, 1f, Dark, 0.1f, 1f);

            // Tambor: fechado, de lado com três células; aberto, desce e mostra a face com as 8 câmaras
            if (p.Open <= 0.05f)
            {
                r.Plate(-33f, -23f, 22f, 16f, 2f, Silver, -0.1f, 4f);
                for (int i = 0; i < 3; i++)
                    r.Plate(-31f + i * 7f, -19f, 5f, 8f, 1f, Purple, 0.2f, 1f);
            }
            else
            {
                float cx = -22f - p.Open * 6f, cy = -15f + p.Open * 15f;
                r.Ellipse(cx, cy, 7f + p.Open * 3f, 9f, Silver, 0.05f);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    float hx = cx + (float)Math.Cos(a) * 4.5f, hy = cy + (float)Math.Sin(a) * 5.5f;
                    r.Flat(Circle(hx, hy, 1.6f), p.Charge > i / 8f ? Purple[4] : Dark[0]);
                }
            }

            if (p.Eject >= 0f && p.Eject < 1f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float fall = p.Eject * (38f + i * 6f);
                    r.Capsule(-26f + i * 3f, -10f + fall, -24f + i * 3f, -6f + fall, 1.3f, Steel, -0.1f);
                }
            }
        }

        static void RevolverFx(SpriteRig r, WeaponPose p)
        {
            if (p.Open <= 0.05f)
                r.Glow(-22f, -15f, 9f, Purple[3], 0.35f);
            else if (p.Charge > 0f)
                r.Glow(-22f - p.Open * 6f, -15f + p.Open * 15f, 12f, Purple[3], 0.5f * p.Charge);
            r.Glow(-80f, -22f, 6f, BlueFire[3], 0.4f);
        }

        static void Viper(SpriteRig r, WeaponPose p)
        {
            // Coronha de arame dobrável
            r.Line(8f, -21f, 36f, -18f, Steel[2], 2.5f);
            r.Line(36f, -18f, 38f, -4f, Steel[2], 2.5f);
            r.Line(38f, -4f, 12f, -7f, Steel[2], 2.5f);
            Grip(r, Dark);
            // Pente helicoidal (cai na recarga)
            if (!p.MagGone)
            {
                float d = p.MagDrop * 55f;
                r.Capsule(-29f, -12f + d, -21f, 26f + d, 6f, Dark, 0.05f);
                for (int i = 0; i < 7; i++)
                    r.Line(-33f + i * 1.2f, -8f + i * 5f + d, -19f + i * 1.2f, -10f + i * 5f + d, Bio[2], 1.2f);
            }

            // Corpo com faixa verde, mira compacta
            r.Plate(-42f, -31f, 50f, 19f, 2f, Dark, 0.1f, 2f);
            r.FlatRect(-38f, -20f, 40f, 2f, Bio[3]);
            r.Plate(-28f, -39f, 13f, 8f, 1f, Dark, 0.15f, 1f);
            r.Flat(Circle(-22f, -35f, 1.8f), Bio[4]);
            // Cano curto e silenciador
            r.Tube(-42f, -23f, -56f, -23f, 3f, Steel, 0.1f);
            r.Tube(-56f, -23f, -106f, -23f, 6.5f, Dark, 0.1f);
            for (float x = -62f; x >= -100f; x -= 12f)
                r.Plate(x, -30f, 3f, 14f, 0.8f, Steel, 0f);
            for (float x = -68f; x >= -98f; x -= 12f)
                r.Flat(Circle(x, -21f, 1.2f), Outline);
            r.Plate(-110f, -30f, 5f, 14f, 1f, Steel, 0.1f, 1f);
            // Empunhadura vertical
            r.Capsule(-46f, -14f, -48f, 5f, 3.2f, Dark, 0.1f);
        }

        static void ViperFx(SpriteRig r, WeaponPose p) => r.Glow(-22f, -35f, 5f, Bio[3], 0.5f);

        static void Liberdade(SpriteRig r, WeaponPose p)
        {
            // Coronha escura com friso dourado
            r.Plate(10f, -30f, 46f, 24f, 3f, Dark, 0.05f, 4f);
            r.FlatRect(14f, -26f, 38f, 1.5f, Gold[3]);
            Grip(r, Dark);
            // Dois canos lado a lado: o de trás aparece mais alto e mais escuro
            r.Tube(-44f, -31f, -128f, -31f, 5f, Gold, -0.2f);
            r.Tube(-44f, -21f, -130f, -21f, 5.5f, Gold, 0f);
            r.Plate(-120f, -36f, 6f, 20f, 1f, Dark, 0.1f);
            r.Flat(Ellipse(-129f, -31f, 1.5f, 3f), Outline);
            r.Flat(Ellipse(-131f, -21f, 1.6f, 3.4f), Outline);
            // Tambor de cartuchos sob os canos (sai na recarga)
            if (!p.MagGone)
            {
                float d = p.MagDrop * 55f;
                r.Circle(-58f, -5f + d, 13f, Dark, 0.05f);
                r.Ring(-58f, -5f + d, 13f, 10.5f, Gold, 0f);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3.0 + 0.3;
                    r.Circle(-58f + (float)Math.Cos(a) * 6f, -5f + d + (float)Math.Sin(a) * 6f, 2.2f, Red, 0.1f);
                }
            }

            // Corpo dourado com placas escuras
            r.Plate(-46f, -37f, 60f, 29f, 3f, Gold, 0f, 3f);
            r.Plate(-40f, -33f, 22f, 12f, 2f, Dark, 0.05f, 1f);
            r.Plate(-14f, -33f, 18f, 6f, 1f, Dark, -0.1f);
            r.Plate(-38f, -43f, 30f, 6f, 1f, Dark, 0.1f, 1f);
        }
    }
}
