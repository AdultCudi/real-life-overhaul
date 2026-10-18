using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// Moves Newsstand's page towards where the wheel wants it, on the real clock (the game stands still under the
    /// phone). The cost: enabled only while it moves, nothing per frame otherwise; nothing is allocated.
    /// </summary>
    public sealed class NewsstandGlide : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "NewsstandGlide", typeof(NewsstandGlide));

        private const float Speed = 14f;       // per second: most of the way to the target in a sixth of a second

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
                enabled = false;
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
