using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What Flare in the city asks of the living population, filled in by Wiring/Wiring.FlareCityLinks: the population in use here and its last
    /// day's pools. Unset (or a fault) reads as no population, and Flare runs as before. Asked once a second and on the day's work, on the main thread.
    /// </summary>
    public static partial class FlareBridges
    {
        /// <summary>The living population this machine runs (null while it does not: the option off, a city it does not know, a guest following the host).</summary>
        public static Func<PopulationState> Residents;

        /// <summary>The pools of its last day lived, or null.</summary>
        public static Func<PoolRules.DemandDay> Pools;

        /// <summary>Flare's desk session put into the sessions' registry (true, as the residents' side starts) or taken out (false, as it stops); Wiring.FlareSessionLinks.</summary>
        public static Action<bool> Desk;

        /// <summary>Flare in the city's bridges cleared (the wiring disconnects).</summary>
        public static void ResetCity()
        {
            Residents = null;
            Pools = null;
            CityMoves = null;
        }
    }
}
