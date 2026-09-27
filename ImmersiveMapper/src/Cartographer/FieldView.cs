using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Drawing in the field: a draft large in the middle of the screen (zoom with the wheel, pan with the middle mouse,
    /// right-click to select), the tools the FieldDrawing rule allows, tabs for the master copy, the drafts and the
    /// journal. The game's input is blocked while it's open, so you stand still. Esc closes it.
    /// </summary>
    internal sealed class FieldView
    {
        private readonly Transform _canvas;
        private readonly RectTransform _root;
        private readonly CanvasView _draft;
        private readonly CanvasView _master;
        private readonly DrawTools _tools;
        private readonly ToolPanel _panel;
        private readonly RectTransform _tabs;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly RawImage _journal;
        private readonly Text _journalText;
        private readonly Button _endLeg;
        private readonly Button _resetTally;
        private readonly Text _hint;

        private CaseSession _session;
        private bool _onJournal;

        /// <summary>The journal page's End leg button.</summary>
        public Action OnEndLeg;
        public Action OnResetTally;
        public Action OnDone;

        public FieldView(Transform canvas)
        {
            _canvas = canvas;
            _root = Ui.Rect("IM_MapField", canvas);
            Ui.Stretch(_root);
            _root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            _draft = new CanvasView(_root, "draft", true) { Interactive = true, MaxZoom = 6f };
            _master = new CanvasView(_root, "master", false) { Interactive = true, MinZoom = 0.02f, MaxZoom = 8f };
            _tools = new DrawTools(_draft);
            _draft.Tool = _tools;
            _panel = new ToolPanel(_root, _tools, () => OnDone?.Invoke());

            _journal = Ui.Rect("journal", _root).gameObject.AddComponent<RawImage>();
            _journal.texture = PaperTextures.Paper;
            _journalText = Ui.Text("legs", _journal.transform, 18, Ui.InkBrown, TextAnchor.UpperLeft);
            _endLeg = Ui.Button("End leg", _journal.transform, new Vector2(160f, 40f));
            _endLeg.onClick.AddListener(() => OnEndLeg?.Invoke());
            _resetTally = Ui.Button("Reset tally", _journal.transform, new Vector2(160f, 40f));
            _resetTally.onClick.AddListener(() => { OnResetTally?.Invoke(); RefreshJournal(); });
            _tools.Selection.Changed += UpdateHint;

            _tabs = Ui.Rect("tabs", _root);
            _hint = Ui.HudText("hint", _root, 16, TextAnchor.UpperCenter);
            _root.gameObject.SetActive(false);
        }

        public bool Alive => _root != null;

        public bool IsOpen => _root.gameObject.activeSelf;

        public void Open(CaseSession session)
        {
            _session = session;
            _tools.ClearUndo();
            _tools.MaxPoints = KitConfig.MaxPointsPerSheet.Value;
            _tools.MaxMarks = KitConfig.MaxMarksPerSheet.Value;
            _tools.Changed = () => _session?.SaveDraft(_session.Page);
            _onJournal = false;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            FieldDrawing rule = KitConfig.Drawing.Value;
            _panel.Apply(tool => Allowed(rule, tool), rule == FieldDrawing.Everything);
            BuildTabs();
            Layout();
            _master.Show(session.Master);
            if (session.MasterViewSet)
            {
                _master.SetView(session.MasterZoom, session.MasterCenter);
            }
            else
            {
                _master.Fit();
            }
            ShowPage();
        }

        public void Close()
        {
            _tools.Finish();
            _tools.Deselect();
            if (_session != null && _session.HasMaster)
            {
                _session.MasterZoom = _master.Zoom;
                _session.MasterCenter = _master.Center;
                _session.MasterViewSet = true;
            }
            _root.gameObject.SetActive(false);
            _session = null;
        }

        public void Update()
        {
            if ((ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) && ZInput.GetKeyDown(KeyCode.Z))
            {
                _tools.Undo();
                _panel.Refresh();
            }
            _tools.UpdateKeys();
            _draft.Update();
            _master.Update();
        }

        public void RefreshJournal()
        {
            if (_onJournal)
            {
                ShowJournal();
            }
        }

        private static bool Allowed(FieldDrawing rule, ToolKind tool)
        {
            switch (rule)
            {
                case FieldDrawing.Everything:
                    return true;
                case FieldDrawing.Sketch:
                    return tool == ToolKind.Charcoal || tool == ToolKind.Stamp || tool == ToolKind.Text || tool == ToolKind.Eraser;
                case FieldDrawing.Stamps:
                    return tool == ToolKind.Stamp || tool == ToolKind.Text || tool == ToolKind.Eraser;
                default:
                    return false;
            }
        }

        private void BuildTabs()
        {
            foreach (Button tab in _tabButtons)
            {
                UnityEngine.Object.Destroy(tab.gameObject);
            }
            _tabButtons.Clear();
            if (_session.HasMaster)
            {
                AddTab("Master", () => SelectPage(CaseSession.MasterPage));
            }
            for (int i = 0; i < _session.Drafts.Length; i++)
            {
                int page = i;
                AddTab($"Sheet {i + 1}", () => SelectPage(page));
            }
            AddTab("Journal", ShowJournal);
        }

        private void AddTab(string label, Action onClick)
        {
            Button tab = Ui.Button(label, _tabs, new Vector2(120f, 36f));
            tab.onClick.AddListener(() => onClick());
            _tabButtons.Add(tab);
        }

        private void SelectPage(int page)
        {
            _tools.Finish();
            _tools.Deselect();
            _session.ShowPage(page);
            _onJournal = false;
            ShowPage();
        }

        private void ShowPage()
        {
            bool master = _session.OnMaster;
            _journal.gameObject.SetActive(false);
            _draft.Root.gameObject.SetActive(!master);
            _master.Root.gameObject.SetActive(master);
            _panel.SetVisible(!master);
            if (!master)
            {
                _draft.Show(_session.Current);
                _draft.Fit();
            }
            UpdateHint();
            TintTabs();
            _panel.Refresh();
        }

        private void UpdateHint()
        {
            if (_session == null || _onJournal)
            {
                return;
            }
            if (_session.OnMaster)
            {
                _hint.text = "Your copy of a table's master: read only. Wheel to zoom · middle mouse to pan · Esc to close";
            }
            else if (_tools.Selection.Any)
            {
                _hint.text = string.Format("Selected {0}: drag to move · arrows nudge (Shift: more) · Ctrl + wheel resizes · a colour recolours · Delete erases · right-click elsewhere lets go", _tools.Selection.Describe());
            }
            else
            {
                _hint.text = "Left-click to draw · right-click to select · wheel to zoom · middle mouse to pan · Ctrl+Z undo · Esc to close";
            }
        }

        private void ShowJournal()
        {
            _tools.Finish();
            _tools.Deselect();
            _onJournal = true;
            _draft.Root.gameObject.SetActive(false);
            _master.Root.gameObject.SetActive(false);
            _panel.SetVisible(false);
            _journal.gameObject.SetActive(true);
            _journalText.text = JournalText(_session.Journal);
            _hint.text = "End a leg to note the tally with a label (Reset tally starts over without one). At a cartography table, legs become measuring strings.";
            TintTabs();
        }

        public static string JournalText(Journal journal)
        {
            var lines = new StringBuilder();
            lines.AppendLine($"This leg so far: {Tally.Paces} paces");
            lines.AppendLine();
            List<Leg> legs = journal.Legs;
            if (legs.Count == 0)
            {
                lines.AppendLine("No legs noted yet.");
            }
            // Newest first; the page shows as many as fit.
            for (int i = legs.Count - 1; i >= 0 && i >= legs.Count - 18; i--)
            {
                Leg leg = legs[i];
                string label = string.IsNullOrEmpty(leg.Label) ? "" : $" — {leg.Label}";
                string plotted = leg.Plotted ? "  ✓" : "";
                string sea = leg.AtSea ? ", at sea" : "";
                lines.AppendLine($"Leg {leg.Number}: {leg.Paces} paces{label}  (day {leg.Day}{sea}){plotted}");
            }
            return lines.ToString();
        }

        private void TintTabs()
        {
            int offset = _session.HasMaster ? 1 : 0;
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool selected;
                if (i == _tabButtons.Count - 1)
                {
                    selected = _onJournal;
                }
                else if (_session.HasMaster && i == 0)
                {
                    selected = !_onJournal && _session.OnMaster;
                }
                else
                {
                    selected = !_onJournal && _session.Page == i - offset;
                }
                Button tab = _tabButtons[i];
                if (tab.image != null)
                {
                    tab.image.color = selected ? new Color(1f, 0.75f, 0.35f) : Color.white;
                }
            }
        }

        private void Layout()
        {
            var canvas = (RectTransform)_canvas;
            float h = Ui.CanvasHeight(_canvas);
            float w = canvas.rect.width > 1f ? canvas.rect.width : Screen.width;
            float u = h / 100f;
            float left = 21f * u;
            float right = 14f * u;
            float viewHeight = Mathf.Min(KitConfig.DrawingSize.Value * h, (w - 2f * Mathf.Max(left, right) - 4f * u) / Sheet.Aspect);
            var size = new Vector2(viewHeight * Sheet.Aspect, viewHeight);
            var center = new Vector2(0.5f, 0.5f);

            Ui.Place(_draft.Root, center, center, Vector2.zero, Vector2.zero);
            _draft.SetSize(size);
            Ui.Place(_master.Root, center, center, Vector2.zero, Vector2.zero);
            _master.SetSize(size);
            Ui.Place(_journal.rectTransform, center, center, Vector2.zero, size);
            _journalText.fontSize = Mathf.Max(12, Mathf.RoundToInt(2.1f * u));
            Ui.Stretch(_journalText.rectTransform);
            _journalText.rectTransform.offsetMin = new Vector2(4f * u, 10f * u);
            _journalText.rectTransform.offsetMax = new Vector2(-4f * u, -4f * u);
            var endLeg = (RectTransform)_endLeg.transform;
            Ui.Place(endLeg, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-1f * u, 3f * u), new Vector2(16f * u, 4.5f * u));
            Ui.LabelSize(endLeg, u);
            var resetTally = (RectTransform)_resetTally.transform;
            Ui.Place(resetTally, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1f * u, 3f * u), new Vector2(16f * u, 4.5f * u));
            Ui.LabelSize(resetTally, u);

            _panel.Layout(new Vector2(-size.x * 0.5f - 2f * u - 19f * u, size.y * 0.5f), new Vector2(size.x * 0.5f + 2f * u, size.y * 0.5f), u);

            Ui.Place(_tabs, center, new Vector2(0f, 0f), new Vector2(-size.x * 0.5f, size.y * 0.5f + 0.8f * u), new Vector2(size.x, 4f * u));
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var rect = (RectTransform)_tabButtons[i].transform;
                Ui.Place(rect, Vector2.zero, Vector2.zero, new Vector2(i * 12.5f * u, 0f), new Vector2(12f * u, 3.8f * u));
                Ui.LabelSize(rect, u);
            }
            _hint.fontSize = Mathf.Max(11, Mathf.RoundToInt(1.8f * u));
            Ui.Place(_hint.rectTransform, center, new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.5f - 1f * u), new Vector2(size.x + 30f * u, 3f * u));
        }
    }
}
