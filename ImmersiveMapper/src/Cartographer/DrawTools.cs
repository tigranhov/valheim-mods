using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ImmersiveMapper.Cartographer
{
    internal enum ToolKind
    {
        Charcoal,
        Quill,
        Wash,
        Fill,
        Stamp,
        Text,
        Eraser,
    }

    /// <summary>
    /// The drawing tools, working on whatever drawing a canvas view shows. Sizes are shares of the view's height, divided
    /// by the zoom, so zooming in draws finer. Right-click selects a thing to move, nudge, resize, recolour or delete.
    /// Every change can be undone while the view stays open.
    /// </summary>
    internal sealed class DrawTools : ICanvasTool
    {
        // View heights between recorded points of a line.
        private const float PointSpacing = 0.004f;
        private const int NoteLimit = 40;
        private const int MaxFillPoints = 240;

        public ToolKind Tool = ToolKind.Charcoal;
        public BrushSize Size = BrushSize.Medium;
        public byte Color = Inks.Black;
        public bool Straight;
        public bool Dotted;
        public string StampId = "mark_x";
        public Opacity Opacity = Opacity.Solid;
        public Func<ToolKind, bool> Allowed = _ => true;
        public int MaxPoints = 6000;
        public int MaxMarks = 200;
        /// <summary>After every change to the drawing: save it.</summary>
        public Action Changed;

        private readonly CanvasView _view;
        private readonly List<Action> _undo = new List<Action>();
        private Stroke _live;
        private bool _liveIsFill;
        private List<Action> _erased;
        private bool _warnedFull;

        private readonly Selection _selection;

        public DrawTools(CanvasView view)
        {
            _view = view;
            _selection = new Selection(view, Done);
        }

        public Selection Selection => _selection;

        public bool CanUndo => _undo.Count > 0;

        private Sheet Sheet => _view.Sheet;

        public void ClearUndo()
        {
            _undo.Clear();
        }

        public void Deselect()
        {
            _selection.Clear();
        }

        public void RightClick(Vector2 point)
        {
            Finish();
            _selection.SelectAt(Clamp(point));
        }

        /// <summary>Keys for the selected thing: Delete or Backspace erases it, the arrows nudge it (Shift: further).</summary>
        public void UpdateKeys()
        {
            if (!_selection.Any || TextPrompt.Open)
            {
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.Delete) || ZInput.GetKeyDown(KeyCode.Backspace))
            {
                _selection.Delete();
                return;
            }
            float step = ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift) ? 10f : 1f;
            Vector2 nudge = Vector2.zero;
            if (ZInput.GetKeyDown(KeyCode.LeftArrow)) nudge.x -= step;
            if (ZInput.GetKeyDown(KeyCode.RightArrow)) nudge.x += step;
            if (ZInput.GetKeyDown(KeyCode.DownArrow)) nudge.y -= step;
            if (ZInput.GetKeyDown(KeyCode.UpArrow)) nudge.y += step;
            if (nudge != Vector2.zero)
            {
                _selection.Nudge(nudge);
            }
        }

        /// <summary>A colour clicked while a line or fill is selected recolours it; false when nothing could take it.</summary>
        public bool RecolourSelection(byte color)
        {
            if (!_selection.Recolourable)
            {
                return false;
            }
            _selection.Recolour(color);
            return true;
        }

        public void Down(Vector2 point, PointerEventData data)
        {
            if (Sheet == null || Sheet.Unreadable || TextPrompt.Open)
            {
                return;
            }
            point = Clamp(point);
            // With something selected, a press on it moves it and a press elsewhere only lets go (it doesn't draw).
            if (_selection.Any)
            {
                if (!_selection.Grab(point))
                {
                    _selection.Clear();
                }
                return;
            }
            if (!Allowed(Tool))
            {
                return;
            }
            _warnedFull = false;
            switch (Tool)
            {
                case ToolKind.Charcoal:
                case ToolKind.Quill:
                case ToolKind.Wash:
                case ToolKind.Fill:
                    StartLine(point);
                    break;
                case ToolKind.Stamp:
                    if (HasRoomForMark())
                    {
                        var stamp = new Stamp { Id = StampId, Position = point, Size = Inks.StampSize(Size) / _view.Zoom };
                        Sheet sheet = Sheet;
                        sheet.Stamps.Add(stamp);
                        _view.Added(stamp);
                        Done(() => sheet.Stamps.Remove(stamp));
                    }
                    break;
                case ToolKind.Text:
                    if (HasRoomForMark())
                    {
                        Sheet sheet = Sheet;
                        float size = Inks.TextSize(Size) / _view.Zoom;
                        TextPrompt.Ask("Write a label", "", NoteLimit, text => AddNote(sheet, point, size, text));
                    }
                    break;
                case ToolKind.Eraser:
                    _erased = new List<Action>();
                    EraseAt(point);
                    break;
            }
        }

        public void Drag(Vector2 point, PointerEventData data)
        {
            point = Clamp(point);
            if (_selection.Moving)
            {
                _selection.DragTo(point);
                return;
            }
            if (_live != null)
            {
                if (Straight && !_liveIsFill)
                {
                    _live.Points[_live.Points.Count - 1] = point;
                    _view.SetLive(_live);
                    return;
                }
                Vector2 last = _live.Points[_live.Points.Count - 1];
                if ((point - last).magnitude * _view.Zoom < PointSpacing)
                {
                    return;
                }
                if (Sheet.PointCount + _live.Points.Count >= MaxPoints)
                {
                    WarnFull();
                    return;
                }
                _live.Points.Add(point);
                _view.SetLive(_live);
            }
            else if (_erased != null)
            {
                EraseAt(point);
            }
        }

        public void Up(Vector2 point, PointerEventData data)
        {
            if (_selection.Moving)
            {
                _selection.Release();
                return;
            }
            Finish();
            if (_erased != null && _erased.Count > 0)
            {
                List<Action> restore = _erased;
                Done(() =>
                {
                    for (int i = restore.Count - 1; i >= 0; i--)
                    {
                        restore[i]();
                    }
                });
            }
            _erased = null;
        }

        /// <summary>Ctrl + wheel resizes the selected thing.</summary>
        public bool Scroll(Vector2 point, float delta)
        {
            if (!_selection.Any || (!ZInput.GetKey(KeyCode.LeftControl) && !ZInput.GetKey(KeyCode.RightControl)))
            {
                return false;
            }
            _selection.ScaleBy(delta > 0f ? 1.06f : 1f / 1.06f);
            return true;
        }

        /// <summary>Ends a line being drawn (the button was let go, the page changed or the view closed).</summary>
        public void Finish()
        {
            if (_live == null)
            {
                return;
            }
            Stroke stroke = _live;
            bool fill = _liveIsFill;
            _live = null;
            _view.SetLive(null);
            Sheet sheet = Sheet;
            if (sheet == null)
            {
                return;
            }
            if (fill)
            {
                Fill area = ToFill(stroke);
                if (area == null)
                {
                    return;
                }
                sheet.Fills.Add(area);
                _view.Added(area);
                Done(() => sheet.Fills.Remove(area));
                return;
            }
            sheet.Strokes.Add(stroke);
            _view.Added(stroke);
            Done(() => sheet.Strokes.Remove(stroke));
        }

        /// <summary>A change made outside the tools (a copied draft, a measuring string), so Undo takes it back too.</summary>
        public void Record(Action undo)
        {
            Done(undo);
        }

        public void Undo()
        {
            if (_undo.Count == 0 || _live != null || _erased != null)
            {
                return;
            }
            Action undo = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            undo();
            _view.Refresh();
            _selection.Refresh();
            Changed?.Invoke();
        }

        private void StartLine(Vector2 point)
        {
            _liveIsFill = Tool == ToolKind.Fill;
            Pen pen = Tool == ToolKind.Charcoal ? Pen.Charcoal : Tool == ToolKind.Wash ? Pen.Wash : Pen.Ink;
            _live = new Stroke
            {
                Pen = pen,
                Color = Tool == ToolKind.Charcoal ? Inks.Black : Color,
                // An outline being drawn for a fill shows as a thin line in the fill's colour.
                Width = (_liveIsFill ? Inks.PenWidth(Pen.Ink, BrushSize.Fine) : Inks.PenWidth(pen, Size)) / _view.Zoom,
                Style = Dotted && !_liveIsFill && pen != Pen.Wash ? LineStyle.Dotted : LineStyle.Solid,
                Alpha = Inks.AlphaOf(Opacity),
            };
            _live.Points.Add(point);
            if (Straight && !_liveIsFill)
            {
                _live.Points.Add(point);
            }
            _view.SetLive(_live);
        }

        private Fill ToFill(Stroke outline)
        {
            List<Vector2> points = outline.Points;
            if (points.Count < 3)
            {
                return null;
            }
            var fill = new Fill { Color = Color, Alpha = Inks.AlphaOf(Opacity) };
            // Long outlines are thinned out evenly: triangulating is quadratic in the number of points.
            int step = Mathf.Max(1, Mathf.CeilToInt(points.Count / (float)MaxFillPoints));
            for (int i = 0; i < points.Count; i += step)
            {
                fill.Points.Add(points[i]);
            }
            return fill.Points.Count >= 3 ? fill : null;
        }

        private void AddNote(Sheet sheet, Vector2 point, float size, string text)
        {
            if (string.IsNullOrEmpty(text) || sheet != Sheet)
            {
                return;
            }
            var note = new Note { Text = text, Position = point, Size = size };
            sheet.Notes.Add(note);
            _view.Added(note);
            Done(() => sheet.Notes.Remove(note));
        }

        private void EraseAt(Vector2 point)
        {
            Sheet sheet = Sheet;
            float radius = Inks.EraserRadius(Size) / _view.Zoom;
            bool erased = false;
            for (int i = sheet.Strokes.Count - 1; i >= 0; i--)
            {
                Stroke stroke = sheet.Strokes[i];
                if (Touches(stroke.Points, point, radius + stroke.Width * 0.5f))
                {
                    sheet.Strokes.RemoveAt(i);
                    _erased.Add(() => sheet.Strokes.Add(stroke));
                    erased = true;
                }
            }
            // A fill goes when the eraser touches its outline, not its inside: rubbing out a line on the sea keeps the sea.
            for (int i = sheet.Fills.Count - 1; i >= 0; i--)
            {
                Fill fill = sheet.Fills[i];
                if (Touches(fill.Points, point, radius))
                {
                    sheet.Fills.RemoveAt(i);
                    _erased.Add(() => sheet.Fills.Add(fill));
                    erased = true;
                }
            }
            for (int i = sheet.Stamps.Count - 1; i >= 0; i--)
            {
                Stamp stamp = sheet.Stamps[i];
                if ((stamp.Position - point).magnitude <= radius + stamp.Size * 0.5f)
                {
                    sheet.Stamps.RemoveAt(i);
                    _erased.Add(() => sheet.Stamps.Add(stamp));
                    erased = true;
                }
            }
            for (int i = sheet.Notes.Count - 1; i >= 0; i--)
            {
                Note note = sheet.Notes[i];
                if ((note.Position - point).magnitude <= radius + note.Size)
                {
                    sheet.Notes.RemoveAt(i);
                    _erased.Add(() => sheet.Notes.Add(note));
                    erased = true;
                }
            }
            if (erased)
            {
                _view.Refresh();
            }
        }

        private static bool Touches(List<Vector2> points, Vector2 point, float reach)
        {
            float reach2 = reach * reach;
            foreach (Vector2 p in points)
            {
                if ((p - point).sqrMagnitude <= reach2)
                {
                    return true;
                }
            }
            return false;
        }

        private void Done(Action undo)
        {
            _undo.Add(undo);
            Changed?.Invoke();
        }

        private bool HasRoomForMark()
        {
            if (Sheet.MarkCount < MaxMarks)
            {
                return true;
            }
            WarnFull();
            return false;
        }

        private void WarnFull()
        {
            if (!_warnedFull)
            {
                _warnedFull = true;
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "This sheet is full");
            }
        }

        private Vector2 Clamp(Vector2 point)
        {
            return _view.Bounded ? new Vector2(Mathf.Clamp(point.x, 0f, Sheet.Aspect), Mathf.Clamp01(point.y)) : point;
        }
    }
}
