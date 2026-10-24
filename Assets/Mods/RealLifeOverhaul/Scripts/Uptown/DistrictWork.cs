using System;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What Uptown's offices' tests ask of the working life (Scripts/Careers), set by the wiring (Wiring/CareersLinks) while
    /// education and careers are connected: whether it employs at a business type, a job for the player at an address, and
    /// how many of the city's open posts are at places of Uptown. Unset: the offices' census says the working life is not
    /// there, and the job test says why not. Only the tests call them.
    /// COST: nothing per frame.
    /// </summary>
    public static class DistrictWork
    {
        /// <summary>True when the working life employs a profession at a business of that type (ProCatalogue.AnyEmploys).</summary>
        public static Func<string, bool> Employs;

        /// <summary>A job for the player now at the business at a street and number; null when hired, else the locale key of the notice saying why not.</summary>
        public static Func<string, int, string> HireAt;

        /// <summary>How many of the city's open posts today are at a place the function picks; the open posts in all after it.</summary>
        public delegate int OpeningsCount(Func<string, int, bool> at, out int all);

        /// <summary>The open posts at places the function picks, and in all (CareersDirector.OpeningsAt).</summary>
        public static OpeningsCount Openings;

        /// <summary>The bridges taken off (the wiring's disconnect).</summary>
        public static void Reset()
        {
            Employs = null;
            HireAt = null;
            Openings = null;
        }
    }
}
