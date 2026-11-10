using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The words of the showroom's suppliers that are built from the tables: a supplier's name, a kind of car, a price against the wholesaler's, a delivery.</summary>
    public static class CarSupplyText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Name(int supplier)
        {
            return DealerText.T(CarSupplyCatalogue.NameKeys[DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1)]);
        }

        public static string About(int supplier)
        {
            return DealerText.T(CarSupplyCatalogue.AboutKeys[DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1)]);
        }

        public static string Kind(int kind)
        {
            return DealerText.T(CarSupplyCatalogue.KindKeys[DealerCatalogue.Clamp(kind, 0, CarSupplyCatalogue.Kinds - 1)]);
        }

        /// <summary>A supplier's price for a kind against the wholesaler's: "3.5% under the wholesaler", "1.2% over the wholesaler" or "the wholesaler's price".</summary>
        public static string Against(int supplier, int kind)
        {
            int share = CarSupplyCatalogue.Price[DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1)][DealerCatalogue.Clamp(kind, 0, CarSupplyCatalogue.Kinds - 1)];
            if (share == 1000)
            {
                return DealerText.T("reallife_carsup_price_same");
            }
            string percent = (System.Math.Abs(share - 1000) / 10f).ToString("0.#", Inv);
            return DealerText.T(share < 1000 ? "reallife_carsup_price_under" : "reallife_carsup_price_over", "percent", percent);
        }

        /// <summary>When a supplier's car comes: at once, or in so many days.</summary>
        public static string Delivery(int supplier)
        {
            int days = CarSupplyRules.Days(supplier);
            return days <= 0 ? DealerText.T("reallife_carsup_delivery_now") : DealerText.T("reallife_carsup_delivery_days", "days", days.ToString(Inv));
        }

        /// <summary>A supplier's terms for a kind on one line: its price against the wholesaler's and its delivery.</summary>
        public static string Terms(int supplier, int kind)
        {
            return DealerText.Fill("reallife_carsup_terms", "price", Against(supplier, kind), "delivery", Delivery(supplier));
        }

        /// <summary>Which supplier each of the other dealers buys a kind from, as one line; the same all game.</summary>
        public static string Rivals(int seed, int kind)
        {
            var t = new System.Text.StringBuilder(160);
            for (int s = 0; s < CarSupplyCatalogue.Suppliers; s++)
            {
                int count = 0;
                for (int r = 0; r < DealerCatalogue.RivalNames.Length; r++)
                {
                    count += CarSupplyRules.RivalSupplier(seed, r, kind) == s ? 1 : 0;
                }
                if (count > 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(DealerText.Fill("reallife_carsup_rivals_part", "supplier", Name(s), "count", count.ToString(Inv)));
                }
            }
            return DealerText.T("reallife_carsup_rivals", "list", t.ToString());
        }
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
