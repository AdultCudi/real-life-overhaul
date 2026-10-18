using System;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Scene control: the taped rectangle surveyed and dressed, its posts, tape and barriers placed on the carriageway.</summary>
    public sealed partial class SceneCordon
    {
        // The taped rectangle: the wrecks and a working margin, pulled in to a near kerb; then its corners, the entry side, the posts.
        private void Survey()
        {
            _back = _alongMin - Margin;
            _front = _alongMax + Margin;
            _left = _acrossMin - Side;
            _rightEdge = _acrossMax + Side;

            float middleAlong = (_alongMin + _alongMax) * 0.5f;
            float middleAcross = (_acrossMin + _acrossMax) * 0.5f;
            _crossing = IsPavement(middleAlong, middleAcross);
            _kerbLeft = !_crossing && FindKerb(-1f, middleAlong, middleAcross, ref _left);
            _kerbRight = !_crossing && FindKerb(1f, middleAlong, middleAcross, ref _rightEdge);
            // A crossing inside the tape (walled all the same).
            for (int i = 0; i < 3 && !_crossing; i++)
            {
                float along = Mathf.Lerp(_back, _front, i * 0.5f);
                _crossing = IsPavement(along, Mathf.Lerp(_left, _rightEdge, 0.25f)) || IsPavement(along, Mathf.Lerp(_left, _rightEdge, 0.75f));
            }

            // A parking lane with cars in it is not carriageway: the tape stops short of them, and that side counts as a kerb.
            bool pavementLeft = _kerbLeft;
            bool pavementRight = _kerbRight;
            bool parkedLeft = ClampToParked(-1f, ref _left);
            bool parkedRight = ClampToParked(1f, ref _rightEdge);
            _kerbLeft |= parkedLeft;
            _kerbRight |= parkedRight;
            // An edge pulled in for parked cars is pushed out again to clear the wrecks; not at a kerb, where a wreck against
            // it would take the tape onto the pavement: there the wreck itself closes that stretch.
            float left = _left;
            float right = _rightEdge;
            if (!pavementLeft)
            {
                _left = Mathf.Min(_left, _acrossMin - 0.3f);
            }
            if (!pavementRight)
            {
                _rightEdge = Mathf.Max(_rightEdge, _acrossMax + 0.3f);
            }
            if (_rightEdge - _left < 2.5f)
            {
                // Wrecks almost wholly off the carriageway: no room between the kerbs, so the edges clear the wrecks.
                _left = Mathf.Min(left, _acrossMin - 0.3f);
                _rightEdge = Mathf.Max(right, _acrossMax + 0.3f);
            }
            CrewRoom(pavementLeft || parkedLeft, pavementRight || parkedRight);

            _entry = _kerbRight && !_kerbLeft ? 1f : _kerbLeft && !_kerbRight ? -1f : _gateSide != 0f ? -_gateSide : 1f;

            // The near end's opening is in line with the ambulance, so its crew goes straight in and out.
            float span = _rightEdge - _left;
            _doorWidth = Mathf.Min(EntryGap, span * 0.6f);
            float door = (_left + _rightEdge) * 0.5f;
            if (Stand(false, 9f, out _, out Vector3 ambulance))
            {
                door = Vector3.Dot(ambulance - _scene, _right);
            }
            float reach = Mathf.Max(0f, span * 0.5f - _doorWidth * 0.5f - 0.6f);
            _doorAcross = Mathf.Clamp(door, (_left + _rightEdge) * 0.5f - reach, (_left + _rightEdge) * 0.5f + reach);

            // Whoever only waits stands outside the tape: a place the street's mesh moved in over the line is put back out.
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = Point(i == 0 || i == 3 ? _back - 1f : _front + 1f, i < 2 ? _left - 1f : _rightEdge + 1f);
                _corners[i] = KeepOutside(StreetSpots.OnRoad(corner, 1.5f, out Vector3 ground) ? ground : corner, WaitOut);
            }
            // At the end traffic arrives at, on the side it is let past.
            Vector3 duty = Point(_back - 1.6f, _entry > 0f ? _left - 0.8f : _rightEdge + 0.8f);
            DirectorPost = StreetSpots.OnRoad(duty, 2f, out Vector3 stand) ? stand : duty;
            Oncoming = DirectorPost - _forward * 20f;
            // ...and, for when a response unit passes by that lane: the same end, well in from the lane (no further than the
            // middle); and beside the wrecks' near end on that side, for once the tape is down and a unit pulls out round them.
            float step = Mathf.Min(1.6f, (_rightEdge - _left) * 0.5f);
            Vector3 inward = Point(_back - 1.6f, _entry > 0f ? _left + step : _rightEdge - step);
            PassPost = StreetSpots.OnRoad(inward, 1.5f, out Vector3 beside) ? beside : inward;
            Vector3 byWrecks = Point(_alongMin + 0.5f, _entry > 0f ? _left + 0.35f : _rightEdge - 0.35f);
            PassBeside = StreetSpots.OnRoad(byWrecks, 1f, out Vector3 wreckSide) ? wreckSide : byWrecks;

            // Where people with no business inside are sent: the pavement by the near entry, else the road just outside it.
            Vector3 aside = Point(_back - 2.5f, (_entry > 0f ? _rightEdge : _left) + _entry * 2.5f);
            Kerbside = KeepOutside(StreetSpots.OnPavement(aside, 4f, out Vector3 kerb) ? kerb
                : StreetSpots.OnRoad(Point(_back - 3f, (_entry > 0f ? _rightEdge : _left) - _entry * 1f), 2f, out kerb) ? kerb : DirectorPost, WaitOut);

            // Just outside the far end's opening, looking up the road away from the scene.
            float opening = Mathf.Min(EntryGap, span * 0.5f);
            Vector3 gate = Point(_front + BarrierOut + 0.6f, (_entry > 0f ? _rightEdge : _left) - _entry * opening * 0.5f);
            EntryPost = StreetSpots.OnRoad(gate, 1.5f, out Vector3 post) ? post : gate;
            EntryLook = EntryPost + _forward * 12f;
        }

        // The crews walk past the held vehicles with the stretcher: one side needs CrewLane between vehicle and tape. If neither
        // has it, the side free to move is taken out (never the side traffic passes on, a kerb or parked cars); else logged.
        private void CrewRoom(bool fixedLeft, bool fixedRight)
        {
            if (!_anySolid)
            {
                return;
            }
            float roomLeft = _solidMin - _left;
            float roomRight = _rightEdge - _solidMax;
            if (roomLeft >= CrewLane || roomRight >= CrewLane)
            {
                return;
            }
            bool canLeft = !fixedLeft && _gateSide >= 0f;
            bool canRight = !fixedRight && _gateSide <= 0f;
            if (canLeft && (!canRight || roomLeft >= roomRight))
            {
                _left = _solidMin - CrewLane;
            }
            else if (canRight)
            {
                _rightEdge = _solidMax + CrewLane;
            }
            else
            {
                Logger?.Info(FormattableString.Invariant($"Cordon: no side of the held vehicle(s) can be given {CrewLane:F1} m for the crews ")
                             + FormattableString.Invariant($"(kerb, parked cars or the open lane): {roomLeft:F1} m on the left, {roomRight:F1} m on the right."));
            }
        }

        // A parked vehicle or street furniture on the carriageway beside the wrecks: the edge on that side stops short of it.
        private bool ClampToParked(float side, ref float edge)
        {
            if (ParkedMask == 0)
            {
                return false;
            }
            float from = side > 0f ? _acrossMax + 0.35f : _acrossMin - 0.35f;
            if ((edge - from) * side <= 0f)
            {
                return false;   // the kerb has already brought this edge in to the wrecks: nothing between them to look at
            }
            var strip = new Vector3(0.15f, 0.5f, (_front - _back) * 0.5f);
            Quaternion facing = Quaternion.LookRotation(_forward, Vector3.up);
            for (float step = 0f; step <= Mathf.Abs(edge - from) + 0.01f; step += 0.3f)
            {
                float across = from + side * step;
                Vector3 at = Point((_back + _front) * 0.5f, across);
                if (StreetSpots.Ground(at, out float ground))
                {
                    at.y = ground;
                }
                if (Physics.CheckBox(at + Vector3.up * 0.9f, strip, facing, ParkedMask, QueryTriggerInteraction.Ignore))
                {
                    edge = across - side * ParkedGap;
                    return true;
                }
            }
            return false;
        }

        // A pavement close beside the wrecks: the edge on that side is pulled in to just short of it.
        private bool FindKerb(float side, float along, float middle, ref float edge)
        {
            float reach = Mathf.Abs(edge - middle) + KerbReach;
            for (float step = 0.5f; step <= reach; step += 0.5f)
            {
                float across = middle + side * step;
                if (IsPavement(along, across))
                {
                    float kerb = across - side * KerbInset;
                    edge = side > 0f ? Mathf.Min(edge, kerb) : Mathf.Max(edge, kerb);
                    return true;
                }
            }
            return false;
        }

        private bool IsPavement(float along, float across)
        {
            Vector3 point = Point(along, across);
            StreetSpots.Ground(point, out float ground);
            point.y = ground;
            return NavMesh.SamplePosition(point, out _, 0.35f, StreetSpots.Pavement);
        }

        private Vector3 Point(float along, float across)
        {
            return _scene + _forward * along + _right * across;
        }

        private void Dress(float now)
        {
            if (_props != null && PropLibrary.Ready && !_modelled)
            {
                // Built from plain shapes before the modelled props had loaded: once, built again with them.
                UnityEngine.Object.Destroy(_props);
                _props = null;
            }
            if (_props == null)
            {
                Build();
            }
            float entryEdge = _entry > 0f ? _rightEdge : _left;
            float otherEdge = _entry > 0f ? _left : _rightEdge;
            float width = Mathf.Abs(entryEdge - otherEdge);
            float length = _front - _back;
            float gap = Mathf.Min(EntryGap, width * 0.5f);
            float gapEnd = entryEdge - _entry * gap;   // where each end's tape stops, short of the entry-side corner

            float doorNear = _doorAcross + _entry * _doorWidth * 0.5f;
            float doorFar = _doorAcross - _entry * _doorWidth * 0.5f;

            // Tape on posts in two runs that leave both openings, every stretch walled as it goes up.
            _placed = 0;
            _taped = 0;
            _wallCount = 0;
            _tapeBlocked = 0;
            _blockedBy = null;
            _movedPosts = 0;
            _wayRefused = 0;
            _wayOpenings = 0;
            // (An opening that reaches a corner leaves no tape on that side of it: the side then starts at the corner by itself.)
            bool nearEntry = Mathf.Abs(doorNear - entryEdge) > 0.3f;
            bool nearOther = Mathf.Abs(doorFar - otherEdge) > 0.3f;
            if (nearEntry)
            {
                Line(_back, doorNear, _back, entryEdge, fresh: true, outward: -1f);
            }
            Run(_back, entryEdge, _front, entryEdge, fresh: !nearEntry, outward: 0f);
            if (nearOther)
            {
                Line(_back, doorFar, _back, otherEdge, fresh: true, outward: -1f);
            }
            Run(_back, otherEdge, _front, otherEdge, fresh: !nearOther, outward: 0f);
            Run(_front, otherEdge, _front, gapEnd, fresh: false, outward: 1f);
            HideFrom(_posts, _placed);
            HideFrom(_tapes, _taped);

            // Barriers across both ends, facing outwards: end to end across the closed part, none in an opening.
            Quaternion nearFacing = Quaternion.LookRotation(-_forward, Vector3.up);
            int barriers = Barriers(0, _back - BarrierOut, otherEdge, doorFar, nearFacing);
            barriers = Barriers(barriers, _back - BarrierOut, doorNear, entryEdge, nearFacing);
            barriers = Barriers(barriers, _front + BarrierOut, otherEdge, gapEnd, Quaternion.LookRotation(_forward, Vector3.up));
            HideFrom(_barriers, barriers);

            // The cones are a taper, laid when the response vehicles are in (LayTaper).
            if (!_redress)
            {
                HideFrom(_cones, 0);
            }

            // The tape that went up, as walls for anyone on foot; the openings are where no tape runs.
            bool walled = _wallCount > 0;
            _wallsChanged = 0;
            for (int i = 0; i < Walls; i++)
            {
                bool used = walled && i < _wallCount && SetWall(i);
                GameObject box = _walls[i].gameObject;
                if (box.activeSelf != used)
                {
                    _wallsChanged += used ? 0 : 1;   // (a wall that SetWall moved was counted there)
                    box.SetActive(used);
                }
            }
            // ...and a bar across every opening a walkway of the game's pedestrians runs through: their mesh inside is an island.
            _barCount = walled ? Bar() : 0;
            for (int i = _barCount; i < Bars; i++)
            {
                GameObject box = _bars[i].gameObject;
                if (box.activeSelf)
                {
                    _wallsChanged++;
                    box.SetActive(false);
                }
            }
            MeshCuts.Note("a cordon's walls", _wallsChanged);   // all in this one frame: the engine cuts the mesh once for the lot
            _props.SetActive(true);
            _carveAt = walled ? now + CarveWait : 0f;
            _carveTries = 0;
            Logger?.Info($"Cordon: tape up, {length:F0} x {width:F0} m, {_placed} post(s), {_taped} length(s) of tape, {barriers} barrier(s), "
                         + FormattableString.Invariant($"{_wallCount} wall(s), open at the near end {_doorAcross - _left:F1} m from the left edge ")
                         + FormattableString.Invariant($"({_doorWidth:F1} m wide: the crew's corridor) and at the far end's {(_entry > 0f ? "right" : "left")} corner")
                         + (_crossing ? "; a crossing runs through it, shut to people on foot while the tape is up" : "")
                         + (_barCount > 0 ? $"; {_barCount} bar(s) across an opening a walkway runs through" : "")
                         + $", kerb {(_kerbLeft ? (_kerbRight ? "both sides" : "left") : _kerbRight ? "right" : "on neither side")}"
                         + (_movedPosts > 0 ? $"; {_movedPosts} post(s) moved in off their line" : "")
                         + (_tapeBlocked > 0 ? $"; {_tapeBlocked} stretch(es) of tape left out, in the way: '{_blockedBy}'" : "") + ".");
            CheckWay(barriers);
            // Nobody but the scene's own people inside it: whoever is, is put at the nearest place outside, on the frame the tape appears.
            CordonClear.Out(this, Logger);
        }

        // A side of the rectangle, with an opening where the crew's way crosses it: tape up to it and on from it.
        private void Run(float fromAlong, float fromAcross, float toAlong, float toAcross, bool fresh, float outward)
        {
            Vector3 from = Point(fromAlong, fromAcross);
            Vector3 to = Point(toAlong, toAcross);
            float length = (to - from).magnitude;
            if (!HasWay || length < 0.5f || !WayCrossing(from, to, CrewClear, out float low, out float high))
            {
                Line(fromAlong, fromAcross, toAlong, toAcross, fresh, outward);
                return;
            }
            float more = (CrewGap - (high - low) * length) * 0.5f / length;
            if (more > 0f)
            {
                low -= more;
                high += more;
            }
            _wayOpenings++;
            if (low * length > 0.6f)
            {
                Line(fromAlong, fromAcross, Mathf.Lerp(fromAlong, toAlong, low), Mathf.Lerp(fromAcross, toAcross, low), fresh, outward);
            }
            if ((1f - high) * length > 0.6f)
            {
                Line(Mathf.Lerp(fromAlong, toAlong, high), Mathf.Lerp(fromAcross, toAcross, high), toAlong, toAcross, fresh: true, outward);
            }
            else
            {
                _hasLast = false;   // the opening reaches the corner: the next side starts by itself
            }
        }

        private int Barriers(int used, float along, float from, float to, Quaternion facing)
        {
            float run = Mathf.Abs(to - from);
            int count = Mathf.FloorToInt((run + 0.4f) / BarrierWidth);
            float step = to >= from ? BarrierWidth : -BarrierWidth;
            float first = (from + to) * 0.5f - step * (count - 1) * 0.5f;
            for (int i = 0; i < count && used < MaxBarriers; i++)
            {
                if (Place(along, first + step * i, 0.6f, post: false, out Vector3 ground)
                    && !Physics.CheckBox(ground + Vector3.up * 0.6f, new Vector3(BarrierWidth * 0.5f, 0.4f, 0.3f), facing,
                        TrafficTools.VehicleMask | ParkedMask, QueryTriggerInteraction.Ignore))
                {
                    _barriers[used].SetPositionAndRotation(ground, facing);
                    _barriers[used].gameObject.SetActive(true);
                    used++;
                }
            }
            return used;
        }

        // Posts from one point of the rectangle to another, tape between, a wall along each unbroken run; outward -1, 1 or 0 (a side).
        private void Line(float fromAlong, float fromAcross, float toAlong, float toAcross, bool fresh, float outward)
        {
            float run = Mathf.Abs(toAlong - fromAlong) + Mathf.Abs(toAcross - fromAcross);   // the lines run along or across, never both
            int steps = Mathf.Max(1, Mathf.CeilToInt(run / PostSpacing));
            if (fresh)
            {
                _hasLast = false;
            }
            _segment = -1;   // a new straight: its tape is another wall
            _outward = outward;
            // The line starts from the corner post it shares with the line before, if that went up; if not, the first stretch
            // cuts across the corner and is walled on its own, so no wall takes a short cut through the working area.
            bool cornered = fresh || (Mathf.Abs(_lastAlong - fromAlong) < 0.1f && Mathf.Abs(_lastAcross - fromAcross) < 0.1f);
            for (int i = fresh ? 0 : 1; i <= steps && _placed < MaxPosts; i++)
            {
                float t = (float)i / steps;
                float along = Mathf.Lerp(fromAlong, toAlong, t);
                float across = Mathf.Lerp(fromAcross, toAcross, t);
                if (!Place(along, across, 0.3f, post: true, out Vector3 ground))
                {
                    continue;   // in a vehicle, off the carriageway or on a kerb: the tape runs on to the next post
                }
                _posts[_placed].SetPositionAndRotation(ground, Quaternion.identity);
                _posts[_placed].gameObject.SetActive(true);
                _placed++;
                _lastAlong = along;
                _lastAcross = across;
                cornered |= !_hasLast;   // nothing to join up with: this post starts the run
                if (_hasLast && !cornered)
                {
                    cornered = true;
                    if (Stretch(_last, ground) && _wallCount < Walls)
                    {
                        _wallFrom[_wallCount] = _last;
                        _wallTo[_wallCount] = ground;
                        _wallOut[_wallCount] = 0f;
                        _wallCount++;
                    }
                    _segment = -1;
                }
                else if (_hasLast && Stretch(_last, ground))
                {
                    if (_segment < 0 && _wallCount < Walls)
                    {
                        _segment = _wallCount++;
                        _wallFrom[_segment] = _last;
                        _wallOut[_segment] = _outward;
                    }
                    if (_segment >= 0)
                    {
                        _wallTo[_segment] = ground;
                    }
                }
                else
                {
                    _segment = -1;
                }
                _last = ground;
                _hasLast = true;
            }
        }

        // Parked vehicles, the player's and street furniture: what the tape and the barriers stop short of.
        private static int ParkedMask => _parkedMask != 0 ? _parkedMask : (_parkedMask = LayerMask.GetMask("ParkedVehicles", "PlayerVehicles", "StreetProps"));

        // Everything a length of tape must not pass through: vehicles, street furniture, buildings and the default layer.
        private static int SolidMask
        {
            get
            {
                if (_solidMask == 0)
                {
                    _solidMask = TrafficTools.VehicleMask
                                 | LayerMask.GetMask("Default", "StreetProps", "Buildings", "BuildingWalls", "BuildingsOutlined", "InteractiveItems", "InteractiveItemsOutlined");
                }
                return _solidMask;
            }
        }

        // Tape between two posts near enough with nothing solid between, looked for at tape height and low down. False if left out.
        private bool Stretch(Vector3 from, Vector3 to)
        {
            Vector3 run = to - from;
            float length = run.magnitude;
            Vector3 lift = Vector3.up * _tapeHeight;
            if (_taped >= _tapes.Length || length < 0.3f || length > PostSpacing * 2.3f)
            {
                return false;
            }
            if (HasWay && WayCrossing(from, to, CrewClear - 0.1f, out _, out _))
            {
                _wayRefused++;
                return false;   // across the ambulance crew's way: that stretch stays open
            }
            Vector3 low = Vector3.up * 0.3f;
            if (Physics.Linecast(from + lift, to + lift, out RaycastHit hit, SolidMask, QueryTriggerInteraction.Ignore)
                || Physics.Linecast(from + low, to + low, out hit, SolidMask, QueryTriggerInteraction.Ignore))
            {
                _tapeBlocked++;
                if (_blockedBy == null && hit.collider != null)
                {
                    _blockedBy = hit.collider.name + " on layer " + LayerMask.LayerToName(hit.collider.gameObject.layer);
                }
                return false;
            }
            _tapeFrom[_taped] = from;
            _tapeTo[_taped] = to;
            Transform tape = _tapes[_taped++];
            tape.SetPositionAndRotation((from + to) * 0.5f + lift, Quaternion.LookRotation(run / length, Vector3.up));
            // The modelled strip is made a set length with its lettering to scale: stretched along its run only.
            tape.localScale = _tapeUnit > 0f ? new Vector3(1f, 1f, length / _tapeUnit) : new Vector3(0.012f, 0.075f, length);
            tape.gameObject.SetActive(true);
            return true;
        }

        // The ground at a point of the rectangle if a prop can stand there: carriageway (road mesh, no pavement) and in no
        // vehicle. A post that cannot is tried further in across the road, twice, never along it, so the tape runs straight.
        private bool Place(float along, float across, float radius, bool post, out Vector3 ground)
        {
            float middleAcross = (_left + _rightEdge) * 0.5f;
            for (int attempt = 0; attempt < (post ? 3 : 1); attempt++)
            {
                Vector3 wanted = Point(along, across);
                // Never on the ambulance crew's way, nor within reach of anything of the crew on it: that place stays empty.
                if (HasWay && WayClearance(wanted) < CrewClear - 0.1f)
                {
                    _wayRefused++;
                    break;
                }
                bool there = StreetSpots.OnRoad(wanted, 1f, out ground);
                if (there)
                {
                    Vector3 off = ground - wanted;
                    off.y = 0f;
                    there = off.sqrMagnitude < 0.3f * 0.3f;
                }
                // The tape run again: the mesh is still cut where its walls stood, so there the ground itself is asked (it was
                // road when the tape first went up); else every post would move in off its line, onto the crew's way.
                if (!there && _redress && NearOldWall(wanted) && StreetSpots.Ground(wanted, out float height))
                {
                    ground = new Vector3(wanted.x, height, wanted.z);
                    there = true;
                }
                if (there && !Physics.CheckSphere(ground + Vector3.up * 0.6f, radius, TrafficTools.VehicleMask, QueryTriggerInteraction.Ignore)
                    && (_crossing || !NavMesh.SamplePosition(ground, out _, 0.2f, StreetSpots.Pavement)))
                {
                    if (attempt > 0)
                    {
                        _movedPosts++;
                    }
                    return true;
                }
                across = Mathf.MoveTowards(across, middleAcross, 0.5f);
            }
            ground = default;
            return false;
        }

        // True within reach of where a wall of the last dressing stood (its cut in the navigation mesh, and the mesh's own margin round it).
        private bool NearOldWall(Vector3 point)
        {
            for (int i = 0; i < _oldCount; i++)
            {
                Vector3 run = _oldTo[i] - _oldFrom[i];
                run.y = 0f;
                Vector3 offset = point - _oldFrom[i];
                offset.y = 0f;
                float length = run.magnitude;
                float along = length > 0.01f ? Mathf.Clamp(Vector3.Dot(offset, run) / length, 0f, length) : 0f;
                Vector3 nearest = length > 0.01f ? run * (along / length) : Vector3.zero;
                if ((offset - nearest).sqrMagnitude < OldWallReach * OldWallReach)
                {
                    return true;
                }
            }
            return false;
        }

        private const float OldWallReach = BarrierWall + 1f;   // an end's wall is that deep, and the mesh is cut a little wider than the wall

        private static void HideFrom(Transform[] props, int from)
        {
            for (int i = from; i < props.Length; i++)
            {
                props[i].gameObject.SetActive(false);
            }
        }
    }
}
