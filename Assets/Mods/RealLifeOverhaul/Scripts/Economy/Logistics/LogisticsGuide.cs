using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Delivery trips' and food spoilage's entry in the mod's feature list: their two options, their save lines, their two tests
    /// and the Guide page's figures, each {name} filled from the rules when the page opens. Called once when the list is built.
    /// </summary>
    public static class LogisticsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("trips", "reallife_meta_title_trips")
                .About("reallife_guide_trips_about").Help("reallife_guide_trips")
                .Owns(Settings.LivingTripsId, Settings.LivingSpoilageId, Settings.LivingSpoilagePaceId)
                .StandsAside(GoingPublic.SpoilPaceSentence, Settings.LivingSpoilagePaceId)   // every machine of a session spoils at the usual pace
                .Data(TripStore.Key, SpoilStore.Key, SendNowLimit.Key)
                .Test("reallife_trips_test", "reallife_spoil_test")
                .Fact("trips_load", () => Whole(TripRules.LoadMinutes))
                .Fact("trips_unload", () => Whole(TripRules.UnloadMinutes))
                .Fact("trips_van_kmh", () => Whole(TripRules.Kmh(TripVehicle.Van)))
                .Fact("trips_truck_kmh", () => Whole(TripRules.Kmh(TripVehicle.Truck)))
                .Fact("trips_van_litres", () => Tenths(TripRules.Litres(TripVehicle.Van)))
                .Fact("trips_truck_litres", () => Tenths(TripRules.Litres(TripVehicle.Truck)))
                .Fact("trips_log_days", () => Whole(TripRules.LogDays))
                .Fact("spoil_produce", () => Whole(SpoilRules.UsualLife(SpoilClass.Produce, false)))
                .Fact("spoil_produce_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Produce, true)))
                .Fact("spoil_dairy", () => Whole(SpoilRules.UsualLife(SpoilClass.Dairy, false)))
                .Fact("spoil_dairy_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Dairy, true)))
                .Fact("spoil_bakery", () => Whole(SpoilRules.UsualLife(SpoilClass.Bakery, false)))
                .Fact("spoil_bakery_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Bakery, true)))
                .Fact("spoil_meat", () => Whole(SpoilRules.UsualLife(SpoilClass.Meat, false)))
                .Fact("spoil_meat_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Meat, true)))
                .Fact("spoil_prepared", () => Whole(SpoilRules.UsualLife(SpoilClass.Prepared, false)))
                .Fact("spoil_prepared_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Prepared, true)))
                .Fact("spoil_frozen", () => Whole(SpoilRules.UsualLife(SpoilClass.Frozen, false)))
                .Fact("spoil_frozen_cold", () => Whole(SpoilRules.UsualLife(SpoilClass.Frozen, true)))
                .Fact("spoil_short", () => Whole(SpoilRules.ShortLife));
        }

        private static string Whole(int n) => n.ToString(Inv);

        private static string Tenths(int tenths) => (tenths / 10).ToString(Inv) + "." + (tenths % 10).ToString(Inv);
    }
}
