using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One movement of money in a period, for a table: what it was, when, how much and what for (CashCategory).</summary>
    public struct CashEntry
    {
        public string Type;
        public Dictionary<string, string> Data;
        public float Amount;
        public int Day;
        public int Category;
    }

    /// <summary>The words a table of movements of money is written in: the app's own (FinanceText, the game's words), or a check's.</summary>
    public interface ICashWords
    {
        /// <summary>"Day 12".</summary>
        string Day(int day);
        /// <summary>One movement in the game's own words (its type's text, filled from its data).</summary>
        string Item(string type, Dictionary<string, string> data);
        /// <summary>A category's name (CashCategory.Keys).</summary>
        string Category(int category);
        /// <summary>An amount, as the app writes money.</summary>
        string Money(float amount);
    }

    /// <summary>
    /// The movements of money of a period as a table that is written only as it is shown (GameUi.ITableSource): the Finance
    /// app's Cash flow tab and a bank's statement. A month of a late game is some 30,000 movements; each row's words (its day,
    /// the game's text for it filled from its data, its category, its amount) took six strings, and the scroller shows some
    /// fifteen. Now a row's words are made when it comes into view; the table is ordered, filtered and summed by the
    /// figures and the category, and a column's words are made only when the table is ordered by them.
    ///
    /// Newest first, as the tab always listed them: row 0 is the last movement read. Columns: 0 day, 1 what, 2 category,
    /// 3 in, 4 out (FinanceTables.Cash, BankTables.Statement). A row is written exactly as the tab wrote every row before:
    /// id "cash:" and the movement's place in the list, kind its category, the day by its number, money in green under
    /// "in" and out in red under "out".
    ///
    /// Game-free: the offline checks run it as it is (EconomyCheck, AppsChecks.cs).
    /// </summary>
    public sealed class CashTable : ITableSource
    {
        public const int ColumnDay = 0, ColumnWhat = 1, ColumnCategory = 2, ColumnIn = 3, ColumnOut = 4;

        private readonly List<CashEntry> _lines;
        private readonly ICashWords _words;

        /// <summary>A table over a list of movements, oldest first (CashFlow.Entries); the list is read, never changed.</summary>
        public CashTable(List<CashEntry> lines, ICashWords words)
        {
            _lines = lines;
            _words = words;
        }

        public int Count => _lines.Count;

        // Row 0 is the newest.
        private int Place(int row) => _lines.Count - 1 - row;

        public double Key(int row, int column)
        {
            CashEntry e = _lines[Place(row)];
            switch (column)
            {
                case ColumnDay: return e.Day;
                case ColumnIn: return e.Amount > 0f ? e.Amount : double.NaN;
                case ColumnOut: return e.Amount > 0f ? double.NaN : -e.Amount;
                default: return double.NaN;
            }
        }

        public int Kind(int row, int filter)
        {
            return filter == 0 ? _lines[Place(row)].Category : -1;
        }

        public string Text(int row, int column)
        {
            CashEntry e = _lines[Place(row)];
            switch (column)
            {
                case ColumnDay: return _words.Day(e.Day);
                case ColumnWhat: return _words.Item(e.Type, e.Data);
                case ColumnCategory: return _words.Category(e.Category);
                case ColumnIn: return e.Amount > 0f ? _words.Money(e.Amount) : null;
                case ColumnOut: return e.Amount > 0f ? null : _words.Money(-e.Amount);
                default: return null;
            }
        }

        public void Write(int row, TableRow into)
        {
            int i = Place(row);
            CashEntry e = _lines[i];
            into.Id = "cash:" + i;
            into.Kind = e.Category;
            into.Set(ColumnDay, _words.Day(e.Day), e.Day);
            into.Set(ColumnWhat, _words.Item(e.Type, e.Data));
            into.Set(ColumnCategory, _words.Category(e.Category));
            if (e.Amount > 0f)
            {
                into.Set(ColumnIn, _words.Money(e.Amount), e.Amount, TableRow.Green);
            }
            else
            {
                into.Set(ColumnOut, _words.Money(-e.Amount), -e.Amount, TableRow.Red);
            }
        }
    }
}
