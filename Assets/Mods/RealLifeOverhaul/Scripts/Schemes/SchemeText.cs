using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>The schemes' words: every line their pages, calls and notices show, through the mod's one way of filling a text. The keys named by a number are in SchemeKeys.</summary>
    public static class SchemeText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs ?? System.Array.Empty<string>());

        public static string Money(long amount) => FinanceText.Money(amount);

        public static string Whole(long n) => n.ToString(Inv);

        /// <summary>Thousandths as a whole per cent: "25%".</summary>
        public static string Percent(int thousandths) => (thousandths / 10).ToString(Inv) + "%";

        public static string Kind(SchemeKind kind) => T(SchemeKeys.Kinds[SchemeCatalogue.Clamp((int)kind, 0, SchemeKeys.Kinds.Length - 1)]);
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
