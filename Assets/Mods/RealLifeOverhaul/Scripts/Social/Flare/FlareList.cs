using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One row of a Flare list: what it shows (a post, a notification, a conversation...) and how tall it is.</summary>
    internal sealed class FlareRow
    {
        public int Kind;                 // which kind of cell shows it (FlareView's)
        public FlarePost Post;
        public FlareNotice Notice;
        public FlareDm Dm;
        public string Key;               // an account, a tag, a conversation, a business...
        public string Text;              // a line of its own (a note, a title)
        public int Number;               // a figure of its own (a count, an index)
        public float Height;             // canvas units: measured once its cell is bound, estimated before
        public bool Measured;
        public float Width;              // the width it was measured at
        public int At;                   // its place in its list

        public void Reset()
        {
            Kind = 0;
            Post = null;
            Notice = default(FlareNotice);
            Dm = null;
            Key = Text = null;
            Number = 0;
            Height = 0f;
            Measured = false;
            Width = 0f;
        }
    }

    /// <summary>A cell of a Flare list: made once, bound to one row after another as the list scrolls.</summary>
    internal abstract class FlareCell
    {
        public RectTransform Rect;
        public FlareRow Row;
        public int Kind;

        /// <summary>Shows a row at a width; returns the height it takes.</summary>
        public abstract float Bind(FlareRow row, float width);

        /// <summary>The look changed (light, dark): the colours set again.</summary>
        public abstract void Paint(FlareLook look);
    }

    /// <summary>
    /// A scrolling list of Flare's: only the rows in view (and a screen's worth either side) have a cell, and cells are
    /// reused as the list scrolls (the 0.19.2 rule: no hitch on opening or scrolling). A row's height is estimated until its
    /// cell has shown it once, then measured and kept; what is below moves to fit, and what is in view stays where it is.
    /// The mouse wheel glides the list smoothly (FlareGlide); dragging is the engine's own ScrollRect, with its momentum.
    ///
    /// COST: nothing while it stands still. While it moves, one look a frame at which rows are in view (a binary search over
    /// their tops), and a cell bound only when a row comes into view: a few texts set.
    /// </summary>
    internal sealed class FlareList
    {
        public const float Gap = 18f;
        private const float Over = 600f;           // canvas units beyond the view that have cells, either side

        public readonly RectTransform Viewport, Content;
        public readonly ScrollRect Scroll;
        private readonly FlareGlide _glide;
        private readonly List<FlareRow> _rows = new List<FlareRow>(128);
        private readonly List<float> _tops = new List<float>(128);
        private readonly Dictionary<int, Stack<FlareCell>> _pool = new Dictionary<int, Stack<FlareCell>>(8);
        private readonly List<FlareCell> _live = new List<FlareCell>(16);
        private readonly Func<int, FlareCell> _make;
        private readonly Func<FlareRow, float, float> _estimate;
        private float _width;
        private bool _filling;
        private FlareLook _look;

        public FlareList(Transform parent, Func<int, FlareCell> make, Func<FlareRow, float, float> estimate)
        {
            _make = make;
            _estimate = estimate;
            RectTransform root = FlareKit.Rect(parent, "List");
            var catcher = root.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);   // takes the wheel and the drag everywhere over the list
            Viewport = FlareKit.Rect(root, "Viewport");
            FlareKit.Stretch(Viewport, 0f, 0f, 0f, 0f);
            Viewport.gameObject.AddComponent<RectMask2D>();
            Content = FlareKit.Rect(Viewport, "Content");
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = new Vector2(1f, 1f);
            Content.pivot = new Vector2(0.5f, 1f);
            Content.sizeDelta = new Vector2(0f, 10f);
            Scroll = root.gameObject.AddComponent<ScrollRect>();
            Scroll.viewport = Viewport;
            Scroll.content = Content;
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Elastic;
            Scroll.elasticity = 0.1f;
            Scroll.inertia = true;
            Scroll.decelerationRate = 0.135f;
            Scroll.scrollSensitivity = 0f;   // the wheel is the glide's
            Scroll.onValueChanged.AddListener(Guard.Listener<Vector2>(Moved));
            _glide = root.gameObject.AddComponent<FlareGlide>();
            _glide.Scroll = Scroll;
            _glide.enabled = false;
            var wheel = root.gameObject.AddComponent<FlareWheel>();
            wheel.Glide = _glide;
            Root = root;
        }

        public readonly RectTransform Root;

        public IReadOnlyList<FlareRow> Rows => _rows;

        /// <summary>The rows to show, newest first or as the screen orders them; 'top' takes the list back to its top.</summary>
        public void Set(List<FlareRow> rows, bool top)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Free(_live[i]);
            }
            _live.Clear();
            _rows.Clear();
            _rows.AddRange(rows);
            _width = Viewport.rect.width;
            Layout();
            if (top)
            {
                _glide.Stop();
                Scroll.StopMovement();
                Content.anchoredPosition = Vector2.zero;
            }
            Fill();
        }

        /// <summary>The look changed: every cell made is painted again.</summary>
        public void Paint(FlareLook look)
        {
            _look = look;
            for (int i = 0; i < _live.Count; i++)
            {
                _live[i].Paint(look);
            }
            foreach (KeyValuePair<int, Stack<FlareCell>> pair in _pool)
            {
                foreach (FlareCell c in pair.Value)
                {
                    c.Paint(look);
                }
            }
        }

        /// <summary>The cells in view shown again as they stand (a like, a picture come in).</summary>
        public void Rebind()
        {
            bool moved = false;
            for (int i = 0; i < _live.Count; i++)
            {
                FlareCell c = _live[i];
                float h = c.Bind(c.Row, _width);
                if (Mathf.Abs(h - c.Row.Height) > 0.5f)
                {
                    c.Row.Height = h;
                    moved = true;
                }
            }
            if (moved)
            {
                Relayout();
            }
        }

        private void Layout()
        {
            _tops.Clear();
            float y = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                FlareRow r = _rows[i];
                if (!r.Measured || Mathf.Abs(r.Width - _width) > 1f)
                {
                    r.Height = _estimate(r, _width);
                    r.Measured = false;
                }
                r.At = i;
                _tops.Add(y);
                y += r.Height + Gap;
            }
            Content.sizeDelta = new Vector2(0f, Mathf.Max(10f, y + Gap * 2f));
        }

        private void Moved(Vector2 _)
        {
            if (!_filling)
            {
                Fill();
            }
        }

        // The rows in view (and a screen's worth either side) get a cell; the others give theirs back.
        private void Fill()
        {
            if (_rows.Count == 0 || _width <= 0f)
            {
                return;
            }
            _filling = true;
            try
            {
                float from = Content.anchoredPosition.y - Over, to = Content.anchoredPosition.y + Viewport.rect.height + Over;
                int first = Find(from), last = Find(to);
                for (int i = _live.Count - 1; i >= 0; i--)
                {
                    int at = _live[i].Row.At;
                    if (at < first || at > last)
                    {
                        Free(_live[i]);
                        _live.RemoveAt(i);
                    }
                }
                bool changed = false;
                for (int i = first; i <= last && i < _rows.Count; i++)
                {
                    if (Bound(_rows[i]))
                    {
                        continue;
                    }
                    FlareCell c = Take(_rows[i].Kind);
                    c.Row = _rows[i];
                    FlareKit.Show(c.Rect, true);   // (shown before it is bound: its texts measure themselves as they will be drawn)
                    float h = c.Bind(_rows[i], _width);
                    _rows[i].Width = _width;
                    if (!_rows[i].Measured || Mathf.Abs(h - _rows[i].Height) > 0.5f)
                    {
                        changed |= Mathf.Abs(h - _rows[i].Height) > 0.5f;
                        _rows[i].Height = h;
                        _rows[i].Measured = true;
                    }
                    _live.Add(c);
                }
                if (changed)
                {
                    Relayout();
                }
                else
                {
                    Position();
                }
            }
            finally
            {
                _filling = false;
            }
        }

        // The tops worked out again after a measure; what is in view stays where it was on the screen.
        private void Relayout()
        {
            FlareRow anchor = null;
            float anchorScreen = 0f;
            float scrolled = Content.anchoredPosition.y;
            for (int i = 0; i < _rows.Count && i < _tops.Count; i++)
            {
                if (_tops[i] + _rows[i].Height >= scrolled)
                {
                    anchor = _rows[i];
                    anchorScreen = _tops[i] - scrolled;
                    break;
                }
            }
            _tops.Clear();
            float y = 0f;
            int at = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                _tops.Add(y);
                _rows[i].At = i;
                if (_rows[i] == anchor)
                {
                    at = i;
                }
                y += _rows[i].Height + Gap;
            }
            Content.sizeDelta = new Vector2(0f, Mathf.Max(10f, y + Gap * 2f));
            if (at >= 0 && scrolled > 1f)
            {
                Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, Mathf.Max(0f, _tops[at] - anchorScreen));
            }
            Position();
        }

        private void Position()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                int at = _live[i].Row.At;
                if (at >= 0 && at < _tops.Count)
                {
                    FlareKit.Place(_live[i].Rect, 0f, _tops[at], _width, _live[i].Row.Height);
                }
            }
        }

        private bool Bound(FlareRow row)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Row == row)
                {
                    return true;
                }
            }
            return false;
        }

        // The first row whose bottom is below a point (a binary search over the tops).
        private int Find(float y)
        {
            int lo = 0, hi = _rows.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_tops[mid] + _rows[mid].Height < y)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return Mathf.Clamp(lo, 0, _rows.Count - 1);
        }

        private FlareCell Take(int kind)
        {
            Stack<FlareCell> stack;
            if (_pool.TryGetValue(kind, out stack) && stack.Count > 0)
            {
                return stack.Pop();
            }
            FlareCell c = _make(kind);
            c.Kind = kind;
            c.Rect.SetParent(Content, false);
            if (_look != null)
            {
                c.Paint(_look);
            }
            return c;
        }

        private void Free(FlareCell c)
        {
            FlareKit.Show(c.Rect, false);
            c.Row = null;
            Stack<FlareCell> stack;
            if (!_pool.TryGetValue(c.Kind, out stack))
            {
                stack = new Stack<FlareCell>(8);
                _pool[c.Kind] = stack;
            }
            stack.Push(c);
        }

        /// <summary>The width changed (the app was laid out): every row is measured again as it shows.</summary>
        public void Resized()
        {
            float width = Viewport.rect.width;
            if (Mathf.Abs(width - _width) > 1f)
            {
                var rows = new List<FlareRow>(_rows);
                Set(rows, false);
            }
        }
    }

    /// <summary>
    /// The mouse wheel over a Flare list: each notch sets where the list is to be, a little further on, and the glide takes
    /// it there smoothly. A drag stops a glide. No Update: it answers the wheel and the drag, only while the app is open.
    /// </summary>
    internal sealed class FlareWheel : MonoBehaviour, IScrollHandler, IBeginDragHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("flare", "FlareWheel", typeof(FlareWheel));   // the engine's calls of it (Core/Guard)

        internal FlareGlide Glide;
        internal float Step = 160f;     // canvas units a notch
        private float _unit = 120f;     // the wheel's delta for one notch, as learnt

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                float d = data != null ? data.scrollDelta.y : 0f;
                if (Glide == null || d == 0f)
                {
                    return;
                }
                _unit = Mathf.Clamp(Mathf.Min(_unit, Mathf.Abs(d)), 1f, 120f);
                Glide.By(-Mathf.Clamp(d / _unit, -5f, 5f) * Step);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Glide?.Stop();
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }

    /// <summary>Moves a Flare list towards where the wheel wants it, easing in on the real clock. Switched on only while it moves.</summary>
    internal sealed class FlareGlide : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("flare", "FlareGlide", typeof(FlareGlide));   // its Update (Core/Guard)

        private const float Speed = 14f;       // most of the way in a sixth of a second

        internal ScrollRect Scroll;
        private float _target;

        public void By(float amount)
        {
            if (Scroll == null || Scroll.content == null || Scroll.viewport == null)
            {
                return;
            }
            float from = enabled ? _target : Scroll.content.anchoredPosition.y;
            _target = Mathf.Clamp(from + amount, 0f, Mathf.Max(0f, Scroll.content.rect.height - Scroll.viewport.rect.height));
            Scroll.StopMovement();
            enabled = true;
        }

        public void Stop()
        {
            enabled = false;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Social: FlareGlide.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Scroll == null || Scroll.content == null)
            {
                enabled = false;
                return;
            }
            Vector2 at = Scroll.content.anchoredPosition;
            float k = 1f - Mathf.Exp(-Speed * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            float y = Mathf.Lerp(at.y, _target, k);
            if (Mathf.Abs(y - _target) < 0.5f)
            {
                y = _target;
                enabled = false;
            }
            Scroll.content.anchoredPosition = new Vector2(at.x, y);
        }

        private void OnDisable()
        {
            enabled = false;
        }
    }
}
