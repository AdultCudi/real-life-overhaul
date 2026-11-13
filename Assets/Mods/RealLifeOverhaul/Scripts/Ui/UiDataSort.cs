using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Sorting a big table by any of its sortable columns, for a table that has no order of its own to keep (a market's companies, a list of
    /// properties or services): a press on a head picks the column or turns its order round, and the rows are put in order by the figure each
    /// row gives for it (<see cref="UiDataRow.Sorts"/>), or by its words where it gives none. A row with no figure for the column stands last
    /// whichever way the order runs. The sort is stable, so rows that tie keep the order they came in. Game-free. Cost: one pass a sort, at the
    /// time the screen builds its rows.
    /// </summary>
    public static class UiDataSort
    {
        /// <summary>A sort key that says the row has no figure in the column.</summary>
        public const float None = float.NaN;

        /// <summary>True for a column whose first press puts the highest first (a figure, a sum, a date, a trend); words start at the front of the alphabet.</summary>
        public static bool StartsDown(UiCellKind kind)
        {
            return kind != UiCellKind.Text && kind != UiCellKind.Tile && kind != UiCellKind.Person && kind != UiCellKind.Chip;
        }

        /// <summary>
        /// A press on a column's head: the column already sorted by turns its order round, any other sortable column is sorted by in its
        /// first order. False, and nothing changed, for a column that is not there or does not sort.
        /// </summary>
        public static bool Press(UiDataColumn[] columns, ref int column, ref bool down, int pressed)
        {
            if (columns == null || pressed < 0 || pressed >= columns.Length || !columns[pressed].Sortable)
            {
                return false;
            }
            if (pressed == column)
            {
                down = !down;
            }
            else
            {
                column = pressed;
                down = StartsDown(columns[pressed].Kind);
            }
            return true;
        }

        /// <summary>
        /// Puts a table's rows in order by its <see cref="UiDataTableModel.SortColumn"/> and <see cref="UiDataTableModel.SortDown"/>. A table with no
        /// column chosen is left as it is. The chosen row keeps the row it was.
        /// </summary>
        public static void Sort(UiDataTableModel m)
        {
            int n = Math.Min(m.RowCount, m.Rows.Length);
            int column = m.SortColumn;
            if (n < 2 || column < 0 || column >= m.Columns.Length)
            {
                return;
            }
            var order = new int[n];
            var keys = new float[n];
            var words = new string[n];
            bool figures = false;
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
                float[] sorts = m.Rows[i].Sorts;
                keys[i] = sorts != null && column < sorts.Length ? sorts[column] : None;
                figures |= !float.IsNaN(keys[i]);
                string[] cells = m.Rows[i].Cells;
                words[i] = cells != null && column < cells.Length ? cells[column] ?? string.Empty : string.Empty;
            }
            bool down = m.SortDown;
            Array.Sort(order, (a, b) =>
            {
                int c = figures ? Figures(keys[a], keys[b], down) : Words(words[a], words[b], down);
                return c != 0 ? c : a.CompareTo(b);
            });
            var rows = new UiDataRow[m.Rows.Length];
            int selected = m.Selected;
            for (int i = 0; i < n; i++)
            {
                rows[i] = m.Rows[order[i]];
                if (order[i] == m.Selected)
                {
                    selected = i;
                }
            }
            for (int i = n; i < rows.Length; i++)
            {
                rows[i] = m.Rows[i];
            }
            m.Rows = rows;
            m.Selected = selected;
        }

        private static int Figures(float a, float b, bool down)
        {
            bool na = float.IsNaN(a), nb = float.IsNaN(b);
            if (na || nb)
            {
                return na == nb ? 0 : na ? 1 : -1;   // no figure stands last either way
            }
            int c = a.CompareTo(b);
            return down ? -c : c;
        }

        private static int Words(string a, string b, bool down)
        {
            bool ea = string.IsNullOrEmpty(a), eb = string.IsNullOrEmpty(b);
            if (ea || eb)
            {
                return ea == eb ? 0 : ea ? 1 : -1;
            }
            int c = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
            return down ? -c : c;
        }
    }
}
