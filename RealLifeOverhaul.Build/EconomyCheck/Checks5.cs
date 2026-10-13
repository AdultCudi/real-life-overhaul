using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The fifth part of the economy's checks: running a business (Scripts/Economy/Company). The wage market, raises,
// a rival's offer, a strike, overtime and sick pay; premiums, excess, claims and the insurer that will not renew;
// what a campaign adds and what its report says; the quarterly return, the accountant, cash off the books and the
// court's threshold; licences; the save's line; and that the same seed gives the same history. Every figure expected
// here is worked out by hand from the catalogue.
internal static partial class Program
{
    private static void Company()
    {
        CompanyCatalogueChecks();
        CompanyWageMarket();
        CompanyRaises();
        CompanyPoaching();
        CompanyStrikes();
        CompanyOvertimeAndSickPay();
        CompanyPremiums();
        CompanyClaims();
        CompanyCampaigns();
        CompanyReports();
        CompanyReturn();
        CompanyAccountant();
        CompanyHiddenCash();
        CompanyLicences();
        CompanySaveAndLoad();
        CompanyDeterminism();
    }

    private static StaffFile Worker(int wage, int going, int skill = 60)
    {
        return new StaffFile { Id = "e" + wage + "-" + going, Place = "High%20Street~1", Wage = wage, Going = going, Skill = skill, DayHours = 800 };
    }

    private static void CompanyCatalogueChecks()
    {
        Check(CompanyCatalogue.Level(-4) == CompanyCatalogue.Relaxed && CompanyCatalogue.Level(9) == CompanyCatalogue.Hardcore && CompanyCatalogue.Level(2) == 2, "a strictness out of range is the nearest one");
        Check(CompanyCatalogue.RaiseGap.Length == 4 && CompanyCatalogue.RaiseDays.Length == 4 && CompanyCatalogue.PoachChance.Length == 4 && CompanyCatalogue.StrikeDays.Length == 4
              && CompanyCatalogue.FineShare.Length == 4, "every figure that goes by strictness has four steps");
        bool harder = true;
        for (int l = 1; l < 4; l++)
        {
            harder &= CompanyCatalogue.RaiseGap[l] < CompanyCatalogue.RaiseGap[l - 1] && CompanyCatalogue.RaiseDays[l] < CompanyCatalogue.RaiseDays[l - 1]
                      && CompanyCatalogue.PoachChance[l] > CompanyCatalogue.PoachChance[l - 1] && CompanyCatalogue.StrikeDays[l] > CompanyCatalogue.StrikeDays[l - 1]
                      && CompanyCatalogue.FineShare[l] > CompanyCatalogue.FineShare[l - 1];
        }
        Check(harder, "each step of strictness makes staff ask sooner, rivals approach more, a strike longer and a fine larger");
        Check(CompanyCatalogue.PoachChance[0] == 0 && CompanyCatalogue.StrikeDays[0] == 0, "Relaxed has no poaching and no strikes");
        Check(CompanyCatalogue.InsurerPrice.Length == CompanyCatalogue.Insurers && CompanyCatalogue.InsurerExcessShare.Length == CompanyCatalogue.Insurers
              && CompanyCatalogue.InsurerPayDays.Length == CompanyCatalogue.Insurers && CompanyCatalogue.InsurerClaimsMost.Length == CompanyCatalogue.Insurers, "three insurers, each with every figure");
        bool trade = true;
        for (int i = 1; i < CompanyCatalogue.Insurers; i++)
        {
            trade &= CompanyCatalogue.InsurerPrice[i] > CompanyCatalogue.InsurerPrice[i - 1] && CompanyCatalogue.InsurerExcessShare[i] < CompanyCatalogue.InsurerExcessShare[i - 1]
                     && CompanyCatalogue.InsurerPayDays[i] < CompanyCatalogue.InsurerPayDays[i - 1] && CompanyCatalogue.InsurerClaimsMost[i] > CompanyCatalogue.InsurerClaimsMost[i - 1];
        }
        Check(trade, "a dearer insurer has a smaller excess, pays sooner and stands more claims");
        Check(CompanyCatalogue.CoverBase.Length == CompanyCatalogue.CoverKinds && CompanyCatalogue.CoverMost.Length == CompanyCatalogue.CoverKinds && CompanyCatalogue.CoverTakingsShare.Length == CompanyCatalogue.CoverKinds
              && CompanyCatalogue.CoverIncomePerPoint.Length == CompanyCatalogue.CoverKinds, "three kinds of cover, each with every figure");
        Check(CompanyCatalogue.CampaignCost.Length == CompanyCatalogue.CampaignKinds && CompanyCatalogue.CampaignDays.Length == CompanyCatalogue.CampaignKinds
              && CompanyCatalogue.CampaignFootfall.Length == CompanyCatalogue.CampaignKinds && CompanyCatalogue.CampaignLocal.Length == CompanyCatalogue.CampaignKinds, "four campaigns, each with every figure");
        Check(CompanyCatalogue.CampaignCost[(int)CampaignKind.DiscountDay] == 0 && CompanyCatalogue.CampaignDays[(int)CampaignKind.DiscountDay] == 1, "a discount day is free to book and lasts a day");
        Check(CompanyCatalogue.CampaignMost <= 15 && -CompanyCatalogue.StrikeFootfall <= 25, "no campaign and no strike asks for more than business pressure lets an outside change take");
        Check(CompanyCatalogue.AccountantFee.Length == CompanyCatalogue.Accountants && CompanyCatalogue.AccountantSaving.Length == CompanyCatalogue.Accountants
              && CompanyCatalogue.AccountantRisk.Length == CompanyCatalogue.Accountants, "three accountants, each with every figure");
        Check(CompanyCatalogue.LicenceFee.Length == CompanyCatalogue.LicenceKinds && CompanyCatalogue.LicenceTerm.Length == CompanyCatalogue.LicenceKinds, "four licences, each with a fee and a term");
        Check(CompanyCatalogue.HideSteps[0] == 0 && CompanyCatalogue.HideSteps[CompanyCatalogue.HideSteps.Length - 1] == 30 && Array.IndexOf(CompanyCatalogue.HideSteps, CompanyCatalogue.HideMostRelaxed) >= 0,
            "cash off the books goes in steps from nothing to thirty per cent, and Relaxed's most is one of them");
        Check(CompanyCatalogue.AtPrices(150, 1000) == 150 && CompanyCatalogue.AtPrices(150, 1040) == 160 && CompanyCatalogue.AtPrices(1200, 1100) == 1320 && CompanyCatalogue.AtPrices(0, 1500) == 0,
            "an amount at today's prices is rounded to the nearest ten");
        Check(CompanyCatalogue.Share(1000, 150) == 150 && CompanyCatalogue.Share(-1000, 150) == -150 && CompanyCatalogue.Share(3, 500) == 2 && CompanyCatalogue.Share(long.MaxValue / 4000, 1000) > 0,
            "a share is rounded and never runs over");
        Check(CompanyCatalogue.TermDays(60) == 60 && CompanyCatalogue.TermDays(8) == 20 && CompanyCatalogue.TermDays(365) == 365, "a policy's term is a year of the game, twenty days at least");
    }

    private static void CompanyWageMarket()
    {
        Check(StaffRules.Market(1000, CycleState.Steady, 100) == 1000, "prices at the start, a steady economy and a middling district: the going rate is the game's own formula");
        Check(StaffRules.Market(1100, CycleState.Steady, 100) == 1100 && StaffRules.Market(900, CycleState.Steady, 100) == 900,
            "the going rate is at today's prices: the city's cost of living pays every wage so, and pay is counted the same way");
        Check(StaffRules.Market(1000, CycleState.Boom, 100) == 1040 && StaffRules.Market(1000, CycleState.Slowdown, 100) == 980 && StaffRules.Market(1000, CycleState.Recession, 100) == 950,
            "a boom raises the going rate and bad times lower it");
        Check(StaffRules.Market(1000, CycleState.Steady, 140) == 1080 && StaffRules.Market(1000, CycleState.Steady, 60) == 920, "a rich district pays more and a poor one less");
        Check(StaffRules.Market(1000, CycleState.Boom, 300) == CompanyCatalogue.WageMarketMost && StaffRules.Market(1000, CycleState.Recession, 10) == CompanyCatalogue.WageMarketLeast,
            "the economy and the district move it within its bounds");
        Check(StaffRules.Market(2000, CycleState.Boom, 300) == CompanyCatalogue.WageMarketMost * 2 && StaffRules.Market(500, CycleState.Recession, 10) == CompanyCatalogue.WageMarketLeast / 2,
            "and today's prices on top of that");
        bool bounded = true, rises = true;
        for (int index = 400; index <= 3000; index += 50)
        {
            for (int state = 0; state < 4; state++)
            {
                for (int income = 60; income <= 140; income += 10)
                {
                    int m = StaffRules.Market(index, (CycleState)state, income);
                    int priced = Math.Max(500, Math.Min(4000, index));   // (the index is read within its own bounds)
                    bounded &= m >= (long)CompanyCatalogue.WageMarketLeast * priced / 1000 - 1 && m <= (long)CompanyCatalogue.WageMarketMost * priced / 1000 + 1;
                    rises &= StaffRules.Market(index + 50, (CycleState)state, income) >= m && StaffRules.Market(index, (CycleState)state, income + 10) >= m;
                }
            }
        }
        Check(bounded, "the going rate is inside its bounds at today's prices for every index, state and district");
        Check(rises, "the going rate never falls when prices or the district's income rise");
        Check(StaffRules.Going(1200, 1050) == 1260 && StaffRules.Going(-5, 1050) == 0, "the going rate for somebody is the game's formula by the market");
        Check(StaffRules.Gap(1260, 1134) == 100 && StaffRules.Gap(1000, 1100) == -100 && StaffRules.Gap(0, 500) == 0, "the gap is how far under the going rate somebody is paid, in thousandths");
        Check(StaffRules.Pay(1250, 800) == 100 && StaffRules.Pay(1000, 0) == 0 && StaffRules.Pay(-3, 800) == 0, "pay is the rate by the hours, in whole money");
        StaffFile f = Worker(1000, 1200);
        f.Raise = 150;
        Check(f.Paid == 1150 && !StaffRules.Underpaid(f, CompanyCatalogue.Standard) && StaffRules.Underpaid(Worker(1000, 1200), CompanyCatalogue.Standard),
            "an agreed raise counts as pay when somebody is compared with the going rate");
    }

