using System;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A building's place key ("street~number", the street URI-escaped) and its number read back without making a string. Game-free.</summary>
    public static class PlaceRules
    {
        public static string Key(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The number after a place key's last '~', as <see cref="Key"/> writes it; false for a key it cannot read. No string is made.</summary>
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
