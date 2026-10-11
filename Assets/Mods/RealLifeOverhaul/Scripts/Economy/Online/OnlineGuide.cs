using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The figures the Guide's page "Online shops" prints: every {name} in its text is filled from
    /// <see cref="OnlineCatalogue"/> here, so the page can never say anything the rules do not do. Called once, when
    /// the feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class OnlineGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] KindKeys =
        {
            "reallife_guide_economy_online_kind_fresh", "reallife_guide_economy_online_kind_groceries", "reallife_guide_economy_online_kind_clothing",
            "reallife_guide_economy_online_kind_electronics", "reallife_guide_economy_online_kind_home", "reallife_guide_economy_online_kind_general"
        };

        public static void AddFacts(Feature f)
        {
            f.Fact("online_sites", Sites)
                .Fact("online_shops", () => Whole(OnlineCatalogue.ShopsMost))
                .Fact("online_lines", () => Whole(OnlineCatalogue.LinesMost))
                .Fact("online_agency", () => Tenths(OnlineCatalogue.AgencyDiscount))
                .Fact("online_agency_orders", () => Tenths(OnlineCatalogue.AgencyOrders - 1000))
                .Fact("online_price_ceiling", () => Tenths(OnlineCatalogue.PriceCeiling - 1000))
                .Fact("online_same_day", () => Tenths(OnlineCatalogue.PromiseFactor[0] - 1000))
                .Fact("online_same_hours", () => Whole(OnlineCatalogue.SameDayHours))
                .Fact("online_cutoff", () => Hour(OnlineCatalogue.SameDayCutoff))
                .Fact("online_next_morning", () => Hour(OnlineCatalogue.NextMorning))
                .Fact("online_next_hour", () => Hour(OnlineCatalogue.NextDayHour))
                .Fact("online_pick", () => Whole(OnlineCatalogue.PickPerHour))
                .Fact("online_wait", () => Whole(OnlineCatalogue.WaitMost))
                .Fact("online_warehouse_from", () => Hour(OnlineCatalogue.WarehouseFrom))
                .Fact("online_warehouse_until", () => Hour(OnlineCatalogue.WarehouseUntil))
                .Fact("online_drive_from", () => Hour(OnlineCatalogue.DriveFrom))
                .Fact("online_drive_until", () => Hour(OnlineCatalogue.DriveUntil))
                .Fact("online_driver_hours", () => (OnlineCatalogue.DriverMinutes / 60f).ToString("0.#", Inv))
                .Fact("online_van_km", () => Guide.Money(OnlineCatalogue.VanCostPerKm / 100f))
                .Fact("online_courier_base", () => Guide.Money(OnlineCatalogue.CourierBase / 100f))
                .Fact("online_courier_km", () => Guide.Money(OnlineCatalogue.CourierPerKm / 100f))
                .Fact("online_courier_hours", () => Whole(OnlineCatalogue.CourierHours))
                .Fact("online_late_refund", () => Tenths(OnlineCatalogue.LateRefund))
                .Fact("online_missing_hours", () => Whole(OnlineCatalogue.MissingHours))
                .Fact("online_rating_move", () => ((OnlineCatalogue.StarsMost - OnlineCatalogue.StarsLeast) / (float)OnlineCatalogue.RatingWeight / 1000f).ToString("0.##", Inv))
                .Fact("online_stars_low", () => Tenths(OnlineRules.StarsFactor(OnlineCatalogue.StarsLeast)))
                .Fact("online_stars_high", () => Tenths(OnlineRules.StarsFactor(OnlineCatalogue.StarsMost)))
                .Fact("online_returns", Returns)
                .Fact("online_return_days", () => Whole(OnlineCatalogue.ReturnDays))
                .Fact("online_ad_cost", () => Guide.Money(OnlineCatalogue.AdCost))
                .Fact("online_ad_days", () => Whole(OnlineCatalogue.AdDays))
                .Fact("online_ad_percent", () => Whole(OnlineCatalogue.AdPercent))
                .Fact("online_idle", () => Whole(OnlineCatalogue.IdleAlert));
        }

        // One line per quality of site: what it costs to set up and to host, the orders of an ordinary day and the most in one day.
        private static string Sites()
        {
            var text = new StringBuilder();
            for (int q = 0; q < OnlineCatalogue.Qualities; q++)
            {
                text.Append("- ").Append("reallife_guide_economy_online_site".GetLocalization()
                    .Replace("{name}", OnlineText.Quality(q))
                    .Replace("{setup}", Guide.Money(OnlineCatalogue.SetUp[q]))
                    .Replace("{hosting}", Guide.Money(OnlineCatalogue.Hosting[q]))
                    .Replace("{orders}", Whole(OnlineCatalogue.OrdersADay[q]))
                    .Replace("{most}", Whole(OnlineCatalogue.OrdersMost[q]))).Append('\n');
            }
            return text.ToString();
        }

        // One line per kind of goods: how many orders in a hundred come back and how many of those can be sold again.
        private static string Returns()
        {
            var text = new StringBuilder();
            for (int k = 0; k < OnlineCatalogue.ReturnShare.Length && k < KindKeys.Length; k++)
            {
                text.Append("- ").Append("reallife_guide_economy_online_return".GetLocalization()
                    .Replace("{name}", KindKeys[k].GetLocalization())
                    .Replace("{share}", Tenths(OnlineCatalogue.ReturnShare[k]))
                    .Replace("{resale}", Tenths(OnlineCatalogue.ResaleShare[k]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        // An hour of the day as the first half of "08:00".
        private static string Hour(int hour)
        {
            return hour.ToString("00", Inv);
        }

        // Thousandths as a per cent: "25%", "2.5%".
        private static string Tenths(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }
    }
}
