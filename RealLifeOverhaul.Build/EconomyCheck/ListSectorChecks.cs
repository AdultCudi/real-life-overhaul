using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Markets;

// The sector a player's listed company is in (Markets/Listing/ListingSector, MarketEngine.Own): a player reported their
// company stayed in Restaurants and cafes, their first business's sector, long after they sold it and earned in jewellery,
// clothing and tech, and the share price followed restaurants. The trade's sector, the mix by trade, the quarter's review
// with no flicker at the boundary, a sector left at once when nothing is earned in it, the engine on every machine, the
// save, a save from before the mix (moved at the load), the way to the market, the sources, the test and the texts.
internal static partial class Program
{
    private static void ListSector()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        ListSectorTrades();
        ListSectorReviews();
        ListSectorBooks();
        ListSectorEngine();
        ListSectorSave();
        ListSectorSources();
        ListSectorTexts();
        Console.WriteLine("  a listed company's sector: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static int Trade(string id) => Array.IndexOf(MarketInputs.Trades, id);

    // A quarter of market mornings: the takings a day by trade into the mix.
    private static void LQuarter(OwnListing o, params (string trade, int sales)[] takings)
    {
        MarketEnv e = MarketEnv.Quiet();
        e.Trades = 0;
        foreach ((string trade, int sales) in takings)
        {
            e.TradeOf[e.Trades] = MarketInputs.TradeOf(trade);
            e.TradeSales[e.Trades] = sales;
            e.Trades++;
        }
        int days = (int)Math.Ceiling(MarketCatalogue.TradingDaysPerYear(LYear) / 4.0);
        for (int d = 0; d < days; d++)
        {
            ListingSector.Take(o, e, LYear);
        }
    }

    private static void ListSectorTrades()
    {
        Check(ListingSector.SectorOf(Trade("coffeeshop")) == MarketSector.Restaurants && ListingSector.SectorOf(Trade("fastfood")) == MarketSector.Restaurants,
            "listing sector: cafes and fast food are restaurants");
        Check(ListingSector.SectorOf(Trade("jewelry")) == MarketSector.Specialists && ListingSector.SectorOf(Trade("clothing")) == MarketSector.Stores
              && ListingSector.SectorOf(Trade("webdevelopment")) == MarketSector.Software && ListingSector.SectorOf(Trade("construction")) == MarketSector.Construction,
            "listing sector: a trade is filed under the catalogue's first company in it (jewellery, clothing, software, construction)");
        Check(ListingSector.SectorOf(Trade("florist")) == MarketSector.Specialists && ListingSector.SectorOf(Trade("cardealership")) == MarketSector.Specialists
              && ListingSector.SectorOf(Trade("eventplanning")) == MarketSector.Leisure && ListingSector.SectorOf(-1) == MarketSector.Services
              && ListingSector.SectorOf(MarketInputs.Trades.Length) == MarketSector.Services, "listing sector: the trades no company is in have a sector, and none is business services");
        bool tech = true;
        foreach (string type in new[] { "reallife:businesstype_softwarestudio", "reallife:businesstype_gamestudio", "reallife:businesstype_itconsultancy", "reallife:businesstype_cybersecurity",
                     "reallife:businesstype_ailab" })
        {
            tech &= MarketInputs.TradeOf(type) == Trade("webdevelopment");
        }
        Check(tech && MarketInputs.TradeOf("ba:businesstype_webdevelopmentagency") == Trade("webdevelopment") && MarketInputs.TradeOf("ba:businesstype_jewelrystore") == Trade("jewelry")
              && MarketInputs.TradeOf("reallife:businesstype_construction") == Trade("construction") && MarketInputs.TradeOf("ba:businesstype_headquarters") == -1,
            "listing sector: the tech sector's five firms count as software, as the web development agency they are furnished as; the game's own types as before");
    }

    private static void ListSectorReviews()
    {
        // The player's case: listed under restaurants, the cafe sold, now jewellery, clothing and a software studio.
        var o = new OwnListing { Mixed = true };
        int current = (int)MarketSector.Restaurants;
        LQuarter(o, ("ba:businesstype_coffeeshop", 4000));
        Check(ListingSector.Review(o, current, false, out int _) < 0, "listing review: a company still earning in its sector stays");
        LQuarter(o, ("ba:businesstype_jewelrystore", 9000), ("ba:businesstype_clothingstore", 6000), ("reallife:businesstype_softwarestudio", 3000));
        int to = ListingSector.Review(o, current, false, out int trade);
        Check(to == (int)MarketSector.Specialists && trade == Trade("jewelry"),
            "listing review: a quarter with nothing earned in its sector moves it at once, to the sector it earns most in now (" + to + ", trade " + trade + ")");
        Check(o.Lead == -1 && o.LeadCount == 0 && o.Had == 0, "listing review: a review starts the next one afresh");

        // No flicker: a company split near evenly between two sectors stays, quarter after quarter.
        var even = new OwnListing { Mixed = true };
        int sec = (int)MarketSector.Specialists, moves = 0;
        for (int q = 0; q < 12; q++)
        {
            LQuarter(even, ("ba:businesstype_jewelrystore", q % 2 == 0 ? 5200 : 4800), ("ba:businesstype_clothingstore", q % 2 == 0 ? 4800 : 5200));
            int next = ListingSector.Review(even, sec, false, out int _);
            if (next >= 0)
            {
                sec = next;
                moves++;
            }
        }
        Check(moves == 0 && sec == (int)MarketSector.Specialists, "listing review: 52 to 48 one quarter and 48 to 52 the next, twelve quarters: never moved (" + moves + ")");

        // A lead must hold at two reviews in a row; one lead and back is not enough.
        var lead = new OwnListing { Mixed = true };
        LQuarter(lead, ("ba:businesstype_jewelrystore", 5000), ("ba:businesstype_clothingstore", 5000));
        int first = ListingSector.Review(lead, (int)MarketSector.Specialists, false, out int _);
        LQuarter(lead, ("ba:businesstype_jewelrystore", 3000), ("ba:businesstype_clothingstore", 7000));
        int second = ListingSector.Review(lead, (int)MarketSector.Specialists, false, out int _);
        Check(first < 0 && second < 0 && lead.Lead == (int)MarketSector.Stores && lead.LeadCount == 1, "listing review: a tie stays, and a first lead only counts");
        LQuarter(lead, ("ba:businesstype_jewelrystore", 3000), ("ba:businesstype_clothingstore", 7000));
        Check(ListingSector.Review(lead, (int)MarketSector.Specialists, false, out int t2) == (int)MarketSector.Stores && t2 == Trade("clothing"),
            "listing review: a lead held at a second review moves it");
        var back = new OwnListing { Mixed = true };
        LQuarter(back, ("ba:businesstype_jewelrystore", 3000), ("ba:businesstype_clothingstore", 7000));
        int once = ListingSector.Review(back, (int)MarketSector.Specialists, false, out int _);
        LQuarter(back, ("ba:businesstype_jewelrystore", 9000), ("ba:businesstype_clothingstore", 1000));
        int after = ListingSector.Review(back, (int)MarketSector.Specialists, false, out int _);
        LQuarter(back, ("ba:businesstype_jewelrystore", 3000), ("ba:businesstype_clothingstore", 7000));
        Check(once < 0 && after < 0 && ListingSector.Review(back, (int)MarketSector.Specialists, false, out int _) < 0,
            "listing review: a lead that falls back starts its count again");

        // Nothing earned, or a company whose mix is not followed (listed by an older version): nothing moves.
        var idle = new OwnListing { Mixed = true };
        LQuarter(idle);
        var old = new OwnListing();
        LQuarter(old, ("ba:businesstype_jewelrystore", 9000));
        Check(ListingSector.Review(idle, current, false, out int _) < 0 && ListingSector.Review(old, current, false, out int _) < 0 && old.Mix[Trade("jewelry")] == 0.0,
            "listing review: nothing earned moves nothing, and a company listed by an older version is not followed (as its founder's machine does not)");
        Check(ListingSector.Weight(LYear) > 0.0 && ListingSector.Weight(LYear) < 1.0 && ListingSector.Weight(365) < ListingSector.Weight(LYear),
            "listing review: a day weighs less in a longer year's quarter");
        int review = ListingSector.NextReview(200, LYear);
        Check(review > 200 && MarketCatalogue.TradingDay(review) && MarketCatalogue.QuarterOf(review, LYear) == MarketCatalogue.QuarterOf(200, LYear) + 1
              && MarketCatalogue.QuarterOf(review - 1, LYear) <= MarketCatalogue.QuarterOf(200, LYear) + 1, "listing review: the next review is the next quarter's first market day");
        var top = new OwnListing { Mixed = true };
        LQuarter(top, ("ba:businesstype_jewelrystore", 6000), ("ba:businesstype_clothingstore", 3000), ("reallife:businesstype_gamestudio", 1000));
        int[] sectors = new int[3], bps = new int[3];
        int n = ListingSector.Top(top, sectors, bps);
        Check(n == 3 && sectors[0] == (int)MarketSector.Specialists && sectors[1] == (int)MarketSector.Stores && sectors[2] == (int)MarketSector.Software && Math.Abs(bps[0] - 6000) <= 1
              && bps[0] + bps[1] + bps[2] >= 9998, "listing review: the page's mix is its sectors, the most first, with their shares");
        var watch = Stopwatch.StartNew();
        var busy = new OwnListing { Mixed = true };
        MarketEnv e = MarketEnv.Quiet();
        e.Trades = MarketEnv.TradesMost;
        for (int i = 0; i < e.Trades; i++)
        {
            e.TradeOf[i] = i * 3;
            e.TradeSales[i] = 1000 + i;
        }
        for (int i = 0; i < 100000; i++)
        {
            ListingSector.Take(busy, e, LYear);
        }
        Check(watch.ElapsedMilliseconds < 400, "listing review: a market morning's mix costs little (100,000 mornings in " + watch.ElapsedMilliseconds + " ms)");
    }

    private static void ListSectorBooks()
    {
        // A save from before the mix: the last quarter's books move it at once, with no second review.
        long[] books = new long[MarketInputs.Trades.Length];
        books[Trade("jewelry")] = 450000;
        books[Trade("clothing")] = 300000;
        books[Trade("webdevelopment")] = 150000;
        var o = new OwnListing();
        Check(ListingSector.Seed(o, books, 15, false) && o.Mixed && o.Mix[Trade("jewelry")] == 30000.0, "listing books: an older listing's mix is begun from its books, a day's takings each");
        Check(ListingSector.Review(o, (int)MarketSector.Restaurants, true, out int trade) == (int)MarketSector.Specialists && trade == Trade("jewelry"),
            "listing books: listed under restaurants with no cafe left, it moves to specialist shops at once");
        Check(!ListingSector.Seed(o, books, 15, false) && ListingSector.Seed(o, books, 15, true), "listing books: a followed mix is begun again only when asked (the test)");
        Check(ListingSector.FromBooks(books, (int)MarketSector.Restaurants) == (int)MarketSector.Specialists && ListingSector.FromBooks(books, (int)MarketSector.Specialists) == (int)MarketSector.Specialists,
            "listing books: the books' sector, and the one it is in when that still leads");
        long[] close = new long[MarketInputs.Trades.Length];
        close[Trade("jewelry")] = 520;
        close[Trade("clothing")] = 480;
        Check(ListingSector.FromBooks(close, (int)MarketSector.Specialists) == (int)MarketSector.Specialists && ListingSector.FromBooks(close, (int)MarketSector.Stores) == (int)MarketSector.Stores,
            "listing books: near even books keep it where it is either way");
        Check(!ListingSector.Seed(new OwnListing(), new long[MarketInputs.Trades.Length], 15, false) && !ListingSector.Seed(new OwnListing(), books, 0, false),
            "listing books: nothing in the books begins nothing");
        Check(ListingSector.Leading(books, out int lt) == (int)MarketSector.Specialists && lt == Trade("jewelry") && ListingSector.Leading(new long[MarketInputs.Trades.Length], out int none) == -1
              && none == -1, "listing books: on the way to the market, the sector the books are most in, none for empty books");
    }

    private static void LTrades(OwnWorld w, params (string trade, int sales)[] takings)
    {
        w.City.Trades = 0;
        foreach ((string trade, int sales) in takings)
        {
            w.City.TradeOf[w.City.Trades] = MarketInputs.TradeOf(trade);
            w.City.TradeSales[w.City.Trades] = sales;
            w.City.Trades++;
        }
    }

    private static void ListSectorEngine()
    {
        var w1 = new OwnWorld(141);
        var w2 = new OwnWorld(141);
        LTrades(w1, ("ba:businesstype_coffeeshop", 50000));
        LTrades(w2, ("ba:businesstype_coffeeshop", 50000));
        MarketEngine a = LListed(141, w1, 3000, out Stock ka);
        MarketEngine b = LListed(141, w2, 3000, out Stock kb);
        if (ka == null || kb == null)
        {
            Check(false, "the companies are listed for the sector's engine checks");
            return;
        }
        b.State.Me = LGuest;   // another machine of the session: the same company, another player
        Check(ka.Sector == MarketSector.Restaurants && ka.Own.Mixed, "listing engine: a new listing is in its prospectus's sector, its business followed");
        int q = MarketCatalogue.QuarterOf(a.State.Day, LYear), firstReview = ListingSector.NextReview(a.State.Day, LYear);
        RunTo(a, firstReview);   // to the close of the review's day: the cafe's takings stop with it
        RunTo(b, firstReview);
        Check(ka.Sector == MarketSector.Restaurants && w1.Count(MarketNewsKind.OwnSector) == 0, "listing engine: still a cafe owner at the review, still restaurants");
        int seen = a.State.Day;
        LTrades(w1, ("ba:businesstype_jewelrystore", 30000), ("ba:businesstype_clothingstore", 15000), ("reallife:businesstype_softwarestudio", 10000));
        LTrades(w2, ("ba:businesstype_jewelrystore", 30000), ("ba:businesstype_clothingstore", 15000), ("reallife:businesstype_softwarestudio", 10000));
        int[] levels = (int[])a.State.SectorLast.Clone();
        RunTo(a, MarketCatalogue.QuarterStart(q + 2, LYear) + 3);
        RunTo(b, MarketCatalogue.QuarterStart(q + 2, LYear) + 3);
        MarketNews heard = w1.Last(MarketNewsKind.OwnSector);
        Check(ka.Sector == MarketSector.Specialists && ka.Def.Sector == MarketSector.Specialists && ka.Def.Trade == "jewelry" && w1.Count(MarketNewsKind.OwnSector) == 1
              && heard.Stock == ka.Id && heard.A == (int)MarketSector.Restaurants && heard.B == (int)MarketSector.Specialists && heard.Day > seen,
            "listing engine: the cafe sold, at the next review it moves from restaurants to specialist shops, and the city hears of it once (" + ka.Sector + ")");
        Check(a.State.Listing.Sector == (int)MarketSector.Specialists && a.State.Listing.Trade == Trade("jewelry"), "listing engine: the founder's own file says the new sector and trade");
        Check(kb.Sector == ka.Sector && kb.Last == ka.Last && kb.Own.Rps == ka.Own.Rps && b.State.Listing.Sector == (int)MarketSector.Restaurants,
            "listing engine: every machine moves it at the same moment to the same price, and another machine's own file is not touched (" + ka.Last + ", " + kb.Last + ")");
        bool steady = true;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            int was = levels[sec], now = a.State.SectorLast[sec];
            steady &= was <= 0 || now <= 0 || Math.Abs(now - was) * 2 < was;
        }
        Check(steady, "listing engine: no sector's index jumped when it moved");
        RunTo(a, MarketCatalogue.QuarterStart(q + 5, LYear) + 3);
        Check(ka.Sector == MarketSector.Specialists && w1.Count(MarketNewsKind.OwnSector) == 1, "listing engine: the same business, three reviews on: it stays");

        // A record from before the mix (an older save): moved at the load by its books, then followed.
        var w3 = new OwnWorld(151);
        MarketEngine c = LListed(151, w3, 3000, out Stock kc);
        if (kc == null)
        {
            Check(false, "the company is listed for the older save's check");
            return;
        }
        kc.Own.Mixed = false;
        Array.Clear(kc.Own.Mix, 0, kc.Own.Mix.Length);
        long[] books = new long[MarketInputs.Trades.Length];
        books[Trade("jewelry")] = 600000;
        books[Trade("clothing")] = 200000;
        int news = w3.Count(MarketNewsKind.OwnSector);
        Check(c.SectorFromBooks(kc, books, 15, false) && kc.Sector == MarketSector.Specialists && kc.Own.Mixed && w3.Count(MarketNewsKind.OwnSector) == news + 1
              && c.State.Listing.Sector == (int)MarketSector.Specialists, "listing engine: an older save's company in restaurants moves to specialist shops at its load, with its notice");
        Check(!c.SectorFromBooks(kc, books, 15, false), "listing engine: and only once: it is followed after");
        var w4 = new OwnWorld(161);
        MarketEngine d = LListed(161, w4, 3000, out Stock kd);
        kd.Own.Mixed = false;
        Check(!d.SectorFromBooks(kd, new long[MarketInputs.Trades.Length], 15, false) && kd.Own.Mixed && kd.Sector == MarketSector.Restaurants,
            "listing engine: an older save's company with empty books stays, followed from now");
    }

