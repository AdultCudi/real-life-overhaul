using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using GamePart = RealLifeOverhaul.Tech.Part;

// The game studio's checks, continued (StudioChecks.cs): ten years against the end game, the models and piracy, life after
// launch, bonuses, the rival studios, older saves, the save and the shared city, the screens, the wiring, the texts, the cost.
internal static partial class Program
{
    // ---------------------------------------------------------------- ten years

    private struct StudioTen
    {
        public long Cash, Capital;
        public int Games, RatePpm;
    }

    // A studio making games of a scope back to back for the money scale's ten years (ScaleCatalogue.SimDays game days) with an
    // ordinary team, by the studio's own rules, in a calendar of so many days a year: each budget spent over its making, each
    // game's quality rolled about the team's and a little hype, released into its genre among the typical rivals and sold day by
    // day; a game four years of sales old (by the game day) has stopped selling and is let go. The team is ordinary for its time:
    // as the save's years raise the critics' bar, its craft keeps its standing with them (each game made at the quality that
    // reviews as its roll would have against the first day's bar), as the game's people learn by the hour and are promoted by
    // the save's years (TalentCatalogue.Scaled), so the scale is held whatever the year's length.
    private static StudioTen TenYears(Scope scope, int seed, int year = StudioYear)
    {
        int dev = ScaleCatalogue.DevDays[(int)scope], mean = ScaleCatalogue.TeamQuality[(int)scope], days = ScaleCatalogue.SimDays;
        long budget = ScaleCatalogue.BudgetTypical[(int)scope];
        int games = days / dev;
        StudioFile f = StudioFileFor(seed, year);
        int bar = StudioRules.Expect((int)Genre.Adventure, 0, false, year);
        var live = new List<GameDev>(games);
        var plat = new long[StudioCatalogue.GamePlatforms];
        long cash = 0, low = 0, spend = budget / dev;
        for (int d = 0; d < days; d++)
        {
            if (d < games * dev)
            {
                cash -= spend;
            }
            if (d > 0 && d % dev == 0 && d / dev <= games)
            {
                int n = d / dev - 1;
                int quality = KeptStanding(ScaleRules.QualityRoll(seed, n, mean), bar, StudioRules.Expect((int)Genre.Adventure, d, false, year));
                GameDev g = MadeGame(100 + n, Genre.Adventure, Platform.Pc, budget, quality, TechRandom.Range(seed, n, 5, 0, 0, 300), d);
                if (scope == Scope.Big)
                {
                    g.Platforms |= 1 << (int)Platform.Console;
                }
                StudioRules.Release(g, f, d, 500, seed, 1000 + StudioCatalogue.TypicalRivalAppeal, plat, 0);
                live.Add(g);
            }
            for (int i = live.Count - 1; i >= 0; i--)
            {
                GameDev g = live[i];
                if (d - g.Launched > CalendarRules.MoneyYearDays * 4)
                {
                    live.RemoveAt(i);
                    continue;
                }
                cash += StudioRules.SellDay(g, f, d, StudioRules.AppealOf(g.Score, d - g.Launched) + StudioCatalogue.TypicalRivalAppeal, plat, 1000, 1000).Payout;
            }
            low = Math.Min(low, cash);
        }
        long capital = Math.Max(1L, -low);
        return new StudioTen { Cash = cash, Capital = capital, Games = games, RatePpm = (int)(cash * 1000000L / capital / days) };
    }

    // The quality that reviews against today's bar as a quality reviewed against the first day's (StudioRules.CriticsScore turned round).
    private static int KeptStanding(int quality, int barThen, int barNow)
    {
        int score = StudioRules.CriticsScore(quality, barThen);
        int kept = score >= StudioCatalogue.ReviewBase
            ? barNow + ((score - StudioCatalogue.ReviewBase) * (100 - barNow) + (100 - StudioCatalogue.ReviewBase) / 2) / (100 - StudioCatalogue.ReviewBase)
            : barNow + (score - StudioCatalogue.ReviewBase) * 1000 / StudioCatalogue.ReviewPerQuality;
        return StudioRules.Clamp(kept, 0, 100);
    }

