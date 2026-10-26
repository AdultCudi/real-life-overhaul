using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// The city around the construction company: the building code the elected government sets with its permits policy, as
// the construction game reads it; the one council's planning business (its sitting, its members' odds, the new district's
// voters, the player's zoning motions in the office's line); rigged tenders; unpaid bills; the building of the year; the
// construction game's line with the city's parts, and Justice's with the player's own claims; then, over the sources, the
// hooks, the bridges, the option and the tests; and the texts in every language.
internal static partial class Program
{
    private static void CivicBuild()
    {
        CivicBuildCode();
        CivicBuildCouncil();
        CivicBuildOffice();
        CivicBuildRings();
        CivicBuildBills();
        CivicBuildAward();
        CivicBuildStore();
        CivicBuildJustice();
        CivicBuildSources();
        CivicBuildTexts();
    }

    private static void CivicBuildCode()
    {
        Check(CivicCatalogue.HeightPermille(0) == 1000 && CivicCatalogue.ParkingPermille(0) == 1000 && CivicCatalogue.PlanningFeePermille(0) == 1000 && CivicCatalogue.VacantPermille(0) == 1000,
            "civic build: as the city came, the building code is the construction game's own");
        Check(CivicCatalogue.HeightPermille(1) > 1000 && CivicCatalogue.ParkingPermille(1) < 1000 && CivicCatalogue.PlanningFeePermille(1) < 1000 && CivicCatalogue.VacantPermille(1) > 1000,
            "civic build: fast-track permits build more: taller, fewer spaces, a cheaper fee, empty land dearer to keep");
        Check(CivicCatalogue.HeightPermille(-1) < 1000 && CivicCatalogue.ParkingPermille(-1) > 1000 && CivicCatalogue.PlanningFeePermille(-1) > 1000 && CivicCatalogue.VacantPermille(-1) < 1000,
            "civic build: strict permits build less: lower, more spaces, a dearer fee, empty land cheaper to keep");
        Check(CivicCatalogue.PercentOff(1334) == 33 && CivicCatalogue.PercentOff(667) == 33 && CivicCatalogue.PercentOff(600) == 40 && CivicCatalogue.PercentOff(1500) == 50
              && CivicCatalogue.PercentOff(2000) == 100 && CivicCatalogue.PercentOff(500) == 50, "civic build: the texts' per cents are the code's (a third, 40%, half, double)");
        Check(PlansRules.HeightLimit("x", true, 1) == BuildCatalogue.UptownHeightLimit && PlansRules.HeightLimit("x", true, 0) == BuildCatalogue.UptownHeightLimit
              && PlansRules.HeightLimit("x", true, -1) == CivicCatalogue.UptownStrictStoreys && CivicCatalogue.UptownMostStoreys == BuildCatalogue.UptownHeightLimit,
            "civic build: the new district never allows more than its buildings take (12), and strict permits 8");
        Check(PlansRules.HeightLimit("ba:neighborhood_midtown", false, 1) == 53 && PlansRules.HeightLimit("ba:neighborhood_midtown", false, 0) == 40
              && PlansRules.HeightLimit("ba:neighborhood_midtown", false, -1) == 26 && PlansRules.HeightLimit("ba:neighborhood_thehamptons", false, -1) >= 1
              && PlansRules.HeightLimit("ba:neighborhood_unknown", false, 0) == BuildCatalogue.DefaultHeightLimit,
            "civic build: a district's limit moves a third either way, never under a storey; one the table does not know has the default");
        Check(PlansRules.ParkingNeeded(BuildUse.Homes, 1200) == 10 && PlansRules.ParkingNeeded(BuildUse.Homes, 1200, 1) == 6 && PlansRules.ParkingNeeded(BuildUse.Homes, 1200, -1) == 15,
            "civic build: parking asked by the code in force");
        long fee = PlansRules.PlanningFee(1000000, 1000);
        Check(fee == 3500 && PlansRules.PlanningFee(1000000, 1000, 1) == 2100 && PlansRules.PlanningFee(1000000, 1000, -1) == 5250, "civic build: the planning fee by the code in force");
        Check(LandRules.VacantTax(0) == BuildCatalogue.VacantTaxPermille && LandRules.VacantTax(1) == 50 && LandRules.VacantTax(-1) == 12
              && LandRules.LandTax(1000000, 112, 112, true, 1) == 62000 && LandRules.LandTax(1000000, 112, 112, true) == 37000 && LandRules.LandTax(1000000, 112, 112, false, 1) == 12000,
            "civic build: the surcharge on land kept empty by the code in force, the tax on land in use as it was");
        CostSheet usual = PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 1200, 4, true, 2, 1000, null), fast = PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 1200, 4, true, 2, 1000, null, 1);
        Check(fast.Parking < usual.Parking && fast.Fees < usual.Fees && fast.Materials == usual.Materials, "civic build: the estimate's parking and fee follow the code, its works do not");
        Check(LandRules.ZonesWith(1, 4) == 5 && LandRules.ZonesWith(0, 255) == 15, "civic build: a lot's uses are its own and its class's, four uses at most");
    }

    private static void CivicBuildCouncil()
    {
        bool same = true;
        for (int day = 1; day < 120; day++)
        {
            same &= CouncilZoning.SittingDay(day) == LandRules.CouncilDay(day) && CouncilZoning.SittingDay(day) >= day + CouncilZoning.SittingDaysLeast
                    && ((CouncilZoning.SittingDay(day) - 1) % 7 + 7) % 7 == CouncilZoning.SittingWeekday;
        }
        Check(same, "civic build: the city's council sits for planning on the Thursday the construction game's own council did, three days on at least");
        float plain = CouncilZoning.MemberOdds(600, true, 0, 0.5f, 0.5f, 0, false, 50);
        Check(Math.Abs(plain - 0.6f) < 0.001f, "civic build: a member's odds with nothing leaning are the planners' view of the land (" + plain + ")");
        Check(CouncilZoning.MemberOdds(600, true, 1, 0.5f, 0.5f, 0, false, 50) > plain && CouncilZoning.MemberOdds(600, true, -1, 0.5f, 0.5f, 0, false, 50) < plain
              && CouncilZoning.MemberOdds(600, true, 0, 1f, 0f, 0, false, 50) > plain && CouncilZoning.MemberOdds(600, true, 0, 0.5f, 0.5f, 1, true, 50) > plain
              && CouncilZoning.MemberOdds(600, true, 0, 0.5f, 0.5f, 1, false, 50) == plain && CouncilZoning.MemberOdds(600, true, 0, 0.5f, 0.5f, 0, false, 90) > plain,
            "civic build: fast-track permits, a city wanting growth, the mayor's line (for their party) and the applicant's standing each make a yes likelier");
        Check(CouncilZoning.MemberOdds(600, false, 1, 0.5f, 0.5f, 0, false, 50) < CouncilZoning.MemberOdds(600, false, -1, 0.5f, 0.5f, 0, false, 50)
              && Math.Abs(CouncilZoning.MemberOdds(600, false, 0, 0.5f, 0.5f, 0, false, 90) - CouncilZoning.MemberOdds(600, false, 0, 0.5f, 0.5f, 0, false, 10)) < 0.001f,
            "civic build: to stop allowing a use the leans go the other way, and standing counts for nothing");
        Check(CouncilZoning.MemberOdds(5000, true, 1, 1f, 0f, 1, true, 100) == CouncilZoning.MostOdds && CouncilZoning.MemberOdds(-5, true, -1, 0f, 1f, -1, true, 0) == CouncilZoning.LeastOdds,
            "civic build: a member's odds are bounded");
        float r = CouncilZoning.Roll(77, 30, 2, CouncilZoning.Matter("uptown_1_01_01", 0, 2, true));
        Check(r >= 0f && r < 1f && r == CouncilZoning.Roll(77, 30, 2, CouncilZoning.Matter("uptown_1_01_01", 0, 2, true))
              && CouncilZoning.Matter("uptown_1_01_01", 0, 2, true) != CouncilZoning.Matter("uptown_1_01_01", 0, 2, false)
              && CouncilZoning.Matter(null, 1, 2, true) != CouncilZoning.Matter(null, 2, 2, true), "civic build: a vote's draws are the same for the same save, day, seat and matter");
        var mayor = new Candidate { Planks = 1 };
        mayor.Platform[0] = CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Permits, -1));
        var none = new Candidate();
        Check(CouncilZoning.Line(mayor, 1) == -1 && CouncilZoning.Line(none, 1) == 1 && CouncilZoning.Line(none, 0) == 0, "civic build: the mayor's line is their promise, else the policy in force");
        Check(CouncilZoning.BuildingLean(true, (int)Issue.Growth, 0.8f) == CouncilZoning.NewDistrictGrowth && CouncilZoning.BuildingLean(true, (int)Issue.Growth, 1.3f) == 1.3f
              && CouncilZoning.BuildingLean(true, (int)Issue.Living, 1.2f) == CouncilZoning.NewDistrictLiving && CouncilZoning.BuildingLean(true, (int)Issue.Safety, 0.9f) == 0.9f
              && CouncilZoning.BuildingLean(false, (int)Issue.Growth, 0.8f) == 0.8f, "civic build: the new district's voters lean to growth and less to quiet; another district keeps its own lean");
        Check(CivicCatalogue.Find(Lever.Permits, 1).Issue == Issue.Growth && CivicCatalogue.Find(Lever.Permits, -1).Issue == Issue.Living,
            "civic build: the building code's platforms speak to growth or to living");
        Check(OfficeRules.Conflicted(Lever.Permits, 0, false, true) && !OfficeRules.Conflicted(Lever.Permits, 3, true, false) && OfficeRules.Conflicted(Lever.Rent, 0, true, false)
              && !OfficeRules.Conflicted(Lever.Cleaning, 3, true, true), "civic build: the building code is an interest of a builder's alone; the other levers as they were");
    }

    private static void CivicBuildOffice()
    {
        var s = new OfficeState { Holds = OfficeRace.Council, Seat = 4, NextMeeting = 40 };
        s.Agenda.Add(new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = 20, By = OfficeMotion.ByPlayer, Day = 38 });
        s.Agenda.Add(new OfficeMotion { Kind = OfficeMotion.KindZoning, By = OfficeMotion.ByApplicant, Day = 37, Lot = "uptown_1_02_03", Class = 1, Use = 0, Add = true, Suit = 450, Reputation = 61 });
        s.Agenda.Add(new OfficeMotion { Kind = OfficeMotion.KindZoning, By = OfficeMotion.ByPlayer, Day = 38, Class = 3, Use = 1, Add = false, Suit = 600, Undeclared = true });
        string line = s.Write();
        var back = new OfficeState();
        Check(back.Read(line) && back.Write() == line && back.Agenda.Count == 3 && back.ZoningCount() == 2, "civic build: the office's line keeps the zoning matters on the agenda");
        OfficeMotion app = null, rezone = null;
        foreach (OfficeMotion m in back.Agenda)
        {
            if (m.Zoning && m.Lot != null)
            {
                app = m;
            }
            else if (m.Zoning)
            {
                rezone = m;
            }
        }
        Check(app != null && app.Lot == "uptown_1_02_03" && app.Class == 1 && app.Suit == 450 && app.Reputation == 61 && app.By == OfficeMotion.ByApplicant && rezone != null && !rezone.Add
              && rezone.Undeclared && rezone.Class == 3 && rezone.Use == 1, "civic build: a zoning matter comes back with every field");
        // A line of before the zoning matters: read whole, nothing of them; and an older reader stops before them.
        int tail = line.LastIndexOf("|2|", StringComparison.Ordinal);
        var older = new OfficeState();
        string head = line.Substring(0, line.IndexOf("|uptown_1_02_03", StringComparison.Ordinal));
        head = head.Substring(0, head.LastIndexOf("|2|", StringComparison.Ordinal) + 1);
        Check(tail > 0 && older.Read(head) && older.Agenda.Count == 1 && older.ZoningCount() == 0, "civic build: an office line without zoning matters reads as before");
        Check(Regex.Matches(line, @"\|0\|20\|1\|38\|").Count == 1, "civic build: a policy motion is written where an older reader looks for it, the zoning matters after everything");
    }

    private static void CivicBuildRings()
    {
        int city = 0, other = 0;
        for (int t = 1; t <= 20000; t++)
        {
            city += RingRules.Forms(4242, t, TenderSource.CityWorks) ? 1 : 0;
            other += RingRules.Forms(4242, t, TenderSource.Client) ? 1 : 0;
        }
        Check(Math.Abs(city - 20 * RingRules.CityWorksPermille) < 400 && Math.Abs(other - 20 * RingRules.OtherPermille) < 450, "civic build: rings form on about "
              + RingRules.CityWorksPermille / 10 + "% of the city's tenders (" + city / 200 + "%) and " + RingRules.OtherPermille / 10 + "% of others (" + other / 200 + "%)");
        bool distinct = true;
        for (int t = 1; t < 500; t++)
        {
            int members = RingRules.Ring(99, t, out int leader);
            distinct &= RingRules.Count(members) == RingRules.Members && RingRules.Member(members, leader) && members == RingRules.Ring(99, t, out int again) && again == leader;
        }
        Check(distinct, "civic build: a ring is three different builders with its leader among them, the same however often asked");
        var own = new TenderBid { Bidder = 2, Price = 1000000, Days = 20 };
        TenderBid led = RingRules.Rigged(own, 5, 9, 1000000, 20, true, false), cover = RingRules.Rigged(own, 5, 9, 1000000, 20, false, false);
        Check(led.Price == 1100000 && led.Days == 20 && cover.Price >= RingRules.CoverLeastPermille * 1000L && cover.Price <= RingRules.CoverMostPermille * 1000L && led.Score < cover.Score,
            "civic build: the leader marks its own price up, a cover bids high; the client's score still prefers the leader");
        Check(RingRules.PlayerCoverPermille >= BuildCatalogue.BidLeastPermille && RingRules.PlayerCoverPermille <= BuildCatalogue.BidMostPermille
              && (RingRules.PlayerCoverPermille - BuildCatalogue.BidLeastPermille) % BuildCatalogue.BidStepPermille == 0, "civic build: the cover the player is asked for is one of the app's bids");
        Check(RingRules.Cut(1000000) == 25000 && RingRules.Cut(-5) == 0, "civic build: the cut is a share of the winning price");
        bool window = !RingRules.Found(1, 1, 0, 50) && !RingRules.Found(1, 1, 10, 10) && !RingRules.Found(1, 1, 10, 10 + RingRules.FoundDays + 1);
        int found = 0;
        for (int day = 11; day <= 10 + RingRules.FoundDays; day++)
        {
            found += RingRules.Found(1, 1, 10, day) == RingRules.Found(1, 1, 10, day) && RingRules.Found(1, 1, 10, day) ? 1 : 0;
        }
        Check(window && found <= RingRules.FoundDays, "civic build: the inquiry can find a ring out only in its weeks after the award, the same draw each time");
        Check(!RingRules.Approach(3, 3, false, 90) && !RingRules.Approach(3, 3, true, RingRules.ApproachLeastReputation - 1), "civic build: an unlicensed company, or one of no standing, is never asked to cover");
    }

    private static void CivicBuildBills()
    {
        int never = 0, late = 0, cityNever = 0;
        for (int p = 1; p <= 10000; p++)
        {
            int pays = BillRules.ClientPays(77, p, 100, false);
            never += pays == BillRules.Never ? 1 : 0;
            late += pays > 100 ? 1 : 0;
            cityNever += BillRules.ClientPays(77, p, 100, true) == BillRules.Never ? 1 : 0;
            if (pays != BillRules.Never && pays > 100 && (pays < 100 + BillRules.LateLeast || pays > 100 + BillRules.LateMost))
            {
                late = -100000;
            }
        }
        Check(cityNever == 0 && Math.Abs(never - BillRules.ClientNeverPermille * 10) < 300 && Math.Abs(late - BillRules.ClientLatePermille * 10) < 400,
            "civic build: clients pay on time, late (" + late / 100 + "%) or never (" + never / 100 + "%); the city pays late at worst");
        int lateBuyers = BillRules.BuyersLate(5, 8, 12, out int neverBuyers);
        Check(lateBuyers >= 0 && lateBuyers <= 12 && neverBuyers <= lateBuyers && BillRules.BuyersLate(5, 8, 0, out int none) == 0 && none == 0
              && BillRules.BuyersPay(5, 8, 50, 0, 0) == 50 && BillRules.BuyersPay(5, 8, 50, 2, 2) == BillRules.Never && BillRules.BuyersPay(5, 8, 50, 3, 1) > 50,
            "civic build: buyers who pay late are some of those who bought, and pay on a later day unless none of them will");
        Check(BillRules.AfterChase(1, 2, 1, 60, 70) <= 70 && BillRules.AfterChase(1, 2, 1, 60, 61) == 61 && BillRules.AfterChase(1, 2, 1, 60, BillRules.Never) == BillRules.AfterChase(1, 2, 1, 60, BillRules.Never),
            "civic build: a letter never makes a debtor pay later, and works the same each time");
        Check(!BillRules.MayChase(0, 0, 50, 49) && BillRules.MayChase(0, 0, 50, 50) && !BillRules.MayChase(1, 50, 50, 51) && BillRules.MayChase(1, 50, 50, 53)
              && !BillRules.MayChase(BillRules.ChaseMost, 0, 50, 90), "civic build: letters only once the bill is due, a few days apart, and so many");
        Check(!BillRules.MaySue(0, 50, 55) && BillRules.MaySue(1, 50, 51) && BillRules.MaySue(0, 50, 50 + BillRules.SueAfterDays) && !BillRules.MaySue(1, 50, 49),
            "civic build: to court after a letter, or a week overdue");
        Check(BillRules.Strength(0, false) == BillRules.StrongPermille && BillRules.Strength(2, false) > BillRules.Strength(0, false) && BillRules.Strength(9, false) <= 950
              && BillRules.Strength(0, true) == BillRules.CityStrongPermille, "civic build: a claim's strength by its letters, the city's debt the strongest");
        Check(!BillRules.CreditorSues(BillKind.Wages, 10, 14) && BillRules.CreditorSues(BillKind.Wages, 10, 15) && !BillRules.CreditorSues(BillKind.Merchant, 10, 16)
              && BillRules.CreditorSues(BillKind.Merchant, 10, 17) && !BillRules.CreditorSues(BillKind.Client, 0, 999), "civic build: a crew and a merchant go to court after their days");
        Check(BillRules.Account(30, false) && !BillRules.Account(29, false) && !BillRules.Account(90, true), "civic build: an account for a company of standing with nothing overdue");
    }

    private static void CivicBuildAward()
    {
        Check(AwardRules.Year(1, 112) == 0 && AwardRules.Year(112, 112) == 0 && AwardRules.Year(113, 112) == 1 && AwardRules.ShortlistDay(0, 112) == 106 && AwardRules.AwardDay(0, 112) == 113
              && AwardRules.VoteDays(28) == AwardRules.VoteLeastDays && AwardRules.VoteDays(400) == AwardRules.VoteMostDays && AwardRules.ShortlistDay(0, 28) == 26,
            "civic build: the shortlist in the year's last days, the award on the next year's first, for a long year and a short one");
        bool bounded = true;
        for (int q = -20; q <= 140; q += 7)
        {
            int score = AwardRules.Score(q, q % 3 == 0 ? 0 : q / 10, 9, q);
            bounded &= score >= 5 && score <= 98 && score == AwardRules.Score(q, q % 3 == 0 ? 0 : q / 10, 9, q) && AwardRules.Stars(score) >= 1 && AwardRules.Stars(score) <= AwardRules.StarsMost;
        }
        Check(bounded && AwardRules.Stars(5) == 1 && AwardRules.Stars(98) == 5, "civic build: the critic's score and stars are bounded and the same each time");
        Check(AwardRules.Score(80, 0, 1, 1) > AwardRules.Score(80, 20, 1, 1), "civic build: a late building scores less");
        Check(AwardRules.DayVotes(1, 2, 3, 4, 70, 50000, true) > AwardRules.DayVotes(1, 2, 3, 4, 70, 50000, false) && AwardRules.DayVotes(1, 2, 3, 4, 70, 0, true) == AwardRules.DayVotes(1, 2, 3, 4, 70, 0, false)
              && AwardRules.DayVotes(1, 2, 3, 4, 70, 9999999, true) - AwardRules.DayVotes(1, 2, 3, 4, 70, 0, true) == AwardRules.FollowerVotesMost,
            "civic build: the player's following votes for their own buildings, to a limit");
        var entries = new List<AwardEntry>
        {
            new AwardEntry { Builder = 1, Score = 60, Day = 5 }, new AwardEntry { Builder = -1, Score = 70, Day = 9 }, new AwardEntry { Builder = 2, Score = 70, Day = 3 }, new AwardEntry { Builder = 3, Score = 40, Day = 1 }
        };
        var order = new List<int>();
        AwardRules.Rank(entries, order);
        Check(order.Count == 4 && order[0] == 2 && order[1] == 1 && order[2] == 0 && order[3] == 3, "civic build: the shortlist's order: the best score, then the earlier finished");
        entries[0].Short = entries[1].Short = true;
        entries[0].Votes = 300;
        entries[1].Votes = 300;
        Check(AwardRules.Winner(entries) == 1 && AwardRules.Winner(new List<AwardEntry>()) == -1, "civic build: the most votes wins, then the better score");
        var full = new List<AwardEntry>();
        for (int i = 0; i < AwardRules.EntriesMost; i++)
        {
            full.Add(new AwardEntry { Builder = i == 0 ? -1 : 1, Score = i == 0 ? 6 : 50 + i });
        }
        Check(AwardRules.Room(full, 40, false) == -1 && AwardRules.Room(full, 99, false) == 1 && AwardRules.Room(full, 1, true) == 1, "civic build: a full year keeps the player's own and the best");
    }

    private static void CivicBuildStore()
    {
        var s = new BuildState { Seed = 3, FirstDay = 1, LastDay = 9, NextId = 20 };
        s.ClassAdded[1] = 1;
        s.ClassAdded[3] = 6;
        s.Rings.Add(new RingFile { Tender = 7, Leader = 2, Members = 1 << 2 | 1 << 5 | 1 << 6, Asked = true, Answer = RingFile.Joined, Awarded = 8, LeaderWon = true, Cut = 25000, Found = 9, Case = 31 });
        s.Bills.Add(new BillFile { Id = 4, Kind = BillKind.Buyers, Project = 3, Who = 2, Amount = 88000, Raised = 7, Due = 7, PaysOn = BillRules.Never, Chased = 1, ChasedDay = 8, City = false });
        s.Bills.Add(new BillFile { Id = 5, Kind = BillKind.Wages, Who = 1, Crew = 9, Amount = 1260, Raised = 6, Due = 6, Closed = 9, Outcome = BillFile.Sued, Case = 32 });
        s.NextBill = 6;
        s.Award.Year = 0;
        s.Award.Voting = true;
        s.Award.LastYear = -1;
        s.Award.Entries.Add(new AwardEntry { Kind = AwardEntry.OfPlayer, Project = 3, Lot = "uptown_1_01_02", Use = BuildUse.Homes, Builder = -1, Day = 8, Score = 77, Votes = 120, Short = true });
        s.Award.Entries.Add(new AwardEntry { Kind = AwardEntry.OfCity, Street = "ba:street_x|y", Number = 4, Use = BuildUse.Offices, Builder = 6, Day = 5, Score = 64, Votes = 90, Short = true });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        Check(back != null && BuildStore.Write(back) == line, "civic build: the construction game's line with the city's parts reads back to the same line");
        if (back != null)
        {
            Check(back.ClassAdded[3] == 6 && back.Ring(7) != null && back.Ring(7).Cut == 25000 && back.Ring(7).Answer == RingFile.Joined && back.Bill(4) != null && back.Bill(4).PaysOn == BillRules.Never
                  && back.Bill(5).Outcome == BillFile.Sued && back.NextBill == 6 && back.Award.Voting && back.Award.Entries.Count == 2 && back.Award.Entries[1].Street == "ba:street_x|y"
                  && back.ZonesOf(new LotFile { Zones = 8 }, 3) == 14, "civic build: the zoning, rings, bills and the year's buildings come back whole");
        }
        BuildState before = BuildStore.Read(line.Substring(0, line.IndexOf("^z:", StringComparison.Ordinal)), out _);
        Check(before != null && before.Rings.Count == 0 && before.Bills.Count == 0 && before.Award.Year == -1 && before.NextBill == 1 && before.ClassAdded[3] == 0,
            "civic build: a line of before the city's parts reads as a company the city has never ruled on");
        BuildState newer = BuildStore.Read(line + "^q2:later", out _);
        Check(newer != null && newer.Rings.Count == 1, "civic build: a newer version's part is passed over");
        var tidy = new BuildState();
        for (int i = 0; i < BuildState.BillsKept * 3; i++)
        {
            tidy.Bills.Add(new BillFile { Id = i + 1, Amount = 1, Closed = i % 2 == 0 ? 3 : 0 });
        }
        tidy.Tidy();
        int live = 0;
        foreach (BillFile b in tidy.Bills)
        {
            live += b.Live ? 1 : 0;
        }
        Check(tidy.Bills.Count <= BuildState.BillsKept * 2 && live > 0, "civic build: the bills kept are capped, settled ones going first");
    }

    private static void CivicBuildJustice()
    {
        ChargeDef rigging = JusticeLaw.Find(JusticeLaw.BidRigging);
        Check(rigging != null && rigging.NameKey == "reallife_justice_charge_bid_rigging" && rigging.Fine > 0, "civic build: bid rigging is a charge of its own");
        Check(JusticeLaw.ClaimFee(0) == JusticeLaw.ClaimFeeLeast && JusticeLaw.ClaimFee(1000000) == 30000 && JusticeLaw.ClaimChance(780, 0.5f) == 780 && JusticeLaw.ClaimChance(5000, 1f) == 950
              && JusticeLaw.ClaimChance(0, 0f) == 50 && JusticeLaw.ClaimChance(780, 1f) > JusticeLaw.ClaimChance(780, 0f), "civic build: a claim's fee and its odds, counsel helping a little");
        JusticeState.Clear();
        JusticeState.Claims.Add(new JusticeClaim { Number = 5, Amount = 2000, Fee = 150, Strength = 840, FiledDay = 10, HearDay = 17, Seed = -12345, Result = -1, Defendant = "a property firm, Ltd." });
        JusticeState.Claims.Add(new JusticeClaim { Number = 6, Amount = 900, Fee = 150, Strength = 780, FiledDay = 2, HearDay = 9, Seed = 7, Result = 1, DecidedDay = 9, Defendant = "the city" });
        string line = JusticeState.Write();
        JusticeState.Clear();
        JusticeState.Parse(line);
        Check(JusticeState.Claims.Count == 2 && JusticeState.Claim(5) != null && JusticeState.Claim(5).Result == -1 && JusticeState.Claim(5).Seed == -12345
              && JusticeState.Claim(5).Defendant == "a property firm, Ltd." && JusticeState.Claim(6).Result == 1 && JusticeState.ClaimsOpen == 1 && JusticeState.Write() == line,
            "civic build: the player's own claims come back in Justice's line, waiting ones and decided ones");
        JusticeState.Clear();
        JusticeState.Parse(line.Substring(0, line.IndexOf("|O:", StringComparison.Ordinal)));
        Check(JusticeState.Claims.Count == 0, "civic build: Justice's line of before reads with no claims of the player's");
        JusticeState.Clear();
    }

    private static void CivicBuildSources()
    {
        string[] rules = { "RingRules.cs", "BillRules.cs", "AwardRules.cs", "BuildState.City.cs", "BuildStore.City.cs" };
        string[] director = { "ConstructionDirector.City.cs", "ConstructionDirector.CityAsks.cs", "ConstructionDirector.Rings.cs", "ConstructionDirector.Bills.cs", "ConstructionDirector.Award.cs",
            "ConstructionDirector.CityTests.cs", "ConstructionApp.City.cs" };
        var code = new Dictionary<string, string>();
        foreach (string name in rules)
        {
            code[name] = ModSource(@"Construction\" + name);
            Check(code[name].Length > 0 && !Regex.IsMatch(code[name], @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode|using System\.Linq"),
                "civic build sources: " + name + " is game-free and draws only from its seed");
        }
        foreach (string name in director)
        {
            code[name] = ModSource(@"Construction\" + name);
            Check(code[name].Length > 0 && !Regex.IsMatch(code[name], @"\b(CivicDirector|CivicWorks|SiteState|BuildingCopies|TradesBridges|DistrictBuildings|UptownLots|MarketDirector|FameDirector|FlareDirector|JusticeState|OwnClaims|TaxCases)\b"),
                "civic build sources: " + name + " meets the rest of the mod through the bridges");
            Check(!Regex.IsMatch(code[name], @"void (Update|LateUpdate|FixedUpdate)\(|using System\.Linq|FindObjects|GameObject\.Find\b"), "civic build sources: " + name + " costs nothing a frame");
        }
        foreach (string name in new[] { @"Civic\CouncilZoning.cs" })
        {
            Check(!Regex.IsMatch(ModSource(name), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|GetHashCode"), "civic build sources: " + name + " is game-free");
        }
        // The hooks into the construction game's own passes, one line each.
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs"), day = ModSource(@"Construction\ConstructionDirector.Day.cs");
        string projects = ModSource(@"Construction\ConstructionDirector.Projects.cs"), sites = ModSource(@"Construction\ConstructionDirector.Sites.cs");
        Check(market.Contains("RingFor(t);") && market.Contains("count = RingBids(t, days, need, count);") && market.Contains("RingAwarded(t, won, day);") && market.Contains("NoteFinished(dev.Public"),
            "civic build sources: a tender's ring is drawn as it opens and played at its award; a developer's building is one of the year's");
        Check(day.Contains("CityDay(day);") && day.Contains("LateClient(p, pay, ConstructionGame.Day)") && day.Contains("OweWages(c, day);") && day.Contains("PayArrears(c, day);")
              && day.Contains("HearZoning(l, false);") && day.Contains("LandRules.LandTax(l.Paid, day - l.TaxedTo, year, empty, Permits())") && !day.Contains("private void ZoneVote("),
            "civic build sources: the day's pass: the city's day, a client's bill, a crew's wages, the council's zoning and the code's land tax");
        Check(projects.Contains("LateBuyers(p, day);") && projects.Contains("NoteFinished(AwardEntry.OfPlayer") && projects.Contains("int limit = HeightLimitOf(p);")
              && projects.Contains("PlansRules.ParkingNeeded(p.Use, p.Area, code)") && projects.Contains("PlansRules.PlanningFee(Math.Max(0L, p.Estimate), ConstructionGame.Prices, Permits())"),
            "civic build sources: the plans and the decision under the code in force; late buyers and the year's buildings at the handover");
        Check(sites.Contains("OnAccount(p, m0, cost, ConstructionGame.Day)") && ModSource(@"Construction\ConstructionDirector.Actions.cs").Contains("l.ZoneDay = CouncilDay(ConstructionGame.Day);")
              && ModSource(@"Construction\ConstructionDirector.Tests.cs").Contains("int withdrawn = CityBack(back);"),
            "civic build sources: an order on account, the council's sitting for zoning, and the put back taking back the tests' cases");
        // Civic's one council: the office's hooks and the government's planning sitting.
        string office = ModSource(@"Civic\CivicOffice.cs");
        Check(office.Contains("government.ToPlanning = PutZoning;") && office.Contains("government.OfficeMeeting = () => State.NextMeeting;") && office.Contains("government.DecideNow = DecideZoningNow;")
              && ModSource(@"Civic\CivicGovernment.Planning.cs").Contains("return CouncilZoning.SittingDay(day);") && ModSource(@"Civic\CivicGovernment.Council.cs").Contains("return BuildingLean(district, issue,"),
            "civic build sources: zoning goes to the player's meetings while they sit and the council's own sitting otherwise; the new district's lean in every count");
        Check(ModSource(@"Civic\CivicOffice.Office.cs").Contains("OfficeRules.Conflicted(p.Lever, Businesses(-1), CivicBridges.Ask(CivicBridges.Landlord), ZoningBridges.HasInterest())")
              && ModSource(@"Civic\OfficeScenes.cs").Contains("bool conflicted = _office.Conflicted(m);"), "civic build sources: a builder's interest is a conflict on the code and on zoning, at every meeting");
        // Every question Civic's council asks of the builders is wired, in and out.
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        foreach (Match m in Regex.Matches(ModSource(@"Civic\ZoningBridges.cs"), @"public static (?:Func|Action)<[^;]+> ([A-Z][A-Za-z]+);"))
        {
            Check(wiring.Contains("ZoningBridges." + m.Groups[1].Value + " = ") && ModSource(@"Civic\ZoningBridges.cs").Contains(m.Groups[1].Value + " = null;"),
                "civic build sources: the council's question " + m.Groups[1].Value + " is wired in, and put back");
        }
        Check(wiring.Contains("ZoningBridges.Reset();"), "civic build sources: the council's questions are put back with the construction game's wiring");
        // The option, on by default, read, in the tree and the feature's own; the tests on the page, in the feature list, with their lines.
        string settings = ModSource(@"Core\Settings.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs"), tests = ModSource(@"Wiring\Wiring.CityTests.cs");
        Check(settings.Contains("public static bool ConstructionCity = true;") && settings.Contains("ConstructionCity = Bool(modId, ConstructionCityId, ConstructionCity);")
              && ModSource(@"Meta\OptionsTree.cs").Contains(".Opt(\"construction_city\", \"construction\")") && guide.Contains(".Owns(Settings.ConstructionCityId)")
              && guide.Contains("AddToggle(Settings.ConstructionCityId, \"reallife_bld_city_option\""), "civic build sources: the option is on by default, read, in the tree and the feature's own");
        int buttons = 0;
        foreach (Match m in Regex.Matches(tests, @"public static void ([A-Z][A-Za-z]+)\(\) => Run([A-Z][A-Za-z]+)\(\);"))
        {
            buttons++;
            Check(m.Groups[1].Value == m.Groups[2].Value && Regex.IsMatch(guide, "\\.Check\\(\"reallife_bld_test_city_[a-z]+\", Wiring\\.CityTests\\." + m.Groups[1].Value + ", \"(Civic|Construction) check \\("),
                "civic build sources: the test " + m.Groups[1].Value + " is in the test run, and its button");
        }
        Check(buttons == 6, "civic build sources: six tests of the city around the company (" + buttons + ")");
        string cityTests = code["ConstructionDirector.CityTests.cs"];
        foreach (string test in new[] { "TestCouncilZoning", "TestRing", "TestBills", "TestYearAward" })
        {
            string body = MethodText(cityTests, "public bool " + test + "()");
            Check(body.Contains("Construction check (") && body.Contains("PASS") && body.Contains("FAIL") && body.Contains("BeforeTest();"),
                "civic build sources: " + test + " writes its check line, passed or failed, and keeps the state to put back");
        }
        foreach (string test in new[] { "Code", "Office" })
        {
            string body = MethodText(tests, "private static bool Run" + test + "()");
            Check(body.Contains("Civic check (") && body.Contains("PASS") && body.Contains("FAIL") && (body.Contains("g.Set(Lever.Permits, was, false);") || body.Contains("o.State.Read(officeBefore);")),
                "civic build sources: " + test + " writes its check line and puts the government back as it was");
        }
    }

    private static void CivicBuildTexts()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { @"Construction\ConstructionDirector.City.cs", @"Construction\ConstructionDirector.Rings.cs", @"Construction\ConstructionDirector.Bills.cs",
                     @"Construction\ConstructionDirector.Award.cs", @"Construction\ConstructionDirector.CityTests.cs", @"Construction\ConstructionApp.City.cs", @"Construction\ConstructionApp.Tenders.cs",
                     @"Construction\ConstructionGuide.cs", @"Construction\ConstructionTables.cs", @"Construction\PlanningCall.cs", @"Civic\CivicOffice.Planning.cs", @"Civic\ZoningBridges.cs", @"Civic\CivicText.cs", @"Civic\OfficePhone.cs",
                     @"Civic\OfficeScenes.cs", @"Wiring\Wiring.CityTests.cs", @"Justice\OwnClaims.cs", @"Justice\BuildClaims.cs", @"Justice\LegalFile.cs", @"Justice\JusticeLaw.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                if (!m.Groups[1].Value.EndsWith("_calldialogtype", StringComparison.Ordinal))
                {
                    keys.Add(m.Groups[1].Value);   // a dialogue's type is a name, not a text
                }
            }
        }
        Check(Regex.Matches(ModSource(@"Construction\ConstructionDirector.Award.cs"), "\"reallife_bld_paper_critic_[1-9]\"").Count == AwardRules.StarsMost,
            "civic build texts: the critic has a piece for every star");
        keys.Add("reallife_opt_tip_construction_city");
        Check(keys.Count > 150, "civic build texts: their keys are found (" + keys.Count + ")");
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("civic build texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            int missing = 0, styled = 0, named = 0;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "civic build texts: " + key + " is in " + codes[l] + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Replace("\n- ", "\n").StartsWith("- ") || value.Replace("\n- ", "\n").Contains(" - "))
                {
                    styled++;
                    Check(false, "civic build texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                else if (l > 0 && words[0].TryGetValue(key, out string english) && NamesOf(english) != NamesOf(value))
                {
                    named++;
                    Check(false, "civic build texts: " + key + " has the English's names in curly brackets in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0 && named == 0, "civic build texts: every key of the city around the company is in " + codes[l] + ".json, in the style, with its names");
        }
        foreach (string key in keys)
        {
            if (key.Contains("_notice_") && words[0].TryGetValue(key, out string notice))
            {
                Check(notice.Length <= 240, "civic build texts: the notification " + key + " is at most 240 characters (" + notice.Length + ")");
            }
        }
        Check(words[0]["reallife_civic_code_up"].Contains("{uptown}") && words[0]["reallife_civic_code_up"].Contains("{vacant}") && words[0]["reallife_guide_construction"].Contains("{bld_strict_storeys}")
              && words[0]["reallife_guide_construction"].Contains("40%") && words[0]["reallife_guide_construction"].Contains("eight tabs"),
            "civic build texts: the code's words quote its figures, and the Guide's page has the city around the company");
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "The city around your construction company:" }, { "pt", "A cidade em volta da sua construtora:" }, { "zh-cn", "你建筑公司身边的城市：" },
            { "de", "Die Stadt rund um deine Baufirma:" }, { "fr", "La ville autour de votre entreprise de construction :" }, { "es", "La ciudad alrededor de tu constructora:" },
            { "ru", "Город вокруг вашей строительной компании:" }, { "nl", "De stad rond je bouwbedrijf:" }, { "tr", "İnşaat şirketinizin etrafındaki şehir:" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = starts.TryGetValue(codes[l], out string start) ? FixRow(words[l], start) : string.Empty;   // the row that holds it
            int line = start != null ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix), "civic build texts: What's new in " + codes[l] + " has the city around the company, before its fixes");
        }
    }
}