    private static void ListSectorSave()
    {
        var w = new OwnWorld(171);
        LTrades(w, ("ba:businesstype_jewelrystore", 30000), ("ba:businesstype_clothingstore", 29000));
        MarketEngine m = LListed(171, w, 3000, out Stock own);
        if (own == null)
        {
            Check(false, "the company is listed for the sector's save checks");
            return;
        }
        RunTo(m, m.State.Day + 10);
        own.Own.Lead = (int)MarketSector.Stores;
        own.Own.LeadCount = 1;
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Stock again = back != null ? back.Stock(own.Id) : null;
        bool mix = again != null && again.Own != null && again.Own.Mixed;
        for (int t = 0; mix && t < own.Own.Mix.Length; t++)
        {
            mix &= again.Own.Mix[t] == own.Own.Mix[t];
        }
        Check(mix && again.Sector == own.Sector && again.Def.Trade == own.Def.Trade && again.Own.Had == own.Own.Had && again.Own.Lead == own.Own.Lead && again.Own.LeadCount == 1,
            "listing save: the sector, trade, mix exactly, the sectors earned in and a lead under way come back");
        Check(MarketStore.Write(back) == line, "listing save: written again, the line is the same");
        string record = MarketStore.StockLine(m.State, own);
        int cut = record.LastIndexOf('|');
        Stock older = MarketStore.ReadStockLine(record.Substring(0, cut), LYear);
        Check(older != null && older.Own != null && !older.Own.Mixed && older.Sector == own.Sector && older.Own.Founder == own.Own.Founder,
            "listing save: a record of an older version, without the mix, reads as before, not followed");
        own.Own.Mixed = false;
        string plain = MarketStore.StockLine(m.State, own);
        Stock unfollowed = MarketStore.ReadStockLine(plain, LYear);
        Check(plain.EndsWith("|", StringComparison.Ordinal) && unfollowed != null && !unfollowed.Own.Mixed, "listing save: a company not followed writes an empty part and reads back the same");
        Stock damaged = MarketStore.ReadStockLine(record.Substring(0, cut) + "|x,y", LYear);
        Check(damaged != null && damaged.Own != null && !damaged.Own.Mixed, "listing save: a damaged mix is left unfollowed, the company kept");
    }

