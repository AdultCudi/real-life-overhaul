using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// What the conversion keeps in a save: the shops that were offices and the player had (rented, owned, or listed for
    /// sale while an office) when the save was written, so that one rented as an office with no business in it yet is an
    /// office again after a load. One line under its own key, and never a building's type: the game's own fields of the
    /// save are left as the game wrote them, so without the mod every building is as the game made it.
    ///
    ///     v=1^ba:street_ninthstreet#13^ba:street_fifthavenue#2
    ///
    /// the version, then a place each (street id and number, as <see cref="ConversionRules.Key"/>), sorted. In a place,
    /// '%' and '^' are written as %25 and %5E. A line of a later version, or one that cannot be read, gives no places
    /// (only the shops with an office business in them are offices at load) and is left in the save untouched until
    /// it is written again. Game-free. Cost: nothing unless a save is loaded or written.
    /// </summary>
    public static class ConversionStore
    {
        public const string Key = "reallife:offices";
        public const int Version = 1;

        public static string Write(ICollection<string> places)
        {
            var sorted = new List<string>(places);
            sorted.Sort(string.CompareOrdinal);   // the same line for the same places
            var t = new StringBuilder(8 + sorted.Count * 32);
            t.Append("v=").Append(Version);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!string.IsNullOrEmpty(sorted[i]))
                {
                    t.Append('^').Append(sorted[i].Replace("%", "%25").Replace("^", "%5E"));
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the places. False if it is of another version or cannot be read: there are then no places.</summary>
        public static bool Read(string text, HashSet<string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] parts = text.Split('^');
            if (parts[0] != "v=" + Version)
            {
                return false;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].IndexOf('#') <= 0)
                {
                    into.Clear();
                    return false;
                }
                into.Add(parts[i].Replace("%5E", "^").Replace("%25", "%"));
            }
            return true;
        }
    }
}
