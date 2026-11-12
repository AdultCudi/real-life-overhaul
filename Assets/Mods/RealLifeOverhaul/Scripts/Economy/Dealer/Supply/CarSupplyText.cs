using System.Globalization;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The words of the showroom's suppliers in the game's language: <see cref="Ui.SupplyWords"/>, which the kit's page and the checks use too, through the game's localisation.</summary>
    public static class CarSupplyText
    {
        /// <summary>The game's own text of a key; the English is only what the key stands for.</summary>
        public static readonly Ui.UiWords Words = (key, english) => key.GetLocalization();

        public static string Name(int supplier) => Ui.SupplyWords.Name(Words, supplier);

        public static string About(int supplier) => Ui.SupplyWords.About(Words, supplier);

        public static string Kind(int kind) => Ui.SupplyWords.Kind(Words, kind);

        public static string Delivery(int supplier) => Ui.SupplyWords.Delivery(Words, supplier);

        public static string Terms(int supplier, int kind) => Ui.SupplyWords.Terms(Words, supplier, kind);

        public static string Rivals(int seed, int kind) => Ui.SupplyWords.Rivals(Words, seed, kind);
    }

    /// <summary>
    /// The suppliers' entry in the feature list, a part of the Car Showroom with its own switch and Guide page; its page's numbers
    /// are facts filled from <see cref="CarSupplyCatalogue"/> when it opens.
    /// </summary>
    public static class CarSupplyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string Percent(int supplier, int kind)
        {
            return (System.Math.Abs(CarSupplyCatalogue.Price[supplier][kind] - 1000) / 10f).ToString("0.#", Inv) + "%";
        }

        public static void Register()
        {
            Features.Add("economy_dealer_supply", "reallife_meta_title_economy_dealer_supply")
                .About("reallife_guide_economy_dealer_supply_about").Help("reallife_guide_economy_dealer_supply")
                .Owns(Settings.EconomyDealerSupplyId)
                .Data(CarSupplyStore.Key)
                .Test("reallife_carsup_test_order", "reallife_carsup_test_manager_btn")
                .Fact("carsup_importer_days", () => CarSupplyRules.Days(CarSupplyCatalogue.Importer).ToString(Inv))
                .Fact("carsup_specialist_days", () => CarSupplyRules.Days(CarSupplyCatalogue.Specialist).ToString(Inv))
                .Fact("carsup_importer_economy", () => Percent(CarSupplyCatalogue.Importer, 0))
                .Fact("carsup_importer_saloon", () => Percent(CarSupplyCatalogue.Importer, 1))
                .Fact("carsup_importer_van", () => Percent(CarSupplyCatalogue.Importer, 5))
                .Fact("carsup_importer_dear", () => Percent(CarSupplyCatalogue.Importer, 3))
                .Fact("carsup_specialist_luxury", () => Percent(CarSupplyCatalogue.Specialist, 2))
                .Fact("carsup_specialist_sports", () => Percent(CarSupplyCatalogue.Specialist, 3))
                .Fact("carsup_specialist_utility", () => Percent(CarSupplyCatalogue.Specialist, 4))
                .Fact("carsup_specialist_dear", () => Percent(CarSupplyCatalogue.Specialist, 0))
                .Fact("carsup_keep_most", () => CarSupplyCatalogue.KeepMost.ToString(Inv))
                .Fact("carsup_hour", () => DealerCatalogue.DeliveryHour.ToString(Inv) + ":00")
                .Fact("carsup_ways_most", () => DealerCatalogue.DeliveriesMost.ToString(Inv))
                .ResetData(() => DealerDirector.Instance?.ForgetSupply());
        }
    }
}
