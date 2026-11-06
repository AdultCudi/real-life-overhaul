using System;
using System.Threading;
using BAModAPI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the game's traffic right when it has stopped adding cars for good. The game's traffic adds a car in its
    /// density pass (GleyTrafficSystem.DensityManager.UpdateVehicleDensity, from TrafficManager.Update); once the density
    /// manager it holds has lost its link to the road grid, that pass throws on every frame before the rest of the same
    /// Update runs, so no car is added again and the city empties, and nothing in the game recovers it. The game's own log
    /// line of each failed pass is counted as it is logged (<see cref="Listen"/>); once a second the count is weighed
    /// (<see cref="TrafficMendRules.Decide"/>), and two seconds of failing make the traffic a new density manager from
    /// its own parts (<see cref="TrafficMendGame.Mend"/>), at most once in 30 s and five times a city. Nothing is done on
    /// a Going Public guest, whose traffic is the host's. Every mend is one warning line; with Developer diagnostics on,
    /// the second after it is a "Traffic check" line. Cost: one comparison a frame; once a second, an exchange and a
    /// few field reads; on each line the game logs, a type comparison (and two text searches for an exception).
    /// </summary>
    public static class TrafficMend
    {
        private static int _faults;          // failed density passes the game logged since the last look; from any thread
        private static bool _listening;
        private static float _next;
        private static MendState _state;
        private static object _traffic;      // the city's traffic manager the state is for
        private static bool _checkDue;       // a mend was made: the next look that sees the traffic run says how it went
        private static int _before;          // failed passes in the look that made it
        private static int _kept;            // cars it kept on the road

        private static readonly Application.LogCallback Heard = OnLog;

        public static IModLogger Logger => TrafficTools.Logger;

        /// <summary>Once, at game start (Core/RealLifeOverhaulMod): the game's log lines are weighed from then on.</summary>
        public static void Listen()
        {
            if (_listening)
            {
                return;
            }
            _listening = true;
            Application.logMessageReceivedThreaded += Heard;
        }

        // From any thread, for every line the game logs. Nothing here may log (it would come back here); a fault drops the line.
        private static void OnLog(string text, string stack, LogType type)
        {
            try
            {
                if (TrafficMendRules.IsDensityFault((int)type, text, stack))
                {
                    Interlocked.Increment(ref _faults);
                }
            }
            catch (Exception)
            {
                // (see above: the line is not counted, the game's log is not touched)
            }
        }

        /// <summary>From TrafficAccidents' frame, with the real clock: one comparison a frame, a look once a second.</summary>
        public static void Tick(float now)
        {
            if (now < _next)
            {
                return;
            }
            _next = now + TrafficMendRules.Interval;
            int faults = Interlocked.Exchange(ref _faults, 0);
            try
            {
                Look(now, faults);
            }
            catch (Exception e)
            {
                // A mend that cannot be made now (the player off the road grid as it was made) is tried again after a while.
                Logger?.Error(e);
                _next = now + TrafficMendRules.Settle;
            }
        }

        private static void Look(float now, int faults)
        {
            object traffic = TrafficMendGame.Current;
            if (!ReferenceEquals(traffic, _traffic))
            {
                _traffic = traffic;
                _state = default;
                _checkDue = false;
            }
            bool running = traffic != null && TrafficMendGame.Running;
            if (_checkDue && running)
            {
                _checkDue = false;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info(TrafficMendRules.CheckLine(_before, faults, _kept));
                }
            }
            switch (TrafficMendRules.Decide(ref _state, faults, running, now))
            {
                case MendStep.Mend:
                    Mend(faults);
                    break;
                case MendStep.GiveUp:
                    Logger?.Warn("Traffic mend: the game's traffic still fails to add cars after " + TrafficMendRules.MostMends
                                 + " new density managers; it is left as the game has it until the next load.");
                    break;
            }
        }

        private static void Mend(int faults)
        {
            if (!TrafficMendGame.Mend(out int kept, out int wanted, out bool heldMissing, out string why))
            {
                Logger?.Warn("Traffic mend: the game's traffic stopped adding cars (" + faults + " failed density passes in a second), but "
                             + why + ": nothing was changed.");
                return;
            }
            _before = faults;
            _kept = kept;
            _checkDue = true;
            Logger?.Warn("Traffic mend: the game's traffic had stopped adding cars (its density pass failed " + faults
                         + " times in the last second with a NullReferenceException; the density manager it held "
                         + (heldMissing ? "was missing" : "had lost its link to the road grid or was no longer whole")
                         + "). It was given a new one made from its own parts: " + kept + " cars kept on the road, " + wanted
                         + " wanted (mend " + _state.Mends + " of " + TrafficMendRules.MostMends + " for this city).");
        }

        /// <summary>
        /// Options > Tests: breaks the traffic's density manager as a player's log showed it, so that the mend is seen to
        /// follow within three seconds and the cars to keep coming. Refused, with the reason, off the street or on a guest.
        /// </summary>
        public static void Test()
        {
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && GameContract.TrafficMend.Off)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Traffic mend", why);
                    return;
                }
                if (!TrafficMendGame.Break())
                {
                    TestRefusals.Tell(Logger, "Traffic mend", TestRefusal.TrafficOff, detail: "the traffic holds no density manager");
                    return;
                }
                Logger?.Info("Traffic mend test: the density manager's link to the road grid cut and its count of cars set to 0; the mend should follow within three seconds.");
                Notifications.Show(NotificationType.Info, "reallife_test_traffic_mend_done", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
