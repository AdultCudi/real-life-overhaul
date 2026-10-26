using System;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What build mode asks of the rest of the mod (plans/build-mode.md), filled in by Wiring/Wiring.BuildModeLinks.cs and put
    /// back by <see cref="Reset"/>; each default answers as if the other feature were not there.
    /// </summary>
    public static class BuildModeBridges
    {
        /// <summary>A district's demand for a use (BuildUse; the district 0 Uptown, 1 Cedar Heights), 0 to 100, or -1 while not known. Default: not known.</summary>
        public static Func<int, int, int> Demand;

        /// <summary>A district's market level, thousandths of its rents and values. Default: even.</summary>
        public static Func<int, int> MarketLevel;

        /// <summary>The band's district a point on the ground (x, z) is in, 0 or 1; the map's middle looked at tells the demand bars which district they show. Default: 0.</summary>
        public static Func<float, float, int> DistrictAt;

        /// <summary>
        /// The ghost: one look of a design (by name and size) standing on ground (its middle, the way its street lies, its width and
        /// depth) as the district would stand that design there; its root, or null. Default: none.
        /// </summary>
        public static Func<string, string, float, float, float, float, float, Transform> GhostStand;

        /// <summary>The ghost taken away. Default: nothing.</summary>
        public static Action<Transform> GhostGone;

        /// <summary>The ghost of a corner building: its model at the street corner (x, z), turned (degrees); its root, or null. Default: none.</summary>
        public static Func<string, float, float, int, Transform> CornerGhost;

        /// <summary>A corner building's ghost taken away. Default: nothing.</summary>
        public static Action<Transform> CornerGhostGone;

        /// <summary>Test: every corner building standing checked now; how many failed, -1 for none standing. Default: -2, not asked.</summary>
        public static Func<int> CornerCheck;

        /// <summary>Test: the building finished on a lot checked now as a whole building of the game's. Default: not checked (false).</summary>
        public static Func<string, bool> CompleteNow;

        /// <summary>Test: every finished building checked now; how many failed. Default: -1, not checked.</summary>
        public static Func<int> CompleteAll;

        public static void Reset()
        {
            Demand = null;
            MarketLevel = null;
            DistrictAt = null;
            GhostStand = null;
            GhostGone = null;
            CornerGhost = null;
            CornerGhostGone = null;
            CornerCheck = null;
            CompleteNow = null;
            CompleteAll = null;
        }
    }
}
