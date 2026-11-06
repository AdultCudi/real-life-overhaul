namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A post's working week fitted to its workplace's opening hours; game-free, checked offline (EconomyCheck ShiftOpenChecks).
    /// A profession's catalogue week (ProCatalogue) is only the week it would like: the business that employs the player keeps
    /// its own opening hours, and the game shuts its door outside them (BuildingHelper.CanEnterBuilding), so a day that began
    /// while the place was shut could not be worked and was counted as missed. The warehouse's 06:00 start at a wholesaler that
    /// opens at 08:00 got players sacked that way. So every post's week is kept inside its place's hours: no shift starts
    /// before the place opens or runs past its close, as the game's own staff are rostered (ScheduleDay.workShifts lie inside
    /// its slots). No post is staffed before opening. A week is seven masks, Monday first, bit h set while the place is open
    /// over hour h; a place with no hours the game keeps (the mod's own buildings, a building with no business) is not fitted.
    /// Asked once a game day for each post (at most 64): a few thousand bit tests, nothing allocated.
    /// </summary>
    public static class OpenHoursRules
    {
        public const int LeastHours = 4;     // game hours: a shorter run of open hours than this makes no working day
        public const int AllDays = 127;      // bit 0 Monday to bit 6 Sunday
        public const int AllHours = 0xFFFFFF;   // bit h for hour h of the day

        /// <summary>A working week as one number: the days (bit 0 Monday), the hour it starts and its length. Never 0 for a week with a day in it.</summary>
        public static int Pack(int days, int hour, int hours) => (days & AllDays) | (hour & 31) << 8 | (hours & 31) << 16;

        public static int DaysOf(int shift) => shift & AllDays;

        public static int HourOf(int shift) => shift >> 8 & 31;

        public static int HoursOf(int shift) => shift >> 16 & 31;

        /// <summary>The weekday of a game day, Monday 0 (day 1 of a game is a Monday, as the game counts).</summary>
        public static int Weekday(int day) => ((day - 1) % 7 + 7) % 7;

        /// <summary>True if the week holds any open hour: a place whose hours are known to the game.</summary>
        public static bool Known(int[] open)
        {
            if (open == null || open.Length < 7)
            {
                return false;
            }
            for (int i = 0; i < 7; i++)
            {
                if ((open[i] & AllHours) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a day's mask is open over every hour from <paramref name="hour"/> for <paramref name="hours"/>, within that day.</summary>
        public static bool Covers(int mask, int hour, int hours)
        {
            if (hour < 0 || hours <= 0 || hour + hours > 24)
            {
                return false;
            }
            int want = (1 << hours) - 1 << hour;
            return (mask & want) == want;
        }

        /// <summary>The days of <paramref name="days"/> whose opening hours cover the span.</summary>
        public static int DaysCovered(int days, int hour, int hours, int[] open)
        {
            int got = 0;
            for (int d = 0; d < 7; d++)
            {
                if ((days >> d & 1) != 0 && Covers(open[d], hour, hours))
                {
                    got |= 1 << d;
                }
            }
            return got;
        }

        /// <summary>True if a working week starts or runs on a day while the place is shut: what the fit never gives. A place not known: false.</summary>
        public static bool AnyShut(int shift, int[] open)
        {
            if (!Known(open) || shift == 0)
            {
                return false;
            }
            int days = DaysOf(shift);
            return DaysCovered(days, HourOf(shift), HoursOf(shift), open) != days;
        }

        /// <summary>True if a working day of the week on that weekday starts while the place is shut.</summary>
        public static bool StartsShut(int shift, int[] open, int weekday)
        {
            return Known(open) && (DaysOf(shift) >> weekday & 1) != 0 && (open[weekday] >> HourOf(shift) & 1) == 0;
        }

        /// <summary>
        /// The week of a post at a place with these opening hours: the profession's own if the place is not known, else the
        /// most working hours in a week that lie wholly inside the opening hours, on the profession's own days first, with its
        /// own length and hour where they fit (the nearest start to its hour, a later one first). Days of the week other than
        /// its own only if none of them is open long enough, never more of them than it has. 0 if no day is open for
        /// <see cref="LeastHours"/> in a row: there is no working day to be had there.
        /// </summary>
        public static int Fit(int days, int hour, int hours, int[] open)
        {
            days &= AllDays;
            if (!Known(open) || days == 0 || hours <= 0)
            {
                return Pack(days, hour, hours);
            }
            int want = Count(days);
            int best = Best(days, hour, hours, open, want, days);
            return best != 0 ? best : Best(AllDays, hour, hours, open, want, days);
        }

        private static int Best(int candidates, int hour, int hours, int[] open, int want, int own)
        {
            int best = 0, bestScore = 0;
            int least = hours < LeastHours ? hours : LeastHours;
            for (int len = hours > 24 ? 24 : hours; len >= least; len--)
            {
                for (int k = 0; k < 24; k++)
                {
                    for (int side = 0; side < (k == 0 ? 1 : 2); side++)
                    {
                        int start = side == 0 ? hour + k : hour - k;   // a later start first: a place opens later more often than it shuts early
                        if (start < 0 || start + len > 24)
                        {
                            continue;
                        }
                        int got = DaysCovered(candidates, start, len, open);
                        if (got == 0)
                        {
                            continue;
                        }
                        int kept = Trim(got, want, own);
                        int score = Count(kept) * len;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = Pack(kept, start, len);
                        }
                    }
                }
            }
            return best;
        }

        // At most `want` days, the profession's own first, each from Monday.
        private static int Trim(int got, int want, int own)
        {
            int kept = 0, n = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int d = 0; d < 7 && n < want; d++)
                {
                    bool mine = (own >> d & 1) != 0;
                    if ((got >> d & 1) != 0 && mine == (pass == 0) && (kept >> d & 1) == 0)
                    {
                        kept |= 1 << d;
                        n++;
                    }
                }
            }
            return kept;
        }

        /// <summary>
        /// True if a street career's rota day nobody worked is excused because the player tried and nobody near was open to work
        /// for (a stocker's or cleaner's shift is worked at a client's, whose doors keep their own hours): the shut doors', never a no-show.
        /// </summary>
        public static bool RotaShutExcuses(int shutDay, int day) => shutDay > 0 && shutDay == day;

        /// <summary>True if a rota shift clocked on to after its hour is a late mark: not on a day the player was turned away earlier because nobody near was open.</summary>
        public static bool RotaLateMarks(bool late, int shutDay, int day) => late && !RotaShutExcuses(shutDay, day);

        public static int Count(int days)
        {
            int n = 0;
            for (int d = 0; d < 7; d++)
            {
                n += days >> d & 1;
            }
            return n;
        }
    }
}
