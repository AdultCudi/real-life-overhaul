using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's one runner of motion: arrivals (a fade and a short rise), a screen opening, figures counting up and a
    /// loading state's beat, as entries in a fixed array of structs on the real clock, so starting one allocates nothing
    /// and a finished one leaves no trace. It switches itself off when nothing moves. Cost a frame: nothing while still
    /// (disabled); while moving, a few property writes an entry, up to <see cref="Most"/> entries.
    /// </summary>
    public sealed class UiTweens : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiTweens", typeof(UiTweens));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: UiTweens.Update");

        public const int Most = 512;           // entries at once: two screens' worth of arriving rows
        private const float BeatPeriod = 0.9f;  // seconds a loading dot takes to beat once, as the wireframe's typing dots

        private enum Kind : byte
        {
            Arrive,
            Open,
            Count,
            Beat
        }

        private struct Entry
        {
            public Kind Kind;
            public UiView.Slot Slot;
            public CanvasGroup Group;
            public RectTransform Rect;
            public Graphic Graphic;
            public TMP_Text Text;
            public string Final;
            public float Start, Length, Rise;
            public Vector2 Base;
        }

        private readonly Entry[] _entries = new Entry[Most];
        private readonly char[] _digits = new char[48];
        private int _count;

        /// <summary>The runner, made by the kit's host once a session.</summary>
        public static UiTweens Instance;

        /// <summary>A slot arriving: faded in and risen into place from <paramref name="rise"/> canvas units below.</summary>
        internal void Arrive(UiView.Slot slot, float start, float length, float rise)
        {
            if (slot == null || length <= 0f)
            {
                return;
            }
            Stop(slot.Rect);   // an older arrival on the same object would hold its old place and fight this one
            var e = new Entry { Kind = Kind.Arrive, Slot = slot, Rect = slot.Rect, Start = start, Length = length, Rise = rise, Base = slot.Rect.anchoredPosition };
            Fade(slot, 0f);
            slot.Rect.anchoredPosition = e.Base - new Vector2(0f, rise);
            Add(ref e);
        }

        /// <summary>A screen opening: its group faded in, its root risen into place.</summary>
        public void Open(CanvasGroup group, RectTransform root, float start, float length, float rise)
        {
            if (length <= 0f)
            {
                group.alpha = 1f;
                return;
            }
            Stop(root);
            var e = new Entry { Kind = Kind.Open, Group = group, Rect = root, Start = start, Length = length, Rise = rise, Base = root.anchoredPosition };
            group.alpha = 0f;
            root.anchoredPosition = e.Base - new Vector2(0f, rise);
            Add(ref e);
        }

        /// <summary>A figure counting up from nothing to its words' number, in their form.</summary>
        public void Count(TMP_Text text, string final, float start, float length)
        {
            if (text == null || length <= 0f || UiMotionRules.CountInto(final, 0f, _digits) < 0)
            {
                return;
            }
            var e = new Entry { Kind = Kind.Count, Text = text, Final = final, Start = start, Length = length };
            Add(ref e);
        }

        /// <summary>A loading dot beating until it is hidden.</summary>
        public void Beat(Graphic dot, float phase)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Kind == Kind.Beat && _entries[i].Graphic == dot)
                {
                    return;
                }
            }
            var e = new Entry { Kind = Kind.Beat, Graphic = dot, Start = Time.unscaledTime + phase, Length = BeatPeriod };
            Add(ref e);
        }

        /// <summary>Every entry on objects under a root stopped where it ends: arrivals in place, counts written whole.</summary>
        public void StopAll(Transform root)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                Transform t = Target(ref _entries[i]);
                if (t != null && t.IsChildOf(root))
                {
                    Finish(ref _entries[i]);
                    _entries[i] = _entries[--_count];
                    _entries[_count] = default(Entry);
                }
            }
        }

        private void Stop(Transform target)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                if (Target(ref _entries[i]) == target)
                {
                    Finish(ref _entries[i]);
                    _entries[i] = _entries[--_count];
                    _entries[_count] = default(Entry);
                }
            }
        }

        private void Add(ref Entry e)
        {
            if (_count == Most || FrameGuard.Off)
            {
                Finish(ref e);   // full, or the runner is off and would never move it: it lands at once (an opening left at the start of its fade is a screen nobody sees)
                return;
            }
            _entries[_count++] = e;
            enabled = true;
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            using var cost = UiCost.Kit.Time(UiWork.Frame);   // the interface's share of the frame (Host/UiCost)
            if (FrameGuard.Off)
            {
                Land();            // its feature was switched off after its faults (Core/Guard): nothing is left part way
                enabled = false;
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

        /// <summary>
        /// Every entry put where it ends, at once: the interface's part has stopped and nothing will move them on. An opening
        /// left at the start of its fade would be a screen that is there and cannot be seen.
        /// </summary>
        public void Land()
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                try
                {
                    Finish(ref _entries[i]);
                }
                catch (Exception)
                {
                    // its object went with its screen: nothing to land
                }
                _entries[i] = default(Entry);
            }
            _count = 0;
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            for (int i = _count - 1; i >= 0; i--)
            {
                bool more;
                try
                {
                    more = Step(ref _entries[i], now);
                }
                catch (Exception e)
                {
                    // One entry's fault costs that entry alone: left in the list it threw every frame, and five frames later
                    // the guard switched the whole interface off for one row's fade.
                    more = false;
                    Dropped(e);
                }
                if (!more)
                {
                    _entries[i] = _entries[--_count];
                    _entries[_count] = default(Entry);
                }
            }
            if (_count == 0)
            {
                enabled = false;
            }
        }

        // One entry's frame; false once it is done (or its object has gone).
        private bool Step(ref Entry e, float now)
        {
            Transform target = Target(ref e);
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (target != null)
                {
                    Finish(ref e);
                }
                return false;
            }
            if (now < e.Start)
            {
                return true;
            }
            if (e.Kind == Kind.Beat)
            {
                float p = ((now - e.Start) % e.Length) / e.Length;
                e.Graphic.canvasRenderer.SetAlpha(0.4f + 0.6f * (1f - Mathf.Abs(p * 2f - 1f)));
                return true;
            }
            float t = Mathf.Clamp01((now - e.Start) / e.Length);
            float k = UiMotionRules.Ease(t);
            switch (e.Kind)
            {
                case Kind.Arrive:
                    Fade(e.Slot, k);
                    e.Rect.anchoredPosition = e.Base - new Vector2(0f, e.Rise * (1f - k));
                    break;
                case Kind.Open:
                    e.Group.alpha = k;
                    e.Rect.anchoredPosition = e.Base - new Vector2(0f, e.Rise * (1f - k));
                    break;
                case Kind.Count:
                    int n = UiMotionRules.CountInto(e.Final, k, _digits);
                    if (n > 0)
                    {
                        e.Text.SetCharArray(_digits, 0, n);
                    }
                    break;
            }
            if (t >= 1f)
            {
                Finish(ref e);
                return false;
            }
            return true;
        }

        private static bool _droppedSaid;

        // Said once a session: which fault an entry was dropped for.
        private static void Dropped(Exception e)
        {
            if (_droppedSaid)
            {
                return;
            }
            _droppedSaid = true;
            UiDirector.Logger?.Warn("UI: a piece of motion was dropped where it stood after a fault (" + FaultWords.Of(e) + "); the screen it moved is drawn in place.");
        }

        private static void Finish(ref Entry e)
        {
            switch (e.Kind)
            {
                case Kind.Arrive:
                    if (e.Rect != null)
                    {
                        Fade(e.Slot, 1f);
                        e.Rect.anchoredPosition = e.Base;
                    }
                    break;
                case Kind.Open:
                    if (e.Group != null)
                    {
                        e.Group.alpha = 1f;
                        e.Rect.anchoredPosition = e.Base;
                    }
                    break;
                case Kind.Count:
                    if (e.Text != null)
                    {
                        e.Text.SetText(e.Final);
                    }
                    break;
                case Kind.Beat:
                    if (e.Graphic != null)
                    {
                        e.Graphic.canvasRenderer.SetAlpha(1f);
                    }
                    break;
            }
        }

        private static Transform Target(ref Entry e)
        {
            switch (e.Kind)
            {
                case Kind.Count: return e.Text != null ? e.Text.transform : null;
                case Kind.Beat: return e.Graphic != null ? e.Graphic.transform : null;
                default: return e.Rect;
            }
        }

        private static void Fade(UiView.Slot slot, float a)
        {
            Graphic[] g = slot.Graphics;
            for (int i = 0; g != null && i < g.Length; i++)
            {
                if (g[i] != null)
                {
                    g[i].canvasRenderer.SetAlpha(a);
                }
            }
        }

        private void OnDisable()
        {
            enabled = false;
        }
    }
}
