using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.Offices;
using RealLifeOverhaul.Tech;

// BizMan's open, switch and tab change (Scripts/BizPace; docs/standards/performance.md, RLO-PERF-018). A player's report (27 Oct 2026) was that opening a
// business and switching business or tab in BizMan got slower with each release. The mod's part in a step is a few handlers; what the game-free parts of
// them cost is timed here on a large save's shape (100 businesses, 2,000 employees, 50,000 transactions, the city's 1,425 registrations), against the
// owner's budgets (under 2 ms to an open, under 1 ms to a switch or a tab change) and for what they allocate, which is nothing. The game's own parts
// (the pages, the layouts) are timed in the game by Options > Tests > Check BizMan's speed. Set RLO_BIZPACE_REPORT=1 to print the figures.
internal static partial class Program
{
    private const int BizBusinesses = 100, BizEmployees = 2000, BizTransactions = 50000, BizRegistrations = 1425, BizShops = 224;

    // A save's shape: the lists the handlers must not depend on, and the facts of each business the registration filter reads.
    private sealed class BizShape
    {
        public string[] Places;
        public bool[] Vacant, Office, Rented, Owned, Foreign, Held, OnSale;
        public int[] Staff;        // each employee's business
        public long[] Ledger;      // each transaction's amount
        public bool[] RentedByPlayer;   // the city's registrations, the player's among them
        public Dictionary<string, int> Looked;
    }

    private static BizShape MakeBizShape(int businesses, int employees, int transactions, int registrations)
    {
        var rng = new Random(2710);
        var s = new BizShape
        {
            Places = new string[businesses], Vacant = new bool[businesses], Office = new bool[businesses], Rented = new bool[businesses], Owned = new bool[businesses],
            Foreign = new bool[businesses], Held = new bool[businesses], OnSale = new bool[businesses], Staff = new int[employees], Ledger = new long[transactions],
            RentedByPlayer = new bool[registrations], Looked = new Dictionary<string, int>(businesses * 2, StringComparer.Ordinal)
        };
        for (int i = 0; i < businesses; i++)
        {
            s.Places[i] = "street_" + (i % 17) + "#" + (i * 7 + 3);
            s.Vacant[i] = i % 5 == 0;
            s.Office[i] = i % 3 == 0;
            s.Rented[i] = i % 5 != 0;
            s.Owned[i] = i % 11 == 0;
            s.OnSale[i] = i % 13 == 0;
        }
        for (int i = 0; i < employees; i++)
        {
            s.Staff[i] = rng.Next(businesses);
        }
        for (int i = 0; i < transactions; i++)
        {
            s.Ledger[i] = rng.Next(-5000, 5000);
        }
        for (int i = 0; i < registrations; i++)
        {
            s.RentedByPlayer[i] = i % (registrations / businesses) == 0 && i / (registrations / businesses) < businesses;
        }
        return s;
    }

    private static void BizPaceAll()
    {
        BizPaceRuleChecks();
        BizPaceBookChecks();
        BizPaceHarness();
        BizPaceSourceChecks();
        BizPaceTextChecks();
    }

    private static bool BizReport => Environment.GetEnvironmentVariable("RLO_BIZPACE_REPORT") == "1";

    private static string BizMs(double ms) => ms.ToString("0.0000", CultureInfo.InvariantCulture);

