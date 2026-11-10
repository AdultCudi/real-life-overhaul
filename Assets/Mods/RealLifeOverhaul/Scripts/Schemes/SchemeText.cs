using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>The schemes' words: every line their pages, calls and notices show, through the mod's one way of filling a text. Keys are written out whole for the checks.</summary>
    public static class SchemeText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>What each kind of scheme is called where a file or a notice names it.</summary>
        public static readonly string[] KindKeys =
        {
            "reallife_scheme_kind_claim", "reallife_scheme_kind_expense", "reallife_scheme_kind_books", "reallife_scheme_kind_club", "reallife_scheme_kind_wash",
            "reallife_scheme_kind_graft"
        };

        /// <summary>How a trail stands, in a word: calm, talked about, watched.</summary>
        public static readonly string[] BandKeys = { "reallife_scheme_band_calm", "reallife_scheme_band_talk", "reallife_scheme_band_watched" };

        /// <summary>What a favour is called on its button.</summary>
        public static readonly string[] FavourKeys =
        {
            "reallife_scheme_favour_word", "reallife_scheme_favour_gift", "reallife_scheme_favour_access", "reallife_scheme_favour_vote",
            "reallife_scheme_favour_investigator", "reallife_scheme_favour_judge"
        };

        /// <summary>What a favour is, what it costs and what it risks, said before the money moves.</summary>
        public static readonly string[] FavourAskKeys =
        {
            "reallife_scheme_ask_word", "reallife_scheme_ask_gift", "reallife_scheme_ask_access", "reallife_scheme_ask_vote", "reallife_scheme_ask_investigator",
            "reallife_scheme_ask_judge"
        };

        /// <summary>What they say when they take it.</summary>
        public static readonly string[] FavourTakenKeys =
        {
            "reallife_scheme_taken_word", "reallife_scheme_taken_gift", "reallife_scheme_taken_access", "reallife_scheme_taken_vote",
            "reallife_scheme_taken_investigator", "reallife_scheme_taken_judge"
        };

        /// <summary>The paper's pieces about a rival: looked into, found out, convicted, cleared (SchemeRules.RivalNews, from 1).</summary>
        public static readonly string[] RivalPaperKeys =
        {
            "reallife_scheme_news_rival_letter", "reallife_scheme_news_rival_found", "reallife_scheme_news_rival_convicted", "reallife_scheme_news_rival_cleared"
        };

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs ?? System.Array.Empty<string>());

        public static string Money(long amount) => FinanceText.Money(amount);

        public static string Whole(long n) => n.ToString(Inv);

        /// <summary>Thousandths as a whole per cent: "25%".</summary>
        public static string Percent(int thousandths) => (thousandths / 10).ToString(Inv) + "%";

        public static string Kind(SchemeKind kind) => T(KindKeys[SchemeCatalogue.Clamp((int)kind, 0, KindKeys.Length - 1)]);
    }

    /// <summary>What a press came to: a text's key and what fills it, for a notice of the game's or, in words, for a page.</summary>
    public struct SchemeSaid
    {
        public string Key;
        public string[] Pairs;

        public string Words => SchemeText.Fill(Key, Pairs);

        public static SchemeSaid Of(string key, params string[] pairs)
        {
            return new SchemeSaid { Key = key, Pairs = pairs };
        }
    }
}
