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
    /// One business's week as the seat rule reads it: the hours it is open, the stations (desks) there are and when each is taken, which of them
    /// the hire may stand at and how well each suits their demands (rank 0 is best). Arrays are kept and made again only when they must grow.
    /// Game-free; cost: a few passes over seven days of twenty-four hours for each station, when a hire is seated, never per frame.
    /// </summary>
    public sealed class SeatSpace
    {
        public const int Days = 7, Hours = 24, Cells = Days * Hours;

        public int Stations;
        public readonly bool[] Open = new bool[Cells];           // [day * 24 + hour] the business is open that hour
        public readonly bool[] DayOpen = new bool[Days];         // the day has an open hour (set by Seal)
        public readonly int[] RunMost = new int[Days];           // scratch: the longest free run on each day over the stations the hire may use
        public readonly int[] Seen = new int[Days];              // scratch: the day sets a plan has tried with one length of shift
        public bool[] Taken = new bool[0];                       // [station * Cells + cell] a shift of somebody stands there
        public bool[] Usable = new bool[0];                      // [station] the hire has the skill and the desk lacks nothing
        public int[] Rank = new int[0];                          // [station] 0 where every wish of the hire for an item is met, else 1

        /// <summary>Makes room for so many stations and clears everything: no hours, nothing taken, nothing usable.</summary>
        public void Size(int stations)
        {
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
    /// (the game's own test of each), and the first that keeps every demand and has a free desk is placed; if none can, the one that breaks the
    /// least. Nothing of another person's is moved: a hire gets only hours at a desk that nobody has. Game-free, the same answer for the same
    /// business and hire. Cost: on an event, a pass over the stations and a few dozen weeks tried; never per frame.
    /// </summary>
    public static class SeatPlan
    {
        public const int Window = 30;       // days after the hire a professional of a tech firm is looked after
        public const int Batch = 20;        // hires seated in one hour at most, so a backlog is worked off over several
        public const int TestMost = 200;    // hires the test in Options seats in one press
        public const int ShiftLeast = 2;    // hours: the shortest shift worth writing
        public const int ShiftMost = 12;    // hours in one shift at most (the game's ScheduleHelper.ShiftLengthCap)
        public const int Tries = 12;        // places tried for weeks that keep every demand, before the best that can be kept is placed
        public const int DayStart = 8;      // the hour a day's shift starts at where the open hours leave room, so the first hire of a business open all day is not put on the night

        public const int Seated = 0, Closed = 1, NoDesk = 2, Full = 3;   // what a plan came to

        /// <summary>How many days a week is tried with, the usual first.</summary>
        public static readonly int[] DayCounts = { 5, 4, 6, 3, 7, 2, 1 };

        /// <summary>True for a hire made within the window: the day hired is known (a game employee's), not in the future, and under <see cref="Window"/> days ago.</summary>
        public static bool Newcomer(int hired, int day)
        {
            return hired > 0 && hired <= day && day - hired <= Window;
        }

        /// <summary>
        /// Plans a week for a hire in a space. Returns <see cref="Seated"/> with the shifts in <paramref name="into"/> (at least seven long) and their
        /// number, or why not: <see cref="Closed"/> (the business has no open hour), <see cref="NoDesk"/> (no desk the hire may use) or
        /// <see cref="Full"/> (desks, but no free run of two hours on any). <paramref name="unmet"/> is the weight of the demands the week breaks.
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
            int bestScore = int.MaxValue, bestMask = 0, bestHours = 0, tried = 0;
            bool bestLate = false, bestShrink = false;
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
                                int n = Nominal(space, mask, hours, late, shrink, into);
                                if (n == 0)
                                {
                                    continue;
                                }
                                int score = judge(into, n);
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestMask = mask;
                                    bestHours = hours;
                                    bestLate = late;
                                    bestShrink = shrink;
                                }
                                if (score == 0 && tried < Tries && Fits(space, mask, hours, shrink))
                                {
                                    tried++;
                                    if (Place(space, into, n, shrink, late) && Enough(weekHours, into, n) && judge(into, n) == 0)   // the start may have moved to find a free desk
                                    {
                                        count = n;
                                        return Seated;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (bestScore == int.MaxValue)
            {
                return Full;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool shrink = attempt == 1 || bestShrink;
                if (attempt == 1 && bestShrink)
                {
                    break;
                }
                int n = Nominal(space, bestMask, bestHours, bestLate, shrink, into);
                if (n > 0 && Place(space, into, n, shrink, bestLate) && Enough(weekHours, into, n))
                {
                    count = n;
                    unmet = judge(into, n);
                    return Seated;
                }
            }
            return Full;
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
                        if (len > length || (len == length && space.Rank[s] < rank))   // the longest shift, then the best-ranked desk, then the first
                        {
                            station = s;
                            from = f;
                            length = len;
                            rank = space.Rank[s];
                            if (rank == 0 && length >= want)
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
