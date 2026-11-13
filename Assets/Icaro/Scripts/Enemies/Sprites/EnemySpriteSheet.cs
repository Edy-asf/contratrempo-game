using System.Globalization;
using System.Text;

namespace Icaro
{
    // Monta a folha de sprites de um inimigo desenhado em código (EnemyArt): uma linha por clipe e direção, no
    // sentido horário visto de cima (4 direções: frente, direita, costas, esquerda = espelho da direita; 8 direções:
    // frente, frente-direita, direita, costas-direita, costas, e as três do lado esquerdo espelhadas; a linha 0 é a
    // de cima) e uma coluna por quadro, com contorno
    // escuro. Gera também o mapa de brilho (as partes que brilham na cor cheia; o resto na cor vezes BodyGlow) e a
    // descrição JSON no formato do LEIAME.md de Art/Sprites/Inimigos. Sem UnityEngine (prévias fora do Unity).
    public sealed class EnemySpriteSheet
    {
        // Quanto do próprio corpo brilha no mapa de brilho (o brilho mínimo no escuro)
        public const float BodyGlow = 0.25f;

        public readonly EnemyArt Art;
        public readonly PixelCanvas Color;
        public readonly PixelCanvas Emission;
        public readonly int Columns;
        public readonly int Rows;

        public EnemySpriteSheet(EnemyArt art)
        {
            Art = art;
            EnemyClipSpec[] clips = art.Clips;
            int dirs = Directions = art.Directions == 8 ? 8 : 4;
            Rows = clips.Length * dirs;
            foreach (EnemyClipSpec clip in clips)
                Columns = clip.Frames > Columns ? clip.Frames : Columns;

            int w = art.Width, h = art.Height;
            Color = new PixelCanvas(w * Columns, h * Rows);
            Emission = new PixelCanvas(w * Columns, h * Rows);
            for (int c = 0; c < clips.Length; c++)
            {
                for (int d = 0; d < dirs; d++)
                {
                    // Mesma vista em todas as direções: copia a linha da frente
                    if (art.SameAllDirections && d > 0)
                    {
                        CopyRow(c * dirs * h, (c * dirs + d) * h, h);
                        continue;
                    }

                    ViewFor(d, dirs, out EnemyView view, out bool mirror);
                    for (int f = 0; f < clips[c].Frames; f++)
                    {
                        var cell = new PixelCanvas(w, h) { Dither = 0.3f };
                        var mask = new PixelCanvas(w, h);
                        art.DrawFrame(cell, mask, view, clips[c], f);
                        if (!art.OwnOutline)
                            cell.Outline(EnemyRamps.Outline);
                        Copy(cell, mask, f * w, (c * dirs + d) * h, mirror);
                    }
                }
            }
        }

        // Direções por clipe (4 ou 8)
        public readonly int Directions;

        // Vista e espelho da direção d (sentido horário a partir da frente); o lado esquerdo espelha o direito
        public static void ViewFor(int d, int dirs, out EnemyView view, out bool mirror)
        {
            int half = dirs / 2;
            mirror = d > half;
            int k = mirror ? dirs - d : d; // 0 frente .. half costas
            if (dirs == 8)
                view = k == 0 ? EnemyView.Front : k == 1 ? EnemyView.FrontSide : k == 2 ? EnemyView.Side
                    : k == 3 ? EnemyView.BackSide : EnemyView.Back;
            else
                view = k == 0 ? EnemyView.Front : k == 1 ? EnemyView.Side : EnemyView.Back;
        }

        void CopyRow(int fromY, int toY, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < Color.Width; x++)
                {
                    Color.Pixels[(toY + y) * Color.Width + x] = Color.Pixels[(fromY + y) * Color.Width + x];
                    Emission.Pixels[(toY + y) * Color.Width + x] = Emission.Pixels[(fromY + y) * Color.Width + x];
                }
            }
        }

        void Copy(PixelCanvas cell, PixelCanvas mask, int ox, int oy, bool mirror)
        {
            int w = cell.Width;
            for (int y = 0; y < cell.Height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sx = mirror ? w - 1 - x : x;
                    Px c = cell.Get(sx, y);
                    if (c.A < 128)
                        continue;
                    c.A = 255;
                    Color.Set(ox + x, oy + y, c);
                    bool glows = mask.Get(sx, y).A > 0;
                    Px e = glows ? c : c.Scaled(BodyGlow);
                    e.A = 255;
                    Emission.Set(ox + x, oy + y, e);
                }
            }
        }

        // Descrição da folha (o EnemySpriteImporter lê); emission = nome do arquivo do mapa de brilho
        public string Json(string emission)
        {
            var inv = CultureInfo.InvariantCulture;
            var s = new StringBuilder();
            s.Append("{\n");
            s.AppendFormat(inv, "  \"name\": \"{0}\",\n", Art.Key);
            s.AppendFormat(inv, "  \"cellWidth\": {0},\n  \"cellHeight\": {1},\n", Art.Width, Art.Height);
            s.AppendFormat(inv, "  \"columns\": {0},\n  \"rows\": {1},\n", Columns, Rows);
            s.AppendFormat(inv, "  \"pixelsPerMeter\": {0},\n", Art.PixelDensity);
            s.AppendFormat(inv, "  \"directions\": {0},\n", Directions);
            s.AppendFormat(inv, "  \"pivotX\": {0},\n  \"pivotY\": {1},\n", Art.PivotX, Art.PivotY);
            s.AppendFormat(inv, "  \"emission\": \"{0}\",\n  \"bodyGlow\": {1},\n", emission, BodyGlow);
            s.Append("  \"clips\": [\n");
            EnemyClipSpec[] clips = Art.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                s.AppendFormat(inv, "    {{\"name\": \"{0}\", \"row\": {1}, \"frames\": {2}, \"fps\": {3}, \"loop\": {4}}}{5}\n",
                    clips[i].Name, i * Directions, clips[i].Frames, clips[i].Fps, clips[i].Loop ? "true" : "false",
                    i < clips.Length - 1 ? "," : "");
            }

            s.Append("  ]\n}\n");
            return s.ToString();
        }
    }
}
