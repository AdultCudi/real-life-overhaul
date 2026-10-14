using System;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A building's place key as the company writes a place ("street~number", the street escaped as a URI's data, so it
    /// holds no '#' and its number no '~'), and the number read back out of one. Game-free (TalentGame.Place keeps one per
    /// building; TalentGame.Find passes over every building whose number is not the key's on an int compare).
    /// </summary>
    public static class PlaceRules
    {
        /// <summary>The place key of a street and number.</summary>
        public static string Key(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The number of a place key: what follows its last '~', when that is a whole number written as <see cref="Key"/>
        /// writes one. Whenever Key(street, n) is the key, this is true with n; false for a key it cannot read, which is
        /// then compared whole. No string is made.
        /// </summary>
        public static bool Number(string place, out int number)
        {
            number = 0;
            int at = place != null ? place.LastIndexOf('~') : -1;
            if (at < 0 || at == place.Length - 1)
            {
                return false;
            }
            int i = at + 1;
            bool negative = place[i] == '-';
            if (negative && ++i == place.Length)
            {
                return false;
            }
            long value = 0;
            for (; i < place.Length; i++)
            {
                char c = place[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }
                value = value * 10 + (c - '0');
                if (value > 2147483648L)
                {
                    return false;
                }
            }
            value = negative ? -value : value;
            if (value < int.MinValue || value > int.MaxValue)
            {
                return false;
            }
            number = (int)value;
            return true;
        }
    }
}
