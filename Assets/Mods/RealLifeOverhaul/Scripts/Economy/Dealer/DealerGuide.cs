using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The figures the Guide's page "Car dealership" prints: every {name} in its text is filled from
    /// <see cref="DealerCatalogue"/> here, so the page can never say anything the rules do not do. Called once, when the
    /// feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class DealerGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddFacts(Feature f)
        {
            f.Fact("dealer_places_least", () => Whole(DealerCatalogue.SpotsLeast))
                .Fact("dealer_places_most", () => Whole(DealerCatalogue.SpotsMost))
                .Fact("dealer_markup", () => Share(DealerCatalogue.MarkUp))
                .Fact("dealer_wholesale_least", () => Share(DealerCatalogue.WholesaleLeast))
                .Fact("dealer_wholesale_most", () => Share(DealerCatalogue.WholesaleMost))
                .Fact("dealer_wholesale_count", () => Whole(DealerCatalogue.WholesaleADay))
                .Fact("dealer_used_count", () => Whole(DealerCatalogue.UsedADay))
                .Fact("dealer_inspection", () => Share(DealerCatalogue.InspectionShare))
                .Fact("dealer_inspection_least", () => Guide.Money(Prices.At(DealerCatalogue.InspectionLeast)))
                .Fact("dealer_inspection_most", () => Guide.Money(Prices.At(DealerCatalogue.InspectionMost)))
                .Fact("dealer_haggles", () => Whole(DealerCatalogue.HagglesMost))
                .Fact("dealer_lots", () => Whole(DealerCatalogue.AuctionLots))
                .Fact("dealer_auction_from", () => Hour(DealerCatalogue.AuctionFrom))
                .Fact("dealer_auction_to", () => Hour(DealerCatalogue.AuctionTo))
                .Fact("dealer_opening", () => Share(DealerCatalogue.OpeningShare))
                .Fact("dealer_fee", () => Share(DealerCatalogue.BuyersFee))
                .Fact("dealer_lot_inspection", () => Guide.Money(Prices.At(DealerCatalogue.AuctionInspection)))
                .Fact("dealer_no_pay", () => Whole(DealerCatalogue.NoPayDays))
                .Fact("dealer_visitors", () => Share(DealerCatalogue.VisitorShare))
                .Fact("dealer_serve", () => Whole(DealerCatalogue.ServePerStaff))
                .Fact("dealer_floor", () => Share(DealerCatalogue.StaffFloor))
                .Fact("dealer_deal_hours", () => Whole(DealerCatalogue.DealHours))
                .Fact("dealer_counters", () => Whole(DealerCatalogue.CountersMost))
                .Fact("dealer_trade", () => Share(DealerCatalogue.TradeShare))
                .Fact("dealer_finance_least", () => Guide.Money(Prices.At(DealerCatalogue.FinanceLeast)))
                .Fact("dealer_deposit_least", () => Whole(DealerCatalogue.DepositLeast) + "%")
                .Fact("dealer_deposit_most", () => Whole(DealerCatalogue.DepositMost) + "%")
                .Fact("dealer_threshold", () => Whole(CreditCatalogue.LendingThreshold))
                .Fact("dealer_rate_points", () => Whole(DealerCatalogue.OwnRate + ProductCatalogue.CarRatePoints))
                .Fact("dealer_misses", () => Whole(DealerCatalogue.MissesToDefault))
                .Fact("dealer_recover", () => Share(DealerCatalogue.RecoverShare))
                .Fact("dealer_tiers", Tiers)
                .Fact("dealer_claims", () => Whole(DealerCatalogue.ClaimsMost))
                .Fact("dealer_claim_rest", () => Whole(DealerCatalogue.ClaimRest));
        }

        // One line per warranty: its name, how long it covers and its premium.
        private static string Tiers()
        {
            var text = new System.Text.StringBuilder();
            for (int t = 0; t < DealerCatalogue.Tiers; t++)
            {
                text.Append("- ").Append(DealerText.Fill("reallife_guide_economy_dealer_tier", "name", DealerText.Tier(t), "days", Whole(DealerCatalogue.TierDays[t]),
                    "premium", Share(DealerCatalogue.TierPremium[t]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Hour(int hour)
        {
            return hour.ToString("00", Inv) + ":00";
        }

        // Thousandths as a per cent: "25%", "2.5%".
        private static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }
    }
}
