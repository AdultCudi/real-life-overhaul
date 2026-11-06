using System;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The modelled engine's light bar flashing, one end's lens then the other's, in step with TrafficEffects' flasher above
    /// it. On the lights' own object, so it exists only while they are on: a comparison a frame, the renderers touched only
    /// when the state flips, both dark when the lights go.
    /// </summary>
    internal sealed class BarFlash : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("events", "BarFlash", typeof(BarFlash));

        public Renderer Left;
        public Renderer Right;
        public float Interval;

        private int _shown = -1;

        private static readonly int HotUpdate = HotSpots.Slot("Events: BarFlash.Update");
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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            int shown = Interval > 0f && Mathf.FloorToInt(Time.unscaledTime / Interval) % 2 == 0 ? 0 : 1;
            if (shown != _shown)
            {
                _shown = shown;
                Show(shown == 0, shown == 1);
            }
        }

        private void OnDisable()
        {
            _shown = -1;
            Show(false, false);
        }

        private void Show(bool left, bool right)
        {
            if (Left != null)
            {
                Left.enabled = left;
            }
            if (Right != null)
            {
                Right.enabled = right;
            }
        }
    }
}
