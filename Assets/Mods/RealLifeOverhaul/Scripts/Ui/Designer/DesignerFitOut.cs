using System;
using System.Collections.Generic;
using RealLifeOverhaul.Designs;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The furniture the mod's own fit-outs are made of, for the designer's "RLO fit-out" tab and the badge on its tiles. The mod adds no furniture of its own to the
    /// game's catalogue (it registers no item), so the set is the game's pieces that the mod's ready-made designs keep (Scripts/Designs/DesignCatalogue.cs: what a
    /// Car Showroom and a bank branch are made of), by the game's item names. The tab lists those of them the designer can place in this business. Game-free.
    /// </summary>
    public static class DesignerFitOut
    {
        private static readonly HashSet<string> Names = Build();

        private static HashSet<string> Build()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            Add(set, DesignCatalogue.ShowroomKeeps);
            Add(set, DesignCatalogue.BranchExtras);
            return set;
        }

        private static void Add(HashSet<string> set, string[] bare)
        {
            for (int i = 0; i < bare.Length; i++)
            {
                set.Add(DesignCatalogue.ItemPrefix + bare[i]);
            }
        }

        /// <summary>How many pieces the mod's fit-outs are made of, whether or not a business can place them.</summary>
        public static int Size => Names.Count;

        /// <summary>True for a piece the mod's fit-outs are made of.</summary>
        public static bool Is(string itemName) => itemName != null && Names.Contains(itemName);

        /// <summary>The pieces of a list the designer offers that are the mod's, in the list's order: what the tab shows.</summary>
        public static int Pick(IList<string> offered, List<string> into)
        {
            into.Clear();
            for (int i = 0; offered != null && i < offered.Count; i++)
            {
                if (Is(offered[i]))
                {
                    into.Add(offered[i]);
                }
            }
            return into.Count;
        }
    }
}
