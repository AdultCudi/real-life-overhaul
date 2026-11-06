using System;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// A landlord's duties (Scripts/Economy/Estate/EstateRules.Duties.cs): a tenant's satisfaction from the building, the
// rent and how they are treated, and what it does to renewing, missing and staying; repair calls by condition, their
// quotes, a contractor and the player's own job; the city's inspections, fines, the order and the bar on lettings;
// a reminder and a payment plan; what a letting agent and a property manager do and charge; the option off leaving
// a tenancy's day exactly as before; the duties through the save and back, and a line from before them; and the same
// seed giving the same history, with a year of a full city of buildings inside the time a day allows.
internal static partial class Program
{
    private static void EstateDuties()
    {
        EstateDutyHappy();
        EstateDutyEffects();
        EstateDutyCalls();
        EstateDutyInspections();
        EstateDutyArrears();
        EstateDutyManagers();
        EstateDutyOff();
        EstateDutySave();
        EstateDutyDeterminismAndTiming();
    }

    // A building of four units with a tenant in each at the market's rent, judged once.
    private static EstateHolding DutyBuilding(int cond = 800, int record = 80)
    {
        EstateHolding h = Units(4, 1000000, cond);
        EstateClimate c = Climate();
        int rent = EstateRules.UnitRent(h, c);
        for (int u = 0; u < 4; u++)
        {
            LetUnit(h, u, rent, record, 1, 150);
            h.Units[u].Happy = EstateRules.HappyTarget(h, u, 1, c);
        }
        return h;
    }

    private static void EstateDutyHappy()
    {
        EstateClimate c = Climate();
        EstateHolding good = DutyBuilding(1000), poor = DutyBuilding(300);
        int atGood = EstateRules.HappyTarget(good, 0, 2, c), atPoor = EstateRules.HappyTarget(poor, 0, 2, c);
        Check(atGood > atPoor && atGood >= EstateCatalogue.HappyGood - 50 && atPoor < EstateCatalogue.HappyFair, "a tenant is happier in a building in good condition ("
            + atGood + " against " + atPoor + ")");
        EstateHolding h = DutyBuilding();
        int at = EstateRules.HappyTarget(h, 0, 2, c);
        h.Units[0].Rent = EstateRules.UnitRent(h, c) * 12 / 10;
        int dear = EstateRules.HappyTarget(h, 0, 2, c);
        h.Units[0].Rent = EstateRules.UnitRent(h, c) * 8 / 10;
        int cheap = EstateRules.HappyTarget(h, 0, 2, c);
        Check(dear < at && cheap > at, "a rent over the market's annoys a tenant and one under it pleases them");
        h.Units[0].Rent = EstateRules.UnitRent(h, c) * 10;
        Check(at - EstateRules.HappyTarget(h, 0, 2, c) == -EstateCatalogue.HappyRentLeast, "however dear the rent, it takes no more than its most off");
        h.Units[0].Rent = EstateRules.UnitRent(h, c);
        h.Level = 2;
        h.Units[0].Rent = EstateRules.UnitRent(h, c);   // a refurbished unit's market rent is higher: let at it, only the level counts
        Check(EstateRules.HappyTarget(h, 0, 2, c) == at + 2 * EstateCatalogue.HappyLevel, "each level of refurbishment pleases a tenant a little more");
        h.Level = 0;
        h.Units[0].Rent = EstateRules.UnitRent(h, c);
        EstateRules.RaiseCall(h, 0, 5, 10, c);
        Check(EstateRules.HappyTarget(h, 0, 10, c) == at - EstateCatalogue.HappyCall && EstateRules.HappyTarget(h, 0, 10 + EstateCatalogue.CallGraceDays, c) == at - EstateCatalogue.HappyCall,
            "an open repair annoys at once, the same within the grace");
        Check(EstateRules.HappyTarget(h, 0, 15 + EstateCatalogue.CallGraceDays, c) == at - EstateCatalogue.HappyCall - 5 * EstateCatalogue.HappyCallPerDay
              && EstateRules.HappyTarget(h, 0, 400, c) == Math.Max(0, at - EstateCatalogue.HappyCallMost), "then more for each day it waits, up to its most");
        h.Units[0].CallClosed();
        h.Units[0].NoticeEnd = 30;
        h.OrderDay = 40;
        Check(EstateRules.HappyTarget(h, 0, 10, c) == at - EstateCatalogue.HappyNotice - EstateCatalogue.HappyOrder, "notice served and the city's order both annoy");
        Check(EstateRules.HappyTarget(h, 3, 10, c) == at - EstateCatalogue.HappyOrder && EstateRules.HappyTarget(Units(2), 0, 10, c) == 0, "notice is the one tenant's; nobody in is nothing");
        Check(EstateRules.Drift(-1, 640) == 640 && EstateRules.Drift(500, 600) == 515 && EstateRules.Drift(600, 500) == 585 && EstateRules.Drift(600, 603) == 601 && EstateRules.Drift(600, 600) == 600,
            "satisfaction is set at once when first judged, then closes part of the gap a day, a point at least");
        int happy = 100;
        for (int day = 0; day < 60; day++)
        {
            happy = EstateRules.Drift(happy, 900);
        }
        Check(happy == 900, "and in two months it has reached its target from far below");
        Check(EstateRules.Drift(0, -50) == 0 && EstateRules.Drift(1000, 2000) == 1000, "it stays within 0 and 1000");
    }

