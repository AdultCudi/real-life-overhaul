using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The cost basis of a stake the player sells, and the sales that were still untaxed (ExitGains, TechDirector.ExitGains, MarketEngine.Own). Shares the founder buys back carry what
// they cost, on every path that sells them; and the whole listed company's founder is taxed on a deal on the market and on a bid that wins, as a firm's founder is. The market is
// driven through its engine, the gains are worked by the game-free rules, the game's side is read from the sources.
internal static partial class Program
{
    private static void ExitBasisChecks()
    {
        ExitBasisRules();
        ExitBasisFirmDeals();
        ExitBasisCompanyDeals();
        ExitBasisCompanyBids();
        ExitBasisSave();
        ExitBasisWiring();
    }

    private static void ExitBasisRules()
    {
        const long paidIn = 30000000, cost = 20000000, fee = 500000, price = 100000000;
        Cap c = TaxCap(paidIn);
        long held = ExitGains.PlayerShares(c), back = held / 4, all = held + back, total = cost + fee;
        Func<GainFile> bought = () =>
        {
            var g = new GainFile();
            ExitGains.ForMarketDeal(g, paidIn, "Main~1", "Firm", cost / 100, fee / 100, held, back, 40, TaxLevel);
            return g;
        };
        GainFile first = bought();
        Check(first.Lines.Count == 0 && first.Basis.Count == 1 && first.Basis[0].Bought == total && first.Basis[0].Used == 0 && first.Basis[0].Sold == 0 && ExitGains.Between(first, 0, 999) == 0,
            "exit basis: shares bought back note no gain and use none of the basis, and what they cost with the commission joins it (" + (first.Basis.Count > 0 ? first.Basis[0].Bought : -1) + ")");
        long want = price - paidIn - total;
        long viaPage = ExitGains.ForShares(bought(), c, "Main~1", "Firm", price, all, all, 50, TaxLevel);
        long viaMarket = ExitGains.ForMarketDeal(bought(), paidIn, "Main~1", "Firm", (price + 1000000) / 100, 1000000 / 100, all, -all, 50, TaxLevel);
        long viaOffer = ExitGains.ForSale(bought(), c, "Main~1", "Firm", price, 0, 50, TaxLevel);
        Check(want == 49500000 && viaPage == want && viaMarket == want && viaOffer == want,
            "exit basis: the same stake sold on the Exits page, on the market and to a buyer's offer has one gain, the price less what was paid in and what the shares bought back cost ("
            + viaPage + ", " + viaMarket + ", " + viaOffer + " of " + want + ")");
        Check(viaPage < price - paidIn, "exit basis: shares bought back are no longer taxed as if they cost nothing (" + viaPage + " against " + (price - paidIn) + ")");
        GainFile parts = bought();
        long left = all, gains = 0, whole = 0, slice = Math.Max(1, all / 7);
        int n = 0;
        while (left > 0 && n < 100)
        {
            long take = Math.Min(slice, left), got = price * take / all;
            gains += ExitGains.ForShares(parts, c, "Main~1", "Firm", got, left, take, 60 + n, TaxLevel);
            whole += got;
            left -= take;
            n++;
        }
        BasisLine used = parts.BasisOf("Main~1", false);
        Check(left == 0 && used != null && used.Used == paidIn + total && gains == whole - paidIn - total,
            "exit basis: the stake sold in " + n + " slices after the purchase uses the whole basis once and no more (" + (used != null ? used.Used : -1) + "), gain " + gains + " of " + whole);
        var mix = new GainFile();
        ExitGains.ForShares(mix, c, "Main~1", "Firm", price / 2, held, held / 2, 70, TaxLevel);
        ExitGains.ForMarketDeal(mix, paidIn, "Main~1", "Firm", cost / 100, fee / 100, held - held / 2, back, 71, TaxLevel);
        long rest = ExitGains.Slice(mix, paidIn, "Main~1", held - held / 2 + back, held - held / 2 + back);
        Check(mix.Basis[0].Used > 0 && rest == paidIn - mix.Basis[0].Used + total && rest <= paidIn + total,
            "exit basis: shares sold, then bought back, then sold again leave exactly what is unused of the paid in cost and the purchase (" + rest + ")");
        var lv = new GainFile();
        ExitGains.ForMarketDeal(lv, paidIn, "Main~1", "Firm", cost / 100, fee / 100, held, back, 40, 1500);
        long atStart = lv.Basis[0].Bought;
        Check(atStart == total * 1000 / 1500 && ExitGains.Level(atStart, 1500) <= total && ExitGains.Level(atStart, 1500) >= total - 2,
            "exit basis: a purchase is kept at the start's prices and levelled back to today's, as the paid in cost is (" + atStart + ")");
        var none = new GainFile();
        Check(ExitGains.ForMarketDeal(none, 0, "Main~1", "Firm", 0, 0, held, back, 1, TaxLevel) == 0 && ExitGains.ForMarketDeal(none, 0, "Main~1", "Firm", -5, 0, held, back, 1, TaxLevel) == 0
              && none.Basis.Count == 0 && ExitGains.ForMarketDeal(null, 0, "Main~1", "Firm", 5000, 10, held, back, 1, TaxLevel) == 0,
            "exit basis: a purchase of nothing, a negative one, and a purchase with no file note nothing");
        var put = new GainFile();
        ExitGains.ForMarketDeal(put, 0, ExitGains.CompanyPlace, "Co", 50000, 1, 1000, 0, 1, TaxLevel);
        Check(put.Basis.Count == 1 && put.Basis[0].Place == ExitGains.CompanyPlace && put.Basis[0].Bought == 5000100 && put.Lines.Count == 0,
            "exit basis: money put into a stake without shares (a buy-back) adds to its basis and notes no gain");
        var gone = bought();
        ExitGains.ForSale(gone, c, "Main~1", "Firm", price, 0, 50, TaxLevel);
        Check(gone.Basis.Count == 0, "exit basis: a stake sold whole leaves no basis, bought shares included");
        var dollars = new GainFile();
        long noted = ExitGains.ForMarketDeal(dollars, 0, "Main~1", "Firm", 1000, 0, 100, -10, 5, TaxLevel);
        var taking = new GainFile();
        long takeover = ExitGains.ForTakeover(taking, "Co", 1000, 0, 100, 400, 6, TaxLevel);
        Check(noted == 100000 && ExitGains.Between(dollars, 5, 5) == 1000 && takeover == 100000 && ExitGains.Between(taking, 6, 6) == 1000,
            "exit basis: the market's wallet counts whole dollars and the return cents, so $1,000 sold or paid with no basis is $1,000 on the return and not $10 (" + noted + ", " + takeover + ")");
    }

