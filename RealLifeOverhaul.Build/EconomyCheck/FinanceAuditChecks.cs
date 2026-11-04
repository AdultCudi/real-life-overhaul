using System;
using System.Collections.Generic;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Ui;

// The Finance audit's fixes (4 Nov 2026), each through the rule the page or the day turn calls:
//   1. the 5 per cent price cut put a hold on every plan and took it off every plan at its end, the player's own holds included, and was on
//      when it had moved nothing (PriceCutRules, the one rule the Finance page, the day turn and the rivals' answer share);
//   2. the Overview's "costs more after the last rise" and its rate review line;
//   3. "where it went, 30 days" read the game's short window, not the saved ledger (FinanceRules.CategoryNet);
//   4. the Flare campaign answer's forecast, and the rivals' answers (RivalRules.Weight);
//   5. the business loan wants a month of takings that cover the payment (ProductTerms), and the orphan rules gone.
internal static partial class Program
{
    // The player's pricing plans, without the game: each plan is held or not, and a cut moves its managed prices (none for a plan set by hand).
    private sealed class FakePlans : IPlanHolds
    {
        public readonly List<string> Ids = new List<string>();
        public readonly List<bool> Hold = new List<bool>();
        public readonly List<int> Managed = new List<int>();   // prices the plan manages: what a cut moves

        public int Count => Ids.Count;

        public FakePlans Add(string id, bool held, int managed)
        {
            Ids.Add(id);
            Hold.Add(held);
            Managed.Add(managed);
            return this;
        }

        public string IdAt(int index) => Ids[index];

        public bool Held(int index) => Hold[index];

        public bool Cuttable(int index) => Managed[index] > 0;

        public int Cut(int index, int percent) => percent > 0 && percent < 50 ? Managed[index] : 0;

        public void SetHeld(int index, bool held) => Hold[index] = held;

        public bool HeldId(string id) => Hold[Ids.IndexOf(id)];
    }

    private static void FinanceAuditChecks()
    {
        PriceCutHolds();
        PriceCutRun();
        RivalAnswers();
        LedgerWhere();
        RiseAndReview();
        BusinessRule();
        AnswersPressed();
    }

    // A source that shows one sample state of the Rivals tab and records what it is asked.
    private sealed class StateStub : IFinanceSource
    {
        private readonly string _state;
        public readonly List<string> Acts = new List<string>();

        public StateStub(string state)
        {
            _state = state;
        }

        public int Generation => Acts.Count;

        public int TakeWanted() => -1;

        public void Read(FinanceFacts into, FinanceView view)
        {
            FinanceFacts f = UiSamples.FinanceFactsOf((key, english) => english, _state);
            into.Ready = f.Ready;
            into.Day = f.Day;
            into.Score = f.Score;
            into.Rivals = f.Rivals;
            into.RivalsCount = f.RivalsCount;
        }

        public bool Do(FinanceAct act, FinanceView view, int arg)
        {
            Acts.Add(act + ":" + arg);
            return true;
        }
    }

    // ---------------------------------------------------------------- every answer, pressed through the screen's real handler

