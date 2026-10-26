using System.Collections.Generic;
using RealLifeOverhaul.Fame;

// Checks for Standing, a city that reacts to the player's success (Scripts/Fame): a worked example of wealth and
// recognition, the bounds of wealth and recognition, where each reputation is heading, a day's step, the daily round, the
// day's most for what the player does, the districts, the street, the shops and the staff, what the page calls the
// player, and the save's line.
internal static partial class Program
{
    private static void Fame()
    {
        FameWorkedExample();
        FameBounds();
        FameTargets();
        FameDayRound();
        FameMoves();
        FameStreet();
        FameProfiles();
        FameSave();
        FamePressChecks();
        FameCityChecks();
        FameFixes();
        FameMeetings();
    }

    private static FameInputs FameSample()
    {
        var inputs = new FameInputs { NetWorth = 400000, Weekly = 12000, Followers = 5000, Properties = 2 };
        inputs.Shops.Add(new FameShop { Place = "a~1", District = "north", Figures = true, Satisfaction = 80, Pricing = 60, Staff = 4, Standing = 70, Low = 0 });
        inputs.Shops.Add(new FameShop { Place = "b~2", District = "north", Figures = true, Satisfaction = 70, Pricing = 50, Staff = 2, Standing = 40, Low = 1 });
        inputs.Shops.Add(new FameShop { Place = "c~3", District = "south", Figures = false, Staff = 0, Standing = -1 });
        inputs.Holdings.Add(new FameHolding { District = "south", Let = true, Ask = 1200, Rent = 100 });
        inputs.Holdings.Add(new FameHolding { District = "south", Let = true, Ask = 1000, Rent = 80 });
        inputs.Brand["north"] = 30;
        return inputs;
    }

    private static void FameWorkedExample()
    {
        // A worked example, row by row: worth, a week's income, businesses, followers, wealth, recognition, tier.
        long[,] rows =
        {
            { 5000, 0, 0, 40, 11, 8, 0 },
            { 60000, 2500, 1, 600, 35, 23, 1 },
            { 400000, 12000, 3, 5000, 56, 36, 2 },
            { 3000000, 60000, 6, 30000, 78, 48, 2 },
            { 40000000, 400000, 12, 150000, 100, 60, 3 }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            int wealth = FameRules.Wealth(rows[i, 0], rows[i, 1], (int)rows[i, 2]);
            int recognition = FameRules.Recognition(wealth, (int)rows[i, 3], 0, 0);
            Check(wealth == rows[i, 4] && recognition == rows[i, 5] && (int)FameRules.TierOf(recognition) == rows[i, 6],
                "Standing: the worked example's row " + (i + 1) + " (wealth " + wealth + ", recognition " + recognition + ")");
        }
        Check(FameRules.Recognition(56, 5000, FameRules.PressMost, 100) == 36 + FameRules.PressMost / 10 + 10, "Standing: the papers add 30 points at most and a known brand ten");
    }

    private static void FameBounds()
    {
        Check(FameRules.Wealth(0, 0, 0) == 0 && FameRules.Wealth(-5000, -100, -2) == 0, "Standing: nothing, or debts, are no wealth");
        Check(FameRules.Wealth(long.MaxValue / 4, long.MaxValue / 4, 99) == 100, "Standing: wealth is a hundred at most");
        Check(FameRules.Wealth(400000, 12000, 50) - FameRules.Wealth(400000, 12000, 5) == 0, "Standing: businesses count ten points at most");
        Check(FameRules.FromFollowers(0) == 0 && FameRules.FromFollowers(int.MaxValue) == FameRules.FollowersMost && FameRules.FromFollowers(100) == 6, "Standing: followers count 30 points at most");
        Check(FameRules.Recognition(100, int.MaxValue, 99999, 9999) == 100 && FameRules.Recognition(0, 0, -50, -50) == 0, "Standing: recognition stays between 0 and 100");
        Check(FameRules.TierOf(14) == FameTier.Unknown && FameRules.TierOf(15) == FameTier.Local && FameRules.TierOf(35) == FameTier.Known && FameRules.TierOf(55) == FameTier.Prominent
              && FameRules.TierOf(75) == FameTier.Famous && FameRules.TierOf(100) == FameTier.Famous, "Standing: the levels from 15, 35, 55 and 75");
        Check(FameRules.DistrictKnown(50, 10, 0) == 40 + FameRules.ShopKnownMost && FameRules.DistrictKnown(100, 9, 100) == 100, "Standing: a district knows the player by 80% and its businesses, 24 points at most");
        Check(FameRules.Overall(new[] { 100, 0, 0, 0 }) == 30 && FameRules.Overall(new[] { 50, 50, 50, 50 }) == 50 && FameRules.Overall(new[] { 0, 100, 100, 100 }) == 70,
            "Standing: the overall reputation weighs customers 30, workers 25, neighbourhoods 20, integrity 25");
        Check((int)FamePart.Integrity == 3 && (int)FameCause.Press == 29 && (int)FameCause.Bribe == 23 && (int)FameReaction.Complaint == 6 && (int)FameTier.Famous == 4,
            "Standing: the kinds keep their numbers (they are in saves)");
    }

