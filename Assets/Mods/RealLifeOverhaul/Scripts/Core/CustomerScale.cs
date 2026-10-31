using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place the mod's own copies of the game's customer arithmetic ask how far the population moves a business's customers (the staff
    /// rota's forecast, the car dealership's and the bank branches' visitors, a brand's site estimate): a scale on the game's "initial customers"
    /// of a registration, 1 while nothing sets it (the living population off, or a business it has no figure for). Set by Wiring/PopulationLinks
    /// from the population's figures; the game's own count is moved by the same scale inside its calculators (Population/PopulationGame.Calculators).
    /// Main thread only. COST: a call and a dictionary look-up when asked, nothing per frame.
    /// </summary>
    public static class CustomerScale
    {
        /// <summary>The scale of a registration's customers; set by the population's wiring, null otherwise.</summary>
        public static Func<BuildingRegistration, float> Of;

        /// <summary>The scale of a district's and a business type's customers for a business not yet open (a site weighed for a franchise); null otherwise.</summary>
        public static Func<string, string, float> Site;

        /// <summary>A registration's scale; 1 with none.</summary>
        public static float For(BuildingRegistration reg)
        {
            try
            {
                float scale = Of != null && reg != null ? Of(reg) : 1f;
                return scale >= 0f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
            }
            catch (Exception)
            {
                return 1f;   // the game's own count stands
            }
        }

        /// <summary>A site's scale for a business type in a district; 1 with none.</summary>
        public static float ForSite(string district, string businessType)
        {
            try
            {
                float scale = Site != null ? Site(district, businessType) : 1f;
                return scale >= 0f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
            }
            catch (Exception)
            {
                return 1f;
            }
        }
    }
}
