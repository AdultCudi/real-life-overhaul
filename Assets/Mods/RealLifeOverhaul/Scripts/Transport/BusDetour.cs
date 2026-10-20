using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>The lanes of the road as a bus going round something sees them: game-free, so NavCheck drives it over made-up roads.</summary>
    public interface IBusLanes
    {
        /// <summary>True for a lane point a bus may drive over: one of the road, not closed by a scene or the works.</summary>
        bool Open(int point);

        float X(int point);
        float Z(int point);

        /// <summary>The lane points that follow a point along its lane (the traffic system's neighbours).</summary>
        int NextCount(int point);
        int Next(int point, int k);

        /// <summary>The lane points beside a point running the same way (the traffic system's other lanes).</summary>
        int SideCount(int point);
        int Side(int point, int k);
    }

    /// <summary>
    /// The way round what stops a bus for good on its lane, as the city's traffic goes round it (a lane closed for the works or
    /// by an accident's cordon, a car standing still in the lane, somebody standing in the road): over to a lane beside it
    /// running the same way, along that lane past the place, and back onto its own route beyond it. Before this the bus stood
    /// behind any of them for as long as they stayed, in view for good (a player's report, 20 Oct 2026: "I see the bus but it
    /// is stuck in place and not moving"). Game-free; asked once a hold, never per frame: at most a few lanes beside the route
    /// walked for <see cref="MostPoints"/> points each, nothing allocated but what the caller's list holds.
    /// </summary>
    public static class BusDetour
    {
        public const int MostPoints = 40;          // lane points walked along a lane beside it, at the most
        public const float MostMetres = 220f;      // metres walked along it, at the most
        public const float RejoinWithin = 4.5f;    // metres between a point of the lane beside it and one of its route that are side by side
        public const float Lead = 9f;              // metres ahead of its front axle the way over begins: a lane change, not a turn on the spot
        public const int StartsBack = 3;           // points of its route before the blocked place from which a lane beside it is looked for
        public const float Straight = 0.3f;        // the least cosine between a step and the way the road runs: never back nor round a corner

        /// <summary>
        /// The way round a blocked stretch of a route: <paramref name="route"/> holds the lane point of each place of the route
        /// (-1 for a place of the bus's own), the stretch from <paramref name="blockFrom"/> to <paramref name="blockTo"/> is
        /// where it cannot go, no place before <paramref name="earliest"/> is left from (it is behind the bus), and the front axle
        /// is at (<paramref name="frontX"/>, <paramref name="frontZ"/>). On true,
        /// <paramref name="way"/> holds the lane points to drive in place of the route from <paramref name="leave"/> + 1, and the
        /// route goes on from <paramref name="rejoin"/> (beyond the blocked stretch). False when no lane beside it gets past.
        /// </summary>
        public static bool Find(IBusLanes lanes, IList<int> route, int earliest, int blockFrom, int blockTo, float frontX, float frontZ, List<int> way, out int leave, out int rejoin)
        {
            way.Clear();
            leave = -1;
            rejoin = -1;
            if (lanes == null || route == null || blockFrom < 0 || blockTo < blockFrom || blockTo >= route.Count)
            {
                return false;
            }
            for (int back = 1; back <= StartsBack; back++)
            {
                int from = blockFrom - back;
                if (from < Math.Max(0, earliest))
                {
                    break;
                }
                int point = route[from];
                if (point < 0 || !Heading(lanes, route, from, out float hx, out float hz))
                {
                    continue;
                }
                for (int k = 0; k < lanes.SideCount(point); k++)
                {
                    int side = lanes.Side(point, k);
                    if (side >= 0 && lanes.Open(side) && !OnRoute(route, side) && Walk(lanes, route, side, blockTo, hx, hz, frontX, frontZ, way, out rejoin))
                    {
                        leave = from;
                        return true;
                    }
                }
            }
            way.Clear();
            return false;
        }

        // Along the lane beside it from a point until a point of it is side by side with an open point of the route beyond the
        // blocked stretch; the straightest open lane point each step. The points nearer the front axle than Lead are left out.
        private static bool Walk(IBusLanes lanes, IList<int> route, int start, int blockTo, float hx, float hz, float frontX, float frontZ, List<int> way, out int rejoin)
        {
            way.Clear();
            rejoin = -1;
            int at = start;
            float metres = 0f;
            for (int step = 0; step < MostPoints; step++)
            {
                if ((lanes.X(at) - frontX) * hx + (lanes.Z(at) - frontZ) * hz >= Lead || way.Count > 0)
                {
                    way.Add(at);
                }
                int j = Beside(lanes, route, at, blockTo);
                if (j >= 0 && way.Count > 0)
                {
                    rejoin = j;
                    return true;
                }
                int best = -1;
                float straightest = Straight;
                for (int k = 0; k < lanes.NextCount(at); k++)
                {
                    int next = lanes.Next(at, k);
                    if (next < 0 || !lanes.Open(next) || OnRoute(route, next))
                    {
                        continue;
                    }
                    float dx = lanes.X(next) - lanes.X(at), dz = lanes.Z(next) - lanes.Z(at);
                    float length = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (length < 0.01f)
                    {
                        continue;
                    }
                    float cos = (dx * hx + dz * hz) / length;
                    if (cos > straightest)
                    {
                        straightest = cos;
                        best = next;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                float sx = lanes.X(best) - lanes.X(at), sz = lanes.Z(best) - lanes.Z(at);
                float leg = (float)Math.Sqrt(sx * sx + sz * sz);
                metres += leg;
                if (metres > MostMetres)
                {
                    break;
                }
                hx = sx / leg;
                hz = sz / leg;
                at = best;
            }
            way.Clear();
            return false;
        }

        // The first place of the route beyond the blocked stretch that a lane point is side by side with: one of its other lanes,
        // or within RejoinWithin, and open, as is the place after it. -1 for none.
        private static int Beside(IBusLanes lanes, IList<int> route, int at, int blockTo)
        {
            for (int j = blockTo + 1; j < route.Count; j++)
            {
                int point = route[j];
                if (point < 0 || !lanes.Open(point) || (j + 1 < route.Count && route[j + 1] >= 0 && !lanes.Open(route[j + 1])))
                {
                    continue;
                }
                bool side = false;
                for (int k = 0; k < lanes.SideCount(at) && !side; k++)
                {
                    side = lanes.Side(at, k) == point;
                }
                float dx = lanes.X(point) - lanes.X(at), dz = lanes.Z(point) - lanes.Z(at);
                if (side || dx * dx + dz * dz < RejoinWithin * RejoinWithin)
                {
                    return j;
                }
            }
            return -1;
        }

        // The way the road runs at a place of the route: to the next lane point of it, else from the one before.
        private static bool Heading(IBusLanes lanes, IList<int> route, int from, out float hx, out float hz)
        {
            hx = hz = 0f;
            int a = route[from], b = -1;
            for (int j = from + 1; j < route.Count && b < 0; j++)
            {
                b = route[j];
            }
            if (b < 0)
            {
                return false;
            }
            float dx = lanes.X(b) - lanes.X(a), dz = lanes.Z(b) - lanes.Z(a);
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.01f)
            {
                return false;
            }
            hx = dx / length;
            hz = dz / length;
            return true;
        }

        private static bool OnRoute(IList<int> route, int point)
        {
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i] == point)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
