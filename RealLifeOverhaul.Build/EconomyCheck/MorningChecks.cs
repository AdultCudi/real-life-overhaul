using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// The frame a game day starts in, and the economy's hour (14 Oct 2026, "start the optimization work of this mod.
// Completely."): the economy's look-ups of a morning made by dictionaries and one walk of the day's transactions
// (Core/LookupRules). Each index gives exactly what the walk it replaces gave, on late-game figures: 300 businesses,
// 3,000 employees, 30,000 transactions.
internal static partial class Program
{
    private static void Morning()
    {
        MoIndexSame();
        MoIndexCost();
        MoSumsSame();
        MoSumsCost();
        MoDaysSame();
        MoDaysCost();
        MoRunsSame();
        MoSources();
    }

    // ---------------------------------------------------------------- stand-ins for the game's things

    // As the game's Address: equal by street and number.
    private sealed class MoAddr
    {
        public string Street;
        public int Number;

        public MoAddr(string street, int number)
        {
            Street = street;
            Number = number;
        }

        public override bool Equals(object obj)
        {
            return obj is MoAddr a && Street == a.Street && Number == a.Number;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Street, Number);
        }
    }

    // As the city's DayKey: the address (none only equals none), the category and the deduction.
    private struct MoLine
    {
        public MoAddr Address;
        public string Category;
        public bool Deductible;
    }

    private sealed class MoLineComparer : IEqualityComparer<MoLine>
    {
        public bool Equals(MoLine a, MoLine b)
        {
            return a.Deductible == b.Deductible && a.Category == b.Category
                   && (ReferenceEquals(a.Address, b.Address) || (!ReferenceEquals(a.Address, null) && !ReferenceEquals(b.Address, null) && a.Address.Equals(b.Address)));
        }

        public int GetHashCode(MoLine k)
        {
            unchecked
            {
                return ((ReferenceEquals(k.Address, null) ? 0 : k.Address.GetHashCode()) * 397 ^ (k.Category != null ? k.Category.GetHashCode() : 0)) * 2 + (k.Deductible ? 1 : 0);
            }
        }
    }

    private sealed class MoReg
    {
        public string Key;
    }

    private sealed class MoTx
    {
        public int Day;
        public float Amount;
    }

    private sealed class MoStatement
    {
        public MoAddr Address;
        public float Profit;
    }

    // ---------------------------------------------------------------- the first item by key

    private static string MoKeyOf(MoReg r)
    {
        return r.Key;
    }

    private static int MoFirst(List<MoReg> list, string key)
    {
        for (int i = 0; key != null && i < list.Count; i++)
        {
            if (list[i] != null && list[i].Key != null && string.Equals(list[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static List<MoReg> MoRegs(int count, int keys, Random r)
    {
        var list = new List<MoReg>(count);
        for (int i = 0; i < count; i++)
        {
            int k = r.Next(keys);
            list.Add(i % 97 == 13 ? null : new MoReg { Key = i % 31 == 7 ? null : "street " + k + "#" + (k % 40) });
        }
        return list;
    }

    private static void MoIndexSame()
    {
        var r = new Random(12);
        List<MoReg> regs = MoRegs(3000, 2600, r);
        var index = new ListIndex<MoReg, string>(MoKeyOf, StringComparer.Ordinal);
        bool same = true;
        for (int k = 0; k < 2700; k++)
        {
            string key = "street " + k + "#" + (k % 40);
            same &= index.At(regs, key, 1) == MoFirst(regs, key);
        }
        same &= index.At(regs, "nowhere", 1) == -1 && index.At(regs, null, 1) == -1 && index.First(regs, null, 1) == null;
        Check(same, "index: the first of 3,000 registrations by key (repeated keys, none, empty places) is the walk's, every key");
        Check(index.Built == 1, "index: made once for all those askings (" + index.Built + ")");
        // The list changes: added to, taken from, its last replaced; the day turns.
        regs.Add(new MoReg { Key = "street new#1" });
        same = index.At(regs, "street new#1", 1) == regs.Count - 1;
        regs.RemoveAt(10);
        for (int k = 0; k < 2700; k += 7)
        {
            string key = "street " + k + "#" + (k % 40);
            same &= index.At(regs, key, 1) == MoFirst(regs, key);
        }
        regs[regs.Count - 1] = new MoReg { Key = "street other#2" };
        same &= index.At(regs, "street new#1", 1) == MoFirst(regs, "street new#1") && index.At(regs, "street other#2", 1) == regs.Count - 1;
        int built = index.Built;
        index.At(regs, "street 5#5", 2);
        same &= index.Built == built + 1;
        index.At(regs, "street 5#5", 2);
        same &= index.Built == built + 1;
        Check(same, "index: made again when its list is added to, taken from or its last item replaced, or its stamp moves; not otherwise");
        var other = new List<MoReg>(regs);
        same = index.At(other, "street 5#5", 2) == MoFirst(other, "street 5#5") && index.Built == built + 2;
        Check(same, "index: another list (another save) is indexed afresh");
        index.Reset();
        Check(index.At(regs, "street 9#9", 2) == MoFirst(regs, "street 9#9"), "index: reset, it is made again at the next asking");
    }

    private static void MoIndexCost()
    {
        var r = new Random(5);
        List<MoReg> small = MoRegs(1000, 1000, r), large = MoRegs(4000, 4000, r);
        MoLookAll(small, out _);
        MoLookAll(large, out _);
        MoLookAll(small, out double t1);
        MoLookAll(large, out double t4);
        Check(t4 < 12.0 * Math.Max(t1, 0.05) && t4 < 20.0, "index: every key of 4,000 registrations looked up takes about four times what 1,000 take, not sixteen ("
                                                           + t1.ToString("0.00") + " ms, " + t4.ToString("0.00") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int MoLookAll(List<MoReg> regs, out double ms)
    {
        var index = new ListIndex<MoReg, string>(MoKeyOf, StringComparer.Ordinal);
        var watch = Stopwatch.StartNew();
        int found = 0;
        for (int i = 0; i < regs.Count; i++)
        {
            if (regs[i] != null && index.At(regs, regs[i].Key, 0) >= 0)
            {
                found++;
            }
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return found;
    }

    // ---------------------------------------------------------------- sums by key (the city's day lines)

    // The city's day line as it was: the parts walked for every figure.
    private sealed class MoOldLine
    {
        public readonly List<MoAddr> Addresses = new List<MoAddr>();
        public readonly List<string> Categories = new List<string>();
        public readonly List<bool> Deductible = new List<bool>();
        public readonly List<float> Figures = new List<float>();

        public void Add(MoAddr address, string category, float figure, bool deductible)
        {
            for (int i = 0; i < Figures.Count; i++)
            {
                if ((ReferenceEquals(Addresses[i], address) || (!ReferenceEquals(Addresses[i], null) && !ReferenceEquals(address, null) && Addresses[i].Equals(address)))
                    && Categories[i] == category && Deductible[i] == deductible)
                {
                    Figures[i] += figure;
                    return;
                }
            }
            Addresses.Add(address);
            Categories.Add(category);
            Deductible.Add(deductible);
            Figures.Add(figure);
        }
    }

    // A late save's day: 3,000 wages (one an employee, at 300 businesses, a few with no business), 300 rents, 300 takings,
    // the day's shops and services by category alone. Each a new address object, as each of the game's transactions has.
    private static void MoDayLines(Random r, out List<MoLine> keys, out List<float> figures)
    {
        keys = new List<MoLine>(4000);
        figures = new List<float>(4000);
        string[] categories = { "ba:transactioncategory_salaryexpenses", "ba:transactioncategory_rent", null, "ba:transactioncategory_marketing", "ba:transactioncategory_parkingfees" };
        for (int i = 0; i < 3000; i++)
        {
            int shop = r.Next(300);
            keys.Add(new MoLine { Address = i % 113 == 0 ? null : new MoAddr("Street " + (shop % 37), shop), Category = categories[0], Deductible = shop % 3 == 0 });
            figures.Add((float)(r.NextDouble() * 180.0 + 20.0));
        }
        for (int i = 0; i < 600; i++)
        {
            int shop = r.Next(300);
            keys.Add(new MoLine { Address = new MoAddr("Street " + (shop % 37), shop), Category = categories[1 + i % 2], Deductible = i % 2 == 0 });
            figures.Add((float)(r.NextDouble() * 900.0));
        }
        for (int i = 0; i < 200; i++)
        {
            keys.Add(new MoLine { Address = null, Category = categories[3 + i % 2], Deductible = false });
            figures.Add((float)(r.NextDouble() * 50.0));
        }
    }

    private static void MoSumsSame()
    {
        var r = new Random(31);
        MoDayLines(r, out List<MoLine> keys, out List<float> figures);
        var old = new MoOldLine();
        var sums = new KeyedSums<MoLine>(new MoLineComparer());
        for (int i = 0; i < keys.Count; i++)
        {
            old.Add(keys[i].Address, keys[i].Category, figures[i], keys[i].Deductible);
            sums.Add(keys[i], figures[i]);
        }
        bool same = sums.Count == old.Figures.Count;
        for (int i = 0; same && i < sums.Count; i++)
        {
            MoLine k = sums.Keys[i];
            same = BitConverter.SingleToInt32Bits(sums.Sums[i]) == BitConverter.SingleToInt32Bits(old.Figures[i]) && k.Category == old.Categories[i] && k.Deductible == old.Deductible[i]
                   && (ReferenceEquals(k.Address, old.Addresses[i]) || (k.Address != null && k.Address.Equals(old.Addresses[i])));
        }
        Check(same && sums.Count > 300, "sums: a late save's day of rent, wages and takings (" + keys.Count + " lines) gives the same parts in the same order, each to the last bit, as the walk did (" + sums.Count + " parts)");
        int counted = 0;
        for (int i = 0; i < sums.Count; i++)
        {
            counted += sums.Counts[i];
        }
        Check(counted == keys.Count && sums.At(keys[0]) == 0 && sums.At(new MoLine { Address = new MoAddr("nowhere", 1) }) == -1, "sums: every line counted once, each found again by its key, none for a key never booked");
        sums.Clear();
        Check(sums.Count == 0 && sums.At(keys[0]) == -1, "sums: cleared, nothing is kept");
    }

    private static void MoSumsCost()
    {
        var r = new Random(4);
        MoDayLines(r, out List<MoLine> keys, out List<float> figures);
        MoSumAll(keys, figures, out _);
        MoOldAll(keys, figures, out _);
        MoSumAll(keys, figures, out double ours);
        MoOldAll(keys, figures, out double walk);
        Check(ours < 5.0 && ours * 4.0 < walk + 0.5, "sums: the day's lines by dictionary take " + ours.ToString("0.000") + " ms against the walk's " + walk.ToString("0.000") + " ms");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int MoSumAll(List<MoLine> keys, List<float> figures, out double ms)
    {
        var watch = Stopwatch.StartNew();
        var sums = new KeyedSums<MoLine>(new MoLineComparer());
        for (int i = 0; i < keys.Count; i++)
        {
            sums.Add(keys[i], figures[i]);
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return sums.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int MoOldAll(List<MoLine> keys, List<float> figures, out double ms)
    {
        var watch = Stopwatch.StartNew();
        var old = new MoOldLine();
        for (int i = 0; i < keys.Count; i++)
        {
            old.Add(keys[i].Address, keys[i].Category, figures[i], keys[i].Deductible);
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return old.Figures.Count;
    }

    // ---------------------------------------------------------------- a day's transactions

    private static int MoDayOf(MoTx t)
    {
        return t.Day;
    }

    private static readonly Func<MoTx, int> MoDayFunc = MoDayOf;

    private static List<MoTx> MoWalk(Queue<MoTx> queue, int day)
    {
        var list = new List<MoTx>();
        foreach (MoTx t in queue)
        {
            if (t != null && MoDayOf(t) == day)
            {
                list.Add(t);
            }
        }
        return list;
    }

    private static bool MoSameList(List<MoTx> a, List<MoTx> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    // A week of a late save's transactions: about 4,300 a day, mostly in order, some dated on the day before.
    private static Queue<MoTx> MoWeek(Random r, int firstDay, int days)
    {
        var queue = new Queue<MoTx>(32000);
        for (int d = firstDay; d < firstDay + days; d++)
        {
            for (int i = 0; i < 4300; i++)
            {
                queue.Enqueue(new MoTx { Day = i % 11 == 0 ? d - 1 : d, Amount = (float)r.NextDouble() });
            }
        }
        return queue;
    }

    private static void MoDaysSame()
    {
        var r = new Random(9);
        Queue<MoTx> queue = MoWeek(r, 10, 7);
        var cut = new DayCut<MoTx>(MoDayFunc);
        bool same = true;
        for (int day = 9; day <= 16; day++)
        {
            same &= MoSameList(cut.Of(queue, day, 3), MoWalk(queue, day));
        }
        Check(same && queue.Count > 30000, "days: each day's transactions of " + queue.Count + " are the walk's, the same ones in the same order");
        // The morning: the city books its lines, the features their costs, all dated yesterday, between the readers.
        int yesterday = 16;
        List<MoTx> before = cut.Of(queue, yesterday, 10);
        int had = before.Count;
        for (int i = 0; i < 900; i++)
        {
            queue.Enqueue(new MoTx { Day = i % 5 == 0 ? yesterday + 1 : yesterday });
        }
        same = MoSameList(cut.Of(queue, yesterday, 10), MoWalk(queue, yesterday)) && cut.Of(queue, yesterday, 10).Count == had + 720;
        same &= MoSameList(cut.Of(queue, yesterday + 1, 10), MoWalk(queue, yesterday + 1));
        same &= MoSameList(cut.Of(queue, yesterday, 10), MoWalk(queue, yesterday));
        Check(same, "days: what is booked after a reading is in the next (the city's lines, the features' costs), the two days kept apart");
        // The game drops old days from the head as it books (GameManager.ReduceTransactionQueue).
        for (int i = 0; i < 5000; i++)
        {
            queue.Dequeue();
            if (i % 2 == 0)
            {
                queue.Enqueue(new MoTx { Day = yesterday });
            }
        }
        same = MoSameList(cut.Of(queue, yesterday, 10), MoWalk(queue, yesterday)) && MoSameList(cut.Of(queue, 11, 12), MoWalk(queue, 11));
        Check(same, "days: with old days dropped from the head and more booked: still the walk's (a day that may have been dropped is walked afresh)");
        queue.Clear();
        same = cut.Of(queue, yesterday, 10).Count == 0;
        queue.Enqueue(new MoTx { Day = yesterday });
        same &= MoSameList(cut.Of(queue, yesterday, 10), MoWalk(queue, yesterday));
        Queue<MoTx> loaded = MoWeek(r, 20, 2);
        same &= MoSameList(cut.Of(loaded, 21, 15), MoWalk(loaded, 21)) && cut.Of(null, 21, 15).Count == 0;
        Check(same, "days: an emptied history, a new one (another save) and none at all are read afresh");
    }

    private static void MoDaysCost()
    {
        var r = new Random(2);
        Queue<MoTx> queue = MoWeek(r, 30, 7);
        int day = 36, size = queue.Count;
        var cut = new DayCut<MoTx>(MoDayFunc);
        // The morning's readers: the city's day figures, its books, the tech sector's, the economy's, the dealerships'.
        cut.Of(queue, day, 31);
        long walkedOnce = cut.Walked;
        int booked = 0;
        for (int reader = 0; reader < 4; reader++)
        {
            for (int i = 0; i < 300; i++)
            {
                queue.Enqueue(new MoTx { Day = day });
                booked++;
            }
            cut.Of(queue, day, 31);
        }
        long again = cut.Walked;
        cut.Of(queue, day, 31);
        Check(walkedOnce == size && cut.Opened == size + booked, "days: one full walk a morning; after it only the transactions booked since are opened (" + cut.Opened + " of " + (size + booked) + ")");
        Check(cut.Walked == again, "days: asked again with nothing booked, nothing is walked");
        MoReaders(queue, day, out _);
        MoReaders(queue, day, out double ms);
        Check(ms < 15.0, "days: five readers of a morning over " + queue.Count + " transactions take " + ms.ToString("0.00") + " ms in all");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int MoReaders(Queue<MoTx> queue, int day, out double ms)
    {
        var cut = new DayCut<MoTx>(MoDayFunc);
        var watch = Stopwatch.StartNew();
        int count = 0;
        for (int reader = 0; reader < 5; reader++)
        {
            List<MoTx> ofDay = cut.Of(queue, day, day - 5);
            float sum = 0f;
            for (int i = 0; i < ofDay.Count; i++)
            {
                sum += ofDay[i].Amount;
            }
            count += ofDay.Count + (sum > -1f ? 0 : 1);
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return count;
    }

    // ---------------------------------------------------------------- the rivals' runs of profit

    // The run for one business as it was: the days newest first, the first statement of the business each day.
    private static bool MoOldRun(List<List<MoStatement>> days, List<int> dayNumbers, MoAddr address, int today, int need, out float total)
    {
        total = 0f;
        int found = 0, from = today - need;
        for (int i = days.Count - 1; i >= 0; i--)
        {
            if (days[i] == null || dayNumbers[i] >= today)
            {
                continue;
            }
            if (dayNumbers[i] < from)
            {
                break;
            }
            for (int b = 0; b < days[i].Count; b++)
            {
                MoStatement s = days[i][b];
                if (s != null && s.Address != null && s.Address.Equals(address))
                {
                    if (s.Profit <= 0f)
                    {
                        return false;
                    }
                    total += s.Profit;
                    found++;
                    break;
                }
            }
        }
        return found >= need;
    }

    private static void MoRunsSame()
    {
        var r = new Random(41);
        const int need = 14, today = 40;
        var days = new List<List<MoStatement>>();
        var numbers = new List<int>();
        for (int d = 20; d < today + 1; d++)
        {
            var statements = new List<MoStatement>(310);
            for (int s = 0; s < 300; s++)
            {
                if (r.Next(40) == 0)
                {
                    continue;   // (a business with no statement that day)
                }
                statements.Add(new MoStatement { Address = new MoAddr("Street " + (s % 23), s), Profit = r.Next(30) == 0 ? -(float)r.NextDouble() * 50f : (float)(r.NextDouble() * 400.0) });
                if (s % 50 == 7)
                {
                    statements.Add(new MoStatement { Address = new MoAddr("Street " + (s % 23), s), Profit = -100f });   // (a second statement of it: only the first counts)
                }
            }
            statements.Add(null);
            statements.Add(new MoStatement { Address = null, Profit = 5f });
            days.Add(d == 22 ? null : statements);   // (a day the game has no statements of, before the run's window)
            numbers.Add(d);
            if (d == 33)
            {
                days.Add(null);   // (an empty place in the game's list, inside the window: passed over)
                numbers.Add(d);
            }
        }
        var runs = new ProfitRuns<MoAddr>();
        runs.Begin();
        int from = today - need;
        for (int i = days.Count - 1; i >= 0; i--)
        {
            if (days[i] == null || numbers[i] >= today)
            {
                continue;
            }
            if (numbers[i] < from)
            {
                break;
            }
            runs.Day();
            for (int b = 0; b < days[i].Count; b++)
            {
                if (days[i][b] != null && days[i][b].Address != null)
                {
                    runs.Add(days[i][b].Address, days[i][b].Profit);
                }
            }
        }
        bool same = true;
        int ran = 0;
        for (int s = 0; s < 300; s++)
        {
            var address = new MoAddr("Street " + (s % 23), s);
            bool a = MoOldRun(days, numbers, address, today, need, out float oldTotal);
            bool b = runs.Ran(address, need, out float total);
            same &= a == b && (!a || BitConverter.SingleToInt32Bits(oldTotal) == BitConverter.SingleToInt32Bits(total));
            ran += a ? 1 : 0;
        }
        same &= !runs.Ran(new MoAddr("nowhere", 0), need, out _) && !runs.Ran(null, need, out _);
        Check(same && ran > 30 && ran < 270, "runs: each of 300 businesses' run of profit over " + need + " days is the walk's, its total to the last bit (" + ran + " with a run, the rest a loss or a day without a statement)");
        runs.Begin();
        Check(!runs.Ran(new MoAddr("Street 1", 1), need, out _), "runs: begun afresh, nobody has a run");
    }

    // ---------------------------------------------------------------- the mod's sources as they ship

    // What the rules above stand on in the game-touching files: the morning's readers of the transactions share the one walk.
    private static void MoSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!System.IO.Directory.Exists(scripts))
        {
            Check(false, "sources: the mod's sources are at " + scripts);
            return;
        }
        string books = MoSource(scripts, @"Economy\GameBooks.cs");
        string city = MoSource(scripts, @"Economy\City\CityGame.cs");
        string ledger = MoSource(scripts, @"Economy\Finance\Books.cs");
        bool shared = books.Length > 0 && MoBody(books, "public static bool Refresh(").Contains("Day(day)") && !MoBody(books, "public static bool Refresh(").Contains("in save.Transactions")
                      && MoBody(books, "public static float Deduct(").Contains("Day(day)") && !MoBody(books, "public static float Deduct(").Contains("in save.Transactions")
                      && MoBody(city, "public static DayBooks DayFigures(").Contains("GameBooks.Day(day)") && !MoBody(city, "public static DayBooks DayFigures(").Contains("in save.Transactions")
                      && MoBody(ledger, "public static Obligations Tomorrow(CreditFile file, int today)").Contains("GameBooks.Day(") && !MoBody(ledger, "public static Obligations Tomorrow(CreditFile file, int today)").Contains("in save.Transactions");
        Check(shared, "sources: the city's day figures, each statement brought up to date, the dealerships' deduction and what leaves tomorrow read the day's transactions from the morning's one walk (GameBooks.Day)");
    }

    private static string MoSource(string scripts, string file)
    {
        string path = System.IO.Path.Combine(scripts, file);
        return System.IO.File.Exists(path) ? MoCode(System.IO.File.ReadAllText(path)) : string.Empty;
    }

    // The body of the member that starts with this text, braces matched; nothing if it is not there.
    private static string MoBody(string code, string start)
    {
        int at = code.IndexOf(start, StringComparison.Ordinal);
        int open = at >= 0 ? code.IndexOf('{', at) : -1;
        for (int i = open, depth = 0; open >= 0 && i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }

    // A C# file with its comments taken out (texts and characters kept, so that "//" in one is no comment).
    private static string MoCode(string text)
    {
        var code = new System.Text.StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    i++;
                }
                i += 2;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                bool verbatim = c == '"' && i > 0 && text[i - 1] == '@';
                code.Append(c);
                i++;
                while (i < text.Length && text[i] != c)
                {
                    if (text[i] == '\\' && !verbatim && i + 1 < text.Length)
                    {
                        code.Append(text[i]);
                        i++;
                    }
                    code.Append(text[i]);
                    i++;
                }
                if (i < text.Length)
                {
                    code.Append(text[i]);
                    i++;
                }
                continue;
            }
            code.Append(c);
            i++;
        }
        return code.ToString();
    }
}
