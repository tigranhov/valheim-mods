using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// The sheet held low in front of you while you walk: bottom of the screen, with the sheet's name and the tally above it.
    /// Nothing here takes clicks; the game keeps its controls.
    /// </summary>
    internal sealed class ReadingView
    {
        private readonly Transform _canvas;
        private readonly SheetView _sheet;
        private readonly Text _title;
        private readonly Text _tally;
        private float _height;
        private float _lift;
        private int _paces = -1;

        public ReadingView(Transform canvas)
        {
            _canvas = canvas;
            _sheet = new SheetView(canvas, "IM_MapReading");
            _title = Ui.HudText("title", _sheet.Root, 18, TextAnchor.LowerLeft);
            _tally = Ui.HudText("tally", _sheet.Root, 18, TextAnchor.LowerRight);
            SetVisible(false);
        }

        /// <summary>False once the game's GUI was torn down (logout); the view is built again next time.</summary>
        public bool Alive => _sheet.Root != null;

        public bool Visible => _sheet.Root.gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_sheet.Root.gameObject.activeSelf != visible)
            {
                _sheet.Root.gameObject.SetActive(visible);
            }
        }

        public void Show(Sheet sheet, string title)
        {
            Layout(true);
            _title.text = title;
            _sheet.Show(sheet);
        }

        public void Update(int paces)
        {
            if (Layout(false))
            {
                _sheet.Refresh();
            }
            if (paces != _paces)
            {
                _paces = paces;
                _tally.text = paces == 1 ? "1 pace" : $"{paces} paces";
            }
        }

        /// <returns>True when the size changed.</returns>
        private bool Layout(bool force)
        {
            float canvas = Ui.CanvasHeight(_canvas);
            float height = KitConfig.ReadingSize.Value * canvas;
            float lift = KitConfig.ReadingLift.Value * canvas;
            if (!force && Mathf.Approximately(height, _height) && Mathf.Approximately(lift, _lift))
            {
                return false;
            }
            _height = height;
            _lift = lift;
            Ui.Place(_sheet.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, lift), Vector2.zero);
            _sheet.SetHeight(height);

            int font = Mathf.Max(12, Mathf.RoundToInt(canvas * 0.02f));
            _title.fontSize = font;
            _tally.fontSize = font;
            float width = height * Sheet.Aspect;
            Ui.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 2f), new Vector2(width * 0.6f, font * 1.5f));
            Ui.Place(_tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(width * 0.4f, font * 1.5f));
            return true;
        }
    }
}