    private static void CompanyRaises()
    {
        StaffFile f = Worker(1000, 1200);   // a sixth under the going rate
        StaffNote note = StaffNote.None;
        int asked = 0;
        for (int day = 1; day <= 7; day++)
        {
            note = StaffRules.Day(f, CompanyCatalogue.Standard, day);
            asked = note == StaffNote.Asked && asked == 0 ? day : asked;
        }
        Check(asked == 7 && f.AskDay == 7 && f.AskTo == 1200, "on Standard somebody underpaid for seven days asks for the going rate on the seventh");
        Check(f.Standing == CompanyCatalogue.StandingStart - 7, "each day underpaid costs a point of standing");
        Check(StaffRules.RaiseFor(f, RaiseAnswer.Agree) == 200 && StaffRules.RaiseFor(f, RaiseAnswer.Less) == 100 && StaffRules.RaiseFor(f, RaiseAnswer.Refuse) == 0, "agreeing gives what is asked, offering less gives half of it");
        StaffRules.Answer(f, RaiseAnswer.Agree, 8);
        Check(f.Raise == 200 && f.Paid == 1200 && f.Standing == 63 && f.AskDay == 0 && f.AskRest == 8 + CompanyCatalogue.RaiseRestDays, "an agreed raise is paid on top of the game's wage and raises their standing");
        Check(f.Wage == 1000, "the game's own wage is not touched by a raise");
        for (int day = 9; day <= 40; day++)
        {
            note = StaffRules.Day(f, CompanyCatalogue.Standard, day);
        }
        Check(f.Standing == CompanyCatalogue.StandingCalm && f.AskDay == 0 && f.UnderDays == 0, "paid fairly, standing climbs back to the calm level and stops there");

        StaffFile less = Worker(1000, 1200);
        for (int day = 1; day <= 7; day++)
        {
            StaffRules.Day(less, CompanyCatalogue.Standard, day);
        }
        StaffRules.Answer(less, RaiseAnswer.Less, 7);
        Check(less.Raise == 100 && less.Standing == 53 + CompanyCatalogue.StandLess, "offering less pays half the raise and costs standing");
        StaffFile refused = Worker(1000, 1200);
        for (int day = 1; day <= 7; day++)
        {
            StaffRules.Day(refused, CompanyCatalogue.Standard, day);
        }
        StaffRules.Answer(refused, RaiseAnswer.Refuse, 7);
        Check(refused.Raise == 0 && refused.Standing == 53 + CompanyCatalogue.StandRefuse, "refusing pays nothing and costs more standing");
        bool again = false;
        int next = 0;
        for (int day = 8; day <= 40 && !again; day++)
        {
            again = StaffRules.Day(refused, CompanyCatalogue.Standard, day) == StaffNote.Asked;
            next = day;
        }
        Check(again && next == 7 + CompanyCatalogue.RaiseRestDays, "after an answer the same employee does not ask again for fourteen days");

        StaffFile silent = Worker(1000, 1200);
        StaffNote last = StaffNote.None;
        for (int day = 1; day <= 11; day++)
        {
            last = StaffRules.Day(silent, CompanyCatalogue.Standard, day);
        }
        Check(silent.AskDay == 7 && last == StaffNote.None, "a request waits four days for its answer");
        last = StaffRules.Day(silent, CompanyCatalogue.Standard, 12);
        Check(last == StaffNote.AskLapsed && silent.AskDay == 0 && silent.Raise == 0, "a request nobody answers is taken as refused");

        StaffFile eased = Worker(1000, 1200);
        for (int day = 1; day <= 7; day++)
        {
            StaffRules.Day(eased, CompanyCatalogue.Standard, day);
        }
        eased.Going = 1050;   // the going rate has fallen: no longer a tenth under
        Check(StaffRules.Day(eased, CompanyCatalogue.Standard, 8) == StaffNote.AskWithdrawn && eased.AskDay == 0, "a request is dropped when they are no longer underpaid");

        StaffFile slight = Worker(1000, 1090);   // 8.3 % under
        bool everAsked = false;
        for (int day = 1; day <= 60; day++)
        {
            everAsked |= StaffRules.Day(slight, CompanyCatalogue.Standard, day) == StaffNote.Asked;
        }
        Check(!everAsked, "on Standard somebody less than a tenth under the going rate never asks");
        everAsked = false;
        slight = Worker(1000, 1090);
        for (int day = 1; day <= 60; day++)
        {
            everAsked |= StaffRules.Day(slight, CompanyCatalogue.Hardcore, day) == StaffNote.Asked;
        }
        Check(everAsked, "on Hardcore the same employee does");
        StaffFile relaxed = Worker(1000, 1200);
        int relaxedDay = 0;
        for (int day = 1; day <= 30 && relaxedDay == 0; day++)
        {
            relaxedDay = StaffRules.Day(relaxed, CompanyCatalogue.Relaxed, day) == StaffNote.Asked ? day : 0;
        }
        Check(relaxedDay == CompanyCatalogue.RaiseDays[CompanyCatalogue.Relaxed], "on Relaxed it takes ten days");
        StaffFile floor = Worker(1000, 1200);
        floor.Standing = 1;
        StaffRules.Day(floor, CompanyCatalogue.Standard, 1);
        StaffRules.Day(floor, CompanyCatalogue.Standard, 2);
        Check(floor.Standing == 0, "standing never goes under nothing");
    }

    private static void CompanyPoaching()
    {
        StaffFile f = Worker(1000, 1200, 60);
        Check(StaffRules.Wanted(f, CompanyCatalogue.Standard, 1), "somebody good at the job and clearly underpaid is wanted by a rival");
        Check(!StaffRules.Wanted(f, CompanyCatalogue.Relaxed, 1), "nobody is poached on Relaxed");
        Check(!StaffRules.Wanted(Worker(1000, 1200, 30), CompanyCatalogue.Standard, 1), "somebody with little skill is not wanted");
        Check(!StaffRules.Wanted(Worker(1000, 1050, 80), CompanyCatalogue.Hardcore, 1), "somebody paid close to the going rate is not wanted");
        StaffRules.Offer(f, "Rival Stores", 5);
        Check(f.PoachDay == 5 && f.PoachOffer == 1260 && f.Rival == "Rival Stores" && !StaffRules.Wanted(f, CompanyCatalogue.Standard, 6), "the rival offers the going rate and a twentieth more");
        Check(!StaffRules.OfferLapsed(f, 8) && StaffRules.OfferLapsed(f, 9), "an offer waits three days for an answer");
        StaffRules.OfferAnswer(f, true, 6);
        Check(f.Raise == 260 && f.Paid == 1260 && f.LeaveDay == 0 && f.Standing == CompanyCatalogue.StandingStart + CompanyCatalogue.StandMatch && f.PoachRest == 6 + CompanyCatalogue.PoachRestDays,
            "matching it pays them what the rival offered and they stay");
        StaffFile g = Worker(1000, 1200, 60);
        StaffRules.Offer(g, "Rival Stores", 5);
        StaffRules.OfferAnswer(g, false, 6);
        Check(g.Raise == 0 && g.LeaveDay == 6 + CompanyCatalogue.PoachNoticeDays && g.PoachDay == 0, "not matching it has them leave after their notice");
        Check(!StaffRules.Wanted(g, CompanyCatalogue.Hardcore, 7), "somebody who is leaving is not approached again");
        int standard = 0, hardcore = 0, relaxed = 0;
        for (int age = 0; age < 20000; age++)
        {
            standard += StaffRules.Approach(777, age, CompanyCatalogue.Standard) ? 1 : 0;
            hardcore += StaffRules.Approach(777, age, CompanyCatalogue.Hardcore) ? 1 : 0;
            relaxed += StaffRules.Approach(777, age, CompanyCatalogue.Relaxed) ? 1 : 0;
        }
        Check(relaxed == 0, "no rival ever approaches on Relaxed");
        Check(standard > 200 && standard < 400 && hardcore > 650 && hardcore < 950, "a rival approaches on about fifteen days in a thousand on Standard and forty on Hardcore (" + standard + ", " + hardcore + " in 20000)");
        bool inside = true;
        for (int age = 0; age < 500; age++)
        {
            int who = StaffRules.Who(99, age, 7);
            inside &= who >= 0 && who < 7;
        }
        Check(inside && StaffRules.Who(99, 3, 1) == 0, "the one approached is always one of those who fit");
    }