    private static void AnswersPressed()
    {
        foreach (string state in new[] { "rivals_move", "rivals_cut", "rivals_blocked" })
        {
            var stub = new StateStub(state);
            var screen = new FinanceScreen(stub, UiSamples.FinanceWordsOf((key, english) => english));
            screen.Press(UiAppWindow.NavCommand, FinanceFace.Rivals);
            FinanceAnswer[] answers = UiSamples.FinanceFactsOf((key, english) => english, state).Rivals.Answers;
            int acted = 0, wrong = 0, refused = 0;
            for (int i = 0; i < answers.Length; i++)
            {
                LayFinance(screen);
                int before = stub.Acts.Count;
                bool did = screen.Press(FinanceFace.Answer, i);
                if (answers[i].On)
                {
                    acted += did && stub.Acts.Count == before + 1 && stub.Acts[stub.Acts.Count - 1] == answers[i].Act + ":" + answers[i].Arg ? 1 : 0;
                }
                else
                {
                    refused += !did && stub.Acts.Count == before ? 1 : 0;
                    wrong += did ? 1 : 0;
                }
            }
            int on = 0;
            foreach (FinanceAnswer a in answers)
            {
                on += a.On ? 1 : 0;
            }
            Check(answers.Length >= 3 && acted == on && wrong == 0 && refused == answers.Length - on,
                "finance answers (" + state + "): each answer on reaches the handler of its own act, and one that is off, with its reason, does nothing (" + acted + " of " + on + ", " + refused + " refused)");
        }
        FinanceAnswer[] blocked = UiSamples.FinanceFactsOf((key, english) => english, "rivals_blocked").Rivals.Answers;
        FinanceAnswer[] cut = UiSamples.FinanceFactsOf((key, english) => english, "rivals_cut").Rivals.Answers;
        Check(blocked[0].Act == FinanceAct.Campaign && !blocked[0].On && blocked[0].Hint.StartsWith("Not now", StringComparison.Ordinal) && blocked[1].Act == FinanceAct.CutPrices && !blocked[1].On
              && blocked[1].Hint.StartsWith("Not now", StringComparison.Ordinal) && cut[1].Act == FinanceAct.HoldPrices && cut[1].On,
            "finance answers: a cut with no plan to move, or a campaign that cannot be booked, is drawn off with its reason, and a cut that is on offers to end it");
    }

    // ---------------------------------------------------------------- 1. the price cut and the holds

    private static void PriceCutHolds()
    {
        // A held plan stays held after the cut and its release; the plan the cut held is let go; a plan with nothing to cut is never touched.
        var book = new DaySeriesBook { Seed = DaySeriesRules.DefaultSeed };
        FakePlans plans = new FakePlans().Add("held", true, 3).Add("free", false, 2).Add("hand", false, 0).Add("held-hand", true, 0);
        int moved = PriceCutRules.Start(book, plans, 10);
        Check(moved == 5 && DaySeriesRules.CutOn(book, 10) && book.CutUntil == 10 + DaySeriesRules.CutDays && plans.HeldId("free") && plans.HeldId("held") && !plans.HeldId("hand"),
            "price cut: the plans it moved are held, and the cut is on for 14 days (" + moved + ")");
        Check(PriceCutRules.Has(book.HeldAlready, "held") && !PriceCutRules.Has(book.HeldAlready, "free") && PriceCutRules.Has(book.HeldByCut, "free") && !PriceCutRules.Has(book.HeldByCut, "held"),
            "price cut: it records which plans were held before it and which it held (" + book.HeldAlready + " | " + book.HeldByCut + ")");
        Check(PriceCutRules.Start(book, plans, 11) == 0 && book.CutFrom == 10, "price cut: a cut already on is not started again");
        string line = DaySeriesStore.Write(book);
        DaySeriesBook back = DaySeriesStore.Read(line, out _);
        Check(back != null && back.HeldAlready == book.HeldAlready && back.HeldByCut == book.HeldByCut && back.CutFrom == 10 && back.CutUntil == book.CutUntil,
            "price cut: what it held, and what was held before, is kept in the save line (" + line.Substring(0, Math.Min(60, line.Length)) + ")");
        int freed = PriceCutRules.End(back, plans);
        Check(freed == 1 && plans.HeldId("held") && !plans.HeldId("free") && plans.HeldId("held-hand") && !plans.HeldId("hand") && back.CutFrom == 0 && back.HeldByCut.Length == 0,
            "price cut: its release leaves a plan the player already held held, and takes the hold off only the plan the cut held");

        // A cut that moves nothing is not on, and holds nothing.
        var none = new DaySeriesBook();
        FakePlans manual = new FakePlans().Add("a", false, 0).Add("b", true, 0);
        Check(PriceCutRules.Start(none, manual, 5) == 0 && none.CutFrom == 0 && none.CutUntil == 0 && !manual.HeldId("a") && manual.HeldId("b") && !PriceCutRules.Possible(manual),
            "price cut: plans with no price of the plan's own to cut give a cut that is not on, and holds nothing");
        Check(PriceCutRules.Start(none, new FakePlans(), 5) == 0 && PriceCutRules.Start(null, plans, 5) == 0 && PriceCutRules.Start(none, null, 5) == 0 && PriceCutRules.Start(none, plans, 0) == 0,
            "price cut: no plans, no book or no day: nothing happens");

        // Every plan the player already held: the cut moves their prices and holds nothing new; the release puts nothing back.
        var allHeld = new DaySeriesBook();
        FakePlans mine = new FakePlans().Add("x", true, 2).Add("y", true, 4);
        Check(PriceCutRules.Start(allHeld, mine, 3) == 6 && allHeld.HeldByCut.Length == 0 && PriceCutRules.End(allHeld, mine) == 0 && mine.HeldId("x") && mine.HeldId("y"),
            "price cut: when every plan was held already, the release takes no hold off");

        // A hold the player puts on during the cut is theirs: the release does not take it off.
        var during = new DaySeriesBook();
        FakePlans three = new FakePlans().Add("p", false, 2).Add("q", false, 0);
        PriceCutRules.Start(during, three, 7);
        three.SetHeld(1, true);
        PriceCutRules.End(during, three);
        Check(!three.HeldId("p") && three.HeldId("q"), "price cut: a hold the player sets while the cut runs is not taken off at its end");

        // A save from before the record was kept (a cut with neither list): the release frees every plan the cut could have moved, so no price stays held for good.
        var old = DaySeriesStore.Read("v=1~l:50~d:5~c:40|54~s:~n:", out _);
        FakePlans oldPlans = new FakePlans().Add("m", true, 2).Add("n", true, 0);
        Check(old != null && old.CutFrom == 40 && old.HeldByCut.Length == 0 && PriceCutRules.End(old, oldPlans) == 1 && !oldPlans.HeldId("m") && oldPlans.HeldId("n"),
            "price cut: an old save's cut is released as it was (the plans it could have moved), and loads");
    }

