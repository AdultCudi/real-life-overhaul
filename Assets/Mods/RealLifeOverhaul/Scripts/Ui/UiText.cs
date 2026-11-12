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

        /// <summary>
        /// A record's key as the game names the place: the business there by its own name, else the address as the game
        /// writes it ("70 Fifth Avenue"). An office building's other firms ("|t") are the building. Never the key itself.
        /// </summary>
        public static string Place(string key)
        {
            if (!UiPlaces.IsKey(key))
            {
                return key ?? string.Empty;
            }
            try
            {
                if (key.Split('|').Length == 2)
                {
                    string name = Population.PopulationGame.BusinessName(key);
                    if (!string.IsNullOrEmpty(name) && !UiPlaces.IsKey(name))
                    {
                        return name;
                    }
                }
                return UiPlaces.Address(key, Street);
            }
            catch (System.Exception e) when (Guard.Missing(e))
            {
                return UiPlaces.Address(key, null);
            }
        }

        // A street's name in the game's language, as the game's own address line writes it.
        private static string Street(string street)
        {
            return Localizor.LocalizorManager.IsLocalizedKey(street) ? T(street) : null;
        }
    }
}
