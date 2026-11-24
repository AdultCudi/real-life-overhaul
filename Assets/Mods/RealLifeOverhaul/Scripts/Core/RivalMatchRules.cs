using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "Match rivals' offers automatically": when an offer from a rival is within the limit of the option that agrees raises by
    /// itself, it is matched the moment it is made, and the day's matches are told in one notice. Shared by the Company's staff
    /// (Economy/Company) and the professionals (Tech), which answer an offer by the same rule they always did: matched, they are
    /// paid what the rival offered. Game-free: EconomyCheck runs it. No cost a frame: a few comparisons when an offer is made.
    /// </summary>
    public static class RivalMatchRules
    {
        public const int WeekHours = 40;   // hours a week that a month's cost of an hourly wage is priced over, for a professional and for staff alike

        /// <summary>The limit, per cent of the pay, held to its ends (the option that agrees raises by itself, 0 is off).</summary>
        public static int Limit(int option, int most)
        {
            return Math.Max(0, Math.Min(option, most));
        }

        /// <summary>
        /// Whether an offer of <paramref name="offer"/> an hour is within <paramref name="limitPercent"/> of what the person is paid
        /// (<paramref name="paid"/>, the same unit). 0 is off. An offer under the pay is within any limit that is on.
        /// </summary>
        public static bool WithinOffer(long paid, long offer, int limitPercent)
        {
            return limitPercent > 0 && offer > 0 && Math.Max(0L, offer - paid) * 100 <= Math.Max(1L, paid) * limitPercent;
        }

        /// <summary>The same for an offer given as basis points over the pay (a professional's).</summary>
        public static bool WithinBp(int bp, int limitPercent)
        {
            return limitPercent > 0 && bp > 0 && bp <= limitPercent * 100;
        }

        /// <summary>The hours a month's cost of an hourly wage is priced over: a 40 hour week for the days of a month of the save's calendar.</summary>
        public static long HoursInMonth(int monthDays)
        {
            return Math.Max(1L, WeekHours * (long)Math.Max(1, monthDays) / 7);
        }

        /// <summary>What an hourly wage costs in a month, in the unit it is given in.</summary>
        public static long Monthly(long hourly, int monthDays)
        {
            return hourly * HoursInMonth(monthDays);
        }
    }

    /// <summary>
    /// The day's matches of rivals' offers added up, so that one notice says them all and none comes for a person. Offers are
    /// matched in the night's pass of each part, so the notice is due from the first hour of the day. Game-free.
    /// </summary>
    public sealed class RivalMatchTally
    {
        private int _day = -1, _count;
        private long _monthly;

        /// <summary>Notes a match made on <paramref name="day"/>, costing <paramref name="monthly"/> a month more (the unit the caller counts in).</summary>
        public void Add(int day, long monthly)
        {
            if (_count > 0 && day != _day)
            {
                _count = 0;
                _monthly = 0;   // one of an earlier day that was never told (a part that matches was off all that time): let go, not told as today's
            }
            _day = day;
            _count++;
            _monthly += Math.Max(0L, monthly);
        }

        /// <summary>The tally as a save keeps it ("v=1|day|count|monthly"); empty with nothing waiting to be told.</summary>
        public string Write()
        {
            return _count > 0 ? "v=1|" + _day.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + _count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + _monthly.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>
        /// Takes the tally from a save's text, whatever was counted before it (another save's matches are never told in this one). Empty, or a line that cannot
        /// be read, leaves nothing waiting.
        /// </summary>
        public void Read(string text)
        {
            _day = -1;
            _count = 0;
            _monthly = 0;
            string[] x = (text ?? string.Empty).Split('|');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (x.Length >= 4 && x[0] == "v=1" && int.TryParse(x[1], System.Globalization.NumberStyles.Integer, inv, out int day)
                && int.TryParse(x[2], System.Globalization.NumberStyles.Integer, inv, out int count) && long.TryParse(x[3], System.Globalization.NumberStyles.Integer, inv, out long monthly)
                && count > 0)
            {
                _day = day;
                _count = count;
                _monthly = Math.Max(0L, monthly);
            }
        }

        /// <summary>True, with what to say, once only: from the first hour of the day the matches were made on, or on any later day.</summary>
        public bool Take(int day, int hour, out int count, out long monthly)
        {
            count = 0;
            monthly = 0;
            if (_count == 0 || (day == _day && hour < 1))
            {
                return false;
            }
            count = _count;
            monthly = _monthly;
            _count = 0;
            _monthly = 0;
            return true;
        }
    }
}