    private static void EstateDutyEffects()
    {
        Check(EstateRules.RenewChance(0) == EstateCatalogue.RenewLeast && EstateRules.RenewChance(1000) == EstateCatalogue.RenewMost
              && EstateRules.RenewChance(800) > EstateRules.RenewChance(500) && EstateRules.RenewChance(500) > EstateRules.RenewChance(200), "a happier tenant renews more often, within the bounds");
        Check(Math.Abs(EstateRules.RenewChance(700) - EstateCatalogue.RenewChance) <= 50, "a tenant as content as most renews about as often as before the duties");
        Check(EstateRules.UnhappyMiss(100, -1) == 100 && EstateRules.UnhappyMiss(100, EstateCatalogue.HappyFair) == 100 && EstateRules.UnhappyMiss(100, 800) == 100,
            "a content tenant, or one not yet judged, misses rent as before");
        Check(EstateRules.UnhappyMiss(100, 0) == 130 && EstateRules.UnhappyMiss(100, 250) == 115 && EstateRules.UnhappyMiss(999, 0) == 1000, "an unhappy one misses more, at most every day");
        EstateClimate c = Climate();
        int renewedHappy = 0, renewedAngry = 0;
        for (int s = 1; s <= 400; s++)
        {
            EstateHolding a = Let(EstateRules.MarketRent(Building(), c), 95, 1, 60), b = Let(EstateRules.MarketRent(Building(), c), 95, 1, 60);
            a.Units[0].Happy = 950;
            b.Units[0].Happy = 50;
            renewedHappy += EstateRules.Day(a, 0, s, 62, c, true).Renewed ? 1 : 0;
            renewedAngry += EstateRules.Day(b, 0, s, 62, c, true).Renewed ? 1 : 0;
        }
        Check(renewedHappy > 280 && renewedAngry < 80, "at a lease's end a happy tenant mostly renews and an angry one mostly goes (" + renewedHappy + " and " + renewedAngry + " of 400)");
        EstateHolding q = DutyBuilding();
        q.Units[1].Happy = 100;
        bool said = false, gone = false;
        for (int day = 2; day < 400 && !gone; day++)
        {
            q.Units[1].Happy = Math.Min(q.Units[1].Happy, 100);
            DutyDay d = EstateRules.Duty(q, 1, 3, day, c);
            said |= d.Quits;
            if (said && day >= q.Units[1].QuitDay && q.Units[1].QuitDay > 0)
            {
                gone = EstateRules.Day(q, 1, 3, day, c, true).Quit;
            }
        }
        Check(said && gone && !q.Units[1].Let, "a tenant kept angry says they go, and goes on the day they said");
        EstateHolding r = DutyBuilding();
        r.Units[2].Happy = 100;
        r.Units[2].QuitDay = 50;
        r.Units[2].Happy = EstateCatalogue.QuitBelow + EstateCatalogue.StayAbove + 100;
        r.Cond = 1000;
        DutyDay stay = EstateRules.Duty(r, 2, 3, 20, c);
        Check(stay.Stays && r.Units[2].QuitDay == 0, "made happier before the day, they stay");
        EstateHolding w = DutyBuilding();
        w.Units[0].QuitDay = 30;
        Check(!EstateRules.Day(w, 0, 3, 30, c, false).Quit && w.Units[0].Let, "with the option off a tenant's word to go is not acted on");
        int complaints = 0;
        EstateHolding m = DutyBuilding(300);
        for (int day = 2; day < 202; day++)
        {
            m.Units[3].Happy = 300;
            complaints += EstateRules.Duty(m, 3, 9, day, c).Complained ? 1 : 0;
            m.Units[3].CallClosed();
            m.InspectDay = 0;
        }
        Check(complaints > 4 && complaints < 40, "an unhappy tenant complains now and then (" + complaints + " times in 200 days)");
        EstateHolding pleased = DutyBuilding(1000);
        int none = 0;
        for (int day = 2; day < 202; day++)
        {
            pleased.Units[0].Happy = 900;
            none += EstateRules.Duty(pleased, 0, 9, day, c).Complained ? 1 : 0;
            pleased.Units[0].CallClosed();
        }
        Check(none == 0, "a happy tenant never complains");
        Check(EstateRules.Grievance(DutyBuilding(200), 0, 5, c) == 1, "in a building falling apart, it is the building they mind");
        EstateHolding g = DutyBuilding();
        g.Units[0].Rent *= 2;
        Check(EstateRules.Grievance(g, 0, 5, c) == 0, "paying twice the market, it is the rent");
        EstateRules.RaiseCall(g, 1, 1, 1, c);
        Check(EstateRules.Grievance(g, 1, 20, c) == 2, "a repair left two weeks, it is the repair");
    }

