using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What education and careers ask of the rest of the mod. Each is optional and has a default, so Scripts/Careers works
    /// on its own; Scripts/Wiring/CareersLinks.cs fills them in. Careers never calls another feature directly.
    /// </summary>
    public static class CareersBridges
    {
        /// <summary>True if a home of the player's has a working internet connection now (Utilities). Default: yes (nothing to ask).</summary>
        public static Func<bool> HomeInternet;

        /// <summary>The council's policy on education: +1 a tuition subsidy, -1 a surcharge, 0 as the city came (Civic). Default: 0.</summary>
        public static Func<int> Council;

        /// <summary>A driving refresher passed: penalty points to take off the licence; returns how many came off (Licences). Default: none.</summary>
        public static Func<int, int> Refresher;

        /// <summary>The player's standing moves: the part (0 customers, 1 workers, 2 neighbourhood, 3 integrity) and the points. Default: nobody is told.</summary>
        public static Action<int, int> Standing;

        /// <summary>The player's standing as a professional, -10 to 10 (Standing's recognition and integrity, as an employer reads a name). Default: 0.</summary>
        public static Func<int> Reputation;

        public static bool HomeOnline()
        {
            try
            {
                return HomeInternet == null || HomeInternet();
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static int CouncilNow()
        {
            try
            {
                return Council != null ? Math.Max(-1, Math.Min(1, Council())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int ReputationNow()
        {
            try
            {
                return Reputation != null ? Math.Max(-10, Math.Min(10, Reputation())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Reset()
        {
            HomeInternet = null;
            Council = null;
            Refresher = null;
            Standing = null;
            Reputation = null;
        }
    }
}
