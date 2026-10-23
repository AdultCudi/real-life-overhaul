using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Portable price tolerance terms from the production city catalogue.</summary>
    public static class CityCompanion
    {
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
