using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How many people the street has out in the weather. Game-free. COST: a multiplication, asked when a site is due to start.
    /// </summary>
    public static class StreetLifeRules
    {
        /// <summary>
        /// The sites the hour and the density call for, thinned by the weather's street life (1 in fair weather, less in rain, ice, snow and a
        /// heat wave or cold snap, 0 in a storm): at least one while the factor is above 0, so rain thins the street and never empties it.
        /// </summary>
        public static int Sites(int byDensity, float streetLife)
        {
            if (byDensity <= 0 || streetLife <= 0f)
            {
                return 0;
            }
            return streetLife >= 1f ? byDensity : Math.Max(1, (int)Math.Round(byDensity * streetLife, MidpointRounding.AwayFromZero));
        }
    }
}