    private static void StudioTenYears(bool print)
    {
        const int Seeds = 16;
        int bandLeast = 1000000 / EndScaleRules.PaybackDaysMost, bandMost = 1000000 / EndScaleRules.PaybackDaysLeast;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (int year in new[] { StudioYear, CalendarRules.YearDefault })
        {
            for (int s = 0; s < (int)Scope.Count; s++)
            {
                var rates = new List<int>(Seeds);
                long sum = 0, day = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    StudioTen r = TenYears((Scope)s, seed * 7919 + s, year);
                    rates.Add(r.RatePpm);
                    sum += r.RatePpm;
                    day += r.Cash / ScaleCatalogue.SimDays / 100;
                }
                rates.Sort();
                int mean = (int)(sum / Seeds), p10 = rates[Seeds / 10], p90 = rates[Seeds * 9 / 10];
                int payback = mean > 0 ? 1000000 / mean : -1;
                if (print)
                {
                    Console.WriteLine("  studio ten years " + (Scope)s + " (years of " + year + " days): mean " + mean + " ppm a day (payback " + payback + " days), p10 " + p10 + ", p90 " + p90
                                      + ", profit a day $" + (day / Seeds).ToString("N0"));
                }
                Check(mean >= bandLeast && mean <= bandMost && ScaleRules.OnTarget(PaybackKind.GameStudio, payback),
                    "studio: a " + (Scope)s + " studio's average day over ten years by the studio's own rules, in years of " + year + " days, returns " + mean + " millionths of its capital, a payback of "
                    + payback + " days, inside the end game's aim of " + EndScaleRules.PaybackDaysLeast + " to " + EndScaleRules.PaybackDaysMost);
                Check(p90 > p10 * 2, "studio: a " + (Scope)s + " studio's luck swings wide over ten years of " + year + " days a year (its tenth " + p10 + ", its ninetieth " + p90 + ")");
            }
        }
        StudioTen a = TenYears(Scope.Mid, 77), b = TenYears(Scope.Mid, 77);
        Check(a.Cash == b.Cash && a.Capital == b.Capital && a.Games == ScaleCatalogue.SimDays / ScaleCatalogue.DevDays[1], "studio: the same seed makes the same ten years on every machine");
        if (print)
        {
            Console.WriteLine("  studio ten years took " + watch.ElapsedMilliseconds + " ms");
        }
    }

    // ---------------------------------------------------------------- piracy and the models

    private static long YearOf(GameDev g, StudioFile f, int day)
    {
        return GameLife(g, f, day, StudioYear, out _);
    }

    private static void StudioPiracyAndModels()
    {
        var plat = new long[StudioCatalogue.GamePlatforms];
        GameDev g = MadeGame(1, Genre.Action, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 60, 0, StudioDay);
        long audience = StudioRules.AudienceOf(g, null, StudioDay, plat);
        g.Launched = StudioDay;
        int open = StudioRules.PirateShare(g, StudioDay, plat, audience);
        g.Drm = true;
        int early = StudioRules.PirateShare(g, StudioDay + 10, plat, audience), cracked = StudioRules.PirateShare(g, StudioDay + StudioCatalogue.DrmDays, plat, audience);
        g.DrmRemoved = true;
        int removed = StudioRules.PirateShare(g, StudioDay + 200, plat, audience);
        g.Model = (int)GameModel.FreeToPlay;
        int free = StudioRules.PirateShare(g, StudioDay, plat, audience);
        Check(open == StudioCatalogue.Piracy[0] && early == open * StudioCatalogue.DrmEarly / 1000 && cracked == open * StudioCatalogue.DrmLater / 1000 && removed == open && free == 0,
            "studio: a fifth of PC buyers pirate a paid game, DRM cuts it to " + early + " in its first days and " + cracked + " once cracked, all of it back when taken off, none for a free game");
        g = MadeGame(2, Genre.Action, Platform.Console, ScaleCatalogue.BudgetTypical[1], 60, 1000, StudioDay);
        audience = StudioRules.AudienceOf(g, null, StudioDay, plat);
        Check(StudioRules.PirateShare(g, StudioDay, plat, audience) == StudioCatalogue.Piracy[1] * 1500 / 1000, "studio: a console game is pirated far less, a hyped one half again more");
        // phones lean free: a free casual game earns more there than a paid one
        GameDev paid = MadeGame(3, Genre.Casual, Platform.Mobile, ScaleCatalogue.BudgetTypical[0], 60, 0, StudioDay);
        GameDev f2p = MadeGame(4, Genre.Casual, Platform.Mobile, ScaleCatalogue.BudgetTypical[0], 60, 0, StudioDay);
        f2p.Model = (int)GameModel.FreeToPlay;
        f2p.Price = 0;
        long paidYear = YearOf(paid, StudioFileFor(1), StudioDay), freeYear = YearOf(f2p, StudioFileFor(1), StudioDay);
        Check(freeYear > paidYear * 2 && f2p.Sold > paid.Sold * 4, "studio: on phones a free game finds many more players and earns more than a paid one (" + freeYear / 100 + " against " + paidYear / 100 + ")");
        // a subscription: subscribers come and go, the month billed by the day; an online genre's game pays servers
        GameDev sub = MadeGame(5, Genre.Shooter, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, StudioDay);
        sub.Model = (int)GameModel.Subscription;
        sub.Price = StudioRules.PriceShown((int)GameModel.Subscription, 2, 1, 0);
        long subYear = YearOf(sub, StudioFileFor(1), StudioDay);
        Check(sub.Subscribers > 0 && sub.Subscribers < sub.Sold && subYear > 0, "studio: a subscription keeps some of its buyers paying each month (" + sub.Subscribers + " of " + sub.Sold + ")");
        GameSales servers = StudioRules.SellDay(sub, StudioFileFor(1), StudioDay + StudioYear, 3000, plat, 1000, 1000);
        GameDev offline = MadeGame(6, Genre.Adventure, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, StudioDay);
        YearOf(offline, StudioFileFor(1), StudioDay);
        Check(servers.Servers == ScaleRules.Servers(sub.Active) / 30 && servers.Servers > 0 && StudioRules.SellDay(offline, StudioFileFor(1), StudioDay + 400, 3000, plat, 1000, 1000).Servers == 0,
            "studio: an online game pays servers for its players still playing; an offline one none");
        // early access: on sale before its full release, the critics wait, then the full release brings its own window
        GameDev ea = MadeGame(7, Genre.Simulation, Platform.Pc, ScaleCatalogue.BudgetTypical[0], 60, 0, StudioDay);
        ea.Model = (int)GameModel.EarlyAccess;
        ea.Phase = (int)GamePhase.Production;
        ea.Done = ea.Work * 700 / 1000;
        Check(StudioRules.CanOpenEarly(ea), "studio: an early access game can open with most of it built");
        StudioFile f = StudioFileFor(2);
        StudioRules.OpenEarly(ea, f, StudioDay, 3000, plat, 0);
        long earlyUnits = 0;
        for (int d = StudioDay; d < StudioDay + 60; d++)
        {
            earlyUnits += StudioRules.SellDay(ea, f, d, 3000, plat, 1000, 1000).Units;
        }
        bool waited = ea.Score < 0 && ea.Early && ea.OnSale && earlyUnits > 0;
        ea.Done = ea.Work;
        ea.Phase = (int)GamePhase.Ready;
        StudioRules.Release(ea, f, StudioDay + 60, 500, 2, 3000, plat, 0);
        Check(waited && ea.Score >= 0 && !ea.Early && ea.WindowStart == StudioDay + 60 && ea.WindowUnits > 0 && ea.Launched == StudioDay,
            "studio: early access sells " + earlyUnits + " copies before the critics' score, and the full release brings a window of its own");
        // a publisher's royalties go to its advance first
        GameDev pub = MadeGame(8, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 60, 0, StudioDay);
        pub.Published = true;
        pub.Advance = pub.Budget * StudioCatalogue.AdvanceShare / 1000;
        StudioRules.Release(pub, f, StudioDay, 500, 3, 3000, plat, 0);
        GameSales first = StudioRules.SellDay(pub, f, StudioDay, 3000, plat, 1000, 1000);
        Check(first.Net > 0 && first.Payout == 0 && pub.Royalties == first.Net * StudioCatalogue.Royalty / 1000, "studio: a published game's royalties earn back its advance before the studio sees any");
        // a licensed engine's royalty only past its free first million
        GameDev small = MadeGame(9, Genre.Puzzle, Platform.Pc, ScaleCatalogue.BudgetTypical[0], 60, 0, StudioDay);
        StudioRules.Release(small, f, StudioDay, 500, 4, 3000, plat, 0);
        Check(StudioRules.SellDay(small, f, StudioDay, 3000, plat, 1000, 1000).Royalty == 0, "studio: a licensed engine takes nothing of a game's first million");
        small.RoyaltyGross = ScaleCatalogue.EngineRoyaltyFree;
        GameSales past = StudioRules.SellDay(small, f, StudioDay + 1, 3000, plat, 1000, 1000);
        Check(past.Royalty == past.Gross * ScaleCatalogue.EngineRoyalty / 1000, "studio: past it, the engine takes its share of every sale");
        Check(StudioRules.Season(830, 1000) == StudioCatalogue.SeasonHoliday && StudioRules.Season(500, 1000) == StudioCatalogue.SeasonSummer && StudioRules.Season(100, 1000) == 1000
              && StudioRules.Season(55, 60) == StudioCatalogue.SeasonHoliday,
            "studio: the year's end brings buyers and summer takes some, by the calendar's own year");
        GameDev sale = MadeGame(10, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, StudioDay);
        StudioRules.Release(sale, f, StudioDay, 500, 5, 3000, plat, 0);
        sale.WindowStart = -1;
        GameSales plain = StudioRules.SellDay(sale, f, StudioDay + 100, 3000, plat, 1000, 1000);
        sale.SaleUntil = StudioDay + 200;
        GameSales cheap = StudioRules.SellDay(sale, f, StudioDay + 101, 3000, plat, 1000, 1000);
        Check(cheap.Units > plain.Units * 3 / 2 && cheap.Gross * plain.Units < plain.Gross * cheap.Units, "studio: a sale brings buyers forward at a lower price");
    }

    // ---------------------------------------------------------------- after launch

    private static void StudioAfterLaunch()
    {
        var plat = new long[StudioCatalogue.GamePlatforms];
        StudioFile f = StudioFileFor(6);
        GameDev g = MadeGame(1, Genre.Rpg, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 72, 0, StudioDay);
        g.Hidden = 400;
        StudioRules.Release(g, f, StudioDay, 500, 6, 3000, plat, 0);
        int reported = StudioRules.ReportDay(g);
        Check(reported == 20 && g.Reported == 20 && g.Hidden == 380 && g.Asks != 0, "studio: after launch players report a twentieth of the hidden bugs a day, and the fans ask for things");
        g.Hidden = 1;
        StudioRules.ReportDay(g);
        Check(g.Hidden == 0 && g.Reported == 21, "studio: the last hidden bug is reported too");
        StudioCrew team = Crew(5, 2, 1, 1, 60);
        g.Fix = StudioCatalogue.FixShares[StudioRules.FixAll];
        StudioRules.AfterLaunch(g, team, 1000, StudioDay + 1);
        Check(g.Reported < 21, "studio: a full patch's work fixes the reported bugs");
        for (int d = 0; d < 20 && g.Reported > 0; d++)
        {
            StudioRules.AfterLaunch(g, team, 1000, StudioDay + 2 + d);
        }
        Check(g.Reported == 0 && g.Fix == StudioCatalogue.FixShareBuild, "studio: with every bug fixed the patch is out and the work goes back to its usual share");
        // projects: an update, an add-on, a port, localisation
        Check(StudioRules.ProjectWhy(g, StudioRules.ProjectPort, (int)Platform.Pc) == "reallife_studio_project_why_platform" && StudioRules.ProjectWhy(g, StudioRules.ProjectPort, 1) == null,
            "studio: a game can be ported only to a platform it is not on");
        StudioRules.StartProject(g, StudioRules.ProjectUpdate, StudioRules.FirstAsk(g));
        Check(StudioRules.ProjectWhy(g, StudioRules.ProjectLocalise, 0) == "reallife_studio_project_why_busy" && g.ProjectWork == g.Work * StudioCatalogue.UpdateWork[0] / 1000,
            "studio: one project at a time, an update a share of the game's work");
        int released = 0;
        for (int d = 0; d < 1000 && released == 0; d++)
        {
            released = StudioRules.AfterLaunch(g, team, 1000, StudioDay + 30 + d);
        }
        long before = StudioRules.LifeUnits(g, f, StudioDay + 200, 3000, plat, out _);
        long more = StudioRules.Released(g, StudioRules.ProjectUpdate, f, StudioDay + 200, 3000, plat, 6);
        Check(released == StudioRules.ProjectUpdate && g.Updates == 1 && g.Lift == 1000 + StudioCatalogue.UpdateLift && more > 0 && g.WindowStart == StudioDay + 200
              && StudioRules.LifeUnits(g, f, StudioDay + 200, 3000, plat, out _) > before,
            "studio: a free update, built, lifts the life's sales and opens a window of " + more + " copies");
        g.Sold = 100000;
        StudioRules.Released(g, StudioRules.ProjectAddOn, f, StudioDay + 300, 3000, plat, 6);
        long addOn = 0;
        for (int d = StudioDay + 300; d < StudioDay + 330; d++)
        {
            addOn += StudioRules.SellDay(g, f, d, 3000, plat, 1000, 1000).AddOnUnits;
        }
        Check(g.AddOns == 1 && g.AddOnWindow == 100000L * (StudioCatalogue.AttachLeast + StudioCatalogue.AttachPerScore * g.Score) / 1000 && addOn > g.AddOnWindow * 9 / 10,
            "studio: an add-on sells to the game's owners by its score over its window (" + addOn + " of " + g.AddOnWindow + ")");
        g.ProjectArg = (int)Platform.Console;
        long port = StudioRules.Released(g, StudioRules.ProjectPort, f, StudioDay + 400, 3000, plat, 6);
        Check(g.On(Platform.Console) && port > 0, "studio: a port opens a new platform's store (" + port + " more copies)");
        long local = StudioRules.Released(g, StudioRules.ProjectLocalise, f, StudioDay + 500, 3000, plat, 6);
        Check(g.Localised && local > 0 && StudioRules.ProjectWhy(g, StudioRules.ProjectLocalise, 0) == "reallife_studio_project_why_done", "studio: localisation reaches the world's own languages, once");
        // sequels: the fans carry over; a weak one burns the name
        g.Score = 90;
        long fans = StudioRules.FansOf(g);
        Check(fans == g.Sold * StudioCatalogue.FansPerScore * 90 / 1000, "studio: a game's fans for a sequel by its score (" + fans + ")");
        var sequel = new GameDev { Score = 70, Sequel = g.Id };
        Check(StudioRules.Burns(sequel, g) && !StudioRules.Burns(new GameDev { Score = 80 }, g) && !StudioRules.Burns(new GameDev { Score = 50 }, new GameDev { Score = -1 }),
            "studio: a sequel far under the first burns the name, a close one does not");
        g.Burnt = true;
        Check(StudioRules.FansOf(g) == fans / 2, "studio: a burnt name's fans halve");
        // the year's award
        StudioFile year = StudioFileFor(8);
        year.Games.Add(new GameDev { Id = 1, Score = 88, Launched = 100 });
        year.Rivals.Add(new RivalGame { Id = 5, Score = 91, Launched = 120 });
        int none = StudioRules.Award(year, 30, 60, true, out _);
        int won = StudioRules.Award(year, 70, 60, true, out bool rival);
        int again = StudioRules.Award(year, 80, 60, true, out _);
        Check(none == -1 && won == 5 && rival && year.Rivals[0].Award && again == -1, "studio: the year's award goes to the best game of the year, a rival's here, once a year");
        year.AwardYear = 1;
        year.Games[0].Award = false;
        year.Rivals[0].Award = true;
        int mine = StudioRules.Award(year, 125, 60, false, out bool rivalAgain);
        Check(mine == 1 && !rivalAgain && year.Games[0].Award, "studio: the next year the player's game wins it, the rival already crowned");
    }

    // ---------------------------------------------------------------- bonuses and the rivals' pull

    private static void StudioBonuses()
    {
        var g = new GameDev { Launched = 100, Phase = (int)GamePhase.Live, Score = 70, Net = 3000000, Spent = 1000000, Bonus = 2 };
        Check(!StudioRules.BonusDue(g, 100 + StudioCatalogue.BonusDay - 1) && StudioRules.BonusDue(g, 100 + StudioCatalogue.BonusDay) && StudioRules.BonusPay(g) == 2000000 * 100 / 1000,
            "studio: on a release's bonus day its pool pays its share of the profit");
        g.Net = 500000;
        Check(StudioRules.BonusPay(g) == 0, "studio: no bonus from a loss");
        g.Score = 86;
        Check(StudioRules.Hit(g), "studio: a score of a trend counts as a hit");
        g.Score = 60;
        g.Net = g.Spent * 3;
        Check(StudioRules.Hit(g) && !StudioRules.Hit(new GameDev { Score = 60, Net = 1, Spent = 100 }), "studio: three times its cost back counts as a hit");
        StudioFile f = StudioFileFor(1);
        f.SnubUntil[StudioPlace] = 500;
        Check(StudioRules.Pull(f, StudioPlace, 499) == StudioCatalogue.SnubPull && StudioRules.Pull(f, StudioPlace, 500) == 1000 && StudioRules.Pull(f, "Other~2", 100) == 1000,
            "studio: rivals pull hard at a team whose hit paid no bonus, for a while, and only there");
        var p = new Profile { Id = "pro:9", Level = TalentCatalogue.Senior, Morale = 500 };
        int calm = 0, pulled = 0;
        for (int day = 1; day <= 20000; day++)
        {
            calm += TalentRules.Approached(3, p, day, 1000, 0, 1000, 1000) ? 1 : 0;
            pulled += TalentRules.Approached(3, p, day, 1000, 0, 1000, 1000, StudioCatalogue.SnubPull) ? 1 : 0;
        }
        Check(pulled > calm * 2 && calm > 0 && TalentRules.Approached(3, p, 7, 1000, 0, 1000, 1000) == TalentRules.Approached(3, p, 7, 1000, 0, 1000, 1000, 1000),
            "studio: the people part's rivals come for them about three times as often under that pull (" + calm + " to " + pulled + " approaches in 20,000 days), as before without it");
        Check(StudioRules.TicketToll(0, 0) == 0 && StudioRules.TicketToll(100, 100) == 0 && StudioRules.TicketToll(100, 0) == 5 && StudioRules.TicketToll(100, 90) == 1,
            "studio: support tickets left unanswered cost the studio's name a little each day, none when all are answered");
        var leads = new GameDev { Launched = 100, Leads = 2, NetMonth = 1000000 };
        Check(StudioRules.LeadsPay(leads, 120) == 0 && StudioRules.LeadsPay(leads, 130) == 20000 && leads.NetMonth == 0 && StudioRules.LeadsPay(leads, 140) == 0,
            "studio: the leads' royalty pays its share of the month's net once a month");
    }

    // ---------------------------------------------------------------- the rival studios

    private static StudioFile RivalRun(int seed, int days)
    {
        StudioFile f = StudioFileFor(seed);
        var released = new List<RivalGame>(4);
        for (int d = 1; d <= days; d++)
        {
            StudioRules.RivalDay(f, d, released);
        }
        return f;
    }

    private static void StudioRivals()
    {
        StudioFile a = RivalRun(11, StudioYear * 3), b = RivalRun(11, StudioYear * 3), c = RivalRun(12, StudioYear * 3);
        Check(StudioShare.Write(a) == StudioShare.Write(b) && StudioShare.Write(a) != StudioShare.Write(c), "studio: the rival studios' market is the same for the same seed on every machine, another for another");
        bool sane = a.Rivals.Count > 0 && a.Rivals.Count <= StudioCatalogue.RivalGamesKept;
        foreach (RivalGame r in a.Rivals)
        {
            sane &= r.Sold <= r.Units && r.Score >= 0 && r.Score <= 100 && r.Name.Length > 3 && r.Platforms != 0;
        }
        StudioFile year = RivalRun(11, StudioYear);
        Check(sane && year.NextRival - 1 >= StudioCatalogue.RivalNames.Length && year.NextRival - 1 <= StudioCatalogue.RivalNames.Length * 3,
            "studio: each rival studio releases a game or a few a market year (" + (year.NextRival - 1) + " in the first), each selling no more than its life");
        StudioFile ten = RivalRun(13, StudioYear * 10);
        Check(ten.Rivals.Count == StudioCatalogue.RivalGamesKept && ten.Rivals[0].Launched <= ten.Rivals[ten.Rivals.Count - 1].Launched, "studio: only the newest rival games are kept, in order");
        // a hit starts a trend
        StudioFile f = StudioFileFor(14);
        var released = new List<RivalGame>(4);
        RivalGame hit = null;
        int hitDay = 0;
        for (int d = 1; d <= StudioYear * 30 && hit == null; d++)
        {
            StudioRules.RivalDay(f, d, released);
            foreach (RivalGame r in released)
            {
                if (r.Score >= StudioCatalogue.HitScore)
                {
                    hit = r;
                    hitDay = d;
                }
            }
        }
        Check(hit != null && f.TrendGenre == hit.Genre && f.TrendUntil == hitDay + StudioCatalogue.TrendDays && StudioRules.Trend(f, hit.Genre, hitDay) == 1000 + StudioCatalogue.TrendLift
              && StudioRules.Trend(f, hit.Genre, f.TrendUntil) == 1000 && StudioRules.Poacher(f, hitDay) == hit.Studio,
            "studio: a rival's hit makes its genre hot for a while, and its studio comes for the player's people");
        Check(StudioCatalogue.RivalStock[0] == FirmsCatalogue.RivalStock(ProductKind.Game, Segment.Consumer) && StudioCatalogue.RivalNames[0] == FirmsCatalogue.RivalName(110),
            "studio: the market's listed game company, Pixel Harbour Games, is the first rival studio and moves with its releases");
        var g = new RivalGame { Units = 100000, Score = 60, Launched = 0 };
        long window = 0;
        for (int d = 0; d < StudioCatalogue.WindowDays; d++)
        {
            window += StudioRules.RivalUnitsDay(g, d);
        }
        long launchShare = 100000L * 1000 / ScaleRules.ByQuality(ScaleCatalogue.Tail, 60);
        Check(window <= launchShare && window > launchShare - 40, "studio: a rival game's window sells its launch share of its life, front loaded (" + window + " of " + launchShare + ")");
        var near = new List<RivalGame>();
        int crowd = StudioRules.Crowd(a, a.Rivals[0].Genre, a.Rivals[0].Launched, 0, near);
        Check(crowd >= 1 && near.Count == crowd && near.Contains(a.Rivals[0]), "studio: the rival games of a genre around a day are counted for the release date's page");
    }

    // ---------------------------------------------------------------- older saves

    private static void StudioConversion()
    {
        int day = 300;
        var live = new Product
        {
            Id = 7, Place = StudioPlace, Name = "Dragon Saga", Kind = (int)ProductKind.Game, Stage = 1, Scope = 1, Users = 12000, Revenue = 2000000, Launched = day - 20, Hype = 800,
            Marketing = 2, Progress = FirmsCatalogue.GameScope[1], Bugs = 30
        };
        double daily = StudioRules.OldDaily(1200, live.Hype, FirmsCatalogue.MarketingReach[2], 1000, 20);
        GameDev g = StudioRules.Convert(live, 800, (long)daily, 5);
        bool kept = g.Converted && g.Genre == (int)Genre.Rpg && g.Sold == 12000 && g.Net == 2000000 && g.Launched == day - 20 && g.Score == StudioRules.OldScore(800) && g.OnSale;
        var plat = new long[StudioCatalogue.GamePlatforms];
        long left = g.Left;
        long next = StudioRules.SellDay(g, StudioFileFor(5), day, 3000, plat, 1000, 1000).Units;
        Check(kept && Math.Abs(next - (long)daily) <= Math.Max(2L, (long)daily / 7),
            "studio: an older save's game on sale keeps its copies, takings and day, gets a genre from its name, and sells on from its old day's copies (" + next + " against " + (int)daily + "): nothing jumps");
        for (int d = day + 1; d < day + 400; d++)
        {
            StudioRules.SellDay(g, StudioFileFor(5), d, 3000, plat, 1000, 1000);
        }
        Check(g.Left < left / 100 + 1 && g.Sold - 12000 <= left, "studio: it runs out on its old half-life, never selling more than it had left");
        var half = new Product { Id = 8, Place = StudioPlace, Name = "Brightdesk", Kind = (int)ProductKind.Game, Stage = 0, Scope = 1, Progress = FirmsCatalogue.GameScope[1] / 2, Bugs = 12, Hype = 300 };
        GameDev h = StudioRules.Convert(half, 700, 0, 5);
        Check(h.Work == FirmsCatalogue.GameScope[1] && StudioRules.Progress(h) == 500 && h.Phase == (int)GamePhase.Production && h.Hidden == 12 && h.Hype == 150 && !h.Converted && h.Launched == -1,
            "studio: a half built game stays half built at its old size, in production, its bugs and hype kept");
        var gone = new Product { Id = 9, Place = StudioPlace, Name = "Old", Kind = (int)ProductKind.Game, Stage = 2, Scope = 0, Users = 50 };
        Check(StudioRules.Convert(gone, 500, 0, 5).Phase == (int)GamePhase.Ended, "studio: a withdrawn game is over");
        int rolled = StudioRules.GenreOfName("Brightdesk", 8, 5);
        Check(rolled == StudioRules.GenreOfName("Brightdesk", 8, 5) && rolled >= 0 && rolled < (int)Genre.Count && StudioRules.GenreOfName("Night Fear", 1, 5) == (int)Genre.Horror,
            "studio: a name with no hint gets a genre rolled from its id, the same every time");
        Check(StudioRules.OldScore(300) == 40 && StudioRules.OldScore(1000) == 90, "studio: an older game's quality reads as a score of 40 to 90");
    }

    // ---------------------------------------------------------------- the save and the shared city

    private static StudioFile RichStudio()
    {
        StudioFile f = RivalRun(21, 800);
        f.FirstDay = 3;
        f.LastDay = 800;
        f.Converted = true;
        f.AwardYear = 4;
        for (int i = 0; i < 3; i++)
        {
            GameDev g = MadeGame(50 + i, (Genre)(i * 4), (Platform)i, ScaleCatalogue.BudgetTypical[i], 60 + i, 100 * i, 500);
            g.Hidden = 11 + i;
            g.Found = 3;
            g.BugCarry = 77;
            g.Milestone = i == 0 ? 1 : -1;
            g.Launched = i == 2 ? 600 : -1;
            g.Score = i == 2 ? 81 : -1;
            g.Sold = 123456789L * i;
            g.Gross = 99999999999L;
            g.Net = 88888888888L;
            g.Asks = 5;
            g.Project = StudioRules.ProjectPort;
            g.ProjectArg = 1;
            g.ProjectWork = 999;
            g.Localised = i == 1;
            g.Advance = 123;
            g.Bonus = 1;
            g.Leads = 2;
            g.LastLeadsPay = 610;
            g.Pledged = 42;
            g.Lift = 1400;
            f.Games.Add(g);
        }
        f.Drafts.Add(new GameDraft { Place = StudioPlace, Genre = 5, Setting = 2, Platform = 1, Budget = 9, Price = 3, Model = 2, Engine = 1, Sequel = 52, Name = "Copper Crown" });
        var x = new GenreExperience { Id = "pro:3" };
        x.Days[2] = 400;
        x.Days[11] = 17;
        f.People.Add(x);
        f.Engines.Add(new StudioEngine { Place = StudioPlace, Done = 4000, Building = true });
        f.SnubUntil["Other~4"] = 900;
        return f;
    }

    private static void StudioSave()
    {
        StudioFile f = RichStudio();
        string line = StudioStore.Write(f);
        StudioFile back = StudioStore.Read(line, out int version);
        Check(version == StudioStore.Version && StudioStore.Write(back) == line && back.Games.Count == 3 && back.Games[2].Sold == 246913578L && back.Games[0].Milestone == 1
              && back.Drafts[0].Name == "Copper Crown" && back.People[0].Days[2] == 400 && back.Engines[0].Building && back.SnubUntil["Other~4"] == 900 && back.Rivals.Count == f.Rivals.Count
              && back.RivalNext[3] == f.RivalNext[3] && back.Games[1].Lift == 1400 && back.Games[2].Gross == 99999999999L,
            "studio: the save's line reads back exactly, every field of every record");
        StudioFile thin = StudioStore.Read("v=1~h:5|7~g:9|" + StudioPlace.Replace("~", "%7E") + "|2", out _);
        Check(thin.Seed == 5 && thin.FirstDay == 7 && thin.LastDay == -1 && thin.AwardYear == -1 && thin.Games.Count == 1 && thin.Games[0].Genre == 2 && thin.Games[0].Launched == -1
              && thin.Games[0].Score == -1 && thin.Games[0].Lift == 1000 && thin.Games[0].Milestone == -1 && thin.Games[0].WindowStart == -1,
            "studio: a record with fewer fields reads the rest as they start");
        var data = new Dictionary<string, string> { { StudioStore.Key, "not a line" } };
        Check(StudioStore.Load(data, out int bad) == null && bad <= 0 && StudioStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0,
            "studio: a line that cannot be read gives nothing (kept as it was), and a save with none loads");
        // the shared city's part: the host's rivals on a guest, never the player's own games
        StudioFile guest = StudioFileFor(99);
        guest.Games.Add(new GameDev { Id = 1 });
        string part = StudioShare.Write(f);
        Check(StudioShare.Apply(guest, part) && StudioShare.Write(guest) == part && guest.Games.Count == 1 && guest.Seed == 99,
            "studio: a Going Public guest takes the host's rival market whole, its own games and seed its own");
        string held = StudioShare.Write(guest);
        Check(!StudioShare.Apply(guest, "v=2~h:1") && !StudioShare.Apply(guest, "garbage") && StudioShare.Write(guest) == held && StudioShare.Part == 'V',
            "studio: a part of another version or broken changes nothing (an older RLO never applies a newer one's)");
        // a large studio's line stays small
        StudioFile big = RichStudio();
        for (int i = 0; i < StudioCatalogue.GamesStored - 3; i++)
        {
            big.Games.Add(MadeGame(100 + i, Genre.Action, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 60, 0, 500));
        }
        for (int i = 0; i < 600; i++)
        {
            big.People.Add(new GenreExperience { Id = "pro:" + (1000 + i) });
        }
        string bigLine = StudioStore.Write(big);
        Check(bigLine.Length < 200000 && StudioStore.Read(bigLine, out _).Games.Count == StudioCatalogue.GamesStored, "studio: a full studio's line is " + bigLine.Length + " characters, a small part of a save");
    }

    // ---------------------------------------------------------------- the screens

    private static readonly Regex StudioKey = new Regex(@"^reallife_[a-z0-9_]+$|^common_[a-z0-9_]+$", RegexOptions.Compiled);

    // Every key of a screen in en.json, at most one primary action, something to do next, few sections and lines.
    private static bool ScreenFine(StudioScreen v, Dictionary<string, string> en, out string why)
    {
        why = null;
        var keys = new List<string> { v.TitleKey };
        if (v.ChipKey != null)
        {
            keys.Add(v.ChipKey);
        }
        if (v.Next == null)
        {
            why = "nothing to do next";
            return false;
        }
        keys.Add(v.Next.Key);
        if (v.Primary != null)
        {
            keys.Add(v.Primary.Key);
        }
        foreach (ViewAction a in v.Others)
        {
            keys.Add(a.Key);
        }
        if (v.Sections.Count > 6)
        {
            why = v.Sections.Count + " sections";
            return false;
        }
        foreach (ViewSection s in v.Sections)
        {
            keys.Add(s.TitleKey);
            if (s.Lines.Count + s.Choices.Count > 8)
            {
                why = s.TitleKey + " holds " + (s.Lines.Count + s.Choices.Count) + " things";
                return false;
            }
            foreach (ViewLine l in s.Lines)
            {
                keys.Add(l.Key);
            }
            foreach (ViewChoice c in s.Choices)
            {
                keys.Add(c.LabelKey);
                if (c.Trade != null)
                {
                    keys.Add(c.Trade.Key);
                }
                if (c.Go != null)
                {
                    keys.Add(c.Go.Key);
                }
                if (c.Picked < 0 || c.Picked >= c.Options.Count || c.Values.Count != 0 && c.Values.Count != c.Options.Count)
                {
                    why = c.LabelKey + " picks " + c.Picked + " of " + c.Options.Count;
                    return false;
                }
            }
        }
        foreach (string k in keys)
        {
            if (k == null || !StudioKey.IsMatch(k) || !k.StartsWith("common_", StringComparison.Ordinal) && !en.ContainsKey(k))
            {
                why = "no text for " + k;
                return false;
            }
        }
        return true;
    }

    private static void StudioScreens()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var words = new PlainWords();
        StudioFile f = RichStudio();
        var place = new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", PointsADay = 80, People = 10 };
        bool all = true;
        string why = null;
        for (int g = 0; g < (int)Genre.Count && all; g++)
        {
            for (int p = 0; p < StudioCatalogue.GamePlatforms && all; p++)
            {
                for (int m = 0; m < (int)GameModel.Count && all; m++)
                {
                    var d = new GameDraft { Place = StudioPlace, Genre = g, Platform = p, Model = m, Engine = m % 2, Budget = (g + p) % 15, Price = m, Sequel = m == 3 ? 52 : 0 };
                    StudioScreen v = StudioViews.Concept(f, d, place, 500, words);
                    all &= ScreenFine(v, en, out why) && v.Primary != null && v.Primary.Act == StudioAct.Pitch && v.Sections[0].Choices[0].Options.Count == (int)Genre.Count;
                }
            }
        }
        Check(all, "studio screens: the concept for every genre, platform and model has its texts, something to do next and the desk as its one primary action (" + why + ")");
        StudioScreen full = StudioViews.Concept(f, f.Drafts[0], new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Room = false }, 500, words);
        Check(!full.Primary.Enabled && full.Next.Key == "reallife_studio_next_full", "studio screens: a studio with no room says so and cannot go to the desk for a pitch");
        StudioScreen noDesks = StudioViews.Concept(f, f.Drafts[0], new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Desks = false }, 500, words);
        Check(ScreenFine(noDesks, en, out why) && noDesks.Primary.Act == StudioAct.Greenlight && noDesks.Next.Key == "reallife_studio_next_greenlight",
            "studio screens: with desk sessions off the concept is greenlit on its page, said so (" + why + ")");
        // a game in every state
        GameDev game = f.Games[0];
        var states = new List<string>();
        foreach (int phase in new[] { 0, 1, 2, 3, 4, 5 })
        {
            game.Phase = phase;
            game.Launched = phase >= 4 ? 450 : -1;
            game.Score = phase >= 4 ? 75 : -1;
            game.Milestone = phase == 1 ? 0 : -1;
            game.Reported = phase == 4 ? 5000 : 0;
            game.CertUntil = 0;
            StudioScreen v = StudioViews.Game(f, game, "Copper Crown", place, 1000, 100, 500, 365, phase, phase, words);
            all &= ScreenFine(v, en, out why);
            states.Add(phase + ":" + (v.Primary != null ? v.Primary.Act.ToString() : "-") + ":" + v.Next.Key.Replace("reallife_studio_next_", string.Empty));
        }
        string seen = string.Join(" ", states);
        Check(all && seen.Contains("1:" + StudioAct.Desk + ":milestone") && seen.Contains("3:" + StudioAct.LaunchNow + ":ready") && seen.Contains("4:" + StudioAct.Desk + ":postlaunch")
              && seen.Contains("5:-:ended"),
            "studio screens: a game's page in every state says what to do next with the one action that does it (" + seen + ", " + why + ")");
        game.Phase = (int)GamePhase.Production;
        game.Milestone = -1;
        game.Launched = -1;
        StudioScreen nobody = StudioViews.Game(f, game, "Copper Crown", new StudioPlace { Place = StudioPlace, Name = "Kestrel Works" }, 1000, 100, 500, 365, 0, 0, words);
        Check(nobody.Next.Key == "reallife_studio_next_nobody" && nobody.Next.Tone == ViewTone.Bad, "studio screens: nobody building a game is said plainly, in red");
        StudioScreen studio = StudioViews.Studio(f, place, 900, words);
        StudioScreen empty = StudioViews.Studio(StudioFileFor(1), place, 0, words);
        bool emptyWords = false;
        foreach (ViewSection s in empty.Sections)
        {
            foreach (ViewLine l in s.Lines)
            {
                emptyWords |= l.Key == "reallife_studio_studio_rivals_none";
            }
        }
        StudioScreen busy = StudioViews.Studio(f, new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Tickets = 300, Answered = 60 }, 900, words);
        bool support = false;
        foreach (ViewLine l in busy.Sections[0].Lines)
        {
            support |= l.Key == "reallife_studio_studio_support" && l.Tone == ViewTone.Warn;
        }
        Check(support && ScreenFine(busy, en, out why), "studio screens: the studio's page says how many of its players' tickets go unanswered (" + why + ")");
        bool studioFine = ScreenFine(studio, en, out why), emptyFine = ScreenFine(empty, en, out string why2);
        Check(studioFine && emptyFine && emptyWords && studio.Primary.Act == StudioAct.Concept,
            "studio screens: the studio's part of its firm's page, with words where there is nothing yet (" + why + why2 + ")");
        // The studio's whole page for the mod's design system (the owner, 1 Nov: fill the page with what the Tech app knows).
        var reading = new FirmReading();
        for (int i = 0; i < reading.Takings.Length; i++)
        {
            reading.Takings[i] = 100000 + i * 5000;
        }
        reading.Add(TalentCatalogue.Role(0), 60);
        reading.Add(TalentCatalogue.Role(0), 70);
        reading.Add(TalentCatalogue.Role(18), 50);
        var firms = new FirmsFile();
        int mine = 0;
        foreach (GameDev g in f.Games)
        {
            mine += g.Place == StudioPlace ? 1 : 0;
            firms.Products.Add(new Product { Id = g.Id, Place = g.Place, Name = "Game " + g.Id });
        }
        FirmPage page = StudioViews.Page(f, firms, new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Tickets = 300, Answered = 0 }, reading, 900, words);
        bool lacks = false, engineers = false;
        foreach (ViewRole r in page.Team)
        {
            lacks |= r.Group == TeamGroup.Support && r.Needed && r.Count == 0;
            engineers |= r.Group == TeamGroup.Engineers && r.Count == 2 && r.Skill == 65;
        }
        ViewFigure takings = page.Figures.Count == 3 ? page.Figures[2] : null;
        Check(takings != null && takings.Wide && takings.Trend.Length == FirmsCatalogue.MonthDays - StudioViews.WeekDays + 1 && takings.Trend[takings.Trend.Length - 1] > takings.Trend[0]
              && page.Main.Rows.Count == mine && page.Main.Rows.Count > 0 && page.More.Rows.Count == f.Rivals.Count && lacks && engineers && page.Facts.Count == 7 && page.Screen.Primary != null,
            "studio pages: three figures with a week's takings for each day in a wide one, a row for each of the studio's games, the market's newest, the team with the support it lacks while players write in");
        Check(PageWords(page, out string missing), "studio pages: every word the page uses is written in English, Portuguese and Chinese (" + missing + ")");
    }

    // Every key a firm's page uses (its labels, its columns, its words for nothing, and the words its cells are written
    // with, which the plain formatter leaves as their keys) has its text in en, pt and zh-cn.
    private static bool PageWords(FirmPage page, out string missing)
    {
        var keys = new HashSet<string> { page.TeamKey, page.FactsKey, StudioViews.GroupCountKey, StudioViews.NeededKey };
        foreach (ViewFigure fig in page.Figures)
        {
            keys.Add(fig.LabelKey);
        }
        foreach (ViewTable t in new[] { page.Main, page.More })
        {
            keys.UnionWith(t.ColumnKeys);
            keys.Add(t.EmptyKey);
            foreach (string[] row in t.Rows)
            {
                foreach (string cell in row)
                {
                    if (cell != null && cell.StartsWith("reallife_", StringComparison.Ordinal))
                    {
                        keys.Add(cell);
                    }
                }
            }
        }
        foreach (ViewRole r in page.Team)
        {
            keys.Add(StudioViews.GroupKey(r.Group));
        }
        foreach (ViewPair pair in page.Facts)
        {
            keys.Add(pair.LabelKey);
            foreach (string part in pair.Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("reallife_", StringComparison.Ordinal))
                {
                    keys.Add(part);
                }
            }
        }
        var gaps = new List<string>();
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> texts = LocaleFile(lang);
            foreach (string key in keys)
            {
                if (!string.IsNullOrEmpty(key) && !texts.ContainsKey(key))
                {
                    gaps.Add(lang + ":" + key);
                }
            }
        }
        missing = gaps.Count == 0 ? "all " + keys.Count : string.Join(", ", gaps.GetRange(0, Math.Min(6, gaps.Count)));
        return gaps.Count == 0;
    }

    // ---------------------------------------------------------------- the wiring and the sources

    private static void StudioWiring()
    {
        string rules = ModSource(@"Tech\Studio\StudioRules.cs") + ModSource(@"Tech\Studio\StudioRules.Sales.cs") + ModSource(@"Tech\Studio\StudioRules.Rivals.cs")
                       + ModSource(@"Tech\Studio\StudioViews.cs") + ModSource(@"Tech\Studio\StudioStore.cs");
        Check(rules.Length > 1000 && !rules.Contains("UnityEngine") && !rules.Contains("BuildingRegistration") && !rules.Contains("new Random(") && !rules.Contains("DateTime"),
            "studio sources: the rules, the screens and the store are game-free and roll nothing unseeded");
        string firms = ModSource(@"Tech\TechDirector.Firms.cs"), studio = ModSource(@"Tech\TechDirector.Studio.cs");
        Check(firms.Contains("StudioRuns(file.K) && p.K == ProductKind.Game") && firms.Contains("StudioFirm(firm, day, cycle") && studio.Contains("Settings.LivingPopulation && Settings.LivingStudio")
              && studio.Contains("kind == FirmKind.Games"),
            "studio sources: only with the master option and the studio's own on does the studio take a game studio's games; off, the firms' own rules run them as before");
        Check(studio.Contains("SyncProducts();   // the old rules carry on") && studio.Contains("FromProduct(g, p)"),
            "studio sources: switched off, the games are left as the old rules carry on with; on again, what those rules did is taken back");
        Check(studio.Contains("Id = StudioShare.Part") && studio.Contains("SharedCity.Follows(StudioShare.Part)") && studio.Contains("SharedCity.Changed(StudioShare.Part)"),
            "studio sources: the rival studios are the host's part of the shared city, followed on a guest and never stepped there");
        string links = ModSource(@"Wiring\Wiring.StudioLinks.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), books = ModSource(@"Economy\BizLines.cs");
        Check(links.Contains("TechBridges.StudioSessions = Hold;") && links.Contains("SessionRegistry.Remove(StudioRules.SessionIds[k])") && links.Contains("SessionRules.Strength(ask.ByManager, ask.Skill)"),
            "studio sources: the four desk sessions join the registry while the studio runs, a manager's strength counted");
        Check(mod.Contains("Settings.LivingStudioId, \"reallife_studio_enabled\"") && mod.Contains("TestStudioStart()") && mod.Contains("TestStudioFinish()") && mod.Contains("TestStudioLaunch()")
              && mod.Contains("TestStudioYear()") && tree.Contains("S(\"studio\").For(\"studio\").Opt(\"living_studio\", \"living_population\")"),
            "studio sources: the studio's option under the master switch, and its four tests in the options");
        bool keys = true;
        foreach (string k in new[] { "tech_game_engine", "tech_game_certification", "tech_game_contractors", "tech_game_bonus", "tech_game_royalty", "tech_game_advance", "tech_game_crowdfund" })
        {
            keys &= books.Contains("P + \"" + k + "\"");
        }
        Check(keys, "studio sources: every money key of the studio is in BizMan's lists, takings, costs or capital");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        Check(people.Contains("StudioPull(p.Place, day)") && people.Contains("StudioPoacher(p.Place, day)"), "studio sources: the people part's rivals pull at a studio whose hit paid no bonus, and a rival studio courts them");
    }

    // ---------------------------------------------------------------- the texts

    private static readonly Regex StudioKeys = new Regex(@"""(reallife_studio_[a-z0-9_]+)""", RegexOptions.Compiled);

    private static readonly string[] SessionChoiceNames =
    {
        "pitch_lean", "pitch_planned", "pitch_bold", "pitch_self", "pitch_publisher", "pitch_no_drm", "pitch_drm", "milestone_cut", "milestone_hold", "milestone_delay", "milestone_crunch",
        "milestone_normal", "milestone_budget", "playtest_team", "playtest_closed", "playtest_open", "playtest_fix_all", "playtest_fix_worst", "playtest_ship", "postlaunch_hotfix",
        "postlaunch_patch", "postlaunch_leave", "postlaunch_update", "postlaunch_addon", "postlaunch_listen", "postlaunch_hold", "postlaunch_sale", "postlaunch_drm_off"
    };

    // Every key the studio's sources name, and the keys they build from parts.
    private static List<string> StudioTextKeys()
    {
        var keys = new List<string>();
        foreach (string file in new[] { @"Tech\Studio\StudioRules.cs", @"Tech\Studio\StudioRules.Sales.cs", @"Tech\Studio\StudioViews.cs", @"Tech\Studio\StudioGuide.cs", @"Tech\TechDirector.Studio.cs",
                     @"Tech\TechDirector.StudioDesk.cs", @"Tech\TechDirector.StudioTests.cs", @"Core\RealLifeOverhaulMod.cs" })
        {
            foreach (Match m in StudioKeys.Matches(ModSource(file)))
            {
                string k = m.Groups[1].Value;
                if (!k.EndsWith("_", StringComparison.Ordinal) && !keys.Contains(k))
                {
                    keys.Add(k);
                }
            }
        }
        var built = new List<string>();
        for (int g = 0; g < (int)Genre.Count; g++)
        {
            built.Add(StudioViews.GenreKey(g));
        }
        for (int s = 0; s < StudioCatalogue.Settings; s++)
        {
            built.Add(StudioViews.SettingKey(s));
        }
        for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
        {
            built.Add(StudioViews.PlatformKey(p));
        }
        for (int m = 0; m < (int)GameModel.Count; m++)
        {
            built.Add(StudioViews.ModelKey(m));
            built.Add(StudioViews.ModelKey(m) + "_trade");
        }
        for (int p = 0; p <= (int)GamePhase.Ended; p++)
        {
            built.Add(StudioViews.PhaseKey(p));
        }
        for (int p = 0; p < (int)GamePart.Count; p++)
        {
            built.Add(StudioViews.PartKey(p));
        }
        for (int a = 0; a < (int)FanAsk.Count; a++)
        {
            built.Add(StudioViews.AskKey(a));
        }
        for (int b = 0; b < StudioRules.Beats; b++)
        {
            built.Add("reallife_studio_beat_" + b);
            built.Add("reallife_studio_beat_" + b + "_trade");
            built.Add("reallife_studio_note_beat_" + b);
        }
        for (int p = 1; p <= StudioRules.ProjectLocalise; p++)
        {
            built.Add("reallife_studio_after_project_" + p);
            if (p <= StudioRules.ProjectLocalise)
            {
                built.Add("reallife_studio_note_project_" + p);
            }
        }
        foreach (string session in new[] { "pitch", "milestone", "playtest", "postlaunch" })
        {
            built.Add("reallife_studio_" + session + "_title");
            built.Add("reallife_studio_" + session + "_about");
        }
        foreach (string choice in SessionChoiceNames)
        {
            built.Add("reallife_studio_" + choice);
            built.Add("reallife_studio_" + choice + "_trade");
        }
        foreach (string step in new[] { "pitch_step_ambition", "pitch_step_funding", "pitch_step_drm", "milestone_step_plan", "milestone_step_pace", "playtest_step_who", "playtest_step_fixes",
                     "postlaunch_step_bugs", "postlaunch_step_fans", "postlaunch_step_store" })
        {
            built.Add("reallife_studio_" + step);
        }
        built.AddRange(new[]
        {
            "reallife_transaction_tech_game_engine", "reallife_transaction_tech_game_certification", "reallife_transaction_tech_game_contractors", "reallife_transaction_tech_game_bonus",
            "reallife_transaction_tech_game_royalty", "reallife_transaction_tech_game_advance", "reallife_transaction_tech_game_crowdfund", "reallife_opt_sec_studio",
            "reallife_opt_tip_living_studio", "reallife_meta_title_studio", "reallife_guide_studio_about", "reallife_guide_studio", "reallife_studio_next_greenlight",
            "reallife_studio_button_greenlight"
        });
        foreach (string k in built)
        {
            if (!keys.Contains(k))
            {
                keys.Add(k);
            }
        }
        return keys;
    }

    private static void StudioTexts()
    {
        List<string> keys = StudioTextKeys();
        Check(AllIn(keys.ToArray(), out string missing), "studio texts: every key in en, pt and zh-cn (" + keys.Count + " keys; " + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        string over = "none";
        bool fits = true;
        foreach (string k in keys)
        {
            if ((k.Contains("_note_") || k.Contains("_test_") || k.EndsWith("_done", StringComparison.Ordinal) || k.Contains("_done_")) && en.ContainsKey(k)
                && en[k].Replace("{game}", new string('g', 24)).Replace("{studio}", new string('s', 24)).Replace("{business}", new string('b', 24)).Length > 240)
            {
                fits = false;
                over = k;
            }
        }
        Check(fits, "studio texts: every notice is 240 characters or fewer with long names in it (" + over + ")");
        bool buttons = true;
        foreach (string choice in SessionChoiceNames)
        {
            buttons &= en.TryGetValue("reallife_studio_" + choice, out string v) && !v.Contains("{");
        }
        Check(buttons, "studio texts: no session button's text has a placeholder, as the game's panel shows a button's key alone");
        string page = en.TryGetValue("reallife_guide_studio", out string guide) ? guide : string.Empty, facts = ModSource(@"Tech\Studio\StudioGuide.cs");
        bool filled = page.Length > 500;
        foreach (Match m in Regex.Matches(page, @"\{([a-z0-9_]+)\}"))
        {
            filled &= facts.Contains("\"" + m.Groups[1].Value + "\"");
        }
        Check(filled && page.Contains("<b>Living population</b>") && page.Contains("{studio_grace}"), "studio texts: the Guide's game studio page names its option and every figure in it is the code's");
        Check(WhatsNewHas(en, "The game studio is rebuilt") && WhatsNewHas(LocaleFile("pt"), "O estúdio de jogos foi refeito") && WhatsNewHas(LocaleFile("zh-cn"), "游戏工作室全面重做"),
            "studio texts: What's new tells of the rebuilt game studio in each of the three languages (any of its rows)");
    }

    // ---------------------------------------------------------------- what it costs

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double StudioDayCost(StudioFile f, List<GameDev> games, StudioCrew crew, long[] appeal, long[] plat, int rounds, int day)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < rounds; r++)
        {
            StudioRules.GenreAppeals(f, day + r, appeal);
            for (int i = 0; i < games.Count; i++)
            {
                GameDev g = games[i];
                if (g.Building)
                {
                    StudioRules.Build(g, crew, 300, day + r);
                    StudioRules.Advance(g, day + r);
                }
                else
                {
                    StudioRules.SellDay(g, f, day + r, appeal[g.Genre], plat, 1000, 1000);
                    StudioRules.ReportDay(g);
                    StudioRules.AfterLaunch(g, crew, 300, day + r);
                }
                StudioRules.HypeDay(g, day + r);
            }
        }
        return watch.Elapsed.TotalMilliseconds / rounds;
    }

    private static void StudioTimings()
    {
        StudioFile f = RivalRun(31, 900);
        var plat = new long[StudioCatalogue.GamePlatforms];
        var appeal = new long[(int)Genre.Count];
        var games = new List<GameDev>();
        for (int i = 0; i < FirmsCatalogue.ProductsMost; i++)
        {
            GameDev g = MadeGame(200 + i, (Genre)(i % 12), (Platform)(i % 3), ScaleCatalogue.BudgetTypical[i % 3], 60, 100, 900);
            if (i % 3 == 0)
            {
                g.Phase = (int)GamePhase.Production;
                g.Done = g.Work / 3;
            }
            else
            {
                StudioRules.Release(g, f, 900, 500, 31, 3000, plat, 0);
            }
            games.Add(g);
            f.Games.Add(g);
        }
        StudioCrew crew = Crew(20, 8, 3, 4, 60);
        StudioDayCost(f, games, crew, appeal, plat, 5, 901);
        double day = StudioDayCost(f, games, crew, appeal, plat, 100, 910);
        Check(day < 0.5, "studio timing: a day of the most games the firms hold, a third being built and the rest on sale, among 24 rival games, costs " + day.ToString("0.000")
              + " ms, under 0.5 (it comes once a game day)");
        var released = new List<RivalGame>(4);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int d = 0; d < 200; d++)
        {
            StudioRules.RivalDay(f, 2000 + d, released);
        }
        double rivals = watch.Elapsed.TotalMilliseconds / 200;
        Check(rivals < 0.2, "studio timing: the rival studios' day costs " + rivals.ToString("0.000") + " ms, under 0.2");
        StudioStore.Write(f);
        watch.Restart();
        string line = null;
        for (int i = 0; i < 20; i++)
        {
            line = StudioStore.Write(f);
        }
        double write = watch.Elapsed.TotalMilliseconds / 20;
        watch.Restart();
        for (int i = 0; i < 20; i++)
        {
            StudioStore.Read(line, out _);
        }
        double read = watch.Elapsed.TotalMilliseconds / 20;
        Check(write < 5 && read < 10, "studio timing: the save's line of the most games is written in " + write.ToString("0.00") + " ms and read in " + read.ToString("0.00") + " ms");
    }
}
