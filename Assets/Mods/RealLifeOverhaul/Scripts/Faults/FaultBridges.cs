using System;
using UnityEngine;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// What the city's faults ask of the rest of the mod (Wiring/FaultLinks fills each in). Each is optional: without one
    /// the faults still break and mend, and that part of what they do is simply not felt.
    /// </summary>
    public static class FaultBridges
    {
        /// <summary>One of the city's people reports a real fault on Flare (kind as FlareReport's number). True if posted.</summary>
        public static Func<int, string, string, Vector3, bool> CityReport;

        /// <summary>Street crime where the player stands is scaled by this (a dark lamp near, after dark).</summary>
        public static Action<float> CrimeScale;

        /// <summary>The player is in a district with litter: the dirty streets' mood is kept for so many hours.</summary>
        public static Action<int> LitterMood;

        /// <summary>A business of the player's near litter has its customers written again.</summary>
        public static Action<BuildingRegistration> Footfall;

        /// <summary>A passer-by within so many metres of a point trips, never fatally. True if somebody did.</summary>
        public static Func<Vector3, float, Vector3, bool> Trip;

        /// <summary>The cleanliness of a business (0 to 100) as the game keeps it; -1 if not known.</summary>
        public static Func<string, int> Cleanliness;

        public static void Reset()
        {
            CityReport = null;
            CrimeScale = null;
            LitterMood = null;
            Footfall = null;
            Trip = null;
            Cleanliness = null;
        }
    }
}
