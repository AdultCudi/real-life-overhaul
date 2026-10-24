using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Portable price tolerance terms from the production city catalogue.</summary>
    public static class CityCompanion
    {
        public const string PriceBasis = "item-field-before-market-index-and-multiplier";

        /// <summary>Captured registered fields, before the game's later import index and market multiplier.</summary>
        public static Dictionary<string, object> PriceRow(string key, float own, float wholesale, float market,
            int kind, int factor, double exchange)
        {
            bool material = kind >= 0 && exchange > 0 && !double.IsInfinity(exchange);
            return CompanionSnapshot.Row("itemId", key, "ownAmount", CompanionValues.Decimal(own),
                "effectiveAmount", CompanionValues.Decimal(wholesale), "kind", kind,
                "factorThousandths", factor, "exchangeFactor", material ? CompanionValues.Decimal(exchange) : null,
                "provenance", kind < 0 ? "registered-current" : material ? "material-exchange" : "city-kind",
                "defaultMarketAmount", CompanionValues.Decimal(market));
        }

        /// <summary>The price tolerance terms from the city's catalogue.</summary>
        public static Dictionary<string, object> PriceTerms()
        {
            return CompanionSnapshot.Row("bearsMinThousandths", MacroCatalogue.BearsLeast, "bearsMaxThousandths", MacroCatalogue.BearsMost,
                "monopolyAllowanceThousandths", MacroCatalogue.MonopolyAllowance,
                "overBandThousandths", MacroCatalogue.OverBand, "underGainPercent", MacroCatalogue.UnderGain,
                "overLossPerPercent", MacroCatalogue.OverLossPerPercent, "overLossMaxPercent", MacroCatalogue.OverLossMost,
                "footfallMinPercent", MacroCatalogue.FootfallLeast, "footfallMaxPercent", MacroCatalogue.FootfallMost,
                "footfallQuietPercent", MacroCatalogue.FootfallQuiet, "ratioRounding", "float32-then-mean-to-even");
        }
    }
}