    private static void ExitBasisFirmDeals()
    {
        SxFirm f = MarketGainFirm(7711);
        Stock s = f.Stock;
        long step = Math.Max(1, MarketTrading.MostPerOrder(s) / 8), held = ExitGains.PlayerShares(f.Cap);
        SxDeal(f, step);
        SxDeal(f, -step);
        Check(f.Dealt.Count == 2 && f.Dealt[0][1] == step && f.Dealt[1][1] == -step && f.Dealt[0][3] > 0 && f.Dealt[1][3] > f.Dealt[1][4],
            "exit basis: the market tells the tech sector a purchase and a sale of a firm's shares (" + f.Dealt.Count + " deals)");
        if (f.Dealt.Count != 2)
        {
            return;
        }
        long[] b = f.Dealt[0], d = f.Dealt[1];
        long paidIn = held * b[2] / 4;
        var book = new GainFile();
        long g0 = ExitGains.ForMarketDeal(book, paidIn, SxPlace, "Nimbleloop", b[3], b[4], held, b[1], 300, TaxLevel);
        long cost = ExitGains.CentsOfMarket(b[3] + b[4]), net = ExitGains.CentsOfMarket(d[3] - d[4]);   // the market's money is whole dollars
        long want = Math.Max(0L, net - (long)((double)(paidIn + cost) * step / (held + step)));
        long without = Math.Max(0L, net - (long)((double)paidIn * step / (held + step)));
        long g1 = ExitGains.ForMarketDeal(book, paidIn, SxPlace, "Nimbleloop", d[3], d[4], held + step, d[1], 300, TaxLevel);
        Check(g0 == 0 && book.Basis.Count == 1 && book.Basis[0].Bought == cost && want > 0 && g1 == want && g1 < without,
            "exit basis: a firm's shares bought back on the market and sold again note the sale's gain less their cost (" + g1 + " of " + want + ", " + without + " when they cost nothing)");
    }

