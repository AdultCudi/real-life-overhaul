using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>A free stretch of kerb near a point, the way a car is put down at the roadside.</summary>
    public delegate bool KerbFinder(Vector3 near, out Vector3 spot, out Quaternion heading);

    /// <summary>A van hired for the day at the roadside near a point; null with the reason (a locale key) when none can be had.</summary>
    public delegate CarController VanHire(Vector3 near, out string reasonKey);

    /// <summary>What the Marketplace asks of the rest of the mod, each with a default so it works alone; Wiring/HustleLinks fills them in.</summary>
    public static class HustleBridges
    {
        /// <summary>Where a car bought is handed over. Default: nowhere, and a car cannot be collected.</summary>
        public static KerbFinder KerbSpot;

        /// <summary>A van from the jobs' van hire. Default: none to be had.</summary>
        public static VanHire HireVan;

        /// <summary>The hired van goes back once the player is out of it. Default: nothing.</summary>
        public static System.Action ReturnVan;

        /// <summary>True while one of the mod's jobs is on: work of the Marketplace waits for it. Default: never.</summary>
        public static System.Func<bool> OnShift;

        public static bool Busy => OnShift != null && OnShift();

        /// <summary>Back to the defaults, when the city goes.</summary>
        public static void Clear()
        {
            KerbSpot = null;
            HireVan = null;
            ReturnVan = null;
            OnShift = null;
        }
    }
}
