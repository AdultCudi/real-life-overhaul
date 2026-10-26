namespace RealLifeOverhaul.Construction
{
    /// <summary>Which notice an opening day is owed at an hour.</summary>
    public enum OpeningNotice
    {
        None,
        Eve,       // the evening before: tomorrow is the opening
        Morning    // the morning of the day: today is the opening
    }

    /// <summary>
    /// A building's opening day (docs/features/construction.md, "The opening"), game-free so EconomyCheck holds it: the day after
    /// a handover, the ribbon cut at its front between ten and six, a notice the evening before and on the morning, and what an
    /// opening without the player costs (the city's regard for them rises by the quiet opening's share, not the ribbon's).
    /// What the opening gives is unchanged: the ribbon's moment on Flare, the paper's story and the lift in the company's
    /// reputation come only with the ribbon.
    /// COST: nothing per frame; asked once an hour.
    /// </summary>
    public static class OpeningRules
    {
        public const int From = 10;            // the hour the ribbon may be cut from
        public const int To = 18;              // the hour an opening is held without the player
        public const int MorningHour = From - 2;   // the morning's reminder: two hours before the ribbon may be cut
        public const int EveHour = 18;         // the evening before's reminder
        public const int RibbonStanding = 3;   // points of Standing for the ribbon cut
        public const int QuietStanding = 1;    // points of Standing for an opening held without the player

        /// <summary>The notice owed for an opening on day <paramref name="opening"/> at this hour of this day.</summary>
        public static OpeningNotice Notice(int opening, int day, int hour)
        {
            if (opening <= 0)
            {
                return OpeningNotice.None;
            }
            if (day == opening && hour == MorningHour)
            {
                return OpeningNotice.Morning;
            }
            return day == opening - 1 && hour == EveHour ? OpeningNotice.Eve : OpeningNotice.None;
        }

        /// <summary>True when an opening day's hours are over: the opening is held without the player.</summary>
        public static bool Over(int opening, int day, int hour)
        {
            return opening > 0 && (day > opening || (day == opening && hour >= To));
        }

        /// <summary>Points of Standing an opening gives, with the ribbon cut or without.</summary>
        public static int Standing(bool ribbon)
        {
            return ribbon ? RibbonStanding : QuietStanding;
        }

        /// <summary>Points of Standing a missed opening cost against the ribbon's.</summary>
        public static int Lost()
        {
            return RibbonStanding - QuietStanding;
        }
    }
}
