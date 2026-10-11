using System;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;

// Checks for the table model every app's table is ordered, filtered and summed by (Core/GameUi/TableModel.cs):
// each kind of order both ways, that equal rows keep their place, rows with nothing to compare, the filters and
// their counts, the totals, what stays selected, that a second sort allocates nothing, and what a sort costs.
internal static partial class Program
{
    private static readonly string[] TableKinds = { "loan", "card", "mortgage" };
    private static readonly string[] TableStates = { "fine", "late" };

    private static TableSpec TableSpecOf()
    {
        return new TableSpec("Check", "Accounts")
            .Column("name", 500f, TableSort.Text, flexible: true)
            .Column("owed", 200f, TableSort.Money, sums: true)
            .Column("rate", 200f, TableSort.Number)
            .Column("due", 200f, TableSort.Date)
            .Column("note", 200f)
            .Column("mixed", 200f, TableSort.Custom)
            .Filter("all kinds", TableKinds)
            .Filter("all states", TableStates)
            .Empty("empty");
    }

    // Six rows: name, owed, rate, due day, kind, state. Rows 1 and 4 owe the same; row 5 has no rate and no day.
    private static readonly string[] TableNames = { "delta", "Alpha", "charlie", "bravo", "echo", "" };
    private static readonly double[] TableOwed = { 500, -200, 1200.5, 0, -200, 75 };
    private static readonly double[] TableRate = { 4.5, 19.9, 3, 4.5, 7, double.NaN };
    private static readonly double[] TableDue = { 12, 3, 40, 3, 9, double.NaN };
    private static readonly int[] TableKind = { 0, 1, 2, 0, 1, 0 };
    private static readonly int[] TableState = { 0, 1, 0, 0, 1, 0 };
    private static readonly double[] TableMixedKey = { 7, double.NaN, 2, double.NaN, double.NaN, 11 };
    private static readonly string[] TableMixedText = { "7", "pear", "2", "Apple", null, "11" };

    private static TableModel TableOf(TableSpec spec)
    {
        var model = new TableModel();
        model.Begin(spec, TableNames.Length);
        for (int r = 0; r < TableNames.Length; r++)
        {
            model.Set(r, 0, double.NaN, TableNames[r]);
            model.Set(r, 1, TableOwed[r], null);
            model.Set(r, 2, TableRate[r], null);
            model.Set(r, 3, TableDue[r], null);
            model.Set(r, 4, 99 - r, "never read");
            model.Set(r, 5, TableMixedKey[r], TableMixedText[r]);
            model.Kind(r, 0, TableKind[r]);
            model.Kind(r, 1, TableState[r]);
        }
        model.Apply();
        return model;
    }

