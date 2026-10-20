namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's warmth after the player says yes to something they asked for: three texts over the following days
    /// (the next day, three days on and six days on: warm words, which hold nothing else back, PartnerVoice.Warm), each a step
    /// further in how it is changing life at home, from the set of lines for what was agreed. Cost: nothing per frame; a
    /// comparison once a game hour in the day.
    /// </summary>
    internal static class PartnerGlad
    {
        private static readonly int[] After = { 1, 3, 6 };   // days after the yes from which each text may come

        // The sets of lines, by what was agreed; the keys are written out in full for the locale check. A set is saved by its first key.
        public static readonly string[] Work = { "reallife_family_glad_work_1", "reallife_family_glad_work_2", "reallife_family_glad_work_3" };
        public static readonly string[] Stop = { "reallife_family_glad_stop_1", "reallife_family_glad_stop_2", "reallife_family_glad_stop_3" };
        public static readonly string[] Retrain = { "reallife_family_glad_retrain_1", "reallife_family_glad_retrain_2", "reallife_family_glad_retrain_3" };
        private static readonly string[][] Sets = { Work, Stop, Retrain };

        /// <summary>A yes to what they asked, with the set of lines that follow it.</summary>
        public static void Start(string[] lines)
        {
            FamilySave s = FamilyState.S;
            s.PGlad = lines[0];
            s.PGladDay = TimeHelper.CurrentDay;
            s.PGladNext = 0;
        }

        /// <summary>The next text of the set once its day has come and the partner may text; nothing once the last is sent or the two are no longer together.</summary>
        public static void Hourly(int day)
        {
            FamilySave s = FamilyState.S;
            string[] lines = SetOf(s.PGlad);
            if (lines == null || !FamilyState.Together || s.PGladNext >= lines.Length || s.PGladNext >= After.Length)
            {
                s.PGlad = null;
                return;
            }
            if (day - s.PGladDay >= After[s.PGladNext])
            {
                PartnerVoice.Warm(lines[s.PGladNext], Args.Us);
                s.PGladNext++;
            }
        }

        private static string[] SetOf(string first)
        {
            for (int i = 0; first != null && i < Sets.Length; i++)
            {
                if (Sets[i][0] == first)
                {
                    return Sets[i];
                }
            }
            return null;
        }
    }
}
