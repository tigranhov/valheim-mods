using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cartographer
{
    /// <summary>
    /// A journal leg as a string of true length at the master's scale: press where the leg began and drag toward where
    /// it went; the string keeps its length, you only choose the direction. Let go to lay it down as a dotted line.
    /// Only the length comes from the tally; the direction is the player's memory.
    /// </summary>
    internal sealed class MeasureTool : ICanvasTool
    {
        private readonly CanvasView _view;
        private Stroke _live;
        private bool _pinned;
        private Vector2 _anchor;

        public Leg Leg { get; private set; }
        public float Length { get; private set; }
        /// <summary>Called with the laid string, ready to go on the master.</summary>
        public Action<Stroke> Placed;

        public MeasureTool(CanvasView view)
        {
            _view = view;
        }

        /// <param name="anchor">Where the string starts (the end of the previous leg), or null to press a start.</param>
        public void Begin(Leg leg, float length, Vector2? anchor)
        {
            Leg = leg;
            Length = length;
            _pinned = anchor.HasValue;
            _anchor = anchor ?? Vector2.zero;
            _live = null;
            _view.SetLive(null);
        }

        public void End()
        {
            Leg = null;
            _live = null;
            _view.SetLive(null);
        }

        public void Down(Vector2 point, PointerEventData data)
        {
            if (Leg == null)
            {
                return;
            }
            // Shift pins the start somewhere new; otherwise a chained leg starts where the last one ended.
            if (!_pinned || ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift))
            {
                _anchor = point;
                _pinned = true;
            }
            Aim(point);
        }

        public void Drag(Vector2 point, PointerEventData data)
        {
            if (Leg != null)
            {
                Aim(point);
            }
        }

        public void Up(Vector2 point, PointerEventData data)
        {
            Stroke laid = _live;
            _live = null;
            _view.SetLive(null);
            if (laid != null && Leg != null)
            {
                Placed?.Invoke(laid);
            }
        }

        public bool Scroll(Vector2 point, float delta)
        {
            return false;
        }

        public void RightClick(Vector2 point)
        {
        }

        private void Aim(Vector2 point)
        {
            Vector2 direction = point - _anchor;
            // Too little drag to tell a direction yet.
            if (direction.magnitude * _view.Zoom < 0.01f)
            {
                _live = null;
                _view.SetLive(null);
                return;
            }
            _live = new Stroke
            {
                Pen = Pen.Ink,
                Color = Inks.Black,
                Style = LineStyle.Dotted,
                Width = Inks.PenWidth(Pen.Ink, BrushSize.Medium) / _view.Zoom,
            };
            _live.Points.Add(_anchor);
            _live.Points.Add(_anchor + direction.normalized * Length);
            _view.SetLive(_live);
        }
    }
}
