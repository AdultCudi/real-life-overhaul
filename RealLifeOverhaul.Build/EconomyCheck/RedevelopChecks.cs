using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// Redevelopment (Scripts/Construction with Uptown, the district save and Civic's sites), in the construction game's BuildersChecks
// style, a part at a time: refurnishing (the game's designs for any unit of the player's, the game's own installation contract
// and its price, an AI business's new layout after a tender); then over the sources (one path for the player and the city's
// side, the game touched only in the construction game's game file, nothing in a Going Public session that would differ between
// machines, the tests' lines and buttons); then the texts in every language.
internal static partial class Program
{
    private static void Redevelop()
    {
        RefurnishRuleChecks();
        RefurnishStoreChecks();
        RefurnishSourceChecks();
        DemolitionRuleChecks();
        DemolitionStoreChecks();
        DemolitionSourceChecks();
        ClearedBuildChecks();
        RedevelopTextChecks();
    }

    // ------------------------------------------------------------------ refurnishing

    private static void RefurnishRuleChecks()
    {
        // The game's own price: its fee and the design's pieces, less what the furniture there sells for; nothing below nothing.
        Check(RefitRules.FurnishPrice(5860L, 12000L, 3000L) == 14860L && RefitRules.FurnishPrice(5860L, 1000L, 9000L) == 0L && RefitRules.FurnishPrice(0L, 0L, 0L) == 0L
              && RefitRules.FurnishPrice(-5L, 100L, -7L) == 100L, "refurnishing: the installers' price is their fee and the pieces less what the old furniture sells for, never below nothing");
        // The selector: its first place keeps the furniture, the rest are the firm's designs in order; a place past them keeps it too.
        Check(RefitRules.DesignAt(0, 3) == -1 && RefitRules.DesignAt(1, 3) == 0 && RefitRules.DesignAt(3, 3) == 2 && RefitRules.DesignAt(4, 3) == -1 && RefitRules.DesignAt(-2, 3) == -1
              && RefitRules.DesignAt(1, 0) == -1, "refurnishing: the selector's first place keeps the furniture and the others are the designs in the firm's order");
        // An AI business's new layout: never its own, always one of the list, the same for the same seed and tender, none with no other.
        bool never = true, inside = true, same = true;
        var seen = new HashSet<int>();
        for (int tender = 1; tender <= 400; tender++)
        {
            int pick = RefitRules.AiLayout(12345, tender, 5, 2);
            never &= pick != 2;
            inside &= pick >= 0 && pick < 5;
            same &= pick == RefitRules.AiLayout(12345, tender, 5, 2);
            seen.Add(pick);
        }
        Check(never && inside && same, "refurnishing: a business of the city's always wears another layout of the list than its own, the same for the same save and tender");
        Check(seen.Count == 4, "refurnishing: over many tenders every other layout of its kind is worn (" + seen.Count + " of 4)");
        Check(RefitRules.AiLayout(7, 9, 1, 0) == -1 && RefitRules.AiLayout(7, 9, 0, -1) == -1 && RefitRules.AiLayout(7, 9, 1, -1) == 0 && RefitRules.AiLayout(7, 9, 2, 0) == 1
              && RefitRules.AiLayout(7, 9, 2, 1) == 0, "refurnishing: with no other layout the business keeps its own; one not of the list may take any");
    }

