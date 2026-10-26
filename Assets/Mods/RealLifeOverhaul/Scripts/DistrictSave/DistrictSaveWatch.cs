using System;
using UnityEngine;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The district save's frame: puts back what a save took out if the game's own line that the file is written did not
    /// come, writes the lines the put back had to hold (the log listener writes none), and keeps the mod's save hook last
    /// in the game's save event within a frame of anything that joins it. COST: disabled while there are no places of
    /// the mod's and nothing waits (idle: nothing a frame); otherwise a few field reads and one reference comparison.
    /// </summary>
    public sealed class DistrictSaveWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DistrictSaveWatch", typeof(DistrictSaveWatch));
        private static readonly int HotUpdate = HotSpots.Slot("DistrictSave: DistrictSaveWatch.Update");
        private static DistrictSaveWatch _instance;

        /// <summary>A save took something out, or places of the mod's are there now: the frame looks again.</summary>
        public static void Wake()
        {
            if (_instance != null && !FrameGuard.Off)
            {
                _instance.enabled = true;
            }
        }

        private void Awake()
        {
            _instance = this;
            enabled = DistrictSaveDirector.Holds;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            try
            {
                DistrictSaveDirector.Leave();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults: the game's own line still brings the records back
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
            DistrictSaveDirector.Frame();
            if (!DistrictSaveDirector.Holds)
            {
                enabled = false;
            }
        }
    }
}
