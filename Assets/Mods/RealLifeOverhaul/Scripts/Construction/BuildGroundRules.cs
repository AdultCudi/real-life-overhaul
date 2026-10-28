using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What holds a piece of ground that build mode's squares do not cover (mirrors Uptown's HeldKind, number for number).</summary>
    public enum HeldWhat
    {
        Place = 0,    // a big special place's ground: its building stands on it
        Park = 1,
        Campus = 2,
        Edge = 3      // the ring blocks' ground past their buildings
    }

    /// <summary>A piece of ground the grid of squares leaves out, held by something (a place, a park, the campus, the district's edge), as the plan gives it.</summary>
    public struct HeldView
    {
        public HeldWhat Kind;
        public int Block;          // Row * 100 + Column
        public int District;       // 0 Uptown, 1 Cedar Heights
        public string Lot;         // a place's own lot id, else null
        public string Street;      // a place's address, else null
        public int Number;
        public string Name;        // a place's business name, as it is in every language; else null
        public string NameKey;     // else a locale key naming what holds it
        public bool Stands;        // a place: its building stands at this load
        public float X1, Z1, X2, Z2;

        public bool Holds(float x, float z) => x >= X1 && x < X2 && z >= Z1 && z < Z2;
    }

    /// <summary>A block of the band as a rectangle on the world's axes: what the bare ground check walks over.</summary>
    public struct BlockView
    {
        public int Block;
        public float X1, Z1, X2, Z2;
    }

    /// <summary>What a column of squares is to the check that no ground is left unexplained.</summary>
    public enum ColumnClass
    {
        Unclassified = 0,   // not for sale, not held, no building standing: ground nothing accounts for
        Standing = 1,       // a building stands on it (or is going up)
        ForSale = 2,        // land the player may buy
        Yours = 3,          // the player's land or building
        Developer = 4,      // the city's developers hold it
        Landmark = 5,       // held for a landmark (a tower, a mall, a stadium)
        Kept = 6            // kept clear: something of the city's own stands there
    }

    /// <summary>What the bare ground check found, for the log and the offline checks.</summary>
    public sealed class BareReport
    {
        public int Blocks, Columns, Cells, Held;
        public int[] Classes = new int[7];          // columns by ColumnClass
        public int[] HeldKinds = new int[4];        // held grounds by HeldWhat
        public int PlacesDown;                      // special places whose building does not stand
        public double CellM2, HeldM2, MarginM2, BareM2;
        public string FirstColumn;                  // the first column nothing accounts for (a lot's id and why)
        public string FirstBare;                    // the first bare spot: its block and where
        public string WorstBlock;                   // the block with the most bare ground
        public double WorstM2;

        public int Unclassified => Classes[(int)ColumnClass.Unclassified];

        /// <summary>Every column of every block is a square, a building or a named holder; every metre of ground a square, a held ground or the plan's margin; every place stands.</summary>
        public bool Ok => Unclassified == 0 && BareM2 <= 0.0 && PlacesDown == 0;

        /// <summary>The check's one line for Player.log ("Build mode check (bare ground)").</summary>
        public string Line()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var s = new StringBuilder(512);
            s.Append("Build mode check (bare ground): ").Append(Blocks.ToString(inv)).Append(" blocks, ").Append(Columns.ToString(inv)).Append(" columns of ")
                .Append(Cells.ToString(inv)).Append(" squares (").Append(CellM2.ToString("0", inv)).Append(" m2): ")
                .Append(Classes[(int)ColumnClass.Standing].ToString(inv)).Append(" with a building, ").Append(Classes[(int)ColumnClass.ForSale].ToString(inv)).Append(" for sale, ")
                .Append(Classes[(int)ColumnClass.Yours].ToString(inv)).Append(" the player's, ").Append(Classes[(int)ColumnClass.Developer].ToString(inv)).Append(" the developers', ")
                .Append(Classes[(int)ColumnClass.Landmark].ToString(inv)).Append(" held for a landmark, ").Append(Classes[(int)ColumnClass.Kept].ToString(inv)).Append(" kept clear, ")
                .Append(Unclassified.ToString(inv)).Append(" accounted for by nothing").Append(FirstColumn != null ? " (the first " + FirstColumn + ")" : string.Empty)
                .Append("; held ground ").Append(Held.ToString(inv)).Append(" (").Append(HeldKinds[(int)HeldWhat.Place].ToString(inv)).Append(" special places, ")
                .Append(HeldKinds[(int)HeldWhat.Park].ToString(inv)).Append(" parks, ").Append(HeldKinds[(int)HeldWhat.Campus].ToString(inv)).Append(" campus, ")
                .Append(HeldKinds[(int)HeldWhat.Edge].ToString(inv)).Append(" edges, ").Append(HeldM2.ToString("0", inv)).Append(" m2), ")
                .Append(MarginM2.ToString("0", inv)).Append(" m2 of the plan's margin, ").Append(BareM2.ToString("0", inv)).Append(" m2 of bare ground")
                .Append(FirstBare != null ? " (the first " + FirstBare + ")" : string.Empty).Append("; ").Append(PlacesDown.ToString(inv)).Append(" special place(s) with no building: ")
                .Append(Ok ? "PASS" : "FAIL");
            return s.ToString();
        }
    }

    /// <summary>
    /// The rule that no ground of a block is left unexplained (the owner, 27 Oct 2026: "There are empty spots in the block with no
    /// grids"): every column of squares is a building's, for sale, the player's, the developers', held for a landmark or kept clear;
    /// every metre of every block is a square, a held ground (a special place, a park, the campus, the district's edge) or the plan's
    /// thin margin; every special place has its building up. Game-free, the same on every machine; EconomyCheck's BuildGroundChecks
    /// runs it over the plan and the game runs it over what stands at the load. COST: nothing per frame; once a load with Developer
    /// diagnostics on, a few thousand comparisons a block.
    /// </summary>
    public static class BuildGroundRules
    {
        public const float MarginMost = 2f;   // m of bare ground left between squares, or by a held ground, that is the plan's margin (Uptown's GroundSurvey.MarginMost)

        /// <summary>What a column of squares is, from what is known of it.</summary>
        public static ColumnClass ClassOf(BuildModeRules.ColumnFacts f)
        {
            if (f.Held)
            {
                return f.Free ? ColumnClass.Unclassified : f.Kept ? ColumnClass.Kept : ColumnClass.Landmark;   // held ground offered for sale is the rival's and the player's to buy twice
            }
            if (f.Mine || f.MyBuilding)
            {
                return ColumnClass.Yours;
            }
            if (f.UnderWay)
            {
                return ColumnClass.Standing;
            }
            if (f.Building)
            {
                return f.Stands ? ColumnClass.Standing : ColumnClass.Unclassified;   // a building the lot says it has and nothing stands: a bare spot with squares on it
            }
            if (f.Free)
            {
                return ColumnClass.ForSale;
            }
            return f.Developer ? ColumnClass.Developer : ColumnClass.Unclassified;
        }

        /// <summary>
        /// The check over the grid: every column classed by <paramref name="facts"/>, every held ground counted and every place's
        /// building asked after, then each block walked at <paramref name="step"/> m and every spot put under a square, a held ground,
        /// the plan's margin or bare. <paramref name="blocks"/> are the band's blocks, the ring's among them.
        /// </summary>
        public static BareReport Audit(CellGrid grid, IReadOnlyList<BlockView> blocks, Func<int, BuildModeRules.ColumnFacts> facts, float step)
        {
            var r = new BareReport { Columns = grid.Columns.Count, Cells = grid.CellCount, Held = grid.Held.Count, Blocks = blocks.Count };
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                ColumnClass k = ClassOf(facts(c));
                r.Classes[(int)k]++;
                if (k == ColumnClass.Unclassified && r.FirstColumn == null)
                {
                    r.FirstColumn = "column " + c.ToString(CultureInfo.InvariantCulture) + " of block " + grid.Columns[c].Block.ToString(CultureInfo.InvariantCulture)
                                    + ", lot " + grid.Columns[c].LotId;
                }
            }
            for (int i = 0; i < grid.Held.Count; i++)
            {
                HeldView h = grid.Held[i];
                r.HeldKinds[(int)h.Kind]++;
                r.HeldM2 += (h.X2 - h.X1) * (double)(h.Z2 - h.Z1);
                r.PlacesDown += h.Kind == HeldWhat.Place && !h.Stands ? 1 : 0;
            }
            var mine = new List<int>(64);
            double each = (double)step * step;
            for (int b = 0; b < blocks.Count; b++)
            {
                BlockView block = blocks[b];
                mine.Clear();
                for (int c = 0; c < grid.Columns.Count; c++)
                {
                    if (grid.Columns[c].Block == block.Block)
                    {
                        mine.Add(c);
                    }
                }
                double bare = 0;
                for (float z = block.Z1 + step * 0.5f; z < block.Z2; z += step)
                {
                    for (float x = block.X1 + step * 0.5f; x < block.X2; x += step)
                    {
                        if (SquareUnder(grid, mine, x, z))
                        {
                            r.CellM2 += each;
                        }
                        else if (grid.HeldAt(x, z) >= 0)
                        {
                            // counted once in HeldM2 above; here the ground of this block walked
                        }
                        else if (Gap(grid, mine, x, z) <= MarginMost)
                        {
                            r.MarginM2 += each;
                        }
                        else
                        {
                            bare += each;
                            r.FirstBare = r.FirstBare ?? "block " + block.Block.ToString(CultureInfo.InvariantCulture) + " at " + x.ToString("0", CultureInfo.InvariantCulture)
                                          + ", " + z.ToString("0", CultureInfo.InvariantCulture);
                        }
                    }
                }
                r.BareM2 += bare;
                if (bare > r.WorstM2)
                {
                    r.WorstM2 = bare;
                    r.WorstBlock = block.Block.ToString(CultureInfo.InvariantCulture);
                }
            }
            return r;
        }

        private static bool SquareUnder(CellGrid grid, List<int> columns, float x, float z)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (CellGrid.InColumn(grid.Columns[columns[i]], x, z))
                {
                    return true;
                }
            }
            return false;
        }

        // The nearest square of the block's columns, in m (the larger of the two ways the spot is outside the column's rectangle).
        private static float Gap(CellGrid grid, List<int> columns, float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0; i < columns.Count; i++)
            {
                CellColumn c = grid.Columns[columns[i]];
                bool alongX = c.Facing == 0 || c.Facing == 180;
                float along = alongX ? x : z, across = alongX ? z : x;
                float dAlong = along < c.A ? c.A - along : along > c.B ? along - c.B : 0f;
                float back = (across - c.Front) * c.Inward;
                float dBack = back < 0f ? -back : back > c.Depth ? back - c.Depth : 0f;
                best = Math.Min(best, Math.Max(dAlong, dBack));
            }
            return best;
        }
    }
}
