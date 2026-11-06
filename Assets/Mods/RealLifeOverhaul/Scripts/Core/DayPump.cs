using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Plays what the game's midnight left for later: the steps in <see cref="DaySpread.Shared"/>, a slice of
    /// <see cref="SliceMs"/> ms a frame (at least one step), from the frame after the day turned, and the Info lines the
    /// day held (<see cref="DayLines"/>), half a millisecond a frame. Each step is timed under its own name in the
    /// session record. Disabled while nothing waits: it is switched on by the first step or line queued, and writes the
    /// lines still held when the game ends. COST: nothing while idle (disabled); at work a clock read and one list entry
    /// a step.
    /// </summary>
    public sealed class DayPump : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DayPump", typeof(DayPump));   // its Update (Core/Guard)

        /// <summary>Milliseconds of steps a frame plays; a step that is longer is played alone in its frame.</summary>
        public const double SliceMs = 2.0;

        public static IModLogger Logger;

        private static readonly int HotUpdate = HotSpots.Slot("Core: DayPump.Update");
        private static DayPump _instance;

        private void Awake()
        {
            _instance = this;
            DayLines.Begin();
            DaySpread.Shared.Wake = Wake;
            DaySpread.Shared.Faulted = Failed;
            DayLines.Wake = Wake;
            enabled = DaySpread.Shared.Busy || DayLines.Waiting > 0;
        }

        private static void Wake()
        {
            if (_instance != null)
            {
                _instance.enabled = true;
            }
        }

        private static void Failed(string name, Exception e)
        {
            Logger?.Error(e);
            Logger?.Warn("Day: the step '" + name + "' failed and is left out; the rest of the day's steps go on.");
        }

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
            DaySpread steps = DaySpread.Shared;
            int frame = Time.frameCount;
            WorkSlice slice = WorkSlice.Of(SliceMs, 1);
            while (steps.Due(frame) && slice.More())
            {
                using var step = HotSpots.Time(steps.NextSlot);
                steps.PlayOne(frame);
            }
            if (DayLines.Waiting > 0)
            {
                WorkSlice lines = WorkSlice.Of(DayLines.SliceMs, 4);
                DayLines.Write(ref lines);
            }
            if (!steps.Busy && DayLines.Waiting == 0)
            {
                enabled = false;
            }
        }

        private void OnApplicationQuit()
        {
            DayLines.Flush();
        }

        private void OnDestroy()
        {
            DayLines.Flush();
            if (_instance == this)
            {
                _instance = null;
                DaySpread.Shared.Wake = null;
                DaySpread.Shared.Faulted = null;
                DayLines.Wake = null;
            }
        }
    }
}
