using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// A draft laid over the master map like tracing paper: see-through, and moved (drag), turned (Shift + drag) and
    /// resized (Ctrl + wheel) until it matches. Or fitted from two places: click one on the draft and the same one on the
    /// master, then a second pair, and the draft is moved, turned and sized so both pairs meet.
    /// </summary>
    internal sealed class DraftOverlay : ICanvasTool
    {
        private enum Pick
        {
            None,
            DraftA,
            MasterA,
            DraftB,
            MasterB,
        }

        private static readonly Vector2 Middle = new Vector2(Sheet.Aspect * 0.5f, 0.5f);

        /// <summary>Canvas position of the draft's lower-left corner.</summary>
        public Vector2 Position;
        /// <summary>Degrees, counter-clockwise.</summary>
        public float Angle;
        /// <summary>Master canvas units per draft unit.</summary>
        public float Scale = 1f;
        /// <summary>Tells the table what to show as the hint.</summary>
        public Action<string> Hint;

        private readonly CanvasView _master;
        private readonly CanvasView _paper;
        private readonly RectTransform[] _markers = new RectTransform[4];
        private Pick _pick;
        private Vector2 _draftA;
        private Vector2 _masterA;
        private Vector2 _draftB;
        private bool _dragging;
        private bool _turning;
        private Vector2 _grab;
        private float _grabAngle;
        private float _startAngle;

        public DraftOverlay(CanvasView master)
        {
            _master = master;
            _paper = new CanvasView(master.Overlay, "draft", true) { Zoomable = false };
            _paper.Root.anchorMin = _paper.Root.anchorMax = Vector2.zero;
            _paper.Root.pivot = Vector2.zero;
            var group = _paper.Root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.8f;
            group.blocksRaycasts = false;
            group.interactable = false;
            if (_paper.PaperImage != null)
            {
                _paper.PaperImage.color = new Color(1f, 1f, 1f, 0.45f);
            }
            for (int i = 0; i < _markers.Length; i++)
            {
                _markers[i] = Ui.Rect("pin", master.Overlay);
                _markers[i].anchorMin = _markers[i].anchorMax = Vector2.zero;
                _markers[i].pivot = new Vector2(0.5f, 0.5f);
                var dot = _markers[i].gameObject.AddComponent<Image>();
                dot.color = i % 2 == 0 ? new Color(0.75f, 0.2f, 0.1f, 0.95f) : new Color(0.1f, 0.35f, 0.75f, 0.95f);
                dot.raycastTarget = false;
                _markers[i].gameObject.SetActive(false);
            }
            master.ViewChanged += SizeMarkers;
            _paper.Root.gameObject.SetActive(false);
        }

        public bool Active => _paper.Root.gameObject.activeSelf;

        public bool Picking => _pick != Pick.None;

        public void Begin(Sheet draft)
        {
            _paper.Root.gameObject.SetActive(true);
            _paper.Root.SetAsLastSibling();
            _paper.SetSize(new Vector2(Sheet.Aspect, 1f) * _master.Ppu);
            _paper.Show(draft);
            _paper.Fit();
            // Start in the middle of the view, a little smaller than it.
            Rect view = _master.VisibleRect();
            Angle = 0f;
            Scale = Mathf.Min(view.height * 0.6f, view.width * 0.6f / Sheet.Aspect);
            Position = view.center - Middle * Scale;
            _pick = Pick.None;
            HideMarkers();
            Apply();
        }

        public void End()
        {
            _pick = Pick.None;
            _dragging = false;
            HideMarkers();
            _paper.Root.gameObject.SetActive(false);
        }

        public void StartTwoPoint()
        {
            _pick = Pick.DraftA;
            HideMarkers();
            Hint?.Invoke("Two-point fit: click a place on the draft you can also find on the master (1 of 2).");
        }

        public Vector2 ToMaster(Vector2 draft)
        {
            return Position + Rotate(draft * Scale, Angle);
        }

        public Vector2 ToDraft(Vector2 master)
        {
            return Rotate(master - Position, -Angle) / Scale;
        }

        public void Down(Vector2 point, PointerEventData data)
        {
            if (_pick != Pick.None)
            {
                PickAt(point);
                return;
            }
            _dragging = true;
            _turning = ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift);
            if (_turning)
            {
                Vector2 center = ToMaster(Middle);
                _grabAngle = AngleOf(point - center);
                _startAngle = Angle;
            }
            else
            {
                _grab = point - Position;
            }
        }

        public void Drag(Vector2 point, PointerEventData data)
        {
            if (!_dragging)
            {
                return;
            }
            if (_turning)
            {
                Vector2 center = ToMaster(Middle);
                Angle = _startAngle + Mathf.DeltaAngle(_grabAngle, AngleOf(point - center));
                Position = center - Rotate(Middle * Scale, Angle);
            }
            else
            {
                Position = point - _grab;
            }
            Apply();
        }

        public void Up(Vector2 point, PointerEventData data)
        {
            _dragging = false;
        }

        public bool Scroll(Vector2 point, float delta)
        {
            if (!ZInput.GetKey(KeyCode.LeftControl) && !ZInput.GetKey(KeyCode.RightControl))
            {
                return false;
            }
            Vector2 center = ToMaster(Middle);
            Scale = Mathf.Clamp(Scale * (delta > 0f ? 1.06f : 1f / 1.06f), 0.005f, 200f);
            Position = center - Rotate(Middle * Scale, Angle);
            Apply();
            return true;
        }

        private void PickAt(Vector2 point)
        {
            switch (_pick)
            {
                case Pick.DraftA:
                    if (!OnDraft(point, out _draftA))
                    {
                        return;
                    }
                    ShowMarker(0, point);
                    _pick = Pick.MasterA;
                    Hint?.Invoke("Now click where that place is on the master.");
                    break;
                case Pick.MasterA:
                    _masterA = point;
                    ShowMarker(1, point);
                    _pick = Pick.DraftB;
                    Hint?.Invoke("Click a second place on the draft, far from the first (2 of 2).");
                    break;
                case Pick.DraftB:
                    if (!OnDraft(point, out Vector2 draftB))
                    {
                        return;
                    }
                    if ((draftB - _draftA).magnitude < 0.05f)
                    {
                        Hint?.Invoke("Too close to the first place; pick one further away on the draft.");
                        return;
                    }
                    _draftB = draftB;
                    ShowMarker(2, point);
                    _pick = Pick.MasterB;
                    Hint?.Invoke("And where is that second place on the master?");
                    break;
                case Pick.MasterB:
                    Vector2 onDraft = _draftB - _draftA;
                    Vector2 onMaster = point - _masterA;
                    if (onMaster.magnitude < 0.0005f)
                    {
                        Hint?.Invoke("That's the same spot as the first; click where the second place is.");
                        return;
                    }
                    Scale = Mathf.Clamp(onMaster.magnitude / onDraft.magnitude, 0.005f, 200f);
                    Angle = AngleOf(onMaster) - AngleOf(onDraft);
                    Position = _masterA - Rotate(_draftA * Scale, Angle);
                    _pick = Pick.None;
                    HideMarkers();
                    Apply();
                    Hint?.Invoke("Fitted. Nudge it if needed, then Copy as is or Trace.");
                    break;
            }
        }

        private bool OnDraft(Vector2 master, out Vector2 draft)
        {
            draft = ToDraft(master);
            bool inside = draft.x >= 0f && draft.x <= Sheet.Aspect && draft.y >= 0f && draft.y <= 1f;
            if (!inside)
            {
                Hint?.Invoke("That's off the draft; click a place on the see-through sheet.");
            }
            return inside;
        }

        private void Apply()
        {
            RectTransform root = _paper.Root;
            root.anchoredPosition = Position * _master.Ppu;
            root.localRotation = Quaternion.Euler(0f, 0f, Angle);
            root.localScale = new Vector3(Scale, Scale, 1f);
        }

        private void ShowMarker(int index, Vector2 point)
        {
            RectTransform marker = _markers[index];
            marker.gameObject.SetActive(true);
            marker.SetAsLastSibling();
            marker.anchoredPosition = point * _master.Ppu;
            SizeMarkers();
        }

        // Pins stay the same size on screen whatever the zoom.
        private void SizeMarkers()
        {
            float size = Ui.CanvasHeight(GUIManager.CustomGUIBack != null ? GUIManager.CustomGUIBack.transform : _master.Root) * 0.012f;
            foreach (RectTransform marker in _markers)
            {
                marker.sizeDelta = new Vector2(size, size);
                marker.localScale = Vector3.one / Mathf.Max(_master.Zoom, 0.0001f);
            }
        }

        private void HideMarkers()
        {
            foreach (RectTransform marker in _markers)
            {
                marker.gameObject.SetActive(false);
            }
        }

        private static float AngleOf(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
