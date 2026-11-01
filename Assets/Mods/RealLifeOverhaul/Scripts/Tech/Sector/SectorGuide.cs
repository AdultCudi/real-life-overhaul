using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms' entry in the mod's feature list: their option, their save line, their eight tests and the Guide
    /// page's figures, each {name} filled from the catalogues when the page opens. Called once when the feature list is built.
    /// </summary>
    public static class SectorGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("firms", "reallife_meta_title_firms")
                .About("reallife_guide_firms_about").Help("reallife_guide_firms")
                .Owns(Settings.LivingFirmsId)
                .Data(SectorStore.Key)
                .Test("reallife_tech_mkt_test_software", "reallife_tech_mkt_test_lab", "reallife_tech_mkt_test_consultancy", "reallife_tech_mkt_test_security",
                    "reallife_tech_mkt_test_agency", "reallife_tech_mkt_test_centres", "reallife_tech_mkt_test_markets", "reallife_tech_mkt_test_year")
                .ResetData(() => TechDirector.Instance?.ForgetSector())
                .Fact("firms_world_people", () => Times(SectorCatalogue.World[(int)SectorBuyers.People]))
                .Fact("firms_world_businesses", () => Times(SectorCatalogue.World[(int)SectorBuyers.Businesses]))
                .Fact("firms_world_companies", () => Times(SectorCatalogue.World[(int)SectorBuyers.Enterprises]))
                .Fact("firms_world_growth", () => Mille(SectorCatalogue.WorldGrowth[(int)SectorBuyers.People]))
                .Fact("firms_small", () => Whole(SectorCatalogue.SmallLeast))
                .Fact("firms_large", () => Whole(SectorCatalogue.LargeLeast))
                .Fact("firms_anchor_days", () => Whole(SectorRules.AnchorDays))
                .Fact("firms_products_firm", () => Whole(SectorCatalogue.ProductsPerFirm))
                .Fact("firms_products_most", () => Whole(SectorCatalogue.ProductsMost))
                .Fact("firms_contracts_most", () => Whole(SectorCatalogue.ContractsMost))
                .Fact("firms_offers_most", () => Whole(FirmsCatalogue.OffersMost))
                .Fact("firms_rivals", () => Whole(Companies()))
                .Fact("firms_cut", () => Mille(SectorCatalogue.RivalCutLoss))
                .Fact("firms_poach_days", () => Whole(SectorRules.PoachDays))
                .Fact("firms_sprint_push", () => Mille(SectorCatalogue.SprintPush))
                .Fact("firms_rollback", () => Mille(SectorCatalogue.RollbackSaved))
                .Fact("firms_hotfix_risk", () => Mille(SectorCatalogue.HotfixRisk))
                .Fact("firms_big_months", () => Whole(SectorCatalogue.BigDeal))
                .Fact("firms_train_days", () => Whole(SectorCatalogue.TrainRunDays))
                .Fact("firms_scrape_risk", () => Mille(SectorCatalogue.ScrapeRisk))
                .Fact("firms_skip_risk", () => Mille(SectorCatalogue.SafetyIncident[2]))
                .Fact("firms_scope_more", () => Mille(SectorCatalogue.ScopeMore))
                .Fact("firms_incident_days", () => Whole(SectorCatalogue.IncidentDays))
                .Fact("firms_spike_least", () => Times(SectorCatalogue.SpikeLeast))
                .Fact("firms_spike_most", () => Times(SectorCatalogue.SpikeMost))
                .Fact("firms_hedge", () => Mille(SectorCatalogue.HedgeFee))
                .Fact("firms_portfolio_days", () => Whole(SectorCatalogue.PortfolioDays))
                .Fact("firms_award_chance", () => Mille(SectorCatalogue.AwardChance));
        }

        // The listed companies that sell software in the shared markets.
        private static int Companies()
        {
            int n = 0;
            for (int c = 0; c < SectorCatalogue.CompanyMarkets.Length; c++)
            {
                n += SectorCatalogue.CompanyMarkets[c] != 0 ? 1 : 0;
            }
            return n;
        }

        private static string Whole(int value) => value.ToString(Inv);

        private static string Mille(int thousandths) => (thousandths / 10f).ToString("0", Inv) + "%";

        private static string Times(int thousandths) => (thousandths / 1000f).ToString("0.#", Inv);
    }
}
