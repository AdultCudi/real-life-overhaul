using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>Fills the {names} of the Guide's car dealership page from <see cref="DealerCatalogue"/> when it is opened.</summary>
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
                .Fact("dealer_claim_rest", () => Whole(DealerCatalogue.ClaimRest))
                .Fact("dealer_events", Events)
                .Fact("dealer_event_rest", () => Whole(DealerCatalogue.EventRest))
                .Fact("dealer_promotion_top", () => Whole(DealerCatalogue.PromotionMost))
                .Fact("dealer_steps", Steps)
                .Fact("dealer_rep_sale", () => Whole(DealerCatalogue.RepSale))
                .Fact("dealer_rep_good", () => Whole(DealerCatalogue.RepGoodCar))
                .Fact("dealer_rep_fault", () => Whole(DealerCatalogue.RepFault))
                .Fact("dealer_missions", Missions)
                .Fact("dealer_extras", Extras)
                .Fact("dealer_workshop_bays", () => Whole(DealerCatalogue.WorkshopBays))
                .Fact("dealer_workshop_restore", () => Share(DealerCatalogue.WorkshopRestore))
                .Fact("dealer_workshop_least", () => Whole(DealerCatalogue.WorkshopHoursLeast))
                .Fact("dealer_workshop_most", () => Whole(DealerCatalogue.WorkshopHoursMost))
                .Fact("dealer_prepare_restore", () => Share(DealerCatalogue.PrepareRestore))
                .Fact("dealer_delivery_least", () => Whole(DealerCatalogue.DeliveryDaysLeast))
                .Fact("dealer_delivery_most", () => Whole(DealerCatalogue.DeliveryDaysMost))
                .Fact("dealer_delivery_hour", () => Hour(DealerCatalogue.DeliveryHour))
                .Fact("dealer_report_days", () => Whole(DealerCatalogue.ReportDays))
                .Fact("dealer_average_days", () => Whole(DealerCatalogue.AverageDays));
        }

        private static string Events()
        {
            var text = new System.Text.StringBuilder();
            for (int k = 0; k < DealerCatalogue.EventKinds; k++)
            {
                text.Append("- ").Append(DealerText.Fill("reallife_guide_economy_dealer_event", "name", DealerText.Event(k, 0), "days", Whole(DealerCatalogue.EventDays[k]),
                    "cost", Guide.Money(Prices.At(DealerCatalogue.EventCost[k])), "does", DealerText.EventShort(k))).Append('\n');
            }
            return text.ToString();
        }

        private static string Steps()
        {
            var text = new System.Text.StringBuilder();
            for (int s = 1; s < DealerCatalogue.RepSteps.Length; s++)
            {
                text.Append("- ").Append(DealerText.Fill("reallife_dealer_rep_step", "step", DealerText.Step(s), "points", Whole(DealerCatalogue.RepSteps[s]), "perk", DealerText.Perk(s)))
                    .Append('\n');
            }
            return text.ToString();
        }

        private static string Missions()
        {
            var text = new System.Text.StringBuilder();
            for (int m = 0; m < DealerCatalogue.Missions; m++)
            {
                text.Append("- ").Append(DealerText.Fill("reallife_guide_economy_dealer_mission", "name", DealerText.Mission(m), "about", DealerText.MissionAbout(m),
                    "points", Whole(DealerCatalogue.MissionReward[m]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Extras()
        {
            var text = new System.Text.StringBuilder();
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                text.Append("- ").Append(DealerText.Fill("reallife_guide_economy_dealer_extra", "name", DealerText.Extra(e), "price", Share(DealerCatalogue.ExtraPrice[e]),
                    "least", Guide.Money(Prices.At(DealerCatalogue.ExtraLeast[e])), "cost", Share(DealerCatalogue.ExtraCost[e]), "takeup", Share(DealerCatalogue.ExtraTakeUp[e]))).Append('\n');
            }
            return text.ToString();
        }

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