    private static void CompanyStrikes()
    {
        Check(StaffRules.StrikeDue(2, 3, CompanyCatalogue.Standard) && StaffRules.StrikeDue(3, 5, CompanyCatalogue.Standard) && !StaffRules.StrikeDue(1, 2, CompanyCatalogue.Standard),
            "a strike is due when six in ten of a business's staff stand low");
        Check(!StaffRules.StrikeDue(1, 1, CompanyCatalogue.Hardcore), "one employee alone does not strike");
        Check(!StaffRules.StrikeDue(5, 5, CompanyCatalogue.Relaxed), "Relaxed never strikes");
        var shop = new ShopFile { Place = "High%20Street~1" };
        Check(StaffRules.CanStrike(shop, 10), "a business with no strike on and none lately can have one");
        StaffRules.Call(shop, CompanyCatalogue.Standard, 10);
        Check(shop.StrikeFrom == 11 && shop.StrikeEnd == 13 && shop.StrikeCalled(10) && !shop.Striking(10) && shop.Striking(11) && shop.Striking(12) && !shop.Striking(13),
            "a strike is called a day ahead and runs two days on Standard");
        Check(StaffRules.Footfall(shop, 10) == 0 && StaffRules.Footfall(shop, 11) == CompanyCatalogue.StrikeFootfall && StaffRules.Footfall(shop, 13) == 0, "it takes a quarter of the customers on its days and none before or after");
        Check(!StaffRules.CanStrike(shop, 11), "no second strike is called while one is on");
        StaffRules.End(shop, 12);
        Check(shop.StrikeFrom == 0 && shop.StrikeRest == 12 + CompanyCatalogue.StrikeRestDays && !StaffRules.CanStrike(shop, 20) && StaffRules.CanStrike(shop, 33) && StaffRules.Footfall(shop, 12) == 0,
            "settled, it ends at once and the business is left alone for three weeks");
        var hard = new ShopFile();
        StaffRules.Call(hard, CompanyCatalogue.Hardcore, 10);
        Check(hard.StrikeEnd - hard.StrikeFrom == 4, "on Hardcore a strike runs four days");

        StaffFile a = Worker(1000, 1200);
        a.Standing = 20;
        StaffRules.After(a, StrikeAnswer.Raise);
        Check(a.Raise == 200 && a.Standing == CompanyCatalogue.StrikeRaiseStanding && !StaffRules.StandsLow(a), "a raise for all brings each up to the going rate and their standing to sixty");
        StaffFile b = Worker(1000, 1200);
        b.Standing = 20;
        Check(StaffRules.StrikePayment(b) == 160, "the one-off payment is sixteen hours' pay each");
        StaffRules.After(b, StrikeAnswer.Payment);
        Check(b.Raise == 0 && b.Standing == 45 && !StaffRules.StandsLow(b), "a one-off payment raises standing by twenty-five and no wage");
        StaffFile c = Worker(1000, 1200);
        c.Standing = 20;
        StaffRules.After(c, StrikeAnswer.SitOut);
        Check(c.Raise == 0 && c.Standing == CompanyCatalogue.StrikeAfterStanding, "sitting it out costs nothing and leaves them at forty");

        // A scripted history: three underpaid employees, every request refused, at each strictness.
        for (int level = 0; level < 4; level++)
        {
            var staff = new[] { Worker(1000, 1250), Worker(1010, 1250), Worker(1020, 1250) };
            var s = new ShopFile();
            int called = 0;
            for (int day = 1; day <= 200 && called == 0; day++)
            {
                int low = 0;
                for (int i = 0; i < staff.Length; i++)
                {
                    if (StaffRules.Day(staff[i], level, day) == StaffNote.Asked)
                    {
                        StaffRules.Answer(staff[i], RaiseAnswer.Refuse, day);
                    }
                    low += StaffRules.StandsLow(staff[i]) ? 1 : 0;
                }
                if (StaffRules.CanStrike(s, day) && StaffRules.StrikeDue(low, staff.Length, level))
                {
                    StaffRules.Call(s, level, day);
                    called = day;
                }
            }
            Check(level == CompanyCatalogue.Relaxed ? called == 0 : called > 0 && called < 60,
                level == CompanyCatalogue.Relaxed ? "200 days of underpaid staff refused every raise: still no strike on Relaxed" : "underpaid staff refused every raise strike within sixty days at strictness " + level + " (day " + called + ")");
        }
    }

    private static void CompanyOvertimeAndSickPay()
    {
        StaffFile f = Worker(1000, 1000);
        Check(StaffRules.OvertimePay(f) == 30, "a day's overtime is two hours at time and a half");
        var shop = new ShopFile();
        Check(!StaffRules.OvertimeOffered(shop, false, 5, 1, 0, 1), "staff offer no overtime at a business that is not busy");
        int offers = 0;
        for (int age = 0; age < 10000; age++)
        {
            offers += StaffRules.OvertimeOffered(shop, true, 5, age, 2, 100) ? 1 : 0;
        }
        Check(offers > 2700 && offers < 3300, "at a busy business they offer it on about three days in ten (" + offers + " in 10000)");
        StaffRules.OvertimeAnswer(shop, true, 90, 5);
        Check(shop.OvertimeFrom == 5 && shop.OvertimeEnd == 8 && shop.OvertimeCost == 90 && shop.Overtime(5) && shop.Overtime(7) && !shop.Overtime(8), "agreed overtime runs three days from the day it is agreed");
        Check(StaffRules.Footfall(shop, 6) == CompanyCatalogue.OvertimeFootfall && StaffRules.Footfall(shop, 8) == 0, "it adds four per cent of customers on those days");
        Check(!StaffRules.OvertimeOffered(shop, true, 5, 1, 0, 6), "no new offer while overtime runs");
        StaffRules.OvertimeDay(shop, 8);
        Check(shop.OvertimeFrom == 0 && shop.OvertimeCost == 0 && shop.OvertimeRest == 8 + CompanyCatalogue.OvertimeRestDays, "it ends by itself and is not offered again for ten days");
        var other = new ShopFile { OvertimeAsk = 3 };
        StaffRules.OvertimeDay(other, 5);
        Check(other.OvertimeAsk == 3, "an offer waits two days");
        StaffRules.OvertimeDay(other, 6);
        Check(other.OvertimeAsk == 0 && other.OvertimeFrom == 0 && other.OvertimeRest == 6 + CompanyCatalogue.OvertimeRestDays, "an offer nobody answers is dropped");
        var striking = new ShopFile();
        StaffRules.Call(striking, CompanyCatalogue.Standard, 1);
        bool any = false;
        for (int age = 0; age < 200; age++)
        {
            any |= StaffRules.OvertimeOffered(striking, true, 5, age, 0, 2);
        }
        Check(!any, "staff on strike offer no overtime");

        Check(StaffRules.SickPay(f, 0) == 0 && StaffRules.SickPay(f, 1) == 40 && StaffRules.SickPay(f, 2) == 80, "sick pay is nothing, half a day's pay or all of it");
        StaffFile noHours = Worker(1000, 1000);
        noHours.DayHours = 0;
        Check(StaffRules.SickPay(noHours, 2) == 80, "somebody with no hours on record is paid for an eight-hour day");
        StaffFile s0 = Worker(1000, 1000), s1 = Worker(1000, 1000), s2 = Worker(1000, 1000);
        StaffRules.SickDay(s0, 0);
        StaffRules.SickDay(s1, 1);
        StaffRules.SickDay(s2, 2);
        Check(s0.Standing == 57 && s1.Standing == 60 && s2.Standing == 62, "no sick pay costs standing, statutory pay changes nothing, generous pay raises it");
    }

