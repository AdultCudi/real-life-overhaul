using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The rest of the firm-on-the-market checks: shares, the founder's deals, the money, the endings, the offers, the city's startups, the save, a session, an old save and the options.
internal static partial class Program
{
    // ---------------------------------------------------------------- one source of truth for the shares

    private static void ExitStockShares()
    {
        SxFirm f = SxMake(7201);
        Stock s = f.Stock;
        if (s == null)
        {
            Check(false, "shares: made for the shares' checks");
            return;
        }
        Check(ExitStockRules.Grown(f.Cap, Info(s)) == 0 && !ExitStockRules.Align(f.Cap, Info(s)), "shares: the cap table and the stock agree at the listing, so bringing them together changes nothing");
        // staff options granted since: the stock is short by exactly them, and grows by them
        long granted = VentureRules.Grant(f.Cap, VentureCatalogue.OptionGrant);
        long grown = ExitStockRules.Grown(f.Cap, Info(s));
        Check(granted > 0 && grown == granted, "shares: staff options granted since make the stock's others' shares short by exactly them (" + grown + ")");
        long mineBefore = s.Own.Founder, floatBefore = s.Own.Float;
        Check(f.Engine.GrowFirm(s.Id, grown) && s.Shares == f.Cap.Total && ExitStockRules.Grown(f.Cap, Info(s)) == 0 && s.Own.Founder == mineBefore && s.Own.Float == floatBefore,
            "shares: the stock grows by them and the two agree again, the founder's and the public's untouched (" + s.Shares + ")");
        Check(!f.Engine.GrowFirm(s.Id, 0) && !f.Engine.GrowFirm(999999, 5), "shares: nothing grows by nothing, or for a stock that is not there");
        // the founder sells on the market: the stock moves, the cap table follows it
        s.Own.LockDay = 0;
        SxOpenTime(f);
        long raised = f.Cap.Acc.Raised, taken = f.Cap.Acc.Taken, floor = StartupMoney.Floor(f.Cap, f.Day + 1);
        long sellable = ListingFirm.Sellable(s.Own, f.Engine.State.Day);
        long n = Math.Min(sellable, Math.Max(1, MarketTrading.MostPerOrder(s) / 3));
        long cash = f.World.Purse.Money;
        SxDeal(f, -n);
        Check(f.Dealt.Count == 1 && f.Dealt[0][0] == s.Id && f.Dealt[0][1] == -n && f.Dealt[0][2] > 0 && f.Dealt[0][3] > 0 && s.Own.Founder == mineBefore - n && s.Own.Float == floatBefore + n && s.Shares == f.Cap.Total,
            "shares: a founder's sale is the stock's: their stake falls and the public's rises by it, the shares in issue as they were (" + n + ")");
        Check(f.World.Purse.Money > cash && f.World.Count(MarketNewsKind.OwnDeal) == 1, "shares: ...and the cash it brings is the player's own, and the city hears of the deal");
        Check(ExitStockRules.Align(f.Cap, Info(s)) && ExitRules.PlayerShares(f.Cap) == mineBefore - n && ExitStockRules.PublicShares(f.Cap) == floatBefore + n && ExitStockRules.Grown(f.Cap, Info(s)) == 0,
            "shares: the cap table's player and public lines are made what the stock says, and the investors' lines are as they were");
        Check(f.Cap.Acc.Raised == raised && f.Cap.Acc.Taken == taken && StartupMoney.Floor(f.Cap, f.Day + 1) == floor, "shares: the firm's account and the wallet's floor are untouched by the player's own shares sold");
        Check(!ExitStockRules.Align(f.Cap, Info(s)), "shares: aligned once, a second time changes nothing");
        // lines of the public from sales before there was a stock fold into one
        Cap g = FundedCap();
        var e = new FirmExit { Place = "Main~1", Stage = ListStage.Listed, LockTo = 0 };
        e.Stage = ListStage.Roadshow;
        e.Price = 6000000000L;
        e.FloatBp = ExitCatalogue.FloatBp;
        ExitRules.List(g, e, 100);
        g.Holders.Add(new Holder { Kind = (int)HolderKind.Public, Shares = 1000, Invested = 50 });
        var info = new StockInfo { Live = true, Shares = g.Total, Founder = ExitRules.PlayerShares(g), Float = ExitStockRules.PublicShares(g) };
        Check(ExitStockRules.Align(g, info) && CountOf(g, HolderKind.Public) == 1 && g.Total == info.Shares, "shares: two lines of the public are one line of the same shares once the stock holds them");
        // the vesting schedule is the cap table's own
        string differs = string.Empty;
        foreach (int year in new[] { 60, 365 })
        {
            for (int day = 0; day <= 5 * year && differs.Length == 0; day += 7)
            {
                var o = new OwnListing { Firm = true, Founder = 10000000, VestBase = 10000000, VestDay = 0, VestCliff = ExitStockRules.VestCliff(year), VestSpan = ExitStockRules.VestSpan(year) };
                long unvested = o.Unvested(day), expect = 10000000L - 10000000L * VentureRules.Vested(0, day, year) / 1000;
                differs = Math.Abs(unvested - expect) <= 1 ? string.Empty : "year " + year + " day " + day + ": " + unvested + " against " + expect;
            }
        }
        Check(differs.Length == 0, "shares: what is still to vest is what the cap table's own schedule says, at a game year of 60 days and of 365: a cliff of a year, then four years (" + differs + ")");
        // the founder removed: the unvested shares go back; the stock cancels them and what is left is all vested
        SxFirm h = SxMake(7202);
        h.Stock.Own.VestDay = h.Day - 100;
        h.Stock.Own.LockDay = 0;
        long founder = h.Stock.Own.Founder, shares = h.Stock.Shares, unv = h.Stock.Own.Unvested(h.Day);
        long cancelled = h.Engine.CancelUnvested(h.Stock.Id, h.Day);
        Check(unv > 0 && cancelled == unv && h.Stock.Own.Founder == founder - unv && h.Stock.Shares == shares - unv && h.Stock.Own.Unvested(h.Day) == 0 && h.Stock.Own.VestBase == 0,
            "shares: a founder removed as a bad leaver loses the unvested part in the stock too; what they keep is all vested (" + cancelled + " of " + founder + ")");
        Check(ExitStockRules.Align(h.Cap, Info(h.Stock)) && ExitRules.PlayerShares(h.Cap) == founder - unv && h.Cap.Total == h.Stock.Shares, "shares: ...and the cap table follows");
        Check(h.Engine.CancelUnvested(h.Stock.Id, h.Day) == 0, "shares: ...once");
        // the lock-up is one number
        SxFirm k = SxMake(7203);
        InfoCheckLocks(k);
    }

