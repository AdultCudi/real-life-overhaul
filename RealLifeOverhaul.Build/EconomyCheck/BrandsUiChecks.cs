using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Ui;

// Brands beyond the old app (run with --ventures-ui): brand value and the rival brand by one rule, awareness week by week beside a rival, royalties paid on time month by month, the
// rebate claim when a rival opens inside the exclusive area, the creators campaign, their save, and a year of days through the day entry.
internal static partial class Program
{
    private static BrandsState BrandsWorld(out Brand b, out Franchise f)
    {
        var s = new BrandsState { Seed = 99 };
        b = new Brand { Id = 1, Name = "Brewed Beans", Type = "coffee", Founded = 1, Ticket = 900, City = 4000, Standards = 78 };
        b.Districts.Add(new DistrictFile { Name = "Midtown", Rec = 6000 });
        s.Brands.Add(b);
        f = new Franchise { Id = 9, BrandId = 1, Name = "Sofia", District = "Midtown", Place = "a#1", Exclusive = true, Base = 120, RoyaltyBp = 600, LevyBp = 100, X = 0, Z = 0, HasSpot = true };
        s.Franchises.Add(f);
        s.Members.Add(new Member { Place = "m#1", BrandId = 1, X = 300, Z = 100, HasSpot = true });
        s.Members.Add(new Member { Place = "m#2", BrandId = 1, X = 2000, Z = 2000, HasSpot = true });
        return s;
    }

    private static void BrandsExtraChecks()
    {
        Brand b;
        Franchise f;
        BrandsState s = BrandsWorld(out b, out f);
        int index = 0;
        long one = BrandExtraRules.Value(1, 5000, 0, index), two = BrandExtraRules.Value(2, 5000, 0, index), famous = BrandExtraRules.Value(2, 9000, 0, index), fees = BrandExtraRules.Value(2, 5000, 3000, index);
        Check(two == one * 2 && famous > two && fees == two + 3000L * 24, "brands: a brand is worth its shops at the recognition they have, and two years of its fees a month");
        Check(BrandExtraRules.RivalValue(3, 5000, index) == BrandExtraRules.Value(3, 5000, 3 * CompanyCatalogue.AtPrices(600, index), index), "brands: a rival brand is worth what the same rule gives from its outlets");
        int a = BrandExtraRules.RivalRec(99, 2, 3, 10), c = BrandExtraRules.RivalRec(99, 2, 3, 10), more = BrandExtraRules.RivalRec(99, 2, 6, 10);
        Check(a == c && a >= 0 && a <= 9000 && more > a, "brands: the rival brand recognition is the same each time and grows with its outlets");

        // Awareness ring: one entry a week, twelve kept, the rival line beside.
        var x = new BrandsExtra();
        for (int day = 1; day <= 400; day++)
        {
            BrandExtraRules.Week(x, b, s.Seed, 2, 3, day);
        }
        BrandRing ring = x.RingOf(1, false);
        Check(ring != null && ring.Count == BrandRing.Weeks && ring.LastWeek == 400 / 7 && ring.Mine[11] == BrandRules.Best(b) && ring.Rival[11] > 0, "brands: awareness is kept week by week, twelve weeks, beside a rival brand");
        Check(!BrandExtraRules.Week(x, b, s.Seed, 2, 3, 400), "brands: a week is written once");

        // Royalties on time, month by month.
        var y = new BrandsExtra();
        for (int day = 1; day <= 360; day++)
        {
            f.Trouble = day > 100 && day < 160;
            BrandExtraRules.Month(y, f, day, 30);
        }
        RoyaltyRing r = y.RoyaltyOf(9, false);
        Check(r != null && r.Count == 12 && r.LastMonth == 360 / 30 && r.OnTime >= 9 && r.OnTime < 12, "brands: royalties are marked paid or late each month, twelve months kept (" + (r != null ? r.OnTime : -1) + " of 12 on time)");
        f.Trouble = false;

        // A rival opens inside the exclusive area: one claim per outlet, filed as the franchisee dispute.
        var outlets = new List<RivalOutletFact> { new RivalOutletFact { Move = 4, Tycoon = 1, District = "Midtown", Type = "coffee" }, new RivalOutletFact { Move = 5, Tycoon = 1, District = "Uptown", Type = "coffee" } };
        var z = new BrandsExtra();
        Rebate claim = BrandExtraRules.Inside(z, s, f, outlets, 200);
        Check(claim != null && claim.Move == 4 && claim.District == "Midtown", "brands: a rival outlet of the brand kind in the franchise area raises a rebate claim");
        BrandExtraRules.File(b, f, 200);
        Check(f.Ask == Ask.Dispute && f.AskAmount == BrandExtraRules.RebateClaim(b, f) && f.AskAmount > 0, "brands: the claim stands as the franchisee dispute for days of royalty and levy (" + f.AskAmount + ")");
        f.Ask = Ask.None;
        Check(BrandExtraRules.Inside(z, s, f, outlets, 201) == null, "brands: the same outlet never makes a second claim, and an outlet in another district makes none");
        f.Exclusive = false;
        Check(BrandExtraRules.Inside(new BrandsExtra(), s, f, outlets, 202) == null, "brands: a franchise that is not exclusive has no claim");
        f.Exclusive = true;
        Check(BrandExtraRules.OwnInside(s, f) == 1 && BrandExtraRules.ExclusiveRadius == 600, "brands: the brand own shops inside the 600 m radius are counted by their doors");

        // Creators on Flare as a campaign kind.
        Check(BrandRules.PushCost(BrandCatalogue.PushCreators, 0, index, 7777) == 7777 && BrandRules.PushDays(BrandCatalogue.PushCreators) == BrandCatalogue.CreatorsDays, "brands: a creators campaign costs what Flare asks and runs fourteen days");
        BrandRules.BookPush(b, BrandCatalogue.PushCreators, "Midtown");
        var d = new DistrictFile { Name = "Midtown", Rec = 1000 };
        b.Districts.Clear();
        b.Districts.Add(d);
        int none = BrandRules.GainOn(s, b, d, b.PushEnd + 1), during = BrandRules.GainOn(s, b, d, b.PushFrom);
        Check(b.Push == BrandCatalogue.PushCreators && during > none, "brands: a creators campaign adds recognition while it runs and not after (" + during + " against " + none + ")");
        b.Push = -1;

        // Save: the extras round trip, an old save loads empty, a later part is skipped.
        string line = BrandExtraStore.Write(x);
        BrandsExtra back = BrandExtraStore.Read(BrandExtraStore.Write(BrandExtraWith(x, y, z)));
        Check(back != null && back.Rings.Count == 1 && back.Royalties.Count == 1 && back.Rebates.Count == 1 && back.Rings[0].Count == 12 && back.Royalties[0].Bits == y.Royalties[0].Bits,
            "brands save: the extras write and read back to the same rings and claims");
        Check(BrandExtraStore.Load(new Dictionary<string, string>()).Rings.Count == 0 && BrandExtraStore.Load(new Dictionary<string, string> { { BrandExtraStore.Key, "junk" } }).Rebates.Count == 0,
            "brands save: a save from before the extras loads empty and a broken line gives an empty set");
        Check(BrandExtraStore.Read(line + "~q:future") != null, "brands save: a later version part is skipped");

        // A year of days through the day entry rules: rings fill, a claim is filed once, a save on day 30 reads back.
        BrandsState s2 = BrandsWorld(out Brand b2, out Franchise f2);
        var run = new BrandsExtra();
        string day30 = null;
        int claims = 0;
        for (int day = 1; day <= 360; day++)
        {
            RivalOutletFact[] now = day >= 120 ? outlets.ToArray() : new RivalOutletFact[0];
            BrandExtraRules.Week(run, b2, s2.Seed, 1, now.Length, day);
            BrandExtraRules.Month(run, f2, day, 30);
            Rebate got = BrandExtraRules.Inside(run, s2, f2, now, day);
            if (got != null)
            {
                BrandExtraRules.File(b2, f2, day);
                claims++;
                f2.Ask = Ask.None;
            }
            if (day == 30)
            {
                day30 = BrandExtraStore.Write(run);
            }
        }
        BrandsExtra restored = BrandExtraStore.Read(day30);
        Check(run.RingOf(1, false).Count == 12 && run.RoyaltyOf(9, false).Count == 12 && claims == 1 && restored != null && restored.RingOf(1, false).Count == 5,
            "brands run: a year of days fills the rings, files the claim once for the outlet, and a save on day 30 reads back with five weeks");
    }

