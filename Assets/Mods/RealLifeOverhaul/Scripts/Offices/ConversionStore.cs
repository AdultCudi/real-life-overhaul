using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// What the conversion keeps in a save: the converted shops the player had when it was written, so one with no business
    /// yet is an office again after a load. A building's type is never saved. One line: "v=1^street#13^street#2", each
    /// place as <see cref="ConversionRules.Key"/>, sorted, '%' and '^' as %25 and %5E; a line of a later version, or
    /// unreadable, gives no places and is kept untouched. Game-free.
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
