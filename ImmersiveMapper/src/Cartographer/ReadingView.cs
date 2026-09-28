using UnityEngine;
using UnityEngine.UI;

namespace Cartographer
{
    /// <summary>
    /// The sheet held low in front of you while you walk: bottom of the screen, with the page's name and the tally above
    /// it. A draft shows whole; the master copy shows the part last looked at in the drawing view. Takes no clicks.
    /// </summary>
    internal sealed class ReadingView
    {
        private readonly Transform _canvas;
        private readonly RectTransform _root;
        private readonly CanvasView _draft;
        private readonly CanvasView _master;
        private readonly Text _title;
        private readonly Text _tally;
        private float _height;
        private float _lift;
        private int _paces = -1;
        private bool _onMaster;

        public ReadingView(Transform canvas)
        {
            _canvas = canvas;
            _root = Ui.Rect("IM_MapReading", canvas);
            _draft = new CanvasView(_root, "draft", true) { Zoomable = false };
            _master = new CanvasView(_root, "master", false) { Zoomable = false, MinZoom = 0.02f, MaxZoom = 8f };
            _title = Ui.HudText("title", _root, 18, TextAnchor.LowerLeft);
            _tally = Ui.HudText("tally", _root, 18, TextAnchor.LowerRight);
            SetVisible(false);
        }

        /// <summary>False once the game's GUI was torn down (logout); the view is built again next time.</summary>
        public bool Alive => _root != null;

        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        public void Show(CaseSession session)
        {
            Layout(true);
            _onMaster = session.OnMaster;
            _title.text = session.PageTitle(session.Page);
            _draft.Root.gameObject.SetActive(!_onMaster);
            _master.Root.gameObject.SetActive(_onMaster);
            if (_onMaster)
            {
                _master.Show(session.Master);
                if (session.MasterViewSet)
                {
                    _master.SetView(session.MasterZoom, session.MasterCenter);
                }
                else
                {
                    _master.Fit();
                }
            }
            else
            {
                _draft.Show(session.Current);
                _draft.Fit();
            }
        }

        public void Update(int paces)
        {
            Layout(false);
            (_onMaster ? _master : _draft).Update();
            if (paces != _paces)
            {
                _paces = paces;
                _tally.text = paces == 1 ? "1 pace" : $"{paces} paces";
            }
        }

        private void Layout(bool force)
        {
            float canvas = Ui.CanvasHeight(_canvas);
            float height = KitConfig.ReadingSize.Value * canvas;
            float lift = KitConfig.ReadingLift.Value * canvas;
            if (!force && Mathf.Approximately(height, _height) && Mathf.Approximately(lift, _lift))
            {
                return;
            }
            _height = height;
            _lift = lift;
            var size = new Vector2(height * Sheet.Aspect, height);
            Ui.Place(_root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, lift), size);
            var center = new Vector2(0.5f, 0.5f);
            Ui.Place(_draft.Root, center, center, Vector2.zero, Vector2.zero);
            _draft.SetSize(size);
            Ui.Place(_master.Root, center, center, Vector2.zero, Vector2.zero);
            _master.SetSize(size);

            int font = Mathf.Max(12, Mathf.RoundToInt(canvas * 0.02f));
            _title.fontSize = font;
            _tally.fontSize = font;
            Ui.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 2f), new Vector2(size.x * 0.6f, font * 1.5f));
            Ui.Place(_tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(size.x * 0.4f, font * 1.5f));
        }
    }
}
