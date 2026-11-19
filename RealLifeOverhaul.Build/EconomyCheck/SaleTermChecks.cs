using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The terms a firm's sale is accepted on (flag --sale-terms; Scripts/Tech/Venture/SaleTermRules.cs, SaleTermStore.cs, SaleTermText.cs, TechDirector.SaleTerms.cs, the exits pages of both
// interfaces, Markets/MarketEngine.Grants.cs and the lock in MarketTrading): what each term pays and when, never more in total than its rule says, nothing paid twice (a day run again, a
// long sleep against the months one by one), a lapsed earn-out paying nothing, a locked share that cannot be sold, a contract's pay days, vesting, reviews and endings, the save, and every
// text. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void SaleTermChecks()
    {
        SaleTermPlanChecks();
        SaleTermNeverMoreChecks();
        SaleTermOnceChecks();
        SaleTermEarnChecks();
        SaleTermBossChecks();
        SaleTermLockChecks();
        SaleTermTaxChecks();
        SaleTermDeskChecks();
        SaleTermSaveChecks();
        SaleTermSourceChecks();
        SaleTermTextChecks();
    }

    private const long TermPrice = 1000000000L;   // ten million, cents at the start's prices

    private static TermPlan TermPlanOf(int kind, int step = 0, int target = 0, int measure = 0, long net = TermPrice * 7 / 10, int stock = 5, int cents = 2500, int open = 0, long mrr = 6000000, long customers = 800)
    {
        return SaleTermRules.Plan(new TermChoice { Kind = kind, Step = step, Target = target, Measure = measure }, TermPrice, net, mrr, customers, stock, cents, 1000, open);
    }

    private static TermDeal TermBegin(TermFile f, TermPlan p, int kind, int step = 0, int target = 0, int measure = 0, int day = 100, long mrr = 6000000, long customers = 800)
    {
        return SaleTermRules.Begin(f, p, new TermChoice { Kind = kind, Step = step, Target = target, Measure = measure }, "Elm~1", "Brightwork", "Northgate", 5, 0, day, mrr, customers);
    }

    private static void SaleTermPlanChecks()
    {
        TermPlan cash = TermPlanOf(TermKind.Cash);
        Check(cash.Ok && cash.Now == TermPrice && cash.Deferred == 0 && cash.Most == 0, "sale terms: cash pays all of the price at once and holds nothing back");
        for (int step = 0; step < SaleTermCatalogue.ShareSteps.Length; step++)
        {
            TermPlan p = TermPlanOf(TermKind.Shares, step);
            long want = Math.Min(TermPrice * SaleTermCatalogue.ShareSteps[step] / 1000, TermPrice * 7 / 10);
            Check(p.Ok && p.Shares == want / 2500 && p.Deferred == p.Shares * 2500 && p.Deferred <= want && want - p.Deferred < 2500 && p.Now == TermPrice - p.Deferred && p.LockDays == SaleTermCatalogue.ShareLockDays,
                "sale terms: " + SaleTermCatalogue.ShareSteps[step] + " thousandths in shares is whole shares at the day's price (" + p.Shares + "), the rest of the price cash, and a lock-up of " + p.LockDays + " days");
        }
        Check(!TermPlanOf(TermKind.Shares, 0, 0, 0, TermPrice * 7 / 10, 0, 0).Ok && TermPlanOf(TermKind.Shares, 0, 0, 0, TermPrice * 7 / 10, 0, 0).Why == SaleTermRules.WhyShares,
            "sale terms: no shares are offered where the buyer has no stock to buy");
        Check(TermPlanOf(TermKind.Shares, 0, 0, 0, TermPrice * 7 / 10, 5, int.MaxValue / 2).Why == SaleTermRules.WhySmall, "sale terms: a part of the price worth less than one share is refused, not rounded up");
        Check(TermPlanOf(TermKind.Slice, 0, 0, 0, 0).Why == SaleTermRules.WhyNone && TermPlanOf(TermKind.Earn, 0, 0, 0, 0).Why == SaleTermRules.WhyNone && TermPlanOf(TermKind.Boss, 0, 0, 0, 0).Why == SaleTermRules.WhyNone,
            "sale terms: nothing is held back or kept when none of the price is the player's after the investors");
        Check(!TermPlanOf(TermKind.Earn, 0, 0, 0, TermPrice, 5, 2500, SaleTermCatalogue.OpenMost).Ok && TermPlanOf(TermKind.Earn, 0, 0, 0, TermPrice, 5, 2500, SaleTermCatalogue.OpenMost - 1).Ok,
            "sale terms: no more than " + SaleTermCatalogue.OpenMost + " deals stand at once");
        for (int step = 0; step < SaleTermCatalogue.SliceSteps.Length; step++)
        {
            TermPlan p = TermPlanOf(TermKind.Slice, step);
            Check(p.Ok && p.Deferred == TermPrice * SaleTermCatalogue.SliceSteps[step] / 1000 && p.Mille == SaleTermCatalogue.SliceSteps[step] && p.Now == TermPrice - p.Deferred && p.Mille < 500
                  && p.Cap == p.Deferred * 3, "sale terms: a slice of " + SaleTermCatalogue.SliceSteps[step] + " thousandths is a minority, paid for by that much less now, with a cap of three times its price");
        }
        TermPlan clamped = TermPlanOf(TermKind.Slice, 2, 0, 0, TermPrice / 20);
        Check(clamped.Ok && clamped.Deferred == TermPrice / 20 && clamped.Mille == 50 && clamped.Now == TermPrice - TermPrice / 20,
            "sale terms: a stake that would be more than the player's own part after the investors is cut to that part, and its size says so");
        TermPlan earn = TermPlanOf(TermKind.Earn, 0, 1);
        Check(earn.Ok && earn.Deferred == TermPrice * 3 / 10 && earn.Further == earn.Deferred * SaleTermCatalogue.EarnFurther[1] / 1000 && earn.Target == SaleTermCatalogue.EarnTargets[1] && earn.Figure == 6000000
              && earn.Now == TermPrice - earn.Deferred, "sale terms: an earn-out holds back a part of the price and names its prize and its target, a share of the takings at the sale");
        Check(TermPlanOf(TermKind.Earn, 0, 0, TermMeasure.Customers, TermPrice, 5, 2500, 0, 6000000, 0).Why == SaleTermRules.WhyMeasure && TermPlanOf(TermKind.Earn, 0, 0, TermMeasure.Customers).Figure == 800,
            "sale terms: an earn-out on customers needs customers at the sale to count");
        bool ordered = true;
        for (int i = 1; i < SaleTermCatalogue.EarnTargets.Length; i++)
        {
            ordered &= SaleTermCatalogue.EarnTargets[i] > SaleTermCatalogue.EarnTargets[i - 1] && SaleTermCatalogue.EarnFurther[i] > SaleTermCatalogue.EarnFurther[i - 1];
        }
        Check(ordered && SaleTermCatalogue.EarnTargets[0] > 1000, "sale terms: a harder target always pays a bigger prize, and every target asks for growth");
        TermPlan boss = TermPlanOf(TermKind.Boss, 1, 0);
        Check(boss.Ok && boss.Years == 4 && boss.Grant == TermPrice * SaleTermCatalogue.BossGrant / 1000 && boss.Deferred == boss.Grant + TermPrice * SaleTermCatalogue.BossEarn / 1000
              && boss.Now == TermPrice - boss.Deferred && boss.Salary >= SaleTermCatalogue.SalaryLeast && boss.Salary <= SaleTermCatalogue.SalaryMost && boss.Shares > 0, "sale terms: a contract's figures: its years, grant, held back part, salary within the founders' band and shares to vest");
        Check(TermPlanOf(TermKind.Boss, 0, 0, 0, TermPrice * 7 / 10, 0, 0).Shares == 0 && TermPlanOf(TermKind.Boss, 0, 0, 0, TermPrice * 7 / 10, 0, 0).Ok && TermPlanOf(TermKind.Boss, 0).Years == 3,
            "sale terms: a contract with no listed buyer vests its grant as a bonus, and is three years at the first step");
        Check(boss.Most == boss.Salary * (4 * SaleTermCatalogue.YearDays / SaleTermCatalogue.MonthDays) + boss.Grant + boss.Further, "sale terms: a contract's most is its months of pay, its grant and its prize");
    }

    // Every deal, over ten years of days, seed by seed: what it pays never passes its most, never goes backwards, and ends when its rule says.
    private static void SaleTermNeverMoreChecks()
    {
        bool within = true, done = true, same = true;
        long worst = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            foreach (int kind in new[] { TermKind.Slice, TermKind.Earn })
            {
                var f = new TermFile { Seed = seed * 977 };
                TermPlan p = TermPlanOf(kind, seed % 3, seed % 3);
                TermDeal d = TermBegin(f, p, kind, seed % 3, seed % 3);
                var events = new List<TermEvent>();
                long paid = 0, last = 0;
                for (int day = 100; day <= 100 + 12 * 360; day++)
                {
                    events.Clear();
                    SaleTermRules.Day(f, d, day, default(TermWorld), events);
                    foreach (TermEvent e in events)
                    {
                        paid += e.Cents;
                    }
                    within &= d.Paid <= d.Most && d.Paid >= last;
                    last = d.Paid;
                }
                same &= paid == d.Paid;
                done &= d.State == TermState.Done && !d.Live;
                worst = Math.Max(worst, d.Most > 0 ? d.Paid * 1000 / d.Most : 0);
            }
        }
        Check(within, "sale terms: over ten years and forty seeds a slice and an earn-out never pay more than their written most, and never take anything back");
        Check(same, "sale terms: what the events paid is what the deal says it paid");
        Check(done, "sale terms: a slice ends at its term and an earn-out is judged once, and neither is live afterwards");
        Check(worst > 0, "sale terms: some deals do pay (" + worst + " thousandths of their most at the largest)");
        // the slice: a month's dividend and the buyout, from the rules
        var g = new TermFile { Seed = 5 };
        TermPlan sp = TermPlanOf(TermKind.Slice, 1);
        TermDeal s = TermBegin(g, sp, TermKind.Slice, 1);
        var ev = new List<TermEvent>();
        SaleTermRules.Day(g, s, 100 + SaleTermCatalogue.MonthDays - 1, default(TermWorld), ev);
        Check(ev.Count == 0 && s.Paid == 0, "sale terms: a slice pays nothing before its first month is out");
        SaleTermRules.Day(g, s, 100 + SaleTermCatalogue.MonthDays, default(TermWorld), ev);
        long month = (s.Mrr0 * s.Index / 1000) * SaleTermCatalogue.SliceMargin / 1000 * s.Mille / 1000;
        Check(ev.Count == 1 && ev[0].Kind == TermEventKind.SliceDividend && ev[0].Cents == month && s.Paid == month && month > 0,
            "sale terms: a slice's month pays its share of the profit the firm's takings make (" + month + ")");
        s.Index = 2000;
        Check(SaleTermRules.SliceBuyout(s) == TermPrice * s.Mille / 1000 * 2000 / 1000 * (1000 - SaleTermCatalogue.MinorityDiscount) / 1000 && SaleTermRules.SliceBuyout(s) < TermPrice * s.Mille / 1000 * 2,
            "sale terms: the buyout moves with the firm and is less than the stake's full worth by the minority discount");
    }

    private static void SaleTermOnceChecks()
    {
        var f = new TermFile { Seed = 77 };
        var g = new TermFile { Seed = 77 };
        TermDeal a = TermBegin(f, TermPlanOf(TermKind.Slice, 0), TermKind.Slice);
        TermDeal b = TermBegin(g, TermPlanOf(TermKind.Slice, 0), TermKind.Slice);
        var ea = new List<TermEvent>();
        var eb = new List<TermEvent>();
        for (int day = 100; day <= 100 + 400; day++)
        {
            SaleTermRules.Day(g, b, day, default(TermWorld), eb);   // month by month
        }
        SaleTermRules.Day(f, a, 100 + 400, default(TermWorld), ea);   // one long sleep
        Check(ea.Count == eb.Count && a.Paid == b.Paid && a.Index == b.Index && a.Seen == b.Seen, "sale terms: a long sleep pays every month it passed and the same as the days one by one (" + a.Paid + ")");
        int before = ea.Count;
        long paid = a.Paid;
        SaleTermRules.Day(f, a, 100 + 400, default(TermWorld), ea);
        SaleTermRules.Day(f, a, 100 + 399, default(TermWorld), ea);
        Check(ea.Count == before && a.Paid == paid, "sale terms: a day run again, or an earlier one, pays nothing twice");
        var h = new TermFile { Seed = 77 };
        TermDeal c = TermBegin(h, TermPlanOf(TermKind.Slice, 0), TermKind.Slice);
        var ec = new List<TermEvent>();
        SaleTermRules.Day(h, c, 100 + SaleTermCatalogue.SliceTermDays + 90, default(TermWorld), ec);
        int buyouts = 0;
        foreach (TermEvent e in ec)
        {
            buyouts += e.Kind == TermEventKind.SliceBought ? 1 : 0;
        }
        Check(buyouts == 1 && c.State == TermState.Done && c.Paid <= c.Most, "sale terms: a stake is bought out once, at its end, and never past its most");
        SaleTermRules.Day(h, c, 100 + 4000, default(TermWorld), ec);
        Check(c.State == TermState.Done && !c.Live, "sale terms: a finished deal is not live");
        int n = ec.Count;
        SaleTermRules.Day(h, c, 100 + 9000, default(TermWorld), ec);
        Check(ec.Count == n, "sale terms: a finished deal pays nothing however late the day");
    }

    private static void SaleTermEarnChecks()
    {
        // a lapsed earn-out pays nothing
        var f = new TermFile { Seed = 11 };
        TermDeal d = TermBegin(f, TermPlanOf(TermKind.Earn, 0, 2), TermKind.Earn, 0, 2);
        var ev = new List<TermEvent>();
        d.Index = SaleTermCatalogue.IndexLeast;
        SaleTermRules.Day(f, d, d.Due - 1, default(TermWorld), ev);
        Check(d.Open && d.EarnState == 0, "sale terms: an earn-out is judged on its day and not before");
        d.Index = SaleTermCatalogue.IndexLeast;
        d.Seen = d.Due - SaleTermCatalogue.MonthDays;
        SaleTermRules.Day(f, d, d.Due, default(TermWorld), ev);
        int lapsed = 0, paid = 0;
        foreach (TermEvent e in ev)
        {
            lapsed += e.Kind == TermEventKind.EarnLapsed ? 1 : 0;
            paid += e.Kind == TermEventKind.EarnPaid ? 1 : 0;
        }
        Check(d.EarnState == 2 && lapsed == 1 && paid == 0 && d.Paid == 0 && d.State == TermState.Done, "sale terms: an earn-out that missed its target lapses with one notice and pays nothing");
        // one that reached it pays once, and only its written prize
        var g = new TermFile { Seed = 11 };
        TermPlan plan = TermPlanOf(TermKind.Earn, 0, 0);
        TermDeal e1 = TermBegin(g, plan, TermKind.Earn, 0, 0);
        var ev2 = new List<TermEvent>();
        e1.Index = SaleTermCatalogue.IndexMost;
        e1.Seen = e1.Due - SaleTermCatalogue.MonthDays;
        SaleTermRules.Day(g, e1, e1.Due, default(TermWorld), ev2);
        long prize = 0;
        int paidEvents = 0;
        foreach (TermEvent e in ev2)
        {
            prize += e.Kind == TermEventKind.EarnPaid ? e.Cents : 0;
            paidEvents += e.Kind == TermEventKind.EarnPaid ? 1 : 0;
        }
        Check(paidEvents == 1 && prize == plan.Further && e1.Paid == plan.Further && e1.EarnState == 1, "sale terms: an earn-out that reached its target pays its written prize (" + prize + ") once");
        SaleTermRules.Day(g, e1, e1.Due + 400, default(TermWorld), ev2);
        Check(e1.Paid == plan.Further, "sale terms: and never again");
        // the progress and the aim, on takings and on customers
        var h = new TermFile { Seed = 3 };
        TermDeal c = TermBegin(h, TermPlanOf(TermKind.Earn, 0, 1, TermMeasure.Customers), TermKind.Earn, 0, 1, TermMeasure.Customers);
        Check(SaleTermRules.Aim(c) == 800 * SaleTermCatalogue.EarnTargets[1] / 1000 && SaleTermRules.Progress(c, default(TermWorld)) == 800 && c.Measure == TermMeasure.Customers,
            "sale terms: an earn-out on customers is judged on customers, from the figure at the sale");
        // the drift is bounded and the same everywhere
        bool bounded = true, same = true;
        for (int i = 1; i <= 200; i++)
        {
            int dr = SaleTermRules.Drift(41, i, i % 48, i % 2);
            bounded &= dr >= SaleTermCatalogue.DriftLeast && dr <= SaleTermCatalogue.DriftMost;
            same &= dr == SaleTermRules.Drift(41, i, i % 48, i % 2);
        }
        Check(bounded && same, "sale terms: the modelled growth of a firm the player no longer runs is seeded, bounded and the same on every machine");
        long mean = 0;
        for (int i = 0; i < 1000; i++)
        {
            mean += SaleTermRules.Drift(9, i, i % 60, 0);
        }
        Check(mean / 1000 > 5 && mean / 1000 < 35, "sale terms: a firm grows by a few per cent a quarter on average (" + mean / 1000 + " thousandths a month)");
    }

    private static void SaleTermBossChecks()
    {
        var f = new TermFile { Seed = 21 };
        TermPlan plan = TermPlanOf(TermKind.Boss, 0, 0);
        TermDeal d = TermBegin(f, plan, TermKind.Boss, 0, 0);
        var ev = new List<TermEvent>();
        int salaries = 0, vests = 0, remits = 0;
        long paid = 0;
        // a firm that does well: takings at the sale's, customers above its aim
        var world = new TermWorld { FirmHere = true, Takings = d.Mrr0 * 1200 / 1000, Customers = 5000, StockLive = true, StockCents = 2500 };
        for (int day = d.Day; day <= d.Day + 4 * 360; day++)
        {
            ev.Clear();
            SaleTermRules.Day(f, d, day, world, ev);
            foreach (TermEvent e in ev)
            {
                salaries += e.Kind == TermEventKind.Salary ? 1 : 0;
                vests += e.Kind == TermEventKind.VestShares || e.Kind == TermEventKind.VestCash ? 1 : 0;
                remits += e.Kind == TermEventKind.Remit ? 1 : 0;
                paid += e.Kind == TermEventKind.Salary || e.Kind == TermEventKind.EarnPaid || e.Kind == TermEventKind.VestCash ? e.Cents : 0;
            }
            d.Handed = d.Handed || !d.Open;   // the director's day hands it over
        }
        Check(salaries == 3 * 12 && vests == 3 && d.State == TermState.Done && d.Paid == paid && d.Paid <= d.Most, "sale terms: a three year contract pays thirty six pay days, vests three years and ends at its term (" + salaries + ", " + vests + ")");
        Check(remits == 3 * 12 && d.EarnState == 1 && d.Strikes == 0, "sale terms: its firm's profit is settled with its owner every month, and the earn-out was judged on the real figures");
        // two bad readings in a row dismiss; a good one between clears the strike
        var g = new TermFile { Seed = 21 };
        TermDeal e2 = TermBegin(g, plan, TermKind.Boss, 0, 0);
        var ev2 = new List<TermEvent>();
        var weak = new TermWorld { FirmHere = true, Takings = e2.Mrr0 * (SaleTermCatalogue.KeepMille - 50) / 1000, Customers = 1, StockLive = true, StockCents = 2500 };
        int strikes = 0, dismissed = 0;
        for (int day = e2.Day; day <= e2.Day + 400; day++)
        {
            ev2.Clear();
            SaleTermRules.Day(g, e2, day, weak, ev2);
            foreach (TermEvent e in ev2)
            {
                strikes += e.Kind == TermEventKind.Strike ? 1 : 0;
                dismissed += e.Kind == TermEventKind.Dismissed ? 1 : 0;
            }
            e2.Handed = e2.Handed || !e2.Open;
        }
        Check(strikes == 1 && dismissed == 1 && e2.State == TermState.Dismissed && e2.EarnState == 2 && e2.EndDay <= e2.Seen, "sale terms: two readings in a row under the written floor dismiss the boss, who loses the earn-out");
        Check(e2.Paid < d.Paid && e2.Vested < e2.Years, "sale terms: what had not vested when the boss was dismissed is lost, and the pay stops");
        var h = new TermFile { Seed = 21 };
        TermDeal c = TermBegin(h, plan, TermKind.Boss, 0, 0);
        var ev3 = new List<TermEvent>();
        var mixed = new TermWorld { FirmHere = true, Takings = c.Mrr0 * (SaleTermCatalogue.KeepMille - 50) / 1000, Customers = 1 };
        var fine = new TermWorld { FirmHere = true, Takings = c.Mrr0, Customers = 1 };
        SaleTermRules.Day(h, c, c.Day + SaleTermCatalogue.ReviewDays, mixed, ev3);
        Check(c.Strikes == 1 && c.Open, "sale terms: one reading under the floor is a strike and a notice, not a dismissal");
        SaleTermRules.Day(h, c, c.Day + 2 * SaleTermCatalogue.ReviewDays, fine, ev3);
        SaleTermRules.Day(h, c, c.Day + 3 * SaleTermCatalogue.ReviewDays, mixed, ev3);
        Check(c.Strikes == 1 && c.Open, "sale terms: a good reading clears the strike, so strikes must run in a row");
        // resigning
        var evr = new List<TermEvent>();
        Check(SaleTermRules.Resign(c, evr) && c.State == TermState.Resigned && c.EarnState == 2 && !SaleTermRules.Resign(c, evr) && evr.Count == 1, "sale terms: resigning ends the contract once and loses the earn-out");
        long before = c.Paid;
        var evAfter = new List<TermEvent>();
        SaleTermRules.Day(h, c, c.Day + 800, fine, evAfter);
        bool onlyRemit = true;
        foreach (TermEvent e in evAfter)
        {
            onlyRemit &= e.Kind == TermEventKind.Remit;
        }
        Check(c.Paid == before && onlyRemit && c.Live, "sale terms: after resigning only the firm's profit still goes to its owner, until the business is handed over");
        c.Handed = true;
        evAfter.Clear();
        SaleTermRules.Day(h, c, c.Day + 1200, fine, evAfter);
        Check(!c.Live && evAfter.Count == 0, "sale terms: once the business has been handed over the contract does nothing");
        // a firm closed or sold by other means
        var k = new TermFile { Seed = 21 };
        TermDeal gone = TermBegin(k, plan, TermKind.Boss, 0, 0);
        var evg = new List<TermEvent>();
        SaleTermRules.Day(k, gone, gone.Day + SaleTermCatalogue.MonthDays, default(TermWorld), evg);
        Check(gone.State == TermState.Gone && gone.Handed && evg.Count == 1 && evg[0].Kind == TermEventKind.Gone && gone.Paid == 0, "sale terms: a contract's firm that is gone from the player's hands ends it, with no pay for the month");
        Check(f.BossOf("Elm~1") == null && g.BossOf("Elm~1") == null && h.BossOf("Elm~1") == null, "sale terms: a business is held by a contract only while the contract lives");
        var m = new TermFile { Seed = 1 };
        TermDeal live = TermBegin(m, plan, TermKind.Boss, 0, 0);
        Check(m.BossOf("Elm~1") == live && m.BossOf("Oak~2") == null && m.OpenCount() == 1, "sale terms: the business of a live contract is held, and no other");
        // a bonus contract's year pays cash, and the paid sum counts it
        var n = new TermFile { Seed = 8 };
        TermPlan bonus = TermPlanOf(TermKind.Boss, 0, 0, 0, TermPrice * 7 / 10, 0, 0);
        TermDeal bd = TermBegin(n, bonus, TermKind.Boss, 0, 0);
        var evb = new List<TermEvent>();
        SaleTermRules.Day(n, bd, bd.Day + SaleTermCatalogue.YearDays, new TermWorld { FirmHere = true, Takings = bd.Mrr0 }, evb);
        long cashBonus = 0;
        foreach (TermEvent e in evb)
        {
            cashBonus += e.Kind == TermEventKind.VestCash ? e.Cents : 0;
        }
        Check(cashBonus == bonus.Grant / 3 && bd.Vested == 1 && bd.Paid >= bonus.Grant / 3, "sale terms: with no listed buyer a contract's year vests a cash bonus of a third of the grant");
    }

    private static void SaleTermLockChecks()
    {
        MarketEngine m = InvestMarket(332, 500000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Mid);
        int day = m.State.Day;
        Check(m.GrantPrice(s.Id) == s.MidCents && m.GrantPrice(-5) == 0, "sale terms: the market's price for shares paid is the day's price, and none for a company that is not there");
        bool granted = m.Grant(s.Id, 1000, s.MidCents, day + 30);
        Position p = m.State.Position(s.Id);
        Check(granted && p != null && p.Shares == 1000 && p.Cost == MarketTrading.Value(1000, s.MidCents, true) && m.State.Locks.Count == 1 && MarketTrading.Locked(m.State, s.Id, day) == 1000,
            "sale terms: shares paid are in the portfolio at the price paid, under a lock of their own");
        Check(m.Grant(s.Id, 500, s.MidCents, day + 30) && m.State.Locks.Count == 1 && MarketTrading.Locked(m.State, s.Id, day) == 1500 && MarketTrading.LockedUntil(m.State, s.Id, day) == day + 30,
            "sale terms: shares of the same stock and day share one lock");
        Check(!m.Grant(-9, 10, 100, 0) && !m.Grant(s.Id, 0, 100, 0) && !m.Grant(s.Id, 10, 0, 0), "sale terms: shares are not paid in for a stock that is not there, for nothing, or at no price");
        m.Place(s.Id, OrderSide.Sell, OrderType.Market, OrderLife.Day, 1, 0, 0, true, false, false, out OrderWhy why);
        Check(why == OrderWhy.Locked && m.State.Held(s.Id) == 1500, "sale terms: a locked share cannot be sold: the order is refused with its reason, and the holding is whole");
        Check(ModSource(@"Markets\MarketText.cs").Contains("\"switched\", \"locked\"") && (int)OrderWhy.Locked == (int)OrderWhy.Switched + 1 && OrderWhy.Locked < OrderWhy.Count, "sale terms: the refusal has its place in the table of sentences");
        // shares the player holds besides are free to sell
        m.State.Position(s.Id).Shares = 1500 + 400;
        m.Place(s.Id, OrderSide.Sell, OrderType.Market, OrderLife.Day, 400, 0, 0, true, false, false, out OrderWhy free);
        Check(free == OrderWhy.None && m.State.Held(s.Id) == 1500, "sale terms: shares held besides the locked ones can be sold, and the locked ones stay");
        m.Place(s.Id, OrderSide.Sell, OrderType.Market, OrderLife.Day, 1, 0, 0, true, false, false, out OrderWhy again);
        Check(again == OrderWhy.Locked, "sale terms: and not one share more");
        OrderWhy worked = m.WorkedWhy(s, OrderSide.Sell, 1, false);
        Check(worked == OrderWhy.Locked, "sale terms: a worked sale of locked shares is refused the same way");
        // the save keeps the lock
        string line = DeskStore.Any(m.State) ? "any" : string.Empty;
        var text = new System.Text.StringBuilder();
        DeskStore.Write(text, m.State);
        var back = new MarketState();
        Check(line == "any" && DeskStore.Read(back, text.ToString()) && back.Locks.Count == 1 && back.Locks[0].Shares == 1500 && back.Locks[0].UntilDay == day + 30 && back.Locks[0].StockId == s.Id,
            "sale terms: the lock is saved in the desk's part of the market's line, and read back whole");
        var older = new MarketState();
        Check(DeskStore.Read(older, "1|c|0|0|0|0|0") && older.Locks.Count == 0, "sale terms: a market line from before has no lock and reads as it did");
        // it runs out
        m.State.Day = day + 30;
        Check(MarketTrading.Locked(m.State, s.Id, m.State.Day) == 0 && MarketTrading.LockedUntil(m.State, s.Id, m.State.Day) == 0, "sale terms: the day the lock-up ends the shares are free");
        Check(m.SweepLocks() && m.State.Locks.Count == 0, "sale terms: the day's sweep takes a lock that has run out off the list");
        m.State.Day = day;
        m.Grant(s.Id, 10, s.MidCents, day + 5);
        m.State.Position(s.Id).Shares = 0;
        Check(m.SweepLocks() && m.State.Locks.Count == 0, "sale terms: a lock on shares no longer held (a takeover, a delisting) comes off");
        // the locks are never more than the room
        bool most = true;
        for (int i = 0; i < MarketState.LocksMost + 5; i++)
        {
            most &= m.Grant(s.Id, 1, s.MidCents, day + 100 + i) || m.State.Locks.Count >= MarketState.LocksMost;
        }
        Check(m.State.Locks.Count <= MarketState.LocksMost && most, "sale terms: no more than " + MarketState.LocksMost + " locks are kept");
    }

    // What each kind of payment is to the tax: a dividend, a gain over what was held back, or nothing (pay for work: the game and the mod tax no wages); a contract firm's profit lifted out of the takings.
    private static void SaleTermTaxChecks()
    {
        long dividends = 0, gains = 0, none = 0;
        bool once = true, tallied = true;
        foreach (int kind in new[] { TermKind.Slice, TermKind.Earn, TermKind.Boss })
        {
            var f = new TermFile { Seed = 9 };
            TermPlan p = TermPlanOf(kind, 1, 0);
            TermDeal d = TermBegin(f, p, kind, 1, 0);
            var ev = new List<TermEvent>();
            var world = new TermWorld { FirmHere = true, Takings = d.Mrr0 * 1300 / 1000, Customers = 5000, StockLive = false, StockCents = 0 };
            d.Index = kind == TermKind.Earn ? SaleTermCatalogue.IndexMost : d.Index;
            long paidViaEvents = 0, taxed = 0;
            for (int day = d.Day; day <= d.Day + 4 * 360; day++)
            {
                ev.Clear();
                SaleTermRules.Day(f, d, day, world, ev);
                foreach (TermEvent e in ev)
                {
                    TermTaxing t = SaleTermRules.Taxing(d, e);
                    bool paid = e.Kind == TermEventKind.SliceDividend || e.Kind == TermEventKind.SliceBought || e.Kind == TermEventKind.EarnPaid || e.Kind == TermEventKind.Salary || e.Kind == TermEventKind.VestCash;
                    once &= !(t.Dividend > 0 && t.Gain > 0) && (t.Dividend + t.Gain <= e.Cents) && (e.Kind != TermEventKind.SliceDividend || (t.Dividend == e.Cents && t.Gain == 0))
                            && ((e.Kind != TermEventKind.Salary && e.Kind != TermEventKind.VestCash && e.Kind != TermEventKind.VestShares && e.Kind != TermEventKind.Remit) || (t.Dividend == 0 && t.Gain == 0));
                    dividends += t.Dividend;
                    gains += t.Gain;
                    none += paid && t.Dividend + t.Gain == 0 ? e.Cents : 0;
                    taxed += t.Dividend + t.Gain;
                    paidViaEvents += paid ? e.Cents : 0;
                }
                d.Handed = d.Handed || !d.Open;
            }
            tallied &= taxed <= paidViaEvents;
        }
        Check(once, "sale terms tax: each payment is one thing to the tax: a dividend, a gain, or nothing; never two, never more than was paid");
        Check(tallied && dividends > 0 && gains > 0 && none > 0, "sale terms tax: dividends (" + dividends + "), gains (" + gains + ") and untaxed pay (" + none + ") all arise over the deals, none above what they paid");
        var g = new TermFile { Seed = 2 };
        TermDeal sl = TermBegin(g, TermPlanOf(TermKind.Slice, 0), TermKind.Slice, 0);
        TermTaxing buy = SaleTermRules.Taxing(sl, new TermEvent { Deal = sl, Kind = TermEventKind.SliceBought, Cents = sl.Deferred + 5000 });
        TermTaxing under = SaleTermRules.Taxing(sl, new TermEvent { Deal = sl, Kind = TermEventKind.SliceBought, Cents = sl.Deferred - 5000 });
        Check(buy.Gain == 5000 && buy.Dividend == 0 && under.Gain == 0, "sale terms tax: a buyout is a capital gain over the price of the stake, never under nothing");
        TermDeal er = TermBegin(g, TermPlanOf(TermKind.Earn, 0, 0), TermKind.Earn, 0, 0);
        TermTaxing prize = SaleTermRules.Taxing(er, new TermEvent { Deal = er, Kind = TermEventKind.EarnPaid, Cents = er.Further });
        Check(prize.Gain == er.Further - er.Deferred && prize.Dividend == 0, "sale terms tax: an earn-out's prize is a capital gain over what was held back, which the sale noted already");
        TermDeal bs = TermBegin(g, TermPlanOf(TermKind.Boss, 0, 0), TermKind.Boss, 0, 0);
        TermTaxing bp = SaleTermRules.Taxing(bs, new TermEvent { Deal = bs, Kind = TermEventKind.EarnPaid, Cents = bs.Further });
        Check(bp.Gain == bs.Further - (bs.Deferred - bs.Grant), "sale terms tax: a contract's earn-out prize is over its own held back part, not the grant");
        // a contract firm's profit is the owner's: out of the takings, once, by the days it covers
        var h = new TermFile { Seed = 3 };
        SaleTermRules.Hold(h, 100, 129, 3000);
        Check(SaleTermRules.Held(h, 100, 129) == 3000 && SaleTermRules.Held(h, 100, 114) == 1500 && SaleTermRules.Held(h, 0, 99) == 0 && SaleTermRules.Held(h, 130, 400) == 0,
            "sale terms tax: a month's profit paid to the owner is lifted out of the days it covers and no others");
        Check(SaleTermRules.Held(h, 0, 114) + SaleTermRules.Held(h, 115, 200) == 3000, "sale terms tax: two quarters that split a month take out the whole of it between them, once");
        SaleTermRules.Hold(h, 130, 159, -600);
        Check(SaleTermRules.Held(h, 100, 159) == 2400, "sale terms tax: a loss the owner carried puts the takings back by as much");
        SaleTermRules.Hold(h, 160, 159, 99);
        SaleTermRules.Hold(h, 200, 229, 0);
        Check(h.Held.Count == 2, "sale terms tax: an empty or backwards stretch enters nothing");
        for (int i = 0; i < TermFile.HeldMost + 20; i++)
        {
            SaleTermRules.Hold(h, 300 + i, 300 + i, 1);
        }
        Check(h.Held.Count <= TermFile.HeldMost && SaleTermRules.Held(h, 300 + TermFile.HeldMost + 19, 300 + TermFile.HeldMost + 19) == 1, "sale terms tax: the ledger is bounded and the oldest line goes first");
        SaleTermRules.Hold(h, 2000, 2029, 5);
        Check(SaleTermRules.Held(h, 100, 129) == 0, "sale terms tax: a line older than the return can look back on is dropped");
        var k = new TermFile { Seed = 4 };
        SaleTermRules.Hold(k, 10, 39, 3000);
        TermBegin(k, TermPlanOf(TermKind.Slice, 0), TermKind.Slice);
        TermFile back = SaleTermStore.Load(new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(k) } }, out _);
        Check(back != null && back.Held.Count == 1 && SaleTermRules.Held(back, 10, 39) == 3000, "sale terms tax: the ledger is saved and read back, in a part an older line does not have");
        var onlyHeld = new TermFile { Seed = 4 };
        SaleTermRules.Hold(onlyHeld, 10, 39, 3000);
        Check(SaleTermStore.Write(onlyHeld) != null && SaleTermStore.Load(new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(onlyHeld) } }, out _).Held.Count == 1,
            "sale terms tax: a ledger with no deal left is still kept");
        int none2 = RealLifeOverhaul.Economy.TaxRules.OtherIncome(null, null, 0, 9, (a, b) => 700);
        int both = RealLifeOverhaul.Economy.TaxRules.OtherIncome((a, b) => 1000, (a, b) => 200, 0, 9, (a, b) => 700);
        Check(none2 == -700 && both == 500 && RealLifeOverhaul.Economy.TaxRules.OtherIncome((a, b) => 1000, (a, b) => 200, 0, 9) == 1200,
            "sale terms tax: the return's other income takes a contract firm's profit off the takings, and is as it was with none");
        string d1 = ModSource(@"Tech\TechDirector.SaleTerms.cs"), game = ModSource(@"Economy\Company\CompanyGame.cs"), links = ModSource(@"Wiring\Wiring.ExitLinks.cs");
        Check(d1.Contains("PayDeductible(sum, VentureGame.TermRemitKey") && d1.Contains("SaleTermRules.Hold(_terms, since + 1, day, sum / 100)") && ModSource(@"Tech\Venture\VentureGame.cs").Contains("SetTaxDeductibleName(deductibleWords)"),
            "sale terms tax: the profit paid to the owner is deductible by the game's own tax, and entered in the ledger the return reads");
        Check(game.Contains("TaxRules.OtherIncome(OtherIncome, StartupGains, fromDay, toDay, HeldProfit)") && links.Contains("CompanyBridges.HeldProfit = ") && d1.Contains("HeldBetween(int fromDay, int toDay)"),
            "sale terms tax: the quarterly return, its page and the reserve (one sum, CompanyGame.Other) all read the lifted profit");
        Check(d1.Contains("WithholdDividend(cents, name)") && d1.Contains("ExitBridges.DividendTax(cents / 100)") && links.Contains("StakeDividendTax") && ModSource(@"Markets\MarketDirector.Grants.cs").Contains("ListingVotes.DividendTax"),
            "sale terms tax: a stake's dividend is taxed by the stock market's own dividend rule, as a line of its own");
        Check(d1.Contains("SaleTermRules.Taxing(d, e).Gain") && !d1.Contains("ExitGains.Note(_gains, day, name, e.Cents)"), "sale terms tax: a buyout and a prize are noted as gains once, from one place, and a dividend is not also a gain");
        int salary = d1.IndexOf("case TermEventKind.Salary:", StringComparison.Ordinal);
        string salaryBlock = salary > 0 ? d1.Substring(salary, d1.IndexOf("case TermEventKind.VestShares:", salary, StringComparison.Ordinal) - salary) : string.Empty;
        Check(salaryBlock.Contains("TermSalaryKey") && !salaryBlock.Contains("Tax") && !salaryBlock.Contains("Note("), "sale terms tax: the salary is paid in full with no tax line, as every wage the mod pays is (the game takes none)");
        Check(ModSource(@"Economy\BizLines.cs").Contains("\"tech_terms_dividend_tax\""), "sale terms tax: the dividend tax line is in a list of BizLines");
    }

    private static void SaleTermDeskChecks()
    {
        string links = ModSource(@"Wiring\Wiring.ExitLinks.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(links.Contains("C(\"offer_accept\", 20), C(\"offer_counter\", 40), C(\"offer_decline\", 0), C(\"offer_shares\", 20), C(\"offer_slice\", 20), C(\"offer_earn\", 20), C(\"offer_boss\", 30)"),
            "sale terms desk: the offer's session keeps accept, counter and decline as the first three choices and adds the four terms after them");
        Check(exits.Contains("choice == 0 || choice >= 3") && exits.Contains("SessionTerms(choice)") && exits.Contains("ExitAccept(place, o.Id, terms)")
              && exits.Contains("terms.Kind == TermKind.Boss ? null : ExitBridges.WhyNotHand(place)"), "sale terms desk: the session's accept takes the terms through the same ExitAccept the page uses; a contract may be made from inside");
        Check(exits.Contains("case 3: return new TermChoice { Kind = TermKind.Shares, Step = 1 }") && exits.Contains("case 6: return new TermChoice { Kind = TermKind.Boss, Step = 0, Target = 1 }")
              && exits.Contains("internal bool ExitAccept(string place, int offerId, TermChoice choice = default(TermChoice))"), "sale terms desk: a session choice is a TermChoice, and ExitAccept takes one to be called later with it stored");
        Dictionary<string, string> en = LocaleFile("en");
        bool all = true;
        foreach (string n in new[] { "shares", "slice", "earn", "boss" })
        {
            string a = "reallife_tech_desk_exit_offer_" + n;
            all &= en.TryGetValue(a, out string t1) && en.TryGetValue(a + "_trade", out string t2) && t1.Length > 0 && t2.Length > 0 && t2.Length <= 190 && !t2.Contains("!") && !t2.Contains("—");
        }
        Check(all, "sale terms desk: every choice has its name and its tip within 190 characters");
    }

    private static void SaleTermSaveChecks()
    {
        var f = new TermFile { Seed = 321 };
        TermDeal a = TermBegin(f, TermPlanOf(TermKind.Shares, 1), TermKind.Shares, 1);
        TermDeal b = TermBegin(f, TermPlanOf(TermKind.Slice, 2), TermKind.Slice, 2);
        TermDeal c = TermBegin(f, TermPlanOf(TermKind.Earn, 0, 2, TermMeasure.Customers), TermKind.Earn, 0, 2, TermMeasure.Customers);
        TermDeal d = TermBegin(f, TermPlanOf(TermKind.Boss, 1, 1), TermKind.Boss, 1, 1);
        d.Strikes = 1;
        d.Vested = 2;
        d.Absent = 2;
        d.Firm = "Bright's work & Co, one";
        b.Index = 1234;
        var events = new List<TermEvent>();
        SaleTermRules.Day(f, b, b.Day + 100, default(TermWorld), events);
        string line = SaleTermStore.Write(f);
        Check(SaleTermStore.Key == "reallife:tech_sale_terms" && line != null && line.StartsWith("v=1") && SaleTermStore.Key != ExitStore.Key, "sale terms: the deals have a line of their own, reallife:tech_sale_terms, apart from the exits'");
        var data = new Dictionary<string, string> { { SaleTermStore.Key, line } };
        TermFile back = SaleTermStore.Load(data, out int version);
        bool same = back != null && version == 1 && back.Deals.Count == 4 && back.Seed == 321 && back.Next == f.Next;
        for (int i = 0; same && i < f.Deals.Count; i++)
        {
            TermDeal x = f.Deals[i], y = back.Deals[i];
            same = x.Id == y.Id && x.Kind == y.Kind && x.State == y.State && x.Day == y.Day && x.EndDay == y.EndDay && x.Place == y.Place && x.Firm == y.Firm && x.Buyer == y.Buyer && x.Price == y.Price
                   && x.Now == y.Now && x.Deferred == y.Deferred && x.Mrr0 == y.Mrr0 && x.Cust0 == y.Cust0 && x.Paid == y.Paid && x.Most == y.Most && x.Seen == y.Seen && x.Index == y.Index
                   && x.CIndex == y.CIndex && x.Stock == y.Stock && x.Shares == y.Shares && x.ShareCents == y.ShareCents && x.LockTo == y.LockTo && x.Mille == y.Mille && x.Measure == y.Measure
                   && x.Target == y.Target && x.EarnState == y.EarnState && x.Due == y.Due && x.Further == y.Further && x.Salary == y.Salary && x.Grant == y.Grant && x.Years == y.Years
                   && x.Vested == y.Vested && x.Strikes == y.Strikes && x.Review == y.Review && x.Handed == y.Handed && x.Synced == y.Synced && x.Absent == y.Absent;
        }
        Check(same, "sale terms: every field of every deal comes back from the save, text with its odd characters too");
        Check(SaleTermStore.Write(new TermFile()) == null && SaleTermStore.Write(null) == null, "sale terms: a file with no deal writes no line");
        Check(SaleTermStore.Load(new Dictionary<string, string>(), out _) == null, "sale terms: a save from before has no line and so no deal");
        Check(SaleTermStore.Write(back) == line, "sale terms: written again it is the same line");
        var hostile = new Dictionary<string, string> { { SaleTermStore.Key, "v=1~h:1|zzz~d:1|99|99|x" } };
        TermFile odd = SaleTermStore.Load(hostile, out _);
        Check(odd == null || odd.Deals.Count <= 1, "sale terms: a line that is not the format is read without throwing");
        var many = new TermFile { Seed = 1 };
        for (int i = 0; i < SaleTermCatalogue.DealsMost + 10; i++)
        {
            TermDeal x = TermBegin(many, TermPlanOf(TermKind.Slice, 0), TermKind.Slice);
            x.State = i < 20 ? TermState.Done : TermState.Open;
        }
        Check(many.Deals.Count <= SaleTermCatalogue.DealsMost + 10 && SaleTermStore.Load(new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(many) } }, out _).Deals.Count <= SaleTermCatalogue.DealsMost,
            "sale terms: the save keeps at most " + SaleTermCatalogue.DealsMost + " deals, the oldest finished one going first");
    }

    private static void SaleTermSourceChecks()
    {
        string deal = ModSource(@"Tech\TechDirector.SaleTerms.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), kit = ModSource(@"Ui\Tech\TechLive.Exits.cs"), old = ModSource(@"Tech\Venture\TechApp.Exits.cs");
        Check(deal.Contains("SaleTermRules.Day(") && exits.Contains("TermsDay(day)") && exits.Contains("ExitAccept(string place, int offerId, TermChoice choice"),
            "sale terms: the day runs the deals, and the sale takes the choice");
        Check(kit.Contains("SaleTermText.ConfirmBody") && old.Contains("SaleTermText.ConfirmBody") && kit.Contains("SaleTermText.Lines") && old.Contains("SaleTermText.Lines"),
            "sale terms: both interfaces show every figure of the terms on the page and again in the game's box");
        Check(kit.Contains("TermResign") && old.Contains("TermResign") && ModSource(@"Ui\Tech\TechLive.Deals.cs").Contains("DealRows") && ModSource(@"Tech\Venture\TechApp.Venture.cs").Contains("WriteDealRows"),
            "sale terms: both interfaces list the deals with the startups held and let a contract be resigned");
        Check(exits.Contains("Give(Level(atOnce)") && exits.Contains("terms.Now") && exits.Contains("TermsBegin("), "sale terms: only what is paid at once reaches the wallet at the sale; the rest is the deal's");
        Check(exits.Contains("boss ? null : ExitBridges.WhyNotHand") && exits.Contains("!boss && (ExitBridges.HandOver == null"), "sale terms: a contract is made while the player stands inside the firm, and hands nothing over at the sale");
        Check(deal.Contains("TermHand(d, firm)") && deal.Contains("ExitBridges.WhyNotHand(d.Place)") && deal.Contains("d.Handed = true"), "sale terms: a contract's end hands the business over as a sale does, tried again each day until the game allows it");
        Check(deal.Contains("VentureGame.TermSalaryKey") && deal.Contains("VentureGame.TermRemitKey") && deal.Contains("VentureGame.TermCoverKey") && deal.Contains("VentureGame.TermEarnKey")
              && deal.Contains("VentureGame.TermDividendKey") && deal.Contains("VentureGame.TermBuyoutKey") && deal.Contains("VentureGame.TermBonusKey"), "sale terms: every payment is a bank line with a key of its own");
        Check(deal.Contains("Contracted(string place)") && exits.Contains("Contracted(place)") && ModSource(@"Tech\TechDirector.Deals.cs").Contains("!Contracted(place)"),
            "sale terms: no one bids for a firm a contract holds, and it raises no round");
        string services = ModSource(@"Economy\Services\ServicesDirector.cs") + ModSource(@"Economy\Services\ServicesDirector.Sale.cs");
        Check(services.Contains("reallife_svc_refuse_contract") && services.Contains("ServicesBridges.Contracted") && services.Contains("CanHandOver() =>") && services.Contains("!GoingPublic.InSession"),
            "sale terms: the sale of businesses refuses a firm a contract holds, a takeover leaves it be, and nothing is handed over in a Going Public session");
        string links = ModSource(@"Wiring\Wiring.ExitLinks.cs");
        Check(links.Contains("ExitBridges.ShareQuote = ShareQuote") && links.Contains("ExitBridges.ShareGrant = ShareGrant") && links.Contains("ExitBridges.HandOverReady = HandOverReady") && links.Contains("ServicesBridges.Contracted = null"),
            "sale terms: the bridges are put in and taken out with the exits'");
        string market = ModSource(@"Markets\MarketTrading.cs") + ModSource(@"Markets\MarketEngine.Orders.cs") + ModSource(@"Markets\MarketEngine.Invest.cs");
        Check(market.Contains("OrderWhy.Locked") && ModSource(@"Markets\MarketApp.Portfolio.cs").Contains("reallife_mkt_inv_mark_locked") && ModSource(@"Ui\Stocks\StocksBuild.cs").Contains("reallife_stk_locked_sub"),
            "sale terms: the market refuses a locked sale at the order, at the fill and for a worked order, and both portfolios show the lock");
        Check(ModSource(@"Markets\MarketDirector.Grants.cs").Contains("FirmsOpen") && ModSource(@"Markets\MarketEngine.Grants.cs").Contains("Locks.Add"),
            "sale terms: shares are paid into the portfolio of the player who owns the save only, never a guest's");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("reallife_terms_test_year") && deal.Contains("TestTermsYear"), "sale terms: the options page has the test that moves the deals a year on");
        Check(!deal.Contains("Update(") && !deal.Contains("LateUpdate(") && !deal.Contains("FindObjectsByType"), "sale terms: nothing per frame");
        string lines = ModSource(@"Economy\BizLines.cs");
        foreach (string key in new[] { "salary", "bonus", "dividend", "buyout", "earn_out", "remit", "cover", "dividend_tax" })
        {
            Check(lines.Contains("\"tech_terms_" + key + "\""), "sale terms: the bank line tech_terms_" + key + " is in a list of BizLines");
        }
    }

    private static void SaleTermTextChecks()
    {
        var keys = new List<string>();
        string text = ModSource(@"Tech\Venture\SaleTermText.cs");
        foreach (string group in new[] { "kind_cash", "kind_shares", "kind_slice", "kind_earn", "kind_boss", "share_0", "share_1", "share_2", "slice_0", "slice_1", "slice_2", "target_0", "target_1", "target_2", "measure_0",
            "measure_1", "years_0", "years_1" })
        {
            string k = "reallife_terms_" + group;
            keys.Add(k);
            Check(text.Contains("\"" + k + "\""), "sale terms: the code names the key " + k);
        }
        keys.AddRange(new[]
        {
            "reallife_terms_target_takings", "reallife_terms_target_customers", "reallife_ui_tech_terms_kind", "reallife_ui_tech_terms_size", "reallife_ui_tech_terms_term", "reallife_ui_tech_terms_target",
            "reallife_ui_tech_terms_measure", "reallife_ui_tech_terms_contract", "reallife_ui_tech_terms_deal", "reallife_terms_line_now", "reallife_terms_line_shares", "reallife_terms_line_slice",
            "reallife_terms_line_earn", "reallife_terms_line_boss", "reallife_terms_line_boss_shares", "reallife_terms_line_boss_bonus", "reallife_terms_line_boss_rules", "reallife_terms_confirm_body",
            "reallife_terms_confirm_resign_title", "reallife_terms_confirm_resign_body", "reallife_terms_button_resign", SaleTermRules.WhyNone, SaleTermRules.WhyShares, SaleTermRules.WhySmall,
            SaleTermRules.WhyMeasure, SaleTermRules.WhyOpen, SaleTermRules.WhyBoss, "reallife_svc_refuse_contract", "reallife_terms_deal_slice", "reallife_terms_deal_head", "reallife_terms_deal_shares",
            "reallife_terms_deal_shares_free", "reallife_terms_deal_stake", "reallife_terms_deal_firm_now", "reallife_terms_deal_earn", "reallife_terms_deal_boss", "reallife_terms_deal_over_done",
            "reallife_terms_deal_over_resigned", "reallife_terms_deal_over_dismissed", "reallife_terms_deal_over_gone", "reallife_terms_deal_over_lapsed", "reallife_terms_row_sub", "reallife_terms_row_day",
            "reallife_terms_row_locked", "reallife_terms_row_paying", "reallife_terms_row_target", "reallife_terms_row_contract", "reallife_terms_note_shares", "reallife_terms_note_slice",
            "reallife_terms_note_earn", "reallife_terms_note_boss", "reallife_terms_note_slice_end", "reallife_terms_note_earn_paid", "reallife_terms_note_earn_lapsed", "reallife_terms_note_shares_free",
            "reallife_terms_note_vest_shares", "reallife_terms_note_vest_cash", "reallife_terms_note_strike", "reallife_terms_note_dismissed", "reallife_terms_note_resigned", "reallife_terms_note_ended",
            "reallife_terms_note_gone", "reallife_terms_note_handed", "reallife_terms_test_none", "reallife_terms_test_year", "reallife_mkt_why_locked", "reallife_mkt_inv_mark_locked", "reallife_stk_locked_sub", "reallife_terms_deduct"
        });
        foreach (string k in new[] { VentureGame_Keys("salary"), VentureGame_Keys("bonus"), VentureGame_Keys("dividend"), VentureGame_Keys("buyout"), VentureGame_Keys("earn_out"), VentureGame_Keys("remit"), VentureGame_Keys("cover"), VentureGame_Keys("dividend_tax") })
        {
            keys.Add(k);
            keys.Add(k + "_label");
        }
        Dictionary<string, string> en = LocaleFile("en");
        bool all = true, plain = true;
        var seen = new HashSet<string>();
        foreach (string k in keys)
        {
            bool has = en.TryGetValue(k, out string t) && !string.IsNullOrWhiteSpace(t);
            all &= has;
            plain &= has && !t.Contains("!") && !t.Contains("—") && !t.Contains("–") && !t.Contains(" - ") && (!k.StartsWith("reallife_terms_note_") || t.Length <= 240) && seen.Add(k);
            if (!has)
            {
                Check(false, "sale terms: the text " + k + " is missing in en");
            }
        }
        Check(all && plain, "sale terms: every text is in en, plain, with no dash or exclamation mark, and every notice within 240 characters (" + keys.Count + " keys)");
        string next = WhatsNewText(en, "reallife_changes_next");
        Check(next.Contains("Selling a tech firm now has terms") && !next.Contains("Fixed: Selling a tech firm"), "sale terms: the What's new line says it, as a feature and not a fix");
        string guide = en["reallife_guide_tech_startups"];
        Check(guide.Contains("# Selling a firm on terms") && guide.Contains("{terms_lock}") && guide.Contains("{terms_boss_years}") && guide.Contains("<b>Stay as the boss</b>") && guide.Contains("<b>Keep a slice</b>"),
            "sale terms: the Guide's startup page has the terms, with its figures filled from the code");
        foreach (string fact in new[] { "terms_lock", "terms_slices", "terms_slice_years", "terms_minority", "terms_earn_share", "terms_earn_days", "terms_boss_years", "terms_review", "terms_keep", "terms_most" })
        {
            Check(ModSource(@"Tech\TechGuide.cs").Contains("Fact(\"" + fact + "\""), "sale terms: the Guide fact {" + fact + "} is filled from the code");
        }
        // every placeholder of the lines a page builds is given by the code that fills it
        foreach (string k in new[] { "reallife_terms_line_now", "reallife_terms_line_shares", "reallife_terms_line_slice", "reallife_terms_line_earn", "reallife_terms_line_boss", "reallife_terms_line_boss_rules" })
        {
            bool given = true;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(en[k], @"\{(\w+)\}"))
            {
                int at = text.IndexOf("\"" + k + "\"", StringComparison.Ordinal);
                given &= at >= 0 && text.IndexOf("\"" + m.Groups[1].Value + "\"", at, StringComparison.Ordinal) >= 0;
            }
            Check(given, "sale terms: the code gives every placeholder of " + k);
        }
    }

    private static string VentureGame_Keys(string name) => "reallife_transaction_tech_terms_" + name;
}