    // ---------------------------------------------------------------- the day turn over 120 days

    private static void PriceCutRun()
    {
        // The day turn's part (PriceCutRules.Turn, what DaySeriesGame.Record calls) over 120 days with two cuts and a save written in the middle of the first.
        var book = new DaySeriesBook { Seed = DaySeriesRules.DefaultSeed };
        FakePlans plans = new FakePlans().Add("held", true, 3).Add("free", false, 2).Add("other", false, 4);
        bool cutDuring = true, heldAlways = true, freeAfter = true;
        int endedOn = 0, endedSecond = 0, savedEnds = 0;
        DaySeriesBook reloaded = null;
        FakePlans reloadedPlans = null;
        for (int day = 1; day <= 120; day++)
        {
            if (reloaded != null)
            {
                if (PriceCutRules.Turn(reloaded, reloadedPlans, day))
                {
                    savedEnds = day;
                }
            }
            if (PriceCutRules.Turn(book, plans, day))
            {
                if (endedOn == 0)
                {
                    endedOn = day;
                }
                else
                {
                    endedSecond = day;
                }
            }
            if (day == 20 || day == 70)
            {
                PriceCutRules.Start(book, plans, day);
            }
            if (day == 25)
            {
                // A save written mid-cut, read back into a fresh set of plans in the state the save would hold.
                reloaded = DaySeriesStore.Read(DaySeriesStore.Write(book), out _);
                reloadedPlans = new FakePlans().Add("held", true, 3).Add("free", true, 2).Add("other", true, 4);
            }
            if (day >= 20 && day < 34)
            {
                cutDuring &= DaySeriesRules.CutOn(book, day) && plans.HeldId("free") && plans.HeldId("other");
            }
            heldAlways &= plans.HeldId("held");
            if (day >= 34 && day < 70)
            {
                freeAfter &= !DaySeriesRules.CutOn(book, day) && !plans.HeldId("free") && !plans.HeldId("other");
            }
        }
        Check(cutDuring && endedOn == 34 && endedSecond == 84 && freeAfter && heldAlways,
            "price cut run: over 120 days the cut holds for 14 days and ends on its day, twice, and the plan the player held is held every day (ended " + endedOn + ", " + endedSecond + ")");
        Check(reloaded != null && savedEnds == 34 && !reloadedPlans.HeldId("free") && !reloadedPlans.HeldId("other") && reloadedPlans.HeldId("held"),
            "price cut run: a save written on day 25, mid-cut, ends the same cut on the same day (" + savedEnds + ") and leaves the player's hold");
    }

