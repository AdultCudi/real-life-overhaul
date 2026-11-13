using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a stand, a rack or a dock must not be because a vehicle drives or parks there, in plain numbers so that
    /// EconomyCheck checks them. A lane is a stretch of ground with a half width (a disc when its two ends are one
    /// point): the way into a repair bay, round a pump, round a car dealership's display spot or a garage door. A place
    /// is refused when any lane comes within its half width and the place's own radius. A forecourt is all walkable
    /// pavement, so the pavement test alone cannot tell a lane from a corner by the wall.
    /// </summary>
    public static class BikeLaneRules
    {
        public const float Reach = 14f;          // m a bay's or a pump's lane runs out from the trigger's end: a truck's length and the run-up to it
        public const float Verge = 0.9f;         // m either side of the trigger's width that a vehicle swings through
        public const float DiscRadius = 3.6f;    // m round a display spot, a scooter stand or a garage door
        public const float SquareRatio = 1.3f;   // a trigger less long than this times its width gets a lane along both axes
        public const float Margin = 0.5f;        // m of room kept beyond the place's footprint

        public struct Lane
        {
            public float AX, AZ, BX, BZ, Half;
        }

        /// <summary>A disc of this radius round a point.</summary>
        public static Lane Disc(float x, float z, float radius)
        {
            return new Lane { AX = x, AZ = z, BX = x, BZ = z, Half = radius };
        }

        /// <summary>
        /// The ways a vehicle drives to and from a trigger box (its middle and half sizes, world axes): along its longer side,
        /// out past each end, and both ways when it is nearly square. The box itself is inside the lane.
        /// </summary>
        public static void Approach(float cx, float cz, float halfX, float halfZ, List<Lane> into)
        {
            bool alongX = halfX >= halfZ;
            bool square = Math.Max(halfX, halfZ) < SquareRatio * Math.Min(halfX, halfZ);
            if (alongX || square)
            {
                into.Add(new Lane { AX = cx - halfX - Reach, AZ = cz, BX = cx + halfX + Reach, BZ = cz, Half = halfZ + Verge });
            }
            if (!alongX || square)
            {
                into.Add(new Lane { AX = cx, AZ = cz - halfZ - Reach, BX = cx, BZ = cz + halfZ + Reach, Half = halfX + Verge });
            }
        }

        /// <summary>The distance in metres from a point to the nearest point of the lane's middle line.</summary>
        public static float Distance(Lane lane, float x, float z)
        {
            float dx = lane.BX - lane.AX, dz = lane.BZ - lane.AZ;
            float length = dx * dx + dz * dz;
            float t = length < 1e-6f ? 0f : ((x - lane.AX) * dx + (z - lane.AZ) * dz) / length;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float px = lane.AX + dx * t - x, pz = lane.AZ + dz * t - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }

        /// <summary>The radius that holds a place of this half size, with the room kept round it.</summary>
        public static float Radius(float halfAcross, float halfAlong)
        {
            return (float)Math.Sqrt(halfAcross * halfAcross + halfAlong * halfAlong) + Margin;
        }

        /// <summary>True if a place of this radius at a point is in the way of any lane.</summary>
        public static bool InTheWay(IList<Lane> lanes, float x, float z, float radius)
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                if (Distance(lanes[i], x, z) < lanes[i].Half + radius)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
