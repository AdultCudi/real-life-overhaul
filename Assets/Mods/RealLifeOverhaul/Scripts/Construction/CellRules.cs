using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>One column of zoning cells: one space of a block's strip, from its street back, cut into depth rows.</summary>
    public sealed class CellColumn
    {
        public string LotId;     // the plan's lot the space is part of (a single space's id for a free space)
        public int LotColumn;    // its place among that lot's spaces, from the strip's start
        public int Strip;        // the strip (LotView.Strip: Block * 1000 + Facing)
        public int Space;        // its space along the strip, from the strip's west or south end
        public int Block;
        public int Facing;       // degrees, 0 = +Z: the way the street lies from it
        public float A, B;       // m along the strip's axis (x for an avenue's, z for a street's): its two ends, A below B
        public float Front;      // m: the street's front line on the other axis
        public float Inward;     // +1 or -1: the way into the block from the front line
        public float Depth;      // m: the strip's depth here
        public int Rows;         // its depth rows
    }

    /// <summary>A cell: one depth row of one column, by the column's place in the grid.</summary>
    public struct CellAt
    {
        public int Column;   // index into CellGrid.Columns, -1 for none
        public int Row;      // 0 at the street

        public CellAt(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public bool None => Column < 0;
    }

    /// <summary>A site as the build mode's rules make it from the cells picked: neighbouring columns along one strip, each from the street back.</summary>
    public struct CellSite
    {
        public int Strip;
        public int FirstColumn;   // index into CellGrid.Columns of its first column (lowest space)
        public int Count;         // columns
        public int Cells;         // cells in all
        public int MinRows;       // its shallowest column's rows
        public int[] Rows;        // rows in each column, from the first (length Count)
    }

    /// <summary>
    /// The district's grid of zoning cells over a list of the plan's lots (plans/build-mode.md section 2; the owner, 26 Oct
    /// 2026: "Basically split that block into proper tiny zoning squares like cities skylines"). Every lot is the whole spaces
    /// of a strip from its street back; each space is a column of cells, cut into rows of about 7.5 m of its strip's depth.
    /// Built once from the lots (allocated then), then read: a cell's rectangle, the cell under a point, the site the cells
    /// picked make, a site's price by its cells, the rows bought kept as digits. Game-free, the same on every machine.
    /// COST: nothing per frame; <see cref="At"/> is a pass of comparisons over the columns, asked ten times a second while build
    /// mode is open.
    /// </summary>
    public sealed class CellGrid
    {
        public readonly List<CellColumn> Columns = new List<CellColumn>(2048);
        private readonly Dictionary<long, int> _byKey = new Dictionary<long, int>(2048);

        /// <summary>Cells in all.</summary>
        public int CellCount { get; private set; }

        /// <summary>The grid over the lots given (the district's, as LotView), in their order, each lot's spaces in strip order.</summary>
        public static CellGrid Of(IReadOnlyList<LotView> lots)
        {
            var grid = new CellGrid();
            for (int i = 0; lots != null && i < lots.Count; i++)
            {
                LotView lot = lots[i];
                if (lot.Strip <= 0 || lot.Width <= 0 || lot.Depth <= 0)
                {
                    continue;   // a test's lot or a landmark's site: no space of the grid
                }
                int spaces = Math.Max(1, lot.Spaces);
                int facing = ((lot.Yaw + 180) % 360 + 360) % 360;
                bool alongX = facing == 0 || facing == 180;
                float mid = alongX ? lot.X : lot.Z;
                float lo = mid - lot.Width * 0.5f, each = (float)lot.Width / spaces;
                float front, inward;
                switch (facing)
                {
                    case 0: front = lot.Z + lot.Depth * 0.5f; inward = -1f; break;
                    case 180: front = lot.Z - lot.Depth * 0.5f; inward = 1f; break;
                    case 90: front = lot.X + lot.Depth * 0.5f; inward = -1f; break;
                    default: front = lot.X - lot.Depth * 0.5f; inward = 1f; break;
                }
                int rows = CellRules.RowsFor(lot.Depth);
                for (int c = 0; c < spaces; c++)
                {
                    long key = CellRules.Key(lot.Strip, lot.Index + c);
                    if (grid._byKey.ContainsKey(key))
                    {
                        continue;   // a space is in one lot only; a second claim is passed over
                    }
                    var column = new CellColumn
                    {
                        LotId = lot.Id, LotColumn = c, Strip = lot.Strip, Space = lot.Index + c, Block = lot.Block, Facing = facing, A = lo + each * c, B = lo + each * (c + 1),
                        Front = front, Inward = inward, Depth = lot.Depth, Rows = rows
                    };
                    grid._byKey[key] = grid.Columns.Count;
                    grid.Columns.Add(column);
                    grid.CellCount += rows;
                }
            }
            return grid;
        }

        /// <summary>The column of a strip's space, or -1.</summary>
        public int ColumnOf(int strip, int space)
        {
            return _byKey.TryGetValue(CellRules.Key(strip, space), out int at) ? at : -1;
        }

        /// <summary>The cell under a point on the ground (x, z), or none.</summary>
        public CellAt At(float x, float z)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                CellColumn c = Columns[i];
                bool alongX = c.Facing == 0 || c.Facing == 180;
                float along = alongX ? x : z, across = alongX ? z : x;
                if (along < c.A || along >= c.B)
                {
                    continue;
                }
                float back = (across - c.Front) * c.Inward;
                if (back < 0f || back >= c.Depth)
                {
                    continue;
                }
                int row = Math.Min(c.Rows - 1, (int)(back / (c.Depth / c.Rows)));
                return new CellAt(i, row);
            }
            return new CellAt(-1, 0);
        }

        /// <summary>A cell's rectangle on the world's axes (x and z from, to), with a gutter of <paramref name="inset"/> m each side.</summary>
        public void Rect(int column, int row, float inset, out float x1, out float z1, out float x2, out float z2)
        {
            CellColumn c = Columns[column];
            float rowDepth = c.Depth / c.Rows;
            float d1 = c.Front + c.Inward * rowDepth * row, d2 = c.Front + c.Inward * rowDepth * (row + 1);
            float dLo = Math.Min(d1, d2) + inset, dHi = Math.Max(d1, d2) - inset;
            float aLo = c.A + inset, aHi = c.B - inset;
            if (c.Facing == 0 || c.Facing == 180)
            {
                x1 = aLo;
                x2 = aHi;
                z1 = dLo;
                z2 = dHi;
            }
            else
            {
                x1 = dLo;
                x2 = dHi;
                z1 = aLo;
                z2 = aHi;
            }
        }

        /// <summary>The column of the next space along the same strip, or -1.</summary>
        public int Next(int column)
        {
            CellColumn c = Columns[column];
            return ColumnOf(c.Strip, c.Space + 1);
        }
    }

    /// <summary>The rules of the cells: rows, the site from the cells picked, the price by cells, the rows bought, a design's footprint, the ghost's lot.</summary>
    public static class CellRules
    {
        public const float RowAbout = 7.5f;      // m: a depth row's size, about; a strip's rows are its depth over this, rounded
        public const int SiteCellsLeast = 2;     // cells: a site is at least a frontage cell and one behind it
        public const float CellGutter = 0.35f;   // m left undrawn round each cell, so the grid reads as squares

        // Why the cells picked make no site (a text key each).
        public const string NoCells = "reallife_bld_mode_refuse_none";
        public const string TwoStrips = "reallife_bld_mode_refuse_strips";
        public const string NotJoined = "reallife_bld_mode_refuse_joined";
        public const string TooWide = "reallife_bld_mode_refuse_wide";
        public const string TooFew = "reallife_bld_mode_refuse_few";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A strip's depth rows: its depth over about 7.5 m, rounded, never fewer than one (39 m: 5, 35 m: 5, 32 m: 4).</summary>
        public static int RowsFor(float depth)
        {
            return Math.Max(1, (int)Math.Round(depth / RowAbout, MidpointRounding.AwayFromZero));
        }

        /// <summary>A strip's space as one number.</summary>
        public static long Key(int strip, int space) => (long)strip * 1000L + space;

        /// <summary>A cell's id, stable on every machine and load: its strip, its space and its row.</summary>
        public static string Id(CellColumn c, int row)
        {
            return c.Strip.ToString(Inv) + "." + c.Space.ToString(Inv) + "." + row.ToString(Inv);
        }

        /// <summary>
        /// The site the cells picked make: every cell between a picked one and the street added, the columns joined along one strip.
        /// Null with the site, or a text key of why not (<see cref="NoCells"/> and the others). Each column's rows are its
        /// deepest picked row's, plus one. <paramref name="most"/>: the columns a site may take (BuildCatalogue.SiteSpacesMost).
        /// </summary>
        public static string Site(CellGrid grid, IList<CellAt> picked, int most, out CellSite site)
        {
            site = default(CellSite);
            if (grid == null || picked == null || picked.Count == 0)
            {
                return NoCells;
            }
            int strip = -1, low = int.MaxValue, high = -1;   // spaces count from 0
            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i].None || picked[i].Column >= grid.Columns.Count)
                {
                    continue;
                }
                CellColumn c = grid.Columns[picked[i].Column];
                if (strip >= 0 && c.Strip != strip)
                {
                    return TwoStrips;
                }
                strip = c.Strip;
                low = Math.Min(low, c.Space);
                high = Math.Max(high, c.Space);
            }
            if (strip < 0)
            {
                return NoCells;
            }
            int count = high - low + 1;
            var rows = new int[count];
            for (int i = 0; i < picked.Count; i++)
            {
                if (!picked[i].None && picked[i].Column < grid.Columns.Count)
                {
                    CellColumn c = grid.Columns[picked[i].Column];
                    int at = c.Space - low;
                    rows[at] = Math.Max(rows[at], Math.Min(c.Rows, picked[i].Row + 1));
                }
            }
            int cells = 0, minRows = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (rows[i] == 0 || grid.ColumnOf(strip, low + i) < 0)
                {
                    return NotJoined;   // a space between with nothing picked: two sites, not one
                }
                cells += rows[i];
                minRows = Math.Min(minRows, rows[i]);
            }
            if (count > most)
            {
                return TooWide;
            }
            if (cells < SiteCellsLeast)
            {
                return TooFew;
            }
            site = new CellSite { Strip = strip, FirstColumn = grid.ColumnOf(strip, low), Count = count, Cells = cells, MinRows = minRows, Rows = rows };
            return null;
        }

        /// <summary>A column's price for so many of its rows: its space's price times the rows over the strip's rows; every row is the space's price.</summary>
        public static long ColumnPrice(long spacePrice, int rows, int stripRows)
        {
            if (spacePrice <= 0L || stripRows <= 0 || rows <= 0)
            {
                return 0L;
            }
            if (rows >= stripRows)
            {
                return spacePrice;
            }
            return spacePrice * rows / stripRows;
        }

        /// <summary>The price of rows more in a column that has <paramref name="had"/> (the whole column's price less what the rows had cost).</summary>
        public static long DeeperPrice(long spacePrice, int had, int rows, int stripRows)
        {
            return Math.Max(0L, ColumnPrice(spacePrice, rows, stripRows) - ColumnPrice(spacePrice, had, stripRows));
        }

        /// <summary>The rows bought in each column as digits ("5543"); empty when every column has every row (as every older site).</summary>
        public static string RowsText(int[] rows, int count, int stripRows)
        {
            bool whole = true;
            for (int i = 0; i < count; i++)
            {
                whole &= rows[i] >= stripRows;
            }
            if (whole)
            {
                return string.Empty;
            }
            var chars = new char[count];
            for (int i = 0; i < count; i++)
            {
                chars[i] = (char)('0' + Math.Max(1, Math.Min(9, rows[i])));
            }
            return new string(chars);
        }

        /// <summary>A column's rows from a site's digits (<paramref name="column"/> from its first); every row where the digits say none.</summary>
        public static int RowsAt(string text, int column, int stripRows)
        {
            if (string.IsNullOrEmpty(text) || column < 0 || column >= text.Length)
            {
                return stripRows;
            }
            int d = text[column] - '0';
            return d >= 1 && d <= 9 ? Math.Min(d, stripRows) : stripRows;
        }

        /// <summary>A site's digits as read from a save: only digits 1 to 9, one a space; anything else is every row.</summary>
        public static string CleanRows(string text, int spaces)
        {
            if (string.IsNullOrEmpty(text) || text.Length != spaces)
            {
                return string.Empty;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '1' || text[i] > '9')
                {
                    return string.Empty;
                }
            }
            return text;
        }

        /// <summary>Two sites' digits joined (the first's columns, then the second's); empty when both have every row.</summary>
        public static string JoinRows(string a, int aSpaces, string b, int bSpaces, int stripRows)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            {
                return string.Empty;
            }
            var rows = new int[aSpaces + bSpaces];
            for (int i = 0; i < aSpaces; i++)
            {
                rows[i] = RowsAt(a, i, stripRows);
            }
            for (int i = 0; i < bSpaces; i++)
            {
                rows[aSpaces + i] = RowsAt(b, i, stripRows);
            }
            return RowsText(rows, rows.Length, stripRows);
        }

        /// <summary>One lot's digits of a site split back into its lots (<paramref name="column"/> from its first); empty when it has every row.</summary>
        public static string SplitRows(string text, int column, int stripRows)
        {
            int rows = RowsAt(text, column, stripRows);
            return rows >= stripRows ? string.Empty : ((char)('0' + rows)).ToString();
        }

        /// <summary>The shallowest column's rows of a site.</summary>
        public static int MinRows(string text, int spaces, int stripRows)
        {
            int least = stripRows;
            for (int i = 0; i < spaces; i++)
            {
                least = Math.Min(least, RowsAt(text, i, stripRows));
            }
            return least;
        }

        /// <summary>
        /// A site's ground cut to the rows it has: its depth the shallowest column's rows' share of the strip's (whole metres,
        /// never more), its middle moved toward the street by the depth left behind. Every row: unchanged.
        /// </summary>
        public static LotView Trim(LotView site, int rows, int stripRows)
        {
            if (rows >= stripRows || rows <= 0 || stripRows <= 0 || site.Depth <= 0)
            {
                return site;
            }
            int depth = Math.Max(1, (int)Math.Floor(site.Depth * (double)rows / stripRows + 1e-6));
            float shift = (site.Depth - depth) * 0.5f;
            int facing = ((site.Yaw + 180) % 360 + 360) % 360;
            switch (facing)
            {
                case 0: site.Z += shift; break;     // the street to the north: toward it
                case 180: site.Z -= shift; break;
                case 90: site.X += shift; break;
                default: site.X -= shift; break;
            }
            site.Depth = depth;
            return site;
        }

        /// <summary>The rows a design's depth needs in a column of this row depth.</summary>
        public static int RowsNeeded(float designDepth, float rowDepth)
        {
            return rowDepth <= 0f ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(designDepth / rowDepth - 0.001f));
        }

        /// <summary>The ground a footprint of columns stands on for its ghost: the middle of its frontage at the street, as the district stands a building (NewBuildings' MadeLot).</summary>
        public static void FootprintLot(CellGrid grid, int firstColumn, int count, out float x, out float z, out float facing, out float width, out float depth)
        {
            CellColumn first = grid.Columns[firstColumn];
            float lo = first.A, hi = first.B;
            int at = firstColumn;
            for (int i = 1; i < count; i++)
            {
                int next = grid.Next(at);
                if (next < 0)
                {
                    break;
                }
                at = next;
                lo = Math.Min(lo, grid.Columns[at].A);
                hi = Math.Max(hi, grid.Columns[at].B);
            }
            width = hi - lo;
            depth = first.Depth;
            facing = first.Facing;
            float along = (lo + hi) * 0.5f, across = first.Front + first.Inward * first.Depth * 0.5f;
            bool alongX = first.Facing == 0 || first.Facing == 180;
            x = alongX ? along : across;
            z = alongX ? across : along;
        }
    }
}
