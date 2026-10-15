using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAModAPI;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Pages for the HUD phone's home screen. The game's grid is two buttons wide and exactly four rows fit above the
    /// phone's lower edge (cells of 250 by 225 in a box 868 high), so its own eight apps fill it: a ninth button has
    /// to go on a second page.
    ///
    /// The same pages serve the row of app buttons in the top bar of the full-screen menu (OnStrip). The game centres
    /// that row in a box beside the app's name ("AppButtons", 1400 wide on the game's canvas of 3840, a
    /// HorizontalLayoutGroup of spacing 20, upper centre) and its own seven apps run a little past both ends of the
    /// box. With the mod's apps the centred row grew both ways, under the money, the date and the time ("RightData")
    /// and off the screen.
    ///
    /// A page is a set number of tiles, the same every time (PagerRules): eight on the HUD phone, and on the full menu's
    /// row as many as the game's own row has (seven), counted once, because the game's own apps stand there at every
    /// money, date, language, resolution and interface scale: the narrowest room the row can ever have. (Before, the
    /// room was measured each time the menu opened, from the game's apps to the left edge of the money, so the number
    /// of apps on a page, the number of pages and the row's length changed with the money and the date.) When the apps
    /// fit on one page there are no pages. Otherwise the last tile of every page is the "More apps" button: a copy of
    /// the game's app button (its rounded tile, a neutral colour, the game's double chevron in white), its name where the
    /// apps show theirs and the page under it ("2 / 3"); it goes to the next page, and from the last to the first. The
    /// pages fill in order, only the last may be shorter, and the row stands where a full page stands, so the button
    /// never moves. The wheel over the row or the button turns the page too. The page of the app that is open is the one
    /// shown when the menu opens. The rows' own alignment and padding are put back when only the game's own apps are left.
    /// On the HUD phone the button carries the game's red badge with the number of buttons on other pages whose own badge
    /// is up (the game's apps, the mod's and other mods'), counted again when the game says a badge changed.
    ///
    /// One pager per grid, shared by every app on it, for as long as the game's interface stands: an app that goes
    /// takes its buttons out of the grid and leaves the pages to page what is left (PhoneApp.Remove). What a pager
    /// does to the grid is written on the grid itself, so any pager that comes after carries on from it: a button
    /// switched off for being on another page carries a PageMark, the row keeps how the game built it in a
    /// RowAsBuilt, and the button is the one object of that name under the grid's parent (not in the grid: its layout
    /// and other mods' pagers leave it alone). (Before, every app's removal destroyed the shared pager; one destroyed
    /// before the menu was first opened never put back what it had switched off, and the next counted only what it could
    /// see: the mod's icons went missing for the session and a second set of page marks was left on the bar.)
    ///
    /// Other mods page this grid too, each by switching the buttons on and off. Two doing that would undo each other,
    /// so this one steps aside for good as soon as it sees anyone else at it: a component on the grid that is neither
    /// the game's nor this mod's, or a button it had switched off that someone has shown again. Everything it hid is
    /// shown then, its button is taken away, and the other mod's pages are the pages.
    ///
    /// The row's own layout places the page that shows, and the pager checks that it did: two buttons that show
    /// standing where one does (the layout switched off by another hand, or a button switched on that the layout was
    /// never told of) are placed by the pager itself, the same way the layout would, and one line in Player.log says
    /// what was on the row. (Before, the mod's apps put on after the menu was built, the bank apps among them, could
    /// stand one on top of another at the place of the button they were copied from.) With Developer diagnostics on,
    /// each opening of the menu writes one line: where the row ends against where the money begins.
    ///
    /// Nothing here runs per frame: it acts when the grid is shown, when its children change, when one of them is
    /// shown or hidden by any hand (every button carries a PageMark that says so: one pass before the frame is drawn,
    /// however many changed), when the row's box changes size, on the button and on the wheel. The button is made
    /// once, when a second page is first needed.
    /// </summary>
    public sealed class PhoneGridPager : MonoBehaviour, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneGridPager", typeof(PhoneGridPager));   // the game's calls of it (Core/Guard)

        private const string MoreName = "RealLife MoreApps";
        private const string MoreTitleKey = "reallife_apps_more";       // "More apps", where the apps show their names
        private const string MorePageKey = "reallife_apps_page";        // "{page} / {pages}", under the name
        private const string MoreGlyph = "Icon-Arrows";                  // the game's double chevron, white, 64 by 64 (sharedassets1)
        private const string MoreIconName = "AppIcon-MoreApps";
        private const float GlyphShare = 0.42f;                          // of the tile: the glyph's size on every app icon of the mod's (AppIconPainter)
        private const float LineStep = 1.25f;                            // font sizes: from the name's line to the page's, on the HUD phone's button
        private static readonly Color32 MoreTop = new Color32(240, 242, 245, 255);     // upper left of the tile: a light grey, no app's colour (every app's tile is a strong colour)
        private static readonly Color32 MoreBottom = new Color32(196, 201, 209, 255);  // lower right of the tile
        private static readonly Color32 MoreInk = new Color32(54, 62, 76, 255);        // the glyph: dark on the light tile

        public static IModLogger Logger;

        private static FieldInfo _pictureField;
        private static bool _pictureLooked;
        private static Sprite _drawnGlyph;

        private Transform _template;
        private readonly List<Transform> _apps = new List<Transform>(24);
        private readonly List<MonoBehaviour> _scripts = new List<MonoBehaviour>(4);
        private int _page;
        private bool _aside;        // someone else pages this grid
        private bool _working;      // our own changes must not start another pass
        private bool _ready;        // the template is known
        private float _lastTurn;
        private bool _strip;            // the full menu's row of app buttons, not the HUD phone's grid
        private RectTransform _rightData;
        private int _tiles;             // the strip's tiles on a page: the game's own row, counted once (0: not yet)
        private int _gameApps = -1;     // the strip's buttons of the game's own apps, counted once (-1: not yet)
        private float _pagesLeft;       // the strip's place for a page, in the row's space: where the game's own row begins
        private bool _own;              // the strip holds only the game's own apps: laid out as the game lays it out
        private bool _pending;          // a pass is asked for before the frame is drawn
        private bool _checkDue;         // the menu has opened: one line on where the row ends (Developer diagnostics)
        private RectTransform _more;    // the "More apps" button, beside the grid under its parent
        private TextLocalizationComponent _morePage;
        private global::UI.Elements.Badge _moreBadge;
        private int _badgeShown = -1;   // the number last put on the button's badge
        private UnityEngine.Events.UnityAction<AppName> _onBadge;   // the game's word that an app's badge changed, through the guard: made once, kept for taking off
        private int _labelPage = -1, _labelPages = -1;
        private Canvas.WillRenderCanvases _late;
        private static bool _toldByHand;
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Dictionary<Type, bool> KnownScripts = new Dictionary<Type, bool>(8);   // a script on the grid: the game's, Unity's or the mod's (true), another mod's (false)

        public int Pages { get; private set; } = 1;
        public int Count => _apps.Count;
        public bool SteppedAside => _aside;

        // Tiles on a page: the HUD phone's eight, the full menu's as many as the game's own row.
        private int Tiles => _strip ? (_tiles > 0 ? _tiles : PagerRules.GameRowApps) : PagerRules.PhoneTiles;

        /// <summary>Pages for the HUD phone's grid. (<paramref name="dot"/>: the pages had dots before they had a button; not used.)</summary>
        public static PhoneGridPager On(Transform grid, Transform template, Sprite dot)
        {
            if (grid == null)
            {
                return null;
            }
            PhoneGridPager pager = grid.GetComponent<PhoneGridPager>();
            if (pager == null)
            {
                pager = grid.gameObject.AddComponent<PhoneGridPager>();   // its OnEnable runs here, before it knows the template: it waits
            }
            pager._template = template;
            pager._ready = true;
            pager.HearBadges();
            pager.Refresh();
            return pager;
        }

        // The HUD phone's button counts every badge on the pages not showing, the game's own (Contacts, MyEmployees) too:
        // the game says when one changes. Once for the pager; taken off when it goes.
        private void HearBadges()
        {
            if (_onBadge != null)
            {
                return;
            }
            _onBadge = Guard.Listener<AppName>(BadgeChanged);   // (a fault here never reaches the game's own listeners: Core/Guard)
            try
            {
                ListenBadges(true);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the game's own badges on the More apps button");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ListenBadges(bool on)
        {
            if (on)
            {
                SmartphoneUI.OnUpdatedBadgeCount.AddListener(_onBadge);
            }
            else
            {
                SmartphoneUI.OnUpdatedBadgeCount.RemoveListener(_onBadge);
            }
        }

        private void BadgeChanged(AppName app)
        {
            if (this != null)
            {
                Paint();
            }
        }

        /// <summary>
        /// Pages for the full menu's row of app buttons. <paramref name="rightData"/> (the money, date and time) is only
        /// looked at for the line in Player.log with Developer diagnostics on; <paramref name="dot"/> is not used.
        /// </summary>
        public static PhoneGridPager OnStrip(Transform strip, Transform template, Sprite dot, RectTransform rightData)
        {
            if (strip == null || !(strip is RectTransform) || strip.GetComponent<HorizontalLayoutGroup>() == null)
            {
                return null;   // not the row the game builds: left as it is
            }
            PhoneGridPager pager = strip.GetComponent<PhoneGridPager>();
            if (pager == null)
            {
                pager = strip.gameObject.AddComponent<PhoneGridPager>();
            }
            pager._strip = true;
            pager._rightData = rightData;
            pager._template = template;
            pager._ready = true;
            pager.Refresh();
            return pager;
        }

        private void Awake()
        {
            _late = new Canvas.WillRenderCanvases(Guard.Way(LatePass));   // (one delegate for good, through the guard: asking for a pass allocates nothing)
        }

        private void OnEnable()
        {
            _checkDue = _strip;
            Refresh();
            if (_strip && !_aside)
            {
                ShowSelected();
            }
        }

        // The menu has opened: on the page of the app it opens on (the button whose dot is on).
        private void ShowSelected()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                Transform selected = _apps[i].Find("SelectedIcon");
                if (selected != null && selected.gameObject.activeSelf)
                {
                    Turn(PageOf(i));
                    return;
                }
            }
        }

        private void OnDisable()
        {
            if (_pending)
            {
                _pending = false;
                Canvas.willRenderCanvases -= _late;
            }
            if (_more != null && _more.gameObject.activeSelf)
            {
                _more.gameObject.SetActive(false);   // (it stands beside the grid, not in it: it goes with it)
            }
        }

        private void OnTransformChildrenChanged()
        {
            Refresh();
        }

        // The grid's box changed size (the screen, the interface's scale): where the page and its button stand may have changed.
        private void OnRectTransformDimensionsChange()
        {
            Changed();
        }

        /// <summary>
        /// A button of the grid has been shown or hidden by any hand (its PageMark says so). That is no change of the
        /// grid's children, so nothing else tells this pager, and the layout may not hear of it either: one pass
        /// before the frame is drawn, however many buttons changed in it (the whole row comes on with the menu).
        /// </summary>
        internal void Changed()
        {
            if (_working || _aside || _pending || !_ready || !isActiveAndEnabled)
            {
                return;   // (our own changes, or the grid is not showing: it is looked at again when it is)
            }
            _pending = true;
            Canvas.willRenderCanvases += _late;
        }

        private void LatePass()
        {
            Canvas.willRenderCanvases -= _late;
            _pending = false;
            if (this != null && isActiveAndEnabled)
            {
                Refresh();
            }
        }

        /// <summary>Where a button is in the grid, counted from 1; 0 if it is not one of the apps.</summary>
        public int PlaceOf(Transform button)
        {
            return _apps.IndexOf(button) + 1;
        }

        /// <summary>Turns to the page a button is on.</summary>
        public void ShowPageOf(Transform button)
        {
            int index = _apps.IndexOf(button);
            if (index >= 0 && !_aside)
            {
                Turn(PageOf(index));
            }
        }

        /// <summary>
        /// A button's badge has changed (an app of the mod's): the "More apps" button's badge counts again the buttons on
        /// pages not showing whose badge is up. The badges themselves say what is new, whoever set them.
        /// </summary>
        public void SetAttention(Transform button, bool on)
        {
            Paint();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Wheel(eventData);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // The wheel over the row or its button: one page on or back, stopping at either end.
        internal void Wheel(PointerEventData eventData)
        {
            if (_aside || Pages < 2 || eventData == null || Time.unscaledTime - _lastTurn < 0.15f || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }
            _lastTurn = Time.unscaledTime;
            Turn(PagerRules.Wheel(_page, Pages, eventData.scrollDelta.y < 0f));
        }

        // The "More apps" button: the next page, and from the last back to the first.
        private void NextPage()
        {
            if (!_aside)
            {
                Turn(PagerRules.Next(_page, Pages));
            }
        }

        private void Turn(int page)
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            if (page != _page)
            {
                _page = page;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_working || !_ready || this == null)
            {
                return;
            }
            _working = true;
            try
            {
                if (!_aside && SomeoneElse())
                {
                    StepAside();
                }
                Collect();
                if (!_aside)
                {
                    if (_strip)
                    {
                        StripPages();
                    }
                    else
                    {
                        Pages = PagerRules.Pages(_apps.Count, Tiles);
                    }
                    _page = Mathf.Clamp(_page, 0, Pages - 1);
                    Apply();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                StepAside();
            }
            finally
            {
                _working = false;
            }
        }

        // ---------------------------------------------------------------- which button is on which page

        // The apps are every child of the grid its layout places (all but the template and what the layout leaves out),
        // whoever made it: the game's and the mod's buttons, and other mods' (Going Public's Business, Chat and Hub
        // buttons are copies with the game's script taken off). Those that are showing, or that a pager of this grid has
        // switched off for being on another page (this one or one before it: the PageMark says so). A child somebody else
        // keeps switched off is not counted: it is theirs to show, and its PageMark tells this pager when it is. (Before,
        // only buttons with the game's script were counted, and the layout put another mod's button on the More apps
        // button's tile, under it.)
        private void Collect()
        {
            _apps.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == _template)
                {
                    continue;
                }
                var mark = child.GetComponent<PageMark>();
                if (mark == null && !_aside)
                {
                    mark = child.gameObject.AddComponent<PageMark>();   // (on any button, ours or not: its being shown or hidden reaches the pager)
                }
                if (mark != null)
                {
                    mark.Pager = this;
                }
                if (!(child is RectTransform placed) || Ignored(placed))
                {
                    continue;   // (placed by whoever put it there, not by the layout)
                }
                if (child.gameObject.activeSelf || mark != null && mark.Off)
                {
                    _apps.Add(child);
                }
            }
        }

        // The page a button is on: the pages fill in turn, every one but the last full (PagerRules).
        private int PageOf(int index)
        {
            return PagerRules.PageOf(index, _apps.Count, Tiles);
        }

        private static bool PagedOff(Transform button)
        {
            var mark = button.GetComponent<PageMark>();
            return mark != null && mark.Off;
        }

        private void Apply()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                Transform app = _apps[i];
                bool show = PageOf(i) == _page;
                var mark = app.GetComponent<PageMark>();
                if (!show && mark == null)
                {
                    mark = app.gameObject.AddComponent<PageMark>();
                }
                if (mark != null)
                {
                    mark.Off = !show;
                }
                if (app.gameObject.activeSelf != show)
                {
                    app.gameObject.SetActive(show);
                }
            }
            if (_strip)
            {
                PlaceStripPage();
            }
            else
            {
                PlaceGridPage();
            }
            More();
            if (_checkDue && _strip && Meta.Dev.Verbose && isActiveAndEnabled)
            {
                _checkDue = false;
                StripCheck();
            }
        }

        // ---------------------------------------------------------------- the full menu's row

        // How many pages the row needs and where they stand: where the game's own apps begin as the game lays them out,
        // in the row's space, so the same at any screen size; a page as wide as the game's own row (PagerRules).
        private void StripPages()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            int count = _apps.Count;
            Pages = 1;
            _own = false;
            if (layout == null || cell == null || count < 1)
            {
                return;
            }
            float pitch = Pitch(layout, cell);
            if (pitch < 1f)
            {
                return;
            }
            RowAsBuilt built = AsBuilt(layout);
            if (_gameApps < 0)
            {
                int counted = 0;
                for (int i = 0; i < count; i++)
                {
                    counted += Enum.TryParse(_apps[i].name, out AppName _) ? 1 : 0;   // the game names its buttons after its apps
                }
                if (counted >= PagerRules.LeastTiles)
                {
                    _gameApps = counted;   // once: the game's own apps are made with the menu and never come or go
                    _tiles = PagerRules.StripTiles(counted);   // the game's own row is the room a page has, at any money, date or language
                }
            }
            int own = _gameApps;
            if (own == count)
            {
                _own = true;   // nothing of the mod's (or anyone's) on it: the game's row as the game laid it out
                return;
            }
            float inner = strip.rect.width - built.Left - layout.padding.right;
            _pagesLeft = strip.rect.xMin + built.Left + (inner - PagerRules.RowWidth(Tiles, pitch, layout.spacing)) * ((int)built.Alignment % 3) * 0.5f;
            Pages = PagerRules.Pages(count, Tiles);
        }

        // The page that shows, from where a full page begins: a shorter last page keeps the same left edge.
        private void PlaceStripPage()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            if (layout == null || cell == null)
            {
                return;
            }
            if (_own || _apps.Count == 0)
            {
                RestoreRow();
                return;
            }
            RowAsBuilt built = AsBuilt(layout);
            float pitch = Pitch(layout, cell);
            float begin = _pagesLeft;
            var alignment = (TextAnchor)((int)built.Alignment / 3 * 3);   // the same row, from the left
            int padding = Mathf.RoundToInt(begin - strip.rect.xMin);
            if (layout.childAlignment != alignment || layout.padding.left != padding)
            {
                layout.childAlignment = alignment;
                layout.padding.left = padding;
            }
            if (strip.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(strip);   // in place before anything is drawn
                if (!layout.isActiveAndEnabled || Stacked(strip, pitch))
                {
                    ByHand(strip, layout, begin, pitch - layout.spacing);
                }
            }
            else
            {
                LayoutRebuilder.MarkLayoutForRebuild(strip);   // (and the pass when the menu opens looks again)
            }
        }

        // Whether two buttons that show stand where one does: the layout has not placed them.
        private static bool Stacked(RectTransform strip, float pitch)
        {
            bool first = true;
            float last = 0f;
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || Ignored(child))
                {
                    continue;
                }
                float left = child.localPosition.x + child.rect.xMin * child.localScale.x;
                if (!first && Mathf.Abs(left - last) < pitch * 0.25f)
                {
                    return true;
                }
                first = false;
                last = left;
            }
            return false;
        }

        // The buttons that show, placed as the row's layout places them: from the page's left edge one after another,
        // at the height its alignment gives. Only when the layout has not: while it is on, it does the same.
        private void ByHand(RectTransform strip, HorizontalLayoutGroup layout, float begin, float cellWidth)
        {
            if (!_toldByHand)
            {
                _toldByHand = true;
                UiClone.Fallback("the full menu's row of app buttons was not laid out by its own layout (" + (layout.enabled ? "switched on" : "switched off")
                                 + "), so the pages place its buttons themselves; it showed " + Shown(strip));
            }
            Rect box = strip.rect;
            float x = begin - box.xMin;   // from the row's left edge, as the layout counts
            float row = (int)layout.childAlignment / 3 * 0.5f;   // upper, middle or lower
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || Ignored(child))
                {
                    continue;
                }
                float width = layout.childControlWidth ? Mathf.Max(LayoutUtility.GetPreferredWidth(child), child.rect.width) : child.rect.width;
                if (width < 1f)
                {
                    width = cellWidth;
                }
                float height = child.rect.height;
                float top = layout.padding.top + (box.height - layout.padding.vertical - height) * row;
                child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
                child.anchoredPosition = new Vector2(x + width * child.pivot.x, -(top + height * (1f - child.pivot.y)));
                x += width + layout.spacing;
            }
        }

        // What the row showed, for the line in Player.log: each button's name and left edge.
        private static string Shown(RectTransform strip)
        {
            var said = new System.Text.StringBuilder(256);
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }
                said.Append(said.Length == 0 ? string.Empty : ", ").Append(child.name).Append(" at ")
                    .Append((child.localPosition.x + child.rect.xMin * child.localScale.x).ToString("0"));
            }
            return said.Length == 0 ? "nothing" : said.ToString();
        }

        // A child the layout leaves out (LayoutElement.ignoreLayout): placed by whoever put it there.
        private static bool Ignored(RectTransform child)
        {
            var ignorer = child.GetComponent<ILayoutIgnorer>();
            return ignorer != null && ignorer.ignoreLayout;
        }

        // How the game built the row or the grid, kept on it the first time it is paged, so that any pager after this one knows it.
        private RowAsBuilt AsBuilt(LayoutGroup layout)
        {
            var built = GetComponent<RowAsBuilt>();
            if (built == null)
            {
                built = gameObject.AddComponent<RowAsBuilt>();
                built.Alignment = layout.childAlignment;
                built.Left = layout.padding.left;
            }
            return built;
        }

        // From one button's left edge to the next one's.
        private static float Pitch(HorizontalLayoutGroup layout, RectTransform cell)
        {
            float width = layout.childControlWidth ? Mathf.Max(LayoutUtility.GetPreferredWidth(cell), cell.rect.width) : cell.rect.width;
            return width + layout.spacing;
        }

        // The row or the grid as the game laid it out.
        private void RestoreRow()
        {
            var layout = GetComponent<LayoutGroup>();
            var built = GetComponent<RowAsBuilt>();
            if (layout == null || built == null || layout.childAlignment == built.Alignment && layout.padding.left == built.Left)
            {
                return;
            }
            layout.childAlignment = built.Alignment;
            layout.padding.left = built.Left;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        // With Developer diagnostics on, once each time the menu opens: where a full page and its button end against
        // where the money, the date and the time begin, in the row's space.
        private void StripCheck()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            if (_own || Pages < 2 || layout == null || cell == null)
            {
                return;
            }
            float end = _pagesLeft + PagerRules.RowWidth(Tiles, Pitch(layout, cell), layout.spacing);
            float money = RightLimit(strip);
            Logger?.Info("Game UI: full menu row check (" + _apps.Count + " apps, " + Pages + " pages of " + (Tiles - 1) + " and the More apps button, as wide as the game's own "
                         + Tiles + "): the row runs from " + _pagesLeft.ToString("0") + " to " + end.ToString("0") + ", the money and the date begin at "
                         + (money == float.MaxValue ? "nothing found" : money.ToString("0")) + ": "
                         + (end <= money ? "PASS" : "the money begins under the row's end, where the game's own apps end too") + ".");
        }

        // The left edge of what is drawn of the money, the date and the time, in the row's space; failing them, the top
        // bar's right edge. Each part's own box, and the box of every picture and text in it (a text can be laid out
        // wider than its part).
        private float RightLimit(RectTransform strip)
        {
            float right = float.MaxValue;
            if (_rightData != null && _rightData.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rightData);   // their widths as they read now
                for (int i = 0; i < _rightData.childCount; i++)
                {
                    var part = _rightData.GetChild(i) as RectTransform;
                    if (part == null || !part.gameObject.activeSelf)
                    {
                        continue;
                    }
                    right = Mathf.Min(right, LeftEdge(strip, part));
                    Graphic[] drawn = part.GetComponentsInChildren<Graphic>(false);
                    for (int g = 0; g < drawn.Length; g++)
                    {
                        if (drawn[g].enabled)
                        {
                            right = Mathf.Min(right, LeftEdge(strip, drawn[g].rectTransform));
                        }
                    }
                }
            }
            if (right == float.MaxValue && strip.parent is RectTransform bar)
            {
                bar.GetWorldCorners(Corners);
                right = strip.InverseTransformPoint(Corners[2]).x;
            }
            return right;
        }

        private static float LeftEdge(RectTransform strip, RectTransform part)
        {
            if (part.rect.width < 1f)
            {
                return float.MaxValue;
            }
            part.GetWorldCorners(Corners);
            return strip.InverseTransformPoint(Corners[0]).x;
        }

        // ---------------------------------------------------------------- the HUD phone's grid

        // A page that is not full keeps the full page's left edge: the game's grid centres the columns it uses, so one
        // app alone would stand in the middle. From the left, at the place the full page's first column begins.
        private void PlaceGridPage()
        {
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                return;
            }
            if (Pages < 2)
            {
                RestoreRow();
                return;
            }
            if (grid.startCorner != GridLayoutGroup.Corner.UpperLeft || grid.startAxis != GridLayoutGroup.Axis.Horizontal)
            {
                return;   // not the grid the game builds: its own layout
            }
            RowAsBuilt built = AsBuilt(grid);
            var alignment = (TextAnchor)((int)built.Alignment / 3 * 3);   // the same rows, from the left
            int padding = Mathf.RoundToInt(GridLeft(grid, built));
            if (grid.childAlignment != alignment || grid.padding.left != padding)
            {
                grid.childAlignment = alignment;
                grid.padding.left = padding;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }

        // Where a full page's first column begins, from the grid's left edge, as the game's layout places it.
        private static float GridLeft(GridLayoutGroup grid, RowAsBuilt built)
        {
            float width = ((RectTransform)grid.transform).rect.width;
            int columns = Columns(grid, built);
            float required = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
            return built.Left + (width - built.Left - grid.padding.right - required) * ((int)built.Alignment % 3) * 0.5f;
        }

        private static int Columns(GridLayoutGroup grid, RowAsBuilt built)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                return Mathf.Max(1, grid.constraintCount);
            }
            float width = ((RectTransform)grid.transform).rect.width - built.Left - grid.padding.right;
            return Mathf.Max(1, PagerRules.Fit(width, grid.cellSize.x + grid.spacing.x, grid.spacing.x));
        }

        // ---------------------------------------------------------------- the "More apps" button

        // The button on the last tile of the page, shown while there are pages.
        private void More()
        {
            if (!PagerRules.ButtonShown(Pages) || _aside || !isActiveAndEnabled)
            {
                // (with the grid not showing it is placed again, or made, when the grid is: OnEnable)
                if (_more != null && _more.gameObject.activeSelf)
                {
                    _more.gameObject.SetActive(false);
                }
                return;
            }
            if (_more == null && !MakeMore())
            {
                return;
            }
            if (!Tile(PagerRules.ButtonTile(Tiles), out Vector3 centre, out Vector2 size))
            {
                _more.gameObject.SetActive(false);
                return;
            }
            _more.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _more.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            _more.localScale = transform.localScale;
            _more.position = transform.TransformPoint(centre);
            int grid = transform.GetSiblingIndex(), now = _more.GetSiblingIndex();
            if (now != grid + 1)
            {
                _more.SetSiblingIndex(now < grid ? grid : grid + 1);   // drawn just after the grid, as one of its tiles
            }
            if (!_more.gameObject.activeSelf)
            {
                _more.gameObject.SetActive(true);
            }
            Label();
            Paint();
        }

        // The middle of a page's tile (from 0) in the grid's space, and the tile's size.
        private bool Tile(int tile, out Vector3 centre, out Vector2 size)
        {
            centre = Vector3.zero;
            size = Vector2.zero;
            var box = ((RectTransform)transform).rect;
            if (_strip)
            {
                var layout = GetComponent<HorizontalLayoutGroup>();
                var cell = _template as RectTransform;
                if (layout == null || cell == null)
                {
                    return false;
                }
                float pitch = Pitch(layout, cell);
                float height = cell.rect.height;
                float top = layout.padding.top + (box.height - layout.padding.vertical - height) * ((int)layout.childAlignment / 3 * 0.5f);
                size = new Vector2(pitch - layout.spacing, height);
                centre = new Vector3(_pagesLeft + tile * pitch + size.x * 0.5f, box.yMax - top - height * 0.5f, 0f);
                return size.x >= 1f;
            }
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                return false;
            }
            RowAsBuilt built = AsBuilt(grid);
            int columns = Columns(grid, built);
            int rows = (Tiles + columns - 1) / columns;
            float column = tile % columns, line = tile / columns;
            float needed = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
            float from = grid.padding.top + (box.height - grid.padding.vertical - needed) * ((int)built.Alignment / 3 * 0.5f);
            size = grid.cellSize;
            centre = new Vector3(box.xMin + GridLeft(grid, built) + column * (size.x + grid.spacing.x) + size.x * 0.5f,
                                 box.yMax - from - line * (size.y + grid.spacing.y) - size.y * 0.5f, 0f);
            return size.x >= 1f;
        }

        // The button, made once from the template the apps are made from, asleep until it is dressed: the game's own
        // tile, title and hover; the game's scripts that would make it an app taken off, so that nobody counts it as one.
        // One object of the name under the grid's parent: one a pager left before this one is taken away.
        private bool MakeMore()
        {
            Transform parent = transform.parent;
            if (parent == null || _template == null)
            {
                return false;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform old = parent.GetChild(i);
                if (old.name == MoreName)
                {
                    old.gameObject.SetActive(false);
                    old.name = MoreName + " (gone)";
                    Destroy(old.gameObject);
                }
            }
            Transform dormant = UiClone.Dormant(parent, MoreName + " (making)");
            GameObject copy = UiClone.Asleep(_template.gameObject, dormant, MoreName);
            if (copy == null)
            {
                Destroy(dormant.gameObject);
                return false;
            }
            var rect = (RectTransform)copy.transform;
            var row = (RectTransform)transform;
            rect.anchorMin = row.anchorMin;   // anchored as the row or grid is: it moves with them when the bar or the phone changes size
            rect.anchorMax = row.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            if (Tile(0, out Vector3 _, out Vector2 size))
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);   // (the tile's size before the page's line is placed under its name or picture)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
            Image picture = Picture(copy.transform);   // (through the game's own field, before its script is taken off)
            var title = copy.GetComponentInChildren<TextLocalizationComponent>(true);
            UiClone.StripGameScripts(copy, KeptOnMore);
            Transform selected = copy.transform.Find("SelectedIcon");
            if (selected != null)
            {
                DestroyImmediate(selected.gameObject);
            }
            if (UiClone.Rewire(copy.transform, NextPage) == null)
            {
                UiClone.Fallback("the phone's app button has no Button, so the pages have no More apps button (the wheel still turns them)");
                Destroy(dormant.gameObject);
                return false;
            }
            if (picture != null)
            {
                Sprite icon = MoreIcon(picture.sprite);
                if (icon != null)
                {
                    picture.sprite = icon;
                }
            }
            if (title != null)
            {
                UiClone.Key(title, MoreTitleKey);
                _morePage = PageLine(rect, title, picture);
            }
            _moreBadge = copy.GetComponentInChildren<global::UI.Elements.Badge>(true);
            copy.AddComponent<MoreAppsWheel>().Pager = this;
            copy.SetActive(false);
            rect.SetParent(parent, false);
            Destroy(dormant.gameObject);
            _more = rect;
            _labelPage = _labelPages = _badgeShown = -1;
            return true;
        }

        // What stays of the game's scripts on the button: its hover and sound, and the HUD phone's badge.
        private static bool KeptOnMore(MonoBehaviour script)
        {
            return script is ButtonEffects || script is global::UI.Elements.Badge;
        }

        // The page, in a copy of the name's text: under the picture where the name stands over it (the full menu's
        // button), one line under the name where it stands under the picture (the HUD phone's).
        private static TextLocalizationComponent PageLine(RectTransform button, TextLocalizationComponent title, Image picture)
        {
            var titleRect = (RectTransform)title.transform;
            GameObject copy = Instantiate(title.gameObject, titleRect.parent, false);
            copy.name = "Page";
            var rect = (RectTransform)copy.transform;
            var words = copy.GetComponent<TMP_Text>();
            float line = words != null && words.fontSize > 1f ? words.fontSize * LineStep : 34f;
            RectTransform pictureRect = picture != null ? picture.rectTransform : null;
            if (pictureRect != null && pictureRect.parent == titleRect.parent && titleRect.localPosition.y > pictureRect.localPosition.y)
            {
                float bottom = pictureRect.localPosition.y + pictureRect.rect.yMin;
                float anchor = Mathf.Lerp(button.rect.yMin, button.rect.yMax, rect.anchorMax.y);
                rect.pivot = new Vector2(rect.pivot.x, 1f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, line);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom - anchor);
            }
            else
            {
                rect.anchoredPosition = titleRect.anchoredPosition - new Vector2(0f, line);
            }
            if (words != null)
            {
                words.verticalAlignment = VerticalAlignmentOptions.Top;
            }
            var page = copy.GetComponent<TextLocalizationComponent>();
            UiClone.Key(page, MorePageKey);
            return page;
        }

        // The page under the name, "2 / 3": written again only when it changes. A new set of names each time, because
        // the game's text writes itself again only when it is given another object.
        private void Label()
        {
            if (_morePage == null)
            {
                return;
            }
            PagerRules.Shown(_page, Pages, out int shown, out int of);
            if (shown == _labelPage && of == _labelPages)
            {
                return;
            }
            _labelPage = shown;
            _labelPages = of;
            _morePage.Arguments = new Dictionary<string, string>(2)
            {
                { "page", shown.ToString(CultureInfo.InvariantCulture) },
                { "pages", of.ToString(CultureInfo.InvariantCulture) },
            };
        }

        // The game's red badge on the button (the HUD phone's): how many buttons on pages not showing have their own
        // badge up, the game's apps', the mod's and other mods' alike. Written only when the number changes.
        private void Paint()
        {
            if (_moreBadge == null || _aside)
            {
                return;
            }
            int waiting = 0;
            if (Pages > 1)
            {
                for (int i = 0; i < _apps.Count; i++)
                {
                    if (_apps[i] != null && PageOf(i) != _page && BadgeUp(_apps[i]))
                    {
                        waiting++;
                    }
                }
            }
            if (waiting == _badgeShown)
            {
                return;
            }
            _badgeShown = waiting;
            try
            {
                ShowBadge(_moreBadge, waiting);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the badge on the More apps button");
                _moreBadge = null;   // (a game without the badge's call: the button goes without it)
            }
        }

        // Whether a button's own badge shows (the game's Badge switches its object on for a number over 0).
        private static bool BadgeUp(Transform button)
        {
            var badge = button.GetComponentInChildren<global::UI.Elements.Badge>(true);
            return badge != null && badge.gameObject.activeSelf;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShowBadge(global::UI.Elements.Badge badge, int count)
        {
            badge.UpdateBadge(count);
        }

        // The button's picture: an app icon of the game's manner on a light grey tile, with the game's double chevron in dark ink, or
        // one drawn the same way if the game's is not in memory.
        private static Sprite MoreIcon(Sprite like)
        {
            Sprite glyph = GameSprites.Get(MoreGlyph) ?? DrawnGlyph();
            return AppIconPainter.Paint(like, glyph, MoreTop, MoreBottom, GlyphShare, MoreIconName, MoreInk);
        }

        // Two white chevrons pointing on, 64 by 64 as the game's glyph: made once a session, kept.
        private static Sprite DrawnGlyph()
        {
            if (_drawnGlyph != null)
            {
                return _drawnGlyph;
            }
            const int size = 64;
            const float stroke = 9f;   // pixels: the width of each chevron's line, as heavy as the game's glyph
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Min(Chevron(x + 0.5f, y + 0.5f, 30f), Chevron(x + 0.5f, y + 0.5f, 48f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stroke * 0.5f - distance + 0.5f) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;   // kept for the session
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "RealLife-MoreAppsGlyph";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _drawnGlyph = sprite;
            return sprite;
        }

        // How far a point is from a chevron pointing right with its tip at (tip, 32): two lines from 14 to the left of it.
        private static float Chevron(float x, float y, float tip)
        {
            return Mathf.Min(Segment(x, y, tip - 14f, 14f, tip, 32f), Segment(x, y, tip - 14f, 50f, tip, 32f));
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px = ax + t * dx - x, py = ay + t * dy - y;
            return Mathf.Sqrt(px * px + py * py);
        }

        // An app button's picture: the game's own field for it (AppButton's "icon": the "Logo" of the full menu's
        // button, the "Icon" of the HUD phone's), else a child of either name.
        private static Image Picture(Transform button)
        {
            if (!_pictureLooked)
            {
                _pictureLooked = true;
                _pictureField = GameContract.AppButtonIcon.Field;   // (resolved once: Core/GameContract)
            }
            var script = button.GetComponent<AppButton>();
            if (_pictureField != null && script != null && _pictureField.GetValue(script) is Image own && own != null)
            {
                return own;
            }
            Transform named = FindDeep(button, "Logo") ?? FindDeep(button, "Icon");
            return named != null ? named.GetComponent<Image>() : null;
        }

        private static Transform FindDeep(Transform under, string name)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
                Transform deeper = FindDeep(child, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- another mod at it

        private bool SomeoneElse()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.gameObject.activeSelf && PagedOff(child))
                {
                    return true;   // a button switched off for being on another page has been switched on by another hand
                }
            }
            _scripts.Clear();
            GetComponents(_scripts);
            for (int i = 0; i < _scripts.Count; i++)
            {
                MonoBehaviour script = _scripts[i];
                if (script == null || script == this)
                {
                    continue;
                }
                if (!Known(script.GetType()))
                {
                    return true;
                }
            }
            return false;
        }

        // Whether a script is the game's, Unity's or the mod's: worked out once for each kind of script (its assembly's name is made each time it is asked).
        private static bool Known(Type type)
        {
            if (KnownScripts.TryGetValue(type, out bool known))
            {
                return known;
            }
            string assembly = type.Assembly.GetName().Name;
            known = type.Assembly == typeof(SmartphoneUI).Assembly || assembly.StartsWith("Unity", StringComparison.Ordinal)
                    || assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) || assembly == "HGPlugins" || assembly == "HGExtensions"
                    || assembly == "ExternalPlugins" || type.Assembly == typeof(PhoneGridPager).Assembly;
            KnownScripts[type] = known;
            return known;
        }

        private void StepAside()
        {
            _aside = true;
            ShowAll();
            RestoreRow();
            if (_more != null)
            {
                Destroy(_more.gameObject);
                _more = null;
            }
            UiClone.Fallback(_strip ? "another mod pages the full menu's app buttons, so this one leaves the pages to it"
                                    : "another mod pages the phone's home screen, so this one leaves the pages to it");
        }

        // Every button switched off for being on another page, shown again.
        private void ShowAll()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                var mark = child.GetComponent<PageMark>();
                if (mark == null || !mark.Off)
                {
                    continue;
                }
                mark.Off = false;
                child.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (_onBadge != null)
            {
                try
                {
                    ListenBadges(false);   // (the game's event outlives the city: taken off on every path)
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "the game's own badges on the More apps button");
                }
                _onBadge = null;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                return;   // (the interface goes with the city)
            }
            ShowAll();
            RestoreRow();
            if (_more != null)
            {
                Destroy(_more.gameObject);
            }
        }
    }

    /// <summary>
    /// On the "More apps" button: the wheel over it turns the pages, as over the row itself (the button stands beside
    /// the grid, so the grid does not hear the wheel there). It has no Update.
    /// </summary>
    public sealed class MoreAppsWheel : MonoBehaviour, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MoreAppsWheel", typeof(MoreAppsWheel));   // the game's calls of it (Core/Guard)

        internal PhoneGridPager Pager;

        public void OnScroll(PointerEventData eventData)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (Pager != null)
                {
                    Pager.Wheel(eventData);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }

    /// <summary>
    /// On each button of a paged grid: switched off by a pager of that grid for being on another page (Off), so that
    /// any pager of that grid counts it as one of the apps; and the pager's word when the button is shown or hidden by
    /// any hand, the game's, another mod's or this one's, which is no change of the grid's children. It has no Update.
    /// </summary>
    public sealed class PageMark : MonoBehaviour
    {
        public bool Off;
        internal PhoneGridPager Pager;   // (not saved with the button: a copy of it waits for its own pager)

        private void OnEnable()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }

        private void OnDisable()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }
    }

    /// <summary>On the full menu's row of app buttons, and on the HUD phone's grid: its alignment and left padding as the game built them, kept from the first time it was paged.</summary>
    public sealed class RowAsBuilt : MonoBehaviour
    {
        public TextAnchor Alignment;
        public int Left;
    }
}
