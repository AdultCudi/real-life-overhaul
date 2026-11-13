using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;
using FirmOwner = RealLifeOverhaul.Population.Owner;
using IoPath = System.IO.Path;

// The News logic audit's fixes, through the handlers the game calls: the Newsstand's Reading tab (NewsReadingPage), "Fix your loan's rate" (NewsLoanFix),
// the Economy app's unemployment (the day series and NewsRules), and a business that opens (the population's record, the Ledger's story).
internal static partial class Program
{
    private static void NewsAuditChecks()
    {
        NewsReadingChecks();
        NewsFixChecks();
        NewsJoblessChecks();
        NewsOpeningChecks();
        NewsParityChecks();
    }

    // ---------------------------------------------------------------- the Newsstand's Reading tab

    private sealed class ReadDeskFake : INewsReadDesk
    {
        public int Today { get; set; } = 20;
        public int Stamp { get; set; }
        public readonly Dictionary<int, LedgerEdition> Kept = new Dictionary<int, LedgerEdition>();
        public int Readers, Printed, Told;

        public LedgerEdition Edition(int day) => Kept.TryGetValue(day, out LedgerEdition e) ? e : null;

        public UiReaderModel Reader(LedgerEdition edition, int title)
        {
            Readers++;
            return new UiReaderModel { Title = "paper " + title + " day " + edition.Day, Blocks = new[] { new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "words" } } };
        }

        public bool ReadPrinted(LedgerEdition edition)
        {
            Printed++;
            return true;
        }

        public void Tell(string key) => Told++;

        public string Fill(string key, params string[] pairs) => key + "|" + string.Join(",", pairs);

        public string TitleName(int title) => "Paper" + title;

        public string DayWords(int day) => "Day" + day;

        public string WeekPrice(int title) => "$" + (title + 3);

