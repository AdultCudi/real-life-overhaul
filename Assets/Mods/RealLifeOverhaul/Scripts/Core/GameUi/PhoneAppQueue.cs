using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// On the game's full menu while apps of the mod's wait to go on the phone: one a frame (PhoneApp.Pump), then their
    /// buttons shown and the pages laid out once. It switches itself off when none waits, so it costs nothing a frame after
    /// the apps are in; while they go on, one install a frame.
    /// </summary>
    public sealed class PhoneAppQueue : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneAppQueue", typeof(PhoneAppQueue));   // its Update
        private static readonly int HotUpdate = HotSpots.Slot("Core: PhoneAppQueue.Update");

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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!PhoneApp.Pump())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            PhoneApp.Drop();   // the full menu goes with the city: nothing waits for it
        }
    }
}
