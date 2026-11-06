using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The card's frame, sized by Unity's layout with no height or y of the mod's own. The game's white box was a fixed card
    /// whose vertical layout, given more than it could hold, squeezed every text towards a height of nothing (a text's own
    /// minimum), so section heads were drawn over the text above and wrapped lines over the next row. Now the card holds three
    /// parts: the head (the title, its line and its chips) and the foot (the page's rows of buttons and the Guide's page) ask
    /// for the height their parts ask for, and the body between them takes what is left and scrolls (a ScrollRect in the style of
    /// the game's own table, its scroll bar a copy of the table's). The body's content is as tall as its parts ask
    /// (ContentSizeFitter), so a page of any length, in any language, overlaps nothing. Costs nothing a frame: laid out only when a
    /// page changes or the card is resized.
    /// </summary>
    public sealed partial class InfoBox
    {
        private const string HairlineName = "Hairline";

        private Transform _head, _content, _foot;   // the card's three parts; the head and the foot are null for a box written into a scroll view of the game's
        private RectTransform _contentRect, _viewport;
        private ScrollRect _scroll;
        private Transform _group;           // the section card the page is writing into, or null
        private bool _bodyStarted;          // something has been written into the body: a title written now is no longer the card's header
        private string _pageKey, _lastKey;  // the page's title, and the last page's: the same title is the same page written again
        private float _lastY;               // where the last page was scrolled to
        private PartKind[] _kinds = new PartKind[64];
        private bool[] _toFoot = new bool[64];

        /// <summary>The body's scroll, or null for a box with none of its own.</summary>
        public ScrollRect Scroll => _scroll;

        // The card as three parts under its white box: head, body, foot.
        private void Frame(Transform whiteBox, ScrollRect style)
        {
            var layout = whiteBox.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                UiClone.Fallback("the game's white box has no vertical layout, so its card does not scroll");
                _content = whiteBox;
                _contentRect = whiteBox as RectTransform;
                return;
            }
            var fitter = whiteBox.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;   // the card keeps the height the app gives it; its body scrolls
            }
            layout.padding = new RectOffset(LeftPaneRules.PadLeft, LeftPaneRules.PadRight, LeftPaneRules.PadTop, LeftPaneRules.PadBottom);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _head = Column(whiteBox, "Head", new RectOffset(0, LeftPaneRules.Gutter, 0, 10), 2f).transform;
            MakeBody(whiteBox, style);
            GameObject foot = Column(whiteBox, "Foot", new RectOffset(0, LeftPaneRules.Gutter, 14, 0), 4f);
            _foot = foot.transform;
            Hairline(_foot);
            _head.gameObject.SetActive(false);   // shown while it holds something
            _foot.gameObject.SetActive(false);
        }

        // A box written into a scroll view of the game's: its content is the body, and there is no head or foot of its own.
        private void Inside(Transform content)
        {
            _content = content;
            _contentRect = content as RectTransform;
            _head = _foot = null;
            _viewport = null;
            _scroll = null;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = LeftPaneRules.BodySpacing;
            }
        }

        // A vertical stack that asks for the height of its parts.
        private static GameObject Column(Transform parent, string name, RectOffset padding, float spacing)
        {
            var made = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            made.transform.SetParent(parent, false);
            var group = made.GetComponent<VerticalLayoutGroup>();
            group.padding = padding;
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return made;
        }

        // The scrolling body: it takes the height the head and the foot leave (a flexible height and none preferred, so the card's
        // layout never squeezes the head or the foot), a masked viewport that catches the wheel, the content, the bar.
        private void MakeBody(Transform whiteBox, ScrollRect style)
        {
            var body = new GameObject("Body", typeof(RectTransform), typeof(LayoutElement), typeof(ScrollRect));
            body.transform.SetParent(whiteBox, false);
            var size = body.GetComponent<LayoutElement>();
            size.minHeight = 0f;
            size.preferredHeight = 0f;
            size.flexibleHeight = 1f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(body.transform, false);
            _viewport = (RectTransform)viewport.transform;
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.pivot = new Vector2(0f, 1f);
            _viewport.offsetMin = _viewport.offsetMax = Vector2.zero;
            var catcher = viewport.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;   // the wheel and the drag are the pointer's over the body, whatever it is over

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(_viewport, false);
            _content = content.transform;
            _contentRect = (RectTransform)_content;
            _contentRect.anchorMin = new Vector2(0f, 1f);
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = Vector2.zero;
            var stack = content.GetComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(0, LeftPaneRules.Gutter, 4, 28);
            stack.spacing = LeftPaneRules.BodySpacing;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            var fit = content.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = body.GetComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _contentRect;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = style != null ? style.movementType : ScrollRect.MovementType.Clamped;
            _scroll.elasticity = style != null ? style.elasticity : 0.1f;
            _scroll.inertia = style != null && style.inertia;
            _scroll.decelerationRate = style != null ? style.decelerationRate : 0.135f;
            _scroll.scrollSensitivity = style != null && style.scrollSensitivity > 0f ? style.scrollSensitivity : 40f;   // the game's own wheel step
            CopyBar(body.transform, style != null ? style.verticalScrollbar : null);
        }

        // The bar of the game's own table, with the scripts that are not Unity's taken off, in the gutter at the body's right; without
        // one the body still scrolls.
        private void CopyBar(Transform body, Scrollbar source)
        {
            if (source == null)
            {
                UiClone.Fallback("the game's table has no scroll bar to copy, so the card's body scrolls without one");
                return;
            }
            try
            {
                GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, body, false);
                copy.name = "ScrollBar";
                MonoBehaviour[] scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < scripts.Length; i++)
                {
                    string space = scripts[i] != null ? scripts[i].GetType().Namespace : null;
                    if (scripts[i] != null && (space == null || !space.StartsWith("UnityEngine", StringComparison.Ordinal)))
                    {
                        UnityEngine.Object.DestroyImmediate(scripts[i]);
                    }
                }
                var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
                element.ignoreLayout = true;
                var rect = (RectTransform)copy.transform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-(LeftPaneRules.Gutter - LeftPaneRules.BarWidth) * 0.5f, 0f);
                rect.sizeDelta = new Vector2(LeftPaneRules.BarWidth, -8f);
                Scrollbar bar = copy.GetComponent<Scrollbar>();
                if (bar == null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    return;
                }
                bar.onValueChanged = new Scrollbar.ScrollEvent();
                var track = copy.GetComponent<Image>();
                if (track != null)
                {
                    track.color = Color.Lerp(_rule, Color.white, 0.4f);   // the card is white: the game's bar is made for its dark table
                }
                var handle = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
                if (handle != null)
                {
                    handle.color = _muted;
                }
                _scroll.verticalScrollbar = bar;
                _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                copy.SetActive(true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A hairline across the top of a part that does not lay it out: where the foot begins.
        private void Hairline(Transform under)
        {
            Image line = Picture(under, HairlineName, null, _rule);
            var rect = (RectTransform)line.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);   // from the left edge to the gutter
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-LeftPaneRules.Gutter, LookRules.RuleHeight);
        }

        // Where the page is written: the section card it is in, else the body. Writing there ends the header.
        private Transform Body()
        {
            _bodyStarted = true;
            return _group != null ? _group : _content;
        }

        // Where the header's parts stand: the head while the page has written nothing into the body, else the body.
        private Transform Head()
        {
            return _head != null && !_bodyStarted ? _head : Body();
        }

        // A place the box keeps parts in: those it lays aside between pages.
        private bool Ours(Transform place)
        {
            return place != null && (place == _head || place == _content || place == _foot || place == _group);
        }

        // The width a fact's row has: the body's width less the gutter (and the section card's padding), or the phone's for a body
        // not laid out yet.
        private float Room()
        {
            float gutter = _head != null ? LeftPaneRules.Gutter : 0f;
            float width = _contentRect != null ? _contentRect.rect.width : 0f;
            float room = width > 200f ? width - gutter : LeftPaneRules.DefaultRoom;
            return _group != null ? room - 2f * LeftPaneRules.GroupPadX : room;
        }

        // What of the last page is kept for the next: where it was scrolled to and its title.
        private void LeaveScroll()
        {
            _lastKey = _pageKey;
            _pageKey = null;
            _lastY = _scroll != null && _contentRect != null ? _contentRect.anchoredPosition.y : 0f;
        }

        // A page is written: the rows of buttons that close it stand in the foot, the head and the foot show while they hold
        // something, and the body is scrolled to the top for another page (another title) and left where it was for the same.
        private void Settle()
        {
            MoveFoot();
            if (_head != null)
            {
                _head.gameObject.SetActive(_head.childCount > 0);
            }
            if (_foot != null)
            {
                _foot.gameObject.SetActive(_foot.childCount > 1);   // the hairline is the first
            }
            if (_scroll != null && _contentRect != null)
            {
                float y = LeftPaneRules.KeepOffset(LeftPaneRules.SamePage(_lastKey, _pageKey), _lastY, float.MaxValue, 0f);   // the scroll clamps it to the new page's length
                _contentRect.anchoredPosition = new Vector2(_contentRect.anchoredPosition.x, y);
                _scroll.StopMovement();
            }
        }

        /// <summary>The body goes to the top, as when another row is picked.</summary>
        public void ScrollToTop()
        {
            if (_scroll != null && _contentRect != null)
            {
                _contentRect.anchoredPosition = new Vector2(_contentRect.anchoredPosition.x, 0f);
                _scroll.StopMovement();
            }
            _lastKey = null;
        }

        private void MoveFoot()
        {
            if (_foot == null)
            {
                return;
            }
            int n = _live.Count;
            if (_kinds.Length < n)
            {
                _kinds = new PartKind[n * 2];
                _toFoot = new bool[n * 2];
            }
            for (int i = 0; i < n; i++)
            {
                _kinds[i] = KindOf(_liveFrom[i]);
            }
            if (LeftPaneRules.FootRows(_kinds, n, _toFoot) == 0)
            {
                return;
            }
            int at = 1;   // after the hairline
            for (int i = 0; i < n; i++)
            {
                if (_toFoot[i] && _live[i] != null)
                {
                    _live[i].transform.SetParent(_foot, false);
                    _live[i].transform.SetSiblingIndex(at++);   // before the Guide's page, which was written into the foot first
                }
            }
        }

        private PartKind KindOf(GameObject template)
        {
            if (template == _buttons)
            {
                return PartKind.Buttons;
            }
            if (template == _bodyT || template == _noteT || template == _subT || template == _headerT || template == _guideT)
            {
                return PartKind.Light;
            }
            return PartKind.Heavy;
        }

        /// <summary>
        /// The page written now, measured where it stands, for the in-game test: no text taller than the place it is given, no two
        /// texts' boxes crossing, none past the body's edge, the head and foot leaving the body room, and the scroll reaching the
        /// page's last part. Works the layout out at once (twice: the second pass measures each text at the width the first gave it).
        /// Returns the number of faults, each said in <paramref name="said"/>.
        /// </summary>
        public int Audit(StringBuilder said)
        {
            var box = (RectTransform)_box;
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
            int faults = 0;
            TMP_Text[] texts = box.GetComponentsInChildren<TMP_Text>(false);
            var left = new List<float>(texts.Length);
            var right = new List<float>(texts.Length);
            var top = new List<float>(texts.Length);
            var bottom = new List<float>(texts.Length);
            var names = new List<string>(texts.Length);
            var corners = new Vector3[4];
            Rect inside = _contentRect != null ? BoxRect(box, _contentRect, corners) : BoxRect(box, box, corners);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null || string.IsNullOrEmpty(t.text) || t.GetComponentInParent<ButtonFlow>() != null)
                {
                    continue;   // a button's label is the game's (it cuts its own words with dots)
                }
                Rect r = BoxRect(box, t.rectTransform, corners);
                t.ForceMeshUpdate();
                float k = box.lossyScale.y > 0f ? t.rectTransform.lossyScale.y / box.lossyScale.y : 1f;
                float drawn = t.textBounds.size.y * k;
                float used = Mathf.Max(r.height, drawn);
                string name = t.transform.parent != null ? t.transform.parent.name + "/" + t.name : t.name;
                if (drawn > r.height + 1.5f)
                {
                    faults++;
                    said.Append(" clipped: '").Append(Short(t.text)).Append("' needs ").Append(Mathf.RoundToInt(drawn)).Append(" px and has ").Append(Mathf.RoundToInt(r.height)).Append(";");
                }
                bool inBody = _content != null && t.transform.IsChildOf(_content);
                Rect room = inBody ? inside : BoxRect(box, box, corners);
                if (r.xMin < room.xMin - 1f || r.xMax > room.xMax + 1f)
                {
                    faults++;
                    said.Append(" past the edge: '").Append(Short(t.text)).Append("';");
                }
                left.Add(r.xMin);
                right.Add(r.xMax);
                top.Add(r.yMax);
                bottom.Add(r.yMax - used);
                names.Add(name);
            }
            for (int a = 0; a < names.Count; a++)
            {
                for (int b = a + 1; b < names.Count; b++)
                {
                    if (LeftPaneRules.Crosses(left[a], right[a], top[a], bottom[a], left[b], right[b], top[b], bottom[b]))
                    {
                        faults++;
                        said.Append(" overlap: ").Append(names[a]).Append(" and ").Append(names[b]).Append(";");
                    }
                }
            }
            if (_scroll != null && _viewport != null && _contentRect != null)
            {
                faults += AuditScroll(said);
            }
            return faults;
        }

        private int AuditScroll(StringBuilder said)
        {
            int faults = 0;
            if (!_scroll.vertical || _scroll.viewport != _viewport || _scroll.content != _contentRect || !(_scroll.scrollSensitivity > 0f)
                || _viewport.GetComponent<Image>() == null || !_viewport.GetComponent<Image>().raycastTarget)
            {
                faults++;
                said.Append(" the wheel cannot reach the body;");
            }
            float page = _contentRect.rect.height, view = _viewport.rect.height;
            if (view < LeftPaneRules.MinBody)
            {
                faults++;
                said.Append(" the body is only ").Append(Mathf.RoundToInt(view)).Append(" px high;");
            }
            if (page > view + 1f)
            {
                Vector2 was = _contentRect.anchoredPosition;
                _contentRect.anchoredPosition = new Vector2(was.x, page - view);   // the scroll's foot
                Transform last = null;
                for (int i = _content.childCount - 1; i >= 0 && last == null; i--)
                {
                    Transform child = _content.GetChild(i);
                    last = child.gameObject.activeSelf ? child : null;
                }
                if (last != null)
                {
                    var corners = new Vector3[4];
                    ((RectTransform)last).GetWorldCorners(corners);
                    float bottom = _viewport.InverseTransformPoint(corners[0]).y;
                    if (!LeftPaneRules.ReachesEnd(bottom, _viewport.rect.yMin))
                    {
                        faults++;
                        said.Append(" the scroll does not reach the page's end;");
                    }
                }
                _contentRect.anchoredPosition = was;
            }
            return faults;
        }

        // A rectangle in the box's own space, as the layout has it now.
        private static Rect BoxRect(RectTransform box, RectTransform of, Vector3[] corners)
        {
            of.GetWorldCorners(corners);
            Vector3 low = box.InverseTransformPoint(corners[0]), high = box.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        private static string Short(string words)
        {
            return words.Length > 28 ? words.Substring(0, 28) : words;
        }

        /// <summary>For the test: how tall the page is and how tall the body that shows it, in canvas pixels.</summary>
        public string Heights()
        {
            if (_viewport == null || _contentRect == null)
            {
                return "no body of its own";
            }
            return "page " + Mathf.RoundToInt(_contentRect.rect.height) + " px, body " + Mathf.RoundToInt(_viewport.rect.height) + " px";
        }
    }
}
