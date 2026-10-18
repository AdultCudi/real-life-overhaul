using System;
using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the ambulance and the patrol car, from being sent to their crews at work.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float AmbulanceRouteLength = 170f;   // metres of road the ambulance comes in along
        private const float AmbulanceStopDistance = 13f;   // metres; only where the scene has no lane to stand in
        private const float AmbulanceBack = 10f;        // metres from the nearest wreck or casualty to the ambulance's middle, outside the tape
        private const float PatrolBack = 21f;           // ...and of the patrol car when it stands behind the ambulance
        private const float PatrolBackAlone = 10f;      // ...or has that end of the scene to itself
        private const float CrewWalkLimit = 150f;       // metres: a unit stopped further from its place keeps its crew aboard
        private const float BlockedCheck = 0.5f;        // seconds between looks at what is holding a unit up
        private const float HeldBeforeMoving = 1.5f;    // seconds a unit stands behind traffic before that traffic is moved on (out of sight)
        private const float HeldBeforeMovingSeen = 4f;  // ...or in sight
        private const float RearDoorRoom = 6.5f;        // metres behind the ambulance kept free of cones: its crew turns the stretcher round there
        private const float CrewWalk = 14f;             // metres of route left: held up within this a unit stops for good; further out it waits
        private const float ParkLookDistance = 48f;     // metres from the scene at which a unit picks where to pull up
        private const float DriveAllowance = 60f;       // seconds a unit may take to reach the scene
        private const float StandBackMost = 18f;        // metres back along its own lane a clear place to stand is looked for
        private const float StandStep = 1.5f;           // metres
        private const float StandMargin = 0.3f;         // metres all round its body that the place it stands must be clear of everything solid
        private const float CrewGrace = 14f;            // seconds a unit waits past its time for a crew still walking back
        private const float StretcherGrace = 40f;       // ...and for paramedics bringing someone back on the stretcher
        private const float CrewLastCall = 6f;   // seconds before the hard limit at which the ambulance goes, whoever is still out

        private sealed class Attendance
        {
            public readonly ResponseUnit Unit;
            public readonly bool Police;
            public Stage Stage;
            public float At;                        // when the current stage ends
            public float StopDistance;
            public bool Retried;
            public bool ParkChosen;                 // where to pull up has been looked for
            public bool Recalled;                   // the crew has been called back to the vehicle
            public bool FarEnd;                     // it comes to the far end of the scene, by the lane running the other way
            public bool AtStand;                    // it is driving to (or standing at) a place the cordon gave it
            public float NextCheck;                 // when what is holding it up is next looked at
            public int MovedOn;                     // traffic cars taken out of its way
            public bool Waits;                      // crew aboard, but it goes only when the scene clears round it (the patrol car)
            public bool Through;                    // it is driving away through where the scene is: at a careful pace until it is beyond
            public float Still;                     // since when it has stood with its crew aboard and free to go; 0 while it is moving
            public Vector3 From;                    // where it set off from when it left
            public bool OwnLane;                    // it leaves through the scene by its own lane, checked for it: it does not pull out to pass there
            public bool Backing;                    // crew aboard, backing along its own lane out of the other unit's way
            public Vector3 BackFrom;                // ...from here
            public float BackUntil;                 // ...and for no longer than this
            public bool WaitedBefore;               // ...and whether it was waiting for the scene to clear before that
            public int GaveWay;                     // times the other unit was moved out of this one's way as it left
            public float SetOffAt;                  // when it set off to leave
            public bool SaidHeld;                   // it has been said that it is held up in view and is not taken off the road there

            public Attendance(ResponseUnit unit, bool police)
            {
                Unit = unit;
                Police = police;
            }

            /// <summary>Nothing more will happen with this unit at the scene: it never came, or it has gone.</summary>
            public bool Finished => Stage == Stage.Idle || Stage == Stage.Done || (Stage == Stage.Ready && Waits);

            /// <summary>It has left the scene's working area, or is on its way out of it.</summary>
            public bool Going => Finished || Stage == Stage.Leaving || Stage == Stage.Ready;

            /// <summary>Beyond the scene, or never at it: nothing of it left for the other unit to wait for.</summary>
            public bool Past => Stage == Stage.Idle || Stage == Stage.Done || (Stage == Stage.Leaving && !Through);
        }

        // The road measured and (unless a minor bump) closed, the vehicles in it solid for people on foot, the units scheduled.
        private void PlanResponse(float now, bool ambulance, bool police, float ambulanceAt, float policeAt, ICasualty casualty, bool shut)
        {
            _hardLimit = now + HardSceneTime;
            _extended = false;
            _clearLogged = false;
            _phase = Phase.Active;
            EndDepartures();   // a unit still driving away from the last scene is wanted for this one
            CloseRoad(casualty, shut);
            _people.OnRoadClosed(_cordon);
            for (int i = 0; i < _wrecks.Count; i++)
            {
                SceneObstacles.Add(_wrecks[i].Car, transform);
            }

            Schedule(_ambulance, ambulance, ambulanceAt, AmbulanceStopDistance);
            Schedule(_police, police, policeAt, Random.Range(19f, 25f));
            // The patrol car takes the far end where a lane runs up to it; on one lane the ambulance goes first, nearest the hurt.
            _police.FarEnd = police && _cordon.HasFarEnd;
            if (police && ambulance && !_police.FarEnd && _chase != Chase.Planned)
            {
                _police.At = Mathf.Max(_police.At, _ambulance.At + 5f);
            }
            PlanDeparture(ambulance, police);   // where the patrol car stands is settled against the ambulance's way out, now
        }

        private static void Schedule(Attendance attendance, bool attends, float at, float stopDistance)
        {
            attendance.Stage = attends ? Stage.Waiting : Stage.Idle;
            attendance.At = at;
            attendance.StopDistance = stopDistance;
            attendance.Retried = false;
            attendance.ParkChosen = false;
            attendance.Recalled = false;
            attendance.FarEnd = false;
            attendance.AtStand = false;
            attendance.NextCheck = 0f;
            attendance.MovedOn = 0;
            attendance.Waits = false;
            attendance.Through = false;
            attendance.Still = 0f;
            attendance.OwnLane = false;
            attendance.Backing = false;
            attendance.GaveWay = 0;
            attendance.SetOffAt = 0f;
            attendance.SaidHeld = false;
        }

        private float _nextSweep;
        private bool _sweepSaid;
        private const float SweepWithin = 90f;   // metres round a scene in which no ordinary ambulance is left in traffic
        private static readonly int HotSweep = HotSpots.Slot("Traffic: TrafficAccidents.SweepAmbulances");
        // One ambulance at a scene: an ordinary one from traffic that comes near is taken off the road unseen. Every 3 s.
        private void SweepAmbulances()
        {
            using var hotSpot = HotSpots.Time(HotSweep);
            if (!TrafficTools.Ready)
            {
                return;
            }
            List<VehicleComponent> pool = Manager.GetVehicleList();
            VehicleComponent ours = _ambulance.Unit.InUse ? _ambulance.Unit.Car : null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car == ours || !car.gameObject.activeInHierarchy || !TrafficTools.IsAmbulance(car) || TrafficTools.IsHeld(car) || car.presetPath != null)
                {
                    continue;
                }
                Vector3 at = car.transform.position;
                if ((at - _scene).sqrMagnitude > SweepWithin * SweepWithin || Seen(at))
                {
                    continue;
                }
                TrafficTools.Remove(car, "an ordinary ambulance near an accident scene, out of view: the scene has its own");
                if (!_sweepSaid)
                {
                    _sweepSaid = true;
                    Logger?.Info("Accident: an ordinary ambulance from traffic came near the scene and was taken off the road out of view, "
                                 + "so that only the scene's own stands there. (Said once a session.)");
                }
            }
        }

        private static readonly int HotAttend = HotSpots.Slot("Traffic: TrafficAccidents.TickAttendance");
        // One unit's visit, from being sent to driving away; every stage also ends on a clock, said once in the log.
        private void TickAttendance(Attendance attendance, float now)
        {
            using var hotSpot = HotSpots.Time(HotAttend);
            ResponseUnit unit = attendance.Unit;
            switch (attendance.Stage)
            {
                case Stage.Waiting:
                    if (now >= attendance.At)
                    {
                        Dispatch(attendance, now);
                    }
                    break;

                case Stage.EnRoute:
                    if (unit.IsLost)
                    {
                        Logger?.Info($"Accident: the {Name(attendance)} was taken off the road on its way; the scene runs without it.");
                        Withdraw(attendance);
                        break;
                    }
                    float sceneSqr = (unit.Car.transform.position - _scene).sqrMagnitude;
                    if (attendance.Police && _chase == Chase.Planned && sceneSqr < FleeTriggerDistance * FleeTriggerDistance)
                    {
                        BeginChase(now);
                        break;
                    }
                    EmergencyDriver driver = unit.Driver;
                    if (!attendance.AtStand && !attendance.ParkChosen && sceneSqr < ParkLookDistance * ParkLookDistance)
                    {
                        // No lane to stand in was to be had: near enough to see what is there, it picks a place on its route.
                        attendance.ParkChosen = true;
                        int place = SceneParking.Find(unit.Car, unit.Path, driver.NextPoint, _scene, attendance.StopDistance);
                        if (place >= 0)
                        {
                            driver.PullUpAt(place);
                        }
                    }
                    if (now >= attendance.NextCheck)
                    {
                        attendance.NextCheck = now + BlockedCheck;
                        ClearTheWay(attendance, driver);
                    }
                    if (driver.Arrived || !driver.Active || now >= attendance.At)
                    {
                        Pulled(attendance, driver, Mathf.Sqrt(sceneSqr), now);
                    }
                    break;

                case Stage.OnScene:
                    if (unit.IsLost)
                    {
                        Logger?.Info($"Accident: the {Name(attendance)} was taken off the road while parked.");
                        Withdraw(attendance);
                        break;
                    }
                    if (!attendance.Recalled && now >= attendance.At && MayLeave(attendance, now))
                    {
                        attendance.Recalled = true;
                        attendance.At = now + (attendance.Police ? CrewGrace : StretcherGrace);
                        _people.Recall(attendance.Police);
                    }
                    bool aboard = _people.CrewAboard(attendance.Police);
                    bool finished = !attendance.Police && _people.CrewWorked;   // the ambulance goes as soon as its patient is aboard
                    // The ambulance never leaves from under its crew: with the stretcher out it waits, up to the hard limit.
                    bool waitsForCrew = !attendance.Police && _people.CrewOut && now < _hardLimit - CrewLastCall;
                    if ((aboard && (attendance.Recalled || finished)) || (attendance.Recalled && now >= attendance.At && !waitsForCrew))
                    {
                        Depart(attendance, now);
                    }
                    break;

                case Stage.Ready:
                    if (unit.IsLost)
                    {
                        Withdraw(attendance);
                    }
                    else if (attendance.Backing)
                    {
                        TickBacking(attendance, now);
                    }
                    else if (attendance.Waits && attendance.Police && _phase == Phase.Active && _ambulance.Past && now >= _lingerUntil && now >= attendance.NextCheck)
                    {
                        // The ambulance is past and the tape has been down a few seconds: if the patrol car's own lane through
                        // the scene is clear it goes now, the road given back once both are away; with a wreck in that lane it
                        // waits for the scene to clear.
                        attendance.NextCheck = now + 1f;
                        TrafficTools.Footprint(unit.Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
                        float reach = Mathf.Clamp(_cordon.Through(centre, forward) + 6f, halfLength + 8f, 70f);
                        if (SceneParking.InStrip(centre + forward * (halfLength + 0.3f), centre + forward * reach, halfWidth + ExitMargin, unit.Car.rb, SceneParking.RoadsideMask) == null)
                        {
                            attendance.OwnLane = true;
                            attendance.Still = 0f;
                            if (!Leave(attendance, now))
                            {
                                Stand(attendance, now, waits: true);
                            }
                        }
                    }
                    else if (!attendance.Waits && now >= attendance.NextCheck)
                    {
                        attendance.NextCheck = now + 1f;
                        TryToGo(attendance, now);
                    }
                    break;

                case Stage.Leaving:
                    if (unit.IsLost)
                    {
                        ReleaseExit(attendance);
                        Withdraw(attendance);
                        break;
                    }
                    Vector3 where = unit.Car.transform.position;
                    if (attendance.Through && (!_cordon.Surveyed || _cordon.Through(where, unit.Car.transform.forward) < -PassedBy))
                    {
                        // Past the scene: it picks up speed, and the lane held for it is given back.
                        attendance.Through = false;
                        unit.Driver.CruiseSpeed = EmergencyDriver.DefaultCruiseSpeed;
                        unit.Driver.MayPass = true;   // beyond the scene it drives as on any road
                        ReleaseExit(attendance);
                        DepartureCheck(Name(attendance) + " past the scene", !unit.Driver.OutOfLane,
                            $"beyond the far side of the taped area {now - attendance.SetOffAt:F0} s after it set off"
                            + (unit.Driver.OutOfLane ? ", out of its own lane" : ", in its own lane")
                            + (attendance.GaveWay > 0 ? $"; the other unit was moved out of its way {attendance.GaveWay} time(s)" : ""));
                    }
                    bool moving = unit.Driver.Active && unit.Driver.Speed > MovingOff;
                    if (moving)
                    {
                        attendance.Still = 0f;
                    }
                    else if (attendance.Still <= 0f)
                    {
                        attendance.Still = now;
                    }
                    bool away = (where - attendance.From).sqrMagnitude > LeaveDistance * LeaveDistance;
                    if (moving && (away || (now >= attendance.At && !attendance.Through)))
                    {
                        // Clear of the scene and moving along a lane: only now does the traffic system have it back.
                        float speed = unit.Driver.Speed;
                        ReleaseExit(attendance);
                        unit.Release(resumeTraffic: true, remove: false, quiet: attendance.Police);
                        attendance.Stage = Stage.Done;
                        Logger?.Info($"Accident: the {Name(attendance)} has left: handed back to traffic {Vector3.Distance(where, attendance.From):F0} m on, moving at {speed * 3.6f:F0} km/h.");
                    }
                    else if (!unit.Driver.Active || unit.Driver.Arrived)
                    {
                        // At the end of the road it was given, and standing: not handed back like that; more road is looked for.
                        ReleaseExit(attendance);
                        Stand(attendance, now, waits: false);
                    }
                    else if (!moving)
                    {
                        if (now >= attendance.NextCheck)
                        {
                            attendance.NextCheck = now + BlockedCheck;
                            if (GiveWay(attendance, now))
                            {
                                attendance.Still = now;   // the other unit is moving out of its way: not boxed in
                                break;
                            }
                            ClearExit(attendance, unit.Driver);
                        }
                        // Behind a person it waits (it never forces through), but not for ever; behind anything else, not for long.
                        bool person = unit.Driver.HeldFor > 0f && unit.Driver.Blocker == null;
                        if (now - attendance.Still > (person ? BoxedInByPeople : BoxedIn))
                        {
                            BoxedOut(attendance, now, person ? "someone stood in front of it" : unit.Driver.Blocker != null
                                ? "'" + unit.Driver.Blocker.name + "' stood in its way and could not be moved" : "it could not get moving");
                        }
                    }
                    break;
            }
        }

        private static string Name(Attendance attendance)
        {
            return attendance.Police ? "patrol car" : "ambulance";
        }

        // Ordinary traffic stopped in a unit's way is taken off the road: at once if unseen, after a few seconds otherwise.
        private void ClearTheWay(Attendance attendance, EmergencyDriver driver)
        {
            float held = driver.HeldFor;
            Rigidbody body = driver.Blocker;
            if (held < HeldBeforeMoving || body == null || attendance.MovedOn >= 8)
            {
                return;
            }
            VehicleComponent car = body.GetComponentInParent<VehicleComponent>();
            if (!IsUsable(car) || car.GetCurrentSpeed() > 1f
                || (held < HeldBeforeMovingSeen && StreetSpots.IsInView(car.transform.position)))
            {
                return;
            }
            if (attendance.MovedOn == 0)
            {
                Logger?.Info($"Accident: the {Name(attendance)} is held up by standing traffic with {driver.RouteLeft:F0} m to go; what is in its way is moved on.");
            }
            attendance.MovedOn++;
            TrafficTools.Remove(car, "standing in a response unit's way");
        }

        // The unit has stopped (where it meant, held up close, or out of time): it parks, solid for walkers, and its crew gets out.
        private void Pulled(Attendance attendance, EmergencyDriver driver, float fromScene, float now)
        {
            ResponseUnit unit = attendance.Unit;
            float left = attendance.AtStand ? driver.RouteLeft : Mathf.Max(0f, fromScene - attendance.StopDistance);
            string how = driver.Arrived ? (left < 3f ? "at its place" : $"held up {left:F0} m short of its place")
                : now >= attendance.At ? $"out of time, {left:F0} m short of its place"
                : $"stopped {left:F0} m short of its place";
            bool crew = left < CrewWalkLimit;

            unit.Park();
            SceneObstacles.Add(unit.Car, transform);
            attendance.Stage = Stage.OnScene;
            attendance.At = Mathf.Min(_deadline, now + StayTime(attendance.Police));
            _attended = true;
            if (attendance.Police)
            {
                _chase = Chase.None;   // too late for anyone to run
            }
            Logger?.Info($"Accident: the {Name(attendance)} parked {fromScene:F0} m from the scene ({how}"
                         + (attendance.MovedOn > 0 ? $", {attendance.MovedOn} car(s) moved out of its way" : "")
                         + (crew ? ")." : "); too far to walk, its crew stays aboard."));

            // A self-check: a unit must never stand in anything solid, a sign overhanging the road included.
            Collider touching = SceneParking.Standing(unit.Car, unit.Car.transform.position, unit.Car.transform.forward, 0f);
            if (touching != null && !_clearLogged)
            {
                _clearLogged = true;
                Logger?.Warn($"Accident: the {Name(attendance)} is parked touching '{touching.name}' (layer {LayerMask.LayerToName(touching.gameObject.layer)}).");
            }
            OnUnitParked(attendance, crew, now);
        }

        // The patrol car stays while the road is taped off and the ambulance is still working: someone has to hold the traffic.
        private bool MayLeave(Attendance attendance, float now)
        {
            return !attendance.Police || !_cordon.Up || _ambulance.Going || now >= _deadline - 10f;
        }

        // Where a unit will stand, tested before it sets off: its body and a margin against everything solid, and no crossing
        // under it. A place that fails moves back along the unit's own lane a step at a time, never sideways, and the route is
        // cut to end there. Said once if anything was in the way.
        private void SettleStand(Attendance attendance)
        {
            ResponseUnit unit = attendance.Unit;
            List<Vector3> path = unit.Path;
            VehicleComponent car = unit.Car;
            float halfLength = Mathf.Max(1.5f, car.length * 0.5f);
            string inWay = null;
            for (float back = 0f; back <= StandBackMost && path.Count >= 2; back += StandStep)
            {
                // The point so far back along the route from its end, and the way the lane runs there.
                int index = path.Count - 1;
                float left = back;
                Vector3 along = default;
                float leg = 0f;
                while (index > 0)
                {
                    along = path[index] - path[index - 1];
                    along.y = 0f;
                    leg = along.magnitude;
                    if (leg > 0.05f && leg >= left)
                    {
                        break;
                    }
                    left -= leg;
                    index--;
                }
                if (index <= 0)
                {
                    break;   // the route is no longer than that
                }
                along /= leg;
                Vector3 point = path[index] - along * left;
                Collider hit = SceneParking.Standing(car, point, along, StandMargin);
                bool crossing = hit == null && SceneParking.OnCrossing(point, along, halfLength);
                if (hit == null && !crossing)
                {
                    if (back > 0f)
                    {
                        path.RemoveRange(index, path.Count - index);
                        path.Add(point);
                        Logger?.Info(FormattableString.Invariant(
                            $"Accident: the place planned for the {Name(attendance)} failed its clear test ({inWay}); it stands {back:F1} m further back along its own lane instead."));
                    }
                    return;
                }
                if (inWay == null)
                {
                    inWay = crossing ? "it lies on a crossing"
                        : "'" + hit.name + "' on layer " + LayerMask.LayerToName(hit.gameObject.layer) + " within " + StandMargin.ToString("F1", CultureInfo.InvariantCulture) + " m of its body";
                }
            }
            if (inWay != null)
            {
                Logger?.Warn($"Accident: no place for the {Name(attendance)} within {StandBackMost:F0} m back along its lane passed the clear test ({inWay}); it stands as planned.");
            }
        }

        // The cones go out upstream of each end once the units are in; an end a unit still drives to waits for it.
        private void LayTaper()
        {
            if (!_cordon.Up || _conesIn)
            {
                return;
            }
            float near = 0f;
            float far = 0f;
            bool nearDue = false;
            bool farDue = false;
            for (int i = 0; i < 2; i++)
            {
                Attendance attendance = i == 0 ? _ambulance : _police;
                if (attendance.Stage == Stage.Waiting || attendance.Stage == Stage.EnRoute)
                {
                    nearDue |= !attendance.FarEnd;
                    farDue |= attendance.FarEnd;
                }
                else if ((attendance.Stage == Stage.OnScene || attendance.Stage == Stage.Ready) && attendance.Unit.Car != null)
                {
                    // Behind the ambulance the road stays free of cones longer: its crew brings the stretcher to the rear doors there.
                    TrafficTools.Footprint(attendance.Unit.Car, out Vector3 centre, out _, out float halfLength, out _);
                    _cordon.Reach(centre, halfLength + (attendance.Police ? 0f : RearDoorRoom), ref near, ref far);
                }
            }
            _cordon.LayTaper(nearDue ? -1f : near, farDue ? -1f : far);
        }

        private float StayTime(bool police)
        {
            if (police)
            {
                return _severity == Severity.Severe ? Random.Range(40f, 70f)
                    : _severity == Severity.Moderate ? Random.Range(25f, 50f)
                    : Random.Range(20f, 35f);
            }
            return _severity == Severity.Severe ? Random.Range(45f, 70f) : Random.Range(25f, 45f);
        }

        private static readonly int HotParked = HotSpots.Slot("Traffic: TrafficAccidents.OnUnitParked");
        /// <summary>A unit has parked: its crew gets out and walks in, however far short it stopped, and the police put the tape up.</summary>
        /// <param name="crew">False only if it stopped too far away for anyone to walk in.</param>
        private void OnUnitParked(Attendance attendance, bool crew, float now)
        {
            using var hotSpot = HotSpots.Time(HotParked);
            // No tape once the ambulance is loaded and leaving: it would cross the lane it drives out by.
            bool ambulanceGone = _ambulance.Stage == Stage.Ready || _ambulance.Stage == Stage.Leaving || _ambulance.Stage == Stage.Done;
            if (attendance.Police && _cordon.Closed && !_cordon.Up && !ambulanceGone)
            {
                _cordon.Raise(now);
                _people.OnCordonRaised(_cordon);
                Notice(NotificationType.Info, "reallife_accident_closed", 6f);
            }
            else if (attendance.Police)
            {
                Notice(NotificationType.Info, "reallife_accident_police", 6f);
            }
            else
            {
                Notice(NotificationType.Info, "reallife_accident_paramedics", 6f);
            }
            LayTaper();
            ParkedCheck();
            if (!crew)
            {
                return;
            }

            // The unit waits at least as long as its crew says its work takes, up to the scene's limit (Tick).
            float limit = !attendance.Police && (_people.PatientDown || (_casualty != null && _casualty.Present)) ? _hardLimit - 20f : _deadline;
            float busyUntil = _people.OnUnitParked(attendance.Police, attendance.Unit.Car, limit - 8f, now);
            attendance.At = Mathf.Min(limit - 6f, Mathf.Max(attendance.At, busyUntil));
        }

        private static readonly int HotDispatch = HotSpots.Slot("Traffic: TrafficAccidents.Dispatch");
        private void Dispatch(Attendance attendance, float now)
        {
            using var hotSpot = HotSpots.Time(HotDispatch);
            ResponseUnit unit = attendance.Unit;
            TrafficManager traffic = TrafficManager.Instance;

            // Where it goes: a patrol car that will chase comes up the suspect's own lane, so its route runs on into the chase.
            // Any other unit gets a place in a lane the wrecks block, in sight of the scene; failing that, the nearest road point.
            bool chasing = attendance.Police && _chase == Chase.Planned;
            bool ambulanceThere = _ambulance.Stage != Stage.Idle && _ambulance.Stage != Stage.Done;
            float back = !attendance.Police ? AmbulanceBack : attendance.FarEnd || !ambulanceThere ? PatrolBackAlone : PatrolBack;
            Waypoint anchor = null;
            Vector3 stand = default;
            bool hasStand = !chasing && _cordon.Stand(attendance.FarEnd, back, out anchor, out stand);
            if (!hasStand)
            {
                anchor = chasing ? _fleeAnchor : TrafficTools.ClosestWaypoint(_scene, 40f);
            }
            bool spawned = attendance.Police
                ? unit.Spawn(UnitKind.Police, anchor, Random.Range(140f, 200f), attendance.Retried ? 25f : 50f)
                : unit.Spawn(UnitKind.Ambulance, anchor, AmbulanceRouteLength, attendance.Retried ? 30f : 60f);   // a second try settles for a shorter run-in

            if (spawned)
            {
                attendance.AtStand = hasStand;
                if (hasStand)
                {
                    Vector3 beyond = stand - anchor.position;
                    beyond.y = 0f;
                    if (beyond.sqrMagnitude > 1f)
                    {
                        unit.Path.Add(stand);
                    }
                    SettleStand(attendance);   // in its lane and clear of everything solid, else further back along it
                    unit.Driver.BeginRoute(unit.Car, unit.Path, CrewWalk);
                }
                else
                {
                    unit.Driver.Begin(unit.Car, unit.Path, _scene, attendance.StopDistance, CrewWalk);
                }
                attendance.Stage = Stage.EnRoute;
                attendance.At = Mathf.Min(now + DriveAllowance, _hardLimit - 60f);
                Logger?.Info($"Accident: {unit.Car.prefab.name} dispatched as {unit.Kind}, {unit.Path.Count} road points, "
                             + (hasStand ? $"to stand {back:F0} m from the wrecks at the {(attendance.FarEnd ? "far" : "near")} end."
                                 : chasing ? "up the lane of the driver who will run."
                                 : "no lane to stand in here: it pulls up where it can."));
            }
            else if (!attendance.Retried)
            {
                // The spawn point may just have been occupied (often by the other unit): try once more on another route.
                attendance.Retried = true;
                attendance.At = now + 6f;
                Logger?.Info($"Accident: no {Name(attendance)} could be put on the road ({unit.Refusal}); trying once more, with a shorter run-in.");
            }
            else
            {
                Logger?.Info($"Accident: no {Name(attendance)} available ({unit.Refusal}); the scene runs without one.");
                Withdraw(attendance);
            }
        }

        // The unit is not coming, or is gone. Without a patrol car there is nobody to run from.
        private void Withdraw(Attendance attendance)
        {
            ReleaseExit(attendance);
            _people.OnUnitLeft(attendance.Police);
            SceneObstacles.Remove(attendance.Unit.Car);
            attendance.Unit.Release(resumeTraffic: true, remove: false);
            attendance.Stage = Stage.Done;
            if (attendance.Police && _chase == Chase.Planned)
            {
                _chase = Chase.None;
            }
            LayTaper();   // an end it was due at need not be kept clear for it any more
        }
    }
}
