using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Flare;

internal static partial class Program
{
    private static void FlarePlayChecks()
    {
        var paid = new FlareBrief
        {
            Followers = 34, Hour = 12, Photo = true, PhotoHere = true, SameDistrict = 1, DayCustomers = 160, BasketCents = 2500,
            Districts = 1, PriceLevel = 1000, ReachPaid = 2000, AdDays = 1, Reliability = -1, Seed = 3, RollKey = 1
        };
        FlareExpect reach = FlareUseRules.Expect(in paid);
        Check(reach.Visitors > 0 && reach.Cost > 0 && FlareUseRules.KeepsBoost(paid.ReachPaid, reach.Visitors, false)
              && !FlareUseRules.KeepsBoost(paid.ReachPaid, reach.Visitors, true),
            "Flare play: paying for reach still grants the visitors the preview priced (" + reach.Visitors + ")");
        var stored = new List<FlareBoost>
        {
            new FlareBoost { Place = "cafe~1", FromMinute = 0, Hours = reach.Hours, Visitors = reach.Visitors, Cents = reach.Cents, Percent = reach.Percent }
        };
        Check(FlareUseRules.Marketing(stored, "cafe~1", 30) == reach.Percent && FlareUseRules.Marketing(stored, "rival~9", 30) == 0,
            "Flare play: the paid boost is what footfall adds for that business");

        var rival = new List<FlareBoost>
        {
            new FlareBoost { Place = "rival~9", FromMinute = 0, Hours = 6, Visitors = 20, Cents = 4000, Percent = 12, Flags = 16 }
        };
        Check(FlareUseRules.RivalCustomers(rival, "rival~9", 30, false, false) == 12
              && FlareUseRules.RivalCustomers(rival, "rival~9", 30, true, false) == 0
              && FlareUseRules.RivalCustomers(rival, "rival~9", 30, false, true) == 0
              && FlareUseRules.RivalCustomers(rival, "cafe~1", 30, false, false) == 0
              && FlareUseRules.Marketing(rival, "rival~9", 30) == 0,
            "Flare play: a rival's promotion adds customers only at that rival's business");

        var shops = new List<FlareShopSeen> { new FlareShopSeen { Place = "cafe~1" }, new FlareShopSeen { Place = "bar~2" } };
        FlareUseRules.FlarePick pick = FlareUseRules.FlarePick.Fresh();
        var blank = new FlareCompose();
        FlareUseRules.ApplyPick(blank, shops, pick);
        Check(pick.Shop < 0 && blank.Place == null, "Flare play: compose does not force the first business");
        FlareUseRules.Chip(ref pick, 1, shops.Count, 3, 2);
        FlareUseRules.Chip(ref pick, 1, shops.Count, 3, 2);
        FlareUseRules.Chip(ref pick, 0, shops.Count, 3, 2);
        FlareUseRules.Chip(ref pick, 2, shops.Count, 3, 2);
        FlareUseRules.Chip(ref pick, 3, shops.Count, 3, 2);
        pick.Pic = "f:counter.jpg";
        pick.Tags = "lunch";
        var chosen = new FlareCompose();
        FlareUseRules.ApplyPick(chosen, shops, pick);
        Check(pick.Shop == 1 && chosen.Place == "bar~2" && chosen.Place != shops[0].Place && chosen.Pic == "f:counter.jpg"
              && chosen.Tags == "lunch" && chosen.PromoteReach == 2000 && pick.Photo == 0 && pick.Tag == 0,
            "Flare play: a chip does its own job, and the second business is the one that is posted");

        var early = new FlareBoost { Place = "cafe~1", FromMinute = 1000, Hours = 6, Visitors = 80, Cents = 20000, Percent = 5 };
        int visits, cents;
        FlareUseRules.Credited(early, 1000, out visits, out cents);
        Check(visits == 0 && cents == 0 && !FlareUseRules.HourRan(early, 1000), "Flare play: before an hour, the result is not the forecast");
        early.GivenVisitors = 12;
        early.GivenCents = 3000;
        FlareUseRules.Credited(early, 1000, out visits, out cents);
        Check(visits == 12 && cents == 3000 && visits != early.Visitors, "Flare play: after an hour, the result is what was credited");

        var free = paid;
        free.ReachPaid = 0;
        free.Reliability = -1;
        free.Style = (int)FlareStyle.Flaky;
        FlareExpect quiet = FlareUseRules.Expect(in free);
        var hired = free;
        hired.Reliability = 10;
        FlareExpect roll = FlareUseRules.Expect(in hired);
        Check(quiet.Flop == 0 && !quiet.Flopped && !quiet.WentViral && roll.Flop > 0,
            "Flare play: a free post does not roll a flop, and a hired post can");

        var poor = new FlareStarRow { One = 4 };
        var loved = new FlareStarRow { Five = 4 };
        Check(FlareUseRules.StarPull(null) == 0 && FlareUseRules.StarPull(poor) < 0 && FlareUseRules.StarPull(loved) > 0,
            "Flare play: saved stars move attention");

        string use = PlayFile("Social", "Flare", "FlareDirector.Use.cs");
        string links = PlayFile("Social", "Flare", "FlareDirector.Links.cs");
        // The kit's screen with its parts (FlareScreen.Fill, .Desk, .Sheet and .Rows since the kit's Flare was completed), read as one.
        string screen = PlayFile("Ui", "Flare", "FlareScreen.cs") + PlayFile("Ui", "Flare", "FlareScreen.Fill.cs") + PlayFile("Ui", "Flare", "FlareScreen.Desk.cs")
                        + PlayFile("Ui", "Flare", "FlareScreen.Sheet.cs") + PlayFile("Ui", "Flare", "FlareScreen.Rows.cs") + PlayFile("Ui", "Flare", "FlareScreen.Compose.cs");
        string attention = PlayFile("Social", "Flare", "City", "FlareCityRules.Attention.cs");
        Check(use.Contains("KeepsBoost(") && use.IndexOf("PromoteReach <= 0", System.StringComparison.Ordinal) < 0
              && use.Contains("ReadSidecar(") && use.Contains("PhotoPlace(") && use.IndexOf("PhotoPlace(null", System.StringComparison.Ordinal) < 0
              && use.Contains("Reliability = -1") && use.IndexOf("Reliability = move == 1 ? 70", System.StringComparison.Ordinal) < 0
              && use.Contains("hired.Reliability"),
            "Flare play: the post path stores a paid boost, reads the picture's place, and does not invent a flop");
        Check(links.Contains("RivalCustomers(") && links.Contains("RivalBoosts") && links.Contains("GoingPublic.Mine") && links.Contains("GoingPublic.Foreign"),
            "Flare play: footfall reads a rival's boost and leaves the player's and another player's alone");
        // A composer chip opens the compose sheet on its part, the post goes to the business chosen there, a result is the
        // visitors the hours that ran have credited, the city's demand is its real trends, and a poll's yes and no are the model's.
        Check(screen.Contains("case FlareFace.ChipCommand: return OpenCompose(arg);") && screen.Contains("_compose.Place = shops[_place].Place;")
              && screen.Contains("FlareUseRules.Credited(") && screen.Contains("StarsFor(") && screen.Contains("CityTrendsView()")
              && screen.Contains("FlareFeedRules.Poll(p, now, out int yes, out int votes)")
              && screen.IndexOf("Shops[0]", System.StringComparison.Ordinal) < 0
              && screen.IndexOf("\"@me\"", System.StringComparison.Ordinal) < 0
              && screen.IndexOf("TrendDemand(file.Posts.Count)", System.StringComparison.Ordinal) < 0
              && screen.IndexOf("PollYes", System.StringComparison.Ordinal) < 0
              && screen.IndexOf("Roll(p.Seed, p.Id, 931)", System.StringComparison.Ordinal) < 0,
            "Flare play: the kit's press runs the chip, the chosen business, the credited result and the city's demand");
        Check(attention.Contains("StarPull("), "Flare play: attention reads the saved stars");
    }

    // The repository's root, found by walking up from where the checker runs to the folder that holds RealLifeOverhaul.Build, so a build output that moves (another
    // configuration, another target) still finds the sources the checks read.
    private static string RepoRoot()
    {
        for (var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "RealLifeOverhaul.Build")) && System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "Assets")))
            {
                return dir.FullName;
            }
        }
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\.."));
    }

    private static string PlayFile(params string[] parts)
    {
        string root = System.IO.Path.Combine(RepoRoot(), "Assets", "Mods", "RealLifeOverhaul", "Scripts");
        string path = root;
        for (int i = 0; i < parts.Length; i++)
        {
            path = System.IO.Path.Combine(path, parts[i]);
        }
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }
}
