using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The order, filter and totals of one table over row numbers, so any app's row class can use it: a stable merge sort,
    /// arrays made once so ordering again allocates nothing. A source's words are read only when an order needs them.
    /// </summary>
    public sealed class TableModel
    {
        private const int Small = 12;       // runs this short are ordered by insertion before they are merged

        private TableSpec _spec;
        private ITableSource _source;       // the rows' words come from here when an order needs them
        private readonly bool[] _wordsRead = new bool[TableSpec.MaxColumns];
        private int _rows;
        private double[][] _keys = new double[TableSpec.MaxColumns][];
        private string[][] _texts = new string[TableSpec.MaxColumns][];
        private readonly int[][] _kinds = new int[TableSpec.MaxFilters][];
        private readonly int[][] _counts = new int[TableSpec.MaxFilters][];
        private bool[] _pinned = new bool[0], _left = new bool[0];
        private int[] _order = new int[0], _scratch = new int[0];
        private readonly double[] _totals = new double[TableSpec.MaxColumns];
        private int _shown, _pinnedShown;

        // Read by Compare while a sort runs.
        private double[] _k;
        private string[] _t;
        private int _direction;
        private bool _byText;

        public TableSpec Spec => _spec;
        public int Rows => _rows;
        /// <summary>How many of them are shown: the pinned ones and those the filters let through.</summary>
        public int Count => _shown;
        /// <summary>How many of the shown rows are pinned (they come first).</summary>
        public int Pinned => _pinnedShown;
        public int RowAt(int place) => _order[place];
        /// <summary>The sum of a column over the rows shown; only columns declared with 'sums' are added up.</summary>
        public double Total(int column) => _totals[column];

        /// <summary>Starts a table of so many rows, each with no number, words or kind, not pinned.</summary>
        public void Begin(TableSpec spec, int rows)
        {
            _spec = spec;
            _source = null;
            Array.Clear(_wordsRead, 0, _wordsRead.Length);
            _rows = Math.Max(0, rows);
            for (int c = 0; c < spec.Count; c++)
            {
                if (spec.Columns[c].Sort == TableSort.None && !spec.Columns[c].Sums)
                {
                    continue;
                }
                _keys[c] = Room(_keys[c], _rows);
                _texts[c] = Room(_texts[c], _rows);
                double[] keys = _keys[c];
                string[] texts = _texts[c];
                for (int r = 0; r < _rows; r++)
                {
                    keys[r] = double.NaN;
                    texts[r] = null;
                }
            }
            for (int g = 0; g < spec.FilterCount; g++)
            {
                _kinds[g] = Room(_kinds[g], _rows);
                int[] kinds = _kinds[g];
                for (int r = 0; r < _rows; r++)
                {
                    kinds[r] = -1;
                }
            }
            _pinned = Room(_pinned, _rows);
            _left = Room(_left, _rows);
            for (int r = 0; r < _rows; r++)
            {
                _pinned[r] = false;
                _left[r] = false;
            }
            _order = Room(_order, _rows);
            _scratch = Room(_scratch, _rows);
            _shown = _pinnedShown = 0;
        }

        /// <summary>A table of rows an app wrote; a line of words and a pinned row come first unless the table neither sorts nor filters.</summary>
        public void Begin(TableSpec spec, List<TableRow> rows)
        {
            Begin(spec, rows.Count);
            bool ordered = spec.Sortable > 0 || spec.FilterCount > 0;
            for (int r = 0; r < rows.Count; r++)
            {
                TableRow row = rows[r];
                if (ordered && (row.Info || row.Pinned))
                {
                    Pin(r);
                    continue;
                }
                for (int c = 0; c < spec.Count; c++)
                {
                    if (spec.Columns[c].Sort != TableSort.None || spec.Columns[c].Sums)
                    {
                        Set(r, c, row.KeyAt(c), row.TextAt(c));
                    }
                }
                Kind(r, 0, row.Kind);
                Kind(r, 1, row.Kind2);
                if (row.NoTotal)
                {
                    Leave(r);
                }
            }
        }

        /// <summary>A table written from a source: figures and kinds now, words when an order needs them.</summary>
        public void Begin(TableSpec spec, ITableSource source)
        {
            int rows = source != null ? source.Count : 0;
            Begin(spec, rows);
            _source = source;
            for (int c = 0; c < spec.Count; c++)
            {
                if (spec.Columns[c].Sort == TableSort.None && !spec.Columns[c].Sums)
                {
                    continue;
                }
                double[] keys = _keys[c];
                for (int r = 0; r < rows; r++)
                {
                    keys[r] = source.Key(r, c);
                }
            }
            for (int g = 0; g < spec.FilterCount; g++)
            {
                int[] kinds = _kinds[g];
                for (int r = 0; r < rows; r++)
                {
                    kinds[r] = source.Kind(r, g);
                }
            }
        }

        // A column's words from the source where they decide the order: every row of a column ordered by its words, the rows
        // without a figure in any other (Compare). Once a column, until the table is written again.
        private void Words(int column, bool byText)
        {
            if (_source == null || _wordsRead[column])
            {
                return;
            }
            _wordsRead[column] = true;
            double[] keys = _keys[column];
            string[] texts = _texts[column];
            for (int r = 0; r < _rows; r++)
            {
                if (byText || double.IsNaN(keys[r]))
                {
                    texts[r] = _source.Text(r, column);
                }
            }
        }

        private static T[] Room<T>(T[] array, int rows)
        {
            if (array != null && array.Length >= rows)
            {
                return array;
            }
            return new T[Math.Max(rows, array != null ? array.Length * 2 : 16)];
        }

        /// <summary>What a row gives for a column: a number (NaN for none) and its words.</summary>
        public void Set(int row, int column, double key, string text)
        {
            if (row < 0 || row >= _rows || column < 0 || column >= _spec.Count || _keys[column] == null)
            {
                return;
            }
            TableColumn c = _spec.Columns[column];
            if (c.Sort == TableSort.None && !c.Sums)
            {
                return;
            }
            _keys[column][row] = key;
            _texts[column][row] = text;
        }

        /// <summary>The kind a row is of for one filter (-1: none, shown only under "all").</summary>
        public void Kind(int row, int filter, int kind)
        {
            if (row >= 0 && row < _rows && filter >= 0 && filter < _spec.FilterCount)
            {
                _kinds[filter][row] = kind;
            }
        }

        /// <summary>A row that always shows first, whatever the order and the filter.</summary>
        public void Pin(int row)
        {
            if (row >= 0 && row < _rows)
            {
                _pinned[row] = true;
            }
        }

        /// <summary>A row ordered and filtered like any other but in no total (an offer among deals).</summary>
        public void Leave(int row)
        {
            if (row >= 0 && row < _rows)
            {
                _left[row] = true;
            }
        }

        /// <summary>How many rows are of a kind, whatever is filtered now.</summary>
        public int KindCount(int filter, int kind)
        {
            int[] counts = filter >= 0 && filter < _spec.FilterCount ? _counts[filter] : null;
            return counts != null && kind >= 0 && kind < counts.Length ? counts[kind] : 0;
        }

        /// <summary>How many different kinds the rows are of: a filter is worth showing from two.</summary>
        public int KindsHeld(int filter)
        {
            int held = 0;
            int[] counts = filter >= 0 && filter < _spec.FilterCount ? _counts[filter] : null;
            int kinds = counts != null ? Math.Min(counts.Length, _spec.Filters[filter].Kinds) : 0;
            for (int k = 0; k < kinds; k++)
            {
                held += counts[k] > 0 ? 1 : 0;
            }
            return held;
        }

        /// <summary>Works out the rows shown, their order and totals; a chosen kind left with no rows goes back to "all".</summary>
        public void Apply()
        {
            TableSpec spec = _spec;
            for (int g = 0; g < spec.FilterCount; g++)
            {
                TableFilter filter = spec.Filters[g];
                int kinds = filter.Kinds;
                _counts[g] = Room(_counts[g], kinds);
                int[] counts = _counts[g];
                for (int k = 0; k < counts.Length; k++)
                {
                    counts[k] = 0;
                }
                int[] of = _kinds[g];
                for (int r = 0; r < _rows; r++)
                {
                    if (!_pinned[r] && of[r] >= 0 && of[r] < kinds)
                    {
                        counts[of[r]]++;
                    }
                }
                if (filter.OwnNames && filter.ChosenName != null)
                {
                    filter.Chosen = filter.Names.IndexOf(filter.ChosenName);
                    if (filter.Chosen < 0)
                    {
                        filter.ChosenName = null;
                    }
                }
                if (filter.Chosen >= kinds || (filter.Chosen >= 0 && counts[filter.Chosen] == 0) || KindsHeld(g) < 2)
                {
                    filter.Chosen = -1;
                    filter.ChosenName = null;
                }
            }

            int n = 0;
            for (int r = 0; r < _rows; r++)
            {
                if (_pinned[r])
                {
                    _order[n++] = r;
                }
            }
            _pinnedShown = n;
            for (int r = 0; r < _rows; r++)
            {
                if (_pinned[r])
                {
                    continue;
                }
                bool passes = true;
                for (int g = 0; g < spec.FilterCount && passes; g++)
                {
                    int chosen = spec.Filters[g].Chosen;
                    passes = chosen < 0 || _kinds[g][r] == chosen;
                }
                if (passes)
                {
                    _order[n++] = r;
                }
            }
            _shown = n;

            int column = spec.OrderColumn;
            if (column >= 0 && column < spec.Count && spec.Columns[column].Sort != TableSort.None && _shown - _pinnedShown > 1)
            {
                _byText = spec.Columns[column].Sort == TableSort.Text;
                Words(column, _byText);
                _k = _keys[column];
                _t = _texts[column];
                _direction = spec.OrderAscending ? 1 : -1;
                Sort(_pinnedShown, _shown);
                _k = null;
                _t = null;
            }

            for (int c = 0; c < spec.Count; c++)
            {
                double sum = 0;
                if (spec.Columns[c].Sums)
                {
                    double[] keys = _keys[c];
                    for (int i = _pinnedShown; i < _shown; i++)
                    {
                        double v = keys[_order[i]];
                        if (!double.IsNaN(v) && !_left[_order[i]])
                        {
                            sum += v;
                        }
                    }
                }
                _totals[c] = sum;
            }
        }

        /// <summary>A header was clicked. False, and nothing done, if the column does not sort.</summary>
        public bool SortBy(int column)
        {
            if (_spec == null || !_spec.Click(column))
            {
                return false;
            }
            Apply();
            return true;
        }

        /// <summary>A kind was chosen in a filter (-1: all).</summary>
        public void Choose(int filter, int kind)
        {
            if (_spec == null || filter < 0 || filter >= _spec.FilterCount)
            {
                return;
            }
            TableFilter f = _spec.Filters[filter];
            f.Chosen = kind >= 0 && kind < f.Kinds ? kind : -1;
            f.ChosenName = f.OwnNames && f.Chosen >= 0 ? f.Names[f.Chosen] : null;
            Apply();
        }

        /// <summary>Where a row is shown, or -1 if the filter has taken it out.</summary>
        public int PlaceOf(int row)
        {
            for (int i = 0; i < _shown; i++)
            {
                if (_order[i] == row)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The row to select after a change: the selected one if it still shows, else the first unpinned; -1 for none.</summary>
        public int Keep(int selectedRow)
        {
            if (selectedRow < 0)
            {
                return -1;
            }
            if (PlaceOf(selectedRow) >= 0)
            {
                return selectedRow;
            }
            return _shown > _pinnedShown ? _order[_pinnedShown] : -1;
        }

        // Bottom-up merge sort of _order[from..to): short runs by insertion, then merged through _scratch. Stable.
        private void Sort(int from, int to)
        {
            int[] a = _order, b = _scratch;
            for (int lo = from; lo < to; lo += Small)
            {
                int hi = Math.Min(lo + Small, to);
                for (int i = lo + 1; i < hi; i++)
                {
                    int row = a[i];
                    int j = i - 1;
                    while (j >= lo && Compare(a[j], row) > 0)
                    {
                        a[j + 1] = a[j];
                        j--;
                    }
                    a[j + 1] = row;
                }
            }
            for (int width = Small; width < to - from; width *= 2)
            {
                for (int lo = from; lo < to; lo += 2 * width)
                {
                    int mid = Math.Min(lo + width, to), hi = Math.Min(lo + 2 * width, to);
                    int i = lo, j = mid, k = lo;
                    while (i < mid && j < hi)
                    {
                        b[k++] = Compare(a[j], a[i]) < 0 ? a[j++] : a[i++];
                    }
                    while (i < mid)
                    {
                        b[k++] = a[i++];
                    }
                    while (j < hi)
                    {
                        b[k++] = a[j++];
                    }
                }
                int[] swap = a;
                a = b;
                b = swap;
            }
            if (a != _order)
            {
                Array.Copy(a, from, _order, from, to - from);
            }
        }

        // Less than 0 if row x comes first. A row with nothing to compare comes last in both directions; numbers before words.
        private int Compare(int x, int y)
        {
            if (!_byText)
            {
                double a = _k[x], b = _k[y];
                bool noA = double.IsNaN(a), noB = double.IsNaN(b);
                if (!noA && !noB)
                {
                    return a < b ? -_direction : a > b ? _direction : 0;
                }
                if (noA != noB)
                {
                    return noA ? 1 : -1;
                }
            }
            string s = _t[x], t = _t[y];
            bool noS = string.IsNullOrEmpty(s), noT = string.IsNullOrEmpty(t);
            if (noS || noT)
            {
                return noS == noT ? 0 : noS ? 1 : -1;
            }
            int c = string.Compare(s, t, StringComparison.OrdinalIgnoreCase);
            return c < 0 ? -_direction : c > 0 ? _direction : 0;
        }
    }
}
