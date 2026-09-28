using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cartographer
{
    /// <summary>
    /// Right-click picks the thing under the cursor (a label, a stamp, a line, a wash or a fill, topmost first); it can
    /// then be dragged, nudged with the arrow keys, resized, recoloured or deleted. Every change can be undone.
    /// </summary>
    internal sealed class Selection
    {
        // Screen units of slack when clicking a thin line.
        private const float PickSlack = 6f;

        private readonly CanvasView _view;
        private readonly Action<Action> _record;
        private bool _moving;
        private Vector2 _grab;
        private Vector2 _moved;

        public Selection(CanvasView view, Action<Action> record)
        {
            _view = view;
            _record = record;
        }

        /// <summary>A Stroke, Fill, Stamp or Note on the view's drawing, or null.</summary>
        public object Selected { get; private set; }

        /// <summary>When something is picked or let go.</summary>
        public event Action Changed;

        public bool Any => Selected != null;

        public bool Moving => _moving;

        public bool Recolourable => Selected is Fill || (Selected is Stroke stroke && stroke.Pen != Pen.Guide);

        public string Describe()
        {
            switch (Selected)
            {
                case Note _:
                    return "label";
                case Stamp _:
                    return "stamp";
                case Fill _:
                    return "fill";
                case Stroke stroke:
                    return stroke.Pen == Pen.Wash ? "wash" : stroke.Pen == Pen.Guide ? "guide" : "line";
                default:
                    return "";
            }
        }

        public void SelectAt(Vector2 point)
        {
            Selected = HitTest(point);
            ShowBox();
            Changed?.Invoke();
        }

        public void Clear()
        {
            _moving = false;
            if (Selected == null)
            {
                return;
            }
            Selected = null;
            ShowBox();
            Changed?.Invoke();
        }

        /// <summary>After undo or a redraw: keeps the box on the selected thing, or lets go if it's gone.</summary>
        public void Refresh()
        {
            if (Selected != null && !OnSheet(Selected))
            {
                Clear();
                return;
            }
            ShowBox();
        }

        /// <summary>A press on the selected thing starts moving it; false when the press is elsewhere.</summary>
        public bool Grab(Vector2 point)
        {
            if (Selected == null || !Grown(BoundsOf(Selected), Slack()).Contains(point))
            {
                return false;
            }
            _moving = true;
            _grab = point;
            _moved = Vector2.zero;
            return true;
        }

        public void DragTo(Vector2 point)
        {
            if (!_moving)
            {
                return;
            }
            Vector2 step = point - _grab - _moved;
            Translate(Selected, step);
            _moved += step;
            _view.Moved(Selected);
            ShowBox();
        }

        public void Release()
        {
            if (!_moving)
            {
                return;
            }
            _moving = false;
            if (_moved != Vector2.zero)
            {
                object thing = Selected;
                Vector2 moved = _moved;
                _record(() => Translate(thing, -moved));
            }
        }

        /// <param name="screenUnits">How far to move, in screen units (converted by the zoom).</param>
        public void Nudge(Vector2 screenUnits)
        {
            if (Selected == null)
            {
                return;
            }
            object thing = Selected;
            Vector2 step = screenUnits / _view.Scale;
            Translate(thing, step);
            _view.Moved(thing);
            ShowBox();
            _record(() => Translate(thing, -step));
        }

        public void ScaleBy(float factor)
        {
            if (Selected == null)
            {
                return;
            }
            object thing = Selected;
            Vector2 center = BoundsOf(thing).center;
            Scale(thing, center, factor);
            _view.Moved(thing);
            ShowBox();
            _record(() => Scale(thing, center, 1f / factor));
        }

        public void Recolour(byte color)
        {
            object thing = Selected;
            if (thing is Fill fill)
            {
                byte before = fill.Color;
                fill.Color = color;
                _record(() => fill.Color = before);
            }
            else if (thing is Stroke stroke && stroke.Pen != Pen.Guide)
            {
                byte before = stroke.Color;
                stroke.Color = color;
                _record(() => stroke.Color = before);
            }
            else
            {
                return;
            }
            _view.Moved(thing);
        }

        public void Delete()
        {
            Sheet sheet = _view.Sheet;
            object thing = Selected;
            if (sheet == null || thing == null)
            {
                return;
            }
            Action restore = Remove(sheet, thing);
            Clear();
            if (restore != null)
            {
                _view.Refresh();
                _record(restore);
            }
        }

        // ---- Finding and measuring things ----

        private object HitTest(Vector2 point)
        {
            Sheet sheet = _view.Sheet;
            if (sheet == null)
            {
                return null;
            }
            float slack = Slack();
            for (int i = sheet.Notes.Count - 1; i >= 0; i--)
            {
                if (Grown(BoundsOf(sheet.Notes[i]), slack).Contains(point))
                {
                    return sheet.Notes[i];
                }
            }
            for (int i = sheet.Stamps.Count - 1; i >= 0; i--)
            {
                if (Grown(BoundsOf(sheet.Stamps[i]), slack).Contains(point))
                {
                    return sheet.Stamps[i];
                }
            }
            // Lines over guides over washes, as they're drawn.
            foreach (Pen[] pens in new[] { new[] { Pen.Charcoal, Pen.Ink }, new[] { Pen.Guide }, new[] { Pen.Wash } })
            {
                for (int i = sheet.Strokes.Count - 1; i >= 0; i--)
                {
                    Stroke stroke = sheet.Strokes[i];
                    if (Array.IndexOf(pens, stroke.Pen) >= 0 && NearLine(stroke.Points, point, stroke.Width * 0.5f + slack))
                    {
                        return stroke;
                    }
                }
            }
            for (int i = sheet.Fills.Count - 1; i >= 0; i--)
            {
                if (InsidePolygon(sheet.Fills[i].Points, point))
                {
                    return sheet.Fills[i];
                }
            }
            return null;
        }

        private float Slack()
        {
            return PickSlack / Mathf.Max(_view.Scale, 0.0001f);
        }

        public static Rect BoundsOf(object thing)
        {
            switch (thing)
            {
                case Stroke stroke:
                    return Grown(PointBounds(stroke.Points), stroke.Width * 0.5f);
                case Fill fill:
                    return PointBounds(fill.Points);
                case Stamp stamp:
                    return new Rect(stamp.Position - Vector2.one * stamp.Size * 0.5f, Vector2.one * stamp.Size);
                case Note note:
                    // Roughly what the label covers: about half a text height per letter.
                    float halfWidth = note.Size * (0.27f * Mathf.Max(1, note.Text?.Length ?? 1) + 0.2f);
                    return new Rect(note.Position.x - halfWidth, note.Position.y - note.Size * 0.6f, halfWidth * 2f, note.Size * 1.2f);
                default:
                    return new Rect();
            }
        }

        private void ShowBox()
        {
            _view.ShowSelection(Selected != null ? BoundsOf(Selected) : (Rect?)null);
        }

        private bool OnSheet(object thing)
        {
            Sheet sheet = _view.Sheet;
            switch (thing)
            {
                case Stroke stroke:
                    return sheet != null && sheet.Strokes.Contains(stroke);
                case Fill fill:
                    return sheet != null && sheet.Fills.Contains(fill);
                case Stamp stamp:
                    return sheet != null && sheet.Stamps.Contains(stamp);
                case Note note:
                    return sheet != null && sheet.Notes.Contains(note);
                default:
                    return false;
            }
        }

        // ---- Changing things ----

        private static void Translate(object thing, Vector2 step)
        {
            switch (thing)
            {
                case Stroke stroke:
                    Shift(stroke.Points, step);
                    break;
                case Fill fill:
                    Shift(fill.Points, step);
                    break;
                case Stamp stamp:
                    stamp.Position += step;
                    break;
                case Note note:
                    note.Position += step;
                    break;
            }
        }

        // Scaling keeps a fill's triangles valid, so they aren't worked out again.
        private static void Scale(object thing, Vector2 center, float factor)
        {
            switch (thing)
            {
                case Stroke stroke:
                    ScalePoints(stroke.Points, center, factor);
                    stroke.Width *= factor;
                    break;
                case Fill fill:
                    ScalePoints(fill.Points, center, factor);
                    break;
                case Stamp stamp:
                    stamp.Position = center + (stamp.Position - center) * factor;
                    stamp.Size *= factor;
                    break;
                case Note note:
                    note.Position = center + (note.Position - center) * factor;
                    note.Size *= factor;
                    break;
            }
        }

        /// <summary>Takes the thing off the sheet; returns how to put it back where it was.</summary>
        private static Action Remove(Sheet sheet, object thing)
        {
            switch (thing)
            {
                case Stroke stroke:
                    return RemoveFrom(sheet.Strokes, stroke);
                case Fill fill:
                    return RemoveFrom(sheet.Fills, fill);
                case Stamp stamp:
                    return RemoveFrom(sheet.Stamps, stamp);
                case Note note:
                    return RemoveFrom(sheet.Notes, note);
                default:
                    return null;
            }
        }

        private static Action RemoveFrom<T>(List<T> list, T item)
        {
            int index = list.IndexOf(item);
            if (index < 0)
            {
                return null;
            }
            list.RemoveAt(index);
            return () => list.Insert(Mathf.Min(index, list.Count), item);
        }

        private static void Shift(List<Vector2> points, Vector2 step)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] += step;
            }
        }

        private static void ScalePoints(List<Vector2> points, Vector2 center, float factor)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = center + (points[i] - center) * factor;
            }
        }

        private static Rect PointBounds(List<Vector2> points)
        {
            if (points.Count == 0)
            {
                return new Rect();
            }
            Vector2 min = points[0];
            Vector2 max = points[0];
            foreach (Vector2 p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Grown(Rect rect, float by)
        {
            return Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);
        }

        private static bool NearLine(List<Vector2> points, Vector2 point, float reach)
        {
            float reach2 = reach * reach;
            if (points.Count == 1)
            {
                return (points[0] - point).sqrMagnitude <= reach2;
            }
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 ab = points[i] - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                if ((a + ab * t - point).sqrMagnitude <= reach2)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InsidePolygon(List<Vector2> points, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
