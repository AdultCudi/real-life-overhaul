using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The mouse wheel over Newsstand's page: each notch sets where the page is to be, a few lines further on, and the
    /// glide (<see cref="NewsstandGlide"/>) takes it there smoothly. Dragging is the game's own ScrollRect's, with its
    /// momentum; a drag stops a glide. This part has no Update: it only answers the wheel and the drag, and only while the
    /// page is open (a closed app's panel is off, and gets no events).
    /// </summary>
    public sealed class NewsstandWheel : MonoBehaviour, IScrollHandler, IBeginDragHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "NewsstandWheel", typeof(NewsstandWheel));   // the game's calls of it (Core/Guard)

        internal NewsstandGlide Glide;
        internal float Step = 120f;     // canvas units a notch
        private float _unit = 120f;     // the wheel's delta for one notch, as learnt

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (Glide != null && data != null)
                {
                    // A notch of the wheel is 1 from one input module, 6 or 120 from others: the smallest step seen is taken
                    // for one notch (between 1 and 120), so the page moves the same few lines a notch whichever it is.
                    float d = data.scrollDelta.y;
                    if (d == 0f)
                    {
                        return;
                    }
                    _unit = Mathf.Clamp(Mathf.Min(_unit, Mathf.Abs(d)), 1f, 120f);
                    Glide.By(-Mathf.Clamp(d / _unit, -5f, 5f) * Step);
                }
            }
            catch (System.Exception fault)
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
                if (Glide != null)
                {
                    Glide.Stop();
                }
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }

    /// <summary>
    /// Moves Newsstand's page towards where the wheel wants it, easing in on the real clock (the game stands still while
    /// the phone is open). Switched on only while it is moving: no frame is spent on it otherwise, and nothing is made
    /// while it runs.
    /// </summary>
    public sealed class NewsstandGlide : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "NewsstandGlide", typeof(NewsstandGlide));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float Speed = 14f;       // how quickly it closes on the target: most of the way in a sixth of a second

        internal ScrollRect Scroll;
        private float _target;

        /// <summary>The page moved on (down for more than 0) from where it is going; held within the page.</summary>
        public void By(float amount)
        {
            if (Scroll == null || Scroll.content == null || Scroll.viewport == null)
            {
                return;
            }
            float from = enabled ? _target : Scroll.content.anchoredPosition.y;
            _target = Mathf.Clamp(from + amount, 0f, Most());
            Scroll.StopMovement();
            enabled = true;
        }

        /// <summary>To the top at once.</summary>
        public void Top()
        {
            Stop();
            if (Scroll != null && Scroll.content != null)
            {
                Scroll.StopMovement();
                Scroll.content.anchoredPosition = new Vector2(Scroll.content.anchoredPosition.x, 0f);
            }
        }

        public void Stop()
        {
            enabled = false;
        }

        private float Most()
        {
            return Mathf.Max(0f, Scroll.content.rect.height - Scroll.viewport.rect.height);
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ledger: NewsstandGlide.Update");
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
            catch (System.Exception e)
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
