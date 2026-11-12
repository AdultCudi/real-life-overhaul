using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The two new answers to a raise above a manager's limit (the Company app on the kit, the manager's question): a one-off bonus in place of the raise, and
// a raise of the manager's own limit that stands. A bonus closes the request and starts the existing wait; a limit is held to the scope above the store.
internal static partial class Program
{
    private static void CompanyAnswers()
    {
        int before = _run, failedBefore = _failed;
        BonusSums();
        BonusAnswer();
        BonusByManager();
        LimitSteps();
        AnswerSaves();
        Console.WriteLine("  the company's new answers (a bonus, a raise of the limit): " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // The bonus is a starting sum scaled by the wage band, at today's prices, and what a raise adds over a year is hours times days.
    private static void BonusSums()
    {
        int start = StaffRules.BonusFor(ManagerCatalogue.WageBandDefault, CompanyCatalogue.IndexStart);
        Check(start == 300, "bonus: at the starting band and the starting prices it is $300 (" + start + ")");
        Check(StaffRules.BonusFor(ManagerCatalogue.WageBandDefault, 1500) == 450 && StaffRules.BonusFor(ManagerCatalogue.WageBandDefault, 800) == 240,
            "bonus: it follows the city's prices, a half dearer and a fifth cheaper");
        int tight = StaffRules.BonusFor(0, CompanyCatalogue.IndexStart), wide = StaffRules.BonusFor(ManagerCatalogue.WageBands.Length - 1, CompanyCatalogue.IndexStart);
        Check(tight < start && start < wide, "bonus: a tighter wage band gives less and a wider one more (" + tight + ", " + start + ", " + wide + ")");
        bool ordered = true;
        for (int s = 1; s < ManagerCatalogue.WageBands.Length; s++)
        {
            ordered &= StaffRules.BonusFor(s, 1000) >= StaffRules.BonusFor(s - 1, 1000);
        }
        Check(ordered && StaffRules.BonusFor(-4, 1000) == tight && StaffRules.BonusFor(99, 1000) == wide, "bonus: it never falls as the band widens, and a band outside the steps is clamped");
        Check(StaffRules.YearOf(200, 800, 247) == 3952 && StaffRules.YearOf(0, 800, 247) == 0 && StaffRules.YearOf(-5, 800, 247) == 0 && StaffRules.YearOf(100, 800, 0) > 0,
            "bonus: a $2 raise over eight hours for 247 days is about $3,950 a year, and none is nothing");
    }

    // The answer itself: no raise, the request closed, the person content for a little, and the same wait as any answer.
    private static void BonusAnswer()
    {
        var f = new StaffFile { Id = "e1", Place = "p", Wage = 1700, Going = 1900, AskDay = 100, AskTo = 1900, UnderDays = 9, Standing = 50 };
        int paid = f.Paid;
        StaffRules.Answer(f, RaiseAnswer.Bonus, 101);
        Check(f.Raise == 0 && f.Paid == paid, "bonus answer: the pay is not raised");
        Check(f.AskDay == 0 && f.AskTo == 0 && f.UnderDays == 0, "bonus answer: the request is closed");
        Check(f.AskRest == 101 + CompanyCatalogue.RaiseRestDays, "bonus answer: the same wait as any answer before they ask again (" + f.AskRest + ")");
        Check(f.Standing == 50 + CompanyCatalogue.StandBonus && CompanyCatalogue.StandBonus > CompanyCatalogue.StandRefuse && CompanyCatalogue.StandBonus < CompanyCatalogue.StandAgree,
            "bonus answer: standing rises by less than a raise and more than a refusal does");
        Check(!StaffRules.CanGive(f, 101 + CompanyCatalogue.RaiseRestDays - 1) && StaffRules.CanGive(f, 101 + CompanyCatalogue.RaiseRestDays),
            "bonus answer: a raise cannot be given unasked before the wait has run, and can be after it");
        var g = new StaffFile { Id = "e2", Wage = 1700, Going = 1900, AskDay = 0, Standing = 50 };
        StaffRules.Answer(g, RaiseAnswer.Bonus, 5);
        Check(g.Standing == 50 && g.AskRest == 0, "bonus answer: nothing asked, nothing answered");
        Check(StaffRules.RaiseFor(f, RaiseAnswer.Bonus) == 0, "bonus answer: it adds no raise to the request");
        // A request is asked again by the rule's own days: never before the wait has run, and on the first day after it when still underpaid.
        f.UnderDays = 20;
        bool early = false;
        for (int day = 102; day < f.AskRest; day++)
        {
            StaffRules.Day(f, CompanyCatalogue.Standard, day, 0);
            early |= f.AskDay != 0;
            f.UnderDays = 20;
        }
        StaffRules.Day(f, CompanyCatalogue.Standard, f.AskRest, 0);
        Check(!early && f.AskDay == f.AskRest, "bonus answer: they ask again only after the wait, on its first day while still underpaid");
    }

    // A manager answers a refusal with the bonus only inside the month's budget, and never by mistake.
    private static void BonusByManager()
    {
        Check(ManagerRules.OrBonus(RaiseAnswer.Refuse, 300, 500, false) == RaiseAnswer.Bonus, "manager: a request they would refuse is a bonus when the budget left covers it");
        Check(ManagerRules.OrBonus(RaiseAnswer.Refuse, 300, 299, false) == RaiseAnswer.Refuse, "manager: not when the budget left is a dollar short");
        Check(ManagerRules.OrBonus(RaiseAnswer.Refuse, 300, 300, false) == RaiseAnswer.Bonus, "manager: exactly the budget left is enough");
        Check(ManagerRules.OrBonus(RaiseAnswer.Refuse, 300, 500, true) == RaiseAnswer.Refuse, "manager: a mistake never adds a bonus");
        Check(ManagerRules.OrBonus(RaiseAnswer.Agree, 300, 500, false) == RaiseAnswer.Agree && ManagerRules.OrBonus(RaiseAnswer.Less, 300, 500, false) == RaiseAnswer.Less,
            "manager: a raise, whole or half, stays what it was");
        Check(ManagerRules.OrBonus(RaiseAnswer.Refuse, 0, 500, false) == RaiseAnswer.Refuse, "manager: a bonus of nothing is no bonus");
        // Over many requests a budget bounds the bonuses: each costs its sum out of what is left.
        int left = 1000, given = 0;
        for (int i = 0; i < 20; i++)
        {
            if (ManagerRules.OrBonus(RaiseAnswer.Refuse, 300, left, false) == RaiseAnswer.Bonus)
            {
                left -= 300;
                given++;
            }
        }
        Check(given == 3 && left == 100, "manager: a $1,000 month pays three $300 bonuses and no more (" + given + ")");
    }

    // The limit a raise needs: the least steps that reach it, never lower than now, held to the scope above, and the same for the same figures.
    private static void LimitSteps()
    {
        int paid = 1700, going = 1800, askTo = 1900;   // pay $17, going rate $18, asks $19
        int bandNow = ManagerCatalogue.WageBandDefault, raiseNow = 6;   // 105% of the going rate, no largest raise
        int bandAbove = ManagerCatalogue.WageBands.Length - 1, raiseAbove = 6;
        Check(PolicyRules.RaiseCeiling(paid, going, bandNow, raiseNow) == 1890 && 1890 < askTo, "limit: the manager's limit now is $18.90, a cent short of the $19 asked");
        bool ok = PolicyRules.LimitFor(paid, going, askTo, bandNow, raiseNow, bandAbove, raiseAbove, out int band, out int most);
        Check(ok && band == 4 && ManagerRules.Ceiling(going, band) >= askTo && ManagerRules.Ceiling(going, band - 1) < askTo, "limit: the least band that reaches $19 is the next step (" + band + ")");
        Check(most == raiseNow && PolicyRules.RaiseCeiling(paid, going, band, most) >= askTo, "limit: no largest raise is set, so none is changed");
        // A manager already inside it changes nothing.
        ok = PolicyRules.LimitFor(paid, going, 1850, bandNow, raiseNow, bandAbove, raiseAbove, out band, out most);
        Check(ok && band == bandNow && most == raiseNow, "limit: a raise already inside the limit asks for nothing more");
        // A largest raise in force binds too: 3% of $17 is 51 cents, so $19 needs a wider share as well as the band.
        int narrow = 2;   // the third step of the largest raise
        ok = PolicyRules.LimitFor(paid, going, askTo, bandNow, narrow, bandAbove, raiseAbove, out band, out most);
        Check(ok && most > narrow && PolicyRules.RaiseCeiling(paid, going, band, most) >= askTo && PolicyRules.RaiseCeiling(paid, going, band, most - 1) < askTo,
            "limit: a largest raise that stops short is widened to the least step that reaches the pay asked (" + most + ")");
        // Held to the scope above: the company's band is the most the store can be set to.
        ok = PolicyRules.LimitFor(paid, going, askTo, bandNow, raiseNow, bandNow, raiseAbove, out band, out most);
        Check(!ok, "limit: when the company's own band stops short, the store cannot be set past it and the answer is not on offer");
        ok = PolicyRules.LimitFor(paid, going, askTo, bandNow, narrow, bandAbove, narrow, out band, out most);
        Check(!ok, "limit: nor when the company's largest raise stops short");
        // Never lower than now, whatever the pay asked.
        ok = PolicyRules.LimitFor(paid, going, 1000, 5, 6, bandAbove, raiseAbove, out band, out most);
        Check(ok && band == 5 && most == 6, "limit: it never lowers a limit already wider than needed");
        // Every step pair gives the same answer twice, and the written steps are in range.
        bool inRange = true;
        for (int b = 0; b < ManagerCatalogue.WageBands.Length; b++)
        {
            for (int m = 0; m < PolicyCatalogue.Def(PolicyField.RaiseMost).Steps.Length; m++)
            {
                bool a = PolicyRules.LimitFor(paid, going, askTo, b, m, bandAbove, raiseAbove, out int b1, out int m1);
                bool c = PolicyRules.LimitFor(paid, going, askTo, b, m, bandAbove, raiseAbove, out int b2, out int m2);
                inRange &= a == c && b1 == b2 && m1 == m2 && b1 >= b && m1 >= m && b1 < ManagerCatalogue.WageBands.Length && m1 < PolicyCatalogue.Def(PolicyField.RaiseMost).Steps.Length;
                inRange &= !a || PolicyRules.RaiseCeiling(paid, going, b1, m1) >= askTo;
            }
        }
        Check(inRange, "limit: for every limit in force the steps are in range, never lower, the same twice, and reach the pay asked whenever the answer is on offer");
    }

    // A bonus in a manager's log is kept, with its amount, and an old line without it loads as it always did.
    private static void AnswerSaves()
    {
        var s = new ManagerState { MonthFrom = 10 };
        s.Managers.Add(new ManagerFile { Place = "shop~1", Id = "m1", Name = "Ray Walsh" });
        Decision d = s.Add(20, 9, "shop~1", ManagerTask.Raises, "e1", "Ray Walsh");
        d.F = (int)RaiseAnswer.Bonus;
        d.G = 315;
        d.State = DecisionState.Closed;
        d.Detail = "Dev Kapoor";
        string line = ManagerStore.Write(s);
        ManagerState back = ManagerStore.Read(line, out int version);
        Check(back != null && back.Decisions.Count == 1 && back.Decisions[0].G == 315 && back.Decisions[0].F == (int)RaiseAnswer.Bonus && back.Decisions[0].State == DecisionState.Closed,
            "bonus save: the amount a bonus paid and its answer round trip");
        Check(ManagerStore.Write(back) == line, "bonus save: writing what was read gives the same line");
        // The same line as the version before wrote it, with no seventh figure: it loads, with none paid.
        int cut = line.LastIndexOf('|');
        string old = line.Substring(0, cut);
        ManagerState older = ManagerStore.Read(old, out version);
        Check(older != null && older.Decisions.Count == 1 && older.Decisions[0].G == 0 && older.Decisions[0].Detail == "Dev Kapoor" && older.Managers.Count == 1,
            "bonus save: a line from before the figure still loads, with nothing paid");
        var h = new HierarchyState();
        HierarchyEntry e = h.Log(HierarchyLog.Bonus, 4, "You", "shop~1", 20, 9);
        e.A = 315;
        e.Detail = "Dev Kapoor";
        HierarchyEntry e2 = h.Log(HierarchyLog.Limit, 4, "You", "shop~1", 20, 9);
        e2.A = 4;
        HierarchyState hb = HierarchyStore.Read(HierarchyStore.Write(h), out int hv);
        Check(hb != null && hb.Entries.Count == 2 && hb.Entries[0].Kind == (int)HierarchyLog.Bonus && hb.Entries[0].A == 315 && hb.Entries[1].Kind == (int)HierarchyLog.Limit,
            "bonus save: the hierarchy's log keeps the owner's bonus and limit answers");
    }
}
