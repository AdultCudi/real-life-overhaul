using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The road the tow truck takes, looked up once on the lane points when it is sent: it stands ahead of the wreck in the
    /// wreck's own lane (nothing drives into it), reached by a lane beside running the same way (TowRules.Stretch), starting
    /// some 170 m back where nobody sees it. No such lane, a bend, a junction or a closed road: no truck.
    /// </summary>
    public static class TowRoads
    {
        private const float WreckReach = 9f;         // metres from a wreck's middle its lane point is looked for
        private const float BesideLeast = 2.4f;      // metres across to the middle of a lane beside it
        private const float BesideMost = 6.5f;       // metres
        private const float BehindLook = 12f;        // metres of the wreck's lane behind it whose lanes beside are looked at
        private const float Straight = 0.97f;        // the cosine of the most a lane may turn over the stretch (14 degrees)
        private const float WalkBack = 280f;         // metres of the lane beside walked back for a start
        private const float StartLeast = 70f;        // metres
        private const float StartPrefer = 170f;      // metres
        private const int StartTries = 24;
        private const float MergeStep = 3f;          // metres between the points laid for its move over
        private const float RoomMargin = 0.3f;       // metres all round the body that its start and its stand must be clear
        private const float PassMargin = 0.45f;      // metres clear between the wreck and the truck as it passes (its driver looks 0.2 wider)

        private static readonly List<Waypoint> Walk = new List<Waypoint>(220);
        private static readonly List<Waypoint> Lane = new List<Waypoint>(32);
        private static readonly List<Vector3> Laid = new List<Vector3>(240);
        private static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>What the truck is to do: the road from its start to its stand, laid for its front axle, and where that is.</summary>
        public sealed class Plan
        {
            public readonly List<Vector3> Route = new List<Vector3>(200);     // from the first point after its start to beyond its stand
            public readonly List<Waypoint> Back = new List<Waypoint>(220);    // the lane beside, from far back up to where it moves over
            public readonly List<Vector3> Over = new List<Vector3>(16);       // ...then across into the wreck's lane and on to beyond its stand
            public Vector3 Start;            // its front axle at the start...
            public Vector3 StartAlong;       // ...facing this way
            public Vector3 Stand;            // its front axle where it stands
            public Vector3 Along;            // the way the wreck's lane runs (flat, unit)
            public Vector3 Across;           // ...and across it, towards the lane beside
            public Waypoint Onward;          // a point of the wreck's lane ahead of where it stands: it drives off from here
            public float Metres;             // of road from its start to its stand
            public int StartIndex;           // the point of Back it starts at
            public string Refusal;           // why there is none, for the log
        }

        /// <summary>Where a truck can stand for this wreck, and the road there; false, with Plan.Refusal said, if there is none.</summary>
        public static bool Find(Vector3 centre, Vector3 forward, float halfLength, float halfWidth, Rigidbody wreck, Plan plan)
        {
            plan.Route.Clear();
            plan.Back.Clear();
            plan.Over.Clear();
            plan.Onward = null;
            plan.Refusal = null;
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint own = TrafficTools.ClosestWaypoint(centre, WreckReach);
            if (own == null || !Usable(own) || own.neighbors == null || own.neighbors.Count == 0)
            {
                plan.Refusal = "the wreck is on no plain lane";
                return false;
            }
            Waypoint next = traffic.GetWaypoint(own.neighbors[0]);
            Vector3 along = Flat(next != null ? next.position - own.position : forward);
            if (along.sqrMagnitude < 0.01f)
            {
                plan.Refusal = "the wreck's lane has no direction";
                return false;
            }
            along.Normalize();
            Vector3 origin = own.position;

            // The lane beside: from the points of the wreck's lane round it, one that runs the same way, a lane's width across.
            Waypoint beside = null;
            float across = 0f;
            Waypoint walk = own;
            for (int back = 0; back < 6 && walk != null && beside == null; back++)
            {
                beside = Beside(traffic, walk, origin, along, out across);
                walk = walk.prev != null && walk.prev.Count > 0 && Vector3.Dot(origin - walk.position, along) < BehindLook ? traffic.GetWaypoint(walk.prev[0]) : null;
            }
            if (beside == null)
            {
                plan.Refusal = "no lane beside the wreck's runs the same way";
                return false;
            }
            Vector3 side = Vector3.Cross(Vector3.up, along) * Mathf.Sign(across);   // towards the lane beside
            float sideWays = Mathf.Abs(across);

            // The wreck along its lane: its middle and its far end.
            float middle = Vector3.Dot(centre - origin, along);
            Vector3 flatForward = Flat(forward).normalized;
            float reach = halfLength * Mathf.Abs(Vector3.Dot(flatForward, along)) + halfWidth * Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up, flatForward), along));
            // A wreck standing so far askew that it reaches into the lane beside would hold the truck up behind it as it passed.
            float reachAcross = halfLength * Mathf.Abs(Vector3.Dot(flatForward, side)) + halfWidth * Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up, flatForward), side));
            float lateral = Vector3.Dot(centre - origin, side);
            if (sideWays - lateral - reachAcross < TowRules.HalfWidth + PassMargin)
            {
                plan.Refusal = "the wreck reaches " + (lateral + reachAcross).ToString("F1", CultureInfo.InvariantCulture) + " m towards the lane beside, "
                    + sideWays.ToString("F1", CultureInfo.InvariantCulture) + " m across: no room to pass it";
                return false;
            }
            TowRules.Stretch(middle, middle + reach, across, halfLength, out float mergeFrom, out float mergeTo, out float standAt);

            // Both lanes plain and straight over the stretch: the wreck's from behind it to beyond the stand, the one beside up to the move.
            if (!StraightLane(traffic, own, origin, along, standAt + 8f, Lane) || Lane.Count < 2)
            {
                plan.Refusal = "the wreck's lane is not plain and straight for " + (standAt + 8f).ToString("F0") + " m ahead";
                return false;
            }
            plan.Onward = Lane[Lane.Count - 1];
            if (!StraightLane(traffic, beside, origin, along, mergeFrom, Lane) || Lane.Count < 1)
            {
                plan.Refusal = "the lane beside is not plain and straight up to the wreck";
                return false;
            }
            Waypoint anchor = Lane[Lane.Count - 1];
            for (int i = Lane.Count - 1; i >= 0; i--)
            {
                if (Vector3.Dot(Lane[i].position - origin, along) <= mergeFrom)
                {
                    anchor = Lane[i];
                    break;
                }
            }

            // Its stand, and the road to the wreck behind it, clear of everything but the wreck.
            Vector3 stand = origin + along * standAt;
            stand.y = Height(stand, origin.y);
            float to = standAt + TowRules.Nose - TowRules.AxleFront;   // from the wreck's far end to the truck's nose
            var half = new Vector3(TowRules.HalfWidth + RoomMargin, 0.85f, (to - (middle + reach)) * 0.5f);
            Vector3 clearMiddle = origin + along * ((middle + reach + to) * 0.5f);
            clearMiddle.y = stand.y + 1.25f;
            Collider blocker = Blocking(clearMiddle, half, Quaternion.LookRotation(along), wreck);
            if (blocker != null)
            {
                plan.Refusal = "something stands where it would stand or lower its bed: " + blocker.name;
                return false;
            }

            // The lane beside, as it passes the wreck: nothing standing in it (another wreck, a vehicle held at the scene).
            float passFrom = middle - halfLength - 8f;
            Vector3 passMiddle = origin + along * ((passFrom + mergeFrom) * 0.5f) + side * sideWays;
            passMiddle.y = stand.y + 1.25f;
            var passHalf = new Vector3(TowRules.HalfWidth + 0.2f, 0.85f, (mergeFrom - passFrom) * 0.5f);
            Collider standing = Blocking(passMiddle, passHalf, Quaternion.LookRotation(along), wreck, stillOnly: true);
            if (standing != null)
            {
                plan.Refusal = "something stands in the lane beside where the truck would pass: " + standing.name;
                return false;
            }

            // The road there: the lane beside walked back, then laid across into the wreck's lane, then on to the stand.
            float walked = TrafficTools.WalkBack(anchor, WalkBack, 200, Walk);
            if (walked < StartLeast || Walk.Count < 3)
            {
                plan.Refusal = "the lane beside leads back only " + walked.ToString("F0") + " m";
                Walk.Clear();
                return false;
            }
            plan.Back.AddRange(Walk);
            Walk.Clear();
            plan.Along = along;
            plan.Across = side;
            plan.Stand = stand;
            float beginAcross = sideWays;
            int steps = Mathf.Max(2, Mathf.CeilToInt((mergeTo - mergeFrom) / MergeStep));
            for (int k = 1; k <= steps; k++)
            {
                float s = mergeFrom + (mergeTo - mergeFrom) * k / steps;
                float l = beginAcross * (1f - TowRules.Across(s, mergeFrom, mergeTo));
                Vector3 point = origin + along * s + side * l;
                point.y = Height(point, origin.y);
                plan.Over.Add(point);
            }
            plan.Over.Add(stand);
            plan.Over.Add(stand + along * BusPath.FinalShort);
            return true;
        }

        /// <summary>Picks the start on the lane beside as things are now (out of sight, away from the player, clear) and lays the route. False if none.</summary>
        public static bool PickStart(Plan plan)
        {
            int count = plan.Back.Count;
            if (count < 3)
            {
                return false;
            }
            Vector3 anchor = plan.Back[count - 1].position;
            int best = -1;
            // From the preferred distance outwards, nearer and further in turn.
            int prefer = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i < count - 2; i++)
            {
                float back = Mathf.Abs(Vector3.Dot(anchor - plan.Back[i].position, plan.Along));
                float gap = Mathf.Abs(back - StartPrefer);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    prefer = i;
                }
            }
            int tries = 0;
            for (int step = 0; step < count && tries < StartTries && best < 0; step++)
            {
                int i = prefer + ((step & 1) == 0 ? step / 2 : -(step + 1) / 2);
                if (i < 0 || i >= count - 2)
                {
                    continue;
                }
                float back = Vector3.Distance(anchor, plan.Back[i].position);
                if (back < StartLeast)
                {
                    continue;
                }
                tries++;
                if (CanStart(plan.Back[i], plan.Back[i + 1]))
                {
                    best = i;
                }
            }
            if (best < 0)
            {
                return false;
            }
            plan.StartIndex = best;
            plan.Start = plan.Back[best].position;
            plan.StartAlong = Flat(plan.Back[best + 1].position - plan.Start).normalized;
            // The route: the lane beside from the point after the start, then what Find laid across and on.
            List<Vector3> lane = Laid;
            lane.Clear();
            for (int i = best + 1; i < count; i++)
            {
                lane.Add(plan.Back[i].position);
            }
            lane.AddRange(plan.Over);
            float metres = 0f;
            Vector3 last = plan.Start;
            for (int i = 0; i < lane.Count; i++)
            {
                metres += Vector3.Distance(last, lane[i]);
                last = lane[i];
            }
            plan.Metres = metres;
            plan.Route.Clear();
            plan.Route.AddRange(lane);
            lane.Clear();
            return true;
        }

        /// <summary>The road on from where it stands: the wreck's lane ahead and wherever it leads, this far.</summary>
        public static bool Onward(Plan plan, Vector3 frontAxle, float metres, List<Vector3> route)
        {
            route.Clear();
            Waypoint from = plan.Onward;
            if (from == null)
            {
                from = TrafficTools.ClosestWaypointAlong(frontAxle + plan.Along * 8f, plan.Along, 20f);
            }
            if (from == null)
            {
                return false;
            }
            TrafficTools.WalkForward(from, metres, 200, Walk);
            for (int i = 0; i < Walk.Count; i++)
            {
                if (Vector3.Dot(Walk[i].position - frontAxle, plan.Along) > 2f || i > 0)
                {
                    route.Add(Walk[i].position);
                }
            }
            Waypoint end = Walk.Count > 0 ? Walk[Walk.Count - 1] : null;
            Walk.Clear();
            if (end != null)
            {
                plan.Onward = end;   // the next stretch, if this one ends with somebody still looking
            }
            return route.Count >= 2;
        }

        private static bool Usable(Waypoint point)
        {
            return point != null && RoadGraph.Drivable(point) && !point.temporaryDisabled;
        }

        // A point of a lane beside this one that runs the same way, a lane's width across, and its signed distance across.
        private static Waypoint Beside(TrafficManager traffic, Waypoint point, Vector3 origin, Vector3 along, out float across)
        {
            across = 0f;
            Waypoint found = null;
            float best = float.MaxValue;
            Vector3 right = Vector3.Cross(Vector3.up, along);
            for (int k = 0; point.otherLanes != null && k < point.otherLanes.Count; k++)
            {
                Waypoint other = traffic.GetWaypoint(point.otherLanes[k]);
                if (!Usable(other) || !RoadGraph.Plain(other) || other.neighbors == null || other.neighbors.Count == 0)
                {
                    continue;
                }
                Waypoint ahead = traffic.GetWaypoint(other.neighbors[0]);
                if (ahead == null || Vector3.Dot(Flat(ahead.position - other.position).normalized, along) < Straight)
                {
                    continue;
                }
                float sideways = Vector3.Dot(other.position - point.position, right);
                float size = Mathf.Abs(sideways);
                if (size >= BesideLeast && size <= BesideMost && size < best && Mathf.Abs(other.position.y - point.position.y) < 1.5f)
                {
                    best = size;
                    found = other;
                    across = sideways;
                }
            }
            return found;
        }

        // Walks a lane while plain, open and straight, up to upTo metres along, collecting its points; false if it ends, bends or forks first.
        private static bool StraightLane(TrafficManager traffic, Waypoint start, Vector3 origin, Vector3 along, float upTo, List<Waypoint> into)
        {
            into.Clear();
            Waypoint point = start;
            into.Add(point);
            for (int guard = 0; guard < 80; guard++)
            {
                if (Vector3.Dot(point.position - origin, along) >= upTo)
                {
                    return true;
                }
                if (point.neighbors == null || point.neighbors.Count == 0)
                {
                    return false;
                }
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                if (!Usable(next) || !RoadGraph.Plain(next) || Vector3.Dot(Flat(next.position - point.position).normalized, along) < Straight)
                {
                    return false;
                }
                into.Add(next);
                point = next;
            }
            return false;
        }

        // A start: a plain lane point, open, away from the player and out of sight, with room for the whole truck.
        private static bool CanStart(Waypoint point, Waypoint next)
        {
            if (!Usable(point) || !RoadGraph.Plain(point) || TransitSight.Near(point.position, TransitSight.NearKeep))
            {
                return false;
            }
            Vector3 along = Flat(next.position - point.position);
            if (along.sqrMagnitude < 0.01f)
            {
                return false;
            }
            along.Normalize();
            Vector3 middle = point.position + along * ((TowRules.Nose + TowRules.BedTail) * 0.5f - TowRules.AxleFront);
            if (TransitSight.Seen(middle, along, TowTruck.Length * 0.5f + 0.5f))
            {
                return false;
            }
            var half = new Vector3(TowRules.HalfWidth + RoomMargin, 0.85f, TowTruck.Length * 0.5f + RoomMargin);
            return Blocking(middle + Vector3.up * 1.25f, half, Quaternion.LookRotation(along), null) == null;
        }

        // What stands in this box, the wreck left out; stillOnly leaves out moving traffic too, gone by the time the truck comes.
        private static Collider Blocking(Vector3 middle, Vector3 half, Quaternion rotation, Rigidbody except, bool stillOnly = false)
        {
            int count = Physics.OverlapBoxNonAlloc(middle, half, Overlaps, rotation, SceneParking.RoadsideMask, QueryTriggerInteraction.Ignore);
            Collider found = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = Overlaps[i];
                Overlaps[i] = null;
                Rigidbody body = hit != null ? hit.attachedRigidbody : null;
                if (found == null && hit != null && (except == null || body != except) && (!stillOnly || body == null || body.isKinematic))
                {
                    found = hit;
                }
            }
            return found;
        }

        private static float Height(Vector3 point, float otherwise)
        {
            return CitySpots.Ground(point, out float height) ? height : otherwise;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
