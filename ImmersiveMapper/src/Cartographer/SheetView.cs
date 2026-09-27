using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Shows one sheet: parchment, strokes, stamps and notes. The owner sizes <see cref="Root"/>; stamps and notes are
    /// laid out from its height each time the sheet is shown or changes.
    /// </summary>
    internal sealed class SheetView
    {
        /// <summary>Stamp size as a share of the sheet's height.</summary>
        public const float StampSize = 0.065f;
        /// <summary>Note text height as a share of the sheet's height.</summary>
        public const float NoteSize = 0.036f;

        public readonly RectTransform Root;
        public readonly RawImage Paper;

        private readonly StrokeGraphic _strokes;
        private readonly RectTransform _marks;
        private Sheet _sheet;

        public SheetView(Transform parent, string name)
        {
            Root = Ui.Rect(name, parent);
            Paper = Root.gameObject.AddComponent<RawImage>();
            Paper.texture = PaperTextures.Paper;
            Paper.raycastTarget = false;

            RectTransform strokes = Ui.Rect("strokes", Root);
            Ui.Stretch(strokes);
            _strokes = strokes.gameObject.AddComponent<StrokeGraphic>();
            _strokes.raycastTarget = false;

            _marks = Ui.Rect("marks", Root);
            Ui.Stretch(_marks);
        }

        public Sheet Sheet => _sheet;

        public StrokeGraphic Strokes => _strokes;

        /// <summary>Sizes the sheet to a height in canvas units, at the sheet's fixed aspect.</summary>
        public void SetHeight(float height)
        {
            Root.sizeDelta = new Vector2(height * Sheet.Aspect, height);
        }

        public void Show(Sheet sheet)
        {
            _sheet = sheet;
            _strokes.Sheet = sheet;
            Refresh();
        }

        /// <summary>Redraws everything; call after the sheet changed or the view was resized.</summary>
        public void Refresh()
        {
            _strokes.SetVerticesDirty();
            for (int i = _marks.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_marks.GetChild(i).gameObject);
            }
            if (_sheet == null)
            {
                return;
            }
            float height = Root.sizeDelta.y;
            foreach (Stamp stamp in _sheet.Stamps)
            {
                AddStamp(stamp, height);
            }
            foreach (Note note in _sheet.Notes)
            {
                AddNote(note, height);
            }
        }

        /// <summary>Converts a screen point to sheet space. False when the point is off the sheet.</summary>
        public bool ScreenToSheet(Vector2 screen, Camera camera, out Vector2 point)
        {
            point = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, camera, out Vector2 local))
            {
                return false;
            }
            Rect rect = Root.rect;
            point = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        private void AddStamp(Stamp stamp, float height)
        {
            Stamps.Kind kind = Stamps.Get(stamp.Id);
            Sprite sprite = kind?.Sprite;
            if (sprite == null)
            {
                return;
            }
            RectTransform rect = Ui.Rect("stamp", _marks);
            float size = StampSize * height;
            Ui.Place(rect, stamp.Position, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // Tinted art takes the ink colour; coloured art is dimmed a little so it sits on the parchment.
            image.color = kind.Tinted ? (Color)StrokeGraphic.Palette[0] : new Color(0.82f, 0.78f, 0.72f, 0.95f);
        }

        private void AddNote(Note note, float height)
        {
            int size = Mathf.Max(8, Mathf.RoundToInt(NoteSize * height));
            Text text = Ui.Text("note", _marks, size, StrokeGraphic.Palette[0], TextAnchor.MiddleCenter);
            text.text = note.Text;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(text.rectTransform, note.Position, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 12f, size * 1.4f));
        }
    }
}
