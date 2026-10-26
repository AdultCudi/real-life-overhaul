using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The emergency driver: the look ahead, the lane to pass in, and the speed it may go for the bends, a held point and the stop.</summary>
    public sealed partial class EmergencyDriver : MonoBehaviour
    {
        private const float SenseDistance = 12f;    // metres
        private const float SenseMargin = 0.15f;    // metres the look-ahead is wider than the vehicle, each side
        private const float PersonGap = 2f;         // ...and this much more if it is a person
        private const float PassOffset = 3.3f;      // metres: one lane sideways
        private const float PassRoom = 30f;         // metres from where it will stop inside which it no longer pulls out to pass
        private const float LaneSnap = 1.7f;        // metres: a road point this close to where a pass would run makes it a lane
        private const float PassLookInterval = 1f;  // seconds between looks for a lane to pass in, while held up
        private const float FastGrip = 8.5f;        // m/s2 sideways through a bend
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        // The fastest it may go now for the bends ahead: what tyres hold through each, plus what it can lose before it. Reads only the route.
        private float BendLimit(Vector3 position)
        {
            if (_next < 1 || _next >= _path.Count)
            {
                return float.MaxValue;
            }
            float limit = float.MaxValue;
            // (A careless drive plans with the grip it was given and with no more braking than it has.)
            float grip = _bendGrip > 0f ? _bendGrip : FastGrip;
            float braking = _bendGrip > 0f ? Mathf.Min(FastBraking, _stopping) : FastBraking;
            float look = _speed * _speed / (2f * braking) + 25f;
            Vector3 from = _path[_next];
            Vector3 before = Flat(from - _path[_next - 1]);
            float lastLeg = before.magnitude;
            float travelled = Flat(from - position).magnitude;   // metres of route to the bend being looked at
            for (int i = _next + 1; i < _path.Count && travelled < look; i++)
            {
                Vector3 leg = Flat(_path[i] - from);
                float length = leg.magnitude;
                if (length < 1f)
                {
                    continue;   // (points on top of one another say nothing about the road)
                }
                if (lastLeg >= 1f)
                {
                    float bend = Vector3.Angle(before, leg) * Mathf.Deg2Rad;
                    if (bend > 0.01f)
                    {
                        float through = Mathf.Sqrt(grip * (lastLeg + length) * 0.5f / bend);
                        limit = Mathf.Min(limit, Mathf.Sqrt(through * through + 2f * braking * travelled));
                    }
                }
                travelled += length;
                lastLeg = length;
                before = leg;
                from = _path[i];
            }
            return limit;
        }

        private const float HoldShort = 3f;         // metres short of the point it holds at that its nose stops (the point is the stop line's own)

        // The fastest it may go for the point it holds at: an ordinary stop a little short of it; no limit once the point is behind it.
        private float HoldLimit(Vector3 toPoint)
        {
            if (_holdAt < _next || _holdAt >= _path.Count)
            {
                return float.MaxValue;
            }
            float along = toPoint.magnitude;
            for (int i = _next + 1; i <= _holdAt; i++)
            {
                along += Flat(_path[i] - _path[i - 1]).magnitude;
            }
            float room = along - _car.length * 0.5f - HoldShort;
            return room > 0.1f ? Mathf.Sqrt(2f * Mathf.Min(Braking, _stopping) * room) : 0f;
        }

        // Metres to where it will stop when that is close: its pull-up point, its route's end or the goal; a large number while far off.
        private float StopDistance(Vector3 position, Vector3 toPoint, float goalSqr, int last)
        {
            int stopIndex = _stopAt >= 0 ? _stopAt : last;
            float toStop = float.MaxValue;
            if (_next == stopIndex)
            {
                toStop = toPoint.magnitude;
            }
            else if (_next == stopIndex - 1)
            {
                toStop = toPoint.magnitude + Flat(_path[stopIndex] - _path[_next]).magnitude;
            }
            if (!_routeOnly && _stopAt < 0)
            {
                toStop = Mathf.Min(toStop, Mathf.Max(0f, Mathf.Sqrt(goalSqr) - _stopDistance));
            }
            return toStop;
        }

        // Looks ahead and picks a lane and a speed: carry on, move over to another lane, or close up and wait behind what is ahead.
        private void Sense(Vector3 position, Vector3 roadRight, float goalSqr)
        {
            Vector3 forward = _body.rotation * (_reverse ? Vector3.back : Vector3.forward);   // the way it is going
            Vector3 middle = position + Vector3.up * SenseHeight;

            float free = FreeAhead(middle, forward, beside: false, out Rigidbody blocker);
            _room = free;
            _ahead = blocker;
            if (free == float.MaxValue)
            {
                _blocked = false;
                _blocker = null;
                _limit = float.MaxValue;
                // Back into our own lane once it has been clear there for a moment (not when pulled in to the kerb).
                if (_targetOffset != 0f && !_pulledOver)
                {
                    if (IsBlocked(middle - roadRight * _offset, forward))
                    {
                        _clearSince = Time.time;
                    }
                    else if (Time.time - _clearSince > 1.2f)
                    {
                        _targetOffset = 0f;
                    }
                }
                return;
            }

            _clearSince = Time.time;
            // Held up out of its lane: back into its own once that is clear (or it may not pass), not nose to tail in the other.
            if (_targetOffset != 0f && !_pulledOver && !_reverse && Time.time >= _nextPassLook)
            {
                _nextPassLook = Time.time + PassLookInterval;
                if (!MayPass || !IsBlocked(middle - roadRight * _offset, forward))
                {
                    _targetOffset = 0f;
                }
            }
            // Already out of lane and still held up: there is no third lane to try. Close to where it
            // will stop there is nothing to gain by pulling out. Otherwise the lanes either side are
            // looked at, but not at every glance: finding a lane means asking the road.
            bool look = MayPass && !_reverse && !_pulledOver && _targetOffset == 0f && Mathf.Abs(_offset) < 0.3f && Time.time >= _nextPassLook && HasRoomToPass(goalSqr);
            if (look && TryPass(middle, forward, roadRight))
            {
                Unblock();
            }
            else
            {
                if (look)
                {
                    _nextPassLook = Time.time + PassLookInterval;
                }
                float room = free - StopGap;
                // (A careless driver plans with all the braking they have, which is to say late.)
                float plan = _stopping < Slowing ? _stopping : CruiseSpeed > FastFrom ? FastBraking : Braking;
                _limit = room > 0.1f ? Mathf.Sqrt(2f * plan * room) : 0f;
                _blocker = blocker;
                if (_limit > 0f)
                {
                    _blocked = false;
                }
                else if (!_blocked)
                {
                    _blocked = true;
                    _blockedSince = Time.time;
                }
            }
        }

        private void Unblock()
        {
            _blocked = false;
            _blocker = null;
            _limit = float.MaxValue;
        }

        private bool HasRoomToPass(float goalSqr)
        {
            if (_following)
            {
                return true;
            }
            if (_routeOnly)
            {
                return RouteLeft > PassRoom;
            }
            float near = _stopDistance + PassRoom;
            return goalSqr > near * near;
        }

        private bool IsBlocked(Vector3 origin, Vector3 direction)
        {
            return Physics.CheckSphere(origin, _halfWidth + SenseMargin, Mask, QueryTriggerInteraction.Ignore)
                   || FreeAhead(origin, direction, beside: true, out _) < float.MaxValue;
        }

        // Metres clear ahead of its nose (float.MaxValue: nothing in reach), by a ball as wide as it, swept further the faster it goes.
        private float FreeAhead(Vector3 origin, Vector3 direction, bool beside, out Rigidbody blocker)
        {
            blocker = null;
            if (!beside)
            {
                _aheadHit = null;   // set again below if the look finds something with a collider
            }
            float halfLength = _car.length * 0.5f;
            // From a fast drive's speeds, as far as it takes to stop and a little over; a careless driver takes in only a share.
            float reach = halfLength + Mathf.Max(SenseDistance + Mathf.Max(0f, _speed - DefaultCruiseSpeed), _speed * _speed / (2f * Slowing) * 1.15f + StopGap + 4f) * _foresight;
            float halfWidth = _halfWidth + SenseMargin;
            float free = float.MaxValue;
            bool onFoot = false;
            if (!beside)
            {
                _solidRoom = float.MaxValue;   // measured afresh by this look, if there is anything to measure
            }
            if (_kerbed)
            {
                // Beside a kerb it was told of: the nearest thing on the carriageway, so every hit is looked at (the first may be
                // on the pavement); what the ball starts inside, and the vehicle itself, are no hit.
                int count = Physics.SphereCastNonAlloc(origin, halfWidth, direction, Hits, reach, Mask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit seen = Hits[i];
                    Hits[i] = default;
                    if (seen.distance <= 0f || seen.rigidbody == _body || (_target != null && seen.rigidbody == _target)
                        || seen.point.x * _kerbTowards.x + seen.point.z * _kerbTowards.z > _kerbAt)
                    {
                        continue;
                    }
                    if (seen.distance < nearest)
                    {
                        nearest = seen.distance;
                        blocker = seen.rigidbody;
                        if (!beside)
                        {
                            _aheadHit = seen.collider;
                        }
                    }
                }
                if (nearest < float.MaxValue)
                {
                    free = Mathf.Max(0f, nearest + halfWidth * 0.5f - halfLength);
                }
            }
            else if (Physics.SphereCast(origin, halfWidth, direction, out RaycastHit hit, reach, Mask | (_solids && !beside ? _solidMask : 0), QueryTriggerInteraction.Ignore))
            {
                float travelled = hit.distance;
                bool found = true;
                if (hit.rigidbody == _body)
                {
                    // A part of our own vehicle the ball did not start inside: look again from just ahead of the nose.
                    float skip = halfLength + 0.4f;
                    found = Physics.SphereCast(origin + direction * skip, halfWidth, direction, out hit, reach - skip, Mask | (_solids && !beside ? _solidMask : 0), QueryTriggerInteraction.Ignore)
                            && hit.rigidbody != _body;
                    travelled = skip + hit.distance;
                }
                if (found && _solids && !beside && !_sliding && hit.collider != null && ((1 << hit.collider.gameObject.layer) & _solidMask) != 0)
                {
                    // A wall or a lamp post while the car is being steered: measured, not braked for (see SeeSolids).
                    _solidRoom = Mathf.Max(0f, travelled + halfWidth * 0.5f - halfLength);
                }
                else if (found && (_target == null || hit.rigidbody != _target))
                {
                    blocker = hit.rigidbody;
                    if (!beside)
                    {
                        _aheadHit = hit.collider;
                    }
                    free = Mathf.Max(0f, travelled + halfWidth * 0.5f - halfLength);
                    onFoot = hit.collider != null && ((1 << hit.collider.gameObject.layer) & TrafficTools.PeopleMask) != 0;
                }
            }
            // People a physics query cannot see: those standing still, kneeling or lying have no collider.
            float person = _kerbed
                ? PeopleAhead.Nearest(origin, direction, beside ? -halfLength : halfLength, reach, halfWidth + 0.3f, _kerbTowards, _kerbAt)
                : PeopleAhead.Nearest(origin, direction, beside ? -halfLength : halfLength, reach, halfWidth + 0.3f);
            if (person < float.MaxValue)
            {
                float room = Mathf.Max(0f, person - halfLength - PersonGap);
                if (room < free)
                {
                    free = room;
                    blocker = null;
                    if (!beside)
                    {
                        _aheadHit = null;
                    }
                    onFoot = true;
                }
            }
            if (!beside)
            {
                _aheadPerson = onFoot;
            }
            return free;
        }

        // Everything solid and everybody, or for a driver who does not see them everything but the game's pedestrians.
        private int Mask => _seesPeople || _blindMask == 0 ? _obstacleMask : _blindMask;

        private bool _solids;
        private bool _sliding;
        private float _solidRoom = float.MaxValue;

        private void SlideOn(Vector3 position)
        {
            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + SenseInterval;
                _room = FreeAhead(position + Vector3.up * SenseHeight, _body.rotation * Vector3.forward, beside: false, out _ahead);
            }
            else if (_room < float.MaxValue)
            {
                _room = Mathf.Max(0f, _room - _speed * Time.fixedDeltaTime);   // between two looks: what it has covered since is off the room it had
            }
            _speed = Mathf.MoveTowards(_speed, 0f, _stopping * Time.fixedDeltaTime);
            if (_speed <= 0.3f)
            {
                Stop();
                return;
            }
            Move(_speed);
        }
    }
}
