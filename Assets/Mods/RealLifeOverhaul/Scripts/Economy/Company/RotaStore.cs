using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the staff rota planner keeps in a save: for each business the player has chosen it for, how it weighs
    /// what people ask for against what an hour brings (<see cref="RotaMode"/>). One line under its own key:
    ///
    ///     v=1^1:Main Street~12^2:Harbour Road~4
    ///
    /// the version, then a mode and a place each. A business with no entry is on <see cref="RotaMode.Auto"/>. In a
    /// place, '%' and '^' are written as %25 and %5E. A line of a later version, or one that cannot be read, gives
    /// no entries (every business on Auto) and is left in the save untouched until a choice is made again.
    /// Game-free. Cost: nothing unless a save is loaded or written.
    /// </summary>
    public static class RotaStore
    {
        public const string Key = "reallife:economy_rota";
        public const int Version = 1;

        public static string Write(Dictionary<string, int> modes)
        {
            var t = new StringBuilder(16 + modes.Count * 32);
            t.Append("v=").Append(Version);
            var places = new List<string>(modes.Keys);
            places.Sort(string.CompareOrdinal);     // the same line for the same choices
            for (int i = 0; i < places.Count; i++)
            {
                int mode = modes[places[i]];
                if (mode == RotaMode.StaffFirst || mode == RotaMode.Lean)
                {
                    t.Append('^').Append(mode).Append(':').Append(places[i].Replace("%", "%25").Replace("^", "%5E"));
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the choices. False if it is of another version or cannot be read: there are then no choices.</summary>
        public static bool Read(string text, Dictionary<string, int> into)
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
                int at = parts[i].IndexOf(':'), mode;
                if (at <= 0 || !int.TryParse(parts[i].Substring(0, at), out mode) || (mode != RotaMode.StaffFirst && mode != RotaMode.Lean) || at + 1 >= parts[i].Length)
                {
                    into.Clear();
                    return false;
                }
                into[parts[i].Substring(at + 1).Replace("%5E", "^").Replace("%25", "%")] = mode;
            }
            return true;
        }
    }
}
