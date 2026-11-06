using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus's part for being held up on its way: standing where it is at its stop, a car told to go on, the hold in the log.</summary>
    public sealed partial class TransitBus
    {
        private const float StillSpeed = 0.3f;       // m/s under which it is standing still
        private const float CarClear = 6f;           // metres beyond a car's nose, and
        private const float CarHalfWay = 1.5f;       // either side of its middle, that are the car's own way ahead (RideRules.Yield)
        private static int _playerLayer = -2;

        // On the road, four times a second: how long it has stood still. Held up near its stop with a door by the kerb (or by the
        // player near its place) it is at the stop; a car that waits for it is told it is not in its way; a hold of more than a
        // few seconds goes in the log once. A few comparisons while it moves.
        private void OnTheWay(float now)
        {
            if ((State != Phase.Approaching && State != Phase.Leaving) || Vehicle == null)
            {
                _hold.Clear();
                return;
            }
            Vector3 at = Vehicle.transform.position;
            float still = _hold.At(at.x, at.z, now);
            if (still < RideRules.StandShortAfter || (_driver.Active && _driver.Speed >= StillSpeed))
            {
                return;
            }
            Collider by = _driver.HeldBy;
            if (State == Phase.Approaching && !_startHold.Holds(now) && Stop != null)
            {
                bool player = by != null && IsPlayer(by);
                bool door = DoorOnKerb();
                if (RideRules.StandsWhereItIs(still, door, player, _driver.PathLeft))
                {
                    string why = "held up " + _driver.PathLeft.ToString("F1", CultureInfo.InvariantCulture) + " m of its route short of its place by " + WaitsFor() + "; "
                                 + (door ? "a door of it by the stop's kerb" : "the player in front of it near its place") + ", it stands there";
                    StandAtStop(now, why);
                    return;
                }
            }
            if (by != null && Waiting == Hold.Traffic && !_hold.Yielded && still >= RideRules.YieldAfter)
            {
                YieldTo(by, still);
            }
            if (!_hold.Rounded && Round(now, still, by))
            {
                return;   // on its way round what held it
            }
            if (!_hold.Logged && still >= RideRules.HoldLogAfter)
            {
                _hold.Logged = true;
                Logger?.Info("Transit check (hold): " + HoldWords(still) + ".");
            }
        }

        // True when a lit emergency vehicle is coming up behind it, in its lane or the next (EmergencyDriver.UnitComingUp).
        private bool UnitBehind()
        {
            if (Vehicle == null)
            {
                return false;
            }
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            return EmergencyDriver.UnitComingUp(centre, forward);
        }

        /// <summary>True while it stands at its stop in the lane, with no bay to stand in, and a lit emergency vehicle is coming up behind it: it should stand clear.</summary>
        public bool StandsInTheWay()
        {
            return State == Phase.Standing && MakeWayRules.BusStandsClear(_bay, UnitBehind());
        }

        // Its hold at the start let go because the player can see it (RideRules.StartHold): it drives on from this tick, what
        // it stood is what counts as held, and the director is told how early it now is. One line in the log.
        private void HoldLetGo()
        {
            float cut = _startHold.Cut;
            Held0 = Mathf.Max(0f, Held0 - cut);
            _holdCutTold = true;
            Logger?.Info("Transit check (start hold): the " + (_line != null ? _line.Number : "bus") + ", held at its start out of sight to keep the timetable, can be seen "
                         + Mathf.Sqrt(CitySpots.Flat(Vehicle.transform.position - PlayerHelper.GetPosition())).ToString("F0") + " m from the player after standing "
                         + Held0.ToString("F1", CultureInfo.InvariantCulture) + " s: the last " + cut.ToString("F1", CultureInfo.InvariantCulture)
                         + " s of the hold are let go and it drives on now (looked at every " + RideRules.StartLookEvery.ToString("F1", CultureInfo.InvariantCulture)
                         + " s while it holds), early at its stop by that much: PASS.");
        }

        // A door of it by the stop's kerb, as it stands now.
        private bool DoorOnKerb()
        {
            DoorsAlong(out float front, out float middle, out float aside, out float angle);
            return RideRules.DoorOnKerb(front, middle, aside, angle, Stop.Stand, Stop.KerbAside);
        }

        // Its doors' middles along the stop's lane from its lane point, the side they are in off the lane's line (towards the kerb
        // positive), and its angle to the lane.
        private void DoorsAlong(out float front, out float middle, out float aside, out float angle)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * BusVehicle.HalfWidth;
            Vector3 frontDoor = centre + forward * BusVehicle.FrontDoorAhead + side - Stop.Lane;
            Vector3 middleDoor = centre + forward * BusVehicle.MiddleDoorAhead + side - Stop.Lane;
            front = frontDoor.x * Stop.Along.x + frontDoor.z * Stop.Along.z;
            middle = middleDoor.x * Stop.Along.x + middleDoor.z * Stop.Along.z;
            aside = ((frontDoor.x + middleDoor.x) * right.x + (frontDoor.z + middleDoor.z) * right.z) * 0.5f;
            angle = Vector3.Angle(forward, Stop.Along);
        }

        private static bool IsPlayer(Collider hit)
        {
            if (_playerLayer == -2)
            {
                _playerLayer = LayerMask.NameToLayer("Player");
            }
            if (_playerLayer >= 0 && hit.gameObject.layer == _playerLayer)
            {
                return true;
            }
            PlayerController player = PlayerHelper.PlayerController;
            return player != null && hit.transform.root == player.transform.root;
        }

        // Standing behind a car of the traffic that stands for the bus, the bus not on the car's own way ahead: the car is told the
        // bus is not in its way, as the game tells its cars of a pedestrian put away (VehicleComponent.ColliderRemoved). Only this
        // machine's own traffic (TrafficTools.Ready), nothing else of the car touched, once a hold.
        private void YieldTo(Collider by, float still)
        {
            Rigidbody body = by.attachedRigidbody;
            VehicleComponent car = body != null ? body.GetComponent<VehicleComponent>() : null;
            Collider solid = Vehicle.Solid;
            if (car == null || solid == null)
            {
                return;
            }
            bool standing = body.velocity.sqrMagnitude < 0.25f;
            bool waits = WaitsForBus(car);
            bool onItsWay = OnCarsWay(car, body);
            if (!RideRules.Yield(still, TrafficTools.Ready, standing, waits, onItsWay, _hold.Yielded))
            {
                return;
            }
            _hold.Yielded = true;
            SpecialDriveActionTypes was = car.GetCurrentAction();
            car.ColliderRemoved(solid);
            Logger?.Info("Transit check (hold): the " + (_line != null ? _line.Number : "bus") + " had stood " + still.ToString("F0") + " s behind '" + TrafficTools.Name(car)
                         + "', a car of the traffic that stood for the bus (" + was
                         + ", the bus in its sensor), and is not on its way ahead: the car is told the bus is not in its way and "
                         + (WaitsForBus(car) ? "still has it in its sensor: FAIL" : "drives on when nothing else holds it: PASS") + ".");
        }

        // True when the bus is in the car's sensor, the game's own list of what it stands for.
        private bool WaitsForBus(VehicleComponent car)
        {
            FieldInfo sensor = GameContract.CarObstacles.Field;   // VehicleComponent.obstacleList: read once a hold at most
            Collider solid = Vehicle != null ? Vehicle.Solid : null;
            return solid != null && sensor != null && sensor.GetValue(car) is List<Collider> seen && seen.Contains(solid);
        }

        // True when the bus is on the car's own way ahead of it: told to go on, it would drive into the bus.
        private bool OnCarsWay(VehicleComponent car, Rigidbody body)
        {
            Vector3 forward = car.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return true;
            }
            forward.Normalize();
            Vector3 centre = body.worldCenterOfMass;
            Vector3 nearest = Vehicle.Solid.ClosestPoint(centre) - centre;
            nearest.y = 0f;
            float ahead = Vector3.Dot(nearest, forward);
            float aside = Vector3.Dot(nearest, Vector3.Cross(Vector3.up, forward));
            return ahead > 0f && ahead < Mathf.Max(car.length, 4f) * 0.5f + CarClear && Mathf.Abs(aside) < CarHalfWay;
        }

        /// <summary>What it is standing for now, in words for the log.</summary>
        public string WaitsFor()
        {
            if (State == Phase.Approaching && _startHold.Holds(Time.time))
            {
                return "nothing on the road: it is held at its start, out of sight, to keep the timetable (" + HoldLeft.ToString("F0") + " s more)";
            }
            if (_awaitGap)
            {
                return "a gap in the lane to pull out of its bay into";
            }
            if (State == Phase.Leaving && Vehicle != null && !Vehicle.DoorsShut)
            {
                return "its doors to shut";
            }
            if (_gate >= 0 && !_driver.Active)
            {
                return _gateWhy == Hold.Light ? "a red light at the junction ahead"
                    : _gateWhy == Hold.Closure ? "a road ahead that a scene has closed" : "the place it was told to pull up at";
            }
            Collider by = _driver.HeldBy;
            if (by != null)
            {
                return Describe(by) + " within " + (_driver.HeldAhead + 3f).ToString("F0") + " m of its nose";
            }
            return _driver.Active
                ? "nothing in its way (its driver's pace " + _driver.CruiseSpeed.ToString("F1", CultureInfo.InvariantCulture) + " m/s, at "
                  + _driver.Speed.ToString("F1", CultureInfo.InvariantCulture) + ")"
                : "nothing: its driver has stopped";
        }

        // What a collider in its way is, for the log.
        private string Describe(Collider hit)
        {
            Rigidbody body = hit.attachedRigidbody;
            string name = (body != null ? body.gameObject : hit.gameObject).name;
            string layer = LayerMask.LayerToName(hit.gameObject.layer);
            VehicleComponent car = body != null ? body.GetComponent<VehicleComponent>() : null;
            if (car != null)
            {
                float speed = body.velocity.magnitude;
                return "a car of the traffic ('" + TrafficTools.Name(car) + "', " + (speed < 0.5f ? "standing" : "at " + (speed * 3.6f).ToString("F0") + " km/h") + ", its driver: "
                       + car.GetCurrentAction() + ", " + (WaitsForBus(car) ? "the bus in its sensor: it stands for the bus" : "the bus not in its sensor") + ")";
            }
            if (IsPlayer(hit))
            {
                return "the player, in the road in front of it";
            }
            if (body != null && body.GetComponent<BusVehicle>() != null)
            {
                return "another of the mod's buses ('" + name + "')";
            }
            switch (layer)
            {
                case "Human": return "somebody on foot in the road ('" + name + "')";
                case "ParkedVehicles": return "a parked car reaching into the lane ('" + name + "')";
                case "PlayerVehicles": return "a player's car ('" + name + "')";
                default: return "'" + name + "' on the layer " + layer;
            }
        }

        // The hold in words for the log: where it is against its place, what holds it, who can see it.
        private string HoldWords(float still)
        {
            string line = _line != null ? _line.Number : "bus";
            string label = Stop != null ? TransitDirector.StopLabel(Stop) : "its stop";
            string where;
            if (State == Phase.Approaching && Stop != null)
            {
                Measure(out float along, out float aside, out float angle, out _, out _);
                bool door = DoorOnKerb();
                where = "on its way to " + label + ", " + _driver.PathLeft.ToString("F1", CultureInfo.InvariantCulture) + " m of its route from where it is to stand (its middle "
                        + Mathf.Abs(along).ToString("F1", CultureInfo.InvariantCulture) + " m " + (along >= 0f ? "past" : "short of") + " its place, "
                        + Mathf.Abs(aside).ToString("F1", CultureInfo.InvariantCulture) + " m " + (aside >= 0f ? "towards the kerb" : "away from the kerb")
                        + " off the lane's line, " + angle.ToString("F0") + " degrees off its direction; " + (door ? "a door by the stop's kerb" : "no door by the stop's kerb") + ")";
            }
            else
            {
                float from = Stop != null ? Mathf.Sqrt(CitySpots.Flat(Vehicle.transform.position - Stop.Lane)) : 0f;
                where = (_goingOut ? "driving away out of sight from " : "leaving ") + label + ", " + from.ToString("F0") + " m from where it stood there";
            }
            return "the " + line + " has stood still for " + still.ToString("F0") + " s " + where + "; it waits for " + WaitsFor() + "; seen: " + (InView ? "yes" : "no")
                   + ", " + Mathf.Sqrt(CitySpots.Flat(Vehicle.transform.position - PlayerHelper.GetPosition())).ToString("F0") + " m from the player";
        }
    }
}
