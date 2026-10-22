using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The watch over the mod's guide marks: once a second of real time it takes off a mark whose owner is gone, stopped or
    /// past its time (<see cref="GuideMarkBook.Sweep"/>), and every mark when a city starts or unloads. Costs one time
    /// comparison a frame.
    /// </summary>
    public sealed class GuideMarkWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "GuideMarkWatch", typeof(GuideMarkWatch));   // its Update

        private float _next;

        private void Awake()
        {
            Drop("a city is starting");   // a mark left from the city before: its owner and guider are gone
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: GuideMarkWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
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
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + GuideMarkRules.WatchEvery;
            GuideMark.Sweep();
        }

        private void OnDestroy()
        {
            Drop("the city is unloading");
        }

        private static void Drop(string why)
        {
            try
            {
                GuideMark.HideAll(why);
            }
            catch (System.Exception e)
            {
                GuideMark.Logger?.Error(e);
            }
        }
    }
}