    // ---------------------------------------------------------------- 4. the rivals answer the player in kind

    private static void RivalAnswers()
    {
        // The same days, the same seed: the rivals make more price moves while the player's price cut is on, and more pieces in the paper while a campaign is.
        int quiet = 0, cut = 0, pressQuiet = 0, pressCampaign = 0, otherQuiet = 0, otherCut = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            for (int mode = 0; mode < 3; mode++)
            {
                var f = RivalRules.Open(seed, 1, 1000);
                RivalRules.Activate(f, 3, 1, 1000);
                RivalWorld w = RivalWorldOf(3000000000L);
                w.PlayerCut = mode == 1;
                w.PlayerCampaign = mode == 2;
                for (int d = 10; d < 130; d++)
                {
                    RivalMove m = RivalRules.Announce(f, w, d, RivalCatalogue.Ruthless, true);
                    if (m == null)
                    {
                        continue;
                    }
                    if (mode == 0)
                    {
                        quiet += m.Kind == RivalMoveKind.PriceRise ? 1 : 0;
                        pressQuiet += m.Kind == RivalMoveKind.Press ? 1 : 0;
                        otherQuiet++;
                    }
                    else if (mode == 1)
                    {
                        cut += m.Kind == RivalMoveKind.PriceRise ? 1 : 0;
                        otherCut++;
                    }
                    else
                    {
                        pressCampaign += m.Kind == RivalMoveKind.Press ? 1 : 0;
                    }
                    m.State = RivalMoveState.Over;
                }
            }
        }
        Check(cut > quiet && pressCampaign > pressQuiet && quiet > 0,
            "rival answers: a rival answers the player's price cut with price moves of their own (" + quiet + " without, " + cut + " with) and a Flare campaign with pieces in the paper (" + pressQuiet + ", " + pressCampaign + ")");
        var t = RivalRules.Open(7, 1, 1000);
        RivalRules.Activate(t, 3, 1, 1000);
        Tycoon who = t.Tycoons[0];
        var plain = new RivalWorld();
        var answering = new RivalWorld { PlayerCut = true, PlayerCampaign = true };
        int basePrice = RivalRules.Weight(who, RivalMoveKind.PriceRise, plain);
        Check(RivalRules.Weight(who, RivalMoveKind.PriceRise, answering) == basePrice * RivalCatalogue.AnswerFactor
              && RivalRules.Weight(who, RivalMoveKind.Press, answering) == RivalRules.Weight(who, RivalMoveKind.Press, plain) * RivalCatalogue.AnswerFactor
              && RivalRules.Weight(who, RivalMoveKind.Outlet, answering) == RivalRules.Weight(who, RivalMoveKind.Outlet, plain),
            "rival answers: only the price move and the paper are made likelier, by the one factor, and no other kind is");

