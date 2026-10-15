using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Markets;

// Conservative investments (Scripts/Markets/Bonds, Scripts/Markets/TrustRules): the property trusts, which pay every
// week or every third day (their schedule, how much, how often, a yield that is a fair one, no money from buying for a
// payment, prices that follow the property market), and the bonds (their yields and price, their payments with the
// share put back, their end, a sale before it, a deposit closed early, every refusal, the same on every machine, the
// save's line and a save from before them). Every expected figure is worked out from the catalogue or by hand.
internal static partial class Program
{
    private static void MarketIncome()
    {
        TrustCatalogueChecks();
        TrustScheduleChecks();
        TrustPayoutChecks();
        TrustFairChecks();
        TrustPropertyChecks();
        TrustJoinChecks();
        BondYieldChecks();
        BondPaymentChecks();
        BondSaleChecks();
        BondRefusalChecks();
        BondSameEverywhere();
        BondStoreChecks();
        BondCost();
        IncomeWords();
    }

    private static List<Stock> Trusts(MarketEngine m)
    {
        return m.State.Stocks.Where(s => s.Listed && TrustRules.Is(s)).ToList();
    }

    // ---------------------------------------------------------------- the property trusts

    private static void TrustCatalogueChecks()
    {
        CompanyDef[] trusts = MarketCatalogue.All.Where(d => d.PayDays > 0).ToArray();
        Check(trusts.Length >= 4 && trusts.All(d => d.Sector == MarketSector.Trusts && !d.Pipeline && d.Trade == null),
            "trusts: four property trusts at least, every one in its own sector, listed from the start and in no trade of the player's (" + trusts.Length + ")");
        Check(trusts.All(d => d.PayDays == MarketCatalogue.TrustWeekly || d.PayDays == MarketCatalogue.TrustThreeDays)
              && trusts.Any(d => d.PayDays == MarketCatalogue.TrustWeekly) && trusts.Any(d => d.PayDays == MarketCatalogue.TrustThreeDays),
            "trusts: each pays every week or every third day, and both kinds are listed");
        Check(MarketCatalogue.All.Where(d => d.Sector == MarketSector.Trusts).All(d => d.PayDays > 0) && MarketCatalogue.All.Where(d => d.PayDays > 0).All(d => d.Id > 136 && d.Id < 200),
            "trusts: every company of the sector pays often, and each has an id of its own after the first block's");
        Check(MarketCatalogue.SectorKeys[(int)MarketSector.Trusts] == "reallife_mkt_sector_trusts" && (int)MarketSector.Trusts == 23,
            "trusts: the sector is added at the end (saves keep every other sector's number) and has its name");
        var made = MarketLife.Make(MarketCatalogue.Find(137), null, 10, MarketEnv.Quiet(), 60, 5, 0);
        Check(TrustRules.Is(made) && !TrustRules.Is(MarketLife.Make(MarketCatalogue.Find(1), null, 10, MarketEnv.Quiet(), 60, 5, 0)),
            "trusts: a trust is told from a company that pays each quarter");
    }

    // A trust's calendar: periods, record days on market days, the year's rate shared by the days with nothing lost.
    private static void TrustScheduleChecks()
    {
        bool records = true;
        foreach (int every in new[] { MarketCatalogue.TrustThreeDays, MarketCatalogue.TrustWeekly })
        {
            for (int p = 0; p < 300; p++)
            {
                int r = TrustRules.RecordDay(p, every);
                records &= MarketCatalogue.TradingDay(r) && TrustRules.Period(r, every) == p && r > TrustRules.RecordDay(p - 1, every);
            }
        }
        Check(records, "trusts: every period's record day is a market day inside it, and they follow one another");
        Check(TrustRules.RecordDay(0, 7) == 5 && TrustRules.RecordDay(1, 7) == 12 && TrustRules.RecordDay(0, 3) == 3 && TrustRules.RecordDay(1, 3) == 5 && TrustRules.RecordDay(4, 3) == 15,
            "trusts: a week's record day is its Friday, three days' their last market day (Wednesday, then Friday for Thursday to Saturday, Monday for Saturday to Monday)");
        bool sums = true, fair = true;
        foreach (int year in new[] { 60, 61, 100, 365 })
        {
            foreach (int every in new[] { 3, 7 })
            {
                foreach (int quarterly in new[] { 1, 7, 30, 123, 999 })
                {
                    int periods = 400, total = 0, least = int.MaxValue, most = 0;
                    for (int p = 0; p < periods; p++)
                    {
                        int c = TrustRules.Cents(quarterly, p, every, year);
                        total += c;
                        least = Math.Min(least, c);
                        most = Math.Max(most, c);
                    }
                    double want = quarterly * 4.0 * every / year * periods;
                    sums &= Math.Abs(total - want) <= 1.0;
                    fair &= most - least <= 1;
                }
            }
        }
        Check(sums, "trusts: over any run of periods the distributions add up to the quarterly dividend's year, to the cent");
        Check(fair, "trusts: no period's share differs from another's by more than a cent");
        Check(TrustRules.Cents(0, 5, 7, 60) == 0 && TrustRules.Cents(-3, 5, 7, 60) == 0, "trusts: no dividend, no distribution");
        // A declaration at an open: the period whose record day is today or later, built up from the last record day.
        var s = MarketLife.Make(MarketCatalogue.Find(139), null, 1, MarketEnv.Quiet(), 60, 9, 0);
        s.DpsCents = 30;
        s.RecordDay = TrustRules.RecordDay(3, 3);   // day 12, a Friday: paid, gone out of the price on the Monday
        s.DeclaredCents = 4;
        s.ExDone = true;
        TrustRules.Declare(s, 15, 60);   // the Monday's open: period 4 (13 to 15) has its record day today
        Check(s.RecordDay == 15 && s.DeclaredCents == TrustRules.Cents(30, 4, 3, 60) && !s.ExDone && s.PayDay == 16 && s.LastExDay == 12
              && Math.Abs(MarketLife.Accrued(s, 15, 60) - s.DeclaredCents / 100.0) < 1e-12,
            "trusts: a record day on the very morning the last payment leaves the price is declared then, and the price holds all of it that day");
        int was = s.DeclaredCents;
        TrustRules.Declare(s, 15, 60);
        Check(s.DeclaredCents == was && s.RecordDay == 15, "trusts: nothing more is declared while one waits for its record day");
        s.RecordDay = 3;
        s.ExDone = true;
        TrustRules.Declare(s, 15, 60);   // a long gap: periods 1 to 4 had no declaration
        Check(s.RecordDay == 15 && s.DeclaredCents == TrustRules.Cents(30, 1, 3, 60) + TrustRules.Cents(30, 2, 3, 60) + TrustRules.Cents(30, 3, 3, 60) + TrustRules.Cents(30, 4, 3, 60)
              && s.LastExDay == 3, "trusts: periods whose record day went by with nothing declared are paid with the next (" + s.DeclaredCents + " cents)");
        var first = MarketLife.Make(MarketCatalogue.Find(139), null, 15, MarketEnv.Quiet(), 60, 9, 0);
        Check(first.RecordDay == 18 && first.DeclaredCents > 0 && first.LastExDay == 15, "trusts: a trust made today first pays for a record day after today");
    }