    private static void FameTargets()
    {
        FameInputs s = FameSample();
        // (80 x 70 + 60 x 30) / 100 = 74, (70 x 70 + 50 x 30) / 100 = 64: 69; the shop with no figures does not count
        Check(FameRules.CustomersTarget(s.Shops) == 69, "Standing: the customers' target is their satisfaction and prices (" + FameRules.CustomersTarget(s.Shops) + ")");
        // standing (4 x 70 + 2 x 40) / 6 = 60, less 20 x 1 / 6 = 3.3: 57
        Check(FameRules.WorkersTarget(s.Shops) == 57, "Standing: the workers' target is their standing less those who stand low (" + FameRules.WorkersTarget(s.Shops) + ")");
        var striking = new List<FameShop> { new FameShop { Staff = 5, Standing = 60, Strike = true } };
        Check(FameRules.WorkersTarget(striking) == 60 - FameRules.StrikePenalty, "Standing: a strike counts 15 points against the workers");
        Check(FameRules.CustomersTarget(new List<FameShop>()) == FameRules.Start && FameRules.WorkersTarget(new List<FameShop>()) == FameRules.Start, "Standing: no shops, no staff: 50");
        Check(FameRules.DistrictTarget(4, 0) == 58 && FameRules.DistrictTarget(40, 0) == 80 && FameRules.DistrictTarget(0, 1) == 40 && FameRules.DistrictTarget(0, 9) == 10
              && FameRules.DistrictTarget(0, 0) == 50, "Standing: a district's target, jobs against rents raised, inside its bounds");
        Check(FameRules.Drift(50, 90) == 2 && FameRules.Drift(50, 51) == 1 && FameRules.Drift(50, 52) == 1 && FameRules.Drift(60, 40) == -2 && FameRules.Drift(50, 50) == 0,
            "Standing: a day's step is an eighth of the way, one at least, two at most");
    }

    private static void FameDayRound()
    {
        FameFile f = FameRules.Open(7, 10);
        FameInputs s = FameSample();
        FameDayReport r = FameRules.Day(f, s, 10);
        Check(f.SettledDay == 10 && f.Businesses == 3 && f.Staff == 6 && f.Properties == 2 && f.Weekly == 12000 && f.Followers == 5000, "Standing: the round reads what it was given");
        Check(f.Wealth == FameRules.Wealth(400000, 12000, 3) && f.Recognition == FameRules.Recognition(f.Wealth, 5000, 0, 30) && r.WealthAfter == f.Wealth && r.RecognitionAfter == f.Recognition,
            "Standing: wealth and recognition from the round (" + f.Wealth + ", " + f.Recognition + ")");
        FameDistrict north = f.District("north"), south = f.District("south");
        Check(north != null && south != null && north.Shops == 2 && north.Jobs == 6 && south.Raised == 1 && south.Shops == 1 && north.Brand == 30,
            "Standing: the districts from the shops, the homes let above the market's rent and the brand");
        Check(north.Opinion == 51 && south.Opinion == 49, "Standing: a district's opinion takes its first step (" + north.Opinion + ", " + south.Opinion + ")");
        Check(f.Rep[0] == 52 && f.Rep[1] == 51 && f.Target[0] == 69 && f.Target[1] == 57, "Standing: the customers and the workers a day nearer their targets (" + f.Rep[0] + ", " + f.Rep[1] + ")");
        Check(f.Rep[2] == 50, "Standing: the neighbourhoods are the districts' average (" + f.Rep[2] + ")");
        int before = f.Rep[0];
        FameRules.Day(f, s, 10);
        Check(f.Rep[0] == before, "Standing: a day played already changes nothing");
        for (int day = 11; day < 60; day++)
        {
            FameRules.Day(f, s, day);
        }
        Check(f.Rep[0] == 69 && f.Rep[1] == 57, "Standing: in time the reputations stand where what is read puts them (" + f.Rep[0] + ", " + f.Rep[1] + ")");
        Check(f.District("north").Opinion == FameRules.DistrictTarget(6, 0) && f.District("south").Opinion == FameRules.DistrictTarget(0, 1), "Standing: and so do the districts");
        f.Rep[3] = 60;
        int forgotten = 0;
        for (int day = 60; day < 74; day++)
        {
            int was = f.Rep[3];
            FameRules.Day(f, s, day);
            forgotten += was - f.Rep[3];
        }
        Check(forgotten == 2, "Standing: integrity forgets a point a week towards 50 (" + forgotten + " in two weeks)");
        int press = f.Press = 200;
        FameRules.Day(f, s, 74);
        Check(f.Press == press - 5, "Standing: the papers are forgotten by a fortieth a day (" + f.Press + ")");
        FameRules.Day(f, s, 104);
        Check(f.Press < 200 * 0.5 && f.Press > 0, "Standing: a month away forgets more, at once (" + f.Press + ")");
    }

