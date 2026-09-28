using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cartographer
{
    /// <summary>
    /// Turns strokes and fills into UI mesh. A solid stroke is one strip with mitred joints, so a see-through wash doesn't
    /// darken where its segments meet; a dotted one is a row of short dashes. Coordinates are canvas units times
    /// <c>ppu</c> (pixels per unit of the view).
    /// </summary>
    internal static class StrokeMesh
    {
        /// <summary>Most vertices one mesh takes (UI meshes use 16-bit indices).</summary>
        public const int MaxVertices = 64000;

        // Mitres longer than this many half-widths are cut short, so sharp turns don't grow spikes.
        private const float MaxMiter = 3f;

        private static readonly List<Vector2> Scratch = new List<Vector2>();

        /// <summary>A rough vertex count, for splitting big drawings over several meshes.</summary>
        public static int EstimateVertices(Stroke stroke)
        {
            return stroke.Style == LineStyle.Dotted ? stroke.Points.Count * 8 + 8 : stroke.Points.Count * 2 + 4;
        }

        public static int EstimateVertices(Fill fill)
        {
            return fill.Points.Count + 4;
        }

        public static void AddStroke(VertexHelper vh, Stroke stroke, float ppu)
        {
            if (stroke.Points.Count == 0)
            {
                return;
            }
            Color32 color = ColorOf(stroke);
            Vector2 rows = PaperTextures.PenRows(stroke.Pen);
            float half = Mathf.Max(stroke.Width * ppu * 0.5f, 0.5f);
            // The pen texture repeats every few widths along the line.
            float tile = half * 8f;

            Scratch.Clear();
            foreach (Vector2 point in stroke.Points)
            {
                Vector2 p = point * ppu;
                if (Scratch.Count == 0 || (p - Scratch[Scratch.Count - 1]).sqrMagnitude > 0.0001f)
                {
                    Scratch.Add(p);
                }
            }
            if (Scratch.Count == 1)
            {
                Vector2 p = Scratch[0];
                AddQuad(vh, p + new Vector2(-half, -half), p + new Vector2(-half, half), p + new Vector2(half, half), p + new Vector2(half, -half),
                    0f, 0.25f, rows, color);
                return;
            }
            if (stroke.Style == LineStyle.Dotted)
            {
                AddDashes(vh, Scratch, half, tile, rows, color);
            }
            else
            {
                AddStrip(vh, Scratch, half, tile, rows, color);
            }
        }

        public static void AddFill(VertexHelper vh, Fill fill, float ppu, List<int> triangles)
        {
            if (fill.Points.Count < 3 || triangles.Count == 0 || vh.currentVertCount + fill.Points.Count > MaxVertices)
            {
                return;
            }
            Color32 color = Inks.Of(fill.Color);
            color.a = fill.Alpha;
            int start = vh.currentVertCount;
            foreach (Vector2 point in fill.Points)
            {
                // Mottled by position, so neighbouring fills of one colour look like one wash.
                vh.AddVert(point * ppu, color, new Vector2(point.x * 2f, PaperTextures.FillRow));
            }
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                vh.AddTriangle(start + triangles[i], start + triangles[i + 1], start + triangles[i + 2]);
            }
        }

        private static Color32 ColorOf(Stroke stroke)
        {
            switch (stroke.Pen)
            {
                case Pen.Guide:
                    return Inks.GuideColor;
                default:
                    Color32 color = Inks.Of(stroke.Color);
                    color.a = stroke.Alpha;
                    return color;
            }
        }

        private static void AddStrip(VertexHelper vh, List<Vector2> points, float half, float tile, Vector2 rows, Color32 color)
        {
            int count = points.Count;
            if (vh.currentVertCount + count * 2 > MaxVertices)
            {
                return;
            }
            float along = 0f;
            int first = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = points[i];
                Vector2 dirIn = i > 0 ? (p - points[i - 1]).normalized : (points[1] - p).normalized;
                Vector2 dirOut = i < count - 1 ? (points[i + 1] - p).normalized : dirIn;
                if (i > 0)
                {
                    along += (p - points[i - 1]).magnitude;
                }
                Vector2 normalIn = new Vector2(-dirIn.y, dirIn.x);
                Vector2 normalOut = new Vector2(-dirOut.y, dirOut.x);
                Vector2 miter = normalIn + normalOut;
                miter = miter.sqrMagnitude < 0.0001f ? normalOut : miter.normalized;
                float length = half / Mathf.Max(Vector2.Dot(miter, normalOut), 1f / MaxMiter);
                Vector2 offset = miter * length;
                // Square caps: the ends reach half a width past the first and last points.
                if (i == 0)
                {
                    p -= dirOut * half;
                }
                else if (i == count - 1)
                {
                    p += dirIn * half;
                }
                float u = along / tile;
                vh.AddVert(p - offset, color, new Vector2(u, rows.x));
                vh.AddVert(p + offset, color, new Vector2(u, rows.y));
                if (i > 0)
                {
                    int a = first + (i - 1) * 2;
                    vh.AddTriangle(a, a + 1, a + 3);
                    vh.AddTriangle(a + 3, a + 2, a);
                }
            }
        }

        private static void AddDashes(VertexHelper vh, List<Vector2> points, float half, float tile, Vector2 rows, Color32 color)
        {
            float dash = half * 5f;
            float gap = half * 4f;
            float period = dash + gap;
            float along = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                float length = (b - a).magnitude;
                Vector2 dir = (b - a) / length;
                Vector2 normal = new Vector2(-dir.y, dir.x) * half;
                // Walk this segment in dash-sized pieces, keeping the pattern going across segments.
                float t = 0f;
                while (t < length)
                {
                    float phase = (along + t) % period;
                    if (phase < dash)
                    {
                        // Always move on a little, so float error at a dash's end can't stall the walk.
                        float end = Mathf.Min(length, Mathf.Max(t + dash - phase, t + 0.01f));
                        if (vh.currentVertCount + 4 > MaxVertices)
                        {
                            return;
                        }
                        Vector2 s = a + dir * t;
                        Vector2 e = a + dir * end;
                        AddQuad(vh, s - normal, s + normal, e + normal, e - normal, (along + t) / tile, (along + end) / tile, rows, color);
                        t = end;
                    }
                    else
                    {
                        t += Mathf.Max(period - phase, 0.01f);
                    }
                }
                along += length;
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br, float u0, float u1, Vector2 rows, Color32 color)
        {
            if (vh.currentVertCount + 4 > MaxVertices)
            {
                return;
            }
            int index = vh.currentVertCount;
            vh.AddVert(bl, color, new Vector2(u0, rows.x));
            vh.AddVert(tl, color, new Vector2(u0, rows.y));
            vh.AddVert(tr, color, new Vector2(u1, rows.y));
            vh.AddVert(br, color, new Vector2(u1, rows.x));
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
