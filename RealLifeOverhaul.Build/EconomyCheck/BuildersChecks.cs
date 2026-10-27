using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using BuildTrade = RealLifeOverhaul.Construction.Trade;

// The construction game (Scripts/Construction): its catalogue; the land's price, zoning and taxes; the plans, the
// requirements and the estimate; planning permission and the appeal; a site's pace, its inspections, deliveries and
// risks; the money (the loan's draws, interest and instalments, investors, flats sold off plan); tenders and the rival
// builders; the city's developers, the same on every machine; the frame's footprint against the city's plots; the store's
// line read back whole; then, over the sources, the layering, the wiring (the building trades' three bridges among them),
// the options, the tests' check lines, and the texts in every language.
internal static partial class Program
{
    private static void Builders()
    {
        BuildCatalogueChecks();
        BuildLandChecks();
        BuildPlansChecks();
        BuildPlanningChecks();
        BuildSiteChecks();
        BuildMoneyChecks();
        BuildTenderChecks();
        BuildDeveloperChecks();
        BuildFrameChecks();
        SiteFitChecks();
        BuildStoreChecks();
        BuildSourceChecks();
        BuildTextChecks();
    }

    private static void BuildCatalogueChecks()
    {
        Check(BuildCatalogue.UseTypes.Length == (int)BuildUse.Count && BuildCatalogue.UseKeys.Length == (int)BuildUse.Count && BuildCatalogue.UseMaterialsPermille.Length == (int)BuildUse.Count
              && BuildCatalogue.HoursPerHundred.Length == (int)BuildUse.Count && BuildCatalogue.ParkingPerSpace.Length == (int)BuildUse.Count,
            "builders: every use has its game type, its words, its materials, its hours and its parking");
        for (int u = 0; u < (int)BuildUse.Count; u++)
        {
            Check(BuildCatalogue.UseOf(BuildCatalogue.UseTypes[u]) == u, "builders: the game's type " + BuildCatalogue.UseTypes[u] + " reads back as its use");
        }
        Check(BuildCatalogue.UseOf("ba:buildingtype_special") < 0 && BuildCatalogue.UseOf(null) < 0, "builders: a type the company does not build is no use");
        Check(BuildCatalogue.MaterialKeys.Length == (int)Material.Count && BuildCatalogue.ExchangeItem.Length == (int)Material.Count && BuildCatalogue.LoadPrice.Length == (int)Material.Count,
            "builders: every material has its words, its item on the exchange and its price");
        Check(BuildCatalogue.PlantKeys.Length == (int)Plant.Count && BuildCatalogue.HireDay.Length == (int)Plant.Count && BuildCatalogue.BuyPrice.Length == (int)Plant.Count,
            "builders: every piece of plant has its words, its hire and its price");
        Check(BuildCatalogue.TradeKeys.Length == (int)BuildTrade.Count && BuildCatalogue.TradeIds.Length == (int)BuildTrade.Count && BuildCatalogue.TradeRate.Length == (int)BuildTrade.Count,
            "builders: every trade has its words, its id and its rate");
        Check(BuildCatalogue.RoleKeys.Length == (int)StaffRole.Count && BuildCatalogue.RoleNoteKeys.Length == (int)StaffRole.Count && BuildCatalogue.RoleIds.Length == (int)StaffRole.Count,
            "builders: every role of the office has its words, its note and its id");
        Check(BuildCatalogue.SpecialityKeys.Length == (int)Speciality.Count && BuildCatalogue.CoverKeys.Length == BuildCatalogue.CoverDayPerMillion.Length
              && BuildCatalogue.CoverTheftPermille.Length == BuildCatalogue.CoverKeys.Length && BuildCatalogue.CoverClaimPermille.Length == BuildCatalogue.CoverKeys.Length
              && BuildCatalogue.CoverStorm.Length == BuildCatalogue.CoverKeys.Length, "builders: every speciality and every level of cover has its words and its figures");
        Check(BuildCatalogue.StageKeys.Length == (int)WorkStage.Count && BuildCatalogue.StageLoads.Length == (int)WorkStage.Count && BuildCatalogue.StageCrew.Length == (int)WorkStage.Count
              && BuildCatalogue.StagePlant.Length == (int)WorkStage.Count && BuildCatalogue.InspectionKeys.Length == (int)Inspection.Count
              && BuildCatalogue.InspectionFee.Length == BuildCatalogue.InspectionCount && BuildCatalogue.InspectionCount == (int)Inspection.Count,
            "builders: every stage has its words, loads, crew and plant; every inspection its words and fee");
        for (int s = 0; s < (int)WorkStage.Count; s++)
        {
            Check(BuildCatalogue.StageLoads[s].Length == (int)Material.Count && BuildCatalogue.StageCrew[s].Length == (int)BuildTrade.Count && BuildCatalogue.StagePlant[s].Length == (int)Plant.Count,
                "builders: stage " + (WorkStage)s + "'s tables have a column for every material, trade and piece of plant");
        }
        Check(BuildCatalogue.Gangs.Length == BuildCatalogue.GangCount && BuildCatalogue.GangKeys.Length == BuildCatalogue.GangCount, "builders: every gang has its people and its words");
        foreach (int[] gang in BuildCatalogue.Gangs)
        {
            Check(gang.Length == (int)BuildTrade.Count, "builders: a gang names its people of every trade");
        }
        Check(BuildCatalogue.FirmKeys.Length == BuildCatalogue.FirmCount && BuildCatalogue.FirmFeePermille.Length == BuildCatalogue.FirmCount && BuildCatalogue.FirmDays.Length == BuildCatalogue.FirmCount
              && BuildCatalogue.FirmQuality.Length == BuildCatalogue.FirmCount, "builders: every practice has its name, fee, days and quality");
        Check(BuildCatalogue.FirmFeePermille[0] < BuildCatalogue.FirmFeePermille[2] && BuildCatalogue.FirmQuality[0] < BuildCatalogue.FirmQuality[2], "builders: a dearer practice draws better plans");
        Check(BuildCatalogue.MerchantKeys.Length == BuildCatalogue.MerchantCount && BuildCatalogue.MerchantPricePermille.Length == BuildCatalogue.MerchantCount
              && BuildCatalogue.MerchantDays.Length == BuildCatalogue.MerchantCount && BuildCatalogue.MerchantShortPermille.Length == BuildCatalogue.MerchantCount,
            "builders: every builders' merchant has its name, prices, days and short deliveries");
        Check(BuildCatalogue.InvestorKeys.Length == BuildCatalogue.InvestorCount && BuildCatalogue.InvestorSharePermille.Length == BuildCatalogue.InvestorCount
              && BuildCatalogue.InvestorReturnPermille.Length == BuildCatalogue.InvestorCount, "builders: every investor has its name, share and return");
        Check(BuildCatalogue.RivalKeys.Length == BuildCatalogue.RivalCount && BuildCatalogue.RivalSpeciality.Length == BuildCatalogue.RivalCount
              && BuildCatalogue.RivalReputation.Length == BuildCatalogue.RivalCount && BuildCatalogue.RivalMarginPermille.Length == BuildCatalogue.RivalCount
              && BuildCatalogue.RivalCrews.Length == BuildCatalogue.RivalCount, "builders: every rival builder has its name, speciality, standing, margin and crews");
        Check(BuildCatalogue.ClassKeys.Length == BuildCatalogue.ClassCount && BuildCatalogue.ClassZones.Length == BuildCatalogue.ClassCount && BuildCatalogue.ClassUse.Length == BuildCatalogue.ClassCount
              && BuildCatalogue.PlotRatioPermille.Length == BuildCatalogue.ClassCount, "builders: every class of lot has its words, its zoning, its usual use and its plot ratio");
        for (int c = 0; c < BuildCatalogue.ClassCount; c++)
        {
            Check(LandRules.Zoned(BuildCatalogue.ClassZones[c], BuildCatalogue.ClassUse[c]), "builders: a lot of class " + c + " is zoned for its usual use");
        }
        Check(BuildCatalogue.LoanDrawPermille.Length == (int)Inspection.Count + 1, "builders: the loan is drawn at the start and after each inspection");
        int draws = 0;
        foreach (int d in BuildCatalogue.LoanDrawPermille)
        {
            draws += d;
        }
        Check(draws == 1000, "builders: the loan's draws add up to the whole of it");
        Check(BuildCatalogue.AskPermille.Length == BuildCatalogue.AskChancePermille.Length && BuildCatalogue.AskChancePermille[0] > BuildCatalogue.AskChancePermille[2],
            "builders: a dearer asking price for a flat finds a buyer less often");
        Check(BuildCatalogue.HeightDistricts.Length == BuildCatalogue.HeightLimits.Length, "builders: every district's height limit is given");
        Check(BuildCatalogue.SitesMost == CivicCatalogue.SitesMost, "builders: the city's sites at once are counted the same in both (" + BuildCatalogue.SitesMost + ")");
        Check(BuildCatalogue.BuildingMost + BuildCatalogue.DevSitesMost + CivicCatalogue.ActiveSites <= CivicCatalogue.SitesMost,
            "builders: the company's sites, the developers' and the city's own six fit in what the city holds at once");
        // The roles and trades the company gives people are the building trades' own ids (docs/features/trades.md).
        for (int r = 1; r < BuildCatalogue.RoleIds.Length; r++)
        {
            Check(RealLifeOverhaul.BuildTrades.TradeCatalogue.Role(BuildCatalogue.RoleIds[r]) != null, "builders: the role '" + BuildCatalogue.RoleIds[r] + "' is one of the trades' roles");
        }
        foreach (string id in BuildCatalogue.TradeIds)
        {
            Check(RealLifeOverhaul.BuildTrades.TradeCatalogue.Role(id) != null, "builders: the trade '" + id + "' is one of the trades' roles");
        }
        Check(RealLifeOverhaul.BuildTrades.TradeCatalogue.Role("architect") != null && RealLifeOverhaul.BuildTrades.TradeCatalogue.Role("engineer") != null,
            "builders: the two qualifications the company asks of the player are roles the trades know");
    }

