using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A name no one in the player's company has: the rules behind the names the tech sector gives its seekers, its founders and
    /// the staff it renames (<see cref="StaffNamesGame"/> reads the names from the game). The game keeps its own list of names
    /// taken in a memory that starts with the hired staff only, so the seekers, the mod's payroll and the candidates waiting
    /// for an answer were never in it, and a name could come up twice. Game-free; no allocation but the name made.
    /// </summary>
    public static class StaffNameRules
    {
        /// <summary>Rolls tried before a middle initial tells two names apart.</summary>
        public const int Tries = 60;

        /// <summary>True for a name that tells somebody apart: not blank, not the "?" a failed roll leaves.</summary>
        public static bool IsName(string name) => !string.IsNullOrWhiteSpace(name) && name != "?";

        /// <summary>
        /// The first roll that <paramref name="taken"/> lacks, put in it. After <see cref="Tries"/> rolls the last one gets a middle
        /// initial, the first letter that is free, so the answer is always one nobody has.
        /// </summary>
        public static string Fresh(Func<string> roll, ISet<string> taken)
        {
            string name = null;
            for (int i = 0; i < Tries; i++)
            {
                name = roll();
                if (IsName(name) && taken.Add(name.Trim()))
                {
                    return name;
                }
            }
            name = IsName(name) ? name.Trim() : "Alex Morgan";
            int space = name.IndexOf(' ');
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                string initialled = space > 0 ? name.Substring(0, space) + " " + letter + ". " + name.Substring(space + 1) : name + " " + letter + ".";
                if (taken.Add(initialled))
                {
                    return initialled;
                }
            }
            return name;
        }

        /// <summary>The places in <paramref name="names"/> that repeat a name an earlier place has (the earlier keeps it), blanks and "?" passed over. Returns how many.</summary>
        public static int Doubles(IList<string> names, List<int> into)
        {
            into.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Count; i++)
            {
                if (IsName(names[i]) && !seen.Add(names[i].Trim()))
                {
                    into.Add(i);
                }
            }
            return into.Count;
        }
    }
}
