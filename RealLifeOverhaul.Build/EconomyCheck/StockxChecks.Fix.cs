using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The stock market's extras, the faults a review of the logic found (11 Nov 2026), each driven through the rules the shipped director and engine call: a listed bank's owner is never
// paid the whole of a payout, the capital a bank's worth rests on is what it held throughout, the offer's money stays in the bank and the stock never rests on more than three times it,
// a forward split divides the year's dividend, the bank's days are kept over the whole look back, a consolidation divides what is left of an order, a studio's one good month is not
// a firm, the rivals play every day passed, the old rule's split waits for nothing, the fund is owed a dividend before the company leaves, a holding built by orders is told,
// and the exact numbers of the news and the tape.
internal static partial class Program
{
    private static void StockxFixChecks()
    {
        StockxOwnerPartChecks();
        StockxCapitalChecks();
        StockxPublicChecks();
        StockxYearDividendChecks();
        StockxBankDaysChecks();
        StockxPartFilledChecks();
        StockxSteadyChecks();
        StockxRivalDaysChecks();
        StockxFundDueChecks();
        StockxDisclosureChecks();
        StockxWaitSaveChecks();
        StockxRegulatorChecks();
        StockxPageCallChecks();
        StockxPennyChecks();
    }

    private static BankFacts StockxFacts(long capitalCents = 500000000)
    {
        return new BankFacts { Running = true, Clear = true, FoundedDay = 1, CapitalCents = capitalCents, Name = "Harbour Bank" };
    }

    // The bank's days, each with the capital it held that evening.
    private static void StockxFill(BankFloat b, int days, long income, long profit, long capital)
    {
        for (int d = 1; d <= days; d++)
        {
            BankFloatRules.AddDay(b, d, income, profit, 60, capital);
        }
    }

    // A market whose player's bank is listed as the director leaves it: the prospectus written, the book priced, the stock on the market, the stake seen.
    private static Stock StockxListedBank(ulong seed, out MarketEngine m, out MarketWorld world, out BankFloat b)
    {
        m = StockxMarket(seed, 500000000, out world, 20);
        MarketState st = m.State;
        st.Me = StockxMe;
        b = st.Extras.Bank;
        StockxFill(b, 300, 4000000, 900000, 500000000);
        BankFacts facts = StockxFacts();
        BankFloatRules.Prospectus(b, facts, 2000, StockxMe, 301, 60, MarketEnv.Quiet());
        b.PriceCents = b.LowCents + ListingCatalogue.PriceStepCents;
        b.Raised = BankFloatRules.Raised(b, b.PriceCents, 1.0, out long _, out long _);
        Stock k = m.ListFirm(BankFloatRules.Listing(b, "Harbour Bank", st.Day, 60), out FirmWhy _);
        b.Stage = BankStage.Listed;
        b.Founder = b.OldShares;
        b.Shares = b.OldShares + b.NewShares;
        return k;
    }

    // ---------------------------------------------------------------- fault 1: a listed bank's owner never takes everything

    private static void StockxOwnerPartChecks()
    {
        Stock k = StockxListedBank(1401, out MarketEngine m, out MarketWorld _, out BankFloat b);
        MarketState st = m.State;
        long founder = k.Own.Founder, shares = k.Shares, sum = 1000000;
        long expected = BankFloatRules.OwnerPart(sum, founder, shares);
        Check(k != null && expected < sum && expected == sum * founder / shares && BankFloatRules.Payout(st, sum, true) == expected && b.PaidOthers == sum - expected,
            "stock extras fix: while the bank trades its owner has their shares' part of a payout (" + expected + " of " + sum + "), and the rest is counted as the other holders'");
        b.PaidOthers = 0;

        // A guest in another player's city: the bank's stock is set aside, the bank goes on booking, and the owner still has only their part of it.
        MarketEngine host = StockxMarket(1402, 1000000, out MarketWorld _, 20);
        string sent = MarketShare.Write(host.State, host.Tape);
        MarketState adopted = MarketShare.Adopt(st, sent, new List<long>(), null, out MarketTape _, out long _);
        bool aside = adopted != null && adopted.Stocks.All(s => !(s.Own != null && s.Own.Firm && BankFloatRules.IsBank(s.Own.Place) && s.Listed)) && adopted.Listing.SuspendedFirms.Count == 1;
        Check(aside, "stock extras fix: as a guest in another player's city the bank's stock is set aside, not on the market");
        if (adopted != null)
        {
            long guestPart = BankFloatRules.Payout(adopted, sum, false);
            Check(aside && guestPart == expected && guestPart < sum, "stock extras fix: ...and the owner's part of a payout is still their shares' (" + guestPart + "), never the whole (" + sum + ")");
            adopted.Extras.Bank.Founder = adopted.Extras.Bank.Shares = 0;   // a stake never seen: the offer's own shares stand
            long offered = BankFloatRules.Payout(adopted, sum, false);
            Check(aside && offered == BankFloatRules.OwnerPart(sum, b.OldShares, b.OldShares + b.NewShares) && offered < sum, "stock extras fix: ...and before any stake was seen, the offer's own shares stand (" + offered + ")");
            adopted.Extras.Bank.Stage = BankStage.Ended;
            Check(BankFloatRules.Payout(adopted, sum, false) == sum, "stock extras fix: a bank whose stock is gone pays its owner all of it");
        }

        // The market switched off: the save's own lines hold the stake.
        var data = new Dictionary<string, string>();
        MarketStore.Save(data, st);
        ExtrasStore.Save(data, st);
        MarketState kept = MarketStore.Load(data, out int _);
        ExtrasStore.Load(data, kept);
        bool read = BankFloatRules.Stake(kept, out long keptFounder, out long keptShares);
        Check(read && keptFounder == founder && keptShares == shares && kept.Extras.Bank.Founder == founder && kept.Extras.Bank.Shares == shares && kept.Extras.Bank.HeldCents == b.HeldCents,
            "stock extras fix: with the market off, the stake is read from the save's lines (" + keptFounder + " of " + keptShares + ")");
        st.Extras.Bank.Stage = BankStage.None;
        Check(!BankFloatRules.Stake(st, out long _, out long _) && BankFloatRules.Payout(st, sum, true) == sum, "stock extras fix: a bank that is not listed is its owner's alone");
    }

