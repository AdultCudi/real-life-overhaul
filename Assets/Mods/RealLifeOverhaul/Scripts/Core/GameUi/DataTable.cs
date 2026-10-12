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
    /// The tone of a cell: plain, the game's yellow, its red, its green (as EconoView colours a gain and a loss). A type
    /// of its own so that a cell's figure is never taken for a tone: TableRow.Set(column, words, figure) orders and sums
    /// the column by the figure whatever its type.
    /// </summary>
    public enum CellTone : byte
    {
        Plain = 0,
        Yellow = 1,
        Red = 2,
        Green = 3
    }

    /// <summary>
    /// One row of an app's table: the words of each cell, what each cell is ordered by, the kind the row is of, or
    /// one line that only says something. An app keeps its rows and uses them again (Reset).
    /// </summary>
    public sealed class TableRow
    {
        /// <summary>The tones of a cell (GameUi.CellTone), by their old names.</summary>
        public const GameUi.CellTone Plain = GameUi.CellTone.Plain, Yellow = GameUi.CellTone.Yellow, Red = GameUi.CellTone.Red, Green = GameUi.CellTone.Green;

        public string Id;
        public readonly string[] Text = new string[TableSpec.MaxColumns];
        public readonly double[] Key = new double[TableSpec.MaxColumns];    // what a column is ordered and summed by; NaN for nothing
        public readonly byte[] Tones = new byte[TableSpec.MaxColumns];
        public int Kind, Kind2;     // for the table's first and second filter; -1 for of no kind
        public bool Info;           // a line of words: no background, not to be selected, always shown first
        public bool Pick;           // a click on it means something
        public bool Pinned;         // shown first whatever the order and the filter, and not counted or summed
        public bool NoTotal;        // ordered and filtered like any other row, but in no total
        public int Index;           // what it stands for in its tab's list

        // The four cells the apps' first tables had, by their old names.
        public string A { get { return Text[0]; } set { Text[0] = value; } }
        public string B { get { return Text[1]; } set { Text[1] = value; } }
        public string C { get { return Text[2]; } set { Text[2] = value; } }
        public string D { get { return Text[3]; } set { Text[3] = value; } }
        /// <summary>The tone of the fourth cell (the last one of a table of four).</summary>
        public GameUi.CellTone Tone { get { return (GameUi.CellTone)Tones[3]; } set { Tones[3] = (byte)value; } }

        public TableRow Reset(string id)
        {
            Id = id;
            for (int i = 0; i < TableSpec.MaxColumns; i++)
            {
                Text[i] = null;
                Key[i] = double.NaN;
                Tones[i] = (byte)Plain;
            }
            Kind = Kind2 = -1;
            Info = Pick = Pinned = NoTotal = false;
            Index = 0;
            return this;
        }

        /// <summary>A cell of words, ordered by them where its column sorts.</summary>
        public void Set(int column, string words, GameUi.CellTone tone = Plain)
        {
            Text[column] = words;
            Key[column] = double.NaN;
            Tones[column] = (byte)tone;
        }

        /// <summary>A cell with the figure it is ordered (and summed) by: an amount, a rate, a count, a day.</summary>
        public void Set(int column, string words, double key, GameUi.CellTone tone = Plain)
        {
            Text[column] = words;
            Key[column] = key;
            Tones[column] = (byte)tone;
        }
    }

    /// <summary>
    /// The table of an app, whatever it lists: the game's own (BaTable) over the game's scroller, which makes only
    /// the rows in view and reuses them. An app hands over its rows and the table's specification; the order, the
    /// filter and the totals are the model's (TableView, TableModel), and this class shows the rows in that order,
    /// with one line at the foot for the totals (as EconoView's income statement ends in its total) and one sentence
    /// where a filter leaves nothing (as EconoView says "no loans" in the list's place).
    ///
    /// The header names are not fields of TableRow, so the game's own sort by reflection takes no part.
    /// </summary>
    public sealed class DataTable : BaTable<DataCellView, TableRow>
    {
        /// <summary>How the totals line writes an amount: the app's own way of writing money.</summary>
        public Func<double, string> Money = amount => ((float)amount).ToShortCurrencyFormat();
        /// <summary>
        /// The filter took the selected row out and left nothing to select in its place: the app closes what it was
        /// showing of that row. (Where a row is left, the first is selected as a click would: onRowSelected.)
        /// </summary>
        public Action SelectionLost;

        /// <summary>
        /// The player picked a kind in a filter and the selection needed nothing done: an app whose box says
        /// something about the rows shown (a category's share) writes itself again.
        /// </summary>
        public Action Filtered;

        private readonly List<TableRow> _shown = new List<TableRow>(48);
        private readonly TableRow _total = new TableRow(), _empty = new TableRow();
        private List<TableRow> _source;
        private TableView _view;
        private bool _started, _jump;
        private int _layout;

        public TableSpec Spec => _view != null ? _view.Model.Spec : null;
        /// <summary>Changes when another table is shown: a row lays its cells out again when it differs from what it last saw.</summary>
        public int Layout => _layout;

        /// <summary>The colour of the selected row, the game's own (read from its row by the frame).</summary>
        public Color RowSelected { get; private set; }

        /// <summary>
        /// The table of an app, on its frame: the game's row made into plain cells, the game's scroller, and the
        /// frame's header cells and filter. Null, with one line in Player.log, if the game's row is not as expected.
        /// Called while the frame still sleeps (in the app's Build).
        /// </summary>
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
            return 100f;   // what every table of the game's phone uses
        }

        protected override string GetDataId(TableRow model)
        {
            return model.Id;
        }

        /// <summary>
        /// Shows these rows as the table the specification describes, with the row of that id selected. The first
        /// time the app opens this runs before the scroller has woken up: the rows are kept and Start shows them.
        /// The same table written again (a figure changed, a row was clicked) stays where the player scrolled to.
        /// </summary>
        public void Show(TableSpec spec, List<TableRow> rows, string selectedId)
        {
            bool same = ReferenceEquals(spec, Spec) && ReferenceEquals(rows, _source);
            if (!same)
            {
                _layout++;
            }
            _source = rows;
            TableModel model = _view.Model;
            _view.Begin(spec, rows.Count);
            // A table that neither sorts nor filters is a page laid out by hand (headings between its rows): every
            // row stays exactly where it was written. In any other, a line of words and a pinned row come first.
            bool ordered = spec.Sortable > 0 || spec.FilterCount > 0;
            for (int r = 0; r < rows.Count; r++)
            {
                TableRow row = rows[r];
                if (ordered && (row.Info || row.Pinned))
                {
                    model.Pin(r);
                    continue;
                }
                for (int c = 0; c < spec.Count; c++)
                {
                    if (spec.Columns[c].Sort != TableSort.None || spec.Columns[c].Sums)
                    {
                        model.Set(r, c, row.Key[c], row.Text[c]);
                    }
                }
                model.Kind(r, 0, row.Kind);
                model.Kind(r, 1, row.Kind2);
                if (row.NoTotal)
                {
                    model.Leave(r);
                }
            }
            _view.Apply();
            Fill();
            SelectRow(null, selectedId);   // remembers who is selected; the row colours itself when the scroller makes it
            int jump = _jump ? Place(selectedId) : -1;
            _jump = false;
            Reload(jump, same);
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
                if (_shown[i].Id == id)
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

        // The player clicked a header or picked a kind. The selected row stays selected if it still shows, and the
        // table scrolls to it; if the filter took it out, the first row is selected instead, exactly as a click on
        // it would be (the app shows that row in its box); with nothing left, the app is told the selection is gone.
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
                if (_source[r].Id == was)
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
    /// A row: the game's MyEmployees row made into plain cells of text (AppShell.PrepareRow), written from a
    /// TableRow. Which cells show and how wide they are is the table's specification, so one row serves every tab
    /// of an app. The game colours the selected row and leaves its own red and yellow alone
    /// (BaTableCellView.VisualizeSelected), so a cell's emphasis is written in exactly the game's colours.
    /// </summary>
    public sealed class DataCellView : BaTableCellView<TableRow>
    {
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

        // Found by name each time a row is made: a copy does not have to carry references for this to work.
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
                    _cells[i].text = row.Text[i] ?? string.Empty;
                }
            }
            Look(false);
        }

        // The cells this table uses, each as wide as its column's header; a line of words has the whole row.
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
            Look(false);
        }

        public override void VisualizeSelected(bool selected)
        {
            if (_row == null || !_row.Pick || backgroundImage == null)
            {
                Look(false);   // only a row that means something is selected
                return;
            }
            base.VisualizeSelected(selected);
            Look(selected);
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
                switch ((CellTone)_row.Tones[i])
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
