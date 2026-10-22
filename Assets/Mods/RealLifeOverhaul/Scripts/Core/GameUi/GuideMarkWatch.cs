using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The watch over the mod's marks on the game's destination guider (<see cref="GuideMark"/>): once a second of real
    /// time it takes off a mark whose owner was destroyed, stopped by the guard or past its latest time, and forgets one
    /// that lost the guider (<see cref="GuideMarkBook.Sweep"/>). Every mark goes when a city starts and when it unloads.
    /// Costs one time comparison a frame; once a second, a few compares per mark up.
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
