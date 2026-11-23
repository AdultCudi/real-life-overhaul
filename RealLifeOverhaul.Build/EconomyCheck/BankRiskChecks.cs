using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

// The own bank's risk, credit cards and trading desk (Scripts/Economy/OwnBank/Risk, Cards, Desk; players' suggestions of 1 Nov 2026): the risk level's tables and
// that Off is the bank as before, credit risk that follows the cycle, a kind of borrower in trouble drawn from the seed, concentration, savers who leave faster than
// they come, a run with its tools and that it resolves, the waterfall when the regulator takes the bank, the card book's sums and its rules, the owner's own card,
// the desk's commission, its own book and its risk limit, quarterly dividends never below the required capital, the lines of the statement adding up to the day,
// the save, the texts and the timing.
internal static partial class Program
{
    private static void BankRiskChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        RiskTables();
        RiskCredit();
        RiskShock();
        RiskConcentration();
        RiskLiquidity();
        RiskRun();
        RiskWaterfall();
        RiskOff();
        RiskStore();
        CardBusiness();
        CardApproval();
        CardOwner();
        DeskBrokerage();
        DeskOwnBook();
        GrowthDividends();
        GrowthLines();
        GrowthSources();
        GrowthTexts();
        GrowthTiming();
        Console.WriteLine("  own bank's risk, cards and trading desk: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static BankCity RiskCity(int risk, CycleState cycle = CycleState.Steady)
    {
        BankCity c = TierCity(0);
        c.Risk = risk;
        c.Cycle = cycle;
        return c;
    }

    // A national bank grown for a year in a steady city with the risk off, then the risk put on.
    private static OwnBank GrownBank(int tier, int seed, long extra = 0)
    {
        BankCity c = RiskCity(0);
        OwnBank b = TierBank(c, tier, seed, extra);
        RunBank(b, c, 2, 3 * TierYear, null, new BankDay());
        return b;
    }

    // ---------------------------------------------------------------- the tables

    private static void RiskTables()
    {
        int n = BankRisk.Count;
        Check(BankRisk.FirmCycle.Length == n && BankRisk.PeopleStress.Length == n && BankRisk.RecoveryCycle.Length == n && BankRisk.SaleDiscount.Length == n && BankRisk.WindCycle.Length == n
              && BankRisk.ShockChance.Length == n && BankRisk.ShockDefault.Length == n && BankRisk.ConcLimit.Length == n && BankRisk.ConcWeight.Length == n && BankRisk.ConcDefault.Length == n
              && BankRisk.ReserveShare.Length == n && BankRisk.LendRatio.Length == n && BankRisk.FlightSpeed.Length == n && BankRisk.FlightShare.Length == n && BankRisk.RunStart.Length == n
              && BankRisk.RunEnd.Length == n && BankRisk.RunMult.Length == n && BankRisk.WatchLend.Length == n && BankRisk.InterbankMost.Length == n && BankRisk.InterbankSpread.Length == n
              && BankRisk.NameKeys.Length == n, "bank risk: a figure of every table for every level");
        bool cycles = true;
        for (int l = 0; l < n; l++)
        {
            cycles &= BankRisk.FirmCycle[l].Length == MacroCatalogue.StateCount && BankRisk.PeopleStress[l].Length == MacroCatalogue.StateCount
                && BankRisk.RecoveryCycle[l].Length == MacroCatalogue.StateCount
                      && BankRisk.SaleDiscount[l].Length == MacroCatalogue.StateCount && BankRisk.WindCycle[l].Length == MacroCatalogue.StateCount;
        }
        Check(cycles && BankRisk.InterbankCycle.Length == MacroCatalogue.StateCount, "bank risk: a figure for every state of the cycle");
        bool neutral = true;
        for (int s = 0; s < MacroCatalogue.StateCount; s++)
        {
            neutral &= BankRisk.FirmCycle[0][s] == 1000 && BankRisk.PeopleStress[0][s] == 1000 && BankRisk.RecoveryCycle[0][s] == 1000 && BankRisk.SaleDiscount[0][s] == 0
                && BankRisk.WindCycle[0][s] == 1000;
        }
        Check(neutral && BankRisk.ShockChance[0] == 0 && BankRisk.ShockDefault[0] == 1000 && BankRisk.ConcLimit[0] == 1000 && BankRisk.ConcWeight[0] == 0 && BankRisk.ConcDefault[0] == 0
              && BankRisk.ReserveShare[0] == OwnBankCatalogue.ReserveShare && BankRisk.LendRatio[0] == OwnBankCatalogue.LendRatio && BankRisk.FlightSpeed[0] == 1000 && BankRisk.FlightShare[0] == 0
              && BankRisk.RunStart[0] == OwnBankCatalogue.RunStart && BankRisk.RunEnd[0] == OwnBankCatalogue.RunEnd && BankRisk.RunMult[0] == 1000 && BankRisk.WatchLend[0] == 1000
              && BankRisk.InterbankMost[0] == 0, "bank risk: the Off level is every neutral figure, the bank as it was");
        bool harder = true;
        for (int s = 0; s < MacroCatalogue.StateCount; s++)
        {
            harder &= BankRisk.FirmCycle[2][s] >= BankRisk.FirmCycle[1][s] || s < 2;
            harder &= BankRisk.RecoveryCycle[2][s] <= BankRisk.RecoveryCycle[1][s] && BankRisk.SaleDiscount[2][s] >= BankRisk.SaleDiscount[1][s]
                && BankRisk.WindCycle[2][s] <= BankRisk.WindCycle[1][s];
        }
        Check(harder && BankRisk.ShockChance[2] > BankRisk.ShockChance[1] && BankRisk.ShockDefault[2] > BankRisk.ShockDefault[1] && BankRisk.ConcLimit[2] < BankRisk.ConcLimit[1]
              && BankRisk.ReserveShare[2] > BankRisk.ReserveShare[1] && BankRisk.LendRatio[2] > BankRisk.LendRatio[1] && BankRisk.FlightSpeed[2] > BankRisk.FlightSpeed[1]
              && BankRisk.RunStart[2] > BankRisk.RunStart[1] && BankRisk.WatchLend[2] < BankRisk.WatchLend[1] && BankRisk.InterbankMost[2] < BankRisk.InterbankMost[1],
            "bank risk: Hard is harder than Standard in every table");
        Check(BankRisk.RunEnd[1] > BankRisk.RunStart[1] && BankRisk.RunEnd[2] > BankRisk.RunStart[2] && BankRisk.LendRatio[1] > OwnBankCatalogue.WatchRatio
            && BankRisk.LendRatio[2] > BankRisk.LendRatio[1],
            "bank risk: a run ends over a higher confidence than it began under, and the warning line is over the watch line");
        Check(BankDividends.Order.Length == BankDividends.Count && BankDividends.PositionOf(BankDividends.Quarterly) == 3 && BankDividends.PositionOf(BankDividends.Yearly) == 4
              && BankDividends.EveryKeys.Length == BankDividends.Count && BankDividends.WhyKeys.Length == BankDividends.WhyMost + 1,
            "bank risk: the dividends' choices are in time order, quarterly before yearly, and the saved numbers are not renumbered");
    }

    // ---------------------------------------------------------------- credit risk that follows the economy

    private static void RiskCredit()
    {
        OwnBank b = NewBank(RiskCity(0), 1, 7);
        int firm = OwnBankCatalogue.Shops, person = OwnBankCatalogue.Fair;
        var defaults = new int[BankRisk.Count, MacroCatalogue.StateCount, 2];
        for (int l = 0; l < BankRisk.Count; l++)
        {
            for (int s = 0; s < MacroCatalogue.StateCount; s++)
            {
                BankCity c = RiskCity(l, (CycleState)s);
                c.Trouble = 1000;
                defaults[l, s, 0] = OwnBankRules.DefaultPerMille(c, b, firm);
                defaults[l, s, 1] = OwnBankRules.DefaultPerMille(c, b, person);
            }
        }
        bool rises = true;
        for (int l = 1; l < BankRisk.Count; l++)
        {
            for (int k = 0; k < 2; k++)
            {
                rises &= defaults[l, 0, k] <= defaults[l, 1, k] && defaults[l, 1, k] < defaults[l, 2, k] && defaults[l, 2, k] < defaults[l, 3, k];
            }
        }
        Check(rises, "bank risk: defaults rise with the economy's state, a boom to a recession, for firms and for people, on both levels");
        Check(defaults[1, 3, 0] > defaults[0, 3, 0] * 3 / 2 && defaults[2, 3, 0] > defaults[1, 3, 0] && defaults[2, 3, 1] > defaults[1, 3, 1] && defaults[1, 3, 1] > defaults[0, 3, 1],
            "bank risk: a recession brings a firm's loans over half as many defaults again with the risk Standard, and more with Hard (" + defaults[0, 3, 0] + ", " + defaults[1, 3, 0] + ", "
            + defaults[2, 3, 0] + " in a thousand)");
        BankCity off = RiskCity(0, CycleState.Recession);
        Check(OwnBankRules.StressFactor(off, b, firm) == 1000 && OwnBankRules.StressFactor(off, b, person) == 1000 && OwnBankRules.RecoveryFactor(off) == 1000,
            "bank risk: with the risk off the economy adds nothing to defaults and recoveries");
        Check(OwnBankRules.RecoveryFactor(RiskCity(1, CycleState.Recession)) < OwnBankRules.RecoveryFactor(RiskCity(1, CycleState.Boom)) && OwnBankRules.RecoveryFactor(RiskCity(2,
            CycleState.Recession))
              < OwnBankRules.RecoveryFactor(RiskCity(1, CycleState.Recession)), "bank risk: what is written off comes back less in a recession, and less again on Hard");
        // The same bank and days give the same figures: all from the seed.
        OwnBank one = GrownBank(BankTiers.Regional, 31), two = GrownBank(BankTiers.Regional, 31);
        BankCity hard = RiskCity(2, CycleState.Slowdown);
        var day = new BankDay();
        RunBank(one, hard, one.LastDay + 1, 2 * TierYear, null, day);
        RunBank(two, hard, two.LastDay + 1, 2 * TierYear, null, day);
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two), "bank risk: the same bank and days in the same economy come to the same books to the cent, whatever the level");
    }

    private static void RiskShock()
    {
        int hits = 0, months = 4000;
        bool same = true, ranged = true, off = true;
        for (int m = 0; m < months; m++)
        {
            int s = OwnBankRules.ShockSegment(77, m, BankRisk.Standard);
            hits += s >= 0 ? 1 : 0;
            same &= s == OwnBankRules.ShockSegment(77, m, BankRisk.Standard);
            ranged &= s >= -1 && s < OwnBankCatalogue.Segments;
            off &= OwnBankRules.ShockSegment(77, m, BankRisk.Off) == -1;
        }
        int expected = months * BankRisk.ShockChance[BankRisk.Standard] / 1000;
        Check(same && ranged && off && Math.Abs(hits - expected) * 4 < expected,
            "bank risk: a kind in trouble is drawn from the seed: the same each time, none with the risk off, " + hits + " months in "
            + months + " on Standard (about " + expected + ")");
        int hard = 0, diff = 0;
        for (int m = 0; m < months; m++)
        {
            hard += OwnBankRules.ShockSegment(77, m, BankRisk.Hard) >= 0 ? 1 : 0;
            diff += OwnBankRules.ShockSegment(78, m, BankRisk.Standard) != OwnBankRules.ShockSegment(77, m, BankRisk.Standard) ? 1 : 0;
        }
        Check(hard > hits && diff > months / 20, "bank risk: Hard draws more troubled months, and another bank's seed draws others");
        int monthAt = -1;
        for (int m = 0; m < 400 && monthAt < 0; m++)
        {
            monthAt = OwnBankRules.ShockSegment(41, m + 1, BankRisk.Standard) >= 0 ? m : -1;
        }
        OwnBank b = NewBank(RiskCity(1), 1, 41);
        b.Risk = BankRisk.Standard;
        b.Month = monthAt;
        b.MonthFrom = 100;
        int[] seen = new int[5];
        for (int d = 100; d < 105; d++)
        {
            seen[d - 100] = OwnBankRules.ShockAhead(b, d, 5);
        }
        int want = OwnBankRules.ShockSegment(41, monthAt + 1, BankRisk.Standard);
        Check(monthAt >= 0 && seen[0] == -1 && seen[1] == -1 && seen[2] == -1 && seen[3] == want && seen[4] == want,
            "bank risk: the trouble coming next month is told in the last two fifths of the month before it, and not before");
        b.Month = monthAt + 1;
        BankCity c = RiskCity(1);
        int troubled = OwnBankRules.ShockSegment(41, b.Month, BankRisk.Standard), other = (troubled + 1) % OwnBankCatalogue.Segments;
        Check(OwnBankRules.StressFactor(c, b, troubled) > OwnBankRules.StressFactor(c, b, other) * 3 / 2, "bank risk: the kind in trouble has its defaults swollen that month, the others' not");
    }

    // ---------------------------------------------------------------- concentration

    private static void RiskConcentration()
    {
        OwnBank b = GrownBank(BankTiers.National, 53);
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Loans[j] = 0;
            b.Arrears[j] = 0;
        }
        long capital = OwnBankRules.CapitalNow(b);
        b.Loans[OwnBankCatalogue.Shops] = capital * 3;
        b.Loans[OwnBankCatalogue.Companies] = capital / 2;
        b.Reserves = b.Savings + b.Capital - b.Lent - b.InArrears - b.FundCost - b.OtherAssets;
        b.Risk = BankRisk.Off;
        long rwaOff = OwnBankRules.RiskWeighted(b);
        Check(OwnBankRules.ConcExcess(b, OwnBankCatalogue.Shops) == 0 && OwnBankRules.ConcentrationCharge(b) == 0, "bank risk: with the risk off a lopsided book is charged nothing");
        b.Risk = BankRisk.Standard;
        int excess = OwnBankRules.ConcExcess(b, OwnBankCatalogue.Shops);
        Check(excess > 300 && OwnBankRules.ConcExcess(b, OwnBankCatalogue.Companies) == 0 && OwnBankRules.Concentration(b, OwnBankCatalogue.Shops) > 800
              && OwnBankRules.RiskWeighted(b) > rwaOff && OwnBankRules.ConcentrationCharge(b) > 0,
            "bank risk: a book with six sevenths in one kind is over the line by " + excess + " thousandths, and the regulator weighs the excess");
        BankCity c = RiskCity(1);
        Check(OwnBankRules.StressFactor(c, b, OwnBankCatalogue.Shops) > OwnBankRules.StressFactor(c, b, OwnBankCatalogue.Fair) + 300 && OwnBankRules.StressFactor(c, b, OwnBankCatalogue.Fair) == 1000,
            "bank risk: too much in one kind brings that kind more defaults and spares the rest");
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Loans[j] = capital * 2 / 5;
        }
        Check(OwnBankRules.ConcExcess(b, OwnBankCatalogue.Shops) == 0 && OwnBankRules.ConcentrationCharge(b) == 0, "bank risk: a book spread evenly over the kinds is charged nothing");
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Loans[j] = 0;
        }
        b.Loans[OwnBankCatalogue.Shops] = capital / 4;
        Check(OwnBankRules.ConcExcess(b, OwnBankCatalogue.Shops) == 0, "bank risk: a book smaller than the capital is too young to be concentrated");
        b.Loans[OwnBankCatalogue.Shops] = 0;
        b.CardBal[OwnBankCatalogue.Fair] = capital * 2;
        b.Loans[OwnBankCatalogue.Fair] = capital;
        Check(OwnBankRules.Concentration(b, OwnBankCatalogue.Fair) == 1000 && OwnBankRules.ConcExcess(b, OwnBankCatalogue.Fair) > 400, "bank risk: cards count in the book of their kind");
    }

    // ---------------------------------------------------------------- liquidity

    private static void RiskLiquidity()
    {
        OwnBank b = GrownBank(BankTiers.National, 61);
        b.Risk = BankRisk.Off;
        long off = OwnBankRules.ReserveNeed(b);
        b.Risk = BankRisk.Standard;
        long std = OwnBankRules.ReserveNeed(b);
        b.Risk = BankRisk.Hard;
        long hard = OwnBankRules.ReserveNeed(b);
        Check(off == b.Savings * OwnBankCatalogue.ReserveShare / 1000 && std > off && hard > std,
            "bank risk: the reserve kept against the deposits is the usual with the risk off, more on Standard, more again on Hard");
        // Savers leave faster than they come: the same gap one way and the other.
        long outflow = 0, inflow = 0;
        for (int dir = 0; dir < 2; dir++)
        {
            foreach (int level in new[] { 0, 1, 2 })
            {
                BankCity c = RiskCity(level);
                OwnBank s = NewBank(c, 1, 9);
                s.Deposits[0] = 40000000000L / 10;
                s.Reserves += s.Deposits[0];
                s.SaverOffset[0] = dir == 0 ? OwnBankCatalogue.SaverOffsetLeast : OwnBankCatalogue.SaverOffsetMost;
                s.Test = true;
                s.FoundedDay = -3000;   // a name the city trusts: what it wants is its own to give
                s.Deposits[0] = 3000000000L;
                s.Reserves = s.Savings + s.Capital;
                var day = new BankDay();
                long before = s.Deposits[0];
                OwnBankRules.Day(s, c, 2, null, day);
                long moved = s.Deposits[0] - before - day.Savers;
                if (level == 0)
                {
                    outflow = dir == 0 ? moved : outflow;
                    inflow = dir == 1 ? moved : inflow;
                }
                else if (level == 1)
                {
                    Check(dir == 1 || moved < 0 && moved <= outflow * 3 / 2 && -moved >= -outflow * 19 / 10,
                        "bank risk: on Standard a bank paying less than the rivals loses savers about twice as fast as with the risk off (" + (-moved) + " against " + (-outflow) + ")");
                }
            }
        }
        Check(outflow < 0 && inflow >= 0, "bank risk: the risk's flight applies to leaving only, savers coming in are not slowed");
        b.Risk = BankRisk.Standard;
        Check(OwnBankRules.Flight(b, 0, 700) == 0 && OwnBankRules.Flight(b, 0, 500) > 0 && OwnBankRules.Flight(b, 1, 500) > OwnBankRules.Flight(b, 0, 500) * b.Deposits[1] / Math.Max(1,
            b.Deposits[0]) && OwnBankRules.Flight(b, 0, 300) > OwnBankRules.Flight(b, 0, 500),
            "bank risk: savers leave of their own accord under a confidence of " + BankRisk.FlightConfidence + ", more the lower it is, and businesses first");
        b.Risk = BankRisk.Off;
        Check(OwnBankRules.Flight(b, 0, 100) == 0, "bank risk: no flight of its own with the risk off");
        // Cash for a run: firms leave first, so a firms' bank has less cover.
        OwnBank people = NewBank(RiskCity(1), 1, 5), firms = NewBank(RiskCity(1), 1, 5);
        people.Risk = firms.Risk = BankRisk.Standard;
        people.Deposits[0] = 100000000000L;
        firms.Deposits[1] = 100000000000L;
        people.Reserves = firms.Reserves = people.Deposits[0] / 10;
        people.Capital = firms.Capital = 0;
        Check(OwnBankRules.CoverDays(people) >= BankRisk.CoverWarnDays && OwnBankRules.CoverDays(firms) < BankRisk.CoverWarnDays && OwnBankRules.RiskCushion(people) == 1000
              && OwnBankRules.RiskCushion(firms) == BankRisk.CoverWarnTrust,
                  "bank risk: ten per cent in reserve covers " + OwnBankRules.CoverDays(people) / 10.0 + " days of a run of people's savings and "
              + OwnBankRules.CoverDays(firms) / 10.0 + " of businesses', and a bank short of cash loses the city's confidence");
        firms.Reserves = 0;
        Check(OwnBankRules.RiskCushion(firms) == BankRisk.CoverRunTrust && OwnBankRules.CoverDays(firms) == 0, "bank risk: a bank with no cash at all loses more of it");
        // Paying savers well holds back a run.
        BankCity city = RiskCity(1);
        OwnBank holder = NewBank(city, 1, 6);
        holder.Risk = BankRisk.Standard;
        int none = OwnBankRules.RunHold(city, holder, 0);
        holder.SaverOffset[0] = 200;
        int some = OwnBankRules.RunHold(city, holder, 0);
        holder.SaverOffset[0] = OwnBankCatalogue.SaverOffsetMost;
        int most = OwnBankRules.RunHold(city, holder, 0);
        holder.Risk = BankRisk.Off;
        Check(none == 0 && some > 0 && most == BankRisk.RunRateMost && OwnBankRules.RunHold(city, holder, 0) == 0,
            "bank risk: paying savers over the rivals' rate holds back a run, up to " + BankRisk.RunRateMost / 10 + "%, and nothing with the risk off");
    }

    // ---------------------------------------------------------------- a run, its tools and its end

    private static void RiskRun()
    {
        // A thinly capitalised national bank in a recession with the city scared: a run begins, and without help it closes or ends; the books balance every day.
        foreach (int level in new[] { BankRisk.Standard, BankRisk.Hard })
        {
            OwnBank b = GrownBank(BankTiers.National, 70 + level);
            BankCity c = RiskCity(level, CycleState.Recession);
            var day = new BankDay();
            long capital = OwnBankRules.CapitalNow(b);
            b.Scare = OwnBankCatalogue.ScareMost;
            b.Capital -= capital * 3 / 5;
            b.Reserves -= capital * 3 / 5;
            bool balanced = OwnBankRules.Balanced(b), started = false, resolved = false, always = true;
            int firstDay = b.LastDay + 1;
            for (int d = firstDay; d < firstDay + 400 && b.Running; d++)
            {
                OwnBankRules.Day(b, c, d, null, day);
                started |= day.RunStarted;
                balanced &= !b.Running || OwnBankRules.Balanced(b);
                resolved |= (started && day.RunEnded) || day.Closed;
                always &= !b.Running || b.Reserves >= 0;
            }
            Check(started && balanced && always && (resolved || !b.Running),
                "bank risk: a thin bank in a recession with the city scared meets a run on " + BankRisk.NameKeys[level] + ", the books balance every day and it ends ("
                    + (b.Running ? "the run ended" : "the bank closed: " + b.Why) + ")");
        }
        // The tools: a bank in a run puts in capital, sells loans and borrows, and its cash and its ratio come back; each leaves the books balanced.
        BankCity run = RiskCity(BankRisk.Standard, CycleState.Slowdown);
        OwnBank t = GrownBank(BankTiers.National, 83);
        t.Risk = BankRisk.Standard;
        long cash = t.Reserves, ratio = OwnBankRules.Ratio(t);
        long face = OwnBankRules.SaleMostFace(t, OwnBankCatalogue.Good);
        bool sold = face > 0 && OwnBankRules.SellLoans(t, run, OwnBankCatalogue.Good, face, out long got, out long loss);
        long discount = face * OwnBankRules.SaleDiscountOf(run, t) / 1000;
        Check(sold && t.Reserves == cash + face - discount && OwnBankRules.Balanced(t) && t.SoldFace == face && t.SoldLoss == discount && t.SaleLossPend == discount,
            "bank risk: loans sold to other banks bring their face less the discount into the reserves, the discount out of the capital, and the books balance");
        Check(!OwnBankRules.SellLoans(t, run, OwnBankCatalogue.Good, OwnBankRules.SaleMostFace(t, OwnBankCatalogue.Good) + 1, out _, out _) && !OwnBankRules.SellLoans(t, run,
            OwnBankCatalogue.Good, 0, out _, out _),
            "bank risk: a sale over what one sale may take, or of nothing, is refused");
        long room = OwnBankRules.BorrowRoom(t), asked = OwnBankRules.ToStep(room / 2);
        long reserves = t.Reserves, capitalNow = t.Capital;
        bool borrowed = asked > 0 && OwnBankRules.Borrow(t, run, asked, 100, out long fee);
        Check(borrowed && t.Interbank == asked && t.Reserves == reserves + asked - OwnBankRules.BorrowFee(asked) && t.Capital == capitalNow - OwnBankRules.BorrowFee(asked) && OwnBankRules.Balanced(t)
              && t.InterbankBp == OwnBankRules.BorrowBp(run, t) && t.InterbankDue == 100 + BankRisk.InterbankMonths * run.MonthDays,
            "bank risk: money borrowed from other banks is a debt beside the deposits at the bank rate and a margin, the fee out of the capital, and the books balance");
        Check(!OwnBankRules.Borrow(t, run, OwnBankRules.BorrowRoom(t) + OwnBankCatalogue.CapitalStepCents, 100, out _),
            "bank risk: more than the share of the capital that may be borrowed is refused");
        OwnBank offBank = GrownBank(BankTiers.National, 84);
        Check(OwnBankRules.BorrowRoom(offBank) == 0 && OwnBankRules.SaleMostFace(offBank, OwnBankCatalogue.Good) == 0 && !OwnBankRules.Borrow(offBank, RiskCity(0), 100000, 100, out _),
            "bank risk: with the risk off there is nothing to borrow and nothing sold");
        // A day with money borrowed: its interest is a cost; on its day it is repaid out of the cash over the reserve, else rolled over dearer.
        var d1 = new BankDay();
        long interestBefore = t.Capital;
        OwnBankRules.InterbankDay(t, 101, run.YearDays, run.MonthDays, d1);
        long interest = asked * t.InterbankBp / (10000L * run.YearDays);
        Check(d1.Funding == interest + OwnBankRules.BorrowFee(asked) + 0 || d1.Funding >= interest, "bank risk: a day of the money borrowed costs its interest, the fee told with it");
        OwnBank rich = GrownBank(BankTiers.National, 85);
        rich.Risk = BankRisk.Standard;
        OwnBankRules.Borrow(rich, run, OwnBankRules.ToStep(OwnBankRules.BorrowRoom(rich) / 3), 50, out _);
        rich.Reserves += rich.Interbank * 3;
        rich.Capital += rich.Interbank * 3;
        rich.Deposits[0] += 0;
        long owed = rich.Interbank;
        var d2 = new BankDay();
        OwnBankRules.InterbankDay(rich, rich.InterbankDue, run.YearDays, run.MonthDays, d2);
        Check(owed > 0 && rich.Interbank == 0 && rich.InterbankDue == 0 && !d2.Rolled && OwnBankRules.Balanced(rich),
            "bank risk: money borrowed is repaid on its day out of the cash over the reserve");
        OwnBank poor = GrownBank(BankTiers.National, 86);
        poor.Risk = BankRisk.Standard;
        OwnBankRules.Borrow(poor, run, OwnBankRules.ToStep(OwnBankRules.BorrowRoom(poor) / 2), 50, out _);
        poor.Reserves = OwnBankRules.ReserveNeed(poor);
        poor.Capital -= poor.Interbank;
        poor.Loans[0] += 0;
        poor.Reserves = poor.Savings + poor.Capital + poor.Interbank - poor.Lent - poor.InArrears - poor.FundCost - poor.OtherAssets;
        int bpBefore = poor.InterbankBp;
        var d3 = new BankDay();
        OwnBankRules.InterbankDay(poor, poor.InterbankDue, run.YearDays, run.MonthDays, d3);
        Check(poor.Interbank > 0 && d3.Rolled && poor.InterbankBp == bpBefore + BankRisk.InterbankRoll && poor.InterbankDue > 0 && OwnBankRules.Balanced(poor),
            "bank risk: a loan the bank cannot repay on its day runs on for another month at a penalty rate, and is told");
        long paid = OwnBankRules.Repay(rich, 1);
        Check(paid == 0 && RepayOnce(t) && OwnBankRules.Balanced(t),
            "bank risk: early repayment takes only cash over the reserve and keeps the books balanced");
        // The owner's capital: what it takes to be over the warning line.
        OwnBank w = GrownBank(BankTiers.National, 87);
        w.Risk = BankRisk.Hard;
        long toClear = OwnBankRules.CapitalToClear(w);
        OwnBankRules.PutIn(w, toClear);
        Check(OwnBankRules.CapitalToClear(w) == 0 && OwnBankRules.Ratio(w) >= OwnBankRules.LendRatioOf(w) && !OwnBankRules.Warned(w),
            "bank risk: the capital the page says would clear the warning line does clear it");
        // Tools end a run that would otherwise run on: the same bank twice, once helped.
        int daysHelped = RunLength(true, out bool helpedClosed), daysAlone = RunLength(false, out bool aloneClosed);
        Check(daysHelped >= 0 && daysAlone >= 0 && (!helpedClosed || (aloneClosed && daysHelped >= daysAlone)),
            "bank risk: a run met with capital, a sale and borrowed money ends, or lasts no shorter than one left alone (" + daysHelped + " days" + (helpedClosed ? " to the bank's closing" : " to its end")
            + " against " + daysAlone + (aloneClosed ? " to the bank's closing" : " to its end") + ")");
    }

    // Days a run lasts in a recession on Standard for a thin bank, with the tools used on its first day or not; -1 if the bank closes first.
    private static int RunLength(bool help, out bool closed)
    {
        BankCity c = RiskCity(BankRisk.Standard, CycleState.Slowdown);
        OwnBank b = GrownBank(BankTiers.National, 90);
        b.Risk = BankRisk.Standard;
        long capital = OwnBankRules.CapitalNow(b);
        b.Scare = OwnBankCatalogue.ScareMost;
        b.Capital -= capital / 3;
        b.Reserves -= capital / 3;
        var day = new BankDay();
        int started = -1, first = b.LastDay + 1;
        closed = false;
        for (int d = first; d < first + 300 && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
            if (day.RunStarted && started < 0)
            {
                started = d;
                if (help)
                {
                    OwnBankRules.PutIn(b, OwnBankRules.CapitalToClear(b) + capital / 2);
                    OwnBankRules.Borrow(b, c, OwnBankRules.BorrowRoom(b), d, out _);
                    OwnBankRules.SellLoans(b, c, OwnBankCatalogue.Good, OwnBankRules.SaleMostFace(b, OwnBankCatalogue.Good), out _, out _);
                }
            }
            if (started >= 0 && day.RunEnded)
            {
                return d - started;
            }
            if (day.Closed)
            {
                closed = true;
                return started >= 0 ? d - started : -1;
            }
        }
        return started >= 0 ? 300 : -1;
    }

    // ---------------------------------------------------------------- the waterfall

    private static void RiskWaterfall()
    {
        var rnd = new Random(2026);
        bool adds = true, owner = true, order = true, test = true, old = true, wound = true;
        for (int i = 0; i < 300; i++)
        {
            var b = new OwnBank();
            b.State = OwnBankState.Running;
            b.Reserves = rnd.Next(0, 900000000);
            b.CounterFees = rnd.Next(0, 2) * rnd.Next(0, 1000000);
            b.Loans[2] = rnd.Next(0, 800000000);
            b.Arrears[1] = rnd.Next(0, 100000000);
            b.Deposits[0] = rnd.Next(0, 1500000000);
            b.Deposits[1] = rnd.Next(0, 800000000);
            b.Interbank = rnd.Next(0, 3) == 0 ? 0 : rnd.Next(0, 600000000);
            b.CardBal[1] = rnd.Next(0, 3) == 0 ? 0 : rnd.Next(0, 200000000);
            b.CardArr[1] = rnd.Next(0, 3) == 0 ? 0 : rnd.Next(0, 20000000);
            b.Holdings.Add(new FundHolding { StockId = 1, Qty = rnd.Next(0, 5000), LastCents = rnd.Next(1, 5000), CostCents = 1 });
            b.Book.Add(new FundHolding { StockId = 2, Qty = rnd.Next(0, 5000), LastCents = rnd.Next(1, 5000), CostCents = 1 });
            b.Test = i % 17 == 0;
            int cycle = rnd.Next(800, 1001);
            Waterfall w = OwnBankRules.Resolve(b, cycle);
            long rest = Math.Max(0, w.Assets);
            adds &= w.Savers + w.Banks + w.Fee + w.Owner == rest && w.Savers + w.SaversLoss == b.Savings && w.Banks + w.BanksLoss == b.Interbank;
            owner &= w.Owner >= 0 && w.Fee >= 0 && w.Savers >= 0 && w.Banks >= 0;
            order &= (w.Savers == b.Savings || (w.Banks == 0 && w.Owner == 0 && w.Fee == 0)) && (w.Banks == b.Interbank || (w.Owner == 0 && w.Fee == 0));
            test &= !b.Test || w.Owner == 0;
            if (b.Interbank == 0 && b.CardBook == 0 && b.CardArrears == 0 && b.Book.Count > 0)
            {
                // the old formula with the own book counted as the fund
                long assets = b.Reserves + b.CounterFees + b.Lent * OwnBankCatalogue.WindPerforming / 1000 * cycle / 1000 + b.InArrears * OwnBankCatalogue.WindArrears / 1000
                              + (b.FundValue + b.BookValue) * OwnBankCatalogue.WindFund / 1000;
                long left = b.Test ? 0 : Math.Max(0, assets - b.Savings) * (1000 - OwnBankCatalogue.WindFee) / 1000;
                old &= left == w.Owner;
            }
            OwnBank copy = new OwnBank();
            OwnBankRules.Found(copy, 1, 1, 1, "x");
            copy.Reserves = b.Reserves;
            copy.Deposits[0] = b.Deposits[0];
            copy.Interbank = b.Interbank;
            copy.Capital = copy.Reserves - copy.Deposits[0] - copy.Interbank + 0;
            copy.Loans[0] = 0;
            copy.Reserves = copy.Savings + copy.Capital + copy.Interbank;
            long expect = OwnBankRules.Resolve(copy, 1000).Owner;
            long got = OwnBankRules.WindUp(copy, 5, ClosedWhy.Collapse, 1000);
            wound &= got == expect && copy.State == OwnBankState.Closed && copy.Interbank == 0 && copy.CardBook == 0 && copy.Book.Count == 0;
        }
        Check(adds && owner, "bank risk: when the regulator takes a bank the waterfall adds up to what its assets fetch, in 300 banks: savers paid first, then the banks it borrowed from, " +
            "the charge, the owner");
        Check(order && test, "bank risk: the owner has nothing until savers and the banks it borrowed from are paid in full, and a test's bank leaves nothing");
        Check(old, "bank risk: with no money borrowed and no cards the waterfall is the winding up's old formula to the cent");
        Check(wound, "bank risk: winding a bank up pays what the waterfall says, and clears every balance it held, the borrowed money, the cards and the own book");
        OwnBank slump = GrownBank(BankTiers.National, 91);
        slump.Risk = BankRisk.Hard;
        long calm = OwnBankRules.Resolve(slump, OwnBankRules.WindFactor(slump, RiskCity(2, CycleState.Steady))).Owner, recession = OwnBankRules.Resolve(slump, OwnBankRules.WindFactor(slump,
            RiskCity(2, CycleState.Recession))).Owner;
        Check(recession < calm, "bank risk: a bank taken in a recession fetches less for its loans than one taken in a steady city (" + Dollars(recession) + " against " + Dollars(calm)
            + " left to the owner)");
        // A closing in the day carries the waterfall.
        OwnBank closing = GrownBank(BankTiers.Regional, 92);
        BankCity c = RiskCity(2, CycleState.Recession);
        closing.Capital = 1;
        var day = new BankDay();
        closing.Reserves = closing.Savings + closing.Capital + closing.Interbank - closing.Lent - closing.InArrears - closing.FundCost - closing.OtherAssets;
        bool closed = false;
        for (int d = closing.LastDay + 1; d < closing.LastDay + 40 && closing.Running && !closed; d++)
        {
            OwnBankRules.Day(closing, c, d, null, day);
            closed = day.Closed;
        }
        Check(closed && day.Waterfall.Owner == day.WoundUp && day.Waterfall.Assets >= 0, "bank risk: a day that closes the bank carries the waterfall and what the owner was left");
    }

    // ---------------------------------------------------------------- off is as before, and the regulator's ladder

    private static void RiskOff()
    {
        BankCity c = RiskCity(0, CycleState.Recession);
        OwnBank b = TierBank(c, BankTiers.National, 101);
        var day = new BankDay();
        long funding = 0, sale = 0, cards = 0, desk = 0;
        bool balanced = true, lines = true;
        for (int d = 2; d < 2 + 3 * TierYear && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
            funding += day.Funding;
            sale += day.SaleLoss;
            cards += day.CardInterest + day.CardInterchange + day.CardFees + day.CardLost + day.CardFraud + day.CardCosts;
            desk += day.Brokerage + day.Derivs + day.DeskCosts;
            balanced &= !b.Running || OwnBankRules.Balanced(b);
            BankLines l = BankLines.Of(day);
            lines &= l.In == day.Income && l.Out == day.Costs && l.Cards == 0 && l.Desk == 0 && l.Funding == 0 && l.CardCosts == 0 && l.DeskCosts == 0;
        }
        Check(balanced && funding == 0 && sale == 0 && cards == 0 && desk == 0 && b.Interbank == 0 && b.CardBook == 0 && b.BookValue == 0 && b.Risk == 0 && lines,
            "bank risk: with the risk off and the cards and the desk closed, three years in a recession book nothing of the new parts and the lines are the old ones");
        // The regulator's ladder with the risk on: a watched bank lends a part, pays nothing out; off, a watched bank is as before.
        foreach (int level in new[] { BankRisk.Standard, BankRisk.Hard })
        {
            OwnBank w = GrownBank(BankTiers.National, 102);
            w.Risk = level;
            long target = OwnBankRules.RiskWeighted(w) * (OwnBankCatalogue.WatchRatio - 10) / 1000;
            long shortBy = OwnBankRules.CapitalNow(w) - target;
            w.Capital -= shortBy;
            w.Reserves -= shortBy;
            w.Stage = Regulator.Watch;
            w.PayMode = BankTiers.PayShareMode;
            w.PayShare = 500;
            long pay = OwnBankRules.OwnerPay(w, 100000000, 1000, out long wanted, out int held);
            long out_ = OwnBankRules.MostOut(w, 1000);
            w.DivEvery = BankDividends.Weekly;
            w.DivShare = 1000;
            w.DivFrom = 1;
            w.DivKept = 100000000;
            var d = new BankDay();
            OwnBankRules.Dividends(w, RiskCity(level), 7, d);
            Check(OwnBankRules.WatchHeld(w) && pay == 0 && held == BankTiers.PayHeldWatch && out_ == 0 && d.PaidOut == 0 && w.DivWhy == BankDividends.Watch
                && OwnBankRules.LendScale(w) == BankRisk.WatchLend[level],
                "bank risk: " + BankRisk.NameKeys[level] + ": a watched bank pays its owner nothing and no dividend, says why, and lends only " + BankRisk.WatchLend[level] / 10
                    + "% of what it would");
            w.Risk = BankRisk.Off;
            Check(!OwnBankRules.WatchHeld(w) && OwnBankRules.LendScale(w) == 1000 && OwnBankRules.MostOut(w, 1000) >= 0,
                "bank risk: with the risk off a watched bank is held to nothing more than before");
        }
        OwnBank warn = GrownBank(BankTiers.National, 103);
        warn.Risk = BankRisk.Hard;
        long lend = OwnBankRules.RiskWeighted(warn) * (BankRisk.LendRatio[2] - 5) / 1000;
        long gap = OwnBankRules.CapitalNow(warn) - lend;
        warn.Capital -= gap;
        warn.Reserves -= gap;
        Check(OwnBankRules.Warned(warn) && warn.Stage == Regulator.Clear && OwnBankRules.Ratio(warn) >= OwnBankCatalogue.WatchRatio,
            "bank risk: a clear bank under its warning line is warned, and not yet watched");
        BankCity hard = RiskCity(2);
        var wd = new BankDay();
        long lentBefore = warn.Lent + warn.CardBook;
        OwnBankRules.Day(warn, hard, warn.LastDay + 1, null, wd);
        Check(wd.Lent == 0 && warn.Lent + warn.CardBook <= lentBefore + 1, "bank risk: under its warning line the bank lends nothing further");
    }

    // ---------------------------------------------------------------- the save

    private static OwnBank FullBank()
    {
        BankCity c = RiskCity(2);
        OwnBank b = TierBank(c, BankTiers.National, 111, 5000000000L);
        b.Risk = BankRisk.Hard;
        b.Interbank = 12345678;
        b.InterbankDue = 77;
        b.InterbankBp = 640;
        b.SoldFace = 900000;
        b.SoldLoss = 55000;
        b.SignsTold = 40;
        b.ShockTold = 9;
        b.SaleLossPend = 111;
        b.FundingPend = 222;
        b.CardsOn = true;
        b.CardAuto = 780;
        b.CardDecline = 640;
        b.CardRate = -150;
        b.CardFee = 2;
        b.CardLostAll = 8888;
        b.OwnCard = true;
        b.OwnOwed = 456789;
        b.OwnLostAll = 321;
        b.OwnLimit = 5000000;
        b.OwnRateBp = 1475;
        b.OwnDrawn = 1; b.OwnPaid = 2; b.OwnInterest = 3; b.OwnFees = 4; b.OwnLost = 5;
        for (int j = 0; j < CardTerms.Kinds; j++)
        {
            b.CardIssue[j] = j % 2 == 0;
            b.CardLimit[j] = j % CardTerms.LimitSteps;
            b.CardHolders[j] = 100 + j;
            b.CardBal[j] = 1000 + j;
            b.CardArr[j] = 10 + j;
            b.CardQueue[j] = 20 + j;
            b.CardRisk[j] = 900 + 10 * j;
        }
        for (int i = 0; i < CardTerms.Figures; i++)
        {
            b.CardFig[i] = 100 + i;
            b.CardLast[i] = 200 + i;
        }
        b.DeskOn = true;
        b.DeskMandate = BankDesk.Income;
        b.DeskShare = 150;
        b.DeskLimit = 125;
        b.DeskPeak = 99999;
        b.DeskCutDay = 33;
        b.DeskCuts = 2;
        b.DeskBrokerage = 1; b.DeskDerivs = 2; b.DeskCommissionAll = 3; b.DeskLastBrokerage = 4; b.DeskLastDerivs = 5;
        b.DeskDirty = true;
        b.Book.Add(new FundHolding { StockId = 5, Ticker = "AB|C", Name = "Alpha; Beta", Qty = 40, CostCents = 8000, LastCents = 210, DpsCents = 3 });
        b.DivEvery = BankDividends.Quarterly;
        b.DivShare = 450;
        return b;
    }

    private static void RiskStore()
    {
        OwnBank b = FullBank();
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out int version);
        bool fields = back != null && version == OwnBankStore.Version && back.Risk == BankRisk.Hard && back.Interbank == 12345678 && back.InterbankDue == 77 && back.InterbankBp == 640
            && back.SoldFace == 900000
                      && back.SoldLoss == 55000 && back.SignsTold == 40 && back.ShockTold == 9 && back.SaleLossPend == 111 && back.FundingPend == 222 && back.CardsOn && back.CardAuto == 780
                          && back.CardDecline == 640
                      && back.CardRate == -150 && back.CardFee == 2 && back.CardLostAll == 8888 && back.OwnCard && back.OwnOwed == 456789 && back.OwnLostAll == 321
                          && back.OwnLimit == 5000000 && back.OwnRateBp == 1475
                      && back.OwnDrawn == 1 && back.OwnPaid == 2 && back.OwnInterest == 3 && back.OwnFees == 4 && back.OwnLost == 5 && back.DeskOn && back.DeskMandate == BankDesk.Income
                          && back.DeskShare == 150
                      && back.DeskLimit == 125 && back.DeskPeak == 99999 && back.DeskCutDay == 33 && back.DeskCuts == 2 && back.DeskDirty && back.DeskLastDerivs == 5
                          && back.DivEvery == BankDividends.Quarterly
                      && back.Book.Count == 1 && back.Book[0].Ticker == "AB|C" && back.Book[0].Name == "Alpha; Beta" && back.Book[0].Qty == 40;
        for (int j = 0; back != null && j < CardTerms.Kinds; j++)
        {
            fields &= back.CardIssue[j] == (j % 2 == 0) && back.CardLimit[j] == j % CardTerms.LimitSteps && back.CardHolders[j] == 100 + j && back.CardBal[j] == 1000 + j && back.CardArr[j] == 10 + j
                      && back.CardQueue[j] == 20 + j && back.CardRisk[j] == 900 + 10 * j;
        }
        for (int i = 0; back != null && i < CardTerms.Figures; i++)
        {
            fields &= back.CardFig[i] == 100 + i && back.CardLast[i] == 200 + i;
        }
        Check(fields && OwnBankStore.Write(back) == line, "bank risk: every field of the risk, the cards and the desk is saved and read back, the line the same again");
        // A line written before them has none of the parts: it reads as a bank with the risk off, cards and desk closed, and balanced.
        string older = Regex.Replace(line, @"\^[rcqwtg]:[^\^]*", string.Empty);
        OwnBank oldBank = OwnBankStore.Read(older, out _);
        Check(older != line && oldBank != null && oldBank.Risk == 0 && !oldBank.CardsOn && !oldBank.DeskOn && oldBank.Interbank == 0 && oldBank.CardAuto == CardTerms.AutoDefault
            && oldBank.CardRisk[2] == 1000 && !oldBank.CardIssue[OwnBankCatalogue.Poor]
              && oldBank.CardIssue[OwnBankCatalogue.Good] && oldBank.DeskShare == BankDesk.ShareDefault && oldBank.Book.Count == 0 && oldBank.Capital == b.Capital,
            "bank risk: a line written before these parts reads as a bank with the risk off, the cards and the desk closed, and every old figure as it was");
        // Never written for a bank that never used them: still readable by the old code (parts are appended, unknown letters skipped).
        OwnBank plain = TierBank(RiskCity(0), BankTiers.Community, 112);
        string plainLine = OwnBankStore.Write(plain);
        OwnBank plainBack = OwnBankStore.Read(plainLine, out _);
        Check(plainBack != null && OwnBankStore.Write(plainBack) == plainLine && OwnBankRules.Balanced(plainBack), "bank risk: a bank that never used any of it saves and loads unchanged");
        // Garbage inside the parts is put inside its bounds.
        string odd = line.Replace("^r:" + Between(line, "^r:"), "^r:9|-5|-1|-3|-1|-1||x|-4|-4").Replace("^c:" + Between(line, "^c:"), "^c:1|99999|1|999|9|-1|1|-5|-5|-1|-1|-1|-1|-1|-9|-9");
        OwnBank bad = OwnBankStore.Read(odd, out _);
        Check(bad != null && bad.Risk == BankRisk.Hard && bad.Interbank == 0 && bad.InterbankDue == 0 && bad.CardAuto <= CardTerms.AutoMost && bad.CardDecline <= CardTerms.AutoMost
            && bad.CardRate <= CardTerms.RateMost
              && bad.CardFee <= CardTerms.FeeSteps.Length - 1 && bad.OwnOwed == 0 && bad.OwnPaid == 0 && bad.OwnLimit == 0,
                  "bank risk: a damaged risk or card part is put inside its bounds, and reads, never throws");
        OwnBank mended = OwnBankStore.Read(line, out _);
        mended.Reserves += 12345;
        Check(OwnBankStore.Mend(mended) && OwnBankRules.Balanced(mended), "bank risk: a line that does not balance is mended counting the borrowed money, the cards and the own book");
    }

    private static bool RepayOnce(OwnBank t)
    {
        long before = t.Interbank, free = Math.Max(0, OwnBankRules.FreeCash(t));
        long paid = OwnBankRules.Repay(t, long.MaxValue);
        return paid == Math.Min(before, free) && t.Interbank == before - paid;
    }

    private static string Between(string line, string start)
    {
        int at = line.IndexOf(start, StringComparison.Ordinal) + start.Length, end = line.IndexOf('^', at);
        return line.Substring(at, (end < 0 ? line.Length : end) - at);
    }

    // ---------------------------------------------------------------- the card business

    private static OwnBank CardBank(int seed, int risk, CycleState cycle, int years = 1)
    {
        BankCity c = RiskCity(risk, cycle);
        OwnBank b = TierBank(c, BankTiers.National, seed, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        b.CardsOn = true;
        b.CardIssue[OwnBankCatalogue.Poor] = true;
        var day = new BankDay();
        RunBank(b, c, 2, years * TierYear, null, day);
        return b;
    }

    private static void CardBusiness()
    {
        BankCity steady = RiskCity(0);
        OwnBank b = TierBank(steady, BankTiers.National, 120, 3 * OwnBankRules.TierCapital(BankTiers.National, steady.Level, 0, 0));
        b.CardsOn = true;
        var day = new BankDay();
        long interest = 0, interchange = 0, fees = 0, costs = 0, lost = 0, fraud = 0;
        bool balanced = true, lines = true, held = true;
        for (int d = 2; d < 2 + 3 * TierYear && b.Running; d++)
        {
            OwnBankRules.Day(b, steady, d, null, day);
            interest += day.CardInterest;
            interchange += day.CardInterchange;
            fees += day.CardFees;
            costs += day.CardCosts;
            lost += day.CardLost;
            fraud += day.CardFraud;
            balanced &= !b.Running || OwnBankRules.Balanced(b);
            BankLines l = BankLines.Of(day);
            lines &= l.In == day.Income && l.Out == day.Costs && l.Cards == day.CardInterest + day.CardInterchange + day.CardFees && l.CardCosts == day.CardLost + day.CardFraud + day.CardCosts;
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                held &= b.CardHolders[j] >= 0 && b.CardBal[j] >= 0 && b.CardArr[j] >= 0 && b.CardQueue[j] >= 0 && b.CardRisk[j] > 0;
            }
        }
        Check(balanced && lines && held, "cards: three years of the card business, the books balance every day, the statement's lines add up to the day, and no balance is under nothing");
        Check(b.CardsHeld > 0 && b.CardBook > 0 && interest > 0 && interchange > 0 && lost > 0 && fraud > 0 && costs > 0,
            "cards: the book grows to " + b.CardsHeld + " cards and " + Dollars(b.CardBook)
              + " carried, earning interest and the shops' fee and losing to defaults and fraud");
        Check(interest + interchange + fees - lost - fraud - costs > 0 && interchange > fraud * 5,
            "cards: in a steady city the card business is in profit, and the shops' fee is well over fraud (" + Dollars(interest + interchange + fees - lost - fraud - costs) + " in 3 years)");
        Check(b.CardFig[CardTerms.FigInterest] + b.CardLast[CardTerms.FigInterest] <= interest && b.CardFig[CardTerms.FigSpend] > 0,
            "cards: the month's figures kept for the page are what the days booked");
        Check(OwnBankRules.RiskWeighted(b) >= b.CardBook * CardTerms.CardWeight / 1000, "cards: the regulator counts a card balance at " + CardTerms.CardWeight / 10 + "% of it");
        // Cards lose more in a recession, by the loans' own risk model.
        OwnBank boom = CardBank(121, BankRisk.Standard, CycleState.Boom, 2), slump = CardBank(121, BankRisk.Standard, CycleState.Recession, 2);
        long boomLost = boom.CardLostAll, slumpLost = slump.CardLostAll;
        double boomRate = boom.CardBook > 0 ? boomLost / (double)boom.CardBook : 0, slumpRate = slump.CardBook > 0 ? slumpLost / (double)slump.CardBook : 1;
        Check(boomLost >= 0 && slumpRate > boomRate * 1.3, "cards: card losses follow the economy by the same risk model as the loans' (" + (boomRate * 100).ToString("0.0",
            CultureInfo.InvariantCulture) + "% of the book in a boom, "
              + (slumpRate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% in a recession)");
        BankCity c = RiskCity(1, CycleState.Recession);
        Check(OwnBankRules.DefaultPerMille(c, slump, OwnBankCatalogue.Fair) * CardTerms.DefaultMult[OwnBankCatalogue.Fair] / 1000 > OwnBankRules.DefaultPerMille(c, slump, OwnBankCatalogue.Fair),
            "cards: a card has no security, so it fails more often than a loan of the same kind");
        // The owner's choices.
        OwnBank rates = TierBank(steady, BankTiers.National, 122, 3 * OwnBankRules.TierCapital(BankTiers.National, steady.Level, 0, 0));
        rates.Deposits[0] = 20000000000L;
        rates.Reserves += rates.Deposits[0];
        int normal = OwnBankRules.CardWanted(steady, rates, OwnBankCatalogue.Fair);
        rates.CardRate = 500;
        int dear = OwnBankRules.CardWanted(steady, rates, OwnBankCatalogue.Fair);
        rates.CardRate = -500;
        int cheap = OwnBankRules.CardWanted(steady, rates, OwnBankCatalogue.Fair);
        rates.CardRate = 0;
        rates.CardFee = 3;
        int fee = OwnBankRules.CardWanted(steady, rates, OwnBankCatalogue.Fair);
        Check(dear < normal && normal < cheap && fee < normal && normal > 0,
            "cards: a dearer card or a yearly fee brings fewer clients, a cheaper one more (" + dear + ", " + normal + ", " + cheap + " cards; a fee " + fee + ")");
        OwnBank off = TierBank(steady, BankTiers.National, 123, 3 * OwnBankRules.TierCapital(BankTiers.National, steady.Level, 0, 0));
        off.Deposits[0] = 20000000000L;
        off.Reserves += off.Deposits[0];
        off.CardsOn = false;
        RunBank(off, steady, 2, 2 * TierYear, null, day);
        Check(off.CardsHeld == 0 && off.CardBook == 0, "cards: a bank with the card business closed issues no card");
        OwnBank some = TierBank(steady, BankTiers.National, 124, 3 * OwnBankRules.TierCapital(BankTiers.National, steady.Level, 0, 0));
        some.CardsOn = true;
        some.CardIssue[OwnBankCatalogue.Fair] = false;
        some.CardIssue[OwnBankCatalogue.Good] = true;
        RunBank(some, steady, 2, 2 * TierYear, null, day);
        Check(some.CardHolders[OwnBankCatalogue.Fair] == 0 && some.CardHolders[OwnBankCatalogue.Good] > 0, "cards: a kind switched off is issued no card, the others are");
    }

    private static void CardApproval()
    {
        for (int j = 0; j < CardTerms.Kinds; j++)
        {
            OwnBankRules.CardShares(j, 720, 600, out int auto, out int queue, out int autoMult, out int queueMult);
            OwnBankRules.CardShares(j, 520, 520, out int allAuto, out int noQueue, out int looseMult, out _);
            OwnBankRules.CardShares(j, 820, 420, out int fewAuto, out int manyQueue, out int fewMult, out _);
            Check(auto >= 0 && queue >= 0 && auto + queue <= 1000 && allAuto >= auto && noQueue == 0 && fewAuto <= auto && manyQueue >= queue && queueMult > autoMult && looseMult > fewMult,
                "cards: " + OwnBankCatalogue.SegmentKeys[j] + ": the lines split applicants by score into approved, queued and declined (" + auto / 10 + "%, " + queue / 10
                    + "%), the lower the score the worse the client");
        }
        BankCity c = RiskCity(0);
        OwnBank b = CardBank(130, BankRisk.Off, CycleState.Steady, 2);
        int queued = b.CardQueue[OwnBankCatalogue.Fair], good = b.CardHolders[OwnBankCatalogue.Fair];
        long risk = b.CardRisk[OwnBankCatalogue.Fair];
        Check(queued > 0 || good > 0, "cards: a bank that has run two years has applicants approved or waiting for its owner");
        b.CardQueue[OwnBankCatalogue.Fair] = 500;
        int decided = OwnBankRules.CardDecide(b, OwnBankCatalogue.Fair, true);
        Check(decided == 500 && b.CardQueue[OwnBankCatalogue.Fair] == 0 && b.CardHolders[OwnBankCatalogue.Fair] == good + 500 && b.CardRisk[OwnBankCatalogue.Fair] >= risk,
            "cards: approving the applicants in the queue makes them holders at the worse defaults their scores bring, all together");
        b.CardQueue[OwnBankCatalogue.Fair] = 300;
        int declined = OwnBankRules.CardDecide(b, OwnBankCatalogue.Fair, false);
        Check(declined == 300 && b.CardQueue[OwnBankCatalogue.Fair] == 0 && b.CardHolders[OwnBankCatalogue.Fair] == good + 500 && OwnBankRules.CardDecide(b, OwnBankCatalogue.Fair, true) == 0,
            "cards: declining the queue sends them away, and an empty queue answers nothing");
        // Looser lines bring worse clients: the same bank on two settings.
        OwnBank loose = TierBank(c, BankTiers.National, 131, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0)), tight = TierBank(c, BankTiers.National, 131,
            3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        loose.CardsOn = tight.CardsOn = true;
        loose.CardAuto = loose.CardDecline = 520;
        tight.CardAuto = tight.CardDecline = 720;
        var day = new BankDay();
        RunBank(loose, c, 2, 2 * TierYear, null, day);
        RunBank(tight, c, 2, 2 * TierYear, null, day);
        Check(loose.CardsHeld > tight.CardsHeld && loose.CardRisk[OwnBankCatalogue.Fair] > tight.CardRisk[OwnBankCatalogue.Fair] && tight.CardQueue[OwnBankCatalogue.Fair] == 0
            && loose.CardQueue[OwnBankCatalogue.Fair] == 0,
            "cards: looser lines bring more clients and worse (" + loose.CardsHeld + " cards at risk " + loose.CardRisk[OwnBankCatalogue.Fair] + ", against " + tight.CardsHeld + " at "
                + tight.CardRisk[OwnBankCatalogue.Fair] + ")");
        Check(OwnBankRules.OwnCardMost(tight, c.Level) <= OwnBankRules.CapitalNow(tight) * CardTerms.BlackMostShare / 1000 && OwnBankRules.OwnCardMost(NewBank(c, 1, 3, 100000), c.Level) == 0,
            "cards: the owner's card may be no more than a tenth of the capital and not at all for a bank too small for its least limit");
    }

    // ---------------------------------------------------------------- the owner's own card

    private static void CardOwner()
    {
        BankCity c = RiskCity(0);
        OwnBank b = TierBank(c, BankTiers.National, 140, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        b.CardsOn = true;
        OwnCardSink.Bank = b;
        b.OwnCard = true;
        long reserves = b.Reserves, capital = b.Capital;
        OwnCardSink.Moved(true, OwnCardSink.Drawn, 5000);
        OwnCardSink.Moved(true, OwnCardSink.Fee, 150);
        Check(b.OwnDrawn == 500000 && b.OwnFees == 15000 && OwnCardSink.CanFund(100) && !OwnCardSink.CanFund(int.MaxValue / 2),
            "owner's card: what the card does since the bank's last day is kept, and a draw the bank has not the cash for is refused");
        var day = new BankDay();
        OwnBankRules.Day(b, c, 2, null, day);
        Check(OwnBankRules.Balanced(b) && b.OwnOwed == 515000 && b.OwnDrawn == 0 && day.CardFees >= 15000 && b.Reserves < reserves,
            "owner's card: the day takes it in: its balance is an asset funded from the bank's cash, the fee income, the books balanced");
        long capitalBefore = b.Capital;
        OwnCardSink.Moved(true, OwnCardSink.Interest, 40);
        OwnCardSink.Moved(true, OwnCardSink.Paid, 2000);
        OwnBankRules.Day(b, c, 3, null, day);
        Check(OwnBankRules.Balanced(b) && b.OwnOwed == 515000 + 4000 - 200000 && day.CardInterest >= 4000 && b.Capital > capitalBefore - 5000000,
            "owner's card: interest is the bank's income and a payment brings cash back, the books balanced");
        long owed = b.OwnOwed;
        OwnCardSink.Moved(true, OwnCardSink.Lost, 1000);
        long capNow = b.Capital;
        OwnBankRules.Day(b, c, 4, null, day);
        Check(OwnBankRules.Balanced(b) && b.OwnOwed == owed - 100000 && b.OwnLostAll == 100000 && day.CardLost >= 100000 && capNow > 0,
            "owner's card: a claim or a bankruptcy writes the card off against the bank's capital like any client's loss");
        long weighted = OwnBankRules.RiskWeighted(b);
        b.OwnOwed += 100000000;
        b.Reserves -= 100000000;
        Check(OwnBankRules.RiskWeighted(b) - weighted >= 100000000L * CardTerms.BlackWeight / 1000 - 1 && OwnBankRules.Balanced(b),
            "owner's card: the owner is exposed like any client: the balance is weighed for the regulator");
        OwnCardSink.Moved(false, OwnCardSink.Drawn, 100);
        OwnCardSink.Bank = null;
        OwnCardSink.Moved(true, OwnCardSink.Drawn, 100);
        Check(b.OwnDrawn == 0 && !OwnCardSink.CanFund(1), "owner's card: a card that is not the owner's, or no bank to hear it, moves nothing");
        // A bank over the owner's card: its cash is the limit; the credit file's one-card rule never sees it.
        CreditFile file = new CreditFile();
        file.Add(new CreditAccount { Kind = ProductKind.CreditCard, Agreed = 100, Own = true });
        Check(file.Account(ProductKind.CreditCard) == null && file.OwnCard() != null && file.OwnCard().Own,
            "owner's card: the credit file's one card in all is the city's banks' card, and the owner's own card is held beside it");
        file.Add(new CreditAccount { Kind = ProductKind.CreditCard, Agreed = 100 });
        Check(file.Account(ProductKind.CreditCard) != null && !file.Account(ProductKind.CreditCard).Own && file.OwnCard().Own, "owner's card: a card of a city's bank may be held with it");
        // The account keeps the flag in the credit file's line, and a line from before it has none.
        string text = CreditStore.Write(file);
        CreditFile read = CreditStore.Read(text, out _);
        Check(read != null && read.OwnCard() != null && read.Accounts.Count == 2, "owner's card: the credit file saves and reads back which card is the owner's");
        // Hearing it through the accounts' own arithmetic.
        OwnBank heard = TierBank(c, BankTiers.National, 141, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        heard.CardsOn = true;
        heard.OwnCard = true;
        OwnCardSink.Bank = heard;
        CreditAccount card = AccountRules.Open(ProductKind.CreditCard, 1000, 1500, 1);
        card.Own = true;
        bool drew = AccountRules.Draw(card, 400, out int cashFee);
        int paidIn = AccountRules.Pay(card, 100);
        Check(drew && heard.OwnDrawn == 40000 && heard.OwnFees == cashFee * 100L && heard.OwnPaid == paidIn * 100L && card.Owed == 400 + cashFee - paidIn,
            "owner's card: the card's own arithmetic tells the bank what is drawn, its fee and what is paid, to the whole money");
        OwnCardSink.Bank = null;
    }

    // ---------------------------------------------------------------- the desk

    private static List<FundQuote> MarketOf(int count, int price, int step)
    {
        List<FundQuote> q = Quotes(count, price);
        for (int i = 0; i < q.Count; i++)
        {
            FundQuote f = q[i];
            f.Distress = (i % 7) * 20;
            f.DpsCents = 20 + (i * 37) % 160;
            f.LastCents = price + step * (i % 5);
            q[i] = f;
        }
        return q;
    }

    private static void DeskBrokerage()
    {
        BankCity c = RiskCity(0);
        long extra = 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0);
        long[] pay = new long[4];
        int[] activity = { 0, 1000, 2000, 3000 };
        for (int a = 0; a < activity.Length; a++)
        {
            OwnBank b = TierBank(c, BankTiers.National, 150, extra);
            b.DeskOn = true;
            b.Deposits[0] = 30000000000L;
            b.Deposits[1] = 10000000000L;
            b.Reserves += b.Deposits[0] + b.Deposits[1];
            BankCity m = c;
            m.Activity = activity[a];
            var day = new BankDay();
            OwnBankRules.Day(b, m, 2, null, day);
            pay[a] = day.Brokerage;
            Check(OwnBankRules.Balanced(b) && BankLines.Of(day).Desk == day.Brokerage + day.Derivs,
                "desk: a day of brokerage balances the books and is one line of the statement (activity " + activity[a] + ")");
        }
        Check(pay[0] == 0 && pay[1] > 0 && Math.Abs(pay[2] - 2 * pay[1]) <= pay[1] / 20 + 2 && pay[3] > pay[2],
            "desk: the clients' commission follows the market's activity, nothing while the market is not running (" + pay[1] + ", " + pay[2] + ", " + pay[3] + " cents a day)");
        OwnBank closed = TierBank(c, BankTiers.National, 150, extra);
        closed.Deposits[0] = 30000000000L;
        closed.Reserves += closed.Deposits[0];
        BankCity live = c;
        live.Activity = 1000;
        var d2 = new BankDay();
        OwnBankRules.Day(closed, live, 2, null, d2);
        Check(d2.Brokerage == 0 && d2.Derivs == 0 && d2.DeskCosts == 0, "desk: a desk that is closed earns and costs nothing");
        OwnBank with = TierBank(c, BankTiers.National, 150, extra), without = TierBank(c, BankTiers.National, 150, extra);
        with.DeskOn = without.DeskOn = true;
        with.Deposits[0] = without.Deposits[0] = 30000000000L;
        with.Reserves += with.Deposits[0];
        without.Reserves += without.Deposits[0];
        BankCity futures = c, none = c;
        futures.Activity = none.Activity = 1000;
        futures.Derivs = true;
        var dw = new BankDay();
        var dn = new BankDay();
        OwnBankRules.Day(with, futures, 2, null, dw);
        OwnBankRules.Day(without, none, 2, null, dn);
        Check(dw.Derivs > 0 && dn.Derivs == 0 && dw.Brokerage == dn.Brokerage,
            "desk: options and futures earn the desk a commission only while the exchange has them: " + dw.Derivs + " cents a day against none");
        BankCity wild = futures;
        wild.Activity = 2500;
        BankCity calm = futures;
        calm.Activity = 1500;
        var ww = new BankDay();
        var wc = new BankDay();
        OwnBank a1 = TierBank(c, BankTiers.National, 150, extra), a2 = TierBank(c, BankTiers.National, 150, extra);
        a1.DeskOn = a2.DeskOn = true;
        a1.Deposits[1] = a2.Deposits[1] = 20000000000L;
        a1.Reserves += a1.Deposits[1];
        a2.Reserves += a2.Deposits[1];
        OwnBankRules.Day(a1, wild, 2, null, ww);
        OwnBankRules.Day(a2, calm, 2, null, wc);
        Check(ww.DeskCosts > wc.DeskCosts * 2 / 2 && ww.DeskCosts - wc.DeskCosts > 0 && wc.DeskCosts > 0,
            "desk: a very wild market costs the desk its clients' failed margin calls on top of its running costs");
    }

    private static void DeskOwnBook()
    {
        BankCity c = RiskCity(0);
        c.Activity = 1000;
        long extra = 6 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0);
        foreach (int mandate in new[] { BankDesk.Index, BankDesk.Income, BankDesk.Growth })
        {
            OwnBank b = TierBank(c, BankTiers.National, 160 + mandate, extra);
            b.DeskOn = true;
            b.DeskMandate = mandate;
            b.DeskShare = 200;
            b.DeskDirty = true;
            List<FundQuote> q = MarketOf(60, 5000, 10);
            var day = new BankDay();
            bool balanced = true, eligible = true, share = true, marked = true;
            for (int d = 2; d < 40 && b.Running; d++)
            {
                OwnBankRules.Day(b, c, d, q, day);
                balanced &= OwnBankRules.Balanced(b);
                share &= b.BookValue <= OwnBankRules.CapitalNow(b) * 210 / 1000 + 1 || b.BookValue <= OwnBankRules.DeskRoom(b) + 100000;
                for (int i = 0; i < b.Book.Count; i++)
                {
                    FundQuote own = q[b.Book[i].StockId - 1];
                    eligible &= OwnBankRules.DeskEligible(own, mandate);
                    marked &= b.Book[i].LastCents == own.LastCents;
                }
            }
            Check(balanced && eligible && share && marked && b.Book.Count > 0 && b.Book.Count <= BankDesk.Companies[mandate],
                "desk: the " + BankDesk.MandateKeys[mandate] + " mandate holds " + b.Book.Count + " of its companies, within its share of the capital, marked to the day's prices, the books balanced");
        }
        // Income picks the highest yields; index the largest.
        OwnBank inc = TierBank(c, BankTiers.National, 170, extra), idx = TierBank(c, BankTiers.National, 170, extra);
        inc.DeskOn = idx.DeskOn = true;
        inc.DeskMandate = BankDesk.Income;
        idx.DeskMandate = BankDesk.Index;
        inc.DeskShare = idx.DeskShare = 200;
        inc.DeskDirty = idx.DeskDirty = true;
        List<FundQuote> market = MarketOf(60, 5000, 10);
        var dd = new BankDay();
        for (int d = 2; d < 12; d++)
        {
            OwnBankRules.Day(inc, c, d, market, dd);
            OwnBankRules.Day(idx, c, d, market, dd);
        }
        double yieldInc = 0, yieldIdx = 0;
        for (int i = 0; i < inc.Book.Count; i++)
        {
            yieldInc += inc.Book[i].DpsCents / (double)Math.Max(1, inc.Book[i].LastCents) / Math.Max(1, inc.Book.Count);
        }
        for (int i = 0; i < idx.Book.Count; i++)
        {
            yieldIdx += idx.Book[i].DpsCents / (double)Math.Max(1, idx.Book[i].LastCents) / Math.Max(1, idx.Book.Count);
        }
        Check(inc.Book.Count > 0 && idx.Book.Count > 0 && yieldInc > yieldIdx, "desk: the income mandate holds companies that pay more than the index's");
        // The risk limit: prices fall, the loss from the peak passes the limit and the book is cut to what a cut keeps; nothing is bought for a month.
        OwnBank cut = TierBank(c, BankTiers.National, 171, extra);
        cut.DeskOn = true;
        cut.DeskMandate = BankDesk.Index;
        cut.DeskShare = 250;
        cut.DeskLimit = 100;
        cut.DeskDirty = true;
        List<FundQuote> prices = MarketOf(60, 5000, 10);
        for (int d = 2; d < 20; d++)
        {
            OwnBankRules.Day(cut, c, d, prices, dd);
        }
        long peak = cut.BookValue, cost = cut.BookCost;
        for (int i = 0; i < prices.Count; i++)
        {
            FundQuote f = prices[i];
            f.LastCents = f.LastCents * 80 / 100;
            prices[i] = f;
        }
        OwnBankRules.Day(cut, c, 20, prices, dd);
        bool cutNow = dd.DeskCut && cut.DeskCuts == 1 && cut.DeskCutDay == 20;
        long afterCut = cut.BookValue;
        Check(peak > 0 && cutNow && afterCut <= peak * 80 / 100 * (BankDesk.CutKeep + 20) / 1000 && afterCut > 0 && OwnBankRules.Balanced(cut) && cut.DeskPeak == afterCut,
            "desk: a fall of a fifth from the peak passes a limit of " + cut.DeskLimit / 10 + "% and cuts the book to about half (" + Dollars(peak) + " to " + Dollars(afterCut)
                + "), the books balanced");
        long costAfter = cut.BookCost;
        for (int d = 21; d < 20 + BankDesk.CutMonths * c.MonthDays; d++)
        {
            OwnBankRules.Day(cut, c, d, prices, dd);
        }
        Check(cut.BookCost <= costAfter + 1 && cut.BookCost < cost, "desk: after a cut nothing is bought for a month");
        // Closed, the book is sold, and a fire sale sells the book first.
        cut.DeskOn = false;
        OwnBankRules.Day(cut, c, 20 + BankDesk.CutMonths * c.MonthDays, prices, dd);
        Check(cut.Book.Count == 0 && OwnBankRules.Balanced(cut), "desk: closing the desk sells the book");
        OwnBank sale = TierBank(c, BankTiers.National, 172, extra);
        sale.DeskOn = true;
        sale.DeskMandate = BankDesk.Index;
        sale.DeskShare = 250;
        sale.DeskDirty = true;
        sale.FundLimit = 100;
        sale.Strategy = 1;
        List<FundQuote> mk = MarketOf(60, 5000, 10);
        for (int d = 2; d < 16; d++)
        {
            OwnBankRules.Day(sale, c, d, mk, dd);
        }
        long fund = sale.FundValue, book = sale.BookValue;
        var fs = new BankDay();
        OwnBankFund.FireSale(sale, book / 2, fs);
        Check(book > 0 && sale.BookValue < book && sale.FundValue == fund && OwnBankRules.Balanced(sale), "desk: a fire sale to meet savers sells the own book before the fund");
        long cap = OwnBankRules.CapitalNow(sale);
        Check(OwnBankRules.RiskWeighted(sale) >= (sale.FundValue + sale.BookValue) * BankDesk.Weight / 1000 && cap == sale.Capital + sale.FundValue - sale.FundCost + sale.BookValue - sale.BookCost,
            "desk: the regulator counts the own book at the fund's weight, and its gains and losses at market are in the capital");
    }

    // ---------------------------------------------------------------- dividends: quarterly, never below the capital, from profit only

    private static void GrowthDividends()
    {
        BankCity c = TierCity(0);
        Check(BankDividends.Days(BankDividends.Quarterly, c.MonthDays, c.YearDays) == Math.Min(c.YearDays, 3 * c.MonthDays) && BankDividends.Days(BankDividends.Quarterly, c.MonthDays,
            c.YearDays) < c.YearDays
              && BankDividends.Days(BankDividends.Quarterly, 5, 4) == 4 && BankDividends.Days(BankDividends.Quarterly, 30, 360) == 90,
            "dividends: a quarter is three of the game's months, never longer than its year (" + BankDividends.Days(BankDividends.Quarterly, c.MonthDays,
                c.YearDays) + " days of a " + c.YearDays + "-day year)");
        OwnBank q = DividendBank(c, BankTiers.Regional, BankDividends.Quarterly, 300, out int from);
        int days = BankDividends.Days(BankDividends.Quarterly, c.MonthDays, c.YearDays), due = OwnBankRules.DividendDay(q, c.MonthDays, c.YearDays);
        var day = new BankDay();
        var on = new List<int>(4);
        for (int d = from; d < from + 3 * days && q.Running; d++)
        {
            OwnBankRules.Day(q, c, d, null, day);
            if (day.DividendDue)
            {
                on.Add(d);
            }
        }
        Check(due == from + days - 1 && on.Count == 3 && on[0] == due && on[1] == on[0] + days && on[2] == on[1] + days, "dividends: every quarter falls due on days " + string.Join(", ",
            on) + " from day " + from);
        // The expected sum: shown before the day, no more than the bank may pay, and close to what is paid when the profit goes on as it was.
        OwnBank e = DividendBank(c, BankTiers.National, BankDividends.Monthly, 500, out int f2, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        int dueE = OwnBankRules.DividendDay(e, c.MonthDays, c.YearDays);
        long expectedEarly = 0, expectedLate = 0, paid = 0;
        for (int d = f2; d <= dueE && e.Running; d++)
        {
            if (d == f2 + 1)
            {
                expectedEarly = OwnBankRules.DividendExpected(e, c);
            }
            if (d == dueE)
            {
                expectedLate = OwnBankRules.DividendExpected(e, c);
            }
            OwnBankRules.Day(e, c, d, null, day);
            paid += day.PaidOut;
        }
        long most = OwnBankRules.MostOut(e, c.Level) + paid;
        Check(paid > 0 && expectedEarly > 0 && expectedLate > 0 && expectedEarly <= most + paid && Math.Abs(expectedLate - paid) * 4 < paid && Math.Abs(expectedEarly - paid) * 2 < paid,
            "dividends: the expected sum shown before the day is " + Dollars(expectedEarly) + " early and " + Dollars(expectedLate) + " on the last day, against " + Dollars(paid) + " paid");
        OwnBank none = DividendBank(c, BankTiers.Regional, BankDividends.Off, 500, out _);
        Check(OwnBankRules.DividendExpected(none, c) == 0, "dividends: nothing is expected while they are off");
        none.Test = true;
        OwnBankRules.SetDividends(none, BankDividends.Weekly, 500, 5);
        Check(OwnBankRules.DividendExpected(none, c) == 0, "dividends: nothing is expected of a test's bank");
        // Never below the required capital, only from profit: every dividend over years, on every level of the risk and in every economy.
        foreach (int level in new[] { 0, 1, 2 })
        {
            foreach (CycleState cycle in new[] { CycleState.Boom, CycleState.Recession })
            {
                BankCity city = RiskCity(level, cycle);
                OwnBank b = TierBank(city, BankTiers.Regional, 180 + level, 0);
                RunBank(b, city, 2, TierYear, null, new BankDay());
                int start = b.LastDay + 1;
                OwnBankRules.SetDividends(b, BankDividends.Weekly, 1000, start);
                long profit = 0, total = 0;
                bool below = false, balanced = true, watch = false;
                for (int d = start; d < start + 3 * TierYear && b.Running; d++)
                {
                    OwnBankRules.Day(b, city, d, null, day);
                    profit += day.Profit - day.Pay;
                    total += day.PaidOut;
                    balanced &= !b.Running || OwnBankRules.Balanced(b);
                    if (day.PaidOut > 0)
                    {
                        below |= OwnBankRules.CapitalNow(b) < OwnBankRules.Keep(b, city.Level) || b.Reserves < OwnBankRules.ReserveNeed(b);
                        watch |= OwnBankRules.WatchHeld(b) || b.Run || b.Stage == Regulator.Breach;
                    }
                }
                Check(!below && !watch && balanced && total <= Math.Max(0, profit),
                    "dividends: " + BankRisk.NameKeys[level] + " in a " + cycle + ": every dividend of " + Dollars(total) + " in three years left the capital over its required capital and " +
                        "the reserve, from profit alone (profit "
                    + Dollars(profit) + "), never in a watch, a run or a breach");
            }
        }
        // The tax is the mod's own rate for dividends moved by the council's business tax, and never more than the dividend.
        Check(ListingVotes.DividendTax(100000, 0) == 15000 && ListingVotes.DividendTax(100000, 1) > 15000 && ListingVotes.DividendTax(100000, -1) < 15000 && ListingVotes.DividendTax(100000,
            1) < 100000,
            "dividends: the tax withheld is the stock market's rate for dividends, " + ListingCatalogue.DividendTaxBp / 100 + "%, moved by the council's business tax");
    }

    // ---------------------------------------------------------------- the lines of the statement

    private static void GrowthLines()
    {
        foreach (string key in new[] { OwnBankCatalogue.CardsKey, OwnBankCatalogue.DeskKey })
        {
            Check(BizLines.Of(key) == BizLine.Takings && OwnBankCatalogue.IsTakings(key), "lines: " + key + " is takings in the statement");
        }
        foreach (string key in new[] { OwnBankCatalogue.CardCostsKey, OwnBankCatalogue.FundingKey, OwnBankCatalogue.DeskCostsKey })
        {
            Check(BizLines.Of(key) == BizLine.Cost && OwnBankCatalogue.CostRow(key) != null && !OwnBankCatalogue.IsTakings(key),
                "lines: " + key + " is a running cost with a row of its own among the goods");
        }
        foreach (string key in new[] { OwnBankCatalogue.DividendAutoKey, OwnBankCatalogue.DividendTaxKey })
        {
            Check(BizLines.Of(key) == BizLine.Capital, "lines: " + key + " is capital, in no statement's sales or costs");
        }
        bool listed = true;
        foreach (System.Reflection.FieldInfo field in typeof(OwnBankCatalogue).GetFields())
        {
            if (field.IsLiteral && field.FieldType == typeof(string) && ((string)field.GetRawConstantValue()).StartsWith("reallife_transaction_ownbank_", StringComparison.Ordinal))
            {
                listed &= BizLines.Of((string)field.GetRawConstantValue()) != BizLine.Unlisted;
            }
        }
        Check(listed, "lines: every transaction key of the bank is in exactly one of the statement's lists");
        // The statement's lines are the day: the market's view of a listed bank (BankDay.Income, BankDay.Profit) counts each new line once.
        BankCity c = RiskCity(1, CycleState.Slowdown);
        c.Activity = 1200;
        c.Derivs = true;
        OwnBank b = TierBank(c, BankTiers.National, 190, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        b.CardsOn = true;
        b.DeskOn = true;
        b.DeskMandate = BankDesk.Index;
        b.DeskDirty = true;
        List<FundQuote> q = MarketOf(60, 5000, 10);
        var day = new BankDay();
        bool once = true, profit = true, any = false;
        long income = 0, cost = 0;
        for (int d = 2; d < 2 + 3 * TierYear && b.Running; d++)
        {
            if (d == 70)
            {
                OwnBankRules.Borrow(b, c, OwnBankRules.ToStep(OwnBankRules.BorrowRoom(b) / 4), d, out _);
                OwnBankRules.SellLoans(b, c, OwnBankCatalogue.Good, OwnBankRules.SaleMostFace(b, OwnBankCatalogue.Good) / 2, out _, out _);
            }
            OwnBankRules.Day(b, c, d, q, day);
            BankLines l = BankLines.Of(day);
            once &= l.In == day.Income && l.Out == day.Costs && l.Takings + l.Cards + l.Desk == day.Income
                && l.Savers + l.Losses + l.CardCosts + l.Funding + l.DeskCosts + l.Wages + l.Systems + l.Fine == day.Costs;
            profit &= day.Profit == l.In - l.Out;
            any |= l.Cards > 0 && l.Desk > 0 && l.Funding > 0 && l.CardCosts > 0 && l.DeskCosts > 0;
            income += day.Income;
            cost += day.Costs;
        }
        Check(once && profit && any && OwnBankRules.Balanced(b),
            "lines: three years of every part running, each day's lines add up to its income and its costs, so the statement, the tax and a listed bank's books count each line once");
        // The market's quarters get the day's income and profit once: the bridge is told the same sums the lines book.
        string run = MemberText(EconomySource(@"OwnBank\OwnBankDirector.cs"), "private void RunDay(");
        Check(run.Contains("OwnBankBridges.DayBooked?.Invoke(day, _day.Income, _day.Profit, _bank.Capital)") && run.Contains("OwnBankGame.BookDay(_day, hq, _bank.Name, dated ? day : Save.Day)"),
            "lines: a listed bank's own books are told the day's income and profit once, as the statement books them");
    }

    // ---------------------------------------------------------------- sources: both interfaces, the director, the options

    private static void GrowthSources()
    {
        string app = EconomySource(@"OwnBank\OwnBankApp.cs") + EconomySource(@"OwnBank\OwnBankApp.Growth.cs"), kit = ModSource(@"Ui\Money\OwnBankBuild.Growth.cs"),
            live = ModSource(@"Ui\Money\OwnBankLive.cs");
        string director = EconomySource(@"OwnBank\OwnBankDirector.cs");
        Check(app.Contains("TabKeys") && app.Contains("reallife_ob_tab_cards") && app.Contains("reallife_ob_tab_desk") && app.Contains("WriteCards") && app.Contains("WriteDesk")
            && app.Contains("WriteRisk")
              && ModSource(@"Ui\Money\OwnBankBuild.cs").Contains("Tabs = 7") && kit.Contains("CardsPage") && kit.Contains("DeskPage") && kit.Contains("RiskCard"),
            "both interfaces: the old app and the kit show the risk page, the Cards tab and the Desk tab");
        string[] acts = { "SetCards", "SetCardIssue", "SetCardLimit", "SetCardRate", "SetCardFee", "SetCardLines", "DecideCards", "OpenOwnCard", "CloseOwnCard", "SetDesk", "SetDeskMandate",
            "SetDeskShare", "SetDeskLimit", "SellLoans", "Borrow", "Repay" };
        bool both = true;
        foreach (string act in acts)
        {
            both &= (app.Contains("_director." + act + "(") || app.Contains("Decide(")) && live.Contains("director." + act + "(");
        }
        Check(both, "both interfaces: every act of the new parts is a call of the director that the old app and the kit's screen both make");
        string day = MemberText(director, "private void RunDay(");
        Check(day.Contains("TellRisk(day, city)") && day.Contains("TellDesk()") && day.Contains("TellCards()"),
            "the day tells the warning signs, a coming shock, a desk cut and the card applicants waiting");
        Check(director.Contains("OwnCardSink.Bank = bank.Running ? bank : null") && director.Contains("ReconcileOwnCard()") && director.Contains("LetOwnCardGo()"),
            "the director connects the owner's card to the bank's record, reconciles it and lets it go when the bank ends");
        string game = MemberText(EconomySource(@"OwnBank\OwnBankGame.cs"), "public static BankCity City()");
        Check(game.Contains("Risk = Settings.EconomyBank ? Mathf.Clamp(Settings.EconomyBankRisk") && game.Contains("Activity = OwnBankBridges.Activity")
            && game.Contains("Derivs = OwnBankBridges.Derivs"),
            "the city the bank's day reads has the option's level, the market's activity and whether the exchange has options and futures");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), settings = ModSource(@"Core\Settings.cs");
        Check(mod.Contains("Settings.EconomyBankRiskId, \"reallife_ob_risk\"") && settings.Contains("EconomyBankRisk = 0;") && settings.Contains("EconomyBankRiskId = \"economy_bank_risk\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("economy_bank_risk") && ModSource(@"Meta\FeatureCities.cs").Contains("economy_bank_risk"),
            "the option Bank risk is a choice of three, off by default, on the options page, in the tree and in the feature list");
        Check(ModSource(@"Wiring\OwnBankLinks.cs").Contains("OwnBankBridges.DividendTax = DividendTax") && ModSource(@"Markets\MarketDirector.Grants.cs").Contains("public int ActivityMilli")
            && ModSource(@"Markets\MarketDirector.Grants.cs").Contains("DerivativesOpen"),
            "the bank reads the market's tax on dividends, its activity and whether it has derivatives through its bridges");
        // Futures and options exist in the market, so the desk offers them (and the docs say so).
        Check(System.IO.File.Exists(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Markets\Derivatives\OptionRules.cs")) && typeof(DerivCatalogue).GetField("WeeksAhead") != null,
            "the stock market has options and futures (Markets/Derivatives), so the desk's clients may deal in them");
    }

    // ---------------------------------------------------------------- the texts

    private static void GrowthTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (words == null || words.Length == 0 || words[0] == null)
        {
            Check(false, "bank risk's texts: the English file is where the checks look for it");
            return;
        }
        string en(string key) => words[0].TryGetValue(key, out string v) ? v : null;
        var keys = new List<string>(BankDividends.EveryKeys);
        keys.AddRange(BankRisk.NameKeys);
        keys.AddRange(CardTerms.TierKeys);
        keys.AddRange(BankDesk.MandateKeys);
        keys.AddRange(OwnBankReader.SignKeys);
        keys.Add(CardTerms.BlackKey);
        keys.Add(BankDividends.WhyKeys[BankDividends.Watch]);
        foreach (string k in new[] { OwnBankCatalogue.CardsKey, OwnBankCatalogue.DeskKey, OwnBankCatalogue.CardCostsKey, OwnBankCatalogue.FundingKey, OwnBankCatalogue.DeskCostsKey,
            OwnBankCatalogue.DividendAutoKey,
                     OwnBankCatalogue.DividendTaxKey })
        {
            keys.Add(k);
            keys.Add(k + "_label");
        }
        keys.AddRange(new[] { OwnBankCatalogue.CardsRow, OwnBankCatalogue.FundingRow, OwnBankCatalogue.DeskRow, "reallife_ob_pay_held_watch", "reallife_opt_tip_economy_bank_risk",
            "reallife_ob_risk", "reallife_meta_title_economy_bank_risk",
            "reallife_guide_economy_bank_risk", "reallife_guide_economy_bank_risk_about", "reallife_ob_test_stress", "reallife_ob_test_cards", "reallife_ob_test_desk" });
        string[] notices = { "reallife_ob_toast_signs", "reallife_ob_toast_shock", "reallife_ob_toast_rolled", "reallife_ob_toast_taken", "reallife_ob_toast_desk_cut",
            "reallife_ob_toast_loans_sold", "reallife_ob_toast_borrowed",
            "reallife_ob_toast_repaid", "reallife_ob_toast_cards_approved", "reallife_ob_toast_cards_declined", "reallife_ob_toast_cards_queue", "reallife_ob_toast_own_card" };
        string[] refusals = { "reallife_ob_why_borrow_too_much", "reallife_ob_why_cards_off", "reallife_ob_why_no_own_card", "reallife_ob_why_no_queue", "reallife_ob_why_nothing_borrowed",
            "reallife_ob_why_own_card_capital",
            "reallife_ob_why_own_card_limit", "reallife_ob_why_own_card_open", "reallife_ob_why_own_card_owed", "reallife_ob_why_repay_cash", "reallife_ob_why_risk_off",
                "reallife_ob_why_sale_too_much" };
        keys.AddRange(notices);
        keys.AddRange(refusals);
        keys.AddRange(new[] { "reallife_ob_confirm_sale_title", "reallife_ob_confirm_sale_body", "reallife_ob_confirm_borrow_title", "reallife_ob_confirm_borrow_body",
            "reallife_ob_ask_borrow", "reallife_ob_ask_repay", "reallife_ob_ask_sale",
            "reallife_ob_ask_own_limit" });
        // Every label the kit's pages write.
        string kit = ModSource(@"Ui\Money\OwnBankBuild.Growth.cs") + ModSource(@"Ui\Money\OwnBankBuild.cs") + ModSource(@"Ui\Money\OwnBankBuild.Tabs.cs");
        string screen = ModSource(@"Ui\UiScreens.Money.Bank.cs");
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(kit, "w\\.[TF]\\(\\s*\"(reallife_[a-z0-9_]+)\""))
        {
            used.Add(m.Groups[1].Value);
        }
        int lacking = 0, unlisted = 0;
        var gone = new List<string>();
        foreach (string key in used)
        {
            if (en(key) == null)
            {
                lacking++;
                gone.Add(key);
            }
            if (!screen.Contains("\"" + key + "\""))
            {
                unlisted++;
                gone.Add("not in the screen's list: " + key);
            }
        }
        foreach (string key in keys)
        {
            if (en(key) == null)
            {
                gone.Add(key);
            }
        }
        Check(gone.Count == 0, "bank risk's texts: every text of the new parts is in English, and every label the kit's pages write is a text and in the Your bank screen's list " +
            "(" + string.Join(", ", gone) + ")");
        bool fits = true, plain = true;
        foreach (string n in notices)
        {
            fits &= en(n) != null && en(n).Length <= 240;
        }
        foreach (string key in keys)
        {
            string t = en(key) ?? string.Empty;
            plain &= t.IndexOf('—') < 0 && t.IndexOf('–') < 0 && t.IndexOf('!') < 0 && (key.StartsWith("reallife_guide", StringComparison.Ordinal) || t.IndexOf(';') < 0);
        }
        Check(fits && plain, "bank risk's texts: a notice is at most 240 characters, and no text has a dash, an exclamation mark or a semicolon");
        string tip = en("reallife_opt_tip_economy_bank_risk") ?? string.Empty;
        Check(tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32, "bank risk's texts: the option's tip is at most 32 words and 190 characters");
        string guide = en("reallife_guide_economy_bank_risk") ?? string.Empty;
        var facts = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(ModSource(@"Economy\OwnBank\Risk\BankGrowthGuide.cs"), "\\.Fact\\(\"(rk_[a-z0-9_]+)\""))
        {
            facts.Add(m.Groups[1].Value);
        }
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(guide, "\\{(rk_[a-z0-9_]+)\\}"))
        {
            named.Add(m.Groups[1].Value);
        }
        bool filled = named.Count > 30;
        foreach (string name in named)
        {
            filled &= facts.Contains(name);
        }
        Check(filled && guide.Contains("# Credit cards") && guide.Contains("# Trading desk") && guide.Contains("# Meeting a run"),
            "bank risk's guide page: every figure in it is a fact the feature fills (" + named.Count + " of them), and it has a section on risk, cards and the desk");
        string tabs = en("reallife_guide_economy_ownbank") ?? string.Empty;
        Check(tabs.Contains("<b>Cards</b> and <b>Desk</b>") && tabs.Contains("every quarter"), "bank risk's texts: the bank's guide page names the new tabs and the quarterly dividends");
    }

    // ---------------------------------------------------------------- timing

    private static void GrowthTiming()
    {
        BankCity c = RiskCity(2, CycleState.Slowdown);
        c.Activity = 1300;
        c.Derivs = true;
        OwnBank b = TierBank(c, BankTiers.National, 200, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        b.CardsOn = true;
        b.DeskOn = true;
        b.DeskMandate = BankDesk.Growth;
        b.DeskDirty = true;
        List<FundQuote> q = MarketOf(150, 5000, 7);
        var day = new BankDay();
        RunBank(b, c, 2, 100, q, day);
        var watch = Stopwatch.StartNew();
        int days = 0;
        for (int d = 102; d < 102 + 600 && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, q, day);
            days++;
        }
        double each = watch.Elapsed.TotalMilliseconds / Math.Max(1, days);
        Check(days > 100 && each < 1.5, "timing: a day of a bank with the risk, the cards and the desk all running over 150 quotes takes " + each.ToString("0.000",
            CultureInfo.InvariantCulture) + " ms (under 1.5)");
        var facts = new OwnBankFacts();
        var world = new OwnBankWorld { Ready = true, Today = b.LastDay + 1, Level = c.Level };
        var say = new BankSay();
        var read = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            OwnBankReader.Read(facts, b, c, world, say);
        }
        double reads = read.Elapsed.TotalMilliseconds / 200;
        Check(facts.Ready && facts.CardKinds.Length == CardTerms.Kinds && facts.RiskNames.Length == BankRisk.Count && facts.MandateNames.Length == BankDesk.Mandates && reads < 2.0,
            "timing: the page's facts read in " + reads.ToString("0.000", CultureInfo.InvariantCulture) + " ms (under 2), with the cards, the desk and the risk in them");
    }

    private sealed class BankSay : IOwnBankSay
    {
        public string T(string key) => key;

        public string F(string key, params string[] pairs) => key;

        public string Day(int day) => day.ToString(CultureInfo.InvariantCulture);

        public string Cents(long cents) => cents.ToString(CultureInfo.InvariantCulture);
    }
}
