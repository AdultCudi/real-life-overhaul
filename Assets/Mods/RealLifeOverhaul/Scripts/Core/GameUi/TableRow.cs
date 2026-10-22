namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A cell's tone: plain, or the game's yellow, red or green. A type of its own so that a figure is never taken for a
    /// tone: TableRow.Set(column, words, figure) orders and sums by the figure whatever its type.
    /// </summary>
    public enum CellTone : byte
    {
        Plain = 0,
        Yellow = 1,
        Red = 2,
        Green = 3
    }

    /// <summary>
    /// A table whose rows are written only when shown (a month's 30,000 movements of money, where a row's words cost more
    /// than its figures). The table asks for each row's figures and kinds at once, a column's words only when ordered by
    /// them, and the whole row (<see cref="Write"/>) only when it comes into view; each must match what writing the row
    /// gives. Its rows are plain: no line of words, none pinned, all in the totals.
    /// </summary>
    public interface ITableSource
    {
        /// <summary>How many rows, in the order they are written (row 0 first).</summary>
        int Count { get; }
        /// <summary>What a row gives a column to be ordered and summed by; NaN for nothing.</summary>
        double Key(int row, int column);
        /// <summary>The kind a row is of for one of the table's filters; -1 for of none.</summary>
        int Kind(int row, int filter);
        /// <summary>A row's words in a column, or null: asked only when the table is ordered by that column.</summary>
        string Text(int row, int column);
        /// <summary>The whole row, written into a table row: asked when it comes into view.</summary>
        void Write(int row, TableRow into);
    }

    /// <summary>
    /// One row of an app's table: each cell's words, figure and tone, the row's kinds, or one line that only says something.
    /// Apps reuse their rows (Reset). A row that stands for a source's row (<see cref="ITableSource"/>) makes its arrays
    /// only when written (<see cref="Ready"/>), so 30,000 of them cost some 70 bytes a row, once.
    /// </summary>
    public sealed class TableRow
    {
        /// <summary>The tones of a cell (GameUi.CellTone), by their old names.</summary>
        public const GameUi.CellTone Plain = GameUi.CellTone.Plain, Yellow = GameUi.CellTone.Yellow, Red = GameUi.CellTone.Red, Green = GameUi.CellTone.Green;

        public string Id;
        private string[] _text;
        private double[] _key;
        private byte[] _tones;
        public int Kind, Kind2;     // for the table's first and second filter; -1 for none
        public bool Info;           // a line of words: no background, not to be selected, always shown first
        public bool Pick;           // a click on it means something
        public bool Pinned;         // shown first whatever the order and the filter, and not counted or summed
        public bool NoTotal;        // ordered and filtered like any other row, but in no total
        public int Index;           // what it stands for in its tab's list

        private ITableSource _source;   // the table it is written from when shown; null for a row an app wrote itself
        private int _sourceRow;
        private bool _written;

        /// <summary>The words of each cell, made when first written to.</summary>
        public string[] Text => _text ?? (_text = new string[TableSpec.MaxColumns]);

        /// <summary>What each column is ordered and summed by; NaN for nothing.</summary>
        public double[] Key
        {
            get
            {
                if (_key == null)
                {
                    _key = new double[TableSpec.MaxColumns];
                    for (int i = 0; i < _key.Length; i++)
                    {
                        _key[i] = double.NaN;
                    }
                }
                return _key;
            }
        }

        /// <summary>The tone of each cell (GameUi.CellTone).</summary>
        public byte[] Tones => _tones ?? (_tones = new byte[TableSpec.MaxColumns]);

        /// <summary>A cell's words, figure and tone, read without making the row's arrays.</summary>
        public string TextAt(int column) => _text != null ? _text[column] : null;
        public double KeyAt(int column) => _key != null ? _key[column] : double.NaN;
        public byte ToneAt(int column) => _tones != null ? _tones[column] : (byte)Plain;

        // The four cells of the apps' first tables, by their old names.
        public string A { get { return Text[0]; } set { Text[0] = value; } }
        public string B { get { return Text[1]; } set { Text[1] = value; } }
        public string C { get { return Text[2]; } set { Text[2] = value; } }
        public string D { get { return Text[3]; } set { Text[3] = value; } }
        /// <summary>The tone of the fourth cell, the last of a table of four.</summary>
        public GameUi.CellTone Tone { get { return (GameUi.CellTone)ToneAt(3); } set { Tones[3] = (byte)value; } }

        public TableRow Reset(string id)
        {
            _source = null;
            _written = false;
            Clean();
            Id = id;
            return this;
        }

        private void Clean()
        {
            Id = null;
            for (int i = 0; i < TableSpec.MaxColumns; i++)
            {
                if (_text != null)
                {
                    _text[i] = null;
                }
                if (_key != null)
                {
                    _key[i] = double.NaN;
                }
                if (_tones != null)
                {
                    _tones[i] = (byte)Plain;
                }
            }
            Kind = Kind2 = -1;
            Info = Pick = Pinned = NoTotal = false;
            Index = 0;
        }

        /// <summary>The row stands for a source's row, written when first shown (<see cref="Ready"/>).</summary>
        public TableRow Stand(ITableSource source, int row)
        {
            _source = source;
            _sourceRow = row;
            _written = false;
            Clean();
            return this;
        }

        /// <summary>Writes the row from its source once, if it stands for one.</summary>
        public void Ready()
        {
            if (_source != null && !_written)
            {
                _written = true;
                Clean();
                _source.Write(_sourceRow, this);
            }
        }

        /// <summary>A cell of words, ordered by them where its column sorts.</summary>
        public void Set(int column, string words, GameUi.CellTone tone = Plain)
        {
            Text[column] = words;
            if (_key != null)
            {
                _key[column] = double.NaN;
            }
            if (_tones != null || tone != Plain)
            {
                Tones[column] = (byte)tone;
            }
        }

        /// <summary>A cell with the figure it is ordered and summed by.</summary>
        public void Set(int column, string words, double key, GameUi.CellTone tone = Plain)
        {
            Text[column] = words;
            Key[column] = key;
            if (_tones != null || tone != Plain)
            {
                Tones[column] = (byte)tone;
            }
        }
    }
}