    private static string TableOrder(TableModel model)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < model.Count; i++)
        {
            text.Append(model.RowAt(i));
        }
        return text.ToString();
    }

    private static void Tables()
    {
        TableSpec spec = TableSpecOf();
        TableModel m = TableOf(spec);

        // ---- as written
        Check(m.Rows == 6 && m.Count == 6 && TableOrder(m) == "012345", "a table with no first order shows its rows as they were written");
        Check(spec.Sortable == 5 && spec.HasTotals && spec.Count == 6 && spec.FilterCount == 2, "the specification counts its sortable columns, its totals and its filters");
        Check(Math.Abs(m.Total(1) - 1375.5) < 1e-9, "the totals line adds a money column up, negative amounts included");
        Check(m.Total(0) == 0 && m.Total(2) == 0, "a column that is not declared as summed has no total");

        // ---- text
        Check(m.SortBy(0) && spec.OrderColumn == 0 && spec.OrderAscending, "a first click on a header orders by it, ascending, as the game's table does");
        Check(TableOrder(m) == "132045", "text ascending: capitals and small letters alike, the empty cell last");
        Check(m.SortBy(0) && !spec.OrderAscending && TableOrder(m) == "402315", "a second click turns the order round; the empty cell is still last");
        Check(m.SortBy(0) && spec.OrderAscending && TableOrder(m) == "132045", "a third click turns it back");

        // ---- money, with a tie and negative amounts
        Check(m.SortBy(1) && spec.OrderAscending && TableOrder(m) == "143502", "money ascending: negative amounts first, the two that owe the same in the order they were written");
        Check(m.SortBy(1) && TableOrder(m) == "205314", "money descending: the tie keeps its written order here too (stable both ways)");

        // ---- number, with a tie and a row that has none
        Check(m.SortBy(2) && TableOrder(m) == "203415", "number ascending: equal rates in written order, the row without a rate last");
        Check(m.SortBy(2) && TableOrder(m) == "140325", "number descending: the row without a rate is still last");

        // ---- date
        Check(m.SortBy(3) && TableOrder(m) == "134025", "date ascending: soonest first, same day in written order, no date last");
        Check(m.SortBy(3) && TableOrder(m) == "204135", "date descending");

        // ---- a column that does not sort
        string before = TableOrder(m);
        Check(!m.SortBy(4) && TableOrder(m) == before && spec.OrderColumn == 3, "a click on a column that does not sort changes nothing");
        Check(!m.SortBy(-1) && !m.SortBy(6) && !m.SortBy(99), "a column that is not there is refused");

        // ---- numbers against text
        Check(m.SortBy(5) && TableOrder(m) == "205314", "a custom column: numbers first by size, then words by the alphabet, then nothing");
        Check(m.SortBy(5) && TableOrder(m) == "502134", "custom descending: numbers still before words, each turned round, nothing last");

        // ---- filters and their counts
        Check(m.KindCount(0, 0) == 3 && m.KindCount(0, 1) == 2 && m.KindCount(0, 2) == 1, "the kinds are counted");
        Check(m.KindCount(1, 0) == 4 && m.KindCount(1, 1) == 2 && m.KindCount(1, 7) == 0 && m.KindCount(5, 0) == 0, "a second filter is counted on its own; what is not there counts nothing");
        Check(m.KindsHeld(0) == 3 && m.KindsHeld(1) == 2, "how many kinds the rows are of is known: a filter is offered from two");
        m.SortBy(1);
        m.Choose(0, 0);
        Check(m.Count == 3 && TableOrder(m) == "350", "choosing a kind shows only its rows, in the order chosen before");
        Check(Math.Abs(m.Total(1) - 575) < 1e-9, "the totals follow the filter");
        Check(m.KindCount(0, 1) == 2, "the counts do not: they say what choosing another kind would show");
        m.Choose(1, 1);
        Check(m.Count == 0 && m.Total(1) == 0, "two filters together can leave nothing, and then the total is nothing");
        m.Choose(1, -1);
        m.Choose(0, 1);
        Check(TableOrder(m) == "14", "another kind");
        m.Choose(1, 1);
        Check(TableOrder(m) == "14" && Math.Abs(m.Total(1) + 400) < 1e-9, "both filters, both passed; a total can be negative");
        m.Choose(0, -1);
        m.Choose(1, -1);
        Check(m.Count == 6, "back to all");
        m.Choose(0, 99);
        Check(m.Count == 6 && spec.Filters[0].Chosen == -1, "a kind that does not exist is 'all'");

        // ---- the choice is remembered on the specification, and a kind with no rows left goes back to all
        m.SortBy(0);
        m.SortBy(0);
        m.Choose(0, 2);
        TableModel again = TableOf(spec);
        Check(TableOrder(again) == "2" && spec.OrderColumn == 0 && !spec.OrderAscending, "a table written again keeps the column, the direction and the kind chosen");
        var fewer = new TableModel();
        fewer.Begin(spec, 2);
        fewer.Kind(0, 0, 0);
        fewer.Kind(1, 0, 1);
        fewer.Set(0, 0, double.NaN, "b");
        fewer.Set(1, 0, double.NaN, "a");
        fewer.Apply();
        Check(fewer.Count == 2 && spec.Filters[0].Chosen == -1 && TableOrder(fewer) == "01", "a chosen kind no row is of any more goes back to all (and the order kept is descending by name)");
        spec.Filters[0].Chosen = 1;
        var one = new TableModel();
        one.Begin(spec, 3);
        for (int r = 0; r < 3; r++)
        {
            one.Kind(r, 0, 1);
        }
        one.Apply();
        Check(one.Count == 3 && one.KindsHeld(0) == 1 && spec.Filters[0].Chosen == -1, "a table of one kind has no filter to offer, so nothing stays filtered");

        // ---- the first order
        TableSpec soonest = new TableSpec("Check", "Soonest").Column("name", 100f, TableSort.Text).Column("due", 100f, TableSort.Date).Order(1, true);
        var first = new TableModel();
        first.Begin(soonest, 4);
        double[] days = { 9, 2, double.NaN, 2 };
        for (int r = 0; r < 4; r++)
        {
            first.Set(r, 0, double.NaN, "row" + r);
            first.Set(r, 1, days[r], null);
        }
        first.Apply();
        Check(TableOrder(first) == "1302" && soonest.OrderColumn == 1 && !soonest.Touched, "a table opens in the order it declares (soonest first)");
        Check(first.SortBy(1) && !soonest.OrderAscending && TableOrder(first) == "0132", "a click on the column it opens by turns that order round");
        Check(first.SortBy(0) && soonest.OrderAscending && TableOrder(first) == "0123", "and another column starts ascending");

        // ---- pinned rows
        TableSpec pins = new TableSpec("Check", "Pins").Column("name", 100f, TableSort.Text).Column("sum", 100f, TableSort.Money, sums: true).Filter("all", TableKinds);
        var pinned = new TableModel();
        pinned.Begin(pins, 5);
        string[] pinNames = { "zed", "note one", "alpha", "note two", "mid" };
        for (int r = 0; r < 5; r++)
        {
            pinned.Set(r, 0, double.NaN, pinNames[r]);
            pinned.Set(r, 1, 10 * (r + 1), null);
            pinned.Kind(r, 0, r == 0 ? 0 : 1);
        }
        pinned.Pin(1);
        pinned.Pin(3);
        pinned.Apply();
        Check(TableOrder(pinned) == "13024" && pinned.Pinned == 2, "pinned rows come first, in the order they were written");
        pinned.SortBy(0);
        Check(TableOrder(pinned) == "13240", "they stay there when the rest is ordered");
        Check(Math.Abs(pinned.Total(1) - 90) < 1e-9 && pinned.KindCount(0, 1) == 2, "they are neither summed nor counted");
        pinned.Choose(0, 0);
        Check(TableOrder(pinned) == "130", "and no filter takes them out");

        // ---- a row that is in no total (an offer among deals)
        var deals = new TableModel();
        deals.Begin(pins, 3);
        for (int r = 0; r < 3; r++)
        {
            deals.Set(r, 0, double.NaN, "deal" + r);
            deals.Set(r, 1, 100 * (r + 1), null);
            deals.Kind(r, 0, 1);
        }
        deals.Leave(1);
        pins.Filters[0].Chosen = -1;
        deals.SortBy(1);
        deals.SortBy(1);
        Check(TableOrder(deals) == "210" && Math.Abs(deals.Total(1) - 400) < 1e-9 && deals.KindCount(0, 1) == 3, "a row left out of the totals is still ordered, filtered and counted");

        // ---- what stays selected
        TableSpec sel = TableSpecOf();
        TableModel s = TableOf(sel);
        Check(s.Keep(-1) == -1, "nothing selected, nothing selected after");
        s.SortBy(1);
        Check(s.Keep(2) == 2 && s.PlaceOf(2) == 5, "the selected row stays selected through a sort, and its new place is known (to scroll to)");
        s.Choose(0, 2);
        Check(s.Keep(2) == 2 && s.PlaceOf(2) == 0, "and through a filter that keeps it");
        s.Choose(0, 0);
        Check(s.PlaceOf(2) == -1 && s.Keep(2) == 3, "a filter that takes it out selects the first row shown instead");
        s.Choose(1, 1);
        Check(s.Count == 0 && s.Keep(2) == -1, "and nothing when nothing is left");
        TableModel sp = pinned;
        Check(sp.Keep(4) == 0, "the first row after the pinned ones is the one selected, never a pinned line");

        // ---- empty and one-row tables
        TableSpec bare = TableSpecOf();
        var none = new TableModel();
        none.Begin(bare, 0);
        none.Apply();
        Check(none.Count == 0 && none.Rows == 0 && none.Total(1) == 0 && none.KindsHeld(0) == 0, "an empty table is empty");
        Check(none.SortBy(0) && none.Count == 0 && none.Keep(0) == -1 && none.PlaceOf(0) == -1, "ordering an empty table is allowed and does nothing");
        none.Choose(0, 1);
        Check(none.Count == 0 && bare.Filters[0].Chosen == -1, "filtering it too");
        var single = new TableModel();
        single.Begin(bare, 1);
        single.Set(0, 0, double.NaN, "only");
        single.Set(0, 1, 42, null);
        single.Kind(0, 0, 2);
        single.Apply();
        Check(single.Count == 1 && single.RowAt(0) == 0 && single.Total(1) == 42, "a table of one row shows it and sums it");
        Check(single.SortBy(1) && single.SortBy(1) && single.RowAt(0) == 0 && single.Keep(0) == 0, "and ordering it either way leaves it there");
        single.Set(5, 0, 1, "outside");
        single.Set(0, 12, 1, "outside");
        single.Kind(9, 0, 1);
        single.Pin(9);
        single.Apply();
        Check(single.Count == 1, "what is written outside the table is ignored");

        // ---- a filter with the app's own names remembers the name, not the number
        TableSpec named = new TableSpec("Check", "Named").Column("name", 100f, TableSort.Text).Filter("all businesses");
        named.Filters[0].Names.Add("Corner Shop");
        named.Filters[0].Names.Add("Flower Stall");
        var nm = new TableModel();
        nm.Begin(named, 3);
        nm.Kind(0, 0, 0);
        nm.Kind(1, 0, 1);
        nm.Kind(2, 0, 1);
        nm.Apply();
        nm.Choose(0, 1);
        Check(nm.Count == 2 && named.Filters[0].ChosenName == "Flower Stall", "a kind the app named is remembered by its name");
        named.Filters[0].Names.Clear();
        named.Filters[0].Names.Add("Bakery");
        named.Filters[0].Names.Add("Corner Shop");
        named.Filters[0].Names.Add("Flower Stall");
        nm.Begin(named, 3);
        nm.Kind(0, 0, 2);
        nm.Kind(1, 0, 0);
        nm.Kind(2, 0, 1);
        nm.Apply();
        Check(nm.Count == 1 && nm.RowAt(0) == 0 && named.Filters[0].Chosen == 2, "so it is still the same business when the list has changed");
        named.Filters[0].Names.Clear();
        named.Filters[0].Names.Add("Bakery");
        named.Filters[0].Names.Add("Corner Shop");
        nm.Begin(named, 2);
        nm.Kind(0, 0, 0);
        nm.Kind(1, 0, 1);
        nm.Apply();
        Check(nm.Count == 2 && named.Filters[0].Chosen == -1 && named.Filters[0].ChosenName == null, "and 'all' when that business is gone");

        // ---- more columns and filters than the game's table has are not taken
        var wide = new TableSpec("Check", "Wide");
        for (int i = 0; i < 12; i++)
        {
            wide.Column("c" + i, 10f, TableSort.Number);
        }
        wide.Filter("a", TableKinds).Filter("b", TableKinds).Filter("c", TableKinds);
        Check(wide.Count == TableSpec.MaxColumns && wide.FilterCount == TableSpec.MaxFilters && wide.Id == "Check, Wide", "a specification holds as many columns and filters as the game's table has room for");

        TableCost();
        CashCategories();
    }

    // What a movement of money was for, told from its transaction type (Economy/Finance/CashCategory.cs).
    private static void CashCategories()
    {
        Check(CashCategory.Keys.Length == CashCategory.Count, "every category of money has a name");
        Check(CashCategory.For("ba:transaction_revenue") == CashCategory.Takings && CashCategory.For("reallife_transaction_macro_takings") == CashCategory.Takings, "a business's takings are takings");
        Check(CashCategory.For("ba:transaction_rent") == CashCategory.Rent && CashCategory.For("ba:transaction_rentrevenue") == CashCategory.Rent, "rent paid and rent received are both rent");
        Check(CashCategory.For("ba:transaction_wage") == CashCategory.Wages && CashCategory.For("ba:transaction_playerjobsalary") == CashCategory.Wages
              && CashCategory.For("reallife_transaction_job_courier") == CashCategory.Wages && CashCategory.For("reallife_transaction_co_overtime") == CashCategory.Wages, "wages, a salary, a job's pay and overtime are wages");
        Check(CashCategory.For("ba:transaction_taxpayment") == CashCategory.Tax && CashCategory.For("reallife_transaction_co_backtax") == CashCategory.Tax, "tax is tax");
        Check(CashCategory.For("ba:transaction_taxiride") == CashCategory.Other && CashCategory.For("reallife_transaction_job_taxi") == CashCategory.Wages, "a taxi ride is no tax, and a shift in a taxi is pay");
        Check(CashCategory.For("ba:transaction_loanpayment") == CashCategory.Loans && CashCategory.For("reallife_transaction_late_fee") == CashCategory.Loans
              && CashCategory.For("reallife_transaction_credit_payment") == CashCategory.Loans && CashCategory.For("reallife_transaction_takings_diverted") == CashCategory.Loans, "loan payments, a late fee, a card payment and diverted takings are loans and credit");
        Check(CashCategory.For("ba:transaction_licensingfee") == CashCategory.Fees && CashCategory.For("ba:transaction_parkingticket") == CashCategory.Fees
              && CashCategory.For("reallife_transaction_court_fine") == CashCategory.Fees, "fees, tickets and fines are fees and fines");
        Check(CashCategory.For("reallife_transaction_coffee") == CashCategory.Other && CashCategory.For("reallife_transaction_social_coffee") == CashCategory.Other, "a coffee is no fee");
        Check(CashCategory.For("ba:transaction_marketing") == CashCategory.Marketing && CashCategory.For("reallife_transaction_co_campaign") == CashCategory.Marketing, "marketing and a campaign are marketing");
        Check(CashCategory.For("reallife_transaction_shares_bought") == CashCategory.Investments && CashCategory.For("ba:transaction_buildingsold") == CashCategory.Investments
              && CashCategory.For("ba:transaction_investmentpayout") == CashCategory.Investments && CashCategory.For("reallife_transaction_venture_invested") == CashCategory.Investments, "shares, buildings, funds and investors' money are investments");
        Check(CashCategory.For("ba:transaction_itempurchase") == CashCategory.Stock && CashCategory.For("ba:transaction_importdelivery") == CashCategory.Stock, "purchases and deliveries are stock");
        Check(CashCategory.For("ba:transaction_itemsold") == CashCategory.Takings && CashCategory.For("ba:transaction_businessinventorysold") == CashCategory.Takings, "goods sold are takings");
        Check(CashCategory.For(null) == CashCategory.Other && CashCategory.For(string.Empty) == CashCategory.Other && CashCategory.For("ba:transaction_subwayride") == CashCategory.Other, "anything else, and a transaction with no type, is 'other'");
        Check(CashCategory.For("ba:transaction_rent") == CashCategory.Rent, "a type is read the same the second time (it is remembered)");
    }

    // 5,000 rows: stable against a simple reference, nothing allocated by a second sort, and the time it takes.
    private static void TableCost()
    {
        const int Rows = 5000;
        TableSpec spec = new TableSpec("Check", "Large")
            .Column("name", 100f, TableSort.Text)
            .Column("money", 100f, TableSort.Money, sums: true)
            .Column("day", 100f, TableSort.Date)
            .Filter("all", TableKinds);
        var model = new TableModel();
        var random = new Random(20261011);
        var money = new double[Rows];
        var names = new string[Rows];
        model.Begin(spec, Rows);
        double sum = 0;
        for (int r = 0; r < Rows; r++)
        {
            money[r] = random.Next(-400, 400);            // many ties: 5,000 rows over 800 values
            names[r] = "Row " + random.Next(0, 100000).ToString("00000");
            sum += money[r];
            model.Set(r, 0, double.NaN, names[r]);
            model.Set(r, 1, money[r], null);
            model.Set(r, 2, random.Next(1, 60), null);
            model.Kind(r, 0, r % 3);
        }
        model.Apply();
        Check(model.Count == Rows && Math.Abs(model.Total(1) - sum) < 1e-6, "5,000 rows are shown and summed");

        model.SortBy(1);
        bool ordered = true, stable = true;
        for (int i = 1; i < Rows; i++)
        {
            int a = model.RowAt(i - 1), b = model.RowAt(i);
            ordered &= money[a] <= money[b];
            stable &= money[a] != money[b] || a < b;
        }
        Check(ordered && stable, "5,000 rows by money, ascending: in order, and equal amounts in the order they were written");
        model.SortBy(1);
        ordered = stable = true;
        for (int i = 1; i < Rows; i++)
        {
            int a = model.RowAt(i - 1), b = model.RowAt(i);
            ordered &= money[a] >= money[b];
            stable &= money[a] != money[b] || a < b;
        }
        Check(ordered && stable, "descending: in order, and equal amounts still in the order they were written");
        model.SortBy(0);
        ordered = true;
        for (int i = 1; i < Rows; i++)
        {
            ordered &= string.Compare(names[model.RowAt(i - 1)], names[model.RowAt(i)], StringComparison.OrdinalIgnoreCase) <= 0;
        }
        Check(ordered, "5,000 rows by name: in order");
        var seen = new bool[Rows];
        bool each = true;
        for (int i = 0; i < Rows; i++)
        {
            each &= !seen[model.RowAt(i)];
            seen[model.RowAt(i)] = true;
        }
        Check(each, "every row is there once after the sorts");

        // Nothing allocated by sorting or filtering again (the arrays were made by the first).
        model.SortBy(1);
        model.Choose(0, 1);
        model.Choose(0, -1);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        model.SortBy(1);
        model.SortBy(2);
        model.SortBy(0);
        model.Choose(0, 2);
        model.Choose(0, -1);
        model.Apply();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(bytes == 0, "a second sort, a filter and a re-count of 5,000 rows allocate nothing (" + bytes + " bytes)");
        model.Begin(spec, Rows);
        long refill = GC.GetAllocatedBytesForCurrentThread();
        model.Begin(spec, Rows);
        for (int r = 0; r < Rows; r++)
        {
            model.Set(r, 1, money[r], null);
            model.Kind(r, 0, r % 3);
        }
        model.Apply();
        refill = GC.GetAllocatedBytesForCurrentThread() - refill;
        Check(refill == 0, "writing a table of the same size again allocates nothing either (" + refill + " bytes)");
        for (int r = 0; r < Rows; r++)
        {
            model.Set(r, 0, double.NaN, names[r]);
            model.Set(r, 2, r % 59, null);
        }

        // The cost: the best of several runs, so that another program busy on this machine does not fail the check.
        double byMoney = BestMs(() => model.SortBy(1));
        double byName = BestMs(() => model.SortBy(0));
        double byFilter = BestMs(() => model.Choose(0, (spec.Filters[0].Chosen + 2) % 4 - 1));
        Check(byMoney < 2.0, "5,000 rows are sorted by a money column in under 2 ms (" + byMoney.ToString("0.000") + " ms)");
        Check(byName < 2.0, "and by a text column (" + byName.ToString("0.000") + " ms)");
        Check(byFilter < 2.0, "a filter click over 5,000 rows, with its sort and totals, in under 2 ms (" + byFilter.ToString("0.000") + " ms)");
        Console.WriteLine("Table cost, 5,000 rows: sort by money " + byMoney.ToString("0.000") + " ms, by text " + byName.ToString("0.000") + " ms, a filter click " + byFilter.ToString("0.000") + " ms.");

        var small = new TableModel();
        TableSpec forty = new TableSpec("Check", "Forty").Column("money", 100f, TableSort.Money, sums: true).Filter("all", TableKinds);
        small.Begin(forty, 40);
        for (int r = 0; r < 40; r++)
        {
            small.Set(r, 0, money[r], null);
            small.Kind(r, 0, r % 3);
        }
        small.Apply();
        double click = BestMs(() => small.SortBy(0));
        Check(click < 0.05, "a table the size of an app's (40 rows) is sorted in under 0.05 ms (" + click.ToString("0.0000") + " ms)");
        Console.WriteLine("Table cost, 40 rows: a sort click " + (click * 1000).ToString("0.0") + " microseconds.");
    }

    private static double BestMs(Action action)
    {
        for (int i = 0; i < 20; i++)
        {
            action();   // (the first runs are the compiler's)
        }
        double best = double.MaxValue;
        var watch = new Stopwatch();
        for (int i = 0; i < 60; i++)
        {
            watch.Restart();
            action();
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        return best;
    }
}
