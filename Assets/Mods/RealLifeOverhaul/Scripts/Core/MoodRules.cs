using System;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How long one of the mod's moods may run (the game's maxHoursDuration), and how a saved one comes back. Going
    /// Public multiplies a positive mood's hours on every AddModifier and trusts the modifier's cap to hold it, so every
    /// mood has a cap, and a saved mood is written back as saved, never added again. Game-free; nothing per frame.
    /// </summary>
    public static class MoodRules
    {
        public const int DayHours = 24;    // hours: the game shows up to this many as hours, more as days
        public const int Times = 3;        // a mood may run to three times its length, as most of the game's own do
        public const int WeekHours = 168;  // hours: the longest a mood of days may run, the game's own longest cap

        /// <summary>
        /// The most hours a mood of <paramref name="hours"/> may have left: three times its length, at most a day for a
        /// mood of hours and a week (never under its length) for one of days; -1 (no cap) for a mood with no length.
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

        /// <summary>The hours a saved mood comes back with: what was saved, trimmed to its cap.</summary>
        public static int Kept(int saved, int cap)
        {
            return cap > 0 && saved > cap ? cap : saved;
        }

        /// <summary>One "type=hours" entry of the mood stash; false for anything else or for no hours left.</summary>
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
