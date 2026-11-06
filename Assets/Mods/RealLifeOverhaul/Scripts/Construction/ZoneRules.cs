using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Zone tool's ways of picking squares (plans/growables.md section 1, as Cities Skylines' zoning tools).</summary>
    public enum ZoneShape
    {
        Fill = 0,     // a click fills every square of the player's own along that block side, to the depth they hold
        Rect = 1,     // a drag marks the rectangle between the press and the pointer, along the press's street
        Brush = 2,    // a drag marks each square the pointer passes over
        Unzone = 3,   // a drag's rectangle has its zoning taken away
        Count = 4
    }

    /// <summary>
    /// Zoning per square (plans/growables.md section 1; the owner, 26 Oct 2026: "Basically split that block into proper tiny
    /// zoning squares like cities skylines"). A site of the player's keeps its squares' zones as text, one group a column of
    /// its spaces divided by '.', one digit a row from the street ('0' none, '1' to '4' a use and one): "2222.2210". A column's
    /// group may be shorter than its rows and an older save's text is empty: a square with no digit takes the site's own
    /// paint (LotFile.Paint), so every older save reads as before, its site's zone on each of its squares. The council zones
    /// the free land square by square too (BuildState.CouncilCells; a column with no record takes the use the plan gives its
    /// lot, DevelopmentRules.Use, on every row). The tools' picks (a block side, a rectangle, the brush) are worked here on
    /// the grid. Game-free (EconomyCheck's GrowChecks), the same on every machine.
    /// COST: nothing per frame; a pass over a site's digits on a paint, over a strip's spaces on a pick.
    /// </summary>
    public static class ZoneRules
    {
        public const char Gap = '.';        // between two columns' groups
        public const int MostRows = 9;      // digits a column's group holds at most (a strip has 4 or 5 rows)
        public const int RezoneMost = 3;    // free columns the council rezones at one sitting, side by side along a street
        public const int RezoneHigh = 60;   // demand a use needs before the council rezones free land for it
        public const int RezoneLow = 35;    // demand under which a use's free land may be rezoned for another
        public const int RezoneOddsBase = 500;   // thousandths: the council's odds of rezoning at a sitting before the demand's lift (BuildModeRules.CouncilLift)

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The paint a square of a site has: its own digit, else the site's paint (0 none, else BuildUse + 1).</summary>
        public static int At(string cells, int column, int row, int sitePaint)
        {
            if (string.IsNullOrEmpty(cells) || column < 0 || row < 0)
            {
                return Paint(sitePaint);
            }
            int start = GroupStart(cells, column, out int length);
            if (start < 0 || row >= length)
            {
                return Paint(sitePaint);
            }
            return Digit(cells[start + row], sitePaint);
        }

        /// <summary>One column's digits of a site (null when it has none of its own: every square takes the site's paint).</summary>
        public static string Column(string cells, int column)
        {
            if (string.IsNullOrEmpty(cells) || column < 0)
            {
                return null;
            }
            int start = GroupStart(cells, column, out int length);
            return start < 0 || length == 0 ? null : cells.Substring(start, length);
        }

        /// <summary>
        /// A site's digits with one square painted (<paramref name="paint"/> 0 none, else BuildUse + 1): the text is first made whole
        /// for its <paramref name="spaces"/> columns, each its <paramref name="rows"/> rows, the squares with no digit given the site's paint.
        /// </summary>
        public static string Set(string cells, int spaces, int[] rows, int sitePaint, int column, int row, int paint)
        {
            char[][] groups = Groups(cells, spaces, rows, sitePaint);
            if (column >= 0 && column < groups.Length && row >= 0 && row < groups[column].Length)
            {
                groups[column][row] = (char)('0' + Paint(paint));
            }
            return Text(groups);
        }

        /// <summary>A site's digits with many squares painted at once (pairs of column and row): one pass, one text.</summary>
        public static string SetMany(string cells, int spaces, int[] rows, int sitePaint, IList<int> columns, IList<int> squareRows, int paint)
        {
            char[][] groups = Groups(cells, spaces, rows, sitePaint);
            char d = (char)('0' + Paint(paint));
            for (int i = 0; columns != null && squareRows != null && i < columns.Count && i < squareRows.Count; i++)
            {
                int c = columns[i], r = squareRows[i];
                if (c >= 0 && c < groups.Length && r >= 0 && r < groups[c].Length)
                {
                    groups[c][r] = d;
                }
            }
            return Text(groups);
        }

        /// <summary>Every square of a site one paint: as an older save's site is read, written out whole.</summary>
        public static string Fill(int spaces, int[] rows, int paint)
        {
            return Text(Groups(null, spaces, rows, paint));
        }

        /// <summary>The use most of a site's squares carry (BuildUse + 1; ties to the lower use), 0 for none painted: the site's own paint, for every reader of LotFile.Paint.</summary>
        public static int Main(string cells, int sitePaint)
        {
            if (string.IsNullOrEmpty(cells))
            {
                return Paint(sitePaint);
            }
            int[] count = new int[(int)BuildUse.Count + 1];
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != Gap)
                {
                    count[Digit(cells[i], 0)]++;
                }
            }
            int best = 0;
            for (int u = 1; u < count.Length; u++)
            {
                best = count[u] > count[best] || (best == 0 && count[u] > 0) ? u : best;
            }
            return best;
        }

        /// <summary>How many of a site's squares carry a paint, its digits alone (those with no digit are not counted).</summary>
        public static int Count(string cells, int paint)
        {
            int n = 0;
            char d = (char)('0' + Paint(paint));
            for (int i = 0; cells != null && i < cells.Length; i++)
            {
                n += cells[i] == d ? 1 : 0;
            }
            return n;
        }

        /// <summary>A site's digits with every square of one paint taken back to none (the council refused that use).</summary>
        public static string Cleared(string cells, int paint)
        {
            if (string.IsNullOrEmpty(cells) || paint <= 0)
            {
                return cells ?? string.Empty;
            }
            return cells.Replace((char)('0' + Paint(paint)), '0');
        }

        /// <summary>A site's digits as read from a save: digits 0 to 4 in at most <paramref name="spaces"/> groups of up to MostRows; anything else reads as none of its own.</summary>
        public static string Clean(string text, int spaces)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            int groups = 1, run = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == Gap)
                {
                    groups++;
                    run = 0;
                    continue;
                }
                if (c < '0' || c > (char)('0' + (int)BuildUse.Count) || ++run > MostRows)
                {
                    return string.Empty;
                }
            }
            return groups <= Math.Max(1, spaces) ? text : string.Empty;
        }

        /// <summary>Two sites' digits joined (the first's columns, then the second's), each made whole with its own paint first.</summary>
        public static string Join(string a, int aSpaces, int[] aRows, int aPaint, string b, int bSpaces, int[] bRows, int bPaint)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b) && Paint(aPaint) == Paint(bPaint))
            {
                return string.Empty;   // both as an older save's, one paint: still none of their own
            }
            return Fill(a, aSpaces, aRows, aPaint) + Gap + Fill(b, bSpaces, bRows, bPaint);
        }

        /// <summary>One column's digits of a site split back into its lots (a site of one column), or empty when it has none of its own.</summary>
        public static string SplitOne(string cells, int column)
        {
            return Column(cells, column) ?? string.Empty;
        }

        /// <summary>True when every square of a footprint (its columns, each from the street to <paramref name="rows"/>) carries the use, in force or asked.</summary>
        public static bool Covers(string cells, int sitePaint, int firstColumn, int count, int rows, BuildUse use)
        {
            int paint = (int)use + 1;
            for (int c = firstColumn; c < firstColumn + count; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (At(cells, c, r, sitePaint) != paint)
                    {
                        return false;
                    }
                }
            }
            return count > 0 && rows > 0;
        }

        // ------------------------------------------------------------------ the council's zoning of free land

        /// <summary>The use the council zones a free square for when it has made no change there: the plan's use for the lot (BuildUse), on every row.</summary>
        public static int CouncilUse(string lotId, int lotClass) => (int)DevelopmentRules.Use(lotId ?? string.Empty, lotClass);

        /// <summary>The council's paint of a free square: its record's digit (0 unzoned), else the plan's use and one.</summary>
        public static int CouncilAt(string record, int row, string lotId, int lotClass)
        {
            if (!string.IsNullOrEmpty(record) && row >= 0 && row < record.Length)
            {
                return Digit(record[row], 0);
            }
            return CouncilUse(lotId, lotClass) + 1;
        }

        /// <summary>A column of free land zoned whole for one use (BuildUse + 1, 0 unzoned) by the council, as its record.</summary>
        public static string CouncilColumn(int rows, int paint)
        {
            return new string((char)('0' + Paint(paint)), Math.Max(1, Math.Min(MostRows, rows)));
        }

        /// <summary>
        /// The council's own rezoning of free land at a sitting: the use to zone for (the one with the strongest demand of RezoneHigh
        /// or more) and the use whose free land it takes (the weakest under RezoneLow among those with free land), or false. Demand
        /// unknown (-1) never rezones. Ties to the lower use.
        /// </summary>
        public static bool Rezone(int[] demand, int[] freeColumns, out int to, out int from)
        {
            to = from = -1;
            for (int u = 0; demand != null && u < demand.Length && u < (int)BuildUse.Count; u++)
            {
                if (demand[u] >= RezoneHigh && (to < 0 || demand[u] > demand[to]))
                {
                    to = u;
                }
            }
            for (int u = 0; to >= 0 && u < demand.Length && u < (int)BuildUse.Count; u++)
            {
                bool has = freeColumns != null && u < freeColumns.Length && freeColumns[u] > 0;
                if (u != to && has && demand[u] >= 0 && demand[u] < RezoneLow && (from < 0 || demand[u] < demand[from]))
                {
                    from = u;
                }
            }
            return to >= 0 && from >= 0;
        }

        /// <summary>The council's odds, in thousandths, of carrying a rezoning at a sitting for a use with this demand (the player's own rule's lift).</summary>
        public static int RezoneOdds(int demand) => Math.Max(0, Math.Min(1000, RezoneOddsBase + BuildModeRules.CouncilLift(demand)));

        // ------------------------------------------------------------------ the tools' picks on the grid

        /// <summary>The rows a column holds for the tool (the player's rows; 0 for a column not theirs), asked in order along a strip.</summary>
        public interface IHeld
        {
            int Held(int column);
        }

        /// <summary>
        /// Fill a block side: every square of the player's along the strip of <paramref name="column"/>, each column to the rows they
        /// hold, into <paramref name="into"/> (cleared). Returns how many.
        /// </summary>
        public static int FillSide(CellGrid grid, int column, IHeld held, List<CellAt> into)
        {
            into.Clear();
            if (grid == null || column < 0 || column >= grid.Columns.Count)
            {
                return 0;
            }
            int at = column;
            while (grid.Prev(at) >= 0)
            {
                at = grid.Prev(at);
            }
            for (; at >= 0; at = grid.Next(at))
            {
                int rows = Math.Min(grid.Columns[at].Rows, held.Held(at));
                for (int r = 0; r < rows; r++)
                {
                    into.Add(new CellAt(at, r));
                }
            }
            return into.Count;
        }

        /// <summary>
        /// A rectangle: along the strip of <paramref name="from"/> between its space and <paramref name="to"/>'s (read onto that strip,
        /// CellGrid.Project), the rows between theirs, the player's squares alone, into <paramref name="into"/> (cleared). Returns how many.
        /// </summary>
        public static int Rect(CellGrid grid, CellAt from, CellAt to, IHeld held, List<CellAt> into)
        {
            into.Clear();
            if (grid == null || from.None || to.None || from.Column >= grid.Columns.Count || to.Column >= grid.Columns.Count)
            {
                return 0;
            }
            CellColumn a = grid.Columns[from.Column], b = grid.Columns[to.Column];
            if (a.Strip != b.Strip)
            {
                return 0;
            }
            int low = Math.Min(a.Space, b.Space), high = Math.Max(a.Space, b.Space);
            int top = Math.Min(from.Row, to.Row), bottom = Math.Max(from.Row, to.Row);
            for (int space = low; space <= high; space++)
            {
                int c = grid.ColumnOf(a.Strip, space);
                if (c < 0)
                {
                    continue;
                }
                int rows = Math.Min(grid.Columns[c].Rows, held.Held(c));
                for (int r = top; r <= bottom && r < rows; r++)
                {
                    into.Add(new CellAt(c, r));
                }
            }
            return into.Count;
        }

        /// <summary>The brush: the one square under the pointer when it is the player's; false otherwise.</summary>
        public static bool Brush(CellGrid grid, CellAt at, IHeld held)
        {
            return grid != null && !at.None && at.Column < grid.Columns.Count && at.Row < Math.Min(grid.Columns[at.Column].Rows, held.Held(at.Column));
        }

        // ------------------------------------------------------------------ the text

        private static int Paint(int paint) => paint < 0 || paint > (int)BuildUse.Count ? 0 : paint;

        private static int Digit(char c, int otherwise)
        {
            int d = c - '0';
            return d >= 0 && d <= (int)BuildUse.Count ? d : Paint(otherwise);
        }

        // Where a column's group starts in the text, and its length; -1 past the last group.
        private static int GroupStart(string cells, int column, out int length)
        {
            length = 0;
            int start = 0;
            for (int c = 0; c < column; c++)
            {
                int gap = cells.IndexOf(Gap, start);
                if (gap < 0)
                {
                    return -1;
                }
                start = gap + 1;
            }
            int end = cells.IndexOf(Gap, start);
            length = (end < 0 ? cells.Length : end) - start;
            return start;
        }

        private static string Fill(string cells, int spaces, int[] rows, int sitePaint) => Text(Groups(cells, spaces, rows, sitePaint));

        // Every column's digits made whole for its rows, a square with none given the site's paint.
        private static char[][] Groups(string cells, int spaces, int[] rows, int sitePaint)
        {
            spaces = Math.Max(1, spaces);
            var groups = new char[spaces][];
            for (int c = 0; c < spaces; c++)
            {
                int n = Math.Max(1, Math.Min(MostRows, rows != null && c < rows.Length ? rows[c] : 1));
                groups[c] = new char[n];
                for (int r = 0; r < n; r++)
                {
                    groups[c][r] = (char)('0' + At(cells, c, r, sitePaint));
                }
            }
            return groups;
        }

        private static string Text(char[][] groups)
        {
            var t = new StringBuilder(groups.Length * 6);
            for (int c = 0; c < groups.Length; c++)
            {
                if (c > 0)
                {
                    t.Append(Gap);
                }
                t.Append(groups[c]);
            }
            return t.ToString();
        }

        /// <summary>A council record's key text, for the log: "strip.space".</summary>
        public static string KeyText(long key) => (key / 1000L).ToString(Inv) + "." + (key % 1000L).ToString(Inv);
    }
}