    private static string BizUs(double ms) => (ms * 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " us";

    // ---------------------------------------------------------------- the rules

    private static void BizPaceRuleChecks()
    {
        Check(BizPaceRules.OpenBudgetMs == 2.0 && BizPaceRules.SwitchBudgetMs == 1.0 && BizPaceRules.TabBudgetMs == 1.0, "bizman pace: the owner's budgets are 2 ms to an open, 1 ms to a switch and 1 ms to a tab change");
        Check(BizPaceRules.Budget(BizStep.Open) == 2.0 && BizPaceRules.Budget(BizStep.Switch) == 1.0 && BizPaceRules.Budget(BizStep.Tab) == 1.0, "bizman pace: Budget names each step's");
        Check(BizPaceRules.Within(BizStep.Open, 1.99) && !BizPaceRules.Within(BizStep.Open, 2.01) && BizPaceRules.Within(BizStep.Tab, 1.0) && !BizPaceRules.Within(BizStep.Switch, 1.0001),
            "bizman pace: a step is within its budget at it and over just past it");
        Check(BizPaceRules.Stronger(BizStep.Tab, BizStep.Open) == BizStep.Open && BizPaceRules.Stronger(BizStep.Open, BizStep.Tab) == BizStep.Open
              && BizPaceRules.Stronger(BizStep.Tab, BizStep.Switch) == BizStep.Switch && BizPaceRules.Stronger(BizStep.Switch, BizStep.Tab) == BizStep.Switch,
            "bizman pace: an open holds the tab and the switch it brings with it, a switch its tab");
        Check(BizPaceRules.OfTabMark(true) == BizStep.Switch && BizPaceRules.OfTabMark(false) == BizStep.Tab, "bizman pace: a tab's page switched on is a switch when the business moved, else a tab change");
        Check(BizPaceRules.Collects(10, 10) && BizPaceRules.Collects(10, 10 + BizPaceRules.Window) && !BizPaceRules.Collects(10, 11 + BizPaceRules.Window) && !BizPaceRules.Collects(10, 9),
            "bizman pace: an episode collects in its own frame and the " + BizPaceRules.Window + " after it, not before or later");
        Check(BizPaceRules.Word(BizStep.Open, false) == "open" && BizPaceRules.Word(BizStep.Switch, true) == "switches" && BizPaceRules.Word(BizStep.Tab, true) == "tab changes", "bizman pace: the steps' words");

        // The signature: every fact its own bit, so no two states of the seven facts share a number.
        var seen = new HashSet<int>();
        for (int bits = 0; bits < 128; bits++)
        {
            seen.Add(BizPaceRules.Signature((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0));
        }
        Check(seen.Count == 128, "bizman pace: the seven facts a registration handler keys on make 128 different signatures");
        int s0 = BizPaceRules.Signature(true, false, false, false, false, false, false);
        Check(!BizPaceRules.Moved(s0, BizPaceRules.Signature(true, false, false, false, false, false, false)) && BizPaceRules.Moved(s0, BizPaceRules.Signature(false, false, true, false, false, false, false)),
            "bizman pace: the same facts are no move, a business rented out of a vacant unit is one");
        Check(BizPaceRules.KeepsFor("ba:buildingtype_warehouse", "ba:buildingtype_warehouse") && !BizPaceRules.KeepsFor("ba:buildingtype_retail", "ba:buildingtype_warehouse")
              && !BizPaceRules.KeepsFor(null, "ba:buildingtype_warehouse"), "bizman pace: only a warehouse is a warehouse's change");

        // A walk passes under its budgets with each step made; a step missing or over fails and says which.
        var worst = new[] { 0.4, 0.2, 0.9 };
        var counts = new[] { 3, 2, 31 };
        Check(BizPaceRules.Passes(worst, counts, true, out string why) && why == null, "bizman pace: a walk under its budgets passes");
        Check(!BizPaceRules.Passes(new[] { 2.5, 0.2, 0.9 }, counts, true, out why) && why != null && why.Contains("open"), "bizman pace: an open over 2 ms fails, and the reason names it (" + why + ")");
        Check(!BizPaceRules.Passes(new[] { 0.4, 0.2, 1.4 }, counts, true, out why) && why != null && why.Contains("tab change"), "bizman pace: a tab change over 1 ms fails, and the reason names it (" + why + ")");
        Check(!BizPaceRules.Passes(worst, new[] { 3, 0, 31 }, true, out why) && why != null && why.Contains("switch"), "bizman pace: a walk with no switch fails where a second business was there to switch to (" + why + ")");
        Check(BizPaceRules.Passes(worst, new[] { 3, 0, 31 }, false, out why), "bizman pace: a walk with no switch passes for one business");
        Check(!BizPaceRules.Passes(worst, new[] { 0, 2, 31 }, false, out why) && why != null && why.Contains("open"), "bizman pace: a walk with no open fails (" + why + ")");
    }

    // ---------------------------------------------------------------- the book

    private static void BizPaceBookChecks()
    {
        var book = new BizPaceBook();
        var text = new StringBuilder();
        book.Append(text);
        Check(!book.Any && text.Length == 0, "bizman pace: an empty book adds nothing to the session line");
        book.Add(BizStep.Open, 0.4, "Interiors: InteriorTab.Fix", 0.3);
        book.Add(BizStep.Open, 2.6, "Interiors: InteriorTab.Write", 2.2);
        book.Add(BizStep.Tab, 0.1, "Core: ClaimedPresentation.Look", 0.1);
        Check(book.Any && book.Count[0] == 2 && book.Count[1] == 0 && book.Count[2] == 1, "bizman pace: the book counts each step's episodes");
        Check(book.Over[0] == 1 && book.Over[2] == 0 && book.WorstMs[0] == 2.6 && book.WorstHandler[0] == "Interiors: InteriorTab.Write", "bizman pace: the book keeps the episodes over budget, the worst and its handler");
        Check(book.Slowest == "Interiors: InteriorTab.Write" && book.SlowestMs == 2.2, "bizman pace: the book names the slowest handler of all");
        book.Append(text);
        string line = text.ToString();
        Check(line.StartsWith("; BizMan:", StringComparison.Ordinal) && line.Contains("2 opens (the mod added 2.60 ms at most, wanted under 2.00, 1 over)") && line.Contains("1 tab change (the mod added 0.10 ms at most, wanted under 1.00)")
              && line.Contains("the slowest handler Interiors: InteriorTab.Write 2.20 ms") && !line.Contains("switch"), "bizman pace: the session line's part says each step, its worst, the budget and the slowest handler (" + line + ")");
        book.Clear();
        text.Clear();
        book.Append(text);
        Check(!book.Any && text.Length == 0 && book.Slowest == null, "bizman pace: a cleared book is empty");
    }

    // ---------------------------------------------------------------- the harness: a large save's shape

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int BizEvents(BizShape s, int events, bool flip, int flipAt)
    {
        int dirty = 0;
        int n = s.Places.Length;
        for (int e = 0; e < events; e++)
        {
            int b = e % n;
            if (flip && e == flipAt)
            {
                s.Vacant[b] = !s.Vacant[b];   // one business's facts really change
            }
            int now = BizPaceRules.Signature(s.Vacant[b], s.Office[b], s.Rented[b], s.Owned[b], s.Foreign[b], s.Held[b], s.OnSale[b]);
            if (!s.Looked.TryGetValue(s.Places[b], out int was) || BizPaceRules.Moved(was, now))
            {
                s.Looked[s.Places[b]] = now;
                dirty++;
            }
        }
        return dirty;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void BizEpisodes(BizPaceBook book, int count)
    {
        for (int i = 0; i < count; i++)
        {
            book.Add((BizStep)(i % BizPaceRules.Steps), 0.2 + (i % 7) * 0.01, "Interiors: InteriorTab.Fix", 0.1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int BizSeenWalk(BizShape s, List<FirmSeen> into)
    {
        into.Clear();
        for (int i = 0; i < s.RentedByPlayer.Length; i++)
        {
            if (s.RentedByPlayer[i])
            {
                into.Add(new FirmSeen { Kind = i % 7, Place = "street#" + i, Name = "Firm " + i, Theirs = false });   // what FirmsGame.Seen makes for each of the player's businesses
            }
        }
        return into.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int BizCards(IList<FirmSeen> seen, IList<string> schools, int rounds)
    {
        int total = 0;
        for (int r = 0; r < rounds; r++)
        {
            for (int kind = 0; kind < CompetitorRules.Kinds; kind++)
            {
                total += CompetitorRules.Gather(kind, null, schools, seen, null, null, null);
            }
        }
        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int BizChoose(List<ConversionRow> rows, bool[] want, int rounds)
    {
        int free = 0;
        for (int r = 0; r < rounds; r++)
        {
            free += ConversionRules.Decide(rows, want);
        }
        return free;
    }

    private static void BizPaceHarness()
    {
        BizShape small = MakeBizShape(5, 50, 500, 60);
        BizShape large = MakeBizShape(BizBusinesses, BizEmployees, BizTransactions, BizRegistrations);
        BizShape[] shapes = { small, large };
        double[] perEvent = new double[2];

        // The registration change event: a handler keyed on the building's own facts. The first event for a building has nothing to compare with and asks; after that
        // a change that concerns nothing asks for nothing; a real change asks once.
        for (int k = 0; k < 2; k++)
        {
            BizShape s = shapes[k];
            int firstAsks = BizEvents(s, s.Places.Length, false, -1);
            Check(firstAsks == s.Places.Length, "bizman pace harness: the first event for each of " + s.Places.Length + " businesses asks (nothing to compare with)");
            BizEvents(s, 4000, false, -1);   // warmed
            const int events = 200000;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            int dirty = BizEvents(s, events, false, -1);
            double ms = watch.Elapsed.TotalMilliseconds;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            perEvent[k] = ms / events;
            Check(dirty == 0, "bizman pace harness: " + events + " events that change nothing ask for nothing (" + dirty + ") on " + s.Places.Length + " businesses");
            Check(allocated < 4096, "bizman pace harness: the registration filter allocates nothing over " + events + " events on " + s.Places.Length + " businesses (" + allocated + " bytes)");
            Check(perEvent[k] < 0.005, "bizman pace harness: a registration event is read in under 5 microseconds on " + s.Places.Length + " businesses (" + BizMs(perEvent[k]) + " ms)");
            int real = BizEvents(s, events / 10, true, 17);
            Check(real == 1, "bizman pace harness: one business's real change asks exactly once (" + real + ")");
        }
        Check(perEvent[1] <= perEvent[0] * 3.0 + 0.002, "bizman pace harness: a registration event costs the same on a save of " + BizBusinesses + " businesses, " + BizEmployees + " employees and "
              + BizTransactions + " transactions as on a small one (" + BizMs(perEvent[0]) + " and " + BizMs(perEvent[1]) + " ms)");

        // The accounting of an episode: no allocation, a microsecond.
        var book = new BizPaceBook();
        BizEpisodes(book, 2000);
        long booked = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        BizEpisodes(book, 200000);
        double perEpisode = clock.Elapsed.TotalMilliseconds / 200000;
        booked = GC.GetAllocatedBytesForCurrentThread() - booked;
        Check(booked < 4096, "bizman pace harness: counting an episode allocates nothing (" + booked + " bytes over 200000)");
        Check(perEpisode < 0.005, "bizman pace harness: counting an episode takes under 5 microseconds (" + BizMs(perEpisode) + " ms)");

        // The start screen's cards. Before: the player's businesses were walked among all the city's registrations at every opening. After: the walk is made when a registration
        // changed, the day turned or the academy moved; the cards' own count is what is left.
        var seenList = new List<FirmSeen>(128);
        BizSeenWalk(large, seenList);
        long walked = GC.GetAllocatedBytesForCurrentThread();
        var walk = Stopwatch.StartNew();
        const int walks = 2000;
        for (int i = 0; i < walks; i++)
        {
            BizSeenWalk(large, seenList);
        }
        double walkMs = walk.Elapsed.TotalMilliseconds / walks;
        walked = (GC.GetAllocatedBytesForCurrentThread() - walked) / walks;
        var schools = new List<string>(2) { "academy" };
        BizCards(seenList, schools, 20);
        var cards = Stopwatch.StartNew();
        const int rounds = 2000;
        BizCards(seenList, schools, rounds);
        double cardsMs = cards.Elapsed.TotalMilliseconds / rounds;
        Check(seenList.Count == BizBusinesses, "bizman pace harness: the walk finds the player's " + BizBusinesses + " businesses among the city's " + BizRegistrations + " registrations (" + seenList.Count + ")");
        Check(walkMs < 1.0, "bizman pace harness: the walk the start screen no longer makes at every opening took under 1 ms on a large save (" + BizMs(walkMs) + " ms, " + walked + " bytes)");
        Check(cardsMs < 1.0, "bizman pace harness: the start cards' competitor counts for every kind take under 1 ms on a large save (" + BizMs(cardsMs) + " ms)");
        double cacheMs = BizCacheCheck(walks * 50);
        Check(cacheMs < 0.001, "bizman pace harness: the cache check that replaces the walk takes under a microsecond (" + BizMs(cacheMs) + " ms)");

        // The conversions' choice. Before: the whole choice made again (a second later) after every registration event of a shop or an office, BizMan's included.
        // After: only a real change in the building's facts asks for it.
        var rows = new List<ConversionRow>(BizShops);
        for (int i = 0; i < BizShops; i++)
        {
            rows.Add(new ConversionRow { Street = "street_" + (i % 9), Number = i, District = "district_" + (i % 6), Office = i % 4 == 0, Free = i % 5 == 0, Held = i % 11 == 0 });
        }
        var want = new bool[BizShops];
        BizChoose(rows, want, 20);
        long chose = GC.GetAllocatedBytesForCurrentThread();
        var choice = Stopwatch.StartNew();
        const int choices = 1000;
        BizChoose(rows, want, choices);
        double chooseMs = choice.Elapsed.TotalMilliseconds / choices;
        chose = (GC.GetAllocatedBytesForCurrentThread() - chose) / choices;
        Check(chooseMs < 2.0, "bizman pace harness: the conversions' choice over " + BizShops + " buildings takes under 2 ms (" + BizMs(chooseMs) + " ms)");
        Check(perEvent[1] * 20 < chooseMs, "bizman pace harness: reading a registration event costs a twentieth of the choice it used to ask for (" + BizMs(perEvent[1]) + " against " + BizMs(chooseMs) + " ms)");

        if (BizReport)
        {
            Console.WriteLine("BizPace harness: a registration event " + BizUs(perEvent[0]) + " (small save) and " + BizUs(perEvent[1]) + " (" + BizBusinesses + " businesses, " + BizEmployees + " employees, "
                              + BizTransactions + " transactions); an episode counted " + BizUs(perEpisode) + "; the start screen's walk before " + BizUs(walkMs) + " and " + walked + " bytes, its cards "
                              + BizUs(cardsMs) + ", the cache check that replaces the walk " + BizUs(cacheMs) + "; the conversions' choice before " + BizUs(chooseMs) + " and " + chose
                              + " bytes at each event of a shop or an office.");
        }
    }

    // The cache check InCity makes now: three comparisons.
    private static int _bizStamp = 5, _bizSeenStamp = 5, _bizDay = 9, _bizSeenDay = 9;
    private static string _bizAcademy = "ba:businesstype_academy", _bizSeenAcademy = "ba:businesstype_academy";

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double BizCacheCheck(int rounds)
    {
        int reads = 0;
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++)
        {
            if (_bizSeenStamp != _bizStamp || _bizSeenDay != _bizDay || !string.Equals(_bizSeenAcademy, _bizAcademy, StringComparison.Ordinal))
            {
                reads++;
            }
        }
        double ms = clock.Elapsed.TotalMilliseconds / rounds;
        return reads == 0 ? ms : double.MaxValue;
    }

    // ---------------------------------------------------------------- the sources

    private static void BizPaceSourceChecks()
    {
        string tech = ModSource("Tech/TechDirector.Infra.cs"), techMain = ModSource("Tech/TechDirector.cs"), rivals = ModSource("Tech/TechDirector.Rivals.cs");
        string conv = ModSource("Offices/Conversions.cs"), corner = ModSource("Corners/CornerSigns.cs"), names = ModSource("Core/SpecialNames.cs");
        string pages = ModSource("Interiors/InteriorTab.Pages.cs"), tab = ModSource("Interiors/InteriorTab.cs");
        string watch = ModSource("Diagnostics/SessionWatch.cs"), lines = ModSource("Diagnostics/SessionWatch.Lines.cs"), features = ModSource("Meta/FeatureList.cs");
        string game = ModSource("BizPace/BizPaceGame.cs");

        // The Tech handler asks for the warehouses to be read again only for a warehouse, counts every event for what the start screen read, and keeps its handler on whatever the sector does.
        string change = BizBetween(tech, "private void OnRegistrationChange(", "KeepWarehouses()");
        Check(change.Length > 0 && InOrder(change, "BizPaceWatch.Time(BizChange)", "_registrationStamp++;", "if (InfraGame.IsWarehouse(address))", "_warehousesDirty = true;"),
            "bizman pace source: the Tech handler counts the event and asks for the warehouses only for a warehouse");
        Check(!BizBetween(tech, "private void KeepWarehouses()", "private bool AskCentre(").Contains("EventHold.Keep") && techMain.Contains("KeepRegistrationHandler();")
              && BizBetween(tech, "private void KeepRegistrationHandler()", "private void KeepWarehouses()").Contains("EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onRegChange, ref _regChangeSeen);"),
            "bizman pace source: the Tech handler is put on once, by the director's timer, whatever the sector does");
        Check(InOrder(rivals, "if (_seenStamp != _registrationStamp || _seenDay != day", "FirmsGame.Seen(_seen, academy);", "_seenStamp = _registrationStamp;"), "bizman pace source: the start screen reads the city's businesses when a registration changed, the day turned or the academy moved");

        // The conversions' handler keys on the building's facts; the first event for a building asks; the choice is timed as what a step leaves for the next tick.
        string onChange = BizBetween(conv, "private void OnChange(Address address)", "private static int Facts(");
        Check(onChange.Length > 0 && InOrder(onChange, "BizPaceWatch.Time(BizChange)", "_at.TryGetValue(address, out CityBuildingController c)", "Facts(c)", "!_looked.TryGetValue(address, out int was) || BizPaceRules.Moved(was, now)", "_dirty = true;"),
            "bizman pace source: the conversions' handler asks for a look only when the building's facts moved, or at the first event for it");
        Check(conv.Contains("BizPaceWatch.Time(BizChoose)") && !onChange.Contains("_watched.Contains"), "bizman pace source: the conversions' choice is timed, and no longer asked for by every event of a watched building");

        // The corner signs look nothing up while no corner building stands; the special names' handler is a method of its own.
        Check(InOrder(corner, "private static void OnChange(Address address)", "!CornerLook.Any", "BizPaceWatch.Time(BizChange)", "CornerLook.AtAddress("), "bizman pace source: the corner signs' handler looks nothing up while no corner building stands");
        Check(InOrder(names, "_onRegistration = Guard.Wrap<Address>(OnRegistration);", "private void OnRegistration(Address address)", "BizPaceWatch.Time(BizRegistration)", "if (CityMap.IsOpen)"),
            "bizman pace source: the special names' handler is a method of its own, timed, and asks for something only while the map is open");

        // The Interior tab: its two steps are timed; the first opening no longer lays its columns out to write a line nobody reads.
        Check(tab.Contains("BizPaceWatch.Time(BizFix)") && pages.Contains("BizPaceWatch.Time(BizWrite)"), "bizman pace source: the Interior tab's Fix and Write are timed");
        string write = BizBetween(pages, "internal static void Write()", "// How the columns fit");
        Check(write.Length > 0 && !write.Contains(".Fit()") && write.Contains("if (!_fitLogged && Meta.Dev.Verbose)") && BizBetween(pages, "private static void LogFit()", "// The page selector").Contains(".Fit()"),
            "bizman pace source: the Interior tab lays its columns out for the log line only when the line is read");

        // The Interior tab keeps the page written in this showing of the view: flipping back to it writes nothing; a new showing, another business, another page, another language
        // or a click that changed what it says writes it afresh.
        string flip = BizBetween(pages, "internal static void Write()", "/// <summary>The page written afresh");
        Check(InOrder(flip, "using var timed = BizPaceWatch.Time(BizWrite);", "if (!Written())", "Rewrite();"), "bizman pace source: the Interior tab writes its page only when the boxes do not hold it");
        string held = BizBetween(pages, "private static bool Written()", "/// <summary>The page written afresh");
        Check(InOrder(held, "_wroteShop == null", "_wroteSession != Session", "_wrotePage != _shown", "LocalizorManager.LoadedLocale", "ReferenceEquals(reg, _wroteReg)", "ReferenceEquals(InteriorDirector.At(reg), _wroteShop)"),
            "bizman pace source: the page held counts only for the same business, showing, page and language");
        Check(InOrder(pages, "_wroteReg = reg;", "_wroteShop = s;", "_wroteSession = Session;", "_wroteLocale = LocalizorManager.LoadedLocale;") && pages.Contains("UiClone.Safe(Rewrite);") && !pages.Contains("UiClone.Safe(Write);"),
            "bizman pace source: the page is noted when written, and the refresh click writes it afresh");
        Check(InOrder(tab, "private void OnEnable()", "InteriorTab.Dirty = true;", "InteriorTab.Session++;"), "bizman pace source: a new showing of BizMan's business view is a new session of the Interior tab's page");

        // The code of the steps compiled ahead, a slice a frame; the special names' handlers let go with their feature.
        string warm = ModSource("Core/DayWarm.cs");
        Check(warm.Contains("BizPace.BizPaceGame.Warm(_warm);") && game.Contains("warm.Root(GlobalEvents.onBuildingRegistrationChange);") && game.Contains("typeof(Interiors.InteriorTab), \"Write\"")
              && game.Contains("typeof(ClaimedPresentation), \"Look\"") && game.Contains("typeof(Tech.StartCards), \"Write\""), "bizman pace source: the handlers of BizMan's steps are compiled ahead with the day's");
        string gone = BizBetween(names, "private void OnDestroy()", "private IEnumerator Look()");
        Check(InOrder(gone, "GlobalEvents.onEnterBuilding -= _onEnter;", "GlobalEvents.onCityMapToggle -= _onMap;", "GlobalEvents.onBuildingRegistrationChange -= _onRegistration;"),
            "bizman pace source: the special names' handlers on the game's events are let go with it, none left for a second copy to double");

        // The record, the minute's line and the test.
        Check(InOrder(watch, "HotSpots.On = true;", "BizPaceWatch.Arm(true);") && InOrder(watch, "HotSpots.On = false;", "BizPaceWatch.Arm(false);") && InOrder(watch, "HotSpots.Settle(Time.frameCount);", "BizPaceWatch.Frame(Time.frameCount);")
              && lines.Contains("BizPaceWatch.MinuteLine(_line);"), "bizman pace source: the session record arms the watch, closes its episodes each frame and ends the minute's line with its book");
        Check(features.Contains(".Test(\"reallife_perf_test_bizman\", BizPace.BizPaceTest.Run)"), "bizman pace source: the test is under Performance in Options > Tests");
        Check(game.Contains("GameContract.BizPaceContainers") && ModSource("Core/GameContract.cs").Contains("ContractBook.Part(\"bizman_pace\""), "bizman pace source: the marks read BizMan's pages through the game contract");
        Check(!game.Contains("FindObject") && !game.Contains("GetComponentsInChildren"), "bizman pace source: the marks are put on by a look at BizMan's own pages, never a search of the scene");
    }

    private static string BizBetween(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        if (a < 0)
        {
            return string.Empty;
        }
        int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
    }

    // ---------------------------------------------------------------- the texts

    private static void BizPaceTextChecks()
    {
        string[] keys = { "reallife_perf_test_bizman", "reallife_perf_test_bizman_pass", "reallife_perf_test_bizman_fail", "reallife_perf_test_bizman_none", "reallife_perf_test_bizman_unsaved" };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool all = words != null;
            for (int i = 0; all && i < keys.Length; i++)
            {
                all = words.TryGetValue(keys[i], out string v) && v.Length > 0 && v.Length <= (i == 0 ? 60 : 240) && !v.Contains('!') && !v.Contains((char)0x2014) && !v.Contains((char)0x2013) && !v.Contains("  ");
            }
            Check(all, "bizman pace texts: the test's name and four notices in " + code + " are there, short, plain, with no double space");
            Check(words != null && words.TryGetValue("reallife_perf_test_bizman_pass", out string pass) && pass.Contains("{open}") && pass.Contains("{switch}") && pass.Contains("{tab}") && pass.Contains("{count}")
                  && words.TryGetValue("reallife_perf_test_bizman_fail", out string fail) && fail.Contains("{open}") && fail.Contains("{switch}") && fail.Contains("{tab}") && fail.Contains("{handler}"),
                "bizman pace texts: the pass and fail notices in " + code + " carry the figures");
        }
        // The fix line in all eleven languages: it is in some What's new row, once, with its mark, and has no double space.
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: leaving a business in BizMan made the mod look over its warehouses and empty shops again" }, { "pt", "Corrigido: sair de um negócio no BizMan fazia o mod examinar de novo" },
            { "zh-cn", "已修复：在 BizMan 中离开一家企业时" }, { "de", "Behoben: Wer in BizMan ein Geschäft verließ" }, { "fr", "Corrigé : quitter une entreprise dans BizMan faisait repasser le mod" },
            { "es", "Corregido: salir de un negocio en BizMan hacía que el mod revisara" }, { "ru", "Исправлено: выход из бизнеса в BizMan заставлял мод" },
            { "nl", "Opgelost: een bedrijf verlaten in BizMan liet de mod" }, { "tr", "Düzeltildi: BizMan'de bir işletmeden çıkmak" }, { "pl", "Naprawiono: wyjście z firmy w BizMan sprawiało" },
            { "ko", "수정: BizMan에서 사업체를 나가면" }
        };
        foreach (KeyValuePair<string, string> start in starts)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, start.Key + ".json"));
            int rows = 0;
            bool plain = true;
            if (words != null)
            {
                foreach (KeyValuePair<string, string> row in words)
                {
                    if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(start.Value))
                    {
                        rows++;
                        plain &= !row.Value.Contains("  ") && !row.Value.Contains((char)0x2014) && !row.Value.Contains((char)0x2013) && !row.Value.Contains('!');
                    }
                }
            }
            Check(rows == 1 && plain, "bizman pace texts: What's new has the BizMan fix once in " + start.Key + ", with no double space, dash or exclamation mark (" + rows + " row(s))");
        }
    }
}
