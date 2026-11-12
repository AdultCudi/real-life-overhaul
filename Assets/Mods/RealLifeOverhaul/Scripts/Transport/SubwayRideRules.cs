using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game-free rules of the subway journey's record of a ride (<see cref="SubwayJourney"/>): where a ride began and how many stops it
    /// counts. The game's ride forgets its origin in the frame it begins (SubwaySystem.TravelToCoroutine sets its riding flag and shuts the
    /// map in one call, and the map's close sets lastSubwayStation to null), so the origin is found from the point the ride's camera
    /// starts at (SubwaySystem.CurrentPosition, the origin's own position) or from the station seen while the map was open. COST: a loop
    /// over the stations once a ride begins.
    /// </summary>
    public static class SubwayRideRules
    {
        public const float OriginReach = 40f;      // m from a ride's start within which a station is taken for its origin: the camera has flown a few metres by the time it is seen
        public const float MetresPerStop = 650f;   // m between stations, for counting the stops

        /// <summary>
        /// The index of the station a ride began at: the one seen while the map was open (<paramref name="known"/>, or -1) while the ride's start is
        /// within <see cref="OriginReach"/> of it, else the nearest station within that reach, else -1. A position that is not a number (a station
        /// gone) is never chosen.
        /// </summary>
        public static int Origin(float startX, float startZ, IReadOnlyList<float> x, IReadOnlyList<float> z, int known)
        {
            float reach = OriginReach * OriginReach;
            if (x != null && z != null && known >= 0 && known < x.Count && known < z.Count && Near(startX, startZ, x[known], z[known]) <= reach)
            {
                return known;
            }
            int best = -1;
            float bestAt = reach;
            for (int i = 0; x != null && z != null && i < x.Count && i < z.Count; i++)
            {
                float sqr = Near(startX, startZ, x[i], z[i]);
                if (sqr <= bestAt)
                {
                    bestAt = sqr;
                    best = i;
                }
            }
            return best;
        }

        // The squared distance on the ground; infinity for a position that is not a number.
        private static float Near(float ax, float az, float bx, float bz)
        {
            if (float.IsNaN(bx) || float.IsNaN(bz) || float.IsNaN(ax) || float.IsNaN(az))
            {
                return float.PositiveInfinity;
            }
            float dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz;
        }

        /// <summary>The stops a ride counts: one for every <see cref="MetresPerStop"/> of the straight line between its stations, and one at the least.</summary>
        public static int Stops(float metres)
        {
            return Math.Max(1, (int)Math.Round(metres / MetresPerStop));
        }
    }
}
