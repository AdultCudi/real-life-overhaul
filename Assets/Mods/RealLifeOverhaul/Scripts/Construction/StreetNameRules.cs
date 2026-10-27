using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A street as a player reads it in build mode and the construction texts (the owner, 27 Oct 2026: the design window said "For
    /// your 20 squares on reallife:street_uptown_second"). A lot keeps its street by id, the mod's own ("reallife:street_...") or the
    /// game's ("ba:street_..."); the name a player reads is the game's own street text (the map and the addresses use it), and where
    /// that is missing or is still an id, a plain word made from the id, so no text ever shows an id. Game-free
    /// (EconomyCheck's BuildModePickerChecks). COST: a few string tests when a text is written; nothing per frame.
    /// </summary>
    public static class StreetNameRules
    {
        /// <summary>What an id of the game's or the mod's begins with, in a text key or in the game's data: never shown to a player.</summary>
        private static readonly string[] IdMarks =
        {
            "reallife:", "reallife_", "ba:street_", "ba:neighborhood_", "ba:buildingtype_", "ba:buildingsize_"
        };

        private const string StreetMark = "street_";   // what a street's id has before its own name
        private const string DistrictPart = "uptown_"; // the district's own streets are "street_uptown_<name>"

        /// <summary>True when a text holds an id of the game's or the mod's (it is not words a player reads).</summary>
        public static bool HasId(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int i = 0; i < IdMarks.Length; i++)
            {
                if (text.IndexOf(IdMarks[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The words for a street: <paramref name="named"/> (the game's street text, or the mod's own) when it is words, else a plain
        /// word made from the id. An empty street gives nothing; a street with no id mark in it is already words and is kept.
        /// </summary>
        public static string Words(string street, string named)
        {
            if (string.IsNullOrEmpty(street))
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(named) && !HasId(named))
            {
                return named;
            }
            return HasId(street) ? Plain(street) : street;
        }

        /// <summary>A street's id as one plain word ("reallife:street_uptown_second" is "Second"): the last resort, when no text names the street.</summary>
        public static string Plain(string street)
        {
            if (string.IsNullOrEmpty(street))
            {
                return string.Empty;
            }
            string name = street;
            int at = name.LastIndexOf(StreetMark, StringComparison.Ordinal);
            if (at >= 0)
            {
                name = name.Substring(at + StreetMark.Length);
            }
            else
            {
                int colon = name.LastIndexOf(':');
                name = colon >= 0 ? name.Substring(colon + 1) : name;
            }
            if (name.StartsWith(DistrictPart, StringComparison.Ordinal))
            {
                name = name.Substring(DistrictPart.Length);
            }
            name = name.Replace('_', ' ').Trim();
            return name.Length == 0 ? string.Empty : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// An address as written: <paramref name="written"/> (the game's, "1010 2nd Street") when it holds no id, else the number and
        /// the street's words, so a street whose text is not there yet still reads as a word.
        /// </summary>
        public static string Address(string street, int number, string written, string named)
        {
            if (!string.IsNullOrEmpty(written) && !HasId(written))
            {
                return written;
            }
            string words = Words(street, named);
            return words.Length == 0 ? string.Empty : number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + words;
        }
    }
}
