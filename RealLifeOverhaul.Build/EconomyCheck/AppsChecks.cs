using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ledger;

// The phone apps' clicks (14 Oct 2026, "start the optimization work of this mod. Completely."): a table of thousands of rows
// written only as it scrolls (Core/GameUi/TableModel.cs: ITableSource, TableRow; Economy/Finance/CashTable.cs), checked on
// 30,000 generated movements of money against the same table written whole as the Cash flow tab wrote it; the City Ledger's
// pages kept for the paper they were laid out for (Ledger/LedgerPageStamp.cs); and a photo's small copy for Flare made off
// the main thread a row at a time (Core/PhotoThumbs.cs), against PNGs made here with every filter of the format and a
// reference drawing of the engine's.
internal static partial class Program
{
    private static void Apps()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        AppsRowParts();
        AppsCashSame();
        AppsMixedSame();
        AppsCashWordsWhenNeeded();
        AppsCashCost();
        AppsLedgerStamp();
        AppsPngRows();
        AppsThumbSame();
        AppsThumbRefused();
        AppsThumbFile();
        AppsThumbMemory();
        Console.WriteLine("  the apps' clicks: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- a table's row

    private sealed class AppsOneRow : ITableSource
    {
        public int Writes;
        public int Count => 1;
        public double Key(int row, int column) => column == 1 ? 7.5 : double.NaN;
        public int Kind(int row, int filter) => filter == 0 ? 2 : -1;
        public string Text(int row, int column) => column == 0 ? "a" : null;

        public void Write(int row, TableRow into)
        {
            Writes++;
            into.Id = "one";
            into.Kind = 2;
            into.Set(0, "a");
            into.Set(1, "7.5", 7.5, TableRow.Green);
        }
    }

    private static void AppsRowParts()
    {
        var row = new TableRow();
        Check(double.IsNaN(row.KeyAt(4)) && row.TextAt(4) == null && row.ToneAt(4) == (byte)CellTone.Plain && double.IsNaN(row.Key[4]), "apps: a new row has no figure, no words and the plain tone in every cell");
        row.Reset("x").Set(3, "three", 3.0, TableRow.Red);
        Check(row.Id == "x" && row.KeyAt(3) == 3.0 && row.TextAt(3) == "three" && row.ToneAt(3) == (byte)CellTone.Red && double.IsNaN(row.KeyAt(2)), "apps: a cell set with a figure keeps its words, figure and tone");
        row.Set(3, "words");
        Check(double.IsNaN(row.KeyAt(3)) && row.ToneAt(3) == (byte)CellTone.Plain, "apps: a cell set again with words alone has no figure and the plain tone");
        row.Pick = true;
        row.Kind = 4;
        row.Reset("y");
        Check(row.Id == "y" && row.TextAt(3) == null && !row.Pick && row.Kind == -1 && row.Kind2 == -1 && row.Index == 0, "apps: a row used again is empty");
        var one = new AppsOneRow();
        row.Stand(one, 0);
        Check(row.Id == null && row.TextAt(0) == null && one.Writes == 0, "apps: a row standing for a source's row is not written until asked");
        row.Ready();
        row.Ready();
        Check(one.Writes == 1 && row.Id == "one" && row.TextAt(0) == "a" && row.KeyAt(1) == 7.5 && row.ToneAt(1) == (byte)CellTone.Green && row.Kind == 2, "apps: it is written from its source once, when first asked");
        row.Reset("z");
        row.Ready();
        Check(one.Writes == 1 && row.Id == "z", "apps: a row an app wrote is never written over from a source it stood for before");
        var bare = new TableRow();
        Check(bare.Tone == CellTone.Plain && bare.A == null, "apps: the old names of a row's cells read without making its cells");
    }

    // ---------------------------------------------------------------- the Cash flow tab: written whole, and as it scrolls

    private sealed class AppsWords : ICashWords
    {
        public int Days, Items, Categories, Moneys;

        public string Day(int day)
        {
            Days++;
            return "Day " + day.ToString(CultureInfo.InvariantCulture);
        }

        public string Item(string type, Dictionary<string, string> data)
        {
            Items++;
            return data != null && data.TryGetValue("name", out string name) ? type + " for " + name : type;
        }

        public string Category(int category)
        {
            Categories++;
            return "category " + category.ToString(CultureInfo.InvariantCulture);
        }

        public string Money(float amount)
        {
            Moneys++;
            return amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public int All => Days + Items + Categories + Moneys;
    }

    private static readonly string[] AppsTypes =
    {
        "ba:transaction_rent", "reallife_transaction_loan_payment", "wage", "itempurchase", "revenue", "marketing_campaign", "fine", "", "tax_levy",
        "investment_shares_bought", "util_power", "ba:transaction_bonus"
    };

    // A late game's month: 30,000 movements, oldest first, as the game keeps them (none of nought: CashFlow.Read leaves those out).
    private static List<CashEntry> AppsEntries(int count, int seed)
    {
        var r = new Random(seed);
        var names = new[] { "Alpha Shop", "Bravo Bar", "Charlie Cars", "Delta Deli" };
        var list = new List<CashEntry>(count);
        for (int i = 0; i < count; i++)
        {
            string type = AppsTypes[r.Next(AppsTypes.Length)];
            Dictionary<string, string> data = r.Next(3) == 0 ? null : new Dictionary<string, string> { { "name", names[r.Next(names.Length)] } };
            float amount = (float)Math.Round((r.NextDouble() - 0.6) * 5000.0, 2);
            if (amount == 0f)
            {
                amount = 1f;
            }
            if (r.Next(50) == 0)
            {
                amount = 125f;   // (equal amounts: the order among them is the order written)
            }
            list.Add(new CashEntry { Type = type, Data = data, Amount = amount, Day = 1 + i * 30 / count, Category = CashCategory.For(type) });
        }
        return list;
    }

    // The Cash flow tab's rows as it wrote every one of them before (FinanceApp.WriteCashFlow, 0.19.1): newest first.
    private static List<TableRow> AppsWhole(List<CashEntry> entries, ICashWords words)
    {
        var rows = new List<TableRow>(entries.Count);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            CashEntry entry = entries[i];
            TableRow row = new TableRow().Reset("cash:" + i);
            row.Kind = entry.Category;
            row.Set(0, words.Day(entry.Day), entry.Day);
            row.Set(1, words.Item(entry.Type, entry.Data));
            row.Set(2, words.Category(entry.Category));
            if (entry.Amount > 0f)
            {
                row.Set(3, words.Money(entry.Amount), entry.Amount, TableRow.Green);
            }
            else
            {
                row.Set(4, words.Money(-entry.Amount), -entry.Amount, TableRow.Red);
            }
            rows.Add(row);
        }
        return rows;
    }

    // As FinanceTables.Cash declares it (a spec of its own for each model: a spec keeps what the player chose).
    private static TableSpec AppsCashSpec()
    {
        return new TableSpec("Check", "Cash flow")
            .Column("day", 200f, TableSort.Date)
            .Column("what", 760f, TableSort.Text, flexible: true)
            .Column("category", 420f, TableSort.Text)
            .Column("in", 300f, TableSort.Money, sums: true)
            .Column("out", 300f, TableSort.Money, sums: true, right: true)
            .Filter("all", CashCategory.Keys)
            .Order(0, false)
            .Empty("none");
    }

    private static bool AppsSameModel(TableModel a, TableModel b, out string why)
    {
        why = null;
        if (a.Count != b.Count || a.Pinned != b.Pinned || a.Rows != b.Rows)
        {
            why = "counts " + a.Count + "/" + b.Count;
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a.RowAt(i) != b.RowAt(i))
            {
                why = "place " + i + ": row " + a.RowAt(i) + " against " + b.RowAt(i);
                return false;
            }
        }
        for (int c = 0; c < a.Spec.Count; c++)
        {
            if (a.Total(c) != b.Total(c))
            {
                why = "total of column " + c;
                return false;
            }
        }
        for (int k = 0; k < CashCategory.Count; k++)
        {
            if (a.KindCount(0, k) != b.KindCount(0, k))
            {
                why = "count of kind " + k;
                return false;
            }
        }
        if (a.KindsHeld(0) != b.KindsHeld(0) || a.Spec.Filters[0].Chosen != b.Spec.Filters[0].Chosen)
        {
            why = "kinds held";
            return false;
        }
        return true;
    }

