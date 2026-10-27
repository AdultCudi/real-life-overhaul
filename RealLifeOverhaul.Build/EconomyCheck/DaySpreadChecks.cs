using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// The game's midnight spread over frames (Scripts/Core/DaySpread.cs, Economy/Ventures/LookRound.cs, Core/DayPump.cs, Core/DayLines.cs).
// A player's log (27 Oct 2026) put the mod's scripts at 61 to 151 ms of the frame the game turns the day in. What the game does not read in that
// frame is queued and played afterwards, a slice a frame, in the order it was queued. The checks: a step queued in a frame is played in a later
// one; the steps run a slice at a time give the result of the same steps run at once; the save, the next day and standing down finish what is left;
// a city that goes drops its steps; a step that fails costs only itself; and the weekly look of the investors, made a business at a time with a flush
// halfway, leaves the file as it is left by the whole look in one go.
internal static partial class Program
{
    private static void DaySpreadAll()
    {
        DaySpreadFrames();
        DaySpreadSameResult();
        DaySpreadFlush();
        DaySpreadFaults();
        DaySpreadLook();
        DaySpreadTiming();
        DaySpreadSources();
    }

    // A job of items that adds to a total: an item is a step's piece of work. Done whole or a slice at a time, the total and the order are the same.
    private sealed class SpreadJob
    {
        public readonly int Items;
        public int At;
        public long Total;
        public readonly List<int> Order = new List<int>();

        public SpreadJob(int items)
        {
            Items = items;
        }

        // One call of the step: a few items (the slice), true when they are all done.
        public bool Step(int most)
        {
            for (int n = 0; n < most && At < Items; n++)
            {
                Total = Total * 31 + At + 7;
                Order.Add(At);
                At++;
            }
            return At >= Items;
        }

        public void Whole()
        {
            while (!Step(int.MaxValue))
            {
            }
        }
    }

    private static void DaySpreadFrames()
    {
        var queue = new DaySpread();
        int woken = 0;
        queue.Wake = () => woken++;
        var log = new List<string>();
        queue.Add("a", "first", () => { log.Add("first"); return true; }, 10);
        queue.Add("a", "second", () => { log.Add("second"); return true; }, 10);
        Check(woken == 1 && queue.Count == 2 && queue.Busy, "day spread: the first step queued on an empty queue wakes the player once, and two steps wait");
        Check(!queue.Due(10) && !queue.PlayOne(10) && log.Count == 0, "day spread: a step queued in a frame is not played in that frame");
        var slice = WorkSlice.Of(0.0, 1);
        Check(queue.Run(ref slice, 10) == 0 && log.Count == 0, "day spread: nor by a slice of that frame");
        Check(queue.Due(11) && queue.NextName == "first", "day spread: the next frame is its frame, and the first queued is first");
        slice = WorkSlice.Of(0.0, 1);
        Check(queue.Run(ref slice, 11) == 1 && log.Count == 1 && log[0] == "first" && queue.Count == 1, "day spread: a slice of no time plays one step, so that the queue is never stuck");
        slice = WorkSlice.Of(1000.0, 1);
        Check(queue.Run(ref slice, 12) == 1 && log.Count == 2 && log[1] == "second" && !queue.Busy, "day spread: a slice with room plays what is left, in order, and the queue is empty");
        queue.Add("a", "again", () => { log.Add("again"); return true; }, 20);
        Check(woken == 2, "day spread: an empty queue wakes the player again");

        // A step that adds a step: the new one waits for a later frame.
        var nested = new DaySpread();
        var seen = new List<string>();
        nested.Add("a", "outer", () => { seen.Add("outer"); nested.Add("a", "inner", () => { seen.Add("inner"); return true; }, 5); return true; }, 4);
        var all = WorkSlice.Of(1000.0, 1);
        nested.Run(ref all, 5);
        Check(seen.Count == 1 && seen[0] == "outer" && nested.Count == 1, "day spread: a step queued by a step is played in a later frame than the one that queued it");
        all = WorkSlice.Of(1000.0, 1);
        nested.Run(ref all, 6);
        Check(seen.Count == 2 && seen[1] == "inner" && !nested.Busy, "day spread: and then it is played");
    }

