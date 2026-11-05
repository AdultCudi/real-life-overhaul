using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The rules the Tech app's kit face stands on (the owner's mockup features): the month before a firm's takings, the sector league, the outlets' reviews, the daily sales ring, the data
// centre's supply limits and its 90 day outage ring, the five points of a game's making, and each one's save. Run through the same methods the day turn calls, over many days.
internal static partial class Program
{
    private static void TechRulesChecks()
    {
        TechChangeChecks();
        TechLeagueChecks();
        TechReviewChecks();
        TechSalesChecks();
        TechCentreChecks();
        TechMilestoneChecks();
        TechColoChecks();
    }

    private static void TechChangeChecks()
    {
        Check(TechStatusRules.Firm(true, 0, 0, 0) == TechStatusRules.NoTeam && TechStatusRules.Firm(true, 3, 1, 0) == TechStatusRules.InProduction
              && TechStatusRules.Firm(true, 3, 0, 2) == TechStatusRules.Running && TechStatusRules.Firm(true, 3, 0, 0) == TechStatusRules.NeedsProduct && TechStatusRules.Firm(false, 3, 0, 0) == TechStatusRules.Running,
            "tech status: a firm is in production, running, in need of a product or without a team by what it has");
        Check(TechStatusRules.Centre(90, 100, false, true) == TechStatusRules.NearlyFull && TechStatusRules.Centre(89, 100, false, true) == TechStatusRules.Running
              && TechStatusRules.Centre(10, 100, true, true) == TechStatusRules.Down && TechStatusRules.Centre(10, 100, false, false) == TechStatusRules.Down,
            "tech status: a data centre is nearly full at 90 per cent of its racks, down when down or short of a supply");
        Check(TechStatusRules.Startup(true, 0) == TechStatusRules.SeedFunded && TechStatusRules.Startup(true, 3) == TechStatusRules.Growing && TechStatusRules.Startup(true, 5) == TechStatusRules.Expanding
              && TechStatusRules.Startup(false, 2) == TechStatusRules.Closed, "tech status: a stake's startup is seed funded, growing, expanding or closed by its round");
        // The month before ages a day at a time: 60 days of a steady 1,000 a day make a flat 0 per cent, a rise of a fifth in the second month makes about 20.
        var firm = new Firm { Place = "p" };
        for (int day = 0; day < 60; day++)
        {
            FirmsRules.Roll(firm, day, 1000);
        }
        Check(firm.Revenue30 == 30000 && TechStatusRules.Change(firm.Revenue30, firm.Prev30, firm.PrevDays, out int flat) && flat == 0, "tech change: a steady firm's takings are level against the month before (" + firm.Prev30 + ")");
        for (int day = 60; day < 90; day++)
        {
            FirmsRules.Roll(firm, day, 1200);
        }
        TechStatusRules.Change(firm.Revenue30, firm.Prev30, firm.PrevDays, out int rise);
        Check(rise >= 15 && rise <= 25, "tech change: a rise of a fifth in the month shows as about 20 per cent (" + rise + ")");
        var young = new Firm { Place = "y" };
        for (int day = 0; day < 20; day++)
        {
            FirmsRules.Roll(young, day, 500);
        }
        Check(!TechStatusRules.Change(young.Revenue30, young.Prev30, young.PrevDays, out _), "tech change: a firm younger than a month has no month before to say it by");
        // The save carries it at the end of the firm's record, and a line from before it reads as a month not yet gathered.
        var file = new FirmsFile { Seed = 4 };
        firm.Place = "Long Rd~7";
        file.Firms.Add(firm);
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(file), out _);
        Firm b = back.FirmAt("Long Rd~7");
        Check(b != null && b.Prev30 == firm.Prev30 && b.PrevDays == firm.PrevDays && b.Prev30 > 0, "tech change: the month before comes back from the save");
        string text = FirmsStore.Write(file);
        string[] parts = text.Split('~');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("f:", StringComparison.Ordinal))
            {
                string[] fields = parts[i].Substring(2).Split(';')[0].Split('|');
                parts[i] = "f:" + string.Join("|", fields, 0, fields.Length - 2);   // the line as it was before the two fields
            }
        }
        FirmsFile old = FirmsStore.Read(string.Join("~", parts), out _);
        Firm o = old.FirmAt("Long Rd~7");
        Check(o != null && o.Prev30 == 0 && o.PrevDays == 0 && o.Revenue30 == firm.Revenue30, "tech change: an old save's firm loads with no month before and plays on");
    }

    private static void TechLeagueChecks()
    {
        var f = new SectorFile();
        f.Rivals.Add(new RivalProduct { Id = 1, Company = 0, Market = (int)SectorMarket.EnterpriseSoftware, Customers = 100, Tier = 1 });
        f.Rivals.Add(new RivalProduct { Id = 2, Company = 2, Market = (int)SectorMarket.BusinessSoftware, Customers = 5000, Tier = 1 });
        f.Rivals.Add(new RivalProduct { Id = 3, Company = 2, Market = (int)SectorMarket.Licences, Customers = 10, Tier = 1 });
        f.Rivals.Add(new RivalProduct { Id = 4, Company = -1, Startup = 3, Market = (int)SectorMarket.Apps, Customers = 100000, Tier = 1 });
        var rows = new List<LeagueRow>();
        int rank = SectorRules.League(f, 0, rows);
        Check(rank == 0 && rows.Count == 2 && rows[0].Monthly >= rows[1].Monthly, "tech league: only the listed companies that sell software stand in it, the startups' products do not");
        long enterprise = 100L * FirmsCatalogue.Price[2];
        long business = 5000L * FirmsCatalogue.Price[1] + 10L * FirmsCatalogue.LicenceDeal * FirmsCatalogue.LicenceMaintenance / 1000 / 12;
        Check(rows[0].Company == (enterprise > business ? 0 : 2) && Math.Max(enterprise, business) == rows[0].Monthly, "tech league: a rival's takings are its customers at the price its market asks (" + rows[0].Monthly + ")");
        rank = SectorRules.League(f, rows[0].Monthly + 1, rows);
        Check(rank == 1 && rows[0].Company == SectorRules.LeagueYou && rows.Count == 3, "tech league: the player's firms rank first with more takings than every rival");
        rank = SectorRules.League(f, 1, rows);
        Check(rank == 3, "tech league: and last with hardly any");
        var again = new List<LeagueRow>();
        SectorRules.League(f, 1, again);
        bool same = again.Count == rows.Count;
        for (int i = 0; same && i < rows.Count; i++)
        {
            same = rows[i].Company == again[i].Company && rows[i].Monthly == again[i].Monthly;
        }
        Check(same, "tech league: two reads of one state give one order");
    }

    private static GameDev TechMade(int id, int quality, int score, int launched)
    {
        GameDev g = MadeGame(id, Genre.Strategy, Platform.Pc, 400000, quality, 500, launched - 1);
        g.Score = score;
        g.Launched = launched;
        g.Phase = (int)GamePhase.Live;
        return g;
    }

    private static void TechReviewChecks()
    {
        GameDev g = TechMade(7, 70, 80, 200);
        g.PartCraft[(int)RealLifeOverhaul.Tech.Part.Art] = g.PartDone[(int)RealLifeOverhaul.Tech.Part.Art] * 95;       // art is its best part
        g.PartCraft[(int)RealLifeOverhaul.Tech.Part.Content] = g.PartDone[(int)RealLifeOverhaul.Tech.Part.Content] * 30;   // content its weakest
        StudioRules.WriteReviews(g, 11, 200);
        int sum = 0;
        var outlets = new HashSet<int>();
        bool inWeek = true;
        foreach (GameReview r in g.Reviews)
        {
            sum += r.Score;
            outlets.Add(r.Outlet);
            inWeek &= r.Day >= 200 && r.Day < 207;
        }
        Check(g.Reviews.Count == StudioCatalogue.Outlets.Length && outlets.Count == g.Reviews.Count, "tech reviews: all twelve outlets review a game once each");
        Check(sum / g.Reviews.Count == 80 && StudioRules.ReviewAverage(g) == 80, "tech reviews: the outlets' average is the game's critics' score (" + sum / g.Reviews.Count + ")");
        Check(inWeek, "tech reviews: each comes in the first week after launch");
        bool part = true, above = true;
        foreach (GameReview r in g.Reviews)
        {
            part &= r.Praise ? r.Part == (int)RealLifeOverhaul.Tech.Part.Art : r.Part == (int)RealLifeOverhaul.Tech.Part.Content || r.Part == StudioRules.PartBugs;
            above &= r.Praise == (r.Score >= 80);
        }
        Check(part && above, "tech reviews: an outlet above the score praises the game's best part, one below blames its weakest part or its bugs");
        GameDev same = TechMade(7, 70, 80, 200);
        same.PartCraft[(int)RealLifeOverhaul.Tech.Part.Art] = same.PartDone[(int)RealLifeOverhaul.Tech.Part.Art] * 95;
        same.PartCraft[(int)RealLifeOverhaul.Tech.Part.Content] = same.PartDone[(int)RealLifeOverhaul.Tech.Part.Content] * 30;
        StudioRules.WriteReviews(same, 11, 200);
        bool equal = same.Reviews.Count == g.Reviews.Count;
        for (int i = 0; equal && i < g.Reviews.Count; i++)
        {
            equal = same.Reviews[i].Outlet == g.Reviews[i].Outlet && same.Reviews[i].Score == g.Reviews[i].Score && same.Reviews[i].Day == g.Reviews[i].Day && same.Reviews[i].Part == g.Reviews[i].Part;
        }
        Check(equal, "tech reviews: the same game and seed write the same reviews (a host and a guest agree)");
        GameDev other = TechMade(8, 70, 80, 200);
        StudioRules.WriteReviews(other, 11, 200);
        bool differs = false;
        for (int i = 0; i < g.Reviews.Count; i++)
        {
            differs |= other.Reviews[i].Outlet != g.Reviews[i].Outlet || other.Reviews[i].Score != g.Reviews[i].Score;
        }
        Check(differs, "tech reviews: another game has its own order of kind and harsh outlets");
        GameDev none = TechMade(9, 70, -1, -1);
        StudioRules.WriteReviews(none, 11, 200);
        Check(none.Reviews.Count == 0 && StudioRules.ReviewAverage(none) == -1, "tech reviews: a game with no score has no reviews");
        // The release writes them and the save keeps them with the game.
        GameDev rel = MadeGame(12, Genre.Action, Platform.Pc, 400000, 70, 500, 300);
        var plat = new long[StudioCatalogue.GamePlatforms];
        StudioRules.Release(rel, StudioFileFor(5), 301, 500, 5, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
        Check(rel.Reviews.Count == StudioCatalogue.Outlets.Length && StudioRules.ReviewAverage(rel) >= rel.Score - 1 && StudioRules.ReviewAverage(rel) <= rel.Score + 1, "tech reviews: a release writes the outlets' reviews (average " + StudioRules.ReviewAverage(rel) + " of score " + rel.Score + ")");
        var file = StudioFileFor(5);
        file.Games.Add(rel);
        StudioFile back = StudioStore.Read(StudioStore.Write(file), out _);
        GameDev b = back.Games.Count > 0 ? back.Games[0] : null;
        bool kept = b != null && b.Reviews.Count == rel.Reviews.Count;
        for (int i = 0; kept && i < rel.Reviews.Count; i++)
        {
            kept = b.Reviews[i].Outlet == rel.Reviews[i].Outlet && b.Reviews[i].Score == rel.Reviews[i].Score && b.Reviews[i].Praise == rel.Reviews[i].Praise && b.Reviews[i].Variant == rel.Reviews[i].Variant;
        }
        Check(kept, "tech reviews: the save keeps each game's reviews");
    }

    private static void TechSalesChecks()
    {
        GameDev g = MadeGame(3, Genre.Strategy, Platform.Pc, 400000, 70, 500, 99);
        var f = StudioFileFor(5);
        f.Games.Add(g);
        var plat = new long[StudioCatalogue.GamePlatforms];
        StudioRules.Release(g, f, 100, 500, f.Seed, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
        long before = g.Sold;
        long total = 0;
        for (int d = 100; d < 160; d++)
        {
            long at = g.Sold;
            StudioRules.SellDay(g, f, d, StudioRules.AppealOf(g.Score, d - g.Launched) + StudioCatalogue.TypicalRivalAppeal, plat, 1000, 1000);
            total += g.Sold - at;
            Check(StudioRules.SalesOn(g, d) == g.Sold - at, "tech sales: day " + d + " is in the ring as sold");
        }
        long ring = 0;
        for (int d = 100; d < 160; d++)
        {
            ring += StudioRules.SalesOn(g, d);
        }
        Check(ring == total && total > 0 && g.SalesLast == 159, "tech sales: sixty days of copies a day are in the ring and add to the copies sold (" + ring + " of " + total + ")");
        // past ninety days the oldest fall out, and a gap in the days is written as none
        for (int d = 160; d < 220; d++)
        {
            StudioRules.SellDay(g, f, d, StudioRules.AppealOf(g.Score, d - g.Launched) + StudioCatalogue.TypicalRivalAppeal, plat, 1000, 1000);
        }
        Check(StudioRules.SalesOn(g, 100) == 0 && StudioRules.SalesOn(g, 130) > 0 && StudioRules.SalesOn(g, 219) >= 0, "tech sales: the ring holds the last 90 days and no more");
        StudioRules.RecordSales(g, 230, 5);
        Check(StudioRules.SalesOn(g, 225) == 0 && StudioRules.SalesOn(g, 230) == 5, "tech sales: days with no sale between two are none, never an old day's copies");
        StudioRules.RecordSales(g, 400, 9);
        Check(StudioRules.SalesOn(g, 399) == 0 && StudioRules.SalesOn(g, 400) == 9 && StudioRules.SalesOn(g, 230) == 0, "tech sales: a long gap clears the ring");
        // the save keeps the ring and a game from before it has none
        var file = StudioFileFor(5);
        file.Games.Add(g);
        StudioFile back = StudioStore.Read(StudioStore.Write(file), out _);
        GameDev b = back.Games[0];
        bool same = b.SalesLast == g.SalesLast;
        for (int d = 311; d <= 400 && same; d++)
        {
            same = StudioRules.SalesOn(b, d) == StudioRules.SalesOn(g, d);
        }
        Check(same, "tech sales: the save keeps the daily sales ring");
        // the rival game's line runs by the rule that sells it
        var rival = new RivalGame { Id = 1, Studio = 2, Genre = (int)Genre.Strategy, Score = 80, Launched = 50, Units = 90000 };
        var line = new float[90];
        StudioRules.RivalSeries(rival, 40, line);
        float sum = 0f;
        bool front = line[11] > line[40];
        for (int i = 0; i < line.Length; i++)
        {
            sum += line[i];
        }
        Check(line[5] == 0f && line[10] > 0f && front && sum <= rival.Units, "tech sales: a rival game's line starts at its release, is front loaded and never sells past its life (" + sum + " of " + rival.Units + ")");
        f.Rivals.Add(rival);
        f.Rivals.Add(new RivalGame { Id = 2, Studio = 1, Genre = (int)Genre.Racing, Score = 70, Launched = 10, Units = 500000, Sold = 400000 });
        Check(StudioRules.BestRival(f, (int)Genre.Strategy) == rival && StudioRules.BestRival(f, (int)Genre.Puzzle).Id == 2, "tech sales: the line beside a game is its genre's best selling rival, else the best selling of all");
    }

    private static void TechCentreChecks()
    {
        var d = new DataCentre { Place = "dc~1", Tier = 2, Sqm = 1200, Racks = 100 };   // a floor of 200 racks
        CentreLimits l = InfraLimits.Of(d, 1000);
        Check(l.PowerCap == 200L * InfraCatalogue.RackKw * InfraLimits.FeedShare[2] / 1000 && l.PowerUse == 100L * InfraCatalogue.RackKw && l.NetCap == 1000 && l.NetUse == 100L * InfraLimits.MbpsPerRack,
            "tech limits: a hall's supplies are its floor's, by its tier, and the connection is the plan's");
        int fit = InfraLimits.Fit(d, false, 1000, out int binding);
        Check(fit == 0 && binding == InfraLimits.Bandwidth, "tech limits: the connection is full at ten racks to the gigabit (fit " + fit + ")");
        fit = InfraLimits.Fit(d, false, 100000, out binding);
        Check(fit == 100 && binding == InfraLimits.Floor, "tech limits: with a fast line the floor is what is left (fit " + fit + ")");
        d.GpuRacks = 20;
        d.Racks = 60;   // 60 + 40 on the floor, 60 x 8 + 20 x 32 = 1120 kW of a feed that carries 1600
        fit = InfraLimits.Fit(d, true, 100000, out binding);
        Check(fit == (int)((200L * InfraCatalogue.RackKw - 1120) / InfraCatalogue.GpuRackKw) && binding == InfraLimits.Power, "tech limits: accelerator racks fill the power feed before the floor (fit " + fit + ", " + binding + ")");
        d.GpuRacks = 100;   // the feed is over what it carries after racks were bought, however: nothing more fits and the percentage says over
        Check(InfraLimits.Fit(d, false, 100000, out _) == 0 && InfraLimits.Percent(InfraLimits.Of(d, 1000).PowerUse, InfraLimits.Of(d, 1000).PowerCap) > 100, "tech limits: a hall over its feed takes no more and says it is over");
        Check(InfraLimits.Of(d, -1).NetCap == InfraCatalogue.NetNeeded && InfraLimits.Of(d, 0).NetCap == InfraCatalogue.NetNeeded, "tech limits: an office with no plan read yet is held to the business line a centre needs");
        // the 90 day outage ring: days down are bits, a day 90 on takes the place of the one before, and the uptime is of the days since it was fitted out
        var c = new DataCentre { Place = "dc~2", Fitted = 10 };
        for (int day = 10; day < 100; day++)
        {
            InfraRules.MarkDay(c, day, day == 20 || day == 21 || day == 50);
        }
        Check(InfraRules.Uptime(c, 99, 10) == (90 - 3) * 1000 / 90, "tech uptime: three days down in ninety is " + InfraRules.Uptime(c, 99, 10) + " of a thousand");
        for (int day = 100; day < 125; day++)
        {
            InfraRules.MarkDay(c, day, false);
        }
        Check(InfraRules.Uptime(c, 124, 10) == (90 - 1) * 1000 / 90, "tech uptime: the days of the first week of outages fall out of the ring 90 days on (" + InfraRules.Uptime(c, 124, 10) + ")");
        Check(InfraRules.Uptime(new DataCentre { Fitted = 50 }, 50, 50) == 1000 && InfraRules.Uptime(new DataCentre { Fitted = 50 }, 52, 50) == 1000, "tech uptime: a new centre with no day down is fully up");
        // the ring, the month before and the supply limits survive the save; an old save's centre reads as always up
        var file = new InfraFile { Seed = 3 };
        c.Place = "dc~2";
        c.Racks = 8;
        file.Centres.Add(c);
        InfraFile back = InfraStore.Read(InfraStore.Write(file), out _);
        DataCentre b = back.CentreAt("dc~2");
        bool ring = b != null;
        for (int day = 35; day < 125 && ring; day++)
        {
            ring = ((b.Outage90[(day % 90) / 30] >> (day % 30)) & 1) == ((c.Outage90[(day % 90) / 30] >> (day % 30)) & 1);
        }
        Check(ring && b.Prev30 == c.Prev30 && b.PrevDays == c.PrevDays, "tech uptime: the outage ring comes back from the save");
        string text = InfraStore.Write(file);
        string[] parts = text.Split('~');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("d:", StringComparison.Ordinal))
            {
                string[] fields = parts[i].Substring(2).Split(';')[0].Split('|');
                parts[i] = "d:" + string.Join("|", fields, 0, fields.Length - 5);   // the line as it was before the five fields
            }
        }
        InfraFile old = InfraStore.Read(string.Join("~", parts), out _);
        DataCentre o = old.CentreAt("dc~2");
        Check(o != null && o.Racks == 8 && o.Prev30 == 0 && o.Outage90[0] == 0 && InfraRules.Uptime(o, 150, 10) == 1000, "tech uptime: an old save's centre loads with no day down and plays on");
    }

    private static void TechMilestoneChecks()
    {
        var states = new int[StudioViews.MilestoneCount];
        var g = new GameDev { Phase = (int)GamePhase.Pre, Milestone = -1, Launched = -1, Work = 1000, Done = 100 };
        int met = StudioViews.Milestones(g, states);
        Check(met == 1 && states[StudioViews.MsPitch] == StudioViews.StateDone && states[StudioViews.MsReview1] == StudioViews.StateNext && states[StudioViews.MsLaunch] == StudioViews.StateLater,
            "tech milestones: a game in pre-production has met its pitch and waits for the first review");
        g.Phase = (int)GamePhase.Production;
        g.Milestone = (int)GamePhase.Pre;
        met = StudioViews.Milestones(g, states);
        Check(met == 1 && states[StudioViews.MsReview1] == StudioViews.StateNow, "tech milestones: a review that waits at the desk is the one that stands now");
        g.Milestone = -1;
        g.Done = 400;
        g.Hidden = 10;
        met = StudioViews.Milestones(g, states);
        Check(met == 2 && states[StudioViews.MsPlaytest] == StudioViews.StateNow, "tech milestones: with something to show and bugs unfound the playtest is what waits");
        g.Phase = (int)GamePhase.Polish;
        g.Milestone = (int)GamePhase.Production;
        met = StudioViews.Milestones(g, states);
        Check(met == 3 && states[StudioViews.MsReview2] == StudioViews.StateNow && states[StudioViews.MsLaunch] == StudioViews.StateNext, "tech milestones: the second review then the launch");
        g.Milestone = -1;
        g.Phase = (int)GamePhase.Ready;
        met = StudioViews.Milestones(g, states);
        Check(met == 4 && states[StudioViews.MsLaunch] == StudioViews.StateNow, "tech milestones: a game ready for its day has four met and its launch now");
        g.Phase = (int)GamePhase.Live;
        g.Launched = 300;
        met = StudioViews.Milestones(g, states);
        Check(met == StudioViews.MilestoneCount, "tech milestones: a game on sale has met all five");
    }
}
