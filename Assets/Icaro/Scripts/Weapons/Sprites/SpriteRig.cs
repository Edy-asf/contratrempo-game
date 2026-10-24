using System;
using static Icaro.PixelCanvas;

namespace Icaro
{
    // Desenha peças de um sprite em coordenadas locais (de uma arma ou de um braço) sobre um PixelCanvas: a origem
    // local cai em (OriginX, OriginY) da tela e tudo gira de Angle graus (positivo gira no sentido horário da tela,
    // então uma arma com o cano para -x aponta para cima e para a esquerda). As formas são avaliadas pixel a pixel já
    // giradas, então o desenho continua nítido em qualquer ângulo, e as normais giram junto (a luz não muda).
    public sealed class SpriteRig
    {
        public readonly PixelCanvas Canvas;
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }
        public float Angle { get; private set; }

        float m_Cos = 1f;
        float m_Sin;

        public SpriteRig(PixelCanvas canvas, float originX, float originY, float angle)
        {
            Canvas = canvas;
            SetPose(originX, originY, angle);
        }

        public void SetPose(float originX, float originY, float angle)
        {
            OriginX = originX;
            OriginY = originY;
            Angle = angle;
            double radians = angle * Math.PI / 180.0;
            m_Cos = (float)Math.Cos(radians);
            m_Sin = (float)Math.Sin(radians);
        }

        // Ponto local -> tela
        public void ToCanvas(float x, float y, out float cx, out float cy)
        {
            cx = OriginX + x * m_Cos - y * m_Sin;
            cy = OriginY + x * m_Sin + y * m_Cos;
        }

        // Direção local -> tela
        public void Rotate(float x, float y, out float rx, out float ry)
        {
            rx = x * m_Cos - y * m_Sin;
            ry = x * m_Sin + y * m_Cos;
        }

        // Forma local avaliada na tela
        public Shape Local(Shape shape)
        {
            float ox = OriginX, oy = OriginY, cos = m_Cos, sin = m_Sin;
            return (float x, float y, out float nx, out float ny) =>
            {
                float dx = x - ox, dy = y - oy;
                float lx = dx * cos + dy * sin;
                float ly = -dx * sin + dy * cos;
                if (!shape(lx, ly, out float lnx, out float lny))
                {
                    nx = ny = 0f;
                    return false;
                }

                nx = lnx * cos - lny * sin;
                ny = lnx * sin + lny * cos;
                return true;
            };
        }

        // ---------- peças ----------

        public void Plate(float x, float y, float w, float h, float bevel, Px[] ramp, float bias = 0f,
            float corner = 0f) => Canvas.Fill(Local(PixelCanvas.Plate(x, y, w, h, bevel, corner)), ramp, bias);

        public void Tube(float x0, float y0, float x1, float y1, float r, Px[] ramp, float bias = 0f) =>
            Canvas.Fill(Local(PixelCanvas.Tube(x0, y0, x1, y1, r)), ramp, bias);

        public void Capsule(float x0, float y0, float x1, float y1, float r, Px[] ramp, float bias = 0f) =>
            Canvas.Fill(Local(PixelCanvas.Capsule(x0, y0, x1, y1, r)), ramp, bias);

        public void Circle(float x, float y, float r, Px[] ramp, float bias = 0f) =>
            Canvas.Fill(Local(PixelCanvas.Circle(x, y, r)), ramp, bias);

        public void Ellipse(float x, float y, float rx, float ry, Px[] ramp, float bias = 0f) =>
            Canvas.Fill(Local(PixelCanvas.Ellipse(x, y, rx, ry)), ramp, bias);

        public void Ring(float x, float y, float ro, float ri, Px[] ramp, float bias = 0f) =>
            Canvas.Fill(Local(PixelCanvas.Ring(x, y, ro, ri)), ramp, bias);

        public void Poly(float bevel, Px[] ramp, float bias, params float[] xy) =>
            Canvas.Fill(Local(PixelCanvas.Poly(bevel, xy)), ramp, bias);

        public void Fill(Shape local, Px[] ramp, float bias = 0f, byte alpha = 255) =>
            Canvas.Fill(Local(local), ramp, bias, alpha);

        public void Flat(Shape local, Px color) => Canvas.FillFlat(Local(local), color);

        public void FlatPoly(Px color, params float[] xy) => Canvas.FillFlat(Local(PixelCanvas.Poly(0f, xy)), color);

        public void FlatRect(float x, float y, float w, float h, Px color) =>
            Canvas.FillFlat(Local(PixelCanvas.Plate(x, y, w, h, 0f)), color);

        public void Clear(Shape local) => Canvas.Clear(Local(local));

        // Linha de largura 'width' (cápsula sem sombreado)
        public void Line(float x0, float y0, float x1, float y1, Px color, float width = 1f) =>
            Canvas.FillFlat(Local(PixelCanvas.Capsule(x0, y0, x1, y1, width * 0.5f)), color);

        public void Glow(float x, float y, float r, Px color, float strength, bool over = false)
        {
            ToCanvas(x, y, out float cx, out float cy);
            Canvas.Glow(cx, cy, r, color, strength, over);
        }

        // Chama saindo do ponto local (x, y) na direção local (dx, dy)
        public void Flame(float x, float y, float dx, float dy, float w, float h, Px[] ramp, float seed)
        {
            ToCanvas(x, y, out float cx, out float cy);
            Rotate(dx, dy, out float rx, out float ry);
            Canvas.Flame(cx, cy, rx, ry, w, h, ramp, seed);
        }
    }
}