    private static BrandsExtra BrandExtraWith(BrandsExtra rings, BrandsExtra royalties, BrandsExtra claims)
    {
        var all = new BrandsExtra();
        all.Rings.AddRange(rings.Rings);
        all.Royalties.AddRange(royalties.Royalties);
        all.Rebates.AddRange(claims.Rebates);
        return all;
    }

    private static void BrandsEntryPoints()
    {
        string root = RepoRoot();
        string dir = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Economy", "Brands");
        string director = File.ReadAllText(System.IO.Path.Combine(dir, "BrandsDirector.cs"));
        string extra = File.ReadAllText(System.IO.Path.Combine(dir, "BrandsDirector.Extra.cs"));
        Check(director.Contains("RealLifeOverhaul.Ui.BrandsLive.Want(tab, pick)") && director.Contains("Kit = () => new RealLifeOverhaul.Ui.VentScreen(") && director.Contains("UiShell.Face(_app, def)"),
            "brands entry points: the phone tile opens the kit page, and the tab and row of a notice reach it through Want");
        Check(extra.Contains("TabAgreements, \"fr:\" + f.Id") && director.Contains("ExtraDay(day);"), "brands entry points: the rebate notice opens the agreement, and the day turn runs the extras");
        Check(UiScreens.All.Any(sc => sc.Id == "brands" && sc.Section == UiScreenSwitches.Ventures), "brands entry points: the app has its own switch in Mod settings");
        string live = File.ReadAllText(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Ventures", "BrandsLive.cs"));
        Check(!live.Contains("Application.Quit") && !live.Contains("AppShell"), "brands entry points: nothing on the kit pages quits the game or opens the old window");
    }
}
