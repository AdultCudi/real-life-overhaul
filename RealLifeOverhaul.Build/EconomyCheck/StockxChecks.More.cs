using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// The stock market's extras, part: a bank of the player's own on the market, a studio's way there, the rival tycoons, the
// save's line, the sources, the texts and the cost.
internal static partial class Program
{
    private const ulong StockxMe = 0x57C0FFEEUL;

    private static BankFloat StockxBank(int days, long incomeCents, long profitCents)
    {
        var b = new BankFloat();
        for (int d = 1; d <= days; d++)
        {
            BankFloatRules.AddDay(b, d, incomeCents, profitCents, 60);
        }
        return b;
    }

    // ---------------------------------------------------------------- a bank of the player's own

    private static void StockxBankChecks()
    {
        BankFloat b = StockxBank(300, 4000000, 900000);   // 40,000 of income and 9,000 of profit a day
        int lookBack = ExtrasCatalogue.BankLookBackYears * 60;   // the whole look back a company's listing reads: three game years of 60 days
        Check(b.Days.Count <= ExtrasCatalogue.BankDaysKept && b.Days[0].Day <= 300 - lookBack && b.Days.Count >= lookBack && b.Books.Count <= ExtrasCatalogue.BankQuartersKept
              && b.Books.All(q => q.Days == 15 && q.Revenue == 600000 && q.Profit == 135000),
            "stock extras: the bank's own days make quarters of its own books (15 days of 40,000 and 9,000 each), and its days are kept over the whole look back of three years (" + b.Days.Count + " days)");
        BankFloatRules.AddDay(b, 300, 999999999, 999999999, 60);
        Check(b.Books[b.Books.Count - 1].Revenue == 600000, "stock extras: a day booked twice counts once");
        BankFloatRules.Sum(b, 291, 301, out long revenue, out long profit);
        Check(revenue == 400000 && profit == 90000, "stock extras: ten of the bank's days are 400,000 of takings and 90,000 of profit for the market's morning");
        Check(BankFloatRules.Quarters(b, 301, 60, out int profitable) == 4 && profitable == 4, "stock extras: four complete quarters before the one under way, all in profit");

        var facts = new BankFacts { Running = true, Clear = true, FoundedDay = 1, CapitalCents = 500000000, Name = "Harbour Bank" };
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, false, false) == BankWhy.None, "stock extras: a bank a year old with four profitable quarters of its own books, clear of the regulator, may engage advisers");
        Check(BankFloatRules.Ready(b, facts, 301, 60, false, false, false) == BankWhy.Off, "stock extras: not with the option off, nor as a guest in another player's city");
        Check(BankFloatRules.Ready(b, default(BankFacts), 301, 60, true, false, false) == BankWhy.NoBank, "stock extras: not without a bank");
        BankFacts test = facts;
        test.Test = true;
        Check(BankFloatRules.Ready(b, test, 301, 60, true, false, false) == BankWhy.Test, "stock extras: not a test's bank");
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, true, false) == BankWhy.Company && BankFloatRules.Ready(b, facts, 301, 60, true, false, true) == BankWhy.Company,
            "stock extras: not while the player's whole company is on the market or on its way: the bank is part of it already");
        BankFacts young = facts;
        young.FoundedDay = 280;
        Check(BankFloatRules.Ready(b, young, 301, 60, true, false, false) == BankWhy.Young, "stock extras: not in its first year");
        Check(BankFloatRules.Ready(StockxBank(40, 4000000, 900000), new BankFacts { Running = true, Clear = true, FoundedDay = -100, CapitalCents = 1 }, 41, 60, true, false, false) == BankWhy.Books,
            "stock extras: not with fewer than " + ExtrasCatalogue.BankQuartersLeast + " complete quarters of its own books");
        Check(BankFloatRules.Ready(StockxBank(300, 4000000, -5), facts, 301, 60, true, false, false) == BankWhy.Profit, "stock extras: not without a profit in three of its last four quarters");
        BankFacts watched = facts;
        watched.Clear = false;
        Check(BankFloatRules.Ready(b, watched, 301, 60, true, false, false) == BankWhy.Regulator, "stock extras: not while the regulator watches it or savers run on it");
        b.Stage = BankStage.Advised;
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, false, false) == BankWhy.OnTheWay, "stock extras: not twice at once");
        b.Stage = BankStage.Listed;
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, false, false) == BankWhy.Listed, "stock extras: nor once it trades");
        b.Stage = BankStage.None;

        // Its worth: the market's model on its own profit, never over so many times its own capital. Nothing else is asked of the bank.
        MarketEnv env = MarketEnv.Quiet();
        double model = ListingBook.Worth(BankFloatRules.File(b, "Harbour Bank"), StockxMe, 301, 60, env, 0L);
        double worth = BankFloatRules.Worth(b, facts, StockxMe, 301, 60, env);
        Check(model > 0 && worth > 0 && worth <= model + 1e-6 && worth <= facts.CapitalCents / 100.0 * ExtrasCatalogue.BankBookMost + 1e-6, "stock extras: the bank is worth what the market's model makes of its own books ("
            + ((long)model) + "), never over " + ExtrasCatalogue.BankBookMost + " times its own capital (" + ((long)worth) + ")");
        BankFacts thin = facts;
        thin.CapitalCents = 1000000;   // 10,000 of capital under the same profit: the rest was earned on other people's money
        Check(Math.Abs(BankFloatRules.Worth(b, thin, StockxMe, 301, 60, env) - 30000.0) < 1e-6, "stock extras: a bank with 10,000 of its own capital is worth 30,000 at most, whatever it earns on its savers' money");
        thin.CapitalCents = 0;
        Check(BankFloatRules.Worth(b, thin, StockxMe, 301, 60, env) == 0.0, "stock extras: a bank with no capital of its own is worth nothing to the market");
        Check(typeof(BankFacts).GetFields().All(f => !f.Name.Contains("Deposit") && !f.Name.Contains("Reserve") && !f.Name.Contains("Savings")),
            "stock extras: savers' deposits and the cash that covers them are not among what the market is told of the bank");

        double written = BankFloatRules.Prospectus(b, facts, 2000, StockxMe, 301, 60, env);
        Check(Math.Abs(written - worth) < 1e-6 && b.FloatBp == 2000 && b.OldShares >= ListingCatalogue.FounderSharesLeast && b.NewShares == ListingBook.NewShares(b.OldShares, 2000)
              && b.LowCents > 0 && b.HighCents > b.LowCents && b.LowCents < b.FairCents, "stock extras: the prospectus offers a fifth of the bank in new shares, in a range under what the books say a share is worth ("
                                                                                       + b.LowCents + " to " + b.HighCents + " cents, fair " + b.FairCents + ")");
        Check(Math.Abs(b.FairCents - worth * 100.0 / b.OldShares) <= 1.0, "stock extras: a share is the bank's worth over the founder's shares, before the offer's money comes in");
        Check(BankFloatRules.FloatStep(2100) == 2000 && BankFloatRules.FloatStep(9000) == 4000 && ExtrasCatalogue.BankFloatSteps.Max() < 5000, "stock extras: under half the bank is offered, in steps");
        int same = 0, inRange = 0, calledOff = 0;
        for (ulong secret = 1; secret <= 200; secret++)
        {
            int price = BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, facts.CapitalCents);
            same += price == BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, facts.CapitalCents) ? 1 : 0;
            inRange += price == 0 || (price >= b.LowCents && price <= b.HighCents * (1.0 + ListingCatalogue.PriceBeyondRange) + ListingCatalogue.PriceStepCents) ? 1 : 0;
            calledOff += price == 0 ? 1 : 0;
        }
        Check(same == 200 && inRange == 200, "stock extras: the book's price is drawn from the day's secret as a company's is: the same for the same secret, at or over the range's bottom or called off ("
                                             + calledOff + " of 200 called off)");
        Check(calledOff > 0 && calledOff < 150, "stock extras: a book under the bottom of its range is called off, and most are not");
        b.PriceCents = b.LowCents + ListingCatalogue.PriceStepCents;
        long raised = BankFloatRules.Raised(b, b.PriceCents, 1.0, out long underwriting, out long exchange);
        long gross = MarketTrading.Value(b.NewShares, b.PriceCents, false);
        Check(raised > 0 && underwriting > 0 && exchange > 0 && raised + underwriting + exchange == gross, "stock extras: the offer's money is the new shares at the price, less the underwriters and the exchange ("
                                                                                                    + gross + " = " + raised + " + " + underwriting + " + " + exchange + ")");

        // On the market: a stock of its own with the bank's place, kept when the tech sector sweeps its firms, its founder's deals told for the tax return.
        MarketEngine m = StockxMarket(1101, 500000000, out MarketWorld world, 20);
        MarketState st = m.State;
        st.Me = StockxMe;
        int id = m.BankStockId();
        Check(id == m.BankStockId() && id >= ListingCatalogue.IdFirst, "stock extras: the bank's stock has an id of its own among the player's");
        Stock k = m.ListFirm(BankFloatRules.Listing(b, "Harbour Bank", st.Day, 60), out FirmWhy why);
        Check(k != null && why == FirmWhy.None && k.Own != null && k.Own.Firm && BankFloatRules.IsBank(k.Own.Place) && k.Own.Founder == b.OldShares && k.Own.Float == b.NewShares
              && k.Shares == b.OldShares + b.NewShares && k.Sector == MarketSector.Banks && k.Last == b.PriceCents && k.Own.LockDay > st.Day,
            "stock extras: the bank lists by the path a firm of the player's lists by: a stock among the banks at the book's price, the founder locked up");
        if (k == null)
        {
            return;
        }
        Check(m.SweepFirms(new List<string>()) == 0 && k.Listed, "stock extras: the tech sector's sweep of firms it no longer lists leaves the bank's stock: the bank is no tech firm");
        Check(m.BlockQuoteOf(k.Id, true, 100000).Why == BlockWhy.Founders, "stock extras: nobody takes a block of the bank's stock either");
        int dealt = 0;
        long dealtShares = 0, dealtMoney = 0, dealtFee = 0;
        m.FirmDealt = (stock, shares, cents, money, fee) =>
        {
            dealt += stock == k.Id ? 1 : 0;
            dealtShares = shares;
            dealtMoney = money;
            dealtFee = fee;
        };
        k.Own.LockDay = 0;
        k.ListedDay = st.Day - 40;
        long founder = k.Own.Founder;
        long sale = Math.Min(100L, MarketTrading.MostPerOrder(k));
        m.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, k.Id, (int)-sale);
        RunTo(m, st.Day, st.Phase + 1);
        Check(dealt == 1 && dealtShares == -sale && k.Own.Founder == founder - sale && dealtMoney > 0, "stock extras: the founder's sale of the bank's shares is told once, with its money, for the tax return (" + dealt + " told)");
        var gains = new GainFile();
        long invested = 30000000;   // what the bank was founded with, cents at the start's prices
        long gain = ExitGains.ForMarketDeal(gains, invested, ExtrasCatalogue.BankPlace, "Harbour Bank", dealtMoney, dealtFee, founder, dealtShares, st.Day, 1000);
        long slice = (long)((double)invested * sale / founder);
        Check(gain == Math.Max(0L, (dealtMoney - dealtFee) * 100 - slice) && ExitGains.Between(gains, st.Day, st.Day) == gain / 100 && gains.Lines.Count(l => l.Cents != 0) == 1 && ExitGains.Between(gains, st.Day + 1, st.Day + 100) == 0,
            "stock extras: the sale's gain is what it fetched less its part of what the bank was founded with, noted once for the quarter's return (" + gain + " cents)");
        Check(gains.BasisOf(ExtrasCatalogue.BankPlace, false) != null && gains.BasisOf(ExitGains.CompanyPlace, false) == null && gains.BasisOf(ExtrasCatalogue.BankPlace, false).Used == slice,
            "stock extras: ...on the bank's own line of the cost, apart from the whole company's");
        long noted = gains.Lines.Sum(l => l.Cents);
        ExitGains.ForMarketDeal(gains, invested, ExtrasCatalogue.BankPlace, "Harbour Bank", 40000, 40, founder - sale, 300, st.Day, 1000);
        Check(gains.Lines.Sum(l => l.Cents) == noted && gains.BasisOf(ExtrasCatalogue.BankPlace, false).Bought > 0, "stock extras: shares bought back note no gain: what they cost joins the cost of the stake");
        Check(m.EndFirm(k.Id, GoneReason.TakenOver, 1234) && !k.Listed && k.GoneCents == 1234, "stock extras: when the bank is sold its stock leaves the market, each share paid its part");

        // What the bank pays its owner once other people hold shares of it.
        Check(BankFloatRules.OwnerPart(1000000, 750, 1000) == 750000 && BankFloatRules.OwnerPart(999, 1, 3) == 333 && BankFloatRules.OwnerPart(1000000, 1000, 1000) == 1000000
              && BankFloatRules.OwnerPart(1000000, 5, 0) == 1000000 && BankFloatRules.OwnerPart(0, 1, 2) == 0 && BankFloatRules.OwnerPart(-5, 1, 2) == 0,
            "stock extras: of what the bank pays its owner the founder has their shares' part, rounded down; all of it while the bank is theirs alone");
        bool never = true;
        for (long cents = 1; cents < 200000; cents += 7919)
        {
            for (long f = 0; f <= 1000; f += 125)
            {
                long part = BankFloatRules.OwnerPart(cents, f, 1000);
                never &= part >= 0 && part <= cents && part <= cents * f / 1000 + 1;
            }
        }
        Check(never, "stock extras: the owner's part is never more than their shares' part of the sum, at any holding");

        // A studio's or a consultancy's way to the market: its real takings count, not recurring revenue alone.
        long least = ExitCatalogue.ListMrrLeast;
        var exit = new FirmExit();
        Check(!ExitRules.ListReady(0, exit) && ExitRules.ListReady(ExitRules.ListMonth(0, least), exit),
            "stock extras: a game studio with no recurring revenue and a Series B's month of takings may start the way to the market, where the old gate never opened");
        Check(ExitRules.ListMonth(12 * least, 0) == least && ExitRules.ListMonth(1200, 500) == 500 && ExitRules.ListMonth(1200, 50) == 100 && ExitRules.ListMonth(-5, -5) == 0,
            "stock extras: a firm's month is its recurring revenue or its real takings of thirty days, whichever is more");
        Check(!ExitRules.ListReady(ExitRules.ListMonth(0, least - 1), exit), "stock extras: ...and a month under a Series B's still is not enough");
        Check(ExitRules.ListValue(100, 400) == 400 && ExitRules.ListValue(400, 100) == 400 && ExitRules.ListValue(400, 0) == 400 && ExitRules.ListValue(-1, -1) == 0,
            "stock extras: a firm is worth for its listing what the market's model makes of its books when that is more than its usual value");
    }

    // ---------------------------------------------------------------- the rival tycoons

    private static void StockxRivalChecks()
    {
        MarketEngine m = StockxMarket(1201, 1000000, out MarketWorld world, 20);
        MarketState st = m.State;
        int[] holdings = MarketCatalogue.All.Where(d => d.Holding).Select(d => d.Id).ToArray();
        Check(holdings.Length >= 3, "stock extras: the market knows the tycoons' holdings");
        m.ListHolding(holdings[0], 60000000);
        m.ListHolding(holdings[1], 90000000);
        foreach (int h in holdings.Take(2))
        {
            st.Stock(h).ListedDay = st.Day - 60;
        }
        var days = new List<int>[3];
        for (int t = 0; t < 3; t++)
        {
            days[t] = new List<int>();
            var book = new RivalBook();
            for (int day = 1; day <= 60; day++)
            {
                if (RivalMarket.NextLook(t, day, book) == day)
                {
                    days[t].Add(day);
                    book.LookDay = day;
                    Check(RivalMarket.NextLook(t, day, book) == 0, "stock extras: tycoon " + t + " looks at the market once on their day " + day);
                }
            }
        }
        Check(days.All(d => d.Count == 2 && d[1] - d[0] == ExtrasCatalogue.RivalEveryDays) && days[0][0] != days[1][0] && days[1][0] != days[2][0],
            "stock extras: each tycoon looks at the market every " + ExtrasCatalogue.RivalEveryDays + " days, on a day of their own");

        // The fund: a step of their worth at a time, to their share of it and no further.
        var rich = new RivalBook();
        long worth = 100000000;
        double nav = FundRules.Nav(st) / 100.0;
        int steps = 0;
        for (int i = 0; i < 10; i++)
        {
            long units = RivalMarket.FundUnits(st, rich, worth);
            if (units <= 0)
            {
                break;
            }
            Check(units * nav <= worth * (double)ExtrasCatalogue.RivalFundStepBp / 10000.0 + nav, "stock extras: a tycoon buys at most " + ExtrasCatalogue.RivalFundStepBp + " bp of their worth in units at a look");
            rich.Units += units;
            steps++;
        }
        Check(steps >= 3 && steps <= 4 && rich.Units * nav <= worth * (double)ExtrasCatalogue.RivalFundBp / 10000.0 + nav && RivalMarket.FundUnits(st, rich, worth) == 0,
            "stock extras: ...until they hold " + ExtrasCatalogue.RivalFundBp + " bp of their worth in the fund, in " + steps + " looks, and then no more");
        Check(RivalMarket.FundUnits(st, new RivalBook(), 0) == 0, "stock extras: a tycoon with nothing buys nothing");

        // A block: drawn from the market's id, the tycoon and the day; never their own holding nor a player's company; within what a holder may have.
        Stock mine = StockxCity(m, 6);
        mine.Own = new OwnListing { Owner = 5, Founder = 1 };
        var picks = new HashSet<int>();
        bool allowed = true, again = true, others = false;
        for (int day = 1; day <= 240; day++)
        {
            var book = new RivalBook();
            int pick = RivalMarket.Pick(st, 0, holdings[0], day, book);
            again &= pick == RivalMarket.Pick(st, 0, holdings[0], day, book);
            Stock k = pick > 0 ? st.Stock(pick) : null;
            allowed &= k != null && k.Listed && k.Own == null && k.Id != holdings[0] && k.BidCents <= 0;
            others |= pick == holdings[1];
            picks.Add(pick);
        }
        mine.Own = null;
        Check(allowed && again, "stock extras: a tycoon's block is of a listed company, never their own holding nor a player's company, and the same for the same day");
        Check(picks.Count >= 20 && others, "stock extras: over 240 days they pick " + picks.Count + " different companies, another tycoon's holding among them");
        var full = new RivalBook();
        for (int i = 0; i < ExtrasCatalogue.RivalStakesMost; i++)
        {
            full.Stakes.Add(new RivalStake { Stock = StockxCity(m, i).Id, Shares = 1, Cost = 1 });
        }
        Check(RivalMarket.Pick(st, 0, holdings[0], 5, full) == 0, "stock extras: a tycoon holds blocks of at most " + ExtrasCatalogue.RivalStakesMost + " companies at once");
        Stock target = StockxCity(m, 0, MarketSize.Mid) ?? StockxCity(m, 0);
        Quote q = MarketTrading.QuoteOf(st, target, MarketCatalogue.PhaseOpen + 1);
        long block = RivalMarket.BlockShares(st, target, 100000000000, out int cents);
        Check(block > 0 && block <= target.Shares * ExtrasCatalogue.RivalStakeMostBp / 10000L && block <= MarketTrading.MostHeld(target) && cents >= q.Ask,
            "stock extras: with all the money in the world a tycoon's block is at most " + ExtrasCatalogue.RivalStakeMostBp + " bp of a company and the most a holder may have, bought over the offer");
        long modest = RivalMarket.BlockShares(st, target, 500000, out int modestCents);
        Check(modest > 0 && MarketTrading.Value(modest, modestCents, true) <= 500000 && modest < block, "stock extras: ...and no more than their money buys");
        Check(RivalMarket.Sold(st, target, block) < cents && RivalMarket.Sold(st, target, block) <= q.Bid, "stock extras: a tycoon who buys a block and sells it loses by it too: sold under the bid, bought over the offer");
        int sells = 0, arms = 0, kinds = 0;
        var holder = new RivalBook();
        holder.Stakes.Add(new RivalStake { Stock = target.Id, Shares = 100, Cost = 1000, Day = 3 });
        holder.Stakes.Add(new RivalStake { Stock = StockxCity(m, 1).Id, Shares = 100, Cost = 1000, Day = 2 });
        for (int day = 1; day <= 2000; day++)
        {
            if (RivalMarket.Sells(st, 1, day, holder, out int at))
            {
                sells++;
                again &= at == 1;
            }
            if (RivalMarket.ListsArm(st, 1, day, new RivalBook(), true, out int kind))
            {
                arms++;
                kinds += kind;
            }
        }
        Check(again && sells > 2000 * ExtrasCatalogue.RivalSellChance / 1000 * 3 / 4 && sells < 2000 * ExtrasCatalogue.RivalSellChance / 1000 * 5 / 4,
            "stock extras: a tycoon with blocks sells the oldest at about " + ExtrasCatalogue.RivalSellChance + " looks in a thousand (" + sells + " of 2000)");
        Check(arms > 2000 * ExtrasCatalogue.RivalArmChance / 1000 * 3 / 4 && arms < 2000 * ExtrasCatalogue.RivalArmChance / 1000 * 5 / 4 && kinds > arms / 4 && kinds < arms * 3 / 4,
            "stock extras: a tycoon whose holding is listed puts an arm on the market at about " + ExtrasCatalogue.RivalArmChance + " looks in a thousand, a bank or a lab alike (" + arms + ", " + kinds + " labs)");
        Check(!RivalMarket.ListsArm(st, 1, 7, new RivalBook(), false, out int _) && !RivalMarket.Sells(st, 1, 7, new RivalBook(), out int _), "stock extras: no arm without a listed holding, and nothing sold by a tycoon who holds nothing");
        bool once = true;
        for (int day = 1; day <= 400; day++)
        {
            once &= !RivalMarket.ListsArm(st, 1, day, new RivalBook { ArmStock = 2001 }, true, out int _);
        }
        Check(once, "stock extras: a tycoon lists one arm in all");
        Check(RivalMarket.ArmName("Karen Aerospace & Motors", 0) == "Karen Bank" && RivalMarket.ArmName("Luthor Everything Co.", 1) == "Luthor Labs" && RivalMarket.ArmName(string.Empty, 0) == "Bank",
            "stock extras: an arm is named after its holding (Karen Bank, Luthor Labs)");
        StartupListing arm = RivalMarket.Arm("Karen Bank", 0, 80000000, 1000);
        Stock listed = m.ListStartup(arm, out FirmWhy armWhy);
        double value = listed != null ? listed.Shares * (double)listed.Last / 100.0 : 0.0;
        Check(listed != null && armWhy == FirmWhy.None && listed.Sector == MarketSector.Banks && listed.Own == null && Math.Abs(value - 80000000.0 * ExtrasCatalogue.RivalArmWorthBp / 10000.0) < value * 0.02,
            "stock extras: the arm comes to the market as a company of the market's own among the banks, worth " + ExtrasCatalogue.RivalArmWorthBp + " bp of its tycoon's fortune (" + (long)value + ")");
        Check(RivalMarket.Arm("Luthor Labs", 1, 80000000, 1000).Sector == (int)MarketSector.Software, "stock extras: a lab comes among the software companies");

        // Their block is a trade on the tape: the price answers at the market's next moment, on every machine, and a holding past 5% is told.
        MarketEngine a = StockxMarket(1202, 1000000, out MarketWorld wa, 20), b = StockxMarket(1202, 1000000, out MarketWorld _, 20);
        a.ListHolding(holdings[0], 60000000);
        b.ListHolding(holdings[0], 60000000);
        Stock sa = StockxCity(a, 0, MarketSize.Mid), sb = b.State.Stock(sa.Id), other = StockxCity(a, 3);
        long qty = sa.Shares * 6 / 100;
        Check(a.RivalBlock(holdings[0], sa.Id, qty, true, 600) && Math.Abs(sa.Mid - sb.Mid) < 1e-12, "stock extras: a tycoon's block goes on the tape and moves nothing in the same moment");
        RunTo(a, a.State.Day, a.State.Phase + 1);
        RunTo(b, b.State.Day, b.State.Phase + 1);
        Check(sa.Mid > sb.Mid * 1.0001 && Math.Abs(other.Mid - b.State.Stock(other.Id).Mid) < 1e-9 * other.Mid && sa.DayVolume >= sb.DayVolume + qty - 100,
            "stock extras: ...at the market's next moment its price is up by the block's lasting move and its volume by its shares; no other company moves ("
            + sb.Mid.ToString("0.000") + " to " + sa.Mid.ToString("0.000") + ")");
        Check(wa.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == sa.Id && n.A >= 600 && n.B == 1 && n.C == holdings[0]) == 1, "stock extras: a tycoon's holding past 5% is market news, by their holding's name");
        var twin = new MarketWorld(1202);
        MarketEngine replay = NewMarket(1202, 60, 1, 0, twin, a.Tape);
        RunTo(replay, a.State.Day, a.State.Phase);
        Check(replay.State.Stock(sa.Id) != null && Math.Abs(replay.State.Stock(sa.Id).Mid - sa.Mid) < 1e-9 * sa.Mid, "stock extras: a machine replaying the tape sees the same price after the block");
        a.State.Following = true;
        Check(!a.RivalBlock(holdings[0], sa.Id, qty, true, 600), "stock extras: no tycoon deals on a market that follows another player's");
        a.State.Following = false;

        // A block of a company that left is paid what each share was; what a tycoon holds is counted at today's prices.
        var book2 = new RivalBook { Units = 1000 };
        Stock gone = StockxCity(a, 4), kept = StockxCity(a, 5);
        book2.Stakes.Add(new RivalStake { Stock = gone.Id, Shares = 1000, Cost = 700 });
        book2.Stakes.Add(new RivalStake { Stock = kept.Id, Shares = 2000, Cost = 900 });
        long worthNow = RivalMarket.Worth(a.State, book2);
        Check(Math.Abs(worthNow - (1000 * FundRules.Nav(a.State) / 100.0 + 1000 * gone.Last / 100.0 + 2000 * kept.Last / 100.0)) <= 2, "stock extras: what a tycoon holds on the market is their units and blocks at today's prices");
        a.Delist(gone.Id, GoneReason.Failed, 150);
        Check(RivalMarket.Sweep(a.State, book2) == 1500 - 700 && book2.Stakes.Count == 1 && book2.Stakes[0].Stock == kept.Id, "stock extras: a block of a company that left is paid out at what each share was, the gain or the loss theirs");
    }

    // ---------------------------------------------------------------- the save's line

    private static void StockxSaveChecks()
    {
        var x = new ExtrasState { Units = 1234, Cost = 5678, Realised = -90, Paid = 12, Fees = 3, Track = 0.9987654321012345, TrackDay = 77, OddCents = 42 };
        x.Dues.Add(new FundDue { PayDay = 80, Cents = 4567 });
        x.NoteSplit(31, 60, 2, 1);
        x.NoteSplit(32, 61, 1, 10);
        x.Push(31, 0.00123456789);
        x.Push(40, -0.004);
        x.SetTold(31, 0, 500);
        x.SetTold(40, 143, 1000);
        x.Rivals[1].Units = 9;
        x.Rivals[1].UnitsCost = 900;
        x.Rivals[1].LookDay = 65;
        x.Rivals[1].ArmStock = 2003;
        x.Rivals[1].ArmKind = 1;
        x.Rivals[1].ArmDay = 50;
        x.Rivals[1].Stakes.Add(new RivalStake { Stock = 31, Shares = 5000, Cost = 60000, Day = 44 });
        x.Rivals[2].Stakes.Add(new RivalStake { Stock = 40, Shares = 7, Cost = 8, Day = 9 });
        BankFloat k = x.Bank;
        k.Stage = BankStage.Advised;
        k.AdvisedDay = 70;
        k.PriceDay = 76;
        k.FloatBp = 2000;
        k.FairCents = 1100;
        k.LowCents = 900;
        k.HighCents = 1050;
        k.OldShares = 800000;
        k.NewShares = 200000;
        k.PriceCents = 950;
        k.Raised = 1800000;
        k.Fees = 25000;
        k.StockId = 1500;
        k.ListDay = 76;
        k.PaidOthers = 333;
        BankFloatRules.AddDay(k, 69, 4000000, 900000, 60);
        BankFloatRules.AddDay(k, 70, 4100000, -50000, 60);
        string line = ExtrasStore.Write(x);
        ExtrasState back = ExtrasStore.Read(line, out int version);
        Check(back != null && version == ExtrasStore.Version && ExtrasStore.Write(back) == line, "stock extras save: the line reads back to the same line");
        Check(back != null && back.Units == 1234 && back.Cost == 5678 && back.Realised == -90 && back.Track == x.Track && back.TrackDay == 77 && back.OddCents == 42 && back.Dues.Count == 1 && back.Dues[0].Cents == 4567,
            "stock extras save: the units, their cost, the fund's standing to the last digit, the cents kept and what is owed");
        Check(back != null && back.Splits.Count == 2 && back.Splits[1].Reverse && back.PushOf(31) == 0.00123456789 && back.PushOf(40) == -0.004 && back.ToldBp(40, 143) == 1000 && back.ToldBp(31, 0) == 500,
            "stock extras save: the splits for the charts, the player's own push and what was told of holdings");
        Check(back != null && back.Rivals[1].Units == 9 && back.Rivals[1].ArmStock == 2003 && back.Rivals[1].ArmKind == 1 && back.Rivals[1].Stakes.Count == 1 && back.Rivals[1].Stakes[0].Shares == 5000
              && back.Rivals[2].Stakes[0].Cost == 8 && back.Rivals[0].Empty && back.Rivals[1].LookDay == 65, "stock extras save: each tycoon's units, blocks and arm");
        Check(back != null && back.Bank.Stage == BankStage.Advised && back.Bank.PriceDay == 76 && back.Bank.NewShares == 200000 && back.Bank.PaidOthers == 333 && back.Bank.Days.Count == 2
              && back.Bank.Days[1].Profit == -50000 && back.Bank.Books.Count == k.Books.Count && back.Bank.Books[0].Revenue == k.Books[0].Revenue,
            "stock extras save: the bank's way to the market in the middle of it, and its own books");

        // An old save, a line of a later version, a damaged one.
        var data = new Dictionary<string, string>();
        var state = new MarketState();
        ExtrasStore.Load(data, state);
        Check(state.Extras.Empty && state.Extras.Track == 1.0 && state.Extras.Units == 0 && state.Extras.Bank.Stage == BankStage.None, "stock extras save: a save from before has no line and starts with nothing");
        ExtrasStore.Save(data, state);
        Check(!data.ContainsKey(ExtrasStore.Key), "stock extras save: a market with no extras writes no line at all");
        state.Extras.Units = 5;
        ExtrasStore.Save(data, state);
        Check(data.ContainsKey(ExtrasStore.Key) && ExtrasStore.Key == "reallife:stock_extras", "stock extras save: units held are written under the extras' own key");
        ExtrasState later = ExtrasStore.Read(line.Replace("v1|", "v9|").Replace("|d:", ",777,888|z:1,2;3|d:"), out int laterVersion);
        Check(later != null && laterVersion == 9 && later.Units == 1234 && later.OddCents == 42 && later.Dues.Count == 1 && later.Bank.PriceDay == 76,
            "stock extras save: a line of a later version, with fields and a part this one does not know, is read as far as it goes");
        Check(ExtrasStore.Read("nonsense", out int _) == null && ExtrasStore.Read(string.Empty, out int _) == null && ExtrasStore.Read("vx|f:1", out int _) == null, "stock extras save: a line that is none of these is not read");
        ExtrasState odd = ExtrasStore.Read("v1|f:-5,-6,7,-8,-9,77.5,3,5000|s:31,60,2,2;31,60,99,1;0,1,2,1|p:31,NaN;32,9|b:99", out int _);
        Check(odd != null && odd.Units == 0 && odd.Cost == 0 && odd.Track == 1.0 && odd.OddCents == 99 && odd.Splits.Count == 0 && odd.Pushes.Count == 0 && odd.Bank.Stage == BankStage.Ended,
            "stock extras save: figures out of bounds are held to them: no units under nothing, a standing that cannot be, a split that is none, a push that is no number");

        // A guest in another player's city keeps their own extras, and nothing of the host's comes over.
        MarketEngine hostMarket = StockxMarket(1310, 1000000, out MarketWorld _, 20), guestMarket = StockxMarket(1311, 1000000, out MarketWorld _, 20);
        hostMarket.State.Extras.Units = 999;
        hostMarket.State.Extras.Bank.Stage = BankStage.Listed;
        guestMarket.State.Extras.Units = 77;
        guestMarket.State.Extras.OddCents = 5;
        guestMarket.State.Extras.Track = 0.75;
        guestMarket.State.Extras.Bank.Stage = BankStage.Advised;
        string sent = MarketShare.Write(hostMarket.State, hostMarket.Tape);
        MarketState adopted = MarketShare.Adopt(guestMarket.State, sent, new List<long>(), null, out MarketTape _, out long _);
        Check(adopted != null && adopted.Extras.Units == 77 && adopted.Extras.OddCents == 5 && adopted.Extras.Track == 0.75 && adopted.Extras.Bank.Stage == BankStage.Advised,
            "stock extras save: a guest takes their own units, cents kept and bank's way into the host's market, and nothing of the host's extras");
        MarketState hostRead = MarketStore.Read(sent.Substring(0, sent.IndexOf((char)10)), out int _);
        Check(!sent.Contains("stock_extras") && hostRead != null && hostRead.Extras.Empty, "stock extras save: the line a host sends is the market's own line, with no part for the host's extras");
        string goingPublic = ModSource(@"Markets\MarketDirector.GoingPublic.cs");
        Check(goingPublic.Contains("HookExtras();") && ModSource(@"Markets\MarketShare.cs").Contains("host.Extras.CopyFrom(mine.Extras);"),
            "stock extras save: the engine made for a guest hears the extras as the first one does");

        // The market's own line keeps shares to be put together.
        MarketEngine m = StockxMarket(1301, 1000000, out MarketWorld _, 20);
        Stock s = StockxCity(m, 0), t = StockxCity(m, 1);
        s.SplitDay = m.State.Day + 3;
        s.SplitRatio = -10;
        t.SplitDay = m.State.Day + 3;
        t.SplitRatio = -1;
        MarketState read = MarketStore.Read(MarketStore.Write(m.State), out int _);
        Check(read != null && read.Stock(s.Id).SplitRatio == -10 && read.Stock(s.Id).SplitDay == s.SplitDay && read.Stock(t.Id).SplitDay == 0,
            "stock extras save: the market's line keeps a consolidation to come, and drops a ratio that is none");
        Check(MarketNewsKind.Reverse == (MarketNewsKind)34 && MarketNewsKind.Block == (MarketNewsKind)35 && MarketInputs.EventSplit == MarketInputs.EventBuilder + 1 && MarketRandom.Extras == 42,
            "stock extras save: the news kinds, the tape's event and the draw have numbers of their own after every other (never renumbered)");
    }

    // ---------------------------------------------------------------- the sources

    private static void StockxSourceChecks()
    {
        string session = ModSource(@"Markets\MarketEngine.Session.cs"), close = ModSource(@"Markets\MarketEngine.Close.cs"), engine = ModSource(@"Markets\MarketEngine.cs");
        Check(session.Contains("SplitNow(s, day)") && !session.Contains("MarketLife.Split(State, s, s.SplitRatio)"), "stock extras sources: every split at an open goes through the one place that also puts shares together");
        Check(session.Contains("State.Extras.Fade(fade)") && session.Contains("State.Extras.Fade(Kappa * _openNight)"), "stock extras sources: the player's own push fades at each step and each open, as the mood does");
        Check(close.Contains("ExtrasDues(day)") && close.Contains("ExtrasCorporate(day)") && close.Contains("FundRules.Left(State, s, centsEach)") && close.IndexOf("FundRules.Left(State, s, centsEach)", StringComparison.Ordinal)
              < close.IndexOf("s.Status = StockStatus.Gone", StringComparison.Ordinal), "stock extras sources: the close runs the fund, and a company that leaves is counted before it is taken out of the index");
        Check(close.IndexOf("Dividends(day);", StringComparison.Ordinal) < close.IndexOf("ExtrasDues(day);", StringComparison.Ordinal) && close.IndexOf("ExtrasDues(day);", StringComparison.Ordinal)
              < close.IndexOf("Corporate(day);", StringComparison.Ordinal) && close.IndexOf("Corporate(day);", StringComparison.Ordinal) < close.IndexOf("ExtrasCorporate(day);", StringComparison.Ordinal),
            "stock extras sources: the fund's dues are counted after the players' own and before any company leaves, and the splits are decided after the companies' own lives");
        Check(engine.Contains("e.Kind == MarketInputs.EventSplit") && engine.Contains("SplitEvent(e)"), "stock extras sources: a split from the tape is applied where every event is");
        Check(ModSource(@"Markets\MarketEngine.Orders.cs").Contains("State.Extras.Push(s.Id, kept)") && ModSource(@"Markets\MarketEngine.Bots.cs").Contains("State.Extras.Push(s.Id, kept)")
              && ModSource(@"Markets\MarketEngine.Own.cs").Contains("State.Extras.Push(s.Id, kept + reading)"), "stock extras sources: every fill of the player's, a bot's and a founder's adds to their own push");
        Check(ModSource(@"Markets\InvestRules.cs").Contains("FundRules.Worth(s, s.Extras.Units, phase)"), "stock extras sources: the units count in what the player holds on the market");
        string director = ModSource(@"Markets\MarketDirector.cs");
        Check(director.Contains("ExtrasStore.Load(save.modData, state)") && Regex.Matches(director, @"ExtrasStore\.Save\(Save\.modData, _engine\.State\)").Count == 2 && director.Contains("ExtrasSecond();")
              && director.Contains("HookExtras();"), "stock extras sources: the director loads the line at the start, writes it at a save and when it stands down, and looks once a second");
        string extras = ModSource(@"Markets\Extras\MarketDirector.Extras.cs"), bank = ModSource(@"Markets\Extras\MarketDirector.ExtrasBank.cs"), tests = ModSource(@"Markets\Extras\MarketDirector.ExtrasTests.cs");
        foreach (string file in new[] { extras, bank, tests, ModSource(@"Markets\Extras\MarketEngine.Extras.cs"), ModSource(@"Markets\Extras\FundRules.cs"), ModSource(@"Markets\Extras\RivalMarket.cs") })
        {
            Check(file.Length > 0 && !file.Contains("UnityEngine.Random") && !file.Contains("void Update(") && !file.Contains("void LateUpdate(") && !file.Contains("System.Random"),
                "stock extras sources: no frame's work and no chance of its own outside the market's seeded draws");
        }
        Check(bank.IndexOf("BankWhy why = BankWhyNow(out double worth);", StringComparison.Ordinal) < bank.IndexOf("_engine.Wallet.Take(retainer", StringComparison.Ordinal)
              && bank.IndexOf("_engine.Wallet.Take(retainer", StringComparison.Ordinal) < bank.IndexOf("b.Stage = BankStage.Advised;", StringComparison.Ordinal),
            "stock extras sources: the bank's advisers are refused before any money moves, and engaged only once the retainer is paid");
        Check(bank.Contains("ExtrasBridges.BankCapital(raised * 100L)") && !bank.Contains("Wallet.Give("), "stock extras sources: the offer's money goes into the bank's capital and never into the player's cash");
        Check(bank.Contains("ExtrasOpen && FirmsOpen") && extras.Contains("!_following && !GoingPublic.IsGuest"), "stock extras sources: a guest in another player's city changes nothing of the market");
        string links = ModSource(@"Wiring\Wiring.StockExtrasLinks.cs"), game = ModSource(@"Economy\OwnBank\OwnBankGame.cs"), bankDirector = ModSource(@"Economy\OwnBank\OwnBankDirector.cs");
        Check(links.Contains("CapitalCents = gone ? b.EndCents : b.Capital") && !links.Contains("Deposits") && !links.Contains("b.Reserves") && !links.Contains("Savings"),
            "stock extras sources: the bank is read by its own capital alone: no deposit and no reserve reaches the market");
        Check(game.Contains("Book(Owner(d.Pay), OwnBankCatalogue.PayKey") && game.Contains("Book(Owner(d.PaidOut), OwnBankCatalogue.CapitalOutKey") && game.Contains("cents = Owner(cents);")
              && game.Contains("key == OwnBankCatalogue.CapitalOutKey || key == OwnBankCatalogue.SaleKey"), "stock extras sources: the owner's pay, a dividend, a sale and a winding up are all shared with the bank's other holders");
        Check(bankDirector.Contains("public bool PublicCapital(long cents)") && bankDirector.Contains("OwnBankBridges.DayBooked?.Invoke(day, _day.Income, _day.Profit, _bank.Capital)") && !bankDirector.Substring(
                  bankDirector.IndexOf("public bool PublicCapital(long cents)", StringComparison.Ordinal), 600).Contains("OwnBankGame.Pay("),
            "stock extras sources: the public's money is put into the bank without touching the player's cash, and each day of the bank's books is told");
        Check(links.Contains("MarketBridges.Books = Books;") && links.Contains("books.Revenue -= revenue;") && ModSource(@"Wiring\Wiring.ExitStockLinks.cs").Contains("m.BankDays(from, day, out long bankRevenue, out long bankProfit)"),
            "stock extras sources: the bank's days feed its own stock and are taken out of the whole company's books, so the same takings never feed two stocks");
        Check(ModSource(@"Markets\MarketDirector.Firms.cs").Contains("if (BankDealt(stockId, shares, money, fee))") && ModSource(@"Tech\TechDirector.ExitGains.cs").Contains("internal void StakeDealt(string place, long invested")
              && links.Contains("StakeDealt(place, invested, name, shares, money, fee, held)"), "stock extras sources: the founder's deals in the bank's shares reach the tax return's gains by one path");
        Check(ModSource(@"Tech\TechDirector.Exits.cs").Contains("long mrr = ListMonthOf(firm);") && ModSource(@"Tech\TechDirector.Exits.cs").Contains("long value = ListValueOf(firm);")
              && ModSource(@"Ui\Tech\TechLive.Exits.cs").Contains("_d.ListMonthOf(firm)"), "stock extras sources: the firm's page and the bankers count a firm's real takings for its way to the market");
        string rivals = ModSource(@"Rivals\RivalDirector.cs"), page = ModSource(@"Rivals\RivalPage.cs");
        Check(rivals.Contains("case RivalMoveKind.Shares:") && rivals.Contains("RivalBridges.Shares(m.Tycoon, m.Amount)") && Regex.Matches(page, @"RivalBridges\.MarketLine\(").Count == 2,
            "stock extras sources: a tycoon's announced purchase of shares is a real block, and what they hold shows on their panel and the rich list");
        Check(ModSource(@"Core\Settings.cs").Contains("public static bool MarketsExtras = true;") && ModSource(@"Core\Settings.cs").Contains("MarketsExtras = Bool(modId, MarketsExtrasId, MarketsExtras);")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("AddToggle(Settings.MarketsExtrasId, \"reallife_stx_opt\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"stockextras\").For(\"markets_extras\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"markets_extras\"") && ModSource(@"Meta\FeatureList.Economy.cs").Contains("Markets.ExtrasGuide.Register();"),
            "stock extras sources: one option, on by default, on the options page, in the options' search, the cities' table and the feature list");
        string wiring = ModSource(@"Wiring\Wiring.cs");
        Check(wiring.Contains("StockExtrasLinks.Connect") && wiring.Contains("StockExtrasLinks.Disconnect"), "stock extras sources: the bridges are connected and taken off with the rest");

        // The pages: each sheet is built, and each button reaches its call.
        string build = ModSource(@"Ui\Stocks\StocksBuild.Extras.cs"), own = ModSource(@"Ui\Stocks\StocksBuild.Own.cs"), screen = ModSource(@"Ui\Stocks\StocksScreen.Extras.cs"),
            sheet = ModSource(@"Ui\Stocks\StocksScreen.Sheet.cs");
        string[][] sheets =
        {
            new[] { "Fund", "FundSheet(d, st, w)", "DealFund()", "_market.FundTrade(" }, new[] { "Block", "BlockSheet(d, st, w, p.Companies)", "DealBlock()", "_market.Block(" },
            new[] { "Split", "SplitSheet(d, st, w)", "AskSplit()", "_market.AskSplit(" }, new[] { "BankList", "BankSheet(d, st, w)", "BankStep()", "_market.BankEngage(" }
        };
        foreach (string[] s in sheets)
        {
            Check(own.Contains("case StocksSheets." + s[0] + ": return " + s[1]) && sheet.Contains("case StocksSheets." + s[0] + ": done = " + s[2]) && screen.Contains(s[3])
                  && StocksSheets.Actions.Any(a => a.EndsWith(":" + s[0], StringComparison.Ordinal)), "stock extras pages: the " + s[0] + " sheet is built, its button reaches the market, and it is among the desk's actions");
        }
        Check(screen.Contains("_market.BankWithdraw()") && sheet.Contains("OpenSheet(StocksSheets.Fund, 0)") && sheet.Contains("OpenSheet(StocksSheets.Block, _stock)")
              && sheet.Contains("code == StocksBuild.CodeSplit ? StocksSheets.Split : StocksSheets.BankList"), "stock extras pages: the Portfolio's and the Listing page's buttons open their sheets");
        Check(StocksSheets.Fund == 16 && StocksSheets.Block == 17 && StocksSheets.Split == 18 && StocksSheets.BankList == 19 && StocksSheets.Ticket == 15, "stock extras pages: the sheets have numbers of their own after every other");
        string defs = ModSource(@"Ui\UiScreens.Stocks.cs");
        var missing = new List<string>();
        foreach (Match match in Regex.Matches(build, "w\\(\"(reallife_[a-z_0-9]+)\""))
        {
            if (!defs.Contains("\"" + match.Groups[1].Value + "\""))
            {
                missing.Add(match.Groups[1].Value);
            }
        }
        Check(missing.Count == 0 && Regex.Matches(build, "w\\(\"(reallife_stx_[a-z_0-9]+)\"").Count >= 40, "stock extras pages: every word of the sheets is among the Stocks screen's labels" + (missing.Count > 0 ? ": missing "
            + string.Join(", ", missing.Distinct().Take(6)) : string.Empty));
        foreach (string state in UiSamples.StockExtrasStates)
        {
            Check(UiSamples.StockStates.Contains(state), "stock extras pages: the sample " + state + " is among the Stocks screen's samples, so the previews and the screen's checks draw it");
        }
        Check(UiSamples.StockExtrasStates.Length == 4, "stock extras pages: a sample for each of the four sheets");
        Check(!build.Contains("UiDraw.") && !screen.Contains("UiDraw."), "stock extras pages: the sheets are the desk's own sheet, with nothing drawn of their own (no bar down an edge)");
    }

    // ---------------------------------------------------------------- the texts

    private static void StockxTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources =
        {
            @"Markets\Extras\ExtrasText.cs", @"Markets\Extras\ExtrasCatalogue.cs", @"Markets\Extras\ExtrasGuide.cs", @"Markets\Extras\MarketDirector.Extras.cs", @"Markets\Extras\MarketDirector.ExtrasBank.cs",
            @"Markets\Extras\MarketDirector.ExtrasTests.cs", @"Ui\Stocks\StocksBuild.Extras.cs", @"Ui\Stocks\StocksScreen.Extras.cs"
        };
        foreach (string file in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(stx|meta_title_markets_extras|guide_markets_extras)[a-z_0-9]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_stx_opt", "reallife_mkt_news_reverse", "reallife_mkt_news_block", "reallife_mkt_news_block_sold", "reallife_mkt_news_block_rival", "reallife_mkt_news_block_rival_sold" })
        {
            keys.Add(key);
        }
        Check(keys.Count >= 120, "stock extras texts: its keys are found: " + keys.Count);
        string[] facts = { "{stx_split_from}", "{stx_split_to}", "{stx_notice}", "{stx_reverse_days}", "{stx_fund_fee}", "{stx_fund_most}", "{stx_disclose}", "{stx_block_days}", "{stx_bank_quarters}", "{stx_bank_book}" };
        string[] bold = { "reallife_mkt_app", "reallife_stk_nav_listing", "reallife_mkt_tab_portfolio", "reallife_stx_split", "reallife_stx_fund", "reallife_stx_block", "reallife_stx_bank" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in GivingLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "stock extras texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "stock extras texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains('！') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "stock extras texts: " + key
                    + " has no exclamation mark or dash in " + code);
                bool notice = key.Contains("_note") || key.Contains("_why_") || key.Contains("_test_");
                Check(!notice || value.Length <= 240, "stock extras texts: " + key + " is at most 240 characters in " + code + " (" + value.Length + ")");
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "stock extras texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "stock extras texts: " + key
                        + " keeps its tags and lines in " + code);
                    Check(code == "en" || en.Length < 12 || value != en || key == "reallife_stx_range" || key == "reallife_stx_ratio", "stock extras texts: " + key + " is translated in " + code);
                }
            }
            if (words.TryGetValue("reallife_guide_markets_extras", out string guidePage))
            {
                foreach (string fact in facts)
                {
                    Check(guidePage.Contains(fact), "stock extras texts: the Guide page has " + fact + " in " + code);
                }
                foreach (Match name in Regex.Matches(guidePage, "<b>(.*?)</b>"))
                {
                    Check(bold.Any(k => words.TryGetValue(k, out string named) && named == name.Groups[1].Value), "stock extras texts: the Guide's bold name " + name.Groups[1].Value + " is a text of the mod in " + code);
                }
            }
        }
        string guide = ModSource(@"Markets\Extras\ExtrasGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "stock extras texts: the Guide's " + fact + " is filled from the rules");
        }
        Check(ExtrasKeys.SplitWhyKeys.Length == (int)SplitWhy.Count && ExtrasKeys.FundWhyKeys.Length == (int)FundWhy.Count && ExtrasKeys.BlockWhyKeys.Length == (int)BlockWhy.Count
              && ExtrasKeys.BankWhyKeys.Length == (int)BankWhy.Count, "stock extras texts: every reason something cannot be done has a sentence");
        foreach (string key in ExtrasKeys.SplitWhyKeys.Concat(ExtrasKeys.FundWhyKeys).Concat(ExtrasKeys.BlockWhyKeys).Concat(ExtrasKeys.BankWhyKeys))
        {
            Check(english != null && english.ContainsKey(key), "stock extras texts: the reason " + key + " is in en.json");
        }
        // What's new: the feature's line, found by its English words in whichever row holds it, is in that row of every language, translated.
        const string Sentence = "The stock market gains extras";
        string row = english != null ? english.Where(p => p.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Key.EndsWith("_title", StringComparison.Ordinal) && p.Value.Contains(Sentence))
            .Select(p => p.Key).FirstOrDefault() : null;
        Check(row != null, "stock extras texts: What's new has the feature's line in English");
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int i = 0; row != null && i < GivingLanguages.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, GivingLanguages[i] + ".json"));
            string next = words != null && words.TryGetValue(row, out string value) ? value : string.Empty;
            int mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            if (i == 0)
            {
                Check(next.IndexOf(Sentence, StringComparison.Ordinal) >= 0 && next.IndexOf(Sentence, StringComparison.Ordinal) < (mark < 0 ? int.MaxValue : mark), "stock extras texts: What's new has the line before the first fix mark in en");
            }
            else
            {
                Check(next.Length > 40 && !next.Contains(Sentence) && next.Contains("City Index Fund"), "stock extras texts: What's new has the line, translated, in " + GivingLanguages[i]);
            }
        }
    }

    // ---------------------------------------------------------------- the cost

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long StockxFades(ExtrasState x, int count)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < count; i++)
        {
            x.Fade(0.0000001);
            x.PushOf(31 + (i & 7));
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void StockxCostChecks()
    {
        var x = new ExtrasState();
        for (int i = 0; i < 8; i++)
        {
            x.Push(31 + i, 0.01);
        }
        StockxFades(x, 1000);
        long least = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            least = Math.Min(least, StockxFades(x, 100000));
        }
        Check(least == 0, "stock extras cost: a step's fading of the player's own push and a look at it allocate nothing (" + least + " bytes for 100,000)");
        x.Fade(1.0);
        Check(x.Pushes.Count == 0, "stock extras cost: a push faded to nothing is forgotten, so an idle market fades an empty list");
        MarketEngine m = StockxMarket(1401, 2000000000, out MarketWorld world, 20);
        FundRules.Quote(m.State, 1000, true, m.State.Phase, 5, long.MaxValue, 0, true, true, false);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            FundRules.Quote(m.State, 1000 + i, true, m.State.Phase, 5, long.MaxValue, 0, true, true, false);
        }
        double each = watch.Elapsed.TotalMilliseconds / 200.0;
        Check(each < 2.0, "stock extras cost: a quote of the fund over every listed company, on a press, takes " + each.ToString("0.000") + " ms");
        watch.Restart();
        for (int i = 0; i < 200; i++)
        {
            BlockRules.Quote(m.State, StockxCity(m, 0), true, 100000 + i, long.MaxValue, 5, m.State.Phase, true, true, false);
        }
        Check(watch.Elapsed.TotalMilliseconds / 200.0 < 0.5, "stock extras cost: a block's quote, on a press, takes " + (watch.Elapsed.TotalMilliseconds / 200.0).ToString("0.000") + " ms");
    }
}