    private static void ListSectorSources()
    {
        string engine = ModSource(@"Markets\MarketEngine.Own.cs");
        string business = MethodText(engine, "private void OwnBusiness(Stock s, in MarketEnv env, int from, int day)");
        Check(business.Contains("ListingSector.Take(s.Own, env, Year);") && business.Contains("ListingSector.Review(s.Own, (int)s.Sector, false, out int trade)")
              && business.IndexOf("ListingRules.CloseQuarter(", StringComparison.Ordinal) < business.IndexOf("ListingSector.Review(", StringComparison.Ordinal),
            "listing sources: each market morning feeds the mix, and the review comes at the quarter's turn, on every machine");
        string resector = MethodText(engine, "private void Resector(Stock s, MarketSector to, int trade, int day, in MarketEnv env)");
        Check(resector.Contains("SectorWeigh((int)from, value, false);") && resector.Contains("SectorWeigh((int)to, value, true);") && resector.Contains("MarketCatalogue.PlayerDef(")
              && resector.Contains("s.Own.Owner == State.Me") && resector.Contains("MarketNewsKind.OwnSector"), "listing sources: a move keeps the indices level, remakes its profile, writes only the founder's own file, and is news");
        string listing = ModSource(@"Markets\MarketDirector.Listing.cs");
        string named = MethodText(listing, "private void Named(ListingFile f)");
        Check(!listing.Contains("f.Trade < 0") && named.Contains("f.Stage <= ListingStage.Audited") && named.Contains("Classify(f);") && listing.Contains("ListingSector.Leading(_takings, out int trade)"),
            "listing sources: on the way to the market the sector follows the takings until the prospectus, not the first business read");
        string own = ModSource(@"Markets\MarketDirector.Own.cs");
        Check(MethodText(own, "private void OwnStarted(GameInstance save)").Contains("SectorAtLoad(save.Day);") && MethodText(own, "private void SectorAtLoad(int day)").Contains("_engine.SectorFromBooks(own, _takings, days, false)")
              && own.Contains("case MarketNewsKind.OwnSector:") && own.Contains("\"reallife_lst_note_sector\""), "listing sources: a load moves an older listing by its books, and the founder is told");
        string links = ModSource(@"Wiring\ListingLinks.cs");
        string takings = MethodText(links, "private static void TradeTakings(int from, int to, long[] into)");
        Check(links.Contains("MarketBridges.TradeTakings = TradeTakings;") && links.Contains("MarketBridges.TradeTakings = null;") && takings.Contains("Books.Businesses(Businesses);")
              && takings.Contains("MarketInputs.TradeOf(r.businessTypeName)") && takings.Contains("MarketsBuildLinks.ConstructionBooks(") && !links.Contains("private static int Trade()"),
            "listing sources: the books by trade read every business of the player's own (Going Public: Books.Businesses), a builder's works too");
        string types = ModSource(@"Tech\Firms\TechTypes.cs"), inputs = ModSource(@"Markets\MarketInputs.cs");
        bool same = true;
        foreach (string kind in new[] { "softwarestudio", "gamestudio", "itconsultancy", "cybersecurity", "ailab" })
        {
            same &= types.Contains("Prefix + \"" + kind + "\"") && inputs.Contains("\"reallife:businesstype_" + kind + "\"");
        }
        Check(same && types.Contains("public const string Prefix = \"reallife:businesstype_\";"), "listing sources: the market's tech types are the tech sector's own five");
        string tests = ModSource(@"Markets\MarketDirector.OwnTests.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), guide = ModSource(@"Markets\Listing\ListingGuide.cs");
        Check(tests.Contains("const string Test = \"Listing check (sector)\";") && tests.Contains("Test + \": from \" + was + \" to \" + now") && tests.Contains("(pass ? \"PASS\" : \"FAIL\")")
              && tests.Contains("\"reallife_lst_test_sector_session\"") && tests.Contains("\"reallife_lst_test_sector_none\"") && tests.Contains("\"reallife_lst_test_sector_book\"")
              && mod.Contains(".AddButton(\"reallife_lst_test_sector\", () => Markets.MarketDirector.Instance?.TestSector())") && guide.Contains("\"reallife_lst_test_sector\")"),
            "listing sources: Options > Tests checks the sector and logs \"Listing check (sector): from X to Y\", or says why it cannot");
        string app = ModSource(@"Markets\MarketApp.Own.cs");
        Check(app.Contains("box.Line(SectorLine(s, k));") && app.Contains("ListingSector.NextReview(s.Day, s.YearDays)") && app.Contains("\"reallife_lst_box_sector_plain\""),
            "listing sources: the company's page shows its sector, its takings by sector and the next review");
        Check(ModSource(@"Ledger\LedgerCatalogue.cs").Contains("K(\"_mkt_news_own_sector\", LedgerSection.Markets, 50, \"own_sector\")")
              && ModSource(@"Markets\MarketText.cs").Contains("return \"reallife_mkt_news_own_sector\";"), "listing sources: the City Ledger prints the move");
    }

    private static void ListSectorTexts()
    {
        string[] keys =
        {
            "reallife_mkt_news_own_sector", "reallife_ledger_head_own_sector", "reallife_lst_note_sector", "reallife_lst_box_sector", "reallife_lst_box_sector_plain", "reallife_lst_test_sector",
            "reallife_lst_test_sector_session", "reallife_lst_test_sector_book", "reallife_lst_test_sector_none", "reallife_lst_test_sector_done"
        };
        Check(AllIn(keys, out string missing), "listing sector texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = true;
        foreach (string key in new[] { "reallife_lst_note_sector", "reallife_lst_test_sector_session", "reallife_lst_test_sector_book", "reallife_lst_test_sector_none", "reallife_lst_test_sector_done" })
        {
            fits &= Fits(en, key);
        }
        Check(fits, "listing sector texts: the notices are 240 characters or fewer");
        bool holes = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LocaleFile(code);
            holes &= words["reallife_mkt_news_own_sector"].Contains("{name}") && words["reallife_mkt_news_own_sector"].Contains("{from}") && words["reallife_mkt_news_own_sector"].Contains("{to}")
                     && words["reallife_ledger_head_own_sector"].Contains("{to}") && words["reallife_lst_note_sector"].Contains("{from}") && words["reallife_lst_note_sector"].Contains("{to}")
                     && words["reallife_lst_box_sector"].Contains("{mix}") && words["reallife_lst_box_sector"].Contains("{day}") && words["reallife_lst_box_sector_plain"].Contains("{sector}")
                     && words["reallife_lst_test_sector_done"].Contains("{from}") && words["reallife_lst_test_sector_done"].Contains("{to}");
        }
        Check(holes, "listing sector texts: the words carry what the code fills in, in each language");
        Check(FixLineEverywhere("kept the sector of your first business for good", "ficava para sempre no setor do seu primeiro negócio", "会永远留在你第一家企业所属的板块",
                "blieb für immer in der Branche deines ersten Geschäfts", "restait pour toujours dans le secteur de votre première entreprise",
                "se quedaba para siempre en el sector de tu primer negocio", "навсегда оставалась в секторе вашего первого бизнеса",
                "bleef voorgoed in de sector van je eerste zaak", "ilk işletmenizin sektöründe sonsuza dek kalıyordu"),
            "listing sector texts: What's new tells of the fix in every language, behind its fix mark, in any row");
    }
}