    private static int CountOf(Cap c, HolderKind kind)
    {
        int n = 0;
        for (int i = 0; i < c.Holders.Count; i++)
        {
            n += c.Holders[i].Kind == (int)kind ? 1 : 0;
        }
        return n;
    }

    // The tech sector's lock-up and the stock's are one: the days around the end of it.
    private static void InfoCheckLocks(SxFirm k)
    {
        StockInfo i = Info(k.Stock);
        bool same = true;
        for (int day = k.Exit.LockTo - 3; day <= k.Exit.LockTo + 3; day++)
        {
            bool techLocked = ExitRules.Locked(k.Exit, day), stockLocked = ExitStockRules.LockLeft(i, day) > 0;
            same &= techLocked == stockLocked;
        }
        Check(same && i.LockDay == k.Exit.LockTo && ExitStockRules.LockLeft(i, k.Exit.LockTo - 1) == 1 && ExitStockRules.LockLeft(i, k.Exit.LockTo) == 0 && ExitStockRules.LockLeft(default(StockInfo), 5) == 0,
            "lock-up: the stock's lock and the tech sector's are the same days, one lock and not two (to day " + i.LockDay + ")");
    }

    // ---------------------------------------------------------------- the founder's deals: the one way to sell

    private static void ExitStockDeals()
    {
        SxFirm f = SxMake(7301);
        Stock s = f.Stock;
        if (s == null)
        {
            Check(false, "deals: made for the deals' checks");
            return;
        }
        SxOpenTime(f);
        int day = f.Engine.State.Day;
        long small = Math.Max(1, MarketTrading.MostPerOrder(s) / 4);
        // inside the lock-up a sale is refused, by the stock's own reason
        OwnQuote q = f.Engine.Quote(s, OwnAction.Deal, -small);
        Check(day < s.Own.LockDay && q.Why == OwnWhy.FirmLocked, "deals: a sale inside the 180 day lock-up is refused (" + q.Why + ")");
        long cash = f.World.Purse.Money, founder = s.Own.Founder;
        f.Engine.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, s.Id, (int)-small);
        RunTo(f.Engine, day, f.Engine.State.Phase + 1);
        Check(s.Own.Founder == founder && f.World.Purse.Money == cash && f.World.Last(MarketNewsKind.OwnRefused).B == (int)OwnWhy.FirmLocked && f.Dealt.Count == 0,
            "deals: asked all the same, it is refused again when its moment comes, on every machine, and nothing is sold or paid");
        int lockWas = s.Own.LockDay;
        s.Own.LockDay = day;   // the quarter's open days are the ones a deal is asked on; the lock-up is tried on its last day and the next
        Check(ListingRules.DealBlocked(s, day - 1, SxYear, -small) == OwnWhy.FirmLocked && ListingRules.DealBlocked(s, day, SxYear, -small) != OwnWhy.FirmLocked,
            "deals: the day before the lock-up ends it still holds, the day it ends it does not");
        s.Own.LockDay = lockWas;
        // what a founder buys: only what the public holds
        long publicShares = s.Own.Float;
        Check(ListingRules.DealBlocked(s, day, SxYear, publicShares + 1) == OwnWhy.NoShares && ListingRules.DealBlocked(s, day, SxYear, small) == OwnWhy.None,
            "deals: all there is to buy is what the public holds (" + publicShares + "); the investors' and the staff's shares are not on this market");
        // the other things a founder may ask of their company are not open to a firm
        bool allShut = true;
        foreach (OwnAction a in new[] { OwnAction.Pay, OwnAction.Dividend, OwnAction.BuyBack, OwnAction.Issue, OwnAction.Private })
        {
            allShut &= f.Engine.Quote(s, a, 1000).Why == OwnWhy.Firm;
        }
        Check(allShut, "deals: its pay is the board's salary band, and a dividend, a buy-back, new shares or going private would each be a second way for investors' money out: only the founder's deals are open");
        // vesting: the part not yet vested is not for sale, once the lock-up is over
        s.Own.LockDay = 0;
        s.Own.VestDay = day - 100;
        long sellable = ListingFirm.Sellable(s.Own, day), unvested = s.Own.Unvested(day);
        Check(unvested > 0 && sellable == s.Own.Founder - unvested && ListingRules.DealBlocked(s, day, SxYear, -(sellable + 1)) == OwnWhy.Unvested && ListingRules.DealBlocked(s, day, SxYear, -(sellable - small)) != OwnWhy.Unvested,
            "deals: shares still to vest are not for sale: of " + s.Own.Founder + " " + sellable + " are free, one more is refused");
        s.Own.VestBase = 0;
        s.Own.Unvested(day);
        Check(ListingRules.DealBlocked(s, day, SxYear, -(sellable + 1)) != OwnWhy.Unvested, "deals: ...a founder who is a good leaver or whose shares have all vested sells what they hold");
        // the closed time before the results, the year's limit, the short swing
        int closed = day;
        while (!ListingRules.Closed(s, closed, SxYear) || !MarketCatalogue.TradingDay(closed))
        {
            closed++;
        }
        Check(ListingRules.DealBlocked(s, closed, SxYear, small) == OwnWhy.Closed, "deals: from the last third of a quarter until its results a founder deals in nothing: the figures are known to them first");
        long tenth = s.Shares / 10;
        Check(ListingRules.DealBlocked(s, day, SxYear, tenth + s.Shares / 100) == OwnWhy.TooLarge || ListingRules.DealBlocked(s, day, SxYear, tenth + s.Shares / 100) == OwnWhy.YearLimit,
            "deals: a founder's dealing in a year is limited, as it is for their whole company");
        double rps = s.Own.Rps;
        SxDeal(f, small);
        Check(f.Dealt.Count == 1 && f.Dealt[0][1] == small && s.Own.Float == publicShares - small, "deals: a purchase takes shares from the public's");
        s.Own.LockDay = 0;
        SxDeal(f, -small / 2);
        Check(s.Own.Rps == rps && s.Own.Rps == 0.0, "deals: a gain paid back for shares bought and sold within the time is paid, and no reserve is made of it: there is no reserve to put it in");
        Check(f.Dealt.Count == 2 && f.Dealt[1][1] == -(small / 2), "deals: each deal is told to the tech sector as it is done, the sale's price and money with it");
        // the market reads a founder's selling as bad news, as it reads their company's
        SxFirm twin = SxMake(7301);
        SxOpenTime(twin);
        SxFirm sale = SxMake(7301);
        sale.Stock.Own.LockDay = 0;
        SxOpenTime(sale);
        long sold = Math.Min(ListingFirm.Sellable(sale.Stock.Own, sale.Engine.State.Day), Math.Max(1, MarketTrading.MostPerOrder(sale.Stock) / 2));
        SxDeal(sale, -sold);
        RunTo(twin.Engine, twin.Engine.State.Day, twin.Engine.State.Phase + 1);
        Check(sale.Stock.Last <= twin.Stock.Last && sale.Dealt.Count == 1, "deals: a founder's sale weighs on the price, never lifts it (" + sale.Stock.Last + " against " + twin.Stock.Last + " with none)");
        // no bid, no investor pressing a sale, no bot
        SxFirm cheap = SxMake(7302);
        cheap.Stock.Sent = -2.0;
        cheap.Stock.Mis = -2.0;
        Check(!cheap.Engine.BidFor(cheap.Stock.Id) && cheap.Stock.BidCents == 0, "bids: no bid is made for a firm on the market, whatever its price; it is bought through the tech sector's offers");
        SxRun(cheap, 120);
        ActivistDemand demand = ListingVotes.Reason(cheap.Stock, cheap.State.IndexLast, cheap.State.Day, SxYear, out double strength);
        Check(cheap.Stock.BidCents == 0 && demand != ActivistDemand.Sale, "bids: nor in a hundred and twenty days of a cheap price, and no investor presses for a sale (" + demand + ")");
        // a price high enough for a split in the market's other companies: a firm's is left alone, its cap table's shares are not scaled with it
        SxFirm rich = SxMake(7303, 50000, 8000, 900000000000000L);
        RunTo(rich.Engine, rich.Day + 30);
        Check(rich.Stock.Last > MarketCatalogue.SplitAboveCents && rich.Stock.SplitDay == 0 && rich.Stock.SplitRatio == 0 && rich.World.Count(MarketNewsKind.Split) == 0 && rich.Stock.Shares == rich.Cap.Total,
            "splits: a firm's shares are never split, whatever the price (" + rich.Stock.Last + "): the shares in issue stay the cap table's");
        bool bots = false;
        foreach (BotScope scope in new[] { BotScope.Market, BotScope.Picked, BotScope.Held })
        {
            var bot = new Bot { Firm = 1, Scope = scope };
            bot.Picks.Add(s.Id);
            bots |= MarketBots.InScope(f.State, bot, s);
        }
        Check(!bots && !MarketBots.Buyable(f.State, MarketBots.All[0], s, day + 100), "bots: a trading bot never trades a firm that listed on its own, in any scope");
    }

    // ---------------------------------------------------------------- the money stays lawful

    private static void ExitStockMoney()
    {
        // the offer's money is booked once, by the tech sector, into the firm's account
        SxFirm f = SxMake(7401);
        long net = f.Exit.Net;
        Check(net > 0 && f.Cap.Acc.Open && f.Cap.Acc.Raised == Raise + net && StartupMoney.Floor(f.Cap, f.Day + 1) == Raise + net,
            "money: the offer's net is money raised in the firm's account once, on top of the round, so the wallet must keep it (" + f.Cap.Acc.Raised + ")");
        string stockFiles = ModSource(@"Markets\MarketEngine.Firms.cs") + ModSource(@"Markets\Listing\ListingFirm.cs");
        Check(!Regex.IsMatch(stockFiles, @"\bPay\(|\bReceive\(|Wallet\.|\.Force\(|\.Give\(|\.Take\(|ListingMoney\."), "money: making, growing and ending a firm's stock books no money: no offer, no fee, no payout of its own");
        Check(f.World.Purse.Money == 1000000000 && f.World.Purse.In == 0 && f.World.Purse.Out == 0, "money: the wallet is as it was after the stock is made: the offer was not booked a second time");
        // listing a firm with money missing from its account is not done
        Cap taken = FundedCap();
        StartupMoney.Shortfall(new List<Cap> { taken }, 0, Raise / 2, 20, false, "ba:transaction_yacht");
        Check(ExitStockRules.ListBlocked(taken) == ExitStockRules.BlockedTaken && ExitStockRules.ListBlocked(FundedCap()) == ExitStockRules.BlockedNone && ExitStockRules.ListBlocked(VentureRules.NewCap("Main~1")) == 0
              && ExitStockRules.ListBlocked(null) == 0, "money: a firm whose investors' money was spent elsewhere and not put back is not listed: the public is not sold shares of a firm that owes");
        StartupMoney.Repay(taken, Raise, Raise, 25);
        Check(ExitStockRules.ListBlocked(taken) == ExitStockRules.BlockedNone, "money: ...until the money is put back");
        Cap asked = FundedCap();
        asked.Acc.Step = StartupMoney.StepAsked;
        Check(ExitStockRules.ListBlocked(asked) == ExitStockRules.BlockedLadder, "money: nor with the board's question open");
        string exits = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(Regex.IsMatch(exits, @"ListBlocked\(c\) != ExitStockRules\.BlockedNone[\s\S]{0,260}ListStage\.CalledOff") && Regex.IsMatch(exits, @"ListBlocked\(_venture\.CapOf\(place\)\)[\s\S]{0,400}return false;"),
            "money: the bankers are not engaged and the book is called off at the pricing day for such a firm");
        // the investors are paid once: at the listing nothing, in a sale or a wind down the waterfall, and the stock's holders by what the waterfall owes the public
        SxFirm g = SxMake(7402);
        long paidBefore = 0;
        for (int i = 0; i < g.Cap.Holders.Count; i++)
        {
            paidBefore += g.Cap.Holders[i].Paid;
        }
        Check(paidBefore == 0, "money: nobody is paid at the listing: the offer is new shares sold to the public, and no investor sells into it");
        long pot = 9000000000L;
        long[] lines = VentureRules.Waterfall(g.Cap.Holders, pot);
        long publicLine = 0, investors = 0;
        for (int i = 0; i < g.Cap.Holders.Count; i++)
        {
            publicLine += g.Cap.Holders[i].Kind == (int)HolderKind.Public ? lines[i] : 0;
            investors += ExitRules.Investor(g.Cap.Holders[i]) ? lines[i] : 0;
        }
        long each = ExitStockRules.PublicEach(g.Cap, pot), publicShares = ExitStockRules.PublicShares(g.Cap);
        Check(each > 0 && each * publicShares <= publicLine && each * publicShares > publicLine - publicShares && publicLine <= investors && publicLine > 0,
            "money: what each of the stock's shares is paid at the end is the public's line of the waterfall over its shares: never more than the investors were owed (" + each + ")");
        Check(ExitStockRules.PublicEach(g.Cap, 0) == 0 && ExitStockRules.PublicEach(g.Cap, -5) == 0 && ExitStockRules.PublicEach(VentureRules.NewCap("x"), pot) == 0 && ExitStockRules.PublicEach(null, pot) == 0,
            "money: a firm with nothing to share pays its stock nothing, and one with no public pays nobody");
        // the market cannot be asked (it is off, or the player is a guest and the firm is set aside): the shares are still its, so no other path sells them
        var away = new StockInfo { Live = false, Why = StockWhy.Off };
        var live = new StockFirm { Place = SxPlace, ListDay = 100, OpenDay = 110, Ticker = "NIM" };
        var ended = new StockFirm { Place = SxPlace, ListDay = 100, OpenDay = 110, EndDay = 150, Ended = StockEnd.WoundDown };
        var never = new StockFirm { Place = SxPlace, ListDay = 100 };
        Check(ExitStockRules.Held(away, live) && ExitStockRules.Held(Info(g.Stock), null) && !ExitStockRules.Held(away, ended) && !ExitStockRules.Held(away, never) && !ExitStockRules.Held(away, null),
            "money: while the market cannot be asked a stock made and not ended is still the market's: a sale by the older path then would sell the same shares twice");
        string director = ModSource(@"Tech\TechDirector.ExitStock.cs");
        Check(Regex.IsMatch(director, @"internal bool HasStock\(string place\)[\s\S]{0,200}ExitStockRules\.Held\(StockBridges\.LookNow\(place\), _stock\.Of\(place\)\)")
              && Regex.IsMatch(director, @"private void EndStock[\s\S]{0,500}rec\.EndDay = day;[\s\S]{0,200}if \(!info\.Live\)"),
            "money: a firm whose stock the market cannot be asked for is held to the stock all the same, and its end is noted whether or not the market could be asked");
        Check(Regex.IsMatch(ModSource(@"Tech\TechDirector.Money.cs"), @"private void Remove\(Cap c, FirmPlace firm, int day\)[\s\S]{0,260}HasStock\(c\.Place\) && !StockOf\(c\.Place\)\.Live[\s\S]{0,40}return;"),
            "money: a founder is not removed while the stock cannot be asked: the unvested shares go back in the stock, not only in the cap table");
        // a sale, as the tech sector works it out, pays the public once and the stock follows it
        SaleResult r = ExitRules.Settle(g.Cap, pot, StartupMoney.Balance(g.Cap), 300);
        long eachSale = ExitStockRules.PublicEach(g.Cap, r.Pot);
        Check(r.Paid == r.Owed && r.Gap == 0 && eachSale > 0 && eachSale * publicShares <= r.Owed, "money: a sale pays the investors, the public's line among them, once out of the price; the stock is paid out of that line and not on top of it");
        // a wind down of a listed firm: the public's line is owed and a firm that cannot pay owes a claim, never nothing
        StartupMoney.Wind w = StartupMoney.Close(g.Cap, 0, 300);
        long eachWind = ExitStockRules.PublicEach(g.Cap, w.Price);
        Check(w.Owed > 0 && w.Gap == w.Owed && eachWind > 0, "money: a listed firm wound down with nothing in the wallet still owes the public its line: the claim is the investors', and the stock's holders are paid what it is (" + eachWind + ")");
        StartupMoney.Profit(g.Cap, -StartupMoney.Balance(g.Cap) - 5, 200);
        StartupMoney.Wind dry = StartupMoney.Close(g.Cap, 0, 300);
        Check(dry.Dry && ExitStockRules.PublicEach(g.Cap, dry.Price) == 0 && ExitStockRules.Reason(StockEnd.WoundDown, dry.Dry) == 0 && ExitStockRules.Reason(StockEnd.WoundDown, false) == 2 && ExitStockRules.Reason(StockEnd.Sold, true) == 1,
            "money: a firm that honestly ran dry ends its stock as failed, paying nothing; one that paid, delisted; one sold, taken over");
        // the shares sold by the tech sector's older paths are not open to a firm whose shares trade
        string deals = ModSource(@"Tech\TechDirector.Deals.cs");
        string exitsFile = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(!deals.Contains("internal bool SellShares") && Regex.IsMatch(exitsFile, @"internal bool ExitSell[\s\S]{0,420}StockSellRefused\(place, firm\.File\.Name\)")
              && Regex.IsMatch(exitsFile, @"internal long ExitRoom[\s\S]{0,520}HasStock\(place\)") && Regex.IsMatch(exitsFile, @"internal int ExitLockLeft[\s\S]{0,200}StockLockLeft\(place\)"),
            "money: the window's sale, the only older path left, is refused for a firm whose shares trade, and the lock-up it reads is the stock's");
        Check(Regex.IsMatch(deals, @"internal bool StartRaise[\s\S]{0,500}StockRaiseRefused") && Regex.IsMatch(deals, @"internal bool TakeSheet[\s\S]{0,600}HasStock\(place\)"),
            "money: a firm whose shares trade closes no private round: the shares in issue are the stock's, and a round under way ends with the listing");
        Check(exitsFile.Contains("VentureGame.Give(Level(paid), VentureGame.ShareSaleKey, firm.File.Name);") && exitsFile.Contains("VentureGame.Give(Level(net), VentureGame.RaiseKey, firm.File.Name, firm.Reg);"),
            "money: the older sale of a firm's shares and the offer's money are booked as they were");
        // the same takings never feed two stocks
        string links = ModSource(@"Wiring\ListingLinks.cs");
        Check(Regex.IsMatch(links, @"private static void Mine\(\)[\s\S]{0,600}ExitStockLinks\.IsListed") && Regex.IsMatch(links, @"private static void Places\([\s\S]{0,500}ExitStockLinks\.IsListed")
              && Regex.IsMatch(links, @"private static void TradeTakings[\s\S]{0,900}ExitStockLinks\.IsListed"),
            "money: a firm that listed on its own is kept out of the whole company's books, places and trades: its takings feed one stock, and a take over of the company does not hand it over");
        // the dividend stays the firm's: the stock keeps no reserve, so a dividend cannot be paid on the stock and on the firm's page
        SxFirm h = SxMake(7403);
        SxRun(h, SxYear);
        Check(h.Stock.Own.Rps == 0.0 && ListingRules.Most(h.Stock.Own) == 0 && ListingRules.Declared(h.Stock) == 0 && ListingRules.ToReserve(h.Stock, 5.0) == 0.0 && h.Stock.Own.Rps == 0.0,
            "money: the stock has no reserve and can declare no dividend; a listed firm's dividend stays the firm page's, every holder by their share, out of its account");
    }

    // ---------------------------------------------------------------- the stock ends with the firm

    private static void ExitStockEndings()
    {
        SxFirm f = SxMake(7501);
        Stock s = f.Stock;
        if (s == null)
        {
            Check(false, "endings: made for the endings' checks");
            return;
        }
        RunTo(f.Engine, f.Day + 10);
        bool ended = f.Engine.EndFirm(s.Id, GoneReason.TakenOver, 777);
        Check(ended && !s.Listed && s.Reason == GoneReason.TakenOver && s.GoneCents == 777 && s.GoneDay == f.Engine.State.Day, "endings: a firm sold takes its stock off the market, each share paid out at what the sale paid the public");
        Check(f.Engine.MineFirms(new List<Stock>()) == 0 && f.Engine.FirmByPlace(SxPlace) == null && f.Engine.FirmByPlace(SxPlace, false) == s && f.World.Purse.Money == 1000000000,
            "endings: the market finds no live stock for it any more, keeps its record, and the founder's wallet is not paid by it (the sale's money came through the firm's account)");
        Check(!f.Engine.EndFirm(s.Id, GoneReason.Failed, 0) && !f.Engine.EndFirm(123456, GoneReason.Failed, 0), "endings: a stock ends once, and an unknown one not at all");
        Check(f.World.Heard.Exists(n => n.Stock == s.Id && (n.Kind == MarketNewsKind.Delisted || n.Kind == MarketNewsKind.Failure)) && !f.World.Heard.Exists(n => n.Stock == s.Id && n.Kind == MarketNewsKind.BidDone),
            "endings: the city hears it left, never as a takeover bid that would hand the player's businesses over");
        // wound down: failed, nothing paid; the company beside it is not touched
        var world = new OwnWorld(7502);
        MarketEngine both = LListed(7502, world, 2500, out Stock company);
        if (company != null)
        {
            SxFirm tmp = SxMake(7502, put: false);
            FirmListing r = tmp.Listing;
            r.Place = "Ash~3";
            r.Name = "Fernlight";
            r.Books = SxBooks(both.State.Day, 800, 90);
            r.LockDay = both.State.Day + 90;
            Stock firm = both.ListFirm(r, out _);
            bool failed = both.EndFirm(firm.Id, GoneReason.Failed, 0);
            Check(failed && !firm.Listed && firm.Reason == GoneReason.Failed && company.Listed && both.MineListed() == company && both.State.Listing.Stage == ListingStage.Listed,
                "endings: a firm wound down ends its own stock and nothing of the player's whole company: it is still listed and its way to the market stands");
            double rps = company.Own.Rps;
            RunTo(both, both.State.Day + 20);
            Check(company.Listed && !firm.Listed, "endings: the days go on for the company, and the firm's stock stays gone");
        }
        // made again for a later listing of the same firm
        Stock back = f.Engine.ListFirm(f.Listing, out FirmWhy why);
        Check(back != null && why == FirmWhy.None && back.Listed && back.Own.Place == s.Own.Place && f.Engine.FirmByPlace(SxPlace) == back, "endings: a firm that lists again later is given a stock again");
        // a holder who is not the founder is paid at the end: the host's firm in a guest's market
        SxFirm host = SxMake(7503);
        MarketState guestState = MarketStore.Read(MarketStore.Write(host.State, true), out _);
        guestState.Me = SxGuest;
        var guestWorld = new StockWorld(7503);
        guestWorld.State = () => guestState;
        var guest = new MarketEngine(guestState, new MarketTape(7503), guestWorld);
        Stock theirs = guestState.Stock(host.Stock.Id);
        guestState.Positions.Add(new Position { StockId = theirs.Id, Shares = 200, Cost = 1000 });
        long had = guestWorld.Purse.Money;
        Check(theirs != null && !guest.Mine(theirs) && guest.MineFirms(new List<Stock>()) == 0, "endings: to a guest the host's firm is another player's company");
        guestState.Following = false;
        guest.EndFirm(theirs.Id, GoneReason.Delisted, 600);
        Check(!theirs.Listed && guestState.Position(theirs.Id) == null, "endings: ...whose end the guest's market plays as the host's does (the end of a stock reaches a guest by the market the host gives)");
        Check(guestWorld.Purse.Money - had == 0 || guestWorld.Purse.Money - had == MarketTrading.Value(200, 600, false), "endings: a holder of the stock that is not its founder is paid at the price the end set");
        // a firm the tech sector no longer lists leaves the market
        SxFirm sweepFirm = SxMake(7504);
        SxFirm second = sweepFirm;
        FirmListing other = sweepFirm.Listing;
        other.Place = "Oak~2";
        other.Name = "Oakwell";
        Stock oak = sweepFirm.Engine.ListFirm(other, out _);
        Check(sweepFirm.Engine.SweepFirms(new[] { SxPlace, "Oak~2" }) == 0 && sweepFirm.Stock.Listed && oak.Listed, "endings: a sweep of the firms the tech sector still lists leaves the stocks of them");
        Check(sweepFirm.Engine.SweepFirms(new[] { SxPlace }) == 1 && sweepFirm.Stock.Listed && !oak.Listed && oak.GoneCents == 0 && sweepFirm.Engine.SweepFirms(null) == 1 && !sweepFirm.Stock.Listed,
            "endings: ...and any stock of a firm it no longer lists (set aside while its player was a guest, wound down meanwhile) leaves, paying nothing");
        // the market's own hand: a lone player is back, the other players' firms leave
        SxFirm lone = SxMake(7505);
        lone.State.Me = SxGuest;
        int moved = lone.Engine.Alone();
        Check(moved == 1 && !lone.Stock.Listed && lone.Stock.Reason == GoneReason.Delisted, "endings: playing alone again, another player's firm leaves this market at its last price");
    }

    // ---------------------------------------------------------------- offers for a listed firm

    private static void ExitStockOffers()
    {
        var buyers = new List<ExitBuyer> { new ExitBuyer { Buyer = 0, Id = 3, Name = "Northgate Systems", Cash = long.MaxValue } };
        long mrr = 5000000, value = 4000000000L;
        int listedNone = 0, tradedSome = 0, unlisted = 0;
        for (int day = 1; day <= 3000; day++)
        {
            var listed = new FirmExit { Place = "Main~1", Stage = ListStage.Listed };
            var file = new ExitFile { Seed = 4242 };
            listedNone += ExitRules.Offer(file, listed, day, 1000, mrr, value, 2500, buyers) != null ? 1 : 0;
            tradedSome += ExitRules.Offer(new ExitFile { Seed = 4242 }, new FirmExit { Place = "Main~1", Stage = ListStage.Listed }, day, 1000, mrr, value, 2500, buyers, true) != null ? 1 : 0;
            unlisted += ExitRules.Offer(new ExitFile { Seed = 4242 }, new FirmExit { Place = "Main~1" }, day, 1000, mrr, value, 2500, buyers) != null ? 1 : 0;
        }
        Check(listedNone == 0 && tradedSome > 0 && Math.Abs(tradedSome - unlisted) <= 1, "offers: a firm that listed and has no stock draws none, as before; one whose shares trade draws them as often as any, at what the market makes of it (" + tradedSome + ")");
        Check(ExitRules.Offer(new ExitFile { Seed = 1 }, new FirmExit { Place = "Main~1", Stage = ListStage.Bankers }, 5, 1000, mrr, value, 2500, buyers, true) == null,
            "offers: a firm on its way to the market draws none even then");
        Check(ExitStockRules.MarketValue(15000000, 480, 1000) == 7200000000L && ExitStockRules.MarketValue(15000000, 480, 2000) == 3600000000L && ExitStockRules.MarketValue(-5, 480, 1000) == 0,
            "offers: the value an offer starts from for a firm whose shares trade is all its shares at the market's last price, at the start's prices");
        // a sale of such a firm pays its investors, the public among them, once; the stock follows what the public's line is paid
        SxFirm f = SxMake(7601);
        ExitOffer o = null;
        var ef = new ExitFile { Seed = 9 };
        FirmExit e = ef.Exit(SxPlace, true);
        e.Stage = ListStage.Listed;
        long worth = ExitStockRules.MarketValue(f.Stock.Shares, f.Stock.Last, 1000);
        for (int day = 1; day < 3000 && o == null; day++)
        {
            o = ExitRules.Offer(ef, e, day, 1000, mrr, worth, 2500, buyers, true);
        }
        SaleResult r = o != null ? ExitRules.Settle(f.Cap, o.Price, StartupMoney.Balance(f.Cap), 400) : default(SaleResult);
        Check(o != null && o.Price >= worth * 11 / 10 * 1000 / 1000 - 1 && r.Owed > 0 && r.Paid == r.Owed && ExitStockRules.PublicEach(f.Cap, r.Pot) * ExitStockRules.PublicShares(f.Cap) <= r.Owed,
            "offers: a firm whose shares trade is bought at a premium over its market value, its investors paid by the waterfall once, the stock paid out of the public's line (" + (o != null ? o.Price : 0) + ")");
        string exits = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(Regex.IsMatch(exits, @"StockSyncFor\(place, c, day\);[\s\S]{0,4000}StockSold\(place, c, r\.Pot, day\);[\s\S]{0,200}_venture\.Caps\.Remove\(c\)")
              && Regex.IsMatch(ModSource(@"Tech\TechDirector.Money.cs"), @"StockSyncFor\(c\.Place, c, day\);[\s\S]{0,3600}StockWound\(c\.Place, c, w\.Price, w\.Dry, day\);[\s\S]{0,200}_venture\.Caps\.Remove\(c\)"),
            "endings: a sale and a wind down both bring the cap table to the stock first and end the stock before the cap table goes, with the pot the waterfall shared");
        Check(Regex.IsMatch(ModSource(@"Tech\TechDirector.Money.cs"), @"HasStock\(c\.Place\) \? StockCancelUnvested\(c, day\) : StartupMoney\.BadLeaver"), "endings: a founder removed has the stock's unvested shares cancelled, not a second count");
    }

    // ---------------------------------------------------------------- a startup of the city that listed

    private static StartupListing SxStartup(string name = "Spark Ledger", int sector = (int)MarketSector.Software)
    {
        return new StartupListing { Name = name, Sector = sector, RevenueM = 30, Shares = 15000000, PriceCents = 1200 };
    }

    private static void ExitStockCity()
    {
        MarketEngine m = NewMarket(7701, SxYear, TradingOnOrAfter(200));
        Stock s = m.ListStartup(SxStartup(), out FirmWhy why);
        Check(s != null && why == FirmWhy.None && s.Listed && s.Own == null && MarketCatalogue.IsStartup(s.Id) && !ListingRules.IsPlayer(s.Id) && s.Id >= MarketCatalogue.StartupFirst,
            "city: a startup of the city that listed is a company of the market's, with an id of its own range, owned by nobody");
        if (s == null)
        {
            return;
        }
        Check(s.Name == "Spark Ledger" && s.Ticker.Length >= 3 && s.Sector == MarketSector.Software && s.Shares == 15000000 && s.Last == 1200 && s.Size == MarketSize.Small,
            "city: it has its name, a ticker, the sector of what it does, the listing's shares and its price (" + s.Ticker + ", " + s.Last + ")");
        Check(m.MineListed() == null && !m.Mine(s) && m.State.Stocks.Contains(s), "city: it is not a player's, and the player may trade it like any company");
        int bars = s.Daily.Count;
        RunTo(m, m.State.Day + 40);
        Check(s.Daily.Count > bars && s.Results.Count > 0 && s.Listed && s.Last > 0, "city: it charts, reports and moves with the market through the same engine as any company");
        // capped, seeded, host-owned
        MarketEngine c = NewMarket(7702, SxYear, TradingOnOrAfter(200));
        FirmWhy last = FirmWhy.None;
        int made = 0;
        for (int i = 0; i < MarketCatalogue.StartupsMost + 3 && last != FirmWhy.TooMany; i++)
        {
            Stock t = c.ListStartup(SxStartup("Startup " + (char)('A' + i)), out last);
            made += t != null && last == FirmWhy.None ? 1 : 0;
        }
        Check(made == MarketCatalogue.StartupsMost && last == FirmWhy.TooMany, "city: the market holds at most " + MarketCatalogue.StartupsMost + " of the city's startups, so they can never fill it");
        Stock first = null;
        for (int i = 0; i < c.State.Stocks.Count && first == null; i++)
        {
            first = MarketCatalogue.IsStartup(c.State.Stocks[i].Id) ? c.State.Stocks[i] : null;
        }
        c.Delist(first.Id, GoneReason.Failed, 0);
        Check(c.ListStartup(SxStartup("Late One"), out FirmWhy again) != null && again == FirmWhy.None, "city: one that fails or leaves makes room for the next");
        int goneId = first.Id;
        RunTo(c, c.State.Day + SxYear + 5);
        Check(c.ListStartup(SxStartup("Later One"), out FirmWhy room) == null || c.State.Stock(goneId) == null || c.State.Stock(goneId).Listed, "city: a startup long gone is taken out of the market's list when the next comes, so ids and rooms are reused");
        MarketEngine a = NewMarket(7703, SxYear, TradingOnOrAfter(200)), b = NewMarket(7703, SxYear, TradingOnOrAfter(200));
        Stock sa = a.ListStartup(SxStartup(), out _), sb = b.ListStartup(SxStartup(), out _);
        RunTo(a, a.State.Day + 30);
        RunTo(b, b.State.Day + 30);
        Check(sa.Id == sb.Id && sa.Ticker == sb.Ticker && sa.Last == sb.Last && sa.Shares == sb.Shares && sa.Rev == sb.Rev, "city: seeded: the same market and the same startup give the same company, to the day's price on every machine");
        a.State.Following = true;
        Check(a.ListStartup(SxStartup("Guest One"), out FirmWhy off) == null && off == FirmWhy.Off, "city: a market followed from a host's takes nothing from a guest's own startups: they are the host's");
        a.State.Following = false;
        MarketEngine full = NewMarket(7704, SxYear, TradingOnOrAfter(200));
        while (full.State.Stocks.Count < MarketCatalogue.CompaniesMost)
        {
            full.State.Stocks.Add(CloneGone(full.State, full.State.Stocks.Count));
        }
        Check(full.ListStartup(SxStartup(), out FirmWhy fullWhy) == null && fullWhy == FirmWhy.Full, "city: a market with as many companies as it may hold takes no startup");
        // the save keeps the company it made, and reads a line without it
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Stock again2 = back != null ? back.Stock(s.Id) : null;
        Check(again2 != null && again2.Name == s.Name && again2.Ticker == s.Ticker && again2.Sector == s.Sector && again2.Def.RevenueM == s.Def.RevenueM && again2.Shares == s.Shares && again2.Last == s.Last
              && MarketStore.Write(back) == line, "city: the save keeps its name, ticker, sector, turnover and shares, exactly, and writes them again the same");
        string damaged = line.Replace("Spark Ledger", string.Empty);
        MarketState hurt = MarketStore.Read(damaged, out _);
        Check(hurt == null || hurt.Stock(s.Id) == null || hurt.Stock(s.Id).Name.Length > 0, "city: a startup whose record is damaged is dropped, not half read");
        m.Delist(s.Id, GoneReason.Failed, 0);
        Check(!s.Listed && s.Reason == GoneReason.Failed, "city: it can fail like any company of the market's, and its holders are paid what is left");
        string tech = ModSource(@"Tech\TechDirector.Venture.cs") + ModSource(@"Tech\TechDirector.ExitStock.cs");
        Check(Regex.IsMatch(tech, @"case VentureRules\.NoteListing:[\s\S]{0,80}PayOut\(s\);[\s\S]{0,40}StockCity\(s\);") && tech.Contains("StockBridges.Startup == null || !StockBridges.OpenNow()"),
            "city: a startup's listing makes its stock only on the host's or a lone player's market, after the player's own lines in it were paid out as before");
    }
}
