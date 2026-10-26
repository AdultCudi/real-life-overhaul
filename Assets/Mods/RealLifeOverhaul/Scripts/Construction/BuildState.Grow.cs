using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Where a grown building stands (plans/growables.md section 2).</summary>
    public enum GrowStage
    {
        Plans = 0,      // the land bought, the developer's architect drawing the plans
        Planning = 1,   // City Hall deciding the permission
        Building = 2,   // going up on its plot in the city's programme
        Letting = 3,    // standing, its unit for rent until its tenant moves in
        Let = 4,        // standing, its condition watched each week
        Dropped = 5     // given up: refused, nothing fits, or the district would not take it
    }

    /// <summary>Who a grown building is for.</summary>
    public enum GrowOwner
    {
        Builder = 0,    // a rival builder's development arm: the city's once it stands
        Holding = 1,    // a rival tycoon's holding, at a tier of its own
        Player = 2      // the player's own Let it grow building, once handed over (its project before that)
    }

    /// <summary>
    /// A building of the city's growth, from the land bought to the years after: its stage, the land it takes (its first space and
    /// how many after it along the strip), its use, developer, design, the land's price paid to the city, its tenant's day, and its
    /// condition. The player's Let it grow building is one too from its keys (its project before). Saved in the line's "^u:"
    /// part; a line without it has none.
    /// </summary>
    public sealed class GrowFile
    {
        public int Id;
        public GrowStage Stage;
        public GrowOwner Owner;
        public string Lot = string.Empty;            // its first space's lot
        public int Spaces = 1;                       // spaces it takes along the strip from the first
        public BuildUse Use;
        public int Rival;                            // the builder whose arm develops it, or who does a holding's works
        public string Holding = string.Empty;        // a rival tycoon's holding; empty for a builder's own arm or the player's
        public GrowTier Tier = GrowTier.Standard;
        public int Day;                              // the day it began (the land bought)
        public int Until;                            // the day its stage ends (Letting: the day its tenant moves in)
        public int SiteId;                           // the city's site showing it going up; 0 for none
        public string Design = string.Empty;         // the design it wears (a corner building's model by its own name), "-" when none fits
        public long Price;                           // the land's price paid to the city
        public int Handed;                           // the day it stood; 0 before
        public int Condition = GrowRules.ConditionFull;
        public int EmptyDays;                        // days in a row its unit has stood mostly empty since it was let or stood
        public bool RunDown;
        public int Week = -1;                        // the last week its condition was looked at
        public int Project;                          // the player's: the project it came from

        public bool UnderWay => Stage == GrowStage.Plans || Stage == GrowStage.Planning || Stage == GrowStage.Building;

        public bool Standing => Stage == GrowStage.Letting || Stage == GrowStage.Let;
    }

    public sealed partial class BuildState
    {
        public const int GrowsMost = 400;            // grown buildings kept: every one standing (Uptown's free land holds fewer), those given up let go first

        public readonly List<GrowFile> Grows = new List<GrowFile>(32);
        public int NextGrowDay;                      // the day the next grown site may start; 0 before the growth first looked
        public long CityLand;                        // money the city has had for land sold to its developers
        public long CityLandTax;                     // ... and in land tax on it while it was built on

        /// <summary>
        /// The council's zoning of free squares, by column (CellRules.Key of its strip and space): a digit a row from the street,
        /// '0' unzoned, '1' to '4' a use and one. A column with no record is zoned as the plan gives its lot (ZoneRules.CouncilUse).
        /// </summary>
        public readonly Dictionary<long, string> CouncilCells = new Dictionary<long, string>(64);

        /// <summary>The grown building that takes a lot (its first or one after it) while under way or standing, or null.</summary>
        public GrowFile GrowOn(string lot, Func<string, string, int, bool> within)
        {
            for (int i = 0; !string.IsNullOrEmpty(lot) && i < Grows.Count; i++)
            {
                GrowFile g = Grows[i];
                if (g.Stage == GrowStage.Dropped)
                {
                    continue;
                }
                if (string.Equals(g.Lot, lot, StringComparison.Ordinal) || (g.Spaces > 1 && within != null && within(g.Lot, lot, g.Spaces)))
                {
                    return g;
                }
            }
            return null;
        }

        /// <summary>The grown site under way whose site of the programme has this id, or null.</summary>
        public GrowFile GrowSite(int siteId)
        {
            for (int i = 0; siteId > 0 && i < Grows.Count; i++)
            {
                if (Grows[i].UnderWay && Grows[i].SiteId == siteId)
                {
                    return Grows[i];
                }
            }
            return null;
        }

        /// <summary>Grown sites under way now.</summary>
        public int GrowingNow()
        {
            int n = 0;
            for (int i = 0; i < Grows.Count; i++)
            {
                n += Grows[i].UnderWay ? 1 : 0;
            }
            return n;
        }
    }
}
