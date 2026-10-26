using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's window (the owner, 26 Oct 2026: "The right pane is not enough, open it as a proper big center modal"): a
    /// copy of the game's help screen (GameUi.HelpFrame, 2400 by 1600 at the middle, in the mod's dark colours) on the help's own
    /// canvas over the map. On the left the tools and the views, as the help's categories and links, the one in use in the
    /// game's blue; on the right a page that changes with the tool, written from the game's own parts: the help's topic header
    /// and paragraphs, the settings menu's rows with their buttons (GameUi.OptionRows), the top bar's bars for the demand
    /// (GameUi.GameBar), and a legend of plain swatches. While the player picks squares or places a design the window steps aside:
    /// it slides down until only its top bar shows at the foot of the screen, with the selection or the square under the pointer
    /// in it, and comes back from its arrow. Laid out by the page's own layout group and scroll, so nothing overlaps at any
    /// length of text. Built at the first opening; a page written only after a pick or an action; nothing runs a frame of its own.
    /// </summary>
    internal sealed class BuildModePanel
    {
        public static IModLogger Logger;

        private const float DemandHigh = 56f;      // a demand row, in the help canvas's units (3840 by 2160)
        private const float LabelWide = 420f;      // a demand row's name, before its bar
        private const float KeyHigh = 46f;         // a legend row
        private const float SwatchSize = 38f;      // a legend row's square: its edge, and inside it its fill
        private const float SwatchEdge = 7f;
        private const float FootGap = 24f;         // between the screen's foot and the top bar when the window has stepped aside
        private const float TextSize = 28f;        // the help's own text size

        private sealed class Part
        {
            public OptionRow Row;
            public int Id;
        }

        private sealed class DemandRow
        {
            public GameObject Root;
            public TMP_Text Name, Value;
            public GameBar Bar;
            public Image Fill;
        }

        private sealed class KeyRow
        {
            public GameObject Root;
            public Image Edge, Inside;
            public TMP_Text Name;
        }

        private HelpFrame _frame;
        private OptionRows _kit;
        private GameObject _holder;
        private RectTransform _root;
        private HelpLink[] _tools;
        private HelpLink[] _views;
        private Button _fold;
        private Transform _foldIcon;
        private float _canvasHigh, _windowHigh, _barHigh;
        private Action<int> _onTool, _onView, _onRow;
        private Action _onFold, _onClose;
        private readonly List<Part> _buttons = new List<Part>(16);
        private readonly List<Part> _switches = new List<Part>(2);
        private readonly List<TMP_Text> _paragraphs = new List<TMP_Text>(16);
        private readonly List<TMP_Text> _headings = new List<TMP_Text>(8);
        private readonly DemandRow[] _demand = new DemandRow[4];
        private readonly List<KeyRow> _keys = new List<KeyRow>(BuildModeRules.LegendMost);
        private int _buttonsUsed, _switchesUsed, _paragraphsUsed, _headingsUsed, _keysUsed;

        public bool Alive => _root != null;

        /// <summary>True while the window is up (stepped aside or not).</summary>
        public bool Shown => _root != null && _root.gameObject.activeSelf;

        /// <summary>True while it has stepped aside to its top bar.</summary>
        public bool Folded { get; private set; }

        /// <summary>The window, hidden, on the help's canvas. Null, with one line in Player.log, if the game's help or settings menu is not built as expected.</summary>
        public static BuildModePanel Make(Transform canvas, Action<int> onTool, Action<int> onView, Action<int> onRow, Action onFold, Action onClose)
        {
            if (canvas == null)
            {
                UiClone.Fallback("the game's help screen has no canvas to stand build mode's window on");
                return null;
            }
            var panel = new BuildModePanel { _onTool = onTool, _onView = onView, _onRow = onRow, _onFold = onFold, _onClose = onClose };
            try
            {
                panel._holder = UiClone.Dormant(canvas, "RealLife Build Mode (asleep)").gameObject;
                panel._frame = HelpFrame.Make(panel._holder.transform, "RealLife Build Mode");
                panel._kit = panel._frame != null ? OptionRows.Take(panel._holder.transform) : null;
                if (panel._frame == null || panel._kit == null)
                {
                    panel.Destroy();
                    return null;
                }
                panel.Build(canvas);
                return panel;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("build mode's window could not be built from the game's help screen");
                panel.Destroy();
                return null;
            }
        }

        private void Build(Transform canvas)
        {
            _root = (RectTransform)_frame.Root.transform;
            Vector2 size = _root.rect.size;
            _root.SetParent(canvas, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = size.x > 1f ? size : new Vector2(2400f, 1600f);
            _root.anchoredPosition = Vector2.zero;
            _root.SetAsLastSibling();
            _windowHigh = _root.sizeDelta.y;
            _canvasHigh = ((RectTransform)canvas).rect.height > 1f ? ((RectTransform)canvas).rect.height : 2160f;
            UiClone.Key(_frame.Headline, "reallife_bld_mode_title");
            UiClone.Words(_frame.Status, string.Empty);
            UiClone.Rewire(_frame.Close, _onClose);
            _frame.Back.gameObject.SetActive(false);      // no pages to go back through
            _frame.Search.gameObject.SetActive(false);    // nothing to look for: the stray "Name" field of the first window is gone
            // The forward button, turned, becomes the arrow that sends the window aside and brings it back, in the top bar.
            Transform bar = _frame.Close.transform.parent;
            _barHigh = bar is RectTransform barRect && barRect.rect.height > 1f ? barRect.rect.height : 120f;
            var fold = (RectTransform)_frame.Forward.transform;
            fold.SetParent(bar, false);
            fold.anchorMin = fold.anchorMax = fold.pivot = new Vector2(1f, 0.5f);
            fold.anchoredPosition = new Vector2(-(136f + 1300f + 24f), 0f);   // left of the status (HelpFrame: 1300 wide, 136 from the right)
            _fold = UiClone.Rewire(_frame.Forward, _onFold);
            _foldIcon = fold.Find("Icon");
            // The tools and the views as the help's categories, each always open; a link is a tool or a view.
            HelpGroup tools = _frame.AddGroup("Tools");
            UiClone.Key(tools.Name, "reallife_bld_mode_tools");
            tools.SetOpen(true);
            _tools = new HelpLink[(int)BuildTool.Count];
            for (int t = 0; t < _tools.Length; t++)
            {
                int tool = t;
                _tools[t] = tools.Add("Tool " + t);
                UiClone.Key(_tools[t].Name, BuildMode.ToolKey((BuildTool)t));
                UiClone.Rewire(_tools[t].Button, () => _onTool?.Invoke(tool));
            }
            UiClone.Rewire(tools.Button, null);
            HelpGroup views = _frame.AddGroup("Views");
            UiClone.Key(views.Name, "reallife_bld_mode_views");
            views.SetOpen(true);
            _views = new HelpLink[(int)BuildView.Count];
            for (int v = 0; v < _views.Length; v++)
            {
                int view = v;
                _views[v] = views.Add("View " + v);
                UiClone.Key(_views[v].Name, BuildModeRules.ViewKeys[v]);
                UiClone.Rewire(_views[v].Button, () => _onView?.Invoke(view));
            }
            UiClone.Rewire(views.Button, null);
            for (int u = 0; u < _demand.Length; u++)
            {
                _demand[u] = MakeDemand(u);
            }
            _root.gameObject.SetActive(false);   // the holder stays, asleep: the help's and the menu's templates sleep in it for the parts made later
        }

        // ------------------------------------------------------------------ showing

        public void Show(bool shown)
        {
            if (_root != null && _root.gameObject.activeSelf != shown)
            {
                _root.gameObject.SetActive(shown);
                if (shown)
                {
                    _root.SetAsLastSibling();
                }
            }
        }

        /// <summary>Steps aside (only the top bar shows, at the foot of the screen) or comes back to the middle.</summary>
        public void Fold(bool folded)
        {
            if (_root == null)
            {
                return;
            }
            Folded = folded;
            float y = folded ? -_canvasHigh * 0.5f + _barHigh + FootGap - _windowHigh * 0.5f : 0f;
            _root.anchoredPosition = new Vector2(0f, y);
            if (_foldIcon != null)
            {
                _foldIcon.localEulerAngles = new Vector3(0f, 0f, folded ? 90f : -90f);   // up: bring it back; down: send it aside
            }
        }

        public void Destroy()
        {
            for (int u = 0; u < _demand.Length; u++)
            {
                _demand[u]?.Bar?.Destroy();
                _demand[u] = null;
            }
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            if (_holder != null)
            {
                UnityEngine.Object.Destroy(_holder);
            }
            _root = null;
            _holder = null;
            _buttons.Clear();
            _switches.Clear();
            _paragraphs.Clear();
            _headings.Clear();
            _keys.Clear();
        }

        // ------------------------------------------------------------------ writing a page

        /// <summary>Starts a page: the tool and the view chosen in the list, the header's square, its small capitals, its name and note.</summary>
        public void Begin(BuildTool tool, BuildView view, Sprite glyph, Color square, string group, string name, string note)
        {
            for (int t = 0; t < _tools.Length; t++)
            {
                _tools[t].SetChosen(t == (int)tool);
            }
            for (int v = 0; v < _views.Length; v++)
            {
                _views[v].SetChosen(v == (int)view);
            }
            if (_frame.TopicGlyph != null)
            {
                if (glyph != null)
                {
                    _frame.TopicGlyph.sprite = glyph;
                }
                Image back = _frame.TopicGlyph.transform.parent != null ? _frame.TopicGlyph.transform.parent.GetComponent<Image>() : null;
                if (back != null)
                {
                    back.color = square;
                }
            }
            UiClone.Words(_frame.TopicCategory, group);
            UiClone.Words(_frame.TopicName, name);
            UiClone.Words(_frame.TopicNote, note ?? string.Empty);
            _buttonsUsed = _switchesUsed = _paragraphsUsed = _headingsUsed = _keysUsed = 0;
            for (int u = 0; u < _demand.Length; u++)
            {
                if (_demand[u] != null && _demand[u].Root.activeSelf)
                {
                    _demand[u].Root.SetActive(false);
                }
            }
        }

        /// <summary>The line in the top bar: the selection, or the square under the pointer.</summary>
        public void Status(string text)
        {
            UiClone.Words(_frame.Status, text ?? string.Empty);
        }

        public void Heading(string text)
        {
            TMP_Text t = Take(_headings, ref _headingsUsed, true);
            if (t != null)
            {
                UiClone.Words(t, text);
            }
        }

        public void Paragraph(string text)
        {
            TMP_Text t = Take(_paragraphs, ref _paragraphsUsed, false);
            if (t != null)
            {
                UiClone.Words(t, text);
            }
        }

        /// <summary>A row of the settings menu's with its button: a name, a line under it, the button's word; tinted in a colour, or the game's own look.</summary>
        public void Button(int id, string name, string note, string caption, bool usable, Color? tint = null)
        {
            Part part = TakeRow(_buttons, ref _buttonsUsed, OptionRowKind.Button);
            part.Id = id;
            UiClone.Words(part.Row.Name, name);
            if (part.Row.Note != null)
            {
                part.Row.Note.gameObject.SetActive(!string.IsNullOrEmpty(note));
                UiClone.Words(part.Row.Note, note ?? string.Empty);
            }
            UiClone.Words(part.Row.Caption, caption);
            part.Row.SetUsable(usable);
            if (part.Row.Button != null && part.Row.Button.image != null)
            {
                part.Row.Button.image.color = tint ?? Color.white;   // a zone's button in its colour; any other the game's own
            }
        }

        /// <summary>A switch row of the settings menu's.</summary>
        public void Switch(int id, string name, string note, bool on, bool usable)
        {
            Part part = TakeRow(_switches, ref _switchesUsed, OptionRowKind.Switch);
            part.Id = id;
            UiClone.Words(part.Row.Name, name);
            if (part.Row.Note != null)
            {
                part.Row.Note.gameObject.SetActive(!string.IsNullOrEmpty(note));
                UiClone.Words(part.Row.Note, note ?? string.Empty);
            }
            part.Row.Switch.SetIsOnWithoutNotify(on);
            part.Row.SetUsable(usable);
        }

        /// <summary>A use's demand as one of the top bar's bars, in the use's colour, with its name and its figure.</summary>
        public void Demand(int use, string name, int value, Color colour)
        {
            DemandRow d = use >= 0 && use < _demand.Length ? _demand[use] : null;
            if (d == null)
            {
                return;
            }
            d.Root.transform.SetAsLastSibling();
            d.Root.SetActive(true);
            UiClone.Words(d.Name, name);
            UiClone.Words(d.Value, value < 0 ? "-" : value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (d.Fill != null)
            {
                d.Fill.color = new Color(colour.r, colour.g, colour.b, 1f);
            }
            if (d.Bar != null)
            {
                d.Bar.Show(true);
                d.Bar.Fill(Mathf.Clamp01(Math.Max(0, value) / 100f));
            }
        }

        /// <summary>A legend row: a square of the fill inside its edge's colour (an edge alone: the window's own colour inside), and its words.</summary>
        public void Key(Color fill, Color edge, bool edgeOnly, string text)
        {
            KeyRow k;
            if (_keysUsed < _keys.Count)
            {
                k = _keys[_keysUsed];
            }
            else
            {
                k = MakeKey();
                _keys.Add(k);
            }
            _keysUsed++;
            k.Root.transform.SetAsLastSibling();
            k.Root.SetActive(true);
            k.Edge.color = new Color(edge.r, edge.g, edge.b, 1f);
            k.Inside.color = edgeOnly ? DarkLook.Back : new Color(fill.r, fill.g, fill.b, 1f);
            UiClone.Words(k.Name, text);
        }

        /// <summary>Ends a page: the parts not written hidden, the page laid out at once, scrolled to its top when asked.</summary>
        public void End(bool top)
        {
            Hide(_buttons, _buttonsUsed);
            Hide(_switches, _switchesUsed);
            HideTexts(_paragraphs, _paragraphsUsed);
            HideTexts(_headings, _headingsUsed);
            for (int i = _keysUsed; i < _keys.Count; i++)
            {
                if (_keys[i].Root.activeSelf)
                {
                    _keys[i].Root.SetActive(false);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            if (top)
            {
                _frame.Page.anchoredPosition = new Vector2(_frame.Page.anchoredPosition.x, 0f);
                _frame.PageScroll.velocity = Vector2.zero;
            }
        }

        // ------------------------------------------------------------------ the parts

        private TMP_Text Take(List<TMP_Text> pool, ref int used, bool heading)
        {
            TMP_Text t;
            if (used < pool.Count)
            {
                t = pool[used];
            }
            else
            {
                t = heading ? _frame.NewSmall() : _frame.NewParagraph();
                if (t == null)
                {
                    return null;
                }
                pool.Add(t);
            }
            used++;
            t.transform.SetAsLastSibling();
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }
            return t;
        }

        private Part TakeRow(List<Part> pool, ref int used, OptionRowKind kind)
        {
            Part part;
            if (used < pool.Count)
            {
                part = pool[used];
            }
            else
            {
                part = new Part { Row = _kit.Make(kind, _frame.Page) };
                Part wired = part;
                if (kind == OptionRowKind.Switch)
                {
                    part.Row.Switch.onValueChanged.AddListener(_ => UiClone.Safe(() => _onRow?.Invoke(wired.Id)));
                }
                else
                {
                    UiClone.Rewire(part.Row.Button, () => _onRow?.Invoke(wired.Id));
                }
                pool.Add(part);
            }
            used++;
            part.Row.Root.transform.SetAsLastSibling();
            part.Row.Show(true);
            return part;
        }

        private static void Hide(List<Part> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                pool[i].Row.Show(false);
            }
        }

        private static void HideTexts(List<TMP_Text> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(false);
                }
            }
        }

        // A row of the page laid by hand inside: a fixed height for the page's layout, its parts placed from its left.
        private GameObject Strip(string name, float high)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.layer = _frame.Page.gameObject.layer;
            go.transform.SetParent(_frame.Page, false);
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = high;
            element.flexibleWidth = 1f;
            return go;
        }

        // A one-line text of the help's paragraph, placed in a strip from its left.
        private TMP_Text Words(Transform strip, float from, float wide, TextAlignmentOptions align)
        {
            TMP_Text t = _frame.NewParagraph();
            if (t == null)
            {
                return null;
            }
            t.transform.SetParent(strip, false);
            var element = t.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.ignoreLayout = true;
            }
            var rect = (RectTransform)t.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(from, 0f);
            rect.sizeDelta = new Vector2(wide, 0f);
            t.fontSize = TextSize;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private DemandRow MakeDemand(int use)
        {
            GameObject strip = Strip("Demand " + use, DemandHigh);
            var d = new DemandRow { Root = strip };
            d.Name = Words(strip.transform, 0f, LabelWide - 20f, TextAlignmentOptions.MidlineLeft);
            d.Bar = GameBar.Make(strip.transform, "Demand bar " + use);
            float barWide = 600f;
            if (d.Bar != null)
            {
                RectTransform r = d.Bar.Rect;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(LabelWide, 0f);
                barWide = r.sizeDelta.x;
                d.Fill = r.childCount > 0 ? r.GetChild(0).GetComponent<Image>() : null;   // the filler, tinted the use's colour
                Canvas own = r.GetComponent<Canvas>();
                if (own != null)
                {
                    UnityEngine.Object.DestroyImmediate(own);   // drawn with the page, so the page's scroll clips it as it clips the rest
                }
            }
            d.Value = Words(strip.transform, LabelWide + barWide + 24f, 120f, TextAlignmentOptions.MidlineLeft);
            strip.SetActive(false);
            return d;
        }

        private KeyRow MakeKey()
        {
            GameObject strip = Strip("Key", KeyHigh);
            var k = new KeyRow { Root = strip };
            k.Edge = Square(strip.transform, "Edge", SwatchSize, 0f);
            k.Inside = Square(k.Edge.transform, "Inside", SwatchSize - SwatchEdge * 2f, 0f);
            ((RectTransform)k.Inside.transform).anchorMin = ((RectTransform)k.Inside.transform).anchorMax = ((RectTransform)k.Inside.transform).pivot = new Vector2(0.5f, 0.5f);
            ((RectTransform)k.Inside.transform).anchoredPosition = Vector2.zero;
            k.Name = Words(strip.transform, SwatchSize + 24f, 1400f, TextAlignmentOptions.MidlineLeft);
            return k;
        }

        // A plain square of colour, looked at only (it takes no pointer).
        private static Image Square(Transform parent, string name, float size, float from)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(from, 0f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // ------------------------------------------------------------------ the test's measure

        /// <summary>
        /// The page as drawn now, for the test: every part shown stands below the one before it with no overlap, no text is cut
        /// short or runs out of its box, and the list's links do not overlap. Null when all is well, else what is wrong.
        /// </summary>
        public string Overlap()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy)
            {
                return "the window is not up";
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            string wrong = Stacked(_frame.Page, "the page");
            if (wrong != null)
            {
                return wrong;
            }
            foreach (TMP_Text t in _frame.Page.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text))
                {
                    continue;
                }
                t.ForceMeshUpdate();
                RectTransform r = t.rectTransform;
                if (t.isTextTruncated || (t.textWrappingMode != TextWrappingModes.NoWrap && t.preferredHeight > r.rect.height + 2f))
                {
                    return "'" + Short(t.text) + "' is cut short (" + t.preferredHeight.ToString("0") + " high in " + r.rect.height.ToString("0") + ")";
                }
            }
            for (int i = 0; i < _tools.Length; i++)
            {
                for (int k = 0; k < _views.Length; k++)
                {
                    if (Meets((RectTransform)_tools[i].Root.transform, (RectTransform)_views[k].Root.transform))
                    {
                        return "the list's links meet (" + i + ", " + k + ")";
                    }
                }
            }
            return null;
        }

        // Each shown child below the one before it.
        private static string Stacked(RectTransform parent, string what)
        {
            float lastBottom = float.MaxValue;
            string lastName = null;
            var corners = new Vector3[4];
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout)
                {
                    continue;
                }
                child.GetWorldCorners(corners);
                float top = corners[1].y, bottom = corners[0].y;
                if (lastName != null && top > lastBottom + 0.5f)
                {
                    return what + ": '" + child.name + "' overlaps '" + lastName + "' by " + (top - lastBottom).ToString("0.0");
                }
                lastBottom = bottom;
                lastName = child.name;
            }
            return null;
        }

        private static bool Meets(RectTransform a, RectTransform b)
        {
            if (!a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy)
            {
                return false;
            }
            var ca = new Vector3[4];
            var cb = new Vector3[4];
            a.GetWorldCorners(ca);
            b.GetWorldCorners(cb);
            return ca[0].x < cb[2].x - 0.5f && cb[0].x < ca[2].x - 0.5f && ca[0].y < cb[1].y - 0.5f && cb[0].y < ca[1].y - 0.5f;
        }

        private static string Short(string text) => text.Length > 40 ? text.Substring(0, 40) : text;
    }
}