    // In a market: how often a trust pays its holders, how much over the days held, and that the price drops by it.
    private static void TrustPayoutChecks()
    {
        int weeklyGaps = 0, weeklyOff = 0, thirdGaps = 0, thirdOff = 0, cases = 0, wholeOnRecord = 0, goneAfter = 0, recordCases = 0, afterCases = 0, shortGaps = 0;
        double thirdMean = 0.0, share = 0.0, shortMean = 0.0;
        int shareCases = 0;
        foreach (int year in new[] { 60, 365 })
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var world = new MarketWorld(seed * 13 + (ulong)year);
                world.Purse.Money = 2000000000;
                MarketEngine m = NewMarket(seed * 13 + (ulong)year, year, 1, 0, world);
                int bought = SessionDay(m, 2, 5);
                foreach (Stock t in Trusts(m))
                {
                    Trade(m, t, OrderSide.Buy, 1000);
                }
                int days = year == 60 ? 120 : 150;
                var records = new Dictionary<int, List<int>>();
                var rate = new Dictionary<int, List<double>>();   // its rate on each day held, in cents a share a day
                for (int d = bought + 1; d <= days; d++)
                {
                    RunTo(m, d, MarketCatalogue.PhaseOpen + 70);
                    foreach (Stock t in Trusts(m))
                    {
                        if (!rate.TryGetValue(t.Id, out List<double> rates))
                        {
                            rate[t.Id] = rates = new List<double>();
                        }
                        rates.Add(t.DpsCents * 4.0 / year);
                        if (MarketCatalogue.TradingDay(d) && t.RecordDay == d && t.DeclaredCents > 0 && !t.ExDone)
                        {
                            if (!records.TryGetValue(t.Id, out List<int> list))
                            {
                                records[t.Id] = list = new List<int>();
                            }
                            list.Add(d);
                            wholeOnRecord += Math.Abs(MarketLife.Accrued(t, d, year) - t.DeclaredCents / 100.0) < 1e-9 ? 1 : 0;
                            recordCases++;
                        }
                    }
                    RunTo(m, d);
                    if (MarketCatalogue.TradingDay(d) && MarketCatalogue.TradingDay(d + 1))
                    {
                        RunTo(m, d + 1, MarketCatalogue.PhaseOpen);
                        foreach (Stock t in Trusts(m))
                        {
                            if (records.TryGetValue(t.Id, out List<int> list) && list.Count > 0 && list[list.Count - 1] == d)
                            {
                                double next = t.DeclaredCents > 0 && !t.ExDone && t.RecordDay > d ? t.DeclaredCents / 100.0 * (d + 1 - d) / (t.RecordDay - d) : 0.0;
                                goneAfter += t.LastExDay == d && Math.Abs(MarketLife.Accrued(t, d + 1, year) - next) < 1e-9 ? 1 : 0;
                                afterCases++;
                            }
                        }
                    }
                }
                foreach (Stock t in Trusts(m))
                {
                    if (!records.TryGetValue(t.Id, out List<int> list) || list.Count < 3)
                    {
                        continue;
                    }
                    cases++;
                    for (int i = 1; i < list.Count; i++)
                    {
                        int gap = list[i] - list[i - 1];
                        if (t.Def.PayDays == MarketCatalogue.TrustWeekly)
                        {
                            weeklyGaps++;
                            weeklyOff += gap == 7 ? 0 : 1;
                        }
                        else
                        {
                            thirdGaps++;
                            thirdOff += gap >= 1 && gap <= (year == 60 ? 6 : 12) ? 0 : 1;   // (a long year's period of nothing is paid with the next)
                            thirdMean += gap;
                            if (year == 60)
                            {
                                shortGaps++;
                                shortMean += gap;   // (in a long year a period's share is a cent or two, and one with nothing waits for the next)
                            }
                        }
                    }
                    // Paid for the record days up to the last one whose pay day has come, against the rate over the days to it.
                    Position p = m.State.Position(t.Id);
                    int last = list.Last(r => MarketTrading.NextTradingDay(r) <= days);
                    double expected = rate[t.Id].Take(last - bought).Sum() * 1000.0 / 100.0;
                    if (p != null && expected > 50.0)
                    {
                        share += p.Dividends / expected;
                        shareCases++;
                    }
                }
            }
        }
        thirdMean /= Math.Max(1, thirdGaps);
        shortMean /= Math.Max(1, shortGaps);
        Console.WriteLine("  trusts: " + cases + " held, weekly record days a week apart " + (weeklyGaps - weeklyOff) + " of " + weeklyGaps + ", the others on average every " + thirdMean.ToString("0.00")
                          + " days (" + shortMean.ToString("0.00") + " in a 60-day year); paid " + (share / Math.Max(1, shareCases) * 100).ToString("0.0") + "% of their rate over the days held");
        Check(cases >= 20 && weeklyGaps > 50 && weeklyOff == 0, "trusts: a weekly trust's record days are a week apart, every week (" + weeklyOff + " of " + weeklyGaps + " not)");
        Check(thirdGaps > 80 && thirdOff == 0 && shortMean > 2.7 && shortMean < 3.3 && thirdMean < 4.5,
            "trusts: one that pays every third day pays every three days or so, never more than six apart in a 60-day year (on average " + shortMean.ToString("0.00") + " in a 60-day year, " + thirdMean.ToString("0.00") + " in all)");
        Check(shareCases >= 20 && Math.Abs(share / shareCases - 1.0) < 0.15, "trusts: what a holding is paid comes to the trust's rate over the days held (" + (share / Math.Max(1, shareCases)).ToString("0.000") + ")");
        Check(recordCases > 100 && wholeOnRecord == recordCases && afterCases > 100 && goneAfter == afterCases,
            "trusts: the price holds the whole distribution on its record day, and the morning after only the next one's share of the day since (" + wholeOnRecord + " of " + recordCases + ", " + goneAfter + " of "
            + afterCases + ")");
    }

    // A trust's yield is a fair one, and buying for a payment earns nothing but the costs.
    private static void TrustFairChecks()
    {
        var yields = new List<double>();
        double gross = 0.0, net = 0.0, sq = 0.0;
        int trips = 0;
        foreach (int year in new[] { 60, 365 })
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                MarketEngine m = NewMarket(seed * 31 + 7, year);
                foreach (Stock t in Trusts(m))
                {
                    yields.Add(t.DpsCents * 4.0 / Math.Max(1, t.Last));
                }
                int days = year == 60 ? 360 : 420;
                var lastClose = new Dictionary<int, int>();
                var lastRecord = new Dictionary<int, int>();
                var cents = new Dictionary<int, int>();
                for (int d = 2; d <= days; d++)
                {
                    if (!MarketCatalogue.TradingDay(d))
                    {
                        continue;
                    }
                    RunTo(m, d, MarketCatalogue.PhaseOpen + 1);
                    foreach (Stock t in Trusts(m))
                    {
                        // Bought at the offer at the record day's close, sold at the bid at the next open, the distribution paid.
                        if (lastRecord.TryGetValue(t.Id, out int r) && r > 0 && r < d && lastClose.TryGetValue(t.Id, out int close) && close > 0)
                        {
                            double half = MarketTrading.HalfSpread(m.State, t, m.State.Phase);
                            double back = (t.Open + cents[t.Id]) / (double)close - 1.0 - MarketValuation.CostOfEquity(t, m.State.Env) * (d - r) / year;
                            gross += back;
                            sq += back * back;
                            net += back - 2.0 * half - 2.0 * MarketCatalogue.CommissionBp / 10000.0;
                            trips++;
                            lastRecord[t.Id] = 0;
                        }
                    }
                    RunTo(m, d);
                    foreach (Stock t in Trusts(m))
                    {
                        if (t.RecordDay == d && t.DeclaredCents > 0 && !t.ExDone)
                        {
                            lastRecord[t.Id] = d;
                            lastClose[t.Id] = t.Last;
                            cents[t.Id] = t.DeclaredCents;
                        }
                    }
                }
                foreach (Stock t in Trusts(m))
                {
                    yields.Add(t.DpsCents * 4.0 / Math.Max(1, t.Last));
                }
            }
        }
        yields.Sort();
        double median = yields[yields.Count / 2], mean = gross / Math.Max(1, trips), sd = Math.Sqrt(Math.Max(0.0, sq / Math.Max(1, trips) - mean * mean));
        Console.WriteLine("  trusts: yields " + (yields[0] * 100).ToString("0.0") + "% to " + (yields[yields.Count - 1] * 100).ToString("0.0") + "%, median " + (median * 100).ToString("0.0")
                          + "%; bought for a payment " + trips + " times, " + (mean * 100).ToString("0.000") + "% a time over what holding is expected to return (spread " + (sd * 100).ToString("0.00") + "%), "
                          + (net / Math.Max(1, trips) * 100).ToString("0.000") + "% after");
        Check(median > 0.03 && median < 0.065 && yields[0] > 0.01 && yields[yields.Count - 1] < 0.11,
            "trusts: a trust yields what a property trust does, about 3% to 6.5% a year (median " + (median * 100).ToString("0.0") + "%)");
        Check(trips > 500 && Math.Abs(mean) < 3.0 * sd / Math.Sqrt(trips) + 0.0005,
            "trusts: buying at a record day's close and selling at the next open earns, with the payment counted, no more than holding is expected to over those days ("
            + (mean * 100).ToString("0.000") + "%)");
        Check(net / Math.Max(1, trips) < 0.0, "trusts: and after the spread and the commission it loses: no money is made from the frequent payments");
    }

    // A trust's price follows where the property market is heading.
    private static void TrustPropertyChecks()
    {
        MarketEnv steady = MarketEnv.Quiet(), boom = MarketEnv.Quiet(), slump = MarketEnv.Quiet(), dear = MarketEnv.Quiet();
        boom.Cycle = 0;
        slump.Cycle = 3;
        dear.RateBp = 500;
        Check(TrustRules.PropertyFactor(boom) > TrustRules.PropertyFactor(steady) && TrustRules.PropertyFactor(steady) > TrustRules.PropertyFactor(slump)
              && TrustRules.PropertyFactor(dear) < TrustRules.PropertyFactor(steady),
            "trusts: their buildings are worth more in a boom and less in a recession or when the bank rate is up, as Estate's market heads");
        Stock t = MarketLife.Make(MarketCatalogue.Find(137), null, 10, steady, 60, 5, 0);
        Stock other = MarketLife.Make(MarketCatalogue.Find(20), null, 10, steady, 60, 5, 0);
        double ratio = MarketValuation.Fair(t, boom) / MarketValuation.Fair(t, steady), otherRatio = MarketValuation.Fair(other, boom) / MarketValuation.Fair(other, steady);
        Check(ratio > otherRatio && ratio > 1.0, "trusts: a boom lifts a trust's worth more than an ordinary property company's (" + ratio.ToString("0.000") + " against " + otherRatio.ToString("0.000") + ")");
        MarketEnv control = MarketEnv.Quiet();
        var levers = new int[MarketEnv.LeverCount];
        levers[5] = 1;   // rents controlled
        control.Levers = MarketEnv.Packed(levers);
        Check(MarketValuation.Fair(t, control) < MarketValuation.Fair(t, steady), "trusts: the council's rent control takes from a trust's rents and its worth");
        double y = Math.Max(8, 60);
        Check(Math.Abs(MarketValuation.PayoutOf(t, 0.03) - Math.Max(MarketCatalogue.TrustPayout, MarketValuation.Payout(0.03))) < 1e-12 && MarketValuation.PayoutOf(other, 0.03) == MarketValuation.Payout(0.03) && y > 0,
            "trusts: a trust pays out nine tenths of its profit at least, every other company as it did");
    }

    // A save from before the trusts: they join its market at its load, the index does not jump, and a sector starts.
    private static void TrustJoinChecks()
    {
        MarketEngine m = NewMarket(141, 60);
        RunTo(m, 40);
        string line = MarketStore.Write(m.State);
        string old = line;
        foreach (CompanyDef d in MarketCatalogue.All.Where(d => d.PayDays > 0))
        {
            old = RemoveCompany(old, d.Id);
        }
        // (a line written before the sector was there has one sector fewer)
        int y = old.IndexOf("~y:", StringComparison.Ordinal);
        int yEnd = old.IndexOf('~', y + 3);
        string[] sectors = old.Substring(y + 3, (yEnd < 0 ? old.Length : yEnd) - y - 3).Split(';');
        old = old.Substring(0, y + 3) + string.Join(";", sectors.Take(sectors.Length - 1)) + (yEnd < 0 ? string.Empty : old.Substring(yEnd));
        MarketState before = MarketStore.Read(old, out _);
        Check(before != null && Trusts(new MarketEngine(before, new MarketTape(before.Id), new MarketWorld(1))).Count == 0 && before.SectorDivisor[(int)MarketSector.Trusts] == 0.0,
            "trusts: a save's line from before them reads with none, and no trusts' sector");
        var levels = typeof(MarketEngine).GetMethod("Index", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object[] args = { 0 };
        levels.Invoke(new MarketEngine(before, new MarketTape(before.Id), new MarketWorld(1)), args);
        int index = (int)args[0];
        int joined = MarketSetup.Join(before);
        var engine = new MarketEngine(before, new MarketTape(before.Id), new MarketWorld(1));
        List<Stock> trusts = Trusts(engine);
        Check(joined == trusts.Count && joined == MarketCatalogue.All.Count(d => d.PayDays > 0) && trusts.All(t => t.Daily.Count == MarketCatalogue.DailyKept && t.ListedDay < before.Day - 60),
            "trusts: every trust joins a save's market from before them, with its past, listed years ago (" + joined + ")");
        levels.Invoke(engine, args);
        int now = (int)args[0];
        int sector = before.SectorLast[(int)MarketSector.Trusts];
        Check(Math.Abs(now - index) <= Math.Max(2, index / 10000), "trusts: the city's index takes them in without a jump (" + index + " to " + now + ")");
        Check(Math.Abs(sector - 100000) <= 10 && before.SectorDaily[(int)MarketSector.Trusts].Count > 50, "trusts: their sector starts at 1,000 with a past from theirs (" + sector + ")");
        Check(MarketSetup.Join(before) == 0, "trusts: a market that has them takes nothing more");
        MarketState twice = MarketStore.Read(old, out _);
        MarketSetup.Join(twice);
        Check(twice.Stocks.Where(TrustRules.Is).Select(t => t.Last).SequenceEqual(before.Stocks.Where(TrustRules.Is).Select(t => t.Last)),
            "trusts: the same save loaded twice has the same trusts at the same prices");
        RunTo(engine, before.Day + 30);
        Check(Trusts(engine).All(t => t.Listed && t.Last > 0) && engine.State.IndexLast > 0, "trusts: and the market goes on with them");
    }

    // ---------------------------------------------------------------- the bonds

    private static void BondYieldChecks()
    {
        MarketEnv env = MarketEnv.Quiet();
        int t1 = BondRules.YieldBp(BondKind.Treasury, -1, 1.0, env), t5 = BondRules.YieldBp(BondKind.Treasury, -1, 5.0, env), t0 = BondRules.YieldBp(BondKind.Treasury, -1, 0.25, env);
        Check(t0 == 306 && t1 == 325 && t5 == 400, "bonds: at a bank rate of 3% the Treasury yields 3.06% for a quarter, 3.25% for a year and 4.00% for five (" + t0 + ", " + t1 + ", " + t5 + ")");
        MarketEnv high = env, low = env, cut = env;
        high.RateBp = 600;
        low.RateBp = 100;
        cut.RateNextBp = -100;
        Check(BondRules.YieldBp(BondKind.Treasury, -1, 0.25, high) > t0 + 250 && BondRules.YieldBp(BondKind.Treasury, -1, 5.0, high) - t5 < BondRules.YieldBp(BondKind.Treasury, -1, 0.25, high) - t0,
            "bonds: a higher bank rate lifts every yield, a short term's most");
        Check(BondRules.YieldBp(BondKind.Treasury, -1, 1.0, cut) < t1, "bonds: a cut the market expects is in the yields already");
        Check(BondRules.YieldBp(BondKind.Treasury, -1, 0.25, low) >= BondCatalogue.YieldLeastBp && BondRules.YieldBp(BondKind.Treasury, -1, 0.0, new MarketEnv { RateBp = -400, InflationBp = -300 }) == BondCatalogue.YieldLeastBp,
            "bonds: no yield goes under its least");
        Check(BondRules.YieldBp(BondKind.City, -1, 1.0, env) == t1 + BondCatalogue.CitySpreadBp, "bonds: a city bond pays its spread over the Treasury's");
        int d0 = BondRules.YieldBp(BondKind.Deposit, 0, 1.0, env), d1 = BondRules.YieldBp(BondKind.Deposit, 1, 1.0, env), d2 = BondRules.YieldBp(BondKind.Deposit, 2, 1.0, env);
        Check(d0 == t1 - BondCatalogue.DepositUnderBp && d1 == d0 + BondCatalogue.DepositPerPointBp && d2 == d0 - BondCatalogue.DepositPerPointBp,
            "bonds: a deposit pays less than the Treasury, more at the bank whose loans are dearer, less at the cheaper one (" + d0 + ", " + d1 + ", " + d2 + ")");
        var offers = new List<BondOffer>();
        BondRules.Offers(env, 60, new[] { "First", "Second", "Third", "Fourth" }, offers);
        int deposits = BondCatalogue.All.Count(d => d.Kind == BondKind.Deposit), others = BondCatalogue.All.Length - deposits;
        Check(offers.Count == others + deposits * BondCatalogue.BanksMost && offers.Where(o => o.Def.Kind == BondKind.Deposit).All(o => o.Bank >= 0 && o.Bank < 3 && o.Issuer.Length > 0),
            "bonds: every Treasury and city bond is on offer, and a deposit at each of the three largest banks only");
        BondRules.Offers(env, 60, null, offers);
        Check(offers.Count == others, "bonds: with no banks named there are no deposits");
        // Bought at its yield, on the day it is bought a bond is worth 100 for each 100: every bond, years short and long.
        bool par = true;
        foreach (int year in new[] { 60, 61, 100, 365 })
        {
            foreach (BondDef d in BondCatalogue.All)
            {
                var offer = new BondOffer { Def = d, Bank = d.Kind == BondKind.Deposit ? 1 : -1, Issuer = "Bank", YieldBp = BondRules.YieldBp(d.Kind, d.Kind == BondKind.Deposit ? 1 : -1, BondRules.TermDays(d, year) / (double)year, env) };
                BondHolding h = BondRules.Make(1, offer, 10000, 10, year, 0, false);
                double price = BondRules.Price(h, d, env, 10, year);
                par &= Math.Abs(price - 100.0) < 0.05 && h.Payments == d.Payments && BondRules.PayDay(h, h.Payments) == h.EndDay && h.EndDay - h.BoughtDay == BondRules.TermDays(d, year);
            }
        }
        Check(par, "bonds: each is worth 100 for each 100 on the day it is bought, its last payment on its last day");
        Check(BondCatalogue.All.Select(d => d.Id).Distinct().Count() == BondCatalogue.All.Length && BondCatalogue.All.All(d => d.Payments >= 1 && d.TermQuarters >= 1)
              && BondCatalogue.Find(4) != null && BondCatalogue.Find(99) == null, "bonds: each bond has its own id, a term and a payment at least");
    }

    private static MarketEngine BondMarket(ulong seed, int year, long money, out MarketWorld world)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = money;
        MarketEngine m = NewMarket(seed, year, 1, 0, world);
        SessionDay(m, 2, 10);
        return m;
    }

    private static BondOffer OfferOf(MarketEngine m, int def, int bank = -1)
    {
        return m.Offer(def, bank, bank >= 0 ? "Granite Bank" : null);
    }

    private static void BondPaymentChecks()
    {
        // A year's Treasury bond paying every quarter, all its interest to the player.
        MarketEngine m = BondMarket(201, 60, 1000000, out MarketWorld world);
        var told = new List<BondNote>();
        m.BondTold = n => told.Add(n);
        BondOffer offer = OfferOf(m, 2);
        BondHolding h = m.BuyBond(offer, 100000, 0, m.Trading, true, out BondWhy why);
        Check(h != null && why == BondWhy.None && world.Purse.Money == 900000 && h.Face == 10000000 && h.RateBp == offer.YieldBp && h.Payments == 4,
            "bonds: a bond bought takes its price from the cash, at 100 for each 100, with no commission (" + why + ")");
        long start = world.Purse.Money;
        int end = h.EndDay;
        var days = new List<int>();
        for (int d = m.State.Day; d <= end + 8; d++)
        {
            int paidBefore = told.Count(n => n.Kind == BondNoteKind.Interest);
            RunTo(m, d);
            if (told.Count(n => n.Kind == BondNoteKind.Interest) > paidBefore)
            {
                days.Add(d);
            }
        }
        long interest = h.Face * offer.YieldBp / 40000L;   // cents, each payment
        long tax = ListingVotes.DividendTax(interest, 0);
        Check(told.Count(n => n.Kind == BondNoteKind.Interest) == 4 && told.Count(n => n.Kind == BondNoteKind.Repaid) == 1 && m.State.Bonds.Holdings.Count == 0,
            "bonds: four payments over its year and its repayment at the end, then it is gone");
        bool onTime = days.Count == 4;
        for (int k = 0; onTime && k < 4; k++)
        {
            int due = BondRules.PayDay(h, k + 1);
            onTime &= days[k] == TradingOnOrAfter(due);
        }
        Check(onTime, "bonds: each payment is made at the close of its day, or of the next market day (" + string.Join(",", days) + ")");
        long got = world.Purse.Money - start;
        long want = 100000 + 4 * (interest - tax) / 100;
        Check(got == want, "bonds: what it paid is what it repays and its interest less the tax withheld, to a unit (" + got + " against " + want + ")");
        Check(told.Where(n => n.Kind == BondNoteKind.Interest).All(n => n.B == 0 && n.C >= 0), "bonds: with nothing put back, all its interest after tax is paid out");

        // All of it put back: its face grows with each payment, nothing is paid until its end.
        m = BondMarket(202, 60, 1000000, out world);
        told.Clear();
        m.BondTold = n => told.Add(n);
        h = m.BuyBond(OfferOf(m, 11), 100000, 100, m.Trading, true, out why);
        long cash = world.Purse.Money, face = h.Face;
        int checkDay = BondRules.PayDay(h, 6);
        RunTo(m, TradingOnOrAfter(checkDay) + 1);
        BondHolding grown = m.State.Bonds.Holding(h.Id);
        Check(grown != null && grown.Paid >= 6 && grown.Face > face && world.Purse.Money == cash && grown.Reinvested > 0 && grown.Cost == face + grown.Reinvested,
            "bonds: with all of its interest put back its face grows at each payment and nothing is paid out (" + (grown != null ? grown.Face - face : 0) + " cents more)");
        long faceAfter = grown != null ? grown.Face : 0;
        RunTo(m, grown.EndDay + 5);
        long finalCash = world.Purse.Money - cash;
        Check(m.State.Bonds.Holdings.Count == 0 && finalCash > 100000 && finalCash >= faceAfter / 100, "bonds: at its end it repays the grown face and its last interest (" + finalCash + ")");
        // Half of it put back: half the interest after tax paid out at each payment.
        m = BondMarket(203, 60, 1000000, out world);
        told.Clear();
        m.BondTold = n => told.Add(n);
        h = m.BuyBond(OfferOf(m, 12), 200000, 50, m.Trading, true, out why);
        RunTo(m, TradingOnOrAfter(BondRules.PayDay(h, 3)) + 1);
        BondNote first = told.FirstOrDefault(n => n.Kind == BondNoteKind.Interest);
        Check(told.Count(n => n.Kind == BondNoteKind.Interest) >= 3 && Math.Abs(first.A - first.B) <= 1 && first.A > 0, "bonds: with half put back, half of each payment is paid out (" + first.A + " and " + first.B + ")");
        Check(m.SetBondReinvest(h.Id, 75, out why) && m.State.Bonds.Holding(h.Id).Reinvest == 75 && m.SetBondReinvest(h.Id, 60, out why) && m.State.Bonds.Holding(h.Id).Reinvest == 50,
            "bonds: the share put back can be changed, held to the choices (a 60 is a 50)");
        // The test tools' payment now.
        int paidNow = m.PayBondsNow();
        Check(paidNow == 1 && m.State.Bonds.Holding(h.Id).Paid >= 4, "bonds: the test pays each bond's next payment at once");
        // Share: tax, then the share put back, then the rest, never under nothing.
        BondRules.Share(10000, 0, 25, out long t, out long back, out long paid);
        Check(t == 1500 && back == 2125 && paid == 6375 && t + back + paid == 10000, "bonds: a payment of 100.00 is 15.00 tax, 21.25 put back at a quarter and 63.75 paid out");
        BondRules.Share(-5, 0, 50, out t, out back, out paid);
        Check(t == 0 && back == 0 && paid == 0, "bonds: a payment of nothing shares out nothing");
        long carry = 0;
        long m1 = BondRules.Money(150, ref carry), m2 = BondRules.Money(150, ref carry);
        Check(m1 == 1 && m2 == 2 && carry == 0, "bonds: cents under a unit are carried to the next payment, never lost");
    }

    private static void BondSaleChecks()
    {
        MarketEngine m = BondMarket(211, 60, 10000000, out MarketWorld world);
        BondHolding longBond = m.BuyBond(OfferOf(m, 4), 100000, 0, m.Trading, true, out _);
        BondHolding shortBond = m.BuyBond(OfferOf(m, 2), 100000, 0, m.Trading, true, out _);
        BondHolding city = m.BuyBond(OfferOf(m, 12), 100000, 0, m.Trading, true, out _);
        BondHolding deposit = m.BuyBond(OfferOf(m, 22, 0), 100000, 0, m.Trading, true, out _);
        long now = m.BondSale(longBond);
        Check(now < longBond.Cost && now > longBond.Cost * 99 / 100 && m.BondSale(city) < m.BondSale(shortBond),
            "bonds: sold at once a bond brings a little less than it cost (its spread), a city bond less than a Treasury's (" + now + " of " + longBond.Cost + ")");
        Check(m.BondSale(deposit) == deposit.Face - deposit.Face * deposit.RateBp * BondCatalogue.DepositPenaltyMonths / 120000L, "bonds: a deposit closed early repays what is in it less three months' interest");
        // Rates up: every bond is worth less, the longer the more; a deposit is not moved.
        MarketEnv was = m.State.Env;
        MarketEnv up = was;
        up.RateBp += 200;
        MarketEnv down = was;
        down.RateBp = Math.Max(50, was.RateBp - 150);
        double longNow = m.BondPrice(longBond), shortNow = m.BondPrice(shortBond);
        long depositNow = m.BondSale(deposit);
        m.State.Env = up;
        double longUp = m.BondPrice(longBond), shortUp = m.BondPrice(shortBond);
        int year = m.State.YearDays;
        double longYears = (longBond.EndDay - m.State.Day) / (double)year, shortYears = (shortBond.EndDay - m.State.Day) / (double)year;
        double longMove = BondRules.YieldBp(BondKind.Treasury, -1, longYears, up) - BondRules.YieldBp(BondKind.Treasury, -1, longYears, was);
        double shortMove = BondRules.YieldBp(BondKind.Treasury, -1, shortYears, up) - BondRules.YieldBp(BondKind.Treasury, -1, shortYears, was);
        Check(longUp < longNow && shortUp < shortNow && (longNow - longUp) / longMove > (shortNow - shortUp) / shortMove && shortMove > longMove && m.BondSale(deposit) == depositNow,
            "bonds: when rates rise every bond's price falls (a short term's yield moves most, a long bond most for each point of its own), and a deposit's sum does not move ("
            + longNow.ToString("0.00") + " to " + longUp.ToString("0.00") + ", " + shortNow.ToString("0.00") + " to " + shortUp.ToString("0.00") + ")");
        m.State.Env = down;
        Check(m.BondPrice(longBond) > longNow && m.BondPrice(shortBond) > shortNow, "bonds: when they fall its price rises");
        m.State.Env = was;
        // A sale: what it brings into the cash, and the bond is gone.
        long before = world.Purse.Money, value = m.BondSale(shortBond) / 100;
        Check(m.SellBond(shortBond.Id, m.Trading, out long money, out BondWhy why) && money == value && world.Purse.Money == before + money && m.State.Bonds.Holding(shortBond.Id) == null,
            "bonds: sold, it brings its price into the cash and is gone");
        Check(!m.SellBond(shortBond.Id, m.Trading, out _, out why) && why == BondWhy.NotHeld, "bonds: a bond sold is not sold twice");
        // Over its life a bond pays about its yield: bought and held, less than shares are expected to return.
        double k = MarketValuation.CostOfEquity(m.State.Stocks[0], m.State.Env);
        Check(longBond.RateBp / 10000.0 < k, "bonds: a bond's rate is under what shares are expected to return (" + longBond.RateBp + " bp against " + (k * 10000).ToString("0") + ")");
    }

    private static void BondRefusalChecks()
    {
        MarketEngine m = BondMarket(221, 60, 50000, out MarketWorld world);
        BondOffer o = OfferOf(m, 2);
        long least = BondRules.Least(m.State.Env.PriceLevel), most = BondRules.Most(m.State.Env.PriceLevel);
        Check(m.CanBuyBond(o, least - 1, true, true) == BondWhy.TooLittle && m.CanBuyBond(o, most + 1, true, true) == BondWhy.TooMuch, "bonds: under the least or over the most is refused");
        Check(m.CanBuyBond(o, 60000, true, true) == BondWhy.NoCash && m.CanBuyBond(o, least, true, false) == BondWhy.Off && m.CanBuyBond(o, least, false, true) == BondWhy.Closed,
            "bonds: more than the cash, with bonds off, or while the market is shut is refused");
        Check(m.CanBuyBond(OfferOf(m, 22, -1), least, true, true) == BondWhy.NoBank && m.CanBuyBond(default(BondOffer), least, true, true) == BondWhy.NoBank, "bonds: a deposit with no bank is refused");
        world.Purse.Money = 100000000;
        int bought = 0;
        while (m.BuyBond(o, least, 0, true, true, out _) != null)
        {
            bought++;
        }
        Check(bought == BondRules.HoldingsMost(m.LargeOn) && m.CanBuyBond(o, least, true, true) == BondWhy.Full, "bonds: no more than the most held at once (" + bought + ")");
        long cash = world.Purse.Money;
        Check(m.BuyBond(o, least, 0, false, true, out BondWhy why) == null && why == BondWhy.Closed && world.Purse.Money == cash, "bonds: a refused purchase takes nothing");
        Check(!m.SellBond(m.State.Bonds.Holdings[0].Id, false, out _, out why) && why == BondWhy.Closed, "bonds: nothing is sold while the market is shut");
        m.State.HeldDay = m.State.Day + 5;
        m.State.HeldPhase = 1;
        Check(m.CanBuyBond(o, least, true, true) == BondWhy.Held && !m.SetBondReinvest(m.State.Bonds.Holdings[0].Id, 50, out why) && why == BondWhy.Held,
            "bonds: after a load nothing is bought, sold or changed until the market is back where it was");
    }

    // The same bonds bought in two markets of the same city pay the same on the same days: nothing is left to the machine.
    private static void BondSameEverywhere()
    {
        string[] lines = new string[2];
        long[] cash = new long[2];
        for (int run = 0; run < 2; run++)
        {
            MarketEngine m = BondMarket(231, 60, 10000000, out MarketWorld world);
            m.BuyBond(OfferOf(m, 4), 200000, 50, m.Trading, true, out _);
            m.BuyBond(OfferOf(m, 11), 100000, 100, m.Trading, true, out _);
            m.BuyBond(OfferOf(m, 21, 2), 50000, 25, m.Trading, true, out _);
            foreach (Stock t in Trusts(m).Take(2))
            {
                Trade(m, t, OrderSide.Buy, 500);
            }
            RunTo(m, 200);
            lines[run] = MarketStore.Write(m.State);
            cash[run] = world.Purse.Money;
        }
        Check(lines[0] == lines[1] && cash[0] == cash[1], "bonds: the same bonds and trusts in two markets of the same city pay the same on the same days");
    }

    private static void BondStoreChecks()
    {
        MarketEngine m = BondMarket(241, 60, 10000000, out _);
        BondHolding a = m.BuyBond(OfferOf(m, 3), 120000, 75, m.Trading, true, out _);
        m.BuyBond(OfferOf(m, 22, 1), 80000, 0, m.Trading, true, out _);
        RunTo(m, 40, MarketCatalogue.PhaseOpen + 20);
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Check(back != null && MarketStore.Write(back) == line, "bonds: the save's line with bonds reads back and writes again exactly the same");
        bool same = back != null && back.Bonds.Holdings.Count == 2 && back.Bonds.NextId == m.State.Bonds.NextId;
        for (int i = 0; same && i < 2; i++)
        {
            BondHolding x = m.State.Bonds.Holdings[i], y = back.Bonds.Holdings[i];
            same = x.Id == y.Id && x.Def == y.Def && x.Bank == y.Bank && x.Issuer == y.Issuer && x.Face == y.Face && x.Cost == y.Cost && x.RateBp == y.RateBp && x.BoughtDay == y.BoughtDay
                   && x.EndDay == y.EndDay && x.Payments == y.Payments && x.Paid == y.Paid && x.Reinvest == y.Reinvest && x.Carry == y.Carry && x.Received == y.Received
                   && x.Reinvested == y.Reinvested && x.Taxed == y.Taxed;
        }
        Check(same, "bonds: every bond comes back with its face, rate, days, payments and record");
        Check(!MarketStore.Write(m.State, true).Contains("~f:"), "bonds: the city's line a Going Public host sends has nobody's bonds");
        int f = line.IndexOf("~f:", StringComparison.Ordinal), after = line.IndexOf('~', f + 3);
        string none = line.Substring(0, f) + (after < 0 ? string.Empty : line.Substring(after));
        MarketState old = MarketStore.Read(none, out _);
        Check(old != null && old.Bonds.Holdings.Count == 0 && old.Stocks.Count == back.Stocks.Count, "bonds: a line from before bonds loads, with none");
        string unknown = line.Replace("~f:" + m.State.Bonds.NextId + ";" + a.Id + "|" + a.Def + "|", "~f:" + m.State.Bonds.NextId + ";" + a.Id + "|77|");
        MarketState dropped = MarketStore.Read(unknown, out _);
        Check(unknown != line && dropped != null && dropped.Bonds.Holdings.Count == 1, "bonds: a bond this version does not know is dropped, the others kept");
        var guest = new BondBook();
        guest.Take(back.Bonds);
        Check(guest.Holdings.Count == 2 && guest.NextId == back.Bonds.NextId, "bonds: a guest's bonds come with them into the host's market");
    }

    // A close with the most bonds held and every trust: what it costs.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void BondCost()
    {
        MarketEngine m = BondMarket(251, 60, 1000000000, out _);
        for (int i = 0; i < BondCatalogue.HoldingsMost; i++)
        {
            m.BuyBond(OfferOf(m, BondCatalogue.All[i % 6].Id), 10000, i % 2 == 0 ? 100 : 0, m.Trading, true, out _);
        }
        var income = typeof(MarketEngine).GetMethod("Income", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var watch = Stopwatch.StartNew();
        int rounds = 200;
        for (int r = 0; r < rounds; r++)
        {
            income.Invoke(m, new object[] { m.State.Day });
        }
        double ms = watch.Elapsed.TotalMilliseconds / rounds;
        Console.WriteLine("  bonds' cost: a close with " + m.State.Bonds.Holdings.Count + " bonds and every trust, " + ms.ToString("0.000") + " ms");
        Check(ms < 0.5, "bonds: a close with the most bonds held costs under half a millisecond (" + ms.ToString("0.000") + " ms)");
    }

    // ---------------------------------------------------------------- the words

    private static readonly string[] IncomeKeys =
    {
        "reallife_mkt_sector_trusts", "reallife_mkt_tab_bonds", "reallife_mkt_col_next_pay", "reallife_mkt_next_pay", "reallife_mkt_bond_name_treasury", "reallife_mkt_bond_name_city",
        "reallife_mkt_bond_name_deposit", "reallife_mkt_bond_why_closed", "reallife_mkt_bond_why_no_bank", "reallife_mkt_bond_note_paid", "reallife_mkt_bond_note_paid_back",
        "reallife_mkt_bond_note_repaid", "reallife_mkt_bond_note_bought", "reallife_mkt_bond_note_sold", "reallife_mkt_ticket_trust", "reallife_mkt_bond_opt", "reallife_opt_tip_markets_bonds",
        "reallife_mkt_bond_test_open", "reallife_mkt_bond_test_pay", "reallife_mkt_bond_test_none", "reallife_transaction_shares_bond_bought", "reallife_transaction_shares_bond_bought_label",
        "reallife_transaction_shares_bond_sold", "reallife_transaction_shares_bond_sold_label", "reallife_transaction_shares_bond_paid", "reallife_transaction_shares_bond_paid_label",
        "reallife_transaction_shares_bond_repaid", "reallife_transaction_shares_bond_repaid_label", "reallife_mkt_plan_bond_title", "reallife_mkt_plan_bond_end_title"
    };

    private static void IncomeWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("bonds", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0], pt = langs[1], zh = langs[2];
        string missing = string.Join(", ", IncomeKeys.Where(k => LanguagesLacking(langs, codes, k).Length > 0).Select(k => k + " (" + LanguagesLacking(langs, codes, k) + ")"));
        Check(missing.Length == 0, "bonds: every text of the bonds and the trusts is in every language with the same {names} (" + missing + ")");
        Check(new[] { "reallife_mkt_bond_note_paid", "reallife_mkt_bond_note_paid_back", "reallife_mkt_bond_note_repaid", "reallife_mkt_bond_note_bought", "reallife_mkt_bond_note_sold" }
                  .All(k => en.ContainsKey(k) && en[k].Length <= 200), "bonds: every notification is short (240 characters with its names at most)");
        Check(new[] { BondMoney.Bought, BondMoney.Sold, BondMoney.Paid, BondMoney.Repaid }.All(k => k.Contains("shares_") && en.ContainsKey(k) && en.ContainsKey(k + "_label"))
              && RealLifeOverhaul.Economy.CashCategory.For(BondMoney.Paid) == RealLifeOverhaul.Economy.CashCategory.Investments
              && RealLifeOverhaul.Economy.CashCategory.For(BondMoney.Repaid) == RealLifeOverhaul.Economy.CashCategory.Investments,
            "bonds: each payment is a transaction of its own, with its text and label, under investments in the Finance app");
        string guide = en.TryGetValue("reallife_guide_markets", out string g) ? g : string.Empty;
        Check(guide.Contains("{mkt_trusts}") && guide.Contains("{bond_least}") && guide.Contains("# Bonds") && pt["reallife_guide_markets"].Contains("{bond_penalty_months}")
              && zh["reallife_guide_markets"].Contains("{mkt_trust_payout}"), "bonds: the Stocks page of the Guide tells of the bonds and the trusts, with their figures, in each language");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row).
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("new Bonds tab"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal) && !row.Value.StartsWith("Fixed: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "bonds: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }
}
