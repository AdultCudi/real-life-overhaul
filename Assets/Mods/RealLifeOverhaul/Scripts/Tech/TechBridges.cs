using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the tech sector asks of the rest of the mod. Each is optional and has a default, so Scripts/Tech works on its
    /// own; Scripts/Wiring/TechLinks.cs fills them in. The tech sector never calls another feature directly.
    /// </summary>
    public static class TechBridges
    {
        /// <summary>The player's standing as an employer, -10 to 10 (Standing's workers). Default: 0.</summary>
        public static Func<int> Esteem;

        /// <summary>A rival's name to put to an approach for one of the player's people at a business. Default: one of the city's tech companies.</summary>
        public static Func<BuildingRegistration, string> Rival;

        /// <summary>Told when the player's standing as an employer should move: points, signed (a lay-off, a rise refused, a promotion). Default: nobody.</summary>
        public static Action<int> Standing;

        /// <summary>True while the company already has a rival's offer or a departure in hand for an employee (by the game's id): no second approach is made. Default: never.</summary>
        public static Func<string, bool> Held;

        public static int EsteemNow()
        {
            try
            {
                return Esteem != null ? Math.Max(-10, Math.Min(10, Esteem())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Reset()
        {
            Esteem = null;
            Rival = null;
            Standing = null;
            Held = null;
        }
    }
}
