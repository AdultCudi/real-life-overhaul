using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game-free part of <see cref="SceneryCulling"/> (docs/standards/performance.md, RLO-PERF-023), checked by NavCheck
    /// (SceneryCullCheck.cs): the game's culling list compacted in one pass with its culling group's spheres moved in step,
    /// so the group's count can be set at once and the group never reads an entry the list no longer has, and the words of
    /// the check made as a city unloads.
    /// </summary>
    public static class SceneryCullRules
    {
        /// <summary>The check's line once the city has unloaded, so a search for it finds it.</summary>
        public const string UnloadName = "Culling check (unload)";

        /// <summary>
        /// Takes every entry <paramref name="gone"/> says out of <paramref name="list"/>, keeping the order of the rest, and
        /// moves <paramref name="spheres"/> with it when given (the group reads sphere i for entry i; null while the game has
        /// not made its group). Each entry taken out is handed to <paramref name="dropped"/>, in the list's order. Returns how
        /// many were taken out; the list's count is then the group's new count. One pass, nothing allocated.
        /// </summary>
        public static int Compact<T, TSphere>(List<T> list, TSphere[] spheres, Predicate<T> gone, Action<T> dropped)
        {
            if (list == null || gone == null)
            {
                return 0;
            }
            int count = list.Count;
            int kept = 0;
            for (int read = 0; read < count; read++)
            {
                T item = list[read];
                if (gone(item))
                {
                    dropped?.Invoke(item);
                    continue;
                }
                if (kept != read)
                {
                    list[kept] = item;
                    if (spheres != null && read < spheres.Length)
                    {
                        spheres[kept] = spheres[read];   // kept < read, so it is inside the array too
                    }
                }
                kept++;
            }
            int taken = count - kept;
            if (taken > 0)
            {
                list.RemoveRange(kept, taken);
            }
            return taken;
        }

        /// <summary>True when the unload check passes: nothing the mod destroyed left in the game's culling lists.</summary>
        public static bool UnloadPasses(int dead) => dead == 0;

        /// <summary>
        /// The unload check's line: dead entries in the game's culling lists as the next scene came in (the city's scenes and
        /// its culling still there, every object the mod destroyed gone), out of how many listed; whether the game is to
        /// make its culling group again (a rebuild walks every entry); and how many the mod took out as it destroyed them.
        /// dead below zero: the lists could not be read, so nothing was counted.
        /// </summary>
        public static string UnloadLine(int dead, int listed, bool rebuild, int forgotten, int byGame)
        {
            var line = new StringBuilder(320);
            line.Append(UnloadName).Append(": ");
            if (dead < 0)
            {
                line.Append("not checked, the city's culling lists could not be read (gone before the next scene came in, or kept elsewhere in this game)");
                return line.ToString();
            }
            line.Append(dead).Append(" dead cullables in the game's culling lists (").Append(listed).Append(" listed) as the next scene came in, the city still there; ")
                .Append("a rebuild of its culling group ").Append(rebuild ? "asked for" : "not asked for").Append("; ")
                .Append(forgotten).Append(" taken out by the mod as it destroyed their objects since the start");
            if (byGame > 0)
            {
                line.Append(", ").Append(byGame).Append(" through the game's own way (its fields not found)");
            }
            line.Append(" (wanted: 0 dead): ").Append(UnloadPasses(dead) ? "PASS" : "FAIL");
            return line.ToString();
        }
    }
}
