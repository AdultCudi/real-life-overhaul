namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's work when Social keeps them, game-free: their job in People (its locale key) read as Family's working
    /// hours and a day's pay. A job Family knows keeps its own hours (a nurse's shifts, a cook's evenings, a barista's
    /// mornings); the rest work office hours; a retired partner does not work and brings in nothing. Nothing here is read
    /// per frame.
    /// </summary>
    public static class PartnerJobRules
    {
        public const string RetiredKey = "reallife_social_job_retired";
        public const int OfficeHours = 3;   // a trade of Family's with office hours (Partner.Trades: the designer), for the timetable only

        // Social's jobs (SocialCatalogue.Jobs), the trade of Family's whose hours they keep, and what a day of it brings in.
        private static readonly string[] Keys =
        {
            "reallife_social_job_barista", "reallife_social_job_nurse", "reallife_social_job_teacher", "reallife_social_job_mechanic", "reallife_social_job_cook",
            "reallife_social_job_accountant", "reallife_social_job_bartender", "reallife_social_job_cabbie", "reallife_social_job_designer",
            "reallife_social_job_electrician", "reallife_social_job_librarian", "reallife_social_job_courier", "reallife_social_job_trainer",
            "reallife_social_job_florist", "reallife_social_job_reporter"
        };

        private static readonly int[] Trades = { 9, 0, 1, 4, 2, 5, 2, OfficeHours, 3, OfficeHours, 6, OfficeHours, OfficeHours, OfficeHours, OfficeHours };
        private static readonly int[] Pay = { 110, 210, 180, 190, 160, 260, 150, 160, 230, 220, 140, 130, 150, 140, 190 };

        /// <summary>Whether the job is no job: retired.</summary>
        public static bool Retired(string key) => key == RetiredKey;

        /// <summary>The trade of Family's whose hours the job keeps; <paramref name="fallback"/> for a job it does not know.</summary>
        public static int TradeOf(string key, int fallback)
        {
            int i = System.Array.IndexOf(Keys, key);
            return i >= 0 ? Trades[i] : fallback;
        }

        /// <summary>What a day of the job brings in: nothing retired, <paramref name="fallback"/> for a job it does not know.</summary>
        /// <summary>Asleep at that hour by People's own hours: a night owl from two to ten, anyone else from eleven to eight (SocialPerson.Asleep).</summary>
        public static bool Asleep(int hour, bool nightOwl) => nightOwl ? hour >= 2 && hour < 10 : hour >= 23 || hour < 8;

        /// <summary>
        /// The working stretch of a weekday from People's free hours: the longer of the busy hours between waking and being free
        /// and those between being free and going to sleep. Office hours (free 17 to 23) are 8 to 17; a night owl free 11 to 18
        /// works 18 to 2 (26: past midnight runs on).
        /// </summary>
        public static void Work(int freeFrom, int freeTo, bool nightOwl, out int from, out int to)
        {
            int wake = nightOwl ? 10 : 8, sleep = nightOwl ? 26 : 23;
            if (freeFrom - wake >= sleep - freeTo)
            {
                from = wake;
                to = freeFrom;
            }
            else
            {
                from = freeTo;
                to = sleep;
            }
        }

        /// <summary>Whether the hour is in a stretch that may run past midnight (to over 24).</summary>
        public static bool Within(int hour, int from, int to) => (hour >= from && hour < to) || (hour + 24 >= from && hour + 24 < to);

        public static int IncomeOf(string key, int fallback)
        {
            if (Retired(key))
            {
                return 0;
            }
            int i = System.Array.IndexOf(Keys, key);
            return i >= 0 ? Pay[i] : fallback;
        }
    }
}
