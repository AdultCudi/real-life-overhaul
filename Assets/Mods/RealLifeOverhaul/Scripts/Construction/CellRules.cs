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
        public bool StreetLow;   // a cross street runs past its strip's low end: the block's corner (LotView.StreetLow)
        public bool StreetHigh;  // ... past its high end
        public float EdgeLow;    // m on the strip's axis: the block's edge at the low end
        public float EdgeHigh;   // ... at the high end
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

    /// <summary>A site at a block's corner (plans/build-mode.md section 12): its ground as one lot facing its street, and which end of its strip the corner is.</summary>
    public struct CellCorner
    {
        public float X, Z;        // m: the site's ground's middle, cut to its shallowest column's rows
        public int Facing;        // degrees: the way its street lies
        public float Width;       // m along its street
        public float Depth;       // m back from it: its shallowest column's rows
        public bool Low;          // the corner is at the strip's low end (west or south)
        public float Edge;        // m on the strip's axis: the block's edge there, the cross street's building line
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
                        Front = front, Inward = inward, Depth = lot.Depth, Rows = rows, StreetLow = lot.StreetLow, StreetHigh = lot.StreetHigh, EdgeLow = lot.EdgeLow,
                        EdgeHigh = lot.EdgeHigh
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

        /// <summary>The column of the space before along the same strip, or -1.</summary>
        public int Prev(int column)
        {
            CellColumn c = Columns[column];
            return ColumnOf(c.Strip, c.Space - 1);
        }

        /// <summary>
        /// The cell of <paramref name="column"/>'s strip nearest a point on the ground (x, z), as Cities Skylines reads a drag: the
        /// pointer's place along the street held to the strip's spaces (walked from the column to the first gap either way), its
        /// depth from the street held to the strip's rows. A pointer gone over the block's corner into the next street's strip, or
        /// out onto the road, still picks along the street the drag began on. COST: a walk over the strip's spaces, no allocation.
        /// </summary>
        public CellAt Project(int column, float x, float z)
        {
            if (column < 0 || column >= Columns.Count)
            {
                return new CellAt(-1, 0);
            }
            CellColumn c = Columns[column];
            bool alongX = c.Facing == 0 || c.Facing == 180;
            float along = alongX ? x : z, across = alongX ? z : x;
            int at = column;
            while (along < Columns[at].A)
            {
                int prev = Prev(at);
                if (prev < 0)
                {
                    break;
                }
                at = prev;
            }
            while (along >= Columns[at].B)
            {
                int next = Next(at);
                if (next < 0)
                {
                    break;
                }
                at = next;
            }
            CellColumn hit = Columns[at];
            float back = (across - hit.Front) * hit.Inward;
            int row = back <= 0f ? 0 : Math.Min(hit.Rows - 1, (int)(back / (hit.Depth / hit.Rows)));
            return new CellAt(at, Math.Max(0, row));
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

        // ------------------------------------------------------------------ a forgiving drag (the owner, 26 Oct 2026: "selecting must be forgiving, like Cities Skylines")

        // What a space along a drag is to the tool in use (RunOk the tool may take it).
        public const int RunOk = 0;      // it may be part of the site
        public const int RunGap = 1;     // the strip has no such space (a landmark's ground, the strip's end)
        public const int RunTaken = 2;   // the tool cannot take it (another owner's, another site of the player's, not theirs)

        // What a drag left out, said under the panel's heading (BuildModeHintRules.TookKey gives its words).
        public const int TookAll = 0;      // every square dragged over was taken
        public const int TookWide = 1;     // the run was cut at a site's widest
        public const int TookGap = 2;      // ... at a gap in the street's spaces
        public const int TookTaken = 3;    // squares the tool cannot take were left out
        public const int TookStreet = 4;   // the pointer went onto another street's squares: only those along the first street were taken
        public const int TookDeeper = 5;   // one square alone makes no site: the square behind it was added

        /// <summary>
        /// The spaces a drag takes along its strip, as Cities Skylines takes a drag: <paramref name="spaces"/> holds what each space
        /// is to the tool (RunOk, RunGap, RunTaken), from the pressed space (0) toward the pointer's (wanted - 1). The run starts at
        /// the first space the tool may take and goes on while the tool may take each, at most <paramref name="most"/> spaces;
        /// nothing the tool may take: the pressed space alone, so the panel can say why. Returns the run's first index and its
        /// length (at least one), and what was left out (TookAll when nothing). Game-free; no allocation.
        /// </summary>
        public static int Reach(int[] spaces, int wanted, int most, out int start, out int took)
        {
            start = 0;
            took = TookAll;
            wanted = Math.Max(1, Math.Min(wanted, spaces != null ? spaces.Length : 0));
            if (spaces == null || spaces.Length == 0)
            {
                return 1;
            }
            int first = -1;
            for (int i = 0; i < wanted; i++)
            {
                if (spaces[i] == RunOk)
                {
                    first = i;
                    break;
                }
            }
            if (first < 0)
            {
                took = wanted > 1 ? (spaces[1] == RunGap ? TookGap : TookTaken) : TookAll;
                return 1;   // nothing to take: the pressed space alone, its reason in the panel
            }
            if (first > 0)
            {
                took = spaces[first - 1] == RunGap ? TookGap : TookTaken;
            }
            start = first;
            int count = 0;
            for (int i = first; i < wanted; i++)
            {
                if (spaces[i] != RunOk)
                {
                    took = spaces[i] == RunGap ? TookGap : TookTaken;
                    break;
                }
                if (count >= Math.Max(1, most))
                {
                    took = TookWide;
                    break;
                }
                count++;
            }
            return count;
        }

        /// <summary>
        /// How deep a drag's run is picked: the deeper of the pressed square's row and the pointer's, held to the strip's rows; a
        /// run of one space one square deep (no site, SiteCellsLeast) is taken a square deeper where the strip has one, with
        /// <paramref name="deeper"/> set. Rows count from 0 at the street.
        /// </summary>
        public static int RunRow(int fromRow, int toRow, int count, int stripRows, out bool deeper)
        {
            deeper = false;
            int row = Math.Max(0, Math.Min(Math.Max(fromRow, toRow), stripRows - 1));
            if (count == 1 && row == 0 && stripRows > 1)
            {
                deeper = true;
                row = 1;
            }
            return row;
        }

        /// <summary>What each column is to the tool in use, asked in order along a drag from its pressed space (CellRules.Run*).</summary>
        public interface IRunKind
        {
            int Kind(int column);
        }

        /// <summary>
        /// A drag's pick added to <paramref name="depth"/> (each column's deepest row picked, -1 none), as Cities Skylines takes a
        /// drag: along the pressed column's strip from it to <paramref name="to"/> (the pointer read onto that strip, CellGrid.Project),
        /// the run the tool may take (<see cref="Reach"/>, the kinds asked of <paramref name="kinds"/> in order), from the street back
        /// to the deeper of the two rows; with <paramref name="deepen"/> a run of one street square is taken a square deeper. The
        /// buffers hold the run (their length the farthest read). Returns what was left out (Took*). Game-free; no allocation.
        /// </summary>
        public static int DragPick(CellGrid grid, CellAt from, CellAt to, IRunKind kinds, int most, bool deepen, int[] depth, int[] run, int[] runColumn, out int firstColumn)
        {
            firstColumn = from.Column;
            CellColumn a = grid.Columns[from.Column], b = grid.Columns[to.Column];
            int step = Math.Sign(b.Space - a.Space);
            int wanted = Math.Min(Math.Min(run.Length, runColumn.Length), Math.Abs(b.Space - a.Space) + 1);
            int at = from.Column;
            for (int i = 0; i < wanted; i++)
            {
                runColumn[i] = at;
                run[i] = at < 0 ? RunGap : kinds.Kind(at);
                at = at < 0 || step == 0 ? -1 : step > 0 ? grid.Next(at) : grid.Prev(at);
            }
            int count = Reach(run, wanted, most, out int start, out int took);
            bool deeper = false;
            int row = Math.Max(from.Row, to.Row);
            if (deepen && run[start] == RunOk)
            {
                row = RunRow(from.Row, to.Row, count, a.Rows, out deeper);
            }
            firstColumn = runColumn[start];
            for (int i = start; i < start + count; i++)
            {
                int column = runColumn[i];
                if (column >= 0 && column < depth.Length)
                {
                    BuildModeRules.Toggle(depth, column, Math.Min(row, grid.Columns[column].Rows - 1), false);
                }
            }
            return took == TookAll && deeper ? TookDeeper : took;
        }

        /// <summary>
        /// Picks added to those already there wider than a site may be: the columns farthest from <paramref name="keep"/> (the drag's
        /// start) let go until the picks are <paramref name="most"/> spaces at most. True when any were. Game-free; a pass over the columns.
        /// </summary>
        public static bool TrimWide(CellGrid grid, int[] depth, int keep, int most)
        {
            if (keep < 0 || keep >= grid.Columns.Count)
            {
                return false;
            }
            int strip = grid.Columns[keep].Strip, near = grid.Columns[keep].Space;
            bool cut = false;
            while (true)
            {
                int lo = -1, hi = -1, n = 0;
                for (int c = 0; c < depth.Length && c < grid.Columns.Count; c++)
                {
                    if (depth[c] < 0 || grid.Columns[c].Strip != strip)
                    {
                        continue;
                    }
                    n++;
                    lo = lo < 0 || grid.Columns[c].Space < grid.Columns[lo].Space ? c : lo;
                    hi = hi < 0 || grid.Columns[c].Space > grid.Columns[hi].Space ? c : hi;
                }
                if (n == 0 || grid.Columns[hi].Space - grid.Columns[lo].Space + 1 <= most)
                {
                    return cut;
                }
                depth[near - grid.Columns[lo].Space >= grid.Columns[hi].Space - near ? lo : hi] = -1;
                cut = true;
            }
        }

        /// <summary>
        /// True when a press on <paramref name="cell"/> joins what is picked (<paramref name="depth"/>): on the same strip, at or beside a
        /// picked column; nothing picked: true. A press anywhere else starts a new pick (the owner's test, 26 Oct 2026: a drag on
        /// another street after a pick mixed both streets' squares and was refused). Game-free; a pass over the columns.
        /// </summary>
        public static bool Touches(CellGrid grid, int[] depth, CellAt cell)
        {
            if (cell.None || cell.Column >= grid.Columns.Count)
            {
                return true;
            }
            CellColumn at = grid.Columns[cell.Column];
            bool any = false;
            for (int c = 0; c < depth.Length && c < grid.Columns.Count; c++)
            {
                if (depth[c] < 0)
                {
                    continue;
                }
                any = true;
                CellColumn p = grid.Columns[c];
                if (p.Strip == at.Strip && Math.Abs(p.Space - at.Space) <= 1)
                {
                    return true;
                }
            }
            return !any;
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

        // ------------------------------------------------------------------ corner sites (plans/build-mode.md section 12)

        public const float CornerSlack = 1f;   // m: a site's end this near the block's edge is at the corner (a strip's spaces leave under a metre at its ends)

        /// <summary>
        /// Which end of its strip a run of ground from <paramref name="lo"/> to <paramref name="hi"/> (m on the strip's axis) meets a cross
        /// street at: its low end within <see cref="CornerSlack"/> of the block's edge there, else its high end; the edge at that end.
        /// False for ground that meets no cross street (Scripts/Corners' CornerRules.End answers the same).
        /// </summary>
        public static bool CornerEnd(float lo, float hi, bool streetLow, float edgeLow, bool streetHigh, float edgeHigh, out bool low, out float edge)
        {
            low = false;
            edge = 0f;
            if (streetLow && Math.Abs(lo - edgeLow) <= CornerSlack)
            {
                low = true;
                edge = edgeLow;
                return true;
            }
            if (streetHigh && Math.Abs(edgeHigh - hi) <= CornerSlack)
            {
                edge = edgeHigh;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The corner a run of columns stands at: a corner site is a set of cells touching both streets at a block's corner, which is
        /// a run of the strip's corner column and its neighbours along the avenue (every site runs from the street back, so its first
        /// row touches the avenue, and the corner column's side the cross street). False for a run at no corner.
        /// </summary>
        public static bool Corner(CellGrid grid, int firstColumn, int count, out bool low, out float edge)
        {
            low = false;
            edge = 0f;
            if (grid == null || firstColumn < 0 || firstColumn >= grid.Columns.Count || count <= 0)
            {
                return false;
            }
            CellColumn first = grid.Columns[firstColumn];
            int at = firstColumn;
            for (int i = 1; i < count; i++)
            {
                at = grid.Next(at);
                if (at < 0)
                {
                    return false;
                }
            }
            CellColumn last = grid.Columns[at];
            return CornerEnd(first.A, last.B, first.StreetLow, first.EdgeLow, last.StreetHigh, last.EdgeHigh, out low, out edge);
        }

        /// <summary>The corner a lot of the plan stands at (the developers' and the city's lots), by its ground along its strip; false for none.</summary>
        public static bool Corner(LotView lot, out bool low, out float edge)
        {
            int facing = ((lot.Yaw + 180) % 360 + 360) % 360;
            float mid = facing == 0 || facing == 180 ? lot.X : lot.Z;
            return CornerEnd(mid - lot.Width * 0.5f, mid + lot.Width * 0.5f, lot.StreetLow, lot.EdgeLow, lot.StreetHigh, lot.EdgeHigh, out low, out edge);
        }

        /// <summary>A corner site's ground as one lot (as <see cref="Trim"/> cut it) with its corner.</summary>
        public static CellCorner CornerGround(LotView site, bool low, float edge)
        {
            return new CellCorner
            {
                X = site.X, Z = site.Z, Facing = ((site.Yaw + 180) % 360 + 360) % 360, Width = site.Width, Depth = site.Depth, Low = low, Edge = edge
            };
        }
    }
}
