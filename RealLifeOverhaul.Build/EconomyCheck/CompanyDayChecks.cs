using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Careers;
using PolicyField = RealLifeOverhaul.Economy.PolicyField;

// The day entries the director and the run share (CompanyDay), through the switches that gate them, the owner who is not there, a long jump of days and the market
// rent the property feature will feed. Each is run through the same world the 120-day run uses (CompanyRunWorld.cs).
internal static partial class Program
{
    private static void CompanyDayVariants()
    {
        int before = _run, failedBefore = _failed;
        DayManagersOff();
        DayHierarchyOff();
        DayOptionOff();
        DayLongJump();
        DayEntriesAlone();
        DayMarketRent();
        DayLeaseCard();
        LeaseZeroOffers();   // an offer of nothing is an offer (LeaseZeroChecks.cs)
        Console.WriteLine("  the day entries through their switches, a long jump of days and the market rent: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // The managers' switch off: nothing is decided, no manager's wait is booked, and the owner is asked at the landlord's offer, as the company's own notices ask them.
    private static void DayManagersOff()
    {
        CoWorld w = NewCoWorld(RunSeed);
        w.ManagersOn = false;
        for (int day = Epoch + 1; day <= Epoch + 100; day++)
        {
            CoDay(w, day);
        }
        Check(w.Answered == 0 && w.ManagerLeases == 0 && w.PassedUp == 0 && w.Speeds == 0 && w.M.Decisions.Count == 0, "day entries: with the managers off no manager decides anything and no wait is booked to them");
        Check(w.Offers >= 3 && w.LeaseNotices == w.Offers, "day entries: with the managers off the owner is told of every offer, once (" + w.LeaseNotices + " notices for " + w.Offers + " offers)");
    }

    // The hierarchy off: no escalation is ever raised, the owner is still told of an offer once, and the owner can answer it (the lease question is the lease's, not the
    // hierarchy's), so the lease does not renew at the landlord's figure only because the switch is off.
    private static void DayHierarchyOff()
    {
        CoWorld w = NewCoWorld(RunSeed);
        w.HierarchyOn = false;
        for (int day = Epoch + 1; day <= Epoch + 100; day++)
        {
            CoDay(w, day);
        }
        Check(w.H.Escalations.Count == 0 && w.Escalated == 0 && w.HandedDown == 0, "day entries: with the hierarchy off no question is raised or handed down");
        Check(w.LeaseNotices >= 2 && w.OwnerLeases >= 1 && w.PassedUp == 1, "day entries: with the hierarchy off the owner is told of the offer once and answers it (" + w.LeaseNotices + " notices, " + w.OwnerLeases + " answered by the owner, "
              + w.PassedUp + " passed up)");
        Check(w.Answered >= 1 && w.Bonuses >= 0, "day entries: with the hierarchy off a manager still answers inside their own band (" + w.Answered + " answered)");
    }

    // An option off: the staff part's switch stops a manager answering a request, and the request is left as it is.
    private static void DayOptionOff()
    {
        CoWorld w = NewCoWorld(RunSeed);
        var world = new CoDayWorld(w);
        StaffFile f = w.S.Staff[0];
        f.AskDay = Epoch + 1;
        f.AskTo = f.Going;
        ManagerFile m = w.M.Managers[0];
        w.PartOff[(int)CompanyPart.Staff] = true;
        bool off = CompanyDay.Raise(world, m.Place, m, f, 70, 0, m.Name, Epoch + 2, 9);
        Check(!off && f.AskDay == Epoch + 1 && w.M.Decisions.Count == 0 && m.Speeds.Count == 0, "day entries: with the staff option off a manager does not answer a request, writes no decision and books no wait");
        w.PartOff[(int)CompanyPart.Staff] = false;
        bool on = CompanyDay.Raise(world, m.Place, m, f, 70, 0, m.Name, Epoch + 2, 9);
        Check(on && f.AskDay == 0 && w.M.Decisions.Count == 1 && m.Decided == 1, "day entries: and with it on they answer it, write it down and count it for themselves");
        // An approver named: the request past the band is the approver's, not the manager's.
        w.H.Company.Steps[(int)PolicyField.Approver] = PolicyCatalogue.ApprovePeople;
        StaffFile g = w.S.Staff[3];
        g.AskDay = Epoch + 1;
        g.AskTo = g.Going;
        bool left = CompanyDay.Raise(world, m.Place, m, g, 70, 0, m.Name, Epoch + 2, 9);
        Check(!left && g.AskDay == Epoch + 1 && w.H.Escalations.Count == 1 && w.H.Escalations[0].Kind == (int)EscalationKind.Exception, "day entries: with an approver named a request past the band is left open and raised to them, not answered by the manager");
        // Nobody can decide: the manager off sick and nobody to stand in (the gate before every decision).
        int skill, review;
        string who;
        bool sick = false;
        for (int day = Epoch + 1; day < Epoch + 400 && !sick; day++)
        {
            w.Day = day;
            sick = !CompanyDay.Deciding(world, m.Place, ManagedKind.Shop, m, ManagerTask.Raises, out skill, out review, out who);
        }
        Check(sick && w.OffSick >= 1, "day entries: a manager off sick decides nothing that day (CompanyDay.Deciding)");
    }

    // Days skipped past a lease's whole notice (the game was left running, or a save loaded long after): the lease is offered and renewed at the landlord's figure on the
    // first day back, once, and the owner is not asked a question whose day has gone.
    private static void DayLongJump()
    {
        CoWorld w = NewCoWorld(RunSeed);
        var world = new CoDayWorld(w);
        LeaseFile l = w.L.Of("Elm~7");
        int end = l.End;
        w.Day = end - 40;
        for (int day = Epoch + 1; day < end - 30; day++)
        {
            CoDay(w, day);
        }
        Check(l.Offer == 0 && l.Renewals == 0, "day entries: before the notice nothing is offered");
        int day2 = end + 25;   // the days between are never lived
        w.Day = day2;
        CoLeases(w, world, day2);
        LeaseFile after = w.L.Of("Elm~7");
        Check(w.Skipped == 1 && after.Renewals == 1, "day entries: a lease whose whole notice was skipped is offered and renewed on the first day back, once (" + w.Skipped + " skipped, " + after.Renewals + " renewals)");
        Check(w.NoticeLog.FindAll(x => x.StartsWith("Elm~7", StringComparison.Ordinal)).Count == 0 && after.Offer == 0 && after.Told == 0 && after.End > day2, "day entries: the owner is not asked about a question whose day has gone, and the new term runs on past today");
        CoLeases(w, world, day2 + 1);
        Check(after.Renewals == 1, "day entries: and the next day it is not renewed again");
    }

    // The entries by themselves: nothing waits, one ask, a limit, the question that stays with the owner.
    private static void DayEntriesAlone()
    {
        CoWorld w = NewCoWorld(RunSeed);
        var world = new CoDayWorld(w);
        ManagerFile m = w.M.Managers[0];
        LeaseFile l = w.L.Of("Broadway~12");
        Check(CompanyDay.Lease(world, m.Place, m, l, 70, 0, m.Name, Epoch + 1, 9) == LeaseHour.None, "day entries: a lease with no offer made has nothing for the manager to do");
        l.Offer = 150;
        l.Asked = Epoch + 2;
        l.Start = Epoch + 1;
        l.Term = 300;
        w.Hour = 9;
        LeaseHour first = CompanyDay.Lease(world, m.Place, m, l, 70, 0, m.Name, Epoch + 2, 9), second = CompanyDay.Lease(world, m.Place, m, l, 70, 0, m.Name, Epoch + 3, 9);
        Check(first == LeaseHour.PassedUp && second == LeaseHour.Kept && l.Told == Epoch + 2 && m.Speeds.Count == 1 && w.LeaseNotices == 1 && w.H.Escalations.Count == 1,
            "day entries: an offer past the manager's limit is passed up once, then kept quiet: one notice, one question, one entry in their ring (" + first + " then " + second + ")");
        Check(!CompanyDay.Ask(world, m.Place, l, Epoch + 4) && w.LeaseNotices == 1, "day entries: asking the owner again about the same offer says nothing");
        // The landlord comes down (asked once): inside the limit now, the manager answers it.
        m.LeaseStep = 5;
        LeaseHour third = CompanyDay.Lease(world, m.Place, m, l, 70, 0, m.Name, Epoch + 5, 9);
        Check(third == LeaseHour.Accepted || third == LeaseHour.Declined, "day entries: an offer that is inside the limit again is the manager's to answer (" + third + ")");
        Check(CompanyDay.OwnerAnswers(world, m.Place, new LeaseFile { Place = "Nowhere~1" }, true, "You", Epoch + 6) == false, "day entries: the owner's answer to a lease with no offer does nothing");
        // The lease question has a number of its own when no escalation stands behind it, and the notice that opens it maps to that number.
        int elm = LeaseRules.QuestionId("Elm~7");
        string pick = LeaseRules.NoticePick("Elm~7");
        Check(elm < 0 && elm == LeaseRules.QuestionId("Elm~7") && elm != LeaseRules.QuestionId("Dock~4"), "lease question: a lease with no escalation behind it has a number of its own (negative, the same each time)");
        Check(LeaseRules.QuestionOfPick(pick) == elm && LeaseRules.QuestionOfPick(LeaseRules.NoticePick("Dock~4")) == LeaseRules.QuestionId("Dock~4") && LeaseRules.QuestionOfPick("he:12") == 0
              && LeaseRules.QuestionOfPick("ls:") == 0 && LeaseRules.QuestionOfPick(null) == 0 && LeaseRules.QuestionOfPick("mp:Elm~7") == 0,
            "lease question: the notice an offer sends carries a pick, and that pick pressed opens the lease's own question, hierarchy or none; no other pick does");
        var notices = new CoWorld { Seed = RunSeed };
        var noticeWorld = new CoDayWorld(notices);
        noticeWorld.LeaseNotice("Elm~7", new LeaseFile { Place = "Elm~7" }, Epoch + 5);
        Check(notices.NoticeLog.Count == 1 && notices.NoticeLog[0] == "Elm~7@" + (Epoch + 5) && LeaseRules.QuestionOfPick(LeaseRules.NoticePick(notices.NoticeLog[0].Substring(0, notices.NoticeLog[0].IndexOf('@')))) == elm,
            "lease question: and the place a notice names is the place the pick opens");
        // A manager hires a place up to twelve crew; past that the gap is the owner's.
        Check(CompanyDay.Wants(new ManagerFile { Keep = 20 }, 11) && !CompanyDay.Wants(new ManagerFile { Keep = 20 }, 12) && !CompanyDay.Wants(new ManagerFile { Keep = 6 }, 6) && CompanyDay.Wants(new ManagerFile { Keep = 6 }, 5),
            "day entries: a manager hires while the crew they are told to keep is not there and it is under " + ManagerCatalogue.HireCrewMost + ", and no further");
        string dir = FindCompanyDirectory(), game = dir != null ? SourceText(dir, "CompanyGame.cs") : string.Empty;
        Check(game.Contains("CellarTypes = { \"warehouse\" }") && LicenceRules.Needed(LicenceRules.Needs(new LicenceSite { Cellar = true }), (int)LicenceKind.BondedStorage),
            "licences: bonded storage is needed where goods are held in bulk, and the game's warehouse is where they are");
    }

    // Offer takes the market rent from the one function the property feature feeds: none known leaves the ask as it was; a market above the rent pulls the ask up by a
    // share of the way, one below pulls it down; and the director asks CompanyBridges.MarketRent, and nothing else, for it.
    // The lease question's figures are measured against the rent paid now (the escalation's own A, with the agreed rise and a hold-over premium in it), not the game's base rent: in a falling
    // market the landlord's offer is under what is paid, and the card must show a cut with its figure and a Rent now that differs from the Offered.
    private static void DayLeaseCard()
    {
        var held = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 0, Hold = true };
        LeaseRules.Make(held, 0, held.End - LeaseRules.NoticeDays);   // a market under the rent pulls the ask to nothing: the premium of the hold-over goes
        int paidNow = LeaseRules.RentAt(held.Rent, held.Running), offered = LeaseRules.RentAt(held.Rent, held.Offer);
        int more, less;
        LeaseRules.YearChange(paidNow, offered, 365, out more, out less);
        Check(paidNow != offered && less > 0 && more == 0, "lease card: a hold-over lease in a falling market shows rent now (" + paidNow + ") apart from the offered (" + offered + ") and a cut of " + less + " a year, not nought");
        int baseRent = held.Rent;
        LeaseRules.YearChange(baseRent, offered, 365, out more, out less);
        Check(less == 0 && baseRent == offered, "lease card: measured against the game's base rent the same cut reads as nought, which is what the card said before");
    }

    private static void DayMarketRent()
    {
        var l = new LeaseFile { Place = "Main~1", Start = 100, Term = 360, Rent = 200, Rise = 40 };
        int none = LeaseRules.Offer(l, RunSeed, 30, 0, 0), same = LeaseRules.Offer(l, RunSeed, 30, 0, 200), above = LeaseRules.Offer(l, RunSeed, 30, 0, 260), below = LeaseRules.Offer(l, RunSeed, 30, 0, 150);
        Check(none == LeaseRules.Offer(l, RunSeed, 30, 0, 0) && above > none && below < none && same <= above && same >= below, "market rent: unknown leaves the ask, a market above the rent raises it and one below lowers it (" + below + ", " + none + ", " + above + ")");
        int toward = none + (300 - none) * LeaseRules.MarketShare / 1000;
        Check(LeaseRules.Offer(l, RunSeed, 30, 0, 260) == CompanyCatalogue.Clamp(40 + (LeaseRules.Rise(RunSeed, "Main~1", 1, 30, 0)) + (300 - (40 + LeaseRules.Rise(RunSeed, "Main~1", 1, 30, 0))) * LeaseRules.MarketShare / 1000, 0, 4 * LeaseRules.RiseMost),
            "market rent: the ask moves " + LeaseRules.MarketShare + " per mille of the way to the market (a market of 260 over the rent of 200 is 300 per mille over it)");
        Check(toward >= 0, "market rent: the share of the way is a share");
        string dir = FindCompanyDirectory(), leases = dir != null ? SourceText(dir, "CompanyDirector.Leases.cs") : string.Empty, game = dir != null ? SourceText(dir, "CompanyGame.cs") : string.Empty;
        Check(leases.Contains("CompanyBridges.MarketRent(district, place, rent)") && SourceText(dir, "CompanyDirector.DayWorld.cs").Contains("MarketOf(district, place, rent)") && game.Contains("public static Func<string, string, int, int> MarketRent;") && game.Contains("MarketRent = null;"),
            "market rent: the director takes the market rent from CompanyBridges.MarketRent alone, which the property feature feeds and the bridges reset");
    }
}
