using System;
using System.Collections.Generic;
using System.Diagnostics;
using Localizor;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// What makes a table of an app's sort and filter: the thin layer between the table's model (TableModel: pure
    /// arithmetic) and the game's own controls on the copy of MyEmployees (AppShell): its header cells, which are
    /// buttons with two arrows, and its drop-down over the table. One per app.
    ///
    ///   view.Begin(spec, rows)    the table that is being written: its columns go on the headers
    ///   view.Model.Set / Kind     what each row gives for each column, and its kind
    ///   view.Apply()              the order, the filter and the totals are worked out; arrows and drop-downs follow
    ///   view.Changed              called when the player clicked a header or picked a kind: show the rows again
    ///
    /// Nothing here runs unless the table is written or the player clicks. If the game's header buttons or its
    /// drop-down are not there (a game update), the table is what it was before: plain headers, no filter, one
    /// line in Player.log (UiClone.Fallback), and the app works as ever.
    /// </summary>
    public sealed class TableView
    {
        public readonly TableModel Model = new TableModel();
        /// <summary>The player changed the order (false) or the filter (true): the owner shows its rows again in the model's order.</summary>
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

        /// <summary>How many cells of text the app's row has: said once, for the self-check.</summary>
        public int RowCells
        {
            set { _rowCells = value; }
        }

        /// <summary>Whether a filter can be shown at all (the game's drop-down was there to copy and has not failed).</summary>
        public bool CanFilter => !_filtersOff && _shell.FilterTemplate != null;

        /// <summary>A table is about to be written: its columns go on the header cells, and the model is emptied for its rows.</summary>
        public void Begin(TableSpec spec, int rows)
        {
            _shell.SetColumns(spec);
            Model.Begin(spec, rows);
        }

        /// <summary>The rows are in: the order, the filter and the totals are worked out and the game's controls show them.</summary>
        public void Apply()
        {
            Model.Apply();
            Shown();
            Report();
        }

        // The arrow on the column the table is ordered by, and a drop-down for each filter the rows have two kinds for.
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

        // "All" first, then each kind the table holds, as EconoView lists the types of its transactions.
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
                    // (it is being given up: nothing more to say about it)
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

        // ---------------------------------------------------------------- the player's clicks

        private void HeaderClicked(int column)
        {
            if (Model.Spec == null)
            {
                return;
            }
            long started = Stopwatch.GetTimestamp();
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

        private static string Ms(long started)
        {
            return ((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency).ToString("0.00");
        }

        // ---------------------------------------------------------------- the self-check

        // One line the first time a table is written in a session, with "Developer diagnostics" on: what it declares
        // and whether the game's parts it needs were all found.
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
            string failed = (headers ? string.Empty : " the header cells do not fit;") + (filters ? string.Empty : " the game's drop-down is missing;") + (cells ? string.Empty : " the row has too few cells;");
            AppShell.Logger?.Info("Table check (" + spec.Id + "): " + spec.Count + " columns, " + spec.Sortable + " sortable, " + filter + ", " + (spec.HasTotals ? "totals" : "no totals")
                + ": " + (headers && filters && cells ? "PASS" : "FAIL:" + failed.TrimEnd(';')) + ".");
        }
    }
}
