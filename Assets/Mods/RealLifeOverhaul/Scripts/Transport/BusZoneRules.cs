using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The kerb of a bus stop that no car but the player's may stand in, numbers only (no Unity types, so EconomyCheck runs the
    /// arithmetic the game runs). Every place the buses stop at has one, bay or not: the strip between its lane's edge and the
    /// kerb, from <see cref="BusBay.CarMargin"/> short of the zone's first metre to as much beyond its last. A place with a bay
    /// has the bay's length (the bus comes in and pulls out over it); one without has the standing bus's length and the box the
    /// road is painted with round it. <see cref="Holds"/> asks whether a car's footprint reaches into one: the single question
    /// every path that stands a car at a kerb asks (SceneParking.Stands, BusZones.Holds). Cost: a few multiplications a zone,
    /// asked on events only.
    /// </summary>
    public static class BusZoneRules
    {
        public const float Margin = BusBay.CarMargin;            // metres of kerb kept clear beyond each end of a zone
        public const float Inner = BusBay.LaneHalf - 0.6f;       // metres aside of the lane's line the strip begins: a car in its own lane, mirrors and all, is not in it
        public const float KerbSlack = 0.3f;                     // metres beyond the kerb's edge the strip reaches: a wheel up on it
        public const float StandBehind = 3f;                     // the standing bus's box behind its tail (the painted box's: BusStopDressing.PaintBehind)
        public const float StandAhead = 2f;                      // and ahead of its nose (BusStopDressing.PaintAhead)
        public const float Height = 3f;                          // metres above or below the lane within which a car is in the zone: not on a bridge over it

        /// <summary>One zone, laid out once when the places are made; a flat struct, so the table of them is read with no allocation.</summary>
        public struct Zone
        {
            public float X, Y, Z;              // the stop's lane point
            public float AlongX, AlongZ;       // the way the lane runs there (flat, unit)
            public float MidX, MidZ;           // the middle of the strip, margins included
            public float HalfAlong, HalfAside; // half its length and half its width
            public float Reach;                // the circle round it, for the quick no

            /// <summary>The zone from <paramref name="from"/> to <paramref name="to"/> metres along the lane from the pole, kerb <paramref name="kerbAside"/> metres from its line.</summary>
            public static Zone Make(float x, float y, float z, float alongX, float alongZ, float from, float to, float kerbAside)
            {
                float length = (float)Math.Sqrt(alongX * alongX + alongZ * alongZ);
                if (length < 0.0001f)
                {
                    alongX = 0f;
                    alongZ = 1f;
                    length = 1f;
                }
                alongX /= length;
                alongZ /= length;
                float outer = Math.Max(Inner + 0.1f, kerbAside + KerbSlack);
                float alongMid = (from + to) * 0.5f;
                float asideMid = (Inner + outer) * 0.5f;
                var zone = new Zone { X = x, Y = y, Z = z, AlongX = alongX, AlongZ = alongZ };
                zone.MidX = x + alongX * alongMid + alongZ * asideMid;     // aside is to the right of the way: (z, -x)
                zone.MidZ = z + alongZ * alongMid - alongX * asideMid;
                zone.HalfAlong = (to - from) * 0.5f + Margin;
                zone.HalfAside = (outer - Inner) * 0.5f;
                zone.Reach = (float)Math.Sqrt(zone.HalfAlong * zone.HalfAlong + zone.HalfAside * zone.HalfAside);
                return zone;
            }
        }

        /// <summary>
        /// The length of kerb a place without a bay keeps: from <paramref name="from"/> to <paramref name="to"/> metres along from the pole,
        /// the bus standing with its middle <paramref name="stand"/> on from it.
        /// </summary>
        public static void Standing(float stand, out float from, out float to)
        {
            from = stand - BusBay.BusLength * 0.5f - StandBehind;
            to = stand + BusBay.BusLength * 0.5f + StandAhead;
        }

        /// <summary>
        /// Whether a vehicle's footprint (its middle, the way it points, half its length and half its width) reaches into the zone:
        /// two rectangles on the ground, apart if a line along a side of either has them on its two sides.
        /// </summary>
        public static bool Holds(in Zone zone, float x, float y, float z, float forwardX, float forwardZ, float halfLength, float halfWidth)
        {
            if (Math.Abs(y - zone.Y) > Height)
            {
                return false;
            }
            float dx = x - zone.MidX;
            float dz = z - zone.MidZ;
            float spread = (float)Math.Sqrt(halfLength * halfLength + halfWidth * halfWidth);
            if (dx * dx + dz * dz > (zone.Reach + spread) * (zone.Reach + spread))
            {
                return false;
            }
            float fl = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (fl < 0.0001f)
            {
                forwardX = 0f;
                forwardZ = 1f;
            }
            else
            {
                forwardX /= fl;
                forwardZ /= fl;
            }
            float ux = zone.AlongX, uz = zone.AlongZ;     // the zone's axes: along the lane, and aside (to the right)
            float rx = uz, rz = -ux;
            float gx = forwardZ, gz = -forwardX;          // the vehicle's: its way, and its side
            return Overlap(ux, uz, dx, dz, zone, forwardX, forwardZ, gx, gz, halfLength, halfWidth)
                   && Overlap(rx, rz, dx, dz, zone, forwardX, forwardZ, gx, gz, halfLength, halfWidth)
                   && Overlap(forwardX, forwardZ, dx, dz, zone, forwardX, forwardZ, gx, gz, halfLength, halfWidth)
                   && Overlap(gx, gz, dx, dz, zone, forwardX, forwardZ, gx, gz, halfLength, halfWidth);
        }

        // True if the two shadows on an axis overlap; shadows that only touch do not (a car that ends where the margin ends is clear of it).
        private static bool Overlap(float ax, float az, float dx, float dz, in Zone zone, float fx, float fz, float gx, float gz, float halfLength, float halfWidth)
        {
            float zoneShadow = zone.HalfAlong * Math.Abs(zone.AlongX * ax + zone.AlongZ * az) + zone.HalfAside * Math.Abs(zone.AlongZ * ax - zone.AlongX * az);
            float carShadow = halfLength * Math.Abs(fx * ax + fz * az) + halfWidth * Math.Abs(gx * ax + gz * az);
            return Math.Abs(dx * ax + dz * az) < zoneShadow + carShadow;
        }

        /// <summary>Whether a parked car's body between these metres along the lane is in the zone from <paramref name="from"/> to <paramref name="to"/>, margins counted.</summary>
        public static bool Along(float carStart, float carEnd, float from, float to)
        {
            return BusBay.CarInZone(carStart, carEnd, from, to, Margin);
        }
    }
}
