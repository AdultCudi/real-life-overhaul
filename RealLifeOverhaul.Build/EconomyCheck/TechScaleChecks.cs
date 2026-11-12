using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using BbCat = RealLifeOverhaul.Utilities.BroadbandCatalogue;
using BbNetwork = RealLifeOverhaul.Utilities.Network;
using BbRules = RealLifeOverhaul.Utilities.BroadbandRules;

// The tech sector's money scale (Tech/Scale: ScaleCatalogue, ScaleRules; plans/tech-overhaul DRAFT2 section 1): budgets by
// scope, the audience to sales relation and the bands of return it gives at every scope's budgets, the stores' fees, a
// licensed engine, marketing's curve and its inverse, one shared market per kind, the payback targets; a studio over ten
// years at random quality against a hyperscaler and a national bank for the same capital, its average in the end game's
// band and its spread wider; a shop, a chain, the studios and the end game in their order, nothing outrunning the next;
// the one month rule for monthly bills (broadband by the save's month against tech's 30 days) and a broadband network at
// scale under it. Run with "scale" on the command line it prints the bands and the sims.
internal static partial class Program
{
    private static void TechScale()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool print = Array.IndexOf(Environment.GetCommandLineArgs(), "scale") >= 0;
        ScaleCatalogueShape();
        ScaleAudienceToSales();
        ScaleBands(print);
        ScaleCosts();
        ScaleOneMarket();
        ScaleGrown();
        ScaleStudioAgainstEndGame(print);
        ScaleMonthRule(print);
        ScaleTimings();
        Console.WriteLine("  Tech money scale: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the catalogue

    private static void ScaleCatalogueShape()
    {
        bool ordered = true;
        for (int s = 0; s < (int)Scope.Count; s++)
        {
            ordered &= ScaleCatalogue.BudgetLeast[s] < ScaleCatalogue.BudgetTypical[s] && ScaleCatalogue.BudgetTypical[s] < ScaleCatalogue.BudgetMost[s];
            ordered &= s == 0 || ScaleCatalogue.BudgetLeast[s] > ScaleCatalogue.BudgetMost[s - 1];
        }
        Check(ordered && ScaleCatalogue.BudgetLeast[0] == 5000000L && ScaleCatalogue.BudgetMost[0] == 50000000L && ScaleCatalogue.BudgetLeast[1] == 100000000L
              && ScaleCatalogue.BudgetMost[1] == 1000000000L && ScaleCatalogue.BudgetLeast[2] == 2000000000L && ScaleCatalogue.BudgetMost[2] == 15000000000L,
            "tech scale: budgets 50 thousand to 500 thousand, 1 to 10 million and 20 to 150 million, each scope's typical inside, none overlapping");
        Check(ScaleCatalogue.FlopLeast == 100 && ScaleCatalogue.FlopMost == 500 && ScaleCatalogue.AverageLeast == 1000 && ScaleCatalogue.AverageMost == 1500
              && ScaleCatalogue.HitLeast == 3000 && ScaleCatalogue.HitMost == 10000, "tech scale: a flop 0.1 to 0.5 of its budget, an average 1 to 1.5, a hit 3 to 10");
        bool tables = ScaleCatalogue.Conversion.Length == 11 && ScaleCatalogue.Tail.Length == 11 && ScaleCatalogue.PlatformFee.Length == (int)Platform.Count
                      && ScaleCatalogue.PaybackLeast.Length == (int)PaybackKind.Count && ScaleCatalogue.PaybackMost.Length == (int)PaybackKind.Count
                      && ScaleCatalogue.PeoplePerMoney.Length == (int)Scope.Count && ScaleCatalogue.DevDays.Length == (int)Scope.Count && ScaleCatalogue.TeamQuality.Length == (int)Scope.Count
                      && ScaleCatalogue.MarketingReachStep.Length == ScaleCatalogue.MarketingStepsMost + 1;
        bool rising = true;
        for (int i = 1; i < 11; i++)
        {
            rising &= ScaleCatalogue.Conversion[i] >= ScaleCatalogue.Conversion[i - 1] && ScaleCatalogue.Tail[i] > ScaleCatalogue.Tail[i - 1];
        }
        Check(tables && rising, "tech scale: every table is as long as what it is read by, and a better product converts and lasts no worse");
        bool targets = true;
        for (int k = 0; k < (int)PaybackKind.Count; k++)
        {
            targets &= ScaleCatalogue.PaybackLeast[k] > 0 && ScaleCatalogue.PaybackLeast[k] < ScaleCatalogue.PaybackMost[k];
        }
        Check(targets && ScaleCatalogue.PaybackLeast[(int)PaybackKind.Infrastructure] == EndScaleRules.PaybackDaysLeast && ScaleCatalogue.PaybackMost[(int)PaybackKind.Infrastructure] == EndScaleRules.PaybackDaysMost
              && ScaleCatalogue.PaybackLeast[(int)PaybackKind.Network] == EndScaleRules.PaybackYearsLeast * 365 && ScaleCatalogue.PaybackMost[(int)PaybackKind.Network] == EndScaleRules.PaybackYearsMost * 365
              && ScaleCatalogue.PaybackLeast[(int)PaybackKind.GameStudio] == EndScaleRules.PaybackDaysLeast && ScaleCatalogue.PaybackMost[(int)PaybackKind.GameStudio] == EndScaleRules.PaybackDaysMost,
            "tech scale: the payback targets are the end game's own for infrastructure, networks and a studio, each a range");
        Check(ScaleRules.OnTarget(PaybackKind.GameStudio, 200) && !ScaleRules.OnTarget(PaybackKind.GameStudio, 400) && !ScaleRules.OnTarget(PaybackKind.Shop, 200)
              && ScaleRules.OnTarget(PaybackKind.Shop, 30) && ScaleRules.OnTarget((PaybackKind)99, 1000) == ScaleRules.OnTarget(PaybackKind.Network, 1000),
            "tech scale: a payback is on its kind's target only inside it; a kind out of range reads as the last");
        Check(ScaleRules.ScopeOf(0) == Scope.Indie && ScaleRules.ScopeOf(4999999) == Scope.Indie && ScaleRules.ScopeOf(99999999) == Scope.Indie && ScaleRules.ScopeOf(100000000) == Scope.Mid
              && ScaleRules.ScopeOf(1999999999) == Scope.Mid && ScaleRules.ScopeOf(2000000000) == Scope.Big && ScaleRules.ScopeOf(long.MaxValue / 4) == Scope.Big,
            "tech scale: a budget's scope by the floors, under the indie floor still indie, past the big most still big");
        Check(ScaleRules.Budget(120000, 90, 500000, 4000000, 1000000) == 120000L * 90 + 5500000 && ScaleRules.Budget(-5, -1, -1, -1, -1) == 0
              && ScaleRules.Budget(40000000, 300, 0, 0, 0) == 12000000000L,
            "tech scale: a budget is the team's pay over its days with licences, marketing and launch, never negative, in longs past two billion");
    }

    // ---------------------------------------------------------------- from the audience to sales

    private static void ScaleAudienceToSales()
    {
        long people = 1000000;
        long half = people * ScaleCatalogue.ReachHalfCents;
        int none = ScaleRules.Reach(0, people), atHalf = ScaleRules.Reach(half, people), huge = ScaleRules.Reach(half * 1000, people);
        Check(none == ScaleCatalogue.ReachFloor && atHalf == (ScaleCatalogue.ReachFloor + ScaleCatalogue.ReachMost) / 2 + (ScaleCatalogue.ReachMost - ScaleCatalogue.ReachFloor) % 2 / 2
              && huge < ScaleCatalogue.ReachMost && huge > ScaleCatalogue.ReachMost - 2 && ScaleRules.Reach(5000, 0) == 0 && ScaleRules.Reach(-50, people) == ScaleCatalogue.ReachFloor,
            "tech scale: marketing reaches the floor with none, half way at its half cost a person, never the most however much is spent (" + none + ", " + atHalf + ", " + huge + ")");
        bool inverse = true, monotone = true;
        int last = -1;
        for (int r = ScaleCatalogue.ReachFloor + 1; r < ScaleCatalogue.ReachMost; r += 7)
        {
            long spend = ScaleRules.SpendFor(r, people);
            int back = ScaleRules.Reach(spend, people);
            inverse &= back >= r && back <= r + 1;
            monotone &= back >= last;
            last = back;
        }
        Check(inverse && monotone && ScaleRules.SpendFor(ScaleCatalogue.ReachFloor, people) == 0 && ScaleRules.SpendFor(ScaleCatalogue.ReachMost, people) == long.MaxValue / 4
              && ScaleRules.StepSpend(0, people) == 0 && ScaleRules.StepSpend(9, people) == ScaleRules.StepSpend(ScaleCatalogue.MarketingStepsMost, people)
              && ScaleRules.StepSpend(2, people) > ScaleRules.StepSpend(1, people),
            "tech scale: the spend for a reach gives that reach back, rising with it; none at the floor, never the most; a plan's steps cost more each");
        long billion = 1000000000L;
        Check(ScaleRules.Reach(15000000000L, billion) > ScaleCatalogue.ReachFloor && ScaleRules.Reach(15000000000L, billion) < ScaleCatalogue.ReachMost,
            "tech scale: an audience of a billion and a spend of 150 million stay inside whole numbers");
        bool better = true;
        long prev = -1;
        for (int q = 0; q <= 100; q += 5)
        {
            long u = ScaleRules.Units(people, 500, q, 1000, 1000);
            better &= u >= prev && u <= people;
            prev = u;
        }
        Check(better, "tech scale: a better product never sells fewer copies to the same people, and never more than them");
        Check(ScaleRules.Units(people, 1000, 100, 1000000, 2000) == people && ScaleRules.Units(0, 800, 90, 1000, 1000) == 0 && ScaleRules.Units(people, 800, 90, 0, 1000) == 0,
            "tech scale: no cap but the audience: a product everybody hears of and wants sells to them all and no more; no audience or no fit, no sales");
        Check(ScaleRules.PriceAppeal(1500, 1500) == 1000 && ScaleRules.PriceAppeal(3000, 1500) < 500 && ScaleRules.PriceAppeal(750, 1500) > 2000 - 1
              && ScaleRules.PriceAppeal(100000, 1500) == ScaleCatalogue.PriceAppealLeast && ScaleRules.PriceAppeal(1, 1500) == ScaleCatalogue.PriceAppealMost && ScaleRules.PriceAppeal(0, 1500) == 1000,
            "tech scale: the usual price sells as usual; double it and fewer than half buy; halve it and twice as many; held within the least and most");
        long gross3000 = 3000 * ScaleRules.Units(people, 600, 60, 1000, ScaleRules.PriceAppeal(3000, 1500)), gross1500 = 1500 * ScaleRules.Units(people, 600, 60, 1000, 1000);
        Check(gross3000 < gross1500, "tech scale: doubling a product's price takes in less than the usual price (" + gross3000 + " against " + gross1500 + ")");
        long units = 2000000;
        Check(ScaleRules.LaunchUnits(units, 0) * ScaleCatalogue.Tail[0] / 1000 <= units && ScaleRules.LaunchUnits(units, 100) == units * 1000 / ScaleCatalogue.Tail[10]
              && ScaleRules.HalfLife(0) == ScaleCatalogue.HalfLifeBase && ScaleRules.HalfLife(100) > ScaleRules.HalfLife(50) && ScaleRules.HalfLife(500) == ScaleRules.HalfLife(100),
            "tech scale: the launch window's share is the life over the tail; a better product's sales take longer to halve");
        long left = 1000000, sold = 0;
        int days = 0;
        while (left > 500000 && days < 1000)
        {
            long today = ScaleRules.TailDay(left, 70);
            left -= today;
            sold += today;
            days++;
        }
        Check(days >= 68 && days <= 72 && ScaleRules.TailDay(0, 70) == 0 && ScaleRules.TailDay(3, 70) == 1 && ScaleRules.TailDay(5, 0) <= 5,
            "tech scale: what is left to sell halves over the half-life (" + days + " days for 70), a cent at least while anything is left");
    }

    // ---------------------------------------------------------------- the bands at every scope's budgets

    private static void ScaleBands(bool print)
    {
        long[][] budgets =
        {
            new[] { ScaleCatalogue.BudgetLeast[0], ScaleCatalogue.BudgetTypical[0], ScaleCatalogue.BudgetMost[0] },
            new[] { ScaleCatalogue.BudgetLeast[1], ScaleCatalogue.BudgetTypical[1], ScaleCatalogue.BudgetMost[1] },
            new[] { ScaleCatalogue.BudgetLeast[2], ScaleCatalogue.BudgetTypical[2], ScaleCatalogue.BudgetMost[2] }
        };
        bool flops = true, averages = true, hits = true, masters = true, rising = true;
        string worst = "none";
        for (int s = 0; s < 3; s++)
        {
            for (int b = 0; b < 3; b++)
            {
                long budget = budgets[s][b];
                int last = -1;
                for (int q = 0; q <= 100; q++)
                {
                    ScaleRules.Typical(budget, q, out _, out int ret);
                    rising &= ret >= last;
                    last = ret;
                    ReturnBand band = ScaleRules.BandOf(ret);
                    bool ok = true;
                    if (q <= 35)
                    {
                        ok = ret >= ScaleCatalogue.FlopLeast && ret <= ScaleCatalogue.FlopMost && band == ReturnBand.Flop;
                        flops &= ok;
                    }
                    else if (q >= 50 && q <= 60)
                    {
                        ok = ret >= ScaleCatalogue.AverageLeast && ret <= ScaleCatalogue.AverageMost && band == ReturnBand.Average;
                        averages &= ok;
                    }
                    else if (q >= 80 && q <= 92)
                    {
                        ok = ret >= ScaleCatalogue.HitLeast && ret <= ScaleCatalogue.HitMost && band == ReturnBand.Hit;
                        hits &= ok;
                    }
                    else if (q >= 96)
                    {
                        ok = ret > ScaleCatalogue.HitMost && band == ReturnBand.Masterpiece;
                        masters &= ok;
                    }
                    if (!ok && worst == "none")
                    {
                        worst = "scope " + s + ", budget " + budget / 100 + ", quality " + q + ": " + ret;
                    }
                }
            }
        }
        Check(flops, "tech scale: a poor product (quality 35 or less) returns 0.1 to 0.5 of its budget, at every scope's least, typical and most budget (" + worst + ")");
        Check(averages, "tech scale: an average product (quality 50 to 60) returns 1 to 1.5 of its budget at every scope's budgets (" + worst + ")");
        Check(hits, "tech scale: a hit (quality 80 to 92) returns 3 to 10 times its budget at every scope's budgets (" + worst + ")");
        Check(masters && rising, "tech scale: a masterpiece (96 and up) returns more than ten times, with no cap but the audience; a better product never returns less (" + worst + ")");
        Check(ScaleRules.BandOf(99) == ReturnBand.Flop && ScaleRules.BandOf(500) == ReturnBand.Flop && ScaleRules.BandOf(501) == ReturnBand.Poor && ScaleRules.BandOf(1000) == ReturnBand.Average
              && ScaleRules.BandOf(1501) == ReturnBand.Good && ScaleRules.BandOf(3000) == ReturnBand.Hit && ScaleRules.BandOf(10000) == ReturnBand.Hit && ScaleRules.BandOf(10001) == ReturnBand.Masterpiece
              && ScaleRules.Return(500, 1000) == 500 && ScaleRules.Return(5, 0) == 0 && ScaleRules.Return(-5, 100) == 0,
            "tech scale: the bands' edges, and a return over no budget is nothing");
        long audience = ScaleRules.TypicalAudience(ScaleCatalogue.BudgetTypical[2]);
        Check(audience > 100000000L && audience < 1000000000L && ScaleRules.TypicalAudience(ScaleCatalogue.BudgetTypical[0]) < 20000000L && ScaleRules.TypicalAudience(-5) == 0,
            "tech scale: a big game's usual audience is hundreds of millions in the city and the world, an indie one's a few million (" + audience + ")");
        if (print)
        {
            Console.WriteLine("TECH SCALE: return (x budget) by quality at each scope's typical budget");
            for (int s = 0; s < 3; s++)
            {
                var line = new System.Text.StringBuilder("  " + (Scope)s + " $" + (ScaleCatalogue.BudgetTypical[s] / 100).ToString("N0") + ":");
                for (int q = 0; q <= 100; q += 10)
                {
                    ScaleRules.Typical(ScaleCatalogue.BudgetTypical[s], q, out long u, out int ret);
                    line.Append(" q" + q + " " + (ret / 1000.0).ToString("0.00") + " (" + u.ToString("N0") + ")");
                }
                Console.WriteLine(line.ToString());
            }
        }
    }

    // ---------------------------------------------------------------- the real costs

    private static void ScaleCosts()
    {
        long gross = 1000000000L;   // ten million in sales
        long pc = ScaleRules.Net(gross, Platform.Pc, false), direct = ScaleRules.Net(gross, Platform.Direct, false), licensed = ScaleRules.Net(gross, Platform.Pc, true);
        Check(pc == gross * (1000 - 300 - ScaleCatalogue.Refunds) / 1000 && direct > pc && licensed == pc - (gross - ScaleCatalogue.EngineRoyaltyFree) * ScaleCatalogue.EngineRoyalty / 1000,
            "tech scale: the store takes 30% and refunds 4%; selling direct keeps more; a licensed engine takes 5% past its free first million");
        Check(ScaleRules.Net(ScaleCatalogue.EngineRoyaltyFree, Platform.Pc, true) == ScaleRules.Net(ScaleCatalogue.EngineRoyaltyFree, Platform.Pc, false) && ScaleRules.Net(-5, Platform.Console, true) == 0
              && ScaleRules.Net(100, (Platform)42, false) == ScaleRules.Net(100, Platform.Direct, false) && ScaleRules.Servers(1000) == 1000L * ScaleCatalogue.ServerCentsPerPlayerMonth && ScaleRules.Servers(-1) == 0,
            "tech scale: under a million in sales no royalty; nothing below nothing; a platform out of range reads as the last; servers by the players still playing");
        Check(ScaleCatalogue.OwnEngineCost > 0 && ScaleCatalogue.OwnEngineCost < ScaleCatalogue.BudgetMost[0] * 2, "tech scale: an engine of one's own costs about an indie budget, once");
    }

    // ---------------------------------------------------------------- one market per kind

    private static void ScaleOneMarket()
    {
        long[] appeals = { 900, 600, 400, 250, 100 };
        long all = 0;
        for (int i = 0; i < appeals.Length; i++)
        {
            all += appeals[i];
        }
        int sum = 0;
        for (int i = 0; i < appeals.Length; i++)
        {
            sum += ScaleRules.Share(appeals[i], all);
        }
        int alone = ScaleRules.Share(900, 900), withRival = ScaleRules.Share(900, 900 + 900);
        Check(sum <= 1000 && sum > 700 && alone > withRival && withRival * 2 <= 1000 && ScaleRules.Share(0, 500) == 0 && ScaleRules.Share(500, 0) == ScaleRules.Share(500, 500),
            "tech scale: products of a kind share one pool: their shares never pass the whole, a rival takes from the same people (" + sum + ", " + alone + " alone, " + withRival + " with an equal rival)");
    }

    // ---------------------------------------------------------------- a studio over ten years against the end game

    private struct StudioRun
    {
        public long Cash, Capital;   // cents: the cash at the end, the most it was down
        public int Games;
        public int RatePpm;          // a day's profit over the capital, millionths
    }

    // A studio making products of a scope back to back with an ordinary team for SimDays: each budget spent evenly over its
    // making, each product's quality rolled about the team's, its money as the rules sell it (the launch window, then a
    // tail halving by its quality). The capital is the most cash it ever needed.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static StudioRun Studio(Scope scope, long budget, int seed, int days)
    {
        int dev = ScaleCatalogue.DevDays[(int)scope], mean = ScaleCatalogue.TeamQuality[(int)scope];
        int games = days / dev;
        var launch = new long[games];
        var left = new long[games];
        var half = new int[games];
        for (int g = 0; g < games; g++)
        {
            int q = ScaleRules.QualityRoll(seed, g, mean);
            long net = ScaleRules.Typical(budget, q, out _, out _);
            launch[g] = net * 1000 / ScaleRules.ByQuality(ScaleCatalogue.Tail, q);
            left[g] = net - launch[g];
            half[g] = ScaleRules.HalfLife(q);
        }
        long cash = 0, low = 0, spend = budget / dev;
        for (int d = 0; d < days; d++)
        {
            if (d < games * dev)
            {
                cash -= spend;
            }
            for (int g = 0; g < games; g++)
            {
                int out_ = (g + 1) * dev;
                if (d < out_)
                {
                    break;
                }
                if (d - out_ < ScaleCatalogue.LaunchDays)
                {
                    cash += launch[g] / ScaleCatalogue.LaunchDays;
                }
                long t = ScaleRules.TailDay(left[g], half[g]);
                left[g] -= t;
                cash += t;
            }
            low = Math.Min(low, cash);
        }
        long capital = Math.Max(1L, -low);
        return new StudioRun { Cash = cash, Capital = capital, Games = games, RatePpm = (int)(cash * 1000000L / capital / days) };
    }

    private static void ScaleStudioAgainstEndGame(bool print)
    {
        const int Seeds = 60;
        int days = ScaleCatalogue.SimDays;
        // the end game for the same capital: a hyperscaler and a cloud at scale, the national bank at scale, a day's net over what was put in
        ScaleRun h3 = CloudSim(EndScaleRules.Full, 400, 24, 3, 2, true, 3000), cloud = CloudSim(EndScaleRules.Full, 120, 10, 1, 2, false, 3000);
        ScaleRun bank = BankSim(EndScaleRules.Full, 0, 3000, out _, out _);
        int hyperPpm = (int)(h3.Net * 1000000L / Math.Max(1L, h3.Capex)), cloudPpm = (int)(cloud.Net * 1000000L / Math.Max(1L, cloud.Capex)), bankPpm = (int)(bank.Net * 1000000L / Math.Max(1L, bank.Capex));
        int endLeast = Math.Min(hyperPpm, Math.Min(cloudPpm, bankPpm)), endMost = Math.Max(hyperPpm, Math.Max(cloudPpm, bankPpm));
        int bandLeast = 1000000 / EndScaleRules.PaybackDaysMost, bandMost = 1000000 / EndScaleRules.PaybackDaysLeast;   // the end game's payback aim as a day's return
        var day = new long[3];
        for (int s = 0; s < 3; s++)
        {
            var rates = new List<int>(Seeds);
            long rateSum = 0, profit = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                StudioRun r = Studio((Scope)s, ScaleCatalogue.BudgetTypical[s], seed * 7919 + s, days);
                rates.Add(r.RatePpm);
                rateSum += r.RatePpm;
                profit += r.Cash / days;
            }
            rates.Sort();
            int mean = (int)(rateSum / Seeds), p10 = rates[Seeds / 10], p90 = rates[Seeds * 9 / 10];
            day[s] = profit / Seeds / 100;   // whole money a day
            int payback = mean > 0 ? 1000000 / mean : -1;
            Check(mean >= bandLeast && mean <= bandMost && ScaleRules.OnTarget(PaybackKind.GameStudio, payback),
                "tech scale: a " + (Scope)s + " studio's average day over ten years returns " + mean + " millionths of its capital, a payback of " + payback + " days, inside the end game's aim of "
                + EndScaleRules.PaybackDaysLeast + " to " + EndScaleRules.PaybackDaysMost);
            Check(mean >= endLeast * 7 / 10 && mean <= endMost * 13 / 10,
                "tech scale: a " + (Scope)s + " studio earns about what the end game earns for the same capital (" + mean + " against the hyperscaler's " + hyperPpm + ", the cloud's " + cloudPpm + " and the bank's " + bankPpm + ")");
            Check(p90 - p10 > endMost - endLeast && p10 < endLeast && p90 > endMost,
                "tech scale: a " + (Scope)s + " studio's luck swings it wider than the end game's ventures differ: its tenth to ninetieth from " + p10 + " to " + p90 + ", the end game's " + endLeast + " to " + endMost);
            if (print)
            {
                Console.WriteLine("  studio " + (Scope)s + ": mean " + mean + " ppm a day (payback " + payback + " days), p10 " + p10 + ", p90 " + p90 + ", profit a day $" + day[s].ToString("N0"));
            }
        }
        if (print)
        {
            Console.WriteLine("  end game: hyperscaler " + hyperPpm + " ppm ($" + (h3.Net / 100).ToString("N0") + " a day), cloud " + cloudPpm + " ppm, bank " + bankPpm + " ppm ($" + (bank.Net / 100).ToString("N0") + " a day)");
        }
        // nothing outruns the rest: a shop, a chain, the studios, the end game, each a day's profit in its own class
        long hyperDay = h3.Net / 100;
        Check(day[0] > ScaleCatalogue.ShopDayLeast / 2 && day[0] < ScaleCatalogue.ChainDayLeast,
            "tech scale: an indie studio earns about a good shop a day, never a chain's (" + day[0] + " against " + ScaleCatalogue.ShopDayLeast + " to " + ScaleCatalogue.ShopDayMost + ")");
        Check(day[1] > ScaleCatalogue.ShopDayMost && day[1] < ScaleCatalogue.BankDay,
            "tech scale: a mid studio earns more than any shop and less than the end game's bank (" + day[1] + ")");
        Check(day[2] > ScaleCatalogue.ChainDayMost && day[2] <= Math.Max(hyperDay, ScaleCatalogue.BankDay) * 3 / 2,
            "tech scale: a big studio earns more than a strong chain and about the end game's ventures, not past them (" + day[2] + " against the hyperscaler's " + hyperDay + ")");
        StudioRun same = Studio(Scope.Mid, ScaleCatalogue.BudgetTypical[1], 77, days), again = Studio(Scope.Mid, ScaleCatalogue.BudgetTypical[1], 77, days);
        Check(same.Cash == again.Cash && same.Capital == again.Capital && same.Games == ScaleCatalogue.SimDays / ScaleCatalogue.DevDays[1],
            "tech scale: the same seed makes the same studio on every machine (" + same.Games + " products)");
        bool spread = true;
        int lowQ = 100, highQ = 0;
        for (int g = 0; g < 400; g++)
        {
            int q = ScaleRules.QualityRoll(3, g, 55);
            spread &= q >= 0 && q <= 100;
            lowQ = Math.Min(lowQ, q);
            highQ = Math.Max(highQ, q);
        }
        Check(spread && lowQ < 30 && highQ > 80 && ScaleRules.QualityRoll(3, 1, 55) == ScaleRules.QualityRoll(3, 1, 55) && ScaleRules.QualityRoll(3, 1, 200) == 100,
            "tech scale: a team's products fall about its mean, now and then a flop or a hit, always 0 to 100 (" + lowQ + " to " + highQ + ")");
    }

