using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using GamePart = RealLifeOverhaul.Tech.Part;

// The game studio (Tech/Studio; plans/tech-overhaul DRAFT2 section 3): the catalogue's shape, the plan from a budget, a game
// built day by day by a crew into its parts with milestones and bugs, the desk sessions' choices, hype and the review, a
// game's whole life of sales by the studio's own rules held to the money scale's bands at every scope, no cap but the audience
// (a masterpiece in a hot genre sells for years, a flop drops in weeks, the audience grows), a studio over ten years against
// the end game, piracy and DRM, the models, after launch, bonuses and the rivals' pull, the rival studios (seeded, the same on
// every machine, kept, trends), the older saves' games converted with nothing jumping, the save's line and the shared city's
// part round trip, every screen built offline with what to do next and one primary action, the option and the sources, the
// texts, and what a day costs. Run with "studio" on the command line it prints the bands and the ten years.
internal static partial class Program
{
    private static void Studio()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool print = Array.IndexOf(Environment.GetCommandLineArgs(), "studio") >= 0;
        StudioBridges.Reset();
        StudioShape();
        StudioPlan();
        StudioBuilding();
        StudioSessions();
        StudioHypeAndReview();
        StudioBands(print);
        StudioNoCap(print);
        StudioTenYears(print);
        StudioPiracyAndModels();
        StudioAfterLaunch();
        StudioBonuses();
        StudioRivals();
        StudioConversion();
        StudioSave();
        StudioScreens();
        StudioWiring();
        StudioTexts();
        StudioTimings();
        StudioBridges.Reset();
        Console.WriteLine("  Game studio: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- helpers

    private const string StudioPlace = "Studio Street~1";
    private const int StudioDay = 400;   // a day into the market's second year: the critics' bar has moved once

    // A budget's step on the concept's slider.
    private static int BudgetStep(long budget)
    {
        int best = 0;
        for (int i = 0; i < StudioCatalogue.Budgets.Length; i++)
        {
            best = Math.Abs(StudioCatalogue.Budgets[i] - budget) < Math.Abs(StudioCatalogue.Budgets[best] - budget) ? i : best;
        }
        return best;
    }

    // A game made to a quality: greenlit as planned and self-published, its plan built in full at that craft, the scope's usual
    // campaign spent, hype as given, its cost the budget: ready for its day.
    private static GameDev MadeGame(int id, Genre genre, Platform platform, long budget, int quality, int hype, int day)
    {
        var d = new GameDraft { Place = StudioPlace, Genre = (int)genre, Platform = (int)platform, Budget = BudgetStep(budget), Price = 2 };
        GameDev g = StudioRules.Greenlight(d, id, new[] { StudioRules.AsPlanned, StudioRules.SelfPublish, StudioRules.NoDrm }, false, day);
        g.Done = g.Work;
        for (int p = 0; p < (int)GamePart.Count; p++)
        {
            g.PartDone[p] = g.Plan * StudioCatalogue.PartNeed[p] / 1000;
            g.PartCraft[p] = g.PartDone[p] * quality;
        }
        g.Phase = (int)GamePhase.Ready;
        g.CampaignSpent = g.Budget * ScaleCatalogue.MarketingShare[g.Scope] / 1000;
        g.Hype = hype;
        g.Spent = g.Budget;
        return g;
    }

    // A game's life from its launch for so many days among the typical rivals of its genre: the money it kept, its copies by the out.
    private static long GameLife(GameDev g, StudioFile f, int day, int days, out long units)
    {
        var plat = new long[StudioCatalogue.GamePlatforms];
        StudioRules.Release(g, f, day, 500, f.Seed, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
        long net = 0;
        for (int d = day; d < day + days; d++)
        {
            long appeal = StudioRules.AppealOf(g.Score, d - g.Launched) + StudioCatalogue.TypicalRivalAppeal;
            net += StudioRules.SellDay(g, f, d, appeal, plat, 1000, 1000).Payout;
        }
        units = g.Sold;
        return net;
    }

    private static StudioFile StudioFileFor(int seed) => new StudioFile { Seed = seed, FirstDay = 0 };

    private sealed class PlainWords : IStudioFormat
    {
        public string Money(long cents) => "$" + (cents / 100).ToString("N0");

        public string Whole(long n) => n.ToString("N0");

        public string Mille(int thousandths) => (thousandths / 10) + "%";

        public string Day(int day) => "day " + day;

        public string Text(string key) => key;
    }

    // ---------------------------------------------------------------- the catalogue

    private static void StudioShape()
    {
        bool weights = StudioCatalogue.PartWeights.Length == (int)Genre.Count, settings = StudioCatalogue.GenreSettings.Length == (int)Genre.Count && StudioCatalogue.GenreFit.Length == (int)Genre.Count;
        for (int g = 0; g < (int)Genre.Count; g++)
        {
            int sum = 0, best = 0;
            foreach (int w in StudioCatalogue.PartWeights[g])
            {
                sum += w;
            }
            weights &= StudioCatalogue.PartWeights[g].Length == (int)GamePart.Count && sum == 1000;
            settings &= StudioCatalogue.GenreSettings[g].Length == 4 && StudioCatalogue.GenreFit[g].Length == 4;
            for (int s = 0; s < 4 && settings; s++)
            {
                settings &= StudioCatalogue.GenreSettings[g][s] >= 0 && StudioCatalogue.GenreSettings[g][s] < StudioCatalogue.Settings;
                best = Math.Max(best, StudioCatalogue.GenreFit[g][s]);
            }
            settings &= best > 1000 && StudioCatalogue.GenreFit[g][0] != best | StudioCatalogue.GenreFit[g][1] != best;
        }
        Check(weights, "studio: every genre weighs the five parts of quality, summing to the whole");
        Check(settings && StudioCatalogue.SettingKeys.Length == StudioCatalogue.Settings,
            "studio: every genre offers four of the settings, one fitting best and the menu's order not telling which");
        bool lengths = StudioCatalogue.UsualScope.Length == (int)Genre.Count && StudioCatalogue.Online.Length == (int)Genre.Count && StudioCatalogue.ExpectBase.Length == (int)Genre.Count
                       && StudioCatalogue.GenreShare.Length == StudioCatalogue.GamePlatforms && StudioCatalogue.InstalledBase.Length == StudioCatalogue.GamePlatforms
                       && StudioCatalogue.RivalStock.Length == StudioCatalogue.RivalNames.Length && StudioCatalogue.RivalScope.Length == StudioCatalogue.RivalNames.Length
                       && StudioCatalogue.RivalGenres.Length == StudioCatalogue.RivalNames.Length && StudioCatalogue.Piracy.Length == StudioCatalogue.GamePlatforms
                       && StudioCatalogue.PhaseShare.Length == 3 && StudioCatalogue.PartNeed.Length == (int)GamePart.Count;
        int phases = StudioCatalogue.PhaseShare[0] + StudioCatalogue.PhaseShare[1] + StudioCatalogue.PhaseShare[2], need = 0;
        foreach (int n in StudioCatalogue.PartNeed)
        {
            need += n;
        }
        Check(lengths && phases == 1000 && need == 1000, "studio: every table is as long as what reads it; the phases and the parts' plan each make the whole work");
        bool budgets = true;
        for (int i = 0; i < StudioCatalogue.Budgets.Length; i++)
        {
            long b = StudioCatalogue.Budgets[i];
            int s = StudioRules.ScopeOf(b);
            budgets &= b >= ScaleCatalogue.BudgetLeast[s] && b <= ScaleCatalogue.BudgetMost[s] && (i == 0 || b > StudioCatalogue.Budgets[i - 1]);
        }
        Check(budgets && StudioCatalogue.Budgets[0] == ScaleCatalogue.BudgetLeast[0] && StudioCatalogue.Budgets[StudioCatalogue.Budgets.Length - 1] == ScaleCatalogue.BudgetMost[2],
            "studio: the concept's budgets run from the money scale's least to its most, each inside its scope's band and rising");
        long avg = 0;
        foreach (int share in StudioCatalogue.GenreShare[(int)Platform.Pc])
        {
            avg += share;
        }
        avg = StudioCatalogue.InstalledBase[(int)Platform.Pc] * (avg / (int)Genre.Count) / 1000;
        Check(Math.Abs(avg - StudioCatalogue.ReferenceMarket) * 20 <= StudioCatalogue.ReferenceMarket,
            "studio: the reference market is the average genre's PC players on the first day (" + avg + " against " + StudioCatalogue.ReferenceMarket + ")");
        Check(StudioCatalogue.PointCents * FirmsCatalogue.PointsPerDay == StudioCatalogue.PersonDayCents,
            "studio: a work point costs a mid-level's day over the points they give in it");
    }

    // ---------------------------------------------------------------- the plan

    private static void StudioPlan()
    {
        long indie = StudioRules.WorkFor(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, false);
        long mid = StudioRules.WorkFor(ScaleCatalogue.BudgetTypical[1], StudioRules.AsPlanned, false);
        long big = StudioRules.WorkFor(ScaleCatalogue.BudgetTypical[2], StudioRules.AsPlanned, false);
        Check(indie == 20000000L * 750 / 1000 / StudioCatalogue.PointCents && mid > indie * 10 && big > mid * 10,
            "studio: a budget buys its team share's work points (an indie game's typical budget " + indie + " points, mid " + mid + ", big " + big + ")");
        int indieDays = StudioRules.DaysLeft(indie, 6 * FirmsCatalogue.PointsPerDay), bigDays = StudioRules.DaysLeft(big, 100 * FirmsCatalogue.PointsPerDay);
        Check(indieDays >= 60 && indieDays <= 150 && bigDays > 3 * 365 && StudioRules.DaysLeft(100, 0) == 0,
            "studio: six mid-levels make a typical indie game in " + indieDays + " days; a hundred take " + bigDays + " days over a typical big one, years as a big game takes");
        Check(StudioRules.WorkFor(1000000000L, StudioRules.Lean, false) < StudioRules.WorkFor(1000000000L, StudioRules.AsPlanned, false)
              && StudioRules.WorkFor(1000000000L, StudioRules.Ambitious, false) > StudioRules.WorkFor(1000000000L, StudioRules.AsPlanned, false)
              && StudioRules.WorkFor(1000000000L, StudioRules.AsPlanned, true) < StudioRules.WorkFor(1000000000L, StudioRules.AsPlanned, false),
            "studio: a lean concept takes less work, an ambitious one more, the studio's own engine less again");
        Check(StudioRules.Visibility(ScaleCatalogue.BudgetTypical[0]) < StudioRules.Visibility(ScaleCatalogue.BudgetTypical[1]) && StudioRules.Visibility(ScaleCatalogue.BudgetMost[2]) == 1000
              && StudioRules.Visibility(1) >= 1,
            "studio: a bigger budget is seen by more of its genre's players, all of them at the most, one in a thousand at the least");
        Check(StudioRules.UsualPrice(0, (int)Platform.Pc) == 1500 && StudioRules.UsualPrice(2, (int)Platform.Console) == 6900 && StudioRules.UsualPrice(0, (int)Platform.Mobile) == 375
              && StudioRules.PriceShown((int)GameModel.FreeToPlay, 4, 1, 0) == 0 && StudioRules.PriceShown((int)GameModel.Subscription, 2, 2, 0) == 1500,
            "studio: the usual price by scope and platform (phones far cheaper), a free game's nothing, a subscription's month a quarter of a copy");
        Check(StudioBridges.World((int)Genre.Puzzle, (int)Platform.Mobile, 0) == 2600000000L * 450 / 1000
              && StudioBridges.World(0, 0, StudioCatalogue.MarketYear) == 1000000000L * 1030 / 1000 * 300 / 1000
              && StudioBridges.World(0, 0, StudioCatalogue.MarketYear * 10) > StudioBridges.World(0, 0, StudioCatalogue.MarketYear * 9) && StudioBridges.World(-1, 0, 0) == 0,
            "studio: the documented world: each platform's players by the genre's share, growing every market year with nothing stopping it");
    }

    // ---------------------------------------------------------------- building

    private static StudioCrew Crew(int engineers, int designers, int product, int testers, int craft)
    {
        return new StudioCrew
        {
            Engineering = engineers * FirmsCatalogue.PointsPerDay, Design = designers * FirmsCatalogue.PointsPerDay, Product = product * FirmsCatalogue.PointsPerDay,
            Testing = testers * FirmsCatalogue.PointsPerDay, CraftEngineering = craft, CraftDesign = craft, CraftProduct = craft, CraftTesting = craft, Faults = 120, Support = 1
        };
    }

    // A game built to the end by a crew, milestones held as planned: its days and quality, a playtest at each milestone if asked.
    private static GameDev BuildAll(long budget, int ambition, StudioCrew c, bool playtests, int cutAt, out int days, out int milestones)
    {
        var d = new GameDraft { Place = StudioPlace, Genre = (int)Genre.Rpg, Budget = BudgetStep(budget), Price = 2 };
        GameDev g = StudioRules.Greenlight(d, 1, new[] { ambition, StudioRules.SelfPublish, StudioRules.NoDrm }, false, 0);
        days = 0;
        milestones = 0;
        for (int day = 1; day < 5000 && g.Phase <= (int)GamePhase.Polish; day++)
        {
            StudioRules.Build(g, c, 1000, day);
            if (StudioRules.Advance(g, day) == 1)
            {
                milestones++;
                if (playtests)
                {
                    StudioRules.ApplyPlaytest(g, StudioRules.OpenBeta, StudioRules.FixAll, 1000, 9, day, out _);
                }
                StudioRules.ApplyMilestone(g, milestones == cutAt ? StudioRules.Cut : StudioRules.Hold, StudioRules.Normal, day);
            }
            days = day;
        }
        return g;
    }

    private static void StudioBuilding()
    {
        StudioCrew team = Crew(5, 2, 1, 1, 60);
        GameDev g = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, team, true, 0, out int days, out int milestones);
        int q = StudioRules.Quality(g);
        Check(g.Phase == (int)GamePhase.Ready && milestones == 2 && g.Milestone == -1 && g.Done == g.Work && q >= 55 && q <= 62,
            "studio: a crew of nine at craft 60 builds a typical indie game through two milestones to ready in " + days + " days, its quality " + q + " near their craft");
        GameDev lean = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.Lean, team, true, 0, out int leanDays, out _);
        GameDev bold = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.Ambitious, team, true, 0, out int boldDays, out _);
        GameDev cut = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, team, true, 1, out int cutDays, out _);
        Check(leanDays < days && boldDays > days && StudioRules.Quality(lean) < q && StudioRules.Quality(bold) > q && cutDays < days && StudioRules.Quality(cut) < q,
            "studio: lean is quicker and weaker (" + leanDays + " days, " + StudioRules.Quality(lean) + "), ambitious slower and better (" + boldDays + ", " + StudioRules.Quality(bold)
            + "), a feature cut at a milestone quicker and it shows (" + cutDays + ", " + StudioRules.Quality(cut) + ")");
        GameDev skilled = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, Crew(5, 2, 1, 1, 85), true, 0, out _, out _);
        GameDev green = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, Crew(5, 2, 1, 1, 35), true, 0, out _, out _);
        Check(StudioRules.Quality(skilled) > q + 15 && StudioRules.Quality(green) < q - 15, "studio: a skilled team makes a far better game, a green one a far worse (" + StudioRules.Quality(skilled)
              + " and " + StudioRules.Quality(green) + ")");
        GameDev untested = BuildAll(ScaleCatalogue.BudgetTypical[0], StudioRules.AsPlanned, Crew(5, 2, 1, 0, 60), false, 0, out _, out _);
        Check(untested.Hidden > g.Hidden + g.Found && StudioRules.BugPenalty(untested) > StudioRules.BugPenalty(g) && StudioRules.BugPenalty(untested) <= StudioCatalogue.BugPenaltyMost,
            "studio: with no playtest and no tester the bugs hide and ship (" + untested.Hidden + " against " + (g.Hidden + g.Found) + "), and the reviews see them");
        // a crunch's days and contractors' come faster
        var draft = new GameDraft { Place = StudioPlace, Budget = 2 };
        GameDev a = StudioRules.Greenlight(draft, 2, null, false, 0), b = StudioRules.Greenlight(draft, 3, null, false, 0);
        b.CrunchUntil = 10;
        b.BoostUntil = 10;
        for (int day = 0; day < 10; day++)
        {
            StudioRules.Build(a, team, 1000, day);
            StudioRules.Build(b, team, 1000, day);
        }
        Check(b.Done * 1000 / Math.Max(1, a.Done) >= 1400 && b.Done * 1000 / Math.Max(1, a.Done) <= 1500,
            "studio: a crunch and contractors together give about " + (StudioCatalogue.CrunchOutput + StudioCatalogue.BoostOutput) / 10 + "% more work a day (" + a.Done + " to " + b.Done + ")");
        // a small team's few points still make bugs, carried
        GameDev small = StudioRules.Greenlight(draft, 4, null, false, 0);
        for (int day = 0; day < 100; day++)
        {
            StudioRules.Build(small, Crew(1, 0, 0, 0, 50), 1000, day);
        }
        Check(small.Hidden == (int)(small.Done * 120 / 2000), "studio: a team of one still makes its share of bugs, nothing lost to rounding (" + small.Hidden + ")");
        // experience: days in a genre lift a person's craft, up to the most
        var f = StudioFileFor(5);
        for (int i = 0; i < 400; i++)
        {
            StudioRules.Learn(f, "pro:1", (int)Genre.Rpg, 8);
        }
        Check(StudioRules.ExperienceDays(f, "pro:1", (int)Genre.Rpg) == 400 && StudioRules.ExperienceDays(f, "pro:1", 0) == 0
              && StudioRules.Craft(60, 1, 400) == StudioRules.Craft(60, 1, 0) + 8 && StudioRules.Craft(60, 1, 99999) == StudioRules.Craft(60, 1, 0) + StudioCatalogue.CraftExperienceMost
              && StudioRules.Craft(100, 4, 99999) <= 100,
            "studio: a person's days in a genre lift their craft in it, a third of the most at a third of a full experience, never past 100");
    }

    // ---------------------------------------------------------------- the sessions' choices

    private static void StudioSessions()
    {
        var d = new GameDraft { Place = StudioPlace, Genre = (int)Genre.Action, Platform = (int)Platform.Console, Budget = 12, Price = 2, Model = (int)GameModel.Premium };
        GameDev g = StudioRules.Greenlight(d, 7, new[] { StudioRules.Ambitious, StudioRules.Publisher, StudioRules.WithDrm }, false, 50);
        Check(g.Scope == (int)Scope.Big && g.Published && g.Advance == g.Budget / 2 && g.Drm && g.Work > g.Plan && g.On(Platform.Console) && g.Spent == StudioCatalogue.EngineFee[2]
              && g.Price == StudioRules.UsualPrice(2, (int)Platform.Pc),
            "studio: a pitch greenlights the draft with its choices: an ambitious big console game, a publisher's half of the budget advanced, DRM, the engine's fee");
        var free = new GameDraft { Place = StudioPlace, Model = (int)GameModel.FreeToPlay, Budget = 3 };
        GameDev f2p = StudioRules.Greenlight(free, 8, new[] { 1, 0, StudioRules.WithDrm }, false, 0);
        Check(f2p.Price == 0 && !f2p.Drm, "studio: a free game has no price and never DRM");
        g.Milestone = 0;
        long work = g.Work;
        StudioRules.ApplyMilestone(g, StudioRules.Cut, StudioRules.Crunch, 100);
        Check(g.Work == work * StudioCatalogue.CutWork / 1000 && g.CrunchUntil == 100 + StudioCatalogue.CrunchDays && g.Milestone == -1,
            "studio: a milestone review cuts a feature and calls a crunch, and the milestone is passed");
        g.Milestone = 1;
        g.MilestoneDay = 200;
        Check(!StudioRules.Lapsed(g, 200 + StudioCatalogue.MilestoneGraceDays - 1) && StudioRules.Lapsed(g, 200 + StudioCatalogue.MilestoneGraceDays),
            "studio: a milestone waits its grace for a review, then the team holds the course");
        g.Hidden = 1000;
        g.Found = 0;
        int found = StudioRules.ApplyPlaytest(g, StudioRules.ClosedBeta, StudioRules.FixAll, 750, 1, 300, out int hype);
        Check(found == 1000 * StudioCatalogue.PlaytestFinds[1] * 750 / 1000000 && g.Found == found && g.Hidden == 1000 - found && g.Fix == StudioCatalogue.FixShares[0] && hype >= StudioCatalogue.BetaHype - 100,
            "studio: a closed beta held by a manager of three quarters' strength finds that share of the hidden bugs, and the fixes chosen set the work on them");
        g.Hype = 100;
        StudioRules.ApplyPlaytest(g, StudioRules.OpenBeta, StudioRules.ShipThem, 1000, 1, 301, out int moved);
        Check(g.Fix == 0 && moved == (StudioRules.Quality(g) * StudioCatalogue.BacklashPerQuality - 100) / 2, "studio: an open beta moves hype half way to what the game carries, and shipping the bugs fixes none");
        g.Phase = (int)GamePhase.Live;
        g.Launched = 400;
        g.Reported = StudioRules.BugRoom(g) / 5;
        g.Asks = 1 << (int)FanAsk.CoOp;
        int reported = g.Reported;
        int fixedNow = StudioRules.ApplyPostLaunch(g, StudioRules.Hotfix, StudioRules.AddOn, StudioRules.RemoveDrm, 1000, 410);
        Check(fixedNow == reported * 400 / 1000 && g.Reported == reported - fixedNow && g.Project == StudioRules.ProjectAddOn && g.ProjectArg == (int)FanAsk.CoOp && g.DrmRemoved,
            "studio: a post-launch review hotfixes the worst of the reported bugs, starts an add-on on the fans' request and takes DRM off");
        Check(StudioRules.PickPlaytest(g, 0) == StudioRules.ClosedBeta && StudioRules.PickPostLaunch(g, 0, 410) == StudioRules.Hotfix && StudioRules.PickPostLaunch(g, 1, 410) == StudioRules.Listen,
            "studio: a manager's sound choices: a closed beta, a hotfix while many bugs are reported, no new project while one is under way");
    }

    // ---------------------------------------------------------------- hype and the review

    private static void StudioHypeAndReview()
    {
        GameDev g = MadeGame(1, Genre.Action, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 60, 0, 0);
        g.Phase = (int)GamePhase.Production;
        g.Done = g.Work / 2;
        Check(StudioRules.BeatWhy(g, StudioRules.BeatAnnounce, 10, 100) == null && StudioRules.BeatWhy(g, StudioRules.BeatPreviews, 10, 100) == "reallife_studio_beat_why_early"
              && StudioRules.BeatWhy(g, StudioRules.BeatExpo, 10, 100) == "reallife_studio_beat_why_expo" && StudioRules.BeatWhy(g, StudioRules.BeatExpo, 10, 500) == null
              && StudioRules.BeatWhy(g, StudioRules.BeatDevlog, 10, 100) == "reallife_studio_beat_why_flare",
            "studio: an announcement any time, previews only with most of it built, the expo only in its weeks, a devlog only with Flare's bridge");
        int by = StudioRules.Beat(g, StudioRules.BeatAnnounce, 0, 10);
        Check(by == StudioCatalogue.HypeAnnounce && StudioRules.BeatWhy(g, StudioRules.BeatAnnounce, 11, 100) == "reallife_studio_beat_why_once" && g.LastNews == 10,
            "studio: an announcement adds hype once");
        StudioRules.Beat(g, StudioRules.BeatTrailer, 0, 12);
        Check(StudioRules.BeatWhy(g, StudioRules.BeatTrailer, 20, 100) == "reallife_studio_beat_why_soon" && g.Spent == g.Budget + StudioCatalogue.TrailerCost[1],
            "studio: a trailer costs its scope's price and another waits its gap");
        int hype = g.Hype;
        StudioRules.HypeDay(g, 12 + StudioCatalogue.HypeQuietDays);
        int same = g.Hype;
        StudioRules.HypeDay(g, 13 + StudioCatalogue.HypeQuietDays);
        Check(same == hype && g.Hype < hype, "studio: hype holds through a quiet spell, then fades without news");
        g.Campaign = 3;
        g.CampaignSpent = 0;
        long spend = StudioRules.HypeDay(g, 200);
        Check(spend == g.Budget * 300 / 1000 / StudioCatalogue.CampaignDays && g.LastNews == 200, "studio: a campaign spends its share of the budget over its days and keeps hype rising");
        // the review
        int e0 = StudioRules.Expect((int)Genre.Rpg, 0, false), e5 = StudioRules.Expect((int)Genre.Rpg, StudioCatalogue.MarketYear * 5, false);
        Check(e5 == e0 + 6 && StudioRules.Expect((int)Genre.Rpg, StudioCatalogue.MarketYear * 100, false) == StudioCatalogue.ExpectMost && StudioRules.Expect(0, 0, true) == StudioRules.Expect(0, 0, false) + 3,
            "studio: the critics' bar rises by the market's years to its most, and asks more of a sequel");
        GameDev r = MadeGame(2, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, 0);
        int s70 = StudioRules.Review(r, 70, 500, 0, 1);
        r.Drm = true;
        int drm = StudioRules.Review(r, 70, 500, 0, 1);
        r.Drm = false;
        r.Hype = 1000;
        int backlash = StudioRules.Review(r, 70, 500, 0, 1);
        Check(drm == s70 - StudioCatalogue.DrmReview && backlash == s70 - StudioRules.Backlash(1000, 70) && StudioRules.Backlash(1000, 70) == 7 && StudioRules.Backlash(600, 70) == 0,
            "studio: DRM costs its points; hype far above what the quality carries brings a backlash, none within it");
        r.Hype = 0;
        Check(StudioRules.Review(r, 95, 500, 0, 1) > s70 && StudioRules.Review(r, 30, 500, 0, 1) < s70 && StudioRules.Review(r, 70, 900, 0, 1) > s70 && StudioRules.Review(r, 100, 1000, 0, 1) <= 100
              && StudioRules.Review(r, 0, 0, 99999, 1) >= 0 && StudioRules.Review(r, 70, 500, 0, 1) == s70,
            "studio: a better game and a better name review higher, always 0 to 100, the same score for the same game on every machine");
    }

    // ---------------------------------------------------------------- the bands

    private static readonly int[] BandQualities = { 30, 45, 58, 70, 80, 90, 100 };

    private static int GameReturn(Scope scope, int quality, int hype, out long units)
    {
        GameDev g = MadeGame(10 + quality, Genre.Adventure, Platform.Pc, ScaleCatalogue.BudgetTypical[(int)scope], quality, hype, StudioDay);
        if (scope == Scope.Big)
        {
            g.Platforms |= 1 << (int)Platform.Console;   // a big game goes out on consoles too: one platform's players are not enough for its budget
        }
        long net = GameLife(g, StudioFileFor(3), StudioDay, StudioCatalogue.MarketYear * 3, out units);
        return ScaleRules.Return(net, g.Budget);
    }

    private static void StudioBands(bool print)
    {
        for (int s = 0; s < (int)Scope.Count; s++)
        {
            var line = new System.Text.StringBuilder();
            int flop = GameReturn((Scope)s, 30, 0, out _), average = GameReturn((Scope)s, ScaleCatalogue.TeamQuality[s], 150, out _), hit = GameReturn((Scope)s, 85, 300, out long hitUnits);
            if (print)
            {
                foreach (int q in BandQualities)
                {
                    int ret = GameReturn((Scope)s, q, 0, out long u), hyped = GameReturn((Scope)s, q, 300, out long uh);
                    ScaleRules.Typical(ScaleCatalogue.BudgetTypical[s], q, out _, out int typical);
                    line.Append(" q" + q + ": " + ret + " (" + u.ToString("N0") + ") hyped " + hyped + " [scale " + typical + "];");
                }
                Console.WriteLine("  studio bands " + (Scope)s + ":" + line);
            }
            Check(ScaleRules.BandOf(flop) == ReturnBand.Flop, "studio: a " + (Scope)s + " game of quality 30 is a flop by the studio's own rules (" + flop + " thousandths of its budget back)");
            Check(ScaleRules.BandOf(average) == ReturnBand.Average || ScaleRules.BandOf(average) == ReturnBand.Good,
                "studio: a " + (Scope)s + " game of its team's usual quality with a little hype earns its budget back and a little more (" + average + ")");
            Check(ScaleRules.BandOf(hit) >= ReturnBand.Hit, "studio: a " + (Scope)s + " game of quality 85 with hype is a hit (" + hit + ", " + hitUnits.ToString("N0") + " copies)");
        }
    }

    // ---------------------------------------------------------------- no cap

    private static void StudioNoCap(bool print)
    {
        // a masterpiece in a hot genre on two platforms: more than the old rules' cap, still selling after years
        StudioFile f = StudioFileFor(4);
        f.TrendGenre = (int)Genre.Shooter;
        f.TrendUntil = StudioDay + 10000;
        GameDev m = MadeGame(1, Genre.Shooter, Platform.Pc, ScaleCatalogue.BudgetMost[2], 98, 600, StudioDay);
        m.Platforms |= 1 << (int)Platform.Console;
        var plat = new long[StudioCatalogue.GamePlatforms];
        StudioRules.Release(m, f, StudioDay, 800, f.Seed, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
        long year1 = 0, year3 = 0;
        for (int d = StudioDay; d < StudioDay + StudioCatalogue.MarketYear * 4; d++)
        {
            long units = StudioRules.SellDay(m, f, d, StudioRules.AppealOf(m.Score, d - m.Launched) + StudioCatalogue.TypicalRivalAppeal, plat, 1000, 1000).Units;
            year1 += d < StudioDay + StudioCatalogue.MarketYear ? units : 0;
            year3 += d >= StudioDay + StudioCatalogue.MarketYear * 3 ? units : 0;
        }
        long reachable = plat[0] + plat[1];   // read now: the flop below fills the same array
        // a flop drops within weeks
        GameDev flop = MadeGame(2, Genre.Puzzle, Platform.Pc, ScaleCatalogue.BudgetTypical[0], 25, 0, StudioDay);
        StudioRules.Release(flop, f, StudioDay, 500, f.Seed, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
        long first = 0, later = 0;
        for (int d = StudioDay; d < StudioDay + 120; d++)
        {
            long units = StudioRules.SellDay(flop, f, d, StudioRules.AppealOf(flop.Score, d - flop.Launched) + StudioCatalogue.TypicalRivalAppeal, plat, 1000, 1000).Units;
            first += d - StudioDay < 7 ? units : 0;
            later += d - StudioDay >= 90 ? units : 0;
        }
        if (print)
        {
            Console.WriteLine("  studio no cap: a masterpiece " + m.Sold.ToString("N0") + " copies in four years (year one " + year1.ToString("N0") + ", year four " + year3.ToString("N0") + "), $"
                              + (m.Net / 100).ToString("N0") + "; a flop's first week " + first + ", its fourth month " + later);
        }
        Check(m.Sold > 50 * 190000L && year3 > 0 && m.Sold < reachable,
            "studio: a masterpiece in a hot genre sells " + m.Sold.ToString("N0") + " copies, far past the old rules' 190 thousand, still selling in its fourth year (" + year3.ToString("N0")
            + "), never more than its audience (" + reachable.ToString("N0") + ")");
        Check(later / 30 * 10 <= first / 7, "studio: a flop's sales a day fall tenfold within weeks (" + first / 7 + " a day in its first week, " + later / 30 + " a day in its fourth month)");
        // the audience grows: the same game made later sells more
        GameDev early = MadeGame(3, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, 0), late = MadeGame(4, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, 0);
        early.Reach = late.Reach = 150;
        long lifeEarly = StudioRules.LifeUnits(early, f, 0, 3000, plat, out _), lifeLate = StudioRules.LifeUnits(late, f, StudioCatalogue.MarketYear * 10, 3000, plat, out _);
        Check(lifeLate > lifeEarly * 13 / 10, "studio: the same game ten years on finds a larger audience (" + lifeEarly.ToString("N0") + " to " + lifeLate.ToString("N0") + "): no cap, the audience grows");
        // one market a genre: strong rivals take sales, an empty genre gives more
        long crowded = StudioRules.LifeUnits(early, f, 0, 1000 + 8000, plat, out _), alone = StudioRules.LifeUnits(early, f, 0, 1000, plat, out _);
        Check(alone > lifeEarly && crowded < lifeEarly, "studio: one market a genre: alone it sells " + alone.ToString("N0") + ", among typical rivals " + lifeEarly.ToString("N0") + ", among strong ones "
              + crowded.ToString("N0"));
    }
}
