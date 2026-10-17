using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// The frame a game day starts in, and the economy's hour: the customers of the player's businesses drawn again once a
// business, spread over the frames that follow within the hour they were asked in (Core/RedrawRules), and the economy's
// look-ups of a morning made by dictionaries and one walk of the day's transactions (Core/LookupRules). Each index
// gives exactly what the walk it replaces gave, on late-game figures: 300 businesses, 3,000 employees, 30,000
// transactions.
internal static partial class Program
{
    private static void Morning()
    {
        MoRedrawOnce();
        MoRedrawUnchanged();
        MoRedrawAlways();
        MoRedrawReplaced();
        MoRedrawAdded();
        MoRedrawSame();
        MoRedrawCost();
        MoRedrawRules();
        MoRedrawMornings();
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

    private sealed class MoShop
    {
        public string Name;
    }

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

    private static List<MoShop> MoShops(int count)
    {
        var shops = new List<MoShop>(count);
        for (int i = 0; i < count; i++)
        {
            shops.Add(new MoShop { Name = "shop " + i });
        }
        return shops;
    }

    // ---------------------------------------------------------------- the redraws: one a business, the last figure

    private static void MoRedrawOnce()
    {
        var q = new RedrawQueue<MoShop>();
        List<MoShop> shops = MoShops(300);
        var lists = new object[300];
        for (int i = 0; i < 300; i++)
        {
            lists[i] = new object();
        }
        // The morning: the city, the company, the brands and the interiors each write every business's figure.
        for (int f = 0; f < 4; f++)
        {
            for (int i = 0; i < 300; i++)
            {
                q.Ask(shops[i], 40 + f + i % 7, false, 240, DrawPrint.Of(lists[i], 10, null));
            }
        }
        Check(q.Waiting == 300, "redraws: four features asking for 300 businesses leave 300 waiting, one each (" + q.Waiting + ")");
        int n = 0;
        bool order = true, figures = true, hours = true;
        while (q.Next(out MoShop shop, out int figure, out int hour))
        {
            order &= ReferenceEquals(shop, shops[n]);
            figures &= figure == 43 + n % 7;
            hours &= hour == 240;
            n++;
        }
        Check(n == 300 && order, "redraws: each business is drawn once, in the order it was first asked for");
        Check(figures, "redraws: each is drawn with the figure of its last asking, as the last of the redraws made at once had it");
        Check(hours, "redraws: each keeps the game hour it was first asked in");
        Check(q.Waiting == 0 && !q.Next(out _, out _, out _), "redraws: nothing waits once all are drawn");
        Check(!q.Ask(null, 1, true, 0, default(DrawPrint)) && q.Waiting == 0, "redraws: no business, nothing asked");
    }

    private static void MoRedrawUnchanged()
    {
        var q = new RedrawQueue<MoShop>();
        var shop = new MoShop { Name = "a" };
        object list = new object(), last = new object();
        DrawPrint drawn = DrawPrint.Of(list, 12, last);
        q.Drew(shop, 50, drawn);
        Check(!q.Ask(shop, 50, false, 0, drawn) && q.Waiting == 0, "redraws: a business whose list stands drawn with the figure it has now is not drawn again");
        Check(q.Stands(shop, 50, drawn) && !q.Stands(shop, 51, drawn), "redraws: the list stands drawn with that figure, and no other");
        Check(q.Ask(shop, 51, false, 0, drawn) && q.Waiting == 1, "redraws: another figure is drawn");
        Check(!q.Ask(shop, 50, false, 0, drawn) && q.Waiting == 0, "redraws: written back to the figure it was drawn with before its turn, its wish is dropped (that redraw would have been undone)");
        Check(q.Ask(shop, 50, false, 0, DrawPrint.Of(list, 13, last)) && q.Take(shop, out _, out _), "redraws: a list somebody else has added to or drawn (another length) is drawn");
        Check(q.Ask(shop, 50, false, 0, DrawPrint.Of(list, 12, new object())) && q.Take(shop, out _, out _), "redraws: a list with another last customer (the game drew it again) is drawn");
        Check(q.Ask(shop, 50, false, 0, DrawPrint.Of(new object(), 12, last)) && q.Take(shop, out _, out _), "redraws: another list (the game's new day) is drawn");
        Check(q.Ask(shop, 50, false, 0, default(DrawPrint)) && q.Take(shop, out _, out _), "redraws: a business with no list is drawn whenever asked");
        q.Forget(shop);
        Check(q.Ask(shop, 50, false, 0, drawn) && q.Take(shop, out _, out _), "redraws: a business whose draw is forgotten is drawn");
        q.Drew(shop, 50, drawn);
        q.Clear();
        Check(!q.Stands(shop, 50, drawn) && q.Waiting == 0, "redraws: cleared, nothing waits and nothing is known");
    }

    private static void MoRedrawAlways()
    {
        var q = new RedrawQueue<MoShop>();
        var shop = new MoShop { Name = "a" };
        DrawPrint drawn = DrawPrint.Of(new object(), 5, new object());
        q.Drew(shop, 50, drawn);
        Check(q.Ask(shop, 50, true, 0, drawn) && q.Waiting == 1, "redraws: opening hours changed (always) are drawn even with the same figure");
        Check(q.Ask(shop, 50, false, 0, drawn) && q.Waiting == 1, "redraws: a footfall asking with the drawn figure leaves that wish waiting");
        q.Ask(shop, 47, false, 0, drawn);
        Check(q.Next(out _, out int figure, out _) && figure == 47, "redraws: ...and it is drawn with the latest figure");
    }

    private static void MoRedrawReplaced()
    {
        var q = new RedrawQueue<MoShop>();
        var shop = new MoShop { Name = "a" };
        object before = new object(), after = new object();
        q.Ask(shop, 50, false, 0, DrawPrint.Of(before, 3, null));
        Check(!q.Replaced(shop, before) && q.Waiting == 1, "redraws: a wish asked against the list there now waits on");
        Check(q.Replaced(shop, after) && q.Waiting == 0, "redraws: a wish asked against a list the game has since made anew (its day's draw) is dropped, as the game's own draw wiped the redraw made then");
        Check(!q.Replaced(shop, after), "redraws: nothing waits, nothing is dropped");
    }

    private static void MoRedrawAdded()
    {
        var q = new RedrawQueue<MoShop>();
        var shop = new MoShop { Name = "a" };
        object list = new object();
        DrawPrint drawn = DrawPrint.Of(list, 10, new object()), grown = DrawPrint.Of(list, 12, new object());
        q.Drew(shop, 50, drawn);
        q.Added(shop, drawn, grown);
        Check(q.Stands(shop, 50, grown) && !q.Stands(shop, 50, drawn), "redraws: the mod's own clients added to a list as last drawn: it stands drawn with that figure still");
        DrawPrint other = DrawPrint.Of(list, 14, new object());
        q.Added(shop, DrawPrint.Of(list, 11, new object()), other);
        Check(!q.Stands(shop, 50, other), "redraws: added to a list that did not stand as last drawn: nothing is known of it");
    }

    private static void MoRedrawSame()
    {
        var a = new RedrawQueue<MoShop>();
        var b = new RedrawQueue<MoShop>();
        List<MoShop> shops = MoShops(120);
        var lists = new object[120];
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = new object();
        }
        var ra = new Random(77);
        var rb = new Random(77);
        var outA = new List<int>(512);
        var outB = new List<int>(512);
        for (int step = 0; step < 4000; step++)
        {
            MoRedrawStep(a, shops, lists, ra, outA);
            MoRedrawStep(b, shops, lists, rb, outB);
        }
        bool same = outA.Count == outB.Count;
        for (int i = 0; same && i < outA.Count; i++)
        {
            same = outA[i] == outB[i];
        }
        Check(same && outA.Count > 100, "redraws: the same askings give the same draws in the same order, every time (" + outA.Count + " draws): nothing of the machine or the frame");
    }

