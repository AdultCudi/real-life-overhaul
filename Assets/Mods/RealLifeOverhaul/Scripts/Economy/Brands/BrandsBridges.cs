using System;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// What brands and franchises ask of the rest of the mod, filled in by Wiring/BrandLinks. Each is optional with a
    /// default, so Economy/Brands works alone and never calls another feature directly.
    /// </summary>
    public static class BrandsBridges
    {
        /// <summary>Writes a business's promotion figure again as it should stand now (business pressure's writer). Default: nothing is written.</summary>
        public static CityBridges.FootfallWriter Footfall;

        /// <summary>True where the game keeps a promotion figure for a business, so that its footfall can be changed at all. Default: nowhere.</summary>
        public static Func<BuildingRegistration, bool> Promoted;

        /// <summary>The customers the city's economy takes from a business for its prices, per cent (0 or negative). Default: none.</summary>
        public static Func<BuildingRegistration, int> PriceLoss;

        /// <summary>True on the day the company's own strike starts at a business, by its place. Default: never.</summary>
        public static Func<string, int, bool> StrikeStarts;

        /// <summary>Files a franchisee's civil claim for a sum with the courts. Returns the court's number; 0 with no court to file with. Default: 0.</summary>
        public static Func<int, int> FileClaim;

        /// <summary>True while a claim with this number is before the court. Default: never.</summary>
        public static Func<int, bool> ClaimOpen;

        /// <summary>Withdraws a claim that has not been judged. Default: nothing happens.</summary>
        public static Func<int, bool> WithdrawClaim;

        /// <summary>How a scandal at a shop spreads, thousandths of its loss (Flare's account reputation). Default: 1000.</summary>
        public static Func<string, int> ScandalScale;

        /// <summary>Points a shop's interior counts in its standards (Scripts/Interiors). Default: nothing.</summary>
        public static Func<BuildingRegistration, int> Interior;

        /// <summary>A scandal has cost a brand: the shop's place, its name, the recognition lost (hundredths) and the scale it was lost at. Default: nobody is told.</summary>
        public static Action<string, string, int, int> ScandalHeard;

        public static void Reset()
        {
            Footfall = null;
            Promoted = null;
            PriceLoss = null;
            StrikeStarts = null;
            FileClaim = null;
            ClaimOpen = null;
            WithdrawClaim = null;
            ScandalScale = null;
            Interior = null;
            ScandalHeard = null;
        }
    }
}