    // The open time of a company's quarter the deals are made in, as the listing's own checks find it.
    private static int ExitBasisOpen(MarketEngine m, Stock own)
    {
        int day = m.State.Day + 1;
        while (ListingRules.Closed(own, day, LYear) || !MarketCatalogue.TradingDay(day))
        {
            day++;
        }
        RunTo(m, day, MarketCatalogue.PhaseOpen + 5);
        return day;
    }

    private static void ExitBasisCompanyDeals()
    {
        var world = new OwnWorld(9301);
        MarketEngine m = LListed(9301, world, 4000, out Stock own);
        if (own == null)
        {
            Check(false, "exit basis: the player's company is listed for the deals' checks");
            return;
        }
        var deals = new List<long[]>();
        m.CompanyDealt = (nm, sh, ct, mn, fe, hd) => deals.Add(new[] { sh, ct, mn, fe, hd });
        OwnListing o = own.Own;
        int day = ExitBasisOpen(m, own);
        long shares = Math.Min(10000, MarketTrading.MostPerOrder(own) / 2), founder = o.Founder, cash = world.Purse.Money;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, own.Id, (int)shares);
        RunTo(m, day, m.State.Phase + 1);
        long paid = cash - world.Purse.Money;
        Check(deals.Count == 1 && deals[0][0] == shares && deals[0][4] == founder && Math.Abs(paid - (deals[0][2] + deals[0][3])) <= paid / 50 + 2,
            "exit basis: the founder's purchase of the whole company's shares is told with the shares held before it and what it cost the wallet (" + paid + ")");
        o.LockDay = 0;
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, own.Id, (int)-shares);
        RunTo(m, day, m.State.Phase + 1);
        Check(deals.Count == 2 && deals[1][0] == -shares && deals[1][4] == founder + shares && deals[1][2] > deals[1][3],
            "exit basis: the founder's sale of the whole company's shares is told with the money, the commission and the shares held before it");
        if (deals.Count != 2)
        {
            return;
        }
        var sale = new GainFile();
        long net = ExitGains.CentsOfMarket(deals[1][2] - deals[1][3]);
        long alone = ExitGains.ForMarketDeal(sale, 0L, ExitGains.CompanyPlace, own.Name, deals[1][2], deals[1][3], deals[1][4], deals[1][0], day, TaxLevel);
        Check(alone == net && net > 0 && ExitGains.Between(sale, day, day) == (int)(net / 100),
            "exit basis: a founder's sale of the whole company's shares on the market notes what the wallet gained as a gain (" + alone + " of " + net + ")");
        var book = new GainFile();
        ExitGains.ForMarketDeal(book, 0L, ExitGains.CompanyPlace, own.Name, deals[0][2], deals[0][3], deals[0][4], deals[0][0], day, TaxLevel);
        long cost = ExitGains.CentsOfMarket(deals[0][2] + deals[0][3]);
        long gain = ExitGains.ForMarketDeal(book, 0L, ExitGains.CompanyPlace, own.Name, deals[1][2], deals[1][3], deals[1][4], deals[1][0], day, TaxLevel);
        long slice = (long)((double)cost * shares / (founder + shares));
        Check(book.Basis.Count == 1 && book.Basis[0].Bought == cost && gain == net - slice && gain < alone,
            "exit basis: shares of the whole company bought back and sold again are taxed on the sale less their cost (" + gain + " of " + net + ")");
        long others = o.Others(own), back = Math.Min(others / 20, MarketTrading.MostPerOrder(own));
        OwnQuote qb = m.Quote(own, OwnAction.BuyBack, back);
        m.Event(MarketInputs.EventOwn + (int)OwnAction.BuyBack, own.Id, (int)back);
        RunTo(m, day, m.State.Phase + 1);
        Check(deals.Count == 3 && deals[2][0] == 0 && deals[2][2] > 0 && Math.Abs(deals[2][2] + deals[2][3] - qb.Total) <= qb.Total / 50 + 2,
            "exit basis: a buy-back the founder pays for is told as money put into the stake (" + (deals.Count > 2 ? deals[2][2] + deals[2][3] : -1) + " of " + qb.Total + ")");
        if (deals.Count == 3)
        {
            long before = book.Basis[0].Bought;
            ExitGains.ForMarketDeal(book, 0L, ExitGains.CompanyPlace, own.Name, deals[2][2], deals[2][3], deals[2][4], deals[2][0], day, TaxLevel);
            Check(book.Basis[0].Bought == before + ExitGains.CentsOfMarket(deals[2][2] + deals[2][3]), "exit basis: the buy-back's cost joins the stake's basis");
        }
        OwnQuote qp = m.Quote(own, OwnAction.Private, 0);
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Private, own.Id, 0);
        RunTo(m, day, m.State.Phase + 1);
        Check(!own.Listed && deals.Count == 4 && deals[3][0] == 0 && Math.Abs(deals[3][2] - qp.Total) <= qp.Total / 50 + 2,
            "exit basis: taking the company private, which buys every other holder's shares, is told as money put into the stake");
        var tw = new OwnWorld(9302);
        MarketEngine t = LListed(9302, tw, 4000, out Stock tk, true);
        if (tk != null)
        {
            var told = new List<long[]>();
            t.CompanyDealt = (nm, sh, ct, mn, fe, hd) => told.Add(new[] { sh });
            int tday = ExitBasisOpen(t, tk);
            long tf = tk.Own.Founder;
            t.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, tk.Id, (int)Math.Min(10000, MarketTrading.MostPerOrder(tk) / 2));
            RunTo(t, tday, t.State.Phase + 1);
            Check(tk.Own.Test && tk.Own.Founder > tf && told.Count == 0, "exit basis: a test's company deals in shares and notes nothing for the return, as it moves no money");
        }
    }

    private static void ExitBasisCompanyBids()
    {
        const long fixtures = 3000000;
        int won = 0, lapsed = 0, testWon = 0, testTold = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            foreach (int floatBp in new[] { 6000, 2500 })
            {
                var world = new OwnWorld(9400 + seed);
                MarketEngine m = LListed(9400 + seed, world, floatBp, out Stock own);
                if (own == null)
                {
                    continue;
                }
                var taken = new List<long[]>();
                m.CompanyTaken = (nm, fo, tt, ct, mn) => taken.Add(new[] { fo, tt, ct, mn });
                if (floatBp == 6000)
                {
                    own.Own.Founder = own.Shares / 4;
                }
                long cash = world.Purse.Money;
                m.BidFor(own.Id);
                int bid = own.BidCents, day = own.BidEndDay;
                RunTo(m, day);
                if (own.Listed)
                {
                    lapsed++;
                    Check(taken.Count == 0, "exit basis: a bid that lapses pays the founder nothing and notes nothing");
                    continue;
                }
                won++;
                long held = own.Own.Founder, money = MarketTrading.Value(held, bid, false);
                Check(taken.Count == 1 && taken[0][0] == held && taken[0][1] == own.Shares && taken[0][2] == bid && taken[0][3] == money && world.Purse.Money > cash,
                    "exit basis: a bid for the whole company that wins tells the founder's shares, the price and what they were paid, once");
                if (taken.Count != 1 || won > 3)
                {
                    continue;
                }
                var book = new GainFile();
                long part = (long)((double)fixtures * held / own.Shares);
                long gain = ExitGains.ForTakeover(book, own.Name, money, fixtures, held, own.Shares, day, TaxLevel);
                Check(gain == ExitGains.CentsOfMarket(money) - part && gain > 0 && book.Lines.Count == 1 && ExitGains.Between(book, day, day) == (int)(gain / 100),
                    "exit basis: the whole company taken over notes the founder's pay less their part of the businesses' stock and furniture (" + gain + " of " + money + ")");
                var bk = new GainFile();
                ExitGains.ForMarketDeal(bk, 0L, ExitGains.CompanyPlace, own.Name, 40000, 1000, held, 500, day - 5, TaxLevel);
                long dearer = ExitGains.ForTakeover(bk, own.Name, money, fixtures, held, own.Shares, day, TaxLevel);
                Check(dearer == gain - 4100000 && bk.Basis.Count == 0, "exit basis: shares bought back before a takeover come off its gain, and the basis goes with the company (" + dearer + ")");
                Check(ExitGains.ForTakeover(null, own.Name, money, fixtures, held, own.Shares, day, TaxLevel) == 0
                      && ExitGains.ForTakeover(new GainFile(), own.Name, 1000, fixtures, held, own.Shares, day, TaxLevel) == 0,
                    "exit basis: with no file, or a price under the fixtures' part, a takeover notes nothing");
            }
        }
        Check(won >= 3 && lapsed >= 10, "exit basis: bids for the whole company win only for founders under the threshold and not every time (" + won + " won, " + lapsed + " lapsed)");
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var world = new OwnWorld(9500 + seed);
            MarketEngine m = LListed(9500 + seed, world, 6000, out Stock own, true);
            if (own == null)
            {
                continue;
            }
            m.CompanyTaken = (nm, fo, tt, ct, mn) => testTold++;
            own.Own.Founder = own.Shares / 4;
            m.BidFor(own.Id);
            RunTo(m, own.BidEndDay);
            testWon += own.Listed ? 0 : 1;
        }
        Check(testWon >= 1 && testTold == 0, "exit basis: a test button's bid that wins pays no money and notes nothing (" + testWon + " won)");
    }

    private static void ExitBasisSave()
    {
        const long paidIn = 30000000;
        Cap c = TaxCap(paidIn);
        long held = ExitGains.PlayerShares(c), back = held / 4;
        var f = new GainFile();
        ExitGains.ForMarketDeal(f, paidIn, "Main~1", "Firm", 200000, 5000, held, back, 40, 1500);
        ExitGains.ForMarketDeal(f, 0L, ExitGains.CompanyPlace, "Co", 70000, 700, 5000, 400, 41, 1500);
        long part = ExitGains.ForMarketDeal(f, paidIn, "Main~1", "Firm", 900000, 10, held + back, -(held / 3), 42, 1500);
        string line = ExitGainStore.Write(f);
        GainFile again = ExitGainStore.Read(line, out _);
        BasisLine a = again.BasisOf("Main~1", false), b = again.BasisOf(ExitGains.CompanyPlace, false);
        Check(part > 0 && again.Basis.Count == 2 && a != null && b != null && a.Bought == f.BasisOf("Main~1", false).Bought && a.Sold == held / 3 && a.Used == f.BasisOf("Main~1", false).Used
              && b.Bought == f.BasisOf(ExitGains.CompanyPlace, false).Bought && again.Lines.Count == 1 && again.Lines[0].Cents == part,
            "exit basis: a save and reload keeps the cost of shares bought back, for a firm and for the whole company, and the gain and the basis used");
        long left = held + back - held / 3;
        long onLine = ExitGains.ForMarketDeal(again, paidIn, "Main~1", "Firm", 900000, 10, left, -left, 43, 1500);
        long direct = ExitGains.ForMarketDeal(f, paidIn, "Main~1", "Firm", 900000, 10, left, -left, 43, 1500);
        Check(onLine > 0 && onLine == direct, "exit basis: the rest sold after a reload has the gain it would have had with no save in between (" + onLine + " and " + direct + ")");
        GainFile old = ExitGainStore.Read("v=1~g:80|123456|Firm~b:Main%7E1|2000|7500000", out _);
        BasisLine ol = old.BasisOf("Main~1", false);
        Check(ol != null && ol.Bought == 0 && ol.Sold == 2000 && ol.Used == 7500000 && old.Lines.Count == 1 && old.Lines[0].Cents == 123456,
            "exit basis: a save from before reads each stake's cost of shares bought back as nothing and keeps what it had");
        Check(ExitGains.Slice(old, paidIn, "Main~1", 6000, 3000) == 11250000 && ExitGains.Slice(old, paidIn, "Main~1", 6000, 6000) == 22500000,
            "exit basis: an old save's next sale uses the same slice of the paid in cost as before, with no jump (" + ExitGains.Slice(old, paidIn, "Main~1", 6000, 3000) + ")");
        Check(ExitGainStore.Write(ExitGainStore.Read(ExitGainStore.Write(again), out _)) == ExitGainStore.Write(again), "exit basis: reading and writing the line again changes nothing");
    }

    private static void ExitBasisWiring()
    {
        string engine = ModSource(@"Markets\MarketEngine.Own.cs"), director = ModSource(@"Tech\TechDirector.ExitStock.cs"), tech = ModSource(@"Tech\TechDirector.ExitGains.cs");
        string links = ModSource(@"Wiring\Wiring.ExitStockLinks.cs"), bridges = ModSource(@"Markets\MarketBridges.cs"), market = ModSource(@"Markets\MarketDirector.Firms.cs");
        Check(Regex.IsMatch(engine, @"Receive\(s, paid, ListingMoney\.Takeover\);\s*if \(Mine\(s\) && !o\.Test\)\s*\{\s*CompanyTaken\?\.Invoke\(s\.Name, o\.Founder, s\.Shares, s\.BidCents, paid\);")
              && engine.IndexOf("CompanyTaken?.Invoke", StringComparison.Ordinal) < engine.IndexOf("Leave(s, day, GoneReason.TakenOver, s.BidCents);", StringComparison.Ordinal),
            "exit basis: a bid that wins, a rival tycoon's bid and the test button's (one path) tell the founder's gain before the company leaves, on the founder's machine and never for a test's company");
        Check(Regex.IsMatch(engine, @"if \(o\.Firm\)\s*\{\s*FirmDealt\?\.Invoke\([^;]*;[^}]*\}\s*else\s*\{\s*CompanyDealt\?\.Invoke\(s\.Name, shares, q\.Cents, q\.Value - q\.Reserve, fee, heldBefore\);")
              && Regex.IsMatch(engine, @"Pay\(s, q\.Total, ListingMoney\.BuyBack\);\s*if \(Mine\(s\) && !s\.Own\.Test\)\s*\{\s*CompanyDealt\?\.Invoke")
              && Regex.IsMatch(engine, @"Pay\(s, q\.Total, ListingMoney\.Private\);\s*if \(Mine\(s\) && !s\.Own\.Test\)\s*\{\s*CompanyDealt\?\.Invoke"),
            "exit basis: a deal, a buy-back and going private tell the tax of the founder's gain or cost, a firm's through its own bridge, on the founder's machine and never for a test's company");
        Check(Regex.IsMatch(director, @"long gain = ExitGains\.ForMarketDeal\(_gains,") && !director.Contains("ForMarketSale") && !Regex.IsMatch(director, @"if \(shares < 0\)\s*\{\s*// Before Align"),
            "exit basis: a firm's purchases reach the tax as well as its sales");
        Check(tech.Contains("ExitGains.ForMarketDeal(file, 0L, ExitGains.CompanyPlace, name, money, fee, held, shares, Save.Day, Prices.Level)")
              && tech.Contains("ExitGains.ForTakeover(file, name, money, fixtures, founder, total, Save.Day, Prices.Level)") && tech.Contains("_gains ?? ExitGainStore.Load(Save.modData)"),
            "exit basis: the whole company's gains go in the same file as a firm's, read from the save when the venture part is off, and write back at once");
        Check(links.Contains("MarketBridges.CompanyDealt = CompanyDealt;") && links.Contains("MarketBridges.CompanyTaken = CompanyTaken;") && links.Contains("MarketBridges.CompanyDealt = null;")
              && links.Contains("MarketBridges.CompanyTaken = null;") && links.Contains("MarketBridges.Businesses?.Invoke(places)") && links.Contains("ServicesGame.Fixtures(ServicesGame.Find(places[i]))")
              && bridges.Contains("CompanyDealt = null;") && bridges.Contains("CompanyTaken = null;") && market.Contains("_engine.CompanyDealt = CompanyDealtHeard;") && market.Contains("_engine.CompanyTaken = CompanyTakenHeard;"),
            "exit basis: the market's two tellings are wired and put back, and a takeover reads the businesses' stock and furniture while they are still the player's");
        Check(ModSource(@"Markets\MarketEngine.Own.cs").Contains("public bool BidBy(") || ModSource(@"Markets\MarketEngine.Holdings.cs").Contains("bool made = BidFor(stockId);"),
            "exit basis: a rival tycoon's bid goes through the same bid as the test button's");
        string all = tech + ModSource(@"Tech\Venture\ExitGains.cs") + links;
        Check(!Regex.IsMatch(all, @"void (Update|LateUpdate|FixedUpdate)\(") && !Regex.IsMatch(tech + ModSource(@"Tech\Venture\ExitGains.cs"), @"GoingPublic|Network|Peer|Rival|Npc"),
            "cost, going public, NPC: it runs on a deal or a won bid on the founder's machine, nothing is sent, and a rival's holding or a city startup's founder is never taxed here");
        Check(ModSource(@"Markets\MarketBots.cs").Contains("public static bool Mine(") && engine.Contains("return MarketBots.Mine(State, s);"),
            "exit basis: the founder is the owner of the machine's save alone, so another player's company in a session tells this machine nothing");
    }
}