    private static void DaySpreadSameResult()
    {
        const int items = 137;
        var whole = new SpreadJob(items);
        whole.Whole();

        // A frame at a time, the steps' slices of 1, 3 and 40 items; two other jobs queued beside, in between.
        foreach (int per in new[] { 1, 3, 40 })
        {
            var queue = new DaySpread();
            var job = new SpreadJob(items);
            var beside = new SpreadJob(20);
            var after = new SpreadJob(20);
            queue.Add("a", "job", () => job.Step(per), 0);
            queue.Add("b", "beside", () => beside.Step(5), 0);
            queue.Add("a", "after", () => after.Step(5), 0);
            int frame = 1, calls = 0;
            while (queue.Busy && frame < 100000)
            {
                var slice = WorkSlice.Of(0.0, 1);
                calls += queue.Run(ref slice, frame++);
            }
            var beside2 = new SpreadJob(20);
            beside2.Whole();
            Check(job.Total == whole.Total && job.Order.Count == items && SameOrder(job.Order, whole.Order),
                "day spread: a job of " + items + " items played " + per + " at a time gives the total and the order of the same job done at once (" + calls + " calls)");
            Check(beside.Total == beside2.Total && after.At == 20 && beside.At == 20, "day spread: the jobs queued beside it are done whole, each in its own order");
            Check(job.At == items && calls >= (items + per - 1) / per, "day spread: a step that says it is not finished is called again, a slice at a time");
        }

        // The steps in the order queued, across owners.
        var q = new DaySpread();
        var order = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            int n = i;
            q.Add(i % 2 == 0 ? "even" : "odd", "s" + i, () => { order.Add("s" + n); return true; }, 0);
        }
        q.Flush();
        Check(string.Join(",", order) == "s0,s1,s2,s3,s4,s5", "day spread: steps of different owners are played in the order they were queued");
    }

    private static bool SameOrder(List<int> a, List<int> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void DaySpreadFlush()
    {
        // The save: a job half played, then the flush the save makes first; the file written holds the whole day.
        const int items = 90;
        var whole = new SpreadJob(items);
        whole.Whole();
        var queue = new DaySpread();
        var job = new SpreadJob(items);
        var other = new SpreadJob(10);
        queue.Add("director", "the job", () => job.Step(4), 0);
        queue.Add("another", "other", () => other.Step(3), 0);
        for (int frame = 1; frame <= 3; frame++)
        {
            var slice = WorkSlice.Of(0.0, 1);
            queue.Run(ref slice, frame);
        }
        Check(job.At > 0 && job.At < items && queue.Busy, "flush: three frames in, the job is part played and the queue is not empty");
        int played = queue.Flush();   // (the mod's SaveHook runs this first)
        Check(!queue.Busy && queue.Count == 0 && played > 0, "flush: before a save the queue is emptied");
        Check(job.Total == whole.Total && job.At == items && SameOrder(job.Order, whole.Order) && other.At == 10,
            "flush: the save holds the same result as the job done at once, every step of every owner finished");
        Check(queue.Flush() == 0, "flush: nothing to flush when nothing waits");

        // One owner's: the next day, the hour, a feature standing down. Another owner's steps stay for the frames.
        var q = new DaySpread();
        var mine = new SpreadJob(30);
        var theirs = new SpreadJob(30);
        var mineWhole = new SpreadJob(30);
        mineWhole.Whole();
        q.Add("mine", "mine", () => mine.Step(2), 0);
        q.Add("theirs", "theirs", () => theirs.Step(2), 0);
        q.Flush("mine");
        Check(mine.At == 30 && mine.Total == mineWhole.Total && theirs.At == 0 && q.Count == 1 && q.Has("theirs") && !q.Has("mine"),
            "flush: one owner's steps are finished at once and another's are left");
        Check(q.NextName == "theirs", "flush: the steps left are the other owner's");

        // Going with the city: dropped, not played, and nothing queued after is lost.
        var d = new DaySpread();
        var gone = new SpreadJob(10);
        var kept = new SpreadJob(10);
        d.Add("old city", "gone", () => gone.Step(1), 0);
        d.Add("new city", "kept", () => kept.Step(1), 0);
        int dropped = d.Drop("old city");
        d.Flush();
        Check(dropped == 1 && gone.At == 0 && kept.At == 10 && !d.Busy, "flush: a city that goes drops its steps, played none, and the new city's are played");

        // A step that never says it is finished does not stop a flush for ever.
        var stuck = new DaySpread();
        int calls = 0;
        stuck.Add("x", "stuck", () => { calls++; return false; }, 0);
        int made = stuck.Flush();
        Check(made == DaySpread.FlushMost && calls == DaySpread.FlushMost, "flush: a step that never finishes is called a bounded number of times, then the flush gives up");
    }

    private static void DaySpreadFaults()
    {
        var queue = new DaySpread();
        var names = new List<string>();
        queue.Faulted = (name, e) => names.Add(name + ":" + e.Message);
        var after = new SpreadJob(5);
        queue.Add("a", "breaks", () => throw new InvalidOperationException("boom"), 0);
        queue.Add("a", "after", () => after.Step(10), 0);
        var slice = WorkSlice.Of(1000.0, 1);
        queue.Run(ref slice, 1);
        Check(names.Count == 1 && names[0] == "breaks:boom" && after.At == 5 && !queue.Busy && queue.Faults == 1,
            "faults: a step that throws is named, left out for good, and the steps after it are played");
        var careless = new DaySpread();
        careless.Faulted = (name, e) => throw new InvalidOperationException("the report fails too");
        careless.Add("a", "breaks", () => throw new InvalidOperationException("boom"), 0);
        careless.Flush();
        Check(!careless.Busy, "faults: a fault in the report of a fault stops nothing");

        var once = new DaySpread();
        int ran = 0;
        once.Add("a", "once", () => { ran++; throw new InvalidOperationException("boom"); }, 0);
        once.Flush();
        once.Flush();
        Check(ran == 1, "faults: a step that failed is not tried again");
    }

    private sealed class LookBusiness
    {
        public string Place, Name;
        public BusinessFigures Figures;
    }

    // The weekly look of the investors: the businesses taken one after the other.
    private static List<LookBusiness> LookBusinesses(int count)
    {
        var list = new List<LookBusiness>(count);
        for (int i = 0; i < count; i++)
        {
            BusinessFigures b = Steady(i % 4 == 3 ? 900f : 2000f + 450f * i, i % 5 == 4 ? 8 : 14, 40 + i);
            b.Sector = (Sector)(i % 3);
            list.Add(new LookBusiness { Place = "street#" + (10 + i), Name = "Shop " + i, Figures = b });
        }
        return list;
    }

    private static string LookOutcome(VenturesFile file, List<LookBusiness> businesses, int from, int to, int day, List<string> said)
    {
        for (int i = from; i < to; i++)
        {
            LookBusiness b = businesses[i];
            LookResult r = LookRound.One(file, day, VenturesCatalogue.Standard, 0, b.Place, b.Name, b.Figures);
            said.Add(b.Place + " " + r.Kind + " " + (r.Investor != null ? r.Investor.Id : -1) + " " + r.Chance + " " + r.Roll + " " + (r.Offer != null ? r.Offer.Id + "/" + r.Offer.Amount + "/" + r.Offer.ShareBp : "-"));
        }
        return VenturesStore.Write(file);
    }

    // A save with a deal that asks for a second round at one place and an offer already out for another; with 'busy' every tycoon has an offer out already.
    private static VenturesFile LookFile(int seed, List<LookBusiness> businesses, bool busy)
    {
        VenturesFile file = NewVentures(seed);
        Investor first = file.Investors.Find(v => v.Kind == InvestorKind.Tycoon);
        file.Deals.Add(new Deal { Id = file.NextId++, InvestorId = first.Id, Place = businesses[1].Place, Business = businesses[1].Name, Amount = 40000, ShareBp = 2000, FollowOn = true, StartDay = 1 });
        file.Offers.Add(new Offer { Id = file.NextId++, InvestorId = first.Id, Place = businesses[2].Place, Business = businesses[2].Name, Amount = 40000, ShareBp = 2000, LapseDay = 30 });
        for (int i = 0; busy && i < file.Investors.Count; i++)
        {
            if (file.Investors[i].Kind == InvestorKind.Tycoon && file.Investors[i].Id != first.Id)
            {
                file.Offers.Add(new Offer { Id = file.NextId++, InvestorId = file.Investors[i].Id, Place = "elsewhere#" + i, Business = "Elsewhere", Amount = 40000, ShareBp = 2000, LapseDay = 30 });
            }
        }
        return file;
    }

    private static void DaySpreadLook()
    {
        List<LookBusiness> businesses = LookBusinesses(14);
        bool same = true, offers = false, sameSaid = true;
        int kinds = 0;
        foreach (int seed in new[] { 31337, 4242, 90210, 77, 1234 })
        {
            bool busy = seed == 77;   // (every tycoon has an offer out: nobody is free to come)
            // the whole look in one go
            VenturesFile whole = LookFile(seed, businesses, busy);
            var saidWhole = new List<string>();
            string wholeText = LookOutcome(whole, businesses, 0, businesses.Count, 21, saidWhole);

            // a business at a time, as the pump plays it, with the save's flush (the rest at once) after the sixth
            VenturesFile spread = LookFile(seed, businesses, busy);
            var saidSpread = new List<string>();
            var queue = new DaySpread();
            int at = 0;
            queue.Add("ventures", "the weekly look", () =>
            {
                if (at < businesses.Count)
                {
                    LookOutcome(spread, businesses, at, at + 1, 21, saidSpread);
                    at++;
                }
                return at >= businesses.Count;
            }, 0);
            for (int frame = 1; frame <= 6; frame++)
            {
                var slice = WorkSlice.Of(0.0, 1);
                queue.Run(ref slice, frame);
            }
            bool midway = at == 6 && queue.Busy;
            queue.Flush("ventures");
            string spreadText = VenturesStore.Write(spread);
            same &= midway && spreadText == wholeText && !queue.Busy;
            sameSaid &= saidWhole.Count == saidSpread.Count;
            for (int i = 0; i < saidWhole.Count && i < saidSpread.Count; i++)
            {
                sameSaid &= saidWhole[i] == saidSpread[i];
            }
            offers |= whole.Offers.Count > 2;
            foreach (string line in saidWhole)
            {
                kinds |= 1 << (int)Enum.Parse(typeof(LookKind), line.Split(' ')[1]);
            }
        }
        Check(same, "weekly look: made a business a frame with the rest flushed halfway, the file is the file the whole look in one go leaves, on five saves");
        Check(sameSaid, "weekly look: and each business's result, its investor, chance, roll and offer, is the same");
        Check(offers && kinds == 15, "weekly look: the saves cover a second round, a business not asked, no tycoon free and a roll (" + kinds + ")");
        VenturesFile again = LookFile(31337, businesses, false);
        var a = new List<string>();
        var b2 = new List<string>();
        string first = LookOutcome(again, businesses, 0, businesses.Count, 21, a);
        string second = LookOutcome(LookFile(31337, businesses, false), businesses, 0, businesses.Count, 21, b2);
        Check(first == second && string.Join("|", a) == string.Join("|", b2), "weekly look: the same save and day give the same look on every machine");
    }

    private static void DaySpreadTiming()
    {
        // A thousand queued steps are one list entry each: queued and played in a few milliseconds, and the count is not a walk of the list.
        var queue = new DaySpread();
        long total = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            int n = i;
            queue.Add("o" + (i % 7), "s", () => { total += n; return true; }, 0);
        }
        int busy = queue.Count;
        queue.Flush();
        double ms = clock.Elapsed.TotalMilliseconds;
        Check(busy == 1000 && total == 499500 && !queue.Busy && ms < 25.0, "timing: a thousand steps queued and played in " + ms.ToString("0.0") + " ms (under 25)");
    }

    private static void DaySpreadSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts"));
        string save = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Core\SaveHook.cs"));
        string pump = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Core\DayPump.cs"));
        string start = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Core\RealLifeOverhaulMod.cs"));
        string ventures = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Economy\Ventures\VenturesDirector.cs"))
                          + System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Economy\Ventures\VenturesDirector.Day.cs"));
        string careers = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Careers\CareersDirector.cs"))
                         + System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Careers\CareersDirector.Vacancies.cs"))
                         + System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, @"Careers\CareersDirector.Work.cs"));
        Check(save.Contains("DaySpread.Shared.Flush()") && save.Contains("DayLines.Flush()") && save.IndexOf("FinishDay();", StringComparison.Ordinal) < save.IndexOf("Handlers[i]();", StringComparison.Ordinal),
            "day spread: the save plays what the midnight left, and writes the lines it held, before any feature writes its state");
        Check(pump.Contains("DaySpread.Shared.Wake = Wake") && pump.Contains("DayLines.Wake = Wake") && pump.Contains("OnApplicationQuit") && start.Contains("Guard.Add<DayPump>(_root)"),
            "day spread: the pump is started with the mod, woken by the first step or line, and writes the held lines when the game ends");
        Check(ventures.Contains("DaySpread.Shared.Drop(this)") && ventures.Contains("FinishDay();") && ventures.Contains("LookBegin(day, level)") && ventures.Contains("DaySpread.Shared.Flush(this)"),
            "day spread: Ventures finishes its look before the hour, the next day, the save and standing down, and drops it with its city");
        Check(careers.Contains("DaySpread.Shared.Drop(this)") && careers.Contains("DaySpread.Shared.Flush(this)") && careers.Contains("VacanciesLater(day)"),
            "day spread: Careers finishes its board before the hour, the next day, the save and the app's opening, and drops it with its city");
    }
}
