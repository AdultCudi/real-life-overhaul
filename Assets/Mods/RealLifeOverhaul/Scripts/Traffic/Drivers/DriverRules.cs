using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of the city's drivers in plain numbers: how far ahead a driver looks, when they begin to slow for what is
    /// in the road ahead and how hard, the room kept behind the player's car, when the player's car is pulling out of a
    /// space, which cars let it out, and when a hold line may go up and must come down. The traffic system stops for what
    /// its sensor touches within 1.25 m (Gley's DrivingAI.GetActionValue); these bring a car to that sensor at walking pace,
    /// so the game's own stop is gentle. Game-free, so NavCheck's DriverCheck runs every rule against a model of that stop.
    /// </summary>
    public static partial class DriverRules
    {
        // When and where they look.
        public const float TickSeconds = 0.125f;     // the street is looked at eight times a second
        public const float IdleSeconds = 0.5f;       // ...and twice a second while there is nothing to look at
        public const float Near = 90f;               // metres from the player inside which cars are driven with care (nobody sees further)
        public const float ObstaclesNear = 150f;     // metres from the player inside which vehicles are things in somebody's way
        public const int MostCars = 96;              // cars looked at in one look
        public const int SliceCars = 12;             // cars whose road is read in one frame: a look takes a few frames
        public const int PathPoints = 14;            // lane points of a car's road ahead read at the most (about 60 m)
        public const int MostObstacles = 112;        // vehicles, the player, the hold lines

        // The driver.
        public const float Reaction = 0.4f;          // seconds before a driver's foot is on the brake
        public const float Comfort = 2.5f;           // m/s2 of an ordinary slowing in good time
        public const float StartFrom = 1.2f;         // m/s2 needed at which a driver begins to slow
        public const float KeepFrom = 0.5f;          // ...and under which one who has begun lets the brake off
        public const float Firm = 4.5f;              // m/s2 from which it is a firm stop, put on at once
        public const float Most = 7f;                // m/s2 the most a careful driver uses (the tyres give about 9)
        public const float Rise = 6f;                // m/s3 an ordinary slowing comes on with: no jolt
        public const float Overrun = 1000f;          // m/s2 "needed" at the most: more than any limit
        public const float RoomLeast = 0.5f;         // metres of room a need is worked over at the least: inside it a crawl stays a crawl
        public const float DropBack = 3f;            // seconds a car inside the gap it keeps behind the player takes to open it again
        public const float Settle = 1f;              // seconds it takes to come to that speed
        public const float Handover = 2f;            // m/s a car is brought to at the game's sensor: the game's own 1.25 m stop is then 1.6 m/s2
        public const float GameStop = 1.25f;         // metres the traffic system stops in once its sensor touches something
        public const float SensorDefault = 3.3f;     // metres past the nose the game's sensor reaches (a 4.5 m box from the front axle)
        public const float SensorLeast = 1.5f;       // ...the least a measurement is believed
        public const float SensorMost = 6f;          // ...and the most
        public const float SightLeast = 10f;         // metres: no road ahead is read shorter...
        public const float SightMost = 60f;          // ...or longer
        public const float StraightFrom = 0.87f;     // the cosine within which every leg of a road runs as its first does (30 degrees): a straight road
        public const float PathMargin = 0.2f;        // metres beyond a car's side that are still in its way
        public const float BehindNose = 1.5f;        // metres behind a car's nose from which something no longer counts as ahead
        public const float MovingFrom = 0.5f;        // m/s from which what is ahead is moving
        public const float SlowingFrom = 0.3f;       // m/s2 from which what is ahead counts as slowing
        public const float NearReach = 10f;          // metres round a car's road inside which something may be in its way at all (the bus's half length and two half widths)

        // The road.
        public const float StopTake = 5f;            // metres before a give-way line at which the traffic system holds the car itself (one lane point)
        public const float StopSpeed = 3f;           // m/s a car is brought to by then: it creeps up to the line and goes when it may
        public const float RedShort = 1f;            // metres short of a red light's stop point a car is brought to rest (the traffic system holds a
                                                     // car standing within 10 m of it, and would speed it up again with room)
        public const float ClosedShort = 2f;         // metres short of the last open lane point at which the traffic system stops dead
        public const float ClosedSpeed = 1.5f;       // m/s by then: the dead stop is from walking pace
        public const float LimitShort = 1.5f;        // metres before a slower lane point at which the traffic system brakes for it
        public const float BendGrip = 3f;            // m/s2 sideways a city driver takes a corner with: a brisk, comfortable turn
        public const float BendFrom = 20f;           // degrees a road turns within a bend's reach for it to be slowed for
        public const float BendReach = 12f;          // metres of road a bend is measured over (a junction's curve is many small steps)
        public const float BendLead = 2f;            // metres before the bend's start the car is at the bend's speed
        public const float StopGap = 2f;             // metres short of something a car is brought to rest at when the game's sensor cannot hold it
        public const float HoldWithin = 0.5f;        // metres of room left inside which a car brought to rest short of something standing is held there

        // The player.
        public const float PlayerTime = 1.5f;        // seconds kept behind the player's car while it moves
        public const float SlowPlayer = 4.2f;        // m/s (15 km/h): slower than this in a kerb-side lane, the player may be about to park
        public const float ParkRoom = 8.5f;          // metres from the player's rear to the nose of the car behind, kept for parking: past the game's
                                                     // own room (4.8 m) and a sensor, so that room never comes up inside a car's stop
        public const float RoomWanted = 6.5f;        // ...and the least a stop behind the parking player passes the self-check with
        public const float ReversingFrom = 0.3f;     // m/s backwards from which the player's car is reversing
        public const float ReversingMost = 2.8f;     // m/s (10 km/h): faster than that it is not a manoeuvre
        public const float LetOutBack = 3f;          // metres behind the player's rear (along the lane) that a car letting them out stops
        public const float YieldMost = 3f;           // m/s2: a car that would need more than this to stop for the line goes by first
        public const float HoldMost = 15f;           // seconds a hold line stays up at the most
        public const float GaveUpAfter = 4f;         // seconds the player's car stands still before its line comes down
        public const float StillWithin = 6f;         // seconds: a car that stood still this recently is setting off, not driving along
        public const float OutWithin = 0.9f;         // metres from the lane's line at which the player's car is in the lane
        public const float OutAngle = 20f;           // degrees from the lane's way at which it is lined up in it
        public const float OutAhead = 6f;            // metres past the line at which a car out in the lane is away

        // The lane.
        public const float LaneHalf = 1.75f;         // metres either side of a lane's line
        public const float KerbFrom = 1.2f;          // metres from a lane's line at which a car's middle is out of that lane
        public const float KerbMost = 5.5f;          // ...and within which it is still beside it (a parking lane and a little)
        public const float NoseNear = 1f;            // metres outside the lane's edge within which its near corner is at the lane
        public const float TowardsFrom = 4f;         // degrees towards the lane from which a car is pointing out
        public const float SideSpeedFrom = 0.15f;    // m/s towards the lane from which a car is moving out
        public const float CreepFrom = 0.2f;         // m/s from which the player's car is moving
        public const float CreepMost = 4f;           // m/s: faster than that it is not setting off from a space

        /// <summary>What a driver brakes for, most pressing first in no order: for the log and the self-check lines.</summary>
        public enum Why { None, Car, Player, PlayerRoom, Parking, OnFoot, Bus, HoldLine, RedLight, GiveWay, Closed, Limit, Bend }

        /// <summary>Metres a car at that speed covers while it stops with that braking.</summary>
        public static float StoppingDistance(float speed, float braking)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            return speed * speed / (2f * (braking < 0.1f ? 0.1f : braking));
        }

        /// <summary>How far ahead of its nose a driver reads the road: a reaction, an ordinary slowing and a third, the sensor's reach and 4 m; 10 to 60 m.</summary>
        public static float Sight(float speed, float sensor)
        {
            if (speed < 0f)
            {
                speed = 0f;
            }
            float sight = speed * Reaction + StoppingDistance(speed, Comfort) * 1.3f + sensor + 4f;
            return sight < SightLeast ? SightLeast : sight > SightMost ? SightMost : sight;
        }

        /// <summary>The sensor's reach past the nose as measured, or the usual one when the measurement is not to be believed.</summary>
        public static float Sensor(float measured)
        {
            return measured >= SensorLeast && measured <= SensorMost ? measured : SensorDefault;
        }

        /// <summary>
        /// The braking (m/s2) that brings a car from <paramref name="speed"/> to <paramref name="pointSpeed"/> in
        /// <paramref name="room"/> metres, while what it comes up behind moves on at <paramref name="ahead"/>: worked in
        /// that thing's own frame, so a car behind one that drives on needs only what closes the gap. 0 when no slowing is
        /// needed; the room is never taken as under <see cref="RoomLeast"/>, so a crawl at the end stays a crawl. Inside a
        /// gap kept beyond the game's sensor (<paramref name="keep"/>: behind the player) the car drops back to open it again.
        /// </summary>
        public static float Need(float speed, float room, float pointSpeed, float ahead, float keep = 0f)
        {
            if (ahead < 0f)
            {
                ahead = 0f;   // something coming the other way, or across: taken as standing
            }
            float closing = speed - ahead;
            float wanted = pointSpeed - ahead;
            if (wanted < 0f)
            {
                wanted = 0f;
            }
            if (keep > 0f && room < 0f)
            {
                // Under the speed of what is ahead by as much as the gap is short over DropBack, never more than the gap.
                float target = wanted + (room < -keep ? -keep : room) / DropBack;
                return closing <= target ? 0f : Math.Min(Overrun, (closing - target) / Settle);
            }
            if (closing <= wanted)
            {
                return 0f;
            }
            float need = (closing * closing - wanted * wanted) / (2f * (room < RoomLeast ? RoomLeast : room));
            return need > Overrun ? Overrun : need;
        }

        /// <summary>
        /// The braking to put on this physics step, m/s2: none until <see cref="StartFrom"/> is needed, then what is needed, up
        /// to <see cref="Most"/>; an ordinary slowing comes on at <see cref="Rise"/>, a firm one at once. One that has begun
        /// keeps on until under <see cref="KeepFrom"/>, and one being brought to rest at its holding place until it stands.
        /// </summary>
        /// <param name="braking">The car was being slowed for this at the last step.</param>
        /// <param name="applied">What was put on at the last step, m/s2.</param>
        /// <param name="holding">It is at the place it is held at (nothing of the game's would stop it there).</param>
        public static float Brake(float need, bool braking, float applied, float step, bool holding = false)
        {
            if (holding)
            {
                need = need < KeepFrom ? KeepFrom : need;
            }
            else if (need <= 0f || (!braking && need < StartFrom) || (braking && need < KeepFrom))
            {
                return 0f;
            }
            float wanted = need > Most ? Most : need;
            if (wanted >= Firm)
            {
                return wanted;
            }
            float rose = (applied < 0f ? 0f : applied) + Rise * step;
            return wanted < rose ? wanted : rose;
        }

        /// <summary>True if a slowing of that need is an ordinary one, not a firm stop.</summary>
        public static bool Gentle(float need) => need > 0f && need < Firm;

        /// <summary>True if a car is to be slowed for what binds it: it begins at <see cref="StartFrom"/>, and one that has begun keeps on to <see cref="KeepFrom"/>.</summary>
        public static bool Slows(float need, bool slowing) => need > 0f && need >= (slowing ? KeepFrom : StartFrom);

        /// <summary>True if a car is to be held standing where it is: at what it was brought to rest short of, which stands, with nothing of the game's to hold it.</summary>
        public static bool Held(in Binding best) => best.Hold && best.Room <= HoldWithin;

        /// <summary>
        /// One physics step of a named car's slowing (DriverBrakes): the room left after the step's travel, and the braking
        /// (m/s2) to put on now. <paramref name="braking"/> and <paramref name="applied"/> carry from step to step; a car
        /// that is to be held (<paramref name="hold"/>) is brought to rest once within <see cref="HoldWithin"/> of its place.
        /// </summary>
        public static float StepBrake(ref float room, ref bool braking, ref float applied, float speed, float ahead, float pointSpeed, float keep, bool hold, float step)
        {
            if (ahead < 0f)
            {
                ahead = 0f;
            }
            room -= (speed - ahead) * step;   // what it has come up since the look
            if (speed <= 0.05f)
            {
                applied = 0f;
                return 0f;
            }
            float brake = Brake(Need(speed, room, pointSpeed, ahead, keep), braking, applied, step, hold && room <= HoldWithin);
            braking = brake > 0f;
            applied = brake;
            return brake;
        }

        /// <summary>
        /// Metres the car behind keeps behind the player's car: the game's sensor, a time gap while it moves, and the room to
        /// park while it is slow in a kerb-side lane.
        /// </summary>
        public static float PlayerGap(float playerSpeed, float sensor, bool kerbLane)
        {
            float gap = sensor + (playerSpeed > 0f ? PlayerTime * playerSpeed : 0f);
            if (kerbLane && playerSpeed < SlowPlayer && gap < ParkRoom)
            {
                gap = ParkRoom;
            }
            return gap;
        }

        /// <summary>How far across a line a rectangle reaches either side, by its half length and half width and the angle between them.</summary>
        public static float Across(float halfLength, float halfWidth, float cosine, float sine)
        {
            return Math.Abs(sine) * halfLength + Math.Abs(cosine) * halfWidth;
        }

        /// <summary>How far along a line a rectangle reaches either side of its middle.</summary>
        public static float Along(float halfLength, float halfWidth, float cosine, float sine)
        {
            return Math.Abs(cosine) * halfLength + Math.Abs(sine) * halfWidth;
        }

        /// <summary>True if something that far to the side of a car's road, reaching that far across it, is in the car's way.</summary>
        public static bool InWay(float lateral, float carHalfWidth, float reachAcross)
        {
            float wide = carHalfWidth + reachAcross + PathMargin;
            return lateral <= wide && lateral >= -wide;
        }

        /// <summary>
        /// The speed (m/s) a bend is taken at: the road turns that many degrees over that many metres (its radius follows), and
        /// a driver keeps to <see cref="BendGrip"/> sideways. MaxValue for a gentle one.
        /// </summary>
        public static float BendSpeed(float angle, float over)
        {
            if (angle < 0f)
            {
                angle = -angle;
            }
            if (angle < BendFrom || over < 0.5f)
            {
                return float.MaxValue;
            }
            float radius = over / (angle * (float)(Math.PI / 180.0));
            return (float)Math.Sqrt(BendGrip * radius);
        }

        // ---------------------------------------------------------------- the player's car at the kerb

        /// <summary>
        /// True if the player's car is pulling out of a space beside a lane: its middle offLine metres from the lane's line (out
        /// of the lane but beside it, either side), its nearest corner nearCorner metres from it (at the lane's edge), setting off
        /// slowly (speed m/s along the way it faces, sinceStill seconds after it stood still) and pointing towards the lane
        /// (towards degrees; negative: away) or moving towards it (sideSpeed m/s).
        /// </summary>
        public static bool PullingOut(float offLine, float nearCorner, float speed, float towards, float sideSpeed, float sinceStill)
        {
            if (offLine < KerbFrom || offLine > KerbMost || nearCorner > LaneHalf + NoseNear)
            {
                return false;
            }
            if (speed < CreepFrom || speed > CreepMost || sinceStill > StillWithin)
            {
                return false;
            }
            return towards >= TowardsFrom || sideSpeed >= SideSpeedFrom;
        }

        /// <summary>True once the player's car is out: in the lane and lined up with it, moving, or well past the line.</summary>
        /// <param name="pastLine">Metres its rear is past the hold line along the lane.</param>
        public static bool PulledOut(float offLine, float angle, float speed, float pastLine)
        {
            bool inLane = offLine <= OutWithin && angle <= OutAngle && angle >= -OutAngle;
            return (inLane && speed >= MovingFrom * 3f) || (inLane && pastLine >= OutAhead) || pastLine >= OutAhead * 2f;
        }

        /// <summary>
        /// True if a car coming up the lane lets the player out: it can stop for the line with an ordinary slowing (at most
        /// <see cref="YieldMost"/>), and its sensor is not on the line already. False: it goes by first.
        /// </summary>
        /// <param name="toLine">Metres from its nose to the line along its road.</param>
        public static bool Yields(float speed, float toLine, float sensor)
        {
            if (toLine <= sensor + 1f)
            {
                return speed < MovingFrom;   // at the line already: only one that stands there holds
            }
            return Need(speed, toLine - sensor, Handover, 0f) <= YieldMost;
        }

        /// <summary>True if a line may go up now: no car that cannot stop for it gently is near enough to be caught by it.</summary>
        /// <param name="toLine">Metres from that car's nose to the line along its road; negative once past it.</param>
        public static bool Blocks(float speed, float toLine, float sensor)
        {
            if (toLine < -BehindNose)
            {
                return false;   // past it
            }
            return toLine <= Sight(speed, sensor) && !Yields(speed, toLine, sensor);
        }

        /// <summary>True when a line must come down: held long enough, the player stopped trying, out and away, or gone.</summary>
        public static bool HoldOver(float heldFor, float stillFor, bool done, bool gone)
        {
            return gone || done || heldFor >= HoldMost || stillFor >= GaveUpAfter;
        }

        /// <summary>True if the player's car is reversing as a manoeuvre: backwards, at walking pace or a little more.</summary>
        public static bool Reversing(float speed) => speed <= -ReversingFrom && speed >= -ReversingMost;

        /// <summary>True for a kerb-side lane: lanes the same way lie on at most one side of it.</summary>
        /// <param name="leftLanes">Lanes running the same way to its left.</param>
        /// <param name="rightLanes">...and to its right.</param>
        public static bool KerbSide(int leftLanes, int rightLanes) => leftLanes == 0 || rightLanes == 0;
    }
}
