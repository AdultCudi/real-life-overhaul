using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's words, through the mod's one way of reading a text (the game's own localisation, which holds the mod's
    /// keys and the game's): a key's text, a key with its names filled, money in the game's own form, and a whole count.
    /// Called only when a screen is laid out.
    /// </summary>
    public static class UiText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A key's text in the game's language (the mod's keys and the game's alike).</summary>
        public static string T(string key) => FinanceText.T(key);

        /// <summary>A key's text with its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs ?? System.Array.Empty<string>());

        /// <summary>Money as the game writes it.</summary>
        public static string Money(float amount) => FinanceText.Money(amount);

        /// <summary>A whole count, as the game's lists write their counts.</summary>
        public static string Whole(int n) => n.ToString(Inv);

        /// <summary>A text source for a screen's model: a key's text, the second argument left for the checks' English.</summary>
        public static string Words(string key, string english) => T(key);
    }
}