    private static void EstateDutyCalls()
    {
        EstateClimate c = Climate();
        Check(EstateRules.CallChance(1000) == EstateCatalogue.CallBase && EstateRules.CallChance(0) == EstateCatalogue.CallBase + 1000 / EstateCatalogue.CallPerPoint
              && EstateRules.CallChance(300) > EstateRules.CallChance(700), "faults are reported more often the worse the building");
        EstateHolding h = DutyBuilding();
        int calls = 0;
        for (int day = 2; day < 1002; day++)
        {
            for (int u = 0; u < 4; u++)
            {
                h.Units[u].Happy = 700;
                DutyDay d = EstateRules.Duty(h, u, 21, day, c);
                calls += d.NewCall ? 1 : 0;
                h.Units[u].CallClosed();
            }
            h.Cond = 800;
            h.InspectDay = 0;
        }
        double perDay = calls / 4000.0;
        Check(Math.Abs(perDay - EstateRules.CallChance(800) / 1000.0) < 0.006, "over many days a unit reports at its chance (" + perDay.ToString("0.0000") + " a day)");
        EstateHolding one = DutyBuilding(800);
        Check(EstateRules.RaiseCall(one, 0, 7, 5, c) && one.Units[0].Call >= 1 && one.Units[0].Call <= EstateCatalogue.CallKinds && one.Units[0].CallDay == 5 && one.Units[0].CallCost > 0,
            "a call has a kind, a day and a quote");
        Check(!EstateRules.RaiseCall(one, 0, 7, 6, c) && !EstateRules.RaiseCall(Units(2), 0, 7, 6, c), "one call at a time at a unit, and none where nobody lives");
        Check(EstateRules.CallCost(Building(4000000), 1, 1000) > EstateRules.CallCost(Building(1000000), 1, 1000)
              && EstateRules.CallCost(Building(1000000), 1, 2000) == 2 * EstateRules.CallCost(Building(1000000), 1, 1000), "a quote follows the unit's value and prices");
        Check(EstateRules.CallCost(Building(1000), 5, 1000) == EstateCatalogue.CallLeast, "and is never under its least");
        int quote = one.Units[0].CallCost;
        Check(EstateRules.DiyCost(quote) < quote && EstateRules.DiyCost(quote) == EstateCatalogue.Share(quote, EstateCatalogue.DiyShare) && EstateRules.DiyCost(1) == 1,
            "doing it yourself costs the materials only");
        Check(EstateRules.SendContractor(one.Units[0], 5) && one.Units[0].CallAnswer == (int)CallAnswer.Contractor && one.Units[0].CallDue == 5 + EstateCatalogue.ContractorDays
              && !EstateRules.SendContractor(one.Units[0], 5), "a contractor is booked once for the next morning");
        one.Units[0].Happy = 600;
        DutyDay fixedDay = EstateRules.Duty(one, 0, 7, 6, c);
        Check(fixedDay.Fixed && one.Units[0].Call == 0, "and puts it right that morning");
        EstateHolding mine = DutyBuilding(800);
        EstateRules.RaiseCall(mine, 1, 7, 5, c);
        Check(EstateRules.DoItYourself(mine.Units[1], 5) && mine.Units[1].CallDue == 5 + EstateCatalogue.DiyDays && !EstateRules.DoItYourself(mine.Units[1], 5),
            "the player takes a job on for some days");
        DutyDay waits = EstateRules.Duty(mine, 1, 7, 5 + EstateCatalogue.DiyDays, c);
        DutyDay lapsed = EstateRules.Duty(mine, 1, 7, 6 + EstateCatalogue.DiyDays, c);
        Check(!waits.DiyLapsed && lapsed.DiyLapsed && mine.Units[1].CallAnswer == (int)CallAnswer.Waiting && mine.Units[1].Call > 0, "not done in them, it is open again");
        mine.Units[1].Happy = 600;
        Check(EstateRules.Fix(mine.Units[1], 20) == EstateCatalogue.HappyFixed && mine.Units[1].Happy == 600 + EstateCatalogue.HappyFixed && mine.Units[1].Call == 0, "a late repair pleases a little");
        EstateRules.RaiseCall(mine, 1, 7, 30, c);
        Check(EstateRules.Fix(mine.Units[1], 30 + EstateCatalogue.CallGraceDays) == EstateCatalogue.HappyFixedQuick, "a quick one pleases more");
        Check(EstateRules.Fix(mine.Units[1], 40) == 0 && EstateRules.Fix(null, 40) == 0, "nothing to put right is nothing");
        EstateHolding left = DutyBuilding(800);
        EstateRules.RaiseCall(left, 2, 7, 10, c);
        int before = left.Cond;
        DutyDay ten = default(DutyDay);
        for (int day = 11; day <= 10 + EstateCatalogue.CallLapseDays; day++)
        {
            ten = EstateRules.Duty(left, 2, 7, day, c);
        }
        Check(ten.Lapsed && ten.Complained && left.Cond == before - EstateCatalogue.CallLapseLoss, "a call left unanswered its days does damage, once, and the tenant complains");
        DutyDay next = EstateRules.Duty(left, 2, 7, 11 + EstateCatalogue.CallLapseDays, c);
        Check(!next.Lapsed && left.Cond == before - EstateCatalogue.CallLapseLoss, "the damage is done once, not every day after");
        EstateHolding busy = DutyBuilding(500);
        busy.Work = (int)EstateWork.Refurbish;
        int raised = 0;
        for (int day = 2; day < 300; day++)
        {
            raised += EstateRules.Duty(busy, 0, 7, day, c).NewCall ? 1 : 0;
        }
        Check(raised == 0, "no faults are reported while builders are in");
        EstateHolding city = DutyBuilding(800);
        EstateRules.RaiseCall(city, 0, 7, 10, c);
        city.Units[0].Moans = EstateCatalogue.ComplaintsToCity - 1;
        city.InspectDay = 100;
        city.Units[0].Happy = 0;
        bool reported = false;
        for (int day = 10 + EstateCatalogue.CityCallDays; day < 10 + EstateCatalogue.CallLapseDays + 1 && !reported; day++)
        {
            city.Units[0].Happy = 0;
            reported = EstateRules.Duty(city, 0, 7, day, c).Reported;
        }
        Check(reported && city.InspectDay < 100 && city.InspectDay <= 10 + EstateCatalogue.CallLapseDays + EstateCatalogue.CityDays,
            "complaints enough, with a repair waiting a week, bring the inspector early");
    }

