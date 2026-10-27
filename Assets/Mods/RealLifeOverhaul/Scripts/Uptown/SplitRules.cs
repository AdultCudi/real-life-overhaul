using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What happens to a save made when the band was one district (the owner, 27 Oct 2026: "Every single thing must be separated
    /// properly"), game-free (EconomyCheck's UptownSplitChecks): once, at the first load after the split, everything the save
    /// holds that names the old Uptown is put in the right district by its position, and the rebalance (the plan builds only a few
    /// of the band's M1 stores, the rest J1 offices on the same lots, LayoutRules.Swaps) is decided lot by lot: a lot someone holds
    /// (the player's rent or deed, another player's, a building the construction game finished or knocked down there) keeps its
    /// store, so no rent, deed or business is lost; the others are rebuilt as offices and the records the save kept for their
    /// old shops let go, as a lot the plan built newly would have none. The decision is written into the district's line and
    /// never made twice. Every answer comes from the arguments alone: the same on every machine with the same save.
    /// COST: nothing per frame; a pass over the nineteen lots once a save.
    /// </summary>
    public static class SplitRules
    {
        /// <summary>
        /// The lots the rebalance would rebuild (<paramref name="swaps"/>) sorted into those a save keeps as stores
        /// (<paramref name="pinned"/>: <paramref name="held"/> says someone holds the lot) and those it rebuilds
        /// (<paramref name="rebuilt"/>); of the rebuilt, <paramref name="retired"/> are the ones the save kept records for
        /// (<paramref name="kept"/>), which the district save lets go. Into the callers' lists (cleared), in the swaps' order.
        /// </summary>
        public static void Decide(IReadOnlyList<string> swaps, Func<string, bool> kept, Func<string, bool> held, List<string> pinned, List<string> rebuilt, List<string> retired)
        {
            pinned.Clear();
            rebuilt.Clear();
            retired.Clear();
            for (int i = 0; swaps != null && i < swaps.Count; i++)
            {
                string lot = swaps[i];
                if (held != null && held(lot))
                {
                    pinned.Add(lot);
                    continue;
                }
                rebuilt.Add(lot);
                if (kept != null && kept(lot))
                {
                    retired.Add(lot);
                }
            }
        }

        /// <summary>
        /// Whether the save needs the rebalance decided now: its line has none of the current generation. A line that cannot be
        /// read decides nothing and keeps every lot as it was (<see cref="KeepAll"/>).
        /// </summary>
        public static bool Undecided(int decidedGeneration)
        {
            return decidedGeneration < LayoutRules.RebalanceGeneration;
        }

        /// <summary>Every swap lot pinned: the plan of a save whose line cannot be read is the first plan's, so nothing in it changes.</summary>
        public static void KeepAll(IReadOnlyList<string> swaps, List<string> pinned)
        {
            pinned.Clear();
            for (int i = 0; swaps != null && i < swaps.Count; i++)
            {
                pinned.Add(swaps[i]);
            }
        }

        /// <summary>
        /// The district's market level a district starts from in a save from before the split: its own kept level, else the whole
        /// band's (Uptown's line part), else even; always inside the market's caps.
        /// </summary>
        public static int StartLevel(int ownLevel, int bandLevel)
        {
            int level = ownLevel > 0 ? ownLevel : bandLevel > 0 ? bandLevel : 1000;
            return Math.Max(DemandRules.LevelLeast, Math.Min(DemandRules.LevelMost, level));
        }

        /// <summary>
        /// How a record of the save that names the old Uptown by its district id alone is dealt with at the split: the stats, the demand
        /// rows, the map filter and the market level are copied so each district has its own, the same at the start (Both); a market
        /// event or a pricing manager's plan stays with Uptown, which it was set for (First); a thing with a place (a building, a lot, a
        /// cell, a project, a deed) needs nothing, as its place names its district (Position).
        /// </summary>
        public static SplitKind KindOf(string what)
        {
            switch (what)
            {
                case "stats":
                case "demand":
                case "filter":
                case "level":
                    return SplitKind.Both;
                case "event":
                case "plan":
                    return SplitKind.First;
                default:
                    return SplitKind.Position;
            }
        }
    }

    /// <summary>How a record naming the old Uptown is dealt with at the split (<see cref="SplitRules.KindOf"/>).</summary>
    public enum SplitKind
    {
        Position,   // its place says the district: a building, a lot, a cell, a project, a deed, a rental
        Both,       // copied for the second district, the first keeping its own
        First       // stays with Uptown
    }
}