    private static void MoRedrawStep(RedrawQueue<MoShop> q, List<MoShop> shops, object[] lists, Random r, List<int> drawn)
    {
        int i = r.Next(shops.Count), what = r.Next(10);
        if (what < 6)
        {
            q.Ask(shops[i], 40 + r.Next(4), what == 0, 0, DrawPrint.Of(lists[i], 1, null));
        }
        else if (what < 8 && q.Next(out MoShop shop, out int figure, out _))
        {
            int at = shops.IndexOf(shop);
            q.Drew(shop, figure, DrawPrint.Of(lists[at], 1, null));
            drawn.Add(at * 100 + figure);
        }
        else if (what == 8 && q.Take(shops[i], out int taken, out _))
        {
            drawn.Add(-(i * 100 + taken));
        }
    }

    private static void MoRedrawCost()
    {
        var q = new RedrawQueue<MoShop>();
        List<MoShop> shops = MoShops(300);
        var lists = new object[300];
        for (int i = 0; i < 300; i++)
        {
            lists[i] = new object();
        }
        MoRedrawRounds(q, shops, lists, 3, out _);   // (the pool and the lists grow to a morning's size first)
        long bytes = MoRedrawRounds(q, shops, lists, 50, out double ms);
        Check(bytes == 0, "redraws: fifty mornings of 300 businesses asked for four times and drawn allocate nothing once grown (" + bytes + " bytes)");
        Check(ms / 50 < 2.0, "redraws: a morning's 1,200 askings and 300 draws of the rules take well under a millisecond or two (" + (ms / 50).ToString("0.000") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long MoRedrawRounds(RedrawQueue<MoShop> q, List<MoShop> shops, object[] lists, int rounds, out double ms)
    {
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int r = 0; r < rounds; r++)
        {
            for (int f = 0; f < 4; f++)
            {
                for (int i = 0; i < shops.Count; i++)
                {
                    q.Ask(shops[i], 40 + f, false, r, DrawPrint.Of(lists[i], r, null));
                }
            }
            while (q.Next(out MoShop shop, out int figure, out _))
            {
                q.Drew(shop, figure, DrawPrint.Of(shop, r, null));
            }
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // ---------------------------------------------------------------- when they are made

    private static void MoRedrawRules()
    {
        float step = RedrawRules.StepMost(1f / 3f, 1f, 1f);
        Check(Math.Abs(step - 1f / 3f) < 1e-5f, "redraws: at the game's own speed one frame moves the clock a third of a game minute at most (the engine's largest step)");
        Check(Math.Abs(RedrawRules.StepMost(1f / 3f, 0f, 1f) - 1f / 3f) < 1e-5f, "redraws: paused, the step is counted as at the normal speed (never less)");
        Check(RedrawRules.StepMost(1f / 3f, 6f, 1f) > 1.9f, "redraws: fast forward moves the clock six times as far a frame");
        Check(RedrawRules.Spread(true, false, 1f, step, 0f), "redraws: alone, at the normal speed, early in the hour: spread over frames");
        Check(RedrawRules.Spread(true, false, 0f, step, 30f), "redraws: paused: spread (the clock does not move)");
        Check(!RedrawRules.Spread(false, false, 1f, step, 0f), "redraws: in a Going Public session: at once (its clock can move on by an hour in a frame)");
        Check(!RedrawRules.Spread(true, true, 1f, step, 0f), "redraws: in the game's time skip (sleep, the time machine): at once");
        Check(!RedrawRules.Spread(true, false, 6f, RedrawRules.StepMost(1f / 3f, 6f, 1f), 0f), "redraws: at fast forward: at once");
        Check(!RedrawRules.Spread(true, false, 1f, step, 50f) && RedrawRules.Spread(true, false, 1f, step, 49.9f), "redraws: from the hour's last " + RedrawRules.SettleMinutes + " game minutes: at once");
        Check(!RedrawRules.Spread(true, false, 1f, RedrawRules.StepMost(1f / 3f, 1f, 12f), 0f), "redraws: a clock set to run twelve times as fast: at once (a frame could cross the margin)");
        Check(RedrawRules.SettleMinutes > RedrawRules.StepsKept * step * 3f, "redraws: the margin holds many of the game's largest steps of a frame");
        Check(RedrawRules.More(0, 50.0) && RedrawRules.More(1, 0.5) && !RedrawRules.More(1, RedrawRules.BudgetMs) && !RedrawRules.More(7, 3.0), "redraws: a frame takes one at least, then more while under " + RedrawRules.BudgetMs + " ms");
        Check(!RedrawRules.Late(100, 100) && RedrawRules.Late(100, 101), "redraws: one made in a later game hour than asked is late");
        Check(Math.Abs(RedrawRules.Seen(100.0, 100.5, 0.5f, 0.5f) - 1f) < 1e-4f && RedrawRules.Seen(100.0, 99.0, 0.5f, 0.5f) == 0f && RedrawRules.Seen(100.0, 170.0, 0.5f, 0.5f) == 0f,
            "redraws: the clock's speed as seen between frames (a jump of an hour or a clock set back is no speed)");
        Check(Math.Abs(RedrawRules.Seen(0.0, 0.05, 0.05f, 0.005f) - 1f) < 1e-3f, "redraws: a long frame before a short one is not read as a fast clock");
    }

    private struct MoDay
    {
        public int Frames, AtOnce, Late;
        public float Ended, SettledAt;
        public double Worst;
    }

    // A morning (or any hour) of redraws: so many asked for at a minute, each costing so many milliseconds, at so many
    // frames a second and a clock of so many game minutes a second. Each frame the pace's rules decide; the game's own
    // step of the clock comes after the pace's frame (the later of the two orders a frame can have).
    private static MoDay MoPace(int count, double cost, float fps, float minutesASecond, float timeScale, float askedAt, bool alone)
    {
        var day = new MoDay { SettledAt = -1f };
        float minute = askedAt, dt = 1f / fps;
        int waiting = count;
        float step = RedrawRules.StepMost(1f / 3f, timeScale, minutesASecond);
        while (waiting > 0 && day.Frames < 1000000)
        {
            day.Frames++;
            if (!RedrawRules.Spread(alone, false, timeScale, step, minute))
            {
                day.AtOnce = waiting;
                day.SettledAt = minute;
                waiting = 0;
                break;
            }
            double used = 0.0;
            int done = 0;
            while (waiting > 0 && RedrawRules.More(done, used))
            {
                used += cost;
                done++;
                waiting--;
            }
            day.Worst = Math.Max(day.Worst, used);
            minute += dt * Math.Max(1f, timeScale) * minutesASecond;
            if (minute >= RedrawRules.MinutesInHour)
            {
                day.Late = waiting;
                break;
            }
        }
        day.Ended = minute;
        return day;
    }

    private static void MoRedrawMornings()
    {
        MoDay d = MoPace(300, 0.3, 60f, 1f, 1f, 0f, true);
        Check(d.Late == 0 && d.AtOnce == 0 && d.Ended < 5f, "redraws: a late save's morning, 300 redraws of 0.3 ms at 60 frames a second, is spread and done in "
                                                         + d.Frames + " frames, by game minute " + d.Ended.ToString("0.0"));
        Check(d.Worst <= RedrawRules.BudgetMs + 0.3 + 1e-9, "redraws: no frame takes more than the budget and one redraw (" + d.Worst.ToString("0.00") + " ms)");
        d = MoPace(300, 1.5, 20f, 1f, 1f, 0f, true);
        Check(d.Late == 0 && d.AtOnce == 0 && d.Worst <= 1.5 + 1e-9 && d.Ended < RedrawRules.MinutesInHour - RedrawRules.SettleMinutes,
            "redraws: a slow machine (20 frames a second, 1.5 ms a redraw) draws one a frame and still ends by minute " + d.Ended.ToString("0.0"));
        d = MoPace(300, 2.0, 30f, 1f, 1f, 48f, true);
        Check(d.Late == 0 && d.AtOnce > 0 && d.SettledAt >= RedrawRules.MinutesInHour - RedrawRules.SettleMinutes && d.SettledAt < RedrawRules.MinutesInHour,
            "redraws: asked near the hour's end, those left are drawn at once at minute " + d.SettledAt.ToString("0.0") + ", before the hour turns (" + d.AtOnce + " of 300)");
        d = MoPace(300, 0.3, 60f, 1f, 6f, 0f, true);
        Check(d.Late == 0 && d.AtOnce == 300 && d.Frames == 1, "redraws: at fast forward every redraw is made at once, as it was asked");
        d = MoPace(300, 0.3, 60f, 1f, 1f, 0f, false);
        Check(d.Late == 0 && d.AtOnce == 300, "redraws: in a Going Public session every redraw is made at once");
        bool everyMinute = true;
        for (float at = 0f; at < 60f; at += 0.25f)
        {
            MoDay any = MoPace(300, 0.8, 30f, 1f, 1f, at, true);
            everyMinute &= any.Late == 0;
        }
        Check(everyMinute, "redraws: asked at any minute of an hour, none is ever made after the hour turns");
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

    // What the rules above stand on in the game-touching files: every redraw of a business's customers goes through
    // Core/CustomerDraw, every batch of writes is closed whatever happens, the morning's readers of the transactions share
    // the one walk, the footfall writer builds no line on every write, and the game's draw is noted before any write.
    private static void MoSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!System.IO.Directory.Exists(scripts))
        {
            Check(false, "sources: the mod's sources are at " + scripts);
            return;
        }
        int files = 0, holds = 0;
        var drawsElsewhere = new List<string>();
        var unclosed = new List<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = MoCode(System.IO.File.ReadAllText(path));
            string name = path.Substring(scripts.Length + 1);
            files++;
            if (code.Contains("CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness") && !name.EndsWith(@"Core\CustomerDraw.cs", StringComparison.Ordinal))
            {
                drawsElsewhere.Add(name);
            }
            int hold = MoCount(code, "CustomerDraw.Hold(");
            int closed = System.Text.RegularExpressions.Regex.Matches(code, @"finally\s*\{\s*CustomerDraw\.Release\(\);\s*\}").Count;
            holds += hold;
            if (hold != closed || MoCount(code, "CustomerDraw.Release()") != closed)
            {
                unclosed.Add(name + " (" + hold + " held, " + closed + " released in a finally)");
            }
        }
        Check(files > 500 && drawsElsewhere.Count == 0, "sources: the game's redraw of a business's customers is called in Core/CustomerDraw alone" + (drawsElsewhere.Count > 0 ? " (also in " + string.Join(", ", drawsElsewhere) + ")" : string.Empty));
        Check(holds >= 11 && unclosed.Count == 0, "sources: every batch of footfall writes (" + holds + ") is released in a finally" + (unclosed.Count > 0 ? " (not in " + string.Join(", ", unclosed) + ")" : string.Empty));

        string books = MoSource(scripts, @"Economy\GameBooks.cs");
        string city = MoSource(scripts, @"Economy\City\CityGame.cs");
        string ledger = MoSource(scripts, @"Economy\Finance\Books.cs");
        bool shared = books.Length > 0 && MoBody(books, "public static bool Refresh(").Contains("Day(day)") && !MoBody(books, "public static bool Refresh(").Contains("in save.Transactions")
                      && MoBody(books, "public static float Deduct(").Contains("Day(day)") && !MoBody(books, "public static float Deduct(").Contains("in save.Transactions")
                      && MoBody(city, "public static DayBooks DayFigures(").Contains("GameBooks.Day(day)") && !MoBody(city, "public static DayBooks DayFigures(").Contains("in save.Transactions")
                      && MoBody(ledger, "public static Obligations Tomorrow(CreditFile file, int today)").Contains("GameBooks.Day(") && !MoBody(ledger, "public static Obligations Tomorrow(CreditFile file, int today)").Contains("in save.Transactions");
        Check(shared, "sources: the city's day figures, each statement brought up to date, the dealerships' deduction and what leaves tomorrow read the day's transactions from the morning's one walk (GameBooks.Day)");

        string business = MoSource(scripts, @"Business\BusinessGame.cs");
        string footfall = MoBody(business, "public static bool Footfall(BuildingRegistration reg, int percent, bool regenerate)");
        Check(footfall.Length > 0 && !footfall.Contains("Logger?.Info") && footfall.Contains("CustomerDraw.Rewritten(reg)") && footfall.Contains("CustomerDraw.Writing(reg)"),
            "sources: the footfall writer builds no line on a write, asks for the customers through Core/CustomerDraw and says when it writes");

        string economy = MoSource(scripts, @"Economy\EconomyDirector.cs");
        string day = MoBody(economy, "private void OnDay()");
        int morning = day.IndexOf("CustomerDraw.Morning()", StringComparison.Ordinal), after = day.IndexOf("Guard.Each(AfterDay)", StringComparison.Ordinal);
        Check(morning >= 0 && after > morning, "sources: the economy's day notes the game's own draw before any feature writes a figure");

        string draw = MoSource(scripts, @"Core\CustomerDraw.cs");
        Check(MoBody(draw, "public static List<CustomerEntry> Entries(").Contains("DrawNow(reg)") && MoBody(draw, "internal static bool Pace()").Contains("BusinessSimulatorHelper.Work.HasPendingWork")
              && MoBody(draw, "public static void Keep()").Contains("onTimeMachineStarted") && MoBody(draw, "public static void Keep()").Contains("SaveHook.Keep("),
            "sources: a list read is drawn first, the pace waits for the game's own simulation of the hour, and the game's save and time skip draw what waits");
    }

    private static int MoCount(string code, string what)
    {
        int count = 0;
        for (int at = code.IndexOf(what, StringComparison.Ordinal); at >= 0; at = code.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
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
