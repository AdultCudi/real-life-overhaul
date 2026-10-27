using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The mouth of a garage door on the district's streets, game-free (EconomyCheck's GateChecks): the ground a vehicle crosses
    /// from the road to a warehouse's door, which nothing may stand on, and the parts of a parking lane that are left when a mouth
    /// is cut out of it. A big special place keeps its whole front and its right side clear (BigPlacesRules); a warehouse's door
    /// keeps its own width and a car's swing either side of it, from the door over the pavement to a little past the kerb, the
    /// width of the game's parking lane, so a car or a truck turns in from the road as it does at the game's own warehouses.
    /// Every answer comes from the arguments alone. COST: nothing per frame; a few sums for each door at a load.
    /// </summary>
    public static class MouthRules
    {
        public const float Side = 2f;          // m a mouth reaches past each side of a door: a car swings in from the road, not square to the door
        public const float LaneMargin = 1f;    // m of a parking lane kept clear either side of a mouth
        public const float LaneLeast = 6f;     // m: a piece of a lane shorter than this parks no car (a spot is 5 m)
        public const float PoleReach = 0.5f;   // m: a lamp post's reach round its middle
        public const float StopReach = 6f;     // m: a bus stop's pole and its shelter along the kerb (the shelter 3.6 m long, 3.2 m ahead of the pole), either way

        /// <summary>
        /// The mouth of a garage door whose object stands at (x, z) looking out the way <paramref name="way"/> says (0 north, +Z; 1
        /// east, +X; 2 south; 3 west), the door <paramref name="width"/> metres wide: from the door out to a little past the kerb of
        /// the avenue (facing north or south) or the street (east or west) it fronts (<see cref="BigPlacesRules.MouthIntoRoad"/>), across
        /// the door and <see cref="Side"/> either side of it. Into the caller's list (not cleared).
        /// </summary>
        public static void Gate(float x, float z, int way, float width, List<GroundRect> into)
        {
            float half = Math.Max(0f, width) * 0.5f + Side;
            way = (way % 4 + 4) % 4;
            if (way == 0 || way == 2)
            {
                float road = LayoutRules.AvenueZ((int)Math.Round((z - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch));
                float far = way == 0 ? road - LayoutRules.AvenueKerb + BigPlacesRules.MouthIntoRoad : road + LayoutRules.AvenueKerb - BigPlacesRules.MouthIntoRoad;
                into.Add(new GroundRect(x - half, z, x + half, far));
                return;
            }
            float street = LayoutRules.StreetX((int)Math.Round((x - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch));
            float reach = way == 1 ? street - LayoutRules.StreetKerbWest + BigPlacesRules.MouthIntoRoad : street + LayoutRules.StreetKerbEast - BigPlacesRules.MouthIntoRoad;
            into.Add(new GroundRect(x, z - half, reach, z + half));
        }

        /// <summary>
        /// The pieces of a straight parking lane that stay when the mouths are cut out of it: the lane runs from <paramref name="start"/>
        /// to <paramref name="end"/> metres along the world's <c>x</c> (<paramref name="alongX"/>) or <c>z</c> axis, across the band
        /// between <paramref name="crossMin"/> and <paramref name="crossMax"/> of the other. Each mouth that meets the lane takes its
        /// own stretch of it and <see cref="LaneMargin"/> either side; a piece left shorter than <see cref="LaneLeast"/> is dropped.
        /// The pieces (their two ends along the axis) into <paramref name="from"/> and <paramref name="to"/> (cleared); how many.
        /// A lane no mouth meets is one piece, the whole lane.
        /// </summary>
        public static int Pieces(float start, float end, float crossMin, float crossMax, bool alongX, IList<GroundRect> mouths, List<float> from, List<float> to)
        {
            from.Clear();
            to.Clear();
            float low = Math.Min(start, end), high = Math.Max(start, end);
            var cutFrom = new List<float>(4);
            var cutTo = new List<float>(4);
            for (int i = 0; mouths != null && i < mouths.Count; i++)
            {
                GroundRect m = mouths[i];
                float c0 = alongX ? m.Z1 : m.X1, c1 = alongX ? m.Z2 : m.X2, a0 = (alongX ? m.X1 : m.Z1) - LaneMargin, a1 = (alongX ? m.X2 : m.Z2) + LaneMargin;
                if (c1 < crossMin || c0 > crossMax || a1 < low || a0 > high)
                {
                    continue;
                }
                int at = cutFrom.Count;
                while (at > 0 && cutFrom[at - 1] > a0)
                {
                    at--;
                }
                cutFrom.Insert(at, a0);
                cutTo.Insert(at, a1);
            }
            float cursor = low;
            for (int i = 0; i < cutFrom.Count; i++)
            {
                if (cutFrom[i] - cursor >= LaneLeast)
                {
                    from.Add(cursor);
                    to.Add(cutFrom[i]);
                }
                cursor = Math.Max(cursor, cutTo[i]);
            }
            if (high - cursor >= LaneLeast)
            {
                from.Add(cursor);
                to.Add(high);
            }
            return from.Count;
        }

        /// <summary>
        /// True when a garage door of a design, stood on <paramref name="ground"/> as the engine stands it, would have its mouth over the
        /// pole or the shelter of a bus stop (the poles' places in the world, <see cref="StreetsRules.StopPoles"/>): a stop is laid by the
        /// bus network where the routes want it, so the warehouse takes a look whose doors are elsewhere.
        /// </summary>
        public static bool TakesStop(DesignEntry d, MadeLot ground, IList<float> poleX, IList<float> poleZ)
        {
            if (d == null || d.Gates == null || poleX == null || poleZ == null)
            {
                return false;
            }
            NewBuildingsRules.Place(d, ground, out float x, out float z, out float yaw);
            var mouth = new List<GroundRect>(1);
            for (int i = 0; i < d.Gates.Length; i++)
            {
                GateRules.World(x, z, yaw, d.Gates[i].X, d.Gates[i].Z, out float doorX, out float doorZ);
                mouth.Clear();
                Gate(doorX, doorZ, GateRules.Quarter(yaw, d.Gates[i].Facing), d.Gates[i].Width, mouth);
                for (int k = 0; k < poleX.Count && k < poleZ.Count; k++)
                {
                    if (Holds(mouth, poleX[k], poleZ[k], StopReach))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True when a lane (its band and its two ends) is met by any of the mouths: it is not laid whole.</summary>
        public static bool Meets(float start, float end, float crossMin, float crossMax, bool alongX, IList<GroundRect> mouths)
        {
            float low = Math.Min(start, end), high = Math.Max(start, end);
            for (int i = 0; mouths != null && i < mouths.Count; i++)
            {
                GroundRect m = mouths[i];
                float c0 = alongX ? m.Z1 : m.X1, c1 = alongX ? m.Z2 : m.X2, a0 = (alongX ? m.X1 : m.Z1) - LaneMargin, a1 = (alongX ? m.X2 : m.Z2) + LaneMargin;
                if (!(c1 < crossMin || c0 > crossMax || a1 < low || a0 > high))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when a post or a piece of litter at (x, z) with the given reach stands in any of the mouths.</summary>
        public static bool Holds(IList<GroundRect> mouths, float x, float z, float reach)
        {
            return BigPlacesRules.Meets(mouths, x - reach, z - reach, x + reach, z + reach);
        }
    }
}
