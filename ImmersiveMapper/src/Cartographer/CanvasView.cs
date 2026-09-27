using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>What a view's owner does with left-button input on the canvas, in canvas units.</summary>
    internal interface ICanvasTool
    {
        void Down(Vector2 point, PointerEventData data);
        void Drag(Vector2 point, PointerEventData data);
        void Up(Vector2 point, PointerEventData data);
        /// <summary>True when the tool used the wheel itself (the view then doesn't zoom).</summary>
        bool Scroll(Vector2 point, float delta);
    }

    /// <summary>
    /// A window onto a drawing that zooms (wheel, around the cursor) and pans (middle mouse, or Space + left mouse).
    /// Bounded: one draft sheet, which always fills the view at the widest zoom. Unbounded: a master map on endless
    /// parchment, with an optional grid. Zoom moves the drawing as a whole, so the mesh isn't rebuilt while zooming.
    /// </summary>
    internal sealed class CanvasView
    {
        private const float WheelStep = 1.18f;
        private const float NoteRelayoutDelay = 0.2f;

        public readonly RectTransform Root;
        /// <summary>Holds the drawing, in canvas units times <see cref="Ppu"/>.</summary>
        public readonly RectTransform Content;
        /// <summary>Above the drawing, in the same space: for things laid over it, like a draft.</summary>
        public readonly RectTransform Overlay;
        public readonly bool Bounded;

        public float MinZoom = 1f;
        public float MaxZoom = 6f;
        public bool Zoomable = true;
        public ICanvasTool Tool;
        public event Action ViewChanged;

        private readonly Image _mask;
        private readonly RawImage _tiledPaper;
        private readonly RawImage _sheetPaper;
        private readonly GridGraphic _grid;
        private readonly RectTransform[] _groups = new RectTransform[3];
        private readonly List<DrawingLayer>[] _chunks = { new List<DrawingLayer>(), new List<DrawingLayer>(), new List<DrawingLayer>() };
        private readonly int[] _cursor = new int[3];
        private readonly DrawingLayer _live;
        private readonly RectTransform _marks;
        private readonly List<KeyValuePair<Text, Note>> _noteTexts = new List<KeyValuePair<Text, Note>>();

        private float _ppu = 100f;
        private float _zoom = 1f;
        private Vector2 _pan;
        private float _canvasScale = 1f;
        private float _notesDirtyAt = -1f;

        public CanvasView(Transform parent, string name, bool bounded)
        {
            Bounded = bounded;
            Root = Ui.Rect(name, parent);
            _mask = Root.gameObject.AddComponent<Image>();
            _mask.color = Color.white;
            _mask.raycastTarget = false;
            Root.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Root.gameObject.AddComponent<CanvasInput>().View = this;

            if (!bounded)
            {
                _tiledPaper = Ui.Rect("paper", Root).gameObject.AddComponent<RawImage>();
                Ui.Stretch(_tiledPaper.rectTransform);
                _tiledPaper.texture = PaperTextures.PaperTile;
                _tiledPaper.raycastTarget = false;
                _grid = Ui.Rect("grid", Root).gameObject.AddComponent<GridGraphic>();
                Ui.Stretch(_grid.rectTransform);
                _grid.raycastTarget = false;
                _grid.enabled = false;
            }

            Content = Ui.Rect("content", Root);
            Content.anchorMin = Content.anchorMax = new Vector2(0.5f, 0.5f);
            Content.pivot = Vector2.zero;
            Content.sizeDelta = Vector2.zero;

            if (bounded)
            {
                _sheetPaper = Ui.Rect("paper", Content).gameObject.AddComponent<RawImage>();
                _sheetPaper.rectTransform.anchorMin = _sheetPaper.rectTransform.anchorMax = Vector2.zero;
                _sheetPaper.rectTransform.pivot = Vector2.zero;
                _sheetPaper.texture = PaperTextures.Paper;
                _sheetPaper.raycastTarget = false;
            }
            // Washes and fills, then traced guides, then lines: each group may hold several meshes.
            for (int i = 0; i < _groups.Length; i++)
            {
                _groups[i] = Layer(Content, i == 0 ? "washes" : i == 1 ? "guides" : "lines");
            }
            _live = NewLayer(Layer(Content, "live"));
            _marks = Layer(Content, "marks");
            Overlay = Layer(Content, "overlay");
        }

        public bool Alive => Root != null;

        /// <summary>A bounded view's parchment (null for a master map's endless one).</summary>
        public RawImage PaperImage => _sheetPaper;

        public Sheet Sheet { get; private set; }

        public float Zoom => _zoom;

        /// <summary>Viewport units per canvas unit at zoom 1: the view's height (a bounded sheet fits the view).</summary>
        public float Ppu => _ppu;

        /// <summary>Viewport units per canvas unit right now.</summary>
        public float Scale => _ppu * _zoom;

        public bool Interactive
        {
            get => _mask.raycastTarget;
            set => _mask.raycastTarget = value;
        }

        public bool ShowGrid
        {
            get => _grid != null && _grid.enabled;
            set
            {
                if (_grid != null)
                {
                    _grid.enabled = value;
                    ApplyView();
                }
            }
        }

        /// <summary>The canvas point in the middle of the view.</summary>
        public Vector2 Center => -_pan / Scale;

        /// <summary>Sizes the view (canvas units stay the same: zoom 1 shows one sheet height).</summary>
        public void SetSize(Vector2 size)
        {
            Root.sizeDelta = size;
            Canvas canvas = Root.GetComponentInParent<Canvas>();
            _canvasScale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float ppu = Mathf.Max(Bounded ? Mathf.Min(size.y, size.x / Sheet.Aspect) : size.y, 1f);
            bool changed = !Mathf.Approximately(ppu, _ppu);
            Vector2 center = Center;
            _ppu = ppu;
            if (_sheetPaper != null)
            {
                _sheetPaper.rectTransform.sizeDelta = new Vector2(Sheet.Aspect, 1f) * _ppu;
            }
            SetView(_zoom, center);
            if (changed)
            {
                Refresh();
            }
        }

        public void Show(Sheet sheet)
        {
            Sheet = sheet;
            Refresh();
        }

        /// <summary>Rebuilds every mesh and mark; after changes other than adding one thing.</summary>
        public void Refresh()
        {
            foreach (List<DrawingLayer> chunks in _chunks)
            {
                foreach (DrawingLayer chunk in chunks)
                {
                    chunk.Clear();
                    chunk.Ppu = _ppu;
                }
            }
            _live.Ppu = _ppu;
            Array.Clear(_cursor, 0, _cursor.Length);
            if (Sheet != null)
            {
                foreach (Fill fill in Sheet.Fills)
                {
                    Place(0, fill, null, false);
                }
                foreach (Stroke stroke in Sheet.Strokes)
                {
                    Place(GroupOf(stroke), null, stroke, false);
                }
            }
            foreach (List<DrawingLayer> chunks in _chunks)
            {
                foreach (DrawingLayer chunk in chunks)
                {
                    chunk.SetVerticesDirty();
                }
            }
            RebuildMarks();
        }

        public void Added(Stroke stroke)
        {
            Place(GroupOf(stroke), null, stroke, true);
        }

        public void Added(Fill fill)
        {
            Place(0, fill, null, true);
        }

        public void Added(Stamp stamp)
        {
            AddStamp(stamp);
        }

        public void Added(Note note)
        {
            AddNote(note);
        }

        /// <summary>Shows a stroke that isn't on the drawing yet (being drawn), or nothing.</summary>
        public void SetLive(Stroke stroke)
        {
            _live.Clear();
            if (stroke != null)
            {
                _live.Strokes.Add(stroke);
            }
            _live.SetVerticesDirty();
        }

        public void SetView(float zoom, Vector2 center)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _pan = -center * Scale;
            ApplyView();
        }

        /// <summary>Bounded: the whole sheet. Unbounded: everything drawn, or a sheet-sized start on an empty map.</summary>
        public void Fit()
        {
            if (Bounded || Sheet == null || !Sheet.TryGetBounds(out Rect bounds))
            {
                SetView(1f, new Vector2(Sheet.Aspect * 0.5f, 0.5f));
                return;
            }
            Vector2 size = Root.rect.size;
            float zoom = Mathf.Min(size.x / Mathf.Max(bounds.width, 0.05f), size.y / Mathf.Max(bounds.height, 0.05f)) / _ppu * 0.85f;
            SetView(zoom, bounds.center);
        }

        public void ZoomAt(Vector2 screen, Camera camera, float factor)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, camera, out Vector2 local))
            {
                return;
            }
            Vector2 v = local - Root.rect.center;
            Vector2 point = (v - _pan) / Scale;
            _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            _pan = v - point * Scale;
            ApplyView();
        }

        public void PanBy(Vector2 screenDelta)
        {
            _pan += screenDelta / _canvasScale;
            ApplyView();
        }

        /// <summary>The canvas point under a screen point; false when it's outside the view.</summary>
        public bool ScreenToCanvas(Vector2 screen, Camera camera, out Vector2 point)
        {
            point = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, screen, camera, out Vector2 local))
            {
                return false;
            }
            point = local / _ppu;
            return RectTransformUtility.RectangleContainsScreenPoint(Root, screen, camera);
        }

        /// <summary>Call each frame while shown: lays notes out again once zooming has settled, so their text stays sharp.</summary>
        public void Update()
        {
            if (_notesDirtyAt >= 0f && Time.unscaledTime - _notesDirtyAt > NoteRelayoutDelay)
            {
                _notesDirtyAt = -1f;
                foreach (KeyValuePair<Text, Note> pair in _noteTexts)
                {
                    LayoutNote(pair.Key, pair.Value);
                }
            }
        }

        private void ApplyView()
        {
            if (Bounded)
            {
                ClampToSheet();
            }
            Content.anchoredPosition = _pan;
            Content.localScale = new Vector3(_zoom, _zoom, 1f);
            if (_tiledPaper != null)
            {
                Rect view = VisibleRect();
                float tile = PaperTextures.TileUnits;
                _tiledPaper.uvRect = new Rect(view.xMin / tile, view.yMin / tile, view.width / tile, view.height / tile);
                if (_grid.enabled)
                {
                    _grid.View = view;
                    _grid.Scale = Scale;
                    _grid.SetVerticesDirty();
                }
            }
            _notesDirtyAt = Time.unscaledTime;
            ViewChanged?.Invoke();
        }

        /// <summary>The canvas area in view, in canvas units.</summary>
        public Rect VisibleRect()
        {
            Vector2 half = Root.rect.size * 0.5f;
            Vector2 min = (-half - _pan) / Scale;
            Vector2 max = (half - _pan) / Scale;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // A draft never slides out of view: centred while it's smaller than the view, edge to edge once it's bigger.
        private void ClampToSheet()
        {
            Vector2 view = Root.rect.size;
            Vector2 sheet = new Vector2(Sheet.Aspect, 1f) * Scale;
            _pan.x = sheet.x <= view.x ? -sheet.x * 0.5f : Mathf.Clamp(_pan.x, view.x * 0.5f - sheet.x, -view.x * 0.5f);
            _pan.y = sheet.y <= view.y ? -sheet.y * 0.5f : Mathf.Clamp(_pan.y, view.y * 0.5f - sheet.y, -view.y * 0.5f);
        }

        private static int GroupOf(Stroke stroke)
        {
            return stroke.Pen == Pen.Wash ? 0 : stroke.Pen == Pen.Guide ? 1 : 2;
        }

        // Items only ever go to the end of a group's last mesh (or a new one), so everything keeps its drawing order.
        private void Place(int group, Fill fill, Stroke stroke, bool dirty)
        {
            int estimate = fill != null ? StrokeMesh.EstimateVertices(fill) : StrokeMesh.EstimateVertices(stroke);
            List<DrawingLayer> chunks = _chunks[group];
            if (chunks.Count == 0)
            {
                chunks.Add(NewLayer(_groups[group]));
            }
            DrawingLayer chunk = chunks[_cursor[group]];
            if (chunk.Estimate > 0 && chunk.Estimate + estimate > StrokeMesh.MaxVertices)
            {
                _cursor[group]++;
                if (_cursor[group] >= chunks.Count)
                {
                    chunks.Add(NewLayer(_groups[group]));
                }
                chunk = chunks[_cursor[group]];
            }
            if (fill != null)
            {
                chunk.Fills.Add(fill);
            }
            else
            {
                chunk.Strokes.Add(stroke);
            }
            chunk.Estimate += estimate;
            if (dirty)
            {
                chunk.SetVerticesDirty();
            }
        }

        private DrawingLayer NewLayer(RectTransform parent)
        {
            RectTransform rect = Ui.Rect("mesh", parent);
            Ui.Stretch(rect);
            var layer = rect.gameObject.AddComponent<DrawingLayer>();
            layer.raycastTarget = false;
            layer.Ppu = _ppu;
            return layer;
        }

        private static RectTransform Layer(RectTransform parent, string name)
        {
            RectTransform rect = Ui.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private void RebuildMarks()
        {
            for (int i = _marks.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_marks.GetChild(i).gameObject);
            }
            _noteTexts.Clear();
            if (Sheet == null)
            {
                return;
            }
            foreach (Stamp stamp in Sheet.Stamps)
            {
                AddStamp(stamp);
            }
            foreach (Note note in Sheet.Notes)
            {
                AddNote(note);
            }
        }

        private void AddStamp(Stamp stamp)
        {
            Stamps.Kind kind = Stamps.Get(stamp.Id);
            Sprite sprite = kind?.Sprite;
            if (sprite == null)
            {
                return;
            }
            RectTransform rect = Ui.Rect("stamp", _marks);
            float size = stamp.Size * _ppu;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = stamp.Position * _ppu;
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // White art takes the ink colour; coloured art is dimmed a little so it sits on the parchment.
            image.color = kind.Tinted ? (Color)Inks.Of(Inks.Black) : new Color(0.82f, 0.78f, 0.72f, 0.95f);
        }

        private void AddNote(Note note)
        {
            Text text = Ui.Text("note", _marks, 14, Inks.Of(Inks.Black), TextAnchor.MiddleCenter);
            text.text = note.Text;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = note.Position * _ppu;
            LayoutNote(text, note);
            _noteTexts.Add(new KeyValuePair<Text, Note>(text, note));
        }

        // Text is drawn at its on-screen size and shrunk back by the zoom, so it stays crisp when zoomed in.
        private void LayoutNote(Text text, Note note)
        {
            if (text == null)
            {
                return;
            }
            float screenSize = note.Size * Scale;
            int font = Mathf.Clamp(Mathf.RoundToInt(screenSize), 1, 200);
            float shrink = font / Mathf.Max(screenSize, 0.01f) * _zoom;
            text.fontSize = font;
            text.rectTransform.localScale = Vector3.one / shrink;
            text.rectTransform.sizeDelta = new Vector2(font * 16f, font * 1.5f);
            text.enabled = screenSize >= 3f;
        }
    }

    /// <summary>Pointer input on a canvas view: panning and zooming here, the rest to the view's tool.</summary>
    internal sealed class CanvasInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public CanvasView View;
        private bool _panning;

        public void OnPointerDown(PointerEventData data)
        {
            if (View == null)
            {
                return;
            }
            bool space = ZInput.GetKey(KeyCode.Space);
            if (data.button == PointerEventData.InputButton.Middle || (data.button == PointerEventData.InputButton.Left && space))
            {
                _panning = View.Zoomable;
                return;
            }
            if (data.button == PointerEventData.InputButton.Left && View.ScreenToCanvas(data.position, data.pressEventCamera, out Vector2 point))
            {
                View.Tool?.Down(point, data);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (View == null)
            {
                return;
            }
            if (_panning)
            {
                View.PanBy(data.delta);
                return;
            }
            if (data.button == PointerEventData.InputButton.Left)
            {
                View.ScreenToCanvas(data.position, data.pressEventCamera, out Vector2 point);
                View.Tool?.Drag(point, data);
            }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (View == null)
            {
                return;
            }
            if (_panning)
            {
                _panning = false;
                return;
            }
            if (data.button == PointerEventData.InputButton.Left)
            {
                View.ScreenToCanvas(data.position, data.pressEventCamera, out Vector2 point);
                View.Tool?.Up(point, data);
            }
        }

        public void OnScroll(PointerEventData data)
        {
            if (View == null || Mathf.Approximately(data.scrollDelta.y, 0f))
            {
                return;
            }
            View.ScreenToCanvas(data.position, data.enterEventCamera, out Vector2 point);
            if (View.Tool != null && View.Tool.Scroll(point, Mathf.Sign(data.scrollDelta.y)))
            {
                return;
            }
            if (View.Zoomable)
            {
                View.ZoomAt(data.position, data.enterEventCamera, data.scrollDelta.y > 0f ? 1.18f : 1f / 1.18f);
            }
        }
    }
}
