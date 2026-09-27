using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Drawing in the field: the sheet large in the middle of the screen, tools on the left, stamps on the right, sheet
    /// tabs and the journal above. The game's input is blocked while it's open, so you stand still.
    /// Which tools show depends on the FieldDrawing setting.
    /// </summary>
    internal sealed class DrawingView
    {
        private enum Tool
        {
            Charcoal,
            Ink,
            Stamp,
            Note,
            Eraser,
        }

        private struct UndoStep
        {
            public int Page;
            public Action Undo;
        }

        // Sheet heights between recorded points of a line.
        private const float PointSpacing = 0.004f;
        private const float EraseRadius = 0.02f;
        private const int NoteLimit = 40;

        private static readonly Color Selected = new Color(1f, 0.75f, 0.35f);

        private readonly Transform _canvas;
        private readonly RectTransform _root;
        private readonly SheetView _sheet;
        private readonly RectTransform _toolbar;
        private readonly RectTransform _palette;
        private readonly RectTransform _tabs;
        private readonly RawImage _journal;
        private readonly Text _journalText;
        private readonly Button _endLeg;
        private readonly Text _hint;
        private readonly Dictionary<Tool, Button> _toolButtons = new Dictionary<Tool, Button>();
        private readonly List<Button> _colorButtons = new List<Button>();
        private readonly Dictionary<string, Button> _stampButtons = new Dictionary<string, Button>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly Button _undoButton;
        private readonly Button _doneButton;
        private readonly List<UndoStep> _undo = new List<UndoStep>();

        private CaseSession _session;
        private Tool _tool = Tool.Charcoal;
        private byte _color;
        private string _stampId = "point";
        private bool _onJournal;
        private Stroke _live;
        private List<Action> _erased;
        private bool _warnedFull;

        /// <summary>Called when the player asks to end the leg from the journal page.</summary>
        public Action OnEndLeg;
        /// <summary>Called when the player presses Done.</summary>
        public Action OnDone;

        public DrawingView(Transform canvas)
        {
            _canvas = canvas;
            _root = Ui.Rect("IM_MapDrawing", canvas);
            Ui.Stretch(_root);
            var dim = _root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            _sheet = new SheetView(_root, "sheet");
            _sheet.Paper.raycastTarget = true;
            _sheet.Paper.gameObject.AddComponent<SheetInput>().View = this;

            _journal = Ui.Rect("journal", _root).gameObject.AddComponent<RawImage>();
            _journal.texture = PaperTextures.Paper;
            _journalText = Ui.Text("legs", _journal.transform, 18, Ui.InkBrown, TextAnchor.UpperLeft);
            _endLeg = Ui.Button("End leg", _journal.transform, new Vector2(160f, 40f));
            _endLeg.onClick.AddListener(() => OnEndLeg?.Invoke());

            _toolbar = Ui.Rect("tools", _root);
            AddToolButton(Tool.Charcoal, "Charcoal");
            AddToolButton(Tool.Ink, "Ink");
            for (byte i = 0; i < StrokeGraphic.Palette.Length; i++)
            {
                byte color = i;
                Button swatch = Ui.Button("", _toolbar, new Vector2(40f, 40f));
                var fill = Ui.Rect("fill", swatch.transform).gameObject.AddComponent<Image>();
                Ui.Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(6f, 6f);
                fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
                fill.color = StrokeGraphic.Palette[i];
                fill.raycastTarget = false;
                swatch.onClick.AddListener(() => { _color = color; UpdateSelection(); });
                _colorButtons.Add(swatch);
            }
            AddToolButton(Tool.Stamp, "Stamp");
            AddToolButton(Tool.Note, "Note");
            AddToolButton(Tool.Eraser, "Eraser");
            _undoButton = Ui.Button("Undo", _toolbar, new Vector2(160f, 40f));
            _undoButton.onClick.AddListener(Undo);
            _doneButton = Ui.Button("Done", _toolbar, new Vector2(160f, 40f));
            _doneButton.onClick.AddListener(() => OnDone?.Invoke());

            _palette = Ui.Rect("stamps", _root);
            _tabs = Ui.Rect("tabs", _root);

            _hint = Ui.HudText("hint", _root, 16, TextAnchor.UpperCenter);
            _hint.text = "Left-click to draw · Ctrl+Z undo · Right-click or Esc to put the quill down";

            _root.gameObject.SetActive(false);
        }

        public bool Alive => _root != null;

        public bool IsOpen => _root.gameObject.activeSelf;

        public void Open(CaseSession session)
        {
            _session = session;
            _undo.Clear();
            _onJournal = false;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            BuildStampPalette();
            BuildTabs();
            ApplyFieldRules();
            Layout();
            ShowPage();
        }

        public void Close()
        {
            FinishLine();
            _erased = null;
            _root.gameObject.SetActive(false);
            _session = null;
        }

        public void Update()
        {
            if ((ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) && ZInput.GetKeyDown(KeyCode.Z))
            {
                Undo();
            }
        }

        /// <summary>The journal changed (a leg was ended): redraw it if it's showing.</summary>
        public void RefreshJournal()
        {
            if (_onJournal)
            {
                ShowJournal();
            }
        }

        // ---- Pointer input on the sheet ----

        internal void PointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || TextPrompt.Open || !IsAllowed(_tool) || !ToSheet(data, out Vector2 point))
            {
                return;
            }
            Sheet sheet = _session.Current;
            switch (_tool)
            {
                case Tool.Charcoal:
                case Tool.Ink:
                    _warnedFull = false;
                    _live = new Stroke { Pen = _tool == Tool.Ink ? Pen.Ink : Pen.Charcoal, Color = _tool == Tool.Ink ? _color : (byte)0 };
                    _live.Points.Add(point);
                    _sheet.Strokes.Live = _live;
                    _sheet.Strokes.SetVerticesDirty();
                    break;
                case Tool.Stamp:
                    if (HasRoomForMark(sheet))
                    {
                        var stamp = new Stamp { Id = _stampId, Position = point };
                        sheet.Stamps.Add(stamp);
                        Changed(() => sheet.Stamps.Remove(stamp));
                    }
                    break;
                case Tool.Note:
                    if (HasRoomForMark(sheet))
                    {
                        int page = _session.Page;
                        TextPrompt.Ask("Write a note", "", NoteLimit, text => AddNote(page, point, text));
                    }
                    break;
                case Tool.Eraser:
                    _erased = new List<Action>();
                    EraseAt(point);
                    break;
            }
        }

        internal void Drag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            ToSheet(data, out Vector2 point);
            point = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
            if (_live != null)
            {
                Vector2 last = _live.Points[_live.Points.Count - 1];
                Vector2 step = new Vector2((point.x - last.x) * Sheet.Aspect, point.y - last.y);
                if (step.magnitude < PointSpacing)
                {
                    return;
                }
                if (_session.Current.PointCount + _live.Points.Count >= KitConfig.MaxPointsPerSheet.Value)
                {
                    WarnFull();
                    return;
                }
                _live.Points.Add(point);
                _sheet.Strokes.SetVerticesDirty();
            }
            else if (_erased != null)
            {
                EraseAt(point);
            }
        }

        internal void PointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            FinishLine();
            if (_erased != null && _erased.Count > 0)
            {
                List<Action> restore = _erased;
                PushUndo(() =>
                {
                    for (int i = restore.Count - 1; i >= 0; i--)
                    {
                        restore[i]();
                    }
                });
                Save();
            }
            _erased = null;
        }

        private void FinishLine()
        {
            if (_live == null)
            {
                return;
            }
            Stroke stroke = _live;
            _live = null;
            _sheet.Strokes.Live = null;
            if (_session != null)
            {
                Sheet sheet = _session.Current;
                sheet.Strokes.Add(stroke);
                Changed(() => sheet.Strokes.Remove(stroke));
            }
        }

        private void AddNote(int page, Vector2 point, string text)
        {
            if (_session == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            Sheet sheet = _session.Drafts[page];
            var note = new Note { Text = text, Position = point };
            sheet.Notes.Add(note);
            PushUndo(() => sheet.Notes.Remove(note), page);
            _session.SaveDraft(page);
            if (page == _session.Page && !_onJournal)
            {
                _sheet.Refresh();
            }
        }

        private void EraseAt(Vector2 point)
        {
            Sheet sheet = _session.Current;
            bool erased = false;
            for (int i = sheet.Strokes.Count - 1; i >= 0; i--)
            {
                Stroke stroke = sheet.Strokes[i];
                if (Touches(stroke, point))
                {
                    sheet.Strokes.RemoveAt(i);
                    _erased.Add(() => sheet.Strokes.Add(stroke));
                    erased = true;
                }
            }
            for (int i = sheet.Stamps.Count - 1; i >= 0; i--)
            {
                Stamp stamp = sheet.Stamps[i];
                if (Near(stamp.Position, point, SheetView.StampSize * 0.5f))
                {
                    sheet.Stamps.RemoveAt(i);
                    _erased.Add(() => sheet.Stamps.Add(stamp));
                    erased = true;
                }
            }
            for (int i = sheet.Notes.Count - 1; i >= 0; i--)
            {
                Note note = sheet.Notes[i];
                if (Near(note.Position, point, SheetView.NoteSize))
                {
                    sheet.Notes.RemoveAt(i);
                    _erased.Add(() => sheet.Notes.Add(note));
                    erased = true;
                }
            }
            if (erased)
            {
                _sheet.Refresh();
            }
        }

        private static bool Touches(Stroke stroke, Vector2 point)
        {
            float reach = EraseRadius + StrokeGraphic.Width(stroke.Pen) * 0.5f;
            foreach (Vector2 p in stroke.Points)
            {
                if (Near(p, point, reach))
                {
                    return true;
                }
            }
            return false;
        }

        // Distance in sheet heights, so a circle on the sheet is round.
        private static bool Near(Vector2 a, Vector2 b, float radius)
        {
            return new Vector2((a.x - b.x) * Sheet.Aspect, a.y - b.y).sqrMagnitude <= radius * radius;
        }

        private bool HasRoomForMark(Sheet sheet)
        {
            if (sheet.MarkCount < KitConfig.MaxMarksPerSheet.Value)
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

        private bool ToSheet(PointerEventData data, out Vector2 point)
        {
            return _sheet.ScreenToSheet(data.position, data.pressEventCamera, out point);
        }

        // ---- Changes and undo ----

        private void Changed(Action undo)
        {
            PushUndo(undo);
            Save();
            _sheet.Refresh();
        }

        private void PushUndo(Action undo, int page = -1)
        {
            _undo.Add(new UndoStep { Page = page >= 0 ? page : _session.Page, Undo = undo });
        }

        private void Undo()
        {
            if (_session == null || _undo.Count == 0 || _live != null || _erased != null)
            {
                return;
            }
            UndoStep step = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            step.Undo();
            _session.SaveDraft(step.Page);
            if (step.Page != _session.Page || _onJournal)
            {
                SelectPage(step.Page);
            }
            else
            {
                _sheet.Refresh();
            }
        }

        private void Save()
        {
            _session.SaveDraft(_session.Page);
        }

        // ---- Pages, tools and layout ----

        private void AddToolButton(Tool tool, string label)
        {
            Button button = Ui.Button(label, _toolbar, new Vector2(160f, 40f));
            button.onClick.AddListener(() => { _tool = tool; UpdateSelection(); });
            _toolButtons[tool] = button;
        }

        private void BuildStampPalette()
        {
            if (_stampButtons.Count > 0)
            {
                return;
            }
            foreach (Stamps.Kind kind in Stamps.All)
            {
                if (kind.Sprite == null)
                {
                    continue;
                }
                string id = kind.Id;
                Button button = Ui.Button("", _palette, new Vector2(48f, 48f));
                var icon = Ui.Rect("icon", button.transform).gameObject.AddComponent<Image>();
                Ui.Stretch(icon.rectTransform);
                icon.rectTransform.offsetMin = new Vector2(5f, 5f);
                icon.rectTransform.offsetMax = new Vector2(-5f, -5f);
                icon.sprite = kind.Sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = kind.Tinted ? GUIManager.Instance.ValheimBeige : Color.white;
                button.onClick.AddListener(() => { _stampId = id; _tool = Tool.Stamp; UpdateSelection(); });
                _stampButtons[id] = button;
            }
            if (!_stampButtons.ContainsKey(_stampId))
            {
                foreach (string id in _stampButtons.Keys)
                {
                    _stampId = id;
                    break;
                }
            }
        }

        private void BuildTabs()
        {
            foreach (Button tab in _tabButtons)
            {
                UnityEngine.Object.Destroy(tab.gameObject);
            }
            _tabButtons.Clear();
            for (int i = 0; i < _session.Drafts.Length; i++)
            {
                int page = i;
                Button tab = Ui.Button($"Sheet {i + 1}", _tabs, new Vector2(120f, 36f));
                tab.onClick.AddListener(() => SelectPage(page));
                _tabButtons.Add(tab);
            }
            Button journal = Ui.Button("Journal", _tabs, new Vector2(120f, 36f));
            journal.onClick.AddListener(ShowJournal);
            _tabButtons.Add(journal);
        }

        private void ApplyFieldRules()
        {
            FieldDrawing rule = KitConfig.Drawing.Value;
            bool lines = rule == FieldDrawing.Everything || rule == FieldDrawing.Sketch;
            bool ink = rule == FieldDrawing.Everything;
            bool marks = rule != FieldDrawing.Nothing;
            _toolButtons[Tool.Charcoal].gameObject.SetActive(lines);
            _toolButtons[Tool.Ink].gameObject.SetActive(ink);
            foreach (Button swatch in _colorButtons)
            {
                swatch.gameObject.SetActive(ink);
            }
            _toolButtons[Tool.Stamp].gameObject.SetActive(marks);
            _toolButtons[Tool.Note].gameObject.SetActive(marks);
            _toolButtons[Tool.Eraser].gameObject.SetActive(marks);
            _undoButton.gameObject.SetActive(marks);
            _palette.gameObject.SetActive(marks);
            if (!IsAllowed(_tool))
            {
                _tool = lines ? Tool.Charcoal : Tool.Stamp;
            }
        }

        private static bool IsAllowed(Tool tool)
        {
            switch (KitConfig.Drawing.Value)
            {
                case FieldDrawing.Everything:
                    return true;
                case FieldDrawing.Sketch:
                    return tool != Tool.Ink;
                case FieldDrawing.Stamps:
                    return tool == Tool.Stamp || tool == Tool.Note || tool == Tool.Eraser;
                default:
                    return false;
            }
        }

        private void SelectPage(int page)
        {
            FinishLine();
            _session.ShowPage(page);
            _onJournal = false;
            ShowPage();
        }

        private void ShowPage()
        {
            _sheet.Root.gameObject.SetActive(true);
            _journal.gameObject.SetActive(false);
            _toolbar.gameObject.SetActive(true);
            _sheet.Show(_session.Current);
            UpdateSelection();
        }

        private void ShowJournal()
        {
            FinishLine();
            _onJournal = true;
            _sheet.Root.gameObject.SetActive(false);
            _journal.gameObject.SetActive(true);
            _toolbar.gameObject.SetActive(false);
            _palette.gameObject.SetActive(false);

            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"This leg so far: {Tally.Paces} paces");
            lines.AppendLine();
            List<Leg> legs = _session.Journal.Legs;
            if (legs.Count == 0)
            {
                lines.AppendLine("No legs noted yet. End a leg to note the tally with a label.");
            }
            // Newest first; the page shows as many as fit.
            for (int i = legs.Count - 1; i >= 0 && i >= legs.Count - 18; i--)
            {
                Leg leg = legs[i];
                string label = string.IsNullOrEmpty(leg.Label) ? "" : $" — {leg.Label}";
                lines.AppendLine($"Leg {leg.Number}: {leg.Paces} paces{label}  (day {leg.Day})");
            }
            _journalText.text = lines.ToString();
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            foreach (KeyValuePair<Tool, Button> pair in _toolButtons)
            {
                Tint(pair.Value, pair.Key == _tool);
            }
            for (int i = 0; i < _colorButtons.Count; i++)
            {
                Tint(_colorButtons[i], _tool == Tool.Ink && i == _color);
            }
            foreach (KeyValuePair<string, Button> pair in _stampButtons)
            {
                Tint(pair.Value, _tool == Tool.Stamp && pair.Key == _stampId);
            }
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool journal = i == _tabButtons.Count - 1;
                Tint(_tabButtons[i], journal ? _onJournal : !_onJournal && i == _session.Page);
            }
            if (!_onJournal)
            {
                _palette.gameObject.SetActive(KitConfig.Drawing.Value != FieldDrawing.Nothing);
            }
        }

        private static void Tint(Button button, bool selected)
        {
            if (button.image != null)
            {
                button.image.color = selected ? Selected : Color.white;
            }
        }

        private void Layout()
        {
            var canvas = (RectTransform)_canvas;
            float h = Ui.CanvasHeight(_canvas);
            float w = canvas.rect.width > 1f ? canvas.rect.width : Screen.width;
            float u = h / 100f;
            float side = 16f * u;
            float sheetHeight = Mathf.Min(KitConfig.DrawingSize.Value * h, (w - 2f * side) / Sheet.Aspect);
            float sheetWidth = sheetHeight * Sheet.Aspect;
            var center = new Vector2(0.5f, 0.5f);

            Ui.Place(_sheet.Root, center, center, Vector2.zero, Vector2.zero);
            _sheet.SetHeight(sheetHeight);
            Ui.Place(_journal.rectTransform, center, center, Vector2.zero, new Vector2(sheetWidth, sheetHeight));
            _journalText.fontSize = Mathf.Max(12, Mathf.RoundToInt(2.1f * u));
            Ui.Stretch(_journalText.rectTransform);
            _journalText.rectTransform.offsetMin = new Vector2(4f * u, 10f * u);
            _journalText.rectTransform.offsetMax = new Vector2(-4f * u, -4f * u);
            PlaceButton(_endLeg, _journal.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 3f * u), new Vector2(16f * u, 4.5f * u), u);

            // Tools: a column left of the sheet.
            float left = -sheetWidth * 0.5f - 2f * u;
            Ui.Place(_toolbar, center, new Vector2(1f, 1f), new Vector2(left, sheetHeight * 0.5f), new Vector2(13f * u, sheetHeight));
            float y = 0f;
            foreach (Transform child in _toolbar)
            {
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }
                var rect = (RectTransform)child;
                bool swatch = _colorButtons.Exists(b => b.transform == child);
                Vector2 size = swatch ? new Vector2(4.5f * u, 4.5f * u) : new Vector2(13f * u, 4.5f * u);
                if (swatch)
                {
                    int index = _colorButtons.FindIndex(b => b.transform == child);
                    float x = (index % 2) * 5f * u;
                    Ui.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), size);
                    if (index % 2 == 1 || index == _colorButtons.Count - 1)
                    {
                        y += 5f * u;
                    }
                    continue;
                }
                Ui.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), size);
                SetLabelSize(rect, u);
                y += size.y + 1f * u;
            }

            // Stamps: two columns right of the sheet.
            float right = sheetWidth * 0.5f + 2f * u;
            Ui.Place(_palette, center, new Vector2(0f, 1f), new Vector2(right, sheetHeight * 0.5f), new Vector2(12f * u, sheetHeight));
            int n = 0;
            foreach (Button button in _stampButtons.Values)
            {
                var rect = (RectTransform)button.transform;
                Ui.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((n % 2) * 6f * u, -(n / 2) * 6f * u), new Vector2(5.5f * u, 5.5f * u));
                n++;
            }

            // Tabs: a row above the sheet.
            Ui.Place(_tabs, center, new Vector2(0f, 0f), new Vector2(-sheetWidth * 0.5f, sheetHeight * 0.5f + 0.8f * u), new Vector2(sheetWidth, 4f * u));
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var rect = (RectTransform)_tabButtons[i].transform;
                Ui.Place(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * 12.5f * u, 0f), new Vector2(12f * u, 3.8f * u));
                SetLabelSize(rect, u);
            }

            _hint.fontSize = Mathf.Max(11, Mathf.RoundToInt(1.8f * u));
            Ui.Place(_hint.rectTransform, center, new Vector2(0.5f, 1f), new Vector2(0f, -sheetHeight * 0.5f - 1f * u), new Vector2(sheetWidth, 3f * u));
        }

        private static void PlaceButton(Button button, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, float u)
        {
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            Ui.Place(rect, anchor, new Vector2(0.5f, 0f), position, size);
            SetLabelSize(rect, u);
        }

        private static void SetLabelSize(RectTransform button, float u)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.Max(11, Mathf.RoundToInt(2f * u));
            }
        }
    }

    /// <summary>Forwards pointer events on the sheet to the drawing view.</summary>
    internal sealed class SheetInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public DrawingView View;

        public void OnPointerDown(PointerEventData eventData)
        {
            View?.PointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            View?.Drag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            View?.PointerUp(eventData);
        }
    }
}