    private static bool AppsSameRow(TableRow a, TableRow b)
    {
        if (a.Id != b.Id || a.Kind != b.Kind || a.Kind2 != b.Kind2 || a.Info != b.Info || a.Pick != b.Pick || a.Pinned != b.Pinned || a.NoTotal != b.NoTotal || a.Index != b.Index)
        {
            return false;
        }
        for (int c = 0; c < TableSpec.MaxColumns; c++)
        {
            double x = a.KeyAt(c), y = b.KeyAt(c);
            if (a.TextAt(c) != b.TextAt(c) || a.ToneAt(c) != b.ToneAt(c) || !(x == y || (double.IsNaN(x) && double.IsNaN(y))))
            {
                return false;
            }
        }
        return true;
    }

    private static void AppsCashSame()
    {
        List<CashEntry> entries = AppsEntries(30000, 21);
        var words = new AppsWords();
        List<TableRow> whole = AppsWhole(entries, words);
        var source = new CashTable(entries, words);
        TableSpec wholeSpec = AppsCashSpec(), sourceSpec = AppsCashSpec();
        var written = new TableModel();
        var scrolled = new TableModel();
        written.Begin(wholeSpec, whole);
        written.Apply();
        scrolled.Begin(sourceSpec, source);
        scrolled.Apply();
        bool same = AppsSameModel(written, scrolled, out string why);
        Check(same, "apps: the Cash flow table written as it scrolls is in the order, with the totals and kinds, of the table written whole (opening order, newest first)" + (why != null ? ": " + why : string.Empty));

        // every column ordered both ways, every category picked and all again, and orders under a filter
        int steps = 0;
        bool all = true;
        string failed = null;
        for (int c = 0; c < wholeSpec.Count && all; c++)
        {
            for (int twice = 0; twice < 2 && all; twice++)
            {
                written.SortBy(c);
                scrolled.SortBy(c);
                steps++;
                all &= AppsSameModel(written, scrolled, out why);
                failed = all ? null : "ordered by column " + c + (twice == 0 ? " ascending" : " descending") + ": " + why;
            }
        }
        for (int k = -1; k < CashCategory.Count && all; k++)
        {
            written.Choose(0, k);
            scrolled.Choose(0, k);
            steps++;
            all &= AppsSameModel(written, scrolled, out why);
            failed = all ? null : "filtered to kind " + k + ": " + why;
            for (int c = 1; c < 3 && all; c++)
            {
                written.SortBy(c);
                scrolled.SortBy(c);
                steps++;
                all &= AppsSameModel(written, scrolled, out why);
                failed = all ? null : "filtered to kind " + k + " and ordered by column " + c + ": " + why;
            }
        }
        Check(all && steps > 30, "apps: ordered by every column both ways and filtered by every category, the table written as it scrolls shows the rows of the table written whole, in the same order, with the same totals" + (failed != null ? " (" + failed + ")" : string.Empty));

        // the rows that come into view are written as the whole table wrote them
        bool rows = true;
        var stand = new TableRow();
        for (int i = 0; i < scrolled.Count && rows; i += 97)
        {
            int r = scrolled.RowAt(i);
            stand.Stand(source, r).Ready();
            rows &= AppsSameRow(stand, whole[r]);
        }
        for (int r = 0; r < 40 && rows; r++)
        {
            stand.Stand(source, r).Ready();
            rows &= AppsSameRow(stand, whole[r]);
        }
        Check(rows, "apps: a row written as it comes into view has the id, kind, words, figures and tones the whole table gave it");

        var none = new CashTable(new List<CashEntry>(), words);
        var empty = new TableModel();
        empty.Begin(AppsCashSpec(), none);
        empty.Apply();
        Check(empty.Count == 0 && empty.Rows == 0 && empty.Total(3) == 0 && empty.KindsHeld(0) == 0, "apps: a period with no movement is an empty table");
        var single = AppsEntries(1, 3);
        var oneModel = new TableModel();
        oneModel.Begin(AppsCashSpec(), new CashTable(single, words));
        oneModel.Apply();
        Check(oneModel.Count == 1 && oneModel.RowAt(0) == 0 && (single[0].Amount > 0f ? oneModel.Total(3) == single[0].Amount : oneModel.Total(4) == -single[0].Amount),
            "apps: a period of one movement is a table of one row, summed");
    }

