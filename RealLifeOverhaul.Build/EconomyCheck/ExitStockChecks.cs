using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// A firm of the player's that listed, and the stock it became (Scripts/Tech/Venture/ExitStockRules.cs, Scripts/Markets/Listing/ListingFirm.cs, MarketEngine.Firms.cs): the stock made
// from the cap table and the offer, its shares and its lock-up the stock's alone, its days the firm's own statements on the market's tape, the founder's deals the only way to sell, the
// cap table brought back to agree with it, the stock ended with the firm; the city's startups that listed as companies of the market's; every way of getting money out twice tried
// (the offer's money, the investors paid at the listing and again in a waterfall, a sale inside the lock-up by either path, a wind down that skips the holders, a firm listed with money
// missing from its account); a Going Public session, the save, an old save, the options. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private const int SxYear = 60;
    private const ulong SxMe = 0x5EED00A1UL, SxGuest = 0x5EED00A2UL;
    private const string SxPlace = "Main~1";

    /// <summary>The city and the world for a market with the player's firms in it: each firm's statements a day at a time, as the game side hands them over.</summary>
    private sealed class StockWorld : IMarketWorld
    {
        public ulong Seed;
        public int Secrets;
        public MarketEnv City = MarketEnv.Quiet();
        public readonly MarketWallet Purse = new MarketWallet { Money = 1000000000 };
        public readonly List<MarketNews> Heard = new List<MarketNews>();
        public Func<MarketState> State;
        public readonly Dictionary<int, double[]> Feeds = new Dictionary<int, double[]>();   // a stock's id to its takings and profit a day
        public double[] Fallback;                                                            // ...a firm with no feed of its own
        public bool Silent;                                                                  // the tech sector is off: no firm's day is given

        public StockWorld(ulong seed)
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
            if (s == null || Silent)
            {
                return e;
            }
            var ids = new List<int>();
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (k.Listed && k.Own != null && k.Own.Firm && k.Own.Owner == s.Me && ids.Count < MarketEnv.FirmsMost)
                {
                    ids.Add(k.Id);
                }
            }
            if (ids.Count > 0)
            {
                int days = Math.Max(0, day - s.BusinessDay);
                e.OwnFrom = s.BusinessDay;
                e.OwnStanding = 50;
                e.FirmFrom = s.BusinessDay;
                e.FirmDays = days;
                e.FirmStock = new int[ids.Count];
                e.FirmRev = new long[ids.Count];
                e.FirmProfit = new long[ids.Count];
                e.FirmDebt = new long[ids.Count];
                for (int i = 0; i < ids.Count; i++)
                {
                    double[] feed = Feeds.TryGetValue(ids[i], out double[] own) ? own : Fallback;
                    e.FirmStock[i] = ids[i];
                    e.FirmRev[i] = feed != null ? (long)(feed[0] * days) : 0;
                    e.FirmProfit[i] = feed != null ? (long)(feed[1] * days) : 0;
                }
                e.Firms = ids.Count;
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

    /// <summary>A firm that has listed: the cap table and the exit the listing priced, the market, and the stock made from them.</summary>
    private sealed class SxFirm
    {
        public MarketEngine Engine;
        public StockWorld World;
        public MarketState State;
        public Stock Stock;
        public Cap Cap;
        public FirmExit Exit;
        public StockAsk Ask;
        public FirmListing Listing;
        public FirmWhy Why;
        public int Day;
        public readonly List<long[]> Dealt = new List<long[]>();   // the founder's deals the market told the tech sector: stock, shares, cents, money
    }

    private static BookQuarter[] SxBooks(int day, double revDay, double profitDay)
    {
        int now = MarketCatalogue.QuarterOf(day, SxYear);
        var books = new List<BookQuarter>();
        for (int back = 8; back >= 1; back--)
        {
            int days = Math.Max(1, MarketCatalogue.QuarterStart(now - back + 1, SxYear) - MarketCatalogue.QuarterStart(now - back, SxYear));
            books.Add(new BookQuarter { Quarter = now - back, Revenue = (long)(revDay * days), Profit = (long)(profitDay * days), Days = days });
        }
        return books.ToArray();
    }

    /// <summary>The ask a cap table and a priced offer make, in the market's figures as the wiring turns them (the level is the start's).</summary>
    private static FirmListing SxListingOf(in StockAsk a, int day, double revDay, double profitDay, int sector = (int)MarketSector.Software)
    {
        return new FirmListing
        {
            Place = a.Place, Name = a.Name, Sector = sector, Trade = MarketInputs.TradeOf("reallife:businesstype_softwarestudio"), Shares = a.Shares, Founder = a.Founder, Float = a.Float,
            PriceCents = a.PriceCents, LockDay = a.LockDay, VestBase = a.VestBase, VestDay = a.VestDay, VestCliff = a.VestCliff, VestSpan = a.VestSpan, Debt = a.Debt / 100, ListedDay = a.ListedDay,
            RevDay = revDay, ProfitDay = profitDay, Books = SxBooks(day, revDay, profitDay)
        };
    }

    /// <summary>A funded firm priced and listed through the tech sector's own rules, and put on a market that plays alone with its player as the owner.</summary>
    private static SxFirm SxMake(ulong seed, double revDay = 50000, double profitDay = 8000, long fair = 6000000000L, bool put = true, string place = SxPlace, string name = "Nimbleloop")
    {
        var f = new SxFirm { World = new StockWorld(seed), Day = TradingOnOrAfter(200) };
        MarketState state = MarketSetup.Open(seed, f.Day, 0, SxYear, f.World.Env(f.Day), seed, null, null, null);
        state.Me = SxMe;
        f.State = state;
        f.World.State = () => state;
        f.World.Fallback = new[] { revDay, profitDay };
        f.Engine = new MarketEngine(state, new MarketTape(seed), f.World);
        f.Engine.FirmDealt = (id, shares, cents, money, fee) => f.Dealt.Add(new[] { (long)id, shares, cents, money, fee });
        f.Cap = FundedCap();
        f.Cap.Place = place;
        var file = new ExitFile { Seed = 5 };
        f.Exit = file.Exit(place, true);
        ExitRules.Engage(f.Exit, ExitCatalogue.ListMrrLeast, 0, "Harbour City Bancorp", 0, fair, 100);
        ExitRules.Hold(f.Exit, VentureRules.Mood(1), 80, 80, 1, 100);
        f.Exit.Stage = ListStage.Roadshow;
        f.Exit.Price = fair;
        ExitRules.List(f.Cap, f.Exit, f.Day);
        f.Ask = ExitStockRules.Ask(f.Cap, f.Exit, place, name, f.Exit.Price, f.Cap.Total - ExitStockRules.PublicShares(f.Cap), SxYear);
        f.Ask.ListedDay = f.Day;
        f.Listing = SxListingOf(f.Ask, f.Day, revDay, profitDay);
        if (put)
        {
            f.Stock = f.Engine.ListFirm(f.Listing, out f.Why);
        }
        return f;
    }

    private static void SxRun(SxFirm f, int days)
    {
        RunTo(f.Engine, f.Engine.State.Day + days);
    }

    // The next open time of a quarter in a session: not in the last third before the results, on a trading day, in the middle of the session.
    private static void SxOpenTime(SxFirm f)
    {
        int day = f.Engine.State.Day + 1;
        while (ListingRules.Closed(f.Stock, day, SxYear) || !MarketCatalogue.TradingDay(day))
        {
            day++;
        }
        RunTo(f.Engine, day, MarketCatalogue.PhaseOpen + 5);
    }

    private static void SxDeal(SxFirm f, long shares)
    {
        f.Engine.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, f.Stock.Id, (int)shares);
        RunTo(f.Engine, f.Engine.State.Day, f.Engine.State.Phase + 1);
    }

    private static void ExitStockChecks()
    {
        ExitStockListing();
        ExitStockFigures();
        ExitStockShares();
        ExitStockDeals();
        ExitStockMoney();
        ExitStockEndings();
        ExitStockOffers();
        ExitStockCity();
        ExitStockSaves();
        ExitStockSession();
        ExitStockOldAndOff();
        ExitStockWiring();
    }

    // ---------------------------------------------------------------- the stock made

    private static void ExitStockListing()
    {
        SxFirm f = SxMake(7001);
        Stock s = f.Stock;
        Check(s != null && f.Why == FirmWhy.None && s.Listed && s.Own != null && s.Own.Firm && s.Own.Owner == SxMe,
            "stock: a firm that listed has a stock on the market, a company of its player's marked as a firm's");
        if (s == null)
        {
            return;
        }
        Check(s.Name == "Nimbleloop" && s.Ticker.Length >= 3 && s.Ticker.Length <= 4 && s.Ticker == s.Ticker.ToUpperInvariant(), "stock: its name is the firm's and its ticker three or four capitals (" + s.Ticker + ")");
        Check(s.Sector == MarketSector.Software && s.Own.Place == MarketStore.Clean(SxPlace) && s.Def.Trade == "webdevelopment", "stock: its sector and trade are the firm's, and it names the firm it is");
        Check(f.Engine.MineListed() == null && f.Engine.MineFirms(new List<Stock>()) == 1 && f.Engine.FirmByPlace(SxPlace) == s, "stock: it is not the player's whole company; the market finds it by the firm's place");
        long total = f.Cap.Total, mine = ExitRules.PlayerShares(f.Cap), pub = ExitStockRules.PublicShares(f.Cap);
        Check(s.Shares == total && s.Own.Founder == mine && s.Own.Float == pub && ExitStockRules.Grown(f.Cap, Info(s)) == 0,
            "stock: the shares in issue, the founder's and the public's are the cap table's at the listing (" + s.Shares + ", " + s.Own.Founder + ", " + s.Own.Float + ")");
        long investors = total - mine - pub;
        Check(investors > 0 && s.Shares - s.Own.Founder - s.Own.Float == investors && s.Own.Others(s) == investors + pub,
            "stock: the investors' shares are the other holders' in it, the public's the float (" + investors + ")");
        Check(s.Last == f.Ask.PriceCents && s.Last == (int)(f.Exit.Price / (total - pub)) && s.Last > 100,
            "stock: it comes to the market at the price the offer was priced at, the firm's value before the money over the shares before it (" + s.Last + ")");
        Check(s.Own.LockDay == f.Exit.LockTo && s.Own.LockDay == f.Day + ExitCatalogue.LockUpDays, "stock: the 180 day lock-up is the stock's own lock (" + s.Own.LockDay + ")");
        Check(s.ListedDay == f.Day && s.Quarter == MarketCatalogue.QuarterOf(f.Day, SxYear) && s.Results.Count >= 4, "stock: it has a quarter under way and the firm's last quarters as its results");
        Check(s.Own.Rps == 0.0 && s.Own.Mixed == false && s.Own.PayBp == ListingCatalogue.PayRefBp, "stock: no reserve, no sector mix to move it, the fair pay");
        Check(s.Adv == Math.Max(1000.0, s.Own.Float * s.Def.TurnoverBp / 10000.0), "stock: only the public's shares trade, so its day's volume follows the float and not the investors' stake");
        // no money moves to make it
        Check(f.World.Purse.Money == 1000000000 && f.World.Purse.In == 0 && f.World.Purse.Out == 0 && f.World.Purse.Forced == 0,
            "stock: making it moves no money: the offer's money went into the firm's account when the book was priced, and the stock books none of it");
        Check(f.World.Count(MarketNewsKind.Listing) == 1 && f.World.Last(MarketNewsKind.Listing).Stock == s.Id, "stock: the city hears of the listing once");
        // again: the same firm is not made twice
        Stock again = f.Engine.ListFirm(f.Listing, out FirmWhy why);
        Check(again == s && why == FirmWhy.Already && f.State.Stocks.Count == f.Engine.State.Stocks.Count && f.World.Purse.In == 0, "stock: asked again for the same firm, the market gives the one it has and makes no second");
        // a second firm gets its own id and ticker
        SxFirm g = SxMake(7001, 40000, 5000, 4000000000L, false, "Oak~2", "Nimbleloop North");
        FirmListing second = f.Listing;
        second.Place = "Oak~2";
        second.Name = "Nimbleloop";
        Stock other = f.Engine.ListFirm(second, out FirmWhy why2);
        Check(other != null && why2 == FirmWhy.None && other.Id != s.Id && other.Ticker != s.Ticker && f.Engine.MineFirms(new List<Stock>()) == 2 && f.Engine.MineListed() == null,
            "stock: a second firm of the player's gets an id and a ticker of its own (" + s.Ticker + ", " + (other != null ? other.Ticker : "-") + ")");
        Check(g.Stock == null, "stock: a firm not put on the market has none");
        // the cap: the market holds the player's firms only so many at once, and companies only so many
        FirmWhy last = FirmWhy.None;
        for (int i = 3; i <= MarketEnv.FirmsMost + 2 && last != FirmWhy.TooMany; i++)
        {
            FirmListing more = f.Listing;
            more.Place = "Elm~" + i;
            more.Name = "Firm " + (char)('A' + i);
            f.Engine.ListFirm(more, out last);
        }
        Check(last == FirmWhy.TooMany && f.Engine.MineFirms(new List<Stock>()) == MarketEnv.FirmsMost, "stock: the market takes at most " + MarketEnv.FirmsMost + " of a player's firms at once");
        SxFirm h = SxMake(7002);
        while (h.State.Stocks.Count < MarketCatalogue.CompaniesMost)
        {
            h.State.Stocks.Add(CloneGone(h.State, h.State.Stocks.Count));
        }
        FirmListing crowded = h.Listing;
        crowded.Place = "Ash~9";
        Stock none = h.Engine.ListFirm(crowded, out FirmWhy full);
        Check(none == null && full == FirmWhy.Full && h.State.Stocks.Count == MarketCatalogue.CompaniesMost, "stock: a market with as many companies as it may hold takes no more, whoever lists");
        // not the player's: a guest follows another's market and lists nothing
        SxFirm k = SxMake(7003, put: false);
        k.State.Following = true;
        Check(k.Engine.ListFirm(k.Listing, out FirmWhy following) == null && following == FirmWhy.NotMine, "stock: a market followed from a host's takes no firm of this machine's player");
        k.State.Following = false;
        k.State.Me = 0UL;
        Check(k.Engine.ListFirm(k.Listing, out FirmWhy nobody) == null && nobody == FirmWhy.NotMine, "stock: a market with no player of this machine's takes none either");
        // the index does not jump as a company joins it
        SxFirm m = SxMake(7004, put: false);
        RunTo(m.Engine, m.Day, MarketCatalogue.PhaseOpen + 3);
        int before = m.Engine.State.IndexLast;
        double divisor = m.Engine.State.IndexDivisor;
        m.Stock = m.Engine.ListFirm(m.Listing, out _);
        RunTo(m.Engine, m.Day, MarketCatalogue.PhaseOpen + 4);
        Check(m.Stock != null && Math.Abs(m.Engine.State.IndexLast - before) * 200 < before && m.Engine.State.IndexDivisor != divisor,
            "stock: a firm joining the market in a session moves the index no more than a step does (" + before + " to " + m.Engine.State.IndexLast + ")");
    }

    private static StockInfo Info(Stock s)
    {
        return new StockInfo { Live = s.Listed, Id = s.Id, Ticker = s.Ticker, Name = s.Name, Shares = s.Shares, Founder = s.Own != null ? s.Own.Founder : 0, Float = s.Own != null ? s.Own.Float : 0,
            Last = s.Last, LockDay = s.Own != null ? s.Own.LockDay : 0 };
    }

    // A company that has left, to fill a market's rooms with.
    private static Stock CloneGone(MarketState state, int n)
    {
        CompanyDef d = MarketCatalogue.All[n % MarketCatalogue.All.Length];
        var s = new Stock { Id = 5000 + n, Def = d, Name = d.Name, Ticker = d.Ticker, Sector = d.Sector, Size = d.Size, Status = StockStatus.Gone, GoneDay = 1, Shares = 1000000, Last = 100 };
        return s;
    }

    // ---------------------------------------------------------------- the firm's days: its own statements on the market's tape

    private static void ExitStockFigures()
    {
        SxFirm f = SxMake(7101, 50000, 8000);
        Stock s = f.Stock;
        if (s == null)
        {
            Check(false, "stock: made for the firm's figures");
            return;
        }
        int quarters = s.Results.Count;
        long rev = (long)s.Own.RevY;
        SxRun(f, 3 * SxYear / 2);
        Check(Math.Abs(s.Own.RevQ - 50000) < 2500 && Math.Abs(s.Own.NiQ - 8000 * (1 - ListingCatalogue.PayRefBp / 10000.0)) < 400,
            "figures: the stock follows the firm's own takings and profit, a day's from its statements (" + (int)s.Own.RevQ + ", " + (int)s.Own.NiQ + ")");
        Check(s.Results.Count > quarters && s.Results[s.Results.Count - 1].Quarter >= MarketCatalogue.QuarterOf(f.Day, SxYear) && s.Results[s.Results.Count - 1].RevenueK > 0,
            "figures: it reports quarter by quarter from the firm's books, as a player's company does (" + s.Results.Count + " results)");
        Check(f.World.Count(MarketNewsKind.Results) >= 1 && s.Own.Rps == 0.0 && s.DeclaredCents == 0 && s.DpsCents == 0, "figures: results come out, and no reserve builds and no dividend is declared on the stock");
        Check(f.World.Purse.In == 0 && f.World.Purse.Out == 0 && f.World.Purse.Forced == 0 && f.World.Purse.Money == 1000000000,
            "figures: the days of a firm that makes a profit move none of the player's cash through the market: its holders' share stays in the firm's account");
        // a loss day is followed too
        SxFirm l = SxMake(7102, 30000, -20000);
        RunTo(l.Engine, l.Day + 60);
        Check(l.Stock != null && l.Stock.Own.NiQ < 0 && l.Stock.Own.RevQ > 25000 && l.World.Purse.Money == 1000000000, "figures: a firm that burns cash is followed too, at a loss, and the stock moves no cash for it");
        Check(l.Stock.Distress > 0.0 && l.Stock.Last > 0 && l.Stock.Listed, "figures: ...and the market reads its distress, it never fails or leaves by itself (the founder's company is not a catalogue company)");
        // two firms: each its own statements
        SxFirm a = SxMake(7103, 90000, 20000);
        FirmListing second = a.Listing;
        second.Place = "Oak~2";
        second.Name = "Quillworks";
        second.RevDay = 9000;
        second.ProfitDay = 500;
        second.Books = SxBooks(a.Day, 9000, 500);
        Stock b = a.Engine.ListFirm(second, out _);
        a.World.Feeds[a.Stock.Id] = new[] { 90000.0, 20000.0 };
        a.World.Feeds[b.Id] = new[] { 9000.0, 500.0 };
        RunTo(a.Engine, a.Day + 45);
        Check(Math.Abs(a.Stock.Own.RevQ - 90000) < 4500 && Math.Abs(b.Own.RevQ - 9000) < 450 && a.Stock.Own.RevQ > b.Own.RevQ * 8,
            "figures: two firms of one player are followed each by its own statements, never by each other's (" + (int)a.Stock.Own.RevQ + ", " + (int)b.Own.RevQ + ")");
        // the whole company beside a firm: neither feeds the other
        var world = new OwnWorld(7104);
        MarketEngine both = LListed(7104, world, 2500, out Stock company);
        if (company != null)
        {
            FirmListing r = SxMake(7104, put: false).Listing;
            r.Place = "Ash~3";
            r.Name = "Fernlight";
            r.RevDay = 1000;
            r.ProfitDay = 100;
            r.Books = SxBooks(both.State.Day, 1000, 100);
            r.LockDay = both.State.Day + 100;
            Stock firm = both.ListFirm(r, out FirmWhy why);
            Check(firm != null && both.MineListed() == company && both.MineFirms(new List<Stock>()) == 1, "figures: the whole company and a firm are both on the market; the company is still the player's company");
            double firmRev = firm.Own.RevQ;
            RunTo(both, both.State.Day + 40);
            Check(company.Own.RevQ > 40000 && Math.Abs(firm.Own.RevQ - firmRev) < firmRev * 0.2 + 1 && firm.Own.RevQ < 5000,
                "figures: the company's takings do not feed the firm's stock (" + (int)company.Own.RevQ + " the company's, " + (int)firm.Own.RevQ + " the firm's)");
        }
        // a firm with no feed (the tech sector off) is not followed and nothing breaks
        SxFirm quiet = SxMake(7105, 50000, 8000);
        quiet.World.Silent = true;
        double was = quiet.Stock.Own.RevQ;
        RunTo(quiet.Engine, quiet.Day + 50);
        Check(quiet.Stock.Own.RevQ == was && quiet.Stock.Listed && quiet.Stock.Last > 0, "figures: with no statements given for it, the stock stands on its last figures and nothing breaks");
        // the tape: the day's env with the firms, and old readers
        var e = MarketEnv.Quiet();
        var t = new StringBuilder();
        MarketTape.WriteEnv(t, e);
        string plain = t.ToString();
        e.Firms = 2;
        e.FirmFrom = 120;
        e.FirmDays = 3;
        e.FirmStock = new[] { 1011, 1512 };
        e.FirmRev = new[] { 123456789012L, 777L };
        e.FirmProfit = new[] { -98765L, 42L };
        e.FirmDebt = new[] { 5000L, 0L };
        e.OwnStanding = 61;
        t.Length = 0;
        MarketTape.WriteEnv(t, e);
        string line = t.ToString();
        Check(MarketTape.ReadEnv(line, out MarketEnv r2) && r2.Firms == 2 && r2.FirmFrom == 120 && r2.FirmDays == 3 && r2.FirmStock[0] == 1011 && r2.FirmStock[1] == 1512 && r2.FirmRev[0] == 123456789012L
              && r2.FirmRev[1] == 777 && r2.FirmProfit[0] == -98765 && r2.FirmProfit[1] == 42 && r2.FirmDebt[0] == 5000 && r2.OwnStanding == 61,
            "tape: a day with firms reads back exactly: the window, each stock's takings, profit and debt");
        string[] columns = line.Split(','), plainColumns = plain.Split(',');
        int own = plainColumns.Length;   // where the eight columns of the player's own company begin
        Check(columns.Length == own + 8 + 3 + 2 * 4 && columns[own + 4] == "61", "tape: the firms' columns come after the eight an older reader stops at, so it reads the day it always did (" + columns.Length + " columns)");
        Check(MarketTape.ReadEnv(plain, out MarketEnv q) && q.Firms == 0 && q.FirmStock == null, "tape: a day with no firm is written and read as before");
        var cut = new StringBuilder();
        for (int i = 0; i < own + 8; i++)
        {
            cut.Append(i > 0 ? "," : string.Empty).Append(columns[i]);
        }
        Check(MarketTape.ReadEnv(cut.ToString(), out MarketEnv old) && old.Firms == 0 && old.OwnStanding == 61, "tape: the day an older version wrote or cut at its own columns reads with no firm");
        e.Firms = 0;
        t.Length = 0;
        MarketTape.WriteEnv(t, e);
        Check(MarketTape.ReadEnv(t.ToString(), out MarketEnv none) && none.Firms == 0, "tape: a day whose firms have gone is written without them");
        // a replay plays the same days: the tape keeps the firm's figures
        SxFirm p = SxMake(7106, 50000, 8000);
        RunTo(p.Engine, p.Day + 20);
        string text = p.Engine.Tape.Whole();
        Check(text.Contains("," + p.Stock.Id + ","), "tape: the firm's stock is in the tape's days");
        MarketState saved = MarketStore.Read(MarketStore.Write(p.State), out _);
        long sumLast = p.Stock.Last;
        double revQ = p.Stock.Own.RevQ;
        RunTo(p.Engine, p.Day + 40);
        var twinWorld = new StockWorld(7106);
        saved.Me = SxMe;
        twinWorld.State = () => saved;
        var twin = new MarketEngine(saved, MarketTape.Read(p.Engine.Tape.Id, p.Engine.Tape.Whole()), twinWorld);
        twin.HoldAfterLoad();
        RunTo(twin, p.Engine.State.Day);
        Stock again = saved.Stock(p.Stock.Id);
        Check(again != null && again.Last == p.Stock.Last && again.Own.RevQ == p.Stock.Own.RevQ && again.Shares == p.Stock.Shares,
            "tape: a load replays the firm's days as they were played, from the figures the tape kept (" + p.Stock.Last + ", " + (again != null ? again.Last : 0) + ")");
    }
}
