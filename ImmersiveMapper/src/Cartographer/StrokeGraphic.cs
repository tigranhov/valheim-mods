using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Draws a sheet's strokes as one UI mesh: a textured quad per segment, overlapping at the joints. Rebuilt only when
    /// the sheet changes (SetVerticesDirty), so a still sheet costs nothing per frame.
    /// </summary>
    internal sealed class StrokeGraphic : MaskableGraphic
    {
        // UI meshes use 16-bit indices.
        private const int MaxVertices = 64000;

        public static readonly Color32[] Palette =
        {
            new Color32(38, 30, 24, 255),   // charcoal / black ink
            new Color32(150, 52, 30, 255),  // red ochre
            new Color32(40, 66, 120, 255),  // woad blue
            new Color32(58, 92, 44, 255),   // green
        };

        public Sheet Sheet;
        /// <summary>The stroke being drawn right now, not yet on the sheet.</summary>
        public Stroke Live;

        public override Texture mainTexture => PaperTextures.Pens;

        /// <summary>Line width as a share of the sheet's height.</summary>
        public static float Width(Pen pen)
        {
            return pen == Pen.Ink ? 0.0045f : 0.011f;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (Sheet != null)
            {
                foreach (Stroke stroke in Sheet.Strokes)
                {
                    AddStroke(vh, stroke, rect);
                }
            }
            if (Live != null)
            {
                AddStroke(vh, Live, rect);
            }
        }

        private static void AddStroke(VertexHelper vh, Stroke stroke, Rect rect)
        {
            if (stroke.Points.Count == 0)
            {
                return;
            }
            Color32 color = Palette[stroke.Color < Palette.Length ? stroke.Color : 0];
            Vector2 rows = PaperTextures.PenRows(stroke.Pen);
            float half = Width(stroke.Pen) * rect.height * 0.5f;
            // The pen texture repeats every few widths along the line.
            float tile = half * 8f;

            if (stroke.Points.Count == 1)
            {
                Vector2 p = ToLocal(stroke.Points[0], rect);
                AddQuad(vh, p + new Vector2(-half, -half), p + new Vector2(-half, half), p + new Vector2(half, half), p + new Vector2(half, -half),
                    0f, 0.25f, rows, color);
                return;
            }

            float along = 0f;
            Vector2 a = ToLocal(stroke.Points[0], rect);
            for (int i = 1; i < stroke.Points.Count; i++)
            {
                Vector2 b = ToLocal(stroke.Points[i], rect);
                Vector2 d = b - a;
                float length = d.magnitude;
                if (length < 0.01f)
                {
                    continue;
                }
                if (vh.currentVertCount + 4 > MaxVertices)
                {
                    return;
                }
                Vector2 dir = d / length;
                Vector2 normal = new Vector2(-dir.y, dir.x) * half;
                // Each segment reaches half a width past both ends, so joints overlap instead of leaving gaps.
                Vector2 start = a - dir * half;
                Vector2 end = b + dir * half;
                AddQuad(vh, start - normal, start + normal, end + normal, end - normal, along / tile, (along + length) / tile, rows, color);
                along += length;
                a = b;
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br, float u0, float u1, Vector2 rows, Color32 color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(bl, color, new Vector2(u0, rows.x));
            vh.AddVert(tl, color, new Vector2(u0, rows.y));
            vh.AddVert(tr, color, new Vector2(u1, rows.y));
            vh.AddVert(br, color, new Vector2(u1, rows.x));
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        private static Vector2 ToLocal(Vector2 point, Rect rect)
        {
            return new Vector2(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);
        }
    }
}
