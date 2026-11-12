using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Record keys made into words for a player. The mod files a building or a business by its address key
    /// ("ba:street_fifthavenue|70", and "|t" for an office building's other firms), and those keys reached a screen in
    /// beta 3. Every kit page writes a place through here, and EconomyCheck looks for a key left on any screen. Game-free:
    /// the street's own words come in from the game. Cost: a split and a few compares, when a page is laid out.
    /// </summary>
    public static class UiPlaces
    {
        private static readonly string[] Suffixes = { "avenue", "street", "road", "place", "square", "boulevard", "lane", "drive", "plaza", "park", "way" };

        /// <summary>
        /// True for a text that is a record's key rather than words: an address key, a namespaced game key
        /// ("ba:businesstype_cafe") or one of the mod's own keys left untranslated.
        /// </summary>
        public static bool IsKey(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(' ') >= 0)
            {
                return false;
            }
            if (text.IndexOf('|') >= 0)
            {
                return true;
            }
            int colon = text.IndexOf(':');
            if (colon > 0 && colon < text.Length - 1 && text.IndexOf('_', colon) > colon)
            {
                return true;
            }
            return text.StartsWith("reallife_", StringComparison.Ordinal);
        }

        /// <summary>
        /// "70 Fifth Avenue" from "ba:street_fifthavenue|70" (or "|70|t"), the street by the game's own words when it
        /// has them. Anything else that is a key becomes plain words, so a key never shows.
        /// </summary>
        public static string Address(string key, Func<string, string> street)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            string[] parts = key.Split('|');
            if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return IsKey(key) ? Words(key) : key;
            }
            string name = street != null ? street(parts[0]) : null;
            if (string.IsNullOrEmpty(name) || IsKey(name))
            {
                name = Words(parts[0]);
            }
            return number.ToString(CultureInfo.InvariantCulture) + " " + name;
        }

        /// <summary>
        /// A key the game has no words for, as plain words: its namespace and kind dropped, a street's suffix parted from
        /// its name ("ba:street_fifthavenue" reads "Fifth Avenue"), each word capitalised.
        /// </summary>
        public static string Words(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            string s = key;
            int bar = s.IndexOf('|');
            s = bar >= 0 ? s.Substring(0, bar) : s;
            int colon = s.IndexOf(':');
            s = colon >= 0 ? s.Substring(colon + 1) : s;
            if (s.StartsWith("reallife_", StringComparison.Ordinal))
            {
                s = s.Substring(s.LastIndexOf('_') + 1);
            }
            int kind = s.IndexOf('_');
            if (kind > 0 && kind < s.Length - 1 && (s.StartsWith("street", StringComparison.Ordinal) || s.StartsWith("businesstype", StringComparison.Ordinal)))
            {
                s = s.Substring(kind + 1);
            }
            s = s.Replace('_', ' ');
            foreach (string suffix in Suffixes)
            {
                if (s.Length > suffix.Length && s.EndsWith(suffix, StringComparison.Ordinal) && s[s.Length - suffix.Length - 1] != ' ')
                {
                    s = s.Substring(0, s.Length - suffix.Length) + " " + suffix;
                    break;
                }
            }
            var b = new StringBuilder(s.Length);
            bool start = true;
            foreach (char c in s)
            {
                b.Append(start ? char.ToUpperInvariant(c) : c);
                start = c == ' ';
            }
            return b.ToString();
        }
    }
}
