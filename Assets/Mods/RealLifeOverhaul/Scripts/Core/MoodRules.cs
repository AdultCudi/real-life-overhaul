using System;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How long one of the mod's moods may run, and how a saved one comes back. The game's happiness modifier has a cap,
    /// maxHoursDuration, which its AddModifier applies to every add (Helpers.HappinessHelper.GetCappedHoursDuration); the
    /// mod used to register its moods with none (-1). Going Public multiplies a positive mood's hours on every add
    /// (Patch_AddModifier_PositiveDurationScale, 100 / its morale tempo: ten times by default), trusting each modifier's
    /// own cap to hold it, and the mod put every saved mood back on load through AddModifier, so each load in a session
    /// multiplied the hours again: a mood of a day showed thousands of days left. Now every mood has a cap taken from its
    /// own catalogue entry (<see cref="Cap"/>), and a saved mood is written back as it was saved, never added again
    /// (<see cref="Kept"/>); a save that already carries a mood past its cap has it trimmed once, on load.
    ///
    /// A mood added again while it runs is refreshed by the game to its length, never added on top (AddModifier without
    /// additiveHours, as the mod always calls it), and now never past its cap however the hours were scaled.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, MoodBuffChecks.cs). Cost: a few integer
    /// operations a mood when the moods are registered and when a save is loaded; nothing per frame.
    /// </summary>
    public static class MoodRules
    {
        public const int DayHours = 24;    // hours: the game shows up to this many as hours, more as days
        public const int Times = 3;        // a mood may run to three times its own length, as most of the game's own (walked in the park 24 h, capped at 72 h)
        public const int WeekHours = 168;  // hours: the longest a mood of days may run, the game's own longest cap (played golf, a workout plan)

        /// <summary>
        /// The most hours a mood of <paramref name="hours"/> may have left (its maxHoursDuration): three times its length, a
        /// mood of hours staying hours (at most a day) and one of days staying days (at most a week, never under its own
        /// length). A mood with no length of its own (0 or less: until taken off) has no cap (-1, as the game marks one).
        /// </summary>
        public static int Cap(int hours)
        {
            if (hours <= 0)
            {
                return -1;
            }
            long cap = (long)hours * Times;
            if (hours < DayHours)
            {
                return (int)Math.Min(cap, DayHours);
            }
            return (int)Math.Min(cap, Math.Max(WeekHours, hours));
        }

        /// <summary>The hours a saved mood comes back with: what was saved, trimmed to its cap when it is over (a save that grew before this fix).</summary>
        public static int Kept(int saved, int cap)
        {
            return cap > 0 && saved > cap ? cap : saved;
        }

        /// <summary>
        /// One entry of the mod's mood stash ("type=hours", as ModState.BeforeSave writes it). False for anything else, and
        /// for a mood with no hours left: such an entry is not put back.
        /// </summary>
        public static bool TryRead(string entry, out string type, out int hours)
        {
            type = null;
            hours = 0;
            int equals = entry == null ? -1 : entry.IndexOf('=');
            if (equals <= 0 || equals != entry.LastIndexOf('=')
                || !int.TryParse(entry.Substring(equals + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours) || hours <= 0)
            {
                hours = 0;
                return false;
            }
            type = entry.Substring(0, equals);
            return true;
        }
    }
}
