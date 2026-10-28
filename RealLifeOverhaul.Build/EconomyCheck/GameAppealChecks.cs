using System;
using RealLifeOverhaul.Tech;

// A game's appeal and bug allowance (Scripts/Tech/Firms/FirmsRules.cs): a finished game appeals above the floor, appeal rises
// with how much of it is built and with its size, and a bigger game holds more bugs before its quality falls. The scenario at
// the end builds, launches and sells a small, a mid-sized and a big game with typical crews, as the balance reference.
internal static partial class Program
{
    private static void GameAppeal()
    {
        var f = new FirmsFile { Seed = 11 };
        var firm = new Firm { Place = "g", Reputation = 500 };
        Crew crew = Team(32);

        // a finished game of each size appeals above the floor, and a bigger one appeals more
        int floor = 200;
        int[] finished = new int[3];
        for (int s = 0; s < 3; s++)
        {
            Product g = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, s, null);
            g.Progress = FirmsCatalogue.GameScope[s];
            finished[s] = FirmsRules.Appeal(g, firm, crew);
            Check(finished[s] > floor, "game appeal: a finished " + ScopeName(s) + " game appeals above the floor (" + finished[s] + ")");
        }
        Check(finished[0] < finished[1] && finished[1] < finished[2], "game appeal: a bigger game appeals more (" + finished[0] + ", " + finished[1] + ", " + finished[2] + ")");

        // appeal rises with how complete the game is
        int last = -1;
        bool rises = true, strictly = false;
        for (int tenth = 0; tenth <= 10; tenth++)
        {
            Product g = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 1, null);
            g.Progress = FirmsCatalogue.GameScope[1] * tenth / 10;
            int a = FirmsRules.Appeal(g, firm, crew);
            rises &= a >= last;
            strictly |= last >= 0 && a > last;
            last = a;
        }
        Check(rises && strictly, "game appeal: appeal rises with how much of the game is built");

        // the rival counts: a rival with more to show takes the appeal down
        Product ahead = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 1, null), level = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 1, null);
        ahead.Progress = level.Progress = FirmsCatalogue.GameScope[1];
        ahead.RivalFeatures = level.RivalFeatures + 8;
        Check(FirmsRules.Appeal(ahead, firm, crew) < FirmsRules.Appeal(level, firm, crew), "game appeal: a rival with more features takes a game's appeal down");

        // a game is no more than whole: work past its size adds nothing
        Product over = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 0, null), whole = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 0, null);
        whole.Progress = FirmsCatalogue.GameScope[0];
        over.Progress = FirmsCatalogue.GameScope[0] * 3;
        Check(FirmsRules.Appeal(over, firm, crew) == FirmsRules.Appeal(whole, firm, crew), "game appeal: a game built past its size appeals no more than a whole one");

        // a bigger game holds more bugs before its quality falls: the same bugs hurt a small game more
        Product small = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 0, null), big = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 2, null);
        small.Progress = FirmsCatalogue.GameScope[0];
        big.Progress = FirmsCatalogue.GameScope[2];
        small.Bugs = big.Bugs = 120;
        Check(FirmsRules.Quality(big) > FirmsRules.Quality(small), "game appeal: a bigger game has a larger bug allowance (" + FirmsRules.Quality(small) + " against " + FirmsRules.Quality(big) + " at 120 bugs)");
        Check(FirmsRules.Allowance(big) > FirmsRules.Allowance(small) && FirmsRules.Allowance(small) > 22, "game appeal: the allowance of a game is its size's, not a service's first features");

        // a service is as it was: its features against the rival's, by the one rule (five against nine, at a sound product's price tier and reputation)
        Product service = Saas(f, Segment.Consumer, 4, 0);
        service.RivalFeatures = 8;
        Check(FirmsRules.Appeal(service, firm, crew) == 1000 * 5 / 9, "game appeal: a service still appeals by its features against the rival's (" + FirmsRules.Appeal(service, firm, crew) + ")");

        // designers' lift and product people keep working on a game
        Crew designers = Team(32);
        designers.Design = 8;
        Product made = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 1, null);
        made.Progress = FirmsCatalogue.GameScope[1];
        Check(FirmsRules.Appeal(made, firm, designers) > FirmsRules.Appeal(made, firm, crew), "game appeal: designers lift a game's appeal");
        Crew managed = Team(32);
        managed.Product = 8;
        Product bare = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 2, null), helped = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, 2, null);
        var focus = new Firm { Place = "g", Focus = (int)Focus.Features };
        for (int d = 0; d < 20; d++)
        {
            FirmsRules.Build(focus, bare, Team(32), 32, 0);
            FirmsRules.Build(focus, helped, managed, 32, 0);
        }
        Check(helped.Progress > bare.Progress, "game appeal: product people make a game's work go further");

        if (Environment.GetEnvironmentVariable("RLO_GAME_SCENARIO") != null)
        {
            GameScenario();
        }
    }

    // A small, a mid-sized and a big game built by a typical crew, launched and sold for 200 days at a steady market, no marketing
    // and again with marketing level 2: the days to build, the appeal, the sales on the first day and over the first month, and
    // the takings after the stores' cut. Printed when RLO_GAME_SCENARIO is set, so the balance can be read before and after a change.
    private static void GameScenario()
    {
        int[] engineers = { 16, 32, 64 };     // work points a day: two, four and eight mid-level programmers
        int[] designers = { 8, 8, 16 };        // one, one and two designers
        int[] testers = { 8, 8, 16 };
        for (int marketing = 0; marketing <= 2; marketing += 2)
        {
            for (int s = 0; s < 3; s++)
            {
                var f = new FirmsFile { Seed = 3 };
                var firm = new Firm { Place = "g", Reputation = 500, Focus = (int)Focus.Balanced };
                Product g = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Consumer, s, null);
                g.Marketing = marketing;
                Crew c = Team(engineers[s]);
                c.Design = designers[s];
                c.Testing = testers[s];
                int days = 0;
                while (!FirmsRules.GameReady(g) && days < 600)
                {
                    FirmsRules.Build(firm, g, c, c.Engineering, c.Testing);
                    FirmsRules.Hype(g);
                    days++;
                }
                FirmsRules.Launch(g, 100);
                int appeal = FirmsRules.Appeal(g, firm, c), quality = FirmsRules.Quality(g);
                long first = 0, month = 0, all = 0;
                for (int d = 100; d < 300; d++)
                {
                    long won = FirmsRules.Sell(f, firm, g, c, 5, d, 1, 1000).Won;
                    first = d == 100 ? won : first;
                    month += d < 130 ? won : 0;
                    all += won;
                }
                Console.WriteLine("GAME SCENARIO marketing " + marketing + " " + ScopeName(s) + ": built in " + days + " days, appeal " + appeal + ", quality " + quality + ", bugs " + g.Bugs
                    + ", hype " + g.Hype + ", copies day one " + first + ", first 30 days " + month + ", 200 days " + all + ", takings " + g.Revenue / 100 + " (price " + FirmsCatalogue.GamePrice[s] / 100.0 + ")");
            }
        }
    }

    private static string ScopeName(int scope) => scope == 0 ? "small" : scope == 1 ? "mid-sized" : "big";
}