    // ---------------------------------------------------------------- fault 2: temporary capital counts for nothing

    private static void StockxCapitalChecks()
    {
        MarketEnv env = MarketEnv.Quiet();
        var b = new BankFloat();
        StockxFill(b, 300, 4000000, 900000, 500000000);   // 5,000,000 of its own capital through the year
        BankFacts honest = StockxFacts(500000000);
        double worth = BankFloatRules.Worth(b, honest, StockxMe, 301, 60, env);
        // Borrowed money put in for the day, advisers engaged on it, the money taken out again.
        BankFacts inflated = StockxFacts(5000000000);
        Check(BankFloatRules.Held(b, inflated, 301, 60) == 500000000 && Math.Abs(BankFloatRules.Worth(b, inflated, StockxMe, 301, 60, env) - worth) < 1e-6,
            "stock extras fix: capital put in today counts for nothing: the bank is worth the same with ten times the capital for a day (" + (long)worth + ")");
        var thin = new BankFloat();
        StockxFill(thin, 300, 4000000, 900000, 500000000);
        BankFloatRules.AddDay(thin, 301, 4000000, 900000, 60, 20000000);   // one evening it held 200,000 only, inside the last year
        Check(BankFloatRules.Held(thin, honest, 302, 60) == 20000000 && BankFloatRules.Worth(thin, honest, StockxMe, 302, 60, env) <= 20000000 / 100.0 * ExtrasCatalogue.BankBookMost + 1e-6,
            "stock extras fix: the capital that counts is the least the bank held over the last year (200,000), and the worth is capped by it");
        BankFloat old = new BankFloat();
        StockxFill(old, 300, 4000000, 900000, 100);
        BankFloatRules.AddDay(old, 301, 4000000, 900000, 60, 500000000);
        Check(BankFloatRules.Held(old, honest, 400, 60) == 500000000 || BankFloatRules.Held(old, honest, 400, 60) == 500000000L,
            "stock extras fix: a low evening more than a year ago no longer weighs on the capital that counts");
        var unknown = new BankFloat();
        for (int d = 1; d <= 300; d++)
        {
            BankFloatRules.AddDay(unknown, d, 4000000, 900000, 60);   // days from a save before the capital was noted
        }
        Check(BankFloatRules.Held(unknown, honest, 301, 60) == 500000000, "stock extras fix: days without a capital noted are not counted (a save from before)");

        // Read again at the pricing: capital taken out since the advisers were engaged cuts the price, or calls the offer off.
        BankFloatRules.Prospectus(b, honest, 2000, StockxMe, 301, 60, env);
        int unconstrained = 0, cut = 0, off = 0, full = 0, bound = 0;
        for (ulong secret = 1; secret <= 200; secret++)
        {
            int open = BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, 500000000L * 100);
            int same = BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, 500000000);
            int half = BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, 250000000);
            int none = BankFloatRules.Price(b, "Harbour Bank", secret * 7919, 1500, 0, 50, 0);
            unconstrained += open > 0 ? 1 : 0;
            long most = BankFloatRules.StockCapCents(250000000) / b.OldShares;
            if (open > 0)
            {
                full += same > 0 ? 1 : 0;
                cut += half > 0 && half <= most && half <= open ? 1 : 0;
                off += half == 0 || half < b.LowCents ? 1 : 0;
                bound += none == 0 ? 1 : 0;
            }
        }
        Check(unconstrained > 0 && bound == unconstrained && cut + off == unconstrained,
            "stock extras fix: a book priced on a day the bank holds half the capital is cut to three times it a founder's share, or called off under the range's bottom (" + cut + " cut, " + off + " called off of "
            + unconstrained + "); with none it is called off in all");
        Check(BankFloatRules.StockCapCents(1000) == 3000 && BankFloatRules.StockCapCents(-5) == 0, "stock extras fix: the stock's most is three times the capital that counts");
    }

    // ---------------------------------------------------------------- fault 3: the offer's money stays, the stock never rests on more than three times the capital

    private static void StockxPublicChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        OwnBank ob = NewBank(c);
        RunBank(ob, c, 2, BankYear, null, day);
        long excess = OwnBankRules.CapitalNow(ob) - OwnBankRules.Keep(ob, c.Level) - OwnBankRules.PayAhead(ob), capital = ob.Capital, reserves = ob.Reserves;
        Check(OwnBankRules.PublicIn(ob, 500000000L) && ob.Public == 500000000L && ob.Capital == capital + 500000000L && ob.Reserves == reserves + 500000000L && OwnBankRules.Balanced(ob),
            "stock extras fix: the offer's money goes into the bank's capital and its cash, and is marked as the public's");
        long most = OwnBankRules.MostOut(ob, c.Level);
        Check(excess == OwnBankRules.CapitalNow(ob) - OwnBankRules.Keep(ob, c.Level) - OwnBankRules.PayAhead(ob) && most <= Math.Max(0, excess)
              && !OwnBankRules.TakeOut(ob, Math.Max(0, excess) + OwnBankCatalogue.CapitalStepCents, c.Level),
            "stock extras fix: while the stock trades no dividend takes the public's money: the capital over what the bank keeps is " + excess + " before and after the offer, and at most " + most + " may be taken out");
        OwnBank plain = NewBank(c);
        RunBank(plain, c, 2, BankYear, null, day);
        OwnBankRules.PutIn(plain, 500000000L);
        Check(OwnBankRules.CapitalNow(plain) - OwnBankRules.Keep(plain, c.Level) - OwnBankRules.PayAhead(plain) == excess + 500000000L && OwnBankRules.MostOut(plain, c.Level) > most,
            "stock extras fix: the same money put in by its owner can be taken out again, the public's cannot");
        ob.PayMode = BankTiers.PayFixedMode;
        ob.PayFixed = BankTiers.PayFixedMostCents;
        plain.PayMode = BankTiers.PayFixedMode;
        plain.PayFixed = BankTiers.PayFixedMostCents;
        long asked = OwnBankRules.OwnerPay(ob, 0, c.Level), free = OwnBankRules.OwnerPay(plain, 0, c.Level);
        Check(asked <= OwnBankRules.CapitalNow(ob) - OwnBankRules.Keep(ob, c.Level) && asked <= free && OwnBankRules.Keep(ob, c.Level) >= OwnBankRules.Keep(plain, c.Level) + 500000000L,
            "stock extras fix: the owner's pay and the automatic dividends are held to what is over the public's money too (" + asked + " against " + free + ")");
        ob.Public = 0;   // the stock has left the market
        ob.PayMode = plain.PayMode = BankTiers.PayShareMode;
        Check(OwnBankRules.MostOut(ob, c.Level) == OwnBankRules.MostOut(plain, c.Level) && OwnBankRules.MostOut(ob, c.Level) > most,
            "stock extras fix: once the stock has left the market the money is the bank's to pay out like the rest");
        Check(!OwnBankRules.PublicIn(new OwnBank(), 100) && !OwnBankRules.PublicIn(null, 100) && !OwnBankRules.PublicIn(ob, 0), "stock extras fix: no money is put in for a bank that does not run, or for nothing");
        ob.Public = 123456789;
        OwnBank back = OwnBankStore.Read(OwnBankStore.Write(ob), out int _);
        Check(back != null && back.Public == 123456789, "stock extras fix: the money kept for the public is in the bank's save");
        // The owner's share of a sum the bank pays: all of it with no one else holding, their part otherwise, never more than the sum nor under nothing.
        Check(OwnBankRules.OwnerShare(1000, null) == 1000 && OwnBankRules.OwnerShare(1000, x => x * 3 / 10) == 300 && OwnBankRules.OwnerShare(1000, x => x * 2) == 1000 && OwnBankRules.OwnerShare(1000, x => -5) == 0
              && OwnBankRules.OwnerShare(0, x => 1) == 0,
            "stock extras fix: what the bank pays its owner is the owner's part of it, never more than the sum and never under nothing");

        // The stock: worth no more than three times the capital that counts at any open, set for every machine in the stock itself.
        Stock k = StockxListedBank(1411, out MarketEngine m, out MarketWorld _, out BankFloat bank);
        Stock free2 = StockxListedBank(1411, out MarketEngine m2, out MarketWorld _, out BankFloat _);
        long cap = BankFloatRules.StockCapCents(300000000);   // 3,000,000 of capital that counts: the stock may be worth 9,000,000
        Check(m.SetBankCap(k.Id, cap) && !m.SetBankCap(k.Id, cap) && k.Own.CapCents == cap && !m.SetBankCap(k.Id + 1, cap), "stock extras fix: the cap is set once on the bank's stock and on nothing else");
        StockxPrice(k, 40.0);
        StockxPrice(free2, 40.0);
        int next = MarketTrading.NextTradingDay(m.State.Day);
        RunTo(m, next, MarketCatalogue.PhaseOpen + 1);
        RunTo(m2, next, MarketCatalogue.PhaseOpen + 1);
        double value = k.Shares * k.Mid, uncapped = free2.Shares * free2.Mid;
        Check(value <= cap / 100.0 * (1 + 1e-6) && value > cap / 100.0 * 0.99 && uncapped > cap / 100.0 * 1.5,
            "stock extras fix: a stock priced far over its cap is brought back to three times the capital that counts at the next open (" + (long)value + " against " + (long)(cap / 100) + "), where without it it stays ("
            + (long)uncapped + ")");
        double lower = k.Mid;
        StockxPrice(k, 0.5);
        RunTo(m, MarketTrading.NextTradingDay(next), MarketCatalogue.PhaseOpen + 1);
        Check(k.Mid < lower && k.Shares * k.Mid < cap / 100.0, "stock extras fix: a cap is a ceiling: a price under it is left where the market has it");
        m.State.Following = true;
        StockxPrice(k, 40.0);
        RunTo(m, MarketTrading.NextTradingDay(MarketTrading.NextTradingDay(next)), MarketCatalogue.PhaseOpen + 1);
        Check(k.Shares * k.Mid > cap / 100.0, "stock extras fix: a market that follows another player's is not changed by the player's own bank");
        m.State.Following = false;
        MarketState readBack = MarketStore.Read(MarketStore.Write(m.State), out int _);
        Stock again = readBack != null ? readBack.Stock(k.Id) : null;
        Check(again != null && again.Own != null && again.Own.CapCents == cap, "stock extras fix: the cap is in the market's line, so a guest or a load plays the same");
    }

    // ---------------------------------------------------------------- fault 4: a forward split divides the year's dividend

    private static void StockxYearDividendChecks()
    {
        var world = new OwnWorld(1421);
        MarketEngine m = LListed(1421, world, 2500, out Stock own);
        Check(own != null && own.Own != null, "stock extras fix: the player's company is listed for the year's meeting");
        if (own == null)
        {
            return;
        }
        OwnListing o = own.Own;
        own.DeclaredCents = 0;
        StockxPrice(own, 40.0);
        o.YearCents = own.Last;
        o.YearDividendCents = 40;
        o.YearIndex = 1000;
        o.Rps = 0.4;
        int index = 1300;
        HolderMood before = ListingVotes.Read(own, index);
        StockxSplit(m, own, 2);
        HolderMood after = ListingVotes.Read(own, index);
        Check(o.YearDividendCents == 20 && Math.Abs(before.Lag - after.Lag) < 1e-9 && Math.Abs(before.Hoard - after.Hoard) < 1e-9,
            "stock extras fix: a two for one split halves the year's dividend a share, so the year meeting reads the same return (" + before.Lag.ToString("0.0000") + " before, " + after.Lag.ToString("0.0000") + " after)");
        StockxSplit(m, own, -2);
        Check(o.YearDividendCents == 40, "stock extras fix: and a consolidation brings it back");
    }

    // ---------------------------------------------------------------- fault 5: the bank's income is in one stock only

    private static void StockxBankDaysChecks()
    {
        int look = ExtrasCatalogue.BankLookBackYears * 60;
        var b = new BankFloat();
        for (int d = 1; d <= 400; d++)
        {
            BankFloatRules.AddDay(b, d, 100 * d, 10 * d, 60, 500000000);
        }
        BankFloatRules.Sum(b, 400 - look, 401, out long revenue, out long profit);
        long expectRevenue = 0, expectProfit = 0;
        for (int d = 400 - look; d <= 400; d++)
        {
            expectRevenue += 100 * d;
            expectProfit += 10 * d;
        }
        Check(revenue == expectRevenue / 100 && profit == expectProfit / 100,
            "stock extras fix: the bank's days are kept over the whole look back of a company's listing (" + look + " days), so all of it is taken out of the company's books while the bank trades");
        Check(ListingCatalogue.BooksLookBackYears == ExtrasCatalogue.BankLookBackYears && b.Days.Count <= ExtrasCatalogue.BankDaysKept && b.Days.Count >= look,
            "stock extras fix: the look back is the company's own (" + ListingCatalogue.BooksLookBackYears + " game years), and at most " + ExtrasCatalogue.BankDaysKept + " days are saved");
        var x = new ExtrasState();
        for (int d = 1; d <= 400; d++)
        {
            BankFloatRules.AddDay(x.Bank, d, 100 * d, 10 * d, 60, 7000 + d);
        }
        ExtrasState back = ExtrasStore.Read(ExtrasStore.Write(x), out int _);
        Check(back != null && back.Bank.Days.Count == x.Bank.Days.Count && back.Bank.Days[0].Capital == x.Bank.Days[0].Capital && back.Bank.Days[back.Bank.Days.Count - 1].Capital == 7400,
            "stock extras fix: the capital of each day is in the save (" + (back != null ? back.Bank.Days.Count : 0) + " days)");
    }

    // ---------------------------------------------------------------- fault 6: a consolidation divides what is left of an order

    private static void StockxPartFilledChecks()
    {
        MarketEngine m = StockxMarket(1431, 50000000, out MarketWorld _, 30);
        Stock s = StockxCity(m, 1);
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        Order a = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, 100, Math.Max(1, q.Bid / 2), 0);
        Order c = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, 100, Math.Max(1, q.Bid / 2), 0);
        Order d = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, 105, Math.Max(1, q.Bid / 2), 0);
        a.Filled = 5;     // 95 left: nine whole new shares, and the five filled were part of the holding that was divided
        c.Filled = 95;    // 5 left: nothing whole
        d.Filled = 25;    // 80 left: eight
        StockxSplit(m, s, -10);
        Check(a.Left == 9 && a.Qty == 9 && a.Filled == 0, "stock extras fix: an order of 100 with 5 filled is left with 9 to fill after ten into one, not 10 (" + a.Left + " left of " + a.Qty + ")");
        Check(d.Left == 8 && d.Filled == 2, "stock extras fix: 105 with 25 filled is left with 8 (" + d.Left + ")");
        Check(c.State == OrderState.Cancelled && c.Why == OrderWhy.Cancelled, "stock extras fix: an order with nothing whole left is cancelled (" + c.State + ")");
        Check(a.IsOpen && d.IsOpen, "stock extras fix: the others stay open");
    }

    // ---------------------------------------------------------------- fault 7: one good month is not a firm

    private static void StockxSteadyChecks()
    {
        long bar = ExitCatalogue.ListMrrLeast;   // a Series B's month, cents
        var launch = new Firm();
        for (int day = 1; day <= 200; day++)
        {
            FirmsRules.Roll(launch, day, day == 190 ? bar * 3 : bar / 120);   // a quarter of the bar a month, and one launch day of three bars
        }
        Check(launch.Revenue30 >= bar && FirmsRules.Steady(launch) < bar / 2,
            "stock extras fix: a launch day of three bars lifts the last month to " + launch.Revenue30 + " (the bar is " + bar + ") but the steady month stays at " + FirmsRules.Steady(launch));
        var exit = new FirmExit();
        Check(!ExitRules.ListReady(ExitRules.ListMonth(0, FirmsRules.Steady(launch)), exit) && ExitRules.ListReady(ExitRules.ListMonth(0, launch.Revenue30), exit),
            "stock extras fix: so one good month no longer opens the way to the market (the gate on the old month was open)");
        long oneMonth = FirmsRules.Value(launch, 0, 0), steady = FirmsRules.Value(launch, 0, 0, FirmsRules.Steady(launch));
        Check(steady * 4 < oneMonth && steady == FirmsRules.Steady(launch) * 3,
            "stock extras fix: nor is the firm priced at three times that month (" + oneMonth + " on the month, " + steady + " on the steady one)");
        var sound = new Firm();
        for (int day = 1; day <= 200; day++)
        {
            FirmsRules.Roll(sound, day, bar / 25);   // 1.2 bars a month, every month
        }
        Check(FirmsRules.Steady(sound) >= bar && ExitRules.ListReady(ExitRules.ListMonth(0, FirmsRules.Steady(sound)), exit),
            "stock extras fix: a firm that takes the bar two months running may start the way (" + FirmsRules.Steady(sound) + ")");
        var young = new Firm();
        for (int day = 1; day <= 50; day++)
        {
            FirmsRules.Roll(young, day, bar / 10);
        }
        Check(young.Revenue30 >= bar && FirmsRules.Steady(young) == 0, "stock extras fix: a firm with less than two months behind it has no steady month");
        // The player's own other businesses as the customer.
        var selfDealt = new Firm();
        for (int day = 1; day <= 200; day++)
        {
            FirmsRules.Roll(selfDealt, day, bar / 10, bar / 10);
        }
        Check(selfDealt.Revenue30 >= bar && selfDealt.Self30 == selfDealt.Revenue30 && FirmsRules.Steady(selfDealt) == 0,
            "stock extras fix: takings the player's own other businesses paid the firm count for nothing (" + selfDealt.Revenue30 + " taken, " + selfDealt.Self30 + " of it from their own businesses)");
        var mixed = new Firm();
        for (int day = 1; day <= 200; day++)
        {
            FirmsRules.Roll(mixed, day, bar / 20, day % 2 == 0 ? bar / 20 : 0);
        }
        Check(Math.Abs(FirmsRules.Steady(mixed) * 2 - mixed.Revenue30) <= bar / 10 && FirmsRules.Steady(mixed) < mixed.Revenue30,
            "stock extras fix: half from their own businesses, half of the month counts (" + FirmsRules.Steady(mixed) + " of " + mixed.Revenue30 + ")");
        FirmsRules.Roll(mixed, 201, 100, 5000);   // a part that is more than the day's takings is the day's takings at most
        Check(mixed.SelfDays[201 % FirmsCatalogue.MonthDays] == 100, "stock extras fix: no more than a day's takings can have come from the player's own businesses");
        var file = new FirmsFile();
        file.Firms.Add(selfDealt);
        selfDealt.Place = "p1";
        FirmsFile read = FirmsStore.Read(FirmsStore.Write(file), out int _);
        Firm back = read != null && read.Firms.Count == 1 ? read.Firms[0] : null;
        Check(back != null && back.Self30 == selfDealt.Self30 && back.SelfPrev30 == selfDealt.SelfPrev30 && back.SelfDays.SequenceEqual(selfDealt.SelfDays) && FirmsRules.Steady(back) == FirmsRules.Steady(selfDealt),
            "stock extras fix: what the player's own businesses paid is in the firm's save");
    }

    // ---------------------------------------------------------------- fault 8: every day passed is played

    private static void StockxRivalDaysChecks()
    {
        for (int t = 0; t < 3; t++)
        {
            int offset = (t * 11 + 5) % ExtrasCatalogue.RivalEveryDays;
            var book = new RivalBook();
            var seen = new List<int>();
            int day = 0;
            var steps = new[] { 1, 1, 30, 75, 2, 1, 61, 13, 29, 90, 1, 1, 1, 200, 3 };
            foreach (int step in steps)
            {
                day += step;
                int looks = 0;
                for (int due = RivalMarket.NextLook(t, day, book); due > 0; due = RivalMarket.NextLook(t, day, book))
                {
                    seen.Add(due);
                    book.LookDay = due;
                    looks++;
                }
                Check(looks <= step / ExtrasCatalogue.RivalEveryDays + 1, "stock extras fix: tycoon " + t + " made " + looks + " look(s) over " + step + " day(s) passed");
            }
            var every = new List<int>();
            for (int d = Math.Max(1, seen[0]); d <= day; d++)
            {
                if (d % ExtrasCatalogue.RivalEveryDays == offset)
                {
                    every.Add(d);
                }
            }
            Check(seen.SequenceEqual(every) && seen.Count > 10, "stock extras fix: tycoon " + t + ", looked at on 15 irregular days (a load, a long sleep), made every look of their own days: " + seen.Count + " of " + every.Count
                  + ", none skipped, none twice");
            Check(RivalMarket.NextLook(t, day, book) == 0, "stock extras fix: ...and none is left over");
        }
        // A tycoon who has never looked takes the latest of their days within a month, not a hundred.
        var fresh = new RivalBook();
        int first = RivalMarket.NextLook(0, 5000, fresh);
        Check(first > 5000 - ExtrasCatalogue.RivalEveryDays && first <= 5000, "stock extras fix: a tycoon who never looked starts with their latest day (" + first + ")");
    }

    // ---------------------------------------------------------------- fault 10: the fund is owed a dividend before the company leaves

    private static void StockxFundDueChecks()
    {
        long[] dues = new long[2];
        for (int variant = 0; variant < 2; variant++)
        {
            MarketEngine m = StockxMarket(1441, 1000000000, out MarketWorld _, 30);
            MarketState st = m.State;
            m.FundTrade(200000, true);
            Stock gone = StockxCity(m, 3);
            gone.DeclaredCents = 40;
            gone.ExDone = false;
            gone.RecordDay = st.Day;
            gone.PayDay = st.Day + 2;
            gone.BidCents = 0;
            gone.SplitDay = 0;
            gone.Distress = 0.0;
            if (variant == 0)
            {
                StockxPrice(gone, MarketCatalogue.PennyCents / 100.0 * 0.4);
                gone.LowDays = MarketCatalogue.PennyDays - 1;   // the penny rule takes it off the market at this close
            }
            Check(variant == 1 || gone.Last < MarketCatalogue.PennyCents, "stock extras fix: the company is under a unit and one day short of the penny rule's");
            RunTo(m, st.Day, MarketCatalogue.PhaseClose);
            dues[variant] = st.Extras.Dues.Sum(d => d.Cents) + st.Extras.Paid * 100;
            if (variant == 0)
            {
                Check(!gone.Listed && gone.Reason == GoneReason.Delisted, "stock extras fix: it leaves the market at the close of its record day");
            }
            else
            {
                Check(gone.Listed, "stock extras fix: the control company stays");
            }
        }
        Check(dues[0] > 0 && dues[1] > 0 && Math.Abs(dues[0] - dues[1]) * 20 < dues[1] + dues[0],
            "stock extras fix: the fund is owed the dividend of a company that leaves on its record day as of one that stays (" + dues[0] + " against " + dues[1] + " cents, the price of each differs)");
    }

    // ---------------------------------------------------------------- the 5% a holding passes, however it was built

    private static void StockxDisclosureChecks()
    {
        MarketEngine m = StockxMarket(1451, 1000000000, out MarketWorld world, 30);
        Stock s = StockxCity(m, 2);
        s.Shares = 40000;   // few shares: a modest purchase is a large stake
        long fivePercent = s.Shares * ExtrasCatalogue.DiscloseStepBp / 10000L;
        long most = MarketTrading.MostPerOrder(s);
        long qty = Math.Min(fivePercent + 50, most);
        Check(qty > fivePercent, "stock extras fix: an order of the company's shares can pass a whole 5% (" + qty + " of " + s.Shares + ", an order may be " + most + ")");
        StockxOrder(m, s, OrderSide.Buy, OrderType.Market, qty, 0, 0);
        int told = world.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == s.Id && n.B == 1 && n.C == 0 && n.A >= ExtrasCatalogue.DiscloseStepBp);
        Check(told == 1, "stock extras fix: a holding built by an ordinary order that passes 5% is told to the market, once (" + told + ")");
        StockxOrder(m, s, OrderSide.Buy, OrderType.Market, 3, 0, 0);
        Check(world.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == s.Id) == 1, "stock extras fix: a few shares more, on the same step, tell nothing more");
        long held = m.State.Held(s.Id);
        Order sell = StockxOrder(m, s, OrderSide.Sell, OrderType.Market, Math.Min(held, most), 0, 0);
        int down = world.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == s.Id && n.B == -1 && n.C == 0);
        Check(sell != null && m.State.Held(s.Id) < fivePercent && down == 1, "stock extras fix: and the step kept is told again when a sale takes the holding under it (" + down + ")");
        m.ExtrasOn = false;
        StockxOrder(m, s, OrderSide.Buy, OrderType.Market, qty, 0, 0);
        Check(world.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == s.Id) == 2, "stock extras fix: with the option off nothing is told");
    }

    // ---------------------------------------------------------------- the new parts of the line

    private static void StockxWaitSaveChecks()
    {
        var x = new ExtrasState();
        x.WaitSplit(31, 70);
        x.WaitSplit(32, 71);
        x.WaitSplit(31, 72);
        x.Bank.Founder = 800000;
        x.Bank.Shares = 1000000;
        x.Bank.HeldCents = 123456;
        ExtrasState back = ExtrasStore.Read(ExtrasStore.Write(x), out int _);
        Check(back != null && back.SplitWaits.Count == 2 && back.SplitWaiting(31, 72) && !back.SplitWaiting(31, 70) && back.SplitWaiting(32, 71) && back.Bank.Founder == 800000
              && back.Bank.Shares == 1000000 && back.Bank.HeldCents == 123456 && !back.Empty, "stock extras fix: the splits that may wait, and the bank's stake and capital, are in the line");
        back.SplitWaitDone(31);
        Check(!back.SplitWaiting(31, 72) && back.SplitWaiting(32, 71), "stock extras fix: a split done waits no more");
        for (int i = 0; i < 40; i++)
        {
            back.WaitSplit(100 + i, 5);
        }
        Check(back.SplitWaits.Count == ExtrasCatalogue.SplitWaitsKept, "stock extras fix: the list of splits that may wait is kept short");
        // A line from before these parts: read as far as it goes.
        string old = "v1|f:0,0,0,0,0,1,0,0|d:|s:|p:|t:|r:,,,,,,,;;|b:2,5,6,2000,1100,900,1050,800000,200000,950,1800000,25000,1500,76,333|k:69,4000000,900000|q:";
        ExtrasState early = ExtrasStore.Read(old, out int _);
        Check(early != null && early.Bank.Stage == BankStage.Listed && early.Bank.Founder == 0 && early.Bank.Shares == 0 && early.Bank.Days.Count == 1 && early.Bank.Days[0].Capital == -1 && early.SplitWaits.Count == 0,
            "stock extras fix: a line from before them is read: no stake seen (the offer's shares stand), days with no capital noted, no split that waits");
        Check(MarketInputs.EventSplit == MarketInputs.EventBuilder + 1, "stock extras fix: the tape's split event follows the last event exactly (" + MarketInputs.EventSplit + ")");
    }

    // ---------------------------------------------------------------- a bank under the regulator's action

    private static void StockxRegulatorChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        var b = new BankFloat();
        StockxFill(b, 300, 4000000, 900000, 500000000);
        BankFacts clear = StockxFacts();
        BankFacts watched = clear, running = clear, test = clear, gone = clear;
        watched.Clear = false;
        test.Test = true;
        gone.Running = false;
        Check(BankFloatRules.PriceReady(clear) && !BankFloatRules.PriceReady(watched) && !BankFloatRules.PriceReady(test) && !BankFloatRules.PriceReady(gone),
            "stock extras fix: the book is priced only for a bank that runs, is no test's and is clear of the regulator on the day (watched, in breach or in a run it is called off)");
        Check(BankFloatRules.Ready(b, watched, 301, 60, true, false, false) == BankWhy.Regulator, "stock extras fix: and its advisers are not engaged for one");
        // While it trades: no pay, dividend or sale for a bank in breach or in a run, the public's money apart.
        OwnBank ob = NewBank(c);
        RunBank(ob, c, 2, BankYear, null, day);
        OwnBankRules.PublicIn(ob, 500000000L);
        ob.PayMode = BankTiers.PayFixedMode;
        ob.PayFixed = 1000;
        ob.Stage = Regulator.Breach;
        Check(OwnBankRules.MostOut(ob, c.Level) == 0 && OwnBankRules.OwnerPay(ob, 100000, c.Level) == 0 && !OwnBankRules.Sellable(ob), "stock extras fix: a bank in breach pays nothing out and cannot be sold while its stock trades");
        ob.Stage = Regulator.Clear;
        ob.Run = true;
        Check(OwnBankRules.MostOut(ob, c.Level) == 0 && OwnBankRules.OwnerPay(ob, 100000, c.Level) == 0 && !OwnBankRules.Sellable(ob), "stock extras fix: nor a bank in a run");
        ob.Run = false;
        ob.Stage = Regulator.Watch;
        Check(OwnBankRules.MostOut(ob, c.Level) <= OwnBankRules.CapitalNow(ob) - OwnBankRules.Keep(ob, c.Level), "stock extras fix: a bank on watch pays only what is over the public's money and the least it keeps");
    }

    // ---------------------------------------------------------------- the penny rule with the extras on

    // The market's own penny rule leaves a share under a unit for weeks off the market at its last price. With the extras on (the shipped default) a sound company that pays no dividend puts its
    // shares together first; one that pays a dividend, or is near failing, is left to the penny rule, which delists it and pays its holders: tried here with the extras ON.
    private static void StockxPennyChecks()
    {
        for (int variant = 0; variant < 3; variant++)
        {
            var world = new MarketWorld(180 + (ulong)variant);
            world.Purse.Money = 100000000;
            MarketEngine e = NewMarket(180 + (ulong)variant, 60, 1, 0, world);
            Check(e.ExtrasOn, "stock extras fix: the penny rule is tried with the extras on, as shipped (" + (variant == 0 ? "a dividend" : variant == 1 ? "near failing" : "sound, no dividend") + ")");
            SessionDay(e, 2, 10);
            Stock p = Liquid(e, MarketSize.Small);
            Trade(e, p, OrderSide.Buy, 1000);
            double toPenny = Math.Log(0.6 / p.Mid);
            p.Mis += toPenny;
            p.Sent += toPenny;
            p.Last = p.MidCents;
            long shares = p.Shares;
            bool together = false;
            int start = e.State.Day, left = 0;
            for (int d = start; d <= start + 70 && p.Listed; d++)
            {
                if (variant == 0)
                {
                    p.DpsCents = Math.Max(p.DpsCents, 3);   // it pays a dividend still, on a price that has collapsed
                }
                else if (variant == 1)
                {
                    p.Distress = 0.9;
                }
                else
                {
                    p.DpsCents = 0;   // it pays nothing, and is sound
                    p.DeclaredCents = 0;
                    p.Distress = 0.1;
                }
                RunTo(e, d);
                together |= p.SplitRatio < 0 || p.Shares < shares;
                if (p.Listed && p.Last >= MarketCatalogue.PennyCents)
                {
                    p.Mis -= 0.5;   // (kept under a unit, as a company in trouble would be)
                    p.Sent -= 0.5;
                }
                left = d;
            }
            if (variant < 2)
            {
                Check(!p.Listed && p.Reason == GoneReason.Delisted && !together, "stock extras fix: with the extras on a share under a unit that pays a dividend, or is near failing, leaves the market by the penny rule, its shares never put together (day "
                                                                                    + left + ")");
                Check(e.State.Trades.Any(t => t.Kind == TradeKind.Payout && t.StockId == p.Id && t.Value == 1000L * p.GoneCents / 100 && p.GoneCents > 0),
                    "stock extras fix: ...and its holders are paid the last price (" + p.GoneCents + " cents)");
            }
            else
            {
                Check(together && (p.Listed || p.Reason != GoneReason.Delisted || e.State.Extras.Splits.Any(s => s.Stock == p.Id && s.Reverse)),
                    "stock extras fix: a sound company that pays no dividend puts its shares together first, and is not delisted (" + p.Shares + " shares of " + shares + ")");
            }
        }
    }

    // ---------------------------------------------------------------- the four sheets and their buttons

    private static void StockxPageCallChecks()
    {
        string screen = ModSource(@"Ui\Stocks\StocksScreen.Extras.cs"), extras = ModSource(@"Markets\Extras\MarketDirector.Extras.cs"), bank = ModSource(@"Markets\Extras\MarketDirector.ExtrasBank.cs"),
            engine = ModSource(@"Markets\Extras\MarketEngine.Extras.cs");
        string[][] calls =
        {
            new[] { "Fund", "_market.FundTrade(", "_engine.FundTrade(" }, new[] { "Block", "_market.Block(", "_engine.Block(" }, new[] { "Split", "_market.AskSplit(", "_engine.AskSplit(" },
            new[] { "Bank", "_market.BankEngage(", "Wallet.Take(retainer" }
        };
        foreach (string[] call in calls)
        {
            string director = call[0] == "Bank" ? bank : extras;
            Check(Regex.Matches(screen, Regex.Escape(call[1])).Count == 1 && Regex.Matches(director, Regex.Escape(call[2])).Count == 1,
                "stock extras pages: the " + call[0] + " sheet's button asks the market once, and the market books once (" + Regex.Matches(screen, Regex.Escape(call[1])).Count + " and "
                + Regex.Matches(director, Regex.Escape(call[2])).Count + ")");
        }
        Check(Regex.Matches(screen, @"_market\.BankWithdraw\(").Count == 1 && Regex.Matches(engine, @"Event\(MarketInputs\.EventSplit").Count == 3,
            "stock extras pages: the withdraw button asks once, and a split reaches the tape from the founder's ask (once) and from a board's decisions (twice) only");
        // A double press cannot book twice: the second press of the bank's button finds the advisers engaged already.
        var b = new BankFloat();
        StockxFill(b, 300, 4000000, 900000, 500000000);
        BankFacts facts = StockxFacts();
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, false, false) == BankWhy.None, "stock extras pages: a bank may engage advisers");
        b.Stage = BankStage.Advised;
        Check(BankFloatRules.Ready(b, facts, 301, 60, true, false, false) == BankWhy.OnTheWay, "stock extras pages: ...and a second press is refused before any money moves");
    }
}