    private static void CompanyPremiums()
    {
        int fire = (int)CoverKind.Fire, theft = (int)CoverKind.Theft, liability = (int)CoverKind.Liability;
        Check(CoverRules.Premium(fire, 1, 2000, 100, 0, 1000) == 20, "fire cover for a business taking 2,000 a day costs 20 a day at the middle insurer");
        Check(CoverRules.Premium(fire, 0, 2000, 100, 0, 1000) == 17 && CoverRules.Premium(fire, 2, 2000, 100, 0, 1000) == 25, "the cheap insurer charges less and the dear one more");
        Check(CoverRules.Premium(fire, 1, 2000, 100, 1, 1000) == 25 && CoverRules.Premium(fire, 1, 2000, 100, 2, 1000) == 30, "each claim of the term before adds a quarter");
        Check(CoverRules.Premium(theft, 1, 2000, 60, 0, 1000) == 22 && CoverRules.Premium(theft, 1, 2000, 140, 0, 1000) == 18, "theft cover is dearer in a poor district");
        Check(CoverRules.Premium(fire, 1, 2000, 140, 0, 1000) > CoverRules.Premium(fire, 1, 2000, 60, 0, 1000), "fire cover is dearer in a rich one");
        Check(CoverRules.Premium(fire, 1, 2200, 100, 0, 1100) == 22 && CoverRules.Premium(fire, 1, 2000, 100, 0, 1100) < 22,
            "premiums follow prices: the base at today's prices, the takings (at today's in the game's statement) as they are");
        Check(CoverRules.Premium(liability, 1, 0, 100, 0, 1000) == 8 && CoverRules.Premium(liability, 1, 10000, 100, 0, 1000) == 38, "a bigger business pays more");
        Check(CoverRules.District(fire, 400) == CompanyCatalogue.CoverDistrictMost && CoverRules.District(theft, 400) == CompanyCatalogue.CoverDistrictLeast, "the district's part of a premium has bounds");
        bool positive = true, grows = true;
        for (int kind = 0; kind < CompanyCatalogue.CoverKinds; kind++)
        {
            for (int insurer = 0; insurer < CompanyCatalogue.Insurers; insurer++)
            {
                for (int takings = 0; takings <= 200000; takings += 20000)
                {
                    int p = CoverRules.Premium(kind, insurer, takings, 100, 0, 1000);
                    positive &= p >= 1;
                    grows &= CoverRules.Premium(kind, insurer, takings + 20000, 100, 0, 1000) >= p && CoverRules.Premium(kind, insurer, takings, 100, 1, 1000) >= p;
                }
            }
        }
        Check(positive && grows, "a premium is one at least and never falls with size or with a claim");
        Check(CoverRules.Premium(fire, 2, int.MaxValue, 140, 50, 4000) > 0, "a premium never runs over");

        Check(CoverRules.Excess(1, 1000) == 150 && CoverRules.Excess(0, 500) == 200 && CoverRules.Excess(2, 30) == 30 && CoverRules.Excess(2, 10000) == 500, "the excess is a share of the loss, a stated sum at least, never more than the loss");
        Check(CoverRules.Payout(fire, 1, 1000) == 850 && CoverRules.Payout(fire, 0, 1000) == 700 && CoverRules.Payout(fire, 2, 1000) == 950, "a claim pays the loss less the excess");
        Check(CoverRules.Payout(theft, 1, 10000) == 6800, "a loss is counted up to the most the cover goes to");
        Check(CoverRules.Payout(liability, 2, 30) == 0 && CoverRules.Payout(liability, 0, 0) == 0 && CoverRules.Payout(liability, 0, -40) == 0, "a loss no bigger than the excess pays nothing");

        var p1 = new Policy();
        Check(!p1.Active && !CoverRules.Covers(p1, 10), "no policy covers nothing");
        CoverRules.Buy(p1, 1, 20, 10, 60);
        Check(p1.Active && p1.From == 10 && p1.Until == 69 && CoverRules.Covers(p1, 10) && CoverRules.Covers(p1, 69) && !CoverRules.Covers(p1, 9) && !CoverRules.Covers(p1, 70), "a policy covers its term");
        Check(!CoverRules.TermOver(p1, 69) && CoverRules.TermOver(p1, 70), "the term is over the day after its last");
        Check(!CoverRules.Charged(p1, false) && !CoverRules.Charged(p1, false) && p1.Missed == 2, "two premiums missed running leave the policy standing");
        Check(!CoverRules.Charged(p1, true) && p1.Missed == 0, "a premium paid clears the count");
        CoverRules.Charged(p1, false);
        CoverRules.Charged(p1, false);
        Check(CoverRules.Charged(p1, false) && !p1.Active, "three premiums missed running and the policy lapses");

        var p2 = new Policy();
        CoverRules.Buy(p2, 1, 20, 10, 60);
        p2.Claims = 1;
        Check(CoverRules.Renew(p2, fire, 2000, 100, 1000, 70, 60) && p2.Premium == 25 && p2.From == 70 && p2.Until == 129 && p2.Claims == 0, "a claim raises the next term's premium, and the count starts again");
        p2.Claims = 3;
        Check(!CoverRules.Renew(p2, fire, 2000, 100, 1000, 130, 60) && !p2.Active && !CoverRules.WillWrite(p2, 1) && CoverRules.WillWrite(p2, 0) && CoverRules.WillWrite(p2, 2),
            "after three claims in a term the middle insurer declines to renew and will not write that cover there again");
        Check(!CoverRules.Renews(0, 2) && CoverRules.Renews(0, 1) && CoverRules.Renews(2, 3) && !CoverRules.Renews(2, 4), "the cheap insurer stands one claim in a term, the dear one three");
    }

