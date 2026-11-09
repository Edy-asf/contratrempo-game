using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Acentos e cedilha desenhados por cima de um MenuTitle (a fonte dos títulos não os tem): blocos de pixel com
    // sombra, contorno branco e a cor do texto, nessa ordem, que acompanham a letra (MenuTitle.CollectAccents).
    public class MenuTitleAccents : MaskableGraphic
    {
        public MenuTitle Owner;
        static readonly List<Rect> s_Rects = new List<Rect>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Owner)
                return;
            Owner.CollectAccents(s_Rects);
            float stroke = Owner.Stroke;
            var shadowOffset = new Vector2(stroke * 1.2f, -stroke * 1.2f);
            var shadow = new Color(0.05f, 0.05f, 0.05f, 0.9f);
            foreach (Rect r in s_Rects)
                Quad(vh, Grow(r, stroke), shadowOffset, shadow);
            foreach (Rect r in s_Rects)
                Quad(vh, Grow(r, stroke), Vector2.zero, Color.white);
            foreach (Rect r in s_Rects)
                Quad(vh, r, Vector2.zero, Owner.FillColor);
        }

        static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        static void Quad(VertexHelper vh, Rect r, Vector2 offset, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin + offset.x, r.yMin + offset.y), color, Vector4.zero);
            vh.AddVert(new Vector3(r.xMin + offset.x, r.yMax + offset.y), color, Vector4.zero);
            vh.AddVert(new Vector3(r.xMax + offset.x, r.yMax + offset.y), color, Vector4.zero);
            vh.AddVert(new Vector3(r.xMax + offset.x, r.yMin + offset.y), color, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
