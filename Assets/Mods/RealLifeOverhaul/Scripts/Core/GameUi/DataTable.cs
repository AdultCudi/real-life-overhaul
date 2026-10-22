using System;
using System.Collections.Generic;
using BaTable;
using EnhancedUI.EnhancedScroller;
using Extensions;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// An app's table: the game's BaTable over its scroller, which makes only the rows in view, in the model's order
    /// (TableView, TableModel), with a totals line and a sentence where a filter leaves nothing. A source's rows are written
    /// only as they come into view. Costs nothing a frame of its own.
    /// </summary>
    public sealed class DataTable : BaTable<DataCellView, TableRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DataTable", typeof(DataTable));   // the game's calls of it

        /// <summary>How the totals line writes money.</summary>
        public Func<double, string> Money = amount => ((float)amount).ToShortCurrencyFormat();
        /// <summary>A filter took the selected row out and left none to select: the app closes what it showed of it.</summary>
        public Action SelectionLost;

        /// <summary>A kind was picked and the selection needed nothing done: an app whose box speaks of the rows shown writes itself again.</summary>
        public Action Filtered;

        private readonly List<TableRow> _shown = new List<TableRow>(48);
        private readonly TableRow _total = new TableRow(), _empty = new TableRow();
        private readonly List<TableRow> _standing = new List<TableRow>(64);   // the rows a source's rows stand in, kept and reused
        private readonly List<TableRow> _rowsOf = new List<TableRow>(64);     // those of the source shown now, in its order
        private List<TableRow> _source;
        private ITableSource _from;   // the source the rows are written from, or null for rows the app wrote
        private TableView _view;
        private bool _started, _jump;
        private int _layout;

        public TableSpec Spec => _view != null ? _view.Model.Spec : null;
        /// <summary>Changes when another table is shown: a row lays its cells out again when it differs.</summary>
        public int Layout => _layout;

        /// <summary>The game's colour of the selected row, read from its row by the frame.</summary>
        public Color RowSelected { get; private set; }

        /// <summary>The app's table on its frame, made while the frame sleeps (Build); null, with one log line, if the game's row is not as expected.</summary>
        public static DataTable On(AppShell shell, string app)
        {
            if (shell == null || !AppShell.PrepareRow(shell.RowPrefab, TableSpec.MaxColumns, app))
            {
                return null;
            }
            var table = shell.List.gameObject.AddComponent<DataTable>();
            table.scroller = shell.Scroller;
            table.prefab = shell.RowPrefab.AddComponent<DataCellView>();
            table.allowRowSelection = true;
            table.RowSelected = shell.RowSelected;
            table._view = new TableView(shell) { RowCells = TableSpec.MaxColumns };
            table._view.Changed = filter => UiClone.Safe(() => table.Reshow(filter));
            table.data = table._shown;
            return table;
        }

        public override void Start()
        {
            base.Start();
            _started = true;
            scroller.ReloadData();
        }

        public override float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return 100f;   // every table of the game's phone uses it
        }

        protected override string GetDataId(TableRow model)
        {
            if (FrameGuard.Off)
            {
                return string.Empty;
            }
            try
            {
                return IdOf(model);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, string.Empty);
            }
        }

        /// <summary>Shows the rows with the row of that id selected; the same table written again keeps the player's scroll.</summary>
        public void Show(TableSpec spec, List<TableRow> rows, string selectedId)
        {
            bool same = ReferenceEquals(spec, Spec) && ReferenceEquals(rows, _source) && _from == null;
            if (!same)
            {
                _layout++;
            }
            _source = rows;
            _from = null;
            _view.Begin(spec, rows);   // a line of words and a pinned row first, unless the table neither sorts nor filters
            Shown(selectedId, same);
        }

        /// <summary>Shows a table whose rows are written as they come into view (<see cref="ITableSource"/>); the same source again keeps the scroll.</summary>
        public void Show(TableSpec spec, ITableSource source, string selectedId)
        {
            bool same = ReferenceEquals(spec, Spec) && ReferenceEquals(source, _from);
            if (!same)
            {
                _layout++;
            }
            int count = source.Count;
            while (_standing.Count < count)
            {
                _standing.Add(new TableRow());   // once for the longest list shown: some 70 bytes a row
            }
            _rowsOf.Clear();
            for (int r = 0; r < count; r++)
            {
                _rowsOf.Add(_standing[r].Stand(source, r));
            }
            _source = _rowsOf;
            _from = source;
            _view.Begin(spec, source);
            Shown(selectedId, same);
        }

        private void Shown(string selectedId, bool same)
        {
            _view.Apply();
            Fill();
            SelectRow(null, selectedId);   // remembers who is selected; the row colours itself when the scroller makes it
            int jump = _jump ? Place(selectedId) : -1;
            _jump = false;
            Reload(jump, same);
        }

        // A row from a source is written first (only a search for a selected row asks this of rows not shown).
        private static string IdOf(TableRow row)
        {
            row.Ready();
            return row.Id;
        }

        public void ClearSelection()
        {
            SelectRow(null, null);
        }

        // The rows in the model's order, then the sentence for a filter that left nothing, or the totals.
        private void Fill()
        {
            TableModel model = _view.Model;
            TableSpec spec = model.Spec;
            _shown.Clear();
            for (int i = 0; i < model.Count; i++)
            {
                _shown.Add(_source[model.RowAt(i)]);
            }
            int rows = model.Count - model.Pinned;
            if (rows == 0)
            {
                if (spec.EmptyKey != null && (model.Pinned == 0 || model.Rows > model.Pinned))
                {
                    _empty.Reset("table:empty");
                    _empty.Info = true;
                    _empty.Text[0] = spec.EmptyKey.GetLocalization();
                    _shown.Add(_empty);
                }
                return;
            }
            if (!spec.HasTotals)
            {
                return;
            }
            _total.Reset("table:total");
            int label = 0;
            while (label < spec.Count - 1 && spec.Columns[label].Sums)
            {
                label++;
            }
            _total.Text[label] = "<b>" + "reallife_table_total".GetLocalization() + "</b>";
            for (int c = 0; c < spec.Count; c++)
            {
                if (spec.Columns[c].Sums)
                {
                    _total.Text[c] = "<b>" + Money(model.Total(c)) + "</b>";
                }
            }
            _shown.Add(_total);
        }

        // Where the row of an id is shown, or -1.
        private int Place(string id)
        {
            for (int i = 0; id != null && i < _shown.Count; i++)
            {
                if (IdOf(_shown[i]) == id)
                {
                    return i;
                }
            }
            return -1;
        }

        private void Reload(int jumpTo, bool keepPlace)
        {
            if (!_started || scroller == null)
            {
                return;
            }
            float at = keepPlace ? scroller.ScrollPosition : 0f;
            scroller.ReloadData();
            if (jumpTo >= 0)
            {
                scroller.JumpToDataIndex(jumpTo, 0.5f, 0.5f);   // the selected row, in the middle of what shows
            }
            else if (keepPlace)
            {
                scroller.SetScrollPositionImmediately(at);       // as the game does after its own filters (RefreshFilters)
            }
        }

        // A header clicked or a kind picked. The selected row stays selected if it still shows and is scrolled to; if the
        // filter took it out the first row is selected as a click would; with nothing left the app hears the selection is gone.
        private void Reshow(bool filter)
        {
            if (_source == null)
            {
                return;
            }
            string was = GetSelectedRowDataId();
            int selected = -1;
            for (int r = 0; was != null && r < _source.Count; r++)
            {
                if (IdOf(_source[r]) == was)
                {
                    selected = r;
                    break;
                }
            }
            Fill();
            int keep = _view.Model.Keep(selected);
            if (keep == selected)
            {
                Reload(Place(was), false);
                if (filter && Filtered != null)
                {
                    Filtered();
                }
                return;
            }
            SelectRow(null, null);
            Reload(-1, false);
            if (keep >= 0)
            {
                _source[keep].Ready();
            }
            if (keep >= 0 && _source[keep].Pick)
            {
                _jump = true;
                onRowSelected.Invoke(_source[keep]);
                _jump = false;
            }
            else if (SelectionLost != null)
            {
                SelectionLost();
            }
        }
    }

    /// <summary>
    /// A row: the game's MyEmployees row made plain text cells (AppShell.PrepareRow), written from a TableRow; one row
    /// serves every tab, coloured in the game's own colours. Costs nothing a frame of its own.
    /// </summary>
    public sealed class DataCellView : BaTableCellView<TableRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DataCellView", typeof(DataCellView));   // the game's calls of it

        private readonly TMP_Text[] _cells = new TMP_Text[TableSpec.MaxColumns];
        private readonly LayoutElement[] _widths = new LayoutElement[TableSpec.MaxColumns];
        private int _count;
        private DataTable _table;
        private TableRow _row;
        private bool _bound;
        private int _laidOut = -1;

        protected override void Awake()
        {
            Bind();
            base.Awake();
        }

        // By name each time a row is made: a copy needs carry no references.
        private void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            Transform background = transform.Find("Background");
            backgroundImage = background != null ? background.GetComponent<Image>() : null;
            for (_count = 0; _count < TableSpec.MaxColumns; _count++)
            {
                Transform cell = transform.Find("Cell" + _count);
                if (cell == null)
                {
                    break;
                }
                _cells[_count] = cell.GetComponent<TMP_Text>();
                _widths[_count] = cell.GetComponent<LayoutElement>();
            }
        }

        public override void SetData(TableRow row)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);   // a new row is a copy of a template that sleeps
                }
                Bind();
                if (_table == null)
                {
                    _table = GetComponentInParent<DataTable>();
                    if (_table != null)
                    {
                        selectedBackgroundColor = _table.RowSelected;
                    }
                }
                row.Ready();   // a source's row is written when it first comes into view
                _row = row;
                TableSpec spec = _table != null ? _table.Spec : null;
                int shown = row.Info || spec == null ? 1 : Mathf.Min(spec.Count, _count);
                int stamp = (_table != null ? _table.Layout : 0) * 2 + (row.Info ? 1 : 0);
                if (stamp != _laidOut)
                {
                    _laidOut = stamp;
                    LayOut(spec, shown, row.Info);
                }
                for (int i = 0; i < shown; i++)
                {
                    if (_cells[i] != null)
                    {
                        _cells[i].text = row.TextAt(i) ?? string.Empty;
                    }
                }
                Look(false);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // The cells this table uses, each as wide as its header; a line of words has the whole row.
        private void LayOut(TableSpec spec, int shown, bool info)
        {
            for (int i = 0; i < _count; i++)
            {
                bool used = i < shown;
                GameObject cell = _cells[i] != null ? _cells[i].gameObject : null;
                if (cell == null)
                {
                    continue;
                }
                if (cell.activeSelf != used)
                {
                    cell.SetActive(used);
                }
                if (!used || _widths[i] == null)
                {
                    continue;
                }
                TableColumn column = info || spec == null ? null : spec.Columns[i];
                bool flexible = column == null || column.Flexible;
                float width = column != null ? column.Width : 400f;
                _widths[i].minWidth = flexible ? 120f : width;
                _widths[i].preferredWidth = width;
                _widths[i].flexibleWidth = flexible ? 1f : 0f;
                _cells[i].alignment = column != null && column.Right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            }
        }

        public override void ResetVisuals()
        {
            try
            {
                Look(false);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public override void VisualizeSelected(bool selected)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (_row == null || !_row.Pick || backgroundImage == null)
                {
                    Look(false);   // only a row that means something is selected
                    return;
                }
                base.VisualizeSelected(selected);
                Look(selected);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void Look(bool selected)
        {
            if (_row == null)
            {
                return;
            }
            if (backgroundImage != null && _row.Info)
            {
                Color clear = defaultBackgroundColor;
                clear.a = 0f;
                backgroundImage.color = clear;
            }
            else if (backgroundImage != null && !selected)
            {
                backgroundImage.color = defaultBackgroundColor;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null)
            {
                return;
            }
            Color plain = selected ? (Color)references.colors.midnight : (Color)references.colors.white;
            for (int i = 0; i < _count; i++)
            {
                if (_cells[i] == null)
                {
                    continue;
                }
                switch ((CellTone)_row.ToneAt(i))
                {
                    case TableRow.Red: _cells[i].color = references.colors.red; break;
                    case TableRow.Yellow: _cells[i].color = selected ? references.colors.orange : references.colors.yellow; break;
                    case TableRow.Green: _cells[i].color = references.colors.green; break;
                    default: _cells[i].color = plain; break;
                }
            }
        }
    }
}
