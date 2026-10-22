using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for the first second after a save loads (Core/StartRules.cs, compiled as it ships, and the mod's sources): the
// directors' first loads one a frame instead of all in one, in the line's order whatever order the engine runs their
// timers in within a frame, the economy's followers in the frames after the credit side, and the order the same as the
// one they ran in before (the order they are made in, with the economy's followers inside its second), so that each still
// finds what it reads of another there. And the session record's word for the game's screen in a long frame
// (Diagnostics/FrameRules.cs): the loading screen, still fading out after the save has loaded, counts as hidden, as the
// game's black screen does. A session's log had a frame of 1381 ms as the loading screen went, with some
// 205 ms of the mod's coroutines in it, said to be "clear".
//
// What is not here: how long each first load takes in the game, which the long-frame lines of Player.log now name
// ("Economy: EstateDirector.Second", "Tech: TechDirector.Tick"), and the "Start line check (...)" line.
internal static partial class Program
{
    private static void Starts()
    {
        StartSeatTable();
        StartAllAtOnce();
        StartAnyOrder();
        StartCreditLate();
        StartEdges();
        StartCost();
        StartSources();
        Screens();
    }

    // The seats: each a number of its own from 0, a name each, and every seat read at a start placed before the seat that reads it.
    private static void StartSeatTable()
    {
        var seen = new bool[StartSeats.Count];
        bool distinct = true;
        int fields = 0;
        foreach (FieldInfo f in typeof(StartSeats).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.IsLiteral || f.Name == "Count")
            {
                continue;
            }
            int v = (int)f.GetRawConstantValue();
            distinct &= v >= 0 && v < StartSeats.Count && !seen[v];
            if (v >= 0 && v < StartSeats.Count)
            {
                seen[v] = true;
            }
            fields++;
        }
        Check(distinct && fields == StartSeats.Count, "start line: " + fields + " seats, each its own number from 0 to " + (StartSeats.Count - 1));
        Check(StartSeats.Names.Length == StartSeats.Count && StartSeats.After.Length == StartSeats.Count, "start line: every seat has a name and a list of what it reads");
        bool kept = true;
        string broken = null;
        for (int s = 0; s < StartSeats.Count; s++)
        {
            foreach (int before in StartSeats.After[s])
            {
                if (before >= s)
                {
                    kept = false;
                    broken = StartSeats.Names[s] + " reads " + StartSeats.Names[before];
                }
            }
        }
        Check(kept, "start line: every director starts after each one whose state it reads at its start" + (broken != null ? " (not: " + broken + ")" : string.Empty));
    }

    // One director's timer: its first tick waits until it is given its seat.
    private sealed class OwnTimer
    {
        public int Seat;
        public bool Done;
    }

    // The credit side's timer: its own seat, then each follower's in turn, the next asked for in the frame the last was given.
    private sealed class CreditTimer
    {
        public int Next;          // the seat it asks for; past the last follower, done
        public int RunningFrom;   // the frame from which the credit side runs (its followers start only then)
    }

    // Runs the twenty seats' first ticks, all due in frame 'due', the engine running the timers in each frame in the order
    // 'shuffle' gives; the frames each seat was given in.
    private static int[] RunLine(int due, Random shuffle, int creditRunsFrom = -1)
    {
        var queue = new StartQueue(StartSeats.Count);
        var timers = new List<object>();
        var credit = new CreditTimer { Next = StartSeats.Credit, RunningFrom = creditRunsFrom };
        timers.Add(credit);
        for (int s = StartSeats.Ventures; s < StartSeats.Count; s++)
        {
            timers.Add(new OwnTimer { Seat = s });
        }
        for (int frame = due; frame < due + 200; frame++)
        {
            if (shuffle != null)
            {
                for (int i = timers.Count - 1; i > 0; i--)
                {
                    int j = shuffle.Next(i + 1);
                    (timers[i], timers[j]) = (timers[j], timers[i]);
                }
            }
            foreach (object t in timers)
            {
                if (t is OwnTimer own)
                {
                    if (!own.Done && !queue.Holds(own.Seat, frame))
                    {
                        own.Done = true;
                    }
                    continue;
                }
                // The credit side: its seat, then each follower while it runs (EconomyDirector.Tick).
                while (credit.Next <= StartSeats.Brands)
                {
                    if (credit.Next > StartSeats.Credit && frame < credit.RunningFrom)
                    {
                        break;   // not running yet: the followers do nothing, and ask for no seat
                    }
                    if (queue.Holds(credit.Next, frame))
                    {
                        break;   // yield return null
                    }
                    credit.Next++;
                }
            }
        }
        var given = new int[StartSeats.Count];
        for (int s = 0; s < StartSeats.Count; s++)
        {
            given[s] = queue.Given(s);
        }
        return given;
    }

    private static bool OnePerFrame(int[] given)
    {
        var frames = new HashSet<int>();
        foreach (int g in given)
        {
            if (g == int.MinValue || !frames.Add(g))
            {
                return false;
            }
        }
        return true;
    }

    // Every first tick due in one frame (as after a load): one a frame, in the line's order, none in the frame it is due.
    private static void StartAllAtOnce()
    {
        int[] given = RunLine(100, null);
        bool order = true;
        for (int s = 0; s < StartSeats.Count; s++)
        {
            order &= given[s] == 101 + s;
        }
        Check(OnePerFrame(given), "start line: twenty first loads due in one frame start in twenty frames, one a frame");
        Check(order, "start line: they start in the line's order, the credit side and its nine followers first, the first a frame after they are due (" + string.Join(",", given) + ")");
    }

    // The order the engine runs the timers in within a frame changes nothing: the same frames for every seat, on every machine.
    private static void StartAnyOrder()
    {
        int[] first = RunLine(100, null);
        bool same = true;
        for (int seed = 1; seed <= 40; seed++)
        {
            int[] given = RunLine(100, new Random(seed));
            for (int s = 0; s < StartSeats.Count; s++)
            {
                same &= given[s] == first[s];
            }
        }
        Check(same, "start line: whatever order the timers run in within a frame (40 orders), every seat starts in the same frame");
    }

    // The credit side waits for the record a few seconds after the load (EconomyDirector: WaitForRecord): the others go first,
    // and its followers then take a frame each, in their order, once it runs.
    private static void StartCreditLate()
    {
        int[] given = RunLine(100, new Random(7), 150);
        bool others = true;
        for (int s = StartSeats.Ventures; s < StartSeats.Count; s++)
        {
            others &= given[s] == 102 + (s - StartSeats.Ventures);   // the credit side's own seat came first, at 101
        }
        bool followers = true;
        for (int s = StartSeats.Banks; s <= StartSeats.Brands; s++)
        {
            followers &= given[s] == 151 + (s - StartSeats.Banks);
        }
        Check(given[StartSeats.Credit] == 101 && others, "start line: with the credit side not running yet, the other directors start one a frame without waiting for its followers");
        Check(followers && OnePerFrame(given), "start line: once the credit side runs, its followers start one a frame, in their order");
    }

    private static void StartEdges()
    {
        var q = new StartQueue(StartSeats.Count);
        Check(q.Holds(StartSeats.Fame, 5) && !q.Holds(StartSeats.Fame, 6) && q.Given(StartSeats.Fame) == 6, "start line: a director alone waits one frame");
        bool after = true;
        for (int frame = 7; frame < 200; frame++)
        {
            after &= !q.Holds(StartSeats.Fame, frame);
        }
        Check(after && q.GivenCount == 1, "start line: once given, its seat is never held again");
        Check(!q.Holds(-1, 7) && !q.Holds(StartSeats.Count, 7), "start line: a number that is no seat is never held");

        // A seat that stops asking (its director gone) holds nobody up after a frame.
        q = new StartQueue(StartSeats.Count);
        q.Holds(StartSeats.Credit, 10);   // ... and never asks again
        q.Holds(StartSeats.Flare, 10);
        bool held11 = q.Holds(StartSeats.Flare, 11);
        bool held12 = q.Holds(StartSeats.Flare, 12);
        Check(held11 && !held12 && q.Given(StartSeats.Flare) == 12 && q.Given(StartSeats.Credit) == int.MinValue,
            "start line: a seat before it that stopped asking holds a director up for one frame at most");

        // Begin: a new city, every seat waits again.
        q.Begin();
        Check(q.GivenCount == 0 && q.Given(StartSeats.Flare) == int.MinValue && q.Holds(StartSeats.Flare, 20) && !q.Holds(StartSeats.Flare, 21), "start line: a new city begins the line again");
        Check(q.Settled(30) && q.LastGiven == 21, "start line: with every seat given, the line is settled");
    }

    // Asking costs nothing: no allocation, a few comparisons, whether the seat waits or has been given.
    private static void StartCost()
    {
        var q = new StartQueue(StartSeats.Count);
        for (int s = 0; s < StartSeats.Count; s++)
        {
            q.Holds(s, 1);
        }
        long waiting = StartAsks(q, StartSeats.Permadeath, 2, true, out double waitMs);   // (held: the seats before it wait)
        q.Begin();
        q.Holds(StartSeats.Permadeath, 1);
        q.Holds(StartSeats.Permadeath, 2);
        long given = StartAsks(q, StartSeats.Permadeath, 3, false, out double givenMs);   // (given: what each second asks from then on)
        Check(waiting == 0 && given == 0, "start line: a million questions allocate nothing, waiting or given (" + waiting + " and " + given + " bytes)");
        Console.WriteLine("start line: a question while seats wait " + (waitMs * 1000000.0 / 1000000).ToString("F1") + " ns, once given " + (givenMs * 1000000.0 / 1000000).ToString("F1") + " ns");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long StartAsks(StartQueue q, int seat, int frame, bool wanted, out double ms)
    {
        var watch = new System.Diagnostics.Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        bool same = true;
        for (int i = 0; i < 1000000; i++)
        {
            same &= q.Holds(seat, frame) == wanted;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.Elapsed.TotalMilliseconds;
        return same ? allocated : -1;
    }

    // Each seat's director waits for its seat on its first tick and names its tick for the session record; the order of the
    // seats is the order the directors are made in, the credit side's followers inside its second as they were.
    private static void StartSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "start line: the mod's sources are at " + scripts);
            return;
        }
        // seat field, file, how it ticks (own: its own timer; follower: the credit side's second), what the wiring adds
        var table = new (string Seat, string File, bool Own, string Add)[]
        {
            ("Credit", @"Economy\Banking\EconomyDirector.cs", true, "Guard.Add<Economy.EconomyDirector>(_root)"),
            ("Banks", @"Economy\Banking\BankApps\BanksDirector.cs", false, "Guard.Add<Economy.BanksDirector>(_root)"),
            ("CityEconomy", @"Economy\City\CityEconomyDirector.cs", false, "Guard.Add<Economy.CityEconomyDirector>(_root)"),
            ("Dealer", @"Economy\Dealer\DealerDirector.cs", false, "Guard.Add<Economy.Dealership.DealerDirector>(_root)"),
            ("Online", @"Economy\Online\OnlineDirector.cs", false, "Guard.Add<Economy.OnlineDirector>(_root)"),
            ("Services", @"Economy\Services\ServicesDirector.cs", false, "Guard.Add<Economy.ServicesDirector>(_root)"),
            ("Company", @"Economy\Company\CompanyDirector.cs", false, "Guard.Add<Economy.CompanyDirector>(_root)"),
            ("Estate", @"Economy\Estate\EstateDirector.cs", false, "Guard.Add<Economy.EstateDirector>(_root)"),
            ("OwnBank", @"Economy\OwnBank\OwnBankDirector.cs", false, "Guard.Add<Economy.OwnBankDirector>(_root)"),
            ("Brands", @"Economy\Brands\BrandsDirector.cs", false, "Guard.Add<Economy.Brands.BrandsDirector>(_root)"),
            ("Ventures", @"Economy\Ventures\VenturesDirector.cs", true, "Guard.Add<Economy.Ventures.VenturesDirector>(_root)"),
            ("Markets", @"Markets\MarketDirector.cs", true, "Guard.Add<Markets.MarketDirector>(_root)"),
            ("Ledger", @"Ledger\LedgerDirector.cs", true, "Guard.Add<Ledger.LedgerDirector>(_root)"),
            ("Tech", @"Tech\TechDirector.cs", true, "Guard.Add<Tech.TechDirector>(_root)"),
            ("Careers", @"Careers\CareersDirector.cs", true, "Guard.Add<Careers.CareersDirector>(_root)"),
            ("Offices", @"Offices\Conversions.cs", true, "Guard.Add<Offices.Conversions>(_root)"),
            ("TechSites", @"Tech\Sites\TechSites.cs", true, "Guard.Add<Tech.TechSites>(_root)"),
            ("Flare", @"Social\Flare\FlareDirector.cs", true, "Guard.Add<Flare.FlareDirector>(_root)"),
            ("Fame", @"Fame\FameDirector.cs", true, "Guard.Add<Fame.FameDirector>(_root)"),
            ("Permadeath", @"Permadeath\PermadeathDirector.cs", true, "Guard.Add<PermadeathDirector>(_root)"),
        };
        Check(table.Length == StartSeats.Count, "start line: every seat is in the check's table");
        string mod = Source(scripts, @"Core\RealLifeOverhaulMod.cs");
        int lastOwn = -1, lastFollower = -1, seatOwn = -1, seatFollower = -1;
        bool ownOrder = true, followerOrder = true, wired = true, held = true, named = true;
        var missing = new List<string>();
        foreach (var row in table)
        {
            int seat = (int)typeof(StartSeats).GetField(row.Seat).GetRawConstantValue();
            string code = Source(scripts, row.File);
            int at = mod.IndexOf(row.Add, StringComparison.Ordinal);
            wired &= at >= 0;
            if (row.Own)
            {
                ownOrder &= at > lastOwn && seat > seatOwn;
                lastOwn = at;
                seatOwn = seat;
                string tick = Body(code, "private IEnumerator Tick()");
                bool waits = tick.Contains("StartLine.Holds(StartSeats." + row.Seat + ")") && tick.Contains("yield return null");
                bool timed = Body(code, "private void Slow()").Contains("HotSpots.Time(HotTick)") && code.Contains("HotSpots.Slot(\"");
                held &= waits;
                named &= timed;
                if (!waits || !timed)
                {
                    missing.Add(row.Seat);
                }
            }
            else
            {
                followerOrder &= at > lastFollower && seat > seatFollower && seat > StartSeats.Credit && seat < StartSeats.Ventures;
                lastFollower = at;
                seatFollower = seat;
                bool follows = code.Contains("EconomyDirector.Follow(StartSeats." + row.Seat + ", _second)") && code.Contains("EconomyDirector.Unfollow(_second)");
                bool timed = Body(code, "private void EachSecond()").Contains("HotSpots.Time(HotSecond)");
                held &= follows;
                named &= timed;
                if (!follows || !timed)
                {
                    missing.Add(row.Seat);
                }
            }
        }
        string missed = missing.Count > 0 ? " (not: " + string.Join(", ", missing) + ")" : string.Empty;
        Check(wired, "start line: every seat's director is made by the mod's start");
        Check(ownOrder, "start line: the directors with a timer of their own have their seats in the order they are made in, so they start in the order their timers ran in before");
        Check(followerOrder, "start line: the credit side's followers have their seats in the order they joined its second, between the credit side and the rest");
        Check(held, "start line: each director's first tick after a load waits for its seat (its own timer, or the credit side's for a follower)" + missed);
        Check(named, "start line: each director's tick opens a scope of its own for the session record (Core/HotSpots)" + missed);

        string credit = Body(Source(scripts, @"Economy\Banking\EconomyDirector.cs"), "private IEnumerator Tick()");
        Check(credit.Contains("StartLine.Holds(StartSeats.Credit)") && credit.Contains("_running && StartLine.Holds(seat)") && credit.Contains("Call(Followers[i])"),
            "start line: the credit side asks for each follower's seat while it runs, then calls it, in the seats' order");
        bool secondGone = true;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            secondGone &= !System.Text.RegularExpressions.Regex.IsMatch(Code(File.ReadAllText(file)), @"(?<![A-Za-z])EconomyDirector\.Second\b");   // (not "CityEconomyDirector.Second", a slot's name)
        }
        Check(secondGone, "start line: no feature joins the credit side's second without a seat (EconomyDirector.Second is gone)");
        int begin = mod.IndexOf("StartLine.Begin()", StringComparison.Ordinal);
        Check(begin >= 0 && begin < mod.IndexOf("Guard.Add<Economy.EconomyDirector>(_root)", StringComparison.Ordinal), "start line: it begins again with every city, before any director's timer starts");
        string line = Source(scripts, @"Core\StartLine.cs");
        Check(Body(line, "public static bool Holds(int seat)").Contains("LoadScene.isLoading") && Body(line, "public static bool Holds(int seat)").Contains("SaveGameManager.Current == null"),
            "start line: nothing is held while the save is not there or the loading screen is up (the directors' ticks return at their first line then)");
    }

    // The session record's word for the screen in a long frame, and the frames it counts as seen.
    private static void Screens()
    {
        Check(FrameRules.Screen(false, false, false, false, false) == FrameScreen.Clear, "screen: nothing up is a clear screen");
        Check(FrameRules.Screen(true, true, true, false, false) == FrameScreen.Black && FrameRules.Screen(false, true, true, false, false) == FrameScreen.Black,
            "screen: the game's black screen drawn at full strength as the frame began is black (also when its fade back in ended in the frame)");
        Check(FrameRules.Screen(true, false, false, false, false) == FrameScreen.Fading && FrameRules.Screen(false, true, false, false, false) == FrameScreen.Fading
              && FrameRules.Screen(true, true, false, false, false) == FrameScreen.Fading,
            "screen: the game's black screen fading out or back in at either look, not full as the frame began, is fading (up to 0.47.0 it was counted as black)");
        Check(!FrameRules.Unseen(FrameScreen.Fading) && !FrameRules.Unseen(FrameScreen.Clear) && FrameRules.Unseen(FrameScreen.Black) && FrameRules.Unseen(FrameScreen.Loading),
            "screen: a long frame in a fade is one the player saw; behind the black screen at full strength or the loading screen it is not");
        Check(FrameRules.Screen(false, false, false, true, false) == FrameScreen.Loading && FrameRules.Screen(false, false, false, false, true) == FrameScreen.Loading,
            "screen: the loading screen there at either look (up, or fading out after the save loaded) is the loading screen");
        Check(FrameRules.Screen(true, true, true, true, true) == FrameScreen.Loading, "screen: the loading screen is named before the black screen behind it");
        string loading = FrameRules.ScreenWords(FrameScreen.Loading);
        Check(loading.StartsWith("its screen was black or fading", StringComparison.Ordinal) && loading.Contains("loading screen") && loading.Contains("not seen as a stutter"),
            "screen: the long-frame line says the screen was black or fading because of the loading screen");
        Check(FrameRules.ScreenWords(FrameScreen.Clear) == "its screen was clear"
              && FrameRules.ScreenWords(FrameScreen.Black) == "its screen was black (its own walk into or out of a building: not seen as a stutter)"
              && FrameRules.ScreenWords(FrameScreen.Fading) == "its screen was fading, the picture showing through (its own walk into or out of a building: seen as a stutter)",
            "screen: the words for a clear screen, the black screen and its fade");

        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string watch = Source(scripts, @"Diagnostics\SessionWatch.cs");
        string update = Body(watch, "private void OnUpdate()");
        Check(update.Contains("FrameRules.Screen(faded, _fadedLastFrame, _coveredLastFrame, loading, _loadingLastFrame)") && update.Contains("hidden = FrameRules.Unseen(screen)")
              && update.Contains("_longHidden += hidden") && update.Contains("bool covered = faded && SceneFinds.Covered") && update.Contains("_coveredLastFrame = covered"),
            "screen: the session record counts a long frame with the loading screen there, or the black screen at full strength as it began, as one the player did not see, and one in a fade as seen");
        Check(watch.Contains("InstanceBehavior<UI.Load.LoadingScreen>.IsInitialized") && Body(watch, "private bool LoadingThere()").Contains("catch"),
            "screen: the loading screen is read from the game's register of its singletons (no search), in a method of its own that a build without it cannot break");
        Check(Body(watch, "private void Long(").Contains("FrameRules.ScreenWords(screen)"), "screen: the long-frame line takes its words from the rule");
    }
}
