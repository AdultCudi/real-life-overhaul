using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Markets;

// A player's company on the city's market (Scripts/Markets/Listing, MarketEngine.Own): its books, what it takes to come
// to the market, the fees, the prospectus and the book that prices it, the first day; its price from its real books, the
// reserve that keeps the other holders' part of each day's profit, results and dividends; what its founder asks for and
// what stops them dealing on what they know; the shareholders' meetings, investors who press the board and a bid for it;
// the tax on dividends; its line in the save and on the tape; a Going Public guest's own company set aside and back. One
// worked example of the book is printed with "--listing-example".
internal static partial class Program
{
    private const int LYear = 60;
    private const ulong LMe = 0x5EED0001UL, LGuest = 0x5EED0002UL;

    /// <summary>The city and the world for a market with a player's company in it: the player's books a day at a time, as the game side hands them over.</summary>
    private sealed class OwnWorld : IMarketWorld
    {
        public ulong Seed;
        public int Secrets;
        public MarketEnv City = MarketEnv.Quiet();
        public readonly MarketWallet Purse = new MarketWallet { Money = 100000000 };
        public readonly List<MarketNews> Heard = new List<MarketNews>();
        public Func<MarketState> State;
        public double Revenue = 50000, Profit = 8000;   // the company's takings and profit a day
        public Func<int, double> ProfitOn;               // ...or by the statement day
        public int Standing = 50, Press, ScandalOn = -1;
        public long Debt = 200000;
        public bool Company = true;                      // the player has a company on the market or on its way

        public OwnWorld(ulong seed)
        {
            Seed = seed;
        }

        public ulong NewSecret()
        {
            Secrets++;
            return MarketRandom.Mix64(Seed * 0x9E3779B97F4A7C15UL + (ulong)Secrets * 0xD1B54A32D192ED03UL);
        }

        public MarketEnv Env(int day)
        {
            MarketEnv e = City;
            e.Supply = (int[])City.Supply.Clone();
            e.TradeOf = (int[])City.TradeOf.Clone();
            e.TradeSales = (int[])City.TradeSales.Clone();
            MarketState s = State != null ? State() : null;
            if (Company && s != null)
            {
                e.OwnFrom = s.BusinessDay;
                e.OwnDays = Math.Max(0, day - s.BusinessDay);
                double rev = 0, profit = 0;
                for (int d = e.OwnFrom; d < e.OwnFrom + e.OwnDays; d++)
                {
                    rev += Revenue;
                    profit += ProfitOn != null ? ProfitOn(d) : Profit;
                }
                e.OwnRev = (long)rev;
                e.OwnProfit = (long)profit;
                e.OwnStanding = Standing;
                e.OwnPress = Press;
                e.OwnScandal = ScandalOn >= e.OwnFrom && ScandalOn < e.OwnFrom + Math.Max(1, e.OwnDays) ? 1 : 0;
                e.OwnDebt = Debt;
            }
            return e;
        }

        public IMarketWallet Wallet => Purse;
        public int CommissionLeast => MarketCatalogue.CommissionLeastAtStart;

        public void Said(in MarketNews news)
        {
            if (Heard.Count < 100000)
            {
                Heard.Add(news);
            }
        }

        public void Filled(Order order, Stock stock, long qty, int cents)
        {
        }

        public void Paid(TradeKind kind, Stock stock, long amount)
        {
        }

        public void Called(long amount, int byDay)
        {
        }

        public int Count(MarketNewsKind kind)
        {
            int n = 0;
            for (int i = 0; i < Heard.Count; i++)
            {
                n += Heard[i].Kind == kind ? 1 : 0;
            }
            return n;
        }

        public MarketNews Last(MarketNewsKind kind)
        {
            for (int i = Heard.Count - 1; i >= 0; i--)
            {
                if (Heard[i].Kind == kind)
                {
                    return Heard[i];
                }
            }
            return default(MarketNews);
        }
    }

    // A year and more of books before a day: every statement day of the quarters before it, at so much a day.
    private static void LBooks(ListingFile f, int day, double revenue, double profit, int quarters, Func<int, double> profitOn = null)
    {
        int now = MarketCatalogue.QuarterOf(day, LYear);
        int from = MarketCatalogue.QuarterStart(now - quarters, LYear);
        for (int d = Math.Max(1, from); d < day; d++)
        {
            ListingBook.AddDay(f, d, (long)revenue, (long)(profitOn != null ? profitOn(d) : profit), LYear);
        }
    }

    // A market on a day with the player's company priced on the next open (its book closing), from books of so much a day.
    private static MarketEngine LMarket(ulong seed, int day, OwnWorld world, int floatBp, bool test = false, int walk = 1)
    {
        MarketState state = MarketSetup.Open(seed, day, 0, LYear, world.Env(day), seed, null, null, null);
        state.Me = LMe;
        world.State = () => state;
        var engine = new MarketEngine(state, new MarketTape(seed), world);
        ListingFile f = state.Listing;
        f.Name = "Harbour Lights Group";
        f.Ticker = ListingRules.TickerOf(f.Name);
        f.Sector = (int)MarketSector.Restaurants;
        f.Test = test;
        LBooks(f, day, world.Revenue, world.Profit, 8);
        f.AuditDay = day - 1;
        f.Findings = 0;
        double worth = ListingBook.Worth(f, LMe, day, LYear, state.Env, world.Debt);
        f.OldShares = ListingBook.FounderShares(worth, 1.0);
        f.NewShares = ListingBook.NewShares(f.OldShares, floatBp);
        ListingBook.Prospectus(f, floatBp, ListingBook.FairCents(f, LMe, day, LYear, state.Env, world.Debt), walk);
        f.Stage = ListingStage.Building;
        f.BookFrom = day;
        f.BookEnd = MarketTrading.NextTradingDay(day);
        return engine;
    }

    // The player's company listed: the book priced and its first day played.
    private static MarketEngine LListed(ulong seed, OwnWorld world, int floatBp, out Stock own, bool test = false)
    {
        int day = TradingOnOrAfter(200);
        MarketEngine m = LMarket(seed, day, world, floatBp, test);
        RunTo(m, MarketTrading.MarketDays(day, 2));
        own = m.MineListed();
        return m;
    }

