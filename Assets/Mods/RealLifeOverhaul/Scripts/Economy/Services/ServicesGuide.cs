using System.Globalization;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The figures the Guide's pages "Cleaning service" and "Selling a business" print: every {name} in their text is
    /// filled from <see cref="ServicesCatalogue"/> here, so the pages can never say anything the rules do not do.
    /// Called once, when the feature list is built (Meta/FeatureList); each figure is worked out when a page is opened.
    /// </summary>
    public static class ServicesGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddCleaningFacts(Feature f)
        {
            f.Fact("svc_walk", () => Tenths(ServicesCatalogue.WalkPerMinute / 10))
                .Fact("svc_van", () => Tenths(ServicesCatalogue.VanPerMinute / 10))
                .Fact("svc_street", () => Whole(ServicesCatalogue.StreetFactor - 100))
                .Fact("svc_van_cost", () => Guide.Money(ServicesCatalogue.VanCostPerKm))
                .Fact("svc_visit_least", () => Whole(ServicesCatalogue.VisitLeast))
                .Fact("svc_visit_most", () => Whole(ServicesCatalogue.VisitMost))
                .Fact("svc_visits", () => Whole(ServicesCatalogue.VisitsMost))
                .Fact("svc_threshold", () => Whole(ServicesCatalogue.ThresholdDefault))
                .Fact("svc_travel_most", () => Whole(ServicesCatalogue.TravelMost))
                .Fact("svc_need_days", () => Whole(ServicesCatalogue.NeedDaysAverage))
                .Fact("svc_offers", () => Whole(ServicesCatalogue.OffersMost))
                .Fact("svc_offer_every", () => Whole(ServicesCatalogue.OfferEveryDays))
                .Fact("svc_offer_open", () => Whole(ServicesCatalogue.OfferOpenDays))
                .Fact("svc_contracts", () => Whole(ServicesCatalogue.ContractsMost))
                .Fact("svc_days_under", () => Whole(ServicesCatalogue.ContractDaysUnderMost))
                .Fact("svc_penalty", () => Whole(ServicesCatalogue.ContractPenaltyPct))
                .Fact("svc_misses", () => Whole(ServicesCatalogue.ContractMissesMost))
                .Fact("svc_fee_least", () => Guide.Money(ServicesCatalogue.ContractFeeLeast))
                .Fact("svc_crew_most", () => Whole(ServicesCatalogue.CrewMost))
                .Fact("svc_clients_most", () => Whole(ServicesCatalogue.ClientsMost));
        }

        public static void AddSaleFacts(Feature f)
        {
            f.Fact("sale_days", () => Whole(VenturesCatalogue.OfferValueDays))
                .Fact("sale_history", () => Whole(ServicesCatalogue.HistoryDays))
                .Fact("sale_boom", () => Whole(ServicesCatalogue.ClimateBoomPct))
                .Fact("sale_slow", () => Whole(ServicesCatalogue.ClimateSlowPct))
                .Fact("sale_district_least", () => Whole(ServicesCatalogue.DistrictLeastPct))
                .Fact("sale_district_most", () => Whole(ServicesCatalogue.DistrictMostPct))
                .Fact("sale_asking_least", () => Whole(ServicesCatalogue.AskingPct[0]))
                .Fact("sale_asking_most", () => Whole(ServicesCatalogue.AskingPct[ServicesCatalogue.AskingPct.Length - 1]))
                .Fact("sale_offers_value", () => Whole(ServicesCatalogue.AskingOffersMost[ServicesCatalogue.AskingDefault]))
                .Fact("sale_gap_value", () => Whole(ServicesCatalogue.AskingGapDays[ServicesCatalogue.AskingDefault]))
                .Fact("sale_listing_days", () => Whole(ServicesCatalogue.ListingDays))
                .Fact("sale_quick", () => Whole(ServicesCatalogue.QuickPct))
                .Fact("sale_rival", () => Whole(ServicesCatalogue.BuyerPct[(int)BuyerKind.Rival]))
                .Fact("sale_investor", () => Whole(ServicesCatalogue.BuyerPct[(int)BuyerKind.Investor]))
                .Fact("sale_staff", () => Whole(ServicesCatalogue.BuyerPct[(int)BuyerKind.Staff]))
                .Fact("sale_chain", () => Whole(ServicesCatalogue.BuyerPct[(int)BuyerKind.Chain]))
                .Fact("sale_staff_parts", () => Whole(ServicesCatalogue.BuyerInstalments[(int)BuyerKind.Staff]))
                .Fact("sale_chain_parts", () => Whole(ServicesCatalogue.BuyerInstalments[(int)BuyerKind.Chain]))
                .Fact("sale_rival_clause", () => Whole(ServicesCatalogue.BuyerClauseDays[(int)BuyerKind.Rival]))
                .Fact("sale_chain_clause", () => Whole(ServicesCatalogue.BuyerClauseDays[(int)BuyerKind.Chain]))
                .Fact("sale_offer_least", () => Whole(ServicesCatalogue.OfferDaysLeast))
                .Fact("sale_offer_most", () => Whole(ServicesCatalogue.OfferDaysMost))
                .Fact("sale_counter", () => Whole(ServicesCatalogue.CounterStepPct))
                .Fact("sale_walk", () => Whole(ServicesCatalogue.CounterWalkPct))
                .Fact("sale_part_days", () => Whole(ServicesCatalogue.InstalmentDays))
                .Fact("sale_clause_penalty", () => Whole(ServicesCatalogue.ClausePenaltyPct));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        // Tenths as a figure with one decimal: 35 is "3.5".
        private static string Tenths(int tenths)
        {
            return (tenths / 10f).ToString("0.#", Inv);
        }
    }
}