    // The table checks' six rows (TableChecks.cs) as a source: words, money, a rate, a day, a column that never sorts, and a
    // column of figures where some rows have only words (ordered by their words after the figures).
    private sealed class AppsMixed : ITableSource
    {
        public int Count => TableNames.Length;

        public double Key(int row, int column)
        {
            switch (column)
            {
                case 1: return TableOwed[row];
                case 2: return TableRate[row];
                case 3: return TableDue[row];
                case 4: return 99 - row;
                case 5: return TableMixedKey[row];
                default: return double.NaN;
            }
        }

        public int Kind(int row, int filter) => filter == 0 ? TableKind[row] : filter == 1 ? TableState[row] : -1;
        public string Text(int row, int column) => column == 0 ? TableNames[row] : column == 4 ? "never read" : column == 5 ? TableMixedText[row] : null;

        public void Write(int row, TableRow into)
        {
            into.Id = "mixed:" + row;
            into.Kind = TableKind[row];
            into.Kind2 = TableState[row];
            for (int c = 0; c < 6; c++)
            {
                into.Set(c, Text(row, c), Key(row, c));
            }
        }
    }

    private static void AppsMixedSame()
    {
        var source = new AppsMixed();
        var rows = new List<TableRow>();
        for (int r = 0; r < source.Count; r++)
        {
            var row = new TableRow();
            row.Stand(source, r).Ready();
            rows.Add(row);
        }
        TableSpec wholeSpec = TableSpecOf(), sourceSpec = TableSpecOf();
        var written = new TableModel();
        var scrolled = new TableModel();
        written.Begin(wholeSpec, rows);
        scrolled.Begin(sourceSpec, source);
        written.Apply();
        scrolled.Apply();
        bool all = AppsSameOrder(written, scrolled);
        for (int c = 0; c < wholeSpec.Count && all; c++)
        {
            for (int twice = 0; twice < 2 && all; twice++)
            {
                written.SortBy(c);
                scrolled.SortBy(c);
                all &= AppsSameOrder(written, scrolled) && written.Total(1) == scrolled.Total(1);
            }
        }
        for (int k = -1; k < TableKinds.Length && all; k++)
        {
            written.Choose(0, k);
            scrolled.Choose(0, k);
            all &= AppsSameOrder(written, scrolled) && written.Total(1) == scrolled.Total(1);
        }
        Check(all, "apps: a table of words, money, rates, days and a column of figures and words orders and filters the same written as it scrolls as written whole");
    }

