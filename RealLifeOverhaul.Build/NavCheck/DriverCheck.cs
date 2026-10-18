using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RealLifeOverhaul;

// Checks for the city's drivers (Traffic/Drivers/DriverRules.cs, compiled as it ships), against a model of the game's own
// traffic system driving a car along a lane (Gley's DriveJob and DrivingAI, read from ExternalPlugins.dll: a 4.5 m sensor
// from the front axle, a stop within 1.25 m of whatever it touches, a red light seen one lane point ahead, the prefabs'
// brake time of 1.5 s). Each case is driven twice: with the drivers' braking, as CityDrivers names a car and DriverBrakes
// slows it each physics step, and with the game alone, which must fail what the drivers pass: braking in good time and
// smoothly, the room kept for the player to park, a car letting the player out of a space. And the wave-through's sequence
// and where the officer stands aside (Traffic/CordonRules.cs), the option off giving everything back (over the sources),
// what a look costs, and the words.
internal static partial class Program
{
    private static void Drivers()
    {
        DriverNumbers();
        DriverRoadShape();
        DriverSmoothness();
        DriverParkingRoom();
        DriverLetOut();
        DriverWave();
        DriverOptionOff();
        DriverTiming();
        DriverWords();
    }

    // ---------------------------------------------------------------- the rules' own numbers

    private static void DriverNumbers()
    {
        Check(Near(DriverRules.Sight(0f, 3.3f), DriverRules.SightLeast), "drivers: a standing car reads 10 m of road ahead");
        Check(Near(DriverRules.Sight(40f, 3.3f), DriverRules.SightMost), "drivers: never more than 60 m");
        bool grows = true;
        for (float v = 0f; v < 20f; v += 0.5f)
        {
            grows &= DriverRules.Sight(v + 0.5f, 3.3f) >= DriverRules.Sight(v, 3.3f);
        }
        Check(grows, "drivers: the faster, the further ahead a driver reads the road");
        Check(DriverRules.Sight(12.5f, 3.3f) >= DriverRules.StoppingDistance(12.5f, DriverRules.Comfort) + 3.3f + 12.5f * DriverRules.Reaction,
            "drivers: at 45 km/h it reads further than a reaction, an ordinary stop and the sensor");
        Check(Near(DriverRules.Sensor(3.1f), 3.1f) && DriverRules.Sensor(0.2f) == DriverRules.SensorDefault && DriverRules.Sensor(float.NaN) == DriverRules.SensorDefault
              && DriverRules.Sensor(9f) == DriverRules.SensorDefault, "drivers: a sensor reach measured in 1.5 to 6 m is believed, any other is the usual 3.3 m");

        Check(DriverRules.Need(0f, 10f, 2f, 0f) == 0f && DriverRules.Need(1.5f, 10f, 2f, 0f) == 0f, "drivers: a car at or under its speed for the point needs no slowing");
        Check(Near(DriverRules.Need(14f, 40f, 2f, 0f), (196f - 4f) / 80f), "drivers: 14 m/s to 2 m/s in 40 m needs 2.4 m/s2");
        Check(DriverRules.Need(12f, 20f, 12f, 12f) == 0f, "drivers: behind a car going as fast, no slowing");
        Check(Near(DriverRules.Need(12f, 20f, 5f, 5f), 49f / 40f), "drivers: closing at 7 m/s on a car at 5 with 20 m to go: 1.2 m/s2, worked in its frame");
        Check(Near(DriverRules.Need(6f, 0f, 2f, 0f), (36f - 4f) / (2f * DriverRules.RoomLeast)) && DriverRules.Need(6f, -5f, 2f, 0f) < DriverRules.Overrun,
            "drivers: inside its room a car is still worked over half a metre: a number, never endless");
        Check(DriverRules.Need(10f, 20f, 2f, -4f) == DriverRules.Need(10f, 20f, 2f, 0f), "drivers: something coming the other way is taken as standing");
        Check(Near(DriverRules.Need(6f, -3f, 6f, 6f, 9f), 1f) && DriverRules.Need(6f, -3f, 6f, 6f, 0f) == 0f,
            "drivers: 3 m inside the gap kept behind the player's car, a car as fast drops back at 1 m/s2 (opening it in about 3 s)");
        Check(DriverRules.Need(6f, -20f, 6f, 6f, 9f) <= 9f / DriverRules.DropBack / DriverRules.Settle + 1e-4f, "drivers: never harder than the whole gap asks");

        Check(DriverRules.Brake(1f, false, 0f, 0.02f) == 0f, "drivers: 1 m/s2 needed is not yet a reason to brake");
        Check(DriverRules.Brake(2f, false, 0f, 0.02f) > 0f && DriverRules.Brake(2f, false, 0f, 0.02f) <= DriverRules.Rise * 0.02f + 1e-5f,
            "drivers: an ordinary slowing comes on gently, 6 m/s3");
        Check(Near(DriverRules.Brake(2f, true, 2f, 0.02f), 2f), "drivers: once on, it is what is needed");
        Check(DriverRules.Brake(0.6f, true, 0.6f, 0.02f) > 0f && DriverRules.Brake(0.4f, true, 0.6f, 0.02f) == 0f, "drivers: one who has begun lets go under 0.5 m/s2");
        Check(Near(DriverRules.Brake(0.1f, true, 0.5f, 0.02f, true), DriverRules.KeepFrom) && DriverRules.Brake(0f, false, 0f, 0.02f, true) > 0f,
            "drivers: ...but not at the place it is held at, before it stands");
        Check(Near(DriverRules.Brake(6f, false, 0f, 0.02f), 6f) && Near(DriverRules.Brake(50f, false, 0f, 0.02f), DriverRules.Most),
            "drivers: a firm stop is put on at once, never more than 7 m/s2");
        Check(DriverRules.Slows(1.3f, false) && !DriverRules.Slows(1.1f, false) && DriverRules.Slows(0.6f, true) && !DriverRules.Slows(0.4f, true) && !DriverRules.Slows(0f, true),
            "drivers: a car is named at 1.2 m/s2 and kept to 0.5");

        Check(Near(DriverRules.PlayerGap(0f, 3.3f, false), 3.3f), "drivers: behind the player's car standing in a middle lane, the game's own gap");
        Check(Near(DriverRules.PlayerGap(10f, 3.3f, false), 3.3f + 15f), "drivers: behind it moving at 36 km/h, a second and a half more");
        Check(Near(DriverRules.PlayerGap(2f, 3.3f, true), DriverRules.ParkRoom) && Near(DriverRules.PlayerGap(0f, 3.3f, true), DriverRules.ParkRoom),
            "drivers: behind it slow in a kerb-side lane, the room to park");
        Check(Near(DriverRules.PlayerGap(5f, 3.3f, true), 3.3f + 7.5f), "drivers: at 18 km/h in a kerb-side lane it is only the time gap (it is not parking)");

        Check(Near(DriverRules.Across(2.4f, 1f, 1f, 0f), 1f) && Near(DriverRules.Across(2.4f, 1f, 0f, 1f), 2.4f), "drivers: a car reaches its half width across a line it runs along, its half length across one it crosses");
        Check(Near(DriverRules.Along(2.4f, 1f, 1f, 0f), 2.4f) && Near(DriverRules.Along(2.4f, 1f, 0f, -1f), 1f), "drivers: and the other way round along it");
        Check(DriverRules.InWay(1.1f, 1f, 1f) && !DriverRules.InWay(2.3f, 1f, 1f) && DriverRules.InWay(-2.1f, 1f, 1f), "drivers: in the way where the two widths and 0.2 m overlap");

        Check(DriverRules.BendSpeed(10f, 12f) == float.MaxValue, "drivers: a gentle bend is not slowed for");
        float corner = DriverRules.BendSpeed(90f, 12f);
        Check(corner > 3.5f && corner < 6f, "drivers: a city corner (90 degrees over 12 m) is taken at " + (corner * 3.6f).ToString("0") + " km/h (wanted 13 to 22)");
        Check(DriverRules.BendSpeed(-90f, 12f) == corner, "drivers: a left corner as a right one (the game slows only for right ones)");

        // At the kerb.
        Check(DriverRules.PullingOut(2.6f, 1.6f, 0.6f, 8f, 0.1f, 0.5f), "drivers: at the kerb, setting off slowly and turned 8 degrees towards the lane: pulling out");
        Check(DriverRules.PullingOut(2.6f, 1.6f, 0.6f, 0f, 0.3f, 0.5f), "drivers: ...or edging towards it at 0.3 m/s");
        Check(!DriverRules.PullingOut(2.6f, 1.6f, 0.6f, -8f, -0.2f, 0.5f), "drivers: not turned away from the lane");
        Check(!DriverRules.PullingOut(0.4f, -0.6f, 0.6f, 8f, 0.2f, 0.5f), "drivers: not a car in the lane already");
        Check(!DriverRules.PullingOut(2.6f, 1.6f, 6f, 8f, 0.2f, 0.5f), "drivers: not a car driving along at 22 km/h");
        Check(!DriverRules.PullingOut(2.6f, 1.6f, 0.6f, 8f, 0.2f, 9f), "drivers: not one that has been moving for 9 s (it is not setting off from a space)");
        Check(!DriverRules.PullingOut(7f, 6f, 0.6f, 8f, 0.2f, 0.5f), "drivers: not one 7 m from the lane (not beside it)");
        Check(DriverRules.PulledOut(0.5f, 5f, 2f, 1f) && DriverRules.PulledOut(0.5f, 5f, 0.2f, 7f) && !DriverRules.PulledOut(1.5f, 25f, 2f, 1f) && DriverRules.PulledOut(1.5f, 25f, 2f, 13f),
            "drivers: out when lined up in the lane and moving, or well past the line");
        Check(DriverRules.Yields(13f, 60f, 3.3f) && DriverRules.Yields(13f, 35f, 3.3f) && !DriverRules.Yields(13f, 25f, 3.3f) && !DriverRules.Yields(13f, 3f, 3.3f),
            "drivers: at 47 km/h a car lets the player out from 35 m, not from 25");
        Check(DriverRules.Yields(0f, 2f, 3.3f), "drivers: one standing at the line already holds there");
        Check(!DriverRules.Blocks(13f, -3f, 3.3f) && !DriverRules.Blocks(13f, 80f, 3.3f) && DriverRules.Blocks(13f, 20f, 3.3f) && !DriverRules.Blocks(13f, 40f, 3.3f),
            "drivers: a line waits only for a car near it that could not stop gently (past it, or far off, no)");
        Check(DriverRules.HoldOver(15f, 0f, false, false) && DriverRules.HoldOver(3f, 4f, false, false) && DriverRules.HoldOver(1f, 0f, true, false)
              && DriverRules.HoldOver(1f, 0f, false, true) && !DriverRules.HoldOver(10f, 2f, false, false), "drivers: a line comes down at 15 s, after 4 s standing, when out, or gone");
        Check(DriverRules.Reversing(-1f) && !DriverRules.Reversing(-0.1f) && !DriverRules.Reversing(-4f) && !DriverRules.Reversing(1f), "drivers: reversing is backing at walking pace");
        Check(DriverRules.KerbSide(0, 1) && DriverRules.KerbSide(2, 0) && !DriverRules.KerbSide(1, 1), "drivers: a kerb-side lane has lanes its way on one side at most");
    }

