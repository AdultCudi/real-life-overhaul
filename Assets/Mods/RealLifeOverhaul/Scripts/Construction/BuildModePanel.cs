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
    /// on the map". The cards and the foot's buttons are the designer's tool button (BuildModeKit). Closed, the map is all there
    /// is. Built at its first opening; written when a filter or a card is picked; nothing runs a frame of its own.
    /// </summary>
    internal sealed class BuildModePanel
    {
        public static IModLogger Logger;

        private const float FootHigh = 150f;    // the foot that stays in view, in the help canvas's units
        private const float FootGap = 40f;      // ... from the window's edges
        private const float CardHigh = 480f;
        private const float CardGap = 36f;
        private const float PictureHigh = 150f;

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
            _gridLayout.cellSize = new Vector2(Mathf.Floor((width - CardGap * 2f - 8f) / 3f), CardHigh);
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
            BuildModeBar.Paint(c.Button, _kit.White ?? _kit.Line, DarkLook.Raised);
            if (c.Button.Chosen != null)
            {
                c.Button.Chosen.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
                ((RectTransform)c.Button.Chosen.transform).sizeDelta = Vector2.zero;
                BuildModeKit.Size(c.Button.Chosen, 0f, 0f).ignoreLayout = true;
            }
            if (c.Button.Icon != null)
            {
                c.Button.Icon.gameObject.SetActive(false);
                BuildModeKit.Size(c.Button.Icon.gameObject, 0f, 0f).ignoreLayout = true;
            }
            var layout = c.Button.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Image ground = BuildModeKit.Picture(c.Button.Root, "Picture", _kit.White ?? _kit.Line, DarkLook.Sunk, -1f, PictureHigh);
            c.Picture = BuildModeKit.Picture(ground.transform, "Use", null, new Color(0.62f, 0.78f, 0.91f, 1f), 110f, 110f);
            var pic = (RectTransform)c.Picture.transform;
            pic.anchorMin = pic.anchorMax = pic.pivot = new Vector2(0.5f, 0.5f);
            pic.anchoredPosition = Vector2.zero;
            c.Picture.GetComponent<LayoutElement>().ignoreLayout = true;
            c.Name = _kit.Text(c.Button.Root, "Name", 40f, DarkLook.Text, true, true);
            c.Kind = _kit.Text(c.Button.Root, "Kind", 30f, DarkLook.Soft, false, true);
            RectTransform row = BuildModeKit.Strip(c.Button.Root, "Figures", true, new RectOffset(), 16f, TextAnchor.MiddleLeft);
            c.Size = _kit.Text(row, "Size", 30f, DarkLook.Muted, false, true);
            c.Size.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            c.Cost = _kit.Text(row, "Cost", 30f, DarkLook.Text, true, false);
            return c;
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

        /// <summary>The window as drawn now: no text cut short, no card over another or past the page's sides, the foot apart from the page. Null when all is well.</summary>
        public string Overlap()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy)
            {
                return "the design window is not up";
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Page);
            foreach (TMP_Text t in _root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.transform.IsChildOf(_grid) && !t.transform.IsChildOf(_foot))
                {
                    continue;
                }
                t.ForceMeshUpdate();
                RectTransform r = t.rectTransform;
                if (t.isTextTruncated || t.preferredHeight > r.rect.height + 2f || (t.textWrappingMode == TextWrappingModes.NoWrap && !t.enableAutoSizing && t.preferredWidth > r.rect.width + 2f))
                {
                    return "'" + (t.text.Length > 40 ? t.text.Substring(0, 40) : t.text) + "' is cut short";
                }
            }
            Rect page = BuildModeBar.World((RectTransform)_grid.parent);
            Rect foot = BuildModeBar.World(_foot);
            Rect view = BuildModeBar.World((RectTransform)_frame.PageScroll.transform);
            if (view.Overlaps(foot))
            {
                return "the page runs under the foot";
            }
            for (int i = 0; i < _cardsUsed; i++)
            {
                Rect a = BuildModeBar.World(_cards[i].Button.Root);
                if (a.xMin < page.xMin - 0.5f || a.xMax > page.xMax + 0.5f)
                {
                    return "a card runs past the page's side";
                }
                foreach (Transform part in _cards[i].Button.Root)
                {
                    var r = part as RectTransform;
                    if (r != null && part.gameObject.activeSelf && (part.GetComponent<LayoutElement>() == null || !part.GetComponent<LayoutElement>().ignoreLayout))
                    {
                        Rect p = BuildModeBar.World(r);
                        if (p.yMin < a.yMin - 0.5f)
                        {
                            return "a card's '" + part.name + "' runs out of its card";
                        }
                    }
                }
                for (int k = i + 1; k < _cardsUsed; k++)
                {
                    if (a.Overlaps(BuildModeBar.World(_cards[k].Button.Root)))
                    {
                        return "two cards meet";
                    }
                }
            }
            return null;
        }
    }
}