    private static void CompanyClaims()
    {
        var state = new CompanyState();
        var policy = new Policy();
        CoverRules.Buy(policy, 1, 20, 10, 60);
        Claim claim = CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 1000, 20);
        Check(claim != null && claim.Id == 1 && claim.Excess == 150 && claim.Payout == 850 && claim.Stage == ClaimStage.Open && state.Claims.Count == 1 && state.NextClaim == 2, "a covered loss opens a claim with its excess and what it pays");
        Check(CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 1000, 80) == null, "a loss after the term is not covered");
        Check(CoverRules.Open(state, new Policy(), "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 1000, 20) == null, "a loss with no policy is not covered: uninsured is allowed");
        Check(CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 90, 20) == null, "a loss under the excess opens no claim");
        Check(!CoverRules.Day(claim, 25) && claim.Stage == ClaimStage.Open, "a claim can be put in for five days");
        Check(CoverRules.File(claim, policy, 21) && claim.Stage == ClaimStage.Filed && claim.PayDay == 25 && policy.Claims == 1, "put in, it is paid four days later by the middle insurer and counts against the term");
        Check(!CoverRules.File(claim, policy, 22) && policy.Claims == 1, "a claim is put in once");
        Check(!CoverRules.Day(claim, 24) && CoverRules.Day(claim, 25) && claim.Stage == ClaimStage.Paid && !CoverRules.Day(claim, 26), "it is paid once, on its day");
        Claim late = CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 2000, 30);
        CoverRules.Day(late, 35);
        Check(late.Stage == ClaimStage.Open, "the fifth day is still in time");
        CoverRules.Day(late, 36);
        Check(late.Stage == ClaimStage.Lapsed && !CoverRules.File(late, policy, 36), "a claim not put in within five days lapses");
        for (int i = 0; i < 40; i++)
        {
            CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", (int)CoverKind.Fire, 1000 + i, 40);
        }
        Check(state.Claims.Count == CompanyCatalogue.ClaimsMost && state.ClaimOf(1) == null && state.Claims[state.Claims.Count - 1].Loss == 1039, "the list of claims is capped: the finished and the oldest go first");
    }

    private static void CompanyCampaigns()
    {
        int flyers = (int)CampaignKind.Flyers, radio = (int)CampaignKind.Radio, discount = (int)CampaignKind.DiscountDay, refit = (int)CampaignKind.Refit;
        Check(CampaignRules.Cost(radio, 1000) == 1200 && CampaignRules.Cost(radio, 1100) == 1320 && CampaignRules.Cost(flyers, 1040) == 160 && CampaignRules.Cost(discount, 1500) == 0, "a campaign's cost follows prices; a discount day is free to book");
        Check(CampaignRules.Suit(flyers, 1, 140) == 1200 && CampaignRules.Suit(flyers, 1, 60) == 800 && CampaignRules.Suit(flyers, -1, 60) == 1200 && CampaignRules.Suit(flyers, 0, 140) == 1000,
            "a local campaign is stronger where the district suits the business");
        Check(CampaignRules.Suit(radio, 1, 140) == 1000 && CampaignRules.Suit(radio, -1, 60) == 1000, "a radio spot is city-wide: the district does not matter");
        Check(CampaignRules.Suit(refit, 1, 400) == CompanyCatalogue.SuitMost && CampaignRules.Suit(refit, 1, -200) == CompanyCatalogue.SuitLeast, "how well a district suits has bounds");
        Check(CampaignRules.Repeat(0, 50) == 1000 && CampaignRules.Repeat(20, 20) == 500 && CampaignRules.Repeat(20, 21) == 550 && CampaignRules.Repeat(20, 25) == 750 && CampaignRules.Repeat(20, 30) == 1000 && CampaignRules.Repeat(20, 90) == 1000,
            "the same kind again straight away is worth half, and all of it again after ten days");
        Check(CampaignRules.Effect(flyers, 1000, CycleState.Steady, 1000) == 5 && CampaignRules.Effect(radio, 1000, CycleState.Steady, 1000) == 8 && CampaignRules.Effect(refit, 1000, CycleState.Steady, 1000) == 3,
            "a campaign's own figure in a steady economy and a district of neither kind");
        Check(CampaignRules.Effect(flyers, 1200, CycleState.Boom, 1000) == 7 && CampaignRules.Effect(radio, 1000, CycleState.Recession, 1000) == 6, "stronger in a boom and where the district suits, weaker in a recession");
        Check(CampaignRules.Effect(flyers, 1000, CycleState.Steady, 500) == 3, "repeated straight away it is weaker");
        Check(CampaignRules.Effect(discount, 1200, CycleState.Boom, 1000) == CompanyCatalogue.CampaignMost && CampaignRules.Effect(refit, 800, CycleState.Recession, 500) == 1, "no campaign adds more than twelve per cent or less than one");
        bool bounded = true, weaker = true;
        for (int kind = 0; kind < CompanyCatalogue.CampaignKinds; kind++)
        {
            for (int suit = CompanyCatalogue.SuitLeast; suit <= CompanyCatalogue.SuitMost; suit += 100)
            {
                for (int state = 0; state < 4; state++)
                {
                    for (int repeat = 500; repeat <= 1000; repeat += 100)
                    {
                        int e = CampaignRules.Effect(kind, suit, (CycleState)state, repeat);
                        bounded &= e >= 1 && e <= CompanyCatalogue.CampaignMost;
                        weaker &= repeat == 1000 || CampaignRules.Effect(kind, suit, (CycleState)state, repeat + 100) >= e;
                    }
                }
            }
        }
        Check(bounded, "every campaign's effect is inside its bounds wherever and whenever it runs");
        Check(weaker, "a campaign repeated sooner is never stronger");

        var shop = new ShopFile();
        Check(CampaignRules.CanBook(shop), "a business with no campaign can book one");
        CampaignRules.Book(shop, flyers, 150, 5, 10);
        Check(shop.Push == flyers && shop.PushFrom == 11 && shop.PushEnd == 14 && shop.PushCost == 150 && !CampaignRules.CanBook(shop), "booked today, flyers run from tomorrow for three days; one campaign at a time");
        Check(CampaignRules.Footfall(shop, 10) == 0 && CampaignRules.Footfall(shop, 11) == 5 && CampaignRules.Footfall(shop, 13) == 5 && CampaignRules.Footfall(shop, 14) == 0, "it adds its customers on its days and no others");
        Check(!CampaignRules.Over(shop, 13) && CampaignRules.Over(shop, 14), "it is over on the first day without it");
        CampaignRules.Close(shop);
        Check(shop.Push == -1 && shop.PushEnded[flyers] == 14 && CampaignRules.CanBook(shop) && CampaignRules.Repeat(shop.PushEnded[flyers], 16) == 600, "closed, its end is remembered for the next one of its kind");
        CampaignRules.Book(shop, discount, 0, 40, 20);
        Check(shop.PushPercent == CompanyCatalogue.CampaignMost && shop.PushEnd - shop.PushFrom == 1, "a booking never carries more than the most a campaign may add");
        Check(CampaignRules.Discount(2000) == 300 && CampaignRules.Discount(0) == 0 && CampaignRules.Discount(-50) == 0, "a discount day gives away fifteen per cent of the day's takings");
    }

    private static void CompanyReports()
    {
        // A scripted series: 100 customers, 1,000 taken and 300 made a day before; 120, 1,300 and 420 on the campaign's days.
        CampaignRules.DayBook book = (int day, out int customers, out int takings, out int profit) =>
        {
            bool during = day >= 11;
            customers = during ? 120 : 100;
            takings = during ? 1300 : 1000;
            profit = during ? 420 : 300;
            return day >= 1;
        };
        CampaignReport r = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Flyers, 11, 3, 150, book);
        Check(r.DaysBefore == 3 && r.DaysDuring == 3 && r.CustomersBefore == 300 && r.CustomersDuring == 360 && r.TakingsBefore == 3000 && r.TakingsDuring == 3900 && r.ProfitBefore == 900 && r.ProfitDuring == 1260,
            "the report adds up the campaign's days and the same number before it");
        Check(r.Footfall == 20 && r.Extra == 210 && r.Return == 140, "20 per cent more customers, 210 extra profit after the cost of 150, a return of 140 per cent");
        CampaignReport loss = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Radio, 11, 3, 1200, book);
        Check(loss.Extra == -840 && loss.Return == -70, "a campaign that cost more than it brought shows a loss");
        CampaignReport free = CampaignRules.Measure("Corner Shop", (int)CampaignKind.DiscountDay, 11, 1, 0, book);
        Check(free.DaysBefore == 1 && free.DaysDuring == 1 && free.Extra == 120 && free.Return == 0, "a campaign that cost nothing has no return to state");
        CampaignReport early = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Flyers, 2, 3, 150, book);
        Check(early.DaysBefore == 1 && early.DaysDuring == 3 && early.CustomersBefore == 100, "days the game has no figures for are left out and counted");
        CampaignReport none = CampaignRules.Measure(null, (int)CampaignKind.Flyers, 11, 3, 150, null);
        Check(none.DaysBefore == 0 && none.DaysDuring == 0 && none.Footfall == 0 && none.Extra == -150 && none.Name == string.Empty, "with no figures at all the report is empty and says so");
        var kept = new List<CampaignReport>();
        for (int i = 0; i < 10; i++)
        {
            CampaignRules.Keep(kept, new CampaignReport { From = i });
        }
        Check(kept.Count == CompanyCatalogue.ReportsKept && kept[0].From == 2 && kept[7].From == 9, "the last eight reports are kept");
    }

    private static void CompanyReturn()
    {
        Check(TaxRules.QuarterDays(5) == 15 && TaxRules.QuarterDays(30) == 90 && TaxRules.QuarterDays(0) == 3, "a quarter is three of the city's months");
        var t = new TaxFile { QuarterFrom = 10 };
        Check(!TaxRules.QuarterOver(t, 24, 15) && TaxRules.QuarterOver(t, 25, 15), "a quarter is over when its days have passed");
        TaxRules.Note(t, CostKind.Premiums, 400);
        TaxRules.Note(t, CostKind.Premiums, 100);
        TaxRules.Note(t, CostKind.Marketing, 1200);
        TaxRules.Note(t, CostKind.Wages, -50);
        Check(t.Costs[(int)CostKind.Premiums] == 500 && t.Costs[(int)CostKind.Wages] == 0 && TaxRules.CostsSum(t) == 1700, "the mod's own business costs of the quarter are added up by kind");
        TaxRules.Close(t, 60000, 25);
        Check(t.ReturnWaiting && t.ReturnDue == 30 && t.ReturnQuarter == 1 && t.ReturnTakings == 60000 && t.ReturnCosts == 1700 && t.Quarter == 2 && t.QuarterFrom == 25 && TaxRules.CostsSum(t) == 0,
            "when a quarter closes its return waits five days, and the next quarter starts from nothing");
        Check(TaxRules.Penalty(60000) == 300 && TaxRules.Penalty(1000) == CompanyCatalogue.PenaltyLeast && TaxRules.Penalty(5000000) == CompanyCatalogue.PenaltyMost && TaxRules.Penalty(-9) == CompanyCatalogue.PenaltyLeast,
            "the late penalty is half a per cent of the quarter's takings, 200 at least and 5,000 at most");
        Check(TaxRules.LateDay(t, 30) == 0 && t.LateRun == 0, "the last day to file is still in time");
        Check(TaxRules.LateDay(t, 31) == 300 && t.ReturnPenalties == 1 && t.LateRun == 1, "the first day late brings the penalty");
        int more = 0;
        for (int day = 32; day <= 35; day++)
        {
            more += TaxRules.LateDay(t, day);
        }
        Check(more == 0, "no second penalty in the next four days");
        Check(TaxRules.LateDay(t, 36) == 300 && TaxRules.LateDay(t, 41) == 300 && t.ReturnPenalties == 3 && t.LateRun == 1, "unfiled, it is charged again after each further five days");
        more = 0;
        for (int day = 42; day <= 80; day++)
        {
            more += TaxRules.LateDay(t, day);
        }
        Check(more == 0, "three penalties at most for one return");
        Check(TaxRules.File(t, 50) && !t.ReturnWaiting && t.LateRun == 1 && !TaxRules.File(t, 51), "filing late is late, and a return is filed once");

        var onTime = new TaxFile { QuarterFrom = 1, LateRun = 1 };
        TaxRules.Close(onTime, 60000, 16);
        Check(!TaxRules.File(onTime, 21) && onTime.LateRun == 0 && TaxRules.LateDay(onTime, 30) == 0, "a return filed on time ends a run of late ones and draws no penalty");

        var run = new TaxFile { QuarterFrom = 1 };
        TaxRules.Close(run, 60000, 16);
        TaxRules.LateDay(run, 22);
        Check(!TaxRules.LateAudit(run), "one late return brings no audit");
        TaxRules.Close(run, 60000, 31);   // the first was never filed: dropped, already counted
        Check(run.LateRun == 1 && run.ReturnQuarter == 2 && run.ReturnPenalties == 0, "a return never filed is dropped when the next quarter closes, and is not counted twice");
        TaxRules.LateDay(run, 37);
        Check(run.LateRun == 2 && TaxRules.LateAudit(run) && run.LateRun == 0 && !TaxRules.LateAudit(run), "two late returns running bring an audit, once");
    }

    private static void CompanyAccountant()
    {
        Check(TaxRules.Fee(0, 1000, false) == 300 && TaxRules.Fee(1, 1000, false) == 700 && TaxRules.Fee(2, 1000, false) == 1500 && TaxRules.Fee(2, 1100, false) == 1650, "an accountant's monthly fee follows prices");
        Check(TaxRules.Fee(0, 1000, true) == 450 && TaxRules.Fee(1, 1000, true) == 700, "the cheapest charges half as much again with cash off the books");
        Check(TaxRules.Resigns(2, true) && !TaxRules.Resigns(2, false) && !TaxRules.Resigns(1, true) && !TaxRules.Resigns(0, true), "the dearest resigns when cash is kept off the books");
        Check(TaxRules.Files(0, false) && TaxRules.Files(1, false) && TaxRules.Files(2, false) && !TaxRules.Files(-1, false), "an accountant files the return; nobody does without one");
        Check(TaxRules.Files(0, true) && !TaxRules.Files(1, true) && !TaxRules.Files(2, true), "with cash off the books only the cheapest still files");
        Check(TaxRules.Rebate(0, 10000) == 200 && TaxRules.Rebate(1, 10000) == 400 && TaxRules.Rebate(2, 10000) == 700 && TaxRules.Rebate(-1, 10000) == 0 && TaxRules.Rebate(2, -5) == 0,
            "an accountant finds 2, 4 or 7 per cent of the quarter's deductible costs");
        Check(TaxRules.AuditChance(1000, -1) == 1000 && TaxRules.AuditChance(1000, 0) == 1000 && TaxRules.AuditChance(1000, 1) == 800 && TaxRules.AuditChance(1000, 2) == 600, "a better accountant lowers the chance of an audit");
        int errors = 0;
        bool onlyCheap = true;
        for (int quarter = 1; quarter <= 5000; quarter++)
        {
            errors += TaxRules.Errs(0, 4242, quarter) ? 1 : 0;
            onlyCheap &= !TaxRules.Errs(1, 4242, quarter) && !TaxRules.Errs(2, 4242, quarter) && !TaxRules.Errs(-1, 4242, quarter);
        }
        Check(onlyCheap, "only the cheapest accountant makes errors");
        Check(errors > 850 && errors < 1150, "the cheapest gets about one return in five wrong (" + errors + " in 5000)");
        Check(TaxRules.ErrorPenalty(60000) == 150 && TaxRules.ErrorPenalty(0) == 100, "an error costs half a late penalty");
    }

    private static void CompanyHiddenCash()
    {
        Check(TaxRules.HideMost(CompanyCatalogue.Relaxed) == 10 && TaxRules.HideMost(CompanyCatalogue.Standard) == 30 && TaxRules.HideMost(CompanyCatalogue.Hardcore) == 30, "Relaxed stops at ten per cent off the books");
        Check(TaxRules.HideStep(30, CompanyCatalogue.Relaxed) == 10 && TaxRules.HideStep(20, CompanyCatalogue.Standard) == 20 && TaxRules.HideStep(7, CompanyCatalogue.Standard) == 5 && TaxRules.HideStep(-3, CompanyCatalogue.Standard) == 0
              && TaxRules.HideStep(99, CompanyCatalogue.Hardcore) == 30, "a share is brought to a step the strictness allows");
        Check(TaxRules.Saved(2000, 10, 20) == 40 && TaxRules.Saved(2000, 0, 20) == 0 && TaxRules.Saved(2000, 30, 0) == 0 && TaxRules.Saved(-10, 30, 20) == 0, "what is saved is the tax the hidden takings would have borne");
        Check(TaxRules.RiskDay(0, 30) == 60 && TaxRules.RiskDay(0, 5) == 10 && TaxRules.RiskDay(100, 0) == 60 && TaxRules.RiskDay(30, 0) == 0, "the risk grows with every per cent hidden and falls slowly when nothing is");
        int risk = 0, days = 0;
        bool never = true, rising = true;
        while (risk < CompanyCatalogue.RiskMost && days < 1000)
        {
            int next = TaxRules.RiskDay(risk, 30);
            rising &= next > risk;
            risk = next;
            never &= risk <= CompanyCatalogue.RiskMost;
            days++;
        }
        Check(days == 42 && never && rising && TaxRules.RiskDay(risk, 60) == CompanyCatalogue.RiskMost, "thirty per cent hidden at one business reaches the most risk in 42 days, and it never goes higher");
        int small = 0, large = 0;
        for (int d = 0; d < 20; d++)
        {
            small = TaxRules.RiskDay(small, 5);
            large = TaxRules.RiskDay(large, 30);
        }
        Check(large == 6 * small, "the risk grows six times as fast at thirty per cent as at five");
        int fall = 0;
        while (risk > 0)
        {
            risk = TaxRules.RiskDay(risk, 0);
            fall++;
        }
        Check(fall == 63, "from the most it takes 63 clean days to fall to nothing");

        var t = new TaxFile { Saved = 6000, Risk = 900 };
        int backTax, fine;
        int owed = TaxRules.Audit(t, CompanyCatalogue.Standard, 44, out backTax, out fine);
        Check(backTax == 6000 && fine == 3000 && owed == 9000 && t.Owed == 9000 && t.Saved == 0 && t.Risk == 0 && t.LastAudit == 44, "an audit finds all that was saved: it is owed again with a fine of half on Standard");
        Check(TaxRules.Fine(6000, CompanyCatalogue.Relaxed) == 1500 && TaxRules.Fine(6000, CompanyCatalogue.Realistic) == 4500 && TaxRules.Fine(6000, CompanyCatalogue.Hardcore) == 6000, "the fine is a quarter on Relaxed and the whole sum again on Hardcore");
        Check(TaxRules.Court(6000, CompanyCatalogue.Standard) && TaxRules.Court(5000, CompanyCatalogue.Hardcore) && !TaxRules.Court(4999, CompanyCatalogue.Hardcore), "back-tax of 5,000 or more goes to court");
        Check(!TaxRules.Court(6000, CompanyCatalogue.Relaxed) && !TaxRules.Court(900000, CompanyCatalogue.Relaxed), "it never goes to court on Relaxed");
        Check(TaxRules.TakeOwed(t, 5000) == 5000 && t.Owed == 4000 && TaxRules.TakeOwed(t, 0) == 0 && TaxRules.TakeOwed(t, 99999) == 4000 && t.Owed == 0, "what is owed is taken as far as the cash goes and the rest is carried");
        var clean = new TaxFile { Risk = 500 };
        Check(TaxRules.Audit(clean, CompanyCatalogue.Hardcore, 9, out backTax, out fine) == 0 && clean.Owed == 0, "an audit that finds nothing costs nothing");
        var chance = new TaxFile { Risk = CompanyCatalogue.RiskMost };
        int audits = 0, none = 0;
        var quiet = new TaxFile();
        for (int age = 0; age < 10000; age++)
        {
            audits += TaxRules.Audited(chance, 31337, age) ? 1 : 0;
            none += TaxRules.Audited(quiet, 31337, age) ? 1 : 0;
        }
        Check(none == 0, "with no risk there is never an audit");
        Check(audits > 2250 && audits < 2750, "at the most risk an audit comes on about one day in four (" + audits + " in 10000)");
    }

    private static void CompanyLicences()
    {
        int hygiene = (int)LicenceKind.Hygiene, alcohol = (int)LicenceKind.Alcohol, late = (int)LicenceKind.Late, signage = (int)LicenceKind.Signage;
        Check(LicenceRules.Needs(true, false, false) == 9 && LicenceRules.Needs(false, true, true) == 14 && LicenceRules.Needs(false, false, false) == 8, "every business needs signage; food, alcohol and late opening need their own");
        Check(LicenceRules.Needed(9, hygiene) && LicenceRules.Needed(9, signage) && !LicenceRules.Needed(9, alcohol) && !LicenceRules.Needed(9, late), "a business needs exactly the licences of what it does");
        Check(LicenceRules.IsLate(8, 23) && !LicenceRules.IsLate(8, 22) && LicenceRules.IsLate(4, 12) && !LicenceRules.IsLate(5, 18) && !LicenceRules.IsLate(9, 9), "open past ten at night or before five in the morning is late opening");
        Check(LicenceRules.Fee(alcohol, 1000) == 900 && LicenceRules.Fee(hygiene, 1100) == 440 && LicenceRules.Fine(alcohol, 1000) == 1800, "a fee follows prices and the fine is twice the fee");
        Check(LicenceRules.Shuts(hygiene) && LicenceRules.Shuts(alcohol) && !LicenceRules.Shuts(late) && !LicenceRules.Shuts(signage), "only food hygiene and alcohol shut the doors");

        var old = new ShopFile();
        LicenceRules.First(old, 9, 20, true);
        Check(old.Permits[hygiene].Until == 49 && old.Permits[signage].Until == 59 && old.Permits[alcohol].Until == 0 && old.Permits[late].Until == 0,
            "a business that already ran gets every licence it needs for a first term, free, and none it does not need");
        Check(LicenceRules.Missing(old, 9, 20) == 0 && LicenceRules.Missing(old, 9, 49) == 0 && LicenceRules.Missing(old, 9, 50) == 1 && LicenceRules.Missing(old, 9, 60) == 9, "a licence is good to its last day and missing from the next");
        Check(LicenceRules.Missing(old, 13, 20) == 4, "a need that appears later (late opening) is missing until it is bought");
        var opened = new ShopFile();
        LicenceRules.First(opened, 14, 20, false);
        Check(opened.Permits[alcohol].Until == 24 && LicenceRules.Missing(opened, 14, 24) == 0 && LicenceRules.Missing(opened, 14, 25) == 14, "a business opened later may trade five days before it needs them");

        Licence l = old.Permits[hygiene];
        Check(LicenceRules.Day(l, 46) == LicenceNote.None && LicenceRules.Day(l, 47) == LicenceNote.Remind && LicenceRules.Day(l, 48) == LicenceNote.None, "the reminder goes out once, three days before the end");
        Check(LicenceRules.Day(l, 50) == LicenceNote.Lapsed && LicenceRules.Day(l, 51) == LicenceNote.None, "it lapses the day after its last, and that is said once");
        LicenceRules.Grant(l, hygiene, 55);
        Check(l.Until == 84 && !l.Reminded && LicenceRules.Valid(l, 84) && !LicenceRules.Valid(l, 85), "bought after a lapse, the term runs from the day it is bought");
        LicenceRules.Grant(l, hygiene, 60);
        Check(l.Until == 114, "renewed early, the new term is added on at the end");
        var auto = new Licence { Until = 49, Auto = true };
        Check(LicenceRules.Day(auto, 47) == LicenceNote.None && LicenceRules.Day(auto, 50) == LicenceNote.Renew, "one that renews by itself needs no reminder and is renewed the day after its last");
        LicenceRules.Grant(auto, signage, 50);
        Check(auto.Until == 89 && LicenceRules.Valid(auto, 50), "renewed by itself, there is no day without it");
        Check(LicenceRules.Day(new Licence(), 5) == LicenceNote.None, "a licence never held needs nothing said");
        int asked = 0;
        bool sameAnswer = true;
        for (int age = 0; age < 10000; age++)
        {
            bool today = LicenceRules.Checked(2024, age, 3);
            asked += today ? 1 : 0;
            sameAnswer &= today == LicenceRules.Checked(2024, age, 3);
        }
        Check(sameAnswer && asked > 850 && asked < 1150, "where a licence is missing an inspection is asked for on about one day in ten, the same days for the same seed (" + asked + " in 10000)");
    }

    private static CompanyState FullCompany()
    {
        var s = new CompanyState { Seed = 12345, FirstDay = 3, LastDay = 77, Age = 74, SickPolicy = 2, PoachRest = 80, NextClaim = 9 };
        TaxFile t = s.Tax;
        t.QuarterFrom = 61;
        t.Quarter = 5;
        for (int k = 0; k < t.Costs.Length; k++)
        {
            t.Costs[k] = 100 + k;
        }
        t.ReturnDue = 66;
        t.ReturnQuarter = 4;
        t.ReturnTakings = 61234;
        t.ReturnCosts = 2345;
        t.ReturnPenalties = 2;
        t.LateRun = 1;
        t.Accountant = 1;
        t.AccountantNext = 79;
        t.Risk = 820;
        t.Saved = 4321;
        t.Owed = 250;
        t.LastAudit = 40;
        t.CaseNumber = 7;
        for (int i = 0; i < 2; i++)
        {
            var shop = new ShopFile { Place = "Main%20Street~" + (i + 1), FirstDay = 3 + i, Hide = i == 0 ? 20 : 0 };
            shop.Push = i == 0 ? (int)CampaignKind.Radio : -1;
            shop.PushFrom = i == 0 ? 75 : 0;
            shop.PushEnd = i == 0 ? 81 : 0;
            shop.PushCost = i == 0 ? 1200 : 0;
            shop.PushPercent = i == 0 ? 8 : 0;
            for (int k = 0; k < shop.PushEnded.Length; k++)
            {
                shop.PushEnded[k] = 10 * k + i;
            }
            shop.StrikeFrom = 78 + i;
            shop.StrikeEnd = 80 + i;
            shop.StrikeRest = 30 + i;
            shop.OvertimeAsk = 76 + i;
            shop.OvertimeFrom = 70 + i;
            shop.OvertimeEnd = 73 + i;
            shop.OvertimeRest = 83 + i;
            shop.OvertimeCost = 90 + i;
            shop.LossRun = 2 + i;
            for (int k = 0; k < shop.Cover.Length; k++)
            {
                if (k != 1)
                {
                    CoverRules.Buy(shop.Cover[k], (k + i) % 3, 20 + k, 10 + k, 60);
                    shop.Cover[k].Claims = k;
                    shop.Cover[k].Missed = 1;
                }
                shop.Cover[k].Declined = 1 << ((k + 1) % 3);
            }
            for (int k = 0; k < shop.Permits.Length; k++)
            {
                shop.Permits[k].Until = 80 + k + i;
                shop.Permits[k].Auto = (k + i) % 2 == 0;
                shop.Permits[k].Reminded = (k + i) % 3 == 0;
            }
            s.Shops.Add(shop);
        }
        for (int i = 0; i < 5; i++)
        {
            s.Staff.Add(new StaffFile
            {
                Id = "AbC" + i + "+/=", Place = "Main%20Street~" + (i % 2 + 1), Standing = 20 + 15 * i, Raise = 25 * i, UnderDays = i, AskDay = i == 1 ? 76 : 0, AskTo = i == 1 ? 1300 : 0, AskRest = 60 + i,
                PoachDay = i == 2 ? 75 : 0, PoachOffer = i == 2 ? 1400 : 0, PoachRest = 50 + i, LeaveDay = i == 3 ? 79 : 0, Rival = i == 2 ? "Rival & Sons" : string.Empty
            });
        }
        var policy = new Policy();
        CoverRules.Buy(policy, 2, 30, 1, 400);
        Claim a = CoverRules.Open(s, policy, "Main%20Street~1", "Corner Shop", (int)CoverKind.Fire, 1500, 70);
        CoverRules.File(a, policy, 71);
        CoverRules.Open(s, policy, "Main%20Street~2", "The Bar", (int)CoverKind.Theft, 900, 76);
        s.NextClaim = 9;
        s.Reports.Add(new CampaignReport { Name = "Corner Shop", Kind = 1, From = 40, Days = 6, DaysBefore = 6, DaysDuring = 5, CustomersBefore = 600, CustomersDuring = 700, TakingsBefore = 6000, TakingsDuring = 7400, ProfitBefore = -200, ProfitDuring = 2100, Cost = 1200 });
        s.Reports.Add(new CampaignReport { Name = "The Bar", Kind = 2, From = 50, Days = 1, DaysBefore = 1, DaysDuring = 1, CustomersBefore = 90, CustomersDuring = 101, TakingsBefore = 2000, TakingsDuring = 2200, ProfitBefore = 500, ProfitDuring = 520, Cost = 330 });
        return s;
    }

    private static void CompanySaveAndLoad()
    {
        CompanyState s = FullCompany();
        string line = CompanyStore.Write(s);
        int version;
        CompanyState back = CompanyStore.Read(line, out version);
        Check(back != null && version == CompanyStore.Version, "a written line is read back");
        Check(back != null && CompanyStore.Write(back) == line, "what is read back writes the same line again: nothing is lost or changed");
        if (back != null)
        {
            Check(back.Seed == 12345 && back.FirstDay == 3 && back.LastDay == 77 && back.Age == 74 && back.SickPolicy == 2 && back.PoachRest == 80 && back.NextClaim == 9, "the header comes back");
            Check(back.Tax.Quarter == 5 && back.Tax.Costs[4] == 104 && back.Tax.ReturnDue == 66 && back.Tax.ReturnTakings == 61234 && back.Tax.Accountant == 1 && back.Tax.Risk == 820 && back.Tax.Saved == 4321
                  && back.Tax.Owed == 250 && back.Tax.CaseNumber == 7 && back.Tax.LateRun == 1, "tax comes back");
            ShopFile shop = back.Shop("Main%20Street~1");
            Check(back.Shops.Count == 2 && shop != null && shop.Hide == 20 && shop.Push == (int)CampaignKind.Radio && shop.PushEnd == 81 && shop.PushPercent == 8 && shop.PushEnded[3] == 30 && shop.StrikeFrom == 78
                  && shop.OvertimeCost == 90 && shop.LossRun == 2, "a business comes back with its campaign, its strike and its overtime");
            Check(shop != null && shop.Cover[0].Insurer == 0 && shop.Cover[0].Premium == 20 && shop.Cover[0].Until == 69 && !shop.Cover[1].Active && shop.Cover[1].Declined == 4 && shop.Cover[2].Claims == 2
                  && shop.Cover[2].Missed == 1, "its policies come back, and who has declined");
            Check(shop != null && shop.Permits[0].Until == 80 && shop.Permits[0].Auto && shop.Permits[0].Reminded && !shop.Permits[1].Auto && shop.Permits[3].Until == 83, "its licences come back");
            StaffFile f = back.Employee("AbC2+/=");
            Check(back.Staff.Count == 5 && f != null && f.Standing == 50 && f.Raise == 50 && f.PoachDay == 75 && f.PoachOffer == 1400 && f.Rival == "Rival & Sons" && back.Employee("AbC3+/=").LeaveDay == 79
                  && back.Employee("AbC1+/=").AskTo == 1300, "the staff come back by the game's id, with what was agreed and what is waiting");
            Check(back.Claims.Count == 2 && back.Claims[0].Stage == ClaimStage.Filed && back.Claims[0].Payout == 1425 && back.Claims[0].PayDay == 73 && back.Claims[1].Name == "The Bar" && back.Claims[1].Stage == ClaimStage.Open,
                "the claims come back");
            Check(back.Reports.Count == 2 && back.Reports[0].ProfitBefore == -200 && back.Reports[0].Extra == 1100 && back.Reports[1].Name == "The Bar" && back.Reports[1].Cost == 330, "the reports come back, a loss included");
        }

        // A later version: a higher number, a part this reader does not know, and fields added at the end of every record.
        string later = line.Replace("v=1^", "v=2^").Replace("^t:", "|99|98^z:something|new;more^t:").Replace("^s:", "|77^s:").Replace("^e:", "|1|2|3^e:").Replace("^c:", "|extra|9^c:").Replace("^r:", "|5^r:") + "|6|7";
        CompanyState newer = CompanyStore.Read(later, out version);
        Check(newer != null && version == 2, "a line written by a later version still loads");
        Check(newer != null && newer.Seed == 12345 && newer.Tax.CaseNumber == 7 && newer.Shops.Count == 2 && newer.Staff.Count == 5 && newer.Claims.Count == 2 && newer.Reports.Count == 2
              && newer.Shops[1].Permits[3].Until == 84 && newer.Staff[4].Raise == 100 && newer.Reports[1].Name == "The Bar", "the fields it knows are read and what it does not know is skipped");
        Check(newer != null && CompanyStore.Write(newer) == line, "written again by this version, it is this version's line");

        Check(CompanyStore.Read(null, out version) == null && CompanyStore.Read(string.Empty, out version) == null && CompanyStore.Read("nonsense", out version) == null
              && CompanyStore.Read("v=x^h:1", out version) == null && CompanyStore.Read("v=0^h:1", out version) == null && CompanyStore.Read("v=1^t:1|2", out version) == null,
            "a line that is not this feature's is not read");
        CompanyState wild = CompanyStore.Read("v=1^h:5|-3|-9|-1|44|-2|-6^t:-5|0|-1|-2|-3|-4|-5|-6|-7|-8|-9|99|-1|42|-3|999999|-4|-5|-6|-7^s:A~1|-4|17|99|1|2|3|900^e:x|A~1|500|-40^c:0|A~1|9|1|1|1|1|1|1|1^r:99|1|1", out version);
        Check(wild != null && wild.FirstDay == 0 && wild.SickPolicy == 2 && wild.NextClaim == 1 && wild.Tax.Quarter == 1 && wild.Tax.Costs[0] == 0 && wild.Tax.ReturnPenalties == CompanyCatalogue.PenaltiesMost
              && wild.Tax.Accountant == CompanyCatalogue.Accountants - 1 && wild.Tax.Risk == CompanyCatalogue.RiskMost && wild.Tax.Owed == 0, "figures out of their bounds are put inside them");
        Check(wild != null && wild.Shops.Count == 1 && wild.Shops[0].Hide == 0 && wild.Shops[0].Push == -1 && wild.Shops[0].PushPercent == 0 && wild.Staff.Count == 1 && wild.Staff[0].Standing == CompanyCatalogue.StandingMost
              && wild.Staff[0].Raise == 0 && wild.Claims.Count == 0 && wild.Reports.Count == 0, "a share that is no step, a campaign, a claim and a report of no known kind are dropped");
        Check(CompanyStore.Clean("a|b;c^d") == "a b c d" && CompanyStore.Clean(null) == string.Empty, "a name can never break the line apart");
        var named = new CompanyState { Seed = 1 };
        named.Staff.Add(new StaffFile { Id = "id1", Place = "A~1", Rival = "Smith|Jones;Co^Ltd" });
        CompanyState namedBack = CompanyStore.Read(CompanyStore.Write(named), out version);
        Check(namedBack != null && namedBack.Staff.Count == 1 && namedBack.Staff[0].Rival == "Smith Jones Co Ltd", "a rival's name with a divider in it is stored without it");

        var big = new CompanyState { Seed = 1 };
        for (int i = 0; i < CompanyCatalogue.ShopsMost + 20; i++)
        {
            big.Shops.Add(new ShopFile { Place = "S~" + i });
        }
        for (int i = 0; i < CompanyCatalogue.StaffMost + 50; i++)
        {
            big.Staff.Add(new StaffFile { Id = "E" + i, Place = "S~1" });
        }
        for (int i = 0; i < CompanyCatalogue.ReportsKept + 5; i++)
        {
            big.Reports.Add(new CampaignReport { From = i });
        }
        CompanyState capped = CompanyStore.Read(CompanyStore.Write(big), out version);
        Check(capped != null && capped.Shops.Count == CompanyCatalogue.ShopsMost && capped.Staff.Count == CompanyCatalogue.StaffMost && capped.Reports.Count == CompanyCatalogue.ReportsKept && capped.Reports[0].From == 5,
            "the line is capped: 64 businesses, 400 staff and the last eight reports");
        var data = new Dictionary<string, string> { { CompanyStore.Key, line } };
        Check(CompanyStore.Load(data, CompanyStore.Key, out version) != null && CompanyStore.Load(data, CompanyStore.TestKey, out version) == null && CompanyStore.Load(null, CompanyStore.Key, out version) == null,
            "the line is found under its own key and nowhere else");
        Check(CompanyStore.Key == "reallife:economy_company", "the save key is the one the feature list names");
    }

    // A scripted company for a number of days: every rule that draws by the seed is called, and the line is what is compared.
    private static string CompanyHistory(int seed, int level, int days)
    {
        var s = new CompanyState { Seed = seed, FirstDay = 1 };
        for (int i = 0; i < 2; i++)
        {
            var shop = new ShopFile { Place = "S~" + i, FirstDay = 1, Hide = i == 0 ? TaxRules.HideStep(20, level) : 0 };
            LicenceRules.First(shop, LicenceRules.Needs(i == 0, i == 1, false), 1, true);
            s.Shops.Add(shop);
        }
        for (int i = 0; i < 6; i++)
        {
            StaffFile f = Worker(1000 + 20 * i, 1250, 40 + 10 * i);
            f.Id = "E" + i;
            f.Place = "S~" + i % 2;
            s.Staff.Add(f);
        }
        s.Tax.QuarterFrom = 1;
        for (int day = 2; day <= days; day++)
        {
            s.Age++;
            s.LastDay = day;
            int wanted = 0;
            for (int i = 0; i < s.Staff.Count; i++)
            {
                StaffFile f = s.Staff[i];
                if (StaffRules.Day(f, level, day) == StaffNote.Asked && i % 2 == 0)
                {
                    StaffRules.Answer(f, RaiseAnswer.Less, day);
                }
                if (StaffRules.OfferLapsed(f, day))
                {
                    StaffRules.OfferAnswer(f, false, day);
                }
                wanted += StaffRules.Wanted(f, level, day) ? 1 : 0;
            }
            if (wanted > 0 && day >= s.PoachRest && StaffRules.Approach(s.Seed, s.Age, level))
            {
                int pick = StaffRules.Who(s.Seed, s.Age, wanted);
                for (int i = 0; i < s.Staff.Count; i++)
                {
                    if (StaffRules.Wanted(s.Staff[i], level, day) && pick-- == 0)
                    {
                        StaffRules.Offer(s.Staff[i], "Rival", day);
                        s.PoachRest = day + CompanyCatalogue.PoachCompanyRestDays;
                        break;
                    }
                }
            }
            int hidden = 0;
            for (int i = 0; i < s.Shops.Count; i++)
            {
                ShopFile shop = s.Shops[i];
                int low = 0, total = 0;
                for (int k = 0; k < s.Staff.Count; k++)
                {
                    if (s.Staff[k].Place == shop.Place && s.Staff[k].LeaveDay == 0)
                    {
                        total++;
                        low += StaffRules.StandsLow(s.Staff[k]) ? 1 : 0;
                    }
                }
                if (shop.StrikeFrom > 0 && day >= shop.StrikeEnd)
                {
                    StaffRules.End(shop, day);
                }
                else if (StaffRules.CanStrike(shop, day) && StaffRules.StrikeDue(low, total, level))
                {
                    StaffRules.Call(shop, level, day);
                }
                StaffRules.OvertimeDay(shop, day);
                if (StaffRules.OvertimeOffered(shop, true, s.Seed, s.Age, i, day))
                {
                    shop.OvertimeAsk = day;
                }
                hidden += shop.Hide;
                s.Tax.Saved += TaxRules.Saved(2000, shop.Hide, 20);
            }
            s.Tax.Risk = TaxRules.RiskDay(s.Tax.Risk, hidden);
            if (TaxRules.Audited(s.Tax, s.Seed, s.Age))
            {
                int backTax, fine;
                TaxRules.Audit(s.Tax, level, day, out backTax, out fine);
            }
            if (TaxRules.QuarterOver(s.Tax, day, 15))
            {
                TaxRules.Close(s.Tax, 30000, day);
                if (TaxRules.Errs(0, s.Seed, s.Tax.ReturnQuarter))
                {
                    s.Tax.Owed += TaxRules.ErrorPenalty(30000);
                }
                TaxRules.File(s.Tax, day);
            }
        }
        return CompanyStore.Write(s);
    }

    private static void CompanyDeterminism()
    {
        bool same = true, differ = false, relaxedQuiet = true;
        string first = null;
        for (int seed = 1; seed <= 12; seed++)
        {
            for (int level = 0; level < 4; level++)
            {
                string a = CompanyHistory(seed * 7919, level, 240);
                string b = CompanyHistory(seed * 7919, level, 240);
                same &= a == b;
                if (level == CompanyCatalogue.Standard)
                {
                    differ |= first != null && a != first;
                    first = first ?? a;
                }
                if (level == CompanyCatalogue.Relaxed)
                {
                    int version;
                    CompanyState s = CompanyStore.Read(a, out version);
                    for (int i = 0; s != null && i < s.Shops.Count; i++)
                    {
                        relaxedQuiet &= s.Shops[i].StrikeFrom == 0 && s.Shops[i].StrikeRest == 0 && s.Shops[i].Hide <= CompanyCatalogue.HideMostRelaxed;
                    }
                    for (int i = 0; s != null && i < s.Staff.Count; i++)
                    {
                        relaxedQuiet &= s.Staff[i].LeaveDay == 0 && s.Staff[i].PoachRest == 0;
                    }
                }
            }
        }
        Check(same, "the same seed and the same days give the same history, to the last figure, at every strictness");
        Check(differ, "another seed gives another history");
        Check(relaxedQuiet, "240 days on Relaxed from twelve seeds: no strike was ever called, nobody was poached, and no business hides more than ten per cent");
    }
}