    // ---------------------------------------------------------------- the road and what is on it

    private static void DriverRoadShape()
    {
        var road = new DriverRules.Road(16);
        road.Begin(0f, 0f, 1f, 0f);
        road.Add(0.1f, 0f);   // too near: skipped
        road.Add(10f, 0f);
        road.Add(20f, 0f);
        road.Add(25f, 5f);
        Check(road.Count == 4 && Near(road.Length, 20f + (float)Math.Sqrt(50.0)), "drivers' road: points too near the last are skipped, the length is the legs'");
        Check(road.Project(12f, 1f, 3f, out float along, out float lateral, out _, out _) && Near(along, 12f) && Near(lateral, -1f),
            "drivers' road: a place 12 m on and 1 m to the left (z north, facing east)");
        Check(!road.Project(12f, 6f, 3f, out _, out _, out _, out _), "drivers' road: 6 m to the side is not on it");
        Check(!road.Project(-9f, 0f, 3f, out _, out _, out _, out _), "drivers' road: 9 m behind its start is not on it");
        Check(road.Project(24f, 3f, 2f, out along, out lateral, out float wx, out float wz) && wx > 0.6f && wz > 0.6f, "drivers' road: on the last leg the way is that leg's");
        Check(Near(road.Turn(2), -45f, 0.01f) && road.Turn(0) == 0f && road.Turn(3) == 0f, "drivers' road: it turns 45 degrees left at its third point, nowhere at its ends");
        float bend = road.Bend(2, 12f, out float over);
        Check(Near(bend, 45f, 0.01f) && over > 5f, "drivers' road: the bend from there is 45 degrees");

        var s = new DriverRules.Road(16);
        s.Begin(0f, 0f, 1f, 0f);
        s.Add(5f, 0f);
        s.Add(10f, 1f);
        s.Add(15f, 1f);
        s.Add(20f, 1f);
        Check(s.Bend(1, 12f, out _) < 15f, "drivers' road: a lane's S is not a corner (its turns cancel)");
        // Round a corner (30 m east, then 30 m north): a place beside the far leg is found on that leg, not where the chord would put it.
        var corner = new DriverRules.Road(16);
        corner.Begin(0f, 0f, 1f, 0f);
        for (int k = 1; k <= 6; k++)
        {
            corner.Add(5f * k, 0f);
        }
        for (int k = 1; k <= 6; k++)
        {
            corner.Add(30f, 5f * k);
        }
        Check(corner.Project(31f, 25f, 3f, out along, out lateral, out _, out _) && Near(along, 55f, 0.05f) && Near(lateral, 1f, 0.05f),
            "drivers' road: round a corner, a place beside the far leg is 55 m on and 1 m to the right of it");
        Check(road.Project(12f, 1f, 3f, out along, out _, out _, out _) && Near(along, 12f), "drivers' road: on a straight one, the same as before");

        var obstacles = new DriverRules.Obstacles(8);
        // A car 20 m ahead in the lane (its middle at 22.4 m), one in the next lane, one behind, the car itself.
        obstacles.Add(DriverRules.Why.Car, 7, 22.4f, 0f, 1f, 0f, 0f, 0f, 2.4f, 1f, 0f);
        obstacles.Add(DriverRules.Why.Car, 8, 22.4f, 3.4f, 1f, 0f, 0f, 0f, 2.4f, 1f, 0f);
        obstacles.Add(DriverRules.Why.Car, 9, -6f, 0f, 1f, 0f, 0f, 0f, 2.4f, 1f, 0f);
        obstacles.Add(DriverRules.Why.Car, 3, 1f, 0f, 1f, 0f, 0f, 0f, 2.4f, 1f, 0f);
        var lane = new DriverRules.Road(4);
        lane.Begin(0f, 0f, 1f, 0f);
        lane.Add(60f, 0f);
        Check(DriverRules.OnRoad(lane, obstacles, 0, 3, 1f, out float distance, out _) && Near(distance, 20f), "drivers' road: a car 20 m ahead in the lane is 20 m to its rear");
        Check(!DriverRules.OnRoad(lane, obstacles, 1, 3, 1f, out _, out _), "drivers' road: one in the next lane is not in the way");
        Check(!DriverRules.OnRoad(lane, obstacles, 2, 3, 1f, out _, out _), "drivers' road: one behind is not");
        Check(!DriverRules.OnRoad(lane, obstacles, 3, 3, 1f, out _, out _), "drivers' road: nor the car itself");
        // The player's car nosing out of a space: its middle 2.6 m to the side, turned 25 degrees, its corner in the lane.
        var nosing = new DriverRules.Obstacles(2);
        float c = (float)Math.Cos(25.0 * Math.PI / 180.0), sn = (float)Math.Sin(25.0 * Math.PI / 180.0);
        nosing.Add(DriverRules.Why.Player, -1, 30f, 2.6f, c, -sn, 0f, 0f, 2.4f, 1f, 0f);
        Check(DriverRules.OnRoad(lane, nosing, 0, 3, 1f, out distance, out _) && distance < 30f, "drivers' road: a car nosing out of a space into the lane is in the way");
        var parked = new DriverRules.Obstacles(2);
        parked.Add(DriverRules.Why.Player, -1, 30f, 3.3f, 1f, 0f, 0f, 0f, 2.4f, 1f, 0f);
        Check(!DriverRules.OnRoad(lane, parked, 0, 3, 1f, out _, out _), "drivers' road: the same car parked straight at the kerb is not");

        var best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, obstacles, 3, 1f, 12f, 3.3f);
        Check(best.Why == DriverRules.Why.Car && best.Obstacle == 0 && Near(best.Room, 20f - 3.3f), "drivers: the car standing ahead binds, with its room to the game's sensor");
        Check(Near(best.PointSpeed, DriverRules.Handover), "drivers: ...which it is brought to at walking pace");
        obstacles.Stop[0] = true;
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, obstacles, 3, 1f, 12f, 3.3f);
        Check(best.PointSpeed == 0f && Near(best.Room, 20f - DriverRules.StopGap - DriverRules.RoomLeast) && best.Hold,
            "drivers: for what the game's sensor cannot hold, to rest 2 m short (aimed half a metre before, never past) and held there");
        var kept = new DriverRules.Obstacles(2);
        kept.Add(DriverRules.Why.PlayerRoom, -1, 22.4f, 0f, 1f, 0f, 0f, 0f, 2.4f, 1f, 5.2f);
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, kept, 3, 1f, 6f, 3.3f);
        Check(best.Why == DriverRules.Why.PlayerRoom && best.PointSpeed == 0f && best.Hold && Near(best.Room, 20f - 3.3f - 5.2f - DriverRules.RoomLeast),
            "drivers: behind the player's car standing with room kept behind it, to rest at that room and held there (the game's sensor would let it on)");
        Check(DriverRules.Held(best) == false, "drivers: ...held once there, not while it is still 9 m off");
        kept.Vx[0] = 4f;
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, kept, 3, 1f, 6f, 3.3f);
        Check(!best.Hold && Near(best.PointSpeed, 4f), "drivers: behind it moving, at its speed, not held");
        kept.Vx[0] = 0f;
        kept.X[0] = 16.4f;
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, kept, 3, 1f, 3.46f, 3.3f);
        Check(DriverRules.Sight(3.46f, 3.3f) < 14f && best.Why == DriverRules.Why.PlayerRoom,
            "drivers: the player's car 14 m ahead, beyond the sight of a car at 12 km/h, is seen: the room kept behind it comes inside that sight");
        kept.Kind[0] = DriverRules.Why.Player;
        kept.X[0] = 22.4f;
        kept.Vx[0] = 4f;
        kept.Slowing[0] = 2f;
        kept.KeepAtRest[0] = 0f;
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, kept, 3, 1f, 6f, 3.3f);
        Check(best.Obstacle == -1 && Near(best.Distance, 24f), "drivers: behind something slowing, the car plans for where it will stand (4 m on at 2 m/s2)");
        var moving = new DriverRules.Obstacles(2);
        moving.Add(DriverRules.Why.Car, 7, 22.4f, 0f, 1f, 0f, 12f, 0f, 2.4f, 1f, 0f);
        best = DriverRules.Binding.None;
        DriverRules.Worst(ref best, lane, moving, 3, 1f, 12f, 3.3f);
        Check(best.Need == 0f, "drivers: a car ahead going as fast binds nobody");
        var none = DriverRules.Binding.None;
        Check(DriverRules.Consider(ref none, DriverRules.Why.Limit, -1, 30f, 28f, 8f, 0f, 7f) && none.Need == 0f && none.Why == DriverRules.Why.Limit,
            "drivers: with no need anywhere the nearest thing is kept, for what it is");
        Check(DriverRules.Consider(ref none, DriverRules.Why.RedLight, -1, 50f, 45f, 3f, 0f, 12f) && none.Why == DriverRules.Why.RedLight, "drivers: and anything needing slowing beats it");
        Check(!DriverRules.Consider(ref none, DriverRules.Why.Car, 1, 10f, 40f, 12f, 12f, 12f), "drivers: nothing needing none beats something needing some");
    }

    // ---------------------------------------------------------------- driven: the game's car, with and without the drivers

    // The game's traffic system driving one car along a straight lane (positions in metres along it), as DriveJob and
    // DrivingAI do each physics step: on, at its acceleration step; StopInDistance's 1.25 m once its sensor touches
    // something (a traffic car moving on is followed instead); StopInPoint once a red light's point is its target.
    private sealed class GameCar
    {
        public const float Step = 0.02f;           // the game's fixed step (its TimeManager)
        public const float LaneGap = 4.8f;         // metres between two lane points (the median, TrafficComponent's note)
        public float MaxSpeed = 12.5f;             // m/s: a 45 km/h car of the pool
        public float BrakeTime = 1.5f;             // seconds (the prefabs')
        public float AccelerationTime = 8f;
        public float Sensor = 3.3f;                // metres past the nose
        public float AxleBehind = 1f;              // metres from the front axle (where the sensor box starts) to the nose
        public float Nose;
        public float Speed;
        public bool Stopping;                      // in StopInDistance (the drivers do not touch it then)
        public bool AtLine;                        // in StopInPoint
        private float _left;                       // StopInDistance's metres left
        private bool _following;

        public float BrakeStep => MaxSpeed / (int)(BrakeTime / Step);
        public float PowerStep => MaxSpeed / (int)(AccelerationTime / Step);

        /// <summary>
        /// The game's target speed for this step: the nearest thing's rear, its length and speed, whether the game follows it
        /// when it moves on (a traffic car; the player's car and what is tagged to halt cars are stopped for), and a red
        /// light's place (NaN for none).
        /// </summary>
        public float Target(float thing, float thingSpeed, float redLight, bool follows = true, float thingLength = 4.8f)
        {
            float gap = thing - Nose;
            if (gap <= Sensor && gap > -AxleBehind - thingLength)   // the sensor box overlaps it
            {
                if (!Stopping && !_following)
                {
                    if (follows && thingSpeed > 2.24f && Speed > 2.24f)
                    {
                        _following = true;
                    }
                    else
                    {
                        Stopping = true;
                        _left = DriverRules.GameStop;
                    }
                }
                else if (_following && thingSpeed < 2f)
                {
                    _following = false;
                    Stopping = true;
                    _left = DriverRules.GameStop;
                }
            }
            else if (Stopping || _following)
            {
                Stopping = false;
                _following = false;   // it left the sensor: Continue
            }
            float axle = Nose - AxleBehind;
            AtLine = !float.IsNaN(redLight) && redLight - axle <= LaneGap;
            float v = Speed;
            float brakeStep = BrakeStep;
            if (Stopping)
            {
                if (v <= brakeStep)
                {
                    return 0f;
                }
                _left -= v * Step;
                if (_left <= 0f)
                {
                    return 0f;
                }
                int n2 = (int)(_left / (v * Step)) + 1;
                int n3 = (int)(v / brakeStep);
                return n3 >= n2 ? Math.Max(0f, v - brakeStep * n3 / n2) : v;
            }
            if (AtLine)
            {
                float d = redLight - axle;
                if (d < 0f || v < brakeStep)
                {
                    return 0f;
                }
                int n2 = (int)(d / (v * Step));
                if (n2 == 0)
                {
                    return 0f;
                }
                int n3 = (int)(v / brakeStep);
                float target = v;
                if (n3 > n2)
                {
                    target -= brakeStep * n3 / n2;
                }
                if (n2 - n3 > 60)
                {
                    target += PowerStep;
                }
                return Math.Max(0f, Math.Min(target, MaxSpeed));
            }
            if (_following)
            {
                return v > thingSpeed ? Math.Max(thingSpeed, v - brakeStep) : Math.Min(v + PowerStep, thingSpeed);
            }
            return v > MaxSpeed ? Math.Max(MaxSpeed, v - brakeStep) : Math.Min(v + PowerStep, MaxSpeed);   // (DriveJob.SetMaxSpeed: over its top, the normal brake)
        }
    }

    // The drivers' part in a run, as the mod does it: a look eight times a second that names the car for what binds it
    // (CityDrivers.Decide), and each physics step its brake (DriverBrakes.Step), which leaves the car to the game while the
    // game stops it, reads the speed of what it slows for from that thing's body when it has one, and holds it standing.
    private sealed class DriverModel
    {
        public readonly DriverRules.Road Road = new DriverRules.Road(4);
        public readonly DriverRules.Obstacles Obstacles = new DriverRules.Obstacles(4);
        public DriverRules.Binding Best;
        public float Most;                 // m/s2: the most it braked with
        private bool _named, _braking, _bodied, _hold;
        private float _applied, _room, _pointSpeed, _ahead, _keep, _next;

        /// <summary>True if a look is due: the road ahead is laid down and the obstacles cleared, for the caller to add to.</summary>
        public bool Due(float t, float nose)
        {
            if (t < _next)
            {
                return false;
            }
            _next = t + DriverRules.TickSeconds;
            Road.Begin(nose, 0f, 1f, 0f);
            Road.Add(nose + DriverRules.SightMost, 0f);
            Obstacles.Clear();
            Best = DriverRules.Binding.None;
            return true;
        }

        /// <summary>The look's end: what binds the car among the obstacles and what the caller considered, named or let go.</summary>
        public void Decide(GameCar car)
        {
            DriverRules.Worst(ref Best, Road, Obstacles, 0, 1f, car.Speed, car.Sensor);
            _named = DriverRules.Slows(Best.Need, _named) || DriverRules.Held(Best);
            if (!_named)
            {
                _braking = false;
                _applied = 0f;
                return;
            }
            _room = Best.Room;
            _pointSpeed = Best.PointSpeed;
            _ahead = Best.Ahead;
            _keep = Best.Keep;
            _hold = Best.Hold;
            DriverRules.Why why = Best.Obstacle >= 0 ? Obstacles.Kind[Best.Obstacle] : DriverRules.Why.None;
            _bodied = why == DriverRules.Why.Car || why == DriverRules.Why.Player || why == DriverRules.Why.PlayerRoom || why == DriverRules.Why.Parking;
        }

        /// <summary>One physics step: the speed the car is left with, from the game's target; thingSpeed is that of the body it slows for.</summary>
        public float Step(GameCar car, float before, float target, float thingSpeed)
        {
            if (!_named || car.Stopping)
            {
                return target;
            }
            float brake = DriverRules.StepBrake(ref _room, ref _braking, ref _applied, before, _bodied ? thingSpeed : _ahead, _pointSpeed, _keep, _hold, GameCar.Step);
            if (before <= 0.05f)
            {
                return _hold && _room <= DriverRules.HoldWithin ? 0f : target;   // the game's step taken off: it stays standing
            }
            if (brake <= 0f)
            {
                return target;
            }
            Most = Math.Max(Most, brake);
            return Math.Max(0f, target - Math.Min(brake * GameCar.Step + car.PowerStep, before));
        }
    }

    // What a run measured.
    private struct DriveRun
    {
        public float Slowing;      // m/s2: the most speed lost over half a second, while above 3 m/s
        public float Handed;       // m/s: the speed the game's own stop took over at
        public float Least;        // metres: the least gap there ever was
        public float Rest;         // metres: the gap it came to rest at
        public float Braked;       // m/s2: the most the drivers braked with
        public bool Stopped;
    }

    /// <summary>
    /// One car driven for so many seconds behind a traffic car (its rear at thing0, moving at its speed and slowing at its
    /// braking to rest) and before a red light (NaN: none), with or without the drivers.
    /// </summary>
    private static DriveRun DriveBehind(bool drivers, float speed0, float thing0, float thingSpeed0, float thingBraking, float redLight, float seconds)
    {
        const float ThingHalfLength = 2.4f;   // metres
        var car = new GameCar { Speed = speed0, Nose = 0f, MaxSpeed = Math.Max(12.5f, speed0) };
        var model = new DriverModel();
        float thing = thing0;
        float thingSpeed = thingSpeed0;
        var past = new float[(int)(0.5f / GameCar.Step) + 1];
        int steps = (int)(seconds / GameCar.Step);
        var drive = new DriveRun { Least = float.MaxValue, Handed = -1f };
        for (int k = 0; k < steps; k++)
        {
            float t = k * GameCar.Step;
            // The drivers' look, eight times a second: what binds the car on its road (CityDrivers.Evaluate).
            if (drivers && model.Due(t, car.Nose))
            {
                int o = model.Obstacles.Add(DriverRules.Why.Car, 1, thing + ThingHalfLength, 0f, 1f, 0f, thingSpeed, 0f, ThingHalfLength, 1f, 0f);
                model.Obstacles.Slowing[o] = thingSpeed > 0f ? thingBraking : 0f;
                if (!float.IsNaN(redLight))
                {
                    float at = redLight - car.Nose;
                    if (at > -1f)
                    {
                        DriverRules.Consider(ref model.Best, DriverRules.Why.RedLight, -1, at, at - DriverRules.RedShort, 0f, 0f, car.Speed);
                    }
                }
                model.Decide(car);
            }
            float before = car.Speed;
            float target = car.Target(thing, thingSpeed, redLight, true, ThingHalfLength * 2f);
            if ((car.Stopping || car.AtLine) && drive.Handed < 0f)
            {
                drive.Handed = before;
            }
            // The drivers' brake, each physics step (DriverBrakes): never while the game itself stops the car.
            if (drivers)
            {
                target = model.Step(car, before, target, thingSpeed);
            }
            car.Speed = target;
            car.Nose += car.Speed * GameCar.Step;
            // The thing ahead.
            if (thingSpeed > 0f && thingBraking > 0f)
            {
                thingSpeed = Math.Max(0f, thingSpeed - thingBraking * GameCar.Step);
            }
            thing += thingSpeed * GameCar.Step;
            float gap = thing - car.Nose;
            if (!float.IsNaN(redLight))
            {
                gap = Math.Min(gap, redLight - car.Nose + car.AxleBehind);
            }
            drive.Least = Math.Min(drive.Least, gap);
            int slot = k % past.Length;
            float halfSecondAgo = k >= past.Length - 1 ? past[slot] : float.NaN;
            past[slot] = car.Speed;
            if (!float.IsNaN(halfSecondAgo) && halfSecondAgo > 3f)
            {
                drive.Slowing = Math.Max(drive.Slowing, (halfSecondAgo - car.Speed) / 0.5f);
            }
            drive.Rest = gap;
        }
        drive.Braked = model.Most;
        drive.Stopped = car.Speed < 0.05f;
        return drive;
    }

    private static void DriverSmoothness()
    {
        // Seen in good time: a car standing ahead, at the speeds the game's cars go (their prefabs: 35 to 100 km/h), and a
        // red light. The game alone stops each from full speed within 1.25 m of its sensor; with the drivers it slows early.
        float[] speeds = { 8f, 12.5f, 12.5f, 12.5f, 16f };
        float[] aheads = { 30f, 45f, 60f, 75f, 85f };
        float worstSlowing = 0f, worstHand = 0f, gameSlowing = float.MaxValue, gameHand = float.MaxValue, leastGap = float.MaxValue;
        for (int i = 0; i < speeds.Length; i++)
        {
            DriveRun with = DriveBehind(true, speeds[i], aheads[i], 0f, 0f, float.NaN, 20f);
            DriveRun alone = DriveBehind(false, speeds[i], aheads[i], 0f, 0f, float.NaN, 20f);
            worstSlowing = Math.Max(worstSlowing, with.Slowing);
            worstHand = Math.Max(worstHand, with.Handed);
            leastGap = Math.Min(leastGap, with.Least);
            gameSlowing = Math.Min(gameSlowing, alone.Slowing);
            gameHand = Math.Min(gameHand, alone.Handed);
            Check(with.Stopped && with.Rest > 1f && with.Rest < 4f, $"drivers' braking: from {speeds[i] * 3.6f:0} km/h behind a car standing {aheads[i]:0} m ahead it comes to rest {with.Rest:0.0} m behind (wanted 1 to 4)");
        }
        Check(worstSlowing <= DriverRules.Firm, $"drivers' braking: the most speed lost in half a second is {worstSlowing:0.0} m/s2 (an ordinary slowing: under {DriverRules.Firm:0.0})");
        Check(worstHand <= DriverRules.Handover + 0.5f, $"drivers' braking: the game's own stop takes over at {worstHand * 3.6f:0} km/h at the most (wanted walking pace, under {(DriverRules.Handover + 0.5f) * 3.6f:0})");
        Check(leastGap > 0.5f, $"drivers' braking: never nearer than {leastGap:0.0} m (no touch)");
        Check(gameSlowing > 15f && gameHand >= 7.9f, $"drivers' braking: the game alone stops at {gameSlowing:0} m/s2 or more, taking over at full speed ({gameHand * 3.6f:0} km/h): the check tells the two apart");

        // A red light: the game sees it one lane point ahead and stops in that.
        DriveRun light = DriveBehind(true, 12.5f, 1000f, 0f, 0f, 55f, 20f);
        DriveRun lightAlone = DriveBehind(false, 12.5f, 1000f, 0f, 0f, 55f, 20f);
        Check(light.Slowing <= DriverRules.Firm && light.Handed <= DriverRules.StopSpeed + 0.5f && light.Stopped,
            $"drivers' braking: for a red light 55 m ahead it slows at {light.Slowing:0.0} m/s2 at the most and is at {light.Handed * 3.6f:0} km/h when the game sees the light (wanted under {(DriverRules.StopSpeed + 0.5f) * 3.6f:0})");
        Check(lightAlone.Slowing > 10f, $"drivers' braking: the game alone brakes for it at {lightAlone.Slowing:0} m/s2 (it sees it 4.8 m off)");

        // A slower car ahead: closed on gently, then followed.
        DriveRun slower = DriveBehind(true, 12.5f, 30f, 5f, 0f, float.NaN, 20f);
        Check(slower.Slowing <= DriverRules.Firm && slower.Least > 2f, $"drivers' braking: behind a car at 18 km/h 30 m ahead it slows at {slower.Slowing:0.0} m/s2 and follows {slower.Least:0.0} m back at the nearest");

        // A car ahead that brakes to a stop: the drivers plan for where it will stand, and brake as hard as needed, never into it.
        DriveRun braking = DriveBehind(true, 12.5f, 20f, 12.5f, 2.5f, float.NaN, 15f);
        DriveRun brakingAlone = DriveBehind(false, 12.5f, 20f, 12.5f, 2.5f, float.NaN, 15f);
        Check(braking.Slowing <= DriverRules.Firm && braking.Least > 1f && braking.Stopped,
            $"drivers' braking: behind a car slowing to a stop at 2.5 m/s2 it slows at {braking.Slowing:0.0} m/s2 at the most and stops {braking.Rest:0.0} m behind it");
        Check(brakingAlone.Slowing > DriverRules.Firm, $"drivers' braking: the game alone, following it, stops at {brakingAlone.Slowing:0} m/s2 once it is under 8 km/h (the check tells the two apart)");
        DriveRun sudden = DriveBehind(true, 12.5f, 22f, 12.5f, 6f, float.NaN, 15f);
        Check(sudden.Least > 0.5f && sudden.Braked <= DriverRules.Most, $"drivers' braking: behind a car braking at 6 m/s2 it stops {sudden.Least:0.0} m short at the nearest, at {sudden.Braked:0.0} m/s2 at the most");

        // Seen late (something comes into the lane 14 m ahead at 45 km/h): firm, never more than the tyres give, and no touch.
        DriveRun late = DriveBehind(true, 12.5f, 14f, 0f, 0f, float.NaN, 10f);
        Check(late.Least > 0.5f && late.Braked <= DriverRules.Most, $"drivers' braking: for something 14 m ahead at 45 km/h it brakes at {late.Braked:0.0} m/s2 at the most (all it may: 7) and stops {late.Least:0.0} m short");
    }

    // ---------------------------------------------------------------- the room to park

    private static void DriverParkingRoom()
    {
        // The player slows from 22 km/h to a stop at 1.5 m/s2 in a kerb-side lane, beside a free space; a car follows. Under
        // 10 km/h the game offers the space and puts its own room behind the player's car: a box 7 m long reaching 4.8 m
        // behind its rear (VehicleParkingHelper.parallelParkingCollider), which the cars' sensors stop for. The game stops
        // its cars for the player's car itself too (it follows only its own cars).
        const float PlayerHalf = 2.4f;     // metres: half the player's car
        const float BoxBehind = 4.8f;      // metres the game's room reaches behind its rear
        const float BoxLength = 7f;        // metres: the room's length
        const float Slowing = 1.5f;        // m/s2 the player slows with
        const float Offered = 2.78f;       // m/s (10 km/h) under which the game offers the space
        // Close behind (the way the game's cars follow), and further back; with the space offered, and with none (the player
        // stops at the kerb to reverse into one the game does not offer).
        foreach (bool offers in new[] { true, false })
        foreach (float behind in new[] { 6f, 12f, 25f })
        {
            for (int drivers = 1; drivers >= 0; drivers--)
            {
                var car = new GameCar { Speed = 6f, Nose = 0f };
                var model = new DriverModel();
                float player = behind + PlayerHalf;   // the player's car's middle
                float playerSpeed = 6f;
                float rest = 0f;
                for (int k = 0; k < 1500; k++)
                {
                    float t = k * GameCar.Step;
                    bool offered = offers && playerSpeed < Offered;
                    float rear = player - PlayerHalf;
                    if (drivers == 1 && model.Due(t, car.Nose))
                    {
                        // As CityDrivers.ReadPlayer: the player's car with its gap (and the gap it keeps once at rest), and the room.
                        float keep = DriverRules.PlayerGap(playerSpeed, DriverRules.SensorDefault, true) - DriverRules.SensorDefault;
                        int o = model.Obstacles.Add(DriverRules.Why.PlayerRoom, -1, player, 0f, 1f, 0f, playerSpeed, 0f, PlayerHalf, 1f, keep);
                        model.Obstacles.Slowing[o] = playerSpeed > 0f ? Slowing : 0f;
                        model.Obstacles.KeepAtRest[o] = DriverRules.PlayerGap(0f, DriverRules.SensorDefault, true) - DriverRules.SensorDefault;
                        if (offered)
                        {
                            int box = model.Obstacles.Add(DriverRules.Why.Parking, -1, rear - BoxBehind + BoxLength / 2f, 0f, 1f, 0f, playerSpeed, 0f, BoxLength / 2f, 1f, 0f);
                            model.Obstacles.Slowing[box] = playerSpeed > 0f ? Slowing : 0f;
                        }
                        model.Decide(car);
                    }
                    float before = car.Speed;
                    float target = offered
                        ? car.Target(rear - BoxBehind, playerSpeed, float.NaN, false, BoxLength)
                        : car.Target(rear, playerSpeed, float.NaN, false, PlayerHalf * 2f);
                    if (drivers == 1)
                    {
                        target = model.Step(car, before, target, playerSpeed);
                    }
                    car.Speed = target;
                    car.Nose += car.Speed * GameCar.Step;
                    playerSpeed = Math.Max(0f, playerSpeed - Slowing * GameCar.Step);
                    player += playerSpeed * GameCar.Step;
                    rest = player - PlayerHalf - car.Nose;
                }
                string how = offers ? "as they park" : "stopping at the kerb with no space offered";
                if (drivers == 1)
                {
                    Check(rest >= DriverRules.RoomWanted && model.Most < DriverRules.Firm,
                        $"drivers' parking room: following {behind:0} m back, the car stops {rest:0.0} m behind the player's car {how} (wanted at least {DriverRules.RoomWanted:0.0}), braking at most {model.Most:0.0} m/s2");
                }
                else if (behind < 10f || !offers)
                {
                    Check(rest < DriverRules.RoomWanted, $"drivers' parking room: the game alone, following {behind:0} m back, stops {rest:0.0} m behind them {how}, inside the room (the check tells the two apart)");
                }
            }
        }
    }

    // ---------------------------------------------------------------- letting the player out

    private static void DriverLetOut()
    {
        // The player's car at the kerb starts pulling out; a car comes up the lane at 47 km/h, so many metres behind the line
        // 3 m behind it. The line goes up only once no car near would be caught by it (DriverRules.Blocks); a car that can stop
        // gently stops for it, the game's sensor holding it; one too close goes by first.
        const float Line = 100f;          // where the line is along the lane
        const float LineDepth = 0.8f;     // metres: the line's box along the lane
        foreach (float back in new[] { 120f, 60f, 35f, 25f })
        {
            for (int drivers = 1; drivers >= 0; drivers--)
            {
                var car = new GameCar { Speed = 13f, Nose = Line - back };
                var model = new DriverModel();
                bool up = false;
                int waited = 0;
                var past = new float[(int)(0.5f / GameCar.Step) + 1];
                float slowing = 0f;   // m/s2: the most speed the car lost over half a second
                for (int k = 0; k < 1200; k++)
                {
                    float t = k * GameCar.Step;
                    if (drivers == 1 && model.Due(t, car.Nose))
                    {
                        if (!up)
                        {
                            bool blocked = DriverRules.Blocks(car.Speed, Line - car.Nose, car.Sensor);
                            up = !blocked;
                            waited += blocked ? 1 : 0;
                        }
                        if (up)
                        {
                            model.Obstacles.Add(DriverRules.Why.HoldLine, -1, Line + LineDepth / 2f, 0f, 1f, 0f, 0f, 0f, LineDepth / 2f, 1.2f, 0f);
                        }
                        model.Decide(car);
                    }
                    float before = car.Speed;
                    float target = car.Target(up ? Line : float.MaxValue, 0f, float.NaN, false, LineDepth);
                    if (drivers == 1)
                    {
                        target = model.Step(car, before, target, 0f);
                    }
                    car.Speed = target;
                    car.Nose += car.Speed * GameCar.Step;
                    // (While above 3 m/s, as for the braking: the game's own stop from walking pace is its own.)
                    int slot = k % past.Length;
                    if (k >= past.Length - 1 && past[slot] > 3f)
                    {
                        slowing = Math.Max(slowing, (past[slot] - car.Speed) / 0.5f);
                    }
                    past[slot] = car.Speed;
                }
                bool letOut = car.Nose < Line;
                float most = model.Most;
                if (drivers == 0)
                {
                    Check(!letOut, $"drivers' let-out: the game alone, {back:0} m back, drives on past the player pulling out (the check tells the two apart)");
                }
                else if (back >= 35f)
                {
                    Check(letOut && Line - car.Nose >= 1f && most <= DriverRules.YieldMost + 0.6f && slowing <= DriverRules.YieldMost + 0.6f,
                        $"drivers' let-out: {back:0} m back at 47 km/h, it stops {Line - car.Nose:0.0} m short of the line ({Line - car.Nose + DriverRules.LetOutBack:0.0} m behind the player), "
                        + $"slowing at {slowing:0.0} m/s2 at the most (wanted under {DriverRules.YieldMost + 0.6f:0.0})");
                }
                else
                {
                    Check(!letOut && most < 0.01f && waited > 0, $"drivers' let-out: {back:0} m back it cannot stop gently: it goes by unbraked, the line waiting {waited} look(s) for it");
                }
            }
        }
    }

    // ---------------------------------------------------------------- the wave-through

    private static void DriverWave()
    {
        // Its sequence: on the post, a unit due is waved through; until it is past nothing else happens; called back meanwhile,
        // the officers board only once it is.
        var pass = CordonRules.Pass.Post;
        pass = CordonRules.NextPass(pass, due: false, over: false, recalled: false);
        Check(pass == CordonRules.Pass.Post, "wave-through: with no unit coming the officer keeps the post");
        pass = CordonRules.NextPass(pass, due: true, over: false, recalled: false);
        Check(pass == CordonRules.Pass.Waving, "wave-through: a unit coming up the open lane is waved through");
        pass = CordonRules.NextPass(pass, due: true, over: false, recalled: true);
        Check(pass == CordonRules.Pass.Waving, "wave-through: called back while waving, the officer stays out of the lane until it is past");
        Check(CordonRules.NextPass(pass, due: false, over: true, recalled: true) == CordonRules.Pass.Board, "wave-through: ...then boards");
        Check(CordonRules.NextPass(CordonRules.Pass.Waving, false, true, false) == CordonRules.Pass.Post, "wave-through: past, and not called back: back to the post");
        Check(CordonRules.NextPass(CordonRules.Pass.Post, true, false, true) == CordonRules.Pass.Board, "wave-through: called back first, no pass begins");

        Check(CordonRules.PassDue(30f, 0.4f, 0.98f) && CordonRules.PassDue(0f, 1f, 0.9f), "wave-through: a unit in the open lane 30 m before the post is due");
        Check(!CordonRules.PassDue(60f, 0.4f, 0.98f), "wave-through: not 60 m off");
        Check(!CordonRules.PassDue(-3f, 0.4f, 0.98f), "wave-through: not once its nose is past the post");
        Check(!CordonRules.PassDue(20f, 3.3f, 0.98f), "wave-through: not a unit in the next lane (the scene's own, in its blocked lane)");
        Check(!CordonRules.PassDue(20f, 0.4f, 0.3f), "wave-through: not one crossing the road");
        Check(CordonRules.PassOver(3.5f, false, 2f) && CordonRules.PassOver(-5f, true, 1f) && CordonRules.PassOver(-5f, false, 8f) && !CordonRules.PassOver(1f, false, 4f),
            "wave-through: over when its tail is 3 m past, it is gone, or after 8 s");

        // Where the officer steps to. The usual street: the wrecks in their lane (across -1 to 1), the tape 0.6 m outside them,
        // the open lane's line at -3.4, the ambulance standing in the wrecks' lane with its nose 6.55 m before them.
        float lane = -3.4f;
        float halfWidth = 1.22f;          // the game's ambulance
        var along = new float[2];
        var across = new float[2];
        // The old place alone: before the near barriers, 1.6 m in from the tape on the open side.
        along[0] = -4.6f;
        across[0] = -1.6f + 1.6f;
        int chosen = CordonRules.PassPlace(along, across, 1, lane, halfWidth, true, 0f, -8.55f, 1f, out float clear);
        Check(chosen == 0 && clear < CordonRules.PassClear, $"wave-through: the old place alone is {clear:0.0} m clear of the ambulance leaving round the wrecks (in its own lane, 2 m before its nose): the check tells it apart");
        // With the tape down, beside the wrecks' near end is the first choice.
        along[0] = 0.5f;
        across[0] = -1.6f + 0.35f;
        along[1] = -4.6f;
        across[1] = 0f;
        chosen = CordonRules.PassPlace(along, across, 2, lane, halfWidth, true, 0f, -8.55f, 1f, out clear);
        Check(chosen == 0 && clear >= CordonRules.PassClear, $"wave-through: beside the wrecks, {clear:0.0} m clear of its way (wanted at least {CordonRules.PassClear:0.0}): that is where the officer stands");
        float[] swappedAlong = { along[1], along[0] };
        float[] swappedAcross = { across[1], across[0] };
        chosen = CordonRules.PassPlace(swappedAlong, swappedAcross, 2, lane, halfWidth, true, 0f, -8.55f, 1f, out clear);
        Check(chosen == 1 && clear >= CordonRules.PassClear, "wave-through: a place in the leaving ambulance's way is passed over for the next one clear of it");
        // A unit coming up the open lane with the tape up: the old place before the barriers is clear of it.
        chosen = CordonRules.PassPlace(along, across, 1, lane, 1.0f, false, 0f, 0f, 1f, out clear);
        Check(chosen == 0 && clear >= CordonRules.PassClear, $"wave-through: for a patrol car coming up the open lane the place before the barriers is {clear:0.0} m clear of it");
        // A narrow street: the open lane hard by the wrecks. Nothing is clear: the clearest is taken, and it is said.
        chosen = CordonRules.PassPlace(along, across, 2, -2.2f, halfWidth, true, 0f, -8.55f, 1f, out clear);
        Check(chosen >= 0 && clear < CordonRules.PassClear, "wave-through: in a street too narrow for it the clearest place is taken (the self-check says FAIL)");

        // Over the sources: it is restored and wired, and the ordinary traffic's wave stays as it was.
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string officer = Source(scripts, @"People\SceneOfficer.cs");
        string cast = Source(scripts, @"People\AccidentCast.cs");
        string accidents = Source(scripts, @"Traffic\TrafficAccidents.cs") + Source(scripts, @"Traffic\TrafficAccidents.Leaving.cs") + Source(scripts, @"Traffic\TrafficAccidents.Wave.cs");
        Check(officer.Contains("public bool LetPass(") && officer.Contains("public void PassDone(") && officer.Contains("Duty.Pass") && officer.Contains("PointClear") && officer.Contains("PointEvery"),
            "wave-through: SceneOfficer.LetPass, PassDone, Duty.Pass, PointClear and PointEvery are back");
        Check(Body(cast, "public bool LetUnitPast(").Contains(".LetPass(") && Body(cast, "public bool LetUnitPast(").Contains("PassPost") && Body(cast, "public bool LetUnitPast(").Contains("PassBeside")
              && Body(cast, "public void UnitPast(").Contains(".PassDone("), "wave-through: AccidentCast.LetUnitPast sends the officer on the post to a pass place, UnitPast back");
        Check(Body(cast, "public void Recall(bool police)").Contains("_boardAfterPass = true"), "wave-through: a recall while a unit is waved through waits until it is past");
        Check(Body(accidents, "private void Depart(").Contains("StartWave(") && Body(accidents, "private void Tick()").Contains("WatchWave(now)")
              && Body(accidents, "private void WatchWave(").Contains("CordonRules.PassDue(") && Body(accidents, "private void WatchWave(").Contains("CordonRules.PassOver("),
            "wave-through: called for the ambulance leaving round the wrecks and for any unit with its lights on coming up the open lane, and ended when it is past");
        Check(Body(accidents, "private void Tick()").Contains("_people.WaveTrafficOn()"), "wave-through: waving ordinary traffic on stays as it was (TrafficAccidents.Tick)");
        string cordon = Source(scripts, @"Traffic\SceneCordon.Tape.cs");
        Check(Body(cordon, "private void Survey()").Contains("PassPost = StreetSpots.OnRoad(") && Body(cordon, "private void Survey()").Contains("PassBeside = StreetSpots.OnRoad("),
            "wave-through: SceneCordon.PassPost (and PassBeside) are worked out with their road query in the survey");
    }

    // ---------------------------------------------------------------- the option off

    private static void DriverOptionOff()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string folder = Path.Combine(scripts, "Traffic", "Drivers");
        string all = string.Empty;
        foreach (string file in Directory.GetFiles(folder, "*.cs"))
        {
            all += Code(File.ReadAllText(file));
        }
        Check(all.Length > 0, "drivers' sources: found at " + folder);
        // (The hold lines are the mod's own objects: their own body is set up where they are made.)
        all = all.Replace(Body(all, "private bool MakeLines("), string.Empty);
        // Nothing of a car, of the traffic system or of a lane point is ever written: a car let go of is the game's as it was.
        string[] writes =
        {
            ".velocity =", ".velocity=", "isKinematic =", "presetPath =", "temporaryDisabled =", ".stop =", ".giveWay =", ".maxSpeed =", "brakeTime =",
            "accelerationTime =", "minPossibleSpeed =", "maxPossibleSpeed =", "SetVehicleIgnored(", "ForceSetVehicleActiveAction(", "SetVehicleAction(",
            "SetVehicleTargetWaypoint(", "InteractionDelegate(", "VehicleUpdateWaypoint(", "LoadVehicle(", "RemoveVehicle(", ".enabled =", "MovePosition(", "MoveRotation("
        };
        string found = null;
        foreach (string write in writes)
        {
            if (all.Contains(write))
            {
                found = (found == null ? "" : found + ", ") + write;
            }
        }
        Check(found == null, "drivers' sources: nothing on a car, the traffic system or a lane point is written" + (found != null ? " (found " + found + ")" : ""));
        string brakes = Source(scripts, @"Traffic\Drivers\DriverBrakes.cs");
        Check(Count(all, "AddForce(") == Count(brakes, "AddForce(") && Count(brakes, "AddForce(") == Count(brakes, "ForceMode.VelocityChange"),
            "drivers' sources: the only thing done to a car is a change of velocity, in DriverBrakes alone");
        string director = Source(scripts, @"Traffic\Drivers\CityDrivers.cs");
        string release = Body(director, "public void ReleaseAll(string why)");
        Check(release.Contains("_brakes.Clear()") && release.Contains("DropLines(why)"), "drivers' sources: giving everything back clears every brake and takes every hold line away");
        string step = Body(director, "private void Step(float now)");
        Check(step.Contains("!Settings.CityDrivers") && step.Contains("!TrafficTools.Ready") && step.Contains("ReleaseAll("),
            "drivers' sources: the option off, indoors, a pause or a Going Public guest (TrafficTools.Ready) give everything back at the next look");
        Check(Body(director, "private void OnDisable()").Contains("ReleaseAll(") && Body(director, "private void OnDestroy()").Contains("ReleaseAll(")
              && Body(director, "private void OnDestroy()").Contains("DestroyLines()") && Body(director, "private void Fault(").Contains("ReleaseAll("),
            "drivers' sources: and so do its updates stopping, the mod closing and a fault");
        string game = Source(scripts, @"Traffic\Drivers\DriverGame.cs");
        Check(Body(game, "public void ParkLine(int slot)").Contains("Manager.TriggerColliderRemovedEvent(") && Body(game, "public void DestroyLines()").Contains("Manager.TriggerColliderRemovedEvent("),
            "drivers' sources: a hold line taken away is reported gone to the traffic system first, so the cars held for it drive on");
        string list = Source(scripts, @"Meta\FeatureList.cs");
        Check(list.Contains("CityDrivers.Instance?.ReleaseAll(") && list.Contains("Settings.CityDriversId"), "drivers' sources: the options page's clean-up gives everything back too");
        Check(DriverRules.HoldOver(0f, 0f, false, true), "drivers: a line comes down the moment the player is gone (the option off is handled as gone)");
        // The frame: one comparison while idle, the guard and the hot spot round it, no search or query in it.
        string update = Body(director, "private void Update()") + Body(director, "private void OnUpdate()");
        Check(update.Contains("HotSpots.Time(") && update.Contains("FrameGuard.Off") && Body(director, "private void OnUpdate()").Contains("if (!_evaluating && now < _next)"),
            "drivers' sources: its frame opens its hot spot and guard, and costs one comparison while idle");
        string look = Source(scripts, @"Traffic\Drivers\CityDrivers.Look.cs") + Source(scripts, @"Traffic\Drivers\CityDrivers.Player.cs") + director;
        Check(!look.Contains("GetComponent") && !look.Contains("Physics.") && !look.Contains("FindObject") && !look.Contains("GameObject.Find"),
            "drivers' sources: a look asks for no component and makes no physics query or search (what it needs is measured once, in DriverGame)");
    }

    // ---------------------------------------------------------------- what a look costs

    private static void DriverTiming()
    {
        // One look's rules: 40 cars, each with a road of 13 points through streets of 4.8 m lane points, among 50 things.
        var random = new Random(7);
        var road = new DriverRules.Road(DriverRules.PathPoints + 2);
        var obstacles = new DriverRules.Obstacles(DriverRules.MostObstacles);
        for (int i = 0; i < 50; i++)
        {
            bool eastWest = i % 2 == 0;
            float a = (float)(random.NextDouble() * 170.0 - 85.0);
            float laneAt = (i % 4 < 2 ? -30f : 30f) + (i % 8 < 4 ? -1.7f : 1.7f);
            obstacles.Add(DriverRules.Why.Car, i, eastWest ? a : laneAt, eastWest ? laneAt : a, eastWest ? 1f : 0f, eastWest ? 0f : 1f, 0f, 0f, 2.4f, 1f, 0f);
        }
        var cx = new float[40];
        var cz = new float[40];
        var fx = new float[40];
        var fz = new float[40];
        var speed = new float[40];
        for (int i = 0; i < 40; i++)
        {
            int j = i + 5;
            fx[i] = obstacles.Fx[j];
            fz[i] = obstacles.Fz[j];
            cx[i] = obstacles.X[j] + fx[i] * 2.4f;
            cz[i] = obstacles.Z[j] + fz[i] * 2.4f;
            speed[i] = (float)random.NextDouble() * 14f;
        }
        // Warmed first: the runtime compiles the rules' methods in full only once it has seen them run for a while, in the
        // background (tiered compilation), and the game's runtime has no such first tier.
        Looks(2000, road, obstacles, cx, cz, fx, fz, speed);
        System.Threading.Thread.Sleep(300);
        Looks(500, road, obstacles, cx, cz, fx, fz, speed);
        int bound = 0;
        double best = double.MaxValue;
        long allocated = 0L;
        var watch = new Stopwatch();
        for (int batch = 0; batch < 7; batch++)
        {
            watch.Restart();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bound += Looks(500, road, obstacles, cx, cz, fx, fz, speed);
            allocated = Math.Max(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds * 1000.0 / 500);
        }
        Check(bound > 0, "drivers' timing: the timed looks found cars bound by what is ahead (" + bound + ")");
        Check(allocated == 0L, "drivers' timing: a look's rules allocate nothing (" + allocated + " bytes over 500 looks)");
        Check(best < 20.0, "drivers' timing: the rules of one look at 40 cars among 50 things take " + best.ToString("0.0") + " microseconds here (under 20 wanted: the game's runtime is 4 to 8 times slower, spread over "
                           + (40 / DriverRules.SliceCars + 1) + " frames, and the look's engine reads come on top)");
        Console.WriteLine("      drivers: one look's rules for 40 cars among 50 things: " + best.ToString("0.00") + " microseconds (best of 7 batches of 500)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int Looks(int looks, DriverRules.Road road, DriverRules.Obstacles obstacles, float[] cx, float[] cz, float[] fx, float[] fz, float[] speed)
    {
        int bound = 0;
        for (int look = 0; look < looks; look++)
        {
            for (int c = 0; c < cx.Length; c++)
            {
                road.Begin(cx[c], cz[c], fx[c], fz[c]);
                for (int p = 1; p <= 13; p++)
                {
                    road.Add(cx[c] + fx[c] * 4.8f * p, cz[c] + fz[c] * 4.8f * p);
                }
                var best = DriverRules.Binding.None;
                DriverRules.Consider(ref best, DriverRules.Why.RedLight, -1, 40f, 35f, DriverRules.StopSpeed, 0f, speed[c]);
                DriverRules.Worst(ref best, road, obstacles, c + 5, 1f, speed[c], 3.3f);
                bound += DriverRules.Slows(best.Need, false) ? 1 : 0;
            }
        }
        return bound;
    }

    // ---------------------------------------------------------------- the words

    private static readonly string[] DriverKeys =
    {
        "reallife_city_drivers", "reallife_opt_tip_city_drivers", "reallife_test_drivers_park", "reallife_test_drivers_letout", "reallife_test_wave_through",
        "reallife_test_drivers_stop_first", "reallife_test_drivers_kerb_first", "reallife_test_drivers_no_room", "reallife_test_drivers_park_started",
        "reallife_test_drivers_letout_started", "reallife_test_wave_none", "reallife_test_wave_started", "reallife_test_wave_no_unit"
    };

    private static void DriverWords()
    {
        string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var words = new Dictionary<string, Dictionary<string, string>>();
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru" })
        {
            var map = new Dictionary<string, string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, code + ".json")), new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    map[property.Name] = property.Value.GetString();
                }
            }
            words[code] = map;
        }
        var missing = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            foreach (string key in DriverKeys)
            {
                if (!words[code].TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                {
                    missing.Add(code + ":" + key);
                }
            }
        }
        Check(missing.Count == 0, "drivers' words: every text is in en, pt and zh-cn" + (missing.Count > 0 ? " (" + string.Join(", ", missing) + ")" : string.Empty));
        if (missing.Count > 0)
        {
            return;
        }
        bool kept = true, brief = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            foreach (string key in new[] { "reallife_test_drivers_park_started", "reallife_test_drivers_letout_started" })
            {
                kept &= words[code][key].Contains("{metres}") && words[code][key].Contains("{seconds}");
            }
        }
        foreach (string key in DriverKeys)
        {
            brief &= words["en"][key].Length <= 240;
        }
        Check(kept, "drivers' words: the test notices keep {metres} and {seconds} in every language");
        Check(brief, "drivers' words: every English notice is 240 characters at the most");
        // The Guide says what the city's drivers do, and that the officer waves a unit through, in every language.
        var lacking = new List<string>();
        foreach (KeyValuePair<string, Dictionary<string, string>> language in words)
        {
            string streets = language.Value["reallife_guide_streets"];
            string traffic = language.Value["reallife_guide_traffic"];
            if (!streets.Contains("{drivers_gap}") || !streets.Contains("{drivers_room}") || !streets.Contains("{drivers_letout}") || !streets.Contains("{drivers_hold}")
                || traffic.Split('\n').Length != words["en"]["reallife_guide_traffic"].Split('\n').Length
                || streets.Split('\n').Length != words["en"]["reallife_guide_streets"].Split('\n').Length)
            {
                lacking.Add(language.Key);
            }
        }
        Check(lacking.Count == 0, "drivers' words: the Guide's streets page tells of the city's drivers with their numbers, and the traffic page of the wave-through, line for line in every language"
                                  + (lacking.Count > 0 ? " (lacking: " + string.Join(", ", lacking) + ")" : string.Empty));
        Check(words["en"]["reallife_guide_traffic"].Contains("waved through") && words["en"]["reallife_guide_streets"].Contains("'" + words["en"]["reallife_city_drivers"] + "'"),
            "drivers' words: the English pages name the option as the options page does, and the wave-through");
        // What's new: the feature's line, in some row of every language (it moves from the next row to its version's).
        var whatsNew = new Dictionary<string, string>
        {
            ["en"] = "Traffic near you now brakes in good time", ["pt"] = "O trânsito perto de você agora freia com antecedência", ["zh-cn"] = "你附近的车流现在会提前平稳地刹车",
            ["de"] = "Der Verkehr in deiner Nähe bremst jetzt rechtzeitig", ["fr"] = "La circulation près de vous freine désormais à temps",
            ["es"] = "El tráfico cerca de ti ahora frena a tiempo", ["ru"] = "Машины рядом с вами теперь тормозят заранее"
        };
        var without = new List<string>();
        foreach (KeyValuePair<string, string> line in whatsNew)
        {
            bool found = false;
            foreach (KeyValuePair<string, string> row in words[line.Key])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(line.Value);
            }
            if (!found)
            {
                without.Add(line.Key);
            }
        }
        Check(without.Count == 0, "drivers' words: What's new has the feature's line in some reallife_changes_ row of every language" + (without.Count > 0 ? " (lacking: " + string.Join(", ", without) + ")" : string.Empty));
    }
}