    private static void EstateDutyInspections()
    {
        EstateClimate c = Climate();
        EstateHolding h = DutyBuilding(800);
        Check(EstateRules.Schedule(h, 7, 10) && h.InspectDay >= 10 + EstateCatalogue.InspectFirstLeast && h.InspectDay <= 10 + EstateCatalogue.InspectEvery && !EstateRules.Schedule(h, 7, 11),
            "a let building gets its first inspection, drawn, once");
        Check(!EstateRules.Schedule(Units(2), 7, 10) && !EstateRules.Schedule(Building(1000000, 1, 900, false), 7, 10), "an empty building, or one the game lets, gets none");
        Check(!EstateRules.Inspect(h, h.InspectDay - 1, c).Done, "nothing before its day");
        Inspection pass = EstateRules.Inspect(h, h.InspectDay, c);
        Check(pass.Done && pass.Passed && pass.Fine == 0 && h.InspectDay > 10 + EstateCatalogue.InspectFirstLeast && h.Strikes == 0, "a building in fair order passes and is seen again later");
        EstateHolding bad = DutyBuilding(EstateCatalogue.InspectPass + 30);
        EstateRules.RaiseCall(bad, 0, 7, 1, c);
        Check(EstateRules.InspectScore(bad, 1 + EstateCatalogue.InspectCallOld) == bad.Cond && EstateRules.InspectScore(bad, 2
            + EstateCatalogue.InspectCallOld) == bad.Cond - EstateCatalogue.InspectCallPenalty,
            "a repair counts against the building only once it is older than its days");
        bad.InspectDay = 20;
        Inspection fail = EstateRules.Inspect(bad, 20, c);
        Check(fail.Done && !fail.Passed && fail.Fine == EstateRules.InspectFine(1, 1000) && bad.OrderDay == 20 + EstateCatalogue.OrderDays
            && bad.InspectDay == bad.OrderDay && !EstateRules.Barred(bad),
            "an old repair can fail a building just over the mark: a fine and an order, letting not yet barred");
        Inspection again = EstateRules.Inspect(bad, bad.InspectDay, c);
        Check(!again.Passed && again.Fine == EstateRules.InspectFine(2, 1000) && again.Fine > fail.Fine && EstateRules.Barred(bad) && bad.InspectDay == bad.OrderDay
              && bad.OrderDay - (20 + EstateCatalogue.OrderDays) == EstateCatalogue.ReinspectDays, "failing again costs more, bars new tenants and brings the inspector sooner");
        EstateRules.Fix(bad.Units[0], 40);
        Inspection lifted = EstateRules.Inspect(bad, bad.InspectDay, c);
        Check(lifted.Passed && lifted.Lifted && !EstateRules.Barred(bad) && bad.OrderDay == 0 && bad.Strikes == 0, "put right, it passes and the bar is lifted");
        EstateHolding works = DutyBuilding(100);
        works.InspectDay = 30;
        works.Work = (int)EstateWork.Repair;
        works.WorkEnd = 33;
        Inspection waited = EstateRules.Inspect(works, 30, c);
        Check(waited.Postponed && !waited.Done && works.InspectDay == 34 && works.Strikes == 0, "with repair works under way the inspector comes after them, with no fine");
        Check(EstateRules.InspectFine(1, 2000) == 2 * EstateRules.InspectFine(1, 1000), "a fine follows prices");
        EstateHolding test = DutyBuilding(900);
        test.InspectDay = 99;
        Check(EstateRules.Inspect(test, 10, c, true).Done, "a test inspects whatever the day");
    }