    // ---------------------------------------------------------------- the one month rule

    private static void ScaleMonthRule(bool print)
    {
        Check(CalendarRules.BillingMonthDays == 30 && FirmsCatalogue.MonthDays == CalendarRules.BillingMonthDays,
            "month rule: tech bills by the one 30-day month, from one constant");
        Check(CalendarRules.BroadbandBillingDays(false, 60) == 5 && CalendarRules.BroadbandBillingDays(false, 365) == 30 && CalendarRules.BroadbandBillingDays(true, 60) == 30
              && CalendarRules.BroadbandBillingDays(true, 365) == 30 && CalendarRules.BroadbandBillingDays(true, 8) == 30 && CalendarRules.BroadbandBillingDays(false, 8) == CalendarRules.MonthDays(8),
            "month rule: off, broadband bills by the save's month (5 days in a 60-day year); on, by the same 30 days as tech in every year");
        var n = BbNetworkAtScale();
        long monthCents = BbRules.AverageCents(n, 1000);
        int conns = BbRules.Connections(n);
        int off60 = BbRules.DayTakings(n, 1000, CalendarRules.BroadbandBillingDays(false, 60), false, true);
        int on60 = BbRules.DayTakings(n, 1000, CalendarRules.BroadbandBillingDays(true, 60), false, true);
        int on365 = BbRules.DayTakings(n, 1000, CalendarRules.BroadbandBillingDays(true, 365), false, true);
        int off365 = BbRules.DayTakings(n, 1000, CalendarRules.BroadbandBillingDays(false, 365), false, true);
        long tech = (long)conns * monthCents / FirmsCatalogue.MonthDays / 100;   // a tech subscription of the same price a month, the same customers, a day (whole money)
        Check(conns > 0 && on60 == on365 && on60 == off365 && Math.Abs(on60 - tech) <= 1 + tech / 1000 && off60 >= on60 * 59 / 10 && off60 <= on60 * 61 / 10,
            "month rule: on, a broadband connection earns what a tech subscription of the same monthly price does a day (" + on60 + " against " + tech + "), in any year; off, six times that in a 60-day year (" + off60 + ")");
        ScaleRun living = BroadbandBilled(60, 30, 6 * 365 + 10), before = BroadbandBilled(60, 5, 6 * 365 + 10);
        int least = ScaleCatalogue.PaybackLeast[(int)PaybackKind.Network], most = ScaleCatalogue.PaybackMost[(int)PaybackKind.Network];
        Check(living.Payback >= least && living.Payback <= most,
            "month rule: on, a broadband network over the whole city in a 60-day year pays back in " + least + " to " + most + " days, two to six real years (" + living.Payback + " days)");
        long livingDay = living.Net / 100;
        Check(livingDay > ScaleCatalogue.BankDay / 4 && livingDay < ScaleCatalogue.BroadbandDayLeast && before.Net / 100 > livingDay * 4,
            "month rule: on, broadband at scale earns about the national bank's day for its capital (" + livingDay + " a day), where before the rule it earned six times as much (" + before.Net / 100 + ")");
        if (print)
        {
            Console.WriteLine("  broadband at scale, 60-day year: billed by 30 days $" + livingDay.ToString("N0") + " a day, payback " + living.Payback + " days; by the save's month $" + (before.Net / 100).ToString("N0")
                              + " a day, payback " + before.Payback + " days");
        }
        string util = ModSource(@"Utilities\UtilitiesDirector.cs");
        string day = MethodText(util, "private void Day(int today)");
        Check(day.Contains("int billing = CalendarRules.BroadbandBillingDays(Settings.LivingPopulation, GameCalendar.DaysPerYear);") && day.Contains("_company.Day(d, level, billing);")
              && day.Contains("InternetRules.AddDay(a, billing,") && day.Contains("if (d + 1 - _state.MonthStart >= monthDays)") && !day.Contains("_company.Day(d, level, monthDays)"),
            "month rule sources: the broadband company's day and the internet plans bill by the rule under Living population; the month still turns on the save's month");
    }

