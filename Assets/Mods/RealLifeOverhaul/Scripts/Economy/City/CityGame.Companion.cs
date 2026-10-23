using System;
using System.Collections.Generic;
using AI.Citizens;
using BigAmbitions.Items;

namespace RealLifeOverhaul.Economy
{
    internal static partial class CityGame
    {
        /// <summary>The owner's saved wholesale originals and live catalogue values; no price writes or catalogue scan.</summary>
        public static CompanionModule CompanionPrices(IReadOnlyDictionary<string, double> materials)
        {
            if (CityEconomyDirector.Instance != null && CityEconomyDirector.Instance.CompanionFailed)
            {
                return new CompanionModule("failed");
            }
            CityState state = CityEconomy.Live;
            if (state == null || Goods.Count == 0)
            {
                return new CompanionModule("unavailable");
            }
            var items = new List<object>();
            var keys = new List<string>(Goods.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                Good good = Goods[key];
                if (good.Item == null)
                {
                    continue;
                }
                float own = Own.TryGetValue(good.Item, out float original) ? original : good.Item.wholesalePrice;
                double exchange = materials != null && materials.TryGetValue(key, out double factor) ? factor : double.NaN;
                bool material = exchange > 0 && !double.IsInfinity(exchange);
                items.Add(CompanionSnapshot.Row("itemId", key, "ownAmount", CompanionValues.Decimal(own),
                    "effectiveAmount", CompanionValues.Decimal(good.Item.wholesalePrice), "kind", good.Kind,
                    "factorThousandths", good.Kind < 0 ? 1000 : CityModel.Wholesale(state, good.Kind),
                    "exchangeFactor", material ? CompanionValues.Decimal(exchange) : null,
                    "provenance", material ? "material-exchange" : "city-kind",
                    "defaultMarketAmount", CompanionValues.Decimal(good.Item.DefaultMarketPrice)));
            }
            var living = new List<string>(LivingTypes);
            living.Sort(StringComparer.Ordinal);
            string depositAdjustment = null;
            foreach (Transaction transaction in SaveGameManager.Current.Transactions)
            {
                if (transaction.transactionType != "ba:transaction_deposit" || transaction.amount >= 0)
                {
                    continue;
                }
                string adjustment = transaction.transactionCategories != null
                    && transaction.transactionCategories.Contains(RentCategory) ? "rentCostFactor" : "none";
                if (depositAdjustment != null && depositAdjustment != adjustment)
                {
                    depositAdjustment = null;
                    break;
                }
                depositAdjustment = adjustment;
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-prices-v1",
                "levelThousandths", Prices.Level, "quietBand", PriceRules.Quiet, "adjustmentQuiet", MacroCatalogue.AdjustQuiet,
                "costFactorThousandths", CityModel.Costs(state),
                "takingsFactorThousandths", CityModel.Takings(state, CityEconomy.Level), "items", items,
                "livingTransactionTypes", living, "livingExcludesWholesalePurchases", true,
                "adjustmentRounding", "aggregate-whole-away-from-zero", "statementAdjustment", "already-included",
                "depositAdjustment", depositAdjustment, "depositAdjustmentBasis", "observed-native-negative-transactions",
                "depositRefundAdjustment", "none", "carriedDebtInSiteForecast", false));
        }

        public static object CompanionTolerance()
        {
            CityState state = CityEconomy.Live;
            if (state == null || Goods.Count == 0)
            {
                return null;
            }
            var districts = new List<object>();
            foreach (CityDistrict district in state.Districts)
            {
                bool known = CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(district.Name, out float index);
                districts.Add(CompanionSnapshot.Row("districtId", district.Name,
                    "priceIndex", known ? CompanionValues.Decimal(index) : null,
                    "toleranceThousandths", CityModel.Tolerance(state, district, CityEconomy.Level),
                    "cyclePercent", CityModel.CycleFootfall(state.Cycle, CityEconomy.Level),
                    "districtPercent", CityModel.DistrictFootfall(district)));
            }
            var goods = new List<object>();
            var keys = new List<string>(Goods.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                goods.Add(CompanionSnapshot.Row("itemId", key, "marketAmount", CompanionValues.Decimal(Goods[key].Market)));
            }
            var monopolies = new List<object>();
            var pairs = new List<(string, string)>(MonopolyPairs);
            pairs.Sort((left, right) => string.CompareOrdinal(left.Item1 + "\n" + left.Item2, right.Item1 + "\n" + right.Item2));
            foreach ((string item, string district) pair in pairs)
            {
                monopolies.Add(CompanionSnapshot.Row("itemId", pair.item, "districtId", pair.district));
            }
            Dictionary<string, object> terms = CityCompanion.PriceTerms();
            terms["districts"] = districts;
            terms["items"] = goods;
            terms["monopolies"] = monopolies;
            return terms;
        }
    }
}
