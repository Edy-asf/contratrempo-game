using System;
using static Icaro.PixelCanvas;

namespace Icaro
{
    // Desenho em código de um inimigo em sprite (sem UnityEngine, para as prévias fora do Unity usarem o mesmo
    // código). Cada quadro é desenhado numa célula de Width x Height pixels (16 px por metro) em coordenadas locais:
    // origem no ponto do chão (o pivô), x para a direita (para a frente na vista de lado) e y para baixo (negativo é
    // para cima). Pose (deslocamento e giro em volta de um ponto) e Push/Pop permitem animar partes inteiras. As
    // partes que brilham (Emit*) vão também para a máscara de brilho: no jogo acendem no escuro, e o resto do corpo
    // fica com o brilho mínimo.
    public abstract class EnemyArt
    {
        public const float PixelsPerMeter = 16f;

        // Pasta e nome da folha (o nome do inimigo no código: Cascudo, Espinhento...)
        public abstract string Key { get; }
        public abstract int Width { get; }
        public abstract int Height { get; }

        // Pivô em pixels a partir do canto de baixo à esquerda (o chão; nos voadores, o centro do corpo)
        public virtual float PivotX => Width * 0.5f;
        public virtual float PivotY => 1f;

        public abstract EnemyClipSpec[] Clips { get; }

        // Plantas que sempre olham para o jogador: as quatro direções usam a vista de frente
        public virtual bool SameAllDirections => false;

        protected abstract void Draw(EnemyView view, string clip, float t, int frame);

        PixelCanvas m_Canvas, m_Mask;
        SpriteRig m_Rig, m_MaskRig;
        readonly float[] m_Stack = new float[3 * 16];
        int m_Depth;

        // Um quadro: t vai de 0 a 1 ao longo do clipe (nos laços, sem repetir o primeiro quadro no fim)
        public void DrawFrame(PixelCanvas canvas, PixelCanvas mask, EnemyView view, EnemyClipSpec clip, int frame)
        {
            m_Canvas = canvas;
            m_Mask = mask;
            float ox = PivotX, oy = Height - PivotY;
            m_Rig = new SpriteRig(canvas, ox, oy, 0f);
            m_MaskRig = new SpriteRig(mask, ox, oy, 0f);
            m_Depth = 0;
            float t = clip.Loop ? frame / (float)clip.Frames : clip.Frames > 1 ? frame / (float)(clip.Frames - 1) : 0f;
            Draw(view, clip.Name, t, frame);
        }

        // ---------- pose ----------

        // Desloca (dx, dy) e gira 'angle' graus (horário) em volta do ponto local (px, py), a partir da pose atual.
        // Dentro, as coordenadas continuam as mesmas (relativas ao chão).
        protected void Push(float dx, float dy, float angle = 0f, float px = 0f, float py = 0f)
        {
            m_Stack[m_Depth * 3] = m_Rig.OriginX;
            m_Stack[m_Depth * 3 + 1] = m_Rig.OriginY;
            m_Stack[m_Depth * 3 + 2] = m_Rig.Angle;
            m_Depth++;

            // Ponto de giro na tela e a nova origem: o ponto (px, py) vai para onde estava + (dx, dy)
            m_Rig.ToCanvas(px + dx, py + dy, out float cx, out float cy);
            float total = m_Rig.Angle + angle;
            double r = total * Math.PI / 180.0;
            float cos = (float)Math.Cos(r), sin = (float)Math.Sin(r);
            float ox = cx - (px * cos - py * sin);
            float oy = cy - (px * sin + py * cos);
            m_Rig.SetPose(ox, oy, total);
            m_MaskRig.SetPose(ox, oy, total);
        }

        protected void Pop()
        {
            m_Depth--;
            float ox = m_Stack[m_Depth * 3], oy = m_Stack[m_Depth * 3 + 1], angle = m_Stack[m_Depth * 3 + 2];
            m_Rig.SetPose(ox, oy, angle);
            m_MaskRig.SetPose(ox, oy, angle);
        }

        // ---------- formas (bias clareia ou escurece; as partes de trás usam bias negativo) ----------

        protected void Ell(float x, float y, float rx, float ry, Px[] ramp, float bias = 0f) =>
            m_Rig.Ellipse(x, y, rx, ry, ramp, bias);

