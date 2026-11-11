using System.Globalization;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>The words of the taxi and delivery shifts: which locale key says what, and a key's text with its names filled in.</summary>
    public static class SideJobText
    {
        public static string T(string key) => key.GetLocalization();

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place. Builds strings: on a press, never a frame.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }

        /// <summary>The firm behind a desk, as the employers' ladder names it.</summary>
        public static string FirmKey(SideKind kind)
        {
            return kind == SideKind.Cab ? "reallife_career_cab_name" : "reallife_career_courier_name";
        }

        /// <summary>What a desk says when it will not sign the driver on.</summary>
        public static string RefusalKey(SideRefusal refusal)
        {
            switch (refusal)
            {
                case SideRefusal.NoCar: return "reallife_job_desk_no_car";
                case SideRefusal.NotMotor: return "reallife_job_desk_no_motor";
                case SideRefusal.Licence: return "reallife_job_desk_no_licence";
                case SideRefusal.Endorsement: return "reallife_job_no_taxi_licence";
                case SideRefusal.Seats: return "reallife_job_desk_no_seats";
                case SideRefusal.Room: return "reallife_job_desk_no_room";
                case SideRefusal.Condition: return "reallife_job_desk_no_condition";
                case SideRefusal.Fuel: return "reallife_job_desk_no_fuel";
                case SideRefusal.DayDone: return "reallife_job_desk_no_day";
                default: return "reallife_job_no_now";
            }
        }
    }

    /// <summary>
    /// The taxi and delivery shifts' entry in the feature list, its keys written out whole as the options checks read them. Its
    /// Guide page's numbers are facts filled from <see cref="SideJobRules"/> and the two gigs' own figures when the page opens.
    /// </summary>
    public static class SideJobGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("side_jobs", "reallife_meta_title_side_jobs")
                .Group("reallife_jobs_header")
                .About("reallife_guide_side_jobs_about").Help("reallife_guide_side_jobs")
                .Owns(Settings.SideJobsId)
                .Data(SideJobStore.Key)
                .Test("reallife_job_desk_test_cab", "reallife_job_desk_test_parcels", "reallife_job_desk_test_books")
                .Fact("desk_minutes", () => SideJobRules.SignOnMinutes.ToString(Inv))
                .Fact("desk_car_within", () => SideJobRules.CarWithin.ToString("0", Inv))
                .Fact("desk_seats", () => SideJobRules.CabSeats.ToString(Inv))
                .Fact("desk_cab_condition", () => SideJobRules.CabCondition.ToString(Inv) + "%")
                .Fact("desk_parcel_condition", () => SideJobRules.ParcelCondition.ToString(Inv) + "%")
                .Fact("desk_fuel", () => SideJobRules.FuelLeast.ToString(Inv) + "%")
                .Fact("desk_cab_day", () => SideJobRules.CabDay.ToString(Inv))
                .Fact("desk_parcel_day", () => SideJobRules.ParcelDay.ToString(Inv))
                .Fact("desk_commission", () => Guide.Percent(SideJobRules.CommissionMille / 1000f))
                .Fact("desk_margin", () => Guide.Percent(SideJobRules.MarginMille / 1000f))
                .Fact("desk_fare_600", () => Guide.Money(SideJobRules.Fare(Cab.UnitPay, Cab.PayPerMetre, 600f, SideJobRules.Par(600f), 1f)))
                .Fact("desk_minute", () => Guide.Money(SideJobRules.SecondsRate * 60f))
                .Fact("desk_time_most", () => SideJobRules.TimeMost.ToString("0.#", Inv))
                .Fact("desk_tip_best", () => Guide.Percent(SideJobRules.TipShare(100)))
                .Fact("desk_offence", () => SideJobRules.OffenceCost.ToString(Inv))
                .Fact("desk_fled", () => SideJobRules.FledCost.ToString(Inv))
                .Fact("desk_late_most", () => SideJobRules.LateMost.ToString(Inv))
                .Fact("desk_drops_most", () => SideJobRules.DropsMost.ToString(Inv))
                .Fact("desk_food_most", () => SideJobRules.FoodMost.ToString(Inv))
                .Fact("desk_food_pay", () => Guide.Percent(SideJobRules.FoodPay - 1f))
                .Fact("desk_drop", () => Guide.Money(Courier.UnitPay))
                .Fact("desk_early", () => Guide.Percent(SideJobRules.EarlyMille / 1000f - 1f))
                .Fact("desk_late_least", () => Guide.Percent(SideJobRules.LateLeastMille / 1000f))
                .Fact("desk_late_full", () => SideJobRules.LateFull.ToString("0", Inv))
                .Cleanup(() => SideJobDirector.Instance?.PutAside())
                .ResetData(() => SideJobDirector.Instance?.Forget());
        }

        private static JobDef Cab => JobCatalogue.Find(JobCatalogue.Taxi);

        private static JobDef Courier => JobCatalogue.Find(JobCatalogue.Courier);
    }
}
