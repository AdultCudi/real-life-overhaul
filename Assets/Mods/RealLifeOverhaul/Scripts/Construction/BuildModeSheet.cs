using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's tool panel (BuildModeBar): a small box of the game's designer over the toolbar's left end for the tool in use
    /// alone, as tall as what it says (a vertical layout group and a content size fitter; never a scroll). Written from parts the
    /// way a page of the game's is: a heading with a count at its right, lines of words (red for what stops an action), facts as
    /// a name and a figure, big zone rows with their colour, the main button in the game's blue beside a plain one, and the view's
    /// legend. Parts are kept and reused; a part not written is hidden. Written only when the state changes; nothing a frame.
    /// </summary>
    internal sealed class BuildModeSheet
    {
        private const float PanelWide = 880f;           // the panel, in the canvas's units (3840 by 2160)
        private const float TitleSize = 48f;
        private const float CountSize = 32f;
        private const float LineSize = 36f;
        private const float RowHigh = 116f;         // a zone row
        private const float ButtonHigh = 108f;      // the panel's buttons
        private const float SideButton = 280f;      // the plain button beside the main one
        private const float KeySize = 30f;          // a legend's square

        private sealed class FactPart
        {
            public RectTransform Root;
            public TMP_Text Name, Value;
        }

        private sealed class SwatchPart
        {
            public KitButton Button;
            public Image Square;
            public TMP_Text Name, Status;
            public int Id;
        }

        private sealed class Actions
        {
            public RectTransform Root;
            public KitButton Main, Side;
            public int MainId, SideId;
        }

        private sealed class Key
        {
            public RectTransform Root;
            public Image Edge, Inside;
            public TMP_Text Name;
        }

        private readonly BuildModeKit _kit;
        private readonly RectTransform _rect;
        private readonly TMP_Text _title, _count;
        private readonly Action<int> _onRow;
        private readonly List<TMP_Text> _lines = new List<TMP_Text>(8);
        private readonly List<FactPart> _facts = new List<FactPart>(8);
        private readonly List<SwatchPart> _swatches = new List<SwatchPart>(6);
        private readonly List<Actions> _actions = new List<Actions>(3);
        private readonly List<Key> _keys = new List<Key>(BuildModeRules.LegendMost);
        private int _linesUsed, _factsUsed, _swatchesUsed, _actionsUsed, _keysUsed;
        private RectTransform _keyRow;   // the legend's entries flow two to a row
        private readonly List<RectTransform> _keyRows = new List<RectTransform>(6);

        public RectTransform Rect => _rect;

        public BuildModeSheet(BuildModeKit kit, Transform parent, Action<int> onRow)
        {
            _kit = kit;
            _onRow = onRow;
            _rect = kit.Panel(parent, "Tool panel", false, new RectOffset(48, 48, 44, 44), 24f);
            var fit = _rect.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            _rect.sizeDelta = new Vector2(PanelWide, 200f);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0f);
            _rect.pivot = new Vector2(0f, 0f);
            RectTransform head = BuildModeKit.Strip(_rect, "Heading", true, new RectOffset(), 20f, TextAnchor.LowerLeft);
            _title = kit.Text(head, "Title", TitleSize, BuildModeBar.Text, true, true);
            _title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _count = kit.Text(head, "Count", CountSize, BuildModeBar.Muted, false, false);
            _count.alignment = TextAlignmentOptions.BottomRight;
        }

        /// <summary>Stands the panel with its foot left corner at a point over the canvas's foot middle.</summary>
        public void Stand(Vector2 at)
        {
            _rect.anchoredPosition = at;
        }

        public void Show(bool shown)
        {
            if (_rect.gameObject.activeSelf != shown)
            {
                _rect.gameObject.SetActive(shown);
            }
        }

        // ------------------------------------------------------------------ writing

        /// <summary>Starts the panel: its heading and the count at its right (none for none).</summary>
        public void Begin(string title, string count)
        {
            UiClone.Words(_title, title);
            UiClone.Words(_count, count ?? string.Empty);
            _linesUsed = _factsUsed = _swatchesUsed = _actionsUsed = _keysUsed = 0;
            _keyRow = null;
        }

        /// <summary>A line of words, wrapping.</summary>
        public void Line(string text, Color colour)
        {
            TMP_Text t;
            if (_linesUsed < _lines.Count)
            {
                t = _lines[_linesUsed];
            }
            else
            {
                t = _kit.Text(_rect, "Line", LineSize, colour, false, true);
                _lines.Add(t);
            }
            _linesUsed++;
            t.color = colour;
            UiClone.Words(t, text);
            Last(t.transform);
        }

        /// <summary>A fact: its name at the left, its figure at the right in bold (a colour for a use).</summary>
        public void Fact(string name, string value, Color colour)
        {
            FactPart f;
            if (_factsUsed < _facts.Count)
            {
                f = _facts[_factsUsed];
            }
            else
            {
                f = new FactPart { Root = BuildModeKit.Strip(_rect, "Fact", true, new RectOffset(), 24f, TextAnchor.MiddleLeft) };
                f.Name = _kit.Text(f.Root, "Name", LineSize, BuildModeBar.Muted, false, true);
                f.Name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                f.Value = _kit.Text(f.Root, "Value", LineSize, BuildModeBar.Text, true, true);
                f.Value.alignment = TextAlignmentOptions.MidlineRight;
                f.Value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                _facts.Add(f);
            }
            _factsUsed++;
            UiClone.Words(f.Name, name);
            UiClone.Words(f.Value, value);
            f.Value.color = colour;
            Last(f.Root);
        }

        /// <summary>A big zone row: its colour's square, its name, and under it whether it is allowed (green) or needs City Hall (amber); the one chosen in the game's blue.</summary>
        public void Swatch(int id, Color colour, string name, string status, Color statusColour, bool chosen, bool usable)
        {
            SwatchPart s;
            if (_swatchesUsed < _swatches.Count)
            {
                s = _swatches[_swatchesUsed];
            }
            else
            {
                s = new SwatchPart();
                SwatchPart wired = s;
                s.Button = _kit.Button(_rect, "Zone row", () => _onRow?.Invoke(wired.Id), -1f, RowHigh);
                var row = s.Button.Root.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(28, 28, 12, 12);
                row.spacing = 28f;
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                if (s.Button.Chosen != null)
                {
                    BuildModeKit.Size(s.Button.Chosen, 0f, 0f).ignoreLayout = true;
                    s.Button.Chosen.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
                    ((RectTransform)s.Button.Chosen.transform).sizeDelta = Vector2.zero;
                }
                if (s.Button.Icon != null)
                {
                    s.Button.Icon.gameObject.SetActive(false);
                    BuildModeKit.Size(s.Button.Icon.gameObject, 0f, 0f).ignoreLayout = true;
                }
                s.Square = BuildModeKit.Picture(s.Button.Root, "Colour", _kit.White ?? _kit.Line, Color.white, 60f, 60f);
                RectTransform words = BuildModeKit.Strip(s.Button.Root, "Words", false, new RectOffset(), 2f, TextAnchor.MiddleLeft);
                words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                s.Name = _kit.Text(words, "Name", 38f, BuildModeBar.Text, true, true);
                s.Status = _kit.Text(words, "Status", 30f, BuildModeBar.Good, false, true);
                BuildModeBar.Paint(s.Button, _kit.White ?? _kit.Line, BuildModeBar.ButtonGrey);
                _swatches.Add(s);
            }
            _swatchesUsed++;
            s.Id = id;
            s.Square.gameObject.SetActive(colour.a > 0f);
            s.Square.color = new Color(colour.r, colour.g, colour.b, 1f);
            UiClone.Words(s.Name, name);
            s.Status.gameObject.SetActive(!string.IsNullOrEmpty(status));
            UiClone.Words(s.Status, status ?? string.Empty);
            s.Status.color = statusColour;
            s.Button.SetChosen(chosen);
            s.Button.SetUsable(usable);
            Last(s.Button.Root);
        }

        /// <summary>A row of the main button in the game's blue (filling the row) and a plain one beside it (id 0: none).</summary>
        public void Buttons(int mainId, string main, bool mainUsable, int sideId, string side, bool sideUsable)
        {
            Actions a;
            if (_actionsUsed < _actions.Count)
            {
                a = _actions[_actionsUsed];
            }
            else
            {
                a = new Actions { Root = BuildModeKit.Strip(_rect, "Buttons", true, new RectOffset(0, 0, 8, 0), 24f, TextAnchor.MiddleLeft) };
                Actions wired = a;
                a.Main = Wide(a.Root, () => _onRow?.Invoke(wired.MainId));
                a.Main.Root.GetComponent<LayoutElement>().flexibleWidth = 1f;
                BuildModeBar.Paint(a.Main, _kit.Blue, Color.white);
                a.Side = Wide(a.Root, () => _onRow?.Invoke(wired.SideId));
                BuildModeKit.Size(a.Side.Root.gameObject, SideButton, ButtonHigh);
                BuildModeBar.Paint(a.Side, _kit.White ?? _kit.Line, BuildModeBar.ButtonGrey);
                _actions.Add(a);
            }
            _actionsUsed++;
            a.MainId = mainId;
            a.SideId = sideId;
            a.Main.Show(mainId != 0);
            a.Side.Show(sideId != 0);
            UiClone.Words(a.Main.Label, main ?? string.Empty);
            UiClone.Words(a.Side.Label, side ?? string.Empty);
            a.Main.SetUsable(mainUsable);
            a.Side.SetUsable(sideUsable);
            // One button alone fills the row, whichever it is.
            a.Side.Root.GetComponent<LayoutElement>().flexibleWidth = mainId == 0 ? 1f : 0f;
            Last(a.Root);
        }

        private KitButton Wide(Transform parent, Action click)
        {
            KitButton b = _kit.Button(parent, "Button", click, -1f, ButtonHigh);
            if (b.Icon != null)
            {
                b.Icon.gameObject.SetActive(false);
            }
            if (b.Chosen != null)
            {
                b.Chosen.SetActive(false);
            }
            b.Label = _kit.Text(b.Root, "Words", 38f, Color.white, true, false);
            var r = (RectTransform)b.Label.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(20f, 0f);
            r.offsetMax = new Vector2(-20f, 0f);
            b.Label.alignment = TextAlignmentOptions.Center;
            b.Label.textWrappingMode = TextWrappingModes.NoWrap;
            b.Label.enableAutoSizing = true;   // a long figure in another language fits on the button, never cut
            b.Label.fontSizeMin = 24f;
            b.Label.fontSizeMax = 38f;
            return b;
        }

        /// <summary>A legend entry: a square of the fill inside its edge's colour (an edge alone: the panel's own dark inside), and its words; two to a row.</summary>
        public void Legend(Color fill, Color edge, bool edgeOnly, string text)
        {
            Key k;
            if (_keysUsed < _keys.Count)
            {
                k = _keys[_keysUsed];
            }
            else
            {
                k = new Key { Root = BuildModeKit.Strip(_rect, "Key", true, new RectOffset(), 14f, TextAnchor.MiddleLeft) };
                k.Root.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                k.Edge = BuildModeKit.Picture(k.Root, "Edge", _kit.Line, Color.white, KeySize, KeySize);
                k.Inside = BuildModeKit.Picture(k.Edge.transform, "Inside", _kit.Line, Color.white, KeySize - 10f, KeySize - 10f);
                var inside = (RectTransform)k.Inside.transform;
                inside.anchorMin = inside.anchorMax = inside.pivot = new Vector2(0.5f, 0.5f);
                inside.anchoredPosition = Vector2.zero;
                k.Inside.GetComponent<LayoutElement>().ignoreLayout = true;
                k.Name = _kit.Text(k.Root, "Name", 28f, BuildModeBar.Soft, false, true);
                k.Name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                _keys.Add(k);
            }
            if (_keysUsed % 2 == 0)
            {
                int row = _keysUsed / 2;
                if (row >= _keyRows.Count)
                {
                    RectTransform r = BuildModeKit.Strip(_rect, "Legend row", true, new RectOffset(), 20f, TextAnchor.MiddleLeft);
                    ((HorizontalLayoutGroup)r.GetComponent<HorizontalLayoutGroup>()).childForceExpandWidth = true;
                    _keyRows.Add(r);
                }
                _keyRow = _keyRows[row];
                Last(_keyRow);
            }
            _keysUsed++;
            k.Root.SetParent(_keyRow, false);
            k.Root.SetAsLastSibling();
            k.Root.gameObject.SetActive(true);
            k.Edge.color = new Color(edge.r, edge.g, edge.b, 1f);
            k.Inside.color = edgeOnly ? new Color(0.12f, 0.13f, 0.15f, 1f) : new Color(fill.r, fill.g, fill.b, 1f);
            UiClone.Words(k.Name, text);
        }

        /// <summary>Ends the panel: the parts not written hidden, laid out at once.</summary>
        public void End()
        {
            Hide(_lines, _linesUsed);
            for (int i = _factsUsed; i < _facts.Count; i++)
            {
                Off(_facts[i].Root.gameObject);
            }
            for (int i = _swatchesUsed; i < _swatches.Count; i++)
            {
                _swatches[i].Button.Show(false);
            }
            for (int i = _actionsUsed; i < _actions.Count; i++)
            {
                Off(_actions[i].Root.gameObject);
            }
            for (int i = _keysUsed; i < _keys.Count; i++)
            {
                Off(_keys[i].Root.gameObject);
            }
            for (int i = (_keysUsed + 1) / 2; i < _keyRows.Count; i++)
            {
                Off(_keyRows[i].gameObject);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        }

        private static void Hide(List<TMP_Text> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                Off(pool[i].gameObject);
            }
        }

        private static void Off(GameObject go)
        {
            if (go.activeSelf)
            {
                go.SetActive(false);
            }
        }

        // A part written: shown, after those written before it.
        private static void Last(Transform t)
        {
            t.SetAsLastSibling();
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }
        }

        /// <summary>The panel's parts each below the one before it, none over another; null when so.</summary>
        public string Stacked()
        {
            if (!_rect.gameObject.activeInHierarchy)
            {
                return null;
            }
            float lastBottom = float.MaxValue;
            string lastName = null;
            for (int i = 0; i < _rect.childCount; i++)
            {
                var child = _rect.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }
                Rect r = BuildModeBar.World(child);
                if (lastName != null && r.yMax > lastBottom + 0.5f)
                {
                    return "the tool panel: '" + child.name + "' overlaps '" + lastName + "'";
                }
                lastBottom = r.yMin;
                lastName = child.name;
            }
            return null;
        }
    }
}
