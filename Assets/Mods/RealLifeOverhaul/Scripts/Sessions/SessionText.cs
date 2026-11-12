using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>The desk sessions' words: every line the panel, the notices and the tests show, through the mod's one way of filling a text.</summary>
    public static class SessionText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs ?? System.Array.Empty<string>());

        public static string Whole(int n) => n.ToString(Inv);

        /// <summary>Thousandths as a whole per cent, signed: "+12%", "-3%".</summary>
        public static string Signed(int thousandths)
        {
            int pct = thousandths / 10;
            return (pct > 0 ? "+" : string.Empty) + pct.ToString(Inv) + "%";
        }
    }
}
