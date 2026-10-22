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
        public bool Flexible;       // takes the room that is left
        public TableSort Sort;
        public bool Sums;           // the totals line adds it up over the rows shown
        public bool Right;          // set to the right, as the game sets its last column
    }

    /// <summary>
    /// One "filter by" of a table: fixed kinds (KindKeys, locale keys) or the app's own for this opening (Names). The choice
    /// is kept for the session; with the app's own names, by name, since their numbers change with the save.
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
    /// A table declared as data: its columns, filters, first order and the sentence shown when a filter leaves nothing.
    /// One static object per table, so it also keeps the player's last order and kinds until the game closes, never in
    /// the save. Game-free: the offline checks compile this file.
    ///
    ///   static readonly TableSpec Accounts = new TableSpec("Finance", "Accounts")
    ///       .Column("reallife_econ_col_account", 520f, TableSort.Text, flexible: true)
    ///       .Column("reallife_econ_col_owed", 240f, TableSort.Money, sums: true)
    ///       .Filter("reallife_tab_all_kinds", KindKeys)
    ///       .Order(1, false)
    ///       .Empty("reallife_tab_empty_debts");
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

        // The player's last choice, for the session.
        public int SortColumn = -1;
        public bool Ascending = true;
        public bool Touched;                // a header has been clicked: the first order no longer applies
        public bool Reported;               // the self-check's line for this table has been written
        public int Slot = -1;               // HotSpots slot of its sorts and filters (TableView); -1 until the first

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

        /// <summary>A filter over fixed kinds (locale keys), "all" first.</summary>
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

        /// <summary>The order now: the player's choice, else the first order.</summary>
        public int OrderColumn => Touched ? SortColumn : FirstColumn;
        public bool OrderAscending => Touched ? Ascending : FirstAscending;

        /// <summary>
        /// A header was clicked, as the game's table takes it (BaTable.ChangeSortOrder): the same column turns the order round,
        /// another starts ascending. False if the column does not sort.
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
}