    private static void EstateDutyArrears()
    {
        EstateClimate c = Climate();
        EstateHolding h = DutyBuilding(800, 50);
        EstateUnit u = h.Units[0];
        Check(!EstateRules.Remind(u) && !EstateRules.MayOfferPlan(u), "nothing to remind of, and no plan, while nothing is owed");
        u.Arrears = u.Rent * 4;
        u.ArrearsDays = 4;
        int before = EstateRules.PayUpChance(u, true);
        u.Happy = 600;
        Check(EstateRules.Remind(u) && u.Reminded && u.Happy == 600 - EstateCatalogue.HappyReminder && !EstateRules.Remind(u), "a reminder once in a spell, and the tenant a little put out");
        Check(EstateRules.PayUpChance(u, true) == before + EstateCatalogue.ReminderLift && EstateRules.PayUpChance(u, false) == before,
            "reminded, they are likelier to pay it all; not with the option off");
        Check(EstateRules.PlanDays(1) == EstateCatalogue.PlanDaysLeast && EstateRules.PlanDays(4) == 8 && EstateRules.PlanDays(40) == EstateCatalogue.PlanDaysMost,
            "a plan's days are twice the days owed, within bounds");
        Check(EstateRules.PlanAcceptChance(95) > EstateRules.PlanAcceptChance(20) && EstateRules.PlanAcceptChance(100) <= 1000, "a better record takes a plan more often");
        int taken = 0;
        for (int s = 1; s <= 400; s++)
        {
            EstateHolding p = DutyBuilding(800, 60);
            p.Units[0].Arrears = p.Units[0].Rent * 3;
            p.Units[0].ArrearsDays = 3;
            taken += EstateRules.OfferPlan(p, 0, s, 10) ? 1 : 0;
            Check(p.Units[0].PlanAsked && !EstateRules.MayOfferPlan(p.Units[0]), "a plan is offered once in a spell, taken or not");
        }
        Check(Math.Abs(taken / 400.0 - EstateRules.PlanAcceptChance(60) / 1000.0) < 0.07, "tenants take plans at their chance (" + taken + " of 400)");
        EstateHolding plan = null;
        for (int s = 1; s < 200 && plan == null; s++)
        {
            EstateHolding p = DutyBuilding(900, 95);
            p.Units[0].Arrears = p.Units[0].Rent * 3;
            p.Units[0].ArrearsDays = 3;
            plan = EstateRules.OfferPlan(p, 0, s, 10) ? p : null;
        }
        EstateUnit on = plan.Units[0];
        int owed = on.Arrears, days = EstateRules.PlanDays(3);
        Check(on.OnPlan && on.PlanEach == (owed + days - 1) / days && on.Paying && EstateRules.PayingRent(plan) == 4 * on.Rent, "on a plan the rent is paid again, the arrears in instalments");
        Check(!EstateRules.MayServeNotice(plan, 0, 10) && !EstateRules.Remind(on), "and no notice or reminder while it runs");
        long paid = 0;
        bool done = false;
        int day = 11;
        for (; day < 60 && on.OnPlan; day++)
        {
            TenancyDay d = EstateRules.Day(plan, 0, 3, day, c, true);
            paid += d.Lump;
            done |= d.PlanDone;
            Check(!d.Missed || d.PlanBroken, "a missed day on a plan ends it");
        }
        Check(done ? paid == owed && on.Arrears == 0 && !on.OnPlan && !on.PlanAsked : !on.OnPlan && on.Arrears > 0, "the plan pays the arrears off to the dollar, or a missed day leaves them owed");
        EstateHolding broke = null;
        TenancyDay broken = default(TenancyDay);
        for (int s = 1; s < 400 && broke == null; s++)
        {
            EstateHolding p = DutyBuilding(900, 20);
            p.Units[0].Arrears = p.Units[0].Rent * 2;
            p.Units[0].ArrearsDays = 2;
            p.Units[0].OnPlan = true;
            p.Units[0].PlanEach = 1;
            for (int d = 11; d < 40 && broke == null; d++)
            {
                TenancyDay t = EstateRules.Day(p, 0, s, d, c, true);
                if (t.PlanBroken)
                {
                    broke = p;
                    broken = t;
                }
            }
        }
        Check(broke != null && broken.Missed && !broke.Units[0].OnPlan && !broke.Units[0].Paying && broke.Units[0].ArrearsDays == 3, "a broken plan is arrears again, a day more of them");
        EstateHolding off = DutyBuilding(900, 95);
        off.Units[0].Arrears = 50;
        off.Units[0].ArrearsDays = 1;
        off.Units[0].OnPlan = true;
        off.Units[0].PlanEach = 10;
        TenancyDay legacy = EstateRules.Day(off, 0, 3, 20, c, false);
        Check(legacy.Lump == 0 || legacy.Lump == 50, "with the option off a plan is not worked: arrears are all or nothing as before");
    }

