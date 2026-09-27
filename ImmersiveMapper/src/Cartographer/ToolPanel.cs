using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// The buttons for a <see cref="DrawTools"/>: tools, sizes, straight and dotted, colours, undo and done in a column,
    /// and the stamps in a second column. Tools the rules don't allow are hidden.
    /// </summary>
    internal sealed class ToolPanel
    {
        private static readonly Color Selected = new Color(1f, 0.75f, 0.35f);

        private static readonly (ToolKind Kind, string Label)[] ToolLabels =
        {
            (ToolKind.Charcoal, "Charcoal"),
            (ToolKind.Quill, "Quill"),
            (ToolKind.Wash, "Wash"),
            (ToolKind.Fill, "Fill"),
            (ToolKind.Stamp, "Stamp"),
            (ToolKind.Text, "Text"),
            (ToolKind.Eraser, "Eraser"),
        };

        public readonly RectTransform Root;
        public readonly RectTransform StampColumn;

        private readonly DrawTools _tools;
        private readonly Dictionary<ToolKind, Button> _toolButtons = new Dictionary<ToolKind, Button>();
        private readonly Button[] _sizes = new Button[3];
        private readonly Button _straight;
        private readonly Button _dotted;
        private readonly List<Button> _swatches = new List<Button>();
        private readonly Text _swatchName;
        private readonly Dictionary<string, Button> _stamps = new Dictionary<string, Button>();
        private readonly Button _undo;
        private readonly Button _done;
        private Func<ToolKind, bool> _allowed = _ => true;
        private bool _colours = true;

        public ToolPanel(Transform parent, DrawTools tools, Action onDone)
        {
            _tools = tools;
            Root = Ui.Rect("tools", parent);
            StampColumn = Ui.Rect("stamps", parent);

            foreach (var entry in ToolLabels)
            {
                ToolKind tool = entry.Kind;
                Button button = Ui.Button(entry.Label, Root, new Vector2(100f, 40f));
                button.onClick.AddListener(() => Select(tool));
                _toolButtons[tool] = button;
            }
            string[] sizeLabels = { "Fine", "Medium", "Broad" };
            for (int i = 0; i < _sizes.Length; i++)
            {
                var size = (BrushSize)i;
                _sizes[i] = Ui.Button(sizeLabels[i], Root, new Vector2(60f, 40f));
                _sizes[i].onClick.AddListener(() => { _tools.Size = size; Refresh(); });
            }
            _straight = Ui.Button("Straight", Root, new Vector2(100f, 40f));
            _straight.onClick.AddListener(() => { _tools.Straight = !_tools.Straight; Refresh(); });
            _dotted = Ui.Button("Dotted", Root, new Vector2(100f, 40f));
            _dotted.onClick.AddListener(() => { _tools.Dotted = !_tools.Dotted; Refresh(); });

            for (byte i = 0; i < Inks.All.Length; i++)
            {
                byte color = i;
                Button swatch = Ui.Button("", Root, new Vector2(40f, 40f));
                Image fill = Ui.Rect("fill", swatch.transform).gameObject.AddComponent<Image>();
                Ui.Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(5f, 5f);
                fill.rectTransform.offsetMax = new Vector2(-5f, -5f);
                fill.color = Inks.All[i].Color;
                fill.raycastTarget = false;
                swatch.onClick.AddListener(() =>
                {
                    _tools.Color = color;
                    if (_tools.Tool == ToolKind.Charcoal || _tools.Tool == ToolKind.Stamp || _tools.Tool == ToolKind.Text || _tools.Tool == ToolKind.Eraser)
                    {
                        // Picking a colour means painting: biome colours go to the wash, ink colours to the quill.
                        Select(color >= Inks.FirstBiome ? ToolKind.Wash : ToolKind.Quill);
                        return;
                    }
                    Refresh();
                });
                _swatches.Add(swatch);
            }
            _swatchName = Ui.HudText("colour", Root, 14, TextAnchor.UpperLeft);

            _undo = Ui.Button("Undo", Root, new Vector2(100f, 40f));
            _undo.onClick.AddListener(() => { _tools.Undo(); Refresh(); });
            _done = Ui.Button("Done", Root, new Vector2(100f, 40f));
            _done.onClick.AddListener(() => onDone?.Invoke());
        }

        /// <summary>Shows only what the rules allow; colours (quill, wash, fill and the palette) only when colours are.</summary>
        public void Apply(Func<ToolKind, bool> allowed, bool colours)
        {
            _allowed = allowed;
            _colours = colours;
            _tools.Allowed = tool => _allowed(tool) && (_colours || !UsesColour(tool));
            foreach (KeyValuePair<ToolKind, Button> pair in _toolButtons)
            {
                pair.Value.gameObject.SetActive(_tools.Allowed(pair.Key));
            }
            bool lines = _tools.Allowed(ToolKind.Charcoal) || _tools.Allowed(ToolKind.Quill);
            _straight.gameObject.SetActive(lines);
            _dotted.gameObject.SetActive(lines);
            foreach (Button swatch in _swatches)
            {
                swatch.gameObject.SetActive(colours);
            }
            _swatchName.gameObject.SetActive(colours);
            bool any = false;
            foreach (ToolKind tool in _toolButtons.Keys)
            {
                any |= _tools.Allowed(tool);
            }
            foreach (Button size in _sizes)
            {
                size.gameObject.SetActive(any);
            }
            _undo.gameObject.SetActive(any);
            StampColumn.gameObject.SetActive(_tools.Allowed(ToolKind.Stamp));
            if (!_tools.Allowed(_tools.Tool))
            {
                foreach (var entry in ToolLabels)
                {
                    if (_tools.Allowed(entry.Kind))
                    {
                        _tools.Tool = entry.Kind;
                        break;
                    }
                }
            }
            BuildStamps();
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            StampColumn.gameObject.SetActive(visible && _tools.Allowed(ToolKind.Stamp));
        }

        /// <summary>Lays the column out from its top-left corner (in the parent's centred space); returns its width.</summary>
        public float Layout(Vector2 topLeft, Vector2 stampsTopLeft, float u)
        {
            float w = 9f * u;
            float h = 4.2f * u;
            float gap = 0.6f * u;
            float width = w * 2f + gap;
            var center = new Vector2(0.5f, 0.5f);
            Ui.Place(Root, center, new Vector2(0f, 1f), topLeft, new Vector2(width, 60f * u));
            float y = 0f;
            int column = 0;
            foreach (var entry in ToolLabels)
            {
                Button button = _toolButtons[entry.Kind];
                if (!button.gameObject.activeSelf)
                {
                    continue;
                }
                PlaceAt(button, column * (w + gap), y, w, h, u);
                column++;
                if (column == 2)
                {
                    column = 0;
                    y += h + gap;
                }
            }
            if (column != 0)
            {
                y += h + gap;
            }
            y += gap;
            if (_sizes[0].gameObject.activeSelf)
            {
                float sw = (width - 2f * gap) / 3f;
                for (int i = 0; i < _sizes.Length; i++)
                {
                    PlaceAt(_sizes[i], i * (sw + gap), y, sw, h, u * 0.85f);
                }
                y += h + gap;
            }
            if (_straight.gameObject.activeSelf)
            {
                PlaceAt(_straight, 0f, y, w, h, u);
                PlaceAt(_dotted, w + gap, y, w, h, u);
                y += h + gap * 2f;
            }
            if (_colours)
            {
                float s = (width - 3f * gap) / 4f;
                for (int i = 0; i < _swatches.Count; i++)
                {
                    PlaceAt(_swatches[i], (i % 4) * (s + gap), y + (i / 4) * (s + gap), s, s, u);
                }
                y += ((_swatches.Count + 3) / 4) * (s + gap);
                _swatchName.fontSize = Mathf.Max(11, Mathf.RoundToInt(1.8f * u));
                Ui.Place(_swatchName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(width, 2.6f * u));
                y += 2.8f * u;
            }
            if (_undo.gameObject.activeSelf)
            {
                PlaceAt(_undo, 0f, y, w, h, u);
            }
            PlaceAt(_done, _undo.gameObject.activeSelf ? w + gap : 0f, y, w, h, u);

            float stamp = 5.5f * u;
            Ui.Place(StampColumn, center, new Vector2(0f, 1f), stampsTopLeft, new Vector2(stamp * 2f + gap, 60f * u));
            int n = 0;
            foreach (Button button in _stamps.Values)
            {
                PlaceAt(button, (n % 2) * (stamp + gap), (n / 2) * (stamp + gap), stamp, stamp, u);
                n++;
            }
            return width;
        }

        public void Refresh()
        {
            foreach (KeyValuePair<ToolKind, Button> pair in _toolButtons)
            {
                Tint(pair.Value, pair.Key == _tools.Tool);
            }
            for (int i = 0; i < _sizes.Length; i++)
            {
                Tint(_sizes[i], (int)_tools.Size == i);
            }
            Tint(_straight, _tools.Straight);
            Tint(_dotted, _tools.Dotted);
            bool coloured = UsesColour(_tools.Tool);
            for (int i = 0; i < _swatches.Count; i++)
            {
                Tint(_swatches[i], coloured && i == _tools.Color);
            }
            _swatchName.text = coloured ? Inks.All[_tools.Color].Name : "";
            foreach (KeyValuePair<string, Button> pair in _stamps)
            {
                Tint(pair.Value, _tools.Tool == ToolKind.Stamp && pair.Key == _tools.StampId);
            }
        }

        private void Select(ToolKind tool)
        {
            _tools.Finish();
            _tools.Tool = tool;
            Refresh();
        }

        private static bool UsesColour(ToolKind tool)
        {
            return tool == ToolKind.Quill || tool == ToolKind.Wash || tool == ToolKind.Fill;
        }

        private void BuildStamps()
        {
            if (_stamps.Count > 0)
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
                Button button = Ui.Button("", StampColumn, new Vector2(48f, 48f));
                Image icon = Ui.Rect("icon", button.transform).gameObject.AddComponent<Image>();
                Ui.Stretch(icon.rectTransform);
                icon.rectTransform.offsetMin = new Vector2(5f, 5f);
                icon.rectTransform.offsetMax = new Vector2(-5f, -5f);
                icon.sprite = kind.Sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = kind.Tinted ? GUIManager.Instance.ValheimBeige : Color.white;
                button.onClick.AddListener(() => { _tools.StampId = id; Select(ToolKind.Stamp); });
                _stamps[id] = button;
            }
            if (!_stamps.ContainsKey(_tools.StampId))
            {
                foreach (string id in _stamps.Keys)
                {
                    _tools.StampId = id;
                    break;
                }
            }
        }

        private static void PlaceAt(Button button, float x, float y, float w, float h, float u)
        {
            var rect = (RectTransform)button.transform;
            Ui.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.Max(10, Mathf.RoundToInt(1.9f * u));
            }
        }

        private static void Tint(Button button, bool selected)
        {
            if (button.image != null)
            {
                button.image.color = selected ? Selected : Color.white;
            }
        }
    }
}
