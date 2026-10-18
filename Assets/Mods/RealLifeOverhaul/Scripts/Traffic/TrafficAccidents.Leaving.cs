using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the units leaving, the way out kept clear, and the departure's self-check.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float ClearInView = 5f;           // seconds a wreck in a loaded ambulance's lane is given to be out of view before it is taken off anyway
        private const float MovingOff = 1.5f;           // m/s: faster than this a unit is moving along its lane, and traffic may have it back
        private const float ExitMargin = 0.35f;         // metres each side of its body that the lane it leaves by must be clear
        private const float ExitPace = 7f;              // m/s through where the scene is
        private const float LaneAcross = 3.3f;          // metres: one lane sideways
        private const float TapeDownFor = 4f;           // seconds (at least) between the tape coming down and the scene clearing, the patrol car driving off with it
        private const float LeaveDistance = 55f;        // metres a departing unit is driven this far from the scene before traffic has it back
        private const float LeaveAllowance = 22f;       // ...or for this many seconds
        private const float BoxedIn = 20f;              // seconds a unit may stand with its crew aboard and free to go before it is taken off the road instead
        private const float BoxedInByPeople = 45f;      // ...or behind a person, who will move (it never forces through)
        private const float BackOffMost = 16f;          // metres a patrol car backs along its own lane to give the ambulance the road
        private const float BackOffPace = 3f;           // ...at this many m/s
        private const float NearPlayer = 45f;           // metres: a vehicle this near the camera is never taken off the road
        private const float PassedBy = 7f;          // metres beyond the far end of the tape at which it has passed the scene

        // Somebody could be looking: in the camera's view, or near it (the camera turns faster than a car goes).
        private static bool Seen(Vector3 place)
        {
            if (StreetSpots.IsInView(place))
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            return camera != null && (camera.transform.position - place).sqrMagnitude < NearPlayer * NearPlayer;
        }

        // A leaving unit has stopped behind the other unit: that is never left to time. The ambulance has the road: a patrol
        // car in its way drives off ahead of it, or backs along its own lane if nose to nose (its officers called aboard
        // first); a patrol car behind the ambulance waits. True if the other unit is in the way and something is done.
        private bool GiveWay(Attendance attendance, float now)
        {
            Attendance other = attendance.Police ? _ambulance : _police;
            ResponseUnit unit = attendance.Unit;
            Rigidbody blocker = unit.Driver.Blocker;
            if (blocker == null || !other.Unit.InUse || other.Unit.Car == null || other.Unit.Car.rb != blocker || unit.Driver.HeldFor < 1f)
            {
                return false;
            }
            if (attendance.Police)
            {
                // Nose to nose with the leaving ambulance: the patrol car is the one that backs.
                if (other.Stage == Stage.Leaving && other.Unit.Driver.Blocker == unit.Car.rb)
                {
                    BackOff(attendance, now, wasWaiting: false);
                }
                return true;
            }
            attendance.GaveWay++;
            switch (other.Stage)
            {
                case Stage.OnScene:
                    if (!other.Recalled)
                    {
                        other.At = Mathf.Min(other.At, now);
                        Logger?.Info("Accident: the patrol car stands in the loaded ambulance's way with its officers out; they are called aboard at once.");
                    }
                    return true;

                case Stage.Ready:
                    if (other.Backing)
                    {
                        return true;
                    }
                    bool noseToNose = Vector3.Dot(other.Unit.Car.transform.forward, unit.Car.transform.forward) < 0.2f;
                    if (noseToNose)
                    {
                        BackOff(other, now, other.Waits);
                    }
                    else if (!Leave(other, now))
                    {
                        BackOff(other, now, other.Waits);
                    }
                    return true;

                case Stage.Leaving:
                    return true;   // on its way already: the ambulance follows it out
            }
            return false;
        }

        // Backs along its own lane out of the ambulance's way, driven like any route: it stops short of whatever is behind.
        private void BackOff(Attendance attendance, float now, bool wasWaiting)
        {
            ResponseUnit unit = attendance.Unit;
            VehicleComponent car = unit.Car;
            Vector3 at = car.transform.position;
            Vector3 facing = car.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f || !TrafficTools.Ready)
            {
                return;
            }
            facing.Normalize();
            float tail = Mathf.Max(1.5f, car.length * 0.5f);
            Waypoint behind = TrafficTools.ClosestWaypointAlong(at - facing * (tail + 2f), facing * 2f, 12f);
            bool found = behind != null && TrafficTools.WalkBack(behind, BackOffMost + 8f, 24, _routeScratch) > 6f;
            unit.Path.Clear();
            for (int i = _routeScratch.Count - 1; i >= 0 && found; i--)
            {
                Vector3 point = _routeScratch[i].position;
                if (Vector3.Dot(point - at, facing) < -tail)
                {
                    unit.Path.Add(point);
                }
            }
            _routeScratch.Clear();
            if (unit.Path.Count == 0)
            {
                // No road point behind it to back along: straight back along the way it stands, which is the lane it drove up.
                unit.Path.Add(at - facing * (tail + BackOffMost * 0.5f));
                unit.Path.Add(at - facing * (tail + BackOffMost));
            }
            SceneObstacles.Remove(car);
            unit.Unpark(quiet: true);
            unit.Driver.BeginReverse(car, unit.Path, BackOffPace);
            attendance.Stage = Stage.Ready;
            attendance.Backing = true;
            attendance.BackFrom = at;
            attendance.BackUntil = now + 12f;
            attendance.WaitedBefore = wasWaiting;
            attendance.Waits = false;   // not finished while it moves: the scene does not clear round a backing unit
            attendance.Still = 0f;
            TrafficWatch.Guard(car, Name(attendance));
            Logger?.Info($"Accident: the {Name(attendance)} stands nose to nose with the ambulance that is leaving; it backs along its own lane, up to {BackOffMost:F0} m, to give it the road.");
        }

        // Backing: until it has gone far enough, has come up to something behind it, or the ambulance is past. Then it stands
        // again (waiting for the scene to clear, as before) or, with the ambulance gone, leaves forwards itself.
        private void TickBacking(Attendance attendance, float now)
        {
            ResponseUnit unit = attendance.Unit;
            EmergencyDriver driver = unit.Driver;
            float backed = Vector3.Distance(unit.Car.transform.position, attendance.BackFrom);
            Attendance other = attendance.Police ? _ambulance : _police;
            bool done = !driver.Active || driver.Arrived || backed >= BackOffMost || driver.HeldFor > 1.5f || now >= attendance.BackUntil || other.Past;
            if (!done)
            {
                return;
            }
            attendance.Backing = false;
            // It waits for the scene to clear again if it did before and a scene remains; else it tries to leave every second.
            Stand(attendance, now, waits: attendance.WaitedBefore && _phase != Phase.None);
            SceneObstacles.Add(unit.Car, transform);
            Logger?.Info($"Accident: the {Name(attendance)} has backed {backed:F0} m along its lane and stands there until the ambulance is past.");
        }

        // The crew is aboard (or out of time): a unit now leaves forwards within seconds, handed back to traffic only once it
        // moves along a lane beyond the scene. The ambulance goes first and never reverses; the patrol car goes last.
        private void Depart(Attendance attendance, float now)
        {
            _cordon.LiftTaper();   // the cones are the first thing taken in
            _conesIn = true;
            _people.OnUnitLeft(attendance.Police);
            attendance.Still = 0f;
            bool sceneAhead = attendance.AtStand && (_cordon.Up || _wrecks.Count > 0);
            if (attendance.Police)
            {
                if (sceneAhead)
                {
                    Stand(attendance, now, waits: true);
                    if (_ambulance.Going)
                    {
                        // The ambulance is done: the tape comes down now, and the scene clears a few seconds later.
                        _cordon.Lower();
                        _lingerUntil = Mathf.Max(_lingerUntil, now + Random.Range(TapeDownFor, TapeDownFor + 3f));
                    }
                    Logger?.Info("Accident: the officers are aboard; the patrol car drives off through the scene as soon as it is cleared.");
                }
                else if (!Leave(attendance, now))
                {
                    Stand(attendance, now, waits: false);
                }
                return;
            }

            CollectCasualty();
            _people.StandAside(now);
            if (_police.Stage == Stage.OnScene && !_police.Recalled)
            {
                _police.At = Mathf.Min(_police.At, now);   // nothing is left to guard: the officers pack up as it goes
            }
            _exitWrecks.Clear();
            if (sceneAhead)
            {
                _cordon.Lower();
                PlanExit(attendance);
            }
            Stand(attendance, now, waits: false);
            attendance.At = now + ClearInView;
            TryToGo(attendance, now);
        }

        // Which way a loaded ambulance gets past the scene in front of it, said once in the log.
        private void PlanExit(Attendance attendance)
        {
            VehicleComponent car = attendance.Unit.Car;
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float reach = Mathf.Clamp(_cordon.Through(centre, forward) + 6f, halfLength + 8f, 70f);
            Collider own = SceneParking.InStrip(centre + forward * (halfLength + 0.3f), centre + forward * reach, halfWidth + ExitMargin, car.rb);
            attendance.OwnLane = true;   // unless planned round the scene by the lane beside, below
            string way;
            if (own == null)
            {
                way = "straight on through the scene by its own lane, which is clear";
            }
            else
            {
                float side = 0f;
                for (int i = 0; i < 2 && side == 0f && TrafficManager.HasInstance; i++)
                {
                    float tried = i == 0 ? -1f : 1f;   // the left first: in most streets the lane running the other way
                    Vector3 over = right * (tried * LaneAcross);
                    if (TrafficTools.ClosestWaypoint(centre + forward * (halfLength + 6f) + over, 1.7f) != null
                        && SceneParking.InStrip(centre - forward * halfLength + over, centre + forward * reach + over, halfWidth + ExitMargin, car.rb) == null)
                    {
                        side = tried;
                    }
                }
                if (side != 0f)
                {
                    attendance.OwnLane = false;   // it pulls out into that lane to get by: the one case where it may
                    _cordon.HoldGateShut();   // nothing more is let past the scene into that lane until it is through
                    way = "round the scene by the lane on its " + (side < 0f ? "left" : "right") + ", which is clear and held empty for it";
                }
                else
                {
                    for (int i = 0; i < _wrecks.Count; i++)
                    {
                        if (InLane(_wrecks[i], centre, forward, right, halfLength, halfWidth + ExitMargin, reach))
                        {
                            _exitWrecks.Add(_wrecks[i]);
                        }
                    }
                    way = _exitWrecks.Count > 0
                        ? _exitWrecks.Count + " vehicle(s) held at the scene stand in its lane and no lane beside it is clear: those are taken off the road first"
                          + " (out of view at once, in view after " + ClearInView.ToString("F0") + " s at most)"
                        : "'" + own.name + "' (layer " + LayerMask.LayerToName(own.gameObject.layer) + ") is in its lane: it goes as far as it can and passes where a lane allows";
                }
            }
            Logger?.Info("Accident: the ambulance is loaded; tape taken in, and it leaves forwards, siren on: " + way + ".");
        }

        // True if a held vehicle lies across the lane ahead of a unit: its real footprint against the strip the unit will drive.
        private static bool InLane(Wreck wreck, Vector3 from, Vector3 forward, Vector3 right, float halfLength, float halfWidth, float reach)
        {
            if (wreck == null || wreck.Car == null)
            {
                return false;
            }
            TrafficTools.Footprint(wreck.Car, out Vector3 centre, out Vector3 along, out float length, out float width);
            Vector3 across = Vector3.Cross(Vector3.up, along);
            Vector3 offset = centre - from;
            float ahead = Vector3.Dot(offset, forward);
            float aside = Mathf.Abs(Vector3.Dot(offset, right));
            float deep = Mathf.Abs(Vector3.Dot(along, forward)) * length + Mathf.Abs(Vector3.Dot(across, forward)) * width;
            float wide = Mathf.Abs(Vector3.Dot(along, right)) * length + Mathf.Abs(Vector3.Dot(across, right)) * width;
            return ahead + deep > halfLength && ahead - deep < reach && aside < halfWidth + wide;
        }

        // Standing with its crew aboard: parked (kinematic, so nothing bounces), lit, and watched. With 'waits' it goes
        // when the scene clears round it; without, it tries to go every second and is never left there (BoxedOut).
        private void Stand(Attendance attendance, float now, bool waits)
        {
            ResponseUnit unit = attendance.Unit;
            unit.Driver.Stop();
            unit.Park(keepSiren: !waits && !attendance.Police);
            attendance.Stage = Stage.Ready;
            attendance.Waits = waits;
            attendance.Through = false;
            attendance.NextCheck = 0f;
            if (attendance.Still <= 0f)
            {
                attendance.Still = now;
            }
            if (!waits)
            {
                TrafficWatch.Guard(unit.Car, Name(attendance));
            }
        }

        // A unit ready to go: the wrecks in its lane go first (each once nobody looks that way, or when its time is up), then
        // it leaves. One that still cannot go is not left standing.
        private void TryToGo(Attendance attendance, float now)
        {
            bool pending = false;
            for (int i = _exitWrecks.Count - 1; i >= 0 && !attendance.Police; i--)
            {
                Wreck wreck = _exitWrecks[i];
                if (wreck.Car == null || !_wrecks.Contains(wreck))
                {
                    _exitWrecks.RemoveAt(i);
                }
                else if (now >= attendance.At || !StreetSpots.IsInView(wreck.Car.transform.position))
                {
                    Tow(wreck, "towed out of the loaded ambulance's lane");
                }
                else
                {
                    pending = true;
                }
            }
            if (pending || Leave(attendance, now))
            {
                return;
            }
            if (now - attendance.Still > BoxedIn)
            {
                BoxedOut(attendance, now, "there is no lane of road ahead of it to leave by");
            }
        }

        // Drives a standing unit away along the lane ahead; false (nothing changed) if there is none. Leaving hands it back.
        private bool Leave(Attendance attendance, float now)
        {
            ResponseUnit unit = attendance.Unit;
            VehicleComponent car = unit.Car;
            Vector3 at = car.transform.position;
            Vector3 facing = car.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f || !TrafficTools.Ready)
            {
                return false;
            }
            facing.Normalize();
            float nose = Mathf.Max(1.5f, car.length * 0.5f) + 1f;
            // The direction is given long: the search weighs a lane's direction only for one longer than a metre.
            Waypoint ahead = TrafficTools.ClosestWaypointAlong(at + facing * (nose + 3f), facing * 2f, 30f);
            bool found = ahead != null && TrafficTools.WalkForward(ahead, LeaveDistance + 40f, 60, _routeScratch) > 20f;
            if (found)
            {
                unit.Path.Clear();
                for (int i = 0; i < _routeScratch.Count; i++)
                {
                    Vector3 point = _routeScratch[i].position;
                    if (unit.Path.Count > 0 || Vector3.Dot(point - at, facing) > nose)
                    {
                        unit.Path.Add(point);
                    }
                }
                found = unit.Path.Count >= 2;
            }
            _routeScratch.Clear();
            if (!found)
            {
                return false;
            }
            SceneObstacles.Remove(car);
            unit.Unpark(quiet: attendance.Police);
            unit.Driver.Begin(car, unit.Path, unit.Path[unit.Path.Count - 1], 6f);
            float through = _cordon.Surveyed ? _cordon.Through(at, facing) : 0f;
            attendance.Through = through > 0f && through < 90f;
            unit.Driver.CruiseSpeed = attendance.Through ? ExitPace : EmergencyDriver.DefaultCruiseSpeed;
            // Through the scene it keeps to the lane checked for it and waits behind whatever turns up, never pulling out
            // beside, where the other unit stands; only an ambulance planned round the wrecks by the lane beside them may.
            unit.Driver.MayPass = !attendance.Through || (!attendance.Police && !attendance.OwnLane);
            attendance.Stage = Stage.Leaving;
            attendance.Waits = false;
            attendance.Backing = false;
            attendance.From = at;
            attendance.NextCheck = 0f;
            attendance.SetOffAt = now;
            attendance.At = now + LeaveAllowance + (attendance.Through ? 10f : 0f);
            TrafficWatch.Guard(car, Name(attendance));
            Logger?.Info($"Accident: the {Name(attendance)} is leaving, forwards along its lane"
                         + (attendance.Through ? $" ({through:F0} m to the far side of the scene, at {ExitPace * 3.6f:F0} km/h until it is past)." : "."));
            SetOffCheck(attendance, at, facing, through);
            return true;
        }

        // A leaving unit stopped behind something: a vehicle held at the scene is towed out of its way, standing ordinary
        // traffic is moved on (at once out of view, after a few seconds in view). A person, or anything else, is waited for.
        private void ClearExit(Attendance attendance, EmergencyDriver driver)
        {
            float held = driver.HeldFor;
            Rigidbody body = driver.Blocker;
            if (held < HeldBeforeMoving || body == null)
            {
                return;
            }
            VehicleComponent car = body.GetComponentInParent<VehicleComponent>();
            if (car == null || (held < HeldBeforeMovingSeen && StreetSpots.IsInView(car.transform.position)))
            {
                return;
            }
            for (int i = 0; i < _wrecks.Count; i++)
            {
                if (_wrecks[i].Car == car)
                {
                    Tow(_wrecks[i], "towed out of the way of the " + Name(attendance) + " as it leaves");
                    return;
                }
            }
            if (IsUsable(car) && car.GetCurrentSpeed() <= 1f && attendance.MovedOn < 12)
            {
                attendance.MovedOn++;
                TrafficTools.Remove(car, "standing in the way of a response unit that is leaving");
            }
        }

        // Takes one held vehicle off the road while the scene goes on (TrafficTools.Remove says so in the log).
        private void Tow(Wreck wreck, string why)
        {
            _wrecks.Remove(wreck);
            _exitWrecks.Remove(wreck);
            VehicleComponent car = wreck.Car;
            Unpin(wreck);
            if (car != null)
            {
                _involved.Remove(car.rb);
                if (TrafficTools.IsHeld(car))
                {
                    TrafficTools.Remove(car, "the accident scene: " + why);
                }
                _released.Add(car);
            }
            _leadWreck = _leadWreck == wreck ? null : _leadWreck;
            _worstWreck = _worstWreck == wreck ? null : _worstWreck;
            _otherWreck = _otherWreck == wreck ? null : _otherWreck;
        }

        // A unit with its crew aboard that has stood too long and cannot get away is taken off the road, but only out of view.
        private void BoxedOut(Attendance attendance, float now, string why)
        {
            ResponseUnit unit = attendance.Unit;
            bool seen = unit.Car != null && Seen(unit.Car.transform.position);
            if (seen)
            {
                // Never in front of the player: it stays, lit, and keeps trying to drive off (GiveWay, ClearExit) until nobody
                // can see it. Said once.
                if (!attendance.SaidHeld)
                {
                    attendance.SaidHeld = true;
                    DepartureCheck(Name(attendance) + " held up", false, $"it has stood {now - attendance.Still:F0} s with its crew aboard and cannot get away ({why}); "
                        + "it is in view, so it is not taken off the road: it keeps trying to drive off, and goes only when nobody can see it");
                }
                return;
            }
            Logger?.Warn($"Accident: the {Name(attendance)} stood {now - attendance.Still:F0} s with its crew aboard and could not get away ({why}); "
                         + "it is taken off the road out of view.");
            ReleaseExit(attendance);
            SceneObstacles.Remove(unit.Car);
            unit.Release(resumeTraffic: true, remove: true);
            attendance.Stage = Stage.Done;
        }

        /// <summary>For TrafficWatch: a unit of this scene has stood too long with its crew aboard; it goes as a boxed-in unit does.</summary>
        public void Unstick(VehicleComponent car)
        {
            Attendance attendance = _ambulance.Unit.Car == car ? _ambulance : _police.Unit.Car == car ? _police : null;
            if (attendance == null || !attendance.Unit.InUse || (attendance.Stage == Stage.Ready && attendance.Waits && _phase != Phase.None)
                || (attendance.Stage != Stage.Ready && attendance.Stage != Stage.Leaving))
            {
                return;   // not ours, at work, or the patrol car waiting its few seconds for the scene to clear
            }
            if (attendance.Still <= 0f)
            {
                attendance.Still = Time.time - BoxedIn;
            }
            BoxedOut(attendance, Time.time, "the traffic watch found it standing");
        }

        private static readonly int HotDepart = HotSpots.Slot("Traffic: TrafficAccidents.TickDepartures");
        // After a scene has ended, a unit still driving away is carried on until traffic has it back or it is removed.
        private void TickDepartures(float now)
        {
            using var hotSpot = HotSpots.Time(HotDepart);
            try
            {
                if (!TrafficTools.Ready)
                {
                    EndDepartures();
                    return;
                }
                for (int i = 0; i < 2; i++)
                {
                    Attendance attendance = i == 0 ? _ambulance : _police;
                    if (!attendance.Unit.InUse)
                    {
                        continue;
                    }
                    if (attendance.Stage == Stage.Ready || attendance.Stage == Stage.Leaving)
                    {
                        TickAttendance(attendance, now);
                    }
                    else
                    {
                        attendance.Unit.Release(resumeTraffic: true, remove: true);
                        attendance.Stage = Stage.Idle;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                EndDepartures();
            }
        }

        // A unit still out from a scene that is over: back to traffic if it moves along its lane, off the road if it stands.
        private void EndDepartures()
        {
            for (int i = 0; i < 2; i++)
            {
                Attendance attendance = i == 0 ? _ambulance : _police;
                ResponseUnit unit = attendance.Unit;
                if (!unit.InUse)
                {
                    continue;
                }
                bool moving = attendance.Stage == Stage.Leaving && unit.Driver.Active && unit.Driver.Speed > MovingOff;
                _cordon.ReleaseExit();
                unit.Release(TrafficTools.Ready, remove: !moving, quiet: true);
                attendance.Stage = Stage.Idle;
            }
        }

        // The scene is over: a unit still out that is moving goes back to traffic or carries on leaving. One standing is never
        // handed back like that: in view it drives off along its lane and is handed back once moving; otherwise it is removed.
        private void SendOff(Attendance attendance, bool resumeTraffic, bool drive)
        {
            ResponseUnit unit = attendance.Unit;
            if (!unit.InUse)
            {
                attendance.Stage = Stage.Idle;
                return;
            }
            drive &= resumeTraffic && !_closing && TrafficTools.Ready && !unit.IsLost;
            if (attendance.Backing)
            {
                attendance.Backing = false;
                unit.Driver.Stop();
            }
            if (drive && attendance.Stage == Stage.Leaving)
            {
                return;   // on its way already: TickDepartures sees it off
            }
            if (drive && unit.Driver.Active && unit.Driver.Speed > MovingOff)
            {
                unit.Release(resumeTraffic: true, remove: false, quiet: true);   // still on its way in, and moving
                attendance.Stage = Stage.Idle;
                return;
            }
            if (drive && Seen(unit.Car.transform.position))
            {
                try
                {
                    float now = Time.time;
                    attendance.Still = 0f;
                    attendance.OwnLane = true;
                    if (!Leave(attendance, now))
                    {
                        Stand(attendance, now, waits: false);
                    }
                    return;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            unit.Release(resumeTraffic, remove: true, quiet: true);
            attendance.Stage = Stage.Idle;
        }

        private void ReleaseExit(Attendance attendance)
        {
            if (!attendance.Police)
            {
                _cordon.ReleaseExit();
            }
        }
    }
}