        // The forecast of a Flare campaign and of the cut: nothing without takings, and a sum that grows with each figure.
        Check(DaySeriesRules.CampaignForecast(2000, 10, 7) == 1400 && DaySeriesRules.CampaignForecast(0, 10, 7) == 0 && DaySeriesRules.CampaignForecast(2000, 0, 7) == 0
              && DaySeriesRules.CampaignForecast(2000, 10, 0) == 0 && DaySeriesRules.CampaignForecast(2000, 20, 7) > DaySeriesRules.CampaignForecast(2000, 10, 7),
            "campaign forecast: the extra takings are the share a campaign adds of what the business takes a day, for each of its days");
        Check(DaySeriesRules.CutFootfall(new DaySeriesBook { CutFrom = 5, CutUntil = 19 }, 10) == DaySeriesRules.CutCustomers, "price cut: its forecast of customers is the figure the footfall uses");
    }

    // ---------------------------------------------------------------- 3. where it went, from the saved ledger

    private static void LedgerWhere()
    {
        var book = new DaySeriesBook { Seed = DaySeriesRules.DefaultSeed };
        var net = new long[CashCategory.Count];
        // The ledger begins on day 40 (a save from earlier has no days before): 20 days in, 10 of the 30 days are not there, and are not zero.
        for (int d = 40; d <= 59; d++)
        {
            book.Set(DaySeriesCatalogue.Category(CashCategory.Takings), d, 1000);
            book.Set(DaySeriesCatalogue.Category(CashCategory.Wages), d, -400);
            book.Set(DaySeriesCatalogue.Category(CashCategory.Rent), d, d % 2 == 0 ? -100 : 0);
        }
        int have = FinanceRules.CategoryNet(book, 59, DaySeriesRules.CashWindow, net, out int first);
        Check(have == 20 && first == 40 && net[CashCategory.Takings] == 20000 && net[CashCategory.Wages] == -8000 && net[CashCategory.Rent] == -1000,
            "where it went: the saved ledger sums its 20 days, the days before it are not days and not zeros (" + have + " from day " + first + ")");
        // A month later the window has moved on: only the last 30 days count, and the sums are the later days'.
        for (int d = 60; d <= 100; d++)
        {
            book.Set(DaySeriesCatalogue.Category(CashCategory.Takings), d, 500);
        }
        have = FinanceRules.CategoryNet(book, 100, DaySeriesRules.CashWindow, net, out first);
        Check(have == 30 && first == 71 && net[CashCategory.Takings] == 15000 && net[CashCategory.Wages] == 0, "where it went: a month on, the 30 newest days only (" + have + " from day " + first + ")");
        Check(FinanceRules.CategoryNet(book, 10, DaySeriesRules.CashWindow, net, out first) == 0 && first == 0 && FinanceRules.CategoryNet(null, 10, 30, net, out first) == 0,
            "where it went: before the ledger began, or with no book: no days, so the page says not yet");
        string line = DaySeriesStore.Write(book);
        DaySeriesBook back = DaySeriesStore.Read(line, out _);
        have = FinanceRules.CategoryNet(back, 100, DaySeriesRules.CashWindow, net, out first);
        Check(back != null && have == 30 && net[CashCategory.Takings] == 15000, "where it went: the ledger reads the same from a save");
    }

    // ---------------------------------------------------------------- 2. the last rise and the rate review

    private static void RiseAndReview()
    {
        var file = new CreditFile();
        file.NoteRise(12, 40);
        file.NoteRise(12, 25);
        Check(file.RiseDay == 12 && file.RiseExtra == 65, "last rise: two causes on one day add up (" + file.RiseExtra + ")");
        file.NoteRise(40, 10);
        file.NoteRise(0, 99);
        Check(file.RiseDay == 40 && file.RiseExtra == 10, "last rise: a later rise replaces the earlier, and no day is not a rise");
        string text = CreditStore.Write(file);
        CreditFile back = CreditStore.Read(text, out _);
        Check(back != null && back.RiseDay == 40 && back.RiseExtra == 10, "last rise: kept in the credit line");
        // A line from before the rise was kept (header without the two fields) loads with none.
        int h = text.IndexOf("~h:", StringComparison.Ordinal);
        int d = text.IndexOf("~d:", StringComparison.Ordinal);
        string[] fields = text.Substring(h + 3, d - h - 3).Split('|');
        string oldText = text.Substring(0, h + 3) + string.Join("|", fields, 0, fields.Length - 2) + text.Substring(d);
        CreditFile older = CreditStore.Read(oldText, out _);
        Check(older != null && older.RiseDay == 0 && older.RiseExtra == 0, "last rise: a credit line from before it loads with no rise noted");
        // What a rise of a quarter point adds a day on the accounts that follow the bank rate: the overdraft and the card, not a finance.
        var accounts = new List<CreditAccount>
        {
            new CreditAccount { Kind = ProductKind.Overdraft, Owed = 100000 },
            new CreditAccount { Kind = ProductKind.CreditCard, Owed = 50000 },
            new CreditAccount { Kind = ProductKind.CarFinance, Owed = 900000 },
            new CreditAccount { Kind = ProductKind.CreditCard, Owed = 70000, Closed = true }
        };
        int year = 60;
        int expect = AccountRules.InterestADay(100000, 25, year) + AccountRules.InterestADay(50000, 25, year);
        Check(AccountRules.RiseCost(accounts, 25, year) == expect && expect > 0 && AccountRules.RiseCost(accounts, 0, year) == 0 && AccountRules.RiseCost(null, 25, year) == 0,
            "last rise: a rise costs a day what its points come to on the overdraft and the card alone (" + expect + ")");
    }

    // ---------------------------------------------------------------- 5. the business loan's month of takings

    private static void BusinessRule()
    {
        Check(!ProductTerms.HistoryOk(ProductCatalogue.BusinessDaysOnRecord - 1) && ProductTerms.HistoryOk(ProductCatalogue.BusinessDaysOnRecord) && ProductCatalogue.BusinessDaysOnRecord == 30,
            "business loan: a bank wants a month of takings on record");
        int pay = 100;
        Check(ProductTerms.CoverOk(pay * ProductCatalogue.BusinessCoverTimes, pay) && !ProductTerms.CoverOk(pay * ProductCatalogue.BusinessCoverTimes - 1, pay) && ProductTerms.CoverOk(1, 0) && !ProductTerms.CoverOk(0, 0),
            "business loan: takings must cover the payment 20 times");
        int most = ProductTerms.CoverMost(100000, 300000, 50);   // covers: 20 x 50 = 1,000
        int scaled = ProductTerms.CoverMost(100000, 1000, 500);  // needs 10,000 of takings for 500 a day; has 1,000: a tenth of the loan
        // Enough days and a profit, but takings that do not cover even the least loan's payment: the refusal is the cover's, not the days'.
        int least = CreditCatalogue.LoanLeast;
        int capped = ProductTerms.CoverMost(least * 10, 50, 50);   // takings of 50 against a payment of 50 a day: a twentieth of the loan, under the least
        Check(ProductTerms.BusinessVerdict(34, true, capped) == ProductAnswer.NoCover && ProductTerms.BusinessVerdict(12, true, least * 10) == ProductAnswer.NoSecurity
              && ProductTerms.BusinessVerdict(34, false, 0) == ProductAnswer.NoSecurity && ProductTerms.BusinessVerdict(34, true, least) == ProductAnswer.Yes,
            "business loan: a business with the days but too little cover gets the cover reason, one without the days or a profit gets the days' (" + capped + ")");
        Check(ProductTerms.CoverTimes(1000, 50) == 20 && ProductTerms.CoverTimes(1999, 100) == 19 && ProductTerms.CoverTimes(1000, 0) == 0 && ProductTerms.CoverTimes(0, 5) == 0,
            "business loan: the refusal quotes how many whole times the takings cover the payment");
        Check(most == 100000 && scaled == 10000 && ProductTerms.CoverMost(100000, 0, 500) == 0 && ProductTerms.CoverMost(0, 1000, 5) == 0,
            "business loan: a loan the takings do not cover is cut to what they do (" + scaled + ")");
    }
}
