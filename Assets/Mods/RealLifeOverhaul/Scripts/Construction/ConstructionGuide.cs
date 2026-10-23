using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's entry in the feature list, its keys written out whole as the options checks read them. It
    /// registers itself (<see cref="DescribesFeatureAttribute"/>). Its Guide page's figures are facts filled from the
    /// catalogue at today's prices.
    /// </summary>
    [DescribesFeature]
    public static class ConstructionGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void DescribeFeature()
        {
            Features.Add("construction", "reallife_meta_title_construction")
                .Group("reallife_bld_header")
                .About("reallife_guide_construction_about").Help("reallife_guide_construction")
                .Owns(Settings.ConstructionId, Settings.ConstructionDevelopersId, Settings.ConstructionLandTaxId, Settings.ConstructionRisksId)
                .Owns(Settings.ConstructionCityId)
                .Data(BuildStore.Key, BuildStore.TestKey)
                .ResetData(() => ConstructionDirector.Instance?.Forget())
                .Fact("bld_licence_fee", () => Money(BuildCatalogue.LicenceFee))
                .Fact("bld_licence_days", () => BuildCatalogue.LicenceDays.ToString(Inv))
                .Fact("bld_zone_fee", () => Money(BuildCatalogue.ZoneFee))
                .Fact("bld_planning_fee", () => Money(BuildCatalogue.PlanningFee))
                .Fact("bld_planning_share", () => Share(BuildCatalogue.PlanningFeePermille))
                .Fact("bld_planning_days", () => BuildCatalogue.PlanningDays.ToString(Inv))
                .Fact("bld_planning_most", () => BuildCatalogue.PlanningDaysMost.ToString(Inv))
                .Fact("bld_appeal_fee", () => Money(BuildCatalogue.AppealFee))
                .Fact("bld_land_share", () => Share(BuildCatalogue.LandSharePermille))
                .Fact("bld_land_tax", () => Share(BuildCatalogue.LandTaxPermille))
                .Fact("bld_vacant_tax", () => Share(BuildCatalogue.VacantTaxPermille))
                .Fact("bld_vacant_days", () => BuildCatalogue.VacantAfter.ToString(Inv))
                .Fact("bld_uptown_limit", () => BuildCatalogue.UptownHeightLimit.ToString(Inv))
                .Fact("bld_engineer_from", () => BuildCatalogue.EngineerFromStoreys.ToString(Inv))
                .Fact("bld_engineer_fee", () => Share(BuildCatalogue.EngineerFirmFeePermille))
                .Fact("bld_firm_fee_least", () => Share(BuildCatalogue.FirmFeePermille[0]))
                .Fact("bld_firm_fee_most", () => Share(BuildCatalogue.FirmFeePermille[BuildCatalogue.FirmCount - 1]))
                .Fact("bld_crane_from", () => BuildCatalogue.CraneFrom.ToString(Inv))
                .Fact("bld_crews_most", () => BuildCatalogue.CrewsMost.ToString(Inv))
                .Fact("bld_sites_most", () => BuildCatalogue.BuildingMost.ToString(Inv))
                .Fact("bld_loan_share", () => Share(BuildCatalogue.LoanToCostPermille))
                .Fact("bld_loan_points", () => BuildCatalogue.LoanPointsOver.ToString("0.#", Inv))
                .Fact("bld_loan_years", () => BuildCatalogue.LoanYears.ToString(Inv))
                .Fact("bld_investor_least", () => Share(BuildCatalogue.InvestorSharePermille[0]))
                .Fact("bld_investor_most", () => Share(BuildCatalogue.InvestorSharePermille[BuildCatalogue.InvestorCount - 1]))
                .Fact("bld_investor_days", () => BuildCatalogue.InvestorPayDays.ToString(Inv))
                .Fact("bld_off_plan", () => Share(BuildCatalogue.OffPlanPermille))
                .Fact("bld_deposit", () => Share(BuildCatalogue.DepositPermille))
                .Fact("bld_flat_area", () => BuildCatalogue.FlatArea.ToString(Inv))
                .Fact("bld_tender_hours", () => BuildCatalogue.TenderHours.ToString(Inv))
                .Fact("bld_bid_least", () => Share(BuildCatalogue.BidLeastPermille))
                .Fact("bld_bid_most", () => Share(BuildCatalogue.BidMostPermille))
                .Fact("bld_rivals", () => BuildCatalogue.RivalCount.ToString(Inv))
                .Fact("bld_cover_excess", () => Money(BuildCatalogue.CoverExcess))
                .Fact("bld_bribe_days", () => BuildCatalogue.BribeDays.ToString(Inv))
                .Fact("bld_speciality_rest", () => BuildCatalogue.SpecialityRest.ToString(Inv))
                .Fact("bld_strict_storeys", () => CivicCatalogue.UptownStrictStoreys.ToString(Inv))
                .Fact("bld_ring_cut", () => Share(RingRules.CutPermille))
                .Fact("bld_ring_days", () => RingRules.FoundDays.ToString(Inv))
                .Fact("bld_chase_most", () => BillRules.ChaseMost.ToString(Inv))
                .Fact("bld_sue_after", () => BillRules.SueAfterDays.ToString(Inv))
                .Fact("bld_account_days", () => BillRules.MerchantTerms.ToString(Inv))
                .Fact("bld_award_shortlist", () => AwardRules.Shortlist.ToString(Inv))
                .Test("reallife_bld_test_lot", "reallife_bld_test_company", "reallife_bld_test_licence", "reallife_bld_test_plans", "reallife_bld_test_planning",
                    "reallife_bld_test_council", "reallife_bld_test_stage", "reallife_bld_test_inspection", "reallife_bld_test_deliveries", "reallife_bld_test_handover",
                    "reallife_bld_test_opening", "reallife_bld_test_tender", "reallife_bld_test_award", "reallife_bld_test_developers", "reallife_bld_test_poach",
                    "reallife_bld_test_strike", "reallife_bld_test_fault", "reallife_bld_test_bribe", "reallife_bld_test_buyer", "reallife_bld_test_city_code",
                    "reallife_bld_test_city_zoning", "reallife_bld_test_city_motions", "reallife_bld_test_city_ring", "reallife_bld_test_city_bills", "reallife_bld_test_city_award",
                    "reallife_bld_test_back")
                .Cleanup(() => ConstructionDirector.Instance?.StandDown("its things were put away"));
        }

        private static string Money(int amount) => Guide.Money(Prices.At(amount));

        private static string Share(int thousandths) => ConstructionText.Share(thousandths);
    }

    /// <summary>The construction game's place on the options page: its four options and the test entries (shown with the test tools).</summary>
    public static class ConstructionOptions
    {
        public static ModOptions AddConstruction(this ModOptions options)
        {
            return options
                .AddHeader("reallife_bld_header")
                .AddToggle(Settings.ConstructionId, "reallife_bld_enabled", Settings.Construction, v => Settings.Construction = v)
                .AddToggle(Settings.ConstructionDevelopersId, "reallife_bld_developers", Settings.ConstructionDevelopers, v => Settings.ConstructionDevelopers = v)
                .AddToggle(Settings.ConstructionLandTaxId, "reallife_bld_land_tax", Settings.ConstructionLandTax, v => Settings.ConstructionLandTax = v)
                .AddToggle(Settings.ConstructionRisksId, "reallife_bld_risks", Settings.ConstructionRisks, v => Settings.ConstructionRisks = v)
                .AddToggle(Settings.ConstructionCityId, "reallife_bld_city_option", Settings.ConstructionCity, v => Settings.ConstructionCity = v)
                .AddButton("reallife_bld_test_lot", () => ConstructionDirector.Instance?.TestLot())
                .AddButton("reallife_bld_test_company", () => ConstructionDirector.Instance?.TestCompany())
                .AddButton("reallife_bld_test_licence", () => ConstructionDirector.Instance?.TestLicence())
                .AddButton("reallife_bld_test_plans", () => ConstructionDirector.Instance?.TestPlans())
                .AddButton("reallife_bld_test_planning", () => ConstructionDirector.Instance?.TestPlanning())
                .AddButton("reallife_bld_test_council", () => ConstructionDirector.Instance?.TestCouncil())
                .AddButton("reallife_bld_test_stage", () => ConstructionDirector.Instance?.TestStage())
                .AddButton("reallife_bld_test_inspection", () => ConstructionDirector.Instance?.TestInspection())
                .AddButton("reallife_bld_test_deliveries", () => ConstructionDirector.Instance?.TestDeliveries())
                .AddButton("reallife_bld_test_handover", () => ConstructionDirector.Instance?.TestHandover())
                .AddButton("reallife_bld_test_opening", () => ConstructionDirector.Instance?.TestOpening())
                .AddButton("reallife_bld_test_tender", () => ConstructionDirector.Instance?.TestTender())
                .AddButton("reallife_bld_test_award", () => ConstructionDirector.Instance?.TestAward())
                .AddButton("reallife_bld_test_developers", () => ConstructionDirector.Instance?.TestDevelopers())
                .AddButton("reallife_bld_test_poach", () => ConstructionDirector.Instance?.TestPoach())
                .AddButton("reallife_bld_test_strike", () => ConstructionDirector.Instance?.TestStrike())
                .AddButton("reallife_bld_test_fault", () => ConstructionDirector.Instance?.TestFault())
                .AddButton("reallife_bld_test_bribe", () => ConstructionDirector.Instance?.TestBribe())
                .AddButton("reallife_bld_test_buyer", () => ConstructionDirector.Instance?.TestBuyer())
                .AddButton("reallife_bld_test_city_code", () => Wiring.CityTests.Code())
                .AddButton("reallife_bld_test_city_zoning", () => Wiring.CityTests.Zoning())
                .AddButton("reallife_bld_test_city_motions", () => Wiring.CityTests.Office())
                .AddButton("reallife_bld_test_city_ring", () => Wiring.CityTests.Ring())
                .AddButton("reallife_bld_test_city_bills", () => Wiring.CityTests.Bills())
                .AddButton("reallife_bld_test_city_award", () => Wiring.CityTests.YearAward())
                .AddButton("reallife_bld_test_back", () => ConstructionDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
