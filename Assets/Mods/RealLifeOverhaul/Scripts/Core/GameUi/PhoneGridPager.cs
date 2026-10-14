using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Pages for the HUD phone's home screen. The game's grid is two buttons wide and exactly four rows fit above the
    /// phone's lower edge (cells of 250 by 225 in a box 868 high), so its own eight apps fill it: a ninth button has
    /// to go on a second page. The wheel over the grid turns the page, and so do the dots under it.
    ///
    /// The same pages serve the row of app buttons in the top bar of the full-screen menu (OnStrip). The game centres
    /// that row in a box beside the app's name ("AppButtons", 1400 wide on the game's canvas of 3840, a
    /// HorizontalLayoutGroup of spacing 20, upper centre) and its own seven apps run a little past both ends of the
    /// box. With the mod's apps the centred row grew both ways, under the money, the date and the time ("RightData")
    /// and off the screen. There, the row may take the room from where the game's own apps begin to the left edge of
    /// the money, measured each time the menu opens (the money and a language's date differ in width). The apps are
    /// shared out evenly over as few pages as fit (21 over three pages is seven, seven and seven, never six, six, six
    /// and three), each page centred where the widest stands, and the dots stand still after that place, level with
    /// the apps' pictures. The page of the app that is open is the one shown when the menu opens. The row's own
    /// alignment and padding are put back when only the game's own apps are left.
    ///
    /// One pager per grid, shared by every app on it, for as long as the game's interface stands: an app that goes
    /// takes its buttons out of the grid and leaves the pages to page what is left (PhoneApp.Remove). What a pager
    /// does to the grid is written on the grid itself, so any pager that comes after carries on from it: a button
    /// switched off for being on another page carries a PageMark, the row keeps how the game built it in a
    /// RowAsBuilt, and the dots are the one holder of that name under the grid's parent. (Before, every app's
    /// removal destroyed the shared pager; one destroyed before the menu was first opened never put back what it
    /// had switched off, and the next counted only what it could see: the mod's icons went missing for the session
    /// and a second set of dots was left on the bar.)
    ///
    /// Other mods page this grid too, each by switching the buttons on and off. Two doing that would undo each other,
    /// so this one steps aside for good as soon as it sees anyone else at it: a component on the grid that is neither
    /// the game's nor this mod's, or a button it had switched off that someone has shown again. Everything it hid is
    /// shown then, and the other mod's pages are the pages.
    ///
    /// The row's own layout places the page that shows, and the pager checks that it did: two buttons that show
    /// standing where one does (the layout switched off by another hand, or a button switched on that the layout was
    /// never told of) are placed by the pager itself, the same way the layout would, and one line in Player.log says
    /// what was on the row. (Before, the mod's apps put on after the menu was built, the bank apps among them, could
    /// stand one on top of another at the place of the button they were copied from.)
    ///
    /// Nothing here runs per frame: it acts when the grid is shown, when its children change, when one of them is
    /// shown or hidden by any hand (every button carries a PageMark that says so: one pass before the frame is drawn,
    /// however many changed), when the row's box changes size, and on the wheel.
    /// </summary>
    public sealed class PhoneGridPager : MonoBehaviour, IScrollHandler
    {
        public const int PerPage = 8;
        private const float DotSize = 14f, DotGap = 14f, DotsFromBottom = 22f;
        private const float StripRightGap = 60f, StripDotsGap = 34f;
        private const string DotsName = "RealLife PageDots";

        public static IModLogger Logger;

        private static FieldInfo _pictureField;
        private static bool _pictureLooked;

        private Transform _template;
        private Sprite _dotSprite;
        private RectTransform _dots;
        private readonly List<Transform> _apps = new List<Transform>(24);
        private readonly List<Transform> _attention = new List<Transform>(2);
        private readonly List<Image> _dotImages = new List<Image>(4);
        private readonly List<MonoBehaviour> _scripts = new List<MonoBehaviour>(4);
        private int _page;
        private bool _aside;        // someone else pages this grid
        private bool _working;      // our own changes must not start another pass
        private bool _ready;        // the template and the dot's picture are known
        private float _lastTurn;
        private bool _strip;            // the full menu's row of app buttons, not the HUD phone's grid
        private RectTransform _rightData;
        private float _pagesLeft;       // the strip's place for a page, in the row's space: where the widest page begins
        private float _pagesWidth;      // and how wide it is
        private bool _own;              // the strip holds only the game's own apps: laid out as the game lays it out
        private bool _pending;          // a pass is asked for before the frame is drawn
        private Canvas.WillRenderCanvases _late;
        private static bool _toldByHand;
        private static readonly Vector3[] Corners = new Vector3[4];

        public int Pages { get; private set; } = 1;
        public int Count => _apps.Count;
        public bool SteppedAside => _aside;

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
            pager._dotSprite = dot;
            pager._ready = true;
            pager.Refresh();
            return pager;
        }

        /// <summary>Pages for the full menu's row of app buttons, which must end before <paramref name="rightData"/> (the money, date and time; null: the top bar's right edge).</summary>
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
            pager._dotSprite = dot;
            pager._ready = true;
            pager.Refresh();
            return pager;
        }

        private void Awake()
        {
            _late = LatePass;   // (one delegate for good: asking for a pass allocates nothing)
        }

        private void OnEnable()
        {
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
        }

        private void OnTransformChildrenChanged()
        {
            Refresh();
        }

        // The row's box changed size (the screen, the interface's scale): how many fit on a page may have changed.
        private void OnRectTransformDimensionsChange()
        {
            if (_strip)
            {
                Changed();
            }
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

        /// <summary>Marks a button as having something new: the dot of a page that is not showing turns the badge's red.</summary>
        public void SetAttention(Transform button, bool on)
        {
            bool has = _attention.Contains(button);
            if (on == has)
            {
                return;
            }
            if (on)
            {
                _attention.Add(button);
            }
            else
            {
                _attention.Remove(button);
            }
            Paint();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_aside || Pages < 2 || Time.unscaledTime - _lastTurn < 0.15f || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }
            _lastTurn = Time.unscaledTime;
            Turn(_page + (eventData.scrollDelta.y < 0f ? 1 : -1));
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
                        Pages = Mathf.Max(1, (_apps.Count + PerPage - 1) / PerPage);
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

        // The apps are the buttons of the game's kind in the grid that are showing, or that a pager of this grid has
        // switched off for being on another page (this one or one before it: the PageMark says so). A button somebody
        // else keeps switched off is not counted: it is theirs to show, and its PageMark tells this pager when it is.
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
                if (child.GetComponent<AppButton>() == null)
                {
                    continue;
                }
                if (child.gameObject.activeSelf || mark != null && mark.Off)
                {
                    _apps.Add(child);
                }
            }
            for (int i = _attention.Count - 1; i >= 0; i--)
            {
                if (_attention[i] == null || !_apps.Contains(_attention[i]))
                {
                    _attention.RemoveAt(i);
                }
            }
        }

        // The page a button is on. The HUD phone's grid fills its pages in turn, eight to a page (a grid of two by four
        // reads like the game's own); the full menu's row shares its buttons out evenly, the first pages one more where
        // they do not divide.
        private int PageOf(int index)
        {
            if (!_strip || Pages < 2)
            {
                return _strip ? 0 : index / PerPage;
            }
            int count = _apps.Count;
            int small = count / Pages, extra = count % Pages, big = small + 1;
            return index < extra * big ? index / big : extra + (index - extra * big) / Mathf.Max(1, small);
        }

        private int PageSize(int page)
        {
            if (!_strip)
            {
                return Mathf.Clamp(_apps.Count - page * PerPage, 0, PerPage);
            }
            int count = _apps.Count;
            return count / Pages + (page < count % Pages ? 1 : 0);
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
            Dots();
        }

        // ---------------------------------------------------------------- the full menu's row

        // How many pages the row needs and where they stand: from where the game's own apps begin as the game lays them
        // out to the left edge of the money, in the row's space, so the same at any screen size. A block of the widest
        // page and the dots after it, centred in that room.
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
            int own = 0;
            for (int i = 0; i < count; i++)
            {
                own += Enum.TryParse(_apps[i].name, out AppName _) ? 1 : 0;   // the game names its buttons after its apps
            }
            if (own == count)
            {
                _own = true;   // nothing of the mod's (or anyone's) on it: the game's row as the game laid it out
                return;
            }
            float spacing = layout.spacing;
            float rectLeft = strip.rect.xMin;
            float inner = strip.rect.width - built.Left - layout.padding.right;
            float left = rectLeft + built.Left + (inner - (Mathf.Max(1, own) * pitch - spacing)) * ((int)built.Alignment % 3) * 0.5f;
            float right = RightLimit(strip) - StripRightGap;
            int pages = 1;
            if (count > Mathf.FloorToInt((right - left + spacing) / pitch))
            {
                // Room for the dots too; more pages make the dots wider, so until the count settles (it only grows).
                for (pages = 2; pages < count; pages++)
                {
                    int most = Mathf.Max(1, Mathf.FloorToInt((right - left - StripDotsGap - DotsWidth(pages) + spacing) / pitch));
                    if ((count + most - 1) / most <= pages)
                    {
                        break;
                    }
                }
            }
            Pages = pages;
            int widest = (count + pages - 1) / pages;
            _pagesWidth = widest * pitch - spacing;
            float block = _pagesWidth + (pages > 1 ? StripDotsGap + DotsWidth(pages) : 0f);
            _pagesLeft = left + Mathf.Max(0f, (right - left - block) * 0.5f);
        }

        // The page that shows, centred where the widest page stands.
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
                RestoreStrip();
                return;
            }
            RowAsBuilt built = AsBuilt(layout);
            float pitch = Pitch(layout, cell);
            float width = PageSize(_page) * pitch - layout.spacing;
            float begin = _pagesLeft + Mathf.Max(0f, (_pagesWidth - width) * 0.5f);
            var alignment = (TextAnchor)((int)built.Alignment / 3 * 3);   // the same row, from the left
            int padding = Mathf.RoundToInt(begin - strip.rect.xMin);
            if (layout.childAlignment != alignment || layout.padding.left != padding)
            {
                layout.childAlignment = alignment;
                layout.padding.left = padding;
            }
            if (strip.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(strip);   // in place before anything is drawn (and before the dots look)
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

        // How the game built the row, kept on the row the first time it is paged, so that any pager after this one knows it.
        private RowAsBuilt AsBuilt(HorizontalLayoutGroup layout)
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

        private static float DotsWidth(int pages)
        {
            return pages * DotSize + (pages - 1) * DotGap;
        }

        // The row as the game laid it out.
        private void RestoreStrip()
        {
            var layout = GetComponent<HorizontalLayoutGroup>();
            var built = GetComponent<RowAsBuilt>();
            if (layout == null || built == null || layout.childAlignment == built.Alignment && layout.padding.left == built.Left)
            {
                return;
            }
            layout.childAlignment = built.Alignment;
            layout.padding.left = built.Left;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
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

        // ---------------------------------------------------------------- the dots

        // The page dots: under the HUD phone's grid, on the phone's own panel (not in the grid: its layout would place
        // them); after the full menu's pages, level with the apps' pictures. One holder of the name under that parent:
        // one left by a pager before this one is taken away.
        private void Dots()
        {
            if (Pages < 2)
            {
                if (_dots != null)
                {
                    _dots.gameObject.SetActive(false);
                }
                return;
            }
            if (_dots == null)
            {
                Transform parent = transform.parent;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform old = parent.GetChild(i);
                    if (old.name == DotsName)
                    {
                        old.gameObject.SetActive(false);
                        old.name = DotsName + " (gone)";
                        Destroy(old.gameObject);
                    }
                }
                var holder = new GameObject(DotsName, typeof(RectTransform));
                _dots = (RectTransform)holder.transform;
                _dots.SetParent(parent, false);
                _dots.anchorMin = _dots.anchorMax = new Vector2(0.5f, 0f);
                _dots.pivot = new Vector2(0.5f, 0.5f);
                _dots.anchoredPosition = new Vector2(0f, DotsFromBottom);
                _dots.sizeDelta = new Vector2(200f, DotSize + 16f);
            }
            _dots.gameObject.SetActive(true);
            while (_dotImages.Count < Pages)
            {
                int page = _dotImages.Count;
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                dot.transform.SetParent(_dots, false);
                var image = dot.GetComponent<Image>();
                image.sprite = _dotSprite;
                var button = dot.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(Guard.Listener(() => Turn(page)));
                _dotImages.Add(image);
            }
            float width = DotsWidth(Pages);
            if (_strip)
            {
                PlaceStripDots(width);
            }
            for (int i = 0; i < _dotImages.Count; i++)
            {
                Image image = _dotImages[i];
                image.gameObject.SetActive(i < Pages);
                var rect = (RectTransform)image.transform;
                rect.sizeDelta = new Vector2(DotSize, DotSize);
                rect.anchoredPosition = new Vector2(-width * 0.5f + DotSize * 0.5f + i * (DotSize + DotGap), 0f);
            }
            Paint();
        }

        // In the row's space: after the place of the widest page (so they stand still as the pages turn), level with
        // the middle of the apps' pictures.
        private void PlaceStripDots(float width)
        {
            var strip = (RectTransform)transform;
            float x = _pagesLeft + _pagesWidth + StripDotsGap + width * 0.5f;
            float y = strip.rect.center.y;
            for (int i = 0; i < _apps.Count; i++)
            {
                if (!_apps[i].gameObject.activeSelf)
                {
                    continue;
                }
                Image picture = Picture(_apps[i]);
                if (picture != null)
                {
                    RectTransform rect = picture.rectTransform;
                    y = strip.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y;
                }
                break;
            }
            _dots.sizeDelta = new Vector2(width + 16f, DotSize + 16f);
            _dots.position = strip.TransformPoint(new Vector3(x, y, 0f));
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

        private void Paint()
        {
            if (_dots == null || _aside)
            {
                return;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            Color red = references != null ? (Color)references.colors.red : Color.red;
            for (int i = 0; i < _dotImages.Count && i < Pages; i++)
            {
                bool wanted = false;
                for (int a = 0; a < _attention.Count; a++)
                {
                    int index = _apps.IndexOf(_attention[a]);
                    wanted |= index >= 0 && PageOf(index) == i;
                }
                _dotImages[i].color = i == _page ? Color.white : wanted ? red : new Color(1f, 1f, 1f, 0.35f);
            }
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
                string assembly = script.GetType().Assembly.GetName().Name;
                bool known = script.GetType().Assembly == typeof(SmartphoneUI).Assembly || assembly.StartsWith("Unity", StringComparison.Ordinal)
                             || assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) || assembly == "HGPlugins" || assembly == "HGExtensions"
                             || assembly == "ExternalPlugins" || script.GetType().Assembly == typeof(PhoneGridPager).Assembly;
                if (!known)
                {
                    return true;
                }
            }
            return false;
        }

        private void StepAside()
        {
            _aside = true;
            ShowAll();
            RestoreStrip();
            if (_dots != null)
            {
                _dots.gameObject.SetActive(false);
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
            if (GameManager.isCitySceneBeingUnloaded)
            {
                return;   // (the interface goes with the city)
            }
            ShowAll();
            RestoreStrip();
            if (_dots != null)
            {
                Destroy(_dots.gameObject);
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

    /// <summary>On the full menu's row of app buttons: its alignment and left padding as the game built them, kept from the first time it was paged.</summary>
    public sealed class RowAsBuilt : MonoBehaviour
    {
        public TextAnchor Alignment;
        public int Left;
    }
}
