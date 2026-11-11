namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// The texts the schemes name by a number rather than by hand: what each kind is called, how a trail stands, each
    /// favour's three lines, and the paper's pieces about a rival. Keys are written out whole for the checks. Game-free.
    /// </summary>
    public static class SchemeKeys
    {
        /// <summary>What each kind of scheme is called where a file or a notice names it: nobody's own, since the paper says it of a rival too.</summary>
        public static readonly string[] Kinds =
        {
            "reallife_scheme_kind_claim", "reallife_scheme_kind_expense", "reallife_scheme_kind_books", "reallife_scheme_kind_club", "reallife_scheme_kind_wash",
            "reallife_scheme_kind_graft"
        };

        /// <summary>How a trail stands, in a word: calm, talked about, watched (SchemeRules.Band).</summary>
        public static readonly string[] Bands = { "reallife_scheme_band_calm", "reallife_scheme_band_talk", "reallife_scheme_band_watched" };

        /// <summary>What a favour is called on its button.</summary>
        public static readonly string[] Favours =
        {
            "reallife_scheme_favour_word", "reallife_scheme_favour_gift", "reallife_scheme_favour_access", "reallife_scheme_favour_vote",
            "reallife_scheme_favour_investigator", "reallife_scheme_favour_judge"
        };

        /// <summary>The paper's line on the player's own exposure, by kind of scheme.</summary>
        public static readonly string[] NewsOwn =
        {
            "reallife_scheme_news_own_claim", "reallife_scheme_news_own_expense", "reallife_scheme_news_own_books", "reallife_scheme_news_own_club",
            "reallife_scheme_news_own_wash", "reallife_scheme_news_own_graft"
        };

        /// <summary>What a favour is, what it costs and what it risks, said before the money moves.</summary>
        public static readonly string[] Asks =
        {
            "reallife_scheme_ask_word", "reallife_scheme_ask_gift", "reallife_scheme_ask_access", "reallife_scheme_ask_vote", "reallife_scheme_ask_investigator",
            "reallife_scheme_ask_judge"
        };

        /// <summary>What they say when they take it.</summary>
        public static readonly string[] Taken =
        {
            "reallife_scheme_taken_word", "reallife_scheme_taken_gift", "reallife_scheme_taken_access", "reallife_scheme_taken_vote",
            "reallife_scheme_taken_investigator", "reallife_scheme_taken_judge"
        };

        /// <summary>The paper's pieces about a rival: looked into, found out, convicted, cleared (SchemeRules.RivalNews, from 1).</summary>
        public static readonly string[] RivalPaper =
        {
            "reallife_scheme_news_rival_letter", "reallife_scheme_news_rival_found", "reallife_scheme_news_rival_convicted", "reallife_scheme_news_rival_cleared"
        };
    }
}