    private static bool AppsSameOrder(TableModel a, TableModel b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a.RowAt(i) != b.RowAt(i))
            {
                return false;
            }
        }
        return true;
    }

    private static void AppsCashWordsWhenNeeded()
    {
        List<CashEntry> entries = AppsEntries(30000, 22);
        var words = new AppsWords();
        var source = new CashTable(entries, words);
        var model = new TableModel();
        TableSpec spec = AppsCashSpec();
        model.Begin(spec, source);
        model.Apply();
        Check(words.All == 0, "apps: opening the Cash flow tab on 30,000 movements makes no words at all (" + words.All + " made): it is ordered by day and summed by the figures");
        var stand = new TableRow();
        for (int i = 0; i < 15; i++)
        {
            stand.Stand(source, model.RowAt(i)).Ready();
        }
        Check(words.Days == 15 && words.Items == 15 && words.Categories == 15 && words.Moneys == 15, "apps: the fifteen rows in view make fifteen rows of words");
        int before = words.All;
        for (int k = 0; k < CashCategory.Count; k++)
        {
            model.Choose(0, k);
        }
        model.Choose(0, -1);
        model.SortBy(3);
        model.SortBy(4);
        model.SortBy(0);
        Check(words.All == before, "apps: picking every category and ordering by money in, money out and the day make no words");
        model.SortBy(1);
        Check(words.Items - 15 == entries.Count && words.All - before == entries.Count, "apps: ordering by what each was makes that column's words once for each movement, and no others");
        model.SortBy(1);
        model.Choose(0, 2);
        Check(words.All - before == entries.Count, "apps: ordering by it again, and filtering it, uses the words made");
        model.Begin(spec, source);
        model.SortBy(2);
        Check(words.Categories - 15 == entries.Count, "apps: written again, a column's words are made again when the order needs them");
    }

    private static void AppsCashCost()
    {
        List<CashEntry> entries = AppsEntries(30000, 23);
        var words = new AppsWords();
        var source = new CashTable(entries, words);
        var model = new TableModel();
        TableSpec spec = AppsCashSpec();
        for (int warm = 0; warm < 40; warm++)
        {
            AppsOpenScrolled(model, spec, source, out _, out _);   // (warmed: the model's arrays made once, and its methods past the runtime's tiering, whose own stubs would be counted)
        }
        AppsOpenScrolled(model, spec, source, out double scrolledMs, out long scrolledBytes);
        AppsOpenWhole(entries, words, out double wholeMs);
        AppsOpenWhole(entries, words, out wholeMs);
        Check(scrolledMs < 25.0 && scrolledMs * 3.0 < wholeMs, "apps: the Cash flow tab on 30,000 movements is ordered and summed in " + scrolledMs.ToString("0.0") + " ms written as it scrolls, against "
                                                              + wholeMs.ToString("0.0") + " ms written whole (wanted: under 25 ms, and under a third)");
        Check(scrolledBytes < 1024, "apps: written again as it scrolls, the table allocates nothing (" + scrolledBytes + " bytes)");
        AppsFilter(model, out double filterMs, out long filterBytes);
        Console.WriteLine("  apps: the Cash flow tab on 30,000 movements: " + wholeMs.ToString("0.0") + " ms written whole, " + scrolledMs.ToString("0.0") + " ms written as it scrolls ("
                          + scrolledBytes + " bytes); every category picked and all again " + filterMs.ToString("0.00") + " ms");
        Check(filterMs < 10.0 && filterBytes < 1024, "apps: picking a category filters what is held: " + filterMs.ToString("0.00") + " ms and " + filterBytes + " bytes for every category and all again");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void AppsOpenScrolled(TableModel model, TableSpec spec, CashTable source, out double ms, out long bytes)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        model.Begin(spec, source);
        model.Apply();
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void AppsOpenWhole(List<CashEntry> entries, ICashWords words, out double ms)
    {
        var watch = Stopwatch.StartNew();
        List<TableRow> rows = AppsWhole(entries, words);
        var model = new TableModel();
        model.Begin(AppsCashSpec(), rows);
        model.Apply();
        ms = watch.Elapsed.TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void AppsFilter(TableModel model, out double ms, out long bytes)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int k = 0; k < CashCategory.Count; k++)
        {
            model.Choose(0, k);
        }
        model.Choose(0, -1);
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    // ---------------------------------------------------------------- the City Ledger's pages

    private static void AppsLedgerStamp()
    {
        var edition = new LedgerEdition { Day = 4 };
        object face = new object();
        LedgerPageStamp Stamp() => new LedgerPageStamp { Paper = edition, Language = "en", Width = 1800f, Height = 900f, Pixel = 1.5f, Face = face, Copy = 3, Week = 15, Laid = true };
        LedgerPageStamp laid = Stamp();
        Check(Stamp().Same(laid), "apps: a page turned to again for the same paper, language, sheet, face and prices is kept as it was laid out");
        Check(!Stamp().Same(default(LedgerPageStamp)) && !default(LedgerPageStamp).Same(default(LedgerPageStamp)), "apps: a page never laid out is laid out");
        LedgerPageStamp language = Stamp();
        language.Language = new string(new[] { 'e', 'n' });
        Check(language.Same(laid), "apps: the language is compared by its name, not by the string object");
        var changes = new List<LedgerPageStamp>();
        LedgerPageStamp s;
        s = Stamp(); s.Paper = new LedgerEdition { Day = 4 }; changes.Add(s);
        s = Stamp(); s.Language = "pt"; changes.Add(s);
        s = Stamp(); s.Width = 1799f; changes.Add(s);
        s = Stamp(); s.Height = 901f; changes.Add(s);
        s = Stamp(); s.Pixel = 1f; changes.Add(s);
        s = Stamp(); s.Face = null; changes.Add(s);
        s = Stamp(); s.Copy = 4; changes.Add(s);
        s = Stamp(); s.Week = 16; changes.Add(s);
        bool every = true;
        for (int i = 0; i < changes.Count; i++)
        {
            every &= !changes[i].Same(laid) && !laid.Same(changes[i]);
        }
        Check(every, "apps: a new edition (even of the same day), another language, sheet size, pixel, face or price lays the page out again");
    }

    // ---------------------------------------------------------------- a photo's small copy

    // A PNG of these pixels (rows from the top, 'channels' bytes a pixel), each row with the filter given, its image data in
    // chunks of 'chunk' bytes, as any writer of the format may make it.
    private static byte[] AppsPng(byte[] pixels, int w, int h, int channels, Func<int, int> filterOf, int chunk, int depth = 8)
    {
        int colour = channels == 1 ? 0 : channels == 2 ? 4 : channels == 3 ? 2 : 6;
        int stride = w * channels;
        var raw = new MemoryStream();
        var previous = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int f = filterOf(y);
            raw.WriteByte((byte)f);
            for (int i = 0; i < stride; i++)
            {
                int x = pixels[y * stride + i], left = i >= channels ? pixels[y * stride + i - channels] : 0, up = previous[i];
                int corner = i >= channels ? previous[i - channels] : 0;
                int p = left + up - corner, pa = Math.Abs(p - left), pb = Math.Abs(p - up), pc = Math.Abs(p - corner);
                int paeth = pa <= pb && pa <= pc ? left : pb <= pc ? up : corner;
                int v = f == 0 ? x : f == 1 ? x - left : f == 2 ? x - up : f == 3 ? x - ((left + up) >> 1) : x - paeth;
                raw.WriteByte((byte)v);
            }
            Buffer.BlockCopy(pixels, y * stride, previous, 0, stride);
        }
        byte[] plain = raw.ToArray();
        var packed = new MemoryStream();
        packed.WriteByte(0x78);
        packed.WriteByte(0x9C);
        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            deflate.Write(plain, 0, plain.Length);
        }
        uint a = 1, b = 0;
        foreach (byte x in plain)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        uint adler = (b << 16) | a;
        packed.WriteByte((byte)(adler >> 24));
        packed.WriteByte((byte)(adler >> 16));
        packed.WriteByte((byte)(adler >> 8));
        packed.WriteByte((byte)adler);
        byte[] data = packed.ToArray();
        var png = new MemoryStream();
        png.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10 }, 0, 8);
        var head = new byte[13];
        AppsBig(head, 0, w);
        AppsBig(head, 4, h);
        head[8] = (byte)depth;
        head[9] = (byte)colour;
        AppsChunk(png, "IHDR", head, 0, 13);
        AppsChunk(png, "tEXt", new byte[] { (byte)'a', 0, (byte)'b' }, 0, 3);   // (a chunk of no interest before the data)
        for (int at = 0; at < data.Length; at += chunk)
        {
            AppsChunk(png, "IDAT", data, at, Math.Min(chunk, data.Length - at));
        }
        AppsChunk(png, "IEND", new byte[0], 0, 0);
        return png.ToArray();
    }

    private static void AppsBig(byte[] into, int at, int value)
    {
        into[at] = (byte)(value >> 24);
        into[at + 1] = (byte)(value >> 16);
        into[at + 2] = (byte)(value >> 8);
        into[at + 3] = (byte)value;
    }

    private static void AppsChunk(Stream png, string type, byte[] data, int at, int length)
    {
        var head = new byte[8];
        AppsBig(head, 0, length);
        for (int i = 0; i < 4; i++)
        {
            head[4 + i] = (byte)type[i];
        }
        png.Write(head, 0, 8);
        png.Write(data, at, length);
        uint crc = 0xFFFFFFFF;
        for (int i = 4; i < 8; i++)
        {
            crc = AppsCrc(crc, head[i]);
        }
        for (int i = 0; i < length; i++)
        {
            crc = AppsCrc(crc, data[at + i]);
        }
        crc ^= 0xFFFFFFFF;
        var tail = new byte[4];
        AppsBig(tail, 0, (int)crc);
        png.Write(tail, 0, 4);
    }

    private static uint AppsCrc(uint crc, byte b)
    {
        crc ^= b;
        for (int k = 0; k < 8; k++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }

    // A photo of a city at dusk, roughly: smooth gradients with edges and noise, so every filter has work to do.
    private static byte[] AppsPicture(int w, int h, int channels, int seed)
    {
        var r = new Random(seed);
        var pixels = new byte[w * h * channels];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                for (int c = 0; c < channels; c++)
                {
                    double v = 128 + 100 * Math.Sin(x * (0.013 + c * 0.004) + y * 0.021) + ((x / 37 + y / 23) % 2 == 0 ? 20 : -20) + r.Next(-12, 13);
                    pixels[(y * w + x) * channels + c] = (byte)Math.Max(0, Math.Min(255, (int)v));
                }
            }
        }
        return pixels;
    }

    private static void AppsPngRows()
    {
        bool every = true;
        string failed = null;
        int[] channelCounts = { 1, 2, 3, 4 };
        foreach (int channels in channelCounts)
        {
            int w = 97, h = 61;
            byte[] pixels = AppsPicture(w, h, channels, channels);
            byte[] png = AppsPng(pixels, w, h, channels, y => y % 5, 333);
            var reader = new PhotoThumbs.PngRows(new MemoryStream(png));
            bool opened = reader.Open() == PhotoThumbs.PngRows.Opened.Yes && reader.Width == w && reader.Height == h;
            var rgb = new byte[w * 3];
            for (int y = 0; y < h && opened; y++)
            {
                opened &= reader.Next(rgb);
                for (int x = 0; x < w && opened; x++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int from = channels >= 3 ? k : 0;
                        opened &= rgb[x * 3 + k] == pixels[(y * w + x) * channels + from];
                    }
                }
            }
            opened &= !reader.Next(rgb);
            every &= opened;
            if (!opened && failed == null)
            {
                failed = channels + " channel(s)";
            }
        }
        Check(every, "apps: a PNG is read row by row to its exact pixels: grey, grey and alpha, colour, colour and alpha, every one of the five filters, data in many chunks" + (failed != null ? " (wrong: " + failed + ")" : string.Empty));
    }

    // The small copy as the engine drew it, worked out here independently, in double precision: each pixel of the copy (rows
    // from the bottom) sampled at its centre between the four nearest of the photo, the photo's rows from the bottom as the
    // engine's texture has them, in linear light.
    private static byte[] AppsReference(byte[] rgbTop, int w, int h, out int tw, out int th)
    {
        tw = 256;
        th = Math.Max(1, h * 256 / w);
        var small = new byte[tw * th * 3];
        for (int y = 0; y < th; y++)
        {
            double ty = (y + 0.5) / th * h - 0.5;
            int y0 = (int)Math.Floor(ty);
            double fy = ty - y0;
            for (int x = 0; x < tw; x++)
            {
                double tx = (x + 0.5) / tw * w - 0.5;
                int x0 = (int)Math.Floor(tx);
                double fx = tx - x0;
                for (int k = 0; k < 3; k++)
                {
                    double Texel(int tx2, int ty2) => AppsLinear(rgbTop[((h - 1 - ty2) * w + tx2) * 3 + k]);
                    double v = Texel(x0, y0) * (1 - fx) * (1 - fy) + Texel(x0 + 1, y0) * fx * (1 - fy) + Texel(x0, y0 + 1) * (1 - fx) * fy + Texel(x0 + 1, y0 + 1) * fx * fy;
                    double c = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
                    small[(y * tw + x) * 3 + k] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(c * 255)));
                }
            }
        }
        return small;
    }

    private static double AppsLinear(byte b)
    {
        double c = b / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static void AppsThumbSame()
    {
        Check(PhotoThumbs.Width == FlareRules.ThumbWidth, "apps: the small copy is as wide as Flare shows a picture");
        int[,] sizes = { { 1920, 1080 }, { 1366, 768 }, { 300, 211 } };
        for (int s = 0; s < sizes.GetLength(0); s++)
        {
            int w = sizes[s, 0], h = sizes[s, 1];
            byte[] rgb = AppsPicture(w, h, 3, 40 + s);
            byte[] png = AppsPng(rgb, w, h, 3, y => (y * 7) % 5, 65536);
            byte[] small = PhotoThumbs.Small(new MemoryStream(png), out int tw, out int th, out bool undecoded);
            byte[] reference = AppsReference(rgb, w, h, out int rw, out int rh);
            int most = 0;
            for (int i = 0; small != null && i < small.Length && i < reference.Length; i++)
            {
                most = Math.Max(most, Math.Abs(small[i] - reference[i]));
            }
            Check(small != null && !undecoded && tw == rw && th == rh && th == Math.Max(1, h * 256 / w) && small.Length == reference.Length && most <= 1,
                "apps: the small copy of a " + w + " by " + h + " photo is " + tw + " by " + th + ", each channel within one step of the engine's drawing (at most " + most + " off)");
        }
        byte[] rgba = AppsPicture(640, 360, 4, 9);
        byte[] colourOnly = new byte[640 * 360 * 3];
        for (int i = 0, j = 0; i < rgba.Length; i += 4, j += 3)
        {
            colourOnly[j] = rgba[i];
            colourOnly[j + 1] = rgba[i + 1];
            colourOnly[j + 2] = rgba[i + 2];
        }
        byte[] withAlpha = PhotoThumbs.Small(new MemoryStream(AppsPng(rgba, 640, 360, 4, y => 4, 4096)), out _, out _, out _);
        byte[] without = PhotoThumbs.Small(new MemoryStream(AppsPng(colourOnly, 640, 360, 3, y => 1, 4096)), out _, out _, out _);
        bool same = withAlpha != null && without != null && withAlpha.Length == without.Length;
        for (int i = 0; same && i < withAlpha.Length; i++)
        {
            same &= withAlpha[i] == without[i];
        }
        Check(same, "apps: a photo's alpha is left out of its small copy, as the engine's copy (RGB24) leaves it out");
    }

    private static void AppsThumbRefused()
    {
        byte[] rgb = AppsPicture(800, 450, 3, 77);
        byte[] png = AppsPng(rgb, 800, 450, 3, y => 2, 8192);
        byte[] cut = new byte[png.Length / 2];
        Buffer.BlockCopy(png, 0, cut, 0, cut.Length);
        Check(PhotoThumbs.Small(new MemoryStream(cut), out _, out _, out bool undecodedCut) == null && !undecodedCut, "apps: a photo still being written gives no copy yet, and is not given up on");
        Check(PhotoThumbs.Small(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }), out _, out _, out bool undecodedJunk) == null && undecodedJunk, "apps: a file that is no PNG is left to the engine");
        byte[] wide16 = AppsPng(AppsPicture(400, 10, 3, 1), 400, 10, 3, y => 0, 4096, depth: 16);
        Check(PhotoThumbs.Small(new MemoryStream(wide16), out _, out _, out bool undecoded16) == null && undecoded16, "apps: a PNG of 16 bits a channel is left to the engine");
        byte[] narrow = AppsPng(AppsPicture(256, 100, 3, 2), 256, 100, 3, y => 0, 4096);
        Check(PhotoThumbs.Small(new MemoryStream(narrow), out _, out _, out bool undecodedNarrow) == null && undecodedNarrow, "apps: a photo no wider than its copy is left to the engine");
        bool thrown = false;
        for (int at = 100; at < png.Length - 40; at += png.Length / 7)
        {
            byte[] bad = (byte[])png.Clone();
            for (int i = at; i < at + 60 && i < bad.Length; i++)
            {
                bad[i] ^= 0x5A;
            }
            try
            {
                PhotoThumbs.Small(new MemoryStream(bad), out _, out _, out _);
            }
            catch (Exception)
            {
                thrown = true;
            }
        }
        Check(!thrown, "apps: damaged image data is a copy of what it decodes to or none, never a fault");
    }

    private static void AppsThumbFile()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-apps-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            string photo = System.IO.Path.Combine(folder, "RealLifeOverhaul_20261014_120000.png");
            Check(PhotoThumbs.FileOf(photo) == System.IO.Path.Combine(folder, "Thumbs", "RealLifeOverhaul_20261014_120000.png.thumb"), "apps: a photo's small copy is kept beside it, in Thumbs");
            Directory.CreateDirectory(folder);
            byte[] rgb = AppsPicture(1280, 720, 3, 5);
            File.WriteAllBytes(photo, AppsPng(rgb, 1280, 720, 3, y => y % 5, 65536));
            byte[] made = PhotoThumbs.Get(photo, out int w, out int h, out bool undecoded);
            byte[] kept = PhotoThumbs.Read(PhotoThumbs.FileOf(photo), out int kw, out int kh);
            bool same = made != null && kept != null && kw == w && kh == h && made.Length == kept.Length && !undecoded;
            for (int i = 0; same && i < made.Length; i++)
            {
                same &= made[i] == kept[i];
            }
            Check(same && w == 256 && h == 144, "apps: an old photo's small copy is made once and kept in its file, read back as it was made");
            File.WriteAllBytes(photo, new byte[] { 0 });   // the photo gone bad: the kept copy is what is shown
            byte[] again = PhotoThumbs.Get(photo, out _, out _, out _);
            Check(again != null && again.Length == made.Length, "apps: a photo with a kept copy is not read again");
            string file = PhotoThumbs.FileOf(photo);
            byte[] whole = File.ReadAllBytes(file);
            File.WriteAllBytes(file, new ArraySegment<byte>(whole, 0, whole.Length - 5).ToArray());
            Check(PhotoThumbs.Read(file, out _, out _) == null, "apps: a copy's file that is not whole is not read");
            PhotoThumbs.Write(file, made, 256, 144);
            Check(PhotoThumbs.Read(file, out int ww, out int wh) != null && ww == 256 && wh == 144 && Directory.GetFiles(System.IO.Path.GetDirectoryName(file), "*.part").Length == 0,
                "apps: a copy's file is written whole over one that was not, and leaves no part file");
            Check(PhotoThumbs.Get(System.IO.Path.Combine(folder, "missing.png"), out _, out _, out bool gone) == null && !gone, "apps: a photo that is not there gives no copy (the post shows without it)");
        }
        finally
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void AppsThumbMemory()
    {
        byte[] rgb = AppsPicture(3840, 2160, 3, 8);
        byte[] png = AppsPng(rgb, 3840, 2160, 3, y => y % 5, 65536);
        AppsShrink(png, out _, out _);   // (warmed)
        AppsShrink(png, out double ms, out long bytes);
        Console.WriteLine("  apps: a 4K photo's small copy for Flare: " + ms.ToString("0") + " ms off the main thread, " + (bytes / 1024) + " KB allocated");
        Check(bytes < 512 * 1024, "apps: a 4K photo's small copy is made a row at a time: " + (bytes / 1024) + " KB allocated (a whole decode would be some 50 MB), in " + ms.ToString("0") + " ms off the main thread");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void AppsShrink(byte[] png, out double ms, out long bytes)
    {
        var stream = new MemoryStream(png, false);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        PhotoThumbs.Small(stream, out _, out _, out _);
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
}
