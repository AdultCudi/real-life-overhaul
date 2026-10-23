using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Localizor;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The layer between a table's model (TableModel) and the game's own header buttons and drop-down on the copy of
    /// MyEmployees, one per app: Begin, the model's figures, Apply, and Changed when the player clicks. Without the game's
    /// controls the table has plain headers and no filter.
    /// </summary>
    public sealed class TableView
    {
        public readonly TableModel Model = new TableModel();
        /// <summary>The player changed the order (false) or the filter (true): the owner shows its rows again.</summary>
        public Action<bool> Changed;

        private readonly AppShell _shell;
        private readonly FilterDrop[] _drops = new FilterDrop[TableSpec.MaxFilters];
        private readonly List<string>[] _options = new List<string>[TableSpec.MaxFilters];
        private readonly List<int>[] _optionKinds = new List<int>[TableSpec.MaxFilters];
        private readonly Action<int>[] _pickers = new Action<int>[TableSpec.MaxFilters];
        private bool _filtersOff;
        private int _rowCells = TableSpec.MaxColumns;

        public TableView(AppShell shell)
        {
            _shell = shell;
            shell.HeaderClicked = HeaderClicked;
            for (int g = 0; g < TableSpec.MaxFilters; g++)
            {
                int filter = g;
                _options[g] = new List<string>(16);
                _optionKinds[g] = new List<int>(16);
                _pickers[g] = option => Picked(filter, option);
            }
        }

        /// <summary>How many cells of text the app's row has, for the self-check.</summary>
        public int RowCells
        {
            set { _rowCells = value; }
        }

        /// <summary>A filter can be shown: the game's drop-down was there to copy and has not failed.</summary>
        public bool CanFilter => !_filtersOff && _shell.FilterTemplate != null;

        /// <summary>A table is about to be written: its columns on the header cells, the model emptied for its rows.</summary>
        public void Begin(TableSpec spec, int rows)
        {
            _shell.SetColumns(spec);
            Model.Begin(spec, rows);
        }

        /// <summary>A table of rows the app has written: columns on the header cells, figures and kinds into the model.</summary>
        public void Begin(TableSpec spec, List<TableRow> rows)
        {
            _shell.SetColumns(spec);
            Model.Begin(spec, rows);
        }

        /// <summary>A table written from a source when shown: columns on the header cells, figures and kinds into the model.</summary>
        public void Begin(TableSpec spec, ITableSource source)
        {
            _shell.SetColumns(spec);
            Model.Begin(spec, source);
        }

        /// <summary>The rows are in: order, filter and totals worked out, and the game's controls show them.</summary>
        public void Apply()
        {
            Model.Apply();
            Shown();
            Report();
        }

        // The arrow on the ordered column, and a drop-down for each filter the rows have two kinds for.
        private void Shown()
        {
            TableSpec spec = Model.Spec;
            int column = spec.OrderColumn;
            _shell.ShowSort(column >= 0 && column < spec.Count && spec.Columns[column].Sort != TableSort.None ? column : -1, spec.OrderAscending);
            for (int g = 0; g < TableSpec.MaxFilters; g++)
            {
                bool wanted = g < spec.FilterCount && CanFilter && Model.KindsHeld(g) >= 2;
                if (!wanted && _drops[g] == null)
                {
                    continue;
                }
                try
                {
                    if (wanted)
                    {
                        Offer(spec, g);
                    }
                    else
                    {
                        _drops[g].Show(false);
                    }
                }
                catch (Exception e)
                {
                    FiltersFailed(e);
                }
            }
        }

        // "All" first, then each kind the table holds, as EconoView lists its transaction types.
        private void Offer(TableSpec spec, int g)
        {
            if (_drops[g] == null)
            {
                _drops[g] = FilterDrop.Make(_shell.FilterTemplate, _shell.List != null ? _shell.List.parent : null, g, "Filter" + g);
                if (_drops[g] == null)
                {
                    throw new InvalidOperationException("the copy of the game's drop-down is no drop-down");
                }
            }
            TableFilter filter = spec.Filters[g];
            List<string> options = _options[g];
            List<int> kinds = _optionKinds[g];
            options.Clear();
            kinds.Clear();
            options.Add(filter.AllKey.GetLocalization());
            kinds.Add(-1);
            int chosen = 0;
            for (int k = 0; k < filter.Kinds; k++)
            {
                if (Model.KindCount(g, k) == 0)
                {
                    continue;
                }
                if (k == filter.Chosen)
                {
                    chosen = options.Count;
                }
                options.Add(filter.OwnNames ? filter.Names[k] : filter.KindKeys[k].GetLocalization());
                kinds.Add(k);
            }
            _drops[g].Set(options, chosen, _pickers[g]);
            _drops[g].Show(true);
        }

        // The game's drop-down did something unexpected: the tables go on without filters, everything shown.
        private void FiltersFailed(Exception e)
        {
            if (_filtersOff)
            {
                return;
            }
            _filtersOff = true;
            AppShell.Logger?.Error(e);
            UiClone.Fallback("the game's drop-down could not be used as a table's filter, so the tables are shown without one");
            for (int g = 0; g < TableSpec.MaxFilters; g++)
            {
                try
                {
                    if (_drops[g] != null && _drops[g].Object != null)
                    {
                        _drops[g].Object.SetActive(false);
                    }
                }
                catch (Exception)
                {
                    // It is being given up: nothing more to say.
                }
            }
            TableSpec spec = Model.Spec;
            for (int g = 0; spec != null && g < spec.FilterCount; g++)
            {
                spec.Filters[g].Chosen = -1;
                spec.Filters[g].ChosenName = null;
            }
            if (spec != null)
            {
                Model.Apply();
            }
        }

        private void HeaderClicked(int column)
        {
            if (Model.Spec == null)
            {
                return;
            }
            long started = Stopwatch.GetTimestamp();
            using var hotSpot = HotSpots.Time(SlotOf(Model.Spec));
            if (!Model.SortBy(column))
            {
                return;
            }
            Shown();
            if (Changed != null)
            {
                Changed(false);
            }
            if (Meta.Dev.Verbose)
            {
                TableSpec spec = Model.Spec;
                AppShell.Logger?.Info("Table (" + spec.Id + "): sorted by column " + (column + 1) + " (" + spec.Columns[column].Sort + "), " + (spec.OrderAscending ? "ascending" : "descending")
                    + ", " + Model.Count + " row(s) shown; " + Ms(started) + " ms.");
            }
        }

        private void Picked(int filter, int option)
        {
            TableSpec spec = Model.Spec;
            if (spec == null || filter >= spec.FilterCount || option < 0 || option >= _optionKinds[filter].Count)
            {
                return;
            }
            long started = Stopwatch.GetTimestamp();
            using var hotSpot = HotSpots.Time(SlotOf(spec));
            int kind = _optionKinds[filter][option];
            if (kind == spec.Filters[filter].Chosen)
            {
                return;
            }
            Model.Choose(filter, kind);
            Shown();
            if (Changed != null)
            {
                Changed(true);
            }
            if (Meta.Dev.Verbose)
            {
                AppShell.Logger?.Info("Table (" + spec.Id + "): filter " + (filter + 1) + " set to " + (kind < 0 ? "all" : "kind " + kind) + ", " + (Model.Count - Model.Pinned) + " of "
                    + (Model.Rows - Model.Pinned) + " row(s) shown; " + Ms(started) + " ms.");
            }
        }

        // "Table: Finance", named on an app's first click: a slot per app, not per table (HotSpots.Most allows a few hundred).
        private static int SlotOf(TableSpec spec)
        {
            if (spec.Slot < 0)
            {
                spec.Slot = HotSpots.Slot("Table: " + spec.App);
            }
            return spec.Slot;
        }

        private static string Ms(long started)
        {
            return ((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency).ToString("0.00", CultureInfo.InvariantCulture);
        }

        // With diagnostics on, one line the first time a table is written in a session: what it declares and whether the
        // game's parts it needs were found.
        private void Report()
        {
            TableSpec spec = Model.Spec;
            if (spec.Reported || !Meta.Dev.Verbose)
            {
                return;
            }
            spec.Reported = true;
            bool headers = _shell.HeadersFit(spec), filters = spec.FilterCount == 0 || CanFilter, cells = _rowCells >= spec.Count;
            string filter = "no filter";
            if (spec.FilterCount > 0)
            {
                filter = "filter with " + spec.Filters[0].Kinds + " kinds";
                for (int g = 1; g < spec.FilterCount; g++)
                {
                    filter += " and one with " + spec.Filters[g].Kinds;
                }
            }
            string failed = (headers ? string.Empty : " the header cells do not fit;") + (filters ? string.Empty : " the game's drop-down is missing;")
                            + (cells ? string.Empty : " the row has too few cells;");
            AppShell.Logger?.Info("Table check (" + spec.Id + "): " + spec.Count + " columns, " + spec.Sortable + " sortable, " + filter + ", " + (spec.HasTotals ? "totals" : "no totals")
                + ": " + (headers && filters && cells ? "PASS" : "FAIL:" + failed.TrimEnd(';')) + ".");
        }
    }
}
