using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan's operations on the kit, their rules game-free and checked offline: where a shift dragged onto a lane lands
    /// among the shifts already there, by the game's own rule for its drag (WorkShiftDrag.AdjustHoursForOverlap), how far a
    /// shift's end may be pulled before it meets another (WorkShiftHelper.CalculateHandleRange), the day's opening hours by
    /// the hour from its slots, the hour a lane offers for a new shift (where the game shows its add mark), and the words of
    /// the opening hours. Every sum is the game's, copied whole, so the kit's drag and the game's agree to the hour; the edit
    /// itself always goes through the game's ScheduleHelper. Also the figures the pages show beside the game's own: pieces a
    /// day from the game's week, how long a stock lasts in tints, how often a machine makes one, a free window and the hours
    /// a person has left. Cost: a few sums a call, at a press or a layout only.
    /// </summary>
    public static class BizOpsRules
    {
        public const int Hours = 24;   // the game's schedule runs a day of 24 hours
        public const int LastsShort = 3, LastsLow = 5;   // days of stock: under the first in the danger's colour, under the second the warning's
        public const int LaneSearchFrom = 8;   // stations from which the lanes' search shows with nothing typed in it: more than a board holds at 1080

        /// <summary>Tenths of a piece a day from a count over the game's week of seven days, rounded half up; 0 for none.</summary>
        public static int TenthsADay(int week) => week <= 0 ? 0 : (int)((week * 20L + 7) / 14);

        /// <summary>A figure in tenths as the pages show it: one decimal under ten ("2.4", "3"), whole from there ("12").</summary>
        public static string Tenths(int tenths)
        {
            if (tenths <= 0)
            {
                return "0";
            }
            if (tenths >= 100)
            {
                return ((tenths + 5) / 10).ToString(CultureInfo.InvariantCulture);
            }
            return (tenths / 10).ToString(CultureInfo.InvariantCulture) + (tenths % 10 != 0 ? "." + (tenths % 10).ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>A count over the game's week as pieces a day, in figures (see <see cref="Tenths"/>).</summary>
        public static string PerDay(int week) => Tenths(TenthsADay(week));

        /// <summary>
        /// The tint of how long a stock lasts, by the game's days until empty (-1 for none left, int.MaxValue for never): none
        /// left or under three days in the danger's colour, under five the warning's, else none.
        /// </summary>
        public static UiRole LastsTone(int days)
        {
            if (days == int.MaxValue)
            {
                return UiRole.None;
            }
            return days < LastsShort ? UiRole.DangerText : days < LastsLow ? UiRole.WarnText : UiRole.None;
        }

        /// <summary>Tenths of a second (of the game's clock) a machine takes for one piece at so many an hour, rounded; 0 for none.</summary>
        public static int TenthsEach(int perHour) => perHour <= 0 ? 0 : (36000 + perHour / 2) / perHour;

        /// <summary>
        /// The hours a person is free around an hour of a day, by the hours of their shifts that day (the others' ends not
        /// counted): from the end of the last shift before it, or midnight, to the start of the next, or the day's end.
        /// False where a shift holds the hour itself.
        /// </summary>
        public static bool FreeWindow(int[] starts, int[] ends, int count, int hour, out int from, out int to)
        {
            from = 0;
            to = Hours;
            for (int i = 0; i < count; i++)
            {
                if (hour >= starts[i] && hour < ends[i])
                {
                    from = to = hour;
                    return false;
                }
                if (ends[i] <= hour)
                {
                    from = Math.Max(from, ends[i]);
                }
                else if (starts[i] > hour)
                {
                    to = Math.Min(to, starts[i]);
                }
            }
            return true;
        }

        /// <summary>The hours a person may still work this week under the most their demand allows; -1 where it sets no most.</summary>
        public static int HoursLeft(int most, int assigned) => most <= 0 ? -1 : Math.Max(0, most - assigned);

        /// <summary>
        /// A shift moved to new hours on a lane, moved off the shifts it would overlap there and the person's own shifts, as
        /// the game's drag does (WorkShiftDrag.AdjustHoursForOverlap): its start moves past one it starts inside, its end back
        /// to one it ends inside, and one it covers whole cuts it on the side the pointer let go of. The others are given by
        /// their hours, the moved shift itself left out. A start at or past the end means it does not fit.
        /// </summary>
        public static void MoveOff(int[] starts, int[] ends, int count, int released, ref int start, ref int end)
        {
            for (int i = 0; i < count; i++)
            {
                int s = starts[i], e = ends[i];
                if (In(s, e, start))
                {
                    start = e;
                }
                if (In(s, e, end))
                {
                    end = s;
                }
                int first = FirstOverlap(s, e, start, end);
                if (first != -1)
                {
                    int last = LastOverlap(s, e, start, end);
                    if (released < s)
                    {
                        end = first;
                    }
                    else
                    {
                        start = last + 1;
                    }
                }
            }
        }

        /// <summary>
        /// How far a shift's start (or end) may be pulled, as the game's handle allows (WorkShiftHelper.CalculateHandleRange):
        /// a start no earlier than the end of the nearest shift before it and at least an hour before its own end; an end no
        /// later than the start of the nearest shift after it and at least an hour after its own start. The others include
        /// the lane's shifts and the person's, the shift itself among them as the game lists it.
        /// </summary>
        public static void HandleRange(bool start, int shiftStart, int shiftEnd, int[] starts, int[] ends, int count, out int lo, out int hi)
        {
            if (start)
            {
                lo = 0;
                for (int i = 0; i < count; i++)
                {
                    if (starts[i] != shiftStart && ends[i] <= shiftStart)
                    {
                        lo = Math.Max(lo, ends[i]);
                    }
                }
                hi = shiftEnd - 1;
                return;
            }
            hi = Hours;
            for (int i = 0; i < count; i++)
            {
                if (ends[i] != shiftEnd && starts[i] >= shiftEnd)
                {
                    hi = Math.Min(hi, starts[i]);
                }
            }
            lo = shiftStart + 1;
        }

        /// <summary>The day's opening hours by the hour, from its slots (start inclusive, end exclusive); all closed on a closed day.</summary>
        public static bool[] OpenHours(bool open, int[] slotStarts, int[] slotEnds, int count)
        {
            var hours = new bool[Hours];
            if (!open)
            {
                return hours;
            }
            for (int i = 0; i < count; i++)
            {
                for (int h = Math.Max(0, slotStarts[i]); h < Math.Min(Hours, slotEnds[i]); h++)
                {
                    hours[h] = true;
                }
            }
            return hours;
        }

        /// <summary>
        /// The hour a lane offers a new shift at, where the game shows its add mark (ScheduleCellView): a cleaning station's
        /// first free hour, otherwise the first hour that opens a stretch of opening hours and is free on the lane, or the
        /// first open hour after a shift ends; -1 when the lane has no such hour.
        /// </summary>
        public static int OfferHour(bool[] open, bool[] taken, bool cleaning)
        {
            for (int h = 0; h < Hours; h++)
            {
                bool free = !taken[h], after = h > 0 && taken[h - 1];
                if (cleaning ? free : free && open[h] && (h == 0 || !open[h - 1] || after))
                {
                    return h;
                }
            }
            return -1;
        }

        /// <summary>Whether a new shift may start at an hour on a lane: free there, and open (a cleaning station keeps every hour).</summary>
        public static bool CanStart(bool[] open, bool[] taken, bool cleaning, int hour) =>
            hour >= 0 && hour < Hours && !taken[hour] && (cleaning || open[hour]);

        /// <summary>The first and last of each run of open hours, for the words of the opening hours; returns how many runs.</summary>
        public static int Runs(bool[] open, int[] from, int[] to)
        {
            int n = 0;
            for (int h = 0; h < Hours && n < from.Length; h++)
            {
                if (open[h] && (h == 0 || !open[h - 1]))
                {
                    int e = h;
                    while (e < Hours && open[e])
                    {
                        e++;
                    }
                    from[n] = h;
                    to[n] = e;
                    n++;
                }
            }
            return n;
        }

        private static bool In(int s, int e, int hour) => hour >= s && hour < e;

        private static int FirstOverlap(int s, int e, int from, int to)
        {
            for (int h = from; h < to; h++)
            {
                if (In(s, e, h))
                {
                    return h;
                }
            }
            return -1;
        }

        private static int LastOverlap(int s, int e, int from, int to)
        {
            for (int h = to - 1; h >= from; h--)
            {
                if (In(s, e, h))
                {
                    return h;
                }
            }
            return -1;
        }
    }
}
