using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The city's drivers: one look at the street, the cars gathered, each car's road read and what binds it on that road.</summary>
    public sealed partial class CityDrivers : MonoBehaviour
    {
        private const float Level = 3f;              // metres up or down within which two things are on the same road
        private const float SampleEvery = 10f;       // seconds: the braking self-check speaks for one car in this long at the most

        // The cars the traffic system drives near the player, this look.
        private readonly VehicleComponent[] _car = new VehicleComponent[DriverRules.MostCars];
        private readonly int[] _index = new int[DriverRules.MostCars];
        private readonly Vector3[] _nose = new Vector3[DriverRules.MostCars];
        private readonly Vector3[] _forward = new Vector3[DriverRules.MostCars];
        private readonly float[] _speed = new float[DriverRules.MostCars];
        private int _cars;

        // What is remembered of each pooled car from look to look, by its place in the pool.
        private bool[] _slowing = new bool[0];
        private DriverRules.Why[] _slowedFor = new DriverRules.Why[0];
        private byte[] _said = new byte[0];          // self-check lines said for this car's current stop (flags)
        private float[] _lastAlong = new float[0];   // its speed along the way it faces at the last look...
        private float[] _lastAt = new float[0];      // ...and when that was
        private float[] _slowingOf = new float[0];   // m/s2 it has been losing speed with, smoothed over looks
        private float _nextSample;

        private const byte SaidRoom = 1, SaidLetOut = 2;

        // The mod's own vehicles' last places, for their speed (their bodies are kinematic: velocity says nothing).
        private readonly Vector3[] _ownAt = new Vector3[8];
        private readonly float[] _ownWhen = new float[8];

        // Gathers this look's cars and everything on the roads; the number of cars to read.
        private int BeginLook(float now)
        {
            TrafficManager traffic = TrafficManager.Instance;
            List<VehicleComponent> pooled = Manager.GetVehicleList();
            if (_game.Pool(traffic, pooled.Count) || _slowing.Length < pooled.Count)
            {
                // Another city's traffic: what was remembered of each car is forgotten.
                if (_brakes != null)
                {
                    _brakes.Clear();
                }
                _slowing = new bool[pooled.Count];
                _slowedFor = new DriverRules.Why[pooled.Count];
                _said = new byte[pooled.Count];
                _lastAlong = new float[pooled.Count];
                _lastAt = new float[pooled.Count];
                _slowingOf = new float[pooled.Count];
            }
            _obstacles.Clear();
            _cars = 0;
            Vector3 player = PlayerHelper.GetPosition();
            ReadPlayer(traffic, player, now);
            _game.BeginLook();
            float near = DriverRules.Near * DriverRules.Near;
            float far = DriverRules.ObstaclesNear * DriverRules.ObstaclesNear;
            for (int i = 0; i < pooled.Count; i++)
            {
                VehicleComponent car = pooled[i];
                if (car == null || !car.isActiveAndEnabled || car.rb == null)
                {
                    continue;
                }
                int index = car.GetIndex();
                if (index < 0 || index >= _slowing.Length)
                {
                    continue;
                }
                Transform body = _game.At(index, car);
                if (body == null)
                {
                    continue;   // not measured yet, and enough have been at this look
                }
                Vector3 at = body.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                float apart = dx * dx + dz * dz;
                if (apart > far || Mathf.Abs(at.y - player.y) > 20f)
                {
                    LetGo(index);
                    continue;
                }
                Vector3 forward = body.forward;
                float flat = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);
                if (flat < 0.5f)
                {
                    LetGo(index);
                    continue;   // on its side: a wreck, nobody drives it
                }
                forward.x /= flat;
                forward.z /= flat;
                forward.y = 0f;
                Vector3 velocity = car.rb.velocity;
                Vector3 middle = at + forward * _game.Middle(index);
                int obstacle = _obstacles.Add(DriverRules.Why.Car, index, middle.x, middle.z, forward.x, forward.z, velocity.x, velocity.z,
                    _game.HalfLength(index), _game.HalfWidth(index), 0f);
                if (obstacle >= 0)
                {
                    _obstacles.Slowing[obstacle] = Slowing(index, velocity.x * forward.x + velocity.z * forward.z, now);
                }
                // (A car the mod drives itself, a unit, a wreck or the reckless one, is only something in the way.)
                if (apart > near || traffic.IsVehicleIgnored(index) || _cars >= _car.Length)
                {
                    LetGo(index);
                    continue;
                }
                _car[_cars] = car;
                _index[_cars] = index;
                _nose[_cars] = at + forward * _game.Nose(index);
                _forward[_cars] = forward;
                _speed[_cars] = Mathf.Max(0f, velocity.x * forward.x + velocity.z * forward.z);
                _cars++;
            }
            AddOwnVehicles(player, now);
            AddLines();
            return _cars;
        }

        // How fast a car of the pool is losing speed (m/s2, 0 if it is not), from its speed at the last look, smoothed.
        private float Slowing(int index, float along, float now)
        {
            float since = now - _lastAt[index];
            float slowing = 0f;
            if (_lastAt[index] > 0f && since > 0.01f && since < 0.5f)
            {
                slowing = Mathf.Max(0f, (_slowingOf[index] + (_lastAlong[index] - along) / since) * 0.5f);
            }
            _slowingOf[index] = slowing;
            _lastAlong[index] = along;
            _lastAt[index] = now;
            return slowing;
        }

        // The mod's bus and tow truck: things on the road like any car, their speed from where they were at the last look.
        private void AddOwnVehicles(Vector3 player, float now)
        {
            float far = DriverRules.ObstaclesNear * DriverRules.ObstaclesNear;
            for (int i = 0; i < TrafficTools.OwnVehicles && i < _ownAt.Length; i++)
            {
                IDrivenVehicle vehicle = TrafficTools.OwnVehicleOut(i);
                BoxCollider solid = vehicle != null ? vehicle.Solid as BoxCollider : null;
                if (solid == null)
                {
                    _ownWhen[i] = 0f;
                    continue;
                }
                Transform frame = vehicle.Frame;
                Vector3 middle = frame.TransformPoint(solid.center);
                if ((middle - player).sqrMagnitude > far)
                {
                    _ownWhen[i] = 0f;
                    continue;
                }
                Vector3 forward = frame.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                Vector3 velocity = Vector3.zero;
                float since = now - _ownWhen[i];
                if (_ownWhen[i] > 0f && since > 0.01f && since < 1f)
                {
                    velocity = (middle - _ownAt[i]) / since;
                    velocity.y = 0f;
                }
                _ownAt[i] = middle;
                _ownWhen[i] = now;
                Vector3 size = solid.size;
                _obstacles.Add(DriverRules.Why.Bus, -1, middle.x, middle.z, forward.x, forward.z, velocity.x, velocity.z, size.z * 0.5f, size.x * 0.5f, 0f);
            }
        }

        // One car of the look: its road read, what binds it found, its brake named or let go.
        private void Evaluate(int c, float now)
        {
            VehicleComponent car = _car[c];
            if (car == null || !car.isActiveAndEnabled)
            {
                return;
            }
            int index = _index[c];
            float speed = _speed[c];
            float sensor = _game.SensorPast(index);
            float halfWidth = _game.HalfWidth(index);
            var best = DriverRules.Binding.None;
            ReadRoad(c, index, speed, sensor, ref best);
            DriverRules.Worst(ref best, _road, _obstacles, index, halfWidth, speed, sensor);
            WatchLines(c, index, speed, sensor, halfWidth);
            Decide(c, index, car, speed, best, now);
        }

        // The car's road ahead, from its nose through the lane point it is driving to and on, and what the road itself asks of it.
        private void ReadRoad(int c, int index, float speed, float sensor, ref DriverRules.Binding best)
        {
            Vector3 nose = _nose[c];
            Vector3 forward = _forward[c];
            _road.Begin(nose.x, nose.z, forward.x, forward.z);
            float sight = DriverRules.Sight(speed, sensor);
            if (_playerKeep > 0f)
            {
                // Near the player's car, the gap kept behind it is read on top.
                float px = _playerMiddle.x - nose.x;
                float pz = _playerMiddle.z - nose.z;
                float far = sight + _playerKeep + DriverRules.NearReach;
                sight += px * px + pz * pz < far * far ? _playerKeep : 0f;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint point = DriverGame.Target(traffic, index);
            if (point == null)
            {
                _road.Add(nose.x + forward.x * sight, nose.z + forward.z * sight);   // no lane to read: straight on
                return;
            }
            float limit = _car[c].GetMaxSpeed() / 3.6f;   // what the car itself goes at, at the most
            Waypoint before = null;
            int turning = int.MinValue;   // asked of its indicator at the first fork only
            for (int k = 0; k < DriverRules.PathPoints && point != null; k++)
            {
                Vector3 p = point.position;
                if (Mathf.Abs(p.y - nose.y) > Level)
                {
                    break;   // a bridge over or a road under: not this car's road
                }
                if (k == 0 && (p.x - nose.x) * forward.x + (p.z - nose.z) * forward.z < 0.3f)
                {
                    before = point;   // passing it now: the road starts at the next
                    point = Next(traffic, point, forward, ref turning, index);
                    continue;
                }
                float upTo = _road.Length;
                if (point.temporaryDisabled)
                {
                    // A closed lane (a cordon, its gate, roadworks): the traffic system stops dead at the last open point,
                    // unless it has another way on there, a branch or a lane beside.
                    if (before != null && !OtherWay(traffic, before))
                    {
                        DriverRules.Consider(ref best, DriverRules.Why.Closed, -1, upTo, upTo - DriverRules.ClosedShort, DriverRules.ClosedSpeed, 0f, speed);
                    }
                    break;
                }
                _road.Add(p.x, p.z);
                float at = _road.Length;
                if (point.stop)
                {
                    DriverRules.Consider(ref best, DriverRules.Why.RedLight, -1, at, at - DriverRules.RedShort, 0f, 0f, speed);
                    break;   // beyond a red light the car is not going yet
                }
                if (point.giveWay)
                {
                    DriverRules.Consider(ref best, DriverRules.Why.GiveWay, -1, at, at - DriverRules.StopTake, DriverRules.StopSpeed, 0f, speed);
                    break;
                }
                float pointLimit = point.maxSpeed > 0 ? Mathf.Min(limit, point.maxSpeed / 3.6f) : limit;
                if (pointLimit < speed - 0.5f)
                {
                    DriverRules.Consider(ref best, DriverRules.Why.Limit, -1, upTo, upTo - DriverRules.LimitShort, pointLimit, 0f, speed);
                }
                if (at >= sight)
                {
                    break;
                }
                Vector3 into = before != null ? p - before.position : p - nose;
                before = point;
                point = Next(traffic, point, into, ref turning, index);
            }
            // The bends on it, measured from each point the road starts to turn at: slowed for before the corner, not in it.
            for (int i = 1; i < _road.Count - 1; i++)
            {
                float turn = _road.Turn(i);
                if (turn < 3f && turn > -3f)
                {
                    continue;
                }
                float bend = DriverRules.BendSpeed(_road.Bend(i, DriverRules.BendReach, out float over), over);
                if (bend < speed)
                {
                    DriverRules.Consider(ref best, DriverRules.Why.Bend, -1, _road.S(i), _road.S(i) - DriverRules.BendLead, bend, 0f, speed);
                }
            }
        }

        // The lane point after this one: the only one, or at a fork the branch the car's indicator shows (the straightest without one).
        // into: the way the road comes into this point.
        private Waypoint Next(TrafficManager traffic, Waypoint point, Vector3 into, ref int turning, int index)
        {
            List<int> next = point.neighbors;
            if (next == null || next.Count == 0)
            {
                return null;
            }
            if (next.Count == 1)
            {
                return traffic.GetWaypoint(next[0]);
            }
            if (turning == int.MinValue)
            {
                turning = _game.Turning(index);
            }
            into.y = 0f;
            Waypoint chosen = null;
            float best = float.MinValue;
            for (int i = 0; i < next.Count; i++)
            {
                Waypoint way = traffic.GetWaypoint(next[i]);
                if (way == null || way.temporaryDisabled)
                {
                    continue;
                }
                Vector3 leaving = way.position - point.position;
                leaving.y = 0f;
                float angle = into.sqrMagnitude > 0.01f && leaving.sqrMagnitude > 0.01f ? Vector3.SignedAngle(into, leaving, Vector3.up) : 0f;
                float score = turning > 0 ? angle : turning < 0 ? -angle : -Mathf.Abs(angle);
                if (score > best)
                {
                    best = score;
                    chosen = way;
                }
            }
            return chosen;
        }

        // True if the traffic system has another way on from this point when the next is closed: a branch or a lane beside.
        private static bool OtherWay(TrafficManager traffic, Waypoint point)
        {
            List<int> ways = point.neighbors;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; ways != null && i < ways.Count; i++)
                {
                    Waypoint way = traffic.GetWaypoint(ways[i]);
                    if (way != null && !way.temporaryDisabled)
                    {
                        return true;
                    }
                }
                ways = point.otherLanes;
            }
            return false;
        }

        // What the look decided for one car: slowed for what binds it, or let go.
        private void Decide(int c, int index, VehicleComponent car, float speed, DriverRules.Binding best, float now)
        {
            bool slowing = _slowing[index];
            bool needed = DriverRules.Slows(best.Need, slowing) || DriverRules.Held(best);
            // People in the road are Traffic sees people's: a car it brakes for somebody is left to it.
            if (needed && SolidStreets.Braking(car))
            {
                needed = false;
            }
            if (!needed)
            {
                if (slowing)
                {
                    float most = _brakes.Drop(index);
                    AtRest(c, index, car, speed, best, most);
                }
                _slowing[index] = false;
                _slowedFor[index] = DriverRules.Why.None;
                if (speed > 1f)
                {
                    _said[index] = 0;
                }
                return;
            }
            Rigidbody ahead = null;
            if (best.Obstacle >= 0)
            {
                ahead = BodyOf(best.Obstacle);
            }
            // Short of anything the game's sensor reads (the gap behind the player standing, a line that could not be made), the
            // car is kept standing once there.
            if (!_brakes.Set(index, car, best.Room, best.PointSpeed, best.Ahead, best.Keep, ahead, best.Hold))
            {
                return;   // sixteen are slowed already: this one is the game's until one is let go
            }
            if (!slowing || _slowedFor[index] != best.Why)
            {
                Began(car, speed, best, now);
            }
            _slowing[index] = true;
            _slowedFor[index] = best.Why;
            if (speed < 0.3f)
            {
                AtRest(c, index, car, speed, best, _brakes.MostOn(index));
            }
        }

        // The body of what a car slows for, when it is one that moves on: a traffic car, the player's.
        private Rigidbody BodyOf(int obstacle)
        {
            DriverRules.Why kind = _obstacles.Kind[obstacle];
            if (kind == DriverRules.Why.Car)
            {
                List<VehicleComponent> pooled = Manager.GetVehicleList();
                int id = _obstacles.Id[obstacle];
                VehicleComponent other = id >= 0 && id < pooled.Count ? pooled[id] : null;
                return other != null && other.GetIndex() == id ? other.rb : null;
            }
            return kind == DriverRules.Why.Player || kind == DriverRules.Why.PlayerRoom || kind == DriverRules.Why.Parking ? _playerBody : null;
        }

        // A car begins to slow: the self-check speaks for one now and then, never for every car.
        private void Began(VehicleComponent car, float speed, DriverRules.Binding best, float now)
        {
            if (!Meta.Dev.Verbose || now < _nextSample || speed < 3f)
            {
                return;
            }
            _nextSample = now + SampleEvery;
            bool gentle = DriverRules.Gentle(best.Need);
            Logger?.Info(System.FormattableString.Invariant(
                             $"Traffic check (braking in good time): '{TrafficTools.Name(car)}' at {speed * 3.6f:F0} km/h began to slow for {Said(best.Why)} {best.Distance:F0} m ahead, ")
                         + System.FormattableString.Invariant($"needing {best.Need:F1} m/s2 to reach it at {best.PointSpeed * 3.6f:F0} km/h (an ordinary slowing is under {DriverRules.Firm:F1}; ")
                         + System.FormattableString.Invariant($"the game alone would have stopped it within {DriverRules.GameStop:F2} m of its sensor): ")
                         + (gentle ? "PASS" : System.FormattableString.Invariant($"a firm stop: it was {best.Distance:F0} m off when it came into view")));
        }

        // The car stands for what it slowed for: the self-checks that are about a stop say so, once.
        private void AtRest(int c, int index, VehicleComponent car, float speed, DriverRules.Binding best, float mostBraking)
        {
            if (speed >= 0.3f || !Meta.Dev.Verbose)
            {
                return;
            }
            if ((best.Why == DriverRules.Why.Parking || best.Why == DriverRules.Why.PlayerRoom || (best.Why == DriverRules.Why.HoldLine && LineIs(best.Obstacle, ReverseLine)))
                && (_said[index] & SaidRoom) == 0)
            {
                _said[index] |= SaidRoom;
                float room = RoomBehindPlayer(_nose[c], _forward[c]);
                bool pass = room >= DriverRules.RoomWanted && mostBraking < DriverRules.Firm;
                Logger?.Info(System.FormattableString.Invariant($"Traffic check (parking room): '{TrafficTools.Name(car)}' stopped {room:F1} m behind the player's car while they park ")
                             + System.FormattableString.Invariant($"(wanted at least {DriverRules.RoomWanted:F1}), braking at most {mostBraking:F1} m/s2 (wanted under {DriverRules.Firm:F1}): ")
                             + (pass ? "PASS" : room < DriverRules.RoomWanted ? "FAIL, it was nearer than that already when the player began to park" : "FAIL, it had to stop firmly"));
            }
            else if (best.Why == DriverRules.Why.HoldLine && LineIs(best.Obstacle, LetOutLine) && (_said[index] & SaidLetOut) == 0)
            {
                _said[index] |= SaidLetOut;
                float room = RoomBehindPlayer(_nose[c], _forward[c]);
                bool pass = room >= DriverRules.LetOutBack && mostBraking <= DriverRules.YieldMost + 0.5f;
                _letOutHeld++;
                Logger?.Info(System.FormattableString.Invariant($"Traffic check (let out): '{TrafficTools.Name(car)}' gave way: it stands {room:F1} m behind the player's car pulling out ")
                             + System.FormattableString.Invariant($"(wanted at least {DriverRules.LetOutBack:F1}), having braked at most {mostBraking:F1} m/s2 ")
                             + System.FormattableString.Invariant($"(wanted under {DriverRules.YieldMost:F1}): ")
                             + (pass ? "PASS" : "FAIL, it was too close to give way gently"));
            }
        }

        private static string Said(DriverRules.Why why)
        {
            switch (why)
            {
                case DriverRules.Why.Car: return "a car";
                case DriverRules.Why.Player: return "the player's car";
                case DriverRules.Why.PlayerRoom: return "the room behind the player's car";
                case DriverRules.Why.Parking: return "the player parking";
                case DriverRules.Why.OnFoot: return "the player in the road";
                case DriverRules.Why.Bus: return "the bus or the tow truck";
                case DriverRules.Why.HoldLine: return "a hold line for the player";
                case DriverRules.Why.RedLight: return "a red light";
                case DriverRules.Why.GiveWay: return "a give-way line";
                case DriverRules.Why.Closed: return "a closed lane";
                case DriverRules.Why.Limit: return "a lower limit";
                case DriverRules.Why.Bend: return "a sharp bend";
                default: return "nothing";
            }
        }

        // A car of the pool is out of this look (too far, held by the mod, gone): it is the game's again.
        private void LetGo(int index)
        {
            if (index >= 0 && index < _slowing.Length && _slowing[index])
            {
                _brakes.Drop(index);
                _slowing[index] = false;
                _slowedFor[index] = DriverRules.Why.None;
            }
        }

        // What was remembered of every car is forgotten (the brakes have been cleared).
        private void ForgetCars()
        {
            for (int i = 0; i < _slowing.Length; i++)
            {
                _slowing[i] = false;
                _slowedFor[i] = DriverRules.Why.None;
                _said[i] = 0;
                _lastAt[i] = 0f;
            }
            _cars = 0;
        }
    }
}