    private static void EstateDutyManagers()
    {
        EstateClimate c = Climate();
        EstateHolding h = Units(3, 1000000, 900);
        Check(EstateRules.ApplicantLift(h) == 1000, "letting it yourself brings applicants as before");
        h.Manager = (int)EstateManager.Agent;
        Check(EstateRules.ApplicantLift(h) == EstateCatalogue.ManagedApplicants, "an agent brings more");
        int plain = 0, agent = 0;
        for (int day = 1; day < 600; day++)
        {
            EstateHolding a = Units(1, 1000000, 900), b = Units(1, 1000000, 900);
            plain += EstateRules.Apply(a, 0, 11, day, c) != null ? 1 : 0;
            agent += EstateRules.Apply(b, 0, 11, day, c, false, EstateCatalogue.ManagedApplicants) != null ? 1 : 0;
        }
        Check(agent > plain && agent < plain * 14 / 10, "and over many days about a quarter more (" + agent + " against " + plain + ")");
        int market = EstateRules.UnitRent(h, c);
        h.Units[0].Applicants.Add(new RentApplicant { Id = 7, Kind = 1, Offer = market, Record = 30, Term = 90, Lapse = 9 });
        Check(EstateRules.ManagerPick(h, 0, 5, c) == 0, "an agent takes no tenant with a poor record");
        h.Units[0].Applicants.Add(new RentApplicant { Id = 8, Kind = 1, Offer = market * 90 / 100, Record = 60, Term = 90, Lapse = 9 });
        Check(EstateRules.ManagerPick(h, 0, 5, c) == 0 && EstateRules.ManagerPick(h, 0, 9, c) == 8, "an offer well under the asking rent waits for its last day");
        h.Units[0].Applicants.Add(new RentApplicant { Id = 9, Kind = 1, Offer = market, Record = 70, Term = 90, Lapse = 9 });
        Check(EstateRules.ManagerPick(h, 0, 5, c) == 9, "one at the asking rent with a fair record is taken at once");
        h.Units[0].ToLet = false;
        Check(EstateRules.ManagerPick(h, 0, 9, c) == 0, "nobody is taken for a unit not to let");
        Check(EstateRules.AgentFee(100) == 100 * EstateCatalogue.AgentLetDays && EstateRules.AgentFee(-5) == 0, "an agent's fee is days of the agreed rent");
        Check(EstateRules.ManagerFee(1000) == EstateCatalogue.Share(1000, EstateCatalogue.ManagerShare) && EstateRules.ManagerFee(0) == 0
            && EstateRules.ManagerFee(-10) == 0, "a manager's cut is a share of what comes in");
        EstateHolding m = DutyBuilding(800, 50);
        m.Manager = (int)EstateManager.Manager;
        EstateRules.RaiseCall(m, 0, 7, 10, c);
        Check(EstateRules.ManagerDay(m, 0, 10).Contractor, "a manager sends a contractor to a call");
        m.Units[1].Arrears = m.Units[1].Rent;
        m.Units[1].ArrearsDays = 1;
        ManagerMove first = EstateRules.ManagerDay(m, 1, 10);
        Check(first.Remind && !first.Plan && !first.Notice, "it reminds first");
        EstateRules.Remind(m.Units[1]);
        ManagerMove second = EstateRules.ManagerDay(m, 1, 11);
        Check(second.Plan && !second.Notice, "then offers a plan");
        m.Units[1].PlanAsked = true;
        m.Units[1].ArrearsDays = EstateCatalogue.NoticeArrearsDays;
        ManagerMove third = EstateRules.ManagerDay(m, 1, 12);
        Check(third.Notice && !third.Plan, "then serves notice once it may");
        EstateRules.ServeNotice(m, 1, 12, 0);
        Check(EstateRules.ManagerDay(m, 1, 13 + EstateCatalogue.NoticeDays[1]).Court, "and goes to the court when notice has run out");
        m.Manager = (int)EstateManager.Agent;
        ManagerMove agentDay = EstateRules.ManagerDay(m, 0, 10);
        Check(!agentDay.Contractor && !agentDay.Remind && !agentDay.Court, "a letting agent leaves repairs and arrears to the player");
        EstateHolding order = DutyBuilding(EstateCatalogue.InspectPass - 20);
        order.Manager = (int)EstateManager.Manager;
        order.OrderDay = 30;
        Check(!EstateRules.ManagerRepairs(order, 30 - EstateCatalogue.ManagerRepairDays - 1) && EstateRules.ManagerRepairs(order, 30 - EstateCatalogue.ManagerRepairDays),
            "a manager has the builders in a week before the city's order runs out");
        order.Cond = 1000;
        Check(!EstateRules.ManagerRepairs(order, 29), "but not for a building already well over the mark");
    }

