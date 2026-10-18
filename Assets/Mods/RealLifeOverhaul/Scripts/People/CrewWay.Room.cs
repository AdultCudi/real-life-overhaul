using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Whether a way has room: the casualty, the tape and every pose tested, and the ground under it.</summary>
    public sealed partial class CrewWay
    {
        // The places in _x and _z as a way, tested against the casualty, the tape and, pose by pose, every vehicle and
        // fixture: on the carriageway out of the open lane (tier 0), or wherever there is ground (tier 1).
        private bool Valid(int tier, float guide, bool swing, Vector3 swingFrom, Vector3 swingHeading, Vector3 swingTo, Vector3 swingToHeading, bool home)
        {
            _tryOpen = false;
            if (!_try.Build(_x, _z, _count, Corner, Keep, guide))
            {
                _blocked = "no line through its places";
                return false;
            }
            float travel = _try.Travel;
            if (swing)
            {
                _try.AddSwing(swingFrom.x, swingFrom.z, swingHeading.x, swingHeading.z, swingTo.x, swingTo.z, swingToHeading.x, swingToHeading.z);
            }
            else if (home)
            {
                // The way back's own swings: out from beside the casualty, and (without room to turn) square at the doors.
                _try.Pose(0f, out _, out _, out float startX, out float startZ, out float startHeadingX, out float startHeadingZ);
                if (Swing)
                {
                    _try.AddSwing(Bed.x, Bed.z, HomeHeading.x, HomeHeading.z, startX, startZ, startHeadingX, startHeadingZ);
                }
                if (!Square)
                {
                    _try.Pose(travel, out _, out _, out float endX, out float endZ, out float endHeadingX, out float endHeadingZ);
                    _try.AddSwing(endX, endZ, endHeadingX, endHeadingZ, HomeBed.x, HomeBed.z, -Back.x, -Back.z);
                }
            }

            // Whoever lies there: on the way out nothing passes over their own line (on the way back they are on the bed).
            if (!home)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 part = Chest + Axis * (0.45f - i * 0.55f);
                    if (_try.Clearance(part.x, part.z) < BodyClear)
                    {
                        _blocked = "it would pass over the casualty";
                        return false;
                    }
                }
            }

            // The tape: crossed in one place a side at most, never run along; and the bed parked inside it.
            if (_framed)
            {
                float most = tier == 0 ? MostOpening : MostOpening * 1.8f;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = Edge(i);
                    Vector3 b = Edge(i + 1);
                    if (_try.Crossing(a.x, a.z, b.x, b.z, TapeClear, out float low, out float high) && (high - low) * Flat(b - a).magnitude > most)
                    {
                        _blocked = "it would run along the tape";
                        return false;
                    }
                }
                if (tier == 0 && !home && Inside(Chest, 0f) && !(Inside(Bed + Axis * CrewPath.BedHalfLength, 0.2f) && Inside(Bed - Axis * CrewPath.BedHalfLength, 0.2f)))
                {
                    _blocked = "the stretcher would stand across the tape";
                    return false;
                }
            }

            int mask = tier == 0 ? SceneParking.StreetMask : SceneParking.RoadsideMask;
            float half = Sweep + (tier == 0 ? 0.3f : 0.12f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.5f));
            // From the far end back: what is in the way is most often by the casualty, and the first thing found ends the way.
            for (int i = steps; i >= 0; i--)
            {
                float along = travel * i / steps;
                _try.Pose(along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ);
                bool atDoors = home ? along > travel - 2.5f : along < 2.5f;
                if (!PoseClear(new Vector3(pusherX, Door.y, pusherZ), new Vector3(bedX, Door.y, bedZ), new Vector3(headingX, 0f, headingZ), half, mask, atDoors, tier, i % 2 == 0))
                {
                    return false;
                }
            }
            int swings = (swing ? 1 : 0) + (home && Swing ? 1 : 0) + (home && !Square ? 1 : 0);
            for (int s = 0; s < swings; s++)
            {
                bool first = swing || (home && Swing && s == 0);
                Vector3 from, fromHeading, to, toHeading;
                if (swing)
                {
                    from = swingFrom;
                    fromHeading = swingHeading;
                    to = swingTo;
                    toHeading = swingToHeading;
                }
                else if (first)
                {
                    _try.Pose(0f, out _, out _, out float x, out float z, out float headingX, out float headingZ);
                    from = Bed;
                    fromHeading = HomeHeading;
                    to = new Vector3(x, Bed.y, z);
                    toHeading = new Vector3(headingX, 0f, headingZ);
                }
                else
                {
                    _try.Pose(travel, out _, out _, out float x, out float z, out float headingX, out float headingZ);
                    from = new Vector3(x, Door.y, z);
                    fromHeading = new Vector3(headingX, 0f, headingZ);
                    to = HomeBed;
                    toHeading = -Back;
                }
                for (int i = 0; i <= CrewPath.SwingPoses; i += 2)
                {
                    CrewPath.Blend(from.x, from.z, fromHeading.x, fromHeading.z, to.x, to.z, toHeading.x, toHeading.z, (float)i / CrewPath.SwingPoses,
                        out float bedX, out float bedZ, out float headingX, out float headingZ);
                    var heading = new Vector3(headingX, 0f, headingZ);
                    var bed = new Vector3(bedX, from.y, bedZ);
                    if (!PoseClear(bed - heading * Reach, bed, heading, half, mask, home && !first, tier, false))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>What stands on the way back when the crew sets off, half a minute after planning; null if it is still clear.</summary>
        public Collider HomeBlocked()
        {
            if (!Planned || !Home.Built)
            {
                return null;
            }
            float travel = Home.Travel;
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.6f));
            for (int i = 0; i <= steps; i++)
            {
                float along = travel * i / steps;
                Home.Pose(along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ);
                var heading = new Vector3(headingX, 0f, headingZ);
                var pusher = new Vector3(pusherX, Home.Height(along), pusherZ);
                var bed = new Vector3(bedX, pusher.y, bedZ);
                Collider hit = SceneParking.InStrip(pusher - heading * CrewPath.Body, bed + heading * (CrewPath.SecondAhead + CrewPath.Body), Sweep + 0.05f,
                    along > travel - 2.5f ? _ambulance : null, SceneParking.RoadsideMask);
                if (hit != null)
                {
                    return hit;
                }
            }
            return null;
        }

        // One pose: nothing solid in its strip, ground under it, and (tier 0) carriageway that is nobody's open lane.
        private bool PoseClear(Vector3 pusher, Vector3 bed, Vector3 heading, float half, int mask, bool atDoors, int tier, bool looked)
        {
            Collider hit = InStrip(pusher - heading * (CrewPath.Body + 0.05f), bed + heading * (CrewPath.SecondAhead + CrewPath.Body + 0.05f), half,
                atDoors ? _ambulance : null, mask);
            if (hit != null)
            {
                _blocked = Name(hit);
                return false;
            }
            if (!looked)
            {
                return true;
            }
            if (!HasGround(pusher) || !HasGround(bed))
            {
                _blocked = "no ground under it";
                return false;
            }
            bool open = _cordon != null && (_cordon.InOpenLane(bed, 0.2f) || _cordon.InOpenLane(pusher, 0.2f));
            if (tier > 0)
            {
                _tryOpen |= open;
                return true;
            }
            if (open)
            {
                _blocked = "the lane traffic is let past in";
                return false;
            }
            if (!OnRoad(pusher) || !OnRoad(bed))
            {
                _blocked = "a pavement or a kerb in the line";
                return false;
            }
            return true;
        }

        // The ground height along a way: asked every half metre, and in between as the road runs.
        private static void Level(CrewPath path)
        {
            int count = path.Samples;
            int last = 0;
            float lastHeight = 0f;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                if (i % 5 != 0 && i != count - 1)
                {
                    continue;
                }
                path.Sample(i, out float x, out float z);
                // From the last height found: the road may climb.
                if (!StreetSpots.Ground(new Vector3(x, any ? lastHeight : _startHeight, z), out float height))
                {
                    height = any ? lastHeight : _startHeight;
                }
                if (!any)
                {
                    lastHeight = height;
                    any = true;
                }
                for (int j = last; j <= i; j++)
                {
                    path.SetHeight(j, i == last ? height : Mathf.Lerp(lastHeight, height, (float)(j - last) / (i - last)));
                }
                last = i;
                lastHeight = height;
            }
        }

        private static float _startHeight;   // the height the ground is first looked for from: the rear doors' (set by Measure)

        private void Measure(Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
        {
            _startHeight = centre.y;
            DoorsToTape = -1f;
            Between = null;
            if (!_framed)
            {
                return;
            }
            float rear = Vector3.Dot(RearFace - _scene, _forward);
            float line = rear < _back ? _back : rear > _front ? _front : float.NaN;
            if (float.IsNaN(line))
            {
                return;
            }
            DoorsToTape = Mathf.Abs(line - rear);
            Vector3 nose = centre + forward * (halfLength + 0.3f);
            float ahead = Mathf.Abs(line - Vector3.Dot(nose - _scene, _forward));
            if (ahead > 0.6f && Vector3.Dot(forward, _forward) * (line - rear) > 0f)
            {
                Collider hit = SceneParking.InStrip(nose, nose + forward * (ahead - 0.3f), halfWidth, _ambulance, SceneParking.RoadsideMask);
                Between = hit != null ? Name(hit) : null;
            }
        }

        // How far the pusher has walked when the bed's middle crosses the tape into (or out of) the taped area; -1 if never.
        private float Gate(CrewPath path, bool entering)
        {
            if (!_framed)
            {
                return -1f;
            }
            float travel = path.Travel;
            bool was = false;
            for (float along = 0f; along <= travel; along += 0.25f)
            {
                path.Pose(along, out _, out _, out float x, out float z, out _, out _);
                bool inside = Inside(new Vector3(x, 0f, z), 0f);
                if (along > 0f && inside != was && inside == entering)
                {
                    return along;
                }
                was = inside;
            }
            return -1f;
        }

        private Vector3 Edge(int index)
        {
            index &= 3;
            return _scene + _forward * (index == 0 || index == 3 ? _back : _front) + _right * (index < 2 ? _left : _rightEdge);
        }

        private bool Inside(Vector3 point, float margin)
        {
            float along = Vector3.Dot(point - _scene, _forward);
            float across = Vector3.Dot(point - _scene, _right);
            return along > _back + margin && along < _front - margin && across > _left + margin && across < _rightEdge - margin;
        }

        private static Collider In(Vector3 place)
        {
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            int count = Physics.OverlapSphereNonAlloc(place + Vector3.up * 0.9f, 0.3f + Gap, Hits, SceneParking.StreetMask, QueryTriggerInteraction.Ignore);
            Collider found = count > 0 ? Hits[0] : null;
            for (int i = 0; i < count; i++)
            {
                Hits[i] = null;
            }
            return found;
        }

        private static readonly Collider[] Hits = new Collider[4];

        // True on the carriageway: street under the point, and not where only the game's pedestrians walk (a
        // pavement). A crossing is carriageway: their mesh runs over it, but so does a lane of the road.
        private static bool OnCarriage(Vector3 point)
        {
            if (!StreetSpots.Ground(point, out float ground))
            {
                return false;
            }
            point.y = ground;
            return !NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement) || InLane(point);
        }

        private const float LaneHalf = 1.9f;   // metres from a lane's middle line that are that lane

        private const float LaneReach = 6f;    // ...and from a place that a lane's nearest point is looked for

        // Within a lane's width across its nearest point's own line, so a pavement beside a kerb lane is not taken for road.
        private static bool InLane(Vector3 point)
        {
            return TrafficManager.HasInstance && InLane(point, TrafficTools.ClosestWaypoint(point, LaneReach));
        }

        private static bool InLane(Vector3 point, Waypoint nearest)
        {
            if (nearest == null || !TrafficManager.HasInstance)
            {
                return false;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint other = nearest.neighbors != null && nearest.neighbors.Count > 0 ? traffic.GetWaypoint(nearest.neighbors[0])
                : nearest.prev != null && nearest.prev.Count > 0 ? traffic.GetWaypoint(nearest.prev[0]) : null;
            Vector3 along = other != null ? Flat(other.position - nearest.position) : Vector3.zero;
            Vector3 offset = Flat(point - nearest.position);
            if (along.sqrMagnitude < 0.01f)
            {
                return offset.sqrMagnitude < LaneHalf * LaneHalf;
            }
            return Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, along.normalized))) < LaneHalf;
        }

        private static string Name(Collider hit)
        {
            return "'" + hit.name + "' on layer " + LayerMask.LayerToName(hit.gameObject.layer);
        }
    }
}