    // A network in every district, open and trusted, a fifth of each district's market.
    private static BbNetwork BbNetworkAtScale()
    {
        var n = new BbNetwork();
        BbRules.Found(n, 0);
        n.Scale = EndScaleRules.Full;
        for (int d = 0; d < BbNetwork.Districts; d++)
        {
            BbRules.StartBuild(n, d, 0, 5, BbRules.BuildCost(d, 1000, EndScaleRules.Full), true);
        }
        BbRules.Finish(n, 999);
        for (int d = 0; d < BbNetwork.Districts; d++)
        {
            n.Share[d, BbNetwork.Own] = 200;
        }
        return n;
    }

    // The broadband company as EndScaleChecks' BroadbandSim runs it, at scale, its calendar month and its billing month apart:
    // the network dug and its market moving on the save's months, the money a day by the billing month.
    private static ScaleRun BroadbandBilled(int year, int billing, int days)
    {
        int m = RealLifeOverhaul.Economy.MacroCatalogue.MonthDays(year), share = EndScaleRules.Full, buy = EndScaleRules.Buying(true);
        var calm = new[] { 1000, 1000, 1000, 1000 };
        var n = new BbNetwork();
        BbRules.Found(n, 0);
        n.Scale = share;
        long capex = BbRules.LicenceFee(PriceRules.Start, buy);
        for (int d = 0; d < BbNetwork.Districts; d++)
        {
            int cost = BbRules.BuildCost(d, PriceRules.Start, buy);
            BbRules.StartBuild(n, d, 0, m, cost, true);
            capex += cost;
        }
        var run = new ScaleRun { Capex = capex * 100, Payback = -1 };
        long cash = -capex * 100;
        for (int day = 1; day <= days; day++)
        {
            BbRules.Finish(n, day);
            BbRules.Hire(n, day);
            long takings = BbRules.DayTakings(n, PriceRules.Start, billing, false, true);
            BbRules.DayCosts(n, PriceRules.Start, day, billing, out int staff, out int network, out int marketing);
            BbRules.DayCosts(n, PriceRules.Start, day, billing, out int staff0, out int network0, out _, 0);
            BbRules.DayCharge charged = BbRules.Charge(n, PlentyCash, takings, staff, network, marketing, staff0, network0);
            long costs = charged.Staff + charged.Network + marketing;
            if (day % m == 0)
            {
                int before = BbRules.Connections(n);
                BbRules.Month(n, calm, day, m);
                costs += BbRules.ChargeInstalls(n, PlentyCash, before, BbRules.Connections(n), PriceRules.Start, share);
                BbRules.CloseMonth(n, BbRules.Connections(n), 0);
            }
            cash += (takings - costs) * 100;
            if (run.Payback < 0 && cash >= 0)
            {
                run.Payback = day;
            }
            run.Takings = takings * 100;
            run.Net = (takings - costs) * 100;
            run.Crew = n.Crew[0] + n.Crew[1] + n.Crew[2];
        }
        return run;
    }