        protected void Cap(float x0, float y0, float x1, float y1, float r, Px[] ramp, float bias = 0f) =>
            m_Rig.Capsule(x0, y0, x1, y1, r, ramp, bias);

        protected void Poly(Px[] ramp, float bias, params float[] xy) => m_Rig.Poly(0.9f, ramp, bias, xy);

        protected void Plate(float x, float y, float w, float h, Px[] ramp, float bias = 0f, float bevel = 1.2f) =>
            m_Rig.Plate(x, y, w, h, bevel, ramp, bias);

        protected void Shape(Shape local, Px[] ramp, float bias = 0f) => m_Rig.Fill(local, ramp, bias);

        protected void Line(float x0, float y0, float x1, float y1, Px color, float width = 1f) =>
            m_Rig.Line(x0, y0, x1, y1, color, width);

        // Linha por vários pontos (x, y em sequência)
        protected void Path(Px color, float width, params float[] xy)
        {
            for (int i = 0; i + 3 < xy.Length; i += 2)
                Line(xy[i], xy[i + 1], xy[i + 2], xy[i + 3], color, width);
        }

        protected void Dot(float x, float y, Px color)
        {
            m_Rig.ToCanvas(x, y, out float cx, out float cy);
            m_Canvas.Set((int)Math.Floor(cx), (int)Math.Floor(cy), color);
        }

        // ---------- partes que brilham ----------

        protected void EmitEll(float x, float y, float rx, float ry, Px[] ramp, float bias = 0f)
        {
            m_Rig.Ellipse(x, y, rx, ry, ramp, bias);
            m_MaskRig.Flat(Ellipse(x, y, rx, ry), PixelRamps.White);
        }

        protected void EmitCap(float x0, float y0, float x1, float y1, float r, Px[] ramp, float bias = 0f)
        {
            m_Rig.Capsule(x0, y0, x1, y1, r, ramp, bias);
            m_MaskRig.Flat(Capsule(x0, y0, x1, y1, r), PixelRamps.White);
        }

        protected void EmitPoly(Px[] ramp, float bias, params float[] xy)
        {
            m_Rig.Poly(0.9f, ramp, bias, xy);
            m_MaskRig.Flat(PixelCanvas.Poly(0f, xy), PixelRamps.White);
        }

        protected void EmitShape(Shape local, Px[] ramp, float bias = 0f)
        {
            m_Rig.Fill(local, ramp, bias);
            m_MaskRig.Flat(local, PixelRamps.White);
        }

        protected void EmitLine(float x0, float y0, float x1, float y1, Px color, float width = 1f)
        {
            m_Rig.Line(x0, y0, x1, y1, color, width);
            m_MaskRig.Line(x0, y0, x1, y1, PixelRamps.White, width);
        }

        protected void EmitDot(float x, float y, Px color)
        {
            m_Rig.ToCanvas(x, y, out float cx, out float cy);
            int px = (int)Math.Floor(cx), py = (int)Math.Floor(cy);
            m_Canvas.Set(px, py, color);
            m_Mask.Set(px, py, PixelRamps.White);
        }

        // Clareia o que já está desenhado em volta de (x, y): núcleo de luz por cima do corpo, sem halo no vazio
        protected void Light(float x, float y, float r, Px color, float strength)
        {
            if (strength <= 0f)
                return;
            m_Rig.ToCanvas(x, y, out float cx, out float cy);
            int x0 = (int)(cx - r) - 1, x1 = (int)(cx + r) + 1, y0 = (int)(cy - r) - 1, y1 = (int)(cy + r) + 1;
            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    if (m_Canvas.Get(px, py).A < 200)
                        continue;
                    float ddx = px + 0.5f - cx, ddy = py + 0.5f - cy;
                    float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy) / r;
                    if (d >= 1f)
                        continue;
                    float amount = strength * (1f - d);
                    amount = (float)Math.Floor(amount * 4f + 0.35f) / 4f;
                    m_Canvas.Add(px, py, color, amount);
                }
            }
        }

        // ---------- utilidades ----------

        protected static float Sin01(float t) => (float)Math.Sin(t * Math.PI * 2.0);
        protected static float Cos01(float t) => (float)Math.Cos(t * Math.PI * 2.0);
        protected static float Lerp(float a, float b, float t) => a + (b - a) * t;
        protected static float Ease(float t) => t * t * (3f - 2f * t);
        protected static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