    // With the option off, a tenancy runs exactly as before the duties: the same days, the same money, whatever the new fields say.
    private static void EstateDutyOff()
    {
        EstateClimate c = Climate(2);
        bool same = true;
        for (int s = 1; s <= 30; s++)
        {
            EstateHolding a = Let(500, 20 + s * 2, 1, 60), b = Let(500, 20 + s * 2, 1, 60);
            b.Units[0].Happy = 50;
            b.Units[0].QuitDay = 30;
            b.Units[0].Reminded = true;
            for (int day = 2; day < 200; day++)
            {
                TenancyDay x = EstateRules.Day(a, 0, s, day, c), y = EstateRules.Day(b, 0, s, day, c, false);
                same &= x.Lump == y.Lump && x.Missed == y.Missed && x.Left == y.Left && x.Renewed == y.Renewed && a.Units[0].Arrears == b.Units[0].Arrears && a.Units[0].Until == b.Units[0].Until;
                if (!a.Units[0].Let)
                {
                    break;
                }
            }
        }
        Check(same, "with the option off a lease's days are those of before: the new fields change nothing");
        EstateHolding h = DutyBuilding(800, 40);
        EstateRules.RaiseCall(h, 0, 7, 3, c);
        EstateRules.RaiseCall(h, 1, 7, 3, c);
        EstateRules.SendContractor(h.Units[1], 3);
        h.Units[1].Happy = 500;
        h.Units[2].Arrears = 100;
        h.Units[2].OnPlan = true;
        h.Units[2].PlanEach = 20;
        h.Units[3].QuitDay = 50;
        h.InspectDay = 40;
        h.OrderDay = 40;
        h.Strikes = 2;
        EstateRules.DutiesOff(h, 4);
        Check(h.Units[0].Call == 0 && h.Units[1].Call == 0 && h.Units[1].Happy > 500 && !h.Units[2].OnPlan && h.Units[2].Arrears == 100 && h.Units[3].QuitDay == 0
              && h.InspectDay == 0 && h.OrderDay == 0 && !EstateRules.Barred(h), "switched off, nothing is left pending: a contractor paid for finishes, plans end with their arrears owed");
    }