    // ---------------------------------------------------------------- what it costs

    private static void ScaleTimings()
    {
        ScaleWarm();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long sink = ScaleBurst(20000);
        double each = watch.Elapsed.TotalMilliseconds * 1000.0 / 20000;
        Check(each < 2.0 && sink != 0, "tech scale timing: a product's whole sale worked out (audience, reach, units, net, return) costs " + each.ToString("0.000") + " microseconds, under 2 (the studio's day may ask for hundreds)");
    }

    private static void ScaleWarm()
    {
        ScaleBurst(500);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long ScaleBurst(int count)
    {
        long sink = 0;
        for (int i = 0; i < count; i++)
        {
            sink += ScaleRules.Typical(ScaleCatalogue.BudgetTypical[i % 3], i % 101, out long u, out int r) + u + r;
        }
        return sink;
    }

    // A market grown by the save's years towards its ceiling (ScaleRules.Grown, the one clock).
    private static void ScaleGrown()
    {
        long one = ScaleRules.Grown(1000000, 50, 2000, 365, 365), half = ScaleRules.Grown(1000000, 50, 2000, 365 / 2, 365);
        long ten = ScaleRules.Grown(1000000, 50, 2000, 3650, 365), twenty = ScaleRules.Grown(1000000, 50, 2000, 7300, 365), full = ScaleRules.Grown(1000000, 50, 2000, 365 * 400, 365);
        Check(one == 1050000 && half > 1000000 && half < one && ScaleRules.Grown(1000000, 50, 2000, 60, 60) == one && ScaleRules.Grown(1000000, 50, 2000, 600, 60) == ten,
            "scale grown: a market grows its full rate in its first year of the save, straight within it, and a year or ten alike at 60 days a year and at 365");
        Check(ten > one && twenty - ten < ten - 1000000 && full > twenty && full <= 2000000 && ScaleRules.Grown(1000000, 50, 2000, int.MaxValue, 8) <= 2000000,
            "scale grown: it slows as it fills and never passes its ceiling, even after centuries (" + ten.ToString("N0") + " after ten years, " + full.ToString("N0") + " filled)");
        Check(ScaleRules.Grown(1000000, 50, 1000, 3650, 365) == 1000000 && ScaleRules.Grown(1000000, 50, 500, 3650, 365) == 1000000 && ScaleRules.Grown(1000000, 0, 2000, 3650, 365) == 1000000
              && ScaleRules.Grown(1000000, 50, 2000, 0, 365) == 1000000 && ScaleRules.Grown(1000000, 50, 2000, -5, 365) == 1000000 && ScaleRules.Grown(0, 50, 2000, 3650, 365) == 0
              && ScaleRules.Grown(1000000, 50, 2000, 3650, 0) == ScaleRules.Grown(1000000, 50, 2000, 3650, CalendarRules.YearLeast),
            "scale grown: no room, no rate, no days or nothing to grow leave a market as it is; a year shorter than the calendar allows is its shortest");
    }
}
