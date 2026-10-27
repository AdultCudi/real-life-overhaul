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
    /// Build mode's one big window (the owner, 26 Oct 2026: the map first, a window in the middle only to choose among many
    /// designs): "Choose a building", a copy of the game's help screen (GameUi.HelpFrame, 2400 by 1600 in the middle, in the
    /// mod's dark colours). Its list on the left holds the filters as the help's links (Fits here, Corner designs, City designs,
    /// each with its count), its page a line saying for which squares, then the designs as cards in a grid of three (the game's
    /// picture of the use, the name, the kind and use, storeys and floor area, the estimate; one that cannot be built there
    /// greyed with the reason), and a foot that stays in view with the design picked in one line, "Back to the map" and "Place it
    /// on the map". The cards and the foot's buttons are the designer's tool button (BuildModeKit). The page is a scroll view
    /// clipped to its area (a RectMask2D on the game's viewport, the scrollbar and the wheel the game's own); every card is the
    /// same size with the same fixed rows (the game's picture, the name, the kind, the storeys and floor area, the price on a line
    /// of its own), the one picked shown by a blue edge and never by its size; the window is scaled to the screen and the game's
    /// UI scale and stands clear of the toolbar. Closed, the map is all there is. Built at its first opening; written when a
    /// filter or a card is picked; nothing runs a frame of its own.
    /// </summary>
    internal sealed class BuildModePanel
    {
        public static IModLogger Logger;

        private const float FootHigh = 150f;    // the foot that stays in view, in the help canvas's units
        private const float FootGap = 40f;      // ... from the window's edges
        private const float CardGap = BuildModeCatalogue.CardGap;
        private const float PictureHigh = 150f;
        private const float CardPad = 24f;          // inside a card's edge
        private const float CardSpace = 12f;        // between the rows of a card
        private const float NameHigh = 56f;         // each row of a card has one height whatever it says, so every card is the same size
        private const float KindHigh = 84f;         // two lines: the kind, or why the design cannot go there
        private const float FigureHigh = 44f;
        private const float PriceHigh = 56f;
        private const float CardEdge = 6f;          // the blue edge of the card picked, in the window's units
        private const float ToolbarGap = 12f;       // kept clear above the toolbar
        private const float CardHigh = CardPad * 2f + PictureHigh + NameHigh + KindHigh + FigureHigh + PriceHigh + CardSpace * 4f;   // the card's height, from its rows

        private sealed class Card
        {
            public KitButton Button;
            public Image Picture;
            public TMP_Text Name, Kind, Size, Cost;
            public int Index;
        }

        private HelpFrame _frame;
        private BuildModeKit _kit;
        private GameObject _holder;
        private RectTransform _root, _grid, _foot;
        private Vector2 _designSize = new Vector2(2400f, 1600f);   // the window's own size, in the canvas's units at 3840 by 2160
        private GridLayoutGroup _gridLayout;
        private TMP_Text _context, _summary;
        private KitButton _back, _place;
        private HelpLink[] _filters;
        private Action<int> _onFilter, _onCard;
        private Action _onBack, _onPlace;
        private readonly List<Card> _cards = new List<Card>(32);
        private int _cardsUsed;

        public bool Alive => _root != null;

        public bool Shown => _root != null && _root.gameObject.activeSelf;

        /// <summary>The window, hidden, on the help's canvas. Null, with one line in Player.log, if the game's help is not built as expected.</summary>
        public static BuildModePanel Make(Transform canvas, BuildModeKit kit, Action<int> onFilter, Action<int> onCard, Action onBack, Action onPlace)
        {
            if (canvas == null || kit == null)
            {
                UiClone.Fallback("the game's help screen has no canvas to stand build mode's design window on");
                return null;
            }
            var panel = new BuildModePanel { _kit = kit, _onFilter = onFilter, _onCard = onCard, _onBack = onBack, _onPlace = onPlace };
            try
            {
                panel._holder = UiClone.Dormant(canvas, "RealLife Build Mode designs (asleep)").gameObject;
                panel._frame = HelpFrame.Make(panel._holder.transform, "RealLife Build Mode designs");
                if (panel._frame == null)
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
                UiClone.Fallback("build mode's design window could not be built from the game's help screen");
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
            _designSize = _root.sizeDelta;
            _root.anchoredPosition = Vector2.zero;
            UiClone.Key(_frame.Headline, "reallife_bld_cat_title");
            UiClone.Words(_frame.Status, string.Empty);
            UiClone.Rewire(_frame.Close, _onBack);
            _frame.Back.gameObject.SetActive(false);
            _frame.Forward.gameObject.SetActive(false);
            _frame.Search.gameObject.SetActive(false);
            if (_frame.TopicGlyph != null && _frame.TopicGlyph.transform.parent != null)
            {
                _frame.TopicGlyph.transform.parent.parent.gameObject.SetActive(false);   // the help's topic header: the page starts with what it is for
            }

            // The filters as the help's links.
            HelpGroup group = _frame.AddGroup("Filters");
            UiClone.Key(group.Name, "reallife_bld_cat_show");
            group.SetOpen(true);
            UiClone.Rewire(group.Button, null);
            _filters = new HelpLink[BuildModeCatalogue.FilterCount];
            for (int f = 0; f < _filters.Length; f++)
            {
                int filter = f;
                _filters[f] = group.Add("Filter " + f);
                UiClone.Rewire(_filters[f].Button, () => _onFilter?.Invoke(filter));
            }

            // The page: what it is for, then the grid.
            _context = _frame.NewParagraph();
            _context.fontSize = 34f;
            var grid = new GameObject("Designs", typeof(RectTransform));
            grid.layer = _frame.Page.gameObject.layer;
            grid.transform.SetParent(_frame.Page, false);
            _grid = (RectTransform)grid.transform;
            _gridLayout = grid.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = new Vector2(CardGap, CardGap);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = 3;
            _gridLayout.cellSize = new Vector2(500f, CardHigh);
            _gridLayout.childAlignment = TextAnchor.UpperLeft;

            // The foot that stays in view: the design picked, back to the map, place it.
            var page = (RectTransform)_frame.PageScroll.transform;
            page.offsetMin = new Vector2(page.offsetMin.x, page.offsetMin.y + FootHigh + FootGap);
            ClipPage();
            _foot = BuildModeKit.Strip(_root, "Foot", true, new RectOffset(40, 40, 20, 20), 28f, TextAnchor.MiddleLeft);
            _foot.anchorMin = new Vector2(0f, 0f);
            _foot.anchorMax = new Vector2(1f, 0f);
            _foot.pivot = new Vector2(0.5f, 0f);
            _foot.offsetMin = new Vector2(FootGap, FootGap);
            _foot.offsetMax = new Vector2(-FootGap, FootGap + FootHigh);
            BuildModeKit.Picture(_foot, "Ground", _kit.White ?? _kit.Line, DarkLook.Sunk, 0f, 0f).GetComponent<LayoutElement>().ignoreLayout = true;
            Stretch((RectTransform)_foot.GetChild(0));
            _summary = _kit.Text(_foot, "Picked", 34f, DarkLook.Soft, false, true);
            _summary.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _back = FootButton("reallife_bld_cat_back", () => _onBack?.Invoke(), 400f, false);
            _place = FootButton("reallife_bld_cat_place", () => _onPlace?.Invoke(), 480f, true);
            _root.gameObject.SetActive(false);
        }

        // The list is clipped to its area (the owner's screenshot, 27 Oct 2026: the seventh card drew below the window, over the foot and
        // the toolbar). The game's viewport holds a stencil Mask, which did not hold the cards in; a RectMask2D clips what is drawn to the
        // viewport's rectangle, whatever is in the stencil buffer, and the scroll view is the game's own (its scrollbar, the wheel).
        private void ClipPage()
        {
            ScrollRect scroll = _frame.PageScroll;
            RectTransform view = scroll.viewport != null ? scroll.viewport : scroll.transform.Find("Viewport") as RectTransform;
            if (view != null && view.GetComponent<RectMask2D>() == null)
            {
                view.gameObject.AddComponent<RectMask2D>();
            }
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = Math.Max(scroll.scrollSensitivity, 50f);   // the wheel moves the list as it does the game's help
        }

        private KitButton FootButton(string key, Action click, float wide, bool main)
        {
            KitButton b = _kit.Button(_foot, key, click, wide, 108f);
            if (b.Icon != null)
            {
                b.Icon.gameObject.SetActive(false);
            }
            b.Label = _kit.Text(b.Root, "Words", 38f, Color.white, true, false);
            Stretch((RectTransform)b.Label.transform);
            b.Label.alignment = TextAlignmentOptions.Center;
            b.Label.enableAutoSizing = true;
            b.Label.fontSizeMin = 24f;
            b.Label.fontSizeMax = 38f;
            UiClone.Key(b.Label, key);
            BuildModeBar.Paint(b, main ? _kit.Blue : _kit.White ?? _kit.Line, main ? Color.white : BuildModeBar.ButtonGrey);
            return b;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(16f, 0f);
            r.offsetMax = new Vector2(-16f, 0f);
            var e = r.GetComponent<LayoutElement>() ?? r.gameObject.AddComponent<LayoutElement>();
            e.ignoreLayout = true;
        }

        // ------------------------------------------------------------------ showing and writing

        public void Show(bool shown) => Show(shown, float.NaN);

        /// <summary>
        /// Shows or hides the window. Shown, it is drawn at the scale that fits the screen now (the canvas's size follows the game's UI
        /// scale) and clear of the toolbar, whose top is <paramref name="toolbarTop"/> (world y; NaN for unmeasured).
        /// </summary>
        public void Show(bool shown, float toolbarTop)
        {
            if (_root != null && _root.gameObject.activeSelf != shown)
            {
                if (shown)
                {
                    Fit(toolbarTop);
                }
                _root.gameObject.SetActive(shown);
                if (shown)
                {
                    _root.SetAsLastSibling();
                }
            }
        }

        // The window is centred on the canvas, so the room it has is as much above the toolbar's top as below it. Scaled, never resized:
        // the page, the cards and the foot keep their layout, so what is measured here is all that decides whether it fits.
        private void Fit(float toolbarTop)
        {
            var canvas = _root.parent as RectTransform;
            if (canvas == null)
            {
                return;
            }
            Rect units = canvas.rect;
            float free = units.height * BuildModeCatalogue.HighShare;
            float unit = canvas.lossyScale.y;
            if (!float.IsNaN(toolbarTop) && unit > 0f)
            {
                float middle = canvas.TransformPoint(units.center).y;
                free = 2f * (middle - toolbarTop) / unit - 2f * ToolbarGap;
            }
            float s = BuildModeCatalogue.Scale(units.width, free, _designSize.x, _designSize.y);
            _root.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Starts the page: what it is for, the filters' names with their counts and the one chosen.</summary>
        public void Begin(string context, string[] filterNames, int chosen)
        {
            UiClone.Words(_context, context);
            for (int f = 0; f < _filters.Length; f++)
            {
                UiClone.Words(_filters[f].Name, filterNames[f]);
                _filters[f].SetChosen(f == chosen);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            float width = _frame.Page.rect.width > 600f ? _frame.Page.rect.width : 1600f;
            int columns = BuildModeCatalogue.Columns(width);
            _gridLayout.constraintCount = columns;
            _gridLayout.cellSize = new Vector2(BuildModeCatalogue.CardWide(width, columns), CardHigh);
            _cardsUsed = 0;
        }

        /// <summary>A design's card: the game's picture of its use, its name, kind and use (or why it cannot go there), its size and its estimate.</summary>
        public void Add(int index, Sprite picture, string name, string kind, string size, string cost, bool usable, bool chosen)
        {
            Card c;
            if (_cardsUsed < _cards.Count)
            {
                c = _cards[_cardsUsed];
            }
            else
            {
                c = MakeCard();
                _cards.Add(c);
            }
            _cardsUsed++;
            c.Index = index;
            c.Picture.sprite = picture;
            c.Picture.gameObject.SetActive(picture != null);
            UiClone.Words(c.Name, name);
            UiClone.Words(c.Kind, kind);
            c.Kind.color = usable ? DarkLook.Soft : BuildModeTip.Why;
            UiClone.Words(c.Size, size);
            UiClone.Words(c.Cost, cost);
            c.Button.SetChosen(chosen);
            c.Button.SetUsable(usable);
            c.Button.Root.SetAsLastSibling();
            c.Button.Show(true);
        }

        private Card MakeCard()
        {
            var c = new Card();
            Card wired = c;
            c.Button = _kit.Button(_grid, "Card", () => _onCard?.Invoke(wired.Index), -1f, -1f);
            BuildModeKit.KeepSize(c.Button);   // the designer's buttons grow by a tenth under the pointer: a card never changes size
            if (c.Button.Icon != null)
            {
                c.Button.Icon.gameObject.SetActive(false);
                BuildModeKit.Size(c.Button.Icon.gameObject, 0f, 0f).ignoreLayout = true;
            }
            if (c.Button.Chosen != null)
            {
                BuildModeKit.Size(c.Button.Chosen, 0f, 0f).ignoreLayout = true;   // the designer's blue is not used: the card picked has a blue edge
            }
            _kit.Frame(c.Button, DarkLook.Raised, false, CardEdge);   // a face inside an edge; picked, the edge turns blue
            var layout = c.Button.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = (int)CardPad;
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = CardSpace;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Image ground = BuildModeKit.Picture(c.Button.Root, "Picture", _kit.White ?? _kit.Line, DarkLook.Sunk, -1f, PictureHigh);
            c.Picture = BuildModeKit.Picture(ground.transform, "Use", null, new Color(0.62f, 0.78f, 0.91f, 1f), 110f, 110f);
            var pic = (RectTransform)c.Picture.transform;
            pic.anchorMin = pic.anchorMax = pic.pivot = new Vector2(0.5f, 0.5f);
            pic.anchoredPosition = Vector2.zero;
            c.Picture.GetComponent<LayoutElement>().ignoreLayout = true;
            c.Name = Row(c.Button.Root, "Name", 40f, 28f, DarkLook.Text, true, false, NameHigh);
            c.Kind = Row(c.Button.Root, "Kind", 30f, 22f, DarkLook.Soft, false, true, KindHigh);
            c.Size = Row(c.Button.Root, "Size", 30f, 22f, DarkLook.Muted, false, false, FigureHigh);
            c.Cost = Row(c.Button.Root, "Cost", 38f, 26f, DarkLook.Text, true, false, PriceHigh);
            return c;
        }

        // One row of a card: a fixed height whatever it says, its words shrinking to fit before they would run past the card (a long word
        // in another language), the kind alone wrapping over its two lines.
        private TMP_Text Row(Transform card, string name, float size, float least, Color colour, bool bold, bool wrap, float high)
        {
            TMP_Text t = _kit.Text(card, name, size, colour, bold, wrap);
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = least;
            t.raycastTarget = false;
            BuildModeKit.Size(t.gameObject, -1f, high).flexibleWidth = 1f;
            return t;
        }

        /// <summary>Ends the page: the cards not written hidden, the foot's line, Place usable or not, scrolled to the top when asked.</summary>
        public void End(string summary, bool placeUsable, bool top)
        {
            for (int i = _cardsUsed; i < _cards.Count; i++)
            {
                _cards[i].Button.Show(false);
            }
            UiClone.Words(_summary, summary);
            _place.SetUsable(placeUsable);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_foot);
            if (top)
            {
                _frame.Page.anchoredPosition = new Vector2(_frame.Page.anchoredPosition.x, 0f);
                _frame.PageScroll.velocity = Vector2.zero;
            }
        }

        public void Destroy()
        {
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
            _cards.Clear();
        }

        // ------------------------------------------------------------------ the test's measure

        /// <summary>The heading as written now (what the page is for), for the tests.</summary>
        public string Heading => _context != null ? _context.text : string.Empty;

        /// <summary>How many cards are written now, for the tests.</summary>
        public int CardsShown => _cardsUsed;

        /// <summary>The scale the window is drawn at now (1 where the screen has room), for the tests.</summary>
        public float Scale => _root != null ? _root.localScale.x : 1f;

        /// <summary>
        /// The window as drawn now (the owner's screenshot, 27 Oct 2026: the seventh card ran over the foot and the toolbar): no heading
        /// that shows an id, no text cut short, the window on the screen and above the toolbar (<paramref name="toolbarTop"/>, world y,
        /// NaN for unmeasured), the list clipped to its area and between the window's top and its foot, the foot inside the window, every
        /// card the same size, at its own size, inside the list's rectangle and with its rows apart, and the first and the last card
        /// each reached by scrolling the list. Null when all is well, else what is wrong.
        /// </summary>
        public string Overlap(float toolbarTop)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy)
            {
                return "the design window is not up";
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            if (StreetNameRules.HasId(Heading))
            {
                return "the heading shows an id: '" + Heading + "'";
            }
            foreach (TMP_Text t in _root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.transform.IsChildOf(_grid) && !t.transform.IsChildOf(_foot))
                {
                    continue;
                }
                t.ForceMeshUpdate();
                RectTransform r = t.rectTransform;
                if (t.isTextTruncated || (t.enableAutoSizing ? t.renderedHeight : t.preferredHeight) > r.rect.height + 2f || (t.textWrappingMode == TextWrappingModes.NoWrap && !t.enableAutoSizing && t.preferredWidth > r.rect.width + 2f)
                    || (t.textWrappingMode == TextWrappingModes.NoWrap && t.enableAutoSizing && t.renderedWidth > r.rect.width + 2f))   // a row shrunk to its least still too wide
                {
                    return "'" + (t.text.Length > 40 ? t.text.Substring(0, 40) : t.text) + "' is cut short";
                }
            }
            Rect panel = BuildModeBar.World(_root);
            var canvas = _root.parent as RectTransform;
            if (canvas != null)
            {
                Rect screen = BuildModeBar.World(canvas);
                if (panel.xMin < screen.xMin - 0.5f || panel.xMax > screen.xMax + 0.5f || panel.yMin < screen.yMin - 0.5f || panel.yMax > screen.yMax + 0.5f)
                {
                    return "the window runs off the screen";
                }
            }
            if (!float.IsNaN(toolbarTop) && panel.yMin < toolbarTop - 0.5f)
            {
                return "the window covers the toolbar";
            }
            RectTransform viewport = _frame.PageScroll.viewport;
            if (viewport == null)
            {
                return "the list has no viewport";
            }
            RectMask2D clip = viewport.GetComponent<RectMask2D>();
            if (clip == null || !clip.enabled || !viewport.gameObject.activeInHierarchy)
            {
                return "the list is not clipped to its area";
            }
            Rect view = BuildModeBar.World(viewport);
            Rect foot = BuildModeBar.World(_foot);
            if (view.yMin < panel.yMin - 0.5f || view.yMax > panel.yMax + 0.5f || view.xMin < panel.xMin - 0.5f || view.xMax > panel.xMax + 0.5f)
            {
                return "the list runs out of the window";
            }
            if (view.Overlaps(foot))
            {
                return "the list runs under the foot";
            }
            if (foot.yMin < panel.yMin - 0.5f || foot.yMax > panel.yMax + 0.5f || foot.xMin < panel.xMin - 0.5f || foot.xMax > panel.xMax + 0.5f)
            {
                return "the foot is out of the window";
            }
            Rect list = BuildModeBar.World(_grid);
            Vector2 size = _cardsUsed > 0 ? _cards[0].Button.Root.rect.size : Vector2.zero;
            for (int i = 0; i < _cardsUsed; i++)
            {
                RectTransform root = _cards[i].Button.Root;
                if (Mathf.Abs(root.rect.width - size.x) > 0.5f || Mathf.Abs(root.rect.height - size.y) > 0.5f)
                {
                    return "the cards are not all the same size";
                }
                if (Mathf.Abs(root.localScale.x - 1f) > 0.001f || Mathf.Abs(root.localScale.y - 1f) > 0.001f)
                {
                    return "a card has been resized (scale " + root.localScale.x.ToString("0.00") + ")";
                }
                Rect a = BuildModeBar.World(root);
                if (a.xMin < list.xMin - 0.5f || a.xMax > list.xMax + 0.5f || a.yMin < list.yMin - 0.5f || a.yMax > list.yMax + 0.5f)
                {
                    return "a card lies outside the list's rectangle";
                }
                string inside = RowsApart(root, a);
                if (inside != null)
                {
                    return inside;
                }
                for (int k = i + 1; k < _cardsUsed; k++)
                {
                    if (a.Overlaps(BuildModeBar.World(_cards[k].Button.Root)))
                    {
                        return "two cards meet";
                    }
                }
            }
            return Reached(view);
        }

        // A card's rows each inside the card and none on another (the price on a line of its own, never beside the figures).
        private static string RowsApart(RectTransform card, Rect a)
        {
            Rect before = default(Rect);
            string beforeName = null;
            foreach (Transform part in card)
            {
                var r = part as RectTransform;
                if (r == null || !part.gameObject.activeSelf || (part.GetComponent<LayoutElement>() != null && part.GetComponent<LayoutElement>().ignoreLayout))
                {
                    continue;
                }
                Rect p = BuildModeBar.World(r);
                if (p.yMin < a.yMin - 0.5f || p.xMin < a.xMin - 0.5f || p.xMax > a.xMax + 0.5f || p.yMax > a.yMax + 0.5f)
                {
                    return "a card's '" + part.name + "' runs out of its card";
                }
                if (beforeName != null && p.Overlaps(before))
                {
                    return "a card's '" + part.name + "' meets its '" + beforeName + "'";
                }
                before = p;
                beforeName = part.name;
            }
            return null;
        }

        // The list scrolled to its foot and back: the last card, then the first, each wholly inside the viewport at its end of the list.
        private string Reached(Rect view)
        {
            if (_cardsUsed == 0)
            {
                return null;
            }
            ScrollRect scroll = _frame.PageScroll;
            float was = scroll.verticalNormalizedPosition;
            try
            {
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                Rect last = BuildModeBar.World(_cards[_cardsUsed - 1].Button.Root);
                if (last.yMin < view.yMin - 0.5f || last.yMax > view.yMax + 0.5f)
                {
                    return "the last card cannot be scrolled into view";
                }
                scroll.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                Rect first = BuildModeBar.World(_cards[0].Button.Root);
                if (first.yMin < view.yMin - 0.5f || first.yMax > view.yMax + 0.5f)
                {
                    return "the first card cannot be scrolled into view";
                }
                return null;
            }
            finally
            {
                scroll.verticalNormalizedPosition = was;
            }
        }
    }
}