    private static void FameMoves()
    {
        FameFile f = FameRules.Open(1, 1);
        int a = FameRules.Move(f, FamePart.Integrity, FameCause.GoodDeed, 5, 3);
        int b = FameRules.Move(f, FamePart.Integrity, FameCause.GoodDeed, 5, 3);
        Check(a == 5 && b == 3 && f.Rep[3] == 58 && f.Notes.Count == 1 && f.Notes[0].Delta == 8, "Standing: 8 points a day at most, the rest lost, one note for the day");
        int c = FameRules.Move(f, FamePart.Integrity, FameCause.Bribe, -12, 3);
        Check(c == -8 && f.Rep[3] == 50 && f.Notes.Count == 2, "Standing: and 8 the other way, apart");
        int d = FameRules.Move(f, FamePart.Integrity, FameCause.GoodDeed, 4, 4);
        Check(d == 4 && f.Notes.Count == 3, "Standing: a new day, a new allowance");
        f.Rep[0] = 98;
        Check(FameRules.Move(f, FamePart.Customers, FameCause.Praise, 5, 4) == 2 && f.Rep[0] == 100, "Standing: never past 100");
        for (int i = 0; i < 40; i++)
        {
            FameRules.Move(f, FamePart.Workers, (FameCause)(1 + i % 20), -1, 10 + i);
        }
        Check(f.Notes.Count == FameRules.NotesMost, "Standing: the newest 16 changes are kept");
        FameFile g = FameRules.Open(1, 1);
        FameRules.Day(g, FameSample(), 1);
        int north = g.District("north").Opinion;
        int moved = FameRules.Move(g, FamePart.Neighbourhood, FameCause.Sponsor, 6, 1, "north");
        Check(moved == 6 && g.District("north").Opinion == north + 6 && g.Rep[2] == (north + 6 + g.District("south").Opinion + 1) / 2,
            "Standing: a district's opinion moves, and the neighbourhoods with it (" + g.Rep[2] + ")");
        Check(FameRules.Publicity(g, 50, 1, FameCause.Interview) == 50 && g.Press == 50 && FameRules.Publicity(g, 9999, 1, FameCause.Show) == FameRules.PressMost - 50,
            "Standing: what the papers made of the player, inside its bounds");
    }

    private static void FameStreet()
    {
        Check(FameRules.StreetChance(14) == 0 && FameRules.StreetChance(15) == 7 && FameRules.StreetChance(70) == 90, "Standing: the street knows the player from 15, 90 per mille at 70");
        FameFile f = FameRules.Open(1, 1);
        Check(FameRules.StreetMay(f, 5, 600) && !FameRules.StreetMay(f, 5, 610) && FameRules.StreetMay(f, 5, 620), "Standing: 20 game minutes between two");
        for (int m = 640; m < 1400; m += 20)
        {
            FameRules.StreetMay(f, 5, m);
        }
        Check(f.StreetToday == FameRules.StreetMost && !FameRules.StreetMay(f, 5, 1430) && FameRules.StreetMay(f, 6, 0), "Standing: eight a day at most, and a new day");
        var raised = new FameDistrict { Name = "x", Opinion = 30, Raised = 2 };
        Check(FameRules.React(70, 20, raised, 100) == FameReaction.Rents && FameRules.React(70, 20, raised, 600) != FameReaction.Rents, "Standing: the rents first where they were raised and the district minds");
        Check(FameRules.React(20, 20, null, 100) == FameReaction.Complaint && FameRules.React(20, 20, null, 900) == FameReaction.Look, "Standing: a poor name brings complaints");
        Check(FameRules.React(80, 20, null, 100) == FameReaction.Praise && FameRules.React(80, 20, null, 500) == FameReaction.Greeting && FameRules.React(80, 70, null, 900) == FameReaction.Remark
              && FameRules.React(80, 20, null, 900) == FameReaction.Look, "Standing: a good name brings praise, and somebody rich hears about the money");
        Check(!FameRules.Speaks(FameReaction.Look) && FameRules.Speaks(FameReaction.Rents), "Standing: a look says nothing");
        Check(FameRules.MayNotice(f, 9) && !FameRules.MayNotice(f, 9) && FameRules.MayNotice(f, 10), "Standing: one routine notice a day");
        Check(!FameRules.ShopMay(f, 9, 10) && FameRules.ShopMay(f, 9, 30) && FameRules.ShopMay(f, 9, 30) && FameRules.ShopMay(f, 9, 30) && !FameRules.ShopMay(f, 9, 30),
            "Standing: customers know the player from 20, three times a day");
        Check(FameRules.VisitCounts(f, "a", 3) && !FameRules.VisitCounts(f, "a", 3) && FameRules.VisitCounts(f, "b", 3) && FameRules.VisitCounts(f, "c", 3) && !FameRules.VisitCounts(f, "d", 3)
              && FameRules.VisitCounts(f, "a", 4), "Standing: a visit counts once a day, three businesses a day");
        Check(FameRules.VisitMood(60) == 1 && FameRules.VisitMood(59) == 0 && FameRules.VisitMood(40) == 0 && FameRules.VisitMood(39) == -1, "Standing: proud from 60, nervous under 40");
    }

