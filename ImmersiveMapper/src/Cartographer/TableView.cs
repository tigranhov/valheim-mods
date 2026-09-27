using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Working at the cartography table: the table's master map (it grows as you draw, with a grid at its scale) and every
    /// tool; your case's drafts to lay over it and copy in; your journal's legs to lay down as measuring strings.
    /// Saved on the table every couple of seconds and when you leave.
    /// </summary>
    internal sealed class TableView
    {
        private const float SaveEvery = 2f;
        private const float BeatEvery = 3f;
        private const int LegButtons = 6;
        private static readonly Color Selected = new Color(1f, 0.75f, 0.35f);

        private readonly Transform _canvas;
        private readonly RectTransform _root;
        private readonly CanvasView _view;
        private readonly DrawTools _tools;
        private readonly ToolPanel _panel;
        private readonly DraftOverlay _overlay;
        private readonly MeasureTool _measure;

        private readonly Text _title;
        private readonly Button _grid;
        private readonly Button _scale;
        private readonly Button _fit;
        private readonly Button _clearGuides;
        private readonly Button _layCopy;
        private readonly RectTransform _bottom;
        private readonly Text _draftsLabel;
        private readonly Text _legsLabel;
        private readonly List<Button> _draftButtons = new List<Button>();
        private readonly List<Button> _legButtons = new List<Button>();
        private readonly RectTransform _placing;
        private readonly Button _twoPoint;
        private readonly Button _copy;
        private readonly Button _trace;
        private readonly Button _cancel;
        private readonly Text _hint;

        private MapTable _table;
        private CaseSession _case;
        private Sheet _master;
        private int _placingDraft = -1;
        private bool _dirty;
        private float _sinceSave;
        private float _sinceBeat;
        private bool _gridOn = true;
        private float _u = 10f;
        private float _left;
        private float _barY;

        public Action OnDone;

        public TableView(Transform canvas)
        {
            _canvas = canvas;
            _root = Ui.Rect("IM_MapTable", canvas);
            Ui.Stretch(_root);
            _root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            _view = new CanvasView(_root, "master", false) { Interactive = true, MinZoom = 0.02f, MaxZoom = 12f };
            _tools = new DrawTools(_view);
            _view.Tool = _tools;
            _panel = new ToolPanel(_root, _tools, () => OnDone?.Invoke());
            _overlay = new DraftOverlay(_view) { Hint = SetHint };
            _measure = new MeasureTool(_view) { Placed = LayString };

            _title = Ui.HudText("title", _root, 20, TextAnchor.MiddleLeft);
            _grid = Ui.Button("Grid", _root, new Vector2(100f, 36f));
            _grid.onClick.AddListener(() => { _gridOn = !_gridOn; _view.ShowGrid = _gridOn; RefreshBars(); });
            _scale = Ui.Button("Scale", _root, new Vector2(200f, 36f));
            _scale.onClick.AddListener(AskScale);
            _fit = Ui.Button("Fit", _root, new Vector2(100f, 36f));
            _fit.onClick.AddListener(() => _view.Fit());
            _clearGuides = Ui.Button("Clear guides", _root, new Vector2(140f, 36f));
            _clearGuides.onClick.AddListener(ClearGuides);
            _layCopy = Ui.Button("Lay your copy here", _root, new Vector2(200f, 36f));
            _layCopy.onClick.AddListener(LayCopy);

            _bottom = Ui.Rect("bottom", _root);
            _draftsLabel = Ui.HudText("drafts", _bottom, 16, TextAnchor.MiddleLeft);
            _legsLabel = Ui.HudText("legs", _bottom, 16, TextAnchor.MiddleLeft);

            _placing = Ui.Rect("placing", _root);
            _twoPoint = Ui.Button("Two-point fit", _placing, new Vector2(160f, 36f));
            _twoPoint.onClick.AddListener(() => _overlay.StartTwoPoint());
            _copy = Ui.Button("Copy as is", _placing, new Vector2(160f, 36f));
            _copy.onClick.AddListener(() => CopyDraft(false));
            _trace = Ui.Button("Trace", _placing, new Vector2(160f, 36f));
            _trace.onClick.AddListener(() => CopyDraft(true));
            _cancel = Ui.Button("Cancel", _placing, new Vector2(160f, 36f));
            _cancel.onClick.AddListener(EndPlacing);

            _hint = Ui.HudText("hint", _root, 16, TextAnchor.MiddleCenter);
            _tools.Selection.Changed += () =>
            {
                if (_view.Tool == _tools)
                {
                    DefaultHint();
                }
            };
            _root.gameObject.SetActive(false);
        }

        public bool Alive => _root != null;

        public bool IsOpen => _root.gameObject.activeSelf;

        public MapTable Table => _table;

        public void Open(MapTable table, CaseSession session)
        {
            _table = table;
            _case = session;
            _master = Sheet.FromBytes(TableAccess.ReadMaster(table));
            _dirty = false;
            _sinceSave = 0f;
            _sinceBeat = 0f;
            _tools.ClearUndo();
            _tools.MaxPoints = KitConfig.MaxMasterPoints.Value;
            _tools.MaxMarks = KitConfig.MaxMasterMarks.Value;
            _tools.Changed = () => { _dirty = true; RefreshBars(); };
            bool writable = !_master.Unreadable;
            _panel.Apply(_ => writable, true);

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Layout();
            _view.Show(_master);
            _view.ShowGrid = _gridOn;
            _view.Fit();
            EndPlacing();
            EndMeasure();
            if (!writable)
            {
                SetHint("This table's map was saved by a newer version of the mod; it can't be shown or changed here.");
            }
        }

        public void Close()
        {
            _tools.Finish();
            _tools.Deselect();
            EndPlacing();
            EndMeasure();
            if (_dirty)
            {
                Save();
            }
            TableLock.Release(_table);
            // Leaving the table refreshes your case's copy of its master (an empty master leaves your copy as it was).
            if (_case != null && !_master.Unreadable && !_master.IsEmpty)
            {
                MapCaseItem.SaveMasterBytes(_case.Item, _master.ToBytes());
            }
            _root.gameObject.SetActive(false);
            _table = null;
            _case = null;
        }

        public void Update()
        {
            _sinceSave += Time.unscaledDeltaTime;
            if (_dirty && _sinceSave >= SaveEvery)
            {
                Save();
            }
            _sinceBeat += Time.unscaledDeltaTime;
            if (_sinceBeat >= BeatEvery)
            {
                _sinceBeat = 0f;
                TableLock.Heartbeat(_table);
            }
            if (!_overlay.Active && (ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) && ZInput.GetKeyDown(KeyCode.Z))
            {
                _tools.Undo();
                _panel.Refresh();
            }
            if (_view.Tool == _tools)
            {
                _tools.UpdateKeys();
            }
            _view.Update();
        }

        /// <summary>Esc: stops placing a draft or laying strings. False when there was nothing to stop.</summary>
        public bool Cancel()
        {
            if (_overlay.Active)
            {
                EndPlacing();
                return true;
            }
            if (_measure.Leg != null)
            {
                EndMeasure();
                return true;
            }
            return false;
        }

        private void Save()
        {
            if (_table == null || _master.Unreadable)
            {
                return;
            }
            TableAccess.WriteMaster(_table, _master.ToBytes());
            _dirty = false;
            _sinceSave = 0f;
        }

        // ---- Drafts onto the master ----

        private void StartPlacing(int page)
        {
            EndMeasure();
            _tools.Finish();
            _tools.Deselect();
            _placingDraft = page;
            _overlay.Begin(_case.Drafts[page]);
            _view.Tool = _overlay;
            SetHint("Drag the draft to move it · Shift + drag to turn it · Ctrl + wheel to resize · or use Two-point fit · Esc to cancel");
            RefreshBars();
        }

        private void EndPlacing()
        {
            _overlay.End();
            _placingDraft = -1;
            if (_view.Tool == _overlay)
            {
                _view.Tool = _tools;
            }
            DefaultHint();
            RefreshBars();
        }

        private void CopyDraft(bool trace)
        {
            if (_placingDraft < 0 || _case == null || _master.Unreadable)
            {
                return;
            }
            int page = _placingDraft;
            Sheet draft = _case.Drafts[page];
            if (_master.PointCount + draft.PointCount > KitConfig.MaxMasterPoints.Value || _master.MarkCount + draft.MarkCount > KitConfig.MaxMasterMarks.Value)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "The master map is full");
                return;
            }
            float scale = _overlay.Scale;
            var strokes = new List<Stroke>();
            var fills = new List<Fill>();
            var stamps = new List<Stamp>();
            var notes = new List<Note>();
            foreach (Stroke stroke in draft.Strokes)
            {
                var copy = new Stroke { Pen = trace ? Pen.Guide : stroke.Pen, Color = stroke.Color, Style = stroke.Style, Width = stroke.Width * scale };
                foreach (Vector2 point in stroke.Points)
                {
                    copy.Points.Add(_overlay.ToMaster(point));
                }
                strokes.Add(copy);
            }
            foreach (Fill fill in draft.Fills)
            {
                // Traced, a filled area comes over as its outline, to paint again.
                if (trace)
                {
                    var outline = new Stroke { Pen = Pen.Guide, Width = Inks.PenWidth(Pen.Ink, BrushSize.Fine) * scale };
                    foreach (Vector2 point in fill.Points)
                    {
                        outline.Points.Add(_overlay.ToMaster(point));
                    }
                    outline.Points.Add(outline.Points[0]);
                    strokes.Add(outline);
                    continue;
                }
                var copy = new Fill { Color = fill.Color };
                foreach (Vector2 point in fill.Points)
                {
                    copy.Points.Add(_overlay.ToMaster(point));
                }
                fills.Add(copy);
            }
            foreach (Stamp stamp in draft.Stamps)
            {
                stamps.Add(new Stamp { Id = stamp.Id, Position = _overlay.ToMaster(stamp.Position), Size = stamp.Size * scale });
            }
            foreach (Note note in draft.Notes)
            {
                notes.Add(new Note { Text = note.Text, Position = _overlay.ToMaster(note.Position), Size = note.Size * scale });
            }
            _master.Strokes.AddRange(strokes);
            _master.Fills.AddRange(fills);
            _master.Stamps.AddRange(stamps);
            _master.Notes.AddRange(notes);

            // The draft is wiped once it's on the master; undo puts both back.
            CaseSession session = _case;
            session.WipeDraft(page);
            _tools.Record(() =>
            {
                strokes.ForEach(s => _master.Strokes.Remove(s));
                fills.ForEach(f => _master.Fills.Remove(f));
                stamps.ForEach(s => _master.Stamps.Remove(s));
                notes.ForEach(n => _master.Notes.Remove(n));
                session.Drafts[page] = draft;
                session.SaveDraft(page);
                RefreshBars();
            });
            EndPlacing();
            _view.Refresh();
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Sheet {page + 1} {(trace ? "traced" : "copied")} onto the master, and wiped");
        }

        // ---- Measuring strings ----

        private void StartMeasure(Leg leg, Vector2? anchor)
        {
            if (_overlay.Active)
            {
                EndPlacing();
            }
            _tools.Finish();
            _tools.Deselect();
            float squares = leg.Paces / (float)Mathf.Max(1, _master.PacesPerSquare);
            _measure.Begin(leg, squares * Sheet.GridSquare, anchor);
            _view.Tool = _measure;
            string label = string.IsNullOrEmpty(leg.Label) ? "" : $" ({leg.Label})";
            SetHint($"Leg {leg.Number}{label}: {leg.Paces} paces = {squares:0.#} squares. " +
                (anchor.HasValue ? "Drag toward where it went · Shift + press to start elsewhere" : "Press where it began and drag toward where it went")
                + " · Esc to stop");
        }

        private void EndMeasure()
        {
            _measure.End();
            if (_view.Tool == _measure)
            {
                _view.Tool = _tools;
            }
            DefaultHint();
        }

        private void LayString(Stroke stroke)
        {
            Leg leg = _measure.Leg;
            CaseSession session = _case;
            if (leg == null || session == null)
            {
                return;
            }
            _master.Strokes.Add(stroke);
            _view.Added(stroke);
            leg.Plotted = true;
            session.SaveJournal();
            _tools.Record(() =>
            {
                _master.Strokes.Remove(stroke);
                leg.Plotted = false;
                session.SaveJournal();
                RefreshBars();
            });
            // Chain on: the next leg in the journal starts where this one ended.
            Leg next = NextUnplotted(leg);
            if (next != null)
            {
                StartMeasure(next, stroke.Points[stroke.Points.Count - 1]);
            }
            else
            {
                EndMeasure();
            }
            RefreshBars();
        }

        private Leg NextUnplotted(Leg after)
        {
            List<Leg> legs = _case.Journal.Legs;
            int index = legs.IndexOf(after);
            for (int i = index + 1; i < legs.Count; i++)
            {
                if (!legs[i].Plotted)
                {
                    return legs[i];
                }
            }
            return null;
        }

        // ---- Table buttons ----

        private void AskScale()
        {
            TextPrompt.Ask("Paces per grid square", _master.PacesPerSquare.ToString(), 6, text =>
            {
                if (int.TryParse(text, out int paces) && paces > 0)
                {
                    _master.PacesPerSquare = Mathf.Clamp(paces, 1, 100000);
                    _dirty = true;
                    RefreshBars();
                }
            });
        }

        private void ClearGuides()
        {
            List<Stroke> guides = _master.Strokes.FindAll(s => s.Pen == Pen.Guide);
            if (guides.Count == 0)
            {
                return;
            }
            _master.Strokes.RemoveAll(s => s.Pen == Pen.Guide);
            _view.Refresh();
            _tools.Record(() => _master.Strokes.AddRange(guides));
        }

        // An empty table (an outpost's) can start from the master copy you carry.
        private void LayCopy()
        {
            Sheet copy = _case != null ? MapCaseItem.LoadMaster(_case.Item) : null;
            if (copy == null || copy.Unreadable || !_master.IsEmpty)
            {
                return;
            }
            _master.Strokes.AddRange(copy.Strokes);
            _master.Fills.AddRange(copy.Fills);
            _master.Stamps.AddRange(copy.Stamps);
            _master.Notes.AddRange(copy.Notes);
            int scale = _master.PacesPerSquare;
            _master.PacesPerSquare = copy.PacesPerSquare;
            _tools.Record(() => { _master.Clear(); _master.PacesPerSquare = scale; RefreshBars(); });
            _view.Refresh();
            _view.Fit();
        }

        private void RefreshBars()
        {
            if (_master == null)
            {
                return;
            }
            Tint(_grid, _gridOn);
            SetLabel(_scale, $"1 square = {_master.PacesPerSquare} paces");
            _clearGuides.gameObject.SetActive(_master.Strokes.Exists(s => s.Pen == Pen.Guide));
            _layCopy.gameObject.SetActive(_master.IsEmpty && _case != null && _case.HasMaster);
            _title.text = "Master map";

            bool placing = _overlay.Active;
            _placing.gameObject.SetActive(placing);
            _bottom.gameObject.SetActive(!placing);

            foreach (Button button in _draftButtons)
            {
                UnityEngine.Object.Destroy(button.gameObject);
            }
            _draftButtons.Clear();
            foreach (Button button in _legButtons)
            {
                UnityEngine.Object.Destroy(button.gameObject);
            }
            _legButtons.Clear();
            if (_case == null)
            {
                _draftsLabel.text = "No map case with you: drafts and legs come from a map case in your inventory.";
                _legsLabel.text = "";
            }
            else
            {
                _draftsLabel.text = "Drafts:";
                for (int i = 0; i < _case.Drafts.Length; i++)
                {
                    if (_case.Drafts[i].IsEmpty)
                    {
                        continue;
                    }
                    int page = i;
                    Button button = Ui.Button($"Sheet {i + 1}", _bottom, new Vector2(120f, 36f));
                    button.onClick.AddListener(() => StartPlacing(page));
                    _draftButtons.Add(button);
                }
                if (_draftButtons.Count == 0)
                {
                    _draftsLabel.text = "Drafts: all blank";
                }
                _legsLabel.text = "Legs:";
                foreach (Leg leg in _case.Journal.Legs)
                {
                    if (leg.Plotted)
                    {
                        continue;
                    }
                    Leg chosen = leg;
                    Button button = Ui.Button($"Leg {leg.Number}: {leg.Paces}", _bottom, new Vector2(140f, 36f));
                    button.onClick.AddListener(() => StartMeasure(chosen, null));
                    _legButtons.Add(button);
                    if (_legButtons.Count == LegButtons)
                    {
                        break;
                    }
                }
                if (_legButtons.Count == 0)
                {
                    _legsLabel.text = "Legs: none to lay";
                }
            }
            LayoutBars();
        }

        private void SetHint(string text)
        {
            _hint.text = text;
        }

        private void DefaultHint()
        {
            if (_tools.Selection.Any)
            {
                SetHint(string.Format("Selected {0}: drag to move · arrows nudge (Shift: more) · Ctrl + wheel resizes · a colour recolours · Delete erases · right-click elsewhere lets go", _tools.Selection.Describe()));
                return;
            }
            SetHint("Right-click to select · wheel to zoom · middle mouse or Space + drag to pan · Ctrl+Z undo · Esc to leave the table");
        }

        private static void Tint(Button button, bool selected)
        {
            if (button.image != null)
            {
                button.image.color = selected ? Selected : Color.white;
            }
        }

        private static void SetLabel(Button button, string text)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }

        // ---- Layout ----

        private void Layout()
        {
            var canvas = (RectTransform)_canvas;
            float h = Ui.CanvasHeight(_canvas);
            float w = canvas.rect.width > 1f ? canvas.rect.width : Screen.width;
            float u = h / 100f;
            _u = u;
            float panel = 18.6f * u;
            float stamps = 11.6f * u;
            float left = -w * 0.5f + 2f * u + panel + 2f * u;
            float right = w * 0.5f - 2f * u - stamps - 2f * u;
            float top = h * 0.5f - 7f * u;
            float bottom = -h * 0.5f + 11f * u;
            var center = new Vector2(0.5f, 0.5f);

            Ui.Place(_view.Root, center, center, new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f), Vector2.zero);
            _view.SetSize(new Vector2(right - left, top - bottom));
            _panel.Layout(new Vector2(-w * 0.5f + 2f * u, top), new Vector2(w * 0.5f - 2f * u - stamps, top), u);

            _left = left;
            _barY = h * 0.5f - 3.5f * u;
            _title.fontSize = Mathf.Max(12, Mathf.RoundToInt(2.4f * u));
            Ui.Place(_title.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(left, _barY), new Vector2(20f * u, 4f * u));

            Ui.Place(_bottom, center, new Vector2(0f, 0.5f), new Vector2(left, -h * 0.5f + 4f * u), new Vector2(right - left, 4.5f * u));
            Ui.Place(_placing, center, new Vector2(0f, 0.5f), new Vector2(left, -h * 0.5f + 4f * u), new Vector2(right - left, 4.5f * u));
            float px = 0f;
            foreach (Button button in new[] { _twoPoint, _copy, _trace, _cancel })
            {
                px = PlaceRowIn(button, px, 16f * u, u);
            }
            _hint.fontSize = Mathf.Max(11, Mathf.RoundToInt(1.8f * u));
            Ui.Place(_hint.rectTransform, center, new Vector2(0.5f, 0.5f), new Vector2((left + right) * 0.5f, -h * 0.5f + 8.5f * u), new Vector2(right - left, 3f * u));
            RefreshBars();
        }

        // Buttons come and go (Clear guides, Lay your copy here), so the rows are laid out again each time.
        private void LayoutBars()
        {
            float u = _u;
            float top = _left + 20f * u;
            top = PlaceRow(_grid, top, _barY, 8f * u, u);
            top = PlaceRow(_scale, top, _barY, 22f * u, u);
            top = PlaceRow(_fit, top, _barY, 8f * u, u);
            top = PlaceRow(_clearGuides, top, _barY, 14f * u, u);
            PlaceRow(_layCopy, top, _barY, 20f * u, u);

            float x = 0f;
            _draftsLabel.fontSize = Mathf.Max(11, Mathf.RoundToInt(1.9f * u));
            _legsLabel.fontSize = _draftsLabel.fontSize;
            float draftsWidth = _case == null ? 90f * u : 9f * u;
            Ui.Place(_draftsLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(draftsWidth, 4f * u));
            x += draftsWidth;
            foreach (Button button in _draftButtons)
            {
                x = PlaceRowIn(button, x, 11f * u, u);
            }
            x += 3f * u;
            Ui.Place(_legsLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(_legButtons.Count == 0 ? 20f * u : 6f * u, 4f * u));
            x += 6f * u;
            foreach (Button button in _legButtons)
            {
                x = PlaceRowIn(button, x, 13f * u, u);
            }
        }

        private static float PlaceRow(Button button, float x, float y, float width, float u)
        {
            if (!button.gameObject.activeSelf)
            {
                return x;
            }
            var rect = (RectTransform)button.transform;
            Ui.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, y), new Vector2(width, 4f * u));
            Ui.LabelSize(rect, u);
            return x + width + 1f * u;
        }

        private static float PlaceRowIn(Button button, float x, float width, float u)
        {
            var rect = (RectTransform)button.transform;
            Ui.Place(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 4f * u));
            Ui.LabelSize(rect, u);
            return x + width + 1f * u;
        }
    }
}