        public string DeliveryHour() => "06:00";
    }

    private static int NewsNodes(UiScene scene, NewsModel m, int command)
    {
        m.Title = "Newsstand";
        m.Across = true;
        m.Nav = new[] { new UiNavEntry { Icon = UiIcon.Newspaper, Label = "Titles" }, new UiNavEntry { Icon = UiIcon.Books, Label = "Reading" } };
        m.Tab = 1;
        NewsPages.Lay(scene, m, UiFrame.FullMenu(16f / 9f), true, UiDefaultKeys.Keyboard);
        int n = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            n += scene.Nodes[i].Command == command ? 1 : 0;
        }
        return n;
    }

    private static void NewsReadingChecks()
    {
        var desk = new ReadDeskFake();
        var page = new NewsReadingPage();
        var state = new NewsstandState();
        var model = new NewsModel { App = NewsApp.Stand, Tab = 1 };
        var scene = new UiScene();

        // Nothing taken out: nothing to read, every paper refused, the printed page too.
        page.Fill(model, state, desk);
        Check(model.Reader == null && page.Reading.State == NewsstandReadState.Nothing && model.EmptyTitle.StartsWith("reallife_news_read_none", StringComparison.Ordinal),
            "news reading: with no subscription the page holds nothing to read");
        Check(page.Press(model, NewsCommands.ReadTitle, 0, state, desk), "news reading: a paper can still be picked to see how to take it out");
        page.Fill(model, state, desk);
        Check(model.Reader == null && page.Reading.State == NewsstandReadState.NotHeld && model.EmptyBody.Contains("reallife_news_read_not_held_body") && model.ReadDays.Length == 0
              && string.IsNullOrEmpty(model.ReadPrinted.Label),
            "news reading: a paper you do not take opens no edition and shows how to take it out");
        Check(!page.Press(model, NewsCommands.ReadDay, 20, state, desk) && !page.Press(model, NewsCommands.ReadPrinted, 0, state, desk) && desk.Printed == 0 && desk.Readers == 0,
            "news reading: a day and the printed page are refused without a subscription, and no reader is made");
        Check(NewsNodes(scene, model, NewsCommands.ToTitles) == 1 && NewsNodes(scene, model, NewsCommands.ReadTitle) == 4,
            "news reading: the page offers the way to the Titles tab and the four papers to pick");

        // A subscription to the City Ledger with editions of three days.
        NewsstandRules.Start(state, NewsstandCatalogue.Ledger, 18);
        for (int d = 18; d <= 20; d++)
        {
            state.Loaded[NewsstandCatalogue.Ledger].Add(d);
            desk.Kept[d] = new LedgerEdition { Day = d };
        }
        page.Press(model, NewsCommands.ReadTitle, NewsstandCatalogue.Ledger, state, desk);
        page.Fill(model, state, desk);
        Check(page.Reading.State == NewsstandReadState.Edition && model.Reader != null && page.Reading.Day == 20 && model.ReadDays.Length == 3 && !string.IsNullOrEmpty(model.ReadPrinted.Label),
            "news reading: a paper you take opens its newest edition with its days and the printed page");
        UiReaderModel first = model.Reader;
        page.Fill(model, state, desk);
        Check(ReferenceEquals(first, model.Reader) && desk.Readers == 1, "news reading: the reader is kept between looks, so its scroll is not lost");
        Check(page.Press(model, NewsCommands.ReadDay, 19, state, desk) && page.Reading.Day == 19, "news reading: a day of the paper is picked");
        page.Fill(model, state, desk);
        Check(!ReferenceEquals(first, model.Reader) && model.Reader.Title.EndsWith("day 19", StringComparison.Ordinal), "news reading: the day picked is the edition read");
        Check(!page.Press(model, NewsCommands.ReadDay, 5, state, desk) && page.Reading.Day == 19, "news reading: a day with no edition kept cannot be picked");
        Check(page.Press(model, NewsCommands.ReadPrinted, 0, state, desk) && desk.Printed == 1 && NewsNodes(scene, model, NewsCommands.ReadPrinted) == 1,
            "news reading: the Ledger's printed page opens from the edition read");
        page.Press(model, NewsCommands.ReadTitle, 2, state, desk);
        page.Fill(model, state, desk);
        Check(model.Reader == null && page.Reading.State == NewsstandReadState.NotHeld && !page.Press(model, NewsCommands.ReadPrinted, 0, state, desk) && desk.Printed == 1,
            "news reading: another paper, not taken, is refused again and the printed page does not open");

        // The subscription ends: what came can still be read, with a line saying so. The Pass opens every paper.
        state.Subs[NewsstandCatalogue.Ledger].Subscribed = false;
        page.Press(model, NewsCommands.ReadTitle, NewsstandCatalogue.Ledger, state, desk);
        page.Fill(model, state, desk);
        Check(page.Reading.State == NewsstandReadState.Edition && model.ReadNote != null && model.ReadNote.Contains("box_kept"), "news reading: after a subscription ends the editions that came can be read");
        NewsstandRules.Start(state, NewsstandCatalogue.Pass, 20);
        state.Loaded[NewsstandCatalogue.Street].Add(20);
        desk.Kept[20] = new LedgerEdition { Day = 20 };
        page.Press(model, NewsCommands.ReadTitle, NewsstandCatalogue.Street, state, desk);
        page.Fill(model, state, desk);
        Check(page.Reading.State == NewsstandReadState.Edition && model.Reader != null && string.IsNullOrEmpty(model.ReadPrinted.Label),
            "news reading: a paper taken through the Pass is read, and only the Ledger has a printed page");
        Check(!page.Press(model, 9999, 0, state, desk) && !page.Press(model, NewsCommands.ReadTitle, 9, state, desk), "news reading: a press that is none of the page's, or a paper that is none, changes nothing");
        string live = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Stand.cs"));
        Check(!live.Contains("director.Latest)") && live.Contains("_reading.Fill("), "news reading: the live tab reads the page above, not the Ledger's latest edition for everyone");
    }

    private static string NewsRoot(string path)
    {
        string root = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        return IoPath.Combine(root, path.Replace('/', IoPath.DirectorySeparatorChar));
    }

    // ---------------------------------------------------------------- Fix your loan's rate

    // The game's loans as a list the checks can change between the question and the answer; a loan is told apart by its street, number and total.
    private sealed class FixDeskFake : ILoanFixDesk
    {
        public readonly List<LoanFixCase> Loans = new List<LoanFixCase>();
        public int Asked, Fixed, Opened, Told;
        public string FixedStreet;
        public int FixedIndex = -1;
        public Action Yes;

        public bool Find(out LoanFixCase found)
        {
            int best = -1;
            for (int i = 0; i < Loans.Count; i++)
            {
                best = best < 0 || Loans[i].Remaining > Loans[best].Remaining ? i : best;
            }
            found = default(LoanFixCase);
            if (best < 0)
            {
                return false;
            }
            found = Loans[best];
            found.Index = best;
            return true;
        }

        public bool Again(LoanFixCase asked, out LoanFixCase now)
        {
            now = default(LoanFixCase);
            for (int i = 0; i < Loans.Count; i++)
            {
                if (Loans[i].Street == asked.Street && Loans[i].Number == asked.Number && Loans[i].Total == asked.Total)
                {
                    now = Loans[i];
                    now.Index = i;
                    return true;
                }
            }
            return false;
        }

        public void Gone() => Told++;

        public void Ask(LoanFixCase asked, Action yes)
        {
            Asked++;
            Yes = yes;
        }

        public void Fix(LoanFixCase fixing)
        {
            Fixed++;
            FixedStreet = fixing.Street;
            FixedIndex = fixing.Index;
        }

        public bool OpenApp()
        {
            Opened++;
            return true;
        }
    }

    private static LoanFixCase FixLoan(string street, int total, int remaining) => new LoanFixCase { Street = street, Number = 1, Total = total, Remaining = remaining };

    private static void NewsFixChecks()
    {
        int fixedDaily = NewsRules.FixedDaily(1200000, ProductTerms.FixedRateBp(700, 300, 0, 1f), AccountRules.DaysPerYear);
        int variableDaily = fixedDaily * 2;   // a variable loan that costs twice what its fixed rate would
        bool offered = NewsLoanFix.Offer(0, "Elm", 4, 1500000, 1200000, variableDaily, 700, 300, AccountRules.DaysPerYear, true, out LoanFixCase made);
        Check(offered && made.OldRateBp > made.NewRateBp && made.NewRateBp == ProductTerms.FixedRateBp(700, 300, 0, 1f) && made.Saving == NewsRules.MonthlySaving(made.OldDaily, made.NewDaily)
              && made.Saving > 0 && made.Fee == 0 && made.Street == "Elm" && made.Number == 4 && made.Total == 1500000,
            "news fix: the case shows the old rate (" + made.OldRateBp + "), the new rate (" + made.NewRateBp + "), the saving ($" + made.Saving + ") and the fee (none), and names the loan");
        Check(!NewsLoanFix.Offer(0, "Elm", 4, 1, 1200000, fixedDaily / 2, 700, 300, AccountRules.DaysPerYear, false, out _) && !NewsLoanFix.Offer(0, "Elm", 4, 1, 0, variableDaily, 700, 300, AccountRules.DaysPerYear, true, out _),
            "news fix: a loan the fixed rate would not help, or one with nothing left, is not offered");

        var desk = new FixDeskFake();
        desk.Loans.Add(FixLoan("Elm", 900000, 800000));
        desk.Loans.Add(FixLoan("Oak", 1500000, 1200000));
        Check(NewsLoanFix.Press(desk) && desk.Asked == 1 && desk.Fixed == 0, "news fix: the press asks first and changes no loan");
        Check(desk.Fixed == 0 && desk.Yes != null, "news fix: an answer of no, or none, leaves the loan as it was");
        desk.Loans.RemoveAt(0);   // the other loan is repaid while the question is open: the one asked about moves up the list
        desk.Yes();
        Check(desk.Fixed == 1 && desk.FixedStreet == "Oak" && desk.FixedIndex == 0 && desk.Told == 0, "news fix: yes fixes the loan asked about, found by its identity after the list moved");
        var bigger = new FixDeskFake();
        bigger.Loans.Add(FixLoan("Oak", 1500000, 1200000));
        NewsLoanFix.Press(bigger);
        bigger.Loans.Insert(0, FixLoan("Pine", 9000000, 8000000));   // a bigger loan is taken meanwhile: it is the best to fix now, but it is not the one asked about
        bigger.Yes();
        Check(bigger.Fixed == 1 && bigger.FixedStreet == "Oak" && bigger.FixedIndex == 1, "news fix: a bigger loan taken while the question is open does not take the answer's place");
        var gone = new FixDeskFake();
        gone.Loans.Add(FixLoan("Oak", 1500000, 1200000));
        NewsLoanFix.Press(gone);
        gone.Loans.Clear();
        gone.Yes();
        Check(gone.Fixed == 0 && gone.Told == 1, "news fix: a yes after the loan is repaid changes nothing and tells the player");
        var other = new FixDeskFake();
        other.Loans.Add(FixLoan("Oak", 1500000, 1200000));
        NewsLoanFix.Press(other);
        other.Loans.Clear();
        other.Loans.Add(FixLoan("Pine", 9000000, 8000000));   // repaid, and another taken: the index is the same, the loan is not
        other.Yes();
        Check(other.Fixed == 0 && other.Told == 1, "news fix: a loan repaid and another taken while the question is open is not fixed in its place");
        var none = new FixDeskFake();
        Check(NewsLoanFix.Press(none) && none.Asked == 0 && none.Fixed == 0 && none.Opened == 1, "news fix: with no loan to fix the press opens the Economy app and asks nothing");
        string fix = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Fix.cs"));
        string live = File.ReadAllText(NewsRoot("Ui/News/NewsLive.cs"));
        Check(live.Contains("NewsLoanFix.Press(FixDesk)") && !live.Contains("LoanHolds.Begin") && !live.Contains("GameLoans.WriteTerms")
              && Regex.Matches(fix, @"GameLoans\.WriteTerms\(").Count == 1 && fix.IndexOf("GameConfirm.Show", StringComparison.Ordinal) > 0,
            "news fix: the live press goes through the question; the loan's terms are written in one place, behind it");
        string rival = File.ReadAllText(NewsRoot("Markets/MarketEngine.Session.cs"));
        Check(rival.Contains("NewsRules.Offer(") && rival.Contains("NewsRules.WillFix("), "news fix: a rival's debt is fixed by the same rule");
    }

    // ---------------------------------------------------------------- unemployment

    private static void NewsJoblessChecks()
    {
        PopulationSim sim = PopulationMade(777, out PopulationState s, out List<BusinessSeed> seeds);
        PopulationState before = CityPopulation.Live;
        CityPopulation.Live = s;
        var book = new DaySeriesBook();
        var seen = new HashSet<int>();
        for (int day = 2; day <= 91; day++)
        {
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
            DaySeriesRules.CityJobs(book, day, CityPopulation.Unemployment);
            seen.Add(CityPopulation.Unemployment);
        }
        CityPopulation.Live = before;
        Check(book.Has(DaySeriesCatalogue.Unemployment) && seen.Count > 3, "news jobless: the city's unemployment is written every day and is not constant (" + seen.Count + " different readings in 90 days)");
        var window = new int[NewsRules.DaysBook];
        book.Window(DaySeriesCatalogue.Unemployment, book.LastDay, 30, window);
        bool any = NewsRules.SeriesChange(window, 30, DaySeriesCatalogue.NoValue, out int last, out int change, out int days);
        var longWindow = new int[NewsRules.DaysBook];
        book.Window(DaySeriesCatalogue.Unemployment, book.LastDay, NewsRules.DaysBook, longWindow);
        bool anyLong = NewsRules.SeriesChange(longWindow, NewsRules.DaysBook, DaySeriesCatalogue.NoValue, out _, out int changeLong, out int daysLong);
        float[] spark = NewsRules.Spark(window, 30, DaySeriesCatalogue.NoValue, 12);
        bool moving = false;
        for (int i = 1; spark != null && i < spark.Length; i++)
        {
            moving |= spark[i] != spark[0];
        }
        Check(any && anyLong && days == 29 && daysLong > 29 && spark != null && spark.Length == 12 && moving && last >= 0,
            "news jobless: the figure's change and its line come from its own days (" + change + " over " + days + " days, " + changeLong + " over " + daysLong + ")");
        var thin = new int[30];
        for (int i = 0; i < 30; i++)
        {
            thin[i] = DaySeriesCatalogue.NoValue;
        }
        thin[29] = 55;
        Check(!NewsRules.SeriesChange(thin, 30, DaySeriesCatalogue.NoValue, out int one, out int none, out _) && one == 55 && none == 0 && NewsRules.Spark(thin, 30, DaySeriesCatalogue.NoValue, 12) == null,
            "news jobless: with one day of readings there is no change to give, never a nought dressed as one");
        string line = DaySeriesStore.Write(book);
        DaySeriesBook back = DaySeriesStore.Load(new Dictionary<string, string> { { DaySeriesCatalogue.Key, line } }, out _);
        var again = new int[30];
        back.Window(DaySeriesCatalogue.Unemployment, back.LastDay, 30, again);
        bool same = back.LastDay == book.LastDay;
        for (int i = 0; i < 30; i++)
        {
            same &= again[i] == window[i];
        }
        Check(same, "news jobless: the unemployment series is saved and read back the same");
        Check(!DaySeriesCatalogue.IsCityGood(DaySeriesCatalogue.Unemployment) && DaySeriesCatalogue.IsCityGood(DaySeriesCatalogue.CityPrice("Bread")),
            "news jobless: the unemployment line is no good in the goods table");
        string prices = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Prices.cs"));
        Check(!Regex.IsMatch(prices, @"StartsWith\(DaySeriesCatalogue\.CityPrefix"), "news jobless: the goods table reads its goods through IsCityGood");
    }

    // ---------------------------------------------------------------- a business that opens

    private static void NewsOpeningChecks()
    {
        PopulationSim sim = PopulationMade(4321, out PopulationState s, out List<BusinessSeed> seeds);
        Check(s.Events.Count == 0, "news openings: a new city has no openings recorded");
        // Name the city's and rivals' businesses; find a shop with no business and a seed to copy.
        var keys = new HashSet<string>();
        BusinessSeed template = default(BusinessSeed);
        bool haveTemplate = false;
        for (int i = 0; i < seeds.Count; i++)
        {
            BusinessSeed b = seeds[i];
            b.Name = "Firm " + i;
            b.Owner = i % 5 == 0 ? FirmOwner.Rival : FirmOwner.City;
            seeds[i] = b;
            keys.Add(PopulationState.KeyOf(b.Street, b.Number));
            if (!haveTemplate && PopulationCatalogue.CategoryOfType(b.Type) >= 0)
            {
                template = b;
                haveTemplate = true;
            }
        }
        var vacant = new List<CityBuilding>();
        for (int b = 0; b < s.Buildings.Count && vacant.Count < 3; b++)
        {
            CityBuilding cb = s.Buildings[b];
            if (cb.Use == Use.Shop && !keys.Contains(PopulationState.KeyOf(cb.Street, cb.Number)))
            {
                vacant.Add(cb);
            }
        }
        Check(haveTemplate && vacant.Count >= 2, "news openings: the checks' city has two empty shops to open in");
        BusinessSeed OpenAt(CityBuilding cb, string name, FirmOwner owner)
        {
            BusinessSeed o = template;
            o.Street = cb.Street;
            o.Number = cb.Number;
            o.Name = name;
            o.Owner = owner;
            return o;
        }
        var openings = new List<FirmEvent>();
        var played = new List<BusinessSeed>(seeds);
        for (int day = 2; day <= 60; day++)
        {
            if (day == 10)
            {
                played.Add(OpenAt(vacant[0], "Pizzeria Luigi", FirmOwner.City));
            }
            if (day == 20)
            {
                played.Add(OpenAt(vacant[1], "Brooks Bistro", FirmOwner.Rival));
            }
            sim.BeginDay(day, Ordinary, new List<BusinessSeed>(played));
            sim.RunAll();
        }
        s.Events.Between(FirmEventKind.Opened, 1, 61, openings);
        Check(openings.Count == 2 && openings[0].Name == "Pizzeria Luigi" && openings[0].Day == 10 && openings[0].Owner == FirmOwner.City
              && openings[1].Name == "Brooks Bistro" && openings[1].Day == 20 && openings[1].Owner == FirmOwner.Rival,
            "news openings: each opening is recorded with its firm and its day, the city's and the rival's alike (" + openings.Count + " recorded)");
        // The Ledger's story: in the edition after the day, once, naming the right firm; not in the editions before or after it.
        var stories = new List<string>();
        for (int day = 5; day <= 25; day++)
        {
            var found = new List<FirmEvent>();
            s.Events.Between(FirmEventKind.Opened, day - 1, day, found);   // an edition each morning: the window runs from the last edition's day up to today
            for (int i = 0; i < found.Count; i++)
            {
                LedgerPiece piece = LedgerStories.Opened(new LedgerOpening { Firm = found[i].Name, Day = found[i].Day, Rival = found[i].Owner == FirmOwner.Rival });
                Check(piece != null && piece.Section == LedgerSection.Business && piece.Value("firm") == found[i].Name && LedgerStories.OpenedStory(piece.Key),
                    "news openings: the story is a Business piece that names " + found[i].Name);
                stories.Add(day + ":" + found[i].Name + ":" + piece.Key);
            }
        }
        Check(stories.Count == 2 && stories[0] == "11:Pizzeria Luigi:" + LedgerStories.OpenedCity && stories[1] == "21:Brooks Bistro:" + LedgerStories.OpenedRival,
            "news openings: each opening is told once, in the edition after its day, the city's and the rival's by their own story (" + string.Join(", ", stories) + ")");
        Check(LedgerStories.Opened(new LedgerOpening { Firm = string.Empty }) == null && !LedgerStories.OpenedStory("reallife_ledger_beaten_1"), "news openings: no name, no story");

        // Saved and read back, and an older line (version 4) loads with no record.
        string line = PopulationStore.Encode(s);
        PopulationState back = PopulationStore.Decode(line, out int version);
        Check(back != null && version == PopulationStore.Version && back.Events.Count == s.Events.Count && back.Events[0].Name == s.Events[0].Name && back.Events[0].Day == s.Events[0].Day
              && back.Events[back.Events.Count - 1].Value == s.Events[s.Events.Count - 1].Value,
            "news openings: the record is saved with the population and read back the same");
        PopulationState old = PopulationStore.Decode(PopulationStore.EncodeVersion(s, 4), out int oldVersion);
        Check(old != null && oldVersion == 4 && old.Events.Count == 0 && old.Households.Live == s.Households.Live, "news openings: a line from before the record loads and plays on with none");
        var log = new FirmEventLog();
        for (int i = 1; i <= FirmEventLog.Most + 5; i++)
        {
            log.Add(FirmEventKind.Opened, FirmOwner.City, i, 0, "F" + i);
        }
        Check(log.Count == FirmEventLog.Most && log[0].Name == "F6" && !log.Add(FirmEventKind.Opened, FirmOwner.City, FirmEventLog.Most + 5, 0, "F" + (FirmEventLog.Most + 5)),
            "news openings: the record keeps the newest few and one doing once");
        string paper = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Paper.cs"));
        Check(!paper.Contains("Openings(") && !paper.Contains("Growth.Opened"), "news openings: the kit's paper no longer tells a count of the year's openings under the wrong firm");
    }
}
