using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.TestRun;

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
                .Owns(Settings.ConstructionShowFlatsId, Settings.ConstructionRenovationsId)
                .Owns(Settings.ConstructionCityId)
                .Owns(Settings.ConstructionRedevelopId, Settings.ConstructionCityRedevelopId)
                .Owns(Settings.ConstructionTrackId)
                .Owns(Settings.ConstructionBuildModeId)
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
                .Fact("bld_show_day", () => Money(BuildCatalogue.ShowUpkeepDay))
                .Fact("bld_agent_day", () => Money(BuildCatalogue.AgentDay))
                .Fact("bld_viewings_most", () => BuildCatalogue.ViewingHours.Length.ToString(Inv))
                .Fact("bld_show_price_least", () => Share(BuildCatalogue.ShowPriceLeast))
                .Fact("bld_show_price_most", () => Share(BuildCatalogue.ShowPriceMost))
                .Fact("bld_refit_hours_least", () => BuildCatalogue.RefitHoursLeast.ToString(Inv))
                .Fact("bld_refit_hours_most", () => BuildCatalogue.RefitHoursMost.ToString(Inv))
                .Fact("bld_refit_lift_most", () => Interiors.InteriorRules.RefitMost.ToString(Inv))
                .Fact("bld_hire_deposit", () => Share(BuildCatalogue.HireDepositPermille))
                .Fact("bld_hire_stage_first", () => Share(BuildCatalogue.HireStagePermille[0]))
                .Fact("bld_hire_stage_frame", () => Share(BuildCatalogue.HireStagePermille[1]))
                .Fact("bld_hire_stage_final", () => Share(BuildCatalogue.HireStagePermille[2]))
                .Fact("bld_hire_retention", () => Share((int)CommissionRules.Retention(1000L)))
                .Fact("bld_hire_penalty", () => Share(BuildCatalogue.HirePenaltyDayPermille))
                .Fact("bld_hire_penalty_most", () => Share(BuildCatalogue.HirePenaltyMostPermille))
                .Fact("bld_permit_days_most", () => RedevelopRules.PermitDaysMost.ToString(Inv))
                .Fact("bld_notice_days", () => RedevelopRules.NoticeDays.ToString(Inv))
                .Fact("bld_buyout_days", () => RedevelopRules.TenantDays.ToString(Inv))
                .Fact("bld_site_spaces_most", () => BuildCatalogue.SiteSpacesMost.ToString(Inv))
                .Check("reallife_bld_test_sites", () => ConstructionDirector.TestSitesNow(), "Construction check (sites of 1, 2 and 3 spaces bought, joined and planned)", RunPlace.Here)
                .Check("reallife_bld_test_hire_quotes", () => ConstructionDirector.TestHireQuotesNow(), "Construction check (a builder's quotes shown)", RunPlace.Here)
                .Check("reallife_bld_test_hire", () => ConstructionDirector.TestHireNow(), "Construction check (a client project built by a rival now)", RunPlace.Here)
                .Check("reallife_bld_test_hire_late", () => ConstructionDirector.TestHireLateNow(), "Construction check (a late builder pays its penalty)", RunPlace.Here)
                .Check("reallife_bld_test_hire_worked", () => ConstructionDirector.TestHireWorkedNow(), "Construction check (a hired builder's site worked to its keys)", RunPlace.Here)
                .Check("reallife_bld_test_desk_go", () => ConstructionDirector.TestDeskGoNow(), "Construction check (take me to a builder's desk)", RunPlace.Here)
                .Test("reallife_bld_test_desk_talk", () => ConstructionDirector.TestDeskTalkNow())
                .Check("reallife_bld_test_desk_pages", () => ConstructionDirector.TestDeskPagesNow(), "Construction check (a builder's desk pages)", RunPlace.Here)
                .Check("reallife_bld_test_map", () => ConstructionDirector.TestMapNow(), "Construction check (show on map)", RunPlace.Here)
                .Check("reallife_bld_test_track", () => ConstructionDirector.TestTrackNow(), "Construction check (objectives)", RunPlace.Here)
                .Test("reallife_bld_mode_test_open", () => ConstructionDirector.TestBuildModeOpenNow())
                .Check("reallife_bld_mode_test_price", () => ConstructionDirector.TestBuildModePriceNow(), "Construction check (build mode: price)", RunPlace.Here)
                .Check("reallife_bld_mode_test_buy", () => ConstructionDirector.TestBuildModeBuyNow(), "Construction check (build mode: buy)", RunPlace.Here)
                .Check("reallife_bld_mode_test_zone", () => ConstructionDirector.TestBuildModeZoneNow(), "Construction check (build mode: zone)", RunPlace.Here)
                .Check("reallife_bld_mode_test_place", () => ConstructionDirector.TestBuildModePlaceNow(), "Construction check (build mode: place)", RunPlace.Here)
                .Test("reallife_bld_mode_test_bulldoze", () => ConstructionDirector.TestBuildModeBulldozeNow())
                .Check("reallife_bld_mode_test_complete", () => ConstructionDirector.TestBuildModeCompleteNow(), "Construction check (build mode: whole buildings)", RunPlace.Here)
                .Test("reallife_bld_mode_test_stand", () => ConstructionDirector.TestBuildModeStandNow())
                .Test("reallife_bld_mode_test_layer", () => ConstructionDirector.TestBuildModeLayerNow())
                .Test("reallife_bld_mode_test_zoning", () => ConstructionDirector.TestBuildModeZoningNow())
                .Test("reallife_bld_mode_test_window", () => ConstructionDirector.TestBuildModeWindowNow())
                .Test("reallife_bld_mode_test_hints", () => ConstructionDirector.TestBuildModeHintsNow())
                .Test("reallife_bld_mode_test_tools", () => ConstructionDirector.TestBuildModeToolsNow())
                .Test("reallife_bld_mode_test_tour", () => ConstructionDirector.TestBuildModeTourNow())
                .Test("reallife_bld_mode_test_city", () => ConstructionDirector.TestBuildModeCityNow())
                .Test("reallife_bld_mode_test_cursor", () => ConstructionDirector.TestBuildModeCursorNow())
                .Test("reallife_bld_mode_test_plot", () => ConstructionDirector.TestBuildModePlotNow())
                .Test("reallife_corner_test_stand", () => ConstructionDirector.TestCornerStandNow())
                .Check("reallife_corner_test_check", () => ConstructionDirector.TestCornerCheckNow(), "Construction check (corner buildings: check)", RunPlace.Here)
                .Check("reallife_bld_test_show_flat", () => ConstructionDirector.TestShowFlatNow(), "Show flats check (a show flat at the test project now)", RunPlace.Here)
                .Check("reallife_bld_test_viewing", () => ConstructionDirector.TestViewingNow(), "Show flats check (a viewing now)", RunPlace.Here)
                .Check("reallife_bld_test_show_buyer", () => ConstructionDirector.TestShowBuyerNow(), "Show flats check (a buyer at the show flat now)", RunPlace.Here)
                .Check("reallife_bld_test_refit_start", () => ConstructionDirector.TestRefitStartNow(), "Renovation check (a refit under way at your nearest business now)", RunPlace.Here)
                .Check("reallife_bld_test_refit", () => ConstructionDirector.TestRefitNow(), "Renovation check (renovate the nearest shop of yours now)", RunPlace.Here)
                .Check("reallife_bld_test_refit_tender", () => ConstructionDirector.TestRefitTenderNow(), "Renovation check (a business's refit tender now)", RunPlace.Here)
                .Check("reallife_bld_test_show_visit", () => ConstructionDirector.TestShowVisitNow(), "Show flats check (into the show flat now)", RunPlace.Outdoors)
                .Check("reallife_bld_test_refurnish", () => ConstructionDirector.TestRefurnishNow(),
                    "Redevelopment check (refurnish your nearest unit with one of the game's designs now)", RunPlace.Here)
                .Check("reallife_bld_test_city_refurnish", () => ConstructionDirector.TestCityRefurnishNow(),
                    "Redevelopment check (a business of the city's refitted by a rival and furnished anew now)", RunPlace.Here)
                .Check("reallife_bld_test_join_split", () => ConstructionDirector.TestJoinSplitNow(),
                    "Redevelopment check (two sites of yours joined into one and split back now)", RunPlace.Here)
                .Test("reallife_bld_test_demolish", () => ConstructionDirector.TestDemolitionNow())
                .Test("reallife_bld_test_cleared_build", () => ConstructionDirector.TestClearedBuildNow())
                .Test("reallife_bld_test_extend", () => ConstructionDirector.TestExtensionNow())
                .Test("reallife_bld_test_convert", () => ConstructionDirector.TestConversionNow())
                .Check("reallife_bld_test_city_choices", () => ConstructionDirector.TestCityChoicesNow(),
                    "Redevelopment check (the city's developers' choices now, nothing changed)", RunPlace.Here)
                .Test("reallife_bld_test_city_redevelop", () => ConstructionDirector.TestCityRedevelopNow())
                .Test("reallife_bld_test_lot", "reallife_bld_test_company", "reallife_bld_test_licence", "reallife_bld_test_plans", "reallife_bld_test_planning",
                    "reallife_bld_test_council", "reallife_bld_test_stage", "reallife_bld_test_inspection", "reallife_bld_test_deliveries", "reallife_bld_test_handover",
                    "reallife_bld_test_opening", "reallife_bld_test_opening_missed", "reallife_bld_test_tender", "reallife_bld_test_award", "reallife_bld_test_developers", "reallife_bld_test_poach",
                    "reallife_bld_test_strike", "reallife_bld_test_fault", "reallife_bld_test_bribe", "reallife_bld_test_buyer", "reallife_bld_test_back")
                .Check("reallife_bld_test_city_code", Wiring.CityTests.Code, "Civic check (building code)", RunPlace.Here)
                .Check("reallife_bld_test_city_zoning", Wiring.CityTests.Zoning, "Construction check (council zoning)", RunPlace.Here)
                .Check("reallife_bld_test_city_motions", Wiring.CityTests.Office, "Civic check (council motions)", RunPlace.Here)
                .Check("reallife_bld_test_city_ring", Wiring.CityTests.Ring, "Construction check (ring)", RunPlace.Here)
                .Check("reallife_bld_test_city_bills", Wiring.CityTests.Bills, "Construction check (bills)", RunPlace.Here)
                .Check("reallife_bld_test_city_award", Wiring.CityTests.YearAward, "Construction check (building of the year)", RunPlace.Here)
                .Cleanup(() => ConstructionDirector.Instance?.StandDown("its things were put away"));
        }

        private static string Money(int amount) => Guide.Money(Prices.At(amount));

        private static string Share(int thousandths) => ConstructionText.Share(thousandths);
    }

    /// <summary>The construction game's place on the options page: its six options and the test entries (shown with the test tools).</summary>
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
                .AddToggle(Settings.ConstructionShowFlatsId, "reallife_bld_show_flats", Settings.ConstructionShowFlats, v => Settings.ConstructionShowFlats = v)
                .AddToggle(Settings.ConstructionRenovationsId, "reallife_bld_renovations", Settings.ConstructionRenovations, v => Settings.ConstructionRenovations = v)
                .AddToggle(Settings.ConstructionCityId, "reallife_bld_city_option", Settings.ConstructionCity, v => Settings.ConstructionCity = v)
                .AddToggle(Settings.ConstructionRedevelopId, "reallife_bld_redevelop_option", Settings.ConstructionRedevelop, v => Settings.ConstructionRedevelop = v)
                .AddDropdown(Settings.ConstructionCityRedevelopId, "reallife_bld_city_redevelop_option",
                    new[] { "reallife_bld_city_rate_off", "reallife_bld_city_rate_slow", "reallife_bld_city_rate_usual", "reallife_bld_city_rate_lively" },
                    Settings.ConstructionCityRedevelop, v => Settings.ConstructionCityRedevelop = v)
                .AddToggle(Settings.ConstructionTrackId, "reallife_bld_track_option", Settings.ConstructionTrack, v => Settings.ConstructionTrack = v)
                .AddToggle(Settings.ConstructionBuildModeId, "reallife_bld_mode_option", Settings.ConstructionBuildMode, v => Settings.ConstructionBuildMode = v)
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
                .AddButton("reallife_bld_test_opening_missed", () => ConstructionDirector.Instance?.TestOpeningMissed())
                .AddButton("reallife_bld_test_tender", () => ConstructionDirector.Instance?.TestTender())
                .AddButton("reallife_bld_test_award", () => ConstructionDirector.Instance?.TestAward())
                .AddButton("reallife_bld_test_developers", () => ConstructionDirector.Instance?.TestDevelopers())
                .AddButton("reallife_bld_test_poach", () => ConstructionDirector.Instance?.TestPoach())
                .AddButton("reallife_bld_test_strike", () => ConstructionDirector.Instance?.TestStrike())
                .AddButton("reallife_bld_test_fault", () => ConstructionDirector.Instance?.TestFault())
                .AddButton("reallife_bld_test_bribe", () => ConstructionDirector.Instance?.TestBribe())
                .AddButton("reallife_bld_test_buyer", () => ConstructionDirector.Instance?.TestBuyer())
                .AddButton("reallife_bld_test_back", () => ConstructionDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
