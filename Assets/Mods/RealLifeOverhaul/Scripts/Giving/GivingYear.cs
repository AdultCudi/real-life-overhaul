using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// The game's tax year as the deduction for gifts lives in it, with no game in it. The game bills for a year on the day that divides by the year's length: its daily pass
    /// (GameManager.NewDay: the day goes up, then TaxHelper.RunDaily) works the tax out from the list of deductions and clears that list on that very day. So a year of the
    /// list runs from such a day to the day before the next, and a gift made on any day of it, the last included, is in the list the game reads. Everything giving does about
    /// the year (when it turns, what is claimed and when) is decided here, and the director only calls it. COST: a few operations a call, nothing allocated.
    /// </summary>
    public static class GivingYear
    {
        /// <summary>The first day of the year a day is in: the day the game clears its list (a multiple of the year's length), 0 for the first year.</summary>
        public static int Start(int day, int daysPerYear)
        {
            int length = Math.Max(1, daysPerYear);
            return Math.Max(0, day) / length * length;
        }

        /// <summary>The first day whose sales the year's tax counts: the game sums the sales of the days after the clearing day (its summary for a day is written the day after).</summary>
        public static int IncomeFrom(int start) => Math.Max(0, start) + 1;

        /// <summary>The days whose sales the year's cap is worked out on today: from the first day the tax counts to yesterday. False while there is none yet (or before the first look).</summary>
        public static bool Window(GivingState s, int day, out int from, out int to)
        {
            from = IncomeFrom(s.PeriodStart);
            to = day - 1;
            return s.PeriodStart >= 0 && to >= from;
        }

        /// <summary>
        /// A year start as an earlier version of this feature saved it (one past the clearing day) written as the clearing day, so a save made before keeps its year and its
        /// sums. Any other number stays as it is.
        /// </summary>
        public static int FromSaved(int saved, int daysPerYear) => saved >= 0 && daysPerYear > 1 && saved % daysPerYear == 1 ? saved - 1 : saved;

        /// <summary>
        /// The year turns when the game's does: the sums of a year start again at the clearing day, so a gift made that day is in the new year, as the game's list has it.
        /// False when the year is the same, or when this is the first look (nothing to start again). Called before anything is counted, by a daily pass and by every gift.
        /// </summary>
        public static bool Turn(GivingState s, int day, int daysPerYear)
        {
            int start = Start(day, daysPerYear);
            if (start == s.PeriodStart)
            {
                return false;
            }
            bool first = s.PeriodStart < 0;
            s.PeriodStart = start;
            if (first)
            {
                return false;
            }
            s.TaxGifts = 0;
            s.TaxClaimed = 0;
            s.NeighbourYear = 0;
            s.NeighbourHelped = 0;
            for (int i = 0; i < s.Books.Length; i++)
            {
                CharityBook b = s.Books[i];
                b.PlayerYear = 0;
                b.OthersYear = 0;
                b.PaidYear = 0;
                b.PeopleYear = 0;
            }
            for (int i = 0; i < s.RivalBooks.Length; i++)
            {
                s.RivalBooks[i].Year = 0;
            }
            return true;
        }

        /// <summary>True for a name in the game's list of deductions that is giving's: the key the game translates for the statement, or the words of the language in use.</summary>
        public static bool Ours(string name, string key, string words)
        {
            return !string.IsNullOrEmpty(name) && (string.Equals(name, key, StringComparison.Ordinal) || string.Equals(name, words, StringComparison.Ordinal));
        }

        /// <summary>
        /// What is already claimed this year: what the game's list holds under giving's name, but never less than what giving itself recorded claiming. The game's list is read
        /// by the name it holds, which a change of language can leave behind, so its reading can fall short of what is in it; giving's own record cannot.
        /// </summary>
        public static long Claimed(GivingState s, long gameHolds) => Math.Max(Math.Max(0L, s.TaxClaimed), Math.Max(0L, gameHolds));

        /// <summary>
        /// The claim to make now, and it is recorded as made: the year's gifts held to the cap on the income so far, less what is claimed already (<see cref="Claimed"/>). Never
        /// negative, and never a claim taken back: if the cap falls (the game's sums are only ever added to, but nothing here depends on it), nothing more is claimed.
        /// </summary>
        public static long Claim(GivingState s, long gameHolds, long income)
        {
            long already = Claimed(s, gameHolds);
            long due = GivingRules.DeductionDue(s.TaxGifts, already, income);
            s.TaxClaimed = already + due;
            return due;
        }

        /// <summary>
        /// Today's claim, made: the cap is worked out on the sales of the days gone this year, what the game's list holds is read, <see cref="Claim"/> says what is due and
        /// <paramref name="deduct"/> gives it to the game's tax. Nothing before there are gifts or before the first look. Returns what was claimed.
        /// </summary>
        public static long ClaimToday(GivingState s, int day, Func<int, int, long> income, Func<long> gameHolds, Action<long> deduct)
        {
            if (s.TaxGifts <= 0 || s.PeriodStart < 0)
            {
                return 0L;
            }
            long sales = income != null && Window(s, day, out int from, out int to) ? income(from, to) : 0L;
            long due = Claim(s, gameHolds != null ? gameHolds() : 0L, sales);
            if (due > 0)
            {
                deduct?.Invoke(due);
            }
            return due;
        }
    }
}