    private static void RefurnishStoreChecks()
    {
        var s = new BuildState { Seed = 5, FirstDay = 1, LastDay = 9, NextId = 30 };
        s.Projects.Add(new ProjectFile
        {
            Id = 21, Kind = ProjectKind.Refit, Stage = ProjectStage.Building, Storeys = 1, Hours = 40, Contractor = 3, ContractorPrice = 4567, Street = "ba:street_x", Number = 4,
            FitDesign = "Modern|Blue;1^x", FitType = "ba:businesstype_empty"
        });
        s.Projects.Add(new ProjectFile { Id = 22, Kind = ProjectKind.Refit, Stage = ProjectStage.Ready, Storeys = 1, Hours = 40, Street = "ba:street_y", Number = 6 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int version);
        ProjectFile a = back != null ? back.Project(21) : null, b = back != null ? back.Project(22) : null;
        Check(version == BuildStore.Version && a != null && a.FitDesign == "Modern|Blue;1^x" && a.FitType == "ba:businesstype_empty" && b != null && b.FitDesign.Length == 0
              && b.FitType.Length == 0 && BuildStore.Write(back) == line, "refurnishing: a refit's design and its folder come back from the save, any letters in its name, the line as it was");
        // A line from before refurnishing: every refit keeps its furniture.
        string older = line.Replace("|Modern%7CBlue%3B1%5Ex|ba%3Abusinesstype_empty|0|0", string.Empty);   // the record's redevelopment fields, its last
        BuildState old = BuildStore.Read(older, out int _);
        Check(older != line && old != null && old.Project(21) != null && old.Project(21).FitDesign.Length == 0 && old.Project(21).FitType.Length == 0 && old.Project(21).ContractorPrice == 4567,
            "refurnishing: a line from before refurnishing reads with every refit keeping its furniture");
        // A folder with no design is no furnishing.
        string lost = line.Replace("|Modern%7CBlue%3B1%5Ex|", "||");
        BuildState none = BuildStore.Read(lost, out int _);
        Check(lost != line && none != null && none.Project(21) != null && none.Project(21).FitType.Length == 0, "refurnishing: a folder with no design reads as no furnishing");
    }

    private static void RefurnishSourceChecks()
    {
        string refits = ModSource(@"Construction\ConstructionDirector.Refits.cs"), units = ModSource(@"Construction\ConstructionGame.Units.cs");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.RedevelopTests.cs"), app = ModSource(@"Construction\ConstructionApp.Refit.cs"), rules = ModSource(@"Construction\RefitRules.cs");
        Check(refits.Length > 0 && units.Length > 0 && tests.Length > 0 && app.Length > 0 && rules.Length > 0, "refurnishing sources: the files are where the checks look");
        // The furnishing is the game's own contract and installation, and only the game file names them.
        string furnish = MethodText(units, "internal static bool Furnish(");
        Check(furnish.Contains("new InteriorInstallationFirmContract") && furnish.Contains("interiorInstallationFirmContracts.Add(order)") && furnish.Contains("order.DoInstallation()")
              && furnish.Contains("dayOfInstallation = save.Day"), "refurnishing sources: the design goes in through the game's own installation contract, dated the handover, done by the game");
        Check(MethodText(units, "internal static void Designs(").Contains("InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(") && MethodText(units, "internal static void Designs(").Contains("into.Sort(StringComparer.Ordinal)"),
            "refurnishing sources: the designs are the game's firm's own list, in the same order on every machine");
        Check(MethodText(units, "internal static bool FurnishFigures(").Contains("InteriorInstallationFirmHelper.GetInstallationFee(") && MethodText(units, "internal static bool FurnishFigures(").Contains("ItemHelper.GetDefaultMarketPrice(")
              && MethodText(units, "internal static bool FurnishFigures(").Contains("GetSellingPrice()"), "refurnishing sources: the price shown is the game's fee, its market prices and its selling prices");
        foreach (string name in new[] { @"Construction\ConstructionDirector.Refits.cs", @"Construction\ConstructionApp.Refit.cs", @"Construction\ConstructionDirector.RedevelopTests.cs" })
        {
            string src = ModSource(name);
            Check(!src.Contains("InteriorInstallationFirmContract") && !src.Contains("InteriorInstallationFirmHelper") && !src.Contains(".Layout ="), "refurnishing sources: " + name + " leaves the game to the construction game's game file");
        }
        foreach (string src in new[] { units, rules, MethodText(refits, "private void AiRefurnish(") })
        {
            Check(!Regex.IsMatch(src, @"using System\.Linq|\.Select\(|\.Where\(|\.Any\(|\.First"), "refurnishing sources: no LINQ in the refurnishing's reads and rules");
        }
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\."), "refurnishing sources: the refit's rules are game-free and draw only from their seed");
        // The handover lays the design; a business opens; a home is no business for Interiors.
        string refitted = MethodText(refits, "private void Refitted(");
        Check(refitted.Contains("LetOpen(p);") && refitted.Contains("Furnished(p)") && refitted.Contains("u.Trading") && MethodText(refits, "private string Furnished(").Contains("ConstructionGame.Furnish(u, p.FitDesign"),
            "refurnishing sources: at the handover the business opens and the chosen design is laid by the game's installers; a home has no business to score");
        Check(MethodText(refits, "public string RefitBlock(").Contains("ConstructionGame.Unit(street, number") && MethodText(units, "private static bool UnitOf(").Contains("reg.RentedByPlayer")
              && MethodText(units, "private static bool UnitOf(").Contains("GoingPublic.Mine(reg)") && MethodText(units, "private static bool UnitOf(").Contains("IsHamptonsHouse()"),
            "refurnishing sources: any unit the player rents or owns can be refitted, theirs in a session, never a Hamptons house");
        // The city's side: by tender, by whoever wins it, the game's own layouts, never in a session.
        string ai = MethodText(refits, "private void AiRefurnish(");
        Check(ai.Contains("GoingPublic.InSession") && ai.Contains("ConstructionGame.AiLayouts(") && ai.Contains("RefitRules.AiLayout(_state.Seed, tender") && ai.Contains("ConstructionGame.SetAiLayout("),
            "refurnishing sources: a business of the city's wears another of the game's layouts after its refit, from the save's seed, never in a Going Public session");
        Check(MethodText(market, "private void RivalHanded(").Contains("AiRefurnish(t.Street, t.Number, t.Id)") && market.Contains("t.SiteId = StartWorksSite(t.Street, t.Number, RefitRules.Works, false, won.Bidder")
              && MethodText(projects, "private void Handover(").Contains("AiRefurnish(p.Street, p.Number, p.Tender)"), "refurnishing sources: a rival's refit and the player's company's refit of a business of the city's both refurnish it at their handover");
        string layouts = MethodText(units, "internal static bool AiLayouts(");
        Check(layouts.Contains("reg.RentedByPlayer") && layouts.Contains("reg.BuildingOwnedByPlayer") && layouts.Contains("GoingPublic.Foreign(reg)") && layouts.Contains("corporationRivalId")
              && layouts.Contains("GetOrLoadBusinessLayoutSet("), "refurnishing sources: only a business of the city's of no chain is refurnished, with layouts the game has at its size, never the player's or another player's");
        // The tests: public static methods, each once in the guide with its line.
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (string name in new[] { "TestRefurnishNow", "TestCityRefurnishNow" })
        {
            Check(tests.Contains("public static bool " + name + "()") && Regex.Matches(guide, "ConstructionDirector." + name + "\\(\\)").Count == 1, "refurnishing sources: the test " + name + " is a public static method, once in the guide");
        }
        Check(tests.Contains(": PASS.") && tests.Contains(": FAIL.") && tests.Contains("\"reallife_bld_test_passed\""), "refurnishing sources: the tests' lines end PASS or FAIL and a passed test says so in a notice");
    }

    // ------------------------------------------------------------------ demolition

    private static void DemolitionRuleChecks()
    {
        // The works: by floor area, bounded, never fewer for a larger building.
        bool rising = true, bounded = true;
        for (int area = 0; area <= 20000; area += 250)
        {
            int h = RedevelopRules.DemolitionHours(area);
            bounded &= h >= RedevelopRules.DemolishHoursLeast && h <= RedevelopRules.DemolishHoursMost;
            rising &= area == 0 || h >= RedevelopRules.DemolitionHours(area - 250);
        }
        Check(bounded && rising && RedevelopRules.DemolitionHours(2000) == 60 && RedevelopRules.DemolitionHours(100) == RedevelopRules.DemolishHoursLeast
              && RedevelopRules.DemolitionHours(50000) == RedevelopRules.DemolishHoursMost, "demolition: its working hours grow with the floor area, inside their bounds (2,000 m2 in 60)");
        Check(RedevelopRules.RubbleLoads(0) == 1 && RedevelopRules.RubbleLoads(100) == 1 && RedevelopRules.RubbleLoads(101) == 2 && RedevelopRules.RubbleLoads(2000) == 20
              && RedevelopRules.RubbleCost(2000, 2000) == 2L * RedevelopRules.RubbleCost(2000, 1000), "demolition: a lorry load of rubble for each hundred square metres, at today's prices");
        Check(RedevelopRules.PermitFeeOf(0L, 1000) == RedevelopRules.PermitFee && RedevelopRules.PermitFeeOf(2000000L, 1000) == RedevelopRules.PermitFee + 2000L
              && RedevelopRules.PermitFeeOf(-5L, 2000) == 2L * RedevelopRules.PermitFee, "demolition: the permit's fee at today's prices and a thousandth of the building's value");
        Check(RedevelopRules.PermitDays(0) == RedevelopRules.PermitDaysLeast && RedevelopRules.PermitDays(100000) == RedevelopRules.PermitDaysMost
              && RedevelopRules.PermitDays(RedevelopRules.PermitArea) == RedevelopRules.PermitDaysLeast + 1, "demolition: the permit is decided in a few working days, one more for a large building");
        Check(RedevelopRules.BuyOut(0.0, 0.0, false) == 0L && RedevelopRules.BuyOut(100.0, 500.0, false) == 100L * RedevelopRules.TenantDays
              && RedevelopRules.BuyOut(100.0, 500.0, true) == 100L * RedevelopRules.TenantDays + 500L * RedevelopRules.BusinessDays && RedevelopRules.BuyOut(-4.0, 500.0, false) == 0L
              && RedevelopRules.BuyOut(double.NaN, 0.0, false) == 0L && RedevelopRules.BuyOut(0.0, 300.0, false) == 0L, "demolition: the tenants are bought out at their days' rent and a business at the unit's, nothing for an empty building");
        Check(RedevelopRules.NoticeEnds(40) == 40 + RedevelopRules.NoticeDays && RedevelopRules.LookDownShare > 0.5f && RedevelopRules.LookDownShare < 0.8f,
            "demolition: the notice runs its days; the building comes down behind its wrap in the works' fourth of five stages");
        RefitQuote a = RedevelopRules.Quote(77, 30, 2400, 1000), b = RedevelopRules.Quote(77, 30, 2400, 1000);
        Check(a.Rival == b.Rival && a.Price == b.Price && a.Days == b.Days && a.Rival >= 0 && a.Rival < BuildCatalogue.RivalCount && a.Price > 0 && a.Days >= 1
              && a.Hours == RedevelopRules.DemolitionHours(2400), "demolition: the contractor's quote is the same for the same seed, day and building, one of the city's builders");
        // The works' tables have the demolition's row: no materials in, the digger and the scaffold, its speciality.
        int k = RedevelopRules.DemolitionWorks;
        Check(BuildCatalogue.WorksRate.Length == k + 1 && BuildCatalogue.WorksLoads.Length == k + 1 && BuildCatalogue.WorksCrew.Length == k + 1 && BuildCatalogue.WorksPlant.Length == k + 1
              && BuildCatalogue.WorksPlant[k][(int)Plant.Digger] == 1 && BuildCatalogue.WorksPlant[k][(int)Plant.Scaffold] == 1 && Array.TrueForAll(BuildCatalogue.WorksLoads[k], x => x == 0),
            "demolition: the programme's works tables have its row: its rate, no materials in, its crew, the digger and the scaffold");
        Check(TenderRules.NeedOf(TenderSource.Client, k, BuildUse.Shops) == Speciality.Demolition && FirmRules.Special(Speciality.Demolition, ProjectKind.Contract, BuildUse.Shops, k)
              && !FirmRules.Special(Speciality.Demolition, ProjectKind.Contract, BuildUse.Shops, RefitRules.Works) && FirmRules.Special(Speciality.Demolition, ProjectKind.Rebuild, BuildUse.Shops, 4),
            "demolition: its works are the demolition speciality's, and that speciality is no refit's");
        // A lot knocked down takes a building of any size of the grid on no more of its spaces than it holds, not only its plan's.
        var planned = new RealLifeOverhaul.Uptown.BuildingsLot
        {
            Id = "uptown_1_02_0003", Spaces = 3, Sizes = new[] { "ba:buildingsize_c" }, Version = 1, Width = 21, Depth = 35, Class = RealLifeOverhaul.Uptown.BuildingsClass.Small
        };
        string asPlanned = RealLifeOverhaul.Uptown.BuildingsRules.FinishRefusal(planned, true, false, "ba:buildingsize_b", 1, "ba:buildingtype_retail", 0, RealLifeOverhaul.Uptown.FinishOwner.City, 0, true, true, false, false, false);
        string freed = RealLifeOverhaul.Uptown.BuildingsRules.FinishRefusal(planned, true, false, "ba:buildingsize_b", 1, "ba:buildingtype_retail", 0, RealLifeOverhaul.Uptown.FinishOwner.City, 0, true, true, false, false, true);
        string wide = RealLifeOverhaul.Uptown.BuildingsRules.FinishRefusal(planned, true, false, "ba:buildingsize_p", 1, "ba:buildingtype_warehouse", 0, RealLifeOverhaul.Uptown.FinishOwner.City, 0, true, true, false, false, true);
        Check(asPlanned != null && freed == null && wide != null, "demolition: a lot knocked down takes a building of any size that stands on its spaces, never one wider; a lot still as planned takes its plan's size alone");
        var p = new ProjectFile { Kind = ProjectKind.Demolition, CivicKind = k, Contractor = 1 };
        Check(p.Model == ProjectKind.Contract && !p.Hired && PlansRules.InspectionAfter(p.Model, WorkStage.Frame) < 0 && PlansRules.InspectionAfter(p.Model, WorkStage.Envelope) == (int)Inspection.Final,
            "demolition: worked as the programme's works with one final inspection; a contractor's is paid at once, never a hired build");
    }

    private static void DemolitionStoreChecks()
    {
        var s = new BuildState { Seed = 3, NextId = 40 };
        s.Projects.Add(new ProjectFile { Id = 31, Kind = ProjectKind.Demolition, Stage = ProjectStage.Ready, Lot = "uptown_2_07_0012", Street = "reallife:street_uptown_x", Number = 12,
            CivicKind = RedevelopRules.DemolitionWorks, Storeys = 5, Hours = 60, NoticeUntil = 44, BuyOut = 123456 });
        s.Projects.Add(new ProjectFile { Id = 32, Kind = ProjectKind.Demolition, Stage = ProjectStage.Design, Storeys = 2, Hours = 40, NoticeUntil = 50, BuyOut = -1 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int _);
        ProjectFile a = back != null ? back.Project(31) : null, b = back != null ? back.Project(32) : null;
        Check(a != null && a.Kind == ProjectKind.Demolition && a.CivicKind == RedevelopRules.DemolitionWorks && a.NoticeUntil == 44 && a.BuyOut == 123456 && b != null && b.BuyOut == -1
              && BuildStore.Write(back) == line, "demolition: a demolition, its works' kind, its notice and its buy-out come back from the save, the line as it was");
        string later = line.Replace("|4|2|0|12|", "|9|2|0|12|");
        BuildState clamped = BuildStore.Read(later, out int _);
        Check(later != line && clamped != null && clamped.Project(31) != null && clamped.Project(31).Kind == ProjectKind.Demolition, "demolition: a project of a kind this version does not know reads as its last kind");
        // Uptown's line: the cleared lots come back; a line from before them has none; what is no lot id is left out.
        var life = new RealLifeOverhaul.Uptown.LifeState();
        life.MarkCleared("uptown_1_02_0003");
        life.MarkCleared("uptown_1_02_0004");
        life.MarkCleared("uptown_1_02_0003");
        string uptown = RealLifeOverhaul.Uptown.LifeStore.Write(life);
        RealLifeOverhaul.Uptown.LifeState read = RealLifeOverhaul.Uptown.LifeStore.Read(uptown, out int _);
        Check(read != null && read.Cleared.Count == 2 && read.IsCleared("uptown_1_02_0004") && !read.IsCleared("uptown_1_02_0005") && RealLifeOverhaul.Uptown.LifeStore.Write(read) == uptown,
            "demolition: the district's cleared lots come back from its line, each once, the line as it was");
        RealLifeOverhaul.Uptown.LifeState none = RealLifeOverhaul.Uptown.LifeStore.Read("v=1^f:^g:" + RealLifeOverhaul.Uptown.LayoutRules.PlanGeneration, out int _);
        RealLifeOverhaul.Uptown.LifeState odd = RealLifeOverhaul.Uptown.LifeStore.Read("v=1^f:^c:uptown_1_02_0003,bad id;x,^g:4", out int _);
        Check(none != null && none.Cleared.Count == 0 && odd != null && odd.Cleared.Count == 1 && odd.IsCleared("uptown_1_02_0003"),
            "demolition: a district line from before demolitions has no cleared lot; what is no lot id is passed over");
        // The district save lets go what it kept for one address alone.
        var records = new List<RealLifeOverhaul.DistrictSave.StoredRecord>();
        var items = new List<object>();
        foreach (int number in new[] { 12, 14, 12 })
        {
            var r = new RealLifeOverhaul.DistrictSave.StoredRecord(RealLifeOverhaul.DistrictSave.SaveKind.Registration, records.Count, null);
            r.Addresses.Add(new RealLifeOverhaul.DistrictSave.PlaceKey("reallife:street_uptown_x", number));
            records.Add(r);
            items.Add(number);
        }
        int gone = RealLifeOverhaul.DistrictSave.DistrictSaveStore.RetireAddress(records, items, "reallife:street_uptown_x", 12);
        Check(gone == 2 && records.Count == 1 && items.Count == 1 && (int)items[0] == 14, "demolition: the district save lets go every record kept for the address knocked down, and only those");
    }

    private static void DemolitionSourceChecks()
    {
        string cleared = ModSource(@"Uptown\LifeDirector.Cleared.cs"), life = ModSource(@"Uptown\LifeDirector.cs"), clear = ModSource(@"DistrictSave\DistrictSaveDirector.Clear.cs");
        string demolish = ModSource(@"Construction\ConstructionDirector.Demolish.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string estate = ModSource(@"Construction\ConstructionGame.Estate.cs"), buildings = ModSource(@"Uptown\LifeGame.Buildings.cs"), app = ModSource(@"Construction\ConstructionApp.Redevelop.cs");
        Check(cleared.Length > 0 && clear.Length > 0 && demolish.Length > 0 && estate.Length > 0 && app.Length > 0, "demolition sources: the files are where the checks look");
        // Knocked down in the district: every record of its address let go for good, out of the lists, hidden, walked on, cleared.
        string down = MethodText(cleared, "public static bool Demolish(");
        Check(down.Contains("DistrictSaveDirector.Clear(made.Spec.Street, made.Spec.Number") && down.Contains("LifeGame.Leave(made, ClearSpan)") && down.Contains("CopyGame.Show(made.Controller, false)")
              && down.Contains("_state.MarkCleared(id)") && down.Contains("WriteState()") && down.Contains("DistrictPlaces.Changed()") && down.Contains("KeptFromRedevelopment(made)") && down.Contains("LifeGame.Inside(made)"),
            "demolition sources: a building knocked down lets go its records for good, leaves the game's lists, is hidden and its lots cleared; never one of the district's own places, never with the player inside");
        string kept = MethodText(cleared, "private static bool KeptFromRedevelopment(");
        Check(kept.Contains("LeaseOf(lot)") && kept.Contains("BigPlacesRules.Holds(lot)") && kept.Contains("SpecialsRules.At(lot)") && kept.Contains("SpecialBuildings.TakenBy(") && kept.Contains("DistrictBuildings.KeptOpen"),
            "demolition sources: the rival builders' offices, the big places, the special places, the mod's own places and the landmarks are never knocked down");
        string leave = MethodText(buildings, "public static int Leave(");
        Check(leave.Contains("BuildingHelper.allBuildings.Remove(record)") && leave.Contains("district.Remove(record)") && leave.Contains("cut.enabled = false") && leave.Contains("HideFillersOn(lots)"),
            "demolition sources: the building leaves the game's lists, its ground is walked on again and its fillers are hidden");
        Check(life.Contains("if (!Unbuilt(Plan[Built[i]].Id))") && life.Contains("if (f == null && Unbuilt(lot.Id))"), "demolition sources: a lot knocked down, or under a building finished over it, stands nothing of its own at the next load");
        string strip = MethodText(clear, "public static int Clear(");
        Check(strip.Contains("DistrictSaveRules.IsModId(street)") && strip.Contains("PlaceSet.Scoped().Address(street, number).Door(") && strip.Contains("DistrictSaveGame.TakeOut(save, places, true)")
              && strip.Contains("s.PutBacks.Clear()") && strip.Contains("DistrictSaveGame.Down(s)") && strip.Contains("DistrictSaveStore.RetireAddress(KeptRecords, KeptItems, street, number)") && !strip.Contains("KeptRecords.Add("),
            "demolition sources: the district save takes one address's records out for good and keeps none, never an address of the game's");
        // The project: refused in a session and for anything not the player's; notice with the permit; empty before the works.
        string block = MethodText(demolish, "public string DemolishBlock(");
        Check(block.Contains("GoingPublic.InSession") && block.Contains("b.Kept") && block.Contains("ConstructionGame.Owns(street, number)") && block.Contains("Busy(street, number, b.Lot)")
              && block.Contains("ConstructionGame.Occupied(street, number)"), "demolition sources: only a building of the player's in Uptown, empty of their own things, outside a Going Public session");
        Check(MethodText(projects, "public string Apply(").Contains("GiveNotice(p, day)") && MethodText(projects, "public string StartBlock(").Contains("!Emptied(p)")
              && MethodText(projects, "public long PlanningFee(").Contains("RedevelopRules.PermitFeeOf(") && MethodText(projects, "private void Decide(").Contains("p.Kind != ProjectKind.Demolition"),
            "demolition sources: the permit is the planning application's path, with its own fee and days, the notice given with it, no requirements of a building to meet, and the works wait for an empty building");
        string settle = MethodText(demolish, "private void SettleNotice(");
        Check(settle.Contains("RedevelopRules.BuyOut(") && settle.Contains("ConstructionGame.Evict(") && settle.Contains("ConstructionGame.Force(cost") && settle.Contains("p.BuyOut = cost > 0L ? cost : -1L"),
            "demolition sources: at the notice's end the business closes, the tenants are bought out by the rule and it is paid");
        string evict = MethodText(estate, "internal static bool Evict(");
        Check(evict.Contains("reg.ShutDownAIBusiness()") && evict.Contains("!reg.RentedByPlayer") && evict.Contains("!GoingPublic.Foreign(reg)") && evict.Contains("reg.AvailableForRent = false"),
            "demolition sources: only a business of the city's is closed, by the game's own call, and the unit kept off the market; never the player's or another player's");
        string done = MethodText(demolish, "private bool Demolished(");
        Check(done.Contains("ConstructionBridges.Demolish(p.Lot)") && done.Contains("land.Spaces = BuildCatalogue.Clamp(lots, 1, BuildCatalogue.SiteSpacesMost)") && done.Contains("p.InDistrict = true"),
            "demolition sources: at the handover the district knocks it down and its ground is the player's land, one site of all its lots");
        Check(MethodText(demolish, "private void RedevelopSecond(").Contains("RedevelopRules.LookDownShare") && MethodText(projects, "public string Abandon(").Contains("DemolitionGivenUp(p)"),
            "demolition sources: the building's look goes as it comes down behind the wrap, and comes back if the works are given up");
        // Civic: the site's kind after the six, read by an older version as its last, heard next door, set out at a building.
        Check((int)SiteKind.Demolition == 6 && ModSource(@"Civic\CivicState.cs").Contains("Math.Min((int)SiteKind.Demolition, r.Int())") && new SiteState { Kind = SiteKind.Demolition, Needed = 10f, Done = 3f }.Noisy
              && !new SiteState { Kind = SiteKind.Demolition, Needed = 10f, Done = 0f }.Noisy, "demolition sources: the site's kind is the seventh, saved as a number, its works heard next door after the hoarding");
        Check(ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("kind == (int)SiteKind.Demolition ? SiteKind.Demolition") && ModSource(@"Civic\SiteDresser.cs").Contains("case SiteKind.Demolition: BuildDemolition(dressed); break;")
              && ModSource(@"Civic\SiteDresser.Place.cs").Contains("site.Kind == SiteKind.Demolition) ? ResolveWrap(dressed)"), "demolition sources: its site is set out at the building, wrapped in the game's scaffolding and dressed as a demolition");
        WrapRules.DemolitionStage(0, 6, out int l0, out bool f0);
        WrapRules.DemolitionStage(2, 6, out int l2, out bool f2);
        WrapRules.DemolitionStage(4, 6, out int l4, out bool f4);
        Check(l0 == 0 && !f0 && l2 == 6 && f2 && l4 == 0 && !f4, "demolition sources: the wrap is put up, closed to the roof for the strip-out and gone once the building is down");
        foreach (string src in new[] { cleared, clear, demolish, estate, app, ModSource(@"Construction\RedevelopRules.cs") })
        {
            Check(!Regex.IsMatch(src, @"using System\.Linq|\.Select\(|\.Where\(|\.Any\(|\.First\("), "demolition sources: no LINQ in demolition's sources");
        }
        Check(!Regex.IsMatch(ModSource(@"Construction\RedevelopRules.cs"), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\."), "demolition sources: redevelopment's rules are game-free and draw only from their seed");
        string guide = ModSource(@"Construction\ConstructionGuide.cs"), tests = ModSource(@"Construction\ConstructionDirector.RedevelopTests.cs");
        Check(tests.Contains("public static bool TestDemolitionNow()") && guide.Contains(".Test(\"reallife_bld_test_demolish\", () => ConstructionDirector.TestDemolitionNow())")
              && ModSource(@"TestRun\RunPlan.cs").Contains("{ \"reallife_bld_test_demolish\","), "demolition sources: its test is a button only, left out of the test run with why: it knocks a building of the player's down for good");
    }

    // ------------------------------------------------------------------ building new on cleared ground

    private static void ClearedBuildChecks()
    {
        // A cleared site offers the city's designs that fill its spaces, of any size that does, never wider.
        Check(BuildCatalogue.Offered(3, 3, false) && BuildCatalogue.Offered(3, 2, true) && !BuildCatalogue.Offered(3, 2, false) && !BuildCatalogue.Offered(3, 4, true),
            "cleared ground: a site offers a design of its own spaces, or of fewer that fills its frontage, never one wider");
        string lots = ModSource(@"Uptown\LifeDirector.Lots.cs"), land = ModSource(@"Construction\ConstructionApp.Land.cs"), demolish = ModSource(@"Construction\ConstructionDirector.Demolish.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.RedevelopTests.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs");
        Check(MethodText(lots, "public static bool FinishOver(").Contains("spanned, Cleared(lotId))"), "cleared ground: the district takes a building of any size that fits on a lot knocked down");
        Check(MethodText(demolish, "public bool ClearedGround(").Contains("_state.Lot(lot) != null && AskCleared(lot)") && land.Contains("_director.ClearedGround(lot.Id) ? \"reallife_bld_land_state_cleared\""),
            "cleared ground: the Land tab says the player's ground is cleared and ready to build on");
        string build = MethodText(tests, "private bool ClearedBuildNow(");
        Check(tests.Contains("public static bool TestClearedBuildNow()") && build.Contains("NewPlot(lot, out string why)") && build.Contains("HireBuilder(p, CommissionRules.Cheapest(_quotes, count), true)")
              && build.Contains("p.InDistrict") && guide.Contains(".Test(\"reallife_bld_test_cleared_build\", () => ConstructionDirector.TestClearedBuildNow())")
              && ModSource(@"TestRun\RunPlan.cs").Contains("{ \"reallife_bld_test_cleared_build\","), "cleared ground: its test plans, builds and hands a building to the district on the player's cleared ground by the usual path, a button only");
    }

    // ------------------------------------------------------------------ the texts

    private static void RedevelopTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { @"Construction\ConstructionApp.Refit.cs", @"Construction\ConstructionDirector.RedevelopTests.cs", @"Construction\ConstructionDirector.Refits.cs",
                     @"Construction\ConstructionDirector.Demolish.cs", @"Construction\ConstructionApp.Redevelop.cs", @"Construction\ConstructionGame.Estate.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        Check(keys.Count > 30, "redevelopment texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("redevelopment texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            int missing = 0, styled = 0;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "redevelopment texts: " + key + " is in " + codes[l] + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    styled++;
                    Check(false, "redevelopment texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && !SameNames(en, value))
                {
                    styled++;
                    Check(false, "redevelopment texts: " + key + " has the English names in curly brackets in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0, "redevelopment texts: every key of redevelopment is in " + codes[l] + ".json, in the style");
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            Check(page.Contains("{bld_refit_hours_least}") && page.Split('\n').Length >= 92, "redevelopment texts: the Guide's construction page in " + codes[l] + " has the refurnishing's lines");
        }
        keys.Add("reallife_transaction_bld_demolition_label");
        keys.Add("reallife_transaction_bld_buyout_label");
        keys.Add("reallife_opt_tip_construction_redevelop");
        keys.Add("reallife_bld_redevelop_option");
        foreach (string notice in new[] { "reallife_bld_test_passed", "reallife_bld_test_no_unit", "reallife_bld_test_session_city", "reallife_bld_test_no_city_business", "reallife_bld_test_failed_log",
                     "reallife_bld_test_no_uptown", "reallife_bld_notice_demolished", "reallife_bld_notice_emptied", "reallife_bld_notice_notice_given", "reallife_bld_test_no_cleared" })
        {
            Check(words[0].TryGetValue(notice, out string en) && en.Length <= 240, "redevelopment texts: the notice " + notice + " is at most 240 characters");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Refurnish any unit you rent or own" }, { "pt", "Remobilie qualquer unidade que você aluga ou tem" }, { "zh-cn", "可以重新布置你租用或拥有的任何单元" },
            { "de", "Richte jede Einheit neu ein" }, { "fr", "Réaménagez toute unité que vous louez ou possédez" }, { "es", "Vuelve a amueblar cualquier unidad" },
            { "ru", "Обставьте заново любое помещение" }, { "nl", "Richt elke eenheid die je huurt of bezit opnieuw in" }, { "tr", "Kiraladığınız ya da sahip olduğunuz her birimi" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(starts.TryGetValue(codes[l], out string start) && WhatsNewHas(words[l], start), "redevelopment texts: What's new has refurnishing's line in " + codes[l] + " (any of its rows)");
        }
        var built = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Build a new building on ground you cleared" }, { "pt", "Construa um prédio novo no terreno que você limpou" }, { "zh-cn", "可以在你清空的地块上建造新楼" },
            { "de", "Bau ein neues Gebäude auf dem Grund, den du geräumt hast" }, { "fr", "Construisez un nouveau bâtiment sur le terrain que vous avez dégagé" },
            { "es", "Construye un edificio nuevo en el terreno que despejaste" }, { "ru", "Постройте новое здание на расчищенном участке" },
            { "nl", "Bouw een nieuw gebouw op grond die je hebt vrijgemaakt" }, { "tr", "Temizlediğiniz arsaya yeni bir bina yapın" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(built.TryGetValue(codes[l], out string start) && WhatsNewHas(words[l], start), "redevelopment texts: What's new has the line for building on cleared ground in " + codes[l]);
        }
        var down = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Knock down a building of yours in Uptown" }, { "pt", "Demola um prédio seu em Uptown" }, { "zh-cn", "可以拆除你在上城的建筑" }, { "de", "Reiß ein Gebäude ab, das dir in Uptown gehört" },
            { "fr", "Démolissez un bâtiment qui vous appartient à Uptown" }, { "es", "Derriba un edificio tuyo en Uptown" }, { "ru", "Снесите своё здание в Аптауне" },
            { "nl", "Sloop een gebouw van je in Uptown" }, { "tr", "Uptown'daki size ait bir binayı yıkın" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(down.TryGetValue(codes[l], out string start) && WhatsNewHas(words[l], start), "redevelopment texts: What's new has demolition's line in " + codes[l] + " (any of its rows)");
        }
        string guideSource = ModSource(@"Construction\ConstructionGuide.cs");
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            Check(page.Contains("{bld_permit_days_most}") && page.Contains("{bld_notice_days}") && page.Contains("{bld_buyout_days}"), "redevelopment texts: the Guide's construction page in " + codes[l] + " has demolition's figures");
        }
        foreach (string fact in new[] { "bld_permit_days_most", "bld_notice_days", "bld_buyout_days" })
        {
            Check(guideSource.Contains(".Fact(\"" + fact + "\""), "redevelopment texts: the Guide's figure {" + fact + "} is a fact of the feature list");
        }
    }
}
