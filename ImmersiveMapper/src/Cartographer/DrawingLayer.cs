using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cartographer
{
    /// <summary>
    /// One mesh of a drawing: a slice of its fills and strokes. A big master map is spread over several of these, since
    /// one UI mesh holds at most 64k vertices. Rebuilt only when marked dirty.
    /// </summary>
    internal sealed class DrawingLayer : MaskableGraphic
    {
        public float Ppu = 100f;
        public readonly List<Fill> Fills = new List<Fill>();
        public readonly List<Stroke> Strokes = new List<Stroke>();
        public int Estimate;

        public override Texture mainTexture => PaperTextures.Pens;

        public void Clear()
        {
            Fills.Clear();
            Strokes.Clear();
            Estimate = 0;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Fill fill in Fills)
            {
                StrokeMesh.AddFill(vh, fill, Ppu, fill.Triangles);
            }
            foreach (Stroke stroke in Strokes)
            {
                StrokeMesh.AddStroke(vh, stroke, Ppu);
            }
        }
    }

    /// <summary>A faint square grid over a master map, drawn for the part in view. Not part of the drawing.</summary>
    internal sealed class GridGraphic : MaskableGraphic
    {
        private static readonly Color32 LineColor = new Color32(90, 65, 40, 55);

        /// <summary>The visible canvas area, in canvas units.</summary>
        public Rect View;
        /// <summary>Viewport pixels per canvas unit at the current zoom.</summary>
        public float Scale = 100f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float step = Sheet.GridSquare;
            // Zoomed far out, show every tenth line only, so the grid doesn't turn into a solid wash.
            while (step * Scale < 10f)
            {
                step *= 10f;
            }
            Rect rect = rectTransform.rect;
            float thickness = 1f;
            float x0 = Mathf.Floor(View.xMin / step) * step;
            for (float x = x0; x <= View.xMax; x += step)
            {
                float px = rect.xMin + (x - View.xMin) / View.width * rect.width;
                AddRect(vh, new Rect(px - thickness * 0.5f, rect.yMin, thickness, rect.height));
            }
            float y0 = Mathf.Floor(View.yMin / step) * step;
            for (float y = y0; y <= View.yMax; y += step)
            {
                float py = rect.yMin + (y - View.yMin) / View.height * rect.height;
                AddRect(vh, new Rect(rect.xMin, py - thickness * 0.5f, rect.width, thickness));
            }
        }

        private static void AddRect(VertexHelper vh, Rect r)
        {
            if (vh.currentVertCount + 4 > StrokeMesh.MaxVertices)
            {
                return;
            }
            int index = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMin), LineColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMax), LineColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), LineColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), LineColor, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
