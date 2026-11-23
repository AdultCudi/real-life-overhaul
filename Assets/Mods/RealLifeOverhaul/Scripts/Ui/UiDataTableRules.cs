using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Rules for a table's columns that need no game: a column of words, figures, money, dates, chips or changes that holds nothing in any
    /// row is left out (it only takes the room of the columns that have something to say), and a column that holds a value in some rows
    /// shows a dash where a row has none, so a blank cell never reads as a missing figure. Game-free.
    /// </summary>
    public static class UiDataTableRules
    {
        /// <summary>What a cell that does not apply to its row shows.</summary>
        public const string None = "-";

        /// <summary>
        /// Leaves out every column (but the first) whose cells are blank in every row, keeping the sort, the totals and each row's card with the
        /// columns that stay. The columns it can leave out are those of words and figures; a meter, a tile, a person, a spark and the like draw
        /// from their own values. A table with no rows keeps its head. Returns how many columns were left out.
        /// </summary>
        public static int LeaveOutBlank(UiDataTableModel t)
        {
            if (t == null || t.Rows == null || t.Rows.Length == 0 || t.Columns.Length < 2)
            {
                return 0;
            }
            var keep = new List<int>(t.Columns.Length);
            for (int c = 0; c < t.Columns.Length; c++)
            {
                if (c == 0 || !Plain(t.Columns[c].Kind) || AnyValue(t, c))
                {
                    keep.Add(c);
                }
            }
            int gone = t.Columns.Length - keep.Count;
            if (gone == 0)
            {
                return 0;
            }
            t.SortColumn = keep.IndexOf(t.SortColumn);
            t.Columns = Pick(t.Columns, keep);
            if (t.Totals != null)
            {
                t.Totals = Pick(t.Totals, keep);
            }
            for (int r = 0; r < t.Rows.Length; r++)
            {
                UiDataRow row = t.Rows[r];
                row.Cells = Pick(row.Cells, keep);
                row.Values = Pick(row.Values, keep);
                row.Tones = Pick(row.Tones, keep);
                row.Subs = Pick(row.Subs, keep);
                row.Pictures = Pick(row.Pictures, keep);
                row.SortKeys = Pick(row.SortKeys, keep);
                row.Sorts = Pick(row.Sorts, keep);
                if (row.Card != null)
                {
                    row.Card.Column = Math.Max(0, keep.IndexOf(row.Card.Column));
                }
                t.Rows[r] = row;
            }
            return gone;
        }

        /// <summary>
        /// Puts <see cref="None"/> in a text, figure, money, date or chip column's blank cells where some row of the column holds a value, so a
        /// row it does not apply to (a rival's ad has no posts to fill) is a dash and not a gap. A column no row has a value in is left to
        /// <see cref="LeaveOutBlank"/>, unless <paramref name="always"/> (a table whose columns cannot be left out, as its sort and its
        /// rows are numbered by column): every blank cell of such a column is a dash. Returns how many cells it filled.
        /// </summary>
        public static int DashBlank(UiDataTableModel t, int fromColumn = 1, bool always = false, string mark = None)
        {
            int filled = 0;
            if (t == null || t.Rows == null)
            {
                return 0;
            }
            for (int c = Math.Max(0, fromColumn); c < t.Columns.Length; c++)
            {
                if (!Plain(t.Columns[c].Kind) || t.Columns[c].Kind == UiCellKind.Trend || (!always && !AnyValue(t, c)))
                {
                    continue;
                }
                for (int r = 0; r < t.Rows.Length; r++)
                {
                    string[] cells = t.Rows[r].Cells;
                    if (cells != null && c < cells.Length && string.IsNullOrWhiteSpace(cells[c]))
                    {
                        cells[c] = mark;
                        filled++;
                    }
                }
            }
            return filled;
        }

        private static bool Plain(UiCellKind k) => k == UiCellKind.Text || k == UiCellKind.Figure || k == UiCellKind.Money || k == UiCellKind.Date || k == UiCellKind.Chip
                                                  || k == UiCellKind.Trend;

        private static bool AnyValue(UiDataTableModel t, int c)
        {
            for (int r = 0; r < t.Rows.Length; r++)
            {
                UiDataRow row = t.Rows[r];
                if (row.Cells != null && c < row.Cells.Length && !string.IsNullOrWhiteSpace(row.Cells[c]) && row.Cells[c] != None)
                {
                    return true;
                }
                if (t.Columns[c].Kind == UiCellKind.Trend && row.Values != null && c < row.Values.Length && row.Values[c] != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static T[] Pick<T>(T[] from, List<int> keep)
        {
            if (from == null)
            {
                return null;
            }
            var to = new T[keep.Count];
            for (int i = 0; i < keep.Count; i++)
            {
                to[i] = keep[i] < from.Length ? from[keep[i]] : default(T);
            }
            return to;
        }
    }
}
