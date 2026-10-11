using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>How a column is ordered when its header is clicked.</summary>
    public enum TableSort
    {
        /// <summary>The header is no button: nothing sensible to order by.</summary>
        None,
        /// <summary>By the cell's words, capitals and small letters alike; an empty cell comes last.</summary>
        Text,
        /// <summary>By a number the row gives (a count, a share, a rate).</summary>
        Number,
        /// <summary>By an amount of money the row gives; a column of money may also be summed.</summary>
        Money,
        /// <summary>By a day (or a minute) the row gives.</summary>
        Date,
        /// <summary>By whatever the app gives: a number where the row has one, else the cell's words (numbers first).</summary>
        Custom
    }

    /// <summary>One column of a table, as data: its title, its width, how it sorts and whether it is summed.</summary>
    public sealed class TableColumn
    {
        public string TitleKey;
        public float Width;
        public bool Flexible;       // takes the room that is left (one column of a table)
        public TableSort Sort;
        public bool Sums;           // the totals line adds this column up over the rows shown
        public bool Right;          // set to the right, as the game sets its last column
    }

    /// <summary>
    /// One "filter by" of a table: the kinds its rows can be of. The kinds are fixed (KindKeys, translated when
    /// shown) or the app's own for this opening (Names: businesses, sectors). What is chosen is kept here for the
    /// session; a list of the app's own keeps the name chosen, since its numbers change with the save.
    /// </summary>
    public sealed class TableFilter
    {
        public string AllKey;
        public string[] KindKeys;
        public readonly List<string> Names = new List<string>(16);
        public int Chosen = -1;             // -1: all
        public string ChosenName;

        public bool OwnNames => KindKeys == null;
        public int Kinds => KindKeys != null ? KindKeys.Length : Names.Count;
    }

    /// <summary>
    /// A table declared as data: its columns, what it can be filtered by, its first order, and the sentence shown
    /// when a filter leaves nothing. One static object per table of an app, so it also keeps what the player last
    /// chose there (the column, the direction, the kinds) until the game is closed: a convenience for whoever is
    /// playing, never part of the save.
    ///
    ///   static readonly TableSpec Accounts = new TableSpec("Finance", "Accounts")
    ///       .Column("reallife_econ_col_account", 520f, TableSort.Text, flexible: true)
    ///       .Column("reallife_econ_col_owed", 240f, TableSort.Money, sums: true)
    ///       .Filter("reallife_tab_all_kinds", KindKeys)
    ///       .Order(1, false)
    ///       .Empty("reallife_tab_empty_debts");
    ///
    /// No Unity type and no type of the game's is used here: the offline checks run this file as it is.
    /// </summary>
    public sealed class TableSpec
    {
        public const int MaxColumns = 9;    // the header row of the game's table has nine cells
        public const int MaxFilters = 2;

        public readonly string App, Tab;
        public readonly TableColumn[] Columns = new TableColumn[MaxColumns];
        public readonly TableFilter[] Filters = new TableFilter[MaxFilters];
        public int Count, FilterCount;
        public int FirstColumn = -1;        // the order on opening: -1 for the order the rows were written in
        public bool FirstAscending = true;
        public string EmptyKey;

        // What the player last chose here (the session's memory).
        public int SortColumn = -1;
        public bool Ascending = true;
        public bool Touched;                // a header has been clicked: the first order no longer applies
        public bool Reported;               // the self-check's line for this table has been written this session

        public TableSpec(string app, string tab)
        {
            App = app;
            Tab = tab;
        }

        public string Id => App + ", " + Tab;

        public TableSpec Column(string titleKey, float width, TableSort sort = TableSort.None, bool flexible = false, bool sums = false, bool right = false)
        {
            if (Count < MaxColumns)
            {
                Columns[Count++] = new TableColumn { TitleKey = titleKey, Width = width, Sort = sort, Flexible = flexible, Sums = sums, Right = right };
            }
            return this;
        }

        /// <summary>A filter over fixed kinds (their names are locale keys), "all" first.</summary>
        public TableSpec Filter(string allKey, string[] kindKeys)
        {
            if (FilterCount < MaxFilters)
            {
                Filters[FilterCount++] = new TableFilter { AllKey = allKey, KindKeys = kindKeys };
            }
            return this;
        }

        /// <summary>A filter over kinds the app names each time it writes the table (TableFilter.Names).</summary>
        public TableSpec Filter(string allKey)
        {
            return Filter(allKey, null);
        }

        public TableSpec Order(int column, bool ascending)
        {
            FirstColumn = column;
            FirstAscending = ascending;
            return this;
        }

        public TableSpec Empty(string key)
        {
            EmptyKey = key;
            return this;
        }

        public int Sortable
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++)
                {
                    n += Columns[i].Sort != TableSort.None ? 1 : 0;
                }
                return n;
            }
        }

        public bool HasTotals
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Columns[i].Sums)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The column the table is ordered by now, and the direction: the player's choice, else the first order.</summary>
        public int OrderColumn => Touched ? SortColumn : FirstColumn;
        public bool OrderAscending => Touched ? Ascending : FirstAscending;

        /// <summary>
        /// A header was clicked, as the game's own table takes it (BaTable.ChangeSortOrder): the same column again
        /// turns the order round, another column starts ascending. False if the column does not sort.
        /// </summary>
        public bool Click(int column)
        {
            if (column < 0 || column >= Count || Columns[column].Sort == TableSort.None)
            {
                return false;
            }
            bool same = OrderColumn == column;
            Ascending = same ? !OrderAscending : true;
            SortColumn = column;
            Touched = true;
            return true;
        }
    }

    /// <summary>
    /// The order, the filter and the totals of one table: which rows are shown and in what order, worked out from
    /// what each row gives for each column (a number, or its words) and the kind it is of. Rows are numbers here
    /// (0, 1, 2... in the order they were written), so any app's own row class can use it.
    ///
    /// Everything is kept in arrays that are made once and used again: after the first time a table of some size has
    /// been ordered, ordering it again allocates nothing. The sort is a merge sort on the array of row numbers, so
    /// rows that compare equal stay in the order they were written (stable), in both directions. It runs when the
    /// rows are written and when the player clicks a header or picks a kind, never otherwise.
    /// </summary>
    public sealed class TableModel
    {
        private const int Small = 12;       // runs this short are ordered by insertion before they are merged

        private TableSpec _spec;
        private int _rows;
        private double[][] _keys = new double[TableSpec.MaxColumns][];
        private string[][] _texts = new string[TableSpec.MaxColumns][];
        private readonly int[][] _kinds = new int[TableSpec.MaxFilters][];
        private readonly int[][] _counts = new int[TableSpec.MaxFilters][];
        private bool[] _pinned = new bool[0], _left = new bool[0];
        private int[] _order = new int[0], _scratch = new int[0];
        private readonly double[] _totals = new double[TableSpec.MaxColumns];
        private int _shown, _pinnedShown;

        // What a comparison reads while a sort runs.
        private double[] _k;
        private string[] _t;
        private int _direction;
        private bool _byText;

        public TableSpec Spec => _spec;
        /// <summary>How many rows were written.</summary>
        public int Rows => _rows;
        /// <summary>How many of them are shown: the pinned ones and those the filters let through.</summary>
        public int Count => _shown;
        /// <summary>How many of the shown rows are pinned (they come first).</summary>
        public int Pinned => _pinnedShown;
        /// <summary>The row shown at a place.</summary>
        public int RowAt(int place) => _order[place];
        /// <summary>The sum of a column over the rows shown; only columns declared with 'sums' are added up.</summary>
        public double Total(int column) => _totals[column];

        /// <summary>Starts a table of so many rows: every row has no number, no words, no kind, and is not pinned.</summary>
        public void Begin(TableSpec spec, int rows)
        {
            _spec = spec;
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

        /// <summary>The kind a row is of, for one of the table's filters (-1: of none, shown only under "all").</summary>
        public void Kind(int row, int filter, int kind)
        {
            if (row >= 0 && row < _rows && filter >= 0 && filter < _spec.FilterCount)
            {
                _kinds[filter][row] = kind;
            }
        }

        /// <summary>A row that always shows, first, whatever the order and the filter: a line that only says something.</summary>
        public void Pin(int row)
        {
            if (row >= 0 && row < _rows)
            {
                _pinned[row] = true;
            }
        }

        /// <summary>A row that is ordered and filtered like any other but is in no total (an offer among deals: money not yet put in).</summary>
        public void Leave(int row)
        {
            if (row >= 0 && row < _rows)
            {
                _left[row] = true;
            }
        }

        /// <summary>How many rows are of a kind (whatever is filtered now).</summary>
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

        /// <summary>
        /// Works out which rows show and in what order, and the totals. A kind that was chosen and that no row is
        /// of any more (or a filter with fewer than two kinds left, which is not offered) goes back to "all": the
        /// player is never left with an empty table and no way out of it.
        /// </summary>
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
                _k = _keys[column];
                _t = _texts[column];
                _byText = spec.Columns[column].Sort == TableSort.Text;
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

        /// <summary>A header was clicked. False (and nothing done) if that column does not sort.</summary>
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

        /// <summary>
        /// The row to have selected after the order or the filter has changed: the one that was, if it still shows;
        /// else the first row that is not pinned; -1 if nothing was selected or nothing is left.
        /// </summary>
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

        // ---------------------------------------------------------------- the sort

        // A merge sort from the bottom up over _order[from..to): short runs by insertion, then merged pair by pair
        // through _scratch. Both steps leave equal rows as they were, so the order is stable.
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

        // Less than 0 if row x comes before row y. A row with nothing to compare (no number, no words) comes last
        // in both directions; numbers come before words where a column has both.
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
