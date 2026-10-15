using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The end game at scale's entry in the mod's feature list (Core/EndScale; the ventures it scales keep their own
    /// figures and pages), its option and preset (off in Relaxed: the cheaper figures of before), its two tests, and the
    /// figures the Guide's page "The end game at scale" prints: every {name} in its text is filled from the ventures'
    /// catalogues at scale here, so the page can never say anything the rules do not do. Called once, when the feature
    /// list is built (Meta/FeatureList); each figure is worked out when the page is opened. Cost: nothing per frame.
    /// </summary>
    public static class EndScaleGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The share the Guide's figures of the late ventures are given at: the whole scale with its option on, none off.</summary>
        public static int Scale => Settings.EndScale ? EndScaleRules.Full : 0;

        public static void Register()
        {
            Features.Add("economy_endscale", "reallife_meta_title_endscale")
                .About("reallife_guide_endscale_about").Help("reallife_guide_endscale")
                .Owns(Settings.EndScaleId)
                .Preset(Settings.EndScaleId, 0, 1, 1)
                .Test("reallife_endscale_test_phase", "reallife_endscale_test_full")
                .Fact("es_days", () => Whole(EndScaleRules.PhaseDays))
                .Fact("es_years_least", () => Whole(EndScaleRules.PaybackYearsLeast))
                .Fact("es_years_most", () => Whole(EndScaleRules.PaybackYearsMost))
                .Fact("es_working", () => Whole(EndScaleRules.WorkingLeast / 10) + "%")
                .Fact("es_outages", () => Whole(EndScaleRules.OutageMost / 1000))
                .Fact("es_pace", () => Whole(InfraCatalogue.CrewPace / 10) + "%")
                // data centres, the cloud and the hyperscaler
                .Fact("es_fit", () => Cents(InfraRules.FitCost(1, 0, EndScaleRules.Full)) + " " + TechText.T("reallife_guide_tech_to") + " " + Cents(InfraRules.FitCost(1, 3, EndScaleRules.Full)))
                .Fact("es_rack", () => Cents(InfraCatalogue.RackCostScale))
                .Fact("es_gpu", () => Cents(InfraCatalogue.GpuRackCostScale))
                .Fact("es_units", () => Whole(InfraCatalogue.UnitsPerRackScale))
                .Fact("es_units_old", () => Whole(InfraCatalogue.UnitsPerRack))
                .Fact("es_gpu_units", () => Whole(InfraCatalogue.GpuUnitsPerRackScale))
                .Fact("es_gpu_units_old", () => Whole(InfraCatalogue.GpuUnitsPerRack))
                .Fact("es_upkeep_rack", () => Cents(InfraCatalogue.UpkeepRack))
                .Fact("es_upkeep_gpu", () => Cents(InfraCatalogue.UpkeepGpu))
                .Fact("es_facility", () => Mille(InfraCatalogue.FacilityYear))
                .Fact("es_cooling", () => Mille(InfraCatalogue.PueScale[0] - 1000) + " " + TechText.T("reallife_guide_tech_to") + " " + Mille(InfraCatalogue.PueScale[InfraCatalogue.Tiers - 1] - 1000))
                .Fact("es_operators", () => Whole(InfraCatalogue.OperatorRacks))
                .Fact("es_operators_gpu", () => Whole(InfraCatalogue.OperatorGpu))
                .Fact("es_engineer_kw", () => Whole(InfraCatalogue.EngineerKw))
                .Fact("es_guards", () => Whole(InfraCatalogue.GuardsLeast))
                .Fact("es_guard_sqm", () => Whole(InfraCatalogue.GuardSqm))
                .Fact("es_ops_units", () => Whole(InfraCatalogue.OpsUnits))
                .Fact("es_dc_wages", () => Cents(InfraCatalogue.CrewWage[InfraCatalogue.Security]) + " " + TechText.T("reallife_guide_tech_to") + " " + Cents(InfraCatalogue.OpsWage))
                .Fact("es_cloud_licence", () => Cents(InfraCatalogue.LicenceFeeScale))
                .Fact("es_cloud_cash", () => Cents(InfraCatalogue.CloudCashLeastScale))
                .Fact("es_cloud_racks", () => Whole(InfraCatalogue.CloudRacksLeastScale))
                .Fact("es_hyper_cash", () => Cents(InfraCatalogue.HyperCashScale))
                .Fact("es_hyper_racks", () => Whole(InfraCatalogue.HyperRacksScale))
                .Fact("es_market", () => Whole(InfraCatalogue.CloudDemandScale / InfraCatalogue.CloudDemand))
                .Fact("es_hyper_market", () => Whole(InfraCatalogue.HyperDemandScale / 1000))
                .Fact("es_region_market", () => (InfraCatalogue.HyperRegionScale / 1000f).ToString("0.#", Inv))
                // the broadband company
                .Fact("es_bb_market", () => Whole(BroadbandCatalogue.MarketScale / 1000))
                .Fact("es_bb_home", () => Cents(BroadbandCatalogue.HomeCentsScale))
                .Fact("es_bb_far", () => Cents(BroadbandCatalogue.HomeCentsFarScale))
                .Fact("es_bb_licence", () => Guide.Money(Prices.At((float)BroadbandCatalogue.LicenceFeeScale)))
                .Fact("es_bb_build", () => Guide.Money(BroadbandRules.BuildCostRange(Prices.Level, EndScaleRules.Full, false)) + " " + TechText.T("reallife_guide_tech_to")
                                            + " " + Guide.Money(BroadbandRules.BuildCostRange(Prices.Level, EndScaleRules.Full, true)))
                .Fact("es_bb_upkeep", () => Mille(BroadbandCatalogue.UpkeepPerMilleScale))
                .Fact("es_bb_install", () => Cents(BroadbandCatalogue.InstallCentsScale))
                .Fact("es_bb_field", () => Whole(BroadbandCatalogue.FieldConnections))
                .Fact("es_bb_calls", () => Whole(BroadbandCatalogue.CallConnections))
                .Fact("es_bb_ops", () => Whole(BroadbandCatalogue.OperationsPerDistrict))
                // the bank
                .Fact("es_ob_licence", () => Cents(OwnBankCatalogue.LicenceCentsScale))
                .Fact("es_ob_capital", () => Cents(OwnBankCatalogue.CapitalLeastCentsScale))
                .Fact("es_ob_pool", () => Whole(OwnBankCatalogue.PoolScale / 1000))
                .Fact("es_ob_tellers", () => Cents(OwnBankCatalogue.TellerCents))
                .Fact("es_ob_managers", () => Cents(OwnBankCatalogue.ManagerCents))
                .Fact("es_ob_officers", () => Cents(OwnBankCatalogue.OfficerCents))
                .Fact("es_ob_compliance", () => Whole(OwnBankCatalogue.ComplianceLeast))
                .Fact("es_ob_compliance_per", () => Cents(OwnBankCatalogue.ComplianceCents))
                .Fact("es_ob_systems", () => Cents(OwnBankCatalogue.SystemsDayCents))
                .Fact("es_ob_insurance", () => (OwnBankCatalogue.InsuranceBp / 100.0).ToString("0.##", Inv) + "%");
        }

        // An amount in cents at the start's prices, at today's.
        private static string Cents(long cents)
        {
            return Guide.Money(Prices.At(cents / 100f));
        }

        private static string Mille(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
