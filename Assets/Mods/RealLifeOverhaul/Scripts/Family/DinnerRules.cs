namespace RealLifeOverhaul.Family
{
    /// <summary>Where a family dinner is: at Mom's, or at home with the household.</summary>
    public enum DinnerPlace { Moms = 0, Home = 1 }

    /// <summary>How Mom's table stands today: no dinner at hers, set later today, or set now (her call and Family offer going over).</summary>
    public enum MomsTable { None = 0, Later = 1, Set = 2 }

    /// <summary>
    /// The four family dinners of the game's year (Relatives) and how each is attended: Mom writes the day before, saying how;
    /// on the day a household of the player's own (a partner living in, or children at home) has it at home in the hours
    /// <see cref="HomeFrom"/> to <see cref="HomeTo"/>, begun at the home's door, and anybody else is expected at Mom's, whose
    /// table is set from <see cref="MomsFrom"/>: her own call and the Family contact then offer going over. One table for the
    /// texts, the calls, the Family page and the Guide, so they never disagree. Game-free (EconomyCheck, MomCallChecks.cs).
    /// COST: a little arithmetic, on the day's round, a call or a page.
    /// </summary>
    public static class DinnerRules
    {
        public const int Count = 4;          // dinners a year: spring, summer, Thanksgiving, winter
        public const int MomsFrom = 12;      // hour Mom's table is set: "from midday", as her texts say
        public const int HomeFrom = 15;      // hour a dinner at home can begin...
        public const int HomeTo = 23;        // ...and the hour it is missed at
        public const int HomeLength = 3;     // hours a dinner at home takes
        public const int MomsLength = 4;     // hours a dinner at Mom's takes, there and back

        private static readonly float[] At = { 0.22f, 0.47f, 0.72f, 0.97f };   // of the game's year: each dinner's place in it

        /// <summary>
        /// The day of the year (from 0, as (day - 1) % year counts it) a dinner falls on: never the year's first or last,
        /// so its invitation the day before is in the same year. Rounded as the game's Mathf.RoundToInt rounds.
        /// </summary>
        public static int DayOfYear(int which, int year)
        {
            int days = year < 3 ? 3 : year;
            int on = (int)System.Math.Round(days * At[which < 0 ? 0 : which >= Count ? Count - 1 : which]);
            return on < 1 ? 1 : on > days - 1 ? days - 1 : on;
        }

        /// <summary>Which dinner is on this day (0 to 3), or -1; <paramref name="stamp"/> is the year's count times four and the dinner, unique for each.</summary>
        public static int DinnerOn(int day, int year, out int stamp)
        {
            return Find(day, year, 0, out stamp);
        }

        /// <summary>Which dinner is tomorrow, the day Mom writes to invite the player, or -1; the stamp as <see cref="DinnerOn"/>'s for that dinner.</summary>
        public static int InviteOn(int day, int year, out int stamp)
        {
            return Find(day, year, 1, out stamp);
        }

        private static int Find(int day, int year, int ahead, out int stamp)
        {
            stamp = -1;
            if (day < 1 || year < 3)
            {
                return -1;
            }
            int count = (day - 1) / year;
            int dayOfYear = (day - 1) % year;
            for (int i = 0; i < Count; i++)
            {
                if (dayOfYear + ahead == DayOfYear(i, year))
                {
                    stamp = count * Count + i;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The next dinner from this day on (today's counts), and in how many days it falls.</summary>
        public static int Next(int day, int year, out int inDays)
        {
            inDays = 0;
            if (day < 1 || year < 3)
            {
                return -1;
            }
            int dayOfYear = (day - 1) % year;
            for (int k = 0; k <= Count; k++)
            {
                int which = k % Count;
                int on = DayOfYear(which, year) + k / Count * year;
                if (on >= dayOfYear)
                {
                    inDays = on - dayOfYear;
                    return which;
                }
            }
            return -1;
        }

        /// <summary>Where this year's dinner is, by the household as it is: at home with a partner living in or a child at home.</summary>
        public static DinnerPlace PlaceFor(bool livesInTogether, int childrenAtHome)
        {
            return livesInTogether || childrenAtHome > 0 ? DinnerPlace.Home : DinnerPlace.Moms;
        }

        /// <summary>
        /// Mom's table today: set from <paramref name="from"/> (<see cref="MomsFrom"/>; a test sets it to now) on the dinner's
        /// day, until the player goes over or the day ends.
        /// </summary>
        public static MomsTable Table(int dinnerDay, int today, int hour, int from)
        {
            if (dinnerDay <= 0 || dinnerDay != today)
            {
                return MomsTable.None;
            }
            return hour >= from ? MomsTable.Set : MomsTable.Later;
        }

        /// <summary>The hours a dinner at home is due, for a dinner told at <paramref name="hour"/>: its usual hours, or from now when a test sets it later in the day.</summary>
        public static void HomeHours(int hour, bool fromNow, out int fromHour, out int toHour)
        {
            fromHour = HomeFrom;
            toHour = HomeTo;
            if (!fromNow)
            {
                return;
            }
            int now = hour < 0 ? 0 : hour > 23 ? 23 : hour;
            if (now < fromHour)
            {
                fromHour = now;
            }
            if (now >= toHour)
            {
                fromHour = now;
                toHour = 24;   // the errand is due while the hour is under its end: the last hour of the day still counts
            }
        }
    }
}
