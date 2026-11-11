using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One shift of a new hire's week: the weekday (0 Monday), the desk by its place in the <see cref="SeatSpace"/> (-1 until one is chosen), and the hours, the last not counted.</summary>
    public struct SeatShift
    {
        public int Day, Station, From, To;
    }

    /// <summary>How far a week of shifts is from the hire's demands: the weight of the schedule demands it breaks, 0 when it keeps every one (the game's own test of each).</summary>
    public delegate int SeatJudge(SeatShift[] week, int count);

    /// <summary>
    /// A week that fits the desks, as a plan found it: the days it works (a bit each, 0 Monday), the hours of a shift, early or late in the open hours, shortened to
    /// fit a free run or not, and the weight of the demands it breaks as planned in the open hours alone. <c>Tried</c> is set once it has been placed.
    /// </summary>
    public struct SeatWeek
    {
        public int Mask, Hours, Score;
        public bool Late, Shrink, Tried;
    }

    /// <summary>
    /// One business's week as the seat rule reads it: the hours it is open, the stations (desks) there are and when each is taken, which of them
    /// the hire may stand at and how well each suits their demands (rank 0 is best). Arrays are kept and made again only when they must grow.
    /// Game-free; cost: a few passes over seven days of twenty-four hours for each station, when a hire is seated, never per frame.
    /// </summary>
    public sealed class SeatSpace
    {
        public const int Days = 7, Hours = 24, Cells = Days * Hours;
        public const int WeeksMost = 400;   // the most weeks a plan can find: two passes over seven day counts, two lengths of shift, early and late, and seven starting days (392)

        public int Stations;
        public readonly bool[] Open = new bool[Cells];           // [day * 24 + hour] the business is open that hour
        public readonly bool[] DayOpen = new bool[Days];         // the day has an open hour (set by Seal)
        public readonly int[] RunMost = new int[Days];           // scratch: the longest free run on each day over the stations the hire may use
        public readonly int[] Seen = new int[Days];              // scratch: the day sets a plan has tried with one length of shift
        public readonly SeatWeek[] Weeks = new SeatWeek[WeeksMost];   // scratch: the weeks a plan found that fit the desks, in the order found
        public readonly SeatShift[] Best = new SeatShift[Days];       // scratch: the week placed so far that breaks the least
        public int WeekCount, BestCount, BestBroken;                  // scratch: the weeks found, the shifts of the best, the weight of the demands it breaks
        public bool[] Taken = new bool[0];                       // [station * Cells + cell] a shift of somebody stands there
        public bool[] Usable = new bool[0];                      // [station] the hire has the skill and the desk lacks nothing
        public int[] Rank = new int[0];                          // [station] 0 where every wish of the hire for an item is met, else 1

        /// <summary>Makes room for so many stations and clears everything: no hours, nothing taken, nothing usable.</summary>
        public void Size(int stations)
        {
            WeekCount = 0;
            Stations = Math.Max(0, stations);
            Array.Clear(Open, 0, Open.Length);
            Array.Clear(DayOpen, 0, DayOpen.Length);
            if (Taken.Length < Stations * Cells)
            {
                Taken = new bool[Stations * Cells];
            }
            else
            {
                Array.Clear(Taken, 0, Taken.Length);
            }
            if (Usable.Length < Stations)
            {
                Usable = new bool[Stations];
                Rank = new int[Stations];
            }
            else
            {
                Array.Clear(Usable, 0, Usable.Length);
                Array.Clear(Rank, 0, Rank.Length);
            }
        }

        /// <summary>Notes which days have an open hour; after the hours are set.</summary>
        public void Seal()
        {
            for (int d = 0; d < Days; d++)
            {
                bool any = false;
                for (int h = 0; h < Hours && !any; h++)
                {
                    any = Open[d * Hours + h];
                }
                DayOpen[d] = any;
            }
        }

        /// <summary>A shift of somebody's stands at a station on a day from an hour to an hour (the last not counted): the hours are no longer free there.</summary>
        public void Take(int station, int day, int from, int to)
        {
            if (station < 0 || station >= Stations || day < 0 || day >= Days)
            {
                return;
            }
            for (int h = Math.Max(0, from); h < to && h < Hours; h++)
            {
                Taken[station * Cells + day * Hours + h] = true;
            }
        }

        /// <summary>True if the business is open at the cell and (for a station, not -1) nobody has it.</summary>
        public bool IsFree(int station, int cell)
        {
            return Open[cell] && (station < 0 || !Taken[station * Cells + cell]);
        }
    }

    /// <summary>
    /// Where and when a new hire at a tech firm works, so they are not left without a desk or a shift: the game seats an employee only at a
    /// shift, and nothing gave a hire one (the rota planner refuses a business of over 64 stations or 120 staff, and its managers wrote rotas only
    /// for businesses it reads). Weeks are tried in a fixed order, the usual first (five days of eight hours, then four of ten, six of seven and so
    /// on, the days starting on each weekday, early in the open hours and then late), each asked of the hire's demands through a judge
    /// (the game's own test of each), and the first that keeps every demand and has a free desk is placed; if none can, of the weeks that do fit
    /// the desks the one that breaks the least, as placed. Whether there is a seat never depends on the demands, only on the free hours of the
    /// desks the hire may use: a demand chooses between the weeks that fit, and is broken, and told, when it must be. Nothing of another person's
    /// is moved: a hire gets only hours at a desk that nobody has. Game-free, the same answer for the same business and hire. Cost: on an event,
    /// a pass over the stations and a few dozen weeks tried; never per frame.
    /// </summary>
    public static class SeatPlan
    {
        public const int Batch = 20;        // hires seated in one hour at most (those seated: a hire for whom no desk was free is not counted), so a backlog is worked off over several hours
        public const int Look = 1000;       // professionals looked at for a shift at a desk in one hour at most, so the first hours after a load, with thousands to look at, stay short
        public const int TestMost = 200;    // hires the test in Options seats in one press
        public const int ShiftLeast = 2;    // hours: the shortest shift worth writing
        public const int ShiftMost = 12;    // hours in one shift at most (the game's ScheduleHelper.ShiftLengthCap)
        public const int Tries = 12;        // weeks placed at most for a hire: first those that keep every demand, then the rest from the one that breaks the least
        public const int DayStart = 8;      // the hour a day's shift starts at where the open hours leave room, so a hire of a business open all day gets a day while any desk has one

        public const int Seated = 0, Closed = 1, NoDesk = 2, Full = 3;   // what a plan came to

        /// <summary>How many days a week is tried with, the usual first.</summary>
        public static readonly int[] DayCounts = { 5, 4, 6, 3, 7, 2, 1 };

        /// <summary>
        /// Plans a week for a hire in a space. Returns <see cref="Seated"/> with the shifts in <paramref name="into"/> (at least seven long) and their
        /// number, or why not: <see cref="Closed"/> (the business has no open hour), <see cref="NoDesk"/> (no desk the hire may use) or
        /// <see cref="Full"/> (desks, but the hours free at them hold no week of at least half the hours wanted, whatever the demands).
        /// <paramref name="unmet"/> is the weight of the demands the week breaks.
        /// </summary>
        public static int Plan(SeatSpace space, int weekHours, SeatJudge judge, SeatShift[] into, out int count, out int unmet)
        {
            count = 0;
            unmet = 0;
            space.Seal();
            int open = 0, usable = 0;
            for (int d = 0; d < SeatSpace.Days; d++)
            {
                open += space.DayOpen[d] ? 1 : 0;
            }
            for (int s = 0; s < space.Stations; s++)
            {
                usable += space.Usable[s] ? 1 : 0;
            }
            if (open == 0)
            {
                return Closed;
            }
            if (usable == 0)
            {
                return NoDesk;
            }
            int longest = Runs(space);
            if (longest < ShiftLeast)
            {
                return Full;
            }
            weekHours = Math.Max(ShiftLeast, weekHours);
            int tried = 0;
            space.WeekCount = 0;
            space.BestCount = 0;
            space.BestBroken = int.MaxValue;
            for (int pass = 0; pass < 2; pass++)
            {
                bool shrink = pass == 1;
                for (int c = 0; c < DayCounts.Length; c++)
                {
                    int days = DayCounts[c];
                    if (days > open)
                    {
                        continue;
                    }
                    int round = (weekHours + days / 2) / days;
                    for (int v = 0; v < 2; v++)
                    {
                        int hours = v == 0 ? round : weekHours / days;
                        if (v == 1 && hours == round)
                        {
                            continue;
                        }
                        hours = Math.Min(ShiftMost, Math.Max(ShiftLeast, hours));
                        for (int l = 0; l < 2; l++)
                        {
                            bool late = l == 1;
                            int seen = 0;
                            for (int start = 0; start < SeatSpace.Days; start++)
                            {
                                int mask = DaysFrom(space, start, days);
                                if (mask == 0 || Has(space.Seen, seen, mask))
                                {
                                    continue;
                                }
                                space.Seen[seen++] = mask;
                                if (!Fits(space, mask, hours, shrink))
                                {
                                    continue;   // a day of it has no desk with a free run for the shift: whatever its demands, it cannot be placed
                                }
                                int n = Nominal(space, mask, hours, late, shrink, into);
                                if (n == 0 || !Reaches(space, into, n, weekHours, shrink))
                                {
                                    continue;   // or it would hold under half the hours wanted, as placed
                                }
                                int score = judge(into, n);
                                bool placed = false;
                                if (score == 0 && tried < Tries)
                                {
                                    tried++;
                                    placed = true;
                                    if (Place(space, into, n, shrink, late) && Enough(weekHours, into, n))
                                    {
                                        int broken = judge(into, n);   // the start may have moved to find a free desk
                                        if (broken == 0)
                                        {
                                            count = n;
                                            return Seated;
                                        }
                                        Keep(space, into, n, broken);
                                    }
                                }
                                if (space.WeekCount < SeatSpace.WeeksMost)
                                {
                                    space.Weeks[space.WeekCount++] = new SeatWeek { Mask = mask, Hours = hours, Score = score, Late = late, Shrink = shrink, Tried = placed };
                                }
                            }
                        }
                    }
                }
            }
            return Fallback(space, weekHours, judge, into, out count, out unmet);
        }

        // No week that keeps every demand could be placed: the weeks that fit the desks are placed from the one that breaks the least as planned (the first found
        // of equals), at most Tries of them, and the one that breaks the least as placed, among these and the weeks placed above, is the answer. Full only if no
        // week fits at all, so whether there is a seat never depends on the demands.
        private static int Fallback(SeatSpace space, int weekHours, SeatJudge judge, SeatShift[] into, out int count, out int unmet)
        {
            count = 0;
            unmet = 0;
            for (int attempt = 0; attempt < Tries && space.BestBroken > 0; attempt++)
            {
                int pick = -1;
                for (int k = 0; k < space.WeekCount; k++)
                {
                    if (!space.Weeks[k].Tried && (pick < 0 || space.Weeks[k].Score < space.Weeks[pick].Score))
                    {
                        pick = k;
                    }
                }
                if (pick < 0)
                {
                    break;
                }
                space.Weeks[pick].Tried = true;
                SeatWeek w = space.Weeks[pick];
                int n = Nominal(space, w.Mask, w.Hours, w.Late, w.Shrink, into);
                if (n > 0 && Place(space, into, n, w.Shrink, w.Late) && Enough(weekHours, into, n))
                {
                    Keep(space, into, n, judge(into, n));
                }
            }
            if (space.BestCount == 0)
            {
                return Full;
            }
            Array.Copy(space.Best, into, space.BestCount);
            count = space.BestCount;
            unmet = space.BestBroken;
            return Seated;
        }

        // A week placed that breaks a demand is an answer if no week keeps them all: of those placed, the one that breaks the least is kept (the first of equals).
        private static void Keep(SeatSpace space, SeatShift[] week, int count, int broken)
        {
            if (broken < space.BestBroken)
            {
                space.BestBroken = broken;
                space.BestCount = count;
                Array.Copy(week, space.Best, count);
            }
        }

        // True if a week holds at least half the hours wanted: less is a few shifts and not a seat, and the player is better told that no desk is free.
        private static bool Enough(int weekHours, SeatShift[] week, int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += week[i].To - week[i].From;
            }
            return total * 2 >= weekHours;
        }

        // True if a week planned in the open hours would hold at least half the hours wanted once it is placed. Placed, a shift is as long as planned, or with shrink as
        // long as the longest free run on its day if that is shorter, so this is the total Place will come to and Enough will ask of: a week that cannot reach it is no
        // candidate, and the weeks tried are weeks that can be written.
        private static bool Reaches(SeatSpace space, SeatShift[] week, int count, int weekHours, bool shrink)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                int length = week[i].To - week[i].From;
                total += shrink ? Math.Min(length, space.RunMost[week[i].Day]) : length;
            }
            return total * 2 >= weekHours;
        }

        // The longest free run on each day over the stations the hire may use; the longest of all.
        private static int Runs(SeatSpace space)
        {
            int all = 0;
            for (int d = 0; d < SeatSpace.Days; d++)
            {
                int most = 0;
                for (int s = 0; s < space.Stations; s++)
                {
                    if (!space.Usable[s])
                    {
                        continue;
                    }
                    int run = 0;
                    for (int h = 0; h < SeatSpace.Hours; h++)
                    {
                        if (space.IsFree(s, d * SeatSpace.Hours + h))
                        {
                            run++;
                            most = Math.Max(most, run);
                        }
                        else
                        {
                            run = 0;
                        }
                    }
                }
                space.RunMost[d] = most;
                all = Math.Max(all, most);
            }
            return all;
        }

        // The days of a week: so many days that have an open hour, the first on or after a weekday, going round; 0 if there are not so many.
        private static int DaysFrom(SeatSpace space, int start, int count)
        {
            int mask = 0, got = 0;
            for (int k = 0; k < SeatSpace.Days && got < count; k++)
            {
                int day = (start + k) % SeatSpace.Days;
                if (space.DayOpen[day])
                {
                    mask |= 1 << day;
                    got++;
                }
            }
            return got == count ? mask : 0;
        }

        private static bool Has(int[] list, int count, int mask)
        {
            for (int i = 0; i < count; i++)
            {
                if (list[i] == mask)
                {
                    return true;
                }
            }
            return false;
        }

        // The week with no desk chosen yet: on each day of the mask a shift of the length in the business's open hours alone (the first run that
        // holds it, or the last for a late start; with shrink the longest run, up to the length). 0 if a day cannot hold one.
        private static int Nominal(SeatSpace space, int mask, int hours, bool late, bool shrink, SeatShift[] into)
        {
            int n = 0;
            for (int day = 0; day < SeatSpace.Days; day++)
            {
                if ((mask & (1 << day)) == 0)
                {
                    continue;
                }
                if (!FindRun(space, -1, day, hours, shrink, late, out int from, out int length))
                {
                    return 0;
                }
                into[n++] = new SeatShift { Day = day, Station = -1, From = from, To = from + length };
            }
            return n;
        }

        // True if every day of the mask has, on some desk the hire may use, a free run for the shift (or two hours, with shrink).
        private static bool Fits(SeatSpace space, int mask, int hours, bool shrink)
        {
            for (int day = 0; day < SeatSpace.Days; day++)
            {
                if ((mask & (1 << day)) != 0 && space.RunMost[day] < (shrink ? ShiftLeast : hours))
                {
                    return false;
                }
            }
            return true;
        }

        // Puts a nominal week at desks: each day the best-ranked desk with a free run (the day before's first), the shift moved to that run.
        // Nothing is taken in the space; the caller does that for the week it writes. False if a day finds no desk.
        private static bool Place(SeatSpace space, SeatShift[] week, int count, bool shrink, bool late)
        {
            int keep = -1;
            for (int i = 0; i < count; i++)
            {
                SeatShift w = week[i];
                int want = w.To - w.From, station = -1, from = 0, length = 0, rank = int.MaxValue;
                bool planned = false;
                if (keep >= 0 && FindRun(space, keep, w.Day, want, shrink, late, out int heldFrom, out int heldLength) && heldLength >= want)
                {
                    station = keep;
                    from = heldFrom;
                    length = heldLength;
                }
                else
                {
                    for (int s = 0; s < space.Stations; s++)
                    {
                        if (!space.Usable[s] || !FindRun(space, s, w.Day, want, shrink, late, out int f, out int len))
                        {
                            continue;
                        }
                        bool here = f == w.From;   // free at the hours the week was planned at, which its demands were asked of
                        // the longest shift, then the best-ranked desk, then one free at the planned hours, then the first
                        if (len > length || (len == length && (space.Rank[s] < rank || (space.Rank[s] == rank && here && !planned))))
                        {
                            station = s;
                            from = f;
                            length = len;
                            rank = space.Rank[s];
                            planned = here;
                            if (rank == 0 && length >= want && planned)
                            {
                                break;
                            }
                        }
                    }
                }
                if (station < 0)
                {
                    return false;
                }
                week[i] = new SeatShift { Day = w.Day, Station = station, From = from, To = from + length };
                keep = station;
            }
            return true;
        }

        // Where a shift of so many hours starts in a free run from `h` to `end`: at the day's start (the eighth hour) when the run leaves room for it from there, else at the run's.
        private static int Start(int h, int end, int hours)
        {
            int at = Math.Max(h, DayStart);
            return at + hours <= end ? at : h;
        }

        // A shift of up to `want` hours on a day, in the hours a station is free (station -1: the hours the business is open). Whole `want`
        // hours, from the start of the first run that holds them or, late, the end of the last; with shrink the longest run of two hours or
        // more, up to `want`, the earliest on a tie or, late, the latest.
        private static bool FindRun(SeatSpace space, int station, int day, int want, bool shrink, bool late, out int from, out int length)
        {
            from = 0;
            length = 0;
            int start = day * SeatSpace.Hours, h = 0;
            while (h < SeatSpace.Hours)
            {
                if (!space.IsFree(station, start + h))
                {
                    h++;
                    continue;
                }
                int end = h;
                while (end < SeatSpace.Hours && space.IsFree(station, start + end))
                {
                    end++;
                }
                int run = end - h;
                if (!shrink)
                {
                    if (run >= want)
                    {
                        from = late ? end - want : Start(h, end, want);
                        length = want;
                        if (!late)
                        {
                            return true;
                        }
                    }
                }
                else if (run >= ShiftLeast)
                {
                    int take = Math.Min(run, want);
                    if (take > length || (late && take == length))
                    {
                        length = take;
                        from = late ? end - take : Start(h, end, take);
                    }
                }
                h = end;
            }
            return length > 0;
        }
    }
}
