using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The rota planner's save line, each business's <see cref="RotaMode"/>: "v=1^1:Main Street~12^2:Harbour Road~4";
    /// '%' and '^' in a place are written %25 and %5E. A business with none is on Auto. Game-free.
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
            places.Sort(string.CompareOrdinal);   // the same line for the same choices
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

        /// <summary>Reads a line into the choices; false, with none, for another version or a bad line.</summary>
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
