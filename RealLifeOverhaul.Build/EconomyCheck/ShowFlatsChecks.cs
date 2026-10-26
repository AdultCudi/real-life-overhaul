using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Designs;
using RealLifeOverhaul.Interiors;

// Show flats and renovations (Scripts/Construction, with Designs, Interiors, Business and the floors core), in the construction
// game's BuildersChecks style: the show flat's rules (viewings, a buyer's chance, the price, its costs, which blocks may have
// one), the refit's rules (hours, grade, a contractor's quote, the same on every run), the decor a refit's fittings give and how
// they fade, the decor of a room of the game's items by a business's own arithmetic, the store's lines read back (the project's
// new fields, the interiors' refits, a refit's closure), the tender's spacing and speciality; then the show flats' designs
// against the installed game's residential designs; then over the sources: the layering, nothing per frame, the floors core's
// design layout, the place's id after every shared place's, the wiring, the options, the tests' lines and buttons; then the texts.
internal static partial class Program
{
    private static void ShowFlats()
    {
        ShowFlatRuleChecks();
        RefitRuleChecks();
        RefitInteriorChecks();
        ShowFlatStoreChecks();
        ShowFlatDesignChecks();
        ShowFlatGameDesignChecks();
        ShowFlatSourceChecks();
        ShowFlatTextChecks();
    }

