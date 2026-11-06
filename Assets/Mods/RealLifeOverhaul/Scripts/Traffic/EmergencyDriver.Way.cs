using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The emergency driver: the ways past what holds a lit unit up (one lane over, then two, MakeWayRules), what is said once in the
    /// log of a hold and of how it ended, and whether a lit unit is coming up behind a vehicle (a bus standing in the lane makes way).
    /// Cost: the ways past are looked at once a second while held up, at most four; the log is written once a hold; UnitComingUp
    /// reads sixteen fields.
    /// </summary>
    public sealed partial class EmergencyDriver : MonoBehaviour
    {
        private Collider _aheadHit;     // what was ahead at the last look, if it has a collider
        private bool _wayTold;          // a hold has been written to the log, and its end is still to be
        private float _wayPassed;       // the lane (metres right of its line) it went round by since, or 0

        private void ResetWay()
        {
            _aheadHit = null;
            _wayTold = false;
            _wayPassed = 0f;
        }

        // The ways past in turn, nearest first: a lane left, a lane right and, held up a while (a lit unit only), two lanes over.
        private bool TryPass(Vector3 middle, Vector3 forward, Vector3 roadRight)
        {
            float held = _blocked ? Time.time - _blockedSince : 0f;
            int choices = MakeWayRules.Choices(Lit, held);
            float snap = MakeWayRules.LaneSnap(LaneSnap, Lit, held);
            for (int choice = 0; choice < choices; choice++)
            {
                float offset = MakeWayRules.Offset(choice, PassOffset);
                if (PassWhy(middle, forward, roadRight, offset, snap, choice >= 2) == null)
                {
                    _targetOffset = offset;
                    _wayPassed = offset;
                    return true;
                }
            }
            return false;
        }

        // A pass is only ever into a lane of the road: a road point where it would run, and nothing in it (nor, for a way two lanes
        // over, in the lane between). Null if it can; else why not, in words for the log.
        private string PassWhy(Vector3 middle, Vector3 forward, Vector3 roadRight, float offset, float snap, bool between)
        {
            Vector3 beside = middle + roadRight * offset;
            Vector3 lane = beside + forward * (_car.length * 0.5f + 6f) - Vector3.up * SenseHeight;
            if (IsBlocked(beside, forward))
            {
                return "something stands there";
            }
            if (between && IsBlocked(middle + roadRight * (offset * 0.5f), forward))
            {
                return "the lane between is taken";
            }
            if (_gridLanes && RoadMap.Usable)
            {
                // The squares of RoadMap's grid round the place: a handful of points, whatever the lane; the call below finds the nearest lane point.
                return RoadMap.NearestLane(lane, out float off, out _, out _) && off < snap ? null : "no lane there";
            }
            return TrafficManager.HasInstance && TrafficTools.ClosestWaypoint(lane, snap) != null ? null : "no lane there";
        }

        // After each look of a lit unit: a hold of MakeWayRules.TellAfter seconds is written once, with what holds it and why no way
        // past was taken, and so is its end, once it is moving again.
        private void TellWay(Vector3 position, Vector3 roadRight)
        {
            if (!Lit)
            {
                return;
            }
            float held = _blocked ? Time.time - _blockedSince : 0f;
            if (MakeWayRules.Tell(held, _wayTold))
            {
                _wayTold = true;
                _wayPassed = 0f;
                Vector3 forward = _body.rotation * (_reverse ? Vector3.back : Vector3.forward);
                Vector3 middle = position + Vector3.up * SenseHeight;
                float snap = MakeWayRules.LaneSnap(LaneSnap, true, held);
                string kept = !MayPass ? "it was told to keep its lane" : _pulledOver ? "it is pulled in to the kerb" : _reverse ? "it is backing"
                    : !HasRoomToPass(Flat(_goal - position).sqrMagnitude) ? "it is too near its place to pull out" : null;
                string ways = kept ?? "a lane left: " + (PassWhy(middle, forward, roadRight, -PassOffset, snap, false) ?? "open")
                    + "; a lane right: " + (PassWhy(middle, forward, roadRight, PassOffset, snap, false) ?? "open")
                    + "; two lanes left: " + (PassWhy(middle, forward, roadRight, -2f * PassOffset, snap, true) ?? "open")
                    + "; two lanes right: " + (PassWhy(middle, forward, roadRight, 2f * PassOffset, snap, true) ?? "open");
                string room = _room < float.MaxValue ? _room.ToString("F1", CultureInfo.InvariantCulture) : "?";
                ResponseUnit.Logger?.Info($"Emergency way: the {TrafficTools.Name(_car)} has stood {held:F0} s behind {Holder()}, {room} m ahead of its nose, "
                                          + $"with {RouteLeft:F0} m of its route left; ways past: {ways}.");
            }
            else if (_wayTold && !_blocked && _speed > 1f)
            {
                _wayTold = false;
                string how = _wayPassed != 0f
                    ? $"it went round by the lane {Mathf.Abs(_wayPassed):F1} m to its {(_wayPassed < 0f ? "left" : "right")}" : "what held it moved on";
                ResponseUnit.Logger?.Info($"Emergency way: the {TrafficTools.Name(_car)} is moving again, {RouteLeft:F0} m of its route left: {how}.");
            }
        }

        // What it is held up behind, in words for the log.
        private string Holder()
        {
            if (_aheadPerson)
            {
                return "somebody on foot";
            }
            Rigidbody body = _ahead;
            if (body != null)
            {
                if (body.GetComponent<BusVehicle>() != null)
                {
                    return "a bus of the mod's own";
                }
                VehicleComponent car = body.GetComponent<VehicleComponent>();
                if (car != null)
                {
                    float speed = Flat(body.velocity).magnitude;
                    string pace = speed < 0.5f ? "standing" : (speed * 3.6f).ToString("F0") + " km/h";
                    return "'" + TrafficTools.Name(car) + "' of the traffic (" + pace + ", its driver: " + car.GetCurrentAction() + ")";
                }
            }
            Collider hit = _aheadHit;
            return hit != null ? "'" + hit.name + "' on " + LayerMask.LayerToName(hit.gameObject.layer) : "something it could not name";
        }

        /// <summary>
        /// True if a unit driving with its lights on is coming up on a vehicle: behind it, within MakeWayRules.ComesUpWithin, in its lane
        /// or the next, heading its way. For a vehicle of the mod's own that stands in the lane (the bus at a stop with no bay) to stand
        /// clear. Sixteen field reads; asked only while such a vehicle stands or is held up.
        /// </summary>
        public static bool UnitComingUp(Vector3 position, Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < Driving.Length; i++)
            {
                EmergencyDriver driver = LitAt(i);
                if (driver == null || driver._body == null || driver._reverse)
                {
                    continue;
                }
                Vector3 off = Flat(driver._body.position - position);
                Vector3 heading = Flat(driver._body.rotation * Vector3.forward);
                float facing = heading.sqrMagnitude > 0.01f ? Vector3.Dot(heading.normalized, forward) : 0f;
                if (MakeWayRules.UnitComesUp(-Vector3.Dot(off, forward), Vector3.Dot(off, right), facing))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