    private static void BuildLandChecks()
    {
        // The game's own value of a building (Building.GetMarketPricePerSqm): price x type x (1.3 - (100 - traffic) / 100).
        double uptown = 20554.0 * 1.75;   // the district's base price and multiplier (docs/features/uptown.md)
        double sqm = LandRules.PricePerSqm(uptown, 1.0, 30, false);
        Check(Math.Abs(sqm - uptown * 0.6) < 0.01, "builders: a square metre at traffic 30 is the game's 0.6 of the district's price (" + sqm.ToString("0.0") + ")");
        Check(Math.Abs(LandRules.PricePerSqm(uptown, 1.0, 100, false) - uptown * 1.3) < 0.01 && LandRules.PricePerSqm(uptown, 1.0, 60, false) > sqm,
            "builders: more traffic, a dearer square metre, 1.3 times the price at the most");
        Check(Math.Abs(LandRules.PricePerSqm(1000.0, 2.0, 30, true) - 2000.0) < 0.01 && LandRules.PricePerSqm(1000.0, 2.0, 10, true) > 2000.0,
            "builders: a type whose value falls with traffic is priced as the game prices it");
        long value = LandRules.Value(1000, sqm);
        Check(value == (long)Math.Round(1000 * sqm), "builders: a building's value is its floor area at the square metre's price");
        long lot = LandRules.LotPrice(24, 30, 0, sqm);
        long usual = LandRules.Value(LandRules.UsualArea(24, 30, 0), sqm);
        Check(lot % BuildCatalogue.LandRound == 0 && Math.Abs(lot - usual * BuildCatalogue.LandSharePermille / 1000) <= BuildCatalogue.LandRound,
            "builders: a lot costs " + BuildCatalogue.LandSharePermille / 10 + "% of its usual building's value, in thousands (" + lot + " of " + usual + ")");
        Check(LandRules.LotPrice(24, 30, 1, sqm) > lot, "builders: an office lot's usual building is bigger, so it costs more");
        for (int day = 1; day <= 30; day++)
        {
            int council = LandRules.CouncilDay(day);
            Check(LandRules.Weekday(council) == BuildCatalogue.CouncilWeekday && council - day >= BuildCatalogue.ZoneDaysLeast && council - day < BuildCatalogue.ZoneDaysLeast + 7,
                "builders: a request made on day " + day + " is heard at the council's next meeting, day " + council);
        }
        Check(LandRules.Weekday(1) == 0 && LandRules.Weekday(7) == 6 && LandRules.Weekday(8) == 0, "builders: the game's day 1 is a Monday");
        int tax = LandRules.LandTax(1000000, 7, 112, false), vacant = LandRules.LandTax(1000000, 7, 112, true);
        Check(tax == 1000000L * BuildCatalogue.LandTaxPermille * 7 / (1000L * 112) && vacant > tax, "builders: a week's land tax is its share of a year, more on land kept empty");
        Check(LandRules.LandTax(0, 7, 112, true) == 0 && LandRules.LandTax(1000, 0, 112, false) == 0, "builders: no land, or no days, no tax");
        Check(!LandRules.Empty(0, 100) && !LandRules.Empty(80, 100) && LandRules.Empty(70, 100), "builders: land counts as empty after " + BuildCatalogue.VacantAfter + " days without works");
        Check(LandRules.Flats(10) == BuildCatalogue.FlatsLeast && LandRules.Flats(850) == 10, "builders: a block holds a flat for every " + BuildCatalogue.FlatArea + " m2, two at least");
        int flat = LandRules.FlatPrice(10000000, 10, 60, 1);
        Check(flat == 1000000 && LandRules.FlatPrice(10000000, 10, 60, 2) > flat && LandRules.FlatPrice(10000000, 10, 90, 1) > flat,
            "builders: a flat is its share of the building's value, more at a dearer asking price or for better work");
        int offPlan = LandRules.OffPlanPrice(10000000, 10, 60);
        Check(offPlan == flat * BuildCatalogue.OffPlanPermille / 1000 && LandRules.Deposit(offPlan) == offPlan * BuildCatalogue.DepositPermille / 1000,
            "builders: a flat sold before it is built goes at a discount, a deposit now");
        Check(LandRules.BuyerChance(0, 100) <= 900 && LandRules.BuyerChance(2, 0) >= 50 && LandRules.BuyerChance(0, 50) > LandRules.BuyerChance(2, 50),
            "builders: a buyer's chance a day stays in bounds and falls with the asking price");
        Check(LandRules.ZoneOdds(BuildUse.Offices, 1, 0, 50) >= 50 && LandRules.ZoneOdds(BuildUse.Offices, 1, 1, 50) > LandRules.ZoneOdds(BuildUse.Offices, 1, -1, 50),
            "builders: the council's odds on rezoning follow its permits policy");
    }

