using System;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>What the traffic mend does on one look (<see cref="TrafficMendRules.Decide"/>).</summary>
    public enum MendStep
    {
        None,
        Mend,      // make the traffic a new density manager now
        GiveUp     // mended as often as a city allows and still failing: say so once and leave it
    }

    /// <summary>What the traffic mend remembers between looks, for one city's traffic.</summary>
    public struct MendState
    {
        public int Streak;          // looks in a row in which the density pass failed
        public int Mends;           // new density managers made for this city's traffic
        public float LastMend;      // when the last was made; 0 never
        public bool GaveUp;
    }

    /// <summary>
    /// The traffic mend's rules (Traffic/TrafficMend): when the game's traffic has stopped adding cars for good, and when
    /// it is made a new density manager. The game's traffic adds a car, when it has fewer than it wants, in
    /// GleyTrafficSystem.DensityManager.UpdateVehicleDensity, called from TrafficManager.Update. Once the density manager
    /// it holds has lost its link to the road grid (or is no longer a whole object), that call throws a
    /// NullReferenceException on every frame, before the same Update takes far cars off, turns the lights or moves the
    /// grid with the player: no car is ever added again and the city empties, and nothing in the game puts it right.
    /// Such a pass is told by the game's own log line (<see cref="IsDensityFault"/>); the pass failing in
    /// <see cref="StreakToMend"/> looks in a row, at least <see cref="FaultsInALook"/> times in each, is the traffic broken
    /// for good, never one bad frame. Another kind of fault there (an IndexOutOfRangeException while the player stands
    /// off the road grid) passes when the player is back on it, and is left to the game.
    /// No Unity or game types: checked offline (NavCheck, TrafficMendCheck.cs).
    /// </summary>
    public static class TrafficMendRules
    {
        public const float Interval = 1f;            // seconds between two looks
        public const int FaultsInALook = 3;          // failed passes in one look that count it: one a frame at 3 frames a second and up
        public const int StreakToMend = 2;           // looks in a row: two seconds of failing, never a single bad frame
        public const float Settle = 30f;             // seconds after a mend before another is made, if the pass still fails
        public const int MostMends = 5;              // new density managers for one city's traffic; past that it is left as the game has it

        public const int TypeException = 4;          // UnityEngine.LogType.Exception, as the engine hands a line to its listeners
        public const string NullFault = "NullReferenceException";
        public const string DensityPass = "GleyTrafficSystem.DensityManager.UpdateVehicleDensity";
        public const string TrafficUpdate = "GleyTrafficSystem.TrafficManager.Update";

        /// <summary>
        /// True for the game's log line of a density pass that threw a NullReferenceException: an exception whose text
        /// begins with its name and whose stack has the pass called from TrafficManager.Update. A few comparisons, no
        /// allocation: it is asked of every line the game logs.
        /// </summary>
        public static bool IsDensityFault(int type, string text, string stack)
        {
            if (type != TypeException || text == null || stack == null)
            {
                return false;
            }
            if (!text.StartsWith(NullFault, StringComparison.Ordinal))
            {
                return false;
            }
            int pass = stack.IndexOf(DensityPass, StringComparison.Ordinal);
            return pass >= 0 && stack.IndexOf(TrafficUpdate, pass + DensityPass.Length, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// One look: <paramref name="faults"/> failed passes since the last, while the traffic runs here as this machine's
        /// own (out of doors, not a guest's mirrored traffic). Updates the state and says what to do.
        /// </summary>
        public static MendStep Decide(ref MendState state, int faults, bool running, float now)
        {
            if (!running || faults < FaultsInALook)
            {
                state.Streak = 0;
                return MendStep.None;
            }
            state.Streak++;
            if (state.Streak < StreakToMend || state.GaveUp)
            {
                return MendStep.None;
            }
            if (state.Mends > 0 && now - state.LastMend < Settle)
            {
                return MendStep.None;
            }
            if (state.Mends >= MostMends)
            {
                state.GaveUp = true;
                return MendStep.GiveUp;
            }
            state.Mends++;
            state.LastMend = now;
            state.Streak = 0;
            return MendStep.Mend;
        }

        /// <summary>
        /// The self-check after a mend, on the next look that saw the traffic run: the passes that failed in the look
        /// before the mend against those in the look after it, which must be none.
        /// </summary>
        public static string CheckLine(int before, int after, int carsKept)
        {
            return "Traffic check (density pass): " + before.ToString(CultureInfo.InvariantCulture) + " failed passes in the second before the mend, "
                   + after.ToString(CultureInfo.InvariantCulture) + " in the second after it, wanted 0; "
                   + carsKept.ToString(CultureInfo.InvariantCulture) + " cars kept on the road: "
                   + (after == 0 ? "PASS" : "FAIL, the new density manager fails too");
        }
    }
}