    private static void ShowFlatRuleChecks()
    {
        // Viewings: one at the standard, a second ten over it, a third twenty-five over it; their hours in order.
        Check(ShowFlatRules.Viewings(40, 50) == 1 && ShowFlatRules.Viewings(50, 50) == 1 && ShowFlatRules.Viewings(60, 50) == 2 && ShowFlatRules.Viewings(75, 50) == 3
              && ShowFlatRules.Viewings(100, 0) == BuildCatalogue.ViewingHours.Length, "show flats: viewings a day rise with the quality over the district's standard, at most three");
        bool hours = true;
        for (int n = 1; n <= BuildCatalogue.ViewingHours.Length; n++)
        {
            for (int i = 0; i < n; i++)
            {
                hours &= ShowFlatRules.ViewingAt(ShowFlatRules.ViewingHour(i, n), n) == i;
            }
            hours &= ShowFlatRules.ViewingHour(n, n) == -1;
        }
        Check(hours && ShowFlatRules.ViewingAt(3, 3) == -1 && ShowFlatRules.ViewingAt(BuildCatalogue.ViewingHours[2], 2) == -1,
            "show flats: a viewing's hour and the viewing at an hour agree, and an hour past the day's viewings has none");
        // A buyer's chance: bounded, more with quality and reputation, the player over the agent over nobody.
        bool bounded = true, rising = true;
        for (int q = 0; q <= 100; q += 5)
        {
            foreach (ViewingHost host in new[] { ViewingHost.Nobody, ViewingHost.Agent, ViewingHost.Player })
            {
                int c = ShowFlatRules.BuyerChance(q, 50, 50, host);
                bounded &= c >= BuildCatalogue.ShowBuyerLeast && c <= BuildCatalogue.ShowBuyerMost;
                rising &= q == 0 || c >= ShowFlatRules.BuyerChance(q - 5, 50, 50, host);
            }
        }
        Check(bounded && rising, "show flats: a buyer's chance stays inside its bounds and never falls as the quality rises");
        Check(ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Player) > ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Agent)
              && ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Agent) > ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Nobody)
              && ShowFlatRules.BuyerChance(60, 50, 80, ViewingHost.Agent) > ShowFlatRules.BuyerChance(60, 50, 20, ViewingHost.Agent),
            "show flats: buyers buy more often shown round by the player than by the agent than alone, and more from a company of standing");
        Check(ShowFlatRules.BuyerChance(50, 50, 50, ViewingHost.Nobody) == BuildCatalogue.ShowBuyerBase * BuildCatalogue.ShowAloneShare / 1000,
            "show flats: alone at the standard, a viewing's chance is the base at its share for nobody there");
        // The price: the plain off plan price at the standard, more over it, less under it, bounded.
        Check(ShowFlatRules.PricePermille(50, 50) == 1000 && ShowFlatRules.PricePermille(60, 50) == 1030 && ShowFlatRules.PricePermille(40, 50) == 970
              && ShowFlatRules.PricePermille(100, 0) == BuildCatalogue.ShowPriceMost && ShowFlatRules.PricePermille(0, 100) == BuildCatalogue.ShowPriceLeast,
            "show flats: a buyer pays the plain off plan price at the standard, three thousandths more for each point over it and less under, inside the bounds");
        Check(ShowFlatRules.Price(200000, 60, 50) == 206000 && ShowFlatRules.Price(0, 60, 50) == 0 && ShowFlatRules.Price(int.MaxValue, 100, 0) > 0,
            "show flats: a flat's price at the show flat's share, never below nothing nor past the largest");
        // What it costs.
        Check(ShowFlatRules.FitCost(12000, 3000) == 15000 && ShowFlatRules.FitCost(-5, -5) == 0, "show flats: dressing costs the furniture and the installation, never less than nothing");
        Check(ShowFlatRules.DayCost(false, 1000) == BuildCatalogue.ShowUpkeepDay && ShowFlatRules.DayCost(true, 1000) == BuildCatalogue.ShowUpkeepDay + BuildCatalogue.AgentDay
              && ShowFlatRules.DayCost(true, 2000) == 2L * (BuildCatalogue.ShowUpkeepDay + BuildCatalogue.AgentDay) && ShowFlatRules.AgentCost(1500) == BuildCatalogue.AgentDay * 3 / 2,
            "show flats: a day open costs the suite and the agent at today's prices");
        // Which blocks may have one.
        var block = new ProjectFile { Id = 1, Kind = ProjectKind.Plot, Use = BuildUse.Homes, Stage = ProjectStage.Building, Area = 1700 };
        Check(ShowFlatRules.Allowed(block, 20), "show flats: a block of homes going up, with flats left, may have a show flat");
        block.FlatsSold = 20;
        bool soldOut = !ShowFlatRules.Allowed(block, 20);
        block.FlatsSold = 0;
        block.Stage = ProjectStage.Finished;
        bool finished = !ShowFlatRules.Allowed(block, 20);
        block.Stage = ProjectStage.Ready;
        block.Use = BuildUse.Offices;
        bool offices = !ShowFlatRules.Allowed(block, 20);
        block.Use = BuildUse.Homes;
        block.InDistrict = true;
        bool handed = !ShowFlatRules.Allowed(block, 20);
        Check(soldOut && finished && offices && handed && !ShowFlatRules.Allowed(null, 5) && !ShowFlatRules.Allowed(new ProjectFile { Kind = ProjectKind.Rebuild, Use = BuildUse.Homes, Stage = ProjectStage.Ready }, 5),
            "show flats: none for a block sold out, finished, handed to the district, of offices, a rebuild, or no project");
        Check(ShowFlatRules.FlatArea(1700, 20) == 85 && ShowFlatRules.FlatArea(100, 0) == BuildCatalogue.FlatArea && ShowFlatRules.FlatArea(10, 5) == 20,
            "show flats: a flat's floor area is the block's over its flats, never under twenty square metres");
        Check(ShowFlatRules.Quality(70, 40) == InteriorRules.Overall(70, 40), "show flats: the quality is Interiors' overall figure of the interior and decor scores");
        // A sale's random draw is the save's seed, the project and the hour: the same twice, and not the same for every hour.
        int draws = 0, same = 0;
        for (int key = 1000; key < 1100; key++)
        {
            bool a = BuildRandom.Of(4242, 7, key * 7 + 5).Chance(300), b = BuildRandom.Of(4242, 7, key * 7 + 5).Chance(300);
            same += a == b ? 1 : 0;
            draws += a ? 1 : 0;
        }
        Check(same == 100 && draws > 10 && draws < 60, "show flats: a viewing's draw is the same from the same seed and hour, and about its chance over many hours (" + draws + " in 100 at 300)");
    }

    private static void RefitRuleChecks()
    {
        Check(RefitRules.Hours(0) == BuildCatalogue.RefitHoursLeast && RefitRules.Hours(600) == BuildCatalogue.RefitHoursBase + 100 && RefitRules.Hours(100000) == BuildCatalogue.RefitHoursMost,
            "refits: the working hours grow with the floor area, inside their bounds");
        bool rising = true;
        for (int sqm = 0; sqm < 2000; sqm += 25)
        {
            rising &= RefitRules.Hours(sqm + 25) >= RefitRules.Hours(sqm);
        }
        Check(rising, "refits: a larger business never takes fewer hours");
        Check(RefitRules.Grade(0, false) == 0 && RefitRules.Grade(59, false) == 0 && RefitRules.Grade(60, false) == 1 && RefitRules.Grade(80, false) == 2 && RefitRules.Grade(100, true) == 2
              && RefitRules.Grade(60, true) == 2 && RefitRules.Grade(10, true) == 1, "refits: the fittings' grade by the work's quality, a renovation company's a grade better, fine at most");
        RefitQuote a = RefitRules.Quote(777, 40, 300, 1000), b = RefitRules.Quote(777, 40, 300, 1000);
        long value = RefitRules.Value(a.Hours, 1000);
        Check(a.Rival == b.Rival && a.Price == b.Price && a.Days == b.Days && a.Hours == RefitRules.Hours(300), "refits: a contractor's quote is the same for the same seed, day and business");
        Check(a.Rival >= 0 && a.Rival < BuildCatalogue.RivalCount && a.Price > value * 85 / 100 && a.Price < value * 115 / 100 && a.Days >= 1,
            "refits: the quote is one of the city's builders' at about the works' value (" + a.Price + " for " + value + ") and at least a day");
        Check(value == TenderRules.WorksValue(RefitRules.Works, a.Hours, 1000) && RefitRules.Value(a.Hours, 2000) == value * 2, "refits: a refit is valued at the programme's refit rate and today's prices");
        int differ = 0;
        for (int day = 1; day < 40; day++)
        {
            differ += RefitRules.Quote(777, day, 300, 1000).Price != a.Price ? 1 : 0;
        }
        Check(differ > 0, "refits: the quote changes from one day to another");
        var watch = Stopwatch.StartNew();
        long sum = 0;
        for (int i = 0; i < 2000; i++)
        {
            sum += RefitRules.Quote(i, i, 50 + i % 900, 1000).Price + ShowFlatRules.BuyerChance(i % 100, 50, 50, (ViewingHost)(i % 3));
        }
        watch.Stop();
        Check(sum > 0 && watch.ElapsedMilliseconds < 400, "refits: 2,000 quotes and chances in " + watch.ElapsedMilliseconds + " ms (under 400)");
        // The tenders of businesses' refits: their spacing, the renovation speciality's work.
        bool spaced = true;
        for (int day = 0; day < 200; day++)
        {
            int n = TenderRules.NextIn(99, day, TenderSource.Refit);
            spaced &= n >= BuildCatalogue.RefitEveryLeast && n <= BuildCatalogue.RefitEveryMost;
        }
        Check(spaced && TenderRules.NeedOf(TenderSource.Refit, RefitRules.Works, BuildUse.Shops) == Speciality.Renovation,
            "refits: businesses' refits open every few days, and are the renovation speciality's work");
        var refit = new ProjectFile { Kind = ProjectKind.Refit, CivicKind = RefitRules.Works, Use = BuildUse.Shops };
        Check(refit.Model == ProjectKind.Contract && FirmRules.Special(Speciality.Renovation, refit.Model, refit.Use, refit.CivicKind)
              && !FirmRules.Special(Speciality.Homes, refit.Model, refit.Use, refit.CivicKind) && PlansRules.InspectionAfter(refit.Model, WorkStage.Envelope) == (int)Inspection.Final,
            "refits: a refit is worked as the programme's refit works, the renovation speciality's, with one final inspection");
        // A contractor's refit is no site of the company's.
        var state = new BuildState();
        state.Projects.Add(new ProjectFile { Id = 1, Kind = ProjectKind.Refit, Stage = ProjectStage.Building, Contractor = 2 });
        state.Projects.Add(new ProjectFile { Id = 2, Kind = ProjectKind.Plot, Stage = ProjectStage.Building });
        state.Projects.Add(new ProjectFile { Id = 3, Kind = ProjectKind.Refit, Stage = ProjectStage.Ready, Street = "ba:street_x", Number = 4 });
        Check(state.Building() == 1 && state.AtBuilding("ba:street_x", 4) != null && state.AtBuilding("ba:street_x", 5) == null, "refits: a contractor's refit is not one of the company's sites; a building's refit is found by its address");
    }

    private static void RefitInteriorChecks()
    {
        Check(InteriorRules.RefitPoints(0) == InteriorRules.RefitGrades[0] && InteriorRules.RefitPoints(2) == InteriorRules.RefitMost && InteriorRules.RefitPoints(9) == InteriorRules.RefitMost
              && InteriorRules.RefitPoints(-3) == InteriorRules.RefitGrades[0], "refits: the new fittings' points by grade, inside the table");
        Check(InteriorRules.RefitLift(10, 0) == 10 && InteriorRules.RefitLift(10, InteriorRules.WearMost) == 0 && InteriorRules.RefitLift(10, 500) == 5 && InteriorRules.RefitLift(99, 0) == InteriorRules.RefitMost
              && InteriorRules.RefitLift(-4, 0) == 0, "refits: the fittings' lift is whole when fresh, half at half wear, nothing worn out");
        bool fades = true;
        for (int wear = 0; wear < InteriorRules.WearMost; wear += 50)
        {
            fades &= InteriorRules.RefitLift(14, wear + 50) <= InteriorRules.RefitLift(14, wear);
        }
        Check(fades, "refits: the lift never grows as the finishes wear");
        // A business's decor with and without a refit's fittings, by Interiors' own working.
        InteriorParts parts = InteriorRules.Parts(100, 100, 100, 100, true, true, true, false, false);
        InteriorShop plain = RefitShop(0, 0), fitted = RefitShop(10, 0), worn = RefitShop(10, 1000);
        InteriorRules.Work(plain, 0, parts);
        InteriorRules.Work(fitted, 0, parts);
        InteriorRules.Work(worn, 1000, parts);
        Check(fitted.DecorRaw == Math.Min(100, plain.DecorRaw + 10) && fitted.RefitNow == 10 && worn.RefitNow == 0 && fitted.Overall >= plain.Overall,
            "refits: a refit's fittings lift a business's decor score by their points (" + plain.DecorRaw + " -> " + fitted.DecorRaw + "), and give nothing once worn out");
        InteriorShop full = RefitShop(14, 0);
        full.Points[(int)DecorCategory.Art] = 100000;
        full.Points[(int)DecorCategory.Greenery] = 100000;
        full.Points[(int)DecorCategory.Lighting] = 100000;
        full.Points[(int)DecorCategory.Floor] = 100000;
        full.Points[(int)DecorCategory.Audio] = 100000;
        full.Kinds = 20;
        InteriorRules.Work(full, 0, parts);
        Check(full.DecorRaw == 100, "refits: the lift never takes the decor score past 100");
        // The decor of a room of the game's items (a show flat's design): the same arithmetic as a business's.
        var categories = new List<int> { 0, 0, 1, 2, 5, 6, -1 };
        var copies = new List<int> { 2, 1, 3, 4, 6, 1, 9 };
        InteriorShop room = RefitShop(0, 0);
        room.Sqm = 80;
        Array.Clear(room.Points, 0, room.Points.Length);
        room.Kinds = 0;
        for (int i = 0; i < categories.Count; i++)
        {
            if (categories[i] >= 0)
            {
                room.Points[categories[i]] += InteriorRules.KindPoints((DecorCategory)categories[i], copies[i]);
                room.Kinds += categories[i] < (int)DecorCategory.Furniture ? 1 : 0;
            }
        }
        InteriorRules.Work(room, 0, parts);
        Check(InteriorRules.DecorOf(categories, copies, 80) == room.DecorRaw && InteriorRules.DecorOf(null, null, 80) == 0 && InteriorRules.DecorOf(new List<int> { 9 }, new List<int> { 3 }, 80) == 0,
            "refits: a room's decor score from its items' kinds is a business's own (" + room.DecorRaw + "), nothing for no items or one not counted");
    }

    private static InteriorShop RefitShop(int refit, int wear)
    {
        var s = new InteriorShop { Kind = InteriorKind.Shop, Sqm = 120, Interior = 55, Clean = 100, Income = 100, Refit = refit };
        s.Points[(int)DecorCategory.Art] = 30;
        s.Points[(int)DecorCategory.Greenery] = 20;
        s.Points[(int)DecorCategory.Furniture] = 16;
        s.Kinds = 4;
        return s;
    }

    private static void ShowFlatStoreChecks()
    {
        var s = new BuildState { Seed = 5, NextId = 20, NextRefitDay = 61 };
        var p = new ProjectFile
        {
            Id = 7, Kind = ProjectKind.Plot, Stage = ProjectStage.Building, Use = BuildUse.Homes, Storeys = 6, Hours = 120, Lot = "uptown_1_02_03", Area = 1700, Flats = 20, ShowDesign = "B1/Modern/1", ShowSince = 33,
            ShowAgent = true, ShowInterior = 61, ShowDecor = 44, ShowStandard = 52, ShowViewings = 9, ShowSold = 3, ShowSpent = 45678, ShowShown = 33 * 24 + 14, WithRefit = false
        };
        var r = new ProjectFile
        {
            Id = 8, Kind = ProjectKind.Refit, Stage = ProjectStage.Finished, Storeys = 1, Street = "ba:street_5thavenue", Number = 36, Contractor = 5, ContractorPrice = 98765, Grade = 1, CivicKind = RefitRules.Works,
            Source = TenderSource.Client, Hours = 64
        };
        var w = new ProjectFile { Id = 9, Kind = ProjectKind.Rebuild, Stage = ProjectStage.Design, Storeys = 2, Hours = 40, Street = "ba:street_x", Number = 2, WithRefit = true };
        s.Projects.Add(p);
        s.Projects.Add(r);
        s.Projects.Add(w);
        s.Tenders.Add(new TenderFile { Id = 11, Source = TenderSource.Refit, CivicKind = RefitRules.Works, Street = "ba:street_y", Number = 3, Hours = 40, Value = 70000 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        Check(back != null && BuildStore.Write(back) == line, "show flats: the construction game's line with a show flat and refits reads back to the same line");
        if (back != null)
        {
            ProjectFile q = back.Project(7), t = back.Project(8), u = back.Project(9);
            Check(q != null && q.ShowOpen && q.ShowDesign == "B1/Modern/1" && q.ShowAgent && q.ShowDecor == 44 && q.ShowStandard == 52 && q.ShowSold == 3 && q.ShowSpent == 45678 && q.ShowShown == 33 * 24 + 14
                  && back.ShowFlat() == q && back.NextRefitDay == 61, "show flats: a block's show flat comes back with its design, scores, agent, sales and costs");
            Check(t != null && t.Kind == ProjectKind.Refit && t.Contractor == 5 && t.ContractorPrice == 98765 && t.Grade == 1 && u != null && u.WithRefit && back.Tenders[0].Source == TenderSource.Refit,
                "refits: a refit, its contractor and grade, a rebuild with its refit and a business's refit tender come back");
        }
        // A line from before these fields: no show flat, the company's own crews, no refit with a rebuild.
        BuildState old = BuildStore.Read(Regex.Replace(line, @"(\|1000\|0)\|-?\d+\|-?\d+\|[01]\|[^;^]*", "$1"), out _);
        Check(old != null && old.Project(7) != null && !old.Project(7).ShowOpen && old.Project(8).Contractor == -1 && old.Project(8).Grade == -1 && !old.Project(9).WithRefit && old.ShowFlat() == null,
            "show flats: a line written before show flats and refits reads with none of them");
        // The interiors' refits: a line of their own, read back; none written while no business has one.
        var store = new InteriorStore();
        InteriorFile shop = store.File("ba:street_5thavenue#36", true);
        shop.Wear = 120;
        Check(store.WriteRefits() == null, "refits: no refits' line while no business has a refit's fittings");
        shop.Refit = 10;
        shop.RefitDay = 44;
        string refits = store.WriteRefits();
        var again = InteriorStore.Read(store.Write());
        bool read = again.ReadRefits(refits);
        InteriorFile shopBack = again.File("ba:street_5thavenue#36", false);
        Check(read && shopBack != null && shopBack.Refit == 10 && shopBack.RefitDay == 44 && shopBack.Wear == 120 && again.WriteRefits() == refits,
            "refits: a business's refit comes back from its own line, the files' line as it was");
        var foreign = new InteriorStore();
        Check(!foreign.ReadRefits("9|x,1,2") && foreign.ReadRefits(null) && foreign.ReadRefits(string.Empty) && InteriorStore.Read(store.Write()).Files[0].Refit == 0,
            "refits: a refits' line of another version is not read; the files' line alone carries no refit");
        var data = new Dictionary<string, string> { { InteriorStore.Key, store.Write() }, { InteriorStore.RefitKey, refits } };
        InteriorStore loaded = InteriorStore.Load(data);
        Check(loaded.File("ba:street_5thavenue#36", false) != null && loaded.File("ba:street_5thavenue#36", false).Refit == 10, "refits: a save's two lines load the refit with its business");
        // A refit's closure in business pressure's line: its maker kept.
        BusinessState.Reset();
        BusinessState.Load(new Dictionary<string, string>(), 50);
        BusinessState.SetClosure("ba:street_5thavenue#36", ClosureSource.Refit, 50, 14);
        var saved = new Dictionary<string, string>();
        BusinessState.Save(saved);
        BusinessState.Reset();
        BusinessState.Load(saved, 50);
        bool kept = BusinessState.ClosureAt("ba:street_5thavenue#36", ClosureSource.Refit) >= 0 && BusinessState.Holds(ClosureSource.Refit);
        BusinessState.Reset();
        Check(kept, "refits: a refit's closure comes back as the refit's, so business pressure's own end does not reopen it early");
    }

    private static void ShowFlatDesignChecks()
    {
        Check(ShowFlatDesigns.Keeps(ShowFinish.Staged, "ba:itemname_painting50x70") && !ShowFlatDesigns.Keeps(ShowFinish.Plain, "ba:itemname_painting50x70")
              && ShowFlatDesigns.Keeps(ShowFinish.Plain, "ba:itemname_kingsizebed") && ShowFlatDesigns.Keeps(ShowFinish.Plain, "kingsizebed") && !ShowFlatDesigns.Keeps(ShowFinish.Plain, null)
              && !ShowFlatDesigns.Keeps(ShowFinish.Staged, string.Empty), "show flats: staged keeps every piece; plain keeps the furniture a home is pictured by, none of its dressing");
        bool parse = ShowFlatDesigns.Parse(ShowFlatDesigns.Id("N1", "modern", ShowFinish.Plain), out string folder, out string design, out ShowFinish finish);
        Check(parse && folder == "N1" && design == "modern" && finish == ShowFinish.Plain && !ShowFlatDesigns.Parse("N1/modern", out _, out _, out _) && !ShowFlatDesigns.Parse("N1/modern/7", out _, out _, out _)
              && !ShowFlatDesigns.Parse(null, out _, out _, out _), "show flats: a design's id reads back; one that is not a show flat's does not");
        Check(ShowFlatDesigns.Nearest(new List<int> { 40, 80, 150 }, 85) == 1 && ShowFlatDesigns.Nearest(new List<int> { 40, 80, 120 }, 100) == 1 && ShowFlatDesigns.Nearest(new List<int> { 0, 0 }, 50) == -1
              && ShowFlatDesigns.Nearest(new List<int>(), 50) == -1 && ShowFlatDesigns.Nearest(new List<int> { 200, 60 }, 500) == 0, "show flats: the structure nearest the flats' size, the smaller on a tie");
        Check(ShowFlatDesigns.Offered(ShowFinish.Staged, 3, 3) && !ShowFlatDesigns.Offered(ShowFinish.Plain, 20, 20) && !ShowFlatDesigns.Offered(ShowFinish.Plain, ShowFlatDesigns.PlainLeast - 1, 30)
              && ShowFlatDesigns.Offered(ShowFinish.Plain, ShowFlatDesigns.PlainLeast, 30), "show flats: a plain cut is offered with enough pieces and fewer than the whole");
        Check(!ShowFlatDesigns.Flat(ShowFlatDesigns.HouseSize) && !ShowFlatDesigns.Flat(ShowFlatDesigns.ParkingSize) && ShowFlatDesigns.Flat("ba:buildingsize_b") && !ShowFlatDesigns.Flat(null),
            "show flats: never a Hamptons house's or the car park's structure");
        Check(ShowFlatDesigns.Building == BuildCatalogue.UseTypes[(int)BuildUse.Homes], "show flats: the designs are the residential building type's, the homes' use");
    }

    // The installed game's residential designs: every apartment structure has one, every plain cut is worth offering, and every
    // name the plain cut keeps is one of the game's own pieces in them.
    private static void ShowFlatGameDesignChecks()
    {
        string root = System.IO.Path.Combine(DesignGameFiles, "InteriorDesigns", ShowFlatDesigns.Folder);
        if (!Directory.Exists(root))
        {
            Check(false, "show flats: the game's residential designs are at " + root);
            return;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int structures = 0, designs = 0, plains = 0;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string size = DesignRules.SizeId(System.IO.Path.GetFileName(dir), out _);
            if (size == null || !ShowFlatDesigns.Flat(size))
            {
                continue;
            }
            structures++;
            string[] files = Directory.GetFiles(dir, "*.json");
            Check(files.Length > 0, "show flats: the game has a residential design for structure " + System.IO.Path.GetFileName(dir));
            foreach (string file in files)
            {
                var names = new List<string>();
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                {
                    foreach (JsonElement item in doc.RootElement.GetProperty("Items").EnumerateArray())
                    {
                        names.Add(item.GetProperty("itemName").GetString() ?? string.Empty);
                    }
                }
                designs++;
                int kept = 0;
                foreach (string n in names)
                {
                    seen.Add(n.StartsWith("ba:itemname_", StringComparison.Ordinal) ? n.Substring(12) : n);
                    kept += ShowFlatDesigns.Keeps(ShowFinish.Plain, n) ? 1 : 0;
                }
                bool offered = ShowFlatDesigns.Offered(ShowFinish.Plain, kept, names.Count);
                plains += offered ? 1 : 0;
                Check(names.Count > 0 && ShowFlatDesigns.Offered(ShowFinish.Staged, names.Count, names.Count), "show flats: " + System.IO.Path.GetFileName(dir) + "/" + System.IO.Path.GetFileName(file) + " has pieces to stage");
                Check(offered, "show flats: " + System.IO.Path.GetFileName(dir) + "/" + System.IO.Path.GetFileName(file) + " cut plain keeps " + kept + " of " + names.Count + " pieces, worth offering");
            }
        }
        Check(structures >= 3 && designs >= structures, "show flats: the game has residential designs for " + structures + " apartment structures (" + designs + " designs, " + plains + " plain cuts)");
        foreach (string keep in ShowFlatDesigns.PlainKeeps)
        {
            Check(seen.Contains(keep), "show flats: the plain cut's " + keep + " is a piece of the game's residential designs");
        }
    }

    private static void ShowFlatSourceChecks()
    {
        string shows = ModSource(@"Construction\ConstructionDirector.ShowFlats.cs"), refits = ModSource(@"Construction\ConstructionDirector.Refits.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.SalesTests.cs"), screens = ModSource(@"Construction\ConstructionDirector.Screens.cs");
        string place = ModSource(@"Construction\ShowFlatPlace.cs"), rules = ModSource(@"Construction\ShowFlatRules.cs") + ModSource(@"Construction\RefitRules.cs");
        string app = ModSource(@"Construction\ConstructionApp.ShowFlat.cs") + ModSource(@"Construction\ConstructionApp.Refit.cs");
        string designs = ModSource(@"Designs\ShowFlatDesigns.cs"), designsDirector = ModSource(@"Designs\DesignsDirector.ShowFlats.cs"), designsGame = ModSource(@"Designs\DesignsGame.cs");
        string all = shows + refits + tests + screens + place + rules + app + designs + designsDirector;
        Check(shows.Length > 0 && refits.Length > 0 && tests.Length > 0 && screens.Length > 0 && place.Length > 0 && rules.Length > 0 && app.Length > 0 && designs.Length > 0,
            "show flats sources: every file is there");
        Check(!Regex.IsMatch(all, @"using System\.Linq|(?<!FloorsDirector)\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(\)|\.ToArray\(\)|void Update\(\)|void LateUpdate\(\)|FindObjectsOfType|FindObjectsByType|\bHarmony\b|requiredDLC|SteamApps"),
            "show flats sources: no LINQ, no Update of their own, no whole-scene search, no patch, no DLC read");
        foreach (string name in new[] { @"Construction\ShowFlatRules.cs", @"Construction\RefitRules.cs", @"Designs\ShowFlatDesigns.cs" })
        {
            Check(!Regex.IsMatch(ModSource(name), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode"), "show flats sources: " + name + " is game-free and draws only from its seed");
        }
        foreach (string source in new[] { shows, refits, tests })
        {
            Check(!Regex.IsMatch(source, @"SaveGameManager\.Current\.(Money|BuildingRegistrations|EmployeeInstances|realEstate)|(?<!Save)GameManager\.|BuildingHelper\.|EmployeeHelper\.|\b(CivicDirector|CivicWorks|SiteState|InteriorDirector|BusinessDirector|DesignsDirector)\b"),
                "show flats sources: the director's parts leave the game to ConstructionGame and reach other features through the bridges");
        }
        Check(!Regex.IsMatch(designsDirector, @"InteriorInstallationFirmHelper|BuildingSizeHelper|ItemHelper") && designsGame.Contains("InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(ShowFlatDesigns.Building, info)")
              && designsGame.Contains("InteriorInstallationFirmHelper.GetInteriorDesignLayout(names[n], ShowFlatDesigns.Building, info)") && designsGame.Contains("ItemHelper.GetDefaultMarketPrice(item.itemName)")
              && designsGame.Contains("InteriorInstallationFirmHelper.GetInstallationFee(info.buildingSize)"),
            "show flats sources: the designs are the firm's own residential designs, read by the firm's own helpers and priced at the game's market prices and fee, in DesignsGame alone");
        // The floors core lays a firm's design and leaves out what the cut does not keep, with what stands on it.
        string layout = ModSource(@"Floors\FloorsGame.Layout.cs"), visit = ModSource(@"Floors\FloorVisit.cs"), entering = ModSource(@"Floors\FloorsDirector.Entering.cs");
        Check(layout.Contains("InteriorInstallationFirmHelper.GetInteriorDesignLayout(layout.Name, layout.DesignBuilding, new BuildingSizeInfo(size, version))")
              && layout.Contains("items[i].parentId") && visit.Contains("public static FloorLayout Design(string building, string name, Func<string, bool> keeps)")
              && entering.Contains("FloorsGame.LayOrder(set, place.LayLast, place.Layout.Keeps)"), "show flats sources: the floors core lays an installation firm's design cut to the pieces kept, none left standing on a piece taken out");
        Check(place.Contains("public const string PlaceId = \"~construction_showflat\";") && string.CompareOrdinal("~construction_showflat", "trades_architects") > 0
              && string.CompareOrdinal("~construction_showflat", "floors_test") > 0 && string.CompareOrdinal("~construction_showflat", "zzzz") > 0
              && place.Contains("Structure = new FloorStructure(design.Size, design.Version)") && place.Contains("FloorLayout.Design(") && !place.Contains("void Update"),
            "show flats sources: the show flat's place asks after every shared place (so it never moves theirs), on its design's apartment structure, laid by the core");
        Check(shows.Contains("FloorsDirector.Ask(_showPlace)") && shows.Contains("FloorsDirector.Release(place, why)") && MethodText(ModSource(@"Construction\ConstructionDirector.cs"), "public void StandDown(").Contains("DropShowPlace(")
              && MethodText(ModSource(@"Construction\ConstructionDirector.cs"), "private void Slow()").Contains("ShowSecond();"), "show flats sources: the floor is asked and let go with the show flat, the option and the construction game");
        Check(MethodText(shows, "private void ShowHour(int day, int hour)").Contains("Viewing(p, HourKey(beforeDay, before), false)") && MethodText(shows, "internal bool Viewing(").Contains("BuildRandom.Of(_state.Seed, p.Id, key * 7 + 5)")
              && MethodText(shows, "private int OffPlanSale(").Contains("p.OffPlanBalance += (long)(price - deposit) * n;") && MethodText(ModSource(@"Construction\ConstructionDirector.Actions.cs"), "public string SellOffPlan(").Contains("OffPlanSale(p, price, count)"),
            "show flats sources: a viewing is decided as its hour ends from the save's seed, and every off plan sale is the one sale");
        Check(MethodText(ModSource(@"Construction\ConstructionDirector.Day.cs"), "private void HourWork(").Contains("RefitHour();") && MethodText(ModSource(@"Construction\ConstructionDirector.Day.cs"), "private void HourWork(").Contains("ShowHour(day, hour);")
              && MethodText(ModSource(@"Construction\ConstructionDirector.Day.cs"), "private void DayWork(").Contains("ShowDay(day);") && MethodText(ModSource(@"Construction\ConstructionDirector.Market.cs"), "private void MarketDay(").Contains("RefitTenders(day);"),
            "show flats sources: the viewings and the refits' closures on the hour, the show flat's costs and the refit tenders on the day");
        Check(MethodText(refits, "private void HoldShut(").Contains("ConstructionBridges.CloseShop(p.Street, p.Number, BuildCatalogue.RefitHold)") && MethodText(refits, "private void Refitted(").Contains("LetOpen(p);")
              && MethodText(ModSource(@"Construction\ConstructionDirector.Projects.cs"), "public string Abandon(").Contains("LetOpen(p);"), "refits sources: a refit holds its business closed a few hours at a time and lets it open at its end or when it is given up");
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        Check(wiring.Contains("BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.Refit)") && wiring.Contains("BusinessDirector.Instance?.Release(reg, ClosureSource.Refit)")
              && wiring.Contains("InteriorDirector.Instance.Refit(reg, grade)") && wiring.Contains("InteriorDirector.DecorOf(d.Names, d.Sqm)") && wiring.Contains("DesignsDirector.ShowDesigns(flatArea, Shows)")
              && wiring.Contains("InteriorBridges.Renovate = Renovate;") && wiring.Contains("InteriorBridges.Renovate = null;"),
            "refits sources: the closure is business pressure's own, the refit and the decor Interiors', the designs the Designs feature's, and the Interior tab's button the construction game's");
        Check(ModSource(@"Business\BusinessState.cs").Contains("Refit = 2") && ModSource(@"Interiors\InteriorTab.Pages.cs").Contains("InteriorBridges.Renovate") && ModSource(@"Interiors\InteriorGuide.cs").Contains(".Data(InteriorStore.Key, InteriorStore.RefitKey)"),
            "refits sources: the closure's maker, the tab's button and the refits' line are the features' own");
        // The options, and every test: a public static method, a button of the page, in the feature list, writing its line.
        string settings = ModSource(@"Core\Settings.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (string id in new[] { "ConstructionShowFlats", "ConstructionRenovations" })
        {
            Check(settings.Contains("public static bool " + id + " = true;") && settings.Contains(id + " = Bool(modId, " + id + "Id, " + id + ");") && guide.Contains("Settings." + id + "Id"),
                "show flats sources: the option " + id + " is on by default, read, and the feature's");
        }
        Check(tree.Contains(".Opt(\"construction_show_flats\", \"construction\").Opt(\"construction_renovations\", \"construction\")"), "show flats sources: both options in the construction section, each needing the company");
        int found = 0;
        foreach (Match m in Regex.Matches(tests, @"public static bool (Test[A-Za-z]+Now)\(\)"))
        {
            found++;
            string name = m.Groups[1].Value;
            string body = MethodText(tests, "public static bool " + name + "()");
            Check(guide.Contains("ConstructionDirector." + name + "()") || name == "TestPutBackNow", "show flats sources: " + name + " is a button of the options page");
            Check(body.Contains("ShowCheck") || body.Contains("RefitCheck") || body.Contains("ViewingTest(") || name == "TestPutBackNow", "show flats sources: " + name + " writes its check line");
        }
        Check(found == 8, "show flats sources: eight tests, each a public static method a runner can call (" + found + ")");
        foreach (string key in new[] { "reallife_bld_test_show_flat", "reallife_bld_test_show_visit", "reallife_bld_test_viewing", "reallife_bld_test_show_buyer", "reallife_bld_test_refit",
                     "reallife_bld_test_refit_start", "reallife_bld_test_refit_tender" })
        {
            Match joined = Regex.Match(guide, @"\.Check\(""" + key + @""", \(\) => ConstructionDirector\.Test[A-Za-z]+Now\(\), ""(Show flats|Renovation) check \(([^""]+)\)""");
            Check(joined.Success && Regex.Matches(guide, "\"" + key + "\"").Count == 1 && tests.Contains("\"" + joined.Groups[2].Value + "\""),
                "show flats sources: the test " + key + " joins the test run once (its button too), its line the one its method writes");
        }
        Check(MethodText(ModSource(@"Construction\ConstructionDirector.Tests.cs"), "public void TestBack()").Contains("SalesBack(was)") && MethodText(tests, "private int SalesBack(").Contains("ConstructionBridges.InteriorsBack()"),
            "show flats sources: putting the construction game back puts back the interiors a test refitted and the show flat's floor");
        Check(tests.Contains(": PASS.") && tests.Contains(": FAIL."), "show flats sources: the tests' lines end PASS or FAIL");
    }

    private static void ShowFlatTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { @"Construction\ConstructionDirector.ShowFlats.cs", @"Construction\ConstructionDirector.Refits.cs", @"Construction\ConstructionDirector.SalesTests.cs",
                     @"Construction\ConstructionDirector.Screens.cs", @"Construction\ShowFlatPlace.cs", @"Construction\ConstructionApp.ShowFlat.cs", @"Construction\ConstructionApp.Refit.cs",
                     @"Construction\ConstructionGuide.cs", @"Wiring\Wiring.ConstructionLinks.cs", @"Interiors\InteriorTab.Pages.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_transaction_bld_show", "reallife_transaction_bld_show_fit", "reallife_transaction_bld_refit" })
        {
            keys.Add(key + "_label");   // deductible: the game's statement shows the label
        }
        keys.Add("reallife_opt_tip_construction_show_flats");
        keys.Add("reallife_opt_tip_construction_renovations");
        Check(keys.Count > 100, "show flats texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("show flats texts", words, codes))
        {
            return;
        }
        var notices = new[] { "reallife_bld_notice_show_gone", "reallife_bld_notice_viewing", "reallife_bld_notice_show_sold", "reallife_bld_notice_refitted" };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            int missing = 0, styled = 0;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "show flats texts: " + key + " is in " + codes[l] + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    styled++;
                    Check(false, "show flats texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && !SameNames(en, value))
                {
                    styled++;
                    Check(false, "show flats texts: " + key + " has the English names in curly brackets in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0, "show flats texts: every key of the show flats and refits is in " + codes[l] + ".json, in the style");
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            Check(page.Contains("{bld_show_day}") && page.Contains("{bld_agent_day}") && page.Contains("{bld_refit_lift_most}") && page.Contains("{bld_viewings_most}"),
                "show flats texts: the Guide's construction page in " + codes[l] + " has the show flats' and refits' figures");
        }
        foreach (string notice in notices)
        {
            Check(words[0].TryGetValue(notice, out string en) && en.Length <= 240, "show flats texts: the notice " + notice + " is at most 240 characters");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Show flats and renovations:" }, { "pt", "Apartamentos decorados e reformas:" }, { "zh-cn", "样板间与翻新：" }, { "de", "Musterwohnungen und Renovierungen:" },
            { "fr", "Appartements témoins et rénovations :" }, { "es", "Pisos piloto y reformas:" }, { "ru", "Демонстрационные квартиры и ремонт:" }, { "nl", "Modelwoningen en renovaties:" },
            { "tr", "Örnek daireler ve yenilemeler:" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = words[l].TryGetValue("reallife_changes_next", out string row) ? row : string.Empty;
            int line = starts.TryGetValue(codes[l], out string start) ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix), "show flats texts: What's new's next row in " + codes[l] + " has show flats and renovations, before its fixes");
        }
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (Match m in Regex.Matches(words[0]["reallife_guide_construction"], @"\{(bld_[a-z_]+)\}"))
        {
            Check(guide.Contains(".Fact(\"" + m.Groups[1].Value + "\""), "show flats texts: the Guide's figure {" + m.Groups[1].Value + "} is a fact of the feature list");
        }
    }

    // The same names in curly brackets, in any order.
    private static bool SameNames(string a, string b)
    {
        var x = new SortedSet<string>(StringComparer.Ordinal);
        var y = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Names.Matches(a))
        {
            x.Add(m.Value);
        }
        foreach (Match m in Names.Matches(b))
        {
            y.Add(m.Value);
        }
        return x.SetEquals(y);
    }
}