    private static void Listing()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        ListingBooksChecks();
        ListingRequirementChecks();
        ListingFeeChecks();
        ListingOfferChecks();
        ListingBookPriceChecks();
        ListingValuationChecks();
        ListingFirstDayChecks();
        ListingReserveChecks();
        ListingSpikeChecks();
        ListingResultsChecks();
        ListingClosedChecks();
        ListingDealChecks();
        ListingCompanyActionChecks();
        ListingVoteChecks();
        ListingMeetingChecks();
        ListingActivistAndBidChecks();
        ListingTaxChecks();
        ListingAuditEventChecks();
        ListingProceedsChecks();
        ListingStoreChecks();
        ListingTapeChecks();
        ListingSessionChecks();
        ListingDeterminism();
        ListingCost();
        Console.WriteLine("  a player's company on the market: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the books

    private static void ListingBooksChecks()
    {
        var f = new ListingFile();
        for (int d = 1; d <= 15 * 14; d++)
        {
            ListingBook.AddDay(f, d, 1000, d % 15 == 0 ? -50 : 100, LYear);
        }
        Check(f.Books.Count == 12, "the books keep twelve quarters (" + f.Books.Count + ")");
        bool ordered = true;
        for (int i = 1; i < f.Books.Count; i++)
        {
            ordered &= f.Books[i].Quarter == f.Books[i - 1].Quarter + 1;
        }
        Check(ordered && f.Books[f.Books.Count - 1].Quarter == MarketCatalogue.QuarterOf(15 * 14, LYear), "the books' quarters run in order to the last day's");
        Check(f.Books[5].Days == 15 && f.Books[5].Revenue == 15000 && f.Books[5].Profit == 14 * 100 - 50, "a quarter adds up its days: takings, profit and days");
        int n = ListingBook.Last(f, 15 * 14 + 1, LYear, 4, out long revenue, out long profit, out int profitable);
        Check(n == 4 && revenue == 60000 && profit == 4 * (14 * 100 - 50) && profitable == 4, "the last four complete quarters: " + n + ", " + revenue + ", " + profit);
        // A day out of order (a statement entered late) goes into its own quarter.
        ListingBook.AddDay(f, 15 * 14 - 20, 7, 7, LYear);
        Check(f.Books[f.IndexOf(MarketCatalogue.QuarterOf(15 * 14 - 20, LYear))].Revenue == 15007, "a late statement goes into its own quarter");
        var g = new ListingFile();
        Check(ListingBook.Last(g, 100, LYear, 4, out _, out _, out _) == 0, "no books: no quarters");
        ListingRules.Books(g, 100, LYear, out double rY, out double nY, out double rQ, out double nQ, out double gr);
        Check(rY == 0 && nY == 0 && rQ == 0 && nQ == 0 && gr == 0, "no books: nothing to value");
        // Growth from eight quarters: takings 10% up on the year before.
        var h = new ListingFile();
        int day = 15 * 12 + 1;
        int now = MarketCatalogue.QuarterOf(day, LYear);
        for (int d = MarketCatalogue.QuarterStart(now - 8, LYear); d < day; d++)
        {
            bool recent = MarketCatalogue.QuarterOf(d, LYear) >= now - 4;
            ListingBook.AddDay(h, d, recent ? 1100 : 1000, 100, LYear);
        }
        ListingRules.Books(h, day, LYear, out rY, out nY, out rQ, out nQ, out gr);
        Check(Math.Abs(gr - Math.Log(1.1)) < 1e-9 && Math.Abs(rY - 1100) < 1e-9, "growth is the year's takings against the year before's (" + gr.ToString("F4") + ")");
        Check(ListingRules.AfterPay(1000, 500) == 950 && ListingRules.AfterPay(-1000, 500) == -1000, "the founder's pay is a share of a profit and nothing of a loss");
    }

    // ---------------------------------------------------------------- what it takes

    private static void ListingRequirementChecks()
    {
        var reqs = new Requirement[ListingBook.NeedCount];
        int day = TradingOnOrAfter(300);
        var f = new ListingFile();
        LBooks(f, day, 30000, 6000, 8);
        var facts = new ListingFacts { FirstDay = day - 2 * LYear - 1 };
        bool all = ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs);
        Check(!all && ListingBook.ReadyForAdvisers(reqs) && !reqs[ListingBook.NeedAudit].Met, "a big enough company of two years with no audit is ready for advisers but not for the market");
        f.AuditDay = day - 3;
        Check(ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs), "with a clean audit every requirement is met");
        f.Findings = ListingBook.CashOffBooks;
        Check(!ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs) && !reqs[ListingBook.NeedAudit].Met, "an audit that found cash off the books does not do");
        f.Findings = ListingBook.BribesOut;
        Check(ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs), "a bribe that came out is disclosed, and does not stop a listing");
        f.Findings = 0;
        f.AuditDay = day - LYear / 4 - 1;
        Check(!ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs), "an audit older than a quarter of a year no longer does");
        f.AuditDay = day - 3;
        facts.FirstDay = day - 2 * LYear + 5;
        Check(!ListingBook.Requirements(f, facts, day, LYear, 1.0, reqs) && !reqs[ListingBook.NeedYears].Met, "under two years since the first business: not yet");
        facts.FirstDay = day - 2 * LYear - 1;
        Check(!ListingBook.Requirements(f, facts, day, LYear, 2.0, reqs) && !reqs[ListingBook.NeedTurnover].Met && reqs[ListingBook.NeedTurnover].Needed == 3000000,
            "the least takings are at today's prices (twice the start's: " + reqs[ListingBook.NeedTurnover].Needed + ")");
        var small = new ListingFile { AuditDay = day - 3 };
        LBooks(small, day, 30000, 500, 8);
        Check(!ListingBook.Requirements(small, facts, day, LYear, 1.0, reqs) && !reqs[ListingBook.NeedProfit].Met, "too little profit: not yet");
        var patchy = new ListingFile { AuditDay = day - 3 };
        LBooks(patchy, day, 30000, 6000, 8, d => MarketCatalogue.QuarterOf(d, LYear) % 2 == 0 ? 15000 : -100);
        ListingBook.Requirements(patchy, facts, day, LYear, 1.0, reqs);
        Check(!reqs[ListingBook.NeedProfitable].Met && reqs[ListingBook.NeedProfitable].Has == 2, "two quarters of four in profit is not enough (" + reqs[ListingBook.NeedProfitable].Has + ")");
        var young = new ListingFile { AuditDay = day - 3 };
        LBooks(young, day, 30000, 6000, 3);
        Check(!ListingBook.Requirements(young, facts, day, LYear, 1.0, reqs) && reqs[ListingBook.NeedQuarters].Has == 3, "three quarters of books is not enough");
        int findings = ListingBook.Findings(new ListingFacts { BackTaxes = true, LateReturns = 2, CashOffBooks = true, Bankrupt = true, BribesOut = 1 });
        Check(findings == (ListingBook.Qualifying | ListingBook.BribesOut), "the audit's findings, each from its fact");
        Check(ListingBook.Findings(new ListingFacts()) == 0, "nothing found in a clean company");
    }

    // ---------------------------------------------------------------- the fees

    private static void ListingFeeChecks()
    {
        Check(ListingBook.Retainer(0, 1.0) == ListingCatalogue.RetainerLeastAtStart && ListingBook.Retainer(1e12, 1.0) == ListingCatalogue.RetainerMostAtStart, "the retainer within its least and most");
        Check(ListingBook.Retainer(20000000, 1.0) == 100000, "the retainer: half a per cent of 20 million is 100,000");
        Check(ListingBook.Retainer(20000000, 2.0) == 100000 && ListingBook.Retainer(0, 2.0) == 2 * ListingCatalogue.RetainerLeastAtStart, "its least scales with prices, its share does not");
        Check(ListingBook.AuditFee(10000000, 1.0) == 30000 && ListingBook.AuditFee(100, 1.0) == ListingCatalogue.AuditLeastAtStart && ListingBook.AuditFee(long.MaxValue / 100000, 1.0) == ListingCatalogue.AuditMostAtStart,
            "the auditors: 0.3% of the year's takings within its least and most");
        Check(ListingBook.Underwriting(1000000, 1.0) == 50000 && ListingBook.Underwriting(30000000, 1.0) == 1200000, "the underwriters: 5%, and 4% of a large offer");
        Check(ListingBook.ListingFee(1000, 1.0) == ListingCatalogue.ListingFeeLeastAtStart && ListingBook.ListingFee(500000000, 1.0) == 500000, "the exchange: 0.1% at least its least");
        Check(ListingBook.YearlyFee(1000, 1.0) == ListingCatalogue.YearlyFeeLeastAtStart && ListingBook.YearlyFee(500000000, 1.0) == 250000, "the yearly fee: 0.05% at least its least");
    }

    // ---------------------------------------------------------------- the offer

    private static void ListingOfferChecks()
    {
        long founder = ListingBook.FounderShares(25000000, 1.0);
        Check(founder == 2500000, "the founder's shares make one worth about ten (" + founder + ")");
        Check(ListingBook.FounderShares(1, 1.0) == ListingCatalogue.FounderSharesLeast, "never fewer than the least");
        Check(ListingBook.FounderShares(25000000, 2.0) == 1250000, "at twice the prices, a share worth twice as much");
        bool exact = true;
        for (int i = 0; i < ListingCatalogue.FloatSteps.Length; i++)
        {
            int bp = ListingCatalogue.FloatSteps[i];
            long fresh = ListingBook.NewShares(founder, bp);
            double offered = fresh / (double)(founder + fresh);
            exact &= Math.Abs(offered - bp / 10000.0) < 1e-5;
        }
        Check(exact, "every float on offer is the share of the company sold, to a hundred-thousandth");
        Check(ListingBook.FloatStep(2400) == 2500 && ListingBook.FloatStep(100) == 1000 && ListingBook.FloatStep(9999) == 6000, "a float asked for is the nearest one on offer");
        var f = new ListingFile();
        ListingBook.Prospectus(f, 2500, 1000, 0);
        int mid = (int)Math.Round(1000 * (1 - ListingCatalogue.IpoDiscount));
        Check(f.LowCents % ListingCatalogue.PriceStepCents == 0 && f.HighCents % ListingCatalogue.PriceStepCents == 0 && f.LowCents < mid && f.HighCents > mid,
            "the range is in steps either side of the discounted price (" + f.LowCents + " to " + f.HighCents + ")");
        Check(Math.Abs(f.LowCents - mid * (1 - ListingCatalogue.RangeSpread)) <= ListingCatalogue.PriceStepCents && Math.Abs(f.HighCents - mid * (1 + ListingCatalogue.RangeSpread)) <= ListingCatalogue.PriceStepCents,
            "the range is the set share either side");
        Check(f.WalkCents == f.LowCents, "the least price taken is the range's bottom unless set");
        Check(ListingRules.TickerOf("Harbour Lights Group") == "HLG" && ListingRules.TickerOf("Zed") == "ZED" && ListingRules.TickerOf("") == "XXX" && ListingRules.TickerOf("Big Old Rusty Cars Inc") == "BORC",
            "a ticker from a name's words: three or four capitals");
    }

    // ---------------------------------------------------------------- the book

    private static void ListingBookPriceChecks()
    {
        var f = new ListingFile { LowCents = 900, HighCents = 1050, FloatBp = 2500 };
        int a = ListingBook.Price(f, 12345, 1010, 0, 50, out double keenA);
        int b = ListingBook.Price(f, 12345, 1010, 0, 50, out double keenB);
        Check(a == b && keenA == keenB, "the book comes to the same price from the same secret (a load cannot draw it again)");
        double sum = 0;
        int least = int.MaxValue, most = 0, n = 4000;
        for (int i = 0; i < n; i++)
        {
            int p = ListingBook.Price(f, MarketRandom.Mix64((ulong)i + 99), 1010, 0, 50, out double keen);
            sum += keen;
            least = Math.Min(least, p);
            most = Math.Max(most, p);
        }
        double expectKeen = -f.FloatBp / 5000.0 * ListingCatalogue.DemandFloat;
        Check(Math.Abs(sum / n - expectKeen) < 0.006, "on average the book is as keen as its figures say (" + (sum / n).ToString("F4") + " against " + expectKeen.ToString("F4") + ")");
        Check(least >= f.LowCents * (1 - ListingCatalogue.PriceBeyondRange) - ListingCatalogue.PriceStepCents && most <= f.HighCents * (1 + ListingCatalogue.PriceBeyondRange),
            "never further than the set share outside the range (" + least + " to " + most + ")");
        Check(least % ListingCatalogue.PriceStepCents == 0 && most % ListingCatalogue.PriceStepCents == 0, "every price a whole number of steps");
        int up = ListingBook.Price(f, 777, 1010, 1500, 50, out _), flat = ListingBook.Price(f, 777, 1010, 0, 50, out _), down = ListingBook.Price(f, 777, 1010, -1500, 50, out _);
        Check(up >= flat && flat >= down && up > down, "a market up over the month makes a keener book (" + down + ", " + flat + ", " + up + ")");
        int famous = ListingBook.Price(f, 777, 1010, 0, 100, out _), unknown = ListingBook.Price(f, 777, 1010, 0, 0, out _);
        Check(famous > unknown, "a founder of high Standing makes a keener book");
        f.Findings = ListingBook.BribesOut;
        int tainted = ListingBook.Price(f, 777, 1010, 0, 50, out _);
        Check(tainted < flat, "a bribe disclosed makes a cooler one");
        f.Findings = 0;
        f.FloatBp = 6000;
        Check(ListingBook.Price(f, 777, 1010, 0, 50, out _) <= flat, "more of the company offered makes a cooler one");
        Check(ListingBook.Price(f, 777, 1010, 50000, 50, out _) == ListingBook.Price(f, 777, 1010, 1500, 50, out _), "the month's move counts only up to its most");
    }

    // ---------------------------------------------------------------- the worth from the books

    private static void ListingValuationChecks()
    {
        int day = TradingOnOrAfter(300);
        MarketEnv env = MarketEnv.Quiet();
        ListingFile Of(double revenue, double profit, int quarters, Func<int, double> on = null)
        {
            var f = new ListingFile { Name = "Test Holdings", Ticker = "TH", Sector = (int)MarketSector.Stores, OldShares = 1000000 };
            LBooks(f, day, revenue, profit, quarters, on);
            return f;
        }
        double Fair(ListingFile f)
        {
            return MarketValuation.Fair(ListingRules.Provisional(f, LMe, day, LYear, env, 0), env);
        }
        double base1 = Fair(Of(50000, 8000, 8)), base2 = Fair(Of(100000, 16000, 8));
        Check(base1 > 0 && Math.Abs(base2 / base1 - 2.0) < 0.08, "twice the business is worth about twice as much (" + (base2 / base1).ToString("F3") + ")");
        Stock s = ListingRules.Provisional(Of(50000, 8000, 8), LMe, day, LYear, env, 0);
        double eps = MarketValuation.Earnings(s, env);
        double pe = base1 / eps;
        Check(pe > 6 && pe < 30, "a steady company is valued at an ordinary multiple of its earnings (" + pe.ToString("F1") + ")");
        Check(Math.Abs(s.Rev - 50000 * LYear) < 1 && Math.Abs(s.Own.NiY - 8000 * 0.95) < 1e-6, "its turnover a year and its profit after fair pay as the books have them");
        // Only the last quarter better (half as much again): a patch, worth much less than half again.
        double spike = Fair(Of(50000, 8000, 8, d => MarketCatalogue.QuarterOf(d, LYear) == MarketCatalogue.QuarterOf(day, LYear) - 1 ? 12000 : 8000));
        Check(spike > base1 && spike < base1 * 1.12, "a last quarter half as good again lifts the worth only a little (" + (spike / base1).ToString("F3") + ")");
        // A last year better than the two years' mean is valued at the mean: a year made to look good does not pay.
        double year = Fair(Of(50000, 8000, 8, d => MarketCatalogue.QuarterOf(d, LYear) >= MarketCatalogue.QuarterOf(day, LYear) - 4 ? 14000 : 2000));
        double steady = Fair(Of(50000, 8000, 8));
        Check(year <= steady * 1.10, "a last year far better than the one before is not taken at its word (" + (year / steady).ToString("F3") + ")");
        // A business losing money is worth little, never nothing.
        double losing = Fair(Of(50000, -3000, 8));
        Check(losing > 0 && losing < base1 * 0.2, "a business losing money is worth little but something (" + (losing / base1).ToString("F3") + ")");
        // Debt lowers the worth of the shares.
        double geared = MarketValuation.Fair(ListingRules.Provisional(Of(50000, 8000, 8), LMe, day, LYear, env, 5000000), env);
        Check(geared < base1, "debt lowers what a share is worth");
        // A higher bank rate lowers it too, and the council's policies move it as for the city's companies in its sector.
        MarketEnv dear = env;
        dear.RateBp = 800;
        Check(MarketValuation.Fair(ListingRules.Provisional(Of(50000, 8000, 8), LMe, day, LYear, dear, 0), dear) < base1, "a higher bank rate lowers its worth");
        // The worth the prospectus prints is with every share there will be.
        ListingFile g = Of(50000, 8000, 8);
        int one = ListingBook.FairCents(g, LMe, day, LYear, env, 0);
        g.NewShares = g.OldShares;
        int half = ListingBook.FairCents(g, LMe, day, LYear, env, 0);
        Check(Math.Abs(one - 2 * half) <= 2, "with twice the shares a share is worth half (" + one + ", " + half + ")");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--listing-example") >= 0)
        {
            ListingFile x = Of(50000, 8000, 8);
            double worth = ListingBook.Worth(x, LMe, day, LYear, env, 200000);
            x.OldShares = ListingBook.FounderShares(worth, 1.0);
            x.NewShares = ListingBook.NewShares(x.OldShares, 2500);
            ListingBook.Prospectus(x, 2500, ListingBook.FairCents(x, LMe, day, LYear, env, 200000), 0);
            int price = ListingBook.Price(x, 4242, ListingRules.IdFor(LMe), 0, 50, out double keen);
            Console.WriteLine("Listing example: takings 50,000 and profit 8,000 a day for two years; worth " + worth.ToString("N0") + "; founder " + x.OldShares.ToString("N0") + " shares, "
                              + x.NewShares.ToString("N0") + " offered (25%); a share worth " + (x.FairCents / 100.0).ToString("F2") + "; range " + (x.LowCents / 100.0).ToString("F2") + " to "
                              + (x.HighCents / 100.0).ToString("F2") + "; the book (keen " + keen.ToString("F3") + ") prices it at " + (price / 100.0).ToString("F2") + ", raising "
                              + (x.NewShares * price / 100.0).ToString("N0") + " before the underwriters' "
                              + ListingBook.Underwriting(x.NewShares * price / 100, 1.0).ToString("N0") + ".");
        }
    }

    // ---------------------------------------------------------------- the first day

    private static void ListingFirstDayChecks()
    {
        var world = new OwnWorld(31);
        int day = TradingOnOrAfter(200);
        MarketEngine m = LMarket(31, day, world, 2500);
        ListingFile f = m.State.Listing;
        long before = world.Purse.Money;
        RunTo(m, f.BookEnd, MarketCatalogue.PhaseOpen);
        Check(f.Stage == ListingStage.Priced && f.PriceCents > 0 && world.Count(MarketNewsKind.OwnPriced) == 1, "the book is priced at its last open (" + f.Stage + ", " + f.PriceCents + ")");
        Check(f.ListDay == MarketTrading.NextTradingDay(f.BookEnd), "it trades from the next market day");
        Check(m.MineListed() == null && world.Purse.Money == before, "nothing is on the market or paid before its first day");
        RunTo(m, f.ListDay, MarketCatalogue.PhaseOpen);
        Stock own = m.MineListed();
        Check(own != null && own.Own != null && f.Stage == ListingStage.Listed && own.Id == f.StockId, "on its first day it is on the market as the player's company");
        if (own == null)
        {
            return;
        }
        Check(own.Own.Founder == f.OldShares && own.Shares == f.OldShares + f.NewShares && Math.Abs(own.Own.StakeBp(own) - 7500) <= 1, "the founder keeps three quarters (" + own.Own.StakeBp(own) + " bp)");
        long raised = MarketTrading.Value(f.NewShares, f.PriceCents, false);
        long fees = ListingBook.Underwriting(raised, 1.0) + ListingBook.ListingFee(own.Shares * (f.PriceCents / 100.0), 1.0);
        Check(world.Purse.Money - before == raised - fees, "the offer's money comes in less the underwriters and the exchange (" + (world.Purse.Money - before) + " against " + (raised - fees) + ")");
        Check(f.Raised == raised - ListingBook.Underwriting(raised, 1.0) && f.Fees == fees, "what was raised and paid is kept for the page");
        Check(own.ListedDay == f.ListDay && own.Results.Count == 4, "its first day, and the last four quarters as its past results (" + own.Results.Count + ")");
        Check(own.Own.LockDay == f.ListDay + LYear / ListingCatalogue.LockUpYearShare && own.Own.Board && own.Own.PayBp == ListingCatalogue.PayRefBp, "the lock-up, the board and the fair pay to begin with");
        Check(Math.Abs(own.Open - f.PriceCents) < f.PriceCents * 0.6, "its first price is near the offer's (" + own.Open + " against " + f.PriceCents + ")");
        // A walk-away price over the book's: called off, the audit kept.
        var w2 = new OwnWorld(31);
        MarketEngine pulled = LMarket(31, day, w2, 2500, false, 100000000);
        long had = w2.Purse.Money;
        RunTo(pulled, MarketTrading.MarketDays(day, 3));
        Check(pulled.State.Listing.Stage == ListingStage.Audited && pulled.MineListed() == null && w2.Count(MarketNewsKind.OwnPulled) == 1 && w2.Purse.Money == had,
            "a price under the least the founder takes calls it off, and nothing changes hands");
        // The test tools' listing moves no money.
        var w3 = new OwnWorld(31);
        MarketEngine test = LMarket(31, day, w3, 2500, true);
        long was = w3.Purse.Money;
        RunTo(test, MarketTrading.MarketDays(day, 30));
        Check(test.MineListed() != null && w3.Purse.Money == was, "a test's company moves no money, ever");
        // A market that follows another player's prices nothing of this player's.
        var w4 = new OwnWorld(31);
        MarketEngine guest = LMarket(31, day, w4, 2500);
        guest.State.Following = true;
        RunTo(guest, MarketTrading.MarketDays(day, 3));
        Check(guest.State.Listing.Stage == ListingStage.Building, "while it follows a host's market the book waits");
    }

    // ---------------------------------------------------------------- the reserve

    private static void ListingReserveChecks()
    {
        var world = new OwnWorld(41);
        MarketEngine m = LListed(41, world, 4000, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the reserve's checks");
            return;
        }
        double tax = MarketInputs.TaxRate(m.State.Env);
        long before = world.Purse.Money, forcedAtStart = world.Purse.Forced;
        double rps = own.Own.Rps;
        int from = m.State.Day;
        int to = from + 30;
        RunTo(m, to);
        int days = m.State.BusinessDay - from;
        double perDay = world.Profit * (1 - own.Own.PayBp / 10000.0) * (1 - tax) / own.Shares;
        long others = own.Own.Others(own);
        double expectRps = rps + perDay * days;
        Check(Math.Abs(own.Own.Rps - expectRps) < 1e-6, "the reserve a share grows by each day's profit after pay and tax over the shares (" + own.Own.Rps.ToString("F6") + " against " + expectRps.ToString("F6") + ")");
        long taken = before - world.Purse.Money;
        double expectTaken = (Math.Max(0, own.Own.Rps) - Math.Max(0, rps)) * others;
        Check(Math.Abs(taken - expectTaken) <= 1.0, "the other holders' part is taken from the founder's cash to the unit (" + taken + " against " + expectTaken.ToString("F1") + ")");
        Check(world.Purse.Forced - forcedAtStart == taken, "it is taken whether or not the cash is there (what is owed to the holders is owed)");
        // A loss comes back out of the reserve, and beyond it is made good first by later profits.
        world.Profit = -20000;
        long beforeLoss = world.Purse.Money;
        double rpsBefore = own.Own.Rps;
        RunTo(m, to + 40);
        Check(own.Own.Rps < 0 && world.Purse.Money - beforeLoss <= (long)Math.Ceiling(Math.Max(0, rpsBefore) * others) + 1 && world.Purse.Money > beforeLoss,
            "a loss gives back the reserve the holders had and no more (" + (world.Purse.Money - beforeLoss) + ")");
        world.Profit = 8000;
        long beforeBack = world.Purse.Money;
        RunTo(m, to + 45);
        Check(world.Purse.Money == beforeBack || own.Own.Rps <= 0, "while losses are being made good, nothing more is taken");
        Check(MarketLife.Accrued(own, m.State.Day, LYear) == own.Own.Rps, "the price counts the reserve a share");
    }

    // ---------------------------------------------------------------- a day that is not the company's usual

    private static void ListingSpikeChecks()
    {
        var plain = new OwnWorld(51);
        MarketEngine a = LListed(51, plain, 2500, out Stock ka);
        var spiked = new OwnWorld(51);
        MarketEngine b = LListed(51, spiked, 2500, out Stock kb);
        if (ka == null || kb == null)
        {
            Check(false, "the companies are listed for the spike's checks");
            return;
        }
        int day = a.State.Day;
        int spike = MarketTrading.NextTradingDay(day + 2) - 1;
        spiked.ProfitOn = d => d == spike ? 80000 : 8000;   // one day ten times the usual
        RunTo(a, MarketTrading.NextTradingDay(spike + 1), MarketCatalogue.PhaseOpen);
        RunTo(b, MarketTrading.NextTradingDay(spike + 1), MarketCatalogue.PhaseOpen);
        double moved = kb.LogFair - ka.LogFair;
        Check(moved > 0 && moved < 0.05, "one day ten times the usual moves what the market thinks it worth by under five per cent (" + (moved * 100).ToString("F2") + "%)");
        // A whole quarter at twice the profit moves it far more: what the market follows is the run of the business.
        var better = new OwnWorld(51);
        MarketEngine c = LListed(51, better, 2500, out Stock kc);
        better.Profit = 16000;
        RunTo(a, day + 15);
        RunTo(c, day + 15);
        Check(kc != null && kc.LogFair - ka.LogFair > 0.15, "a quarter at twice the profit lifts it a good deal (" + (kc != null ? ((kc.LogFair - ka.LogFair) * 100).ToString("F1") : "-") + "%)");
        // Standing and the papers move the mood; a scandal what is expected of the company too.
        var known = new OwnWorld(51);
        MarketEngine d1 = LListed(51, known, 2500, out Stock kd);
        int at = MarketTrading.NextTradingDay(d1.State.Day);
        known.Standing = 70;   // twenty points up
        RunTo(d1, at, MarketCatalogue.PhaseOpen);
        var ref1 = new OwnWorld(51);
        MarketEngine r1 = LListed(51, ref1, 2500, out Stock kr);
        RunTo(r1, at, MarketCatalogue.PhaseOpen);
        double lift = kd.Sent - kr.Sent;
        Check(Math.Abs(lift - ListingCatalogue.StandingKickMostBp / 10000.0) < 1e-9, "twenty points of Standing lift the mood by the day's most (" + (lift * 10000).ToString("F1") + " bp)");
        var levied = new OwnWorld(51);
        MarketEngine p1 = LListed(51, levied, 2500, out Stock kp);
        RunTo(p1, at, MarketCatalogue.PhaseOpen + 1);
        double fairBefore = kp.LogFair;
        p1.Event(MarketInputs.EventPolicy, 12, 1);   // the minimum wage raised
        RunTo(p1, at, MarketCatalogue.PhaseOpen + 2);
        Check(kp.LogFair < fairBefore, "a wage rise lowers a restaurant company's worth the moment it is known (" + ((kp.LogFair - fairBefore) * 10000).ToString("F1") + " bp)");
        var shamed = new OwnWorld(51);
        MarketEngine e1 = LListed(51, shamed, 2500, out Stock ke);
        shamed.ScandalOn = e1.State.BusinessDay;
        RunTo(e1, at, MarketCatalogue.PhaseOpen);
        Check(ke.Outlook < kr.Outlook - 0.05 && ke.Sent < kr.Sent - 0.02 && ke.Own.YearScandals == 1, "a scandal cuts what is expected of the company and the day's mood, and is remembered for the year");
    }

    // ---------------------------------------------------------------- results and dividends

    private static void ListingResultsChecks()
    {
        var world = new OwnWorld(61);
        MarketEngine m = LListed(61, world, 2500, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the results' checks");
            return;
        }
        int quarter = own.Quarter;
        int end = MarketCatalogue.QuarterStart(quarter + 1, LYear);
        RunTo(m, end + 1);
        Check(own.PendingQuarter == quarter && own.ResultsDay > end, "a quarter ended waits for its results (" + own.ResultsDay + ")");
        own.Own.ProposedCents = 1000000;   // more than the reserve: held to it
        int results = own.ResultsDay;
        int count = own.Results.Count;
        RunTo(m, results, MarketCatalogue.PhaseResults);
        Check(own.Results.Count == count + 1 && own.PendingQuarter < 0, "the results are out on their day");
        QuarterResult r = own.Results[own.Results.Count - 1];
        Check(r.Quarter == quarter && r.RevenueK > 0 && r.ProfitK > 0 && r.Eps > 0, "the results are the books' quarter (" + r.RevenueK + "k, " + r.ProfitK + "k)");
        Check(own.DeclaredCents == ListingRules.Most(own.Own) || own.DeclaredCents == (int)Math.Floor(own.Own.Rps * 100), "a dividend proposed beyond the reserve is held to it (" + own.DeclaredCents + ")");
        Check(own.Own.ProposedCents == 0, "a proposal is for one set of results");
        int declared = own.DeclaredCents;
        int record = own.RecordDay;
        RunTo(m, record, MarketCatalogue.PhaseClose - 1);
        double rps = own.Own.Rps;
        RunTo(m, record);
        Check(declared > 0 && Math.Abs(own.Own.Rps - (rps - declared / 100.0)) < 1e-9, "the dividend goes out of the reserve at its record day's close (" + rps.ToString("F3") + " to " + own.Own.Rps.ToString("F3") + ")");
        // A board the founder does not chair pays half the reserve.
        own.Own.Board = false;
        RunTo(m, own.ResultsDay > 0 ? own.ResultsDay : MarketCatalogue.QuarterStart(own.Quarter + 1, LYear) + 8, MarketCatalogue.PhaseResults);
        Check(ListingRules.Declared(own) >= 0, "a board not the founder's sets its own dividend");
        var t = new Stock { Shares = 1000, Own = new OwnListing { Rps = 2.0, Board = false } };
        Check(ListingRules.Declared(t) == 100, "a board not the founder's pays half the reserve (" + ListingRules.Declared(t) + ")");
        t.Own.Board = true;
        t.Own.ProposedCents = 150;
        Check(ListingRules.Declared(t) == 150, "a founder's board pays what it proposes, within the reserve");
        t.Own.Rps = -1;
        Check(ListingRules.Declared(t) == 0, "nothing is paid out of a deficit");
        // The year's last results set the yearly meeting.
        Check(ListingCatalogue.AgmNoticeDays > 0, "the yearly meeting follows the year's last results");
    }

    // ---------------------------------------------------------------- when the founder may not deal

    private static void ListingClosedChecks()
    {
        var s = new Stock { Quarter = MarketCatalogue.QuarterOf(200, LYear), PendingQuarter = -1 };
        int from = MarketCatalogue.QuarterStart(s.Quarter, LYear), next = MarketCatalogue.QuarterStart(s.Quarter + 1, LYear);
        int closes = next - Math.Max(1, (next - from) / ListingCatalogue.ClosedShare);
        Check(!ListingRules.Closed(s, from, LYear) && !ListingRules.Closed(s, closes - 1, LYear) && ListingRules.Closed(s, closes, LYear), "the last third of a quarter is closed (" + closes + ")");
        s.PendingQuarter = s.Quarter - 1;
        Check(ListingRules.Closed(s, from, LYear), "until a quarter's results are out, closed");
        s.PendingQuarter = -1;
        var o = new OwnListing { Founder = 1000, LockDay = 500 };
        s.Own = o;
        s.Shares = 10000;
        s.Adv = 100000;
        Check(ListingRules.DealBlocked(s, from, LYear, -10) == OwnWhy.LockedUp, "no sale in the lock-up");
        Check(ListingRules.DealBlocked(s, from, LYear, 10) == OwnWhy.None, "a purchase in the lock-up is allowed");
        o.LockDay = 0;
        Check(ListingRules.DealBlocked(s, from, LYear, -2000) == OwnWhy.NoShares, "no sale of more than is held");
        Check(ListingRules.DealBlocked(s, from, LYear, 8001) == OwnWhy.TooMuch, "no holding past the most");
        Check(ListingRules.DealBlocked(s, closes, LYear, 10) == OwnWhy.Closed, "nothing in a closed time");
        s.Adv = 100;
        Check(ListingRules.DealBlocked(s, from, LYear, 50) == OwnWhy.TooLarge, "no deal larger than an order may be");
        s.Adv = 100000;
        ListingRules.Count(o, from, LYear, 900, 0, 0);
        Check(ListingRules.DealBlocked(s, from, LYear, 100) == OwnWhy.None && ListingRules.DealBlocked(s, from, LYear, 101) == OwnWhy.YearLimit,
            "in a game year the founder buys at most a tenth of the company (9% bought: 1% more, and no more)");
        Check(ListingRules.DealBlocked(s, from, LYear, -10) == OwnWhy.None, "...and sells against a limit of its own");
        s.Quarter = MarketCatalogue.QuarterOf(from + LYear, LYear);
        Check(ListingRules.DealBlocked(s, from + LYear, LYear, 1000) == OwnWhy.None, "a new year begins with nothing used");
        ListingRules.Count(o, from + LYear, LYear, 0, 0, 0);
        Check(o.BoughtBp == 0 && o.DealYear == MarketCatalogue.YearOf(from + LYear, LYear), "...and the counts start again");
    }

    // ---------------------------------------------------------------- the founder's deals

    private static void ListingDealChecks()
    {
        var world = new OwnWorld(71);
        MarketEngine m = LListed(71, world, 4000, out Stock own);
        var twinWorld = new OwnWorld(71);
        MarketEngine twin = LListed(71, twinWorld, 4000, out Stock twinOwn);   // the same market with no deal in it
        if (own == null || twinOwn == null)
        {
            Check(false, "the company is listed for the deals' checks");
            return;
        }
        OwnListing o = own.Own;
        // Into an open time of a quarter, in a session.
        int day = m.State.Day + 1;
        while (ListingRules.Closed(own, day, LYear) || !MarketCatalogue.TradingDay(day))
        {
            day++;
        }
        RunTo(m, day, MarketCatalogue.PhaseOpen + 5);
        RunTo(twin, day, MarketCatalogue.PhaseOpen + 5);
        Check(m.Trading && !ListingRules.Closed(own, day, LYear), "an open time of the quarter, in session (day " + day + ")");
        long shares = Math.Min(10000, MarketTrading.MostPerOrder(own) / 2);
        OwnQuote q = m.Quote(own, OwnAction.Deal, shares);
        Check(q.Why == OwnWhy.None && q.Value > 0 && q.Total == q.Value - q.Reserve + q.Fee, "a purchase is quoted with its reserve and commission (" + q.Total + ")");
        long founder = o.Founder, cash = world.Purse.Money;
        int price = own.Last;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, own.Id, (int)shares);
        RunTo(m, day, m.State.Phase + 1);
        RunTo(twin, day, twin.State.Phase + 1);
        Check(o.Founder == founder + shares, "the founder's purchase is theirs at the next moment");
        Check(world.Count(MarketNewsKind.OwnDeal) == 1 && world.Last(MarketNewsKind.OwnDeal).A == shares, "and announced");
        long paid = cash - world.Purse.Money;
        Check(Math.Abs(paid - q.Total) <= q.Total / 50 + 2, "it costs what it was quoted, at the moment's price (" + paid + " against " + q.Total + ")");
        Check(own.Last > twinOwn.Last, "the market reads a founder's buying as good news (" + own.Last + " against " + twinOwn.Last + " without it)");
        // A sale in the lock-up is refused when its moment comes.
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, own.Id, -1000);
        RunTo(m, day, m.State.Phase + 1);
        Check(world.Last(MarketNewsKind.OwnRefused).B == (int)OwnWhy.LockedUp && o.Founder == founder + shares, "a sale in the lock-up is refused");
        // A gain on shares bought and sold within the time the rules look back is paid back to the company.
        o.LockDay = 0;
        var t = new OwnListing();
        long g1 = ListingRules.ShortSwing(t, 100, 1000, 1000, LYear);
        long g2 = ListingRules.ShortSwing(t, 105, -600, 1200, LYear);
        Check(g1 == 0 && g2 == 1200, "shares bought at 10 and sold at 12 within the time: the gain of 2 a share on 600 is paid back (" + g2 + ")");
        long g3 = ListingRules.ShortSwing(t, 106, -400, 900, LYear);
        Check(g3 == 0, "a loss on the rest pays nothing back, and is no credit");
        Check(t.Deals.Count == 0, "every share bought and sold has been matched once, and is gone");
        var u = new OwnListing();
        ListingRules.ShortSwing(u, 200, -500, 1200, LYear);
        long g4 = ListingRules.ShortSwing(u, 205, 300, 800, LYear);
        Check(g4 == 1200 && u.Deals.Count == 1 && u.Deals[0].Shares == -200, "shares bought back cheaper than they were sold: the gain of 4 a share on 300 is paid back, 200 sold left to match (" + g4 + ")");
        long g5 = ListingRules.ShortSwing(u, 200 + LYear, 200, 100, LYear);
        Check(g5 == 0, "beyond the time the rules look back, a gain is the founder's");
        double rpsBefore = o.Rps;
        int sellDay = day;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, own.Id, (int)-shares);
        RunTo(m, sellDay, m.State.Phase + 1);
        Check(o.Founder == founder, "the founder sells what they bought");
        bool disgorged = world.Count(MarketNewsKind.OwnDisgorged) > 0;
        Check(!disgorged || o.Rps > rpsBefore, "a gain paid back goes to the other holders' reserve");
        // Round trips never pay: buying and selling at once costs the spread, commission, the market's move and any gain.
        long round = cash - world.Purse.Money;
        Check(round > 0, "buying and selling straight back costs the founder money, never makes it (" + round + ")");
    }

    // ---------------------------------------------------------------- buy-backs, new shares, taking private

    private static void ListingCompanyActionChecks()
    {
        var world = new OwnWorld(81);
        MarketEngine m = LListed(81, world, 4000, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the actions' checks");
            return;
        }
        OwnListing o = own.Own;
        int day = m.State.Day + 1;
        while (ListingRules.Closed(own, day, LYear) || !MarketCatalogue.TradingDay(day))
        {
            day++;
        }
        RunTo(m, day, MarketCatalogue.PhaseOpen + 3);
        long shares = own.Shares, others = o.Others(own), cash = world.Purse.Money;
        long back = Math.Min(others / 20, MarketTrading.MostPerOrder(own));
        OwnQuote q = m.Quote(own, OwnAction.BuyBack, back);
        int stakeBefore = o.StakeBp(own);
        m.Event(MarketInputs.EventOwn + (int)OwnAction.BuyBack, own.Id, (int)back);
        RunTo(m, day, m.State.Phase + 1);
        Check(own.Shares == shares - back && o.Others(own) == others - back && o.StakeBp(own) > stakeBefore, "a buy-back takes shares out of issue: the founder's stake grows");
        Check(Math.Abs((cash - world.Purse.Money) - q.Total) <= q.Total / 50 + 2, "it costs the shares less their reserve, with commission (" + (cash - world.Purse.Money) + ")");
        Check(m.CanAsk(own, OwnAction.BuyBack, own.Shares * ListingCatalogue.BuyBackMostBp / 10000L) != OwnWhy.None, "no more than a tenth of the company bought back in a year");
        long room = ListingRules.IssueRoom(own, day, LYear);
        Check(room == own.Shares * ListingCatalogue.IssueMostBpYear / 10000L, "the year's room for new shares (" + room + ")");
        long fresh = room / 2;
        long before = own.Shares;
        cash = world.Purse.Money;
        OwnQuote qi = m.Quote(own, OwnAction.Issue, fresh);
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Issue, own.Id, (int)fresh);
        RunTo(m, day, m.State.Phase + 1);
        Check(own.Shares == before + fresh && world.Purse.Money - cash == -qi.Total, "new shares bring their price less the underwriters and the reserve they carry (" + (world.Purse.Money - cash) + ")");
        Check(ListingRules.IssueRoom(own, day, LYear) < room, "the year's room is used");
        Check(m.CanAsk(own, OwnAction.Issue, room) == OwnWhy.TooLarge, "no more than the year's room");
        o.Board = false;
        Check(m.CanAsk(own, OwnAction.BuyBack, 10) == OwnWhy.Board && m.CanAsk(own, OwnAction.Issue, 10) == OwnWhy.Board && m.CanAsk(own, OwnAction.Pay, 0) == OwnWhy.Board
              && m.CanAsk(own, OwnAction.Private, 0) == OwnWhy.Board, "a board that is not the founder's decides no buy-back, new shares, pay or taking private");
        Check(m.CanAsk(own, OwnAction.Deal, 10) == OwnWhy.None, "the founder may still buy their own shares");
        o.Board = true;
        // Taking it private: every other holder at the premium, less the reserve they had.
        cash = world.Purse.Money;
        OwnQuote qp = m.Quote(own, OwnAction.Private, 0);
        Check(qp.Why == OwnWhy.None && qp.Cents >= (int)Math.Round(own.Last * (1 + ListingCatalogue.PrivatePremiumBp / 10000.0)) && qp.Cents >= o.ListedCents, "taking it private: every other share at the premium at least, and never under the offer's price in its first year");
        int highest = 0;
        for (int i = 0; i < own.Daily.Count; i++)
        {
            highest = Math.Max(highest, own.Daily[i].C);
        }
        Check(qp.Cents >= highest, "...nor under its highest close of the last year (" + qp.Cents + " against " + highest + ")");
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Private, own.Id, 0);
        RunTo(m, day, m.State.Phase + 1);
        Check(!own.Listed && m.State.Listing.Stage == ListingStage.None && world.Count(MarketNewsKind.OwnPrivate) == 1, "it leaves the market and is the founder's alone again");
        Check(Math.Abs((cash - world.Purse.Money) - qp.Total) <= qp.Total / 50 + 2, "for what it was quoted (" + (cash - world.Purse.Money) + " against " + qp.Total + ")");
        var thin = new OwnWorld(82);
        MarketEngine n = LListed(82, thin, 6000, out Stock k2);
        if (k2 != null)
        {
            Check(n.CanAsk(k2, OwnAction.Private, 0) == OwnWhy.Control || !n.Trading, "a founder with under half cannot take it private");
        }
    }

    // ---------------------------------------------------------------- votes

    private static void ListingVoteChecks()
    {
        bool half = true;
        for (int against = 0; against <= 100; against += 5)
        {
            for (int turnout = 0; turnout <= 100; turnout += 10)
            {
                for (int investor = 0; investor <= 4000; investor += 1000)
                {
                    half &= ListingVotes.BoardCarries(5000, 5000, investor, turnout / 100.0, against / 100.0);
                    half &= ListingVotes.BoardCarries(6000, 4000, Math.Min(4000, investor), turnout / 100.0, against / 100.0);
                }
            }
        }
        Check(half, "a founder with half the shares or more carries every vote, whatever the others do");
        Check(!ListingVotes.BoardCarries(4000, 6000, 0, 1.0, 0.9), "with under half and nine in ten of the others against, the board loses");
        Check(ListingVotes.BoardCarries(4000, 6000, 0, 0.6, 0.6), "with under half, a turnout of six in ten and six in ten against, the board still carries it");
        Check(!ListingVotes.BoardCarries(1000, 9000, 900, 0.7, 0.6), "with a tenth, six in ten of the others and an investor against, it loses");
        Check(ListingVotes.BoardCarries(1000, 9000, 900, 0.7, 0.5), "...but with half of them, the board carries it");
        var s = new Stock { Shares = 10000, Last = 1000 };
        s.Own = new OwnListing { Founder = 4000, PayBp = 500, YearCents = 1000, YearIndex = 1000 };
        HolderMood m0 = ListingVotes.Read(s, 1000);
        s.Own.PayBp = 2500;
        HolderMood m1 = ListingVotes.Read(s, 1000);
        Check(m0.PayOver == 0 && m1.PayOver == 4.0 && ListingVotes.PayAgainst(m1) > ListingVotes.PayAgainst(m0), "pay five times the fair rate: four fair rates over, and more against");
        s.Own.PayBp = 500;
        HolderMood lag = ListingVotes.Read(s, 1300);
        Check(Math.Abs(lag.Lag - 0.3) < 1e-9 && ListingVotes.BoardAgainst(lag) > ListingVotes.BoardAgainst(m0) + 0.3, "a year 30% under the city index: more against the board");
        s.Own.Rps = 3.0;
        HolderMood hoard = ListingVotes.Read(s, 1000);
        Check(hoard.Hoard > 0 && ListingVotes.PayoutFor(hoard) > ListingVotes.PayoutFor(m0), "a reserve of 30% of the price held back: the holders want it paid out");
        s.Own.YearDividendCents = 200;
        Check(ListingVotes.Read(s, 1000).Hoard == 0, "...not when a good part of it was paid");
        double t1 = ListingVotes.Turnout(1, 1001, 50), t2 = ListingVotes.Turnout(1, 1001, 50);
        Check(t1 == t2 && t1 >= ListingCatalogue.TurnoutLeast && t1 <= ListingCatalogue.TurnoutMost, "the turnout is drawn on the day, the same from the same secret");
        Check(ListingVotes.Acceptance(10, 10) == ListingCatalogue.AcceptBase && ListingVotes.Acceptance(13, 10) > 0.9 && ListingVotes.Acceptance(5, 10) == ListingCatalogue.AcceptLeast,
            "holders take a bid at what the company is worth half the time, more for a premium");
        s.Own.Founder = 2999;
        Check(ListingVotes.CanWin(s), "under 30% a bid can win");
        s.Own.Founder = 3000;
        Check(!ListingVotes.CanWin(s), "at 30% it cannot");
    }

    // A company with its founder's stake at so much, its year poor, its pay high: the yearly meeting played.
    private static MarketEngine LMeeting(ulong seed, int floatBp, out Stock own, out OwnWorld world, int stakeBp = 0)
    {
        world = new OwnWorld(seed);
        MarketEngine m = LListed(seed, world, floatBp, out own);
        if (own != null)
        {
            if (stakeBp > 0)
            {
                own.Own.Founder = own.Shares * stakeBp / 10000L;
            }
            own.Own.PayBp = 2500;
            own.Own.YearCents = own.Last * 2;   // the price has halved over the year
            own.Own.YearScandals = 2;
            own.Own.AgmDay = MarketTrading.NextTradingDay(m.State.Day);
            RunTo(m, own.Own.AgmDay);
        }
        return m;
    }

    private static void ListingMeetingChecks()
    {
        int lost = 0, kept = 0, close = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            LMeeting(900 + seed, 6000, out Stock thin, out OwnWorld w1, 3000);
            LMeeting(900 + seed, 2500, out Stock fat, out OwnWorld w2);
            LMeeting(900 + seed, 6000, out Stock forty, out OwnWorld w3);
            if (thin == null || fat == null || forty == null)
            {
                continue;
            }
            lost += (thin.Own.Votes & (ListingVotes.PayLost | ListingVotes.BoardLost)) != 0 ? 1 : 0;
            close += (forty.Own.Votes & (ListingVotes.PayLost | ListingVotes.BoardLost)) != 0 ? 1 : 0;
            kept += (fat.Own.Votes & (ListingVotes.PayLost | ListingVotes.BoardLost)) == 0 ? 1 : 0;
            if ((thin.Own.Votes & ListingVotes.PayLost) != 0)
            {
                Check(thin.Own.PayBp == ListingCatalogue.PayRefBp, "pay voted down goes back to the fair rate");
            }
            if ((thin.Own.Votes & ListingVotes.BoardLost) != 0)
            {
                Check(!thin.Own.Board && thin.Own.PayBp <= ListingCatalogue.PayRefBp, "a board lost is no longer the founder's, and pays the fair rate");
            }
            Check(w1.Count(MarketNewsKind.OwnMeeting) == 1 && thin.Own.AgmDay == 0 && thin.Own.YearCents == thin.Last, "the meeting is news, and the year starts again from it");
        }
        Check(lost >= 10, "a founder with 30% after a poor year with pay five times the fair rate loses votes at nearly every meeting (" + lost + " of 12)");
        Check(close >= 1 && close < lost, "with 40% sometimes, as the turnout falls (" + close + " of 12)");
        Check(kept == 12, "a founder with 75% never does (" + kept + " of 12)");
    }

    // ---------------------------------------------------------------- investors who press the board, and a bid

    private static void ListingActivistAndBidChecks()
    {
        // An investor buys in at results when the company is cheap, and calls a meeting.
        var s = new Stock { Shares = 10000, Last = 1000, ListedDay = 1, Sent = -0.4 };
        s.Own = new OwnListing { Founder = 4000, YearCents = 1000, YearIndex = 1000 };
        ActivistDemand d = ListingVotes.Reason(s, 1000, 100, LYear, out double strength);
        Check(d == ActivistDemand.Sale && strength == 1.0, "a price well under the company's worth: an investor presses for a sale");
        s.Sent = 0;
        s.Own.Rps = 3.0;
        Check(ListingVotes.Reason(s, 1000, 100, LYear, out _) == ActivistDemand.Payout, "a reserve held back: for a payout");
        s.Own.Rps = 0;
        s.Own.PayBp = 2000;
        Check(ListingVotes.Reason(s, 1000, 100, LYear, out _) == ActivistDemand.Pay, "pay well over the fair rate: for it cut");
        s.Own.PayBp = 500;
        Check(ListingVotes.Reason(s, 1500, 100, LYear, out _) == ActivistDemand.Board, "a year far under the city index: for the board");
        Check(ListingVotes.Reason(s, 1000, 100, LYear, out double none) == ActivistDemand.None && none == 0, "a company that gives no reason: nobody");
        s.ListedDay = 90;
        Check(ListingVotes.Reason(s, 1500, 100, LYear, out _) == ActivistDemand.None, "nobody presses a company listed less than a quarter");
        s.Own.PayBp = 400;
        s.Own.ActDemand = ActivistDemand.Pay;
        Check(ListingVotes.Met(s), "pay at the fair rate meets a demand for it cut");
        // A bid on a cheap company: it wins only under the threshold.
        int won = 0, blocked = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            foreach (int floatBp in new[] { 6000, 2500 })
            {
                var world = new OwnWorld(700 + seed);
                MarketEngine m = LListed(700 + seed, world, floatBp, out Stock own);
                if (own == null)
                {
                    continue;
                }
                if (floatBp == 6000)
                {
                    own.Own.Founder = own.Shares / 4;   // 25%: under the threshold
                }
                long cash = world.Purse.Money;
                m.BidFor(own.Id);
                int bid = own.BidCents;
                int end = own.BidEndDay;
                Check(bid > own.Last && end > m.State.Day, "a bid for it at a premium, to complete in some weeks");
                RunTo(m, end);
                if (!own.Listed)
                {
                    won++;
                    Check(floatBp == 6000 && own.Reason == GoneReason.TakenOver && m.State.Listing.Stage == ListingStage.None, "a bid wins only under the threshold, and the company leaves the market");
                    long paid = world.Purse.Money - cash;
                    long founderMoney = MarketTrading.Value(own.Own.Founder, bid, false);
                    Check(world.Last(MarketNewsKind.BidDone).Stock == own.Id && paid <= founderMoney && paid >= founderMoney - founderMoney / 5, "the founder is paid the bid for their shares (" + paid + " with " + founderMoney + " for them, less the holders' part of the days between)");
                }
                else
                {
                    blocked++;
                    Check(own.BidCents == 0 && world.Count(MarketNewsKind.OwnBidBlocked) == 1, "a bid that does not win lapses, and the price is its own again");
                    if (floatBp == 2500)
                    {
                        Check(world.Last(MarketNewsKind.OwnBidBlocked).A == 1, "...because of the founder's holding");
                    }
                }
            }
        }
        Check(won >= 3 && blocked >= 10, "bids win only for founders under the threshold, and not every time (" + won + " won, " + blocked + " lapsed)");
    }

    // ---------------------------------------------------------------- tax on dividends

    private static void ListingTaxChecks()
    {
        Check(ListingVotes.DividendTax(1000, 0) == 150, "15% of a dividend is withheld");
        Check(ListingVotes.DividendTax(1000, 1) == 175 && ListingVotes.DividendTax(1000, -1) == 125, "...more or less with the council's business tax");
        Check(ListingVotes.DividendTax(-5, 0) == 0 && ListingVotes.DividendTax(0, 1) == 0, "nothing of nothing");
        Check(ListingMoney.Deductible(ListingMoney.Audit) && ListingMoney.Deductible(ListingMoney.Exchange) && !ListingMoney.Deductible(ListingMoney.Holders)
              && !ListingMoney.Deductible(ListingMoney.Underwriting) && !ListingMoney.Deductible(ListingMoney.DividendTax),
            "the audit and the exchange's fees are business costs the game's tax deducts; the holders' part, the underwriters and the dividend tax are not");
    }

    // ---------------------------------------------------------------- a business of the company's sold

    private static void ListingProceedsChecks()
    {
        var world = new OwnWorld(97);
        MarketEngine m = LListed(97, world, 4000, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the sale's checks");
            return;
        }
        double rps = own.Own.Rps;
        long cash = world.Purse.Money;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Proceeds, own.Id, 500);   // a business sold for 500,000
        RunTo(m, MarketTrading.NextTradingDay(m.State.Day), MarketCatalogue.PhaseNone + 1);
        double each = 500000.0 / own.Shares;
        Check(Math.Abs(own.Own.Rps - (rps + each)) < 1e-9, "a business sold: its price is every holder's, a share's part into the reserve");
        Check(Math.Abs((cash - world.Purse.Money) - Math.Floor(each * own.Own.Others(own))) <= 1, "...the other holders' part from the founder's cash (" + (cash - world.Purse.Money) + ")");
        Check(m.CanAsk(own, OwnAction.Proceeds, 1) == OwnWhy.None, "...heard whatever the hour");
    }

    // ---------------------------------------------------------------- the year's audit, heard by the market

    private static void ListingAuditEventChecks()
    {
        var world = new OwnWorld(95);
        MarketEngine m = LListed(95, world, 2500, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the audit's checks");
            return;
        }
        double outlook = own.Outlook;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Audit, own.Id, ListingBook.BribesOut);
        RunTo(m, MarketTrading.NextTradingDay(m.State.Day), MarketCatalogue.PhaseOpen);
        Check(own.Outlook == outlook || Math.Abs(own.Outlook - outlook) < 0.01, "an audit that only discloses moves nothing");
        Check(world.Count(MarketNewsKind.OwnAudit) == 0, "...and is no news");
        double before = own.Outlook;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Audit, own.Id, ListingBook.CashOffBooks);
        RunTo(m, m.State.Day, m.State.Phase + 1);
        Check(own.Outlook < before + ListingCatalogue.AuditOutlook / 2 && world.Count(MarketNewsKind.OwnAudit) == 1, "an audit that finds the books wanting cuts what is expected of the company, and is news");
        Check(m.CanAsk(own, OwnAction.Audit, 0) == OwnWhy.None, "the auditors' findings are heard whatever the hour");
        var press = new OwnWorld(96);
        MarketEngine n = LListed(96, press, 2500, out Stock k);
        Check(k != null && n.PressBoard(k.Id, ActivistDemand.Pay) && k.Own.ActDemand == ActivistDemand.Pay && k.Own.ActBp >= ListingCatalogue.ActivistStakeLeastBp
              && press.Count(MarketNewsKind.OwnActivist) == 1, "an investor pressing the board for a demand, as the test tools make one");
        Check(!n.PressBoard(k.Id, ActivistDemand.None), "...but never for nothing");
    }

    // ---------------------------------------------------------------- the save

    private static void ListingStoreChecks()
    {
        var world = new OwnWorld(91);
        MarketEngine m = LListed(91, world, 3000, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the save's checks");
            return;
        }
        RunTo(m, m.State.Day + 20);
        own.Own.Deals.Add(new OwnDeal { Day = m.State.Day, Shares = -5, Cents = 1234 });
        own.Own.ActBp = 500;
        own.Own.ActDemand = ActivistDemand.Payout;
        own.Own.ActDay = m.State.Day + 9;
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out int version);
        Stock again = back != null ? back.Stock(own.Id) : null;
        Check(again != null && again.Own != null, "a player's company is read back from the save's line");
        if (again == null || again.Own == null)
        {
            return;
        }
        Check(again.Name == own.Name && again.Ticker == own.Ticker && again.Sector == own.Sector && again.Def.DebtPct == own.Def.DebtPct, "its name, ticker, sector and usual debt");
        Check(again.Own.Owner == own.Own.Owner && again.Own.Founder == own.Own.Founder && again.Own.Rps == own.Own.Rps && again.Own.PayBp == own.Own.PayBp && again.Own.Board == own.Own.Board,
            "its founder, their shares, the reserve exactly, pay and board");
        Check(again.Own.RevQ == own.Own.RevQ && again.Own.NiY == own.Own.NiY && again.Own.LeverY == own.Own.LeverY && again.Own.Expect == own.Own.Expect && again.Own.Carry == own.Own.Carry,
            "what the market has followed, exactly");
        Check(again.Own.Deals.Count == 1 && again.Own.Deals[0].Shares == -5 && again.Own.ActDemand == ActivistDemand.Payout && again.Own.ActDay == own.Own.ActDay, "its deals and an investor's demand");
        Check(again.MarginShift == own.MarginShift && again.LogFair == own.LogFair, "its figures beyond the city's companies' bounds are kept as they are");
        Check(back.Listing.Stage == ListingStage.Listed && back.Listing.Books.Count == m.State.Listing.Books.Count && back.Listing.StockId == own.Id && back.Listing.Name == m.State.Listing.Name,
            "the way to the market and the books");
        m.State.Listing.Trade = 4;
        m.State.Listing.TestBooks = true;
        m.State.Listing.HandOverTo = "Grand Bay Holdings";
        m.State.Listing.HandOverBy = 345;
        m.State.Listing.HandOverOwed = 123456789;
        m.State.Listing.HandOverCount = 2;
        m.State.Listing.HandOver.Add("Elm%20Street~14");
        m.State.Listing.HandOver.Add("Ash~2");
        line = MarketStore.Write(m.State);
        back = MarketStore.Read(line, out _);
        ListingFile h = back.Listing;
        Check(h.Trade == 4 && h.TestBooks && h.HandOverTo == "Grand Bay Holdings" && h.HandOverBy == 345 && h.HandOverOwed == 123456789 && h.HandOverCount == 2 && h.HandOver.Count == 2
              && h.HandOver[0] == "Elm%20Street~14" && h.HandOver[1] == "Ash~2", "a hand-over under way is kept exactly, its places' own marks too");
        Check(MarketStore.Write(back) == line, "...and written again the same");
        Check(MarketStore.Write(back) == line, "written again, the line is the same");
        string city = MarketStore.Write(m.State, true);
        Check(city.IndexOf("~l:", StringComparison.Ordinal) < 0 && city.IndexOf("~u:", StringComparison.Ordinal) < 0, "the city's line has nothing of the player's way to the market");
        MarketState shared = MarketStore.Read(city, out _);
        Check(shared != null && shared.Stock(own.Id) != null && shared.Stock(own.Id).Own != null, "...but has their listed company, as every machine of a session must");
        // A line of a version without any of this is read as before: no company of a player's, nothing on the way.
        MarketEngine plain = NewMarket(91, LYear, 200);
        MarketState read = MarketStore.Read(MarketStore.Write(plain.State), out _);
        Check(read.Listing.Stage == ListingStage.None && read.Listing.Books.Count == 0, "a market with no player's company reads back with nothing on the way");
        // A damaged own record is dropped, not half read.
        string damaged = line.Replace("|" + own.Own.Owner.ToString("X16") + "|", "|ZZZ|");
        MarketState hurt = MarketStore.Read(damaged, out _);
        Check(hurt == null || hurt.Stock(own.Id) == null, "a player's company with a damaged founder is dropped");
    }

    private static void ListingTapeChecks()
    {
        MarketEnv e = MarketEnv.Quiet();
        var t = new System.Text.StringBuilder();
        MarketTape.WriteEnv(t, e);
        string plain = t.ToString();
        Check(plain.Split(',').Length == 17 + MarketEnv.SupplyKinds + 1, "a day with no player's company is written as before");
        e.OwnFrom = 120;
        e.OwnDays = 3;
        e.OwnRev = 123456789012;
        e.OwnProfit = -98765;
        e.OwnStanding = 64;
        e.OwnPress = -12;
        e.OwnScandal = 1;
        e.OwnDebt = 4444;
        e.Trades = 1;
        e.TradeOf[0] = 3;
        e.TradeSales[0] = 77;
        t.Length = 0;
        MarketTape.WriteEnv(t, e);
        Check(MarketTape.ReadEnv(t.ToString(), out MarketEnv r) && r.OwnFrom == 120 && r.OwnDays == 3 && r.OwnRev == 123456789012 && r.OwnProfit == -98765 && r.OwnStanding == 64 && r.OwnPress == -12
              && r.OwnScandal == 1 && r.OwnDebt == 4444 && r.Trades == 1 && r.TradeSales[0] == 77, "a day with the player's company reads back exactly, after the trades");
        Check(MarketTape.ReadEnv(plain, out MarketEnv q) && q.OwnDays == 0 && q.OwnStanding == -1, "an old day reads with nothing of a player's company");
    }

    // ---------------------------------------------------------------- a Going Public session

    private static void ListingSessionChecks()
    {
        // The host's company in the city's market; a guest with a company of their own.
        var hostWorld = new OwnWorld(101);
        MarketEngine host = LListed(101, hostWorld, 2500, out Stock hostCo);
        var guestWorld = new OwnWorld(202);
        MarketEngine guest = LListed(202, guestWorld, 2500, out Stock guestCo);
        if (hostCo == null || guestCo == null)
        {
            Check(false, "both companies are listed for the session's checks");
            return;
        }
        guest.State.Me = LGuest;
        guestCo.Own.Owner = LGuest;
        guest.State.Listing.StockId = guestCo.Id;
        string text = MarketShare.Write(host.State, host.Tape);
        MarketState adopted = MarketShare.Adopt(guest.State, text, new List<long>(), null, out MarketTape tape, out long frontier);
        Check(adopted != null && adopted.Me == LGuest && adopted.Listing == guest.State.Listing, "a guest takes the host's market and keeps who they are and their own way to the market");
        if (adopted == null)
        {
            return;
        }
        Stock theirs = adopted.Stock(hostCo.Id);
        Check(theirs != null && theirs.Own != null && theirs.Own.Owner == LMe, "the host's company is in it, the host's");
        Check(!string.IsNullOrEmpty(adopted.Listing.Suspended) && (adopted.Stock(guestCo.Id) == null || adopted.Stock(guestCo.Id).Own == null || adopted.Stock(guestCo.Id).Own.Owner != LGuest),
            "the guest's own company is set aside, not in the host's city");
        var follow = new MarketEngine(adopted, tape, guestWorld);
        adopted.Following = true;
        Check(!follow.Mine(theirs) && follow.MineListed() == null, "to the guest the host's company is another holder's, and nothing of theirs is listed there");
        long cash = guestWorld.Purse.Money;
        int work = int.MaxValue;
        follow.Follow(frontier);
        follow.RunTo(Math.Min(frontier, MarketState.Time(adopted.Day + 5, MarketCatalogue.PhaseClose)), ref work);
        Check(guestWorld.Purse.Money == cash, "the host's company's holders are paid from the host's cash, never the guest's");
        // The guest leaves: the host's company leaves their market (their holding paid at its last price), their own is back.
        adopted.Following = false;
        adopted.Positions.Add(new Position { StockId = hostCo.Id, Shares = 100, Cost = 1000 });
        cash = guestWorld.Purse.Money;
        int moved = follow.Alone();
        Stock mine = follow.MineListed();
        Check(moved == 2 && !theirs.Listed && theirs.Reason == GoneReason.Delisted && adopted.Position(hostCo.Id) == null, "alone again, the host's company leaves this market");
        Check(guestWorld.Purse.Money - cash == MarketTrading.Value(100, theirs.GoneCents, false), "...and the guest's holding in it is paid at its last price");
        Check(mine != null && mine.Own.Founder == guestCo.Own.Founder && mine.Own.Rps == guestCo.Own.Rps && string.IsNullOrEmpty(adopted.Listing.Suspended), "the guest's own company is back as it was");
        Check(follow.Alone() == 0, "and only once");
    }

    // ---------------------------------------------------------------- the same market every time

    private static void ListingDeterminism()
    {
        var w1 = new OwnWorld(111);
        MarketEngine a = LListed(111, w1, 3000, out Stock ka);
        var w2 = new OwnWorld(111);
        MarketEngine b = LListed(111, w2, 3000, out Stock kb);
        if (ka == null || kb == null)
        {
            Check(false, "the companies are listed for the determinism checks");
            return;
        }
        b.State.Me = LGuest;   // another machine: the same company, another player
        long guestHad = w2.Purse.Money, founderHad = w1.Purse.Money;
        int end = a.State.Day + 120;
        RunTo(a, end);
        RunTo(b, end);
        Check(ka.Last == kb.Last && ka.Own.Rps == kb.Own.Rps && ka.Shares == kb.Shares, "every machine plays the company to the same price and reserve (" + ka.Last + ", " + kb.Last + ")");
        Check(w2.Purse.Money == guestHad && w1.Purse.Money != founderHad, "only the founder's cash moves");
        // A save part way and the tape: the same moments again.
        var w3 = new OwnWorld(121);
        MarketEngine c = LListed(121, w3, 3000, out Stock kc);
        RunTo(c, c.State.Day + 30);
        string saved = MarketStore.Write(c.State);
        RunTo(c, c.State.Day + 30);
        string tapeText = c.Tape.Whole();   // (the tape on disk goes on past the save: a load replays what was played)
        MarketState loaded = MarketStore.Read(saved, out _);
        loaded.Me = LMe;
        var w4 = new OwnWorld(121);
        w4.State = () => loaded;
        var d = new MarketEngine(loaded, MarketTape.Read(c.Tape.Id, tapeText), w4);
        d.HoldAfterLoad();
        RunTo(d, c.State.Day);
        Stock kd = d.State.Stock(kc.Id);
        Check(kd != null && kd.Last == kc.Last && kd.Own.Rps == kc.Own.Rps, "a load replays the company's days as they were played (" + kc.Last + ", " + (kd != null ? kd.Last : 0) + ")");
    }

    private static void ListingCost()
    {
        var plain = new OwnWorld(131) { Company = false };
        MarketState s1 = MarketSetup.Open(131, 200, 0, LYear, plain.Env(200), 131, null, null, null);
        plain.State = () => s1;
        var m1 = new MarketEngine(s1, new MarketTape(131), plain);
        var watch = Stopwatch.StartNew();
        RunTo(m1, 200 + 120);
        long without = watch.ElapsedMilliseconds;
        var world = new OwnWorld(131);
        MarketEngine m2 = LListed(131, world, 2500, out Stock own);
        watch.Restart();
        RunTo(m2, m2.State.Day + 120);
        long with = watch.ElapsedMilliseconds;
        Check(with <= without * 1.5 + 200, "a player's company adds little to the market's work (" + without + " ms without, " + with + " ms with, 120 days)");
    }
}