    private static void EstateDutySave()
    {
        EstateClimate c = Climate();
        var s = new EstateState { Seed = 5, FirstDay = 1, LastDay = 9 };
        EstateHolding h = DutyBuilding(700, 70);
        h.Place = "Duty%20Street~4";
        s.Holdings.Add(h);
        h.Manager = (int)EstateManager.Manager;
        h.InspectDay = 55;
        h.OrderDay = 44;
        h.Strikes = 1;
        h.CallsYear = 120;
        h.FeesYear = 33;
        h.FinesYear = 400;
        EstateRules.RaiseCall(h, 2, 5, 8, c);
        EstateRules.DoItYourself(h.Units[2], 8);
        h.Units[2].Moans = 2;
        h.Units[3].Arrears = 90;
        h.Units[3].ArrearsDays = 2;
        h.Units[3].Reminded = true;
        h.Units[3].PlanAsked = true;
        h.Units[3].OnPlan = true;
        h.Units[3].PlanEach = 30;
        h.Units[1].QuitDay = 70;
        int version;
        EstateState back = EstateStore.Read(EstateStore.Write(s), out version);
        EstateHolding b = back != null ? back.Holding(h.Place) : null;
        Check(version == EstateStore.Version && b != null && b.Manager == h.Manager && b.InspectDay == 55 && b.OrderDay == 44 && b.Strikes == 1 && b.CallsYear == 120
            && b.FeesYear == 33 && b.FinesYear == 400,
            "a building's duties come back from the save");
        bool units = b != null;
        for (int u = 0; b != null && u < 4; u++)
        {
            EstateUnit x = h.Units[u], y = b.Units[u];
            units &= x.Happy == y.Happy && x.Call == y.Call && x.CallDay == y.CallDay && x.CallCost == y.CallCost && x.CallAnswer == y.CallAnswer && x.CallDue == y.CallDue && x.Moans == y.Moans
                     && x.Reminded == y.Reminded && x.PlanAsked == y.PlanAsked && x.OnPlan == y.OnPlan && x.PlanEach == y.PlanEach && x.QuitDay == y.QuitDay;
        }
        Check(units, "and every unit's: satisfaction, its call, complaints, the arrears tools and a tenant's word to go");
        Check(EstateStore.Write(back) == EstateStore.Write(s), "a line read and written again is the same line");
        string old = EstateStore.Write(s);
        old = old.Substring(0, old.IndexOf("^l:", StringComparison.Ordinal)).Replace("v=" + EstateStore.Version, "v=3");
        EstateState three = EstateStore.Read(old, out version);
        EstateHolding t = three != null ? three.Holding(h.Place) : null;
        Check(version == 3 && t != null && t.Units.Count == 4 && t.Units[0].Let && t.Units[0].Happy == -1 && t.Units[2].Call == 0 && t.Manager == 0 && t.InspectDay == 0 && !t.Units[3].OnPlan,
            "a line from before the duties reads with none of them, every tenant to be judged afresh");
        var bare = new EstateState { Seed = 5 };
        bare.Holdings.Add(Building());
        string line = EstateStore.Write(bare);
        Check(line.EndsWith("^l:^m:", StringComparison.Ordinal), "a save with no duties to keep adds two empty parts and nothing else");
        EstateHolding drop = DutyBuilding();
        drop.Place = "Gone~1";
        var odd = new EstateState { Seed = 1 };
        odd.Holdings.Add(drop);
        string text = EstateStore.Write(odd).Replace("^l:Gone~1|0|", "^l:Gone~1|9|");
        Check(EstateStore.Read(text, out version) != null, "a record for a unit the building does not have is passed over");
    }

    private static void EstateDutyDeterminismAndTiming()
    {
        EstateClimate c = Climate();
        string first = null;
        bool same = true;
        for (int run = 0; run < 2; run++)
        {
            EstateHolding h = DutyBuilding(700, 60);
            var log = new System.Text.StringBuilder();
            for (int day = 2; day < 300; day++)
            {
                for (int u = 0; u < h.Units.Count; u++)
                {
                    TenancyDay t = EstateRules.Day(h, u, 77, day, c, true);
                    DutyDay d = EstateRules.Duty(h, u, 77, day, c);
                    log.Append(t.Lump).Append(t.Missed ? 'm' : '.').Append(d.NewCall ? 'c' : '.').Append(d.Complained ? 'k' : '.').Append(h.Units[u].Happy).Append(';');
                    if (h.Units[u].Call > 0 && day - h.Units[u].CallDay > 4)
                    {
                        EstateRules.SendContractor(h.Units[u], day);
                    }
                }
                EstateRules.Schedule(h, 77, day);
                Inspection r = EstateRules.Inspect(h, day, c);
                log.Append(r.Done ? (r.Passed ? 'P' : 'F') : '.');
            }
            string text = log.ToString();
            same &= first == null || first == text;
            first = first ?? text;
        }
        Check(same, "the same seed gives the same landlord's history");
        var all = new EstateHolding[EstateCatalogue.HoldingsMost];
        for (int i = 0; i < all.Length; i++)
        {
            all[i] = Units(EstateCatalogue.UnitsMost, 1000000 + i, 700);
            int rent = EstateRules.UnitRent(all[i], c);
            for (int u = 0; u < EstateCatalogue.UnitsMost; u++)
            {
                LetUnit(all[i], u, rent, 60, 1, 180);
            }
        }
        var watch = Stopwatch.StartNew();
        for (int day = 2; day < 62; day++)
        {
            for (int i = 0; i < all.Length; i++)
            {
                for (int u = 0; u < all[i].Units.Count; u++)
                {
                    EstateRules.Day(all[i], u, 5, day, c, true);
                    EstateRules.Duty(all[i], u, 5, day, c);
                }
                EstateRules.Schedule(all[i], 5, day);
                EstateRules.Inspect(all[i], day, c);
            }
        }
        double perDay = watch.Elapsed.TotalMilliseconds / 60.0;
        Check(perDay < 3.0, "a morning of the duties at " + EstateCatalogue.HoldingsMost + " buildings of " + EstateCatalogue.UnitsMost + " units takes under 3 ms ("
            + perDay.ToString("0.000") + " ms)");
    }
}