    private static void FameProfiles()
    {
        Check(FameRules.Profile(10, 5, 50) == FameProfile.Newcomer && FameRules.Profile(60, 20, 50) == FameProfile.QuietMoney && FameRules.Profile(30, 30, 60) == FameProfile.LocalFavourite
              && FameRules.Profile(30, 30, 40) == FameProfile.WellKnown && FameRules.Profile(90, 80, 70) == FameProfile.Admired && FameRules.Profile(90, 80, 30) == FameProfile.Notorious,
            "Standing: a rich owner with a low profile, a popular local owner, a famous tycoon with a terrible name");
    }

    private static void FameSave()
    {
        FameFile f = FameRules.Open(42, 3);
        FameRules.Day(f, FameSample(), 3);
        FameRules.Move(f, FamePart.Integrity, FameCause.Bribe, -6, 3);
        FameRules.Publicity(f, 37, 3, FameCause.Interview);
        FameRules.StreetMay(f, 3, 700);
        FameRules.VisitCounts(f, "a~1", 3);
        f.NoticeDay = 3;
        var data = new Dictionary<string, string>();
        FameStore.Save(data, f);
        FameFile g = FameStore.Load(data);
        Check(g != null && g.Seed == 42 && g.StartDay == 3 && g.SettledDay == 3 && g.Wealth == f.Wealth && g.Recognition == f.Recognition && g.Press == f.Press && g.NetWorth == f.NetWorth
              && g.Weekly == f.Weekly && g.Followers == f.Followers && g.Businesses == f.Businesses && g.Staff == f.Staff, "Standing: the save's line keeps the three parts");
        Check(g != null && g.Rep[0] == f.Rep[0] && g.Rep[3] == f.Rep[3] && g.Moved[3] == f.Moved[3] && g.MovedDay == 3 && g.StreetToday == 1 && g.StreetMinute == 700 && g.Visited.Count == 1
              && g.Visited[0] == "a~1" && g.NoticeDay == 3, "Standing: the reputations, the day's moves and the street's count");
        Check(g != null && g.Districts.Count == f.Districts.Count && g.District("north").Opinion == f.District("north").Opinion && g.Notes.Count == f.Notes.Count
              && g.Notes[g.Notes.Count - 1].Cause == FameCause.Interview && g.Notes[0].Part == 3, "Standing: the districts and the changes");
        Check(FameStore.Read("ST9|1|2") == null && FameStore.Read(null) == null && FameStore.Read("garbage") == null, "Standing: another version's line is not read (a new file)");
        string later = FameStore.Write(f) + "\nN|3|9|2|1|\nN|3|0|99|1|\nX|whatever\nD||40";
        FameFile h = FameStore.Read(later);
        Check(h != null && h.Notes.Count == f.Notes.Count && h.Districts.Count == f.Districts.Count, "Standing: a later version's kinds and lines are left out a line at a time");
        string damaged = FameStore.Write(f).Replace("|" + f.Recognition + "|", "|abc|");
        FameFile k = FameStore.Read(damaged);
        Check(k != null && k.Recognition == 0, "Standing: a damaged number is read as nothing, and the load goes on");
    }
}
