using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The watch over the mod's marks on the game's destination guider (<see cref="GuideMark"/>): once a second of real
    /// time it takes off a mark whose owner was destroyed without taking it off, was stopped by the guard, or let its
    /// latest time pass, and forgets one that has lost the guider to another feature or the game
    /// (<see cref="GuideMarkBook.Sweep"/>). An owner whose updates stop (its faults, its feature switched off) can no
    /// longer leave its marker on screen. Every mark goes when a city starts (one left from before) and when it unloads.
    ///
    /// The cost: one time comparison a frame; once a second, nothing while no mark is up, a few compares a mark up
    /// otherwise.
    /// </summary>
    public sealed class GuideMarkWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "GuideMarkWatch", typeof(GuideMarkWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private float _next;

        private void Awake()
        {
            Drop("a city is starting");   // (a mark left from the city before: its owner and the guider it was on are gone)
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: GuideMarkWatch.Update");
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
