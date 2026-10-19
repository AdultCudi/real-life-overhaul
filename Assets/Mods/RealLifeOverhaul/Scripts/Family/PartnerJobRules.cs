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
