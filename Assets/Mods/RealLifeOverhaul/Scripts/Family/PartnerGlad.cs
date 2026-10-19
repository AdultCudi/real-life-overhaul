namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's warmth after the player says yes to something they asked for: three texts over the following days
    /// (the next day, three days on, six days on), each a step further in how it is changing life at home, from the set of
    /// lines for what was agreed. Cost: nothing per frame; one comparison once a game day.
    /// </summary>
    internal static class PartnerGlad
    {
        private static readonly int[] After = { 1, 3, 6 };   // days after the yes on which they text

        // The sets of lines, by what was agreed; the keys are written out in full for the locale check. A set is saved by its first key.
        public static readonly string[] Work = { "reallife_family_glad_work_1", "reallife_family_glad_work_2", "reallife_family_glad_work_3" };
        private static readonly string[][] Sets = { Work };

        /// <summary>A yes to what they asked, with the set of lines that follow it.</summary>
        public static void Start(string[] lines)
        {
            FamilySave s = FamilyState.S;
            s.PGlad = lines[0];
            s.PGladDay = TimeHelper.CurrentDay;
        }

        /// <summary>Once a day: the day's text, if one is due; nothing once the last is sent or the two are no longer together.</summary>
        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            string[] lines = SetOf(s.PGlad);
            if (lines == null || !FamilyState.Together)
            {
                s.PGlad = null;
                return;
            }
            int since = day - s.PGladDay;
            for (int i = 0; i < After.Length && i < lines.Length; i++)
            {
                if (since == After[i])
                {
                    FamilyText.Message(FamilyPhone.PartnerContact, lines[i], Args.Us);
                }
            }
            if (since >= After[After.Length - 1])
            {
                s.PGlad = null;
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