    private static void BuildPlansChecks()
    {
        Check(PlansRules.Storeys(3.6) == 1 && PlansRules.Storeys(36.0) == 10 && PlansRules.Storeys(0.5) == 1, "builders: a design's height is its storeys at 3.6 m each, one at least");
        Check(PlansRules.Hours(BuildUse.Homes, 10, 1, ProjectKind.Plot) == BuildCatalogue.HoursLeast && PlansRules.Hours(BuildUse.Offices, 100000, 10, ProjectKind.Plot) == BuildCatalogue.HoursMost,
            "builders: a site's working hours are bounded (" + BuildCatalogue.HoursLeast + " to " + BuildCatalogue.HoursMost + ")");
        Check(PlansRules.Hours(BuildUse.Homes, 2000, 4, ProjectKind.Rebuild) < PlansRules.Hours(BuildUse.Homes, 2000, 4, ProjectKind.Plot), "builders: a rebuild is lighter work than a new building");
        foreach (ProjectKind kind in new[] { ProjectKind.Plot, ProjectKind.Rebuild, ProjectKind.Contract })
        {
            int[] ends = PlansRules.Ends(kind);
            Check(ends.Length == (int)WorkStage.Count && ends[ends.Length - 1] == 1000, "builders: a " + kind + "'s stages end at the whole of its progress");
            for (int s = 0; s < (int)WorkStage.Count; s++)
            {
                PlansRules.Span(kind, (WorkStage)s, out double from, out double to);
                Check(to > from && PlansRules.StageAt(kind, (from + to) / 2) == (WorkStage)s, "builders: a " + kind + " is in its " + (WorkStage)s + " stage between " + from + " and " + to);
            }
            Check(PlansRules.StageAt(kind, 1.0) == WorkStage.Envelope, "builders: a finished " + kind + " is in its last stage");
            Check(PlansRules.InspectionAfter(kind, WorkStage.Envelope) == (int)Inspection.Final, "builders: a " + kind + " ends with the final inspection");
        }
        Check(PlansRules.InspectionAfter(ProjectKind.Plot, WorkStage.Ground) < 0 && PlansRules.InspectionAfter(ProjectKind.Plot, WorkStage.Foundation) == (int)Inspection.Foundation
              && PlansRules.InspectionAfter(ProjectKind.Plot, WorkStage.Frame) == (int)Inspection.Frame && PlansRules.InspectionAfter(ProjectKind.Contract, WorkStage.Frame) < 0,
            "builders: a building is inspected after its foundations, its frame and at the end; the city's works at the end only");
        Check(!PlansRules.NeedsPlant(WorkStage.Frame, Plant.Crane, BuildCatalogue.CraneFrom - 1) && PlansRules.NeedsPlant(WorkStage.Frame, Plant.Crane, BuildCatalogue.CraneFrom)
              && PlansRules.CrewNeed(WorkStage.Frame, BuildTrade.CraneOperator, 2) == 0, "builders: a frame of " + BuildCatalogue.CraneFrom + " storeys or more has a tower crane and its operator");
        Check(PlansRules.ExitsNeeded(1, 300) == 1 && PlansRules.ExitsNeeded(3, 300) == 2 && PlansRules.ExitsNeeded(9, 900) == 3 && PlansRules.ExitsNeeded(2, 2400) == 3,
            "builders: fire exits by storeys and by a storey's floor area");
        Check(PlansRules.ParkingNeeded(BuildUse.Shops, 600) == 600 / BuildCatalogue.ParkingPerSpace[1] && PlansRules.ParkingNeeded(BuildUse.Homes, 0) == 0, "builders: parking by use and floor area");
        Check(PlansRules.HeightLimit("anything", true) == BuildCatalogue.UptownHeightLimit && PlansRules.HeightLimit("ba:neighborhood_nowhere", false) == BuildCatalogue.DefaultHeightLimit,
            "builders: the new district's height limit, and a district the table does not know has the default");
        Check(BuildCatalogue.UptownHeightLimit == 12, "builders: the new district allows twelve storeys, as its buildings are given (DistrictBuildings.Finish takes 1 to 12)");
        Check(PlansRules.NeedsEngineer(BuildCatalogue.EngineerFromStoreys, ProjectKind.Plot) && !PlansRules.NeedsEngineer(BuildCatalogue.EngineerFromStoreys - 1, ProjectKind.Plot)
              && PlansRules.NeedsEngineer(1, ProjectKind.Rebuild) && !PlansRules.NeedsEngineer(20, ProjectKind.Contract), "builders: who needs an engineer's sign off");
        Check(PlansRules.Unmet(1, 2, false, 3, 13, 12, true, false, out int reasons) == 4 && reasons == 15 && PlansRules.Unmet(2, 2, true, 3, 4, 12, true, true, out int none) == 0 && none == 0,
            "builders: the requirements not met are counted, each its own bit");
        PlansQuote cheap = PlansRules.Quote(ArchitectKind.Firm, 0, 0, 1000000, 2000), dear = PlansRules.Quote(ArchitectKind.Firm, 2, 0, 1000000, 2000);
        PlansQuote staff = PlansRules.Quote(ArchitectKind.Staff, 0, 80, 1000000, 2000), own = PlansRules.Quote(ArchitectKind.Player, 0, 90, 1000000, 2000);
        Check(cheap.Fee == 1000000L * BuildCatalogue.FirmFeePermille[0] / 1000 && dear.Fee > cheap.Fee && dear.Quality > cheap.Quality && dear.Days < cheap.Days,
            "builders: a practice's fee is a share of the estimate, and a dearer one is better and quicker");
        Check(staff.Fee == 0 && own.Fee == 0 && own.Days == 0 && staff.Quality > BuildCatalogue.StaffQualityBase, "builders: an architect of the staff and the player draw for no fee");
        int[] exchange = { 1000, 1000, 1000, 1000, 1000 };
        CostSheet sheet = PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 2000, 6, false, 2, 1000, exchange);
        CostSheet under = PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 2000, 6, true, 2, 1000, exchange);
        Check(sheet.Materials > 0 && sheet.Labour > 0 && sheet.Plant > 0 && sheet.Fees > 0 && sheet.Exits > 0 && sheet.Contingency > 0 && under.Total > sheet.Total,
            "builders: an estimate counts the materials, the crews, the plant, the fees, the exits and the contingency, and parking under it costs more");
        int[] dearer = { 2000, 2000, 2000, 2000, 2000 };
        Check(PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 2000, 6, false, 2, 1000, dearer).Materials > sheet.Materials
              && PlansRules.Estimate(ProjectKind.Plot, BuildUse.Homes, 2000, 6, false, 2, 2000, exchange).Total > sheet.Total,
            "builders: the materials follow the raw materials exchange, and everything the city's prices");
        CostSheet costed = PlansRules.Costed(sheet, 1300);
        Check(costed.Materials == sheet.Materials * 1300 / 1000 && costed.Fees == sheet.Fees && PlansRules.Costed(sheet, 1000).Total == sheet.Total
              && PlansRules.Costed(sheet, 5000).Materials == sheet.Materials * BuildCatalogue.DesignCostMost / 1000,
            "builders: the player's own design's cost against its budget scales the materials and the crews, bounded");
        CostSheet works = PlansRules.EstimateWorks(4, 200, 1000, exchange);
        Check(works.Total > 0 && works.Fees == (long)BuildCatalogue.InspectionFee[(int)Inspection.Final], "builders: the city's works pay for the final inspection only, and no planning fee");
        for (int spread = 0; spread <= 150; spread += 50)
        {
            int error = PlansRules.Error(7, 3, spread);
            Check(Math.Abs(error) <= spread && error == PlansRules.Error(7, 3, spread), "builders: an estimate's error stays in its spread and is the same every time (" + spread + ")");
        }
        Check(PlansRules.Spread(true, 100, false) == Math.Max(BuildCatalogue.EstimateSpreadLeast, BuildCatalogue.EstimateSpreadBase - 100 * BuildCatalogue.EstimateSpreadPerSkill)
              && PlansRules.Spread(true, 100, false) < PlansRules.Spread(false, 0, true) && PlansRules.Spread(false, 0, true) < PlansRules.Spread(false, 0, false),
            "builders: an estimator brings the estimate closest, a practice closer than nobody");
        Check(PlansRules.Shown(1234567, 0) == 1235000 && PlansRules.Shown(10, 0) == 1000, "builders: an estimate is shown in thousands");
        Check(PlansRules.CrewDay(0, 100, 1000) > PlansRules.CrewDay(0, 0, 1000) && PlansRules.CrewDay(0, 50, 2000) == PlansRules.CrewDay(0, 50, 1000) * 2,
            "builders: a skilled crew costs more, and every rate follows the city's prices");
    }

    private static void BuildPlanningChecks()
    {
        Check(PlanningRules.WorkingDays(0) == BuildCatalogue.PlanningDays && PlanningRules.WorkingDays(1000000) == BuildCatalogue.PlanningDaysMost, "builders: a decision's working days, bounded");
        Check(PlanningRules.AfterWorkingDays(5, 1) == 8 && PlanningRules.AfterWorkingDays(1, 5) == 8 && PlanningRules.AfterWorkingDays(3, 0) == 3,
            "builders: working days skip the weekend (day 5 is a Friday)");
        for (int p = 1; p <= 40; p++)
        {
            int n = PlanningRules.Objections(11, p, 8, 4, false, 10, 30, 50);
            Check(n >= 0 && n <= BuildCatalogue.ObjectionsMost && n == PlanningRules.Objections(11, p, 8, 4, false, 10, 30, 50), "builders: objections are bounded and the same every time");
        }
        int quiet = 0, loud = 0;
        for (int p = 1; p <= 200; p++)
        {
            quiet += PlanningRules.Objections(5, p, 3, 4, true, 0, 60, 85);
            loud += PlanningRules.Objections(5, p, 12, 4, false, 40, 20, 40);
        }
        Check(loud > quiet * 3, "builders: a tall building off its zoning with no parking draws many more objections (" + loud + " against " + quiet + ")");
        Check(PlanningRules.GrantOdds(100, 100, 0, 0, 1) == BuildCatalogue.GrantMostPermille && PlanningRules.GrantOdds(0, 0, 9, 4, -1) == BuildCatalogue.GrantLeastPermille,
            "builders: the council's odds are bounded");
        Check(!PlanningRules.Decide(1, 1, 1, false, 0, 100, 100, 0, 1, out Refusal zoning) && (zoning & Refusal.Zoning) != 0
              && !PlanningRules.Decide(1, 1, 1, true, 4, 100, 100, 0, 1, out Refusal height) && (height & Refusal.Height) != 0,
            "builders: a building off the lot's zoning, or over the height limit, is always refused, and says why");
        int granted = 0;
        for (int p = 1; p <= 300; p++)
        {
            bool yes = PlanningRules.Decide(3, p, 1, true, 0, 80, 60, 0, 0, out Refusal why);
            Check(yes == PlanningRules.Decide(3, p, 1, true, 0, 80, 60, 0, 0, out _) && (yes ? why == Refusal.None : why != Refusal.None),
                "builders: a decision is the same every time, and a refusal always gives a reason");
            granted += yes ? 1 : 0;
        }
        Check(granted > 200, "builders: good plans with nothing against them are mostly granted (" + granted + " of 300)");
        Check(!PlanningRules.Conditions(BuildCatalogue.ConditionsFrom - 1) && PlanningRules.Conditions(BuildCatalogue.ConditionsFrom), "builders: many objections bring conditions");
        for (int p = 1; p <= 20; p++)
        {
            int hearing = PlanningRules.HearingDay(9, p, 100);
            Check(hearing - 100 >= BuildCatalogue.AppealDaysLeast && hearing - 100 <= BuildCatalogue.AppealDaysMost, "builders: an appeal is heard " + BuildCatalogue.AppealDaysLeast + " to "
                                                                                                                    + BuildCatalogue.AppealDaysMost + " days on");
        }
        Check(PlanningRules.AppealOdds(1000, 80, true, 0, Refusal.Objections) > PlanningRules.AppealOdds(1000, 80, false, 0, Refusal.Objections)
              && PlanningRules.AppealOdds(1000, 80, true, 0, Refusal.Zoning) < PlanningRules.AppealOdds(1000, 80, true, 0, Refusal.Objections),
            "builders: a lawyer helps an appeal; one against the zoning or the height limit is harder");
        Check(PlanningRules.MetShare(0) == 1000 && PlanningRules.MetShare(15) == 0, "builders: the share of requirements met");
        Check(PlanningRules.Licence(true) == LicenceState.Refused && PlanningRules.Licence(false) == LicenceState.Granted, "builders: a conviction refuses the contractor's licence");
        Check(PlanningRules.LicenceUntil(10, 112) == 121, "builders: the licence holds a year");
        Check(!PlanningRules.MayAsk(LicenceState.Applied, 1, 0, 2) && PlanningRules.MayAsk(LicenceState.None, 0, 0, 2) && !PlanningRules.MayAsk(LicenceState.Granted, 1, 100, 50)
              && PlanningRules.MayAsk(LicenceState.Granted, 1, 100, 100 - BuildCatalogue.LicenceRemind) && !PlanningRules.MayAsk(LicenceState.Refused, 10, 0, 20)
              && PlanningRules.MayAsk(LicenceState.Refused, 10, 0, 10 + BuildCatalogue.LicenceRefusedRest), "builders: when the licence may be asked for, renewed or asked again");
    }

    private static void BuildSiteChecks()
    {
        int[] need = { 4, 1, 0, 0, 0 };
        int[] full = { 4, 1, 0, 0, 0 };
        int[] none = new int[(int)BuildTrade.Count];
        Check(SiteRules.Pace(none, need, 50, 50, 0, false, 0) == 0, "builders: nobody on a site, no work done");
        int pace = SiteRules.Pace(full, need, 50, 65, 0, false, 0);
        Check(pace >= BuildCatalogue.PaceLeastPermille && pace <= BuildCatalogue.PaceMostPermille, "builders: a crew's pace is bounded (" + pace + ")");
        Check(SiteRules.Pace(full, need, 90, 65, 0, false, 0) > pace && SiteRules.Pace(full, need, 50, 95, 0, false, 0) > pace && SiteRules.Pace(full, need, 50, 65, 1, false, 0) < pace
              && SiteRules.Pace(full, need, 50, 65, 0, true, 0) < pace && SiteRules.Pace(new[] { 4, 0, 0, 0, 0 }, need, 50, 65, 0, false, 0) < pace
              && SiteRules.Pace(full, need, 50, 65, 0, false, BuildCatalogue.SpecialPacePermille) > pace,
            "builders: skill, morale and the speciality speed a site; missing plant, a missing crane and a missing trade slow it");
        Check(SiteRules.Supplied(new[] { 10, 5, 0, 0, 0 }, new[] { 10, 5, 0, 0, 0 }) && !SiteRules.Supplied(new[] { 10, 4, 0, 0, 0 }, new[] { 10, 5, 0, 0, 0 })
              && Math.Abs(SiteRules.MaterialsShare(new[] { 5, 5, 0, 0, 0 }, new[] { 10, 5, 0, 0, 0 }) - 0.5) < 1e-9, "builders: a stage goes as far as its scarcest material");
        Check(SiteRules.WorkQuality(100, 100, 100) == 100 && SiteRules.WorkQuality(0, 0, 0) == 0, "builders: the work's quality is the crews', the manager's and the plans'");
        Check(SiteRules.PassOdds(80, false, true, true) > SiteRules.PassOdds(80, false, false, false) && SiteRules.PassOdds(80, true, false, false) < SiteRules.PassOdds(80, false, false, false)
              && SiteRules.PassOdds(100, false, true, true) == BuildCatalogue.PassMostPermille, "builders: a walk round, a site manager and an unhurried site help an inspection pass");
        Check(SiteRules.Passes(3, 4, 1, 0, 600) == SiteRules.Passes(3, 4, 1, 0, 600), "builders: an inspection's verdict is the same every time");
        Check(SiteRules.InspectorDay(1, 9) == 2 && SiteRules.InspectorDay(1, BuildCatalogue.InspectLateHour) == 3 && SiteRules.InspectorDay(5, 9) == 8,
            "builders: the inspector comes the next working day, a day later when booked late");
        Check(SiteRules.DeliveryDay(1, 9, 2) == PlanningRules.AfterWorkingDays(1, BuildCatalogue.MerchantDays[2]), "builders: a delivery comes in the merchant's working days");
        for (int order = 0; order < 30; order++)
        {
            int share = SiteRules.Delivered(5, 2, order, 1);
            Check(share == 1000 || (share >= BuildCatalogue.ShortLeastPermille && share <= BuildCatalogue.ShortMostPermille), "builders: a delivery is whole, or short within its bounds");
        }
        Check(SiteRules.Rework(100) == 100 * BuildCatalogue.ReworkPermille / 100 && SiteRules.Rework(1) == 10, "builders: rework is a share of the stage, an hour at least");
        Check(SiteRules.Bribe(0, 1000) == BuildCatalogue.BribeLeast && SiteRules.Bribe(10000000, 1000) == 10000000 * BuildCatalogue.BribePermille / 1000, "builders: a bribe's amount");
        int outs = 0;
        for (int day = 1; day <= 2000; day++)
        {
            outs += SiteRules.BribeOut(1, 1, day) ? 1 : 0;
        }
        Check(outs > 20 && outs < 120, "builders: a bribe comes out about " + BuildCatalogue.BribeChancePermille + " days in 1000 (" + outs + " in 2000)");
        for (int day = 1; day <= 50; day++)
        {
            SiteRules.Theft(2, 3, day, out int material, out int part);
            Check(material >= 0 && material < (int)Material.Count && part >= BuildCatalogue.TheftLeastPermille && part <= BuildCatalogue.TheftMostPermille, "builders: a theft takes a share of one material");
        }
        Check(SiteRules.Morale(50, false, true, 0) == 50 - BuildCatalogue.MoraleUnpaid && SiteRules.Morale(50, true, true, 0) == 50 + BuildCatalogue.MoraleWorking
              && SiteRules.Morale(50, true, false, BuildCatalogue.IdleFrom) == 50 - BuildCatalogue.MoraleIdle && SiteRules.Morale(0, false, false, 9) == 0,
            "builders: a crew's morale by its pay, its work and its idle days, bounded");
        Check(SiteRules.Strikes(2, 0, 5) && !SiteRules.Strikes(1, 0, 5) && !SiteRules.Strikes(2, 6, 5), "builders: two days at the strike line, a strike, one at a time");
        Check(SiteRules.WalksOff(BuildCatalogue.LeaveAt) && !SiteRules.WalksOff(BuildCatalogue.LeaveAt + 1), "builders: a crew walks off at the bottom");
        Check(SiteRules.WorkingDay(1) && SiteRules.WorkingDay(5) && !SiteRules.WorkingDay(6) && !SiteRules.WorkingDay(7), "builders: Monday to Friday are working days");
        Check(FirmRules.HandoverPoints(1, 21, 20, 50) == BuildCatalogue.RepOnTime && FirmRules.HandoverPoints(1, 21, 21, 90) == BuildCatalogue.RepOnTime + BuildCatalogue.RepQuality
              && FirmRules.HandoverPoints(1, 21, 40, 50) < 0, "builders: on time raises the company's standing, good work more, very late lowers it");
        Check(FirmRules.Moved(98, 5) == 100 && FirmRules.Moved(2, -5) == 0, "builders: the company's reputation stays 0 to 100");
        Check(FirmRules.Special(Speciality.Homes, ProjectKind.Plot, BuildUse.Homes, -1) && !FirmRules.Special(Speciality.Homes, ProjectKind.Plot, BuildUse.Offices, -1)
              && FirmRules.Special(Speciality.Roads, ProjectKind.Contract, BuildUse.Shops, 0) && FirmRules.Special(Speciality.Renovation, ProjectKind.Rebuild, BuildUse.Shops, -1),
            "builders: each speciality is its own kind of work");
        Check(FirmRules.Premium(CoverLevel.None, 10000000, 1000) == 0 && FirmRules.Premium(CoverLevel.Full, 10000000, 1000) > FirmRules.Premium(CoverLevel.Standard, 10000000, 1000)
              && FirmRules.Premium(CoverLevel.Standard, 0, 1000) == BuildCatalogue.CoverLeast, "builders: cover's cost a day by its level and the works under way");
        long excess = BuildCatalogue.CoverExcess;
        Check(FirmRules.Pays(CoverLevel.None, 100000, false, false, 1000) == 0 && FirmRules.Pays(CoverLevel.Standard, 100000, false, false, 1000) == 100000 * BuildCatalogue.CoverTheftPermille[1] / 1000 - excess
              && FirmRules.Pays(CoverLevel.Standard, 100000, false, true, 1000) == 0 && FirmRules.Pays(CoverLevel.Full, 100000, false, true, 1000) == 100000 - excess,
            "builders: what the insurer pays, less its excess; storms on full cover only");
        CrewOffer a = FirmRules.Offer(9, 3, 1, 1000), b = FirmRules.Offer(9, 3, 1, 1000);
        Check(a.Gang == b.Gang && a.Skill == b.Skill && a.Rate == b.Rate && a.Skill >= BuildCatalogue.SkillLeast && a.Skill <= BuildCatalogue.SkillMost && a.Rate == PlansRules.CrewDay(a.Gang, a.Skill, 1000),
            "builders: a week's crew to hire is the same for the same save, week and slot, at its trades' rates");
        Check(FirmRules.Week(1) == 0 && FirmRules.Week(7) == 0 && FirmRules.Week(8) == 1, "builders: a week from the save's first");
        Check(FirmRules.Resale(100000, 0) == 100000L * BuildCatalogue.ResalePermille / 1000 && FirmRules.Resale(100000, 100000) == 100000L * BuildCatalogue.ResaleLeastPermille / 1000,
            "builders: plant sells for less the longer it is owned, never under its floor");
        Check(FirmRules.RoleFor("ba:skill_graphicdesigner") == StaffRole.Architect && FirmRules.RoleFor("ba:skill_customerservice") == StaffRole.SiteManager && FirmRules.RoleFor("x") == StaffRole.None,
            "builders: the office's designers start as architects, its client staff as site managers");
    }

    private static void BuildMoneyChecks()
    {
        Check(MoneyRules.LoanMost(400000, 600000) == 650000 && MoneyRules.LoanMost(0, 0) == 0, "builders: a construction loan lends " + BuildCatalogue.LoanToCostPermille / 10
                                                                                                 + "% of the land and the estimate");
        Check(MoneyRules.RateBp(8.0) == (int)Math.Round((8.0 + BuildCatalogue.LoanPointsOver) * 100) && MoneyRules.RateBp(double.NaN) == (int)Math.Round(BuildCatalogue.LoanRateDefault * 100),
            "builders: the loan's rate is the business loan's and its points, or the default without the credit side");
        long agreed = 1000000, drawn = 0;
        for (int i = 0; i < BuildCatalogue.LoanDrawPermille.Length; i++)
        {
            drawn += MoneyRules.Draw(agreed, drawn, i);
        }
        Check(drawn == agreed && MoneyRules.Draw(agreed, 0, 9) == 0, "builders: the loan's draws come to what was agreed");
        long carry = 0, total = 0;
        for (int day = 0; day < 112; day++)
        {
            total += MoneyRules.DayInterest(1000000, 1000, 112, ref carry);
        }
        Check(Math.Abs(total - 100000) <= 1, "builders: a year of daily interest at 10% on 1,000,000 is 100,000 (" + total + ")");
        long owed = 100000, paid = 0;
        int term = MoneyRules.TermDays(112);
        for (int left = term; left >= 1; left--)
        {
            long pay = MoneyRules.Instalment(owed, left);
            owed -= pay;
            paid += pay;
        }
        Check(owed == 0 && paid == 100000 && term == 112 * BuildCatalogue.LoanYears, "builders: the instalments repay the loan exactly over its " + BuildCatalogue.LoanYears + " years");
        Check(MoneyRules.InvestorPut(0, 1000000) == 1000000L * BuildCatalogue.InvestorSharePermille[0] / 1000 && MoneyRules.InvestorOwed(0, 200000) == 200000L * (1000 + BuildCatalogue.InvestorReturnPermille[0]) / 1000,
            "builders: an investor's share and return");
        long toPlayer = MoneyRules.Share(100000, 50000, 40000, out long toLoan, out long toInvestor);
        Check(toLoan == 50000 && toInvestor == 40000 && toPlayer == 10000 && MoneyRules.Share(100000, 0, 0, out _, out _) == 100000, "builders: a sale pays the loan and the investor first");
        Check(MoneyRules.Fee(1000000) == 1000000L * BuildCatalogue.LoanFeePermille / 1000, "builders: the loan's arrangement fee");
    }

    private static void BuildTenderChecks()
    {
        Check(TenderRules.WorksValue(0, 100, 2000) == TenderRules.WorksValue(0, 100, 1000) * 2 && TenderRules.WorksValue(5, 100, 1000) > TenderRules.WorksValue(0, 100, 1000),
            "builders: the city's works are valued by their kind, hours and the city's prices");
        Check(TenderRules.BuildingValue(1000000) == 1000000L * (1000 + BuildCatalogue.PublicMarginPermille) / 1000 / 1000 * 1000, "builders: a building's tender pays its cost and a margin");
        Check(TenderRules.StandardDays(95) == 10 && TenderRules.StandardDays(0) == 1, "builders: ten working hours a working day");
        Check(TenderRules.NeedOf(TenderSource.CityWorks, 0, BuildUse.Shops) == Speciality.Roads && TenderRules.NeedOf(TenderSource.Client, 4, BuildUse.Shops) == Speciality.Renovation
              && TenderRules.NeedOf(TenderSource.Public, 5, BuildUse.Offices) == Speciality.Offices, "builders: what each tender's works suit");
        TenderBid one = TenderRules.RivalBid(1, 2, 3, 1000000, 20, Speciality.Roads, 0), again = TenderRules.RivalBid(1, 2, 3, 1000000, 20, Speciality.Roads, 0);
        Check(one.Price == again.Price && one.Days == again.Days && one.Score == again.Score && one.Bidder == 3, "builders: a rival's bid is the same every time");
        Check(TenderRules.PlayerBid(1000000, 900, false, 20, 50, false).Score < TenderRules.PlayerBid(1000000, 1100, false, 20, 50, false).Score
              && TenderRules.PlayerBid(1000000, 1000, true, 20, 50, false).Score < TenderRules.PlayerBid(1000000, 1000, false, 20, 50, false).Score
              && TenderRules.PlayerBid(1000000, 1000, false, 20, 90, false).Score < TenderRules.PlayerBid(1000000, 1000, false, 20, 30, false).Score
              && TenderRules.PlayerBid(1000000, 1000, false, 20, 50, true).Score < TenderRules.PlayerBid(1000000, 1000, false, 20, 50, false).Score,
            "builders: a lower price, a faster time, a better reputation and the right speciality each make a bid likelier to win");
        Check(TenderRules.PlayerBid(1000000, 10, false, 20, 50, false).Price == 1000000L * BuildCatalogue.BidLeastPermille / 1000, "builders: a bid is bounded");
        var bids = new[]
        {
            new TenderBid { Bidder = 2, Price = 100, Score = 500 }, new TenderBid { Bidder = -1, Price = 90, Score = 500 }, new TenderBid { Bidder = 1, Price = 90, Score = 480 }
        };
        Check(TenderRules.Winner(bids, 3) == 2 && TenderRules.Winner(bids, 2) == 1 && TenderRules.Winner(bids, 0) < 0, "builders: the lowest score wins, then the lower price");
        for (int day = 1; day < 30; day++)
        {
            int client = TenderRules.NextIn(4, day, TenderSource.Client), pub = TenderRules.NextIn(4, day, TenderSource.Public);
            Check(client >= BuildCatalogue.ClientEveryLeast && client <= BuildCatalogue.ClientEveryMost && pub >= BuildCatalogue.PublicEveryLeast && pub <= BuildCatalogue.PublicEveryMost,
                "builders: the days between tenders are in their bounds");
        }
    }

    private static void BuildDeveloperChecks()
    {
        // A district of lots as the ground's plan lays them: the developers' share and order, their uses and builders, their days.
        var lots = new List<LotView>();
        for (int i = 0; i < 120; i++)
        {
            lots.Add(new LotView { Id = "uptown_1_" + (i / 10).ToString("00") + "_" + (i % 10).ToString("00"), Width = 24, Depth = 30, Class = i % 4, First = i < 60, Open = i < 60 });
        }
        var order = new List<int>();
        DevelopmentRules.Order(lots, order);
        var again = new List<int>();
        DevelopmentRules.Order(lots, again);
        bool same = order.Count == again.Count;
        for (int i = 0; same && i < order.Count; i++)
        {
            same = order[i] == again[i];
        }
        Check(same && order.Count > 30 && order.Count < 90, "builders: the developers take about half the lots (" + order.Count + " of 120), in the same order every time");
        bool firstFirst = true, seen = false;
        foreach (int k in order)
        {
            seen |= !lots[k].First;
            firstFirst &= !(seen && lots[k].First);
            Check(DevelopmentRules.Theirs(lots[k].Id), "builders: only the developers' lots are in their order");
        }
        Check(firstFirst, "builders: the first block's lots come first");
        foreach (LotView lot in lots)
        {
            BuildUse use = DevelopmentRules.Use(lot.Id, lot.Class);
            Check(LandRules.Zoned(LandRules.ZonesOf(lot.Class), use), "builders: a development on " + lot.Id + " is a use its lot is zoned for (" + use + ")");
            int rival = DevelopmentRules.Rival(lot.Id, use);
            Check(rival >= 0 && rival < BuildCatalogue.RivalCount, "builders: a development is a rival builder's own");
            int days = DevelopmentRules.Days(lot.Id);
            Check(days >= BuildCatalogue.DevDaysLeast && days < BuildCatalogue.DevDaysLeast + BuildCatalogue.DevDaysSpread, "builders: a development takes its days");
            int pick = DevelopmentRules.DesignPick(lot.Id, 5);
            Check(pick >= 0 && pick < 5 && DevelopmentRules.DesignPick(lot.Id, 0) < 0, "builders: a development's design is one of those that fit");
        }
        Check(DevelopmentRules.Started(BuildCatalogue.DevFirstDay - 1) == 0 && DevelopmentRules.Started(BuildCatalogue.DevFirstDay) == 1
              && DevelopmentRules.Started(BuildCatalogue.DevFirstDay + BuildCatalogue.DevSpacing) == 2, "builders: a development starts every " + BuildCatalogue.DevSpacing + " days");
        string id = lots[order[0]].Id;
        int start = DevelopmentRules.Start(0);
        Check(DevelopmentRules.State(0, id, start - 1) == 0 && DevelopmentRules.State(0, id, start) == 1 && DevelopmentRules.State(0, id, start + DevelopmentRules.Days(id)) == 2,
            "builders: a development is waiting, going up, then finished on its own day");
        // Going Public: the schedule is drawn from the lot plan and the day alone, never a save's seed.
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs");
        string developments = MethodText(market, "private void Developments(int day)");
        Check(developments.Length > 0 && !developments.Contains("Seed") && !MethodText(market, "private bool DevDesign(").Contains("Seed")
              && !MethodText(market, "private void FinishDevelopment(").Contains("Seed") && !ModSource(@"Construction\DevelopmentRules.cs").Contains("seed"),
            "builders: the city's developers are drawn from the lot plan and the day alone: the same on every machine of a session");
        Check(developments.Contains("bool host = !GoingPublic.IsGuest;") && developments.Contains("state == 1 && host"), "builders: only the session's host sets the developers' sites out");
        Check(MethodText(market, "private void MarketDay(int day)").Contains("if (!GoingPublic.InSession)") && MethodText(market, "private void MarketDay(int day)").Contains("if (!GoingPublic.IsGuest)"),
            "builders: public buildings' tenders are never put out in a session, a business's by the host only");
        string actions = ModSource(@"Construction\ConstructionDirector.Actions.cs");
        Check(MethodText(actions, "public string BuyBlock(LotView lot)").Contains("GoingPublic.InSession"), "builders: no land is sold in a Going Public session");
    }

    private static void BuildFrameChecks()
    {
        // The company's designs are picked for the frame the city's plots stand (Civic's PlotRules); the two must agree.
        for (int width = PlotRules.NarrowMin; width <= PlotRules.MostLand; width++)
        {
            for (int depth = PlotRules.MinDepth; depth <= PlotRules.MostLand; depth += 4)
            {
                if (PlotRules.Unfit(width, depth, 1) != null)
                {
                    continue;
                }
                PlotRules.Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
                LandRules.Frame(width, depth, out float w, out float d);
                Check(Math.Abs(w - (maxX - minX)) < 0.001f && Math.Abs(d - (maxZ - minZ)) < 0.001f, "builders: the frame of a " + width + " by " + depth + " m plot is the city's plot's");
            }
        }
        Check(BuildCatalogue.WideFrom == PlotRules.MinWidth && BuildCatalogue.NarrowFrameStoreys == PlotRules.NarrowStoreys && BuildCatalogue.FrameStoreysMost == PlotRules.MostStoreys,
            "builders: a narrow plot and the storeys a frame shows are the city's plots' own");
        Check(LandRules.FrameStoreys(12, 9) == PlotRules.NarrowStoreys && LandRules.FrameStoreys(30, 14) == PlotRules.MostStoreys && LandRules.FrameStoreys(30, 0) == 1,
            "builders: a frame shows the storeys its plot can stand");
        Check(PlotRules.Unfit(12, 20, LandRules.FrameStoreys(12, 6)) == null && PlotRules.Unfit(24, 30, LandRules.FrameStoreys(24, 10)) == null,
            "builders: the frames the company asks for are plots the city can lay out");
    }

    private static void BuildStoreChecks()
    {
        var s = new BuildState { Seed = 12345, FirstDay = 3, LastDay = 40, NextId = 99, NextClientDay = 44, NextPublicDay = 51, OffersWeek = 5 };
        s.HiredOffers.Add(2);
        FirmFile f = s.Firm;
        f.Founded = 4;
        f.Office = "ba:street_5thavenue#36";
        f.Yard = "ba:street_x|y;z^w#2";   // dividers in a text: escaped
        f.Licence = LicenceState.Granted;
        f.LicenceDay = 6;
        f.LicenceUntil = 117;
        f.Reputation = 47;
        f.Speciality = Speciality.Homes;
        f.SpecialSince = 9;
        f.Cover = CoverLevel.Full;
        f.Finished = 2;
        f.OnTime = 1;
        f.Late = 1;
        f.Defects = 1;
        f.TendersWon = 3;
        f.TendersLost = 4;
        f.Earned = 123456789;
        f.Spent = 98765432;
        s.Lots.Add(new LotFile { Id = "uptown_1_02_03", Bought = 7, Paid = 2500000, Zones = 3, ZoneAsk = 4, ZoneDay = 11, TaxedTo = 35, EmptySince = 7 });
        s.Crews.Add(new CrewFile { Id = 5, Gang = 2, Foreman = 7, Skill = 66, Morale = 38, Rate = 4321, Hired = 8, Project = 10, IdleDays = 1, LowDays = 1, StrikeUntil = 0, PoachRival = 3, PoachPay = 150, PoachUntil = 43, LastWeekLooked = 5 });
        s.Plant.Add(new PlantFile { Id = 6, Kind = Plant.Crane, Bought = 9, Price = 520000, Project = 10 });
        s.Staff.Add(new StaffFile { Employee = "emp-1", Role = StaffRole.SiteManager, Project = 10 });
        var p = new ProjectFile
        {
            Id = 10, Kind = ProjectKind.Plot, Stage = ProjectStage.Building, Created = 8, Lot = "uptown_1_02_03", District = "reallife:district_uptown", Use = BuildUse.Homes, Design = "Res_B_02",
            Size = "ba:buildingsize_b", Version = 2, Area = 1700, Storeys = 5, Width = 24, Depth = 30, Height = 18, Architect = ArchitectKind.Player, Firm = 1, PlansDay = 20, PlansDone = true,
            Quality = 81, Estimate = 4560000, Error = -37, Exits = 2, ExitsNeeded = 2, ParkingNeeded = 14, ParkingUnder = true, EngineerDay = 22, Engineered = true, Planning = PlanningState.Granted,
            Applications = 1, PlanningDay = 23, DecideDay = 31, Objections = 2, Consulted = true, Reasons = 0, Conditions = true, SiteId = 77, Hours = 240, StartDay = 33, DueDay = 60, Rework = 14,
            OrderDay = 41, Merchant = 2, Orders = 3, StageSeen = 2, Visited = true, PaceSum = 3000, PaceDays = 3, BribeDay = 38, BribeAmount = 4000, Spent = 1200000, LoanAgreed = 3000000,
            LoanDrawn = 1650000, LoanOwed = 1650000, LoanCarry = 42, LoanRateBp = 1150, LoanDraws = 3, Investor = 1, InvestorPut = 1200000, InvestorOwed = 1440000, Flats = 20, FlatsSold = 4,
            FlatsOffPlan = 4, Deposits = 120000, OffPlanBalance = 1080000, After = AfterUse.SellFlats, Ask = 2, DefectClaim = 5000, Source = TenderSource.Public, CivicKind = 5, Client = 2,
            RainHours = 3, WaitHours = 2, StrikeDays = 1, Fails = 1, Tender = 0, CostPermille = 1120, Units = 19
        };
        p.Have[1] = 12;
        p.Need[1] = 30;
        p.OnOrder[1] = 18;
        p.Plant[0] = 6;
        p.Plant[3] = 0;
        p.Insp[0] = InspectionState.Passed;
        p.Insp[1] = InspectionState.Failed;
        p.InspDay[1] = 39;
        p.InspTries[1] = 1;
        s.Projects.Add(p);
        s.Tenders.Add(new TenderFile
        {
            Id = 12, Source = TenderSource.CityWorks, SiteId = 70, CivicKind = 1, Street = "ba:street_7thavenue", Number = 12, Use = BuildUse.Shops, Area = 400, Hours = 60, Value = 900000,
            Opened = 900, Closes = 930, BidPermille = 950, Fast = true, State = TenderState.Lost, Winner = 4, WinningPrice = 870000, WinningDays = 7, Client = 1, Project = 0
        });
        s.Rivals[2].Reputation = 71;
        s.Rivals[2].Won = 5;
        s.Rivals[2].Lost = 2;
        s.Rivals[2].Built = 3;
        s.Developments.Add(new DevFile { Lot = "uptown_1_05_01", SiteId = 81, Handed = false, Design = "Off_D_01", Public = true });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int version);
        Check(back != null && version == BuildStore.Version && BuildStore.Write(back) == line, "builders: the store's line reads back to the same line (" + line.Length + " characters)");
        if (back != null)
        {
            ProjectFile q = back.Project(10);
            Check(back.Firm.Yard == f.Yard && back.Firm.Earned == f.Earned && back.Lots.Count == 1 && back.Lots[0].ZoneAsk == 4 && back.Crews[0].PoachRival == 3 && back.Plant[0].Kind == Plant.Crane
                  && back.Staff[0].Role == StaffRole.SiteManager && back.Tenders[0].Winner == 4 && back.Rivals[2].Built == 3 && back.Developments[0].Public && back.HiredOffers.Contains(2),
                "builders: the company, its lots, crews, plant, staff, tenders, rivals and the developers' lots come back whole");
            Check(q != null && q.Architect == ArchitectKind.Player && q.CostPermille == 1120 && q.Units == 19 && q.Insp[1] == InspectionState.Failed && q.Plant[0] == 6 && q.Have[1] == 12
                  && q.LoanCarry == 42 && q.Ask == 2 && q.Design == "Res_B_02", "builders: a project comes back with every field, its player's design among them");
        }
        // A line of a newer version: fields and parts this one does not know are left alone; a broken line is not read.
        string newer = line.Replace("^o:", "|77|88^o:") + "^z:new part";
        BuildState later = BuildStore.Read(newer, out _);
        Check(later != null && later.Projects.Count == 1 && later.Developments.Count == 1, "builders: a newer line's added fields and parts are passed over");
        Check(BuildStore.Read("not a line", out _) == null && BuildStore.Read("v=0^h:", out _) == null && BuildStore.Read("v=9^h:", out int high) != null && high == 9,
            "builders: a line that is not this feature's is not read; a newer version's is read as far as this one knows it (SaveLines notes it)");
        // A line of this version with a project written before the player's design was added: its cost stays the plans' own.
        string older = Regex.Replace(line, @"\|1120\|19[^;^]*", string.Empty);   // the design's fields and every field written after them
        BuildState old = BuildStore.Read(older, out _);
        Check(old != null && old.Project(10) != null && old.Project(10).CostPermille == 1000 && old.Project(10).Units == 0, "builders: a project without the design's fields reads with the plans' own cost");
        // The store names the district's lots by their ids only: no address, place or registration of the game's.
        string store = ModSource(@"Construction\BuildStore.cs");
        Check(!Regex.IsMatch(store, @"\bAddress\b|BuildingRegistration|\bBuilding\b|UnityEngine"), "builders: the store keeps nothing of the game's part of the save");
    }

    private static void BuildSourceChecks()
    {
        string[] files =
        {
            "BuildCatalogue.cs", "BuildRandom.cs", "LandRules.cs", "DevelopmentRules.cs", "PlansRules.cs", "PlanningRules.cs", "SiteRules.cs", "MoneyRules.cs", "TenderRules.cs", "FirmRules.cs",
            "BuildState.cs", "BuildStore.cs", "ConstructionBridges.cs", "ConstructionType.cs", "ConstructionGame.cs", "ConstructionDirector.cs", "ConstructionDirector.Asks.cs",
            "ConstructionDirector.Sites.cs", "ConstructionDirector.Projects.cs", "ConstructionDirector.Day.cs", "ConstructionDirector.Market.cs", "ConstructionDirector.Actions.cs",
            "ConstructionDirector.App.cs", "ConstructionDirector.Tests.cs", "PlanningCall.cs", "ConstructionText.cs", "ConstructionTables.cs", "ConstructionApp.cs",
            "ConstructionApp.Overview.cs", "ConstructionApp.Land.cs", "ConstructionApp.Projects.cs", "ConstructionApp.Tenders.cs", "ConstructionApp.Crews.cs", "ConstructionApp.Company.cs",
            "ConstructionApp.Rivals.cs", "ConstructionGuide.cs"
        };
        var code = new Dictionary<string, string>();
        foreach (string name in files)
        {
            code[name] = ModSource(@"Construction\" + name);
            Check(code[name].Length > 0, "builders sources: " + name + " is there");
        }
        string all = string.Join("\n", code.Values);
        Check(!Regex.IsMatch(all, @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(\)|\.ToArray\(\)"), "builders sources: no LINQ");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|OverlapSphere\(|RaycastAll\("), "builders sources: no whole-scene search and no physics query that allocates");
        Check(!Regex.IsMatch(all, @"\bHarmony\b|\bMPClient\b|\bMPServer\b|DlcIsOwned|requiredDLC|SteamApps"), "builders sources: no patch, nothing sent over the network, no DLC read");
        foreach (string name in new[] { "BuildCatalogue.cs", "BuildRandom.cs", "LandRules.cs", "DevelopmentRules.cs", "PlansRules.cs", "PlanningRules.cs", "SiteRules.cs", "MoneyRules.cs",
                     "TenderRules.cs", "FirmRules.cs", "BuildState.cs", "BuildStore.cs" })
        {
            Check(!Regex.IsMatch(code[name], @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode"), "builders sources: " + name + " is game-free and draws only from its seed");
        }
        // One Update, a time comparison and the hot spot's timer; everything else once a second, on the hour or the day.
        string director = code["ConstructionDirector.cs"];
        Check(Regex.Matches(all, @"void Update\(\)").Count == 1 && MethodText(director, "private void Update()").Contains("using var hotSpot = HotSpots.Time(HotUpdate);")
              && MethodText(director, "private void OnUpdate()").Contains("if (now < _next)"), "builders sources: one Update: its hot spot, then one time comparison a frame");
        Check(director.Contains("Guard.For(\"construction\", \"ConstructionDirector\", typeof(ConstructionDirector))") && MethodText(director, "private void Update()").Contains("Guard.Fail(FrameGuard, e, this)"),
            "builders sources: the director's frame is guarded");
        Check(director.Contains("SaveHook.Keep(_onSave)") && MethodText(director, "private void OnSave()").Contains("SaveLines.IsKept(SaveGameManager.Current.modData, BuildStore.Key)"),
            "builders sources: the line is written at the save, and one this version could not read is kept as it was read");
        Check(MethodText(director, "private void Slow()").Contains("Settings.Construction && Settings.Civic && Settings.CivicConstruction"), "builders sources: the game runs only with its option and the city's sites on");
        // The layering: one file touches the game; the rest meet the mod through the bridges.
        foreach (string name in files)
        {
            if (name == "ConstructionGame.cs" || name == "ConstructionType.cs" || name.StartsWith("ConstructionApp", StringComparison.Ordinal) || name == "PlanningCall.cs" || name == "ConstructionGuide.cs"
                || name == "ConstructionDirector.App.cs")
            {
                continue;
            }
            Check(!Regex.IsMatch(code[name], @"SaveGameManager\.Current\.(Money|BuildingRegistrations|EmployeeInstances|realEstate)|(?<!Save)GameManager\.|BuildingHelper\.|EmployeeHelper\."),
                "builders sources: " + name + " leaves the game's own objects to ConstructionGame");
            Check(!Regex.IsMatch(code[name], @"\b(CivicDirector|CivicWorks|SiteState|BuildingCopies|TradesBridges|DistrictBuildings|UptownLots|MarketDirector|FameDirector|FlareDirector|JusticeState)\b"),
                "builders sources: " + name + " names no other feature's director, store or game file");
        }
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        string wire = ModSource(@"Wiring\Wiring.cs");
        Check(wiring.Contains("ConstructionBridges.Qualified = TradesBridges.Qualified;") && wiring.Contains("TradesBridges.Commission(project, storeys, units,")
              && wiring.Contains("ConstructionBridges.DesignOf = DesignOf;") && MethodText(wiring, "private static bool DesignOf(").Contains("TradesBridges.DesignOf(project, out DesignScore score)")
              && MethodText(wiring, "private static bool DesignOf(").Contains("costPermille = score.CostPermille;") && MethodText(wiring, "private static bool DesignOf(").Contains("units = score.Units;"),
            "builders sources: the building trades' qualification, brief and design are wired (docs/features/trades.md, With the construction company)");
        Check(wire.Contains("Step(\"the construction game\", ConstructionLinks.Connect);") && wire.Contains("Step(\"the construction game\", ConstructionLinks.Disconnect);")
              && wire.IndexOf("Step(\"the construction game\", ConstructionLinks.Disconnect);", StringComparison.Ordinal) < wire.IndexOf("Step(\"the building trades\", TradesLinks.Disconnect);", StringComparison.Ordinal),
            "builders sources: the construction game is wired in last and out first (it wraps the paper and the credit accounts)");
        // Every bridge the construction game asks is filled in the wiring and put back.
        string bridges = code["ConstructionBridges.cs"];
        int bridgeCount = 0;
        foreach (Match m in Regex.Matches(bridges, @"public static (?:[A-Za-z<>,\[\] ]+) ([A-Z][A-Za-z]+);"))
        {
            string name = m.Groups[1].Value;
            bridgeCount++;
            Check(bridges.Contains(name + " = null;"), "builders sources: the bridge " + name + " is put back by Reset");
            Check(wiring.Contains("ConstructionBridges." + name + " = ") && wiring.Contains("ConstructionBridges." + name + " = null;"), "builders sources: the bridge " + name + " is wired in and out");
        }
        Check(bridgeCount > 30, "builders sources: the bridges are found (" + bridgeCount + ")");
        // The new district: its lots, their sizes and traffic, its land's price and a finished building handed to it.
        Check(wiring.Contains("Open = UptownLots.Open(l), Traffic = Math.Max(0, DistrictBuildings.TrafficOf(l.Id))") && MethodText(wiring, "private static bool SizeTaken(").Contains("l.Sizes")
              && MethodText(wiring, "private static bool Finish(").Contains("player ? FinishOwner.Player : FinishOwner.City"),
            "builders sources: the district's lots, the sizes a lot takes and a finished building handed to it are Uptown's own (UptownLots, DistrictBuildings)");
        foreach (Match m in Regex.Matches(ModSource(@"Civic\SiteBuilders.cs"), @"public static (?:[A-Za-z<>,\[\] ]+) ([A-Z][A-Za-z]+);"))
        {
            Check(wiring.Contains("SiteBuilders." + m.Groups[1].Value + " = "), "builders sources: the city's programme asks the builders " + m.Groups[1].Value + " through the wiring");
        }
        // The shared files' lines.
        string settings = ModSource(@"Core\Settings.cs");
        foreach (string id in new[] { "Construction", "ConstructionDevelopers", "ConstructionLandTax", "ConstructionRisks" })
        {
            Check(settings.Contains("public static bool " + id + " = true;") && settings.Contains(id + " = Bool(modId, " + id + "Id, " + id + ");"), "builders sources: the option " + id + " is on by default and read");
        }
        string tree = ModSource(@"Meta\OptionsTree.cs");
        Check(tree.Contains("S(\"construction\").For(\"construction\").Opt(\"construction\", \"civic_construction\").Opt(\"construction_developers\", \"construction\")"),
            "builders sources: the options' section, each needing the one before");
        Check(ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"construction\", CityScope.NewYork,"), "builders sources: the company stands on New York's map (the city table)");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("Guard.Add<Construction.ConstructionDirector>(_root)") && mod.Contains("Construction.ConstructionOptions.AddConstruction(o)")
              && Regex.Matches(mod, @"Construction\.ConstructionType\.Register\(\)").Count == 2 && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Construction\", \"construction\" }"),
            "builders sources: the director, the options, the business type (at the main menu and the city's load) and the feature's owner are registered");
        Check(ModSource(@"Meta\SaveTools.Steps.cs").Contains("Construction.ConstructionType.Strip(save)"), "builders sources: preparing a save for removing the mod makes each company a design studio");
        Check(code["ConstructionGuide.cs"].Contains(".Owns(Settings.ConstructionId, Settings.ConstructionDevelopersId, Settings.ConstructionLandTaxId, Settings.ConstructionRisksId)")
              && code["ConstructionGuide.cs"].Contains(".Data(BuildStore.Key, BuildStore.TestKey)"), "builders sources: the feature list owns its options and its lines");
        // Every test of the options page is on the page and in the feature list, and writes its check line.
        string guide = code["ConstructionGuide.cs"];
        string tests = code["ConstructionDirector.Tests.cs"];
        foreach (Match m in Regex.Matches(guide, "\\.AddButton\\(\"(reallife_bld_test_[a-z_]+)\""))
        {
            Check(Regex.Matches(guide, "\"" + m.Groups[1].Value + "\"").Count == 2, "builders sources: the test " + m.Groups[1].Value + " is on the page and in the feature list");
        }
        foreach (Match m in Regex.Matches(tests, @"public void (Test[A-Za-z]+)\(\)"))
        {
            string body = MethodText(tests, "public void " + m.Groups[1].Value + "()");
            Check(body.Contains("Construction check (") || m.Groups[1].Value == "TestBack" && body.Contains("Construction check (back)"), "builders sources: " + m.Groups[1].Value + " writes its check line");
            Check(guide.Contains("ConstructionDirector.Instance?." + m.Groups[1].Value + "()"), "builders sources: " + m.Groups[1].Value + " is a button of the options page");
        }
        // Ground a person picks by eye is nearly always taken, so the lot test needs no spot while the district has a lot.
        string lotTest = MethodText(tests, "public void TestLot()");
        string nearestLot = MethodText(tests, "private bool NearestOpenLot(");
        int district = lotTest.IndexOf("NearestOpenLot(", StringComparison.Ordinal), ahead = lotTest.IndexOf("GroundClear(", StringComparison.Ordinal);
        Check(district >= 0 && ahead > district && nearestLot.Contains("!lot.Open") && nearestLot.Contains("_state.Lot(lot.Id) != null") && nearestLot.Contains("DeveloperOn(lot.Id)")
              && nearestLot.Contains("LotTaken(lot.Id)"), "builders sources: the lot test gives the district's nearest open lot nobody has first, the ground ahead only without one");
        // The app and the City Hall call: the game's own frame and conversation.
        Check(code["ConstructionApp.cs"].Contains("AppShell.Make(menu, dormant, ConstructionDirector.AppId)") && code["ConstructionDirector.App.cs"].Contains("PhoneApp.Install(new PhoneAppDef"),
            "builders sources: the app is the game's own frame on the phone");
        Check(code["PlanningCall.cs"].Contains("CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new PlanningDialog()))")
              && ModSource(@"Civic\CityHallDialog.cs").Contains("SiteBuilders.Planning("), "builders sources: planning and building control is City Hall's sixth topic, and the app opens the same pages");
        Check(code["ConstructionDirector.App.cs"].Contains("new DoorPanel(Logger, \"Construction check (ribbon panel)\")"), "builders sources: the ribbon is cut in the game's own panel at the place");
    }

    private static void BuildTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { "BuildCatalogue.cs", "ConstructionDirector.cs", "ConstructionDirector.Asks.cs", "ConstructionDirector.Sites.cs", "ConstructionDirector.Projects.cs",
                     "ConstructionDirector.Day.cs", "ConstructionDirector.Market.cs", "ConstructionDirector.Actions.cs", "ConstructionDirector.App.cs", "ConstructionDirector.Tests.cs", "PlanningCall.cs",
                     "ConstructionText.cs", "ConstructionTables.cs", "ConstructionApp.cs", "ConstructionApp.Overview.cs", "ConstructionApp.Land.cs", "ConstructionApp.Projects.cs",
                     "ConstructionApp.Tenders.cs", "ConstructionApp.Crews.cs", "ConstructionApp.Company.cs", "ConstructionApp.Rivals.cs", "ConstructionGuide.cs", "ConstructionGame.cs" })
        {
            string source = ModSource(@"Construction\" + name);
            foreach (Match m in Regex.Matches(source, "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                if (!m.Groups[1].Value.Contains("calldialogtype"))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
            foreach (Match m in Regex.Matches(source, "Prefix \\+ \"(_bld_[a-z0-9_]+)\""))
            {
                keys.Add("reallife" + m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_transaction_bld_fees", "reallife_transaction_bld_crews", "reallife_transaction_bld_plant", "reallife_transaction_bld_materials",
                     "reallife_transaction_bld_cover", "reallife_transaction_bld_interest", "reallife_transaction_bld_land_tax", "reallife_transaction_bld_vacant_tax" })
        {
            keys.Add(key + "_label");   // the deductible ones' label, as the game's statement shows it
        }
        keys.Add("reallife_text_justice_suit_build");
        keys.Add("reallife:businesstype_construction");
        Check(keys.Count > 600, "builders texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("builders texts", words, codes))
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
                    Check(false, "builders texts: " + key + " is in " + codes[l] + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Replace("\n- ", "\n").StartsWith("- ") || value.Replace("\n- ", "\n").Contains(" - "))
                {
                    styled++;
                    Check(false, "builders texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0, "builders texts: every key of the construction game is in " + codes[l] + ".json, in the style");
            Check(words[l].TryGetValue("reallife_guide_construction", out string page) && page.Contains("{bld_licence_fee}") && page.Contains("{bld_land_share}") && page.Contains("{bld_bribe_days}"),
                "builders texts: the Guide's page in " + codes[l] + " has its figures");
        }
        // What's new: the company's line in every language, before the first fix of the row that holds it (the next row before a
        // release, the version's own row after it: the version script moves the lines).
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Your own construction company:" }, { "pt", "Sua própria construtora:" }, { "zh-cn", "你自己的建筑公司：" }, { "de", "Deine eigene Baufirma:" },
            { "fr", "Votre propre entreprise de construction :" }, { "es", "Tu propia constructora:" }, { "ru", "Своя строительная компания:" }, { "nl", "Je eigen bouwbedrijf:" },
            { "tr", "Kendi inşaat şirketiniz:" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = starts.TryGetValue(codes[l], out string start) ? FixRow(words[l], start) : string.Empty;
            int line = start != null ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix), "builders texts: What's new in " + codes[l] + " has the construction company, before its row's fixes");
        }
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (Match m in Regex.Matches(words[0]["reallife_guide_construction"], @"\{(bld_[a-z_]+)\}"))
        {
            Check(guide.Contains(".Fact(\"" + m.Groups[1].Value + "\""), "builders texts: the Guide's figure {" + m.Groups[1].Value + "} is a fact of the feature list");
        }
    }
}
